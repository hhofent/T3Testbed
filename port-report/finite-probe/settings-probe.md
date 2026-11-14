# Automated settings probe

Run from repository root, without --config: `dotnet src/SpawnGuide/bin/Release/net5.0/SpawnGuide.dll --probe-settings`.

Reflection includes numeric/bool leaves, range/distribution fields, dictionary entries and arrays. Each mutation starts from an independent clone of the loaded configuration. Both directions use 25% (integers at least 1), clamped to declared ranges; zero uses 0.25 or 25% of its declared range. Bools flip. SHA-256 of SVG output is compared on seeds 1993, 7, 42 for four town sizes, country and the world origin window. ERROR takes precedence if any mutation fails validation or generation. DEAD means no sampled change, not proof of unreachable code. Schema/version and inactive distribution fields are included. No settings are wired by this tool.

| Path | Status | Affected maps/seeds | Mutations | Errors |
|---|---|---|---|---|
| Residential.YardTreeCount.Max | DEAD |  | 2.5521175E+38 |  |
| Rural.BinCount.Max | DEAD |  | 2.5521175E+38 |  |
| Rural.ShelterbeltRows.Max | DEAD |  | 2.5521175E+38 |  |
| SmallTown.ElevatorBinCount.Max | DEAD |  | 2.5521175E+38 |  |
| TownBlock.YardTreeCount.Max | DEAD |  | 2.5521175E+38 |  |
