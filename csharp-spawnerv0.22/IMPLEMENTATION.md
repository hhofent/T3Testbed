# Implementation list for review2

Draft. Nothing here is implemented.

Source of truth for this list: [review2.md](review2.md). The contract it is measured against is [INSTRUCTIONS.md](INSTRUCTIONS.md); the previous audit is [REVIEW.md](REVIEW.md).

The list is ordered so that each milestone leaves the viewer in a shippable state. Items marked **BLOCKED** need a decision from the "Decisions" section before code is written.

---

## Baseline the plan is measured against

| Measurement | Current | Where it comes from |
|---|---:|---|
| 100-mile overview, seed 31, 100 x 46.86 mi | 35 s ready, 466 MB, max frame 31 ms | review2, 4,788 chunks |
| Elevation at that view | 13.2 s round trip, 12.7 s worker | review2 |
| Roads at that view | 9.2 s, 9.0 s worker, 3.8 MB | review2 |
| Rail at that view | 6.2 s | review2 |
| Towns at that view | 3.5 s, 27 settlements | review2 |
| Far query at 100 miles | 2,002.6 s, 4,900 MB, 2,565 features | REVIEW.md |
| Mid/Near at 100 miles | never finished | REVIEW.md |
| 6-mile Mid query | 7.652 s, mesh is 5.154 s of it | REVIEW.md |
| Tests | 7 passed, 28 s | review2 |

Proposed targets (confirm or change):

- 100-mile overview: ready in <= 20 s, <= 600 MB, no frame > 50 ms.
- 100-mile Far query: <= 20 s, <= 1 GB, zero rural/tree markers allocated.
- 6-mile Mid query: no slower than today, mesh unchanged.
- 0.1-mile Near query: <= 3 s, no wall plan whose footprint is outside the rectangle.
- UI thread: no single batch apply over 250 ms; no frame over 50 ms at any zoom.

---

## Decisions needed before code

These are the places where review2 contradicts the contract, contradicts itself, or leaves the answer undefined. Each decision lists the recommendation I would take if you do not choose otherwise.

### Q1. One pipeline or two? **blocks A1, C1, C7, E6**

`INSTRUCTIONS.md:196` says the viewer gets its data from the query. review2 says "the viewer and the query are two pipelines" and only asks that the query get the same gates. Those are different sizes of job.

A literal port is not free: `WorldQuery.Load` is a one-shot window call that rebuilds a 9-mile context (`WorldQuery.cs:11,26-27`) and returns everything at once. The viewer is chunked and progressive. Calling the query once per 1-mile chunk would rebuild elevation context 4,788 times.

Options:

1. **Share gates, keep two pipelines** (recommended for this round). Fix `WorldQuery` so a Unity Far load costs what the viewer costs. Keep the viewer pipeline. Phase 11 stays partial.
2. **Add a tiled query session** (`WorldQuerySession.Open(seed, settings)` -> `QueryChunk(x, z, options)` with shared elevation/population/rail/index caches), then move the viewer onto it.
3. **Full port** to `WorldQuery.Load` per window, delete the child workers and the per-stage toggles.

Recommendation: 1 now, 2 as the next version. Option 3 removes the stage toggles, which are the debugging tool the review used for every clock in REVIEW.md.

### Q2. Gravel between 12 and 48 miles **blocks B5, C5**

This is a direct contradiction:

- `INSTRUCTIONS.md:198`: Far (wider than 12 miles) = paved roads only.
- review2: "Skip generating the gravel grid above 48 miles", and the viewer hides gravel only past 48 (`MapSurface.cs:424`).

So the viewer draws gravel at 12-48 miles, while a Far query excludes it. If the viewer adopts Far above 12, the 12-48 mile picture loses roads it has today.

Options:

1. Move the viewer's draw cutoff to 12 miles. Instructions-compliant, cheapest, visible picture change.
2. Keep gravel to 48 miles and let Far include gravel in that band. Contradicts the payload table.
3. Add a width-dependent option (`IncludeGravel`) independent of detail: generate/draw gravel up to 48 miles, skip above. Far at 12-48 keeps gravel.

Recommendation: 3. It preserves the picture and still gives the 9 s saving above 48. It makes "Far" mean "detail level", not "everything that happens to be drawn".

### Q3. Contours above the readable zoom **blocks B6, C4**

review2 wants contour polylines out of the far elevation payload (15.8 MB, 5.2 s apply) but kept "for the zoom where a 20-foot line is readable". It does not say what happens when the camera zooms into an already-cached far chunk.

Options:

1. Ship contours always and only skip drawing. Does not recover the payload/apply saving.
2. Separate contour payload/stage, gated by width, with its own per-chunk "have contours" flag. Cached far chunks upgrade in place.
3. Regenerate the elevation chunk on zoom-in to get contours. Simple, but throws away elevation work and re-pays the worker.

