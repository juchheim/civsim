# F-T1 final-code acceptance-fit preflight (2026-11-15)

**Status:** preflight only. No content base or acceptance gate changed. The live D20 run has completed (72/72 arm runs), but final integrated source and scratch-pack hashes remain pending. The existing 20-world list is reserved as a calibration block only. A separate 80-world acceptance holdout is frozen and must remain unused until the era-base candidate is frozen. This report does not claim a validated fit.

## Evidence from the existing 7,900 and 8,600 cohorts

| Base | Frozen cohort | Complete | KEY-6 right-censored after KEY-5 | KEY-5 not reached | Completed-seat median | Completed-seat p10/p25/p75/p90 |
|---:|---|---:|---:|---:|---:|---|
| 7,900 | 20 seeds / 80 seats, range 20,300,000–20,399,999 | 65 | 8 | 7 | 22.90 min | 20.14 / 21.00 / 24.60 / 25.34 min |
| 8,600 | 20 disjoint seeds / 80 seats, range 20,400,000–20,499,999 | 72 | 7 | 1 | 22.25 min | 19.40 / 20.68 / 24.42 / 25.69 min |

All matches in both cohorts ran naturally to 130 minutes without early match end. Censors are counted as incomplete and are not imputed into interval quantiles. The cohort reports give per-archetype outcomes: at 7,900, completed counts were Builder 17, Grower 16, Warlord 15, Sage 17; at 8,600 they were 20, 16, 17, 19 respectively.

The original fit arithmetic was `7,900 × 25 / 22.90 ≈ 8,624`, rounded to an **8,600 candidate**. The 8,600 cohort's median would mechanically imply `8,600 × 25 / 22.25 ≈ 9,663`, but that is only a sensitivity calculation. Cohort medians are non-monotonic and cannot establish a stable conversion from era base to interval. The prior four-seed 9,500 cohort measured a 30.1-minute median. In particular, the 8,600 cohort is not a validated 25-minute fit and must remain labeled a candidate only.

The 7,900 and 8,600 evidence is not proven to use the current final integrated source snapshot. The older eight-seed 7,900 diagnostic explicitly predates COM-10 integration; the newer fit/candidate outputs do not establish hashes for the current final integrated source tree. Do not treat them as final-code acceptance evidence.

## Frozen calibration and held-out blocks

The earlier proposed 20-seed list is retained as a **calibration block only**: 20 whole-match worlds × four seats = 20 matches and 80 seat outcomes. It is not acceptance evidence and does not meet the literal 80+ whole-match gate. Its seeds are disjoint from the cited F-T1 203xx and 204xx cohorts, earlier 202610xx probes, F-T4 holdout range 20,270,403–20,279,994, and D20 range 20,291,001–20,296,000. No geography or pacing screening was used.

A separate 80-world final acceptance holdout is frozen in [m12-wp5-fief-ft1-final-acceptance-holdout-seeds-2026-11-15.txt](m12-wp5-fief-ft1-final-acceptance-holdout-seeds-2026-11-15.txt), SHA-256 `c21765567392eb2d0b68f10bd04ddf0681dde72d35f27fdf6d6002803d306526`. Each seed is one whole `fief-match` world with four archetype seats, so this is **80 whole matches / 320 seat outcomes**, satisfying the literal `80+ matches` gate. Its seed range (20,600,000–20,699,999) is disjoint from prior F-T1 ranges, F-T4 ranges, the calibration range (20,500,000–20,599,999), and D20. Seeds were selected without geography, world, or outcome screening and have not been run. Keep the manifest unused until the era-base candidate is frozen.

Exact acceptance-manifest generation code (Python 3):

```python
import random

seeds = sorted(random.Random(20261117).sample(range(20_600_000, 20_700_000), 80))
print("\n".join(map(str, seeds)))
```

The calibration list is [m12-wp5-fief-ft1-calibration-seeds-2026-11-15.txt](m12-wp5-fief-ft1-calibration-seeds-2026-11-15.txt), generated from `random.Random(20261116).sample(range(20_500_000, 20_600_000), 20)` before sorting. Preserve both lists unchanged; do not use the 80-world holdout to tune the candidate.

