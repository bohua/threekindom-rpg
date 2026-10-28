"""Turns a Codex region concept (art/<concept>: terrain_layout.json, <P>_HEIGHTMAP.png, <P>_TERRAIN_MAP*.png) into
greybox/region.json, heights.bin and ground_tint.png for RegionGreyboxBuilder.

Codex writes each region's layout in its own words (center vs center_m, points vs points_xz vs centerline ...), so
every reader here is tolerant. Usage: python prepare_region.py <concept dir> <region id> <display name> [hubs.json]
"""
import json, math, os, sys
import numpy as np
from PIL import Image, ImageFilter

src, rid, name = sys.argv[1], sys.argv[2], sys.argv[3]
hubs = json.load(open(sys.argv[4], encoding='utf-8')) if len(sys.argv) > 4 else {}
L = json.load(open(os.path.join(src, 'terrain_layout.json'), encoding='utf-8'))
P = rid.upper()
out = os.path.join(src, 'greybox')
os.makedirs(out, exist_ok=True)

hm = Image.open(os.path.join(src, f'{P}_HEIGHTMAP.png'))
a = np.array(hm, dtype=np.float64)
if a.ndim == 3:
    a = a[..., 0]
if a.max() <= 255:
    a = a * 257.0
H = a / 65535.0 * 65.0 - 5.0      # row 0 = north
H = H[::-1].copy()                  # row 0 = south
# Codex heightmaps step at river banks and cliff feet; a light blur turns the stairs into slopes (plots are
# flattened again below, so platforms keep their stated heights).
def _blur(a, sigma):
    r = int(sigma * 3)
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2); k /= k.sum()
    p = np.pad(a, r, mode='edge')
    p = np.apply_along_axis(lambda v: np.convolve(v, k, 'valid'), 1, p)
    return np.apply_along_axis(lambda v: np.convolve(v, k, 'valid'), 0, p)
H = _blur(H, 1.3)
rows, cols = H.shape
W, D = cols - 1, rows - 1


def xz(p):
    if isinstance(p, dict):
        return float(p['x']), float(p['z'])
    return float(p[0]), float(p[1])


def pts(o, *keys):
    for k in keys:
        if k in o and o[k]:
            return [xz(p) for p in o[k]]
    return []


def centre(o):
    c = o.get('center_m') or o.get('center') or o
    return float(c['x']), float(c['z'])


def size(o):
    f = o.get('footprint') or {}
    if f.get('shape') == 'circle':
        r = float(f.get('radius_m', 8))
        return 2 * r, 2 * r
    w = f.get('width_x_m') or f.get('size_x_m') or f.get('width_m') or 10
    d = f.get('depth_z_m') or f.get('size_z_m') or f.get('depth_m') or 10
    return float(w), float(d)


def facing(o):
    for k in ('facing_degrees', 'facing_deg_clockwise_from_north', 'orientation_degrees_clockwise_from_north', 'facing_degrees_from_north', 'facing_deg'):
        if o.get(k) is not None:
            return float(o[k])
    return 180.0


def kind(t):
    t = (t or '').lower()
    for key, k in (('blocked', 'barrier'), ('barrier', 'barrier'), ('stage_gate', 'barrier'), ('camp', 'camp'),
                   ('command', 'explore'), ('tent', 'explore'), ('city', 'town'), ('hub', 'town'),
                   ('fortress', 'gate_town'), ('gate', 'gate_town'), ('village', 'village'), ('relay', 'post_station'),
                   ('waystation', 'post_station'), ('ferry', 'ferry'), ('bridge', 'bridge'), ('battle', 'battlefield'),
                   ('exit', 'exit')):
        if key in t:
            return k
    return 'explore'


# ---- places, each plot flattened to its stated ground height (blended over 8 m) ----
places = []
for o in L.get('locations', []) + L.get('sites', []):
    x, z = centre(o)
    w, d = size(o)
    k = kind(o.get('type'))
    y = o.get('ground_height_m')
    p = dict(id=o['id'], name=o.get('name_zh', o['id']), kind=k, x=x, z=z, y=float(y if y is not None else 0),
             w=w, d=d, yaw=facing(o) - 180.0, year=int(o.get('open_year') or 0))
    p.update(hubs.get(o['id'], {}))
    places.append(p)
    if y is None or k in ('bridge', 'explore', 'battlefield', 'exit') or o.get('type') in ('river_bend', 'siege_earthwork'):
        continue
    r = 8.0
    c, s = math.cos(math.radians(p['yaw'])), math.sin(math.radians(p['yaw']))
    x0, x1 = int(max(0, x - w - r)), int(min(W, x + w + r))
    z0, z1 = int(max(0, z - d - r)), int(min(D, z + d + r))
    for zz in range(z0, z1 + 1):
        for xx in range(x0, x1 + 1):
            dx, dz = xx - x, zz - z
            lx, lz = abs(dx * c - dz * s), abs(dx * s + dz * c)
            out_d = max(lx - w / 2 - 3, lz - d / 2 - 3, 0)
            if out_d >= r:
                continue
            t = out_d / r
            t = t * t * (3 - 2 * t)
            H[zz, xx] = float(y) * (1 - t) + H[zz, xx] * t

