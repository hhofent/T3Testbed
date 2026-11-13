# To-do — filling out towns and country

A working list. Ordered roughly by "what a viewer notices first". Each item names the
research that justifies it and the files that would change.

**Research:** `docs/research/2026-11-11-iowa-town-composition.md` (what an Iowa small town
actually contains, and the county rules for drives, walks and entrances) and
`docs/research/2026-11-11-ia-calibration-references.md` (town shapes, lot frontages,
canopy, farmstead layout).

**Ground rule:** the world stays a pure function of world coordinates. Everything below
must be derivable from `(seed, world coordinate)` — no sequential passes that depend on
visit order.

---

## 1. Sidewalks leading to the houses  *(user, 2026-11-11)* — **DONE 2026-11-12**

> "have sidewalks leading to the houses, have driveways leading to the garages.
> have dirt paths for rural lots on gravel roads."

**Where it stands.** `SmallTown.cs` draws 4.5-ft concrete sidewalks along both sides of
every grid street, and every lot gets a 4-ft walk to its front door and a 17-ft concrete
drive to its garage. The pieces all exist — **but they didn't quite meet.**

Measured on a rendered town (`out/town4k.svg`, 0.318 px/ft): the median gap between a
front-door walk's street end and the nearest sidewalk line was **6.75 ft**. Cause: the
sidewalk is drawn 2.25 ft *inside* the street's edge line, while the walk started 4.5 ft
*past* the lot's front line. Side-lot walks, which face a N–S street, were already
correct.

