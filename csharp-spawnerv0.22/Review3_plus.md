# Review 3 plus: independent audit of csharp-spawnerv0.21

Reviewed 18 November 2026. This report audits the current source against `INSTRUCTIONS.md`, checks `REVIEW.md` and `review2.md`, and evaluates the proposed `IMPLEMENTATION.md`. The implementation checklist at the bottom is a replacement planning recommendation, not a record of completed changes.

## Independent verdict

**Everything is not implemented.** The library and viewer build, and all seven existing tests pass, but the phase-completion notes overstate correctness. The main problems are inconsistent viewer/query generation, incomplete geometry constraints, incomplete world-space records, and tests that prove less than their names suggest. The performance improvements in the viewer are present in source; they do not make the library's wide queries inexpensive.

The best next work is to fix data correctness and stop generating discarded detail, then consolidate generation into a reusable session. Keep stage controls and the existing water-crossing behavior. Treat the optional viewer features separately from contractual fixes.

### Verification performed

- Read all four requested documents, the library and viewer generation paths, settings and transport code, the spatial index, and all seven tests. Inspected the copied house generator and its reference-test approach.
- Built the WPF application in Release: **success, zero warnings, zero errors**.
- Ran `CsharpSpawner.Tests` in Release: **7 passed, 0 failed, 28 seconds**.
- Ran a temporary net10 console harness referencing the actual net8 library. Seed 31 reproduced **3,151 Mid features** in the six-mile fixture and **161 wall plans** in the tiny town Near query. **124 of those plans have bounding boxes completely disjoint from the requested 0.1-mile rectangle; all 161 have wall Y = 0.**
- The harness converted a synthetic 100-foot contour: geometry Y and `ElevationFeet` were 100, but `HeightFeet` became **528,000**. That directly demonstrates the mixed semantics in the compatibility converter.
- Compared hashes of library source files with matching paths in `csharp-spawnerv0.2`: no differences found. This supports applying the older library findings to this copy; it does not establish identical UI performance.

Commands used, with `DOTNET_ROOT=C:\Users\hhofe\.dotnet` and `DOTNET_MULTILEVEL_LOOKUP=0`:

```powershell
& C:\Users\hhofe\.dotnet\dotnet.exe test CsharpSpawner.Tests/CsharpSpawner.Tests.csproj -c Release --nologo
& C:\Users\hhofe\.dotnet\dotnet.exe build csharp-spawnerv0.21.csproj -c Release --nologo
```

No production code, tests, settings, other version folders, or reference source were changed for this audit. Builds updated ignored output. The temporary harness was outside the repository. No UI session or 100-mile timing was rerun. Historical times, memory, screenshots, and UI observations below remain attributed to the earlier reviews; they are not independently reproduced measurements.

## Findings, ordered by impact

### F1 — High: Far generates and converts detail it immediately discards

`CsharpSpawner/WorldQuery.cs:62–108` unconditionally generates rural features, full town plats, and trees; it then converts, clips, merges, and finally filters for Far. Population also creates individual settlers before Far drops them. The expensive work is upstream of the detail switch.

The viewer already skips rural generation above 12 miles and trees above 1.5 miles (`MapSurface.cs:240–331`). This is two different payload/cost contracts. The old multi-gigabyte Far measurements are credible evidence of the library problem, but they are not timings of today's gated viewer. Move dependency-aware gates before generation. A summary of settlement identity and town extent must remain available without a full plat.

### F2 — High: Near plans are neither rectangle-filtered nor guaranteed to match the accepted house geometry

`WorldQuery.cs:114–121` enumerates touched chunks and returns every plan owned by them. The 161-plan/124-disjoint reproduction above confirms the leak. Filter whole houses by footprint intersection; do not cut individual wall segments at the camera edge.

`WallPlanSpawner.cs:25–45` also creates a new randomly sized archetype at the old footprint center. It does not use the accepted building dimensions, saved facing, or terrain. A footprint that passed water/rail/building checks at Mid can be replaced by a different-sized Near footprint that has never passed those checks. This is a source-confirmed missing validation, not a claim that this audit counted actual Near collisions.

All wall elevations are zero. Choose one explicit foundation datum for each plan and use the same terrain surface as generation. The original plan must remain stable across LOD and window boundaries. Rural `Farmhouse` features are `rural_building`, whereas the wall pass selects only `town_building`; Phase 11's general occupied-house wording is therefore not fully covered. Resolve that scope explicitly rather than calling all occupied houses complete.

### F3 — High: World-space conversion conflates elevation, footprint depth, and vertical height

`WorldRecords.cs:110–115` uses `MapFeature.Height` both as geometry Y/elevation in feet and as a mile-based length multiplied by 5280. The source uses `Height` for contour elevation (`WorldGenerator.cs:841`) and for parcel/building depth (`RuralSpawner`, `TownSpawner`). Consequently buildings acquire a small, meaningless Y equal to their mile-based depth, while contours acquire enormous `HeightFeet` values.

