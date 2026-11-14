"""Seeded outlet-lattice and regional-bearing survey."""
import math
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path: sys.path.insert(0, str(ROOT))


def run(seeds=tuple(range(20)), quick=False, progress=print):
    from world import _basin_angle, _bearing, _next_outlet, _outlet
    count = 2 if quick else len(seeds)
    seeds = tuple(seeds[:count])
    values = []; failures = []; crossings = []
    for i, seed in enumerate(seeds, 1):
        progress(f"[outlet lattice {i}/{len(seeds)}] seed {seed}")
        angle = _basin_angle(seed); candidates = []
        for cx in range(-2, 3):
            for cy in range(-2, 2):
                nx, ny, end = _next_outlet(seed, cx, cy); start = _outlet(seed, cx, cy)
                for a, b in ((start, end),):
                    if (a[1] <= 0 < b[1]) or (b[1] <= 0 < a[1]):
                        t = -a[1] / (b[1] - a[1]); candidates.append(a[0] + t * (b[0] - a[0]))
        bearing = [_bearing(seed, sx, sy) for sx in range(-2, 2) for sy in range(-2, 2)]
        mean_s = statistics.mean(math.sin(a) for a in bearing); mean_c = statistics.mean(math.cos(a) for a in bearing)
        circular_sd = math.degrees(math.sqrt(-2 * math.log(max(1e-12, math.hypot(mean_s, mean_c)))))
        from world import MILE
        x = round(min(candidates) / MILE, 3) if candidates else None
        crossings.append(x)
        values.append({'seed': seed, 'basin_angle_deg': math.degrees(angle), 'trunk_bearing_circular_sd_deg': circular_sd, 'ew_crossing_x_mi': x})
        if not candidates:
            failures.append({'check': 'outlet crossing exists', 'seed': seed, 'coordinates': [0, 0], 'feature_ids': [f'outlet-route:{seed}'], 'message': 'No routed outlet crossed the survey line.'})
        if circular_sd < 25:
            failures.append({'check': 'regional bearing circular SD ≥25°', 'seed': seed, 'coordinates': [0, 0], 'feature_ids': [f'superregion-bearing:{seed}'], 'message': f'Measured {circular_sd:.2f}°'})
    repeated = sorted(x for x in set(crossings) if x is not None and crossings.count(x) > 1)
    for x in repeated:
        same = [v['seed'] for v in values if v['ew_crossing_x_mi'] == x]
        failures.append({'check': 'outlet x positions unique across seeds', 'seed': same, 'coordinates': [x, 0], 'feature_ids': [f'outlet-crossing:{s}' for s in same], 'message': f'Repeated E-W crossing x={x} mi.'})
    result = {'name': 'outlet lattice and superregion bearings', 'passed': not failures, 'measured': f"{len(seeds)} seeds; minimum regional circular SD {min((v['trunk_bearing_circular_sd_deg'] for v in values), default=0):.2f}°; repeated crossing positions {len(repeated)}", 'target': '20 seeds; E-W outlet x positions do not repeat; circular SD ≥25° per seed'}
    return [result], {'outlet_lattice': values}, failures


if __name__ == '__main__':
    from common import write_report
    results, metrics, failures = run(progress=lambda message: print(message, flush=True))
    print(f"Report: {write_report(results, metrics, failures) / 'summary.md'}", flush=True)
    raise SystemExit(1 if failures else 0)
