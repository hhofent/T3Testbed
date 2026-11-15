# ACTIVE: town bake-off on python-test/town-bakeoff-grok

Authorized town-improvement bake-off. Sol's river STOP below still applies to
rivers; this section covers towns only. Off limits: river code/files,
`tests/town_baseline.json`, `trees.local_markers`.

- Branch: `python-test/town-bakeoff-grok` from `2469802febb1d9c2e4885830b5b8ae36b668bb83`.
- Worktree: `..\T3-bakeoff-grok\python-spawner`.
- Shared check: `checks/town_bakeoff.py` (hash fab3cf9e..., do not edit).
- Status: **0 failing checks** on `py checks/town_bakeoff.py` (was 41 at baseline).
- New modules: `town_outline.py`, `town_trees.py`, `town_biz.py`, `town_plat.py`.
- All behaviour gated with `if not option('generation.parity')`. New settings under `town.*`.
- Tests changed: `test_town_on_blacktop_crossing_with_crg_dimensions` (street_w in {66,80}, alley_w==20); footprint test left as block-hull share.
- Extras: park/ball field, water tower, approach-house strings, vacant storefronts, outward lots + ragged refill.
- Deferred: none in `checks/deferred.json`.
- Milestone pytest: running / see final report. Three slow river tests deselected (untouched).

## Design notes (short)

1. **Outline:** notched padded AABB of all town content (blocks, lots, approaches, elevator, downtown shops). Notch depth ~18–21% of content size, uncapped — small absolute caps left hull≈0.99 on big towns. Pad > notch so contain stays 100%. Prefer notched AABB over grid-blob because a pure rectangle still scored hull 1.0 and notches give a stable 0.85–0.95 band.
2. **Streets / Main:** per-town 66 or 80 ft ROW (~25% wide), 20 ft alleys emitted as `kind='alleys'`, 100 ft `T-MAIN-*` on the highway axis. Cross street tagged town_street. Post-extend clip keeps lots/buildings off the 100 ft ROW.
3. **Highway frontage:** highway towns convert blocks on Xc to E/W faces; rail towns keep Yc. `_main_biz` / `to_world` already support E/W.
4. **Businesses:** top-up in `make_town` (does not touch `choose_businesses` / `biz_p`). Edge curb apron reaches paved ROW after `_edge_slot` / arrange moves.
5. **Trees:** new `town_trees.town_markers`; `local_markers` untouched. Yard clumps + street trees inside lot line; Wray & Mize / Gleason weights.
6. **Fuller:** ragged refill, outward lots, business lots emitted, reserved plat under outline for lot_fill, alleys emitted.
7. **Extras:** park, water tower, approach houses, vacants, outward lots — see final report.

# ACTIVE HANDOFF: Part 5 stopped by user; QA deferred

This section overrides all older in-progress notes below. Read this section and
`checks/a2-regression-failures.json` before doing anything. Do not resume tuning.

- Repository: hhofent/T3Testbed. Work only in `python-spawner/`.
- Branch: `python-test/spawner`. Push ONLY this branch. No merge, rebase,
  history rewrite, force-push or PR.
- Last implementation commit: `3b80fe880127d40a4feacf5bfa59f7db44e241c5`
  (`river A2 part 5: bounded spacing repair and continuation guards`).
- Part 4: `aff8968b42d42e385e7afac71662057550f78575`.
- Part 5 is finalized with accepted implementation scope and deferred QA.
  Geometric zero-spacing acceptance is NOT claimed.
- Part 6: NOT STARTED. Its uncommitted verification/copy-helper replacement was
  removed before the final part-5 pytest run. Pre-existing part-4 baseline copy
  scripts/artifacts remain historical evidence. Do not start part 6.
- User ordered STOP after checkpoint, commit and push. No more full audits,
  smoke tuning, renders, or new work in this run.

## What changed

Part 4 replaced the misleading 30-ft sampled stub with pinned 0.3-mi tributary
and receiving approaches. Real arc-length measurement now reports confluences
at 0.1 and 0.25 mi, with continuations/splits separate. IDs, topology and widths
were retained. db78343 seed-14 true-confluence mean was 37.243/41.449 degrees;
part-4 result was 67.499/68.353 degrees. Pooled part-4 means 67.923/68.794,
SD 21.873/21.294, under-25 fraction 0%, minima 25.069/35.000. Detailed per-seed
before/after tables and every connection are already in checks/a2-*.json.

Part 5 adds id-sorted bucket clearance rejection with eight deterministic
H(seed, edge_id, 'space', k) trials, amplitude 1-0.12k, then valley-only/straight
candidates. Failed fallbacks stay explicitly unresolved. Canonical node repair
uses only own-basin/3x3 outlet inputs, immutable outlet curves, <=0.5-mi moves,
basin containment and strict parent/child elevation bounds. All incident ends
move together; IDs, downstream links, orders and scalar/endpoint widths stay
unchanged. Outlets do not move. Infeasible nodes stay fixed and are reported.
Receiving-approach headroom is reserved, not just point clearance.

Keep the final continuation-rounding guard: shorten looping Hermite handles,
check other reaches, plan both continuation sides together, and try nine bounded
common tangent rotations while preserving the intended 8-degree difference.
Self-contact checking is independent of wavelength spacing exemptions. Numerical
zero-length samples are removed. A cached seed-31 repair now reports zero
crossings/self-intersections/reversals/kinks. This is diagnostic, not cold acceptance.

## Last completed full measurements (v4, NOT final acceptance)

Full report: `check-results/20261115-024826-847644/summary.md`.
Portable unaccepted trial: `checks/a2-part5-trial-v4.json`.

| Seed | Spacing db78343 -> v4 | Nodes moved | Mean / max move (mi) | Finished crossings v4 |
|---|---:|---:|---:|---:|
| 14 | 93 -> 21 | 22 | 0.173178 / 0.350217 | 2 |
| 30 | 108 -> 32 | 32 | 0.184451 / 0.386297 | 6 |
| 31 | 102 -> 31 | 27 | 0.189947 / 0.414266 | 4 |
| 7 | 94 -> 28 | 29 | 0.143632 / 0.376843 | 7 |
| 52 | 87 -> 15 | 24 | 0.167819 / 0.410188 | 2 |

Graph/width/attachment/basin/elevation guards passed in v4. Geometric guards
failed; later cached repairs cleared crossings but cold confirmation is deferred.
No topology or density tuning is authorized. N3/N4 values were exactly unchanged:
mean 5.596, range 2.8-10.375, with the known BASELINE failure retained.

| Seed | Density db78343 -> v4 (mi/sq mi) |
|---|---:|
| 13 | 1.574229516 -> 1.554535387 (BASELINE failure, improved) |
| 14 | 1.395387381 -> 1.329368088 |
| 30 | 1.435789956 -> 1.500265757 (NEW QA failure) |
| 31 | 0.887656340 -> 0.914276303 |
| 46 | 1.488776695 -> 1.494150629 |
