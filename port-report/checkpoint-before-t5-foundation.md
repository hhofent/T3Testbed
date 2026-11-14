# PORT-CHECKPOINT

## Current resume point ? supersedes historical audit instructions below

2026-11-13: User replaced manual T4 source-read audit/wiring with automated settings probe. T4 is now complete under that approved scope. Do not resume historical source-read wiring tasks. No DEAD settings were wired or removed.

- Added --probe-settings using loaded SpawnConfig reflection, numeric/bool leaves including distributions, legacy range endpoints, dictionaries and arrays; independent clones, both-direction nudges, bool flips, finite float limits, declared Range clamping. Runs root/no --config on seeds 1993/7/42 across crossroads/hamlet/village/town, country, world origin. SHA-256 SVG comparisons.
- Reports: port-report/settings-probe.csv/.md; port-report/dead-settings.md. Counts: 2755 DEAD, 5 ERROR, 194 LIVE. ERROR takes precedence and retains successful effects. DEAD is sampled no-change, not source-level proof. Viewer-only and house-only settings can be DEAD in these requested map modes.
- Execution evidence includes full run and supplemental legacy endpoint/finite-limit runs merged by exact path. Final default tool directly includes all leaves. Optional prefix/output flags support focused reproduction.
- Release build: 69 warnings, zero errors. Selftests 29/29; verify 13/13. Default hash selftests remain green. Python logic untouched. No tolerances/checks changed. Root decision files untouched.
- Exact next step: T5 clean BuilderGuy settings schema for Python constants/placeholders (no parks), each with range/units/note/source, migration and settings-map. User says BuilderGuy settings need no probing. Then remaining T6; T7 only LIVE T3 settings plus BuilderGuy, grouped by stage with sliders/min-max pairs/stage switches; T8; HANDOFF and [tier1] complete, then tier 2. Earlier T6 overrides/profile repairs remain completed; do not redo them.
- T5-T8/HANDOFF/tier1 complete and tier2 remain unfinished. No user blocker. This is a working-context budget checkpoint, not a claim of overall completion.


**Resume instructions:** Fetch `port/builder-guy-towns`, read PORT-CHECKPOINT.md, STATE.md, T3-DECISIONS.md and SETTINGS-CLASHES.md, run the rebuild/test commands, confirm the numbers match the checkpoint, then continue at 'Exact next step'.

Date/time: 2026-11-13 20:03 America/Chicago
Last implementation commit before audit: de66546; resumed from 994c539 (audit/checkpoint commit follows)
Branch: port/builder-guy-towns, origin https://github.com/hhofent/T3Testbed.git

## Status
PARTIAL. Tier 1 is NOT complete. Tier 2 is NOT started. No HANDOFF.md or `[tier1] complete` commit has been produced.

Completed before takeover: T1 b13c150, T2 19c57a5, T3 35647a8; recovered partial T4 7286652. The earlier checkpoint overstated confidence in T4's baseline diagnosis; the audit and other unwired sections remain unfinished.

Completed this session:
- 921b51f: clean takeover baseline, approved four smalltown hash updates, cost log.
- 4f2d4ef: T6 override subtask: repeatable --param path=value after stacked presets/profiles, strict path/type/nonfinite/range errors, exit 2 for input errors, actual --selftest runner.
- de66546: repair eight existing profiles, preserve immutable range/distribution overrides, profile JSON round-trip, complete binary cache, preserve inactive distribution parameters. See port-report/profile-repairs.md.

Remaining: T4 full settings audit/wiring evidence; T5 full BuilderGuy schema, Source attribute, schema migration and settings-map; T6 reference/default presets and pluggable sweep metrics; T7 /params control page; T8 data contracts, plumbing, parity/side-by-side/pending verify scaffolding and remaining selftests. Then HANDOFF and tier1-complete commit. All S1-S5/M2-M4/M7/M8 tier 2 work remains.

