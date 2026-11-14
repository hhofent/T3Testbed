# Decisions already recorded in T3Testbed (hhofent/T3Testbed, master `cfddcfc`)

Every decision found in the repo's own records, with its source. The port must honour these unless the user overrides one in `SETTINGS-CLASHES.md`.

**Sources read:**
- `STATE.md`, `README.md`;
- `docs/` (`work-in-progress.md`, `todo.md`, `proposal-infinite-map.md`, `archive/handoff-2026-11-11.md`, and everything in `docs/research/`);
- the `review-*/` folders, which hold only rendered SVG/HTML/PNG and no notes;
- code comments that mention the user (only `CountrysideMap.cs:29`);
- all 28 commit messages on GitHub (`gh api repos/hhofent/T3Testbed/commits`; the local clone is shallow and has only `cfddcfc`, "Initial commit").

There are no issues and no pull requests.

**Who decided:**
- **U (user's own call):** the record quotes the user, or marks the item "(user …)", "user call", "user rule" or "user asked".
- **UC (user-confirmed agent proposal):** an agent proposed a package of answers in the grilling log and the user replied "All confirmed" or "Confirmed". The user signed off, but the specific numbers were the agent's.
- **A (agent default):** an implementation choice the agent recorded with no user sign-off.

**Note on dates:** the grilling log (`GRILLED_DATA.md`) is dated "2025-01-15", which looks like a tool default. The surrounding docs place it after 2026-11-12.

## U: the user's own decisions

| # | Decision | Source | Port impact |
|---|---|---|---|
| U1 | Walls spawn already fastened: no raise, anchor or nails flow | STATE.md:23; handoff-2026-11-11.md:24 | None for 2D |
| U2 | A rural lot is the building cluster and its yard only; fields are separate and not part of the lot | STATE.md:24-25; handoff:56 | Honour it. Clash row 42 is about lot size only |
| U3 | The gambrel barn is rare: a labeled *existing* pre-1990s barn on a minority of farms | STATE.md:26-27 | Resolves clash row 46 |
| U4 | `out/example.html` (a single rural farmstead) is the default output; `out/index.html` is the whole spread | STATE.md:28-29; handoff:66 | Keep the outputs as they are |
| U5 | Town density ladder: a hamlet about every 30 mi, very rare under ~10 mi apart and nearly certain by ~50 mi. The middle class is about 1 per 30 mi. The largest class is about 1 per 100 mi and is the biggest a town ever gets. Farmsteads a few per section; most of the map is fields | STATE.md:44-47; calibration-references.md:102-108; commit bf22092 | Resolves clash row 17. The size classes are named generically (U12) |
| U6 | Build one small town first, then grow it; no spawn-frequency field yet | STATE.md:48-50 | Superseded: the world lattice was built later (STATE.md:73) |
| U7 | **Land order: hills → rivers → rail → roads → towns/lots.** Each layer constrains the next | STATE.md:55-56; commit 0755711 | Superseded by N1/N3: roads before rail |
| U8 | World roads: a paved corridor every ~10 mi, and towns always sit on one; gravel on every mile line; rare exceptions both ways. Rail every ~75 mi, E-W and N-S; towns *tend* to be near rail. Rivers run south and tributaries merge into bigger rivers. Hills stay low (<130 ft relief) | STATE.md:57-61 | Resolves rows 2, 3, 9; proposes row 6 |
| U9 | (Refined by N4) **Rail crosses roads square:** "they must only cross perpendicularly. they may, on occasion, cross diagonally, but it must be mostly perpendicular, never almost parallel" | commit 76c6256 (quoted); STATE.md:62-66; todo.md:177-189 | **Conflicts with ours** (row 10) |
| U10 | (Superseded by N5) **"Town on the end of a branch spur":** a town near the rail gets a right-angle branch spur, with the grain elevator at its end | commit 76c6256 item 2 (quoted); STATE.md:66-70 | **Conflicts with ours** (row 11) |
| U11 | "They must not have streets on the outside with no lots on the outside of the streets": town edges are built out | commit 76c6256 item 3; commit 63c255b item 1 (quoted); STATE.md:71-72; todo.md:191-198 | **Conflicts with ours** (row 20/21 group) |
| U12 | **Town names are generic; Boyden/Hull/George are gone from the world.** A 40-name generic pool, and generic sizes crossroads / hamlet / village / town. In grilling: "Name presets by size (Crossroads, Hamlet, Village, Town). Remove Boyden/Hull entirely." The code still keeps them as CLI aliases, which was never finished | STATE.md:78-81; GRILLED_DATA.md:230; proposal-infinite-map.md:90-95; IMPLEMENTATION_PROMPT.md:63 | **Boyden and Hull come out of the port everywhere.** Row 18 uses the generic tiers |
| U13 | (Superseded by N7/N8) **Streets form a loop, not a cul-de-sac maze:** a perimeter street with a jog, every street end meeting another street, and exactly one dead end (two for the biggest town) with four empty lots | STATE.md:82-85 | **Conflicts with ours** (rows 20/21) |
| U14 | Towns vary in which side of the highway they sit: 46% north, 24% south, 30% straddling | STATE.md:86-87 | Resolves row 27. The split is listed under the user note, though the numbers may be the agent's |
| U15 | House walls must not fatten when zoomed: non-scaling stroke at 0.95 px | STATE.md:88-90 | Keep on the new control page's map |
| U16 | Max zoom is 300× | STATE.md:91 | Keep on the control page |
| U17 | Trees render as trunk dots only, with no canopy disks | STATE.md:144 | Keep. Reaffirmed by N2 (no return to the foliage era) |
| U18 | The town silhouette is a seeded blob, never a rectangle | STATE.md:145-146 | Compatible with our "misshapen" rule; see rows 20/21 |
| U19 | (Relaxed by N8 for dead-end stubs; no orphan segments) **Hard rule: every road attaches to another road at both ends, every time** | STATE.md:150-158; commit ac22ef4 | **Conflicts with our 30% dead-end stubs** (row 21) |
| U20 | **No trees along roads or on lots; towns are tree-free.** The country has field groves and farmstead belts. River banks are the exception: the user asked for trees around rivers | STATE.md:159-161 | **Placement part superseded by N2** (trees allowed where realistic, as trunk dots that cover nothing) |
| U21 | Partial-town cells: a town clipped at the cell frame, partly town and partly rural | STATE.md:170-172; CountrysideMap.cs:29 | Keep as a view mode |
| U22 | Goal: "a 3 mile square procedurally generated map that is reproducible with a seed and continues on for hundreds of miles when the 'lens' is slowly moved" | proposal-infinite-map.md:8-11; handoff:129-135 | Resolves row 1 (endless world, with our region as a test view) |
| U23 | The four asks of 2026-11-12: (1) generic business names, so "Casey's" becomes a gas station; (2) all the characteristic town extras, plus more kinds; (3) one rural driveway per lot reaching the machine shed, a walk that follows the drive and curves to the door, and fields with their own entrances; (4) strip, civic buildings and layout not in the same order in every town, but still like a real town | work-in-progress.md:10-21 | Resolves rows 29 and 41, and the field-entrance row |
| U24 | Grilled 2026-11-12: business labels are descriptors with invented flavour (no brands, no reference-map places); 0-2 extras per town drawn by seed; one-lane rural drive; 1-2 field entrances per quarter section | work-in-progress.md:23-30 | Same as U23 |
| U25 | "Have sidewalks leading to the houses, have driveways leading to the garages. Have dirt paths for rural lots on gravel roads" | todo.md:17-20; town-composition.md:173 | Resolves row 40 (together with ours) and the sidewalk row |
| U26 | "Towns still have extra roads. Maybe add a final step to fill with lots all roads in towns, save the few empty ones" | todo.md:58-61; commit 6b2ae17 | Resolves row 26 |
| U27 | (Superseded by N12) **"Commercial should be scattered along the major town roads, not all clumped together"** | commit 63c255b item 2 (quoted) | Replaced by N12: downtown main street plus some highway strip |
| U28 | The user provided the calibration anchors (Hull large, Boyden medium, an average farm, typical land), viewed in road/label mode | calibration-references.md:7-19; handoff:112-126 | **Superseded** by U12 and U30 (references must be generic). Not port targets |
| U29 | Grilling round 1 (G-001): priority order gameplay utility > infinite variety > performance > visual authenticity > controllability. Byte-for-byte determinism. Out of scope: narrative, traffic, crops, seasons. Platform: Windows only. **References are generic archetypes, not specific real towns** | GRILLED_DATA.md:26-38 | Honour all of it. The build must work on Windows |
| U30 | G-002: the settings hierarchy follows real-world generation order; settings are grouped both by system and by concern; declarative correlation rules | GRILLED_DATA.md:70-81 | Already in the config system |
| U31 | G-003 philosophy: do everything the realistic way, with manipulation settings and a rationale comment on every setting. **Only 3 deviations from reality: (1) a 1990s setting, (2) uniformly low density (the lowest of NW Iowa) everywhere, (3) towns capped at the size of the smallest Iowa cities** | GRILLED_DATA.md:113-114, 130, 134-136 | Deviation 1 **conflicts with our 1985-2000 snapshot years** (row 36). Deviation 2 bears on row 43; deviation 3 matches our largest band (750-1,499) |
| U32 | Cattle yards: the user has never confirmed them, so they are not built | calibration-references.md:42; todo.md:204-206 | Don't add them |

## UC: user-confirmed agent proposals (the user replied "All confirmed")

| # | Decision | Source | Port impact |
|---|---|---|---|
| UC1 | Nine extra pipeline stages (geology, soils, wetlands/floodplains, vegetation, historical roads, PLSS, economic hierarchy, infrastructure, demographics) and the 15-step geological→historical order; Phase 2 | GRILLED_DATA.md:116-133, 302-303 | Out of scope; don't build |
| UC2 | Wetlands/floodplains: **no buildings in the floodway** (hard constraint) | GRILLED_DATA.md:119 | Agrees with our floodplain rule; resolves row 7 |
| UC3 | Hills fBm; rivers with LogNormal spacing and Perlin meander, tributaries by Strahler order; rail swell fBm, spurs perpendicular with an elevator, 5.5 mi max; roads 10 mi paved spacing, 1.8%/2.2% exceptions, **300 ft farm margin**; towns **9 mi rail proximity**, Dirichlet class weights; **farm density 0.15**, max 3 per section | GRILLED_DATA.md:171-186 | The 9 mi rail requirement **conflicts with our "towns don't need rail"** (row 12). The 300 ft margin resolves row 8. 0.15 vs "a few per section" (U5) disagree inside T3 (row 43) |
| UC4 | Town tiers: generative k=3 central-place system, presets named by size, **remove Boyden/Hull** | GRILLED_DATA.md:230 | Same as U12 |
| UC5 | Town extras as a feature grammar (prerequisites, exclusions, placement, weights; 0-2 emerges from the weights) | GRILLED_DATA.md:232 | Keep |
| UC6 | Highway commercial vs rail-side industrial; "rails = crops only" | GRILLED_DATA.md:234 | Proposed for row 13 and 33 |
| UC7 | Civic buildings by population threshold, churches scaled per capita, consolidated schools | GRILLED_DATA.md:250 | Proposed for row 34 |
| UC8 | Town setbacks 25 ft front, 7.5 ft side, 25 ft rear | GRILLED_DATA.md:236 | Agrees with our approved rule 2 (front 25, sides 6-8, rear 25-35); resolves row 25 |
| UC9 | Garage modes Dirichlet 55/35/10 (integral/detached/none); concrete drives and walks; Poisson yard trees | GRILLED_DATA.md:236 | Proposed for row 39. Yard trees are overridden by U20 |
| UC10 | Streets: 66 ft ROW, 32 ft paved, 16 ft gravel alleys clustered by a Beta distribution; perimeter loop with a north jog; 70% four-sided blocks | GRILLED_DATA.md:238 | Resolves rows 4, 5 and 22 |
| UC11 | Farmstead evolution (homestead → 1990s); shelterbelts as a mature 1930s PSFP legacy, 3-7 rows on N/W; machine sheds sized for 1990s equipment | GRILLED_DATA.md:240; IMPLEMENTATION_PROMPT.md:67 | Resolves row 45 (together with ours) |
| UC12 | House dimensions LogNormal, Beta fractions, bimodal garage doors (9/16 ft), 1990s siding market | GRILLED_DATA.md:242 | For their archetype houses; our room-layout spawner replaces them (row 37 is still open) |
| UC13 | New archetypes: Coop Corner (section-line corner, needs rail) and **Trailer Court** (town fringe, curved street, 40x70 pads; Phase 2) | GRILLED_DATA.md:244, 303 | Dropped: parks are out of the port (N13) |
| UC14 | Phase 1 build list and Phase 2 split | GRILLED_DATA.md:302-303 | Context |
| UC15 | Parameter Explorer UI as static HTML at **`/params`**: config tree (system + concern), inputs for every type, live SVG preview with **300 ms debounce**, diff view, profile selector (8 profiles), export/import JSON, hover tooltips, validation colours (red = hard, yellow = soft) | GRILLED_DATA.md:305 | **The new control page is this page.** Serve it at `/params` |
| UC16 | Sensitivity CLI: `--param`, `--range`, `--steps`, `--seeds`, `--metrics` (houses, outbuildings, blocks, vacancy, roadLength, townArea, farmCount, riverLength, railLength), `--output` CSV/JSON, `--parallel` default on, linear spacing with a log option | GRILLED_DATA.md:307 | Keep these flags and metrics; ours are added on top |
| UC17 | Config migration: SchemaVersion, auto-migrate with a `.backup.v{N}`, `--migrate-config`, `--validate-config`, `--diff-config` | GRILLED_DATA.md:309 | Keep; bump the schema when adding the Builder Guy section |
| UC18 | 12 string fields → 4 enums, case-insensitive with aliases; unknown → warn + default | GRILLED_DATA.md:311 | Keep |
| UC19 | Test types: `--verify`, `--test-sensitivity`, `--test-visual`, `--test-edge-cases`, `--test-migration`, `--test-profiles`, `--benchmark`; golden masters in the repo | GRILLED_DATA.md:313 | Mostly unbuilt; the port's `--selftest` should cover profiles, edge cases and migration |

## A: agent defaults (recorded, but no user sign-off)

| # | Default | Source | Note |
|---|---|---|---|
| A1 | mulberry32 RNG | STATE.md:35 | Our parity mode uses BLAKE2b instead; keep mulberry32 for T3 content |
| A2 | A 3-mile cell is a request unit, not a render tile; content is pure in world coordinates | STATE.md:51-54; proposal:33-37; work-in-progress.md:44-46 | A sound invariant. Keep it |
| A3 | Town lattice: one candidate per 10-mi slot; a town only where rail runs within 9 mi; 20-mi suppression; a trunk river within 1,200 ft vetoes the site | STATE.md:73-77; proposal:81-89 | The 9 mi rule was later confirmed (UC3). The river veto works against our "towns bent by rivers" |
| A4 | Class weights: town 4.5%, village 9%, hamlet 18.5%, crossroads 32%, the rest empty | proposal:84-85 | Row 18 |
| A5 | Radial vacancy 14/5/1.5%; plat-pass vacancy 22% → 10% | STATE.md:146-147; commit 63c255b | Row 26 |
| A6 | Garages 45% detached / 45% integral / 10% none; ~12% sheds; ~40% half lots (34-47 ft) | STATE.md:162-166 | Disagrees with UC9 (55/35/10) |
| A7 | 36 ft town street ROW in `SmallTown.cs` | STATE.md:180 | Disagrees with UC10 (66 ft) |
| A8 | Extra-street kinds beyond the researched five (industrial side street, boulevard, court, plat edge) and their rates (court 50% … boulevard 16%; 35% of towns get none) | work-in-progress.md:32-36, 92; commit fc5d226 | The user only asked for "plus more kinds" |
| A9 | Landmark list and rates (water tower near-universal, golf/fairgrounds/airstrip villages and up, …) | commit 8307cd5; work-in-progress.md:165-171 | Row 35 |
| A10 | Business catalogue (16 shops + 5 industrial), tier gating, roomy businesses pushed to the ends, 14/10/6/3 shops by tier | commits 08f1d44, 63c255b | Row 28 |
| A11 | Field entrance geometry: 26 ft wide, 95-170 ft long, 110 ft back from intersections | work-in-progress.md:86-89 | Fine |
| A12 | Rail offset 40-210 ft from its own road, one-sided swell, lattice phased three sections off the corridors | STATE.md:63-66; commit 19dc583 | The agent's way of meeting U9 |
| A13 | Repo workflow: STATE.md rewritten in place, write-once research notes, git log as the journal; verify renders geometrically; never inline large images | STATE.md:36-43 | Keep the workflow |
| A14 | The 13-check verify ladder | STATE.md:120-124 | Two of its checks enforce user rules U9 and U13/U19 |
| A15 | Grilling round 2 details (hybrid merge/delta/profile inheritance, hard/soft/repair validation, JSON + binary cache + hot reload). The user only answered "need explanation" / "suggestions welcome" | IMPLEMENTATION_PROMPT.md:38-46 vs GRILLED_DATA.md:72-76 | Built; keep it |
| A16 | `review-*/` folders (rendered SVG/HTML/PNG for seeds 1993, 42, 2024) | `review-*/` | Outputs only; no decisions in them |

## Inconsistencies inside T3Testbed
- Street trees: config default `streetTreeChance 0.75` and Poisson(2.5) yard trees (SpawnConfig.cs:485, 489) vs U20 "no trees along roads or on lots".
- Farm density: UC3's 0.15 vs U5's "a few per section".
- Garages: UC9's 55/35/10 vs A6's 45/45/10 in code.
- Town street width: UC10's 66/32 ft vs A7's 36 ft ROW.
- Boyden/Hull: U12 says "gone", but `SpawnConfig.cs:528-529`, `spawnconfig.json:276,287`, `SmallTown.cs:50-51`, `Program.cs:565,735`, `CountrysideMap.cs:14-15`, `TownStreets.cs:10`, README.md:35-42/83/117 and STATE.md:98-100,140,170-172 still use them.

## N: new user decisions, Nov 13, 2026 (answers to the 8 conflicts plus notes)
These are the user's own calls, from the chat with Coordinator Cal on Nov 13, 2026. They override any older T3 or Builder Guy entry they contradict.

| # | Decision (user's words, then how it's applied) | Overrides |
|---|---|---|
| N1 | "Generated in order generated in real life, for natural looks." Every layer is generated in the order it formed historically: natural land (hills, rivers, creeks) first, then roads, then rail, then towns, lots and buildings | Refines U7 |
| N2 | "Trees can be elsewhere, but they should obstruct other things on the map", clarified as "i mean the trees used to have foilage that made it look like sateliette" and "NOT like a sateliette photo, was all i meant. In the history of t5testbed there was a time where trees had foilage on the map and i did not want to return to that era". **Trees may be placed anywhere realism allows** (not only rivers, windbreaks and groves), but they **draw as simple trunk-dot markers that never cover or hide roads, buildings, driveways or rail.** No canopy layer, no foliage. **The era to avoid:** T3 commits `bf22092` ("street trees, heavy canopy"), `ed62939`, and up to `35a63ca`, which switched to "trunk-location dots, no canopy fills" (STATE.md:144, "no canopy disks (user call)"; README.md "heavy canopy" text) | Keeps U17 (trunk dots); relaxes U20 (tree-free towns) for placement |
| N2a | Placement rule: a tree marker never sits on a road surface, building footprint, driveway or rail corridor. Density per setting is a placeholder. Verify: no tree marker intersects those features | — |
| N3 | "Roads first." Historical road model: wagon trails from westward expansion come first; some get upgraded; every mile line becomes gravel. End state: main roads paved, most everything gravel, some still dirt (Level B roads), some missing entirely (extremely rare). Shares are placeholders | U7 (rail before roads), U8 road details |
| N4 | Rail "can be diagonal, but must be mostly 90deg crossings. Never an extremely acute angle." Applied as a minimum crossing angle of 45° (PLACEHOLDER), with ≥80% of crossings within 15° of square (PLACEHOLDER), both enforced in verify | U9 (square only) |
| N5 | "Rail runs through town in the industrial zone, maybe sometimes commercial and rarely residential." No branch-spur-end rule | U10 |
| N6 | A town "can be far away and not have rail — realism" | UC3 9-mi rule, A3 |
| N7 | Town edges: "realism". Applied as our misshapen approach (roads, rail, creeks/rivers shape the town), with no forced loop street | U13 loop, U18 (still not a rectangle) |
| N8 | Dead ends: "realism". Applied as our ragged dead ends instead of exactly one, but every road still connects somewhere sensible: no orphan segments | U13 one-dead-end, relaxes U19 for stubs |
| N9 | Year: "focused on 1990". The default snapshot is 1990; 1985/1995/2000 stay as a setting | U31 "1990s" (compatible) |
| N10 | (Superseded by N13) Trailer parks: "t3 trailer court isnt really designed yet, is it? im not sure which, maybe another entirely or drop it." Checked: T3's Trailer Court is only one line in the grilling log (GRILLED_DATA.md:244, Phase 2), with no config class, code or doc. **Status: OPEN.** Neither model builds park layouts; leave a stub; keep in-block trailer clusters | UC13 Trailer Court; parks on hold |
| N12 | Shops: "Main street downtown plus some spread along the highway" (user's answer, Nov 13, 2026). Our attached main-street storefront downtown, plus some businesses along the highway/main roads; highway-type businesses (gas station, implement dealer, farm supply, lumber yard, motel, auto repair) go on the strip. The downtown/strip split is a setting, default 0.7/0.3 (PLACEHOLDER) | U27 (commit 63c255b, all scattered) |
| N13 | Trailer parks: "Drop trailer parks entirely" (user's answer, Nov 13, 2026). No park layouts, stub, switch, preset or check in the port. Keep in-block trailer clusters. The Python reference keeps its park code; the port excludes it, and parity leaves out park metrics | N10 (OPEN), UC13 Trailer Court, our park rules |
| N11 | Priority principle for unanswered rows: the user answered "realism" to three of the conflicts, so a realism argument should guide future proposals. Cal's inference, not a user rule | — |