`WorldRoad` and `WorldBuilding` exist, but `WorldQueryResult` returns the generic `WorldFeature` bag. `ToBuilding` always reports facing 0; `ToPerson` receives no lot assignment in the query and defaults `LotId` to empty. Phase 0 is an incomplete migration, not merely an old WPF namespace. Define separate ground elevation, vertical height, footprint dimensions, facing, archetype, occupancy, and household/lot references. Convert units once, with feature-specific mappings during transition.

### F4 — High: Query cleanup can retain children of deleted rural lots

`WorldQuery.cs:79–84` collects removed lot/feature IDs, but removes from `all` only a fixed set of kinds. It omits `rural_building`, `rural_driveway`, `rural_path`, `rural_yard`, and rural tree markers. Those children carry the lot ID and can survive after their lot is removed. The separately constructed `survivingRural` correctly excludes them for the tree input, which does not remove them from the result payload.

This is a conditional source-confirmed defect. The default six-mile seed-31 harness found no orphan carriers in its visible payload, so that fixture alone does not demonstrate the failure. Add a forced overlapping town/yard fixture. Apply removal consistently by owner ID to every dependent feature.

The viewer has a separate related problem: `SetTownChunks` removes existing rural children, but `SetRuralChunks` does not reapply cached town exclusions (`MapSurface.cs:110–144`). A later detail upgrade can reintroduce rural objects without regenerating the cached town.

### F5 — High: The query can return reserved shelterbelt dots that the final tree pass rejected

`RuralSpawner.AddShelterbelt` already emits `tree_marker` records as reservations. `WorldQuery` adds all rural output to `all` before adding the final tree output. `TreeSpawner.GenerateChunk:29–31` passes reservations through obstacle rejection, but rejected reservations remain in the earlier rural list. Grouping by `(Kind, Id)` does not remove them; accepted duplicates also prefer the earlier record.

Use a distinct reservation type/internal collection and return only accepted markers from the final pass. Also extend `TreeSpawner.IsBuilding:132` to rail elevator/bin footprints; it currently excludes these town structures. Do not call Phase 10 fully complete merely because trunk-only rendering exists.

### F6 — High: Generation settings and caches are process-global mutable state

`WorldGenerator.cs:249–264` stores the caller's settings object directly and uses unsynchronized static dictionaries. Two concurrent in-process queries with different terrain settings can overwrite each other's configuration or clear caches while the other is generating. A caller mutating the same settings instance also defeats the old/new-value comparison in `Configure`, allowing stale cached terrain after a setting change.

The test collection disables parallelization, so its determinism result does not cover this API use. This is especially important before moving viewer work in process. Snapshot validated settings into a generation session; own caches by that snapshot and bound their lifetime. A lock around the entire load is a temporary serializing mitigation, not a substitute for immutable settings.

### F7 — High: Rail, zoning, and town geometry constraints are only partly enforced

- `TownSpawner.cs:134` leaves `mainCrossesTrack` false; base, access, and exterior streets are not subjected to the extra-street crossing checks. The extra check actually requires 75 degrees, stricter than the 45-degree minimum.
- Interior house footprints **do** call `IntersectsRail` (`:632`), and ordinary extra-street lots do too (`:673`). Exterior lots (`:687` onward), manually emitted outlying houses, and some other structures lack equivalent protection. The earlier blanket assertion that houses are never rejected against track is inaccurate.
- Pass-through percentages and labels exist, but rail offsets and nearby-track selection do not prove that 70/25/5 of actual town intersections occupy the named zones. Commercial pass-through is not explicitly routed along the highway business row. Test geometry and zone assignment, not just the label.
- Successful town rail paths are already cached in `TownRailLink.Points` (`TransportSpawner.cs:303–307`). Failed bows remain null and can repeat the full search per affected chunk. Fix failed-result caching rather than describing every successful bow as recomputed.
- `GenerateRailChunks` **does** return `PlanTile` rail features (`TransportSpawner.cs:127–132`); the worker has a corresponding path. Deleting this path as unused, as the plan proposes, can delete real returned rail.
- `BuildTownOutline:282–306` chamfers every corner after detecting any entering corridor. It does not adapt the boundary to that corridor. Exterior roads always close every outline edge into a loop (`:201–203`), despite the instruction not to force one perimeter loop.

The 80%-square rule also needs a precise interpretation: if it literally applies to every arbitrarily small query window, a window around one non-square crossing fails. Meeting that literal rule requires all individual crossings to be at least 75 degrees. Otherwise specify a fixed evaluation neighborhood for the ratio. Do not silently weaken the rule to one passing seed-31 sample.

### F8 — Medium: Town detail has additional incomplete rules