D20 has completed. Before any F-T1 run, capture final integrated source provenance and build the intended candidate pack; record the source revision/status, Release harness provenance, and pack SHA-256. These hashes remain pending. Freeze the era-base candidate before any use of the 80-world manifest. Then run those 80 natural Fief matches at the frozen base with a 130-minute horizon and without `--grant-e5`; do not screen worlds or pacing. Report every seat and censor reason, whole-match end/horizon, archetype summaries, and completed-seat quantiles without imputing censored intervals. A different/refit candidate after holdout inspection requires a new untouched holdout.

## Autonomous acceptance interpretation (D21)

To make D8's “about 25 minutes” testable under the user's instruction to close the gates without further input, use the following explicit fit gate for the frozen 80-world holdout. This is an autonomous interpretation, not a separately user-confirmed tolerance:

- At least **256 of 320 seats (80%)** must complete KEY-6 by the pinned 130-minute horizon. A seat that does not reach KEY-5 or does not finish KEY-6 counts as incomplete; do not impute an interval.
- Among seats that complete both keystones, the median own KEY-5→KEY-6 interval must be **22.5–27.5 minutes**, a ±10% band around the 25-minute aim.
- Launch and report all 80 whole matches, all four seats per world, each archetype's completion count and uncensored interval quantiles, and every censor/early-end reason. Do not drop a failed match or seat from the completion denominator.
- Use only the frozen 20-world calibration block to select the era-base candidate. Freeze the source, pack, and candidate before opening the 80-world holdout. If the holdout fails and any fit input changes, the holdout has been consumed and a new untouched 80-world manifest is required.

The ±10% interval gives operational meaning to “about 25 minutes” while allowing stochastic whole-match variation; the completion floor prevents a fast median from concealing a large censored tail. Both thresholds are fixed before the held-out run and may not be revised after inspecting it.

## Remaining execution gates

- D20 has completed 72/72 arm runs (36 matched pairs; see the terminal `complete` record in its run log). Its completion does not establish F-T1 source/pack provenance; hashes remain pending.
- Final integrated source and scratch-pack hashes are not yet established for the F-T1 run. Cohort reproducibility also needs the exact harness binary/build provenance and pack content/base hash recorded together.
- Neither newly frozen block has been run. The 20-world block is calibration-only; the 80-world holdout remains unused until the era-base candidate is frozen. Thus 8,600 remains a candidate, and the 25-minute aim remains unvalidated.

## Frozen calibration runner prepared (2026-11-15)

`tools/run_fief_ft1_calibration.py` is prepared for the frozen 20-world calibration manifest. It refuses changed inputs and pins the current diagnostic numeric pack, Release harness, and base-cost source. The verified input identities are:

- Manifest SHA-256: `ed96e046472d8fc84bb9251cd9ccd62c10e7e937978f52d7b27339af00c1b19e`.
- Numeric scratch pack `/private/tmp/civsim-e5-status-pair-20261115/numeric-e5.pack`, SHA-256 `994fb7801aca466b0da3d9dabfab3cc90fb024a029100bcb50f76393fbe9812f`; the packed E5 `base_cost` is 6,000.
- Release harness `src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll`, SHA-256 `27a9d425047af6c7f1cfb85a42b714145b2449ce1d81182bfeff3816de390ccb`.
- `content/registries/eras.yaml`, SHA-256 `940293b45b531f91555e47d590348f66adcefd1d6fffbba4659651c298b407a9`; canonical E5 `{id: 5, base_cost: 6000}` record SHA-256 `85180312495f72ce543a2087b76219bacb96c5ed9ae4e1424a8281bfef7ff92a`.

The runner fixes the 130-minute horizon and natural Fief mode (no `--grant-e5`), refuses to overwrite its output directory, and records each raw-file hash plus the input hashes in metadata. Run only as calibration:

```sh
python3 docs/MVP/balance/tools/run_fief_ft1_calibration.py \
  --pack /private/tmp/civsim-e5-status-pair-20261115/numeric-e5.pack \
  --harness-dll src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll \
  --manifest docs/MVP/balance/m12-wp5-fief-ft1-calibration-seeds-2026-11-15.txt \
  --eras content/registries/eras.yaml \
  --output-dir docs/MVP/balance/m12-wp5-fief-ft1-calibration-output-2026-11-15
```

Focused runner checks pass with `python3 -m unittest docs/MVP/balance/tools/test_run_fief_ft1_calibration.py` (4 tests). The 80-world acceptance manifest remains untouched and must not be opened until a candidate is frozen. No cohort was launched as part of this preparation.

After the 20-world calibration runner records `status: complete`, analyze its raw outputs with the pinned manifest/metadata/raw-hash checks:

