"""Command line entry point for generation, presets, sweeps and the control page."""
import argparse,json
from pathlib import Path
from presets import stack,save,diff

def parser():
 p=argparse.ArgumentParser(description=__doc__)
 p.add_argument('--seed',type=int,default=31)
 p.add_argument('--preset',action='append',default=[],help='Stack a named preset or JSON path')
 p.add_argument('--param',action='append',default=[],metavar='a.b=value')
 p.add_argument('--save-preset'); p.add_argument('--diff',action='store_true')
 p.add_argument('--output',default='out/region.json')
 p.add_argument('--png',help='Optional map PNG path')
 return p

def main(argv=None):
 p=parser(); args=p.parse_args(argv)
 try: settings=stack(args.preset,args.param)
 except (ValueError,KeyError,OSError) as error: p.error(str(error))
 if args.save_preset: save(settings,args.save_preset)
 if args.diff: print(json.dumps(diff(settings),indent=2)); return
 from region import make_region,spawn_region
 region=make_region(args.seed,settings=settings)
 path=Path(args.output); path.parent.mkdir(parents=True,exist_ok=True)
 path.write_text(json.dumps(region,sort_keys=True,indent=1)+'\n',encoding='utf-8')
 if args.png:
  from map_render import png
  path=Path(args.png); path.parent.mkdir(parents=True,exist_ok=True)
  path.write_bytes(png(region,spawn_region(region,settings=settings)))
 print(args.output)
if __name__=='__main__': main()
