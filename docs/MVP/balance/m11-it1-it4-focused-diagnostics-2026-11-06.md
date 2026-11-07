# M11 I-T1 / I-T4 focused diagnostics (2026-11-06)

**Status: evidence reconciliation and proposed measurement plan only.** No acceptance thresholds or preregistrations were changed, and no tests or simulations were run for this note. See the [I-T1 evidence reconciliation](m11-it1-pacing-reconciliation-2026-11-06.md) and [I-T4 matched-anchor protocol](m11-it4-matched-anchor-protocol.md) for the source counts and detailed proposed measurement contracts.

## I-T1 pacing

### Existing evidence

The 2026-11-04 Iron whole-match screen has 78 calibration and 16 held-out matches (94 total; 376 seats). It meets the proposed 80+ match volume and has a separate held-out cohort. Among the 211 seats that reached both KEY-4 and KEY-5, own-entry pacing has a 19.6-minute mean and 19.4-minute median (range 15.7–24.0 minutes), compared with the older 17-minute Standard 1× projection. The existing candidate year-cap rate is 2/94 matches (2.1%) overall, below its proposed ≤10% line. These are observations, not a pass: the own-entry acceptance band is TBD and the cap line has not been approved as an acceptance gate.

### Missing evidence and next diagnostic

The timing result is observed for 211/376 seats. Another 142 reached KEY-4 but not KEY-5; 23 did not reach KEY-4 (13 ended at zero population and 10 remained alive). These 165 seats have no completed own-entry interval and must remain censored/absent, with no imputation. The source CSV also reports 245 settlement-capture incidents but does not identify unique captured seats, so it cannot separate capture history from other censored outcomes.

Proposed next diagnostic: add an opt-in machine-readable sidecar with one row per seat plus one row per match. Preserve cohort, seed, civilization/archetype, KEY-4/KEY-5 ticks, terminal outcome, final population, elimination and unique captured-seat flags; keep repeated capture event counts separate. Report uncensored interval distributions, each timing disposition, capture/elimination flags, and the year-cap match denominator by cohort. This is observational instrumentation only. `IronMatchCommand.cs` is currently under I-T6 edits, so implement this after those edits are integrated or in an isolated helper.

Proposed comparison: once the owner supplies an own-entry timing band, report the fraction of observed intervals inside it alongside the uncensored N and disposition counts. Continue to show calibration and held-out cohorts separately. Do not classify pacing until the band and denominator rules are approved. The ≤10% cap proposal remains visible as a diagnostic only.

## I-T4 food and city capacity

### Existing evidence

The proposed target in 25 §13 is FoodCap growth of 1.4–1.8× on a matched E3 anchor, with increased city concentration and no free food. The numeric band is unapproved, and the city-concentration criterion has no approved cutoff. The existing E3 anchor contains only two C1-E3 fixture observations (FoodCap means 193.3 and 173.5, paired to E2 values 128.6 and 104.6); it is not the required five-way geography-matched baseline. The E4 economy oracle uses stipulated inputs and reaches 1.4× only in its combined hypothetical fixture; it does not run jobs, production, demography, settlement tiers, or matched geography. The existing CSV auditor validates supplied summary rows and physical-stock arithmetic; it is not a runtime source.

### Missing evidence and next diagnostic

There are no qualifying live matched E3/E4 observations for non-river, river, hill, inland, and coastal starts, and no multi-season production/transfer/consumption/loss ledger with donor reserve checks. Thus neither the FoodCap ratio nor city concentration has a live measured result. The existing protocol's one-row-per-fixture CSV is suitable as a smoke-check, not as raw paired evidence: it loses pair IDs, counts, seasons, and distributions.

Proposed next diagnostic: a separate deterministic `it4-anchor` runner should emit raw matched-pair, per-settlement, per-season observations and a run manifest. Pair on seed and verified start tile, record population/age mix and state checksum, and sample at a fixed offset from each treatment's own KEY-4; verify snapshot/restore before calling the arms same-state matches. Per fixture, compare within-pair `E4 FoodCap / E3 FoodCap` distributions and City-tier population share (`City-tier population / all population`) percentage-point changes. Keep all five geography results separate and show qualifying counts. Add physical food ledger entries at actual production, import/transfer, consumption, loss, and closing-stock boundaries, including donor reserves; do not infer production from stock deltas. This remains diagnostic instrumentation and must not change gameplay behavior.

Proposed comparison: report each fixture's paired FoodCap ratio and city-share change with sample count, median, mean, p10/p90, and range; include the pooled result only as a labeled supplement. Do not call a fixture pass or fail from the proposed 1.4–1.8× band or city-share direction until the owner approves the numeric band, comparison statistic, and coverage rule. The mass-balance equation checks accounting correctness; it does not itself establish a capacity or city-growth pass.

## Approval boundary

No code change, threshold change, preregistration amendment, test, or simulation is included in this note. I-T1 needs an approved own-entry timing band and auditable unique-seat outcome telemetry. I-T4 needs approval of the proposed FoodCap band and of the city-concentration statistic/cutoff, plus live matched fixture data and the physical ledger. All candidate statistics above are proposals, not acceptance criteria.
