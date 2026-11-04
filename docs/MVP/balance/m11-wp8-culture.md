# M11 WP8: culture, learning, wonders and crafts (batch 5)

Iron chapter only. (P) marks provisional numbers. Everything here is gated by `IronChain` and by the civilization knowing the named project, so the Bronze economy, its checksum and the goldens are unchanged.

## Learning (LRN-05, INQ-04, BEL-08, COM-07, INQ-05)

| Rule | Where | Behaviour |
| --- | --- | --- |
| Academy | `IronStructure.Academy` | 16 ws, 8 blocks, 2 wood. A tier 1+ town of 50+ people builds one; a civilization keeps at most three (P). |
| Scholar | `Role.Scholar`, `ScholarSlots`, `ScholarHeads` | One scholar for each academy, staffed from the town's adults like a priest; knowing LRN-05 creates no scholar. The registry's 48 Insight a year each, through the existing role loop, once `LivingEconomySource` reports the heads. |
| Scholar mods | `ResearchSystem` (`scholar_insight`) | INQ-04 (x1.2), BEL-08 (+10%) and LRN-06 (x1.2) multiply the scholars' share only, not the Commons and not the scribes (26 §2.3). |
| Alphabet | `scribe_learning_output` | COM-07 doubles the scribes' own output. (The 26 text also names Commons x1.05; the catalog row has only the scribe stat, so only that is wired.) |
| Mechanics | `EffectiveCost` | INQ-05 cuts MEC and NRG project costs to x0.9, applied to the current cost like the other discounts. Like every Method mod it takes hold as the people adopt it. |

## Wonders and leagues (LRN-06, SEA-08, BLD-11, DIP-07)

| Rule | Where | Behaviour |
| --- | --- | --- |
| The three Wonders | `IronStructure.GreatLibrary/GreatLighthouse/GreatDome` | Buildable as normal Wonders (25 Q7). Costs are provisional, sized to the E3 ziggurat's 200 ws: Library 150 ws, 60 blocks, 40 ashlar, 30 wood, 4 bloom; Lighthouse 120 ws, 40, 30, 40, 4 (needs a harbor); Dome 180 ws, 80, 60, 20, 6. A treadwheel crane halves the labor (they are Large works). |
| One of each | `WonderOwnerCiv`, `WonderWanted` | A civilization builds a Wonder in its most populous town of tier 1+ (harbor town for the Lighthouse), one at a time, and only while nobody holds it. If another civilization finishes first the build is dropped. A Wonder belongs to whoever holds its town; capture hands it over. Finishing one earns the Legacy through the existing `AwardWonderBuilt`. |
| Great Library | `EffectiveCost` | Once built, each knowing civilization's diffusion discount counts double; the 0.5 floor stays. |
| Great Lighthouse | `RollVoyageLost`, `CaravanTiles` | Once held, its civilization's shipwreck risk is halved on every voyage roll, and harbor-to-harbor caravans go a fifth faster on top of the x3. "Landmark claim uses existing Wonder rules": those give a Wonder nothing beyond Cohesion, so there is no claim to add. |
| Great Dome | none | BLD-11 gives only the unlock; the Dome is a Wonder for its Legacy. |
| Leagues | `PoliticsSystem.League.cs`, `MetalState.IronPledgeTo` | `OrderLeague(a, b)` needs DIP-07, an alliance, no war; up to four members, all mutually allied; a member who falls out of the alliance or goes to war leaves. Each season a partner pledges the Iron build labor of its most populous town to a member's Wonder in progress. That labor is delivered into the builder's progress, never multiplied, and the pledging town's own works stand still while it is lent. The builder owns the Wonder and its Legacy; a partner earns +1 Relations a season. |

## Crafts (CER-07, CER-08, WOD-07, FOR-08 with MEC-05)

| Rule | Where | Behaviour |
| --- | --- | --- |
| Blown glass | `IronGood.BlownGlass`, `IronCraftWants` | The registry recipe (a smelter, glass and charcoal for four pieces) runs when wanted: one per twenty people. Blown glass counts as luxury items for Cohesion, like the E3 luxuries. |
| Fired brick | `IronGood.FiredBrick` | Wanted, one per twenty people, in a town with a kiln. A town holding that much is roofed, and a roofed town's structure fires happen half as often (`BurnStructure`, a seeded draw keyed on the tick, settlement and what is left to burn). The +25% durability has no model (non-kit structures have no wear). **Tile** is never made: the planner takes only a recipe's first output, and `fire_brick_tile` lists brick first. |
| Press | `IronStructure.Press` | 8 ws, 4 wood, 2 bloom, wanted where grafted orchards stand. Enables `press_wine` and `press_oil` (two litres of grapes or olives for one of wine or oil). |
| Grapes and olives | `HarvestGrafted` | Each mature grafted orchard plot yields a litre a year: olives on dry ground (grassland, savanna, open woodland), grapes on forest, tropical forest or wetland, nothing elsewhere. They are goods, not food in store: they feed no one and create no calories. Wine and oil are luxury items for Cohesion. |
| Barrels | `IronGood.Barrel`, `SpoilIronLiquids` | Grapes, olives, wine and oil spoil 20%, 10%, 20%, 10% a season (registry); one barrel to ten litres covers its share of them and takes 40% off that loss. A barrel lasts 40 seasons. Shipping loss and container return are not modelled: no compact carries wine or oil. |

## State and roads (GOV-08, GOV-09, ART-08, TRN-07, COM-08)

| Rule | Where | Behaviour |
| --- | --- | --- |
| Feudal local defense | `Defenses.Feudal` | A province under a feudal contract defends x1.15 better. |
| Republic | `RepublicDelaysWar` | A civilization with GOV-08 cannot declare war inside its truce plus a quarter more (16 becomes 20 seasons), neither as an AI choice nor by order. Defense and relief are not touched; only declaring war is gated. |
| Theatre | `CohesionPressures` | A town with a theatre or arena feels 10% less of the distance and span unrest. |
| Roads and relays | `RoadAdoptionDivisor` | A road-linked town's adoption lag is divided by 2 with paved roads and by 4 with courier relays on them. |

## Findings: rows with nothing in the simulation to attach to

| Row | Why it is left |
| --- | --- |
| NRG-03 water mill | The economy has no grinding job and the quern's +30% is a project modifier on everyone, so "grind x5" has nothing to multiply; the mill's saw, bellows and trip-hammer jobs name no number. The structure is not built. **Decision for the user:** leave it dormant (recommended), or give a mill a labor saving on forge and saw recipes with an invented figure. |
| MIN-07 mine drainage | There is no flooded-deposit state and the world generator may not change. Inventing flooding would remove ore from the iron geography the WP4 audit measured. Left unmodelled. |
| SEA-07 ship capacity, INQ-05 cargo, WOD-07 shipping | A caravan's load is the smaller of surplus and shortfall; there is no ship load to multiply. |
| HUS-09 desert multiplier | The walking graph has no terrain cost. |
| STN-12 colonnade and Temple tier 2 | Cosmetic structure options: a display item for WP10. |
| WAY-07 | Display and a player lever: WP10. |
| GOV-07 census view | A planner and screen view: WP9. |
| KEY-5 | The keystone's event and score are the research system's, and the promotion step. |