Recommendation: 2. Threshold proposal: contours at <= 12 miles; verify against slope readability at 12 and 24 miles and tune the constant. Hillshade bitmap carries the far view.

### Q4. In-process stages vs the JSON child processes **blocks C7**

Chunk-level cancellation is impossible with the current transport: each worker reads all of stdin before generating (`TreeSpawner.cs:110`, `TransportSpawner.cs:144`), and the parent ignores view changes while `_isLoading` is set (`MainWindow.xaml.cs:232`). Cancelling means killing the whole child.

REVIEW.md item 4 says to stop shipping the world through a child process. review2 does not list it in "Optimizations worth doing next", but does note that every stage is still a child and a full stdin document.

Options:

1. Keep children; cancel only at stage boundaries; add a chunk-id visibility filter for the next batch.
2. Move the stage loops in-process, keep the child worker entry points for `--headless`, add a real `CancellationToken` checked per chunk and a visibility predicate that drops only chunks the new view no longer needs.
3. New worker protocol that can receive a cancel/update message mid-batch.

Recommendation: 2, but land it after the cheap wins. It is the only way to satisfy "cancel an in-flight batch" and it removes the stdin copy that dominates towns/trees. It also gives `WorldQuery` and the viewer one code path, which makes Q1 option 2 cheap later.

### Q5. Far town: point only, or point plus outline? **blocks B8, A3**

review2 wants Far to "emit the point" (the 100-mile town worker wrote 9.0 MB of plats for 27 dots). But it also says "Generating the plat on the way in also gives town removal a rural list to cut against", and separately that a cached town can grow farms inside it because rural is skipped on the way out and not replayed on the way in.

Point-only makes the farm-in-town bug worse: on zoom-in there is no outline to keep rural out.

Options:

1. Point only; on zoom-in, generate the plat and re-run rural removal for affected chunks. Requires the LOD upgrade ordering in A3 and re-running the rural stage.
2. Point + conservative outline (the plat rectangle before corridor cuts) + name/population/plan. Rural consults that outline and never places a lot inside it. Plat is generated later for drawing.
3. Point + full plat. This is today's cost, just serialized differently.

Recommendation: 2. It keeps the Far payload small, makes rural self-defending, and does not depend on stage ordering. Town removal stays as the cleanup for legacy chunks.

### Q6. Rail bow failure **blocks R1**

`TransportSpawner.cs:365-376` tries 24 bows, then `if(points==null)return;` drops the link. The comment directly above says the direct approach is kept. REVIEW.md asks only that the comment and code agree.

Options:

1. Drop the link, fix the comment. Preserves the 45-degree rule; loses connectivity.
2. Keep the direct approach, emit a diagnostic, and let the rail-angle test see the shallow crossing (which would then fail).
3. Keep the direct approach but mark the link `unsafe` and exclude it from the angle test. The rule is then only best-effort.

Recommendation: 1. The 45-degree rule is a contract; connectivity is not worth a shallow crossing. Add a counter to the stage diagnostic so a seed with many failures is visible.

### Q7. Main Street and rail **blocks R2**

`mainCrossesTrack` is a local `false` (`TownSpawner.cs:134`) and never changes, so Main Street is always the middle east-west row. Base streets never go through `HasSafeRailCrossings`; only extra streets do. Fixing this changes town geometry for existing seeds, including Misty Ridge.

Options:

1. Accept the geometry churn. Compute whether the middle street crosses the rail; if it would cross at under 45 degrees, choose a different street for Main Street or shorten the street. Angle-check all base streets.
2. Keep Main Street where it is and only angle-check; a violating town falls back to a non-crossing plan or drops the street segment.
3. Defer; document as a known limitation.

Recommendation: 1. No test pins Main Street's row, and the phase 6 rule is explicit.

### Q8. Sidewalk inset geometry **blocks R3**

`INSTRUCTIONS.md:148`: "A sidewalk is 4.5 feet wide and sits 2.25 feet inside the street edge." The code computes `road.Width*.5 + (width*.5 - inset)` (`TownSpawner.cs:478`), so 4.5/2.25 puts the centerline on the curb. review2 calls that a bug and asks for the walk beside the curb.

"2.25 feet inside the street edge" is geometrically ambiguous: entirely on the road, flush against the curb, or 2.25 ft of verge then the walk.

Options:

1. **Verge**: centerline = curb + inset + width/2. Defaults give a 2.25-ft strip then a 4.5-ft walk. Keeps the setting meaningful and the walk off the pavement.
2. **Flush**: centerline = curb + width/2; inset becomes unused or means the gap and defaults to 0.
3. **Inside the road** (literal reading): centerline = curb - inset. Physically wrong; not recommended.

Recommendation: 1, and change the setting label to "sidewalk gap from curb". If you meant 2, set `TownSidewalkInsetFeet` default to 0 and keep the current formula.

### Q9. Wall plan clipping and Y **blocks B11**

