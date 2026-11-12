# Work in progress — filling out towns and country

**Started 2026-11-12.** This is the resumption doc for the four changes the user asked
for on 2026-11-11/12. It exists so another agent can pick this up cold if the session
stalls: read this, then `STATE.md`, then the files named here.

**Research:** `docs/research/2026-11-11-iowa-town-composition.md`.
**Full to-do list:** `docs/todo.md`.

## The four asks (user, 2026-11-12)

1. Make the business names generic — "Casey's" becomes a gas station.
2. Some towns should have the characteristic extras (diagonal rail-corridor street,
   curved street, bypassed old highway, outlying rural-residential road, town square),
   **plus more kinds**.
3. Rural walking paths from the road to the front door, following the driveway and
   curving off; **one** driveway per lot even when the lot has barns; the driveway also
   reaches the machine shed; fields get their own driveways.
4. The commercial strip, the civic buildings and the town layout must **not** be in the
   same order in every town — randomised, but still keeping the general similarities a
   real town has.

## Decisions made (grilled with the user 2026-11-12)

| Question | Answer |
|---|---|
| Business label style | **Descriptors with invented flavour** — "GAS STATION", plus short pseudo-names like "CEDAR VALLEY FEED", "WAKEFIELD HARDWARE". Not real brands, not place references to the reference maps. |
| Which town extras | **All of them, plus more kinds.** 0–2 extras per town, drawn by seed. |
| Rural drive shape | **One lane**, road → angles in → past the house → machine shed + bins, with a branch to the garage if it is separate. The concrete walk leaves the road, **follows the drive, then curves off** to the front door. |
| Field entrances | **1–2 per quarter section**, hashed per quarter so they are stable across overlapping windows. |

Extra kinds added on top of the researched five:
- short industrial side-street off the highway
- boulevard street with a planted median
- cul-de-sac court
- a street that dead-ends at the plat edge

## Hard invariants — do not break these

1. `dotnet run -- --verify --seed 1993` must stay green (10 checks).
2. **Every town street end must touch another street** — check 10 counts dangling ends
   and currently reports 0. New streets (curved, diagonal, cul-de-sac) must be attached
   at both ends or be a cul-de-sac with a bulb.
3. **The world stays a pure function of world coordinates.** No sequential passes that
   depend on visit order; any new per-quarter or per-town randomness must be hashed from
   world coordinates, not drawn from a shared RNG stream.
4. **No building overlaps another** — check 7.
5. `.NET 5 / C# 9`, no external packages, no raw strings, no file-scoped namespaces.

## Files that will change

- `src/SpawnGuide/SmallTown.cs` — business catalogue, strip order, civic anchors,
  town extras, final lot pass, sidewalk/drive alignment.
- `src/SpawnGuide/MapGenerator.cs` — `RuralLot`: one lane, walk follows the drive.
- `src/SpawnGuide/WorldMap.cs` — field entrances, town context (rail/highway) passed
  into the town generator.
- `src/SpawnGuide/MapPlan.cs` — new plan fields if a street kind needs its own draw
  (e.g. a planted median, a dirt field entrance).
- `src/SpawnGuide/SvgMapRenderer.cs` — draw the new surface types.
- **NEW** `src/SpawnGuide/BusinessCatalogue.cs` — the business types and the invented
  name flavour pool.

## Staged plan

Each stage is independently committable and keeps `--verify` green.

- [x] **Stage 0 — scaffolding.** `BusinessCatalogue.cs` with the catalogue data; a
      `TownContext` record carrying what the world knows about a town's surroundings
      (rail line position/direction, highway direction) so extras can be placed against
      them; `SmallTownGenerator.Generate` gains an optional context parameter.
      *Partly done:* the catalogue exists. The `TownContext` is deferred to Stage 3,
      where the rail-dependent extras need it.
- [x] **Stage 1 — ask 1 + ask 4 (businesses and randomisation).** Replace the fixed
      `padLabels` list with a catalogue sampled by town class, then shuffled. Randomise
      the civic anchor order and which anchors appear. Randomise the industrial box
      names and count. De-brand the town highway label ("HIGHWAY 18" is already gone).
      *Done and verified:* every seed now gets its own strip, its own civic mix and its
      own street names. Sidewalk labels are no longer suppressed in quiet mode — only
      when the caller asks (`SuppressOutbuildingLabels`).