**Fixed.** The walk and the drive now start on the sidewalk line (2.25 ft inside the
street's edge) and the drive crosses the walk to the kerb, the way a real Iowa driveway
does. Side-lot walks were already right and were left alone.

## 2. Dirt paths for rural lots on gravel roads  *(user, 2026-11-11)* — **DONE 2026-11-12**

**Where it stood.** `MapGenerator.RuralLot` gave the farmstead a 12-ft gravel drive to
the garage *and* a separate 10-ft lane to the machine shed — two entrances — and a
concrete walk that ran in a straight line from the road to the front door, ignoring the
drive.

**Fixed.**
- **One driveway per lot.** It leaves the road at ~90°, runs up the side of the house,
  carries on past it to the machine shed and the bin row, and the garage is a short
  branch off it in the yard — "a farm property may be served by a single driveway for
  the residence, plus a second driveway to allow access to farm buildings or grain
  bins" (Woodbury County).
- **The walk follows the drive** and curves off to the front door.
- **Field entrances added**: `WorldMap.AddFieldEntrances` puts one or two dirt
  entrances per quarter section, hashed off the world coordinates so they never move
  as the lens moves, 26 ft wide, 95–170 ft long, set back 110 ft from an intersection,
  skipped where the section line has no road on it or where a farmyard or a town is in
  the way. They draw on a new "dirt" surface colour.

**Files:** `MapGenerator.cs` (`RuralLot`), `WorldMap.cs` (`AddFieldEntrances`).

## 3. Towns still have extra roads — fill them with lots  *(user, 2026-11-11)* — **DONE 2026-11-12**

> "towns still have extra roads. maybe add a final step to fill with lots all roads in
> towns, save the few empty ones"

**What the research adds.** Iowa town plats are layered, and the extra streets are real
and characteristic — they are not a defect to be erased:

- **A diagonal rail-corridor street** — George: Boiler Ave on the old rail line; Boyden:
  Railroad St curving SE through the south third.
- **A curved street** — Hull has one curved residential street on the east side.
- **A bypassed old highway** — after a US 30 bypass, the old route stayed as a quieter
  parallel street (Kirkwood Blvd = old US 30, Lisbon; Rolesville's Main St after the
  US 401 bypass).
- **Sparse rural-residential along an outlying road** — Hull: "North side has sparse
  rural-residential along 310th St outside the grid."

**(a) The final "plat and build" pass — DONE.** `SmallTown.PlatStreetFrontage` walks
every street in the town and, for each side that is completely empty, plats a row of
lots and builds most of them — 22% are left empty on purpose, because a town with no
vacant lot reads as a pattern rather than a place. Each lot is checked on its own, so
one occupied spot (a cul-de-sac stub) does not stop the row. The pass is bounded by the
town's own ground so it does not spill into the fields.

**(b) The characteristic extra streets — MOSTLY DONE.** Each town now draws 0–2 extras
from a shuffled pool (35% of towns get none):

| Extra | What it is |
|---|---|
| OutlyingRoad | a road beyond the town edge with 3–6 farm houses on it, reached by the two boundary streets |
| BypassedHighway | the same street, with a few shops left on it |
| IndustrialSideStreet | a short street running from the highway up into the grid; the shops keep clear of it |
| Court | a cul-de-sac off the perimeter with houses built along it |
| PlatEdge | a street that runs out of town and stops at the edge of the plat |
| TownSquare | the park goes on the most central face, so the middle streets frame the green |

**Done 2026-11-13:** the **diagonal rail-corridor street** (`OLD RAIL`) and the
**curved street** (`CRESCENT`). Laid after the grid and the plat pass; whatever grid
content ends up under the travelled way comes down, the street goes in as a polyline
(`RoadPath`), and houses front it on both sides, turned to face it (`HousePlan.Rotate`).
The crescent arcs over the NW corner from the top E–W street onto the perimeter street;
the diagonal runs SW→NE across the whole town (seed 1993: ~30% of towns draw each).
They are decided at the end of the generator from the town's stream, so a town without
either is byte-identical to before they existed. The world stamps `RoadPaths` into
windows and counts their ends in the dangling-ends check.

**Files:** `TownStreets.cs` (geometry), `SmallTown.cs` (the free-street pass),
`WorldMap.cs` (`StampTown`, `CountDanglingRoads`).
(`RoadClip` — the loop bounds must grow to enclose the new streets, or county roads
will run through them).

## 4. More commercial — **DONE 2026-11-12**

**Was:** the commercial band was a fixed list of six labels reused in the same order in
every town in the world: `CASEY'S`, `FEED & SEED`, `CO-OP`, `HARDWARE`, `BAR & GRILL`,
`AUTO ELECTRIC` — one real brand and four pseudo-brand industrial names.

**Now:** `BusinessCatalogue.cs` holds 16 shop types and 5 industrial types, each with a
descriptor, the smallest town tier that gets it, the chance it carries an invented
flavour name, whether it wants a site at the end of the strip, and its footprint. The
mix is gated by a town tier (0 crossroads … 3 town) and the order is shuffled, so no
two towns have the same row of shops. The roomy businesses (gas station, implement
dealer, lumber yard, motel) are pushed back to the ends, because that is how a real
main street reads.

## 5. Industrial — and put it on the railroad, not the highway — **DONE 2026-11-12**

**Was:** `SmallTown.cs` placed the grain elevator + bin row and an industrial building in
the **highway** commercial band, and in the world map the rail line could be **up to 9
miles away** — so a town could have an elevator with no railroad beside it.

**Now:**
- The rail sits on the section grid, 40–210 ft to one side of its own road, and swells
  slowly to that same side. It therefore never crosses the road it parallels, and every
  crossing it makes is with a perpendicular road, within a degree or so of square.
- A town within reach of a rail gets a **branch spur** — the rail runs every 75 miles
  and the paved corridors every 10, so the nearest corridor is 5 miles away, and that is
  the branch line. The spur leaves the rail at a right angle, so it crosses the roads it
  meets square too.
- The **grain elevator moves to the end of the spur**, which is where a real Iowa
  elevator stands. Seventeen of the thirty-seven towns in the check window have one.
- The industrial boxes come from the catalogue.

**Done 2026-11-13:** a **rail-side industrial strip** — small works (cabinet shop,
metal fabrication, feed mill) along the spur inside the town, long side along the
track, between the cross streets, tier-gated by the town's industrial catalogue. The
grain elevator stands at the spur's end, the depot beside the track with the far end
of the line to itself, and the town streets yield to the railroad's ground. The rail
lattice is phased three sections off the paved corridors, and the world draws the
spur where the town put it. Creamery remains a ghost-town element — rare by the 1990s.

## 6. Civic and institutional landmarks — **DONE 2026-11-12**

The civic buildings vary per town and per tier: a one-room schoolhouse is a coin flip in
a crossroads and near-certain in a village, a town may have two churches and no park,
and the order they go in is the town's. A park on the most central face becomes the
**town square**.

Added (each with its own town tier and likelihood, in
`BusinessCatalogue.Landmarks`): **water tower** (near-universal), **post office** (its
own building, not a shop label), **fire station**, **public library**, **ball field**,
**cemetery**, **railroad depot** (only where the railroad reaches the town),
**golf course**, **fairgrounds**, **airstrip**. The ones that are areas stand in their
own ground at the edge of town; the rest take a lot face.

## 7. Street variety — **MOSTLY DONE**

Done: the outlying road, the bypassed old highway, the industrial side-street, the
cul-de-sac court, the plat-edge street, the town square, a **boulevard street with
a planted median** (about one town in six), and — 2026-11-13 — the **diagonal
rail-corridor street** (`OLD RAIL`, whole town diagonal) and the **curved street**
(`CRESCENT`, arc over the NW corner), with houses rotated to face them.

## 8. Sidewalk realism

**To do.** Consider dropping the walk on one side of quieter residential streets on some
towns (real Iowa sidewalk networks are patchy), while keeping the commercial strip and
the school/park route fully served.

## 9. Rail and road crossings — **DONE 2026-11-12** (user, 2026-11-12)

> "rails and roads colliding must be redesigned. they must only cross perpendicularly.
> they may, on occasion, cross diagonally, but it must be mostly perpendicular, never
> almost parallel."

The rail used to run on a jittered 75-mile lattice with a wave on top, so an east-west
rail could drift across the east-west section road it was running beside and peel off it
at whatever angle it happened to be at. It now sits on the section grid, offset to one
side of its own road, with a one-sided swell — so it can never come back across the
line it parallels, and every crossing is square. Two checks keep it honest:
"rail crosses roads square, never almost parallel" and "rail towns get a branch spur
and an elevator on it".

## 10. Town edges — **DONE 2026-11-12** (user, 2026-11-12)

> "they must not have streets on the outside with no lots on the outside of the streets."

The plat pass now reaches past the town's outermost north-south street and plats a row
of lots there, so the west and east edges are built out like the rest of the town. The
town grows symmetrically to hold them, and the county-road clip follows the new
footprint.

---

## Not for now

- **Cattle yards** (the feedlot archetype from the Lyon County research) — the user has
  never confirmed it. It is a *parcel* archetype, not a town archetype; keep it on the
  farmstead backlog.
- **Bridges/culverts** where roads cross rivers — separate to-do, belongs to the world
  road layer, not the town layer.
