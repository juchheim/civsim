# GOV-11 city-growth probability preregistration r1 — 2026-11-27

## Why this replaces the prior WP7 effect screen

The completed 20-seed calibration under the 2026-11-16 r2 protocol found that the population-endpoint measure did not represent the agreed “+10% city growth” rule. On candidate `a966ec3`, the old `1.1` multiplier was applied to the fertility input before a nonlinear conception-probability transform. The old population-factor analyzer measured 1.000657 (seed-clustered 95% interval 1.000505–1.000829), while a paired read of 2,376,213 GOV-11 City female-season rows found a 1.228353 conception-probability ratio (95% interval 1.227849–1.228820). The result is a measurement/implementation mismatch, not support for a 10% population-level jump in one year.

The implementation now applies the charter's 1.1 multiplier to final conception probability after the common fertility, food-security, health, and crowding terms. This makes “+10% growth” a +10% chance of conception per eligible City female-season. The separate population-endpoint effect remains a diagnostic and must be reported; it is not the primary gate because it also includes baseline births, deaths, and city size.

## Frozen acceptance rule

- Run each natural Fief seed for 130 simulated minutes with `--fief-wp7-paired`, which also emits the per-opportunity growth rows; do not screen seeds or grant GOV-11.
- The primary outcome is, per seed, the sum of treatment conception probabilities divided by the sum of control probabilities for same-figure, same-state, same-keyed-roll opportunities in live City settlements whose civilization has GOV-11.
- A seed is an independent cluster. The estimate is the geometric mean of the per-seed ratios. Use a 10,000-replicate seed-cluster bootstrap with RNG seed `20261127` for the 95% percentile interval.
- A pass requires at least 20 independent exposed seeds, point estimate 1.08–1.12, and a 95% interval containing 1.10. Report expected and observed seed counts, no-exposure seeds, zero-control-chance rows, eligible female-season opportunities, expected conception totals, paired realized outcome deltas, and the paired whole-city population-factor result as a secondary diagnostic. Do not impute missing or censored observations.
- The 20-seed calibration block may inform implementation only. Freeze source, measurement code, candidate, and criteria before running the untouched 80-seed holdout. Any code or criteria change consumes both blocks and requires a new registration.

## Seed registration

Use `random.Random(20261127)` to sample 100 distinct seeds without replacement from `46,000,000–46,999,999`. The first 20 sampled values, sorted for execution, are calibration; the remaining 80, sorted for execution, are holdout. The r2 `45,000,000` block is retired for this new implementation/metric; its former holdout file remains untouched and is not reused.

- Calibration: [`calibration-seeds.txt`](calibration-seeds.txt), 20 seeds, SHA-256 `9c5ec7210aba37b9f2be2b9e69c352c0c7d7ae311e0574c49655be72581b696a`.
- Holdout: [`holdout-seeds.txt`](holdout-seeds.txt), 80 seeds, SHA-256 `cf632b1d4f3def53ec6915ac0b1cfaf03a9f8ea98d788d7bb4a731698d890b72`.
- A collision scan over 679 existing balance-ledger and run-metadata files found zero of the 100 selected tokens. The calibration and holdout blocks are disjoint by construction.

The code change is currently under review in the E5 closeout candidate. Before calibration launch, commit/freeze the candidate, rebuild the Release Harness and Sim from that exact source, build the numeric-status pack, verify the full source/pack/Harness/Sim/runner/analyzer/seed pins, and run the guarded preflight. Do not open the holdout until calibration analysis and the final candidate fit are frozen.
