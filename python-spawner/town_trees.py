"""Town tree markers for the bake-off: clumpy yard trees and street trees.
NW Iowa 1990 small towns: clumped yard plantings, spaced street trees.
Species weights from Wray & Mize 1985 (Iowa 1990 street tree survey) and Gleason 1992 (elm decline).
Source: /workspace/research/iowa-1990-roads-lots-trees.md §5.
"""
from settings import option
from spawner import FT, Draw, H
import math

def rng(*keys):
    return Draw(H(*keys))

# Species weight table (simplified for the game / unsourced weights derived from Wray & Mize 1985 counts)
# sugar_maple 26, green_ash 22, silver_maple 18, norway_maple 12, hackberry 5, honeylocust 4,
# linden 3, bur_oak 3, spruce 3, siberian_elm 3, american_elm 1
# Total = 100. Maple+ash = 78%, American elm = 1%, Siberian elm = 3%
_SPECIES_WEIGHTS = [
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
]
_SPECIES_NAMES = [s for s, _ in _SPECIES_WEIGHTS]
_SPECIES_WTS = [w for _, w in _SPECIES_WEIGHTS]

# Elm condition: about half 'declining' (53% were in good condition per Gleason 1992)
_ELM_DECLINING_PCT = 0.47


def _draw_species(draw: Draw) -> str:
    return draw.weighted('species', list(zip(_SPECIES_NAMES, _SPECIES_WTS)))


def _elm_condition(draw: Draw) -> str:
    return 'declining' if draw.chance('elm_cond', _ELM_DECLINING_PCT) else 'good'


def _tree_record(x, y, species, setting, condition):
    return {'x': x, 'y': y, 'species': species, 'setting': setting, 'condition': condition, 'label': 'simplified'}


def _clark_evans_ratio(points, area_ticks2):
    """Compute Clark-Evans R for a set of points (in ticks). Returns R or None."""
    if len(points) < 2 or area_ticks2 <= 0:
        return None
    n = len(points)
    d_sum = 0
    for i in range(n):
        xi, yi = points[i]
        best = None
        for j in range(n):
            if i == j:
                continue
            xj, yj = points[j]
            d = math.hypot(xi - xj, yi - yj)
            if best is None or d < best:
                best = d
        d_sum += best
    r_obs = d_sum / n
    expected = 0.5 * math.sqrt(area_ticks2 / n)
    return r_obs / expected if expected > 0 else None