review2: "Plans are clipped to the chunk, not the rectangle" and "Wall Y is still hardcoded to 0". REVIEW.md says "Clip to the request after generation".

Clipping wall segments would produce houses with missing walls. Filtering whole plans is the only sensible reading.

Options for Y: sample the carved elevation at the footprint minimum, the footprint center, or the average of the four corners. A house on a slope needs one plan-level Y, because the plan has no per-wall vertical offsets.

Recommendation: filter whole plans whose footprint does not intersect the query rectangle; set plan Y to the minimum carved elevation under the footprint (so no wall floats below grade). Add a `GroundYFeet` to `WorldWallPlan` if the client wants the exact value, and keep every wall's Y equal to it.

### Q10. Coop corner elevator rule **blocks R6**

`INSTRUCTIONS.md:109` has two sentences: no town within 2 miles, and do not add a coop corner for a town that already carries the rail elevator. The code implements only the 2-mile exclusion (`RuralSpawner.cs:224-227`). REVIEW.md calls out the gap.

The second sentence is only meaningful for a town outside the 2-mile ring, so it needs a radius. Options: 2 miles (redundant), 6 miles (trade area), or "nearest settlement regardless of distance".

Recommendation: 6 miles, new setting `CoopCornerElevatorSearchMiles`. Skip the corner if the nearest settlement within 6 miles has a `town_rail_elevator`.

### Q11. Rural garage **blocks R5**

Phase 8 says "If the garage is separate, it is a short branch off that drive", but rural lots have no garage feature at all. Town lots have one at `GarageChancePercent` 42 (`WorldRecords.cs:48`).

Options:

1. Reuse `GarageChancePercent` for rural lots too.
2. Add `RuralGarageChancePercent` (suggest 42, matching town) so the two can be tuned apart.
3. Make every farmstead have a garage. The instruction says "if", so no.

Recommendation: 2, default 42. New kind `rural_garage`, 20 x 22 ft, placed on the drive side, with a short branch from the driveway. Add it to the overlap test, tree blocking, Mid payload, and the viewer.

### Q12. Settler label complaint **blocks E4**

review2: "the settlers are a pile of overlapping names in the field beside the plat, clipped by the window edge." Two possible issues: labels overlap, and settlers stand outside the plat. The second may be by design: population markers are placed at the settlement site, not inside the future lots.

Recommendation: treat this as a label problem only. Greedy collision boxes + leader lines; keep labels inside the viewport; show fewer names as zoom decreases. Do not move settlers unless you say the field placement itself is wrong.

### Q13. Apply button semantics **blocks E5**

`Regenerate_Click` already applies settings, saves `settings.json`, clears the cache, and regenerates (`MainWindow.xaml.cs:136-211`). A second "Apply" beside the seed box is either a duplicate or a different operation.

Options:

1. Apply = save + regenerate (same handler, different button placement).
2. Apply = save settings only, no regeneration. Seed box/Enter applies and regenerates.
3. Rename the Settings-tab button to Apply and put it beside the seed too.

Recommendation: 1, plus Enter in the seed box. The complaint is discoverability, not missing behavior.

### Q14. Version, folder, namespace **blocks 0.1, E8**

`csharp-spawnerv0.21.csproj:5` still uses `RootNamespace csharp_spawnerv0._1`; `MapSurface.cs:12`, `MainWindow.xaml.cs:16`, `ConsoleHost.cs:9`, and the XAML `x:Class` follow it.

Options:

1. Work in place in 0.21 and rename to a version-free namespace (`WorldSpawner.Viewer`).
2. Copy to `csharp-spawnerv0.22` and use `csharp_spawnerv0._22`.
3. Copy to 0.22 and use a version-free namespace.

Recommendation: 2 if each review round gets its own folder (matches 0.1 -> 0.13 -> 0.15 -> 0.2 -> 0.21). Otherwise 1. Do not rename the assembly (`csharp-spawnerv0.21`) and the namespace in the same commit as behavior changes.

### Q15. Benchmark harness **blocks E6**

review2 wants a headless `--benchmark` for the 100-mile window; REVIEW.md says "the in-process query is the right harness". `ConsoleHost.cs` still generates a fixed 5 x 5 chunk box.

Recommendation: `--benchmark` calls `WorldQuery` at 100 miles (Far), 6 miles (Mid), and 0.1 mile (Near), prints per-stage timings and working set, and exits non-zero on a target miss. Add `--benchmark --pipeline` later if you want the viewer stages. Keep it out of `dotnet test`.

### Q16. Cache budget and retained layers **blocks A4, C6**

Keeping the 100-mile overview while zooming into a town is the point of C6. Today the overview is 466 MB; keeping it plus a detailed neighborhood needs a cap.

Recommendation: 512 MB coarse-layer cap. Retain elevation hillshade, water, population settlements, rail, paved roads, town points/outlines. Drop rural lots, trees, walls, town lots/streets, buildings, and settler bodies when they leave the view plus 2 miles. Evict least-recently-visible chunks first. Confirm the cap.

