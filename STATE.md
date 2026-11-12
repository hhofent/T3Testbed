# STATE — t3 testbed / SpawnGuide (1990s rural Iowa procgen)

**Live state. Rewritten in place at milestones — never appended.** The journal is
`git log`. Research notes live in `docs/research/` (write-once, dated). This file is
the single page a fresh thread reads to resume. Keep it small; if it grows past ~80
lines, push detail down into `docs/` instead.

## Objective

Scratch space for systems headed into a 3D Unity building-construction game. Theme:
**1990s rural Iowa**. The spawner decides where things spawn (walls/houses/neighborhoods/
towns); walls spawn **already fastened** — this is a placement guide, not a construction
sim (no raise mechanic, no anchoring, no nails — user removed the raise flow).

Eventual goal: an infinite, seed-reproducible map around a slowly moving lens, in
3-mile-square cells (see `docs/proposal-infinite-map.md`). **Built 2026-11-11** in
`WorldMap.cs`; Unity is installed but the game project lives elsewhere, so this testbed
stays engine-agnostic C# (.NET 5, C# 9 — no raw strings, no file-scoped namespaces) and
ports later.

## User corrections / constraints (do not regress)

- Walls spawn already fastened. No raise/anchor/nails flow — explicitly removed.
- **Rural lot = the building cluster and its yard only — fields are separate and NOT
  part of the lot.**
- Farmstead barn: rare labeled *existing* pre-1990s gambrel barn is a minority of farms,
  labeled as such.
- Output default: `out/example.html` — single featured example (rural farmstead) is the
  default phone embed. `out/index.html` is the whole spread.
- Builder-Guy wall facts (framed wall 97-1/8 in per story; 2×6; drawn 6.5 in; openings
  are framed header/jack/sill holes) — see `docs/archive/handoff-2026-11-11.md`.

## Locked decisions

- Seeded, deterministic generation (mulberry32 in `Rng.cs`); pure `(worldSeed, coords)`.
- Houses render wall segments with openings; garages attach seeded left/right.
- SVG is verified **geometrically, not visually** — the user views renders (inline
  thread embed works on their phone over T3 Connect). No eyes on renders from inside a session.
- **Never inline large images into thread context** (a prior thread died 3× on
  `400: inlined media exceeds the 56623104 byte total limit`). Snapshot Google Maps
  sparingly; prefer numeric/text observations.
- 2026-11-11: durability scheme replaced append-only `handoff.md` with
  `STATE.md` (rewrite-in-place) + `docs/research/` (write-once) + `git log` (journal).
- Town density ladder (user answer 2026-11-11): hamlet (George-class) typically
  every ~30 mi — very rare under ~10 mi apart, essentially guaranteed by ~50 mi.
  Boyden-class ~1 per 30 mi. Hull-class ~1 per 100 mi (the largest a town ever spawns).
  Farmsteads a few per section; most of the map is fields on the section grid.
- Build order (user answer 2026-11-11): ONE small town first, then grow it bigger
  for testing (hamlet → boyden → hull as parameter bumps). NO spawn-frequency
  field yet — frequency comes after the sizes look right.
- Infinite-world rule (2026-11-11): a 3-mile cell is the lens **request unit**, not an
  independent render tile. Content is pure in world coordinates; windows are rendered as
  one rectangle and clipped. Independent cell images would duplicate seams and split
  boundary buildings.
- **Land order (user, 2026-11-11): hills → rivers → rail → roads → towns/lots.** The
  world is built in that order and each layer constrains the next.
- **World road rules (user, 2026-11-11):** paved corridors every ~10 mi (towns live on
  them and always sit on one), gravel section roads on every mile line, rare exceptions
  in both directions. Rail lines every ~75 mi, E/W and N/S; towns tend to be near rail.
  Rivers run south and tributaries merge into bigger rivers; hills stay low (Great
  Plains, <130 ft relief).
