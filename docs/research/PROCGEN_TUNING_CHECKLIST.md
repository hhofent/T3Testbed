# Procedural Generation Tuning Parameters — Implementation Checklist

> Derived from `docs/research/procgen-tuning-parameters.md`. Check off items as completed.

---

## Phase 1: Core Infrastructure (8 days)

### 1.1 Config Profiles / Presets
- [ ] `ConfigProfile` class in `SpawnConfig.cs` or new `ConfigProfiles.cs`
- [ ] Built-in profiles: `default`, `dense-suburban`, `sparse-frontier`, `modern-iowa`, `railroad-town`, `river-valley`, `testing-minimal`, `testing-max`
- [ ] Profile storage in config JSON (Profiles dictionary)
- [ ] CLI: `--profile <name>`, `--list-profiles`, `--save-profile <name>`
- [ ] Profile validation on load

### 1.2 Distribution Shapes
- [ ] `DistributionShape` enum: `Uniform`, `Normal`, `LogNormal`, `Poisson`, `Exponential`, `Triangular`, `Beta`
- [ ] `FloatDistribution` struct with `Sample(Rng)` method
- [ ] JSON serialization for `FloatDistribution`
- [ ] Migrate key `FloatRange` → `FloatDistribution`:
  - [ ] `HouseConfig`: RanchWidth, RanchDepth, FarmhouseWidth, FarmhouseDepth, SplitLevelWidth, SplitLevelDepth
  - [ ] `RuralConfig`: LotWidth, LotDepth, HouseXFraction, HouseSetback, BinCount (Poisson)
  - [ ] `TownBlockConfig`: LotFrontage, StreetTreeSpacing (Poisson), YardTreeCount (Poisson)
  - [ ] `SmallTownConfig`: LotFrontage, Vacancy radii (Beta), BlobAmplitudes
  - [ ] `WorldConfig`: Elevation amplitudes, River meander amplitudes

### 1.3 Noise Parameterization
- [ ] `NoiseConfig` class extracted from `WorldConfig`
- [ ] Hills: `Octaves`, `Persistence`, `Lacunarity`, `Amplitudes[]`, `Wavelengths[]`
- [ ] Rivers: `MeanderOctaves`, `MeanderAmps[]`, `MeanderWaves[]`
- [ ] Rail: `SwellBaseAmp`, `SwellAmpJitter`, `SwellBaseWave`, `SwellWaveJitter`
- [ ] Town placement: `TownSlotJitterFraction` (already exists)
- [ ] Update `WorldMap.AddHills`, `AddRivers`, `AddRail` to use `NoiseConfig`
- [ ] Backward compat: if `NoiseConfig` null, derive from legacy fields

### 1.4 Conditional Parameter Rules
- [ ] `ConditionalRule` class in new `ConfigRules.cs`
- [ ] Rule engine: `ApplyRules(SpawnConfig config)` — runs after load, before generation
- [ ] Supported operators: `>=`, `<=`, `==`, `!=`, `in`, `notin`
- [ ] Supported operations: `set`, `multiply`, `add`, `clamp`
- [ ] Built-in rules:
  - [ ] IF `Town.Tier >= 2` THEN `CivicDensity *= 1.5`
  - [ ] IF `World.TownLayout == Both` THEN `ResidentialDensity *= 1.2`
  - [ ] IF `Rural.LotWidth > 250` THEN `OutbuildingDensity *= 0.8`
  - [ ] IF `SmallTown.SizePreset.CommercialPads > 10` THEN `IndustrialDensity *= 1.3`

---

## Phase 2: Biome / Region System (5 days)

### 2.1 Region Config
- [ ] `RegionConfig` class in `WorldConfig.cs`
- [ ] Built-in Iowa regions (5):
  - [ ] `loess-hills` (west): steeper contours, narrower valleys, fewer towns
  - [ ] `driftless` (northeast): rugged, no grid roads, organic streets
  - [ ] `prairie-pothole` (north-central): many wetlands, no basements
  - [ ] `southern-iowa` (south): rolling, more timber, different siding
  - [ ] `cedar-valley` (east): river-focused, industrial corridors
- [ ] Region override merging: `ConfigMerge(MergeStrategy.Override, RegionConfig)`
- [ ] Region priority for overlaps

### 2.2 Region-Aware Generation
- [ ] `WorldMap.GetRegionAt(float worldX, float worldZ)` → `RegionConfig?`
- [ ] `WorldMap.Generate` queries region at window center (or per-cell)
- [ ] Smooth boundary transitions: lerp parameters over 1-mile transition zone
- [ ] Region-specific place name pools, business catalogs, siding options

---

## Phase 3: Data-Driven Catalogs (4 days)

### 3.1 Business / Civic / Landmark Catalogs
- [ ] `business-catalog.json` — all highway/commercial/industrial/rail businesses
- [ ] `civic-catalog.json` — churches, schools, parks, post office, fire hall, library
- [ ] `landmark-catalog.json` — water tower, golf course, fairground, landing strip, cemetery, ball field, depot
- [ ] Catalog schema: `id`, `name`, `widthRange`, `depthRange`, `tags[]`, `minTownTier`, `maxCountPerTown`, `requiresRail`, `chancePerTier`
- [ ] Loader + validation in `BusinessCatalogue.cs`
- [ ] `SmallTownGenerator` uses catalog instead of hardcoded arrays

