#!/usr/bin/env python3
"""
In-context mock: draws a real match frame (see `civsim-harness view --dump-frame`) in three looks so the art direction
can be judged at map scale, not from a contact sheet.

    civsim-harness view --pack civsim.pack --minutes 40 --viewer -1 --dump-frame frame.json
    uv run --no-project --with pillow --with numpy python tools/placeholder_art/mock_scene.py frame.json OUTDIR [settlement]

Looks: A flat diorama (the kit as is), B soft-lit diorama (hill shading, soft shadows, season grade, vignette: what a Godot
shader pass would add), C parchment map (ink pawns and drawings on aged paper). All from the same sprites and frame.
"""
import base64, json, math, os, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

KIT = os.path.join(os.path.dirname(__file__), "..", "..", "game", "assets", "placeholder")
BIOMES = ["Ocean", "Coast", "Ice", "Tundra", "Taiga", "TemperateForest", "OpenWoodland", "GrasslandSteppe", "Savanna", "TropicalForest", "Wetland", "Desert", "Mountain", "Peak"]
HERDS = ["deer", "aurochs", "wild_boar", "bison", "horse", "reindeer", "wild_goat_sheep", "gazelle_antelope", "camel_llama", "small_game_birds"]
STRUCT = ["hearth", "lean_to", "hide_tent", "pit_house", "wattle_daub_house", "mudbrick_house", "longhouse", "storage_pit", "drying_rack", "granary", "kiln", "quern",
          "thorn_barrier", "lookout_post", "weir", "field", "pasture", "orchard"]
CIVS = ["#E69F00", "#56B4E9", "#009E73", "#F0E442", "#0072B2", "#D55E00", "#CC79A7", "#4D4D4D"]
WORK = {1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12}   # activities that play the work pose

def rgb(h): h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))
_cache = {}
def sprite(*parts):
    k = "/".join(parts)
    if k not in _cache: _cache[k] = Image.open(os.path.join(KIT, *parts)).convert("RGBA")
    return _cache[k]
def tint(img, col):
    r, g, b = col; px = np.array(img).astype(np.float32)
    px[..., 0] *= r / 255; px[..., 1] *= g / 255; px[..., 2] *= b / 255
    return Image.fromarray(px.astype(np.uint8), "RGBA")
def scaled(img, f): return img.resize((max(1, int(img.width * f)), max(1, int(img.height * f))), Image.LANCZOS)
def b64(s, dtype): return np.frombuffer(base64.b64decode(s), dtype=dtype)

