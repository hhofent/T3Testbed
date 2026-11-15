"""Town makeover bake-off: one shared, deterministic scoring check.

Drop this file into python-spawner/checks/town_bakeoff.py UNCHANGED on every
bake-off branch, then run from python-spawner/:

    py checks/town_bakeoff.py                      # all 5 towns, ~1 min
    py checks/town_bakeoff.py --pytest             # also time the fast pytest subset
    py checks/town_bakeoff.py --seeds 31           # one seed while iterating

Writes check-results/town-bakeoff/<branch>/metrics.json, summary.md and one PNG
per town. check-results/ is git-ignored. Exit 0 only if every hard check passes.

What it reads: the finished town features from world.settlements(seed, rect,
serialized, detail=True), i.e. the same output the map export uses. Rivers and
paved-route offsets are stubbed exactly like tests/test_world.py
skip_river_generation (world.rivers -> (), world._route_offset -> 0), because
river generation costs ~20-90 s per seed and is not what this check scores.
Floodplain rejection stays covered by pytest. Use --with-rivers for a slow,
realistic run (anchors may then move; towns are matched within 0.6 mi).

Optional feature fields this check understands (fallbacks in brackets):
  towns     outline=[[x,y],...] world ticks, closed ring  [4 corners of rect]
  roads     road_class in {'main_street','town_street','alley'}  [id contains
            'main-' -> main_street, else town_street]; row_ft  [thinner side of
            rect]; polylines may use points=[[x,y],...] + width (ticks)
  alleys    either kind='roads' with road_class='alley', or kind='alleys'
  lots      use ('town'/'house' = house lot, 'business...' = business lot);
            polygon=[[x,y],...] optional  [rect]
  buildings business buildings: id contains 'business:' and use=<type>;
            elevator/industry/depot as today
  trees     species (snake_case, e.g. 'sugar_maple', 'green_ash',
            'american_elm'); setting ('town_street','yard',...)
Nothing else in the feature format may change.
"""
import argparse
import json
import math
import os
import random
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

import numpy as np  # matplotlib dependency, already installed with requirements.txt
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.path import Path as MplPath
from matplotlib.patches import Polygon as MplPolygon, Rectangle

from settings import Settings, applied
from presets import document
import world
from world import MILE
from spawner import FT
from verification import tree_collisions, building_collisions, orphan_roads

FT1 = FT(1)          # 192 ticks per foot
CELL_FT = 4          # raster cell for area metrics

# Fixed towns: (seed, anchor x mi, anchor y mi, label). Anchors are the town
# feature 'anchor' at 2469802 with rivers/route offsets stubbed. They cover
# tiny/mid/big, rail and highway towns, and both route axes.
TOWNS = [
    (14, -1.05, 1.0, 's14-tiny-rail-h'),
    (14, 0.95, 7.0, 's14-big-rail-v'),
    (14, 9.95, 7.0, 's14-mid-highway-v'),
    (30, 0.94, 1.0, 's30-big-rail-h'),
    (31, 1.05, -1.0, 's31-mid-rail-v'),
]
MATCH_MI = 0.6

# Targets (see SCORING.md for the reasoning behind each).
T = dict(
    hull_ratio=(0.60, 0.95), polsby_popper=(0.35, 0.90), min_vertices=8,
    contain_pct=99.0, lot_fill_pct=75.0, built_lot_pct=95.0,
    clump_index_max=0.85, nn_cv_min=0.45, nn_mode_share_max=0.20, trees_per_lot=(1.0, 2.5), max_trees_per_lot=6,
    maple_ash_min=0.60, american_elm_max=0.03, siberian_elm_max=0.10,
    biz_types={'tiny': 6, 'mid': 12, 'big': 15, 'large': 17},
    street_row=(66.0, 80.0), alley_row=20.0, main_row=100.0, row_tol=0.5,
    main_coverage=0.98, main_frontage_min=0.60, frontage_ft=60.0,
    town_runtime_s=40.0, total_runtime_s=120.0, pytest_s=120.0,
)

CANON = {
    'elevator': 'grain elevator', 'co-op elevator': 'grain elevator', 'co-op elevator (truck)': 'grain elevator',
    'grain elevator': 'grain elevator', 'co-op': 'co-op', 'co-op office': 'co-op', 'coop': 'co-op',
    'farm supply': 'co-op', 'industry': 'feed mill', 'feed mill': 'feed mill', 'feed mill / scales': 'feed mill',
    'gas station': 'gas station', 'cafe': 'cafe', 'bank': 'bank', 'bank (branch)': 'bank',
    'post office': 'post office', 'church': 'church', 'school': 'school', 'implement dealer': 'implement dealer',
    'tavern': 'tavern', 'bar': 'tavern', 'hardware': 'hardware/lumber', 'lumberyard': 'hardware/lumber',
    'lumber yard': 'hardware/lumber', 'grocery': 'grocery', 'grocery (highway)': 'grocery',
}
REQUIRED_UNION = ['grain elevator', 'co-op', 'gas station', 'cafe', 'bank', 'post office', 'church',
                  'school', 'implement dealer', 'tavern', 'hardware/lumber']
SINGLETONS = {'post office', 'city hall / fire', 'school', 'bank'}
NOT_A_BUSINESS = {'depot', 'house', None}


def canon(use):
    u = (use or '').strip().lower()
    if u.startswith('vacant'):
        return None
    return CANON.get(u, u)