- Business allocation is a per-type hash (`TownSpawner.cs:233–242`), not a count-based 70/30 allocation. Downtown `frontLots` are selected near the center of the plat rather than explicitly fronting Main Street. Eligibility and insufficient strip capacity need deterministic rules.
- Sidewalk defaults cancel to the curb centerline (`:475–480`), and no quiet-street roll removes both sides (`:463–467`). `AddDoorWalk` searches all nearby sidewalks rather than the serving side, so adding the missing roll alone does not guarantee the correct start point. Its no-sidewalk fallback adds `FrontX/FrontY` to the road center; verify the direction because these point toward the street from the house.
- Court bulbs and outlying houses are emitted before `HasSafeRailCrossings` decides whether their parent extra street is accepted (`:393–411`). A rejected street can leave a bulb or houses behind. Build extras transactionally and commit only after validation.
- Dead-end tests permit both ends of named extras; they do not prove one attached end and one free end. They also do not validate the per-town cap.
- Civic rates/minimum populations exist, and ordinary residential lots are removed where they overlap civic grounds. Civic placement itself does not check water, rail, roads, other grounds, or every business location (`:503–537`). A central square is placed at the anchor, not selected as a central block face.
- Removing overlapping buildings at the end does not repair associated occupancy, door walks, garages, or population. Keep a coherent accepted-parcel model and materialize its dependent features afterward.

### F9 — Medium: Defaults, API validation, and dependency extents need correction

`WorldRecords.cs:37–42` defaults major-town chance to 1/3 rather than 1/4, farm thresholds to 60/95 rather than 15/65, and acreage chance to 12% rather than 25%. These are contractual differences. `IMPLEMENTATION.md` explicitly excludes them, which prevents that plan from finishing the user's instructions.

WPF validates many settings, but public generation methods accept mutable settings without a shared validator. Invalid ranges can produce negative dimensions, division/modulo errors, or pathological resource requests. `WorldQuery` validates finite origin/size, but not the enum or all downstream settings and representable coordinate bounds.

The fixed 9-mile context claims to exceed the default 9-mile separation and does not grow when settings grow. Neither that comment nor a single overlap fixture proves independence for all allowed spacing, separation, water-search, rail, and town dependencies. Derive dependency extents or constrain documented supported ranges. Avoid merely enlarging the constant until one fixture passes.

### F10 — Medium: Tests currently leave substantial false confidence

`Phase10To13Tests.cs` does provide useful deterministic regression coverage, a pinned route/name, actual rail-angle checks for the seed-31 fixture, and the preserved road/water-crossing fixture. Its limits matter:

- Overlap checks compare full geometry for one town, but for other shared-center features assert only ID presence. They do not compare geometry, metadata, people, walls, or terrain in the shared area.
- Building/water checks include only four building kinds and intersect water centerlines without buffering river width. A footprint beside the centerline but inside the channel may pass.
- Road connectivity uses a 12-foot tolerance and broad exemptions. `IsPlannedSectionCutoutEnd` treats a missing adjacent section ID as proof of an allowed cutout; it never verifies the terrain/water reason. Named extras can be entirely orphaned and still meet the exemption.
- The SpawnGuide test invokes the copied generator and asserts general properties. It does **not** compare its complete output with the retained reference for seed 1993. This is not the requested cross-check.
- Far checks exclude only three kinds; Near checks exclude `town_building` but do not verify plan bounds, terrain elevation, or plan collisions. Tree obstruction and occupancy consistency are untested.

## INSTRUCTIONS.md implementation coverage

This table covers every phase and its principal subrequirements. “Present” means code exists; it does not imply comprehensive visual or multi-seed verification.

