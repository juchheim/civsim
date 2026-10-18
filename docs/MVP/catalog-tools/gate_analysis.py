import sys
import json
P=json.load(open(sys.argv[1] if len(sys.argv)>1 else 'graph.json'))
seen_gates=sorted({g for p in P.values() for g in p['gates'] if g.startswith('Seen')})
def reachable(blocked):
    done=set(); changed=True
    while changed:
        changed=False
        for k,p in P.items():
            if k in done or any(g in p['gates'] for g in blocked): continue
            if all(r in done for r in p['req']) and (not p['any'] or any(r in done for r in p['any'])):
                done.add(k); changed=True
    return done
for g in seen_gates:
    r=reachable([g]); lost=[k for k in P if k.startswith('KEY') and k not in r]
    print(f"{g:30s} blocks: {lost[:1] or '-'}  lost {len(P)-len(r)}")
r=reachable(seen_gates); print("ALL seen gates blocked -> lost",len(P)-len(r), [k for k in P if k.startswith('KEY') and k not in r][:1])
