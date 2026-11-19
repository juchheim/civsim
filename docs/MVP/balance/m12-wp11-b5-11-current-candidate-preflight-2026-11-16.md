# B5-11 current-candidate performance preflight — 2026-11-16

**Status:** prepared, not run. This preflight binds the two existing 20-seed performance manifests to the current `candidate-5ee659bd` diagnostic tuple. It is useful for current-candidate measurement, but does not close B5-11 for WP-E5-11: the candidate is not the final promoted freeze, and no cohort has run.

> **Superseded and non-runnable snapshot.** The literal 5ee hashes and commands below are retained as historical context only. At the 2026-11-16 audit check, the worktree was dirty (795 paths) and the source aggregate was `2105bd5ddb36d2634dabe4b91fe392544c84486aecaedf8d3e77ad48a423f5e2` over 1,046 scoped files. The runner SHA at this check was `88bf378858a4a040e710266b466569ab7a0f60cb48335b85df7ab8988d3b9873`. The checked-in 5ee commands omit required `--expected-tree`, `--expected-runner-sha256`, and `--expected-provenance-helper-sha256` pins. The runner also requires a clean source checkout. These pins are a time-of-check record only; do not execute those commands or reuse any current-candidate hashes for acceptance.

Audit recheck: both frozen manifests still match their hard-coded SHA-256 pins and contain 20 unique seeds with no overlap. The runner fixture suite passes 6/6. Output preparation now rejects existing paths and symlinks before creating anything, and rejects paths inside (or enclosing) the repository. No performance cohort was launched.

## Frozen inputs

| Input | Value |
|---|---|
| HEAD | `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c` |
| Canonical `content/src/game` aggregate | `5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733` (1,045 files) |
| Numeric E5 diagnostic pack | `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack`; SHA-256 `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a` |
| Release Harness | `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll`; SHA-256 `26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100` |
| Adjacent Release Sim | `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Sim.dll`; SHA-256 `b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe` |

The pack and DLL hashes were rechecked against those paths while preparing this preflight. The source aggregate was rechecked as `5ee659bd…` after the concurrent F-T1 run's source guard was restored. Recheck the full tuple immediately before execution and do not run if any value differs. The runner checks HEAD, source aggregate, pack, Harness, Sim, and manifest hashes before and after each cohort; it refuses to overwrite output roots.

## Manifests and cohort roles

- Natural Fief: [m12-wp11-fief-natural-seeds-2026-11-15.txt](m12-wp11-fief-natural-seeds-2026-11-15.txt), 20 unique seeds, SHA-256 `f787766662b46319f7f0807eacd27592aa8c2af4163f366d52ddec07a3e3a786`.
- E5-granted stress: [m12-wp11-fief-grant-e5-seeds-2026-11-15.txt](m12-wp11-fief-grant-e5-seeds-2026-11-15.txt), 20 unique seeds, SHA-256 `b5d541d0f820ff3fc3979d2ff5158c15b9bc24ac61fa7e11af4196eec1f006b6`.

The two manifests are disjoint. Both were selected without geography, eligibility, progress, or outcome screening. The natural seed block is also used by the prior F-T5 trace protocol; this does not select on performance outcomes, and no B5-11 measurements exist for these seeds. The prior F-T5 outputs belong to an older source tuple. Preserve the seed lists unchanged.

## Exact guarded commands

Run sequentially, in an otherwise idle process, after the active F-T1 calibration and every other simulation/test workload have ended. The output directories below must not exist at launch. These commands collect one 130-minute run per seed with five warm-up minutes; the stress run adds `--grant-e5`.

Natural cohort:

```sh
python3 docs/MVP/balance/tools/run_frozen_e5_cohort.py \
  --repo /Users/ejuchheim/Projects/CivSim/civsim-e5 \
  --pack /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack \
  --harness-dll /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll \
  --sim-dll /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Sim.dll \
  --manifest docs/MVP/balance/m12-wp11-fief-natural-seeds-2026-11-15.txt \
  --expected-head bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c \
  --expected-source-aggregate 5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733 \
  --expected-pack-sha256 f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a \
  --expected-harness-sha256 26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100 \
  --expected-sim-sha256 b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe \
  --expected-manifest-sha256 f787766662b46319f7f0807eacd27592aa8c2af4163f366d52ddec07a3e3a786 \
  --output-dir /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/b5-11-natural \
  -- dotnet /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll \
  fief-perf --pack /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack \
  --seeds-file docs/MVP/balance/m12-wp11-fief-natural-seeds-2026-11-15.txt \
  --minutes 130 --warmup-minutes 5 \
  --output-prefix /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/b5-11-natural/perf
```

E5-granted stress cohort (run only after the natural cohort exits and its raw files are safely written):

```sh
python3 docs/MVP/balance/tools/run_frozen_e5_cohort.py \
  --repo /Users/ejuchheim/Projects/CivSim/civsim-e5 \
  --pack /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack \
  --harness-dll /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll \
  --sim-dll /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Sim.dll \
  --manifest docs/MVP/balance/m12-wp11-fief-grant-e5-seeds-2026-11-15.txt \
  --expected-head bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c \
  --expected-source-aggregate 5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733 \
  --expected-pack-sha256 f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a \
  --expected-harness-sha256 26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100 \
  --expected-sim-sha256 b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe \
  --expected-manifest-sha256 b5d541d0f820ff3fc3979d2ff5158c15b9bc24ac61fa7e11af4196eec1f006b6 \
  --output-dir /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/b5-11-grant-e5 \
  -- dotnet /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll \
  fief-perf --pack /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack \
  --seeds-file docs/MVP/balance/m12-wp11-fief-grant-e5-seeds-2026-11-15.txt \
  --minutes 130 --warmup-minutes 5 --grant-e5 \
  --output-prefix /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/b5-11-grant-e5/perf
```

