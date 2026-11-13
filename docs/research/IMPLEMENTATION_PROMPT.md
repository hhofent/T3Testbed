# Implementation Prompt: Procedural Generation Tuning System for SpawnGuide

## Project Context

**Repository**: `C:/Users/hhofe/Documents/grokkable projects/t3 testbed`
**System**: SpawnGuide — Seeded, engine-agnostic C# generator for 1990s rural Iowa procedural generation (houses, farmsteads, towns, infinite world)
**Current State**: ~300 parameters across 7 config classes (`HouseConfig`, `RuralConfig`, `TownBlockConfig`, `SmallTownConfig`, `WorldConfig`, `CountrysideConfig`, `RenderConfig`) in `SpawnConfig.cs`
**Goal**: Build a comprehensive parameter tuning system with distributions, curves, expressions, enums, validation, profiles, conditional rules, developer tooling, and expanded realism

---

## Key Documents (Read First)

| File | Purpose |
|------|---------|
| `docs/research/GRILLED_DATA.md` | Complete Q&A log (5 rounds, 30+ decisions) — **authoritative source** |
| `docs/research/GRILL_INDEX.json` | Searchable index of grilled data |
| `docs/research/procgen-tuning-parameters.md` | Research background & full parameter inventory |
| `docs/research/PROCGEN_TUNING_CHECKLIST.md` | Implementation checklist (update with Phase 1/2 split) |
| `src/SpawnGuide/SpawnConfig.cs` | Current config classes (modify in place) |
| `src/SpawnGuide/Rng.cs` | Mulberry32 RNG (deterministic, keep) |
| `src/SpawnGuide/WorldMap.cs` | World generation pipeline (refactor for NoiseConfig) |
| `src/SpawnGuide/SmallTown.cs` | Town generation (refactor for tier system, feature grammar) |
| `src/SpawnGuide/CountrysideMap.cs` | 3-mile cell views |
| `src/SpawnGuide/Program.cs` | CLI entry point (add new commands) |

---

## Grilling Decisions Summary (5 Rounds)

### Round 1: Philosophy & Constraints (G-001)
- **Priority**: Gameplay utility > Infinite variety > Performance > Visual authenticity > Controllability
- **Determinism**: Byte-for-byte reproducibility required (mulberry32 already provides)
- **Scope OUT**: Narrative, vehicle traffic, crop simulation, seasons
- **References**: Generic archetypes, not specific real towns
- **Platform**: Windows only

### Round 2: Parameter Architecture (G-002)
- **Inheritance**: Hybrid — Merge (RegionConfig), Delta (runtime sliders), Full configs (Profiles)
- **New Parameter Types** (all four, no conflicts):
  - `FloatDistribution` — Uniform, Normal, LogNormal, Poisson, Beta, Triangular, Exponential
  - `AnimationCurve` — Keyframe curves for distance falloffs
  - `Expression` — Simple math strings (`min(10, max(5, x*1.2))`)
  - `Enum` — Discrete choices (RoadTopology, GarageMode, SurfaceType, SidingType)
- **Validation**: Hard (range/semantic), Soft (deprecated/perf), Repair (min>max swap, empty array)
- **Serialization**: JSON primary (comments, trailing commas) + binary cache (.config.bin) + hot-reload (500ms debounce) + SchemaVersion auto-migration
- **Grouping**: Dual — by System (current classes) AND by Concern (Density, Placement, Visual, Noise, Distribution) via `CategoryAttribute`
- **Correlations**: `ConditionalRule` engine — `If: "Town.Tier >= 2" Then: "CivicDensity *= 1.5"` with set/multiply/add/clamp

### Round 3: Noise & Natural Systems (G-003, G-003-CONFIRM)
- **9 New Pipeline Stages** (geological→historical):
  1. Geology/Glacial Regions → 2. Soils → 3. Hills → 4. Wetlands/Floodplains → 5. Rivers → 6. Native Vegetation → 7. Historical Roads → 8. Rail → 9. Towns → 10. PLSS → 11. Highways → 12. Farmsteads → 13. Shelterbelts → 14. Infrastructure → 15. 1990s Snapshot