def species_key(s):
    return None if s is None else str(s).strip().lower().replace(' ', '_').replace('-', '_')


# ---------------------------------------------------------------- geometry
def rect_of(f):
    return f.get('rect') or f['bounds']


def rect_poly(r):
    x, y, w, h = r
    return [(x, y), (x + w, y), (x + w, y + h), (x, y + h)]


def ring(points):
    pts = [tuple(map(float, p)) for p in points]
    if len(pts) > 1 and pts[0] == pts[-1]:
        pts = pts[:-1]
    out = []
    for p in pts:                       # drop repeated vertices
        if not out or p != out[-1]:
            out.append(p)
    return out


def area(poly):
    return abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(poly, poly[1:] + poly[:1]))) / 2


def perimeter(poly):
    return sum(math.dist(a, b) for a, b in zip(poly, poly[1:] + poly[:1]))


def hull(points):
    pts = sorted(set(points))
    if len(pts) < 3:
        return pts
    def cross(o, a, b): return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, hi = [], []
    for p in pts:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], p) <= 0: lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(hi) >= 2 and cross(hi[-2], hi[-1], p) <= 0: hi.pop()
        hi.append(p)
    return lo[:-1] + hi[:-1]


def _orient(a, b, c):
    v = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
    return 0 if v == 0 else (1 if v > 0 else -1)


def _on_seg(a, b, p):
    return min(a[0], b[0]) <= p[0] <= max(a[0], b[0]) and min(a[1], b[1]) <= p[1] <= max(a[1], b[1])


def seg_intersect(a, b, c, d):
    o1, o2, o3, o4 = _orient(a, b, c), _orient(a, b, d), _orient(c, d, a), _orient(c, d, b)
    if o1 != o2 and o3 != o4:
        return True
    return ((o1 == 0 and _on_seg(a, b, c)) or (o2 == 0 and _on_seg(a, b, d)) or
            (o3 == 0 and _on_seg(c, d, a)) or (o4 == 0 and _on_seg(c, d, b)))


def self_intersections(poly):
    n = len(poly); hits = []
    edges = [(poly[i], poly[(i + 1) % n]) for i in range(n)]
    for i in range(n):
        for j in range(i + 1, n):
            if j == i + 1 or (i == 0 and j == n - 1):
                # adjacent edges share one vertex; flag only a fold-back (collinear overlap)
                a, b = edges[i]; c, d = edges[j]
                shared = b if j == i + 1 else a
                other_i = a if j == i + 1 else b
                other_j = d if j == i + 1 else c
                if _orient(shared, other_i, other_j) == 0 and \
                   (other_i[0] - shared[0]) * (other_j[0] - shared[0]) + (other_i[1] - shared[1]) * (other_j[1] - shared[1]) > 0:
                    hits.append((i, j))
                continue
            if seg_intersect(*edges[i], *edges[j]):
                hits.append((i, j))
    return hits


def overlap_area(a, b):
    w = min(a[0] + a[2], b[0] + b[2]) - max(a[0], b[0])
    h = min(a[1] + a[3], b[1] + b[3]) - max(a[1], b[1])
    return w * h if w > 0 and h > 0 else 0


# ---------------------------------------------------------------- raster
class Raster:
    def __init__(self, bbox, cell_ft=CELL_FT):
        self.c = cell_ft * FT1
        self.x0, self.y0 = bbox[0], bbox[1]
        self.nx = int(math.ceil(bbox[2] / self.c)) + 1
        self.ny = int(math.ceil(bbox[3] / self.c)) + 1

    def blank(self):
        return np.zeros((self.ny, self.nx), dtype=bool)

    def _span(self, lo, hi, origin, n):
        a = max(0, int(math.ceil((lo - origin) / self.c - 0.5)))
        b = min(n, int(math.ceil((hi - origin) / self.c - 0.5)))
        return a, b

    def paint_rect(self, m, r):
        i0, i1 = self._span(r[0], r[0] + r[2], self.x0, self.nx)
        j0, j1 = self._span(r[1], r[1] + r[3], self.y0, self.ny)
        if i1 > i0 and j1 > j0:
            m[j0:j1, i0:i1] = True

    def paint_poly(self, m, poly):
        xs = [p[0] for p in poly]; ys = [p[1] for p in poly]
        i0, i1 = self._span(min(xs), max(xs), self.x0, self.nx)
        j0, j1 = self._span(min(ys), max(ys), self.y0, self.ny)
        if i1 <= i0 or j1 <= j0:
            return
        gx = self.x0 + (np.arange(i0, i1) + 0.5) * self.c
        gy = self.y0 + (np.arange(j0, j1) + 0.5) * self.c
        X, Y = np.meshgrid(gx, gy)
        inside = MplPath(poly).contains_points(np.column_stack([X.ravel(), Y.ravel()])).reshape(X.shape)
        m[j0:j1, i0:i1] |= inside

    def paint_polyline(self, m, pts, width):
        # buffer each segment by a rotated rectangle
        for a, b in zip(pts, pts[1:]):
            dx, dy = b[0] - a[0], b[1] - a[1]; L = math.hypot(dx, dy) or 1
            nx_, ny_ = -dy / L * width / 2, dx / L * width / 2
            self.paint_poly(m, [(a[0] + nx_, a[1] + ny_), (b[0] + nx_, b[1] + ny_), (b[0] - nx_, b[1] - ny_), (a[0] - nx_, a[1] - ny_)])

    def paint_feature(self, m, f):
        if f.get('polygon'):
            self.paint_poly(m, ring(f['polygon']))
        elif f.get('points') and f.get('width'):
            self.paint_polyline(m, [tuple(p) for p in f['points']], f['width'])
        else:
            self.paint_rect(m, rect_of(f))

    def cell_area_ft2(self):
        return (self.c / FT1) ** 2

    def centers(self, m):
        j, i = np.nonzero(m)
        return np.column_stack([self.x0 + (i + 0.5) * self.c, self.y0 + (j + 0.5) * self.c])


