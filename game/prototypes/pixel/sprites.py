"""
Pixel sprites for the CivSim retro prototype.

Two kinds of sprite live here:
  * hand-drawn ASCII maps (people, animals, structures, emblems) whose characters map to palette names or to
    season roles ("@leaf2" = third shade of the season's leaf ramp);
  * small procedural pixel routines (trees, mountains, rocks) built from integer masks with the same light rule:
    light from the top-left, selective outline on the bottom/right in the object's own darkest shade.

A sprite is a uint8 array of palette indices with 255 = transparent, plus an anchor (the pixel that stands on the
ground). Everything is cached per season.
"""
import math
from functools import lru_cache
import numpy as np
from palette import IDX, SEASON_ROLES, season_name, NAMES

T = 255  # transparent


class Spr:
    __slots__ = ("px", "ax", "ay")

    def __init__(self, px, ax=None, ay=None):
        self.px = px
        h, w = px.shape
        self.ax = w // 2 if ax is None else ax
        self.ay = h - 1 if ay is None else ay

    @property
    def w(self):
        return self.px.shape[1]

    @property
    def h(self):
        return self.px.shape[0]

    def flip(self):
        return Spr(self.px[:, ::-1].copy(), self.w - 1 - self.ax, self.ay)


def resolve(token, season):
    """palette name, '@role3' (role ramp index), or '' -> palette index or T."""
    if token is None or token == "":
        return T
    if token.startswith("@"):
        name = token[1:].rstrip("0123456789")
        i = int(token[1 + len(name):] or 0)
        ramp = SEASON_ROLES[season_name(season)][name]
        if not ramp:
            return T
        return IDX[ramp[min(i, len(ramp) - 1)]]
    return IDX[token]


def parse(art, legend, season="summer", anchor=None):
    rows = [r for r in art.strip("\n").split("\n")]
    rows = [r.rstrip() for r in rows]
    # strip common indentation
    ind = min(len(r) - len(r.lstrip(" ")) for r in rows if r.strip())
    rows = [r[ind:] for r in rows]
    w = max(len(r) for r in rows)
    px = np.full((len(rows), w), T, np.uint8)
    cache = {}
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch in ". ":
                continue
            if ch not in cache:
                cache[ch] = resolve(legend[ch], season)
            px[y, x] = cache[ch]
    if anchor is None:
        return Spr(px)
    return Spr(px, anchor[0], anchor[1])


# ====================================================================================================================
# PEOPLE  (7 px wide, 10-11 px tall; facing right; flip for left)
# ====================================================================================================================
PEOPLE_LEGEND = {
    "o": "e0", "h": "e1", "H": "e2", "S": "k2", "s": "k1", "q": "k0",
    "T": "e3", "t": "e2", "u": "e1", "C": "CIVm", "c": "CIVd", "L": "CIVl",
    "l": "e1", "f": "e0", "g": "r3", "G": "r4", "w": "e2", "W": "e3", "i": "ink",
    "P": "e4", "y": "r2", "Y": "r4", "4": "d3", "3": "d1",
}

ADULT = {
    "stand": """
        ..ooo..
        .ohhHo.
        .ohSSo.
        ..osq..
        .oCTto.
        oSTCtto
        osuTCso
        .otuto.
        .ol.lo.
        .ol.lo.
        .of.fo.
    """,
    "walk1": """
        ..ooo..
        .ohhHo.
        .ohSSo.
        ..osq..
        .oCTto.
        oSTCtto
        .suTCso
        .otuto.
        .olollo
        ol..oo.
        of....f
    """,
    "walk2": """
        ..ooo..
        .ohhHo.
        .ohSSo.
        ..osq..
        .oCTto.
        oSTCtto
        osuTCs.
        .otuto.
        .ollo..
        ..ol.o.
        ..of.f.
    """,
    "work": """
        ..ooo.s
        .ohhHoo
        .ohSSso
        ..osqo.
        .oCTto.
        oSTCto.
        osuTCo.
        .otuto.
        .ol.lo.
        ol..lo.
        of..fo.
    """,
    "bend": """
        .......
        ..ooo..
        .ohhHo.
        oCohSSo
        oTCosqo
        otTCtto
        .otutso
        .otu.s.
        .ol.lo.
        ol..lo.
        of..fo.
    """,
}
FEMALE_HAIR = {  # longer hair: replace the head rows
    1: ".ohhHo.", 2: "ohhSSo.", 3: "oh.osq..",
}
ELDER = {
    "stand": """
        .......
        ..ooo..
        .ogGGo.
        .ogSSo.
        ..osqo.
        .oCTto.w
        oSTCttow
        osuTCsw.
        .otuto.w
        .ol.lo.w
        .of.fo.w
    """,
    "walk1": """
        .......
        ..ooo..
        .ogGGo.
        .ogSSo.
        ..osqo.
        .oCTto.w
        oSTCttow
        osuTCsw.
        .otuto.w
        .olollow
        ol..oo.w
    """,
}
CHILD = {
    "stand": """
        .ooo.
        ohhHo
        ohSSo
        oCTto
        sTCts
        .ouo.
        .o.o.
    """,
    "walk1": """
        .ooo.
        ohhHo
        ohSSo
        oCTto
        sTCt.
        .ouoo
        o...o
    """,
}
RESTING = """
    .......ooo.
    .oooooohhSo
    oCLCCCohSso
    occcccCooo.
    .44444444443
    ..3333333...
"""

