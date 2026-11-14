# WP-E5-9 first slice: Fief KEY-6 research routing (2026-11-14)

**Status:** Focused test coverage added for the existing generic Spark, Council, and forecast keystone paths in Fief. No simulator source changes were needed. E5 content remains draft; the test pack promotes the E5 rows in memory only.

The test completes KEY-5 and every E5 project except KEY-6 for both a Warlord Spark and the human Council seat, so KEY-6's direct prerequisites and era-5 breadth requirement are ready. It disables the Spark seat's Council fallback to make its event-driven `keystone ready` routing observable, then verifies KEY-6 reaches Primary. For the Council seat, it verifies KEY-6 is Available and receives a nonzero score from the Council prior. This checks eligibility and scoring, not that the Council always puts KEY-6 ahead of its other available work. A one-season forecast assertion checks that the Fief forecast either reports completion or still treats KEY-6 as available.

## What this does not establish

The fixture-level check does not establish whole-match strategic quality. The separate two-seed natural screen below observed 8/8 closures under the 25-minute target in this small sample, but remains exploratory and does not establish robust pacing. The report does not cover the full A1–A8 Fief AI acceptance gates, larger held-out cohorts, multiplayer performance, or game/view integration. Strategist powder and cannon logic remains dependent on WP-E5-8's missing weapon and powder goods, production accounting, and siege support. The WP-E5-9 package is therefore still open, and no draft content is promoted.

## Small natural-closure measure

`iron-match --fief` runs the four archetype seats with the normal research and economy AI in a bounded whole-match simulation. Its Fief readout now reports, for each seat, elapsed simulated minutes from that seat's own KEY-5 to its own KEY-6. A seat that reaches KEY-5 but not KEY-6 is reported as right-censored at the `--minutes` horizon; a seat that never reaches KEY-5 is reported separately. This gives a direct observation against D8's approximately 25-minute KEY-5-to-KEY-6 target and exposes whether any archetype stalls before closure, without manufacturing ready KEY-6 states as the existing fixture does.

The first useful screen is two ordinary seeds with all four archetypes and a 130-minute horizon, then a larger held-out cohort only if that screen shows consistent closure. Summarize the completed-seat share and the median / range of uncensored KEY-5-to-KEY-6 times by archetype; include censored seats in the completion share rather than dropping them. This is a pacing diagnostic, not an acceptance gate by itself. In Fief, AI KEY-6 does not end a headless match: only the human's KEY-6 closes Fief, per D11. The harness must stop at its explicit horizon and must not use a rival countdown.

### Exploratory natural screen (2026-11-14)

Ran `iron-match --pack <temporary-promoted-e5.pack> --fief --seeds 2 --minutes 130` with no `--grant-e5`. The temporary content copy promoted exactly the 50 era-5 rows; repository content rows remain draft. Pack SHA-256: `4b97732080aa84cfe795cf03b86ff8f20e02f6d2dc569d8f703a2b13860ff5da`. Seeds were `20261019` and `20261020`. The existing Release harness reported per-civilization KEY-5→KEY-6 timing and censor status.

All **8/8 seats (100%)** completed KEY-6 before the 130-minute horizon; **0** seats were censored after KEY-5 and **0** failed to reach KEY-5. The uncensored elapsed minutes by archetype were: Builder 20.4, 18.3 (median **19.35**, range 18.3–20.4); Grower 18.7, 19.8 (median **19.25**, range 18.7–19.8); Warlord 18.4, 17.7 (median **18.05**, range 17.7–18.4); Sage 19.6, 19.8 (median **19.7**, range 19.6–19.8). Every seat reached KEY-6 between 108.0 and 115.6 simulated minutes. Both matches ran to the explicit 130-minute horizon and had no chapter end.

This **n=2 screen is exploratory**, not an acceptance result; it supports proceeding to a larger held-out cohort but does not establish robust pacing or strategic quality.

No AI rule change is supported by the fixture-level evidence or this exploratory natural screen. The Council prior gives ready KEY-6 positive value, Spark explicitly routes a ready keystone, and the forecast recognizes Fief's KEY-6. The two-seed screen observed timely natural closure without a demonstrated routing defect; the larger held-out cohort is still needed to establish robust pacing. No new E5 content is promoted by this measure.

Validation completed 2026-11-14: the focused routing group and the full Release suite passed, with 1,234 tests; strict content validation also passes. The Council assertion is intentionally limited to availability and prior score, not a guarantee that the Council chooses KEY-6 ahead of all other available work. The two-seed screen is exploratory and does not establish robust natural whole-match AI pacing or strategic choices.

Reproduce with:

```bash
dotnet run --project src/CivSim.Harness -c Release -- iron-match --pack <temporary-promoted-e5.pack> --fief --seeds 2 --minutes 130
dotnet build CivSim.slnx -c Release
dotnet test CivSim.slnx -c Release --filter FullyQualifiedName~FiefSparkKey6Tests
dotnet test CivSim.slnx -c Release
dotnet run --project src/CivSim.Cli -c Release -- validate --strict
```