### Q17. Are the stage toggles staying? **blocks Q1 option 3**

The nine checkboxes are how REVIEW.md measured every stage in isolation. A one-shot query cannot honor them without extra plumbing.

Recommendation: keep the toggles, and keep the viewer's per-stage pipeline this round. If you choose Q1 option 3, the toggles become query filters and the "one spawner at a time" workflow changes.

---

## Milestone 0 — freeze and instrument

- [ ] **0.1 Baseline capture.** Record the current 100-mile seed-31 run, the 6-mile Mid/Near query, and `dotnet test` into a `PERF.md` next to this file. Include the exact command, machine, and settings. Freeze the 100-mile screenshot.
- [ ] **0.2 Stage profile lines.** Make every stage print chunks, wall time, payload bytes, and peak working set. The viewer already prints round-trip/worker/apply (`WorkerProcessHost.cs:79`); add the same lines to `WorldQuery` and `ConsoleHost`.
- [ ] **0.3 Seed fixture.** Keep seed 31 as the reference world. Add a second seed (665353858, the settings file default) so determinism is not a single-world accident.
- [ ] **0.4 Decide Q14** (folder/namespace) before touching source.

---

## Workstream A — detail model and chunk state

The root problem behind several review2 bugs is that the viewer uses "dictionary entry exists" as "this stage is done at every detail". That cannot express "elevation cached but contours absent", "town point cached but plat absent", "rural skipped at far", or "trees absent above 1.5 miles". Fix that first or every gate becomes another special case.

- [ ] **A1. Per-chunk stage mask.** Replace the presence checks with a `ChunkState` record per chunk: `Seed`, `SettingsHash`, `Detail`, and flags for `Elevation`, `Contours`, `Water`, `Population`, `Rail`, `Roads` (with `PavedOnly`), `Rural`, `Town` (`Point`/`Plat`), `Trees`, `Walls`. Keep the feature dictionaries as the payload storage.
  Files: `MapSurface.cs` (`GetChunksNeeding*`, `PruneCache`), `MainWindow.xaml.cs` (`LoadVisibleChunksAsync`).
  Accept: `GetChunksNeedingX` no longer calls `.ContainsKey` on a payload dictionary to decide "done"; a chunk can be elevation-complete and contour-incomplete.

- [ ] **A2. Single detail policy.** One function maps view width to detail and stage flags: Near < 1.5, Mid 1.5-12, Far > 12; gravel <= 48 (Q2); contours <= 12 (Q3); trees/walls < 1.5; labels per zoom. Delete the duplicated cutoffs in `GetChunksNeedingRural` (`MapSurface.cs:244`), `GetChunksNeedingTrees` (`:310`), `GetChunksNeedingWallPlans` (`:331`), `DrawRuralLots` (`:590`), `DrawTrees` (`:837`), `DrawWallPlans` (`:850`), and `DrawRoads` (`:424`).
  Accept: one constant table, no inline `_viewSizeMiles > ...` gates left outside the draw functions that consume the table.

- [ ] **A3. LOD upgrade path.** When detail increases, run the missing stages in dependency order: elevation -> water -> population -> rail -> roads -> rural -> town -> trees -> walls. Upgrading a town from point to plat must not erase the rural removal state; rural must consult the town outline (Q5) so no farm appears inside a plat.
  Accept: zoom 100 -> 0.5 miles reproduces the same rural/town result as a cold 0.5-mile load, with no farm lot inside a town outline. Add a deterministic test for this at 6 miles (a far-then-near load vs a cold near load).

- [ ] **A4. LOD downgrade and retention.** Implement Q16: keep the coarse layer on zoom-out/zoom-in, drop fat layers outside the margin, debounce pruning. `PruneCache` currently runs on every wheel event (`MapSurface.cs:537`) and removes all dictionaries together (`:580-586`).
  Accept: 100 -> 0.1 -> 100 miles keeps the overview and does not re-run elevation; measured by a generation counter in the status line.

- [ ] **A5. Cache identity.** Key cached chunks on seed + settings hash + detail so Regenerate and a settings change invalidate cleanly. Today `ClearChunks` handles the common case, but a partial stage rerun after a settings change can mix worlds.
  Accept: change one setting, regenerate; no chunk from the old settings survives.

- [ ] **A6. State-machine tests.** Put the state machine in a testable class (no WPF dependency) and cover upgrade, downgrade, and invalidation at 100/12/1.5/0.1-mile widths.

---

## Workstream B — library query: gates, payload, cost

- [ ] **B1. Keep order-2 rivers in Far.** `WorldQuery.cs:100` filters `f.StreamOrder >= 3`. Change to `>= 2` and add a test with a known order-2 crossing.
  Accept: a Far load that crosses an order-2 river contains that river; the existing 6-mile fixture proves it.

