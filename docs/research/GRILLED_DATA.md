# Grilled Data Log

*Append-only record of all grilling Q&A sessions. Entries added via `tools/grill.py`.*

---

---

id: G-001
topic: "Philosophy & Constraints"
date: "2025-01-15"
status: "complete"
tags: ["philosophy", "constraints", "goals", "scope"]
params: ["SpawnConfig.SchemaVersion", "Rng._state"]
---
## Question
1.1 Design Goals ranking
1.2 Target audiences
1.3 Hard constraints (determinism, performance, memory, platform)
1.4 Scope IN
1.5 Scope OUT
1.6 Success metrics
1.7 Reference truth

## Answer
1.1 Ranked: gameplay utility > infinite variety > performance > visual authenticity > controllability
1.2 Core devs and designers primary; possible player options later
1.3 Determinism: byte-for-byte reproducibility required (mulberry32 RNG already provides this). Performance: no fixed budget yet; world generation used as player map, structure generation can be slower. Memory: no limits yet. Platform: Windows only.
1.4 Meaningfully more realistic spawns
1.5 OUT: narrative, vehicle traffic, crop simulation, seasons
1.6 Visual review, comparison to references, automated tests
1.7 Placeholder references (Lyon County aerials) — must become more generic, not tied to specific real locations

## Decisions
- Gameplay utility is top priority; visual authenticity serves gameplay
- Determinism is non-negotiable
- Scope explicitly excludes narrative/traffic/crops/seasons
- References should be generic archetypes, not specific real towns

## Open Questions
- Performance budget for world gen vs structure gen
- Memory limits for infinite world cache
- Player-facing parameter exposure scope

## Related Params
- SpawnConfig.SchemaVersion
- Rng._state

---

---

id: G-002
topic: "Parameter Architecture"
date: "2025-01-15"
status: "complete"
tags: ["architecture", "params", "hierarchy", "validation", "serialization"]
params: ["SpawnConfig", "WorldConfig", "SmallTownConfig", "TownBlockConfig", "RuralConfig", "HouseConfig"]
---
## Question
2.1 Hierarchy levels
2.2 Inheritance/override strategy
2.3 Parameter types beyond ranges
2.4 Validation strategy
2.5 Serialization & hot-reload
2.6 Parameter discovery
2.7 Cross-system correlations

## Answer
2.1 Hierarchy confirmed: Global (WorldConfig) → Region (future) → Town (SmallTownConfig) → Block/Lot (TownBlockConfig/RuralConfig) → Instance (HouseConfig). Generation order should match real-world historical appearance order.
2.2 Need explanation of merge vs replace vs delta vs profile strategies.
2.3 Need explanation of distributions, curves, expressions, enums.
2.4 Validation varied by error type.
2.5 Suggestions welcome for JSON/binary/hot-reload/schema versioning.
2.6 Both grouping by system and by concern.
2.7 Yes, need declarative correlation rules.

## Decisions
- Hierarchy matches real-world generation order
- Both system and concern grouping for params
- Declarative correlation rules needed

## Open Questions
- Inheritance strategy details
- Distribution/curve/expression types needed
- Validation strategy per error type
- Serialization format and hot-reload approach

## Related Params
- SpawnConfig
- WorldConfig
- SmallTownConfig
- TownBlockConfig
- RuralConfig
- HouseConfig

---

---

id: G-003
topic: "Noise & Natural Systems + Pipeline Extensions"
date: "2025-01-15"
status: "complete"
tags: ["world", "hills", "rivers", "rail", "roads", "towns", "farms", "pipeline", "geology", "soils", "wetlands", "vegetation", "history"]
params: ["WorldConfig.NoiseConfig", "WorldConfig.Hills*", "WorldConfig.Rivers*", "WorldConfig.Rail*", "WorldConfig.Roads*", "WorldConfig.Towns*", "WorldConfig.Farms*"]
---
## Question
3.1-3.10 Noise model, contours, river spacing/meander/tributaries, rail swell/spurs, road exceptions, town placement, farm logic
Plus: Suggest additional systems for pipeline

## Answer
**Core philosophy**: Everything the more realistic way, with manipulation parameters. Rationale comments on every setting.
**Only deviations from reality**: (1) 1990s time setting, (2) Uniformly low density = lowest density of NW Iowa everywhere, (3) Cities capped at size of smallest Iowa cities (rare exceptions).