| Phase / requirement | Assessment |
|---|---|
| Decisions: order, no year setting, no trailers/new bridge classes, cattle parcels | Present. Viewer preserves elevation → water → population → rail → roads → rural → towns → trees → walls. Query computes roads as a rail dependency first; distinguish planning from visible spawn order. Existing versions/reference were not modified in this audit; their historical untouched status cannot be proved from present files alone. |
| 0: net8 library, net10 WPF, in-process generation | Present. Units, typed generation, building facing/archetype, person lot assignment, namespace migration remain partial (F3). Internal half-mile grid and spawners still use miles. |
| 1: query signature, clipping, stable records/IDs | Signature and deterministic default fixture present. General overlap agreement remains unproved; fallback IDs include local feature indices, full plans escape clipping, and Far town points derive from clipped polygon averages, so a boundary can move their apparent position. |
| 2: editable seed, Random, Settings tab, explicit apply, save/load | Present. Defaults drift (F9); load/application validation and save-error reporting should use the same rules as the library. |
| 3: original terrain, half-mile routing, 20-foot contours | Present. Detail uses the same carved elevation approach. Storage-in-feet migration remains incomplete. |
| 4: saved riverbeds, basin lakes, order-based widths | Present. Far wrongly omits order-2 rivers. Preserved crossing fixture passes. |
| 5: 80/40 generic stems, banned place names, stable settlement names | Present in population source; household surnames remain separate. Seed-31 RouteId/Misty Ridge is pinned. Settings drift changes major-town frequency. |
| 6: safe/square crossings, water bridges, actual 70/25/5 pass-through | Partial; F7. The fixture passes but not every placement path enforces the rules. Dropping failed bows matches code, not its comment. |
| 7: section jogs, widths, county bridge, creek bridge, rough cuts, bank link | Preserved code and useful fixture. Bank-link maximum distance, negative fixture, jog/rough-cut boundaries need explicit tests. `best=6.01` is a search upper bound, not a minimum. |
| 8: single drive, door path/material, north/west shelterbelt 3–7 rows | Present structure. Drive starts diagonally toward the house-side point, and traverses selected building centers; “about 90 degrees” and access without cutting through structures deserve geometric fixtures. Reservations bypass final query filtering (F5). |
| 8: gambrel replacement, entrances, cattle parcel/limits, coop corner | Present with limits. Gambrel selection can be skipped when its slot fails placement, so 12% is a proposal rate rather than achieved rate. Entrances are coordinate-hashed and sized; cattle use grouped parcels and town cleanup. Coop implements 300-foot proximity and the 2-mile exclusion, not ownership by a town already served by an elevator. Separate rural garage branch is absent; the wording is conditional, so a new rural garage frequency is a design addition rather than an explicitly specified rate. |
| 9: four plans, side percentages, alleys, facing, exterior lots | Present. Generic outline chamfers and forced perimeter loop remain incomplete. Facing is not exposed reliably in public building records. |
| 9: dead ends, extras, count-split commerce, sidewalks, door walks | Partial; F8. Extra type names and geometry exist, but acceptance of dependencies and road attachment are not consistently enforced. |
| 9: civic own ground and rates, empty lots/95% occupancy | Rates/thresholds, separate civic grounds, and seeded occupied subset exist. Placement validation and post-removal occupancy consistency are incomplete. Near does not invent houses for absent town-building records. |
| 10: one final trunk-only tree pass and obstacle rejection | Partial; final pass exists, but rural reservations remain in query output and building-kind blockers are incomplete (F5). |
| 11: viewer uses query, Far/Mid/Near, 10–30-foot mesh, copied plans | Partial; two pipelines, threshold mismatches, wall bounds/Y/footprint problems, rural house scope, and missing true reference comparison. Large explicit Mid/Near requests have no terrain allocation limit. |
| 12: all seven seed checks | Seven tests pass, but several required invariants are only partially asserted (F10). Not a full completion certificate. |
| 13: remove old AddTown, keep reference/older folders, README | Old method absent, retained SpawnGuide exists, build output ignored, README covers library/query/settings/order. README's stable-clipped/trimmed-outline language is stronger than implementation. Instruction links still target 0.2 and its completion appendix needs correction. |

## Accuracy of REVIEW.md and review2.md

### Substantially supported

Both correctly identify the costly generate-then-filter query, missing order-2 Far rivers, full town plats at overview, unbounded detail mesh, wall Y=0 and chunk-sized Near output, outline chamfers, missing both-off sidewalk case, per-type rather than count-based commerce, stale namespace, impossible rural-pause condition, and incomplete tests. The preserved 95% occupancy is correct; do not revert it to the earlier 72%.

The newer review correctly distinguishes the viewer's rural/tree gates from the ungated library. Its cache-upgrade concern is supported by `SetTownChunks` versus `SetRuralChunks`, even though it explicitly did not capture a clean visual reproduction.

### Corrections and qualifications

1. **“No contour cache / rebuilt every frame” is stale for 0.21.** `MapSurface.OnRender:382–385` uses `_contourGeometry`; `BuildContourGeometry:495` creates frozen world-space geometry only on cache misses. Elevation bitmaps are cached too. Roads, rail, lakes, and other drawing still perform per-render work. Keep the existing contour cache and extend caching selectively.
2. **“Rail bows are solved once per chunk” is overstated.** Successful `Points` are cached. Failed searches are the repeated case. Profile regional `PlanTile` routing independently before attributing the whole rail cost to bows.
3. **“No residential house rejection” is too broad.** Interior and ordinary extra-street paths reject rail intersections; exterior/manual paths remain gaps. Neither prior review demonstrates a counted set of actual residential-track house intersections.
4. **“0.21 differs only by title and README heading” is not literally complete.** Library sources match in the hash comparison, but the versioned project/assembly and viewer optimizations must be accounted for. Old viewer observations are historical evidence, not the current source state.
5. **Rail timing attribution is inconsistent within REVIEW.md.** An early section says both measured paths use `GenerateRailChunks`; a later section attributes the isolated result to the other `PlanTile` path. Current library and worker both contain regional `PlanTile` generation. Preserve the recorded observations but rerun a harness with exact path/arguments before using them as causal proof.
6. **The 35-second total and apply times are not additive stage timings.** The newer review's listed stage values sum to more than its ready time. Cache reuse, stage boundaries, and clock definitions need explaining. `WorkerProcessHost.FlushBatch` measures synchronous callback elapsed time, including dispatcher queue delay; it does not time JSON deserialization despite the printed label. Therefore 6.9 seconds is not a pure road-mutation or pure geometry-build measurement.
7. **Not every measured road cost is removable gravel work.** Paved lines, cutouts, and bridge logic remain; never promise the full 9-second saving without measuring that split.
8. **Garage/coop recommendations add interpretation.** No rural garage is present, but the instructions do not specify a mandatory rural garage occurrence rate. A 6-mile coop service radius is a proposed design, not an existing requirement. Keep the explicit 2-mile town exclusion even for towns without elevators.
9. **“All three payloads match” and “seed checks done” are too strong.** Far rivers, Near clipping, overlap comparison, water-width collision tests, and actual reference comparison are gaps described above.
10. **Screenshots and machine timings remain historical.** Temporary screenshot paths are not durable evidence. This audit did not reopen those UI sessions or reproduce the multi-minute/hour timings.