Each guarded output root records pre/post provenance, the complete wrapped command and log, plus hashes for every output. Preserve the Harness-created `*-ticks.csv`, `*-tick-samples.csv`, `*-seats.csv`, and `*-provenance.txt`. Never reuse a consumed output root.

## Interpretation and closure boundary

The unchanged limits are pooled arithmetic mean tick time **≤ 4 ms** and process GC pause time divided by measured ticks **≤ 16 ms/tick**, for both cohorts. Report pooled and per-seed timing distributions, censor/stop status, and checksums. The p95 own-view figure count is compared with 600 figures per civilization; this is a diagnostic threshold, not a simulation cap. If p95 exceeds 600, investigate view representation cost before claiming the budget is acceptable. Do not reduce simulation population or tune numeric content from these measurements.

The current `5ee659bd` runs, if performed, remain diagnostic because their input pack is not the final promoted release candidate. To close B5-11 for WP-E5-11, refreeze source, numeric pack, adjacent Release Harness/Sim, and fresh output roots after the 50-row promotion and all runtime changes are settled; then repeat both 20-seed cohorts against that exact tuple. No performance cohort was launched in preparing this preflight.

## Post-freeze guarded invocation

After the isolated final promoted candidate P is clean, built, and frozen, set `PACK` to its numeric pack outside the repository and use its adjacent Release Harness and Sim. Run from the repository root so the workspace hash helper resolves the correct checkout. `OUT_DIR` must be a new external path for each cohort. The following shows the complete natural command; the script derives the expected source/artifact/tool hashes from the frozen files, and the runner checks them before and after every seed. Do not build or edit anything between computing these pins and the two cohort runs.

```sh
REPO=/Users/ejuchheim/Projects/CivSim/civsim-e5
PACK=/absolute/path/to/final-P/numeric-e5.pack
HARNESS="$REPO/src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll"
SIM="$REPO/src/CivSim.Harness/bin/Release/net10.0/CivSim.Sim.dll"
RUNNER="$REPO/docs/MVP/balance/tools/run_frozen_e5_cohort.py"
PROVENANCE_HELPER="$REPO/docs/MVP/balance/tools/cohort_provenance.py"
MANIFEST="$REPO/docs/MVP/balance/m12-wp11-fief-natural-seeds-2026-11-15.txt"
OUT_DIR=/absolute/path/outside/repo/b5-11-final-natural-attempt-01

cd "$REPO"
HEAD=$(git rev-parse HEAD)
TREE=$(git rev-parse 'HEAD^{tree}')
SOURCE_AGG=$(python3 "$REPO/docs/MVP/balance/tools/hash_e5_workspace.py" | python3 -c 'import json,sys; print(json.load(sys.stdin)["aggregate_sha256"])')
FILE_SHA() { python3 -c 'import hashlib,sys; print(hashlib.sha256(open(sys.argv[1],"rb").read()).hexdigest())' "$1"; }
PACK_SHA=$(FILE_SHA "$PACK")
HARNESS_SHA=$(FILE_SHA "$HARNESS")
SIM_SHA=$(FILE_SHA "$SIM")
RUNNER_SHA=$(FILE_SHA "$RUNNER")
PROVENANCE_HELPER_SHA=$(FILE_SHA "$PROVENANCE_HELPER")

python3 "$RUNNER" \
  --repo "$REPO" --pack "$PACK" --harness-dll "$HARNESS" --sim-dll "$SIM" \
  --manifest "$MANIFEST" \
  --expected-head "$HEAD" --expected-tree "$TREE" \
  --expected-source-aggregate "$SOURCE_AGG" \
  --expected-pack-sha256 "$PACK_SHA" --expected-harness-sha256 "$HARNESS_SHA" \
  --expected-sim-sha256 "$SIM_SHA" \
  --expected-manifest-sha256 f787766662b46319f7f0807eacd27592aa8c2af4163f366d52ddec07a3e3a786 \
  --expected-runner-sha256 "$RUNNER_SHA" \
  --expected-provenance-helper-sha256 "$PROVENANCE_HELPER_SHA" \
  --output-dir "$OUT_DIR" \
  -- dotnet "$HARNESS" fief-perf --pack "$PACK" --seeds-file "$MANIFEST" \
  --minutes 130 --warmup-minutes 5 --output-prefix "$OUT_DIR/perf"
```

For the second run, wait for the natural run to exit and all files to be written. Use a new external `OUT_DIR`, the grant manifest, its pinned SHA-256 `b5d541d0f820ff3fc3979d2ff5158c15b9bc24ac61fa7e11af4196eec1f006b6`, and add `--grant-e5` to the wrapped `fief-perf` command. Keep every other source/pack/binary/tool pin identical. The wrapper derives one per-seed command from each manifest row and records the seed manifest, log and output hashes with before/after source, pack, Harness, and Sim provenance.

The wrapper's terminal `status: complete` means all 20 commands exited successfully and the pinned tuple stayed stable; it does **not** mean the performance thresholds passed. Analyze both cohorts separately and together: the pooled arithmetic mean across measured ticks must be ≤4 ms, and total process GC pause divided by total measured ticks must be ≤16 ms/tick in each cohort. Compute pooled values weighted by each seed's measured-tick count; retain early-terminal/censored seeds and report them rather than dropping them. The harness sidecar also records runtime framework, OS, and architecture; run both cohorts sequentially on the same otherwise idle host. The 600-figure p95 remains diagnostic and requires investigation if exceeded; it is not a simulation cap.
