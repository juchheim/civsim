# WP-E5-8 first slice: WAY-08 sea-loss reduction (2026-11-14)

**Status:** Implemented and covered by a passing focused Release test; draft content remains draft. WAY-08 halves the existing deterministic open-sea voyage-loss probability after the civilization reaches the Fief era and completes the project. It does not change whether a route can be sailed, the route chosen, the travel time, or land travel.

## Scope

`PoliticsSystem.Voyage` applies the same risk calculation to trip forecasts, outbound expeditions, and settling voyages. WAY-08 reduces the calculated permille risk by integer division by two, before the existing Great Lighthouse modifier. This retains the established deterministic voyage stream and makes the reported forecast agree with the risk used by the actual loss rolls. The Fief guard keeps Iron and Bronze chapter behavior unchanged.

The focused test uses a pack where WAY-08 alone is made numeric for the fixture; it does not promote or edit the E5 content rows. It checks the baseline formula, exact halving, unchanged reachability and sea-tile route, repeatable outcomes for the same route and season, and that halving cannot add a loss for the same deterministic roll. Walking travel remains risk-free.

## Acceptance limits and next work

This is a mechanic slice, not acceptance of WP-E5-8. The test exercises deterministic seeded loss rolls but does not estimate a population-level loss rate across natural Fief matches. Existing open-sea crossing rules remain unchanged; E5 still does not open ocean crossings. The larger war scope remains open: E5 weapons, powder and cannon need goods slots, production and consumption accounting, and siege integration before their combat effects can be implemented and balanced. E4 sign-off and full Fief balance are also outstanding.

Validation already completed in the shared Release run:

```bash
dotnet build CivSim.slnx -c Release
dotnet test CivSim.slnx -c Release --filter FullyQualifiedName~FiefVoyageTests
dotnet test CivSim.slnx -c Release
dotnet run --project src/CivSim.Cli -c Release -- validate --strict
```
