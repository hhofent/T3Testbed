"""Regenerate reference/port parity PNGs from isolated source branch modules."""
import pathlib,subprocess,sys,tempfile,pickle,json,hashlib
ROOT=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from settings import Settings
from region import make_region,spawn_region
from map_render import png

def main():
    dest=ROOT/'artifacts'/'parity'; dest.mkdir(parents=True,exist_ok=True)
    report={}
    with tempfile.TemporaryDirectory() as tmp:
        tmp=pathlib.Path(tmp)
        for name in ['spawner.py','region.py','validate.py']:
            text=subprocess.check_output(['git','show','port/builder-guy-towns:reference/python-spawner/'+name],text=True)
            (tmp/name).write_text(text)
        for seed in [13,14,30,31,46]:
            blob=tmp/'data.pkl'
            code=f"import pickle; from region import make_region,spawn_region; r=make_region({seed}); pickle.dump((r,spawn_region(r)),open({str(blob)!r},'wb'))"
            subprocess.run([sys.executable,'-c',code],cwd=tmp,check=True)
            old,results=pickle.loads(blob.read_bytes()); before=png(old,results)
            settings=Settings({'generation.parity':True}); new=make_region(seed,settings=settings)
            after=png(new,spawn_region(new,settings=settings))
            (dest/f'{seed}-before.png').write_bytes(before); (dest/f'{seed}-after.png').write_bytes(after)
            report[seed]={'identical_pixels_and_bytes':before==after,'before_sha256':hashlib.sha256(before).hexdigest(),'after_sha256':hashlib.sha256(after).hexdigest()}
    (dest/'comparison.json').write_text(json.dumps(report,indent=2)+'\n')
    assert all(v['identical_pixels_and_bytes'] for v in report.values()),report
    print('Five before/after PNG pairs are byte-identical (parity year; parks excluded).')
if __name__=='__main__': main()
