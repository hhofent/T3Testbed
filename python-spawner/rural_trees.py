"""Rural yard trees and farmstead windbreaks. Leaves trees.local_markers untouched.

Yard species follow Wray & Mize 1985 Iowa street-tree counts and Gleason 1992
elm decline (research iowa-1990-roads-lots-trees.md §5), simplified for the map.
Windbreak layout follows ISU Pm-1716 (1997): N and W sides, shrub row plus two
conifer rows minimum, hardwoods mixed in. Conifer species names are
simplified/unsourced: the 1997 Pm-1717 species list is a research gap (§5, Gap 10).
"""
import math
from spawner import FT, H, Draw
from settings import option

# Wray & Mize 1985 counts + Gleason 1992 elm decline; weights simplified for the game.
YARD_SPECIES = (
    ('sugar_maple', 26),
    ('green_ash', 22),
    ('silver_maple', 18),
    ('norway_maple', 12),
    ('hackberry', 5),
    ('honeylocust', 4),
    ('linden', 3),
    ('bur_oak', 3),
    ('siberian_elm', 3),
    ('american_elm', 1),
)

# Simplified/unsourced conifer names (Pm-1717 Gap 10). Hardwoods from Pm-1716 mix.
CONIFER_SPECIES = (('eastern_redcedar', 40), ('white_spruce', 35), ('scotch_pine', 25))
HARDWOOD_SPECIES = (('black_walnut', 30), ('bur_oak', 25), ('green_ash', 25), ('sugar_maple', 20))
SHRUB_SPECIES = (('common_lilac', 40), ('chokecherry', 35), ('american_plum', 25))

ACRE = FT(1) * FT(1) * 43560


def _pick(d, key, table):
    names, weights = zip(*table)
    return d.weighted(key, list(zip(names, weights)))


def _condition(d, species):
    if species in ('american_elm', 'siberian_elm'):
        return 'declining' if d.chance('decl', 50) else 'good'
    return 'good' if d.chance('good', 85) else 'declining'


def _blocked(x, y, blockers, pad):
    for a, b, w, h in blockers:
        if a - pad <= x <= a + w + pad and b - pad <= y <= b + h + pad:
            return True
    return False


def _in_poly(x, y, poly):
    n = len(poly)
    inside = False
    j = n - 1
    for i in range(n):
        xi, yi = poly[i]
        xj, yj = poly[j]
        if ((yi > y) != (yj > y)) and (x < (xj - xi) * (y - yi) / ((yj - yi) or 1e-12) + xi):
            inside = not inside
        j = i
    return inside


def _near_segment(x, y, a, b, pad):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    length2 = dx * dx + dy * dy
    if length2 <= 0:
        return math.hypot(x - ax, y - ay) <= pad
    t = max(0, min(1, ((x - ax) * dx + (y - ay) * dy) / length2))
    px, py = ax + t * dx, ay + t * dy
    return math.hypot(x - px, y - py) <= pad


def world_nw_local_sides(place):
    """Map world north/west to lot-local side names.

    World y increases southward, so north is smaller world y and west is smaller x.
    region.grove_sides uses the opposite y convention; do not reuse it here.
    """
    facing = place['facing']
    if facing == 'S':
        return 'front', 'left'
    if facing == 'N':
        return 'rear', 'right'
    if facing == 'W':
        return 'right', 'front'
    return 'left', 'rear'


