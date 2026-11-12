# To-do — filling out towns and country

A working list. Ordered roughly by "what a viewer notices first". Each item names the
research that justifies it and the files that would change.

**Research:** `docs/research/2026-11-11-iowa-town-composition.md` (what an Iowa small town
actually contains, and the county rules for drives, walks and entrances) and
`docs/research/2026-11-11-ia-calibration-references.md` (town shapes, lot frontages,
canopy, farmstead layout).

**Ground rule:** the world stays a pure function of world coordinates. Everything below
must be derivable from `(seed, world coordinate)` — no sequential passes that depend on
visit order.

---

## 1. Sidewalks leading to the houses  *(user, 2026-11-11)*

> "have sidewalks leading to the houses, have driveways leading to the garages.
> have dirt paths for rural lots on gravel roads."

**Where it stands.** `SmallTown.cs` draws 4.5-ft concrete sidewalks along both sides of
every grid street, and every lot gets a 4-ft walk to its front door and a 17-ft concrete
drive to its garage. The pieces all exist — **but they don't quite meet.**

Measured on a rendered town (`out/town4k.svg`, 0.318 px/ft): the median gap between a
front-door walk's street end and the nearest sidewalk line is **6.75 ft**. Cause: the
sidewalk is drawn 2.25 ft *inside* the street's edge line (`ewZ + 2.25` /
`ewZ + StreetRow − 2.25`), while the walk starts 4.5 ft *past* the lot's front line
(`frontLineZ ± 4.5`). Side-lot walks, which face a N–S street, are correct — gap 0.

**To do.**
- `FillTownLot`: start the front-door walk **on the sidewalk line** (`frontLineZ ∓ 2.25`),
  not `frontLineZ ± 4.5`.
- `FillTownLot` / `PlaceSideLot`: start the garage drive **on the sidewalk line** and let
  it cross the walk to the kerb. `zStart` is currently 2 ft (E–W) / 1.5 ft (side) past
  the front line, so every drive stops in the yard instead of at the street. In real
  Iowa the drive crosses the sidewalk and runs to the kerb — the walk is continuous
  across it and the drive is the lower surface.
- Driveway widths and angle per county practice: 20–40 ft top width for a rural
  entrance, ~90° to the centreline; town residential drives are 10–14 ft concrete.
- Keep the walk as a distinct **spur** that visibly leaves the sidewalk and reaches the
  door, rather than merging into it.

**Also:** real Iowa sidewalk networks are patchy — "there are not sidewalks everywhere",
"no sidewalks along any of the streets that cross US". Consider dropping the walk on one
side of the quieter residential streets on some towns, rather than a uniform network.

## 2. Dirt paths for rural lots on gravel roads  *(user, 2026-11-11)*

**Where it stands.** `MapGenerator.RuralLot` gives the farmstead a 12-ft gravel drive to
the garage and a 10-ft gravel lane to the machine shed. Both start at z = −58, which is
*inside* the 66-ft ROW (the road occupies z −66…0), and they leave the road at ~90° —
both correct. The front-door walk reaches the road too.

**The real gap is the field entrances.** Every Iowa section line in the world is a clean
edge between two fields with nothing on it. Reality:

