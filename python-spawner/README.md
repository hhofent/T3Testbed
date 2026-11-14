# Building spawner: 2D prototype (research only)

This is a Python prototype of the Builder Guy building spawner, formerly called the house spawner. It isn't in a repo, nothing has been pushed, and nobody has been sent anything.

**Pipeline:** lot spec + seed → program draws (rules 21-24, 4, 30) → a fixed adjacency template split on a 2 ft grid, with the footprint snapped to 4 ft → doors and windows → validator → retry with `hash(lotSeed, "attempt", n)`, up to 25 attempts. If an attached garage doesn't fit the lot, the generator tries a smaller garage in this order: double, single, detached. All geometry is in integer 1/16-in ticks. Every seeded choice is `hash(seed, key)`, so adding a new choice never shifts an existing one.

**Template (one story):**
- Public half: the kitchen sits on the road wall next to the garage, and the living room has the front door. Behind them is a dining or family room, plus a utility room or mudroom.
- Bedroom wing: a 4 ft hall, bedrooms on the front and rear, and the baths in the rear row next to each other.
- Each bedroom gets a 2 ft closet on its hall wall.
- The porch goes at the front door and the deck or patio at the rear door. When the garage is attached, the driveway runs in front of it.

## Run
```
.venv/bin/python -m pytest -q tests     # 38 tests
.venv/bin/python run_region.py          # region seed 28 -> out/region.png, out/neighborhood.png, out/town.png, out/region_stats.json
.venv/bin/python batch.py               # 40 seeds -> out/batch.json, out/sample_*.png, out/contact_sheet.png
```

## Files
- `region.py`: the region stage. It takes the region seed and builds the mile-grid roads (gravel or paved), a rare town on a paved road, and rural and town lots, then spawns a building on each lot. It also holds rule 35.
- `spawner.py`: hashing, data model, program draws, layout, lot fit, retry loop.
- `validate.py`: one check per rule. Each result is pass, fail (approved rules only), warn, or not-2D.
- `render.py`: draws the plan PNGs and the contact sheet.
- `batch.py`: runs the batch and writes the stats and samples.
- `tests/`: the determinism and neighbor tests, one mutation per approved rule that the validator must catch, and a small batch smoke test.

## Results (40 seeds, 10 lots each at 60/70/80/100 ft)
- **Success rate:** 82.5% (33 of 40). The 70, 80 and 100 ft lots all succeeded. The 60 ft lots succeeded 3 times out of 10.
- **Average attempts:** 6.5 overall, and 2.6 for the lots that succeeded. By lot width: 60 ft 20.9, 70 ft 2.3, 80 ft 1.5, 100 ft 1.3.
- **Retry causes:** lot fit (rule 2) 206, rule 23 area band 15, rule 30 deck placement 5, rule 8 kitchen to dining door 4, rule 27 secondary entry 2.
- **Warnings:** rule 15 glazing 33 (the dining window is too small for a large dining room), and rule 2 shrunk side setbacks 20 (3 at 60 ft, 8 at 70, 7 at 80, 2 at 100).

**Why 60 ft lots fail.** The smallest house this template makes is 44 ft wide: a 10 ft kitchen, a 14 ft living room, and a 20 ft bedroom wing.
- An attached single garage makes it at least 56 ft wide. That needs side setbacks of 2 ft or less, and the floor is 3 ft.
- An attached double garage makes it 66 ft, wider than the lot.
- A detached garage needs a 13 ft driveway side yard plus 3 ft on the other side. That caps the house at 44 ft wide, and at the maximum 36 ft depth it comes to 1,584 sf, under the 1,600 sf minimum of rule 23.

So a 60 ft lot only works when the program draws no garage (7-13% of draws), and even then it needs shrunk setbacks.

## Not checked in 2D
- Rule 19: stairs.
- Rule 29: roof.
- Rule 35: look-alikes. This was skipped and needs the neighborhood layer.
- Rule 20: spans, headers and studs.
- Rule 24: footings and anchor bolts.
- Rule 16: the net clear opening. Only the sill and the rough opening are checked.
- Rule 10: the R-206 fixture list and fans as real fixtures.
- Rules 5 and 17: door and gypsum ratings, and the landings.
- Rule 18: wiring.
- Rule 25: appliance footprints.
- Rule 31: trap-to-vent distances and stack plates.
- Rule 21: the style is drawn and recorded, but every 2D plan is one story.

