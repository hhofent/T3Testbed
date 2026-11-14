# BuilderGuy settings map (T5 incomplete)

This increment copies values only. No BuilderGuy generation is wired or probed.
Feet/inches are converted from Python tick constants without changing physical defaults.
`user` denotes the approved house rules; `placeholder` denotes unre?searched parameters.
Ranges are editing bounds, not new generation tolerances. Python choice distributions remain pending.

| Python name | C# path | Default | Source |
|---|---|---|---|
| N9 | `BuilderGuy.SnapshotYear` | 1990 | user |
| N12 | `BuilderGuy.Commercial.DowntownShare` | 0.7 | user |
| spawner.py T | `BuilderGuy.Houses.TicksPerInch` | 16 | user |
| spawner.py CLR | `BuilderGuy.Houses.DoorCornerClearanceInches` | 4.5 | user |
| spawner.py HEAD | `BuilderGuy.Houses.OpeningHeadInches` | 83 | user |
| spawner.py GRID | `BuilderGuy.Houses.GridFeet` | 2 | user |
| spawner.py MODULE | `BuilderGuy.Houses.ModuleFeet` | 4 | user |
| spawner.py GW[double] | `BuilderGuy.Houses.DoubleGarageWidthFeet` | 22 | user |
| spawner.py GW[single] | `BuilderGuy.Houses.SingleGarageWidthFeet` | 12 | user |
| spawner.py SIDE_PCT | `BuilderGuy.Setbacks.SidePercent` | 10 | placeholder |
| spawner.py SIDE_MIN_PCT | `BuilderGuy.Setbacks.SideMinimumPercent` | 5 | placeholder |
| spawner.py FRONT_PCT | `BuilderGuy.Setbacks.FrontPercent` | 20 | placeholder |
| spawner.py FRONT_MIN_PCT | `BuilderGuy.Setbacks.FrontMinimumPercent` | 15 | placeholder |
| spawner.py REAR_PCT | `BuilderGuy.Setbacks.RearPercent` | 25 | placeholder |
| spawner.py REAR_MIN_PCT | `BuilderGuy.Setbacks.RearMinimumPercent` | 20 | placeholder |
| spawner.py CORNER_SIDE_X | `BuilderGuy.Setbacks.CornerSideMultiplier` | 2 | placeholder |
| spawner.py PORCH_PROJECT_MAX_W | `BuilderGuy.Setbacks.PorchProjectionMaximumLotWidthFeet` | 70 | placeholder |
| spawner.py PORCH_PROJECT | `BuilderGuy.Setbacks.PorchProjectionFeet` | 6 | placeholder |
| region.py `N_MI` | `BuilderGuy.Region.RegionMiles` | `3` | placeholder |
| region.py `ROW` | `BuilderGuy.Region.RoadRightOfWayFeet` | `66f` | CRG |
| region.py `TOWN_PCT` | `BuilderGuy.Region.TownChancePercent` | `35f` | placeholder |
| region.py `ACREAGE_PCT` | `BuilderGuy.Region.AcreageChancePercent` | `25f` | placeholder |
| region.py `ACRE_BACK` | `BuilderGuy.Region.AcreageSetbackFeet` | `60f` | CRG |
| region.py `GROVE_W` | `BuilderGuy.Region.ShelterbeltWidthFeet` | `80f` | placeholder |
| region.py `BASE_LOT` | `BuilderGuy.Region.BaseLotWidthFeet` | `50f` | CRG |
| region.py `TRAILER_RUN_PCT` | `BuilderGuy.Region.TrailerClusterChancePercent` | `20f` | placeholder |
| region.py `RAIL_PCT` | `BuilderGuy.Region.RailChancePercent` | `60f` | placeholder |
| region.py `RAIL_GAP` | `BuilderGuy.Region.RailRoadGapFeet` | `40f` | placeholder |
| region.py `RAILTOWN_PCT` | `BuilderGuy.Region.RailTownChancePercent` | `50f` | placeholder |
| region.py `NORAIL_TOWN_PCT` | `BuilderGuy.Region.NoRailTownChancePercent` | `15f` | placeholder |
| region.py `RAGGED_PCT` | `BuilderGuy.Region.RaggedEdgePercent` | `35f` | placeholder |
| region.py `RIBBON_DECAY` | `BuilderGuy.Region.RibbonDecayPercent` | `20f` | placeholder |
| region.py `EDGE_HALF_PCT` | `BuilderGuy.Region.EdgeHalfOmissionPercent` | `40f` | placeholder |
| region.py `FRINGE_PCT` | `BuilderGuy.Region.DeadEndStubPercent` | `30f` | placeholder |
| region.py `MAIN_FACES` | `BuilderGuy.Region.MainStreetFaces` | `6` | CRG |
| region.py `RAIL_ROW` | `BuilderGuy.Region.RailCorridorWidthFeet` | `100f` | placeholder |
| region.py `PEOPLE_PER_HOUSE` | `BuilderGuy.Region.PeoplePerHouse` | `2.4f` | placeholder |
| region.py `BASE_YEAR` | `BuilderGuy.Region.BusinessBaseYear` | `1998` | CRG |
| region.py `FADE_1985` | `BuilderGuy.Region.RetailFade1985` | `1.3f` | placeholder |
| region.py `VACANT_PCT` | `BuilderGuy.Region.VacantStorefrontPercent` | `15f` | placeholder |
| region.py `PAVED_PCT` | `BuilderGuy.Region.PavedSharePercent` | `new(22, 25)` | CRG |
| region.py `CLEAR` | `BuilderGuy.Region.IntersectionClearanceFeet` | `new(150, 300)` | CRG |
| region.py `FARM_AC` | `BuilderGuy.Region.FarmParcelAcres` | `new(3, 9)` | CRG |
| region.py `FARM_BACK` | `BuilderGuy.Region.FarmHouseSetbackFeet` | `new(100, 300)` | CRG |
| region.py `ACRE_AC` | `BuilderGuy.Region.AcreageParcelAcres` | `new(1, 5)` | CRG |
| region.py `TRAILER_RUN` | `BuilderGuy.Region.TrailerClusterCount` | `new(2, 4)` | placeholder |
| region.py `RAIL_ANGLE` | `BuilderGuy.Region.RailAngleDegrees` | `new(62, 80)` | placeholder |
| region.py `ELEV_AC` | `BuilderGuy.Region.ElevatorParcelAcres` | `new(3, 6)` | CRG |
| region.py `CHURCH_AC` | `BuilderGuy.Region.ChurchParcelAcres` | `new(1, 3)` | CRG |
| region.py `CREEK_W` | `BuilderGuy.Region.CreekWidthFeet` | `new(20, 60)` | placeholder |
| region.py `CREEK_BUF` | `BuilderGuy.Region.CreekFloodplainBufferFeet` | `new(150, 300)` | placeholder |
| region.py `BRANCH_PCT` | `BuilderGuy.Region.BankBranchPercent` | `new(10, 50)` | placeholder |

## Still required before T5 completion

- Region choice lists, weighted lists, tiny/mid/big grids, business type/rate/footprint tables, setback ranges, dimensions and calendar choices.
- House Census tables, categorical choice lists, trailer constants (in-block only), inline room/opening/site constants and placeholders.
- Validator numeric requirements and shared settings; preserve all existing thresholds.
- Exhaustive source-to-setting inventory, with exclusions for park code and structural literals justified individually.
- Settings range-pair validation, migration through file/cache paths, default comparisons against frozen Python.

No park setting, stub, switch or check is added. No tier 1 completion or tier 2 parity claim is made.
