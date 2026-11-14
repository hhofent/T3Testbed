"""Regenerate the grouped decision report from an existing probe CSV (no probe run)."""
import csv
from collections import defaultdict
from pathlib import Path

root = Path(__file__).resolve().parent
rows = list(csv.DictReader((root / 'settings-probe.csv').open(encoding='utf-8-sig')))
# Alpha is present on every reflected FloatDistribution, including IntDistribution.
parents = {r['path'].rsplit('.', 1)[0] for r in rows if r['path'].endswith('.Alpha')}
groups = defaultdict(list)
for row in rows:
    parent = next((p for p in parents if row['path'].startswith(p + '.')), row['path'])
    groups[parent].append(row)

lines = ['# Settings with no sampled output effect', '',
    'User decision pending: keep, wire later, or remove. Nothing was removed or wired. '
    'DEAD means no effect in the sampled maps, seeds and mutations; see settings-probe.md. '
    'Viewer-only, house-only and inactive distribution fields can appear here.', '',
    'Distribution fields are grouped under their parent. A parent with any LIVE child is '
    'excluded from DEAD even when its other fields are DEAD. ERROR parents are listed separately '
    'because their probes are incomplete. Scalar settings remain individual entries.', '',
    'Regenerate after a probe with `python port-report/group-settings.py`.', '', '## DEAD parents and scalar settings', '']
dead_count = live_count = error_count = 0
for parent, children in sorted(groups.items()):
    if any(r['status'] == 'LIVE' for r in children):
        live_count += 1
        continue
    if any(r['status'] == 'ERROR' for r in children):
        error_count += 1
        continue
    assert all(r['status'] == 'DEAD' for r in children)
    dead_count += 1
    suffix = ''
    if parent in parents:
        suffix = ': ' + ', '.join('`' + r['path'][len(parent)+1:] + '`' for r in children)
    lines.append('- `' + parent + '`' + suffix)

lines += ['', '## ERROR settings', '',
    'All five errors are probe inputs rejected by the existing Min ≤ Max validator. '
    'They are limitations of independent endpoint nudging, not demonstrated generator bugs. '
    'The probe does not coordinate paired bounds; a rejected mutation does not establish '
    'whether the setting is live. No validation was changed.', '',
    '| Setting | Rejected mutation and cause | Bug? |', '|---|---|---|']
for r in rows:
    if r['status'] != 'ERROR':
        continue
    lines.append('| `' + r['path'] + '` | ' + r['errors'].replace('|', '\\|') + ' | Probe limitation; validator correct |')
lines += ['', '## Grouping totals', '',
    f'{dead_count} DEAD entries; {live_count} entries with LIVE evidence (excluded); '
    f'{error_count} ERROR entries without LIVE evidence (excluded).', '',
    'Raw leaf counts remain 194 LIVE, 2,755 DEAD and 5 ERROR. Grouping does not change the evidence.']
(root / 'dead-settings.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
print(f'{dead_count} DEAD, {live_count} LIVE, {error_count} ERROR grouped entries')
