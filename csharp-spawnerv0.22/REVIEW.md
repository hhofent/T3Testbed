# Review of the 0.2 world spawner

Reviewed `csharp-spawnerv0.2` on 18 Nov 2026. The window title is "World Spawner Viewer 0.2". No generator, viewer, test, or settings file was edited. `bin` and `obj` are still there. No pull request.

`dotnet` on `PATH` is SDK 5.0.408. The first `dotnet run` failed in about a second with `NETSDK1045` because that SDK cannot build the net8 library or the net10 window. Every successful run below used `C:\Users\hhofe\.dotnet\dotnet.exe`, SDK 10.0.404, with `DOTNET_ROOT` set and `DOTNET_MULTILEVEL_LOOKUP=0`. The machine has 16,310 MB of RAM. Only the net10 runtime is installed, so a net8 exe does not start. The library stays net8.0 and was loaded from a net10 harness.

The viewer's saved `settings.json` is seed 1985105851. The in-process timings construct `WorldGenerationSettings` in memory and do not write that file. Those defaults are occupancy 95, farm weights 60/95, acreage 12, major town 1 in 3.

## Window pass

The viewer was started with `dotnet run` from the 0.2 project directory, seed 31. It is a WPF window. Drag pans, the wheel zooms, and the zoom clamp is 0.1 to 100 miles. The window was closed at 01:52:11 and was not opened again.

The map is not square. The first ready line was `Seed 31 - 5 x 2.33 mi - ready`, so a 100-mile width is about 100 by 46.6 miles on that window. Chunks are 1 by 1 mile. The in-process 100-mile timings use a square from (−49.5, −49.5) miles, size 100, which is larger than the window. That square's elevation context is 14,161 chunks. The later stages use a 3-mile halo of 11,449 chunks.

| Clock | What the status line and console showed |
|---|---|
| 01:10:01.607 | Launch command started. |
| 01:10:03.572 | Process 15376 started. |
| 01:10:09.087 | Window already ready. `Seed 31 - 5 x 2.33 mi - ready`. The opening 15-chunk stage clocks were missed. Upper bound from process start to that observation is about 5.5 s. Console for those 15 chunks: elevation, water, population (one settlement, 49 settlers), rail ("evaluating 1 settlement sites"), roads, rural ("along 36 saved roads"), towns ("growing 1 settlements"), trees, then "Visible chunks are ready". |
| 01:11:05.593 | Zoom out three notches. Status went straight to `elevation loading 30 chunks`. |
| 01:11:09.499 | Ready, `Seed 31 - 8.64 x 4.03 mi - ready`. About 3.9 s. Rural logged 96 roads. |
| 01:11:22.147 | Pan. Elevation loading 15 chunks. |
| 01:11:25.649 | Ready, still 8.64 by 4.03 miles. About 3.5 s. Water and road status texts fell between the half-second polls. Those stages still logged. |
| 01:11:49.007 | Zoom toward the 100-mile clamp. `elevation loading 4788 chunks`. |
| 01:12:18.827 | Pan during that elevation pass. The in-flight batch kept going. The next stage used the new view. |
| 01:12:39.778 | Water for 2,482 chunks, not 4,788. Elevation of the first request took about 50.8 s, roughly 94 chunks/s, before the map was full of trees. |
| 01:13:14.729 | Population. Water was about 35.0 s. |
| 01:14:00.646 | Rail, "evaluating 15 settlement sites". Population was about 45.9 s. |
| 01:14:43.429 | Roads for 2,482 chunks. Rail was about 42.8 s. |
| 01:15:34.657 | Rural, "placing rural lots in 2234 chunks", "along 5143 saved roads". A second pan during roads did not change the status text. The pan script itself took 32 s because it read the huge console. |
| 01:18:00.392 | Towns for 2,234 chunks. Rural was about 145.7 s. The town child sat on a multi-GB stdin copy before it did much CPU. |
| 01:22:07.513 | Trees for 2,234 chunks. Towns were about 247 s. |
| 01:30:04.678 | Status became `Seed 31 - loading enabled spawners...`. Trees were about 477 s. Console then asked for 1,570 missing elevation chunks. |
| 01:31 to 01:40 | Second elevation pass. About 256/1570 at 01:31:34, 768/1570 at 01:35:17 (about 2.3 chunks/s), 1152/1570 at 01:38:01. |
| 01:40:10 | Water worker started. Status stayed `water filling 2554 chunks`. |
| 01:49 and 01:51:13 | Managed stacks. UI thread inside `MapSurface.OnRender` (`DrawTrees` on the first sample). Water callback blocked in `Dispatcher.Invoke`. The water child was at about 0.7 CPU and about 70 MB with no IO, so it had finished the cheap water work and was blocked writing stdout. |
| 01:52:11 | Window closed. The 100-mile view never reached ready. |

Two UI threads had each burned about 2,200 seconds of CPU by the time the window was closed. `OnRender` rebuilds a contour `StreamGeometry` for every visible elevation chunk and then calls `DrawEllipse` once per tree marker. Chunk results are applied with `Dispatcher.Invoke` at `Background` priority, so a multi-minute render prevents the next water chunk from landing. `DrawTrees` has no zoom cutoff. Gravel is hidden past 48 miles, and wall plans are requested only under 1.5 miles, but rural lots, towns, and trees are still generated at 100 miles.

An in-flight batch is not cancelled. `LoadVisibleChunksAsync` returns immediately when `_isLoading` is set. The comment there says a restart on every zoom discarded work and could starve water. The overview paid for that: 4,788 chunks, then 2,482, then 2,234, then a second pass of 1,570 elevation chunks and 2,554 water chunks.

