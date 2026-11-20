# Review of the 0.21 viewer

Reviewed `csharp-spawnerv0.21` on 18 Nov 2026 against [REVIEW.md](C:\Users\hhofe\Documents\grokkable projects\t3 testbed\csharp-spawnerv0.21\REVIEW.md). No generator, viewer, or test file was edited. The window was left open.

The 100-mile view in `csharp-spawnerv0.21` reaches ready. Seed 31, 4,788 chunks, about 35 seconds, 466 MB. The review's same window never got there.

![100-mile overview, seed 31](C:\Users\hhofe\AppData\Local\Temp\spawner-qa\15-overview.png)

The window is still open on that view. I put the settings file back to seed 665353858. The running process is still seed 31 until you regenerate.

## What the clock said

| Stage | Chunks | This run | Review, same zoom |
|---|---:|---:|---:|
| Elevation | 4,788 | 13.2 s round trip, worker 12.7 s | 50 s, then 2 chunks/s once trees were up |
| Water | 349 | 0.4 s | 100 s |
| Population | 4,822 | 2.2 s, 617 people, 26 sites | 180 s |
| Rail | 4,822 | 6.2 s, worker 5.9 s | 245 s |
| Roads | 4,822 | 9.2 s, worker 9.0 s | 159 s |
| Rural lots | skipped | no stage | 583 s and about 2 GB |
| Towns | 4,822 | 3.5 s, 27 settlements | 998 s, mostly stuck in the rural draw |
| Trees | skipped | no stage | 2,036 s |
| Ready | 100 x 46.86 mi | 35 s, 466 MB, max frame 31 ms | never |

Opening 5 miles was ready in about 4 seconds. A 21.5-mile view of 224 chunks was ready in about 5 seconds. `dotnet test` is still 7 passed, 0 failed, 28 seconds. No new asserts.

The source of 0.21 matches 0.2 except the window title and the readme heading. [REVIEW.md](C:\Users\hhofe\Documents\grokkable projects\t3 testbed\csharp-spawnerv0.21\REVIEW.md) is the prescription. The speed work that landed is real, and it is only part of that list.

## Hostile QA

**The overview gates work.** Above 12 miles the viewer does not generate rural lots. Above 1.5 miles it does not generate trees or wall plans. `DrawRuralLots` and `DrawTrees` return before the loop. That is why this 100-mile pass stayed at 466 MB and the frame stayed under 31 ms. Gravel is hidden past 48 miles, and the picture above is rail, paved roads, rivers, contours, and town dots.

**The library does not have those gates.** `WorldQuery.Load` still builds rural lots and trunk dots, then throws them away on a Far query. A Unity caller asking for the same 100-mile overview is still on the path the review timed at about 2,000 seconds and 5 GB. The viewer and the query are two pipelines.

**JSON children are still the transport.** Every stage is a child process and a full stdin document. Batching at 64 results, plus skipping rural, is what made 35 seconds possible. The tax is still visible. Elevation apply on the UI thread was 5.2 seconds. Road apply was 6.9 seconds. On the 15-chunk opening view the elevation worker took 338 ms and the round trip took 582 ms.

**A cached town can grow farms inside it.** Rural is skipped on the way out and not replayed when you zoom back onto chunks that already have towns. Town removal only runs while the town is being built. I did not get a clean picture of that failure: the zoom I aimed at a new town had to rebuild elevation, so rural ran before towns and the lots stayed outside the plat. The code path is still there.

**Close-up is rough.** At 0.56 miles the view was empty ground and the wall worker still emitted 251 KB of plans for the chunk. Plans are clipped to the chunk, not the rectangle. Wall Y is still hardcoded to 0 in [WallPlanSpawner.cs](C:\Users\hhofe\Documents\grokkable projects\t3 testbed\csharp-spawnerv0.21\CsharpSpawner\WallPlanSpawner.cs). At 0.67 miles the settlers are a pile of overlapping names in the field beside the plat, clipped by the window edge.

![Settler names piled on the field](C:\Users\hhofe\AppData\Local\Temp\spawner-qa\12-town-close.png)

**North is toward the bottom of the window.** `MapSurface.Map` adds map Y, which is north, to screen Y. The opening town sits below its road on screen because that direction is north.

**The rest of the review's behavior list is unchanged.**

- Far queries still drop stream order 2. The viewer draws order 2 and up past 12 miles, so the overview and the query disagree about which rivers exist.
- If every rail bow fails, the link is dropped. The comment above that return says the direct approach is kept.
- `mainCrossesTrack` is set false and never updated, so Main Street is always the middle east-west row. Base streets are not angle-checked. A residential pass-through is a label and a 0.25-mile offset. Houses are still placed on that track.
- Sidewalk width 4.5 ft and inset 2.25 ft still cancel, so the walk centerline sits on the curb. There is still no roll that omits both sides.
- The town outline chamfers corners. It does not cut along the rail, the paved road, or the river.
- Rural lots still have no separate garage, so the driveway branch the instructions ask for cannot fire. A coop corner skips any settlement within 2 miles and does not look for a rail elevator.
- The bank-link test still only checks that an id exists. The seven tests are the same seven.
- An in-flight batch is not cancelled. `PruneCache` still drops chunks outside the view plus 2 miles on every wheel notch, so the trip back from 100 miles to a town throws the overview away.
- The 30-foot mesh still runs for every Mid and Near query, with no size gate.
- The root namespace is still `csharp_spawnerv0._1`. The rural pause sentence is still unreachable, because it requires the rural toggle to be on and off.

## Optimizations worth doing next

Skip generating the gravel grid above 48 miles. That stage was 9.0 seconds and 3.8 MB, and the 100-mile view draws none of it. It is the largest remaining viewer cost.

Stop shipping contour polylines with every elevation chunk at this zoom. The elevation document was 15.8 MB and the UI spent 5.2 seconds applying it. The hillshade bitmap is already the picture. Keep the contours for the zoom where a 20-foot line is readable.

Put `WorldQuery` on the same detail gates as the viewer, and call the spatial index from `GenerateChunk` rather than only inside the worker after the JSON copy. The index cells are 1 mile. The review asked for cells near the 0.04 to 0.12 mile margins. A Far load should not allocate the yard markers it is about to discard.

Build road geometry off the UI thread. The 6.9 second road apply is the window, not the solver.

For a far town, emit the point. The 100-mile town worker wrote 9.0 MB of full plats in 3.3 seconds, and the map draws dots. Generating the plat on the way in also gives town removal a rural list to cut against.

## Features

A north arrow, and north at the top of the window. The mile readout is already in the status line. A scale bar belongs next to it.

Click a town dot and zoom there. At 100 miles there are 27 settlements and no names. Hover can show the name. The close-up labels need a layout that does not stack "Elizabeth de Boer" on top of "Gerrit Bos".

An Apply control beside the seed box. The box does nothing until Regenerate on the Settings tab, which is easy to miss.

Make the Far payload match the viewer: keep order-2 rivers, since those are the rivers that cut gravel. Sample elevation for wall Y so a house on a slope is not planted at sea level. Fix the sidewalk inset so the walk sits beside the curb.

A headless `--benchmark` for the 100-mile window. The profile lines already have the numbers. The console host still only generates a small box, so the next person will time this with the window again.
