"""
The CivSim pixel palette: one hand-picked master list, defined once.

Every pixel the renderer writes is one of these colours; nothing is quantised afterwards. Colours live in short ramps
(dark -> light) whose shadows lean cool/purple and whose highlights lean warm/yellow. Each ramp also defines the
"darker"/"lighter" neighbour of every member, so shading, cast shadows and glows are lookups, never colour maths.

Seasons do not invent colours: they re-map *roles* (grass, leaf, crop, ground ...) onto ramps of the master list.
"""

# --------------------------------------------------------------------------------------------------------------------
# Master colours (name -> hex). World colours first, then the civ identity colours.
# --------------------------------------------------------------------------------------------------------------------
WORLD = [
    # ink: the darkest value, used for deep holes and a few outlines
    ("ink", "#1b1626"),
    # rock / cool neutral ramp
    ("r0", "#2f2b3e"), ("r1", "#4a4659"), ("r2", "#6f6a78"), ("r3", "#9c9590"), ("r4", "#cdc3b0"),
    # earth / wood / hide ramp
    ("e0", "#3a2a2e"), ("e1", "#5e3f33"), ("e2", "#8a5b3b"), ("e3", "#b4824e"), ("e4", "#dbb173"),
    # sand highlight
    ("s4", "#f0dca4"),
    # grass ramp
    ("g0", "#1a3833"), ("g1", "#285638"), ("g2", "#427636"), ("g3", "#6b9a38"), ("g4", "#a3c24c"),
    # canopy ramp (cooler and deeper than grass so forest reads as a mass)
    ("f0", "#11262b"), ("f1", "#1a3d33"), ("f2", "#275a3c"), ("f3", "#3f7a41"),
    # dry grass / straw / thatch ramp
    ("d0", "#5c5530"), ("d1", "#88803c"), ("d2", "#b5a24f"), ("d3", "#d9c777"),
    # water ramp
    ("w0", "#162244"), ("w1", "#1d3a6b"), ("w2", "#28598f"), ("w3", "#367db0"), ("w4", "#58abcc"), ("w5", "#a5dfe0"),
    # snow / ice ramp (blue shadows)
    ("n0", "#6c76a3"), ("n1", "#9babcc"), ("n2", "#cdd9e8"), ("n3", "#f5f7f0"),
    # skin
    ("k0", "#7a4430"), ("k1", "#b06a43"), ("k2", "#df9f70"),
    # fire / autumn
    ("x0", "#5a2224"), ("x1", "#a3372a"), ("x2", "#e3682b"), ("x3", "#f6b23a"), ("x4", "#fdea8e"),
    # tundra moss
    ("t0", "#4e5747"), ("t1", "#717a5d"), ("t2", "#999b7c"),
    # flowers / berries / blossom
    ("p0", "#d9708f"), ("p1", "#f3b5c3"), ("v0", "#8f6fc9"),
]

# Okabe-Ito civ colours, each as (dark, mid = the official hex, light). Reserved for identity: sashes, banners,
# territory wash and borders. They never appear in terrain.
CIV_HEX = ["#E69F00", "#56B4E9", "#009E73", "#F0E442", "#0072B2", "#D55E00", "#CC79A7", "#4D4D4D"]
CIV_RAMPS = [
    ("#7e4a0e", "#E69F00", "#ffd889"),
    ("#2a628f", "#56B4E9", "#c0e8fb"),
    ("#00553f", "#009E73", "#86ddb8"),
    ("#8c7d14", "#F0E442", "#fff9b5"),
    ("#00385f", "#0072B2", "#6cb2e6"),
    ("#76300a", "#D55E00", "#ffa36a"),
    ("#743b63", "#CC79A7", "#f2c0dc"),
    ("#232323", "#4D4D4D", "#a8a8a8"),
]
CIV_NAMES = ["Orange", "Sky", "Green", "Yellow", "Blue", "Vermilion", "Purple", "Neutral"]
CIV_EMBLEMS = ["sun", "wave", "leaf", "star", "mountain", "flame", "moon", "antler"]
CIV_BORDER = ["solid", "long", "dot", "dashdot", "double", "zigzag", "short", "chain"]

