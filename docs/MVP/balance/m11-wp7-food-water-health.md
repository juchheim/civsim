# M11 WP7 — Food, water, health and cities (batch 1 notes)

Records what each piece does in the economy, the provisional numbers behind it, and what is still to build. Everything here runs only in the Iron chapter (`IronChain`); the Bronze economy, its checksums and its snapshots are unchanged. All numbers are provisional (**P**) until the oracle fit and the whole-match runs.

## Built

### Iron construction framework
- A separate labor bucket for Iron-chapter structures (`MetalPlan.IronBuildNeed`/`IronBuildAssigned`), taking at most 10% of a settlement's work beside the metal chain and civic works. It is a separate bucket rather than new `MetalBuild` kinds because the Bronze build code is spread over six places.
- State lives in `MetalState` outside the Bronze arrays: `IronWorks` (a count per `IronStructure`), the structure under construction with its progress, and the terraced tiles. It is hashed and saved only in the Iron section of the snapshot, cleared when a settlement is captured or destroyed, and copied on restore.
- A structure is chosen in `PrimaryWants` (before the wants are expanded) so the stone and bloom it needs are planned like any other want. Materials are paid at completion; labor accumulates up to the structure's cost and waits there if the materials are not yet in store. Nothing is paid before completion, so cancelling refunds nothing because nothing was taken.
- Adding a structure is now: an `IronStructure` value, a row in `IronSpecs` (unlock project, labor, block, ashlar, wood, bloom), a `IronBuildWanted` rule, and its completion effect.

### Terrace (AGR-08)
- 4 G-BLOCK + 2 G-WOOD and 8 work-seasons (26 §3.2). Wanted when the settlement has the research, at least 12 people, a Mountain tile in range that is no one's field or orchard, and its flat fields are full (its allowance is used or no flat tile is left).
- On completion the lowest-index free Mountain tile in range becomes a field. Terraces add plots to the allowance rather than taking one (`FieldLimit` rises by the terrace count). A terraced plot yields 2.0 (a flat tile yields 0.6 to 3.0 by biome), with built-up soil, no soil loss and no flooding. It creates no food by itself: it needs farm labor like any field.

### Iron scythe (AGR-09)
- A farmer with a T11 or T12 kit in a civilization that knows AGR-09 does a third more effective farm work (harvest-stage output x2 on a third of the work; the one-third split is provisional). It is extra work, so it raises a harvest only where the settlement was short of farm labor; it is added to, not folded into, the kit's own harvest-labor saving. No population-food grant.

### Mouldboard plough (AGR-10)
- A temperate-forest tile with a clay deposit is lifted to the world's best base yield (3.0) for a civilization that knows AGR-10. Nothing else changes (no universal bonus). The world has no separate "heavy clay" attribute, so the clay deposit flag stands in for heavy soil; that is an interpretation, and few tiles qualify.

### Water, aqueduct and baths (BLD-10, MED-06)
- **Water cap.** A town's water-limited size is 60 people plus 8 for each fresh-water tile in its range, at most 150 (provisional; the oracle screen used 80). It is one more term in the cap that makes a town bud (`min(food, housing, group, water)`), so a town short of water splits rather than crowding. It binds only in the Iron chapter after the civilization's own KEY-4; otherwise there is no water limit. It creates neither food nor housing.
- **Aqueduct (BLD-10).** 20 blocks, 8 ashlar, 5 wood, 40 work-seasons. Wanted by a town (not a village) that has no aqueduct, whose water cap is the tightest of its caps, and whose people have reached 85% of that cap. It raises the town's water cap by 40% and cuts its density-disease pressure to x0.85.
- **Bathhouse (MED-06).** 8 blocks, 4 wood, 20 work-seasons. Wanted by a town of 40 or more with the research and none built. Density-disease pressure x0.75; with an aqueduct the two multiply (0.6375). Staffing or upkeep is not modelled yet (26 says the sanitation must be "maintained").
- **Medical texts (MED-05).** The care effects already in play (remedies for outbreaks, wound care, midwives, sickness care) are strengthened by 15% of their reduction: 0.70 becomes 0.655, 0.60 becomes 0.54, with no compounding and never below zero. The physician role is not built.
- **A started structure survives a bad season.** A structure whose labor has begun is kept while it is still possible (research known, tile free, none built), even if the reason it was wanted lapses for a season; labor already spent is lost only when it becomes impossible.

### Public grain (FOD-09)
- Each season a town or city holds a reserve target of one ration per person, and grain moves to it from sister settlements in sharing reach, but only the grain a donor holds above its own one-season winter need, at most a quarter ration per person per season. Physical grain moves from one store to another, so none is created. The catalog's "+25% cap" is the outcome the oracle checks (I-T4), not a multiplier applied here, because 25 §5 forbids piling food-cap multipliers on top of the mechanisms.

### Construction research, crane and wells (MEC-04, MEC-06, BLD-11, STN-12, NUM-07)
- The general construction mods (`build_labor` from BLD-11 and MEC-04) now multiply the labor of Iron structures and civic builds; STN-12 (`stone_build_labor`) shortens stone works; surveying (NUM-07, efficiency 1.15) shortens fields, roads and aqueducts. Each factor is exactly 1 until its project is complete, so Bronze civic labor is unchanged.
- **Crane (MEC-06):** 8 wood and 2 bloom, 16 ws. A town builds one when it wants a large work (the aqueduct); it halves the labor of large works.
- **Windlass well (MEC-04):** 4 wood, 8 ws, at most two; wanted when water is the tightest cap before any aqueduct. Each counts as a fresh-water tile for the water cap.

### Grafted orchards (FOR-08)
- A mature orchard plot gives twice the fruit when grafted. A plot counts as grafted when a T11 or T12 kit holder in the settlement keeps it (two plots per blade, provisional). Fruit is the only output so far.

## Gaps found
- **The water mill (NRG-03) has nothing to multiply.** The catalog says "grind output x5", but the economy has no grinding labor or flour: the Quern is only a structure. The mill needs a decision on what grinding costs in this model before it can save any of it.
- **Wine and oil (FOR-08 with MEC-05)** need orchard crop types (grape, olive), the press structure and their goods; the economy's orchards produce one kind of fruit. Left for the culture and trade batch.
- **The physician role** is not a separate staffed role: in this economy a patient is treated only when a healer is assigned, so a treated patient already means staffed care, and MED-05's 15% applies to exactly those treatments.

## Tests
`IronChainTests`: the water cap exists only in the Iron chapter after KEY-4 and an aqueduct raises it 40%; aqueduct and bath factors multiply; medical texts strengthen care 15% and never below zero; a town with the research and people builds a bathhouse; a town with full flat fields terraces a hillside into a field and a Bronze town does not; no terrace without the research; an Iron economy with a terrace restores from a snapshot and continues to the same checksum; the scythe bonus appears only for an iron kit with AGR-09 in the Iron chapter; the plough rule lifts only heavy forest soil and never lowers a yield.

## Still to build in this group
Water and health: the water mill (needs a grinding model), wine and oil, upkeep for baths. Food: public grain supply, grafted orchards and the press (wine, oil), the water mill (grind x5). Cities and learning: academies, theatres, cranes, harbors, mechanical build-labor mods, fired brick and tile, glass. The checklist is `m11-e4-effects-ledger.md`.
