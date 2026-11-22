# M13 WP-E6-13 slice 1: held-out pacing check (2026-11-21)

**Status:** measurement only, no code or content changed. Checks the era cost fitted in [m13-wp12-pacing-slice3.md](m13-wp12-pacing-slice3.md) (Discovery base 12,000, eight late projects doubled) on worlds it was **not** fitted on.

## Setup

`discovery-match --pack civsim.pack --seed 20261101 --seeds 6 --minutes 190 --grant-e5`: six new worlds (seeds 20261101 to 20261106), four civilizations each, the stock AI, E5 granted when a civilization finishes KEY-5 (E5 is still draft). The measure is seasons from a civilization's own KEY-6 to its own KEY-7, four seasons to a minute. The fit used 12 civilizations in three worlds (mean 24.6 minutes, range 20.0 to 27.8).

## Result

| Seed | Builder | Grower | Warlord | Sage |
|---|---|---|---|---|
| 20261101 | 21.5 | 22.8 | 28.8 | 29.8 |
| 20261102 | 20.8 | 23.8 | 25.3 | 25.0 |
| 20261103 | 20.8 | did not reach KEY-5 | 25.8 | 30.0 |
| 20261104 | 19.8 | 30.8 | 31.8 | 27.8 |
| 20261105 | 20.8 | 29.0 | 26.0 | did not reach KEY-5 |
| 20261106 | 21.8 | 28.0 | 24.3 | did not reach KEY-5 |

**21 civilizations reached KEY-7: mean 25.4 minutes, range 19.8 to 31.8.** The target is 25 (D8) and the fit predicted 24.6. The held-out mean is 0.8 minutes (3%) above the fitted one, inside the spread between archetypes (the Builder is consistently the fastest at about 21, the Warlord and Sage the slowest at 25 to 32). **No retune is called for.**

## What is not E6

Three civilizations (a Grower and two Sages) **never reached KEY-5** in 190 minutes (they stopped at era 4 or era 2, one with zero Insight a year): a collapse in the earlier ages that has nothing to do with Discovery. It is E3 to E5 balance and belongs with the E5 owners. These three were left out of the pacing mean.

## Not done in WP-E6-13

Everything that needs the whole age to be accepted: the 80-match sample with a held-out cohort, the replay and snapshot acceptance (see [the resume defect](m13-wp12-resume-slice4.md)), the E5-promoted re-run, and the human playtest.

## Addendum (2026-11-22): re-run after the Palace, greenhouse, instruments and embassy slices

Same command (`discovery-match --pack civsim.pack --seed 20261101 --seeds 6 --minutes 190 --grant-e5`) on commit `e71c8be`. **21 civilizations reached KEY-7: mean 25.4 minutes, range 19.8 to 32.0** (was 25.4 and 19.8 to 31.8); the same three civilizations never reached KEY-5. The instruments, Palace, greenhouse and DIP-09 embassy rule do not move pacing: the E6 fit stands. The caveat above still holds: with real E5 numbers (no grants) the stock AI reaches KEY-7 much sooner after KEY-6, so the era cost needs a re-fit once E5 is promoted or playtests report.
