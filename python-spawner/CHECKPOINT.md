# Python spawner checkpoint

## Status
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

## Exact next step
Fix 3 done: real navigation labels, accessible names and tooltips; all buttons act.
Fix 4 done: coordinate-owned continuous south-draining trunk streams, explicit
source tributaries joining shared vertices, watershed-aligned downhill terrain.
Verifier checks continuity, downhill elevations and crossings over overlapping views.
83 tests pass, including three 40x40 mi drainage checks.
Fix 5 done: world rural lots have seeded chamfered outlines, shifted houses,
seven outbuilding types, varying counts and sizes, connected bent lanes and
irregular shelterbelts on different sides. Seven variety controls added. Reference
parity site plans unchanged. Next: fix 6 rail. Completed fix 4 rivers (continuous downhill,
merging, no crossings/dead ends, verification); 5 rural variety and settings;
6 diagonal/road-parallel rail, crossings >=45 degrees and >=80% within 15 of square;
7 random missing road segments in both directions (~1%), connected network;
8 framed PNG exports including --world. Commit/push each on python-test/spawner.
Only this branch; no merge, force push or PR. Preserve five default parity pairs.
