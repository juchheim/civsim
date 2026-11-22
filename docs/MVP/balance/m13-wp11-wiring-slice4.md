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

## A real bug the gallery found

`FillDiscovery` threw an `OverflowException` when the viewer is -1 (spectator, `--viewer=-1`): `HasOceanReach` casts the civ to `uint`, which overflows under the Sim's checked arithmetic. The frame now returns before the realm block and the town loop for a spectator. `DiscoveryFrameTests` has a spectator test that fails without the fix. Any Discovery match with a spectator or a replay viewer would have crashed at startup.

## How it was checked

The game builds (0 errors). In the Discovery playtest with `--dev-disc-works` the three works stand outside the capital's cleared ground with the engine's steam, the ovens' bank and the factory's black smoke, all reading as intended. The coal pit was photographed the same way (headframe, spoil heap, steam). The idle and cold twins and the spent pit were not photographed in the map (they are in the art branch's gallery).

## Not done

Everything else in doc 39 §3 to §9 is unwired: coal mines, bastion forts and their stages, the garden, society, exchange house and Palace, ships and the ocean-lane and colony overlays (built, fed only by the gallery's demo), figures, soot, canals, fields, the `LOOK_*` bits, the era skin and Moments.
