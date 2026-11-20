# C# spawner 0.2 phase implementation record

Work in [csharp-spawnerv0.2](C:\Users\hhofe\Documents\grokkable projects\t3 testbed\csharp-spawnerv0.2). Leave 0.1, 0.11, 0.12, 0.13, 0.14, and 0.15 alone. Leave [src/SpawnGuide](C:\Users\hhofe\Documents\grokkable projects\t3 testbed\src\SpawnGuide) in place after phase 11 copies the three house archetypes out of it.

Spawn order stays the order in [MainWindow.xaml.cs](C:\Users\hhofe\Documents\grokkable projects\t3 testbed\csharp-spawnerv0.2\MainWindow.xaml.cs): elevation, water, population, rail, section roads, rural lots, towns. Two passes run after towns: trees, then near-scale wall plans.

## Decisions already made

- Keep the 0.13 water crossings. A major river, stream order 2 or higher, still cuts a gravel or dirt section. Paved county roads, the lines where `line % 12 == 1`, still get a bridge. Rail still gets a bridge over a river. A small creek on gravel still gets the existing bridge marker and stays crossable. Bank links still run to the paved crossing within 6 miles. Rough ground still drops a gravel section when the relief is over 105 feet or the cumulative rise is over 150 feet. Do not add CMP, box-culvert, or 1989 bridge-type classes.
- Rail-versus-road angles are a new rule. They do not replace those water bridges.
- The historical "roads before rail" reorder is not in this list. Rail stays before the section grid.
- Towns may still require the existing water search. Sites stay on the east-west main roads, 12 miles apart, with a fallback within 1.5 miles of water.
- There is no year setting. The world stays the current undated snapshot.
- Cattle yards are in. The old "never confirmed" note is closed by this list. A cattle yard is its own parcel, not a farmstead and not a town.
- Trailer parks stay out.
- Do not open the `port/builder-guy-towns` branch. This list is the work.

## Phase 0. Library and coordinates

Do this before new map features. Later phases add fields to this library, not to the WPF window.

1. Add a class library project `CsharpSpawner`, target `net8.0`, no WPF, no UI types. The existing WPF app stays `net10.0-windows` and references the library.
2. Move generation into the library. That is `WorldGenerator`, `ElevationWorker` generation, `WaterSpawner`, `PopulationSpawner`, `TransportSpawner`, `RoadSpawner`, `RuralSpawner`, and `TownSpawner`. The window, `MapSurface`, and process hosting stay in the WPF project.
3. Change the root namespace from `csharp_spawnerv0._1` to `CsharpSpawner`.
4. Store world positions in feet. X is east. Z is north. Y is elevation. The current map Y is north, and the current unit is miles. Multiply by 5280 on the way into the library. The map control converts feet back to miles only for pan, zoom, and drawing.
5. Library methods must run in process. The viewer may keep launching child processes, but each spawner also exposes a direct method the tests and a future Unity build can call.
6. Replace the single `MapFeature` bag for new work with typed records. Keep a converter so the current viewer still draws. A road has centerline, width, and surface. A building has id, footprint, facing, and archetype. A person keeps the settler id, name, and lot id.

## Phase 1. Query

1. Add `WorldQuery.Load(seed, settings, originXFeet, originZFeet, sizeFeet, detail)`.
2. The same arguments return the same records. The set of camera chunks must not change a site, a road, or a name. Generate the neighborhood the rectangle needs, then clip.
3. Every id already used, `RouteId` and settler id included, stays stable for a seed.
4. `detail` is `Far`, `Mid`, or `Near`. Far returns settlement/town markers, major rivers, paved roads, and rail. Mid returns detail footprints with a 30-foot terrain mesh. Near also returns SpawnGuide wall plans.

## Phase 2. Settings tab

1. Add a Settings tab to the WPF window, beside the map. The seed box becomes editable. Random seed stays.
2. A Regenerate button applies the tab. Changing a value does not rebuild on every keystroke.
3. Save the tab to `settings.json` next to the executable. Load it on startup.
4. Put the current behavior in as defaults.