def _side_strip(side, W, D, depth, inset):
    """Axis-aligned planting band for one lot-local side, inset from the outer edge."""
    depth = min(depth, min(W, D) // 2)
    if side == 'front':
        return inset, inset, W - inset, inset + depth
    if side == 'rear':
        return inset, D - inset - depth, W - inset, D - inset
    if side == 'left':
        return inset, inset, inset + depth, D - inset
    return W - inset - depth, inset, W - inset, D - inset


def _clump_yard(d, cx, cy, radius, n, min_sep, blockers, pad, shape, W, D):
    """Clumpy random points near the house; never a grid."""
    if n <= 0:
        return []
    n_clumps = 1 if n <= 2 else (2 if n <= 5 else 3)
    if n >= 4 and d.chance('3c', 55):
        n_clumps = min(3, max(2, n_clumps))
    sizes = []
    left = n
    for i in range(n_clumps):
        take = left if i == n_clumps - 1 else max(1, min(left - (n_clumps - i - 1), 1 + d.int(f'cs{i}', min(3, left))))
        sizes.append(take)
        left -= take
    pts = []
    edge = FT(4)
    for ci, sz in enumerate(sizes):
        ang = d.int(f'ang{ci}', 360) * math.pi / 180
        dist = d.int(f'dst{ci}', max(1, int(radius / FT(1) * 7 // 10))) * FT(1)
        ox = int(round(math.cos(ang) * dist))
        oy = int(round(math.sin(ang) * dist))
        # Break residual lattice alignment.
        ox += (d.int(f'jx{ci}', 13) - 6) * FT(1)
        oy += (d.int(f'jy{ci}', 13) - 6) * FT(1)
        ccx, ccy = cx + ox, cy + oy
        for k in range(sz):
            placed = False
            for attempt in range(40):
                jx = (d.int(f'ox{ci}{k}{attempt}', 21) - 10) * FT(1)
                jy = (d.int(f'oy{ci}{k}{attempt}', 21) - 10) * FT(1)
                x, y = ccx + jx, ccy + jy
                if not (edge <= x <= W - edge and edge <= y <= D - edge):
                    continue
                if math.hypot(x - cx, y - cy) > radius:
                    continue
                if shape and not _in_poly(x, y, shape):
                    continue
                if _blocked(x, y, blockers, pad):
                    continue
                if any((x - px) * (x - px) + (y - py) * (y - py) < min_sep * min_sep for px, py in pts):
                    continue
                if (x % FT(30) == 0) and (y % FT(30) == 0):
                    continue
                pts.append((x, y))
                placed = True
                break
            if not placed:
                # Fallback: walk a ring around the house until a free cell appears.
                for attempt in range(60):
                    ang2 = (d.int(f'fa{ci}{k}{attempt}', 360)) * math.pi / 180
                    rad2 = FT(20) + d.int(f'fr{ci}{k}{attempt}', max(1, int(radius / FT(1)))) * FT(1)
                    x = cx + int(round(math.cos(ang2) * rad2))
                    y = cy + int(round(math.sin(ang2) * rad2))
                    if not (edge <= x <= W - edge and edge <= y <= D - edge):
                        continue
                    if shape and not _in_poly(x, y, shape):
                        continue
                    if _blocked(x, y, blockers, pad):
                        continue
                    if any((x - px) ** 2 + (y - py) ** 2 < min_sep ** 2 for px, py in pts):
                        continue
                    pts.append((x, y))
                    break
    return pts


def yard_trees(seed, spec, plan, site, place=None):
    """Clumpy yard trees around the house for one rural lot.

    Returns list of dicts with lot-local x,y plus species/setting/condition/label.
    Farmsteads get at least rural.yard_tree_min within about rural.yard_radius_ft.
    """
    W, D = spec['width'], spec['depth']
    salt = spec['id']
    pad = FT(option('world.tree_radius_ft'))
    rooms = [r.rect() for r in plan.rooms]
    x0 = min(r[0] for r in rooms)
    y0 = min(r[1] for r in rooms)
    hw = max(r[0] + r[2] for r in rooms) - x0
    hd = max(r[1] + r[3] for r in rooms) - y0
    dx, dy = site['offset']
    hx, hy = x0 + dx, y0 + dy
    cx, cy = hx + hw // 2, hy + hd // 2
    blockers = [(hx, hy, hw, hd)] + [r for _, r in site.get('buildings', [])] + list(site.get('driveways', []))
    shape = site.get('shape')
    d = Draw(H(seed, 'rural_yard', salt))
    radius = FT(option('rural.yard_radius_ft'))
    min_n = option('rural.yard_tree_min') if spec['kind'] == 'farmstead' else 2
    max_n = 10 if spec['kind'] == 'farmstead' else 5
    n = min_n + d.int('n', max(1, max_n - min_n + 1))
    min_sep = FT(12)
    pts = _clump_yard(d, cx, cy, radius, n, min_sep, blockers, pad, shape, W, D)
    # Guarantee the farmstead minimum near the house.
    guard = 0
    while len(pts) < min_n and guard < 80:
        ang = (H(seed, 'rural_yard', salt, 'g', guard) % 360) * math.pi / 180
        rad = FT(25) + (H(seed, 'rural_yard', salt, 'gr', guard) % max(1, int(radius / FT(1) - 25))) * FT(1)
        x = cx + int(round(math.cos(ang) * rad))
        y = cy + int(round(math.sin(ang) * rad))
        guard += 1
        if not (FT(4) <= x <= W - FT(4) and FT(4) <= y <= D - FT(4)):
            continue
        if shape and not _in_poly(x, y, shape):
            continue
        if _blocked(x, y, blockers, pad):
            continue
        if any((x - px) ** 2 + (y - py) ** 2 < min_sep ** 2 for px, py in pts):
            continue
        pts.append((x, y))
    out = []
    for i, (x, y) in enumerate(pts):
        sp = _pick(Draw(H(seed, 'rural_yard', salt, 'sp', i)), 'sp', YARD_SPECIES)
        out.append({
            'x': x, 'y': y, 'species': sp, 'setting': 'yard',
            'condition': _condition(Draw(H(seed, 'rural_yard', salt, 'cd', i)), sp),
            'label': 'simplified',
        })
    return out


def _row_points(side, W, D, row_index, row_spacing, along_spacing, start, end, inset_outer):
    """Points along one windbreak row parallel to a lot-local side."""
    depth = inset_outer + row_index * row_spacing + row_spacing // 2
    pts = []
    if side in ('front', 'rear'):
        y = depth if side == 'front' else D - depth
        x = start
        while x <= end:
            pts.append((x, y))
            x += along_spacing
    else:
        x = depth if side == 'left' else W - depth
        y = start
        while y <= end:
            pts.append((x, y))
            y += along_spacing
    return pts


def _drive_gaps(drives, pad):
    return [(a, b, w, h) for a, b, w, h in drives]


def windbreak_trees(seed, spec, plan, site, place):
    """N/W farmstead windbreaks after ISU Pm-1716 (1997).

    Real rows (>=3 per side), shrub + conifer rows, hardwoods mixed in, gaps at
    drives, inside the lot shape, adjacent to the core and extended ~100 ft past
    the last main building toward south/east in world terms.
    """
    if spec['kind'] != 'farmstead' or place is None:
        return []
    W, D = spec['width'], spec['depth']
    salt = spec['id']
    pad = FT(option('world.tree_radius_ft'))
    rooms = [r.rect() for r in plan.rooms]
    x0 = min(r[0] for r in rooms)
    y0 = min(r[1] for r in rooms)
    hw = max(r[0] + r[2] for r in rooms) - x0
    hd = max(r[1] + r[3] for r in rooms) - y0
    dx, dy = site['offset']
    hx, hy = x0 + dx, y0 + dy
    buildings = [(hx, hy, hw, hd)] + [r for _, r in site.get('buildings', [])]
    drives = list(site.get('driveways', []))
    blockers = buildings + drives
    shape = site.get('shape')
    d = Draw(H(seed, 'rural_windbreak', salt))
    north, west = world_nw_local_sides(place)
    n_rows = 3 + d.int('rows', 3)  # 3..5
    row_spacing = FT(option('rural.windbreak_row_ft'))
    along = FT(option('rural.windbreak_spacing_ft'))
    inset = FT(6)
    extend = FT(option('rural.windbreak_extend_ft'))
    # Building cluster bounds in lot-local.
    minx = min(b[0] for b in buildings)
    miny = min(b[1] for b in buildings)
    maxx = max(b[0] + b[2] for b in buildings)
    maxy = max(b[1] + b[3] for b in buildings)
    # Target planted area 1-5 acres from the belt itself.
    target_ac = 1 + d.int('ac', 401) / 100  # 1.00 .. 5.00
    belt_depth = inset + n_rows * row_spacing
    # Length budget split across N and W legs (L shape, corner counted once).
    target_area = target_ac * ACRE
    # Approximate length so (len_n + len_w) * depth - depth^2 ~= target.
    length_budget = max(FT(120), int((target_area + belt_depth * belt_depth) / max(belt_depth, 1)))

    def span_for(side):
        # Cover the cluster and extend ~100 ft past it toward S/E in world, mapped to local.
        if side == 'front':
            a = max(inset, minx - extend)
            b = min(W - inset, maxx + extend)
        elif side == 'rear':
            a = max(inset, minx - extend)
            b = min(W - inset, maxx + extend)
        elif side == 'left':
            a = max(inset, miny - extend)
            b = min(D - inset, maxy + extend)
        else:
            a = max(inset, miny - extend)
            b = min(D - inset, maxy + extend)
        # Grow the short span toward the interior of the lot to hit coverage.
        need = length_budget // 2
        if b - a < need:
            extra = need - (b - a)
            a = max(inset, a - extra // 2)
            b = min((W if side in ('front', 'rear') else D) - inset, b + extra - extra // 2)
        return a, b

    out = []
    for side in (north, west):
        start, end = span_for(side)
        if end - start < along:
            continue
        for ri in range(n_rows):
            # Outer shrub row, then conifers; mix hardwoods on the innermost row.
            if ri == 0:
                table = SHRUB_SPECIES
                kind = 'shrub'
            elif ri >= n_rows - 1:
                table = HARDWOOD_SPECIES
                kind = 'hardwood'
            else:
                table = CONIFER_SPECIES
                kind = 'conifer'
            # Keep conifer share high: middle rows always conifer; sometimes swap hardwood row plants.
            for pi, (x, y) in enumerate(_row_points(side, W, D, ri, row_spacing, along, start, end, inset)):
                if shape and not _in_poly(x, y, shape):
                    continue
                if _blocked(x, y, blockers, pad + FT(2)):
                    continue
                # Gap at drives: also skip near drive centerlines more generously.
                if any(_blocked(x, y, [dr], FT(option('rural.lane_width_ft')) // 2 + pad) for dr in drives):
                    continue
                dd = Draw(H(seed, 'rural_windbreak', salt, side, ri, pi))
                if kind == 'hardwood' and dd.chance('to_conifer', 45):
                    sp = _pick(dd, 'sp', CONIFER_SPECIES)
                elif kind == 'conifer' and dd.chance('to_hard', 12):
                    sp = _pick(dd, 'sp', HARDWOOD_SPECIES)
                else:
                    sp = _pick(dd, 'sp', table)
                # Soft clearance: drop plants closer than ~75 ft to a building on the inner row.
                if ri == n_rows - 1:
                    too_close = False
                    for a, b, w, h in buildings:
                        bx, by = a + w / 2, b + h / 2
                        if math.hypot(x - bx, y - by) < FT(50):
                            too_close = True
                            break
                    if too_close:
                        continue
                out.append({
                    'x': int(x), 'y': int(y), 'species': sp, 'setting': 'windbreak',
                    'condition': 'good' if dd.chance('good', 90) else 'declining',
                    'label': 'simplified',
                })
    return out


def shift_buildings_from_belts(seed, spec, place, buildings, house_rect):
    """Push outbuildings out of the N/W belt zone (parity-gated caller)."""
    if spec['kind'] != 'farmstead' or not place:
        return buildings
    W, D = spec['width'], spec['depth']
    north, west = world_nw_local_sides(place)
    n_rows = 3
    row_spacing = FT(option('rural.windbreak_row_ft'))
    inset = FT(6)
    belt = inset + n_rows * row_spacing + FT(option('rural.windbreak_clear_ft'))
    hx, hy, hw, hd = house_rect
    out = []
    for typ, (x, y, w, h) in buildings:
        nx, ny = x, y

        def in_belt(px, py, pw, ph, side):
            if side == 'front':
                return py < belt
            if side == 'rear':
                return py + ph > D - belt
            if side == 'left':
                return px < belt
            return px + pw > W - belt

        for _ in range(8):
            moved = False
            if in_belt(nx, ny, w, h, north):
                if north == 'front':
                    ny = max(ny, belt + FT(5))
                elif north == 'rear':
                    ny = min(ny, D - belt - h - FT(5))
                elif north == 'left':
                    nx = max(nx, belt + FT(5))
                else:
                    nx = min(nx, W - belt - w - FT(5))
                moved = True
            if in_belt(nx, ny, w, h, west):
                if west == 'front':
                    ny = max(ny, belt + FT(5))
                elif west == 'rear':
                    ny = min(ny, D - belt - h - FT(5))
                elif west == 'left':
                    nx = max(nx, belt + FT(5))
                else:
                    nx = min(nx, W - belt - w - FT(5))
                moved = True
            if not moved:
                break
        # Keep inside lot and away from the house footprint.
        nx = max(FT(20), min(nx, W - w - FT(20)))
        ny = max(FT(20), min(ny, D - h - FT(20)))
        if not (nx + w <= hx or hx + hw <= nx or ny + h <= hy or hy + hd <= ny):
            # Overlaps house: shove south/east in lot space (larger y, or away from west belt).
            ny = max(ny, hy + hd + FT(20))
            if ny + h > D - FT(20):
                nx = max(nx, hx + hw + FT(20))
                ny = max(FT(20), min(y, D - h - FT(20)))
        out.append((typ, (nx, ny, w, h)))
    return out


def rural_markers(seed, spec, plan, site, place):
    """All rural trees for one lot: yard plus farmstead windbreaks."""
    trees = yard_trees(seed, spec, plan, site, place)
    if spec['kind'] == 'farmstead':
        trees.extend(windbreak_trees(seed, spec, plan, site, place))
    return trees