- **Only 3 Deviations from Reality**: (1) 1990s era, (2) Uniform NW Iowa low density (0.15 farms/section), (3) City size cap (smallest Iowa cities)
- **Noise Models**:
  - Hills: fBm (octaves=6, persistence=0.45, lacunarity=2.1, base wavelength=12000ft, amplitude=35ft) + optional domain warp
  - Rivers: LogNormal trunk spacing (μ=28mi, σ=6mi), Perlin meander (4 octaves, persistence=0.5), Strahler-order tributaries
  - Rail: Swell as fBm, spurs perpendicular with elevator, 5.5mi max distance
  - Roads: 10mi paved spacing, 1.8% absent/2.2% paved exceptions, 300ft farm margin
  - Towns: 9mi rail proximity, Dirichlet layout/class weights, slot jitter
  - Farms: 0.15 density, Normal perp jitter, LogNormal along jitter, max 3/section

### Round 4: Human Systems (G-004)
- **Town Tiers**: Generative k=3 Central Place Theory (Crossroads, Hamlet, Village, Town) — **remove Boyden/Hull**
- **Town Extras**: Feature grammar with prerequisites/exclusions/placement/weights (0-2 emergent)
- **Commercial**: Highway commercial (weighted business types) vs Rail industrial (grain elevators, ag services) — **rails = crops only**
- **Civic/Landmarks**: Threshold-based with per-capita church scaling, consolidated schools (1990s)
- **Residential**: 1990s standards — 25ft front/7.5ft side/25ft rear setbacks; Garage modes Dirichlet (55/35/10); Concrete drives/walks; Poisson yard trees
- **Streets**: Iowa 66ft ROW, 32ft paved, 16ft gravel alleys (Beta clustered); Perimeter loop with north jog; 70% 4-side blocks
- **Farmsteads**: Evolution model (homestead→1990s); PSFP shelterbelts (mature by 1990s, 3-7 rows N/W); Machine sheds for 1990s equipment
- **Houses**: LogNormal dimensions, Beta fractions, Bimodal garage doors (9ft/16ft), 1990s siding market
- **New Archetypes**: Coop Corner (section-line corner, rail-required) + Trailer Court (town fringe, curved street, 40×70 pads)

### Round 5: Developer Experience & Implementation (G-005)
- **Phase 1 (Core — 16 items)**:
  1. Parameter Infrastructure (Distributions, Curves, Expressions, Enums, Validation, Serialization, CategoryAttribute)
  2. Config Profiles (8 built-in: default, dense-suburban, sparse-frontier, modern-iowa, railroad-town, river-valley, testing-minimal, testing-max)
  3. NoiseConfig extraction (fBm hills, Perlin rivers, rail swell)
  4. Generative Town Tiers (k=3, Dirichlet layout/class)
  5. Feature Grammar for Extras
  6. Highway/Rail Commercial Split
  7. 1990s Residential Standards
  8. Iowa Standard Streets
  9. Farm Evolution Model
  10. House Archetype Distributions
  11. ConditionalRule Engine
  12. Coop Corner Archetype
  13. Parameter Explorer UI (`/params` endpoint)
  14. Sensitivity Analysis CLI (`--analyze-sensitivity`)
  15. Hot-reload + Binary Cache
  16. Config Migration + Diff Tool
- **Phase 2 (2 items)**: Trailer Court Archetype, Region/Geology/Soils Pipeline (9 new stages)

---

## Technical Specifications

### 1. Parameter Infrastructure (Phase 1, Item 1)

**New Files**:
- `src/SpawnGuide/Distributions.cs` — `DistributionShape` enum, `FloatDistribution` struct, `IntDistribution` struct
- `src/SpawnGuide/Curves.cs` — `AnimationCurve` struct, `Keyframe` struct, serialization
- `src/SpawnGuide/Expressions.cs` — `Expression` class (parse/evaluate simple math)
- `src/SpawnGuide/Enums.cs` — `SurfaceType`, `GarageMode`, `RoadTopology`, `SidingType`, `RoadSurface`, `TownLayout`, `TownClass`
- `src/SpawnGuide/Validation.cs` — `ValidationResult`, `ValidationSeverity` (Error/Warning/Repair), `ConfigValidator`
- `src/SpawnGuide/Attributes.cs` — `CategoryAttribute`, `RationaleAttribute`, `DistributionAttribute`
- `src/SpawnGuide/Serialization.cs` — Binary cache, hot-reload, migration framework

**Modify**: `SpawnConfig.cs` — Replace `FloatRange`/`IntRange` with `FloatDistribution`/`IntDistribution` for key params; add `CategoryAttribute`/`RationaleAttribute` to all fields; add `SchemaVersion = 2`; implement `Migrate(fromVersion)`

