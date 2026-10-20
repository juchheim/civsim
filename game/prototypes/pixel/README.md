# Retro pixel-art prototype

A code-only retro pixel renderer for real CivSim match frames (`civsim-harness view --dump-frame`). Every pixel is
written at native resolution from one hand-picked palette, with no antialiasing, smoothing or quantising afterwards. The
image is then scaled up with nearest-neighbour. No downloaded assets and no image models are used.

| File | What it shows |
|------|---------------|
| `map_scale.png` | Z1, 33x20 tiles at x2: the Sky civ's heartland (capital at 50,73, four more villages, mountains, coast, river) |
| `settlement_close.png` | Z0/Z1, 11x6.4 tiles at x6: the Yellow capital (109,40): huts, hearth, totem, banner, fields, orchards, pastures, weir, people |
| `region_overview.png` | Z3, 132x88 tiles at 6 px per hex, x2: both continents, the borders of all four civs, settlement icons, camps |
| `season_{spring,summer,autumn,winter}.png` | The same Orange-civ window (22x12.5 tiles, x3) in each season |
| `sprite_sheet.png` | Every sprite at x6, labelled: people, tools, carried goods, the 10 herd species, predators, structures, fire frames, the 8 civ banners and emblems, trees, terrain, season variants, field crops by season, the palette |
| `animated.gif` | A 16-frame loop of the close view at x4: water shimmer, river flow, fire flicker, smoke, walkers, children milling |
| `native/` | 1:1 renders, so you can judge the actual pixels |

## Run

```bash
cd game/prototypes/pixel
# all deliverables (about 6 s); frames are read from --data, $CIVSIM_FRAMES or ../data
uv run --no-project --with pillow --with numpy python shots.py --data ../data
uv run --no-project --with pillow --with numpy python shots.py --data ../data close gif   # a subset

# any window of any frame
uv run --no-project --with pillow --with numpy python render.py view --frame ../data/frame20.json \
    --center 60,32 --tiles 30x20 --scale 2 --season winter --frame-no 0 --out out.png --native out_1x.png
```

`--center` takes a tile (x,y) and defaults to the largest settlement. `--tw` sets the pixels per hex column (24 by
default; `overview.py` uses 6). The frame JSON files are not committed in this folder. They sit in
`game/prototypes/data/` of the main checkout.

## Modules

- `palette.py`: the master palette (49 world colours plus 8 civ colours x 3 shades) and the ramps that define each
  colour's darker and lighter neighbour, plus a "warm" neighbour used for firelight. Seasons do not add colours. They
  remap roles (grass, leaf, crop, ground, water, snowcap) onto ramps that already exist.
- `sprites.py`: hand-drawn ASCII sprites (people, tools, carried goods, animals, structures, fire, emblems, banners)
  and small procedural pixel routines (clumped broadleaf canopies, conifers, bare winter trees, acacias, palms, bushes,
  rocks, reeds, mountains). Every sprite follows the same rule: light from the top-left and a selective outline on the
  bottom and right in the object's own darkest shade.
- `terrain.py`: the hidden-hex ground. Each pixel looks up the hex under a domain-warped position, so biome edges are
  organic curves with a 1-2 px dithered fringe. Elevation is sampled linearly, box-blurred and lit from the top-left.
  Dithering is an ordered Bayer pattern kept to a narrow band at each ramp step, so flats stay flat. The module also
  draws water depth, lapping foam, drifting sparkles, beaches, meandering rivers with banks, and the decals and
  y-sorted objects (tufts in three shapes, flowers, dune ripples, ice cracks, wetland pools, trees in groves, mountains).
- `village.py`: turns a settlement record into a village plan. The hearth plaza sits on the settlement tile, dwellings
  and stores go on neighbouring tiles that are not fields or water, a thorn hedge follows the footprint outline with
  gates toward the fields, and home-bound people are given open spots.
- `render.py`: composes a scene. It draws fields as a raised-plot patchwork, orchards, fenced pastures, footpaths, camps,
  herds, people (by age band, activity, flags, walk and work frames, tools, carried goods, and hair, skin and clothing
  variants), cast shadows, firelight, smoke and territory. It is also the CLI.
