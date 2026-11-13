# Procedural Generation Tuning Parameters — Research & Implementation Plan

## Executive Summary

This document surveys procedural generation (procgen) parameter design patterns, identifies gaps in the current `SpawnConfig` system, and provides a prioritized implementation list for granular control over the 1990s rural Iowa generator.

---

## 1. Procgen Parameter Taxonomy

### 1.1 Parameter Categories

| Category | Purpose | Examples |
|----------|---------|----------|
| **Geometry** | Physical dimensions, ranges, distributions | Lot sizes, building footprints, street widths |
| **Placement** | Spatial logic, constraints, relationships | Setbacks, collision pads, alignment rules |
| **Distribution** | Counts, densities, probabilities | Trees per lot, outbuilding chances, town class weights |
| **Style/Appearance** | Visual variety, materials, palettes | Siding options, roof types, color schemes |
| **Structure** | Hierarchical composition, grammar rules | Town size presets, road network topology |
| **Noise/Variation** | Randomness control, seeds, jitter | Perlin octaves, jitter ranges, harmonic parameters |
| **Validation** | Hard constraints, quality gates | Min spacing, max overlap, connectivity checks |
| **Performance** | LOD, culling, cache sizes | Viewer zoom limits, render target dimensions |
| **Narrative/Thematic** | Named pools, cultural markers | Street names, place names, business types |

### 1.2 Parameter Granularity Levels

| Level | Scope | Example |
|-------|-------|---------|
| **Global** | Entire world | `WorldConfig.FarmDensity` |
| **System** | One generator | `SmallTownConfig.ResidentialDensity` |
| **Feature** | One feature type | `RuralConfig.BinCount` |
| **Instance** | Single placement | `HouseConfig.RanchWidth` (per-house) |
| **Sub-component** | Part of a feature | `HouseConfig.FrontWindowJitterFraction` |

### 1.3 Parameter Types

| Type | Use Case | Serialization |
|------|----------|---------------|
| `FloatRange` / `IntRange` | Continuous/discrete ranges | `[min, max]` |
| `float` probability | Binary chances | `0.0–1.0` |
| `float` multiplier | Density scaling | `0.5 = half, 2.0 = double` |
| `T[]` options | Discrete choice pools | `["vinyl", "brick"]` |
| `Dictionary<K, V>` presets | Named configurations | `sizePresets` |
| `struct` groups | Related parameters | `TownSizePreset` |

---

## 2. Current State Analysis

### 2.1 Existing Coverage (Strong)

| System | Config Class | Parameter Count | Notes |
|--------|--------------|-----------------|-------|
| Houses | `HouseConfig` | ~25 | Good archetype separation |
| Rural lots | `RuralConfig` | ~30 | Comprehensive farmstead control |
| Town block | `TownBlockConfig` | ~28 | Solid single-block params |
| Small town | `SmallTownConfig` | ~100 | Very deep, includes extras, landmarks |
| World map | `WorldConfig` | ~65 | Full pipeline: hills→rivers→rail→roads→towns→farms |
| Countryside | `CountrysideConfig` | ~20 | 3-mile cell views |
| Rendering | `RenderConfig` | ~35 | SVG + viewer params |

**Total: ~300+ tunable parameters** — already substantial.

### 2.2 Identified Gaps

| Gap | Impact | Priority |
|-----|--------|----------|
| **No parameter groups/profiles** | Can't save/load "dense suburbs" vs "sparse frontier" | High |
| **No cross-system correlations** | Town density doesn't auto-adjust farm density | Medium |
| **Limited distribution shapes** | Only uniform ranges; no normal, log-normal, poisson | Medium |
| **No conditional parameters** | "If town > village, then more civic buildings" | Medium |
| **No animation/transition params** | Can't interpolate between configs for timelapse | Low |
| **No parameter validation UI** | Hard to explore ranges without running generator | Medium |
| **Missing noise parameterization** | Hills/rivers/rail use hardcoded wavelengths | High |
| **No biome/region system** | Can't vary style by world region | High |
| **Limited business/civic catalogs** | Hardcoded arrays, not data-driven | Medium |
| **No road network topology params** | Grid is fixed; no organic/curvilinear options | Medium |

---

## 3. Research: Procgen Parameter Best Practices

### 3.1 From Game Industry (No Man's Sky, Elite Dangerous, Minecraft, Dwarf Fortress)

| Practice | Description | Application Here |
|----------|-------------|------------------|
| **Layered parameters** | Base → biome → local overrides | World → region → town → lot |
| **Parameter inheritance** | Child configs inherit + override | `SpawnConfig` already supports this |
| **Curves over ranges** | `AnimationCurve` for non-linear distributions | Tree density vs distance from road |
| **Correlated parameters** | Groups that move together | "Density profile" scales houses+trees+outbuildings |
| **Seed hierarchies** | Master seed → domain seeds → instance seeds | `Rng` already does this well |
| **Parameter baking** | Precompute expensive distributions | Contour lookup tables, river paths |
| **Debug visualization** | Heatmaps, parameter sensitivity plots | Viewer could show param effects |

### 3.2 From Academic Procgen (PCG Taxonomy, Search-Based PCG)

