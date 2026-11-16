# F-T3 current-source cohort preregistration

**State:** prepared; no matches or builds run.

The runner requires the external manifest to identify protocol `WP-E5-5/F-T3`, preregistration ID and UTC time, exactly 40 distinct nonnegative integer seeds, fixture `{"seasons":64,"people":200}`, exact arm names, and all six nonempty pins (`source_commit`, `source_tree_sha256`, `pack_sha256`, `release_harness_sha256`, `adjacent_sim_sha256`, `runner_sha256`). The manifest records these fields. Source tree, pack, Release Harness, adjacent Sim, and runner pins remain pending final freeze; the source commit is pinned to `fb2d5ced1d16920203adf238d1c459538bd55894`.

The 40 seeds are a simple random sample without replacement from 36,000,000–36,999,999 using Python `random.Random(20261116)`, sorted ascending after sampling. No outcome or geography screening was applied. Before sampling, every committed tracked path matching seed/manifest naming was scanned for exact numeric seed collisions; this cohort has zero collisions with all 40 scanned files, including F-T1 calibration and holdout, current F-T2 preparation if present, F-T4, WP7, WP8/9, and B5 manifests. Recheck against any manifests committed after this preparation before launch.

The acceptance rule remains unchanged: maximum metal-chain/civilian work share must be ≤15% in **every** mature rolling 12-season treatment window, beginning after first kit output plus 12 seasons. Any mature candidate window with no exposure is incomplete, never a pass. Seeds with no mature window remain censored/incomplete. The runner's existing additional fuel, clearing, and material-accounting gates remain unchanged.

Manifest SHA-256: `9182279f182c94a689dff477692bff58124e45a811d0e42b78a0b259f8b0e16c`.

Validation: `python3 tools/wp_e5_5_ft3_cohort.py --self-test` (parser/fixture checks only; no builds or matches).
