# WP-E5-8 current-candidate gap audit and guarded-run preflight (2026-11-15)

## Status

WP-E5-8 remains open. This audit reconciles the natural F-T5 and SEA-10 evidence, identifies the exact-source instrumentation gap, and prepares execution gates. No cohort, test, or build was launched. The host is still saturated by unrelated current-source work, and the worktree/candidate is not a clean freeze; no qualifying run should start from this snapshot.

## Evidence reconciliation

### F-T5 siege and crew

The only frozen 20-seed natural F-T5 cohort has already been inspected and is explicitly **inconclusive**: 20/20 matches reached 130 minutes, but there were zero siege-investment seasons, cannon shots, powder paid, breaches, assigned-crew seasons, observed attrition deaths, or DEF-09 observations. The run used aggregate `87996d7a…`, pack `994fb780…`, Harness `27a9d425…`, and Sim `fc795950…`, not the later current tuple. Its terminal rows contain 39 siege-event keys but lack investment detail. See [F-T5 natural exposure diagnostic](m12-e5-8-f-t5-natural-exposure-diagnostic-2026-11-15.md) and its [raw bundle](m12-e5-8-f-t5-natural-output-2026-11-15/analysis.md).

Controlled fixtures establish plumbing and keyed casualty mechanics, including supplied engine/cannon attacks, powder use, relief conservation, snapshot continuation, and DEF-09 conditions. They do not measure natural acquisition, crew exposure, attrition under normal economy, capture, or relief. The current-source `--fief-siege-trace` emits per-season investment/crew/fire/attrition rows and terminal no-exposure/censor outcomes; the existing `run_fief_ft5_cohort.py` is hard-pinned to the already-consumed 20-seed manifest and its existing output directory. It must not be repurposed or overwritten for a new attempt.

### SEA-10 throughput

The focused cargo tests establish 12 L plank-boat and 24 L SEA-10 shipment limits, only on actual water-crossing Fief routes, with repeat shipments and payment on delivered cargo. They do not establish positive natural sea-route dispatch, water crossing, cargo movement, or surplus throughput in whole matches. The inspected isolated fixture waves are nonqualifying diagnostics: their Harness SHA was `61fbc020…`, Sim SHA was `2e6e031c…`, pack SHA was `f360a3d9…`; the current expected candidate tuple recorded by the readiness report uses Harness `aa7b830c…` and Sim `a8cb7666…`. Complete preservation bundles are in the isolated workspace at:

- [first stopped wrong-binary bundle](../../../../civsim-e5-wpe58-iso/docs/MVP/balance/m12-e5-8-wpe58-controlled-siege-sea10-2026-11-15/wrong-binary-fixtures-stopped-2026-11-15/report.md)
- [relaunch attempt 2 bundle](../../../../civsim-e5-wpe58-iso/docs/MVP/balance/m12-e5-8-wpe58-controlled-siege-sea10-2026-11-15/wrong-binary-fixtures-relaunch-stopped-2026-11-15/report.md)
- [relaunch wave 3 bundle](../../../../civsim-e5-wpe58-iso/docs/MVP/balance/m12-e5-8-wpe58-controlled-siege-sea10-2026-11-15/wrong-binary-relaunch-wave3-stopped-2026-11-15/report.md)
- [stalled isolated build/test bundle](../../../../civsim-e5-wpe58-iso/docs/MVP/balance/m12-e5-8-wpe58-controlled-siege-sea10-2026-11-15/build-41485-stall-diagnostic-2026-11-15/report.md)

All five wave-3 harness stdout/stderr files were empty (SHA-256 `e3b0c442…`); the attempted checksum-neutrality test had no result at preservation and was built against the same mismatched Sim. The scratch SEA10 attempt produced no qualifying natural evidence. The isolated launcher command included `--fief-trade-trace`; a source search in the active `codex/e5-fief` tree found no `fief-trade-trace` option or implementation. Therefore that command cannot be copied to a current-source runner, and the isolated trace/test must not be presented as current behavior or correctness proof.

The trace required to close this gap must identify every Fief dispatch and stable route/expedition key; seller/buyer and origin/destination; land vs actual water leg; civ KEY-6/SEA-10 state when route eligibility is checked; cargo resource and amount selected, capped, removed from stock, carried, delivered, paid, or lost; and shipment season. It must record denied/pre-KEY-6 sea attempts or route rejections distinctly from zero dispatch. To prove the 12/24 L caps, retain normalized cargo volume and the version/cap in force per shipment. A positive-throughput denominator is actual water shipments delivered (not all trade offers or seasons). Missed or censored voyages remain explicit. A diagnostic on/off determinism test must compare full simulation checksum trajectories for current source.