# tools: drawn on top of the figure; anchor = the hand pixel of the "work"/"stand" pose
TOOL_LEGEND = {"w": "e2", "W": "e3", "o": "e0", "r": "r3", "R": "r4", "k": "r1", "n": "d2", "N": "d3", "x": "r2",
               "m": "x1", "M": "x2", "b": "d1", "B": "d2"}
TOOLS = {
    # (art, hand-x, hand-y) hand coords inside the tool art
    "spear": ("""
        .R
        rk
        .w
        .w
        .w
        .w
        .w
        .w
        .w
        .w
        .w
        .w
        .w
        .o
    """, 1, 6),
    "axe": ("""
        rRk
        rww
        ..w
        ..w
        ..o
    """, 2, 3),
    "hoe": ("""
        .wwo
        w...
        rr..
    """, 3, 0),
    "basket": ("""
        .oo.
        oNNo
        onno
        .oo.
    """, 0, 0),
    "net": ("""
        w.....
        .w....
        ..w...
        ...wxx
        ....xx
    """, 0, 0),
    "hammer": ("""
        kRr
        .w.
        .w.
        .o.
    """, 1, 2),
    "sickle": ("""
        .Rr
        R..
        .w.
        .o.
    """, 1, 2),
    "torch": ("""
        .M.
        mMm
        .w.
        .w.
        .o.
    """, 1, 3),
}
CARRY = {
    # bundles on the back (drawn behind the head/shoulder, left side when facing right)
    "plants": """
        .bg.
        gBBg
        .bb.
    """,
    "meat": """
        .mm.
        mMMm
        .mm.
    """,
    "wood": """
        wWWW
        wwww
    """,
    "grain": """
        .NN.
        NNNn
        nnnn
    """,
    "fish": """
        x.xx
        xxxx
    """,
}
CARRY_LEGEND = dict(TOOL_LEGEND, g="@crop2", m="x1", M="x2", x="w3")


HAIRS = [("e1", "e2"), ("e0", "e1"), ("x0", "x1"), ("r0", "r1")]
CLOTHS = [("e3", "e2", "e1"), ("e4", "e3", "e2"), ("d2", "d1", "d0"), ("r3", "r2", "r1")]
SKINS = [("k2", "k1", "k0"), ("k1", "k0", "e1")]


@lru_cache(maxsize=None)
def person(season, civ, kind="adult", pose="stand", female=False, warrior=False, pale=False, hair=0, cloth=0, skin=0):
    legend = dict(PEOPLE_LEGEND)
    legend["C"], legend["c"], legend["L"] = f"c{civ}m", f"c{civ}d", f"c{civ}l"
    legend["h"], legend["H"] = HAIRS[hair % 4]
    legend["T"], legend["t"], legend["u"] = CLOTHS[cloth % 4]
    legend["S"], legend["s"], legend["q"] = SKINS[skin % 2]
    if kind == "child":
        art = CHILD.get(pose, CHILD["stand"])
    elif kind == "elder":
        art = ELDER.get(pose, ELDER["stand"])
    elif kind == "resting":
        art = RESTING
    else:
        art = ADULT.get(pose, ADULT["stand"])
        if female and pose != "bend":
            rows = art.strip("\n").split("\n")
            rows = [r.strip() for r in rows]
            rows[2] = "ohhSSo."
            rows[3] = "oh.osq."
            art = "\n".join(rows)
    if warrior:
        # body paint: civ-coloured stripe on the face and a darker tunic
        legend["q"] = f"c{civ}m"
        legend["T"], legend["t"], legend["u"] = "e2", "e1", "e0"
        legend["o"] = "ink"
    if pale:
        legend["S"], legend["s"] = "s4", "k2"
    sp = parse(art, legend, season)
    return sp


@lru_cache(maxsize=None)
def tool(name):
    art, hx, hy = TOOLS[name]
    return parse(art, TOOL_LEGEND, "summer", (hx, hy))


@lru_cache(maxsize=None)
def carry(name, season):
    return parse(CARRY[name], CARRY_LEGEND, season, (2, 2))


