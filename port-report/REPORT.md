# T3 port report — partial

1. **Status:** partial. Branch `port/builder-guy-towns`. Last implementation commit `de66546`; report/checkpoint commit follows. Implementation commits 921b51f, 4f2d4ef and de66546 were pushed only to that branch. No merge, force push, history rewrite or PR. Tier 1 is unfinished; tier 2 is unstarted.

2. **Parity:** not implemented or measured. No layout exact-match results, distribution table, house parity or side-by-side comparisons exist yet. No tolerances changed.

3. **Verify ladder:** all 13 existing checks pass at seed 1993: identical windows, distant reproducibility, overlapping-window consistency, cell consistency, town spacing, rail proximity, building overlaps, river flow, square crossings, branch-spur/elevator placement, spur clearance, paved town sites and street exits. Legacy checks remain unchanged; required N4/N5/N6/N8 rewrites and additional checks remain outstanding. Evidence: takeover-verify.txt. These legacy passes do not establish tier 2 acceptance.

4. **Controls:** `/params` has not been implemented. Existing `--serve` is available via `dotnet run --project src/SpawnGuide -- --serve`, at http://127.0.0.1:8735/; the parameter page is still missing. Stage switches, complete setting audit/count, BuilderGuy presets and redraw timings remain outstanding. Repeatable `--param path=value` and stacked `--preset`/`--profile` work. Eight existing profiles apply and JSON round-trip. See profile-repairs.md for exact repairs. Baseline hashes were refreshed from clean 35647a8 with explicit repository-root/default-config provenance; working-directory config discovery explains the old mismatch.

5. **Clash decisions:** root decision files unchanged. Generic names retained; U16 zoom capped at 300. No park code added (N13). Full clash merge and N1-N13 world implementation remain outstanding; there is no question blocked on the user.

6. **Tests:** 25 C# selftests pass; 61 frozen Python tests pass. Initial clean build: 72 warnings/0 errors; latest build: 69 warnings/0 errors. All four default smalltown hashes match the refreshed baseline. No house-rule mutation coverage or full tier1 acceptance claim. Evidence: takeover-build.txt and takeover-selftests.txt.

7. **Images and sweep:** no side-by-side artifacts or sample sweep CSV yet.

8. **Open work:** finish T4 settings audit and T5 schema/migration/settings-map; remaining T6 presets/metrics/sweeps; T7 controls; T8 scaffolding and acceptance; HANDOFF and tier1-complete commit; then every tier2 step. PORT-CHECKPOINT.md contains the exact next steps and commands. Do not treat this partial report as completion.