### 2. Config Profiles (Phase 1, Item 2)

**New File**: `src/SpawnGuide/ConfigProfiles.cs` — `ConfigProfile` class, built-in profiles dictionary

**Profiles**:
| Profile | Description | Key Overrides |
|---------|-------------|---------------|
| `default` | 1990s rural Iowa baseline | — |
| `dense-suburban` | Smaller lots, more houses | `ResidentialDensity=1.5`, `LotFrontage μ=4.0`, `Setback μ=15` |
| `sparse-frontier` | Larger lots, fewer towns | `ResidentialDensity=0.5`, `TownDensity=0.3`, `FarmDensity=0.8` |
| `modern-iowa` | 2020s: larger houses, attached garages | `HouseArchetypeWeights=[0.7,0.2,0.1]`, `GarageModeWeights=[0.7,0.2,0.1]`, `SidingOptions` modern |
| `railroad-town` | Towns forced on rail, heavy industrial | `TownRailProximityMi=2`, `RailIndustrialDensity=2.0` |
| `river-valley` | More tributaries, floodplain farms | `TributarySpacingMi=4`, `FarmDensity=0.1`, `WetlandDensity=2.0` |
| `testing-minimal` | Tiny world, few features, fast | All densities=0.1, `ViewerMaxZoom=50` |
| `testing-max` | Everything maxed, stress test | All densities=3.0, `MaxExtrasPerTown=5` |

**CLI**: `--profile <name>`, `--list-profiles`, `--save-profile <name>`

### 3. NoiseConfig Extraction (Phase 1, Item 3)

**New File**: `src/SpawnGuide/NoiseConfig.cs` — Extract all noise params from `WorldConfig`

```csharp
public sealed class NoiseConfig
{
    // Hills (fBm)
    public int HillsOctaves { get; set; } = 6;
    public float HillsPersistence { get; set; } = 0.45f;
    public float HillsLacunarity { get; set; } = 2.1f;
    public float HillsBaseWavelengthFt { get; set; } = 12000f;
    public float HillsAmplitudeFt { get; set; } = 35f;
    public float HillsDomainWarpAmp { get; set; } = 800f;
    public int HillsDomainWarpOctaves { get; set; } = 3;

    // Contours
    public float ContourIntervalFt { get; set; } = 20f;
    public float ContourLatticeCellFt { get; set; } = 1320f;
    public int ContourIndexEvery { get; set; } = 5;

    // Rivers
    public FloatDistribution TrunkRiverSpacingMi { get; set; } = LogNormal(28, 6);
    public int RiverMeanderOctaves { get; set; } = 4;
    public float RiverMeanderBaseAmpFt { get; set; } = 1200f;
    public float RiverMeanderBaseWavelengthFt { get; set; } = 8000f;
    public float RiverMeanderPersistence { get; set; } = 0.5f;
    public float RiverMeanderLacunarity { get; set; } = 2.0f;
    public float RiverMeanderAmpPerWidth { get; set; } = 15f;
    // ... tributary params

    // Rail
    public float RailSpacingMi { get; set; } = 75f;
    public float RailPhaseSections { get; set; } = 3f;
    public float RailOffsetBaseFt { get; set; } = 40f;
    public float RailOffsetJitterFt { get; set; } = 170f;
    public float RailSwellBaseAmpFt { get; set; } = 50f;
    public float RailSwellAmpJitterFt { get; set; } = 150f;
    public float RailSwellBaseWavelengthFt { get; set; } = 8000f;
    public float RailSwellWavelengthJitterFt { get; set; } = 10000f;
    public float RailSpurMaxDistanceMi { get; set; } = 5.5f;
    public float RailSpurOffsetBaseFt { get; set; } = 100f;
    public float RailSpurOffsetJitterFt { get; set; } = 180f;
    public float RailSpurSideChance { get; set; } = 0.5f;
    public bool RailSpurAlwaysPerpendicular { get; set; } = true;
    public bool RailSpurAlwaysHasElevator { get; set; } = true;

    // Roads
    public float PavedSpacingMi { get; set; } = 10f;
    public float RoadLineAbsentChance { get; set; } = 0.018f;
    public float RoadLinePavedChance { get; set; } = 0.022f;
    public float FarmMarginFt { get; set; } = 300f;

    // Towns
    public float TownRailProximityMi { get; set; } = 9f;
    public FloatDistribution TownLayoutWeights { get; set; } = Dirichlet([0.46, 0.24, 0.30]);
    public FloatDistribution TownClassWeights { get; set; } = Dirichlet([0.315, 0.185, 0.09, 0.045]);
    public float TownSlotJitterFraction { get; set; } = 0.035f;

    // Farms
    public int FarmsPerSectionMax { get; set; } = 3;
    public FloatDistribution FarmSectionJitterFt { get; set; } = Normal(0, 250);
    public FloatDistribution FarmAlongJitterFt { get; set; } = LogNormal(200, 300);
    public float FarmDensity { get; set; } = 0.15f;
}
```

