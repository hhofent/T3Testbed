import copy, sys, os
sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))
import pytest
from spawner import make_lot, spawn, generate_attempt, H, FT, IN, Door
from validate import validate, APPROVED

# Fixture: a plan with every optional feature so each rule is live.
FORCE = {"garage": "double", "dining": True, "foundation": "slab", "baths": 2, "beds": 3,
         "porch": True, "deck": True, "patio": False, "mirror": False}

@pytest.fixture(scope="module")
def good():
    plan, rep, _, _ = spawn(make_lot("fixture", 100), 40, FORCE)
    assert plan is not None and not [k for k, (s, _) in rep.items() if s == "fail"]
    return plan

def test_same_seed_same_plan():
    lot = make_lot("w70-1", 70)
    assert spawn(lot)[0].digest() == spawn(copy.deepcopy(lot))[0].digest()

def test_editing_one_lot_leaves_neighbors_unchanged():
    street = lambda w1: [make_lot(f"lot:{i}", w1 if i == 1 else 80) for i in range(3)]
    a = [spawn(l)[0].digest() for l in street(80)]
    b = [spawn(l)[0].digest() for l in street(100)]
    assert a[0] == b[0] and a[2] == b[2] and a[1] != b[1]

def door(p, pred): return next(d for d in p.doors if pred(d))
def kill_doors(p, pred): p.doors = [d for d in p.doors if not pred(d)]

MUT = {
    "U1": lambda p: p.lot.update(road=None),
    "U2": lambda p: setattr(p, "rooms", [r for r in p.rooms if r.kind != "kitchen"]),
    "U3": lambda p: setattr(p, "windows", [w for w in p.windows if w.room != "kitchen"]),
    "1":  lambda p: [setattr(d, "a", "dining") for d in p.doors if d.role == "front"],
    "2":  lambda p: p.lot.update(front_setback=FT(40), front_min=FT(40)),
    "3":  lambda p: p.site.update(drive=[]),
    "4":  lambda p: setattr(p.room("garage"), "h", FT(18)),
    "5":  lambda p: p.doors.append(Door("g-bed", "garage", "bed1", 0, 0, 0, 0, IN(36), IN(80), IN(38), "garage_house")),
    "6":  lambda p: p.program.update(master=min((r for r in p.rooms if r.kind == "bedroom"), key=lambda r: r.area).id),
    "7":  lambda p: kill_doors(p, lambda d: "hall" in (d.a, d.b) and d.b.startswith("bath")),
    "8":  lambda p: kill_doors(p, lambda d: {d.a, d.b} == {"kitchen", "dining"}),
    "9":  lambda p: p.doors.append(Door("x", "living", "bed1", 0, 0, 0, 0, IN(30), IN(80), IN(32), "interior")),
    "10": lambda p: [setattr(r, "fan", False) for r in p.rooms if r.kind == "bath"] and None or setattr(p, "windows", [w for w in p.windows if not w.room.startswith("bath")]),
    "11": lambda p: kill_doors(p, lambda d: {d.a, d.b} == {"living", "hall"}),
    "12": lambda p: setattr(p.room("hall"), "h", IN(30)),
    "16": lambda p: [setattr(w, "ro_h", IN(36)) for w in p.windows if w.room.startswith("bed")],
    "17": lambda p: [setattr(d, "w", IN(32)) for d in p.doors if d.b == "outside"],
    "19": None, "34": None, "35": None,   # not-2D / checked by the determinism tests
    "20": lambda p: setattr(p.room("hall"), "x", p.room("hall").x + IN(1)),
    "21": lambda p: p.program.update(style=None),
    "22": lambda p: p.program.update(beds=p.program["beds"] + 1),
    "23": lambda p: setattr(p.room("living"), "w", p.room("living").w + FT(60)),
    "24": lambda p: p.program.update(foundation="mystery"),
    "25": lambda p: setattr(p, "rooms", [r for r in p.rooms if r.kind != "utility"]),
    "26": lambda p: setattr(next(r for r in p.rooms if r.kind == "bedroom"), "closet", None),
    "27": lambda p: setattr(door(p, lambda d: d.role == "rear"), "a", "living"),
    "28": lambda p: setattr(p.windows[0], "head", IN(80)),
    "30": lambda p: p.site.update(porch=(0, 0, FT(8), FT(6))),
    "31": lambda p: setattr(p.room("bath2"), "x", p.room("bath2").x + FT(100)),
    "32": lambda p: setattr(door(p, lambda d: d.b == "bed1"), "a", "living"),
}

@pytest.mark.parametrize("rule", [r for r in APPROVED if MUT.get(r)])
def test_validator_catches_each_approved_rule(good, rule):
    assert validate(good)[rule][0] == "pass"
    bad = copy.deepcopy(good); MUT[rule](bad)
    assert validate(bad)[rule][0] == "fail", validate(bad)[rule]

def test_small_batch_runs():
    from batch import run
    stats, rows = run(per_width=2)
    assert len(rows) == 8 and stats["success_rate"] > 0

def test_every_house_gets_a_drive_and_detached_garage_respects_accessory_setbacks():
    plan, rep, _, _ = spawn(make_lot("det", 80), 40, {"garage": "detached"})
    assert plan and plan.site["detached_garage"] and rep["2"][0] != "fail" and rep["3"][0] == "pass"
    bad = copy.deepcopy(plan); g = bad.room("garage"); g.y = bad.lot["depth"] - g.h - FT(1)
    assert validate(bad)["2"][0] == "fail"
    nog, rep2, _, _ = spawn(make_lot("nog", 80), 40, {"garage": "none"})
    assert rep2["3"][0] == "pass" and FT(9) <= nog.site["drive"][0][2] <= FT(10)

def test_trailer_on_single_50ft_lot():
    from spawner import TRAILER_NA
    for s in range(3):
        lot = make_lot(f"tr{s}", 50, 7 + s, 140, overrides={"house_type": "trailer", "alley_rear": True, "kind": "town", "base_lots": 1})
        plan, rep, _, _ = spawn(lot)
        assert plan and plan.program["type"] == "trailer"
        x0 = min(r.x for r in plan.rooms); x1 = max(r.x + r.w for r in plan.rooms)
        y0 = min(r.y for r in plan.rooms); y1 = max(r.y + r.h for r in plan.rooms)
        assert FT(14) <= x1 - x0 <= FT(16) and FT(60) <= y1 - y0 <= FT(76)
        assert plan.site.get("drive_surface") == "gravel" and not any(r.kind == "garage" for r in plan.rooms)
        assert any((r[0] == x1 or r[0] + r[2] == x0) and r[1] == 0 for r in plan.site["drive"])   # pad beside, from the road
        assert all(rep[k][0] == "n/a" for k in TRAILER_NA if k != "21") and rep["3"][0] == "pass"
