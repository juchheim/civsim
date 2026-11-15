# WP-E5-9 first slice: Fief KEY-6 research routing (2026-11-14)

**Status:** Focused test coverage added for the existing generic Spark, Council, and forecast keystone paths in Fief. No simulator source changes were needed. E5 content remains draft; the test pack promotes the E5 rows in memory only.

The test completes KEY-5 and every E5 project except KEY-6 for both a Warlord Spark and the human Council seat, so KEY-6's direct prerequisites and era-5 breadth requirement are ready. It disables the Spark seat's Council fallback to make its event-driven `keystone ready` routing observable, then verifies KEY-6 reaches Primary. For the Council seat, it verifies KEY-6 is Available and receives a nonzero score from the Council prior. This checks eligibility and scoring, not that the Council always puts KEY-6 ahead of its other available work. A one-season forecast assertion checks that the Fief forecast either reports completion or still treats KEY-6 as available.

## What this does not establish

The fixture-level check does not establish whole-match strategic quality. The separate two-seed natural screen below observed 8/8 closures under the 25-minute target in this small sample, but remains exploratory and does not establish robust pacing. The report does not cover the full A1–A8 Fief AI acceptance gates, larger held-out cohorts, multiplayer performance, or game/view integration. Strategist powder and cannon logic remains dependent on WP-E5-8's missing weapon and powder goods, production accounting, and siege support. The WP-E5-9 package is therefore still open, and no draft content is promoted.

## Small natural-closure measure

`iron-match --fief` runs the four archetype seats with the normal research and economy AI in a bounded whole-match simulation. Its Fief readout now reports, for each seat, elapsed simulated minutes from that seat's own KEY-5 to its own KEY-6. A seat that reaches KEY-5 but not KEY-6 is reported as right-censored at the `--minutes` horizon; a seat that never reaches KEY-5 is reported separately. This gives a direct observation against D8's approximately 25-minute KEY-5-to-KEY-6 target and exposes whether any archetype stalls before closure, without manufacturing ready KEY-6 states as the existing fixture does.

The first useful screen is two ordinary seeds with all four archetypes and a 130-minute horizon, then a larger held-out cohort only if that screen shows consistent closure. Summarize the completed-seat share and the median / range of uncensored KEY-5-to-KEY-6 times by archetype; include censored seats in the completion share rather than dropping them. This is a pacing diagnostic, not an acceptance gate by itself. In Fief, AI KEY-6 does not end a headless match: only the human's KEY-6 closes Fief, per D11. The harness must stop at its explicit horizon and must not use a rival countdown.

### Exploratory natural screen (2026-11-14)

The initial screen used seeds `20261019`–`20261020`; the larger held-out cohort used the non-overlapping fixed consecutive range `20262001`–`20262008`. Both used the existing Release harness, all four archetypes, Fief, a 130-minute horizon, and no `--grant-e5`. A temporary content copy promoted exactly the 50 era-5 rows for pack construction; repository content rows remain draft. Pack SHA-256: `4b97732080aa84cfe795cf03b86ff8f20e02f6d2dc569d8f703a2b13860ff5da`.

Across the held-out block, **29/32 seats (90.6%)** completed KEY-6 by the horizon; **2** had reached KEY-5 and were right-censored, and **1** Warlord had not reached KEY-5. All eight matches ran to the explicit 130-minute horizon with no chapter end. Among uncensored seats, elapsed KEY-5→KEY-6 minutes were: Builder median **17.55**, range **16.1–19.7** (8/8); Grower median **20.1**, range **18.1–25.9** (8/8); Warlord median **17.9**, range **15.7–18.8** (5/8); Sage median **19.7**, range **18.2–21.5** (8/8). Absolute KEY-6 completion minutes by archetype (median; range) were Builder **107.9** (103.7–117.8), Grower **112.7** (107.6–120.6), Warlord **114.9** (111.2–120.4), and Sage **111.4** (109.7–113.5). Absolute times are for completed seats only.

For context, the initial `20261019`–`20261020` screen completed **8/8 seats**. Its elapsed KEY-5→KEY-6 results were Builder median **19.35** (18.3–20.4), Grower **19.25** (18.7–19.8), Warlord **18.05** (17.7–18.4), and Sage **19.7** (19.6–19.8); absolute KEY-6 times spanned 108.0–115.6 minutes.

This **n=8, 32-seat cohort is exploratory**, not an acceptance result. It includes a Warlord miss to reach KEY-5 and two right-censored Warlord KEY-6 outcomes, so it does not establish robust pacing or strategic quality.

No AI rule change is supported by the fixture-level evidence or these exploratory natural screens. The Council prior gives ready KEY-6 positive value, Spark explicitly routes a ready keystone, and the forecast recognizes Fief's KEY-6. The observed misses do not isolate a routing defect. No new E5 content is promoted by this measure.

Validation completed 2026-11-14: the focused routing group and the full Release suite passed, with 1,234 tests; strict content validation also passes. The Council assertion is intentionally limited to availability and prior score, not a guarantee that the Council chooses KEY-6 ahead of all other available work. Both natural screens are exploratory and do not establish robust natural whole-match AI pacing or strategic choices.

Reproduce with:

```bash
LC_ALL=C LANG=C dotnet run --project src/CivSim.Harness -c Release -- iron-match --pack <temporary-promoted-e5.pack> --fief --seed 20262001 --seeds 8 --minutes 130
dotnet build CivSim.slnx -c Release
dotnet test CivSim.slnx -c Release --filter FullyQualifiedName~FiefSparkKey6Tests
dotnet test CivSim.slnx -c Release
dotnet run --project src/CivSim.Cli -c Release -- validate --strict
```
