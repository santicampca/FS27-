import csv,sys,statistics as st
def summ(path):
    rs=list(csv.DictReader(open(path)))
    if not rs or "h_goals" not in rs[0]: return None
    n=len(rs)
    def m(k): return sum(float(r[k]) for r in rs)/n
    g=[float(r["h_goals"])+float(r["a_goals"]) for r in rs]
    out={"n":n,"goals":sum(g)/n,"goals_sd":st.pstdev(g)}
    for s in ("h_","a_"):
        pass
    for q in ("stam1","stam2","stam3","stam4","stamMean"):
        out[q]=(m("h_"+q)+m("a_"+q))/2
    out["shots"]=(m("h_shotsRated")+m("a_shotsRated"))
    out["sprintDec"]=(m("h_sprintDecisions")+m("a_sprintDecisions"))/2
    out["decisions"]=(m("h_decisions")+m("a_decisions"))/2
    out["kStam"]=(m("h_keeperStamina")+m("a_keeperStamina"))/2; out["low20"]=(m("h_stamLow20")+m("a_stamLow20"))/2; out["low5"]=(m("h_stamLow5")+m("a_stamLow5"))/2
    out["saves"]=m("h_saves")+m("a_saves")
    out["misTired"]=(m("h_misTired")+m("a_misTired"))/2; out["decTired"]=(m("h_decTired")+m("a_decTired"))/2
    return out
for p in [a for a in sys.argv[1:] if not a.endswith('_shots.csv')]:
    o=summ(p)
    if o is None: continue
    print(p.split("/")[-1], " ".join("%s=%.2f"%(k,v) for k,v in o.items()))
