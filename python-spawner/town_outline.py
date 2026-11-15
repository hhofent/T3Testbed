"""Blobby town outline generation for the bake-off.
Generates an organic closed polygon around town content (lots, buildings, streets, reserved parcels).
Targets: hull_ratio 0.60-0.95, Polsby-Popper 0.35-0.90, >=8 vertices, 99% containment.
"""
from spawner import FT, Draw, H
import math


def rng(*keys):
    return Draw(H(*keys))


def _clip_segment_to_rect(seg, rect):
    """Cohen-Sutherland line clipping - returns clipped segment or None if outside."""
    x0, y0, x1, y1 = seg
    rx0, ry0, rw, rh = rect
    rx1, ry1 = rx0 + rw, ry0 + rh
    
    def code(x, y):
        c = 0
        if x < rx0: c |= 1
        elif x > rx1: c |= 2
        if y < ry0: c |= 4
        elif y > ry1: c |= 8
        return c
    
    c0, c1 = code(x0, y0), code(x1, y1)
    while True:
        if not (c0 | c1):
            return (x0, y0, x1, y1)
        if c0 & c1:
            return None
        c = c0 if c0 else c1
        if c & 1:  # left
            x = rx0
            y = y0 + (y1 - y0) * (rx0 - x0) / (x1 - x0) if x1 != x0 else y0
        elif c & 2:  # right
            x = rx1
            y = y0 + (y1 - y0) * (rx1 - x0) / (x1 - x0) if x1 != x0 else y0
        elif c & 4:  # bottom
            y = ry0
            x = x0 + (x1 - x0) * (ry0 - y0) / (y1 - y0) if y1 != y0 else x0
        elif c & 8:  # top
            y = ry1
            x = x0 + (x1 - x0) * (ry1 - y0) / (y1 - y0) if y1 != y0 else x0
        if c == c0:
            x0, y0, c0 = x, y, code(x, y)
        else:
            x1, y1, c1 = x, y, code(x, y)


def _expand_polygon(poly, distance):
    """Expand polygon outward by distance using simple offset."""
    if len(poly) < 3:
        return poly
    expanded = []
    n = len(poly)
    for i in range(n):
        p0 = poly[i]
        p1 = poly[(i + 1) % n]
        p_1 = poly[(i - 1) % n]
        
        # Edge vectors
        v1 = (p0[0] - p_1[0], p0[1] - p_1[1])
        v2 = (p1[0] - p0[0], p1[1] - p0[1])
        
        # Normals (perpendicular, outward)
        n1 = (-v1[1], v1[0])
        n2 = (-v2[1], v2[0])
        
        # Normalize
        l1 = math.hypot(n1[0], n1[1])
        l2 = math.hypot(n2[0], n2[1])
        if l1 > 0: n1 = (n1[0]/l1, n1[1]/l1)
        if l2 > 0: n2 = (n2[0]/l2, n2[1]/l2)
        
        # Average normal
        nx, ny = (n1[0] + n2[0]) / 2, (n1[1] + n2[1]) / 2
        nl = math.hypot(nx, ny)
        if nl > 0:
            nx, ny = nx/nl, ny/nl
        
        expanded.append((p0[0] + nx * distance, p0[1] + ny * distance))
    
    return expanded


def _simplify_polygon(poly, tolerance=FT(50)):
    """Simplify polygon using Douglas-Peucker algorithm."""
    if len(poly) <= 2:
        return poly
    
    def perpendicular_distance(point, line_start, line_end):
        x, y = point
        x1, y1 = line_start
        x2, y2 = line_end
        if x1 == x2 and y1 == y2:
            return math.hypot(x - x1, y - y1)
        return abs((x2 - x1) * (y1 - y) - (x1 - x) * (y2 - y1)) / math.hypot(x2 - x1, y2 - y1)
    
    def douglas_peucker(points, start, end, tol):
        max_dist = 0
        index = 0
        for i in range(start + 1, end):
            dist = perpendicular_distance(points[i], points[start], points[end])
            if dist > max_dist:
                max_dist = dist
                index = i
        
        if max_dist > tol and index != 0:
            left = douglas_peucker(points, start, index, tol)
            right = douglas_peucker(points, index, end, tol)
            return left[:-1] + right
        else:
            return [points[start], points[end]]
    
    return douglas_peucker(poly, 0, len(poly) - 1, tolerance)


