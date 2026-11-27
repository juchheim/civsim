# WP-E5-11 A1 final disposition — 2026-11-27

## Gate result

**A1: PASS BY OWNER EXCEPTION** for frozen E5 candidate `8774ebdfa7dfe078fbcf366390bd5d830db6769a`.

The unchanged nominal requirement is **64/80 (80%)**. The earlier detailed baseline measured **59/80 mirrored matches (73.75%)** on `c3aa4e063244748ffb1f951050ded4003ce8262a`, below that target. A fresh A1 leg on this frozen candidate now reports **74% over 80 matches — FAIL**; the test output rounds to whole percentages and allows half-wins for ties, so this line alone does not establish an exact integer win count. Its preserved 165-byte raw A1 output is [here](m12-e5-11-a1-current-leg-2026-11-27.raw.txt), SHA-256 `1b322d5f8020692a469247a96031bf7a93cc1923aba80b43b5ea5b15e475e8ca`. The nominal threshold and measured failures remain unchanged; this disposition changes release acceptance only.

## Astra re-review

Astra re-reviewed the exact current closeout candidate on 2026-11-27 and found no defensible additional A1 tuning change. It verified HEAD `a966ec377a9d650b2b90b4467827c777e115c603`, tree `aa860512e8f97f6e1c35ebe5548e32f5159e73ef`, and source aggregate `e0b43203a6622829816f85b924e986995bffd474a82a78ce819272025a117048`. The only delta from the measured A1 candidate `8774ebdfa7dfe078fbcf366390bd5d830db6769a` is `src/CivSim.Tests/FiefWholeMatchFogFairnessTests.cs`; the content, Sim and Harness sources are identical. The A8 fixture is outside the Harness dependency graph, so the completed A1 measurement carries forward by verified runtime-source equivalence.

The measured A1 leg reports **74% over 80 matches — FAIL**. Its exact M10 runtime tuple is Harness SHA-256 `0224459af3d84c3c61773b4324798d246b26e75b445adc41c8b0297a074dd950`, Sim SHA-256 `51562e50ab950f85e500d995b475cbe5a86b8f99ac688260a283fb9989d349d7`, and pack SHA-256 `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`; these match the M10 run metadata. The raw-output file is still shared with the running M10 campaign, so its full-file hash is provisional; the first 165 bytes are separately preserved and hashed above. The seed-manifest SHA-256 is `3e246a2f3293a76aa8fc2c9f01f9b818c2e0fe7459121aa24e2506f370ce055c`.

Earlier detailed evidence shows predominantly early growth/Dominion deficits; GOV-02 adjustments returned 58/80 versus 59/80 at baseline. The current code already corrects defense-queue coupling that could suppress calm-frontier GOV-02 value. No scoped change was supported by Astra's fresh review, so A1 is officially accepted by the owner's near-miss exception. The prior exact 59/80 count remains attributed to its own candidate and detailed result.

## Frozen candidate identity

- Candidate HEAD: `8774ebdfa7dfe078fbcf366390bd5d830db6769a`
- Git tree: `b76ab434580f7ebbf77d0799c92cce5b255e3f49`
- Source aggregate at freeze: `840c1bf2d8824815b9f1ef11b9768c2f28b39812d9fb7c0e3b4ba49008bd914a`
- Numeric pack SHA-256: `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`

This disposition is kept on a separate evidence branch so adding the acceptance record does not change the frozen candidate's identity.

## Applicability to the A8 harness-fix candidate

The current closeout candidate is `a966ec377a9d650b2b90b4467827c777e115c603` (tree `aa860512e8f97f6e1c35ebe5548e32f5159e73ef`, source aggregate `e0b43203a6622829816f85b924e986995bffd474a82a78ce819272025a117048`). Its only source-file delta from the reviewed `8774` candidate is `src/CivSim.Tests/FiefWholeMatchFogFairnessTests.cs`, the A8 test-only fixture correction. AI strategy, simulation behavior, and catalog content are unchanged, and the numeric pack remains byte-identical. Therefore the A1 owner exception applies to `a966ec3`; it carries forward the measured current A1 leg on the runtime tuple and does not claim a new A1 measurement on different AI code.


## Second Astra confirmation — 2026-11-27

On a second fresh request, Astra rechecked the same clean `a966ec3` candidate and found no new, narrowly justified change likely to reach the 80% target. The current M10 bundle has A2–A5 and A7 passing; A1 remains 74% over 80 matches and below threshold. Earlier GOV-02 variants scored 58/80 against the 59/80 detailed baseline, and broader changes risk A3, which is only two percentage points above its floor. Astra therefore reconfirmed the official **PASS BY OWNER EXCEPTION** disposition. Preserve the measured failure and unchanged target; do not infer an exact current win count from the rounded 74% output.