def town_markers(plan, seed, lot_id, town_center_dist_ft, is_corner_lot=False):
    """Generate tree markers for a town house lot.
    
    Args:
        plan: the house plan (has .lot with width, depth, site features)
        seed: base seed for RNG
        lot_id: unique lot identifier string
        town_center_dist_ft: distance from town center in feet (older blocks nearer center get more trees)
        is_corner_lot: whether this lot fronts two streets
    
    Returns:
        List of tree dicts with keys: x, y, species, setting, condition, label
        Coordinates are in lot-local system (matching plan.rooms coordinates).
    """
    lot = plan.lot
    place = lot.get('place', {})
    facing = place.get('facing', 'S')
    w = lot['width']
    d = lot['depth']
    front_sb = lot['front_setback']
    rear_sb = lot['rear_setback']
    side_l = lot['side_left']
    side_r = lot['width'] - lot['side_right']
    
    # For W/E facing, the lot-local coordinates have x along depth, y along width
    # But the plan's rooms are already in this coordinate system.
    # We just need to know where the road is in lot-local coords.
    # S-facing: road at y=0, lot extends +y, frontage along x (0 to w)
    # N-facing: road at y=d, lot extends -y (but lot-local y still 0 to d), frontage along x (0 to w)
    # W-facing: road at y=0, lot extends +y, frontage along x (0 to d)
    # E-facing: road at y=0, lot extends +y, frontage along x (0 to d)
    
    # In all cases, frontage dimension is 'w' for S/N, 'd' for W/E
    # But the plan's x coordinate runs along frontage for all facings.
    frontage_len = w if facing in ('S', 'N') else d
    depth_len = d if facing in ('S', 'N') else w
    
    # Road position in lot-local y
    road_y = 0 if facing in ('S', 'W', 'E') else d
    # Direction from road into lot
    yard_dir = 1 if facing in ('S', 'W', 'E') else -1
    
    # Blocked areas: house rooms, driveway, porch, deck, farm buildings
    blocked = []
    for room in plan.rooms:
        blocked.append((room.x, room.y, room.w, room.h))
    for _, r in plan.site.get('farm', []):
        blocked.append(r)
    for r in plan.site.get('drive', []):
        blocked.append(r)
    for k in ('porch', 'deck'):
        r = plan.site.get(k)
        if r:
            blocked.append(r)
    
    def is_blocked(x, y, radius=FT(10)):
        """Check if a circle at (x,y) with given radius intersects any blocked rect."""
        for bx, by, bw, bh in blocked:
            cx = max(bx, min(x, bx + bw))
            cy = max(by, min(y, by + bh))
            dx = x - cx
            dy = y - cy
            if dx * dx + dy * dy < radius * radius:
                return True
        return False
    
    def is_in_yard(x, y):
        """Check if point is in the yard (not in house footprint, not in front setback)."""
        # Yard is the area behind the front setback from the road
        if yard_dir == 1:
            # Road at y=0, yard starts at front_sb
            if y < front_sb:
                return False  # front setback / sidewalk area
        else:
            # Road at y=depth, yard starts at depth - front_sb
            if y > depth_len - front_sb:
                return False
        # Within side setbacks
        if x < side_l or x > side_r:
            return False
        return True
    
    trees = []
    draw = Draw(rng(seed, 'town_trees', lot_id))
    
    # --- Yard trees (rear and side yards) ---
    # Older blocks (closer to center) get slightly more trees
    center_bonus = 0
    if town_center_dist_ft < 1000:
        center_bonus = 1
    elif town_center_dist_ft < 2000:
        center_bonus = 0  # neutral
    
    # Base number of yard trees: 0-6, ~10% get none, target mean ~2.5
    yard_tree_roll = draw.chance('yard_count_0', 0.10)
    if yard_tree_roll:
        n_yard = 0
    else:
        n_yard = draw.weighted('yard_count', [(1, 15), (2, 25), (3, 30), (4, 20), (5, 7), (6, 3)])
    n_yard = min(6, n_yard + center_bonus)
    
    # Place yard trees in clumps of 1-3 in rear/side yards
    # Clump size 2-3 more common for clumping
    clumps = []
    remaining = n_yard
    while remaining > 0:
        clump_size = min(remaining, draw.weighted('clump_size', [(1, 15), (2, 55), (3, 30)]))
        clumps.append(clump_size)
        remaining -= clump_size
    
    # Find the house footprint bounding box
    house_rects = [r for r in blocked if r[2] > FT(20) and r[3] > FT(20)]
    if house_rects:
        hx0 = min(r[0] for r in house_rects)
        hy0 = min(r[1] for r in house_rects)
        hx1 = max(r[0] + r[2] for r in house_rects)
        hy1 = max(r[1] + r[3] for r in house_rects)
    else:
        hx0, hy0, hx1, hy1 = side_l, front_sb, side_r, front_sb + FT(30)
    
    # Yard zones (behind the house, away from road)
    if yard_dir == 1:
        # Road at y=0, yard goes positive y
        # Rear yard: behind house
        rear_y0 = hy1
        rear_y1 = depth_len - rear_sb
        # Side yards: alongside house
        side_y0 = front_sb
        side_y1 = hy1
    else:
        # Road at y=depth, yard goes negative y
        # Rear yard: behind house (toward y=0)
        rear_y0 = rear_sb
        rear_y1 = hy0
        # Side yards: alongside house
        side_y0 = hy1
        side_y1 = depth_len - front_sb
    
    zones = []
    if rear_y1 - rear_y0 > FT(10) and side_r - side_l > FT(10):
        zones.append(('rear', side_l, side_r, rear_y0, rear_y1))
    if hx0 - side_l > FT(8) and side_y1 - side_y0 > FT(10):
        zones.append(('left', side_l, hx0, side_y0, side_y1))
    if side_r - hx1 > FT(8) and side_y1 - side_y0 > FT(10):
        zones.append(('right', hx1, side_r, side_y0, side_y1))
    
    for clump_size in clumps:
        if not zones:
            break
        zone = draw.choice('zone_pick', zones)
        _, zx0, zx1, zy0, zy1 = zone
        
        # Ensure zy0 <= zy1
        if zy0 > zy1:
            zy0, zy1 = zy1, zy0
        
        placed = []
        attempts = 0
        while len(placed) < clump_size and attempts < 100:
            attempts += 1
            x = zx0 + draw.int('tree_x', zx1 - zx0)
            y = zy0 + draw.int('tree_y', zy1 - zy0)
            
            if not is_in_yard(x, y):
                continue
            if is_blocked(x, y, FT(10)):
                continue
            
            ok = True
            for px, py in placed:
                if math.hypot(x - px, y - py) < FT(12):
                    ok = False
                    break
            if not ok:
                continue

            for t in trees:
                if t['setting'] == 'yard' and math.hypot(x - t['x'], y - t['y']) < FT(12):
                    ok = False
                    break
            if not ok:
                continue
            
            species = _draw_species(draw)
            condition = _elm_condition(draw) if species in ('american_elm', 'siberian_elm') else 'good'
            trees.append(_tree_record(x, y, species, 'yard', condition))
            placed.append((x, y))
        
        # Fallback: if clump not fully placed, try anywhere in yard
        fallback_attempts = 0
        while len(placed) < clump_size and fallback_attempts < 50:
            fallback_attempts += 1
            x = side_l + draw.int('fallback_x', side_r - side_l)
            if yard_dir == 1:
                y = front_sb + draw.int('fallback_y', depth_len - rear_sb - front_sb)
            else:
                y = rear_sb + draw.int('fallback_y', depth_len - rear_sb - front_sb)
            if not is_in_yard(x, y):
                continue
            if is_blocked(x, y, FT(8)):
                continue
            ok = True
            for t in trees:
                if t['setting'] == 'yard' and math.hypot(x - t['x'], y - t['y']) < FT(10):
                    ok = False
                    break
            if not ok:
                continue
            species = _draw_species(draw)
            condition = _elm_condition(draw) if species in ('american_elm', 'siberian_elm') else 'good'
            trees.append(_tree_record(x, y, species, 'yard', condition))
            placed.append((x, y))
    
    # --- Street trees (in parkway, between sidewalk and road) ---
    # 30-50% of residential frontages get street trees
    # Place them 4-8 ft INSIDE the front lot line (i.e., in the parkway/front setback area)
    if draw.chance('street_tree', 0.70):
        if frontage_len >= FT(40):
            spacing_ft = 40 + draw.int('street_spacing', 31)  # 40-70 ft
            n_street = max(1, int(frontage_len / FT(spacing_ft)))
            n_street = min(n_street, 3)
            
            start_x = side_l + FT(10 + draw.int('street_start', 21))
            spacing = FT(spacing_ft)
            
            for i in range(n_street):
                x = start_x + i * spacing
                if x > side_r - FT(10):
                    break
                
                # Street trees go in the parkway: between road and sidewalk
                # Road is at road_y, front lot line is at road_y (same line)
                # Sidewalk is typically at front_sb from road
                # Tree goes 4-8 ft from road toward lot (i.e., in the parkway)
                if yard_dir == 1:
                    y = road_y + FT(4 + draw.int('street_y', 5))  # 4-8 ft from road
                else:
                    y = road_y - FT(4 + draw.int('street_y', 5))
                
                # Check not blocked (driveways, etc.)
                if is_blocked(x, y, FT(8)):
                    continue
                
                # Check distance from yard trees
                ok = True
                for t in trees:
                    if math.hypot(x - t['x'], y - t['y']) < FT(12):
                        ok = False
                        break
                if not ok:
                    continue
                
                species = _draw_species(draw)
                condition = _elm_condition(draw) if species in ('american_elm', 'siberian_elm') else 'good'
                trees.append(_tree_record(x, y, species, 'town_street', condition))
    
    return trees


