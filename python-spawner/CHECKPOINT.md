# Item A2: river geometry, 2026-11-14

Start: `6725de2` (v0.10) on `python-test/spawner`. Push only that branch.
Baseline before any A2 edit: `py -m pytest -q` 112 passed in 59.94s.
`py checks/run_all.py --quick` passed in 44.68s (seed 13, 9 mi: entropy 0.884,
E-W 14.6%, sinuosity median/spread 1.771/0.574, order-1 width 12.39-15.00 ft;
crossings/bridges 75/75; chunk hash match, 12 features; outlet SD 28.78°).

## Checklist

1. Versioned chunk cache — done. See below.
2. Curved valley paths — next. `checks/river_valley.py`, seeds 14/30/31, 40 mi window `(-20,-20,40,40)`. Median valley sinuosity after 0.5 mi smoothing 1.15-1.35; edge length p10/median/p90 inside 0.6-1.5 mi; share of edges with near-zero curvature over more than 1 mi under 5%.
3. Varied meanders, no kinks — `checks/river_meander.py`. Per-reach local-wavelength CV >= 0.25; non-junction nodes with heading change over 35° = 0; order-1 reaches with amplitude over 20x channel width = 0.
4. Junction angles — `checks/river_junctions.py`. All joins, unfiltered, last 0.3 mi vs receiving channel. Seeds 14/30/31: mean 55-75°, under 25° < 5%, minimum >= 10°.
5. Channel spacing — `checks/river_spacing.py`. Order >= 2 channels that do not join within 0.5 mi stay at least 0.4 mi apart. Violation count 0 on the 40 mi window; print the worst pair.
6. Real copy test — resample each branch over 3 mi, compare normalised curvature profiles, flag correlation >= 0.9. Must fail on `b2ee52f` and on a same-recipe sine pair, and flag under 5% on the new network.

## Part 1: versioned chunk cache — done

Cache path is `out/cache/<VERSION>-<short git hash>/<seed>/<lod-key>/<x>_<y>.svg`.
Hash is `nogit` when git cannot be run. README says old `out/cache/*` folders can be deleted.

Before (on `6725de2`): `tests/test_chunks.py::test_versioned_chunk_cache_path_contains_version_and_hash` failed. Built path `out/cache/864201/<lod-key>/0_0.svg` (and the halo tiles) did not contain `v0.10-6725de2`.

After: that test and `test_cache_version_tag_uses_nogit_when_git_is_unavailable` passed (2 passed in 0.17s).
`py -m pytest -q`: 114 passed in 61.40s.
`py checks/run_all.py --quick`: passed in 43.96s, same seed-13 metrics as the baseline (entropy 0.884, sinuosity 1.771/0.574, 75/75 bridges).

Next: part 2, curved valley centrelines. Write `checks/river_valley.py`, show it failing, then change `world.py`.

---

# Item A WIP: basin rivers and check harness, 2026-11-14

Starting head confirmed as `b2ee52f`. The old `drainage_*` templates and their
lane-bend test are removed. Rivers now use rotated 32-mi Voronoi basin cells,
seeded outlet routing and terrain potential, local acyclic headwater graphs,
Strahler order, area-based widths, pinned meanders and seeded junction angles.
Outlet-route widths propagate through downstream joins. `crossing_point` handles
collinear overlap. Chunk-order, topology, bridge, metric and lattice checks live
in `checks/`; `py checks/run_all.py`, `--quick` and `--quick --seeds 13` all
completed successfully.
`check-results/` is ignored; `checks/example-summary.md` is the committed sample.

Fresh normal suite: `py -m pytest -q` — 111 passed in 54.97s. The finished-map
smoke was measured on b2ee52f at seed 13, 4x4 mi: bearing entropy 0.701,
sinuosity median 1.0 and spread 0.0, so it fails the new-map thresholds.

Full checks (`check-results/20261114-140501-467000/summary.md`): mean density
1.427 stream-mi/sq-mi; pooled sinuosity median 1.736, p10-p90 spread 0.463;
pooled N3/N4 3.471; per-basin N3/N4 mean 4.896 (range 2.40-10.00); junction
mean/SD 65.26/24.87 degrees, 7.69% under 25 degrees, largest 10-degree bin
17.16%; copy rate 0/397; order-1 widths 12.39-15 ft and largest trunk 150 ft;
zero measured confluence-width violations. Per-seed density ranged 0.948-1.771.
Five-seed topology/crossing checks passed; 276/276 bridge decks matched; shuffled
3-mi chunks matched under both PYTHONHASHSEED values (12 features); 20/20 outlet
crossing positions were unique and minimum regional bearing SD was 28.67 degrees.

