# M13 WP-E6-1: E6 oracle screens (2026-11-20)

**Status:** fixture and accounting screens only, the E6 counterpart of [m12-wp1-oracle-fit](m12-wp1-oracle-fit.md). Nothing here fits live E6 (none exists), approves a band, promotes content or changes an earlier age. All values marked P in the scripts are hypotheses. Written on `codex/e6-discovery` (from `main` at `deba515`); E5 is still being balanced, so every E5-derived input is a placeholder to re-anchor.

## Reproducibility

Four additive scripts plus one anchors file in `docs/MVP/balance/tools/`; each script has a `--check` that passes. They need only Python 3 and read the catalog YAML, `combat_e3.py` and the E4/E5 oracles.

```bash
for f in research power combat economy; do python3 docs/MVP/balance/tools/e6_${f}_oracle.py --check; done
python3 docs/MVP/balance/tools/e6_research_oracle.py     # reports; drop --check
```

| Script | Screens | Targets in [38 §7](../38-discovery-age.md) |
|---|---|---|
| `e6_research_oracle.py` | KEY-7 closure, candidate costs, backfill debt, forward pacing from the live E5 rate | D-T1 |
| `e6_power_oracle.py` | Iron fuel with coke, the coal ledger, steam in the power ledger and its break-even | D-T3, D-T4 |
| `e6_combat_oracle.py` | Cannon breach time against walls and bastions, the storm crossover, drilled levies | D-T7 |
| `e6_economy_oracle.py` | Four-course rotation decomposed, epidemics at E6, the labor pool | D-T8, D-T9 |
| `e6_anchors.py` | The live E5 anchor (F-T1 holdout at era base 7,300: median 24.3 min, 283 of 320 seats completed) | |

## What the screens say

**Research (D-T1).**
- The catalog reconciles: 41 regular (Craft 12, Kin 12, Reach 17) plus KEY-7. The KEY-7 closure is the 10 projects of 38 §3.2 with **five Craft** (MIN-09, NRG-06, MEC-09, MEC-10, FIB-12), one Kin and four Reach, so unlike E5 the Craft systems run in natural play. Four free picks; the cheapest are AGR-12, BLD-13, CER-11 and NUM-10.
- **Eight** of the 41 are late by the repo's depth rule, not seven as the first draft said: CHM-08, FIB-12, FIR-11, FIR-12, INQ-09, MEC-10, MET-13, TRD-11. (CHM-08 sits at the end of INQ-07, INQ-08, CHM-08; the first count used all ancestors instead of chain depth.) CHM-08 now carries its 64,000 cost; doc 38 and the content report are corrected.
- Costs: the whole E6 age lists at **1,875,200** (3.83 times E5's 489,830 at base 7,300), the 14-project candidate plus backfill at 761,462; the backfill debt is 32 earlier projects, 89,462 at the EraLag discount.
- Pacing from the live E5 rate (a growth rate of 4 to 8 percent a minute, and what E5 seats are assumed to have spent): E6's whole age lands at **18 to 30 minutes if E5 seats researched the whole age** and **32 to 57 if they researched only the critical set**. The era base that lands E6 at 25 minutes is about 23,000 to 62,000 against the typed 32,000. **The 32,000 base is plausible but unfit**; the uncertainty is the E5 spend and the growth rate, not E6's own costs. Refit on the first live E6 run, as E5's was.

**Fuel (D-T3).**
- Coke smelting removes about **86% of the wood** the E5 furnace still burns for iron (12.6 to 1.75 L per 100 adults a season at demand x1; at x4 demand, 43 coppiced forest tiles saved per 100 adults). Refining and carburizing charcoal stay, so iron never becomes wood-free.
- Coal is cheap in labor: at E5-sized iron demand it needs 0.2 to 2.6 miners per 100 adults. Its cost is geographic (seeing the seam, hauling it), which the WP-E6-3 audit and the D13 fix address.

**Power (D-T4).** The first-draft engine (6 power units on 3 L coal per unit-season, 2 engineers) **loses labor at every town size**: the crew and the coal cost more than the x2 factory output frees (300 adults: 22.5 freed, 16 engineers and 31 miners paid, net -24.6). The break-even needs about 7.5 units per engine at 1 L coal per unit, or 5 units at 0.25. A draft of **10 units on 1 L coal per unit** is labor-positive (net +2 at 300 adults), a modest lever that grows with the town. NRG-06 and the atmospheric-engine registry text now carry that draft.

**Combat (D-T7).**
- A cannon counts as two engines, so one cannon breaches a bare wall in a season and a gatehouse tower in two; three cannons breach a tower in one. A bastion that counts as a wall of 6.75 to 9.0 holds a three-cannon train for 1 to 2 more seasons, which is what keeps relief and starvation as decisions.
- The storm crossover is a defenders' multiplier of **1.9**. The E5 concentric castle (1.875) sits just under it; the same castle behind a tower wall that keeps 30% against powder is already past it. A bastion that multiplies defenders by 1.5 or more *on top* of a castle closes the storm for good. DEF-10 is therefore **retyped as an alternative to the concentric step** (castle 1.25 x 1.5 = 1.875), with its real work on the integrity track. The first typed value (x2.0) was too strong.
- A drilled national levy (WAR-13, x1.5) needs about 38 iron levies to beat 20 shielded steel professionals against 53 undrilled: a third fewer, which is the intent.

**Food, health, labor (D-T8, D-T9).**
- **Four-course rotation cannot physically deliver x1.5 over three-field.** Only half the land bears grain, so the gain must come from manure and fed livestock; across plausible manure, fodder and conversion values the result is x0.9 to x1.3, and x1.5 needs an implausible conversion of 0.5. AGR-12 is **retyped to x1.2 on field output**, with HUS-11's stall-fed herds supplying the rest. Stacking a free x1.5 on AGR-11 would be x2.25 over the two-field baseline.
- Epidemics: the E6 fixture gives **3.6 per mille per year** against 09 §10.6's 3.8 (5% under), rising to 11.3 at crowding pressure 3 (factories and ports) with neither hospital nor quarantine; hospital plus quarantine plus MED-09 bring it to 6.4. That pull is what makes MED-07/08/09 a real choice.
- Labor: the E6 institutions want about 35 adults per 100 (factory hands 15, army 10, E5 institutions 7.5, crews 3) against about 26 freed by machines, the collar and enclosure. The factory workforce cannot be filled by freed labor alone; it must come from town growth or from the army, and an unstaffed factory is idle and shown so (D-T8).

## Changes this made to the draft rows

All are draft (P) values; the pack golden was refreshed.

| Row | Was | Now | Why |
|---|---|---|---|
| CHM-08 | no doubling | cost 64,000 | the repo's depth rule makes it late |
| DEF-10 | defense x2.0 and a halved penalty | defense x1.5 as an alternative to the concentric step; bastion counts as a 6.75 to 9.0 wall on the integrity track | x2.0 closed the storm |
| AGR-12 | field_output x1.5 | x1.2 (HUS-11 carries the rest) | physical decomposition |
| NRG-06 and `atmospheric_engine` | engine with no size | 10 power units on 1 L coal per unit-season, 2 engineers | first-draft engine lost labor at every size |

## What is still unfit

All of it. No live E6 trajectory; the pacing anchor is E5's still-moving holdout and a carried 6% growth rate; no coal, steam, ocean, bastion or factory implementation to test; the engine's pump draw, heat-wood demand and colony and ocean numbers are open inputs; credit (D12) is undecided. Re-run the four scripts after each E5 rebase and after each E6 system lands.
