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
12 and 13 tile edges. Goldens (toy, research, politics, content pack) are unchanged. The Iron acceptance gates were not rerun
for this change.

## 10. Frontier wall segments

Segments (DEF-07, 12 work-seasons, up to two per town, each facing one rival civilization) were built 0 times in 48
civilizations: a town wanted one only when a rival settlement lay within 6 tiles. That reach only chooses which neighbour a
segment faces; the tiles it guards are a separate constant in the politics system (`FrontierGuardTiles`, 6 tiles from the
town), so (correcting the note in §9's first version) the reach could be widened without touching the border-flip guard.

* `FrontierReach` is now 12, the same border distance as a castle (two claim radii of 5 and a gap of 2).
* The frontier segment joins the gatehouse, crane and castle at the front of the Iron build order. It is cheap and is wanted
  only by a town with a close rival.

Same 12 seeds (48 civilizations), before and after:

| | Castles and gatehouses build (§9) | + frontier reach 12 and priority |
|---|---|---|
| Frontier segments built | 0 | **56** |
| Gatehouses / castles | 159 / 37 | 151 / 30 |
| All Iron works | 2697 | 2742 |
| People per civilization | 1781 | 1746 |
| Own KEY-4 to KEY-5, mean | 24.9 min | 25.1 |
| Wars / raids / assaults / captures | 68 / 198 / 66 / 58 | 66 / 190 / 68 / 62 |

Gatehouse and castle counts moved by the run-to-run noise of a changed match (the matches diverge once any build order
differs); pacing, population and the war events did not move beyond it. Test: the frontier test now checks the 12 and 13
tile edges. The raid-repel and flip-guard rules themselves were not changed, and this run does not measure how many
raids they turned back.

## 11. Gate check, and an idle-labor regression this work caused

The formal gates were run against current `main` (`dc2147f`) on 2026-11-07, with baselines at the clean parent of this work
(`a482e0d`) and at the B4-06 session's frozen source (`040cdc4`). Several are samples smaller than the formal one and are
marked.

| Gate | Result |
|---|---|
| Goldens (toy, research, politics, content pack), strict validation, 1115 tests | pass; the same two unrelated tests fail as before |
| S1 replay determinism (probe, 32 of 1,000 matches) | 32/32 identical |
| S15 no crash or invalid state (probe, 24 of 1,000 matches, 64 min) | 24/24 clean |
| S12 headless chapter time | 8.9 s against 30 s: pass |
| S13 tick time and GC (10-minute window, shared machine) | 1.79 ms average against 4 ms, longest GC pause 1 ms against 16 ms: pass |
| B5 Bronze research timing | pass; KEY-4 median 61.6 min (61.5 at `040cdc4`); B6 shows the same two-seed diagnostic miss on both builds |
| Politics M10 (8 seeds, diagnostic) | same as `040cdc4`: settlements 11.7 against 11.6, Legacy split 54.8/31.8/13.4 against 54.9/31.6/13.5, claimed land 37.2% against 35.7% |
| B4-06 fixed 13-seed screen, the other session's options | 571 I-T2 passes, **1 fail**, 771 censored (632/0/695 at `040cdc4`); kit ledgers 1414/1414 exact; no residuals; no Iron-era clearing crossings; mature work share at most 11.0% |
| M10 AI A1 | 81%, 88% at `040cdc4`, 75% at `a482e0d`: noise at 16 matches, no trend |
| M10 AI A2, A3 | pass |
| M10 AI A4 (Chieftain wins 25 to 40%) | fails on all three builds: 50%, 56%, 56% |
| M10 AI A5 (no archetype above 30%) | Grower and Sage fail on all three builds: 40.6%/35.4%, 36.5%/35.4%, 39.6%/35.4% |
| M10 AI A7 idle labor | **regressed by this work**, see below |

A4 and A5 were already failing at `a482e0d`, before any of this work; they are not touched here.

**The one B4-06 failure** is the capital `s0` of seed `20262121`, seasons 264 to 276: 45 tool workers, 30 holders (66.67%),
fuel passed. On `040cdc4` and on `2127eef` (frontier only) the same seed gives 51 passes, 0 failures: `s0`'s window was
censored there. The failure appears only with `dc2147f`; it is the worker-growth against holder-supply class of miss that
the B4-06 session has been chasing, and one window in 13 matches does not show whether the unlock work makes it more
likely. The B4-06 acceptance block will say.

**A7 idle labor.** The M10 blunder detector flags a settlement where more than 15% of working-age adults did no work in a
season while its food security was under 1. It read 0 at `a482e0d`, and 16 seasons (1.0 per match) from `02c59ca`, the
first commit of this work, onward; with the food-labor cap switched off in the pack it read 0 again. Cause: the cap (§3)
holds food to 65% of plannable labor, but building takes a few percent and material work at most 18%, so the labor the
cap withheld had nowhere to go and the adults stood idle. Fix: labor the cap withheld from food that nothing else wants is
returned to food. It is taken last, after every other use of a figure's capacity: the first version assigned it before
building and material work and cost 9% of the Iron works, so the order matters. Same 12 four-seed Iron matches before and
after (`dc2147f` against the fix): Iron works 2966 to 3092, people 1795 to 1812, own KEY-4 to KEY-5 25.3 to 25.5 minutes;
A7 idle labor 0 seasons, pass. Goldens and tests are unchanged by the fix (the 24-minute politics match never reaches a
season where the cap binds with spare labor). No unit test pins this: the Bronze fixture never runs food-short, so the
regression is guarded only by the A7 gate.

Not rerun after the fix: A1 to A5 and the rest of the M10 AI set (about two hours at the load on this machine).

## 12. A4 (Chieftain against Warlord)

The M10 gate A4 failed on three builds (50%, 56%, 56%), one of them the clean build before this work. Eighty mirrored matches
(`ai --duel chieftain warlord --seeds 40 --minutes 64`) give 36% at `a482e0d`, 38% at `040cdc4` and 35% at `a7147be`, all
inside the 25 to 40% target. The 16-match sample of an 8-seed run carries about +/-12 points of noise, so the miss was
sampling, not the AI or the economy. Following the A2 precedent, acceptance-quality runs now judge A4 on 40 seeds
(`M10AiAcceptanceStatus.MinimumA4Seeds`, signed off by the project owner 2026-11-08); the target and the AI are unchanged.
A1 shows the same noise (75%, 88%, 81% on the same three builds at 16 matches) and was not changed.


## 13. A1 (Warlord against the Council bot) re-fitted to 80%

A1 (target 85% on Legacy) read 75 to 88% on 16-match samples. Two hundred mirrored matches each
(`ai --duel warlord council --seeds 100 --minutes 64`) give 80% at the current main, 81% at `040cdc4` and 81% at `a482e0d`
(the build before this work), so this is not a regression from the production fixes and not sampling noise: at 200 matches
the error is about +/-3 points and 85% is out of reach. Mean Legacy is 6,185 against 5,171, with the Warlord holding 25% of
the land to the Council bot's 14%.

At the original 33-minute horizon the same duel is a tie (52%, Legacy 2,609 against 2,594, 200 matches). The 85% bar (94%
in doc 14 §18) was set when Firsts paid Legacy and the Warlord's research tempo converted straight into points; Firsts now
pay 0 (the Legacy redesign of 2026-11-06), so the Warlord's lead builds only through the second half. Tuning attempts on
the Warlord are recorded in doc 07d (Keystone tempo, rival-pace override, Found threshold, first-expansion cap) and none
held across the other gates.

Decision (project owner, 2026-11-08): the A1 bar becomes 80%, judged on 40 seeds (80 matches) in acceptance-quality runs
(`M10AiAcceptanceStatus.A1Target`, `MinimumA1Seeds`). The Warlord AI is unchanged. The 16-match duel still feeds A7 and the
telemetry gates. A sampled true rate of 80 to 81% sits at the bar, so a future A1 miss at 80 matches is within about
+/-5 points of noise; read it against the 200-match figures above before acting on it.

## 14. A5 (archetype balance)

At the 64-minute chapter horizon A5 failed on the current build: 72 free-for-all seeds (288 appearances per archetype,
`ai --ffa warlord --seeds 72 --minutes 64`) give Builder 28.1%, **Grower 36.1%**, Wanderer 19.1%, Warlord 15.3%, Sage 26.4%
against a 30% cap. The 24-seed run had read Sage at 33%, which was noise.

The Grower won by growing: 15.5 towns against 11.4 to 12.9, so 1,680 Dominion against 1,522 to 1,609, with Mastery and the
Thriving part level. The archetype table of doc 14 §17 was fitted when Firsts paid Legacy and decided matches; with Firsts at
0 (the Legacy redesign of 2026-11-06) the economy bonuses that buy settlements count for more.

Change (`LivingEconomyModel.Profile`): Grower fertility 1.12 to 1.06 and food capacity 1.11 to 1.06; Sage fertility 1.05 to
1.0 and adult mortality 0.95 to 1.0. Builder, Wanderer, Warlord and the research scales are unchanged. Result on 72 seeds:
Builder 26.4%, Grower 26.7%, Wanderer 25.3%, Warlord 23.6%, Sage 22.9%. A1 to A4 play Balanced AIs and are unaffected.
`research-v1` changed on purpose (its bot is a Grower); toy and politics did not.

## 15. Late unlocks are intended (project owner, 2026-11-08)

The survey of 120 matches (docs/MVP/28 §13 data) showed most Bronze civilizations unlock bakery, caravanserai, megalith, school and
stone walls only in the last 10 minutes of the 64-minute chapter, so those buildings are rarely standing at the end. Decision:
every era keeps late unlocks, as the payoff the era's research builds toward; the research pacing and unlock times are not changed.
What the survey does show as defects, separate from this, is a building that is unlocked with time to spare and still never built:
drying rack (0 of 320 civilizations built one; its rule needs two fresh meat in store, which the town eats first), caravanserai
(0 of 5 to 15 that had it early), hillfort (55%), weir (69%) and the granary (87%, bricks). Those are rule or supply problems, not pacing.

## 16. Late projects cost more in every era (project owner, 2026-11-08)

Method of §7, applied to E0 to E3: the 80 numeric non-keystone projects at prerequisite depth 3 or more inside their era
(16 in E0, 18 in E1, 26 in E2, 27 in E3, less the seven defense and war projects below) cost twice their derived cost (`base_cost` of the era x type multiplier; `cost` and
`cost_reason` in their catalog entries, written by script, so they do not follow a later change of an era base cost).
Keystones are excluded. `ChapterCapYear` goes 64 to 85 as a step; the cap itself is then removed (the year cap is a legacy rule).

Eight-seed Warlord against Council duels (16 matches, 64 civilizations), no cap, run to minute 140. Means are of the civilizations
that reached the keystone before the chapter's countdown closed it:

| Keystone | Old costs | x2 late costs | Change |
|---|---|---|---|
| KEY-1 | 6.7 | 8.1 | +21% |
| KEY-2 | 20.6 | 24.6 | +19% |
| KEY-3 | 41.8 (61 of 64 civs) | 53.0 (60) | +27% |
| KEY-4 | 60.9 (49) | 72.5 (50) | +19% |

Era lengths: E0 6.7 to 8.1 minutes, E1 13.9 to 16.5, E2 21.2 to 28.4, E3 19.1 to 19.5 (KEY-4 means are cut off by the 6-year
countdown after the first rival, so the E3 figure understates the slowdown). A 6-minute-tail intermediate run with x1.5 gives
KEY-1 7.6, KEY-2 22.5, KEY-3 46.8.

## 17. The year cap is removed (project owner, 2026-11-08)

Decision: the chapter's hard year cap is a legacy rule and is removed for both chapters (Bronze `ChapterCapYear`, last 85 and before that
64; Iron `ChapterRules.IronCapYear`, 130). A chapter now ends only by (1) the human completing its last keystone, or (2) the countdown
after the first rival completes it (`ChapterCountdownSeasons` 24 seasons in Bronze, `IronCountdownSeasons` 48 in Iron). Reasons: the cap
only ever cut off the slow civilization's chapter at an arbitrary year, it had to be raised each time a cost change moved the keystone
(64, 85, 96, 130), and with the countdown a rival already forces every contested chapter to a close.

