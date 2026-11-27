# A6 current-source acceptance preregistration — 2026-11-27

## Frozen scope and outcome rule

Run the Fief A6 exploit search against the current closeout candidate `2fce6aa29eab5d75e0cd6018c44d6ea6d5a65b78`, content/src/game tree `de5be3c634f2b806c87f0b9ea49cde8352f0f4d5`, source aggregate `227902576ec15604a8399d395b170d55305ad0e50823e30fd49087d8f742342b`, and numeric pack SHA-256 `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`.

Use the established gate unchanged: search at least 10,000 distinct policies, select the diverse set of 16 finalists, and validate each on three mirrored 64-minute seed pairs. A6 passes only if all 16 validations complete and no finalist's Warlord win rate exceeds 55%. This result applies only to this searched policy set.

## Frozen seeds

- Policy-generation seed: `20261127`.
- Screen world: `89010001`.
- Fresh validation worlds: `89000001`–`89000003`.
- Screen and validation world seeds are disjoint from each other and the prior A6 schedule. This manifest was checked against recorded E5 seed and run-metadata files before launch.
- Seed TSV: [current-source seed manifest](m12-e5-9-fief-a6-current-source-seeds-2026-11-27.tsv), SHA-256 `22490d657932a3ae70d0009ae54ee98c610ddf6efcf1fe0a5c87240ae1021b5c`.

The complete seed list and SHA-256 are recorded in the run metadata with the exact command, candidate pins, output hashes and before/after verification.

## Command

```sh
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll ai \
  --pack /private/tmp/e5-closeout-acceptance-2fce6aa/numeric-e5.pack \
  --mode fief --exploit \
  --generations 417 --children 24 --seeds 1 --screen-minutes 1 --minutes 64 \
  --first-seed 20261127 \
  --exploit-screen-first-seed 89010001 \
  --exploit-validation-first-seed 89000001 \
  --exploit-output /private/tmp/e5-closeout-a6-2fce6aa-20261127.txt
```

The current-source run has not started. Do not change the search, seed assignments, or pass threshold after viewing outcomes. Preserve partial outputs as incomplete if the run is interrupted.
