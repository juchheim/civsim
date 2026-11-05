# M12 WP-E5-1: E5 oracle screens (2026-11-05)

**Status:** fixture and accounting screens only, the E5 counterpart of [m11-wp2-oracle-fit](m11-wp2-oracle-fit.md). Nothing here fits live E5 (none exists), approves a band, promotes content or changes E0–E4. All values marked P in the scripts are hypotheses. Written against `main` at `6d6df44`; the Iron chapter was still being balanced, so every Iron-derived input is a placeholder to re-anchor.

## Reproducibility

Four additive scripts in `docs/MVP/balance/tools/`; each has a `--check` that passed on this baseline. They need only Python 3 and read the catalog YAML, `combat_e3.py` and the E4 oracles.

```bash
for f in research power combat economy; do python3 docs/MVP/balance/tools/e5_${f}_oracle.py --check; done
python3 docs/MVP/balance/tools/e5_research_oracle.py     # reports; drop --check
```

| Script | Screens | Targets in [27 §7](../27-faith-and-fief-age.md) |
|---|---|---|
| `e5_research_oracle.py` | KEY-6 closure, candidate cost, backfill debt, required Insight, Insight-project budget | F-T1 |
| `e5_power_oracle.py` | Power-unit ledger, textile kits, blast furnace against the forest | F-T2, F-T3 |
| `e5_combat_oracle.py` | Cavalry and crossbow crossovers, breach time with cannon, storm against a castle, powder and saltpeter access | F-T5 |
| `e5_economy_oracle.py` | Three-field decomposition, claim upkeep, epidemic arithmetic, staffing pool | F-T4, F-T6, F-T7 |

## What the screens say

**Research (F-T1).**
- The catalog reconciles: 49 regular (Craft 18, Kin 13, Reach 18) plus KEY-6, and the KEY-6 critical path is the 12 projects in 27 §3.2 (8 Reach, 4 Kin, **0 Craft**). Four free picks.
- The 16-project candidate plus KEY-6 costs **120,600** list (E4's 21 + KEY-5 candidate is 32,280, so 3.74×). The 23 earlier-era prerequisites KEY-6 needs beyond KEY-5's own closure add up to about **11,240** more at the EraLag discount, an upper bound because a civilization may have done some of them already. Total **about 131,800**.
- Spread over 16.75 to 18.25 minutes that needs **7,200 to 7,900 Insight/min** on average, about **4.0×** what the E4 candidate needs over its model window. The live E4 anchor (about 20 minutes for 32,280) is about 1,600 Insight/min, so **E5 needs about 4.7× the live E4 average rate to land at 17.5 minutes**. If the rate grows only 3× (roughly the model's population growth), E5 takes about **27 minutes**; at 2×, 41.
- The E5 Insight projects (COM-09, COM-10, NUM-09, LRN-08 are all on the critical path) add at most **about 23 to 50%** to the E4 Insight stack, and only for staffed scholars. They help the tail of the era, they cannot carry it.
- The 09 §11.2 table's absolute Insight level is stale (it predates the current EraBase); only its ratios are used.

**Power (F-T2).** At full coverage the machines free about **8.3%** of adult labor (grind ×5, saw ×4, mill-driven crafts ×2), the textile kits another **4.8%**, the horse collar about 5% more. Because most towns need under 5 power units, one wheel covers a small town and the ledger matters only for towns above about 300 adults (one wheel there frees 6.2% against the naive 8.3%). Mills are a modest lever, not a transformation.

**Blast furnace (F-T3).** The result that matters: at the **bloomery's fuel per litre, ×4 iron is a disaster**. Iron kits alone need 11.3 coppiced forest tiles per 100 adults (T12) or 22.3 (T11); with cast iron and the E5 demand response, 25.7 at the same fuel per litre and **51 to 103 tiles** if iron use doubles to quadruples. Anything beyond about 14 to 30 tiles per town is beyond the forests the Iron fuel check found. The furnace saves forest only if it uses at most about half the bloomery's charcoal per litre **and** iron use grows by less than about 1.7×. This is the I-T6 problem again, one step harder.

**Combat (F-T5).** Fights are decided by total strength, so the useful numbers are crossovers: a heavy cavalryman is worth two steel infantry, and 29 cavalry beat 40 shielded infantry. A crossbow levy beats 20 steel infantry at 28 men if bolts ignore shields and 31 if shields still counted, so "pierce armor" is a small effect (about a tenth). On the live integrity track one engine breaches a bare wall in 2 seasons and a tower in 3. If the catalog's "walls −70%" is read as the wall counting for 30% against cannon, **one cannon breaches any wall in one season** and the engine and investment decisions vanish; if a cannon does two engines' damage, a tower still costs a second season. After a breach, the storm against a garrisoned castle flips from attacker to defender at a castle multiplier of about **1.9**: reading "castles ×2" as 1.25 × 2 = 2.5 closes the storm entirely. Saltpeter in own territory is a coin flip at about 600 land tiles (59%) and about a third at 300, so powder depends on trade or expansion for most starts.

**Food, upkeep, epidemics, staffing (F-T4, F-T6, F-T7).**
- Three-field rotation reproduces the catalog's +50% only as a physical split: cropped share 1/2 to 2/3 (×1.333) with a legume yield ×1.125. The food step for a typical mix is **1.2 to 1.45**.
- Claim upkeep (confirmed D4): TER-10 cuts it 30%, so the project is worth only 1.2 to 3.6 points of farm output across 4 to 12%. A first fit of about **10%** makes it worth about 3 points, and the grain feeds the non-farming staff (about 11.5% of people at a 1.3 surplus), so mass is conserved.
- The epidemic fixture gives **2.4 per mille per year** against 09 §10.6's 3.0 (inside the check band, 21% under); hospitals plus quarantine cut it about 40%.
- Staffing: new institutions want about **17.5 adults per 100** at a 10% army (27.5 at 20%) and the freed pool is about **18.1** at full coverage, so E5 institutions fit only with almost no slack, and not at all on a war footing.

## Decisions this suggests (for you, plain English)

1. **The research pace is the biggest risk.** E5's cost is about 4× E4's but nothing in the model makes the research rate rise 4× on its own. Before E5 content exists, re-fit the E5 era base (or the cap) against the live E4 rate once the other session settles E4. Recommended: do nothing now, re-run `e5_research_oracle.py` after the E4 balance.
2. **Blast furnace fuel.** Recommend writing the furnace at about half the bloomery's charcoal per litre and treating cast iron demand as a cap (a stated number of litres per 100 adults), not an open demand. Otherwise the second fuel crunch breaks forests in every start.
3. **Cannon strength.** Recommend "a cannon does two engines' damage" (a tower still costs a second season), not "−70%" read literally.
4. **Castle multiplier.** Recommend the E5 castle bonus stays below about 1.9 in total (the storm stays possible), for example E4's 1.25 plus a smaller concentric step.
5. **Claim upkeep size.** Recommend a first fit near 10% of farm output, delivered as grain to the non-farming staff.

None of these is built or approved; they are inputs to WP-E5-2 (typing the rows) and the later fitting.

## What is still unfit

All of it. Specifically: no live E5 trajectory, no measured Insight growth beyond the single live E4 anchor, no mill, furnace, cannon or monastery implementation to test, no real saltpeter, river or horse geography (WP-E5-3), and the Iron inputs (E4 era base, fuel regrowth, castle ×1.25, engine constants) may change under the other session's balancing. Re-run the four scripts after each E4 rebase.