What changed:
- **No end is representable.** `MatchEnd` starts from `MatchEnd.Open`: reason `MatchEndReason.None` (0), `EndSeason = MatchEnd.NoEndSeason`
  (`int.MaxValue`), `HasEnd = false`, `EndTick = long.MaxValue`, `IsOver(tick)` always false and `SeasonsLeft(tick) = -1`, so nothing
  overflows and every `while (!end.IsOver(tick) && tick < horizon)` loop keeps working unchanged. `MatchEndReason.YearCap = 3` stays as an
  obsolete, never-produced member so the number is not reused and old data still names it. `ChapterRules.CapYear`, `IronCapYear`,
  `ResearchCatalog.ChapterCapYear` and the `ChapterCapYear` constant are gone (the content pack and `tests/golden/content-pack.txt`
  changed with the constant); `IronWithLimits(cap, countdown)` became `IronWithCountdown(countdown)`; `iron-match --cap-years` is removed.
- **A match nobody finishes never ends by itself.** Every headless harness loop is bounded by its own `--minutes` horizon (tournaments,
  `research`, `ai`, `politics`, `iron-match`, `acceptance --s1/--s15`). S12 (`acceptance --s12`) now takes its horizon from `--minutes`
  (default 120) and still requires the chapter to end exactly on its rule tick; a chapter that has not closed by the horizon fails S12. S1
  accepts a match that runs to its horizon exactly when no clause has fired.
