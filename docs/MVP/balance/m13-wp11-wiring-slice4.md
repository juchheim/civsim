# M13 WP-E6-11 slice 4: the art branch merged, and the first E6 works on the map (2026-11-22)

**Status:** merge staged and wiring built on `codex/e6-discovery`, uncommitted. Game and renderer code only; the simulation, hashes and goldens are untouched.

## What it does

1. **Merged `codex/e6-art-p0`** (doc 39's art pass: about 110 meshes across `art_disc_power/civic/war/sea/land/people/marks/p2.gd`, the ocean-lane and colony overlays, the discovery rig, the chart grain shader and the `discovery-gallery` scenario). It merged cleanly with no conflicts; the meshes build lazily (`ensure_discovery`), so other chapters pay nothing. A first draft of this slice had drawn its own three meshes before the branch was noticed; they were deleted, not merged.
2. **Wired three works to the map** with that branch's meshes: the atmospheric engine, the coke oven and the factory.

| Part | How |
|---|---|
| **Data** | `Game.cs` adds `discCoal`, `discEngines`, `discOvens`, `discFactoryCoverage` per town and `discoveryActive`; `world_data.gd` reads them (`discovery_settlement`). The structure counts already reach the renderer through `ironWorks`. |
| **Placement** | `core_layout.gd` `_outer` stands each as an outer work when the town holds it. Working (coal in store for the engine and oven, power coverage for the factory) uses `atmospheric_engine_body`, `coke_oven`, `factory`; idle uses `atmospheric_engine_idle`, `coke_oven_idle`, `factory_cold`. |
| **Motion** | Each placed work is appended to `world_builder.disc_works` (a settlement layout array, cleared with the others) and `LowPoly3DView.update_frame` passes it to `discovery_rig.sync`, which adds the engine's rocking beam and the smoke columns for working works only. |
| **Redraw** | `world_builder.gd`'s layout hash includes the three structures and both states. |
| **Dev flag** | `--dev-disc-works` (Debug) gives the biggest town the three works with coal in store and power on, so they can be photographed without playing to them. |

## Coal pits (added after the first commit)

A dug coal seam (`DepositKind.Coal`, overlay kind 13, with the worked bit) is drawn as the branch's headframe pit instead of the generic deposit marker, in `world_builder.gd` `_deposits()`: working is `coal_mine_body` with the rig adding the winding wheel and smoke; spent (the exhausted bit) is `coal_mine_idle`. Pits go in `world_builder.disc_mines`, rebuilt with the deposits, and `LowPoly3DView` passes `disc_works + disc_mines` to the rig. Outside a Discovery frame nothing changes. The flooded state is not drawn: the sim has no flooding. Debug aids: `--dev-disc-works` also marks the two coal tiles nearest the capital known (one dug, one spent), and `--dev-focus-coal` points the camera at the nearest.

## Bastion fort (added after the second commit)

`core_layout.gd` draws the fort in the Citadel slot once the castle stands and the town holds a bastion fort (DEF-10): `bastion_4` with a garrison, `bastion_4_idle` (banner lowered) without one, `bastion_1..3` while it is being built, and on the siege's integrity track `bastion_cracked` (below 700), `bastion_breach` (below 400) and `bastion_ruin` (below 150), with the rig adding dust over a cracked or breached fort. The fort's own 6.75 wall makes the sim report higher integrity than an E5 wall under the same fire, so it shows fewer stages with no extra rule. Seen in the core gallery (`--scenario=core-gallery --dev-core-gallery`, whose Iron city now also holds the fort, engine, oven and factory in a Discovery match): a star fort in its own hex with banners, beside the factory, engine and ovens. The cracked and breached meshes were not photographed in the map: the tiny playtest village has no Citadel hex, so they need a developed town.

## Ocean lanes (added after the third commit)

`DiscoveryFrame` gains `LaneCount` and per-lane from tile, to tile, civilization and volume. A lane is a trade route **already in the frame** (a player sees the routes it is party to, a spectator all of them) whose trip crosses deep ocean (`DeepSeaTilesOnRoute` above zero, which needs SEA-12), so nothing is invented and no land is revealed. `Game.cs` flattens them (`discLanes`), `world_data.gd` reads them, and `LowPoly3DView._sync_ocean_lanes` hands them to the art branch's `lane_overlay` (a dashed arc, a caravel on it, two on a busy lane). `DiscoveryLaneTests` covers a real deep route and the no-SEA-12 case. In the playtest (`--dev-disc-works`, which injects one lane to a sea tile twelve hexes out, and `--dev-focus-sea`) the pale dashes appear on the water.

**Finding:** in natural play (seeds 20261019 and 20261020, 130 minutes) the stock AI has three or four trade routes and **none crosses deep ocean**, so the lane overlay is empty in most matches. That matches the earlier ocean finding (contact never needed the deep ocean): the lanes will show when ships carry colonies, expeditions or a human's routes, not from AI trade. Ship tokens are decoration on a lane (a clock-driven caravel), not sim voyages; wrecks are not wired (no event in the frame).

## Colonies (added after the fourth commit)

`DiscoveryFrame.Colony` is now a state per own town (0 not a colony, 1 content, 2 strained when its Cohesion band is strained or fraying, 3 blockaded when its harbor is cut off by sea) and `CapitalTile` is where the tether leads (`LivingEconomyModel.CapitalTile`). `Game.cs` sends `discColony` per town and `discCapitalTile`; `LowPoly3DView._sync_colonies` gives the art branch's `colony_overlay` one entry per own colony (never a rival's, never a remembered town): the pennant and tether always, the dashed yellow ring when strained, the solid red ring when blockaded. A colony that breaks away is an ordinary Free Folk town and stops being listed, so the "independent" state and foundings are not wired. `DiscoveryColonyTests` checks a content own colony, the capital tile and that a rival reads none. Seen in the playtest with `--dev-disc-works --dev-colony=1|2|3` (Debug), which marks the capital town as a colony in that state; the strained and blockaded rings render as designed. The galleon on station at a blockaded harbor is not wired.

