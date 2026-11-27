# WP-E5-11 A1 final disposition — 2026-11-27

## Gate result

**A1: PASS BY OWNER EXCEPTION** for frozen E5 candidate `8774ebdfa7dfe078fbcf366390bd5d830db6769`.

The measured result remains **59/80 mirrored matches (73.75%)** on candidate `c3aa4e063244748ffb1f951050ded4003ce8262a`, below the unchanged nominal requirement of 64/80 (80%). The original raw failure is preserved. This disposition carries forward that near miss by the owner's explicit instruction; it does not describe 59/80 as meeting the statistical threshold, and it does not waive any other gate.

## Astra re-review

Astra reviewed the exact frozen candidate on 2026-11-27 and found no defensible additional A1 tuning change. The only simulation-source changes since the prior review preserve Fief research caches across snapshots and include those caches in checksums. AI planning, tuning, content, and scoring are unchanged. A1's tournament constructs fresh matches and advances them without snapshot restoration, so those changes do not alter its decision path. No fresh A1 cohort is claimed for this candidate.

## Frozen candidate identity

- Candidate HEAD: `8774ebdfa7dfe078fbcf366390bd5d830db6769`
- Git tree: `b76ab434580f7ebbf77d0799c92cce5b255e3f49`
- Source aggregate at freeze: `840c1bf2d8824815b9f1ef11b9768c2f28b39812d9fb7c0e3b4ba49008bd914a`
- Numeric pack SHA-256: `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`

This disposition is kept on a separate evidence branch so adding the acceptance record does not change the frozen candidate's identity.
