"""
Terrain for the detail views (Z0-Z1, ~24 px per hex column).

The hex grid is hidden: every native pixel looks up the hex under a domain-warped position, so biome edges become
organic curves with a 1-2 px dithered fringe, while elevation is sampled smoothly and lit from the top-left. The
result is an index buffer (palette indices) plus a list of y-sorted objects (trees, rocks, mountains, reeds) that the
renderer merges with structures, herds and people.
"""
import math
import numpy as np
import sprites as S
from palette import IDX, DARKER, LIGHTER, SEASON_ROLES, season_name
from world import (box_blur, SQ3_2, nearest_tile, smooth_sample, value_noise, fbm, hash2, BAYER4, OCEAN, COAST, ICE, TUNDRA,
                   TAIGA, TFOREST, WOODLAND, STEPPE, SAVANNA, TROPIC, WETLAND, DESERT, MOUNTAIN, PEAK, F_RIVER, F_LAKE,
                   F_SPRING)

LAND_SEA_LEVEL = 84.0


class View:
    """A window onto the map: (x0, y0) top-left in column/row units, size in tiles, TW px per column."""

    def __init__(self, frame, x0, y0, cols, rows, tw=24, season=None, frame_no=0, n_frames=16):
        self.f = frame
        self.tw = tw
        self.rh = tw * SQ3_2
        self.ox = int(round(x0 * tw))
        self.oy = int(round(y0 * self.rh))
        self.w = int(round(cols * tw))
        self.h = int(round(rows * self.rh))
        self.season = season_name(frame.season if season is None else season)
        self.fno = frame_no
        self.nf = n_frames
        py, px = np.mgrid[0:self.h, 0:self.w]
        self.wx = px + self.ox          # absolute world pixel coords (stable hashing / dithering across windows)
        self.wy = py + self.oy
        self.fx = (self.wx + 0.5) / tw
        self.fr = (self.wy + 0.5) / self.rh
        self.bay = BAYER4[self.wy % 4, self.wx % 4]

    def unit_to_px(self, X, Y):
        return X / 1024.0 * self.tw - self.ox, Y / 1024.0 * self.rh - self.oy

    def tile_px(self, t):
        cx, cy = self.f.centre(t)
        return cx * self.tw - self.ox, cy * self.rh - self.oy

    def roles(self, name):
        return SEASON_ROLES[self.season][name]


def dilate(mask, n=1):
    m = mask.copy()
    for _ in range(n):
        m2 = m.copy()
        m2[1:, :] |= m[:-1, :]
        m2[:-1, :] |= m[1:, :]
        m2[:, 1:] |= m[:, :-1]
        m2[:, :-1] |= m[:, 1:]
        m = m2
    return m


def distance_from(mask, cap=24):
    """4-neighbour distance (px) to the nearest True pixel of mask, capped."""
    d = np.where(mask, 0, cap).astype(np.int16)
    m = mask.copy()
    for k in range(1, cap):
        m2 = dilate(m, 1)
        new = m2 & ~m
        d[new] = k
        m = m2
        if m.all():
            break
    return d


def sharpen_quant(level, bay, k=2.6):
    """Quantise a continuous ramp level with an ordered dither confined to a narrow band at each step."""
    fl = np.floor(level)
    f = level - fl
    f2 = np.clip((f - 0.5) * k + 0.5, 0, 1)
    return (fl + (bay < f2)).astype(np.int32)


