# Grilling Session Plan — Procedural Generation Tuning Parameters

## Purpose
Establish deep shared understanding before implementation. Each "grilling" = one focused Q&A round on a specific topic. Answers are saved persistently to build a knowledge base.

---

## Persistence Mechanism

### Storage
- **File:** `docs/research/GRILLED_DATA.md` — append-only log of all Q&A
- **Format:** Markdown with YAML front-matter per entry
- **Index:** `docs/research/GRILL_INDEX.json` — searchable index

### Entry Schema
```yaml
---
id: G-001
topic: "Noise Parameterization"
date: "2025-01-15"
status: "complete"  # pending | in-progress | complete | deferred
tags: ["world", "hills", "rivers", "rail", "noise"]
---
## Question
[The question asked]

## Answer
[The detailed answer/resolution]

## Decisions
- [Decision 1]
- [Decision 2]

## Open Questions
- [Question 1]
- [Question 2]

## Related Params
- [WorldConfig.ElevationWavelength1]
- [WorldConfig.RiverMeanderAmp1]
```

### Tooling (to be created)
```bash
# Add a grilled entry
python tools/grill.py add --topic "Noise" --question "..." --answer "..."

# Search grilled data
python tools/grill.py search --tag "noise"

# View all
python tools/grill.py list
```

---

## Grilling Session Structure

### Round 0: Meta-Grilling (This Document)
- [ ] Agree on persistence mechanism
- [ ] Define session cadence (one topic per turn? batch?)
- [ ] Define "done" criteria for each topic
- [ ] Identify stakeholders / decision makers

### Round 1: Philosophy & Constraints
| Topic | Questions |
|-------|-----------|
| **Design Goals** | What does "good" procgen look like for this project? Realism? Playability? Aesthetic? |
| **Target Audiences** | Who tunes params? Devs? Designers? Modders? Players? |
| **Constraints** | Performance budgets? Determinism requirements? Platform limits? |
| **Scope Boundaries** | What's in/out of scope for *this* tuning pass? |

### Round 2: Parameter Architecture
| Topic | Questions |
|-------|-----------|
| **Hierarchy** | Global → Region → Town → Lot → Instance — is this the right model? |
| **Inheritance** | How do overrides work? Merge strategies? Priority? |
| **Types** | Are `FloatRange`/`IntRange` sufficient? Need curves? Distributions? Expressions? |
| **Validation** | Hard failures vs warnings? Schema versioning strategy? |
| **Serialization** | JSON only? Binary? Hot-reload? |

### Round 3: Noise & Natural Systems (WorldMap)
| Topic | Questions |
|-------|-----------|
| **Hills** | fBm vs ridged vs domain warp? Octaves? Persistence? Lacunarity? Contour extraction method? |
| **Rivers** | Trunk spacing rationale (28 mi)? Meander model (sine sum vs Perlin)? Tributary rules? |
| **Rail** | Swell model rationale? Phase sections? Spur logic? |
| **Roads** | Paved spacing (10 mi) rationale? Grid exceptions? |
| **Towns** | Rail proximity (9 mi) rationale? Layout weights (N/S/Both)? Class distribution? |
| **Farms** | Per-section logic? Jitter ranges? Density? |

### Round 4: Human Systems (Towns, Lots, Houses)
| Topic | Questions |
|-------|-----------|
| **Town Sizes** | Crossroads/Hamlet/Village/Town — are tiers right? Columns/EWStreets mapping? |
| **Extras** | 0-2 per town — pool balance? Court vs plaza vs boulevard? |
| **Commercial** | Highway strip logic? Elevator placement? Rail industrial? South fringe? |
| **Civic/Landmarks** | Per-tier counts? Chances? Placement rules? |
| **Residential** | Garage mode balance (45/45/10)? Shed rate? Setback ranges? |
| **Streets** | Perimeter loop jog? Dead-end stub? Free streets (OLD RAIL, CRESCENT)? |
| **Houses** | Archetype balance? Dimensions by era/region? Opening rules? |

### Round 5: Rural & Countryside
| Topic | Questions |
|-------|-----------|
| **Farmstead Layout** | House setback? Machine shed placement? Bin row? Pole barn? Gambrel? |
| **Shelterbelts** | N/W only? Spacing? Species? |
| **Field Entrances** | Per-quarter logic? Setback from intersections? |
| **Countryside Cell** | 3-mi cell views — town placement jitter? Farmstead rings/groves? |

### Round 6: Developer Experience
| Topic | Questions |
|-------|-----------|
| **Profiles** | What presets needed? "Dense", "Sparse", "Modern", "Frontier", "Testing"? |
| **Distributions** | Which params need Normal/LogNormal/Poisson? Beta for fractions? |
| **Catalogs** | Business/civic/landmark/name pools — external JSON? Schema? |
| **UI** | Parameter explorer — live preview size? Debounce? What metrics shown? |
| **Analysis** | Sensitivity CLI — which params? What metrics? How many seeds? |
| **Migration** | Config versioning — auto-migrate? Backup? Diff tool? |

### Round 7: Advanced / Future
| Topic | Questions |
|-------|-----------|
| **Biomes/Regions** | 5 Iowa regions — boundaries? Transition width? Override priority? |
| **Road Topology** | Grid vs organic vs hybrid? River-following? T-junctions? Cul-de-sacs? |
| **Constraints** | Unified placement constraint system? Hard vs soft? Solver? |
| **Connectivity** | Validation pass — what checks? Performance? |
| **Temporal** | Year-based evolution? Decade deltas? |
| **Inverse Design** | Target metrics → param optimization? |
| **Style Transfer** | Reference SVG → param fitting? |

---

## Session Cadence Options

### Option A: One Topic Per Turn (Deep)
- Pro: Thorough, each topic gets full attention
- Con: Slow (20+ turns for all topics)

### Option B: Batch by Round (Moderate)
- Pro: Logical grouping, 7 turns for all rounds
- Con: Less depth per topic

### Option C: Priority-First (Pragmatic)
- Pro: Hits high-value topics first
- Con: May miss connections

### Recommendation: **Option B (Batch by Round)** with **Option C ordering within rounds**
- Round 1-2: Foundation (Philosophy + Architecture) — 2 turns
- Round 3: Noise/Natural — 1 turn (high impact)
- Round 4: Human Systems — 2 turns (most params)
- Round 5: Rural — 1 turn
- Round 6: DX — 1 turn (quick wins)
- Round 7: Advanced — 1 turn (defer decisions)
- **Total: ~7-8 turns**

---

## "Done" Criteria Per Topic

A topic is **complete** when:
- [ ] All questions answered or explicitly deferred
- [ ] Decisions documented with rationale
- [ ] Open questions listed with owners
- [ ] Related parameters identified
- [ ] Entry saved to `GRILLED_DATA.md`
- [ ] Index updated

A topic is **deferred** when:
- [ ] Explicitly marked for later phase
- [ ] Reason recorded
- [ ] Dependencies noted

---

## Approval Gate

**To start grilling, confirm:**
1. ✅ Persistence mechanism (file + index + tooling)
2. ✅ Session structure (7 rounds, batched)
3. ✅ Cadence (one round per turn, priority within)
4. ✅ Done criteria
5. ✅ First topic: **Round 1 — Philosophy & Constraints**

---

## Next Step

**Reply with "APPROVED" to begin Round 1: Philosophy & Constraints.**

Or request changes to this plan first.