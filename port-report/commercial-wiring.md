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