# ====================================================================================================================
# ANIMALS (side view, facing right). Anchor = bottom centre.
# ====================================================================================================================
ANIMAL_ART = {
    "deer": ("""
        .......o.o..
        ........ooo.
        .......ohHh.
        .......oHHHi
        .o.....ohho.
        oWooooohhho.
        .ohHHHHHHho.
        .ohHHHhhhho.
        ..ohhhhhho..
        ..oh.oo.ho..
        ..o..o..o...
        ..o..o..o...
    """, {"o": "e0", "h": "e2", "H": "e3", "W": "n3", "i": "ink"}),
    "aurochs": ("""
        ..........y.y.
        ..........oyo.
        .oooooooooohho
        ohhhHHHHHhhhhi
        ohhHHHHHhhhhho
        ohhhhhhhhhhho.
        .ohhhhhhhhhho.
        .oho.oooo.oh..
        .oo..o..o.oo..
        .o...o..o..o..
    """, {"o": "ink", "h": "e0", "H": "e1", "y": "s4", "i": "r3"}),
    "boar": ("""
        ..o.o.o....
        .ohohoho...
        ohhhHHhhoo.
        ohhHHHhhhhy
        ohhhhhhhhhi
        .ohhhhhhho.
        .o.o..o.o..
        .o.o..o.o..
    """, {"o": "ink", "h": "r1", "H": "e1", "y": "s4", "i": "r0"}),
    "bison": ("""
        ...oooo.......
        ..oHHHHoo.....
        .oHHHHHHhooo..
        oHHHHHHhhhhhoo
        oHHHHhhhhhhhyo
        oHHHhhhhhhhhhi
        .oHHhhhhhhhho.
        ..oHo.oooo.oho
        ..oo..o..o..o.
        ..o...o..o..o.
    """, {"o": "ink", "h": "e0", "H": "e1", "y": "r4", "i": "r2"}),
    "horse": ("""
        .........oo..
        ........oHho.
        ........oHhhi
        .......omhho.
        .oooooomHho..
        mmHHHHHHHho..
        .oHHHHHhhho..
        .ohhhhhhhho..
        ..ohooooho...
        ..o.o..o.o...
        ..o.o..o.o...
        ..m.m..m.m...
    """, {"o": "e0", "h": "e2", "H": "e3", "m": "ink", "i": "ink"}),
    "reindeer": ("""
        .......y.y.y.
        .......oyoy..
        ........ooo..
        ........oGgo.
        .......oGGGi.
        .o.....oGgo..
        ogoooooGGgo..
        .ohgGGGGGgo..
        .ohhgggggho..
        ..ohhhhhho...
        ..oh.oo.ho...
        ..o..o..o....
    """, {"o": "r0", "h": "r1", "g": "r2", "G": "r4", "y": "e3", "i": "ink"}),
    "sheep": ("""
        .ooooo...
        oWWWWWoo.
        oWWWWwogo
        oWwwwwogi
        .owwwwoo.
        ..o.o.o..
        ..o.o.o..
    """, {"o": "r1", "W": "n3", "w": "n2", "g": "r2", "i": "ink"}),
    "gazelle": ("""
        ........o..
        .......oo..
        .......ohh.
        .......oHHi
        .o....ohho.
        ohooooHho..
        .oHHHHHHo..
        .oiiiiiio..
        ..oWWWWo...
        ..o.o.oo...
        ..o.o..o...
    """, {"o": "e1", "h": "e3", "H": "e4", "W": "s4", "i": "ink"}),
    "llama": ("""
        .........oo.
        .........oHo
        ........oHHi
        ........oHo.
        ........oHo.
        ........oHo.
        .oooooooHho.
        oHHHHHHHHho.
        ohhhhhhhhho.
        .ohhhhhhho..
        .oho..oho...
        .o.o..o.o...
    """, {"o": "e1", "h": "e3", "H": "e4", "i": "ink"}),
    "rabbit": ("""
        ...oo
        ...oH
        .oohh
        ohhhi
        .o.o.
    """, {"o": "e1", "h": "r3", "H": "r4", "i": "ink"}),
    "wolf": ("""
        ........o.o
        ........ooo
        ........oggo
        ooooooooggGi
        .oGGGGGGgggo
        ogggggggggo.
        .ogggggggo..
        .oo.ooo.oo..
        .o..o.o..o..
    """, {"o": "r0", "g": "r2", "G": "r3", "i": "ink"}),
    "bear": ("""
        .........oo..
        ..oooooooho..
        .ohHHHHhhhho.
        ohHHHHhhhhhhi
        ohhhhhhhhhhho
        ohhhhhhhhhho.
        .ohh.oooohho.
        .ooo.....ooo.
    """, {"o": "ink", "h": "e1", "H": "e2", "i": "e0"}),
}
HERD_SPECIES = ["deer", "aurochs", "boar", "bison", "horse", "reindeer", "sheep", "gazelle", "llama", "rabbit"]


@lru_cache(maxsize=None)
def animal(name, season="summer"):
    art, leg = ANIMAL_ART[name]
    return parse(art, leg, season)


@lru_cache(maxsize=None)
def animal_grazing(name, season="summer"):
    """Head-down variant: shift the head block down 2 rows (cheap but reads as grazing)."""
    sp = animal(name, season)
    px = sp.px.copy()
    h, w = px.shape
    head_cols = slice(int(w * 0.62), w)
    block = px[:, head_cols].copy()
    out = np.full_like(px, T)
    out[:, :head_cols.start] = px[:, :head_cols.start]
    # move the head part down by 2, keep legs (bottom 3 rows) in place
    legs = h - 3
    shifted = np.full_like(block, T)
    shifted[2:legs, :] = block[0:legs - 2, :]
    shifted[legs:, :] = block[legs:, :]
    m = shifted != T
    out[:, head_cols][m] = shifted[m]
    o2 = out[:, head_cols]
    o2[m] = shifted[m]
    out[:, head_cols] = o2
    return Spr(out, sp.ax, sp.ay)


# ====================================================================================================================
# EMBLEMS (5x5, drawn in the civ light colour on the civ mid banner)
# ====================================================================================================================
EMBLEMS = {
    "sun": ["#.#.#", ".###.", "##.##", ".###.", "#.#.#"],
    "wave": [".....", ".#...", "#.#.#", "...#.", "....."],
    "leaf": ["...##", ".####", "#####", ".##..", "#...."],
    "star": ["..#..", "#####", ".###.", ".#.#.", "#...#"],
    "mountain": [".....", "..#..", ".###.", "##.##", "#####"],
    "flame": ["..#..", ".##..", ".###.", "##.##", ".###."],
    "moon": [".###.", "##...", "#....", "##...", ".###."],
    "antler": ["#.#.#", "#.#.#", ".###.", "..#..", "..#.."],
}


def emblem(civ):
    from palette import CIV_EMBLEMS
    e = EMBLEMS[CIV_EMBLEMS[civ]]
    px = np.full((5, 5), T, np.uint8)
    for y, r in enumerate(e):
        for x, ch in enumerate(r):
            if ch == "#":
                px[y, x] = IDX[f"c{civ}l"]
    return px


