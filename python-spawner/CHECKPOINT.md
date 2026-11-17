# ACTIVE HANDOFF: v0.11 commit

- Repository: `C:\Users\hhofe\Documents\grokkable projects\t3 testbed\python-spawner`
- Branch: `python-test/spawner`
- Parent HEAD: `2469802febb1d9c2e4885830b5b8ae36b668bb83`
- Current HEAD: `f90cbc7950709481520058887baa093f66f9e3ff` (v0.11)
- VERSION: **v0.11**. Cache identity `lod-v30-river-road-town-narrow-lot` / `6ffc52d24b8ccc8493fb93cd`.
- Milestone: five-improvements + progressive loading + quirk-fixes. Elevation-then-rivers remake was reverted (c0919) and is not in this version.

## Restored

- `world.elevation` is distance-to-river + `_network_potential` again. `window()` emits contours, rivers, roads, rail, towns, countryside, bridges. No lakes / relief wash / `layers='land-water'`.
- Control page has Find/Previous/Next town, Town zoom, all settings groups, generation meter. Status copy is "Temporary grid; … building 32-mi basin".
- `chunks.generate` calls full `window()` (no land-water). `preview_svg` is the cheap section grid.

## Tests

- Fresh `py -m pytest -q` from python-spawner/ after the revert: **154 passed in 353.87s (0:05:53), exit 0**. Same count as the pre-remake quirk-fixes suite.

## Tests

- Fresh `py -m pytest -q` from python-spawner/: **154 passed in 353.87s (0:05:53), exit 0**.

## Exact next action

Pushed `python-test/spawner` as v0.11 (`f90cbc7`). Hard-refresh http://127.0.0.1:8765/. Do not merge, rebase, force-push, or open a PR.

---

## Historical: Quirk fixes (visual) — previous run, preserved dirty work

Repository C:/Users/hhofe/Documents/grokkable projects/t3 testbed/python-spawner; branch python-test/spawner; HEAD 2469802. No commit/push (pre-existing dirty edits from the "spawner five improvements" and "progressive loading" handoffs are preserved and untouched). User reported three visual quirks on top of the earlier findings; implemented fixes for all of them plus the earlier list.

### Done this session

1. **Roads — parallel/duplicate N-S sections at 24-mi survey correction lines.** The N-S section grid steps sideways ~635 ft at each correction line (`correction_shift` flips sign), so every vertical gravel/dirt road looked like two offset parallel segments. Added `world._correction_curve(x1,x2,y0)` (cubic-bezier S-curve) and a loop in `window()` that emits `road:{axis}:{line}:jog:{band}` polyline features bridging the offset for gravel/dirt roads (paved highways already draw their own curve). Also extended the existing corner trim to horizontal transverse-jog segments.
2. **Rivers — regular kink + half-circle.** In `world._edge_curve` non-trunk branch: `valley_amp` 0.33-0.51 -> 0.14-0.24 of chord, `bows=max(1,round(chord/(.75*MILE)))` (was always 1), `bow=sin(pi*bows*t)`, plus per-wave amplitude jitter `wave_amp`. Measured max lateral bulge 0.70 mi -> 0.34 mi on a 1.4 mi reach.
3. **Towns — no lots on outside edge streets.** `region.make_town` now appends a full outer lot ring: four full faces (`town:outer:S/N` via `face_lots`, `town:outer:E/W` via new `face_lots_we`), box expanded by `bld` on N/S. `face_lots_we` handles W/E (vertical-street) frontage. Outer faces flagged `outer:True` and excluded from `res` (church candidates). `commercial_layout.arrange` and `make_region` (edge businesses) now include outer-lot rects in `occupied` so edge/highway-strip businesses don't overlap them. Parity mode is gated off (`parity = option("generation.parity")`).
4. **Earlier quirks:** narrow-lot spawn cliff (detached-garage rear fallback in `spawner.place_on_lot`; 60 ft now 100%), `numpy` added to requirements.txt, `tight_layout`->`subplots_adjust` in map_render.py, VERSION->v0.11 (+ history entry), README test-time corrected, chunk cache identity bumped to `lod-v30-…` (test_chunks hash updated to `6ffc52d24b8ccc8493fb93cd`).

### Tests

- `tests/test_region.py` + `tests/test_settings.py`: **32 passed** (after fixing rail-town box and edge-business overlap).
- `tests/test_spawner.py`: 35 passed (new `test_narrow_lots_spawn_valid_houses`).
- `tests/test_river_shapes.py`: 7 passed.
- Full `py -m pytest -q`: **154 passed in 355.36s (0:05:55), exit 0** (log: pytest-full4.log).
- Screenshots in `artifacts/quirk-fixes/` (town-seed31*.png, rivers-fixed.png, road-corner.png, house-60ft.png).

### Exact next step

Nothing pending. Full suite green (154 passed). Hand back; no merge/push.

---

## Progressive loading follow-up

Recovery c0842; latest c0915. LOCALLY COMPLETE: item4 staged loading plus the added random-default request. Repository C:/Users/hhofe/Documents/grokkable projects/t3 testbed/python-spawner; branch python-test/spawner; starting/current HEAD2469802febb1d9c2e4885830b5b8ae36b668bb83. Fresh py -m pytest -q:153 passed435.53s (0:07:15), log check-results/progressive-random-full.log. Final focused command py -m pytest tests/test_cli_seed.py tests/test_generation_progress.py tests/test_chunks.py tests/test_control.py tests/test_worker_lifecycle.py -q:36 passed1.72s, progressive-random-focused-final.log. Baselines:4 loading failures0.19s (progressive-loading-before.log);3 seed failures2 passes0.19s (random-seed-before.log). Changed this follow-up: generation_progress.py and generation_preview.py(new), world.py observational hooks only, chunks.py live snapshots/versioned payloads, control.html meter and temporary layer replacement, cli.py random launch seed/explicit overrides, tests/test_generation_progress.py and tests/test_cli_seed.py(new), readme.md and CHECKPOINT.md. Previous worker/five-improvement edits remain dirty and preserved. No river geometry, topology, width, crossing, generation parameter, spacing or Part6 changes; final seed31 SVG801534 bytes exactly matches prior unobserved baseline (SHA25689024294123278ac2dc0ad27d67746ab71619e797db8dd3b5c6c5de3f7e9e09a). Cold stage first basin/main-river preview0.66s; roads374.31s, final381.41s. Cached request0.109s; interrupt shutdown exit0 in1.313s. Temporary percentage is explicitly an estimated stage completion measure, not remaining time. Real random CLI launch seed1275092222 matched initial schema; explicit --seed0/31 and preset seeds covered. Logs check-results/progressive-http-smoke.log, progressive-http-server.log, random-launch-smoke.log, random-launch-server.log. Artifacts artifacts/spawner-progressive-loading/seed31-browser-rivers.png, seed31-browser-ready.png, seed31-main-river-routes.svg, seed31-river-channels.svg, seed31-roads.svg, seed31-crossings-and-finishing.svg, seed31-final.svg (other phase SVGs in same folder). Cold cache check-results/progressive-cold-1794864768965257700; test harnesses progressive_http_smoke.py, random_launch_smoke.py and progressive_cached_preview.py retained under check-results. Browser observed100% - Ready, final tile, no console errors; owned tab closed. All owned test servers stopped. git diff --check passed. No commit or push. No new blockers or unfinished implementation; original river-strand issue c0845 remains unrelated and blocked. Cleanup: temporary loading_recovery.py writer is removed immediately after this write; evidence retained. Exact next action: restart py cli.py --serve, hard-refresh Ctrl+F5, observe random seed and staged previews; use py cli.py --serve --seed 31 for a reproducible world. Future work should begin from c0842 and this checkpoint; no additional implementation is pending for these requests.

# Historical: Spawner five improvements (previous run, not this handoff)

- Recovery claim: c0842. Durable findings and measurements: c0843-c0890. Proposed bridge style: c0849; proposed town-distance status design: c0864; proposed overview town-label design: c0869; marker baseline: c0870; renderer name metadata/clamp: c0871; cache hash baseline: c0872; v28 hash update: c0873; passing marker focused runs: c0874/c0878; v28 cold progress: c0875/c0877/c0879; unique town keys: c0876; selected-town redraw issue: c0880; seed42 overview evidence: c0881; selection regression baseline: c0882; stable-ID implementation/v29 identity: c0883; first post-fix run: c0884; passing stable-ID focused run: c0885; v29 cold-start evidence: c0886; seed42 v29 progress: c0887; fresh v29 seed42 named overview: c0888; final full pytest result: c0889; browser screenshot automation limitation: c0890; river strand issue blocked: c0845.
- Repository/worktree: C:\Users\hhofe\Documents\grokkable projects\t3 testbed\python-spawner.
- Branch: python-test/spawner. Starting/current HEAD: 2469802febb1d9c2e4885830b5b8ae36b668bb83. No commit or push.
- Tracked edits: world.py, map_render.py, chunks.py, control.html, tests/test_world.py, tests/test_chunks.py, tests/test_control.py, and this file. Evidence is under artifacts/spawner-five-improvements/. Changes remain uncommitted. Pre-existing untracked paths outside this worktree remain untouched.

## Current item status

