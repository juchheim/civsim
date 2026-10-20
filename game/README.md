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

The renderer loads its `IGameLook` from the `civsim/look` project setting. The
current value, `low_poly_3d`, selects the low-poly palette, shaders, materials and
primitive models in `Scripts/Looks/`; painted and pixel styles can be added as
separate implementations without changing the frame contract or simulation.
