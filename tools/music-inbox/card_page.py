"""Builds a local page of the Suno prompt cards with copy buttons (ADR-0011, semi-automatic Suno).

Reads design/audio/suno-cards/<cue>.md and marks a card done once its cue is in the licence ledger. Open the page in
the same browser as suno.com: copy Title, Style, Lyrics and Exclude into Custom mode, create, pick, download.

  python card_page.py --cards design/audio/suno-cards --ledger design/audio/LICENSE_LEDGER.csv --out D:/.../suno-cards.html
"""
from __future__ import annotations

import argparse
import csv
import html
import re
import sys
from pathlib import Path


def parse_card(path: Path) -> dict:
    text = path.read_text(encoding="utf-8")
    head = text.splitlines()[0].lstrip("# ").strip()
    blocks = re.findall(r"```[^\n]*\n(.*?)```", text, re.S)
    exclude = re.search(r"Exclude styles?：`([^`]*)`", text)
    tips = re.search(r"挑选要点：(.*)", text)
    return {
        "cue": path.stem,
        "head": head,
        "style": blocks[0].strip() if blocks else "",
        "lyrics": blocks[1].strip() if len(blocks) > 1 else "[Instrumental]",
        "exclude": exclude.group(1) if exclude else "",
        "tips": tips.group(1).strip() if tips else "",
    }


def done_cues(ledger: Path) -> set[str]:
    if not ledger.exists():
        return set()
    with ledger.open(encoding="utf-8-sig", newline="") as f:
        return {r["cue_id"] for r in csv.DictReader(f) if r.get("status") in ("ok", "needs_review", "in_game")}


PAGE = """<!doctype html><html lang="zh-CN"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1"><title>Suno 提示词卡片</title>
<style>
:root{{--bg:#f6f2ea;--card:#fffdf8;--ink:#2b2620;--muted:#7a7064;--line:#e3dbcd;--accent:#9b2c1f;--ok:#2f6b3a}}
@media (prefers-color-scheme:dark){{:root{{--bg:#1c1a17;--card:#26231f;--ink:#eee6d8;--muted:#a59a8a;--line:#3a352e;--accent:#e0705f;--ok:#7cc48a}}}}
body{{margin:0;background:var(--bg);color:var(--ink);font:15px/1.6 system-ui,"Microsoft YaHei",sans-serif}}
main{{max-width:880px;margin:0 auto;padding:24px 16px 64px}}
h1{{font-size:22px;margin:0 0 4px}} .lead{{color:var(--muted);margin:0 0 20px}}
section{{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:16px;margin:0 0 16px}}
section.done{{opacity:.55}} h2{{font-size:17px;margin:0 0 10px;display:flex;gap:8px;align-items:center}}
.tag{{font-size:12px;padding:1px 8px;border-radius:99px;border:1px solid currentColor;color:var(--ok)}}
.row{{display:grid;grid-template-columns:80px 1fr auto;gap:8px;align-items:start;margin:6px 0}}
.k{{color:var(--muted);padding-top:6px}} pre{{margin:0;white-space:pre-wrap;word-break:break-word;background:var(--bg);
border:1px solid var(--line);border-radius:6px;padding:6px 8px;font:13px/1.5 ui-monospace,Consolas,monospace;max-height:9em;overflow:auto}}
button{{font:inherit;font-size:13px;padding:5px 12px;border-radius:6px;border:1px solid var(--accent);background:none;color:var(--accent);cursor:pointer}}
button.copied{{background:var(--accent);color:var(--card)}} .tips{{color:var(--muted);font-size:13px;margin-top:8px}}
</style></head><body><main>
<h1>Suno 提示词卡片</h1>
<p class="lead">在 Suno 选 Custom，逐栏点“复制”后粘贴 → Create → 听两个候选，挑一个用官方下载按钮下载。监视窗口会自动把文件送进收件箱处理。灰色卡片表示已入库。</p>
{cards}
</main><script>
document.querySelectorAll('button[data-t]').forEach(b=>b.onclick=async()=>{{
 const t=document.getElementById(b.dataset.t).innerText;
 try{{await navigator.clipboard.writeText(t)}}catch(e){{const r=document.createRange();r.selectNode(document.getElementById(b.dataset.t));
 getSelection().removeAllRanges();getSelection().addRange(r);document.execCommand('copy');getSelection().removeAllRanges()}}
 document.querySelectorAll('button.copied').forEach(x=>{{x.classList.remove('copied');x.textContent='复制'}});
 b.classList.add('copied');b.textContent='已复制'}});
</script></body></html>"""


def render(cards: list[dict], done: set[str]) -> str:
    out = []
    for c in cards:
        rows = []
        for key, label in (("cue", "Title"), ("style", "Style"), ("lyrics", "Lyrics"), ("exclude", "Exclude")):
            tid = f"{c['cue']}-{key}"
            rows.append(f'<div class="row"><div class="k">{label}</div><pre id="{tid}">{html.escape(c[key])}</pre>'
                        f'<button data-t="{tid}">复制</button></div>')
        tag = '<span class="tag">已入库</span>' if c["cue"] in done else ""
        tips = f'<div class="tips">挑选要点：{html.escape(c["tips"])}</div>' if c["tips"] else ""
        cls = ' class="done"' if c["cue"] in done else ""
        out.append(f'<section{cls}><h2>{html.escape(c["head"])} {tag}</h2>{"".join(rows)}{tips}</section>')
    return PAGE.format(cards="\n".join(out))


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--cards", type=Path, required=True)
    p.add_argument("--ledger", type=Path, required=True)
    p.add_argument("--out", type=Path, required=True)
    a = p.parse_args(argv)
    done = done_cues(a.ledger)
    cards = sorted((parse_card(f) for f in a.cards.glob("*.md")), key=lambda c: (c["cue"] in done, c["cue"]))
    a.out.parent.mkdir(parents=True, exist_ok=True)
    a.out.write_text(render(cards, done), encoding="utf-8")
    print(a.out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