# --------------------------------------------------------------------------------------------------------------------
# Ramps: order defines darker/lighter neighbours.
# --------------------------------------------------------------------------------------------------------------------
RAMPS = [
    ["ink", "r0", "r1", "r2", "r3", "r4", "s4"],
    ["e0", "e1", "e2", "e3", "e4", "s4"],
    ["f0", "f1", "f2", "f3", "g4"],
    ["g0", "g1", "g2", "g3", "g4", "d3"],
    ["d0", "d1", "d2", "d3", "s4"],
    ["w0", "w1", "w2", "w3", "w4", "w5", "n3"],
    ["n0", "n1", "n2", "n3"],
    ["k0", "k1", "k2"],
    ["x0", "x1", "x2", "x3", "x4"],
    ["t0", "t1", "t2", "r4"],
    ["p0", "p1"],
    ["v0", "p1"],
]
# darker neighbour for the bottom of a ramp (so shadow on the darkest stays in-family but still darkens)
BOTTOM_DARKER = {"r0": "ink", "e0": "ink", "f0": "ink", "g0": "f0", "d0": "e0", "w0": "ink", "n0": "r1", "k0": "e0",
                 "x0": "e0", "t0": "r0", "p0": "x1", "v0": "r1", "ink": "ink"}

NAMES = [n for n, _ in WORLD]
for i, ramp in enumerate(CIV_RAMPS):
    for j, h in enumerate(ramp):
        NAMES.append(f"c{i}{'dml'[j]}")
HEX = dict(WORLD)
for i, ramp in enumerate(CIV_RAMPS):
    for j, h in enumerate(ramp):
        HEX[f"c{i}{'dml'[j]}"] = h
IDX = {n: i for i, n in enumerate(NAMES)}


def rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


RGB = [rgb(HEX[n]) for n in NAMES]

DARKER = list(range(len(NAMES)))
LIGHTER = list(range(len(NAMES)))
for ramp in RAMPS:
    for j, n in enumerate(ramp):
        i = IDX[n]
        if j > 0 and DARKER[i] == i:
            DARKER[i] = IDX[ramp[j - 1]]
        if j + 1 < len(ramp) and LIGHTER[i] == i:
            LIGHTER[i] = IDX[ramp[j + 1]]
for n, d in BOTTOM_DARKER.items():
    if DARKER[IDX[n]] == IDX[n]:
        DARKER[IDX[n]] = IDX[d]
for i in range(8):
    DARKER[IDX[f"c{i}l"]] = IDX[f"c{i}m"]
    DARKER[IDX[f"c{i}m"]] = IDX[f"c{i}d"]
    DARKER[IDX[f"c{i}d"]] = IDX["ink"]
    LIGHTER[IDX[f"c{i}d"]] = IDX[f"c{i}m"]
    LIGHTER[IDX[f"c{i}m"]] = IDX[f"c{i}l"]

# A warm "firelight" neighbour: what a pixel becomes inside a hearth's glow.
WARM = list(range(len(NAMES)))
for a, b in [("g0", "d0"), ("g1", "d1"), ("g2", "d1"), ("g3", "d2"), ("g4", "d3"), ("d0", "e2"), ("d1", "e3"),
             ("d2", "x3"), ("d3", "x4"), ("e0", "e1"), ("e1", "e2"), ("e2", "x2"), ("e3", "x3"), ("e4", "x4"),
             ("r0", "e1"), ("r1", "e2"), ("r2", "e3"), ("r3", "e4"), ("n0", "r3"), ("n1", "e4"), ("n2", "s4"),
             ("f0", "e0"), ("f1", "d0"), ("f2", "d1"), ("t0", "d0"), ("t1", "d1"), ("t2", "d2")]:
    WARM[IDX[a]] = IDX[b]

SEASONS = ["spring", "summer", "autumn", "winter"]

