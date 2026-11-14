# Checkpoint

## Status
Milestones 1-5 complete. Endless mode (--world, --window) adds global contours,
south-flowing tributaries merging into wider trunks, historical section roads
(paved/gravel/Level B/missing), road-aligned rail, and owned reference towns/farms/
acreages. River-aware ownership and full canonical features preserve overlaps.
Control page has world mode, drag/button pan and wheel/button zoom. 73 tests pass,
including negative-coordinate overlap/order/immutability tests. JS syntax passes.
Usage and simplified placeholder historical model documented in docs/USAGE.md.

## Exact next step
Milestone 6: --verify determinism, overlap equality, building collision checks,
road connectivity, crossing angles >=45 degrees and >=80% within 15 of square,
and no marker intersecting roads/buildings/drives/rail; finish tests/docs and push.
