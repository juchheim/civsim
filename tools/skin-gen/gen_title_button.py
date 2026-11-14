"""Generates the title-screen Start Game button art (docs/MVP/34-era-ui-skins.md section 4).

    uv run --no-project --with numpy --with pillow python tools/skin-gen/gen_title_button.py

Writes game/assets/ui/title/:
    start_plate.png      720x144  button plate, normal   (shown at 360x72)
    start_plate_hot.png  720x144  button plate, hovered  (same shape, warmer)
    start_wing.png       220x144  left scroll flourish   (shown at 110x72; the game mirrors it for the right side)
Deterministic; vector parts are drawn at 4x and downsampled.
"""
import math
import os

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

SS = 4
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "game", "assets", "ui", "title")


def hexc(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float64)


def periodic_noise(w, h, seed, beta):
    """Noise that tiles on both axes (filtered in frequency space), roughly in [-1, 1]."""
    rng = np.random.default_rng(seed)
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    f = np.sqrt(fx * fx + fy * fy)
    f[0, 0] = 1.0
    spec = (rng.normal(size=(h, w)) + 1j * rng.normal(size=(h, w))) / f ** (beta / 2.0)
    spec[0, 0] = 0
    n = np.real(np.fft.ifft2(spec))
    return n / np.abs(n).max()


def to_img(mask):
    return Image.fromarray(np.clip(mask * 255, 0, 255).astype(np.uint8), "L")


def from_img(img):
    return np.asarray(img).astype(np.float64) / 255.0


def down(img, size):
    return img.resize(size, Image.LANCZOS)


def over(base_rgb, base_a, color, mask):
    """Composite a solid or per-pixel colour with a [0,1] mask over an RGB+alpha pair."""
    m = mask[..., None]
    rgb = base_rgb * (1 - m) + color * m
    a = base_a + mask * (1 - base_a)
    return rgb, a


# ---------------------------------------------------------------- plate

def chamfer_poly(w, h, d, c):
    return [(d + c, d), (w - d - c, d), (w - d, d + c), (w - d, h - d - c),
            (w - d - c, h - d), (d + c, h - d), (d, h - d - c), (d, d + c)]


def diamond(draw, cx, cy, r, fill, outline=None):
    pts = [(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)]
    draw.polygon(pts, fill=fill, outline=outline)


