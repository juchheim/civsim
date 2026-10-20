"""Frame loading, hex geometry and deterministic noise shared by the pixel renderer."""
import base64
import json
import math
import os
import numpy as np

SQ3_2 = math.sqrt(3) / 2
UNIT = 1024

BIOMES = ["Ocean", "Coast", "Ice", "Tundra", "Taiga", "TemperateForest", "OpenWoodland", "GrasslandSteppe", "Savanna",
          "TropicalForest", "Wetland", "Desert", "Mountain", "Peak"]
OCEAN, COAST, ICE, TUNDRA, TAIGA, TFOREST, WOODLAND, STEPPE, SAVANNA, TROPIC, WETLAND, DESERT, MOUNTAIN, PEAK = range(14)
STRUCTS = ["Hearth", "LeanTo", "HideTent", "PitHouse", "WattleHouse", "MudbrickHouse", "Longhouse", "StoragePit",
           "DryingRack", "Granary", "Kiln", "Quern", "ThornBarrier", "Lookout", "Weir", "Field", "Pasture", "Orchard"]
F_RIVER, F_LAKE, F_SPRING, F_SALMON, F_GRAIN, F_COASTADJ = 1, 2, 4, 8, 16, 32

DEFAULT_DATA = os.environ.get("CIVSIM_FRAMES") or os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")


class Frame:
    def __init__(self, path):
        d = json.load(open(path))
        self.d = d
        self.W, self.H = d["width"], d["height"]
        self.biome = np.frombuffer(base64.b64decode(d["biome"]), np.uint8).reshape(self.H, self.W).copy()
        self.elev = np.frombuffer(base64.b64decode(d["elevation"]), np.uint8).reshape(self.H, self.W).astype(np.float32)
        self.flags = np.array(d["flags"], np.int32).reshape(self.H, self.W)
        self.owner = np.array(d["owner"], np.int32).reshape(self.H, self.W)
        self.claim = np.frombuffer(base64.b64decode(d["claim"]), np.uint8).reshape(self.H, self.W) \
            if isinstance(d["claim"], str) else np.array(d["claim"]).reshape(self.H, self.W)
        self.season = d.get("seasonOfYear", 0)
        self.year = d.get("year", 0)
        self.figures = d["figures"]
        self.herds = d["herds"]
        self.settlements = d["settlements"]
        self.plots = {p[0]: p[1] for p in d["plots"]}
        self.camps = list(zip(d.get("camps", []), d.get("campCiv", [])))
        self.water = (self.biome == OCEAN) | (self.biome == COAST) | ((self.flags & F_LAKE) != 0)

    # tile centre in "column units" (x) and "row units" (y)
    def centre(self, t):
        x, y = t % self.W, t // self.W
        return x + 0.5 + (0.5 if y & 1 else 0.0), y + 0.5

    def neighbours(self, t):
        x, y = t % self.W, t // self.W
        if y & 1 == 0:
            d = [(-1, 0), (1, 0), (-1, -1), (0, -1), (-1, 1), (0, 1)]
        else:
            d = [(-1, 0), (1, 0), (0, -1), (1, -1), (0, 1), (1, 1)]
        out = []
        for dx, dy in d:
            nx, ny = x + dx, y + dy
            if 0 <= nx < self.W and 0 <= ny < self.H:
                out.append(ny * self.W + nx)
        return out

    def hexdist(self, a, b):
        ax, ay = a % self.W, a // self.W
        bx, by = b % self.W, b // self.W
        aq, bq = ax - (ay - (ay & 1)) // 2, bx - (by - (by & 1)) // 2
        dq, dr = aq - bq, ay - by
        return (abs(dq) + abs(dr) + abs(dq + dr)) // 2


