#!/usr/bin/env python3
"""
Generates the CivSim gray-box / silhouette placeholder kit (docs/MVP/17 §6, 08 §2, §4.3) with Pillow.

    uv run --no-project --with pillow python tools/placeholder_art/generate.py

Output: game/assets/placeholder/{terrain,figures,tools,structures,fauna,emblems}/*.png, palette.json and
contact_sheet.png. Everything is flat-shaded geometry from the rules of doc 08: one channel, one meaning (V2) --
hue = civ (tinted in the engine), silhouette = what it is, tint = tier. Deterministic, no randomness.
"""
import json, math, os
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "game", "assets", "placeholder")
S = 4  # supersample

def hexc(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)

class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)
    def P(self, pts): return [(x * S, y * S) for x, y in pts]
    def poly(self, pts, fill, outline=None, width=1):
        self.d.polygon(self.P(pts), fill=fill)
        if outline: self.d.line(self.P(pts + [pts[0]]), fill=outline, width=max(1, int(width * S)), joint="curve")
    def ell(self, cx, cy, rx, ry, fill, outline=None, width=1):
        self.d.ellipse([(cx - rx) * S, (cy - ry) * S, (cx + rx) * S, (cy + ry) * S], fill=fill, outline=outline, width=max(1, int(width * S)))
    def rect(self, x0, y0, x1, y1, fill, outline=None, width=1):
        self.d.rectangle([x0 * S, y0 * S, x1 * S, y1 * S], fill=fill, outline=outline, width=max(1, int(width * S)))
    def rrect(self, x0, y0, x1, y1, r, fill):
        self.d.rounded_rectangle([x0 * S, y0 * S, x1 * S, y1 * S], radius=r * S, fill=fill)
    def line(self, pts, fill, width=1):
        self.d.line(self.P(pts), fill=fill, width=max(1, int(width * S)), joint="curve")
    def arc(self, box, a0, a1, fill, width=1):
        x0, y0, x1, y1 = box
        self.d.arc([x0 * S, y0 * S, x1 * S, y1 * S], a0, a1, fill=fill, width=int(width * S))
    def done(self):
        return self.img.resize((self.w, self.h), Image.LANCZOS)