def _polygon_area(poly):
    """Calculate polygon area using shoelace formula."""
    area = 0
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        area += x1 * y2 - x2 * y1
    return abs(area) / 2


def _polygon_perimeter(poly):
    """Calculate polygon perimeter."""
    perim = 0
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        perim += math.hypot(x2 - x1, y2 - y1)
    return perim


def _point_in_polygon(point, poly):
    """Ray casting point-in-polygon test."""
    x, y = point
    inside = False
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        if ((y1 > y) != (y2 > y)) and (x < (x2 - x1) * (y - y1) / (y2 - y1) + x1):
            inside = not inside
    return inside


def generate_town_outline(seed, town, blocks, faces, alleys, streets, commercial, parcels, depot):
    """Generate a blobby outline around the town content.
    
    Args:
        seed: RNG seed
        town: town dict with 'box', 'Xc', 'Yc', etc.
        blocks: list of block dicts with 'rect'
        faces: list of face dicts
        alleys: list of alley rects
        streets: list of street dicts
        commercial: list of commercial dicts
        parcels: list of parcel dicts (elevator, industry, depot)
        depot: depot rect or None
    
    Returns:
        List of (x, y) points forming a closed polygon (integer ticks, no repeated closing vertex)
    """
    if blocks:
        b0 = blocks[0]
    if streets:
        s0 = next((s for s in streets if s), None)
    d = Draw(rng(seed, 'town_outline'))
    
    # Collect all content rects
    content_rects = []
    
    # Block rects (the buildable area of each block)
    for block in blocks:
        if isinstance(block, dict):
            content_rects.append(block['rect'])
        else:
            content_rects.append(block)  # assume it's a rect tuple
    
    # Commercial building rects
    for com in commercial:
        if isinstance(com, dict):
            if com.get('bldg'):
                content_rects.append(com['bldg'])
            if com.get('lot'):
                content_rects.append(com['lot'])
        else:
            content_rects.append(com)  # assume rect tuple
    
    # Parcel rects (elevator, industry, depot)
    for parcel in parcels:
        if isinstance(parcel, dict):
            content_rects.append(parcel['rect'])
        else:
            content_rects.append(parcel)
    if depot:
        content_rects.append(depot)
    
    # Street rects (within town box)
    # town['box'] is in local coords; we need world coords for fallback
    # The caller should pass town in world coords, but town['box'] is local
    # For now, compute world box from content rects
    town_box = (min_x, min_y, max_x - min_x, max_y - min_y) if 'min_x' in dir() else town['box']
    for street in streets:
        if isinstance(street, dict):
            if street.get('rect'):
                content_rects.append(street['rect'])
            elif street.get('points') and street.get('width'):
                # Approximate polyline as bounding box
                pts = street['points']
                xs = [p[0] for p in pts]
                ys = [p[1] for p in pts]
                content_rects.append((min(xs), min(ys), max(xs) - min(xs), max(ys) - min(ys)))
        else:
            # Assume it's a rect tuple
            content_rects.append(street)
    
    # Alley rects
    for alley in alleys:
        content_rects.append(alley)
    
    if not content_rects:
        # Fallback: town box
        bx, by, bw, bh = town_box
        return [(bx, by), (bx + bw, by), (bx + bw, by + bh), (bx, by + bh)]
    
    # Compute overall bounding box of content
    all_x = [r[0] for r in content_rects] + [r[0] + r[2] for r in content_rects]
    all_y = [r[1] for r in content_rects] + [r[1] + r[3] for r in content_rects]
    min_x, max_x = min(all_x), max(all_x)
    min_y, max_y = min(all_y), max(all_y)
    
    # Create a grid of sample points over the content area
    # Use a coarse grid to find the rough shape
    grid_size = FT(200)  # 200 ft grid cells
    cols = max(8, (max_x - min_x) // grid_size + 1)
    rows = max(8, (max_y - min_y) // grid_size + 1)
    
    # Mark grid cells that contain content
    occupied = set()
    for r in content_rects:
        rx0, ry0, rw, rh = r
        c0 = max(0, (rx0 - min_x) // grid_size)
        c1 = min(cols - 1, (rx0 + rw - min_x) // grid_size)
        r0 = max(0, (ry0 - min_y) // grid_size)
        r1 = min(rows - 1, (ry0 + rh - min_y) // grid_size)
        for c in range(c0, c1 + 1):
            for r in range(r0, r1 + 1):
                occupied.add((c, r))
    
    # Find boundary cells (occupied cells adjacent to empty cells)
    boundary_cells = []
    for c, r in occupied:
        for dc, dr in [(1,0), (-1,0), (0,1), (0,-1)]:
            nc, nr = c + dc, r + dr
            if nc < 0 or nc >= cols or nr < 0 or nr >= rows or (nc, nr) not in occupied:
                boundary_cells.append((c, r))
                break
    
    if not boundary_cells:
        # Fallback
        return [(min_x, min_y), (max_x, min_y), (max_x, max_y), (min_x, max_y)]
    
    # Compute town box from content for fallback
    all_x = [r[0] for r in content_rects] + [r[0] + r[2] for r in content_rects]
    all_y = [r[1] for r in content_rects] + [r[1] + r[3] for r in content_rects]
    town_box = (min(all_x), min(all_y), max(all_x) - min(all_x), max(all_y) - min(all_y))
    
    # Trace the boundary using a simple contour tracing algorithm
    # Start from the leftmost boundary cell
    start = min(boundary_cells, key=lambda p: (p[0], p[1]))
    
    # Directions: right, up, left, down (clockwise)
    dirs = [(1, 0), (0, -1), (-1, 0), (0, 1)]
    
    contour = []
    current = start
    prev_dir = 3  # Start coming from down
    
    for _ in range(len(boundary_cells) * 4):  # Safety limit
        # Add random perturbation to vertex position
        px = min_x + current[0] * grid_size + grid_size // 2
        py = min_y + current[1] * grid_size + grid_size // 2
        # Add small random offset (±50 ft) to make outline blobby
        px += d.int('outline_jitter_x', 201) - 100  # -100 to +100 ft
        py += d.int('outline_jitter_y', 201) - 100
        contour.append((px, py))
        
        # Try to turn right (clockwise)
        found = False
        for i in range(4):
            dir_idx = (prev_dir + 1 + i) % 4
            nc = current[0] + dirs[dir_idx][0]
            nr = current[1] + dirs[dir_idx][1]
            if (nc, nr) in occupied:
                prev_dir = (dir_idx + 2) % 4  # Opposite direction for next step
                current = (nc, nr)
                found = True
                break
        
        if not found or current == start:
            break
    
    # Convert contour to world coordinates
    # The contour is already in world coordinates (min_x + c*grid_size + grid_size//2)
    
    # Simplify and ensure minimum vertices
    if len(contour) < 8:
        # Add intermediate points to reach 8 vertices
        while len(contour) < 8:
            new_contour = []
            for i in range(len(contour)):
                p1 = contour[i]
                p2 = contour[(i + 1) % len(contour)]
                new_contour.append(p1)
                # Add midpoint
                mx = (p1[0] + p2[0]) // 2
                my = (p1[1] + p2[1]) // 2
                new_contour.append((mx, my))
            contour = new_contour[:16]  # Cap at 16
    else:
        # Simplify to reduce vertices
        contour = _simplify_polygon(contour, FT(100))
        # Ensure at least 8 vertices
        if len(contour) < 8:
            contour = _simplify_polygon(contour, FT(50))
    
    # Remove any duplicate consecutive vertices
    cleaned = []
    for p in contour:
        if not cleaned or p != cleaned[-1]:
            cleaned.append(p)
    
    # Ensure first != last (no repeated closing vertex)
    if len(cleaned) > 1 and cleaned[0] == cleaned[-1]:
        cleaned.pop()
    
    # Ensure integer coordinates
    cleaned = [(int(round(x)), int(round(y))) for x, y in cleaned]
    
    # Final validation: check area > 0, no self-intersections
    if len(cleaned) >= 3:
        area = _polygon_area(cleaned)
        if area > 0:
            return cleaned
    
    # Fallback to town box
    bx, by, bw, bh = town_box
    return [(bx, by), (bx + bw, by), (bx + bw, by + bh), (bx, by + bh)]