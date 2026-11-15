"""Raster union outline for town-owned rectangles (integer world ticks)."""
from collections import deque


def _inside(x, y, r):
    a, b, w, h = r
    return a <= x <= a + w and b <= y <= b + h


def _ring(cells, cell, ox, oy):
    edges = set()
    for x, y in cells:
        if (x, y - 1) not in cells: edges.add(((x, y), (x + 1, y)))
        if (x + 1, y) not in cells: edges.add(((x + 1, y), (x + 1, y + 1)))
        if (x, y + 1) not in cells: edges.add(((x + 1, y + 1), (x, y + 1)))
        if (x - 1, y) not in cells: edges.add(((x, y + 1), (x, y)))
    by_start = {}
    for a, b in sorted(edges): by_start.setdefault(a, []).append(b)
    first = min(edges); start, cur = first; points = [start]; prev = start
    for _ in range(len(edges) + 1):
        points.append(cur)
        if cur == start: break
        opts = by_start.get(cur, [])
        if not opts: return []
        # Prefer left, then straight, right, back: keeps the occupied side left.
        dx, dy = cur[0] - prev[0], cur[1] - prev[1]
        def rank(n):
            ndx, ndy = n[0] - cur[0], n[1] - cur[1]
            cross = dx * ndy - dy * ndx
            dot = dx * ndx + dy * ndy
            return (0 if cross > 0 else 1 if dot > 0 else 2 if cross < 0 else 3)
        nxt = min(opts, key=lambda p: (rank(p), p))
        edges.remove((cur, nxt)); prev, cur = cur, nxt
    if points[-1] == start: points.pop()
    world = [(ox + x * cell, oy + y * cell) for x, y in points]
    # Remove collinear vertices while retaining the orthogonal boundary.
    out = []
    for i, p in enumerate(world):
        a, b = world[i - 1], world[(i + 1) % len(world)]
        if (p[0] - a[0]) * (b[1] - p[1]) != (p[1] - a[1]) * (b[0] - p[0]): out.append(p)
    return out


def _dp(points, tol):
    if len(points) <= 2: return points
    a,b=points[0],points[-1]; dx=b[0]-a[0]; dy=b[1]-a[1]; den=dx*dx+dy*dy or 1
    best=-1; idx=0
    for i,p in enumerate(points[1:-1],1):
        t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/den))
        dist=(p[0]-(a[0]+t*dx))**2+(p[1]-(a[1]+t*dy))**2
        if dist>best: best=dist; idx=i
    if best<=tol*tol: return [a,b]
    left=_dp(points[:idx+1],tol); right=_dp(points[idx:],tol)
    return left[:-1]+right


def _smooth(ring, tolerance):
    pts=[(float(x),float(y)) for x,y in ring]
    for _ in range(2):
        nxt=[]
        for a,b in zip(pts,pts[1:]+pts[:1]):
            nxt.append((.75*a[0]+.25*b[0],.75*a[1]+.25*b[1]))
            nxt.append((.25*a[0]+.75*b[0],.25*a[1]+.75*b[1]))
        pts=nxt
    out=[]
    for i,p in enumerate(pts):
        q=(round(p[0]),round(p[1]))
        if not out or q!=out[-1]: out.append(q)
    changed=True
    while changed and len(out)>3:
        changed=False; keep=[]
        for i,p in enumerate(out):
            a,b=out[i-1],out[(i+1)%len(out)]
            if (p[0]-a[0])*(b[1]-p[1]) == (p[1]-a[1])*(b[0]-p[0]): changed=True
            else: keep.append(p)
        out=keep
    if len(out)>2:
        far=max(range(1,len(out)),key=lambda i:(out[i][0]-out[0][0])**2+(out[i][1]-out[0][1])**2)
        one=_dp(out[:far+1],tolerance); two=_dp(out[far:]+[out[0]],tolerance)
        out=one[:-1]+two[:-1]
    return out


def outline(rects, cell, buffer, close_cells):
    rects = [tuple(map(int, r)) for r in rects if r[2] > 0 and r[3] > 0]
    if not rects: return []
    cell = max(1, int(cell)); radius = max(0, int(close_cells)); pad = int(buffer) + (radius + 2) * cell
    minx = min(r[0] for r in rects) - pad; miny = min(r[1] for r in rects) - pad
    maxx = max(r[0] + r[2] for r in rects) + pad; maxy = max(r[1] + r[3] for r in rects) + pad
    nx = (maxx - minx + cell - 1) // cell; ny = (maxy - miny + cell - 1) // cell
    marked = set()
    for r in rects:
        a, b, w, h = r; a -= buffer; b -= buffer; x1=a+w+buffer; y1=b+h+buffer
        for ix in range(max(0,(a-minx)//cell), min(nx,(x1-minx)//cell+1)):
            cx=minx+ix*cell+cell//2
            if not a <= cx <= x1: continue
            for iy in range(max(0,(b-miny)//cell), min(ny,(y1-miny)//cell+1)):
                cy=miny+iy*cell+cell//2
                if b <= cy <= y1: marked.add((ix,iy))
    # Morphological closing with a square structuring element.
    if radius:
        dilated={(x+dx,y+dy) for x,y in marked for dx in range(-radius,radius+1) for dy in range(-radius,radius+1)}
        marked={ (x,y) for x,y in dilated if all((x+dx,y+dy) in dilated for dx in range(-radius,radius+1) for dy in range(-radius,radius+1)) }
    # Fill enclosed holes and keep the largest four-connected component.
    bounds=(min(x for x,y in marked)-1,max(x for x,y in marked)+1,min(y for x,y in marked)-1,max(y for x,y in marked)+1)
    x0,x1,y0,y1=bounds; seen=set(); q=deque()
    for x in range(x0,x1+1):
        for y in (y0,y1):
            if (x,y) not in marked: seen.add((x,y)); q.append((x,y))
    for y in range(y0,y1+1):
        for x in (x0,x1):
            if (x,y) not in marked and (x,y) not in seen: seen.add((x,y)); q.append((x,y))
    while q:
        x,y=q.popleft()
        for p in ((x+1,y),(x-1,y),(x,y+1),(x,y-1)):
            if x0<=p[0]<=x1 and y0<=p[1]<=y1 and p not in marked and p not in seen: seen.add(p); q.append(p)
    holes={(x,y) for x in range(x0,x1+1) for y in range(y0,y1+1) if (x,y) not in marked and (x,y) not in seen}
    marked |= holes
    comps=[]; remain=set(marked)
    while remain:
        seed=min(remain); remain.remove(seed); comp={seed}; q=[seed]
        while q:
            x,y=q.pop()
            for p in ((x+1,y),(x-1,y),(x,y+1),(x,y-1)):
                if p in remain: remain.remove(p); comp.add(p); q.append(p)
        comps.append(comp)
    largest=min(comps,key=lambda c:(-len(c),min(c)))
    return _smooth(_ring(largest,cell,minx,miny),1.5*cell)
