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

## Tests
`IronChainTests`: a town with full flat fields terraces a hillside into a field and a Bronze town does not; no terrace without the research; an Iron economy with a terrace restores from a snapshot and continues to the same checksum; the scythe bonus appears only for an iron kit with AGR-09 in the Iron chapter; the plough rule lifts only heavy forest soil and never lowers a yield.

## Still to build in this group
Water and health: aqueduct and the water-limited city cap, baths and latrines, physicians and medical texts, windlass wells. Food: public grain supply, grafted orchards and the press (wine, oil), the water mill (grind x5). Cities and learning: academies, theatres, cranes, harbors, mechanical build-labor mods, fired brick and tile, glass. The checklist is `m11-e4-effects-ledger.md`.
