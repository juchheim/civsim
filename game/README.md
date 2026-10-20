# CivSim Godot front end (M7a–M7b)

The project is the presentation layer for `CivSim.Sim`. It reads the viewer-filtered
`MatchFrame` through `FrameStore`, renders the low-poly 3D map, and queues plain
simulation commands. Rendering never changes model state.

## Run

From the repository root, build the compiled content pack once and launch the Godot
4.7 .NET project:

```bash
dotnet run --project src/CivSim.Cli -c Release -- build-pack --out civsim.pack
godot-mono --path game
```

The window defaults to civilization 0 and seed `20261019`. Use the mouse wheel to
zoom through Z0–Z3, drag to orbit, WASD/arrows to pan, Q/E to rotate, Space to pause,
and `[` / `]` to change speed. Press F to refocus the capital, R to show deposits
the viewer has discovered, T to open the research web, and right-click a tile to
place a Beacon through the normal command queue. Press D to open the politics panel:
choose a known civilization and, for military orders, one of its visible settlements.
The panel offers gifts,
barter, war, peace, alliance, raids, assaults and recall for parties on the road;
orders that the current frame shows as unavailable are disabled. Gift and barter
can use a selected visible destination or choose the closest settlement pair. A
refused order appears in the status panel. If Godot cannot
find the .NET runtime on macOS, set `DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec`.
Build the C# project with `dotnet build game/CivSim.Game.csproj -c Debug` before
the normal Godot launch; Godot loads the Debug assembly by default. A screenshot
can be captured after startup with:

```bash
godot-mono --path game -- --screenshot=/tmp/civsim-m7a.png
```

The M7b first pass keeps a 4:2:1 focus bar on screen with slot progress, a sampled ETA,
queue peek, Insight/year and the Council toggle. Leaning opens three sliders whose
shares are normalized to 100% when applied. In the research web, select a visible
project and use the inspector to set Primary/Secondary/Tertiary, enqueue it, or
remove it from the queue. Keys `1`–`3` assign the selected project while the web is
open; `Esc` closes it. Search covers project IDs, names, lineages, effect terms and
gate targets. Hidden projects remain absent from search. **W1 Overview** summarizes
known completed, available/slotted and glimpsed projects by lineage and era without
counting hidden projects; clicking a cell drills into its lane. **W2 Lanes** shows
the individual nodes, with optional size emphasis based only on known dependents.
The inspector's visible prerequisite and unlock buttons navigate between lanes.
Method previews include how many of your current figures perform the action and how
many are below the new rung at their settlement.
A research node can be dragged onto a focus slot, and dragging a slotted project to
another slot changes its rank without losing progress.
A development capture can open the web on launch with:

```bash
godot-mono --path game -- --research-web --screenshot=/tmp/civsim-m7b-web.png
godot-mono --path game -- --research-overview --screenshot=/tmp/civsim-m7b-overview.png
```

For renderer checks, `--start-minute=70 --viewer=-1` advances the deterministic
match to a mature omniscient frame before drawing; the HUD reports the number of
figures in that frame. This development option defaults to minute 0 and viewer
civilization 0. Long fast-forwards can take a while in Godot's Debug configuration.

For a sustained renderer sample, build optimized managed assemblies and run one
zoom level at a time:

```bash
dotnet build game/CivSim.Game.csproj -c Debug -p:Optimize=true
godot-mono --path game -- --start-minute=70 --viewer=-1 --benchmark-seconds=8 --benchmark-zoom=0
```

`--benchmark-zoom` accepts 0–3 (Z0–Z3). The match continues at its normal 1×
speed, the renderer warms up for two seconds, then prints an `M7A_PERF` line with
average FPS, the slowest complete one-second window, simulation pause state and
tick advance, plus the mean and peak main-thread frame refresh time. Pass
`--screenshot=/tmp/civsim-m7a-z0.png` to save a capture at the end. Run the
command with zoom values 0, 1 and 2 for the M7a performance gate.

The current M7a view is the low-poly 3D renderer at
`Renderer/LowPoly3DView.gd`; its terrain, settlement, vegetation and actor assets
live under `Renderer/LowPoly3D/`. The renderer script path is selected by
`civsim/renderer_script`. A replacement view must implement `setup_frame`,
`update_frame`, `set_camera`, and `set_season`, and expose its `Camera3D` as
`Camera3D`. The view receives only viewer-filtered `MatchFrame` data, so painted
and pixel renderers can use the same sim boundary later.

Regional zoom (Z2) swaps trees and people to simpler silhouettes, omits tree
shadows and limits trampled-earth washes to the nearest six settlements. It also
renders the 3D buffer at 75% scale with FSR upscaling; the HUD remains at native
resolution. Close zooms retain the detailed meshes and full-resolution rendering.

Latest local M7a gate on an Apple M4, Godot 4.7.2, 1600×900, 2-second warm-up and
8-second samples:

| Zoom | Average FPS | Minimum 1-second FPS | Figures | Tick advance | Primitives | Draw calls |
|------|------------:|--------------------:|--------:|-------------:|-----------:|-----------:|
| Z0 | 60.5 | 59.9 | 2,731 | 110 | 1.93M | 226 |
| Z1 | 60.0 | 59.9 | 2,731 | 110 | 1.58M | 209 |
| Z2 | 60.4 | 58.4 | 2,731 | 110 | 1.82M | 299 |

All samples ran the simulation unpaused at 1×. Average FPS clears 60 at Z0–Z2;
the minimum one-second window dipped slightly below 60 at each zoom, so repeat the
gate on the target Mac when making later renderer changes.
