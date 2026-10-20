# Art prototypes

Three code-generated looks, all drawing the same real match frames (`data/frame20.json`, `data/frame40.json`, written by `civsim-harness view --dump-frame`). Decision (2026-10-20): **low-poly 3D is the primary look for M7a**; keep all three and test with players. See docs/MVP/19-art-direction.md.

| Look | Folder | Run | Status |
|------|--------|-----|--------|
| Low-poly 3D diorama (primary) | `lowpoly3d/` | `godot-mono --path game/prototypes/lowpoly3d` | Rendered all shots; 53 fps at 40 tiles across, 38 fps whole map |
| Painted illustrated map | `painted/` | `godot-mono --path game/prototypes/painted` | Strong look; unfinished (banners at map scale, no perf numbers) |
| Retro pixel art | `pixel/` | `uv run --no-project --with pillow --with numpy python game/prototypes/pixel/render.py` (see its README) | Python renderer, shots and an animated GIF |

These are GDScript/Python prototypes reading JSON frames. The real M7a project reads `MatchFrame` through `CivSim.Sim.View` in C# (docs/MVP/18-view-layer.md).