## What the window could not show

- Per-stage clocks for the opening 15 chunks. The window was already ready at the first look.
- A few short stage texts between polls. The console still recorded those stages.
- A clean 100-mile ready time. The second pass never left the water status. Stacks showed the dispatcher blocked behind `OnRender`, not a stuck water solver.
- A 0.1-mile pan on a town and on a farm. Those were timed in process instead. See below.
- A click on Random seed. The button calls `RandomNumberGenerator.GetInt32(int.MaxValue)`. That call drew seed 624011666, and that seed was loaded in process.

UIA could not drive the window once the console was large (`0x80131505` on `FindAll`). Status text came from OCR of the status strip plus the generation console.

## In-process times

Release build. Seed 31 unless noted. Seconds are stopwatch readings from the run.

`dotnet test` on `CsharpSpawner.Tests`, Release: the host reported Passed 7, Failed 0, Total 7, Duration 28 s. Wall clock including the already-up-to-date build was 01:52:42.143 to 01:53:16.753.

### Six-mile window

Origin (−4.5, −3) miles, size 6. Same window as `Phase10To13Tests`. Context is 600 chunks. The halo for roads onward is 156 chunks.

| Stage | Seconds | Count |
|---|---|---|
| Elevation | 1.693 | 600 chunks |
| Water | 0.012 | 112 features |
| Population | 0.444 | 59 features, 87 settlers |
| Roads | 0.089 | 295 features |
| Rail | 0.233 | 13 features |
| Rural | 0.216 | 50,840 features |
| Towns | 0.186 | 839 features, 3 rural removals |
| Trees | 0.911 | 38,767 features |
| Walls | 0.025 | 161 plans |
| 30-ft mesh | 5.154 | 1,057 rows, 1,117,249 cells |

`WorldQuery.Load` for that window, cold process, Far then Mid then Near:

| Detail | Seconds | Result |
|---|---|---|
| Far | 3.870 | 26 features, 0 people, 0 walls, 0 terrain cells. Kinds: local_road 13, main_road 2, rail 1, rivers 8, settlement 1, town 1. |
| Mid | 7.652 | 3,151 features, 49 people, 0 walls, 1,117,249 terrain cells at 30 ft. |
| Near | 7.476 | 2,990 features, 49 people, 161 walls, same mesh. `town_building` is absent. The other Mid kinds remain. |

Far costs about the same as the stages without the mesh (about 3.81 s). It still generates the full pipeline and then filters. The mesh is the largest single 6-mile cost: 5.154 s of a 7.652 s Mid load.

Seed 31 place, from that query: settlement Misty Ridge, id `main_road:ew:0:settlement:0`, center (2640, 5940) feet. Town geometry center (2640, 5136) feet.

Mid kinds that matter for the phase check: bridges 2, one cattle yard with 3 pens, 1 feed road, 1 office shed, field_entrance 207, rural_driveway 37, rural_path 37, rural_lot 37, rural_yard 31, rural_building 145, town_alley 4, town_building 161, town_business 8, town_civic_ground 1, town_depot 1, town_door_walk 183, town_driveway 183, town_garage 42, town_lot 192, town_rail_elevator 1, town_sidewalk 32, town_street 17, tree_marker 1,698.

### Close views the window never reached

| View | Seconds | Result |
|---|---|---|
| Near, 0.1 mile, origin (0.45, 0.9227), Misty Ridge | 2.774 | 218 features, 161 wall plans, 361 terrain cells. The 0.1-mile rectangle still received every town wall in the touched chunks. People 0 because the settlers sit outside that rectangle. |
| Near, 0.1 mile, origin (−2, −1.5) | 1.380 | 2 features: one field entrance and one local road. This is not a farmstead. It is recorded so it is not mistaken for one. |
| Near, 0.1 mile, origin (−3.210, −2.570), De Vries farmstead `section:ns:-3:-3:farmstead:0` | 2.192 | 74 features: 1 lot, 1 driveway, 1 path, 1 yard, 4 buildings, 66 trunk markers, 0 walls, 380 terrain cells. |

A 0.1-mile query still pays the 9-mile context, which is why it takes seconds.

### One-hundred-mile stages

Square origin (−49.5, −49.5), size 100. No walls and no mesh in this stage run.

| Stage | Seconds | Count |
|---|---|---|
| Elevation | 19.922 | 14,161 chunks, about 711 chunks/s |
| Water | 0.233 | 2,579 features |
| Population | 2.883 | 900 features, 1,824 settlers |
| Roads | 16.679 | 11,449 chunks, 22,444 features |
| Rail | 1,526.503 | 1,213 features, working set stayed about 171 MB |
| Rural | 56.988 | 4,485,126 features, 1,928 MB |
| Towns | 10.111 | 37,505 features, 58 rural removals |
| Trees | 1,636.455 | 3,488,273 features, about 1,917 MB |

Sum of those stage clocks is 3,269.8 s. A stack during trees was inside `TreeSpawner.NearChunk`, scanning the rural list for every halo chunk.

### WorldQuery.Load at 100 miles

| Call | Seconds | Result |
|---|---|---|
| Far, seed 31 | 2,002.624 | 2,565 features, 0 people, 0 walls, 0 terrain cells. Working set 4,900 MB at the result line. Kinds: local_road 1,818, main_road 6, rail 23, rivers 586, settlement 66, town 66. |
| Far, seed 624011666 | 1,717.663 | 2,531 features, 0 people, 0 walls, 0 terrain cells. Working set 5,009 MB. Kinds: local_road 1,818, main_road 6, rail 14, rivers 595, settlement 49, town 49. |
| Mid, seed 31 | stopped | No result line. See below. |
| Near, seed 31 | not run | The chain stopped when Mid was killed. |

