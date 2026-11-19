# WP-E5 B5-11 Fief performance instrumentation

**Status:** An opt-in Release harness command now measures Fief simulation ticks, process GC activity, allocations, and view-frame figure counts after each civilization's own KEY-5. This adds diagnostics only; it does not change simulation rules, frame representation, or acceptance thresholds. No long cohort has been run for this slice.

## Measurement definitions

- A **tick** is one `SimRunner.Advance(1)` / `StepOnce`. The command times only that call. The 4 ms gate is interpreted as the arithmetic mean wall time per measured simulation tick; per-seed and pooled p50/p95/p99/max are also emitted. Checksum work performed by `SimRunner` at its normal interval is included.
- The **16 ms GC gate** is interpreted as aggregate .NET GC pause time divided by the number of measured simulation ticks. `GC.GetTotalPauseDuration` and generation 0/1/2 collection deltas are sampled around the measured phase. Allocation bytes are reported both total and per measured tick. Because the GC API is process-wide, run the command in an otherwise idle process. The value also includes the once-per-season view-frame samples described below; tick stopwatch values exclude those frame builds.
- Warm-up ticks execute normally but are excluded from stopwatch, GC, and allocation metrics. The default exclusion is five simulated minutes; set `--warmup-minutes` to change it. View/population observations are retained for every post-KEY-5 season, including the warm-up interval, so the population distribution is not conditioned on the timing window.
- At each season boundary, each civilization that has completed its own KEY-5 gets a row with living population and its own-view `MatchFrame.FigureCount`. These counts are per-seat snapshots; the summary reports the linearly interpolated p95 across seat-season observations and the number over 600.

For B5-11 acceptance, each of the natural and E5-granted 20-seed cohorts must independently meet the pooled arithmetic mean tick limit of **≤4 ms** and the pooled process-GC-pause-per-measured-tick limit of **≤16 ms/tick**. Pool by total measured ticks (sum of per-seed tick time × that seed's measured ticks; sum of GC pause milliseconds ÷ sum of measured ticks), not by an unweighted average of seed means. Keep censored/early-terminal seeds in the report. The guarded wrapper's `status: complete` records successful commands and stable pins only; it does not calculate or certify either threshold. The p95 view-figure value of 600 remains diagnostic, as described below.

## Current figure-budget behavior

The 600-figure-per-civilization value is a **diagnostic threshold, not an enforced runtime cap**. `MatchFrame.Reserve` preallocates the intended scale (about 2,400 total / 600 per civilization), but `FrameBuilder.FillFigures` requests capacity for every economy figure and the frame arrays grow when needed. No 1:5 view downsampling was added: first measure p95 counts, and only consider a view-only representation change if the recorded p95 exceeds 600. Simulation population remains 1:1.

The B5-11 wrapper pins source, pack, Harness/Sim DLLs, manifests, runner, and provenance helper, but it does not hash the selected `dotnet` host binary or identify the CPU model. The Harness sidecar records runtime framework, OS, and architecture. Preserve `dotnet --info` and host model/load notes for both cohorts, and run them sequentially on the same otherwise idle machine.

## Reproduce

Use the exact clean, pinned numeric P candidate, its adjacent Release Harness/Sim pair, a frozen B5-11 seed manifest, and a fresh output root outside the repository. The acceptance wrapper in `tools/run_frozen_e5_cohort.py` runs one seed per Harness invocation, pins the source/pack/binaries/manifest/helper files before and after every seed, and hashes raw logs and outputs. Its required expected pins and both exact invocation shapes are documented in `m12-wp11-b5-11-current-candidate-preflight-2026-11-16.md`. The direct Harness command below remains a local diagnostic example; it is not the acceptance runner.

```sh
dotnet build src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-restore
dotnet test src/CivSim.Tests/CivSim.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~FiefPerformanceCommandTests
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll fief-perf \
  --pack /tmp/civsim-e5-numeric.pack \
  --seeds-file /tmp/civsim-e5-b5-11-seeds.txt \
  --minutes 130 --warmup-minutes 5 --grant-e5 \
  --output-prefix /tmp/civsim-e5-b5-11/perf
```

`perf-ticks.csv` contains one row per seed with observed and expected timed tick counts, stop reason, censor flag, final tick/checksum, timing quantiles, GC pause total and pause per tick, generation collection deltas, and allocation total/per tick. `perf-tick-samples.csv` preserves every measured tick's simulation tick and elapsed milliseconds. `perf-seats.csv` contains seed, civilization, archetype, season/tick, own KEY-5 completion tick, living population, view-frame figure count, the 600 diagnostic threshold, and an over-budget flag. `perf-provenance.txt` records natural versus E5-granted cohort, timing configuration, absolute manifest/pack paths and SHA-256 hashes, executable path/hash, runtime, OS, architecture, and process ID. Console summaries report the pooled tick quantiles and p95 population/figure counts. A seed that ends before the requested horizon is explicitly marked `terminal-before-horizon` and censored; its short exposure remains visible in the row and raw samples.

Focused validation on this instrumentation passed: `dotnet build src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-restore` (0 warnings, 0 errors) and `dotnet test src/CivSim.Tests/CivSim.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~FiefPerformanceCommandTests` (3/3). The third test compares a one-minute instrumented Fief simulation's final tick/checksum with the normal uninstrumented stepping path. No multi-seed performance cohort was launched.

Run one natural cohort and one `--grant-e5` stress cohort with separately frozen seed files for typical and E5-loaded behavior. Preserve both seed files, the scratch-pack SHA-256, raw console output, and CSVs. B5-11 remains open until representative measurements demonstrate the existing thresholds and any observed p95 view-budget excess is addressed. The gate numbers themselves remain unchanged.