# ---------------------------------------------------------------- spatial stats
def nn_distances(P):
    P = np.asarray(P, dtype=float)
    n = len(P)
    if n < 2:
        return np.array([])
    out = np.empty(n)
    for k in range(0, n, 512):
        d = np.hypot(P[k:k + 512, None, 0] - P[None, :, 0], P[k:k + 512, None, 1] - P[None, :, 1])
        for r in range(d.shape[0]):
            d[r, k + r] = np.inf
        out[k:k + 512] = d.min(axis=1)
    return out


def nn_mode_share(P):
    """Share of nearest-neighbour distances within 1 ft of the most common value (a grid scores ~1)."""
    d = nn_distances(P) / FT1
    if len(d) == 0:
        return None
    vals, cnt = np.unique(np.round(d), return_counts=True)
    mode = vals[cnt.argmax()]
    return float((np.abs(d - mode) <= 1.0).mean())


def clark_evans(P, area_ticks2):
    d = nn_distances(P)
    if len(d) == 0 or area_ticks2 <= 0:
        return None, None
    expected = 0.5 / math.sqrt(len(P) / area_ticks2)
    return float(d.mean() / expected), float(d.std() / d.mean()) if d.mean() else None


def reference_patterns(allowed_centers, n, area_ticks2, cell, rng, runs=20):
    """R for complete spatial randomness and for a square grid, in the same allowed area."""
    if n < 2 or len(allowed_centers) < n:
        return None, None, None
    rs = []
    for _ in range(runs):
        idx = rng.choice(len(allowed_centers), size=n, replace=False)
        pts = allowed_centers[idx] + rng.uniform(-cell / 2, cell / 2, size=(n, 2))
        rs.append(clark_evans(pts, area_ticks2)[0])
    spacing = math.sqrt(area_ticks2 / n)
    pts = allowed_centers
    snapped = np.unique(np.floor((pts - pts.min(axis=0)) / spacing).astype(int), axis=0)
    grid = snapped * spacing + pts.min(axis=0) + spacing / 2
    rg = clark_evans(grid, area_ticks2)[0] if len(grid) >= 2 else None
    return float(np.mean(rs)), float(np.std(rs)), rg


# ---------------------------------------------------------------- town extraction
def stub_rivers():
    world.rivers = lambda seed, rect: ()
    world._route_offset = lambda *args: 0


def town_features(seed, ax, ay, settings, serialized):
    rect = ((ax - 0.25) * MILE, (ay - 0.25) * MILE, 0.5 * MILE, 0.5 * MILE)
    with applied(settings):
        feats = world.settlements(seed, rect, serialized, True)
    towns = [f for f in feats if f['kind'] == 'towns']
    if not towns:
        return None, []
    def anchor(f): return f.get('anchor') or (rect_of(f)[0] + rect_of(f)[2] / 2, rect_of(f)[1] + rect_of(f)[3] / 2)
    town = min(towns, key=lambda f: math.dist(anchor(f), (ax * MILE, ay * MILE)))
    if math.dist(anchor(town), (ax * MILE, ay * MILE)) > MATCH_MI * MILE:
        return None, []
    prefix = town['id'][:-len('town')] if town['id'].endswith('town') else None
    mine = [f for f in feats if prefix and f['id'].startswith(prefix)] if prefix else feats
    if len(mine) < 2:
        mine = feats
    return town, mine


def road_class(f):
    if f['kind'] == 'alleys':
        return 'alley'
    c = f.get('road_class')
    if c:
        return c
    return 'main_street' if 'main-' in f['id'] else 'town_street'


def road_row_ft(f):
    if f.get('row_ft') is not None:
        return float(f['row_ft'])
    if f.get('points') and f.get('width'):
        return f['width'] / FT1
    r = rect_of(f)
    return min(r[2], r[3]) / FT1


def road_axis(f):
    if f.get('axis'):
        return f['axis']
    r = rect_of(f)
    return 'h' if r[2] >= r[3] else 'v'


