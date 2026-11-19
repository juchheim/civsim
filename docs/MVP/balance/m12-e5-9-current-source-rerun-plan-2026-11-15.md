# WP-E5-9 current-source AI acceptance rerun plan — 2026-11-15

## Frozen input candidate

The completed F-T7 held-out run used the candidate freeze in [the F-T7 ledger](m12-e5-9-f-t7-candidate-freeze-2026-11-15.json) and its independently analyzed [held-out report](m12-e5-9-f-t7-heldout-analysis-rerun-2026-11-15.md). That historical candidate used full `content/src/game` aggregate `e42e3605be2de8bd73a4cc049b0219cf92ed0a43994cf11064486958b2cb7144`, the numeric-status pack `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`, Release Harness DLL `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f`, and Sim DLL `3fa5f4d96f78d6427738523094ce24035bdb471e858e21aea5eaf8f66953f37b`. Earlier AI reports used `content+src` aggregate `fc3a3606b5092b08c73ec4f2ca20d449c8d122ecbd2750be27c7c6fa299c982a` over 446 files. These hashes identify historical evidence; they are not the current-source A1/A7 run inputs below. Recompute all inputs after any source, content, game, pack, or binary change before another gate run.

The F-T7 run completed all 20 frozen seeds; all exit codes, raw hashes, source/HEAD, pack, Release harness, Sim DLL, and manifest hashes were reverified before analysis. These exact inputs may be reused only while every hash remains unchanged; refreeze immediately before each AI cohort. Do not update policy thresholds or tune AI against held-out results.

## Existing evidence and missing current-source proof

| Gate | Existing result | Required rerun on one frozen source/pack/binary set |
|---|---|---|
| A1 | Historical result: 65/80 Warlord wins (81.25%) without a strict source aggregate; the later 9ddb attempt was aborted before output. **Current-source pass:** 65/80 (81.25%), 7dec aggregate, zero censors; [dated A1/A7 closeout](m12-e5-9-fief-a1-current-source-output-rerun-2026-11-15/report.md). | Complete: 40 mirrored Warlord/Council seeds, 80 matches; 65 wins, 81.25% against the ≥80% threshold. |
| A2 | Grower 75%, Builder 82%, Wanderer 75% on historical binaries. | 40 seeds / 80 matches per opponent; each Warlord win rate ≥70%. |
| A3 | 80/80 without settlement loss; 70% Warlord wins vs Rusher, historical binaries. | 40 seeds / 80 matches; retain both floors: ≥80% clean survival and ≥70% wins. |
| A4 | Chieftain 34% vs Warlord, historical binaries. | 40 seeds / 80 matches; Chieftain win rate 25–40%. |
| A5 | 120 seeds / 600 FFA matches; maximum archetype share 29.4%, historical binaries. | `ai` expands the 40 requested base seeds `20261019–20261058` to 120 world seeds `20261019–20261138`, then evaluates five seating rotations per world (600 matches). Preserve the expanded seed list and each world/rotation result; no archetype may exceed 30%. |
| A6 | 10,009-policy search, 16/16 finalists, 50% maximum, bound to the older 9ddb source and DLLs. | Re-run policy generation and all 10,009 one-minute screens against the frozen current source/pack, select a new diverse finalist set, and validate 16 finalists on a newly frozen, disjoint set of three 64-minute mirrored pairs each. Do not reuse the old pack, source/DLL hashes, screen seed or validation seeds. Keep the claim scoped to the newly searched candidate set; all finalists must validate and remain ≤55%. |
| A7 | Historical cohort: six telemetry counters were zero. **Current-source pass:** all six counters zero on the same 40-seed schedule; [dated A1/A7 closeout](m12-e5-9-fief-a1-current-source-output-rerun-2026-11-15/report.md). | Complete: 80 matches / 160 AI seats; all six counters zero. |
| A8 | Focused unseen-state fixture coverage passed 7/7 historically. A current-source no-build `FiefFogFairnessTests` filter passed 2/2; that proves fixture behavior only. The test output and source/test-binary identity are in [the current-candidate fixture check](m12-e5-9-fief-a8-current-candidate-fixture-check-2026-11-15.md). | Run broad whole-match unseen-state invariance on current frozen source, including deposits, powder stock and fleets, with visible-input positive controls. This gate still lacks a completed whole-match result. |

Historical A1–A7 outcomes do not become current-source proof merely because thresholds are unchanged. A1 and A7 now have current-source evidence on the 7dec aggregate; the current A1/A7 hashes and logs are in the dated closeout. A2–A6 still need current-source reruns; A6 is explicitly stale because its frozen `content+src` aggregate differs from the current one. B5-10 also separately requires Legend to achieve ≥60% against Warlord and at least 80 mirrored duels; follow its governing protocol and preserve that result independently.

## Frozen rerun inputs

Use the existing frozen seeds and schedules rather than selecting seeds by outcome:

- A1–A4 and A7: [mirrored seed manifest](m12-e5-9-fief-a1-seeds-2026-11-15.tsv), checked against the command/protocol in the [AI readiness report](m12-e5-9-fief-ai-acceptance-readiness-2026-11-14.md).
- A5: use the [40-world base seed list](m12-e5-9-fief-a1-seeds-2026-11-15.tsv); preserve the 3× expanded 120-world list and five seating rotations per world in the rerun artifacts.
- A6: keep the [policy-generation and validation method](m12-e5-9-fief-a6-run-schedule-2026-11-15.md), but create new screen and mirrored validation seed manifests disjoint from prior A6 runs and other E5 cohorts. Search again and select new finalists on the frozen current source/pack; record all generated policies, finalist selection and validation artifacts. The old A6 manifest/pack/hashes describe historical evidence only.
- A8: run the whole-match invariance harness on the same frozen pack and source. Preserve each unseen-state perturbation, visible control, decision result, seed, and artifact hash. Existing fixture tests do not substitute for this run.

Use the current Fief command with a 64-minute horizon and `--mode fief` for the mirrored policy cohorts, omitting `--grant-e5` unless that exact frozen protocol specifies it. Capture separate metadata with the source aggregate, HEAD/tree, pack, Harness and Sim hashes; seed/policy manifests; exact command lines; outputs; and any censors. Repeat or mark a gate failed if inputs change mid-run. No numeric content row is promoted by this rerun plan.

## Current closure

WP-E5-9 remains open. A1 and A7 passed on the current source aggregate `7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6`; see the dated A1/A7 closeout. A2–A6 still need current-source proof; A8 needs a whole-match result; the separate Legend/duel requirement also remains to be recorded. The fixture and tool plumbing are useful evidence but are not whole-match acceptance.
