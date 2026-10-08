import csv,collections,sys
agg=collections.defaultdict(lambda:[0,0,0,0,0,0])
for r in csv.DictReader(open(sys.argv[1])):
    a=agg[(r["mode"],r["level"],r["situation"])]
    for i,c in enumerate(["carrierDecisions","risky","safe","dribble","shots","passes"]): a[i]+=float(r[c])
print("risky share (forward/through/shot) trailing | level | leading ; swing = trailing-leading   ;  safe share (pass/hold/protect) trailing | level | leading")
for mode in ["Arena","Classic"]:
  for lv in ["Novice","Amateur","Professional","Expert","Elite"]:
    R=[100*agg[(mode,lv,s)][1]/agg[(mode,lv,s)][0] for s in ["trailing0-2","level0-0","leading2-0"]]
    S=[100*agg[(mode,lv,s)][2]/agg[(mode,lv,s)][0] for s in ["trailing0-2","level0-0","leading2-0"]]
    print("%-7s %-12s risky %5.1f %5.1f %5.1f swing %+5.1f | safe %5.1f %5.1f %5.1f swing %+5.1f"%(mode,lv,R[0],R[1],R[2],R[0]-R[2],S[0],S[1],S[2],S[2]-S[0]))