@lru_cache(maxsize=None)
def banner(civ, tall=14):
    """A pole with a civ flag (7x5 cloth) carrying the 5x5 emblem; anchor = pole foot."""
    w, h = 10, tall
    px = np.full((h, w), T, np.uint8)
    for y in range(h):
        px[y, 1] = IDX["e1"] if y > 0 else IDX["e3"]
        px[y, 0] = IDX["e0"] if y > 0 else T
    cloth = np.full((7, 8), IDX[f"c{civ}m"], np.uint8)
    cloth[:, -1] = IDX[f"c{civ}d"]
    cloth[-1, :] = IDX[f"c{civ}d"]
    cloth[0, :] = IDX[f"c{civ}l"]
    cloth[-1, -1] = T
    cloth[1:6, 1:6][emblem(civ) != T] = IDX[f"c{civ}l"]
    # swallow-tail notch
    cloth[3, -1] = T
    cloth[2:5, -1] = T
    cloth[3, -2] = T
    px[1:8, 2:10] = cloth
    return Spr(px, 1, h - 1)


# ====================================================================================================================
# STRUCTURES
# ====================================================================================================================
S_LEGEND = {
    "i": "ink", "o": "e0", "1": "e1", "2": "e2", "3": "e3", "4": "e4", "5": "s4",
    "a": "r0", "b": "r1", "c": "r2", "d": "r3", "e": "r4",
    "D": "@dry0", "E": "@dry1", "F": "@dry2", "G": "@dry3", "H": "@dry4",
    "g": "@grass1", "h": "@grass2", "j": "@grass3", "k": "@grass0",
    "m": "x1", "M": "x2", "N": "x3", "Q": "x4", "q": "x0",
    "C": "CIVm", "K": "CIVd", "L": "CIVl",
    "n": "@snowcap0", "p": "@snowcap1", "P": "@snowcap2",
    "u": "f0", "U": "f1",
}

STRUCT_ART = {
    "hearth": """
        ..bdcb..
        .ba..ab.
        da....ac
        ba....ab
        .bcadcb.
        ..bbab..
    """,
    "lean_to": """
        ..........o2.
        ........oo32o
        ......oo3232o
        ....oo33232hh
        ..oo3jh3232oo
        oojhjhh2322o.
        .ohhgj32221o.
        .o.1o.oooo1o.
        .o.iiiiiiiio.
        ..ooooooooo..
    """,
    "hide_tent": """
        ....o.o......
        .....o.......
        ....o3o......
        ....o32o.....
        ...o4322o....
        ...o43222o...
        ..o443C222o..
        ..o43CKC22o..
        .o4432i2222o.
        .o443iii2221o
        o4443iii22211o
        o4433iii22211o
        .ooooiiioooooo
    """,
    "pit_house": """
        .......oo.......
        ......o21o......
        ....oojhhkoo....
        ...ojjhhhggko...
        ..ojhhhhhgggko..
        .o3jhhhhggggk2o.
        o433hhgggggk222o
        o43332oiio22221o
        .o3332oiio2221o.
        ..ooooooooooooo.
    """,
    "wattle_house": """
        ......oo.......
        .....oGGo......
        ....oGFFEo.....
        ...oGGFEEEo....
        ..oGGFFEEEEo...
        .oGGFFEEEEDDo..
        oGGFFFEEEEDDDo.
        oHFFEEEEEDDDDo.
        .o4333oo33322o.
        .o2222oio2221o.
        .o3433oio3322o.
        .o2222oio2111o.
        ..ooooooooooo..
    """,
    "mudbrick_house": """
        ...............
        .ooooooooooooo.
        o5554444444443o
        o4444444444433o
        oooooooooooo33o
        o4443444344o33o
        o3444344ii4o32o
        o4434443ii3o32o
        o4443444ii4o31o
        o3333333ii3o21o
        .ooooooooooooo.
    """,
    "longhouse": """
        .....oooooooooooooooo.....
        ...ooGGGGGGGGGGGGGGGGoo...
        ..oGGGGGFFFFFFFFFFFFEEEo..
        .oGGFFFFFFFFFFFFEEEEEEEDo.
        oGGFFFFFFEEEEEEEEEEEEEDDDo
        oHFFFEEEEEEEEEEEEEEEEDDDDo
        .o433o433o3oio3o332o322oo.
        .o332o332o3oio3o322o321o..
        .o332o332o3oio3o322o221o..
        ..oooooooooooooooooooooo..
    """,
    "storage_pit": """
        .oooooo.
        o212121o
        o121212o
        oiiiii1o
        .oooooo.
    """,
    "drying_rack": """
        o..........o
        2oooooooooo2
        2.m.M.m.N..2
        2.m.M.m.M..2
        2.q.m.q.m..2
        2..........2
        o..........o
    """,
    "granary": """
        ....oo.....
        ...oGGo....
        ..oGFFEo...
        .oGFFEEEo..
        oGFFEEEEDo.
        oHFEEEEDDDo
        .o3232321o.
        .o2323211o.
        .o3232321o.
        ..ooooooo..
        ..2.2.1.1..
        ..o.o.o.o..
    """,
    "kiln": """
        ....oooo...
        ...o4433o..
        ..o443332o.
        .o4433322o.
        .o443322i1o
        o44332iMi1o
        o4332iNQMi1o
        o332iiMMii1o
        .ooooooooooo
    """,
    "quern": """
        ..ooo..
        .odcbo.
        oeddcbo
        odccbbo
        .ooooo.
    """,
    "thorn": """
        ..1.2..
        .2h3.2.
        3.212h2
        .21d12.
        ..1.1..
    """,
    "palisade": """
        .o.o.o.o.
        o3o3o3o3o
        o3o3o3o3o
        o2o2o2o2o
        o2o2o2o2o
        o2o2o2o2o
        o1o1o1o1o
        ooooooooo
    """,
    "lookout": """
        .ooooooo.
        o3333332o
        o2o222o2o
        .o.....o.
        .32...32.
        .3o2.o2o.
        .3..2..2.
        .3.o2o.2.
        .3..2..2.
        .3.o2o.2.
        .3..2..2.
        .3.o2o.2.
        .3.....2.
        .3.....2.
        o3.....2o
    """,
    "totem": """
        .LCK.
        oCCKo
        .ooo.
        o3o3o
        o333o
        oi3io
        oCCKo
        o3o2o
        o232o
        oo2oo
        o323o
        oCCKo
        o232o
        o232o
        o222o
        ooooo
    """,
    "weir": """
        o.o.o.o.o.o.o.o.
        1212121212121212
        2121212121212121
        o.o.o.o.o.o.o.o.
    """,
    "fence_h": """
        2.2.2.2
        3333333
        o.o.o.o
        2222222
    """,
}
STRUCT_ANCHORS = {"lookout": None}


