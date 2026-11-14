# Automated settings probe

Run from repository root, without --config: `dotnet src/SpawnGuide/bin/Release/net5.0/SpawnGuide.dll --probe-settings`.

Reflection includes numeric/bool leaves, range/distribution fields, dictionary entries and arrays. Each mutation starts from an independent clone of the loaded configuration. Both directions use 25% (integers at least 1), clamped to declared ranges; zero uses 0.25 or 25% of its declared range. Bools flip. SHA-256 of SVG output is compared on seeds 1993, 7, 42 for four town sizes, country and the world origin window. ERROR takes precedence if any mutation fails validation or generation. DEAD means no sampled change, not proof of unreachable code. Schema/version and inactive distribution fields are included. No settings are wired by this tool.

| Path | Status | Affected maps/seeds | Mutations | Errors |
|---|---|---|---|---|
| SmallTown.BlobAmplitude1.Max | LIVE | country@1993; smalltown/hamlet@1993; smalltown/town@1993; smalltown/town@42; smalltown/town@7; smalltown/village@1993; smalltown/village@42; smalltown/village@7 | 0.044999998; 0.074999996 |  |
| SmallTown.BlobAmplitude1.Min | LIVE | smalltown/town@1993; smalltown/town@42; smalltown/town@7; smalltown/village@42; smalltown/village@7 | 0.015; 0.024999999 |  |
| SmallTown.BlobAmplitude2.Max | LIVE | smalltown/town@1993; smalltown/town@42; smalltown/village@1993; smalltown/village@7; world@1993 | 0.022499999; 0.037499998 |  |
| SmallTown.BlobAmplitude2.Min | LIVE | smalltown/town@42; smalltown/village@7 | 0.0075; 0.012499999 |  |
| SmallTown.BlobHarmonic1.Max | LIVE | country@1993; country@42; smalltown/crossroads@7; smalltown/hamlet@1993; smalltown/hamlet@42; smalltown/town@1993; smalltown/town@42; smalltown/town@7; smalltown/village@42; smalltown/village@7; world@1993 | 3; 5 |  |
| SmallTown.BlobHarmonic1.Min | LIVE | country@42; country@7; smalltown/crossroads@7; smalltown/hamlet@42; smalltown/hamlet@7; smalltown/town@1993; smalltown/town@42; smalltown/town@7; smalltown/village@1993; world@1993 | 1; 3 |  |
| SmallTown.BlobHarmonic2.Max | LIVE | country@1993; country@42; smalltown/hamlet@1993; smalltown/hamlet@42; smalltown/town@1993; smalltown/town@42; smalltown/town@7; smalltown/village@42; world@1993 | 6; 10 |  |
| SmallTown.BlobHarmonic2.Min | LIVE | country@1993; country@7; smalltown/hamlet@1993; smalltown/hamlet@7; smalltown/town@42; smalltown/village@1993; smalltown/village@42 | 4; 6 |  |
| SmallTown.BlobPhase.Max | LIVE | country@1993; country@42; smalltown/crossroads@7; smalltown/hamlet@1993; smalltown/hamlet@42; smalltown/town@1993; smalltown/town@42; smalltown/town@7; smalltown/village@42; smalltown/village@7; world@1993 | 4.712389; 7.853982 |  |
| SmallTown.BlobPhase.Min | LIVE | smalltown/town@1993; smalltown/town@42; smalltown/town@7; smalltown/village@1993; smalltown/village@42; world@1993 | -0.25; 0.25 |  |
| SmallTown.BlobRadiusMultiplier | LIVE | country@1993; country@42; country@7; smalltown/crossroads@1993; smalltown/crossroads@42; smalltown/crossroads@7; smalltown/hamlet@1993; smalltown/hamlet@42; smalltown/hamlet@7; smalltown/town@1993; smalltown/town@42; smalltown/town@7; smalltown/village@1993; smalltown/village@42; smalltown/village@7; world@1993 | 0.78; 1.3 |  |
