import csv,glob,os,sys,statistics as st
def cell(path):
    rs=list(csv.DictReader(open(path))); n=len(rs)
    s=lambda k:sum(float(r["h_"+k])+float(r["a_"+k]) for r in rs)
    g=[float(r["h_goals"])+float(r["a_goals"]) for r in rs]
    return dict(n=n,goals=st.mean(g),fouls=s("fouls")/n,yellow=s("yellow")/n,red=s("red")/n,contacts=s("contacts")/n,tackles=s("tackles")/n,slides=s("slides")/n,challenges=s("challenges")/n,
      passAtt=s("passAtt")/n,passRate=s("passDone")/s("passAtt"),poss=s("possessions")/n,shots=s("shotsChosen")/n,clear=s("clearChances")/n,mist=s("mistakes")/s("carrierDecisions"),interceptions=s("interceptions")/n)
def load(d):
    out={}
    for f in sorted(glob.glob(d+"/*_vs_*.csv")):
        if f.endswith("_shots.csv") or f.endswith("_goals.csv"): continue
        out[os.path.basename(f)[:-4]]=cell(f)
    return out
def comb(dirs):
    acc={}
    for d in dirs:
        for k,v in load(d).items(): acc.setdefault(k,[]).append(v)
    res={}
    for k,vs in acc.items():
        n=sum(x["n"] for x in vs); res[k]={m:sum(x[m]*x["n"] for x in vs)/n for m in vs[0] if m!="n"}; res[k]["n"]=n
    return res
if __name__=="__main__":
    a=comb(sys.argv[1].split(",")); b=comb(sys.argv[2].split(",")) if len(sys.argv)>2 else {}
    keys=["goals","fouls","yellow","red","contacts","tackles","slides","challenges","passAtt","passRate","poss","shots","clear","mist"]
    for name in sorted(a):
        if "Classic" not in name: continue
        print(name, "n=%d"%a[name]["n"])
        for k in keys: print("   %-10s %9.3f %s"%(k,a[name][k],"" if name not in b else "(before %.3f, %+.3f)"%(b[name][k],a[name][k]-b[name][k])))
