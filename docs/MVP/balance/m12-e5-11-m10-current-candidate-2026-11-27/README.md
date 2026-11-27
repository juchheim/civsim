# WP-E5-9 current-candidate AI acceptance — 2026-11-27

## Candidate and runtime identity

The closeout candidate is `a966ec377a9d650b2b90b4467827c777e115c603`, tree `aa860512e8f97f6e1c35ebe5548e32f5159e73ef`, source aggregate `e0b43203a6622829816f85b924e986995bffd474a82a78ce819272025a117048`, and numeric pack SHA-256 `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`.

The M10 run started at `8774ebdfa7dfe078fbcf366390bd5d830db6769a`. Its only source delta to the closeout candidate was the A8 test fixture `src/CivSim.Tests/FiefWholeMatchFogFairnessTests.cs`; the Harness and Sim runtime code were unchanged. Its pinned Harness SHA-256 is `0224459af3d84c3c61773b4324798d246b26e75b445adc41c8b0297a074dd950` and Sim SHA-256 is `51562e50ab950f85e500d995b475cbe5a86b8f99ac688260a283fb9989d349d7`. The complete before/after file list is in [run metadata](run_metadata.json).

## M10 results

| Gate | Result |
| --- | ---: |
| A1 Warlord vs Council | 74% / 80 matches — measured **FAIL** against 80%; **PASS BY OWNER EXCEPTION** only |
| A2 vs Grower / Builder / Wanderer | 76% / 82% / 80% — pass |
| A3 survival / wins vs Rusher | 100% / 72% — pass |
| A4 Chieftain vs Warlord | 39% — pass |
| A5 FFA | all five archetypes 22.5–26.9% — pass |
| A7 blunder detectors | all six zero — pass |
| B27 tin-poor fallback | 143/154; dormant bronze 5/80 — pass |
| B28 defenses before believed-bronze raids | 14/14 opportunities — pass |
| B29 forecast fidelity | 159/159 within 15%; median error 3.0%; no missing forecasts — pass |
| B30 mandate and blunders | overflow 18/80; no suicide raids or siege desertions; naked caravans 10/80 — pass |
| B31 Legend vs Warlord | 50% / 80 matches — pass |

The raw `ai --check-m10` process exits nonzero because A1's measured result is below its unchanged nominal threshold. Preserve that raw failure. The user's authorized exception closes A1 only; it does not claim that 74% met 80%. The integrated command also labels A6 and A8 as separate acceptance gates; both have separate exact-candidate passing evidence linked below.

- [A1 exception and Astra re-reviews](../m12-e5-11-a1-final-disposition-2026-11-27.md)
- [A6 exploit search](../m12-e5-9-a6-current-candidate-2026-11-27.md) — 10,009 policies screened; 16 finalists validated; maximum validated Warlord win rate 16.67%, under the 55% ceiling.
- [A8 whole-match fog fairness](../m12-e5-9-a8-current-candidate-2026-11-27/README.md) — 2,304 checkpoints; zero strict/composite differences, unapplied controls, or validation failures.

The preserved M10 raw output is [here](raw-output.txt), SHA-256 `3dba9c9d0f881d8b226c5147d2afc7d9878315cedd0c18ad8bf030fb4e3b3c52` (4,150 bytes); the seed manifest is [here](seeds.txt). This closes the measured current-source AI checks subject to the explicit A1 owner exception. It does not close F-T balance, determinism, performance, or other WP-E5-11 gates.
