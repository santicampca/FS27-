"""Phase 15 analysis. Reads the per-match CSVs of one battery directory and prints, per mode and per comparison, the level under test (A) against its opponent (B):
mean, sd, median, range and a 95% confidence interval (bootstrap, fixed seed) of the paired difference A-B, plus W/D/L. Every match is a row with both sides, so each metric is read for A and for B."""
import csv, sys, os, glob, random, statistics, math, json
random.seed(12345)
DIR = sys.argv[1]
LEVELS = ["Novice","Amateur","Professional","Expert","Elite"]

def rows(path):
    return list(csv.DictReader(open(path)))

def side(r, a_is_side_a, col):
    # returns (valueA, valueB) of column `col` (without the h_/a_ prefix)
    h = float(r["h_"+col]); a = float(r["a_"+col])
    return (h, a) if a_is_side_a else (a, h)

def a_is_home(r, A):
    return r["levelHome"] == A

def boot_ci(xs, n=2000):
    xs = [x for x in xs if x == x]
    if len(xs) < 2: return (float('nan'), float('nan'))
    means = []
    for _ in range(n):
        s = [xs[random.randrange(len(xs))] for _ in xs]
        means.append(sum(s)/len(s))
    means.sort()
    return (means[int(0.025*n)], means[int(0.975*n)])

def summ(xs):
    xs = [x for x in xs if x == x]
    if not xs: return None
    return dict(n=len(xs), mean=sum(xs)/len(xs), sd=statistics.pstdev(xs) if len(xs)>1 else 0.0, med=statistics.median(xs), lo=min(xs), hi=max(xs))

def cell_files():
    return sorted(glob.glob(os.path.join(DIR, "*.csv")))

def parse_name(path):
    b = os.path.basename(path)[:-4]       # Arena_Novice_vs_Professional
    m, rest = b.split("_",1); a, bb = rest.split("_vs_")
    return m, a, bb

RAW = ["goals","shotsChosen","clearChances","clearTaken","passAtt","passDone","lost","recoveries","fouls","yellow","stamMean","homeDistAtt","homeDistDef","sprintDecisions","decisions"]
Q = ["PassCompletion","ShotsOnTargetShare","ShotTakeRate","ClearChancesPer100Possessions","FinalThirdEntriesPerPossession","LossesPerPossession","ChancesConcededPer100OpponentPossessions","DangerConcededPerOpponentPossession","RecoveriesPerOpponentPossession","InterceptionsPerOpponentPossession","ContestSuccess","ErrorRate","ErrorRateUnderPressure","ErrorRateWithLittleSpace","ErrorRateWhenTired","MeanMargin","HomeDistanceAttacking","HomeDistanceDefending","OffBallAttackShare","DefensiveActionShare"]

def analyse(path, full=False):
    m, A, B = parse_name(path)
    rs = rows(path)
    out = {"mode": m, "A": A, "B": B, "matches": len(rs)}
    w = d = l = 0; gd = []; gA = []; gB = []
    for r in rs:
        ah = a_is_home(r, A) if A != B else r["aIsHome"] == "1"
        ga, gb = side(r, ah, "goals")
        gA.append(ga); gB.append(gb); gd.append(ga-gb)
        if ga>gb: w+=1
        elif ga==gb: d+=1
        else: l+=1
    out["W"],out["D"],out["L"] = w,d,l
    out["goals"] = {"A": summ(gA), "B": summ(gB), "diff": summ(gd), "diffCI": boot_ci(gd)}
    # score rate with draws as half
    sr = [ (1.0 if x>0 else 0.5 if x==0 else 0.0) for x in gd ]
    out["score"] = {"mean": sum(sr)/len(sr), "ci": boot_ci(sr)}
    metrics = {}
    for c in RAW:
        va=[];vb=[]
        for r in rs:
            ah = a_is_home(r, A) if A != B else r["aIsHome"] == "1"
            x,y = side(r, ah, c); va.append(x); vb.append(y)
        diff=[x-y for x,y in zip(va,vb) if x==x and y==y]
        metrics[c] = {"A": summ(va), "B": summ(vb), "diff": summ(diff), "diffCI": boot_ci(diff)}
    for c in Q:
        va=[];vb=[]
        for r in rs:
            ah = a_is_home(r, A) if A != B else r["aIsHome"] == "1"
            x,y = side(r, ah, "q_"+c); va.append(x); vb.append(y)
        diff=[x-y for x,y in zip(va,vb) if x==x and y==y]
        metrics["q_"+c] = {"A": summ(va), "B": summ(vb), "diff": summ(diff), "diffCI": boot_ci(diff)}
    out["metrics"] = metrics
    return out

if __name__ == "__main__":
    res = [analyse(p) for p in cell_files()]
    json.dump(res, open(os.path.join(DIR, "analysis.json"), "w"), indent=1)
    for r in res:
        g = r["goals"]; dd = g["diffCI"]
        print("%-8s %-12s vs %-12s n=%3d  W%d/D%d/L%d  score %.3f [%.3f,%.3f]  goals A %.2f B %.2f diff %+.2f [%+.2f,%+.2f]" % (
            r["mode"], r["A"], r["B"], r["matches"], r["W"], r["D"], r["L"], r["score"]["mean"], r["score"]["ci"][0], r["score"]["ci"][1], g["A"]["mean"], g["B"]["mean"], g["diff"]["mean"], dd[0], dd[1]))