## Frozen inputs and current process snapshot

The previously recorded candidate identity is HEAD `bd0f4b8c…`, source aggregate `7decf87a…`, numeric scratch pack `f360a3d9…`, Release Harness `aa7b830c…`, adjacent Release Sim `a8cb7666…`. These hashes are not a license to run: the full worktree remains dirty and active runs are consuming these shared binaries/source state. Recompute full fingerprints and verify a clean/explicitly frozen source snapshot before any cohort; a mismatch at preflight means stop without launching.

The latest host process check showed active heavy jobs:

| Cohort | Live process IDs at check | State |
|---|---|---|
| D20 | parent 7122; workers 47613, 47726, 47755, 47806, 48259, 48553, 51621, 51823 | Active simulation workers |
| WP9 A5 | 15753 | Active FFA harness |
| WP9 A6 | 17489 | Active exploit harness |
| WP9 A8 | testhost 24923 under VSTest 24917 | Active; status/output must be reconciled by its owner |
| WP9 A4 | 38386 | Active Chieftain–Warlord harness |
| WP9 A2 | 41415 | Active Warlord–Builder harness |

No F-T5 or SEA-10 run may overlap these workloads. The older isolated wrong-binary worker PIDs from the preservation bundles are stopped; any newly appearing isolated `civsim-e5-wpe58-iso` harness must be provenance-checked before it is considered, and mismatched processes remain diagnostics only.

### Recurrence record — 2026-11-16

A fourth isolated SEA-10 relaunch was observed and stopped after preserving its state in the isolated checkout's `wrong-binary-recurrence-wave4-preserved-2026-11-16/` bundle. The exact launcher command line was `cat inputs/sea10-natural-seeds-rerun.txt | xargs -P 5 -n 1 ./run_sea10_natural.sh`; per-seed commands in `run_sea10_natural.sh` used `--fief-trade-trace`. Its provenance was Harness `61fbc0204fc6a21c2f9b9c70a06051a73f07fa74e3f119efb89f1e698a51136d`, Sim `2e6e031cc897f1d8aa19dd8457419ebbe64adb2dd8690454c751fd8c18ff54a4`, pack `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`. Harness and Sim do not match the frozen current candidate. The 5 workers (seeds `31220586`, `31224360`, `31337339`, `31353720`, `31424177`) had zero-byte stdout/stderr at preservation; no seed result qualifies. The same isolated tree also had a checksum-neutrality test runner/testhost without a terminal result; both were stopped and preserved with the wave. No output from this wave is acceptance evidence.

The wave's top-level shell launcher was already orphaned (PPID 1). A subsequent relaunch exposed the recurrence mechanism: `supervise.sh` repeatedly invokes `run_sea10_natural.sh` up to eight times until a per-seed `exit-codes-SEED.done` marker appears. This was the exact root launcher for the observed restart; the outer process that initially spawned the orphaned supervisors was gone, so attribution to a scheduler/agent remains unknown. The supervisors and their workers were stopped after preservation. Wave 5 records are in `wrong-binary-recurrence-wave5-preserved-2026-11-16/`; the Claude-owned process waiting on the trace test log was a monitor, not the launcher, and was stopped. Do not restart or accept this isolated command. Any future current-candidate run requires the adjacent `aa7b830c…` Harness and `a8cb7666…` Sim plus a verified supported trace option.

## Guarded execution plan

