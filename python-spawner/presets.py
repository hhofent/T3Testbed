"""Portable JSON settings; type restoration follows the settings schema."""
import json
from pathlib import Path
from settings import Settings,DEFAULTS

from settings import PRESET_VALUES as PRESETS

def plain(value):
 if isinstance(value,range): return {'start':value.start,'stop':value.stop,'step':value.step}
 if isinstance(value,dict): return {str(k):plain(v) for k,v in value.items()}
 if isinstance(value,(tuple,list,set)): return [plain(v) for v in (sorted(value) if isinstance(value,set) else value)]
 return value

def restore(value,default):
 if isinstance(default,dict):
  if not isinstance(value,dict) or set(value)!=set(str(k) for k in default): raise ValueError('Invalid object keys')
  return {k:restore(value[str(k)],v) for k,v in default.items()}
 if isinstance(default,range):
  if not isinstance(value,dict) or set(value)!={'start','stop','step'}: raise ValueError('Invalid range')
  return range(value['start'],value['stop'],value['step'])
 if isinstance(default,(tuple,list)):
  if not isinstance(value,list) or len(value)!=len(default): raise ValueError('Invalid sequence')
  return type(default)(restore(v,d) for v,d in zip(value,default))
 if isinstance(default,set): return set(value)
 return value

def diff(settings,base=None):
 base=base or Settings()
 return {k:{'default':plain(base.values[k]),'value':plain(v)} for k,v in settings.values.items() if v!=base.values[k]}

def document(settings):
 return {'format':'python-spawner-settings','version':1,'values':{k:v['value'] for k,v in diff(settings).items()}}

def apply_document(settings,data):
 if data.get('format')!='python-spawner-settings' or data.get('version')!=1 or not isinstance(data.get('values'),dict): raise ValueError('Unsupported preset document')
 candidate=Settings(settings.values)
 for key,value in data['values'].items():
  if key not in DEFAULTS: raise ValueError('Unknown setting: '+key)
  candidate.set(key,restore(value,DEFAULTS[key]))
 settings.values=candidate.values
 return settings

def save(settings,path): Path(path).write_text(json.dumps(document(settings),indent=2,sort_keys=True)+'\n',encoding='utf-8')
def load(settings,path): return apply_document(settings,json.loads(Path(path).read_text(encoding='utf-8')))
def stack(names=(),overrides=()):
 settings=Settings()
 for name in names:
  if name in PRESETS:
   for key,value in PRESETS[name].items(): settings.set(key,value)
  else: load(settings,name)
 for item in overrides:
  key,sep,value=item.partition('=')
  if not sep: raise ValueError('Expected a.b=value: '+item)
  settings.set(key,json.loads(value))
 return settings