- [ ] **B2. Do not generate markers Far discards.** Gate `RuralSpawner` and `TreeSpawner` on detail before generation, not after filtering (`WorldQuery.cs:64-89`). Far should allocate zero yard markers, zero shelterbelt markers, zero trunk dots, zero settler bodies (B9).
  Accept: 100-mile Far peak allocation is bounded by the retained features; add a counter to the profile line.

- [ ] **B3. Spatial index from `GenerateChunk`.** `TreeSpawner.GenerateChunk` still does full-list scans via `NearChunk` (`TreeSpawner.cs:20-25,135`); the index only exists inside the worker (`:113-124`). Make `FeatureSpatialIndex` public or add an indexed overload, build it once in `WorldQuery.Load`, and use 0.04-0.12-mile cells (D-review). Avoid the per-feature `Min/Max` re-scan in `QueryChunk`; cache feature bounds at insert.
  Accept: 6-mile Mid tree stage does not regress; 100-mile Mid tree stage drops by an order of magnitude (measure with the index alone, before B10 removes it from Mid if that is the decision).

- [ ] **B4. Rail links once.** Choose each link's bow exactly once per `Load`/worker, before per-chunk clipping; cache `Points` and a `Failed` flag on `TownRailLink`. Remove `PlanTile` from the query path (it is a second rail network the query never returned, `TransportSpawner.cs:127-132`). Replace the full `link.MainRoads` scan with roads near the link corridor. Keep river/creek/bridge kinds unchanged.
  Accept: 100-mile rail stage is seconds, not minutes; the same seed produces the same link geometry from a cold and an overlapping load; the rail-angle test still passes.

- [ ] **B5. Gravel generation gate.** Add `IncludeGravel` to the road stage; above 48 miles generate only paved county/section lines plus their bridges and cutouts, per Q2. Keep the draw gate in sync.
  Accept: 100-mile roads stage drops the ~9 s/3.8 MB; a 47-mile load still has gravel; a 49-mile load does not generate it.

- [ ] **B6. Contour payload split.** Add `IncludeContours`/a separate contour method so a far elevation result carries the hillshade samples but no polylines, per Q3. Keep `ContourIntervalFeet` 20 and `ContourCellFeet` 660.
  Accept: 100-mile elevation payload drops from ~15.8 MB; the 12-mile-and-under view still draws the same contours; a cached far chunk upgraded to Near gets contours without a second elevation worker.

- [ ] **B7. Mesh size gate.** `WorldQuery.cs:111-113` builds the 30-ft grid for every Mid/Near. Add `MaxMeshCells` (suggest 4.4M, a 12-mile window) or `MaxMeshMiles`; when exceeded, return `TerrainGrid = null` and a diagnostic flag. Do not silently change `detail`. Keep the 10-30 ft step and do not add a second terrain (`INSTRUCTIONS.md:202`).
  Accept: 100-mile Mid returns in seconds with no grid; 6-mile Mid still has the 1.1M-cell grid; the test asserts `StepFt` stays in 10-30.

- [ ] **B8. Far town payload.** Implement Q5 option 2: point + name + population + major flag + plan id + conservative outline, no lots/streets/buildings. The full plat is generated for Mid/Near on upgrade.
  Accept: 100-mile town payload is kilobytes, not 9 MB; rural placement consults the outline; the 6-mile Mid town is byte-identical to today.

- [ ] **B9. People at Far.** `WorldQuery.cs:108` already drops them. Confirm the viewer should too (it draws settler dots at every zoom today, `MapSurface.cs:472-482`). If the settler dots are part of the overview picture, Far needs a cheap "person dot" kind or the dots must come from settlement populations.
  Accept: a written decision in this file; no silent 617-person payload at 100 miles.

- [ ] **B10. Trees at Mid or Near.** Phase 11's Mid list does not include tree markers, but the viewer gates trees at 1.5 miles and the current 6-mile Mid load returns 1,698 of them. Decide: trees are Near-only (cheaper, matches the viewer), or Mid keeps them (matches the current test `Seed31QueryIsByteStableAndReturnsDetailPayloads`).
  Accept: one contract; the test asserts the chosen payload.

- [ ] **B11. Wall payload correctness.** Filter whole plans by footprint intersection with the query rectangle (Q9); set plan/wall Y from carved elevation under the footprint; pass a terrain sampler into `WallPlanSpawner.Generate` (it has no elevation input today, `WallPlanSpawner.cs:15-45`). Keep openings and the 97.125-inch story height.
  Accept: 0.1-mile Near at an empty spot returns no plans; a sloped footprint returns a non-zero Y; the SpawnGuide test still passes.

- [ ] **B12. Payload tests.** Add: Far has no rural/tree/wall/lot features; Far has an order-2 river; Mid has no walls; Near has no `town_building` and its walls are inside the rectangle; Far town is a point/outline.
  Accept: new tests run in the default suite at 6 miles, plus one slow 100-mile Far test behind a trait.

