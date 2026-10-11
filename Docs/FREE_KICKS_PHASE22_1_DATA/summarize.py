"""Phase 22.1: every table of the report, from the CSVs in this directory. usage: python3 -I summarize.py > summary.txt
Arm A = free kick OFF (FS27_EXP C.FreeKick.Enabled=0: bit-identical to Phase 21), arm B = free kick ON (the Phase 22 behaviour), same seeds and team pairings, paired by seed."""
import csv, glob, math, os, statistics

HERE = os.path.dirname(os.path.abspath(__file__))
P21 = os.path.join(HERE, "..", "REFEREE_CARDS_PHASE21_data", "after_%s", "Classic_%s_vs_%s.csv")
P22 = os.path.join(HERE, "..", "FREE_KICKS_PHASE22_data", "after_%s", "Classic_%s_vs_%s.csv")

def rows_of(path):
    return {int(r["seed"]): r for r in csv.DictReader(open(path))}

def cells(d, level):
    out = {}
    for f in sorted(glob.glob(os.path.join(HERE, d, "Classic_%s_*.csv" % level))):
        if f.endswith(("_shots.csv", "_goals.csv")): continue
        out.update(rows_of(f))
    return out

def both(r, k): return float(r["h_" + k]) + float(r["a_" + k])

def arms(level):
    """-> {block: (A rows, B rows)}"""
    res = {}
    res["5000"] = ({**rows_of(P21 % ("5001", level, level)), **rows_of(P21 % ("5101", level, level))}, {**rows_of(P22 % ("5001", level, level)), **rows_of(P22 % ("5101", level, level))})
    if level == "Professional":
        res["5000dg"] = (cells("off", level), cells("on", level))
        res["6000"] = (cells("off_pro_6000", level), cells("on_pro_6000", level))
        res["7000"] = (cells("off_pro_7000", level), cells("on_pro_7000", level))
    else:
        res["6000"] = (cells("off_ne_6000", level), cells("on_ne_6000", level))
        if level == "Novice": res["7000"] = (cells("off_no_7000", level), cells("on_no_7000", level))
    return res

def mse(v):
    n = len(v); m = sum(v) / n
    return m, (statistics.stdev(v) / math.sqrt(n) if n > 1 else 0.0)

def paired(A, B, k):
    seeds = sorted(set(A) & set(B))
    d = [both(B[s], k) - both(A[s], k) for s in seeds]
    ma = sum(both(A[s], k) for s in seeds) / len(seeds); mb = sum(both(B[s], k) for s in seeds) / len(seeds)
    m, se = mse(d)
    return len(seeds), ma, mb, m, se

print("# 1. goals per match (both teams), paired by seed, free kick OFF (A) -> ON (B)")
print("%-13s %-7s %4s %7s %7s %7s %6s %6s" % ("level", "block", "n", "A", "B", "diff", "SE", "z"))
pool = {}
for level in ("Professional", "Novice", "Elite"):
    for blk, (A, B) in arms(level).items():
        if blk == "5000dg": continue
        n, ma, mb, m, se = paired(A, B, "goals")
        print("%-13s %-7s %4d %7.2f %7.2f %+7.2f %6.2f %+6.1f" % (level, blk, n, ma, mb, m, se, m / se if se else 0))
        for s in sorted(set(A) & set(B)): pool.setdefault(level, []).append((both(A[s], "goals"), both(B[s], "goals"), blk))
print()
allv = []
for level, L in pool.items():
    d = [y - x for x, y, _ in L]; m, se = mse(d)
    print("%-13s pooled n=%3d  A %.2f  B %.2f  diff %+.2f (SE %.2f, z %+.1f)  relative %+.1f%%" % (level, len(L), sum(x for x, _, _ in L) / len(L), sum(y for _, y, _ in L) / len(L), m, se, m / se, 100 * (sum(y for _, y, _ in L) / sum(x for x, _, _ in L) - 1)))
    allv += L
d = [y - x for x, y, _ in allv]; m, se = mse(d)
print("%-13s pooled n=%3d  A %.2f  B %.2f  diff %+.2f (SE %.2f, z %+.1f)  relative %+.1f%%" % ("ALL", len(allv), sum(x for x, _, _ in allv) / len(allv), sum(y for _, y, _ in allv) / len(allv), m, se, m / se, 100 * (sum(y for _, y, _ in allv) / sum(x for x, _, _ in allv) - 1)))
for blk in ("5000", "6000", "7000"):
    sel = [y - x for x, y, b in allv if b == blk]
    if sel: print("  block %s: n=%d diff %+.2f (SE %.2f)" % (blk, len(sel), statistics.mean(sel), statistics.stdev(sel) / math.sqrt(len(sel))))
pro_dg = [(s, a, b) for blk in ("5000dg", "6000", "7000") for s, a, b in [(blk, arms("Professional")[blk][0], arms("Professional")[blk][1])]]

print()
print("# 2. Professional mechanism table (blocks with the diagnostic columns: seeds 5001-5120, 6001-6500, 7001-7500; paired by seed)")
A = {}; B = {}
for blk in ("5000dg", "6000", "7000"):
    A.update(arms("Professional")[blk][0]); B.update(arms("Professional")[blk][1])
print("matches:", len(set(A) & set(B)))
print("%-16s %9s %9s %9s %6s %6s" % ("metric", "A (off)", "B (on)", "B-A", "SE", "z"))
for k in ["goals", "shotsRated", "shotsGoal", "xgSum", "shotQuality", "dangerEntries", "finalThird", "clearChances", "possessions", "passAtt", "passDone", "tackles", "challenges", "contacts", "fouls", "yellow", "red",
          "fkTaken", "fkShot", "fkPass", "fkOther", "fkGoal", "dgFoulsFinal", "dgFoulsChance", "dgShot", "dgGoal", "dgChanceShot", "dgChanceGoal", "dgLostTime", "dgProgress", "dgCtlN", "dgCtlShot", "dgCtlGoal", "dgAnomalies", "dgStopSteps"]:
    n, ma, mb, m, se = paired(A, B, k)
    print("%-16s %9.2f %9.2f %+9.2f %6.2f %+6.1f" % (k, ma, mb, m, se, m / se if se else 0))
