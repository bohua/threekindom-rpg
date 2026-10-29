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
    if f.get('shape') == 'circle' or ('radius_m' in f and 'width_m' not in f):
        r = float(f.get('radius_m', 8))
        return 2 * r, 2 * r
    w = f.get('width_x_m') or f.get('size_x_m') or f.get('width_m') or 10
    d = f.get('depth_z_m') or f.get('size_z_m') or f.get('depth_m') or 10
    return float(w), float(d)


def facing(o):
    for k in ('facing_degrees', 'facing_deg_clockwise_from_north', 'facing_degrees_from_north', 'facing_deg', 'yaw_degrees'):
        if o.get(k) is not None:
            return float(o[k])
    return 180.0


def kind(t):
    t = (t or '').lower()
    for key, k in (('blocked', 'barrier'), ('checkpoint', 'explore'), ('orchard', 'explore'), ('future', 'explore'), ('barrier', 'barrier'), ('stage_gate', 'barrier'), ('camp', 'camp'),
                   ('command', 'explore'), ('tent', 'explore'), ('city', 'town'), ('hub', 'town'),
                   ('fortress', 'gate_town'), ('gate', 'gate_town'), ('village', 'village'), ('relay', 'post_station'),
                   ('waystation', 'post_station'), ('post_station', 'post_station'), ('ferry', 'ferry'), ('bridge', 'bridge'), ('battle', 'battlefield'),
                   ('exit', 'exit')):
        if key in t:
            return k
    return 'explore'


# ---- places, each plot flattened to its stated ground height (blended over 8 m) ----
places = []
# Houses Codex placed one by one (R1 'structures'): drawn as houses, and their villages as a bare-earth yard only.
houses = [o for o in L.get('structures', []) if o.get('type') == 'house']
housed = {o.get('parent_id') for o in houses}
for o in L.get('locations') or L.get('places') or []:
    x, z = centre(o)
    w, d = size(o)
    k = kind(o.get('type'))
    y = o.get('ground_height_m')
    p = dict(id=o['id'], name=o.get('name_zh', o['id']), kind=k, x=x, z=z, y=float(y if y is not None else 0),
             w=w, d=d, yaw=facing(o) - 180.0, year=int(o.get('open_year') or 0))
    if o['id'] in housed and k == 'village':
        p['kind'] = 'hamlet'
    p.update(hubs.get(o['id'], {}))
    places.append(p)
    if p.get('flatten') and y is not None:
        # A hub-file override for a spot sitting in a fold of the hills (R1's beacon): a round level pad of the given
        # radius at its stated height, eased into the surrounding ground over the given ramp.
        rad, ramp = p['flatten']
        # Level at the higher of its stated height and the surrounding ground (80th percentile of the disc), so the
        # pad never sinks into a bowl that hides the party from the camera.
        ring = [H[zz, xx] for zz in range(int(max(0, z - rad - ramp)), int(min(D, z + rad + ramp)) + 1)
                for xx in range(int(max(0, x - rad - ramp)), int(min(W, x + rad + ramp)) + 1)
                if math.hypot(xx - x, zz - z) < rad + ramp]
        y = max(float(y), float(np.percentile(ring, 80)))
        p['y'] = round(y, 2)
        for zz in range(int(max(0, z - rad - ramp)), int(min(D, z + rad + ramp)) + 1):
            for xx in range(int(max(0, x - rad - ramp)), int(min(W, x + rad + ramp)) + 1):
                dd = math.hypot(xx - x, zz - z)
                if dd >= rad + ramp:
                    continue
                t = max(0.0, (dd - rad) / ramp)
                t = t * t * (3 - 2 * t)
                H[zz, xx] = float(y) * (1 - t) + H[zz, xx] * t
        continue
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

for o in houses:
    if (hubs.get(o.get('parent_id')) or {}).get('layout'):
        continue   # the village's own Blender layout places its houses
    x, z = centre(o)
    w, d = size(o)
    places.append(dict(id=o['id'], name='民居', kind='house', x=x, z=z, y=float(o.get('ground_height_m') or 0), w=w, d=d,
                       yaw=facing(o) - 180.0, year=0))
    hm_ = hubs.get('_house')   # a Blender house for the villages without a layout of their own
    if hm_:
        places[-1].update(set=hm_['set'], model=hm_['models'][len([q for q in places if q['kind'] == 'house']) % len(hm_['models'])])
        # Blender houses stand on their front wall's foot: move the origin from the plot centre to the front.
        f_ = math.radians(facing(o))
        places[-1]['x'] = round(x + math.sin(f_) * d / 2, 2)
        places[-1]['z'] = round(z + math.cos(f_) * d / 2, 2)
