"""Finished-map river topology, crossing and bridge checks."""
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path: sys.path.insert(0, str(ROOT))


def _crossing_crop(streams, pair):
    from world import crossing_point
    by_id = {f['id']: f for f in streams}
    a, b = (by_id.get(i) for i in pair)
    if not a or not b: return None, []
    for p, q in zip(a['points'], a['points'][1:]):
        for r, s in zip(b['points'], b['points'][1:]):
            hit = crossing_point(p, q, r, s)
            if hit:
                point = hit[0]
                return [point[0], point[1]], [[p, q], [r, s]]
    return None, []


def run(seeds=(13, 14, 30, 31, 46), quick=False, progress=print):
    from settings import Settings, applied
    from world import MILE, bridges, crossing_point, feature_points, window
    from verification import river_checks

    size = 3 if quick else 10
    results = []; metrics = {'river_checks': {}, 'road_rail_crossings': {}}; failures = []
    for i, seed in enumerate(seeds, 1):
        rect = (0, 0, size * MILE, size * MILE)
        progress(f"[river crossings {i}/{len(seeds)}] seed {seed}, {size}×{size} mi topology")
        with applied(Settings()):
            checks = river_checks(seed, rect)
        metrics['river_checks'][str(seed)] = checks
        passed = all(v['pass'] for v in checks.values())
        results.append({'name': f'river continuity/crossings seed {seed}', 'passed': passed, 'measured': ', '.join(f"{k}={'PASS' if v['pass'] else 'FAIL'}" for k, v in checks.items()), 'target': 'acyclic and downhill; no river crossings outside shared junctions'})
        if not passed:
            from world import rivers
            with applied(Settings()): streams = list(rivers(seed, rect))
            for check_name, item in checks.items():
                if item['pass']: continue
                for pair in item.get('failures', []):
                    coord, segments = _crossing_crop(streams, pair) if isinstance(pair, (tuple, list)) and len(pair) == 2 else (None, [])
                    coordinates = [coord[0] / MILE, coord[1] / MILE] if coord else [size / 2, size / 2]
                    failures.append({'check': check_name, 'seed': seed, 'coordinates': coordinates, 'feature_ids': list(pair) if isinstance(pair, (tuple, list)) else [str(pair)], 'segments': segments, 'message': f'Failure in {check_name}: {pair}'})

    seed = seeds[0] if seeds else 31
    size = 6 if quick else 12
    rect = (-size / 2 * MILE, -size / 2 * MILE, size * MILE, size * MILE)
    progress(f"[bridge geometry] seed {seed}, {size}×{size} mi finished map")
    with applied(Settings({'stages.houses': False})):
        features = window(seed, rect, Settings({'stages.houses': False}), detail=False)['features']
    decks = {f['id']: f for f in features if f['kind'] == 'bridges'}
    in_window = {id: f for id, f in decks.items() if rect[0] < f['crossing'][0] < rect[0] + rect[2] and rect[1] < f['crossing'][1] < rect[1] + rect[3]}
    streams = [f for f in features if f['kind'] == 'rivers']
    expected = set(); bridge_failures = []; checked = 0
    for carrier in features:
        if carrier['kind'] not in ('roads', 'rail'): continue
        for stream in streams:
            if not (carrier['bounds'][0] <= stream['bounds'][0] + stream['bounds'][2] and stream['bounds'][0] <= carrier['bounds'][0] + carrier['bounds'][2] and carrier['bounds'][1] <= stream['bounds'][1] + stream['bounds'][3] and stream['bounds'][1] <= carrier['bounds'][1] + carrier['bounds'][3]): continue
            for a, b in zip(feature_points(carrier), feature_points(carrier)[1:]):
                for j, (c, d) in enumerate(zip(stream['points'], stream['points'][1:])):
                    hit = crossing_point(a, b, c, d)
                    if hit is None: continue
                    point, _, t, sine = hit
                    if not (rect[0] < point[0] < rect[0] + rect[2] and rect[1] < point[1] < rect[1] + rect[3]): continue
                    water_id = f"{stream['id']}:segment:{j}"; bridge_id = f"bridge:{carrier['id']}|{water_id}"
                    expected.add(bridge_id); checked += 1
                    water_width = (stream['widths'][j] * (1 - t) + stream['widths'][j + 1] * t) if stream.get('widths') else stream['width']
                    wanted = math.inf if sine <= 1e-6 else (water_width + 40 * 192) / sine
                    deck = decks.get(bridge_id)
                    actual = math.dist(*deck['points']) if deck else None
                    if sine <= 1e-6 or deck is None or not math.isclose(actual, wanted, rel_tol=1e-8):
                        bridge_failures.append({'check': 'one correctly sized bridge per carrier crossing', 'seed': seed, 'coordinates': [point[0] / MILE, point[1] / MILE], 'feature_ids': [carrier['id'], stream['id'], bridge_id], 'segments': [[a, b], [c, d]], 'message': f'angle sine={sine}, deck length={actual}, target length={wanted}'})
    duplicates = set(in_window) ^ expected
    for bridge_id in sorted(duplicates):
        deck = decks.get(bridge_id)
        point = deck.get('crossing') if deck else None
        bridge_failures.append({'check': 'exactly one bridge per crossing', 'seed': seed, 'coordinates': point and [point[0] / MILE, point[1] / MILE] or [0, 0], 'feature_ids': [bridge_id], 'message': 'Unmatched bridge deck or carrier crossing.'})
    metrics['road_rail_crossings'] = {'seed': seed, 'window_miles': size, 'crossings': checked, 'bridge_count_in_window': len(in_window), 'matched_ids': len(expected & set(in_window))}
    results.append({'name': 'finished road/rail bridges', 'passed': checked > 0 and not bridge_failures, 'measured': f"{checked} crossings; {len(in_window)} decks in window; {len(expected & set(in_window))} matched", 'target': 'exactly one deck per crossing; length (water width + 40 ft) / sin(angle); no parallel overlaps'})
    failures.extend(bridge_failures)
    return results, {'river_crossings': metrics}, failures


if __name__ == '__main__':
    from common import write_report
    results, metrics, failures = run(progress=lambda message: print(message, flush=True))
    print(results[-1]['measured'], flush=True)
    print(f"Report: {write_report(results, metrics, failures) / 'summary.md'}", flush=True)
    raise SystemExit(1 if failures or any(not r['passed'] for r in results) else 0)
