# Drainage-first world rebuild

Pushed only to `python-test/spawner`; no PR or merge.

| Item | Implementation commit |
| --- | --- |
| 1 drainage | e7f582d (checkpoint edc4b44) |
| 2 paved routes | f0164fd |
| 3 correction lines | b94b173 |
| 4 bridges | f834d61 |
| 5 rail | 2d14e32 |
| 6 towns | 95f48f5 |
| 7 farmsteads | 840b56b |
| Shared style and browser review fixes | ac2e682, plus final capture commit |

`py -3.11 -m pytest python-spawner/tests -q --tb=short`: **109 passed in 59.93s**.
Five-seed geometric verification passes: 13,14,30,31,46. See [JSON](final-verification.json).
Five legacy parity pairs remain byte-identical. Five changed world baselines are
in [the manifest](parity/world-baselines.json); a separate seed31 regeneration
matches its final checksum. World changes are intentional: basin topology and
valleys, continuous paved corridors, corrections, bridges, two rail directions,
route towns, farm density/access, and matching browser/export styles.

Real Chromium PNG/SVG comparison of the same seed31 rectangle (-4,-3,10,10) mi at
800 px: RGB mean absolute error 0.583765/255; severe pixel fraction 0.0001078125.
Antialiasers differ; palette and stroke coverage also have a strict pytest check.
[Browser comparison JSON](browser-png-parity-seed31-x-4-y-3.json).
Farmsteads/sq mi: seed14 1.58; seed31 1.4575; seed46 1.4975.

Town limitation: 6-10 mi describes candidate rail stations. Floodplain rejection
or six-mile junction competition can omit stations, leaving larger accepted gaps.
No new rural lot shapes, foliage, trailer parks or far-zoom palette were introduced.

## Real browser screenshots

All seed31, coordinates in miles. Bounds identify the top-left corner and span.

- [3 mi, (-4.5,-1.5)](final-seed31-x-4.5-y-1.5-3mi.png)
- [10 mi, (-4,-3)](final-seed31-x-4-y-3-10mi.png)
- [30 mi, (-15,-15)](final-seed31-x-15-y-15-30mi.png)
- [96 mi, (-48,-48)](final-seed31-x-48-y-48-96mi.png)
- [Rail/road crossing, (0.85,0.6), 0.8 mi](final-seed31-x0.85-y0.6-rail-road-crossing.png)
- [Paved correction jog, (-1.5,22.5), 4 mi](final-seed31-x-1.5-y22.5-paved-correction-jog.png)
- [Bridge, (-3.4,-0.3), 0.65 mi](final-seed31-x-3.4-y-0.3-bridge.png)

96-mi browser validation: **1,024 visible loaded chunks, zero transparent center pixels**.
