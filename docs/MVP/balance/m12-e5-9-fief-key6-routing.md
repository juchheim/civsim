# WP-E5-9 first slice: Fief KEY-6 research routing (2026-11-14)

**Status:** Focused test coverage added for the existing generic Spark, Council, and forecast keystone paths in Fief. No simulator source changes were needed. E5 content remains draft; the test pack promotes the E5 rows in memory only.

The test completes KEY-5 and every E5 project except KEY-6 for both a Warlord Spark and the human Council seat, so KEY-6's direct prerequisites and era-5 breadth requirement are ready. It disables the Spark seat's Council fallback to make its event-driven `keystone ready` routing observable, then verifies KEY-6 reaches Primary. For the Council seat, it verifies KEY-6 is Available and receives a nonzero score from the Council prior. This checks eligibility and scoring, not that the Council always puts KEY-6 ahead of its other available work. A one-season forecast assertion checks that the Fief forecast either reports completion or still treats KEY-6 as available.

## What this does not establish

This is a fixture-level routing check, not evidence that AI seats naturally reach KEY-6, meet the 25-minute target, or make good strategic choices across a whole match. It does not cover the full A1–A8 Fief AI acceptance gates, live held-out cohorts, multiplayer performance, or game/view integration. Strategist powder and cannon logic remains dependent on WP-E5-8's missing weapon and powder goods, production accounting, and siege support. The WP-E5-9 package is therefore still open, and no draft content is promoted.

Validation completed 2026-11-14: the focused routing group passed and the full Release suite passed 1,222 tests; strict content validation also passes. The Council assertion is intentionally limited to availability and prior score, not a guarantee that the Council chooses KEY-6 ahead of all other available work. The fixture still does not establish natural whole-match AI pacing or choices.

Reproduce with:

```bash
dotnet build CivSim.slnx -c Release
dotnet test CivSim.slnx -c Release --filter FullyQualifiedName~FiefSparkKey6Tests
dotnet test CivSim.slnx -c Release
dotnet run --project src/CivSim.Cli -c Release -- validate --strict
```
