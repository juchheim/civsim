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

WAR-07's garrison-claim clause waits for castles (DEF-08). WAR-09 siege engines and the walls (DEF-06 to DEF-08) are the next batch.

## Checks

Five new tests in `IronChainTests`: slots at 10% and 20% and none without WAR-07 or in Bronze; soldiers staffed and supplied; arms wear; the strength constants; the snapshot round trip. Bronze checksum `484d417e35bffe60` and the goldens are unchanged.