## Region stage (rural Iowa, Craig/Struble pattern), updated Nov 10
**Pipeline:** region seed → a 3 x 3 mile section grid of roads, one every mile on the mile.
- **Roads:** most are gravel. Lettered county blacktops are paved first, about 25% of lines.
- **Town:** a rare town sits at the blacktop crossing. It has 4-9 blocks with alleys and an active rail line with a depot, an elevator and a siding.
- **Other parcels:** a cemetery, a fuel station on a blacktop corner, and a country church.
- **Rural lots:** farmsteads (about 1.2 per road mile, with a grove on the N/W sides and farm buildings) and non-farm acreages.
- **Buildings:** the building spawner runs on each lot with seed `hash(regionSeed, lotId)`, and every house gets a drive.
- **Rule 35:** each lot is compared with the previous 2 lots on the same road.

```
.venv/bin/python run_region.py   # seed 9 (natural town) -> out/region.png, out/neighborhood.png, out/town.png, out/region_stats.json
```

### Values
CRG = Construction Research Guy value. Where CRG gave a range, the value is seeded inside it. PLACEHOLDER = no source yet.

| Value | Setting | Status |
|---|---|---|
| Road right-of-way | 66 ft | CRG |
| Surface width | gravel 22-24 ft, paved 22 ft | CRG |
| Paved share | 22-25% drawn, lettered county routes first | CRG. With 8 grid lines, 2 paved lines = 25% always |
| Intersection exclusion (rural lots) | 150-300 ft | CRG |
| Farmsteads per road mile | 0/1/2 weighted 15/50/35, mean 1.2 | CRG mean; the weights are PLACEHOLDER |
| Farmstead size | 300-500 x 400-800 ft, 3-9 ac; house 100-300 ft back | CRG |
| Grove | on the N and W edges | CRG |
| Grove width | 80 ft; house kept 20 ft clear of it | PLACEHOLDER |
| Farm buildings | barn 40x60, machine shed 50x80, two 30 ft bins | PLACEHOLDER sizes. Exempt from setbacks (Iowa Code 335.2) but must stay inside the lot (check "FB") |
| Acreage | 1-5 ac; house 60 ft from the right-of-way | CRG |
| Acreage dimensions | 150-300 x 250-600 ft | PLACEHOLDER |
| Acreage rate | 25% per road mile | PLACEHOLDER |
| Rural side setback | 10% of lot width (minimum 5%) | PLACEHOLDER |
| Rural rear setback | 10% of depth (minimum 5%) | PLACEHOLDER |
| Rural lane | 12-16 ft wide, flaring to 24-40 ft at the road; farmstead lane 150-800 ft | CRG |
| Flare depth | 20 ft | PLACEHOLDER |
| Apron to garage door | 30 ft | PLACEHOLDER |
| Town drive with no garage | 9-10 x 20-40 ft | CRG |
| Town drive with an attached garage | as wide as the garage door | PLACEHOLDER |
| Town chance at a blacktop crossing | 35% | PLACEHOLDER |
| Town blocks | 4-9, grids 2x2/3x2/2x3/3x3 | CRG count; grid shapes are PLACEHOLDER |
| Block centers | 380-445 ft along the street | CRG |
| Block centers across | 2 x lot depth + alley + street (about 356-386 ft) | CRG parts |
| Town streets / alleys / lot depth | 60-66 ft / 16-20 ft / 130-150 ft | CRG |
| Base lot | 50 ft; a house takes 1-3 | CRG |
| 1/2/3 base-lot weights | 15/60/25 | PLACEHOLDER |
| Odd remainder at a block end | goes to the last lot | PLACEHOLDER |
| Town setbacks | front 20-30, side 5-8, rear 20-25, corner 10-15 ft (minimums = the low end) | CRG |
| Accessory buildings | 5 ft from side lines; 5-10 ft from the alley or rear line | CRG |
| Porch projection | up to 6 ft into the front setback on lots 70 ft wide or less | PLACEHOLDER |
| Rail | active line through town with depot and elevator siding | user decision |
| Rail corridor | 100 ft wide, 62-80 deg | PLACEHOLDER |
| Siding | 1,500 ft | PLACEHOLDER |
| Depot | 24 x 80 ft | PLACEHOLDER |
| Elevator / co-op | 3-6 ac | CRG |
| Elevator placement | just south of town | PLACEHOLDER |
| Cemetery | about 1.7 ac (270 x 275 ft) outside town, on the E-W blacktop | CRG |
| Cemetery distance west of town | 400-1,200 ft | PLACEHOLDER |
| Country church | 1-3 ac on a gravel road | CRG |
| Fuel station | on a blacktop corner | CRG |
| Fuel station parcel | 150 x 150 ft | PLACEHOLDER |
| Roof, pitch and siding lists (rule 35) | gable/hip; 4, 5, 6/12; vinyl, wood, brick front | PLACEHOLDER |
| Year | one draw per region | PLACEHOLDER |

