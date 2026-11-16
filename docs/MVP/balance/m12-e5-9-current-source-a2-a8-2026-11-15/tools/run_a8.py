#!/usr/bin/env python3
import json, os, subprocess, sys, time
from pathlib import Path
sys.path.insert(0, "/private/tmp/claude-501/-Users-ejuchheim--claude-spares-spare-CuLIlA/5b70c709-f986-4985-b1f7-c35ad1083c59/scratchpad")
import run_gate as g
SCR = g.SCR; out = SCR / "runs/a8-final"; out.mkdir(parents=True, exist_ok=True)
seeds = [20261019 + 5*i for i in range(8)]
tests = SCR / "frozen/src-tree/src/CivSim.Tests"
env = {**os.environ, "DOTNET_ROOT": "/opt/homebrew/opt/dotnet/libexec", "CIVSIM_A8_PACK": str(SCR/"frozen/numeric-e5.pack"),
       "CIVSIM_A8_OUT": str(out), "CIVSIM_A8_SEEDS": ",".join(map(str, seeds)), "CIVSIM_A8_INTERVAL": "16", "CIVSIM_A8_MINUTES": "64", "CIVSIM_A8_DOP": "4", "CIVSIM_A8_TIERB": "1"}
cmd = ["dotnet", "test", "src/CivSim.Tests/CivSim.Tests.csproj", "-c", "Release", "--no-build", "--filter", "FullyQualifiedName~FiefWholeMatchFogFairnessTests.Whole_fief_matches"]
pre = g.freeze()
tb = tests / "bin/Release/net10.0"
meta = {"gate": "a8-final", "command": cmd, "env": {k: v for k, v in env.items() if k.startswith("CIVSIM_A8")}, "preflight": pre,
        "testBinaries": {n: g.sha(tb / n) for n in ["CivSim.Tests.dll", "CivSim.Sim.dll", "CivSim.Harness.dll"]},
        "testSourceSha256": g.sha(tests / "FiefWholeMatchFogFairnessTests.cs"), "seeds": seeds, "status": "preflight_frozen_before_run"}
if not (pre["snapshot"]["ok"] and pre["binaries"]["ok"] and meta["testBinaries"]["CivSim.Sim.dll"] == g.EXPECT["sim"]):
    raise SystemExit("frozen inputs mismatch")
(out / "run_metadata.json").write_text(json.dumps(meta, indent=2))
t0 = time.time()
with open(out / "raw-output.txt", "wb") as fh:
    code = subprocess.Popen(cmd, stdout=fh, stderr=subprocess.STDOUT, cwd=str(SCR/"frozen/src-tree"), env=env).wait()
post = g.freeze()
meta.update({"postrun": post, "elapsedSeconds": round(time.time()-t0, 1),
             "unchangedDuringRun": post["snapshot"]["ok"] and post["binaries"]["ok"] and g.sha(tb/"CivSim.Sim.dll") == g.EXPECT["sim"],
             "output": {"exitCode": code, "sha256": g.sha(out/"raw-output.txt")}})
meta["status"] = "completed" if meta["unchangedDuringRun"] else "invalid_inputs_changed_mid_run"
(out / "run_metadata.json").write_text(json.dumps(meta, indent=2))
print(meta["status"], code)
