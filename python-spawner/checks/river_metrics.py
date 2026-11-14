"""Five-seed finished-map river measurements."""
from collections import Counter
import json
import math
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path: sys.path.insert(0, str(ROOT))


def run(seeds=(13, 14, 30, 31, 46), quick=False, progress=print):
    from settings import Settings, applied
    from world import MILE
    from verification import river_geometry_metrics

    # A compact square can miss every order-1 feature by chance. Nine miles is
    # still a fast smoke window while covering enough basin cells to measure
    # finished line geometry reliably.
    size = 9 if quick else 10
    def failure_spot(seed):
        from world import MILE, rivers
        with applied(Settings()):
            features = list(rivers(seed, (0, 0, size * MILE, size * MILE)))
        if not features:
            return [size / 2, size / 2], ['river-window:empty'], []
        center = (size * MILE / 2, size * MILE / 2)
        feature = min(features, key=lambda f: min(math.dist(center, point) for point in f['points']))
        index = min(range(len(feature['points']) - 1), key=lambda i: math.dist(center, ((feature['points'][i][0] + feature['points'][i + 1][0]) / 2, (feature['points'][i][1] + feature['points'][i + 1][1]) / 2)))
        segment = [feature['points'][index], feature['points'][index + 1]]
        point = min(segment, key=lambda p: math.dist(center, p))
        return [point[0] / MILE, point[1] / MILE], [feature['id']], [segment]
    per_seed = []; failures = []; all_angles = []; order3 = order4 = 0
    bifurcation_by_basin = {}; bifurcation_means_by_seed = {}
    order34_by_basin = {}; order34_means_by_seed = {}
    close = pairs = 0; density_sum = 0.0; all_sinuosity = []
    for n, seed in enumerate(seeds, 1):
        progress(f"[river metrics {n}/{len(seeds)}] seed {seed}, {size}×{size} mi")
        rect = (0, 0, size * MILE, size * MILE)
        with applied(Settings()):
            m = river_geometry_metrics(seed, rect)
        per_seed.append({'seed': seed, **m})
        density_sum += m['density']
        all_sinuosity.extend(m['sinuosity_values'])
        all_angles.extend(m['junction_angles'])
        close += m['copy_close']; pairs += m['copy_pairs']
        counts = Counter(m['order_counts'])
        order3 += counts[3]; order4 += counts[4]
        for cell, value in m['bifurcation_ratio_by_basin'].items():
            bifurcation_by_basin[f'{seed}:{cell}'] = value
        if m['bifurcation_ratio_by_basin']:
            bifurcation_means_by_seed[str(seed)] = statistics.mean(m['bifurcation_ratio_by_basin'].values())
        for cell, value in m['order3_order4_ratio_by_basin'].items():
            order34_by_basin[f'{seed}:{cell}'] = value
        if m['order3_order4_ratio_by_basin']:
            order34_means_by_seed[str(seed)] = statistics.mean(m['order3_order4_ratio_by_basin'].values())
        if quick:
            checks = {
                'streams': bool(m['streams']),
                'bearing entropy >=0.8': m['bearing_entropy'] >= .8,
                'E-W bearing share <=30%': m['ewest_fraction'] <= .30,
                'sinuosity median 1.6-2.2': 1.6 <= m['sinuosity_median'] <= 2.2,
                'sinuosity spread >=0.4': m['sinuosity_spread'] >= .4,
                'order-1 widths 8-15 ft': bool(m['order1_width_ft']) and min(m['order1_width_ft']) >= 8 and max(m['order1_width_ft']) <= 15,
                'confluence width rule': not m['width_rule_violations'],
            }
        else:
            checks = {
                'sinuosity median 1.6–2.2': 1.6 <= m['sinuosity_median'] <= 2.2,
                'bearing entropy ≥0.8': m['bearing_entropy'] >= .8,
                'E-W bearing share ≤30%': m['ewest_fraction'] <= .30,
                'order-1 width 8–15 ft': bool(m['order1_width_ft']) and min(m['order1_width_ft']) >= 8 and max(m['order1_width_ft']) <= 15,
                'confluence width rule': not m['width_rule_violations'],
                'trunk width 100–150 ft': bool(m['trunk_width_ft']) and 100 <= max(m['trunk_width_ft']) <= 150,
            }
        for name, passed in checks.items():
            if not passed:
                coordinates, feature_ids, segments = failure_spot(seed)
                failures.append({'check': name, 'seed': seed, 'coordinates': coordinates, 'feature_ids': feature_ids, 'segments': segments, 'message': f"Measured river metrics: {m}"})
        per_seed[-1]['checks'] = checks

    density = density_sum / max(1, len(seeds))
    ratio = order3 / order4 if order4 else math.inf
    basin_rb_values = list(order34_by_basin.values())
    basin_rb_mean = statistics.mean(basin_rb_values) if basin_rb_values else 0
    sorted_sinuosity = sorted(all_sinuosity)
    pooled_p10 = sorted_sinuosity[round(.10 * (len(sorted_sinuosity) - 1))] if sorted_sinuosity else 0
    pooled_p90 = sorted_sinuosity[round(.90 * (len(sorted_sinuosity) - 1))] if sorted_sinuosity else 0
    pooled_median = statistics.median(sorted_sinuosity) if sorted_sinuosity else 0
    angle_mean = statistics.mean(all_angles) if all_angles else 0
    angle_sd = statistics.pstdev(all_angles) if all_angles else 0
    bins = [sum(i <= angle < i + 10 for angle in all_angles) for i in range(0, 110, 10)]
    summary = {
        'seeds': list(seeds), 'window_miles': size, 'per_seed': per_seed,
        'mean_stream_density_mi_per_sq_mi': density,
        'pooled_sinuosity_median': pooled_median,
        'pooled_sinuosity_p10': pooled_p10,
        'pooled_sinuosity_p90': pooled_p90,
        'pooled_sinuosity_spread': pooled_p90 - pooled_p10,
        'order3_count': order3, 'order4_count': order4, 'order3_order4_ratio': ratio,
        'junction_mean_deg': angle_mean, 'junction_sd_deg': angle_sd,
        'junction_below25_fraction': sum(a < 25 for a in all_angles) / max(1, len(all_angles)),
        'junction_10deg_bin_max_fraction': max(bins, default=0) / max(1, len(all_angles)),
        'junction_bins': bins, 'copy_close': close, 'copy_pairs': pairs,
        'copy_fraction': close / pairs if pairs else 0,
        'bifurcation_ratio_by_basin': bifurcation_by_basin,
        'bifurcation_mean_by_seed': bifurcation_means_by_seed,
        'order3_order4_ratio_by_basin': order34_by_basin,
        'order3_order4_mean_by_seed': order34_means_by_seed,
        'mean_order3_order4_ratio_by_basin': basin_rb_mean,
        'order3_order4_ratio_by_basin_min': min(basin_rb_values, default=0),
        'order3_order4_ratio_by_basin_max': max(basin_rb_values, default=0),
    }
    if not quick:
        aggregate = {
            'stream density 0.8–1.5 mi/mi²': .8 <= density <= 1.5,
            'pooled sinuosity median 1.6–2.2': 1.6 <= pooled_median <= 2.2,
            'pooled sinuosity p90–p10 spread ≥0.4': pooled_p90 - pooled_p10 >= .4,
            'mean per-basin N3/N4 Rb 3-5': len(basin_rb_values) >= len(seeds) and 3 <= basin_rb_mean <= 5,
            'aggregate order-3/order-4 ratio 3–5': 3 <= ratio <= 5,
            'copy pairs within ε <5%': pairs > 0 and close / pairs < .05,
            'junction mean 55–75°': 55 <= angle_mean <= 75,
            'junction SD ≥20°': angle_sd >= 20,
            'junctions below 25° <10%': summary['junction_below25_fraction'] < .10,
            'maximum 10° bin ≤25%': summary['junction_10deg_bin_max_fraction'] <= .25,
        }
        summary['aggregate_checks'] = aggregate
        for name, passed in aggregate.items():
            if not passed:
                seed = seeds[0] if seeds else 0
                coordinates, feature_ids, segments = failure_spot(seed)
                failures.append({'check': name, 'seed': list(seeds), 'coordinates': coordinates, 'feature_ids': feature_ids, 'segments': segments, 'message': f'Measured aggregate: {summary}'})
    if quick:
        results = []
        for sample in per_seed:
            measured = (f"entropy {sample['bearing_entropy']:.3f}; E-W {sample['ewest_fraction']:.1%}; "
                        f"sinuosity median/spread {sample['sinuosity_median']:.3f}/{sample['sinuosity_spread']:.3f}; "
                        f"order-1 width {min(sample['order1_width_ft'] or [0]):.2f}-{max(sample['order1_width_ft'] or [0]):.2f} ft")
            results.append({'name': f"finished river smoke seed {sample['seed']}", 'passed': all(sample['checks'].values()), 'measured': measured,
                            'target': 'entropy >=0.8; E-W <=30%; sinuosity 1.6-2.2, p90-p10 >=0.4; order-1 width 8-15 ft; confluence width rule'})
    else:
        measured = {
            'stream density 0.8–1.5 mi/mi²': f"mean {density:.3f}; per-seed range {min(s['density'] for s in per_seed):.3f}-{max(s['density'] for s in per_seed):.3f} mi/mi²",
            'pooled sinuosity median 1.6–2.2': f"median {pooled_median:.3f}",
            'pooled sinuosity p90–p10 spread ≥0.4': f"p10 {pooled_p10:.3f}; p90 {pooled_p90:.3f}; spread {pooled_p90-pooled_p10:.3f}",
            'mean per-basin N3/N4 Rb 3-5': f"mean {basin_rb_mean:.3f}; raw per-basin range {min(basin_rb_values, default=0):.2f}-{max(basin_rb_values, default=0):.2f}",
            'aggregate order-3/order-4 ratio 3–5': f"N3 {order3}; N4 {order4}; ratio {ratio:.3f}",
            'copy pairs within ε <5%': f"{close}/{pairs} pairs below ε={.02:.3f}; fraction {close/max(1,pairs):.3%}",
            'junction mean 55–75°': f"mean {angle_mean:.2f}°",
            'junction SD ≥20°': f"SD {angle_sd:.2f}°",
            'junctions below 25° <10%': f"{summary['junction_below25_fraction']:.2%}",
            'maximum 10° bin ≤25%': f"{summary['junction_10deg_bin_max_fraction']:.2%}",
        }
        targets = {
            'stream density 0.8–1.5 mi/mi²': 'mean stream length density 0.8-1.5 mi per sq mi',
            'pooled sinuosity median 1.6–2.2': 'median 1.6-2.2',
            'pooled sinuosity p90–p10 spread ≥0.4': 'p90-p10 spread >=0.4',
            'mean per-basin N3/N4 Rb 3-5': 'mean per-basin order-3/order-4 Rb 3-5',
            'aggregate order-3/order-4 ratio 3–5': 'aggregate N3/N4 ratio 3-5',
            'copy pairs within ε <5%': 'less than 5% of normalized turning-function pairs below ε=0.02 rad',
            'junction mean 55–75°': 'mean 55-75 degrees',
            'junction SD ≥20°': 'standard deviation >=20 degrees',
            'junctions below 25° <10%': 'less than 10% below 25 degrees',
            'maximum 10° bin ≤25%': 'no 10-degree bin exceeds 25%',
        }
        results = [{'name': name, 'passed': passed, 'measured': measured[name], 'target': targets[name]}
                   for name, passed in summary['aggregate_checks'].items()]
        for sample in per_seed:
            results.append({'name': f"finished river bearing/width sample seed {sample['seed']}", 'passed': all(sample['checks'].values()),
                            'measured': (f"entropy {sample['bearing_entropy']:.3f}; E-W {sample['ewest_fraction']:.1%}; "
                                         f"order-1 width {min(sample['order1_width_ft'] or [0]):.2f}-{max(sample['order1_width_ft'] or [0]):.2f} ft; "
                                         f"largest trunk {max(sample['trunk_width_ft'] or [0]):.2f} ft; width violations {len(sample['width_rule_violations'])}"),
                            'target': 'entropy >=0.8; E-W <=30%; order-1 width 8-15 ft; largest trunk 100-150 ft; confluence width rule'})
    if not quick:
        baseline = json.loads((ROOT/'checks'/'a2-baseline-item-a.json').read_text(encoding='utf-8'))['per_seed']
        outside = lambda value: max(.8-value, value-1.5, 0)
        density_comparison = {}
        for sample in per_seed:
            seed = sample['seed']; value = sample['density']; before = baseline.get(str(seed), {}).get('density')
            known = before is not None and outside(before)>0 and outside(value)<=outside(before)+1e-9
            name = ('BASELINE: ' if known else '') + f'stream density seed {seed} 0.8-1.5 mi/mi2'
            passed = .8<=value<=1.5
            measured = f'db78343 {before}; current {value:.9f}'
            results.append(dict(name=name,passed=passed,measured=measured,target='per-seed density 0.8-1.5; known baseline failures remain failures'))
            density_comparison[str(seed)] = dict(before=before,after=value,known_baseline=known)
            if not passed:
                failures.append(dict(check=name,seed=seed,coordinates=[0,0],feature_ids=[],message=measured))
        before_rb = {f'{seed}:{key[:-6]}':value for seed,data in baseline.items()
                     for key,value in data['rb'].items() if key.endswith(':N3/N4')}
        rb_not_worse = all(key in before_rb and max(3-value,value-5,0)<=max(3-before_rb[key],before_rb[key]-5,0)+1e-9
                           for key,value in order34_by_basin.items())
        for result in results:
            if result['name']=='mean per-basin N3/N4 Rb 3-5' and not result['passed'] and rb_not_worse:
                result['name']='BASELINE: '+result['name']
                result['measured']+='; db78343 mean 5.596; known failure'
        for failure in failures:
            if failure['check']=='mean per-basin N3/N4 Rb 3-5' and rb_not_worse:
                failure['check']='BASELINE: '+failure['check']
        summary['density_before_after']=density_comparison
        summary['rb_before_after']={key:dict(before=before_rb.get(key),after=value) for key,value in order34_by_basin.items()}
        results.append(dict(name='Rb outliers no worse than db78343',passed=rb_not_worse,
                            measured=str(summary['rb_before_after']),target='do not increase any measured N3/N4 outlier'))
        if not rb_not_worse:
            failures.append(dict(check='Rb regression',seed=list(seeds),coordinates=[0,0],feature_ids=[],message=str(summary['rb_before_after'])))
    return results, {'river_metrics': summary}, failures


if __name__ == '__main__':
    from common import write_report
    results, metrics, failures = run(progress=lambda message: print(message, flush=True))
    print(results[0]['measured'], flush=True)
    print(f"Report: {write_report(results, metrics, failures) / 'summary.md'}", flush=True)
    raise SystemExit(1 if failures else 0)