**Pipeline extensions suggested** (in geological/historical order):
1. **Geology/Glacial Regions** — Bedrock + glacial landforms (Loess Hills, Des Moines Lobe, Driftless, Southern Iowa Drift Plain, Iowan Surface). Controls: hill amplitude, soil type, drainage, native vegetation.
2. **Soils** — SSURGO-style: productivity index (CSR), drainage class, erodibility. Affects: farm viability, building foundations, shelterbelt species.
3. **Wetlands/Floodplains** — Prairie potholes (Des Moines Lobe), river floodplains, FEMA-style zones. Hard constraint: no buildings in floodway; wetland mitigation.
4. **Native Vegetation** — Pre-settlement: tallgrass prairie, oak savanna, woodland, wet prairie. Determines: shelterbelt species, fire history, soil carbon.
5. **Historical Roads** — Ridge roads (section lines), Native American trails, stagecoach routes, early wagon roads. Some become modern highways; others fade to field entrances.
6. **Property Grid (PLSS)** — Township/Range/Section/Quarter — the fundamental lattice. Farmsteads align to quarters; roads on section lines; drainage districts follow quarters.
7. **Economic Hierarchy (Central Place Theory)** — County seats > market towns > hamlets > crossroads. Grain elevator spacing (~7-10mi), creamery spacing, railroad division points.
8. **Infrastructure Layers** — Rural electrification (1930s REA), telephone, pipelines, drainage districts (tile lines), power lines.
9. **Demographics/Farm Consolidation** — 1990s: fewer, larger farms vs 1900s. Farmstead abandonment/reuse. CRP land (Conservation Reserve Program).

**Revised Pipeline Order** (geological → historical):
1. Geology/Glacial Regions → 2. Soils → 3. Hills (constrained by geology) → 4. Wetlands/Floodplains → 5. Rivers (constrained by geology/glacial) → 6. Native Vegetation → 7. Historical Trails/Ridge Roads → 8. Rail (1850s-80s) → 9. Towns (on rail) → 10. Property Grid (PLSS) → 11. Highways (1920s+) → 12. Farmsteads (homestead → consolidation) → 13. Shelterbelts (1930s) → 14. Electrification/Infrastructure → 15. Modern era (1990s snapshot)

**Parameterization**: Each stage gets config with rationale comments. All deterministic from seed. User can dial "realism" vs "gameplay" via density multipliers, era sliders, region overrides.

## Decisions
- Add 9 new pipeline stages for geological/historical realism
- All params have rationale comments
- Only 3 deviations from reality: 1990s era, uniform NW Iowa low density, city size cap
- Pipeline order follows real-world formation sequence
- Each stage parameterized and overridable

## Open Questions
- Which new stages are Phase 1 vs Phase 2+?
- SSURGO soil data source: procedural approximation vs real data import?
- PLSS grid: perfect vs historical survey errors (offsets, curvature)?
- Economic hierarchy: central place theory parameters (k=3, k=4, k=7 nesting)?
- Infrastructure: which layers visible in 1990s snapshot?

## Related Params
- WorldConfig.NoiseConfig
- WorldConfig.Hills* (wavelengths, amplitudes, octaves, persistence, lacunarity)
- WorldConfig.Rivers* (spacing, meander, tributary rules)
- WorldConfig.Rail* (swell, spurs, phase)
- WorldConfig.Roads* (spacing, exceptions, farm margin)
- WorldConfig.Towns* (rail proximity, layout, class distribution)
- WorldConfig.Farms* (per-section, jitter, density)
- NEW: GeologyConfig, SoilsConfig, WetlandsConfig, VegetationConfig, HistoricalRoadsConfig, PLSSConfig, EconomicConfig, InfrastructureConfig, DemographicsConfig

---

---

id: G-003-CONFIRM
topic: "Noise & Natural Systems — Confirmation"
date: "2025-01-15"
status: "complete"
tags: ["confirmation", "pipeline", "noise", "hills", "rivers", "rail", "roads", "towns", "farms"]
params: ["WorldConfig.NoiseConfig", "WorldConfig.Hills*", "WorldConfig.Rivers*", "WorldConfig.Rail*", "WorldConfig.Roads*", "WorldConfig.Towns*", "WorldConfig.Farms*"]
---
## Question
Confirm the detailed parameterization for 3.1-3.10 (Hills fBm, Rivers Perlin meander, Rail swell/spurs, Roads grid exceptions, Towns placement, Farms per-section) and 9 new pipeline stages.

## Answer
**Confirmed**. Proceed with:
- Hills: fBm with octaves/persistence/lacunarity + domain warp option
- Contours: configurable interval, lattice cell, index frequency
- Rivers: LogNormal trunk spacing, Perlin meander (octaves/persistence/lacunarity), Strahler-order tributaries, bank trees by order
- Rail: Swell as fBm, spurs perpendicular with elevator, 5.5mi max distance
- Roads: 10mi paved spacing, 1.8% absent/2.2% paved exceptions, 300ft farm margin
- Towns: 9mi rail proximity, Dirichlet layout/class weights, slot jitter
- Farms: 0.15 density (NW Iowa low), Normal perp jitter, LogNormal along jitter, max 3/section
- 9 new pipeline stages with rationale comments on all params

