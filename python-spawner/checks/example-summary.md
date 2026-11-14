# River check run

Run time: 20261114-140501-467000 UTC

| Check | Result | Measured value | Target |
|---|---:|---|---|
| stream density 0.8–1.5 mi/mi² | PASS | mean 1.427; per-seed range 0.948-1.771 mi/mi² | mean stream length density 0.8-1.5 mi per sq mi |
| pooled sinuosity median 1.6–2.2 | PASS | median 1.736 | median 1.6-2.2 |
| pooled sinuosity p90–p10 spread ≥0.4 | PASS | p10 1.468; p90 1.931; spread 0.463 | p90-p10 spread >=0.4 |
| mean per-basin N3/N4 Rb 3-5 | PASS | mean 4.896; raw per-basin range 2.40-10.00 | mean per-basin order-3/order-4 Rb 3-5 |
| aggregate order-3/order-4 ratio 3–5 | PASS | N3 59; N4 17; ratio 3.471 | aggregate N3/N4 ratio 3-5 |
| copy pairs within ε <5% | PASS | 0/397 pairs below ε=0.020; fraction 0.000% | less than 5% of normalized turning-function pairs below ε=0.02 rad |
| junction mean 55–75° | PASS | mean 65.26° | mean 55-75 degrees |
| junction SD ≥20° | PASS | SD 24.87° | standard deviation >=20 degrees |
| junctions below 25° <10% | PASS | 7.69% | less than 10% below 25 degrees |
| maximum 10° bin ≤25% | PASS | 17.16% | no 10-degree bin exceeds 25% |
| finished river bearing/width sample seed 13 | PASS | entropy 0.930; E-W 15.8%; order-1 width 12.39-15.00 ft; largest trunk 150.00 ft; width violations 0 | entropy >=0.8; E-W <=30%; order-1 width 8-15 ft; largest trunk 100-150 ft; confluence width rule |
| finished river bearing/width sample seed 14 | PASS | entropy 0.907; E-W 18.6%; order-1 width 12.39-15.00 ft; largest trunk 150.00 ft; width violations 0 | entropy >=0.8; E-W <=30%; order-1 width 8-15 ft; largest trunk 100-150 ft; confluence width rule |
| finished river bearing/width sample seed 30 | PASS | entropy 0.881; E-W 17.9%; order-1 width 12.39-15.00 ft; largest trunk 150.00 ft; width violations 0 | entropy >=0.8; E-W <=30%; order-1 width 8-15 ft; largest trunk 100-150 ft; confluence width rule |
| finished river bearing/width sample seed 31 | PASS | entropy 0.950; E-W 12.9%; order-1 width 12.39-15.00 ft; largest trunk 150.00 ft; width violations 0 | entropy >=0.8; E-W <=30%; order-1 width 8-15 ft; largest trunk 100-150 ft; confluence width rule |
| finished river bearing/width sample seed 46 | PASS | entropy 0.884; E-W 16.8%; order-1 width 12.39-15.00 ft; largest trunk 150.00 ft; width violations 0 | entropy >=0.8; E-W <=30%; order-1 width 8-15 ft; largest trunk 100-150 ft; confluence width rule |
| river continuity/crossings seed 13 | PASS | river_continuity=PASS, river_acyclic=PASS, rivers_downhill=PASS, no_river_crossings=PASS | acyclic and downhill; no river crossings outside shared junctions |
| river continuity/crossings seed 14 | PASS | river_continuity=PASS, river_acyclic=PASS, rivers_downhill=PASS, no_river_crossings=PASS | acyclic and downhill; no river crossings outside shared junctions |
| river continuity/crossings seed 30 | PASS | river_continuity=PASS, river_acyclic=PASS, rivers_downhill=PASS, no_river_crossings=PASS | acyclic and downhill; no river crossings outside shared junctions |
| river continuity/crossings seed 31 | PASS | river_continuity=PASS, river_acyclic=PASS, rivers_downhill=PASS, no_river_crossings=PASS | acyclic and downhill; no river crossings outside shared junctions |
| river continuity/crossings seed 46 | PASS | river_continuity=PASS, river_acyclic=PASS, rivers_downhill=PASS, no_river_crossings=PASS | acyclic and downhill; no river crossings outside shared junctions |
| finished road/rail bridges | PASS | 276 crossings; 276 decks in window; 276 matched | exactly one deck per crossing; length (water width + 40 ft) / sin(angle); no parallel overlaps |
| 3-mile chunks seed 31 | PASS | hash seeds 1/2 equal=True; features=12 | shuffled 3-mi chunks equal full-window features under two PYTHONHASHSEED values |
| outlet lattice and superregion bearings | PASS | 20 seeds; minimum regional circular SD 28.67°; repeated crossing positions 0 | 20 seeds; E-W outlet x positions do not repeat; circular SD ≥25° per seed |

## Failures

None.
