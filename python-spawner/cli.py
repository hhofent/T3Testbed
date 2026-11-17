"""Command line entry point for generation, presets, sweeps and the control page."""
import argparse,json,secrets
from pathlib import Path
from presets import stack,save,diff,PRESETS
from settings import DEFAULTS,CATALOG
ROOT=Path(__file__).resolve().parent

def parser():
 p=argparse.ArgumentParser(description=__doc__)
 p.add_argument('--seed',type=int,help='Reproduce a seed (default: choose a random seed at launch)')
 p.add_argument('--serve',action='store_true')
 p.add_argument('--world',action='store_true')
 p.add_argument('--verify',action='store_true')
 p.add_argument('--window',nargs=4,type=float,default=[0,0,DEFAULTS['world.window_miles'],DEFAULTS['world.window_miles']],metavar=('X_MI','Y_MI','W_MI','H_MI'))
 p.add_argument('--port',type=int)
 p.add_argument('--preset',action='append',default=[],help='Stack a named preset or JSON path')
 p.add_argument('--param',action='append',default=[],metavar='a.b=value')
 p.add_argument('--save-preset'); p.add_argument('--diff',action='store_true')
 p.add_argument('--output',default=str(ROOT/'out'/'region.json'))
 p.add_argument('--png',help='Optional map PNG path')
 p.add_argument('--sweep',nargs=4,metavar=('PARAM','LO','HI','STEPS'))
 p.add_argument('--seeds',type=int,default=1)
 p.add_argument('--csv',default=str(ROOT/'out'/'sweep.csv'))
 return p

def main(argv=None):
 p=parser(); args=p.parse_args(argv)
 if args.seeds<1: p.error("--seeds must be positive")
 try: settings=stack(args.preset,args.param)
 except (ValueError,KeyError,OSError) as error: p.error(str(error))
 if args.seed is None:
  configured=any(item.partition('=')[0]=='controls.seed' for item in args.param) or settings.values['controls.seed']!=DEFAULTS['controls.seed']
  if not configured:
   for name in args.preset:
    values=PRESETS[name] if name in PRESETS else json.loads(Path(name).read_text(encoding='utf-8'))['values']
    if 'controls.seed' in values:configured=True;break
  args.seed=settings.values['controls.seed'] if configured else secrets.randbelow(CATALOG['controls.seed']['max']+1)
 try: settings.set('controls.seed',args.seed)
 except ValueError as error:p.error(str(error))
 if args.save_preset: save(settings,args.save_preset)
 if args.diff: print(json.dumps(diff(settings),indent=2)); return
 if args.verify:
  from verification import run
  from world import MILE
  reports=[run(seed,settings,[v*MILE for v in args.window]) for seed in range(args.seed,args.seed+args.seeds)]
  print(json.dumps(reports,indent=2))
  if not all(r['pass'] for r in reports): raise SystemExit(1)
  return
 if args.serve:
  from control import serve
  print(f'Seed: {args.seed}',flush=True)
  serve(args.port,settings); return
 if args.sweep:
  from sweep import sweep
  key,lo,hi,steps=args.sweep
  try: output=sweep(settings,key,float(lo),float(hi),int(steps),args.seeds,args.csv,args.seed)
  except (ValueError,KeyError) as error: p.error(str(error))
  print(output); return
 from region import make_region,spawn_region
 if args.world:
  from world import window,svg,MILE
  from settings import applied
  scene=window(args.seed,[v*MILE for v in args.window],settings,detail=max(args.window[2:])<=2)
  path=Path(args.output); path.parent.mkdir(parents=True,exist_ok=True); path.write_text(json.dumps(scene,sort_keys=True)+'\n')
  with applied(settings): path.with_suffix('.svg').write_bytes(svg(scene))
  if args.png:
   from map_render import world_png
   png_path=Path(args.png); png_path.parent.mkdir(parents=True,exist_ok=True)
   with applied(settings): png_path.write_bytes(world_png(scene))
  print(path); return
 region=make_region(args.seed,settings=settings)
 path=Path(args.output); path.parent.mkdir(parents=True,exist_ok=True)
 path.write_text(json.dumps(region,sort_keys=True,indent=1)+'\n',encoding='utf-8')
 if args.png:
  from map_render import png
  path=Path(args.png); path.parent.mkdir(parents=True,exist_ok=True)
  from settings import applied
  with applied(settings): path.write_bytes(png(region,spawn_region(region,settings=settings),framed=True))
 print(args.output)
if __name__=='__main__': main()