## Decisions
- All detailed parameterizations confirmed
- fBm for hills, Perlin for river meander
- Dirichlet distributions for correlated town layout/class probabilities
- LogNormal for river spacing, farm along-jitter
- Farm density fixed at 0.15 (uniform NW Iowa low density)

## Open Questions
- Phase 1 vs Phase 2 prioritization for 9 new pipeline stages
- SSURGO soil data: procedural vs import
- PLSS survey error model
- Central place theory k-value
- Infrastructure visibility in 1990s

## Related Params
- WorldConfig.NoiseConfig
- WorldConfig.Hills*
- WorldConfig.Rivers*
- WorldConfig.Rail*
- WorldConfig.Roads*
- WorldConfig.Towns*
- WorldConfig.Farms*
- NEW: GeologyConfig, SoilsConfig, WetlandsConfig, VegetationConfig, HistoricalRoadsConfig, PLSSConfig, EconomicConfig, InfrastructureConfig, DemographicsConfig

---

---

id: G-004
topic: "Human Systems — Towns, Lots, Houses"
date: "2025-01-15"
status: "complete"
tags: ["towns", "residential", "rural", "houses", "commercial", "civic", "streets", "archetypes"]
params: ["SmallTownConfig", "TownBlockConfig", "RuralConfig", "HouseConfig", "TownTierSystem", "TownExtraDef", "CommercialConfig", "CivicConfig", "ResidentialConfig", "StreetConfig", "FarmsteadConfig", "HouseArchetypeConfig", "CoopCornerConfig", "TrailerCourtConfig"]
---
## Question
4.1 Town size tiers: replace fixed presets with generative Central Place Theory (k=3) tier system
4.2 Town extras: feature grammar with prerequisites/exclusions/placement rules
4.3 Commercial strip: weighted business types, separate highway vs rail commercial zones
4.4 Civic & landmarks: threshold-based with per-capita scaling, consolidated schools
4.5 Residential lots: 1990s subdivision standards (setbacks, garage modes, driveways, walks, sheds, trees)
4.6 Streets & perimeter: Iowa standard ROW (66ft), alleys (Beta clustered), perimeter jog, 4-side blocks
4.7 Rural farmstead: homestead→1990s evolution, PSFP shelterbelts, 1990s machine shed sizing
4.8 House archetypes: LogNormal dimensions, Beta fractions, Bimodal garage doors, era-appropriate siding
4.9 Missing archetypes: Coop Corner (grain elevator at section corner) + Trailer Court (town fringe)

## Answer
**All confirmed**.

4.1 Replace fixed presets with generative tier system (k=3 marketing hierarchy). Name presets by size (Crossroads, Hamlet, Village, Town). Remove Boyden/Hull entirely.

4.2 Full feature grammar: prerequisites, exclusions, placement rules, weights. 0-2 limit emergent from weights.

4.3 Weighted business types for highway commercial. Separate rail-side industrial (grain elevators, ag services). Rails = crops only.

4.5 1990s standards: 25ft front/7.5ft side/25ft rear setbacks. Garage modes Dirichlet (55/35/10). Concrete drives/walks. Poisson yard trees.

4.6 66ft street ROW, 32ft paved, 16ft gravel alleys (Beta clustered). Perimeter loop with north jog. 70% 4-side blocks.

4.7 Farm evolution model. Shelterbelts = 1930s PSFP legacy (mature by 1990s). Machine sheds sized for 1990s equipment.

4.8 LogNormal for dimensions (right-skewed). Beta for bounded fractions. Bimodal garage doors (9ft/16ft). 1990s siding market.

4.9 Add CoopCornerConfig (section-line corner, rail-required) and TrailerCourtConfig (town fringe, curved street, 40x70 pads).

## Decisions
- Generative tier system replaces fixed presets (k=3 Central Place Theory)
- Feature grammar for town extras with prerequisites/exclusions
- Highway vs rail commercial zones separated; rails = crops only
- Threshold-based civic/landmarks with per-capita church scaling
- 1990s subdivision standards for all residential params
- Iowa standard 66ft ROW, Beta-distributed alley clustering
- Farmstead evolution: homestead → 1990s consolidation
- LogNormal/Beta/Bimodal distributions for house archetypes
- Two new archetypes: Coop Corner + Trailer Court