- [ ] **B13. Per-stage diagnostics.** Return or log a `WorldQueryProfile` with stage seconds, feature counts, and allocations. Use the same names as the viewer diagnostic so the benchmark and the window can be compared.

---

## Workstream C — viewer performance

- [ ] **C1. Per-chunk frozen visual cache.** `OnRender` rebuilds contours, roads, rail, lots, and every polygon on every frame (`MapSurface.cs:337-489`). Build a frozen per-chunk `DrawingGroup`/geometry set once per detail, on a background thread, then draw it. This is also the real fix for the 6.9 s road apply: the apply is cheap, the first full render is not.
  Accept: a pan at 100 miles does not change chunk geometry build counts; max frame <= 50 ms; no `StreamGeometry` construction inside `OnRender` except for one-off overlays.

- [ ] **C2. Batch apply without blocking on render.** `Dispatcher.Invoke` at `Background` priority waits for `OnRender` to finish (`MainWindow.xaml.cs:264-270` and every stage callback). Mutate state in one `BeginInvoke` per batch, invalidate once, and never await the render. Keep the 64-item transport batch.
  Accept: elevation apply at 100 miles drops from 5.2 s; the status line shows data landing while a frame is in flight.

- [ ] **C3. Road geometry off the UI thread.** Build road/rail geometry in the chunk-cache worker; freeze it before handing it to the UI. Do not rebuild all visible chunks per frame.
  Accept: 100-mile roads apply <= 250 ms; frame <= 50 ms during the road stage.

- [ ] **C4. Contours by zoom.** Draw cached contours only at the Q3 threshold; far view uses the hillshade bitmap. Do not build contour geometry for chunks that are not drawn.
  Accept: 100-mile frame does not touch contour geometry; 6-mile contour picture unchanged.

- [ ] **C5. Draw gates in one place.** Rural > 12, trees/walls >= 1.5, gravel > 48 (or > 12 per Q2), town names > 3 off, labels per zoom. Remove the duplicate per-feature checks inside `DrawRuralLots` (`:597` etc.) once the stage gate is correct; the current loop still reads `feature.Points` on every feature before deciding it draws nothing.
  Accept: a 100-mile render visits only features it can draw; add a drawn/skipped counter to the status line.

- [ ] **C6. Cache retention and prune.** Implement A4/Q16: coarse layer survives, detail prunes, debounce on wheel (currently every notch), keep finished visible chunks, drop only ids that fail the new visibility test.
  Accept: 100 -> 0.1 -> 100 miles does not re-run the elevation stage; memory stays under the cap.

- [ ] **C7. In-flight cancellation.** Per Q4: token checked between chunks, request list filtered to the new visible set, finished chunks kept, water not starved. Do not kill a stage that has a ready water chunk.
  Accept: a pan during a 4,788-chunk elevation pass completes the chunks still visible and does not restart the world; a test or scripted pan shows no second full pass.

- [ ] **C8. Status line truth.** Report the current stage, visible/done/total chunks, render ms, cache MB, and whether the UI is inside a render. Today the status can sit on "water filling" while `OnRender` blocks (`REVIEW.md` 01:40 stall).
  Accept: a scripted pan during a stage never shows a stale stage for more than 2 s without a render counter moving.

- [ ] **C9. Far overlay bitmap (fallback).** If C1 still misses the frame target at 100 miles, render the coarse layer into one frozen bitmap and only rebuild it when the coarse set changes. Do not build this unless the frame target is missed; it complicates hit-testing and labels.

---

## Workstream D — geometry and rules

- [ ] **R1. Rail bow fallback.** Implement Q6. Cache the chosen line once (B4), fix the comment, add a diagnostic count. If the choice is "drop", the link is absent from both viewer and query.
  Accept: a seed with a forced bow failure produces one deterministic result; the rail-angle test covers base streets and the fallback.

- [ ] **R2. Main Street and rail.** Implement Q7. Compute `mainCrossesTrack` from the actual rail/plat intersection; if the middle street would cross under 45 degrees, move Main Street to a non-crossing street or stop it at the track. Run base streets through `HasSafeRailCrossings`. Reject residential-pass-through house lots that intersect the rail corridor (`INSTRUCTIONS.md:91`).
  Accept: no house footprint intersects a rail line; every rail/street crossing is >= 45 degrees; Misty Ridge still has a Main Street.

- [ ] **R3. Sidewalks.** Implement Q8 and the missing roll from `INSTRUCTIONS.md:147`: hash < 15 -> no sidewalk; 15-39 -> one side; >= 40 -> both; Main and School always both. Door walks start at the sidewalk line or the curb, per the same rule. Update the default inset if Q8 option 2.
  Accept: a fixture town has all three cases; no walk centerline on the pavement; door walks meet the walk.

