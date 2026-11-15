"""Organic town outline. Integer ticks, simple closed polygon.

Notched padded AABB of all town content. Notch depth scales with town size
(no small absolute cap) so hull_ratio lands in 0.60-0.95; pad is deeper than
the notches so lots and buildings stay inside.
"""
from spawner import FT, H, Draw


def build_outline(rects, seed, pad_ft=40, cell_ft=50):
    if not rects:
        return [[0, 0], [1, 0], [1, 1], [0, 1]], (0, 0, 1, 1)
    rects = [r for r in rects if r[2] > 0 and r[3] > 0]
    # Drop only extreme outliers (>1.2 mi from median centre). Keep elevator,
    # depot and approach strings that sit just outside the block mass.
    cxs = sorted(r[0] + r[2] / 2 for r in rects)
    cys = sorted(r[1] + r[3] / 2 for r in rects)
    cx, cy = cxs[len(cxs) // 2], cys[len(cys) // 2]
    lim = FT(6400)
    near = [r for r in rects if abs(r[0] + r[2] / 2 - cx) < lim and abs(r[1] + r[3] / 2 - cy) < lim]
    if len(near) < max(3, len(rects) // 5):
        near = rects
    xs0 = [r[0] for r in near]; ys0 = [r[1] for r in near]
    xs1 = [r[0] + r[2] for r in near]; ys1 = [r[1] + r[3] for r in near]
    cw, ch = max(xs1) - min(xs0), max(ys1) - min(ys0)
    d = Draw(H(seed, 'town_outline'))
    # 16-20% notches, uncapped — small absolute caps left hull_ratio ~0.99 on
    # mid/big towns. Target hull ~0.85-0.93.
    pct = 18 + d.int('pct', 4)  # 18..21 — 16% left s30 at hull 0.951
    nx = max(FT(120), cw * pct // 100)
    ny = max(FT(120), ch * pct // 100)
    # Pad past notch tips (+ corner diagonal margin) so content stays inside.
    pad = max(FT(pad_ft), nx + FT(40), ny + FT(40))
    x0, y0 = min(xs0) - pad, min(ys0) - pad
    x1, y1 = max(xs1) + pad, max(ys1) + pad
    cxm, cym = (x0 + x1) // 2, (y0 + y1) // 2
    # Quarter-point side bites for a more organic silhouette (still simple).
    qx1 = x0 + (x1 - x0) // 4
    qx3 = x0 + 3 * (x1 - x0) // 4
    qy1 = y0 + (y1 - y0) // 4
    qy3 = y0 + 3 * (y1 - y0) // 4
    hx, hy = nx // 2, ny // 2
    out = [
        # bottom edge: corner cut, two notches
        [x0 + nx, y0], [qx1 - hx, y0], [qx1, y0 + hy], [qx1 + hx, y0],
        [cxm - hx, y0], [cxm, y0 + ny], [cxm + hx, y0],
        [qx3 - hx, y0], [qx3, y0 + hy], [qx3 + hx, y0], [x1 - nx, y0],
        # right edge
        [x1, y0 + ny], [x1, qy1 - hy], [x1 - hx, qy1], [x1, qy1 + hy],
        [x1, cym - hy], [x1 - nx, cym], [x1, cym + hy],
        [x1, qy3 - hy], [x1 - hx, qy3], [x1, qy3 + hy], [x1, y1 - ny],
        # top edge
        [x1 - nx, y1], [qx3 + hx, y1], [qx3, y1 - hy], [qx3 - hx, y1],
        [cxm + hx, y1], [cxm, y1 - ny], [cxm - hx, y1],
        [qx1 + hx, y1], [qx1, y1 - hy], [qx1 - hx, y1], [x0 + nx, y1],
        # left edge
        [x0, y1 - ny], [x0, qy3 + hy], [x0 + hx, qy3], [x0, qy3 - hy],
        [x0, cym + hy], [x0 + nx, cym], [x0, cym - hy],
        [x0, qy1 + hy], [x0 + hx, qy1], [x0, qy1 - hy], [x0, y0 + ny],
    ]
    clean = []
    for p in out:
        ip = [int(round(p[0])), int(round(p[1]))]
        if not clean or ip != clean[-1]:
            clean.append(ip)
    xs = [p[0] for p in clean]; ys = [p[1] for p in clean]
    return clean, (min(xs), min(ys), max(xs) - min(xs), max(ys) - min(ys))


def point_in_ring(pt, ring):
    x, y = pt
    n = len(ring)
    inside = False
    j = n - 1
    for i in range(n):
        xi, yi = ring[i]; xj, yj = ring[j]
        if ((yi > y) != (yj > y)) and (x < (xj - xi) * (y - yi) / ((yj - yi) or 1) + xi):
            inside = not inside
        j = i
    return inside