for o in L.get('bridges', []):
    (ax, az), (bx, bz) = o['deck_polyline_xz'][0], o['deck_polyline_xz'][-1]
    x, z = centre(o)
    places.append(dict(id=o['id'], name=o.get('name_zh', '木桥'), kind='bridge', x=x, z=z, y=float(o.get('deck_y_m', 0)),
                       w=float(o.get('length_m', 20)), d=float(o.get('width_m', 4)),
                       yaw=-math.degrees(math.atan2(bz - az, bx - ax)), year=0))
    places[-1].update(hubs.get(o['id'], {}))
for i, o in enumerate(L.get('exits', []) if isinstance(L.get('exits'), list) else []):
    places.append(dict(id=o.get('id', f'exit_{i}'), name=o.get('name_zh', '出口'), kind='exit', x=min(max(float(o['x']), 4), W - 4),
                       z=min(max(float(o['z']), 4), D - 4), y=float(o.get('y', 0)), w=8, d=8, yaw=0, year=0))
    places[-1].update(hubs.get(places[-1]['id'], {}))
for o in L.get('gates', []):
    if o['id'] in hubs:   # gates the hub file wires up (portals into a town scene)
        g = dict(id=o['id'], name=o.get('name_zh', o['id']), kind='gate', x=float(o['x']), z=float(o['z']), y=float(o.get('y', 0)),
                 w=8, d=4, yaw=facing(o) - 180.0, year=0)
        g.update(hubs[o['id']])
        places.append(g)

# ---- roads, water ----
roads = []
for o in L.get('roads', []):
    ps = pts(o, 'points_xz', 'control_points', 'points')
    if len(ps) >= 2:
        roads.append(dict(id=o['id'], width=float(o.get('width_m', 5)), pts=[v for p in ps for v in p]))
waters = []
rivers = L.get('rivers') or ([dict(L['river'], id=L['river'].get('id', 'river'))] if isinstance(L.get('river'), dict) else [])
for o in rivers:
    cl = o.get('centerline') or []
    if cl and isinstance(cl[0], dict) and 'water_y_m' in cl[0]:
        wid = float(o.get('water_width_m') or o.get('width_m') or 12)
        for i in range(len(cl) - 1):
            a, b = cl[i], cl[i + 1]
            waters.append(dict(id=f"{o['id']}_{i}", level=(a['water_y_m'] + b['water_y_m']) / 2, width=wid,
                               line=[a['x'], a['z'], b['x'], b['z']]))
        continue
    ps = pts(o, 'centerline_xz', 'centerline', 'points', 'points_xz')
    cl = o.get('centerline') or []
    wid = o.get('width_m') or (sum(q.get('width_m', 12) for q in cl) / len(cl) if cl and isinstance(cl[0], dict) else 12)
    lvl = o.get('water_surface_y_m', o.get('water_surface_m', 0))
    if len(ps) >= 2:
        waters.append(dict(id=o['id'], level=float(lvl), width=float(wid), line=[v for p in ps for v in p]))
for i, o in enumerate(L.get('wetland_pools', []) + L.get('lakes', []) + L.get('ponds', [])):
    ps = pts(o, 'outline_xz', 'outline', 'polygon_xz')
    if len(ps) >= 3:
        waters.append(dict(id=o.get('id', f'pool_{i}'), level=float(o.get('water_surface_m', o.get('level_m', -0.8))),
                           width=0, poly=[v for p in ps for v in p]))

# Hub-file "face_water": turn a place (a ferry landing) so its front faces the nearest river and move it to the bank,
# its origin this far from the centre line, so the jetty in front reaches over the water.
for p in places:
    if not p.get('face_water'):
        continue
    best = None
    for w in waters:
        ln = w.get('line') or []
        for i in range(0, len(ln) - 2, 2):
            ax, az, bx, bz = ln[i], ln[i + 1], ln[i + 2], ln[i + 3]
            n = max(1, int(math.hypot(bx - ax, bz - az)))
            for k in range(n + 1):   # sampled every metre, keeping 30 m clear of bridges
                qx, qz = ax + (bx - ax) * k / n, az + (bz - az) * k / n
                if any(b['kind'] == 'bridge' and math.hypot(qx - b['x'], qz - b['z']) < 30 for b in places):
                    continue
                dd = math.hypot(p['x'] - qx, p['z'] - qz)
                if best is None or dd < best[0]:
                    best = (dd, qx, qz, w['width'], w['level'])
    if best:
        dd, qx, qz, wid, lvl = best
        ux, uz = (qx - p['x']) / dd, (qz - p['z']) / dd           # unit vector, place → water
        off = wid / 2 + float(p['face_water'])
        p['x'], p['z'] = round(qx - ux * off, 2), round(qz - uz * off, 2)
        p['yaw'] = round(math.degrees(math.atan2(-ux, -uz)), 1)   # model front (−Z at yaw 0) towards the water
        p['spotBehind'] = True
        p['y'] = round(lvl + 0.45, 2)   # the landing stands at least this high, its jetty and boat above the water