| Concept | Relevance |
|---------|-----------|
| **Generative space** | The set of all possible outputs; parameters define axes |
| **Expressivity** | How much of the space is reachable; more params = more expressive |
| **Controllability** | Can a user hit a target output? Needs intuitive params |
| **Local vs global control** | Local = per-instance; global = statistical aggregates |
| **Parameter sensitivity** | Some params drastically change output; others barely matter |

### 3.3 Parameter Design Principles

1. **Orthogonality** — Parameters should control independent aspects
2. **Intuitive defaults** — Default = "typical Iowa 1990s"
3. **Bounded ranges** — Min/max that produce valid output
4. **Semantic naming** — `HouseSetbackMin` not `HSM`
5. **Grouping** — Related params in nested configs
6. **Documentation** — Every param has XML doc + JSON comment
7. **Validation** — Fail fast with clear messages
8. **Versioning** — Schema version for migration

---

## 4. Implementation Plan — Prioritized

### Phase 1: Core Infrastructure (Week 1-2)

#### 1.1 Parameter Profiles / Presets
**File:** `SpawnConfig.cs` + new `ConfigProfiles.cs`

```csharp
public sealed class ConfigProfile
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public SpawnConfig Config { get; set; } = SpawnConfig.Default;
    public string[] Tags { get; set; } = Array.Empty<string>(); // "dense", "frontier", "modern", "test"
}

// Built-in profiles:
- "default" — Current 1990s rural Iowa
- "dense-suburban" — Smaller lots, more houses, fewer outbuildings
- "sparse-frontier" — Larger lots, fewer towns, more farmland
- "modern-iowa" — 2020s: larger houses, attached garages, fewer bins
- "railroad-town" — Towns forced on rail, heavy industrial
- "river-valley" — More tributaries, floodplain farms, no towns on water
- "testing-minimal" — Tiny world, few features, fast generation
- "testing-max" — Everything maxed, stress test
```

**CLI:** `--profile dense-suburban`, `--list-profiles`, `--save-profile myprofile`

#### 1.2 Distribution Shapes (Beyond Uniform)
**File:** New `Distributions.cs`

```csharp
public enum DistributionShape { Uniform, Normal, LogNormal, Poisson, Exponential, Triangular, Beta }

public sealed class FloatDistribution
{
    public DistributionShape Shape { get; set; } = DistributionShape.Uniform;
    public float Min { get; set; } = 0f;
    public float Max { get; set; } = 1f;
    public float Mean { get; set; } = 0.5f;      // For Normal, LogNormal
    public float StdDev { get; set; } = 0.15f;   // For Normal
    public float Rate { get; set; } = 1f;        // For Poisson, Exponential
    public float Alpha { get; set; } = 2f;       // For Beta
    public float Beta { get; set; } = 2f;        // For Beta
    public float Mode { get; set; } = 0.5f;      // For Triangular
    
    public float Sample(Rng rng) => Shape switch { ... };
}
```

**Migration:** Replace `FloatRange` with `FloatDistribution` where shape matters (tree spacing, lot frontage, building sizes).

#### 1.3 Noise Parameterization
**File:** `WorldConfig.cs` → extract `NoiseConfig`

```csharp
public sealed class NoiseConfig
{
    // Hills (fBm)
    public int HillsOctaves { get; set; } = 5;
    public float HillsPersistence { get; set; } = 0.5f;
    public float HillsLacunarity { get; set; } = 2.0f;
    public float[] HillsAmplitudes { get; set; } = { 20, 12, 26, 4, 5 };
    public float[] HillsWavelengths { get; set; } = { 11000, 5200, 26000, 1600, 1900 };
    
    // Rivers (meander)
    public int RiverMeanderOctaves { get; set; } = 3;
    public float[] RiverMeanderAmps { get; set; } = { 1500, 700, 260 };
    public float[] RiverMeanderWaves { get; set; } = { 9500, 3300, 1250 };
    
    // Rail (swell)
    public float RailSwellBaseAmp { get; set; } = 60f;
    public float RailSwellAmpJitter { get; set; } = 150f;
    public float RailSwellBaseWave { get; set; } = 9000f;
    public float RailSwellWaveJitter { get; set; } = 12000f;
    
    // Town placement jitter
    public float TownSlotJitterFraction { get; set; } = 0.035f;
}
```

#### 1.4 Conditional Parameter Rules
**File:** New `ConfigRules.cs`

```csharp
public sealed class ConditionalRule
{
    public string IfParameter { get; set; } = "";      // e.g. "SmallTown.SizePreset.Tier"
    public string Operator { get; set; } = ">=";       // >=, <=, ==, !=, in, notin
    public string Value { get; set; } = "";            // "2" or "village,town"
    public string ThenParameter { get; set; } = "";    // e.g. "SmallTown.CivicDensity"
    public string ThenOperation { get; set; } = "multiply"; // set, multiply, add, clamp
    public float ThenValue { get; set; } = 1.5f;
}

// Example rules:
// IF Town.Tier >= 2 THEN CivicDensity *= 1.5
// IF World.TownLayout == Both THEN ResidentialDensity *= 1.2
// IF Rural.LotWidth > 250 THEN OutbuildingDensity *= 0.8
```

---

### Phase 2: Biome / Region System (Week 2-3)

#### 2.1 World Region Profiles
**File:** `WorldConfig.cs` → add `RegionConfig`