# ground ramp per biome (role name or explicit list) and base level
def ground_ramps(season):
    R = SEASON_ROLES[season]
    winter = season == "winter"
    snow = ["n0", "n1", "n2", "n3"]
    sand = ["e1", "e2", "e3", "e4", "s4"]
    rock = ["r0", "r1", "r2", "r3", "r4"]
    table = {
        ICE: (["n0", "n1", "n2", "n3"], 2.4),
        TUNDRA: (R["moss"], 1.9),
        TAIGA: (snow if winter else ["g0", "g1", "g2", "t1"], 2.2 if winter else 1.2),
        TFOREST: (snow if winter else ["f0", "f1", "g1", "g2", "g3"], 2.2 if winter else 1.6),
        WOODLAND: (R["grass"], 2.3),
        STEPPE: (R["steppe"], 2.4),
        SAVANNA: (R["dry"], 2.0),          # the tropics get a dry season, not snow
        TROPIC: (["f0", "f1", "g1", "g2", "g3"], 1.5),
        WETLAND: (["n0", "n1", "n2"] if winter else ["g0", "g1", "g2", "g3"], 1.7),
        DESERT: (sand, 2.6),
        MOUNTAIN: (rock, 2.0),
        PEAK: (["r1", "r2", "n1", "n2", "n3"], 2.5),
        OCEAN: (["w1", "w2"], 1.0),
        COAST: (["w2", "w3"], 1.0),
    }
    lut = np.zeros((14, 6), np.uint8)
    base = np.zeros(14, np.float32)
    lens = np.zeros(14, np.int32)
    for b, (ramp, bl) in table.items():
        ids = [IDX[c] for c in ramp]
        lens[b] = len(ids)
        for i in range(6):
            lut[b, i] = ids[min(i, len(ids) - 1)]
        base[b] = bl
    return lut, base, lens


DETAIL_AMP = np.array([0, 0, 3, 5, 6, 5, 6, 4, 4, 5, 2, 8, 40, 55], np.float32)
PATCH_AMP = np.array([0, 0, 0.6, 0.8, 0.7, 0.7, 0.85, 0.85, 0.75, 0.7, 0.8, 0.8, 0.5, 0.4], np.float32)