**Modify**: `WorldMap.cs` — Use `NoiseConfig` instead of hardcoded values; add `NoiseConfig?` parameter to `Generate()`

### 4. Generative Town Tiers (Phase 1, Item 4)

**New File**: `src/SpawnGuide/TownTierSystem.cs`

```csharp
public sealed class TownTierSystem
{
    public int KValue { get; set; } = 3;  // 3=marketing, 4=transport, 7=admin
    public int MaxTier { get; set; } = 3; // 0=Crossroads, 1=Hamlet, 2=Village, 3=Town
    
    public FloatDistribution BaseColumns { get; set; } = LogNormal(1.5, 0.4);
    public float ColumnsGrowthPerTier { get; set; } = 1.8f;
    public FloatDistribution BaseEWStreets { get; set; } = LogNormal(1.0, 0.3);
    public float EWStreetsGrowthPerTier { get; set; } = 1.5f;
    public float CommercialPadsPer1000Pop { get; set; } = 2.5f;
    public int CivicChurchMinPop { get; set; } = 50;
    public int CivicSchoolMinPop { get; set; } = 200;
    public int CivicParkMinPop { get; set; } = 150;
    public float LandmarkWaterTowerChancePerTier { get; set; } = 0.3f;
    public float LandmarkGolfCourseChancePerTier { get; set; } = 0.1f;
}
```

**Modify**: `SmallTownConfig.cs` — Replace `SizePresets` dictionary with `TownTierSystem`; `SmallTownGenerator.Generate()` uses tier system

### 5. Feature Grammar for Extras (Phase 1, Item 5)

**New File**: `src/SpawnGuide/TownExtraDef.cs`

```csharp
public sealed class TownExtraDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int MinTier { get; set; } = 0;
    public int MaxTier { get; set; } = 3;
    public float BaseWeight { get; set; } = 1f;
    public string[] Requires { get; set; } = Array.Empty<string>();
    public string[] Excludes { get; set; } = Array.Empty<string>();
    public string[] Placement { get; set; } = Array.Empty<string>(); // "perimeter", "interior", "highway", "rail"
    public FloatDistribution SizeMultiplier { get; set; } = Uniform(0.8, 1.2);
}

// Built-in extras (all parameterized in SmallTownConfig.TownExtras):
// CulDeSacCourt, PlatEdgeStreet, OutlyingRoad, BypassedHighway, IndustrialSideStreet, TownSquare, Boulevard, RailCorridorStreet, CurvedStreet
```

**Modify**: `SmallTown.cs` — Use feature grammar for extra selection/placement

### 6. Highway/Rail Commercial Split (Phase 1, Item 6)

**New File**: `src/SpawnGuide/CommercialConfig.cs`

```csharp
public sealed class CommercialConfig
{
    // Highway commercial
    public float HighwayCommercialSpacingMi { get; set; } = 1.5f;
    public FloatDistribution HighwayCommercialDepthFt { get; set; } = LogNormal(3.5, 0.3);
    public FloatDistribution HighwayCommercialWidthFt { get; set; } = LogNormal(3.8, 0.4);
    public Dictionary<string, float> BusinessTypeWeights { get; set; } = new()
    {
        ["gas-station"] = 0.25f, ["feed-store"] = 0.15f, ["co-op"] = 0.10f,
        ["hardware"] = 0.10f, ["cafe-diner"] = 0.15f, ["implement-dealer"] = 0.10f, ["other"] = 0.15f
    };
    
    // Grain elevator (rail)
    public float ElevatorOffsetFromHighwayFt { get; set; } = 200f;
    public int ElevatorBinCount { get; set; } = 5;
    public float BinDiameterFt { get; set; } = 22f;
    public float BinSpacingFt { get; set; } = 30f;
    
    // Rail industrial
    public float RailIndustrialLotDepthFt { get; set; } = 200f;
    public FloatDistribution RailIndustrialLotWidthFt { get; set; } = LogNormal(4.5, 0.5);
}
```

