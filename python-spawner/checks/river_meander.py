"""Meander shape check.

Seeds 14, 30, and 31. Window is 40 miles: (-20, -20, 40, 40).
Counts every river feature, including outlets.

A reversal is a 0.1 mile step whose heading differs by more than 150
degrees from that reach's downstream chord. A continuation kink is a
non-junction node, one reach in and one reach out, whose 0.1 mile
arrival and departure differ by more than 35 degrees. Local wavelength
is twice the gap between curvature sign changes. Order-1 amplitude is
the leftover lateral offset after a 0.22 mile bow is smoothed out of
the middle of the reach, compared with 20 channel widths. Trunks are
order 3 and above. Gentle trunk length is
the share of 0.1 mile steps whose surrounding 0.6 mile window has
sinuosity under 1.08.
"""
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

SEEDS = (14, 30, 31)
WINDOW_MI = (-20.0, -20.0, 40.0, 40.0)
REVERSAL_DEG = 150.0
KINK_DEG = 35.0
WAVE_STEP_MI = 0.005
HALFWAVE_MIN = 4
CV_MIN = 0.25
AMP_WIDTHS = 20.0
TRUNK_SINUOSITY = (1.05, 1.25)
TRUNK_WAVE_WIDTH = (10.0, 14.0)
GENTLE_SINUOSITY = 1.08
GENTLE_SHARE = 0.30


def _dist(a, b):
    return math.hypot(b[0] - a[0], b[1] - a[1])


def _ang(a, b):
    return math.atan2(b[1] - a[1], b[0] - a[0])


def _wrap(delta):
    return (delta + math.pi) % (2 * math.pi) - math.pi


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


def _path_length(points):
    return sum(_dist(a, b) for a, b in zip(points, points[1:]))


def _meander_amplitude(points):
    """Peak meander offset after the valley bow is smoothed away.

    The bow is a mile-scale bend, so a 0.22 mile average absorbs it and
    the residual is the shorter wiggle. Join tapers sit in the outer
    30 percent and are not part of the meander.
    """
    from world import MILE

    step = 0.02 * MILE
    pts = _resample(points, step)
    if len(pts) < 8:
        return 0.0
    a, b = points[0], points[-1]
    chord = _dist(a, b)
    if chord < 1:
        return 0.0
    nx, ny = -(b[1] - a[1]) / chord, (b[0] - a[0]) / chord
    lats = [(q[0] - a[0]) * nx + (q[1] - a[1]) * ny for q in pts]
    radius = max(1, int(round((0.11 * MILE) / step)))
    i0 = int(len(lats) * 0.30)
    i1 = max(i0 + 1, int(len(lats) * 0.70))
    peak = 0.0
    for i in range(i0, i1):
        lo = max(0, i - radius)
        hi = min(len(lats), i + radius + 1)
        smooth = sum(lats[lo:hi]) / (hi - lo)
        peak = max(peak, abs(lats[i] - smooth))
    return peak


def _self_intersections(points, mile):
    from world import crossing_point

    segs = [(a, b) for a, b in zip(points, points[1:]) if _dist(a, b) > 0]
    cell = 0.25 * mile
    buckets = {}
    for i, (a, b) in enumerate(segs):
        for ix in range(math.floor(min(a[0], b[0]) / cell), math.floor(max(a[0], b[0]) / cell) + 1):
            for iy in range(math.floor(min(a[1], b[1]) / cell), math.floor(max(a[1], b[1]) / cell) + 1):
                buckets.setdefault((ix, iy), []).append(i)
    count = 0
    seen = set()
    for i, (a, b) in enumerate(segs):
        hits = set()
        for ix in range(math.floor(min(a[0], b[0]) / cell), math.floor(max(a[0], b[0]) / cell) + 1):
            for iy in range(math.floor(min(a[1], b[1]) / cell), math.floor(max(a[1], b[1]) / cell) + 1):
                hits.update(buckets.get((ix, iy), ()))
        for j in hits:
            if j <= i + 1 or (i, j) in seen:
                continue
            seen.add((i, j))
            hit = crossing_point(a, b, segs[j][0], segs[j][1])
            if hit is None:
                continue
            _, t, s, _ = hit
            if t < 1e-4 or t > 1 - 1e-4 or s < 1e-4 or s > 1 - 1e-4:
                continue
            count += 1
    return count


def _reversals(points, step):
    samples = _resample(points, step)
    if len(samples) < 2 or _dist(points[0], points[-1]) < 1:
        return 0
    downstream = _ang(points[0], points[-1])
    count = 0
    for a, b in zip(samples, samples[1:]):
        if _dist(a, b) < step * 0.5:
            continue
        if abs(math.degrees(_wrap(_ang(a, b) - downstream))) > REVERSAL_DEG:
            count += 1
    return count