# --------------------------------------------------------------------------------------------------------------------
# Season roles: each role is a ramp (dark -> light) of master colour names. Sprites and terrain ask for roles.
# --------------------------------------------------------------------------------------------------------------------
_BASE_ROLES = {
    "grass": ["g0", "g1", "g2", "g3", "g4"],
    "lawn": ["g1", "g2", "g3", "g4"],          # bright meadow accents (tufts)
    "leaf": ["f0", "f1", "f2", "f3", "g4"],     # deciduous canopy
    "leafdk": ["f0", "f0", "f1", "f2", "f3"],   # shade-loving / older trees, for variety inside forests
    "leafalt": ["f0", "g1", "g2", "g3", "g4"],  # brighter young trees
    "pine": ["f0", "f1", "f2", "f3"],          # conifer needles (never change colour)
    "palm": ["f0", "g1", "g2", "g3", "g4"],     # tropical broadleaf
    "dry": ["d0", "d1", "d2", "d3", "s4"],      # savanna grass, straw, thatch
    "steppe": ["g1", "g2", "d1", "d2", "d3"],   # grassland steppe
    "moss": ["t0", "t1", "t2", "r4"],          # tundra
    "crop": ["g1", "g2", "g3", "g4"],          # growing crops
    "soil": ["e0", "e1", "e2", "e3"],
    "bloom": ["p0", "p1", "n3", "x4"],         # flower dots
    "fruit": ["x1", "x2"],
    "water": ["w0", "w1", "w2", "w3", "w4", "w5"],
    "snowcap": [],                             # no snow on objects
    "ground_snow": [],
}

SEASON_ROLES = {
    "spring": dict(_BASE_ROLES, **{
        "grass": ["g0", "g1", "g2", "g3", "g4"],
        "lawn": ["g2", "g3", "g4", "d3"],
        "leaf": ["f0", "f1", "f2", "f3", "g3"],
        "leafdk": ["f0", "f0", "f1", "f2", "f3"],
        "leafalt": ["f1", "g2", "g3", "g4", "p1"],  # a few blossoming trees
        "steppe": ["g1", "g2", "g3", "d2", "d3"],
        "crop": ["e1", "g2", "g3", "g4"],        # young shoots over dark soil
        "bloom": ["p0", "p1", "n3", "x4", "v0"],
        "fruit": ["p1", "n3"],                    # orchard blossom
    }),
    "summer": dict(_BASE_ROLES, **{
        "grass": ["g0", "g1", "g2", "g3", "g4"],
        "lawn": ["g1", "g2", "g3", "g4"],
        "steppe": ["g1", "d1", "d1", "d2", "d3"],
        "crop": ["g1", "g2", "g3", "g4"],
        "bloom": ["x4", "n3", "v0"],
        "fruit": ["x1", "x2"],
    }),
    "autumn": dict(_BASE_ROLES, **{
        "grass": ["g0", "g1", "g2", "d1", "d2"],
        "lawn": ["d1", "d2", "d3", "s4"],
        "leaf": ["x0", "x1", "x2", "x3", "x3"],
        "leafdk": ["f0", "f1", "f2", "d1", "d2"],    # some trees still green-gold
        "leafalt": ["e0", "x0", "x1", "x2", "x2"],   # deep red
        "palm": ["f0", "g1", "d1", "d2", "d3"],
        "steppe": ["d0", "d1", "d2", "d3", "s4"],
        "crop": ["d1", "d2", "x3", "x4"],        # ripe gold
        "bloom": ["x2", "x3"],
        "fruit": ["x1", "x2"],
        "moss": ["t0", "x0", "t1", "t2"],
    }),
    "winter": dict(_BASE_ROLES, **{
        "grass": ["n0", "n1", "n2", "n3", "n3"],
        "lawn": ["d0", "d1", "t1", "t2"],         # straw poking through snow
        "leaf": [],                               # bare branches
        "leafdk": [],
        "leafalt": [],
        "palm": ["f0", "f1", "g1", "g2", "g3"],
        "steppe": ["n0", "n1", "n2", "n3", "n3"],
        "dry": ["d0", "d1", "d2", "d3", "s4"],
        "moss": ["n0", "n1", "n2", "n3"],
        "crop": ["e1", "e2", "n2", "n3"],        # snowy furrows
        "bloom": [],
        "fruit": [],
        "water": ["w0", "w1", "w2", "w3", "n1", "n2"],
        "snowcap": ["n1", "n2", "n3"],
        "ground_snow": ["n0", "n1", "n2", "n3"],
    }),
}


def season_name(s):
    return SEASONS[s] if isinstance(s, int) else s


def used_colours():
    return len(WORLD), len(NAMES) - len(WORLD)
