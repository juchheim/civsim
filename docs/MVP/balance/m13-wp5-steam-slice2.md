# M13 WP-E6-5 slice 2: the atmospheric engine, pumping and coal firing (2026-11-20)

**Status:** built and tested on `codex/e6-discovery`, uncommitted. Second simulation slice of the Discovery chapter ([38 §5.1](../38-discovery-age.md)), after [slice 1](m13-wp5-coal-slice1.md). Everything is inert outside a Discovery match: Bronze, Iron and Fief bytes, hashes, snapshots, planner behavior and goldens are unchanged. All numbers are the draft values of the E6 rows and of [m13-wp1](m13-wp1-oracle-fit.md) Part C; none is fitted.

## What the slice does

| Part | How |
|---|---|
| **The engine** | `IronStructure.AtmosphericEngine` (NRG-06), appended after the Arsenal. Cost 36 work-seasons, 8 block, 6 wood and 4 iron (the registry's 4 cast iron, counted as bloom iron as the blast furnace's brick is counted as block). Gate: Discovery only, NRG-06 learned, a town of at least 80 people, coal in store or an own seam in reach, at most two per town. The build planner wants the first one where a deep coal seam floods or the town's power budget falls short of its work, and a second only in a town of 300 where the shortfall remains. |
| **Staffing** | New role `engineer` (`Role` 12). A town with engines and a coal supply staffs two engineers per engine from its adults, through the same `StaffRoles` pass as priests and master masons (at least four other adults stay at home). With no coal obtainable nobody is taken off other work. |
| **Burning** | Each season, after the roles and before the camps work, every staffed engine burns 1 L of coal per power unit for up to 10 units. No crew or no coal leaves it idle; part of the coal gives part of the power. The units, and whether the engine pumped, are stored on the settlement (hashed and written to the snapshot only in Discovery, in their own section after the coal section). |
| **Power ledger** | The units join the E5 ledger's available hours beside the water mill's 5 and the windmill's 3.2: one ledger, plain addition, no multiplier. A fired engine also counts as a mill for the bellows, the powered saw and the hammer (`MillFactor`), so a town with no river can have powered work, and it makes a blast furnace workable. Grain milling still needs a wheel or sail: an engine alone does not grind. |
| **Pumping and flooding** | A coal seam at depth tier 3 floods: it yields half (`FLOODED_FACTOR`, oracle) until a fired engine in the same town pumps it. Pumping draws a fixed 2 units from the engine's 10 before the rest goes to the ledger (provisional: the oracle left the draw open). Tier-2 seams stay dry. |
| **Coal firing (FIR-11)** | A town that holds coal halves the wood it burns for hearths and kilns (building wood and bakeries are not touched). One litre of coal replaces four of wood (oracle). All or nothing: with too little coal in store the town burns wood as before. The coal charge is taken in the season's fuel step, and the planner wants two seasons of engine and heating coal in store, which is what opens a coal camp. |

## Design choices worth knowing

1. **No snapshot version bump and no Fief change.** The engine kind is only written when `DiscoveryChain` is on (`SnapshotIronStructureCount`); a Fief snapshot still writes exactly the kinds it wrote before, and a Discovery snapshot was never loadable by anything older. The Iron works array is now always read at full length so gates can index every kind.
2. **Flooding starts at tier 3, not 2.** The oracle's constant says "a depth 2-3 seam" floods. Coal is generated at depth 2 or 3; using tier 3 makes about half the seams flood, which keeps the pump worth building without halving every coal camp from MIN-09 until NRG-06. Say if you want both tiers.
3. **Staffing is real labor.** Two engineers per engine is the whole cost the oracle priced (Part C: ten units on one litre of coal is labor-positive at every town size, six units on three litres is not), so nothing else is added.
4. **Heat relief is derived, not stored.** `E3FuelNeed` is a pure function the planner calls many times; the relief is a function of the coal in store, and the season's fuel step charges the coal. Outside Discovery the relief is exactly zero and the demand is the same sum as before.

## Tests (8 new in `DiscoverySteamTests`, all pass)

The gate needs NRG-06, a town, coal and a Discovery match (a Fief match with the same research never builds one; two to a town); a staffed engine burns 1 L per unit and an unstaffed or coal-less one idles, a part-fed one gives part power; steam adds to the ledger (10 alone, 15 beside a water mill) and needs no river; a deep seam floods to half until a fired engine pumps it, the engine staffs itself in natural play, and a shallow seam never floods; a flooded seam asks for the first engine; steam state survives a snapshot restore and continuation to identical bytes; a Fief economy never gains steam state; coal firing halves heat wood only where coal is in store, charges a quarter litre per litre of wood, and does nothing in Fief.

## Checks

See the checks line in the commit message. Toy golden, `validate --strict`, the research and politics finals (`cb6fd7b2b6ebfd06`, `5d3ebe260a0f55b5`, unchanged from slice 1) and the full suite were run after the last edit.

## Not done in this slice

1. **Steam in a factory** (FIB-12, MEC-10): the Factory's output scales by the power ledger share and is a later work package.
2. **Roads and canals** (TRN-10/11): haul-cost changes and the canal structure; FIR-11 does not yet need TRN-11 in the sim (the catalog requires it).
3. **A dedicated coke oven** structure and its gate.
4. **Views:** no card shows engines, engineers, coal stock or the idle state ([08 §13](../08-ui-and-world-display.md)); `SteamReadout` is the hook for WP-E6-11.
5. **No natural-play measurement.** Fixtures show the chain; whether natural play builds engines, how much wood FIR-11 saves and what the pump is worth need a Discovery whole-match run (WP-E6-12).