1. **Locally complete.** Roads use 22-ft traveled surfaces and regional rendering uses a thin ROW outline.
2. **Blocked (river-strand subtask).** Rail has a distinct narrow dash, elevator/depot buildings and spur are styled, bridges use a distinct 22-ft deck. Focused bridge/style tests passed 4 in 0.73s. Review screenshots show close parallel/fanned strokes but lack target world coordinates; a duplicate-draw cause is unproven. c0845 records the blocker. Do not change river geometry, topology, width, crossings, generation parameters, or Part 5 spacing.
3. **Locally complete.** Lots/frontage/trees, buildings/spur, loaded-town Previous/Next, nearest loaded accepted-town Find, gap explanation, and persistent next-town status are implemented. Display-only town names, SVG metadata, unique site-ID keys, marker clamping, and v29 cache identity are implemented. Stable-ID selection regression suite passed 11 in 0.87s (c0885). Fresh v29 seed42 is Ready with town ID `town:rail:h:0:0`; “Mason Falls” marker was observed within the 1280x800 view (c0888). Existing visual close-town evidence is `seed31-town-close-v27.png`; fresh v29 close screenshot was not captured because browser automation stopped responding (c0890).
4. **Locally complete for progressive display.** Cold seed31 showed 0/16, then 8/16, then Ready in the earlier v27 run. Fresh v29 cold seed31 shows its temporary grid at 0/16 and 35 queued (c0886). Fresh seed42 6-mi request advanced to 2/4 chunks, 13 queued, with all four previews visible and one town loaded (c0887).
5. **Locally complete.** Details/stages collapse by default, duplicate names/version are removed, the header stays on one row at 1280px, and the map receives more width.

## Current verification and evidence

- Final fresh full suite: `py -m pytest -q` passed 137 in 431.54s (0:07:11), exit 0; log `check-results/spawner-v29-final-pytest.log`, claim c0889. Earlier 131-pass suite remains stale. `preview_snapshot` and `preview_evaluate` timed out; `preview_open` hung; computer-use fallback returned native pipe unavailable after reset/retry (c0890).
- Focused fresh results: bridge style/deck 4 passed in 0.73s; town distance/navigation 2 passed in 0.16s. Test-first bridge and town-distance baselines and logs are listed in the recovery claim.
- New test-first marker baseline: 2 expected failures in 0.31s because the deterministic name function and town-name SVG metadata/placement path are absent; log check-results/spawner-town-marker-baseline.log; metric claim c0870.
- Post-change focused command passed 5 tests and failed 1 in 0.53s; the only failure is tests/test_chunks.py pinning the previous v27 renderer hash c632d659f54205554c4af10b. The current v28 hash is 75af96767908cdf28e4036d2. Log check-results/spawner-town-marker-focused-baseline.log; claim c0872.
- Updated tests/test_chunks.py to expect the measured v28 hash 75af96767908cdf28e4036d2. The assertion remains strict; focused rerun is pending (claim c0873).
- Fresh focused command now passes 10 in 1.01s: town display name/SVG metadata, marker clamp, town distance, quiet page, progressive status, bridge styling/width, rail style, and v28 cache identity. Log check-results/spawner-town-marker-focused.log; claim c0874.
- Newly restarted v28 server and hard refresh showed a genuinely cold seed31 grid with basin/chunk progress at 0/16 and 35 queued; no blank page. T3 screenshot timed out at 15s. Claim c0875.
- Fresh seed42 6-mi request first waited behind seed31, then started its 32-mi basin at 0/4 with 15 queued; temporary grid remains visible (c0877, c0879). The SVG/browser town map uses data-site-id for unique internal keys while showing data-town-name (c0876).
- After the unique site-key change, the focused suite passed 10 in 1.23s (check-results/spawner-town-marker-focused-final.log; c0878).
- Test-first selection regression failed as expected: 1 failed in 0.25s because replacing the selected rectangle array under the same town ID resets townIndex; log check-results/spawner-town-selection-baseline.log; claim c0882.
- Source fix tracks townList by stable ID and resolves current rectangles from the Map; renderer v29 identity is d6ee9910c81ec978017ba0c4. Focused rerun pending (c0883).
- The first post-fix focused command passed 9 and failed 2 in 1.73s. The implementation uses stable IDs, so the selection harness must initialize an ID and the quiet-page assertion must expect towns.keys(); log check-results/spawner-town-selection-focused.log; c0884.
- After updating those test expectations, the focused selection/name/control/bridge/rail/cache suite passed 11 in 0.87s; log check-results/spawner-town-selection-focused-final.log; c0885.
- Existing evidence includes seed31-country.png, seed31-town.png, seed31-town-close-v27.png, seed13-overview.png, seed42-town-overview.png, cold-start-1280.png, progress-8-of-16-1280.png, fresh-render-ready-1280.png, seed31-bridge-before.png, seed31-bridge-after.png, seed31-bridge-comparison.png, and cold-start-bridge-v27-1280.png.
- Fresh v29 cold seed31 capture (artifacts/spawner-five-improvements/cold-start-v29-seed31.png) shows the temporary grid and queue at 0/16 (c0886). Seed42 progressed 0/4 to Ready, with the readable marker and stable site ID inspected (c0887-c0888). Existing visual evidence also includes `seed42-town-overview-v28.png`, `seed31-town-close-v27.png`, `seed13-overview.png`, and `seed31-bridge-after.png`. A fresh v29 screenshot could not be exported due browser tool failure (c0890); town redraw persistence is covered by focused regression.

## Exact next action

Resume only the blocked river-strand subtask: recover/reproduce the exact world windows represented by `seed31-elevator.png` and `seed13-farm-river.png`, inspect feature IDs/points and emitted SVG, add a regression for a proven duplicate stroke, then apply the smallest rendering-only fix. If exact windows or duplication cannot be established, leave c0845 blocked. The other four items and full suite (137 passed) are complete. Final cleanup: `git diff --check` passed; owned server PID 25288 was stopped after verifying its command line; temporary DB writer scripts were removed. The T3 tab remains open because the Ctrl+W preview action also timed out; no close-tab endpoint is available.

Final verification recorded: river strand subtask is explicitly blocked in c0845; the fresh full pytest suite passed 137 in 431.54s (0:07:11); c0842 contains the final branch/HEAD, files, tests, artifacts, blockers, and exact next action.

- Preserve existing work outside python-spawner. Do not retune Part 5 spacing, start Part 6, change candidate town spacing, alter river geometry/topology/widths/crossings, or touch other projects/worktrees. No merge, rebase, push, or PR.

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
| 46 | 1.488796695 -> 1.494150629 |

Seed-13 historical 9-mi smoke failure: median 1.559370355, target >=1.6, while
10 mi passed at 1.620157738. Later preview reached 1.624547 at 9 mi; the user
explicitly deferred this issue anyway. DO NOT tune it again. Current non-trunk
bow base is 0.328; fillet wave parameter cap 19 widths, with the existing physical
20-width amplitude check unchanged. These are existing trial choices, not an
invitation for more parameter experiments.

## Deferred to QA Guy

The structured list in `checks/a2-regression-failures.json` gives commit, seed,
window size, measured value, threshold and exactly three already-tried entries
for each item. It covers smoke-window sinuosity; residual spacing; cold crossing/
continuation confirmation; seed-30 density; the near-parallel bridge deck cap;
known Rb and seed-13 density baseline failures; and incomplete final audit/render
QA. No known build, save, or map-export-format failure is being bypassed.

Latest completed v7 pytest before isolating part 5: **125 passed in 590.61s**.
Final part-5-only run: **125 passed in 346.39s (0:05:46)**.
Log: `check-results/a2-part5-stop-final-pytest.log`. The v7 full audit was intentionally stopped.
Older full/quick reports are historical; do not claim all geometric checks green.
Requested part-6 PNGs were not produced. Do not generate them in this run.

## Exact next step

Part 5 implementation is committed as `3b80fe880127d40a4feacf5bfa59f7db44e241c5`; this documentation-only
handoff commit records that hash. Both are pushed ONLY to `python-test/spawner`.
STOP. No more code edits, checks, part 6, or new work are authorized in this run. A future model must wait for explicit new work.
If later authorized to help QA Guy, start from the structured deferred list and
its saved v4 metrics; reproduce one specified issue before editing anything.
Do not start part 6 or repeat the five-seed audit without explicit authorization.
Do not poll any running logs more often than once per three minutes. Bypassable
bugs get about 20 minutes, then a structured deferred record. If a visible 5-hour
or weekly usage meter reaches 90%, checkpoint first, commit/push this branch only,
and stop immediately. This agent has no exposed account-usage meter.

---
# Historical investigation notes (not active instructions)

# Item A2: river geometry, 2026-11-14

## Parts 4-6: baseline investigation in progress

Starting head reconfirmed with `git log -1`: `db78343c672c65c0c2e928c4d6124d822253060f`,
`river A2 part 3: varied meanders`, branch `python-test/spawner`.
Parts 4-6 are NOT complete; part 4 validated below; parts 5-6 remain pending.
Unrelated root untracked files remain untouched.

New checks are wired into the full runner. They use (-20,-20,40,40) mi and seeds
14/30/31/7/52. Junction JSON retains every connection's angle at 0.1 and 0.25 mi,
raw endpoint chord angle and the candidate sampled angle (the sample is NOT
actually applied by the old generator to continuations/split pieces). The seed-14
20-lowest list is printed before its summary. Baseline JSON paths:
`checks/a2-baseline-junctions.json`, `checks/a2-baseline-spacing-v2.json`,
`checks/a2-baseline-copies.json`. Spacing's initial scan was preliminary: the final
version clips to the window and excludes shared-junction pockets, including
sibling tributaries. `check-results/a2-baseline-*.log` retains command output.

Unchanged-generator pytest: **114 passed in 98.53 s**.

Two interpretation issues were raised before generator edits:

- Literal EVERY downstream connection includes artificial splits and one-in/one-out
  continuations. Part 3 deliberately pins these near 8 degrees and requires the
  0.1-mi continuation kink <=35 degrees. Seed 14 unfiltered means are 18.549 degrees
  at 0.1 mi and 32.592 degrees at 0.25 mi, with 78.891% / 46.333% under 25 degrees.
  The user was asked whether the angle targets apply to actual confluences while
  still reporting continuations separately, or literally to every connection.
