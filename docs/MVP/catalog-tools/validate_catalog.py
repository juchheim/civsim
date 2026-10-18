import re,sys,collections,json
base=sys.argv[1]
files={'CRAFT':'03a-research-craft.md','KIN':'03b-research-kin.md','REACH':'03c-research-reach.md'}
P={}
for theme,f in files.items():
    for line in open(f"{base}/{f}"):
        line=line.replace('\\|','¦')
        m=re.match(r'\|\s*((?:[A-Z]{3})-\d+)\s*\|\s*([^|]+?)\s*\|\s*(E\d+|Apex)\s*\|\s*\w\s*\|\s*([^|]*?)\s*\|',line)
        if not m:
            m=re.match(r'\|\s*(KEY-\d+)\s*\|\s*([^|]+?)\s*\|\s*(E\d+|Apex)\s*\|\s*K\s*\|\s*([^|]*?)\s*\|',line)
        if m:
            pid,name,era,req=m.groups()
            typ=re.match(r'\|[^|]*\|[^|]*\|[^|]*\|\s*(\w)\s*\|',line).group(1)
            ids=re.findall(r'\b([A-Z]{3}-\d+)\b',re.sub(r'\[[^\]]*\]','',req))
            anys=re.findall(r'\(([^)]*¦[^)]*)\)',req)
            anyids=set(i for a in anys for i in re.findall(r'[A-Z]{3}-\d+',a))
            assert pid not in P, pid
            if pid.startswith('KEY'):
                e=int(era[1:])-1 if era!='Apex' else 9
                th='KEY'
            else:
                e=int(era[1:]); th=theme
            P[pid]=dict(name=name,era=e,req=[i for i in ids if i not in anyids],any=sorted(anyids),theme=th,lin=pid[:3],type=typ,gates=re.findall(r'\[([^\]]*)\]',req))
errs=[]
for pid,p in P.items():
    for r in p['req']+p['any']:
        if r not in P: errs.append(f"{pid}: missing {r}")
        elif P[r]['era']>p['era']: errs.append(f"{pid}(E{p['era']}) requires later {r}(E{P[r]['era']})")
# cycles
state={}
def dfs(n,stack):
    if state.get(n)==1: errs.append("cycle: "+"->".join(stack+[n])); return
    if state.get(n)==2: return
    state[n]=1
    for r in P[n]['req']+P[n]['any']:
        if r in P: dfs(r,stack+[n])
    state[n]=2
for n in P: dfs(n,[])
# keystone era check
for k,p in P.items():
    if k.startswith('KEY'):
        for r in p['req']:
            if r in P and P[r]['era']!=p['era']: errs.append(f"{k} prereq {r} era E{P[r]['era']} != E{p['era']}")
print("ERRORS:",len(errs)); [print(" ",e) for e in errs]
nk=[p for k,p in P.items() if not k.startswith('KEY')]
print("projects (excl keystones):",len(nk),"keystones:",len(P)-len(nk))
byt=collections.Counter(p['theme'] for p in nk); print("by theme:",dict(byt))
bye=collections.Counter(p['era'] for p in nk); print("by era:",dict(sorted(bye.items())))
cum=0
for e in range(10): cum+=bye[e]; print(f"  cumulative through E{e}: {cum}")
byl=collections.Counter(p['lin'] for p in nk); print("lineages:",len(byl)); print(" ",dict(byl))
byty=collections.Counter(p['type'] for p in nk); print("types:",dict(byty))
starters=[k for k,p in P.items() if not p['req'] and not p['any'] and not k.startswith('KEY')]
print("starters:",starters)
# cross theme ratio E1+
print("cross-theme ratio per lineage (E1+ projects with a prereq from another theme):")
low=[]
for lin in byl:
    ps=[(k,p) for k,p in P.items() if p['lin']==lin and p['era']>=1]
    if not ps: continue
    x=sum(1 for k,p in ps if any(P[r]['theme']!=p['theme'] for r in p['req']+p['any'] if r in P))
    ratio=x/len(ps)
    if ratio<0.35: low.append((lin,round(ratio,2)))
    print(f"  {lin}: {x}/{len(ps)} = {ratio:.0%}")
print("below 35%:",low)
# era x theme matrix
print("era x theme:")
for e in range(10):
    print(f"  E{e}", {t:sum(1 for p in nk if p['era']==e and p['theme']==t) for t in ['CRAFT','KIN','REACH']})
# dependents count (most-required projects)
dep=collections.Counter(r for p in P.values() for r in p['req']+p['any'])
print("most required:",dep.most_common(15))
json.dump(P,open(sys.argv[2],'w'),indent=0)
