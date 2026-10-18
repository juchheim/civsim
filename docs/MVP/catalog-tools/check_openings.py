import sys
import json
P=json.load(open(sys.argv[1] if len(sys.argv)>1 else 'graph.json'))
BREADTH={1:13,2:14}
seqs={
'Grower':"FOR-01 SOC-01 FIR-01 FOD-01 SOC-02 STN-01 WOD-01 FOR-02 FIB-01 COM-01 COM-02 ART-01 ART-02 STN-02 FIR-02 FIR-03 STN-03 STN-04 WAY-01 KEY-1 "
         "SOC-03 WAY-02 FIB-05 FOR-03 STN-05 FOD-02 STN-06 FOR-04 FOD-03 MED-01 FOR-05 FOR-06 FIB-02 BLD-01 WOD-02 BLD-02 BLD-03 LRN-01 LRN-02 KEY-2",
'Builder':"STN-01 FIR-01 FOR-01 STN-02 WOD-01 FIB-01 FIR-02 STN-03 FIR-03 FIR-04 BLD-01 STN-04 FIB-02 COM-01 COM-02 SOC-01 KEY-1 "
          "STN-05 FIR-06 STN-06 CHM-01 CHM-02 ART-01 WOD-02 MEC-01 BLD-02 BLD-03 FIB-03 FIB-05 MIN-01 FOR-02 WAY-01 WAY-02 FOR-03 FOR-04 FOD-01 FOD-02 FOD-03 KEY-2",
'Wanderer':"WAY-01 COM-01 HNT-01 TER-01 DIP-01 COM-02 STN-01 FIR-01 FOR-01 SOC-01 STN-02 FIB-01 FIR-02 STN-03 STN-04 KEY-1 "
           "WOD-01 FOR-02 WAY-02 TER-02 INQ-02 WAY-03 SEA-01 HNT-02 HNT-03 INQ-01 HNT-04 HUS-01 TRN-01 TRN-02 SOC-02 SOC-03 DIP-02 FIB-05 FOR-03 STN-05 STN-06 FOR-04 FIR-03 FOD-01 FOD-02 FOD-03 FIB-02 BLD-01 BLD-02 ART-01 WOD-02 BLD-03 KEY-2",
}
for name,s in seqs.items():
    done=[];era=0;ok=True
    for k in s.split():
        p=P[k]
        if p['era']>era and not k.startswith('KEY'): print(name,"ERA violation",k); ok=False
        miss=[r for r in p['req'] if r not in done]+([] if not p['any'] or any(r in done for r in p['any']) else p['any'])
        if miss: print(name,k,"missing",miss); ok=False
        if k.startswith('KEY'):
            cnt=sum(1 for d in done if P[d]['era']==era)
            need=BREADTH[era+1]
            print(f"{name} {k}: era-{era} projects done={cnt} (breadth need {need}) {'OK' if cnt>=need else 'SHORT'}")
            era+=1
        done.append(k)
    print(name,"valid" if ok else "INVALID", "total",len(done))
