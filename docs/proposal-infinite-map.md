# Infinite map — built architecture and grilling findings

**Status: built and verified (2026-11-11, reworked the same day).** `WorldMap.cs` generates
the infinite 1990s rural Iowa world; `WorldMap.Verify()` checks it geometrically. This
document started as a proposal and now records what survived the design review, what
changed, and why.

## The goal (user, verbatim intent)

> "the goal is a 3 mile square procedurally generated map that is reproducible with a
> seed and continues on for hundreds of miles when the 'lens' is slowly moved"

Destination: an **infinite, seed-reproducible map** streaming around a slowly moving
lens, built from **3-mile-square cells**. Everything the land does is a pure function
of world coordinates, so any window asks the same questions and gets the same answers.

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
as one rectangle, with global land, roads, rivers, rail, fields, farmsteads and towns
clipped to the window. A 3-mile cell is just a small window; moving the lens never
changes what exists at a coordinate. Cells remain the canonical decomposition and the
lens request unit, not independent images that get stitched together.

## The land is made in order

The world is built the way land actually makes itself, and the order matters — each
layer is a constraint on the next:

```
1. HILLS    →  2. RIVERS  →  3. RAIL  →  4. ROADS  →  5. TOWNS & LOTS  →  6. FARMS
```

**1. Hills.** A low, long-wavelength elevation field: total relief under ~130 ft over
many miles — Great Plains swells, not mountains. The field is sampled on a globally
anchored quarter-mile lattice and drawn as 20-ft contour lines, so contours are stable
wherever the lens happens to be.

**2. Rivers.** Trunk rivers every ~28 miles run **north to south** with a slow meander
(three sine terms). Tributary heads every ~8 miles start on alternating sides and run
south to **merge into a trunk river**; nothing flows north, and every tributary ends
at a bigger river. Trunk width breathes between ~48 and ~88 ft. Trees line the banks —
the only trees in the world besides farmstead shelterbelts.

**3. Rail.** East-west and north-south rail lines every ~75 miles, jittered a couple of
miles off the lattice and gently curved. Rail crosses rivers where it must. A trunk
river within ~1,200 ft vetoes a town site outright.

**4. Roads.** A paved corridor every **10 miles** (the lattice towns live on), running
both east-west and north-south. Every **mile line** also carries a gravel section road;
~1.8% of mile lines are missing (the exception the user asked for) and ~2.2% of gravel
lines have been paved. County roads stop on a town's perimeter street rather than
running through it.

**5. Towns and lots.** See below.

**6. Farms.** Each section hashes its own farm count (0–3) and per-index seed, then
stamps the existing `MapGenerator.RuralLot` leaf, rotated/mirrored onto one of the
section's road frontages. A collision registry rejects a farmstead that would intersect
a town footprint (240-ft pad), a river bank or the rail.

## Town field: a queryable 10-mile lattice

Towns cannot be an unknowable Poisson field. The implementation is a lattice with
deterministic suppression:

- One candidate per **10-mile slot** on each **10-mile paved corridor** (both
  directions), so a town is always on a paved road.
- A town only exists if a **rail line runs within 9 miles**. The railroad is what
  started the town, and the class ladder is rolled only for rail-adjacent sites:
  4.5% town, 9% village, 18.5% hamlet, 32% crossroads, the rest empty.
- Each candidate gets a stable 64-bit priority. A candidate survives only if no
  stronger candidate in the configured neighboring slots is within **20 miles**. That is a local
  query evaluated the same way from any window, so town spacing never depends on visit
  order.
- Town names come from a generic 40-name pool (`COLD SPRING`, `PINEHURST`,
  `OAKDALE`, …). The old reference-map names are gone — a world of every town called
  Boyden or Hull reads as a bug, not as flavour.
- Sizes are generic: **crossroads** (2 columns), **hamlet** (4), **village** (7),
  **town** (10). The old `boyden`/`hull` preset names were removed with the port.

Seed 1993 over 330 miles: 32 towns, minimum gap 10 mi — 15 crossroads, 11 hamlet,
4 village, 2 town. All 32 are within 16 miles of a rail line.

## Town geometry

The town generator is the leaf; the world stamper places it.

- **Which side of the road.** 46% north, 24% south, 30% straddling. A "Both" town
  gets a second, smaller, mirrored part on the far side of the highway. So towns vary
  between "beside the road" and "through the road" instead of always being one or the
  other.
- **The street loop.** The highway is the town's south side. The outermost north-south
  streets are the west and east sides; they run north to a **perimeter street that
  jogs** — the north edge steps up by one street width at one north-south street. The
  loop therefore encircles the town without ever being an even rectangle. Every
  east-west street reaches both side streets; every north-south street runs from the
  highway all the way to the perimeter.
