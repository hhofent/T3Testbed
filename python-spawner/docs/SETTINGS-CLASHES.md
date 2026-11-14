# Settings clashes: Builder Guy Python spawner vs T3Testbed (master cfddcfc), revision 3 (user answers of Nov 13, 2026 applied)

The user fills in the **Decision** column. The coding agent must not pick for any row. If a row it needs is blank, it stops and asks.

**Sources:**
- **Ours** = `/workspace/building-spawner-2d/` (repo copy: `reference/python-spawner/`). CRG = Construction Research Guy research; CEN = Census-weighted; USER = an earlier Builder Guy user decision (README or memory); PH = placeholder.
- **Theirs** = `src/SpawnGuide/`.

**Proposed answers:** "**T3 decision (source)**" means T3Testbed records a decision for that row. The codes (U#, UC#, A#) refer to `T3-DECISIONS.md`:
- **U** = the user's own call;
- **UC** = an agent proposal the user confirmed in bulk.

A proposed answer is the default. To override it, write your own Decision.

**Conflicts:** the user answered all 8 ⚠ CONFLICT rows on Nov 13, 2026 (section N; `T3-DECISIONS.md` N1-N13). Two more user answers followed the same day: shops (N12) and trailer parks dropped (N13). No row is left open for the user.

**Decision options:** `ours`, `theirs`, `both` (a setting or preset switches between them), or a custom value.

**No real towns:** Boyden and Hull (and George) are not size names, presets or reference targets anywhere in the port (T3 U12, U29). Sizes use T3's generic tiers.

## N. Resolved by the user's answers, Nov 13, 2026 (the user's own words are in `T3-DECISIONS.md` N1-N13)
| # (old) | Topic | Resolved to | Source |
|---|---|---|---|
| — | General principle | Generate things in the order they formed in real life, for natural looks | N1 |
| P1 | Pipeline order | **Land first, then roads, then rail, then towns and lots:** hills → rivers/creeks → roads (historical model below) → rail → towns → lots → buildings. Replaces T3's U7 order (rail before roads) | N1, N3 |
| 2, 3 | Road network (replaces the section R entries for rows 2 and 3) | **Historical road model:** wagon trails from westward expansion come first; some get upgraded; every mile line becomes gravel. End state: main roads paved, most roads gravel, some still dirt (**Level B** roads), and a few segments missing entirely (extremely rare). Shares are placeholders: paved main roads per T3's ~10 mi corridors plus upgraded trails, Level B dirt ~5% (PH), missing ~1% (PH) | N3 |
| 10 | Rail crossings | Diagonal crossings allowed, but **mostly ~90°** and never at an extremely acute angle. **Minimum crossing angle 45° (PLACEHOLDER)**, enforced by verify, with a placeholder target that at least 80% of crossings fall within 15° of square | N4 |
| 11 | Rail and towns | **Rail runs through town, in the industrial zone**; sometimes through the commercial area; rarely through residential. Depot, siding and elevator sit beside it. No "town at the end of a branch spur" rule | N5 |
| 12 | Does a town need rail? | **No.** A town can be far from rail and have none (realism). Drop T3's "rail within 9 mi" rule (UC3) | N6 |
| 20 | Town outline | **Realism, our approach:** misshapen by roads, rail and creeks/rivers (ours USER). No forced perimeter loop street | N7 |
| 21 | Dead ends | **Realism, ours:** ragged dead ends (our ~30% stub rate, PH) instead of exactly one. Every road must still connect somewhere sensible: no orphan segments, and each dead end hangs off a connected street. Replaces T3 U13's "exactly one" and relaxes U19 for stubs | N8 |
| 36 | Year | **Focused on 1990:** the default snapshot is 1990. 1985/1995/2000 stay available as a setting (`BuilderGuy.SnapshotYear`); parity mode keeps Python's per-seed year | N9 |
| 39 | Garage mix | Follows the year: the 1990 census mix by default (ours CEN), not T3's 55/35/10 | N9 |
| C (trees) | Trees | **Trees may be placed anywhere realism allows,** not only by rivers, windbreaks and groves; that includes town yards and farmsteads. They **draw as simple trunk-dot markers (T3 U17 stays)** that never cover or hide roads, buildings, driveways or rail: no canopy layer and no foliage. A marker never sits on a road, building, driveway or rail. **Avoid T3's old foliage era:** `bf22092` ("street trees, heavy canopy") through `35a63ca` ("Trees are trunk-location dots, no canopy fills"; STATE.md:144 "no canopy disks (user call)"). Relaxes U20 (tree-free towns) for placement only | N2 |
| 31 | Shops (main street form) | **Our attached main-street downtown** (25 ft storefronts, zero side setbacks, curb parking, alley behind, ~6 block faces max), **plus some businesses spread along the highway/main roads.** Highway-type businesses go on the strip: gas station, implement dealer, farm supply, lumber yard, motel, auto repair. The split is a setting, `BuilderGuy.Commercial.DowntownShare`, default **0.7 downtown / 0.3 strip (PLACEHOLDER)**. Overrides T3 commit `63c255b`'s all-scattered rule | N12 |
| — | Mobile home parks | **Dropped entirely.** The port has no park code, stub, switch, preset or check. Keep in-block trailer clusters. The Python reference keeps its park code; parity leaves out park metrics | N13 |

## R. Resolved by a T3 decision (removed from the clash list): 19 rows
These follow the T3 decision unless you write something else here. The original row numbers are kept.

| # (old) | Topic | Resolved to | Source | Override? |
|---|---|---|---|---|
| 1 | World model | **Both**: the infinite world is the default; our 3x3 mi region stays as a test view inside it (you also chose "include the endless world" for this port) | T3 U22; port grilling batch 1 | |
| 2 | Paved roads | *Superseded by section N (historical road model).* Paved main corridors about every ~10 mi; towns sit on a paved road | T3 U8; N3 | |
| 3 | Missing road lines | *Superseded by section N:* missing segments are extremely rare (~1% PH), and some lines are Level B dirt | N3 | |
| 4 | Town street ROW / surface | 66 ft ROW, 32 ft paved (the `SmallTown.cs` 36 ft const goes) | T3 UC10 (GRILLED_DATA.md:238) | |
| 5 | Alleys | 16 ft gravel, clustered with a Beta distribution (not on every block) | T3 UC10 | |
| 7 | Floodplain rule | Nothing built in the floodplain/floodway; towns stop at it (ours USER and T3 agree). T3's 1,200 ft river veto on town sites is an agent default (A3); it gives way to our "towns bent by rivers" | T3 UC2 (GRILLED_DATA.md:119); ours USER | |
| 8 | Rural intersection clearance | 300 ft farm margin; field entrances 110 ft back | T3 UC3 (GRILLED_DATA.md:176) | |
| 9 | Rail frequency | One line every ~75 mi, E-W and N-S (rail becomes rare; our 60% is PH) | T3 U8 (STATE.md:59) | |
| 17 | Town spacing | Hamlet about every 30 mi (rarely under 10, nearly always by 50); village ~1 per 30 mi; town ~1 per 100 mi. Fits our USER "a town is very rare". Region test view may still force a town | T3 U5 (STATE.md:44-47) | |
| 22 | Block form | 70% four-sided blocks | T3 UC10 | |
| 25 | Town setbacks | Front 25, side 7.5, rear 25 ft. Matches our approved rule 2 (front 25, sides 6-8, rear 25-35, may shrink on tight lots) | T3 UC8; ours USER rule 2 | |
| 26 | Vacant lots | Towns built out: every street lotted, "save the few empty ones" (the dead-end rule is now N8) | T3 U26 (todo.md:58-61) | |
| 27 | Highway position | Towns sit north 46% / south 24% / straddling 30% of the highway | T3 U14 (STATE.md:86) | |
| 29 | Business names | Descriptors with invented flavour ("GAS STATION", "CEDAR VALLEY FEED"); no brands, no real places | T3 U23/U24 (work-in-progress.md:12, 27) | |
| 31 | Main street form | *Superseded by N12 (section N).* | T3 U27 → N12 | |
| 40 | Town driveway | Every lot: drive to the garage (or a pad beside the house when there's no garage); walks from the sidewalk to the front door. Ours USER and T3 U25 agree | T3 U25 (todo.md:17-20); ours USER | |
| 41 | Rural lane | One lane per lot, leaving the road at ~90°, up past the house to the machine shed and bins, with a garage branch; the walk follows the lane and curves to the door. Our apron touches every vehicle building (ours USER), so both hold | T3 U23/U24 (work-in-progress.md:16-18, 29); ours USER | |
| 45 | Grove / shelterbelt | Mature shelterbelt on N and W (1930s planting); matches ours | T3 UC11 (GRILLED_DATA.md:240) | |
| 46 | Farm buildings | T3's varied set; the gambrel barn is a rare, labeled *existing* pre-1990s barn on a minority of farms (our always-barn was PH) | T3 U3 (STATE.md:26) | |

## A. Clashes still open: 20 rows (none are conflicts any more)
9 rows have a proposed answer. The other 11 (marked "—") are plain value choices. **If you leave one of those blank, the default is `ours`** (the Python value), kept as a setting so you can switch it later. That's Cal's default, picked because the port keeps our towns' look; it isn't your decision.

### A0. Pipeline
| # | Topic | Ours (source) | Theirs | Proposed answer | Decision |
|---|---|---|---|---|---|

### A1. Water
| # | Topic | Ours (source) | Theirs | Proposed answer | Decision |
|---|---|---|---|---|---|
| 6 | Water | Creeks: 0/1/2 per 3-mi region (30/50/20), diagonal, 20-60 ft channel, 150-300 ft floodplain, bridges (PH); towns bent by creeks (USER) | Trunk rivers run south every ~28 mi, tributaries every ~8 mi merging into them, trees on banks | T3 decision (U8, U20) for the river network. Open part: do our smaller creeks also appear (for example as the tributary level)? | |

### A2. Rail and co-op
| # | Topic | Ours (source) | Theirs | Proposed answer | Decision |
|---|---|---|---|---|---|
| 13 | Elevator site | 3-6 ac beside the track (CRG); truck elevator in no-rail towns 43/55/67% (CRG; our README also records "elevators stay rail-town-only" as an earlier user decision) | Elevator on the spur; co-op corner lot needs rail; "rails = crops only" | T3 decision (UC6, UC13) for rail towns. Open: keep our no-rail truck elevator? | |
| 14 | Co-op office | 30x50 ft on the elevator lot (CRG; lot placement USER) | 40±10 ft office + 20 ft scale house | — (no T3 decision) | |
| 15 | Siding length | 1,500 ft (PH) | 600 ft | — | |
| 16 | Feed mill | One 200x300 ft parcel by the elevator in rail towns (PH) | 30% chance, 40±8 ft building; rail-side works (cabinet shop, metal fab, feed mill) | — | |

### A3. Towns: size and shape
| # | Topic | Ours (source) | Theirs | Proposed answer | Decision |
|---|---|---|---|---|---|
| 18 | Size classes | Tiny/mid/big 60/30/10 (PH); business bands <250, 250-749, 750-1,499 people (CRG); "some towns bigger, occasionally the largest size" (USER) | Generic crossroads/hamlet/village/town; capped at the smallest Iowa cities | T3 decision (U12, U31): the four generic tiers. Proposed band mapping: crossroads + hamlet → <250, village → 250-749, town → 750-1,499. Tier weights from the density ladder (U5) | |
| 19 | People per house | 2.4 (PH) | 2.5 per lot | — | |
| 23 | Block / lot depth | Blocks 380-445 ft; lots 130-150 ft deep (CRG) | Lot depth 122 ft; side lots 80 ft | — | |
| 24 | Lot frontage | 50 ft base lot; a house takes 2-3, a trailer 1 (CRG + USER) | 58-92 ft plat-pass frontage; 50-100 ft LogNormal | — | |

### A4. Businesses and civic
| # | Topic | Ours (source) | Theirs | Proposed answer | Decision |
|---|---|---|---|---|---|
| 28 | How businesses are chosen | Each type at the CRG 1998 ZBP rate for the band, scaled by year (USER: "follow research on small-town Iowa businesses") | Weighted catalogue gated by tier; strip randomised but keeping real-town similarities | Partial T3 decision (UC6 weighted types; U23 randomised order). They can merge: our CRG rates as the catalogue weights | |
| 30 | Business type list | Adds tavern, beauty, insurance, locker plant, funeral home, nursing home, vet, school, city hall/fire, vacant storefronts (CRG) | Adds motel, laundromat, restaurant, machine works, steel works, mfg plant, pharmacy | — | |
| 32 | Gas station | Blacktop corner, 120-150 ft lot (CRG) | Every town has one, as a strip pad | — | |
| 33 | Edge businesses | Implement dealer, auto repair, vet, nursing home, school, lumberyard, farm supply at the town edge (CRG) | Industrial side street and a rail-side industrial strip | T3 decision (UC6) for rail-side industry; the rest is open | |
| 34 | Churches / school | Church 41/78/92%, 2nd church 0/25/50%, school 0/30/70% by band (CRG/PH) | By population threshold, churches per capita, consolidated schools | T3 decision (UC7) for the method; values open | |
| 35 | Landmark rates | Cemetery outside town on the blacktop (CRG/PH) | Water tower near-universal, golf/fair/airstrip in villages and up, cemetery at the edge (A9) | — (agent rates) | |

### A5. Houses and rural
| # | Topic | Ours (source) | Theirs | Proposed answer | Decision |
|---|---|---|---|---|---|
| 37 | House generator | Room-layout spawner, 35 approved rules, rule 35 look-alikes (USER rules) | Three wall archetypes (ranch, farmhouse, split-level) | — | |
| 38 | House size | 1,600-2,000 sf scaled to the lot (rule 23 + PH) | Ranch ~1,000 sf; farmhouse 30x33 two-story | — | |
| 42 | Farmstead lot | 300-500 x 400-800 ft, 3-9 ac (CRG) | 180-350 x 250-500 ft; lot = buildings and yard only, fields separate (U2) | Lot meaning: T3 decision (U2). Size open | |
| 43 | Farmstead count | ~1.2 per road mile (CRG) | "A few per section" (U5) vs density 0.15 (UC3); uniformly low NW Iowa density (U31) | T3 decision (U31): uniformly low. The number is open, because T3 contradicts itself | |
| 44 | Rural house setback | 100-300 ft (CRG) | 50±10 ft | — | |

## B. Only ours has it (carry over unless the user says otherwise)
| Topic | Ours | Proposed answer | Decision |
|---|---|---|---|
| Trailers | 14-16 x 60-76 ft single-wide, gravel pad; in-block clusters on ~20% of block faces, 2-4 trailers (USER, PH) | Keep (the only place trailers go; parks dropped, N13) | |
| **Mobile home parks: DROPPED (N13)** | Removed from the port entirely: no park layouts, stub, switch, preset or check. The Python reference keeps its park code, but the port excludes it, and parity leaves out park metrics. T3's "Trailer Court" was only one line in the grilling log (GRILLED_DATA.md:244) | **Keep in-block trailer clusters** | dropped |
| Acreages | 1-5 ac, house 60 ft back (CRG/PH) | Keep | |
| Country church, fuel parcel | (CRG) | Keep | |
| Vacant storefronts | ~10.5% (1985) → ~15.7% (2000); 1990 default (N9) | Keep | |
| Bank branches | 10% → 50%; 1990 default (N9) | Keep | |
| Funeral home in a converted house | (CRG) | Keep | |
| Rule 35 look-alikes | vs the previous 2 lots on a road (USER) | Keep | |
| Setback scaling by lot size, porch projection | (USER: setbacks scale with lot size) | Keep | |
| Integer 1/16-in geometry, BLAKE2b keyed hashing | Needed for parity | Keep (parity mode); T3's mulberry32 (A1) stays for T3 content | |

## C. Only theirs has it (locked to include: verify, landmarks, extras, presets, world, hills, rivers)
| Topic | Theirs | Proposed answer | Decision |
|---|---|---|---|
| Hills / contours | 20 ft contours, <130 ft relief | T3 decision (U8): low hills | |
| River network | Trunks + tributaries | See row 6 | |
| Verify ladder (13 checks) | Determinism, overlaps, spacing, rivers, rail, streets | Keep, with two rewrites from section N: "rail crosses roads square" → "no rail crossing under 45° (PH), most near square" (N4); "no dangling street ends" → "no orphan road segments; dead-end stubs must hang off a connected street" (N8). "Rail towns get a spur" and "every town within 9 mi of rail" style checks are dropped or rewritten (N5, N6) | |
| Town extras | Court, plat edge, outlying road, bypass, industrial side street, square, boulevard, OLD RAIL diagonal, CRESCENT | T3 decision (U23/U24): all of them, 0-2 per town by seed; rates are agent (A8) | |
| Landmarks | Water tower, post office, fire station, library, ball field, cemetery, depot, golf, fairgrounds, airstrip | Keep (A9 rates) | |
| Sidewalks + front walks | 4.5 ft sidewalks; walks to every door | T3 decision (U25): on | |
| Street / yard trees | Config 75% street trees, Poisson yard trees (not drawn today) | **Resolved by N2:** allowed where realistic, drawn as trunk dots only, never on or over roads, buildings, driveways or rail. No canopy | |
| Small sheds | 12-15% of town houses | — (A6) | |
| Field entrances | 1-2 per quarter section, 26 ft dirt | T3 decision (U23/U24): on | |
| Config system (rules, distributions, presets, sweep, validate, diff, migrate) | | Locked include (UC15-UC18) | |
| Control page | `/params` Parameter Explorer | T3 decision (UC15) + our port choices: the new control page lives at `/params` | |
| Cattle yards | Not built | T3 decision (U32): don't add | |
