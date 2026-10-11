"""Phase 22: free-kick summary of a quick-battery directory (reads the fk* columns of every cell csv).
usage: python3 -I fk_summary.py OUTDIR"""
import csv, glob, os, sys

def main():
    out = sys.argv[1]
    for f in sorted(glob.glob(os.path.join(out, "*_vs_*.csv"))):
        if f.endswith("_shots.csv") or f.endswith("_goals.csv"): continue
        rows = list(csv.DictReader(open(f)))
        n = len(rows)
        def tot(k): return sum(float(r.get("h_" + k, 0) or 0) + float(r.get("a_" + k, 0) or 0) for r in rows)
        fk = tot("fkTaken")
        fouls = tot("fouls")
        print("%-45s matches %2d  goals/match %.2f  fouls/match %.2f  free kicks/match %.2f  kicked-by-taker %s  shots %s  passes %s  other %s  goals<=8s %s  wall players/kick %s" % (
            os.path.basename(f)[:-4], n, (tot("goals")) / n, fouls / n, fk / n,
            "%.0f%%" % (100 * tot("fkKicked") / fk) if fk else "-", "%d" % tot("fkShot"), "%d" % tot("fkPass"), "%d" % tot("fkOther"), "%d" % tot("fkGoal"),
            "%.2f" % (tot("fkWall") / fk) if fk else "-"))

main()
