"""Every downstream connection, measured by arc length on finished rivers.

Equal-order continuations and split pieces are deliberately included. Receiver
contacts are projected onto segments; they need not coincide with a vertex.
"""
import math
import statistics
from a2_common import SEEDS, WINDOW_MI, run_check, main


from river_geometry import station, receiver_tail, angle, summarize, junction_metrics


def measure(features, seed):
    summary=junction_metrics(features,seed)
    if seed==14:
        for row in sorted(summary['joins'],key=lambda r:r['0.25'])[:20]:
            print('lowest join '+str(row),flush=True)
    return summary


def passes(m):
    return all(55 <= m[str(s)]['mean'] <= 75 and m[str(s)]['under25'] <= .05
               and m[str(s)]['minimum'] >= 10 and m[str(s)]['sd'] >= 20
               and m[str(s)]['largest_bin'] <= .25 for s in (.1,.25))


def run(seeds=SEEDS, quick=False, progress=print):
    results, metrics, failures = run_check('river_junctions', measure, passes, seeds, progress)
    per = metrics['river_junctions']['per_seed']
    pooled = {str(s):summarize([r[str(s)] for m in per.values() for r in m['joins'] if r['connection']=='confluence']) for s in (.1,.25)}
    metrics['river_junctions']['pooled'] = pooled
    results.append(dict(name='junctions pooled',passed=passes(pooled),measured=str(pooled),target='same per-seed thresholds'))
    return results, metrics, failures


if __name__ == '__main__':
    main(run)
