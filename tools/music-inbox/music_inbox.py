"""Music inbox: turns downloaded tracks (Suno or a music API) into game-ready cues (ADR-0011).

For every audio file in the inbox it
  1. matches a cue id from the cue sheet (file name, then the title tag),
  2. for looping cues finds a loop (bar-snapped when the cue sheet gives a BPM), crossfades the seam
     and splits the track into <cue>__intro.wav and <cue>.wav,
  3. sets one gain for intro and loop so the loop measures -16 LUFS, true peak <= -1.5 dBTP,
  4. writes 48 kHz 16-bit WAV into the Unity Resources folder, a seam audition file and a report,
  5. appends a row to the licence ledger and moves the source into the archive.

Needs Python 3.10+, numpy and ffmpeg/ffprobe on PATH. Never overwrites an existing cue unless --replace.

  python music_inbox.py --inbox D:/.../audio/_inbox --cue-sheet design/audio/cue-sheet.csv \
      --out Assets/_Project/Resources/Audio/Music --work D:/.../audio --ledger design/audio/LICENSE_LEDGER.csv
"""
from __future__ import annotations

import argparse
import csv
import datetime as dt
import hashlib
import json
import re
import shutil
import subprocess
import sys
import wave
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np

RATE = 48000
TARGET_LUFS = -16.0
TRUE_PEAK_DB = -1.5
CROSSFADE_S = 0.08
AUDIO_EXT = {".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac"}
LOOP_SCORE_OK = 0.90
LEDGER_FIELDS = ["cue_id", "source_file", "sha256", "engine", "account_tier", "generation_id",
                 "download_channel", "downloaded_at", "terms_snapshot", "processed_at", "loop_score", "status"]


@dataclass
class Cue:
    cue_id: str
    source: str = "suno"
    loop: bool = True
    bpm: float | None = None
    beats_per_bar: int = 4
    loop_start_s: float | None = None
    loop_end_s: float | None = None
    extra: dict = field(default_factory=dict)


def read_cue_sheet(path: Path) -> dict[str, Cue]:
    cues = {}
    with path.open(encoding="utf-8-sig", newline="") as f:
        for row in csv.DictReader(f):
            cid = (row.get("cue_id") or "").strip()
            if not cid or cid.startswith("#"):
                continue

            def num(key):
                v = (row.get(key) or "").strip()
                return float(v) if v else None

            cues[cid] = Cue(
                cue_id=cid,
                source=(row.get("source") or "suno").strip(),
                loop=(row.get("loop") or "yes").strip().lower() in ("yes", "y", "true", "1", "是"),
                bpm=num("bpm"),
                beats_per_bar=int(num("beats_per_bar") or 4),
                loop_start_s=num("loop_start_s"),
                loop_end_s=num("loop_end_s"),
                extra=row,
            )
    return cues


def match_cue(name: str, title: str | None, cues: dict[str, Cue]) -> Cue | None:
    """Longest cue id that appears as a whole token in the file name, else in the title tag."""
    for text in (name, title or ""):
        text = text.lower()
        hits = [c for c in cues.values() if re.search(r"(?<![a-z0-9])" + re.escape(c.cue_id.lower()) + r"(?![a-z0-9])", text)]
        if hits:
            return max(hits, key=lambda c: len(c.cue_id))
    return None


# ---------- ffmpeg io ----------

def probe_title(path: Path) -> str | None:
    try:
        out = subprocess.run(["ffprobe", "-v", "quiet", "-print_format", "json", "-show_format", str(path)],
                             capture_output=True, text=True, check=True).stdout
        tags = json.loads(out).get("format", {}).get("tags", {})
        return next((v for k, v in tags.items() if k.lower() == "title"), None)
    except (subprocess.CalledProcessError, json.JSONDecodeError):
        return None


def decode(path: Path) -> np.ndarray:
    """Float32 stereo at 48 kHz, shape (n, 2)."""
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-ac", "2", "-ar", str(RATE), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype="<f4").reshape(-1, 2).copy()


