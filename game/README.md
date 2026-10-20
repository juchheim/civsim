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

A new match starts with the human player's Council off and opens the research web
to the first available E0 choices. This gives the player the opening assignment;
the Council toggle remains available if they want later slots automated.

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

To sample every map layer at Z0–Z2 in one live simulation, use:

```bash
godot-mono --path game -- --start-minute=70 --viewer=-1 --benchmark-seconds=4 --benchmark-matrix=all
```

The matrix covers Claims, Food, Methods and Resources at each zoom, with a one-second warm-up before each sample. It prints one `M7A_PERF` line per case, including its zoom, overlay and lowest complete one-second FPS window.

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

Latest M7a overlay gate on an Apple M4, Godot 4.7.2, 1600×900. The matrix used a
one-second warm-up and four-second sample for every overlay/zoom pair, with the
simulation running unpaused at 1×. It began at 2,731 figures and ended at 2,833.

| Zoom | Overlay | Average FPS | Minimum 1-second FPS | Figures | Tick advance |
|------|---------|------------:|--------------------:|--------:|-------------:|
| Z0 | Claims | 137.4 | 136.7 | 2,731 | 60 |
| Z0 | Food | 138.0 | 138.0 | 2,731 | 50 |
| Z0 | Methods | 129.9 | 115.9 | 2,745 | 50 |
| Z0 | Resources | 137.6 | 136.5 | 2,745 | 50 |
| Z1 | Claims | 138.0 | 138.0 | 2,745 | 50 |
| Z1 | Food | 134.4 | 125.7 | 2,780 | 50 |
| Z1 | Methods | 137.9 | 137.7 | 2,780 | 50 |
| Z1 | Resources | 137.6 | 137.3 | 2,780 | 50 |
| Z2 | Claims | 129.9 | 115.6 | 2,817 | 50 |
| Z2 | Food | 140.0 | 139.7 | 2,817 | 50 |
| Z2 | Methods | 139.5 | 138.8 | 2,817 | 50 |
| Z2 | Resources | 132.3 | 124.8 | 2,833 | 50 |

All 12 cases cleared the 60 FPS target, including the slowest one-second window
at 115.6 FPS in Z2 Claims. Frame refreshes peaked at 159.37 ms in Z0 Methods;
the simulation continued advancing during every sample.
