# Port checkpoint

## Status
PARTIAL. Tier 1 is not complete; tier 2 is not started. No HANDOFF.md or `[tier1] complete` commit.
Branch: port/builder-guy-towns. Push only here; no PR, merge, force-push or rewrite.
T1-T4 completed per user: automated settings probe 295083d, grouped dead settings cbe060b. T6 overrides and stacked presets already implemented.
T5 foundation increment: BuilderGuy section, restricted Source attribute, schema 3 and v2 migration preserving existing values; initial house, setback, commercial and region settings. T5 remains incomplete. Generation is untouched; no probing of Python settings.
Validation: Release build 0 errors / 69 warnings; 31 selftests pass; legacy seed-1993 verify passes (not evidence of tier 2 compliance). Evidence: port-report/t5-foundation-{build,selftests,verify}.txt.
Settings map and explicit remaining inventory: port-report/settings-map.md. Previous checkpoint/history: port-report/checkpoint-before-t5-foundation.md.
Stopping at a context checkpoint, not claiming a completed milestone.

## Exact next step
1. Finish T5 before proceeding to T6. Read only needed Python sections using rg/small ranges. Add typed region weighted/choice tables and dimensions, house Census/choice tables and inline constants, and validator thresholds; every setting needs Range/Units/Rationale/Category/Source. No parks. Complete exhaustive settings-map; preserve Python values (blank clash rows use ours). Add migration file/cache and frozen-value checks. Commit and push T5 completion.
2. Finish T6 reference/default presets (including builder-guy-reference) and sweep with pluggable metrics; commit/push.
3. T7 /params uses LIVE settings from port-report/settings-probe.csv plus BuilderGuy, stage grouping/toggles, sliders/pairs, seed random/lock, stacked presets, persistence/default diff/reset, ~300ms debounce. Hide DEAD; commit/push.
4. T8 contracts/plumbing/parity/side-by-side/pending verify scaffolding per PROMPT-1; commit/push. Finish tier1 acceptance and HANDOFF, then `[tier1] complete`.
5. Continue PROMPT-2: Python hash, full towns/35-rule houses, N1-N13, endless hills/rivers world, real verify and parity images, activate deferred controls, final report. Never loosen tolerances/delete checks/invent decisions; checkpoint/commit/stop for a real blocker.

## Constraints
Never reread full specs/prompts/decision files. Use targeted rg and small ranges. Keep Python reference frozen. Follow T3-DECISIONS.md and SETTINGS-CLASHES.md; no park code/stub/preset/check. Per user, continue tier 2 after tier 1 without requesting permission. Update checkpoint and cost log at milestones.
