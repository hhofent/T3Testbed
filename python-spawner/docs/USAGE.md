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

Endless mode:

    py cli.py --world --seed 13 --window -3 -3 6 6 --output out/world.json

This also writes an SVG next to the JSON. On the page, enable Endless world;
use arrow buttons or drag to pan, and +/- or the mouse wheel to zoom.
World coordinates are reference ticks internally; CLI windows are in miles;
positive y runs south. Full canonical features are returned, with stable IDs.
Generation uses globally anchored contour cells, south-flowing river/tributary
segments, historical section edges, road-aligned rail lanes, and owned settlement
tiles using the reference town/farm generator. Towns that would cross their owner
boundary or a trunk/tributary corridor are omitted; lots avoid these rivers and
the reference creeks. Local creeks still shape towns. This is a placeholder survey
history model: aligned wagon corridors upgrade to paved mains, survey sections
fill in with gravel/Level B roads. Missing sections affect only non-main horizontal
edges, leaving continuous vertical routes and preserving network connectivity.
Rail uses straight north-south road corridors, with square grade crossings.
Terrain and network sampling are independent of requested viewport boundaries.
First views may take several seconds; tile results are cached for subsequent pans.
