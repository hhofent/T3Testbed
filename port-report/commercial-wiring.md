# T4 commercial wiring increment

Five previously unread Commercial settings now reach SmallTownGenerator from CLI smalltown and HTTP /gen, and from the settings audit:

| Path | Generation read | Before | Now |
|---|---|---|---|
| Commercial.ElevatorWidthFt | SmallTown.cs elevator footprint | unused | wired |
| Commercial.ElevatorDepthFt | SmallTown.cs elevator footprint | unused | wired |
| Commercial.ElevatorRightChance | SmallTown.cs elevator side draw | unused | wired |
| Commercial.BinSpacingFt | SmallTown.cs grain-bin row | unused | wired |
| Commercial.CrossStreetShopMax | SmallTown.cs cross-street shop draw | unused | wired |

These defaults equal the existing SmallTown aliases. Non-default Commercial values take precedence; otherwise the SmallTown value remains effective, preserving saved profiles. An explicit Commercial value equal to its default cannot override a changed SmallTown alias. This compatibility limitation remains until schema ownership is consolidated. No distributions or defaults changed. Removed the unused fresh CommercialConfig local, which had no consumer.

CrossStreetShopMax=0 now skips the random draw and creates no cross-street shops; the previous RangeInt(0,0) divided by zero. Default maximum remains 3 and preserves random draws.

Evidence: commercial-selftests.txt checks four seed-1993 baseline hashes, five control mutations across seeds 13/14/31/1993, and alias precedence; 28 tests pass. commercial-verify.txt: existing 13 checks pass. commercial-build.txt: 69 warnings, zero errors, unchanged warning count. Python reference: 61 passed during resume.

Limits: this is a partial T4 increment. Nested countryside/world town creation still does not receive the root Commercial section. Other Commercial settings, Residential, Streets, density wiring and full source-read audit remain unfinished. No tier-1 completion or parity claim.

## Nested town configuration (T4 increment)

CountrysideMap.Generate and WorldMap.Generate/GenerateCell accept an optional townConfig and pass its SmallTown, TownTiers and Commercial sections to every generated town, without globals or caches. Existing callers without it retain legacy defaults. CLI country/world/cell, HTTP world generation and the settings audit now pass the loaded config. World helper/probe methods retain their default behavior.

Generation reads: CountrysideMap.cs:34; WorldMap.cs:1007. Call sites can be located with `rg -n townConfig src/SpawnGuide`. This also corrects HTTP world generation previously discarding World and Noise config. Custom configuration output can therefore change intentionally.

Verification: nested-build.txt (69 warnings, zero errors), nested-selftests.txt (29 pass), nested-verify.txt (13 pass). The new check compares default countryside/world SVGs with omitted vs explicit default town config at seeds 13,31,1993 and tests Commercial.ElevatorWidthFt propagation into countryside and windows centered on actual towns. Existing four smalltown baseline hash checks pass. No parity claim.

Rerun audit: 528 properties, 96 changed, 409 unchanged, one rejected probe, 22 structural/unprobed. This remains a partial inventory, not completed source-read evidence.
