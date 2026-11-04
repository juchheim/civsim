# M11 WP8: war, batch 1 (soldiers and iron arms)

Scope: WAR-07 Professional Armies and WAR-08 Iron Weapons, in the Iron chapter only. Numbers marked (P) are provisional until the whole-match playtests.

## What was built

| Rule | Where | Behaviour |
| --- | --- | --- |
| Soldier slots (WAR-07, WAR-08) | `SoldierSlots` | A tenth of the working-age adults (14 to 59, not in a camp), a fifth once WAR-08 is known. Zero without WAR-07, outside the Iron chapter, or while food security is under 0.8. |
| Staffing | `StaffRoles`, `Role.Soldier = 5` | Soldiers are real adults taken off other work. They drill all season (`MetalTask.Drill`, practice "guard"). |
| Upkeep (P) | `PaySoldierUpkeep` | 0.25 ration per soldier per season from food in store. If the store cannot pay it, no soldier counts as a professional that season (`IronSoldiersSupplied = 0`). |
| Iron arms | `IronArmsWant`, `WearIronArms` | A forge town with WAR-08 wants one set per soldier plus one spare, steel-edge sets (G-ARMS4) once MET-10 is known, iron (G-ARMS3) before. Service life 24 and 48 seasons. |
| Fighter strength | `PoliticsSystem.Fighters` | A supplied soldier's strength is weapon x 1.5 (replaces the warrior's x1.3, never both). Weapon is steel-edge 3.0 or iron 2.4 when the town holds arms, strongest first; otherwise the existing tin, arsenical and copper tiers. Only soldiers take iron or steel arms. |

## Why these numbers

* 2.4 and 3.0 come straight from the E4 catalog text (WAR-08 and MET-10: 2.4 x 1.25 = 3.0, applied once).
* 10% and 20% are the catalog's capacity ceiling. They are a ceiling, not a free army: every soldier is an adult not farming, so the food-security gate and the upkeep are what really limit it.
* The 0.8 food gate and the 0.25 ration upkeep (P) follow the same shape as the Bronze guard: a hungry town disbands its army before it starves.

## Not done in this batch

WAR-07's garrison-claim clause, the walls and the siege engines are batch 2, below.

## Checks

Five new tests in `IronChainTests`: slots at 10% and 20% and none without WAR-07 or in Bronze; soldiers staffed and supplied; arms wear; the strength constants; the snapshot round trip. Bronze checksum `484d417e35bffe60` and the goldens are unchanged.

# M11 WP8: war, batch 2 (walls, castles, siege engines)

Scope: DEF-06 gatehouses and towers, DEF-07 frontier walls, DEF-08 castles and WAR-09 siege engines, in the Iron chapter only. (P) marks provisional numbers.

## What was built

| Rule | Where | Behaviour |
| --- | --- | --- |
| Gatehouse (DEF-06) | `IronStructure.Gatehouse`, `Defenses.Gatehouse` | 30 ws, 12 blocks, 6 wood. Wanted by a town with stone walls. A standing wall's intact multiplier goes 3.0 to 4.5. |
| Gate trade (DEF-06) | `PoliticsSystem.GateControls` | Enemy-held ground within 2 tiles (P) of a gatehoused hub no longer blocks the route; the rest of the line must still be open, and an investment still blocks the hub. |
| Frontier segment (DEF-07) | `IronStructure.FrontierWall`, `MetalState.IronFacing` | 12 ws, 4 blocks, 2 wood. Up to two per town, each facing one rival civilization that has a settlement within 6 tiles (P). Faces are saved and hashed. |
| Raids across it | `RepelAtFrontier` | A raid that would succeed against a town whose segment faces the raiders succeeds with probability 0.3; otherwise it turns back with no loot. Any other civ is unaffected. |
| Border flips behind it | `Territory.FlipGuard`, `FrontierGuarded` | A tile of the owner within 6 tiles (P) of a town whose segment faces the challenger needs four seasons of challenger lead instead of two (flip pressure x0.5). Never realm-wide. |
| Castle (DEF-08) | `IronStructure.Castle`, `CastleGarrisoned` | 60 ws, 20 ashlar, 12 wood, 2 bloom, a crane halves the labor. Wanted by a walled town (pop 60+) with a rival settlement within 8 tiles (P). It does nothing without a garrison of four supplied soldiers. Garrisoned: defenders fight x1.25 (P) and the town's claim emission is x1.2 (P; the Iron era radius is already 5). |
| Engine (WAR-09) | `IronGood.Engine`, `IronEngineWant` | A forge town with supplied soldiers wants one engine per four soldiers, at most two. Wears out in 12 seasons in store. |
| Siege train | `BeginInvestment`, `BatterWalls` | The host takes up to three engines from its home, four crew each. An engine runs only if the host is supplied and the home has 0.2 charcoal for it. Wall integrity falls 0.5 per running engine per season at wall 3.0 (a gatehouse's 4.5 slows it by a third). At zero the besiegers storm the breach with the existing assault resolver, wall removed. A win they can hold takes the town; a rout ends the siege; a stand-off keeps the breach open. Engines come home when the siege ends and are spent after 12 seasons in the field. |

## The 0.4 intention

The catalog's "siege duration x0.40" is a target, not a timer. In the tests one engine breaches a bare wall in 2 seasons and a gatehoused wall in 3; the Bronze investment is starvation: 2.5 seasons of stores last about 3.3 seasons at the 75% ration, then each further season has a 35% or 70% chance to end it, so roughly 4 to 6 seasons in total. 2 against 5 is about 0.4, but that comparison is arithmetic on the stores formula, not a measured match. This is a formula and fixture check only; I-T5 needs the oracle and live trials before anyone claims it passes.

## Not done in this batch

Frontier segments do not yet block assaults, only raids and border flips (the catalog names only those two). The AI does not yet choose targets for their walls or prefer an engine siege; that is WP9.

## Checks

Thirteen new tests: five in `IronChainTests` (gatehouse follows a wall, frontier facing, castle conditions, engine formula, train goods) and eight in `IronSiegeTests` (a host takes engines, no engines means no storm, a full engine siege to capture, towers slow it, snapshot round trip, raid repulsion rate 118 of 400, flip guard, gate control).
