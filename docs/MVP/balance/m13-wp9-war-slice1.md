# M13 WP-E6-9 slice 1: DEF-10 bastion forts and WAR-13 drill (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted (on top of the committed textile slice). First slice of WP-E6-9 ([38 §5.6](../38-discovery-age.md)), fitted to `e6_combat_oracle.py` before it was coded, as D-T7 asks. Everything tests `DiscoveryChain`; the fort kind is written only in Discovery snapshots, so Bronze, Iron and Fief bytes, hashes and snapshots are unchanged. All numbers are provisional.

## What the slice does

| Part | How |
|---|---|
| **The fort** | New structure `bastion_fort` (DEF-10; 70 work-seasons, 60 block for the registry's 40 block and 20 fired brick). Hard gate: a town with a **garrisoned castle** (four fed soldiers in post), DEF-10, **no concentric castle**; one to a town. The planner raises it where the gate opens. |
| **Integrity track** | The bastion's wall counts **6.75** (1.5 times a gatehouse wall's 4.5) in `Sieges.Batter`, against 3.0 for a plain wall. Oracle Part 1: a three-cannon train still breaches in a season, **one cannon needs 3 seasons against a bastion (2 against a tower wall)**, two cannons 2. The extra seasons are what relief, sorties and starvation need to stay decisions. |
| **Storm multiplier** | Defenders of a garrisoned castle with a bastion fight at **x1.5 on top of the castle** (1.25 x 1.5 = **1.875**, below the oracle's 1.9 storm crossover). It is an **alternative to the concentric step**: the two never stack, and each gate refuses the other once one stands. An ungarrisoned fort is empty stone and counts for nothing. |
| **WAR-13 drill** | A civilization with WAR-13 **and GOV-12** fights its levy (the adults who are neither professional soldiers nor warriors) at **x1.5**. Oracle Part 3: a drilled iron levy needs 38 against the undrilled 53 to beat 20 shielded steel professionals half the time. Professionals and warriors are unchanged; the state is needed first. |

## Deviations from the typed rows, and why

1. **The gunpowder wall penalty is not halved.** DEF-10's text says the penalty "is halved (P)". The oracle (Part 2) shows that a bastion x1.5 *with* a wall kept at 65% closes the storm for good (defender multiplier 3.66 against a 1.9 crossover), which would erase gunpowder siege a second time. The recommended and built reading is the bastion on the integrity track and as the alternative to the concentric step, with the 30% powder remainder as before. The catalog text now says so.
2. **Crew attrition (+25% for engine and cannon crews) is not built.** The siege code has no per-crew attrition term to scale; it needs a casualty rule of its own (a later slice, with the siege acceptance runs).
3. **No musket good.** "Muskets" in WAR-13 is carried by the project and GOV-12; a musket kit would be a new arms ladder rung and belongs with the arms work.

## Tests (5 in `DiscoveryWarTests`, all pass)

A garrisoned castle plus a bastion multiplies the defense by 1.5 (1.875 with the castle), an ungarrisoned fort counts for nothing and a concentric castle displaces it; the gate needs DEF-10, a garrisoned castle and no concentric step, one to a town and the concentric gate refuses it once the bastion stands; one cannon takes three seasons against a bastion wall against two against a tower wall while a three-cannon train still breaches in one; nothing in Fief; a drilled levy fights up to x1.5 and needs both WAR-13 and GOV-12.

## Not done in WP-E6-9

1. Engine and cannon crew attrition (x1.25 against a bastion); coastal fortifications (DEF-11); a musket rung.
2. A natural-play siege measurement against the E5 force tables (D-T7 acceptance), which needs the promoted E6 pack (WP-E6-12).