- [ ] **R4. Town outline along corridors.** Replace the corner chamfer (`TownSpawner.cs:291-306`) with a cut/bend along the rail, paved road, river, and lake that enter the plat. Keep the concave outline usable by edge streets and exterior lots; do not force a single perimeter loop (`INSTRUCTIONS.md:122-127`).
  Accept: the saved outline does not intersect an entering corridor; edge streets follow the cut; existing town IDs and names do not change; a test asserts no outline/corridor intersection.

- [ ] **R5. Rural garage.** Implement Q11: new `rural_garage` kind, short branch from the driveway, placed clear of the house and outbuildings. Add to `TreeSpawner.IsBuilding`, the building-overlap test, the Mid payload, and `MapSurface.DrawRuralLots`.
  Accept: a farmstead with a garage has one driveway plus one branch; no overlap; the branch disappears when the garage roll is off.

- [ ] **R6. Coop corner elevator rule.** Implement Q10: keep the 300-ft rail rule and 2-mile town exclusion, add the "nearest town within N miles already has a rail elevator" exclusion.
  Accept: a fixture with an elevator 4 miles away produces no coop corner; a fixture with no elevator produces one.

- [ ] **R7. Bank-link distance test.** The code uses `best = 6.01` as the upper bound (`RoadSpawner.cs:239`), not a lower bound. Assert the near->far distance is <= 6 miles (+ bank offset), and assert no link is produced when the nearest paved crossing is over 6 miles. Do not change generation unless the test finds a real violation.
  Accept: the test measures distance; the current fixture still passes.

- [ ] **R8. Building overlap test coverage.** Add `town_rail_elevator`, `town_rail_bin`, `town_depot`, civic grounds/buildings, `cattle_office_shed`, and `rural_garage` to `Seed31BuildingsDoNotOverlapWaterOrOtherBuildings`.
  Accept: the test covers the kinds REVIEW.md lists; no overlap failures at 6 miles.

- [ ] **R9. Rural pause sentence.** `MainWindow.xaml.cs:442` requires `ruralEnabled && !SpawnerIsEnabled(RuralSpawnerToggle)`, which cannot be true. Fix the condition so a finished road grid with rural off reports "paused at Rural lots" when rural is actually pending.
  Accept: toggle rural off, finish roads, status says Rural lots, not Towns.

- [ ] **R10. Namespace and assembly.** Per Q14, update `RootNamespace`, all `namespace` declarations, every `x:Class`, and `App.xaml`. Do not rename the versioned assembly unless the folder changes.
  Accept: solution builds; no `csharp_spawnerv0._1` string remains.

---

## Workstream E — features and tooling

- [ ] **E1. North up.** `MapSurface.Map` adds map Y to screen Y (`:957-958`); flip the vertical transform, flip the wheel anchor (`:530-536`), keep drag direction (`:557-558` already matches once the draw flips), normalize chunk rects, and flip label offsets. Verify at 100, 6, 1.5, and 0.1 miles.
  Accept: the opening town sits above its approach road; a known north feature moves up when panning north; no inverted text.

- [ ] **E2. North arrow and scale bar.** Draw a compass rose and a scale bar in the status strip or as a map overlay (choose one; the mile readout is in `StatusText`). Scale bar length follows zoom, in miles or feet.
  Accept: the bar's labeled length matches the rendered distance at 100, 12, and 1 miles.

- [ ] **E3. Town click and hover.** Hit-test settlement dots in `MapSurface`; distinguish click from drag (movement threshold); zoom to a town at a chosen width (suggest 2-3 miles) and center on the site; hover shows name, population, major flag. At 100 miles show dots + hover names, no plats.
  Accept: click a dot at 100 miles -> ready at the town in one request cycle; drag still pans; hover does not fire while dragging.

- [ ] **E4. Label layout.** Implement Q12: greedy collision boxes for settler, building, lot, and town labels; leader lines or drop the label; clamp to the viewport; density cap by zoom. Replace the fixed `point + 4, -height` placement (`:480`, `:894-899`).
  Accept: no two labels overlap at 0.67 miles; labels stay inside the window; the same seed renders the same label set for a given view.

- [ ] **E5. Apply control.** Implement Q13: an Apply button beside the seed box, Enter in the seed box, same save+regenerate handler. Keep the Settings-tab button.
  Accept: typing a seed and pressing Enter regenerates and saves; the button does the same; status/console say what happened.

- [ ] **E6. `--benchmark`.** Implement Q15. Call `WorldQuery` at 100 miles (Far), 6 miles (Mid), 0.1 mile (Near), print stage seconds, feature counts, allocations, and peak working set; honor `--seed`; exit non-zero on a miss. Use the B13 profile names.
  Accept: `csharp-spawnerv0.21.exe --benchmark --seed 31` prints the three runs and does not open a window.

- [ ] **E7. ConsoleHost cleanup.** Replace the fixed 5 x 5 box (`ConsoleHost.cs:29-30`) with the benchmark path or `--size`/`--origin` arguments. Keep `--headless` working for the manual stage walk.
  Accept: `--headless` still runs; the fixed box is no longer the only option.

