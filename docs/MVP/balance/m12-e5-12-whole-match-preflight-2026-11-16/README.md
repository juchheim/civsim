# WP-E5-12 whole-match held-out preflight — 2026-11-16

**Historical status — 2026-11-26:** The user explicitly enabled all 50 numeric rows before acceptance, so a second promotion step is not required. At that time the 80-world natural holdout was still unused. The runner adds report-only observers for power, health, arms lifecycle, institutions, siege, WP-E5-7 denominators, territory, and sea routes so one match cohort can also support compatible F-T measurements. Its focused fixtures passed 4/4.

**Current status — 2026-11-27:** The 2026-11-16 35M seed block was subsequently consumed in an 80-match run on pre-correction candidate `0f7bdd2`; it is diagnostic and must not be reused for acceptance after the GOV-11 implementation change. The fresh, collision-audited acceptance manifest is [the 2026-11-27 r1 block](../m12-e5-12-natural-seeds-2026-11-27-r1/README.md). The candidate must still be frozen and the complete run has not yet started.

**Historical 2026-11-16 preflight:** preparation only; no production matches had been run at that time. The 35M block below was later consumed by the diagnostic 80-match run and is no longer fresh for acceptance.

## Cohort

Manifest: `m12-e5-12-natural-seeds-2026-11-16.txt` (80 unique ascending seeds, sampled uniformly without replacement by `random.Random(20261116).sample(range(35_000_000, 36_000_000), 80)`, then sorted). SHA-256: `c3ef15f4654a0fb1a0834a61dc6516dfc407493d2a5e7f1e4c8e22684e87b461`.

The reserved 2026-11-15 block is 33,000,000–33,000,079. WP-E5-7 currently reserves 34,000,000–34,000,019 for calibration and 34,000,020–34,000,099 for holdout. The new block is disjoint from those and all seed manifests currently present in the repository, including the 31M WP-E5-11 cohorts and the 32M B5-03 cohort. It is natural Fief only: do not pass `--grant-e5`; run every seed without geography, progress, or outcome screening.

## Final freeze requirements

All 50 rows are already numeric and standard Fief launches are enabled by user direction. Freeze this current integrated state; do not change catalog statuses as part of the cohort. Do not use the known `bd0f4b8…` / `5ee659bd…` diagnostic candidate or its older pack/DLLs as WP-E5-12 acceptance inputs. At freeze, record and independently recheck:

- Clean committed candidate HEAD and tree SHA.
- `content+src+game` source aggregate SHA and complete source file manifest SHA.
- Final numeric pack SHA-256.
- Adjacent Release `CivSim.Harness.dll` and `CivSim.Sim.dll` SHA-256 values.
- Frozen WP-E5-12 runner source SHA-256 and its required helper/tool SHA-256 values.
- Manifest SHA above, 130-minute horizon, and no `--grant-e5` command mode.

The pins in this historical preflight are intentionally placeholders. Fill them from the clean final freeze immediately before the run; never copy old candidate pins into the run record.

## Runner audit / launch blocker

The WP-E5-12 whole-match runner is now integrated at `docs/MVP/balance/tools/run_wp_e5_12_whole_match.py`. `run_frozen_e5_cohort.py` remains B5-11-specific and is not used for this cohort. The WP-E5-12 runner has pinned per-match provenance, structured metadata, and mock fixtures; freeze the exact tool bytes again with the final candidate before any launch. The runner:

1. Requires explicit manifest path+SHA, exactly these 80 unique seeds, a clean candidate HEAD/tree/source aggregate, pack, Release Harness, adjacent Sim, and its own runner/helper hashes.
2. Rejects a present output root, records an immutable attempt ID, and never resumes or overwrites. Launch exactly one process per seed and keep failures in the 80-match denominator.
3. Runs the command below with fresh, absent per-seed replay directories, then records pre/post source/binary/runner provenance, exact argv, start/end and wall time, process exit code, raw log byte count and SHA, replay SHA, replay-index verification result/final checksum, parse status, match end reason, simulated minutes, and all four seats’ KEY-5 / KEY-6 times and censor categories.
4. Writes a row for every manifest seed even after an error: `complete`, `match-ended-early`, `process-error`, `parse-error`, `replay-missing`, `replay-verification-failed`, `provenance-changed`, `runner-error`, `not-started-after-runner-error`, or `not-started-after-provenance-change`. A nonzero process exit, missing output, parse failure, changed pin, or bad replay is never dropped or replaced under this manifest.
5. Rechecks candidate pins before and after every match and refuses the entire run if the checkout is dirty or any input/tool hash changes.

The canonical wrapper is `run_wp_e5_12_whole_match.py` (SHA-256 `fa7790a9355891b8b5716634ab5cecd86c7cf319daf4dc814613fdf4eae0d068`); its `cohort_provenance.py` helper is `496f1064b4b02f3e28c8aaff33140a8b2e13d7f8e1a785a68a8cd32f0138a10a`. Fixture file: `test_run_wp_e5_12_whole_match.py` (SHA-256 `bfeade175ed573a86ee1dafa960b3ed0a9a049dd134dbcf90363ba0a8ee8fb52`). Recompute these pins if either tool changes.

After freeze, invoke the wrapper once against a new absent output root:

