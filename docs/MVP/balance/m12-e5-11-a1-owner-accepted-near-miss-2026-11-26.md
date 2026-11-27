# A1 owner-accepted near miss — 2026-11-26

## Disposition

**Recorded outcome: PASS by explicit owner exception for E5 release acceptance.** The measured A1 result remains a failure against its nominal threshold: Warlord won **59/80 mirrored matches (73.75%)**, while the written threshold is **at least 64/80 (80%)**. This is an owner-approved exception, not a revised statistical result or a change to the general A1 threshold.

The owner directed that if Astra found no defensible solution, the 73% result should be accepted as passing because it is close to 80%. Astra reviewed the pinned candidate and prior experiments, found no evidence-backed low-risk change worth another tuning cycle, and recommended recording this as an exception while preserving the raw failure. A second independent Astra review on 2026-11-26 reached the same conclusion after recounting the match outcomes and inspecting the early-loss trace. No further A1 tuning run is justified without a gameplay change and a new independent cohort.

## Evidence

- Candidate commit: `c3aa4e063244748ffb1f951050ded4003ce8262a`
- Candidate source aggregate: `701d31e084c03e9320f0de57f61abca57c4fa566aa4fca98e6a3d9c5e3e92503`
- Numeric pack SHA-256: `564b9e77b065441fc25b6137c37e6fd71b8c349bf82638199df3beaae37c2ebe`
- Sim assembly SHA-256: `a85cc888b53ef7deb2dfe85fb49218eb62a03a0d51da620205db34ff224234bc`
- Run metadata: `/private/tmp/wp-e5-11-current-source-campaign-20261123/ai/m10-core-and-legend-20261123-01/run_metadata.json`; completed 2026-11-24 with unchanged before/after source pins.
- Raw gate output: `/private/tmp/wp-e5-11-current-source-campaign-20261123/ai/m10-core-and-legend-20261123-01/raw-output.txt` (SHA-256 `1b8d46937175ee0c2e1f0d494e7415741469a629a18f9fcbfd485f7fbfc468b5`).
- Detailed mirrored-match outcomes and losses: `/private/tmp/wp-e5-11-a1-full-baseline46-details.txt`.

The raw run records `A1: 74% over 80 matches FAIL`. Keep that output unchanged. The official E5 disposition is the owner-approved exception above.

## Astra's assessment

The 21 losses mostly reflect early growth and expansion: 20 have a Dominion deficit and 19 have a Thriving deficit. One difficult loss reaches no captured settlements, so it does not point to siege or arms as the common cause. The annual trace shows the deficit already present by season 120, before key early governance research is completed. The measured GOV-02 preferences of 36 and 40 both produced 58/80, while the baseline 46 produced 59/80. No independently verified bonus-50 result is relied on here. A frontier-founding bonus adjustment also remained at 74%, while small conquest-floor and frontier-weight screens were unchanged.

A broader AI change that values early research by expected population, fission, and accumulated land income could be explored later, but the present evidence does not identify a reliable rule or parameter. That is not a sound basis for reopening and retuning the whole AI gate campaign during this closeout.

The fresh review confirmed 20 of 21 losses had a Dominion deficit and 19 had a Thriving deficit. In seed `80000034`, the Warlord was already behind by season 120 in Dominion, population, and owned habitable weight, with no settlements captured or lost. The previous tested GOV-02 valuation changes produced 58/80 at bonuses 36 and 40 and 59/80 at 46; the frontier bonus screen stayed at 74%. These results do not identify a defensible low-risk adjustment. The separately claimed GOV-02 bonus-50 artifact was not independently located during this review and is not treated as fresh evidence.

**Final Astra re-review on 2026-11-26:** The closeout candidate at `403a30d` plus its pending research edits had no new A1 cohort. The 120→180-season B29 report horizon does not influence plan choice; the physical-defense capability and queued-defense edits target B28, not the measured early-growth A1 losses. Astra found no evidence-backed small A1 fix and reaffirmed the official owner exception. It flagged a separate coupling in the newer `4ecde52` candidate: queued physical-defense construction raised the defense-research urgency value that also guards GOV-02's calm-frontier posture bonus. The subsequent `origin/main` change separates research urgency from posture valuation. The early M10 attempt on `4ecde52` was stopped before any acceptance result; its partial log is not acceptance evidence. The measured A1 result remains 59/80 on `c3aa4e0`, not a current-source measurement.

## Scope

The original exception applies to the pinned E5 candidate above; the current closeout candidate extension is recorded below. Neither disposition claims A1 met the nominal 80% threshold or waives other AI or technical gates. Later diagnostics on the pre-integration `0aa3af8` candidate passed B28 at 18/19 and B29 at 31/31, but those short diagnostics are not final current-source acceptance evidence; rerun the applicable gates on the merged freeze.

## Current closeout candidate disposition — 2026-11-26

**A1 is officially recorded as PASS BY OWNER EXCEPTION for closeout candidate `0f7bdd2a182568425f5b1c8531a1442513ca6008`.** The measured outcome remains 59/80 (73.75%) from candidate `c3aa4e0`, below the unchanged 64/80 (80%) nominal threshold. No fresh A1 cohort is claimed for `0f7bdd2`.

