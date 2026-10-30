"""Thickens a region's ground clutter (user 2026-10-30: the wild still reads bare between Codex's pieces). Every detail
placement gets two neighbours a few metres away, kept only where the ground is the same layer as under the original,
so grass stays on grass, reeds on the bank, and nothing spills onto roads, water, towns or camps.
Usage: python densify_detail.py <detail placements json> <concept greybox dir>  -> <greybox>/detail_dense.json
"""
import json, math, os, random, sys
import numpy as np
from PIL import Image

src, grey = sys.argv[1], sys.argv[2]
P = json.load(open(src, encoding='utf-8'))
idx = np.array(Image.open(os.path.join(grey, 'ground_index.png')).convert('RGB'))
layers = json.load(open(os.path.join(grey, 'ground_layers.json'), encoding='utf-8'))
banned = {i for i, k in enumerate(layers) if any(w in k for w in ('road', 'path', 'water', 'town', 'camp', 'village', 'pond', 'paddy'))}
rows = idx.shape[0]
# Only plants are thickened; extra stones, branches and litter read as spots (first try, 2026-10-30).
PLANTS = ('grass', 'flower', 'sedge', 'reed', 'wormwood', 'artemisia', 'stipa', 'tuft', 'clump', 'stubble', 'seedling', 'sapling', 'jujube_low', 'jujube_sucker', 'salt_grass', 'crop', 'shepherd')


def layer(x, z):
    r, c = int(rows - 1 - z * 4), int(x * 4)
    if r < 0 or c < 0 or r >= rows or c >= idx.shape[1]:
        return -1
    return int(idx[r, c, 0])


rng = random.Random(30)
out = []
for p in P['placements']:
    out.append(p)
    pos = p.get('position_m') or p.get('position')
    if not pos or p.get('placement_role') in ('world_geometry', 'bridge_surface') or not any(t in p['asset'] for t in PLANTS):
        continue
    home = layer(pos[0], pos[1])
    if home < 0 or home in banned:
        continue
    for _ in range(2):
        a, d = rng.uniform(0, math.tau), rng.uniform(1.5, 5.0)
        x, z = pos[0] + math.cos(a) * d, pos[1] + math.sin(a) * d
        if layer(x, z) != home:
            continue
        q = dict(p)
        q['position_m'] = [round(x, 2), round(z, 2)]
        q.pop('position', None)
        q['rotation_z_deg'] = round(rng.uniform(0, 360), 1)
        q.pop('rotation_z', None)
        q['scale'] = round((p.get('scale') or 1.0) * rng.uniform(0.75, 1.15), 3)
        out.append(q)
json.dump({'placements': out}, open(os.path.join(grey, 'detail_dense.json'), 'w', encoding='utf-8'))
print(os.path.basename(src), len(P['placements']), '->', len(out))
