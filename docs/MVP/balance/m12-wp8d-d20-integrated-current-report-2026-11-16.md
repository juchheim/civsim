# D20 AGR-11 validation on the integrated current Release build — 2026-11-16

**Status:** The frozen 36-pair D20 operational screen passes on the integrated current candidate. This closes the D20 rerun requirement; it does not promote AGR-11 or any E5 row. F-T8 remains open because this run is not its dedicated map-fill/Dominion/City-share cohort.

## Frozen inputs and execution

- Frozen 36-world/focus-seat list: [m12-wp8d-d20-final-code-seats-2026-11-14.tsv](m12-wp8d-d20-final-code-seats-2026-11-14.tsv), SHA-256 `c9a9b3c09e718a17fc72f04c2019bf8d93381bcaf56b18e0346c6843ae32c14b`.
- Current source aggregate at launch and after completion: `7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6`; HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`. The post-run workspace provenance is [m12-wp8d-d20-integrated-current-provenance-2026-11-16.json](m12-wp8d-d20-integrated-current-provenance-2026-11-16.json).
- Numeric E5 scratch pack `/tmp/civsim-fief-playtest-20261115-221136/numeric-e5.pack`, SHA-256 `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`.
- Release Harness DLL `src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll`, SHA-256 `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f`.
- Adjacent Release Sim DLL `src/CivSim.Harness/bin/Release/net10.0/CivSim.Sim.dll`, SHA-256 `a8cb7666042cf6651a6b9558b3d6db7b1f00566b5cc30041077e67b86540ad50`.
- Runtime: macOS 15.3, `osx-arm64`; .NET SDK 10.0.402, .NET runtime 10.0.13.

The runner used the exact frozen seat list and both treatment/control arms for 180 simulated minutes:

```sh
python3 docs/MVP/balance/tools/run_fief_land_long_horizon.py \
  --pack /tmp/civsim-fief-playtest-20261115-221136/numeric-e5.pack \
  --harness src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll \
  --minutes 180 --workers 8 \
  --output docs/MVP/balance/m12-wp8d-d20-integrated-current-output-2026-11-16.txt \
  docs/MVP/balance/m12-wp8d-d20-final-code-seats-2026-11-14.tsv
```

All 72/72 arm runs completed; the runner reported no errors. The workers loaded the frozen Harness and adjacent Sim DLLs noted above. Raw output SHA-256 is `c3db55aebfb7929a088d041c61aed61ea335fadfc7bd8ba986fb0d0e5c385e97`; runner log SHA-256 is `371fcbff812cfd4ae5d7dcef8e9228b1687f86b362a63ecf86aaef8eeb6e008c`.

## Operational D20 screen

The unchanged operational criteria in D20 are: at +12 seasons, weighted sustainable-capacity ratio 1.0805–1.1401 with seed-bootstrap 95% CI lower bound above 1; at +240 seasons and the 180-minute horizon, whole-match cohort means for both population and sustainable-capacity ratios 0.95–1.05. Use cohort means rather than per-world extrema, report all censors, and do not impute.

- **+12 capacity response: pass.** There were 34 complete focus-seat pairs and two censored pairs. Control-population-weighted sustainable-capacity ratio was **1.1067** (+10.67%); bootstrap 95% CI **1.1007–1.1126**; fitted weighted central 90% observed band **1.0833–1.1352**. The two focus seats never reached KEY-5 by the horizon: `20295617/c2` and `20295706/c3`.
- **+240 whole-match check: pass.** Among 34 matched worlds, mean population ratio was **1.002** and mean sustainable-capacity ratio **1.002**; both are within 0.95–1.05. The same two focus-seat pairs are censored at this focus-anchored offset and are omitted without imputation.
- **180-minute whole-match check: pass.** All 36 worlds have whole-match rows. Mean population ratio was **0.998**; mean sustainable-capacity ratio **1.000**. All 288 civilization-arm rows are present and 278 KEY-6 completions were recorded. No match ended early (`endState=None`, `matchEnded=False`).
- **Anchor integrity:** all 36 pairs match on pack, world and initial-start checksums; offset-zero whole-match state checksums match for all 34 eligible focus-seat pairs.
- **Final-horizon focus censoring:** six focus seats were censored in both arms (12 seat-arm records): `20292867/c3`, `20294314/c0`, `20294562/c3`, `20295113/c2`, `20295617/c2`, and `20295706/c3`. These do not remove any of the 36 whole-match horizon rows. No censored observations were imputed.

The reproducible weighted fit is [m12-wp8d-d20-integrated-current-weighted-fit-2026-11-16.txt](m12-wp8d-d20-integrated-current-weighted-fit-2026-11-16.txt), SHA-256 `3b94729e88ff29238ce5b83b6095785b0e57ab8ff6c424df56d2c5c6e83634e6`. The paired anchor and long-horizon audit is [m12-wp8d-d20-integrated-current-anchor-audit-2026-11-16.txt](m12-wp8d-d20-integrated-current-anchor-audit-2026-11-16.txt), SHA-256 `b5d044aca9b7837f82223e604d3e52b46dd50af12d7ccc31c1c70781c97bc800`.

Reproduction commands:

```sh
python3 docs/MVP/balance/tools/fief_land_population_weighted_fit.py \
  --seats docs/MVP/balance/m12-wp8d-d20-final-code-seats-2026-11-14.tsv \
  --offset 12 \
  docs/MVP/balance/m12-wp8d-d20-integrated-current-output-2026-11-16.txt
python3 docs/MVP/balance/tools/fief_land_anchor_audit.py \
  docs/MVP/balance/m12-wp8d-d20-integrated-current-output-2026-11-16.txt
```

## Descriptive F-T8 coverage from this output

The raw D20 observations include city-share fields. Focused city-share change averaged **+0.066 percentage points** at +12 seasons and **−0.084 pp** at the focus-anchored +240-season offset, each across 34 complete focus pairs. Whole-match city-share change averaged **−0.331 pp** at the +240 checkpoint and **−0.765 pp** across all 36 worlds at 180 minutes. These are descriptive D20 observations only; this analysis applies no F-T8 threshold. The output has no dedicated map-fill measure and no Dominion accrual/victory fields, so it cannot close F-T8.

## Remaining gates

D20 is closed on this integrated candidate. AGR-11 remains draft pending WP-E5-11. The dedicated F-T8 map-fill/Dominion/City-share cohort and its review remain open. WP-E5-11/12 and B5-11/12/13 gates are not closed by this D20 run; the Windows replay and the real eight-person comprehension study require their own prerequisites, and no human-participant data was collected here.
