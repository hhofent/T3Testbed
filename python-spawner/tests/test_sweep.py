import csv,pytest
from settings import Settings
from sweep import sweep

def test_sweep_is_reproducible_and_records_clamped_values(tmp_path):
 a,b=tmp_path/'a.csv',tmp_path/'b.csv'
 args=(Settings(),'region.rail_pct',-100,200,2,2)
 sweep(*args,a,first_seed=13); sweep(*args,b,first_seed=13)
 assert a.read_bytes()==b.read_bytes()
 rows=list(csv.DictReader(a.open()))
 assert len(rows)==4 and {r['value'] for r in rows}=={'0','100'}
 assert [r['seed'] for r in rows]==['13','14','13','14']
 assert all(int(r['houses'])<=int(r['planned_houses']) for r in rows)
 assert {'trailer_share','vacancy','businesses','road_miles'}<=set(rows[0])
 with pytest.raises(ValueError): sweep(Settings(),'region.rail_pct',0,1,0,1,a)