Seed 624011666 is the `GetInt32(int.MaxValue)` draw that stands in for the Random seed button. Both Far loads return a few thousand features and hold about 5 GB while they do it.

The isolated stage sum is 3,269.8 s. The seed-31 Far load finished in 2,002.6 s. A working-set trace of that process explains the gap. Memory stayed under 400 MB for about the first three minutes, jumped to about 2.5 GB as the rural markers were allocated, then sat between 2.5 and 3.1 GB on one core until the last seconds, when it stepped to 4,900 MB as the features were converted and clipped. That long flat stretch matches the 1,636 s tree stage. The 1,526 s rail stage did not appear as a 25-minute flat-memory stretch inside this Load. Both calls use `GenerateRailChunks`. The stage timer is still a real measurement of that function on this window. The end-to-end Far number is the one that matches what a caller waits for.

Mid started at 03:55:40.912. It ran one core the whole time. Working set reached about 4,900 MB around 04:25, the same plateau Far ended on, then climbed slowly to 6,032 MB. At 05:20:34 free RAM was 793 MB and the process was stopped. CPU time on that process was 5,088.7 s. There is no terrain-cell count and no finish time. This PC had about 5.5 GB free when the Far runs started, so a 12 GB working set was not available without paging. Near was not started, because it builds the same 30-foot grid.

`MakeAnalyticGrid` allocates one row at a time. A 100-mile grid at 30 feet is 17,601 by 17,601 samples, about 310 million cells. The 6-mile grid was 1,117,249 cells in 5.154 s. Each 100-mile sample also calls `CarvedElevation` against the rivers found in a 16-mile margin. That is why the mesh was still running, and still growing, 50 minutes after the Far-sized generation work had already reached 5 GB.

## Phase check against INSTRUCTIONS.md

| Instruction | Verdict | Proof |
|---|---|---|
| Phase 0. Library, feet at the boundary (X east, Y elevation, Z north), generators moved | Partial | `CsharpSpawner.csproj` is net8.0, namespace `CsharpSpawner`. `WorldQuery` and `WorldPosition` are feet. `LegacyMapFeatureConverter` multiplies miles by 5280. Internal spawners still use miles. The WPF project `RootNamespace` is still `csharp_spawnerv0._1`. Spawners still emit `MapFeature`. The query returns `WorldFeature`. |
| Phase 1. `WorldQuery.Load(seed, settings, originXFeet, originZFeet, sizeFeet, detail)` | Done for the library | `WorldQuery.cs`. Tests call it. The viewer does not. That gap is under phase 11. |
| Phase 2. Settings tab and `settings.json` | Done, with default drift | `MainWindow.xaml.cs` loads, edits, and saves settings on Regenerate. Closing the window does not write the file. Occupancy 95 matches the 0.2 table. Farm 60/95, acreage 12, and major town 1 in 3 do not match the table. See the drift list. |
| Phase 3. Elevation, 20-foot contours, half-mile routing grid | Done | `WorldGenerator.cs`: `ContourIntervalFeet` 20, `ContourCellFeet` 660, routing step `FeetPerMile / 2`. |
| Phase 4. Water widths by stream order, crossings kept | Partial | `RoadSpawner.cs` and `TransportSpawner.cs` keep the river, creek, county-bridge, and rail-bridge behavior. Far drops rivers with `StreamOrder < 3` (`WorldQuery.cs`). The decisions call order 2 and higher a major river. |
| Phase 5. Place names from the stem lists, stable for a site id | Done | `PopulationSpawner.cs`. Seed 31 town is Misty Ridge, id `main_road:ew:0:settlement:0`. |
| Phase 6. Rail crossings at least 45 degrees, pass-through 70/25/5 | Partial | `HasShallowRoadCrossing` rejects sine below sin(45°) against `link.MainRoads`. If all 24 bows fail, `AddClippedTownRail` returns and drops the link. The comment above that return says the direct approach is kept. `RailPassThrough` stores industrial, commercial, or residential from the same hash as `RailApproachOffset`. Nothing rejects houses on a residential track. `mainCrossesTrack` is set false and never set true, so Main Street is the middle horizontal street. Base streets are not angle-checked. Extra streets call `HasSafeRailCrossings`. |
| Phase 7. Gravel cut by a major river, county line bridged, creek on gravel kept, bank link within 6 miles, rough ground at 105 ft relief or 150 ft rise | Done in the spawner, partial in the test | `RoadSpawner.cs` lines for the 105/150 cut and the bank-link search that starts at 6.01 miles. `SectionRoadCrossingFixtureKeepsRiverAndCreekRules` checks the creek, the order-2 river, the county bridge, and that a bank-link id exists. It does not measure the 6-mile distance. |
| Phase 8. One driveway, door path, shelterbelt, gambrel, field entrances, cattle yards, coop corners | Partial | `RuralSpawner.cs`. The 6-mile Mid query has 37 driveways, 37 paths, 207 field entrances, and one cattle yard with pens, a feed road, and an office. The De Vries 0.1-mile query has one driveway, one path, one yard, and four buildings. Gambrel uses the name "Existing gambrel barn", `YearBuilt` 1989, and `GambrelBarnChancePercent` 12, and it replaces the generic barn with a machine shed. There is no rural garage feature, so the separate-garage branch is absent. Coop corners exclude any settlement within `CoopCornerTownExclusionMiles` (2). There is no extra check that the nearby town already has the rail elevator. |
| Phase 9. Towns, occupancy 95, outline, dead ends, shops, sidewalks, door walks, extra streets, alleys, civic grounds | Partial | `TownSpawner.AddTownV2`. Occupancy 95 is the live default and the 0.2 instruction. It is not a bug. The 6-mile town has alleys, sidewalks, door walks, a civic ground, a depot, an elevator, and garages. The outline chamfers corners when a rail, paved road, or river hits the rectangle. It does not cut around the corridor. Sidewalks: Main Street and School Street always both sides. Other streets with roll at least 40 get both sides. Roll under 40 keeps exactly one side. There is no roll that drops both sides. The offset is `road.Width/2 + (width/2 − inset)/5280`. Defaults 4.5 and 2.25 cancel, so the walk centerline sits on the street edge. Shop split is a per-type roll for the strip types, about 30 percent when a paved road is within 1.5 miles, not a count split. Dead-end cap is 0, 1, 2, or 3 by lot count. COURT, PLAT EDGE, and OUTLYING ROAD count. Excess becomes BOULEVARD. Alleys use `TownAlleyChancePercent` 55. |
| Phase 10. Trees as trunk dots, blocked by roads, buildings, drives, rails, rivers, lakes | Done | `TreeSpawner.cs` emits `tree_marker` only. The 6-mile Mid result keeps 1,698 of them after the clip. The halo had generated 38,767. |
| Phase 11. Far, Mid, Near payloads, 30-foot mesh, SpawnGuide walls, viewer uses the query | Partial | The three 6-mile loads match the payload rules: Far drops people, mesh, and detail. Mid has the mesh and 161 `town_building` footprints. Near swaps those footprints for 161 wall plans. Empty lots stay empty because walls are generated only for `town_building`. `WallPlanSpawner` writes elevation Y as 0. A 0.1-mile Near query still returned all 161 plans. The viewer never calls `WorldQuery`. Its cutoffs are different, and at 100 miles it still generates rural, towns, and trees. |
| Phase 12. Seed checks | Done, with the limits written in the tests | Seven tests, 7 passed, 0 failed, 28 s. Overlap, determinism, rail angle, road ends, building overlap, SpawnGuide story height, and the crossing fixture. Road ends allow COURT, PLAT EDGE, OUTLYING ROAD, court bulbs within 60 ft, and planned section-road cutouts. The 0.2 status section approves those cutouts. Building overlap covers rural_building, town_building, town_business, and town_garage. |
| Phase 13. Cleanup | Partial | No `AddTown` method remains (`AddTownV2` is the live one). README points at the library, the query, settings, and `CsharpSpawner/SpawnGuideHouse`. `src/SpawnGuide` is still the reference copy. `.gitignore` ignores `bin/` and `obj/`, and both directories are still present. The WPF namespace was not renamed. |