1. **Do not launch now.** Wait until D20 and all WP9 heavy jobs have exited and the process tree is confirmed clear. The isolated current-candidate build must not reuse/copy the earlier wrong Sim. Preserve any stale or partial output before cleanup; do not overwrite it.
2. **Freeze the candidate after source/trace work.** If the current candidate is to gain the missing SEA-10 trace, land/review that narrow read-only instrumentation before freezing; build Release Harness and tests together from the same clean source tree, then capture full source aggregate, pack, Harness, Sim, and test-runtime hashes. Verify the Harness actually contains both `--fief-siege-trace` and the newly implemented SEA-10 trace option, and the test assembly loads the same Sim hash. A pack match alone is insufficient.
3. **Freeze new seed blocks before outcome inspection.** Do not reuse the spent F-T5 natural seed list or WP-E5-12's reserved `33,000,000–33,000,079` holdout. Select and hash a fresh unscreened seed manifest disjoint from all prior WP5/WP8/WP9/D20/F-T5/WP-E5-12 manifests. The natural exposure block is diagnostic only; it must retain zero-exposure, censored, error, and early-end matches. Freeze autonomous provisional F-T5/SEA-10 pass criteria in WP-E5-11 before opening any new holdout; do not derive them from observed holdout outcomes.
4. **Execute serially against the one frozen tuple.** First run source-matched checksum-neutrality coverage for each trace flag. Then run the 130-minute natural Fief siege/crew trace cohort with `--fief-siege-trace`; run the SEA-10 natural route/cargo trace cohort only after the exact-source trade trace exists. One match per manifest seed; no targeted geography, grants, or seed replacement. For each run, record the exact shell command, per-seed exit code, stdout/stderr hashes, replay/final checksum where available, and candidate fingerprints in immutable metadata.
5. **Analyze exposure before interpretation.** For siege report investments, active/eligible turns, crew-adult seasons by base/DEF-09 strata, fire attempts, paid shots/powder, breach, attrition, outcome, relief, and horizon censoring. For SEA-10 report eligible route attempts, positive and rejected actual water routes, pre/post-KEY-6/SEA-10 status, shipment volume and cap, repeated shipments, delivery/payment, voyage losses, and censoring. No zero-exposure row is evidence of a passing rate or throughput target.

The current exact command for the existing siege trace, once a fresh manifest and unique output directory are frozen, is:

```sh
dotnet /absolute/frozen/src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll \
  fief-match --pack /absolute/frozen/numeric-e5.pack --seed <manifest-seed> \
  --minutes 130 --fief-siege-trace
```

Do **not** append `--fief-trade-trace` until the active-source harness implements and regression-tests it; that switch exists only in the invalid isolated artifacts inspected here. The current F-T5 cohort wrapper is also not a ready launcher for a rerun because its manifest and default output are already consumed. A new manifest and no-overwrite runner must be prepared only after the candidate is frozen.

## Remaining gates

- Host idle and every current-source WP9/D20 job reconciled.
- Exact current-source pack/Harness/Sim/test tuple frozen; no stale-binary path accepted.
- SEA-10 read-only trace and checksum-neutrality regression implemented in the current source; route eligibility, delivery and shipment-cap semantics covered.
- Fresh disjoint manifests and unique output roots frozen and hashed before any results.
- Positive natural siege/crew and actual water-route exposure observed; all zero-exposure/censored/error rows retained.
- Autonomous provisional F-T5/SEA-10 criteria frozen and passed, and WP-E5-11 promotion gates passed, before any E5 row is promoted or ordinary Fief selection is enabled.

No source, test, runner, manifest, pack, candidate, or simulation output was changed or generated by this audit. It records gaps and guards only.

### SEA-10 instrumentation implementation follow-up — 2026-11-16

An isolated source patch now adds the opt-in `--fief-sea10-trace` report path. It emits per-civilization KEY-5/KEY-6/SEA-04/SEA-10 status each season, identifies land/water/water-locked/unreachable route states using a diagnostic physical-water path even when Fief sailing is still correctly locked, and records caravan attempt rejection reasons, requested/capped/taken/carried cargo, shipment and compact IDs, prior deliveries, delivery/payment/return, raid loot, voyage outcome, and arrival timing. Trace state is a nullable observer outside serialization and simulation checksums. The opt-in provenance runner requires source-file manifest plus source aggregate, pack, Harness, and Sim hashes, unique seeds, and a new output directory; it creates files exclusively and has Python mock-only guard tests.

This patch is isolated and has not been applied to or built from the active candidate. The four Python runner tests pass in the isolated copy. No C# build, simulation, or cohort was run due host load. The requested C# checksum-neutrality regression is added but remains unexecuted. Before use, rebase this patch onto the active candidate while preserving concurrent Harness diagnostics, run focused source-matched tests/build when the host is clear, then freeze the exact artifact tuple. SEA-10 exposure and final numeric acceptance criteria remain open; the trace does not change routes or cargo mechanics and cannot by itself close either gate.

### Additional isolated preflight preparation — 2026-11-16

