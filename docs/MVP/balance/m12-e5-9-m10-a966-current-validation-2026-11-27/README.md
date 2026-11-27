# M10 AI current-candidate validation — 2026-11-27

This run uses the registered mirrored seed block `80000000–80000039` (40 seeds, 80 matches) and the numeric E5 pack. It is current-candidate **runtime-equivalent validation**, not a new held-out sample.

- Accepted source candidate: `a966ec377a9d650b2b90b4467827c777e115c603`
- Run began at `8774ebdfa7dfe078fbcf366390bd5d830db6769a` and ended at `a966ec377a9d650b2b90b4467827c777e115c603`.
- `source_unchanged=false` is preserved. The only `content/`, `src/`, or `game/` difference between those revisions is `src/CivSim.Tests/FiefWholeMatchFogFairnessTests.cs`; simulation source did not change during the run.
- Pack SHA-256: `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`
- Measured Harness SHA-256: `0224459af3d84c3c61773b4324798d246b26e75b445adc41c8b0297a074dd950`
- Measured Sim SHA-256: `51562e50ab950f85e500d995b475cbe5a86b8f99ac688260a283fb9989d349d7`
- Manifest SHA-256: `3e246a2f3293a76aa8fc2c9f01f9b818c2e0fe7459121aa24e2506f370ce055c`
- Exit code: `1`. The campaign exits nonzero because A1 measured below its unchanged target; the other M10 gates pass.
- Raw output SHA-256: `3dba9c9d0f881d8b226c5147d2afc7d9878315cedd0c18ad8bf030fb4e3b3c52`
- Metadata SHA-256: `71f1756b25521ec1d2c06d7c76141bc52ced76edb74518ea0f85607d0e8fa49e`

A1 reports rounded 74% over 80 matches (measured FAIL against 80%). The exact current numerator was not recorded. The block reuses the registered seeds; no independent holdout is claimed. A1 is closed only by the owner's explicit exception, recorded in [the A1 disposition](m12-e5-11-a1-owner-accepted-near-miss-2026-11-26.md).

The remaining measured M10 gates A2–A5, A7, and B27–B31 passed. A6 and A8 were measured separately; their current-candidate evidence is in the [A6 report](m12-e5-9-fief-a6-acceptance-report-2026-11-15.md) and [A8 bundle](m12-e5-9-a8-current-candidate-2026-11-27/README.md).