| Setting | Default |
|---|---|
| Settlement spacing | 12 miles |
| Minimum settlement separation | 9 miles |
| Water search | 1.5 miles |
| Major-town chance | 1 in 4 |
| People at a new site | 24 plus a roll of 0 to 36 |
| Farm count on a road | 0 if roll under 15, 1 if under 65, else 2 |
| Acreage chance | 25% |
| Acreage area | 1 to 5 acres |
| Farmstead area | 3 to 9 acres |
| Home occupancy | 95% |
| Garage chance | 42% |
| Sea level | -50 feet |
| South drop | 4 feet per mile |

5. Each later phase adds its own keys here. Do not hardcode the new rates in the spawners.

## Phase 3. Elevation

No new land rules. Keep the rolling plain, the carved valleys, the 20-foot contours, and the half-mile routing grid. After phase 0, that grid is stored in feet. The half-mile grid stays the routing grid. A finer mesh is a query detail, not a new elevation pass.

## Phase 4. Water

No new water rules. Keep rivers in the saved beds and lakes in the low basins. Width still follows stream order. Order 3 and above stays the wide channel, order 2 the middle channel, and the rest the narrow channel.

## Phase 5. Population and names

1. Keep east-west main roads, the 12-mile stations, the river-crossing preference, and the 1.5-mile water fallback.
2. Replace the town-name pool. Use 80 first stems and 40 second stems, all generic. Ban Boyden, Hull, and George. Assign the name from the settlement id in this phase, before the plat exists, so a later town rebuild cannot rename it.
3. Family names on lots stay. They are household names, not place names.

## Phase 6. Rail

Water bridges from phase 4 and the rail-river bridge markers stay as they are.

### Rail crossings rule

1. Measure every place the rail polyline crosses a road centerline. The angle from the road must be at least 45 degrees.
2. In any query window, at least 80% of those crossings must sit within 15 degrees of square.
3. The current approach runs beside the main road for 0.9 miles, then bows 0.25 to 1.5 miles off that road. Keep a beside-road approach only while the track does not cross that road. When the bow leaves the road, it must not cross it at under 45 degrees. If a sample would, move the bow or drop that crossing and reroute.
4. Do not remove rail bridges over rivers.

### Rail pass-through rule

1. For each town, roll where the track sits inside the town footprint. 70% industrial, 25% commercial, 5% residential. Store the roll on the town from the settlement id.
2. Industrial puts the elevator, the bin row, and the depot on the track, in the industrial ground, not on a house lot.
3. Commercial puts the track along the highway business row from phase 9.
4. Residential is the rare roll. The track may cross that zone. It must not pass through a house footprint.
5. Stop using "Main Street crosses the depot" as the default rail-town plan. A street may cross the track only when the crossing meets the angle rule.

## Phase 7. Section roads

1. Keep every crossing behavior listed in the decisions. Do not retune widths, the 24-mile survey jog, the county-line test, the rough-ground cut, or the 6-mile bank link.
2. Expose nothing new here except the settings that already match those numbers, if a later pass needs to read them. Do not add a culvert type field.

## Phase 8. Rural yards

Rewrite the yard in [RuralSpawner.cs](C:\Users\hhofe\Documents\grokkable projects\t3 testbed\csharp-spawnerv0.13\RuralSpawner.cs). The lot remains the building cluster and its yard. Fields stay outside the lot.

1. One driveway per lot. It leaves the road at about 90 degrees, runs up the side of the house, and continues to the machine shed and the bin row. If the garage is separate, it is a short branch off that drive. Remove the separate apron-to-building spurs as access. The apron may remain as yard surface.
2. A path leaves the road beside the drive, follows it, and curves off to the front door. On a gravel or dirt road the path is dirt. On a paved county road the path is concrete.
3. Shelterbelt on the north and west of the building cluster, 3 to 7 rows. These are tree markers. Phase 10 draws them, and this phase only reserves the rows.
4. Gambrel barn on about 12% of farmsteads, never on acreages. Name it "Existing gambrel barn" and mark it pre-1990. It replaces a generic barn on that lot. It does not add a second barn.
5. Field entrances. One or two per quarter section, hashed from the quarter's world coordinate. 26 feet wide, 95 to 170 feet long, set back 110 feet from an intersection. Skip a quarter whose section line has no road. Skip a mouth that a farmyard occupies. The town phase deletes any entrance the town footprint covers.
6. Cattle yards. A separate parcel of 20 to 40 acres on a gravel or dirt section road. At most one per four sections. Keep it off rivers and off town footprints. Draw pens, a feed road, and a small office shed. The town phase deletes a cattle yard it covers, the same way it deletes a rural lot.
7. Coop corner. At a section-line corner where the rail passes within 300 feet, and no town sits within 2 miles. Place an elevator and a bin row. If a town already carries the rail elevator, do not add a coop corner for that town.

