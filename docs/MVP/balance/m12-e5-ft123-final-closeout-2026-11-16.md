# WP-E5-5/6/7 F-T1 / F-T2 / F-T3 closeout on the final dev source (2026-11-16)

> **Supersession note (2026-11-16):** Do not use this document's F-T1 “PASS” claim as acceptance evidence. The 7,300 holdout attempt was subsequently quarantined because its execution provenance did not establish the required clean/current source tuple; the later quarantine record in [the F-T1 preflight](m12-wp5-fief-ft1-final-code-preflight-2026-11-15.md) governs. Do not analyze or reuse that output. The F-T2 and F-T3 results below are source-bound mechanics diagnostics only; neither closes whole-match WP-E5-11 acceptance. A fresh disjoint F-T1 acceptance block is required after the final source/candidate freeze.

All 50 E5 rows stay `draft`; the repo era base stays 6,000; ordinary/release Fief selection is untouched; F-T4 is not touched. The 7,300 base exists only in a scratch pack. Source: dev copy `/Users/ejuchheim/Projects/CivSim/civsim-e5-dev` (derived from the dirty `civsim-e5` snapshot, HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`, snapshot aggregate `7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6`).

## Hashes

| Item | Value |
|---|---|
| Final source aggregate (content/, src/, game/; 1,201 files incl. build-ignored-free walk) | `9e70ca453ead9c67938d6d5ca909c760929f8d114c78593e8ec7d0b62e8a5411` (per-file record `m12-e5-final-source-provenance-2026-11-16.json`) |
| Same source before the test-only edit (`FiefPowerTests.cs`) used by the F-T2 and 6,000-base cohorts | `07da6c4381ba152a30a3efd5fba8162404988e4744f88bfb7eb228439977f98f` (only `src/CivSim.Tests/FiefPowerTests.cs` differs; behavior binaries identical) |
| Release Harness DLL | `94e3707c69bec6e4ce4044f3dd0f6ea9ddf42ae03ab8e6a68ab6bb229c5ac1ab` |
| Sim DLL | `2de47f0ee7fb246f2eeec62a0a42504eb34818d744bca923e4e5705643b4d35b` |
| Numeric pack, base 6,000 | `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a` (content unchanged from the snapshot) |
| Numeric pack, base 6,700 | `a99d2a84ea34d864d9005da106ab8960dca132f4f36442f4a4ed01d7e19bad0c` |
| Numeric pack, base 7,300 (frozen candidate) | `c4b27bbb6b20aaad0fc18a6555feab42ea08409bef9247d41a66895b27b04a86` |
| F-T1 calibration manifest (20 worlds) | `ed96e046472d8fc84bb9251cd9ccd62c10e7e937978f52d7b27339af00c1b19e` |
| F-T1 holdout manifest (80 worlds), first and only use | `c21765567392eb2d0b68f10bd04ddf0681dde72d35f27fdf6d6002803d306526` |
| F-T2 natural seeds | `1b5bcf7bf840101c896b7dbf3bd6c81cb735de7bc2172c3e812265ef534715bc` |
| F-T3 fresh / untouched seeds | `921e67fd…` (Random(20261123), 20,720,000-20,729,999) / `m12-e5-ev-ft3-validation2-seeds-2026-11-16.txt` (Random(20261124), 20,730,000-20,739,999) |
| Candidate freeze, written before the holdout | `m12-e5-ft1-candidate-freeze-2026-11-16.json` |

Each cohort's `metadata.json` records source aggregate before and after (equal in every run), pack, harness and Sim hashes before and after, manifest hash, per-shard commands, exit codes and raw-file hashes. Raw holdout output is archived in `m12-e5-ft1-holdout-7300-output-2026-11-16/`.

## F-T1 (D21) — PASS on the 80-world holdout

Calibration block, frozen source (fit only here): base 6,000 median 22.50 (73/80 complete); 6,700 median 23.10 (74/80); 7,300 median 24.90 (73/80). Candidate 7,300 frozen, then the untouched 80-world holdout run once: natural Fief, no `--grant-e5`, 130 minutes, 6 shards, all exit codes 0, all 80 matches ran to the horizon with no early end.

- **Complete 283/320 (88.4%)** against the 256/320 floor; right-censored after KEY-5: 24; KEY-5 not reached: 13; no seat imputed.
- **Completed-seat median 24.30 min** (band 22.5-27.5); p10/p25/p75/p90 21.02/22.75/26.20/28.38; min/max 17.40/31.50.
- By archetype (complete/censored/no KEY-5; median): Builder 77/3/0 24.40; Grower 70/6/4 25.00; Warlord 64/11/5 25.45; Sage 72/4/4 22.05.

Caveat: D21 is an autonomous interpretation, not user-confirmed. The 7,300 base is not applied to the repository; promotion with all 50 rows (WP-E5-11) would also move the content golden and docs 02/07c.

## F-T3 — PASS on the unchanged rules, three seed blocks (fixture `iron-fuel --fief`)

Findings and cause in `m12-e5-ft3-ft2-fixes-2026-11-16.md`. On the frozen snapshot source the rule failed (4/33 and 7/39 pairs, max 16.1%). Fixes: SOC-11 craft loss no longer taxes ore reduction and refining (Fief only), and the Fief furnace bloomery labor factor fitted from x2 to x1. Final: fit block 33/33 (max 14.2%), fresh 39/39 (max 14.8%), untouched 38/38 (max 14.6%); fuel cutoff 30/30, 37/37, 36/36 eligible; zero furnace-attributed forest crossings in 39/40/40; censored (no mature window, not passes) 6, 1, 2. The factor was chosen after seeing the first two blocks, so only the third is out of sample. Whole-match F-T3 balance remains part of WP-E5-11.

## F-T2 — PASS at the seed-weighted level; 2 capped-town windows below 60%

Natural 16-seed cohort on the frozen source (`07da6c43…` build): 505 complete town windows, 503 at or above 60%, 26 censored windows excluded; every seed weighted 78.7-85.5%; 30 windows with two same-type sources. Misses: seed 20701208 s5 58.0% and seed 20705710 s5 50.6%, both already at two windmills (the confirmed cap). Cause of the earlier gap: wanted second windmills were unfunded behind other builds and, late, short of dressed blocks; fixed by queue priority for a coverage-deficit windmill and a block-free timber post mill (18 wood, 1 wheel, 24 work; draft cost approved by the user). Previously failing seeds: 156/156 windows at or above 60%. The cap and the 260 residents per source are unchanged; closing the two capped windows would need a rule change (user decision). Also unchanged: coverage is an adult-equivalent capacity proxy, not flour mass.

## Inert effects (WP-E5-6/7 audit follow-up)

Built: ART-09, CER-10 (holder-gated), NUM-08, TRD-08, GOV-10, MED-07 elder hazard (`m12-e5-6-7-inert-effects-built-2026-11-15.md`). Not built: TRD-09 `trade_profit` (ledger only), SOC-11 apprentice speed (counter only), GOV-11 staff/structure dependence (design decision), CER-09/MET-11/CHM-06/MIN-08/FIR-10 (no owner). No acceptance gate exists for WP-E5-6/7 beyond F-T6/F-T7; WP-E5-7 has no F-xx row.

## Tests

Full Release suite on the earlier final-source build: 1,436 of 1,438; after fixing a stale windmill test, `FiefPowerTests` 15/15. Remaining failures predate this work or are load-sensitive: `IronChainTests.Iron_goods_are_hashed_and_saved_only_in_the_Iron_chapter_and_round_trip` (fails identically on the unmodified snapshot) and `ViewStoreTests` frame time (passed on the final-source run). A bug of my own (SOC-11 craft loss leaking into Iron) was caught by `IronCraftTests` and fixed before the final source.

## Results invalidated by source changes (do not cite)

- Everything run on `7decf87…` (the snapshot) as F-T1, F-T2, F-T3 evidence, and the earlier 6,000/7,900/8,600 F-T1 cohorts, the F-T6/F-T7, A1-A8 and F-T4 runs: all predate the inert-effect build, the F-T3 and windmill fixes and the base fit, so they must be rerun on the final source before WP-E5-9/11/12 acceptance. F-T4's final-integrated-code rerun is owned elsewhere and now also needs this source.
- The evidence-copy cohorts (`civsim-e5-evidence`) are superseded; their partial results are retained only as diagnostics in `m12-e5-ft123-closeout-2026-11-15.md`.

## Remaining evidence gaps

F-T2 capped-town decision; F-T4, F-T5, F-T6 whole-match and F-T7 on the final source; A1-A8 and B5-03 determinism (1,000 matches, cross-host); B5-10 AI, B5-11 performance, B5-12 comprehension; WP-E5-6/7 balance gates (none defined); the unbuilt items above; promotion of all 50 rows and the 7,300 base together (WP-E5-11).