- The specified literal curvature/Pearson detector with an L2 epsilon calibrated
  from noisy synthetic copies catches 190/190 synthetic pairs but **0/240** old
  `b2ee52f` within-basin pairs. Pooling historical basins also catches 0/12,720.
  Epsilon = 0.0015378363921072784 (synthetic turning-distance p95).
  Historical turning-L2 p1/p5/median = 0.756259/0.988164/1.507428;
  historical correlation p95 = 0.186357. Current seed-14 turning-L2
  p1/p5/median = 0.597129/0.694514/1.015435 and copy fraction = 263/237688
  (0.11065%). Increasing epsilon to historical p5 would reject >5% of the current
  network too. The old negative control therefore FAILS validation; it must not
  be described as passing or as the expected 87% copies. The user was asked about
  investigating phase-aligned/template-family detection versus retaining the
  literal detector and reporting this acceptance conflict.

No acceptance threshold has been relaxed. No item is marked done.

Clarifications accepted during this run:
- Angle acceptance applies to true confluences; report continuations/splits and
  literal unfiltered angles separately. A receiving channel with only one local
  tributary is still a confluence when contact is inside the receiver.
- Investigate phase-aligned/template-family copies in addition to the literal test.
  A 13-station curvature smoothing window, phase registration and mirrored-bank
  matching catches 16/240 old pairs (6.667%) and 190/190 noisy synthetic pairs
  (100%); current db78343 rates remain below 5%. Literal results are retained.
- DO NOT tune topology or density in this pass. Keep the existing Rb and density
  failures failing in the full runner, labeled BASELINE, and report changes.
  The receiver-choice experiment was reverted. Current basin topology matches
  db78343. Do not claim that the full runner is entirely green.

Completed first failing runs (before generator edits):
- `py checks/river_junctions.py --output checks/a2-baseline-junctions.json`: exit 1,
  five seeds, 5,531 unfiltered connections. Pooled mean/SD/min/under25 at 0.1 mi:
  19.212/23.141/0.06536/77.924%; at 0.25 mi:
  33.275/26.989/0.002055/43.663%. Each connection's raw/sample/final values retained.
- Clarified confluence-only check on saved db78343 seed 14: exit 1, 402 joins,
  mean 37.243/41.449 degrees at 0.1/0.25 mi. See
  `checks/a2-baseline-confluences.json` (all five seeds).
- Literal copy check: exit 1 on five seeds because the historical control was not
  detected, despite current-network copy fractions below 5%. Before family matching:
  `checks/a2-baseline-copies.json`; after detector investigation on unchanged
  geometry: `checks/a2-baseline-copy-families.json`.
- Corrected spacing scan on saved db78343 geometry: FAIL, five seeds, exact segment
  distances, clipped window, actual local curvature half-waves and junction pockets.
  `checks/a2-baseline-spacing-v2.json` supersedes the preliminary spacing JSONs.
  Violating reach-pair counts (14/30/31/7/52): 93/108/102/94/87.

Full db78343 run: `check-results/20261114-230400-882896/summary.md` (exit 1).
Quick db78343 run: `check-results/20261114-225615-136031/summary.md` (exit 0).
Meander and valley checks pass on 14/30/31 in that full baseline. Shuffled 3-mi
chunks match under PYTHONHASHSEED 1 and 2 (20 features).

### Known baseline failures: topology and density (not part of fixes 4-6)

Per-basin N3/N4 mean 5.596; range 2.80-10.375. Values outside [3,5] remain known
failures. Full values and higher-order ratios are in `checks/a2-baseline-item-a.json`.

| Seed | db78343 density (mi/sq mi) | db78343 N3/N4 by basin | Known N3/N4 outliers |
|---|---:|---|---|
| 13 | 1.574229516 | -1:-1: 3.53333; 0:-1: 3; 0:0: 7.9 | 0:0: 7.9 |
| 14 | 1.395387381 | -1:-1: 10.375; 0:-1: 2.8; 0:0: 4.5 | -1:-1: 10.375; 0:-1: 2.8 |
| 30 | 1.435789956 | -1:-1: 3.31579; 0:0: 9.16667 | 0:0: 9.16667 |
| 31 | 0.887656340 | 0:0: 4.55556; 1:-1: 9; 1:0: 4.21429 | 1:-1: 9 |
| 46 | 1.488796695 | -1:-1: 5.11765; 0:0: 5.26667 | -1:-1: 5.11765; 0:0: 5.26667 |

Seed 13 density 1.574229516 is the known per-seed density failure (target 0.8-1.5).


## Part 4: junction angles ? complete

No drainage topology changes. The receiver-choice experiment was reverted.
The old 30-ft pin was lost upstream, and collision fallbacks discarded even that
approach. The 20 lowest *confluences* on db78343 seed 14 average raw chord angle
5.867 degrees, candidate sample 62.585 degrees, finished 0.25-mi angle 1.672 degrees.
The literal 20-lowest downstream connections, including splits, average
1.508/69.276/0.167 degrees. Thus both shallow raw chords and the short-lived pin
contribute; geometry now holds an approach instead of rerouting the tree.

Pin the receiving start and incoming end over 0.3 mi, with a monotone chord-graph
blend so repair cannot fold the reach. Follow short receivers through their actual
continuations when calculating 0.1/0.25-mi directions. Apply pins downstream first,
retain them in every collision candidate, then refresh finished receiver directions.
Thin creek blends lengthen when needed to preserve the width-scaled amplitude cap.
Non-outlet trunk wavelength base is 10.5 widths to retain the 10-14 width measured
range after the longer approach windows. Outlet relief geometry is unchanged.
Canonical confluence inflow metadata prevents the meander check from misclassifying
an actual confluence as a continuation when another inflow is outside the window;
its 35-degree true-continuation limit is unchanged. Finished-map verification now
shares the arc-length metric instead of reading the last stub back.

A pytest failure exposed parallel/zero-delta segments falling through the rectangle
clipper into division by zero. That failed run is retained in
`check-results/a2-part4-approved-pytest.log`; the clipper now continues past those
planes. Final `py -m pytest -q`: **114 passed in 97.86 s**.
All five fresh-generation junction audits pass. Meander and valley audits pass on
14/30/31, including zero self-intersections/reversals/kinks and width-cap violations.
After metrics with every connection: `checks/a2-part4-metrics.json`.

Cells below are mean / SD / percent under 25 / minimum, in degrees except percent.

| Seed | 0.1 mi before | 0.1 mi after | 0.25 mi before | 0.25 mi after |
|---|---|---|---|---|
| 14 | 37.243 / 29.673 / 41.542% / 0.110 | 67.499 / 21.456 / 0.000% / 25.069 | 41.449 / 30.882 / 39.552% / 0.212 | 68.353 / 21.089 / 0.000% / 35.000 |
| 30 | 39.186 / 29.764 / 38.824% / 0.184 | 68.830 / 20.972 / 0.000% / 25.761 | 43.621 / 33.287 / 34.353% / 0.144 | 69.712 / 20.403 / 0.000% / 35.000 |
| 31 | 37.821 / 28.907 / 39.518% / 0.516 | 68.609 / 21.375 / 0.000% / 25.152 | 42.180 / 31.858 / 35.904% / 0.040 | 69.329 / 21.138 / 0.000% / 35.000 |
| 7 | 39.116 / 28.609 / 40.519% / 0.065 | 67.687 / 23.310 / 0.000% / 25.258 | 41.608 / 31.317 / 37.143% / 0.270 | 68.943 / 22.204 / 0.000% / 35.000 |
| 52 | 39.951 / 30.211 / 37.852% / 0.075 | 66.873 / 22.260 / 0.000% / 25.296 | 42.126 / 31.163 / 35.038% / 0.006 | 67.533 / 21.634 / 0.000% / 35.000 |

Pooled after: 0.1 mi: 67.923 / 21.873 / 0.000% / 25.069; max 10-degree bin 18.24%; 0.25 mi: 68.794 / 21.294 / 0.000% / 35.000; max 10-degree bin 18.54%.

Start: `6725de2` (v0.10) on `python-test/spawner`. Push only that branch.
Baseline before any A2 edit: `py -m pytest -q` 112 passed in 59.94s.
`py checks/run_all.py --quick` passed in 44.68s (seed 13, 9 mi: entropy 0.884,
E-W 14.6%, sinuosity median/spread 1.771/0.574, order-1 width 12.39-15.00 ft;
crossings/bridges 75/75; chunk hash match, 12 features; outlet SD 28.78°).

## Latest continuation: v7 validation pending

The completed v4 full audit is `check-results/20261115-024826-847644/summary.md`.
It is NOT accepted: spacing counts 14/30/31/7/52 were 21/32/31/28/15 (db78343
93/108/102/94/87), with finished crossings 2/6/4/7/2. Moved-node counts were
22/32/27/29/24, mean movements 0.173178/0.184451/0.189947/0.143632/0.167819 mi,
maxima 0.350217/0.386297/0.414266/0.376843/0.410188 mi. No node graph/width,
attachment, basin or elevation guard failed, but geometry guards DID fail.
Rb remained exactly unchanged. Seed-30 density rose from 1.435789956 to
1.500265757, a NEW failure, not an approved baseline failure. A near-parallel
road/river crossing also exposed a bridge deck cap mismatch. These remain
unaccepted until the new cold audit resolves them; do not mark items 5-6 done.

Two separate v4 pytest runs passed 124 tests (426.79s and 445.61s). The earlier
quick runner passed. Later source changes invalidate using those as final tests.

Subsequent changes: guard final continuation fillets against other channels,
check self-contacts independently of the wavelength exemption, remove numerical
zero-length sampled segments (new regression FAIL recorded in
`check-results/a2-zero-segments-before.log`), and plan both sides of a continuation
before collision checking. Search nine bounded common tangent rotations while
keeping their 8-degree difference. Cached repair clears crossings on all five
seeds; the remaining seed-31 kink is cleared by the joint tangent search
(`check-results/a2-v7-kink.log`: zero crossings/self-intersections/reversals/kinks).
These cached trials are diagnostics, not replacements for a cold audit.