def make_plate(hot):
    W, H = 720, 144
    top = hexc("#7E3517" if hot else "#4E2013")
    bot = hexc("#4C1D0E" if hot else "#2A110A")
    edge_hi = hexc("#F09A60" if hot else "#D9703A")
    edge_lo = hexc("#B8541F" if hot else "#8A3714")
    gold = hexc("#F4C27A" if hot else "#D9A35A")

    # fill: vertical gradient, darker toward the short ends, grain and mottling
    y = np.linspace(0, 1, H)[:, None, None]
    x = np.linspace(-1, 1, W)[None, :, None]
    fill = top * (1 - y) + bot * y
    fill = fill * (1 - 0.22 * np.abs(x) ** 3)
    grain = periodic_noise(W, H, 11, 1.2) * 0.05 + periodic_noise(W, H, 12, 2.6) * 0.07
    fill = fill * (1 + grain[..., None])
    if hot:  # a soft ember glow up the middle
        glow = np.exp(-((np.linspace(-1, 1, H)[:, None]) ** 2) / 0.35) * np.exp(-(np.linspace(-1, 1, W)[None, :] ** 2) / 1.6)
        fill = fill + (hexc("#FF8A3C") * 0.16) * glow[..., None]
    fill = np.clip(fill, 0, 255)

    # plate silhouette
    sil = Image.new("L", (W * SS, H * SS), 0)
    ImageDraw.Draw(sil).polygon([(px * SS, py * SS) for px, py in chamfer_poly(W, H, 4, 30)], fill=255)
    sil = from_img(down(sil, (W, H)))
    rgb = fill.copy()
    alpha = sil.copy()

    # outer border (gradient terracotta), 6 px
    m = Image.new("L", (W * SS, H * SS), 0)
    d = ImageDraw.Draw(m)
    pts = [(px * SS, py * SS) for px, py in chamfer_poly(W, H, 7, 28)]
    d.line(pts + [pts[0]], fill=255, width=6 * SS, joint="curve")
    border = from_img(down(m, (W, H)))
    grad = edge_hi * (1 - y) + edge_lo * y
    rgb, alpha = over(rgb, alpha, grad, border)

    # inner hairline (gold), 2 px, with bracket corners and centre diamonds
    layer = Image.new("L", (W * SS, H * SS), 0)
    d = ImageDraw.Draw(layer)
    pts = [(px * SS, py * SS) for px, py in chamfer_poly(W, H, 21, 20)]
    d.line(pts + [pts[0]], fill=255, width=2 * SS, joint="curve")
    inner = from_img(down(layer, (W, H))) * 0.7
    rgb, alpha = over(rgb, alpha, gold, inner)

    orn = Image.new("L", (W * SS, H * SS), 0)
    d = ImageDraw.Draw(orn)
    # a dark gap behind each diamond so the hairline breaks around it
    for cx, cy in [(W / 2, 21), (W / 2, H - 21)]:
        d.rectangle([(cx - 22) * SS, (cy - 4) * SS, (cx + 22) * SS, (cy + 4) * SS], fill=0)
    gapmask = from_img(down(orn, (W, H)))
    for cx, cy in [(W / 2, 21), (W / 2, H - 21)]:
        pass
    # corner diamonds on the chamfers and centre diamonds top and bottom
    dm = Image.new("L", (W * SS, H * SS), 0)
    d = ImageDraw.Draw(dm)
    for cx, cy in [(21 + 10, 21 + 10), (W - 31, 31), (31, H - 31), (W - 31, H - 31)]:
        diamond(d, cx * SS, cy * SS, 6.5 * SS, 255)
    for cx, cy in [(W / 2, 21), (W / 2, H - 21)]:
        diamond(d, cx * SS, cy * SS, 8.5 * SS, 255)
        for off in (-22, 22):
            d.ellipse([(cx + off - 2.5) * SS, (cy - 2.5) * SS, (cx + off + 2.5) * SS, (cy + 2.5) * SS], fill=255)
    dmm = from_img(down(dm, (W, H)))
    # dark rim around the diamonds, then the gold fill
    rim = from_img(down(dm, (W, H)).filter(ImageFilter.MaxFilter(5)))
    rgb, alpha = over(rgb, alpha, hexc("#1C0905"), np.clip(rim * 0.85, 0, 1) * (sil > 0))
    rgb, alpha = over(rgb, alpha, gold, dmm)

    # top inner highlight, a faint parchment line
    hl = np.zeros((H, W))
    hl[27:29, 70:W - 70] = 0.10
    hl = from_img(down(to_img(hl), (W, H)))
    rgb, alpha = over(rgb, alpha, hexc("#F5EAD7"), hl * (alpha > 0.5))

    out = np.dstack([np.clip(rgb, 0, 255), np.clip(alpha, 0, 1) * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


# ---------------------------------------------------------------- wing

def stroke(draw, pts, widths):
    for (px, py), w in zip(pts, widths):
        draw.ellipse([px - w / 2, py - w / 2, px + w / 2, py + w / 2], fill=255)


def spiral_pts(cx, cy, r0, turns, r_end, sign, phi0=-90.0, n=900):
    pts, widths = [], []
    for i in range(n + 1):
        t = i / n
        r = r0 * (1 - t) ** 1.15 + r_end * t
        phi = math.radians(phi0 - 360.0 * turns * t)
        pts.append((cx + r * math.cos(phi), cy - sign * r * math.sin(phi)))
        widths.append(15.0 - 9.0 * t)
    return pts, widths


def lens(draw, base, ang_deg, length, wmax):
    a = math.radians(ang_deg)
    d = (math.cos(a), -math.sin(a))
    nrm = (-d[1], d[0])
    left, right = [], []
    for i in range(41):
        t = i / 40
        wv = wmax * math.sin(math.pi * t) ** 0.85
        cx = base[0] + d[0] * length * t
        cy = base[1] + d[1] * length * t
        left.append((cx + nrm[0] * wv, cy + nrm[1] * wv))
        right.append((cx - nrm[0] * wv, cy - nrm[1] * wv))
    draw.polygon(left + right[::-1], fill=255)


def make_wing():
    W, H = 220, 144
    CW, CH = W * SS, H * SS
    cy = CH / 2
    xj = 300.0
    r0 = 118.0

    ink = Image.new("L", (CW, CH), 0)
    d = ImageDraw.Draw(ink)

    # stem: tapers from the plate end out to the volutes
    n = 600
    xs = np.linspace(CW, xj, n)
    stem_w = np.linspace(22, 15, n)
    stroke(d, [(x, cy) for x in xs], stem_w)

    # the two volutes (upper curls up, lower curls down), the lyre shape
    for sign in (1, -1):
        pts, widths = spiral_pts(xj, cy - sign * r0, r0, 1.85, 13.0, sign)
        stroke(d, pts, widths)
        ex, ey = pts[-1]
        d.ellipse([ex - 15, ey - 15, ex + 15, ey + 15], fill=255)

    # leaves along the stem
    for bx, ln, wm in [(720, 130, 30), (540, 105, 25)]:
        for ang in (62, -62):  # fan out nearly square to the stem, tips leaning toward the free end
            lens(d, (bx, cy), 180 - ang if ang > 0 else -180 - ang, ln, wm)
    # beads on the stem
    for bx in (800, 600):
        d.ellipse([bx - 13, cy - 13, bx + 13, cy + 13], fill=255)
    # collar where the wing meets the plate
    d.ellipse([CW - 120, cy - 34, CW - 52, cy + 34], fill=255)

    mask = from_img(down(ink, (W, H)))

    # carve a thin dark channel inside the collar so it reads as a ring
    ring = Image.new("L", (CW, CH), 0)
    rd = ImageDraw.Draw(ring)
    rd.ellipse([CW - 106, cy - 20, CW - 66, cy + 20], fill=255)
    ring = from_img(down(ring, (W, H)))

    # colour: warm gold, lighter near the plate, with an edge highlight and shadow
    xg = np.linspace(0, 1, W)[None, :, None]
    base = hexc("#C8863E") * (1 - xg) + hexc("#E8B468") * xg
    base = np.broadcast_to(base, (H, W, 3)).copy()
    shifted = np.roll(mask, 2, axis=0)
    hi = np.clip(mask * (1 - shifted), 0, 1)
    lo = np.clip(mask * (1 - np.roll(mask, -2, axis=0)), 0, 1)
    rgb = base * (1 - 0.35 * lo[..., None]) + hexc("#FFE6BA") * (0.55 * hi[..., None])
    rgb = rgb * (1 - 0.0) + hexc("#2A110A") * (0.7 * ring[..., None]) * mask[..., None]
    rgb = np.clip(rgb, 0, 255)

    # dark halo so the gold separates from the bright sky behind it
    halo_src = to_img(mask).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(3.2))
    halo = from_img(halo_src) * 0.7
    out_rgb = hexc("#1C0905")[None, None, :] * np.ones((H, W, 3))
    out_rgb = out_rgb * (1 - mask[..., None]) + rgb * mask[..., None]
    alpha = np.clip(halo + mask * (1 - halo), 0, 1)
    out = np.dstack([out_rgb, alpha * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def main():
    os.makedirs(OUT, exist_ok=True)
    make_plate(False).save(os.path.join(OUT, "start_plate.png"))
    make_plate(True).save(os.path.join(OUT, "start_plate_hot.png"))
    make_wing().save(os.path.join(OUT, "start_wing.png"))
    print("wrote", OUT)


if __name__ == "__main__":
    main()
