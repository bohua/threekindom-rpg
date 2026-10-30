"""Pulls every keyed line out of the chapter-1 NPC and side-quest scripts (design/chapter1-rise/npc-sidequests/R*.md)
into one table the game reads (Resources/Text/npc_lines_ch1.json), so the writers' text is the game's text, word
for word. The shapes read:
  table rows   | N4·入营 | “……” | dlg.lb1.yuan_shao.N4in.1 |
  inline       “……”（dlg.lb1.gu_sou.L1.3.1）   and the short form “……”（.2）, which reuses the last full key's stem.
Speaker ids come from the key (dlg.lb1.<id>.… , dlg.lb1.npc.<区>.<场景>.<职能>.…).
Usage: python extract_lines.py <design dir> <out json>
"""
import glob, json, os, re, sys

src, out = sys.argv[1], sys.argv[2]
KEY = r'dlg\.lb1\.[A-Za-z0-9_.\-]+'
lines, dup = {}, []
QUOTE = r'[“"]([^”"]{1,400})[”"]'
for f in sorted(glob.glob(os.path.join(src, 'R*.md'))):
    stem = None
    for raw in open(f, encoding='utf-8'):
        # Table row: the text cell holds a quote, a later cell the key.
        cells = [c.strip() for c in raw.split('|')]
        if len(cells) > 3:
            keys = [c.strip('`* ') for c in cells if re.fullmatch(r'`?' + KEY + r'`?', c.strip('* '))]
            texts = [c for c in cells if re.search(QUOTE, c)]
            if keys and texts:
                t = re.search(QUOTE, texts[-1]).group(1)
                k = keys[0].rstrip('.')
                if k in lines and lines[k] != t: dup.append(k)
                lines.setdefault(k, t)
                stem = k.rsplit('.', 1)[0]
                continue
        # Key first: （dlg.key）：“text”
        for m in re.finditer(r'[（(]`?(' + KEY + r'|\.[A-Za-z0-9_]+)`?[）)][：:]\s*' + QUOTE, raw):
            k, t = m.group(1), m.group(2)
            if k.startswith('.'):
                if not stem: continue
                k = stem + k
            else:
                stem = k.rsplit('.', 1)[0]
            lines.setdefault(k.rstrip('.'), t)
        # Key and text inside one bracket: （dlg.key：“text”）
        for m in re.finditer(r'[（(]`?(' + KEY + r')`?[：:]\s*' + QUOTE, raw):
            lines.setdefault(m.group(1).rstrip('.'), m.group(2))
        # Pipe form: “text”｜`dlg.key`  (and “a”……“b”｜`k1`／`k2`, pairing the last quotes with the keys in order)
        for m in re.finditer(r'((?:' + QUOTE + r'[^“”"｜]{0,40}?)+)｜\s*((?:`?' + KEY + r'`?\s*[／/]?\s*)+)', raw):
            qs = re.findall(QUOTE, m.group(1))
            ks = [k.rstrip('.') for k in re.findall(KEY, m.group(m.lastindex))]
            for t, k in zip(qs[-len(ks):], ks):
                lines.setdefault(k, t)
        # Inline: “text”（dlg.key） or “text”（.n）
        for m in re.finditer(QUOTE + r'[^“”"]{0,6}?[（(]`?(' + KEY + r'|\.[A-Za-z0-9_]+)`?[）)]', raw):
            t, k = m.group(1), m.group(2)
            if k.startswith('.'):
                if not stem: continue
                k = stem + k
            else:
                stem = k.rsplit('.', 1)[0]
            k = k.rstrip('.')
            if k in lines and lines[k] != t: dup.append(k)
            lines.setdefault(k, t)
os.makedirs(os.path.dirname(out), exist_ok=True)
ks = sorted(lines)
# Two parallel arrays: Unity's JsonUtility reads no dictionaries.
json.dump({'keys': ks, 'texts': [lines[k] for k in ks]}, open(out, 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
print(len(lines), 'lines;', len(dup), 'keys with differing text (first kept):', dup[:8])
