# PORT-CHECKPOINT

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