Relocation now reserves space for the 0.3-mi inward receiving approach, not only
the endpoint. If that larger clearance target cannot be reached within the
approved movement/elevation/basin limits, retain and report the node. Higher-order
junction candidates prefer >=55 degrees, giving room outside the 0.5-mi pocket.
The introduced non-trunk bow base was reduced from 0.330 to 0.328 to address the
new density overshoot; no topology, density threshold or bridge code was changed.

Fresh validation logs: `check-results/a2-v7-full.log` (still running) and
`check-results/a2-v7-pytest.log`: **125 passed in 590.61s**.
Parts 5-6 remain uncommitted; only Part 4
`aff8968` has been pushed. No new acceptance or final render is claimed yet.

## Part 5: bounded clearance repair (validation in progress)

Part 4 is pushed as `aff8968`. Parts 5 and 6 are not committed yet.

The first spacing check FAILS on unchanged db78343: 93/108/102/94/87
violating reach pairs for 14/30/31/7/52. Every baseline window, worst pair and
summary is retained in `checks/a2-baseline-spacing-v2.json`.

Fixed-endpoint evidence: ten seed-14 order>=2 starts are already within 0.4 mi
of an outlet outside the junction exemption. Example
`river:0:0:edge:10:9:part:1`, start (7.297796517,18.294739824) mi, is
0.130687426 mi from `river:outlet:0:0`. No curve between unchanged ends can
meet clearance there (`check-results/a2-fixed-node-clearance14.json`). A naive
radial proposal moves at most 0.294313 mi but makes three incident edges uphill
(worst 12.714407 ft); it was rejected, not applied.

The user approved coordinate relocation subject to own-basin/3x3 inputs, a
0.5-mi movement cap, basin containment, strict endpoint descent, all incident
reaches moving together, unchanged IDs/links/orders/widths, and fixed outlets.
Nodes with no admissible candidate stay fixed and must be reported with their
distances. `river_relocation.py` searches upstream first, using radial and
clearance-contour candidates with H(seed, integer/string keys). Decisions use
immutable canonical outlet curves, never another basin's mutable render cache.
Reports include moved-node count, mean/max movement, blocked nodes and rejection
counts, every residual violating pair and graph/width/attachment/downhill guards.

Clearance repair uses id-sorted segment buckets and exactly eight phase trials
H(seed, edge_id, 'space', k), k=0..7, with amplitude 1-0.12k, then valley-only
and straight candidates. Pins survive all candidates. If none satisfies
clearance, retain a crossing-free candidate and explicitly report unresolved
clearance. A failed straight candidate is not counted as an accepted fallback.
Up to four bounded junction/clearance sweeps let a crossing-forced approach
relax toward its raw chord, while retaining the measured statistical angle
requirements. Incoming angular order is preserved; coincident degree samples
are separated. Proposed approaches are checked against fixed outlet curves.

Longer junction blends had erased small-reach waves and failed the existing
sinuosity smoke check (1.358 initially, 1.559 in the later bounded trial).
Seeded variable-wavelength fillet waves, with zero end offset and slope, retain
winding while the existing 20-width physical amplitude check remains enforced.
The final trial uses a 19-width wave parameter cap and a small non-trunk valley
bow adjustment (0.30 to 0.33 chord base). Seed 13's targeted 9/10-mi measurements
are now 1.625/1.666 sinuosity and 1.557/1.555 density; the 10-mi density baseline
was 1.574229516. These are previews; the full report is authoritative.

New regression failures were recorded before their fixes:

| Guard | Recorded failing run |
|---|---|
| Relocation/incident attachment, infeasible height, iteration order | `check-results/a2-relocation-tests-before.log` (3 failed) |
| Canonical inputs despite mutated cached outlet rendering | `check-results/a2-stability-tests-before.log` (1 failed) |
| Distinct reach identities sharing a phase group | `check-results/a2-reach-identity-before.log` |
| Exclude out-of-window segments from bearing statistics | `check-results/a2-window-bearings-before.log` |
| Illegal crossing versus legal endpoint/interior confluence | `check-results/a2-crossing-scan-before.log` |
| Equal sampled angles must not overlap approach stubs | `check-results/a2-overlapping-stubs-before.log` |
| Final continuation fillet must not reintroduce a self-crossing | `check-results/a2-final-rounding-before.log` |

The pin-repeat test is a positive stability guard, not a claimed fail-first test.
It compares geometric differences below 1e-6 internal units: the cubic calculation
can differ by one floating-point ULP on a second application. No river acceptance
threshold changed. Ten focused regressions pass in `check-results/a2-final-rounding-after.log`.
Committed failure transcripts: `checks/a2-regression-failures.json`.
The last continuation repair shortens only self-crossing Hermite handles in
five bounded steps; endpoints, tangents and the 0.1-mi straight sample stay fixed.
The clearance lookup optimization gives exactly identical saved features before
and after (`check-results/a2-bucket-equivalence.log`).

## Part 6: copy detector (validation in progress)

Shared `river_geometry.copy_metrics` resamples every order-1/2 feature, including
split pieces, at 64 equal arc-length stations. Normalize translation, chord
rotation and total length; compare station curvature with Pearson >=0.9 or
turning-function RMS below frozen epsilon 0.0015378363921072784. Epsilon is the
unchanged db78343 noisy-control turning-distance p95, not tuned to passing data.
The user-approved additional family detector smooths 13 stations, registers
cyclic phase and admits mirrored banks at absolute correlation >=0.9. Literal
and family counts remain separately visible.

The first literal historical negative control FAILS detection (0/240); the
synthetic control detects 190/190. Family registration detects old b2ee52f
16/240 (6.667%, not the expected 87%) and synthetic 190/190 (100%). Both controls
must reject network acceptance at >=5%. Their distributions and the initially
passing db78343 network rates are saved in `checks/a2-baseline-copy-families.json`.
All five current-network rates, p1/p5/median turning distances, final junction
angles, spacing residuals, meander/valley checks, density/Rb and chunk comparisons
are being regenerated. Final results and the acceptance table follow after that
run; no all-green or zero-clearance claim is made here yet.

## Checklist

1. Versioned chunk cache — done. See below.
2. Curved valley paths — awaiting visual review. Numbers and render paths are below. Do not treat this part as done until those maps have been looked at.
3. Varied meanders, no kinks — awaiting visual review. Numbers and render paths are below. Do not treat this part as done until those maps have been looked at.
4. Junction angles — `checks/river_junctions.py`. All joins, unfiltered, last 0.3 mi vs receiving channel. Seeds 14/30/31: mean 55-75°, under 25° < 5%, minimum >= 10°.
5. Channel spacing — `checks/river_spacing.py`. Order >= 2 channels that do not join within 0.5 mi stay at least 0.4 mi apart. Violation count 0 on the 40 mi window; print the worst pair.
6. Real copy test — resample each branch over 3 mi, compare normalised curvature profiles, flag correlation >= 0.9. Must fail on `b2ee52f` and on a same-recipe sine pair, and flag under 5% on the new network.

## Part 1: versioned chunk cache — done

Cache path is `out/cache/<VERSION>-<short git hash>/<seed>/<lod-key>/<x>_<y>.svg`.
Hash is `nogit` when git cannot be run. README says old `out/cache/*` folders can be deleted.

Before (on `6725de2`): `tests/test_chunks.py::test_versioned_chunk_cache_path_contains_version_and_hash` failed. Built path `out/cache/864201/<lod-key>/0_0.svg` (and the halo tiles) did not contain `v0.10-6725de2`.

After: that test and `test_cache_version_tag_uses_nogit_when_git_is_unavailable` passed (2 passed in 0.17s).
`py -m pytest -q`: 114 passed in 61.40s.
`py checks/run_all.py --quick`: passed in 43.96s, same seed-13 metrics as the baseline (entropy 0.884, sinuosity 1.771/0.574, 75/75 bridges).

## Part 2: curved valley paths — awaiting visual review

Check `checks/river_valley.py` was shown failing on head `e709402` before `world.py` changed.
`py checks/river_valley.py` exit 1, 92.19s.

- seed 14: median valley sinuosity 1.002; edge mi p10/median/p90 1.308/1.513/2.042; straight-run share 50.14%; points/mi min 0.5
- seed 30: 1.002; 1.299/1.519/2.039; 47.15%; min 0.5
- seed 31: 1.002; 1.289/1.547/2.043; 47.42%; min 0.3

After the valley centreline, the same command exited 0 in 163.27s.

- seed 14: median valley sinuosity 1.245; edge mi p10/median/p90 0.746/0.990/1.379; straight-run share 1.76%; points/mi min 9.3
- seed 30: 1.247; 0.745/0.992/1.378; 2.08%; min 9.9
- seed 31: 1.247; 0.750/0.988/1.375; 2.01%; min 8.1

First full pytest after the curves: 2 failed, 112 passed in 63.69s. `test_drainage_continuity_and_downhill` found split pieces whose end sits above the start (one example, seed 13, rise 5.19 ft on a 0.93 mi piece). `test_finished_river_metrics_smoke_fails_on_comb_geometry` had sinuosity spread 0.214 on the 4 mi window. Outlet centreline stays on the old meander so the relief field does not move. Long chords split only where every piece still drops. Meander amplitude varies per reach by a factor from 0.70 to 1.40. A later pytest is 114 passed in 56.40s. `py checks/run_all.py --quick` passed in 51.82s: seed 13, 9 mi, entropy 0.928, E-W 17.9%, sinuosity median/spread 1.965/0.642, order-1 width 12.39-15.00 ft, bridges 85/85, chunk features 17, outlet SD 28.78°.

Review renders, not committed (`check-results/` is ignored):

