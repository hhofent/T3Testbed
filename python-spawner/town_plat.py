"""Non-parity town plat upgrades: ROW, Main Street, alleys, faces, fill, extras."""
from spawner import FT, H, Draw
from settings import option


def street_width_ft(seed):
    """Residential ROW: 66 ft usual, 80 ft on ~25% of towns (Spencer / Cushing)."""
    d = Draw(H(seed, 'town_plat', 'sw'))
    return 80 if d.chance('wide', option('town.wide_street_pct')) else 66


def main_row_ft():
    return int(option('town.main_row_ft'))


def alley_w_ft():
    return int(option('town.alley_w_ft'))


def highway_local_axis(rail_town):
    """Local plat axis of the highway through town."""
    return 'h' if rail_town else 'v'


def apply_row_tags(town):
    sw = town['street_w']
    main_w = FT(main_row_ft())
    hwy = town.get('highway_local') or highway_local_axis(town['rail_town'])
    for s in town['streets']:
        if s.get('road_class') != 'main_street':
            s['road_class'] = 'town_street'
            s['row_ft'] = sw / FT(1)
    main_id = 'T-MAIN-h' if hwy == 'h' else 'T-MAIN-v'
    existing = next((s for s in town['streets'] if s['id'] == main_id), None)
    Xc, Yc = town['Xc'], town['Yc']
    box = town['box']
    if hwy == 'h':
        lo, hi = box[0], box[0] + box[2]
        c, axis = Yc, 'h'
    else:
        lo, hi = box[1], box[1] + box[3]
        c, axis = Xc, 'v'
    if existing is None:
        town['streets'].append({
            'id': main_id, 'name': 'Main Street', 'axis': axis, 'type': 'paved',
            'c': c, 'lo': lo, 'hi': hi, 'w': main_w, 'surface_ft': 40,
            'road_class': 'main_street', 'row_ft': main_row_ft(),
        })
    else:
        existing['w'] = main_w
        existing['lo'] = min(existing['lo'], lo)
        existing['hi'] = max(existing['hi'], hi)
        existing['road_class'] = 'main_street'
        existing['row_ft'] = main_row_ft()
        existing['name'] = 'Main Street'
        existing['c'] = c
    # Cross street must touch Main Street so connectivity.connect / orphan check pass.
    cross_id = 'T-MAIN-v' if hwy == 'h' else 'T-MAIN-h'
    cross = next((s for s in town['streets'] if s['id'] == cross_id), None)
    if hwy == 'h':
        clo, chi, cc, cax = box[1], box[1] + box[3], Xc, 'v'
    else:
        clo, chi, cc, cax = box[0], box[0] + box[2], Yc, 'h'
    # Extend cross so it reaches the main centreline.
    if hwy == 'h':
        clo, chi = min(clo, Yc), max(chi, Yc)
    else:
        clo, chi = min(clo, Xc), max(chi, Xc)
    if cross is None:
        town['streets'].append({
            'id': cross_id, 'name': 'Center Ave' if cax == 'v' else 'Center St',
            'axis': cax, 'type': 'paved', 'c': cc, 'lo': clo, 'hi': chi, 'w': sw,
            'surface_ft': 22, 'road_class': 'town_street', 'row_ft': sw / FT(1),
        })
    else:
        cross['lo'] = min(cross['lo'], clo)
        cross['hi'] = max(cross['hi'], chi)
        cross['road_class'] = 'town_street'
        cross['row_ft'] = sw / FT(1)
    town['main_id'] = main_id
    town['highway_local'] = hwy


def trim_fringe_stubs(town):
    """End streets at the outermost lot/block instead of poking into empty fringe."""
    if not town['blocks']:
        return
    bx0 = min(b['rect'][0] for b in town['blocks'])
    by0 = min(b['rect'][1] for b in town['blocks'])
    bx1 = max(b['rect'][0] + b['rect'][2] for b in town['blocks'])
    by1 = max(b['rect'][1] + b['rect'][3] for b in town['blocks'])
    pad = town['street_w']
    for s in town['streets']:
        if s.get('road_class') == 'main_street':
            continue
        if s['axis'] == 'v':
            s['lo'] = max(s['lo'], by0 - pad)
            s['hi'] = min(s['hi'], by1 + pad)
        else:
            s['lo'] = max(s['lo'], bx0 - pad)
            s['hi'] = min(s['hi'], bx1 + pad)
        if s['hi'] - s['lo'] < town['street_w']:
            s['_drop'] = True
    town['streets'] = [s for s in town['streets'] if not s.get('_drop')]


