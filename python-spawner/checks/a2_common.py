"""Shared A2 measurement window and reproducible JSON runner."""
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
SEEDS = (14, 30, 31, 7, 52)
WINDOW_MI = (-20, -20, 40, 40)


def run_check(name, measure, passes, seeds=SEEDS, progress=print):
    from settings import Settings, applied
    from world import MILE, rivers
    results, metrics, failures = [], {}, []
    for seed in seeds:
        progress(f'[{name}] seed {seed}, window {WINDOW_MI}')
        with applied(Settings()):
            features = list(rivers(seed, tuple(v * MILE for v in WINDOW_MI)))
            snapshot = ROOT/'check-results'/f'a2-features-seed{seed}.json'
            snapshot.parent.mkdir(parents=True,exist_ok=True)
            snapshot.write_text(json.dumps(features),encoding='utf-8')
            summary = measure(features, seed)
        metrics[str(seed)] = summary
        passed = passes(summary)
        measured = json.dumps({k: v for k, v in summary.items() if k not in ('joins', 'pairs')}, sort_keys=True)
        print(f'seed {seed}: {measured}', flush=True)
        results.append(dict(name=f'{name} seed {seed}', passed=passed, measured=measured, target=name))
        if not passed:
            failures.append(dict(check=name, seed=seed, coordinates=[0, 0], feature_ids=[], message=measured))
    revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    return results, {name: dict(source_revision=revision,window_mi=WINDOW_MI, per_seed=metrics)}, failures


def main(run):
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT/'check-results'/f'{run.__module__}-latest.json',
                        help='Use an explicit checks/*.json path to retain a baseline')
    parser.add_argument('--seeds', default=','.join(map(str, SEEDS)))
    args = parser.parse_args()
    results, metrics, failures = run(tuple(map(int, args.seeds.split(','))))
    args.output.write_text(json.dumps(dict(results=results, metrics=metrics, failures=failures), indent=2) + '\n', encoding='utf-8')
    print('FAIL' if failures else 'PASS', flush=True)
    raise SystemExit(bool(failures))
