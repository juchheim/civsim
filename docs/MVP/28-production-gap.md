# 28. Production gap: why the civilization builds less than it researches

Status: fixes 2 to 5 applied 2026-11-07. Fixes 6 and 7 (Iron pacing) applied the same day; the items in §8 are open.

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
| One Iron building at a time per settlement, at 0.10 of its work | share raised to 0.20 (§5) |
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

## 5. Iron build share

The Iron construction bucket (one Iron structure under way per settlement, with the stone it still lacks) could take
at most 0.10 of a settlement's work. It is now the constant `IronBuildLaborShare`, set to 0.20.

| Share | Iron types built | People per civilization | Structures per civilization |
|---|---|---|---|
| 0.10 | 24% | 1315 | 809 |
| 0.20 | 32% | 1328 | 824 |
| 0.30 | 32% | 1311 | 810 |
| 0.40 | 32% | 1311 | 800 |

(Four-seed Iron matches, with the §2 to §4 changes. 0.20 is the lowest share on the plateau; more labor does not help
because the remaining types wait on the one-at-a-time build order, not on labor.) The 24-minute politics golden reaches
the Iron Age, so `tests/golden/politics-v1.txt` was refreshed with this change.

## 6. Iron pacing: a longer tail

The first KEY-5 comes at about year 76 to 82 and most Iron building types unlock in the 5 to 12 minutes before it. The
chapter ended 24 seasons (6 years) after it, so those types were unlocked and then the match was over. The countdown is
`ChapterRules.IronCountdownSeasons` and the hard cap `IronCapYear` is 130 (was 96, which a longer countdown would have hit).

Four-seed Iron matches, with the §2 to §5 changes, only the countdown varying (cap raised to 400, run to minute 160):

| Countdown after first KEY-5 | Iron types built | Unbuilt types unlocked in the last 10 min | Match ends (min) | People per civ |
|---|---|---|---|---|
| 24 seasons (original) | 32% | 106 | 85 | 1328 |
| 48 | 40% | 82 | 91 | 1607 |
| 72 | 47% | 26 | 97 | 1799 |
| 120 | 51% | 0 | 109 | 2127 |

A longer countdown alone buys build time by letting every civilization finish nearly all of the age (272 types unlocked
at 72 seasons), which is not wanted: the age should end with some research left undone. §7 buys the build time another way
and the countdown is set to 48.

## 7. Late Iron projects cost more

The 28 numeric Iron projects at prerequisite depth 3 or more inside E4 (the Great Library, Dome and Lighthouse
projects, castles, the aqueduct, the academy, the harbor, the bathhouse and the rest of the late chain; keystone KEY-5
excluded) now cost twice their derived cost (`cost` and `cost_reason` in their catalog entries; the derived cost is
1200 x the type multiplier). Research slows toward the end of the age, so unlocks arrive spread out instead of in a rush,
and KEY-5 (which needs AGR-10, GOV-09, BEL-09 and DEF-08) comes later.

Four-seed Iron matches (cap raised to 400, run to minute 170):

| Late cost | Countdown | Types unlocked of ~272 | Iron types built | Median minutes from a building unlock to the match end | Match ends (min) |
|---|---|---|---|---|---|
| x1 | 36 seasons | 225 | 36% | 7.5 | 88 |
| x2 | 36 | 235 | 48% | 10.5 | 95 |
| **x2** | **48 (chosen)** | **248** | **48%** | **13.2** | **98** |
| x2 | 72 | 268 | 49% | 18.2 | 104 |
| x3 | 36 | 230 | 45% | n/a | 97 |
| x3 | 72 | 264 | 47% | n/a | 106 |

x3 does no better than x2. The chosen setting gives a building about 13 minutes to be built before the match ends and
leaves about 24 types never unlocked. The late-cost overrides were written by script from the derived costs (the cost
does not follow a later change of the era base cost by itself).

## 7a. Pacing check against the 2026-11-04 refit (I-T1)

Only I-T1 (own KEY-4 to KEY-5 pacing, `25 §13`) is touched by the §5 to §7 changes, and it has no approved band yet
(`balance/m11-it1-pacing-reconciliation-2026-11-06.md`), so this is a comparison, not a pass or fail. Same harness, same
seeds `20264001` to `20264024` (24 matches, four Warlord seats each, run to the natural chapter end), two builds: the
parent of the walls fix (`a482e0d`, cap 96, countdown 24) and current main (cap 130, countdown 48, all changes in this document).