```sh
python3 docs/MVP/balance/tools/run_wp_e5_12_whole_match.py \
  --repo "$REPO" --pack "$FINAL_PACK" \
  --harness-dll "$RELEASE/CivSim.Harness.dll" --sim-dll "$RELEASE/CivSim.Sim.dll" \
  --manifest docs/MVP/balance/m12-e5-12-natural-seeds-2026-11-16.txt \
  --expected-manifest-sha256 c3ef15f4654a0fb1a0834a61dc6516dfc407493d2a5e7f1e4c8e22684e87b461 \
  --expected-head "$FINAL_HEAD" --expected-tree "$FINAL_TREE" \
  --expected-source-aggregate-sha256 "$FINAL_SOURCE_AGGREGATE" \
  --expected-pack-sha256 "$FINAL_PACK_SHA256" \
  --expected-harness-sha256 "$FINAL_HARNESS_SHA256" --expected-sim-sha256 "$FINAL_SIM_SHA256" \
  --expected-runner-sha256 fa7790a9355891b8b5716634ab5cecd86c7cf319daf4dc814613fdf4eae0d068 \
  --expected-provenance-helper-sha256 496f1064b4b02f3e28c8aaff33140a8b2e13d7f8e1a785a68a8cd32f0138a10a \
  --output-root "$OUTPUT_ROOT"
```

The per-seed Harness command, expanded and recorded by the wrapper, is:

```sh
dotnet "$HARNESS_DLL" fief-match \
  --pack "$FINAL_PACK" \
  --seed "$SEED" \
  --minutes 130 \
  --record-dir "$OUTPUT_ROOT/replays/$SEED" \
  --fief-power-ledger --fief-health-trace --fief-arms-lifecycle-trace \
  --fief-institution-trace --fief-siege-trace --fief-siege-strategist-trace \
  --fief-wp7-trace --fief-territory-trace --fief-sea10-trace
```

The 2026-11-26 closeout update adds these report-only observers to the wrapper. They let this same 80-world natural holdout measure F-T2, F-T5, F-T6, F-T7, F-T8, SEA-10, the arms-production lag, and WP-E5-7 event denominators while preserving the 130-minute replay-verified whole matches. The WP7 observer is not the paired treatment/control experiment; the F-T3 fixture, F-T4 food/capacity anchors, and WP7 paired cohort remain separate because they use different controlled inputs.

Capture combined stdout/stderr to `$OUTPUT_ROOT/logs/$SEED.log`. `--record-dir` must be a new directory for every seed; do not create it before the Harness call. The runner invokes the pinned Harness replay-index command once per successful replay, using that seed's one-line manifest and the same pack, and writes the index JSON under `replay-index/$SEED.json`. Preserve raw logs/replays and record each replay’s verification status, final checksum, and index metadata. Do not infer success from an exit code alone.

## Frozen interpretation and reporting

The WP-E5-5 / F-T1 autonomous held-out rule in `m12-wp5-fief-ft1-final-code-preflight-2026-11-15.md` supplies the current operational thresholds; the 80-world result is still diagnostic unless all applicable WP-E5-11 gates are satisfied:

- Launch and account for all 80 whole matches and 320 seats. At least 256/320 seats must complete KEY-6 within the pinned 130-minute horizon.
- Among seats completing both KEY-5 and KEY-6, median own KEY-5→KEY-6 time must be 22.5–27.5 minutes.
- Do not impute intervals for seats that miss KEY-5 or KEY-6. Both categories count as incomplete against the 256/320 completion floor. Report completion and interval quantiles by archetype and overall, all match-end/horizon reasons, every process/parse/replay failure, and any provenance drift.
- These thresholds are the documented autonomous D21 operational interpretation; they were not separately user-confirmed. Do not change them after opening the holdout.

Use a fresh absent output root outside the repository, for example:

`/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/wp-e5-12-final-2026-11-16-r1`

This root was absent when this plan was prepared. Create a separate new attempt root for any rerun; preserve the failed attempt and consume its seed block.

## Separate human gate

The 80-match study does not satisfy B5-12 comprehension. After final Fief view availability, the human gate still requires eight real, uncoached participants using the same final build and the exact task/questions/scoring in `m12-e5-12-human-session-kit.md`. Keep anonymous IDs and verbatim responses; do not teach mechanics. Pass remains at least 5/8 participants demonstrating both one mill/fuel bottleneck and one faith or money effect. Simulation output cannot replace those sessions.

## Evidence basis

- `m12-e5-12-playtest-preflight-2026-11-15.md`: prior reserved 80-seed holdout and separate human protocol.
- `m12-wp5-fief-ft1-final-code-preflight-2026-11-15.md`, D21 section: 80 whole matches / 320 seats and the 256/320, 22.5–27.5 minute interpretation.
- `27-faith-and-fief-age.md` §10 / B5-12: comprehension is a separate 5/8 human gate.
- Harness path: `Program.cs` routes `fief-match` to `IronMatchCommand.RunFief`; `--record-dir` is Fief-only and must not exist; match summary prints each civilization’s KEY-4/KEY-5 and KEY-5→KEY-6 completion/right-censor/no-KEY-5 result. `ReplayIndexCommand` verifies each replay and yields final checksum evidence.
