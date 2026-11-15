"""Basin-owned, bounded coordinate repair. The drainage graph is immutable."""
import math
import numpy as np


def relocate_nodes(seed,cx,cy,features):
    import world as w

    mile=w.MILE
    channels=sorted((f for f in features if not f['id'].startswith('river:outlet:')
                     and tuple(f.get('basin',(cx,cy)))==(cx,cy)),key=lambda f:f['id'])
    routes=sorted((f for f in features if f['id'].startswith('river:outlet:')),key=lambda f:f['id'])
    if not channels or not routes: return
    # Rendering features are mutable cached objects. Coordinate decisions use
    # only the immutable seed-owned outlet curves, never a previous basin's
    # width propagation or approach rendering.
    canonical_routes=[]
    for f in routes:
        x,y=map(int,f['id'].split(':')[-2:])
        rid,points=w._route_curve(seed,x,y)
        canonical_routes.append((rid,points))
    route_indices=[(rid,w._segment_index(tuple(map(tuple,points)))) for rid,points in canonical_routes]
    route_lines=tuple((rid,points,w.box(points)) for rid,points in canonical_routes)
    nodes={}; fixed=set()
    for f in channels:
        for end in (0,-1): nodes.setdefault(tuple(f['points'][end]),[]).append((f,end))
        if f.get('downstream','').startswith('river:outlet:'): fixed.add(tuple(f['points'][-1]))
        f['_spacing_graph_before']={key:f[key] for key in ('id','downstream','stream_order','width')}
        f['_spacing_width_ends_before']=[f['widths'][0],f['widths'][-1]]
    original={p:p for p in nodes}; positions=dict(original)
    heights={p:w.elevation(seed,*p) for p in nodes}
    polygon=w._basin_polygon(seed,cx,cy); angle=w._basin_angle(seed)
    active=None

    def local_segments(p):
        records=[]
        for rid,(cell,segs,buckets,total) in route_indices:
            ix,iy=math.floor(p[0]/cell),math.floor(p[1]/cell); indices=set()
            for x in range(ix-2,ix+3):
                for y in range(iy-2,iy+3): indices.update(buckets.get((x,y),()))
            records.extend((rid,*segs[i]) for i in sorted(indices))
        if not records: return None
        starts=np.asarray([r[1] for r in records]); ends=np.asarray([r[2] for r in records])
        delta=ends-starts; denominator=np.sum(delta*delta,axis=1)
        return records,starts,delta,np.maximum(denominator,1e-30)

    def nearest(p,only=None):
        if only is None and active is not None:
            records,starts,delta,denominator=active
            ts=np.clip(np.sum((np.asarray(p)-starts)*delta,axis=1)/denominator,0,1)
            qs=starts+ts[:,None]*delta
            ds=np.hypot(qs[:,0]-p[0],qs[:,1]-p[1]); i=int(np.argmin(ds))
            rid,a,b,length,before=records[i]
            return float(ds[i]),rid,float(before+ts[i]*length),(float(qs[i,0]),float(qs[i,1]))
        best=None
        # A move is at most 0.5 mi; segments farther than this bounded local
        # grid cannot obstruct the 0.4-mi clearance target.
        for rid,(cell,segs,buckets,total) in route_indices:
            if only is not None and rid!=only: continue
            ix,iy=math.floor(p[0]/cell),math.floor(p[1]/cell)
            indices=set()
            for x in range(ix-1,ix+2):
                for y in range(iy-1,iy+2): indices.update(buckets.get((x,y),()))
            for i in sorted(indices):
                a,b,length,before=segs[i]; dx,dy=b[0]-a[0],b[1]-a[1]
                den=dx*dx+dy*dy
                t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/den)) if den else 0
                q=(a[0]+t*dx,a[1]+t*dy)
                row=(math.dist(p,q),rid,before+t*length,q)
                if best is None or row[:3]<best[:3]: best=row
        return best

    def exempt(point,incident,closest):
        _,rid,station,_=closest
        relevant=[f for f,end in incident if f.get('stream_order',0)>=2]
        if not relevant: return True
        for f in relevant:
            if f.get('downstream')!=rid: return False
            contact=nearest(tuple(f['points'][-1]),rid)
            if contact is None or abs(station-contact[2])>=.5*mile:
                length=sum(math.dist(a,b) for a,b in zip(f['points'],f['points'][1:]))
                if length>=.5*mile: return False
        return True

    def clear_chords(point,candidate,incident):
        affected={f['id'] for f,end in incident}
        for f,end in incident:
            a=candidate if end==0 else positions[tuple(f['points'][0])]
            b=candidate if end==-1 else positions[tuple(f['points'][-1])]
            if w._crosses_owned_route(a,b,route_lines): return False
            for g in channels:
                if g['id'] in affected: continue
                c=positions[tuple(g['points'][0])]; d=positions[tuple(g['points'][-1])]
                if max(a[0],b[0])<min(c[0],d[0]) or max(c[0],d[0])<min(a[0],b[0]) or max(a[1],b[1])<min(c[1],d[1]) or max(c[1],d[1])<min(a[1],b[1]): continue
                hit=w.crossing_point(a,b,c,d)
                if hit is not None and hit[0] not in (a,b,c,d): return False
        return True

    def node_id(p):
        return min((f['id'] for f,end in nodes[p] if end==0),default=min(f['id'] for f,end in nodes[p]))

    # Upstream nodes move first, so they can retain headroom for downstream
    # nodes. Every accepted move checks both sides of every incident edge.
    for point in sorted(nodes,key=lambda p:(-heights[p],node_id(p))):
        incident=nodes[point]
        if point in fixed: continue
        active=local_segments(point)
        if active is None: continue
        closest=nearest(point)
        if closest is None: continue
        distance,rid,station,q=closest
        # The small upstream buffer avoids flattening all elevations onto one
        # radius. It is only applied near the outlet, and is still <=0.405 mi.
        goal=.405*mile+.2*distance
        # A receiving start is pinned for 0.3 mi along its chord. Reserve
        # enough clearance for that inward run, not just for the node itself.
        high_out=[f for f,end in incident if end==0 and f.get('stream_order',0)>=2]
        for f in high_out:
            length=math.dist(f['points'][0],f['points'][-1])/mile
            if length>.5:
                goal=max(goal,.405*mile/max(.1,1-.3/length))
        if distance>=goal: continue
        obstructed=(distance<.4*mile or bool(high_out)) and not exempt(point,incident,closest)
        representative=min((f for f,end in incident if end==0),key=lambda f:f['id'],default=incident[0][0])
        report=dict(node_id=node_id(point),basin=[cx,cy],original_mi=[v/mile for v in point],
                    position_mi=[v/mile for v in point],distance_mi=distance/mile,
                    outlet=rid,status='blocked',movement_mi=0,spacing_obstruction=obstructed,
                    height_before_ft=heights[point],height_after_ft=heights[point],
                    incident=[[f['id'],end] for f,end in sorted(incident,key=lambda item:(item[0]['id'],item[1]))],
                    rejected=dict(elevation=0,basin=0,clearance=0,crossing=0))
        parent_heights=[heights[tuple(f['points'][-1])] for f,end in incident if end==0]
        child_heights=[heights[tuple(f['points'][0])] for f,end in incident if end==-1]
        lower=max(parent_heights,default=-math.inf); upper=min(child_heights,default=math.inf)
        normal=math.atan2(point[1]-q[1],point[0]-q[0])
        candidates=[]
        goals=[goal]+([.405*mile] if not high_out and distance<.405*mile else [])
        for goal_index,target in enumerate(goals):
            radii=[target-distance]+[i*.025*mile for i in range(1,21)]
            for radius_index,radius in enumerate(radii):
                if radius>.5*mile or radius<target-distance-1e-7: continue
                for angle_index in range(24):
                    theta=normal+angle_index*math.pi/12
                    candidate=(point[0]+radius*math.cos(theta),point[1]+radius*math.sin(theta))
                    candidates.append((goal_index,radius,w.H(seed,'relocate-node',cx,cy,node_id(point),goal_index,radius_index,angle_index),target,candidate))
            # Also sample the clearance contour. A narrow admissible elevation
            # band can fit between the radial grid's angles; tangential moves
            # retain clearance without unnecessarily lifting the node.
            nx,ny=math.cos(normal),math.sin(normal)
            for i in range(41):
                along=(i-20)*.025*mile
                candidate=(q[0]+nx*target-ny*along,q[1]+ny*target+nx*along)
                for _ in range(2):
                    near=nearest(candidate)
                    if near is None or near[0]<1: break
                    vx,vy=candidate[0]-near[3][0],candidate[1]-near[3][1]
                    correction=target-near[0]+1e-6*mile
                    candidate=(candidate[0]+vx/near[0]*correction,candidate[1]+vy/near[0]*correction)
                radius=math.dist(point,candidate)
                if radius<=.5*mile:
                    candidates.append((goal_index,radius,w.H(seed,'relocate-contour',cx,cy,node_id(point),goal_index,i),target,candidate))
        for _,radius,_,target,candidate in sorted(candidates):
            if not w._point_in_polygon(w._rot(*candidate,angle),polygon):
                report['rejected']['basin']+=1; continue
            near=nearest(candidate)
            if near is not None and near[0]<target-1e-6:
                report['rejected']['clearance']+=1; continue
            height=w.elevation(seed,*candidate)
            if not lower+1e-7<height<upper-1e-7:
                report['rejected']['elevation']+=1; continue
            if not clear_chords(point,candidate,incident):
                report['rejected']['crossing']+=1; continue
            positions[point]=candidate; heights[point]=height
            report.update(status='moved',height_after_ft=height,position_mi=[v/mile for v in candidate],movement_mi=radius/mile,
                          distance_mi=near[0]/mile if near is not None else 1.0)
            break
        representative['_node_relocation']=report

    for f in channels:
        old=f['points']; start,end=tuple(old[0]),tuple(old[-1]); a,b=positions[start],positions[end]
        if a==start and b==end: continue
        arc=[0.0]
        for p,q in zip(old,old[1:]): arc.append(arc[-1]+math.dist(p,q))
        total=arc[-1] or 1
        da=(a[0]-start[0],a[1]-start[1]); db=(b[0]-end[0],b[1]-end[1])
        new=[]
        for point,s in zip(old,arc):
            t=s/total
            new.append((point[0]+(1-t)*da[0]+t*db[0],point[1]+(1-t)*da[1]+t*db[1]))
        new[0]=a; new[-1]=b
        # Keep the width profile byte-for-byte: coordinate movement does not
        # change sampling, discharge, or channel width.
        f['points']=new; f['bounds']=w.box(new,f['width']/2)

    reports=[f['_node_relocation'] for f in channels if '_node_relocation' in f]
    moved=[r for r in reports if r['status']=='moved']
    summary=dict(basin=[cx,cy],nodes_moved=len(moved),move_sum_mi=sum(r['movement_mi'] for r in moved),
                 move_max_mi=max((r['movement_mi'] for r in moved),default=0),
                 moved_nodes=[{key:r[key] for key in ('node_id','original_mi','position_mi','movement_mi','distance_mi')}
                              for r in moved],
                 blocked_nodes=[{key:r[key] for key in ('node_id','original_mi','position_mi','distance_mi','outlet','incident','rejected')}
                                for r in reports if r['status']=='blocked' and r['spacing_obstruction']])
    for f in channels: f['_relocation_basin_stats']=summary
