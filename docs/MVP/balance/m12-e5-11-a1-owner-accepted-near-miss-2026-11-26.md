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

The 21 losses mostly reflect early growth and expansion: 20 have a Dominion deficit and 19 have a Thriving deficit. One difficult loss reaches no captured settlements, so it does not point to siege or arms as the common cause. The annual trace shows the deficit already present by season 120, before key early governance research is completed. Raising the tested GOV-02 preference from the baseline 46 to 50 did not improve the result (59/80); lower preferences of 36 and 40 produced 58/80. A frontier-founding bonus adjustment also remained at 74%, while small conquest-floor and frontier-weight screens were unchanged.

A broader AI change that values early research by expected population, fission, and accumulated land income could be explored later, but the present evidence does not identify a reliable rule or parameter. That is not a sound basis for reopening and retuning the whole AI gate campaign during this closeout.

The fresh review confirmed 20 of 21 losses had a Dominion deficit and 19 had a Thriving deficit. In seed `80000034`, the Warlord was already behind by season 120 in Dominion, population, and owned habitable weight, with no settlements captured or lost. The previous tested GOV-02 valuation changes produced 58/80 at bonuses 36 and 40 and 59/80 at 46; the frontier bonus screen stayed at 74%. These results do not identify a defensible low-risk adjustment. The separately claimed GOV-02 bonus-50 artifact was not independently located during this review and is not treated as fresh evidence.

## Scope

This exception applies to the pinned E5 candidate above. It does not claim A1 met the nominal 80% threshold, and it does not waive other AI or technical gates. In particular, the same candidate's campaign reported B28 defense coverage at 8/11 and B29 with eight forecasts missing at capture; those remain separate open items.