```csharp
public sealed class RegionConfig
{
    public string Name { get; set; } = "";
    public FloatRange WorldXRange { get; set; } = new(-float.MaxValue, float.MaxValue);
    public FloatRange WorldZRange { get; set; } = new(-float.MaxValue, float.MaxValue);
    
    // Overrides (null = use global)
    public WorldConfig? WorldOverrides { get; set; }
    public SmallTownConfig? TownOverrides { get; set; }
    public RuralConfig? RuralOverrides { get; set; }
    public HouseConfig? HouseOverrides { get; set; }
    
    // Biome-specific
    public string[] BiomeTags { get; set; } = Array.Empty<string>(); // "loess-hills", "driftless", "prairie-pothole"
    public float[] ElevationBiases { get; set; } = Array.Empty<float>(); // Added to hill heights
    public string[] AllowedPlaceNamePools { get; set; } = Array.Empty<string>();
    public string[] ForbiddenBusinessTypes { get; set; } = Array.Empty<string>();
}

// Built-in Iowa regions:
// - "loess-hills" (west): steeper contours, narrower valleys, fewer towns
// - "driftless" (northeast): rugged, no grid roads, organic streets
// - "prairie-pothole" (north-central): many wetlands, no basements
// - "southern-iowa" (south): rolling, more timber, different siding
// - "cedar-valley" (east): river-focused, industrial corridors
```

#### 2.2 Region-Aware Generation
- `WorldMap.Generate` queries region at `(cx, cz)` and merges overrides
- Regions can overlap with priority
- Smooth transitions at boundaries (lerp parameters over transition zone)

---

### Phase 3: Data-Driven Catalogs (Week 3)

#### 3.1 Business / Civic Catalog (External JSON)
**File:** `business-catalog.json`, `civic-catalog.json`, `landmark-catalog.json`

```json
{
  "businesses": [
    { "id": "gas-station", "name": "GAS", "widthRange": [30, 50], "depthRange": [40, 60], "tags": ["highway", "commercial"], "minTownTier": 0, "maxCountPerTown": 2 },
    { "id": "feed-store", "name": "FEED", "widthRange": [40, 70], "depthRange": [60, 100], "tags": ["highway", "agricultural"], "minTownTier": 1 },
    { "id": "co-op", "name": "CO-OP", "widthRange": [60, 100], "depthRange": [80, 150], "tags": ["highway", "agricultural", "rail"], "minTownTier": 1 },
    { "id": "hardware", "name": "HARDWARE", "widthRange": [35, 60], "depthRange": [50, 80], "tags": ["downtown", "commercial"], "minTownTier": 1 },
    { "id": "grain-elevator", "name": "ELEVATOR", "widthRange": [48, 48], "depthRange": [88, 88], "tags": ["rail", "industrial"], "minTownTier": 1, "requiresRail": true }
  ],
  "civic": [
    { "id": "church", "name": "CHURCH", "widthRange": [40, 80], "depthRange": [60, 120], "tags": ["civic"], "minTownTier": 0, "perTierCount": { "0": [0,1], "1": [1,2], "2": [1,2] } },
    { "id": "school", "name": "SCHOOL", "widthRange": [100, 200], "depthRange": [150, 300], "tags": ["civic", "large"], "minTownTier": 1, "chancePerTier": { "0": 0.3, "1": 0.6, "2": 0.95 } }
  ],
  "landmarks": [
    { "id": "water-tower", "name": "WATER TOWER", "tags": ["landmark", "utility"], "chancePerTier": { "0": 0.5, "1": 0.9, "2": 1.0 } },
    { "id": "golf-course", "name": "GOLF", "widthRange": [2000, 4000], "depthRange": [2000, 4000], "tags": ["landmark", "recreation", "large"], "minTownTier": 2 }
  ]
}
```

#### 3.2 Street / Place Name Pools (External)
**File:** `name-pools.json`

```json
{
  "placeNames": { "iowa": [...], "generic": [...] },
  "streetNames": {
    "ew": { "stateHighways": [...], "presidents": [...], "trees": [...] },
    "ns": { "numbers": [...], "trees": [...], "geographic": [...] }
  },
  "businessNames": { "gas": ["CASEY'S", "KUM & GO", "SINCLAIR"], "feed": ["CO-OP", "FARMERS UNION"] }
}
```

---

### Phase 4: Advanced Placement & Topology (Week 4)

#### 4.1 Road Network Topology Parameters
**File:** `WorldConfig.cs` → extend with `RoadNetworkConfig`

```csharp
public sealed class RoadNetworkConfig
{
    public enum Topology { Grid, Organic, Hybrid, Radial, RiverFollowing }
    
    public Topology Topology { get; set; } = Topology.Grid;
    public float OrganicJitterFraction { get; set; } = 0.15f;      // Grid → organic
    public float RiverFollowChance { get; set; } = 0.3f;           // Roads follow rivers
    public float TIntersectionChance { get; set; } = 0.1f;         // vs 4-way
    public float CulDeSacChance { get; set; } = 0.05f;             // Dead ends in grid
    public float RoundaboutChance { get; set; } = 0.0f;            // Not 1990s but param exists
    public float MinBlockSizeFt { get; set; } = 300f;
    public float MaxBlockSizeFt { get; set; } = 800f;
    public FloatRange BlockAspectRatio { get; set; } = new(0.5f, 2.0f);
}
```