Still open in A: individual basin N3/N4 values exceed the 3-5 range even though
their mean passes; seed 13's 100-sq-mi density is 1.771. The 20/70/10 growth
labels do not yet control branch topology. Channelized reaches, minimum spacing
between neighboring river limbs, road/rail clearance and crossing bends, and
zoom-based meander hiding remain unimplemented or unmeasured. Direct node-to-node
edges in seed 13 basin (0,0) measured 464 edges, 1.181-mi minimum, 1.530-mi
median and 2.644-mi maximum; the 0.6-1.5-mi edge target is not met. Continue
tuning and measuring these before calling A complete.

---

# Final handoff: drainage-first rebuild, 2026-11-14

Items 1-7 are implemented and pushed in order. Important item-6 limitation:
6-10 mi is the candidate rail-station spacing; floodplains / junction competition
can omit stations and leave larger gaps between ACCEPTED towns. Do not describe
that upper bound as guaranteed for every accepted pair. All requested screenshot,
shared-style and baseline work is complete. This section supersedes WIP below.

Implementation commits: 1 e7f582d (checkpoint edc4b44), 2 f0164fd, 3 b94b173,
4 f834d61, 5 2d14e32, 6 95f48f5, 7 840b56b. Shared style and review fixes ac2e682;
this final commit adds round water caps (removes antialias seams between reaches),
final baselines and real-browser evidence. Cache lod-v25-channel-joins.

Fresh full test run: py -3.11 -m pytest python-spawner/tests -q --tb=short
109 passed in 59.93s. Five-seed geometric verifier (13,14,30,31,46) all passes;
artifacts/final-verification.json and final-validation.txt record results.
Real Chromium same-rectangle SVG vs PNG: seed31 (-4,-3,10,10) mi, 800px:
RGB MAE 0.583765/255, severe-pixel fraction 0.0001078125 (0.0108%). Exact palettes
and stroke coverage also checked in pytest. Browser minimum strokes remain
water 1.498px and rail/paved 1.2px at 24/80/256px chunk raster sizes. No far-zoom
palette or visibility-rule change: gravel still drops above 48 mi; simple tree
markers and in-block trailers stay, no trailer parks. Rural lot shape rules stay.

Parity: tools/check_images.py freshly regenerated all five byte-identical LEGACY
pairs and five NEW world baselines. World changes are intentional: branching basin
geometry/valleys, drainage-informed continuous highways, 24-mi corrections,
bridges, two rail directions, route-owned towns and trackside industry, and farm
density/access. WORLD_STYLE supplies the shared export/browser palette, screen
minima and layer order. World exporter snapping was disabled to match browser
subpixel positions. Five world PNG checksums match world-baselines.json; a separate
fresh seed31 regeneration matches. Legacy parity does not test the changed world.

All seven final screenshots are from the real browser, seed31, Ready and loaded:
- artifacts/final-seed31-x-4.5-y-1.5-3mi.png
- artifacts/final-seed31-x-4-y-3-10mi.png
- artifacts/final-seed31-x-15-y-15-30mi.png
- artifacts/final-seed31-x-48-y-48-96mi.png
- artifacts/final-seed31-x0.85-y0.6-rail-road-crossing.png
- artifacts/final-seed31-x-1.5-y22.5-paved-correction-jog.png
- artifacts/final-seed31-x-3.4-y-0.3-bridge.png
96 mi: 1,024 loaded visible chunks, zero transparent center pixels, no server errors;
see browser-overview-seed31-x-48-y-48-96mi.json. Cold 96-mi generation still takes
minutes; no speedup is claimed. Screenshot checksums: final-screenshots.json.
Full human-readable report: artifacts/final-report.md. Temporary servers and the
owned browser tab were closed after capture. Restart cli.py --serve to inspect.

No PR, merge, force push or history rewrite. Only python-test/spawner was pushed.
Unrelated root newprompts/ and reference/ left untouched. Read only the files
named in the original resume allowlist below if continuing work.

--- Prior checkpoint ---
# Final verification WIP, 2026-11-14

Numbered stages 1-7 are implemented and pushed in the commits listed below.
This follow-up fixes issues found in real-browser review and unifies rendering:
- Tributary sources/joins now stagger by stream ID instead of sharing fixed
  stations. Topology, downhill, unique-shape and sinuosity regressions pass.
- A floodplain-blocked rail elevator rejects that town candidate; accepted rail
  towns have both elevator and depot. Industry reservations also protect rural lots.
- E-W paved survey rectangles remain continuous across correction corners;
  the strengthened test includes the negative/zero boundary that exposed a gap.
- WORLD_STYLE in world.py supplies colors, screen minima, LOD hiding and layer
  order for PNG export and browser. Raw SVG carries style attributes; the browser
  applies them generically. PNG disables Matplotlib snapping and matches SVG
  layer order. Legacy reference PNG rendering is unchanged.

