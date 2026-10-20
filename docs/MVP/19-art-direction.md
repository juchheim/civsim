# 19. Art direction (placeholder phase)

**Status:** open decision, 2026-10-20. No art budget besides the developer's time; solo; Steam release wanted after long testing.

## What exists
- `tools/placeholder_art/generate.py` builds the gray-box / silhouette kit (106 sprites: 14 biome hexes, 9 figure poses in body and tintable-sash layers, 9 tools × 3 tiers, 25 structures, 12 animals, 8 civ emblems, `palette.json`) into `game/assets/placeholder/`. Everything is flat geometry and recolourable in the engine (civ sash, tool tier, season).
- `civsim-harness view --dump-frame FILE.json` writes a real frame; `tools/placeholder_art/mock_scene.py` draws it in three looks. Outputs are in [art-mocks/](art-mocks/): `A_flat_diorama`, `B_soft_lit_diorama`, `C_parchment_map` (full scene and close-up each) and `comparison_close.jpg`.

## The three looks
| Look | What it is | What it needs in Godot | Strength | Risk |
|------|------------|------------------------|----------|------|
| A Flat diorama | The kit as drawn: flat fills, thin outlines | Nothing beyond sprites and tints | Clearest at a glance; cheapest | Reads as "programmer art" |
| B Soft-lit diorama | A plus hill shading from elevation, soft shadows, season grade, vignette | A few shaders (shading, shadow blob, colour grade) | Most polish for the least asset work; keeps all tints | Shader tuning time |
| C Parchment map | Ink-and-wash sprites on aged paper, dashed borders | A paper shader and an ink-style sprite pass | Distinct identity; hides sprite simplicity; suits a chronicle theme | Lower contrast; civ colour needs care for colourblind safety (08 §4.3) |

## Options for final art (no budget)
Code-made flat vector (free, consistent, recolourable); CC0 packs such as Kenney for UI icons and sound (free, mixed styles); AI generation (rejected as the core: consistency, tiny readable silhouettes, animation, copyright and storefront disclosure); commissioned art (no budget). The generator and sprite manifest double as the production spec if an artist is ever hired.

## Recommendation
Build M7a on look **B** (it is A plus shaders, so A remains available as the low-end fallback), keep C as a switchable skin to test with players, and decide after the M9 playtest.

## Decision (2026-10-20)
Three Godot/Python prototypes now exist in [game/prototypes](../../game/prototypes/README.md): **low-poly 3D diorama**, **painted illustrated map** and **retro pixel art**, each drawing the same real match frames. **Low-poly 3D is the primary look for M7a** (best colour and contrast); all three are kept so that player tests can compare them. The earlier flat 2D sprite kit and the three 2D mocks above were rejected as too weak and remain only as the sprite manifest and the frame-dump tooling.
