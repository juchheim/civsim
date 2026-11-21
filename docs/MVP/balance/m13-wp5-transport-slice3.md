# M13 WP-E6-5 slice 3: coke oven, navigation canal and turnpikes (2026-11-20)

**Status:** built and tested on `codex/e6-discovery`, uncommitted (it sits on top of the uncommitted [slice 2](m13-wp5-steam-slice2.md)). Closes the remaining simulation items of WP-E6-5 ([38 §9](../38-discovery-age.md)) except the Factory's use of the power ledger, which belongs to WP-E6-8 with FIB-12 and MEC-10, and the views (WP-E6-11). Everything is inert outside a Discovery match; Bronze, Iron and Fief bytes, hashes and snapshots are unchanged. All numbers are provisional.

## What the slice does

| Part | How |
|---|---|
| **Coke oven** (FIR-12) | New structure kind `coke_oven` (20 work-seasons, 12 block, 2 wood; the registry's fired brick is counted as block as the blast furnace's is, the clay is not charged). In a town with an oven `make_coke` takes **1.2 L of coal per litre of coke instead of 1.5, in half the labor** (P). The planner wants one where a furnace smelts on coke (`CokeSmeltingActive`); the hard gate is a town, FIR-12 and a coal supply. The kiln still cokes without it, so slice 1's chain is unchanged until an oven stands. |
| **Navigation canal** (TRN-11) | New structure kind `navigation_canal` (24 work-seasons, 12 wood; the registry's 2.0 labor and 1 timber a tile over a provisional 12-tile line; surveying shortens it). A town with a canal hauls its coal camps at **x10** (`bulk_haul_inland`), which replaces the cart's x5 and does not stack with it. It is open to a town with a coal camp at least 3 tiles out, one canal to a town. Coal only: grain and every other good keep their own haul, because only coal moves by camp in the sim. |
| **Turnpikes** (TRN-10) | `road_speed` x1.5 is read where the sim already uses road speed: a road-linked town's walk to its capital in the Cohesion strain, which TRN-07 shortens to two thirds, now shortens a further x1.5 (to four ninths, rounded up). Discovery only. The row's coach and toll text stays prose. |

## Design choices worth knowing

1. **The oven is an improvement, not a gate.** The registry already lists `make_coke` for `[kiln, coke_oven]`; making the oven mandatory would break slice 1's fixtures and push a second structure onto the coke-smelting path for little gain. It saves coal and labor instead, so building it has a reason.
2. **Canal benefit is small by design.** Camp hauls are already a small part of coal's cost at three to eight tiles (1/60 work-season per litre per tile, divided by the cart's 5). The canal's value shows in a larger coal economy and in the Chronicle; this slice only makes it exist, cost labor and cut haul labor. If E6 pacing wants coal to matter more, the lever is the camp reach, not the divisor.
3. **No new snapshot sections.** Both structures are new `IronStructure` kinds written only in Discovery snapshots (`SnapshotIronStructureCount`), beside the engine.
4. **Natural play dug the canal.** In the test fixture a town with an engine, a coal camp three tiles out and TRN-11 built the canal on its own within six seasons (the gate answered "already built" when the test looked).

## Tests (3 new in `DiscoverySteamTests`, 11 in the file, all pass)

The oven cokes with 1.2 L against the kiln's 1.5 in half the labor, is wanted only once a furnace smelts on coke, and never exists in Fief or without FIR-12; the canal needs TRN-11 and a camp three tiles out, hauls coal at x10 replacing (not stacking with) the cart's x5, is one to a town and never exists in Fief; turnpikes shorten a road-linked town's walk to four from six, leave an unlinked town alone and do nothing in Fief.

## Checks

See the commit message: toy golden, `validate --strict`, the research and politics finals (`cb6fd7b2b6ebfd06`, `5d3ebe260a0f55b5`) and the full suite after the last edit. The content-pack golden was refreshed for the changed registry and catalog text (the oven and canal effect lines and the TRN-10 note).

## Not done

1. **The Factory's use of the power ledger** (FIB-12, MEC-10): WP-E6-8.
2. **Views:** no card shows the oven, the canal, coal stock or the engines ([08 §13](../08-ui-and-world-display.md)); `SteamReadout` is the hook for WP-E6-11.
3. **Natural-play measurement** of coal, coke, steam and wood saved over a whole Discovery match (D-T3, D-T4): needs the promoted E6 pack (WP-E6-12).
