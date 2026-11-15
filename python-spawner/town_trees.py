"""Clumpy town yard and street trees. Leaves trees.local_markers untouched for rural/parity."""
from spawner import FT, H, Draw
from settings import option

# Wray & Mize 1985 counts (Iowa street trees) + Gleason 1992 elm decline.
# Weights simplified for the game from /workspace/research/iowa-1990-roads-lots-trees.md §5.
SPECIES_WEIGHTS = (
    ('sugar_maple', 26),
    ('green_ash', 22),
    ('silver_maple', 18),
    ('norway_maple', 12),
    ('hackberry', 5),
    ('honeylocust', 4),
    ('linden', 3),
    ('bur_oak', 3),
    ('spruce', 3),
    ('siberian_elm', 3),
    ('american_elm', 1),
)


def _pick_species(d):
    names, weights = zip(*SPECIES_WEIGHTS)
    return d.weighted('sp', list(zip(names, weights)))


def _condition(d, species):
    if species in ('american_elm', 'siberian_elm'):
        return 'declining' if d.chance('decl', 50) else 'good'
    return 'good' if d.chance('good', 85) else 'declining'


def _blocked(x, y, blockers, pad):
    for a, b, w, h in blockers:
        if a - pad <= x <= a + w + pad and b - pad <= y <= b + h + pad:
            return True
    return False


def _clump_points(d, x0, y0, x1, y1, n, min_sep, blockers, pad):
    """Place n points in axis-aligned yard box, clumped, never grid-aligned."""
    if n <= 0 or x1 - x0 < min_sep or y1 - y0 < min_sep:
        return []
    # Seed clump centres, then members near them.
    n_clumps = 1 if n == 1 else (2 if n >= 3 and d.chance('2c', 40) else 1)
    sizes = []
    left = n
    for i in range(n_clumps):
        take = left if i == n_clumps - 1 else max(1, min(left - (n_clumps - i - 1), 1 + d.int(f'cs{i}', min(2, left))))
        sizes.append(take)
        left -= take
    pts = []
    for ci, sz in enumerate(sizes):
        cx = x0 + d.int(f'cx{ci}', max(1, int((x1 - x0) / FT(1)))) * FT(1)
        cy = y0 + d.int(f'cy{ci}', max(1, int((y1 - y0) / FT(1)))) * FT(1)
        # Jitter off any 30-ft lattice residual.
        cx += (d.int(f'jx{ci}', 11) - 5) * FT(1)
        cy += (d.int(f'jy{ci}', 11) - 5) * FT(1)
        for k in range(sz):
            for attempt in range(24):
                ox = (d.int(f'ox{ci}{k}{attempt}', 17) - 8) * FT(1)
                oy = (d.int(f'oy{ci}{k}{attempt}', 17) - 8) * FT(1)
                x, y = cx + ox, cy + oy
                if not (x0 <= x <= x1 and y0 <= y <= y1):
                    continue
                if _blocked(x, y, blockers, pad):
                    continue
                if any((x - px) * (x - px) + (y - py) * (y - py) < min_sep * min_sep for px, py in pts):
                    continue
                # Reject exact multiples of 30 ft from lot origin (grid tell).
                if (x - x0) % FT(30) == 0 and (y - y0) % FT(30) == 0:
                    continue
                pts.append((x, y))
                break
    return pts


def town_markers(plan, seed, lot_id, centre_boost=0, street_tree=False, front_inset_ft=None):
    """Yard trees (and optional street tree) for one house lot.

    plan: spawner Plan with .lot and .rooms / .site
    Returns list of dicts: x, y (lot-local), species, setting, condition, label.
    """
    lot = plan.lot
    W, D = lot['width'], lot['depth']
    pad = FT(option('world.tree_radius_ft'))
    blocked = [r.rect() for r in plan.rooms]
    blocked += [r for _, r in plan.site.get('farm', [])]
    blocked += list(plan.site.get('drive', []))
    for k in ('porch', 'deck'):
        if plan.site.get(k):
            blocked.append(plan.site[k])
    d = Draw(H(seed, 'town_trees', lot_id))
    # ~20% of lots get no yard trees; older/central lots get slightly more.
    roll = d.int('ny', 100)
    if roll < 20 - centre_boost:
        n_yard = 0
    elif roll < 55 - centre_boost:
        n_yard = 1
    elif roll < 85:
        n_yard = 2
    else:
        n_yard = 3
    n_yard = min(3, n_yard)  # yard trees only; street tree may add one more (max 4 << 6)
    min_sep = FT(15)
    # Rear and side yards (lot-local: y grows into the lot from the street).
    front_sb = max(FT(8), int(getattr(plan, 'front_setback', FT(20)) or FT(20)))
    rear_band = (max(front_sb + FT(10), D * 55 // 100), D - FT(4))
    side_band_l = (FT(4), max(FT(4), W // 5))
    side_band_r = (min(W - W // 5, W - FT(4)), W - FT(4))
    # Prefer rear, then sides.
    zones = []
    if rear_band[1] > rear_band[0]:
        zones.append((FT(4), rear_band[0], W - FT(4), rear_band[1]))
    if side_band_l[1] > side_band_l[0]:
        zones.append((side_band_l[0], front_sb, side_band_l[1], D - FT(4)))
    if side_band_r[1] > side_band_r[0]:
        zones.append((side_band_r[0], front_sb, side_band_r[1], D - FT(4)))
    out = []
    left = n_yard
    for zi, (x0, y0, x1, y1) in enumerate(zones):
        if left <= 0:
            break
        take = min(left, 1 + d.int(f'zt{zi}', min(2, left)))
        for x, y in _clump_points(Draw(H(seed, 'town_trees', lot_id, 'z', zi)), x0, y0, x1, y1, take, min_sep, blocked, pad):
            sp = _pick_species(Draw(H(seed, 'town_trees', lot_id, 'sp', len(out))))
            out.append({'x': x, 'y': y, 'species': sp, 'setting': 'yard',
                        'condition': _condition(Draw(H(seed, 'town_trees', lot_id, 'cd', len(out))), sp),
                        'label': 'simplified'})
            left -= 1
            if left <= 0:
                break
    if street_tree:
        # 4-8 ft inside the front lot line. Use >= 6 ft so the 1.5 ft trunk
        # radius stays clear of the road feature (roads are full-ROW rects;
        # a parkway tree would conflict — noted in the bake-off report).
        inset = FT(front_inset_ft if front_inset_ft is not None else (6 + Draw(H(seed, 'town_trees', lot_id, 'ins')).int('i', 3)))
        sx = W // 2 + (Draw(H(seed, 'town_trees', lot_id, 'sx')).int('j', 21) - 10) * FT(1)
        sy = inset
        if FT(4) <= sx <= W - FT(4) and not _blocked(sx, sy, blocked, pad):
            if all((sx - p['x']) ** 2 + (sy - p['y']) ** 2 >= min_sep ** 2 for p in out):
                sp = _pick_species(Draw(H(seed, 'town_trees', lot_id, 'ssp')))
                out.append({'x': sx, 'y': sy, 'species': sp, 'setting': 'town_street',
                            'condition': _condition(Draw(H(seed, 'town_trees', lot_id, 'scd')), sp),
                            'label': 'simplified'})
    return out
