# WP-E5-6 F-T6 / F-T7 current-candidate calibration preflight — 2026-11-16

## State

Prepared, frozen, and dormant. No match, build, test, or holdout was launched. The calibration runner must remain unstarted while the host is saturated. This document prepares calibration only.

The shared 20-seed manifest is [calibration-seeds.txt](calibration-seeds.txt), SHA-256 `571d27e2f7454d6f5d91a33c0859a19a3ad159ffaea27a5d6dd4ae640b209d10`. It copies the WP7 preregistered calibration seed set exactly and preserves its order. Set comparisons found zero overlap with the WP7 holdout, prior F-T7 calibration, and prior F-T7 holdout ledgers. See [preflight.json](preflight.json) for source ledger hashes and full provenance.

## Frozen candidate tuple

- Source aggregate: `5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733` (1045 content/src/game files), head `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`.
- Numeric-status pack: `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a` at `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack`.
- Release Harness: `26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100` at `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll`.
- Adjacent Sim: `b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe` at `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Sim.dll`.
- Candidate tuple: `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/candidate.json`.

These pack and DLL hashes were read and checked against the candidate tuple. The runner additionally re-captures the current repository source aggregate, head, pack, harness, and Sim and fails closed if any expected value differs.

## Protocol and unchanged criteria

Use one natural `fief-match` per seed, 130 minutes, no `--grant-e5`, with `--fief-health-trace --fief-institution-trace`. Retain all seed outputs, censoring, unresolved institution staffing, MED-08/quarantine state, and import counters.

F-T6 dormant cutoff stays diagnostic and unapproved. Report working/dormant/unresolved staffing and blockers without converting the dormant diagnostic into an acceptance gate. F-T7 retains the existing 10–14% expected epidemic-hazard-share band and the seed-bootstrap 95% CI-includes-12% criterion, along with existing exposure, cohort-size, and censoring requirements. Valid MED-08 import exposure remains required evidence: if natural valid route-import attempts are zero, do not claim the cohort validated the import mechanic; retain focused keyed-gate evidence separately.

## Prepared calibration command (DO NOT RUN WHILE HOST IS SATURATED)

Run from `/Users/ejuchheim/Projects/CivSim/civsim-e5` only after the host is no longer saturated and an operator explicitly starts calibration:

```sh
python3 docs/MVP/balance/tools/run_fief_t6_t7_cohort.py \
  --pack /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack \
  --harness-dll /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll \
  --sim-dll /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Sim.dll \
  --expected-head bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c \
  --expected-source-aggregate 5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733 \
  --expected-pack-sha256 f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a \
  --expected-harness-sha256 26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100 \
  --expected-sim-sha256 b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe \
  --expected-manifest-sha256 571d27e2f7454d6f5d91a33c0859a19a3ad159ffaea27a5d6dd4ae640b209d10 \
  --manifest /Users/ejuchheim/Projects/CivSim/civsim-e5/docs/MVP/balance/m12-wp-e5-6-f-t6-t7-current-calibration-preflight-2026-11-16/calibration-seeds.txt \
  --output-dir /Users/ejuchheim/Projects/CivSim/civsim-e5/docs/MVP/balance/m12-wp-e5-6-f-t6-t7-current-calibration-preflight-2026-11-16/output \
  --cohort-role calibration
```

The manifest is frozen, 20 unique seeds; the output directory is fresh and must not yet exist. `--resume` is for an interrupted run only and retains the runner's exact-input/output-hash checks. Do not substitute a holdout manifest or set `--cohort-role held_out`.

After a complete calibration run, analyze with:

```sh
python3 docs/MVP/balance/tools/analyze_fief_t6_t7_cohort.py \
  --raw-dir /Users/ejuchheim/Projects/CivSim/civsim-e5/docs/MVP/balance/m12-wp-e5-6-f-t6-t7-current-calibration-preflight-2026-11-16/output \
  --manifest /Users/ejuchheim/Projects/CivSim/civsim-e5/docs/MVP/balance/m12-wp-e5-6-f-t6-t7-current-calibration-preflight-2026-11-16/calibration-seeds.txt \
  --output /Users/ejuchheim/Projects/CivSim/civsim-e5/docs/MVP/balance/m12-wp-e5-6-f-t6-t7-current-calibration-preflight-2026-11-16/analysis.md
```

Analysis uses the existing 20,000-resample seed-level bootstrap (`20261115`) and existing report template. No acceptance thresholds are altered here.
