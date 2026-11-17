import pytest


@pytest.mark.parametrize('arguments,expected',[([],123456),(['--seed','31'],31),(['--seed','0'],0),(['--param','controls.seed=31'],31)])
def test_server_uses_random_default_and_respects_explicit_seed(monkeypatch,arguments,expected):
 import cli,control,secrets
 monkeypatch.setattr(secrets,'randbelow',lambda limit:123456)
 captured=[]
 monkeypatch.setattr(control,'serve',lambda port,settings:captured.append(settings.values['controls.seed']))
 cli.main(['--serve']+arguments)
 assert captured==[expected]


def test_random_default_also_applies_to_non_server_generation(monkeypatch,capsys):
 import cli,secrets,verification
 monkeypatch.setattr(secrets,'randbelow',lambda limit:123456)
 seeds=[]
 def run(seed,*args):seeds.append(seed);return {'pass':True}
 monkeypatch.setattr(verification,'run',run)
 cli.main(['--verify'])
 assert seeds==[123456]


def test_explicit_default_valued_seed_in_preset_is_preserved(monkeypatch,tmp_path):
 import cli,control,json,secrets
 preset=tmp_path/'seed.json'
 preset.write_text(json.dumps({'format':'python-spawner-settings','version':1,'values':{'controls.seed':31}}))
 monkeypatch.setattr(secrets,'randbelow',lambda limit:123456)
 seeds=[];monkeypatch.setattr(control,'serve',lambda port,settings:seeds.append(settings.values['controls.seed']))
 cli.main(['--serve','--preset',str(preset)])
 assert seeds==[31]