## Open Questions
- Phase 1 vs Phase 2 prioritization for new archetypes
- Exact k-value calibration for Iowa settlement hierarchy
- PSFP shelterbelt species mix by region
- Trailer court density vs town tier
- Coop corner spacing along rail lines

## Related Params
- SmallTownConfig
- TownBlockConfig
- RuralConfig
- HouseConfig
- TownTierSystem
- TownExtraDef
- CommercialConfig
- CivicConfig
- ResidentialConfig
- StreetConfig
- FarmsteadConfig
- HouseArchetypeConfig
- CoopCornerConfig
- TrailerCourtConfig

---

---

id: G-005
topic: "Developer Experience & Implementation Prioritization"
date: "2025-01-15"
status: "complete"
tags: ["implementation", "phases", "ui", "cli", "migration", "testing", "enums"]
params: ["SpawnConfig", "NoiseConfig", "TownTierSystem", "TownExtraDef", "CommercialConfig", "CivicConfig", "ResidentialConfig", "StreetConfig", "FarmsteadConfig", "HouseArchetypeConfig", "CoopCornerConfig", "TrailerCourtConfig", "ConditionalRule", "CategoryAttribute", "FloatDistribution", "AnimationCurve", "Expression"]
---
## Question
5.1 Phase 1 vs Phase 2 prioritization
5.2 Parameter Explorer UI (/params endpoint)
5.3 Sensitivity Analysis CLI (--analyze-sensitivity)
5.4 Config Migration Strategy (auto-migrate, backup, diff)
5.5 Enum Migration (string fields → enums)
5.6 Testing Strategy (determinism, sensitivity, visual regression, edge cases, migration, profiles, performance)

## Answer
**All confirmed**.

5.1 Phase 1: Parameter infrastructure (Distributions, Curves, Expressions, Enums, Validation, Serialization, CategoryAttribute), Config Profiles, NoiseConfig extraction, Generative Town Tiers, Feature Grammar for Extras, Highway/Rail Commercial Split, 1990s Residential Standards, Iowa Standard Streets, Farm Evolution Model, House Archetype Distributions, ConditionalRule Engine, Coop Corner Archetype, Parameter Explorer UI, Sensitivity Analysis CLI, Hot-reload + Binary Cache, Config Migration + Diff Tool.
Phase 2: Trailer Court Archetype, Region/Geology/Soils Pipeline (9 new stages).

5.2 Parameter Explorer UI: Static HTML at /params. Config tree (System + Concern groupings). Input controls for all param types. Live 300x300px SVG preview (300ms debounce). Diff view. Profile selector (8 profiles). Export/Import JSON. Hover tooltips from XML docs. Validation feedback (red=hard, yellow=soft).

5.3 Sensitivity CLI: --param (dot-path), --range, --steps, --seeds, --metrics (houses,outbuildings,blocks,vacancy,roadLength,townArea,farmCount,riverLength,railLength), --output (CSV/JSON), --parallel (default true). Linear spacing default, log spacing option.

5.4 Migration: SchemaVersion in config. Auto-migrate on load if older. Backup to .backup.v{N}. Migration logging to console. CLI: --migrate-config, --validate-config, --diff-config. Breaking changes only on major version.

5.5 Enum Migration: 12 string fields → enums (SurfaceType, SidingType, GarageMode, RoadSurface). Case-insensitive parsing with aliases. Unknown → warn + default.

5.6 Testing: --verify (determinism), --test-sensitivity (cliffs/dead zones), --test-visual (pixel-diff vs golden masters), --test-edge-cases (min/max/zero/empty), --test-migration (v1→v2 behavior), --test-profiles (round-trip), --benchmark (100 iter). CI integration. Golden masters in repo.

## Decisions
- Phase 1/2 split confirmed as proposed
- Parameter Explorer UI at /params with full feature set
- Sensitivity CLI with 9 metrics, CSV/JSON output, parallel execution
- Auto-migration with backup + logging; CLI tools for migrate/validate/diff
- 12 string fields migrated to 4 enums with alias support
- 7 automated test types with CI integration

## Open Questions
- Golden master storage location (repo vs artifacts)
- CI provider configuration
- Performance baseline values
- Log spacing option for sensitivity sweep

## Related Params
- SpawnConfig
- NoiseConfig
- TownTierSystem
- TownExtraDef
- CommercialConfig
- CivicConfig
- ResidentialConfig
- StreetConfig
- FarmsteadConfig
- HouseArchetypeConfig
- CoopCornerConfig
- TrailerCourtConfig
- ConditionalRule
- CategoryAttribute
- FloatDistribution
- AnimationCurve
- Expression