## What moved between the 0.13 instruction list and 0.2

A change that the 0.2 README, the 0.2 instructions, or the tests record is a decision. A change that only the code picked up is drift. Nothing in the decision list should be reverted.

### Decisions

- Home occupancy is 95 percent. `csharp-spawnerv0.13\INSTRUCTIONS.md` still says 72 percent in the settings table and in phase 9. `csharp-spawnerv0.2\INSTRUCTIONS.md` says 95 percent in both places. The README says "a 95% home-lot occupancy default." `HomeOccupancyPercent` defaults to 95. The 72 percent line is stale. 95 percent is not a bug.
- Phase 1 in 0.13 said Mid returns the full map and Far and Near may return the same data until phase 11. The 0.2 phase 1 states the Far, Mid, and Near payloads.
- Phase 13 in 0.13 said to delete `bin` and `obj` under 0.13. The 0.2 phase 13 says to leave build output and ignore it. That was followed.
- The central square rate is explicit 15 percent in the 0.2 phase 9 table. The 0.13 line said "a separate roll" and gave no percent.
- The 0.2 instructions add a phase 10–13 status section. It records the section-road cutouts as approved, which is why the road-end test allows them.
- The README records the query API, the stage order, the four town plans, the copied SpawnGuide house plans, and the 97-1/8 inch story height. `SpawnGuideArchetypesRetainOpeningsAndExactStoryHeightFor1993` locks that height.

`csharp-spawnerv0.15\README.md` and the 0.2 README differ on the title line. `Phase10To13Tests.cs` is the same seven facts in both trees. This review did not edit 0.15.

### Drift

The 0.13 and 0.2 instruction tables still say the same numbers. The code moved, and the README does not name the new numbers.

| Setting | Instruction table, 0.13 and 0.2 | `WorldGenerationSettings` default |
|---|---|---|
| Major-town chance | 1 in 4 | `MajorTownChanceDenominator = 3` |
| Farm count weights | 15 / 65 | `FarmCountZeroBelowPercent = 60`, `FarmCountOneBelowPercent = 95` |
| Acreage chance | 25 percent | `AcreageChancePercent = 12` |

`MainWindow.LoadSettings` rewrites a saved file that still has 15/65/25 and major-town 4 onto 60/95/12 and 3. The comment says "Migrate the former defaults only; preserve customized density settings." That comment is not in the README or the tests. Treat these three as drift. Leave occupancy 95 alone.

Settings that the instruction prose never numbered, and that the code now has, include motel chances, `TreeDensityPercent` 20, street-tree 15, a grove every 4 sections, and `CattleYardChancePercent` 25. The cattle-yard prose requires the parcel rules and does not give a percent, so the 25 percent roll is an extra gate.

