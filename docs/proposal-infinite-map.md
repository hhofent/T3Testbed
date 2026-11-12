# Infinite map — built architecture and grilling findings

**Status: built and verified (2026-11-11).** `WorldMap.cs` generates the infinite
1990s rural Iowa world; `WorldMap.Verify()` checks it geometrically. This document
started as a proposal and now records what survived the design review, what changed,
and why.

## The goal (user, verbatim intent)

> "the goal is a 3 mile square procedurally generated map that is reproducible with a
> seed and continues on for hundreds of miles when the 'lens' is slowly moved"

Destination: an **infinite, seed-reproducible map** streaming around a slowly moving
lens, built from **3-mile-square cells**. Town density ladder from the user's
calibration (locked 2026-11-11): Hull-class every ~100 miles (the largest a town ever
gets), Boyden-class every ~30 miles, George-class hamlets every ~30 miles on average —
very rare under ~10 miles apart, essentially guaranteed by ~50 miles — farmsteads
scattered a few per section, most of the map empty fields on the 1-mile section grid.

## The main finding: cells are a request unit, not a render tile

The original sketch said each 3-mile cell generates independently from
`hash(seed, cellX, cellZ)`, and a town is stamped into every cell it overlaps. Grilling
that against the hard rules exposed three failures:

1. **Seam duplication.** Two independent cells both draw the section road on their
   shared edge. At best it doubles the stroke; at worst the two halves disagree by a
   pixel and leave a visible line.
2. **Seam clipping.** A farmstead or town near a cell edge is generated differently in
   each cell: one cell has the whole lot, the next has a clipped copy. Houses pop or
   double when the lens crosses.
3. **No global query.** A Poisson/blue-noise town field is easy to generate forward, but
   a window cannot ask "which towns are near this rectangle?" without replaying the
   world from the origin. That breaks random-access streaming.

**Decision: the content function is pure in world coordinates.** A window is generated
as one rectangle, with global roads, creeks, fields, farmsteads and towns clipped to the
window. A 3-mile cell is just a small window; moving the lens never changes what exists
at a coordinate. Cells remain the canonical decomposition and the lens request unit,
not independent images that get stitched together.

## Town field: a queryable 10-mile lattice

Towns cannot be an unknowable Poisson field. The implementation is a lattice with
deterministic suppression:

- One candidate per **10-mile slot** on each **24-mile east-west highway corridor**.
- Candidate hash → class: 10% Hull, 30% Boyden, 36% hamlet, 24% empty.
- Each candidate gets a stable 64-bit priority. A candidate survives only if no
  stronger candidate in the adjacent slots is within **10 miles**. That is a local
  query evaluated the same way from any window, so town spacing never depends on visit
  order. Cross-corridor pairs are at least 24 miles apart by construction.
- Towns are centered in a 1-mile section, and their commercial highway strip sits on
  the section line. The highway through town is therefore a real through-road, and the
  county section grid can be clipped around a known footprint instead of cutting
  through the town.
- The hull preset overhangs one section line by ~58 ft; that is why centering in the
  section, rather than snapping the whole footprint, is the rule.

Seed 1993 over 330 miles: 20 towns, minimum gap 10 mi — 9 hamlets, 9 Boydens, 2 Hulls.

## What each window contains

**Fields.** Every 1-mile section is split into four quarter fills. A per-section hash
picks the split jitter and four palette colors, so fields are stable while neighbouring
windows meet cleanly.

**Roads.** A global north–south section-road lattice at 1-mile spacing, plus east-west
section roads at 1-mile spacing, with every 24th east-west line promoted to a highway
(yellow centerline). The highway is drawn continuously through towns. County roads are
clipped inside a town footprint, where the town street network takes over. Windows clip
roads at their own bounds; the clipping is a render concern, not a content change.

**Towns.** `SmallTownGenerator` is still the leaf generator. The world stamper keeps the
town's streets, alleys, lots, houses, outbuildings, trees and walks; drops the
south-of-highway fringe so the highway is the town's south edge (the George/Boyden/Hull
pattern); and never redraws the through-highway. The town silhouette is whatever the
seeded blob generator produced — never a rectangle.

**Farmsteads.** Each section hashes its own farm count (0–3) and per-index seed. Each
farmstead calls the existing `MapGenerator.RuralLot` leaf, then rotates/mirrors and
places it on one of the section's road frontages. Per-section RNG streams mean adding
or removing content in one section cannot perturb a neighbour. A collision registry
rejects a farmstead that would intersect a town footprint (240-ft pad) or a creek.

**Creeks.** Global vertical ribbons with a low-frequency sinusoidal meander and
tree-lined banks, clipped out of towns and kept clear of section roads. They are drawn
under roads and buildings; bridge decks are not modeled yet.

## Rendering and the lens

`SvgMapRenderer.Render(..., bare: true)` writes an SVG whose viewBox is exactly the map
rectangle: no margin, title, footer or north arrow. Image pixels therefore map 1:1 to a
world rectangle, with world +Z up:

```
x: cx - span/2 … cx + span/2
z: cz - span/2 … cz + span/2
```

This is what makes the browser viewer able to swap windows without guessing at margins.
World windows set `QuietAnnotations` and `SuppressOutbuildingLabels`: farm labels
("MACHINE SHED 40×30", "LP") are noise at nine miles, while the town name and highway
shield remain.

The viewer (`viewer-template.html`) streams live windows from `--serve`:

- `/world?seed=&cx=&cz=&span=` renders any window; `lod=0` gives silhouette LOD.
- `/world-center?seed=` returns `{cx,cz,span}` centered on the nearest town, so entering
  a live seed lands somewhere worth seeing.
- Panning debounces, then requests a window centered on the viewport. The old image
  stays visible until the new one decodes, then the viewport is anchored on the same
  world coordinate, so there is no jump.
- The lens is **zoom-adaptive**: at the default 9-mile fit it streams 9-mile windows; as
  the user zooms in it requests 6/4/2/1-mile windows (server floor 1 mile) instead of
  upscaling a 34-ft-per-pixel image. This keeps houses and captions crisp and is why the
  viewer's zoom readout is stable across window swaps.

## Verification

```
dotnet run -- --verify --seed 1993
```

The ladder in `WorldMap.Verify()` checks:

1. Same window twice is byte-identical.
2. A window 100 miles from the origin is still reproducible.
3. Two overlapping windows agree on every house, outbuilding and tree in the overlap.
4. A 3-mile cell equals the same land inside a 9-mile window.
5. Town ladder: no towns inside 10 miles; Hulls stay rare.
6. No building overlaps another across towns and farms.

Current seed 1993 result: all six pass; 438 houses and 858 outbuildings with zero
overlaps in the 10-mile census. `--map world` writes a 9-mile lens window, a single
3-mile cell, and the self-contained viewer.

## Open limits / next grilling

- Highway corridors are east-west only. Long north-south travel stays on section roads;
  a north-south highway lattice would be the next structural addition.
- Creeks have no bridges or culverts in the drawing; roads simply cross the water.
- Town materialization runs `SmallTownGenerator` for every town that touches a window.
  The HTTP server caches rendered SVG windows, but the generator layer itself has no
  cross-window town cache yet.
- The diagonal rail-corridor street (George's Boiler Ave / Boyden's Railroad St) is still
  an optional town feature, not in the world seed.
- The world viewer is a desktop browser tool; there is no Unity/engine port yet. The
  world query API is deliberately plain C# so the port can call the same
  `(seed, center, span, lod)` function.