### 7. 1990s Residential Standards (Phase 1, Item 7)

**New File**: `src/SpawnGuide/ResidentialConfig.cs`

```csharp
public sealed class ResidentialConfig
{
    public FloatDistribution LotFrontageFt { get; set; } = LogNormal(4.2, 0.2);
    public FloatDistribution LotDepthFt { get; set; } = LogNormal(4.8, 0.15);
    public FloatDistribution FrontSetbackFt { get; set; } = Normal(25, 5);
    public FloatDistribution SideSetbackFt { get; set; } = Normal(7.5, 2);
    public FloatDistribution RearSetbackFt { get; set; } = Normal(25, 5);
    public FloatDistribution GarageModeWeights { get; set; } = Dirichlet([0.55, 0.35, 0.10]);
    public FloatDistribution AttachedGarageWidthFt { get; set; } = Normal(22, 2);
    public FloatDistribution AttachedGarageDepthFt { get; set; } = Normal(22, 2);
    public FloatDistribution DetachedGarageWidthFt { get; set; } = Normal(24, 3);
    public FloatDistribution DetachedGarageDepthFt { get; set; } = Normal(24, 3);
    public FloatDistribution DrivewayWidthFt { get; set; } = Normal(16, 2);
    public string DrivewaySurface { get; set; } = "concrete";
    public FloatDistribution WalkWidthFt { get; set; } = Normal(4, 0.5);
    public string WalkSurface { get; set; } = "concrete";
    public float ShedChance { get; set; } = 0.15f;
    public FloatDistribution ShedWidthFt { get; set; } = Normal(10, 1.5);
    public FloatDistribution ShedDepthFt { get; set; } = Normal(12, 2);
    public FloatDistribution StreetTreeSpacingFt { get; set; } = Normal(40, 8);
    public FloatDistribution YardTreeCount { get; set; } = Poisson(2.5);
}
```

### 8. Iowa Standard Streets (Phase 1, Item 8)

**New File**: `src/SpawnGuide/StreetConfig.cs`

```csharp
public sealed class StreetConfig
{
    public float StreetRowFt { get; set; } = 66f;
    public float StreetPavedWidthFt { get; set; } = 32f;
    public float CenterlineOffsetFt { get; set; } = 0f;
    public string StreetSurface { get; set; } = "asphalt";
    public float AlleyRowFt { get; set; } = 16f;
    public string AlleySurface { get; set; } = "gravel";
    public FloatDistribution AlleyChancePerBlock { get; set; } = Beta(2, 2);
    public float PerimeterGapNFt { get; set; } = 50f;
    public float PerimeterJogHeightFt { get; set; } = 80f;
    public float DeadEndStubLengthFt { get; set; } = 250f;
    public float DeadEndBulbRadiusFt { get; set; } = 44f;
    public float FourSideBlockChance { get; set; } = 0.7f;
    public float RailCorridorStreetChance { get; set; } = 0.3f;
    public float CurvedStreetChance { get; set; } = 0.3f;
    public float FreeStreetEraseHalfFt { get; set; } = 20f;
}
```

### 9. Farm Evolution Model (Phase 1, Item 9)

**New File**: `src/SpawnGuide/FarmsteadConfig.cs`