- `python-spawner/check-results/a2-part2/seed14-40mi.png`
- `python-spawner/check-results/a2-part2/seed30-40mi.png`
- `python-spawner/check-results/a2-part2/seed31-40mi.png`
- `python-spawner/check-results/a2-part2/seed14-8mi.png`

The review of these maps found folded joins and a uniform trunk wiggle. Part 3 fixes both.

## Part 3: varied meanders — awaiting visual review

Check `checks/river_meander.py` was shown failing on head `8f3a90a` before the new centreline. The amplitude metric is a 0.22 mi high-pass on the middle of the reach, not a cubic residual of the valley bow. `py checks/river_meander.py` exit 1, 111.60s.

- seed 14: self-intersections 33; reversals 13; continuation kinks 168; order-1 over 20x width 0.0% (0/719); wavelength CV median 0.243, below 0.25 on 52.9% (567/1072); trunk sinuosity median 1.970; trunk wavelength/width 34.76; trunk gentle length 6.5%; trunk amplitude CV 0.607
- seed 30: self-intersections 10; reversals 9; kinks 164; order-1 0.0% (0/698); CV median 0.238, below 55.1% (590/1071); trunk sinuosity 1.893; wavelength/width 37.07; gentle 6.0%; amplitude CV 0.580
- seed 31: self-intersections 32; reversals 14; kinks 204; order-1 0.0% (0/701); CV median 0.244, below 52.9% (568/1074); trunk sinuosity 1.968; wavelength/width 33.79; gentle 6.7%; amplitude CV 0.659

After the chord-graph centreline, the same command exited 0 in 148.89s.

- seed 14: self-intersections 0; reversals 0; kinks 0; order-1 over 20x width 0.0% (0/726); wavelength CV median 0.607, below 0.25 on 0.0% (0/1090); trunk sinuosity median 1.224; trunk wavelength/width 11.33; trunk gentle length 71.4%; trunk amplitude CV 0.615
- seed 30: self-intersections 0; reversals 0; kinks 0; order-1 0.0% (0/689); CV median 0.612, below 0.0% (0/1092); trunk sinuosity 1.215; wavelength/width 11.69; gentle 74.7%; amplitude CV 0.587
- seed 31: self-intersections 0; reversals 0; kinks 0; order-1 0.0% (0/705); CV median 0.616, below 0.0% (0/1062); trunk sinuosity 1.197; wavelength/width 11.49; gentle 75.7%; amplitude CV 0.659

`py checks/river_valley.py` still passes, exit 0 in 144.25s. Seed 14 median 1.302, edge mi 0.746/0.989/1.379, straight 0.61%, points/mi min 26.4. Seed 30: 1.305, 0.748/0.992/1.380, 0.69%, 26.4. Seed 31: 1.289, 0.750/0.988/1.375, 0.52%, 26.5.

`py -m pytest -q`: 114 passed in 68.60s. `py checks/run_all.py --quick` passed: seed 13, 9 mi, entropy 0.903, E-W 21.1%, sinuosity median/spread 1.705/0.953, order-1 width 12.39-15.00 ft, bridges 77/77, chunk features 20, outlet SD 28.78°.

Outlet centreline stays on the width-scaled wave. A valley bow there moves relief and makes a headwater edge climb. A step also has to lose elevation, not only network potential. Wide trunks shorter than 4.5 wavelengths keep one arch, because a partial wave train measures as four similar bends.

Review renders, not committed (`check-results/` is ignored):

- `python-spawner/check-results/a2-part3/seed14-40mi.png`
- `python-spawner/check-results/a2-part3/seed30-40mi.png`
- `python-spawner/check-results/a2-part3/seed31-40mi.png`
- `python-spawner/check-results/a2-part3/seed14-8mi.png`

Next: part 4, junction angles, after these four maps have been looked at.

---

# Item A WIP: basin rivers and check harness, 2026-11-14

Starting head confirmed as `b2ee52f`. The old `drainage_*` templates and their
lane-bend test are removed. Rivers now use rotated 32-mi Voronoi basin cells,
seeded outlet routing and terrain potential, local acyclic headwater graphs,
Strahler order, area-based widths, pinned meanders and seeded junction angles.
Outlet-route widths propagate through downstream joins. `crossing_point` handles
collinear overlap. Chunk-order, topology, bridge, metric and lattice checks live
in `checks/`; `py checks/run_all.py`, `--quick` and `--quick --seeds 13` all
completed successfully.
`check-results/` is ignored; `checks/example-summary.md` is the committed sample.

Fresh normal suite: `py -m pytest -q` — 111 passed in 54.97s. The finished-map
smoke was measured on b2ee52f at seed 13, 4x4 mi: bearing entropy 0.701,
sinuosity median 1.0 and spread 0.0, so it fails the new-map thresholds.

Full checks (`check-results/20261114-140501-467000/summary.md`): mean density
1.427 stream-mi/sq-mi; pooled sinuosity median 1.736, p10-p90 spread 0.463;
pooled N3/N4 3.471; per-basin N3/N4 mean 4.896 (range 2.40-10.00); junction
mean/SD 65.26/24.87 degrees, 7.69% under 25 degrees, largest 10-degree bin
17.16%; copy rate 0/397; order-1 widths 12.39-15 ft and largest trunk 150 ft;
zero measured confluence-width violations. Per-seed density ranged 0.948-1.771.
Five-seed topology/crossing checks passed; 276/276 bridge decks matched; shuffled
3-mi chunks matched under both PYTHONHASHSEED values (12 features); 20/20 outlet
crossing positions were unique and minimum regional bearing SD was 28.67 degrees.

Still open in A: individual basin N3/N4 values exceed the 3-5 range even though
their mean passes; seed 13's 100-sq-mi density is 1.771. The 20/70/10 growth
labels do not yet control branch topology. Channelized reaches, minimum spacing
between neighboring river limbs, road/rail clearance and crossing bends, and
zoom-based meander hiding remain unimplemented or unmeasured. Direct node-to-node
edges in seed 13 basin (0,0) measured 464 edges, 1.181-mi minimum, 1.530-mi
median and 2.644-mi maximum; the 0.6-1.5-mi edge target is not met. Continue
tuning and measuring these before calling A complete.

---

# Final handoff: drainage-first rebuild, 2026-11-14

Items 1-7 are implemented and pushed in order. Important item-6 limitation:
6-10 mi is the candidate rail-station spacing; floodplains / junction competition
can omit stations and leave larger gaps between ACCEPTED towns. Do not describe
that upper bound as guaranteed for every accepted pair. All requested screenshot,
shared-style and baseline work is complete. This section supersedes WIP below.

Implementation commits: 1 e7f582d (checkpoint edc4b44), 2 f0164fd, 3 b94b173,
4 f834d61, 5 2d14e32, 6 95f48f5, 7 840b56b. Shared style and review fixes ac2e682;
this final commit adds round water caps (removes antialias seams between reaches),
final baselines and real-browser evidence. Cache lod-v25-channel-joins.

Fresh full test run: py -3.11 -m pytest python-spawner/tests -q --tb=short
109 passed in 59.93s. Five-seed geometric verifier (13,14,30,31,46) all passes;
artifacts/final-verification.json and final-validation.txt record results.
Real Chromium same-rectangle SVG vs PNG: seed31 (-4,-3,10,10) mi, 800px:
RGB MAE 0.583765/255, severe-pixel fraction 0.0001078125 (0.0108%). Exact palettes
and stroke coverage also checked in pytest. Browser minimum strokes remain
water 1.498px and rail/paved 1.2px at 24/80/256px chunk raster sizes. No far-zoom
palette or visibility-rule change: gravel still drops above 48 mi; simple tree
markers and in-block trailers stay, no trailer parks. Rural lot shape rules stay.

Parity: tools/check_images.py freshly regenerated all five byte-identical LEGACY
pairs and five NEW world baselines. World changes are intentional: branching basin
geometry/valleys, drainage-informed continuous highways, 24-mi corrections,
bridges, two rail directions, route-owned towns and trackside industry, and farm
density/access. WORLD_STYLE supplies the shared export/browser palette, screen
minima and layer order. World exporter snapping was disabled to match browser
subpixel positions. Five world PNG checksums match world-baselines.json; a separate
fresh seed31 regeneration matches. Legacy parity does not test the changed world.

All seven final screenshots are from the real browser, seed31, Ready and loaded:
- artifacts/final-seed31-x-4.5-y-1.5-3mi.png
- artifacts/final-seed31-x-4-y-3-10mi.png
- artifacts/final-seed31-x-15-y-15-30mi.png
- artifacts/final-seed31-x-48-y-48-96mi.png
- artifacts/final-seed31-x0.85-y0.6-rail-road-crossing.png
- artifacts/final-seed31-x-1.5-y22.5-paved-correction-jog.png
- artifacts/final-seed31-x-3.4-y-0.3-bridge.png
96 mi: 1,024 loaded visible chunks, zero transparent center pixels, no server errors;
see browser-overview-seed31-x-48-y-48-96mi.json. Cold 96-mi generation still takes
minutes; no speedup is claimed. Screenshot checksums: final-screenshots.json.
Full human-readable report: artifacts/final-report.md. Temporary servers and the
owned browser tab were closed after capture. Restart cli.py --serve to inspect.

No PR, merge, force push or history rewrite. Only python-test/spawner was pushed.
Unrelated root newprompts/ and reference/ left untouched. Read only the files
named in the original resume allowlist below if continuing work.

--- Prior checkpoint ---
# Final verification WIP, 2026-11-14

Numbered stages 1-7 are implemented and pushed in the commits listed below.
This follow-up fixes issues found in real-browser review and unifies rendering:
- Tributary sources/joins now stagger by stream ID instead of sharing fixed
  stations. Topology, downhill, unique-shape and sinuosity regressions pass.
