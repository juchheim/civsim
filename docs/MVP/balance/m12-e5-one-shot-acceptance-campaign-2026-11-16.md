# E5 one-shot acceptance campaign plan — 2026-11-16

**Status: planning only. No acceptance matches were launched.** This plan follows the request to run each final acceptance protocol once, with no repeated exploratory cohorts at closeout. It does not claim that any gate has passed.

## Current candidate boundary

- Current source anchor: `codex/e5-fief` at `2af599acdf543d37c11c61b3c141408e70da94dd`; current acceptance-workspace aggregate: `e5f991d94f5b4bf6a2b66a3c23603b1a9b96699dac042831d800e7826c886fc6`.
- The earlier 6,400-era-base D/P diagnostic pack pair was byte-identical across the 50 status changes, but it was built at `7f75617` and is not the current source freeze. The P pack SHA was `0f0106dc89e910a9559a0612bf42b7232f4085ace81b4261e1c343becbb81cff`. Rebuild/pin current binaries and test assembly from this source before acceptance. It is not an accepted promotion pack; all 50 canonical rows remain draft.
- The dated WP-E5-9 preflight points at stale `5ee659bd` source and binaries. Its output directories contain only placeholders. Rebuild and pin all acceptance inputs after the final source freeze; do not reuse its hashes or outputs as evidence.
- Keep the reserved WP-E5-12 seed block `33,000,000–33,000,079` untouched.

## One final campaign, with distinct protocols run once

1. **Natural Fief trace cohort:** run the combined 80-world, 130-minute runner once with compatible F-T1, F-T2, F-T6/F-T7, and F-T8 traces enabled. F-T1's fitting input must be disjoint from this held-out block. The old F-T1 holdout was used by a quarantined attempt, so allocate a fresh block. The proposed unused pools are 20 calibration seeds in `70,000,000–70,099,999` and 80 holdout seeds in `70,100,000–70,199,999`; treat these as candidates until the final collision audit and a superseding registration are written. The combined runner can collect compatible traces, but its technical support does not itself replace each gate's seed registration.
2. **F-T3:** one controlled, matched `iron-fuel --fief` validation cohort, preserving the strict rule that no mature rolling 12-season window exceeds 15%. Use the fresh untouched validation block and final-source pins; do not reuse consumed fitting/fresh blocks.
3. **F-T4 and WP7:** each gets one paired-state cohort with the respective required 180-minute FoodCap/capacity observations and matched City-year snapshots. F-T4 remains diagnostic until its tolerances are set; WP7 remains diagnostic where its provisional acceptance criteria have not been approved.
4. **F-T5 and SEA-10:** one natural trace leg each, with the existing candidate-reason trace for siege. A run with no qualifying siege or route exposure is inconclusive; do not repeat the same seed block to seek exposure.
5. **WP-E5-9 AI:** one sequential bundle after freeze. A1/A7 share a 40-world manifest (separate reports); A2's three opponents, A3, A4, B5-10, A5's 120-world/600-match rotation, A6's search plus finalist validation, and A8's fog-fairness runner retain their separate protocols and fresh output roots.
6. **Remaining release acceptance:** B5-03 determinism and B5-11 natural/granted performance are separate contexts; keep the performance host idle and run its two cohorts sequentially once. WP-E5-12 human review remains separate and uses its reserved block after WP-E5-11 promotion.

## Freeze and execution rules

Before starting the campaign, finalize code and candidate values, produce a clean frozen source tree and numeric pack, build adjacent Release Harness/Sim and the Release test assembly, and record hashes for every runner, analyzer, manifest, toolchain, and binary. Run the seed collision audit against the repository and external run/scratch roots immediately before registration. Use absent output roots and retain raw outputs, seed-level status, censors, and provenance.

Calibration may inform the final candidate before this freeze. Once the held-out campaign begins, do not tune or rerun against its seeds. If a gate fails, record the failure and stop; any later candidate requires a new held-out block and a separately authorized campaign. The one-shot campaign may contain multiple protocol-specific commands, but each cohort is launched once under the same frozen candidate.

## Gates not represented as numeric passes

- F-T4's weighted FoodCap/capacity bands remain diagnostic by user direction until tolerances are supplied.
- F-T6 has no approved dormancy cutoff; report its measurement descriptively.
- F-T7's provisional rate band is still a draft hypothesis; do not call it a pass without an approved criterion.
- Any natural F-T5/SEA-10 cohort with zero qualifying exposure is inconclusive.
- WP7's current provisional criteria require explicit approval before a pass can be claimed.

The campaign is ready to be converted to exact commands after the final source/candidate freeze and registration decision. Until then, this document is a planning artifact, not a runnable acceptance manifest.
