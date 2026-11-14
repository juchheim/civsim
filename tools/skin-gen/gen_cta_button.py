"""Generates the research CTA ("Start in Primary") button art (docs/MVP/34-era-ui-skins.md section 5).

    uv run --no-project --with numpy --with pillow python tools/skin-gen/gen_cta_button.py

Writes game/assets/ui/cta/ (each 288x110, shown at 144x55 and stretched horizontally by the game):
    cta_plate.png      ready: terracotta plate, gold border, cream end ornaments
    cta_plate_hot.png  hovered: lighter and warmer
    cta_plate_off.png  disabled: dark muted plate
The 84 px end caps are drawn as-is; the 120 px middle is tiled, so it is uniform horizontally and its grain repeats seamlessly.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
from gen_title_button import SS, chamfer_poly, diamond, down, from_img, hexc, over, periodic_noise, stroke, to_img  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "game", "assets", "ui", "cta")
W, H, CAP, MID = 288, 110, 84, 120

LOOKS = {
    "normal": dict(top="#EC9660", bot="#D2702F", edge_hi="#F6D58E", edge_lo="#B8741F", orn="#FFE4B0", inner="#FFF0CF", inner_a=0.55, orn_a=1.0),
    "hot": dict(top="#F6AE7A", bot="#E08342", edge_hi="#FFE7A8", edge_lo="#D08A2A", orn="#FFF0CC", inner="#FFF6DD", inner_a=0.7, orn_a=1.0),
    "off": dict(top="#4C3225", bot="#3A251B", edge_hi="#8A6A48", edge_lo="#5E452F", orn="#A08262", inner="#8A6A48", inner_a=0.35, orn_a=0.55),
}


def volute(draw, cx, cy, r0, turns, sign, scale):
    n = 500
    pts, widths = [], []
    for i in range(n + 1):
        t = i / n
        r = r0 * (1 - t) ** 1.15 + 5.0 * t
        phi = math.radians(-90.0 - 360.0 * turns * t)
        pts.append((cx + r * math.cos(phi), cy - sign * r * math.sin(phi)))
        widths.append((15.0 - 9.0 * t) * scale)
    stroke(draw, pts, widths)
    ex, ey = pts[-1]
    rr = 6.5 * scale * 1.6
    draw.ellipse([ex - rr, ey - rr, ex + rr, ey + rr], fill=255)


def end_ornament():
    """Left-cap ornament, as an L mask at final resolution (CAP x H): a diamond, a short stem, and a tiny lyre."""
    cw, ch = CAP * SS, H * SS
    img = Image.new("L", (cw, ch), 0)
    d = ImageDraw.Draw(img)
    cy = ch / 2
    # drawn facing right-to-left, then flipped: the stem leaves the diamond and ends in two curls
    dx = 196.0
    diamond(d, dx, cy, 25, 255)
    stroke(d, [(x, cy) for x in np.linspace(dx - 30, 82, 200)], np.linspace(11, 8, 200))
    for sign in (1, -1):
        volute(d, 82, cy - sign * 26, 26, 1.55, sign, 0.62)
    d.ellipse([dx - 70, cy - 5.5, dx - 59, cy + 5.5], fill=255)
    img = img.transpose(Image.FLIP_LEFT_RIGHT)
    return from_img(down(img, (CAP, H)))


def make_plate(kind):
    k = LOOKS[kind]
    top, bot = hexc(k["top"]), hexc(k["bot"])
    y = np.linspace(0, 1, H)[:, None, None]
    fill = top * (1 - y) + bot * y

    # grain: periodic over the 120 px middle so the tiled stretch never shows a seam
    n1 = periodic_noise(MID, H, 21, 1.2)
    n2 = periodic_noise(MID, H, 22, 2.6)
    cols = (np.arange(W) - CAP) % MID
    grain = (n1 * 0.05 + n2 * 0.06)[:, cols]
    fill = fill * (1 + grain[..., None])
    # darken only inside the caps, so the middle stays uniform
    xs = np.arange(W)
    edge = np.clip(np.maximum((CAP - xs) / CAP, (xs - (W - CAP - 1)) / CAP), 0, 1)
    fill = fill * (1 - 0.10 * edge[None, :, None] ** 2)
    fill = np.clip(fill, 0, 255)

    sil = Image.new("L", (W * SS, H * SS), 0)
    ImageDraw.Draw(sil).polygon([(px * SS, py * SS) for px, py in chamfer_poly(W, H, 3, 17)], fill=255)
    alpha = from_img(down(sil, (W, H)))
    rgb = fill.copy()

    # outer border (gold gradient)
    m = Image.new("L", (W * SS, H * SS), 0)
    pts = [(px * SS, py * SS) for px, py in chamfer_poly(W, H, 5, 16)]
    ImageDraw.Draw(m).line(pts + [pts[0]], fill=255, width=5 * SS, joint="curve")
    border = from_img(down(m, (W, H)))
    grad = hexc(k["edge_hi"]) * (1 - y) + hexc(k["edge_lo"]) * y
    rgb, alpha = over(rgb, alpha, grad, border)

    # inner hairline
    m = Image.new("L", (W * SS, H * SS), 0)
    pts = [(px * SS, py * SS) for px, py in chamfer_poly(W, H, 14, 12)]
    ImageDraw.Draw(m).line(pts + [pts[0]], fill=255, width=2 * SS, joint="curve")
    rgb, alpha = over(rgb, alpha, hexc(k["inner"]), from_img(down(m, (W, H))) * k["inner_a"])

    # soft top light
    hl = np.zeros((H, W))
    hl[22:24, CAP // 2:W - CAP // 2] = 0.10
    rgb, alpha = over(rgb, alpha, hexc("#FFFFFF"), hl * (alpha > 0.5))

    # end ornaments, with a dark halo for separation from the plate
    left = end_ornament()
    orn = np.zeros((H, W))
    orn[:, :CAP] = left
    orn[:, W - CAP:] = left[:, ::-1]
    halo = from_img(to_img(orn).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(2.0))) * 0.45
    rgb, alpha = over(rgb, alpha, hexc("#5A2208" if kind != "off" else "#120805"), halo * (alpha > 0.5))
    rgb, alpha = over(rgb, alpha, hexc(k["orn"]), orn * k["orn_a"])

    out = np.dstack([np.clip(rgb, 0, 255), np.clip(alpha, 0, 1) * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def main():
    os.makedirs(OUT, exist_ok=True)
    for kind, name in (("normal", "cta_plate"), ("hot", "cta_plate_hot"), ("off", "cta_plate_off")):
        make_plate(kind).save(os.path.join(OUT, name + ".png"))
    print("wrote", OUT)


if __name__ == "__main__":
    main()
