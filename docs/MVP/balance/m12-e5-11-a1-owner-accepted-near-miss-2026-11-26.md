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
