import sys
import json,collections
P=json.load(open(sys.argv[1] if len(sys.argv)>1 else 'graph.json'))
def anc(n,acc=None,choose_any=True):
    acc=set() if acc is None else acc
    p=P[n]
    reqs=list(p['req'])
    if p['any']:
        # choose the any-of option with the smallest ancestry
        best=min(p['any'],key=lambda a: len(anc(a)))
        reqs.append(best)
    for r in reqs:
        if r not in acc:
            acc.add(r); anc(r,acc)
    return acc
print("Minimum prerequisite closure per keystone (projects, incl. earlier eras):")
for k in [f"KEY-{i}" for i in range(1,11)]:
    a=anc(k); e=P[k]['era']
    in_era=[x for x in a if P[x]['era']==e]
    print(k,P[k]['name'],"total",len(a),"in-era",len(in_era))
for k in [f"KEY-{i}" for i in range(1,11)]:
    a=anc(k); e=P[k]['era']
    print(k, sorted([x for x in a if P[x]['era']==e]))
