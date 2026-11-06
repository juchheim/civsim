# M11 I-T4 — matched E3 anchor diagnostic protocol

I-T4 in 25 §13 proposes FoodCap growth of 1.4–1.8× and increased city concentration without free food. The numeric band is not formally accepted. The report tool in `tools/it4_matched_anchor.py` deliberately reports ratios and physical-food residuals without labeling the band a pass or failure.

## Current evidence and limits

The fresh E3 anchor in [m11-wp2-e3-anchor.md](m11-wp2-e3-anchor.md) contains C1-E3 fixture FoodCap means of 193.3 and 173.5 for two seeds, paired to E2 means of 128.6 and 104.6. These are existing live-model fixture observations, not the five geographic E3 anchors I-T4 requires. The current `e4_economy_oracle.py` is explicit target arithmetic: its 100/80/120 cap and grain values are hand-selected, and its public-grain FoodCap multiplier does not establish a live effect or city response. It cannot satisfy I-T4.

## Required matched observations

Collect matched E3 and E4-mode observations for non-river, river, hill, inland and coastal starts. Keep seed, map, start tile, population, age composition, research state and observation horizon paired. Record per settlement: FoodCap, City-tier population and total population. Aggregate ratios within fixture and report distributions and qualifying sample counts; do not pool away a fixture miss. Exclude E4 effects until the owning civilization enters KEY-4. Include a fixture only when both E3 and E4 observations qualify.

For the physical ledger, record a common interval's beginning stocks, locally produced food, imports, consumption, exports/transfers, spoilage/loss and ending stocks. The check is `begin + production + imports - consumption - exports - loss = end`. Report transfer donor and receiver rows separately as well as the system total; verify donors retain their one-season reserve when public grain is active. Aqueducts, terraces and the public-grain target create neither food nor housing. Attribute additional production only to executed labor/jobs and physical inputs.

`tools/it4-matched-anchor-template.csv` is the input shape for the audit script. The script requires all five fixture labels, checks the stock equation with a scale-aware tolerance, and prints E4/E3 FoodCap and City-share changes. It does not simulate the economy, infer causal effects, enforce the proposed band, or claim a gate pass. Use actual matched runtime observations as inputs; synthetic values are not evidence.

## Open work

- Add or expose a deterministic runtime fixture runner that instantiates the five geographic pairs and reports pre/post KEY-4 values from the live E3 food, cap and settlement-tier equations.
- Add a per-good and transfer-level ledger over a multi-season window, including reserve checks and food loss; one-season arithmetic cannot reveal stock depletion or production-work displacement.
- Fit the fixture results and city concentration against the matched E3 anchor, then obtain explicit approval for any numeric band before classifying results.
- Check the result in whole-match mode for population concentration and labor diverted from defense/construction. This screen alone cannot establish demographic or match-level effects.
