# Painted map prototype (Godot 4, GDScript)

Illustrated tabletop-map look for CivSim: relief-shaded 3D terrain with a watercolour shader, inked props, settlements with banners, territory washes. Draws the real frames in `../data/frame40.json` and `frame20.json`.

Run: `godot-mono --path game/prototypes/painted` (orbit drag, wheel zoom, WASD pan, 1-4 seasons, 5/6 frame, G hex grid). Screenshots: `-- --shot` (writes `shots/`).

Status: unfinished. The agent stopped on a usage limit mid-polish; no performance numbers were taken and banners at map scale were still being checked. See `shots/` for the last renders.