Fresh full run: 109 passed in 49.22s. Fresh geometric verifier: seeds 13,14,30,31,46
all pass (artifacts/final-verification.json). Real Chromium same-rectangle PNG/SVG
comparison: seed31 [-4,-3,10,10] mi, 800px, RGB MAE 0.591145/255; only 0.0136%
pixels differ by >40 (antialiasers differ). The test limit is MAE 1.5 / 1% severe;
pytest also strictly checks colors and subpixel line coverage in one rectangle.
Measured farm densities: seed14 1.58, seed31 1.4575, seed46 1.4975 / sq mi.

Current cache lod-v24-continuous-ew-survey. Fresh server on 8004. Real browser tab
owned by this session, current 30-mi view generating. Final required screenshot
collection and world rebaseline are IN PROGRESS; do not claim complete yet.
tools/check_images.py now also writes five canonical world PNGs and their style /
geometry manifest under artifacts/parity, after checking unchanged legacy pairs.
World rebaseline changes are intentional: basin topology/valleys, main routes,
corrections, bridges, both rail directions, route towns, farm density and access,
and corrected export palette / line widths. Only legacy mode remains byte-identical.

Remaining: finish/save current-build 30 and 96-mi screenshots, ensure all seven
requested views are fresh/decoded; finish image script; record browser checks,
commit and push final artifacts/checkpoint. No further numbered stage is pending.
Town spacing caveat: 6-10 mi describes candidate rail stations; floodplains or
six-mile junction competition can omit stations and leave larger accepted gaps.
Read only files named below. Push only python-test/spawner; no PR/merge/rewrite.

--- Prior checkpoint ---
# Item 7 complete, 2026-11-14

Stable world farm ownership thins the old 2.7/sq-mi placement to about 1.5/sq mi,
without changing rural lot shapes, field subdivision or acreage placement.
Farm-only access fractions position the existing yard along a measured 250-830 ft
road-to-house lane; outbuildings, grove dots and chamfer rules are retained.
The default rural.layout API keeps historical behavior unless world mode supplies
an access fraction. A few close same-road yards can share a seeded entrance and
60-ft front connector; old private road entries are trimmed, water is checked,
and the resulting complete access paths remain <=830 ft. Overview stays farm dots.

Fresh full run: 108 passed in 53.29s. New tests measure 1-2 farmsteads/sq mi for
seeds 14/31/46 over 20x20 mi, inspect real generated access polylines against their
250-830 ft lengths, and test shared entrance/trim ownership. Cache v21.

Numbered implementation stages 1-7 are complete, with the item-6 caveat that
6-10-mi town stations can be omitted by junction competition or floodplains.
Commits: 1 e7f582d / edc4b44, 2 f0164fd, 3 b94b173, 4 f834d61, 5 2d14e32,
6 95f48f5; this commit 7. REMAINING REQUIRED WORK: one shared PNG/browser style
table and same-rectangle verification; changed world image rebaselines with
explanation; fresh real-browser 3/10/30/96 mi and rail-road / correction-jog /
bridge screenshots. Existing server 8002 is stale item-1 code; restart for captures.
Read only files named below. Push only python-test/spawner, no PR/merge/rewrite.

--- Prior checkpoint ---
# Item 6 complete, 2026-11-14

Town owners now follow seeded stations along N-S and E-W rail: candidate station
intervals are 6-10 mi (8 +/- seeded jitter). Highway towns are independently
thinned along paved routes, and six-mile local priority competition prevents
close rail/highway pairs. Junction competition and floodplains can omit a station;
6-10 mi is the along-line candidate spacing, not a guarantee across omitted sites.
Towns retain region block/lot variety, edge thinning, 60/30/10 tiny/mid/big weights,
70/30 downtown/highway commercial arrangement, and in-block trailer runs. Town
plats rotate with the rail/road; main streets are paved inside the envelope.
Rail towns export the previously invisible elevator/industrial parcel and depot
beside the track. Urban driveways and simple tree markers use the existing plans.
Rural tile town lottery is disabled; farm lots intersecting a town envelope are
excluded as a group without changing their layouts. Canonical site caching keeps
ownership independent of query order. Cache lod-v20-route-towns.

Fresh full run: 107 passed in 48.15s. Added station spacing/jitter, accepted town
minimum distance, skewed sizes, off-rail towns, and elevator/depot presence tests
across an 80x80 mi sample. Existing overlaps, town spacing, buildings and tree
checks pass. No final screenshots yet.

Completed commits: 1 e7f582d / edc4b44; 2 f0164fd; 3 b94b173; 4 f834d61;
5 2d14e32; this commit 6. NEXT item 7: measured farm density currently 2.69-2.77
per sq mi, so reduce via stable world placement ownership, lengthen access lanes,
and add small shared-lane clusters. Keep rural lot shapes/layout rules otherwise.
Then shared style / same-rectangle test, world rebaselines and final browser
3/10/30/96 mi + crossing/jog/bridge screenshots. Read only files listed below.
Push only python-test/spawner, no PR/merge/history rewrite.