## Review of IMPLEMENTATION.md

The workstreams, staged acceptance criteria, detail state, summary-town payload, immutable cache identity, profiling, and focus on skipping invisible work are sensible. It correctly marks itself as a draft. Its proposed optional features are useful and should remain separate from correctness requirements.

Revise the following before using it as an executable specification:

- **B4 must not remove `PlanTile` as unused.** It emits returned rail. Successful bows already cache; add a failed state and measure the actual expensive paths.
- **C1 must acknowledge frozen contour geometry already exists.** Preserve it, measure cache misses, and cache other world-space layers. Pan should change transforms, not cached geometry.
- **C2 must not mean fire-and-forget unbounded `BeginInvoke`.** That can queue a large memory backlog, report ready before application, and apply stale results after regeneration. Use bounded batches, await application acknowledgments, and tag callbacks with a generation/session ID. Await application, not a completed render.
- **B7's mesh omission is a contract change.** Existing Mid/Near promise terrain. Prefer bounded tile requests or an explicit payload/budget result; never silently return success without promised terrain. A mesh cap alone cannot make a 100-mile Mid load finish in seconds while rural, town, and trees remain unbounded.
- **Q2's recommended 48-mile gravel policy changes Far.** Baseline compliance means Far paved-only above 12 miles. A legacy 48-mile viewer option can be explicit, but must not quietly redefine the query contract.
- **Q8 needs one unambiguous convention.** Setting inset to zero in the current formula does produce a flush sidewalk, so that alternative is valid. For a positive verge, replace the formula with curb + gap + half-width, rename the setting to gap, and align door walks. Do not change the default alone while retaining an ambiguously named inset.
- **Q9's minimum-terrain recommendation is physically reversed.** The minimum does not prevent embedding uphill walls in ground. A maximum sampled grade avoids terrain protrusion but can leave a floating downhill foundation. Choose a level finished-floor/foundation model and represent its terrain consequences. A nonzero Y assertion alone is invalid because legitimate terrain can be at zero.
- **Q5's conservative rectangle is useful, but may suppress valid farms outside the final corridor-cut outline.** Separate cheap broad-phase bounds from the exact authoritative exclusion; refine and reconcile on upgrade. If Far population is pre-plat and Mid population is derived from placed homes, expose that distinction or compute one canonical value.
- **R4/T7's blanket no outline/corridor intersection is too strong.** Towns intentionally contain rail and paved approaches. Exclude incompatible parcels/areas and constrain actual crossings; test corridor-specific outline adaptation rather than banning all entering infrastructure.
- **A5 should not multiply coarse caches by detail.** Use seed + immutable settings/generator version + tile identity, with independently upgradeable layer completeness. Retain one coarse payload; do not generate a copy for each LOD.
- **R8 must distinguish grounds/points from physical buildings.** A cemetery parcel is not a building; a point depot cannot participate in footprint collision checks until it has a physical extent. Add missing coop, auxiliary, and actual civic structures to the appropriate inventory.
- **Defaults cannot be out of scope for a completion plan.** Restore the instruction defaults or explicitly propose a contract revision; preserve the 95% occupancy decision.
- **The blanket north-flip dependency on C1 is unnecessary.** Coordinate it with world-space transforms, but north-up can be fixed independently of a full renderer rewrite. Remember bitmap row orientation and hit tests, not only vector coordinates and text offsets.
- **Milestone performance promises need corrected dependencies.** M1 cannot credibly promise <=20-second Far while leaving expensive full town summaries/session work for later. Measure gates, regional rail, conversion, terrain, and transport separately.
- **Do not force seventeen user decisions before starting useful work.** Most are implementation choices with clear defaults. Flag actual contract changes; do existing-contract fixes and instrumentation without inventing an approval dependency.