def extend_main_to_outline(town, outline):
    """Main Street covers the outline chord on the highway line."""
    hwy = town['highway_local']
    main = next(s for s in town['streets'] if s['id'] == town['main_id'])
    if hwy == 'h':
        xs = [p[0] for p in outline]
        main['lo'] = min(xs)
        main['hi'] = max(xs)
        main['c'] = town['Yc']
    else:
        ys = [p[1] for p in outline]
        main['lo'] = min(ys)
        main['hi'] = max(ys)
        main['c'] = town['Xc']


def curb_starts(town):
    """One point per Main Street approach at the first commercial block edge."""
    hwy = town['highway_local']
    Xc, Yc = town['Xc'], town['Yc']
    comm = [c for c in town.get('commercial', []) if c.get('lot') and not c.get('vacant')]
    if not comm:
        box = town['box']
        if hwy == 'h':
            return [[box[0], Yc], [box[0] + box[2], Yc]]
        return [[Xc, box[1]], [Xc, box[1] + box[3]]]
    pts = []
    if hwy == 'h':
        xs = sorted(c['lot'][0] for c in comm) + sorted(c['lot'][0] + c['lot'][2] for c in comm)
        pts = [[min(xs), Yc], [max(xs), Yc]]
    else:
        ys = sorted(c['lot'][1] for c in comm) + sorted(c['lot'][1] + c['lot'][3] for c in comm)
        pts = [[Xc, min(ys)], [Xc, max(ys)]]
    return pts


def make_ew_face(i, j, xs, ys, Xc, half, bld, al, hw_fn, seed):
    """Face lots toward the N-S avenue at Xc (E or W facing)."""
    # half: 'W' = west half of block east of Xc (faces west toward Xc), 'E' = east half west of Xc
    if half == 'W':
        # block is to the east of Xc: xs[i] == Xc or near
        if xs[i] != Xc and abs(xs[i] - Xc) > abs(xs[i + 1] - Xc):
            return None
        front_x = xs[i] + hw_fn(xs[i], Xc) if xs[i] == Xc else xs[i] + hw_fn(xs[i], Xc)
        # Actually when xs[i]==Xc, west face of eastern block fronts Xc from the east → facing W
        if xs[i] != Xc:
            return None
        bx0 = xs[i] + hw_fn(xs[i], Xc)
        bx1 = xs[i + 1] - hw_fn(xs[i + 1], Xc)
        by0 = ys[j] + hw_fn(ys[j], ys[j])  # will be replaced by caller
        return None  # built in make_town hook instead
    return None


def add_avenue_and_highway_faces(seed, town, faces, xs, ys, blocks, hw, al, bld_for):
    """Convert selected blocks to E/W faces (replacing N/S) for highway frontage / avenues.

    Stacking E/W on top of N/S caused lot overlaps and road-on-lot failures; conversion
    keeps one orientation per block.
    """
    Xc, Yc = town['Xc'], town['Yc']
    hwy = town['highway_local']
    d = Draw(H(seed, 'town_plat', 'faces'))
    kept = {b['ij'] for b in blocks}
    added = []
    avenue_pct = option('town.avenue_face_pct')
    convert = set()
    for i, j in sorted(kept):
        on_left = xs[i] == Xc
        on_right = xs[i + 1] == Xc
        on_hwy_v = hwy == 'v' and (on_left or on_right)
        # Only convert blocks that actually front the highway Main Street.
        # Avenue conversion deferred — vertical alleys were orphaning and parallel-overlapping.
        if on_hwy_v:
            convert.add((i, j))
    if not convert:
        return added
    # Drop N/S faces for converted blocks.
    faces[:] = [F for F in faces if not any(F['run'].startswith(f'town:b{i}{j}:') and F['run'].endswith(s)
                                            for i, j in convert for s in (':S', ':N'))]
    # Also drop runs like town:b00:S exactly.
    def _is_ns_of_converted(F):
        run = F['run']
        for i, j in convert:
            if run in (f'town:b{i}{j}:S', f'town:b{i}{j}:N'):
                return True
        return False
    faces[:] = [F for F in faces if not _is_ns_of_converted(F)]
    for i, j in sorted(convert):
        bx0 = xs[i] + hw(xs[i], Xc)
        bx1 = xs[i + 1] - hw(xs[i + 1], Xc)
        by0 = ys[j] + hw(ys[j], Yc)
        by1 = ys[j + 1] - hw(ys[j + 1], Yc)
        depth_x = (bx1 - bx0 - al) // 2
        if depth_x < FT(60):
            continue
        on_left = xs[i] == Xc
        on_right = xs[i + 1] == Xc
        on_hwy_v = hwy == 'v' and (on_left or on_right)
        # Two E/W halves like the N/S split, with a N-S alley between.
        halves = []
        if on_left or not on_hwy_v:
            halves.append(('W', bx0, on_hwy_v and on_left))
        if on_right or not on_hwy_v:
            halves.append(('E', bx1, on_hwy_v and on_right))
        # Always fill both halves of a converted block to avoid empty land.
        halves = [('W', bx0, on_hwy_v and on_left), ('E', bx1, on_hwy_v and on_right)]
        for fa, front_x, is_main in halves:
            F = {
                'run': f'town:b{i}{j}:{fa}',
                'y0': by0,
                'fa': fa,
                'bx0': by0, 'bx1': by1,
                'd': depth_x,
                'front_y': front_x,
                'front_x': front_x,
                'main': bool(is_main),
                'dist': min(abs(ys[j] - Yc), abs(ys[j + 1] - Yc)),
                'from_lo': True,
                'ew': True,
            }
            added.append(F)
            faces.append(F)
    return added

