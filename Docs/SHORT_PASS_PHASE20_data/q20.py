import csv,glob,os,sys,statistics as st
def cell(path):
    rs=list(csv.DictReader(open(path))); n=len(rs)
    s=lambda k:sum(float(r["h_"+k])+float(r["a_"+k]) for r in rs)
    g=[float(r["h_goals"])+float(r["a_goals"]) for r in rs]
    return dict(n=n,goals=st.mean(g),sd=st.pstdev(g),passAtt=s("passAtt")/n,passDone=s("passDone")/n,passRate=s("passDone")/s("passAtt"),intercepted=s("passIntercepted")/n,
      clear=s("clearChances")/n,shots=s("shotsChosen")/n,onTarget=(s("shotsGoal")+s("shotsStopped"))/n,poss=s("possessions")/n,lost=s("lost")/n,mist=s("mistakes")/s("carrierDecisions"),
      fouls=s("fouls")/n,tackles=s("tackles")/n,finalThird=s("finalThird")/n,dangerEntries=s("dangerEntries")/n,stam=(s("stamMean")/n/2))
def load(d):
    out={}
    for f in sorted(glob.glob(d+"/*_vs_*.csv")):
        if f.endswith("_shots.csv") or f.endswith("_goals.csv"): continue
        out[os.path.basename(f)[:-4]]=cell(f)
    return out
if __name__=="__main__":
    a=load(sys.argv[1]); b=load(sys.argv[2]) if len(sys.argv)>2 else {}
    keys=["goals","passAtt","passDone","passRate","intercepted","clear","shots","onTarget","poss","lost","mist","fouls","tackles","finalThird","dangerEntries","stam"]
    for name,c in a.items():
        print(name)
        for k in keys:
            v=c[k]; w=b.get(name,{}).get(k)
            print("   %-14s %9.3f %s"%(k,v,("" if w is None else "(before %.3f, %+.3f)"%(w,v-w))))
