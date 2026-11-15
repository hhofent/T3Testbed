"""Town streets, alleys, and Main Street layout for the town bake-off.
Wray & Mize 1985 / Spencer 100-ft Main Street (Naumann 2004) / Cushing 1983.
"""
from spawner import FT
from settings import option

def get_town_widths(d, size):
    """Return sw, al, main_w for non-parity mode."""
    sw = FT(80) if d.chance("row80", option("town.row_80_pct")) else FT(66)
    al = FT(option("town.alley_row"))
    main_w = FT(option("town.main_row"))
    return sw, al, main_w

def get_half_widths(rail_town, Xc, Yc, sw, main_w):
    """Return half-width functions for x and y."""
    def hw_x(x):
        if not rail_town and x == Xc:
            return main_w // 2
        return sw // 2

    def hw_y(y):
        if rail_town and y == Yc:
            return main_w // 2
        return sw // 2

    return hw_x, hw_y

def build_streets(seed, rail_town, Xc, Yc, xs, ys, blocks, sw, main_w, names_h):
    """Build connected town streets without fringe stubs and with explicit 100-ft Main Street."""
    streets = []
    if not blocks:
        return streets

    b_rects = [b["rect"] for b in blocks]
    min_bx = min(r[0] for r in b_rects)
    max_bx = max(r[0] + r[2] for r in b_rects)
    min_by = min(r[1] for r in b_rects)
    max_by = max(r[1] + r[3] for r in b_rects)

    # 1. Main Street (100 ft) along highway axis
    if rail_town:
        # Highway is Yc (axis 'h')
        main_lo = min_bx - sw
        main_hi = max_bx + sw
        streets.append({
            "id": "T-MAIN-h", "name": "Main Street", "axis": "h", "type": "paved",
            "c": Yc, "lo": main_lo, "hi": main_hi, "w": main_w, "surface_ft": 30,
            "road_class": "main_street", "row_ft": 100.0
        })
        # At Xc, add a cross street if town has blocks touching Xc
        xc_blocks = [b for b in blocks if abs(b["rect"][0] - (Xc + sw // 2)) < FT(5) or abs(b["rect"][0] + b["rect"][2] - (Xc - sw // 2)) < FT(5)]
        if xc_blocks:
            c_lo = min(b["rect"][1] for b in xc_blocks) - sw // 2
            c_hi = max(b["rect"][1] + b["rect"][3] for b in xc_blocks) + sw // 2
            streets.append({
                "id": "T-Xc", "name": "Railroad St", "axis": "v", "type": "paved",
                "c": Xc, "lo": c_lo, "hi": c_hi, "w": sw, "surface_ft": 24,
                "road_class": "town_street", "row_ft": sw / FT(1)
            })
    else:
        # Highway is Xc (axis 'v')
        main_lo = min_by - sw
        main_hi = max_by + sw
        streets.append({
            "id": "T-MAIN-v", "name": "Main Street", "axis": "v", "type": "paved",
            "c": Xc, "lo": main_lo, "hi": main_hi, "w": main_w, "surface_ft": 30,
            "road_class": "main_street", "row_ft": 100.0
        })
        # At Yc, add a cross street if town has blocks touching Yc
        yc_blocks = [b for b in blocks if abs(b["rect"][1] - (Yc + sw // 2)) < FT(5) or abs(b["rect"][1] + b["rect"][3] - (Yc - sw // 2)) < FT(5)]
        if yc_blocks:
            c_lo = min(b["rect"][0] for b in yc_blocks) - sw // 2
            c_hi = max(b["rect"][0] + b["rect"][2] for b in yc_blocks) + sw // 2
            streets.append({
                "id": "T-Yc", "name": "Cross St", "axis": "h", "type": "paved",
                "c": Yc, "lo": c_lo, "hi": c_hi, "w": sw, "surface_ft": 24,
                "road_class": "town_street", "row_ft": sw / FT(1)
            })

    # 2. Regular town avenues (vertical)
    for k, x in enumerate(xs):
        if x == Xc:
            continue
        # Find blocks adjacent to this avenue
        adj = [b for b in blocks if abs(b["rect"][0] - (x + sw // 2)) < FT(5) or abs(b["rect"][0] + b["rect"][2] - (x - sw // 2)) < FT(5)]
        if not adj:
            continue
        lo = min(b["rect"][1] for b in adj) - sw // 2
        hi = max(b["rect"][1] + b["rect"][3] for b in adj) + sw // 2
        # Connect to Main Street if crossing it
        if rail_town and lo <= Yc <= hi:
            lo = min(lo, Yc - main_w // 2)
            hi = max(hi, Yc + main_w // 2)
        s_num = k + 1
        s_suff = "st" if s_num == 1 else "nd" if s_num == 2 else "rd" if s_num == 3 else "th"
        streets.append({
            "id": f"T-A{k}", "name": f"{s_num}{s_suff} Ave", "axis": "v", "type": "paved",
            "c": x, "lo": lo, "hi": hi, "w": sw, "surface_ft": 24,
            "road_class": "town_street", "row_ft": sw / FT(1)
        })

    # 3. Regular town streets (horizontal)
    for k, y in enumerate(ys):
        if y == Yc:
            continue
        adj = [b for b in blocks if abs(b["rect"][1] - (y + sw // 2)) < FT(5) or abs(b["rect"][1] + b["rect"][3] - (y - sw // 2)) < FT(5)]
        if not adj:
            continue
        lo = min(b["rect"][0] for b in adj) - sw // 2
        hi = max(b["rect"][0] + b["rect"][2] for b in adj) + sw // 2
        # Connect to Main Street if crossing it
        if not rail_town and lo <= Xc <= hi:
            lo = min(lo, Xc - main_w // 2)
            hi = max(hi, Xc + main_w // 2)
        streets.append({
            "id": f"T-S{k}", "name": names_h[k % len(names_h)], "axis": "h", "type": "paved",
            "c": y, "lo": lo, "hi": hi, "w": sw, "surface_ft": 24,
            "road_class": "town_street", "row_ft": sw / FT(1)
        })

    return streets
