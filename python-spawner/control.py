"""Loopback-only control page, served with the Python standard library."""
import base64,json
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
from pathlib import Path
from settings import Settings,CATALOG,applied
from presets import PRESETS,plain,apply_document,document,diff,stack
from region import make_region,spawn_region
from sweep import statistics

PAGE=Path(__file__).with_name('control.html')
def schema(settings=None):
 return {'schema':plain(CATALOG),'values':plain((settings or Settings()).values),'defaults':plain(Settings().values),'presets':list(PRESETS),'units_per_mile':__import__('region').MILE}

def render_request(data):
 settings=apply_document(Settings(),data['settings']); seed=int(data['seed'])
 with applied(settings):
  if data.get('world'):
   from world import window,svg
   rect=data['window']
   if len(rect)!=4 or max(rect[2:])>CATALOG['world.window_miles']['max']*__import__('region').MILE: raise ValueError('Window exceeds control-page zoom range')
   scene=window(seed,rect,settings)
   counts={k:sum(f['kind']==k for f in scene['features']) for k in sorted({f['kind'] for f in scene['features']})}
   return {'image':base64.b64encode(svg(scene)).decode(),'image_type':'image/svg+xml','stats':counts,'changed':diff(settings),'settings':document(settings)}
  region=make_region(seed)
  results=spawn_region(region) if settings.values['stages.houses'] else {}
  from map_render import png
  image=png(region,results,town_only=data.get('town',True))
 return {'image':base64.b64encode(image).decode(),'stats':statistics(region,results),'changed':diff(settings),'settings':document(settings)}

class Handler(BaseHTTPRequestHandler):
 def log_message(self,*args): pass
 def reply(self,status,data,content_type='application/json'):
  body=data if isinstance(data,bytes) else json.dumps(data).encode()
  self.send_response(status); self.send_header('Content-Type',content_type); self.send_header('Content-Length',str(len(body))); self.send_header('Cache-Control','no-store'); self.end_headers()
  try: self.wfile.write(body)
  except (BrokenPipeError,ConnectionResetError): pass
 def do_GET(self):
  if self.path=='/': self.reply(200,PAGE.read_bytes(),'text/html; charset=utf-8')
  elif self.path=='/schema': self.reply(200,schema(getattr(self.server,"settings",None)))
  elif self.path.startswith('/preset/'):
   name=self.path.removeprefix('/preset/')
   if name not in PRESETS: self.reply(404,{'error':'Unknown preset'}); return
   self.reply(200,document(stack([name])))
  else: self.reply(404,{'error':'Not found'})
 def do_POST(self):
  if self.path not in ('/render','/chunks'): self.reply(404,{'error':'Not found'}); return
  try:
   length=int(self.headers.get('Content-Length','0'))
   if length<1 or length>Settings().values['controls.max_request_bytes']: raise ValueError('Invalid request size')
   data=json.loads(self.rfile.read(length))
   if self.path=='/chunks':
    from chunks import request
    result=request(data,apply_document(Settings(),data['settings']))
   else: result=render_request(data)
   self.reply(200,result)
  except (ValueError,KeyError,TypeError,ArithmeticError,IndexError) as error: self.reply(400,{'error':str(error)})

def serve(port=None,settings=None):
 port=port or Settings().values['controls.port']
 with ThreadingHTTPServer(('127.0.0.1',port),Handler) as server:
  server.settings=settings or Settings()
  print(f'Control page: http://127.0.0.1:{server.server_port}',flush=True)
  try: server.serve_forever()
  except KeyboardInterrupt: pass
