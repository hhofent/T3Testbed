# t3 testbed

Scratch space for testing systems before they go into the building-construction game
(3D, Unity, 1990s rural Iowa). Mostly procedural generation. Throwaway by default —
graduate code into the real project only after the rules settle.

## Current experiment: `src/SpawnGuide`

Seeded, engine-agnostic C# generator that decides **where things spawn** for 1990s
rural Iowa — houses, 1990s outbuildings, driveways, walks, lots, and town blocks.
Walls spawn already fastened — this is a placement guide, not a construction
simulation. No raise mechanic, no anchoring step, no nails.

### What it does today

**Houses** — three archetypes: `ranch`, `farmhouse` (two-story + porch), `split-level`
(offset-gable L, side-entry garage). Wall segments with Builder-Guy dimensions and
openings (front door, windows, garage door). Deterministic from `--seed`.

**Rural farmstead lot** (`--map rural`) — section-line county road (66-ft ROW, no
centerline), house set back from the road, machine shed (36–48×30–40) behind the house,
steel bins in a row behind the shed, optional shop, optional pole barn, rare labeled
*existing* gambrel barn, LP tank, shelterbelt ringing the building cluster N and W,
gravel lanes from the county road to the house and shed, walk to the front door.
Typical Iowa farmstead from Lyon County, IA references (House–Barnett/George/Craig
aerial photos; section-line grid, 1-mile spacing).

**Town block** (`--map town`) — one street, both sides (36-ft ROW, yellow centerline),
4 lots per side, seeded archetypes, 20–22 ft setbacks, yard trees, street trees,
public sidewalks, spur walks to each door, integral or side-entry garages with short
driveways, occasional rear pole garage onto a seeded alley, or detached garage for
farmhouses. Matched to the gridded unincorporated towns in the reference images
(George, Craig, Struble — orthogonal streets, some lots alley-loaded, heavy canopy).

**Small town** (`--map smalltown --size hamlet|boyden|hull`) — a whole seeded grid
town, George/Craig/Struble class: highway along the south edge (commercial strip — Casey's,
feed store, co-op, elevator with bin row east of MAIN), cardinal grid inside,
blocks with houses on **4 sides** (~72% of blocks; side lots face the N–S streets,
houses rotated 90°) or 2 sides, 20–30 ft setbacks, sidewalks on both sides of every
street, street trees, 2 parks + 1 school + 2 churches on the grid, per-row alleys
(~55%), heavy canopy. Sizes: hamlet ~0.4 mi, boyden ~0.7 mi, hull ~1.1 mi. The street
network is closed by a **perimeter loop** that jogs on its north edge, so the town is
encircled without the ring ever being an even rectangle, and exactly **one dead-end
street** hangs off it with empty lots along it — the only deliberate vacancy in town.

**Roads & lots discipline** — no building lands on another (AABB registry with pad),
houses never reach into the alley row (clamped to the lot, else the lot goes vacant),
n-street houses face both street orientations, commercial strip sits on **both** sides
of HWY 18 (pads + 1–2 industrial boxes north, houses + a store/plant south), small
sheds ~12% of houses. Garages: ~45% run detached, ~45% integral, ~10% none.

**3-mile cell** (`--map country`, seed via `--seed`) — a 3-mile (15840 ft) test cell
with towns straddling the interior section-line junction or a cell boundary, 1-mile
grid roads, field fills, farmstead clusters, HWY 18 to the cell edges. Four views per
run: hamlet (centered), boyden (centered), hull (partial at the west edge), hamlet
(partial at the north edge) — the "partial town, partial rural" framing.

**Infinite world** (`--map world`) — the destination architecture. A window is a pure
query `(seed, cx, cz, span)`; the 3-mile cell is the lens request unit, not an
independent tile, so seams never duplicate roads or split farmsteads. The land is made
in order: **hills → rivers → rail → roads → towns and lots → farms**. Hills are low
Great-Plains swells (under 130 ft of relief) drawn as 20-ft contours; trunk rivers run
north-to-south every ~28 miles with tributaries merging into them; rail lines cross
every ~75 miles; paved corridors every ~10 miles carry a gravel section-road grid on
every mile line. Towns are a queryable 10-mile lattice **on the paved corridors**,
only where a rail line runs within 9 miles, forced ≥10 miles apart, with generic place
names and four sizes (crossroads / hamlet / village / town). Each town sits north of,
south of, or astride its highway, is encircled by a perimeter street that jogs, and has
exactly one dead-end street with empty lots on it. Farmsteads stamp the rural-lot leaf
from per-section RNG streams (0–3 per section). `--verify` runs the geometric ladder:
byte determinism, 100-mile reproducibility, overlapping-window equality, cell-vs-window
equality, town spacing, rail proximity, zero building overlaps, rivers, every town on a
paved road, and zero dangling street ends.