--- Prior checkpoint ---
# Item 5 complete, 2026-11-14

Rail has N-S and E-W route owners with seeded activation and gentle C2 bends
between straight crossing approaches. Horizontal rail approaches align with the
corrected N-S road coordinates; vertical rail aligns with E-W section lines.
Both share exact canonical section endpoints. Default spacing is 20 mi at 70%
activation in both directions, targeting roughly 29 rail route-mi per 400 sq mi;
a 400x400-mile regression checks the measured range. No diagonal is forced.
Rail runs beside the road corridor, leaving room for paved correction curves.
Industrial town siting follows in item 6. Bridge generation covers both axes.

Fresh full run: 106 passed in 41.71s. Tests check both directions, density,
curvature and shared endpoints, plus >=45-degree crossings and >=80% within
15 degrees of square for three seeds around correction boundaries. Updated old
straight-only expectations to the newly requested gentle curves. Cache v19.

Completed: 1 e7f582d / edc4b44, 2 f0164fd, 3 b94b173, 4 f834d61; this commit 5.
NEXT item 6 towns, then 7 farmsteads. Shared PNG/browser style and world parity
rebaseline still due, as are final real-browser captures. Read only files named
below. Push only python-test/spawner, no PR/merge/history rewrite.

--- Prior checkpoint ---
# Item 4 complete, 2026-11-14

Bridge decks now belong to (carrier road/rail ID, canonical river segment ID).
Segment intersection locates crossings and interpolates tapered water widths;
deck length accounts for the crossing angle. All paved roads and rail bridge,
and gravel/dirt bridge too: this policy preserves every surviving frontage road
without water closures. Candidate segments use one-mile bins. Deck ownership is
independent of request/enumeration order, and decks are drawn over water.

Fresh full run: 105 passed in 38.64s. Added an actual 12x12-mile exhaustive
crossing-to-deck test plus synthetic paved/rail and reversed-order checks.
Cache lod-v18-owned-bridges. Final bridge screenshot pending fresh final server.

Completed commits: item 1 e7f582d + edc4b44; item 2 f0164fd; item 3 b94b173;
this commit completes item 4. NEXT item 5 rail in both directions and density /
angle checks, then 6 towns and 7 farmsteads. Shared style, world rebaselines and
final requested browser captures remain due. Read only files listed below.
Push only python-test/spawner, no PR/merge/rewrite.

--- Prior checkpoint ---
# Item 3 complete, 2026-11-14

All N-S survey roads now shift sideways at globally anchored 24-mile correction
parallels. Seeded integer-tick offsets alternate sides, so no correction is
accidentally zero. E-W correction roads stay continuous even at maximum missing
road settings, providing the connecting jog for gravel as well as pavement.
N-S paved geometry incorporates the same offsets and retains exact shared route
endpoints and wide corners. Region placement uses those corrected N-S road
coordinates; rural layouts are unchanged. The gap verifier tests physical
coordinates rather than expecting an off-viewport corrected line in the view.

Fresh full run: 104 passed in 40.46s. New test covers negative and positive 24-mi
boundaries, seed variation, actual shifted survey sections, continuous E-W edges,
and exact paved joins. Cache lod-v17-correction-lines. No final screenshots yet;
server on 8002 still has the item-1 code and must be restarted before final capture.

Completed item commits: 1 e7f582d + checkpoint edc4b44; 2 f0164fd; this commit 3.
NEXT item 4 bridges, then 5 rail, 6 towns, 7 farmsteads, shared style and world
rebaseline, final browser screenshots. Read only files listed below. Push only
python-test/spawner; no merge, force-push, history rewrite or PR.

--- Prior checkpoint ---
# Item 2 complete, 2026-11-14

Paved routes are planned from drainage before missing gravel edges. Each owns
continuous 24-mile sections with exact shared endpoints, one/two-mile crossroad
jogs and 950-foot rounded corners. Route spacing stays nine miles; the seeded
shared band shift chooses the approach away from drainage junctions. Retained
survey-edge IDs support addresses and deletion verification; their ends trim
at curved corners. Missing-edge sampling never removes pavement. Town candidates
still use the existing region placer pending item 6. Rejected town lots with no
frontage street now skip safely instead of raising StopIteration.

Fresh full run: 103 passed in 39.55s. Added 70-mile route continuity, curved-jog,
missing-route and missing-section checks; preserved the old deleted-edge test.
Sample over both axes in 96x96 mi: 82.53% gravel, 12.11% paved, 4.48% Level B,
0.87% missing. The verifier inspects actual polylines and paved survey sections.
Cache version lod-v16-continuous-paved. Existing far-zoom stroke values retained.

NEXT item 3: seeded 24-mile correction offsets for ALL N-S section roads, continuous
E-W roads and paved jog integration. Then items 4-7, shared style, world rebaseline,
and final browser screenshots. Item 1 commits e7f582d / edc4b44. This commit is item 2.
Read only the files listed below. Push only python-test/spawner, no PR/merge/rewrite.

