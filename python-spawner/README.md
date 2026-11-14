# Python spawner

## 1. What it is

This folder is a Python generator for an endless Iowa-like countryside: gravel section roads, paved highways, rail, basin rivers, small towns, farmsteads, and house plans. One local web page lets you pan, zoom, change the seed, and tweak settings. The same code can also write JSON, SVG, and PNG from the command line.

The map is deterministic for a given seed and settings. The same seed always produces the same towns and rivers. 1990 is the normal snapshot year.

## 2. Requirements

Python 3.11 or newer. Check with `py --version` on Windows, or `python3 --version` on macOS/Linux.

Windows (PowerShell), from this folder:

```powershell
py -m pip install -r requirements.txt
```

macOS / Linux:

```bash
python3 -m pip install -r requirements.txt
```

That installs matplotlib, pytest, and pillow.

## 3. Get the code

The live branch is `python-test/spawner`. The default `master` branch is an old stub. Clone, then switch.

Windows (PowerShell):

```powershell
git clone https://github.com/hhofent/T3Testbed.git
cd T3Testbed
git fetch origin
git checkout python-test/spawner
git pull
git log -1 --oneline
```

macOS / Linux:

```bash
git clone https://github.com/hhofent/T3Testbed.git
cd T3Testbed
git fetch origin
git checkout python-test/spawner
git pull
git log -1 --oneline
```

If you already cloned the repo, skip `git clone` and run the fetch / checkout / pull / log lines from the repo root.

The last log line is the commit you have. The control page shows `v0.N (hash)` in the top-right of the header and again at the top of the left-hand panel. The hash on the page must match `git log -1 --oneline`. If it does not, you are on the wrong commit or an old server is still running.

## 4. Run it

From this `python-spawner/` folder.

Windows (PowerShell):

```powershell
py cli.py --serve
```

macOS / Linux:

```bash
python3 cli.py --serve
```