## Speed

The goal is chunk loading that keeps up with pan and zoom, especially a full 100-mile view and a new seed at that zoom. The window never became ready at 100 miles. A new seed at that zoom was not clicked. The in-process stand-in, seed 624011666 at Far, took 1,717.663 s and peaked near 5 GB to return 2,531 features.

These are the changes the timings point at. They are not implemented in this thread.

### 1. Do not build yard markers or trunk dots for a far overview

What it cuts: `RuralSpawner` emitted 4,485,126 features in 56.988 s, and `TreeSpawner` then spent 1,636.455 s scanning them to emit 3,488,273 more. Far throws that work away and keeps 2,565 features. The seed-31 Far load was 2,002.624 s, and the trace shows most of that wait sitting on the tree scan. At 6 miles the same scan is 0.911 s and the rural stage is 0.216 s, so the bug is the overview, not the farm close-up.

Likely saving: on the order of the 1,636 s tree stage plus the 57 s rural stage, which is most of a 100-mile Far load. The viewer's tree handoff was a separate 477 s for only 2,234 chunks, and `DrawTrees` then stalled the second pass from 01:40 until 01:52.

Risk: a later zoom has to use the same hashes, or the trunks move. Generate the markers when the detail is Mid or Near, from the same ids. Shelterbelt rows that rural already reserves can stay as a count or a span until that zoom.

### 2. Index markers once, then let each chunk read its own bucket

What it cuts: `TreeSpawner.GenerateChunk` calls `NearChunk` on the whole rural list, the town list, the roads, the rails, and the water, for every halo chunk. At 100 miles that is 11,449 passes over 4.5 million rural features. The stack sample was in that predicate. `WaterFeaturesNear` in `TransportSpawner` does the same shape of scan, including a fresh `ToList` of the input on every chunk.

Likely saving: nearly all of the 1,636 s if the index is one pass and each chunk reads a local bucket. At 6 miles the same function is 0.911 s, so the saving there is under a second. This is the right fix if trunk dots must still be generated for a wide Mid view.

Risk: low when the index is only a lookup. Placement hashes stay on the marker id. A bucket that is too coarse just moves the scan, so size the cells to the margins `NearChunk` already uses (0.04 to 0.12 miles).

### 3. Build each town-rail bow once, and stop running `PlanTile` on the query path

What it cuts: the isolated rail stage, 1,526.503 s, one core, working set flat at about 171 MB, 1,213 features out. `GenerateRailChunks` builds the links once, which is right, and then `GenerateRailChunk` does three expensive things for every halo chunk. It scans water. When `HasRailCorridor` is true it scans every road and builds a `RoadCrossingIndex`. It always calls `PlanTile`. Then `AddClippedTownRail` retries up to 24 bows against `link.MainRoads`, and `MainRoads` is the entire road list, for every chunk whose box touches the link. The viewer worker does none of the `PlanTile` or full-road scan. It only clips links. That is why the overview's rail status lasted about 43 s for 2,482 chunks while the in-process stage took 25 minutes for 11,449.

The Far loads did not show a 25-minute flat-memory rail stretch, so do not subtract 1,526 s from the 2,002 s Load and call the rest the other stages. The 1,526 s is the measured cost of this function when it was timed alone. The code that can produce it is still on the Load path.

Likely saving: the 1,526 s case, if the bow is chosen once per link and chunks only clip the polyline. `PlanTile` is a second rail network the viewer never draws. Skipping it on the query path removes a per-chunk route search the map does not show.

Risk: the bow has to stay seed-stable. One search against the same road set is the same line the per-chunk search would accept. The viewer and the query can already disagree, because the worker never sees section roads during the bow test. Aligning them changes query geometry. The rail-angle test would need to move with that change. Do not change the water-crossing kinds while doing it.

### 4. Stop shipping the world through a child process as JSON

What it cuts: one child of the same exe per stage, the full accumulated feature list on stdin, and one JSON line per chunk on stdout. The worker reads all of stdin before it generates anything. For 2,234 chunks the viewer spent about 247 s in towns and about 477 s in trees. In process, towns for 11,449 chunks took 10.111 s. The parent was the busy process during those handoffs, and the town child was holding a gigabyte-scale copy.

Likely saving: most of the town and tree time the window measured, because that time is the copy and the UI-thread deserialize, not the spawner. Elevation in process at 100 miles was 19.922 s for 14,161 chunks. The window's first elevation pass managed about 94 chunks/s, then about 2 to 3 chunks/s once trees were on screen.

Risk: a fault in one stage currently dies in the child. An in-process call needs a cancel that drops chunk ids the camera has left, after the current chunk, and keeps chunks that are still visible. The existing comment is right that killing the worker mid-batch can skip water. Cancelling the request list is a different operation from killing the process.

### 5. Stop redrawing every trunk and every contour while results arrive

What it cuts: the stall from 01:40:10 to 01:52:11. Water had a line ready and could not enter the UI thread because `OnRender` was inside `DrawTrees`. Elevation of the second pass fell from about 94 chunks/s to about 2 to 3 chunks/s after the map filled with markers. `Dispatcher.Invoke` at `Background` priority waits for that render.

Likely saving: the second pass, and the difference between 94 chunks/s and 2 chunks/s. At 100 miles the overview does not need a dot per trunk or a rebuilt contour geometry per chunk per frame. Gravel is already skipped past 48 miles. Trunks and contours need the same kind of gate. A frozen bitmap of the finished chunks would let the next chunk land without redrawing the whole overview.