## Exact next step
1. Read `newprompts/CODING-AGENT-PROMPT.md`, root SETTINGS-CLASHES.md/T3-DECISIONS.md, and tier prompt scope/stop rules. There is no root CODING-AGENT-PROMPT.md; user prompt packs are untracked and untouched.
2. Continue T4 using the new port-report/settings-audit.md and SettingsAudit.cs / --audit-settings. Inventory: 528 property settings; 90 mutations changed SVG output, 415 did not at seed 1993, one rejected probe (zoom=301), 22 structural/unprobed. Review NO CHANGE rows against actual generation reads; broaden seeds and use dedicated mutations for collections, metadata and rare features. This is NOT completed T4 wiring. T4 still only wires six SmallTown constants and TownTiers. Residential/Streets/Commercial and density effects must still be assessed. Re-run audit after each wiring change; keep defaults byte-identical. Do not interpret sampled no-change as proof a setting is unused.
3. Implement T5 under src/SpawnGuide/BuilderGuy: every named/inline Python constant and PLACEHOLDER in region.py/spawner.py/validate.py (excluding parks), with typed settings and attributes, settings-map, schema bump/migration. Read source in small ranges. Use existing Windows Python venv below.
4. Finish remaining T6, T7, T8 in separate commits, then complete tier1 acceptance/HANDOFF before tier 2. Do not mark pending work complete.

## Baseline and rebaseline provenance
Initial takeover status: only untracked T3-port-prompts.zip, T3-port-prompts/, newprompts/. Actual starting HEAD was 7286652, not 35647a8 as the old checkpoint stated. git log --oneline -15 was inspected.

User approved clean-35647a8 smalltown rebaseline because the old baseline had been reported as recorded from a stale incremental build. This session rebuilt a git archive of 35647a8 in an isolated temporary directory, without changing branch/history.

**Correction to old diagnosis:** clean 35647a8 from repository root gives 10eb5b9b...; clean 35647a8 from src/SpawnGuide gives old 8fd7862f.... Working-directory configuration discovery explains both: src/SpawnGuide loads spawnconfig.json; root uses code defaults. No stale binary is required to reproduce the difference. New port-report/baseline-hashes.txt explicitly represents REPOSITORY ROOT, no --config, seed 1993. Four smalltown entries updated; six country/world entries unchanged. Current implementation still matches all four smalltown entries. No generation logic was changed.

The initial --selftest invocation silently generated a gallery and returned 0: the option had no handler. That was not a passing selftest result. It now runs genuine tests.

## Verification
- .NET SDK 5.0.408, Windows, .NET 5 / C# 9, no new packages.
- Initial clean Release build: 72 warnings, 0 errors. Latest build: 69 warnings, 0 errors (old nullable warnings removed with obsolete setter/cache code).
- Verify seed 1993: 13/13 PASS, unchanged legacy logic. These are NOT evidence of N1-N13/tier2 compliance.
- Selftests: 25 PASS, 0 FAIL: overrides, precedence, culture, invalid inputs, immutable bounds, eight profile JSON round-trips, imported profile application, inactive distribution fields and complete binary cache.
- Python: 61 passed in 2.07s using existing .venv/Scripts/python.exe. Frozen Python logic untouched. Export reproducibility not rerun this session.
- git diff --check: clean.
- Evidence: port-report/takeover-build.txt, takeover-verify.txt, takeover-selftests.txt.
- StreetRow=42 changes crossroads hash to abe93ebc45428f81875147ca588d7dc7d00593487cffdbf16783727ff7e28a84; default remains 10eb5b9b71e579c0a69023ac85a8401d6f01296c98ced6b9337ad5ef0cf09f42.

## Parity
Not implemented or measured. No exact-match, distribution or house-parity claims. No side-by-side artifacts or sample sweep yet.

## Decisions applied
U12/U29 generic names retained; U16 max zoom cap 300 enforced in Render setting and testing-max preset. N13: no park code/settings added. Root decision files unchanged; blank value rows must use Python values as settings per user. Full clash merge and N1-N13 implementation remain tier2 work.