def nearest_tile(fx, fr, W, H):
    """Vectorised nearest hex (pointy-top, odd rows shifted east). fx in columns, fr in rows. Returns (tx, ty)."""
    r0 = np.floor(fr - 0.5).astype(np.int32)
    best_d = np.full(fx.shape, 1e9, np.float32)
    bx = np.zeros(fx.shape, np.int32)
    by = np.zeros(fx.shape, np.int32)
    for dr in (0, 1):
        r = np.clip(r0 + dr, 0, H - 1)
        shift = (r & 1) * 0.5
        c0 = np.floor(fx - 0.5 - shift).astype(np.int32)
        for dc in (0, 1):
            c = np.clip(c0 + dc, 0, W - 1)
            dx = fx - (c + 0.5 + shift)
            dy = (fr - (r + 0.5)) * SQ3_2
            d = dx * dx + dy * dy
            m = d < best_d
            best_d = np.where(m, d, best_d)
            bx = np.where(m, c, bx)
            by = np.where(m, r, by)
    return bx, by


def smooth_sample(grid, fx, fr, ease=True):
    """Bilinear sample of a per-tile scalar at continuous (column, row) positions (smoothstep-eased for masks;
    linear for heights, where easing would bunch the slope into stripes at every row)."""
    H, W = grid.shape
    rr = fr - 0.5
    r0 = np.floor(rr).astype(np.int32)
    t = rr - r0
    if ease:
        t = t * t * (3 - 2 * t)
    vals = []
    for r in (r0, r0 + 1):
        rc = np.clip(r, 0, H - 1)
        c = fx - 0.5 - (rc & 1) * 0.5
        c0 = np.floor(c).astype(np.int32)
        u = c - c0
        if ease:
            u = u * u * (3 - 2 * u)
        a = grid[rc, np.clip(c0, 0, W - 1)]
        b = grid[rc, np.clip(c0 + 1, 0, W - 1)]
        vals.append(a * (1 - u) + b * u)
    return vals[0] * (1 - t) + vals[1] * t


# ---------------------------------------------------------------------------------------------------------- noise
def hash2(ix, iy, seed=0):
    """Integer hash -> float in [0,1). Works on numpy int arrays or ints."""
    ix = np.asarray(ix, dtype=np.int64)
    iy = np.asarray(iy, dtype=np.int64)
    h = (ix * 374761393 + iy * 668265263 + seed * 2246822519) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    h = h ^ (h >> 16)
    return (h & 0xFFFFFF) / float(0x1000000)


def value_noise(x, y, seed=0):
    xi = np.floor(x).astype(np.int64)
    yi = np.floor(y).astype(np.int64)
    xf = x - xi
    yf = y - yi
    u = xf * xf * (3 - 2 * xf)
    v = yf * yf * (3 - 2 * yf)
    a = hash2(xi, yi, seed)
    b = hash2(xi + 1, yi, seed)
    c = hash2(xi, yi + 1, seed)
    d = hash2(xi + 1, yi + 1, seed)
    return (a * (1 - u) + b * u) * (1 - v) + (c * (1 - u) + d * u) * v


def fbm(x, y, seed=0, octaves=3):
    s, amp, tot = 0.0, 1.0, 0.0
    for o in range(octaves):
        s = s + value_noise(x * (2 ** o), y * (2 ** o), seed + o * 17) * amp
        tot += amp
        amp *= 0.5
    return s / tot


BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], np.float32) / 16.0 + 1 / 32.0


def bayer(px, py):
    return BAYER4[py % 4, px % 4]


def box_blur(a, r):
    """Separable box blur with edge clamping (r in px)."""
    if r < 1:
        return a
    out = a
    for axis in (0, 1):
        pad = [(0, 0), (0, 0)]
        pad[axis] = (r + 1, r)
        p = np.pad(out, pad, mode="edge")
        c = np.cumsum(p, axis=axis)
        if axis == 0:
            out = (c[2 * r + 1:, :] - c[:-2 * r - 1, :]) / (2 * r + 1)
        else:
            out = (c[:, 2 * r + 1:] - c[:, :-2 * r - 1]) / (2 * r + 1)
    return out
