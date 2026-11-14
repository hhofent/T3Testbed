"""Golden hashes captured from the untouched branch reference before extraction."""
import hashlib
import json
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import pytest
from region import make_region
from settings import Settings, applied

@pytest.mark.parametrize('seed',[13,14,30,31,46])
def test_default_region_bytes_match_reference(seed):
    baseline=json.loads(Path(__file__).with_name('baseline.json').read_text())
    output=json.dumps(make_region(seed,settings=Settings()),sort_keys=True).encode()
    assert hashlib.sha256(output).hexdigest()==baseline[str(seed)]

def test_settings_scope_restores_on_exception():
    import region
    before=region.TRAILER_RUN_PCT
    settings=Settings({'region.trailer_run_pct':0})
    with pytest.raises(RuntimeError):
        with applied(settings):
            assert region.TRAILER_RUN_PCT==0
            raise RuntimeError('test scope')
    assert region.TRAILER_RUN_PCT==before

def test_settings_change_reaches_generator_and_does_not_leak():
    settings=Settings({'region.rail_pct':0})
    assert make_region(30,settings=settings)['rail'] is None
    assert make_region(30)['rail'] is not None
