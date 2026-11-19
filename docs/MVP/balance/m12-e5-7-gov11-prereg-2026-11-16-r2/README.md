# WP-E5-7 GOV-11 current-source paired cohort preregistration r2

Prepared 2026-11-16. This is a seed-block preregistration, not an executable run manifest; final source and binary pins are pending.

## Why r2 replaces the earlier block

The prior calibration block `34000000–34000019` collides with the WP-E5-6 current calibration preflight ledger. The earlier registration is preserved for audit but must not be launched. This r2 block uses a fresh pool and is disjoint from all selected seed tokens found in the checkout at preparation time.

## Registered seed blocks

- Calibration: 20 seeds in [calibration-seeds.txt](calibration-seeds.txt), SHA-256 `f2edf753fd3f26e86e49c22385d2e857192b69cf796f36e1bdbdc079a287742d`.
- Untouched holdout: 80 seeds in [holdout-seeds.txt](holdout-seeds.txt), SHA-256 `94e2a8985079534125635b6d0775748d4e7815b59df8d670b4fd0c0b48c1943d`.
- RNG: `random.Random(20261123)`, sample 100 distinct seeds without replacement from `45,000,000–45,999,999`; first 20 sampled values are calibration, remaining 80 are holdout, then each block is sorted for execution. No outcome, exposure, geography, KEY timing, or world screening.
- Collision audit: 1593 UTF-8 text files / 215344 exact standalone eight-digit tokens scanned; zero collisions with these 100 selected seeds. Re-run after any new seed ledgers are added.

## Measurement and gate interpretation

Follow [the GOV-11 same-state City-year protocol](../m12-e5-7-gov11-city-year-pair-protocol-2026-11-16.md). Each natural City/GOV-11 eligible year is measured from a full `MatchModel` snapshot; treatment and control branches start with identical checksums and run four seasons, with only the GOV-11 City-growth multiplier suppressed in control. Use `tools/wp7_trace_runner.py` with `--fief-wp7-paired`, 130 minutes, and the exact seed blocks here. The analyzer requires exactly one terminal row per expected seed, retains no-exposure/excluded/censored accounting, and needs at least 20 independent seed clusters with eligible pairs.

The provisional working screen is an estimate of 1.08–1.12 with the seed-clustered 95% bootstrap interval including 1.10. These autonomous criteria are **not separately user-approved**. Treat calibration and holdout as diagnostic; do not promote catalog values from them.

## Required before launch

1. Freeze the integrated source and numeric-status pack.
2. Build the Release Harness and adjacent Sim from that same source.
3. Generate separate runner manifests for `tools/wp7_trace_runner.py` pinned to the clean source HEAD and `hash_e5_workspace.py` aggregate, numeric pack, adjacent Release `CivSim.Harness.dll` and `CivSim.Sim.dll`, runner and analyzer SHA-256, cohort role, seed-file SHA, 130-minute horizon, and provisional criteria status. Include the exact .NET host/SDK/runtime inventory, OS/release/version, and architecture. The build-provenance record must bind the Release `net10.0` command and timestamp to those same source, pack, and binary hashes. Candidate pins remain `PENDING_FINAL_FREEZE`.
4. Repeat the full collision audit, verify an idle host, and use a fresh output path outside the source tree.
5. Keep the 80-seed holdout sealed until the calibration fit and candidate are frozen. Any refit consumes this holdout and requires a new preregistered block.

No build, test, preflight, or match was launched, and no output path is reserved.