## Phase 9. Towns

Keep the four plan ids `county-corner`, `rail-crossing`, `rail-cardinal`, and `main-street`. Keep exterior roads and the rule that a street on the edge of town has lots on its outer side. Delete the unused `AddTown` method once `AddTownV2` is the only plat.

### Town variation

1. Keep the four plans as the start.
2. Sit the plat on a side of its road. 46% north, 24% south, 30% astride. Hash it from the settlement id.
3. About 55% of blocks get a 16-foot gravel alley.
4. Lots face both street directions. A house on a north-south street rotates to face that street. A house on an east-west street faces that street.

### Town outline rule

1. Start from the plat rectangle.
2. Where a river, a lake, the rail corridor, or a paved road enters the footprint, cut or bend the outline around it. The saved outline must not be a rectangle on every town.
3. Rebuild the edge streets against the new outline. Outer sides of those streets still receive lots.
4. Do not force one perimeter loop.

### Dead end rule

1. Cap the count by plat size. None on the smallest, one on a small plat, two on a mid plat, three on the largest.
2. Each dead end attaches to the street network at one end. The free end stops at the plat edge or ends in a 44-foot bulb.
3. No orphan segment. A road with neither end on the network is a bug.
4. The court and plat-edge extras below count toward this cap.

### Shops and businesses rule

1. Split the selected businesses 70% downtown on Main Street and 30% on the paved highway or county road. Use count, seeded from the town id.
2. Only these types go on the strip. Gas Station, Implement Dealer, Farm Supply, Lumberyard, Auto Repair, Motel. Add Motel to the catalogue.
3. Every other current business stays downtown. That includes Post Office, Bank, Grocery, Cafe, Tavern, Hardware, Beauty Shop, Insurance, Church, Drugstore, Funeral Home, Nursing Home, Veterinarian, City Hall / Fire Hall, and School.
4. If the town has no paved road within 1.5 miles, the strip types stay downtown.
5. The grain elevator does not use this split. It follows the rail pass-through rule in phase 6.

### Sidewalks and door walks

1. Main Street and the street that serves the school get a sidewalk on both sides.
2. Other residential streets drop one side when the street hash is under 40. A quiet street may drop both sides when the hash is under 15. The school street never drops a side.
3. A sidewalk is 4.5 feet wide and sits 2.25 feet inside the street edge.
4. Each occupied house gets a 4-foot walk from the sidewalk line to the front door. If that side has no sidewalk, the walk starts at the street edge.
5. The driveway crosses the walk to the curb.

### Extra streets

Draw 0 to 2 extras per town. 35% of towns get none. Each extra attaches at both ends, or is an allowed dead end under the cap above. Erase lots that sit in the traveled way. Houses along the extra face that street.

The pool is:

- OLD RAIL, a diagonal across the plat
- CRESCENT, a curve over one corner
- a boulevard with a planted median
- a court, a short dead end with a bulb and houses
- a plat-edge street that runs out and stops
- an outlying road beyond the edge with 3 to 6 farmhouses
- a bypassed highway, the same kind of street, with a few shops left on it

### Civic grounds

Place these on their own ground at the edge, or on one central face for the square. Do not put them on house lots or on the highway shop row. Church, school, and post office stay lot buildings.

| Ground | Rate |
|---|---|
| Water tower | 90% of towns |
| Ball field | population 250 and up, 50% |
| Cemetery | 60% |
| Library | population 750 and up, 70% |
| Park or square | 40% get an edge park. 15% separately get a central square on the most central face |
| Golf course | population 750 and up, 15% |
| Fairgrounds | population 750 and up, 10% |
| Airstrip | population 750 and up, 8% |

