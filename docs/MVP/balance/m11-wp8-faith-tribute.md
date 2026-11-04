# M11 WP8: war, batch 4 (organized faith and tributary states)

Scope: BEL-09 and DIP-06, Iron chapter only. (P) marks provisional numbers. Leagues (DIP-07) and overseas colonies (TER-09) are still to come; leagues wait for the Wonder pipeline of the culture batch because their whole point is contributions to a Wonder under construction.

## Organized faith (BEL-09)

| Rule | Where | Behaviour |
| --- | --- | --- |
| Missionary role | `Role.Missionary`, `MissionarySlots`, `MetalTask.Preach` | One adult per Temple once the civilization knows BEL-09, drawn from the settlement's workers like a priest, preaching all season (practice `faith_spread`). |
| Faith state | `MetalState.IronFaith`, `IronFaithSeason` | Per settlement, the share of its people who follow each civilization's faith, and the season each last gained converts. Saved and hashed in the Iron section only. |
| Conversion | `ConvertTo` | One missionary-season converts 1% a year (0.25% a season) of the people who do not yet follow the faith, so each season gains a 1/400 share of the unconverted. Never in the faith's own civilization. Capped at a quarter of the settlement. |
| Where it spreads | `PoliticsSystem.FaithRoutes`, `AdvanceFaith` | Only to the foreign hub at the other end of a running civilization-to-civilization compact that has delivered at least once. A blockaded sea route or a besieged town is out of reach. Several missionaries take the reached towns in turn. No fog is lifted. |
| Soft claim pressure | `FaithClaimShare`, `BuildSettlementClaimSources` | While preaching continues (this season or the last), the faith's civilization adds a claim source around the town worth up to 5% of the town's own emission, scaling with the converted share and reaching 5% at the cap. Ownership of the town never changes; the pressure can only tip a contested border tile. |

## Tributary states (DIP-06)

| Rule | Where | Behaviour |
| --- | --- | --- |
| Terms | `OrderTribute(overlord, subject)` | Needs DIP-06, a war with the subject, no occupation or siege between them, no chain (a subject has one overlord and no subjects of its own) and a field strength at least 1.5 times the subject's. Ends the war and records the tribute. |
| Surplus | `AdvanceTribute` | Each season every subject town hands over a tenth (P) of its food surplus (stock beyond four seasons of need) to the overlord's nearest town. The food moves once. |
| Dominion points | `AccrueDominionWithTribute`, `Territory.MoveDominion` | A quarter of the subject's yearly Dominion gain moves to the overlord. The points change hands once and the land never counts for the overlord, so Dominion victory is untouched (25 Q5). |
| Ending | `AdvanceTribute` | Terms end with a new war, after the treaty term (10 years), or when the subject's field strength passes the overlord's. |

## Not done

No `CommandKind` for either yet: `OrderTribute` is a method like `OrderAssault`, and the AI evaluation of terms and the player command arrive with WP9 and WP10. The catalog's "KEY-5 prerequisite" for BEL-09 is already in the keystone requirements.

## Checks

Five tests in `IronFaithTests` (staffing, conversion maths and the cap, claim pressure, route-bound spread, blockade cut and snapshot) and four in `IronTributeTests` (rules and refusals, surplus, Dominion conservation, ending and snapshot).
