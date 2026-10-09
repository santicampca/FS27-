import csv,glob,math,sys
exec(open(sys.argv[1]).read().split("train=load")[0])
def rows(d,mode=None):
    out=[]
    for p in glob.glob(d+"/*_shots.csv"):
        for r in csv.DictReader(open(p)):
            if mode is None or r["mode"]==mode: out.append(r)
    return out
def ll(rs,a,b,off=0):
    s=0
    for r in rs:
        p=1/(1+math.exp(-(a+off+b*float(r["value"])))); y=int(r["goal"]); s-=math.log(p if y else 1-p)
    return s/len(rs)
tr=rows(sys.argv[2]); va=rows(sys.argv[3])
print("n train",len(tr),"val",len(va))
a,b=fit1([float(r["value"]) for r in tr if r["mode"]=="Arena"],[int(r["goal"]) for r in tr if r["mode"]=="Arena"])
print("Arena fit a,b",a,b)
ac,bc=fit1([float(r["value"]) for r in tr if r["mode"]=="Classic"],[int(r["goal"]) for r in tr if r["mode"]=="Classic"])
print("Classic fit",ac,bc)
a2,b2=fit1([float(r["value"]) for r in tr],[int(r["goal"]) for r in tr]); print("pooled",a2,b2)
for m in ("Arena","Classic"):
    v=[r for r in va if r["mode"]==m]
    g=sum(int(r["goal"]) for r in v); 
    cur=sum(float(r["xg"]) for r in v)
    aa,bb=(a,b) if m=="Arena" else (ac,bc)
    new=sum(1/(1+math.exp(-(aa+bb*float(r["value"])))) for r in v)
    pool=sum(1/(1+math.exp(-(a2+(0 if m=="Arena" else (ac-a))+b2*float(r["value"])))) for r in v)
    print(m,"val shots",len(v),"goals",g,"xG current",round(cur,1),"xG refit",round(new,1))
