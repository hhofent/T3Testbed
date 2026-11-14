# PORT-CHECKPOINT (tier 1 free model)

**Resume instructions:** Fetch `port/builder-guy-towns`, read PORT-CHECKPOINT.md, STATE.md, T3-DECISIONS.md and SETTINGS-CLASHES.md, run the rebuild/test commands, confirm the numbers match the checkpoint, then continue at "Exact next step".

**Date/time:** 2026-11-13 (resumed after context compaction)  
**Last commit:** 35647a8 [tier1] T3: remove Boyden/Hull everywhere  
**Branch:** port/builder-guy-towns  
**History:** b13c150 (T1 baseline, generic country names), 19c57a5 (T2 python reference + decision files at root), 35647a8 (T3 boyden/hull removal)

## Milestones done / in progress / next
- Done: T1 (M0) b13c150; T2 19c57a5; T3 35647a8.
  - T3 verified: build 146 warnings/0 errors = baseline; verify 13/13 pass; `rg boyden|hull src README.md STATE.md` clean; `--size boyden` fails listing valid sizes; `/gen?size=hull` → 400 with valid list; country renders byte-identical to port-report/baseline-hashes.txt under new village/town names.
- Done: T4 Wire unconnected settings (M1 part 1) — wiring completed and committed-in-progress:
  - SmallTown.cs: removed hard-coded consts (HighwayWidth 66, StreetRow 36, AlleyRow 16, BlockLotDepth 122, SideLotDepth 80, BandDepth 175); all reads now via `cfg.` on SmallTownConfig (defaults identical to old consts → output byte-identical to HEAD). Removed fresh `new CommercialConfig()` (not consumed — logged as unwired). Generate() now accepts `TownTierSystem? townTiers` and uses `townTiers ?? new TownTierSystem()` instead of always `new TownTierSystem()`.
  - Program.cs: both smalltown Generate callsites pass `config.TownTiers`.
  - Verified: T4 output == HEAD output exactly (clean build, crossroads seed 1993 = 10eb5b9b both before/after T4) → zero regression.
  - Not yet written: port-report/settings-audit.md (still pending).
- In progress: none.
- Next: T5 BuilderGuy settings schema; T6 presets/overrides/sweep (start at T6: `--param path=value` overrides in Program.cs); T7 /params control page; T8 scaffolding.

## Exact next step
T6: add repeatable `--param path=value` overrides (applied after presets, validation errors for bad paths/out-of-range) in Program.cs; then `--selftest` override-parsing coverage. Commit after T6, update checkpoint.

## Parity numbers so far
None (tier 1 scaffolding only; Python reference frozen under reference/python-spawner, 61 tests pass per T2).

## Verify ladder status
- Baseline: 13/13 pass at seed 1993. Re-confirmed on clean build: all checks passed (`all checks passed`).
- Build: `dotnet build SpawnGuide.csproj -c Release` → 72 warnings, 0 errors (baseline recorded 146 warnings; fewer now, no new warnings, 0 errors).

## PRE-EXISTING baseline-hash mismatch (NOT caused by T4; document only, do not fix)
- Clean-build default renders: country + world match port-report/baseline-hashes.txt exactly (all 6). All 4 smalltown sizes DIFFER (e.g. crossroads clean build = 10eb5b9b..., baseline-hashes.txt says 8fd7862f...).
- Verified at HEAD (35647a8, before T4): clean build gives the same smalltown hashes as T4 → mismatch predates T4 work. T4 changes no generation values (cfg defaults == old consts), so T4 cannot have caused it.
- Likely cause: baseline hashes were recorded under a stale incremental build state (incremental builds have reproduced 8fd7862f; clean builds give 10eb5b9b). Do NOT touch baseline-hashes.txt or generation logic — out of scope for T4/T6. Record here; tier 2 can decide whether to regenerate baselines.

## Blocked on user
None.

## Decisions applied
U12/U29: Boyden/Hull/George removed from src, README.md, STATE.md, docs/proposal-infinite-map.md (docs/research + docs/archive untouched).
Config/default mismatches logged, not fixed (per T4 rule): SmallTown street stays 36 ft (StreetConfig default 66 ft — UC10); garage chances stay 45/45/10 (SmallTownConfig 55/35/10 appears elsewhere — UC9). CommercialConfig unwired (not consumed by any generator).

## Known bugs
- See PRE-EXISTING baseline-hash mismatch above (smalltown sizes don't match baseline-hashes.txt on clean build; matches were seen only under stale incremental builds).
- Leftover uncommitted partial work from the previous session was invalid C# (`const float cfg.HighwayWidth = 66f;` etc. in SmallTown.cs lines 24-29) — fixed as part of T4; build now passes.

## Commands
- Build: `cd src/SpawnGuide && dotnet build SpawnGuide.csproj -c Release` (expect 0 errors; warnings currently 72 after clean build)
- Verify: `dotnet run -c Release -- --verify --seed 1993` (13/13 pass)
- Baseline hash check: render 4 smalltown sizes + country + world at seed 1993 (each `--map smalltown --size <sz> --seed 1993 --out <dir>` then rename smalltown-1993.svg to smalltown-<sz>-1993.svg; `--map country`; `--map world`) and sha256-compare to port-report/baseline-hashes.txt. Country/world match; smalltown sizes differ — see PRE-EXISTING issue above (do not "fix").
- Python: `cd reference/python-spawner && .venv/bin/python -m pytest -q tests` (61 passed); `export_reference.py` regenerates reference_out.

## Takeover baseline milestone (2026-11-13)
- Actual takeover HEAD: 7286652; branch port/builder-guy-towns. Initial status: only untracked user prompt packs (T3-port-prompts.zip, T3-port-prompts/, newprompts/).
- Read shared spec from newprompts/CODING-AGENT-PROMPT.md (no root copy), root decisions/clashes, and tier prompts for scope/stop rules.
- Clean Release: dotnet clean then dotnet build src/SpawnGuide/SpawnGuide.csproj -c Release: 72 warnings, 0 errors. Verify seed 1993: 13/13 pass.
- Initial --selftest is NOT implemented: it silently falls through to gallery generation and exits 0. Do not treat this as a test pass.
- User approved re-baselining smalltown hashes from clean 35647a8 because the old baseline was reported as recorded from a stale incremental build. Rebuilt a git archive of 35647a8 in an isolated temporary directory (no branch checkout/history changes).
- Correction to the previous diagnosis: clean 35647a8 from repo root reproduces 10eb5b9b...; from src/SpawnGuide it reproduces the old 8fd7862f... . Working-directory config auto-discovery explains both hashes without stale binaries: src/SpawnGuide loads spawnconfig.json; repo root uses code defaults. The new baseline explicitly uses REPO ROOT, no --config, seed 1993. All four smalltown hashes updated; all six country/world hashes unchanged. Current recovered code matches the four new smalltown hashes.
- No generation logic or decision changed. Exact next step: finish strict repeatable --param overrides and their selftests, then commit T6 override subtask; complete the rest of T6 after T5 schema.