## Recommended contract for the next implementation

Keep the current instructions as the baseline: Far above 12 miles has paved roads, rail, order-2+ rivers, stable settlement/town markers and names; Mid has detail footprints and 30-foot terrain; Near adds stable occupied-house plans. Optional legacy gravel visibility and tree LOD policy must be named rather than silently inferred from current rendering. Preserve 95% occupancy and all established water/rough-ground rules.

Use one generation session with immutable settings and canonical feature/parcel ownership. Keep query clipping separate from canonical generation. Preserve whole house plans when intersecting the rectangle; clip terrain and linear display payload as appropriate. Layer completeness, deterministic ownership, and cancellation must be testable outside WPF. Summary payloads should not move markers or change identities as the camera moves.

## Detailed implementation checklist, in delivery order

All boxes below are outstanding recommendations. Each task includes a concrete verification target. Correctness fixtures should be small and forced; wide benchmarks should be opt-in and record exact settings and window dimensions.

### M0 — Reproducible baseline and shared definitions

- [ ] **P01: Record reproducible baselines.** Add `PERF.md` with SDK/runtime, machine, commit/source identity, settings snapshot, origin, size/aspect, cache state, and commands. Measure the 100-mile rectangular viewer separately from the square library query; keep old numbers as historical. Acceptance: another run can reproduce the same paths without UI guesswork.
- [ ] **P02: Add trustworthy profiling.** Separate generation, serialization, deserialization, queue wait, state application, render, allocated bytes, and peak working set. Include generated/retained counts and dropped/failed rail links. Acceptance: stage clocks explain the end-to-end total and callback labels match what is timed.
- [ ] **P03: Validate and snapshot settings.** Centralize percentage/range/finite-value checks; snapshot the complete configuration. Restore 1/4, 15/65, 25% defaults; retain 95% occupancy and all other specified values. Handle saved settings deliberately rather than overwriting user choices. Acceptance: invalid API inputs fail predictably, omitted keys receive documented defaults, and mutation of an original object cannot affect an active generation.
- [ ] **P04: Publish one detail/dependency table.** Define boundary behavior at 1.5 and 12 miles (and any explicit legacy 48-mile option), tree policy, contours, terrain, and labels. Acceptance: query and viewer consume the same policy; boundary tests cover exactly-at and immediately-above/below values. Mid terrain is never silently omitted.

### M1 — Fix current payload correctness before architecture changes

- [ ] **P05: Repair feature ownership cleanup.** Remove every dependent rural feature by lot/parcel owner in query and viewer; preserve explicit owner IDs. Acceptance: forced town-over-yard/cattle/entrance fixtures leave no buildings, paths, drives, reservations, or offices belonging to removed parcels.
- [ ] **P06: Separate tree reservations from final markers.** Do not return rural reservations as finished trees. Route every candidate through final blockers; include town rail structures and width-aware driveway surfaces. Acceptance: a forced blocked reservation is absent from query and viewer; accepted trees remain stable and trunk-only.
- [ ] **P07: Fix converter semantics.** Give ground elevation, vertical height, footprint dimensions, facing, archetype, occupancy, and parent IDs explicit meanings. Keep compatibility conversion feature-specific until migration completes. Acceptance: contour 100 remains 100 feet, building depth never becomes ground elevation, and round-trip tests preserve intended units/metadata.
- [ ] **P08: Create canonical house instances.** Select the archetype, plan seed, orientation, and actual footprint before collision acceptance; save them with the lot/building. Mid uses that footprint and Near uses the matching plan. Include rural homes or document an explicit approved scope restriction. Acceptance: LOD changes preserve ID, facing, footprint and accepted clearances; empty lots never gain a plan.
- [ ] **P09: Bound wall output and set foundation grade.** Query all owners whose whole footprint can intersect the rectangle, including adjacent ownership chunks. Filter whole plans by exact polygon/rectangle intersection, deduplicate by house ID, sample canonical carved terrain for a documented foundation model. Acceptance: the current tiny Near reproduction no longer returns 124 disjoint plans; a boundary-straddling plan is returned whole; elevation equals the chosen grade even when legitimately zero.
- [ ] **P10: Stabilize markers and record identity.** Derive Far town markers from a saved anchor, not clipped polygon averages. Use stable canonical IDs instead of local list-index fallbacks. Populate building facing/archetype and person lot references where assigned; represent unassigned people explicitly. Acceptance: panning changes clipping but never marker position, identity, name, or relationship metadata.

### M2 — Stop overview waste without changing the world

