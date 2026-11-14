"""Chunk-order and PYTHONHASHSEED invariance check."""
import json
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def run(seeds=(31,), quick=False, progress=print):
    size = 6
    step = 3
    results = []; metrics = {}; failures = []
    code = r"""
import hashlib,json,random,sys
from world import MILE,rivers
seed=int(sys.argv[1]); size=int(sys.argv[2]); step=int(sys.argv[3])
full=(0,0,size*MILE,size*MILE)
chunks=[(x*MILE,y*MILE,step*MILE,step*MILE) for x in range(0,size,step) for y in range(0,size,step)]
random.Random(991).shuffle(chunks)
assembled={}
for rect in chunks: assembled.update((f['id'],f) for f in rivers(seed,rect))
whole={f['id']:f for f in rivers(seed,full)}
missing=sorted(whole.keys()-assembled.keys()); extra=sorted(assembled.keys()-whole.keys())
different=sorted(i for i in whole.keys()&assembled.keys() if whole[i]!=assembled[i])
digest=hashlib.sha256(json.dumps(whole,sort_keys=True,separators=(',',':')).encode()).hexdigest()
print(json.dumps({'digest':digest,'missing':missing,'extra':extra,'different':different,'count':len(whole)}))
"""
    for i, seed in enumerate(seeds, 1):
        progress(f"[chunk invariance {i}/{len(seeds)}] seed {seed}; {size} mi full window from shuffled {step} mi chunks")
        outputs = []
        for hash_seed in ('1', '2'):
            env = dict(os.environ, PYTHONHASHSEED=hash_seed)
            try:
                raw = subprocess.check_output([sys.executable, '-c', code, str(seed), str(size), str(step)], cwd=ROOT, env=env, text=True, timeout=600)
                outputs.append(json.loads(raw.strip().splitlines()[-1]))
            except Exception as exc:
                outputs.append({'error': repr(exc), 'digest': None, 'missing': [], 'extra': [], 'different': []})
        same = outputs[0].get('digest') == outputs[1].get('digest') and outputs[0]['digest'] is not None
        features_ok = all(not (o.get('missing') or o.get('extra') or o.get('different') or o.get('error')) for o in outputs)
        passed = same and features_ok
        results.append({'name': f'3-mile chunks seed {seed}', 'passed': passed, 'measured': f"hash seeds 1/2 equal={same}; features={outputs[0].get('count')}", 'target': 'shuffled 3-mi chunks equal full-window features under two PYTHONHASHSEED values'})
        metrics[str(seed)] = outputs
        if not passed:
            ids = sorted(set(sum((o.get('missing', []) + o.get('extra', []) + o.get('different', []) for o in outputs), [])))
            failures.append({'check': 'chunk invariance', 'seed': seed, 'coordinates': [size / 2, size / 2], 'feature_ids': ids[:50] or ['river-chunk-window'], 'message': json.dumps(outputs, sort_keys=True)})
    return results, {'chunk_invariance': metrics}, failures


if __name__ == '__main__':
    from common import write_report
    results, metrics, failures = run(progress=lambda message: print(message, flush=True))
    print(f"Report: {write_report(results, metrics, failures) / 'summary.md'}", flush=True)
    raise SystemExit(1 if failures else 0)