- **Town field is a queryable lattice, not a sequential Poisson pass:** one candidate per
  10-mi slot on the 10-mi paved corridor lattice (both directions), a town only exists
  if rail runs within 9 mi, priority suppression inside 10 mi, and a trunk river within
  1,200 ft vetoes the site. The highway through a town is continuous; county roads are
  clipped inside town footprints.
- **Town names are generic place names (user, 2026-11-11).** Boyden/Hull/George are
  gone from the world: a 40-name pool (`COLD SPRING`, `PINEHURST`, `OAKDALE`, …) and
  generic sizes (`crossroads` / `hamlet` / `village` / `town`). The old preset names
  survive only as CLI aliases.
- **Town streets form a loop, not a cul-de-sac maze (user, 2026-11-11).** A perimeter
  street encircles the town with a jog in its north edge; every street end meets another
  street. Exactly one dead end per town (two for the biggest) hangs off the perimeter
  with four empty lots along it — the only deliberate vacancy in town.
- **Towns vary on which side of the road they sit (user, 2026-11-11):** 46% north,
  24% south, 30% straddling the highway.
- **House walls must not fatten when zoomed (user, 2026-11-11).** SVG
  `vector-effect='non-scaling-stroke'` at 0.95 px; the server renders each world window
  at the viewer's screen resolution so the stroke is a true screen pixel.
- **Max zoom 300×** against the opening view (user, 2026-11-11; was 270×).

## Current status (verified on disk)

- `src/SpawnGuide/` ~2700 LOC, builds clean with .NET 5 SDK (`dotnet run -- --seed 1993`,
  also `--map rural|town|smalltown|country|world`, `--archetype ranch|farmhouse|split-level`,
  `--verify`, `--serve`). No external packages.
- `--map smalltown --size hamlet|boyden|hull` (`SmallTown.cs`, 2026-11-11):
  seed 1993 hamlet = 168 houses (4-sided blocks ~72%, side lots face N–S streets,
  rotated houses), Boyden 530, Hull 1034. Civic anchors on the grid (2 parks,
  school, 2 churches), elevator + bin row, HWY-18 commercial strip, sidewalks on
  both sides of every street, per-row alleys, heavy canopy. `--seeds a,b,c` =
  multiple seeds per run; `out/examples.html` gallery = smalltown×3 + country×3.
- `--map country` (`CountrysideMap.cs`): THE 3-mile cell (15840 ft), town straddling
  the interior section-line junction, 1-mile section grid, field fills, 6 farmstead
  clusters, HWY 18 to the cell edges. One far-LOD (silhouettes, trunk dots) AND one
  detail (walls + captions, trunk dots r≈2 ft) per size class per run.
- **Infinite world** (`WorldMap.cs`, 2026-11-11, reworked the same day): pure
  `(seed, cx, cz, span)` query, built in land order — **hills → rivers → rail → roads →
  towns/lots → farms**. Hills are a low long-wavelength field (<130 ft relief) drawn as
  20-ft contours on a global quarter-mile lattice. Trunk rivers every ~28 mi run south
  with tributaries every ~8 mi merging into them; banks carry trees. Rail every ~75 mi,
  E/W and N/S. Paved corridors every 10 mi (E/W **and** N/S) carry the gravel
  section-road grid on every mile line, with ~1.8% missing / ~2.2% paved exceptions.
  Towns are a queryable 10-mi lattice on the paved corridors, only where rail runs
  within 9 mi, priority-suppressed to ≥10 mi apart, class ladder crossroads/hamlet/
  village/town, generic names, north/south/straddling the road (46/24/30%). Per-section
  farmsteads stamp/rotate the existing `RuralLot` leaf (0–3 per section), fields are
  four quarter fills. `ExactBounds` + bare SVG maps pixels 1:1 to the world rectangle.
  `--verify --seed 1993` passes all **10** checks (byte determinism, 100-mi
  reproducibility, overlapping-window equality, cell-vs-window equality, town spacing,
  rail proximity, zero building overlaps, rivers, every town on a paved road, zero
  dangling street ends). `--map world` writes a 9-mi window, a 3-mi cell and the viewer;
  seed 1993 = 1 town, 65 farms, 219 houses, 683 outbuildings, 3 creek runs.