| | Before (a482e0d) | Now (main) |
|---|---|---|
| Seats reaching KEY-4 / KEY-5 | 94/96, 75/96 | 95/96, 83/96 |
| Own KEY-4 to KEY-5, mean / median | 19.6 / 19.4 min | 25.2 / 25.1 min |
| p10 to p90, range | 17.7 to 22.2, 16.4 to 26.3 | 22.1 to 28.3, 19.8 to 34.3 |
| KEY-4 reached, mean minute | 63.8 | 64.6 |
| Chapter length, median | 85.7 min | 95.8 min |
| Year-cap endings | 0/24 | 0/24 |
| Iron projects done per seat / people per seat | 41.1 / 1191 | 50.1 / 1720 |

The "before" arm reproduces the 11-04 refit (19.6 mean), so the comparison is sound. KEY-4 comes at the same time; the
whole 5.6 minute (29%) slowdown is the age's second half, which is what the doubled late-project cost was meant to do.
The cap is never reached. B4-06 (metal and fuel) was run by another session on a pack with the same hash as current main
and has open I-T2 coverage misses; those were not re-attributed here.

**Decision (user, 2026-11-07): keep about 25 minutes from KEY-4 to KEY-5.** The late-project multiplier stays x2 and the
countdown 48. This is the user's sign-off on the pacing these changes produce; it is not a formal I-T1 acceptance band
(25 §13 still lists the band as TBD), so any later refit starts from this 25.2 minute mean (p10 to p90 22.1 to 28.3).

## 8. Still open

* Gatehouses and castles: addressed in §9.
* The eight dead unlocks, and a way for the player to see (or steer) what a town is waiting on.
* Bronze pacing, if the same tail problem is wanted fixed there.

## 9. Gatehouses and castles

On current main (12 four-seed matches, 48 civilizations, 1131 towns, census at the match end) the defense works were nearly
absent: 805 towns had stone walls and 316 wanted a gatehouse, but 41 had one; a castle needs a rival settlement within
8 tiles and only 30 towns had one, 7 of them walled and large enough, so 1 castle was built. Frontier wall segments (rival
within 6 tiles) were built 0 times. Three causes, in the order they were found:

* **Stone.** Iron works quarry their blocks only from the stone within 3 tiles of the town, which is spent by the time they
  are wanted (the same shortage as §4). Towns finished the labor of a structure and then waited for blocks that never came
  ("gatehouse lacks block" was the commonest stuck reason). Iron works now fall back to the stone within 8 tiles that no
  rival holds, the reach walls already use (`TakeReachStone`).
* **Queue order.** A town builds the first wanted structure in a fixed order, and the gatehouse (9th) and castle (11th) came
  behind terraces, roads, bridges, aqueducts, bathhouses and theatres. The gatehouse, the crane and the castle are now taken
  first. They are wanted only by a walled town, or by a walled border town for the castle, so this does not crowd out
  towns without walls.
* **Castle reach.** A rival within 12 tiles (two Iron claim radii of 5 and a gap of 2: where two civilizations' claims meet)
  instead of 8. A castle still needs walls, 60 people, and a garrison of four supplied soldiers to do anything.

Same 12 seeds, three builds:

| | Before | Defense first | Defense first + 8-tile stone |
|---|---|---|---|
| Gatehouses | 42 | 38 | **159** |
| Castles | 1 | 4 | **37** |
| All Iron works | 1990 | 1986 | **2697** |
| Aqueducts / bathhouses / theatres | 6 / 2 / 11 | 7 / 10 / 11 | **70 / 88 / 87** |
| Academies | 49 | 35 | 123 |
| Roads / bridges | 598 / 258 | 612 / 275 | 706 / 341 |
| People per civilization | 1778 | 1773 | 1781 |
| Own KEY-4 to KEY-5, mean | 25.1 min | 25.4 | 24.9 |

The stone was the main cause: priority alone moved nothing (towns started the work and then stalled on blocks), and the
8-tile stone alone is what lifted every stone-built structure. Pacing and population did not change. Castle building
still depends on a neighbour within 12 tiles, so interior towns do not build one; that is intended.

Tests: `A_walled_town_builds_its_gatehouse_before_the_other_Iron_works_it_wants`, and the castle test now checks the
12 and 13 tile edges. Goldens (toy, research, politics, content pack) are unchanged. Not done: frontier wall segments
(reach 6, still 0 built; it shares its constant with the border-flip guard, so it was left alone), and the Iron acceptance
gates were not rerun for this change.