def refill_ragged_blocks(seed, cols, rows, xs, ys, Xc, Yc, kept, size, cy, cx, hw, al, creeks, rail_hits_fn):
    """Re-add some interior blocks punched out by ragged/ribbon rules."""
    d = Draw(H(seed, 'town_plat', 'refill'))
    added = []
    for i in range(cols):
        for j in range(rows):
            if (i, j) in kept:
                continue
            # Only refill interior or near-main cells.
            outer = i in (0, cols - 1) or j in (0, rows - 1)
            on_main = Yc in (ys[j], ys[j + 1]) or Xc in (xs[i], xs[i + 1])
            if outer and not on_main:
                continue
            if not d.chance(f'rf{i}{j}', 55 if on_main else 35):
                continue
            bx0 = xs[i] + hw(xs[i], Xc)
            bx1 = xs[i + 1] - hw(xs[i + 1], Xc)
            by0 = ys[j] + hw(ys[j], Yc)
            by1 = ys[j + 1] - hw(ys[j + 1], Yc)
            if rail_hits_fn(list(creeks), (bx0, by0, bx1 - bx0, by1 - by0)):
                continue
            bld = (by1 - by0 - al) // 2
            if bld < FT(80):
                continue
            added.append({'id': f'b{i}{j}', 'ij': (i, j), 'rect': (bx0, by0, bx1 - bx0, by1 - by0), 'lot_d': bld})
            kept.add((i, j))
    return added


def add_outward_lots(seed, town, xs, ys, faces):
    """Outward-facing lots on outer streets: no alley, 1.2-2x depth, 20-40% gaps (overhaul C2/C1)."""
    d = Draw(H(seed, 'town_plat', 'outward'))
    Xc, Yc = town['Xc'], town['Yc']
    cols, rows = town['cols'], town['rows']
    kept = {b['ij'] for b in town['blocks']}
    sw = town['street_w']
    new_faces = []
    gap_pct = option('town.outward_gap_pct')
    for i, j in sorted(kept):
        # South outer
        if j == 0 or (i, j - 1) not in kept:
            if ys[j] == Yc:
                continue
            if d.chance(f'outS{i}{j}', 100 - gap_pct):
                depth = FT(int(140 * (12 + d.int(f'dS{i}{j}', 9)) / 10))  # 1.2-2.0 x ~140
                depth = min(depth, FT(280))
                bx0 = xs[i] + sw // 2
                bx1 = xs[i + 1] - sw // 2
                # facing north toward town? Outer south edge faces south onto outer street.
                front = ys[j]
                y0 = front - sw // 2 - depth
                F = {'run': f'town:out{i}{j}:S', 'y0': y0, 'fa': 'S', 'bx0': bx0, 'bx1': bx1,
                     'd': depth, 'front_y': y0 + depth, 'main': False,
                     'dist': abs(xs[i] - Xc), 'from_lo': xs[i] < Xc, 'outward': True}
                # Fix: S facing means street at south of lot, front_y = south edge = y0 for S...
                # Existing convention: S half has y0=by0 (north of street at ys[j]), front_y=by0, fa=S
                # Street at ys[j], lot north of street facing south: y0 = ys[j]+sw/2, fa=S, front_y=y0
                F = {'run': f'town:out{i}{j}:S', 'y0': front + sw // 2, 'fa': 'N', 'bx0': bx0, 'bx1': bx1,
                     'd': depth, 'front_y': front + sw // 2, 'main': False,
                     'dist': abs(xs[i] - Xc), 'from_lo': xs[i] < Xc, 'outward': True}
                # Wait - if we're on the south outer edge, the outer street is ys[j], and
                # outward lots are SOUTH of that street, facing north toward town.
                F = {'run': f'town:out{i}{j}:S', 'y0': front - sw // 2 - depth, 'fa': 'N',
                     'bx0': bx0, 'bx1': bx1, 'd': depth, 'front_y': front - sw // 2,
                     'main': False, 'dist': abs(xs[i] - Xc), 'from_lo': xs[i] < Xc, 'outward': True}
                new_faces.append(F)
        # North outer
        if j == rows - 1 or (i, j + 1) not in kept:
            if ys[j + 1] == Yc:
                continue
            if d.chance(f'outN{i}{j}', 100 - gap_pct):
                depth = FT(int(140 * (12 + d.int(f'dN{i}{j}', 9)) / 10))
                depth = min(depth, FT(280))
                bx0 = xs[i] + sw // 2
                bx1 = xs[i + 1] - sw // 2
                front = ys[j + 1]
                F = {'run': f'town:out{i}{j}:N', 'y0': front + sw // 2, 'fa': 'S',
                     'bx0': bx0, 'bx1': bx1, 'd': depth, 'front_y': front + sw // 2,
                     'main': False, 'dist': abs(xs[i] - Xc), 'from_lo': xs[i] < Xc, 'outward': True}
                new_faces.append(F)
    faces.extend(new_faces)
    return new_faces


