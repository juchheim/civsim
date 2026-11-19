import json,re,sys
d=sys.argv[1]; side=sys.argv[2]  # side whose win rate is reported
m=json.load(open(f'runs/{d}/run_metadata.json')); t=open(f'runs/{d}/raw-output.txt').read()
head=re.search(r"^(\w+) vs (\w+): (\d+) matches, \1 wins (\d+)%",t,re.M)
rows=re.findall(r"^    seed (\d+): (\w+) win;",t,re.M)
wins={}
for _,w in rows: wins[w]=wins.get(w,0)+1
seeds=sorted({int(s) for s,_ in rows})
lost=re.search(r"settlements lost per match: (\w+) ([\d.]+) \(in (\d+) matches\), (\w+) ([\d.]+) \(in (\d+) matches\)",t)
exp=80
print(json.dumps({"gate":d,"status":m["status"],"unchanged":m["unchangedDuringRun"],"exit":m["output"]["exitCode"],"sha":m["output"]["sha256"],"bytes":m["output"]["bytes"],
 "header":head.group(0) if head else None,"matchesReported":int(head.group(3)) if head else None,"perSeedRows":len(rows),"distinctSeeds":len(seeds),
 "seedRange":[seeds[0],seeds[-1]] if seeds else None,"wins":wins,"censoredOrMissing":exp-len(rows),
 "rate":wins.get(side,0)/exp,"lossLine":lost.groups() if lost else None,"elapsed":m["elapsedSeconds"]},indent=1))