def render_ground(v):
    f = v.f
    tw = v.tw
    # ---- domain-warped biome lookup (hides the hexes) ----
    wxn = fbm(v.fx * 0.9, v.fr * 0.9, 11) - 0.5
    wyn = fbm(v.fx * 0.9, v.fr * 0.9, 23) - 0.5
    wxn += (value_noise(v.fx * 3.1, v.fr * 3.1, 5) - 0.5) * 0.35
    wyn += (value_noise(v.fx * 3.1, v.fr * 3.1, 7) - 0.5) * 0.35
    amp = 0.62
    tx, ty = nearest_tile(v.fx + wxn * amp, v.fr + wyn * amp, f.W, f.H)
    tx2, ty2 = nearest_tile(v.fx + wxn * amp + 1.6 / tw, v.fr + wyn * amp + 1.4 / tw, f.W, f.H)
    use2 = v.bay < 0.5
    TX = np.where(use2, tx2, tx)
    TY = np.where(use2, ty2, ty)
    biome = f.biome[TY, TX]
    tile_water = f.water[TY, TX]
    water = tile_water.copy()
    v.tileX, v.tileY = tx, ty            # warped tile under each pixel (for territory etc.)
    v.biome = biome

    # ---- elevation + light ----
    eg = f.elev.copy()
    eg[f.water] = LAND_SEA_LEVEL
    e = box_blur(smooth_sample(eg, v.fx, v.fr, ease=False), max(1, tw // 6))
    detail = (fbm(v.fx * 2.2, v.fr * 2.2, 31, 3) - 0.5)
    e = e + detail * DETAIL_AMP[biome]
    gy, gx = np.gradient(e)
    shade = np.clip(-(gx * 1.0 + gy * 1.25) * (24.0 / tw) ** 0.5 * 0.9, -1.6, 1.6)
    v.elev_px = e

    lut, base, lens = ground_ramps(v.season)
    patch = (fbm(v.fx * 1.7, v.fr * 1.7, 41, 2) - 0.5) * 2.0 * PATCH_AMP[biome]
    level = base[biome] + shade + patch * 0.8
    q = sharpen_quant(level, v.bay)
    q = np.clip(q, 0, lens[biome] - 1)
    G = lut[biome, q]
    if v.season == "winter":   # hoar frost on the dry-season savanna so the climate edge is not a ruler line
        frost = (biome == SAVANNA) & (hash2(v.wx, v.wy, 515) < 0.10 + 0.25 * (fbm(v.fx * 0.7, v.fr * 0.7, 516) > 0.6))
        G = np.where(frost, IDX["n2"], G)

    # ---- water: depth from the shore, coast shallows, foam, sparkles ----
    land = ~water
    dland = distance_from(land, 26)      # px from land, for water pixels
    dwater = distance_from(water, 4)     # px from water, for land pixels
    lod = tw / 24.0
    depth = dland.astype(np.float32) / lod * (0.5 if tw < 12 else 1.0) + np.where(biome == OCEAN, 5.0, 0.0)
    wl = 4.4 - depth * 0.22 + (fbm(v.fx * 1.2, v.fr * 1.2, 51, 2) - 0.5) * 1.1
    wl = np.clip(wl, 1.0, 4.3)
    wbay = BAYER4[v.wy % 4, (v.wx // 3) % 4]     # stretched matrix: dither reads as short horizontal wave strokes
    wq = sharpen_quant(wl, wbay, 3.0)
    wramp = np.array([IDX[c] for c in v.roles("water")], np.uint8)
    W_ = wramp[np.clip(wq, 0, len(wramp) - 2)]
    # foam that laps: the band breathes with the animation phase
    ph = 2 * math.pi * v.fno / v.nf
    lap = np.sin(v.wx / 5.0 + v.wy / 9.0 + ph) > 0.25
    foam = (dland == 1) | ((dland == 2) & lap & (tw >= 12))
    W_ = np.where(foam, IDX["w5"] if v.season != "winter" else IDX["n2"], W_)
    W_ = np.where((dland == 3) & lap & (v.bay < 0.3), IDX["w4"], W_)
    # sparkles: short highlight dashes that drift and twinkle on deeper water
    cellx, celly = 9, 6
    cx, cy = v.wx // cellx, v.wy // celly
    hsh = hash2(cx, cy, 77)
    life = ((hsh * v.nf + v.fno) % v.nf) / v.nf
    on = (life < 0.45) & (hsh > 0.35)
    sx = (v.wx % cellx) - (2 + (hsh * 97 % 1) * 3 + life * 3)
    sy = (v.wy % celly) - 2
    spark = on & (sy == 0) & (sx >= 0) & (sx < 2 + (hsh > 0.7)) & (dland > 3) & (tw >= 12)
    W_ = np.where(spark, np.where(depth > 10, IDX["w3"], IDX["w4"]), W_)
    G = np.where(water, W_, G)

    # ---- beaches & shore rims ----
    beachy = ~np.isin(biome, [ICE, MOUNTAIN, PEAK, WETLAND, TFOREST, TAIGA, TROPIC])
    if v.season == "winter":
        sand1, sand2 = IDX["n1"], IDX["n2"]
    else:
        sand1, sand2 = IDX["e3"], IDX["e4"]
    G = np.where(land & beachy & (dwater == 1), sand1, G)
    if tw >= 12:
        G = np.where(land & beachy & (dwater == 2), sand2, G)
        G = np.where(land & beachy & (dwater == 3) & (v.bay < 0.5), sand2, G)
    G = np.where(land & ~beachy & (dwater == 1), np.array(DARKER, np.uint8)[G], G)

    v.water = water
    v.dland = dland
    v.dwater = dwater
    return G


# ------------------------------------------------------------------------------------------------------- rivers
def river_paths(f):
    """Downstream links between river tiles -> list of polylines in (column,row) units with a width per point."""
    links = {}
    river = (f.flags & F_RIVER) != 0
    ids = np.nonzero(river.ravel())[0]
    for t in ids:
        e0 = f.elev.ravel()[t]
        best, beste = None, e0 + 0.5
        water_n = None
        for n in f.neighbours(t):
            if f.water.ravel()[n]:
                water_n = n
            elif river.ravel()[n] and f.elev.ravel()[n] < beste:
                best, beste = n, f.elev.ravel()[n]
        if water_n is not None and (best is None or f.elev.ravel()[best] >= e0):
            best = water_n
        if best is not None:
            links[t] = best
    # flow accumulation for width
    acc = {t: 1 for t in ids}
    order = sorted(ids, key=lambda t: -f.elev.ravel()[t])
    for t in order:
        n = links.get(t)
        if n is not None and n in acc:
            acc[n] += acc[t]
    segs = []
    for t, n in links.items():
        segs.append((f.centre(t), f.centre(n), acc.get(t, 1), f.water.ravel()[n]))
    return segs


def draw_rivers(v, G):
    f = v.f
    segs = river_paths(f)
    core = np.zeros((v.h, v.w), bool)
    wide = np.zeros((v.h, v.w), bool)
    for (a, b, acc, into_water) in segs:
        ax, ay = a[0] * v.tw - v.ox, a[1] * v.rh - v.oy
        bx, by = b[0] * v.tw - v.ox, b[1] * v.rh - v.oy
        if max(ax, bx) < -20 or min(ax, bx) > v.w + 20 or max(ay, by) < -20 or min(ay, by) > v.h + 20:
            continue
        if into_water:  # stop at the shore
            bx, by = ax + (bx - ax) * 0.8, ay + (by - ay) * 0.8
        L = math.hypot(bx - ax, by - ay)
        n = max(2, int(L * 2.5))
        nx, ny = -(by - ay) / max(L, 1e-6), (bx - ax) / max(L, 1e-6)
        width = 1.5 + min(acc, 10) * 0.16
        for i in range(n + 1):
            t = i / n
            # meander: a sine that is zero at both tile centres so segments join
            m = math.sin(t * math.pi) * (hash2(int(ax * 7), int(ay * 5), 3) - 0.5) * v.tw * 0.45
            m += math.sin(t * math.pi * 2) * v.tw * 0.08
            x = ax + (bx - ax) * t + nx * m
            y = ay + (by - ay) * t + ny * m
            r = width * v.tw / 24.0
            x0, x1 = int(math.floor(x - r - 1)), int(math.ceil(x + r + 1))
            y0, y1 = int(math.floor(y - r - 1)), int(math.ceil(y + r + 1))
            for yy in range(max(0, y0), min(v.h, y1 + 1)):
                for xx in range(max(0, x0), min(v.w, x1 + 1)):
                    d2 = (xx + 0.5 - x) ** 2 + (yy + 0.5 - y) ** 2
                    if d2 <= r * r:
                        core[yy, xx] = True
                    if d2 <= (r + 1.0) ** 2:
                        wide[yy, xx] = True
    bank = wide & ~core & ~v.water
    D = np.array(DARKER, np.uint8)
    G[bank] = D[G[bank]]
    winter = v.season == "winter"
    ph = v.fno / v.nf
    flow = ((v.wx + v.wy * 2 - int(ph * 16)) % 16)
    col = np.where(flow < 2, IDX["w4"], IDX["w3"]) if not winter else np.where(flow < 3, IDX["n3"], IDX["n2"])
    G[core] = col[core]
    below = np.zeros_like(core)
    below[:-1, :] = ~core[1:, :]
    right = np.zeros_like(core)
    right[:, :-1] = ~core[:, 1:]
    shade_px = core & (below | right) & ~v.water
    G[shade_px] = IDX["w2"] if not winter else IDX["n1"]
    v.river = core
    return G


# ------------------------------------------------------------------------------------------------- decorations
def _stamp(G, sp, x, y, only=None):
    """Draw a flat decal into the ground buffer (no sorting). x,y = anchor position."""
    h, w = sp.px.shape
    X0, Y0 = int(x) - sp.ax, int(y) - sp.ay
    for yy in range(h):
        Y = Y0 + yy
        if Y < 0 or Y >= G.shape[0]:
            continue
        for xx in range(w):
            c = sp.px[yy, xx]
            X = X0 + xx
            if c != S.T and 0 <= X < G.shape[1] and (only is None or only[Y, X]):
                G[Y, X] = c


def jitter_points(v, spacing, seed, margin=16):
    """Deterministic jittered grid points in world pixels covering the window (+margin)."""
    gx0 = (v.ox - margin) // spacing
    gy0 = (v.oy - margin) // spacing
    gx1 = (v.ox + v.w + margin) // spacing + 1
    gy1 = (v.oy + v.h + margin) // spacing + 1
    gy, gx = np.mgrid[gy0:gy1, gx0:gx1]
    jx = hash2(gx, gy, seed)
    jy = hash2(gx, gy, seed + 1)
    r = hash2(gx, gy, seed + 2)
    X = (gx + jx) * spacing - v.ox
    Y = (gy + jy) * spacing - v.oy
    return X.ravel(), Y.ravel(), r.ravel(), (gx * 7919 + gy * 104729).ravel()


def sample(v, arr, x, y, default=0):
    xi, yi = int(x), int(y)
    if 0 <= xi < v.w and 0 <= yi < v.h:
        return arr[yi, xi]
    # outside the window: look the tile up directly
    fx, fr = (xi + v.ox + 0.5) / v.tw, (yi + v.oy + 0.5) / v.rh
    if arr is v.biome:
        tx, ty = nearest_tile(np.array([fx]), np.array([fr]), v.f.W, v.f.H)
        return v.f.biome[ty[0], tx[0]]
    return default


def decorate(v, G, blocked):
    """Flat ground decals (tufts, flowers, pebbles, ripples, cracks) and the object list for trees etc."""
    s = v.season
    objs = []
    lawn = v.roles("lawn")
    bloom = v.roles("bloom")
    winter = s == "winter"

    def ok(x, y):
        xi, yi = int(x), int(y)
        return 0 <= xi < v.w and 0 <= yi < v.h and not v.water[yi, xi] and not blocked[yi, xi] \
            and not v.river[yi, xi]

    # --- grass tufts & flowers ---
    X, Y, R, K = jitter_points(v, 5, 101, 4)
    for x, y, r, k in zip(X, Y, R, K):
        if not ok(x, y):
            continue
        b = v.biome[int(y), int(x)]
        xi, yi = int(x), int(y)
        if b in (WOODLAND, STEPPE, SAVANNA, TFOREST, TAIGA, WETLAND, TUNDRA, TROPIC):
            lush = value_noise(np.array((x + v.ox) / 22.0), np.array((y + v.oy) / 22.0), 707)
            if r < 0.15 + 0.6 * float(lush) ** 1.5:
                base = G[yi, xi]
                hi = LIGHTER[base]
                hi2 = LIGHTER[hi]
                lo = DARKER[base]
                if b in (SAVANNA,) and not winter:
                    hi, hi2 = IDX["d3"], IDX["s4"]
                if winter and b not in (SAVANNA, TROPIC):
                    hi, hi2, lo = IDX["d1"], IDX["d2"], IDX["n1"]
                # tufts in three shapes, lit on the left: a 3-blade "v", a tall pair, a low clump
                shape = int(r * 997) % 3
                pts = (((0, 0, lo), (-1, -1, hi), (1, -1, hi), (0, -1, hi2), (-1, -2, hi2)),
                       ((0, 0, lo), (0, -1, hi), (0, -2, hi2), (1, 0, lo), (1, -1, hi)),
                       ((0, 0, lo), (-1, 0, lo), (1, 0, lo), (-1, -1, hi2), (0, -1, hi)))[shape]
                for (dx, dy, c) in pts:
                    X2, Y2 = xi + dx, yi + dy
                    if 0 <= X2 < v.w and 0 <= Y2 < v.h and not v.water[Y2, X2] and not blocked[Y2, X2]:
                        G[Y2, X2] = c
            elif r < 0.62 and bloom and b in (WOODLAND, STEPPE, WETLAND, TUNDRA):
                c = IDX[bloom[int(r * 1000) % len(bloom)]]
                for (dx, dy) in ((0, 0), (2, 1), (-1, 2)) if r < 0.57 else ((0, 0),):
                    X2, Y2 = xi + dx, yi + dy
                    if ok(X2, Y2):
                        G[Y2, X2] = c
                        if Y2 + 1 < v.h and not v.water[Y2 + 1, X2]:
                            G[Y2 + 1, X2] = DARKER[DARKER[G[Y2 + 1, X2]]] if False else G[Y2 + 1, X2]
        elif b == DESERT and r < 0.5:
            # ripple: a short lit arc over a shaded arc
            for dx in range(-2, 3):
                X2, Y2 = xi + dx, yi + (1 if abs(dx) == 2 else 0)
                if ok(X2, Y2):
                    G[Y2, X2] = IDX["s4"] if dx < 1 else IDX["e4"]
                    if ok(X2, Y2 + 1):
                        G[Y2 + 1, X2] = IDX["e2"]
        elif b == ICE and r < 0.25:
            for i in range(4):
                X2, Y2 = xi + i, yi + (i // 2) * (1 if r < 0.12 else -1)
                if ok(X2, Y2):
                    G[Y2, X2] = IDX["n1"]
        elif b in (MOUNTAIN, PEAK) and r < 0.3:
            if ok(x, y):
                G[yi, xi] = IDX["r1"]
                if ok(x + 1, y):
                    G[yi, xi + 1] = IDX["r4"] if not winter else IDX["n3"]

    # --- trees, bushes, rocks, reeds: y-sorted objects ---
    dens = {TFOREST: (5, 0.95), TAIGA: (5, 0.95), TROPIC: (5, 0.97), WOODLAND: (7, 0.6), SAVANNA: (14, 0.28),
            STEPPE: (14, 0.12), TUNDRA: (11, 0.25), WETLAND: (7, 0.55), MOUNTAIN: (9, 0.4), PEAK: (10, 0.3),
            DESERT: (16, 0.1), ICE: (16, 0.0)}
    for spacing in (5, 7, 9, 11, 14, 16):
        X, Y, R, K = jitter_points(v, spacing, 200 + spacing, 20)
        for x, y, r, k in zip(X, Y, R, K):
            xi, yi = int(x), int(y)
            if not (-14 <= xi < v.w + 14 and -4 <= yi < v.h + 24):
                continue
            b = sample(v, v.biome, x, y)
            sp_, p = dens.get(b, (0, 0))
            if sp_ != spacing:
                continue
            if b in (WOODLAND, SAVANNA, STEPPE, TUNDRA):
                grove = value_noise(np.array((x + v.ox) / 30.0), np.array((y + v.oy) / 30.0), 404)
                p = p * float(np.clip((grove - 0.25) * 2.2, 0.05, 1.6))
            if r > p:
                continue
            if 0 <= xi < v.w and 0 <= yi < v.h and (v.water[yi, xi] or blocked[yi, xi] or v.river[yi, xi]):
                continue
            if 0 <= xi < v.w and 0 <= yi < v.h and v.dwater[yi, xi] <= 2:
                continue
            sel = (k * 2654435761) & 0xffff
            spr = None
            shadow = 0
            if b == TFOREST:
                role = ("leaf", "leafdk", "leaf", "leafalt", "leafdk")[sel % 5]
                if sel % 7 == 0:
                    spr, shadow = S.conifer(sel % 8, 5 + sel % 2, s), 3
                else:
                    spr, shadow = S.deciduous(sel % 12, 3 + (sel // 7) % 4, s, role), 5
            elif b == TAIGA:
                spr, shadow = S.conifer(sel % 8, 5 + sel % 3, s), 3
            elif b == TROPIC:
                role = ("palm", "leafdk", "palm", "leaf")[(sel // 3) % 4]
                ts = s if s in ("spring", "summer") else "summer"   # the tropics stay green
                if sel % 5 == 0:
                    spr, shadow = S.palm(sel % 4, 5, s), 4
                else:
                    spr, shadow = S.deciduous(50 + sel % 8, 3 + (sel // 5) % 4, ts, role), 5
            elif b == WOODLAND:
                if sel % 3 == 0:
                    spr, shadow = S.bush(sel % 6, 2 + sel % 2, s), 3
                else:
                    spr, shadow = S.deciduous(sel % 12, 3 + sel % 3, s, ("leaf", "leafalt", "leafdk")[(sel // 3) % 3]), 4
            elif b == SAVANNA:
                spr, shadow = (S.acacia(sel % 5, 3 + sel % 2, s), 5) if sel % 3 else (S.bush(sel % 6, 2, s, "dry"), 2)
            elif b == STEPPE:
                spr, shadow = (S.bush(sel % 6, 2, s), 2) if sel % 2 else (S.rock(sel % 5, 2 + sel % 2, s), 1)
            elif b == TUNDRA:
                spr, shadow = (S.rock(sel % 5, 2 + sel % 3, s), 1) if sel % 2 else (S.bush(sel % 6, 2, s, "moss"), 2)
            elif b == WETLAND:
                spr, shadow = S.reeds(sel % 9, s), 0
            elif b in (MOUNTAIN, PEAK):
                spr, shadow = S.rock(sel % 5, 2 + sel % 4, s), 1
            elif b == DESERT:
                spr, shadow = S.rock(sel % 5, 2, s), 1
            if spr is not None:
                objs.append((yi, xi, spr, shadow))

    # --- wetland pools: little water pockets with a dark rim ---
    X, Y, R, K = jitter_points(v, 8, 301, 4)
    for x, y, r, k in zip(X, Y, R, K):
        xi, yi = int(x), int(y)
        if not ok(x, y) or v.biome[yi, xi] != WETLAND or r > 0.5:
            continue
        rw = 2 + int(r * 6)
        for dy in range(-1, 2):
            for dx in range(-rw, rw + 1):
                if abs(dx) + abs(dy) * 2 <= rw:
                    X2, Y2 = xi + dx, yi + dy
                    if ok(X2, Y2):
                        G[Y2, X2] = IDX["w2"] if not winter else IDX["n2"]
                        if dy == -1 or abs(dx) == rw - abs(dy) * 2:
                            G[Y2, X2] = IDX["w1"] if not winter else IDX["n1"]
                        if dy == 0 and dx < 0 and (dx + v.fno) % 4 == 0:
                            G[Y2, X2] = IDX["w4"] if not winter else IDX["n3"]

    # --- mountains: one or two big sprites per mountain tile ---
    f = v.f
    t0x, t0y = max(0, int(v.ox / v.tw) - 2), max(0, int(v.oy / v.rh) - 2)
    t1x, t1y = min(f.W, int((v.ox + v.w) / v.tw) + 3), min(f.H, int((v.oy + v.h) / v.rh) + 4)
    for ty in range(t0y, t1y):
        for tx in range(t0x, t1x):
            b = f.biome[ty, tx]
            if b not in (MOUNTAIN, PEAK):
                continue
            t = ty * f.W + tx
            cx, cy = v.tile_px(t)
            hh = hash2(tx, ty, 9)
            n = 2 if b == MOUNTAIN else 1
            for i in range(n):
                jx = (hash2(tx, ty, 10 + i) - 0.5) * v.tw * 0.7
                jy = (hash2(tx, ty, 20 + i) - 0.3) * v.rh * 0.6
                width = int(v.tw * (0.8 + hash2(tx, ty, 30 + i) * 0.35)) if b == MOUNTAIN else int(v.tw * 1.35)
                if i == 1:
                    width = int(width * 0.75)
                spr = S.mountain(int(hh * 1000) + i, width, s, peak=(b == PEAK))
                objs.append((int(cy + jy + v.rh * 0.35), int(cx + jx), spr, -1))
    return objs