@lru_cache(maxsize=None)
def structure(name, season, civ=0):
    leg = dict(S_LEGEND)
    leg["C"], leg["K"], leg["L"] = f"c{civ}m", f"c{civ}d", f"c{civ}l"
    sp = parse(STRUCT_ART[name], leg, season)
    if season_name(season) == "winter" and name in ("hide_tent", "pit_house", "wattle_house", "longhouse",
                                                       "granary", "lean_to", "mudbrick_house", "kiln"):
        sp = snow_on(sp)
    return sp


def snow_on(sp, depth=2):
    """Winter: pile snow on every upward-facing top surface (first 1-2 opaque pixels of each column)."""
    px = sp.px.copy()
    h, w = px.shape
    for x in range(w):
        col = px[:, x]
        ys = np.nonzero(col != T)[0]
        if len(ys) == 0:
            continue
        y0 = ys[0]
        for k in range(depth):
            if y0 + k < h and px[y0 + k, x] != T:
                px[y0 + k, x] = IDX["n3"] if (k == 0 and x < w * 0.55) else IDX["n2"] if k == 0 else IDX["n1"]
    return Spr(px, sp.ax, sp.ay)


# ====================================================================================================================
# FIRE + SMOKE frames
# ====================================================================================================================
FIRE_FRAMES = [
    """
    ..Q..
    .QN.
    .NMN.
    mMNMm
    .mqm.
    """,
    """
    ...Q.
    ..NQ.
    .NMN.
    mNMMm
    .mqm.
    """,
    """
    .Q...
    .QN..
    .NNM.
    mMNNm
    .mqm.
    """,
    """
    ..Q..
    ..N..
    .MNM.
    mMNMm
    .mqm.
    """,
]


@lru_cache(maxsize=None)
def fire(frame):
    art = FIRE_FRAMES[frame % len(FIRE_FRAMES)]
    return parse(art, S_LEGEND, "summer")


# ====================================================================================================================
# PROCEDURAL: trees, mountains, rocks
# ====================================================================================================================
def _rng(seed):
    return np.random.default_rng(seed)


def _disc(r):
    yy, xx = np.mgrid[-r:r + 1, -r:r + 1]
    return (xx * xx + yy * yy) <= r * r + r * 0.6


BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


