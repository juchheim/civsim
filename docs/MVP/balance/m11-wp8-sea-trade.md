# M11 WP8: war, batch 3 (trade friction, long routes, harbors, galleys and blockade)

Scope: TRD-06, WAY-06, TRD-07, HUS-09, SEA-07 and SEA-09, Iron chapter only. (P) marks provisional numbers. Faith (BEL-09), tribute and leagues (DIP-06, DIP-07), overseas colonies (TER-09) and the Great Lighthouse (SEA-08, with the other two Wonders) are later batches.

## What was built

| Rule | Where | Behaviour |
| --- | --- | --- |
| Trade friction (TRD-06, WAY-06) | `PoliticsSystem.TradeFriction`, `GainFor` | The sim has no per-trade "friction" number. The thing that stands between two settlements and a deal is the margin each side wants before it trades (`BarterGain`, 10%). Coinage takes that margin to x0.6, route guides divide it by 1.25 on top (x0.48 together). No goods, money or routes are created. |
| Long routes (TRD-07) | `CaravanTiles`, `SendCaravan` | The sim has no cap on route length either, so "range x3" is read as speed: a compact caravan covers three times the tiles a season, but only in a mode the civilization has. On foot needs pack animals (HUS-09). By sea needs a harbor (SEA-07) at both ends and neither blockaded. Anything else travels as before. Rounds up, never to zero. |
| Camel desert multiplier (HUS-09) | none | The walking graph has no terrain cost, so there is no desert cost to divide by three. HUS-09 only gates the on-foot mode above. Recorded as not modelled. |
| Harbor (SEA-07) | `IronStructure.Harbor`, `HasHarbor` | 24 ws, 8 blocks, 12 wood. A tier 1+ town of 30+ people beside the sea (a tile with an ocean or coast neighbour; lakes and rivers do not count). |
| Port city support (SEA-07) | `GroupCap` | The port's group cap is x1.2. Food, housing and water caps still bind separately, so the harbor never creates food, water or houses. |
| Ship capacity (SEA-07) | none | A caravan's load is the smaller of the seller's surplus and the buyer's shortfall; there is no ship load limit to double. Recorded as not modelled. |
| War galley (SEA-09) | `IronStructure.Galley`, `CrewedGalley` | 20 ws, 14 wood, 1 bloom. A harbor town with a standing guard keeps one. It counts only with four supplied soldiers in post. |
| Blockade (SEA-09) | `PoliticsSystem.Naval.cs` | `OrderBlockade(civ, harbor)`: needs SEA-09 and SEA-04, war with the harbor's owner, a harbor that civ has seen, and a crewed galley whose sailing route to it is at most 100 tiles (P). One galley holds one port. It shuts that harbor's sea routes (new compacts refused, caravans already out turn back with their cargo, no harbor-mode speed) and nothing else; an overland route to the same town stays open. |
| On station | `AdvanceBlockades` | Each season after the first: a 3% wreck chance (the base voyage risk, P; the passage out is not rolled); the harbor owner's galley within 8 tiles (P) contests it, a seeded draw weighted by the four strongest fighters on each side (x1.1 for home waters) in which the loser's galley is lost. The blockade lapses with peace, the harbor, the galley or its crew. |

## Not done

There is no `CommandKind` for a blockade yet; `OrderBlockade` is a method like `OrderAssault`, and the AI port and the player command arrive with WP9 and WP10. Command numbers will be appended, never renumbered.

## Checks

Seven tests in `IronSeaTradeTests`: friction (and the Bronze control), the three route modes, harbor site and cap, blockade rules, what a blockade shuts, lapse and snapshot round trip, relief galley.
