# M13 WP-E6-5 slice 1: coal, coke and coke smelting (2026-11-20)

**Status:** built and tested on `codex/e6-discovery`. First simulation slice of the Discovery chapter ([38 §5.1](../38-discovery-age.md)). Everything is inert outside a Discovery match: Bronze, Iron and Fief bytes, hashes, planner behavior and goldens are unchanged. All numbers are the draft values of the E6 rows ([m13-wp1](m13-wp1-oracle-fit.md)); none is fitted.

## What the slice does

A Discovery town with MIN-09 digs coal from an own coal deposit, coke is made from it, and a town with a blast furnace and MET-13 smelts iron on coke with no charcoal.

| Part | How |
|---|---|
| **Goods layer** | `DiscoveryGood { Coal, Coke }` take slots after the levy-bow goods, in their own per-settlement array, their own hash section (only while something is held) and their own snapshot tail, all written only when `DiscoveryChain` is on. Recipes that touch these slots are disabled outside Discovery. New file `Economy/LivingEconomyModel.Coal.cs`; small additions to `Stock`, `AddStock`, `SlotOf`, `PlanSlots`, the snapshot, hash and clone sites. |
| **Coal mining** | A coal deposit is workable after MIN-09 in a Discovery match, with a finite reserve (6,000 L per tile, P) and the ordinary deep-seam rule (depth 2 needs MIN-04 and a mine head). The existing camp machinery digs it: coal is a want of the coking recipe, a camp opens on an own deposit in ore reach, and the crew is staffed from the parent town. |
| **Coking** | Recipe `make_coke` (1.5 coal to 1 coke) runs in the existing kiln. A dedicated coke oven structure is not built yet (it would need a new structure kind and a snapshot version); the registry says so. |
| **Coke smelting** | Where MET-13 is known, the town has a blast furnace, and coal or coke is to hand, the bloomery recipe stands aside and iron comes from `coke_smelt` (6 ore + 3 coke for 3 iron, no charcoal input). With no coal anywhere near the town keeps the Fief furnace unchanged. The planner's existing "first available producer" rule does the switching, so no planner rewrite was needed. |

## Tests (5, all pass)

Coal is dug only after MIN-09 and only in Discovery (a Fief town with the same deposit and research never touches it); coke smelting replaces the bloomery only with MET-13, a furnace and coal to hand (and coke in store alone is enough); a fixture town digs coal, cokes it and makes iron over 12 seasons, with the bloomery unavailable and the `coke_smelt` batch at 3 iron; coal and coke survive a snapshot restore and continuation to identical bytes; a Fief town with a coal deposit on its land plays identically to one without.

## Checks

- Toy golden passes; the research (12 min, grower) and politics (24 min) runs print exactly the `final` values they printed before this slice (`cb6fd7b2b6ebfd06`, `5d3ebe260a0f55b5`), so no existing mode moved.
- Full Release suite: **1,549 of 1,554**; the five failures are the ones `main` already has.
- Content-pack golden refreshed (the `make_coke` structure list and the coke-oven text).

## Not done in this slice

1. **FIR-11 coal firing** (halving heat wood where coal arrives): the heat-wood accounting is not yet read for coal.
2. **The atmospheric engine, pumping and flooding**: a new structure and a snapshot version; coal seams are not yet flooded, so deep coal digs at the ordinary deep-seam rate. Steam also needs its place in the shared power ledger ([m13-wp1](m13-wp1-oracle-fit.md) Part C).
3. **Roads and canals** (TRN-10/11): haul-cost changes and the canal structure.
4. **A dedicated coke oven** structure and its gate in the build planner.
5. **No natural-play measurement.** The fixture shows the chain running; whether natural play reaches coke smelting (and how much wood it saves, D-T3) needs a Discovery whole-match run, which needs the promoted E6 pack (WP-E6-12). The coal reserve, the crew sizes and the camp-opening appetite are first values.
