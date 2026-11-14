import io
from PIL import Image,ImageChops
from cli import main

def test_cli_world_png_and_region_framing(tmp_path,monkeypatch):
 import world
 from world import feature,MILE,FT
 def smoke_window(seed,rect,settings=None,detail=True):
  x,y,w,h=rect
  return {'seed':seed,'window':rect,'features':[feature('rivers','smoke-diagonal',points=[(x,y),(x+w,y+h)],width=FT(100),stream_order=3)]}
 monkeypatch.setattr(world,'window',smoke_window)
 for world in (False,True):
  png=tmp_path/('world.png' if world else 'region.png')
  args=['--seed','31','--output',str(tmp_path/'scene.json'),'--png',str(png)]
  if world: args+=['--world','--window','-1','-1','6','3']
  main(args)
  image=Image.open(png).convert('RGB')
  background=Image.new('RGB',image.size,image.getpixel((0,0)))
  bounds=ImageChops.difference(image,background).getbbox()
  assert bounds and bounds[2]-bounds[0]>.8*image.width and bounds[3]-bounds[1]>.8*image.height
  if world: assert image.size==(1440,720)