# ---- roads, water ----
roads = []
for o in L.get('roads', []):
    ps = pts(o, 'points_xz', 'control_points', 'points')
    if len(ps) >= 2:
        roads.append(dict(id=o['id'], width=float(o.get('width_m', 5)), pts=[v for p in ps for v in p]))
waters = []
for o in L.get('rivers', []):
    ps = pts(o, 'centerline_xz', 'centerline', 'points', 'points_xz')
    cl = o.get('centerline') or []
    wid = o.get('width_m') or (sum(q.get('width_m', 12) for q in cl) / len(cl) if cl and isinstance(cl[0], dict) else 12)
    lvl = o.get('water_surface_y_m', o.get('water_surface_m', 0))
    levels = [float(q['water_surface_m']) for q in cl if isinstance(q, dict) and 'water_surface_m' in q]
    if levels and len(levels) == len(ps):
        lvl = sum(levels) / len(levels)
    else:
        levels = []
    if len(ps) >= 2:
        waters.append(dict(id=o['id'], level=float(lvl), width=float(wid), line=[v for p in ps for v in p], levels=levels))
for i, o in enumerate(L.get('wetland_pools', []) + L.get('lakes', []) + L.get('ponds', [])):
    ps = pts(o, 'outline_xz', 'outline', 'polygon_xz')
    if len(ps) >= 3:
        waters.append(dict(id=o.get('id', f'pool_{i}'), level=float(o.get('water_surface_m', o.get('level_m', -0.8))),
                           width=0, poly=[v for p in ps for v in p]))

# Places the refinement's wild set already models (its placements carry location_id) keep only their labels.
wild = hubs.get('_wild')
if wild:
    wp = os.path.join(os.path.dirname(os.path.normpath(src)), wild['set'], wild['layout'])
    if os.path.exists(wp):
        modelled = {q.get('location_id') for q in json.load(open(wp, encoding='utf-8'))['placements'] if q.get('location_id')}
        for p in places:
            if p['id'] in modelled and 'set' not in p:
                p['skip'] = True
ents = L.get('entrances') or L.get('region_entries') or L.get('region_entrances') or []
open_ents = [e for e in ents if 'x' in e and (str(e.get('status', 'open')) == 'open' and e.get('open', True))] or [e for e in ents if 'x' in e]
sx, sz = hubs.get('_spawn') or (xz(open_ents[0]) if open_ents else (W / 2, D / 2))
sx = min(max(sx, 8), W - 8)
sz = min(max(sz, 8), D - 8)
zone = (L.get('opening_184') or {}).get('polygon_xz') or []

region = dict(id=P, name=name, scene=f'Region_{P}', width=W, height=D, places=places, roads=roads, waters=waters,
              spawnX=sx, spawnZ=sz, openZone=[float(v) for p in zone for v in p],
              wildSet=(hubs.get('_wild') or {}).get('set', ''), wildLayout=(hubs.get('_wild') or {}).get('layout', ''), wildSkip=(hubs.get('_wild') or {}).get('skip', []))
json.dump(region, open(os.path.join(out, 'region.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
with open(os.path.join(out, 'heights.bin'), 'wb') as f:
    f.write(H.astype('<f4').tobytes())

# ---- ground tint: the painted layout with its contour lines and small details filtered out ----
m = Image.open(os.path.join(src, f'{P}_TERRAIN_MAP.png')).convert('RGB')
m = m.filter(ImageFilter.MedianFilter(5)).resize((W * 4, D * 4), Image.LANCZOS)
m.save(os.path.join(out, 'ground_tint.png'))
Image.open(os.path.join(src, f'{P}_TERRAIN_MAP.png')).convert('RGB').save(os.path.join(out, 'map.png'))
Image.open(os.path.join(src, f'{P}_TERRAIN_MAP_LABELED.png')).convert('RGB').save(os.path.join(out, 'map_labeled.png'))
print(P, len(places), 'places', len(roads), 'roads', len(waters), 'waters', 'spawn', (sx, sz), 'h', round(H.min(), 2), round(H.max(), 2))
