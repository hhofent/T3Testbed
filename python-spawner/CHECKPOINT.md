# Checkpoint

## Status
Milestone 1 complete: 130 settings entries including numeric stage packs; recursive
validation/clamping; settings propagation; parks removed; 1990 default and explicit
parity mode. 65 tests pass. Five before/after PNG pairs are byte-identical in parity
mode (N9 year, N13 park exclusions). Raw-default incompatibility is documented in
docs/SETTINGS-AND-PARITY.md. Original source branch lacks CODING-AGENT-PROMPT.md.
Milestones 2-6 pending. Work only in python-spawner on python-test/spawner.

## Exact next step
Implement milestone 2: typed JSON presets save/load/diff, six stackable presets,
and --param dotted overrides with clamping. Then milestones 3-6 in order.
