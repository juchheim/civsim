# WP-E5-9 A6 exploit search — 2026-11-27

## Result

**A6 passes on the pinned numeric E5 runtime tuple.** The search evaluated 10,009 distinct policies and advanced 16 finalists. All 16 completed three fresh mirrored 64-minute validation pairs; the highest observed result was 16.67%, below the 55% exploit ceiling. No validated finalist exceeded the limit.

The protocol used policy seed `20261127`, screen world `84000000`, and validation worlds `84000001–84000003`. It searched 417 generations × 24 children plus the initial policy. Screen and validation seed manifests are preserved with the raw run artifacts.

## Candidate and provenance

The run started from candidate `8774ebdfa7dfe078fbcf366390bd5d830db6769a`, source aggregate `840c1bf2d8824815b9f1ef11b9768c2f28b39812d9fb7c0e3b4ba49008bd914a`, pack SHA-256 `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`, Harness SHA-256 `0224459af3d84c3c61773b4324798d246b26e75b445adc41c8b0297a074dd950`, and Sim SHA-256 `51562e50ab950f85e500d995b475cbe5a86b8f99ac688260a283fb9989d349d7`.

The runner records `source_unchanged: false` because the A8 test-fixture correction landed while the search was running. Its before/after file manifests show exactly one source-file change: `src/CivSim.Tests/FiefWholeMatchFogFairnessTests.cs`. The post-run 1,177-file manifest matches current candidate `a966ec377a9d650b2b90b4467827c777e115c603` file-for-file. That test-only change is not compiled into or called by the pinned A6 Harness/Sim execution; the pack and runtime binaries remained fixed. This is therefore source-equivalent evidence for current candidate A6, while the source-change flag remains visible in the preserved metadata.

## Raw artifacts

The complete run record, output, finalist list, and seed manifests are preserved alongside this report. SHA-256 values:

- Raw output: `65c43d173f7a7a0b3dad4309cbf3d4ab2d6c7f55039e3ceae4966e085cd0dc68`
- Finalist list: `b6e9122f22b2025fa5f79af2a6d04d1a59d619f5ac0a30c449da392e85487a0e`
- Run metadata: `bc42715b9e3eb8e067927373cf7092ecdd8ef081e4a0fe139a7eeb8e24b7dde7`
- Screen-seed manifest: `11792e22f3143f9143394dc5dc321745fa88d20bdd22573071fb6dcca841a9a6`
- Validation-seed manifest: `8836a793a8a96e6717509f609b7f1f7c99707c152be47a0b8334518ef71d1af8`
