import json,pytest
from settings import Settings,CATALOG
from presets import stack,save,load,diff

def test_preset_round_trip_all_reference_types(tmp_path):
 settings=stack(['tiny-towns','rail-heavy','trailer-heavy'],['region.acreage_pct=999'])
 assert settings.values['region.acreage_pct']==100
 path=tmp_path/'preset.json'; save(settings,path)
 restored=load(Settings(),path)
 assert restored.values==settings.values
 assert diff(restored)==diff(settings)
 assert stack(['big-towns']).values['region.town_size']==[('tiny',0),('mid',0),('big',100)]

def test_bad_preset_is_atomic(tmp_path):
 path=tmp_path/'bad.json'; path.write_text(json.dumps({'format':'python-spawner-settings','version':1,'values':{'region.rail_pct':0,'no.such':1}}))
 settings=Settings()
 with pytest.raises(ValueError): load(settings,path)
 assert settings.values==Settings().values

def test_nested_finite_clamping():
 key='region.make_region_numbers'; leaf=next(k for k,v in Settings().values[key].items() if v==400)
 settings=stack(overrides=[key+'.'+leaf+'=999999'])
 assert settings.values[key][leaf]==CATALOG[key]['max'][leaf]
 with pytest.raises(ValueError): Settings({'region.rail_pct':float('nan')})


def test_tuple_and_range_leaf_overrides():
 settings=stack(overrides=['region.trailer_run.0=3','region.block_cx.start=400'])
 assert settings.values['region.trailer_run'][0]==3
 assert settings.values['region.block_cx'].start==400
 assert isinstance(settings.values['region.block_cx'],range)