#### 4.2 Placement Constraint System
**File:** New `PlacementConstraints.cs`

```csharp
public sealed class PlacementConstraint
{
    public enum Type { MinDistance, MaxDistance, Alignment, Containment, Exclusion, Snapping }
    public Type ConstraintType { get; set; }
    public string TargetTag { get; set; } = "";      // "road", "water", "town-center", "rail"
    public float Value { get; set; } = 0f;           // Distance, angle, etc.
    public float Weight { get; set; } = 1f;          // Soft constraint weight
    public bool Hard { get; set; } = false;          // If true, placement fails if violated
}

// Usage: House placement constraints
// - MinDistance from road: 20ft (hard)
// - Alignment to street: parallel (soft, weight 0.8)
// - Containment in lot: (hard)
// - Exclusion from floodplain: (hard)
// - Snapping to grid: 5ft (soft, weight 0.3)
```

#### 4.3 Connectivity Validation
**File:** `WorldConfig.cs` → add `ConnectivityConfig`

```csharp
public sealed class ConnectivityConfig
{
    public bool RequireRoadAccess { get; set; } = true;           // Every lot touches road
    public bool RequireTownConnectivity { get; set; } = true;     // Town streets form connected graph
    public bool RequireRailConnectivity { get; set; } = false;    // Towns on rail have spur
    public bool RequireRiverCrossings { get; set; } = true;       // Roads crossing rivers get bridges
    public float MaxDeadEndLengthFt { get; set; } = 500f;         // Max cul-de-sac
    public bool ValidateOnGenerate { get; set; } = false;         // Slow but catches bugs
}
```

---

### Phase 5: Developer Experience (Week 5)

#### 5.1 Parameter Explorer UI (Web-based)
**File:** `out/param-explorer.html` (served by `--serve`)

Features:
- Tree view of all config sections
- Slider/inputs for each parameter with min/max/step
- Live preview (small SVG, ~300px) regenerates on change
- "Diff from default" view
- Export/import JSON
- Profile selector
- Sensitivity heatmap (run N seeds, measure output variance per param)

#### 5.2 Parameter Sensitivity Analysis CLI
**Command:** `--analyze-sensitivity --param SmallTown.ResidentialDensity --range 0.5,2.0 --steps 10 --seeds 20`

Output:
```
Parameter: SmallTown.ResidentialDensity
Range: 0.5 → 2.0 (10 steps)
Seeds per step: 20

Density  | Houses (μ±σ) | Blocks (μ±σ) | Vacancy (μ±σ)
---------|--------------|--------------|--------------
0.50     |  42 ± 8      |  12 ± 2      |  0.28 ± 0.05
0.65     |  58 ± 9      |  14 ± 2      |  0.19 ± 0.04
0.80     |  71 ± 10     |  16 ± 2      |  0.13 ± 0.03
1.00     |  89 ± 11     |  18 ± 3      |  0.08 ± 0.02
1.25     | 104 ± 12     |  20 ± 3      |  0.05 ± 0.02
1.50     | 118 ± 13     |  22 ± 3      |  0.03 ± 0.01
2.00     | 132 ± 14     |  24 ± 3      |  0.01 ± 0.01
```

#### 5.3 Config Validation & Migration
- `SpawnConfig.Validate()` already exists — extend with warnings
- Schema version migration: `MigrateFrom(v1) → v2`
- `--validate-config` CLI flag

---

### Phase 6: Advanced Features (Future)

#### 6.1 Temporal Evolution (Timelapse)
- Parameters for "year" → generator evolves town over decades
- `YearConfig` with per-year deltas: `HouseDensity += 0.02/yr`, `CommercialDensity += 0.01/yr`

#### 6.2 Multi-Scale LOD Parameters
- Separate params for: region map (1:100k), town map (1:5k), lot map (1:500), house plan (1:50)
- Automatic parameter selection by camera zoom

#### 6.3 Inverse Design / Optimization
- Target: "town with 500 houses, 2 gas stations, 1 school"
- Optimizer adjusts parameters to hit targets (Bayesian optimization, CMA-ES)

#### 6.4 Style Transfer / Analogues
- "Make this town look like George, IA" → parameter fitting from reference SVGs

---

## 5. Implementation Checklist

### Phase 1: Core Infrastructure
- [ ] 1.1 `ConfigProfile` class + built-in profiles
- [ ] 1.1 CLI: `--profile`, `--list-profiles`, `--save-profile`
- [ ] 1.2 `DistributionShape` enum + `FloatDistribution` struct
- [ ] 1.2 Migrate key `FloatRange` → `FloatDistribution` (tree spacing, lot frontage, building dims)
- [ ] 1.3 `NoiseConfig` extracted from `WorldConfig`
- [ ] 1.3 Hills: octaves, persistence, lacunarity, amplitude/wavelength arrays
- [ ] 1.3 Rivers: meander octaves, amplitude/wavelength arrays
- [ ] 1.3 Rail: swell parameterization
- [ ] 1.4 `ConditionalRule` system
- [ ] 1.4 Rule engine: evaluate after config load, before generation
- [ ] 1.4 Built-in rules for common correlations

