# WP-E5-11 technical gate audit and preflight — 2026-11-16

This read-only audit covers technical readiness for B5-01/02/03/11/13 and the promotion boundary. It does not close balance gates, promote E5 rows, run simulations, or approve release. The separate WP-E5-10 UI/save audit is recorded in `m12-wp10-fief-view-audit-2026-11-15.md`.

> **Historical preflight only.** Every source, pack, binary hash, and literal run command in this document is a historical snapshot and is **not runnable for acceptance**. The source has advanced beyond both `5ee659bd…` and `d3908981…`; WP-E5-7 work is still being integrated, and the F-T1 6,700 base remains a hypothesis. Do not launch a gate from an example below. Keep the canonical rows draft and regenerate all preflights only after the final code/value freeze.

## Gate status

| Gate | Latest evidence | Status and remaining work |
| --- | --- | --- |
| B5-01 isolation | Current-source E5 vs current-main Bronze (45,000 ticks) and Iron (66,000 ticks) component-checksum CSVs match byte-for-byte; focused isolation filter 24/24; strict content validation 0 errors/0 warnings. Source/runtime hashes are recorded in `m12-wp11-b5-01-isolation-canary-2026-11-15.md`. | Closed for the tested legacy-mode isolation requirements. This does not establish whole-match E5 balance or promotion readiness. |
| B5-02 completeness | V22/strict validator covers all 50 E5 nodes; the plan records completeness as passed. | Typed completeness is done. Changing all 50 rows from draft to numeric is still a coordinated promotion action, gated by WP-E5-11 acceptance; no row is promoted in this audit. |
| B5-03 determinism | Frozen 1,000-seed manifest SHA `1c05d1be1edf3b6b4cad9bcbf35503ab6b454133515388cc8f42ca82a1ad3ce6`; runner, replay indexer, shard merger, comparator, and focused tests exist. | Open. No 1,000-seed cohort, checkpoint-continuation cohort, merged index, or Windows parity result is recorded. The runner requires a clean committed freeze; this checkout is dirty. Final natural pack/source/binaries must be refrozen after promotion, then run macOS arm64 and Windows x64 with 1,000/1,000 replay and continuation passes and exact per-seed/per-tick index parity. See `m12-e5-9-b5-03-determinism-preflight-2026-11-15.md`. |
| B5-11 performance | `fief-perf` instrumentation measures per-tick time, GC pause, allocations, and own-view season samples after each seat's KEY-5. `FiefPerformanceCommandTests` previously passed 3/3, including checksum parity against ordinary stepping. | Open. No natural or E5-granted multi-seed performance cohort has run. The old preflight's source aggregate `e42e…` is superseded and its recorded `54e2…` Harness hash is stale. The two 20-seed manifests remain valid and disjoint; freeze the final pack and both adjacent Release DLLs again at launch. |
| B5-13 integration | Plan requires full Release suite and strict validation, isolated/explained golden changes, and explicit user approval to merge. | Open. Current full-suite/strict-validation closeout against the final clean promoted candidate is absent. User merge approval remains a human gate. |

## Current technical snapshot

At this audit, the exact working-tree measurement tuple was reverified as:

- HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`
- `content/src/game` aggregate `7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6`
- scratch numeric-status pack `/private/tmp/civsim-fief-playtest-20261115-visual-refresh/numeric-e5.pack`, SHA-256 `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`
- Release Harness `src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll`, SHA-256 `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f`
- adjacent Release Sim `src/CivSim.Harness/bin/Release/net10.0/CivSim.Sim.dll`, SHA-256 `a8cb7666042cf6651a6b9558b3d6db7b1f00566b5cc30041077e67b86540ad50`

The numeric-status pack is a measurement input, not evidence that repository rows have been promoted. These hashes identify the current dirty working-tree candidate; they are not a B5-03 final clean-commit freeze. Recompute the complete tuple immediately before any run and after source changes settle. Evidence from another source aggregate or loaded DLL does not qualify for this tuple.

## B5-11 instrumentation and criteria

`FiefPerformanceCommand` is opt-in; ordinary simulation paths are not instrumented. It executes a five-minute warm-up, then times each `SimRunner.Advance(1)` tick, collecting per-seed and pooled mean/p50/p95/p99/max. The recorded GC gate is total process pause time divided by measured tick count (≤16 ms/tick); the mean-tick gate is ≤4 ms. View/population samples are own-view per seat/season after own KEY-5. They include the warm-up span, while timing excludes warm-up. CSVs report population and view-frame p95 and count rows over the 600-figure-per-civilization diagnostic value. The 600 value is not a runtime cap; if p95 exceeds it, inspect and address view representation cost before claiming the budget is acceptable. Do not downsample simulation population.

The natural and E5-granted stress manifests are 20 unique seeds each, disjoint, and outcome-unscreened:

| Cohort | Manifest | SHA-256 | Protocol |
| --- | --- | --- | --- |
| Natural Fief | `m12-wp11-fief-natural-seeds-2026-11-15.txt` | `f787766662b46319f7f0807eacd27592aa8c2af4163f366d52ddec07a3e3a786` | 130 minutes, 5 warm-up minutes, omit `--grant-e5` |
| E5-granted stress | `m12-wp11-fief-grant-e5-seeds-2026-11-15.txt` | `b5d541d0f820ff3fc3979d2ff5158c15b9bc24ac61fa7e11af4196eec1f006b6` | 130 minutes, 5 warm-up minutes, include `--grant-e5` |

Run them sequentially in an otherwise idle process. Do not launch while D20, AI, Fief balance/performance, or test workloads are active. `run_frozen_e5_cohort.py` now provides a future guard: it requires expected HEAD/source/pack/Harness/Sim/manifest hashes, refuses an existing output root, records provenance before and after, logs command output, and hashes all raw artifacts. Use the exact frozen tuple captured at launch; the current values above must not be reused after a source/pack/binary change.

Natural cohort command shape after host clearance and final tuple freeze:

```sh
python3 docs/MVP/balance/tools/run_frozen_e5_cohort.py \
  --repo /Users/ejuchheim/Projects/CivSim/civsim-e5 \
  --pack /private/tmp/civsim-fief-playtest-20261115-visual-refresh/numeric-e5.pack \
  --harness-dll /Users/ejuchheim/Projects/CivSim/civsim-e5/src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll \
  --sim-dll /Users/ejuchheim/Projects/CivSim/civsim-e5/src/CivSim.Harness/bin/Release/net10.0/CivSim.Sim.dll \
  --manifest docs/MVP/balance/m12-wp11-fief-natural-seeds-2026-11-15.txt \
  --expected-head bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c \
  --expected-source-aggregate 7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6 \
  --expected-pack-sha256 f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a \
  --expected-harness-sha256 aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f \
  --expected-sim-sha256 a8cb7666042cf6651a6b9558b3d6db7b1f00566b5cc30041077e67b86540ad50 \
  --expected-manifest-sha256 f787766662b46319f7f0807eacd27592aa8c2af4163f366d52ddec07a3e3a786 \
  --output-dir /Users/ejuchheim/Projects/CivSim/civsim-e5/docs/MVP/balance/m12-wp11-fief-natural-output-2026-11-16 \
  -- dotnet /Users/ejuchheim/Projects/CivSim/civsim-e5/src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll \
  fief-perf --pack /private/tmp/civsim-fief-playtest-20261115-visual-refresh/numeric-e5.pack \
  --seeds-file docs/MVP/balance/m12-wp11-fief-natural-seeds-2026-11-15.txt \
  --minutes 130 --warmup-minutes 5 \
  --output-prefix /Users/ejuchheim/Projects/CivSim/civsim-e5/docs/MVP/balance/m12-wp11-fief-natural-output-2026-11-16/perf
