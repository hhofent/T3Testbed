# Proposal — infinite, seed-reproducible 3-mile-cell map

**Status: proposal, not built.** Recovered from the archived handoff (2026-11-11).
Confirm with the user before implementing.

## The goal (user, verbatim intent)

> "the goal is a 3 mile square procedurally generated map that is reproducible with a
> seed and continues on for hundreds of miles when the 'lens' is slowly moved"

Destination: an **infinite, seed-reproducible map** streaming around a slowly moving
lens, built from **3-mile-square cells**. Town density ladder from the user's
calibration: Hull-class every ~100 miles, Boyden-class every ~30 miles, George-class
hamlets more frequent still (Struble/Craig/George spacing suggested every ~8–12 mi —
not yet confirmed with the user), farmsteads scattered, most of the map empty fields
on the section grid. Towns sit on the 1-mile section grid; George had one diagonal
"old rail" avenue — that pattern (grid town + a diagonal rail-corridor street) is
worth replicating as an optional town feature.

## Architecture sketch

- World seed → per-cell RNG via hash(worldSeed, cellX, cellZ). Cell = 3 mi square.
- Cell content classes: (a) farmland+section grid (default), (b) hamlet, (c) Boyden-class,
  (d) Hull-class — placed by a low-frequency "town field" (e.g. jittered Poisson /
  blue-noise with class radii ~100 mi / ~30 mi / ~8–12 mi), independent of cell edges so
  towns can straddle cells.
- Each cell independently generates: section-line roads (global 1-mi lattice), fields,
  creek corridor (low-freq noise ribbon), farmstead placements (hashed per section),
  and whichever town overlaps it (town generated once per town-center, stamped into
  every cell it overlaps — deterministic from the town's own seed).
- Lens streams cells in a radius; generation is pure(worldSeed, cellCoord) → same result
  regardless of visit order or direction.
- Existing pieces (houses, farmstead lot, town block archetypes, SvgMapRenderer) become
  the LEAF renderers the town/farmstead stampers call. Field fill stays cheap geometry.
- Verification ladder: (1) cell-hash determinism tests, (2) single 3-mi cell render,
  (3) 9-cell (9×9 mi) super-map render, (4) Unity port.
