# WP-E5-8 SEA-10 natural-route preflight — 2026-11-16

**Status:** Historical route-screen manifest and 5ee diagnostic artifact tuple only; stale for the current checkout and not launch-ready. No matches have been launched from this preflight. This is a diagnostic exposure screen, not an acceptance cohort. It does not promote any E5 row or enable Fief selection.

## Current checkout supersedes the candidate tuple

The 5ee aggregate and artifacts below describe the earlier diagnostic candidate, not the present source. In the canonical checkout at audit time, HEAD is `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`, while `content/`, `src/`, and `game/` are dirty. The current working-tree aggregate is `c617039cef54854f9fb5a19fb7e94bd22417ff7654069d56974f7dc4aeb79981` across 1,046 source files; this is not a freeze and has no matching verified pack/Release tuple. Do not use the candidate paths or hashes below for execution. Recompute all provenance after the final WP-E5-11 source freeze and rebuild.

## Frozen seed block

The manifest [m12-e5-8-sea10-natural-seeds-2026-11-16.txt](m12-e5-8-sea10-natural-seeds-2026-11-16.txt) contains 20 contiguous, unscreened seeds, `4200000000000000001–4200000000000000020`. Its SHA-256 is `1f63acfdc5330fede8d4875799aff0b4cd907708fe538cc4a8abed7e932e84dc`. A repository/project/attachment search for the selected block found no prior references. The range is disjoint from the prepared WP8 F-T5 block (`4100000000000000001–4100000000000000020`), the WP-E5-12 holdout (`33000000–33000079`), and previously recorded WP8 natural runs. At execution freeze, recheck disjointness against every WP5/WP8/WP9/D20/WP-E5-12 manifest.

## Run prerequisites

The prepared diagnostic tuple is source aggregate `5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733`, numeric pack SHA `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`, Release Harness SHA `26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100`, and adjacent Release Sim SHA `b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe`. Its source-manifest file `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/source-manifest-ft5.json` has SHA-256 `e7b0ef35a76f54026939597a0960180d796c1c3989518f1e7779c24366aa92c6`; the runner-ready `sea10-provenance.json` is preserved beside it. The shared runner `/Users/ejuchheim/Projects/CivSim/civsim-e5/tools/fief_trace_runner.py` has SHA-256 `0d5e2b75161fdcc47617e22d4877f264393e7f9390bd43d98bc0665d40a2cb9d`. The runner's input validator accepted the complete tuple and seed manifest. This is still only the current diagnostic candidate, not a final promoted release. Execute only when the host is idle and the source, pack, binaries, manifest, runner, and command protocol remain unchanged. Do not reuse an existing output directory.

The shared `tools/fief_trace_runner.py` supports this exact read-only observation command once those values are frozen:

```sh
python3 tools/fief_trace_runner.py \
  --provenance /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/sea10-provenance.json \
  --source-root /Users/ejuchheim/Projects/CivSim/civsim-e5 \
  --pack /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack \
  --harness /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll \
  --sim /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Sim.dll \
  --seeds /Users/ejuchheim/Projects/CivSim/civsim-e5/docs/MVP/balance/m12-e5-8-sea10-natural-seeds-2026-11-16.txt \
  --output /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/sea10-natural-run-2026-11-16 \
  --minutes 130 --trace-kind sea10
```

The command runs one natural `fief-match` per seed with no grants or geography screening. Preserve every seed, including no-route, rejected, locked, lost, returned, or incomplete cases. The trace emits milestone and caravan route rows for land/water/water-locked/unreachable routes, route rejection reason, requested/capped/taken/carried/delivered/paid/returned/lost quantities, SEA-04/KEY-6/SEA-10 state, and dispatch/arrival season. Require actual water-route dispatch to establish exposure; count delivered shipments separately from dispatched, returned, and lost cargo.

After the run completes, validate its status, per-seed raw hashes, frozen source-manifest hash, candidate artifact hashes, and cargo accounting with the analyzer:

```sh
python3 docs/MVP/balance/tools/analyze_fief_sea10_trace.py \
  --run-dir /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/sea10-natural-run-2026-11-16 \
  --seeds docs/MVP/balance/m12-e5-8-sea10-natural-seeds-2026-11-16.txt \
  --provenance /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/sea10-provenance.json \
  --output /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/sea10-natural-run-2026-11-16/analysis.md
```

## Analysis limits and checks

- A pre-KEY-6 `water_locked` route rejection and a post-KEY-6 permitted route are separate observations; do not infer eligibility from a successful land trade.
- The trace writes cargo quantities in Fix64 raw units and includes the good ID plus SEA-10 state. The analyzer divides by `2^32`, reports food in R units and food-volume equivalents using the documented `2 R ≈ 1 L` conversion, and leaves other goods in good-specific cargo units without assigning a density.
- The analyzer compares the recorded cap to the 12/24 L rule only for water-route dispatches where requested cargo reaches capacity; a lower cap can be request-limited. Dispatch `taken`, event-time `carried`, delivered, voyage-returned, settlement-returned, and lost quantities are kept distinct. Delivered plus settlement-returned, voyage-returned, or lost cargo must reconcile to the dispatched carried amount where a terminal event is observed.
- A shipment without a terminal trace row at the match horizon is reported as right-censored; no-route/no-trace seeds remain in the report. The analysis is descriptive and has no acceptance threshold.
- `FiefShipCargoTests` and `Sea10_report_trace_is_checksum_neutral` are fixture evidence. Natural traces can establish route access, repeat shipments, and throughput frequency; they do not by themselves establish final balance acceptance.

No run hashes, route exposures, or pass/fail outcomes exist yet.
