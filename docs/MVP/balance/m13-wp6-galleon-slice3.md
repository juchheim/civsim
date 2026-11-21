# M13 WP-E6-6 slice 3: SEA-13 galleons (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted (on top of [slice 2](m13-wp6-colony-slice2.md)). Discovery chapter only; Bronze, Iron and Fief bytes, hashes and snapshots are unchanged. The galleon is a new `IronStructure` kind written only in Discovery snapshots (like the engine, oven and canal), so no snapshot version bump. All numbers are provisional.

## What the slice does

| Part | How |
|---|---|
| **The ship** | New structure `galleon` (SEA-13, 30 work-seasons, 20 wood, 4 iron), one to a harbor, laid down in a town with a harbor and a crew of **six** fed soldiers in post (a galley needs four). Planner-wanted wherever the gate opens. |
| **Blockade** | A crewed galleon counts as a crewed galley (`CrewedGalley`), so it holds a station through the existing blockade rows. Its crew strength is the best six fighters (a galley's is four). When a blockade ship is wrecked or beaten (`LoseGalley`) the galleon goes first, then the galley. |
| **Out of the defense pool** | While a crewed galleon holds a station, its six crew soldiers are skipped by `FightersInto` for the home harbor ("a fleet at sea is out of the defense pool"). The crew still count when the blockade measures its own strength. A galley's crew stays counted, exactly as before. |
| **Convoy escort** | A caravan leaving a harbor that holds a crewed galleon keeps six tenths of its shipwreck risk (`convoy_loss` x0.6). |

## Design choices worth knowing

1. **Out of the pool only on station.** A galleon idling in port still defends the harbor: the cost of a war fleet is paid when it sails, not when it is built.
2. **One path for both ships.** The galleon reuses the galley's blockade, wreck and relief code. The relief fight draws on `CrewStrength`, so a galleon's six-soldier crew makes it a stronger contest than a galley's four without a second combat rule.
3. **No cannon ledger yet.** The row says the ship carries cannon with powder per shot. The sim has no ship-versus-ship powder spend; the six-soldier crew stands in for the broadside. Powder per shot belongs with the WP-E6-9 war work (WAR-13, star forts), where cannon and powder are consumed goods.
4. **Interface unchanged.** Convoy risk is applied inside the existing expedition turn-back roll; `ConvoyRisk` is a one-line helper.

## Tests (5 in `DiscoveryGalleonTests`, all pass)

The galleon gate needs the crew and a harbor, allows one to a harbor and does not exist in Fief; a crewed galleon blockades and is the ship that is lost; its crew leaves the defense pool on station and returns when the crew is released; a galley's crew stays in the pool; the convoy cut is 300 to 180 permille with a crewed galleon and nothing in Fief or without one. The colony slice's blockade strain (slice 2) now has a ship that can set it up; an end-to-end test of that strain is still not written.

## Not done in WP-E6-6

1. FOR-10 Columbian Exchange, FOD-10 sugar, HNT-12 deep-sea fishing, DIP-09 resident embassies.
2. TRD-10 and TRD-11 (joint-stock voyages, insurance): wait for decision D12.
3. Powder per shot and ship-versus-ship cannon (WP-E6-9).
