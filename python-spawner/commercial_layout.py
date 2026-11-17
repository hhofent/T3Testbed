"""Main-street storefronts with a controlled highway strip (N12)."""
from settings import option
from region import FT,overlaps,rail_hits,_edge_slot,world_rect
from spawner import H

def arrange(reg):
 town=reg['town']
 if not option('stages.businesses'):
  if town: town['commercial']=[]; town['program']=[]; town['unplaced']=[]
  reg['parcels']=[p for p in reg['parcels'] if p['kind'] not in ('fuel','industry','elevator')]
  return
 if not town or option('generation.parity'): return
 # Move selected existing shops; preserve the researched business mix and attached downtown.
 share=option('commercial.downtown_share')
 road=next((r for r in reg['grid_roads'] if r['type']=='paved' and r['axis']=='h'),None)
 if not road: return
 occupied=[town['box']]+[p['rect'] for p in reg['parcels'] if not p.get('in_town')]+[c['lot'] for c in town['commercial'] if c['site'].startswith('edge')]
 occupied += [world_rect(l['place'],l['spec']['width'],l['spec']['depth']) for l in reg['lots'] if ':outer' in l['spec']['id']]
 for shop in town['commercial']:
  if not shop.get('storefront') and shop['type']!='gas station': continue
  if H(reg['seed'],shop['id'],'highway')/2**64 < share: continue
  w,d=shop['lot'][2:]
  bar=([reg['rail']] if reg['rail'] else [])+reg['creeks']
  slot=_edge_slot(town,road,w,d,bar,occupied)
  if not slot: continue
  lot,local,side=slot; old=shop['lot']; dx,dy=lot[0]-old[0],lot[1]-old[1]
  def shift(rect): return (rect[0]+dx,rect[1]+dy,rect[2],rect[3]) if rect else None
  for key in ('bldg','curb','canopy'): shop[key]=shift(shop.get(key))
  for key in ('parking','pumps'): shop[key]=[shift(r) for r in shop.get(key,[])]
  shop['lot']=lot; shop['site']='highway strip'; occupied.append(lot)
  if shop['type']=='gas station':
   for parcel in reg['parcels']:
    if parcel['kind']=='fuel' and parcel.get('in_town'): parcel['rect']=lot