- **Exactly one dead end.** Each town has one dead-end street (two for the biggest)
  hanging off the perimeter, ending in an asphalt bulb, with four **platted but empty**
  lots along it. That is the only deliberate vacancy in town, and it is what makes the
  town read as real rather than as a pattern.
- **Built out.** Vacancy is now weighted by how far a lot sits from the town's centre:
  14% on the ragged outskirts, 5% in the middle ring, 1.5% in the core — instead of the
  old blanket emptiness. The outer side lots are filled, and the ragged blob edge trims
  *lots* rather than whole blocks. A town is a built-out place with one hole in it.
- **Verification.** `WorldMap.Verify()` counts street ends that touch nothing. The
  town's highway band is the global paved corridor and carries on past the town, and a
  cul-de-sac bulb is a legitimate dead end, so neither counts. Current result: **0
  dangling street ends** across all 37 towns in the check window.

## What each window contains

**Fields.** Every 1-mile section is split into four quarter fills. A per-section hash
picks the split jitter and four palette colors, so fields are stable while neighbouring
windows meet cleanly.

**Roads.** The paved corridors (10-mile lattice) and the gravel section roads (every
mile) are global. `RoadPaved` on each road rect picks asphalt or gravel fill, so the
paved corridor is visibly wider and darker than the gravel grid.

**Towns.** `SmallTownGenerator` is still the leaf generator. The world stamper keeps the
town's streets, alleys, lots, houses, outbuildings, trees and walks, mirrors the south
parts across the highway band, and never redraws the through-highway (the global
corridor draws it once).

**Creeks and rivers.** Global ribbons, drawn under roads and buildings.

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

**The server renders at screen resolution.** `/world?...&ppf=` says how many screen
pixels a world foot will cover; the server draws the window at exactly that resolution
and the viewer shows it 1:1. Every line weight in the SVG is then a true screen pixel,
which is the only way to keep a hairline hairline at 300×.

**House walls are non-scaling strokes.** `vector-effect='non-scaling-stroke'` with a
0.95-px wall means a wall cannot fatten when the map is zoomed, however far the lens
has closed in. Before this, a half-foot wall drawn in world units became a black bar
at high zoom.

The viewer (`viewer-template.html`) streams live windows from `--serve`:

- `/world?seed=&cx=&cz=&span=&ppf=` renders any window; `lod=0` gives silhouette LOD.
- `/world-center?seed=` returns `{cx,cz,span}` centred on the nearest town's **built
  blocks** — not on the highway that runs past it, which used to land the opening view
  on empty farmland.
- Panning debounces, then requests a window centred on the viewport. The old image
  stays visible until the new one decodes, then the viewport is anchored on the same
  world coordinate, so there is no jump.
- The lens is **zoom-adaptive**: zooming in requests a smaller window (a smooth
  "nice span" ladder from 150 ft up to 30 miles) instead of upscaling a 34-ft-per-pixel
  image. Zoom is expressed in screen pixels per world foot, and the readout is stable
  across window swaps.
- **Max zoom is 300×** against the opening view, which puts the finest window at about
  400 ft — close enough to read a house plan.

## Verification

```
dotnet run -- --verify --seed 1993
```

The ladder in `WorldMap.Verify()` checks:

1. Same window twice is byte-identical.
2. A window 100 miles from the origin is still reproducible.
3. Two overlapping windows agree on every house, outbuilding and tree in the overlap.
4. A 3-mile cell equals the same land inside a 9-mile window.
5. Town ladder: no towns inside 20 miles; the big town stays rare.
6. Towns tend to be near rail lines.
7. No building overlaps another across towns and farms.
8. Rivers exist and every tributary merges southward into a bigger river.
9. Every town sits on a paved road.
10. No town street ends without an exit.

Current seed 1993 result: all ten pass; 274 houses and 1014 outbuildings with zero
overlaps in the 12-mile census, 37 of 37 towns on the paved grid, 0 dangling street
ends. `--map world` writes a 9-mile lens window, a single 3-mile cell, and the
self-contained viewer.

## Open limits / next grilling

- Rail lines cross rivers but no bridge or culvert deck is drawn where a road crosses
  water.
- Town materialization runs `SmallTownGenerator` for every town that touches a window.
  The HTTP server caches rendered SVG windows, but the generator layer itself has no
  cross-window town cache yet.
- There is no real place-name list yet — the 40 generic names repeat across the world.
- The world viewer is a desktop browser tool; there is no Unity/engine port yet. The
  world query API is deliberately plain C# so the port can call the same
  `(seed, center, span, lod)` function.
