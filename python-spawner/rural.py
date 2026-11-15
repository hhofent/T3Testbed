"""World-only rural site variation; reference parity plans are left untouched."""
import math
from spawner import H,FT
from settings import option

TYPES=('machine shed','barn','grain bin','corn crib','hog building','poultry house','quonset')
def layout(seed,spec,plan,access_fraction=None,place=None):
 W,D=spec['width'],spec['depth']; salt=spec['id']
 def rand(tag): return H(seed,'rural',salt,tag)%10000/10000
 rooms=[r.rect() for r in plan.rooms]
 x0=min(r[0] for r in rooms); y0=min(r[1] for r in rooms)
 hw=max(r[0]+r[2] for r in rooms)-x0; hd=max(r[1]+r[3] for r in rooms)-y0
 margin=FT(20); lane=FT(option('rural.lane_width_ft'))
 hx=margin+lane+(max(0,W-hw-3*margin-lane))*rand('house-x')
 bend=rand('bend')*100<option('rural.bent_lanes_pct')
 entrance=min(W-margin-lane,margin+lane+rand('entrance')*W*.3) if bend else margin
 hy=margin+max(0,min(D*.3,D-hd-2*margin)-margin)*rand('house-y')
 if access_fraction is not None:
  lateral=abs(entrance-margin)+abs(hx-margin-lane/2)
  lo=max(margin,FT(250)-lateral-hd/2)
  hi=min(D-hd-margin,FT(830)-lateral-hd/2)
  hy=lo+max(0,hi-lo)*access_fraction
 # Keep the house out of the N/W windbreak belt so rows can form (world N/W, not region.grove_sides).
 if not option('generation.parity') and place is not None and spec['kind']=='farmstead':
  from rural_trees import world_nw_local_sides
  north,west=world_nw_local_sides(place)
  belt=FT(6)+3*FT(option('rural.windbreak_row_ft'))+FT(option('rural.windbreak_clear_ft'))
  def clear_house(side):
   nonlocal hx,hy
   if side=='front': hy=max(hy,belt)
   elif side=='rear': hy=min(hy,D-hd-belt)
   elif side=='left': hx=max(hx,belt)
   else: hx=min(hx,W-hw-belt)
  clear_house(north); clear_house(west)
  hx=max(margin,min(hx,W-hw-margin)); hy=max(margin,min(hy,D-hd-margin))
 dx,dy=hx-x0,hy-y0
 buildings=[]; drives=[]; trees=[]
 count=round(option('rural.outbuildings_min')+rand('count')*(option('rural.outbuildings_max')-option('rural.outbuildings_min')))
 if spec['kind']=='acreage': count=min(count,1+int(rand('acreage-count')*3))
 rear=hy+hd+FT(35); gap=FT(option('rural.building_gap_ft')); cellw=FT(100); cellh=FT(120)
 cols=max(1,int((W-2*margin)//cellw))
 for i in range(count):
  typ=TYPES[H(seed,'rural-type',salt,i)%len(TYPES)]
  w=FT(18+int(rand(f'w{i}')*48)); h=w if typ=='grain bin' else FT(28+int(rand(f'h{i}')*55))
  x=margin+(i%cols)*cellw+gap; y=rear+(i//cols)*cellh+gap
  if x+w>W-margin or y+h>D-margin: continue
  buildings.append((typ,(x,y,w,h)))
  # Connected apron and access spur, with a separate front-house connection.
  drives.extend([(margin,y-gap,max(lane,x+w-margin),lane),(x,y-gap,lane,gap+lane)])
 if not option('generation.parity') and place is not None and spec['kind']=='farmstead':
  from rural_trees import shift_buildings_from_belts
  buildings=shift_buildings_from_belts(seed,spec,place,buildings,(hx,hy,hw,hd))
  # Rebuild aprons after any belt shove so drives still touch the buildings.
  drives=[]
  for _,(x,y,w,h) in buildings:
   drives.extend([(margin,y-gap,max(lane,x+w-margin),lane),(x,y-gap,lane,gap+lane)])
 end=max([hy+hd]+[r[1]+r[3] for _,r in buildings])
 turn=max(lane,hy*.45)
 entry_index=len(drives)
 drives.extend([(entrance,0,lane,turn+lane),(min(entrance,margin),turn,abs(entrance-margin)+lane,lane),(margin,turn,lane,max(lane,end-turn)),(margin,hy+hd/2,hx-margin+lane,lane)])
 chamfer=min(W,D)*option('rural.lot_corner_fraction')*rand('shape')
 shape=[(0,chamfer),(chamfer,0),(W-chamfer,0),(W,chamfer),(W,D-chamfer),(W-chamfer,D),(chamfer,D),(0,D-chamfer)]
 if option('stages.trees'):
  if not option('generation.parity'):
   site=dict(offset=(dx,dy),buildings=buildings,driveways=drives,shape=shape)
   from rural_trees import rural_markers
   trees=rural_markers(seed,spec,plan,site,place)
  else:
   side=H(seed,'grove-side',salt)%3
   spacing=FT(option('trees.spacing_ft')); density=option('rural.grove_density')
   length=D if side<2 else W
   for i in range(max(1,int(length/spacing))):
    if rand(f'tree{i}')>density: continue
    along=(i+.3+rand(f'j{i}')*.4)*spacing
    inset=FT(5+rand(f'inset{i}')*25)
    trees.append((inset,along) if side==0 else (W-inset,along) if side==1 else (along,D-inset))
 access=[(entrance+lane/2,0),(entrance+lane/2,turn+lane/2),(margin+lane/2,turn+lane/2),(margin+lane/2,hy+hd/2),(hx,hy+hd/2)]
 return dict(access=access,access_length=sum(math.dist(a,b) for a,b in zip(access,access[1:])),entry_index=entry_index,offset=(dx,dy),buildings=buildings,driveways=drives,trees=trees,shape=shape)