def _halfwaves(points, step):
    samples = _resample(points, step)
    if len(samples) < 8:
        return []
    headings = [_ang(a, b) for a, b in zip(samples, samples[1:])]
    signs = []
    previous = 0
    for i in range(1, len(headings)):
        turn = _wrap(headings[i] - headings[i - 1])
        if abs(turn) < math.radians(0.4):
            signs.append(previous)
        else:
            previous = 1 if turn > 0 else -1
            signs.append(previous)
    lengths = []
    run = step
    for i in range(1, len(signs)):
        if signs[i] != 0 and signs[i - 1] != 0 and signs[i] != signs[i - 1]:
            if run >= step * 2:
                lengths.append(run)
            run = step
        else:
            run += step
    return lengths


def _cv(values):
    if len(values) < 2:
        return None
    mean = sum(values) / len(values)
    if mean <= 0:
        return None
    var = sum((v - mean) ** 2 for v in values) / len(values)
    return math.sqrt(var) / mean


def _continuation_kinks(features, step):
    by_end = {}
    by_start = {}
    for feature in features:
        by_end.setdefault(tuple(feature['points'][-1]), []).append(feature)
        by_start.setdefault(tuple(feature['points'][0]), []).append(feature)
    count = 0
    for point, incoming in by_end.items():
        outgoing = by_start.get(point, [])
        if len(incoming) != 1 or len(outgoing) != 1:
            continue
        before, after = incoming[0], outgoing[0]
        if before is after or after.get('id') != before.get('downstream'):
            continue
        left = _resample(before['points'], step)
        right = _resample(after['points'], step)
        if len(left) < 2 or len(right) < 2:
            continue
        turn = abs(math.degrees(_wrap(_ang(left[-2], left[-1]) - _ang(right[0], right[1]))))
        if turn > KINK_DEG:
            count += 1
    return count


def _gentle_share(points, mile):
    step = 0.1 * mile
    samples = _resample(points, step)
    if len(samples) < 6:
        chord = _dist(points[0], points[-1])
        length = _path_length(points)
        if chord <= 0:
            return 0.0, 0.0
        return (length if length / chord < GENTLE_SINUOSITY else 0.0), length
    gentle = 0
    total = 0
    half = 3
    for i in range(len(samples) - 1):
        lo = max(0, i - half)
        hi = min(len(samples) - 1, i + half)
        if hi - lo < 3:
            continue
        window = samples[lo:hi + 1]
        chord = _dist(window[0], window[-1])
        length = _path_length(window)
        total += 1
        if chord > 0 and length / chord < GENTLE_SINUOSITY:
            gentle += 1
    if not total:
        return 0.0, 0.0
    return gentle * step, total * step


def _percentile(values, fraction):
    if not values:
        return 0.0
    ordered = sorted(values)
    index = min(len(ordered) - 1, max(0, int(round(fraction * (len(ordered) - 1)))))
    return ordered[index]


def measure(features, mile):
    step = WAVE_STEP_MI * mile
    reversal_step = 0.1 * mile
    rivers = [f for f in features if f.get('kind') == 'rivers']
    summary = {
        'self_intersections': 0,
        'reversals': 0,
        'kinks': _continuation_kinks(rivers, reversal_step),
        'order1_reaches': 0,
        'order1_over_cap': 0,
        'wave_reaches': 0,
        'wave_cv_below': 0,
        'wave_cv_median': 0.0,
        'trunk_sinuosity_median': 0.0,
        'trunk_wavelength_over_width': 0.0,
        'trunk_gentle_share': 0.0,
        'trunk_amplitude_cv': 0.0,
        'trunk_reaches': 0,
    }
    cvs = []
    trunk_sinuosity = []
    trunk_ratios = []
    trunk_amps = []
    gentle_len = 0.0
    trunk_len = 0.0
    for feature in rivers:
        points = feature['points']
        if len(points) < 2:
            continue
        summary['self_intersections'] += _self_intersections(points, mile)
        summary['reversals'] += _reversals(points, reversal_step)
        order = feature.get('stream_order', 0)
        widths = feature.get('widths') or [feature.get('width', 1)]
        width = sum(widths) / len(widths) if widths else feature.get('width', 1)
        if order == 1 and width > 0:
            summary['order1_reaches'] += 1
            if _meander_amplitude(points) > AMP_WIDTHS * width:
                summary['order1_over_cap'] += 1
        halves = _halfwaves(points, step)
        if len(halves) >= HALFWAVE_MIN:
            cv = _cv(halves)
            if cv is not None:
                summary['wave_reaches'] += 1
                cvs.append(cv)
                if cv < CV_MIN:
                    summary['wave_cv_below'] += 1
        chord = _dist(points[0], points[-1])
        length = _path_length(points)
        if order >= 3 and chord > 0.2 * mile and width > 0:
            summary['trunk_reaches'] += 1
            trunk_sinuosity.append(length / chord)
            if len(halves) >= 2:
                trunk_ratios.append((2 * _percentile(halves, 0.5)) / width)
            amp = _meander_amplitude(points) / width
            trunk_amps.append(amp)
            part, whole = _gentle_share(points, mile)
            gentle_len += part
            trunk_len += whole
    summary['wave_cv_median'] = _percentile(cvs, 0.5)
    summary['trunk_sinuosity_median'] = _percentile(trunk_sinuosity, 0.5)
    summary['trunk_wavelength_over_width'] = _percentile(trunk_ratios, 0.5)
    summary['trunk_gentle_share'] = gentle_len / trunk_len if trunk_len else 0.0
    summary['trunk_amplitude_cv'] = _cv(trunk_amps) or 0.0
    summary['order1_over_cap_share'] = (
        summary['order1_over_cap'] / summary['order1_reaches'] if summary['order1_reaches'] else 1.0
    )
    summary['wave_cv_fail_share'] = (
        summary['wave_cv_below'] / summary['wave_reaches'] if summary['wave_reaches'] else 1.0
    )
    return summary


