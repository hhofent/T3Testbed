"""Fuller towns: add back interior blocks, outward-facing lots, and improve lot fill.
LUNA overhaul-2 items C1, C2, C3, C4.
"""
from spawner import FT, Draw, H
from region import RAGGED_PCT, EDGE_HALF_PCT, RIBBON_DECAY
import math

def rng(*keys):
    return Draw(H(*keys))


def enhance_town(seed, town, rail_town, size, xs, ys, Xc, Yc, blocks, faces, alleys, streets, sw, main_w, names_h, ld, al, cy, sb, creeks, rail_hits):
    """Add back dropped blocks and add outward-facing lots on outer streets.
    
    Modifies town dict in place: adds to blocks, faces, alleys, streets, lots.
    """
    d = Draw(rng(seed, 'town_enhance'))
    cols = len(xs) - 1
    rows = len(ys) - 1
    
    # Track which block positions are occupied
    occupied = {(b['ij'][0], b['ij'][1]) for b in blocks}
    
    # --- 1. Add back some ragged-dropped outer blocks (C1: outward-facing lots) ---
    # Outer blocks that were dropped by ragged/ribbon/edge rules get a second chance
    # as outward-facing lots on the perimeter streets.
    
    for i in range(cols):
        for j in range(rows):
            if (i, j) in occupied:
                continue
            outer = i in (0, cols - 1) or j in (0, rows - 1)
            if not outer:
                continue  # only outer ring for now
            
            # Check if this block was eligible but dropped
            bx0 = xs[i] + (sw // 2 if xs[i] != Xc else main_w // 2)
            bx1 = xs[i + 1] - (sw // 2 if xs[i + 1] != Xc else main_w // 2)
            by0 = ys[j] + (sw // 2 if ys[j] != Yc else main_w // 2)
            by1 = ys[j + 1] - (sw // 2 if ys[j + 1] != Yc else main_w // 2)
            
            if bx1 <= bx0 or by1 <= by0:
                continue
            
            # Probability to add back: higher for corners, lower for edges
            if i in (0, cols - 1) and j in (0, rows - 1):
                add_prob = 0.7  # corners
            else:
                add_prob = 0.5  # edges
            
            if not d.chance(f'add_outer_{i}_{j}', add_prob):
                continue
            
            # Outward-facing lots: face the perimeter street, no alley
            # Depth 1.2-2x normal lot depth, 20-40% gaps
            normal_depth = ld
            outward_depth = FT(int(normal_depth * (1.2 + d.int('depth_mult', 81) / 100)))  # 1.2-2.0x
            
            # Determine which street this block faces
            if j == 0:
                # Faces south perimeter street (ys[0])
                face_fa = 'S'
                face_y = by0  # front lot line at block edge
                lot_y0 = face_y
                lot_y1 = face_y + outward_depth
            elif j == rows - 1:
                # Faces north perimeter street (ys[rows])
                face_fa = 'N'
                face_y = by1
                lot_y0 = face_y - outward_depth
                lot_y1 = face_y
            elif i == 0:
                # Faces west perimeter street (xs[0])
                face_fa = 'W'
                face_x = bx0
                lot_x0 = face_x
                lot_x1 = face_x + outward_depth
            else:  # i == cols - 1
                # Faces east perimeter street (xs[cols])
                face_fa = 'E'
                face_x = bx1
                lot_x0 = face_x - outward_depth
                lot_x1 = face_x
            
            # 20-40% gap chance
            if d.chance(f'gap_{i}_{j}', 0.30):
                continue
            
            # Add block for outward lots
            bld = outward_depth
            rect = None
            if face_fa in ('S', 'N'):
                rect = (bx0, min(lot_y0, lot_y1), bx1 - bx0, abs(lot_y1 - lot_y0))
            else:
                rect = (min(lot_x0, lot_x1), by0, abs(lot_x1 - lot_x0), by1 - by0)
            
            # Check for creek intersection
            if rect and rail_hits(list(creeks), rect):
                pass  # skip this block
            else:
                blocks.append({"id": f"b{i}{j}_out", "ij": (i, j), "rect": rect, "lot_d": bld, "outward": True})
                # No alley for outward lots
                
                # Add face for lot placement
                if face_fa in ('S', 'N'):
                    faces.append({"run": f"town:b{i}{j}_out:{face_fa}", "y0": lot_y0, "fa": face_fa, "bx0": bx0, "bx1": bx1, "d": bld,
                                  "front_y": face_y, "main": False, "dist": 999, "from_lo": True, "outward": True})
                else:
                    # For W/E facing, the face runs along y
                    faces.append({"run": f"town:b{i}{j}_out:{face_fa}", "y0": lot_x0, "fa": face_fa, "bx0": by0, "bx1": by1, "d": bld,
                                  "front_y": face_x, "main": False, "dist": 999, "from_lo": True, "outward": True})
    
    # --- 2. Add back some ribbon-dropped interior blocks ---
    # Blocks that were dropped by ribbon decay but not on outer edge
    for i in range(1, cols - 1):
        for j in range(1, rows - 1):
            if (i, j) in occupied:
                continue
            # Only add back if not too far from center
            ra = min(abs(xs[i] - Xc), abs(xs[i + 1] - Xc)) / (xs[1] - xs[0])
            rb = min(abs(ys[j] - Yc), abs(ys[j + 1] - Yc)) / cy
            dist_from_center = (ra + rb) / 2
            if dist_from_center > 3:
                continue  # too far out
            
            add_prob = 0.3 * (1 - dist_from_center / 3)  # 30% near center, tapering to 0
            if not d.chance(f'add_interior_{i}_{j}', add_prob):
                continue
            
            bx0 = xs[i] + (sw // 2 if xs[i] != Xc else main_w // 2)
            bx1 = xs[i + 1] - (sw // 2 if xs[i + 1] != Xc else main_w // 2)
            by0 = ys[j] + (sw // 2 if ys[j] != Yc else main_w // 2)
            by1 = ys[j + 1] - (sw // 2 if ys[j + 1] != Yc else main_w // 2)
            
            if bx1 <= bx0 or by1 <= by0:
                continue
            
            bld = (by1 - by0 - al) // 2
            if bld < FT(60):
                continue
            
            rect = (bx0, by0, bx1 - bx0, by1 - by0)
            # Check for creek intersection
            if rail_hits(list(creeks), rect):
                continue  # skip this block
            
            blocks.append({"id": f"b{i}{j}_in", "ij": (i, j), "rect": rect, "lot_d": bld})
            alleys.append((bx0, by0 + bld, bx1 - bx0, al))
            
            for half, y0, fa in (("S", by0, "S"), ("N", by1 - bld, "N")):
                front = ys[j] if half == "S" else ys[j + 1]
                main = front == Yc
                faces.append({"run": f"town:b{i}{j}_in:{half}", "y0": y0, "fa": fa, "bx0": bx0, "bx1": bx1, "d": bld,
                              "front_y": by0 if half == "S" else by1, "main": main,
                              "dist": min(abs(xs[i] - Xc), abs(xs[i + 1] - Xc)), "from_lo": xs[i] == Xc or (xs[i + 1] != Xc and xs[i] < Xc)})
    
    # --- 3. Some blocks get lots facing N-S avenues (C3: ~25% of blocks) ---
    # This is handled in face generation by adding W/E facing faces for some blocks
    # We'll add this as a post-process on faces
    
    town['blocks'] = blocks
    town['faces'] = faces
    town['alleys'] = alleys
    town['streets'] = streets
    
    return town


def add_corner_lots(seed, town, blocks, faces, xs, ys, Xc, Yc, sw, main_w, sb):
    """Add lots on corner blocks facing both streets (C3: ~25% of blocks)."""
    d = Draw(rng(seed, 'corner_lots'))
    occupied_faces = {(f['run'], f['fa']) for f in faces}
    
    for block in blocks:
        i, j = block['ij']
        if block.get('outward') or block.get('interior'):
            continue
        # 25% chance for corner-facing lots
        if not d.chance(f'corner_{i}_{j}', 0.25):
            continue
        
        # Add W/E facing faces for this block
        rx, ry, rw, rh = block['rect']
        bx0, bx1 = rx, rx + rw
        bld = block['lot_d']
        by0, by1 = ry, ry + rh
        base_run = f"town:b{i}{j}"
        
        # West face
        if (f"{base_run}:W", 'W') not in occupied_faces:
            faces.append({"run": f"{base_run}:W", "y0": bx0, "fa": "W", "bx0": by0, "bx1": by0 + bld, "d": bx1 - bx0,
                          "front_x": bx0, "front_y": (by0 + by1) // 2, "main": False, "dist": abs(bx0 - Xc), "from_lo": True})
            occupied_faces.add((f"{base_run}:W", 'W'))
        # East face
        if (f"{base_run}:E", 'E') not in occupied_faces:
            faces.append({"run": f"{base_run}:E", "y0": bx1, "fa": "E", "bx0": by0, "bx1": by0 + bld, "d": bx1 - bx0,
                          "front_x": bx1, "front_y": (by0 + by1) // 2, "main": False, "dist": abs(bx1 - Xc), "from_lo": False})
            occupied_faces.add((f"{base_run}:E", 'E'))
    
    town['faces'] = faces
    return town