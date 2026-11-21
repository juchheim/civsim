# M13 WP-E6-10 slice 1: what the stock AI builds in Discovery, and one rule change (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted. First slice of WP-E6-10 ([38 §9](../38-discovery-age.md)): a measurement of the stock AI's natural play with every E6 project available, and the one policy change it called for. No AI code was edited: the planner builds whatever a structure's gate opens, so "AI policy" here is gate design.

## The measurement

`discovery-match --pack civsim.pack --seed S --minutes 130 --grant-e5 --grant-e6` (every E6 project except KEY-7 granted when a civilization reaches KEY-6; four civilizations a world; the harness now also counts castles, hospitals, gardens and colonies).

| World | Engines | Ovens | Canals | Galleons | Factories | Exchange houses | Bastions | Harbors | Castles / concentric | Hospitals / gardens | Colonies | Towns with coal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 20261019 (before the change) | 33 | 42 | 37 | 9 | 34 | 4 | **0** | 11 | 7 / 7 | 42 / 42 | 10 | 108 of 124 |
| 20261019 (after) | 36 | 42 | 37 | 10 | 36 | 4 | **7** | 11 | 7 / 7 | 42 / 42 | 10 | 108 of 124 |
| 20261020 (after) | | | | | | 4 | | | | | 12 | 93 of 117 |

All four civilizations reached KEY-7 within 25 seasons of KEY-6. What the AI does unaided: it builds engines, ovens, canals, factories, galleons and one exchange house per civilization; it staffs a garden wherever it has a staffed hospital (42 each, the planner's appetite rule working); it plants **10 to 12 overseas colonies** a world; and coal reaches most towns. **Coal, ocean, colony, garden and exchange policy need no AI change.**

## The one finding: bastion forts could not happen

Every castle (7 of 7) had already taken the concentric step (DEF-09, E5) long before DEF-10 arrived, and slice 1 made the bastion an *alternative* that the concentric castle refused. So the stock AI raised **no bastion fort**, and a human who followed the E5 path would be locked out as well: DEF-10 would do nothing.

**Change.** A garrisoned castle may now take a bastion fort **after** the concentric step. The bastion adds its **wall on the integrity track (6.75 against 4.5)**; the **storm multiplier does not stack** (the concentric castle's x1.5 or the bastion's x1.5, never both: the defense product already tests the concentric flag first). A castle without the concentric step may still take the bastion for its multiplier as before. After the change the same world raises 7 bastions. The fitted numbers did not move: the oracle's wall and storm figures describe exactly this pair.

Files: `PoliticsSystem.cs` (the bastion flag no longer requires "no concentric castle"), `BuildGates.cs` (the gate no longer refuses it), `Raids.cs` (comment), `DiscoveryWarTests` (two tests updated: a bastion may follow the concentric step, the storm multipliers do not stack).

## Not done / still open in WP-E6-10

1. **Siege outcomes against bastions** in natural play (D-T7 acceptance): the forts now exist, so the F-T5 force-table run can be done; needs the whole age, WP-E6-12.
2. **Council prior** and **blunder telemetry** for the E6 institutions (D-T8: "working, queued or dormant with a reason within a season").
3. **Natural-play measurement without grants** (E5 is still draft): re-run when E5 is promoted.
4. **Tuning pass on harbors**: only 11 harbors across four civilizations still limits TRD-10 (joint-stock needs two harbor towns); the exchange houses came through (4), so no change yet.
