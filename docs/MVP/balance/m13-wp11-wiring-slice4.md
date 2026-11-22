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

## How it was checked

The game builds (0 errors). In the Discovery playtest with `--dev-disc-works` the three works stand outside the capital's cleared ground with the engine's steam, the ovens' bank and the factory's black smoke, all reading as intended. The idle and cold twins were not photographed in the map (they are in the art branch's gallery).

## Not done

Everything else in doc 39 §3 to §9 is unwired: coal mines, bastion forts and their stages, the garden, society, exchange house and Palace, ships and the ocean-lane and colony overlays (built, fed only by the gallery's demo), figures, soot, canals, fields, the `LOOK_*` bits, the era skin and Moments.
