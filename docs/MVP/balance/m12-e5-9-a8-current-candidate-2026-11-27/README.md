# A8 whole-match hidden-information controls — current candidate

## Result

**PASS.** The guarded eight-seed whole-match run completed on candidate `a966ec377a9d650b2b90b4467827c777e115c603`. The run covered 64 simulated minutes per match with observations every 16 seasons. The candidate tree and runtime binaries remained unchanged during the run; preflight and postflight pins match.

## Coverage and checks

- 8 natural Fief seeds; 2,304 observer checkpoints.
- Unseen-state composite: 640/640 applied; 0 invariance failures; 0 eligible controls not applied.
- Visible deposit, stock, fleet, and horse controls: 128 checkpoints each; all applied and detected, with zero misses.
- Null controls: 512 applied, 0 with diffs.
- Tier-B composite controls: 512 applied, 0 with input or decision diffs.
- Clone checksum mismatches: 0; validator strict failures: 0; composite failures: 0; visible-control misses: 0.

## Test-fixture note

The control setup now ensures that every seed has a controlled horse source in the test clone when the natural map has no horse herd but does have a wild-herd slot. This supplies the visible-horse control without modifying the natural match or candidate simulation. The frozen eight-seed cohort and all control assertions remained intact.

## Pins

- Candidate commit: `a966ec377a9d650b2b90b4467827c777e115c603`
- Candidate tree: `aa860512e8f97f6e1c35ebe5548e32f5159e73ef`
- Source aggregate: `e0b43203a6622829816f85b924e986995bffd474a82a78ce819272025a117048`
- Numeric pack SHA-256: `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`
- Sim SHA-256: `90fe2bed466ec7dc6903e27e4d6a1324d26af5fc5c1d082641e2fd7dd687c86d`
- Harness SHA-256: `1cd422d06fe28e9383eebb0d00dd4de551d3a7b81eee11429f50586cc4e8baf7`
- Test assembly SHA-256: `279035650540e27f496a123fa87fbfd5392ece252005ca8665b5aa9d9a457023`
- Summary SHA-256: `de506c25bfe79bc326bd7bdc7bf54ce542d7f258332c533fd3c0ef1d09e60651`
- Rows SHA-256: `948c3837d8808cceafccb861e840bec0c131cb908743a0c5190dc80a61ca5d2e`
- Validation SHA-256: `776551872c48eab8abc920baf9448492b536e7ac442734562d12989809488f4e`

Raw rows, summary, validation, run metadata, build and test logs, preflight record, and exact run pins are included alongside this report.
