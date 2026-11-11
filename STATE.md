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
- `--map smalltown --size hamlet` (`SmallTown.cs`, 2026-11-11, seed 1993 verified:
  114 houses from 119 lots (5 civic anchors: 2 parks, 1 school, 2 churches),
  elevator + bin row, highway-18 commercial strip w/ MAIN junction, IOWA..DAKOTA ×
  DIVISION..WILLIAMS grid, sidewalks + street trees + per-row alleys, 503 trees).
  Verified geometrically, not visually — user's eyes are the check, via example.html.
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