def town_street_trees(plan, seed, lot_id, frontage_ft, is_corner=False):
    """Generate street trees for a lot frontage (used for lots without house plans).
    
    Returns list of tree dicts in lot-local coordinates.
    """
    trees = []
    draw = Draw(rng(seed, 'town_street_trees', lot_id))
    
    lot = plan.lot
    place = lot.get('place', {})
    facing = place.get('facing', 'S')
    w = lot['width']
    d = lot['depth']
    side_l = lot['side_left']
    side_r = w - lot['side_right']
    frontage_len = w if facing in ('S', 'N') else d
    
    if draw.chance('street_tree', 0.40):
        spacing_ft = 40 + draw.int('street_spacing', 31)
        n_street = max(1, int(frontage_len / FT(spacing_ft)))
        n_street = min(n_street, 3)
        
        start_x = side_l + FT(10 + draw.int('street_start', 21))
        spacing = FT(spacing_ft)
        
        for i in range(n_street):
            x = start_x + i * spacing
            if x > side_r - FT(10):
                break
            
            # Street trees in parkway
            y = FT(4 + draw.int('street_y', 5))
            
            # Check blocked
            blocked = []
            for r in plan.site.get('drive', []):
                blocked.append(r)
            for k in ('porch', 'deck'):
                r = plan.site.get(k)
                if r:
                    blocked.append(r)
            
            ok = True
            for bx, by, bw, bh in blocked:
                cx = max(bx, min(x, bx + bw))
                cy = max(by, min(y, by + bh))
                if (x - cx)**2 + (y - cy)**2 < FT(10)**2:
                    ok = False
                    break
            if not ok:
                continue
            
            species = _draw_species(draw)
            condition = _elm_condition(draw) if species in ('american_elm', 'siberian_elm') else 'good'
            trees.append(_tree_record(x, y, species, 'town_street', condition))
    
    return trees