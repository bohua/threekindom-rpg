"""Where steep ground shows rock (user 2026-10-30: the big mountains were one sheet of grass). The slope comes from the
heightmap smoothed over a few metres, so the rock fades in across a hillside instead of following each terrace step;
a two-scale noise breaks the line, and roads, water, towns, camps and villages never turn to rock.
Usage: python bake_rock.py <concept greybox dir>  -> ground_rock.png (1 px per metre, row 0 = north)
"""
import json, os, sys
import numpy as np
from PIL import Image

grey = sys.argv[1]
g = json.load(open(os.path.join(grey, 'region.json'), encoding='utf-8'))
W, D = g['width'], g['height']
H = np.fromfile(os.path.join(grey, 'heights.bin'), '<f4').reshape(D + 1, W + 1)   # row 0 = south


def blur(a, sigma):
    r = int(sigma * 3)
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2); k /= k.sum()
    p = np.pad(a, r, mode='edge')
    p = np.apply_along_axis(lambda v: np.convolve(v, k, 'valid'), 1, p)
    return np.apply_along_axis(lambda v: np.convolve(v, k, 'valid'), 0, p)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


gz, gx = np.gradient(blur(H, 3.0))
ny = 1 / np.sqrt(1 + gx ** 2 + gz ** 2)
rng = np.random.default_rng(30)
noise = blur(rng.random((D + 1, W + 1)), 6) * 0.6 + blur(rng.random((D + 1, W + 1)), 2) * 0.4
noise = (noise - noise.mean()) / (noise.std() + 1e-6)
rock = smoothstep(0.94, 0.76, ny + noise * 0.025)
# Grounds that must stay what they are.
idx = np.array(Image.open(os.path.join(grey, 'ground_index.png')).convert('RGB'))[::4, ::4, 0][::-1]
layers = json.load(open(os.path.join(grey, 'ground_layers.json'), encoding='utf-8'))
keep = np.isin(idx, [i for i, k in enumerate(layers) if any(w in k for w in ('road', 'path', 'water', 'town', 'camp', 'village', 'pond', 'paddy', 'river', 'canal'))])
mask = np.zeros_like(rock); mask[:keep.shape[0], :keep.shape[1]] = keep[:D + 1, :W + 1]
rock *= 1 - blur(mask.astype(float), 2.5).clip(0, 1)
rock = blur(rock, 1.5).clip(0, 0.92)
Image.fromarray((rock[::-1] * 255).astype(np.uint8), 'L').convert('RGB').save(os.path.join(grey, 'ground_rock.png'))
print(os.path.basename(os.path.dirname(grey)), 'rock share', round(float(rock.mean()), 3), 'cells >0.5', round(float((rock > 0.5).mean()) * 100, 1), '%')
