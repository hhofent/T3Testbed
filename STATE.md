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
3-mile-square cells (see `docs/proposal-infinite-map.md`). Unity is installed but the
game project lives elsewhere; this testbed stays engine-agnostic C# (.NET 5, C# 9 —
no raw strings, no file-scoped namespaces) and ports later.

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

## Current status (verified on disk)

- `src/SpawnGuide/` ~2150 LOC, builds clean with .NET 5 SDK (`dotnet run -- --seed 1993`,
  also `--map rural|town|smalltown`, `--archetype ranch|farmhouse|split-level`). No external packages.
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
- `out/viewer.html` (~24 MB, self-contained data-URI maps, no server — open in a
  desktop browser): pan/zoom viewer, wheel+drag+pinch, far↔detail LOD crossfade at
  1.55× (smalltown) / 2.6× (cell) fit zoom. Controls: dropdown, 🎲 random button,
  seed entry box (jump to any baked seed; unknown seeds flash "seed N not baked").
  Bake pool: 13 hamlet seeds (1993, 42, 2024, 7, 97, 123, 555, 777, 1001, 1337,
  1999, 2718, 4096) via `--bake`; arbitrary seeds need `dotnet run -- --bake` with the
  seed added to `bakedPool` in Program.cs. Floor plans for houses are NOT built yet —
  the detail zoom is the target layer for them when they come.
- Trees render as trunk-location dots only, no canopy disks (user call, 2026-11-11).
- Town silhouettes are seeded superellipse-wobble blobs (edge lots dropped), never a
  rectangle (user call). Hamlet seed 1993 now 130 houses after blob cut (was 168).
- QuietAnnotations is generator-set, not scale-derived; wall min-stroke is 2.2px at
  natural size (was 5px).
- ROADS MUST ATTACH TO ANOTHER ROAD, BOTH ENDS, EVERY TIME (user, hard rule,
  2026-11-11). The network is a full grid: EW streets run the full grid width,
  NS streets run from the highway out to the last surviving EW street. A row or
  column with zero content yields its street (then it vanishes cleanly).
  Cul-de-sacs: seeded ONE per town — a short N–S stub (<half a block, ~110–130 ft)
  into an empty north-edge column, ending in a 46-ft bulb, flanked by 2 lots.
  That is the only cul-de-sac legal under the rule.
- NO trees alongside roads and none on lots (user call, 2026-11-11). Town is
  tree-free; country has a few field-interior groves and farmstead belts only.
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
2. Iterate the hamlet render (eyeball `example.html`): label noise, anchor
   placement, canopy density; optional: lots on N–S faces, curved rail-corridor
   avenue (George's Boiler Ave / Boyden's Railroad St). Then Boyden preset, then
   Hull — testing small → big. No frequency field yet.
3. Confirm/lock `docs/proposal-infinite-map.md` (ladder numbers updated 2026-11-11);
   then the 3-mile-cell architecture. Cattle-yard archetype still unconfirmed.
4. Note: a parallel agent's restructure (2026-11-11, commits be652e2 + 15693f3)
   swept in-flight SmallTown work into git mid-edit; the civic-anchor key bug it
   captured was fixed afterward and verified on disk.

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
- `docs/research/` — dated, write-once observations (calibration links, town anatomy).
- `docs/proposal-infinite-map.md` — 3-mile-cell architecture, unbuilt.
- `docs/archive/handoff-2026-11-11.md` — frozen original handoff (verbatim).
- `out/` — generated embeds (gitignored).
