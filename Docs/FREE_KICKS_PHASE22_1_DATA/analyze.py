"""Phase 22.1: analysis of the diagnostic cells. usage: python3 -I analyze.py ARM_A_DIR[,DIR...] ARM_B_DIR[,DIR...] [LEVEL]   (several directories are pooled; seeds must not repeat across them)
Compares two arms played on the SAME seeds (and the same team pairings): per-match means with standard errors, and PAIRED differences by seed."""
import csv, glob, math, os, sys, statistics

def load(dirs, level=None):
    """dirs: one directory or several separated by commas (pooled)."""
    rows = {}
    files = []
    for d in dirs.split(","): files += sorted(glob.glob(os.path.join(d, "*.csv")))
    for f in files:
        b = os.path.basename(f)
        if b.endswith("_shots.csv") or b.endswith("_goals.csv"): continue
        if level and ("_%s_" % level) not in b: continue
        for r in csv.DictReader(open(f)):
            rows[int(r["seed"])] = r
    return rows

def both(r, k):
    return float(r.get("h_" + k, 0) or 0) + float(r.get("a_" + k, 0) or 0)

def stats(vals):
    n = len(vals)
    m = sum(vals) / n
    sd = statistics.pstdev(vals) * math.sqrt(n / max(1, n - 1)) if n > 1 else 0.0
    return m, sd / math.sqrt(n) if n else 0.0

METRICS = ["goals", "shotsGoal", "shotsRated", "fouls", "yellow", "red", "tackles", "challenges", "contacts", "possessions", "passAtt", "passDone", "dangerEntries", "finalThird", "clearChances",
           "fkTaken", "fkShot", "fkPass", "fkGoal", "dgFouls", "dgFoulsFinal", "dgFoulsChance", "dgClosed", "dgShot", "dgGoal", "dgChanceClosed", "dgChanceShot", "dgChanceGoal",
           "dgCtlN", "dgCtlShot", "dgCtlGoal", "dgLostTime", "dgProgress", "dgAwarded", "dgPrepared", "dgTaken", "dgAnomalies", "dgStopSteps"]

def main():
    a = load(sys.argv[1], sys.argv[3] if len(sys.argv) > 3 else None); b = load(sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else None)
    seeds = sorted(set(a) & set(b))
    print("matches: arm A %d, arm B %d, common seeds %d" % (len(a), len(b), len(seeds)))
    print("%-16s %18s %18s %20s %s" % ("metric (both teams)", "A", "B", "paired B-A (SE)", "z"))
    for k in METRICS:
        if ("h_" + k) not in next(iter(a.values())) or ("h_" + k) not in next(iter(b.values())): continue
        va = [both(a[s], k) for s in seeds]; vb = [both(b[s], k) for s in seeds]
        ma, sa = stats(va); mb, sb = stats(vb)
        d = [y - x for x, y in zip(va, vb)]; md, sd = stats(d)
        print("%-16s %9.2f +-%5.2f %9.2f +-%5.2f %+9.2f (%5.2f) %+6.1f" % (k, ma, sa, mb, sb, md, sd, md / sd if sd else 0.0))
    pa = [both(a[s], "dgPlayed") / 2 for s in seeds] if "h_dgPlayed" in next(iter(a.values())) else []
    if pa: print("match seconds played: A %.1f  B %.1f" % (stats(pa)[0], stats([both(b[s], "dgPlayed") / 2 for s in seeds])[0]))

main()
