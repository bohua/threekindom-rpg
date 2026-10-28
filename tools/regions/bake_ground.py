"""Bakes a region's painted ground for RegionGreyboxBuilder (chapter 1 refinement, user 2026-10-28 "先精修").

From the Codex class mask (1 px per metre, one flat colour per ground) and its class table, writes into
art/<concept>/greybox:
  ground_index.png  4 px per metre, row 0 = north (texture top): R, G = the two strongest grounds (layer index), B = share
                    of the second. Weights are each class blurred over a few metres, broken up by two octaves of noise,
                    so grounds meet in wandering transitions, never square patches (user 2026-10-25).
  ground_layers.png  every ground texture at full detail (1024 px per 8 m tile, ~128 px/m) in a 5-column grid, in layer
                    order; Unity imports it as one compressed texture array.
Usage: python bake_ground.py <ground set dir> <mask png> <classes json> <concept dir>
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageFilter

ground_dir, mask_path, classes_path, concept = sys.argv[1:5]
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
weights = []
for li, c in enumerate(classes):
    m = np.all(mask == np.array(c['rgb']), axis=2).astype(np.float32)
    im = Image.fromarray((m * 255).astype(np.uint8)).resize((W * S, H * S), Image.NEAREST)
    # Water, roads and cliffs keep tighter edges than fields and grass.
    radius = 2 if c['key'] in ('water_river', 'water_marsh', 'road_loess', 'path_rural', 'cliff_taihang') else 5
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
