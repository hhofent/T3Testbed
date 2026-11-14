# Real-life generation order request — WIP, 2026-11-14

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