def place_park_and_tower(seed, town, occupied):
    """Extras: small park/ball field and a water tower (simplified / unsourced)."""
    d = Draw(H(seed, 'town_plat', 'civic'))
    extras = []
    box = town['box']
    # Water tower near an outer corner.
    tw = FT(40)
    corner = d.int('twc', 4)
    ox = box[0] + FT(20) if corner % 2 == 0 else box[0] + box[2] - FT(20) - tw
    oy = box[1] + FT(20) if corner < 2 else box[1] + box[3] - FT(20) - tw
    tower = (ox, oy, tw, tw)
    from region import overlaps
    if not any(overlaps(tower, o) for o in occupied):
        extras.append({'kind': 'water_tower', 'rect': tower, 'use': 'water tower'})
        occupied.append(tower)
    # Park: vacant half-block near edge if room.
    pw, ph = FT(200), FT(250)
    for k in range(6):
        px = box[0] + FT(40) + d.int(f'px{k}', max(1, int((box[2] - pw) / FT(1)))) * FT(1)
        py = box[1] + FT(40) + d.int(f'py{k}', max(1, int((box[3] - ph) / FT(1)))) * FT(1)
        park = (px, py, pw, ph)
        if any(overlaps(park, o) for o in occupied):
            continue
        extras.append({'kind': 'park', 'rect': park, 'use': 'park', 'name': 'ball field' if d.chance('ball', 50) else 'city park'})
        occupied.append(park)
        break
    town['extras'] = extras
    return extras


def approach_houses(seed, town, roads):
    """Short string of houses along the highway approach just outside town (overhaul C2)."""
    d = Draw(H(seed, 'town_plat', 'approach'))
    # Keep approaches on the non-highway shoulders only; skip if they would hit Main Street ROW.
    if not d.chance('do', 55):
        return []
    hwy = town['highway_local']
    box = town['box']
    Xc, Yc = town['Xc'], town['Yc']
    lots = []
    n = 2 + d.int('n', 3)
    depth = FT(160)
    width = FT(80)
    gap = FT(20)
    if hwy == 'h':
        # West approach along Yc
        x = box[0] - gap - width
        for k in range(n):
            side = 1 if d.chance(f's{k}', 50) else -1
            y0 = Yc + FT(50) if side > 0 else Yc - FT(50) - depth
            lots.append({
                'id': f'approach:w:{k}', 'w': width, 'd': depth, 'setting': 'town', 'kind': 'town',
                'place': {'x0': x - k * (width + gap), 'y0': y0, 'facing': 'S' if side > 0 else 'N'},
                'along': x - k * (width + gap), 'base_lots': 1, 'house_type': 'stick', 'use': 'house',
                'approach': True,
            })
    else:
        y = box[1] - gap - width
        for k in range(n):
            side = 1 if d.chance(f's{k}', 50) else -1
            x0 = Xc + FT(50) if side > 0 else Xc - FT(50) - depth
            lots.append({
                'id': f'approach:s:{k}', 'w': width, 'd': depth, 'setting': 'town', 'kind': 'town',
                'place': {'x0': x0, 'y0': y - k * (width + gap), 'facing': 'W' if side > 0 else 'E'},
                'along': y - k * (width + gap), 'base_lots': 1, 'house_type': 'stick', 'use': 'house',
                'approach': True,
            })
    return lots
