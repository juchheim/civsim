# M13 WP-E6-7 slice 3: the glass-and-instruments chain (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted. Gives the goods of CHM-08, CER-11, MEC-09, WAY-10 and INQ-07 a working chain: the registry's recipes now run, the planner wants the outputs, and two of the goods now gate effects that earlier slices gave to the projects alone. Everything tests `DiscoveryChain`; the goods live in the Discovery goods array (written to Discovery snapshots only), so Bronze, Iron and Fief bytes, hashes and snapshots are unchanged and the content-pack golden is untouched. Numbers are the registry's and the rows'.

## What the slice does

| Part | How |
|---|---|
| **Six goods** | `G-ALKALI`, `G-ACID`, `G-PLATEGLASS`, `G-PARTS`, `G-CHRONOMETER`, `G-TELESCOPE` appended to the Discovery goods after coal and coke. The six registry recipes (`make_alkali`, `make_acid`, `make_plate_glass`, `make_parts`, `make_chronometer`, `make_telescope`) compile in a Discovery match and stay off in every other chapter. |
| **Demand** | A town of 50 with CHM-08 wants two alkali. Once MEC-09 stands, a town of 50 wants **one chronometer** (WAY-10) and **one telescope** (INQ-07 and CER-11) **until the realm holds one**; the planner's back-chaining then asks for the parts, plate glass, alkali, steel and glass they need, and a town whose inputs are missing finds the plan infeasible and spends nothing. |
| **WAY-10 chronometer** | The loss cut to three tenths now needs **a chronometer in store in one of the civilization's towns**; without it the voyage risk is the plain one. The row's navigator is not modelled (no new role): the instrument carries the benefit. |
| **INQ-07 telescope** | The 5% safer sea routes now need **a telescope in store**. The row's tower-sight x2 has no consumer (the sim has no tower-sight model), so it stays prose. |
| **CHM-08 alkali** | While a town holds alkali (one litre or more), its **soap** and **glass** (and blown glass) come out at the row's `soap_output` and `glass_output` (x2, scaled by adoption); the alkali is held, not used up. Plate glass is what consumes it. |

## What natural play shows

`discovery-match --seed 20261019 --minutes 130 --grant-e5 --grant-e6` (the harness now prints the goods and who holds which instrument; `CIVSIM_CHAINDIAG=1` prints which towns can run each recipe and what they hold):

- All four civilizations still reach KEY-7 at the same seasons.
- **Three of four civilizations hold a chronometer and two hold a telescope.** Plate glass and parts are made and used up as intermediates (stock near zero); alkali stands at 10 to 14 in total.
- **The binding input is salt.** Soda alkali is salt plus lime, and only 2 to 4 towns per civilization hold any salt, so a civilization with no salt in reach makes no alkali, no plate glass and so no instruments (the fourth civilization here). That is a geography consequence of the registry recipe, not a bug; if it should not cost a civilization WAY-10's and INQ-07's benefit, the lever is an alternative alkali input (an ash-lye recipe) in the registry, not the planner.

## Design choices worth knowing

1. **One want per realm, in every town of size.** The first attempt wanted an instrument at each harbor town (and the capital), which left two of four civilizations with none: the harbor towns often lack the salt, the forge or the smelter the chain needs. Wanting it until the realm holds one, in every town of 50, lets the towns that can make it do so; the cost is a few towns making one in the same season, bounded by how many hold salt.
2. **"Held" means in the realm's stores**, not at a harbor: the sim carries no ship cargo, and the earlier harbor test starved the effect.
3. **Acid has no consumer** in the sim (the row's "bleach cloth faster" is prose) and is never wanted; the recipe exists and stays dormant.
4. **Left for later:** the Renaissance Palace (BLD-13, needs plate glass in the build materials), the greenhouse (CER-11's winter food with a staffed garden, no number given), the parts' survey and textile uses (MEC-09), and the navigator role.

## Tests

`DiscoveryInstrumentsTests` (9): the six recipes exist in Discovery and not in Fief; an instrument counts only while one is in store in the realm; held alkali multiplies soap and glass only with CHM-08. Two earlier tests were updated to the new rule: the WAY-10 cut in `DiscoveryOceanTests` and the INQ-07 safer routes in `DiscoveryMindTests` now need the instrument.

## Checks

Full suite 1,628 passed with the 5 known failures; toy, research and politics goldens unchanged; `validate --strict` clean.