Risk: under 1.5 miles the trunks are the phase 10 picture and should stay. The gate belongs on the draw, and on whether the stage runs at all (item 1). Building the geometry once and freezing it is safer than rebuilding it on every invalidate.

### 6. Keep the 30-foot mesh on small windows

What it cuts: on the 6-mile Mid load the mesh is 5.154 s of 7.652 s. On the 100-mile Mid load the process was stopped after 5,088.7 s of CPU, at 6,032 MB, with 793 MB free, still inside `WorldQuery.Load`, with no cell count. Far already skips the mesh and still took 2,002 s because of the markers. The viewer does not call `WorldQuery`, so today's overview stall is the child pipeline and `DrawTrees`, not this grid. The mesh becomes the wall as soon as a 100-mile Mid or Near query is what the camera uses.

Likely saving: the entire unfinished 100-mile mesh, and about 5 s on every 6-mile Mid or Near load that does not need the grid yet. `GenerateDetailTerrainGrid` also runs `FindRiverBeds` with a 16-mile margin before the first sample.

Risk: phase 11 says Mid and Near include the 30-foot samples. Keep that for windows of a few miles, where 1.1 million cells finished in 5.154 s at 94 MB. For a 100-mile view, serve Far until the camera is close enough that the grid fits. A coarser step would violate the 10-to-30-foot check in `GenerateDetailTerrainGrid`, so the lever is when the grid is requested, not a new step size smuggled into Far.

### 7. Cancel a batch whose view has moved, and do not prune the overview on the way to a close zoom

What it cuts: the window generated 4,788 chunks, then 2,482, then 2,234, then started a second pass of 1,570 plus 2,554 and never reached ready. `PruneCache` on mouse-up drops chunks outside the view plus 2 miles, all stage dictionaries together. A trip from 100 miles to 0.1 miles throws the overview away, so the trip back regenerates it.

Likely saving: the whole second pass on this run, which is the time from 01:30 to 01:52 with no ready state. A later return to 100 miles would skip a second 2,002 s Far-sized rebuild if the coarse chunks were still in memory.

Risk: memory. 4,788 chunk records plus trunk markers is what filled the UI thread. Keeping a coarse layer (roads, rail, rivers, settlement points) and dropping markers is the split that matches item 1. Cancelling must not drop a water chunk the new view still needs. Drop ids that fail the new visibility test. Leave finished visible chunks in the cache.

## Other tweaks

This list is separate from the speed list. It does not add CMP, box culverts, or 1989 bridge-type classes. The 1989 on a gambrel barn is a build year, and it should stay that way.

### Terrain

Contours are extracted per chunk at 660 feet and travel with every elevation result, including chunks the 100-mile view cannot usefully draw. A far payload can carry the elevation samples and skip the contour polylines until the camera is close. `FindRiverBeds` searches a 16-mile margin on every detail-grid request. The route cache helps the second call in one process. Each viewer child starts cold.

### Water

Far omits stream order 2. The decisions call order 2 and higher a major river, and order 2 is what cuts gravel. A far map that drops those rivers will show gravel sections the close map cuts. Keep order 2 in the far river set. Small creeks on gravel should keep the bridge marker they have.

### Roads

The bank-link search starts at 6.01 miles and the fixture only checks that an id exists. Add an assertion on the distance so a later edit cannot stretch the link. Rough-ground cuts at 105 feet of relief or 150 feet of rise match the decisions. Gravel is hidden past 48 miles in the viewer and still generated. That draw cutoff is fine. The generation cutoff belongs in the speed list.

### Rail

When every bow fails, the link is dropped. The comment says the direct approach is kept. Those two should agree, and the tests should see the chosen line. The residential pass-through is a stored label and a 0.25-mile offset. Houses are still placed on that track. The 70/25/5 rule needs a rejection, or the label should stop saying the track crosses town. Base-grid streets do not go through `HasSafeRailCrossings`. Extra streets do. The angle rule should cover the streets that actually cross the rail, including Main Street. `mainCrossesTrack` is initialized false and never updated, so the main street is always the middle east-west row.

### Rural yards

The driveway runs from the road, past the house, to the machine shed, gambrel, and bins, and a dirt or concrete path reaches the front door. The De Vries query shows that set. The instruction also asks for a short branch when the garage is separate. Rural lots have no garage feature, so that branch cannot fire. Town lots do have garages at `GarageChancePercent` 42. Coop corners bow out of any settlement within 2 miles. The instruction also says to skip a coop when that town already has the rail elevator. A town just outside 2 miles, or a town inside 2 miles that has no elevator, is not what that sentence describes.

### Towns

The outline chamfer is a saved non-rectangle, and the README's "trim around nearby corridors" is stronger than what the corners do. Cut the polygon along the rail, the paved road, or the river that entered it. Sidewalk centerlines sit on the curb because the width and the inset cancel. Pick an inset that leaves a walk beside the curb, and add the roll that omits both sides on the quiet streets. The shop rule hashes each strip type. A count split would cap how many of those six types leave downtown. Dead-end caps, bulbs, and alleys are present and showed up in the Misty Ridge counts.

### Buildings

Wall elevation is hardcoded to 0 in `WallPlanSpawner`, so a house on a slope gets a flat plan. Sample the terrain at the footprint. Wall plans are filtered by chunk, not by the query rectangle, which is why a 0.1-mile town query returned all 161 plans. Clip to the request after generation, or the near payload stays chunk-sized. Empty lots stay empty, which is what phase 9 and phase 11 ask for.

### Viewer