# ---------------------------------------------------------------- scoring one town
def score_town(seed, label, town, feats, rng):
    m = {'seed': seed, 'label': label, 'town_id': town['id'], 'size': town.get('size'),
         'rail_town': town.get('rail_town'), 'route_axis': town.get('route_axis'),
         'anchor': list(town.get('anchor') or []), 'checks': {}, 'notes': []}
    chk = m['checks']
    def put(name, ok, value, target, proxy=None):
        chk[name] = {'pass': bool(ok), 'value': value, 'target': target}
        if proxy:
            chk[name]['proxy'] = proxy

    roads = [f for f in feats if f['kind'] in ('roads', 'alleys')]
    lots = [f for f in feats if f['kind'] == 'lots']
    buildings = [f for f in feats if f['kind'] == 'buildings']
    drives = [f for f in feats if f['kind'] == 'driveways']
    trees = [f for f in feats if f['kind'] == 'trees']
    house_lots = [f for f in lots if not str(f.get('use', '')).startswith('business')]

    # 1. outline -------------------------------------------------------------
    if town.get('outline'):
        outline = ring(town['outline']); m['outline_source'] = 'outline'
    else:
        outline = [tuple(map(float, p)) for p in rect_poly(rect_of(town))]
        m['outline_source'] = 'rect fallback (no outline field)'
    hits = self_intersections(outline) if len(outline) >= 3 else [('too few vertices',)]
    a = area(outline) if len(outline) >= 3 else 0
    put('outline_simple_closed', len(outline) >= 3 and not hits and a > 0,
        {'vertices': len(outline), 'self_intersections': len(hits)}, 'closed ring, >=3 vertices, 0 self-intersections, area > 0')
    hv = hull(outline)
    hull_ratio = a / area(hv) if len(hv) >= 3 and area(hv) else 0
    pp = 4 * math.pi * a / perimeter(outline) ** 2 if a else 0
    put('blobby_outline', T['hull_ratio'][0] <= hull_ratio <= T['hull_ratio'][1] and
        T['polsby_popper'][0] <= pp <= T['polsby_popper'][1] and len(outline) >= T['min_vertices'],
        {'hull_ratio': round(hull_ratio, 3), 'polsby_popper': round(pp, 3), 'vertices': len(outline)},
        f"hull_ratio {T['hull_ratio']}, Polsby-Popper {T['polsby_popper']}, vertices >= {T['min_vertices']} (a rectangle scores hull_ratio 1.0)")
    m['outline_area_acres'] = round(a / FT1 ** 2 / 43560, 1)

    # raster over the outline plus everything the town owns
    pts = list(outline) + [p for f in roads + lots + buildings + drives for p in rect_poly(rect_of(f))]
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    pad = 100 * FT1
    R = Raster((min(xs) - pad, min(ys) - pad, max(xs) - min(xs) + 2 * pad, max(ys) - min(ys) + 2 * pad))
    inside = R.blank(); R.paint_poly(inside, outline)
    road_m = R.blank()
    for f in roads: R.paint_feature(road_m, f)
    lot_m = R.blank()
    for f in lots: R.paint_feature(lot_m, f)
    bld_m = R.blank()
    for f in buildings: R.paint_feature(bld_m, f)
    drv_m = R.blank()
    for f in drives: R.paint_feature(drv_m, f)
    reserved_m = R.blank()
    for r in town.get('reserved', []) or []: R.paint_rect(reserved_m, r)

    content = lot_m | bld_m
    c_total = int(content.sum())
    contain = 100.0 * int((content & inside).sum()) / c_total if c_total else 0
    put('outline_contains_town', contain >= T['contain_pct'], round(contain, 2),
        f">= {T['contain_pct']}% of lot+building area inside the outline (export: town owns what is inside)")

    # 2. fill -------------------------------------------------------------------
    buildable = inside & ~road_m
    covered = buildable & (lot_m | bld_m | drv_m | reserved_m)
    fill = 100.0 * int(covered.sum()) / int(buildable.sum()) if buildable.sum() else 0
    built = 0
    for L in house_lots:
        r = rect_of(L)
        if any(overlap_area(r, rect_of(b)) > 0 for b in buildings):
            built += 1
    built_pct = 100.0 * built / len(house_lots) if house_lots else 0
    put('lot_fill', fill >= T['lot_fill_pct'], round(fill, 1),
        f">= {T['lot_fill_pct']}% of non-road land inside the outline is in a lot, building, drive or reserved parcel",
        proxy='business lots are not emitted at 2469802; business/industry building footprints stand in for them')
    put('built_lots', built_pct >= T['built_lot_pct'], {'pct': round(built_pct, 1), 'house_lots': len(house_lots), 'with_building': built},
        f">= {T['built_lot_pct']}% of house lots carry a building (no empty sparse lots)")

    # 3. trees ------------------------------------------------------------------
    tp = np.array([[rect_of(t)[0], rect_of(t)[1]] for t in trees], dtype=float) if trees else np.zeros((0, 2))
    allowed = inside & ~road_m & ~bld_m & ~drv_m
    allowed_area = float(allowed.sum()) * R.c ** 2
    r_obs, cv = clark_evans(tp, allowed_area) if len(tp) >= 2 else (None, None)
    r_csr, r_csr_sd, r_grid = reference_patterns(R.centers(allowed), len(tp), allowed_area, R.c, rng)
    ci = (r_obs / r_csr) if (r_obs is not None and r_csr) else None
    ms = nn_mode_share(tp) if len(tp) >= 2 else None
    put('tree_clumping', ci is not None and ci <= T['clump_index_max'] and cv is not None and cv >= T['nn_cv_min']
        and ms is not None and ms <= T['nn_mode_share_max'],
        {'trees': len(tp), 'clark_evans_R': None if r_obs is None else round(r_obs, 3),
         'R_random_mean': None if r_csr is None else round(r_csr, 3), 'R_random_sd': None if r_csr_sd is None else round(r_csr_sd, 3),
         'R_square_grid': None if r_grid is None else round(r_grid, 3),
         'clump_index': None if ci is None else round(ci, 3), 'nn_cv': None if cv is None else round(cv, 3),
         'nn_mode_share': None if ms is None else round(ms, 3)},
        f"clump_index = R_obs / R_random <= {T['clump_index_max']} (1 = random, grid > 1); NN-distance CV >= {T['nn_cv_min']} (random ~0.52, grid ~0); "
        f"share of NN distances at the modal value <= {T['nn_mode_share_max']} (grid ~1)")
    per_lot = []
    for L in house_lots:
        x, y, w, h = rect_of(L)
        per_lot.append(int(((tp[:, 0] >= x) & (tp[:, 0] <= x + w) & (tp[:, 1] >= y) & (tp[:, 1] <= y + h)).sum()) if len(tp) else 0)
    mean_lot = float(np.mean(per_lot)) if per_lot else 0
    put('trees_per_lot', T['trees_per_lot'][0] <= mean_lot <= T['trees_per_lot'][1] and (max(per_lot) if per_lot else 0) <= T['max_trees_per_lot'],
        {'mean': round(mean_lot, 2), 'max': max(per_lot) if per_lot else 0, 'lots_with_none_pct': round(100 * sum(c == 0 for c in per_lot) / len(per_lot), 1) if per_lot else None},
        f"mean {T['trees_per_lot']} per house lot, max {T['max_trees_per_lot']} (LUNA overhaul-2 item G)")
    blockers = roads + buildings + drives + [dict(kind='roads', id=f'alleyfix{i}', bounds=rect_of(f), rect=rect_of(f)) for i, f in enumerate(f for f in feats if f['kind'] == 'alleys')]
    conflicts = tree_collisions(blockers + trees)
    put('tree_conflicts', not conflicts, {'count': len(conflicts), 'examples': conflicts[:5]},
        '0 trees within 1.5 ft of a road, alley, building (house pad) or driveway (verification.tree_collisions)',
        proxy='porches/decks are not emitted as features; house room rects stand in for the house pad')
    sp = [species_key(t.get('species')) for t in trees]
    n = len(sp) or 1
    missing = sum(s is None for s in sp)
    maple_ash = sum(1 for s in sp if s and ('maple' in s or s == 'green_ash')) / n
    am_elm = sum(1 for s in sp if s in ('american_elm', 'elm')) / n
    sib_elm = sum(1 for s in sp if s == 'siberian_elm') / n
    counts = {}
    for s in sp: counts[str(s)] = counts.get(str(s), 0) + 1
    put('species_mix', trees and missing == 0 and maple_ash >= T['maple_ash_min'] and am_elm <= T['american_elm_max'] and sib_elm <= T['siberian_elm_max'],
        {'maple_or_green_ash': round(maple_ash, 3), 'american_elm': round(am_elm, 3), 'siberian_elm': round(sib_elm, 3),
         'missing_species': missing, 'counts': dict(sorted(counts.items(), key=lambda kv: -kv[1]))},
        f"every tree has species; maples+green ash >= {T['maple_ash_min']:.0%}; American elm <= {T['american_elm_max']:.0%}; Siberian elm <= {T['siberian_elm_max']:.0%} (Wray & Mize 1985)")

    # 4. businesses ---------------------------------------------------------------
    biz = []
    for b in buildings:
        bid = b['id']
        if 'business:' in bid or 'industry:' in bid or bid.endswith(':depot'):
            biz.append((canon(b.get('use')), b))
    for L in lots:
        u = str(L.get('use', ''))
        if u.startswith('business'):
            t = u.split(':', 1)[1] if ':' in u else None
            if t: biz.append((canon(t), L))
    types = {}
    for t, f in biz:
        if t in NOT_A_BUSINESS:
            continue
        types.setdefault(t, set()).add(f['id'].split(':room:')[0])
    vacant = sum(1 for b in buildings if str(b.get('use', '')).startswith('vacant'))
    need = T['biz_types'].get(m['size'], 5)
    dup = {t: len(v) for t, v in types.items() if t in SINGLETONS and len(v) > 1}
    put('business_variety', len(types) >= need and not dup,
        {'distinct_types': len(types), 'types': sorted(types), 'vacant': vacant, 'duplicate_singletons': dup},
        f"distinct non-vacant types >= {need} for size '{m['size']}'; at most one post office, bank, school, city hall each")
    m['business_types'] = sorted(types)

    # 5. street widths ------------------------------------------------------------
    by_class = {}
    for f in roads:
        by_class.setdefault(road_class(f), []).append(round(road_row_ft(f), 2))
    tol = T['row_tol']
    st_ok = all(any(abs(w - s) <= tol for s in T['street_row']) for w in by_class.get('town_street', []))
    al = by_class.get('alley', [])
    al_ok = bool(al) and all(abs(w - T['alley_row']) <= tol for w in al)
    mn = by_class.get('main_street', [])
    mn_ok = bool(mn) and all(abs(w - T['main_row']) <= tol for w in mn)
    put('street_row_widths', st_ok and al_ok and mn_ok,
        {k: sorted(set(v)) for k, v in by_class.items()},
        f"town_street ROW in {T['street_row']} ft, alley {T['alley_row']} ft (alleys must be emitted), main_street {T['main_row']} ft, +/-{tol} ft")

    # 6. main street continuity ---------------------------------------------------
    # The highway through town: a town may set highway_axis explicitly. Otherwise a highway
    # town's highway is its route (route_axis); a rail town sits where the rail meets the
    # paved blacktop (region.make_rail/make_town), which runs across the rail.
    ra = town.get('route_axis') or 'v'
    axis = town.get('highway_axis') or (ra if not town.get('rail_town') else ('h' if ra == 'v' else 'v'))
    m['highway_axis'] = axis
    anc = town.get('anchor') or (0, 0)
    mains = [f for f in roads if road_class(f) == 'main_street' and road_axis(f) == axis]
    k = 0 if axis == 'v' else 1          # coordinate across the route
    line_c = anc[0] if axis == 'v' else anc[1]
    # centreline of the outline chord on the route line
    samples = np.linspace(min(p[1 - k] for p in outline), max(p[1 - k] for p in outline), 4000)
    pts_line = np.column_stack([np.full_like(samples, line_c), samples]) if axis == 'v' else np.column_stack([samples, np.full_like(samples, line_c)])
    in_line = MplPath(outline).contains_points(pts_line)
    chord = (samples[in_line].min(), samples[in_line].max()) if in_line.any() else (0, 0)
    ivs = []
    offsets = []
    for f in mains:
        if f.get('points'):
            p = np.asarray(f['points'], dtype=float)
            ivs.append((p[:, 1 - k].min(), p[:, 1 - k].max())); offsets.append(float(np.abs(p[:, k] - line_c).max()))
        else:
            r = rect_of(f)
            ivs.append((r[1 - k], r[1 - k] + r[3 - k])); offsets.append(abs(r[k] + r[2 + k] / 2 - line_c))
    ivs.sort(); merged = []
    for lo, hi in ivs:
        if merged and lo <= merged[-1][1] + FT1:
            merged[-1][1] = max(merged[-1][1], hi)
        else:
            merged.append([lo, hi])
    span = chord[1] - chord[0]
    cov = 0.0
    if span > 0:
        cov = sum(max(0, min(hi, chord[1]) - max(lo, chord[0])) for lo, hi in merged) / span
    gaps = max(0, len([1 for lo, hi in merged if hi > chord[0] and lo < chord[1]]) - 1)
    max_off = max(offsets) / FT1 if offsets else None
    put('main_street_continuity', bool(mains) and cov >= T['main_coverage'] and gaps == 0 and max_off is not None and max_off <= 1.0,
        {'pieces': len(mains), 'coverage_of_outline_chord': round(cov, 3), 'gaps': gaps,
         'max_offset_from_route_line_ft': None if max_off is None else round(max_off, 2)},
        f"main_street along the route axis covers >= {T['main_coverage']:.0%} of the outline chord on the highway line, 0 gaps, centred on the route line within 1 ft",
        proxy='the countryside highway is not in the town scene; the line through the town anchor on highway_axis stands in for it')
    store = [f for t, f in biz if t not in NOT_A_BUSINESS and t not in ('grain elevator', 'feed mill', 'school', 'implement dealer', 'church') and f['kind'] == 'buildings']
    near = 0
    for f in store:
        r = rect_of(f); g = T['frontage_ft'] * FT1
        grown = (r[0] - g, r[1] - g, r[2] + 2 * g, r[3] + 2 * g)
        if any(overlap_area(grown, rect_of(mf)) > 0 for mf in mains):
            near += 1
    share = near / len(store) if store else 0
    put('main_street_frontage', share >= T['main_frontage_min'], {'downtown_businesses': len(store), 'within_60ft_of_main': near, 'share': round(share, 3)},
        f">= {T['main_frontage_min']:.0%} of downtown businesses (not elevator/feed mill/school/implement/church) within {T['frontage_ft']:.0f} ft of Main Street")

    # 7. road conflicts -----------------------------------------------------------
    parallel = []
    rr = [f for f in roads if not f.get('points')]
    for i, a_ in enumerate(rr):
        for b_ in rr[i + 1:]:
            if road_axis(a_) == road_axis(b_) and overlap_area(rect_of(a_), rect_of(b_)) > 0:
                parallel.append((a_['id'], b_['id']))
    acute = []
    pl = [f for f in roads if f.get('points')]
    for i, a_ in enumerate(pl):
        for b_ in pl[i + 1:]:
            for p, q in zip(a_['points'], a_['points'][1:]):
                for s, t in zip(b_['points'], b_['points'][1:]):
                    if seg_intersect(p, q, s, t):
                        u = (q[0] - p[0], q[1] - p[1]); v = (t[0] - s[0], t[1] - s[1])
                        c = abs(u[0] * v[0] + u[1] * v[1]) / ((math.hypot(*u) * math.hypot(*v)) or 1)
                        if math.degrees(math.acos(min(1, c))) < 30:
                            acute.append((a_['id'], b_['id']))
    on_lots = int((road_m & lot_m).sum()); on_blds = int((road_m & bld_m).sum())
    put('road_conflicts', not parallel and not acute and on_lots == 0 and on_blds == 0,
        {'parallel_overlaps': len(parallel), 'acute_crossings': len(acute), 'road_on_lot_cells': on_lots, 'road_on_building_cells': on_blds,
         'examples': (parallel + acute)[:5]},
        '0 stacked parallel roads, 0 crossings under 30 deg, 0 road area on lots or buildings (perpendicular street junctions are allowed)')
    orphans = orphan_roads([dict(kind='roads', id=f['id'], bounds=rect_of(f)) for f in roads])
    put('streets_connected', not orphans, {'orphans': len(orphans), 'examples': orphans[:5]}, 'every town street/alley touches the connected network')
    coll = building_collisions(buildings)
    lot_ov = sum(1 for i, a_ in enumerate(lots) for b_ in lots[i + 1:] if overlap_area(rect_of(a_), rect_of(b_)) > 0)
    put('no_lot_or_building_overlaps', not coll and lot_ov == 0, {'building_pairs': len(coll), 'lot_pairs': lot_ov}, '0 overlapping buildings, 0 overlapping lots')

    m['counts'] = {'roads': len(roads), 'lots': len(lots), 'house_lots': len(house_lots), 'buildings': len(buildings), 'trees': len(trees), 'driveways': len(drives)}
    m['_draw'] = dict(outline=outline, roads=roads, lots=lots, buildings=buildings, trees=trees, drives=drives, biz=biz)
    return m


