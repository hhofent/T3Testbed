"""Curved valley centreline check.

Seeds 14, 30, and 31. Window is 40 miles: (-20, -20, 40, 40).
An edge is one non-outlet channel feature. Its length is the straight
distance between its endpoints. Valley sinuosity is measured after a
0.5 mile box smooth that pins those endpoints, which removes the short
meander and leaves the valley bend. Near-zero curvature means the
smoothed heading changes by less than 8 degrees per mile.
"""
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

SEEDS = (14, 30, 31)
WINDOW_MI = (-20.0, -20.0, 40.0, 40.0)
SMOOTH_MI = 0.5
SAMPLE_MI = 0.05
FLAT_DEG_PER_MI = 8.0
FLAT_RUN_MI = 1.0


def _dist(a, b):
    return math.hypot(b[0] - a[0], b[1] - a[1])


def _resample(points, step):
    if len(points) < 2:
        return list(points)
    out = [points[0]]
    travelled = 0.0
    target = step
    for a, b in zip(points, points[1:]):
        seg = _dist(a, b)
        if seg <= 0:
            continue
        while travelled + seg >= target - 1e-9:
            t = (target - travelled) / seg
            out.append((a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])))
            target += step
        travelled += seg
    if _dist(out[-1], points[-1]) > step * 0.25:
        out.append(points[-1])
    return out


def _smooth(points, step, window):
    if len(points) < 3:
        return list(points)
    radius = max(1, int(round((window / 2) / step)))
    out = []
    last = len(points) - 1
    for i in range(len(points)):
        if i == 0 or i == last:
            out.append(points[i])
            continue
        lo = max(0, i - radius)
        hi = min(len(points), i + radius + 1)
        chunk = points[lo:hi]
        out.append((sum(p[0] for p in chunk) / len(chunk), sum(p[1] for p in chunk) / len(chunk)))
    return out


def _path_length(points):
    return sum(_dist(a, b) for a, b in zip(points, points[1:]))


def _flat_run(points, step_ticks, mile):
    if len(points) < 5:
        return 0.0
    span = max(1, int(round((0.2 * mile) / step_ticks)))
    longest = run = 0.0
    for i in range(span, len(points) - span):
        a, b, c = points[i - span], points[i], points[i + span]
        h1 = math.atan2(b[1] - a[1], b[0] - a[0])
        h2 = math.atan2(c[1] - b[1], c[0] - b[0])
        turn = (h2 - h1 + math.pi) % (2 * math.pi) - math.pi
        per_mile = abs(math.degrees(turn)) / max(1e-6, _dist(a, c) / mile)
        if per_mile < FLAT_DEG_PER_MI:
            run += step_ticks / mile
            if run > longest:
                longest = run
        else:
            run = 0.0
    return longest


def measure(features, mile):
    edges = []
    for feature in features:
        if feature.get('kind') != 'rivers' or feature['id'].startswith('river:outlet:'):
            continue
        points = feature['points']
        if len(points) < 2:
            continue
        chord = _dist(points[0], points[-1]) / mile
        if chord <= 0.05:
            continue
        length = _path_length(points) / mile
        sampled = _resample(points, SAMPLE_MI * mile)
        smoothed = _smooth(sampled, SAMPLE_MI * mile, SMOOTH_MI * mile)
        smooth_length = _path_length(smoothed) / mile
        smooth_chord = _dist(smoothed[0], smoothed[-1]) / mile
        edges.append({
            'id': feature['id'],
            'chord_mi': chord,
            'points_per_mile': (len(points) - 1) / length if length else 0,
            'sinuosity': smooth_length / smooth_chord if smooth_chord > 1e-6 else 1,
            'flat_mi': _flat_run(smoothed, SAMPLE_MI * mile, mile),
        })
    return edges


def _percentile(values, fraction):
    if not values:
        return 0.0
    ordered = sorted(values)
    index = min(len(ordered) - 1, max(0, int(round(fraction * (len(ordered) - 1)))))
    return ordered[index]


def summarize(edges):
    chords = [edge['chord_mi'] for edge in edges]
    sinuosity = [edge['sinuosity'] for edge in edges]
    straight = [edge for edge in edges if edge['flat_mi'] > FLAT_RUN_MI]
    density = [edge['points_per_mile'] for edge in edges]
    return {
        'edges': len(edges),
        'sinuosity_median': _percentile(sinuosity, 0.5),
        'length_p10': _percentile(chords, 0.10),
        'length_median': _percentile(chords, 0.50),
        'length_p90': _percentile(chords, 0.90),
        'straight_share': len(straight) / len(edges) if edges else 1,
        'straight_edges': len(straight),
        'points_per_mile_min': min(density) if density else 0,
    }


def _passes(summary):
    return (
        1.15 <= summary['sinuosity_median'] <= 1.35
        and 0.6 <= summary['length_p10'] <= summary['length_median'] <= summary['length_p90'] <= 1.5
        and summary['straight_share'] < 0.05
        and summary['points_per_mile_min'] >= 6
    )


def run(seeds=SEEDS, quick=False, progress=print):
    from settings import Settings, applied
    from world import MILE, rivers

    results = []
    metrics = {}
    failures = []
    x, y, w, h = WINDOW_MI
    rect = (x * MILE, y * MILE, w * MILE, h * MILE)
    for index, seed in enumerate(seeds, 1):
        progress(f"[river valley {index}/{len(seeds)}] seed {seed}, 40 mi window")
        with applied(Settings()):
            features = list(rivers(seed, rect))
        summary = summarize(measure(features, MILE))
        metrics[str(seed)] = summary
        passed = _passes(summary)
        measured = (
            f"median valley sinuosity {summary['sinuosity_median']:.3f}; "
            f"edge mi p10/median/p90 {summary['length_p10']:.3f}/{summary['length_median']:.3f}/{summary['length_p90']:.3f}; "
            f"straight-run share {summary['straight_share']:.2%} ({summary['straight_edges']}/{summary['edges']}); "
            f"min points/mi {summary['points_per_mile_min']:.1f}"
        )
        print(
            f"seed {seed}: median valley sinuosity {summary['sinuosity_median']:.3f} "
            f"(target 1.15-1.35); edge length p10/median/p90 "
            f"{summary['length_p10']:.3f}/{summary['length_median']:.3f}/{summary['length_p90']:.3f} mi "
            f"(target 0.6-1.5); straight >1 mi share {summary['straight_share']:.2%} "
            f"(target <5%); points/mi min {summary['points_per_mile_min']:.1f}",
            flush=True,
        )
        results.append({
            'name': f'valley geometry seed {seed}',
            'passed': passed,
            'measured': measured,
            'target': 'smoothed sinuosity 1.15-1.35; edge p10/median/p90 within 0.6-1.5 mi; straight runs over 1 mi under 5%; at least 6 points per mile',
        })
        if not passed:
            failures.append({
                'check': 'valley geometry',
                'seed': seed,
                'coordinates': [0, 0],
                'feature_ids': ['river-valley-window'],
                'message': measured,
            })
    return results, {'river_valley': metrics}, failures


if __name__ == '__main__':
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
    results, _, failures = run(progress=lambda message: print(message, flush=True))
    print('PASS' if not failures else 'FAIL', flush=True)
    raise SystemExit(0 if not failures else 1)