### Phase 2: Biome / Region System
- [ ] 2.1 `RegionConfig` class
- [ ] 2.1 Built-in Iowa regions (5)
- [ ] 2.1 Region override merging logic
- [ ] 2.2 Smooth boundary transitions (lerp zone)
- [ ] 2.2 Region query in `WorldMap.Generate`

### Phase 3: Data-Driven Catalogs
- [ ] 3.1 `business-catalog.json`, `civic-catalog.json`, `landmark-catalog.json`
- [ ] 3.1 Loaders + validation
- [ ] 3.1 `BusinessCatalogue.cs` refactored to use catalog
- [ ] 3.2 `name-pools.json` + loader
- [ ] 3.2 `SmallTownGenerator` uses external name pools

### Phase 4: Advanced Placement & Topology
- [ ] 4.1 `RoadNetworkConfig.Topology` enum + params
- [ ] 4.1 Grid → organic interpolation in `WorldMap.AddRoads`
- [ ] 4.2 `PlacementConstraint` system
- [ ] 4.2 House/lot/outbuilding placement uses constraints
- [ ] 4.3 `ConnectivityConfig` + validation pass

### Phase 5: Developer Experience
- [ ] 5.1 `out/param-explorer.html` (static, no server deps)
- [ ] 5.1 Integrated into `--serve` at `/params`
- [ ] 5.2 `--analyze-sensitivity` CLI command
- [ ] 5.3 Config migration framework + `--validate-config`

### Phase 6: Advanced (Deferred)
- [ ] 6.1 Temporal evolution parameters
- [ ] 6.2 Multi-scale LOD parameter sets
- [ ] 6.3 Inverse design optimizer
- [ ] 6.4 Style transfer from reference images

---

## 6. Parameter Inventory — Complete Reference

### 6.1 HouseConfig (~25 params)
| Parameter | Type | Current | Proposed Enhancement |
|-----------|------|---------|---------------------|
| RanchWidth | FloatRange | [36,48] | FloatDistribution (Normal, μ=42, σ=4) |
| RanchDepth | FloatRange | [24,32] | FloatDistribution |
| RanchGarageWidth | FloatRange | [12,19] | FloatDistribution |
| RanchGarageDepth | FloatRange | [17,24] | FloatDistribution |
| RanchGarageLeftChance | float | 0.5 | — |
| FarmhouseWidth | FloatRange | [28,36] | FloatDistribution |
| FarmhouseDepth | FloatRange | [30,38] | FloatDistribution |
| FarmhousePorchWidthFraction | FloatRange | [0.55,0.75] | FloatDistribution (Beta, α=2, β=2) |
| FarmhousePorchDepth | FloatRange | [6,8] | FloatDistribution |
| FarmhouseDoorCenteredChance | float | 1.0 | — |
| SplitLevelWidth | FloatRange | [28,36] | FloatDistribution |
| SplitLevelDepth | FloatRange | [28,36] | FloatDistribution |
| SplitLevelGarageWidth | FloatRange | [12,18] | FloatDistribution |
| SplitLevelGarageDepth | FloatRange | [16,22] | FloatDistribution |
| SplitLevelGarageLeftChance | float | 0.5 | — |
| SplitLevelGarageDoorOnSideChance | float | 1.0 | — |
| SidingOptions | string[] | 7 options | External catalog |
| DoorOffsetFraction | FloatRange | [0.18,0.32] | FloatDistribution |
| FrontWindowJitterFraction | float | 0.06 | — |
| SideWindowJitterFraction | float | 0.05 | — |
| GarageDoorWidth16ftChance | float | 0.35 | — |
| GarageDoorMinWidth | float | 9 | — |
| GarageDoorMaxWidth | float | 16 | — |
| TwoWindowMinWall | float | 22 | — |
| OneWindowMinWall | float | 9 | — |

