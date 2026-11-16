# Fief A1 policy diagnosis (2026-11-15)

The frozen A1 result remains **61/80 (76.25%), below the unchanged 80% gate**. This review found and corrected one Fief research-valuation units defect. It does not establish a new acceptance result. No broad cohort or Release build was run for this review.

## What the complete frozen diagnostics show

Source: [all 80 match rows](m12-e5-9-fief-a1-cohort-2026-11-14.txt), grouped by the printed winner. These rows sum the two civilizations on each team; their posture counts sum 2 × 256 seasons.

| Metric, Warlord / Council | 19 Council wins | 61 Warlord wins |
|---|---:|---:|
| Mean Dominion | 3008.68 / 3391.32 | 3539.87 / 2860.13 |
| Mean final settlements | 16.68 / 18.63 | 23.38 / 13.44 |
| Mean decree foundings | 2.32 / 3.26 | 3.11 / 2.28 |
| Mean final land share | 15.97% / 16.18% | 21.25% / 11.58% |
| Warlord GOV-02-locked seasons | 396.37 / 512 | 368.87 / 512 |
| Warlord Expand seasons | 92.58 / 512 | 110.11 / 512 |

All 19 losses have a Dominion deficit. Seven nevertheless finish with more Warlord settlements; one finishes tied. The seven are 20261022 mirror 2, 20261025 mirror 2, 20261029 mirror 1, 20261035 mirror 2, 20261037 mirror 1, 20261044 mirror 2, and 20261046 mirror 2 (mirrors numbered by row order). Endpoint settlement count therefore does not identify the whole failure. [`Territory.AccrueDominion`](../../../src/CivSim.Sim/Politics/Territory.cs) awards annual points in proportion to owned **habitable weight**, so earlier ownership and land quality matter. Late expansion cannot recover points already awarded. This is the source-supported scoring mechanism; the frozen rows alone cannot separate timing from habitable quality for each loss.

The four closest Legacy deficits are 24, 78, 147, and 152. Three additional wins would meet 64/80. This describes the size of the miss, not a claim that the correction flips those matches.

## Concrete policy defect and correction

[`Strategist.DecidePosture`](../../../src/CivSim.Sim/Ai/Strategist/Strategist.cs) cannot select Expand until GOV-02 completes. [`ResearchSpark.BuildValuation`](../../../src/CivSim.Sim/Ai/Spark/ResearchSpark.cs) is intended to value that unlock on an open frontier while yielding to urgent conflict. Its `PostureUnlockBonus` helper and existing `SparkPlannerTests` use normalized intent signals and compare defense/readiness against `FortifyAt = 0.45`.

The caller instead supplied already scaled Legacy-equivalent bonuses: frontier × 8, defense urgency × 40, and attack readiness × 30. Consequently, defense urgency of approximately 0.01125 or readiness of 0.015 could suppress the unlock, although both were well below the intended 0.45 guard. Frontier magnitude also saturated at an underlying value of 0.125. This is a demonstrated units mismatch, independently of any cohort interpretation.

The minimal correction passes raw frontier, defense urgency, and attack readiness for Fief. The Bronze/Iron branch keeps its prior calculation. No bonus constant, threshold, military valuation, defensive action, donor protection, decree quota, catalog, or golden changed. This restores the intended link from FrontierRace to its growth posture under moderate conflict; the annual Dominion mechanism explains why delayed access can matter. Per-loss causal attribution and the net win-rate effect still require later measurement.

The Council comparison was also inspected: [`CouncilDecreePolicy`](../../../src/CivSim.Sim/Politics/CouncilDecreePolicy.cs) uses a 70% early Found window and growth corvée, whereas [`StrategistDecreePolicy`](../../../src/CivSim.Sim/Ai/StrategistDecreePolicy.cs) has its own candidate search, donor protection, and EV ranking. Its general alternatives can already propose Found below the nominal 85% fallback threshold. Lowering that threshold or removing donor protection is not supported by the frozen summaries and could damage City growth; neither was changed.

## Bounded validation

[`FiefPostureValuationTests`](../../../src/CivSim.Tests/FiefPostureValuationTests.cs) exercises the valuation actually built by `ResearchSpark.Decide`, rather than only calling the helper. It checks moderate threat/readiness, frontier magnitude, closed frontier, both urgent-conflict guards, and unchanged Bronze/Iron values. The existing posture-helper tests and Strategist raider/Fortify response were also selected.

```sh
dotnet test src/CivSim.Tests/CivSim.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~FiefPostureValuationTests|FullyQualifiedName~SparkPlannerTests.Posture_unlock|FullyQualifiedName~StrategistDecisionTests.A_raider_neighbour_raises_defense_urgency_and_Fortify'
```

**Passed 12/12 in Debug.** Output: `/tmp/civsim-fief-a1-sol-focused-tests.txt`. These tests preserve the urgent-defense controls relevant to A3 and the established older-chapter values; they do not remeasure A2, A3, or A5 outcomes.

Before the correction, one seed (20261046, both mirrors, 64 minutes) was replayed with the existing Debug binary and scratch acceptance pack using `--warlord-found-funnel --warlord-settlement-trace --warlord-frontier-decision-trace`. Output: `/tmp/civsim-fief-a1-sol-20261046-debug.txt`. It remained split 1/2; the losing mirror had 18/15 final settlements, 19.2%/14.0% final land, a 40-point Dominion deficit, and 390/512 GOV-02-locked seasons despite 512/512 frontier-open seasons. It illustrates the timing/weight distinction but is **not an exact replay of the frozen source** (its Legacy deficit is 32 rather than the frozen 24), and no before/after performance claim is made.

The source correction is ready for integration. Frozen A1 still fails; A2/A3/A5 retain their previously documented evidence, and any renewed acceptance claim must use the final integrated binary and unchanged gate protocol.