--- Prior checkpoint ---
# Item 1 complete: e7f582d, 2026-11-14

Resumed at f397d8b on python-test/spawner. Drainage uses coarse basin owners
H(seed,'basin',rx,ry), shared boundary anchors, southwest shear, seeded spacing,
region-specific trunk direction and bends, and independently seeded tributary
and headwater meanders. Three orders (1/2/3) widen at confluences; tributaries
start small and taper downstream. Every downstream endpoint belongs to its
receiving reach. Terrain measures distance to ALL channels, using spatial bins.
World make_region receives the global water network as its placement barrier;
it never calls old per-tile make_creeks. Town blocks clip at the floodplain.

Fresh full validation: 102 passed in 31.33s. Seeds 13,31,46 pass continuity,
acyclicity, downhill and no-crossing checks over 40 mi. New tests check order,
widths, angled joins, unique normalized shapes, median sinuosity of 1.6-2.5,
shared basin endpoints, valley elevations, and forbidden legacy creek calls.
All five regenerated legacy parity PNG pairs remain byte-identical. Those
exercise unchanged legacy region mode; world rebaselines and shared PNG/browser
style verification are still pending with the later stages. Real Chromium:
artifacts/item1-seed31-x-1-y0-3mi.png (Ready and decoded; town/road context).

NEXT: item 2 paved routes, then 3 correction lines, 4 bridges, 5 rail, 6 towns,
7 farmsteads, in order, commit and push each. Final shared-style/world parity
checks and real-browser 3/10/30/96 mi + crossing/jog/bridge captures remain due.
Server started on port 8002; restart after Python changes. Read only the files
named below. Root newprompts/ and reference/ remain untouched. Push only
python-test/spawner; no PR, merge or history rewrite. Checkpoint encoding is
UTF-8: use explicit encoding when writing it. This follow-up records e7f582d
because the first checkpoint write hit a Windows encoding error.

--- Prior checkpoint ---
# Item 1 verification preparation â€” WIP, 2026-11-14

Resumed at b282432 on python-test/spawner. Item 1 is STILL INCOMPLETE;
items 2-7 have not been started. This WIP changes verification only, not geometry.
The next implementation step remains the basin replacement described below.

Extracted drainage_topology_checks: directed downstream ownership may join an
interior canonical vertex of a receiving reach, instead of requiring its first
vertex. Missing/wrong receivers still fail. Added iterative cycle detection so
connected endpoints cannot disguise a cyclic drainage graph. Query-halo boundary
receivers are checked only for selected features; cycles are checked throughout
the supplied graph. Tests cover interior confluences, wrong/missing receivers,
a connected two-reach cycle and a 2,000-reach chain without recursive traversal.

Fresh full test run: 100 passed in 43.47s. Parity: all five regenerated legacy PNG pairs byte-identical. No new browser screenshots: this WIP changes no rendering. The requested
3/10/30/96 mi and crossing/jog/bridge screenshots remain pending; historical
screenshots are not evidence of the unfinished overhaul. No numbered item is
claimed complete. Read only the files named by the following checkpoint section.

--- Prior checkpoint ---
# Real-life generation order request â€” WIP, 2026-11-14

Started at 265c044 on python-test/spawner. This section supersedes the previous
"no further implementation steps pending" statement. The seven numbered items
in the new request are NOT complete. Preserve all earlier styling/foliage and
rural layout decisions. Push only python-test/spawner; no PR, merge or rewrite.

Fresh full validation: 98 passed in 40.94s. Legacy parity: all five pairs byte-identical.

Partial item 1 implemented: tributaries now carry monotonically increasing vertex
widths (6% to 50% of trunk width), rendered as tapered SVG reaches. Conservative
maximum widths still protect lots/towns. Branch ids seed a separate bend phase
and frequency. Cache version lod-v14-tapered-branches prevents stale SVG reuse.
These are incremental changes, NOT the requested replacement basin network:
branches still use the trunk scaffold, trunks remain north-south lanes, terrain
still derives from trunks, and branching orders beyond 1 remain outstanding.
The first, stronger perturbation failed the existing downhill verifier; reduced
variation passes the 14 world tests including continuity, crossings and downhill.
New test covers strictly increasing widths and unique normalized branch profiles.

Parity regeneration: five before/after PNG pairs remain byte-identical. That
script exercises legacy region parity, not the changed world renderer. No world
rebaseline or requested browser screenshots have been produced yet. Do not claim
that unchanged legacy parity proves browser/PNG style agreement.
Team source /workspace/pytest-check3/review/realism-factcheck.md is unavailable on
this Windows machine; use the numbers in the user's request.

