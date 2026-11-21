# M13 WP-E6-6 slice 5: FOR-10 the Columbian Exchange (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted (on top of [slice 4](m13-wp6-fleet-slice4.md)). Discovery chapter only; the two new factors are exact ones (1.0) elsewhere, so Bronze, Iron and Fief bytes, hashes and snapshots are unchanged, and nothing new is stored. All numbers are provisional.

## What the slice does

| Part | How |
|---|---|
| **The condition** | A civilization is *in the exchange* when it has FOR-10 and has **met a civilization whose home landmass differs from its own** (home = the landmass of each civilization's oldest live settlement). The politics side answers a new `IFissionSiteAdvisor.OverseasExchange(civ)`, worked out once a season. It follows contact, never knowledge of the map: unseen continents are met by arriving there. |
| **The crops** | A settlement in the exchange whose own biome can grow an imported staple gets **x1.2 field yield** (the row's food_yield 1.2), folded into the existing per-settlement field-yield term `FiefFieldYield`. Maize grows on grassland, savanna, tropical forest, open woodland and temperate forest; the potato on taiga, tundra and mountain. Desert, ice, wetland, peaks and water take neither (the row's "only where it can grow the staple on its biome"). |
| **The fevers** | A civilization in the exchange takes **x1.15 on its endemic death hazard** (the row's "old-world diseases cross with the ships"), in the same hazard line the other endemic modifiers use. A cost on the researching civilization, paid for as long as the exchange lasts. |

## Design choices worth knowing

1. **Home landmass, not any settlement.** A colony on the far shore does not by itself open the exchange; meeting a civilization on another continent's home does. Otherwise a single island colony would trigger it against everyone.
2. **Both sides need FOR-10 to gain.** Each civilization's own project gates its own crops and fevers; a rival that did not research FOR-10 gets neither.
3. **Settlement biome, not each field's.** The row says "on its biome"; the site's biome is the cheapest honest reading and keeps the term per settlement like the other field-yield policies.
4. **The fever is a flat hazard factor, not an outbreak.** It reuses the continuous endemic budget; a discrete epidemic event from ships would be a new mechanism.

## Tests (4 in `DiscoveryExchangeTests`, all pass)

The exchange needs FOR-10 and contact across an ocean and is not opened by a neighbor on the same land; contact without FOR-10 changes nothing; an imported staple lifts a settlement on a suited biome x1.2, not on an unsuited one, and the fever factor is 1.15 inside the exchange and exactly 1 outside it; nothing happens in Fief.

## Not done

1. A natural-play measurement of how the AI uses the exchange and what the fever costs in deaths (WP-E6-12).
2. FOD-10 sugar and the G-COD and G-SUGAR trade goods; DIP-09 embassies (with WP-E6-7); TRD-10 and TRD-11 wait for D12.