- A floodplain-blocked rail elevator rejects that town candidate; accepted rail
  towns have both elevator and depot. Industry reservations also protect rural lots.
- E-W paved survey rectangles remain continuous across correction corners;
  the strengthened test includes the negative/zero boundary that exposed a gap.
- WORLD_STYLE in world.py supplies colors, screen minima, LOD hiding and layer
  order for PNG export and browser. Raw SVG carries style attributes; the browser
  applies them generically. PNG disables Matplotlib snapping and matches SVG
  layer order. Legacy reference PNG rendering is unchanged.

Fresh full run: 109 passed in 49.22s. Fresh geometric verifier: seeds 13,14,30,31,46
all pass (artifacts/final-verification.json). Real Chromium same-rectangle PNG/SVG
comparison: seed31 [-4,-3,10,10] mi, 800px, RGB MAE 0.591145/255; only 0.0136%
pixels differ by >40 (antialiasers differ). The test limit is MAE 1.5 / 1% severe;
pytest also strictly checks colors and subpixel line coverage in one rectangle.
Measured farm densities: seed14 1.58, seed31 1.4575, seed46 1.4975 / sq mi.

Current cache lod-v24-continuous-ew-survey. Fresh server on 8004. Real browser tab
owned by this session, current 30-mi view generating. Final required screenshot
collection and world rebaseline are IN PROGRESS; do not claim complete yet.
tools/check_images.py now also writes five canonical world PNGs and their style /
geometry manifest under artifacts/parity, after checking unchanged legacy pairs.
World rebaseline changes are intentional: basin topology/valleys, main routes,
corrections, bridges, both rail directions, route towns, farm density and access,
and corrected export palette / line widths. Only legacy mode remains byte-identical.

Remaining: finish/save current-build 30 and 96-mi screenshots, ensure all seven
requested views are fresh/decoded; finish image script; record browser checks,
commit and push final artifacts/checkpoint. No further numbered stage is pending.
Town spacing caveat: 6-10 mi describes candidate rail stations; floodplains or
six-mile junction competition can omit stations and leave larger accepted gaps.
Read only files named below. Push only python-test/spawner; no PR/merge/rewrite.

--- Prior checkpoint ---
# Item 7 complete, 2026-11-14

Stable world farm ownership thins the old 2.7/sq-mi placement to about 1.5/sq mi,
without changing rural lot shapes, field subdivision or acreage placement.
Farm-only access fractions position the existing yard along a measured 250-830 ft
road-to-house lane; outbuildings, grove dots and chamfer rules are retained.
The default rural.layout API keeps historical behavior unless world mode supplies
an access fraction. A few close same-road yards can share a seeded entrance and
60-ft front connector; old private road entries are trimmed, water is checked,
and the resulting complete access paths remain <=830 ft. Overview stays farm dots.

Fresh full run: 108 passed in 53.29s. New tests measure 1-2 farmsteads/sq mi for
seeds 14/31/46 over 20x20 mi, inspect real generated access polylines against their
250-830 ft lengths, and test shared entrance/trim ownership. Cache v21.

Numbered implementation stages 1-7 are complete, with the item-6 caveat that
6-10-mi town stations can be omitted by junction competition or floodplains.
Commits: 1 e7f582d / edc4b44, 2 f0164fd, 3 b94b173, 4 f834d61, 5 2d14e32,
6 95f48f5; this commit 7. REMAINING REQUIRED WORK: one shared PNG/browser style
table and same-rectangle verification; changed world image rebaselines with
explanation; fresh real-browser 3/10/30/96 mi and rail-road / correction-jog /
bridge screenshots. Existing server 8002 is stale item-1 code; restart for captures.
Read only files named below. Push only python-test/spawner, no PR/merge/rewrite.

--- Prior checkpoint ---
# Item 6 complete, 2026-11-14

Town owners now follow seeded stations along N-S and E-W rail: candidate station
intervals are 6-10 mi (8 +/- seeded jitter). Highway towns are independently
thinned along paved routes, and six-mile local priority competition prevents
close rail/highway pairs. Junction competition and floodplains can omit a station;
6-10 mi is the along-line candidate spacing, not a guarantee across omitted sites.
Towns retain region block/lot variety, edge thinning, 60/30/10 tiny/mid/big weights,
70/30 downtown/highway commercial arrangement, and in-block trailer runs. Town
plats rotate with the rail/road; main streets are paved inside the envelope.
Rail towns export the previously invisible elevator/industrial parcel and depot
beside the track. Urban driveways and simple tree markers use the existing plans.
Rural tile town lottery is disabled; farm lots intersecting a town envelope are
excluded as a group without changing their layouts. Canonical site caching keeps
ownership independent of query order. Cache lod-v20-route-towns.

Fresh full run: 107 passed in 48.15s. Added station spacing/jitter, accepted town
minimum distance, skewed sizes, off-rail towns, and elevator/depot presence tests
across an 80x80 mi sample. Existing overlaps, town spacing, buildings and tree
checks pass. No final screenshots yet.

Completed commits: 1 e7f582d / edc4b44; 2 f0164fd; 3 b94b173; 4 f834d61;
5 2d14e32; this commit 6. NEXT item 7: measured farm density currently 2.69-2.77
per sq mi, so reduce via stable world placement ownership, lengthen access lanes,
and add small shared-lane clusters. Keep rural lot shapes/layout rules otherwise.
Then shared style / same-rectangle test, world rebaselines and final browser
3/10/30/96 mi + crossing/jog/bridge screenshots. Read only files listed below.
Push only python-test/spawner, no PR/merge/history rewrite.

--- Prior checkpoint ---
# Item 5 complete, 2026-11-14

Rail has N-S and E-W route owners with seeded activation and gentle C2 bends
between straight crossing approaches. Horizontal rail approaches align with the
corrected N-S road coordinates; vertical rail aligns with E-W section lines.
Both share exact canonical section endpoints. Default spacing is 20 mi at 70%
activation in both directions, targeting roughly 29 rail route-mi per 400 sq mi;
a 400x400-mile regression checks the measured range. No diagonal is forced.
Rail runs beside the road corridor, leaving room for paved correction curves.
Industrial town siting follows in item 6. Bridge generation covers both axes.

Fresh full run: 106 passed in 41.71s. Tests check both directions, density,
curvature and shared endpoints, plus >=45-degree crossings and >=80% within
15 degrees of square for three seeds around correction boundaries. Updated old
straight-only expectations to the newly requested gentle curves. Cache v19.

Completed: 1 e7f582d / edc4b44, 2 f0164fd, 3 b94b173, 4 f834d61; this commit 5.
NEXT item 6 towns, then 7 farmsteads. Shared PNG/browser style and world parity
rebaseline still due, as are final real-browser captures. Read only files named
below. Push only python-test/spawner, no PR/merge/history rewrite.

--- Prior checkpoint ---
# Item 4 complete, 2026-11-14

Bridge decks now belong to (carrier road/rail ID, canonical river segment ID).
Segment intersection locates crossings and interpolates tapered water widths;
deck length accounts for the crossing angle. All paved roads and rail bridge,
and gravel/dirt bridge too: this policy preserves every surviving frontage road
without water closures. Candidate segments use one-mile bins. Deck ownership is
independent of request/enumeration order, and decks are drawn over water.

Fresh full run: 105 passed in 38.64s. Added an actual 12x12-mile exhaustive
crossing-to-deck test plus synthetic paved/rail and reversed-order checks.
Cache lod-v18-owned-bridges. Final bridge screenshot pending fresh final server.

Completed commits: item 1 e7f582d + edc4b44; item 2 f0164fd; item 3 b94b173;
this commit completes item 4. NEXT item 5 rail in both directions and density /
angle checks, then 6 towns and 7 farmsteads. Shared style, world rebaselines and
final requested browser captures remain due. Read only files listed below.
Push only python-test/spawner, no PR/merge/rewrite.

--- Prior checkpoint ---
# Item 3 complete, 2026-11-14

All N-S survey roads now shift sideways at globally anchored 24-mile correction
parallels. Seeded integer-tick offsets alternate sides, so no correction is
accidentally zero. E-W correction roads stay continuous even at maximum missing
road settings, providing the connecting jog for gravel as well as pavement.
N-S paved geometry incorporates the same offsets and retains exact shared route
endpoints and wide corners. Region placement uses those corrected N-S road
coordinates; rural layouts are unchanged. The gap verifier tests physical
coordinates rather than expecting an off-viewport corrected line in the view.

Fresh full run: 104 passed in 40.46s. New test covers negative and positive 24-mi
boundaries, seed variation, actual shifted survey sections, continuous E-W edges,
and exact paved joins. Cache lod-v17-correction-lines. No final screenshots yet;
server on 8002 still has the item-1 code and must be restarted before final capture.

Completed item commits: 1 e7f582d + checkpoint edc4b44; 2 f0164fd; this commit 3.
NEXT item 4 bridges, then 5 rail, 6 towns, 7 farmsteads, shared style and world
rebaseline, final browser screenshots. Read only files listed below. Push only
python-test/spawner; no merge, force-push, history rewrite or PR.

--- Prior checkpoint ---
# Item 2 complete, 2026-11-14

Paved routes are planned from drainage before missing gravel edges. Each owns
continuous 24-mile sections with exact shared endpoints, one/two-mile crossroad
jogs and 950-foot rounded corners. Route spacing stays nine miles; the seeded
shared band shift chooses the approach away from drainage junctions. Retained
survey-edge IDs support addresses and deletion verification; their ends trim
at curved corners. Missing-edge sampling never removes pavement. Town candidates
still use the existing region placer pending item 6. Rejected town lots with no
frontage street now skip safely instead of raising StopIteration.