Next: finish item 1 basin topology, independently owned geometry, variable trunk
direction/spacing, downstream joins, stream order and terrain from all streams;
then items 2-7 in order, committing/pushing each with test results here. Shared
PNG/browser style table and same-rectangle output test remain required. Real
browser screenshots at 3/10/30/96 mi plus actual rail-road crossing, paved
correction-line jog and bridge remain required; filenames must carry seed/coords.

Read only these files as needed on resume (paths relative to python-spawner):
- world.py, chunks.py: world topology, geometry, rendering, caching.
- region.py, rural.py, settings.py, presets.py: integration boundaries and options;
  rural lot layout stays untouched except the requested world placement interface.
- map_render.py, control.html, control.py, cli.py: shared style/export and browser.
- verification.py, tests/test_world.py, tests/test_chunks.py,
  tests/test_control.py: geometry and regression checks.
- tools/check_images.py, tools/browser_flow.js: parity and real browser checks.
Run py -3.11 -m pytest python-spawner/tests -q and
py -3.11 python-spawner/tools/check_images.py. The root untracked newprompts/ and
reference/ folders are unrelated and must remain untouched.

--- Earlier checkpoint follows ---
# Completed fixes 2-4, 2026-11-13
Fix 2: f1eedc7. Match chunk raster dimensions to CSS display size on every zoom;
minimum strokes survive SVG image rasterization. Gravel drops above 48 mi.
Fix 3: c3f24bf. Straight fixed-offset rail; no per-mile sideways excursion.
Fix 4: lane-specific continuous phase/amplitude variation in global coordinates;
terrain follows those valleys. Tributaries share the trunk sampling lattice,
join at staggered stations with a vanishing lateral slope, and keep exact
canonical downstream endpoints. Cache version lod-v13-varied-rivers.

Validation: full pytest 97 passed in 31.85s. Five regenerated parity pairs
byte-identical. Full geometric verifier passed seeds 13,14,30,31,46.
Real Chromium regressionZoomPanCache and regressionDistantChunks passed (both
seeds, all ten distant locations). regressionStrokePixels measured water 1.498px,
rail and paved road 1.2px at 24/80/256px chunk sizes.
Fresh server, seed 14: Ready and decoded at 3/30/48/96 mi and x=90 mi.
96 mi: 1024 visible decoded chunks, zero transparent chunks.
Screenshots: artifacts/fix2-4-3mi.png, fix2-4-30mi.png, fix2-4-48mi.png,
fix2-4-96mi.png, fix2-4-90mi-away.png; artifacts/fix3-rail-crossing.png
uses rail_enabled_pct=100 to expose the straight offset crossing.
No user acceptance is inferred. Cold overview generation latency remains.
Push only python-test/spawner. No merge, force push, history rewrite or PR.
No further implementation steps pending; restart the server to load this version.

--- Previous checkpoint and history follow ---

# Python spawner checkpoint

## Previous handoff, 2026-11-13: fix 1 complete; fixes 2-4 pending
Started at 01b2368 on python-test/spawner. Read this section before historical
status below, which describes earlier work and does NOT establish acceptance of
the latest four reported issues.

Fix 1 root cause: chunk SVG geometry used absolute internal coordinates
(1 mile = 1,013,760 units). Chromium/Skia silently renders distant SVGs transparent
even though image.decode() succeeds and HTTP returns full geometry. A 90-mile
view reproduced Ready + blank decoded SVG. Rebase every chunk's actual geometry
to a local origin before serialization (not a cancelling SVG transform). Chunk
cache version is lod-v11-local. Browser town parsing restores absolute positions.
No rural lot, town, tree, or legacy parity renderer behavior changed.

Inspected real Windows Chromium page console/network: /chunks HTTP 200, no console
errors; request bodies a few KB, far below controls.max_request_bytes=2 MB.
Known IDs and image keys agree, visible images survive trimChunkImages, and server
JOBS protects visible entries even beyond tile_cache_size=128. Those were not the
cause of the distant transparent geometry. Cold 96 mi does take several minutes
with the single worker; it eventually reached Ready with all 1024 visible images
decoded and zero transparent chunks (pixel checks). This latency is not fixed.
Real page regressionDistantChunks() passed seeds 14 and 2113558218 at (+60,0),
(-60,0),(0,+60),(0,-60),(198,198) miles. Ready 3/30/48/96 mi views checked.
Screenshots in artifacts/: fix1-3mi.png, fix1-30mi.png, fix1-48mi.png,
fix1-96mi.png, fix1-90mi-away.png. These show fix 1, not final LOD/rail/river work.
Full pytest: 96 passed in 33.17s. Added far SVG local-coordinate regression for
both seeds at -198,-90,40,90,198 mi. tools/check_images.py regenerated all five
parity pairs, all byte-identical; no parity artifact diff.