- **B6 (22 §16) is redefined.** The old clause "at most 40% of Balanced matches reach Year 64" becomes "at least 60% of Balanced matches
  close within `--minutes` by the countdown or the human keystone" (the same tolerance read the other way round), plus the unchanged check that every sample's end
  equals the rule (open when nobody finished, otherwise first rival + countdown, or the human sooner) and replay identity.
- **Game.** The HUD tooltips and the countdown toast no longer mention a Year cap; the end screen shows "The age has closed" only in the
  unreachable case of an ended match with neither clause (the title for the two real endings is unchanged).
- **Consequence to watch.** The game still ends when the human or a rival finishes the keystone, so a long human game is bounded by the
  AI; an AI-only match that stalls (for example all civilizations collapse) now runs until the user quits. Goldens are unaffected because no golden reached a cap.

**Exception: the DEF and WAR lineages keep their derived cost.** Seven projects (DEF-01, DEF-03, WAR-01, WAR-03, WAR-04, WAR-05,
WAR-06) were first doubled with the rest. Raiders and scripted openings run on a fixed clock that does not slow when research
does, so doubling the defenses that answer them leaves a civilization less ready than before. It showed in three tests: the
Spark's response to a threat (DEF-01 at 53 and WAR-01 at 44 no longer entered its plan at urgency 1; the fixed defense bonus of
40 would have needed about 160), a Strategist losing a settlement to the scripted Rusher, and the Strategist's first gift
(delayed from season 75 to 190). With the seven back at their derived cost all three pass. The gates A1 to A4 passed with the
doubling in place, so this is a design choice for the early game, not a rescue of a failing gate.

### 14a. Re-fit after the slower pacing (2026-11-09)

With the late costs of §16 and the DEF and WAR projects kept at their derived cost, A5 on 72 seeds read Grower 34.0% at 1.06. Fertility and capacity
at 1.0 give Grower 20.8% (48 seeds), 1.04 gives 31.2% (48 seeds), 1.03 gives Builder 25.0%, Grower 28.5%, Wanderer 21.9%, Warlord 25.7% and
Sage 24.0% on 72 seeds. Grower fertility and food capacity are now 1.03; Sage stays at 1.0. `research-v1` changed on purpose (its bot is a Grower).
