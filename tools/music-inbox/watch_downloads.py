"""Semi-automatic Suno loop (ADR-0011): watch the browser's download folder and hand finished cue downloads to
music_inbox.

The user generates on suno.com and presses Suno's own download button; nothing here touches the website. Every few
seconds this script looks at the download folder (or any folder given with --downloads), and each audio file whose
name or title tag carries a cue id from the cue sheet is moved into the inbox once its size has stopped changing.
Then music_inbox runs on the inbox. Files that match no cue are left where they are, so other downloads are safe.

  python watch_downloads.py --downloads %USERPROFILE%\\Downloads --inbox D:/.../audio/_inbox --cue-sheet ... \
      --out ... --work ... --ledger ...          (Ctrl+C to stop; --once for a single pass)
"""
from __future__ import annotations

import argparse
import shutil
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import music_inbox as mi  # noqa: E402

PARTIAL = {".crdownload", ".part", ".tmp", ".download"}


def ready_files(folder: Path, cues: dict, sizes: dict) -> list[tuple[Path, "mi.Cue"]]:
    """Audio files that match a cue and whose size equals the size seen on the previous pass."""
    found = []
    seen = {}
    for f in folder.iterdir():
        if not f.is_file() or f.suffix.lower() not in mi.AUDIO_EXT or f.suffix.lower() in PARTIAL:
            continue
        cue = mi.match_cue(f.stem, None, cues) or mi.match_cue(f.stem, mi.probe_title(f), cues)
        if not cue:
            continue
        size = f.stat().st_size
        seen[f] = size
        if size > 0 and sizes.get(f) == size:
            found.append((f, cue))
    sizes.clear()
    sizes.update(seen)
    return found


def move_to_inbox(src: Path, inbox: Path) -> Path:
    inbox.mkdir(parents=True, exist_ok=True)
    dest = inbox / src.name
    n = 1
    while dest.exists():
        dest = inbox / f"{src.stem}_{n}{src.suffix}"
        n += 1
    shutil.move(str(src), dest)
    return dest


def say(results: list[dict]) -> None:
    words = {"ok": "完成", "needs_review": "完成，循环点需人工复核", "skipped_exists": "跳过：游戏里已有这首（要替换请加 --replace）",
             "unmatched": "没对上曲目编号", "failed": "处理失败"}
    for r in results:
        line = f"  {r.get('cue_id') or r.get('source_file')}: {words.get(r['status'], r['status'])}"
        if "loop_score" in r:
            line += f"，循环评分 {r['loop_score']}，循环 {r['loop_seconds']} 秒，前奏 {r['intro_seconds']} 秒"
        if "measured_lufs" in r:
            line += f"，原响度 {r['measured_lufs']} LUFS，增益 {r['gain_db']} dB"
        print(line, flush=True)


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--downloads", type=Path, default=Path.home() / "Downloads")
    p.add_argument("--inbox", type=Path, required=True)
    p.add_argument("--cue-sheet", type=Path, required=True)
    p.add_argument("--out", type=Path, required=True)
    p.add_argument("--work", type=Path, required=True)
    p.add_argument("--ledger", type=Path, required=True)
    p.add_argument("--interval", type=float, default=3.0, help="seconds between passes")
    p.add_argument("--once", action="store_true", help="one pass (two size checks) and exit")
    a = p.parse_args(argv)
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    inbox_args = ["--inbox", str(a.inbox), "--cue-sheet", str(a.cue_sheet), "--out", str(a.out),
                  "--work", str(a.work), "--ledger", str(a.ledger)]
    print(f"正在监视 {a.downloads}，Suno 下载的曲子会自动送进 {a.inbox} 处理。按 Ctrl+C 停止。", flush=True)
    # Chrome may also be set to save straight into the inbox: then files are processed where they are.
    in_place = a.downloads.resolve() == a.inbox.resolve()
    sizes: dict = {}
    handled: set = set()  # (name, size) already reported, so a skipped file is not re-run every pass
    passes = 0
    try:
        while True:
            cues = mi.read_cue_sheet(a.cue_sheet)  # re-read: new cards can be added while watching
            ready = [(f, c) for f, c in ready_files(a.downloads, cues, sizes) if (f.name, f.stat().st_size) not in handled]
            handled.update((f.name, f.stat().st_size) for f, _ in ready)
            moved = [f if in_place else move_to_inbox(f, a.inbox) for f, _ in ready]
            if not in_place:
                handled.update((m.name, m.stat().st_size) for m in moved)
            if moved:
                print(f"收到 {len(moved)} 个文件：{', '.join(m.name for m in moved)}，开始处理…", flush=True)
                results = run_inbox(inbox_args)
                say(results)
            passes += 1
            if a.once and passes >= 2:
                return 0
            time.sleep(a.interval)
    except KeyboardInterrupt:
        print("已停止监视。", flush=True)
        return 0


def run_inbox(args: list[str]) -> list[dict]:
    import contextlib
    import io
    import json
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        mi.main(args)
    try:
        return json.loads(buf.getvalue())
    except json.JSONDecodeError:
        print(buf.getvalue())
        return []


if __name__ == "__main__":
    sys.exit(main())
