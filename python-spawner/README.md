# Python Builder Guy spawner

Run `py cli.py --serve` from this folder, then open the printed loopback URL.
Install dependencies with `py -m pip install matplotlib pytest`.

- One settings schema, typed stackable JSON presets and dotted overrides.
- CSV parameter sweeps, deterministic endless windows, pan/zoom controls.
- `py cli.py --verify --seed 31` checks determinism, overlaps, buildings, roads,
  crossing angles and marker clearance. Failure exits nonzero.
- `py -m pytest tests -q` runs the regression suite.

See [usage](docs/USAGE.md), [parity details](docs/SETTINGS-AND-PARITY.md),
[before/after comparisons](artifacts/parity/comparison.json), and
[verification reports](artifacts/verification.json).

1990 is the normal snapshot. Historical byte/image comparisons use explicit
`generation.parity=true` and omit removed trailer-park data, as required by N9/N13.
Reference towns, in-block trailer clusters and ragged edges are retained.