- `out/viewer.html` (~2 MB for `--map world`; ~10 MB with all maps baked): pan/zoom
  viewer, wheel+drag+pinch, far↔detail LOD crossfade for old fixed maps. In world mode
  it streams live windows: pan debounces and fetches `/world?seed&cx&cz&span&ppf`,
  keeps the old image until the new one decodes, then anchors the same world point under
  the viewport (no pop). The lens is zoom-adaptive — the server renders each window at
  the viewer's screen resolution (pixels-per-foot), the viewer draws it 1:1, and
  zooming in requests a smaller window (nice-span ladder, 150 ft → 30 mi) instead of
  upscaling a 34-ft/px image. Max zoom 300× against the opening view (~400-ft window).
  House walls are `vector-effect='non-scaling-stroke'` at 0.95 px so they stay
  hairlines at any zoom. `/world-center?seed` puts a live seed on its nearest town's
  **built blocks**. Baked maps still work offline; the server is optional for old maps
  and required for live world panning.
- SpawnGuide --serve [--port N] starts the built-in spawner on 127.0.0.1:N (default
  8735): `/` viewer, `/gen?seed=N&size=hamlet|boyden|hull` old fixed maps,
  `/world?seed&cx&cz&span&ppf&lod` world windows, `/world-center?seed`. CORS + HEAD are
  handled so a `file://` viewer can detect the server. World SVG cache = 24 windows.
  Controls: dropdown, 🎲 random button, seed entry box. Bake pool: 13 hamlet seeds.
- Trees render as trunk-location dots only, no canopy disks (user call, 2026-11-11).
- Town silhouettes are seeded superellipse-wobble blobs (edge **lots** dropped, not
  whole blocks), never a rectangle (user call). Vacancy is radial: 14% on the ragged
  outskirts, 5% mid, 1.5% in the core — a town is built out.
- QuietAnnotations is generator-set, not scale-derived; house walls are non-scaling
  strokes at 0.95 px (was a 2.2-px floor that ballooned at high zoom).
- ROADS MUST ATTACH TO ANOTHER ROAD, BOTH ENDS, EVERY TIME (user, hard rule,
  2026-11-11). The network is a full grid: EW streets run the full grid width,
  NS streets run from the highway out to a **perimeter street with a jog**, so the
  town is encircled without the loop ever being an even rectangle. A row or
  column with zero content yields its street (then it vanishes cleanly).
  Cul-de-sacs: exactly ONE per town (two for the biggest) — a short street hanging
  off the perimeter, ending in a 44-ft bulb, with four platted-but-empty lots along
  it. That is the only cul-de-sac legal under the rule, and the only deliberate
  vacancy in town. `WorldMap.Verify()` counts dangling street ends: **0**.
- NO trees alongside roads and none on lots (user call, 2026-11-11). Town is
  tree-free; country has a few field-interior groves and farmstead belts only.
  (River banks are the one exception — the user asked for trees around rivers.)
- Houses: ~45% detached garage / 45% integral / 10% none; ~12% get a small shed;
  ~40% "half lots" (34–47 ft frontage, farmhouse forced — the condensed look).
  Houses clamped clear of the alley row (offenders go vacant); occupancy registry
  (AABB + 3 ft pad) — no building lands on another; side-lot houses also clamped
  clear of the row depth.
- Highway spine (HWY 18) now built out on BOTH sides: north band = elevator +
  1–2 industrial boxes + pads kept clear of both; south band = sparse fringe with
  industrial or store(s) + 1–3 highway-facing houses. Trees are trunk dots only.