Fresh full run: 103 passed in 39.55s. Added 70-mile route continuity, curved-jog,
missing-route and missing-section checks; preserved the old deleted-edge test.
Sample over both axes in 96x96 mi: 82.53% gravel, 12.11% paved, 4.48% Level B,
0.87% missing. The verifier inspects actual polylines and paved survey sections.
Cache version lod-v16-continuous-paved. Existing far-zoom stroke values retained.

NEXT item 3: seeded 24-mile correction offsets for ALL N-S section roads, continuous
E-W roads and paved jog integration. Then items 4-7, shared style, world rebaseline,
and final browser screenshots. Item 1 commits e7f582d / edc4b44. This commit is item 2.
Read only the files listed below. Push only python-test/spawner, no PR/merge/rewrite.

--- Prior checkpoint ---
# Item 1 complete: e7f582d, 2026-11-14

Resumed at f397d8b on python-test/spawner. Drainage uses coarse basin owners
H(seed,'basin',rx,ry), shared boundary anchors, southwest shear, seeded spacing,
region-specific trunk direction and bends, and independently seeded tributary
and headwater meanders. Three orders (1/2/3) widen at confluences; tributaries
start small and taper downstream. Every downstream endpoint belongs to its
receiving reach. Terrain measures distance to ALL channels, using spatial bins.
World make_region receives the global water network as its placement barrier;
it never calls old per-tile make_creeks. Town blocks clip at the floodplain.

Fresh full validation: 102 passed in 31.33s. Seeds 13,31,46 pass continuity,
acyclicity, downhill and no-crossing checks over 40 mi. New tests check order,
widths, angled joins, unique normalized shapes, median sinuosity of 1.6-2.5,
shared basin endpoints, valley elevations, and forbidden legacy creek calls.
All five regenerated legacy parity PNG pairs remain byte-identical. Those
exercise unchanged legacy region mode; world rebaselines and shared PNG/browser
style verification are still pending with the later stages. Real Chromium:
artifacts/item1-seed31-x-1-y0-3mi.png (Ready and decoded; town/road context).

NEXT: item 2 paved routes, then 3 correction lines, 4 bridges, 5 rail, 6 towns,
7 farmsteads, in order, commit and push each. Final shared-style/world parity
checks and real-browser 3/10/30/96 mi + crossing/jog/bridge captures remain due.
Server started on port 8002; restart after Python changes. Read only the files
named below. Root newprompts/ and reference/ remain untouched. Push only
python-test/spawner; no PR, merge or history rewrite. Checkpoint encoding is
UTF-8: use explicit encoding when writing it. This follow-up records e7f582d
because the first checkpoint write hit a Windows encoding error.

--- Prior checkpoint ---
# Item 1 verification preparation â€” WIP, 2026-11-14

Resumed at b282432 on python-test/spawner. Item 1 is STILL INCOMPLETE;
items 2-7 have not been started. This WIP changes verification only, not geometry.
The next implementation step remains the basin replacement described below.

Extracted drainage_topology_checks: directed downstream ownership may join an
interior canonical vertex of a receiving reach, instead of requiring its first
vertex. Missing/wrong receivers still fail. Added iterative cycle detection so
connected endpoints cannot disguise a cyclic drainage graph. Query-halo boundary
receivers are checked only for selected features; cycles are checked throughout
the supplied graph. Tests cover interior confluences, wrong/missing receivers,
a connected two-reach cycle and a 2,000-reach chain without recursive traversal.

Fresh full test run: 100 passed in 43.47s. Parity: all five regenerated legacy PNG pairs byte-identical. No new browser screenshots: this WIP changes no rendering. The requested
3/10/30/96 mi and crossing/jog/bridge screenshots remain pending; historical
screenshots are not evidence of the unfinished overhaul. No numbered item is
claimed complete. Read only the files named by the following checkpoint section.

--- Prior checkpoint ---
# Real-life generation order request â€” WIP, 2026-11-14

Started at 265c044 on python-test/spawner. This section supersedes the previous
"no further implementation steps pending" statement. The seven numbered items
in the new request are NOT complete. Preserve all earlier styling/foliage and
rural layout decisions. Push only python-test/spawner; no PR, merge or rewrite.

Fresh full validation: 98 passed in 40.94s. Legacy parity: all five pairs byte-identical.

Partial item 1 implemented: tributaries now carry monotonically increasing vertex
widths (6% to 50% of trunk width), rendered as tapered SVG reaches. Conservative
maximum widths still protect lots/towns. Branch ids seed a separate bend phase
and frequency. Cache version lod-v14-tapered-branches prevents stale SVG reuse.
These are incremental changes, NOT the requested replacement basin network:
branches still use the trunk scaffold, trunks remain north-south lanes, terrain
still derives from trunks, and branching orders beyond 1 remain outstanding.
The first, stronger perturbation failed the existing downhill verifier; reduced
variation passes the 14 world tests including continuity, crossings and downhill.
New test covers strictly increasing widths and unique normalized branch profiles.

Parity regeneration: five before/after PNG pairs remain byte-identical. That
script exercises legacy region parity, not the changed world renderer. No world
rebaseline or requested browser screenshots have been produced yet. Do not claim
that unchanged legacy parity proves browser/PNG style agreement.
Team source /workspace/pytest-check3/review/realism-factcheck.md is unavailable on
this Windows machine; use the numbers in the user's request.

Next: finish item 1 basin topology, independently owned geometry, variable trunk
direction/spacing, downstream joins, stream order and terrain from all streams;
then items 2-7 in order, committing/pushing each with test results here. Shared
PNG/browser style table and same-rectangle output test remain required. Real
browser screenshots at 3/10/30/96 mi plus actual rail-road crossing, paved
correction-line jog and bridge remain required; filenames must carry seed/coords.

Read only these files as needed on resume (paths relative to python-spawner):
- world.py, chunks.py: world topology, geometry, rendering, caching.
- region.py, rural.py, settings.py, presets.py: integration boundaries and options;
  rural lot layout stays untouched except the requested world placement interface.
- map_render.py, control.html, control.py, cli.py: shared style/export and browser.
- verification.py, tests/test_world.py, tests/test_chunks.py,
  tests/test_control.py: geometry and regression checks.
- tools/check_images.py, tools/browser_flow.js: parity and real browser checks.
Run py -3.11 -m pytest python-spawner/tests -q and
py -3.11 python-spawner/tools/check_images.py. The root untracked newprompts/ and
reference/ folders are unrelated and must remain untouched.

--- Earlier checkpoint follows ---
# Completed fixes 2-4, 2026-11-13
Fix 2: f1eedc7. Match chunk raster dimensions to CSS display size on every zoom;
minimum strokes survive SVG image rasterization. Gravel drops above 48 mi.
Fix 3: c3f24bf. Straight fixed-offset rail; no per-mile sideways excursion.
Fix 4: lane-specific continuous phase/amplitude variation in global coordinates;
terrain follows those valleys. Tributaries share the trunk sampling lattice,
join at staggered stations with a vanishing lateral slope, and keep exact
canonical downstream endpoints. Cache version lod-v13-varied-rivers.

Validation: full pytest 97 passed in 31.85s. Five regenerated parity pairs
byte-identical. Full geometric verifier passed seeds 13,14,30,31,46.
Real Chromium regressionZoomPanCache and regressionDistantChunks passed (both
seeds, all ten distant locations). regressionStrokePixels measured water 1.498px,
rail and paved road 1.2px at 24/80/256px chunk sizes.
Fresh server, seed 14: Ready and decoded at 3/30/48/96 mi and x=90 mi.
96 mi: 1024 visible decoded chunks, zero transparent chunks.
Screenshots: artifacts/fix2-4-3mi.png, fix2-4-30mi.png, fix2-4-48mi.png,
fix2-4-96mi.png, fix2-4-90mi-away.png; artifacts/fix3-rail-crossing.png
uses rail_enabled_pct=100 to expose the straight offset crossing.
No user acceptance is inferred. Cold overview generation latency remains.
Push only python-test/spawner. No merge, force push, history rewrite or PR.
No further implementation steps pending; restart the server to load this version.

--- Previous checkpoint and history follow ---

# Python spawner checkpoint

## Previous handoff, 2026-11-13: fix 1 complete; fixes 2-4 pending
Started at 01b2368 on python-test/spawner. Read this section before historical
status below, which describes earlier work and does NOT establish acceptance of
the latest four reported issues.

Fix 1 root cause: chunk SVG geometry used absolute internal coordinates
(1 mile = 1,013,760 units). Chromium/Skia silently renders distant SVGs transparent
even though image.decode() succeeds and HTTP returns full geometry. A 90-mile
view reproduced Ready + blank decoded SVG. Rebase every chunk's actual geometry
to a local origin before serialization (not a cancelling SVG transform). Chunk
cache version is lod-v11-local. Browser town parsing restores absolute positions.
No rural lot, town, tree, or legacy parity renderer behavior changed.

Inspected real Windows Chromium page console/network: /chunks HTTP 200, no console
errors; request bodies a few KB, far below controls.max_request_bytes=2 MB.
Known IDs and image keys agree, visible images survive trimChunkImages, and server
JOBS protects visible entries even beyond tile_cache_size=128. Those were not the
cause of the distant transparent geometry. Cold 96 mi does take several minutes
with the single worker; it eventually reached Ready with all 1024 visible images
decoded and zero transparent chunks (pixel checks). This latency is not fixed.
Real page regressionDistantChunks() passed seeds 14 and 2113558218 at (+60,0),
(-60,0),(0,+60),(0,-60),(198,198) miles. Ready 3/30/48/96 mi views checked.
Screenshots in artifacts/: fix1-3mi.png, fix1-30mi.png, fix1-48mi.png,
fix1-96mi.png, fix1-90mi-away.png. These show fix 1, not final LOD/rail/river work.
Full pytest: 96 passed in 33.17s. Added far SVG local-coordinate regression for
both seeds at -198,-90,40,90,198 mi. tools/check_images.py regenerated all five
parity pairs, all byte-identical; no parity artifact diff.

