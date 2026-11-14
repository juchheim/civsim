# WP-E5-8 first slices: WAY-08 and DEF-09 (2026-11-14)

**Status:** WAY-08 and a narrowly scoped DEF-09 concentric-castle defense slice are implemented and covered by the passing integrated Release suite. E5 rows remain draft. E4 sign-off was confirmed by the user on 2026-11-14.

## Scope

`PoliticsSystem.Voyage` applies the same risk calculation to trip forecasts, outbound expeditions, and settling voyages. WAY-08 reduces the calculated permille risk by integer division by two, before the existing Great Lighthouse modifier. This retains the established deterministic voyage stream and makes the reported forecast agree with the risk used by the actual loss rolls. The Fief guard keeps Iron and Bronze chapter behavior unchanged.

The WAY-08 focused test uses a pack where WAY-08 alone is made numeric for the fixture; it does not promote or edit the E5 content rows. It checks the baseline formula, exact halving, unchanged reachability and sea-tile route, repeatable outcomes for the same route and season, and that halving cannot add a loss for the same deterministic roll. Walking travel remains risk-free.

DEF-09 is limited to Fief and enters the existing settlement defense multiplier only when DEF-09 is complete, that same settlement has a built concentric-castle upgrade, and its castle is garrisoned. The Fief planner can construct the upgrade for the registry's 80 work, 24 ashlar, 12 wood and 2 iron, after the project and garrisoned castle are present. It multiplies the existing provisional `CastleStrongpoint` x1.25 by a provisional x1.5, for x1.875 total. The `e5_combat_oracle.py` storm sweep reports the attacker/defender crossover at x1.9; its checked fixtures show x1.25 and x1.5 below that point while the naive x1.25 × 2 = x2.5 reading flips the post-breach storm to defender wins. This supports x1.5 as a bounded implementation candidate, not a final balance result. The change leaves the existing raid and assault resolver path and siege wall-integrity calculations intact; it does not implement DEF-09's separate engine/cannon-crew attrition rule.

## Acceptance limits and next work

These are mechanic slices, not acceptance of WP-E5-8. The WAY-08 test exercises deterministic seeded loss rolls but does not estimate a population-level loss rate across natural Fief matches. Existing open-sea crossing rules remain unchanged; E5 still does not open ocean crossings. Remaining WP-E5-8 work includes the E5 weapon effects, powder goods and production/consumption, cannon goods and production/consumption, cannon/engine combat and siege integration, DEF-09 siege-crew attrition, and the rest of the sea scope including routes/crossings and WAY-09. The concentric-castle multiplier remains provisional pending combat/siege balance review.

Validation completed in the shared Release run:

```bash
dotnet build CivSim.slnx -c Release
dotnet test CivSim.slnx -c Release
dotnet run --project src/CivSim.Cli -c Release -- validate --strict
```