## Previous next step (completed below)
Implement fix 2 (zoom-based line widths) BEFORE fix 3 (straight offset rail,
square road crossings, remove sideways easing bulges) and fix 4 (nonrepeating
continuous meanders, downhill joins, no fan seams). Those are NOT completed.
Read only these files as needed on resume:
- control.html: imageKey/lod/paintChunks/redrawChunks and encoded SVG image cache.
- chunks.py: generate/request/identity and local SVG serialization.
- world.py: svg(), rail_lanes/rail_points, drainage_* / rivers / elevation.
- tests/test_chunks.py, tests/test_world.py, tests/test_control.py.
- verification.py: crossing_angles and river_checks.
- tools/browser_flow.js: browserFlow, regressionZoomPanCache, regressionDistantChunks.
- tools/check_images.py: five parity regeneration command.
- control.py and cli.py: loopback server and request limits.
Run py -3.11 -m pytest python-spawner/tests -q and
py -3.11 python-spawner/tools/check_images.py. Start a fresh server after Python
changes: py -3.11 python-spawner/cli.py --serve --port 8001. A server from this
session may still be listening on 8001; restart it to reload code.
Potential fix 2 approach: SVG image intrinsic raster size vs CSS chunk size must
match, otherwise even vector-effect strokes shrink after the SVG is rasterized.
Consider screen-size-aware serialization/paint, with LOD-aware image identity.
At 96 mi gravel should drop before paved roads/rail/rivers/towns. Add meaningful
pixel-width tests and replace screenshots after final changes.
Commit + push each completed fix only to python-test/spawner, update this file.
No merge/force push/history rewrite/PR. Unrelated untracked root files untouched.

## Historical checkpoint (superseded where noted above)

## Status
Navigation fix: reproduced the zoom-out / pan / zoom-in blank map in the real
control page. After a large overview/long session, FIFO browser-cache trimming
could evict the current detail SVG AFTER advertising it to /chunks as known.
The server correctly omitted its SVG; the page incorrectly settled with a hidden
tile. Visible image keys are now protected during trimming; stale revisions cannot
start requests. The same populated-cache sequence failed before (Ready, hidden
image, missing current cache key) and passes after, with actual HTTP and SVG decode.
Regression: tools/browser_flow.js regressionZoomPanCache(), plus an automated Node
check of the shipped cache routine in pytest. New-area negative-coordinate flow
also passes. Full suite: 95 passed. Node is needed for the browser-script unit test.

Find a town tested in the real page at defaults: found two nearby towns, visited
at 0.50 mi detail, with next/previous enabled. Seed 31, 36x36 mi sample: five towns,
nearest-neighbor distances 9.00,9.00,9.03,11.95,17.78 mi. Spacing regression added;
town layouts and probabilities unchanged. All five regenerated parity PNG pairs
(seeds 13,14,30,31,46) remain byte-identical. Geometric verifier passes all five
seeds, including actual paved geometry, drainage, rail angles and overlaps.
Rural layout code was left alone. No merge, force push or PR.

River fix: canonical endless trunks now have four times as many bends and four
times the samples. Smooth longitudinal compression produces tight oxbow-like
hairpins while preserving strict downstream order (no closed/self-crossing loops).
Tributaries span 85-94% of the default eight-mile interval and merge into wider
trunks. Existing amplitude/period controls set bend size. Floodplain exclusion now
includes the configured creek buffer; roads remain unbroken bridge decks above
water. Fifteen world/verification tests pass, including 40-mile continuity, downhill
and crossing checks for seeds 13,31,46, plus multi-chunk length/neck regressions.

Rail fix: replaced five-point sawtooth deviations with sampled C2 eased curves,
with straight square approaches at every section crossing. Regression checks
heading changes, exact crossing tangents and shared endpoints; 10 world tests pass.

Road fix: paved corridors are selected before missing-edge sampling. Only gravel
and Level B roads can be missing. Overview road strokes have a 0.9 px minimum
(previously 0.3 px), preserving true road width at detail. Added actual paved
geometry gap verification and deletion regression; 13 targeted tests pass.

Fix 1 reworked after the broken 961bf7d browser test. Always endless world;
Find a town / next / previous with adjustable town zoom. Drag and cursor-anchored
wheel zoom are on the visible chunk surface (previously on the hidden PNG).
Default chunks are 3x3 mi, configurable 1-12 mi. Overview skips house plans,
buildings, trees, lots and contour sampling; keeps roads, rivers, town outlines
and farm dots. Full detail is selected at <=2 mi wide. Separate versioned disk
and browser caches prevent LOD mixing. Requests omit SVGs already held by the
browser; existing image nodes survive pans. Visible work outranks halo and
pan-direction prefetch; obsolete queued jobs are discarded. Coordinates have
no geographic bounds; the supported view width is 0.01-96 mi (was capped at 12).
Fix 2 completed: grouped controls, readable names, units and tooltips; known
tick lengths display/edit in feet while presets keep canonical values.
82 tests pass. Seeds 13,14,30,31,46 parity PNG pairs remain byte-identical.

