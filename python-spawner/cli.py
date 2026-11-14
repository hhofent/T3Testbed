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
 p.add_argument('--sweep',nargs=4,metavar=('PARAM','LO','HI','STEPS'))
 p.add_argument('--seeds',type=int,default=1)
 p.add_argument('--csv',default='out/sweep.csv')
 return p

def main(argv=None):
 p=parser(); args=p.parse_args(argv)
 try: settings=stack(args.preset,args.param)
 except (ValueError,KeyError,OSError) as error: p.error(str(error))
 if args.save_preset: save(settings,args.save_preset)
 if args.diff: print(json.dumps(diff(settings),indent=2)); return
 if args.sweep:
  from sweep import sweep
  key,lo,hi,steps=args.sweep
  try: output=sweep(settings,key,float(lo),float(hi),int(steps),args.seeds,args.csv,args.seed)
  except (ValueError,KeyError) as error: p.error(str(error))
  print(output); return
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
