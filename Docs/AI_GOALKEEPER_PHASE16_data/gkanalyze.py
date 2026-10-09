"""Phase 16 analysis of the battery CSVs (Phase15Battery format, with the Phase 16 columns). Reads a directory of <Mode>_<A>_vs_<B>.csv files."""
import csv,glob,os,sys,math,random,statistics
random.seed(2024)
D=sys.argv[1]
def boot(xs,n=2000):
    xs=list(xs)
    if len(xs)<2: return (float('nan'),float('nan'))
    m=[]
    for _ in range(n):
        s=[xs[random.randrange(len(xs))] for _ in xs]; m.append(sum(s)/len(s))
    m.sort(); return m[int(.025*n)],m[int(.975*n)]
LV=["Novice","Amateur","Professional","Expert","Elite"]
for mode in ["Arena","Classic"]:
    print("== %s ==" % mode)
    # keeper view: for each cell L vs Professional, the keeper of L faces the shots of Professional
    print("%-12s %5s | keeper of this level faces: shots goals conv%%  saves save%%  xG  goals/xG | level's own score vs Professional (W/D/L, rate [95%%CI]) | goals per match (own/against)"%("level","n"))
    for L in LV:
        f=os.path.join(D,"%s_%s_vs_Professional.csv"%(mode,L))
        if not os.path.exists(f): continue
        rows=list(csv.DictReader(open(f)))
        shots=goals=saves=0; xg=0.0; sc=[]; w=d=l=0; gf=ga=0.0; ownshots=owngoals=0; ownxg=0.0
        for r in rows:
            lh = r["levelHome"]==L if L!="Professional" else r["aIsHome"]=="1"
            me,op=("h_","a_") if lh else ("a_","h_")
            shots+=float(r[op+"shotsRated"]); goals+=float(r[me+"goals"] if False else r[op+"goals"]); 
            xg+=float(r[op+"xgSum"]); saves+=float(r[me+"saves"])
            ownshots+=float(r[me+"shotsRated"]); owngoals+=float(r[me+"goals"]); ownxg+=float(r[me+"xgSum"])
            a,b=float(r[me+"goals"]),float(r[op+"goals"]); gf+=a; ga+=b
            sc.append(1.0 if a>b else .5 if a==b else 0.0); w+=a>b; d+=a==b; l+=a<b
        n=len(rows); lo,hi=boot(sc)
        print("%-12s %5d | %5d %5d %5.1f %5d %5.1f %6.1f %6.2f | %d/%d/%d %.3f [%.3f,%.3f] | %.2f / %.2f"%(L,n,shots,goals,100*goals/shots,saves,100*saves/shots,xg,goals/xg,w,d,l,sum(sc)/n,lo,hi,gf/n,ga/n))
    f=os.path.join(D,"%s_Professional_vs_Professional.csv"%mode)