def write_wav(path: Path, audio: np.ndarray, seed: int = 0) -> None:
    rng = np.random.default_rng(seed)
    dither = (rng.random(audio.shape) - rng.random(audio.shape)) / 32768.0  # TPDF, 1 LSB
    pcm = np.clip((audio + dither) * 32767.0, -32768, 32767).astype("<i2")
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())


def measure(audio: np.ndarray) -> tuple[float, float]:
    """Integrated loudness (LUFS) and true peak (dBTP) via ffmpeg ebur128."""
    proc = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-f", "f32le", "-ac", "2", "-ar", str(RATE), "-i", "-",
                           "-af", "ebur128=peak=true", "-f", "null", "-"],
                          input=audio.astype("<f4").tobytes(), capture_output=True, check=True)
    log = proc.stderr.decode("utf-8", "replace")
    summary = log[log.rfind("Summary:"):]
    lufs = float(re.search(r"I:\s+(-?[\d.]+|-inf) LUFS", summary).group(1).replace("-inf", "-70"))
    peak = re.search(r"Peak:\s+(-?[\d.]+|-inf) dBFS", summary)
    return lufs, float(peak.group(1).replace("-inf", "-70")) if peak else -70.0


# ---------- loop search ----------

def frame_features(mono: np.ndarray, hop: int = 1024, size: int = 4096) -> np.ndarray:
    """Log band energies per frame (24 log-spaced bands), L2-normalised."""
    n = 1 + max(0, (len(mono) - size) // hop)
    idx = np.arange(size)[None, :] + hop * np.arange(n)[:, None]
    frames = mono[idx] * np.hanning(size)[None, :]
    spec = np.abs(np.fft.rfft(frames, axis=1))
    freqs = np.fft.rfftfreq(size, 1.0 / RATE)
    edges = np.geomspace(60, 16000, 25)
    bands = np.stack([spec[:, (freqs >= lo) & (freqs < hi)].sum(axis=1) for lo, hi in zip(edges[:-1], edges[1:])], axis=1)
    feats = np.log1p(bands)
    return feats / (np.linalg.norm(feats, axis=1, keepdims=True) + 1e-9)


def music_end(mono: np.ndarray) -> int:
    """Sample where the track stops being 'full': skip trailing silence and fade-out (RMS below -18 dB of median)."""
    win = RATE // 4
    rms = np.sqrt(np.convolve(mono ** 2, np.ones(win) / win, mode="same") + 1e-12)
    ref = np.median(rms[rms > 1e-4]) if np.any(rms > 1e-4) else 1e-4
    loud = np.nonzero(rms > ref * 10 ** (-18 / 20))[0]
    return int(loud[-1]) if len(loud) else len(mono)


def music_start(mono: np.ndarray) -> int:
    loud = np.nonzero(np.abs(mono) > 10 ** (-50 / 20))[0]
    return int(loud[0]) if len(loud) else 0


def find_loop(audio: np.ndarray, cue: Cue) -> tuple[int, int, float]:
    """(start, end, score). The jump end->start is smooth when the audio just before both points matches."""
    if cue.loop_start_s is not None and cue.loop_end_s is not None:
        return int(cue.loop_start_s * RATE), int(cue.loop_end_s * RATE), 1.0
    mono = audio.mean(axis=1)
    hop = 1024
    feats = frame_features(mono, hop=hop)
    first = music_start(mono)
    last = min(music_end(mono), len(mono)) - int(0.25 * RATE)
    ctx = max(4, int(2.0 * RATE / hop))  # compare 2 s of context before each point (so the intro is >= 2 s)
    min_len = max(int(20 * RATE), int(0.4 * (last - first)))

    if cue.bpm:
        bar = 60.0 / cue.bpm * cue.beats_per_bar * RATE
        grid = [int(first + k * bar) for k in range(int((last - first) / bar) + 1)]
        ends = [g for g in grid if g <= last][-6:]
        starts = grid
    else:
        step = int(0.25 * RATE)
        ends = list(range(last, max(first, last - int(8 * RATE)), -step))
        starts = list(range(first, last, step))

    best = (first, last, -1.0)
    for e in ends:
        fe = e // hop
        if fe - ctx < 0 or fe > len(feats):
            continue
        a = feats[fe - ctx:fe]
        for s in starts:
            fs = s // hop
            if e - s < min_len or fs - ctx < 0:
                continue
            score = float(np.mean(np.sum(a * feats[fs - ctx:fs], axis=1)))
            # near-ties go to the longer loop: less repetition, shorter intro
            if score > best[2] + 0.005 or (score > best[2] - 0.005 and e - s > best[1] - best[0]):
                best = (s, e, score)
    return best


def build_loop(audio: np.ndarray, start: int, end: int) -> tuple[np.ndarray, np.ndarray]:
    """Intro = audio[:start]; loop body = audio[start:end] whose last CROSSFADE_S fades into audio[start-x:start],
    so playing the body twice in a row continues exactly like the original did at `start`."""
    x = min(int(CROSSFADE_S * RATE), start, end - start)
    body = audio[start:end].copy()
    if x > 0:
        t = np.linspace(0.0, 1.0, x, dtype=np.float32)[:, None]
        fade_out, fade_in = np.cos(t * np.pi / 2), np.sin(t * np.pi / 2)  # equal power
        body[-x:] = body[-x:] * fade_out + audio[start - x:start] * fade_in
    return audio[:start].copy(), body


def trim(audio: np.ndarray) -> np.ndarray:
    mono = audio.mean(axis=1)
    s, e = music_start(mono), len(mono)
    tail = np.nonzero(np.abs(mono) > 10 ** (-60 / 20))[0]
    if len(tail):
        e = min(len(mono), int(tail[-1]) + int(0.05 * RATE))
    return audio[s:e]


def gain_for(measured: np.ndarray, peaks_of: list[np.ndarray]) -> tuple[float, float, float]:
    lufs, _ = measure(measured)
    gain_db = TARGET_LUFS - lufs
    peak_db = max(measure(p)[1] for p in peaks_of if len(p) > RATE // 2) if peaks_of else -70.0
    if peak_db + gain_db > TRUE_PEAK_DB:
        gain_db = TRUE_PEAK_DB - peak_db  # quieter rather than limited; the report says so
    return gain_db, lufs, peak_db


# ---------- pipeline ----------

def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def process(src: Path, cue: Cue, out: Path, review: Path, replace: bool) -> dict:
    dest = out / f"{cue.cue_id}.wav"
    intro_dest = out / f"{cue.cue_id}__intro.wav"
    if dest.exists() and not replace:
        return {"cue_id": cue.cue_id, "status": "skipped_exists", "file": str(dest)}

    audio = decode(src)
    report = {"cue_id": cue.cue_id, "source_file": src.name, "seconds_in": round(len(audio) / RATE, 2)}
    if cue.loop:
        start, end, score = find_loop(audio, cue)
        intro, body = build_loop(audio, start, end)
        if len(intro) < RATE // 2:  # loop from the very top: no separate intro
            intro = np.zeros((0, 2), np.float32)
        gain_db, lufs, peak = gain_for(body, [intro, body])
        g = np.float32(10 ** (gain_db / 20))
        intro, body = intro * g, body * g
        write_wav(dest, body)
        if len(intro):
            write_wav(intro_dest, intro, seed=1)
        elif intro_dest.exists():
            intro_dest.unlink()
        seam = np.concatenate([body[-5 * RATE:], body[:5 * RATE]])
        write_wav(review / f"{cue.cue_id}_seam.wav", seam, seed=2)
        report.update(loop_start_s=round(start / RATE, 3), loop_end_s=round(end / RATE, 3),
                      loop_seconds=round(len(body) / RATE, 2), intro_seconds=round(len(intro) / RATE, 2),
                      loop_score=round(score, 3), status="ok" if score >= LOOP_SCORE_OK else "needs_review")
    else:
        body = trim(audio)
        gain_db, lufs, peak = gain_for(body, [body])
        body = body * np.float32(10 ** (gain_db / 20))
        write_wav(dest, body)
        report.update(seconds=round(len(body) / RATE, 2), status="ok")
    report.update(measured_lufs=round(lufs, 1), measured_peak_dbtp=round(peak, 1), gain_db=round(gain_db, 2),
                  peak_limited=bool(abs((TARGET_LUFS - lufs) - gain_db) > 0.05))
    return report


def append_ledger(ledger: Path, src: Path, cue: Cue, report: dict) -> None:
    new = not ledger.exists()
    ledger.parent.mkdir(parents=True, exist_ok=True)
    with ledger.open("a", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=LEDGER_FIELDS)
        if new:
            w.writeheader()
        x = cue.extra
        w.writerow({
            "cue_id": cue.cue_id, "source_file": src.name, "sha256": sha256(src),
            "engine": cue.source, "account_tier": x.get("account_tier", ""), "generation_id": x.get("generation_id", ""),
            "download_channel": x.get("download_channel", "") or ("suno.com official download" if cue.source == "suno" else "official API"),
            "downloaded_at": dt.datetime.fromtimestamp(src.stat().st_mtime).isoformat(timespec="seconds"),
            "terms_snapshot": x.get("terms_snapshot", ""), "processed_at": dt.datetime.now().isoformat(timespec="seconds"),
            "loop_score": report.get("loop_score", ""), "status": report["status"],
        })


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--inbox", type=Path, required=True)
    p.add_argument("--cue-sheet", type=Path, required=True)
    p.add_argument("--out", type=Path, required=True, help="Unity folder, e.g. Assets/_Project/Resources/Audio/Music")
    p.add_argument("--work", type=Path, required=True, help="holds _archive, _review, _unmatched and reports")
    p.add_argument("--ledger", type=Path, required=True)
    p.add_argument("--replace", action="store_true", help="overwrite a cue that already exists")
    p.add_argument("--keep-source", action="store_true", help="copy into the archive instead of moving")
    a = p.parse_args(argv)

    cues = read_cue_sheet(a.cue_sheet)
    stamp = dt.datetime.now().strftime("%Y%m%dT%H%M%S")
    review = a.work / "_review" / stamp
    results = []
    for src in sorted(f for f in a.inbox.iterdir() if f.suffix.lower() in AUDIO_EXT):
        cue = match_cue(src.stem, probe_title(src), cues)
        if not cue:
            (a.work / "_unmatched").mkdir(parents=True, exist_ok=True)
            shutil.move(str(src), a.work / "_unmatched" / src.name)
            results.append({"source_file": src.name, "status": "unmatched"})
            continue
        try:
            report = process(src, cue, a.out, review, a.replace)
        except subprocess.CalledProcessError as e:
            report = {"cue_id": cue.cue_id, "source_file": src.name, "status": "failed",
                      "error": (e.stderr or b"").decode("utf-8", "replace")[-400:]}
        results.append(report)
        if report["status"] in ("ok", "needs_review"):
            append_ledger(a.ledger, src, cue, report)
            archive = a.work / "_archive" / cue.cue_id
            archive.mkdir(parents=True, exist_ok=True)
            (shutil.copy2 if a.keep_source else shutil.move)(str(src), archive / f"{stamp}_{src.name}")

    review.mkdir(parents=True, exist_ok=True)
    (review / "report.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(results, ensure_ascii=False, indent=2))
    return 1 if any(r["status"] in ("failed", "unmatched") for r in results) else 0


if __name__ == "__main__":
    sys.exit(main())