## Known issues and scope limits
- Existing profiles repaired as documented in port-report/profile-repairs.md. A profile loading/round-tripping does not prove every setting drives output.
- Only two legacy scalar bounds added so far: Rural.ShopChance [0,1], Render.ViewerMaxZoom [1,300]. Full Range/Units/Rationale coverage is still absent. Overrides enforce declared ranges and existing validators; they cannot enforce undocumented bounds. This is unfinished audit/schema work.
- Noise/world and other config sections previously disappeared on binary cache loads; complete-cache format now rebuilds old partial caches from JSON. Historical cached results may therefore differ where custom newer sections were previously discarded.
- Existing --test-profiles handler validates/repairs configs; actual round-trip checks are in --selftest.
- Full layout/house port, updated verify ladder, controls, timings, and broad rule mutation tests remain absent.

## Blocked on user
None. No tolerance/check was loosened or removed. No decisions were invented to establish parity. Work is incomplete, not externally blocked.

## Commands (run at repository root)
```powershell
dotnet clean src/SpawnGuide/SpawnGuide.csproj -c Release
dotnet build src/SpawnGuide/SpawnGuide.csproj -c Release
dotnet src/SpawnGuide/bin/Release/net5.0/SpawnGuide.dll --verify --seed 1993
dotnet src/SpawnGuide/bin/Release/net5.0/SpawnGuide.dll --selftest
reference/python-spawner/.venv/Scripts/python.exe -m pytest -q reference/python-spawner/tests
dotnet src/SpawnGuide/bin/Release/net5.0/SpawnGuide.dll --map smalltown --size crossroads --seed 1993 --out src/SpawnGuide/out/check
```
Repeat last command with hamlet/village/town, renaming smalltown-1993.svg between runs when checking baseline hashes. No --serve process was started this session. Push only port/builder-guy-towns; no merge, force push, history rewrite or PR.

## Resumed audit increment (2026-11-13 20:03)
- Fetched origin/port/builder-guy-towns; resumed from 994c539, no tracked changes at start. Required specs/decisions and tier scope/stop rules re-read. No decision files modified.
- Clean build re-confirmed: 69 warnings / 0 errors. Existing selftests 25/25 and verify 13/13 pass, also after adding diagnostic audit.
- Added --audit-settings [--audit-output path], default port-report/settings-audit.md. Uses independently cloned configs, strict override application and validation, compares eleven generated SVG views at seed 1993. Reports declaration locations and exact mutation/rejection per row. Does not change normal generation, tolerances or checks.
- Audit first pass: 528 property settings; changed 90; unchanged 415; rejected 1; structural/unprobed 22. Collection contents are represented by their owning property, not recursively proven. Before-state wiring remains unverified. Named bounds/distribution fields are probed as containing settings, not each constituent. Additional source review, metadata coverage and targeted tests are still needed before T4 completion.
- The diagnostic initially used a wrong countryside class name (fixed before successful build) and encountered an overflowing extreme distribution mutation (clamped to finite float max in the probe, without changing any generation values or validation tolerance). Completed audit exits 0; unchanged rows are findings, not passing acceptance checks.
- Context-limit checkpoint: stopping with T4 in progress. Exact next action is step 2 above, then T5 onward. No external blocker or user decision needed.

