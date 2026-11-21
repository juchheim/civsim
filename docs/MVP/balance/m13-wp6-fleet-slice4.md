# M13 WP-E6-6 slice 4: HNT-12 deep-sea fishing fleets (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted (on top of [slice 3](m13-wp6-galleon-slice3.md)). Discovery chapter only: banks, the fleet rate and the fleet reach all test `DiscoveryChain`, so Bronze, Iron and Fief fish stocks, hashes and snapshots are unchanged and nothing new is stored (the banks live in the existing per-tile fish array). All numbers are provisional.

## What the slice does

| Part | How |
|---|---|
| **Offshore banks** | The world's fish live only in rivers, lakes and water within two tiles of land, so open water has none to take. In a Discovery match every other water tile regrows fish like a river does: **4.8 a year, up to a stock of 7.2**. Only a fleet can reach them. |
| **The fleet** | A town with a harbor, of a civilization with HNT-12, fishes water **two to three tiles out** (the edge of a settlement's range) at **three times the labor rate** (the row's offshore_fish_yield x3; the coast's boats give x1.5). It draws on the same finite stock as every other fisher. Shore fishers keep their old reach. |
| **Tools** | The fleet's labor is still limited by the fishing tools and the planner's fish work, as any fishing is; there is no separate ship good. |

## Design choices worth knowing

1. **The rate alone would do nothing.** Fish are a finite stock, so a x3 labor rate only drains a bank faster. The banks are the new food: they are what turns "x3" into more food, and they are why the slice touches the regrowth loop (a fish-regrowth hook and a cap, Discovery only).
2. **Banks are sized like a river tile** (4.8 a year) so a harbor town's food gain is bounded by the same sea the rest of the economy fishes, not a new free source. Pacing may want a different number.
3. **Range is three tiles.** A settlement's range is a radius of 3, so a fleet cannot sail farther than that without a larger range. The row's x3 and the "fleets are away in season" text are carried by the rate and the finite bank; no seasonal absence is modeled.
4. **G-COD and the salt_cod recipe are not built.** Cod is a trade good; it belongs with sugar (FOD-10) in a trade-good slice, once the Discovery goods layer takes more goods.
5. **Content text:** HNT-12 gained a rule line stating the harbor, the x3, the reach and the banks; the content-pack golden was refreshed.
6. **The starts are inland**, so no start ever fishes offshore until a daughter town is founded on a coast; a harbor town on a coast is the case this slice serves.

## Tests (3 in `DiscoveryFleetTests`, all pass)

A harbor town with HNT-12 reaches water the shore fishers cannot and fishes it at x3 (the plain rate before); a harbor alone sends no fleet; a fleet needs the project and does nothing in Fief; banks fill to the 7.2 cap in Discovery and do not exist in Fief. The tests move a start settlement to a coast with a fixture, because the starts are inland.

## Not done

1. A natural-play measurement of how much food a fleet adds and how many towns found a harbor on a coast (WP-E6-12).
2. G-COD, salt_cod, G-CANE and G-SUGAR (trade goods); FOR-10 Columbian Exchange; DIP-09 embassies (with WP-E6-7's diffusion work); TRD-10/11 wait for D12.
