# F-T1 partial calibration fit — 2026-11-16

**Status: lower-confidence draft fit hypothesis only.** This record does not establish F-T1 acceptance and does not promote any E5 catalog row.

## Observed subset

The calibration run on source commit `a7ad1588c5214bbc3a456d1bc1b1ab7dd5110c29` recorded 15/20 completed matches, one interrupted match, and four unrun seeds. The 15 completed matches provide 60 observed seats: 53 completed KEY-6 and 7 were right-censored after KEY-5. No incomplete seat was imputed. The completed-seat KEY-5→KEY-6 median is 23.30 minutes (`n=53`).

The official F-T1 analyzer rejects incomplete run metadata: its validation requires `status=complete` and records for all 20 seeds. The preserved diagnostic therefore applies the official per-match/per-seat parser only to the 15 successful records; it does not reclassify the run as complete. See the [partial parse](</Users/ejuchheim/Projects/CivSim/civsim-e5-runs/wp-e5-5-accelerated-diagnostics-20261116/ft1-partial-diagnostic.md>).

Recorded run provenance: source aggregate `46f252c3a6a0a6e030f338861f82dcd332ed9891e79354fc2d34556bc0ed86fc`; numeric pack SHA-256 `592c53f50dff8328c59d8b8bb5273e8d81e6e5ba7de63e7c8b06b9b4a05f9c76`; Release Harness SHA-256 `fc708734e8cd31e7bfa5bcd4810c838acd725ebd109a22a4e0214d9d0a4510b0`; adjacent Sim SHA-256 `d917c69f9ad146f3edb7828a6ec6dfe413af68e6d84243b05e045a93f6314539`.

## Draft fit arithmetic

Using the prior proportional first-pass method:

`6,000 × 25 / 23.30 = 6,437.8`

Rounded to the nearest 100, the draft E5 `base_cost` fit input is **6,400**. This arithmetic assumes proportional response between base cost and completed-seat interval. The 15/20 subset has lower confidence than a complete calibration cohort and is not a stable conversion estimate. The interrupted match, four unrun seeds, and right-censored seats remain unobserved for the fit.

The earlier **6,700** proposal came from the distinct `5ee659bd` source calibration (20/20 matches, median 22.30 minutes). It is retained as historical evidence and was not combined with this `a7ad158` sample. The 6,400 value supersedes it as the current draft fit input; it does not imply that the 5ee result was remeasured or invalid on its own source.

## Remaining acceptance

Keep all 50 E5 project rows at `status: draft`; ordinary and Release Fief selection remain closed. The F-T1 acceptance campaign still requires one fresh, disjoint 80-world holdout after the final source/candidate freeze. Report all four seats per world, censors, early ends, and incomplete matches without imputation. Do not claim acceptance from this abbreviated calibration data.