### 6.2 RuralConfig (~30 params)
| Parameter | Type | Current | Proposed Enhancement |
|-----------|------|---------|---------------------|
| LotWidth | FloatRange | [215,285] | FloatDistribution (LogNormal) |
| LotDepth | FloatRange | [300,420] | FloatDistribution |
| HouseXFraction | FloatRange | [0.22,0.38] | FloatDistribution (Beta) |
| HouseSetback | FloatRange | [38,62] | FloatDistribution |
| MachineShedWidthOptions | float[] | [36,40,48] | Distribution-weighted picks |
| MachineShedDepthOptions | float[] | [30,36,40] | Distribution-weighted picks |
| MachineShedXFractionRight | FloatRange | [0.06,0.18] | FloatDistribution |
| MachineShedXFractionLeft | FloatRange | [0.08,0.20] | FloatDistribution |
| MachineShedBehindHouse | float | 26 | FloatRange |
| DrivewayEntrySideMargin | float | 15 | — |
| DrivewayYardDepth | float | 20 | FloatRange |
| DrivewayWidth | float | 12 | FloatRange |
| DrivewaySurface | string | "gravel" | Enum: gravel|concrete|dirt|asphalt |
| WalkWidth | float | 3.5 | FloatRange |
| WalkSurface | string | "concrete" | Enum |
| WalkOffsetFromLane | float | 7 | FloatRange |
| ShopChance | float | 0.4 | Conditional on lot size |
| ShopWidth | float | 22 | FloatRange |
| ShopDepth | float | 28 | FloatRange |
| ShopOffsetFromShed | float | 10 | FloatRange |
| BinCount | IntRange | [2,5] | Poisson (λ=3) |
| BinDiameterOptions | float[] | [18,21,24] | Distribution-weighted |
| BinSpacingExtra | float | 8 | FloatRange |
| BinRowBehindShed | float | 8 | FloatRange |
| PoleBarnChance | float | 0.45 | Conditional |
| PoleBarnWidth | float | 28 | FloatRange |
| PoleBarnDepth | float | 44 | FloatRange |
| PoleBarnBehindBins | float | 12 | FloatRange |
| PoleBarnXJitter | float | 30 | FloatRange |
| GambrelBarnChance | float | 0.2 | — |
| GambrelBarnWidth | float | 32 | FloatRange |
| GambrelBarnDepth | float | 54 | FloatRange |
| GambrelBarnXFraction | FloatRange | [0.60,0.80] | FloatDistribution |
| GambrelBarnFromBack | float | 14 | FloatRange |
| LpTankChance | float | 0.3 | — |
| LpTankOffsetFromHouse | float | 8 | FloatRange |
| ShelterbeltChance | float | 0.55 | Conditional on lot size |
| ShelterbeltTreeSpacing | float | 15 | FloatRange |
| ShelterbeltTreeJitter | float | 2 | FloatRange |
| ShelterbeltNorthOffset | float | 8 | FloatRange |
| ShelterbeltWestOffset | float | 10 | FloatRange |
| ShelterbeltTreeSpacingZ | float | 17 | FloatRange |
| DetachedGarageWidth | float | 22 | FloatRange |
| DetachedGarageDepth | float | 24 | FloatRange |
| DetachedGarageSideOffset | float | 16 | FloatRange |
| DetachedGarageBehindHouse | float | 4 | FloatRange |
| OutbuildingDensity | float | 1.0 | Global multiplier |
| BuildingCollisionPad | float | 3 | FloatRange |

### 6.3 TownBlockConfig (~28 params)
| Parameter | Type | Current | Proposed Enhancement |
|-----------|------|---------|---------------------|
| StreetRow | float | 36 | FloatRange |
| AlleyRow | float | 16 | FloatRange |
| LotDepth | FloatRange | [120,140] | FloatDistribution |
| AlleyChance | float | 0.35 | Conditional on block depth |
| LotCountPerSide | int | 4 | IntRange [3,6] |
| LotFrontage | FloatRange | [72,88] | FloatDistribution (Normal) |
| Setback | FloatRange | [10,22] | FloatDistribution |
| HouseXJitter | float | 3 | FloatRange |
| GarageViaAlleyChance | float | 0.6 | — |
| DetachedGarageWidth | float | 22 | FloatRange |
| DetachedGarageDepth | float | 26 | FloatRange |
| DetachedGarageXJitter | float | 6 | FloatRange |
| DetachedGarageFromAlley | float | 9 | FloatRange |
| FarmhouseDetachedGarageWidth | float | 20 | FloatRange |
| FarmhouseDetachedGarageDepth | float | 22 | FloatRange |
| FarmhouseDetachedGarageSideMargin | float | 3 | FloatRange |
| FarmhouseDetachedGarageDepthFraction | float | 0.25 | FloatRange |
| DrivewayWidth | float | 17 | FloatRange |
| DrivewaySurface | string | "concrete" | Enum |
| DrivewayStartOffset | float | 31 | FloatRange |
| AlleyDrivewayWidth | float | 10 | FloatRange |
| AlleyDrivewaySurface | string | "gravel" | Enum |
| WalkWidth | float | 4 | FloatRange |
| WalkSurface | string | "concrete" | Enum |
| WalkStartInset | float | 4.5 | FloatRange |
| StreetTreeSpacing | FloatRange | [28,46] | FloatDistribution (Poisson) |
| StreetTreeChance | float | 0.75 | — |
| StreetTreeSidewalkOffset | float | 7 | FloatRange |
| YardTreeCount | IntRange | [2,4] | Poisson |
| YardTreePad | float | 5 | FloatRange |
| YardTreeSideMargin | float | 4 | FloatRange |
| HouseDensity | float | 1.0 | Global multiplier |
| StreetNamePool | string[] | 8 names | External catalog |
| StreetSuffixStreetChance | float | 0.7 | — |

### 6.4 SmallTownConfig (~100 params)
**Core Grid (12)**
- HighwayWidth, StreetRow, AlleyRow, BlockLotDepth, SideLotDepth, BandDepth
- FourSideBlockChance, LotDepthJitter, LotFrontage

**Size Presets (5 tiers × 7 params = 35)**
- Columns, EWStreets, LotsMin, LotsMax, CommercialPads, CivicAnchors, DeadEnds

**Town Extras (10)**
- 7 extra chances, MaxExtrasPerTown, ExtraOneChance, ExtraNoneChance

**Row Alley (1)**
- RowAlleyChance

**Place/Street Names (3 arrays)**
- PlaceNames, EWStreetNamePool, NSStreetNamePool

**Town Silhouette (7)**
- BlobHarmonic1/2, BlobAmplitude1/2, BlobPhase, BlobRadiusMultiplier, FreeStreetBlobPadding

**Vacancy (7)**
- VacantOuter/Mid/Core Chance + Radius, RadialPlatVacantChance