def save(img, *parts):
    path = os.path.join(OUT, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    return img

INK = hexc("#2b2118")           # outline
# ---------------------------------------------------------------- terrain
HEX_W, HEX_H = 112, 128         # pointy-top hex, size 64 (HexGrid is odd-r offset rows)
BIOMES = {
    # name: (fill, glyph colour, glyph)
    "Ocean": ("#2f6fa3", "#5b95c4", "waves"), "Coast": ("#d8c690", "#8fb7d1", "shore"), "Ice": ("#e8f1f6", "#bcd3df", "flakes"),
    "Tundra": ("#a9b49a", "#7f8c70", "tufts"), "Taiga": ("#4f7a5a", "#2f5a3e", "pines"),
    "TemperateForest": ("#5d8f4a", "#3d6b32", "trees"), "OpenWoodland": ("#8aa854", "#5f7f38", "scatter"),
    "GrasslandSteppe": ("#b6c25c", "#8f9c3f", "grass"), "Savanna": ("#cfb257", "#a88f3a", "acacia"),
    "TropicalForest": ("#2f8a4f", "#1e6a3a", "palms"), "Wetland": ("#6f9a86", "#4d7a68", "reeds"),
    "Desert": ("#e0c38a", "#c4a46a", "dunes"), "Mountain": ("#8c8478", "#6a6358", "peaks"), "Peak": ("#d8dde0", "#a7aeb3", "snowpeaks"),
}
def hex_pts(w=HEX_W, h=HEX_H, inset=0.0):
    cx, cy = w / 2, h / 2
    r = h / 2 - inset
    return [(cx + r * math.cos(math.radians(90 + 60 * i)) * 1.0 * (w / (math.sqrt(3) * h / 2)) * 0.5 * 2 / 2 * 1.0, cy + r * math.sin(math.radians(90 + 60 * i))) for i in range(6)]
def hex_poly(w=HEX_W, h=HEX_H):
    cx, cy, r = w / 2, h / 2, h / 2
    return [(cx + r * math.cos(math.radians(30 + 60 * i)) , cy + r * math.sin(math.radians(30 + 60 * i))) for i in range(6)]
def tile(name, fill, gcol, glyph):
    c = Canvas(HEX_W, HEX_H)
    pts = hex_poly()
    c.poly(pts, hexc(fill), outline=hexc("#00000033".replace("#00000033", "#000000")) if False else None)
    g = hexc(gcol)
    cx, cy = HEX_W / 2, HEX_H / 2
    if glyph == "waves":
        for dy in (-22, 0, 22):
            c.line([(cx - 28 + i * 7, cy + dy + (4 if i % 2 else -4)) for i in range(9)], g, 2.5)
    elif glyph == "shore":
        c.poly([(8, HEX_H * 0.6), (HEX_W - 8, HEX_H * 0.5), (HEX_W - 8, HEX_H - 20), (8, HEX_H - 20)], hexc("#3f7fb0"))
        c.line([(14 + i * 7, HEX_H * 0.58 + (3 if i % 2 else -3)) for i in range(13)], hexc("#ffffff"), 2)
    elif glyph == "flakes":
        for x, y in ((-24, -14), (10, -26), (26, 4), (-8, 18), (-30, 24), (12, 30)): c.poly([(cx + x, cy + y - 5), (cx + x + 5, cy + y), (cx + x, cy + y + 5), (cx + x - 5, cy + y)], g)
    elif glyph == "tufts":
        for x, y in ((-22, -18), (14, -24), (26, 8), (-12, 14), (-30, 22), (6, 30)): c.line([(cx + x, cy + y), (cx + x, cy + y - 8)], g, 2.2); c.line([(cx + x - 5, cy + y), (cx + x - 2, cy + y - 6)], g, 2)
    elif glyph in ("pines", "trees", "scatter", "palms", "acacia"):
        spots = {"pines": ((-22, 8), (0, -10), (22, 12), (-8, 28), (16, -26)), "trees": ((-24, 6), (2, -14), (24, 10), (-4, 26), (20, -24), (-26, -22)),
                 "scatter": ((-22, 10), (14, -16), (24, 20)), "palms": ((-20, 12), (8, -12), (22, 18), (-6, 30)), "acacia": ((-18, 8), (16, -10))}[glyph]
        for x, y in spots:
            px, py = cx + x, cy + y
            if glyph == "pines":
                c.poly([(px, py - 18), (px + 9, py), (px - 9, py)], g); c.poly([(px, py - 10), (px + 11, py + 8), (px - 11, py + 8)], g)
            elif glyph == "acacia":
                c.rect(px - 1.5, py - 4, px + 1.5, py + 10, hexc("#6b4f2a")); c.ell(px, py - 8, 15, 5, g)
            elif glyph == "palms":
                c.line([(px, py + 10), (px, py - 6)], hexc("#6b4f2a"), 2.2)
                for a in (200, 240, 300, 340): c.line([(px, py - 6), (px + 13 * math.cos(math.radians(a)), py - 6 + 9 * math.sin(math.radians(a)))], g, 3)
            else:
                c.ell(px, py - 6, 11, 11, g); c.rect(px - 1.5, py + 3, px + 1.5, py + 11, hexc("#6b4f2a"))
    elif glyph == "grass":
        for x, y in ((-24, -16), (4, -26), (24, 4), (-8, 12), (-28, 24), (12, 28), (-2, -4)):
            for k in (-3, 0, 3): c.line([(cx + x + k, cy + y), (cx + x + k * 1.6, cy + y - 8)], g, 1.8)
    elif glyph == "reeds":
        c.ell(cx, cy + 6, 30, 12, hexc("#4f8fa6"))
        for x, y in ((-22, -6), (-10, -20), (18, -12), (26, 12), (-24, 16)):
            c.line([(cx + x, cy + y + 12), (cx + x, cy + y - 6)], g, 2); c.ell(cx + x, cy + y - 8, 2.4, 5, hexc("#7a5a36"))
    elif glyph == "dunes":
        for dy, a in ((-22, 24), (0, 30), (22, 24)): c.arc((cx - a, cy + dy - 10, cx + a, cy + dy + 10), 200, 340, g, 3)
    elif glyph in ("peaks", "snowpeaks"):
        for x, y, s in ((-18, 14, 24), (10, 6, 30), (28, 24, 18)):
            c.poly([(cx + x, cy + y - s), (cx + x + s * 0.8, cy + y + 8), (cx + x - s * 0.8, cy + y + 8)], g)
            c.poly([(cx + x, cy + y - s), (cx + x + s * 0.28, cy + y - s * 0.55), (cx + x - s * 0.28, cy + y - s * 0.55)], hexc("#ffffff"))
    img = c.done()
    # clip to the hex (glyph drawing may overshoot)
    mask = Canvas(HEX_W, HEX_H); mask.poly(hex_poly(), (255, 255, 255, 255)); m = mask.done().split()[3]
    out = Image.new("RGBA", img.size, (0, 0, 0, 0)); out.paste(img, (0, 0), m)
    ol = Canvas(HEX_W, HEX_H); ol.line(hex_poly() + [hex_poly()[0]], (0, 0, 0, 70), 1.5)
    return Image.alpha_composite(out, ol.done())

# ---------------------------------------------------------------- figures
SKIN = hexc("#d9b99b"); BODY = hexc("#b8b0a4"); WHITE = (255, 255, 255, 255)
def figure(pose, kind):
    """64x64. Body layer grey (hue-free); sash layer white (tinted with the civ colour in the engine)."""
    body, sash = Canvas(64, 64), Canvas(64, 64)
    scale = {"adult": 1.0, "child": 0.7, "elder": 0.94}[kind]
    stoop = 5 if kind == "elder" else 0
    cx = 32; foot = 60
    hh = 40 * scale; hw = 10 * scale
    top = foot - hh
    # legs
    swing = {"stand": 0, "walk": 5, "work": 3}[pose]
    body.line([(cx - 3, foot - hh * 0.45), (cx - 3 - swing, foot)], INK, 3.2 * scale)
    body.line([(cx + 3, foot - hh * 0.45), (cx + 3 + swing, foot)], INK, 3.2 * scale)
    # torso (capsule)
    body.rrect(cx - hw + stoop * 0.3, top + 9 * scale, cx + hw + stoop * 0.3, foot - hh * 0.4, 6 * scale, BODY)
    # head
    body.ell(cx + stoop, top + 5 * scale, 6.2 * scale, 6.2 * scale, SKIN, INK, 1.2)
    # arms
    if pose == "work":
        body.line([(cx + hw - 2, top + 15 * scale), (cx + 13, top + 8 * scale)], INK, 3 * scale)
        body.line([(cx - hw + 2, top + 15 * scale), (cx + 8, top + 20 * scale)], INK, 3 * scale)
    elif pose == "walk":
        body.line([(cx + hw - 2, top + 15 * scale), (cx + hw + 4, top + 25 * scale)], INK, 3 * scale)
        body.line([(cx - hw + 2, top + 15 * scale), (cx - hw - 4, top + 24 * scale)], INK, 3 * scale)
    else:
        body.line([(cx + hw - 1, top + 14 * scale), (cx + hw + 1, top + 26 * scale)], INK, 3 * scale)
        body.line([(cx - hw + 1, top + 14 * scale), (cx - hw - 1, top + 26 * scale)], INK, 3 * scale)
    # sash (civ colour): diagonal band across the torso
    sash.poly([(cx - hw, top + 12 * scale), (cx - hw, top + 17 * scale), (cx + hw, top + 26 * scale), (cx + hw, top + 21 * scale)], WHITE)
    return body.done(), sash.done()

def compose(*layers):
    out = Image.new("RGBA", layers[0].size, (0, 0, 0, 0))
    for l in layers: out = Image.alpha_composite(out, l)
    return out

def warrior_overlay():
    c = Canvas(64, 64)
    c.line([(46, 56), (46, 8)], hexc("#6b4f2a"), 2.4)
    c.poly([(46, 2), (49, 10), (43, 10)], hexc("#8a8f94"), INK, 1)
    c.line([(24, 14), (40, 14)], hexc("#b23a2a"), 2.2)  # warpaint stripe on the brow
    return c.done()

# ---------------------------------------------------------------- tools (tiers t0..t7, 08 §2)
TIERS = {"t0": "#8a7d6b", "t1": "#7a6e5c", "t2": "#8b8f92", "t3": "#c8742c", "t4": "#d8aa3a", "t5": "#9aa3ab", "t6": "#4b5560", "t7": "#2fd0c0"}
def tool(name, tint):
    c = Canvas(64, 64); t = hexc(tint); wood = hexc("#7a5a36")
    if name == "axe":
        c.line([(18, 52), (44, 14)], wood, 3.4); c.poly([(38, 10), (54, 20), (46, 30), (36, 22)], t, INK, 1.2)
    elif name == "sickle":
        c.line([(16, 52), (30, 34)], wood, 3.4); c.arc((22, 8, 56, 42), 190, 350, t, 5)
    elif name == "spear":
        c.line([(14, 56), (46, 12)], wood, 2.8); c.poly([(46, 4), (52, 16), (40, 14)], t, INK, 1.2)
    elif name == "basket":
        c.poly([(14, 26), (50, 26), (44, 52), (20, 52)], hexc("#b89458"), INK, 1.4)
        for x in (24, 32, 40): c.line([(x, 28), (x - (32 - x) * -0.0, 51)], hexc("#8c6d3a"), 1.4)
        c.arc((16, 10, 48, 40), 180, 360, hexc("#8c6d3a"), 2.4)
    elif name == "hammer":
        c.line([(32, 56), (32, 22)], wood, 3.4); c.rect(16, 10, 48, 24, t, INK, 1.2)
    elif name == "net":
        for i in range(5): c.line([(10 + i * 11, 12), (10 + i * 11, 52)], hexc("#a9a183"), 1.4); c.line([(10, 12 + i * 10), (54, 12 + i * 10)], hexc("#a9a183"), 1.4)
        c.ell(32, 32, 24, 22, (0, 0, 0, 0), INK, 2)
    elif name == "scroll":
        c.rrect(16, 14, 48, 50, 4, hexc("#e8dcb8")); c.rect(12, 12, 52, 18, wood); c.rect(12, 46, 52, 52, wood)
        for y in (24, 30, 36): c.line([(22, y), (42, y)], hexc("#8a7a55"), 1.6)
    elif name == "bow":
        c.arc((6, 6, 58, 58), 280, 80, wood, 3.2); c.line([(52, 12), (52, 52)], hexc("#d9d2bd"), 1.2); c.line([(14, 32), (50, 32)], wood, 1.8); c.poly([(52, 32), (44, 28), (44, 36)], t)
    elif name == "digging_stick":
        c.line([(14, 56), (44, 10)], wood, 4); c.poly([(44, 6), (50, 14), (42, 12)], t, INK, 1)
    return c.done()
TOOL_NAMES = ["axe", "sickle", "spear", "basket", "hammer", "net", "scroll", "bow", "digging_stick"]

# ---------------------------------------------------------------- structures (ids from content/registries/structures.yaml)
def structure(sid):
    c = Canvas(96, 96); earth = hexc("#a68a64"); wood = hexc("#7a5a36"); hide = hexc("#c8b08a"); stone = hexc("#8e8a82"); thatch = hexc("#c9b25a"); clay = hexc("#b8805a"); dark = hexc("#4a3a2a")
    g = hexc("#9ec35a")
    if sid == "hearth":
        c.ell(48, 68, 18, 8, stone, INK, 1.4); c.poly([(40, 66), (48, 40), (56, 66)], hexc("#e8782a")); c.poly([(44, 66), (48, 50), (52, 66)], hexc("#f4d04a"))
    elif sid == "lean_to":
        c.poly([(18, 74), (30, 34), (74, 34), (78, 74)], hide, INK, 1.6); c.line([(30, 34), (30, 76)], wood, 3); c.line([(74, 34), (76, 76)], wood, 3)
    elif sid in ("hide_tent", "seasonal_camp"):
        c.poly([(48, 22), (80, 74), (16, 74)], hide, INK, 1.6); c.poly([(48, 44), (58, 74), (38, 74)], dark)
        c.line([(48, 22), (48, 12)], wood, 2.4)
        if sid == "seasonal_camp": c.ell(70, 82, 6, 3, stone, INK, 1); c.poly([(68, 80), (70, 72), (72, 80)], hexc("#e8782a"))
    elif sid == "pit_house":
        c.ell(48, 66, 32, 14, earth, INK, 1.6); c.poly([(22, 64), (48, 36), (74, 64)], hexc("#8c7a52"), INK, 1.4); c.rect(42, 58, 54, 70, dark)
    elif sid in ("wattle_daub_house", "mudbrick_house", "longhouse"):
        wall = {"wattle_daub_house": clay, "mudbrick_house": hexc("#c9a06a"), "longhouse": hexc("#9a7a52")}[sid]
        w = 66 if sid == "longhouse" else 48
        x0 = 48 - w / 2
        c.rect(x0, 46, x0 + w, 76, wall, INK, 1.6); c.poly([(x0 - 4, 48), (48, 20), (x0 + w + 4, 48)], thatch, INK, 1.6); c.rect(44, 58, 52, 76, dark)
    elif sid == "storage_pit":
        c.ell(48, 62, 22, 10, dark, INK, 1.6); c.ell(48, 60, 17, 7, hexc("#6a5a40"))
    elif sid == "drying_rack":
        c.line([(22, 76), (22, 36)], wood, 3); c.line([(74, 76), (74, 36)], wood, 3); c.line([(20, 38), (76, 38)], wood, 3)
        for x in (32, 44, 56, 66): c.poly([(x, 40), (x + 4, 40), (x + 3, 58), (x + 1, 58)], hexc("#b4584a"))
    elif sid == "granary":
        c.ell(48, 56, 22, 24, hexc("#c9b278"), INK, 1.6); c.poly([(22, 40), (48, 18), (74, 40)], thatch, INK, 1.6); c.rect(40, 66, 56, 78, dark)
    elif sid == "kiln":
        c.poly([(24, 78), (30, 42), (66, 42), (72, 78)], clay, INK, 1.6); c.ell(48, 42, 18, 8, clay, INK, 1.4); c.ell(48, 66, 8, 10, hexc("#4a2a1a")); c.line([(48, 40), (50, 24)], hexc("#7a7a7a"), 3)
    elif sid == "quern":
        c.ell(48, 66, 24, 10, stone, INK, 1.6); c.ell(48, 58, 20, 8, hexc("#a7a39a"), INK, 1.4); c.line([(48, 58), (62, 44)], wood, 3)
    elif sid == "thorn_barrier":
        for i in range(10):
            x = 14 + i * 7.5; c.poly([(x, 70), (x + 3.5, 46), (x + 7, 70)], hexc("#4f6b2c"), INK, 1); c.line([(x + 3.5, 52), (x + 8, 48)], hexc("#2f4a1a"), 1.4)
    elif sid == "palisade":
        for i in range(9):
            x = 12 + i * 8.5; c.poly([(x, 78), (x, 36), (x + 4.25, 28), (x + 8.5, 36), (x + 8.5, 78)], hexc("#8c6a3c"), INK, 1.2)
    elif sid == "lookout_post":
        c.line([(30, 80), (38, 40)], wood, 3.4); c.line([(66, 80), (58, 40)], wood, 3.4); c.rect(30, 32, 66, 42, hexc("#9a7a4c"), INK, 1.4)
        c.line([(34, 80), (62, 52)], wood, 2); c.line([(62, 80), (34, 52)], wood, 2); c.ell(48, 24, 6, 6, SKIN, INK, 1)
    elif sid == "marker":
        c.poly([(40, 78), (44, 30), (52, 30), (56, 78)], stone, INK, 1.6); c.line([(46, 44), (50, 44)], dark, 2)
    elif sid == "shrine":
        c.rect(24, 64, 72, 76, stone, INK, 1.6); c.rect(30, 52, 66, 64, hexc("#a7a39a"), INK, 1.4); c.poly([(48, 30), (62, 52), (34, 52)], hexc("#d8c890"), INK, 1.4); c.ell(48, 44, 3, 3, hexc("#e8782a"))
    elif sid == "weir":
        c.poly([(8, 60), (88, 60), (88, 80), (8, 80)], hexc("#4f8fa6")); c.line([(14, 54), (14, 82)], wood, 3); 
        for i in range(8): c.line([(16 + i * 9, 58), (22 + i * 9, 76)], wood, 2.2)
    elif sid == "field":
        c.poly([(8, 66), (48, 36), (88, 66), (48, 90)], hexc("#a5864a"), INK, 1.4)
        for i in range(5): c.line([(18 + i * 8, 70 - i * 6), (58 + i * 8 - 12, 82 - i * 6 + 4)], g, 2.4)
    elif sid == "pasture":
        c.poly([(8, 66), (48, 36), (88, 66), (48, 90)], hexc("#8fb35a"), INK, 1.4); c.line([(8, 66), (48, 36), (88, 66), (48, 90), (8, 66)], wood, 2.2)
    elif sid == "orchard":
        for x, y in ((28, 62), (52, 52), (66, 70), (40, 78)): c.rect(x - 2, y - 2, x + 2, y + 12, wood); c.ell(x, y - 6, 12, 12, hexc("#4d8f3a"), INK, 1.2); c.ell(x - 4, y - 8, 2.2, 2.2, hexc("#c43a2a")); c.ell(x + 4, y - 4, 2.2, 2.2, hexc("#c43a2a"))
    elif sid == "megalith":
        for x, h in ((24, 30), (40, 44), (58, 36)): c.poly([(x, 78), (x + 2, 78 - h), (x + 14, 78 - h + 3), (x + 16, 78)], stone, INK, 1.6)
        c.rect(22, 44, 72, 52, hexc("#a7a39a"), INK, 1.6)
    elif sid == "totem":
        c.rect(42, 20, 56, 80, hexc("#9a7a4c"), INK, 1.6)
        for y, col in ((26, "#c43a2a"), (44, "#2a6f9a"), (62, "#d8aa3a")): c.ell(49, y, 9, 7, hexc(col), INK, 1.2); c.ell(46, y - 1, 1.6, 1.6, INK); c.ell(52, y - 1, 1.6, 1.6, INK)
    elif sid == "signal_post":
        c.line([(48, 80), (48, 22)], wood, 3.6); c.poly([(48, 22), (74, 30), (48, 38)], hexc("#d55e00"), INK, 1.2)
    else:
        c.rect(24, 30, 72, 76, stone, INK, 1.6)
    return c.done()

# ---------------------------------------------------------------- fauna (ids from content/registries/fauna.yaml)
def beast(fid):
    c = Canvas(64, 64)
    P = {  # body, leg length, head offset, features
        "deer": ("#a8743c", 12), "aurochs": ("#3a2e24", 12), "wild_boar": ("#5a4636", 9), "bison": ("#5a3e28", 11), "horse": ("#8c5a32", 15),
        "reindeer": ("#8c8478", 12), "wild_goat_sheep": ("#c8c0b0", 10), "gazelle_antelope": ("#cfa56a", 15), "camel_llama": ("#c4a066", 17),
    }
    if fid == "small_game_birds":
        c.ell(30, 40, 12, 8, hexc("#8c7a5a"), INK, 1.2); c.ell(42, 32, 5, 5, hexc("#8c7a5a"), INK, 1.2); c.poly([(46, 32), (53, 34), (46, 36)], hexc("#d8aa3a")); c.poly([(18, 38), (8, 30), (20, 44)], hexc("#6a5a40"))
        c.line([(28, 47), (28, 56)], INK, 1.6); c.line([(34, 47), (34, 56)], INK, 1.6)
        return c.done()
    col, leg = P[fid]; col = hexc(col); L = leg
    by = 54 - L - 8
    c.ell(30, by, 17 if fid != "boar" else 15, 9, col, INK, 1.4)                    # body
    for x in (18, 24, 38, 44): c.line([(x, by + 6), (x + (1 if x > 30 else -1), 54)], INK, 2.4)  # legs
    hx, hy = 48, by - 6
    c.line([(40, by - 2), (hx, hy)], col, 6)                                         # neck
    c.ell(hx + 4, hy, 6, 4.6, col, INK, 1.3)                                         # head
    c.line([(13, by - 2), (7, by + 4)], INK, 1.8)                                    # tail
    if fid == "deer": c.line([(hx + 2, hy - 4), (hx - 2, hy - 14), (hx + 4, hy - 20)], INK, 1.6); c.line([(hx - 2, hy - 14), (hx - 8, hy - 18)], INK, 1.6)
    if fid == "reindeer":
        for dx in (0, 5): c.line([(hx + dx, hy - 4), (hx + dx - 4, hy - 16), (hx + dx + 2, hy - 22)], INK, 1.8)
    if fid == "aurochs": c.line([(hx + 2, hy - 4), (hx - 2, hy - 12)], hexc("#e8e0cc"), 2.6); c.line([(hx + 6, hy - 4), (hx + 10, hy - 12)], hexc("#e8e0cc"), 2.6)
    if fid == "bison": c.ell(34, by - 6, 11, 9, col, INK, 1.2); c.ell(hx + 3, hy + 1, 7, 6, hexc("#3a2a1c"), INK, 1.2)
    if fid == "wild_boar": c.line([(hx + 8, hy + 2), (hx + 12, hy - 3)], hexc("#e8e0cc"), 2.2)
    if fid == "horse": c.line([(40, by - 6), (46, hy - 6)], hexc("#2b2118"), 3)
    if fid == "wild_goat_sheep": c.arc((hx - 4, hy - 14, hx + 8, hy + 2), 200, 40, INK, 2.2)
    if fid == "gazelle_antelope": c.line([(hx + 2, hy - 4), (hx - 1, hy - 17)], INK, 1.8); c.line([(hx + 5, hy - 4), (hx + 2, hy - 17)], INK, 1.8)
    if fid == "camel_llama": c.ell(30, by - 8, 7, 6, col, INK, 1.2); c.line([(40, by - 2), (hx + 2, hy - 8)], col, 5)
    return c.done()
FAUNA = ["deer", "aurochs", "wild_boar", "bison", "horse", "reindeer", "wild_goat_sheep", "gazelle_antelope", "camel_llama", "small_game_birds"]
def predator(kind):
    c = Canvas(64, 64); col = hexc("#6a5f56" if kind == "wolf" else "#b88a3a")
    c.ell(30, 38, 17, 8, col, INK, 1.4)
    for x in (18, 24, 38, 44): c.line([(x, 42), (x + (1 if x > 30 else -1), 54)], INK, 2.6)
    c.ell(50, 32, 7, 5.2, col, INK, 1.3); c.poly([(48, 27), (50, 20), (53, 27)], col, INK, 1); c.line([(13, 36), (6, 28)], INK, 2.2)
    c.ell(53, 31, 1.2, 1.2, hexc("#ffdd55")); 
    return c.done()

# ---------------------------------------------------------------- civ emblems (Okabe-Ito, 08 §4.3)
CIVS = [("Orange", "#E69F00", "sun"), ("SkyBlue", "#56B4E9", "wave"), ("BluishGreen", "#009E73", "leaf"), ("Yellow", "#F0E442", "star"),
        ("Blue", "#0072B2", "mountain"), ("Vermilion", "#D55E00", "flame"), ("ReddishPurple", "#CC79A7", "moon"), ("NeutralDark", "#4D4D4D", "antler")]
def emblem(shape, col):
    c = Canvas(48, 48); f = hexc(col); k = WHITE if shape != "star" else INK
    c.ell(24, 24, 22, 22, f, INK, 2)
    if shape == "sun":
        c.ell(24, 24, 7, 7, k)
        for a in range(0, 360, 45): c.line([(24 + 10 * math.cos(math.radians(a)), 24 + 10 * math.sin(math.radians(a))), (24 + 16 * math.cos(math.radians(a)), 24 + 16 * math.sin(math.radians(a)))], k, 2.4)
    elif shape == "wave":
        for dy in (-7, 0, 7): c.line([(10 + i * 4.6, 24 + dy + (2.4 if i % 2 else -2.4)) for i in range(8)], k, 2.4)
    elif shape == "leaf":
        c.poly([(24, 8), (38, 24), (24, 40), (10, 24)], k); c.line([(24, 12), (24, 38)], f, 2)
    elif shape == "star":
        pts = []
        for i in range(10):
            r = 15 if i % 2 == 0 else 6.5; a = math.radians(-90 + 36 * i); pts.append((24 + r * math.cos(a), 24 + r * math.sin(a)))
        c.poly(pts, k)
    elif shape == "mountain":
        c.poly([(8, 36), (20, 14), (28, 28), (33, 20), (42, 36)], k)
    elif shape == "flame":
        c.poly([(24, 8), (34, 24), (30, 38), (18, 38), (14, 26), (22, 22)], k); c.poly([(24, 24), (28, 32), (24, 38), (20, 32)], f)
    elif shape == "moon":
        c.ell(24, 24, 13, 13, k); c.ell(30, 21, 11, 11, f)
    elif shape == "antler":
        c.line([(24, 38), (24, 20)], k, 2.6); c.line([(24, 24), (14, 10)], k, 2.6); c.line([(24, 24), (34, 10)], k, 2.6); c.line([(19, 17), (11, 18)], k, 2.2); c.line([(29, 17), (37, 18)], k, 2.2)
    return c.done()

# ---------------------------------------------------------------- run
def main():
    os.makedirs(OUT, exist_ok=True)
    sheet_items = []
    tiles = []
    for name, (fill, gcol, glyph) in BIOMES.items():
        im = tile(name, fill, gcol, glyph); save(im, "terrain", f"{name}.png"); tiles.append((name, im))
    figs = []
    for kind in ("adult", "child", "elder"):
        for pose in ("stand", "walk", "work"):
            b, s = figure(pose, kind); save(b, "figures", f"{kind}_{pose}_body.png"); save(s, "figures", f"{kind}_{pose}_sash.png")
            figs.append((f"{kind}_{pose}", compose(b, Image.new("RGBA", s.size, (0, 0, 0, 0)), tint(s, hexc("#E69F00")))))
    save(warrior_overlay(), "figures", "warrior_overlay.png")
    wb, ws = figure("stand", "adult"); figs.append(("warrior", compose(wb, tint(ws, hexc("#D55E00")), warrior_overlay())))
    tools = []
    for n in TOOL_NAMES:
        for t, col in (("t0", TIERS["t0"]), ("t3", TIERS["t3"]), ("t5", TIERS["t5"])):
            im = tool(n, col); save(im, "tools", f"{n}_{t}.png")
            if t == "t0": tools.append((n, im))
    structs = []
    for sid in ["hearth", "lean_to", "hide_tent", "pit_house", "wattle_daub_house", "mudbrick_house", "longhouse", "storage_pit", "drying_rack", "granary", "kiln", "quern",
                "thorn_barrier", "palisade", "lookout_post", "seasonal_camp", "marker", "shrine", "weir", "field", "pasture", "orchard", "megalith", "totem", "signal_post"]:
        im = structure(sid); save(im, "structures", f"{sid}.png"); structs.append((sid, im))
    beasts = []
    for fid in FAUNA: im = beast(fid); save(im, "fauna", f"{fid}.png"); beasts.append((fid, im))
    for k in ("wolf", "lion"): im = predator(k); save(im, "fauna", f"{k}.png"); beasts.append((k, im))
    ems = []
    for n, col, shape in CIVS: im = emblem(shape, col); save(im, "emblems", f"civ_{n}.png"); ems.append((n, im))
    palette = {"civs": [{"slot": i + 1, "name": n, "hex": col, "emblem": shape, "border": b} for i, (n, col, shape) in enumerate(CIVS)
                        for b in [["solid", "long_dash", "dot", "dash_dot", "double", "zigzag", "short_dash", "chain"][i]]],
               "tool_tiers": TIERS, "biomes": {k: v[0] for k, v in BIOMES.items()},
               "season_modulate": {"spring": "#f4fff0", "summer": "#ffffff", "autumn": "#ffe2b8", "winter": "#dfe8f2"},
               "winter_desaturate": 0.45}
    with open(os.path.join(OUT, "palette.json"), "w") as f: json.dump(palette, f, indent=2)
    contact_sheet(tiles, figs, tools, structs, beasts, ems)

def tint(img, col):
    r, g, b, _ = col
    px = img.copy(); data = []
    for pr, pg, pb, pa in px.getdata(): data.append((r * pr // 255, g * pg // 255, b * pb // 255, pa))
    px.putdata(data); return px

def contact_sheet(tiles, figs, tools, structs, beasts, ems):
    font = ImageFont.load_default()
    rows = [("Terrain (one hex per biome; engine tints for season)", tiles, 112), ("Figures: adult/child/elder x stand/walk/work + warrior (sash tinted by civ colour)", figs, 64),
            ("Tools at tier t0 (tint changes with method tier: t3 copper, t5 iron)", tools, 64), ("Structures", structs, 96), ("Fauna and predators", beasts, 64), ("Civ emblems (Okabe-Ito)", ems, 48)]
    W = 1500; pad = 10; y = pad
    layout = []
    for title, items, size in rows:
        x = pad; ymax = y + 18; cell_h = (128 if size == 112 else size) + 16
        layout.append(("title", title, pad, y)); y += 18
        for name, im in items:
            if x + im.width + pad > W: x = pad; y += cell_h
            layout.append(("img", im, x, y, name)); x += im.width + pad
        y += cell_h + 6
    sheet = Image.new("RGBA", (W, y + pad), hexc("#3a3a40")); d = ImageDraw.Draw(sheet)
    for item in layout:
        if item[0] == "title": d.text((item[2], item[3]), item[1], fill=(255, 255, 255, 255), font=font)
        else:
            _, im, x, yy, name = item; sheet.alpha_composite(im, (x, yy)); d.text((x, yy + im.height + 1), name[:14], fill=(200, 200, 200, 255), font=font)
    sheet.save(os.path.join(OUT, "contact_sheet.png"))

if __name__ == "__main__":
    main()
    print("wrote", os.path.abspath(OUT))
