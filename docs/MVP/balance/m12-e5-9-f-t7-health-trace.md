# F-T7 health trace seam

`fief-match --fief-health-trace` enables diagnostic-only health rows. It does not write snapshots or enter checksums. With tracing disabled, the match output shape and deterministic state are unchanged.

Example:

```sh
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll fief-match \
  --pack /path/to/natural-fief.pack --seed "$seed" --minutes 130 \
  --fief-health-trace
```

`--seed` and `--seeds` run a contiguous seed range. The existing harness does not accept an explicit manifest; preserve the frozen cohort's exact seeds by invoking each seed separately or by running contiguous compatible blocks.

Each `fief-health-trace` line is one zero-based mortality season and civilization. The harness advances one 150-tick season, then reads the hazards and death events accumulated during that completed season; the row's season is therefore `runner.Tick / 150 - 1`. Death counts are season-local. Each row includes `ownKey5Season` and `seasonOffsetFromOwnKey5` (`-1` until that civ completes KEY-5), so cohort analysis can select post-own-KEY-5 seasons. Rows report expected hazard totals by cause, their total and epidemic share, observed epidemic and all-cause natural deaths, and `adultEquivalentPersonSeasons`. Expected starvation includes both baseline starvation and the conditional winter-shortfall surcharge. Expected epidemic share is `expectedEpidemic / expectedTotal`, or zero when expected total is zero.

The opt-in trace now also emits `observedPoxDeaths` and `observedPlagueDeaths`. These count epidemic deaths of infected figures while their settlement's active outbreak is Pox or Plague, respectively; other epidemic deaths remain in `observedEpidemicDeaths` only. `quarantine` reports whether that civilization has MED-08 at the season boundary. `importedOutbreakAttempts`, `importedOutbreakBlocked`, and `importedOutbreakSuccesses` are the season-local counts of valid Fief route-import calls: blocked means one of MED-08's keyed import gates rejected the arrival, and success means the outbreak was accepted into the pending imported-outbreak slot. The counts do not include invalid classes or destinations rejected before the Fief import gate. The attempt total reconciles as blocked plus successful; without MED-08 valid arrivals succeed directly.

Adult-equivalent exposure is season-summed at the same pre-mortality boundary as expected hazards: each figure age 14+ contributes 1.0 person-season; each child under 14 contributes 0.5. Thus it is exposure for the population at risk during that season, including figures who die during it.

The output supports the proposed 10–14% expected epidemic-hazard-share band and supplies observed epidemic and total death rates per adult-equivalent person-season. Focused fixtures verify Pox/Plague attribution against actual seeded mortality events and quarantine import counters against the existing keyed gate results; the real route-path fixture exercises 256 deterministic arrivals through MED-08 keyed gates and reconciles attempts with block/success outcomes. This is fixture coverage only. The recorded natural calibration cohort had zero valid route-import attempts, zero keyed blocks, and zero accepted imports, so natural MED-08 import behavior has no exposure and must not be called a natural import pass. The trace does not itself evaluate the 12% confidence-interval gate. That requires filtering each civ to post-own-KEY-5 rows, pooling the frozen natural cohort by seed, and calculating a seeded-bootstrap interval over seed-level ratios; rerun the cohort and analyzer after the final source freeze.
