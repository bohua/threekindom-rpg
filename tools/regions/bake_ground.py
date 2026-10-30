"""Bakes a region's painted ground for RegionGreyboxBuilder (chapter 1 refinement, user 2026-10-28 "先精修").

From the Codex class mask (1 px per metre, one flat colour per ground) and its class table, writes into
art/<concept>/greybox:
  ground_index.png  4 px per metre, row 0 = north (texture top): R, G = the two strongest grounds (layer index), B = share
                    of the second. Weights are each class blurred over a few metres, broken up by two octaves of noise,
                    so grounds meet in wandering transitions, never square patches (user 2026-10-25).
  ground_layers.png  every ground texture at full detail (1024 px per 8 m tile, ~128 px/m) in a 5-column grid, in layer
                    order; Unity imports it as one compressed texture array.
Earth under places (user 2026-10-28: "军营帐篷到旁边的土地……颜色明显就是一个断层"): camps, towns, villages, post
stations and every house in the wild set get their own bare-earth ground baked in, a ragged patch a few metres wider
than the footprint fading into the grass over 6-10 m, in place of the models' flat apron decals (dropped in Unity).
Usage: python bake_ground.py <ground set dir> <mask png> <classes json> <concept dir> [wild placements json]
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageFilter

ground_dir, mask_path, classes_path, concept = sys.argv[1:5]
wild_path = sys.argv[5] if len(sys.argv) > 5 else None
classes = json.load(open(classes_path, encoding='utf-8'))['classes']
mask = np.array(Image.open(mask_path).convert('RGB'), dtype=np.int32)
H, W = mask.shape[:2]
S = 4                                    # index texels per metre
out = os.path.join(concept, 'greybox')
os.makedirs(out, exist_ok=True)

rng = np.random.default_rng(7)
tiles = []
def noise(scale):
    small = rng.random((H * S // scale + 2, W * S // scale + 2)).astype(np.float32)
    return np.array(Image.fromarray(small).resize((W * S, H * S), Image.BICUBIC))

n = noise(6) * 0.65 + noise(2) * 0.35
# Every border wanders: the class map is read through a smooth random displacement of up to ~3 m, so straight mask edges
# (the rectangle of a camp, a town's square) come out ragged before they are blurred.
ys, xs = np.mgrid[0:H * S, 0:W * S].astype(np.float32)
dx = (noise(40) - 0.5) * 2 * 3.0 * S + (noise(10) - 0.5) * 2 * 1.0 * S
dy = (noise(40) - 0.5) * 2 * 3.0 * S + (noise(10) - 0.5) * 2 * 1.0 * S
sx = np.clip((xs + dx) / S, 0, W - 1).astype(np.int32)
sy = np.clip((ys + dy) / S, 0, H - 1).astype(np.int32)
warped = mask[sy, sx]
# Hard flat earth (camps, towns, villages) with the places' own patches added: footprint + 4 m, edge noise.
earth = {'camp_earth': np.zeros((H * S, W * S), np.float32), 'town_ground': np.zeros((H * S, W * S), np.float32), 'village_earth': np.zeros((H * S, W * S), np.float32)}
def spot(key, x, z, rx, rz):
    if key not in earth: return
    cx, cy = x * S, (H - 1 - z) * S
    r = int(max(rx, rz) * S + 12 * S)
    y0, y1, x0, x1 = max(0, int(cy - r)), min(H * S, int(cy + r)), max(0, int(cx - r)), min(W * S, int(cx + r))
    yy, xx = np.mgrid[y0:y1, x0:x1]
    d = np.sqrt(((xx - cx) / (rx * S)) ** 2 + ((yy - cy) / (rz * S)) ** 2)
    edge = d - 0.35 * (n[y0:y1, x0:x1] - 0.5)
    earth[key][y0:y1, x0:x1] = np.maximum(earth[key][y0:y1, x0:x1], np.clip(1.6 - edge, 0, 1))
region = json.load(open(os.path.join(out, 'region.json'), encoding='utf-8'))
for p in region['places']:
    k = {'camp': 'camp_earth', 'town': 'town_ground', 'gate_town': 'town_ground', 'village': 'village_earth', 'post_station': 'village_earth'}.get(p['kind'])
    if k: spot(k, p['x'], p['z'], p['w'] / 2 + 5, p['d'] / 2 + 5)
if wild_path and os.path.exists(wild_path):
    for q in json.load(open(wild_path, encoding='utf-8'))['placements']:
        role, a = q.get('placement_role', ''), q['asset']
        if role in ('village_house', 'poi') or a.startswith(('house_', 'post_station', 'yellow_fort', 'fisher_hut', 'clay_kiln')):
            spot('village_earth', q['position_m'][0], q['position_m'][1], 7, 6)
weights = []
for li, c in enumerate(classes):
    m = np.all(warped == np.array(c['rgb']), axis=2).astype(np.float32)
    if c['key'] in earth: m = np.maximum(m, earth[c['key']])
    im = Image.fromarray((m * 255).astype(np.uint8))
    # Water, roads and cliffs keep tighter edges; flat earth fades widest into the grass around it.
    radius = 2 if c['key'] in ('water_river', 'water_marsh', 'road_loess', 'path_rural', 'cliff_taihang') else 7 if c['key'] in earth else 5
    w = np.array(im.filter(ImageFilter.GaussianBlur(radius * S)), dtype=np.float32) / 255.0
    weights.append(w * (0.75 + 0.5 * np.roll(n, li * 7, axis=1)))
    tiles.append(Image.open(os.path.join(ground_dir, c['texture'])).convert('RGB').resize((1024, 1024), Image.LANCZOS))

stack = np.stack(weights)                # [layers, h, w]
order = np.argsort(-stack, axis=0)
a, b = order[0], order[1]
wa = np.take_along_axis(stack, a[None], 0)[0]
wb = np.take_along_axis(stack, b[None], 0)[0]
share = np.clip(wb / np.maximum(wa + wb, 1e-4), 0, 0.5) * 2 * 0.5   # 0 .. 0.5: the strongest ground always leads
COLS = 5
rows = (len(tiles) + COLS - 1) // COLS
atlas = Image.new('RGB', (COLS * 1024, rows * 1024))
for i, t in enumerate(tiles):
    atlas.paste(t, ((i % COLS) * 1024, (i // COLS) * 1024))
atlas.save(os.path.join(out, 'ground_layers.png'))
json.dump({'columns': COLS, 'rows': rows, 'count': len(tiles)}, open(os.path.join(out, 'ground_layers_grid.json'), 'w'))
img = np.stack([a.astype(np.uint8), b.astype(np.uint8), (share * 255).astype(np.uint8)], axis=2)
Image.fromarray(img, 'RGB').save(os.path.join(out, 'ground_index.png'))
json.dump([c['key'] for c in classes], open(os.path.join(out, 'ground_layers.json'), 'w', encoding='utf-8'))
print('index', img.shape, 'layers', len(classes))
