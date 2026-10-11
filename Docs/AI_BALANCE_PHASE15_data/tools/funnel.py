import json,sys,csv,glob,os
d=sys.argv[1]
order=["Novice","Amateur","Professional","Expert","Elite"]
for mode in ["Arena","Classic"]:
    print("==",mode)
    for lvl in order+["Novice_vs_Elite"]:
        f = d+"/%s_%s_vs_Professional.csv"%(mode,lvl) if lvl!="Novice_vs_Elite" else d+"/%s_Novice_vs_Elite.csv"%mode
        if not os.path.exists(f): continue
        rs=list(csv.DictReader(open(f)))
        A = lvl if lvl!="Novice_vs_Elite" else "Novice"
        tot={}
        for r in rs:
            ah = (r["levelHome"]==A) if (lvl!="Professional") else (r["aIsHome"]=="1")
            p = "h_" if ah else "a_"
            for c in ["shotsAvail","shotsChosen","shotActions","shotsGoal","shotsStopped","shotsOut","clearChances","clearTaken","possessions","carrierDecisions","passChosen","forwardChosen","throughChosen","dribbleChosen","protectChosen","holdChosen","goals"]:
                tot[c]=tot.get(c,0)+float(r[p+c])
        n=len(rs)
        print("%-16s per match: poss %.1f carrierDec %.0f | shotAvail %.1f chosen %.2f (take %.1f%%) goals %.2f conv %.1f%% | clear %.2f taken %.2f (%.0f%%) | mix pass %.0f%% fwd %.0f%% thr %.1f%% drib %.0f%% prot %.0f%% hold %.0f%%"%(
            lvl, tot["possessions"]/n, tot["carrierDecisions"]/n, tot["shotsAvail"]/n, tot["shotsChosen"]/n, 100*tot["shotsChosen"]/max(1,tot["shotsAvail"]), tot["goals"]/n, 100*tot["shotsGoal"]/max(1,tot["shotActions"]),
            tot["clearChances"]/n, tot["clearTaken"]/n, 100*tot["clearTaken"]/max(1,tot["clearChances"]),
            100*tot["passChosen"]/tot["carrierDecisions"],100*tot["forwardChosen"]/tot["carrierDecisions"],100*tot["throughChosen"]/tot["carrierDecisions"],100*tot["dribbleChosen"]/tot["carrierDecisions"],100*tot["protectChosen"]/tot["carrierDecisions"],100*tot["holdChosen"]/tot["carrierDecisions"]))