- [ ] **P11: Gate discarded work upstream.** Far skips rural detail, individual person objects, trees, wall plans and detailed plats. Keep site populations and cheap canonical town descriptors sufficient for rail and later refinement. Acceptance: instrumentation reports zero generation of discarded detail, not merely an empty final list; retained geometry/IDs match a detailed load.
- [ ] **P12: Include order-2 rivers and necessary crossing representation.** Change the Far filter to >=2; specify whether bridge markers are explicit or represented through crossing metadata. Acceptance: a forced order-2 crossing remains visible and its road-cut/bridge behavior agrees across LODs.
- [ ] **P13: Reuse spatial indexes in direct library calls.** Build bounds once and share indexed neighborhoods with rural/town/tree stages. Preserve deterministic candidate ordering. Benchmark cell sizes rather than mandating tiny cells for long rivers/roads; avoid huge duplicate bucket expansion. Acceptance: indexed versus unindexed small fixtures serialize identically; stage counters show reduced candidate scans.
- [ ] **P14: Profile and fix both rail paths.** Cache success and failure once per canonical link, spatially narrow road crossing checks, and independently profile regional `PlanTile` work. Retain regional rail unless a replacement intentionally preserves it. Acceptance: forced failed bow is attempted once per session, deterministic results survive overlapping loads, and expected regional/town rail and water bridges remain.
- [ ] **P15: Separate rendering payload from solver dependencies.** Skip unneeded contour extraction/transport and gravel *output* according to the policy, while preserving road centerlines needed to validate rail, cutouts, and bank links. Acceptance: Far-then-Near rail matches cold Near; removing gravel from drawing cannot make rail cross it unsafely when detail returns.
- [ ] **P16: Bound terrain work explicitly.** Estimate cells and memory before allocation; support tiled terrain requests/cancellation or a clear budget-exceeded result with an explicit follow-up path. Cache the same 10–30-foot surface samples; do not invent another terrain. Acceptance: six-mile Mid still supplies its 30-foot grid; oversized Mid/Near cannot exhaust memory or silently violate the payload contract.

### M3 — Finish world geometry rules

- [ ] **P17: Validate all rail-road crossings and zoning.** Cover regional rail, links, county spurs, base streets, exterior/access roads, extras and Main Street. Resolve the square-crossing evaluation rule and enforce it geometrically. Route industrial/commercial/residential pass-through against actual zones and protect all occupied footprints. Acceptance: forced fixtures cover every path, shallow failures and 70/25/5 assignments; no silently exempt fallback line.
- [ ] **P18: Replace generic chamfers with corridor-aware plats.** Adapt compatible buildable boundaries to buffered rail/water/paved corridors, preserving legitimate access and internal pass-through. Generate edge streets from the resulting topology without forcing one closed loop; retain outer-side lots where suitable. Acceptance: forced river/lake/rail/highway cases change the correct part of the boundary and preserve network access and stable town identities.
- [ ] **P19: Build extras transactionally and enforce graph ends.** Validate each complete extra before emitting its bulb, median, shops or houses. Track one permitted free end and the per-plat cap explicitly. Remove lots covered by actual traveled width, not just a centerline. Acceptance: rejected COURT/OUTLYING fixtures leave no children; every accepted street attaches as required, cap includes all extras, and houses face their serving street.
- [ ] **P20: Enforce count-based commerce.** Select businesses first, compute a deterministic rounded 30% strip target subject to eligible types/capacity, keep all others downtown on Main Street, and fall back downtown when no paved road is nearby. Elevator follows rail zoning. Acceptance: small/large fixture catalogues cover target rounding, ineligible civic businesses, short strips and no paved route.
- [ ] **P21: Unify sidewalks, walks and drives.** Apply <15 neither / 15–39 one / >=40 both for ordinary streets; always both on Main and actual school frontage. Use an explicit curb-to-walk geometry convention. Connect each door to its own serving side and correct curb direction/door position. Acceptance: forced hashes cover every case, paths meet real doors and sidewalks/curbs, and drives cross the appropriate walk.
- [ ] **P22: Validate civic ground and final occupancy together.** Choose central square from central faces; keep grounds clear of incompatible water/rail/roads/business parcels and each other. Validate buildings before emitting their dependent walks, drives and garages; derive population from accepted occupancy once. Acceptance: rates/thresholds remain configurable, grounds have dedicated parcels, and removed houses leave no occupied state or access features.
- [ ] **P23: Finish rural access, gambrel and coop semantics.** Ensure near-perpendicular mouths and one drive with access beside rather than through structures. If separate garages are introduced, define their rate and branch once. Preserve 3–7 north/west shelterbelt rows and pre-1990 gambrel replacement; distinguish proposed from successfully placed barn rate. Associate coops with a service area/town summary before adding any elevator-exclusion radius. Acceptance: forced yard/acreage/coop fixtures cover geometry, no second barn, 300-foot proximity, unconditional 2-mile town exclusion and no duplicate town-served elevator.
- [ ] **P24: Prove retained section-road rules.** Test rough relief at 105 feet and rise at 150 feet, county-line/jog behavior, major-river gravel absence, creek bridge, rail-water bridge, and positive/negative bank-link distance cases. Acceptance: measured search/crossing distance is within the documented six miles (with separately stated bank offset/tolerance); cutouts have explicit reasons rather than inferred missing IDs.

