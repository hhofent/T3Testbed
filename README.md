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

**Outputs** — ASCII houses to the console, one SVG per artifact, a self-contained
`out/index.html` (everything) and `out/example.html` (single featured example —
the rural farmstead). Default phone view is the single example.

### Build & run

```
cd src/SpawnGuide
dotnet run -- --seed 1993                      # houses + rural lot + town block
dotnet run -- --map rural                      # just the farmstead
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
4. Multiple adjacent farm blocks / neighborhood assembly, roads between, creek
   corridors (West Branch Floyd River) and field boundary lines.
5. Unity port: same plan dataclasses → spawns already-fastened wall prefabs.
   Keep this console app as the rule reference.