A fresh 20-seed F-T5 natural siege diagnostic seed list is prepared at `m12-e5-8-f-t5-natural-seeds-2026-11-16.txt` (SHA-256 `d30825f653a1a39c851a7a5f1d64a640ce0c69fee8da70c95c619d0a51edc29b`). Its preflight record is `m12-e5-8-f-t5-natural-preflight-2026-11-16.json`; the proposed output path is new and was confirmed absent. Seeds were selected before any outcome inspection, and a text search of the available Projects/CivSim, Codex worktree, and attachment trees found no prior references to the selected block. The seed list is disjoint from the consumed 20-seed F-T5 list and inspected WP8 SEA-10 reruns. Recheck the complete WP5/WP8/WP9/D20/WP-E5-12 seed inventory at final execution freeze. The record intentionally leaves source/pack/Harness/adjacent-Sim hashes null: this patch changes source, and no matching Release build may be produced while A5 holds the host.

The shared `tools/fief_trace_runner.py` now supports `--trace-kind siege` in addition to SEA-10 and territory. It freezes the canonical source manifest/aggregate, pack, adjacent Release Harness/Sim, seed-file hash, seed values, runner path, duration, and selected trace flag before a run; it rejects stale hashes, duplicate seeds, non-adjacent Sim binaries, or an existing output directory. The siege trace already reports engine/cannon counts, powder paid, assigned crews and attrition, DEF-09 state, breach, outcomes, terminal inventory, relief/capture/sally events, and right-censor reasons. The runner now appends per-seed status/exit-code events and an explicit terminal run status on success, nonzero exit, or catchable interruption, while retaining exclusive output creation. No preflight execution or match was launched. Existing 20-seed F-T5 evidence remains inconclusive because it had no persistent investment, crew-season, shot, or DEF-09 exposure.

### Active-tree reconciliation — 2026-11-16

The active `codex/e5-fief` tree now contains `--fief-sea10-trace` in `src/CivSim.Harness/IronMatchCommand.cs`, the SEA-10 observer in `src/CivSim.Sim/Politics/PoliticsSystem.FiefSea10Trace.cs`, and `tools/fief_trace_runner.py --trace-kind sea10`. Do not use the stale `--fief-trade-trace` spelling mentioned in preserved isolated-run reports. The observer covers KEY-5/KEY-6/SEA-04/SEA-10 milestones; route rejections; dispatch, delivery, raid and voyage outcomes; requested/capped/taken/carried/delivered/paid/returned/lost cargo; and land/water/water-locked classification. `FiefShipCargoTests.Sea10_report_trace_is_checksum_neutral` contains a fixture-level parity check for dispatch and delivery on water with KEY-6/SEA-10. These are instrumentation and fixture checks only, not natural SEA-10 throughput evidence.

The observer serializes cargo amounts in the good's fixed-point units and records the SEA-10 state, but does not explicitly emit normalized liters or the active 12/24 L capacity. A natural-run analyzer must convert each `good` consistently and compare both the capped and taken amount with the route's SEA-10 state; add explicit cap/liter trace fields and a focused assertion if that normalization cannot be derived unambiguously. A water shipment may also be lost/returned before delivery, so dispatched, delivered, returned and lost amounts must remain separate in the report.

As of this audit, the canonical source aggregate hashes to `5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733` (1,045 files, HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`). Its frozen diagnostic tuple is pack `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`, Release Harness `26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100`, adjacent Release Sim `b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe`. This tuple may support current-source diagnostic cohorts; it does not promote values or close natural-exposure/balance gates. The previous natural F-T5 sample remains the only preserved full cohort: 20/20 seeds ran on aggregate `87996d7a…` with zero persistent siege investments, paid cannon shots, powder breaches, assigned crew-seasons, or DEF-09 exposure. The fresh F-T5 manifest remains dormant pending the current tuple's diagnostic run. A separate 20-seed SEA-10 route-screen manifest is prepared at [m12-e5-8-sea10-natural-seeds-2026-11-16.txt](m12-e5-8-sea10-natural-seeds-2026-11-16.txt) (SHA-256 `1f63acfdc5330fede8d4875799aff0b4cd907708fe538cc4a8abed7e932e84dc`); its run preflight is [m12-e5-8-sea10-natural-preflight-2026-11-16.md](m12-e5-8-sea10-natural-preflight-2026-11-16.md). Neither current-candidate cohort has run.