class Scene:
    def __init__(self, d, cx, cy, cols, rows, W):
        self.d, self.W = d, W
        self.w, self.h = d["width"], d["height"]
        self.x0, self.y0 = max(0, min(self.w - cols, cx - cols // 2)), max(0, min(self.h - rows, cy - rows // 2))
        self.cols, self.rows = cols, rows
        self.biome = b64(d["biome"], np.uint8); self.elev = b64(d["elevation"], np.uint8).astype(np.int32)
        self.flags = np.array(d["flags"]); self.owner = np.array(d["owner"]); self.claim = np.array(d["claim"])
        self.row_h = W * 0.8660254
        self.size = (int(cols * W + W), int(rows * self.row_h + W * 0.35))
    def tile_px(self, t):
        x, y = t % self.w, t // self.w
        return ((x - self.x0) * self.W + self.W / 2 + (self.W / 2 if y & 1 else 0), (y - self.y0) * self.row_h + self.W * 0.58)
    def unit_px(self, X, Y): return ((X / 1024 - self.x0) * self.W, (Y / 1024 - self.y0) * self.row_h + self.W * 0.1)
    def tiles(self):
        for y in range(self.y0, self.y0 + self.rows):
            for x in range(self.x0, self.x0 + self.cols):
                yield y * self.w + x
    def neighbors(self, t):
        x, y = t % self.w, t // self.w
        d = ([(-1, 0), (1, 0), (-1, -1), (0, -1), (-1, 1), (0, 1)] if y & 1 == 0 else [(-1, 0), (1, 0), (0, -1), (1, -1), (0, 1), (1, 1)])
        return [(ny * self.w + nx) for dx, dy in d for nx, ny in [(x + dx, y + dy)] if 0 <= nx < self.w and 0 <= ny < self.h]

def paste_center(canvas, im, cx, cy, shadow=None):
    x, y = int(cx - im.width / 2), int(cy - im.height / 2)
    if shadow:
        sh = Image.new("RGBA", canvas.size, (0, 0, 0, 0)); sd = ImageDraw.Draw(sh)
        rx, ry = im.width * shadow[0], im.height * shadow[1]
        sd.ellipse([cx - rx, cy + im.height * 0.28 - ry, cx + rx, cy + im.height * 0.28 + ry], fill=(0, 0, 0, shadow[2]))
        canvas.alpha_composite(sh.filter(ImageFilter.GaussianBlur(im.height * 0.07)))
    canvas.alpha_composite(im, (x, y))

def layout_structures(scene, s, rnd):
    """The sim has no building positions, so the mock arranges each settlement's structures round its tile (the real
    renderer will do the same from the counts)."""
    out = []
    tile = s["tile"]; cx, cy = scene.tile_px(tile)
    W = scene.W
    counts = s["structures"]
    slots = []
    for ring, (r, n) in enumerate(((0.0, 1), (0.30, 6), (0.52, 10))):
        for i in range(n): slots.append((r * W * math.cos(2 * math.pi * i / n + ring), r * W * 0.8 * math.sin(2 * math.pi * i / n + ring)))
    order = [0, 2, 1, 3, 4, 5, 6, 9, 7, 8, 10, 11, 13]
    si = 0
    for k in order:
        for _ in range(min(counts[k], 3 if k not in (0,) else 1)):
            if si >= len(slots): break
            dx, dy = slots[si]; si += 1
            out.append((STRUCT[k], cx + dx, cy + dy))
    if counts[12] > 0:  # thorn barrier: six fence segments round the settlement
        for i in range(6):
            a = 2 * math.pi * i / 6 + 0.5
            out.append(("thorn_barrier", cx + 0.80 * W * math.cos(a), cy + 0.62 * W * math.sin(a)))
    return sorted(out, key=lambda o: o[2])

def draw_rivers(scene, canvas, color, width):
    d = ImageDraw.Draw(canvas)
    for t in scene.tiles():
        if not scene.flags[t] & 1: continue
        for n in scene.neighbors(t):
            if n > t and scene.flags[n] & 1:
                a, b = scene.tile_px(t), scene.tile_px(n)
                mid = ((a[0] + b[0]) / 2 + (a[1] - b[1]) * 0.08, (a[1] + b[1]) / 2 + (b[0] - a[0]) * 0.08)
                d.line([a, mid, b], fill=color, width=width, joint="curve")

def draw_borders(scene, canvas, wash_alpha, line_w, dashed, own_outline=True):
    over = Image.new("RGBA", canvas.size, (0, 0, 0, 0)); od = ImageDraw.Draw(over)
    W = scene.W; R = W / math.sqrt(3)
    def corners(t):
        cx, cy = scene.tile_px(t)
        return [(cx + R * math.cos(math.radians(30 + 60 * i)) * 1.0, cy + R * math.sin(math.radians(30 + 60 * i)) * 1.0) for i in range(6)]
    for t in scene.tiles():
        o = scene.owner[t]
        if o < 0: continue
        col = rgb(CIVS[o % 8])
        od.polygon(corners(t), fill=col + (wash_alpha,))
    for t in scene.tiles():
        o = scene.owner[t]
        if o < 0: continue
        col = rgb(CIVS[o % 8]); c = corners(t)
        nb = scene.neighbors(t)
        # an edge is a border where the neighbour across it has another owner; pair neighbours with corner edges by direction
        cx, cy = scene.tile_px(t)
        for i in range(6):
            a, b = c[i], c[(i + 1) % 6]
            mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
            best, bd = None, 1e9
            for n in nb:
                nx, ny = scene.tile_px(n)
                dd = (nx - mx) ** 2 + (ny - my) ** 2
                if dd < bd: bd, best = dd, n
            if best is None or bd > (W * 0.6) ** 2 or scene.owner[best] != o:
                if dashed and (i % 2): continue
                od.line([a, b], fill=col + (255,), width=line_w)
    canvas.alpha_composite(over)

def draw_world(scene, style):
    W = scene.W
    bg = {"A": (47, 111, 163), "B": (40, 92, 140), "C": (226, 210, 170)}[style]
    canvas = Image.new("RGBA", scene.size, bg + (255,))
    biome_img = {}
    for t in scene.tiles():
        name = BIOMES[scene.biome[t]]
        im = biome_img.get(name)
        if im is None:
            im = scaled(sprite("terrain", f"{name}.png"), W / 112); biome_img[name] = im
        cx, cy = scene.tile_px(t)
        tile_im = im
        if style == "B":
            # hill shading from the elevation gradient (light from the north-west) and a little variation per tile
            e = scene.elev[t]
            nbs = scene.neighbors(t)
            ref = np.mean([scene.elev[n] for n in nbs[2:4]]) if len(nbs) >= 4 else e
            shade = np.clip((e - ref) * 2.2, -40, 40)
            jitter = ((t * 2654435761) % 1000) / 1000 * 10 - 5
            arr = np.array(im).astype(np.float32)
            arr[..., :3] = np.clip(arr[..., :3] + shade + jitter, 0, 255)
            tile_im = Image.fromarray(arr.astype(np.uint8), "RGBA")
        elif style == "C":
            arr = np.array(im).astype(np.float32)
            grey = arr[..., :3].mean(axis=2, keepdims=True)
            arr[..., :3] = np.clip((arr[..., :3] * 0.5 + grey * 0.5) * np.array([1.12, 1.04, 0.88]) + 12, 0, 255)
            tile_im = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
        paste_center(canvas, tile_im, cx, cy)
    if style == "C":
        # ink outlines of every tile edge, drawn faintly
        pass
    draw_rivers(scene, canvas, (74, 144, 190, 255) if style != "C" else (70, 90, 120, 255), int(W * (0.09 if style != "C" else 0.05)))
    if style == "A": draw_borders(scene, canvas, 70, max(3, int(W * 0.07)), False)
    if style == "B": draw_borders(scene, canvas, 80, max(4, int(W * 0.09)), False)
    if style == "C": draw_borders(scene, canvas, 85, max(3, int(W * 0.06)), True)
    return canvas

def draw_actors(scene, canvas, style, rnd):
    d = scene.d; W = scene.W
    items = []
    # fields and pastures on the plot tiles
    for tile, kind in d["plots"]:
        if scene.x0 <= tile % scene.w < scene.x0 + scene.cols and scene.y0 <= tile // scene.w < scene.y0 + scene.rows:
            cx, cy = scene.tile_px(tile); items.append(("plot", "field" if kind == 0 else "orchard", cx, cy - W * 0.05, 0))
    for s in d["settlements"]:
        t = s["tile"]
        if not (scene.x0 - 1 <= t % scene.w < scene.x0 + scene.cols + 1 and scene.y0 - 1 <= t // scene.w < scene.y0 + scene.rows + 1): continue
        for name, x, y in layout_structures(scene, s, rnd): items.append(("struct", name, x, y, s["civ"]))
    for X, Y, sp, n in d["herds"]:
        x, y = scene.unit_px(X, Y)
        if 0 <= x < scene.size[0] and 0 <= y < scene.size[1]:
            for j in range(min(4, 1 + n // 25)):
                items.append(("herd", HERDS[sp], x + rnd.uniform(-0.25, 0.25) * W, y + rnd.uniform(-0.15, 0.15) * W, 0))
    for fx, fy, civ, age, act, flags, rung, sett in d["figures"]:
        x, y = scene.unit_px(fx, fy)
        if 0 <= x < scene.size[0] and 0 <= y < scene.size[1]: items.append(("fig", (age, act, flags), x, y, civ))
    items.sort(key=lambda it: it[3])
    k = W / 64.0
    for kind, what, x, y, civ in items:
        if kind == "plot":
            im = scaled(sprite("structures", f"{what}.png"), k * 0.8)
            if style == "C": im = ink(im, fill=None, line=(96, 72, 44))
            paste_center(canvas, im, x, y)
        elif kind == "struct":
            f = {"thorn_barrier": 0.5, "palisade": 0.7}.get(what, 0.85)
            im = scaled(sprite("structures", f"{what}.png"), k * f * (0.95 if style != "C" else 0.9))
            if style == "C": im = ink(im, fill=(240, 228, 192), line=(78, 54, 32))
            paste_center(canvas, im, x, y, shadow=(0.4, 0.12, 70) if style == "B" else None)
        elif kind == "herd":
            im = scaled(sprite("fauna", f"{what}.png"), k * 0.62)
            if style == "C": im = ink(im, fill=None, line=(70, 52, 34))
            paste_center(canvas, im, x, y, shadow=(0.35, 0.1, 60) if style == "B" else None)
        elif kind == "fig":
            age, act, flags = what
            warrior = bool(flags & 128)
            size = "child" if age == 0 else "elder" if age == 2 else "adult"
            pose = "work" if act in WORK else ("walk" if flags & 2 or act >= 15 else "stand")
            body = sprite("figures", f"{size}_{pose}_body.png"); sash = sprite("figures", f"{size}_{pose}_sash.png")
            col = rgb(CIVS[civ % 8]) if flags & 1024 == 0 else (150, 150, 150)
            if style == "C":
                im = ink(body, fill=None, line=(58, 40, 24)); im.alpha_composite(tint(sash, col))
            else:
                im = body.copy(); im.alpha_composite(tint(sash, col))
                if warrior: im.alpha_composite(sprite("figures", "warrior_overlay.png"))
            im = scaled(im, k * 0.8)
            paste_center(canvas, im, x, y - im.height * 0.25, shadow=(0.3, 0.07, 90) if style == "B" else None)

def ink(im, fill, line):
    """Redraw a sprite as an ink-and-wash drawing: its own shading mapped onto a sepia ramp, with a dark ink outline."""
    arr = np.array(im).astype(np.float32)
    lum = (arr[..., 0] * 0.3 + arr[..., 1] * 0.59 + arr[..., 2] * 0.11) / 255.0
    dark, light = np.array(line, np.float32), np.array([236, 220, 180], np.float32)
    t = np.clip(lum, 0, 1) ** 0.7
    rgb_ = dark[None, None, :] * (1 - t[..., None]) + light[None, None, :] * t[..., None]
    out = Image.fromarray(np.concatenate([rgb_, arr[..., 3:]], axis=2).astype(np.uint8), "RGBA")
    alpha = Image.fromarray(arr[..., 3].astype(np.uint8), "L")
    edge = ImageChops.subtract(alpha.filter(ImageFilter.MaxFilter(3)), alpha.filter(ImageFilter.MinFilter(3)))
    lined = Image.new("RGBA", im.size, tuple(line) + (255,)); out.paste(lined, (0, 0), edge.point(lambda v: min(255, v * 3)))
    return out

def finish(canvas, style, season):
    if style == "A": return canvas
    arr = np.array(canvas).astype(np.float32)
    h, w = arr.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    if style == "B":
        grade = {0: (1.02, 1.04, 0.98), 1: (1.04, 1.02, 0.94), 2: (1.06, 0.97, 0.88), 3: (0.92, 0.97, 1.06)}[season]
        arr[..., :3] *= np.array(grade)
        v = 1 - 0.38 * (((xx - w / 2) / (w / 2)) ** 2 + ((yy - h / 2) / (h / 2)) ** 2) / 2
        arr[..., :3] *= v[..., None]
        arr[..., :3] = np.clip((arr[..., :3] - 128) * 1.06 + 128, 0, 255)
    if style == "C":
        rng = np.random.default_rng(7)
        grain = rng.normal(0, 7, (h, w))
        blot = np.array(Image.fromarray((rng.random((h // 24 + 2, w // 24 + 2)) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)).astype(np.float32)
        paper = np.array([238, 222, 184], np.float32)
        arr[..., :3] = arr[..., :3] * 0.68 + paper * 0.32
        arr[..., :3] += grain[..., None] + (blot[..., None] - 128) * 0.10
        v = 1 - 0.35 * (((xx - w / 2) / (w / 2)) ** 4 + ((yy - h / 2) / (h / 2)) ** 4) / 2
        arr[..., :3] *= np.stack([v, v * 0.98, v * 0.9], axis=2)
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")

def main():
    frame, outdir = sys.argv[1], sys.argv[2]
    which = int(sys.argv[3]) if len(sys.argv) > 3 else 0
    os.makedirs(outdir, exist_ok=True)
    d = json.load(open(frame))
    s = d["settlements"][which]
    cx, cy = s["tile"] % d["width"], s["tile"] // d["width"]
    for style, name in (("A", "A_flat_diorama"), ("B", "B_soft_lit_diorama"), ("C", "C_parchment_map")):
        scene = Scene(d, cx, cy, cols=26, rows=18, W=60)
        canvas = draw_world(scene, style)
        draw_actors(scene, canvas, style, random.Random(11))
        out = finish(canvas, style, d["seasonOfYear"]).convert("RGB")
        out.save(os.path.join(outdir, f"{name}.png"))
        # a close crop around the settlement at higher zoom (Z0-ish): same scene, bigger sprites
        z = Scene(d, cx, cy, cols=9, rows=6, W=170)
        c2 = draw_world(z, style); draw_actors(z, c2, style, random.Random(11))
        finish(c2, style, d["seasonOfYear"]).convert("RGB").save(os.path.join(outdir, f"{name}_close.png"))
    print("wrote", outdir)

if __name__ == "__main__":
    main()
