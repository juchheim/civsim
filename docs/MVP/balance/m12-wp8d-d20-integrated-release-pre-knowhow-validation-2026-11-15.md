# D20 AGR-11 validation on the integrated Release harness (pre-KnowHow retention)

**Status:** completed diagnostic; this run does not close D20 because the harness-adjacent `CivSim.Sim.dll` predates WP-E5-6 Know-how retention. Do not promote AGR-11 or catalog values from this result. Repeat the frozen 36-pair cohort against the final integrated DLL after WP-E5-6 is settled.

## Frozen inputs and binaries

- Frozen 36-world/focus-seat manifest: [m12-wp8d-d20-final-code-seats-2026-11-14.tsv](m12-wp8d-d20-final-code-seats-2026-11-14.tsv), SHA-256 `c9a9b3c09e718a17fc72f04c2019bf8d93381bcaf56b18e0346c6843ae32c14b`.
- Scratch AGR-11 pack: `/tmp/civsim-e5-fit-candidate.pack`, SHA-256 `a45d3e5163c791632825b654bd149061660baf33d38e8e498025397941bee399`.
- Executed Release harness DLL SHA-256 `54e2c31e53764829c9db87d5d671fb1051ed18c749a35859b17bc19508f1ffa4`.
- Adjacent loaded `CivSim.Sim.dll` SHA-256 `f017b5cf9382df50301a8fc5b7e6d2c805ee506bd6c496746020f06367e4291d`.
- The harness and adjacent Sim DLL hashes were unchanged from launch through completion. The newer Sim DLL in another project output directory was not loaded by this cohort.
- Both arms ran 180 simulated minutes: 72/72 completed, 36 matched worlds, no runner errors. Frozen raw output: [m12-wp8d-d20-final-code-output-2026-11-15.txt](m12-wp8d-d20-final-code-output-2026-11-15.txt); runner log: [m12-wp8d-d20-final-code-run-log-2026-11-15.txt](m12-wp8d-d20-final-code-run-log-2026-11-15.txt).

## Results against unchanged D20 screens

- **+12-season capacity response:** 34 complete focus-seat pairs; 2 seats (20295617/c2 and 20295706/c3) never reached KEY-5 by the horizon and were censored without imputation. Population-weighted sustainable-capacity ratio is **1.1049** (10.49% increase), with seed-bootstrap 95% CI **1.0967–1.1125**. The estimate is inside the unchanged `1.0805–1.1401` band and the CI lower bound exceeds 1.0. The weighted central 90% observed band is `1.0709–1.1376`.
- **Anchor integrity:** 36/36 paired manifests have identical pack, world, and initial match checksums; offset-zero whole-match state checksums match for all 34 eligible focus-seat pairs (34/34). No anchor mismatches were observed.
- **+240 seasons:** 34 matched worlds (the same 2 focus-seat censorings omitted from the focus-anchored checkpoint). Whole-match population ratio mean **1.002**, range **0.963–1.022**. Whole-match sustainable-capacity ratio mean **1.000**, range **0.930–1.048**. Means are within `0.95–1.05`; the per-world capacity range includes a low value below `0.95`, so this checkpoint does not satisfy a strict every-world band interpretation.
- **180-minute whole-match horizon:** 36/36 worlds have rows; population ratio mean **1.000**, range **0.980–1.030**; sustainable-capacity ratio mean **1.000**, range **0.961–1.045**. Both ranges are within `0.95–1.05`. There were no match-end censorings (`endState=None`, `matchEnded=False`). The audit lists 16 censored seat-arm records (8 seats censored in each of the two arms); these do not remove the whole-match rows.

Under the D20 cohort-mean interpretation now recorded in the decision, the +12 numeric screen and both +240 cohort-mean ratios pass on this binary; the per-world +240 capacity minimum of 0.930 remains visible as a diagnostic range value. D20 is still open because this binary predates WP-E5-6 Know-how retention and the criteria must be rerun on final integrated code. No values were promoted and no golden files were refreshed.

## Reproducible audits

- [Weighted +12 fit and censor report](m12-wp8d-d20-final-code-weighted-fit-2026-11-15.txt), produced by `fief_land_population_weighted_fit.py --offset 12`.
- [Paired anchor and long-horizon audit](m12-wp8d-d20-final-code-anchor-audit-2026-11-15.txt), produced by `fief_land_anchor_audit.py`.
