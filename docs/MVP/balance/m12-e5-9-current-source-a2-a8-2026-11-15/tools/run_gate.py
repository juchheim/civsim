#!/usr/bin/env python3
"""Frozen-input gate runner for WP-E5-9 acceptance.

usage: run_gate.py GATE_ID OUTDIR [--seeds-file F] [--stdout-name NAME] -- HARNESS_ARGS...

Before the run: re-verifies the private source snapshot (per-file hashes and both
aggregates), the frozen Release binaries, the frozen pack, and (informational) the
shared live tree. Writes run_metadata.json (status preflight_frozen_before_run), runs
the Harness with raw output captured, re-verifies, and rewrites the metadata.
"""
import hashlib, json, os, subprocess, sys, time, datetime
from pathlib import Path

SCR = Path("/private/tmp/claude-501/-Users-ejuchheim--claude-spares-spare-CuLIlA/5b70c709-f986-4985-b1f7-c35ad1083c59/scratchpad")
FROZEN = SCR / "frozen"
LIVE = Path("/Users/ejuchheim/Projects/CivSim/civsim-e5")
EXPECT = {
    "head": "bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c",
    "contentSrcGame": "7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6",
    "contentSrc": "bee3fb3a8f45d68010a649f76b71ecb1b191a884742eeb8118d2b90c64a69dba",
    "pack": "f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a",
    "harness": "aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f",
    "sim": "a8cb7666042cf6651a6b9558b3d6db7b1f00566b5cc30041077e67b86540ad50",
}

def sha(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()

def aggregate(head, files):
    payload = {"head": head, "files": files}
    return hashlib.sha256(json.dumps(payload, sort_keys=True, separators=(",", ":")).encode()).hexdigest()

def verify_snapshot():
    rec = json.load(open(FROZEN / "ws-recorded.json"))
    files, bad = [], []
    for f in rec["files"]:
        p = FROZEN / "src-tree" / f["path"]
        actual = sha(p) if p.is_file() else None
        if actual != f["sha256"]:
            bad.append(f["path"])
        files.append({"path": f["path"], "status": f["status"], "sha256": actual})
    all_agg = aggregate(rec["head"], files)
    cs_files = [f for f in files if f["path"].startswith(("content/", "src/"))]
    cs_agg = aggregate(rec["head"], cs_files)
    return {
        "head": rec["head"], "mismatchedFiles": bad,
        "contentSrcGameAggregateSha256": all_agg, "contentSrcGameFileCount": len(files),
        "contentSrcAggregateSha256": cs_agg, "contentSrcFileCount": len(cs_files),
        "ok": (not bad and all_agg == EXPECT["contentSrcGame"] and cs_agg == EXPECT["contentSrc"]
               and rec["head"] == EXPECT["head"]),
    }

def verify_binaries():
    out = {
        "harnessDll": sha(FROZEN / "bin/CivSim.Harness.dll"),
        "simDll": sha(FROZEN / "bin/CivSim.Sim.dll"),
        "pack": sha(FROZEN / "numeric-e5.pack"),
    }
    out["binDirFiles"] = {p.name: sha(p) for p in sorted((FROZEN / "bin").iterdir()) if p.is_file()}
    out["ok"] = (out["harnessDll"] == EXPECT["harness"] and out["simDll"] == EXPECT["sim"]
                 and out["pack"] == EXPECT["pack"])
    return out

def live_tree():
    try:
        r = subprocess.run([sys.executable, str(LIVE / "docs/MVP/balance/tools/hash_e5_workspace.py")],
                           cwd=LIVE, capture_output=True, check=True)
        d = json.loads(r.stdout)
        cs = [f for f in d["files"] if f["path"].startswith(("content/", "src/"))]
        return {"head": d["head"], "contentSrcGameAggregateSha256": d["aggregate_sha256"],
                "contentSrcAggregateSha256": aggregate(d["head"], cs),
                "equalsFrozen": d["aggregate_sha256"] == EXPECT["contentSrcGame"]}
    except Exception as e:  # informational only
        return {"error": str(e)}

def freeze():
    return {"snapshot": verify_snapshot(), "binaries": verify_binaries(), "liveSharedTree": live_tree(),
            "checkedAt": datetime.datetime.now().astimezone().isoformat()}

def main():
    argv = sys.argv[1:]
    gate, outdir = argv[0], Path(argv[1])
    rest = argv[2:]
    seeds_file = None
    stdout_name = "raw-output.txt"
    while rest and rest[0] != "--":
        if rest[0] == "--seeds-file": seeds_file = Path(rest[1]); rest = rest[2:]
        elif rest[0] == "--stdout-name": stdout_name = rest[1]; rest = rest[2:]
        else: raise SystemExit("bad option " + rest[0])
    harness_args = rest[1:]
    outdir.mkdir(parents=True, exist_ok=True)

    pre = freeze()
    if not (pre["snapshot"]["ok"] and pre["binaries"]["ok"]):
        (outdir / "run_metadata.json").write_text(json.dumps({"gate": gate, "status": "refused_frozen_inputs_changed", "preflight": pre}, indent=2))
        raise SystemExit("frozen inputs do not match; refusing to run")

    cmd = ["dotnet", str(FROZEN / "bin/CivSim.Harness.dll")] + harness_args
    meta = {
        "gate": gate,
        "command": cmd,
        "cwd": str(outdir),
        "frozenIdentity": EXPECT,
        "preflight": pre,
        "seedManifest": ({"path": str(seeds_file), "sha256": sha(seeds_file)} if seeds_file else None),
        "status": "preflight_frozen_before_run",
    }
    (outdir / "run_metadata.json").write_text(json.dumps(meta, indent=2))
    raw = outdir / stdout_name
    t0 = time.time()
    meta["startedAt"] = datetime.datetime.now().astimezone().isoformat()
    with open(raw, "wb") as fh:
        proc = subprocess.Popen(cmd, stdout=fh, stderr=subprocess.STDOUT, cwd=str(outdir),
                                env={**os.environ, "DOTNET_ROOT": os.environ.get("DOTNET_ROOT", "/opt/homebrew/opt/dotnet/libexec")})
        code = proc.wait()
    meta["finishedAt"] = datetime.datetime.now().astimezone().isoformat()
    meta["elapsedSeconds"] = round(time.time() - t0, 1)
    post = freeze()
    meta["postrun"] = post
    meta["unchangedDuringRun"] = (post["snapshot"]["ok"] and post["binaries"]["ok"])
    meta["output"] = {"rawLog": stdout_name, "exitCode": code, "sha256": sha(raw), "bytes": raw.stat().st_size}
    meta["status"] = "completed" if meta["unchangedDuringRun"] else "invalid_inputs_changed_mid_run"
    (outdir / "run_metadata.json").write_text(json.dumps(meta, indent=2))
    print(json.dumps({"gate": gate, "exitCode": code, "status": meta["status"], "elapsed": meta["elapsedSeconds"]}))

if __name__ == "__main__":
    main()
