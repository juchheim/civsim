# WP-E5-11 A1 final disposition — 2026-11-27

## Gate result

**A1: PASS BY OWNER EXCEPTION** for frozen E5 candidate `8774ebdfa7dfe078fbcf366390bd5d830db6769`.

The unchanged nominal requirement is **64/80 (80%)**. The earlier detailed baseline measured **59/80 mirrored matches (73.75%)** on `c3aa4e063244748ffb1f951050ded4003ce8262a`, below that target. A fresh A1 leg on this frozen candidate now reports **74% over 80 matches — FAIL**; the test output rounds to whole percentages and allows half-wins for ties, so this line alone does not establish an exact integer win count. Its preserved 165-byte raw A1 output is [here](m12-e5-11-a1-current-leg-2026-11-27.raw.txt), SHA-256 `1b322d5f8020692a469247a96031bf7a93cc1923aba80b43b5ea5b15e475e8ca`. The nominal threshold and measured failures remain unchanged; this disposition changes release acceptance only.

## Astra re-review

Astra reviewed the exact frozen candidate again on 2026-11-27 and found no defensible additional A1 tuning change. The fresh current-candidate A1 leg reported 74% over 80 matches. The current raw-output file is still shared with the running M10 campaign, so its full-file hash is provisional; the A1 prefix above is separately preserved and hashed. The seed-manifest SHA-256 is `3e246a2f3293a76aa8fc2c9f01f9b818c2e0fe7459121aa24e2506f370ce055c`. Earlier detailed evidence shows predominantly early growth/Dominion deficits; GOV-02 adjustments returned 58/80 versus 59/80 at baseline. The current code already corrects defense-queue coupling that could suppress calm-frontier GOV-02 value. No scoped change was supported by the fresh review, so A1 is officially accepted by the owner's near-miss exception. The prior 59/80 exact count remains attributed to its own candidate and detailed result.

## Frozen candidate identity

- Candidate HEAD: `8774ebdfa7dfe078fbcf366390bd5d830db6769`
- Git tree: `b76ab434580f7ebbf77d0799c92cce5b255e3f49`
- Source aggregate at freeze: `840c1bf2d8824815b9f1ef11b9768c2f28b39812d9fb7c0e3b4ba49008bd914a`
- Numeric pack SHA-256: `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`

This disposition is kept on a separate evidence branch so adding the acceptance record does not change the frozen candidate's identity.