def _passes(summary):
    low, high = TRUNK_SINUOSITY
    wave_lo, wave_hi = TRUNK_WAVE_WIDTH
    return (
        summary['self_intersections'] == 0
        and summary['reversals'] == 0
        and summary['kinks'] == 0
        and summary['order1_over_cap_share'] == 0
        and summary['wave_reaches'] > 0
        and summary['wave_cv_fail_share'] == 0
        and low <= summary['trunk_sinuosity_median'] <= high
        and wave_lo <= summary['trunk_wavelength_over_width'] <= wave_hi
        and summary['trunk_gentle_share'] >= GENTLE_SHARE
        and summary['trunk_amplitude_cv'] >= 0.20
    )


def _line(seed, summary):
    low, high = TRUNK_SINUOSITY
    return (
        f"seed {seed}: self-intersections {summary['self_intersections']} (target 0); "
        f"reversals {summary['reversals']} (target 0); "
        f"continuation kinks >35 deg {summary['kinks']} (target 0); "
        f"order-1 over 20x width {summary['order1_over_cap_share']:.1%} "
        f"({summary['order1_over_cap']}/{summary['order1_reaches']}); "
        f"wavelength CV median {summary['wave_cv_median']:.3f}, "
        f"below 0.25 on {summary['wave_cv_fail_share']:.1%} "
        f"({summary['wave_cv_below']}/{summary['wave_reaches']}); "
        f"trunk sinuosity median {summary['trunk_sinuosity_median']:.3f} (target {low:.2f}-{high:.2f}); "
        f"trunk wavelength/width median {summary['trunk_wavelength_over_width']:.2f} (target 10-14); "
        f"trunk gentle length {summary['trunk_gentle_share']:.1%} (target >=30%); "
        f"trunk amplitude CV {summary['trunk_amplitude_cv']:.3f} (target >=0.20)"
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
        progress(f"[river meander {index}/{len(seeds)}] seed {seed}, 40 mi window")
        with applied(Settings()):
            features = list(rivers(seed, rect))
        summary = measure(features, MILE)
        metrics[str(seed)] = summary
        passed = _passes(summary)
        measured = _line(seed, summary)
        print(measured, flush=True)
        results.append({
            'name': f'meander geometry seed {seed}',
            'passed': passed,
            'measured': measured,
            'target': (
                '0 self-intersections; 0 reversals; 0 continuation kinks over 35 deg; '
                '0 order-1 reaches over 20x width; local-wavelength CV >= 0.25 on every measured reach; '
                'trunk sinuosity median 1.05-1.25; trunk wavelength 10-14x width; '
                'trunk gentle length >= 30%; trunk amplitude CV >= 0.20'
            ),
        })
        if not passed:
            failures.append({
                'check': 'meander geometry',
                'seed': seed,
                'coordinates': [0, 0],
                'feature_ids': ['river-meander-window'],
                'message': measured,
            })
    return results, {'river_meander': metrics}, failures


if __name__ == '__main__':
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
    results, _, failures = run(progress=lambda message: print(message, flush=True))
    print('PASS' if not failures else 'FAIL', flush=True)
    raise SystemExit(0 if not failures else 1)
