"""Geometric verification. Failures return nonzero exit status through cli.py."""
import math,json,statistics
from collections import defaultdict
from settings import Settings,applied,option
from world import window,intersects,MILE
from region import make_region,road_rect,_seg_rect
from spawner import FT

def positive_overlap(a,b): return a[0]<b[0]+b[2] and b[0]<a[0]+a[2] and a[1]<b[1]+b[3] and b[1]<a[1]+a[3]

class Index:
 def __init__(self,features):
  self.size=FT(option('verify.bin_ft')); self.cells=defaultdict(list)
  for f in features:
   for key in self.keys(f['bounds']): self.cells[key].append(f)
 def keys(self,r):
  for x in range(math.floor(r[0]/self.size),math.floor((r[0]+r[2])/self.size)+1):
   for y in range(math.floor(r[1]/self.size),math.floor((r[1]+r[3])/self.size)+1): yield x,y
 def candidates(self,r):
  found={}
  for k in self.keys(r):
   for f in self.cells[k]: found[f['id']]=f
  return found.values()

def building_collisions(features):
 buildings=[f for f in features if f['kind']=='buildings']; index=Index(buildings); collisions=[]
 for f in buildings:
  for other in index.candidates(f['bounds']):
   if other['id']>f['id'] and positive_overlap(f['bounds'],other['bounds']): collisions.append((f['id'],other['id']))
 return collisions

def orphan_roads(features):
 roads=[f for f in features if f['kind']=='roads']; index=Index(roads)
 parent={r['id']:r['id'] for r in roads}
 def root(id):
  while parent[id]!=id: parent[id]=parent[parent[id]]; id=parent[id]
  return id
 for r in roads:
  for other in index.candidates(r['bounds']):
   if intersects(r['bounds'],other['bounds']): parent[root(r['id'])]=root(other['id'])
 components=defaultdict(list)
 for r in roads: components[root(r['id'])].append(r['id'])
 largest=max(components.values(),key=len,default=[])
 return [id for group in components.values() if group is not largest for id in group]

def intersection_angle(a,b,c,d):
 u=(b[0]-a[0],b[1]-a[1]); v=(d[0]-c[0],d[1]-c[1]); det=u[0]*v[1]-u[1]*v[0]
 if det==0: return None
 q=(c[0]-a[0],c[1]-a[1]); t=(q[0]*v[1]-q[1]*v[0])/det; s=(q[0]*u[1]-q[1]*u[0])/det
 if not (0<=t<=1 and 0<=s<=1): return None
 cosine=min(1,abs(u[0]*v[0]+u[1]*v[1])/(math.hypot(*u)*math.hypot(*v)))
 return math.degrees(math.acos(cosine))

def road_line(f):
 if 'points' in f: return f['points'][0],f['points'][-1]
 x,y,w,h=f['rect']; axis=f.get('axis') or ('h' if w>=h else 'v')
 return ((x,y+h/2),(x+w,y+h/2)) if axis=='h' else ((x+w/2,y),(x+w/2,y+h))

def crossing_angles(features):
 angles=[]; roads=Index([f for f in features if f['kind']=='roads'])
 for rail in [f for f in features if f['kind']=='rail']:
  for road in roads.candidates(rail['bounds']):
   points=road.get('points') or list(road_line(road))
   for c,d in zip(points,points[1:]):
    for a,b in zip(rail['points'],rail['points'][1:]):
     angle=intersection_angle(a,b,c,d)
     if angle is not None: angles.append(angle)
 return angles

def tree_collisions(features):
 blockers=Index([f for f in features if f['kind'] in ('roads','buildings','driveways','rail')]); failed=[]
 radius=FT(option('world.tree_radius_ft'))
 for f in features:
  if f['kind']!='trees': continue
  x,y,_,_=f['rect']; rect=(x-radius,y-radius,2*radius,2*radius)
  if any(intersects(rect,b['bounds']) for b in blockers.candidates(rect)): failed.append(f['id'])
 return failed

