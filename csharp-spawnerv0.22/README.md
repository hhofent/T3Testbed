# C# Spawner 0.22

Standalone Windows WPF viewer. A C# elevation worker computes a seeded,
multi-scale rolling plain, renders it with elevation tint and multidirectional
hillshade, carves broad river valleys, and returns marching-squares contours.
A separate water spawner fills the saved riverbeds and low basins with rivers
and lakes. River reaches use seeded, irregular smooth bends that follow local
terrain swales. Valley width and depth vary by stream and order, and blend into
the surrounding elevation. Occasional seed-based small lake basins are saved
by elevation and filled by the water spawner. Each main reach receives a sparse
set of larger tributaries, with shorter headwaters branching into them.
Smaller streams are progressively thinner and carve narrower valleys;
overview views emphasize the larger channels while closer views reveal
headwaters. The population spawner creates reproducible Dutch-English
settler clusters, their initial east-west main roads, and settlement sites
near suitable land and river crossings. A deterministic subset of those sites
is marked as major towns. Settlement sites have a 12-mile route spacing,
favor smoother ground, and retain stable IDs and a suitability score for later
town generation. The rail worker connects major sites before towns are laid
out. The local-road worker then builds the one-mile section grid, cuts gravel
sections where terrain is rough or a major river blocks the route, and retains
small-creek bridges. Major-river crossings use paved county roads; nearby
gravel road ends follow the bank toward those crossings. Rural lots are placed
along the completed grid before town growth. Planned section-road cutouts at
rough ground and major rivers retain their intentional road ends. Town plans
vary by seed across
county-corner, rail-crossing, rail-cardinal, and Main Street layouts. Plats can
sit north or south of their approach road, trim around nearby corridors, and
include gravel alleys, capped dead ends, and occasional extra streets. Exterior
streets receive lots on their outer side. Selected roadside businesses use a
separate paved-road strip; other businesses stay downtown. Towns include seeded
civic grounds, sidewalks, door walks, and a 95% home-lot occupancy default.
Settings, including town variation rates, save beside the executable in
`settings.json`. Towns avoid steep parcels and water and remove rural lots,
field entrances, and cattle yards covered by the town plan. No Python runtime is
required.

After towns, the tree worker places sparse trunk dots only, clearing roads,
buildings, driveways, rail, and water. At near scale, a separate worker creates
already-fastened ranch, farmhouse, and split-level wall plans from the copied
SpawnGuide archetypes, including openings and the 97-1/8 inch story height.
SpawnGuide remains in `../src/SpawnGuide`; the live house-plan source is copied
into `CsharpSpawner/SpawnGuideHouse`.

The initial view spans 5 miles across. The map fills the available viewer area
and keeps its geographic proportions when the window is resized. Drag to pan
and use the mouse wheel to zoom between 0.1 and 100 miles across. The viewer
requests and caches elevation, water, population, rail, local roads, rural lots,
towns, trees, and near-scale wall plans in 1 x
1 mile chunks as they enter the view. New spatial chunks deterministically
seed westbound settler clusters from their eastern edge. Settlement markers
remain visible at map scale; zoom in to see town names, lots, streets, buildings,
and named people.
The generation console is embedded in the window. Closing the window cancels
the active workers.

Use **Random seed** to choose a new seed and regenerate the currently visible
area. The **Spawners** checkboxes enable or pause elevation, water, population,
rail, local roads, rural lots, towns, trees, and near-scale walls for new chunks. Re-seeding clears the current
chunk cache so you can compare a stage with its checkbox off.

## Run from source

Install the .NET 10 SDK, then run from this directory:

```powershell
dotnet run
dotnet run -- --headless
```

Headless mode runs elevation, water, population, rail, local roads, rural lots,
towns, and trees in generation order in a console; Ctrl+C stops it.

`CsharpSpawner.WorldQuery.Load(seed, settings, originXFeet, originZFeet,
sizeFeet, detail)` returns stable, clipped library records. Far queries return
major features, Mid and Near include a 30-foot terrain mesh, and Near includes
the SpawnGuide wall plans. Run the headless regression suite with:

```powershell
dotnet test CsharpSpawner.Tests/CsharpSpawner.Tests.csproj -c Release
```

The stage order is elevation, water, population, rail, section roads, rural lots,
towns, trees, then near-scale wall plans.

## Publish a self-contained executable

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

The resulting single-file app includes the .NET runtime. It targets Windows x64
because WPF is Windows-only.

