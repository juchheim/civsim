# F-T3 current-source cohort — 2fce6aa

**Disposition: INCOMPLETE.** This is the preregistered 40-seed, 64-season matched cohort on frozen E5 source commit `2fce6aa29eab5d75e0cd6018c44d6ea6d5a65b78`. All runs completed with zero command errors, zero structural-analysis errors, zero pin drift, and identical before/after pins. The result does not close F-T3 because four pairs lacked the minimum exposure required by the frozen rule.

## Results

- 36/40 pairs passed every assessed component.
- Work share: 39/40 treatment seeds had mature windows and passed the ≤15% limit in every window. Maximum measured share: **10.1530%**, seed `36528733`, seasons 4–15. Seed `36013794` produced no first kit and had no mature window.
- Fuel: 36/40 passed. Seeds `36269804`, `36602302`, and `36916907` had mature work-share windows but fewer than 12 positive planned-charcoal demand seasons, so the frozen rule censors them as `CENSORED_LOW_DEMAND`.
- Forest clearing: 40/40 passed; no furnace-attributed clearing crossings.
- Material accounting: 40/40 passed; all wood-balance residuals were zero.
- Matched control windows: observed for all 40 seeds.

The four incomplete seeds are **not** treated as passes or removed. The raw traces show that all four had zero positive planned-charcoal demand seasons in the measured late period; three still produced small cumulative iron/wrought output during earlier seasons. The result is therefore insufficient to establish the unchanged ≤5% fuel-short gate across the full cohort. It does not by itself show a game balance failure. It exposes a fixture coverage gap that needs diagnosis before F-T3 can close.

## Frozen run identity

- Manifest SHA-256: `6f717f325a9f98b33fe98bee3d23400940804d752952bdfb229c725b192976a9`
- Source-tree SHA-256: `7d1135f42d47e6ecc8c387020b0cc847291d07cff137d131d40a08f810ae40e3`
- Numeric pack SHA-256: `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`
- Release Harness SHA-256: `650e2ac7775d8842b15c1383bd4f5e08174762a78e74afa1acdda71b68fa8cba`
- Adjacent Sim SHA-256: `52d0cbdc39886119652f4231c383c97dc01623926df99e00876fd043eda37416`
- Runner SHA-256: `8bc5676a1f74549e0373cdf2b0eb5e0db52ecfcb8f586982c7ad4df2b7d498d1`
- Run interval: `2026-11-27T20:56:22Z`–`2026-11-27T21:04:30Z`
- Analysis SHA-256: `ba23f7a4d6f0e66283027998bdfc5d03120672788e9785f247c158d4bf5e4375`

Preserved `raw/` contains the analysis, exact manifest, all 80 arm logs, and `SHA256SUMS.txt`. F-T3 remains open. Next, diagnose why the fixed low-wood furnace fixture lacks 12 late-period charcoal-demand seasons in those seeds. Preserve the current incomplete cohort; any changed fixture or acceptance rule requires a new freeze and preregistration.

## Follow-up exposure probe (diagnostic only)

A one-seed run at 400 people for seed `36269804` used the same frozen source, pack, Harness, Sim, furnace factor, and 64-season horizon, changing only the controlled fixture size. It still recorded **zero** positive planned-charcoal demand seasons in the measured late period; the run produced 0.49 L of iron ore and 0.24 L of bloomery iron/wrought iron, all in season 2. This suggests that simply increasing fixture population does not create sustained late-period furnace demand. It does not establish why the source or demand stops, and it is not acceptance evidence. The raw run is preserved as `raw/diagnostic-people400-seed-36269804.txt` with the same hash inventory.