```csharp
public sealed class FarmsteadConfig
{
    public FloatDistribution LotWidthFt { get; set; } = LogNormal(5.5, 0.1);
    public FloatDistribution LotDepthFt { get; set; } = LogNormal(5.8, 0.1);
    public FloatDistribution HouseArchetypeWeights { get; set; } = Dirichlet([0.6, 0.3, 0.1]);
    public FloatDistribution HouseSetbackFt { get; set; } = Normal(50, 10);
    public FloatDistribution MachineShedWidthFt { get; set; } = LogNormal(3.7, 0.15);
    public FloatDistribution MachineShedDepthFt { get; set; } = LogNormal(3.5, 0.15);
    public float MachineShedBehindHouseFt { get; set; } = 26f;
    public FloatDistribution BinCount { get; set; } = Poisson(3);
    public FloatDistribution BinDiameterFt { get; set; } = LogNormal(3.0, 0.1);
    public float BinRowBehindShedFt { get; set; } = 8f;
    public float PoleBarnChance { get; set; } = 0.4f;
    public FloatDistribution PoleBarnWidthFt { get; set; } = Normal(30, 4);
    public FloatDistribution PoleBarnDepthFt { get; set; } = Normal(48, 6);
    public float GambrelBarnChance { get; set; } = 0.15f;
    public FloatDistribution GambrelBarnWidthFt { get; set; } = Normal(34, 4);
    public FloatDistribution GambrelBarnDepthFt { get; set; } = Normal(56, 6);
    public float ShelterbeltChance { get; set; } = 0.6f;
    public FloatDistribution ShelterbeltTreeSpacingFt { get; set; } = Normal(16, 2);
    public FloatDistribution ShelterbeltRows { get; set; } = Poisson(3) + 2;
    public FloatDistribution ShelterbeltOffsetFt { get; set; } = Normal(30, 10);
    public FloatDistribution DrivewayWidthFt { get; set; } = Normal(14, 2);
    public string DrivewaySurface { get; set; } = "gravel";
    public FloatDistribution WalkWidthFt { get; set; } = Normal(3.5, 0.5);
}
```

### 10. House Archetype Distributions (Phase 1, Item 10)

**Modify**: `HouseConfig.cs` — Replace ranges with distributions

```csharp
public sealed class HouseArchetypeConfig
{
    public sealed class RanchConfig
    {
        public FloatDistribution WidthFt { get; set; } = LogNormal(3.7, 0.1);
        public FloatDistribution DepthFt { get; set; } = LogNormal(3.2, 0.1);
        public FloatDistribution GarageWidthFt { get; set; } = Normal(22, 3);
        public FloatDistribution GarageDepthFt { get; set; } = Normal(22, 2);
        public float GarageLeftChance { get; set; } = 0.5f;
        public FloatDistribution DoorOffsetFraction { get; set; } = Beta(2, 5, 0.15, 0.35);
        public FloatDistribution WindowJitterFraction { get; set; } = Beta(2, 5, 0, 0.08);
    }
    
    public sealed class FarmhouseConfig
    {
        public FloatDistribution WidthFt { get; set; } = LogNormal(3.4, 0.1);
        public FloatDistribution DepthFt { get; set; } = LogNormal(3.5, 0.1);
        public FloatDistribution PorchWidthFraction { get; set; } = Beta(5, 3, 0.5, 0.8);
        public FloatDistribution PorchDepthFt { get; set; } = Normal(7, 1);
        public float DoorCenteredChance { get; set; } = 0.9f;
    }
    
    public sealed class SplitLevelConfig
    {
        public FloatDistribution WidthFt { get; set; } = LogNormal(3.4, 0.1);
        public FloatDistribution DepthFt { get; set; } = LogNormal(3.4, 0.1);
        public FloatDistribution GarageWidthFt { get; set; } = Normal(16, 2);
        public FloatDistribution GarageDepthFt { get; set; } = Normal(20, 2);
        public float GarageLeftChance { get; set; } = 0.5f;
        public float GarageDoorOnSideChance { get; set; } = 0.9f;
    }
    
    public string[] SidingOptions { get; set; } = new[]
    { "White vinyl", "Ivory vinyl", "Beige vinyl", "Light gray vinyl", "Brick front + vinyl sides", "Wood lap (legacy)", "Aluminum (legacy)" };
    
    public FloatDistribution GarageDoorWidthFt { get; set; } = Bimodal([9, 16], [0.65, 0.35]);
    public FloatDistribution TwoWindowMinWallFt { get; set; } = Normal(22, 2);
    public FloatDistribution OneWindowMinWallFt { get; set; } = Normal(9, 1);
}
```

### 11. ConditionalRule Engine (Phase 1, Item 11)

**New File**: `src/SpawnGuide/ConfigRules.cs`