### Results, seed 9
- **Built:** 45 of 49 lots (91.8%). Average 3.7 tries.
  - Farmsteads: 27 of 27 built, 1.9 tries.
  - Acreages: 4 of 4 built, 1.75 tries.
  - Town: 14 of 18 built, 6.8 tries.
- **Paved share:** 25% (drawn 24%).
- **Farmsteads per road mile:** 1.12. The draw gives 1.2; a few didn't fit.
- **Town:** 2x2 = 4 blocks at 390 ft centers (356 ft across), 60 ft streets, 16 ft alleys, 138.5 ft lots. The rail dropped 9 of the 27 planned town lots.
- **Retry causes:** lot fit 101, rule 23 area 21, rule 35 look-alikes 8, rule 8 kitchen to dining door 7, rule 30 deck 5, rule 27 secondary entry 3.
- **All 4 lots that never got a plan are 1-base-lot (50 ft) town lots.**

## Task 4: rail stage, driveways, trailers (seed 12 region, seed 14 no-rail town)

Pipeline is now roads, then rail (`make_rail`), then towns/lots, then houses. Rail is a polyline: a diagonal from one edge,
a straight run beside one N-S road (just off its 66 ft ROW), then a diagonal on to the other edge. It crosses roads at grade,
and the crossings are computed. If the rail passes the blacktop crossing, a rail town may form there: the grid sits across the road
from the track and is elongated along it, with the depot between the road and the track and the elevator and siding across the track.
Otherwise a no-rail town may sit centered on the blacktop x blacktop crossing, with no depot, elevator or siding. Lots never touch the rail corridor.

Driveways (rule 3 now covers every house): with a garage, the drive ends at the overhead door. An alley-side detached garage
takes its drive from the alley. With no garage, there's a parking pad beside the house. A farmstead lane ends in a farmyard apron that touches every
vehicle building (machine shed, barn, bins, garage).

Trailers (single-wide): the only use of a single 50 ft base lot. Stick-built houses take 2-3 base lots. A leftover single lot is
added to the neighboring house lot, or it joins the trailer cluster beside it.
Stick-built size scales with the lot: rural lots 1800-2000 sf, town lots 150 ft wide or more 1700-1900 sf, others 1600-1750 sf,
and narrower room widths when the buildable width is under 70 ft.

| New placeholder | Value |
|---|---|
| RAIL_PCT (region has rail) | 60% |
| Parallel run each side of the town road | 1500 / 2500 / 3500 ft |
| Gap between road ROW and rail ROW | 40 ft |
| RAILTOWN_PCT / NORAIL_TOWN_PCT | 50% / 15% |
| Rail-town grids (cols x rows along the rail) | 2x2, 2x3, 2x4, 3x3 |
| Depot | 24 x 80 ft |
| Trailer width x length | 14 or 16 x 60-76 ft |
| Trailer roof / siding variety | low-slope metal, low-slope membrane, shingled gable / metal, vinyl, T1-11, aluminum |
| Trailer pad | gravel, 9-10 ft wide, beside the long side |
| Trailer cluster (grouping) | 20% of block faces, 2-4 adjacent 50 ft lots. **Placeholder until the user's town examples arrive** |
| Stick-built area bands | above |

Rules that apply to trailers: U1 (the road end counts), U2, U3, 2, 3, 6, 7, 9-12, 16, 17, 22, 26-28, 31, 32, 34, 35.
Rules that are n/a for trailers: 1, 4, 5, 8, 20, 23, 24, 25, 30, plus 21 redefined as single-wide.
Rules that only warn: 13, 14, 15, 18. Not checked in 2D: 19, 29.

Open: a narrower stick-built layout for single 50 ft lots (for now those lots hold trailers only). HUD door width (often 32-34 in) vs rule 17's 36 in.