## Town buildings (added after the fifth commit)

`core_layout.gd` now places, in a Discovery match, the learned society in the Learning hex (`learned_society`, or `_idle` with no scholar at work), the exchange house in the Civic hex (`exchange_house` / `_idle` by whether the exchange is staffed), the Renaissance Palace on a Wonder hex (`renaissance_palace_1..4` while it is built, stage 4 once finished) and the botanical garden as an outer work (`botanical_garden` / `_idle` by whether a botanist is in post). Staffing comes from the frame: the garden per town, the society and exchange per civilization (`discSocietyStaffed`, `discExchangeStaffed`), so an empty building never looks alive. The layout hash includes all four and their staffing.

Seen in the core gallery's Iron city (which now also holds all of them; its Great Dome is dropped in a Discovery match to leave a Wonder hex, because the core holds only a few Wonders): the Palace with its dome and wings, the learned society, the formal garden. The exchange house is placed but I could not pick it out in the top view, and a Palace under construction was not photographed.

**Worth knowing:** a town's Wonder hexes are few. A civilization that already holds the Great Dome, a Library and the Cathedral may not have a hex left for the Palace, which would then be dropped (as any extra Wonder is today). That is a layout-budget question for doc 31a, not wiring.

## A real bug the gallery found

`FillDiscovery` threw an `OverflowException` when the viewer is -1 (spectator, `--viewer=-1`): `HasOceanReach` casts the civ to `uint`, which overflows under the Sim's checked arithmetic. The frame now returns before the realm block and the town loop for a spectator. `DiscoveryFrameTests` has a spectator test that fails without the fix. Any Discovery match with a spectator or a replay viewer would have crashed at startup.

## How it was checked

The game builds (0 errors). In the Discovery playtest with `--dev-disc-works` the three works stand outside the capital's cleared ground with the engine's steam, the ovens' bank and the factory's black smoke, all reading as intended. The coal pit was photographed the same way (headframe, spoil heap, steam). The idle and cold twins and the spent pit were not photographed in the map (they are in the art branch's gallery).

## Not done

Everything else in doc 39 §3 to §9 is unwired: coal mines, bastion forts and their stages, the garden, society, exchange house and Palace, ships and the ocean-lane and colony overlays (built, fed only by the gallery's demo), figures, soot, canals, fields, the `LOOK_*` bits, the era skin and Moments.
