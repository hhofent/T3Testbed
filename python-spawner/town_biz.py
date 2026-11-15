"""Non-parity business top-up for 1990 NW Iowa small towns.

Does not touch choose_businesses or region.biz_p (parity / band chooser).
Types and rates added here are simplified for the game / unsourced.
Storefront depth ~120-140 ft and 25-ft fronts: Spencer lots, Naumann 2004
(research/iowa-1990-roads-lots-trees.md); 100-ft Main Street same file.
"""
from spawner import FT, H, Draw
from settings import option

# Required across the five bake-off towns (canonical names for the check).
REQUIRED = [
    'gas station', 'cafe', 'bank', 'post office', 'church', 'school',
    'implement dealer', 'tavern', 'hardware', 'lumberyard',
]

# Extra filler types (simplified / unsourced) to hit mid>=12 / big>=15.
FILLER = [
    'grocery', 'auto repair', 'beauty shop', 'insurance', 'vet', 'drug store',
    'locker plant', 'funeral home', 'farm supply', 'city hall / fire',
    'variety store', 'newspaper', 'Legion hall', 'feed mill',
]

SINGLETONS = {'post office', 'bank', 'school', 'city hall / fire'}

# Edge parcel footprints for types not already in region.edge_fp (simplified).
EXTRA_EDGE_FP = {
    'tavern': (80, 140, 40, 50, 20),
    'hardware': (100, 140, 50, 60, 20),
    'cafe': (60, 140, 25, 60, 0),
    'bank': (80, 140, 50, 60, 20),
    'post office': (80, 140, 50, 50, 20),
    'gas station': (120, 140, 40, 40, 40),
    'church': (150, 160, 60, 80, 30),
    'variety store': (80, 140, 40, 50, 0),
    'newspaper': (60, 140, 30, 50, 0),
    'Legion hall': (100, 140, 60, 50, 20),
    'feed mill': (150, 200, 80, 60, 40),
}


def _have_types(town):
    have = set()
    for c in town.get('commercial', []):
        t = c.get('type') or ''
        if t.startswith('vacant'):
            continue
        have.add(t)
        if t == 'bank (branch)':
            have.add('bank')
        if t == 'hardware':
            have.add('hardware')
        if t == 'lumberyard':
            have.add('lumberyard')
    return have


def topup_program(seed, town):
    """Ensure program lists enough types for the band; return types still needed."""
    band = town['band']
    need_n = (6, 12, 15, 17)[min(band, 3)]
    have = _have_types(town)
    prog = list(town.get('program') or [])
    d = Draw(H(seed, 'town_biz_topup'))
    # Add missing required first, then filler until need_n.
    want = []
    for t in REQUIRED:
        if t == 'hardware' and ('hardware' in have or 'lumberyard' in have):
            continue
        if t == 'lumberyard' and ('hardware' in have or 'lumberyard' in have):
            continue
        if t not in have and t not in prog and t not in want:
            # Singletons: skip if already present under alias.
            if t in SINGLETONS and t in have:
                continue
            want.append(t)
    for t in FILLER:
        if len(have) + len(want) >= need_n:
            break
        if t in have or t in prog or t in want:
            continue
        if t in SINGLETONS and any(x in have or x in prog or x in want for x in SINGLETONS if x == t):
            continue
        if d.chance(f'fill_{t}', 70):
            want.append(t)
    # Force-fill if still short.
    for t in FILLER:
        if len(have) + len([x for x in prog + want if x not in have]) >= need_n:
            break
        if t not in have and t not in prog and t not in want:
            if t in SINGLETONS and t in (have | set(prog) | set(want)):
                continue
            want.append(t)
    town['program'] = prog + [t for t in want if t not in prog]
    town['edge_want'] = list(town.get('edge_want') or [])
    from region import EDGE
    for t in want:
        if t in EDGE or t in EXTRA_EDGE_FP:
            if t not in town['edge_want'] and t not in ('cafe', 'bank', 'post office', 'tavern', 'hardware'):
                # Prefer downtown for storefronts; edge for large parcels.
                if t in ('implement dealer', 'lumberyard', 'school', 'auto repair', 'vet',
                         'nursing home', 'farm supply', 'feed mill', 'gas station'):
                    town['edge_want'].append(t)
    return want


def place_topup(seed, town, put_fn, mains, off, res, faces):
    """Place top-up types using the same put() helper as make_town.

    put_fn(typ, faces_, w) -> bool
    """
    d = Draw(H(seed, 'town_biz_place'))
    have = _have_types(town)
    band = town['band']
    need_n = (6, 12, 15, 17)[min(band, 3)]
    storefront_w = FT(25)
    for typ in list(town['program']):
        if typ in have:
            continue
        if typ in SINGLETONS and typ in have:
            continue
        placed = False
        if typ == 'gas station':
            placed = put_fn(typ, mains[:1] + mains, FT(100))
        elif typ == 'bank':
            placed = put_fn(typ, mains[1:2] + mains, FT(50))
        elif typ in ('post office',):
            placed = put_fn(typ, mains + off, FT(50))
        elif typ in ('city hall / fire',):
            placed = put_fn(typ, mains + off, FT(75))
        elif typ == 'church':
            # handled separately usually; try residential
            for F in sorted(res, key=lambda F: H(seed, 'church_top', F['run'])):
                if F.get('church'):
                    continue
                # defer to caller via put with width
                placed = put_fn('church', [F], 3 * FT(50))
                if placed:
                    F['church'] = True
                    break
        elif typ in ('cafe', 'tavern', 'hardware', 'insurance', 'beauty shop', 'drug store',
                     'locker plant', 'grocery', 'variety store', 'newspaper'):
            placed = put_fn(typ, mains[1:] + mains[:1] + off, storefront_w)
        elif typ == 'Legion hall':
            placed = put_fn(typ, off + mains, FT(75))
        elif typ in ('implement dealer', 'lumberyard', 'school', 'auto repair', 'vet',
                     'nursing home', 'farm supply', 'feed mill'):
            # left for edge placement
            if typ not in town['edge_want']:
                town['edge_want'].append(typ)
            placed = True  # not unplaced yet
        elif typ == 'funeral home':
            continue  # existing converted-house path
        else:
            placed = put_fn(typ, mains + off, storefront_w)
        if placed:
            have.add(typ)
        have = _have_types(town)
        if len(have) >= need_n and all(
            (t in have) or (t == 'hardware' and 'lumberyard' in have) or
            (t == 'lumberyard' and 'hardware' in have) for t in REQUIRED if t != 'church' or True
        ):
            # keep going until band target; church may still be missing
            pass
    # Drop unplaced entries that we will retry on edge.
    town['unplaced'] = [t for t in town.get('unplaced', []) if t not in town['edge_want']]


def ensure_edge_fp():
    """Merge EXTRA_EDGE_FP into usable footprints without mutating settings defaults."""
    from region import EDGE_FP
    merged = dict(EDGE_FP)
    for k, v in EXTRA_EDGE_FP.items():
        if k not in merged:
            merged[k] = v
    return merged