ents = L.get('entrances') or L.get('region_entries') or []
open_ents = [e for e in ents if 'x' in e and str(e.get('status', 'open')) == 'open'] or [e for e in ents if 'x' in e]
sx, sz = hubs.get('_spawn') or (xz(open_ents[0]) if open_ents else (W / 2, D / 2))
sx = min(max(sx, 8), W - 8)
sz = min(max(sz, 8), D - 8)
zone = (L.get('opening_184') or {}).get('polygon_xz') or []

walls = [dict(dict(id=o['id'], height=6.0, pts=[float(v) for p in o['polyline_xz'] for v in p]), **(hubs.get('_wall') or {}))
         for o in L.get('barriers', []) if o.get('polyline_xz') and not o.get('passable', False)]

scatter = []
# ---- blended ground (hubs "_ground", R1 user 2026-10-28 "野外精修"): a 2 px/m control map for GroundSplat ----
# R = dirt (roads, village yards, place plots), G = fields (flat low land in noisy patches), B = river banks,
# A = hill slope (steep or high ground). Every edge is blurred and broken by noise, so grounds meet raggedly.
ground = hubs.get('_ground')
if ground:
    from PIL import ImageDraw
    PPM = 2
    cw, ch = W * PPM + 1, D * PPM + 1
    rng = np.random.default_rng(11)

    def noise(scale, amp):
        small = rng.random((max(2, ch // scale), max(2, cw // scale)))
        return (np.array(Image.fromarray((small * 255).astype(np.uint8)).resize((cw, ch), Image.BICUBIC), np.float64) / 255 - 0.5) * amp

    def canvas():
        return Image.new('L', (cw, ch), 0)

    def px(x, z):
        return (x * PPM, (D - z) * PPM)   # image row 0 = north

    def soft(img, blur):
        return np.array(img.filter(ImageFilter.GaussianBlur(blur * PPM)), np.float64) / 255

    dirt = canvas(); d = ImageDraw.Draw(dirt)
    for r in roads:
        q = [px(r['pts'][i], r['pts'][i + 1]) for i in range(0, len(r['pts']), 2)]
        d.line(q, fill=255, width=int(r['width'] * PPM), joint='curve')
        for e in (q[0], q[-1]):
            rr = r['width'] * PPM / 2
            d.ellipse([e[0] - rr, e[1] - rr, e[0] + rr, e[1] + rr], fill=255)
    for p in places:
        if p['kind'] in ('hamlet', 'house', 'explore', 'post_station', 'camp', 'ferry', 'gate'):
            rx = rz = {'hamlet': 0.55 * min(p['w'], 40) / 2, 'explore': 5, 'gate': 4, 'camp': p['w'] / 2 + 2}.get(p['kind'], min(p['w'], 24) / 2 + 1)
            if p['kind'] == 'house':
                rx, rz = p['w'] / 2 + 3, p['d'] / 2 + 3
            cx, cz = px(p['x'], p['z'])
            d.ellipse([cx - rx * PPM, cz - rz * PPM, cx + rx * PPM, cz + rz * PPM], fill=255)
    R = np.clip(soft(dirt, 1.2) * 1.6 + noise(6, 0.5) - 0.15, 0, 1)

    bank = canvas(); d = ImageDraw.Draw(bank)
    for w in waters:
        if w.get('line'):
            q = [px(w['line'][i], w['line'][i + 1]) for i in range(0, len(w['line']), 2)]
            d.line(q, fill=255, width=int((w['width'] + 14) * PPM), joint='curve')
            for e in q:
                rr = (w['width'] + 14) * PPM / 2
                d.ellipse([e[0] - rr, e[1] - rr, e[0] + rr, e[1] + rr], fill=255)
        elif w.get('poly'):
            d.polygon([px(w['poly'][i], w['poly'][i + 1]) for i in range(0, len(w['poly']), 2)], fill=255)
    B = np.clip(soft(bank, 3) * 1.5 + noise(10, 0.6) - 0.2, 0, 1)

    # Heights at control resolution (row 0 = north).
    Hc = np.array(Image.fromarray(H[::-1].astype(np.float32), mode='F').resize((cw, ch), Image.BILINEAR), np.float64)
    gz, gx = np.gradient(Hc, 1.0 / PPM)
    slope = np.degrees(np.arctan(np.hypot(gx, gz)))
    def box(a, r):   # separable box blur, twice (≈ Gaussian), r in samples
        for _ in range(2):
            for ax in (0, 1):
                c = np.cumsum(np.pad(a, [(r + 1, r) if i == ax else (0, 0) for i in range(2)], mode='edge'), axis=ax)
                a = (np.take(c, range(2 * r + 1, c.shape[ax]), axis=ax) - np.take(c, range(0, c.shape[ax] - 2 * r - 1), axis=ax)) / (2 * r + 1)
        return a
    slope = box(slope, 3 * PPM)
    A = np.clip((slope - 14) / 8 + (Hc - 30) / 12 + noise(14, 0.9), 0, 1)

    fields = np.clip(noise(40, 2.2) - 0.1, 0, 1)                         # field patches over about a third of the flat land
    flat = np.clip(1 - slope / 6, 0, 1) * np.clip(1 - (Hc - 4) / 4, 0, 1)
    G = np.clip(fields * flat * 1.4 - R - B - A, 0, 1)
    A = np.clip(A - R * 0.8, 0, 1)
    # Graded copies of the painted layers (hubs "_ground".grade: {key: [saturation, brightness]}), so a region can calm
    # a layer that reads too loud under the midday sun without touching the Codex delivery.
    from PIL import ImageEnhance
    gdir = os.path.join(out, 'ground')
    os.makedirs(gdir, exist_ok=True)
    for k in (ground['base'], ground['r'], ground['g'], ground['b'], ground['a']):
        im = Image.open(os.path.join(os.path.dirname(src), ground['set'], k + '.png')).convert('RGB')
        sat, bri = (ground.get('grade') or {}).get(k, [1, 1])
        im = ImageEnhance.Brightness(ImageEnhance.Color(im).enhance(sat)).enhance(bri)
        im.save(os.path.join(gdir, k + '.png'))
    ctrl = np.dstack([R, G, B, A]) * 255
    Image.fromarray(ctrl.astype(np.uint8), 'RGBA').save(os.path.join(out, 'ground_control.png'))

# ---- vegetation (hubs "_scatter"): 3D trees, reeds and rocks placed by the ground they stand on ----
scatter = []
sc = hubs.get('_scatter')
if sc and ground:
    rs = np.random.default_rng(23)

    def at(ch_, x, z):
        u, v = int(round(x * PPM)), int(round((D - z) * PPM))
        return ch_[min(max(v, 0), ch - 1), min(max(u, 0), cw - 1)]

    blocked = []   # (x, z, radius) kept clear: places, towns
    for p in places:
        r = {'town': max(p['w'], p['d']) * 0.62, 'hamlet': p['w'] / 2 + 4, 'house': 6, 'gate': 6, 'bridge': p['w'] / 2 + 2, 'camp': max(p['w'], p['d']) * 0.72}.get(p['kind'], 9)
        if not p.get('skip') or p['kind'] in ('town', 'hamlet', 'explore', 'barrier'):
            blocked.append((p['x'], p['z'], r))
    wall_pts = [(w['pts'][i], w['pts'][i + 1]) for w in walls for i in range(0, len(w['pts']), 2)]

    def free(x, z, gap=0.0, bank=False):
        if x < 4 or z < 4 or x > W - 4 or z > D - 4:
            return False
        if at(R, x, z) > 0.15 or (at(B, x, z) > 0.85 and not bank):
            return False
        if any((x - bx) ** 2 + (z - bz) ** 2 < (br + gap) ** 2 for bx, bz, br in blocked):
            return False
        if wall_pts and min(abs(z - wz) for wx, wz in wall_pts if abs(wx - x) < 60) < 8 if any(abs(wx - x) < 60 for wx, wz in wall_pts) else False:
            return False
        return True

    def add(model, x, z, s0, s1, set_=None):
        scatter.append(dict(set=set_ or sc['set'], model=model, x=round(float(x), 2), z=round(float(z), 2),
                            yaw=round(float(rs.uniform(0, 360)), 1), scale=round(float(rs.uniform(s0, s1)), 2)))
        blocked.append((x, z, 2.5))

    # Willows and reeds along the river banks.
    for w in waters:
        if not w.get('line'):
            continue
        (ax, az), (bx, bz) = (w['line'][0], w['line'][1]), (w['line'][-2], w['line'][-1])
        L_ = math.hypot(bx - ax, bz - az)
        nx, nz = -(bz - az) / L_, (bx - ax) / L_
        for t in np.arange(0, L_, 9):
            for side in (-1, 1):
                off = w['width'] / 2 + rs.uniform(5, 11)
                x, z = ax + (bx - ax) * t / L_ + nx * off * side, az + (bz - az) * t / L_ + nz * off * side
                if rs.random() < 0.7 and free(x, z, bank=True):
                    add(sc['willow'], x, z, 0.85, 1.15)
                off = w['width'] / 2 + rs.uniform(0.5, 3)
                x, z = ax + (bx - ax) * (t + 4) / L_ + nx * off * side, az + (bz - az) * (t + 4) / L_ + nz * off * side
                if rs.random() < 0.6 and free(x, z, bank=True):
                    add(sc['reeds'], x, z, 0.8, 1.3)
    # Poplars in rows along the roads, broken where the road meets places.
    for r in roads:
        q = [(r['pts'][i], r['pts'][i + 1]) for i in range(0, len(r['pts']), 2)]
        dist = 0.0
        for (x0, z0), (x1, z1) in zip(q, q[1:]):
            seg = math.hypot(x1 - x0, z1 - z0)
            if seg < 1e-3:
                continue
            nx, nz = -(z1 - z0) / seg, (x1 - x0) / seg
            for t in np.arange(0, seg, 1.0):
                dist += 1.0
                if dist % 14 > 1.0:
                    continue
                side = 1 if int(dist / 14) % 2 else -1
                off = r['width'] / 2 + 3.5
                x, z = x0 + (x1 - x0) * t / seg + nx * off * side, z0 + (z1 - z0) * t / seg + nz * off * side
                if rs.random() < 0.6 and free(x, z) and at(A, x, z) < 0.4:
                    add(sc['poplar'], x, z, 0.85, 1.1)
    # Pines and granite on the hill slopes; elms, shrubs and wild peach in the open meadow, in loose clumps.
    for _ in range(9000):
        x, z = rs.uniform(4, W - 4), rs.uniform(4, D - 4)
        a_, g_ = at(A, x, z), at(G, x, z)
        if a_ > 0.45:
            if rs.random() < 0.10 and free(x, z):
                add(sc['pine'], x, z, 0.8, 1.25)
            elif rs.random() < 0.035 and free(x, z):
                add(sc['rock'], x, z, 0.7, 1.4)
        elif g_ < 0.2 and a_ < 0.2:
            n = rs.random()
            if n < 0.012 and free(x, z, 2):
                for _k in range(int(rs.integers(1, 4))):
                    xx, zz = x + rs.normal(0, 4), z + rs.normal(0, 4)
                    if free(xx, zz):
                        add(sc['elm'], xx, zz, 0.8, 1.15)
            elif n < 0.03 and free(x, z):
                add(sc['shrub'], x, z, 0.8, 1.3)
    # Mulberry groves round Lousang and the shrine, wild peach near the villages.
    for name_, model_, count_, set_ in sc.get('groves', []):
        p = next((p for p in places if p['id'] == name_), None)
        if not p:
            continue
        for _ in range(count_ * 4):
            if sum(1 for v in scatter if v.get('grove') == name_ + model_) >= count_:
                break
            ang, rad = rs.uniform(0, 2 * math.pi), rs.uniform(p['w'] / 2 + 6, p['w'] / 2 + 26)
            x, z = p['x'] + math.cos(ang) * rad, p['z'] + math.sin(ang) * rad
            if free(x, z):
                add(model_, x, z, 0.85, 1.2, set_)
                scatter[-1]['grove'] = name_ + model_
    for v in scatter:
        v.pop('grove', None)

region = dict(id=P, name=name, scene=f'Region_{P}', width=W, height=D, places=places, roads=roads, waters=waters,
              walls=walls, ground=ground or {}, scatter=scatter, spots=[dict(name=k, x=float(v[0]), z=float(v[1])) for k, v in (hubs.get('_spots') or {}).items()], spawnX=sx, spawnZ=sz, openZone=[float(v) for p in zone for v in p])
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