- Partial-town cells: `--map country` generates 4 views (hamlet + boyden centered,
  hull partial-west, hamlet partial-north) — town clipped at the cell boundary,
  cut hard at the frame: partial town + partial rural (user's ask).
- Sequencing note: parallel thread (durability agent) committed mid-build twice;
  verify with `git status` before committing; STATE.md is the source of truth.
  Verified geometrically (bbox checks) and by user's eye via example.html.
- Built: house archetypes (`ranch`, `farmhouse` two-story + porch, `split-level` offset-gable L);
  rural farmstead lot (section-line road, 66-ft ROW no centerline, setback house, machine
  shed, steel bins, optional shop / pole barn / rare labeled gambrel barn, LP tank,
  shelterbelt N+W of cluster, 2 gravel lanes, walk); town block (36-ft ROW, yellow
  centerline, 4 lots/side each side of street, 50–66 ft frontage, 20–22 ft setbacks,
  heavy canopy requirement, sidewalks, spur walks, integral/side-entry garages, 35% seeded
  alley w/ rear pole garages); driveways (front straight, side-entry 90° turn).
- Outputs: ASCII console plans, per-artifact SVGs in `out/`, `out/index.html`,
  `out/example.html` (gitignored, regenerate via `dotnet run`).
- **Not committed anywhere off-machine — this repo has no git remote.** Local commits
  only. The old thread is dead; its transcript remains readable via `t3_thread_read`.

## Next steps

1. ~~Study the four calibration links~~ — done 2026-11-11; see
   `docs/research/2026-11-11-ia-calibration-references.md`.
2. ~~Lock the 3-mile-cell architecture and build the infinite world~~ — done
   2026-11-11; `WorldMap.cs` + `--verify` + live `/world` streaming.
3. **Fill out towns** — `docs/todo.md` is the working list, with the research behind it in
   `docs/research/2026-11-11-iowa-town-composition.md`. Done 2026-11-12: the business
   catalogue (no two towns share a shop row), randomised civic buildings and street
   names, the six characteristic town extras (outlying road, bypassed highway,
   industrial side-street, cul-de-sac court, plat-edge street, town square), a final
   "plat and build" pass over every street, sidewalks and drives that actually reach
   the houses, one driveway per farm lot, and dirt field entrances on the section
   lines. **Still to do:** the diagonal rail-corridor street, the curved street, the
   boulevard median, and moving the grain elevator onto a **rail spur** (an elevator
   with no railroad 9 miles away is the current worst realism bug).
4. World follow-ups: bridge/culvert decks where roads cross rivers; cache materialized
   towns across windows; a real place-name list (the 40 generic names repeat).
   Cattle-yard archetype still unconfirmed.
5. Keep `dotnet run -- --verify --seed 1993` green after every generation change.
   Floor plans and the Unity port are still future work; the world query API is the
   porting boundary.

## Recovery protocol (when a thread dies)

1. Open a fresh thread (or fork the dead one — see below). Do **not** reconstruct from memory.
2. Read, in order: `STATE.md` → `README.md` → `git log --oneline -20` → files it names.
3. The dead thread's messages still exist — recover specifics with
   `t3_thread_read(threadId, view='messages', limit≈15)`. Never `includeImage`; ask for
   files on disk instead of inlining media. A fork (`t3_thread_transfer`, action=`fork`)
   copies the old context into a new thread, but it carries inlined media too — use it
   only when media bloat was not the failure cause (it will trip the same 56 MB limit if it was).
4. At milestones, rewrite the sections of this file; commit to git. The journal is
   `git log --stat`; don't re-append a handoff narrative.

## File map

- `STATE.md` — this file.
- `docs/todo.md` — working to-do list (town filling-out, with research links).
- `docs/work-in-progress.md` — the staged plan for the town work, with the decisions
  made and what is left; read this first if the town work needs resuming.
- `docs/research/` — dated, write-once observations (calibration links, town anatomy,
  Iowa town composition + county entrance rules).
- `docs/proposal-infinite-map.md` — built world architecture + grilling findings.
- `docs/archive/handoff-2026-11-11.md` — frozen original handoff (verbatim).
- `out/` — generated embeds (gitignored).
