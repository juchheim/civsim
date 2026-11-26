# M13 WP-E6-9 slice 2: the musket rung (2026-11-26)

**Status:** built and tested on `codex/e6-discovery`. Second slice of WP-E6-9 ([38 §5.6](../38-discovery-age.md)); the first slice ([report](m13-wp9-war-slice1.md)) left "a musket rung" out because it needs a new arms good. Discovery only, and fitted to `e6_combat_oracle.py` Part 4 before it was coded. All numbers are provisional (P).

## What it does, in plain words

Before this slice WAR-13 was only a number: a civilization with the drill project and GOV-12 fought its levy at x1.5, but no town ever held a musket, so there was nothing to draw and nothing for the army to carry. Now the musket is a real thing.

| Part | How |
|---|---|
| **The good** | `G-MUSKET`, lasts 40 seasons, unlocked by WAR-13. Held in the Discovery goods array (`DiscoveryGood.Musket`, appended after the telescope), so Faith & Fief saves, hashes and goldens are untouched. |
| **Making it** | Recipe `make_musket` at a **forge**: 0.5 wood, 0.4 wrought iron (or iron), 0.1 cord, 0.8 work. Powder and shot are folded into the weapon, as arrows are into a bow ([36 §2](../36-bow-and-arrow-supply.md)); powder stays the Arsenal's and the cannon's. |
| **The want** | One musket per levy fighter (the same count the bows aim at, at most 40 a town), in a season of enough food, from no more than half the cut wood above the reserve, **at most 3 a season**. A musket counts against the weapons a town holds, so the crossbows and longbows it replaces are not made again as they wear out. |
| **In a fight** | A levy fighter with a musket fights at **strength 2.8** (between an iron edge, 2.4, and a tempered one, 3.0) with training 1.0 like the other levy arms, and the musket **ignores shields** like a bolt. The WAR-13 drill (x1.5, needs GOV-12) then multiplies it, so a drilled musketeer counts 4.2. Muskets are drawn best-first with the rest of the ladder, and are lost with the fallen levy like bows. |
| **In the frame** | `SettlementArms` gains `StockMusket` and `Musket`; `MatchFrame.SettlementMusketPct` is the share of each settlement's fighters holding one, so the drilled-levy look (`LOOK_DRILL`) can follow it. |

## The fit (oracle Part 4: smallest host that beats 20 shielded steel professionals half the time)

| Levy | Host needed |
|---|---|
| undrilled iron levy (2.4) | 53 |
| drilled iron levy (2.4 x1.5) | 38 |
| drilled crossbow levy (2.0 x1.5, training 1.0) | 28 |
| **drilled musket levy (2.8 x1.5, training 1.0)** | **23** |

A drilled musket host nearly matches the professionals man for man (23 against 20): the intended reach of the drill, paid for in iron and limited to 3 muskets a season and 40 a town.

## Deviations and open items

1. **No powder in the recipe.** Chosen so a musket does not depend on the Arsenal's one-per-civilization powder supply; revisit if the siege acceptance runs show muskets are too easy.
2. **The drill still multiplies every levy fighter**, with or without a musket (slice 1's rule, unchanged). The musket adds strength on top; it does not gate the drill.
3. **Rifles (WAR-14, era 7) are not built;** the rung is the place they will join.
4. **Natural-play check still owed** (D-T7): how many muskets a stock AI realm holds, and the siege and field results against the E5 force tables, belong with the promoted E6 pack (WP-E6-12).
5. **Art:** the view can now read `SettlementMusketPct`; wiring the `LOOK_DRILL` switch in the renderer is the next art step.

## Tests (5 in `DiscoveryMusketTests`)

A musket lifts a drilled levy's strength and is worth more drilled than undrilled and more than none; muskets do nothing without WAR-13 or outside Discovery; the planner wants at most 3 a season at a forge with iron and wants none without WAR-13, a forge or iron; a town holding a musket for every levy fighter wants no more, and a stored musket wears out; the arms view reports the musket stock and the fighters holding one.
