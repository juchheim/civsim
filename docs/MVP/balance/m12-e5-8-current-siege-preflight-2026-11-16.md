# WP-E5-8 current-candidate siege trace preflight — 2026-11-16

**Status:** Historical diagnostic tuple only; it is stale for the current checkout and is not launch-ready. No matches were launched from this preflight. WP-E5-8 stays diagnostic, not acceptance or promotion.

## Current checkout supersedes these pins

The tuple below identifies the 2026-11-16 `5ee659bd` diagnostic candidate, not the present source. In the canonical checkout at audit time, HEAD is still `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`, but `content/`, `src/`, and `game/` are dirty. `hash_e5_workspace.py` therefore reports a working-tree aggregate of `c617039cef54854f9fb5a19fb7e94bd22417ff7654069d56974f7dc4aeb79981` over 1,046 source files. This is a snapshot for identifying current source only; it is not a freeze, and no pack or Release binaries have been validated against it. Do not launch with the candidate paths or hashes below. After the final WP-E5-11 source freeze, rebuild/repack once, export a fresh source file manifest, freeze both adjacent Release DLLs and tool hashes, and repeat the disjoint-seed check before reserving a new output root.

## Frozen inputs

- Source aggregate: `5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733` (1,045 files; HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`).
- Numeric diagnostic pack: `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack`, SHA-256 `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`.
- Release Harness SHA-256 `26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100`; adjacent Release Sim SHA-256 `b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe`.
- Frozen source manifest: `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/source-manifest-ft5.json`, SHA-256 `e7b0ef35a76f54026939597a0960180d796c1c3989518f1e7779c24366aa92c6`.
- Runner-ready provenance: `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/ft5-provenance.json`.
- Seed manifest: [m12-e5-8-f-t5-natural-seeds-2026-11-16.txt](m12-e5-8-f-t5-natural-seeds-2026-11-16.txt), 20 unscreened seeds `4100000000000000001–4100000000000000020`, SHA-256 `d30825f653a1a39c851a7a5f1d64a640ce0c69fee8da70c95c619d0a51edc29b`.
- Output directory: `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/fief-siege-natural-run-2026-11-16` (confirmed absent when prepared).
- Shared guarded runner: `tools/fief_trace_runner.py`, SHA-256 `0d5e2b75161fdcc47617e22d4877f264393e7f9390bd43d98bc0665d40a2cb9d`, `--trace-kind siege`.

The generic runner's `validate` function accepted the exact provenance, pack, Harness, adjacent Sim, source manifest, and seed file. The host is busy with frozen F-T1 and other matches; do not launch until it is idle. Any change to `content/`, `src/`, or `game/`, the pack, binaries, manifest, runner, or command protocol invalidates this preflight and requires a refreshed source manifest and hashes. Retain all 20 seeds, including no-exposure, interrupted, failed, censored, and early-end runs.

## Run command

```sh
python3 tools/fief_trace_runner.py \
  --provenance /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/ft5-provenance.json \
  --source-root /Users/ejuchheim/Projects/CivSim/civsim-e5 \
  --pack /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack \
  --harness /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll \
  --sim /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Sim.dll \
  --seeds /Users/ejuchheim/Projects/CivSim/civsim-e5/docs/MVP/balance/m12-e5-8-f-t5-natural-seeds-2026-11-16.txt \
  --output /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/fief-siege-natural-run-2026-11-16 \
  --minutes 130 --trace-kind siege
```

After all 20 seed records complete successfully, validate and summarize the run with:

```sh
python3 docs/MVP/balance/tools/analyze_fief_siege_trace.py \
  --run-dir /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/fief-siege-natural-run-2026-11-16 \
  --manifest docs/MVP/balance/m12-e5-8-f-t5-natural-seeds-2026-11-16.txt \
  --output docs/MVP/balance/m12-e5-8-f-t8-natural-analysis-2026-11-16.md \
  --verify-current
```

The analyzer requires the frozen 20-seed manifest hash, complete runner status, one successful sidecar and output hash per seed, the trace/minute protocol, the adjacent Harness/Sim hashes, and the canonical frozen source manifest. `--verify-current` additionally requires the live source tree to remain identical to the run tuple; omit it only when analyzing preserved output after a later source change. The report uses seed-clustered bootstrap intervals for attrition and marks sparse exposure inconclusive. It reports but does not evaluate an acceptance threshold.

Analyze engine/cannon investment and staffing, paid shots and 0.25 L powder-per-shot conservation, cannon-backed breach state, assigned adult-turns and deaths by base/DEF-09 strata, garrisoned concentric exposure, relief/capture/sally, end stocks, and right-censored/no-investment outcomes. Do not infer attrition rates from zero or low exposure. Keep the confirmed draft rule values (1% per assigned adult per siege turn, 1.25% with garrisoned concentric DEF-09) unpromoted until all independent acceptance requirements pass.

The trace can summarize invested engine/cannon counts, staffing, shots, final inventories, and terminal outcomes; it does not emit acquisition events. Its persistent `powderBreach` field indicates the cannon-backed state, whereas an actual wall breach is identified from integrity reaching zero. End-of-match stock cannot establish the number of assets purchased.

Existing natural F-T5 data is historical and inconclusive: its 20 matches ran on aggregate `87996d7a…` and recorded no siege investments, shots, breaches, crew exposures, or DEF-09 exposure. The current-candidate run has not started.
