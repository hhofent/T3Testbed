# Python spawner checkpoint

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

## Exact next step
Latest requested river, road, rail and navigation fixes are complete. User can
test the pushed python-test/spawner head with
`py -3.11 python-spawner/cli.py --serve` (restart an older server to load changes).
Run `py -3.11 -m pytest python-spawner/tests -q` for all 95 tests. For the real-page
cache regression, evaluate tools/browser_flow.js in the page then await
regressionZoomPanCache(). No merge, force push or PR. Unrelated untracked root
folders remain untouched. No new user acceptance is claimed.
