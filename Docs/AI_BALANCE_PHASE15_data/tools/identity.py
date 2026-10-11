"""Team identity across levels: for each team, its decision mix (shares of the carrier decisions and of all decisions) at each level; Spearman rank correlation of the team ordering of Professional vs each level."""
import csv,glob,sys,os,statistics
d=sys.argv[1]
mixcols=["forwardChosen","throughChosen","dribbleChosen","protectChosen","holdChosen","shotsChosen"]
def rank(v):
    o=sorted(range(len(v)),key=lambda i:v[i]); r=[0]*len(v)
    for k,i in enumerate(o): r[i]=k
    return r
def spearman(a,b):
    ra,rb=rank(a),rank(b); n=len(a); ma=sum(ra)/n; mb=sum(rb)/n
    num=sum((x-ma)*(y-mb) for x,y in zip(ra,rb)); den=(sum((x-ma)**2 for x in ra)*sum((y-mb)**2 for y in rb))**.5
    return num/den if den else float('nan')
data={}   # (mode, level, team, metric) -> [sum_num, sum_den]
for f in glob.glob(d+"/*.csv"):
    for r in csv.DictReader(open(f)):
        for side,pfx,lv,tm in (("h","h_",r["levelHome"],r["teamA"]),("a","a_",r["levelAway"],r["teamB"])):
            cd=float(r[pfx+"carrierDecisions"]); dec=float(r[pfx+"decisions"])
            for c in mixcols:
                k=(r["mode"],lv,tm,c); e=data.setdefault(k,[0.0,0.0]); e[0]+=float(r[pfx+c]); e[1]+=cd
            k=(r["mode"],lv,tm,"defensiveShare"); e=data.setdefault(k,[0.0,0.0]); e[0]+=float(r[pfx+"defensiveDecisions"]); e[1]+=dec
            k=(r["mode"],lv,tm,"sprintShare"); e=data.setdefault(k,[0.0,0.0]); e[0]+=float(r[pfx+"sprintDecisions"]); e[1]+=dec
levels=["Novice","Amateur","Professional","Expert","Elite"]
for mode in ["Arena","Classic"]:
    print("==",mode)
    for c in mixcols+["defensiveShare","sprintShare"]:
        teams=sorted({k[2] for k in data if k[0]==mode},key=int)
        series={}
        for lv in levels:
            vals=[]
            for t in teams:
                e=data.get((mode,lv,t,c))
                vals.append(e[0]/e[1] if e and e[1]>0 else float('nan'))
            series[lv]=vals
        ref=series["Professional"]
        out=[]
        for lv in levels:
            v=series[lv]
            ok=[(x,y) for x,y in zip(ref,v) if x==x and y==y]
            rho=spearman([x for x,_ in ok],[y for _,y in ok]) if len(ok)>3 else float('nan')
            sd=statistics.pstdev([y for _,y in ok]) if ok else float('nan')
            out.append("%s sd %.3f rho %.2f"%(lv[:3],sd,rho))
        print("%-16s"%c," | ".join(out))