Browser measurements, loopback HTTP + actual control.html JS, 1280x800:
- Before: cold 10x10 mi, seed 896131: 9085 ms, 31 polls, final payload 10,582,099 bytes.
- Before: cached pan JS redraw 12.5 ms; cached request + paint 1153.2 ms.
- Before: zoom to 20 mi impossible (12 mi cap); drag/wheel had no handlers on tiles.
- After: cold 10x10 mi, fresh seed 896135: 958.7 ms including debounce,
  polling, SVG installation and image decode.
- After: cached pan 2.0 ms; cached zoom to 20 mi 3.7 ms, including image decode.
  These measure immediate visible redraw; background debounce/HTTP is separate.
  Final known-chunk response at 20 mi: 2652 bytes including transfer headers.
- cProfile: before 1x1 mi full window 3.731 s / 11.04M calls, _tile 3.580 s,
  spawn_region 2.195 s. After 3x3 mi overview 0.497 s / 1.86M calls,
  no spawn_region calls. Profiles intentionally compare the old smallest display
  chunk with the new larger overview chunk. House plans and redundant tile halos
  were the main CPU cost; resending SVG and replacing nodes added browser work.

Fix 8 done: CLI region exports use explicit axes placement, so off-map parcel
labels cannot shrink the drawing through tight_layout. --world --png writes a
framed raster of the requested rectangle, with automatic overview LOD. The old
reference PNG renderer stays available for byte-identical parity comparisons.
Both exports visually inspected; CLI tests check framing and rectangular size.
Legacy independent creek lines removed from world output: only the verified
watershed network is drawn. River meander period is now in full wavelength units.
Final profile found water/lot collision scanning as the new bottleneck: candidate
streams are bounded to each settlement tile and each parcel before segment tests.
Fix 2 clarified further: map effects in labels, explicit percent units, one-line
tooltips, extended reference notes behind About this setting, leaf labels include
parent setting names. Wheel zoom is continuous and anchored at the cursor.
Final browser seed 896137, before the last water broad-phase optimization:
10x10 mi cold 2272.5 ms; cached pan 0.9 ms, cached 20 mi zoom 2.3 ms (decode).
Two-frame browser checks: cached pan 4.8 ms, cached zoom 46.3 ms.

Final validation:
- 89 tests passed (including real CLI PNG export framing).
- Geometric verifier passed seeds 13,14,30,31,46 over 3x3 mi windows.
- Final optimized 3x3 mi overview cProfile: 0.464 s, 1.60M calls,
  versus 1.440 s / 5.81M before broad-phase water rejection. No house plans.
- Final browser fresh seed 896139: cold 10x10 mi 907.7 ms; cached pan
  1.1 ms, cached 20 mi zoom 2.8 ms (HTTP/page logic + image decoding).
- A second browser run found an all-water-blocked town in prefetched tile
  (7,-1), seed 896138. Empty block lists now mean no town instead of raising
  min(empty) and poisoning subsequent requests. Regression test added; the same
  browser flow now completes (552.4 ms with its existing disk cache).
- Main PNG export and world PNG export visually checked; legacy creek strokes
  that crossed the canonical drainage network are no longer drawn in the world.

- Latest complete browser flow, fresh seed 896140: cold 10x10 mi 830.4 ms;
  cached pan 1.1 ms; first zoom out to 20 mi (new visible chunks) 749.9 ms;
  cached zoom to 20 mi 2.0 ms. All include image decode; first views include HTTP
  polling and debounce. Reproduce using tools/browser_flow.js in the real page.
- Final five parity before/after pairs are byte-identical after the empty-town fix.
- Find town reaches full detail, next/previous enable after selection, and small
  wheel deltas zoom continuously (0.5 mi -> 0.4950 mi for a -5 wheel delta).

## Historical next step
Latest requested river, road, rail and navigation fixes are complete. User can
test the pushed python-test/spawner head with
`py -3.11 python-spawner/cli.py --serve` (restart an older server to load changes).
Run `py -3.11 -m pytest python-spawner/tests -q` for all 95 tests. For the real-page
cache regression, evaluate tools/browser_flow.js in the page then await
regressionZoomPanCache(). No merge, force push or PR. Unrelated untracked root
folders remain untouched. No new user acceptance is claimed.

## Fix 2, 2026-11-13
Chunk SVGs now rasterize at their displayed CSS size, with screen-space minima for roads, rail, water and town outlines. Gravel drops above 48 mi. Raw SVG cache identity stays reusable; each image rerasterizes on size changes. Full pytest: 96 passed; parity regeneration running.

## Fix 3, 2026-11-13
Removed per-mile rail excursions. Canonical straight offset corridors have square crossings and exact shared endpoints. Updated the old diagonal expectation to require straight geometry. Parity: five byte-identical pairs.
