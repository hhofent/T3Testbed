"""Immutable, disposable display snapshots of work in progress."""
import copy
from html import escape
from world import svg,intersects

class Preview:
 def __init__(self):
  self.percent=0;self.stage='Planning basins';self.version=0
  self.features={};self.basins=[];self.basin_index=0;self.basin_total=1

 def update(self,stage,fraction=None,features=None,basins=None,basin_index=None,basin_total=None,basin_fraction=None,**unused):
  if basin_index is not None:self.basin_index=basin_index
  if basin_total is not None:self.basin_total=max(1,basin_total)
  if basin_fraction is not None:fraction=.05+.68*(self.basin_index+basin_fraction)/self.basin_total
  self.stage=stage
  if fraction is not None:self.percent=max(self.percent,min(99,round(fraction*100)))
  if features is not None:
   self.features.update((f['id'],copy.deepcopy(f)) for f in features)
  if basins is not None:self.basins=copy.deepcopy(basins)
  self.version+=1

 def svg(self,rect):
  x,y,w,h=rect
  scene=dict(window=rect,features=[f for f in self.features.values() if intersects(f['bounds'],rect)])
  body=svg(scene,local=True).decode()
  outlines=[]
  for i,polygon in enumerate(self.basins):
   points=' '.join(f'{a-x},{b-y}' for a,b in polygon)
   color=('#dce7cf','#e7dfcb','#d7e4e1')[i%3]
   outlines.append(f'<g data-kind="basins"><title>Temporary basin boundary</title><polygon points="{escape(points)}" fill="{color}" fill-opacity="0.45" stroke="#829784" stroke-width="1" stroke-dasharray="5 4" vector-effect="non-scaling-stroke"/></g>')
  # Insert beneath every generated layer; canonical geometry stays untouched.
  end=body.index('/>')+2
  return (body[:end]+''.join(outlines)+body[end:]).encode()
