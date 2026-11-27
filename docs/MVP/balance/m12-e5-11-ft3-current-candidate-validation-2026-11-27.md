# WP-E5-11 F-T3 current-candidate controlled validation — 2026-11-27

## Result

**F-T3 passes for eligible fixture runs at the unchanged 15% ceiling.** The 40-seed run produced 38 seed pairs with at least one mature rolling 12-season window; all 38 passed, none failed, and two were censored for having no mature work-share window. The maximum eligible mature-window work share was **11.1%**. Every eligible wholly late window also passed (38/38; maximum 2.2%).

Fuel remained within the approved shortfall limits for all 35 seeds with at least 12 charcoal-demand seasons; five were censored for insufficient demand seasons and are not counted as passes. Furnace-attributed forest-threshold crossings were zero across all 40 seeds. All seed outcomes and censoring are retained in the raw report.

## Frozen input tuple

- Candidate HEAD: `8774ebdfa7dfe078fbcf366390bd5d830db6769`
- Git tree: `b76ab434580f7ebbf77d0799c92cce5b255e3f49`
- Source aggregate: `840c1bf2d8824815b9f1ef11b9768c2f28b39812d9fb7c0e3b4ba49008bd914a`
- Numeric pack SHA-256: `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`
- Release Harness SHA-256: `0224459af3d84c3c61773b4324798d246b26e75b445adc41c8b0297a074dd950`
- Adjacent Sim SHA-256: `51562e50ab950f85e500d995b475cbe5a86b8f99ac688260a283fb9989d349d7`
- Seed manifest: `86000001–86000040`, SHA-256 `aeba0b9aa802b5f90e4ab69bb594eae501b19f85b3cfb8d947870f384c2a1540`
- Command: `dotnet CivSim.Harness.dll iron-fuel --pack numeric-e5.pack --fief --seasons 64 --seeds 86000001,...,86000040`
- Process exit: 0; source identity unchanged during run.
- Raw output SHA-256: `3b0785265492ad39456803d3c3dc75ec54909c6e36d21ce12fbd8b117152ff8a`

This closes the controlled F-T3 fixture check on the frozen candidate, with the censored cases reported explicitly. It does not substitute for natural whole-match evidence or promote any numeric row; the E5 rows remain draft pending the integrated WP-E5-11 gate.
