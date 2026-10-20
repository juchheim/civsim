# CivSim Godot front end (M7a)

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
the viewer has discovered, and right-click a tile to place a Beacon through the
normal command queue. A refused order appears in the status panel. If Godot cannot
find the .NET runtime on macOS, set `DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec`.
Build the C# project with `dotnet build game/CivSim.Game.csproj -c Release`. A
screenshot can be captured after startup with:

```bash
godot-mono --path game -- --screenshot=/tmp/civsim-m7a.png
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

Latest local M7a gate on an Apple M4, Godot 4.7.2, 1600×900, 2-second warm-up and
5-second samples: Z0 60.2 fps average / 59.1 minimum one-second window, Z1 65.2 /
61.7, Z2 45.3 / 42.9. Each run had 2,731 figures, was explicitly unpaused at
1×, and advanced 80–90 simulation ticks. Z2 remains below the 60 fps target; its
sample reported 2.91 million rendered primitives and 441 draw calls per frame.