- `overview.py`: the Z3 level of detail. It has canopy glyphs, peak glyphs, hut-cluster icons sized by population,
  capital banners with the civ emblem, and the same territory language.
- `sheet.py`, `font.py`, `shots.py`: the sprite sheet, a 3x5 pixel font, and the deliverable builder.

## Decisions

**Grid: hidden hexes, continuous land.** 08 §1 says hexes are invisible by default, so the land reads as one
landscape. The hexes still drive everything: each pixel's biome, owner, field and river come from the hex under it,
and only the edges are warped. Pointy-top pixel-hex tiles would have looked like a board game and fought the "diorama"
brief. At 24 px per hex column a person (7x11 px) is about half a hex tall, which keeps a village of 40 people
readable at x2 and gives trees and huts room for 3-4 shades.

**Territory.** Wash plus border. The wash is a diagonal hatch in the civ colour, densest just inside the border and
fading over about 9 px. The border is a 2 px line in the civ's light and mid shades, carrying the civ's dash pattern
from 08 §4.3 (solid, long dash, dot, dash-dot ...), with a dark rim outside. The first version used a solid tinted line
and a checker wash. The Sky civ's border then looked exactly like a river, so the hatch and the dashes are what tell
the two apart. Single unclaimed hexes surrounded by one civ are filled, so they do not draw as rings.

**Civ identity without colour.** Banners carry the 5x5 emblem shape (sun, wave, leaf, star, mountain, flame, moon,
antler), and the borders carry the dash pattern.

**Seasons.** Spring has fresh greens, blossoming trees and orchards, and sprouting furrows. Summer is deep green with
dry steppe. Autumn has red, orange and gold canopies, ripe fields and olive grass. Winter has snow ground with blue
shadows, bare branching trees with snow on the upper sides, snow-capped conifers, roofs and peaks, and snowy furrows.
Savanna and tropical forest do not get snow; they get a dry season with a little hoar frost, so the season shots use
the temperate Orange heartland.

## Liberties the view takes (data the frame does not carry)

- The frame lists structure **kinds** per settlement, not buildings. The view builds about one dwelling per 5 people,
  using the best dwelling kinds the settlement has.
- The sim jitters home-bound people within a third of a hex of the hearth (28 children on one hearth in frame40). The
  view moves them to open spots across the village and its ring.
- Herd animals that the frame places inside a village are pushed to its edge. Fishers standing in the sea are drawn
  as they are; there are no canoes yet.
- Walk loops ping-pong along each walker's heading so that the GIF loops; real motion would come from consecutive frames.

## Honest weaknesses

- **Villages are busy at Z1.** 30-45 people and 8-10 huts in about 3 hexes is a crowd. The children (65% of the
  figures in frame40, which may be worth checking in the sim) blur into a swarm at x2. 08 §2.3's crowd glyphs would help.
- **People are one silhouette family.** There is one adult body, one child, one elder, a bend pose, a swing pose and a
  2-frame walk. There are no faces, no per-activity animation beyond the tool, and the resting figure reads as a log
  at x2.
- **Borders still look hexagonal** when zoomed out (rounded hex polygons). The Sky and Blue civs' dashed borders are
  still the easiest to confuse with water.
- **The worldgen's dead-straight row rivers** and latitude-band biomes show at Z3. The overview meanders the rivers,
  but the bands stay.
- **Mountains are repeated glyphs.** A range reads as a mosaic of similar peaks, not one massif, and the mountain
  floor dither is busy.
- **Some textures repeat.** Grass tufts sit on a jittered grid, and broadleaf canopies come from one generator with
  colour variants.
- **Lighting is fixed.** There is no day/night pass and no colour grading; firelight is a few warm pixels.
- **Palette count.** It uses 49 world colours, one over the 48 target, plus 24 reserved civ colours. A single frame
  uses 43-49 colours.
- **This is a Python reference renderer**, at about 0.2-1 s per view. In Godot, the per-pixel warp, hillshade and
  dither would become a shader over the tile data. The sprites, palette, ramps and rules port directly.
