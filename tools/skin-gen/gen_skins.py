#!/usr/bin/env python3
"""Procedural era UI skins (docs/MVP/34-era-ui-skins.md, section 1).

Writes 30 PNGs: for each age slug, game/assets/ui/skins/<slug>/{panel_tex,corner,crest}.png,
plus a contact sheet at docs/MVP/ui-screens/skins/contact-sheet.png.

Deterministic: fixed seeds, no clocks. Re-run to regenerate everything:

    uv run --no-project --with numpy --with pillow python tools/skin-gen/gen_skins.py

Building blocks
  * Textures are built from periodic noise (white noise filtered in the FFT domain, so every
    layer tiles by construction) plus vector elements drawn with wrap-around. Alpha carries the
    pattern; it is fitted to a target mean with a soft knee under the peak (spec: mean 8-14 %,
    peak <= 35 %).
  * Corners and crests are drawn as signed-distance shapes on a 4x supersampled canvas and
    downscaled with LANCZOS. Corners are mirrored about the diagonal, crests left-right.
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "game" / "assets" / "ui" / "skins"
SHEET = ROOT / "docs" / "MVP" / "ui-screens" / "skins" / "contact-sheet.png"

N = 256                     # texture tile
CORNER = 96
CREST_W, CREST_H = 384, 24
BONE = np.array([0xF5, 0xEA, 0xD7]) / 255.0
TEX_MEAN, TEX_PEAK = 0.11, 0.32


def hexrgb(h: str) -> np.ndarray:
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)]) / 255.0


# ----------------------------------------------------------------------------------------------
# Signed-distance drawing canvas
# ----------------------------------------------------------------------------------------------

def sd_seg(X, Y, p, q):
    px, py = p
    vx, vy = q[0] - px, q[1] - py
    l2 = vx * vx + vy * vy
    if l2 <= 1e-12:
        t = np.zeros_like(X)
    else:
        t = np.clip(((X - px) * vx + (Y - py) * vy) / l2, 0.0, 1.0)
    return np.hypot(X - px - t * vx, Y - py - t * vy), t


def sd_polygon(X, Y, pts):
    pts = np.asarray(pts, float)
    d = np.full(X.shape, np.inf)
    inside = np.zeros(X.shape, bool)
    n = len(pts)
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        dd, _ = sd_seg(X, Y, a, b)
        d = np.minimum(d, dd)
        if a[1] != b[1]:
            cond = ((a[1] > Y) != (b[1] > Y)) & (X < (b[0] - a[0]) * (Y - a[1]) / (b[1] - a[1]) + a[0])
            inside ^= cond
    return np.where(inside, -d, d)


class Ink:
    """A coverage mask in final-pixel coordinates, supersampled `ss` times."""

    def __init__(self, w, h, ss=4, wrap=False):
        self.w, self.h, self.ss, self.wrap = w, h, ss, wrap
        self.a = np.zeros((h * ss, w * ss))

    # generic shape: fn(X, Y) -> signed distance in final px
    def shape(self, fn, bbox, v=1.0, op="max"):
        x0, y0, x1, y1 = bbox
        ss = self.ss
        offs = [(0, 0)]
        if self.wrap:
            offs = [(dx, dy) for dx in (-self.w, 0, self.w) for dy in (-self.h, 0, self.h)]
        for dx, dy in offs:
            ix0 = max(0, int(math.floor((x0 + dx) * ss)) - 2)
            ix1 = min(self.w * ss, int(math.ceil((x1 + dx) * ss)) + 2)
            iy0 = max(0, int(math.floor((y0 + dy) * ss)) - 2)
            iy1 = min(self.h * ss, int(math.ceil((y1 + dy) * ss)) + 2)
            if ix1 <= ix0 or iy1 <= iy0:
                continue
            xs = (np.arange(ix0, ix1) + 0.5) / ss - dx
            ys = (np.arange(iy0, iy1) + 0.5) / ss - dy
            X, Y = np.meshgrid(xs, ys)
            cov = np.clip(0.5 - fn(X, Y) * ss, 0.0, 1.0) * v
            sub = self.a[iy0:iy1, ix0:ix1]
            if op == "max":
                np.maximum(sub, cov, out=sub)
            elif op == "erase":
                np.minimum(sub, 1.0 - cov, out=sub)
            elif op == "add":
                sub += cov
        return self

    def seg(self, p, q, w, w2=None, v=1.0, op="max"):
        w2 = w if w2 is None else w2
        pad = max(w, w2) / 2 + 1
        bbox = (min(p[0], q[0]) - pad, min(p[1], q[1]) - pad, max(p[0], q[0]) + pad, max(p[1], q[1]) + pad)

        def fn(X, Y):
            d, t = sd_seg(X, Y, p, q)
            return d - (w + (w2 - w) * t) / 2
        return self.shape(fn, bbox, v, op)

    def poly(self, pts, w, w2=None, closed=False, v=1.0, op="max"):
        pts = [tuple(map(float, p)) for p in pts]
        if closed:
            pts = pts + [pts[0]]
        w2 = w if w2 is None else w2
        lens = [0.0]
        for a, b in zip(pts, pts[1:]):
            lens.append(lens[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
        total = lens[-1] or 1.0
        for i, (a, b) in enumerate(zip(pts, pts[1:])):
            wa = w + (w2 - w) * lens[i] / total
            wb = w + (w2 - w) * lens[i + 1] / total
            self.seg(a, b, wa, wb, v, op)
        return self

    def circle(self, c, r, v=1.0, op="max"):
        return self.shape(lambda X, Y: np.hypot(X - c[0], Y - c[1]) - r,
                          (c[0] - r - 1, c[1] - r - 1, c[0] + r + 1, c[1] + r + 1), v, op)

    def ring(self, c, r, w, v=1.0, op="max"):
        R = r + w
        return self.shape(lambda X, Y: np.abs(np.hypot(X - c[0], Y - c[1]) - r) - w / 2,
                          (c[0] - R - 1, c[1] - R - 1, c[0] + R + 1, c[1] + R + 1), v, op)

    def fill(self, pts, v=1.0, op="max"):
        pts = np.asarray(pts, float)
        bbox = (pts[:, 0].min() - 1, pts[:, 1].min() - 1, pts[:, 0].max() + 1, pts[:, 1].max() + 1)
        return self.shape(lambda X, Y: sd_polygon(X, Y, pts), bbox, v, op)

    def outline(self, pts, w, v=1.0, op="max"):
        pts = np.asarray(pts, float)
        bbox = (pts[:, 0].min() - w - 1, pts[:, 1].min() - w - 1, pts[:, 0].max() + w + 1, pts[:, 1].max() + w + 1)
        return self.shape(lambda X, Y: np.abs(sd_polygon(X, Y, pts)) - w / 2, bbox, v, op)

    def arc(self, c, r, a0, a1, w, w2=None, n=None, v=1.0, op="max"):
        n = n or max(8, int(abs(a1 - a0) * r / 1.5))
        ts = np.linspace(a0, a1, n + 1)
        return self.poly([(c[0] + r * math.cos(t), c[1] + r * math.sin(t)) for t in ts], w, w2, v=v, op=op)

    def bez(self, p0, p1, p2, p3, w, w2=None, n=28, v=1.0, op="max"):
        return self.poly(bezier(p0, p1, p2, p3, n), w, w2, v=v, op=op)

    # whole-canvas helpers
    def coords(self):
        xs = (np.arange(self.w * self.ss) + 0.5) / self.ss
        ys = (np.arange(self.h * self.ss) + 0.5) / self.ss
        return np.meshgrid(xs, ys)

    def multiply(self, fn):
        X, Y = self.coords()
        self.a *= fn(X, Y)
        return self

    def merge(self, other):
        np.maximum(self.a, other.a, out=self.a)
        return self

    def sym_diag(self):
        np.maximum(self.a, self.a.T, out=self.a)
        return self

    def sym_lr(self):
        np.maximum(self.a, self.a[:, ::-1], out=self.a)
        return self

    def down_box(self):
        s = self.ss
        return np.clip(self.a.reshape(self.h, s, self.w, s).mean(axis=(1, 3)), 0, 1)

    def down_lanczos(self):
        img = Image.fromarray(self.a.astype(np.float32), mode="F").resize((self.w, self.h), Image.LANCZOS)
        return np.clip(np.asarray(img, dtype=np.float64), 0, 1)


def bezier(p0, p1, p2, p3, n=28):
    t = np.linspace(0, 1, n + 1)[:, None]
    P = [np.array(p, float) for p in (p0, p1, p2, p3)]
    pts = (1 - t) ** 3 * P[0] + 3 * (1 - t) ** 2 * t * P[1] + 3 * (1 - t) * t ** 2 * P[2] + t ** 3 * P[3]
    return [tuple(p) for p in pts]


def leaf_pts(base, tip, width, n=18, power=0.8, bend=0.0):
    """Pointed leaf / kernel / spear-head outline from base to tip."""
    b, t = np.array(base, float), np.array(tip, float)
    d = t - b
    L = float(np.hypot(*d))
    u = d / L
    p = np.array([-u[1], u[0]])
    s = np.linspace(0, 1, n)
    half = width / 2 * np.sin(np.pi * s) ** power
    off = bend * np.sin(np.pi * s) * L
    upper = [b + u * L * si + p * (hi + oi) for si, hi, oi in zip(s, half, off)]
    lower = [b + u * L * si - p * (hi - oi) for si, hi, oi in zip(s[::-1], half[::-1], off[::-1])]
    return np.array(upper + lower[1:-1])


def spear_pts(base, tip, width, n=16):
    """Leaf-shaped spear head: widest a third of the way up, straight taper to the point."""
    b, t = np.array(base, float), np.array(tip, float)
    d = t - b
    L = float(np.hypot(*d))
    u = d / L
    p = np.array([-u[1], u[0]])
    s = np.linspace(0, 1, n)
    half = width / 2 * np.where(s < 0.33, np.sin(s / 0.33 * np.pi / 2), (1 - s) / 0.67)
    upper = [b + u * L * si + p * hi for si, hi in zip(s, half)]
    lower = [b + u * L * si - p * hi for si, hi in zip(s[::-1], half[::-1])]
    return np.array(upper + lower[1:-1])


def sd_gear(c, n, r_root, r_tip, phase):
    def fn(X, Y):
        dx, dy = X - c[0], Y - c[1]
        r = np.hypot(dx, dy)
        th = np.arctan2(dy, dx)
        u = ((th - phase) / (2 * np.pi) * n) % 1.0
        e = np.abs(((u + 0.5) % 1.0) - 0.5)          # 0 at tooth centre, 0.5 mid-gap
        k = np.clip((0.30 - e) / 0.12, 0.0, 1.0)      # flat top to 0.18, flank to 0.30
        return r - (r_root + (r_tip - r_root) * k)
    return fn


def sd_circles_union(centres, r):
    def fn(X, Y):
        d = np.full(X.shape, np.inf)
        for cx, cy in centres:
            d = np.minimum(d, np.hypot(X - cx, Y - cy) - r)
        return d
    return fn


def sd_blob(c, r, seed):
    rng = np.random.default_rng(seed)
    ph = rng.uniform(0, 2 * np.pi, 3)
    amp = rng.uniform(0.05, 0.12, 3)

    def fn(X, Y):
        dx, dy = X - c[0], Y - c[1]
        th = np.arctan2(dy, dx)
        rr = r * (1 + amp[0] * np.sin(2 * th + ph[0]) + amp[1] * np.sin(3 * th + ph[1]) + amp[2] * np.sin(5 * th + ph[2]))
        return np.hypot(dx, dy) - rr
    return fn


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# ----------------------------------------------------------------------------------------------
# Periodic texture tools
# ----------------------------------------------------------------------------------------------

def pnoise(n, seed, f_lo, f_hi, beta=0.0, kx=1.0, ky=1.0):
    """Band-limited periodic noise, std 1. Frequencies in cycles per tile.
    kx > 1 suppresses variation along x (streaks running horizontally)."""
    rng = np.random.default_rng(seed)
    F = np.fft.fft2(rng.standard_normal((n, n)))
    f = np.fft.fftfreq(n) * n
    FX, FY = np.meshgrid(f, f)
    r = np.hypot(FX * kx, FY * ky)
    rs = np.where(r == 0, 1.0, r)
    filt = 1 / (1 + (f_lo / rs) ** 4) / (1 + (rs / f_hi) ** 4) * rs ** (-beta)
    filt[0, 0] = 0.0
    out = np.fft.ifft2(F * filt).real
    out -= out.mean()
    return out / (out.std() + 1e-12)


def pblur(a, sigma):
    n0, n1 = a.shape
    fy = np.fft.fftfreq(n0)[:, None]
    fx = np.fft.fftfreq(n1)[None, :]
    g = np.exp(-2 * (np.pi * sigma) ** 2 * (fx ** 2 + fy ** 2))
    return np.fft.ifft2(np.fft.fft2(a) * g).real


def norm(a):
    a = a - a.mean()
    return a / (a.std() + 1e-12)


def emboss(h, light=(-1.0, -1.0)):
    """Directional relief shading of a height field (light from the top-left)."""
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    lx, ly = np.array(light) / np.hypot(*light)
    return norm(-(gx * lx + gy * ly))


def grid(n=N, ss=1):
    x = (np.arange(n * ss) + 0.5) / ss
    return np.meshgrid(x, x)


def box_down(a, s):
    n0, n1 = a.shape
    return a.reshape(n0 // s, s, n1 // s, s).mean(axis=(1, 3))


def pvoronoi(n, pts):
    X, Y = grid(n)
    F1 = np.full((n, n), np.inf)
    F2 = np.full((n, n), np.inf)
    for px, py in pts:
        dx = np.abs(X - px)
        dx = np.minimum(dx, n - dx)
        dy = np.abs(Y - py)
        dy = np.minimum(dy, n - dy)
        d = np.hypot(dx, dy)
        F2 = np.where(d < F1, F1, np.minimum(F2, d))
        F1 = np.minimum(F1, d)
    return F1, F2


def jittered(n, cells, jitter, seed):
    rng = np.random.default_rng(seed)
    step = n / cells
    pts = []
    for j in range(cells):
        for i in range(cells):
            pts.append(((i + 0.5) * step + rng.uniform(-jitter, jitter) * step,
                        (j + 0.5) * step + rng.uniform(-jitter, jitter) * step))
    return [(x % n, y % n) for x, y in pts]


def fit_alpha(a, mean=TEX_MEAN, peak=TEX_PEAK):
    """Scale so the mean hits `mean`; values above 75 % of `peak` are compressed softly."""
    a = np.clip(a, 0, None)
    k = 0.75 * peak

    def knee(x):
        return np.where(x <= k, x, k + (peak - k) * np.tanh((x - k) / (peak - k)))
    lo, hi = 0.0, 50.0 / max(a.mean(), 1e-9)
    for _ in range(80):
        s = (lo + hi) / 2
        if knee(a * s).mean() < mean:
            lo = s
        else:
            hi = s
    return knee(a * s)


def tex_rgb(tint, mix=0.45):
    return tint * (1 - mix) + BONE * mix


def rgba(alpha, rgb):
    """alpha (h,w) 0..1, rgb (3,) or (h,w,3) 0..1 -> uint8 RGBA."""
    h, w = alpha.shape
    rgb = np.broadcast_to(np.asarray(rgb, float), (h, w, 3))
    out = np.zeros((h, w, 4), np.uint8)
    out[..., :3] = np.round(np.clip(rgb, 0, 1) * 255)
    out[..., 3] = np.round(np.clip(alpha, 0, 1) * 255)
    return out


# ----------------------------------------------------------------------------------------------
# Corner / crest scaffolding
# ----------------------------------------------------------------------------------------------

def corner_ink():
    return Ink(CORNER, CORNER, ss=4)


def corner_fade(ink, start=50.0, end=95.0):
    """Strokes along the edges fade out toward the far ends (x for the top edge, y for the left)."""
    return ink.multiply(lambda X, Y: (1 - smoothstep(start, end, X)) * (1 - smoothstep(start, end, Y)))


def crest_ink():
    return Ink(CREST_W, CREST_H, ss=4)


CX, CY = CREST_W / 2, CREST_H / 2


def crest_hairline(ink, x_from, y=11.0, w=1.7, x_to=4.0):
    """Left-half hairline from x_from (near the centre) out to x_to, tapering; faded later."""
    ink.seg((x_from, y), (x_to, y), w, w * 0.55)


def crest_fade(ink, inner=150.0):
    """Fade to transparent at both ends; full strength inside `inner` px of the centre."""
    return ink.multiply(lambda X, Y: smoothstep(4.0, CX - inner + 30, np.minimum(X, CREST_W - X)))


def finish_crest(orn, lines):
    crest_fade(lines)
    orn.merge(lines).sym_lr()
    a = orn.down_lanczos()
    return (a + a[:, ::-1]) / 2          # exact symmetry after resampling


def finish_corner(ink, diag=True):
    if diag:
        ink.sym_diag()
    a = ink.down_lanczos()
    return a


def rope(ink, x0, x1, yc, amp=2.0, period=7.0, w=1.2):
    """Two interleaved sine strands: reads as a twisted cord and survives downscaling."""
    xs = np.linspace(x0, x1, int((x1 - x0) * 2) + 2)
    for ph in (0.0, math.pi):
        ink.poly([(x, yc + amp * math.sin(2 * math.pi * (x - x0) / period + ph)) for x in xs], w)


# ----------------------------------------------------------------------------------------------
# Ages. Each returns (texture alpha, texture rgb, corner alpha, crest alpha).
# ----------------------------------------------------------------------------------------------

def age_dawn(tint, s):
    # Texture: rough pitted rock, ash, faint ochre smudges.
    h = pnoise(N, s, 2, 10, 1.0) + 0.55 * pnoise(N, s + 1, 10, 40, 1.0) + 0.3 * pnoise(N, s + 2, 40, 128, 0.5)
    rel = emboss(h)
    fine = pnoise(N, s + 3, 70, 128)
    pits = smoothstep(1.8, 2.7, pnoise(N, s + 4, 24, 64))
    pit_rim = emboss(-pblur(pits, 1.0))
    smudge = smoothstep(0.5, 1.9, pnoise(N, s + 5, 2, 5, 1.5))
    a = 0.10 + 0.032 * rel + 0.012 * fine + 0.012 * pit_rim
    a = a * (1 - 0.8 * pits) + 0.022 * smudge
    rgb = tex_rgb(tint, 0.55)[None, None, :] * (1 - smudge[..., None] * 0.7) + tint[None, None, :] * smudge[..., None] * 0.7

    # Corner: scratched lines with tally notches, three finger dabs.
    k = corner_ink()
    rng = np.random.default_rng(s + 10)
    pts = [(x, 3.0 + rng.uniform(-0.35, 0.35)) for x in np.arange(1.5, 96, 4.0)]
    k.poly(pts, 2.6, 0.9)
    pts = [(x, 8.0 + rng.uniform(-0.3, 0.3)) for x in np.arange(18, 80, 5.0)]
    k.poly(pts, 1.3, 0.7)
    for i, x in enumerate((40, 44.5, 49, 53.5)):
        k.seg((x + 0.6, 0.8), (x - 0.6, 12.0), 1.5, 1.0)
    k.seg((37.5, 10.5), (56.5, 1.5), 1.4)
    # finger dabs along the diagonal (one on the axis, one each side -> symmetric pair)
    k.shape(sd_blob((18.5, 18.5), 4.4, s + 11), (12, 12, 25, 25))
    k.shape(sd_blob((28.5, 13.5), 3.5, s + 12), (22, 7, 35, 20))
    a_c = finish_corner(k)

    # Crest: ember dot between two charcoal strokes.
    o = crest_ink()
    o.circle((CX, CY), 3.6)
    o.ring((CX, CY), 6.4, 1.0)
    rng = np.random.default_rng(s + 20)
    for j in range(3):                       # overlapping passes make a rough charcoal edge
        dy = rng.uniform(-0.5, 0.5)
        pts = [(x, CY + dy + 0.35 * math.sin(x * 0.37 + j)) for x in np.linspace(CX - 11, CX - 62, 14)]
        o.poly(pts, 3.4 - j * 0.6, 0.8)
    for _ in range(18):                      # ash flecks knocked out of the stroke
        x = rng.uniform(CX - 55, CX - 16)
        o.circle((x, CY + rng.uniform(-1.2, 1.2)), rng.uniform(0.25, 0.45), op="erase")
    L = crest_ink()
    crest_hairline(L, CX - 60, y=CY, w=1.4)
    return a, rgb, a_c, finish_crest(o, L)


def age_hunters(tint, s):
    # Texture: fine hide grain, creases, pores, a faint stitched seam.
    crease = smoothstep(0.82, 1.0, 1 - np.abs(pnoise(N, s, 8, 26, 0.5)))
    grain = pnoise(N, s + 1, 50, 128, 0.0, kx=2.2)
    pores = smoothstep(2.0, 2.9, pnoise(N, s + 2, 90, 128))
    wrinkle = emboss(pnoise(N, s + 3, 6, 24, 1.0))
    a = 0.10 + 0.018 * grain + 0.02 * wrinkle - 0.03 * crease
    a = a * (1 - 0.55 * pores)
    st = Ink(N, N, ss=2, wrap=True)

    def seam(x):
        return 176 + 5 * math.sin(2 * math.pi * x / N + 0.7)
    x = 0.0
    while x < N:
        y0, y1 = seam(x), seam(x + 3.6)
        st.seg((x, y0 - 0.9), (x + 3.6, y1 + 0.9), 1.0)
        x += 6.4
    a = a + 0.075 * st.down_box()
    rgb = tex_rgb(tint, 0.42)

    # Corner: antler beam round the corner with tines curling inward.
    k = corner_ink()
    pts = [(95, 4.0), (24, 4.0)] + [(24 + 20 * math.cos(t), 24 + 20 * math.sin(t))
                                    for t in np.linspace(-math.pi / 2, -3 * math.pi / 4, 10)]
    k.poly(pts[::-1], 3.4, 0.9)          # thick at the corner, thin at the far end
    k.bez((63, 4.5), (62, 9), (58, 13), (51, 13.5), 1.9, 0.6)
    k.bez((43, 4.5), (42, 11), (36, 16.5), (28, 17), 2.3, 0.6)
    # brow tine from the curve, curling toward the panel interior, mirrored -> a pair
    k.bez((10.5, 10.5), (18, 20), (29, 23), (36, 20), 2.6, 0.7)
    a_c = finish_corner(corner_fade(k, 55, 95))

    # Crest: crossed spear tips.
    o = crest_ink()
    tip = np.array([CX - 27, 4.0])
    butt = np.array([CX + 23, 20.5])
    d = (tip - butt) / np.hypot(*(tip - butt))
    head_base = tip - d * 11.5
    o.fill(spear_pts(head_base, tip, 5.6))
    o.seg(tuple(head_base - d * 0.5), tuple(butt), 1.6)
    o.seg(tuple(head_base - d * 2.2 + np.array([-d[1], d[0]]) * 2.2),
          tuple(head_base - d * 2.2 - np.array([-d[1], d[0]]) * 2.2), 1.3)   # binding
    o.seg(tuple(butt - d * 1.2 + np.array([-d[1], d[0]]) * 1.8),
          tuple(butt - d * 1.2 - np.array([-d[1], d[0]]) * 1.8), 1.3)
    L = crest_ink()
    crest_hairline(L, CX - 38, y=CY + 1, w=1.5)
    L.circle((CX - 34, CY + 1), 1.5)
    return a, rgb, a_c, finish_crest(o, L)


def age_roots(tint, s):
    # Texture: basket weave, two strands per cell, over-under per cell, at 2x then box down.
    ss = 2
    X, Y = grid(N, ss)
    P, SW = 16, 8
    cx, cy = np.floor(X / P), np.floor(Y / P)
    h_top = ((cx + cy) % 2) == 0

    def profile(t):
        g = 0.8
        q = (t - g) / (SW - 2 * g)
        return np.sin(np.pi * np.clip(q, 0, 1)) ** 0.6 * ((q > 0) & (q < 1))
    rng = np.random.default_rng(s)
    var_h = rng.uniform(0.82, 1.15, N // SW)
    var_v = rng.uniform(0.82, 1.15, N // SW)
    fib_h = pnoise(N * ss, s + 1, 30, 200, 0.0, kx=8)
    fib_v = pnoise(N * ss, s + 2, 30, 200, 0.0, ky=8)
    bend_h = 0.5 + 0.5 * np.sin(np.pi * (X % P) / P) ** 0.5
    bend_v = 0.5 + 0.5 * np.sin(np.pi * (Y % P) / P) ** 0.5
    bh = profile(Y % SW) * bend_h * (1 + 0.28 * fib_h) * var_h[(np.floor(Y / SW) % (N // SW)).astype(int)]
    bv = profile(X % SW) * bend_v * (1 + 0.28 * fib_v) * var_v[(np.floor(X / SW) % (N // SW)).astype(int)]
    bright = np.where(h_top, bh, bv)
    a = box_down(0.07 + 0.16 * bright, ss)
    clay = smoothstep(0.6, 2.0, pnoise(N, s + 3, 2, 6, 1.5))
    rgb = tex_rgb(tint, 0.45)[None, None, :] * (1 - 0.35 * clay[..., None]) + hexrgb("#C9A27A")[None, None, :] * 0.35 * clay[..., None]

    # Corner: wheat ear on the diagonal, curling tendril with leaves along the edges.
    k = corner_ink()
    k.seg((3.0, 3.0), (33.0, 33.0), 1.7, 1.2)
    for i, t in enumerate((0.36, 0.48, 0.60, 0.72, 0.84)):
        bx = 3 + 30 * t
        base = (bx + 0.5, bx - 0.5)
        ang = math.pi / 4 - 0.5            # kernels hug the stalk, leaning toward the tip
        ln = 6.0 - 0.3 * i
        tip = (base[0] + ln * math.cos(ang), base[1] + ln * math.sin(ang))
        k.fill(leaf_pts(base, tip, 3.6, power=0.6))
        if i >= 2:
            k.seg(tip, (tip[0] + 4.5 * math.cos(ang + 0.1), tip[1] + 4.5 * math.sin(ang + 0.1)), 0.8, 0.5)  # awn
    k.fill(leaf_pts((32, 32), (39.5, 39.5), 3.6, power=0.7))
    k.seg((39, 39), (44, 44), 0.8, 0.5)
    # tendril along the top edge
    pts = [(x, 4.5 + 1.2 * math.sin((x - 12) / 9.0)) for x in np.linspace(12, 95, 40)]
    k.poly(pts, 1.6, 0.8)
    for x, up in ((48, 1), (78, -1)):
        y = 4.5 + 1.2 * math.sin((x - 12) / 9.0)
        k.fill(leaf_pts((x, y), (x + 6.0, y - 4.0 * up), 2.8, power=0.8))
    # curl hanging below the tendril
    th = np.linspace(0, 1.6 * 2 * math.pi, 60)
    r = 5.0 * (1 - th / (th[-1] * 1.12))
    cxy = (62.0, 10.0)
    curl = [(cxy[0] + rr * math.cos(t + math.pi * 1.5), cxy[1] + rr * math.sin(t + math.pi * 1.5)) for t, rr in zip(th, r)]
    k.poly(curl, 1.3, 0.7)
    a_c = finish_corner(corner_fade(k, 55, 95))

    # Crest: seed and two leaves.
    o = crest_ink()
    o.fill(leaf_pts((CX, CY + 6.0), (CX, CY - 6.0), 6.4, power=0.6))
    o.seg((CX, CY + 4.5), (CX, CY - 3.5), 0.8, op="erase")
    lf = leaf_pts((CX - 4.5, CY + 1.5), (CX - 27, CY - 2.0), 7.4, power=0.75, bend=0.05)
    o.fill(lf)
    o.bez((CX - 5, CY + 1.5), (CX - 12, CY + 0.6), (CX - 19, CY - 0.6), (CX - 25, CY - 1.8), 0.8, 0.4, op="erase")
    L = crest_ink()
    crest_hairline(L, CX - 30, y=CY - 1, w=1.5)
    L.circle((CX - 33, CY - 1), 1.5)
    return a, rgb, a_c, finish_crest(o, L)


def age_bronze(tint, s):
    # Texture: overlapping hammer dimples, faint verdigris.
    F1, F2 = pvoronoi(N, jittered(N, 12, 0.36, s))
    h = pblur((F1 / 11.0) ** 2, 0.7)
    rel = emboss(h)
    ridge = smoothstep(2.5, 0.0, F2 - F1)
    fine = pnoise(N, s + 1, 60, 128)
    verd = smoothstep(0.9, 2.2, pnoise(N, s + 2, 3, 10, 1.0))
    a = 0.10 + 0.017 * rel + 0.012 * fine + 0.005 * ridge + 0.012 * verd * np.clip(fine, 0, None)
    rgb = tex_rgb(tint, 0.42)[None, None, :] * (1 - 0.6 * verd[..., None]) + hexrgb("#9CC9AE")[None, None, :] * 0.6 * verd[..., None]

    # Corner: Greek-key meander along the edges from a riveted corner block.
    k = corner_ink()
    k.outline([(1.5, 1.5), (15.5, 1.5), (15.5, 15.5), (1.5, 15.5)], 1.6)
    k.ring((8.5, 8.5), 3.4, 1.4)
    k.circle((8.5, 8.5), 1.3)
    cell, y0 = 4.0, 4.0
    unit = [(0, 2), (0, 0), (4, 0), (4, 1), (2, 1), (2, 2), (6, 2)]
    pts = []
    x0 = 15.5
    for u in range(5):
        for (ux, uy) in unit[(0 if u == 0 else 1):]:
            pts.append((x0 + (ux + 6 * u) * cell, y0 + uy * cell))
    pts = [(15.5, y0 + 2 * cell)] + pts
    k.poly(pts, 1.6)
    k.seg((15.5, 1.5), (95, 1.5), 1.0)
    k.seg((15.5, 14.5), (95, 14.5), 1.0)
    a_c = finish_corner(corner_fade(k, 50, 95))

    # Crest: sun disc with rays.
    o = crest_ink()
    o.circle((CX, CY), 4.0)
    o.ring((CX, CY), 6.2, 1.2)
    for i in range(16):
        t = -math.pi / 2 + i * 2 * math.pi / 16
        r1 = 11.3 if i % 2 == 0 else 9.6
        u = np.array([math.cos(t), math.sin(t)])
        p = np.array([-u[1], u[0]])
        b = np.array([CX, CY]) + u * 7.6
        o.fill([b + p * 1.15, np.array([CX, CY]) + u * r1, b - p * 1.15])
    L = crest_ink()
    crest_hairline(L, CX - 15, y=CY, w=1.6)
    L.fill([(CX - 26, CY - 3.2), (CX - 29.5, CY), (CX - 26, CY + 3.2), (CX - 22.5, CY)])
    L.fill([(CX - 26, CY - 3.2), (CX - 29.5, CY), (CX - 26, CY + 3.2), (CX - 22.5, CY)])
    return a, rgb, a_c, finish_crest(o, L)


def age_iron(tint, s):
    # Texture: brushed iron with fine scratches and slight scorch.
    brush = pnoise(N, s, 8, 128, 0.4, kx=14)
    brush2 = pnoise(N, s + 1, 40, 128, 0.0, kx=30)
    scorch = smoothstep(0.4, 1.9, pnoise(N, s + 2, 2, 6, 1.5))
    sc = Ink(N, N, ss=2, wrap=True)
    rng = np.random.default_rng(s + 3)
    for _ in range(9):
        x, y = rng.uniform(0, N, 2)
        ang = rng.uniform(-0.2, 0.2)
        ln = rng.uniform(30, 90)
        sc.seg((x, y), (x + ln * math.cos(ang), y + ln * math.sin(ang)), 0.7, 0.4, v=rng.uniform(0.5, 1.0))
    a = 0.11 + 0.03 * brush + 0.018 * brush2 + 0.07 * sc.down_box()
    a = a * (1 - 0.22 * scorch)
    rgb = tex_rgb(tint, 0.45)[None, None, :] * (1 - 0.5 * scorch[..., None]) + hexrgb("#A497C4")[None, None, :] * 0.5 * scorch[..., None]

    # Corner: riveted angle bracket with a gusset.
    k = corner_ink()
    k.outline([(1.5, 1.5), (60, 1.5), (54, 13.5), (13.5, 13.5), (13.5, 54), (1.5, 60)], 1.8)
    k.seg((3.5, 3.6), (57, 3.6), 0.9)         # bevel highlight inside the top edge
    for x in (7.5, 25.5, 43.5):
        k.circle((x, 7.5), 2.5)
        k.circle((x - 0.7, 6.8), 0.7, op="erase")
    k.outline([(13.5, 13.5), (31, 13.5), (13.5, 31)], 1.5)
    k.circle((18.5, 18.5), 2.0)
    k.seg((60, 7.5), (95, 7.5), 1.4, 0.8)
    a_c = finish_corner(corner_fade(k, 55, 95))

    # Crest: strap plate with three rivets.
    o = crest_ink()
    o.outline([(CX - 27, 6.5), (CX - 23, 5.0), (CX + 23, 5.0), (CX + 27, 6.5),
               (CX + 27, 17.5), (CX + 23, 19.0), (CX - 23, 19.0), (CX - 27, 17.5)], 1.6)
    for x in (CX - 13, CX, CX + 13):
        o.circle((x, CY), 2.8)
        o.circle((x - 0.8, CY - 0.8), 0.8, op="erase")
    L = crest_ink()
    crest_hairline(L, CX - 27, y=CY - 1.0, w=1.6)
    L.circle((CX - 40, CY - 1.0), 1.8)
    return a, rgb, a_c, finish_crest(o, L)


def age_faith(tint, s):
    # Texture: vellum fibre with a very faint quatrefoil tracery lattice.
    fine = pnoise(N, s, 60, 128)
    mottle = pnoise(N, s + 1, 3, 12, 1.0)
    fb = Ink(N, N, ss=2, wrap=True)
    rng = np.random.default_rng(s + 2)
    for _ in range(420):
        x, y = rng.uniform(0, N, 2)
        ang = rng.uniform(0, math.pi)
        ln = rng.uniform(5, 16)
        c = rng.uniform(-0.25, 0.25) * ln
        u = np.array([math.cos(ang), math.sin(ang)])
        p = np.array([-u[1], u[0]])
        a0 = np.array([x, y])
        fb.bez(tuple(a0), tuple(a0 + u * ln / 3 + p * c), tuple(a0 + u * 2 * ln / 3 + p * c), tuple(a0 + u * ln),
               0.6, 0.4, n=8, v=rng.uniform(0.35, 0.8))
    tr = Ink(N, N, ss=2, wrap=True)
    for j in range(4):
        for i in range(4):
            c = (32 + 64 * i, 32 + 64 * j)
            r = 7.0
            cen = [(c[0] + r, c[1]), (c[0] - r, c[1]), (c[0], c[1] + r), (c[0], c[1] - r)]
            f = sd_circles_union(cen, r)
            tr.shape(lambda X, Y, f=f: np.abs(f(X, Y)) - 0.5, (c[0] - 16, c[1] - 16, c[0] + 16, c[1] + 16))
            tr.ring((64 * i, 64 * j), 5.0, 1.0)
            for dx, dy in ((1, 1), (1, -1)):
                tr.seg((c[0] + dx * 13, c[1] + dy * 13), (c[0] + dx * 27, c[1] + dy * 27), 1.0)
                tr.seg((c[0] - dx * 13, c[1] - dy * 13), (c[0] - dx * 27, c[1] - dy * 27), 1.0)
    a = 0.10 + 0.012 * fine + 0.005 * mottle + 0.10 * fb.down_box() + 0.045 * tr.down_box()
    rgb = tex_rgb(tint, 0.5)

    # Corner: gothic quatrefoil with a vine running out along the edges.
    k = corner_ink()
    c, r = (18.0, 18.0), 6.6
    cen = [(c[0] + r, c[1]), (c[0] - r, c[1]), (c[0], c[1] + r), (c[0], c[1] - r)]
    f = sd_circles_union(cen, r)
    k.shape(lambda X, Y: np.abs(f(X, Y)) - 0.95, (0, 0, 36, 36))
    k.circle(c, 2.2)
    for dx, dy in ((1, 1), (-1, -1), (1, -1), (-1, 1)):      # cusps between the lobes
        k.seg((c[0] + dx * 4.3, c[1] + dy * 4.3), (c[0] + dx * 6.8, c[1] + dy * 6.8), 1.2, 0.6)
    k.seg((25, 1.5), (95, 1.5), 1.1)
    pts = [(x, 7.0 + 2.6 * math.sin((x - 30) / 7.5)) for x in np.linspace(30, 95, 50)]
    k.bez((24.6, 11.4), (27, 7.0), (28.5, 5.0), (30, 7.0 + 2.6 * math.sin(0)), 1.5)
    k.poly(pts, 1.5, 0.8)
    for x, side in ((41.8, -1), (65.4, 1), (88.9, -1)):
        y = 7.0 + 2.6 * math.sin((x - 30) / 7.5)
        tip = (x + 5.0, y + side * 5.0)
        k.fill(leaf_pts((x, y), tip, 3.6, power=0.75))
    for x in (53.6, 77.2):
        y = 7.0 + 2.6 * math.sin((x - 30) / 7.5)
        k.circle((x + 1.5, y + (3.0 if y > 7 else -3.0)), 1.1)
    a_c = finish_corner(corner_fade(k, 55, 95))

    # Crest: quatrefoil flanked by lozenges.
    o = crest_ink()
    r = 3.7
    cen = [(CX + r, CY), (CX - r, CY), (CX, CY + r), (CX, CY - r)]
    f = sd_circles_union(cen, r)
    o.shape(lambda X, Y: np.abs(f(X, Y)) - 0.8, (CX - 10, 0, CX + 10, CREST_H))
    o.circle((CX, CY), 1.3)
    o.fill([(CX - 16, CY - 3.4), (CX - 18.6, CY), (CX - 16, CY + 3.4), (CX - 13.4, CY)])
    L = crest_ink()
    crest_hairline(L, CX - 19, y=CY - 1.0, w=1.4)
    L.circle((CX - 25, CY - 1.0), 1.2)
    L.circle((CX - 29, CY - 1.0), 1.2)
    return a, rgb, a_c, finish_crest(o, L)


def age_discovery(tint, s):
    # Texture: paper fibre, foxing spots and patches of fine engraved hatching.
    fine = pnoise(N, s, 60, 128)
    fib = pnoise(N, s + 1, 20, 128, 0.0, kx=3)
    X, Y = grid(N)
    q = (X + Y) / 8.0
    dist = np.abs((q % 1.0) - 0.5) * 8 / math.sqrt(2)
    hatch1 = np.clip(0.55 - np.abs(dist - 2.83) + 0.0, 0, 1)
    q2 = (X - Y) / 8.0
    dist2 = np.abs((q2 % 1.0) - 0.5) * 8 / math.sqrt(2)
    hatch2 = np.clip(0.55 - np.abs(dist2 - 2.83), 0, 1)
    m = pnoise(N, s + 2, 3, 9, 1.0)
    patch1 = smoothstep(0.3, 1.3, m)
    patch2 = smoothstep(0.3, 1.3, -m) * smoothstep(0.0, 1.0, pnoise(N, s + 3, 3, 9, 1.0))
    fox = Ink(N, N, ss=2, wrap=True)
    rng = np.random.default_rng(s + 4)
    for _ in range(26):
        x, y = rng.uniform(0, N, 2)
        rr = rng.uniform(1.0, 3.2)
        fox.circle((x, y), rr, v=rng.uniform(0.4, 1.0))
    foxing = pblur(fox.down_box(), 1.2)
    a = 0.10 + 0.012 * fine + 0.012 * fib + 0.10 * hatch1 * patch1 + 0.10 * hatch2 * patch2 + 0.05 * foxing
    rgb = tex_rgb(tint, 0.5)[None, None, :] * (1 - np.clip(foxing, 0, 1)[..., None] * 0.7) \
        + hexrgb("#B8875A")[None, None, :] * np.clip(foxing, 0, 1)[..., None] * 0.7

    # Corner: compass-rose quadrant on a graduated arc, a twisted cord along the edges.
    k = corner_ink()
    C = np.array([5.0, 5.0])
    k.arc(tuple(C), 23.0, 0.0, math.pi / 4, 1.2)
    for i in range(1, 9):
        t = i * math.pi / 2 / 16
        r1 = 26.5 if i % 2 == 0 else 25.0
        k.seg(tuple(C + 23.0 * np.array([math.cos(t), math.sin(t)])), tuple(C + r1 * np.array([math.cos(t), math.sin(t)])), 0.9)
    rope(k, 36.0, 96.0, 5.0, amp=1.8, period=10.0)
    k.sym_diag()
    # rose points: cardinal points run along the edges, the diagonal point is the long one;
    # one half of each point filled, one outlined (not mirrored, so the handedness is kept)
    for t, ln, wd in ((0.0, 32.0, 2.6), (math.pi / 2, 32.0, 2.6), (math.pi / 4, 38.0, 3.4),
                      (math.pi / 8, 17.0, 1.7), (3 * math.pi / 8, 17.0, 1.7)):
        u = np.array([math.cos(t), math.sin(t)])
        p = np.array([-u[1], u[0]])
        side1 = C + u * ln * 0.25 + p * wd
        side2 = C + u * ln * 0.25 - p * wd
        tip = C + u * ln
        k.outline([C, side1, tip, side2], 1.0)
        k.fill([C, side2, tip])
    k.ring(tuple(C), 3.6, 1.1)
    a_c = finish_corner(corner_fade(k, 55, 95), diag=False)

    # Crest: small compass rose between twisted cords.
    o = crest_ink()
    Cc = np.array([CX, CY])
    for u, ln, wd in (((0, -1), 11.3, 2.8), ((0, 1), 11.3, 2.8), ((-1, 0), 15.0, 2.8)):
        u = np.array(u, float)
        p = np.array([-u[1], u[0]])
        tip = Cc + u * ln
        s1, s2 = Cc + u * 3 + p * wd, Cc + u * 3 - p * wd
        o.outline([Cc, s1, tip, s2], 1.0)
        if u[1] <= 0:
            o.fill([Cc, s1, tip] if u[1] < 0 else [Cc, s2, tip])
    for u in ((-1, -1), (-1, 1)):
        u = np.array(u, float) / math.sqrt(2)
        p = np.array([-u[1], u[0]])
        o.fill([Cc + p * 1.4, Cc + u * 7.5, Cc - p * 1.4])
    o.circle((CX, CY), 1.6)
    L = crest_ink()
    rope(L, CX - 64, CX - 19, CY, amp=1.8, period=9.0)
    crest_hairline(L, CX - 64, y=CY, w=1.3)
    return a, rgb, a_c, finish_crest(o, L)


def age_industry(tint, s):
    # Texture: fine cast grain, pits, soot and a few rivets.
    grain = pnoise(N, s, 50, 128)
    mid = emboss(pnoise(N, s + 1, 24, 70, 0.5))
    pits = smoothstep(2.0, 2.8, pnoise(N, s + 2, 50, 110))
    soot = smoothstep(0.3, 1.8, pnoise(N, s + 3, 2, 6, 1.5))
    rv = Ink(N, N, ss=4, wrap=True)
    for (x, y) in ((40.5, 52.5), (168.5, 84.5), (104.5, 200.5), (232.5, 232.5)):
        rv.ring((x, y), 3.6, 0.9, v=0.55)
        rv.circle((x - 1.0, y - 1.0), 1.8, v=1.0)
    a = 0.10 + 0.02 * grain + 0.014 * mid
    a = a * (1 - 0.6 * pits) * (1 - 0.15 * soot) + 0.16 * rv.down_box()
    rgb = tex_rgb(tint, 0.5)

    # Corner: pipe elbow round the corner with flanges, gear nested inside the bend.
    k = corner_ink()
    O = (30.0, 30.0)
    k.seg((30, 3.5), (95, 3.5), 1.6)
    k.seg((30, 10.5), (95, 10.5), 1.6)
    k.arc(O, 26.5, -math.pi / 2, -3 * math.pi / 4, 1.6)
    k.arc(O, 19.5, -math.pi / 2, -3 * math.pi / 4, 1.6)
    for x in (32.0, 35.5):
        k.seg((x, 0.8), (x, 13.2), 2.0)
    k.circle((60, 7.0), 1.2)
    k.seg((66, 0.8), (66, 13.2), 2.0)
    k.circle((84, 7.0), 1.2)
    k.shape(sd_gear(O, 12, 9.6, 12.4, math.pi / 4), (14, 14, 46, 46))
    k.circle(O, 6.6, op="erase")
    k.ring(O, 3.3, 1.5)
    for t in (math.pi / 4, 3 * math.pi / 4, 5 * math.pi / 4, 7 * math.pi / 4):
        k.seg((O[0] + 3.3 * math.cos(t), O[1] + 3.3 * math.sin(t)), (O[0] + 7.0 * math.cos(t), O[1] + 7.0 * math.sin(t)), 1.4)
    a_c = finish_corner(corner_fade(k, 55, 95))

    # Crest: cog between flanged pipe lines.
    o = crest_ink()
    o.shape(sd_gear((CX, CY), 10, 8.0, 10.8, -math.pi / 2), (CX - 13, 0, CX + 13, CREST_H))
    o.circle((CX, CY), 4.6, op="erase")
    o.circle((CX, CY), 2.0)
    for x in (CX - 17, CX - 20.5):
        o.seg((x, CY - 4.5), (x, CY + 4.5), 1.8)
    L = crest_ink()
    crest_hairline(L, CX - 17, y=CY - 2.0, w=1.3)
    crest_hairline(L, CX - 17, y=CY + 2.0, w=1.3)
    L.seg((CX - 44, CY - 4.0), (CX - 44, CY + 4.0), 1.5)
    return a, rgb, a_c, finish_crest(o, L)


def age_modern(tint, s):
    # Texture: fine concrete grain with aggregate speckle and a few air holes.
    g1 = pnoise(N, s, 70, 128)
    g2 = pnoise(N, s + 1, 20, 70, 0.5)
    sp = Ink(N, N, ss=2, wrap=True)
    rng = np.random.default_rng(s + 2)
    for _ in range(900):
        x, y = rng.uniform(0, N, 2)
        sp.circle((x, y), rng.uniform(0.35, 0.9), v=rng.uniform(0.3, 1.0))
    holes = Ink(N, N, ss=2, wrap=True)
    for _ in range(16):
        x, y = rng.uniform(0, N, 2)
        holes.circle((x, y), rng.uniform(0.8, 1.6))
    a = 0.10 + 0.016 * g1 + 0.012 * g2 + 0.06 * sp.down_box()
    a = a * (1 - 0.85 * holes.down_box())
    rgb = tex_rgb(tint, 0.5)

    # Corner: crisp L ticks with ruler marks.
    k = corner_ink()
    k.seg((3.0, 3.0), (95, 3.0), 1.6, 1.0)
    k.fill([(1.0, 1.0), (19.0, 1.0), (19.0, 5.0), (1.0, 5.0)])
    k.seg((9.0, 9.0), (25.0, 9.0), 1.4)
    k.circle((9.0, 9.0), 1.2)
    for i, x in enumerate(range(33, 95, 8)):
        k.seg((x, 3.0), (x, 9.0 if i % 2 == 0 else 6.5), 1.2)
    a_c = finish_corner(corner_fade(k, 50, 95))

    # Crest: short bar between two dots.
    o = crest_ink()
    o.fill([(CX - 14, CY - 1.6), (CX + 14, CY - 1.6), (CX + 14, CY + 1.6), (CX - 14, CY + 1.6)])
    o.circle((CX - 21, CY), 2.3)
    L = crest_ink()
    crest_hairline(L, CX - 27, y=CY, w=1.1)
    return a, rgb, a_c, finish_crest(o, L)


def age_information(tint, s):
    # Texture: dot grid with faint circuit traces and pads.
    dots = Ink(N, N, ss=2, wrap=True)
    for j in range(16):
        for i in range(16):
            dots.circle((i * 16 + 8, j * 16 + 8), 1.0)
    tr = Ink(N, N, ss=2, wrap=True)
    rng = np.random.default_rng(s)
    dirs = [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)]
    for _ in range(11):
        x, y = rng.integers(0, 32, 2) * 8 + 4.0
        d = int(rng.integers(0, 8)) & ~1        # start orthogonal
        pts = [(x, y)]
        for _seg in range(int(rng.integers(2, 5))):
            ln = int(rng.integers(2, 6)) * 8
            dx, dy = dirs[d]
            x, y = x + dx * ln, y + dy * ln
            pts.append((x, y))
            d = (d + int(rng.choice([-1, 1]))) % 8
        tr.poly(pts, 1.0)
        tr.ring(pts[0], 2.0, 1.0)
        tr.circle(pts[-1], 1.8)
    glass = pnoise(N, s + 1, 40, 128)
    a = 0.08 + 0.008 * glass + 0.20 * dots.down_box() + 0.09 * tr.down_box()
    rgb = tex_rgb(tint, 0.4)

    # Corner: parallel traces turning the corner on a chamfer; one steps in to a pad.
    k = corner_ink()
    k.poly([(95, 3.0), (17, 3.0), (10, 10)], 1.5)
    k.poly([(80, 8.0), (23, 8.0), (15.5, 15.5)], 1.5)
    k.poly([(64, 13.0), (42, 13.0), (30.5, 24.5)], 2.0)
    k.ring((27.0, 27.0), 4.2, 1.6)
    k.circle((27.0, 27.0), 1.6)
    k.ring((83.0, 8.0), 2.0, 1.2)
    k.ring((67.0, 13.0), 2.0, 1.2)
    a_c = finish_corner(corner_fade(k, 55, 95))

    # Crest: node with two stepped traces.
    o = crest_ink()
    o.ring((CX, CY), 5.0, 1.6)
    o.circle((CX, CY), 2.0)
    o.poly([(CX - 6.0, CY), (CX - 13, CY), (CX - 18, CY - 5.0), (CX - 34, CY - 5.0)], 1.6)
    o.ring((CX - 36.5, CY - 5.0), 2.0, 1.2)
    L = crest_ink()
    crest_hairline(L, CX - 40, y=CY + 1.0, w=1.2)
    L.circle((CX - 44, CY + 1.0), 1.1)
    return a, rgb, a_c, finish_crest(o, L)


AGES = [
    ("dawn", "Dawn", "#C8A06A", age_dawn),
    ("hunters", "Hunters", "#B7855A", age_hunters),
    ("roots", "Roots", "#8FAE6A", age_roots),
    ("bronze", "Bronze", "#CD8F46", age_bronze),
    ("iron", "Iron", "#8FA1B3", age_iron),
    ("faith", "Faith & Fief", "#D1A94E", age_faith),
    ("discovery", "Discovery", "#D8B972", age_discovery),
    ("industry", "Industry", "#C57A3E", age_industry),
    ("modern", "Modern", "#8DB1CF", age_modern),
    ("information", "Information", "#4FD6C2", age_information),
]


# ----------------------------------------------------------------------------------------------
# Checks and contact sheet
# ----------------------------------------------------------------------------------------------

def seam_ratio(alpha):
    """Seam step vs the step at the same phase mid-tile (columns 127|128), and vs the mean step.
    Returns the larger of the two ratios per axis; ~1 means no seam."""
    a = alpha.astype(float)
    sx = np.abs(a[:, 0] - a[:, -1]).mean()
    sy = np.abs(a[0, :] - a[-1, :]).mean()
    px = np.abs(a[:, 128] - a[:, 127]).mean() + 1e-9
    py = np.abs(a[128, :] - a[127, :]).mean() + 1e-9
    mx = np.abs(np.diff(a, axis=1)).mean()
    my = np.abs(np.diff(a, axis=0)).mean()
    return min(sx / px, sx / mx), min(sy / py, sy / my)


def over(dst, src_rgba, x, y, opacity=1.0):
    """Alpha-composite uint8 RGBA `src_rgba` onto float RGB `dst` at (x, y)."""
    h, w = src_rgba.shape[:2]
    H, W = dst.shape[:2]
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(W, x + w), min(H, y + h)
    if x1 <= x0 or y1 <= y0:
        return
    s = src_rgba[y0 - y:y1 - y, x0 - x:x1 - x].astype(float) / 255
    al = s[..., 3:4] * opacity
    dst[y0:y1, x0:x1] = dst[y0:y1, x0:x1] * (1 - al) + s[..., :3] * al


def resized(rgba_u8, w, h):
    return np.asarray(Image.fromarray(rgba_u8, "RGBA").resize((w, h), Image.LANCZOS))


def font(size, bold=False):
    for p in ("/System/Library/Fonts/Supplemental/Georgia Bold.ttf" if bold else "/System/Library/Fonts/Supplemental/Georgia.ttf",
              "/System/Library/Fonts/Supplemental/Times New Roman.ttf"):
        try:
            return ImageFont.truetype(p, size)
        except OSError:
            pass
    return ImageFont.load_default()


BG = hexrgb("#2D1B13")
PARCH = (0xF5, 0xEA, 0xD7)


def mock_panel(tex, corner, crest, tint, name, w=300, h=180):
    """Panel as the game will draw it: surface, texture, hairline border, four 48 px corners,
    crest at 192x12 on the top edge, parchment text."""
    pad = 8
    img = np.zeros((h + 2 * pad, w + 2 * pad, 3)) + BG
    top, bot = hexrgb("#37231A"), hexrgb("#21130E")
    grad = top[None, :] * (1 - np.linspace(0, 1, h)[:, None]) + bot[None, :] * np.linspace(0, 1, h)[:, None]
    img[pad:pad + h, pad:pad + w] = grad[:, None, :]
    tiled = np.tile(tex, (math.ceil(h / N), math.ceil(w / N), 1))[:h, :w]
    over(img, tiled, pad, pad, 0.9)
    # hairline border in the tint
    for (x0, y0, x1, y1) in ((pad, pad, pad + w, pad + 1), (pad, pad + h - 1, pad + w, pad + h),
                             (pad, pad, pad + 1, pad + h), (pad + w - 1, pad, pad + w, pad + h)):
        img[y0:y1, x0:x1] = img[y0:y1, x0:x1] * 0.5 + tint * 0.5
    c48 = resized(corner, 48, 48)
    over(img, c48, pad, pad, 0.85)
    over(img, c48[:, ::-1], pad + w - 48, pad, 0.85)
    over(img, c48[::-1, :], pad, pad + h - 48, 0.85)
    over(img, c48[::-1, ::-1], pad + w - 48, pad + h - 48, 0.85)
    cr = resized(crest, 192, 12)
    over(img, cr, pad + (w - 192) // 2, pad - 6, 1.0)
    pil = Image.fromarray(np.round(np.clip(img, 0, 1) * 255).astype(np.uint8))
    d = ImageDraw.Draw(pil)
    d.text((pad + 26, pad + 24), f"Age of {name}" if name not in ("Dawn",) else "The Dawn", fill=PARCH, font=font(18, True))
    body = font(13)
    for i, line in enumerate(("Food 214  ·  Wood 87  ·  Stone 40",
                              "Your people grow restless; the",
                              "harvest is in and the granary full.",
                              "Next: 3 discoveries to the new age.")):
        d.text((pad + 26, pad + 58 + i * 20), line, fill=PARCH, font=body)
    return np.asarray(pil).astype(float) / 255


def contact_sheet(results):
    bw, bh = 1150, 300
    cols = 2
    rows = math.ceil(len(results) / cols)
    sheet = np.zeros((rows * bh + 20, cols * bw + 20, 3)) + BG
    labels = []
    for idx, (slug, name, tint, tex, corner, crest) in enumerate(results):
        ox = 10 + (idx % cols) * bw
        oy = 10 + (idx // cols) * bh
        t = np.tile(tex, (2, 3, 1))
        half = np.asarray(Image.fromarray(t, "RGBA").resize((384, 256), Image.BOX))
        over(sheet, half, ox + 10, oy + 30, 0.9)
        p = mock_panel(tex, corner, crest, tint, name)
        sheet[oy + 30:oy + 30 + p.shape[0], ox + 410:ox + 410 + p.shape[1]] = p
        rx = ox + 740
        over(sheet, corner, rx, oy + 30)
        over(sheet, resized(corner, 48, 48), rx + 110, oy + 30)
        over(sheet, resized(corner, 48, 48)[:, ::-1], rx + 170, oy + 30)
        over(sheet, crest, rx, oy + 150)
        over(sheet, resized(crest, 192, 12), rx, oy + 195)
        labels.append((ox + 10, oy + 6, f"{name}  ({slug})  {tint_hex(tint)}"))
    pil = Image.fromarray(np.round(np.clip(sheet, 0, 1) * 255).astype(np.uint8))
    d = ImageDraw.Draw(pil)
    for x, y, txt in labels:
        d.text((x, y), txt, fill=PARCH, font=font(16, True))
    return pil


def tint_hex(t):
    return "#" + "".join(f"{int(round(v * 255)):02X}" for v in t)


def main():
    results = []
    ok = True
    for i, (slug, name, hexc, fn) in enumerate(AGES):
        tint = hexrgb(hexc)
        a_tex, rgb, a_corner, a_crest = fn(tint, 1000 + 97 * i)
        a_tex = fit_alpha(a_tex)
        tex = rgba(a_tex, rgb)
        corner = rgba(a_corner, tint)
        crest = rgba(a_crest, tint)
        d = OUT / slug
        d.mkdir(parents=True, exist_ok=True)
        Image.fromarray(tex, "RGBA").save(d / "panel_tex.png", optimize=True)
        Image.fromarray(corner, "RGBA").save(d / "corner.png", optimize=True)
        Image.fromarray(crest, "RGBA").save(d / "crest.png", optimize=True)
        al = tex[..., 3] / 255
        sx, sy = seam_ratio(al)
        sym = int(np.abs(crest[..., 3].astype(int) - crest[:, ::-1, 3].astype(int)).max())
        far = corner[..., 3].astype(float) / 255
        # ink outside the edge bands and the 44 px corner focus
        Y, X = np.mgrid[0:CORNER, 0:CORNER]
        stray = far[((X > 44) | (Y > 44)) & (X > 16) & (Y > 16)].max()
        ends = crest[:, :6, 3].max()
        print(f"{slug:12s} tex mean {al.mean()*100:5.2f}% peak {al.max()*100:5.2f}% seam {sx:4.2f}/{sy:4.2f}  "
              f"corner ink {far.mean()*100:4.1f}% stray {stray:.2f}  crest sym diff {sym} end alpha {ends}")
        if not (0.08 <= al.mean() <= 0.14 and al.max() <= 0.35 and sx < 1.3 and sy < 1.3 and sym == 0):
            ok = False
        results.append((slug, name, tint, tex, corner, crest))
    SHEET.parent.mkdir(parents=True, exist_ok=True)
    contact_sheet(results).save(SHEET, optimize=True)
    print("contact sheet:", SHEET)
    if not ok:
        print("CHECK FAILED", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
