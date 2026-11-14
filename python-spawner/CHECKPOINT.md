# Checkpoint

## Status
Fix 1 implemented: configurable display chunks, background generation, memory/disk
SVG cache at out/cache/<seed>/<settings hash>/, visible-first scheduling, halo and
pan-direction prefetch, browser cache redraw and progressive placeholders.
80 tests pass, including chunk persistence, cache identity and <200ms server cache reuse.
Browser redraw timing has not been measured. Cache hash serialization corrected
for range-valued settings; completed memory entries bounded by tile_cache_size.
Town generation and parity rendering unchanged. Only python-test/spawner pushed.

## Exact next step
Continue user fixes 2-8 in order, committing/pushing each and updating this file.
Fix 2: plain-English control labels, units, tooltips and stage grouping.
