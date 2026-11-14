# Existing profile repairs

The strict setter exposed silent no-ops and invalid values in the eight pre-existing profiles. Default generation values are unchanged.

| Previous entry | Repair / reason |
|---|---|
| Nested readonly distribution/range fields | Reconstruct the value and copy it back to its containing property |
| World.TownRailProximityMi, RailSpurMaxDistanceMi, RailSpurAlwaysHasElevator, PavedSpacingMi, FarmMarginFt, RoadLinePavedChance, RoadLineAbsentChance | Use the existing Noise section properties with the same values |
| World.RailSpacingMi | Noise.RailSpacingMi.Mean; existing setting is a distribution |
| NoiseConfig.* | Noise.* (actual root section name) |
| World.TownClass*Chance | Noise.TownClassWeights.Concentrations, preserving 0.1/0.2/0.4/0.3 |
| Houses.SidingOptions human-readable obsolete strings | Supported enum values WhiteVinyl, LightGrayVinyl, BeigeVinyl, BrickFrontVinylSides; unsupported blue-gray, fiber-cement and dark-bronze entries omitted |
| SmallTown.RailIndustrialLotWidth.Min/Max | Removed nonexistent properties; no corresponding lot-width setting exists (depth is a different dimension) |
| SmallTown.StreetTreeChance | Removed nonexistent property; TownBlock's existing tree setting retained |
| NoiseConfig.WetlandDensity | Removed nonexistent property; wetlands stage is excluded by UC1 |
| testing-max ViewerMaxZoom=500 | 300, the user's U16 cap |

ConfigProfile.Apply now uses the strict override setter; unknown paths can no longer silently succeed. Imported JSON profiles containing JsonElement values also apply. Integer distribution Min/Max aliases use inclusive bounds and copy back to FloatDist.

The binary cache previously saved only seven original sections, dropping Noise, TownTiers, Residential, Streets and other newer sections. It now caches the complete JSON config with a format marker. Old partial caches are rejected and rebuilt from their JSON source. Distribution JSON retains all shape parameters, including currently inactive ones.

Evidence: port-report/takeover-selftests.txt (25 pass); takeover-verify.txt (13 pass); takeover-build.txt (69 warnings, 0 errors). Full per-setting wiring and attribute audit remains outstanding.