```sh
python3 docs/MVP/balance/tools/analyze_fief_ft1_calibration.py \
  --raw-dir docs/MVP/balance/m12-wp5-fief-ft1-calibration-output-2026-11-15 \
  --manifest docs/MVP/balance/m12-wp5-fief-ft1-calibration-seeds-2026-11-15.txt \
  --output docs/MVP/balance/m12-wp5-fief-ft1-calibration-analysis-2026-11-15.md
```

The analyzer emits every seed and all four seats, distinguishes completed, right-censored-after-KEY-5, and KEY-5-not-reached seats, and computes completed-seat quantiles without imputing missing intervals. Synthetic parser checks pass with `python3 -m unittest docs/MVP/balance/tools/test_analyze_fief_ft1_calibration.py` (5 tests); the parser also successfully read a prior 7,900-base raw match.

## Current integrated combined-output runs

The live 6,000 and 8,600 calibration cohorts were launched with `--seeds-file`, writing one combined raw-output file plus metadata JSON. Once each metadata file reports `status: complete`, analyze them separately with the same frozen source manifest:

```sh
python3 docs/MVP/balance/tools/analyze_fief_ft1_calibration.py \
  --combined-output docs/MVP/balance/m12-wp5-fief-ft1-current-integrated-calibration-output-2026-11-15.txt \
  --metadata docs/MVP/balance/m12-wp5-fief-ft1-current-integrated-calibration-metadata-2026-11-15.json \
  --manifest docs/MVP/balance/m12-wp5-fief-ft1-calibration-seeds-2026-11-15.txt \
  --output docs/MVP/balance/m12-wp5-fief-ft1-current-integrated-calibration-analysis-2026-11-15.md

python3 docs/MVP/balance/tools/analyze_fief_ft1_calibration.py \
  --combined-output docs/MVP/balance/m12-wp5-fief-ft1-8600-current-integrated-calibration-output-2026-11-15.txt \
  --metadata docs/MVP/balance/m12-wp5-fief-ft1-8600-current-integrated-calibration-metadata-2026-11-15.json \
  --manifest docs/MVP/balance/m12-wp5-fief-ft1-calibration-seeds-2026-11-15.txt \
  --output docs/MVP/balance/m12-wp5-fief-ft1-8600-current-integrated-calibration-analysis-2026-11-15.md
```

Combined-output mode requires `status: complete`, exact frozen manifest seed coverage and order in both metadata and raw headers, the pinned 6,000/8,600 pack SHA, harness/simulator hashes, source workspace hash, no E5 grant, and 130 simulated minutes. It records the combined raw-output SHA-256 in each report.

## Sources

- [m12-wp5-merge-reanchor.md](m12-wp5-merge-reanchor.md): 7,900 fit and 8,600 candidate cohort methods and summaries.
- [m12-wp5-fief-7900-fit-seeds-2026-11-14.txt](m12-wp5-fief-7900-fit-seeds-2026-11-14.txt), [m12-wp5-fief-7900-fit-seats-2026-11-14.tsv](m12-wp5-fief-7900-fit-seats-2026-11-14.tsv), [m12-wp5-fief-7900-fit-natural-output-2026-11-14.txt](m12-wp5-fief-7900-fit-natural-output-2026-11-14.txt).
- [m12-wp5-fief-8600-validation-seeds-2026-11-14.txt](m12-wp5-fief-8600-validation-seeds-2026-11-14.txt), [m12-wp5-fief-8600-validation-seats-2026-11-14.tsv](m12-wp5-fief-8600-validation-seats-2026-11-14.tsv), [m12-wp5-fief-8600-validation-natural-output-2026-11-14.txt](m12-wp5-fief-8600-validation-natural-output-2026-11-14.txt).
- [m12-wp5-fief-9500-live-output-2026-11-14.txt](m12-wp5-fief-9500-live-output-2026-11-14.txt): earlier four-seed 9,500 candidate screen.
- [m12-wp8d-d20-final-code-run-log-2026-11-15.txt](m12-wp8d-d20-final-code-run-log-2026-11-15.txt) and [m12-wp8d-land-food-oracle.md](m12-wp8d-land-food-oracle.md): completed D20 run and its final-code closeout evidence.
- [27-faith-and-fief-age.md](../27-faith-and-fief-age.md): D8, F-T1, and current draft/base policy.

## Superseding run-state note — 2026-11-16

