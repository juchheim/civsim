# M11 WP11: promotion and the first whole Iron matches

## Promotion

All 57 E4 rows (E4 projects and KEY-5) went from `draft` to `numeric` together, and `civsim validate --strict` is clean (0 errors, 0 warnings).

* **V21** used to forbid promotion until WP5 was done. It now says the E4 rows are promoted as one body: all numeric or none, so a chapter never plays half the Iron Age. The rest of its checks (cost, typed effects, flavor, Moment) stand.
* Nine registry entries had no numeric project using them, which the strict validator warns about. They were attached to the projects that unlock them: `G-ARMS4` (MET-10), `G-PLANK` (WOD-08), `G-GRAPE`, `G-OLIVE`, `G-WINE`, `G-OILPLANT` (FOR-08), `press` (MEC-05), `temple_tier2` (STN-12), `siege_assault` (WAR-09) and the Great Dome (BLD-11, whose text no longer says "deferred", per Q7).
* The content-pack golden was refreshed (hash `920c92f3...`, 519 projects). **No simulation golden changed**: the Bronze checksum is still `484d417e35bffe60`, and the toy, research and politics determinism checks and the fuel check are unchanged against the promoted pack. A Bronze match still cannot reach any E4 row (`ChapterRules`).
* Eight tests assumed E4 was still draft and were updated for the promoted world (counts 214 to 271; the half-promotion check; the Bronze/Iron visibility check; the visible-change check now runs in an Iron match).

## Defects the promotion and the first matches exposed (all fixed)

1. **A crash in research.** FOD-09's `cap population` effect landed in a three-entry array when the row became live (`CapMult[4]`). The Iron economy delivers that cap as physical grain, so research now skips it.
2. **Stone buildings could never get blocks.** The economy quarried blocks only as the first step of dressing ashlar, so every Iron structure that asks for plain blocks (terraces, baths, roads, bridges, theatres, gatehouses, harbors, academies) waited for ever. Iron buildings now have their own labor claim for quarrying and dressing, inside their 10% share of a town's work.
3. **The tool chain starved construction of bloom.** A finished crane waited 50 minutes for two litres of bloom because the kit chain turned every litre into tools each season. The bloom a structure under construction still needs is now held back from recipes.
4. **One stuck structure blocked the whole queue.** A fully worked structure that stays unpaid for 8 seasons (provisional) is shelved with its progress kept, and the town builds something it can finish.

## Whole-match results (four Warlord AIs, Builder, Grower, Warlord and Sage economies, `iron-match`)

Natural play, seeds 20261019 to 20261021: all three matches ran to the chapter's end by the countdown (82 minutes) in about a minute each, deterministic, no errors.

| Seed | KEY-4 (min) | KEY-5 (min) | E4 projects done | Iron kits (T11 / T12) |
| --- | --- | --- | --- | --- |
| 20261019 | 59.7 all four | 79.3 to 81.1 | 34 to 40 | 280 to 410 / 0 to 73 |
| 20261020 | 59.6 to 68.5 | 79.4 (one civ; the rest not yet) | 18 to 43 | 220 to 390 / 0 to 74 |
| 20261021 | 60.7 to 64.3 | 79.1 to 81.7 | 28 to 41 | 290 to 430 / 0 to 156 |

What the Iron systems did in natural play: iron tools spread to about half the adults, a standing guard of 11 to 50 soldiers a civilization, a few missionaries, windlass wells, terraces and roads. No Wonder, no academy that staffed a scholar, no siege with engines, no blockade, tribute or league: the Iron chapter is about 20 minutes long once KEY-4 is reached, and most Iron research finishes in the last few minutes, so there is no time for them to matter. That is a pacing finding, not a defect of the systems.

To see whether the systems work when research is not the bottleneck, `iron-match --grant-e4` gives every civilization all E4 projects at its KEY-4 and runs to minute 110 with the countdown off. There: T12 steel kits reach about 700 to 990 holders per civilization, 120 to 190 soldiers, 6 to 10 missionaries, 4 leagues formed (2 standing), 70 to 90 roads and bridges, about 100 terraces, wells, presses in every civilization, academies and the Library and Dome in progress. Still absent after 50 more minutes: finished Wonders and academies, because stone and wood in a town's range run out.

## Numbers the matches say to refit (none changed)

* **Chapter length.** KEY-4 to KEY-5 takes about 20 minutes. If the Iron Age should hold wars, Wonders and provinces, the E4 era base (1200, a catalog input, not a fit) is too low or the countdown too short. This is the largest lever.
* **Stone and wood in range.** Aqueducts (20 blocks, 8 ashlar), academies, gatehouses and the Wonders (60 to 80 blocks) exceed the stone a town has in range, and eight to twenty towns lack the wood for a crane or a bridge. Candidates: smaller costs, stone sharing between sister towns like the grain, or a wider quarry reach.
* **Idle institutions.** Academies, once built, rarely get a scholar in natural play (the staffing rule keeps four adults free); castles and galleys sit uncrewed. The blunder counter shows it.
* **Wars without sieges.** Eight wars, eight assaults and eight captures in a match, none of them against a wall, so engines, gatehouses, tribute and the blockade have not been exercised in a natural match. They are covered by the fixture tests; the AI levers need walled targets.

## Not run

* The A8 fog fuzz and the AI tournament gates (A1 to A8, B-series) in Iron mode: they need a longer, walled chapter first.
* A human playtest, and the Godot screens (see WP10).