### Lots the player can build on

Keep 95% of home lots occupied. The empty lots stay empty on purpose. Near scale must not invent a house for an empty lot.

## Phase 10. Trees

Run one pass after towns.

1. Draw trunk dots only. No canopy fill, no foliage disk.
2. Place the reserved farm shelterbelt, a few groves in field interiors, trees along river banks, and trees in town yards and along streets.
3. Reject a marker that intersects a road surface, a building footprint, a driveway, or the rail corridor.
4. Density is a setting. Default it low enough that the dots do not read as a satellite photo. Street-tree chance starts at 0.15. Grove chance starts at one grove per four sections.

## Phase 11. Three distances of scale

The query `detail` argument selects the payload. The viewer uses the same cutoffs it already uses to hide detail, but the data comes from the query.

1. Far, a window wider than 12 miles. Settlement and town markers, names, major rivers, paved roads, and rail. No lots, no buildings, no drives.
2. Mid, from 1.5 miles through 12 miles. Footprints, streets, rural buildings, drives, field entrances, sidewalks, and civic grounds. No wall segments.
3. Near, under 1.5 miles. For an occupied house, replace the footprint with a wall plan. Bring ranch, farmhouse, and split-level across from SpawnGuide, including openings and the 97-1/8 inch story height. Walls are already fastened. Pick the archetype from the lot hash. Outbuildings stay footprints. An empty lot stays empty.
4. Check one house of each archetype against SpawnGuide seed 1993 before deleting any SpawnGuide source. Leave SpawnGuide in the repo after the copy.
5. A finer elevation mesh, about 10 to 30 feet, is part of Near and Mid. Sample it from the same elevation function as the half-mile routing grid. Do not author a second terrain.

## Phase 12. Seed checks

Add `CsharpSpawner.Tests`. Run them headless on seed 31. Use one 6-mile window and a second window that overlaps it by 2 miles.

1. The same query twice serializes to the same bytes.
2. Overlapping windows agree on every feature whose center lies in both.
3. No building footprint intersects water or another building.
4. Every road end meets another road, or is an allowed dead end from phase 9.
5. Every rail-versus-road crossing is at least 45 degrees, and at least 80% in the window are within 15 degrees of square.
6. Record one town `RouteId` and name from seed 31 and assert them. A plat change must not rename that town.
7. Crossing fixture, so the kept 0.13 behavior cannot regress. On a known major river, the gravel section is absent, the county line has a bridge, a small creek on gravel has a bridge, and a bank link exists within 6 miles.

## Phase 13. Cleanup

Do this last.

1. Confirm nothing calls `AddTown`, then delete it.
2. User clarified that 0.13 must remain untouched. Do not delete its `bin` or `obj`; v0.2 build outputs are ignored by its `.gitignore`.
3. Do not delete the 0.1, 0.11, or 0.12 folders.
4. Point [README.md](C:\Users\hhofe\Documents\grokkable projects\t3 testbed\csharp-spawnerv0.2\README.md) at the library, the query, the settings file, and this phase order.
5. Leave SpawnGuide on disk. The README should say the live house plans now live in the library.

## Phase 10–13 implementation status

- Phase 10: `TreeSpawner` is a standalone worker after towns. It emits trunk-only markers, applies seeded shelterbelt, bank, grove, street, and yard placement, and rejects roads, buildings, drives, rails, rivers, and lakes.
- Phase 11: Far/Mid/Near query payloads are distinct. Mid and Near include 30-foot samples from the carved elevation surface. Near replaces occupied town-house footprints with the copied SpawnGuide wall plans; empty lots remain unbuilt.
- Phase 12: `CsharpSpawner.Tests` runs headless seed-31 determinism, two-mile overlap, building collision, road connectivity (including the user-approved intentional ends at planned section-road cutouts), rail-angle, route/name, SpawnGuide archetype, and water-crossing checks.
- Phase 13: README and ignored build-output checks are updated. `AddTown` is absent; SpawnGuide and versions 0.1 through 0.14 remain on disk and untouched.
