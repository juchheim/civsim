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