# ---------------------------------------------------------------- drawing
SPECIES_COLORS = {'sugar_maple': '#d95f02', 'silver_maple': '#e6ab02', 'norway_maple': '#a6761d', 'red_maple': '#e7298a',
                  'black_maple': '#7f3b08', 'green_ash': '#1b9e77', 'american_elm': '#d7191c', 'siberian_elm': '#7570b3'}
CLASS_COLORS = {'main_street': '#444444', 'town_street': '#9a9a9a', 'alley': '#c9b28a'}


def draw(m, path):
    d = m['_draw']
    fig, ax = plt.subplots(figsize=(10, 10), dpi=110)
    ax.set_aspect('equal'); ax.invert_yaxis()
    for f in d['roads']:
        col = CLASS_COLORS.get(road_class(f), '#9a9a9a')
        if f.get('points') and f.get('width'):
            p = np.asarray(f['points'], dtype=float); ax.plot(p[:, 0], p[:, 1], color=col, lw=2)
        else:
            x, y, w, h = rect_of(f); ax.add_patch(Rectangle((x, y), w, h, color=col, lw=0))
    for f in d['lots']:
        if f.get('polygon'):
            ax.add_patch(MplPolygon(ring(f['polygon']), fill=False, ec='#b0c4de', lw=.4))
        else:
            x, y, w, h = rect_of(f); ax.add_patch(Rectangle((x, y), w, h, fill=False, ec='#b0c4de', lw=.4))
    for f in d['drives']:
        x, y, w, h = rect_of(f); ax.add_patch(Rectangle((x, y), w, h, color='#dddddd', lw=0))
    bizids = {f['id'] for _, f in d['biz']}
    for f in d['buildings']:
        x, y, w, h = rect_of(f)
        ax.add_patch(Rectangle((x, y), w, h, color='#c0392b' if f['id'] in bizids else '#5d6d7e', lw=0))
    labelled = set()
    for t, f in d['biz']:
        if t and f['kind'] == 'buildings' and t not in labelled:
            x, y, w, h = rect_of(f); ax.text(x + w / 2, y + h / 2, t, fontsize=6, ha='center', va='center', color='black')
            labelled.add(t)
    if d['trees']:
        P = np.array([[rect_of(t)[0], rect_of(t)[1]] for t in d['trees']])
        cols = [SPECIES_COLORS.get(species_key(t.get('species')), '#2e7d32') for t in d['trees']]
        ax.scatter(P[:, 0], P[:, 1], s=4, c=cols, linewidths=0)
    ax.add_patch(MplPolygon(d['outline'], fill=False, ec='#1f77b4', lw=1.6))
    allp = list(d['outline']) + [p for f in d['lots'] + d['buildings'] + d['roads'] for p in rect_poly(rect_of(f))]
    xs = [p[0] for p in allp]; ys = [p[1] for p in allp]
    pad = 0.03 * max(max(xs) - min(xs), max(ys) - min(ys))
    ax.set_xlim(min(xs) - pad, max(xs) + pad); ax.set_ylim(max(ys) + pad, min(ys) - pad)
    fails = [k for k, v in m['checks'].items() if not v['pass']]
    ax.set_title(f"{m['label']}  {m['town_id']}  size={m['size']}\nfailing: {', '.join(fails) or 'none'}", fontsize=8)
    ax.set_xticks([]); ax.set_yticks([])
    fig.tight_layout(); fig.savefig(path); plt.close(fig)