```csharp
public sealed class ConditionalRule
{
    public string IfParameter { get; set; } = "";      // e.g. "SmallTown.Tier"
    public string Operator { get; set; } = ">=";       // >=, <=, ==, !=, in, notin
    public string Value { get; set; } = "";            // "2" or "village,town"
    public string ThenParameter { get; set; } = "";    // e.g. "SmallTown.CivicDensity"
    public string ThenOperation { get; set; } = "multiply"; // set, multiply, add, clamp
    public float ThenValue { get; set; } = 1.5f;
}

public static class ConfigRuleEngine
{
    public static void ApplyRules(SpawnConfig config, ConditionalRule[] rules)
    {
        // Evaluate each rule after config load, before generation
        // Support nested property paths via reflection
    }
}

// Built-in rules:
var builtInRules = new[]
{
    new ConditionalRule { IfParameter="SmallTown.Tier", Operator=">=", Value="2", ThenParameter="SmallTown.CivicDensity", ThenOperation="multiply", ThenValue=1.5f },
    new ConditionalRule { IfParameter="World.TownLayout", Operator=="==", Value="Both", ThenParameter="SmallTown.ResidentialDensity", ThenOperation="multiply", ThenValue=1.2f },
    new ConditionalRule { IfParameter="Rural.LotWidth", Operator=">", Value="250", ThenParameter="Rural.OutbuildingDensity", ThenOperation="multiply", ThenValue=0.8f },
};
```

### 12. Coop Corner Archetype (Phase 1, Item 12)

**New File**: `src/SpawnGuide/CoopCornerConfig.cs`

```csharp
public sealed class CoopCornerConfig
{
    public float ChancePerSectionCorner { get; set; } = 0.02f;
    public bool RequiresRail { get; set; } = true;
    public bool RequiresHighway { get; set; } = false;
    public FloatDistribution ElevatorCapacityBu { get; set; } = LogNormal(5.5, 0.3);
    public FloatDistribution BinCount { get; set; } = Poisson(6) + 2;
    public FloatDistribution BinDiameterFt { get; set; } = LogNormal(3.1, 0.1);
    public FloatDistribution OfficeSizeFt { get; set; } = Normal(40, 10);
    public FloatDistribution ScaleHouseSizeFt { get; set; } = Normal(20, 5);
}
```

**Modify**: `WorldMap.cs` — Add coop corner generation in farm/rail pipeline

### 13. Parameter Explorer UI (Phase 1, Item 13)

**New File**: `src/SpawnGuide/out/param-explorer.html` — Static HTML (no framework)

**Features**:
- Config tree with System + Concern groupings
- Input controls: slider, number, checkbox, dropdown, color picker, array editor, distribution picker
- Live 300×300px SVG preview (300ms debounce)
- Diff view (highlight changes from default)
- Profile selector (8 profiles)
- Export/Import JSON
- Hover tooltips from XML docs
- Validation feedback (red=hard, yellow=soft)

**Modify**: `Program.cs` — Add `/params` route to `--serve` handler

### 14. Sensitivity Analysis CLI (Phase 1, Item 14)

**Modify**: `Program.cs` — Add `--analyze-sensitivity` command

```bash
dotnet run -- --analyze-sensitivity \
  --param SmallTown.ResidentialDensity \
  --range 0.5,2.0 \
  --steps 10 \
  --seeds 20 \
  --metrics houses,outbuildings,blocks,vacancy,roadLength,townArea,farmCount,riverLength,railLength \
  --output sensitivity.csv \
  --parallel true \
  --spacing linear  # or log
```

**Output**: CSV with columns: `ParameterValue,Seed,Houses,Outbuildings,Blocks,Vacancy,RoadLength,TownArea,FarmCount,RiverLength,RailLength` + summary statistics

### 15. Hot-reload + Binary Cache (Phase 1, Item 15)

**Modify**: `SpawnConfig.cs` — Add `FileSystemWatcher` in `--serve` mode; binary serialization for `.config.bin` cache

### 16. Config Migration + Diff Tool (Phase 1, Item 16)

**Modify**: `SpawnConfig.cs` — `SchemaVersion = 2`; `Migrate(fromVersion)` method; CLI commands:
- `--migrate-config <file>` — upgrade to latest schema
- `--validate-config <file>` — check validity, show warnings
- `--diff-config <old> <new>` — show differences
- Auto-migrate on load with backup to `.backup.v{N}`

### 17. Enum Migration (Phase 1, Part of Item 1)

**Migrate these string fields to enums**:
| Field | Enum |
|-------|------|
| `RuralConfig.DrivewaySurface`, `WalkSurface` | `SurfaceType` |
| `TownBlockConfig.DrivewaySurface`, `AlleyDrivewaySurface`, `WalkSurface` | `SurfaceType` |
| `SmallTownConfig.DrivewaySurface`, `AlleyDrivewaySurface`, `WalkSurface` | `SurfaceType` |
| `WorldConfig.FieldEntranceSurface` | `SurfaceType` |
| `HouseConfig.SidingOptions` | `SidingType` |
| `SmallTownConfig.GarageMode*` | `GarageMode` (with Dirichlet weights) |
| `WorldConfig.RoadSurface` (MapPlan) | `RoadSurface` |

