"""World-only rural site variation; reference parity plans are left untouched."""
import math
from spawner import H,FT
from settings import option

TYPES=('machine shed','barn','grain bin','corn crib','hog building','poultry house','quonset')
def layout(seed,spec,plan):
 W,D=spec['width'],spec['depth']; salt=spec['id']
 def rand(tag): return H(seed,'rural',salt,tag)%10000/10000
 rooms=[r.rect() for r in plan.rooms]
 x0=min(r[0] for r in rooms); y0=min(r[1] for r in rooms)
 hw=max(r[0]+r[2] for r in rooms)-x0; hd=max(r[1]+r[3] for r in rooms)-y0
 margin=FT(20); lane=FT(option('rural.lane_width_ft'))
 hx=margin+lane+(max(0,W-hw-3*margin-lane))*rand('house-x')
 hy=margin+max(0,min(D*.3,D-hd-2*margin)-margin)*rand('house-y')
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
 end=max([hy+hd]+[r[1]+r[3] for _,r in buildings]); bend=rand('bend')*100<option('rural.bent_lanes_pct')
 entrance=min(W-margin-lane,margin+lane+rand('entrance')*W*.3) if bend else margin
 turn=max(lane,hy*.45)
 drives.extend([(entrance,0,lane,turn+lane),(min(entrance,margin),turn,abs(entrance-margin)+lane,lane),(margin,turn,lane,max(lane,end-turn)),(margin,hy+hd/2,hx-margin+lane,lane)])
 if option('stages.trees'):
  side=H(seed,'grove-side',salt)%3
  spacing=FT(option('trees.spacing_ft')); density=option('rural.grove_density')
  length=D if side<2 else W
  for i in range(max(1,int(length/spacing))):
   if rand(f'tree{i}')>density: continue
   along=(i+.3+rand(f'j{i}')*.4)*spacing
   inset=FT(5+rand(f'inset{i}')*25)
   trees.append((inset,along) if side==0 else (W-inset,along) if side==1 else (along,D-inset))
 chamfer=min(W,D)*option('rural.lot_corner_fraction')*rand('shape')
 shape=[(0,chamfer),(chamfer,0),(W-chamfer,0),(W,chamfer),(W,D-chamfer),(W-chamfer,D),(chamfer,D),(0,D-chamfer)]
 return dict(offset=(dx,dy),buildings=buildings,driveways=drives,trees=trees,shape=shape)