The server prints a loopback URL. The default is [http://127.0.0.1:8765](http://127.0.0.1:8765). Open that in a browser. Stop the server with Ctrl+C.

To pick another port:

```powershell
py cli.py --serve --port 8766
```

```bash
python3 cli.py --serve --port 8766
```

The first view of a new seed can take several seconds. A 96-mile overview can take minutes on a cold cache.

## 5. After updating

After `git pull` or any code change:

1. Stop the server with Ctrl+C and start it again. An old process keeps the old Python in memory.
2. Hard-refresh the browser. Windows: Ctrl+F5. macOS: Cmd+Shift+R.
3. Delete the cache folders listed below. The disk cache reuses old chunk SVGs even after a restart.

Windows (PowerShell), from this folder:

```powershell
Remove-Item -Recurse -Force out\cache, __pycache__, tests\__pycache__, checks\__pycache__, tools\__pycache__ -ErrorAction SilentlyContinue
```

macOS / Linux:

```bash
rm -rf out/cache __pycache__ tests/__pycache__ checks/__pycache__ tools/__pycache__
```

Places this code writes cached chunks, tiles, SVGs, or JSON:

- `out/cache/<seed>/<lod-key>/<x>_<y>.svg` is the chunk SVG cache. `chunks.py` writes here, using a sibling `.tmp` file then renaming. This is the folder that makes the map look stale.
- `out/region.json` is the default CLI export. `--world` also writes `out/region.svg` next to it. `--output` and `--png` override those paths.
- `out/sweep.csv` is the default parameter-sweep table (`--csv` overrides it).
- `out/batch.json` and `out/sample_<id>.report.json` come from `batch.py`.
- `reference_out/layout_stats.json` and `reference_out/full_stats.json` come from `export_reference.py`.
- `check-results/<timestamp>/metrics.json` (plus `summary.md` and failure PNGs) come from `py checks/run_all.py`.
- `artifacts/parity/comparison.json` and `artifacts/parity/world-baselines.json` come from `tools/check_images.py`.

Restarting the server also drops the in-memory tile table in `chunks.py` and the `@lru_cache` tables in `world.py`. A hard refresh drops the in-page `chunkImages` map.

## 6. Using the viewer

The page is the map on the right and the settings panel on the left. Status text above the map reads `Loading settings...`, `Generating visible chunks...`, or `Ready / <width> mi wide / detail|overview`.

**Header**

- Seed. Integer that picks the whole world. Default 31. Changing it rebuilds the view.
- Random. Picks a new seed.
- Lock seed / Unlock seed. Keeps the seed fixed while you move sliders. Random is disabled while locked.
- Find a town. Widens the view until a generated town is in the loaded chunks, then zooms to the nearest one. Towns must be on in Generation stages. If none are nearby it says so.
- Previous town / Next town. Cycle the towns Find a town collected. Disabled until a search finds at least one.
- Town zoom (mi). Width of the view after Find / Next / Previous. 0.05 to 2, default 0.5.
- West, North, South, East. Pan by a fraction of the current view.
- + and -. Zoom toward or away from the center.
- Drag the map to pan. Scroll the wheel to zoom around the cursor.

**Left panel**

- Preset list. `default`, `tiny-towns`, `big-towns`, `rail-heavy`, `sparse`, `trailer-heavy`.
- Stack preset. Applies that preset on top of current values. Stacking twice stacks twice.
- Reset all. Restores every setting to the schema defaults. Seed is unchanged.
- Save JSON. Downloads `builder-guy-preset.json` of values that differ from defaults.
- Load JSON. Reads a file in that same format.
- Changed from default. Count and JSON of overrides.
- Setting groups. Generation stages, Endless world and navigation, Towns/roads/countryside, Rail and waterways, Houses and lots, Trees, Validation and repair, Page behavior, Advanced reference rules. Each leaf has a number box and slider, or a checkbox. **About this setting** is the long help. **Reset &lt;name&gt;** restores one setting.

Overview (view wider than 2 miles) draws roads, rivers, town outlines, and farm dots. It skips house plans, buildings, trees, lots, and contour sampling. Detail (2 miles wide or less) draws the rest. Gravel and dirt roads drop out above 48 miles. Coordinates have no geographic edge. Positive Y is south. View width runs from 0.01 to 96 miles.

**Map colors**

| Layer | Color | Meaning |
| --- | --- | --- |
| Background | `#f2f1e6` | Paper / fields |
| Contours | `#b5ba9b` | Elevation traces, detail views |
| Rivers | `#509bc6` | Basin channels. Trunks are thicker than headwaters |
| Creeks | `#67a9cc` | Local town creeks |
| Paved roads | `#525954` | Highways and in-town mains |
| Gravel roads | `#b9a17a` | Section-line gravel. Hidden above 48 mi |
| Dirt / Level B | `#9a784c` | Unimproved section roads |
| Rail | `#685553` | Track |
| Bridges | paved `#525954` or rail `#685553` | Deck over water, colored by the carrier |
| Buildings | `#d6dfc4` | Houses and commercial footprints |
| Driveways | `#c8bea9` | Farm and town drives |
| Trees | `#3c6946` | Trunk markers, not canopy |
| Farms | `#8b7655` | Farmstead dots on overview |
| Towns / lots | fill none, outline `#b5c0ab` | Town envelope and parcel edges |
| Other outlines | `#687060` | Building and road edges |

Draw order is contours, rivers, creeks, towns, lots, roads, rail, bridges, driveways, buildings, trees, farms.

## 7. Tests

From this folder. About one minute:

Windows (PowerShell):

```powershell
py -m pytest -q
```

macOS / Linux:

```bash
python3 -m pytest -q
```

Longer river and chunk checks write a timestamped folder under `check-results/`:

```powershell
py checks/run_all.py
py checks/run_all.py --quick
py checks/run_all.py --quick --seeds 13
py checks/run_all.py --seeds 13,14,30
```

```bash
python3 checks/run_all.py
python3 checks/run_all.py --quick
python3 checks/run_all.py --quick --seeds 13
python3 checks/run_all.py --seeds 13,14,30
```

`--quick` uses smaller windows and fewer seeds (seed 13 unless you pass `--seeds`). `--seeds` is a comma-separated list. Each run writes `check-results/<timestamp>/summary.md` and `metrics.json`.

Other useful commands, still from this folder:

```powershell
py cli.py --verify --seed 31
py cli.py --world --seed 13 --window -3 -3 6 6 --output out/world.json --png out/world.png
```

## 8. Troubleshooting

**Port already in use.** The default is 8765. Start with `--port 8766`, or stop the old `cli.py --serve` process.

Windows (PowerShell), find and stop whatever is bound to 8765:

```powershell
netstat -ano | Select-String ':8765'
Stop-Process -Id <PID>
```

macOS / Linux:

```bash
lsof -i :8765
kill <PID>
```

**Old version still showing.** The page hash does not match `git log -1 --oneline`, or the map looks like yesterday's build. Stop and restart the server. Hard-refresh the browser. Delete `out/cache` and the `__pycache__` folders from section 5. Then confirm the header reads the current `v0.N (hash)`.

**Wrong Python.** `py --version` / `python3 --version` must be 3.11 or newer. On Windows, pin it with `py -3.11 cli.py --serve` and `py -3.11 -m pytest -q`.

**Missing packages.** `ModuleNotFoundError: matplotlib` or `PIL`. From this folder run `py -m pip install -r requirements.txt` (Windows) or `python3 -m pip install -r requirements.txt` (macOS / Linux). Use the same `py` / `python3` you use to start the server.

**Find a town does nothing.** Turn on **Generate towns** under Generation stages.

**Blank tiles far from the origin.** You are on a build older than v0.6, or `out/cache` still holds pre-rebase SVGs. Update, delete `out/cache`, restart.

**Cold 96-mile view sits on "Generating visible chunks..."** That is expected. One worker fills 3-mile chunks. Leave it running, or zoom in. Later pans of the same seed reuse `out/cache`.
