# D20 F-T4 final-candidate preflight — 2026-11-27

**Status: ready, waiting for the local simulation host to clear.** The D20 36-pair AGR-11 operational screen was previously reported as passing on HEAD `bd0f4b8` / source aggregate `7decf87`, but that evidence predates the final integrated candidate and does not close the current gate. Re-run the same frozen 36 seed/focus-seat pairs on the exact `a966ec3` candidate before closing D20.

The candidate, tree, scoped source aggregate, numeric pack, Release Harness/Sim, runner, and seat-list hashes are pinned in [`pins.json`](pins.json). The output path `/private/tmp/e5-closeout-d20-a966-20261127-01.txt` was absent at preflight. The host is not idle while WP-E5-12 and M10 are still running; do not overlap this 72-arm, 180-minute run with them or B5-11 performance.

When the host clears, run from `/private/tmp/e5-closeout-candidate-20261127`:

```sh
python3 docs/MVP/balance/tools/run_fief_land_long_horizon.py \
  --pack /private/tmp/e5-closeout-acceptance-a966-finalcheck-20261127/numeric-e5.pack \
  --harness /private/tmp/e5-closeout-candidate-20261127/src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll \
  --minutes 180 --workers 8 \
  --output /private/tmp/e5-closeout-d20-a966-20261127-01.txt \
  docs/MVP/balance/m12-wp8d-d20-final-code-seats-2026-11-14.tsv
```

Verify the candidate source aggregate and the pinned pack/Harness/Sim/runner/seat-list hashes immediately before and after. Apply the autonomous D20 criteria in `docs/MVP/27-faith-and-fief-age.md`: at +12 seasons, control-population-weighted sustainable-capacity ratio 1.0805–1.1401 with seed-bootstrap 95% CI lower bound above 1; at +240 seasons and 180 minutes, both whole-match cohort mean population and sustainable-capacity ratios 0.95–1.05. Report all censoring without imputation. This closes only D20/F-T4; it does not promote AGR-11 or waive other E5 gates.
