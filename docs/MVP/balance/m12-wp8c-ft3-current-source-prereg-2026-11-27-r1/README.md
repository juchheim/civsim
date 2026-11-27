# F-T3 current-source 80-season cohort preregistration — 2026-11-27 r1

## Why the horizon is extended

The current-source 64-season cohort on 2026-11-27 completed 40 matched seeds: 36 passed every gate, none failed the work-share ceiling, and 4 were incomplete because treatment had too little furnace-fuel demand or no mature window. A longer horizon is needed to observe the same rule without censoring. This fresh cohort extends the fixture to 80 seasons; the work-share rule and thresholds are unchanged. The earlier 64-season cohort remains diagnostic and its seeds are not reused.

## Frozen protocol

- Protocol: `WP-E5-5/F-T3`; 40 paired natural seeds, 200 people per arm, 80 seasons.
- Treatment: `fief-low-wood-furnace`; control: `fief-low-wood-no-furnace`.
- Gate: metal-chain/civilian work share must remain at or below 15% in every mature rolling 12-season treatment window, from first kit output plus 12 seasons through season 80. Each seed also retains its preregistered fuel, clearing, and material-accounting gates. No mature exposure is incomplete, never a pass.
- Seeds: [`seeds.txt`](seeds.txt), SHA-256 `f6faa3eb66ee6ad58b59a6b17a146c151bf7079418460b84d6275942cf7b318e`. They are the first 40 unused values in a fixed 1,000-seed sample generated with `random.Random(20261127)` from 37,000,000–37,999,999, then sorted. No outcome, geography, or progress screening was used.
- [`seed-collision-audit.json`](seed-collision-audit.json) records 5,553 scanned seed/manifest files, 40 previously used candidates removed before selection, and zero collisions in the selected block. Inventory digest: `28b691bdb7c166a667d177e658e881ff2a3a11d9524f2d8c4fba07eb21c9a798`.
- Registered at `2026-11-27T14:53:52Z`, before the cohort launch.

## Validation and source pins

`python3 tools/wp_e5_5_ft3_cohort.py --self-test` passed its parser, pairing, threshold, no-exposure, and pin fixtures. The committed 80-season runner is pinned in the external execution manifest together with the source commit/tree, numeric E5 pack, and adjacent Release Harness/Sim hashes. The completed external manifest and raw run metadata will be added here after execution.