def drainage_topology_checks(streams,selected=None):
 """Validate canonical receivers, including confluences at interior vertices.

 A query halo may omit downstream reaches of unselected boundary features;
 only selected receivers must exist, but every visible graph cycle is invalid.
 """
 by_id={f['id']:f for f in streams}
 selected=streams if selected is None else selected
 dead=[]; cycles=[]
 close=lambda a,b:math.dist(a,b)<.001
 for f in selected:
  receiver=by_id.get(f['downstream'])
  if receiver is None or not any(close(f['points'][-1],p) for p in receiver['points']):
   dead.append(f['id'])
  if not f['source'] and not any(close(g['points'][-1],f['points'][0]) for g in streams if g['id']!=f['id']):
   dead.append(f['id']+':upstream')
 # Iterative traversal avoids recursion limits on long sampled main channels.
 done=set()
 for start in by_id:
  path={}; node=start
  while node in by_id and node not in done:
   if node in path:
    cycles.append(node); break
   path[node]=True; node=by_id[node]['downstream']
  done.update(path)
 return {'river_continuity':{'pass':not dead,'failures':dead[:10],'examined':len(selected)},
         'river_acyclic':{'pass':not cycles,'failures':cycles[:10]}}


def river_checks(seed,rect):
 from world import rivers,elevation,crossing_point,basin,_drainage_options
 selected=list(rivers(seed,rect)); streams=list(selected); by_id={f['id']:f for f in streams}; options=_drainage_options()
 for f in selected:
  cx,cy=f.get('basin',(0,0))
  for related in basin(seed,cx,cy,options):
   if related['id'] not in by_id: streams.append(related); by_id[related['id']]=related
 pending={f['downstream'] for f in selected}-by_id.keys()
 # Resolve only the bounded receiver closure needed for continuity and cycles.
 for _ in range(4):
  if not pending: break
  requested=set(pending); pending.clear()
  for ident in requested:
   parts=ident.split(':')
   if len(parts)<4 or parts[0]!='river': continue
   try:
    cx,cy=(int(parts[2]),int(parts[3])) if parts[1]=='outlet' else (int(parts[1]),int(parts[2]))
   except ValueError: continue
   for f in basin(seed,cx,cy,options):
    if f['id'] not in by_id:
     streams.append(f); by_id[f['id']]=f
    if f['id'] in requested: pending.add(f['downstream'])
   pending.discard(ident)
 uphill=[]; crossings=[]
 close=lambda a,b:math.dist(a,b)<.001
 for f in selected:
  # Elevation is a node potential. Meander samples can rise and fall within one
  # reach; every reach endpoint must still descend to its downstream node.
  if elevation(seed,*f['points'][-1])>elevation(seed,*f['points'][0])+1e-7: uphill.append(f['id'])
 from river_geometry import finished_crossings
 crossings=finished_crossings(seed,selected,by_id)
 return {**drainage_topology_checks(streams,selected),'rivers_downhill':{'pass':not uphill,'failures':uphill[:10]},'no_river_crossings':{'pass':not crossings,'failures':crossings[:10]}}

def river_length_in_rect(streams,rect):
 """Measure finished channel geometry clipped to the requested map window."""
 x,y,w,h=rect; total=0.0
 for f in streams:
  if f['kind']!='rivers' or not intersects(f['bounds'],rect): continue
  for a,b in zip(f['points'],f['points'][1:]):
   dx,dy=b[0]-a[0],b[1]-a[1]; lo,hi=0.0,1.0
   for p,q in ((-dx,a[0]-x),(dx,x+w-a[0]),(-dy,a[1]-y),(dy,y+h-a[1])):
    if p==0:
     if q<0: lo,hi=1,0; break
     continue
    t=q/p
    if p<0: lo=max(lo,t)
    else: hi=min(hi,t)
   if hi>lo: total+=math.hypot(dx,dy)*(hi-lo)
 return total/MILE

