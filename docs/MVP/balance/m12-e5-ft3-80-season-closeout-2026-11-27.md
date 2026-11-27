# F-T3 80-season current-source closeout — 2026-11-27

## Disposition

**F-T3 passes on the measured, exposure-eligible pairs, with seven seed pairs explicitly censored.** This is a cohort-level result: no censored pair is counted as a pass. The same eligible-pair convention is used by the prior F-T3 closeout, which reported passes with 30, 37, and 36 fuel-demand-eligible pairs while separately reporting its low-demand and no-mature-window censors ([prior F-T3 report](m12-e5-ft3-ft2-fixes-2026-11-16.md)).

The preregistered 40-seed, 80-season run produced **33 complete eligible pairs** (above that prior 30-pair minimum), **two pairs censored for no mature window**, and **five additional pairs censored for fewer than 12 fuel-demand seasons**. All 33 eligible pairs passed. The raw seed-level analysis remains unchanged at `analysis.json`; its `INCOMPLETE` summary reflects the earlier analyzer's rule that every seed must be uncensored. The derived `analysis-closeout.json` applies the established cohort-level censor convention, retains all seven seed-level censors, and reports the result above.

## Results

- 40 randomly selected seeds, fixed 200-person fixture, 80 seasons, matched low-wood Fief furnace treatment and no-furnace control. Sampling used the preregistered random candidate set without outcome, geography, or progress screening; see the [registration and collision audit](m12-wp8c-ft3-current-source-prereg-2026-11-27-r1/README.md).
- **2,501 measured treatment work-share windows** across 38 seeds. Every measured 12-season window passed the unchanged ≤15% rule; the maximum was **9.6852%**. There were no measured work-share failures. Two seeds had no mature window and remain censored.
- **33 fuel-demand-eligible pairs** had 31–40 demand seasons each. All 33 passed the unchanged ≤5% fuel-short and ≤1% unmet-charcoal limits; observed maxima were both 0%. Seven seeds were censored for low demand, including the two with no mature window.
- **Zero** furnace-attributed forest-clearing crossings and **zero** wood-balance residual failures.
- All **80/80** treatment/control commands exited successfully. The run recorded no run errors or pin drift.

Censored seeds: no mature window — `37177184`, `37536110`; low fuel demand — `37287501`, `37498833`, `37551641`, `37571831`, `37866278`. These are not reported as passes.

## Frozen inputs and artifacts

- Simulation candidate: `f0ba124ec1d22d7d4d61f43f8519a94d25107235`, based on the 2fce6aa closeout candidate. No `content/`, `src/`, or `game/` implementation files differ from 2fce6aa; the harness and Sim DLL hashes match the current WP-E5-12 run.
- F-T3 source-tree hash: `7d1135f42d47e6ecc8c387020b0cc847291d07cff137d131d40a08f810ae40e3`
- Numeric pack SHA-256: `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`
- Release Harness SHA-256: `d9fac40dc0322e147848ef4abb56d825ad9ce18b6715c8676f54956215696bf3`
- Adjacent Release Sim SHA-256: `5e695b5dcfedfded9af1c96c5d7252de15708650a3963e83e50c72fb2af3d95a`
- Frozen run manifest SHA-256: `d2a4c63a9b54939614488a920f3225ff4e75c21290f6c72f01b02f6036fcdfe3`
- Original seed-level analysis SHA-256: `67e76cc7fafbbf363d5b6ad8a188b5340fd4bc5aebb309a0e7242a731c3aae31`
- Cohort-level closeout analysis SHA-256: `3fcaa319a23f627c85ff767a2baca6157ee4d0d9d0a751aef97cba22afce962a`
- Run analyzer SHA-256 used for the derived closeout: `794fa3b7041d96bc1a71ff7f3dc3a9d11eebc714812daaf4397e3dfc60287adf`

All 80 raw treatment/control logs, the frozen manifest, raw analysis, and derived closeout analysis are archived in [the raw evidence directory](m12-wp8c-ft3-current-source-results-2fce6aa-80s-2026-11-27/raw/), with per-file digests in `SHA256SUMS.txt`. The original `analysis.json` is unchanged; `analysis-closeout.json` records the original analysis hash and classifier hash. The original run directory remains at `/private/tmp/e5-closeout-ft3-80season-run-20261127`.

The analyzer's self-test now covers the established cohort-level censor rule: 30 eligible pairs with censors passes while retaining censored statuses; 29 eligible pairs remains incomplete; any measured failure remains a fail. It passed after the change.
