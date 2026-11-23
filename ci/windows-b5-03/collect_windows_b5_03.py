#!/usr/bin/env python3
"""Validate and merge Windows B5-03 shard receipts into a replay index."""
from __future__ import annotations
import argparse, hashlib, json, sys
from pathlib import Path

HEAD="c3aa4e063244748ffb1f951050ded4003ce8262a"
TREE="5f8d7b925b9292eb01a22e42829145e4b273390d"
AGG="701d31e084c03e9320f0de57f61abca57c4fa566aa4fca98e6a3d9c5e3e92503"
PACK="564b9e77b065441fc25b6137c37e6fd71b8c349bf82638199df3beaae37c2ebe"
HARNESS="5746fa66fefe6e52ab9f0268d6ec69004c95ec3a8a8d275d6d5f468b5704d950"
SIM="a85cc888b53ef7deb2dfe85fb49218eb62a03a0d51da620205db34ff224234bc"
TESTS="ecd86123731d11a49b251f9b06e08c748ff87a626be33031e9b3d3b34e03d702"
MANIFEST="12f313f9878c568798b32e723f1aa1783b2dbb3039bfae60749f7a279d1d9895"
TOTAL=130*600
INTERVAL=50

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--artifacts",type=Path,required=True)
    ap.add_argument("--manifest",type=Path,required=True)
    ap.add_argument("--output",type=Path,required=True)
    a=ap.parse_args()
    if sha(a.manifest)!=MANIFEST: raise ValueError("reviewed manifest hash mismatch")
    seeds=[int(v) for v in a.manifest.read_text("ascii").splitlines()]
    if seeds!=list(range(40_000_000,40_001_000)): raise ValueError("seed block mismatch")
    dirs=sorted(p for p in a.artifacts.iterdir() if p.is_dir())
    if len(dirs)!=100: raise ValueError(f"expected 100 shards, found {len(dirs)}")
    by_seed={}; seen=set(); src_ref=None; receipts=[]
    ticks=list(range(INTERVAL,TOTAL+1,INTERVAL))
    for d in dirs:
        mp=d/"run_metadata.json"; ip=d/"replay-index.json"; sp=d/"seed-manifest.txt"; smp=d/"source-manifest.json"
        if any(not p.is_file() for p in (mp,ip,sp,smp)): raise FileNotFoundError(d)
        m=json.loads(mp.read_text()); shard=m.get("shard",{}); i=shard.get("index")
        if not isinstance(i,int) or i in seen or shard.get("count")!=100: raise ValueError(f"{d.name}: shard id/count")
        seen.add(i); expected=seeds[len(seeds)*i//100:len(seeds)*(i+1)//100]
        if shard.get("seeds")!=expected or shard.get("seedCount")!=len(expected): raise ValueError(f"shard {i}: seed assignment")
        if sha(sp)!=shard.get("subsetManifest",{}).get("sha256") or [int(v) for v in sp.read_text("ascii").splitlines()]!=expected: raise ValueError(f"shard {i}: subset manifest")
        if (m.get("schema"),m.get("status"))!=("civsim.fief-determinism-run/v1","complete"): raise ValueError(f"shard {i}: incomplete")
        src=m.get("source",{})
        if (src.get("head"),src.get("tree"),src.get("aggregateSha256"),src.get("dirty"))!=(HEAD,TREE,AGG,False): raise ValueError(f"shard {i}: source pin")
        rt=m.get("runtime",{})
        if rt.get("architecture")!="x64" or "Windows" not in rt.get("os",""): raise ValueError(f"shard {i}: runtime")
        if (m.get("manifest",{}).get("sha256"),m.get("manifest",{}).get("seedCount"))!=(MANIFEST,1000): raise ValueError(f"shard {i}: full manifest pin")
        if m.get("pack",{}).get("sha256")!=PACK: raise ValueError(f"shard {i}: pack pin")
        bins=m.get("binaries",{})
        if (bins.get("harnessDll",{}).get("sha256"),bins.get("simDll",{}).get("sha256"))!=(HARNESS,SIM): raise ValueError(f"shard {i}: binary pins")
        if m.get("testArtifacts",{}).get("assembly",{}).get("sha256")!=TESTS: raise ValueError(f"shard {i}: test assembly pin")
        proto=m.get("protocol",{})
        if (proto.get("mode"),proto.get("minutes"),proto.get("ticks"),proto.get("command"))!=("Fief",130,TOTAL,"fief-match"): raise ValueError(f"shard {i}: protocol")
        commands=m.get("commands",[]); verify=[c for c in commands if c.get("name")=="replay-verify"]
        if len(verify)!=len(expected) or any(c.get("exitCode")!=0 for c in verify): raise ValueError(f"shard {i}: replay verify")
        if not any(c.get("name")=="fief-match" and c.get("exitCode")==0 for c in commands): raise ValueError(f"shard {i}: match")
        sm=json.loads(smp.read_text()); payload={"head":sm.get("head"),"files":sm.get("files")}
        aggregate=hashlib.sha256(json.dumps(payload,sort_keys=True,separators=(",",":")).encode()).hexdigest()
        if aggregate!=AGG or sm.get("aggregate_sha256")!=AGG: raise ValueError(f"shard {i}: source manifest aggregate")
        if src_ref is not None and sm!=src_ref: raise ValueError(f"shard {i}: source manifest differs")
        src_ref=sm
        if sha(smp)!=m.get("artifacts",{}).get("sourceManifest",{}).get("sha256"): raise ValueError(f"shard {i}: source manifest hash")
        idx=json.loads(ip.read_text())
        if (idx.get("schema"),idx.get("packHash"))!=("civsim-replay-index-v1",PACK): raise ValueError(f"shard {i}: index identity")
        if sha(ip)!=m.get("artifacts",{}).get("replayIndexSha256"): raise ValueError(f"shard {i}: replay index hash")
        rows=idx.get("seeds",[])
        if [r.get("seed") for r in rows]!=expected or [r.get("seed") for r in m.get("seeds",[])]!=expected: raise ValueError(f"shard {i}: replay seed list")
        for e in rows:
            seed=e["seed"]
            if seed in by_seed: raise ValueError(f"duplicate seed {seed}")
            cs=e.get("checksums",[])
            if e.get("verified") is not True or e.get("mode")!="fief" or e.get("ticks")!=TOTAL or e.get("checksumInterval")!=INTERVAL or e.get("checksumCount")!=len(ticks) or [c.get("tick") for c in cs]!=ticks or not cs or e.get("finalChecksum")!=cs[-1].get("checksum"): raise ValueError(f"seed {seed}: checksum evidence")
            by_seed[seed]=e
        receipts.append({"shard":i,"artifact":d.name,"metadataSha256":sha(mp),"indexSha256":sha(ip),"runtime":rt})
    if seen!=set(range(100)) or sorted(by_seed)!=seeds: raise ValueError("missing or extra shard/seed")
    a.output.mkdir(parents=True,exist_ok=False)
    index={"schema":"civsim-replay-index-v1","packHash":PACK,"seeds":[by_seed[s] for s in seeds]}
    out=a.output/"windows-replay-index.json"; out.write_text(json.dumps(index,indent=2,sort_keys=True)+"\n")
    receipt={"schema":"wp-e5-11-windows-b5-03-ci-receipt/v1","status":"complete","host":"Windows-x64","candidateHead":HEAD,"candidateTree":TREE,"sourceAggregate":AGG,"packSha256":PACK,"manifestSha256":MANIFEST,"seedCount":len(seeds),"shardCount":100,"shards":sorted(receipts,key=lambda r:r["shard"]),"mergedIndexSha256":sha(out)}
    (a.output/"receipt.json").write_text(json.dumps(receipt,indent=2,sort_keys=True)+"\n")
    print(f"validated 100 Windows x64 shards / 1000 seeds; index SHA-256 {receipt['mergedIndexSha256']}")
    return 0

if __name__=="__main__":
    try: raise SystemExit(main())
    except Exception as exc:
        print(f"collector failed: {exc}",file=sys.stderr); raise SystemExit(1)

