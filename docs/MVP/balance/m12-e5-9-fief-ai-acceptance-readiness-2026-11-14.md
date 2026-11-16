# WP-E5-9 Fief AI acceptance readiness (2026-11-14)

This report records the existing A1–A5/A7 chapter-horizon acceptance cohorts in Fief mode plus separate A6/A8 readiness. Thresholds remain those in [17 §4.3](../17-vertical-slice.md#43-ai-e0e2-horizon), with the later 14 §18 notes governing corrected sample sizes and the A1 threshold. The original run was blocked by Fief caravan-ledger overflow; the owner fixed large-margin saturation and a second negative Free Folk seller sentinel conversion, after which the rebuilt harness completed the A1 cohort and the combined A1–A5/A7 sequence.

## Scratch pack and command

The scratch source tree was copied to `/tmp/civsim-e5-ai-acceptance-content`. In that copy only, every era-5 project block whose status was `draft` was changed to `numeric`; all other content was copied unchanged. `civsim build-pack` validated and compiled it to `/tmp/civsim-e5-ai-acceptance.pack` (394,524 bytes; hash `e1ce35fd2af715db9cbf0a66c81372a95bf05ef19296248454609fc77f7bc3f2`). No repository catalog row or golden was changed for this attempt.

Command:

```sh
dotnet run --project src/CivSim.Harness -c Release --no-build -- ai \
  --pack /tmp/civsim-e5-ai-acceptance.pack --mode fief \
  --check-m10 --seeds 40 --first-seed 20261019 --minutes 64
```

The intended first 40 seeds are 20261019–20261058 (`Tournament.BaseSeed` is 20261019), with two mirrored matches per seed for duel gates. The harness uses 40 seeds for A1, A2 scripts, A3, and A4; A5 uses three rotations per requested seed, or 120 world seeds / 600 FFA matches here. The initial `--check-m10` attempt and first retry crashed in `RecordFiefTradeProfit`; both captures are preserved in [m12-e5-9-fief-a1-post-saturation-crash-2026-11-14.txt](m12-e5-9-fief-a1-post-saturation-crash-2026-11-14.txt). After the TRD owner fixed saturating ledger arithmetic and guarded the negative Free Folk seller sentinel, I rebuilt the harness (`dotnet build src/CivSim.Harness -c Release --no-restore`, 0 warnings/errors). The direct A1 run used `--duel warlord council --seeds 40 --first-seed 20261019 --minutes 64 --match-details`; the combined A1–A5/A7 command used the same pack and frozen seed span. The pre-fix crash is cleared; the gate outcomes are below.

## Gate evidence

| Gate | Required Fief evidence | Result in this slice |
|---|---|---|
| A1 | Warlord vs Council, ≥80% on 40 seeds / 80 mirrored matches (14 §18 note; it supersedes the older 85% text) | **Fail: 61/80 = 76%** Warlord wins; 19 Council wins. Seed-by-seed outcomes and detailed diagnostics are in [the A1 cohort log](m12-e5-9-fief-a1-cohort-2026-11-14.txt). |
| A2 | Warlord vs Grower, Builder, Wanderer; ≥70%; 40 seeds / 80 matches per script here | **Pass:** Grower 60/80 = 75%; Builder 66/80 = 82%; Wanderer 60/80 = 75%. No Fief owner exception was applied. |
| A3 | vs Rusher: no settlement loss in ≥80%; win ≥70%; 40-seed/80-match sample (14 §18 note) | **Pass at threshold:** 80/80 matches without settlement loss; Warlord wins 56/80 = 70%. |
| A4 | Chieftain vs Warlord, 25–40%; 40-seed/80-match sample (14 §18 note) | **Pass:** Chieftain wins 27/80 = 34%. |
| A5 | No equal-difficulty archetype wins >30% of FFA | **Pass:** Builder 141/480 = 29.4%; Grower 113/480 = 23.5%; Wanderer 104/480 = 21.7%; Warlord 121/480 = 25.2%; Sage 121/480 = 25.2%, from 120 seeds / 600 matches. |
| A6 | Search 10,000 distinct fixed policies; validate 16 diverse finalists on fresh mirrored 64-minute seeds; none >55% (14 §10.3) | **Harness ready; acceptance search not run.** Fief chapter/seed plumbing and focused Release tests passed 17/17; the 10,009-policy search is pending final WP9 source/binary. See [A6 harness report](m12-e5-9-fief-a6-harness-plumbing-2026-11-14.md). |
| A7 | 14 §10.4 per-match blunder limits | **Pass on this cohort:** zero keystone stalls, dithering matches, avoidable starvation deaths, undefended captures, attributed herd crashes, and idle-labor seasons in 80 Warlord/Council mirrored matches. |
| A8 | Decisions invariant under unseen-state randomization, with visible-state positive controls | **Fixture implemented; focused Release tests passed 7/7.** The Fief fixture now perturbs unseen deposits, rival powder/cannon, harbor/galley/crew, and horse state, with visible-state controls. Whole-match Fief A8 acceptance remains open. See [A8 fixture report](m12-e5-9-fief-a8-fixture-coverage-2026-11-14.md). |

The combined command continued after A7 into B29. It was intentionally stopped at that boundary because B29 is outside the Fief E5 gates. The full available A1–A7 output, including all raw A5 cohort counts and the explicit A7 checks, is in [m12-e5-9-fief-a1-a7-cohort-output-2026-11-14.txt](m12-e5-9-fief-a1-a7-cohort-output-2026-11-14.txt); its final B29 line is only the stage banner. No B29 result is claimed.

The A1 loss review finds no single deterministic combat or defense failure. Across its 19 Council wins, mean Warlord/Council values were Legacy 5,188/5,671, Dominion 3,009/3,391, Mastery 1,411/1,465, Firsts 768/815, settlements 16.7/18.6, claimed land 16.0%/16.2%, and KEY-1/KEY-2 8.12/23.77 vs 8.32/24.15 minutes. Lost/captured settlements were equal at about 0.21 each. Warlord launched 4.0 raids on average vs Council's 0.16 and declared 0.32 wars per loss vs 0 for Council, with 0.89 rejected wars; this points to a broad Legacy/settlement race outcome rather than a repeated siege or defense defect. This diagnostic does not support a narrow safe policy correction by itself, so no AI code was changed and the A1 threshold remains unchanged.

## Remaining acceptance work

1. Resolve the A1 shortfall (61/80, 76% against the unchanged ≥80% threshold) and rerun the documented cohort before claiming A1 acceptance.
2. After WP9 tuning/source changes settle, rebuild the final Release harness and run the Fief A6 10,009-policy search plus 16 finalist validations under the documented protocol; the harness plumbing is complete, but the acceptance search remains unmeasured.
3. Run broader whole-match Fief A8 acceptance using the implemented fog fixture; its 7/7 focused tests establish fixture coverage, not the complete acceptance gate.

Focused Fief fog/horses Release tests passed 7/7; see the [A8 fixture report](m12-e5-9-fief-a8-fixture-coverage-2026-11-14.md) for the command and coverage. A6 focused harness tests passed 17/17; see the [A6 harness report](m12-e5-9-fief-a6-harness-plumbing-2026-11-14.md). No A6 search or broad whole-match A8 acceptance result is claimed here. The measured status remains: A1 fails; A2–A5/A7 pass on their documented cohorts; A6 and full A8 remain open.