# ---------------------------------------------------------------- pytest timing
FAST_DESELECT = ('not test_paved_gap_verifier_detects_deleted_edge and '
                 'not test_rivers_follow_seeded_regional_flow_and_merge_into_wider_trunks and '
                 'not test_finished_river_metrics_smoke_fails_on_comb_geometry')


def run_pytest():
    t = time.time()
    p = subprocess.run([sys.executable, '-m', 'pytest', '-q', '-k', FAST_DESELECT], cwd=ROOT, capture_output=True, text=True)
    tail = (p.stdout or '').strip().splitlines()[-1:] or ['']
    secs = time.time() - t
    return {'command': f'py -m pytest -q -k "{FAST_DESELECT}"', 'exit_code': p.returncode, 'summary': tail[0],
            'seconds': round(secs, 1), 'pass': p.returncode == 0, 'under_target': secs <= T['pytest_s']}


def git_branch():
    try:
        return subprocess.run(['git', 'rev-parse', '--abbrev-ref', 'HEAD'], cwd=ROOT, capture_output=True, text=True).stdout.strip() or 'unknown'
    except OSError:
        return 'unknown'


def git_head():
    try:
        return subprocess.run(['git', 'rev-parse', '--short', 'HEAD'], cwd=ROOT, capture_output=True, text=True).stdout.strip() or 'nogit'
    except OSError:
        return 'nogit'