- [ ] **E8. Docs.** Update `README.md` with the detail table, the gates, the new settings, the benchmark command, and the namespace. Add a short `PERF.md` baseline. Update the phase 10-13 status in `INSTRUCTIONS.md` only where a decision changed the contract.
  Accept: README and code agree on Far/Mid/Near, gravel, contours, and the mesh gate.

---

## Tests

Default suite (fast, 6-mile fixture):

- [ ] T1 Bank-link near->far distance <= 6 miles; no link above 6 miles.
- [ ] T2 Far contains an order-2 river; Far has no rural/tree/wall/lot/person features.
- [ ] T3 0.1-mile Near contains no wall plan whose footprint is outside the rectangle.
- [ ] T4 Wall Y is non-zero on a sloped footprint and equal across one plan.
- [ ] T5 Building overlap covers the expanded kind list.
- [ ] T6 Rail crossings: base streets and the fallback line satisfy >= 45 degrees; residential pass-through has no house on the track.
- [ ] T7 Town outline does not intersect an entering rail/paved road/river; edge streets follow the cut.
- [ ] T8 Coop corner: elevator exclusion and 2-mile rule.
- [ ] T9 Rural garage: one branch, no overlap, deterministic roll.
- [ ] T10 Sidewalk side rules: <15 none, 15-39 one, >=40 both, Main/School both.
- [ ] T11 Cache/state machine: far-then-near equals cold near; downgrade keeps the coarse layer.
- [ ] T12 Determinism, overlap, road ends, story height: existing seven still pass.

Slow/opt-in:

- [ ] T13 100-mile Far: <= 20 s, <= 1 GB, zero discarded markers. `[Trait("Category","Slow")]`.
- [ ] T14 100-mile Mid: mesh omitted by B7, no OOM.

Timing assertions stay out of the default suite; the benchmark owns them.

---

## Suggested order

1. **M0** decisions, baseline, profile lines. No behavior change.
2. **M1 "stop the waste"**: A1-A2, B1, B4, B5, B6, B7, C1-C5, R9, T1, T2, T12. This is the milestone that should move the 100-mile overview from 35 s to the target and make Far queryable.
3. **M2 "close-up correctness"**: B8, B11, A3, R1-R8, T3-T10.
4. **M3 "one pipeline"**: A4-A6, B2, B3, B9, B10, C6-C8, R10, T11, T13-T14.
5. **M4 "features"**: E1-E8.

Do not start E1 (north flip) until C1 lands: it touches the same transform and every label offset, and doing it twice is wasted work.

---

## Deliberately out of scope

- New bridge types (CMP, box culvert, 1989 classes). `INSTRUCTIONS.md:9` forbids them.
- Changing water widths, stream orders, settlement spacing, 95% occupancy, or the 9-mile query context.
- Reverting the speed work already landed (64-item batching, rural/tree/gravel draw gates).
- Parallelizing stage chunks across cores. It is a valid lever, but not in review2.
- The Unity client itself.
- The settings drift in REVIEW.md (farm weights 60/95/12, major town 1/3, acreage 12) unless you want it folded into E8. review2 does not mention it.

---

## review2 coverage map

| review2 claim | Items |
|---|---|
| Viewer gates work; library has none | Q1, A2, B2, B5-B7 |
| JSON children still the transport; apply 5.2/6.9 s | Q4, C1-C3, C7 |
| Cached town grows farms | Q5, A3, B8 |
| Close-up rough; walls chunk-clipped; Y=0; name pile | Q9, B11, Q12, E4 |
| North to the bottom | E1 |
| Far drops order 2 | B1 |
| Rail bow fallback comment mismatch | Q6, R1, B4 |
| `mainCrossesTrack`; base streets; residential pass-through | Q7, R2, T6 |
| Sidewalk width/inset cancel; no both-off roll | Q8, R3, T10 |
| Outline chamfers, does not cut corridors | R4, T7 |
| No rural garage; coop elevator check | Q10, Q11, R5, R6, T8, T9 |
| Bank-link test; seven tests | R7, T1, T12 |
| No in-flight cancel; PruneCache drops overview | C6, C7, A4, Q16 |
| Mesh has no size gate | B7 |
| Root namespace; rural pause unreachable | R10, R9 |
| Gravel >48 s / 9 s | B5, Q2 |
| Contours 15.8 MB / 5.2 s | B6, Q3, C2, C4 |
| WorldQuery gates + spatial index | B2, B3, B7 |
| Road geometry off UI thread | C1, C3 |
| Far town emits point | B8, Q5 |
| North arrow, scale bar | E1, E2 |
| Town click, hover, label layout | E3, E4, Q12 |
| Apply control | E5, Q13 |
| Far payload match viewer | B1, B5, B11, Q2, Q3, Q8 |
| Headless `--benchmark` | E6, E7, Q15 |
