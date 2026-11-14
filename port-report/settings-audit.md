# Settings wiring audit — empirical inventory

Reproduce from repository root: `dotnet src/SpawnGuide/bin/Release/net5.0/SpawnGuide.dll --audit-settings --audit-output port-report/settings-audit.md`.

Inventory: 528 settings; changed 94; unchanged 411; rejected probes 1; structural/unprobed 22.
This is an incomplete T4 audit, not a claim that T4 passes. Probe seed: 1993. Views: three house archetypes, rural lot, town block, four smalltown sizes, countryside and a world window. No conditional rules/repair are applied to mutations. Before-state wiring is unverified. A no-change result requires source review and broader seeds; rare features may be absent. Collections require dedicated structural mutations. Source locations below identify declarations, not generation reads. Rendering settings consumed by the separate viewer may not affect these SVG probes.

| Setting | Wired before? | Observed output effect now? | Declaration | Probe / limitation |
|---|---|---|---|---|
| Houses.RanchWidth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:208 | Mutation: {    "shape": "LogNormal",    "min": 30,    "max": 68.75,    "mean": 3.7,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.RanchDepth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:212 | Mutation: {    "shape": "LogNormal",    "min": 20,    "max": 43.75,    "mean": 3.2,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.RanchGarageWidth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:216 | Mutation: {    "shape": "Normal",    "min": 16,    "max": 37.5,    "mean": 22,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.RanchGarageDepth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:220 | Mutation: {    "shape": "Normal",    "min": 18,    "max": 32.5,    "mean": 22,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.RanchGarageLeftChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:224 | Mutation: 0.625 |
| Houses.FarmhouseWidth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:229 | Mutation: {    "shape": "LogNormal",    "min": 24,    "max": 50,    "mean": 3.4,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.FarmhouseDepth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:233 | Mutation: {    "shape": "LogNormal",    "min": 26,    "max": 55,    "mean": 3.5,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.FarmhousePorchWidthFraction | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:237 | Mutation: {    "shape": "Beta",    "min": 0.5,    "max": 1.8,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 5,    "beta": 3,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.FarmhousePorchDepth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:241 | Mutation: {    "shape": "Normal",    "min": 5,    "max": 12.5,    "mean": 7,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.FarmhouseDoorCenteredChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:245 | Mutation: 1.125 |
| Houses.SplitLevelWidth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:250 | Mutation: {    "shape": "LogNormal",    "min": 24,    "max": 50,    "mean": 3.4,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.SplitLevelDepth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:254 | Mutation: {    "shape": "LogNormal",    "min": 24,    "max": 50,    "mean": 3.4,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.SplitLevelGarageWidth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:258 | Mutation: {    "shape": "Normal",    "min": 12,    "max": 27.5,    "mean": 16,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.SplitLevelGarageDepth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:262 | Mutation: {    "shape": "Normal",    "min": 16,    "max": 32.5,    "mean": 20,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.SplitLevelGarageLeftChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:265 | Mutation: 0.625 |
| Houses.SplitLevelGarageDoorOnSideChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:269 | Mutation: 1.125 |
| Houses.SidingOptions | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:274 | Structural/collection/metadata setting: needs dedicated test |
| Houses.DoorOffsetFraction | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:283 | Mutation: {    "shape": "Beta",    "min": 0.15,    "max": 1.35,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 2,    "beta": 5,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.FrontWindowJitterFraction | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:287 | Mutation: {    "shape": "Beta",    "min": 0,    "max": 1.08,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 2,    "beta": 5,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.SideWindowJitterFraction | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:291 | Mutation: {    "shape": "Beta",    "min": 0,    "max": 1.06,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 2,    "beta": 5,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.GarageDoorWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:295 | Mutation: {    "shape": "Bimodal",    "min": 0,    "max": 2,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.65,    "value1": 9,    "value2": 16  } |
| Houses.TwoWindowMinWall | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:300 | Mutation: {    "shape": "Normal",    "min": 18,    "max": 35,    "mean": 22,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.OneWindowMinWall | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:305 | Mutation: {    "shape": "Normal",    "min": 7,    "max": 15,    "mean": 9,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Houses.GarageModeIntegralChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:308 | Mutation: 0.6875 |
| Houses.GarageModeDetachedChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:309 | Mutation: 0.4375 |
| Houses.GarageModeNoneChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:310 | Mutation: 0.125 |
| Rural.LotWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:335 | Mutation: {    "shape": "LogNormal",    "min": 180,    "max": 437.5,    "mean": 5.5,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.LotDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:339 | Mutation: {    "shape": "LogNormal",    "min": 250,    "max": 625,    "mean": 5.8,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.HouseXFraction | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:343 | Mutation: {    "shape": "Uniform",    "min": 0.22,    "max": 1.38,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.HouseSetback | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:347 | Mutation: {    "shape": "Normal",    "min": 30,    "max": 100,    "mean": 50,    "stdDev": 10,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.MachineShedWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:352 | Mutation: {    "shape": "LogNormal",    "min": 30,    "max": 75,    "mean": 3.7,    "stdDev": 0.15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.MachineShedDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:356 | Mutation: {    "shape": "LogNormal",    "min": 24,    "max": 62.5,    "mean": 3.5,    "stdDev": 0.15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.MachineShedXFractionRight | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:358 | Mutation: {    "shape": "Uniform",    "min": 0.06,    "max": 1.1800001,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.MachineShedXFractionLeft | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:359 | Mutation: {    "shape": "Uniform",    "min": 0.08,    "max": 1.2,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.MachineShedBehindHouse | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:360 | Mutation: 32.5 |
| Rural.DrivewayEntrySideMargin | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:363 | Mutation: 18.75 |
| Rural.DrivewayYardDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:364 | Mutation: 25 |
| Rural.DrivewayWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:365 | Mutation: {    "shape": "Normal",    "min": 10,    "max": 25,    "mean": 14,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.DrivewaySurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:366 | Mutation: Concrete |
| Rural.WalkWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:369 | Mutation: {    "shape": "Normal",    "min": 2.5,    "max": 6.25,    "mean": 3.5,    "stdDev": 0.5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.WalkSurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:370 | Mutation: Gravel |
| Rural.WalkOffsetFromLane | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:371 | Mutation: 8.75 |
| Rural.ShopChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:375 | Mutation: 0.5 |
| Rural.ShopWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:376 | Mutation: {    "shape": "Normal",    "min": 16,    "max": 37.5,    "mean": 22,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.ShopDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:377 | Mutation: {    "shape": "Normal",    "min": 20,    "max": 50,    "mean": 28,    "stdDev": 4,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.ShopOffsetFromShed | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:378 | Mutation: 12.5 |
| Rural.BinCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:381 | Mutation: {    "shape": "Poisson",    "min": 2,    "max": 3.4028235E+38,    "mean": 2,    "stdDev": 1,    "mode": 2,    "lambda": 3,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 2,    "value2": 3  } |
| Rural.BinDiameter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:382 | Mutation: {    "shape": "LogNormal",    "min": 15,    "max": 37.5,    "mean": 3,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.BinSpacingExtra | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:383 | Mutation: 10 |
| Rural.BinRowBehindShed | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:384 | Mutation: 10 |
| Rural.PoleBarnChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:387 | Mutation: 0.5 |
| Rural.PoleBarnWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:388 | Mutation: {    "shape": "Normal",    "min": 20,    "max": 55,    "mean": 30,    "stdDev": 4,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.PoleBarnDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:389 | Mutation: {    "shape": "Normal",    "min": 32,    "max": 90,    "mean": 48,    "stdDev": 6,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.PoleBarnBehindBins | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:390 | Mutation: 15 |
| Rural.PoleBarnXJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:391 | Mutation: {    "shape": "Normal",    "min": -60,    "max": 75,    "mean": 0,    "stdDev": 30,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.GambrelBarnChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:394 | Mutation: 0.1875 |
| Rural.GambrelBarnWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:395 | Mutation: {    "shape": "Normal",    "min": 26,    "max": 55,    "mean": 34,    "stdDev": 4,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.GambrelBarnDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:396 | Mutation: {    "shape": "Normal",    "min": 40,    "max": 95,    "mean": 56,    "stdDev": 6,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.GambrelBarnXFraction | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:397 | Mutation: {    "shape": "Uniform",    "min": 0.6,    "max": 1.8,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.GambrelBarnFromBack | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:398 | Mutation: 17.5 |
| Rural.LpTankChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:401 | Mutation: 0.375 |
| Rural.LpTankOffsetFromHouse | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:402 | Mutation: 10 |
| Rural.ShelterbeltChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:405 | Mutation: 0.75 |
| Rural.ShelterbeltTreeSpacing | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:406 | Mutation: {    "shape": "Normal",    "min": 12,    "max": 27.5,    "mean": 16,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.ShelterbeltTreeJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:407 | Mutation: 2.5 |
| Rural.ShelterbeltNorthOffset | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:408 | Mutation: 10 |
| Rural.ShelterbeltWestOffset | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:409 | Mutation: 12.5 |
| Rural.ShelterbeltTreeSpacingZ | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:410 | Mutation: 21.25 |
| Rural.ShelterbeltRows | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:411 | Mutation: {    "shape": "Poisson",    "min": 2,    "max": 3.4028235E+38,    "mean": 2,    "stdDev": 1,    "mode": 2,    "lambda": 3,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 2,    "value2": 3  } |
| Rural.DetachedGarageWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:414 | Mutation: {    "shape": "Normal",    "min": 18,    "max": 40,    "mean": 24,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.DetachedGarageDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:415 | Mutation: {    "shape": "Normal",    "min": 18,    "max": 40,    "mean": 24,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Rural.DetachedGarageSideOffset | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:416 | Mutation: 20 |
| Rural.DetachedGarageBehindHouse | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:417 | Mutation: 5 |
| Rural.OutbuildingDensity | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:420 | Mutation: 1.25 |
| Rural.BuildingCollisionPad | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:423 | Mutation: 3.75 |
| TownBlock.StreetRow | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:435 | Mutation: 45 |
| TownBlock.AlleyRow | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:436 | Mutation: 20 |
| TownBlock.LotDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:439 | Mutation: {    "shape": "LogNormal",    "min": 100,    "max": 200,    "mean": 4.8,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownBlock.AlleyChance | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:443 | Mutation: {    "shape": "Beta",    "min": 0,    "max": 2,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 2,    "beta": 2,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownBlock.LotCountPerSide | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:445 | Mutation: 5 |
| TownBlock.LotFrontage | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:449 | Mutation: {    "shape": "LogNormal",    "min": 50,    "max": 125,    "mean": 4.2,    "stdDev": 0.15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownBlock.Setback | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:453 | Mutation: {    "shape": "Normal",    "min": 8,    "max": 31.25,    "mean": 15,    "stdDev": 5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownBlock.HouseXJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:455 | Mutation: 3.75 |
| TownBlock.GarageViaAlleyChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:458 | Mutation: 0.75 |
| TownBlock.DetachedGarageWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:459 | Mutation: {    "shape": "Normal",    "min": 16,    "max": 37.5,    "mean": 22,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownBlock.DetachedGarageDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:460 | Mutation: {    "shape": "Normal",    "min": 20,    "max": 42.5,    "mean": 26,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownBlock.DetachedGarageXJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:461 | Mutation: 7.5 |
| TownBlock.DetachedGarageFromAlley | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:462 | Mutation: 11.25 |
| TownBlock.FarmhouseDetachedGarageWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:465 | Mutation: {    "shape": "Normal",    "min": 16,    "max": 32.5,    "mean": 20,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownBlock.FarmhouseDetachedGarageDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:466 | Mutation: {    "shape": "Normal",    "min": 18,    "max": 35,    "mean": 22,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownBlock.FarmhouseDetachedGarageSideMargin | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:467 | Mutation: 3.75 |
| TownBlock.FarmhouseDetachedGarageDepthFraction | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:468 | Mutation: 0.3125 |
| TownBlock.DrivewayWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:471 | Mutation: {    "shape": "Normal",    "min": 12,    "max": 30,    "mean": 16,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownBlock.DrivewaySurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:472 | Mutation: Gravel |
| TownBlock.DrivewayStartOffset | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:473 | Mutation: 38.75 |
| TownBlock.AlleyDrivewayWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:476 | Mutation: 12.5 |
| TownBlock.AlleyDrivewaySurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:477 | Mutation: Concrete |
| TownBlock.WalkWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:480 | Mutation: {    "shape": "Normal",    "min": 3,    "max": 6.25,    "mean": 4,    "stdDev": 0.5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownBlock.WalkSurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:481 | Mutation: Gravel |
| TownBlock.WalkStartInset | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:482 | Mutation: 5.625 |
| TownBlock.StreetTreeSpacing | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:485 | Mutation: {    "shape": "Normal",    "min": 25,    "max": 75,    "mean": 40,    "stdDev": 8,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownBlock.StreetTreeChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:486 | Mutation: 0.9375 |
| TownBlock.StreetTreeSidewalkOffset | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:487 | Mutation: 8.75 |
| TownBlock.YardTreeCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:490 | Mutation: {    "shape": "Poisson",    "min": 0,    "max": 3.4028235E+38,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 2.5,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownBlock.YardTreePad | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:491 | Mutation: 6.25 |
| TownBlock.YardTreeSideMargin | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:492 | Mutation: 5 |
| TownBlock.HouseDensity | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:495 | Mutation: 1.25 |
| TownBlock.StreetNamePool | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:498 | Structural/collection/metadata setting: needs dedicated test |
| TownBlock.StreetSuffixStreetChance | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:500 | Mutation: 0.875 |
| SmallTown.HighwayWidth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:513 | Mutation: 82.5 |
| SmallTown.StreetRow | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:514 | Mutation: 45 |
| SmallTown.AlleyRow | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:515 | Mutation: 20 |
| SmallTown.BlockLotDepth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:516 | Mutation: 152.5 |
| SmallTown.SideLotDepth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:517 | Mutation: 100 |
| SmallTown.BandDepth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:518 | Mutation: 218.75 |
| SmallTown.FourSideBlockChance | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:519 | Mutation: 0.875 |
| SmallTown.LotDepthJitter | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:520 | Mutation: 7.5 |
| SmallTown.SizePresets | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:523 | Structural/collection/metadata setting: needs dedicated test |
| SmallTown.ExtraCourtChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:532 | Mutation: 0.625 |
| SmallTown.ExtraPlatEdgeChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:533 | Mutation: 0.5625 |
| SmallTown.ExtraOutlyingRoadChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:534 | Mutation: 0.5625 |
| SmallTown.ExtraBypassedHighwayChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:535 | Mutation: 0.4375 |
| SmallTown.ExtraIndustrialSideStreetChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:536 | Mutation: 0.5 |
| SmallTown.ExtraTownSquareChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:537 | Mutation: 0.4375 |
| SmallTown.ExtraBoulevardChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:538 | Mutation: 0.19999999 |
| SmallTown.ExtraNoneChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:539 | Mutation: 0.4375 |
| SmallTown.ExtraOneChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:540 | Mutation: 0.75 |
| SmallTown.MaxExtrasPerTown | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:541 | Mutation: 3 |
| SmallTown.RowAlleyChance | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:544 | Mutation: {    "shape": "Beta",    "min": 0,    "max": 2,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 2,    "beta": 2,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.PlaceNames | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:547 | Structural/collection/metadata setting: needs dedicated test |
| SmallTown.EWStreetNamePool | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:560 | Structural/collection/metadata setting: needs dedicated test |
| SmallTown.NSStreetNamePool | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:562 | Structural/collection/metadata setting: needs dedicated test |
| SmallTown.LotFrontage | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:567 | Mutation: {    "shape": "LogNormal",    "min": 50,    "max": 125,    "mean": 4.2,    "stdDev": 0.15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.BlobHarmonic1 | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:570 | Structural/collection/metadata setting: needs dedicated test |
| SmallTown.BlobHarmonic2 | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:571 | Structural/collection/metadata setting: needs dedicated test |
| SmallTown.BlobAmplitude1 | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:572 | Structural/collection/metadata setting: needs dedicated test |
| SmallTown.BlobAmplitude2 | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:573 | Structural/collection/metadata setting: needs dedicated test |
| SmallTown.BlobPhase | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:574 | Structural/collection/metadata setting: needs dedicated test |
| SmallTown.BlobRadiusMultiplier | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:575 | Mutation: 1.3 |
| SmallTown.FreeStreetBlobPadding | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:576 | Mutation: 0.39999998 |
| SmallTown.VacantOuterChance | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:579 | Mutation: 0.175 |
| SmallTown.VacantMidChance | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:580 | Mutation: 0.0625 |
| SmallTown.VacantCoreChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:581 | Mutation: 0.018749999 |
| SmallTown.VacantOuterRadius | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:582 | Mutation: 0.875 |
| SmallTown.VacantMidRadius | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:583 | Mutation: 0.625 |
| SmallTown.RadialPlatVacantChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:584 | Mutation: 0.275 |
| SmallTown.CommercialStartX | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:587 | Mutation: {    "shape": "Uniform",    "min": 40,    "max": 325,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.CommercialGap | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:588 | Mutation: {    "shape": "Uniform",    "min": 45,    "max": 175,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.CommercialMainGapMargin | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:589 | Mutation: 15 |
| SmallTown.CommercialBandOccMargin | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:590 | Mutation: 6.25 |
| SmallTown.IndustrialBoxCountChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:591 | Mutation: 0.6875 |
| SmallTown.CrossStreetShopMax | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:592 | Mutation: 4 |
| SmallTown.CrossStreetShopDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:593 | Mutation: {    "shape": "Uniform",    "min": 24,    "max": 125,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.CrossStreetShopWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:594 | Mutation: {    "shape": "Uniform",    "min": 24,    "max": 125,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.ElevatorWidth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:597 | Mutation: 60 |
| SmallTown.ElevatorDepth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:598 | Mutation: 110 |
| SmallTown.ElevatorRightChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:599 | Mutation: 0.775 |
| SmallTown.ElevatorOffsetFromMain | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:600 | Mutation: {    "shape": "Uniform",    "min": 45,    "max": 187.5,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.ElevatorBinCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:601 | Mutation: {    "shape": "Poisson",    "min": 2,    "max": 3.4028235E+38,    "mean": 2,    "stdDev": 1,    "mode": 2,    "lambda": 3,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 2,    "value2": 3  } |
| SmallTown.BinDiameter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:602 | Mutation: {    "shape": "LogNormal",    "min": 18,    "max": 37.5,    "mean": 3.1,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.BinSpacing | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:603 | Mutation: 37.5 |
| SmallTown.BinRowBehindElevator | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:604 | Mutation: 12.5 |
| SmallTown.RailIndustrialRowSpacing | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:607 | Mutation: {    "shape": "Uniform",    "min": 56,    "max": 105,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.RailIndustrialDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:608 | Mutation: {    "shape": "LogNormal",    "min": 60,    "max": 250,    "mean": 4.5,    "stdDev": 0.5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.RailIndustrialZJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:609 | Mutation: {    "shape": "Normal",    "min": -50,    "max": 62.5,    "mean": 0,    "stdDev": 16,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.SouthBandDepth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:612 | Mutation: {    "shape": "Uniform",    "min": 150,    "max": 262.5,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.SouthHouseCount | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:613 | Mutation: {    "shape": "Uniform",    "min": 1,    "max": 5,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.SouthHouseWidth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:614 | Mutation: {    "shape": "Uniform",    "min": 150,    "max": 275,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.SouthHouseSpacing | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:615 | Mutation: {    "shape": "Uniform",    "min": 24,    "max": 75,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.SouthIndustrialChance | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:616 | Mutation: 0.75 |
| SmallTown.ChurchCountTier0 | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:619 | Mutation: {    "shape": "Uniform",    "min": 0,    "max": 3,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.ChurchCountTier1 | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:620 | Mutation: {    "shape": "Uniform",    "min": 1,    "max": 4,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.ChurchCountTier2Plus | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:621 | Mutation: {    "shape": "Uniform",    "min": 1,    "max": 4,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.SchoolChanceTier0 | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:622 | Mutation: 0.375 |
| SmallTown.SchoolChanceTier1 | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:623 | Mutation: 0.75 |
| SmallTown.SchoolChanceTier2Plus | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:624 | Mutation: 1.1875 |
| SmallTown.ParkChanceTier0 | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:625 | Mutation: 0.4375 |
| SmallTown.ParkChanceTier1 | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:626 | Mutation: 0.75 |
| SmallTown.ParkChanceTier2Plus | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:627 | Mutation: 1.0625 |
| SmallTown.LotSetback | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:631 | Mutation: {    "shape": "Normal",    "min": 6,    "max": 27.5,    "mean": 12,    "stdDev": 4,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.GarageModeIntegralChance | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:635 | Mutation: 0.6875 |
| SmallTown.GarageModeDetachedChance | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:636 | Mutation: 0.4375 |
| SmallTown.GarageModeNoneChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:637 | Mutation: 0.125 |
| SmallTown.HouseFrontageNarrowThreshold | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:639 | Mutation: 57.5 |
| SmallTown.HousePlacementJitter | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:640 | Mutation: 2.5 |
| SmallTown.HouseFitPad | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:641 | Mutation: 10 |
| SmallTown.HouseSetbackMin | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:642 | Mutation: 5 |
| SmallTown.ShedChance | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:643 | Mutation: 0.14999999 |
| SmallTown.ShedWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:644 | Mutation: {    "shape": "Normal",    "min": 8,    "max": 17.5,    "mean": 10,    "stdDev": 1.5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.ShedDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:645 | Mutation: {    "shape": "Normal",    "min": 8,    "max": 22.5,    "mean": 12,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.ShedSideMargin | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:646 | Mutation: 5 |
| SmallTown.ShedBackMargin | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:647 | Mutation: 6.25 |
| SmallTown.DetachedGarageViaAlleyChance | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:648 | Mutation: 0.75 |
| SmallTown.DetachedGarageWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:649 | Mutation: {    "shape": "Normal",    "min": 16,    "max": 37.5,    "mean": 22,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.DetachedGarageDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:650 | Mutation: {    "shape": "Normal",    "min": 20,    "max": 42.5,    "mean": 26,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.DetachedGarageXJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:651 | Mutation: 7.5 |
| SmallTown.DetachedGarageFromAlley | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:652 | Mutation: 11.25 |
| SmallTown.FarmhouseDetachedGarageWidth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:653 | Mutation: {    "shape": "Normal",    "min": 16,    "max": 32.5,    "mean": 20,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.FarmhouseDetachedGarageDepth | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:654 | Mutation: {    "shape": "Normal",    "min": 18,    "max": 35,    "mean": 22,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.FarmhouseDetachedGarageSideMargin | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:655 | Mutation: 3.75 |
| SmallTown.FarmhouseDetachedGarageZRange | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:656 | Mutation: {    "shape": "Uniform",    "min": 22,    "max": 125,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.DrivewayWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:659 | Mutation: {    "shape": "Normal",    "min": 12,    "max": 30,    "mean": 16,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.DrivewaySurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:660 | Mutation: Gravel |
| SmallTown.DrivewayStartOffset | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:661 | Mutation: 38.75 |
| SmallTown.AlleyDrivewayWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:662 | Mutation: 12.5 |
| SmallTown.AlleyDrivewaySurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:663 | Mutation: Concrete |
| SmallTown.WalkWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:664 | Mutation: {    "shape": "Normal",    "min": 3,    "max": 6.25,    "mean": 4,    "stdDev": 0.5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.WalkSurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:665 | Mutation: Gravel |
| SmallTown.WalkSidewalkInset | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:666 | Mutation: 2.8125 |
| SmallTown.RailCorridorStreetChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:669 | Mutation: 0.375 |
| SmallTown.CurvedStreetChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:670 | Mutation: 0.375 |
| SmallTown.RailCorridorEraseHalf | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:671 | Mutation: 25 |
| SmallTown.CurvedStreetEraseHalf | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:672 | Mutation: 25 |
| SmallTown.PlatLotFrontage | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:675 | Mutation: {    "shape": "LogNormal",    "min": 55,    "max": 118.75,    "mean": 4.3,    "stdDev": 0.15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.PlatVacantChance | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:676 | Mutation: 0.125 |
| SmallTown.PlatSetback | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:677 | Mutation: {    "shape": "Normal",    "min": 7,    "max": 22.5,    "mean": 12,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.PlatHouseJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:678 | Mutation: 2.5 |
| SmallTown.PlatGarageIntegralChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:679 | Mutation: 0.6875 |
| SmallTown.PlatGarageDetachedChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:680 | Mutation: 0.4375 |
| SmallTown.PlatGarageNoneChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:681 | Mutation: 0.125 |
| SmallTown.PlatDetachedGarageWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:682 | Mutation: {    "shape": "Normal",    "min": 16,    "max": 37.5,    "mean": 22,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.PlatDetachedGarageDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:683 | Mutation: {    "shape": "Normal",    "min": 20,    "max": 42.5,    "mean": 26,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.PlatDetachedGarageXJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:684 | Mutation: 7.5 |
| SmallTown.PlatDetachedGarageFromAlley | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:685 | Mutation: 11.25 |
| SmallTown.CourtXRange | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:688 | Mutation: {    "shape": "Uniform",    "min": 120,    "max": 1000,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.CourtLen | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:689 | Mutation: {    "shape": "Uniform",    "min": 240,    "max": 425,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.CourtHouseCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:690 | Mutation: {    "shape": "Uniform",    "min": 2,    "max": 5,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.CourtHouseFrontage | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:691 | Mutation: {    "shape": "LogNormal",    "min": 50,    "max": 93.75,    "mean": 4.1,    "stdDev": 0.15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.CourtHouseSetback | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:692 | Mutation: {    "shape": "Normal",    "min": 7,    "max": 22.5,    "mean": 12,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.CourtHouseJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:693 | Mutation: 2.5 |
| SmallTown.PlatEdgeXRange | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:696 | Mutation: {    "shape": "Uniform",    "min": 150,    "max": 1000,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.PlatEdgeLen | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:697 | Mutation: {    "shape": "Uniform",    "min": 260,    "max": 525,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.PlatEdgeLotFrontage | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:698 | Mutation: {    "shape": "LogNormal",    "min": 50,    "max": 106.25,    "mean": 4.2,    "stdDev": 0.15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.OutlyingRoadZOffset | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:701 | Mutation: {    "shape": "Uniform",    "min": 200,    "max": 375,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.OutlyingRoadHouseCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:702 | Mutation: {    "shape": "Uniform",    "min": 3,    "max": 7.5,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.OutlyingRoadHouseFrontage | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:703 | Mutation: {    "shape": "LogNormal",    "min": 55,    "max": 112.5,    "mean": 4.2,    "stdDev": 0.15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.OutlyingRoadHouseDepth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:704 | Mutation: {    "shape": "LogNormal",    "min": 40,    "max": 75,    "mean": 3.9,    "stdDev": 0.15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.OutlyingRoadHouseSpacing | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:705 | Mutation: {    "shape": "Uniform",    "min": 300,    "max": 537.5,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.BypassedHighwayXRange | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:708 | Mutation: {    "shape": "Uniform",    "min": 90,    "max": 1000,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.BypassedHighwayShopCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:709 | Mutation: {    "shape": "Uniform",    "min": 3,    "max": 7.5,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.BypassedHighwayShopFrontage | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:710 | Mutation: {    "shape": "LogNormal",    "min": 55,    "max": 112.5,    "mean": 4.2,    "stdDev": 0.15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.BoulevardMedianWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:713 | Mutation: {    "shape": "Normal",    "min": 10,    "max": 37.5,    "mean": 16,    "stdDev": 4,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.PerimeterGapN | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:716 | Mutation: {    "shape": "Normal",    "min": 20,    "max": 125,    "mean": 50,    "stdDev": 15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.PerimeterJogHeight | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:717 | Mutation: {    "shape": "Normal",    "min": 30,    "max": 187.5,    "mean": 80,    "stdDev": 20,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.DeadEndStubXJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:718 | Mutation: 50 |
| SmallTown.DeadEndStubLen | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:719 | Mutation: {    "shape": "Uniform",    "min": 210,    "max": 375,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| SmallTown.DeadEndBulbRadius | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:720 | Mutation: 55 |
| SmallTown.ResidentialDensity | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:723 | Mutation: 1.25 |
| SmallTown.CommercialDensity | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:724 | Mutation: 1.25 |
| SmallTown.IndustrialDensity | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:725 | Mutation: 1.25 |
| SmallTown.CivicDensity | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:726 | Mutation: 1.25 |
| SmallTown.LandmarkDensity | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:727 | Mutation: 1.25 |
| SmallTown.TownExtras | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:729 | Structural/collection/metadata setting: needs dedicated test |
| World.MileFeet | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:755 | Mutation: 6600 |
| World.SectionFeet | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:756 | Mutation: 6600 |
| World.CellFeet | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:757 | Mutation: 19800 |
| World.MinSpan | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:758 | Mutation: 187.5 |
| World.TownDensity | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:761 | Mutation: 1.25 |
| World.FarmDensity | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:762 | Mutation: 1.25 |
| World.HighwayNames | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:765 | Structural/collection/metadata setting: needs dedicated test |
| World.FieldEntrancePerQuarterChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:769 | Mutation: 1.25 |
| World.FieldEntranceMaxPerQuarter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:770 | Mutation: 3 |
| World.FieldEntranceWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:771 | Mutation: 32.5 |
| World.FieldEntranceLength | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:772 | Mutation: {    "shape": "Uniform",    "min": 95,    "max": 212.5,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| World.FieldEntranceSetbackFromIntersection | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:773 | Mutation: 137.5 |
| World.FieldEntranceSurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:774 | Mutation: Gravel |
| Countryside.CellFeet | unverified | YES (sampled) | src/SpawnGuide/SpawnConfig.cs:786 | Mutation: 19800 |
| Countryside.SectionRoadWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:787 | Mutation: 82.5 |
| Countryside.HighwayWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:788 | Mutation: 82.5 |
| Countryside.SectionMid | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:789 | Mutation: 9900 |
| Countryside.TownXJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:792 | Mutation: {    "shape": "Normal",    "min": -440,    "max": 550,    "mean": 0,    "stdDev": 220,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Countryside.TownZJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:793 | Mutation: {    "shape": "Normal",    "min": -440,    "max": 550,    "mean": 0,    "stdDev": 220,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Countryside.FarmsteadCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:796 | Mutation: {    "shape": "Uniform",    "min": 6,    "max": 16.25,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Countryside.FarmsteadXRange | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:797 | Mutation: {    "shape": "Uniform",    "min": 600,    "max": 19050,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Countryside.FarmsteadZRange | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:798 | Mutation: {    "shape": "Uniform",    "min": 600,    "max": 19050,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Countryside.FarmsteadHouseRingCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:799 | Mutation: 11 |
| Countryside.FarmsteadHouseRingRadius | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:800 | Mutation: {    "shape": "Uniform",    "min": 55,    "max": 118.75,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Countryside.FarmsteadHouseRingAngleJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:801 | Mutation: 0.25 |
| Countryside.FarmsteadGroveCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:802 | Mutation: {    "shape": "Uniform",    "min": 6,    "max": 16.25,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Countryside.FarmsteadGroveRadius | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:803 | Mutation: {    "shape": "Uniform",    "min": 35,    "max": 118.75,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Countryside.FarmsteadGroveAngleJitter | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:804 | Mutation: 0.375 |
| Countryside.FieldPalette | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:807 | Structural/collection/metadata setting: needs dedicated test |
| Countryside.FarmsteadDensity | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:811 | Mutation: 1.25 |
| Countryside.FieldFeatureDensity | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:812 | Mutation: 1.25 |
| Render.HouseWallScreenPx | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:825 | Mutation: 1.1875 |
| Render.HouseGapScreenPxExtra | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:826 | Mutation: 0.75 |
| Render.HouseThinScreenPxFactor | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:827 | Mutation: 0.6875 |
| Render.MapTargetWidthPx | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:830 | Mutation: 1150 |
| Render.MapMaxHeightPx | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:831 | Mutation: 1125 |
| Render.MapMarginPx | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:832 | Mutation: 37.5 |
| Render.MapTitleHeightPx | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:833 | Mutation: 82.5 |
| Render.MapFooterHeightPx | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:834 | Mutation: 32.5 |
| Render.LodSimpleTreeRadiusPx | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:837 | Mutation: 4 |
| Render.LodDetailTreeRadiusPxFactor | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:838 | Mutation: 2.75 |
| Render.LodRailTieMinScale | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:839 | Mutation: 0.018749999 |
| Render.ColorBackground | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:842 | Mutation: #f0efe0 audit |
| Render.ColorFloor | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:843 | Mutation: #e7dfd0 audit |
| Render.ColorWall | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:844 | Mutation: #3a332b audit |
| Render.ColorGap | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:845 | Mutation: #faf7ef audit |
| Render.ColorWindow | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:846 | Mutation: #3a332b audit |
| Render.ColorDoor | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:847 | Mutation: #3a332b audit |
| Render.ColorDoorSwing | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:848 | Mutation: #8a7f70 audit |
| Render.ColorDash | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:849 | Mutation: #8a7f70 audit |
| Render.ColorText | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:850 | Mutation: #3a332b audit |
| Render.ColorSmallText | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:851 | Mutation: #6b6154 audit |
| Render.ColorRoadAsphalt | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:852 | Mutation: #8f8b83 audit |
| Render.ColorRoadGravel | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:853 | Mutation: #c8c3b5 audit |
| Render.ColorRoadEdgeGravel | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:854 | Mutation: #a9a495 audit |
| Render.ColorCenterline | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:855 | Mutation: #e8d44d audit |
| Render.ColorAlley | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:856 | Mutation: #c9c3b2 audit |
| Render.ColorDrivewayConcrete | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:857 | Mutation: #dcd9d2 audit |
| Render.ColorDrivewayGravel | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:858 | Mutation: #d6c9a3 audit |
| Render.ColorDrivewayDirt | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:859 | Mutation: #bda882 audit |
| Render.ColorWalk | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:860 | Mutation: #e3e0d8 audit |
| Render.ColorWater | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:861 | Mutation: #7f989b audit |
| Render.ColorWaterEdge | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:862 | Mutation: #a8bfc0 audit |
| Render.ColorRailBallast | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:863 | Mutation: #b7ae9c audit |
| Render.ColorRailRail | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:864 | Mutation: #4a453d audit |
| Render.ColorRailTie | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:865 | Mutation: #57503d audit |
| Render.ColorTree | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:866 | Mutation: #3f3b34 audit |
| Render.ColorContourIndex | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:867 | Mutation: #b3aa90 audit |
| Render.ColorContourMinor | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:868 | Mutation: #c8c0a7 audit |
| Render.ColorMedian | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:869 | Mutation: #8a9a6b audit |
| Render.ColorMedianEdge | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:870 | Mutation: #5f6b4c audit |
| Render.ViewerMaxZoom | unverified | not established | src/SpawnGuide/SpawnConfig.cs:874 | Probe rejected: Invalid --param 'Render.ViewerMaxZoom': Value must be in [1, 300]. |
| Render.ViewerNiceSpanLadder | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:875 | Structural/collection/metadata setting: needs dedicated test |
| Render.ViewerDebounceMs | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:877 | Mutation: 121 |
| Render.ViewerCacheSize | unverified | NO CHANGE (sampled) | src/SpawnGuide/SpawnConfig.cs:878 | Mutation: 25 |
| Noise.HillsOctaves | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:19 | Mutation: 7 |
| Noise.HillsPersistence | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:24 | Mutation: 0.5625 |
| Noise.HillsLacunarity | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:29 | Mutation: 2.625 |
| Noise.HillsBaseWavelengthFt | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:34 | Mutation: 15000 |
| Noise.HillsAmplitudeFt | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:39 | Mutation: 43.75 |
| Noise.HillsDomainWarpAmp | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:44 | Mutation: 1000 |
| Noise.HillsDomainWarpOctaves | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:49 | Mutation: 4 |
| Noise.ContourIntervalFt | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:57 | Mutation: 25 |
| Noise.ContourLatticeCellFt | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:62 | Mutation: 1650 |
| Noise.ContourIndexEvery | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:67 | Mutation: 6 |
| Noise.TrunkRiverSpacingMi | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:75 | Mutation: {    "shape": "LogNormal",    "min": 10,    "max": 100,    "mean": 28,    "stdDev": 6,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.RiverMeanderOctaves | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:80 | Mutation: 5 |
| Noise.RiverMeanderBaseAmpFt | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:85 | Mutation: 1500 |
| Noise.RiverMeanderBaseWavelengthFt | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:90 | Mutation: 10000 |
| Noise.RiverMeanderPersistence | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:95 | Mutation: 0.625 |
| Noise.RiverMeanderLacunarity | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:100 | Mutation: 2.5 |
| Noise.RiverMeanderAmpPerWidth | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:105 | Mutation: 18.75 |
| Noise.TributarySpacingMi | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:111 | Mutation: {    "shape": "LogNormal",    "min": 3,    "max": 25,    "mean": 8,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.TributaryLengthBaseMi | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:116 | Mutation: {    "shape": "LogNormal",    "min": 0.5,    "max": 12.5,    "mean": 2,    "stdDev": 0.5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.TributaryLengthJitterMi | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:121 | Mutation: {    "shape": "LogNormal",    "min": 0.1,    "max": 10,    "mean": 2.4,    "stdDev": 0.8,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.TributaryHeadJitterMi | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:125 | Mutation: {    "shape": "LogNormal",    "min": 0.1,    "max": 10,    "mean": 2.4,    "stdDev": 0.8,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.TributaryGapFromTrunkMi | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:129 | Mutation: {    "shape": "LogNormal",    "min": 0.5,    "max": 12.5,    "mean": 2.6,    "stdDev": 0.5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.TributaryGapJitterMi | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:133 | Mutation: {    "shape": "LogNormal",    "min": 1,    "max": 25,    "mean": 5.4,    "stdDev": 1.5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.TributaryBowAmpFt | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:138 | Mutation: 1000 |
| Noise.TributaryWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:142 | Mutation: 20 |
| Noise.RiverBankTreeChance | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:147 | Mutation: 0.575 |
| Noise.RiverBankTreeOffsetFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:151 | Mutation: {    "shape": "Uniform",    "min": 14,    "max": 130,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.TributaryBankTreeChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:156 | Mutation: 0.425 |
| Noise.TributaryBankTreeOffsetFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:160 | Mutation: {    "shape": "Uniform",    "min": 12,    "max": 90,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.RiverVetoDistanceFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:165 | Mutation: 1500 |
| Noise.RailSpacingMi | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:173 | Mutation: {    "shape": "Normal",    "min": 50,    "max": 125,    "mean": 75,    "stdDev": 5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.RailPhaseSections | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:178 | Mutation: 3.75 |
| Noise.RailOffsetBaseFt | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:183 | Mutation: {    "shape": "Uniform",    "min": 40,    "max": 262.5,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.RailOffsetJitterFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:189 | Mutation: 212.5 |
| Noise.RailSwellBaseAmpFt | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:195 | Mutation: 62.5 |
| Noise.RailSwellAmpJitterFt | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:200 | Mutation: 187.5 |
| Noise.RailSwellBaseWavelengthFt | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:205 | Mutation: 10000 |
| Noise.RailSwellWavelengthJitterFt | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:210 | Mutation: 12500 |
| Noise.RailSpurMaxDistanceMi | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:216 | Mutation: 6.875 |
| Noise.RailSpurOffsetBaseFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:220 | Mutation: {    "shape": "Uniform",    "min": 100,    "max": 350,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.RailSpurOffsetJitterFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:226 | Mutation: 225 |
| Noise.RailSpurSideChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:231 | Mutation: 0.625 |
| Noise.RailSpurAlwaysPerpendicular | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:236 | Mutation: False |
| Noise.RailSpurAlwaysHasElevator | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:241 | Mutation: False |
| Noise.PavedSpacingMi | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:249 | Mutation: 12.5 |
| Noise.RoadLineAbsentChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:254 | Mutation: 0.022499999 |
| Noise.RoadLinePavedChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:259 | Mutation: 0.0275 |
| Noise.FarmMarginFt | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:264 | Mutation: 375 |
| Noise.TownRailProximityMi | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:272 | Mutation: 11.25 |
| Noise.TownLayoutWeights | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:277 | Mutation: {    "shape": "Dirichlet",    "min": 0,    "max": 2,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1,    "concentrations": [      0.46,      0.24,      0.3    ]  } |
| Noise.TownClassWeights | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:282 | Mutation: {    "shape": "Dirichlet",    "min": 0,    "max": 2,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1,    "concentrations": [      0.315,      0.185,      0.09,      0.045    ]  } |
| Noise.TownSlotJitterFraction | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:287 | Mutation: 0.04375 |
| Noise.TownMinSpacingMi | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:292 | Mutation: 25 |
| Noise.FarmsPerSectionMax | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:300 | Mutation: 4 |
| Noise.FarmSectionJitterFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:305 | Mutation: {    "shape": "Normal",    "min": -500,    "max": 625,    "mean": 0,    "stdDev": 250,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.FarmAlongJitterFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/NoiseConfig.cs:310 | Mutation: {    "shape": "LogNormal",    "min": 50,    "max": 2500,    "mean": 5.3,    "stdDev": 0.6,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Noise.FarmDensity | unverified | YES (sampled) | src/SpawnGuide/NoiseConfig.cs:315 | Mutation: 0.1875 |
| TownTiers.KValue | unverified | NO CHANGE (sampled) | src/SpawnGuide/TownTierSystem.cs:17 | Mutation: 4 |
| TownTiers.MaxTier | unverified | NO CHANGE (sampled) | src/SpawnGuide/TownTierSystem.cs:22 | Mutation: 4 |
| TownTiers.BaseColumns | unverified | NO CHANGE (sampled) | src/SpawnGuide/TownTierSystem.cs:28 | Mutation: {    "shape": "LogNormal",    "min": 1,    "max": 6.25,    "mean": 0.4,    "stdDev": 0.4,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownTiers.ColumnsGrowthPerTier | unverified | YES (sampled) | src/SpawnGuide/TownTierSystem.cs:33 | Mutation: 2.25 |
| TownTiers.BaseEWStreets | unverified | NO CHANGE (sampled) | src/SpawnGuide/TownTierSystem.cs:38 | Mutation: {    "shape": "LogNormal",    "min": 1,    "max": 5,    "mean": 0,    "stdDev": 0.3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| TownTiers.EWStreetsGrowthPerTier | unverified | YES (sampled) | src/SpawnGuide/TownTierSystem.cs:43 | Mutation: 1.875 |
| TownTiers.CommercialPadsPer1000Pop | unverified | NO CHANGE (sampled) | src/SpawnGuide/TownTierSystem.cs:48 | Mutation: 3.125 |
| TownTiers.CivicChurchMinPop | unverified | NO CHANGE (sampled) | src/SpawnGuide/TownTierSystem.cs:54 | Mutation: 51 |
| TownTiers.CivicSchoolMinPop | unverified | NO CHANGE (sampled) | src/SpawnGuide/TownTierSystem.cs:59 | Mutation: 201 |
| TownTiers.CivicParkMinPop | unverified | NO CHANGE (sampled) | src/SpawnGuide/TownTierSystem.cs:64 | Mutation: 151 |
| TownTiers.LandmarkWaterTowerChancePerTier | unverified | NO CHANGE (sampled) | src/SpawnGuide/TownTierSystem.cs:70 | Mutation: 0.375 |
| TownTiers.LandmarkGolfCourseChancePerTier | unverified | NO CHANGE (sampled) | src/SpawnGuide/TownTierSystem.cs:75 | Mutation: 0.125 |
| TownTiers.PopulationPerLot | unverified | NO CHANGE (sampled) | src/SpawnGuide/TownTierSystem.cs:81 | Mutation: 3.125 |
| TownExtras | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:39 | Structural/collection/metadata setting: needs dedicated test |
| Commercial.HighwayCommercialSpacingMi | unverified | NO CHANGE (sampled) | src/SpawnGuide/CommercialConfig.cs:51 | Mutation: 1.875 |
| Commercial.HighwayCommercialDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CommercialConfig.cs:56 | Mutation: {    "shape": "LogNormal",    "min": 20,    "max": 125,    "mean": 3.5,    "stdDev": 0.3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Commercial.HighwayCommercialWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CommercialConfig.cs:61 | Mutation: {    "shape": "LogNormal",    "min": 20,    "max": 150,    "mean": 3.8,    "stdDev": 0.4,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Commercial.BusinessTypeWeights | unverified | not probed | src/SpawnGuide/CommercialConfig.cs:66 | Structural/collection/metadata setting: needs dedicated test |
| Commercial.BusinessTypes | unverified | not probed | src/SpawnGuide/CommercialConfig.cs:78 | Structural/collection/metadata setting: needs dedicated test |
| Commercial.ElevatorOffsetFromHighwayFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CommercialConfig.cs:86 | Mutation: 250 |
| Commercial.ElevatorBinCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/CommercialConfig.cs:91 | Mutation: {    "shape": "Poisson",    "min": 2,    "max": 18.75,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 3,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Commercial.BinDiameterFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CommercialConfig.cs:96 | Mutation: {    "shape": "LogNormal",    "min": 18,    "max": 37.5,    "mean": 3.1,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Commercial.BinSpacingFt | unverified | YES (sampled) | src/SpawnGuide/CommercialConfig.cs:101 | Mutation: 37.5 |
| Commercial.ElevatorWidthFt | unverified | YES (sampled) | src/SpawnGuide/CommercialConfig.cs:105 | Mutation: 60 |
| Commercial.ElevatorDepthFt | unverified | YES (sampled) | src/SpawnGuide/CommercialConfig.cs:109 | Mutation: 110 |
| Commercial.ElevatorRightChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/CommercialConfig.cs:113 | Mutation: 0.775 |
| Commercial.RailIndustrialLotDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CommercialConfig.cs:121 | Mutation: {    "shape": "LogNormal",    "min": 60,    "max": 250,    "mean": 4.5,    "stdDev": 0.5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Commercial.RailIndustrialLotWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CommercialConfig.cs:126 | Mutation: {    "shape": "LogNormal",    "min": 30,    "max": 187.5,    "mean": 3.8,    "stdDev": 0.4,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Commercial.RailIndustrialRowSpacingFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CommercialConfig.cs:130 | Mutation: {    "shape": "Uniform",    "min": 56,    "max": 105,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Commercial.RailIndustrialZJitterFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CommercialConfig.cs:134 | Mutation: {    "shape": "Normal",    "min": -50,    "max": 62.5,    "mean": 0,    "stdDev": 16,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Commercial.RailIndustrialTypes | unverified | not probed | src/SpawnGuide/CommercialConfig.cs:139 | Structural/collection/metadata setting: needs dedicated test |
| Commercial.CrossStreetShopMax | unverified | YES (sampled) | src/SpawnGuide/CommercialConfig.cs:152 | Mutation: 4 |
| Commercial.CrossStreetShopDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CommercialConfig.cs:156 | Mutation: {    "shape": "Uniform",    "min": 24,    "max": 125,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Commercial.CrossStreetShopWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CommercialConfig.cs:160 | Mutation: {    "shape": "Uniform",    "min": 24,    "max": 125,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.LotFrontageFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:19 | Mutation: {    "shape": "LogNormal",    "min": 45,    "max": 125,    "mean": 4.2,    "stdDev": 0.2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.LotDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:24 | Mutation: {    "shape": "LogNormal",    "min": 80,    "max": 225,    "mean": 4.8,    "stdDev": 0.15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.FrontSetbackFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:32 | Mutation: {    "shape": "Normal",    "min": 15,    "max": 50,    "mean": 25,    "stdDev": 5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.SideSetbackFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:37 | Mutation: {    "shape": "Normal",    "min": 5,    "max": 18.75,    "mean": 7.5,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.RearSetbackFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:42 | Mutation: {    "shape": "Normal",    "min": 15,    "max": 50,    "mean": 25,    "stdDev": 5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.GarageModeWeights | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:50 | Mutation: {    "shape": "Dirichlet",    "min": 0,    "max": 2,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1,    "concentrations": [      0.55,      0.35,      0.1    ]  } |
| Residential.AttachedGarageWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:55 | Mutation: {    "shape": "Normal",    "min": 18,    "max": 35,    "mean": 22,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.AttachedGarageDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:60 | Mutation: {    "shape": "Normal",    "min": 18,    "max": 35,    "mean": 22,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.DetachedGarageWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:65 | Mutation: {    "shape": "Normal",    "min": 18,    "max": 40,    "mean": 24,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.DetachedGarageDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:70 | Mutation: {    "shape": "Normal",    "min": 18,    "max": 40,    "mean": 24,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.DrivewayWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:78 | Mutation: {    "shape": "Normal",    "min": 12,    "max": 30,    "mean": 16,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.DrivewaySurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:83 | Mutation: Gravel |
| Residential.WalkWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:88 | Mutation: {    "shape": "Normal",    "min": 3,    "max": 6.25,    "mean": 4,    "stdDev": 0.5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.WalkSurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:93 | Mutation: Gravel |
| Residential.ShedChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:101 | Mutation: 0.1875 |
| Residential.ShedWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:105 | Mutation: {    "shape": "Normal",    "min": 8,    "max": 17.5,    "mean": 10,    "stdDev": 1.5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.ShedDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:109 | Mutation: {    "shape": "Normal",    "min": 8,    "max": 22.5,    "mean": 12,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.StreetTreeSpacingFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:117 | Mutation: {    "shape": "Normal",    "min": 25,    "max": 75,    "mean": 40,    "stdDev": 8,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.YardTreeCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:122 | Mutation: {    "shape": "Poisson",    "min": 0,    "max": 3.4028235E+38,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 2.5,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Residential.DensityMultiplier | unverified | NO CHANGE (sampled) | src/SpawnGuide/ResidentialConfig.cs:139 | Mutation: 1.25 |
| Streets.StreetRowFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:19 | Mutation: 82.5 |
| Streets.StreetPavedWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:24 | Mutation: 40 |
| Streets.CenterlineOffsetFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:29 | Mutation: 0.2 |
| Streets.StreetSurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:34 | Mutation: Gravel |
| Streets.AlleyRowFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:42 | Mutation: 20 |
| Streets.AlleySurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:47 | Mutation: Concrete |
| Streets.AlleyChancePerBlock | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:52 | Mutation: {    "shape": "Beta",    "min": 0,    "max": 2,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 2,    "beta": 2,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Streets.PerimeterGapNFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:60 | Mutation: 62.5 |
| Streets.PerimeterJogHeightFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:65 | Mutation: 100 |
| Streets.DeadEndStubLengthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:73 | Mutation: 312.5 |
| Streets.DeadEndBulbRadiusFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:78 | Mutation: 55 |
| Streets.FourSideBlockChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:86 | Mutation: 0.875 |
| Streets.RailCorridorStreetChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:91 | Mutation: 0.375 |
| Streets.CurvedStreetChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:96 | Mutation: 0.375 |
| Streets.FreeStreetEraseHalfFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/StreetConfig.cs:101 | Mutation: 25 |
| Farmsteads.LotWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:19 | Mutation: {    "shape": "LogNormal",    "min": 180,    "max": 437.5,    "mean": 5.5,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.LotDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:24 | Mutation: {    "shape": "LogNormal",    "min": 250,    "max": 625,    "mean": 5.8,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.HouseArchetypeWeights | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:29 | Mutation: {    "shape": "Dirichlet",    "min": 0,    "max": 2,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1,    "concentrations": [      0.6,      0.3,      0.1    ]  } |
| Farmsteads.HouseSetbackFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:34 | Mutation: {    "shape": "Normal",    "min": 30,    "max": 100,    "mean": 50,    "stdDev": 10,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.MachineShedWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:42 | Mutation: {    "shape": "LogNormal",    "min": 30,    "max": 75,    "mean": 3.7,    "stdDev": 0.15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.MachineShedDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:47 | Mutation: {    "shape": "LogNormal",    "min": 24,    "max": 62.5,    "mean": 3.5,    "stdDev": 0.15,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.MachineShedBehindHouseFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:52 | Mutation: 32.5 |
| Farmsteads.BinCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:60 | Mutation: {    "shape": "Poisson",    "min": 2,    "max": 12.5,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 3,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.BinDiameterFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:65 | Mutation: {    "shape": "LogNormal",    "min": 15,    "max": 37.5,    "mean": 3,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.BinRowBehindShedFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:70 | Mutation: 10 |
| Farmsteads.PoleBarnChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:78 | Mutation: 0.5 |
| Farmsteads.PoleBarnWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:83 | Mutation: {    "shape": "Normal",    "min": 20,    "max": 55,    "mean": 30,    "stdDev": 4,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.PoleBarnDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:88 | Mutation: {    "shape": "Normal",    "min": 32,    "max": 90,    "mean": 48,    "stdDev": 6,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.GambrelBarnChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:96 | Mutation: 0.1875 |
| Farmsteads.GambrelBarnWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:100 | Mutation: {    "shape": "Normal",    "min": 26,    "max": 55,    "mean": 34,    "stdDev": 4,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.GambrelBarnDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:104 | Mutation: {    "shape": "Normal",    "min": 40,    "max": 95,    "mean": 56,    "stdDev": 6,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.ShelterbeltChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:112 | Mutation: 0.75 |
| Farmsteads.ShelterbeltTreeSpacingFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:117 | Mutation: {    "shape": "Normal",    "min": 12,    "max": 27.5,    "mean": 16,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.ShelterbeltRows | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:122 | Mutation: {    "shape": "Poisson",    "min": 2,    "max": 12.5,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 3,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.ShelterbeltOffsetFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:127 | Mutation: {    "shape": "Normal",    "min": 15,    "max": 75,    "mean": 30,    "stdDev": 10,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.DrivewayWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:135 | Mutation: {    "shape": "Normal",    "min": 10,    "max": 25,    "mean": 14,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.DrivewaySurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:140 | Mutation: Concrete |
| Farmsteads.WalkWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:145 | Mutation: {    "shape": "Normal",    "min": 2.5,    "max": 6.25,    "mean": 3.5,    "stdDev": 0.5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| Farmsteads.WalkSurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:150 | Mutation: Gravel |
| Farmsteads.LpTankChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:157 | Mutation: 0.375 |
| Farmsteads.LpTankOffsetFromHouseFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:161 | Mutation: 10 |
| Farmsteads.DensityMultiplier | unverified | NO CHANGE (sampled) | src/SpawnGuide/FarmsteadConfig.cs:163 | Mutation: 1.25 |
| HouseArchetypes.Ranch.WidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:20 | Mutation: {    "shape": "LogNormal",    "min": 30,    "max": 68.75,    "mean": 3.7,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.Ranch.DepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:25 | Mutation: {    "shape": "LogNormal",    "min": 20,    "max": 43.75,    "mean": 3.2,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.Ranch.GarageWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:30 | Mutation: {    "shape": "Normal",    "min": 16,    "max": 37.5,    "mean": 22,    "stdDev": 3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.Ranch.GarageDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:35 | Mutation: {    "shape": "Normal",    "min": 18,    "max": 32.5,    "mean": 22,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.Ranch.GarageLeftChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:40 | Mutation: 0.625 |
| HouseArchetypes.Ranch.DoorOffsetFraction | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:45 | Mutation: {    "shape": "Beta",    "min": 0.15,    "max": 1.35,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 2,    "beta": 5,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.Ranch.WindowJitterFraction | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:50 | Mutation: {    "shape": "Beta",    "min": 0,    "max": 1.08,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 2,    "beta": 5,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.Farmhouse.WidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:61 | Mutation: {    "shape": "LogNormal",    "min": 24,    "max": 50,    "mean": 3.4,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.Farmhouse.DepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:66 | Mutation: {    "shape": "LogNormal",    "min": 26,    "max": 55,    "mean": 3.5,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.Farmhouse.PorchWidthFraction | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:71 | Mutation: {    "shape": "Beta",    "min": 0.5,    "max": 1.8,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 5,    "beta": 3,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.Farmhouse.PorchDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:76 | Mutation: {    "shape": "Normal",    "min": 5,    "max": 12.5,    "mean": 7,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.Farmhouse.DoorCenteredChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:81 | Mutation: 1.125 |
| HouseArchetypes.SplitLevel.WidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:92 | Mutation: {    "shape": "LogNormal",    "min": 24,    "max": 50,    "mean": 3.4,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.SplitLevel.DepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:97 | Mutation: {    "shape": "LogNormal",    "min": 24,    "max": 50,    "mean": 3.4,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.SplitLevel.GarageWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:102 | Mutation: {    "shape": "Normal",    "min": 12,    "max": 27.5,    "mean": 16,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.SplitLevel.GarageDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:107 | Mutation: {    "shape": "Normal",    "min": 16,    "max": 32.5,    "mean": 20,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.SplitLevel.GarageLeftChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:111 | Mutation: 0.625 |
| HouseArchetypes.SplitLevel.GarageDoorOnSideChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:116 | Mutation: 1.125 |
| HouseArchetypes.SidingOptions | unverified | not probed | src/SpawnGuide/HouseArchetypeConfig.cs:129 | Structural/collection/metadata setting: needs dedicated test |
| HouseArchetypes.GarageDoorWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:143 | Mutation: {    "shape": "Bimodal",    "min": 0,    "max": 2,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.65,    "value1": 9,    "value2": 16  } |
| HouseArchetypes.TwoWindowMinWallFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:148 | Mutation: {    "shape": "Normal",    "min": 18,    "max": 35,    "mean": 22,    "stdDev": 2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| HouseArchetypes.OneWindowMinWallFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/HouseArchetypeConfig.cs:153 | Mutation: {    "shape": "Normal",    "min": 7,    "max": 15,    "mean": 9,    "stdDev": 1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| CoopCorners.ChancePerSectionCorner | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:15 | Mutation: 0.024999999 |
| CoopCorners.RequiresRail | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:20 | Mutation: False |
| CoopCorners.RequiresHighway | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:25 | Mutation: True |
| CoopCorners.ElevatorCapacityBu | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:33 | Mutation: {    "shape": "LogNormal",    "min": 50000,    "max": 1250000,    "mean": 5.5,    "stdDev": 0.3,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| CoopCorners.BinCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:38 | Mutation: {    "shape": "Poisson",    "min": 2,    "max": 25,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 6,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| CoopCorners.BinDiameterFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:43 | Mutation: {    "shape": "LogNormal",    "min": 18,    "max": 37.5,    "mean": 3.1,    "stdDev": 0.1,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| CoopCorners.BinSpacingFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:47 | Mutation: 37.5 |
| CoopCorners.ElevatorWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:51 | Mutation: 67.5 |
| CoopCorners.ElevatorDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:55 | Mutation: 125 |
| CoopCorners.OfficeSizeFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:63 | Mutation: {    "shape": "Normal",    "min": 24,    "max": 75,    "mean": 40,    "stdDev": 10,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| CoopCorners.ScaleHouseSizeFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:68 | Mutation: {    "shape": "Normal",    "min": 12,    "max": 37.5,    "mean": 20,    "stdDev": 5,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| CoopCorners.OfficeOffsetFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:72 | Mutation: 37.5 |
| CoopCorners.ScaleHouseOffsetFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:76 | Mutation: 62.5 |
| CoopCorners.LotWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:83 | Mutation: 375 |
| CoopCorners.LotDepthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:87 | Mutation: 500 |
| CoopCorners.SidingLengthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:91 | Mutation: 750 |
| CoopCorners.AccessRoadWidthFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:95 | Mutation: 30 |
| CoopCorners.AccessRoadSurface | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:99 | Mutation: Concrete |
| CoopCorners.FeedMillChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:106 | Mutation: 0.375 |
| CoopCorners.FeedMillSizeFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:110 | Mutation: {    "shape": "Normal",    "min": 24,    "max": 75,    "mean": 40,    "stdDev": 8,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| CoopCorners.FertilizerStorageChance | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:114 | Mutation: 0.5 |
| CoopCorners.FertilizerTankCount | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:118 | Mutation: {    "shape": "Poisson",    "min": 1,    "max": 12.5,    "mean": 0,    "stdDev": 1,    "mode": 0,    "lambda": 2,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| CoopCorners.FertilizerTankDiameterFt | unverified | NO CHANGE (sampled) | src/SpawnGuide/CoopCornerConfig.cs:122 | Mutation: {    "shape": "LogNormal",    "min": 12,    "max": 30,    "mean": 2.7,    "stdDev": 0.2,    "mode": 0,    "lambda": 1,    "alpha": 1,    "beta": 1,    "weight1": 0.5,    "value1": 0,    "value2": 1  } |
| ConditionalRules | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:60 | Structural/collection/metadata setting: needs dedicated test |
| SchemaVersion | unverified | not probed | src/SpawnGuide/SpawnConfig.cs:95 | Structural/collection/metadata setting: needs dedicated test |
