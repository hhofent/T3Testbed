"""Batch: 10 lots per width (60/70/80/100 ft) = 40 seeds; stats + 4 samples + contact sheet."""
import json, sys
from collections import Counter
from spawner import make_lot, spawn, FT
from render import save, contact_sheet

def run(per_width=10, widths=(60, 70, 80, 100), seed=1985, max_attempts=25):
    rows, retry = [], Counter()
    for w in widths:
        for i in range(per_width):
            lot = make_lot(f"w{w}-{i}", w, seed)
            plan, rep, att, fails = spawn(lot, max_attempts)
            retry.update(fails)
            warns = [k for k, (s, _) in rep.items() if s == "warn"] if rep else []
            rows.append({"lot": lot["id"], "width": w, "ok": plan is not None, "attempts": att,
                         "garage": plan.program["garage_built"] if plan else None,
                         "garage_drawn": plan.program["garage"] if plan else None, "warns": warns,
                         "beds": plan.program["beds"] if plan else None, "fails": fails})
    by_w = {}
    for w in widths:
        r = [x for x in rows if x["width"] == w]
        by_w[w] = {"success": sum(x["ok"] for x in r) / len(r), "avg_attempts": sum(x["attempts"] for x in r) / len(r),
                   "garages": Counter(x["garage"] for x in r), "rule2_shrink_warn": sum("2" in x["warns"] for x in r)}
    ok = [x for x in rows if x["ok"]]
    stats = {"seeds": len(rows), "success_rate": len(ok) / len(rows),
             "avg_attempts": sum(x["attempts"] for x in rows) / len(rows),
             "avg_attempts_successful": sum(x["attempts"] for x in ok) / max(1, len(ok)),
             "retry_causes": retry.most_common(), "by_width": by_w,
             "warn_counts": Counter(w for x in rows for w in x["warns"]).most_common()}
    return stats, rows

if __name__ == "__main__":
    stats, rows = run()
    json.dump({"stats": stats, "rows": rows}, open("out/batch.json", "w"), indent=1, default=str)
    print(json.dumps(stats, indent=1, default=str))
    # samples: one per width, mixing garage / no garage
    picks = []
    for w in (60, 70, 80, 100):
        cands = [x for x in rows if x["width"] == w and x["ok"]]
        want_none = w in (60, 80)
        pick = next((x for x in cands if (x["garage"] in ("none", "detached")) == want_none), cands[0])
        picks.append(pick["lot"])
    items = []
    for lid in picks:
        w = int(lid[1:].split("-")[0])
        plan, rep, _, _ = spawn(make_lot(lid, w, 1985))
        save(plan, rep, f"out/sample_{lid}.png"); items.append((plan, rep))
        json.dump({k: v for k, v in rep.items()}, open(f"out/sample_{lid}.report.json", "w"), indent=1)
    contact_sheet(items, "out/contact_sheet.png")
    print("samples:", picks)
