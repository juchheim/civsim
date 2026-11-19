# Fief A6 acceptance-search preflight — 2026-11-15

## Verified gate and harness facts

- A6 requires at least 10,000 distinct fixed policies screened, followed by all 16 selected diverse finalists validated on fresh mirrored 64-minute pairs. The gate fails on any finalist above 55%; exactly 55% passes. This is the Fief-mode requirement recorded in [14 §10.3](../14-ai-opponents.md#103-acceptance-targets) and the existing A6 harness report.
- `ExploitSearch.Search` screens the initial random policy plus `generations × children`; therefore 417 × 24 + 1 = **10,009** candidates. Its key set deduplicates candidates, and the implementation selects eight top-fitness plus eight diverse finalists.
- The focused `ExploitSearchGateTests` coverage exercises diagnostic defaults, unique-policy keys and generation, finalist selection/validation, seed-range validation, Fief chapter propagation, and pass/fail classification. The prior focused Release test run is documented as 17 passed, 0 failed; this preflight did not rerun it.
- The frozen screen/validation seed manifest is `m12-e5-9-fief-a6-finalist-seeds-2026-11-14.tsv`; its observed SHA-256 is `de2069b55ff16d22c607a3da0609370f4d398482b5f19da93b596fe785a86980`. It assigns screen seed 21010001 and validation pair seeds 21000001–21000003, which are disjoint.
- The candidate pack exists at `/tmp/civsim-e5-fit-candidate.pack` (394,498 bytes at inspection), but this preflight did not establish that it is the final pack for settled WP9 source.

## Current readiness and blocker

The A6 harness and policy-search source are modified in the shared working tree, along with many WP-E5 files. The running F-T4 D20 cohort is actively using `src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll` and the candidate pack. Per the cohort constraint, neither that binary nor the Release output was built or touched, and the large A6 search was not started. A6 remains **unmeasured** until WP9 changes settle and the cohort releases the shared Release harness; the pack/source pairing also needs confirmation at that point.

No production AI defaults, thresholds, or acceptance parameters were changed by this preflight. No test or search was run.

## Next runnable acceptance command

Once WP9 source and pack are final and the F-T4 cohort has stopped, build the Release harness, then run:

```sh
dotnet build src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-restore
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll ai \
  --pack /tmp/civsim-e5-fit-candidate.pack --mode fief --exploit \
  --generations 417 --children 24 --seeds 1 --screen-minutes 1 --minutes 64 \
  --first-seed 20261019 \
  --exploit-screen-first-seed 21010001 \
  --exploit-validation-first-seed 21000001 \
  --exploit-output docs/MVP/balance/m12-e5-9-fief-a6-output-2026-11-15.txt
```

Expected candidate count is 10,009. The command's report must show 16 finalists and 16 validations, with the explicit Fief chapter, screen seed, and three fresh validation seeds. A6 passes only when the highest validated rate is at most 55%. Keep the resulting output with the final pack/source identity and record any failed/incomplete run without treating it as acceptance evidence.