**Commercial Strip (8)**
- CommercialStartX, CommercialGap, CommercialMainGapMargin, CommercialBandOccMargin
- IndustrialBoxCountChance, CrossStreetShopMax, CrossStreetShopDepth/Width

**Elevator & Bins (9)**
- ElevatorWidth/Depth, ElevatorRightChance, ElevatorOffsetFromMain
- ElevatorBinCount, BinDiameter, BinSpacing, BinRowBehindElevator

**Rail Industrial (3)**
- RailIndustrialRowSpacing, RailIndustrialDepth, RailIndustrialZJitter

**South Fringe (6)**
- SouthBandDepth, SouthHouseCount, SouthHouseWidth, SouthHouseSpacing, SouthIndustrialChance

**Civic Anchors (9)**
- ChurchCountTier0/1/2Plus, SchoolChanceTier0/1/2Plus, ParkChanceTier0/1/2Plus

**Residential Lots (25)**
- LotSetback, GarageModeIntegral/Detached/NoneChance
- HouseFrontageNarrowThreshold, HousePlacementJitter, HouseFitPad, HouseSetbackMin
- ShedChance, ShedWidth/Depth, ShedSide/BackMargin
- DetachedGarageViaAlleyChance, DetachedGarageWidth/Depth/XJitter/FromAlley
- FarmhouseDetachedGarageWidth/Depth/SideMargin/ZRange

**Driveways & Walks (8)**
- DrivewayWidth/Surface/StartOffset, AlleyDrivewayWidth/Surface
- WalkWidth/Surface/SidewalkInset

**Free Streets (6)**
- RailCorridorStreetChance, CurvedStreetChance, RailCorridorEraseHalf, CurvedStreetEraseHalf

**Plat & Build Pass (14)**
- PlatLotFrontage, PlatVacantChance, PlatSetback, PlatHouseJitter
- PlatGarageIntegral/Detached/NoneChance
- PlatDetachedGarageWidth/Depth/XJitter/FromAlley

**Cul-de-sac Court (7)**
- CourtXRange, CourtLen, CourtHouseCount, CourtHouseFrontage, CourtHouseSetback, CourtHouseJitter

**Plat Edge Street (3)**
- PlatEdgeXRange, PlatEdgeLen, PlatEdgeLotFrontage

**Outlying Road (5)**
- OutlyingRoadZOffset, OutlyingRoadHouseCount, OutlyingRoadHouseFrontage/Depth/Spacing

**Bypassed Highway (3)**
- BypassedHighwayXRange, BypassedHighwayShopCount, BypassedHighwayShopFrontage

**Boulevard (1)**
- BoulevardMedianWidth

**Perimeter Loop (6)**
- PerimeterGapN, PerimeterJogHeight, DeadEndStubXJitter, DeadEndStubLen, DeadEndBulbRadius

**Density Multipliers (5)**
- ResidentialDensity, CommercialDensity, IndustrialDensity, CivicDensity, LandmarkDensity

### 6.5 WorldConfig (~65 params)
**Fundamental (4)**
- MileFeet, SectionFeet, CellFeet, MinSpan

**Hills (11)**
- ContourInterval, ContourLatticeCell
- ElevationWavelength1-5, ElevationAmplitude1-5

**Rivers (17)**
- TrunkRiverSpacing, TributarySpacing, TrunkRiverBaseJitter
- TrunkRiverWidthBase, WidthAmp1/2, WidthWave1/2
- TrunkRiverMeanderAmp1/2/3, MeanderWave1/2/3
- TributaryLengthBase/Jitter/HeadJitter, TributaryGapFromTrunk/Jitter
- TributaryBowAmplitude, TributaryWidth
- RiverBankTreeChance/Offset, TributaryBankTreeChance/Offset
- RiverVetoDistance

**Rail (10)**
- RailSpacing, RailPhaseSections, RailOffsetBase/Jitter
- RailSwellAmplitudeBase/Jitter, RailSwellWavelengthBase/Jitter
- RailSpurMaxDistance, RailSpurOffsetBase/Jitter, RailSpurSideChance

**Roads (7)**
- PavedSpacing, TownSlotFeet, MinTownSpacingFeet
- HighwayWidth, RoadHalfPaved, RoadHalfGravel
- RoadLineAbsentChance, RoadLinePavedChance, FarmMargin

**Towns (8)**
- TownRailProximity, TownLayoutNorth/South/BothChance
- TownClassCrossroads/Hamlet/Village/TownChance
- TownSlotJitterFraction, TownDensity

**Farms (4)**
- FarmsPerSectionMax, FarmSectionJitter, FarmAlongJitter, FarmDensity

**Field Entrances (6)**
- FieldEntrancePerQuarterChance, FieldEntranceMaxPerQuarter
- FieldEntranceWidth, FieldEntranceLength, FieldEntranceSetbackFromIntersection
- FieldEntranceSurface

**Highway Names (1 array)**
- HighwayNames

### 6.6 CountrysideConfig (~20 params)
**Core (4)**
- CellFeet, SectionRoadWidth, HighwayWidth, SectionMid

**Town Placement (2)**
- TownXJitter, TownZJitter