def river_geometry_metrics(seed,rect):
 """Measurements from the canonical, finished river features in a window."""
 from world import rivers
 streams={f['id']:f for f in rivers(seed,rect)}; streams=list(streams.values())
 lengths=[]; bearings=[]; bins=[0]*16; ewest=0; bearing_total=0; source_curves=[]
 for f in streams:
  p=f['points']; straight=math.dist(p[0],p[-1]); length=sum(math.dist(a,b) for a,b in zip(p,p[1:]))
  if straight>0: lengths.append(length/straight)
  if f.get('stream_order',3)<3:
   angles=[math.atan2(b[0]-a[0],b[1]-a[1]) for a,b in zip(p,p[1:]) if _seg_rect(a,b,rect)]
  else: angles=[math.atan2(p[-1][0]-p[0][0],p[-1][1]-p[0][1])] if any(_seg_rect(a,b,rect) for a,b in zip(p,p[1:])) else []
  for angle in angles:
   bearings.append(angle); bearing_total+=1
   b=int((angle+math.pi)/(2*math.pi)*16)%16; bins[b]+=1
   if abs(abs(angle)-math.pi/2)<=math.radians(15): ewest+=1
  if f.get('source') and f.get('stream_order',99)<=2: source_curves.append(f)
 entropy=-sum((n/bearing_total)*math.log(n/bearing_total,2) for n in bins if n)/4 if bearing_total else 0
 basin_cells={tuple(f.get('basin',(0,0))) for f in streams}
 from world import basin,_drainage_options
 options=_drainage_options(); full_features={}
 for cell in basin_cells:
  for f in basin(seed,*cell,options): full_features[f['id']]=f
 network=list(full_features.values())
 by_id={f['id']:f for f in network}; join_groups={}
 x0,y0,w0,h0=rect
 for f in network:
  if f.get('downstream') in by_id: join_groups.setdefault((f['downstream'],tuple(f['points'][-1])),[]).append(f)
 joins=[]; angle_bins=[0]*10; below=0; width_violations=[]
 for (rid,point),group in join_groups.items():
  if not (x0<=point[0]<=x0+w0 and y0<=point[1]<=y0+h0): continue
  receiver=by_id[rid]
  idx=min(range(len(receiver['points'])),key=lambda i:math.dist(receiver['points'][i],point))
  receiver_width=receiver.get('widths',[receiver['width']]*len(receiver['points']))[idx]
  upstream=sorted((f.get('widths',[f['width']])[-1] for f in group),reverse=True)
  if rid.startswith('river:outlet:'):
   profile=receiver.get('widths',[receiver['width']]*len(receiver['points']))
   upstream.append(profile[max(0,idx-1)]); upstream.sort(reverse=True)
  if len(upstream)>=2:
   low=max(upstream); high=1.05*math.sqrt(upstream[0]**2+upstream[1]**2)
   if not low-1e-6<=receiver_width<=high+1e-6: width_violations.append((rid,low,receiver_width,high))
 # Measure the finished paths over real arc length, including interior contacts.
 from river_geometry import junction_metrics
 junctions=junction_metrics(streams,seed,rect)
 joins=[r['0.25'] for r in junctions['joins'] if r['connection']=='confluence']
 angle_bins=[sum(i<=angle<i+10 for angle in joins) for i in range(0,181,10)]
 below=sum(angle<25 for angle in joins)
 # Unit-normalized turning-function signatures; remove starting bearing and
 # compare source reaches at 32 equal arc-length stations.
 signatures=[]
 for f in source_curves:
  p=f['points']; total=sum(math.dist(a,b) for a,b in zip(p,p[1:])); turns=[]
  headings=[math.atan2(b[1]-a[1],b[0]-a[0]) for a,b in zip(p,p[1:])]
  for i in range(32):
   j=min(len(headings)-1,int(i/32*len(headings)))
   turns.append((headings[j]-headings[0]+math.pi)%(2*math.pi)-math.pi)
  signatures.append(turns)
 close=0; pairs=0; eps=.02; copy_distances=[]
 for i,a in enumerate(signatures):
  for b in signatures[i+1:]:
   pairs+=1; d=math.sqrt(sum((x-y)**2 for x,y in zip(a,b))/len(a))
   copy_distances.append(d); close+=d<eps
 ordered=sorted(lengths)
 p10=ordered[round(.10*(len(ordered)-1))] if ordered else 0; p90=ordered[round(.90*(len(ordered)-1))] if ordered else 0
 from collections import Counter
 order_counts=dict(Counter(f.get('stream_order',0) for f in streams))
 basin_orders=defaultdict(Counter); basin_trunks=[]
 for cell in basin_cells:
  for f in network:
   if tuple(f.get('basin',(0,0)))==cell:
    basin_orders[cell][f.get('stream_order',0)]+=1
    if f['id'].startswith('river:outlet:'): basin_trunks.append(f['width']/FT(1))
 order_counts_by_basin={f'{cx}:{cy}':dict(counts) for (cx,cy),counts in sorted(basin_orders.items())}
 bifurcation_by_basin={cell:sum(v for k,v in counts.items() if int(k)>=2)/sum(v for k,v in counts.items() if int(k)>=3)
  for cell,counts in order_counts_by_basin.items() if sum(v for k,v in counts.items() if int(k)>=3)}
 order3_order4_by_basin={cell:counts[3]/counts[4] for cell,counts in order_counts_by_basin.items() if counts.get(3,0) and counts.get(4,0)}
 return {'streams':len(streams),'density':river_length_in_rect(streams,rect)/(rect[2]*rect[3]/MILE**2),
  'sinuosity_values':lengths,
  'sinuosity_median':statistics.median(lengths) if lengths else 0,'sinuosity_p10':p10,'sinuosity_p90':p90,'sinuosity_spread':p90-p10,'bearing_entropy':entropy,
  'ewest_fraction':ewest/bearing_total if bearing_total else 0,'order1_width_ft':[f['width']/FT(1) for f in streams if f.get('stream_order')==1],
  'trunk_width_ft':basin_trunks,'order_counts':order_counts,'width_rule_violations':width_violations,
  'junction_01_metrics':junctions['0.1'],'junction_025_metrics':junctions['0.25'],
  'junction_continuations':junctions['continuations'],'junction_unfiltered':junctions['unfiltered'],
  'junction_angles':joins,'junction_mean':statistics.mean(joins) if joins else 0,
  'junction_sd':statistics.pstdev(joins) if joins else 0,'junction_below25_fraction':below/len(joins) if joins else 0,
  'junction_bin_max_fraction':max(angle_bins)/len(joins) if joins else 0,'copy_fraction':close/pairs if pairs else 0,'copy_close':close,'copy_pairs':pairs,'copy_epsilon':eps,'copy_distances':copy_distances,
  'order_counts_by_basin':order_counts_by_basin,'bifurcation_ratio_by_basin':bifurcation_by_basin,
  'order3_order4_ratio_by_basin':order3_order4_by_basin}