Seed 12 results: 67/67 built, avg 1.99 attempts. Rail runs along K34 for 5000 ft with 5 grade crossings; 2x4 rail town. 43 town lots, 5 of them trailers.
Rule 3 passes on all 67 lots. Seed 14 (no rail): 37/37 town lots built, 14 trailers. Images: `out/region.png`, `out/town.png`,
`out/town_norail.png`, `out/neighborhood.png`.

## Task 5: mobile home parks (George IA + park refs), seed 30

Most trailers now go in a **mobile home park**: its own parcel at the edge of town, fronting the E-W blacktop just past the town edge.
It has private lanes in a comb (1-3 dead-end lanes off the road) or a U around a green or pond (two legs plus a back lane).
Trailers sit perpendicular to the lane, each with a parking pad beside it on the lane. The park has a windbreak on 1-3 non-road sides.
In-block trailer clusters restored per user: 20% of block faces get 2-4 adjacent trailers on single 50 ft lots (placeholder), alongside parks. Rail towns also get a feed mill / scales parcel beside the elevator on the corridor.
Park trailers are footprints only, not full spawned plans.

| Placeholder | Value |
|---|---|
| PARK_PCT | 60% of towns |
| Pads per park | 15-40 |
| Pad pitch along the lane | 26 / 28 / 30 ft |
| Lane width | 20-24 ft |
| Site depth | 90 ft |
| First pad from the road ROW | 40 ft |
| Windbreak | 30 ft wide, 1-3 sides |
| U pattern | 40% of parks; green 100-180 ft |
| Industry parcel | 200 x 300 ft |

Not yet modeled from George: a grid partly skewed to the rail angle, industry along an abandoned diagonal corridor, ragged fringe streets,
school/pool/ball fields/campground on the edge, creeks (noted only), 6-10 mi town spacing (region is 3 x 3 mi).
Seed 30: 68/68 lots built, 2x3 rail town, comb park with 29 pads (3.1 ac) on C16. Images: `out/town.png`, `out/park.png`, `out/region.png`, `out/neighborhood.png`.

## Task 6: park rule + commercial strip (seed 30)

A town gets a park only if trailers are its only house style, or stick-built houses >= 2 x (in-block trailers + planned park pads).
In seed 30 this fails: 25 < 2 x (12 + 29), so there's no park. `out/park.png` comes from the task 5 run, and park geometry tests use seed 30's town with a synthetic 120-house count.

Main-street commercial (ALL PLACEHOLDER) goes on block faces fronting the E-W blacktop, starting at the crossing corner, with zero side setbacks and 0-5 ft front setbacks.
Parking is behind the store, open to the alley. The gas station sits on the corner with an apron, a canopy and 3 pumps, and it is the region's fuel parcel. The implement dealer has a front display lot.
Count by blocks: 4 -> 3, 6 -> 5, 8 -> 7, 9 -> 8. Order: gas, bar, post office, cafe, grocery, bank, hardware/lumber, implement dealer. The co-op office (30 x 40 ft with parking, placeholder) sits inside the elevator parcel in rail towns. No-rail towns have no co-op modeled, so they have no co-op office.
Lot widths: 25 ft (bar, cafe), 50 ft (post office, bank, co-op office), 75 ft (grocery), 100 ft (gas, hardware/lumber), 150 ft (implement dealer).

## Task 7: small-town Iowa business mix (source: Construction Research Guy research, small-town Iowa 1985-2000)

All values below are PLACEHOLDER proposals taken from the CRG research. Population is estimated as town houses x 2.4 (placeholder), using the house count before businesses take any lots.
The bands are <250, 250-749 and 750-1,499 people. `choose_businesses(seed, band)` draws each type independently, using the per-band % in `BIZ_P`, so it is deterministic per seed.
- Rates by band: post office 82/96/96, bank 25/67/91, grocery 6/30/70 (CRG has 6/28/70; floored at 30 for 250+ per user), cafe 22/54/76, tavern 18/37/60, gas 17/60/94, elevator 43/55/67,
  farm supply 10/20/30, implement dealer 7/24/43, lumberyard 7/16/51, hardware 3/9/27, auto repair 7/26/53, beauty 3/9/31, insurance 7/33/70, church 41/78/92,
  locker plant 3/10/12, drug 0/4/29, funeral home 0/16/44, nursing home 0/9/47, vet 3/12/37, city hall/fire 50/80/100, school 0/30/70.
