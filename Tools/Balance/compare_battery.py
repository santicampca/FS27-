"""Phase 17: summary of a quick-battery directory, optionally compared with a saved reference.
usage: python3 -I compare_battery.py OUTDIR [--reference REF.json] [--write-reference REF.json]
A run of the SAME code and configuration reproduces the numbers exactly (the Core is deterministic); a difference therefore means the code or the configuration changed. How big a difference has to be before it
means something is given by the bands below; with 20 matches per cell they are wide: this is a regression alarm, NOT a proof of balance or of an improvement."""
import csv, glob, json, math, os, statistics, sys

BANDS = {  # metric: absolute band (about two standard errors of 20 matches, or a safe margin)
    "goals": 1.3, "stam_q1": 4.0, "stam_q2": 3.0, "stam_q3": 3.0, "stam_q4": 3.0, "stam_mean": 2.5, "low5": 0.08, "keeper_stamina": 2.0,
    "shots": 6.0, "pass_completion": 0.03, "mistakes_per_carrier_decision": 0.01, "goals_origin_known": 0.10,
}

def cell_metrics(path):
    rows = list(csv.DictReader(open(path)))
    n = len(rows)
    def side(k): return sum(float(r["h_" + k]) + float(r["a_" + k]) for r in rows)
    goals = [float(r["h_goals"]) + float(r["a_goals"]) for r in rows]
    total_goals = sum(goals)
    return {
        "matches": n, "goals": statistics.mean(goals), "goals_sd": statistics.pstdev(goals),
        "stam_q1": side("stam1") / n / 2, "stam_q2": side("stam2") / n / 2, "stam_q3": side("stam3") / n / 2, "stam_q4": side("stam4") / n / 2, "stam_mean": side("stamMean") / n / 2,
        "low5": side("stamLow5") / n / 2, "keeper_stamina": side("keeperStamina") / n / 2, "shots": side("shotsRated") / n,
        "pass_completion": side("passDone") / max(1.0, side("passAtt")), "mistakes_per_carrier_decision": side("mistakes") / max(1.0, side("carrierDecisions")),
        "goals_origin_known": (total_goals - side("goalsUnknown")) / total_goals if total_goals else float("nan"),
    }

def main():
    args = sys.argv[1:]
    out = args[0]
    ref = args[args.index("--reference") + 1] if "--reference" in args else None
    write = args[args.index("--write-reference") + 1] if "--write-reference" in args else None
    cells = {}
    for f in sorted(glob.glob(os.path.join(out, "*_vs_*.csv"))):
        if f.endswith("_shots.csv") or f.endswith("_goals.csv"): continue
        name = os.path.basename(f)[:-4]
        status = open(f[:-4] + ".status").read().strip() if os.path.exists(f[:-4] + ".status") else "UNKNOWN"
        seconds = float(open(f[:-4] + ".time").read()) if os.path.exists(f[:-4] + ".time") else float("nan")
        m = cell_metrics(f); m["status"] = status; m["seconds"] = seconds
        cells[name] = m
    meta = {"commit": open(os.path.join(out, "COMMIT.txt")).read().strip() if os.path.exists(os.path.join(out, "COMMIT.txt")) else "",
            "config": open(os.path.join(out, "EXP.txt")).read().strip() if os.path.exists(os.path.join(out, "EXP.txt")) else "",
            "total_seconds": float(open(os.path.join(out, "TOTAL.time")).read()) if os.path.exists(os.path.join(out, "TOTAL.time")) else float("nan")}
    summary = {"meta": meta, "cells": cells}
    print("quick battery: commit %s config '%s' total %.0f s (limit per run 1100 s)" % (meta["commit"][:7], meta["config"], meta["total_seconds"]))
    bad = [n for n, c in cells.items() if c["status"] != "OK"]
    for n, c in cells.items():
        print("%-40s %s %4.0fs n=%d goals %.2f stam Q1-4 %.1f %.1f %.1f %.1f low5 %.2f keeper %.1f pass %.3f mistakes %.4f originKnown %.2f" %
              (n, c["status"], c["seconds"], c["matches"], c["goals"], c["stam_q1"], c["stam_q2"], c["stam_q3"], c["stam_q4"], c["low5"], c["keeper_stamina"], c["pass_completion"], c["mistakes_per_carrier_decision"], c["goals_origin_known"]))
    flags = []
    if ref:
        reference = json.load(open(ref))
        print("reference: commit %s config '%s'" % (reference["meta"]["commit"][:7], reference["meta"]["config"]))
        for n, c in cells.items():
            r = reference["cells"].get(n)
            if r is None: flags.append("%s: not in the reference" % n); continue
            for k, band in BANDS.items():
                if abs(c[k] - r[k]) > band: flags.append("%s %s: %.4f against %.4f (band %.4f)" % (n, k, c[k], r[k], band))
    if bad: flags.append("runs not OK (incomplete or failed, never counted as passed): " + ", ".join(bad))
    print("\nOUTSIDE THE BANDS / PROBLEMS:" if flags else "\nnothing outside the bands (20 matches per cell: a regression alarm only, no statistical proof)")
    for f in flags: print("  " + f)
    if write:
        json.dump(summary, open(write, "w"), indent=1, sort_keys=True); print("reference written to", write)
    sys.exit(1 if flags and not write else 0)

main()
