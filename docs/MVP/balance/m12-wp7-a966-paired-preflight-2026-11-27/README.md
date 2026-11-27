# WP-E5-7 GOV-11 paired cohort preflight — 2026-11-27

This preflight pins the missing natural whole-match paired GOV-11 measurement to the clean `a966ec3` candidate. It uses the registered r2 calibration block (20 seeds) and sealed holdout block (80 seeds) without outcome or exposure screening. Each seed runs one 130-minute natural Fief match with `--fief-wp7-paired`; the analyzer checks exact starting-state checksums, four-season outcomes, censoring, and independent seed clusters.

The candidate is HEAD `a966ec377a9d650b2b90b4467827c777e115c603`, tree `aa860512e8f97f6e1c35ebe5548e32f5159e73ef`, source aggregate `e0b43203a6622829816f85b924e986995bffd474a82a78ce819272025a117048`. The frozen numeric pack is `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`; adjacent Release Harness and Sim hashes are in [pins.json](pins.json).

The existing preregistration refers to `tools/wp7_trace_runner.py`, which is not present in the candidate. The evidence branch's pinned [replacement runner](../tools/run_wp7_paired_cohort.py) provides fail-closed candidate, pack, binary, seed, runner, and provenance-helper checks; one match per seed; replay verification; and trace-row counts. Its CLI help smoke passed. The paired effect analyzer remains the candidate's `analyze_fief_gov11_city_year_pairs.py`.

## Acceptance interpretation

Use the registered autonomous GOV-11 criterion: exact City/GOV-11 eligibility and charter transition, plus a paired annual growth multiplier in `[1.08, 1.12]` with its 95% match-clustered interval containing `1.10`. At least 20 independent seed clusters must have eligible pairs. If exposure or precision is insufficient, report the result as inconclusive. These working tolerances were documented before this run and are not separately user-approved.

The calibration run must finish and be analyzed before opening holdout output. Holdout stays sealed until its candidate and criteria are frozen. If calibration changes code or numeric content, these pins and the holdout must be regenerated; do not reuse the registered holdout after a refit.

## Launch state

The 20-seed calibration is being launched while the independent WP-E5-12 cohort continues. Both are fixed-seed deterministic balance runs, and neither measures runtime performance; the available host has capacity for the four calibration workers. The 80-seed holdout remains sealed until calibration analysis is complete. The 20-seed and 80-seed runs use distinct absent output directories outside the repository. The runner's metadata captures `dotnet --info`, operating-system and architecture details, all match commands, trace counts, replay hashes, and final provenance. B5-11 performance cohorts still require an idle host.

The frozen command shape for calibration is:

```sh
python3 /private/tmp/e5-closeout-evidence-20261127/docs/MVP/balance/tools/run_wp7_paired_cohort.py \
  --repo /private/tmp/e5-closeout-candidate-20261127 \
  --pack /private/tmp/e5-closeout-acceptance-0f7bdd2/numeric-e5.pack \
  --harness-dll /private/tmp/e5-closeout-candidate-20261127/src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll \
  --sim-dll /private/tmp/e5-closeout-candidate-20261127/src/CivSim.Harness/bin/Release/net10.0/CivSim.Sim.dll \
  --seeds-file /private/tmp/e5-closeout-candidate-20261127/docs/MVP/balance/m12-e5-7-gov11-prereg-2026-11-16-r2/calibration-seeds.txt \
  --role calibration --minutes 130 --workers 4 \
  --output-dir /private/tmp/e5-closeout-wp7-a966-calibration-20261127-r1 \
  --expected-head a966ec377a9d650b2b90b4467827c777e115c603 \
  --expected-tree aa860512e8f97f6e1c35ebe5548e32f5159e73ef \
  --expected-source-aggregate e0b43203a6622829816f85b924e986995bffd474a82a78ce819272025a117048 \
  --expected-pack-sha256 ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90 \
  --expected-harness-sha256 1cd422d06fe28e9383eebb0d00dd4de551d3a7b81eee11429f50586cc4e8baf7 \
  --expected-sim-sha256 90fe2bed466ec7dc6903e27e4d6a1324d26af5fc5c1d082641e2fd7dd687c86d \
  --expected-seeds-sha256 f2edf753fd3f26e86e49c22385d2e857192b69cf796f36e1bdbdc079a287742d \
  --expected-runner-sha256 8225a5067a83433b1ead499e0a40594bb07f400a6cb147bf541b94fa4a3b68e4 \
  --expected-provenance-helper-sha256 496f1064b4b02f3e28c8aaff33140a8b2e13d7f8e1a785a68a8cd32f0138a10a
```

After calibration is complete and analyzed, use the same frozen tuple with `--role holdout`, the `holdout-seeds.txt` path, its SHA-256 `94e2a8985079534125635b6d0775748d4e7815b59df8d670b4fd0c0b48c1943d`, and the distinct holdout output root in [pins.json](pins.json).

The existing WP-E5-12 natural cohort carries the other GOV-10, TRD-08/09, DIP-08 and SOC-11 traces; this paired cohort is specifically the same-state GOV-11 effect measurement.
