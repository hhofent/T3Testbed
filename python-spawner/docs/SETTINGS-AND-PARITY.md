# Settings and parity

`settings.py` contains 130 top-level entries. Numeric stage packs collect every
numeric literal from region.py, spawner.py and validate.py; leaf keys retain the
original function and source location. Invariants are locked. Unresearched bounds,
steps and inherited numbers are marked PLACEHOLDER. Compound structures retain
the reference types, keys and sequence lengths.

Normal snapshots use 1990 (N9). `generation.parity=true` uses the old per-seed year.
N9 and N13 necessarily change raw default output: the original raw hashes in
`tests/baseline.json` are historical evidence, not replacement golden values.
Parity comparisons omit removed park data and use the reference per-seed year.
The five entire town records compare byte-for-byte after that exclusion.
`tools/check_images.py` independently loads the original branch modules and
renders both versions with the same park-free renderer. All five PNG pairs are
byte-identical; hashes are in `artifacts/parity/comparison.json`. These images
include generated floor plans, town streets, businesses, rail and parcels.

The reference branch has no tracked CODING-AGENT-PROMPT.md. It was not fabricated
from the unrelated untracked prompt folders.