## Resumed commercial wiring increment (2026-11-13)
- Re-read newprompts shared spec, both tier prompts, STATE and decision files; fetched the authorized branch. User prompt packs remain untracked and untouched.
- Rebuilt baseline: 69 warnings, zero errors; verify 13/13, selftests 25/25, Python 61/61.
- Wired Commercial.ElevatorWidthFt, ElevatorDepthFt, ElevatorRightChance, BinSpacingFt, CrossStreetShopMax into smalltown CLI and /gen, plus the audit. Preserve legacy SmallTown aliases when the corresponding Commercial value equals its default; a non-default Commercial value wins. See port-report/commercial-wiring.md for limitations and generation-read table.
- Removed the dead CommercialConfig local. CrossStreetShopMax=0 now disables those shops without calling RangeInt(0,0), which previously divided by zero.
- Added meaningful checks: four default town SHA-256 hashes, five mutations across four seeds, legacy alias compatibility and explicit Commercial precedence. Latest selftests 28/28, verify 13/13, build 69 warnings / zero errors. Default hashes match baseline. Python logic untouched.
- Reran settings audit: 528 properties, 94 sampled output changes, 411 unchanged, one rejected probe, 22 structural/unprobed. RightChance's modest mutation can leave sampled output unchanged; dedicated extreme mutation passes. No-change is still not proof of unused settings.
- Evidence: port-report/commercial-{build,selftests,verify,audit}.txt; resume-{clean,build,selftests,verify}.txt.
- T4 remains PARTIAL. T5-T8, HANDOFF, tier1 completion, and all tier2 remain unfinished. No outside blocker. Stopping for this run's context/usage budget; do not mark a milestone complete.
- Next action: continue Exact next step 2. Read commercial-wiring.md first. Root Commercial still does not flow into nested countryside/world towns; resolve that plumbing, finish remaining Commercial/Residential/Streets/density source-read inventory and wiring without baseline changes. Then T5 onward in the existing prescribed order. Do not replace this with just another sampled audit.

## Resumed nested town plumbing increment (2026-11-13)
- Completed the checkpoint's next concrete plumbing task: countryside/world towns receive root SmallTown, TownTiers and Commercial. CLI, HTTP world, cell, and audit call sites now supply loaded config. GenerateCell forwards it. Existing calls without the new optional argument retain legacy behavior. No global state or cache added.
- Default nested output equality and commercial overrides tested for seeds 13/31/1993; four smalltown golden hashes still pass. Build: 69 warnings, zero errors. Selftests: 29/29. Verify: 13/13. Evidence: port-report/nested-{build,selftests,verify,audit}.txt. Python logic untouched; Python tests not rerun in this increment.
- Audit rerun: 528 settings; 96 sampled changes; 409 unchanged; one rejected mutation; 22 structural/unprobed. Counts do not establish full wiring.
- T4 remains PARTIAL; T5-T8, HANDOFF, tier1 complete and tier2 remain unfinished. No external blocker. Context-budget handoff, not milestone completion.
- Exact next task: finish source-read inventory and wiring for remaining Commercial/Residential/Streets/density. `rg -n 'StreetRow|AlleyRow|PerimeterGap|Jog|Stub|Bulb|FourSide|Curve|Erase' src/SpawnGuide/SmallTown.cs src/SpawnGuide/SpawnConfig.cs` finds existing aliases; StreetConfig default 66 differs from legacy 36 and must preserve baseline per T4. `rg -n 'ResidentialConfig|StreetConfig|DensityMultiplier' src/SpawnGuide --glob '*.cs'` confirms these sections currently lack generation consumers. Do not replace this work with another sampled audit. New nested config plumbing is done; do not repeat it. Do not mark T4 complete until its source-read inventory and required wiring evidence are finished.

## T4 grouped-report milestone (2026-11-13)
- Finished the approved automated-probe T4 scope: dead-settings.md groups distribution children, excludes every parent with LIVE evidence, and separates ERROR parents. 512 DEAD entries, 135 LIVE entries excluded, 4 ERROR parents excluded. Raw counts remain 194 LIVE / 2755 DEAD / 5 ERROR.
- Explained all five errors: independent endpoint mutations violate Min <= Max; expected validator rejection, not demonstrated generation bugs. No DEAD settings wired or removed.
- Added port-report/group-settings.py to regenerate this decision report from the existing CSV without probing. Run with reference/python-spawner/.venv/Scripts/python.exe. Regenerate after future --probe-settings runs (the raw probe still writes its original leaf report).
- Verified raw count/status totals, grouped exclusions and git diff --check. Reporting-only change; no generation code or tolerances changed.
- Exact next step: T5 as approved: inventory constants and inline placeholders from reference/python-spawner region.py/spawner.py/validate.py excluding parks; add typed BuilderGuy settings with Range/Units/Rationale/Category/Source, schema migration, settings-map; commit and push. Then T6, T7, T8, HANDOFF, tier2 in order. Do not resume historical manual T4 instructions.