The owner directed that, if Astra found no evidence-backed solution, the near-miss result should be officially accepted. Astra re-reviewed the current closeout source, prior A1 results, and the candidate's ResearchSpark changes and found no defensible scoped tuning change likely to add five wins without risking other gates. The existing measurement is therefore carried forward as an explicit exception on this candidate; the raw failure and nominal threshold remain intact.

- Current closeout commit: `0f7bdd2a182568425f5b1c8531a1442513ca6008`
- Current closeout tree: `893aefd5123fc663a2668a28ef022bcd1d1cf329`
- Current source aggregate: `c2324286d5578983f6b11ff52d1b6c19bd80ae3933f22edd0eb64ea2a1418aa4`
- Numeric pack SHA-256: `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`

This exception closes A1 for this candidate only. Every other AI, balance, determinism, performance, and technical gate remains independent.

## Final source aggregate extension — 2026-11-27

The same owner-exception pass applies to the closeout source aggregate `e67713fc326672e8543341d56c17b3cd214295a208cdb01a05952120645b4a5e` with numeric pack SHA-256 `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`. The only simulation-code changes since Astra reviewed `0f7bdd2` are Fief/Discovery research-cache snapshot preservation and checksum coverage in `2be9d593f92f2ad2aff784820a9ca9c51bee3f56`; research planning and uninterrupted gameplay are unchanged. This carries forward the measured 59/80 result by owner exception and does not claim a new A1 measurement on the final source.

## Final A1 disposition for candidate `a966ec3` — 2026-11-27

**Official gate status: PASS BY OWNER EXCEPTION.** Astra's fresh review found no defensible, evidence-backed A1 intervention. Per the owner's direction, the near-miss is accepted for E5 closeout. The measured A1 result remains **74% over 80 matches: FAIL** against the unchanged **80%** nominal threshold. This is an explicit acceptance exception; it does not change the metric or threshold.

The completed M10 campaign is runtime-equivalent to candidate `a966ec377a9d650b2b90b4467827c777e115c603`. Its metadata correctly records `source_unchanged=false`: it began at `8774ebdfa7dfe078fbcf366390bd5d830db6769a` and ended at `a966ec377a9d650b2b90b4467827c777e115c603`. The only `content/`, `src/`, or `game/` difference between those revisions is the A8 test file `src/CivSim.Tests/FiefWholeMatchFogFairnessTests.cs`; runtime source did not change. Preserve the false flag and this explanation rather than relabeling the run as exact-head evidence. The measured Sim and Harness binaries and numeric pack match the pinned artifacts below. The campaign reused the registered `80000000–80000039` block (40 mirrored seeds / 80 matches), so it is current-candidate runtime validation, not a fresh held-out sample.

- Accepted candidate: `a966ec377a9d650b2b90b4467827c777e115c603`
- Candidate source aggregate: `e0b43203a6622829816f85b924e986995bffd474a82a78ce819272025a117048`
- Measured M10 candidate: `8774ebdfa7dfe078fbcf366390bd5d830db6769a` (runtime-equivalent as described above)
- Measured Harness SHA-256: `0224459af3d84c3c61773b4324798d246b26e75b445adc41c8b0297a074dd950`
- Measured Sim SHA-256: `51562e50ab950f85e500d995b475cbe5a86b8f99ac688260a283fb9989d349d7`
- Numeric pack SHA-256: `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`
- Run metadata: [bundled run metadata](m12-e5-9-m10-a966-current-validation-2026-11-27/run_metadata.json) (SHA-256 `71f1756b25521ec1d2c06d7c76141bc52ced76edb74518ea0f85607d0e8fa49e`)
- Raw M10 output: [bundled raw output](m12-e5-9-m10-a966-current-validation-2026-11-27/raw-output.txt) (SHA-256 `3dba9c9d0f881d8b226c5147d2afc7d9878315cedd0c18ad8bf030fb4e3b3c52`)

The campaign exited 1 because A1 failed its measured threshold. Its other measured M10 gates passed: A2, A3, A4, A5, A7, B27, B28 (14/14), B29 (159/159), B30, and B31 (50%). A6 and A8 remain separate gates. The current output reports only rounded `74% over 80`; it does not establish an exact numerator, so the historical `59/80` baseline remains attributed only to its original run.

This owner exception closes **A1 only** for candidate `a966ec3`. Preserve the nominal failure, threshold, exit code, raw output, and all independent gate requirements.

**Fresh Astra re-review — 2026-11-27:** Astra found no evidence-backed narrow intervention likely to recover the five wins needed to reach 64/80 without risking nearby A3/A4 gates. The current result is rounded 74%; the exact numerator and a current confidence interval are unavailable. Astra endorses PASS BY OWNER EXCEPTION while preserving the measured failure, unchanged 80% target, reused-seed limitation, and `source_unchanged=false` provenance above. No A1 code change is justified.
