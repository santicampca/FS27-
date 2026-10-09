import csv,math,sys
F=["distance","angle","pressure","keeperPos","foot","speed"]
def load(paths):
    rows=[]
    for p in paths:
        for r in csv.DictReader(open(p)): rows.append(r)
    return rows
def solve(A,b):
    n=len(b); M=[A[i][:]+[b[i]] for i in range(n)]
    for i in range(n):
        piv=max(range(i,n),key=lambda r:abs(M[r][i])); M[i],M[piv]=M[piv],M[i]
        for r in range(i+1,n):
            f=M[r][i]/M[i][i]
            for c in range(i,n+1): M[r][c]-=f*M[i][c]
    x=[0]*n
    for i in range(n-1,-1,-1): x[i]=(M[i][n]-sum(M[i][c]*x[c] for c in range(i+1,n)))/M[i][i]
    return x
def fit_multi(X,y,it=40,ridge=1e-3):
    k=len(X[0])+1; w=[0.0]*k
    for _ in range(it):
        g=[0.0]*k; H=[[ridge if i==j else 0.0 for j in range(k)] for i in range(k)]
        for xi,yi in zip(X,y):
            f=[1.0]+xi; z=sum(a*b for a,b in zip(w,f)); p=1/(1+math.exp(-z)); wt=p*(1-p)
            for i in range(k):
                g[i]+=(yi-p)*f[i]
                for j in range(k): H[i][j]+=wt*f[i]*f[j]
        d=solve(H,g); w=[a+b for a,b in zip(w,d)]
        if sum(abs(x) for x in d)<1e-9: break
    return w
def fit1(xs,ys,it=60):
    a,b=-3.0,5.0
    for _ in range(it):
        g=[0,0]; H=[[0,0],[0,0]]
        for x,y in zip(xs,ys):
            p=1/(1+math.exp(-(a+b*x))); w=p*(1-p); g[0]+=y-p; g[1]+=(y-p)*x; H[0][0]+=w; H[0][1]+=w*x; H[1][1]+=w*x*x
        H[1][0]=H[0][1]; det=H[0][0]*H[1][1]-H[0][1]*H[1][0]
        da=(H[1][1]*g[0]-H[0][1]*g[1])/det; db=(-H[1][0]*g[0]+H[0][0]*g[1])/det; a+=da;b+=db
        if abs(da)+abs(db)<1e-9: break
    return a,b
train=load(sys.argv[1:3]); test=load(sys.argv[3:5])
on=[r for r in train if float(r["aim"])>0]
X=[[float(r[f]) for f in F] for r in on]; y=[int(r["goal"]) for r in on]
w=fit_multi(X,y); print("multivariate logistic (on target shots, training):",dict(zip(["intercept"]+F,[round(v,3) for v in w])))
# weights = positive coefficients; value = sum(w*part)/sum(w) * aim
pos=[max(c,0.0) for c in w[1:]]; s=sum(pos); weights=[c/s for c in pos]
print("weights (normalised positive coefficients):",dict(zip(F,[round(v,3) for v in weights])))
def value(r): return sum(wi*float(r[f]) for wi,f in zip(weights,F))*float(r["aim"])
xs=[value(r) for r in train]; ys=[int(r["goal"]) for r in train]
a,b=fit1(xs,ys); print("1-D refit: intercept %.3f slope %.3f"%(a,b))
def rep(name,rows):
    xv=[value(r) for r in rows]; yv=[int(r["goal"]) for r in rows]
    xg=sum(1/(1+math.exp(-(a+b*x))) for x in xv); g=sum(yv); n=len(yv)
    ll=-sum(yy*math.log(1/(1+math.exp(-(a+b*x))))+(1-yy)*math.log(1-1/(1+math.exp(-(a+b*x)))) for x,yy in zip(xv,yv))/n
    base=g/n; ll0=-(base*math.log(base)+(1-base)*math.log(1-base))
    print("%-22s shots %5d goals %4d xG %.1f goals/xG %.2f logloss %.4f (constant %.4f)"%(name,n,g,xg,g/xg,ll,ll0))
rep("training",train); rep("held-out",test)
for mode in ["Arena","Classic"]:
    rep("held-out "+mode,[r for r in test if r["mode"]==mode])

print("\n=== chosen weights with a mode intercept ===")
W=dict(distance=0.10,angle=0.45,pressure=0.10,keeperPos=0.30,foot=0.03,speed=0.02)
def val2(r): return sum(W[f]*float(r[f]) for f in F)/sum(W.values())*float(r["aim"])
def fit_mode(rows,it=60):
    # logistic: z = a + d*[Classic] + b*value
    a,d,b=-4.0,-0.5,6.0
    for _ in range(it):
        g=[0,0,0]; H=[[0]*3 for _ in range(3)]
        for r in rows:
            x=val2(r); c=1.0 if r["mode"]=="Classic" else 0.0; y=int(r["goal"])
            f=[1.0,c,x]; z=a*f[0]+d*f[1]+b*f[2]; p=1/(1+math.exp(-z)); w=p*(1-p)
            for i in range(3):
                g[i]+=(y-p)*f[i]
                for j in range(3): H[i][j]+=w*f[i]*f[j]
        s=solve(H,g); a+=s[0]; d+=s[1]; b+=s[2]
        if sum(abs(v) for v in s)<1e-9: break
    return a,d,b
a2,d2,b2=fit_mode(train); print("intercept %.3f classic-offset %.3f slope %.3f"%(a2,d2,b2))
def rep2(name,rows):
    xv=[1/(1+math.exp(-(a2+d2*(1.0 if r["mode"]=="Classic" else 0.0)+b2*val2(r)))) for r in rows]; yv=[int(r["goal"]) for r in rows]
    n=len(yv); g=sum(yv); xg=sum(xv)
    ll=-sum(yy*math.log(p)+(1-yy)*math.log(1-p) for p,yy in zip(xv,yv))/n; base=g/n; ll0=-(base*math.log(base)+(1-base)*math.log(1-base))
    print("%-22s shots %5d goals %4d xG %.1f goals/xG %.2f logloss %.4f (constant %.4f)"%(name,n,g,xg,g/xg,ll,ll0))
rep2("training",train); rep2("held-out",test)
for mode in ["Arena","Classic"]: rep2("held-out "+mode,[r for r in test if r["mode"]==mode])