- [x] **Stage 2 — ask 3 (rural).** One lane per lot; walk from the road following the
      drive and curving to the door; 1–2 field entrances per quarter section.
      *Done:* `RuralLot` now has a single lane that leaves the road at ~90°, runs up the
      side of the house, carries on past it to the machine shed and bin row, with the
      garage as a branch off it in the yard. The concrete walk leaves the road beside
      the lane, follows it up the yard and curves off to the front door. Field
      entrances are added by `WorldMap.AddFieldEntrances` — 1–2 per quarter section,
      hashed off world coordinates, 26 ft wide, 95–170 ft long, set back 110 ft from
      an intersection, skipped where the road line is absent or where a farmyard or
      town is in the way. Drawn on a new "dirt" surface colour.
- [x] **Stage 3 — ask 2 (town extras).** The extra street kinds, 0–2 per town.
      *Done:* six extra kinds, drawn from a shuffled pool with a 0–2 count per town
      (35% of towns get none). Implemented:
      - **OutlyingRoad** — a road beyond the town edge with 3–6 farm houses on it,
        reached by the two boundary streets (so it is a through road).
      - **BypassedHighway** — the same street, but with a few shops left on it.
      - **IndustrialSideStreet** — a short street running from the highway up into the
        grid; the shops keep clear of it.
      - **Court** — a cul-de-sac off the perimeter with houses built along it (the
        opposite of the town's one deliberate vacancy).
      - **PlatEdge** — a street that runs out of town and stops at the edge of the
        plat. Its far end is free by design, so `MapPlan.RoadMayEndFree` was added and
        the verification check now honours it.
      - **TownSquare** — the park goes on the most central face, so the middle
        streets frame the green.

      **Not done:** the *diagonal rail-corridor street* and the *boulevard with a
      planted median*. Both need the town's surroundings (the rail line's position and
      bearing) to be threaded into `SmallTownGenerator`, which does not know about them
      today. See "Stage 7" below.
- [ ] **Stage 4 — todo item 3a (fill every street with lots).** Final pass platting
      lots along every street segment, leaving a few deliberately empty.
- [ ] **Stage 5 — todo item 1 (surface alignment).** Front-door walks and garage drives
      start on the sidewalk line; drives cross the walk to the kerb.
- [ ] **Stage 6 — docs.** Update `docs/todo.md` (tick what is done), `STATE.md`,
      `README.md`, and this file.
- [ ] **Stage 7 — the rail-corridor street (still to do).** The research is clear that
      Iowa's industrial follows the railroad: a grain elevator is a co-op on a rail
      siding, and every reference town has the elevator at the rail/main junction. The
      world generator knows where the rail is; `SmallTownGenerator` does not. The
      fix is a `TownContext` parameter carrying the rail's position and bearing into
      the town generator, so it can:
      - put the grain elevator and its bin row at the railroad instead of on the
        highway band (today it is on the highway, and the rail can be 9 miles away);
      - add the **diagonal rail-corridor street** (George's Boiler Ave) along the old
        rail line, with small manufacturing on it;
      - add a **rail spur** into the elevator yard.
      This is the biggest remaining realism gap in the generator.

## Notes / gotchas already discovered

- The town's commercial band is at local z 0…`BandDepth` (66…241) with the highway
  band at z 0…66. The grid starts at `zFirstStreet = zBand + BandDepth`.
- `SmallTown.Generate` places lots (step 2) **before** the street network (step 4), so
  the perimeter loop and the outer faces of the boundary N–S streets have no frontage
  at all. Stage 4 fixes that.
- Front-door walks on the E–W faces currently start `frontLineZ ± 4.5` while the
  sidewalk is drawn at `ewZ ± 2.25` inside the street edge — measured median gap
  **6.75 ft**. Side-lot walks (`PlaceSideLot`) start exactly on the sidewalk.
- The grain elevator is placed on the highway band, but the rail line can be up to
  9 miles from the town. Stage 3 should use the rail context to put the elevator and a
  rail spur at the railroad, and only fall back to the highway band when no rail touches
  the town.
- **The town's ragged silhouette (the blob) was cutting the commercial strip.** The blob
  is an ellipse over the whole town, and the highway band sits on its south edge, so
  the strip could only exist in the middle. The band is now exempt from the blob test:
  a real highway strip runs the full length of the town.
- `OrderBy(_ => rng.NextUInt())` is used to shuffle a list with the town's RNG. It is
  *not* a stable sort by key, so the order depends on the RNG stream — fine inside a
  town (the town seed fixes the stream) but it must never be used where the order has
  to be reproducible across windows. The world layer uses hashed sorts for that.
- **BUILD GOTCHA: `dotnet build` fails silently when `SpawnGuide.exe` is locked by a
  running `--serve` process.** The build errors are all `MSB3026/MSB3021` about
  copying the apphost, and `dotnet run --no-build` then runs the *old* binary, so a
  change appears not to work. Always `Get-Process SpawnGuide | Stop-Process -Force`
  before building, and check for "Build succeeded" rather than assuming.
