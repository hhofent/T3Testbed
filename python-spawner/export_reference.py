"""Export Python reference stats for the C# port.
Run from the python spawner dir:  .venv/bin/python export_reference.py [--layout-seeds 1-100] [--full-seeds 13,14,30,31,46] [--out DIR]
Writes reference_out/layout_stats.json (make_region only, fast) and reference_out/full_stats.json (houses spawned)."""
import json, os, sys, time
from collections import Counter
sys.path.insert(0, os.getcwd())
from region import make_region, spawn_region, FT

def rng_arg(name, default):
    if name in sys.argv:
        v = sys.argv[sys.argv.index(name) + 1]
        if "-" in v and "," not in v: a, b = map(int, v.split("-")); return list(range(a, b + 1))
        return [int(x) for x in v.split(",")]
    return default

def layout(seed):
    r = make_region(seed); t = r["town"]
    lots = [l["spec"] for l in r["lots"]]; kinds = Counter(s["kind"] for s in lots)
    town_lots = [s for s in lots if s["kind"] == "town"]
    out = {"seed": seed, "lots": len(lots), "by_kind": dict(kinds), "road_miles": r["road_miles"],
           "paved_share": r.get("paved_share"), "rail": bool(r["rail"]), "creeks": len(r.get("creeks") or []),
           "parcels": sorted(p["kind"] for p in r["parcels"])}
    if r["rail"]:
        out["rail_crossings"] = len(r["rail"]["crossings"]); out["rail_parallel_ft"] = (r["rail"]["parallel"][1] - r["rail"]["parallel"][0]) / FT(1)
    if t:
        out["town"] = {"size": t["size"], "rail_town": t["rail_town"], "cols": t["cols"], "rows": t["rows"], "blocks": len(t["blocks"]),
                       "houses": len(town_lots), "trailers": sum(s["house_type"] == "trailer" for s in town_lots),
                       "pop_est": t["pop_est"], "band": t["band"], "year": t["year"],
                       "businesses": sorted(c["type"] for c in t["commercial"]), "unplaced": t["unplaced"]}
    return out

def full(seed):
    r = make_region(seed); t0 = time.time(); res = spawn_region(r)
    fails = Counter(); [fails.update(v["fails"]) for v in res.values()]
    return {"seed": seed, "lots": len(res), "built": sum(bool(v["plan"]) for v in res.values()),
            "avg_attempts": round(sum(v["attempts"] for v in res.values()) / max(1, len(res)), 3),
            "retry_causes": dict(fails), "drive_rule3": dict(Counter(v["report"]["3"][0] for v in res.values() if v["report"])),
            "seconds": round(time.time() - t0, 1)}

if __name__ == "__main__":
    outd = sys.argv[sys.argv.index("--out") + 1] if "--out" in sys.argv else "reference_out"; os.makedirs(outd, exist_ok=True)
    L = [layout(s) for s in rng_arg("--layout-seeds", list(range(1, 101)))]
    json.dump(L, open(os.path.join(outd, "layout_stats.json"), "w"), indent=1)
    F = [full(s) for s in rng_arg("--full-seeds", [13, 14, 30, 31, 46])]
    json.dump(F, open(os.path.join(outd, "full_stats.json"), "w"), indent=1)
    print(len(L), "layout seeds;", [(f["seed"], f["built"], f["lots"]) for f in F])
