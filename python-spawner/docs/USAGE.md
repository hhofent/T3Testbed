# Python spawner controls

Run from this folder with Python 3.11 or newer:

    py -m pip install matplotlib pytest
    py cli.py --serve

Open the printed loopback address (default http://127.0.0.1:8765).
The HTTP server uses only the standard library; maps use the existing matplotlib
renderer. Controls come from settings.py, including advanced numeric stage packs.
Sliders and numeric inputs share bounds; compound range settings show both ends.
Locked algorithm invariants remain visible but disabled. Errors appear above the map.

The page has stage switches, seed random/lock, stacked presets, JSON download/load,
changed-settings view, per-setting reset, reset-all, and 300 ms trailing redraw.
Saving JSON downloads a file; it does not write arbitrary server paths.
N12 downtown share controls relocation to highway strips without changing the
researched business mix. N2 trees are small yard markers with no canopy fills;
parity rendering can retain reference grove drawing for historical comparison.

    py cli.py --preset tiny-towns --preset rail-heavy --param region.rail_pct=80 --diff
    py cli.py --preset trailer-heavy --save-preset my-preset.json
    py cli.py --preset my-preset.json --seed 14 --png out/town.png
    py cli.py --sweep region.rail_pct 0 100 5 --seeds 10 --csv out/rail.csv
    py -m pytest tests -q

All preset strengths, control limits, and newly introduced geometry values are
labeled PLACEHOLDER in the settings notes. N9/N13 raw parity exceptions and the
missing source prompt are recorded in SETTINGS-AND-PARITY.md.