- **One or two dirt field entrances per quarter section** (Woodbury County: "up to two
  field entrances may be allowed per forty (40) acre field"), each **20–40 ft top
  width**, **3:1 side slopes**, offset **≥100 ft** from an intersecting road.
- Surfaces: **earth with vegetation, or a minimum 4 in depth of 1⅛" crushed rock**
  (Clarke County) — so they read as a different, paler surface than the gravel road.
- **A farm's residence entrance is a *separate* entrance from its field entrances** —
  "a farm property may be served by a single driveway for the residence, plus a second
  driveway to allow access to farm buildings or grain bins" (Woodbury).
- The county lays gravel on the farm's entrance along a paved road and on occupied
  building sites on a gravel road (O'Brien) — so the farmstead drive is the *gravel*
  one and the field entrances are the *dirt* ones.

**To do.** Add field entrances to `WorldMap.AddRoads` / `AddFields`: per section, 1–2
dirt entrances off the gravel section road, hashed per section so they are stable across
windows, offset from intersections, and drawn as an earth-tone surface distinct from
the road. Keep the farmstead's own gravel drive separate.

**Files:** `MapGenerator.cs` (`RuralLot`), `WorldMap.cs` (`AddFarms`, `AddRoads`).

## 3. Towns still have extra roads — fill them with lots  *(user, 2026-11-11)*

> "towns still have extra roads. maybe add a final step to fill with lots all roads in
> towns, save the few empty ones"

**What the research adds.** Iowa town plats are layered, and the extra streets are real
and characteristic — they are not a defect to be erased:

- **A diagonal rail-corridor street** — George: Boiler Ave on the old rail line; Boyden:
  Railroad St curving SE through the south third.
- **A curved street** — Hull has one curved residential street on the east side.
- **A bypassed old highway** — after a US 30 bypass, the old route stayed as a quieter
  parallel street (Kirkwood Blvd = old US 30, Lisbon; Rolesville's Main St after the
  US 401 bypass).
- **Sparse rural-residential along an outlying road** — Hull: "North side has sparse
  rural-residential along 310th St outside the grid."

**So the to-do is two-part, not one:**

- **(a) Add a final "platted but not built" pass.** Verified in `SmallTown.Generate`:
  the lots are placed (step 2) *before* the street network is laid (step 4), and the lot
  fill only ever faces the **E–W grid streets** (plus the side faces of 4-sided blocks,
  which face the N–S streets). So three kinds of street end up with **no frontage at
  all**:
  - the whole **perimeter loop** — both halves and the jog step;
  - the **outer faces** of the west and east boundary N–S streets;
  - the N–S streets' far ends, past the last E–W street.

  A final pass should walk every street segment and platt lots along both sides wherever
  there is room, then deliberately leave **a few** empty — the dead-end's four lots are
  already the town's one deliberate vacancy, so add 1–3 more on a random street so a
  town reads as having a vacant lot (real Iowa towns are full of them).
- **(b) Add the characteristic extra streets** — one diagonal rail-corridor street, one
  curved street, or one outlying rural-residential road per town, chosen by seed. This is
  the thing that makes a town read as *Iowa* rather than as a grid.

**Files:** `SmallTown.cs` (street network + a new final lot pass), `WorldMap.cs`
(`RoadClip` — the loop bounds must grow to enclose the new streets, or county roads
will run through them).

## 4. More commercial

**Gap.** The commercial band is a fixed list of six labels reused in the same order in
every town in the world: `CASEY'S`, `FEED & SEED`, `CO-OP`, `HARDWARE`, `BAR & GRILL`,
`AUTO ELECTRIC`. So every town has the same shops, and no town has a bank, post office,
pharmacy, hardware, insurance or restaurant as *distinct* businesses.

**Research.** The mix scales with town population (Iowa PBS):

| Class | Commercial strip |
|---|---|
| crossroads | gas + grocery (Casey's), maybe a post office |
| hamlet | + feed & seed, a hardware store, a bar |
| village | + implement dealership, bank, insurance, pharmacy, restaurant, auto repair |
| town | + lumber yard, newspaper office, several of the above, motel |

**To do.** Turn the label list into a *business catalogue* with a footprint and a
frontage, and let the town class draw from it:
Casey's (gas + grocery, big sign), implement dealership (large shed + display lot),
feed & seed, hardware, bank, insurance office, post office, pharmacy, bar & grill,
auto electric / repair, restaurant, lumber yard, hardware, laundromat.

Casey's is the anchor: 312 in Iowa by 1990 and the chain's whole strategy was small
towns, so *every* Iowa small town had one.

## 5. Industrial — and put it on the railroad, not the highway

**Gap.** `SmallTown.cs` places the grain elevator + bin row and an industrial building in
the **highway** commercial band. In the world map the rail line may be **up to 9 miles
away**, so a town can have an elevator with no railroad beside it. Real elevators are
railroad institutions: a siding, a headhouse, bins, and a co-op sign.

**Research.** Iowa DNR registers hundreds of elevators, mostly co-ops. The Bucknam
station was built *by the railroad* with a siding and a grain elevator. Every reference
town has the elevator at the rail/main junction. Real industrial buildings follow the
rail corridor: "industrial corridors extending along more than 40 miles of railroad
right-of-ways"; George's Boiler Ave is lined with small manufacturing.

**To do.**
- Place the **elevator + bin row at the rail siding**, where the rail actually touches
  the town; keep it on the highway band only if there is no rail in the town.
- Add a **rail spur** into the elevator yard.
- Add a **rail-side industrial strip** (small manufacturing: cabinet shop, metal
  fabrication, feed mill) on the diagonal rail-corridor street from item 3.
- Keep the highway-fringe industrial (implement dealership, propane bulk tank) as
  *highway* uses — that is correct; industrial follows rail, commercial follows highway.
- Industrial building form (NRHP Iowa Malleable Iron): 1-story metal building on a
  concrete foundation, e.g. 90'×240'; concrete-block ancillary with 14–16 ft walls and
  an overhead door. Creamery is a ghost-town element by the 1990s — rare.

## 6. Civic and institutional landmarks

**Gap.** No water tower, no depot, no cemetery, no ball field, no library as a building,
no post office, no bank.

**To do.** Add as civic anchors, scaled by town class: **water tower** (near-universal
small-town landmark), **railroad depot** (wooden, single story, town-name sign on a
post), **cemetery** (often with a church), **ball field**, **public library**, and
post office/bank as commercial buildings (item 4).

## 7. Street variety

**To do.** Per town, by seed: one diagonal rail-corridor street; one curved street;
one outlying road with sparse rural-residential; one street that simply stops at the
edge of the plat.

## 8. Sidewalk realism

**To do.** Consider dropping the walk on one side of quieter residential streets on some
towns (real Iowa sidewalk networks are patchy), while keeping the commercial strip and
the school/park route fully served.

---

## Not for now

- **Cattle yards** (the feedlot archetype from the Lyon County research) — the user has
  never confirmed it. It is a *parcel* archetype, not a town archetype; keep it on the
  farmstead backlog.
- **Bridges/culverts** where roads cross rivers — separate to-do, belongs to the world
  road layer, not the town layer.