## Previous next step (completed below)
Implement fix 2 (zoom-based line widths) BEFORE fix 3 (straight offset rail,
square road crossings, remove sideways easing bulges) and fix 4 (nonrepeating
continuous meanders, downhill joins, no fan seams). Those are NOT completed.
Read only these files as needed on resume:
- control.html: imageKey/lod/paintChunks/redrawChunks and encoded SVG image cache.
- chunks.py: generate/request/identity and local SVG serialization.
- world.py: svg(), rail_lanes/rail_points, drainage_* / rivers / elevation.
- tests/test_chunks.py, tests/test_world.py, tests/test_control.py.
- verification.py: crossing_angles and river_checks.
- tools/browser_flow.js: browserFlow, regressionZoomPanCache, regressionDistantChunks.
- tools/check_images.py: five parity regeneration command.
- control.py and cli.py: loopback server and request limits.
Run py -3.11 -m pytest python-spawner/tests -q and
py -3.11 python-spawner/tools/check_images.py. Start a fresh server after Python
changes: py -3.11 python-spawner/cli.py --serve --port 8001. A server from this
session may still be listening on 8001; restart it to reload code.
Potential fix 2 approach: SVG image intrinsic raster size vs CSS chunk size must
match, otherwise even vector-effect strokes shrink after the SVG is rasterized.
Consider screen-size-aware serialization/paint, with LOD-aware image identity.
At 96 mi gravel should drop before paved roads/rail/rivers/towns. Add meaningful
pixel-width tests and replace screenshots after final changes.
Commit + push each completed fix only to python-test/spawner, update this file.
No merge/force push/history rewrite/PR. Unrelated untracked root files untouched.

## Historical checkpoint (superseded where noted above)

## Status
Navigation fix: reproduced the zoom-out / pan / zoom-in blank map in the real
control page. After a large overview/long session, FIFO browser-cache trimming
could evict the current detail SVG AFTER advertising it to /chunks as known.
The server correctly omitted its SVG; the page incorrectly settled with a hidden
tile. Visible image keys are now protected during trimming; stale revisions cannot
start requests. The same populated-cache sequence failed before (Ready, hidden
image, missing current cache key) and passes after, with actual HTTP and SVG decode.
Regression: tools/browser_flow.js regressionZoomPanCache(), plus an automated Node
check of the shipped cache routine in pytest. New-area negative-coordinate flow
also passes. Full suite: 95 passed. Node is needed for the browser-script unit test.

Find a town tested in the real page at defaults: found two nearby towns, visited
at 0.50 mi detail, with next/previous enabled. Seed 31, 36x36 mi sample: five towns,
nearest-neighbor distances 9.00,9.00,9.03,11.95,17.78 mi. Spacing regression added;
town layouts and probabilities unchanged. All five regenerated parity PNG pairs
(seeds 13,14,30,31,46) remain byte-identical. Geometric verifier passes all five
seeds, including actual paved geometry, drainage, rail angles and overlaps.
Rural layout code was left alone. No merge, force push or PR.

River fix: canonical endless trunks now have four times as many bends and four
times the samples. Smooth longitudinal compression produces tight oxbow-like
hairpins while preserving strict downstream order (no closed/self-crossing loops).
Tributaries span 85-94% of the default eight-mile interval and merge into wider
trunks. Existing amplitude/period controls set bend size. Floodplain exclusion now
includes the configured creek buffer; roads remain unbroken bridge decks above
water. Fifteen world/verification tests pass, including 40-mile continuity, downhill
and crossing checks for seeds 13,31,46, plus multi-chunk length/neck regressions.

Rail fix: replaced five-point sawtooth deviations with sampled C2 eased curves,
with straight square approaches at every section crossing. Regression checks
heading changes, exact crossing tangents and shared endpoints; 10 world tests pass.

Road fix: paved corridors are selected before missing-edge sampling. Only gravel
and Level B roads can be missing. Overview road strokes have a 0.9 px minimum
(previously 0.3 px), preserving true road width at detail. Added actual paved
geometry gap verification and deletion regression; 13 targeted tests pass.

Fix 1 reworked after the broken 961bf7d browser test. Always endless world;
Find a town / next / previous with adjustable town zoom. Drag and cursor-anchored
wheel zoom are on the visible chunk surface (previously on the hidden PNG).
Default chunks are 3x3 mi, configurable 1-12 mi. Overview skips house plans,
buildings, trees, lots and contour sampling; keeps roads, rivers, town outlines
and farm dots. Full detail is selected at <=2 mi wide. Separate versioned disk
and browser caches prevent LOD mixing. Requests omit SVGs already held by the
browser; existing image nodes survive pans. Visible work outranks halo and
pan-direction prefetch; obsolete queued jobs are discarded. Coordinates have
no geographic bounds; the supported view width is 0.01-96 mi (was capped at 12).
Fix 2 completed: grouped controls, readable names, units and tooltips; known
tick lengths display/edit in feet while presets keep canonical values.
82 tests pass. Seeds 13,14,30,31,46 parity PNG pairs remain byte-identical.

Browser measurements, loopback HTTP + actual control.html JS, 1280x800:
- Before: cold 10x10 mi, seed 896131: 9085 ms, 31 polls, final payload 10,582,099 bytes.
- Before: cached pan JS redraw 12.5 ms; cached request + paint 1153.2 ms.
- Before: zoom to 20 mi impossible (12 mi cap); drag/wheel had no handlers on tiles.
- After: cold 10x10 mi, fresh seed 896135: 958.7 ms including debounce,
  polling, SVG installation and image decode.
- After: cached pan 2.0 ms; cached zoom to 20 mi 3.7 ms, including image decode.
  These measure immediate visible redraw; background debounce/HTTP is separate.
  Final known-chunk response at 20 mi: 2652 bytes including transfer headers.
- cProfile: before 1x1 mi full window 3.731 s / 11.04M calls, _tile 3.580 s,
  spawn_region 2.195 s. After 3x3 mi overview 0.497 s / 1.86M calls,
  no spawn_region calls. Profiles intentionally compare the old smallest display
  chunk with the new larger overview chunk. House plans and redundant tile halos
  were the main CPU cost; resending SVG and replacing nodes added browser work.

Fix 8 done: CLI region exports use explicit axes placement, so off-map parcel
labels cannot shrink the drawing through tight_layout. --world --png writes a
framed raster of the requested rectangle, with automatic overview LOD. The old
reference PNG renderer stays available for byte-identical parity comparisons.
Both exports visually inspected; CLI tests check framing and rectangular size.
Legacy independent creek lines removed from world output: only the verified
watershed network is drawn. River meander period is now in full wavelength units.
Final profile found water/lot collision scanning as the new bottleneck: candidate
streams are bounded to each settlement tile and each parcel before segment tests.
Fix 2 clarified further: map effects in labels, explicit percent units, one-line
tooltips, extended reference notes behind About this setting, leaf labels include
parent setting names. Wheel zoom is continuous and anchored at the cursor.
Final browser seed 896137, before the last water broad-phase optimization:
10x10 mi cold 2272.5 ms; cached pan 0.9 ms, cached 20 mi zoom 2.3 ms (decode).
Two-frame browser checks: cached pan 4.8 ms, cached zoom 46.3 ms.

Final validation:
- 89 tests passed (including real CLI PNG export framing).
- Geometric verifier passed seeds 13,14,30,31,46 over 3x3 mi windows.
- Final optimized 3x3 mi overview cProfile: 0.464 s, 1.60M calls,
  versus 1.440 s / 5.81M before broad-phase water rejection. No house plans.
- Final browser fresh seed 896139: cold 10x10 mi 907.7 ms; cached pan
  1.1 ms, cached 20 mi zoom 2.8 ms (HTTP/page logic + image decoding).
- A second browser run found an all-water-blocked town in prefetched tile
  (7,-1), seed 896138. Empty block lists now mean no town instead of raising
  min(empty) and poisoning subsequent requests. Regression test added; the same
  browser flow now completes (552.4 ms with its existing disk cache).
- Main PNG export and world PNG export visually checked; legacy creek strokes
  that crossed the canonical drainage network are no longer drawn in the world.

- Latest complete browser flow, fresh seed 896140: cold 10x10 mi 830.4 ms;
  cached pan 1.1 ms; first zoom out to 20 mi (new visible chunks) 749.9 ms;
  cached zoom to 20 mi 2.0 ms. All include image decode; first views include HTTP
  polling and debounce. Reproduce using tools/browser_flow.js in the real page.
- Final five parity before/after pairs are byte-identical after the empty-town fix.
- Find town reaches full detail, next/previous enable after selection, and small
  wheel deltas zoom continuously (0.5 mi -> 0.4950 mi for a -5 wheel delta).

## Historical next step
Latest requested river, road, rail and navigation fixes are complete. User can
test the pushed python-test/spawner head with
`py -3.11 python-spawner/cli.py --serve` (restart an older server to load changes).
Run `py -3.11 -m pytest python-spawner/tests -q` for all 95 tests. For the real-page
cache regression, evaluate tools/browser_flow.js in the page then await
regressionZoomPanCache(). No merge, force push or PR. Unrelated untracked root
folders remain untouched. No new user acceptance is claimed.

## Fix 2, 2026-11-13
Chunk SVGs now rasterize at their displayed CSS size, with screen-space minima for roads, rail, water and town outlines. Gravel drops above 48 mi. Raw SVG cache identity stays reusable; each image rerasterizes on size changes. Full pytest: 96 passed; parity regeneration running.

## Fix 3, 2026-11-13
Removed per-mile rail excursions. Canonical straight offset corridors have square crossings and exact shared endpoints. Updated the old diagonal expectation to require straight geometry. Parity: five byte-identical pairs.