### M4 — Shared sessions, progressive viewer and bounded rendering

- [ ] **P25: Introduce the reusable generation session.** Immutable settings, per-session/versioned caches, deterministic tile ownership and dependency-derived halos; eliminate shared mutable `WorldGenerator` state. Keep direct APIs and headless adapters. Acceptance: concurrent different-settings sessions and same-object mutation tests cannot contaminate results; cache eviction does not alter regenerated data.
- [ ] **P26: Add layer completeness and LOD reconciliation.** Track summary/full town, paved/gravel, contours, rural, trees and walls independently of dictionary presence. Canonicalize exclusions before committing detail. Acceptance: cold Near equals Far→Near and Near→Far→Near in shared canonical data; farms do not appear inside cached towns; regeneration invalidates all old session callbacks.
- [ ] **P27: Move viewer onto session APIs with bounded application.** Preserve stage toggles and progressive reporting. Use cooperative per-chunk cancellation/update, bounded result queues, application acknowledgments and generation IDs. Acceptance: a pan retains visible completed work and ready water; repeated reseed/pan cannot queue unbounded work or display old results.
- [ ] **P28: Extend existing world-space render caches.** Preserve frozen contours and relief bitmaps; cache roads/rail/lakes and expensive detail by content revision. Build/freeze eligible geometry off the UI thread, transform on pan, invalidate only affected layers. Acceptance: panning does not rebuild unchanged geometry; instrument queue wait, cache misses and frame time before promising 50 ms.
- [ ] **P29: Retain coarse data within a memory budget.** Keep coarse tile state separate from prunable detailed payload, use LRU/visibility and debounce, include static/session caches and WPF assets in accounting. Acceptance: returning to retained overview skips regeneration; deliberate eviction regenerates identical data and memory stays within the configured bound.
- [ ] **P30: Fix status reporting.** Repair impossible Rural pause condition, distinguish skipped-by-LOD from paused-by-toggle, report current request/stage/application/render counts and honest save errors. Acceptance: roads-complete/rural-off shows the correct pending stage; ready means current required data has been applied.

### M5 — Strengthen verification, then optional viewer improvements

- [ ] **P31: Compare canonical overlap contents.** Check geometry and metadata for all shared-center records, people, wall plans and terrain samples; normalize request clipping where appropriate. Cover negative coordinates, boundaries, varied allowed settings and a second seed. Acceptance: ID-only equality is no longer sufficient; changes in camera chunk sets cannot rotate/move/rename canonical features.
- [ ] **P32: Make collisions physical and inventory-driven.** Register all actual building footprints (including auxiliaries, rail structures, coops, cattle office, future rural garages), buffer river/rail/road/drive surfaces, distinguish grounds from buildings, and test Near as well as Mid. Acceptance: adversarial footprints inside river width but off its centerline fail; no missing kind bypasses the checks.
- [ ] **P33: Tighten connectivity, tree and occupancy tests.** Use explicit accepted-dead-end/cutout reasons, validate attached ends/caps, final tree obstruction, and dependent-feature ownership. Acceptance: intentionally orphaned named extras and blocked shelterbelt reservations fail even if the current seed-31 scene still passes.
- [ ] **P34: Compare with retained SpawnGuide output.** Generate each archetype at seed 1993 in an isolated reference harness or checked-in golden fixture from the original source. Compare footprint, wall dimensions, openings, tags, seeds, and exact story height. Acceptance: the test detects a changed copied generator, not merely an empty or height-invalid output.
- [ ] **P35: Add opt-in benchmark tooling.** `--benchmark --seed --origin --size --detail` plus fixed Far/Mid/Near scenarios; report the P02 counters and reproducibility metadata, support configurable target failures, never open WPF. Keep `--headless`. Acceptance: both rectangular viewer-equivalent and square library scenarios can be measured without changing source; wide resource tests are excluded from the fast default suite.
- [ ] **P36: Add optional viewer usability features.** North-up (including bitmaps, zoom anchor, pan, clipping, hit tests and labels), compass/scale, click/hover town summaries, collision-aware labels, seed Apply/Enter. Acceptance: north and scale remain correct across zooms, drag stays usable, labels fit, and all seed-apply paths use the same validation/save/regeneration handler.
- [ ] **P37: Finish namespace and documentation cleanup.** Work in 0.21 for this scope; use `CsharpSpawner` for the library and a version-free viewer namespace, updating XAML consistently. Correct README, phase status, stale 0.2 links and this checklist as work lands. Preserve SpawnGuide and old version folders. Acceptance: build/tests pass; documentation accurately distinguishes completed contracts, optional features and remaining limitations.
