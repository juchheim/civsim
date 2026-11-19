# F-T6 / F-T7 cohort closeout preparation — 2026-11-15

**Superseded execution note:** the current-source 20-seed F-T7 calibration rerun is complete; see [its analysis](m12-e5-9-f-t7-calibration-analysis-rerun-2026-11-15.md). The fitted `0.282944` scale is still a draft candidate and must be checked on the disjoint held-out manifest before acceptance. The runner now supports `--resume` only for an interrupted run still marked `running`; it verifies exact manifest, pack, harness, command template, completed seed prefix, and every completed raw-output hash before rerunning the interrupted seed. It rejects completed or failed runs, changed inputs, corrupted outputs, and stray future-seed files.

From the repository root, run the manifest sequentially:

```sh
python3 docs/MVP/balance/tools/run_fief_t6_t7_cohort.py \
  --pack /absolute/path/to/final-natural-fief.pack
```

The runner refuses a changed manifest (frozen SHA-256 `a92dd049c1c56836dd2bffad3106985694be2eb755f15b4665c0937ae318f038`), fixes the horizon at 130 minutes, and executes one natural `fief-match` per manifest seed with no E5 grant. It writes each raw result to `docs/MVP/balance/m12-e5-9-f-t6-t7-natural-output-2026-11-14/seed-<seed>.txt`, and records pack, harness DLL, manifest, and raw-output hashes in `run_metadata.json`. The Release DLL must exist before starting. The output directory must not already exist.

After all 20 runs finish, generate the report:

```sh
python3 docs/MVP/balance/tools/analyze_fief_t6_t7_cohort.py \
  --raw-dir docs/MVP/balance/m12-e5-9-f-t6-t7-natural-output-2026-11-14 \
  --output docs/MVP/balance/m12-e5-9-f-t6-t7-natural-analysis-2026-11-15.md
```

The analysis retains every seed, filters F-T7 rows using the emitted own-KEY-5 season offset, reports censoring, and uses a reproducible 20,000-resample seed-level bootstrap (RNG seed `20261115`). F-T6 summaries use the trace's known working/dormant staffed denominator and report unresolved staffing separately. See [the report template](m12-e5-9-f-t6-t7-natural-analysis-template-2026-11-15.md).

No thresholds are changed here. The report checks the existing proposed 10–14% expected epidemic-hazard-share band and CI-includes-12% gate. The health trace now attributes observed epidemic deaths to active Pox versus Plague class and records MED-08 status plus valid import attempts, keyed blocks and successes ([trace protocol](m12-e5-9-f-t7-health-trace.md)). Those fields and deterministic university allocation have focused tests; only the natural cohort, per-seed summaries and bootstrap acceptance interval remain open.