sec = [both(A[s], "dgPlayed") / 2 for s in A]; secb = [both(B[s], "dgPlayed") / 2 for s in B]
print("match seconds played (of 960 steps): A %.2f B %.2f" % (statistics.mean(sec), statistics.mean(secb)))
fk = sum(both(B[s], "dgTaken") for s in B); an = sum(both(B[s], "dgAnomalies") for s in B)
print("free kicks %d, set-up anomalies %d (%.1f%%), awarded==prepared==taken in every match: %s" % (fk, an, 100 * an / fk, all(both(B[s], "dgAwarded") - both(B[s], "dgTaken") <= 1 and both(B[s], "dgPrepared") == both(B[s], "dgTaken") for s in B)))
fkg = sum(both(B[s], "fkGoal") for s in B); fks = sum(both(B[s], "fkShot") for s in B); fkp = sum(both(B[s], "fkPass") for s in B); fko = sum(both(B[s], "fkOther") for s in B)
print("per free kick: shot %.0f%%, pass %.0f%%, other %.0f%%; goals within 8 s %.1f%% of free kicks (%.1f%% of the shots); taken by the taker %.0f%%" % (100 * fks / fk, 100 * fkp / fk, 100 * fko / fk, 100 * fkg / fk, 100 * fkg / fks, 100 * sum(both(B[s], "fkKicked") for s in B) / fk))
nofoulgoals_A = statistics.mean(both(A[s], "goals") - both(A[s], "dgGoal") for s in A); nofoulgoals_B = statistics.mean(both(B[s], "goals") - both(B[s], "dgGoal") for s in B)
print("goals OUTSIDE the 10 s after a foul: A %.2f B %.2f (inside: A %.2f B %.2f)" % (nofoulgoals_A, nofoulgoals_B, statistics.mean(both(A[s], "dgGoal") for s in A), statistics.mean(both(B[s], "dgGoal") for s in B)))

print()
print("# 3. noise scale and placebo (Professional, seeds 6001-6500, free kick ON in all three; tiny changes of unrelated geometry)")
base = cells("on_pro_6000", "Professional"); p1 = cells("placebo1_pro_6000", "Professional"); p2 = cells("placebo2_pro_6000", "Professional"); off = cells("off_pro_6000", "Professional")
for name, a in (("A  free kick off", off), ("B  free kick on", base), ("P1 on, taker offset 1.0 -> 1.1", p1), ("P2 on, wall distance 4.0 -> 4.3", p2)):
    g = [both(a[s], "goals") for s in sorted(a)]
    print("%-34s goals %.2f (SE %.2f)  fouls %.2f  free kicks %.2f" % (name, statistics.mean(g), statistics.stdev(g) / math.sqrt(len(g)), statistics.mean(both(a[s], "fouls") for s in a), statistics.mean(both(a[s], "fkTaken") for s in a)))
for x, y, nm in ((base, p1, "B vs P1"), (base, p2, "B vs P2"), (off, base, "A vs B")):
    same = sum(1 for s in x if both(x[s], "goals") == both(y[s], "goals") and both(x[s], "shotsRated") == both(y[s], "shotsRated"))
    print("  %-8s matches that came out identical: %d of %d; sd of the per-match goal difference %.2f" % (nm, same, len(x), statistics.pstdev([both(y[s], "goals") - both(x[s], "goals") for s in x])))

print()
print("# 4. wall removed (WallMaxPlayers=0), same seeds")
for level, d, on, offd in (("Professional", "nowall_pro_6000", "on_pro_6000", "off_pro_6000"), ("Novice", "nowall_no_6000", "on_ne_6000", "off_ne_6000")):
    nw = cells(d, level); o = cells(on, level); f = cells(offd, level)
    if not nw: continue
    S = sorted(set(nw) & set(o) & set(f))
    print("%-13s n=%d  goals off %.2f  on %.2f  no-wall %.2f   fkGoal on %.2f no-wall %.2f   fkShot on %.2f no-wall %.2f" % (level, len(S), statistics.mean(both(f[s], "goals") for s in S), statistics.mean(both(o[s], "goals") for s in S), statistics.mean(both(nw[s], "goals") for s in S),
          statistics.mean(both(o[s], "fkGoal") for s in S), statistics.mean(both(nw[s], "fkGoal") for s in S), statistics.mean(both(o[s], "fkShot") for s in S), statistics.mean(both(nw[s], "fkShot") for s in S)))

print()
print("# 5. Arena (control): free kick tuning cannot change it")
for blk in ("5001", "5101"):
    a = list(csv.DictReader(open(os.path.join(HERE, "..", "REFEREE_CARDS_PHASE21_data", "after_%s" % blk, "Arena_Professional_vs_Professional.csv")))); b = list(csv.DictReader(open(os.path.join(HERE, "arena", "Arena_Professional_%s.csv" % blk))))
    ks = [k for k in a[0] if k in b[0]]
    print("Arena Professional %s: %d shared columns x %d matches, %d differing values; fouls %g free kicks %g stopped steps %g" % (blk, len(ks), len(a), sum(1 for x, y in zip(a, b) for k in ks if x[k] != y[k]), sum(both(r, "fouls") for r in b), sum(both(r, "fkTaken") for r in b), sum(float(r["h_dgStopSteps"]) for r in b)))