**Zoomable viewer** — `out/viewer.html` (self-contained; no server required for the
baked maps): pan/zoom, far ↔ detail crossfade, wheel/drag/pinch. In world mode with
`--serve` running it streams live windows as you pan: the old image stays until the new
one decodes, then the same world point is re-anchored under the viewport, so there is
no pop. The server draws each window at the viewer's exact screen resolution and the
viewer shows it 1:1, so every line weight is a true screen pixel — zooming in requests
a smaller window (down to ~400 ft at the 300× cap) instead of upscaling a 9-mile
image, and house walls stay hairlines instead of ballooning into black bars. Trees are
trunk dots; town silhouettes are seeded blobs, not rectangles. **Open in a desktop
browser** — phones can't reach a local file.

**Live seeds from the spawner** — `dotnet run -- --serve` starts the built-in HTTP
server on 127.0.0.1:8735. With it running, the 🎲 random button and seed box generate
ANY seed on demand — no baking needed. `GET /` serves the viewer,
`GET /gen?seed=N&size=hamlet|boyden|hull` generates a fixed map fresh,
`GET /world?seed&cx&cz&span&ppf&lod` renders any infinite-world window, and
`GET /world-center?seed` returns the nearest town's center so a new seed opens on a
town. Baked maps still work offline.

**Outputs** — ASCII houses to the console, one SVG per artifact, a self-contained
`out/index.html` (everything) and `out/example.html` (single featured example —
smalltown when present, else the rural farmstead). Default phone view is the single example.

### If a thread dies (durability)

Threads fail; this repo is the record. Live status is in `STATE.md` (rewritten at
milestones, never appended), observations live in `docs/research/`, decisions and the
journal live in `git log`. Recovery protocol: read `STATE.md` → `git log --oneline -20`
→ re-read only what's needed; the dead thread's messages remain readable via
`t3_thread_read`. Do not inline large media into thread context — that's what killed
the previous thread.

## Build & run

```
cd src/SpawnGuide
dotnet run -- --seed 1993                      # houses + rural lot + town block
dotnet run -- --map rural                      # just the farmstead
dotnet run -- --map smalltown --size hamlet    # the whole hamlet grid
dotnet run -- --map country                    # 3-mi cell — 4 variants (centered/partial)
dotnet run -- --map world --seed 1993          # infinite world: 9-mi window + 3-mi cell + viewer
dotnet run -- --verify --seed 1993             # world determinism / seam / town / collision checks
dotnet run -- --serve                          # live seeds + streaming world windows
dotnet run -- --seeds 1993,42,2024             # multi-seed gallery → out/examples.html
dotnet run -- --archetype ranch --seed 42      # one house archetype
```

`out/index.html` is the whole spread; `out/example.html` is the default embed.
Requires the .NET SDK (built with 5.0; no external packages).

### Wall facts carried from Builder-Guy

(`Builder-Guy/UnityProject1/UnityProject1`, see `Stock.cs`, `CONTEXT.md`,
`Backlog_Update_1-wall test bed/handoff.md`)

- Framed wall height per story: **97-1/8 in** (bottom 2×6 plate + 92-5/8 in precut
  stud + double top plate) = `BuilderGuyDims.WallHeightFeet`.
- Stud/plate stock: `PrecutStudTwoBySix` / `TenFootPlateTwoBySix`, 2×6 construction.
- Drawn thickness is framed+finish (6.5 in); Builder-Guy frames bare 5.5 in 2×6.
- Openings are framed (header, jacks, sill) — the guide shows where they sit;
  the spawned wall is built complete.

### Known simplifications (deliberate)

- Exterior walls only; no interior partitions yet.
- Roof/siding/trim are metadata text, not geometry.
- Windows/doors placed by simple rules with seeded jitter, not room programs.
- ASCII console view is 1 cell per foot, front at top.

### Next steps (roughly in order)

1. Interior partitions + room programs per archetype.
2. Neighborliness checks: driveway/walk collisions, tree-lot overlaps.
3. Coop corner archetype (grain elevator + bin cluster at a section-line corner —
   the Lyon County signature) and trailer-court archetype (curved street, 40×70
   pads at town fringe) — both visible in the reference images, not built yet.
4. Infinite world follow-ups: bridge decks where roads cross rivers, cross-window town
   cache, a real place-name list, optional diagonal rail-corridor street. The land
   pipeline (hills → rivers → rail → roads → towns → farms) is built in `WorldMap.cs`.
5. Unity port: same plan dataclasses → spawns already-fastened wall prefabs.
   Keep this console app as the rule reference.