The window title and assembly are 0.2. The root namespace is still `csharp_spawnerv0._1`. Rename it when the next break is cheap, so the next reader does not look in the 0.1 tree. The status line is the right place to show stage and chunk counts. It already does. It goes stale when a pan happens mid-batch, and it can sit on "water filling" while the UI thread is inside `OnRender`. A status that reports the render, or a render that yields, would have made the 01:40 stall obvious without a stack dump. The headless runner in `ConsoleHost.cs` only generates a 5 by 5 chunk box. It cannot time the overview. The in-process query is the right harness for that.

### Library

`WorldQuery` is the API a Unity client can call. The viewer still speaks `MapFeature` and miles. One pipeline, the query, would remove the second rail path and the JSON copy together. Typed records exist. The spawners still fill `MapFeature` and convert at the edge. Pushing the conversion inward can wait until the speed items land, because a wide conversion of 4.5 million markers is part of the 5 GB Far working set.

### Tests

The seven tests passed. Extend them where the instructions are sharper than the asserts. Measure the bank link's distance. Include the rural and town kinds the overlap test skips, such as the elevator, the depot, the civic buildings, and the cattle office. Assert that a 0.1-mile Near query does not return wall plans whose footprints lie outside the rectangle. Assert Far still contains an order-2 river when one crosses the window. A timing test is optional. The numbers above are enough to see a regression if someone later wraps the same calls.

### Unity handoff

Give the client `WorldQuery.Load` in feet, with Far as the overview. Mid and Near are for the neighborhood, where the 30-foot grid finished in 5.154 s and the De Vries yard came back in 2.192 s. Walls arrive already fastened, with openings and the 97.125-inch story height. Their Y is 0 today, so the client would plant every wall at sea level unless the spawner samples elevation first. People, trunk dots, and yard markers are the bulk of a wide generate-and-discard. The handoff payload for a 100-mile Far view is the 2,565 features that survived the filter, not the 5 GB used to find them. Seed 31's town is Misty Ridge at those coordinates. Seed 624011666 is one random world of the same shape: about 29 minutes, about 5 GB, about 2,500 far features.

## One spawner at a time

18 Nov 2026, after the seed 31 window pass above. Same 0.2 viewer, saved seed 1985105851, settings file left alone. Water, population, rail, roads, rural lots, towns, trees, and near walls were turned off. The view was zoomed to the 100-mile clamp. Each box was turned back on only after the previous stage reached its pause. No generator, viewer, test, or settings file was edited.

The status text was read from the `StatusText` element. A screen crop of the title bar missed that line on the first attempt. The first elevation request, 4,833 chunks, was already `paused at Water` by 13:46:42, under 61 s after the zoom at 13:45:41. The timed elevation figure is a second pass: zoom in to drop the overview cache, zoom back out, then 4,813 chunks.

Later stages report 4,848 chunks. A map click is what starts a stage after the box is checked. Checking the box through UI Automation set the toggle to On and did not run `SpawnerToggle_Click`, so nothing called `ScheduleVisibleChunks`. Water sat on `paused at Water` from 13:51:45 until a map click at 14:18:42. The same click was used after every later box.

| Stage | Chunks | Clock | Stop status |
|---|---:|---:|---|
| Elevation | 4,813 | 49.9 s | paused at Water |
| Water | 4,848 | 100.5 s | paused at Population |
| Population | 4,848 | 180.1 s | paused at Rail |
| Rail | 4,848 | 244.6 s | paused at Roads |
| Roads | 4,848 | 159.2 s | paused at Towns |
| Rural lots | 4,848 | 583.3 s | paused at Towns |
| Towns | 4,848 | 998.4 s | paused at Trees |
| Trees | 4,848 | 2,035.7 s | 100 x 46.86 mi, ready |
| Near walls, 100-mile view | | 0.3 s | stayed ready, no house-plan load |
| Near walls, zoomed to 1.05 x 0.49 mi | 6 | 0.3 s | ready |

Water's 100.5 s runs from the map click at 14:18:42.786 to `paused at Population` at 14:20:23.303. Roads run from the click at 14:27:30.611 to the status change at 14:30:09.814. The other clocks start when the loading sentence appears and stop on the next pause.

Roads never showed `paused at Rural lots`. `PausedStatus` only returns that sentence when the rural toggle is both on and off, because `ruralEnabled` is already `SpawnerIsEnabled(RuralSpawnerToggle)`. With the box off, a finished road grid skips ahead to `paused at Towns`.

### Elevation

49.9 s for 4,813 chunks is about 96 chunks/s. That matches the earlier 50.8 s for 4,788 chunks, taken before the map was full of trees. With only contours on screen, the UI kept up. The slow second pass in the seed 31 run was the tree dots blocking the dispatcher, not the elevation worker.

Skip contour polylines when the view is tens of miles wide. `OnRender` rebuilds a `StreamGeometry` for every visible elevation chunk, and each result calls `InvalidateVisual`. At this zoom a coarse hillshade, or one cached bitmap, is the picture the window can actually show. Batch the chunk results so one invalidate covers the frame, not one per chunk.

### Water

100.5 s in the window, against 0.233 s in-process for 2,579 water features on the square 100-mile harness. The worker is not the cost. The child still receives the elevation grid as JSON, and each result is applied with `Dispatcher.Invoke` at Background priority, which waits until `OnRender` returns. While that waits, the window redraws the contours.

Send the water grid and the river beds, which `LoadWaterAsync` already limits, and apply a batch of chunks without waiting on the render. Do not redraw every contour when a river chunk arrives.

### Population

