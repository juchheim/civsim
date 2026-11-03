# M11 WP6 — Iron metal ledger

Records slice 1 of WP6 (forging, carbon and steel, tool wear, fuel, transport; [25 §4](../25-iron-age.md), [26 §2.3](../26-e4-iron-specs.md)) and what the later slices still owe. Nothing here is a balance claim: every quantity is the provisional number from 26, and no live planner uses the recipes yet.

## Slice 1: the ledger

- **Where the Iron goods live.** `G-WROUGHT`, `G-STEEL`, `G-TOOL11`, `G-TOOL12`, `G-ARMS3` and `G-ARMS4` are stocked in `MetalState.IronGoods`, a separate array allocated on first use. They are *not* appended to `MetalGood`: the Bronze checksum and snapshot hash and write the whole `MetalState.Goods` array, so appending would change every Bronze checksum.
- **Slots.** Iron goods take slots after the E3 and Bronze metal goods (`IronSlotBase`), so every existing slot keeps its number. `Stock`/`AddStock`/`SlotOf` route them.
- **Recipes.** Any recipe that touches an Iron good compiles into `_ironRecipes`, not `_compiledRecipes`, so the Bronze planner (which indexes plan arrays by recipe and slot) never sees them. They run only through `RunIronRecipe`, which reuses `RunRecipe`: the same scarcest-input and labor limits, inputs debited before the output is credited.
- **Gating.** `LivingEconomyModel.IronChain` (set by `MatchModel` from the chapter rules). Off, the recipes cannot run, nothing Iron is hashed or written, and E4 project completions are not recorded. On, `ProjectCeiling` is one era above `MaxEra` so MET-09 and MET-10 are recorded; the era clamp stays at 3 (see the WP5 audit note).
- **State.** `HashIron`, `WriteIron` and `ReadIron` add a tagged section only when `IronChain` is on. A captured or destroyed settlement clears its Iron goods with the rest of its metal.

## Evidence

- The T11 hand check in 26 §2.3 reproduces: one kit costs 0.75 G-IRONORE and 1.36875 G-CHAR (8.2125 G-WOOD at 6 per charcoal), with no bloom left over, so the bloomery charge is not counted twice. Labor excluding charcoal burning is 0.65 ws.
- A Bronze economy and an Iron economy given the same stock behave differently exactly as intended (tests in `IronChainTests`): Bronze refuses the recipe, Iron without MET-09 refuses it, Iron with MET-09 and a forge runs it.
- A Bronze economy's checksum and snapshot bytes are unchanged by stocking an Iron good.

## Not done (later WP6 slices)

1. **Planner wants and labor.** Nothing asks for wrought iron, steel or kits yet; the recipes are only reachable from fixtures. The planner (`PrimaryWants`/`SecondaryWants`) needs an Iron path that reserves labor and charcoal.
2. **Kit use and wear.** `FigureState.Kit` is a byte tier (8–10) with E3 service and quality rules. T11/T12 need their own `Rank`, service (24 and 48 ws), per-kit mods and mixed-age wear, and a T12 attachment for the saw. I-T2 and I-T3 measure this.
3. **Iron mining camps and an iron trade good.** From the [WP4 audit](m11-wp4-iron-geography.md): `MaintainCamps` opens camps only for copper, arsenical copper and tin, and `TradeGood` has no iron, so the 0.68% tail has no working trade or expansion route yet.
4. **Fuel (I-T6).** Sustainable metal must be ≤15% of civilian work with no mandatory clear-cut. The WP4 audit found 45.6% of starts cannot sustain 100 adults of T11 replacement on local wood regrowth alone.
5. **Transport and imports.** Shipping bloom, wrought iron and kits between settlements and across borders, with the captured-forge and imported-kit cases of B4-06.
6. **Fission.** The fission operating-margin projection copies only `Goods`; Iron goods follow whatever happens to the parent's `MetalState`, which is the same rule as E3 metal, but it is untested for Iron.
