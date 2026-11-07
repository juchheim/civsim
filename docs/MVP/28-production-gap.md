# 28. Production gap: why the civilization builds less than it researches

Status: fixes 2 to 4 applied 2026-11-07. Fix 1 (Iron pacing) and fix 5 are open.

A full playthrough to the end of the Iron Age showed a civilization that had researched far more building types than it
had built. Diagnosis was done with a scratch `gap` harness over four-seed Iron matches (seeds 20261019 to 20261022, four
Warlord AIs, the same setup as `iron-match`).

## 1. What was measured

| Cause | Size |
|---|---|
| Iron era is short after the late unlocks: 118 of 154 unbuilt Iron types unlocked in the last 10 minutes | the biggest single cause |
| Stone walls took the settlement's one basic build slot while unaffordable | fixed (§2) |
| Food work took 70 to 85% of every settlement's labor, so construction got 2 to 3% | softened (§3) |
| Stone walls could not be paid for at all (§4) | fixed |
| One Iron building at a time per settlement, at `IronBuildShare` 0.10 of its work | open (§5) |
| Eight build unlocks have no simulation behind them (totem, marker, seasonal camp, shrine, signal post, potter's workshop, water mill, temple tier 2) | open |
| The player has no build command; construction is planner-driven | open |

## 2. Walls no longer park the build slot

`SelectBuildCandidate` returned stone walls with no affordability check once DEF-05 was done, so a settlement with a
palisade kept "stone walls" as its basic build for the rest of the match and never built a granary, pasture or orchard
again. Walls now take the slot only when `CanPayStructure` passes. Effect: orchards 14 to about 95, pastures x2, era 2
built share 94% to 98%. Test: `Unaffordable_stone_walls_do_not_hold_the_basic_build_slot`.

## 3. Food work is capped outside hunger

From the Bronze Age on, food work takes at most `FoodShareCapOutsideHunger` (0.65) of a settlement's plannable labor
unless food security is below 0.9 (food-share rule 1). Effect on the four Iron matches: +21% structures, +8% people.

## 4. Stone walls: cost and supply

Walls cost 60 L of dressed ashlar and 30 work-seasons per Village ring (22 §7.2, x2 Town, x3 City). Neither number could
be met:

* Ashlar comes from the `dress_stone` recipe, which has order 50 and runs only on craft labor left over after the
  higher-order recipes. In the traced matches it got labor in 0.5% of planned settlement-seasons.
* A settlement's quarry range (radius 3) holds about 74 L of stone at the start and the stone does not regrow; by the
  time DEF-05 is done (minute 55 to 70) the median range holds 8.5 L and one season in ten holds none.
* Reserving labor for dressing stone (tried, reverted) lifted ashlar in store to a median of 19 L but still built no
  walls and cut structures built by 9%.

New rule: a wall ring is raised from stone quarried within 8 tiles of the settlement (the reach civic buildings already
use for their stone), taken from raw stone first and from dressed ashlar only if the ground is out of stone. The ring's
build labor covers quarrying, hauling and dressing, so walls no longer depend on the craft step.

| Constant | Was | Now |
|---|---|---|
| `StoneWallsCutStonePerRing` | 60 L ashlar | 20 L stone |
| `StoneWallsLaborPerRing` | 30 ws | 15 ws |

Both are per Village ring; a Town ring takes 2x and a City ring 3x. The stone-walls forecast for the Wall Corvee decree
(`DecreeForecaster.EstimateWallThreatTiming`) uses the same labor constant instead of its own 90 work-seasons.

Measured on the four-seed Iron matches (all with the food cap): with the old 60 L / 30 ws, rings went up 1 to 10 per
civilization; 20 L / 15 ws gave 6 to 18 rings and a few gatehouses. Walls are not free: against the same build without
walls (Iron types built 27 to 29%, 865 to 907 structures per civilization) the matches built 24% of Iron types and about
800 structures (-8 to -12%), and people were 1294 against 1345 to 1376 (-4 to -6%), because the rings take basic build
labor and stone. Cheaper still (20 L / 30 ws, 60 L / 8 ws) did not recover the Iron types. Walls were built 0 times in
every civilization before this change, even when the match was run to minute 120.

Test: `Stone_walls_are_raised_from_stone_quarried_within_reach_without_dressed_ashlar`.

## 5. Still open

* Iron pacing (the match ends about 25 minutes into the age; running the same matches to minute 120 raises the Iron
  share built from 20% to 48%).
* `IronBuildShare` 0.10. In scratch runs a share of 0.25 with the food cap took Iron types built from 23% to 35%.
* Gatehouses are now possible but still rare, and castles still never get built: they sit late in the one-at-a-time Iron
  build order and a castle also needs a close foreign neighbour.
* The eight dead unlocks, and a way for the player to see (or steer) what a town is waiting on.
