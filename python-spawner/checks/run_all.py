"""Run the deterministic extended river checks and write a diagnostic bundle."""
import argparse
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path: sys.path.insert(0, str(HERE))

from common import write_report
import river_chunks
import river_crossings
import river_lattice
import river_metrics
import river_meander
import river_valley


def main(argv=None):
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--quick', action='store_true', help='run one or two seed smoke checks on smaller windows')
    parser.add_argument('--seeds', help='comma-separated seed list, e.g. 13,14,30')
    args = parser.parse_args(argv)
    seeds = tuple(int(x) for x in args.seeds.split(',')) if args.seeds else ((13,) if args.quick else (13, 14, 30, 31, 46))
    lattice_seeds = seeds if args.seeds else ((13, 14) if args.quick else tuple(range(20)))
    chunk_seeds = (seeds[0],) if args.seeds else (31,)
    results = []; metrics = {}; failures = []
    progress = lambda message: print(message, flush=True)
    checks = [
        ('finished-map metrics', lambda: river_metrics.run(seeds, args.quick, progress)),
        ('crossings and bridges', lambda: river_crossings.run(seeds, args.quick, progress)),
        ('chunk request order', lambda: river_chunks.run(chunk_seeds, args.quick, progress)),
        ('outlet lattice', lambda: river_lattice.run(lattice_seeds, args.quick, progress)),
    ]
    # The 40 mi valley window is a full-run measurement. --quick stays on the small smoke.
    if not args.quick:
        valley_seeds = seeds if args.seeds else (14, 30, 31)
        checks.append(('valley geometry', lambda: river_valley.run(valley_seeds, False, progress)))
        meander_seeds = seeds if args.seeds else (14, 30, 31)
        checks.append(('meander geometry', lambda: river_meander.run(meander_seeds, False, progress)))
    for i, (label, run) in enumerate(checks, 1):
        print(f"\n=== {i}/{len(checks)}: {label} ===", flush=True)
        try:
            got_results, got_metrics, got_failures = run()
            results.extend(got_results); metrics.update(got_metrics); failures.extend(got_failures)
            for item in got_results: print(f"{'PASS' if item['passed'] else 'FAIL'} {item['name']}: {item['measured']}", flush=True)
        except Exception as exc:
            message = f'{type(exc).__name__}: {exc}'
            results.append({'name': label, 'passed': False, 'measured': message, 'target': 'check completes and reports deterministic measurements'})
            metrics[label] = {'error': message}
            failures.append({'check': label, 'seed': list(seeds), 'coordinates': [0, 0], 'feature_ids': [f'check:{label}'], 'message': message})
            print(f'FAIL {label}: {message}', flush=True)
    out = write_report(results, metrics, failures)
    passed = all(item['passed'] for item in results) and not failures
    print(f"\n{'ALL CHECKS PASSED' if passed else 'CHECKS FAILED'}; report: {out / 'summary.md'}", flush=True)
    print(f"Raw measurements: {out / 'metrics.json'}", flush=True)
    return 0 if passed else 1


if __name__ == '__main__':
    raise SystemExit(main())
