# WP-E5-7 GOV-11 same-state City-year protocol (2026-11-16)

> **Superseded for the primary GOV-11 growth acceptance metric on 2026-11-27.** The r2 20-seed calibration completed but failed its population-endpoint screen: estimate 1.000657 (seed-clustered 95% interval 1.000505–1.000829). That metric does not correspond to the agreed 1.1 growth-rate modifier. See the [replacement probability-effect preregistration](m12-e5-7-gov11-growth-prereg-2026-11-27-r1/README.md). The same-state four-season population comparison below remains a secondary diagnostic. The old 80-seed r2 holdout is not run or reused.

**Status: instrumentation/protocol only; no qualifying calibration or holdout cohort exists.** The initial implementation was developed in an isolated copy based on content/src/game aggregate `5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733`; the reviewed runner and paired-measurement work is now present in the dirty `civsim-e5` checkout. That aggregate describes the implementation snapshot, not a currently frozen candidate. Existing candidate pins in the preregistration are stale for launch: the source-to-binary relationship is unverified, the checkout remains dirty, and the numeric scratch pack must be deliberately selected and frozen. No numeric E5 row, including draft D20 values, is promoted or separately approved by this protocol.

## Paired measurement

Use `--fief-wp7-paired`. At each natural calendar-year start (a four-season boundary), if a live settlement is City-tier and GOV-11 is complete, the harness snapshots the complete `MatchModel`. It restores one control branch and verifies the treatment and control `MatchModel.ComputeChecksum()` values match before and after enabling the control diagnostic. The natural main match continues as treatment; the restored branch advances in lockstep for the next four seasons with only the GOV-11 growth multiplier suppressed.

The snapshot includes Economy, Politics, Research, AI, chapter mode, and Guidance. Both branches start at the same tick with the same figures, research, keyed random streams, and other simulation state. The suppression flag is not included in checksums and only changes `FiefCityGrowthMultiplier`, which applies to City-tier settlements with GOV-11. That shared multiplier is used in fertility input and childbirth-risk normalization; the control changes neither research nor other mechanics. No cohort is launched by this implementation task.

For each eligible start City, `wp7-gov11-city-year-pair` records both start and end populations, net changes, growth factors, charter eligibility, end status, and both identical starting checksums. Eligibility is observed after each season boundary in both branches. If either branch loses the settlement or leaves City/GOV-11 eligibility during the four seasons, the harness emits `wp7-gov11-city-year-pair-excluded` with the branch and reason. If the match ends before four seasons, it emits `wp7-gov11-city-year-pair-censored` with observed seasons, populations, and match-end status. These excluded/censored outcomes are retained for reporting but are not in the growth effect estimate.

## Run and analyze

Use one candidate pack/Harness/Sim tuple and one frozen seed manifest. The paired run is performed within each seed from an exact snapshot branch:

```sh
while IFS= read -r seed; do
  dotnet /path/to/CivSim.Harness.dll fief-match --pack /path/to/frozen.pack --seed "$seed" --minutes 130 --fief-wp7-paired > /path/to/traces/"$seed".log
done < /path/to/frozen-seeds.txt

python3 docs/MVP/balance/tools/analyze_fief_gov11_city_year_pairs.py \
  --traces /path/to/traces --expected-seeds /path/to/frozen-seeds.txt \
  --output /path/to/analysis.json
```

Every match emits exactly one `wp7-match-terminal` row, including runs with no eligible City-year exposure. With `--expected-seeds`, the analyzer requires exactly one terminal row per manifest seed and rejects missing, duplicate, or unexpected terminal output. It reports expected/observed terminal seeds, missing seeds, seeds with eligible pairs, and seeds with no eligible pair. No-exposure seeds remain in the expected cohort counts but contribute no pair to the effect estimate. Pair rows for seeds outside the expected cohort fail closed. Without a manifest, observed terminal rows are used as the expected set.

The analyzer fails closed if a paired row lacks equal starting checksums, equal positive start populations, four observed seasons, or stable City/GOV-11 eligibility. Per pair it computes `(treatment population end / treatment population start) / (control population end / control population start)`. It takes the geometric mean of the per-seed mean log effects and bootstraps match seeds as clusters. The preregistered wording requires “20 independent City-year pairs”; operationally, one match seed is the independent unit because years within one simulated world are repeated and clustered. Therefore a pass requires at least 20 distinct seed clusters with eligible pairs, while reporting total eligible City-year rows separately. Repeated City-years in fewer than 20 seeds remain insufficient regardless of the total number of rows. It carries the preregistered provisional `[1.08, 1.12]` estimate range and 95% interval containing `1.10`; these values remain draft hypotheses.

## Gate status

This fork design satisfies the same-state pairing requirement for each measured City-year: both branches start from the same full MatchModel checksum and advance the same four-season interval with only the GOV-11 multiplier changed in control. It adds no promotion or calibration conclusion. The existing tolerances remain explicitly autonomous-provisional and are not separately user-approved; calibration and untouched holdout evidence are still required before WP-E5-7 closes.

## Freeze and launch sequence

Do not use the current preregistration candidate hashes as a launch authorization. After the integrated source is frozen and the host is idle:

1. Freeze the exact source tree and numeric scratch pack; verify the source aggregate and full-repository seed disjointness against existing E5/WP5/WP8/WP9/D20 ledgers and reserved WP-E5-12 seeds.
2. Build Release Harness and Sim from that frozen source and verify the adjacent DLL tuple. Recompute source, pack, Harness, Sim, runner, and seed-file hashes; write and hash separate calibration and holdout runner manifests while preserving the registered 20/80 seed blocks and provisional-criteria label.
3. Run `tools/wp7_trace_runner.py --preflight-only` against a fresh output path for calibration. Launch calibration only after that preflight passes and process capacity is confirmed idle. Analyze the complete output with `analyze_fief_gov11_city_year_pairs.py --expected-seeds` and retain no-exposure, excluded, censored, and error accounting.
4. Freeze any calibration fit and criteria before opening holdout. A refit or candidate/criteria change consumes the holdout and requires a newly preregistered block; do not repurpose the existing 80 seeds.

At this audit, the host-idle condition is unresolved (the preregistration recorded A5 Warlord FFA PID 15753 as live at its last check); recheck process state at the time of any future run. There is no valid calibration or holdout output, and no D20 or catalog value may be promoted from the seed registration, instrumentation, or focused tests.