The 20-seed F-T1 calibration in external candidate folder `candidate-5ee659bd` completed successfully and passed its final provenance check. It is pinned to aggregate `5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733`, numeric pack `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`, Release Harness `26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100`, and Sim `b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe`. The 80 seats included 73 KEY-6 completions, 6 right-censored after KEY-5, and 1 that did not reach KEY-5; the completed-seat median was 22.30 minutes. The calibration-only report is [here](m12-wp5-fief-ft1-calibration-analysis-5ee659bd-2026-11-16.md). The proportional first-pass candidate is base cost 6,700; the working registry and pack still hold 6,000 until the integrated freeze is prepared. This result is not acceptance or a final freeze, and the 80-world/320-seat holdout remains unused.

## External 7,300 holdout attempt — quarantined (2026-11-16)

At 11:28 UTC, six active processes were observed running `fief-match --pack .../final2-packs-7300/numeric-e5.pack --minutes 130 --seeds-file .../final2-out/ft1-holdout-7300/shard-{0..5}.seeds` from the separate `/Users/ejuchheim/Projects/CivSim/civsim-e5-dev` path. The pack file at that path hashed to `c4b27bbb6b20aaad0fc18a6555feab42ea08409bef9247d41a66895b27b04a86`; the invoked Release Harness file hashed to `94e3707c69bec6e4ce4044f3dd0f6ea9ddf42ae03ab8e6a68ab6bb229c5ac1ab`. No shard seed files or raw outputs were opened. The invocation does not establish a clean/current source aggregate or a complete pre/post provenance record, so this attempt is **quarantined and cannot qualify as F-T1 acceptance evidence**; do not analyze, reuse, or merge its output into another run.

Because the external attempt is labeled as the 7,300 holdout and its exact seed overlap with the frozen manifest was not checked, keep the frozen acceptance manifest sealed and do not launch it. After the intended source and candidate are frozen, preregister a fresh disjoint acceptance block and run that block once under complete current-source pins. The existing 20-world calibration remains calibration-only. The current calibration runner is still pinned to E5 base cost 6,000; candidate choice and any runner pin update remain blocked on the final freeze. D21's operational thresholds remain an autonomous draft interpretation, not newly user-confirmed tolerances.

The current per-seed runner and analyzer fixtures are now compatible: the analyzer imports its default calibration manifest from the F-T1 path and validates the runner's pre/post provenance and per-seed records. Focused tests pass with `python3 -m unittest docs/MVP/balance/tools/test_run_fief_ft1_calibration.py docs/MVP/balance/tools/test_analyze_fief_ft1_calibration.py` (12 tests). This is a script-level check only; it does not clear the dirty source tree or authorize a cohort.

## Calibration rerun manifest 2 (2026-11-16)

The first pinned calibration attempt used a clean frozen candidate at HEAD `fb2d5ced1d16920203adf238d1c459538bd55894`, but a separate preregistration directory was written into that checkout after preflight. The runner started only seed `20500859` (exit code 0), then its post-run cleanliness check failed; the other 19 rows were marked not run. Preserve output root `candidate-fb2d5ce-ft1-calibration-6000-r1` as a failed provenance attempt, and do not analyze or reuse its raw output. Treat `20500859` as consumed.

A replacement calibration manifest is [m12-wp5-fief-ft1-calibration-r2-seeds-2026-11-16.txt](m12-wp5-fief-ft1-calibration-r2-seeds-2026-11-16.txt), SHA-256 `ec992c60c6e4b2820f401de529a680a74b0bcd77eb33dc57d8e75eecc3ceb417`. It retains the other 19 original calibration seeds and replaces the consumed seed with `20519098`, selected without world or outcome screening. That replacement was checked against numeric entries in 37 local seed/manifest files, including the consumed seed and reserved F-T1 acceptance manifest. Stale external 7,300 shard files were not read; their exact seed overlap is unknown.

The runner's expected manifest digest and the analyzer's default manifest now identify rerun 2. Focused tests pass with `python3 -m unittest docs/MVP/balance/tools/test_run_fief_ft1_calibration.py docs/MVP/balance/tools/test_analyze_fief_ft1_calibration.py` (14 tests). No further match was launched. Before retrying, commit the scoped docs/tool/manifest changes, create a fresh clean candidate checkout and verify its adjacent Release binaries, then use a unique output root. Update the command pins from that checkout and its final runner hash; never resume r1 or reuse its output. Keep the acceptance holdout sealed and preregister a fresh disjoint acceptance block after candidate freeze.
