# M11 WP6 — Iron metal ledger

Records slices 1 to 4 of WP6 (forging, carbon and steel, tool wear, fuel, transport; [25 §4](../25-iron-age.md), [26 §2.3](../26-e4-iron-specs.md)) and what the later slices still owe. Nothing here is a balance claim: every quantity is the provisional number from 26, and no live planner uses the recipes yet.

## Slice 1: the ledger

- **Where the Iron goods live.** `G-WROUGHT`, `G-STEEL`, `G-TOOL11`, `G-TOOL12`, `G-ARMS3` and `G-ARMS4` are stocked in `MetalState.IronGoods`, a separate array allocated on first use. They are *not* appended to `MetalGood`: the Bronze checksum and snapshot hash and write the whole `MetalState.Goods` array, so appending would change every Bronze checksum.
- **Slots.** Iron goods take slots after the E3 and Bronze metal goods (`IronSlotBase`), so every existing slot keeps its number. `Stock`/`AddStock`/`SlotOf` route them.
- **Recipes.** Any recipe that touches an Iron good compiles into `_ironRecipes`, not `_compiledRecipes`, so the Bronze planner (which indexes plan arrays by recipe and slot) never sees them. They run only through `RunIronRecipe`, which reuses `RunRecipe`: the same scarcest-input and labor limits, inputs debited before the output is credited.
- **Gating.** `LivingEconomyModel.IronChain` (set by `MatchModel` from the chapter rules). Off, the recipes cannot run, nothing Iron is hashed or written, and E4 project completions are not recorded. On, `ProjectCeiling` is one era above `MaxEra` so MET-09 and MET-10 are recorded; the era clamp stays at 3 (see the WP5 audit note).
- **State.** `HashIron`, `WriteIron` and `ReadIron` add a tagged section only when `IronChain` is on. A captured or destroyed settlement clears its Iron goods with the rest of its metal.

## Slice 2: planner wants and labor

- **Merged recipe list.** In the Iron chapter the planner's recipe list (`_compiledRecipes`) is the content-order merge of the Bronze and Iron recipes (`_allRecipes`), swapped in by the `IronChain` init accessor. Bronze keeps its own list, so its plan arrays and recipe indices are unchanged. Plan arrays are sized `PlanSlots` (the Bronze slots, plus the six Iron goods in the Iron chapter).
- **The want.** `IronKitWant` (called from `PrimaryWants`) asks for one T11 kit per adult who holds none, in a settlement with a forge that knows MET-09. It rides the existing machinery: kit ← wrought iron ← bloom ← ore, with charcoal and wood, under the same primary chain labor cap as the Bronze kits.
- **Observed (one 60-person fixture, 60 ore and 40 charcoal in store, no kit use yet):** about 3 kits a season, 15 kits after five seasons, stopping when the stored charcoal ran out. The same fixture in the Bronze chapter makes no Iron goods. This shows the chain runs and is labor-bounded; it is not a fit, and the rates are the provisional numbers from 26.
- **Idle kits.** Nothing equips or wears a T11 kit yet, so kits accumulate in store up to one per adult and then the want stops. That is bounded, and it is the reason slice 3 (kit use and wear) comes next.
- **Tests.** The planner makes kits and every litre of ore is accounted for (ore consumed = 4 × bloom-equivalent in iron, wrought and kits); a Bronze settlement with the same stock makes none; an Iron economy restored from a snapshot continues to the same checksum.

## Slice 3: kit use and wear

- **Tiers.** Tools T11 (24 ws) and T12 (48 ws) were already parsed from `tools.yaml`; the tier table now covers 8 to 12, `Rank` gives them 4 and 5, and an Iron kit chops at MET-09's rung 7. Their `mine`, `quarry`, `harvest_labor` and `timber_build_labor` mods apply once per equipped kit through the existing `KitMod`. A figure's `Kit` byte may be 11 or 12 only in the Iron chapter (snapshot validation).
- **Issue.** `IssueIronKits` runs first in `WearAndIssueKits` when the chapter is on: kitless adults first, then worn kits, then early upgrades, each getting the best Iron tier in store that beats what they carry. The kit moves from store to figure, so it exists once. The Bronze hand-out then serves whoever is left, so a civilization with no iron still equips bronze kits.
- **Refund.** Retiring an Iron kit early returns nothing (26 §2.2). A Bronze kit upgraded to Iron is still remelted under the existing E3 rule.
- **Wear.** Unchanged and shared with Bronze: linear quality from 1.2 to 0.7 at replacement and on to 0.5 at the break, scaled by the tier's service life. T12 is therefore exactly twice T11 across the whole curve, including the dormancy tail (tested: 24 vs 48 ws, and break-left doubled).
- **The want.** `IronKitWant` now counts adults with no iron kit or with an iron kit about to be replaced, less kits in store.
- **Observed, not a fit (60-person fixture, 200 ore and 120 charcoal in store, so not a sustainable route):** adults holding a T11 kit were 5, 10, 16, 24, 28 of 28 to 29 over the first five seasons, and 29 of 30 through season 14, with no kit worn out in that time. That is above the I-T2 target (70% within 12 seasons of a sustainable route) but the supply here was handed to the settlement, so it says nothing about access research, delivery or fuel, which I-T2 asks to be reported separately.