def paved_road_gaps(features,rect):
 # Inspect canonical route geometry, including missing whole route sections.
 from world import paved_routes
 present={f['id']:f for f in features if f['kind']=='roads'}; gaps=[]
 # A scene carries its seed on route features; legacy synthetic scenes default 31.
 seed=next((f['seed'] for f in features if 'seed' in f),31)
 for expected in paved_routes(seed,rect):
  if not intersects(expected['bounds'],rect): continue
  f=present.get(expected['id'])
  if f is None or f['surface']!='paved' or f.get('points')!=expected['points']: gaps.append(expected['id'])
 # Survey sections are retained as addressable road edges as well as routes.
 from world import paved_edge,correction_shift
 x,y,w,h=rect
 for axis in ('h','v'):
  c0,c1,s0,s1=(y,y+h,x,x+w) if axis=='h' else (x,x+w,y,y+h)
  for line in range(math.ceil(c0/MILE),math.floor(c1/MILE)+1):
   for segment in range(math.floor(s0/MILE),math.ceil(s1/MILE)):
    if not paved_edge(seed,axis,line,segment): continue
    coordinate=line*MILE+(correction_shift(seed,math.floor(segment/24)) if axis=='v' else 0)
    if not c0<=coordinate<=c1: continue
    id=f'road:{axis}:{line}:{segment}'
    # Pure generator inputs used to verify route ownership have no survey edges.
    if not any(k.startswith('road:') for k in present): continue
    if id not in present or present[id]['surface']!='paved': gaps.append(id)
 return gaps


