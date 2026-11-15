"""Generates the themed mouse cursors (docs/MVP/36-cursors.md).

    uv run --no-project --with numpy --with pillow python tools/skin-gen/gen_cursors.py [--preview OUT.png]

Writes game/assets/ui/cursor/<state>.png (40x40, 1x), <state>@1.5x.png (60x60) and <state>@2x.png (80x80):
    arrow      default pointer                       Godot CURSOR_ARROW
    hand       something you can click               CURSOR_POINTING_HAND
    grab       dragging / panning                    CURSOR_DRAG
    drop       a dragged thing can be dropped here   CURSOR_CAN_DROP
    ibeam      text field                            CURSOR_IBEAM
    forbidden  a dragged thing cannot go here        CURSOR_FORBIDDEN
    target     pick a map tile (Beacon, decree)      CURSOR_CROSS
Era tint: each cursor also gets <state>_accent[@scale].png (RGB = the original gold at that pixel, alpha = how much of the
pixel is gold). The game recolours the gold to the viewer's age tint at run time (UiCursors.cs); forbidden has no gold and no mask.
Style: bone fill, dark oxblood outline (reads on light and dark), gold accents in the brand colours of the
title and CTA plates, soft drop shadow. Hotspots (1x pixels) are in HOTSPOTS below and must match
game/Scripts/Ui/UiCursors.cs. Deterministic; drawn at 4x per scale and downsampled.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "game", "assets", "ui", "cursor")
SS = 4
BASE = 40  # logical canvas, px
SCALES = {"": 1.0, "@1.5x": 1.5, "@2x": 2.0}

# Hotspot of each cursor in 1x pixels; the game multiplies by the asset scale. Keep in sync with UiCursors.cs.
HOTSPOTS = {
    "arrow": (4, 3),
    "hand": (16, 3),
    "grab": (19, 20),
    "drop": (19, 20),
    "ibeam": (20, 20),
    "forbidden": (20, 20),
    "target": (20, 20),
}

BONE_TOP = "#FBF1DC"
BONE_BOT = "#E6CFA4"
OUTLINE = "#1E0C06"
GOLD = "#D9A35A"
GOLD_HI = "#F4C27A"
TERRA = "#C9562C"
TERRA_HI = "#E57A45"


def hexc(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float64) / 255.0


class Canvas:
    """Shapes in 1x units, rendered to a 4x-per-scale mask."""

    def __init__(self, scale):
        self.k = scale * SS
        self.n = int(round(BASE * self.k))

    def mask(self):
        return Image.new("L", (self.n, self.n), 0)

    def p(self, x, y):
        return (x * self.k, y * self.k)

    def poly(self, img, pts):
        ImageDraw.Draw(img).polygon([self.p(*q) for q in pts], fill=255)

    def ellipse(self, img, cx, cy, rx, ry):
        ImageDraw.Draw(img).ellipse([self.p(cx - rx, cy - ry), self.p(cx + rx, cy + ry)], fill=255)

    def rrect(self, img, x0, y0, x1, y1, r):
        ImageDraw.Draw(img).rounded_rectangle([self.p(x0, y0), self.p(x1, y1)], radius=r * self.k, fill=255)

    def line(self, img, a, b, w):
        """A stroke with round caps."""
        d = ImageDraw.Draw(img)
        d.line([self.p(*a), self.p(*b)], fill=255, width=max(1, int(round(w * self.k))))
        for q in (a, b):
            self.ellipse(img, q[0], q[1], w / 2, w / 2)

    def ring(self, img, cx, cy, r, w):
        d = ImageDraw.Draw(img)
        d.ellipse([self.p(cx - r - w / 2, cy - r - w / 2), self.p(cx + r + w / 2, cy + r + w / 2)], fill=255)
        d.ellipse([self.p(cx - r + w / 2, cy - r + w / 2), self.p(cx + r - w / 2, cy + r - w / 2)], fill=0)

    def diamond(self, img, cx, cy, r):
        self.poly(img, [(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)])


def arr(img):
    return np.asarray(img).astype(np.float64) / 255.0


def dilate(m, r):
    """Round dilation by r hi-res pixels (max over a disk of shifts)."""
    r = int(math.ceil(r))
    out = m.copy()
    pad = np.pad(m, r)
    h, w = m.shape
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r + 0.5:
                out = np.maximum(out, pad[r + dy:r + dy + h, r + dx:r + dx + w])
    return out


def erode(m, r):
    return 1.0 - dilate(1.0 - m, r)


def over(rgb, a, color, mask):
    m = mask[..., None]
    return rgb * (1 - m) + color * m, a + mask * (1 - a)


def finish(cv, body, accents=(), details=(), fill_top=BONE_TOP, fill_bot=BONE_BOT, outline_w=1.6):
    """body: union mask of the silhouette. accents: (mask, colour_top, colour_bot) painted inside and outlined.
    details: dark line masks drawn on top (finger gaps etc.)."""
    k = cv.k
    sil = body.copy()
    for m, _, _ in accents:
        sil = np.maximum(sil, m)
    outline = dilate(sil, outline_w * k)
    # soft shadow: outline shifted down-right and blurred
    sh = Image.fromarray((outline * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2 * k))
    sx, sy = int(round(1.0 * k)), int(round(1.5 * k))
    sh = np.pad(arr(sh), ((sy, 0), (sx, 0)))[:cv.n, :cv.n]  # shift down-right, no wrap-around
    n = cv.n
    rgb = np.zeros((n, n, 3))
    a = np.zeros((n, n))
    rgb, a = over(rgb, a, hexc("#000000"), sh * 0.38)
    rgb, a = over(rgb, a, hexc(OUTLINE), outline)
    y = (np.arange(n) / n)[:, None, None]
    grad = hexc(fill_top) * (1 - y) + hexc(fill_bot) * y
    rgb, a = over(rgb, a, grad, body)
    for m, ct, cb in accents:
        rgb, a = over(rgb, a, hexc(OUTLINE), np.clip(dilate(m, 0.9 * k), 0, 1) * sil)
        g = hexc(ct) * (1 - y) + hexc(cb) * y
        rgb, a = over(rgb, a, g, m)
    for m in details:
        rgb, a = over(rgb, a, hexc(OUTLINE), m)
    img = np.dstack([rgb, a])
    # un-premultiply-free: colours were composited straight; store RGBA
    out = Image.fromarray(np.clip(img * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA")
    return out.resize((n // SS, n // SS), Image.LANCZOS)


# ---------------------------------------------------------------- shapes

def make_arrow(cv):
    tx, ty = HOTSPOTS["arrow"]
    s = 1.18
    base = [(0, 0), (0, 23), (5.4, 18.2), (9.2, 26.6), (12.6, 25.1), (8.9, 16.9), (16.2, 16.9)]
    pts = [(tx + x * s, ty + y * s) for x, y in base]
    body = cv.mask()
    cv.poly(body, pts)
    b = arr(body)
    # gold hairline inset along the head, like the inner hairline on the plates
    inner = erode(b, 2.6 * cv.k) - erode(b, 3.6 * cv.k)
    head = cv.mask()
    cv.poly(head, [(tx - 2, ty - 2), (tx - 2, ty + 22 * s), (tx + 18 * s, ty + 18 * s)])
    inner = np.clip(inner, 0, 1) * arr(head)
    # a gold diamond at the tail, the flourish
    tail = cv.mask()
    cv.diamond(tail, tx + 10.9 * s, ty + 25.0 * s, 2.3)
    return finish(cv, b, accents=[(arr(tail), GOLD_HI, GOLD)], details=[]), inner


def make_arrow_full(cv):
    img, inner = make_arrow(cv)
    # paint the inset hairline in gold after the fact (it sits inside the bone body)
    n = cv.n
    hl = Image.fromarray((np.clip(inner, 0, 1) * 255).astype(np.uint8)).resize((n // SS, n // SS), Image.LANCZOS)
    hl = arr(hl)[..., None] * 0.9
    px = np.asarray(img).astype(np.float64) / 255.0
    px[..., :3] = px[..., :3] * (1 - hl) + hexc(GOLD) * hl
    return Image.fromarray(np.clip(px * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA")


def cuff(cv, x0, y0, x1, y1):
    m = cv.mask()
    cv.rrect(m, x0, y0, x1, y1, 1.6)
    return arr(m)


def make_hand(cv):
    body = cv.mask()
    cv.rrect(body, 13.2, 3, 18.8, 21, 2.8)          # index finger, tip is the hotspot
    cv.rrect(body, 11, 14.5, 29.5, 30, 4.5)          # fist
    for x0, x1, top in ((18.6, 22.6, 12.6), (22.4, 26.2, 13.6), (26.0, 29.6, 15.4)):
        cv.rrect(body, x0, top, x1, top + 9, 2.0)    # curled fingers
    cv.ellipse(body, 11.2, 21.5, 3.6, 4.6)           # thumb
    b = arr(body)
    det = cv.mask()
    for x, y0 in ((18.6, 14.5), (22.5, 15.0), (26.1, 16.6)):
        cv.line(det, (x, y0), (x, y0 + 4.2), 0.9)
    cv.line(det, (12.2, 20.5), (15.6, 22.6), 0.9)    # thumb crease
    c = cuff(cv, 13.0, 29.0, 28.0, 35.0)
    dm = cv.mask()
    cv.diamond(dm, 20.5, 32.0, 1.7)
    img = finish(cv, b, accents=[(c, GOLD_HI, GOLD)], details=[arr(det) * b, arr(dm)])
    return img


def make_grab(cv, badge=False):
    body = cv.mask()
    cv.rrect(body, 9.5, 13, 29.5, 29, 5)             # back of the fist
    for i, x in enumerate((12.4, 17.3, 22.2, 27.0)):
        cv.ellipse(body, x, 13.6 + (0.6 if i in (0, 3) else 0), 2.9, 3.4)  # knuckles
    cv.ellipse(body, 9.0, 21.5, 3.4, 5.0)            # thumb tucked at the side
    b = arr(body)
    det = cv.mask()
    for x in (14.85, 19.75, 24.6):
        cv.line(det, (x, 12.5), (x, 18.0), 0.9)
    c = cuff(cv, 12.5, 28.0, 27.0, 34.0)
    dm = cv.mask()
    cv.diamond(dm, 19.75, 31.0, 1.7)
    accents = [(c, GOLD_HI, GOLD)]
    details = [arr(det) * b, arr(dm)]
    if badge:
        bm = cv.mask()
        cv.ellipse(bm, 31.0, 31.0, 5.6, 5.6)
        accents.append((arr(bm), GOLD_HI, GOLD))
        plus = cv.mask()
        cv.line(plus, (28.3, 31.0), (33.7, 31.0), 1.5)
        cv.line(plus, (31.0, 28.3), (31.0, 33.7), 1.5)
        details.append(arr(plus))
    return finish(cv, b, accents=accents, details=details)


def make_ibeam(cv):
    body = cv.mask()
    cv.rrect(body, 18.6, 8, 21.4, 32, 0.8)
    cv.rrect(body, 14.5, 6.6, 25.5, 9.6, 1.4)
    cv.rrect(body, 14.5, 30.4, 25.5, 33.4, 1.4)
    b = arr(body)
    dm = cv.mask()
    cv.diamond(dm, 20, 20, 3.0)
    return finish(cv, b, accents=[(arr(dm), GOLD_HI, GOLD)], outline_w=1.3)


def make_forbidden(cv):
    m = cv.mask()
    cv.ring(m, 20, 20, 10.5, 3.6)
    cv.line(m, (12.8, 27.2), (27.2, 12.8), 3.6)
    b = arr(m)
    return finish(cv, b, fill_top=TERRA_HI, fill_bot=TERRA)


def make_target(cv):
    m = cv.mask()
    cv.ring(m, 20, 20, 9.5, 2.6)
    for (a, bb) in (((20, 3.5), (20, 9.0)), ((20, 31.0), (20, 36.5)), ((3.5, 20), (9.0, 20)), ((31.0, 20), (36.5, 20))):
        cv.line(m, a, bb, 2.6)
    b = arr(m)
    dm = cv.mask()
    cv.diamond(dm, 20, 20, 2.6)
    return finish(cv, b, accents=[(arr(dm), GOLD_HI, GOLD)])


MAKERS = {
    "arrow": make_arrow_full,
    "hand": make_hand,
    "grab": make_grab,
    "drop": lambda cv: make_grab(cv, badge=True),
    "ibeam": make_ibeam,
    "forbidden": make_forbidden,
    "target": make_target,
}


def render_with_accent(make, scale):
    """The cursor plus its accent mask: render with the gold as white and as black; the difference is how gold each pixel is,
    and (default - black) / mask is the gold's own colour there (so the gradient and the arrow's hairline survive a retint)."""
    global GOLD, GOLD_HI
    saved = GOLD, GOLD_HI
    try:
        base = make(Canvas(scale))
        GOLD = GOLD_HI = "#FFFFFF"
        white = make(Canvas(scale))
        GOLD = GOLD_HI = "#000000"
        black = make(Canvas(scale))
    finally:
        GOLD, GOLD_HI = saved
    d, w, k = (np.asarray(i).astype(np.float64) / 255.0 for i in (base, white, black))
    m = np.clip((w[..., :3] - k[..., :3]).mean(axis=2), 0, 1)
    m[m < 2 / 255] = 0
    if m.max() == 0:
        return base, None
    old = np.where(m[..., None] > 0, np.clip((d[..., :3] - k[..., :3]) / np.maximum(m[..., None], 1e-6), 0, 1), 0)
    mask = Image.fromarray(np.clip(np.dstack([old, m]) * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA")
    return base, mask


# Era tints, in the order of the sim's age ids (game/Scripts/Ui/UiSkins.cs).
ERA_TINTS = ["#C8A06A", "#B7855A", "#8FAE6A", "#CD8F46", "#8FA1B3", "#D1A94E", "#D8B972", "#C57A3E", "#8DB1CF", "#4FD6C2"]


def retint(base, mask, tint):
    """Same maths as UiCursors.Retint in C#: out = base + mask * (tint * luma(old) / luma(GOLD) - old)."""
    b = np.asarray(base).astype(np.float64) / 255.0
    mk = np.asarray(mask).astype(np.float64) / 255.0
    old, m = mk[..., :3], mk[..., 3:4]
    luma = lambda c: c @ np.array([0.299, 0.587, 0.114])
    ratio = (luma(old) / luma(hexc(GOLD)))[..., None]
    new = np.clip(hexc(tint) * ratio, 0, 1)
    out = b.copy()
    out[..., :3] = np.clip(b[..., :3] + m * (new - old), 0, 1)
    return Image.fromarray(np.clip(out * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA")


def preview_eras(path):
    """The arrow, hand, drop and target in all ten age tints on a dark and a light strip."""
    names = ["arrow", "hand", "drop", "target"]
    cell = 96
    W, H = cell * len(ERA_TINTS), cell * len(names) * 2
    sheet = Image.new("RGBA", (W, H))
    for half, bg in enumerate([(28, 20, 16), (235, 225, 205)]):
        for r, name in enumerate(names):
            base = Image.open(os.path.join(OUT, f"{name}@2x.png"))
            mask = Image.open(os.path.join(OUT, f"{name}_accent@2x.png"))
            for c, tint in enumerate(ERA_TINTS):
                tile = Image.new("RGBA", (cell, cell), bg + (255,))
                tile.alpha_composite(retint(base, mask, tint), (8, 8))
                sheet.paste(tile, (c * cell, (half * len(names) + r) * cell))
    sheet.save(path)


def preview(path):
    """Every cursor at 2x on a light, a dark and a busy strip, with the hotspot marked in red."""
    names = list(MAKERS)
    cell = 96
    bgs = [(235, 225, 205), (28, 20, 16), None]
    W, H = cell * len(names), cell * len(bgs)
    sheet = Image.new("RGBA", (W, H))
    rng = np.random.default_rng(7)
    busy = (rng.random((H // 8, W // 8, 3)) * 255).astype(np.uint8)
    busy = Image.fromarray(busy, "RGB").resize((W, H), Image.NEAREST).convert("RGBA")
    for r, bg in enumerate(bgs):
        for c, name in enumerate(names):
            box = (c * cell, r * cell, (c + 1) * cell, (r + 1) * cell)
            tile = busy.crop(box) if bg is None else Image.new("RGBA", (cell, cell), bg + (255,))
            cur = Image.open(os.path.join(OUT, f"{name}@2x.png"))
            ox, oy = 8, 8
            tile.alpha_composite(cur, (ox, oy))
            hx, hy = HOTSPOTS[name]
            ImageDraw.Draw(tile).point((ox + hx * 2, oy + hy * 2), fill=(255, 0, 0, 255))
            sheet.paste(tile, box[:2])
    sheet.save(path)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, make in MAKERS.items():
        for suffix, scale in SCALES.items():
            img, mask = render_with_accent(make, scale)
            img.save(os.path.join(OUT, f"{name}{suffix}.png"), optimize=True)
            if mask is not None:
                mask.save(os.path.join(OUT, f"{name}_accent{suffix}.png"), optimize=True)
    if "--preview" in sys.argv:
        preview(sys.argv[sys.argv.index("--preview") + 1])
    if "--preview-eras" in sys.argv:
        preview_eras(sys.argv[sys.argv.index("--preview-eras") + 1])
    print(f"wrote {len(MAKERS) * len(SCALES)} cursors (and their accent masks) to {OUT}")


if __name__ == "__main__":
    main()
