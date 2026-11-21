# WP-E5 military production-to-use lifecycle trace

This candidate-worktree Harness diagnostic adds `--fief-arms-lifecycle-trace` to `fief-match` (or `iron-match --fief`). It reads the existing model once each season and emits first-seen milestones, annual per-settlement states, and annual civilization summaries. It changes no simulation state or gameplay values.

Coverage includes copper/bronze/iron/steel/crucible arms, hunting bows, crossbows and longbows, chariots, siege engines, cannon and powder. It records unlock research (including `MIN-03`, `HNT-06`, and `WAR-03`), recipe input availability, producer structure readiness, first positive and first whole local stock, first fighter-carried tier, fighter/soldier/warrior counts, chariot crew, and siege engine/cannon assignments. `HNT-06` unlocks hunting-bow production and can enable a bow carrier; `WAR-03` is separately reported as levy-bow readiness when paired with `HNT-06`, and is a strength improvement rather than a carrier gate. This separates fractional stock→whole stock→first carrier from the levy production milestone. `MIN-03` is reported alongside observed block input stock so quarry-tech gating can be separated from local material availability. Annual rows expose material stocks and local idle-arm overlap with fighters lacking metal weapons. Cannon and powder are tracked as local inventories; siege assignment is visible from active investments. The trace can be paired with `--fief-siege-trace` to measure cannon shots and powder consumption.

For arsenical bronze, state rows separate `arsenicSource` (a workable source deposit or camp) from `arsenicStockAccess` (ore or bronze in local/sister stores). Supply rows add arsenical ore mined from camps and transferred between settlements, alongside co-smelted bronze output, transfers, and stocks. The optional end-of-match `fief-smelt-labor` rows show the mine, charcoal, and smelt work requested and assigned. Charcoal and ore stock snapshots are pre-craft; in particular, charcoal made from cut or standing wood during the same season is not included in the stock reading.

Example:

```sh
civsim-harness fief-match --pack numeric-e5.pack --seed 123 --minutes 130 --fief-arms-lifecycle-trace
```

## Interpretation limits

"First stock" is the first observed positive local inventory, not the exact production tick. Producer-ready means the unlock and required structure are present; it does not prove an active planner job. One-batch input readiness compares local store against recipe inputs and does not replace actual planner plan/made accounting. The existing build outlook exposes blast-furnace and powder-mill queues; the earlier metal-forge construction queue has no public Harness readout, so its readiness begins when a forge is observed complete. The trace does not inspect the private Arsenal staging/reservation ledger, so cannon/powder local stock and active siege assignment are visible but the precise handoff between them requires the existing siege trace. `G-BOW` stock is treated as the bow-family output, though it can also serve hunting. State rows are annual and empty sites are omitted; milestone rows retain the first observed season. This is diagnostic exposure, with no acceptance thresholds.