- Extras: a 2nd church 0/25/50%, and in the 750+ band the grocery is the newer highway store 50% of the time.
- Siting:
  - Main-street faces on the blacktop at the crossing.
    - Gas station on the crossing corner: 120-150 ft lot, 40 x 60 ft building, 2-4 pumps under a canopy, 6-12 spaces. It is the region's fuel parcel.
    - Bank on the other main corner: 50 ft lot, 25-50 x 80-100 ft building.
    - Attached 25 ft storefronts: cafe, tavern, insurance, beauty, drug, hardware, locker plant, grocery. 80-120 ft deep, zero side setbacks, curb parking in the ROW out front, yard to the alley behind.
    - About 15% extra vacant storefronts ("vacant (former grocery/hardware/drug/variety)").
    - Post office (40 x 60 ft) and city hall/fire (60 x 80 ft) on main street or one block off.
  - Churches (40 x 80 ft, plus parsonage and a side lot) on residential corners taking 2-4 base lots.
  - Funeral home in a converted house on or one block off main street.
  - Edge of town on the blacktop:
    - implement dealer 330 x 400 ft (3 ac, 60 x 120 ft building, gravel yard)
    - auto repair 100 x 150 ft (40 x 60 ft shop)
    - vet 200 x 220 ft
    - nursing home 300 x 400 ft
    - school 500 x 600 ft with a ball field
    - lumberyard 250 x 300 ft
    - farm supply 150 x 200 ft
    - highway grocery 60 x 100 ft building
  - Co-op office (30 x 50 ft) on the elevator lot.
- Not followed: elevators stay rail-town-only (earlier user decision), so the 43/55/67 elevator draw is ignored. Farm supply and lumberyard go on the town edge rather than at the elevator or on the rail.

## Task 8: snapshot year + town sizes (all PLACEHOLDER)
- One snapshot year per world (`region_year`, the existing per-region build year: 1985/1990/1995/2000). CRG rates are the 1998 baseline.
  - Fading types (grocery, hardware, drug, variety, implement dealer, lumberyard, locker plant, cafe) are scaled linearly to 1.3x the 1998 rate at 1985, and slightly below 1x after 1998. Source: CRG, towns under 500 lost about 40% of retail sales from 1983 to 1996.
  - Vacancy share scales the opposite way: about 10.5% in 1985 and about 15.7% in 2000.
  - The bank presence rate is unchanged. Branch share rises from 10% in 1985 to 50% in 2000.
- Town size: tiny 60% / mid 30% / big 10%.
  - Grids: tiny 4-9 blocks; mid 5x4 to 7x6 (rail towns 4x6 to 6x7); big 8x8 to 10x9 (rail towns 9-12 x 8).
  - Mid and big towns leave about 35% of outer-ring blocks as farmland, which gives a ragged edge with dead-end streets.
  - The main street district is capped at 6 block faces.
- No-rail towns get a truck-served co-op elevator (400 x 450 ft, about 4.1 ac) at the CRG 43/55/67% rate, with the 30 x 50 ft co-op office on that lot.
- Demo: `out/town.png` = seed 31 (mid rail town, 1985). `out/town_big.png` = seed 14 (big no-rail town, 1995).

## Task 9: creeks + misshapen towns (all PLACEHOLDER)
- Creek stage runs after roads and rail: 0/1/2 creeks per region (30/50/20%). Each is a meandering, roughly diagonal polyline; the first passes 900-2200 ft off the blacktop crossing.
  Channel 20-60 ft wide, floodplain buffer 150-300 ft each side. No lots, parcels, businesses or parks go in the floodplain. A bridge is marked wherever a creek crosses a road.
- Town shape:
  - Blocks hitting the floodplain are dropped.
  - Ribbon growth: blocks on the main paved roads always grow, and other blocks thin out with distance from both roads (decay scaled by grid size).
  - About 40% of outer half-blocks are left off, giving uneven edge depth.
  - Streets exist only along kept blocks, and 30% of segments end in a dead-end fringe stub.
  - Mid grids are now 6x5 to 8x6 (rail towns 5x7 to 6x8); big grids are 10x9 to 12x10 (rail towns 10-12 x 8).
- Not done: skewing blocks to a diagonal rail (Boiler Ave pattern). This needs rotated lot frames, and the demo rail towns have the rail parallel to the road anyway.
