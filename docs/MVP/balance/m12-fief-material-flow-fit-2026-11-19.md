# Fief weapon input-flow fit — 2026-11-19

## Observed bottleneck

The 150-minute seed-124 arms lifecycle trace on the prior candidate ended with 5,255 of 7,595 fighters holding a modeled weapon kit (69.2%). Only 19 of 108 forge sites could meet one batch of the copper-arms recipe's inputs; the corresponding counts were 9/108 for arsenical-bronze arms, 1/108 for tin-bronze arms, 4/108 for iron arms, 7/108 for steel arms, and 12/108 for crucible-steel arms. Crossbows were input-ready at 66/108 forge sites. The civ-level ending inventories held roughly 13,000 hide, 2,465 wood, and 1,326 cord, but only about 40 copper, 11 arsenical bronze, 2 tin bronze, 4 wrought iron, 11 steel, and 13 crucible steel. This points to mineral extraction and conversion as the shared choke, rather than a lack of weapon assembly labor or common organic inputs.

The trace is one whole-match diagnostic seed, not an acceptance estimate. It also found that every civilization had not researched WAR-06, so it cannot establish the chariot input path; the absence of chariots in that run is a research-access result.

## Candidate changes

- Double the finite reserve on every already-placed metal and Fief mineral deposit again, from the previous 3× fit to 6× the mapped base quantity. No deposit nodes or locations are added.
- Raise the existing ore-mine action from 1.5 to 2.0 litres per productive work-season. This is a 33% increase over the previous candidate; paired with twice the remaining finite reserve, it still extends a deposit's extraction lifetime while increasing near-term supply.
- Raise copper, arsenical bronze, tin, and bloomery output from 0.75 to 1 litre per recipe batch, holding labor and raw inputs constant. This is 33% more intermediate metal per batch than the previous candidate, and 2× the original output, reducing ore and charcoal required per weapon input.
- Keep hide, leather, cord, wood, and recipe input quantities unchanged because their end-of-run stores do not identify them as the common choke.

These are draft fit hypotheses, not promoted E5 numeric rows. The matched natural-horizon rerun is recorded below.

## Same-seed rerun

With these changes, seed 124 at 150 minutes ended with 6,556 of 7,049 fighters holding a modeled weapon kit (93.0%), compared with 5,255/7,595 (69.2%) on the prior candidate. The armed count increased by 1,301; the fighter cohort and civ-by-civ distribution also changed, so this single match is directional evidence rather than a causal or population-weighted estimate. There were no chariots because none of the four civilizations researched WAR-06. No civilization reached WAR-11 in this run either, so it does not test crossbow/longbow manufacture. Keep those research-access gaps separate from the mineral-input fix.

## Forge-site intermediate follow-up

The later 80-season trace at forge site 29 (seed 125, seasons 520–599) showed that common assembly inputs were not the choke: arsenical bronze was below cast-arms demand in 41/79 seasons; hide/leather was never short and wood was short once. At the co-smelter, arsenical ore was below its planned input in 39/78 seasons and charcoal in 32/78. Across the traced window, burn-charcoal produced 35.67/47.11 planned batches (75.7%), arsenical co-smelt 19.51/30.26 (64.5%), and cast arsenical arms 175.83/301.16 (58.4%).

The Fief-only runtime fit now raises charcoal output by 50%, arsenical co-smelt output by 100%, wrought iron and steel by 25%, crucible steel by 33%, and bronze alloy output by 20%, without creating deposits. The matched seed-125–127, 150-minute cohort recorded 519 chariots versus 447 in the earlier material-flow cohort, and no post-MET-09 charcoal-fuel shortfall. However, weighted armed-fighter coverage was 65.6% (15,127/23,060), versus 68.7% (15,898/23,155) in that earlier cohort. Since other flow changes and resulting match trajectories differ, this is directional evidence, not a one-variable causal comparison; the arms-coverage goal is still open.

The trace also found 17/80 seasons below 0.80 food security; these seasons contained 8.59 work-seasons of planned military assembly for which the craft-work gate did not open. Overall, assembly reservation covered 88.4% of planned assembly labor. Further changes should separate food-security gating, raw ore/charcoal delivery, and the remaining assembly-work shortfall before raising intermediate yields again.

A one-seed sensitivity check raised existing mineral extraction by 25% in Fief, but seed 125 then ended at 64.7% armed (5,626/8,691) with 179 chariots, versus 65.7% (5,567/8,470) and 190 chariots in the three-seed run's seed 125. This did not demonstrate a gain, so the extra mining multiplier was removed; the sixfold finite reserve remains.