## Slice 4: iron camps (the trade good is not built)

- **Iron camps.** In the Iron chapter the planner opens a mining camp on a workable iron deposit on own land within mining reach, when it wants iron ore and no deposit in range supplies it. This is the same path copper, arsenical copper and tin already use. Three small changes carry it: an access bit `SourceIron` (64, set only when `IronChain`, so Bronze access masks and hashes never carry it), iron joining the ore loop in `OpenCampsIfNeeded`, and iron counting as obtainable in `FeasibleBatches` so the bloomery is planned and the want exists to open the camp. A camp's digging, hauling and crew rules are the generic ones. The Mine Decree already worked for iron (`OpenMiningCamp` is generic over ore).
- **Evidence.** In a fixture with no iron in range and one surface deposit four tiles out on own land, the Iron planner opens the camp, digs, hauls and makes T11 kits from it; the Bronze-chapter economy on the same world opens none. Bronze checksum and goldens unchanged.
- **What this settles from the WP4 audit.** The finding that "the planner never opens iron camps" is closed for the Iron chapter. Ownership is still required (`IsOwnTile`), so the audit's "only deposits nearer a rival" starts (step 4) stay unserved by camps.

## Iron trade good: not built, and why

An iron `TradeGood` is a Politics change, not an Economy one, and it is larger than it looks:

- `TradeGood` is `None..Luxury = 7, Count = 8`. Appending `Iron = 8` is allowed (append only), but the **embargo mask is a `byte`** with one bit per good (`1 << (int)good`), hashed and snapshotted as `byte[]`. Bit 8 does not fit. Widening it changes Bronze hash and snapshot bytes. Iron would have to be excluded from embargoes (so it could not be embargoed), or the mask widened for the Iron chapter only.
- `Count` bounds several range checks (`>= TradeGood.Count`), the compact argument packing and the belief and forecast code, all shared with Bronze.
- Nothing proposes an iron compact. `CouncilDecreePolicy` and the Strategist propose Tin compacts only. A route is useful only if the planner buys iron when it has no source and a partner sells a surplus, which is new AI and Council policy (WP8/WP9 scope), and needs contact, Relations ≥ 0 and a finite `Travel` (WP4 §7).
- So the 0.68% tail (272 starts, of which 64 to 80 have no capable partner) still has no working trade route. Expansion (camps on newly founded land) is the other half of the Q9 decision and is untested here.

## Evidence

- The T11 hand check in 26 §2.3 reproduces: one kit costs 0.75 G-IRONORE and 1.36875 G-CHAR (8.2125 G-WOOD at 6 per charcoal), with no bloom left over, so the bloomery charge is not counted twice. Labor excluding charcoal burning is 0.65 ws.
- A Bronze economy and an Iron economy given the same stock behave differently exactly as intended (tests in `IronChainTests`): Bronze refuses the recipe, Iron without MET-09 refuses it, Iron with MET-09 and a forge runs it.
- A Bronze economy's checksum and snapshot bytes are unchanged by stocking an Iron good.

## Not done (later WP6 slices)

1. **Planner wants beyond T11.** Steel (MET-10) and T12 kits, iron and steel arms (WP8), and sister-settlement sharing of Iron goods are not wanted yet. Mining iron ore for the chain uses the E3 deposit logic, so the WP4 camp gap still applies.
2. **Saw attachment and mixed-age wear distributions.** The WOD-08 saw on a T12 kit, and a live wear distribution over many seasons and settlements for I-T3, are not built or measured yet. Steel (MET-10) is not wanted by the planner, so T12 kits exist only from fixtures.
3. **Iron trade good.** From the [WP4 audit](m11-wp4-iron-geography.md): `TradeGood` has no iron, so the 0.68% tail has no working trade route yet. See the section above for what building it involves. Sister-settlement sharing of Iron goods and kits (`ShareMetals`, `SharedGoods`) is also unbuilt.
4. **Fuel (I-T6).** Sustainable metal must be ≤15% of civilian work with no mandatory clear-cut. The WP4 audit found 45.6% of starts cannot sustain 100 adults of T11 replacement on local wood regrowth alone.
5. **Transport and imports.** Shipping bloom, wrought iron and kits between settlements and across borders, with the captured-forge and imported-kit cases of B4-06.
6. **Fission.** The fission operating-margin projection copies only `Goods`; Iron goods follow whatever happens to the parent's `MetalState`, which is the same rule as E3 metal, but it is untested for Iron.