def run(seed=31,settings=None,rect=None):
 settings=settings or Settings(); rect=rect or (0,0,3*MILE,3*MILE)
 with applied(settings):
  a=window(seed,rect,settings); again=window(seed,rect,settings)
  shifted=(rect[0]+rect[2]/2,rect[1],rect[2],rect[3]); b=window(seed,shifted,settings)
  common=(shifted[0],rect[1],rect[2]/2,rect[3])
  project=lambda s:{f['id']:f for f in s['features'] if intersects(f['bounds'],common)}
  halo=(rect[0]-MILE,rect[1]-MILE,rect[2]+2*MILE,rect[3]+2*MILE)
  expanded=window(seed,halo,settings); features=expanded['features']
  collisions=building_collisions(features); orphans=orphan_roads(features); trees=tree_collisions(features)
  gaps=paved_road_gaps(features,rect)
  angles=crossing_angles(features)
  region=make_region(seed); local=[]
  if region['rail']:
   rails=[{'kind':'rail','id':'region-rail','points':region['rail']['pts'],'bounds':(0,0,3*MILE,3*MILE)}]
   roads=[dict(kind='roads',id=r['id'],rect=road_rect(r),bounds=road_rect(r),axis=r['axis']) for r in region['roads']]
   local=crossing_angles(rails+roads)
  local_roads=[dict(kind='roads',id='region:'+r['id'],rect=road_rect(r),bounds=road_rect(r),axis=r['axis']) for r in region['roads']]
  orphans.extend(orphan_roads(local_roads))
  all_angles=angles+local
  near=sum(a>=90-option('verify.square_deviation_deg') for a in all_angles)/len(all_angles) if all_angles else 1
  checks={
   'paved_roads_no_gaps':{'pass':not gaps,'failures':gaps[:10]},
   'determinism':{'pass':json.dumps(a,sort_keys=True)==json.dumps(again,sort_keys=True)},
   'region_determinism':{'pass':json.dumps(region,sort_keys=True)==json.dumps(make_region(seed),sort_keys=True)},
   'matching_overlaps':{'pass':project(a)==project(b)},
   'no_building_overlaps':{'pass':not collisions,'failures':collisions[:10],'count':len(collisions),'examined':sum(f['kind']=='buildings' for f in features)},
   'no_orphan_roads':{'pass':not orphans,'failures':orphans[:10],'count':len(orphans),'examined':sum(f['kind']=='roads' for f in features)},
   'rail_crossings':{'pass':all(a>=option('verify.minimum_angle_deg') for a in all_angles) and near>=option('verify.square_share'),'count':len(all_angles),'minimum_angle':min(all_angles,default=None),'near_square_share':near},
   'no_tree_on_geometry':{'pass':not trees,'failures':trees[:10],'count':len(trees),'examined':sum(f['kind']=='trees' for f in features)},
  }
  checks.update(river_checks(seed,rect))
  return {'seed':seed,'pass':all(v['pass'] for v in checks.values()),'checks':checks}