### 3.2 Name Pools
- [ ] `name-pools.json` — place names, E-W street names, N-S street names, business names
- [ ] Loader + per-region overrides
- [ ] `SmallTownGenerator` uses external pools

---

## Phase 4: Advanced Placement & Topology (6 days)

### 4.1 Road Network Topology
- [ ] `RoadNetworkConfig.Topology` enum: `Grid`, `Organic`, `Hybrid`, `Radial`, `RiverFollowing`
- [ ] `RoadNetworkConfig` params: `OrganicJitterFraction`, `RiverFollowChance`, `TIntersectionChance`, `CulDeSacChance`, `RoundaboutChance`, `MinBlockSizeFt`, `MaxBlockSizeFt`, `BlockAspectRatio`
- [ ] `WorldMap.AddRoads` uses topology params
- [ ] Grid → organic interpolation (jitter grid vertices)
- [ ] River-following: roads snap to river banks where parallel

### 4.2 Placement Constraint System
- [ ] `PlacementConstraint` class: `Type` (MinDistance, MaxDistance, Alignment, Containment, Exclusion, Snapping), `TargetTag`, `Value`, `Weight`, `Hard`
- [ ] Constraint solver: `TryPlace(Rect candidate, List<PlacementConstraint>)` → `bool`
- [ ] Apply to: house placement, outbuilding placement, lot platting, civic building placement
- [ ] Default constraints per feature type (configurable)

### 4.3 Connectivity Validation
- [ ] `ConnectivityConfig`: `RequireRoadAccess`, `RequireTownConnectivity`, `RequireRailConnectivity`, `RequireRiverCrossings`, `MaxDeadEndLengthFt`, `ValidateOnGenerate`
- [ ] Validation pass after generation (optional, slow)
- [ ] Reports: orphaned lots, disconnected streets, missing rail spurs, missing bridges

---

## Phase 5: Developer Experience (5 days)

### 5.1 Parameter Explorer UI
- [ ] `out/param-explorer.html` — static, no server deps
- [ ] Tree view of all config sections (Houses, Rural, TownBlock, SmallTown, World, Countryside, Render)
- [ ] Input controls per parameter type: slider (float), number input, checkbox (bool), dropdown (enum), color picker, array editor
- [ ] Live preview: 300px SVG regenerates on parameter change (debounced)
- [ ] "Diff from default" highlighting
- [ ] Export/import JSON config
- [ ] Profile selector dropdown
- [ ] Integrated into `--serve` at `/params` route

### 5.2 Sensitivity Analysis CLI
- [ ] Command: `--analyze-sensitivity --param <path> --range <min,max> --steps <n> --seeds <n>`
- [ ] Parameter path syntax: `SmallTown.ResidentialDensity`, `World.TownDensity`, `Rural.OutbuildingDensity`
- [ ] Runs N seeds per step, collects metrics: house count, building count, road length, town area, vacancy rate
- [ ] Output: markdown table + CSV option
- [ ] Parallel execution for speed

### 5.3 Config Validation & Migration
- [ ] Extend `SpawnConfig.Validate()` with warnings (non-fatal)
- [ ] Schema migration: `MigrateFrom(int fromVersion) → SpawnConfig`
- [ ] CLI: `--validate-config <file>`, `--migrate-config <file>`, `--diff-config <old> <new>`
- [ ] Auto-migrate on load if `SchemaVersion` < current

---

## Phase 6: Advanced Features (Deferred)

- [ ] 6.1 Temporal evolution parameters (`YearConfig`)
- [ ] 6.2 Multi-scale LOD parameter sets
- [ ] 6.3 Inverse design optimizer (Bayesian/CMA-ES)
- [ ] 6.4 Style transfer from reference SVGs

---

## Quick Wins (Do First — 1-2 days each)

1. [ ] **Config Profiles** — Immediate UX win, enables "modes"
2. [ ] **Distribution Shapes** — High impact: normal/log-normal/poisson feel much more natural than uniform
3. [ ] **External Catalogs** — Removes hardcoded arrays, enables modding
4. [ ] **Parameter Explorer UI** — Transforms tuning from "edit JSON, rerun" to interactive

---

## Testing Checklist (Per Phase)

- [ ] Unit tests for new types (`FloatDistribution`, `ConditionalRule`, `PlacementConstraint`)
- [ ] Determinism: same seed + config = byte-identical output
- [ ] `--verify` passes with new configs
- [ ] Edge cases: min/max values, zero densities, empty arrays
- [ ] Cross-platform: Windows/Linux (CI)

---

## Documentation Checklist

- [ ] Auto-generate `PARAMS.md` from XML docs: `dotnet run -- --doc-params`
- [ ] `docs/tuning-guide.md` — recipes for common looks
- [ ] `out/profile-gallery.html` — visual comparison of all profiles
- [ ] Update `README.md` with new CLI flags

---

*Check off items as implemented. Update this file to track progress.*