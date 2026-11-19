# F-T1 tolerance status clarification (2026-11-15)

This note records the provenance of the F-T1 pass/fail thresholds before any final-code cohort is run.

The D21 entry in [27-faith-and-fief-age.md](../27-faith-and-fief-age.md#decision-log) gives a completed-seat median interval of 22.5–27.5 minutes and a completion floor of 256/320 (80%) for the frozen 80-world holdout. D21 says these are an **autonomous interpretation** under the user's earlier instruction to close gates without further input; it also says they were **not separately user-confirmed**. The confirmed design target remains “about 25 minutes,” while these numeric boundaries are the operational interpretation recorded by D21.

Accordingly, future evidence may report pass/fail against the **D21 autonomous gate**, but should not describe the thresholds as user-confirmed tolerances. Keep the 80-world holdout sealed until the calibration candidate and source/pack are frozen. No cohort was launched for this clarification; no catalog or simulation source was changed.

## Execution status at clarification

- No F-T1 or D20 cohort process was running when checked.
- Existing F-T1 results use workspace aggregate `87996d7a3751c48ecdf40be72c83f903dae45c1370ec383f33cb46d160476857`; the current working-tree aggregate at the read-only preflight check was `4acd91919ebf5549a3afbab3b13c8c4c59e71b09965a5df031cced10b7b47736` (HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`). Source is still changing, so this current aggregate is not a freeze identity.
- Existing final-code D20 report identifies its earlier loaded `CivSim.Sim.dll` as predating WP-E5-6 Know-how retention. D20 must be rerun after source freeze against the exact final integrated Release binaries and pinned pack.
- A Release build completed during preflight, before the source-freeze instruction arrived. No simulation was launched. Do not treat its output hashes as cohort provenance; rebuild and record hashes after the eventual freeze.
