"""Arc-length measurements shared by verification and the A2 check harness."""
import math
import statistics


def station(points, distance):
    for a, b in zip(points, points[1:]):
        length = math.dist(a, b)
        if length and distance <= length:
            t = max(0, distance / length)
            return (a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1]))
        distance -= length
    return points[-1]


def receiver_tail(receiver, point):
    best = None
    for i, (a, b) in enumerate(zip(receiver['points'], receiver['points'][1:])):
        dx, dy = b[0] - a[0], b[1] - a[1]
        den = dx * dx + dy * dy
        t = max(0, min(1, ((point[0]-a[0])*dx + (point[1]-a[1])*dy)/den)) if den else 0
        q = (a[0]+t*dx, a[1]+t*dy)
        item = (math.dist(point, q), i, q)
        if best is None or item[:2] < best[:2]:
            best = item
    _, i, q = best
    return [q] + receiver['points'][i+1:]


def angle(a, b):
    den = math.hypot(*a)*math.hypot(*b)
    return math.degrees(math.acos(max(-1, min(1, (a[0]*b[0]+a[1]*b[1])/den)))) if den else 0


def summarize(values):
    bins = [sum(i <= v < i+10 for v in values) for i in range(0, 181, 10)]
    n = len(values)
    return dict(count=n, mean=statistics.mean(values) if n else 0,
                sd=statistics.pstdev(values) if n else 0, minimum=min(values, default=0),
                under25=sum(v < 25 for v in values)/max(1,n), bins=bins,
                largest_bin=max(bins)/max(1,n))


def junction_metrics(features, seed, rect=None):
    from world import MILE, H
    if rect is None: rect=(-20*MILE,-20*MILE,40*MILE,40*MILE)
    x0,y0,w,h=rect
    by_id = {f['id']: f for f in features}
    groups = {}
    for f in features:
        groups.setdefault((f.get('downstream'),tuple(f['points'][-1])),[]).append(f)
    joins = []
    for f in sorted(features, key=lambda f:f['id']):
        receiver = by_id.get(f.get('downstream'))
        if receiver is None:
            continue
        p = f['points'][-1]
        if not (x0 <= p[0] <= x0+w and y0 <= p[1] <= y0+h):
            continue
        tail = receiver_tail(receiver, p)
        # Continue through short receiver pieces to obtain the requested arc.
        seen = {receiver['id']}
        next_f = receiver
        while sum(math.dist(a,b) for a,b in zip(tail,tail[1:])) < .25*MILE:
            next_f = by_id.get(next_f.get('downstream'))
            if next_f is None or next_f['id'] in seen:
                break
            seen.add(next_f['id']); tail += next_f['points'][1:]
        continuation = math.dist(receiver['points'][0],p)<.01 and len(groups[(receiver['id'],tuple(p))])==1
        row = dict(id=f['id'], receiver=receiver['id'], point_mi=[v/MILE for v in p],
                   connection='continuation' if continuation else 'confluence')
        for span in (.1, .25):
            up = station(list(reversed(f['points'])), span*MILE)
            down = station(tail, span*MILE)
            row[str(span)] = angle((p[0]-up[0],p[1]-up[1]),(down[0]-p[0],down[1]-p[1]))
        a = f['points'][0]; b = receiver['points'][-1]
        row['raw_chord'] = angle((p[0]-a[0],p[1]-a[1]),(b[0]-p[0],b[1]-p[1]))
        normal = sum((H(seed,'junction-normal',f['id'],receiver['id'],i)%100000)/100000 for i in range(12))-6
        sampled = 65+30*normal
        sampled += .25*((90 if f.get('stream_order') != receiver.get('stream_order') else 58)-sampled)
        row['sampled'] = f.get('_junction_sample',max(25,min(110,sampled)))
        joins.append(row)
    return dict(joins=joins,
                unfiltered={str(s):summarize([r[str(s)] for r in joins]) for s in (.1,.25)},
                continuations={str(s):summarize([r[str(s)] for r in joins if r['connection']=='continuation']) for s in (.1,.25)},
                **{str(s):summarize([r[str(s)] for r in joins if r['connection']=='confluence']) for s in (.1,.25)})


def finished_crossings(seed,selected,by_id=None):
 """Exact finished-segment crossing scan, with the existing join exemptions."""
 from collections import defaultdict
 from world import MILE,FT,crossing_point,intersects
 by_id=by_id if by_id is not None else {f['id']:f for f in selected}
 crossings=[]
 # Index individual sampled segments, so dense 200-ft meanders do not trigger
 # an all-pairs scan over every point in a 40-mile window.
 cell=.1*MILE; buckets=defaultdict(list); segments=[]
 for f in selected:
  for i,(a,b) in enumerate(zip(f['points'],f['points'][1:])):
   bounds=(min(a[0],b[0]),min(a[1],b[1]),abs(b[0]-a[0]),abs(b[1]-a[1]))
   sid=len(segments); segments.append((f['id'],i,a,b))
   for ix in range(math.floor(bounds[0]/cell),math.floor((bounds[0]+bounds[2])/cell)+1):
    for iy in range(math.floor(bounds[1]/cell),math.floor((bounds[1]+bounds[3])/cell)+1): buckets[ix,iy].append(sid)
 checked=set()
 for ids in buckets.values():
  for pos,sid in enumerate(ids):
   f_id,i,a,b=segments[sid]
   for other in ids[pos+1:]:
    if sid==other: continue
    pair=(min(sid,other),max(sid,other))
    if pair in checked: continue
    checked.add(pair); g_id,j,c,d=segments[other]
    if f_id==g_id and abs(i-j)<=1: continue
    if f_id!=g_id and not intersects((min(a[0],b[0]),min(a[1],b[1]),abs(b[0]-a[0]),abs(b[1]-a[1])),(min(c[0],d[0]),min(c[1],d[1]),abs(d[0]-c[0]),abs(d[1]-c[1]))): continue
    hit=crossing_point(a,b,c,d)
    if hit is None: continue
    point,t,s,_=hit
    if (t<1e-7 or t>1-1e-7) and (s<1e-7 or s>1-1e-7): continue
    # Tributaries terminate on the interior of their downstream channel. This
    # endpoint-to-interior touch is the intended confluence, not a crossing.
    if f_id in by_id and by_id[f_id].get('downstream')==g_id and math.dist(point,by_id[f_id]['points'][-1])<=max(by_id[f_id]['width'],by_id[g_id]['width'])+FT(20): continue
    if g_id in by_id and by_id[g_id].get('downstream')==f_id and math.dist(point,by_id[g_id]['points'][-1])<=max(by_id[f_id]['width'],by_id[g_id]['width'])+FT(20): continue
    fa=by_id.get(f_id); ga=by_id.get(g_id)
    if fa and ga and fa.get('downstream') and fa.get('downstream')==ga.get('downstream'):
     target=fa['downstream']; parts=target.split(':')
     if len(parts)==4 and parts[:2]==['river','outlet']:
      try:
       from world import _outlet
       node=_outlet(seed,int(parts[2]),int(parts[3]))
      except ValueError: node=None
     else: node=by_id.get(target,{}).get('points',[None])[0]
     tolerance=max(fa.get('width',0),ga.get('width',0))+FT(50)
     if node is not None and math.dist(point,node)<=tolerance: continue
    crossings.append((f_id,g_id))
 return crossings
