import json,sys
res=json.load(open(sys.argv[1]+"/analysis.json"))
keys=sys.argv[2].split(",")
order=["Novice","Amateur","Professional","Expert","Elite"]
for mode in ["Arena","Classic"]:
    print("====",mode,"(A level vs opponent; value = mean of A [mean of B]; diff A-B with 95%CI; * = CI excludes 0)")
    for r in sorted([x for x in res if x["mode"]==mode], key=lambda x:(x["B"]!="Professional", order.index(x["A"]))):
        print("%s vs %s"%(r["A"],r["B"]))
        for k in keys:
            m=r["metrics"].get(k)
            if not m: continue
            d=m["diff"]; ci=m["diffCI"]
            star="*" if (ci[0]>0 or ci[1]<0) else " "
            print("   %-44s A %8.3f B %8.3f  diff %+8.3f [%+8.3f,%+8.3f] %s"%(k,m["A"]["mean"],m["B"]["mean"],d["mean"],ci[0],ci[1],star))
