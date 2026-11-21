# M13 WP-E6-6 slice 1: SEA-12 ocean reach and WAY-10 the chronometer (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted. First slice of WP-E6-6 ([38 §5.2](../38-discovery-age.md)): the voyage machinery changes, nothing else. Everything is inert outside a Discovery match (`HasOceanReach` and the WAY-10 cut both test `DiscoveryChain`); Bronze, Iron and Fief bytes, hashes and snapshots are unchanged, and nothing new is stored (the ocean sea graph is a cache like the outrigger one). All numbers are provisional.

## What the slice does

| Part | How |
|---|---|
| **Ocean reach (SEA-12)** | A civilization with SEA-12 sails on a second sea graph whose water tiles are open while they are at most `OceanSeaReach = 24` tiles from land (outriggers: `OpenSeaReach = 11`). `Travel`, `SeaTilesOnRoute`, the blockade's galley reach and contact by sea all read it through `SailFrom(civ, tile)`. No second travel system. The value is the typed 24, above the audit's floor of 20. |
| **Deep water costs more** | A route tile farther from land than 11 is a *deep-ocean* tile, which only SEA-12 ships can cross. Each adds `OceanDeepRiskPerTile = 12` permille to the shipwreck roll on top of the base 30 and 8 per open-sea tile (still capped at 400 before reductions). This is the doc's "higher risk roll". |
| **WAY-10 chronometer** | In Discovery, a civilization with WAY-10 keeps three tenths of the shipwreck risk (`ChronometerRiskTenths = 3`), after WAY-08's halving: a deep crossing at 400 becomes 200 with WAY-08 and 60 with WAY-10. |

`VoyageRiskPermille`, `TurnedBackAtSea` (expeditions) and `VoyageLost` (daughter bands) all route through `VoyageRiskForSeaTiles(civ, sea, deep)`, so the three agree.

## What the numbers showed (the finding worth knowing)

Over six worlds (seeds 20261019 to 20261024, civ 0's home) SEA-12 changes the least-cost route to **about a quarter of land tiles in half the worlds and to none in the other half**, and saves **at most one season** (of a longest trip of 8 to 11 seasons). In the unchanged worlds the islands already lie along the straight line, so the stepping-stone route is as cheap as the direct one (deep tiles appear on the routes there, but the length does not move). This matches the audit ([m13-wp3-e6-geography.md](m13-wp3-e6-geography.md)): contact was already open to every start in two legs; **SEA-12 does not decide whether the oceans open, only how safely and how directly they are crossed.** Its real payoff therefore has to come from what rides on the ocean: colonies (TER-11), the Columbian Exchange (FOR-10), galleons (SEA-13) and the loss cuts (WAY-10, later TRD-11). Do not tune SEA-12's reach to make it matter; tune what uses it.

## Design choices worth knowing

1. **The reach is one more cached graph, not a new rule.** `_oceanFrom` is built lazily per tile like `_sailFrom`; nothing is stored, so there is no snapshot change.
2. **No speed change.** The doc says both "longer voyage time" and "ocean routes become the shortest"; the sim has no ship-speed field, and the draft rows give none. The shorter direct route is the only speed effect.
3. **WAY-10 is carried by the project alone for now.** The row says the benefit needs a chronometer good and a navigator aboard. Both belong with MEC-09's instruments and the navigator role (WP-E6-7); until then researching WAY-10 is the whole condition. Flagged so it is not forgotten when the instrument good lands.
4. **Seed for the tests is 20261020**, a world where SEA-12 does change routes (seed 20261019's does not, see above).

## Tests (4 in `DiscoveryOceanTests`, all pass)

SEA-12 opens deep water only in Discovery, only for the civilization that learned it, never lengthens a route and shortens some; the loss roll adds the deep-tile term and WAY-10 takes three tenths after WAY-08; walking routes and other civilizations are untouched; SEA-12 and WAY-10 do nothing in Fief (the ceiling refuses them and the gate checks the chapter).

## Not done in WP-E6-6 yet

1. **SEA-13 galleons** (armed trade and war ships on the naval rows; a fleet at sea is out of the defense pool).
2. **TER-11 overseas colonies** (distance-based Overreach strain, a seeded independence event, blockade exposure).
3. **FOR-10 Columbian Exchange**, **FOD-10 sugar**, **HNT-12 deep-sea fishing**, **DIP-09 resident embassies**.
4. **TRD-10 and TRD-11** (joint-stock voyage pooling and insurance): wait for decision D12, the credit model.
