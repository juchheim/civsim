# Fief A6 harness plumbing — 2026-11-14

This change connects the existing fixed-policy exploit search to explicit chapter rules and seed ranges. It does not change production AI policy, A6 thresholds, or run the 10,000-policy search.

## Harness changes

- `ExploitSearch.Search` and both `Measure` paths accept optional `ChapterRules`; exploit matches pass them through to `Tournament.PlayForLegacy` and `MatchModel.Create`. The `ai --mode fief` selector now therefore applies to screen and finalist matches.
- The search accepts explicit `--exploit-screen-first-seed` and `--exploit-validation-first-seed` values. It rejects overlapping contiguous screen/validation ranges. Defaults preserve the prior deterministic ranges (`BaseSeed + 5000` and `BaseSeed + 9000`).
- Finalist validation records both mirrored match scores for each seed pair. The console and optional `--exploit-output FILE` report include the chapter, candidate counts, horizons, seed ranges, all selected policies, validation win rates, and each seed pair's policy/Warlord Legacy scores. This preserves the eight best-by-fitness and eight additional most-diverse finalist selection procedure.

## Frozen run setup

The seed manifest is [m12-e5-9-fief-a6-finalist-seeds-2026-11-14.tsv](m12-e5-9-fief-a6-finalist-seeds-2026-11-14.tsv), SHA-256 `de2069b55ff16d22c607a3da0609370f4d398482b5f19da93b596fe785a86980`.

After WP9 tuning/source changes settle and the final Release harness is rebuilt, the acceptance-size command is:

```sh
dotnet build src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-restore
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll ai \
  --pack /tmp/civsim-e5-fit-candidate.pack --mode fief --exploit \
  --generations 417 --children 24 --seeds 1 --screen-minutes 1 --minutes 64 \
  --first-seed 20261019 \
  --exploit-screen-first-seed 21010001 \
  --exploit-validation-first-seed 21000001 \
  --exploit-output docs/MVP/balance/m12-e5-9-fief-a6-output-2026-11-14.txt
```

The candidate count is 10,009 (the initial candidate plus 417 generations of 24 unique children). Validation uses seeds 21000001–21000003, three mirrored pairs per finalist. The run is intentionally not started here; the WP9 cohort must first settle on the final source and binary.

## Verification

```sh
dotnet test src/CivSim.Tests/CivSim.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ExploitSearchGateTests'
```

Result: Release build succeeded; 17 passed, 0 failed, 0 skipped. Coverage includes Fief chapter propagation through the legacy-only match path, default and explicit seed-range resolution, overlap rejection, stable finalist report formatting, and the existing A6 gate/selection checks.