```

For the stress cohort, use a new output root and its manifest/hash, and add `--grant-e5` to the wrapped harness arguments. Preserve both CSVs, wrapper metadata/log, full seed lists, hashes, and exit codes. The command only produces measurements; calculate pass/fail against the unchanged thresholds and explain any over-budget p95 view result. Do not tune numeric content from these performance measurements.

## Minimum sequence before WP-E5-11 technical closeout

Promotion and the clean-checkout requirement are reconciled by using an isolated promotion candidate. The canonical checkout stays draft until every technical gate passes; no acceptance run should change its statuses.

1. Finish and review all code and numeric fit hypotheses while all 50 canonical E5 rows remain `draft`. Refresh the source, pack, Release Harness, adjacent Release Sim, and every manifest preflight. Freeze this draft-status state as a clean commit **D** only in an isolated candidate branch/worktree; preserve the user's canonical dirty checkout.
2. Create a separate clean promotion-candidate worktree/branch **P** from D. Change only the 50 E5 status fields from `draft` to `numeric` in P. Before any runs, verify that P's final numeric pack is byte-for-byte identical to the numeric-status pack generated from D by `build_e5_status_pack_pair.py`; record P's HEAD, tree, content/src/game aggregate, pack, Release Harness, and adjacent Sim hashes. P remains isolated and is not merged or fast-forwarded to the canonical branch.
3. Run the final F-T and A1–A8/Legend cohorts, B5-03's 1,000-seed macOS arm64 replay and 750-tick continuation protocol, and both B5-11 20-seed cohorts against the exact clean P tuple. Run the B5-03 Windows x64 parity leg against the same bytes and compare all 1,000 per-seed/per-tick records to the canonical macOS index. Each runner must pin source, pack, binaries, manifest, output root, and before/after hashes. If source or a numeric value changes, discard P and create a fresh candidate; do not reuse affected manifests or output roots.
4. Run B5-13's full Release suite, strict validation, and golden review on P; document every intentional golden change. Technical pass is necessary but does not itself authorize merging.
5. Only after all technical gates pass, fast-forward the unchanged P commit to the canonical branch and verify its commit/tree/content/pack/binary identities. Keep promotion atomic across all 50 rows. The explicit owner merge approval remains a human decision. WP-E5-12's reserved 80-seed natural holdout and eight-person comprehension study remain post-WP-E5-11; Windows x64 execution also requires an external Windows host.

This resolves the apparent order conflict: the canonical branch remains draft, while the isolated P worktree tests the exact numeric-status Release pack that would result from atomic promotion. Do not treat D or P as accepted until all pinned technical gates pass.

At the audit, the host still had D20, AI, WP8 siege, and test processes active. No B5-03 or B5-11 cohort was launched, no build/test was performed for this audit, and no simulation/source content was changed.

## Superseding candidate applicability note — 2026-11-16

This audit's `7decf87…` tuple and the later `5ee659bd…` tuple are historical diagnostic snapshots, not acceptance freezes. After the A8 harness source was added, the workspace aggregate became `d3908981396418d20de1956179c81a36a4cf2a14a1dd9128c3081995a865592a` (1,046 scoped files); the 5ee pack/Harness/Sim tuple no longer matches the source identity. The current working tree has advanced further while WP-E5-7 is revised, and no final tuple exists. Treat all older pins and command examples above as non-runnable; regenerate them only after the final candidate is clean and frozen. Keep all 50 canonical rows `draft`, ordinary/Release Fief selection gated, the WP-E5-12 holdout unused, and the participant study unrun. Windows parity remains an external-host gate.

## Promotion and cohort-runner hardening — 2026-11-16

The dirty target checkout now includes three fail-closed tooling fixes: promotion preparation binds the report to exact manifest bytes and a content-tree hash and aborts if either changes during staging; the B5-11 wrapper validates that its command invokes the pinned Release Harness `fief-perf` with the exact pinned pack, manifest, horizon, warmup, and fresh output-root prefix; and B5-03 requires the recorded Sim DLL to be adjacent to the pinned Harness DLL. The promotion, B5-11, and B5-03 fixture checks pass 19/19 in this checkout. No acceptance cohorts were run and no rows were promoted. The checkout remains dirty, so none of these changes creates a runnable final freeze.

## Focused runner revalidation — 2026-11-16

On `codex/e5-fief` at HEAD `ceaadef`, with the worktree still dirty and before any final freeze, I reran the relevant guard/fixture suites after the runner hardening:

| Suite | Result |
| --- | ---: |
| `test_run_b5_03_determinism.py` | 14/14 |
| `test_run_frozen_e5_cohort.py` | 7/7 |
| `test_run_wp_e5_12_whole_match.py` | 4/4 |
| `test_wp7_trace_runner.py` | 16/16 |
| `test_prepare_e5_promotion.py` | 15/15 |

These are 56 fixture tests for runner refusal, provenance, and expected command construction; they do not execute acceptance matches. They do not close B5-03, B5-11, B5-13, or the WP-E5-12 whole-match gate. All 50 E5 catalog rows were re-read as unique and `draft`, and the Fief launch override remains under `#if DEBUG`; this does not open ordinary or Release selection. No match cohort was started and no row was promoted. Regenerate pins after all concurrent source edits settle.

## Dirty-integration Release verification — 2026-11-16

After the SOC-11, TRD-09, golden, and A8-validator edits were present, `dotnet test src/CivSim.Tests/CivSim.Tests.csproj -c Release --no-restore --logger "console;verbosity=minimal"` passed **1,470/1,470**. The first full pass exposed two TRD-09 assertions that incorrectly assumed full research adoption immediately after unlock, plus the expected content-pack golden mismatch from the WAR-12 “per shot” wording change. The assertions now use the actual adoption-scaled research multiplier and verify the realized margin once; the committed content-pack summary now matches the rebuilt 519-project pack (396,145 bytes, SHA-256 `144e53f9e835a6638e96b01de47d5857dd6be421c8973b8d521695328dfb7fc7`).

This is full-suite evidence for the current dirty, unpromoted integration checkout, not a clean final P candidate. It does not close B5-13's final-candidate requirement or any natural whole-match, determinism, performance, Windows-parity, human-study, or owner-approval gate. No acceptance cohort ran and all E5 catalog rows remain draft.