**Enums**:
```csharp
public enum SurfaceType { Gravel, Concrete, Asphalt, Dirt }
public enum SidingType { WhiteVinyl, IvoryVinyl, BeigeVinyl, LightGrayVinyl, BrickFrontVinylSides, WoodLap, Aluminum }
public enum GarageMode { Integral, Detached, None }
public enum RoadTopology { Grid, Organic, Hybrid, Radial, RiverFollowing }
public enum RoadSurface { Asphalt, Gravel, Dirt }
public enum TownLayout { North, South, Both }
public enum TownClass { Crossroads, Hamlet, Village, Town }
```

---

## Implementation Order (Recommended)

1. **Distributions.cs + Enums.cs + Attributes.cs** — Foundation types
2. **SpawnConfig.cs modifications** — Add attributes, SchemaVersion, migration, replace ranges with distributions
3. **NoiseConfig.cs** — Extract from WorldConfig
4. **WorldMap.cs** — Use NoiseConfig, update generation pipeline
5. **ConfigProfiles.cs** — Built-in profiles
6. **ConditionalRule/ConfigRules.cs** — Correlation engine
7. **TownTierSystem.cs + TownExtraDef.cs** — Generative tiers + feature grammar
8. **CommercialConfig.cs + CivicConfig.cs** — Threshold-based civic/landmarks
9. **ResidentialConfig.cs + StreetConfig.cs + FarmsteadConfig.cs** — 1990s standards
10. **HouseArchetypeConfig.cs** — Update HouseConfig
11. **CoopCornerConfig.cs** — New archetype
12. **SmallTown.cs** — Integrate tier system, feature grammar, commercial split
13. **CountrysideMap.cs** — Use new farm/residential configs
14. **Program.cs** — Add CLI commands (--profile, --analyze-sensitivity, --migrate-config, etc.)
15. **param-explorer.html** — UI
16. **Serialization.cs** — Binary cache, hot-reload
17. **Tests** — Determinism, sensitivity, visual regression, edge cases, migration, profiles, benchmark

---

## Testing Requirements

| Test | Command | Purpose |
|------|---------|---------|
| Determinism | `--verify --seed 1993` | Byte-for-byte reproducibility |
| Sensitivity | `--test-sensitivity` | Detect cliffs/dead zones |
| Visual Regression | `--test-visual --golden-dir` | Pixel-diff vs golden masters |
| Edge Cases | `--test-edge-cases` | Min/max, zero densities, empty arrays |
| Migration | `--test-migration v1.json v2.json` | Verify migration preserves behavior |
| Profiles | `--test-profiles` | Round-trip all 8 profiles |
| Performance | `--benchmark --iterations 100` | Generation time baselines |

---

## Deliverables

1. **Modified/Created Source Files** in `src/SpawnGuide/`
2. **Updated `spawnconfig.json`** with new schema (v2) and all parameters with rationale comments
3. **Parameter Explorer** at `src/SpawnGuide/out/param-explorer.html`
4. **Golden Master SVGs** for visual regression (in `src/SpawnGuide/out/golden/`)
5. **Updated Documentation**: `PROCGEN_TUNING_CHECKLIST.md` with Phase 1/2 split, `README.md` with new CLI flags

---

## Constraints & Reminders

- **Determinism**: Every change must preserve byte-for-byte reproducibility for same seed
- **Backward Compatibility**: `SpawnConfig.Load()` must handle missing fields gracefully; auto-migrate v1→v2
- **Performance**: No regression in generation speed; binary cache for game load
- **Rationale Comments**: Every parameter in JSON must have `// Rationale: ...` comment
- **No External Dependencies**: Current project has zero NuGet packages — keep it that way
- **Platform**: Windows only, .NET 5.0

---

## Start Here

Begin with **Distributions.cs, Enums.cs, Attributes.cs** — these are the foundation types everything else depends on. Then modify **SpawnConfig.cs** to use them.

Reference `docs/research/GRILLED_DATA.md` for any clarification on decisions. The grilling log is the single source of truth.

Good luck! 🚜