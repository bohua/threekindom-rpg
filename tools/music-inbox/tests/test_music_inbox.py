"""Synthetic-audio checks for music_inbox.py: python -m pytest tools/music-inbox/tests"""
import csv
import sys
import wave
from pathlib import Path

import numpy as np
import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import music_inbox as mi  # noqa: E402

R = mi.RATE


def tone(freqs, seconds, amp=0.2):
    t = np.arange(int(seconds * R)) / R
    env = np.minimum(1.0, np.minimum(t / 0.01, (seconds - t) / 0.05))
    x = sum(np.sin(2 * np.pi * f * t) for f in freqs) * amp * env / len(freqs)
    return np.stack([x, x], axis=1).astype(np.float32)


def phrase():
    # 8 bars at 120 BPM (2 s per bar): a different chord per bar
    roots = [220, 247, 262, 294, 330, 294, 262, 247]
    return np.concatenate([tone([f, f * 1.25, f * 1.5], 2.0) for f in roots])


def song():
    intro = np.concatenate([tone([110, 165], 2.0, 0.1), tone([123, 185], 2.0, 0.1)])
    body = np.concatenate([phrase()] * 3)
    fade = phrase()[: 6 * R] * np.linspace(1, 0, 6 * R, dtype=np.float32)[:, None]
    return np.concatenate([intro, body, fade])


def save(path, audio):
    mi.write_wav(path, audio)
    return path


def read_wav(path):
    with wave.open(str(path)) as w:
        return np.frombuffer(w.readframes(w.getnframes()), "<i2").reshape(-1, 2) / 32768.0


@pytest.fixture
def setup(tmp_path):
    inbox, out, work = tmp_path / "inbox", tmp_path / "out", tmp_path / "work"
    inbox.mkdir()
    sheet = tmp_path / "cues.csv"
    with sheet.open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["cue_id", "source", "loop", "bpm", "beats_per_bar", "account_tier"])
        w.writerow(["mus_battle_boss", "suno", "yes", "120", "4", "pro"])
        w.writerow(["mus_battle", "api", "yes", "", "", ""])
        w.writerow(["jng_victory", "api", "no", "", "", ""])
    args = ["--inbox", str(inbox), "--cue-sheet", str(sheet), "--out", str(out), "--work", str(work),
            "--ledger", str(tmp_path / "ledger.csv")]
    return inbox, out, work, tmp_path / "ledger.csv", args


def test_match_prefers_longest_whole_token():
    cues = {c: mi.Cue(c) for c in ["mus_battle", "mus_battle_boss"]}
    assert mi.match_cue("mus_battle_boss (1)", None, cues).cue_id == "mus_battle_boss"
    assert mi.match_cue("Mus_Battle v2", None, cues).cue_id == "mus_battle"
    assert mi.match_cue("song 123", "mus_battle_boss", cues).cue_id == "mus_battle_boss"
    assert mi.match_cue("mus_battlefield", None, cues) is None


def test_loop_is_bar_snapped_seamless_and_loud_enough(setup):
    inbox, out, work, ledger, args = setup
    save(inbox / "mus_battle_boss.wav", song())
    assert mi.main(args) == 0

    body, intro = read_wav(out / "mus_battle_boss.wav"), read_wav(out / "mus_battle_boss__intro.wav")
    bars = len(body) / R / 2.0
    assert abs(bars - round(bars)) < 0.01 and round(bars) % 8 == 0, bars  # whole phrases
    intro_s = len(intro) / R  # starts after >= 2 s of phrase context, on a bar line
    assert 4.0 < intro_s <= 8.05 and abs(intro_s / 2.0 - round(intro_s / 2.0)) < 0.01, intro_s
    assert len(body) / R >= 32.0
    # seam: wrapping end -> start is no bigger a step than a normal sample-to-sample step
    step = np.abs(np.diff(body[:, 0])).max()
    assert abs(body[-1, 0] - body[0, 0]) <= step * 1.5
    lufs, peak = mi.measure(body.astype(np.float32))
    assert abs(lufs - mi.TARGET_LUFS) < 0.5 and peak <= mi.TRUE_PEAK_DB + 0.3
    rows = list(csv.DictReader(ledger.open(encoding="utf-8")))
    assert rows[0]["cue_id"] == "mus_battle_boss" and rows[0]["account_tier"] == "pro" and rows[0]["status"] == "ok"
    assert not any(inbox.iterdir()) and any((work / "_archive" / "mus_battle_boss").iterdir())


def test_loop_without_bpm_still_finds_a_phrase(setup):
    inbox, out, work, ledger, args = setup
    save(inbox / "mus_battle.wav", song())
    mi.main(args)
    report = (next((work / "_review").iterdir()) / "report.json").read_text(encoding="utf-8")
    import json
    r = json.loads(report)[0]
    assert r["status"] == "ok", r
    assert abs(r["loop_seconds"] % 16.0) < 0.3 or abs(r["loop_seconds"] % 16.0 - 16.0) < 0.3, r


def test_jingle_is_trimmed_not_looped(setup):
    inbox, out, work, ledger, args = setup
    jingle = np.concatenate([np.zeros((R, 2), np.float32), tone([523, 659, 784], 3.0), np.zeros((R, 2), np.float32)])
    save(inbox / "jng_victory.wav", jingle)
    mi.main(args)
    assert not (out / "jng_victory__intro.wav").exists()
    assert abs(len(read_wav(out / "jng_victory.wav")) / R - 3.0) < 0.15


def test_existing_cue_is_not_overwritten_and_unmatched_is_parked(setup):
    inbox, out, work, ledger, args = setup
    out.mkdir()
    (out / "mus_battle.wav").write_bytes(b"keep")
    save(inbox / "mus_battle.wav", song())
    save(inbox / "random title.wav", tone([440], 1.0))
    assert mi.main(args) == 1
    assert (out / "mus_battle.wav").read_bytes() == b"keep"
    assert (work / "_unmatched" / "random title.wav").exists()
    assert (inbox / "mus_battle.wav").exists()  # skipped files stay in the inbox


def test_watcher_moves_finished_cue_downloads_only(setup, tmp_path):
    import watch_downloads as wd
    inbox, out, work, ledger, args = setup
    downloads = tmp_path / "Downloads"
    downloads.mkdir()
    save(downloads / "mus_battle_boss (1).wav", song())
    (downloads / "mus_battle.mp3.crdownload").write_bytes(b"partial")
    (downloads / "invoice.wav").write_bytes(b"not music")
    rest = args[args.index("--cue-sheet"):]
    assert wd.main(["--downloads", str(downloads), "--inbox", str(inbox), "--interval", "0", "--once", *rest]) == 0
    assert (out / "mus_battle_boss.wav").exists()
    assert sorted(f.name for f in downloads.iterdir()) == ["invoice.wav", "mus_battle.mp3.crdownload"]


def test_watcher_in_place_processes_inbox_once(setup):
    import watch_downloads as wd
    inbox, out, work, ledger, args = setup
    out.mkdir()
    (out / "mus_battle.wav").write_bytes(b"keep")
    save(inbox / "mus_battle.wav", song())
    rest = args[args.index("--cue-sheet"):]
    wd.main(["--downloads", str(inbox), "--inbox", str(inbox), "--interval", "0", "--once", *rest])
    assert (inbox / "mus_battle.wav").exists() and (out / "mus_battle.wav").read_bytes() == b"keep"
    assert len(list((work / "_review").iterdir())) == 1  # skipped file reported once, not every pass