# ---------------------------------------------------------------- main
def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--branch', default=None, help='results folder name (default: current git branch)')
    ap.add_argument('--seeds', type=int, nargs='*', default=None, help='subset of seeds (14 30 31)')
    ap.add_argument('--with-rivers', action='store_true', help='do not stub rivers/route offsets (slow)')
    ap.add_argument('--pytest', action='store_true', help='also run and time the fast pytest subset')
    args = ap.parse_args(argv)
    branch = (args.branch or git_branch()).replace('/', '_')
    out = ROOT / 'check-results' / 'town-bakeoff' / branch
    out.mkdir(parents=True, exist_ok=True)
    if not args.with_rivers:
        stub_rivers()
    settings = Settings()
    serialized = json.dumps(document(settings), sort_keys=True)
    rng = np.random.default_rng(20261114)
    t_all = time.time(); results = []; failures = 0
    for seed, ax, ay, label in TOWNS:
        if args.seeds and seed not in args.seeds:
            continue
        t0 = time.time()
        town, feats = town_features(seed, ax, ay, settings, serialized)
        if town is None:
            results.append({'seed': seed, 'label': label, 'checks': {'town_found': {'pass': False, 'value': None,
                            'target': f'a town within {MATCH_MI} mi of ({ax}, {ay}) mi'}}})
            print(f'FAIL {label}: no town near anchor', flush=True); failures += 1
            continue
        m = score_town(seed, label, town, feats, rng)
        secs = time.time() - t0
        m['checks']['runtime'] = {'pass': secs <= T['town_runtime_s'], 'value': round(secs, 1), 'target': f"<= {T['town_runtime_s']} s per town (first town of a seed includes the seed's warm-up)"}
        png = out / f'{label}.png'; draw(m, png); m['png'] = str(png.relative_to(ROOT))
        m.pop('_draw', None)
        bad = [k for k, v in m['checks'].items() if not v['pass']]
        failures += len(bad); results.append(m)
        print(f"{label}: {len(m['checks']) - len(bad)}/{len(m['checks'])} checks pass; failing: {', '.join(bad) or 'none'} ({secs:.1f}s)", flush=True)
    total = time.time() - t_all
    union = sorted({t for r in results for t in r.get('business_types', [])})
    missing_union = [t for t in REQUIRED_UNION if t not in union]
    summary = {'branch': branch, 'head': git_head(), 'with_rivers': args.with_rivers, 'targets': T,
               'towns': results, 'total_runtime_s': round(total, 1),
               'global_checks': {
                   'total_runtime': {'pass': total <= T['total_runtime_s'], 'value': round(total, 1), 'target': f"<= {T['total_runtime_s']} s"},
                   'required_business_types_union': {'pass': not missing_union, 'value': {'present': union, 'missing': missing_union},
                                                     'target': 'all item-4 categories appear in at least one of the five towns'}}}
    if args.pytest:
        pt = run_pytest()
        summary['global_checks']['pytest_fast_subset'] = {'pass': pt['pass'] and pt['under_target'], 'value': pt, 'target': f"passes in <= {T['pytest_s']} s"}
    failures += sum(not v['pass'] for v in summary['global_checks'].values())
    summary['failing_checks'] = failures
    (out / 'metrics.json').write_text(json.dumps(summary, indent=1, default=str), encoding='utf-8')
    names = []
    for r in results:
        for k in r['checks']:
            if k not in names: names.append(k)
    lines = [f"# Town bake-off: {branch} @ {summary['head']}", '',
             f"Failing checks: **{failures}**. Total runtime {total:.1f} s. Rivers {'ON' if args.with_rivers else 'stubbed'}.", '',
             '| check | ' + ' | '.join(r['label'] for r in results) + ' |', '|---|' + '---|' * len(results)]
    for k in names:
        row = []
        for r in results:
            c = r['checks'].get(k)
            if not c: row.append('-'); continue
            v = c['value']
            if isinstance(v, dict):
                v = ', '.join(f'{a}={b}' for a, b in v.items() if not isinstance(b, dict) and not (isinstance(b, list) and len(b) > 4))
            row.append(('PASS ' if c['pass'] else '**FAIL** ') + str(v)[:110])
        lines.append(f'| {k} | ' + ' | '.join(row) + ' |')
    lines += ['', '| global check | result |', '|---|---|']
    for k, c in summary['global_checks'].items():
        lines.append(f"| {k} | {'PASS' if c['pass'] else '**FAIL**'} {json.dumps(c['value'], default=str)[:160]} |")
    lines += ['', 'PNGs:'] + [f"- {r.get('png')}" for r in results if r.get('png')]
    (out / 'summary.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(f"\nfailing checks: {failures}; report: {out / 'summary.md'}", flush=True)
    return 0 if failures == 0 else 1


if __name__ == '__main__':
    raise SystemExit(main())
