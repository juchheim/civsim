# CivSim Godot front end (M7a–M7d, UI redesign)

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

The window defaults to civilization 0 and seed `20261019`. The UI follows
[docs/MVP/20-ui-design-spec.md](../docs/MVP/20-ui-design-spec.md): one theme, one type scale, warm ink-glass
panels and an ochre accent. If Godot cannot find the .NET runtime on macOS, set
`DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec`.

**Camera.** Drag to move the map, right-drag (or Option/Alt-drag) to turn and tilt,
scroll or pinch to zoom toward the cursor (the same two-finger scroll on a trackpad),
`WASD`/arrows to pan, `Q`/`E` to turn, `0`–`3` or `−`/`=` to zoom, `F` to frame your
settlement. The left rail has +/−, a Z0–Z3 zoom ladder, a compass (click for north)
and the map layers (Claims, Food, Methods, Resources) with a legend.

**Keys.** `Space` or `P` pause from anywhere (also inside the research web), `[` `]`
speed, `T` research, `O` diplomacy, `C` Chronicle, `H`/`F1` help, `B` Beacon mode
(click an owned Beacon to remove it, or an empty tile to move the nearest if full),
`R` resources layer, `F5`/`F9` quick save/load,
`F3` developer overlay (fps, figures, UI timings, project IDs in tooltips). `Esc`
closes the topmost layer (popover, drawer, web, modal); with nothing open it opens the
game menu. Typing in the search field never moves the camera.

**Time.** The top band shows the date, a dominant Pause/Resume button and ½×/1×/2×/4×.
While paused the Resume button turns gold, a "PAUSED" pill appears, and the viewport
gets a gold frame and a light scrim. A new match starts paused ("opening hold") until
the first research choice. Orders given while paused show as *pending*.

**Menu.** Save, load, replay save/watch, settings (UI size 100/115/130 %, reduce
motion, hints, tips, pause in web, pause on danger), help, new match and quit.
Saves and the latest replay are stored under Godot's user-data directory in
`saves/quicksave.civsave` and `replays/latest.csr`. Saves include the versioned
simulation snapshot, fog memory, pending player orders, command history, checksums,
AI explanations, and onboarding progress. Replays start from the seed and recorded
player orders; the viewer reports whether periodic checksums match. The match pauses
at the first KEY-3 completion or at 40 simulated minutes and shows standings, the
turning-point timeline and what the rival AIs were thinking.

The standard game uses Gentle start: its first three winters are protected from
harsh conditions, and the human civilization cannot make rival contact before
KEY-1. To capture the first-run intro in a development screenshot, pass
`--show-onboarding` with `--screenshot`.
Build the C# project with `dotnet build game/CivSim.Game.csproj -c Debug` before
the normal Godot launch; Godot loads the Debug assembly by default. A screenshot
can be captured after startup with:

```bash
godot-mono --path game -- --screenshot=/tmp/civsim-m7a.png
```

A new match starts with the human player's Auto-fill (the Council) off and opens the
research web to what is ready now. The focus bar is always on screen: three cards sized
1 : 0.618 : 0.382 (Primary, Secondary, Tertiary) with live share, progress and ETA; an
open slot is a pulsing dashed card with "+ Choose a project". In the web, **Choose**
shows ready projects in Craft/Kin/Reach columns, then "Almost ready" projects with the
one thing each still needs; **Map** shows every known field by age. Click a project to
read it; the big button starts it in the first open slot (or asks which slot to
replace); double-click, `Enter` and `1`–`3` do the same. Prerequisites read "All of
these" / "Any one of these" with each one's state, and an unavailable project says why.
Project codes appear only with F3 on. Project names, lineage names and effects are
phrased in plain English from the content pack (`Ui/UiText.cs`).

Development captures (UI only; none of these touch simulation state):

```bash
godot-mono --path game --resolution 1600x900 -- --screenshot=/tmp/civsim-ui.png [--ui-open=web|map|inspector-almost|replace|diplomacy|chronicle|help|settings|menu|end|intro]
godot-mono --path game --resolution 1280x720 -- --scenario=assign --shots=/tmp/civsim-shots   # scripted walk-through, one PNG per step
```

Scenarios live in `Scripts/Hud/Game.Scenarios.cs` (`assign`, `paused-assign`, `replace`,
`almost`, `map`, `overlays`, `moments`, `toasts`, `menu`, `diplomacy`, `tile`, `end`, `zoom`,
`popovers`, `session`, `input`, `dev`). Other options: `--opening-hold` (use the paused
start in a capture), `--paused`, `--ui-scale=1.3`, `--reduce-motion`, `--research-map`,
`--ui-debug`.

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
