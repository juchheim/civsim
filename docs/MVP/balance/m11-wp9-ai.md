# M11 WP9: AI and telemetry for the Iron chapter

Scope: what the autonomous civilizations do with the E4 systems, and the new blunder counters. Iron chapter only: the Strategist's Iron step returns at once outside it, so Bronze decisions, snapshots and checksums are unchanged.

## What the Strategist does (docs/MVP/25 §11)

`Strategist.DecideIron`, after the war step each season. It reads its own exact state and its belief only, and every order is checked again by the rules (a refusal costs nothing).

| Lever | Condition | Order |
| --- | --- | --- |
| Tribute | At war with a rival it has contacted for at least 4 seasons, knows DIP-06, is no one's subject, has no assault or occupation out, and believes its own field strength is at least 1.7 times the rival's (the rules require 1.5). Tried on a rhythm of every 8 seasons, staggered by the rival's number. | `OrderTribute`: the war ends on terms. |
| Blockade | At war, knows SEA-09, has a crewed galley, and has *seen* a harbor of that rival (a new `SeenFlag.Harbor` bit, set only in the Iron chapter, like the mining-camp bit). Every 4 seasons, staggered. | `OrderBlockade` on the nearest believed settlement of that rival; the rules say whether it really has a harbor. |
| League | Allied with a contacted rival and knows DIP-07. Every 12 seasons, staggered. | `OrderLeague`. |

The rhythms are arithmetic on the season number, so the Strategist stores nothing new and its snapshot format is unchanged. Engines, Wonder building, faith, academies, castles and roads need no order: the settlement planner builds and staffs them, a besieging host takes the engines it has, and partners pledge labor to a league member's Wonder on their own. A8's rule holds: the Strategist reads the belief and its own state, and the one hidden fact a blockade order can test (whether a believed town has a harbor) is a single bit per try, tried at most once every four seasons per rival.

## New blunder counters (harness, per AI seat)

| Counter | Meaning |
| --- | --- |
| `IronIdleInstitutionSeasons` | Seasons with at least one staffed institution standing idle: an academy with no scholar, a castle with no garrison, a galley with no crew, a temple with no missionary once BEL-09 is known (`IronIdleInstitutions`). |
| `EnginelessWallSieges` | Sieges begun by a civilization that knows WAR-09 but brought no engine. ("Engine research mistaken for a built engine.") |

The other blunders in the doc (starving a donor for public grain, replacing every bronze kit at once, treating queued works as complete, an infinite treaty retry) are prevented by construction: grain moves only above the donor's winter reserve, kits are wanted only for adults who lack one or are about to lose theirs, the Strategist reads built structures only, and the rhythms above bound every retry. Nothing counts them because they cannot occur.

## Not done here

* **Spark and the forecast.** They are generic over the catalog and the chapter rules (WP5), but every E4 row is still a draft, so no civilization can research one and the KEY-5 forecast and the use-gated valuation cannot be exercised yet. They are the first thing to check once the rows are promoted together.
* **The Council and player commands.** `OrderBlockade`, `OrderTribute` and `OrderLeague` are methods like `OrderAssault`; the `CommandKind` numbers (append only) and the screens are WP10.
* **Fog fuzz for Iron.** The A8 perturbation tests need Iron-mode matches with E4 research, so they also wait for promotion.

## Checks

Four tests in `StrategistIronTests` (tribute rhythm and its conditions, blockade against a harbor seen, league rhythm) and one in `IronLearningTests` (idle institutions).