180.1 s, against 2.883 s in-process for 900 features on the square harness. The callback logs one line per result, inside the dispatcher invoke: "Population spawner saved N named settlers...". A 15-chunk opening view already repeated that sentence. At 4,848 chunks the console becomes the cost, and it is also what made a full UI Automation tree walk fail in the earlier pass.

Log one summary when the stage ends. Keep the per-chunk work off the UI thread.

### Rail

244.6 s. The status stayed on "connecting major and county-road towns by rail in 4848 chunks..." the whole time, with no chunk counter. The child was using CPU, so this was compute plus the pipe, not a frozen window. The viewer worker does not call `PlanTile`. The 1,526 s isolated rail time from the square harness is that other path. This window measured the cheaper one, and it was still four minutes for half as many chunks as that harness.

The child is given the water list, the town list, and the road list as JSON at the start. Build the rail links once in-process and return the clipped geometry without a second copy of those lists. Leave the crossing kinds as they are. Major rivers still cut gravel, paved county roads and rail still get bridges, and small creeks on gravel stay bridgeable.

### Roads

159.2 s for 4,848 chunks. The square harness laid 11,449 chunks in 16.679 s, about 690 chunks/s. This window managed about 30 chunks/s. Gravel is already skipped in `OnRender` when the view is wider than 48 miles, and the worker still generates it.

At the 100-mile clamp, generate the paved county lines and skip the gravel section grid until the view is inside 48 miles. Apply the road chunks in a batch so the window does not redraw the whole grid per chunk.

### Rural lots

583.3 s, and the viewer process grew to about 2.0 GB during the stage. The square harness produced 4,485,126 rural features in 56.988 s and then threw the yard markers away on a Far query. This window keeps them and draws from them.

`DrawRuralLots` has no early return for a wide view. Yards, drives, paths, and buildings are gated at 12 miles or closer, but the loop still reads `feature.Points` on every feature first. At 100 miles that work draws nothing. Return before the loop when the view is wider than 12 miles. Do not build the yard and trunk markers for a far overview. Keep them for a later zoom, keyed by the same hash the spawner already uses.

### Towns

998.4 s, from 14:43:37 to `paused at Trees` at 15:00:16. The status text never changed during that wait. At 14:55 the town child had about 15 s of CPU and was holding about 2.0 GB after a peak near 4.0 GB. The window had climbed past 3,700 s of CPU.

A stack at 14:49 put the UI thread in `MapSurface.OnRender` inside `DrawRuralLots`, on `MapFeature.get_Points`. The town callback was blocked in `Dispatcher.Invoke` at Background priority, inside `SpawnerProcessHost`, on `StreamReader.ReadLineAsync`. Background priority does not run until the render returns. The child stayed near 15 s of CPU until the stage ended, so almost all of the 998 s was that rural walk, not town layout. The square harness laid out 11,449 town chunks in 10.111 s. `LoadTownsAsync` also sends the rural feature list into the child, which is the 4 GB copy.

Two changes, in this order. Make `DrawRuralLots` return immediately when the view is wider than 12 miles, so a town result can land without walking every yard. Stop shipping every rural marker to the town child. Far already collapses a town to its point in `WorldQuery`. Do that in the viewer at this zoom too.

### Trees

2,035.7 s, from 15:00:17 to `100 x 46.86 mi - ready` at 15:34:13. The status never counted chunks. A stack during the run put the child in `TreeSpawner.NearChunk`, inside the `ToList` over nearby features. By 15:33 that child had about 630 s of CPU and was holding about 1.2 GB. The window was at 4.6 GB, and free RAM dipped to about 1.2 GB, then recovered to about 1.4 GB. The square harness spent 1,636.455 s on 11,449 chunks. This window spent longer, 2,036 s, on 4,848 chunks.

`DrawTrees` still draws every `tree_marker` with `DrawEllipse` and no zoom gate. Return above 1.5 miles, the same cutoff walls and labels already use. Index the markers once, cell size about 0.04 to 0.12 mile, instead of scanning the full rural, town, road, rail, and water lists per chunk. Do not build the trunk dots for a 100-mile view at all.

### Near walls

At the 100-mile clamp the checkbox changed nothing. The status stayed `100 x 46.86 mi - ready`, and the clock from the click to that same sentence was 0.3 s. `GetChunksNeedingWallPlans` returns empty when the view is 1.5 miles or wider.

One wheel event then took the view to 1.05 x 0.49 miles. The next status was `loading near-scale house plans in 6 chunks...`, and the line after that was `1.05 x 0.49 mi - ready`. That span was 0.3 s. The neighborhood under the cursor was still cached, so this number is the wall pass for 6 chunks, not a rebuild of elevation through trees. `PruneCache` still drops chunks outside the view plus 2 miles on that zoom, which is why a later return to the overview would load again. Wall `Transform` Y is still hardcoded to 0, and the plans are not clipped to the view rectangle.

### What this pass changes in the speed list

The seed 31 overview died in `DrawTrees` after the trunks existed. This pass spent 998 s of the town stage inside `DrawRuralLots` before a town result could land, then spent 2,036 s placing trunk markers and drew them with no zoom gate. The rural walk is the first draw cutoff to add. The tree cutoff is the second, and this window measured it: the stage finished, and the frame was the expensive part of a 34-minute clock whose child used about 10 minutes of CPU.

Isolated elevation at 96 chunks/s shows the worker can feed the UI when the frame is cheap. Water at 100 s, population at 180 s, rail at 245 s, and roads at 159 s are the JSON child and the per-chunk dispatcher invoke, measured with the other spawners off. Batching those results is the fix those four numbers share. Rural at 583 s is that cost plus building markers the 100-mile view will not draw.