def _canopy(w, h, clumps, ramp, rng, snow=None, outline_all=False):
    """Clumpy canopy: union of discs, each shaded from the top-left, front clumps rimmed where they overlap."""
    owner = np.full((h, w), -1, np.int16)
    shade = np.zeros((h, w), np.float32)
    # draw back to front: clumps sorted by centre y
    clumps = sorted(clumps, key=lambda c: c[1])
    for k, (cx, cy, r) in enumerate(clumps):
        yy, xx = np.mgrid[0:h, 0:w]
        dx, dy = (xx - cx) / max(r, 1), (yy - cy) / max(r, 1)
        d2 = dx * dx + dy * dy
        inside = d2 <= 1.0 + 0.6 / max(r, 1)
        light = -(dx * 0.75 + dy * 0.95) * 0.9 + (1 - np.minimum(d2, 1)) * 0.25
        owner[inside] = k
        shade[inside] = light[inside]
    mask = owner >= 0
    n = len(ramp)
    out = np.full((h, w), T, np.uint8)
    # quantise with a coarse 2x2-cluster dither so leaves read as clumps, not noise
    yy, xx = np.mgrid[0:h, 0:w]
    b = BAYER4[(yy // 1) % 4, (xx // 1) % 4]
    v = (shade + 0.5) * (n - 1.5) + 0.3
    v = v + (b - 0.5) * 0.55
    lvl = np.clip(np.floor(v), 1, n - 1).astype(int)
    # rim: a pixel whose lower/right neighbour belongs to a different (front) clump or is empty -> dark
    for y in range(h):
        for x in range(w):
            if not mask[y, x]:
                continue
            k = owner[y, x]
            down = owner[y + 1, x] if y + 1 < h else -1
            right = owner[y, x + 1] if x + 1 < w else -1
            left = owner[y, x - 1] if x > 0 else -1
            up = owner[y - 1, x] if y > 0 else -1
            if down == -1 or right == -1 or (outline_all and (left == -1 or up == -1)):
                lvl[y, x] = 0
            elif down > k:
                lvl[y, x] = max(1, min(lvl[y, x], 1))
            elif up == -1 and left != -1 and lvl[y, x] < n - 2:
                lvl[y, x] = min(n - 1, lvl[y, x] + 1)
            elif left == -1 and up != -1:
                lvl[y, x] = max(1, lvl[y, x])
    for y in range(h):
        for x in range(w):
            if mask[y, x]:
                out[y, x] = IDX[ramp[lvl[y, x]]]
    # sparkle highlights on the top-left of the biggest clumps
    for (cx, cy, r) in clumps:
        if r >= 3:
            hx, hy = int(round(cx - r * 0.4)), int(round(cy - r * 0.45))
            for (ox, oy) in ((0, 0), (1, 0), (0, 1)):
                X, Y = hx + ox, hy + oy
                if 0 <= X < w and 0 <= Y < h and mask[Y, X] and owner[Y, X] == clumps.index((cx, cy, r)):
                    out[Y, X] = IDX[ramp[-1]]
    if snow:
        for x in range(w):
            ys = np.nonzero(mask[:, x])[0]
            if len(ys):
                out[ys[0], x] = IDX[snow[-1]]
                if len(ys) > 3 and x < w * 0.6:
                    out[ys[1], x] = IDX[snow[-2]]
    return out, mask


@lru_cache(maxsize=None)
def deciduous(seed, size, season, role="leaf"):
    s = season_name(season)
    ramp = SEASON_ROLES[s][role]
    rng = _rng(seed)
    R = size  # canopy radius 3..6
    w = 2 * R + 5
    trunk_h = max(2, R // 2 + 1)
    h = 2 * R + 3 + trunk_h
    cx, cy = w / 2 - 0.5, R + 1.5
    if not ramp:
        return bare_tree(seed, size, season)
    clumps = [(cx, cy, R)]
    for k in range(rng.integers(2, 4)):
        a = rng.uniform(-math.pi, 0.3)
        rr = max(2, int(R * rng.uniform(0.5, 0.7)))
        clumps.append((cx + math.cos(a) * R * 0.55, cy + math.sin(a) * R * 0.55, rr))
    clumps.append((cx + R * 0.25, cy + R * 0.35, max(2, int(R * 0.6))))
    can, mask = _canopy(w, h, clumps, ramp, rng)
    px = np.full((h, w), T, np.uint8)
    # trunk
    tx = int(round(cx))
    for y in range(int(cy + R * 0.5), h):
        px[y, tx - 1] = IDX["e2"]
        px[y, tx] = IDX["e1"]
        if y == h - 1:
            px[y, tx - 2] = IDX["e1"]
            px[y, tx + 1] = IDX["e0"]
    m = can != T
    px[m] = can[m]
    if s == "summer" and seed % 5 == 0:   # a few fruit dots in summer
        pass
    return Spr(px, tx, h - 1)


@lru_cache(maxsize=None)
def bare_tree(seed, size, season):
    """Winter broadleaf: trunk, four limbs fanning up, forked twigs, a faint twig haze and snow on the upper sides."""
    rng = _rng(seed + 900)
    R = size
    w = 2 * R + 5
    trunk_h = max(2, R // 2 + 1)
    h = 2 * R + 3 + trunk_h
    px = np.full((h, w), T, np.uint8)
    cx, cy = w / 2 - 0.5, R + 1.5
    tx = int(round(cx))
    fork_y = int(cy + R * 0.45)

    def line(x0, y0, x1, y1, c):
        n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
        pts = []
        for i in range(n + 1):
            t = i / max(n, 1)
            X, Y = int(round(x0 + (x1 - x0) * t)), int(round(y0 + (y1 - y0) * t))
            if 0 <= X < w and 0 <= Y < h:
                px[Y, X] = IDX[c]
                pts.append((X, Y))
        return pts

    for y in range(fork_y, h):
        px[y, tx] = IDX["e1"]
        px[y, tx - 1] = IDX["e2"]
    px[h - 1, tx - 2] = IDX["e1"]
    px[h - 1, tx + 1] = IDX["e0"]
    branch_px = []
    angles = [-2.5, -1.95, -1.25, -0.65]
    for a in angles:
        a += rng.uniform(-0.15, 0.15)
        L = R * rng.uniform(0.8, 1.05)
        ex, ey = cx + math.cos(a) * L, cy + math.sin(a) * L * 0.95
        branch_px += line(tx, fork_y, ex, ey, "e1")
        for da in (-0.5, 0.45):
            l2 = L * 0.4
            branch_px += line(ex, ey, ex + math.cos(a + da) * l2, ey + math.sin(a + da) * l2, "e0")
    # twig haze inside the crown
    for y in range(h):
        for x in range(w):
            if px[y, x] != T:
                continue
            dx, dy = (x - cx) / (R + 0.5), (y - cy) / (R + 0.5)
            if dx * dx + dy * dy < 1.0 and ((x * 7 + y * 13 + seed) % 5 == 0):
                px[y, x] = IDX["e0"] if y > cy else IDX["e1"]
    if season_name(season) == "winter":
        for (X, Y) in branch_px:
            if Y > 0 and px[Y - 1, X] == T and (X + Y + seed) % 3:
                px[Y - 1, X] = IDX["n3"]
    return Spr(px, tx, h - 1)


@lru_cache(maxsize=None)
def conifer(seed, size, season):
    s = season_name(season)
    ramp = SEASON_ROLES[s]["pine"]
    snow = SEASON_ROLES[s]["snowcap"]
    rng = _rng(seed)
    tiers = 3 if size < 6 else 4
    hgt = size * 2 + 4
    w = size + 4 + (size % 2 == 0)
    h = hgt + 2
    px = np.full((h, w), T, np.uint8)
    cx = w // 2
    tier_h = (hgt - 1) / tiers
    for t in range(tiers):
        top = int(t * tier_h * 0.85)
        bot = int((t + 1) * tier_h) + 1
        for y in range(top, min(bot + 1, hgt)):
            frac = (y - top + 1) / (bot - top + 1)
            half = int(round(frac * (1.2 + (t + 1) * (size / 2.0) / tiers + 0.4)))
            half = min(half, w // 2)
            for x in range(cx - half, cx + half + 1):
                if 0 <= x < w:
                    d = (x - cx)
                    if y == bot or x == cx + half:
                        c = ramp[0]
                    elif d < -half / 3:
                        c = ramp[2] if (y + x) % 3 else ramp[3]
                    elif d <= 0:
                        c = ramp[2]
                    else:
                        c = ramp[1]
                    if y == bot and (x - cx) % 2 == 0 and x != cx + half:
                        c = ramp[1]
                    px[y, x] = IDX[c]
            if snow and y == top + 1:
                for x in range(cx - half, cx + 1):
                    if 0 <= x < w and px[y, x] != T:
                        px[y, x] = IDX[snow[-1]]
            if snow and y == bot - 1:
                for x in range(cx - half + 1, cx - half // 2):
                    if 0 <= x < w and px[y, x] != T:
                        px[y, x] = IDX[snow[-2]]
    px[0, cx] = IDX[ramp[1]] if not snow else IDX[snow[-1]]
    for y in range(hgt, h):
        px[y, cx] = IDX["e1"]
        px[y, cx - 1] = IDX["e2"] if y == h - 1 else T
    return Spr(px, cx, h - 1)


@lru_cache(maxsize=None)
def acacia(seed, size, season):
    s = season_name(season)
    ramp = SEASON_ROLES[s]["palm"] if s != "autumn" else ["d0", "d1", "d2", "d3", "s4"]
    rng = _rng(seed)
    w, h = size * 3 + 3, size + 8
    cl = []
    for k in range(4):
        cl.append((w / 2 - 0.5 + (k - 1.5) * size * 0.75, 2.5 + rng.uniform(0, 1), max(2, int(size * 0.65))))
    can, mask = _canopy(w, size + 4, cl, ramp, rng)
    px = np.full((h, w), T, np.uint8)
    tx = w // 2
    for y in range(3, h):
        px[y, tx] = IDX["e1"]
    for i in range(4):
        X, Y = tx - 1 - i, 4 + i // 2 + 1
        if 0 <= X < w:
            px[min(Y, h - 1), X] = IDX["e1"]
        X = tx + 1 + i
        if X < w:
            px[min(Y, h - 1), X] = IDX["e0"]
    m = can != T
    sub = px[:size + 4]
    sub[m] = can[m]
    px[h - 1, tx - 1] = IDX["e2"]
    return Spr(px, tx, h - 1)


@lru_cache(maxsize=None)
def palm(seed, size, season):
    s = season_name(season)
    ramp = SEASON_ROLES[s]["palm"]
    w, h = 13, 15
    px = np.full((h, w), T, np.uint8)
    # curved trunk
    pts = [(6 + int(round(math.sin(i / 4.0) * 1.2)), 14 - i) for i in range(10)]
    for (x, y) in pts:
        px[y, x] = IDX["e2"] if y % 2 else IDX["e3"]
        if x + 1 < w:
            px[y, x + 1] = IDX["e1"]
    tx, ty = pts[-1]
    fronds = [(-1.0, 0.1), (-0.7, -0.5), (0.0, -0.7), (0.7, -0.5), (1.0, 0.15), (-0.5, 0.45), (0.55, 0.5)]
    for (dx, dy) in fronds:
        for i in range(1, 6):
            X = int(round(tx + dx * i * 1.1))
            Y = int(round(ty + dy * i + (i * i) * 0.12))
            if 0 <= X < w and 0 <= Y < h:
                c = ramp[3] if dy < 0 else ramp[2]
                if i >= 4:
                    c = ramp[1]
                px[Y, X] = IDX[c]
                if 0 <= Y + 1 < h and px[Y + 1, X] == T:
                    px[Y + 1, X] = IDX[ramp[0]]
    px[ty, tx] = IDX[ramp[4]]
    return Spr(px, 6, h - 1)


@lru_cache(maxsize=None)
def bush(seed, size, season, role="leaf"):
    s = season_name(season)
    ramp = SEASON_ROLES[s][role] or ["e0", "e1", "e2", "e2"]
    rng = _rng(seed)
    w, h = size * 2 + 3, size + 3
    cl = [(w / 2 - 0.5 - size * 0.4, h - size - 0.5, size * 0.7), (w / 2 - 0.5 + size * 0.4, h - size, size * 0.65),
          (w / 2 - 0.5, h - size - 1.2, size * 0.75)]
    can, mask = _canopy(w, h, cl, ramp, rng)
    if not SEASON_ROLES[s][role]:
        can = np.where(can != T, np.where((np.indices(can.shape).sum(0) % 2) == 0, IDX["e1"], T), T).astype(np.uint8)
        top = np.argmax(can != T, axis=0)
        for x in range(w):
            if can[:, x].any() and (can[:, x] != T).any():
                can[top[x], x] = IDX["n3"]
    return Spr(can, w // 2, h - 1)


@lru_cache(maxsize=None)
def mountain(seed, width, season, peak=False):
    """A peak with a lit west face and a shaded east face split by a wandering ridge, gullies on the lit face, a
    jagged snow cap, and a selective outline only on the shaded side."""
    rng = _rng(seed)
    s = season_name(season)
    w = width
    h = int(width * (0.78 if peak else 0.62)) + 2
    px = np.full((h, w), T, np.uint8)
    apex = int(w * rng.uniform(0.4, 0.55))
    top = np.zeros(w, np.float32)
    lslope = (h - 2) / max(apex, 1)
    rslope = (h - 2) / max(w - 1 - apex, 1)
    for x in range(w):
        top[x] = 1 + (apex - x) * lslope if x <= apex else 1 + (x - apex) * rslope
    # jagged shoulders: a small random walk, pinned at the apex and the feet
    walk = np.cumsum(rng.choice([-1, 0, 0, 1], w)).astype(np.float32)
    walk -= np.linspace(walk[0], walk[-1], w)
    pin = np.minimum(np.abs(np.arange(w) - apex) / 3.0, 1.0) * np.minimum(np.minimum(np.arange(w), w - 1 - np.arange(w)) / 3.0, 1.0)
    top = np.clip(np.round(top + walk * 0.9 * pin), 1, h - 1).astype(int)
    top[apex] = 0
    # a sub-peak shoulder on one side for a less symmetric silhouette
    if rng.random() < 0.6:
        sx = int(apex + (w - apex) * 0.55) if rng.random() < 0.5 else int(apex * 0.45)
        for x in range(max(0, sx - 3), min(w, sx + 4)):
            top[x] = max(1, top[x] - (3 - abs(x - sx)))
    ridge = np.zeros(h, int)
    rx = float(apex)
    for y in range(h):
        ridge[y] = int(round(rx))
        rx += rng.uniform(0.0, 0.45)
    snow_line = h * (0.55 if peak else 0.3) + (0 if s != "winter" else h * 0.25)
    if s == "summer" and not peak:
        snow_line = h * 0.22
    gullies = [(int(rng.integers(2, max(3, h - 3))), rng.uniform(0.5, 1.0)) for _ in range(3)]
    for x in range(w):
        for y in range(top[x], h):
            r = ridge[min(y, h - 1)]
            lit = x < r
            yy = y / h
            if lit:
                c = "r3"
                band = yy + ((hash_i(y, seed + 3) % 5) - 2) * 0.03 + (x % 7 == 0) * 0.05
                if band > 0.62:
                    c = "r2"
                    if band > 0.85 and (y + (x // 4)) % 3 == 0:
                        c = "r1"
                elif band > 0.42 and (y + (x // 3)) % 4 == 0:
                    c = "r2"
                if x >= r - 1 and y < h * 0.7:
                    c = "r4"
                for (gy, gs) in gullies:
                    # a gully runs from the ridge down-left
                    gx = r - (y - gy) * 1.6 * gs
                    if y > gy and abs(x - gx) < 0.6 and x < r - 1:
                        c = "r1" if yy > 0.5 else "r2"
            else:
                c = "r1" if yy < 0.75 else "r0"
                if x == r:
                    c = "r2"
            jag = (hash_i(x, seed) % 4) - 1 + (3 if hash_i(x, seed + 1) % 7 == 0 else 0)
            if y < snow_line + jag:
                c = "n3" if lit else ("n1" if x > r else "n2")
                if lit and x >= r - 1:
                    c = "n3"
                if not lit and y >= snow_line + jag - 1:
                    c = "n0"
            # selective outline: east and bottom edges only
            if not lit and (y == top[x] or x == w - 1 or (x + 1 < w and top[x + 1] > y)):
                c = "r0" if y >= snow_line + jag else "n0"
            if y == h - 1:
                c = "r1" if lit else "r0"
            px[y, x] = IDX[c]
    return Spr(px, w // 2, h - 1)


def hash_i(x, seed):
    return (x * 2654435761 + seed * 40503) >> 7 & 0xffff


@lru_cache(maxsize=None)
def rock(seed, size, season):
    rng = _rng(seed)
    w, h = size + 2, size + 1
    px = np.full((h, w), T, np.uint8)
    cl = [(w / 2 - 0.5, h / 2, size / 2.0 + 0.3)]
    can, mask = _canopy(w, h, cl, ["r0", "r1", "r2", "r3", "r4"], rng)
    if season_name(season) == "winter":
        for x in range(w):
            ys = np.nonzero(can[:, x] != T)[0]
            if len(ys):
                can[ys[0], x] = IDX["n3"]
    return Spr(can, w // 2, h - 1)


@lru_cache(maxsize=None)
def reeds(seed, season):
    rng = _rng(seed)
    s = season_name(season)
    w, h = 5, 6
    px = np.full((h, w), T, np.uint8)
    cols = ["d1", "d2"] if s in ("autumn", "winter") else ["g1", "g2"]
    for k in range(3):
        x = 1 + k + int(rng.integers(-1, 1))
        hh = int(rng.integers(3, 6))
        for y in range(h - hh, h):
            px[y, x] = IDX[cols[k % 2]]
        if rng.random() < 0.6:
            px[h - hh, x] = IDX["e1"]   # cattail head
    return Spr(px, 2, h - 1)