**Farmsteads (8)**
- FarmsteadCount, FarmsteadXRange, FarmsteadZRange
- FarmsteadHouseRingCount/Radius/AngleJitter
- FarmsteadGroveCount/Radius/AngleJitter

**Fields (1)**
- FieldPalette (7 colors)

**Density (2)**
- FarmsteadDensity, FieldFeatureDensity

### 6.7 RenderConfig (~35 params)
**House SVG (3)**
- HouseWallScreenPx, HouseGapScreenPxExtra, HouseThinScreenPxFactor

**Map SVG (5)**
- MapTargetWidthPx, MapMaxHeightPx, MapMarginPx, MapTitleHeightPx, MapFooterHeightPx

**LOD (3)**
- LodSimpleTreeRadiusPx, LodDetailTreeRadiusPxFactor, LodRailTieMinScale

**Colors (21)**
- Background, Floor, Wall, Gap, Window, Door, DoorSwing, Dash
- Text, SmallText, RoadAsphalt, RoadGravel, RoadEdgeGravel
- Centerline, Alley, DrivewayConcrete/Gravel/Dirt, Walk
- Water, WaterEdge, RailBallast, RailRail, RailTie, Tree
- ContourIndex, ContourMinor, Median, MedianEdge

**Viewer (3)**
- ViewerMaxZoom, ViewerNiceSpanLadder (11 values), ViewerDebounceMs, ViewerCacheSize

---

## 7. Migration Strategy

### 7.1 Backward Compatibility
- `SpawnConfig.Load()` handles missing fields gracefully (uses defaults)
- SchemaVersion in config file enables migration
- `FloatRange` ↔ `FloatDistribution` auto-conversion:
  - `[min, max]` → Uniform distribution
  - Single float → Uniform [value, value]

### 7.2 Config File Versioning
```json
{
  "SchemaVersion": 2,
  "Houses": { ... },
  "Profiles": {
    "my-custom": { "Name": "my-custom", "Config": { ... } }
  }
}
```

### 7.3 CLI Migration Commands
```
dotnet run -- --migrate-config spawnconfig.json        # Upgrade to latest schema
dotnet run -- --validate-config spawnconfig.json       # Check validity
dotnet run -- --diff-config old.json new.json          # Show differences
```

---

## 8. Testing Strategy

### 8.1 Parameter Sensitivity Tests
- For each major parameter: sweep range, measure output metrics
- Detect: non-monotonic responses, cliffs, dead zones
- Automated in CI: `--test-sensitivity`

### 8.2 Visual Regression Tests
- Golden master SVGs for known seeds + configs
- Pixel-diff on parameter changes
- `--test-visual-regression`

### 8.3 Determinism Tests
- Same seed + same config = identical output (byte-for-byte)
- Cross-platform: Windows/Linux/macOS
- `--verify` already covers this

### 8.4 Edge Case Tests
- Min/max parameter values
- Zero densities
- Extreme aspect ratios
- `--test-edge-cases`

---

## 9. Documentation Plan

### 9.1 Parameter Reference (Auto-generated)
- Tool: `dotnet run -- --doc-params > PARAMS.md`
- Includes: type, default, range, description, affects-what

### 9.2 Profile Gallery
- `out/profile-gallery.html` — side-by-side comparison of all profiles

### 9.3 Tuning Guide
- `docs/tuning-guide.md` — "How to get X look"
- Common recipes: "denser towns", "more farms", "wider roads", "modern look"

### 9.4 API Docs
- XML docs on all config classes → DocFX / Sandcastle

---

## 10. Effort Estimate

| Phase | Tasks | Est. Days | Dependencies |
|-------|-------|-----------|--------------|
| 1. Core Infrastructure | 1.1-1.4 | 8 | None |
| 2. Biome/Region System | 2.1-2.2 | 5 | Phase 1 |
| 3. Data-Driven Catalogs | 3.1-3.2 | 4 | Phase 1 |
| 4. Advanced Placement | 4.1-4.3 | 6 | Phase 1 |
| 5. Developer Experience | 5.1-5.3 | 5 | Phase 1 |
| **Total** | | **28 days** | |

### Quick Wins (Can do in parallel, 1-2 days each)
- Config profiles (1.1)
- Distribution shapes (1.2) — high impact, low effort
- External catalogs (3.1) — removes hardcoded arrays
- Parameter explorer UI (5.1) — huge DX improvement

---

## 11. Appendix: Related Research Links

- [PCG Taxonomy (Togelius et al., 2011)](https://doi.org/10.1109/TCIAIG.2011.2145616)
- [Search-Based PCG Survey (Yannakakis & Togelius, 2018)](https://link.springer.com/book/10.1007/978-3-319-65990-5)
- [No Man's Sky Generation (Murray, 2016)](https://www.gdcvault.com/play/1023832/No-Man-s-Sky-How)
- [Dwarf Fortress World Gen (Adams, various)](http://www.bay12games.com/dwarves/)
- [Minecraft World Gen (Mojang, 2021)](https://minecraft.fandom.com/wiki/World_generation)
- [Unity PCG Package (2023)](https://docs.unity3d.com/Packages/com.unity.pcg@1.0/manual/index.html)
- [Houdini PCG Nodes (SideFX, 2024)](https://www.sidefx.com/docs/houdini/pcg/index.html)

---

*Generated for the SpawnGuide procedural generation system. Last updated: 2025.*