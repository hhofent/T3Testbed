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
def test_reference_town_bytes_in_parity_mode(seed):
    reference=json.loads(Path(__file__).with_name('town_baseline.json').read_text())[str(seed)]
    actual=make_region(seed,settings=Settings({'generation.parity':True}))['town']
    if reference: reference.pop('park_check',None)
    assert json.dumps(actual,sort_keys=True)==json.dumps(reference,sort_keys=True)

def test_explicit_defaults_are_byte_identical():
    for seed in [13,14,30,31,46]:
        assert json.dumps(make_region(seed),sort_keys=True)==json.dumps(make_region(seed,settings=Settings()),sort_keys=True)


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


def test_derived_tiny_grids_follow_primary_settings():
    import region
    settings=Settings({'region.town_grids':[(3,3)]*4})
    with applied(settings): assert region.GRIDS['tiny'][0]==[(3,3)]*4
    assert region.GRIDS['tiny'][0]!=[(3,3)]*4
