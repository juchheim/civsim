#!/usr/bin/env python3
"""
CivSim retro pixel renderer (prototype).

    uv run --no-project --with pillow --with numpy python render.py view  --frame ../data/frame40.json \
        --center 60,32 --tiles 30x20 --scale 2 --out map.png [--season autumn] [--native native.png]
    uv run --no-project --with pillow --with numpy python render.py all --data ../data --out .

See README.md. Everything is drawn into a palette-index buffer at native resolution, then scaled with nearest-neighbour.
"""
import argparse
import math
import os
import sys
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sprites as S  # noqa: E402
import terrain as TR  # noqa: E402
from village import Village, thorn_hedge  # noqa: E402
from palette import IDX, RGB, DARKER, LIGHTER, WARM, CIV_BORDER, season_name  # noqa: E402
from world import (Frame, hash2, nearest_tile, fbm, SQ3_2, F_RIVER, OCEAN, COAST, MOUNTAIN, PEAK,  # noqa: E402
                   DEFAULT_DATA)

LUT = np.array(RGB + [(255, 0, 255)] * (256 - len(RGB)), np.uint8)
D_ = np.array(DARKER, np.uint8)
L_ = np.array(LIGHTER, np.uint8)
W_ = np.array(WARM, np.uint8)

ST = {n: i for i, n in enumerate(["Hearth", "LeanTo", "HideTent", "PitHouse", "WattleHouse", "MudbrickHouse",
                                   "Longhouse", "StoragePit", "DryingRack", "Granary", "Kiln", "Quern",
                                   "ThornBarrier", "Lookout", "Weir", "Field", "Pasture", "Orchard"])}
DWELLINGS = [("Longhouse", "longhouse", 9), ("MudbrickHouse", "mudbrick_house", 6), ("WattleHouse", "wattle_house", 6),
             ("PitHouse", "pit_house", 5), ("HideTent", "hide_tent", 4), ("LeanTo", "lean_to", 3)]


# ==================================================================================================== compositing
class Canvas:
    def __init__(self, G):
        self.G = G
        self.h, self.w = G.shape
        self.actor = np.zeros(G.shape, bool)

    def blit(self, sp, x, y, actor=False, recolour=None):
        px = sp.px
        h, w = px.shape
        X0, Y0 = int(round(x)) - sp.ax, int(round(y)) - sp.ay
        x0, y0 = max(0, X0), max(0, Y0)
        x1, y1 = min(self.w, X0 + w), min(self.h, Y0 + h)
        if x0 >= x1 or y0 >= y1:
            return
        src = px[y0 - Y0:y1 - Y0, x0 - X0:x1 - X0]
        m = src != S.T
        if recolour is not None:
            src = recolour[src]
        dst = self.G[y0:y1, x0:x1]
        dst[m] = src[m]
        if actor:
            self.actor[y0:y1, x0:x1] |= m

    def shadow(self, x, y, rx, ry, steps=1):
        x, y = int(round(x)), int(round(y))
        for yy in range(-ry, ry + 1):
            for xx in range(-rx, rx + 1):
                if (xx / (rx + 0.5)) ** 2 + (yy / (ry + 0.5)) ** 2 <= 1.0:
                    X, Y = x + xx, y + yy
                    if 0 <= X < self.w and 0 <= Y < self.h:
                        c = self.G[Y, X]
                        for _ in range(steps):
                            c = D_[c]
                        self.G[Y, X] = c


# ==================================================================================================== settlements
def settlement_layout(f, si, s, v):
    """Deterministic village plan around the hearth, in px offsets. Returns list of (kind, dx, dy, extra)."""
    st = s["structures"]
    pop = s["population"]
    tw = v.tw
    items = []
    rnd = lambda k: hash2(si, k, 4242)  # noqa: E731
    items.append(("hearth", 0, 2, None))
    items.append(("banner", -6, -1, None))
    # dwellings: one per ~5 people, of the best kinds the settlement has built (the frame gives kinds, not counts)
    kinds = [(spr, w) for (name, spr, w) in DWELLINGS if st[ST[name]] > 0]
    if not kinds:
        kinds = [("lean_to", 3)]
    n = max(sum(st[ST[n_]] for n_, _, _ in DWELLINGS), int(math.ceil(pop / 5.0)))
    n = min(n, 11)
    ring = []
    rad = tw * 1.2
    for i in range(n):
        a = (i / n) * 2 * math.pi + rnd(1) * 6.28 + (rnd(10 + i) - 0.5) * 0.35
        r = rad * (0.85 + rnd(30 + i) * 0.3)
        ring.append((a, r))
    for i, (a, r) in enumerate(ring):
        spr = kinds[0][0] if i % 3 != 2 or len(kinds) == 1 else kinds[(i // 3) % len(kinds)][0]
        items.append((spr, math.cos(a) * r * 1.15, math.sin(a) * r * 0.85, None))
    inner = []
    if st[ST["StoragePit"]]:
        inner += ["storage_pit", "storage_pit"]
    if st[ST["Quern"]]:
        inner.append("quern")
    if st[ST["DryingRack"]]:
        inner.append("drying_rack")
    if st[ST["Granary"]]:
        inner.append("granary")
    if st[ST["Kiln"]]:
        inner.append("kiln")
    inner.append("totem")
    for i, k in enumerate(inner):
        a = rnd(2) * 6.28 + i * 2 * math.pi / max(1, len(inner)) + 0.4
        r = tw * 0.62
        items.append((k, math.cos(a) * r * 1.15, math.sin(a) * r * 0.8 + 2, None))
    if st[ST["Lookout"]]:
        a = rnd(3) * 6.28
        items.append(("lookout", math.cos(a) * tw * 1.95, math.sin(a) * tw * 1.5, None))
    if st[ST["ThornBarrier"]]:
        items.append(("thorn_ring", 0, 0, tw * 1.75))
    return items


def plot_texture(v, G, blocked, tile, kind, fr):
    """Fields: a raised rectangular plot per hex (rows of hexes become a brick patchwork) with furrows and seasonal
    crops; orchards: the same plot with rows of little fruit trees (returned as objects)."""
    f = v.f
    cx, cy = v.tile_px(tile)
    tw, rh = v.tw, v.rh
    hw, hh = int(tw / 2) - 1, int(rh / 2) - 1
    x0, x1 = int(round(cx)) - hw, int(round(cx)) + hw - 1
    y0, y1 = int(round(cy)) - hh, int(round(cy)) + hh - 1
    objs = []
    crop = [IDX[c] for c in v.roles("crop")]
    soil = [IDX[c] for c in v.roles("soil")]
    tx, ty = tile % f.W, tile // f.W
    hv = hash2(tx, ty, 5)
    vertical = hv < 0.35
    tone = 1 if hv > 0.7 else 0          # patchwork: some plots a shade riper
    season = v.season
    winter = season == "winter"
    for y in range(max(0, y0), min(v.h, y1 + 1)):
        for x in range(max(0, x0), min(v.w, x1 + 1)):
            if v.water[y, x] or v.river[y, x]:
                continue
            blocked[y, x] = True
            top, left = y == y0, x == x0
            bot, right = y == y1, x == x1
            if kind == 1:
                if top or left or bot or right:
                    G[y, x] = D_[G[y, x]] if (x + y) % 2 == 0 else G[y, x]
                continue
            if top or left:
                G[y, x] = IDX["e3"] if not winter else IDX["n3"]
                continue
            if bot or right:
                G[y, x] = IDX["e1"] if not winter else IDX["n0"]
                continue
            k = (x - x0) if vertical else (y - y0)
            along = (y - y0) if vertical else (x - x0)
            row = k % 3
            if season == "spring":
                c = soil[1] if row == 0 else soil[2]
                if row == 1 and along % 2 == 0:
                    c = crop[2 + ((along // 2 + tone) % 2)]
            elif season == "summer":
                c = soil[1] if row == 0 else (crop[2 + tone] if (along + row) % 3 else crop[1 + tone])
                if row == 2 and along % 4 == 1:
                    c = crop[3]
            elif season == "autumn":
                c = crop[0] if row == 0 else (crop[2 + tone] if (along + row) % 4 else crop[1])
                if row == 1 and along % 3 == 0:
                    c = crop[3]
            else:
                c = soil[1] if row == 0 else (crop[3] if (along * 3 + row) % 5 else crop[2])
            G[y, x] = c
    if kind == 1:
        for i, (ox_, oy_) in enumerate([(-5, -4), (4, -4), (-1, 1), (8, 1) if False else (-9, 1), (-5, 6), (4, 6)]):
            X, Y = int(cx + ox_ + 1), int(cy + oy_ + 2)
            if 0 <= X < v.w and 0 <= Y < v.h and (v.river[max(0, Y - 3):Y + 2, max(0, X - 4):X + 5].any()
                                                  or v.water[Y, X]):
                continue          # keep the river and its weir visible
            objs.append((Y, X, fruit_tree((i + tile) % 5, v.season), 2))
    return objs


@S.lru_cache(maxsize=None)
def fruit_tree(seed, season):
    sp = S.bush(seed % 5, 3, season, "leafdk" if season == "autumn" else "leaf")
    px = sp.px.copy()
    fruit = S.SEASON_ROLES[season]["fruit"]
    if fruit:
        rng = np.random.default_rng(seed)
        ys, xs = np.nonzero(px != S.T)
        for k in range(3 if season != "spring" else 6):
            i = rng.integers(0, len(ys))
            if ys[i] < px.shape[0] - 2:
                px[ys[i], xs[i]] = IDX[fruit[k % len(fruit)]]
    # little trunk
    h, w = px.shape
    out = np.full((h + 2, w), S.T, np.uint8)
    out[:h] = px
    out[h:, w // 2] = IDX["e1"]
    return S.Spr(out, w // 2, h + 1)


def thorn_ring(v, cx, cy, r, season, objs, gaps):
    n = int(2 * math.pi * r / 5)
    for i in range(n):
        a = i / n * 2 * math.pi
        if any(abs(((a - g + math.pi) % (2 * math.pi)) - math.pi) < 0.28 for g in gaps):
            continue
        x, y = cx + math.cos(a) * r * 1.15, cy + math.sin(a) * r * 0.85
        objs.append((int(y), int(x), S.structure("thorn", season), 1))


def paths(v, G, blocked, a, b, seed):
    """A worn footpath: a slightly wandering 1-2 px dirt line with dithered edges."""
    ax, ay = a
    bx, by = b
    L = math.hypot(bx - ax, by - ay)
    if L < 1:
        return
    n = int(L * 1.5)
    nx, ny = -(by - ay) / L, (bx - ax) / L
    winter = v.season == "winter"
    c1, c2 = (IDX["e2"], IDX["e3"]) if not winter else (IDX["n0"], IDX["n1"])
    for i in range(n + 1):
        t = i / n
        m = math.sin(t * math.pi * 2 + seed) * 2.0 * math.sin(t * math.pi)
        x, y = int(round(ax + (bx - ax) * t + nx * m)), int(round(ay + (by - ay) * t + ny * m))
        for (dx, dy, c) in ((0, 0, c2), (1, 0, c1), (0, 1, c1)):
            X, Y = x + dx, y + dy
            if 0 <= X < v.w and 0 <= Y < v.h and not v.water[Y, X] and not v.river[Y, X]:
                if c == c1 and (X + Y) % 2:
                    continue
                G[Y, X] = c
                blocked[Y, X] = True


# ==================================================================================================== figures
ACT_TOOL = {1: ("bend", "basket"), 2: ("stand", "spear"), 3: ("work", "net"), 4: ("bend", "hoe"), 5: ("work", "axe"),
            6: ("bend", "basket"), 7: ("bend", "hammer"), 8: ("work", "hammer"), 9: ("bend", None),
            10: ("rest", None), 11: ("stand", "torch"), 12: ("stand", "spear"), 14: ("rest", None),
            15: ("walk", None), 16: ("walk", None), 17: ("walk", "spear"), 18: ("walk", "spear"), 19: ("stand", "spear")}
CARRY_OF = {1: "plants", 2: "meat", 3: "fish", 5: "wood", 6: "plants", 4: "grain", 16: "grain"}
TOOL_HAND = {"stand": (5, 6), "work": (6, 0), "bend": (6, 6), "walk": (5, 6)}


def figure_sprite(season, civ, age, act, flags, fh, frame):
    female = bool(flags & 1)
    walking = bool(flags & 2)
    pale = bool(flags & 12)
    warrior = bool(flags & 128)
    armed = bool(flags & 256) or warrior
    pose, tool = ACT_TOOL.get(act, ("stand", None))
    if walking:
        pose = "walk"
    if age == 0:
        kind = "child"
        pose = "walk" if walking or (fh * 7 + frame) % 6 < 3 else "stand"
        tool = None
    elif age == 2:
        kind = "elder"
        tool = None if pose != "work" else tool
        pose = "walk" if walking else "stand"
    else:
        kind = "adult"
    if pose == "rest":
        return S.person(season, civ, "resting", "stand", pale=pale, cloth=int(fh * 13) % 4), None, None
    if armed and age != 0:
        tool = "spear"
    real_pose = pose
    if pose == "walk":
        real_pose = "walk1" if ((frame // 2 + int(fh * 4)) % 2 == 0) else ("walk2" if kind == "adult" else "stand")
        if kind == "elder":
            real_pose = "walk1" if (frame // 2) % 2 == 0 else "stand"
    if pose == "work" and kind == "adult":
        # swing: alternate the raised and lowered arm
        real_pose = "work" if (frame + int(fh * 8)) % 4 < 2 else "stand"
    cloth = int(fh * 13) % 4
    if season == "winter" and int(fh * 31) % 3:
        cloth = 3        # winter furs
    sp = S.person(season, civ, kind, real_pose, female=female, warrior=warrior, pale=pale,
                  hair=int(fh * 7) % 4 if kind != "elder" else 0, cloth=cloth, skin=int(fh * 17) % 2)
    return sp, tool, real_pose


def draw_figure(cv, season, civ, age, act, flags, x, y, fh, frame, facing_left, carry=None):
    sp, tool, pose = figure_sprite(season, civ, age, act, flags, fh, frame)
    if facing_left:
        sp = sp.flip()
    # spear held upright beside the body; other tools in the hand
    if tool:
        tsp = S.tool(tool)
        if pose in ("work",) and tool in ("axe", "hammer"):
            hx, hy = (6, 0)
        elif tool == "spear":
            hx, hy = (6, 7) if pose != "work" else (6, 3)
        elif tool in ("hoe", "basket"):
            hx, hy = (6, 8) if pose == "bend" else (6, 7)
        elif tool == "net":
            hx, hy = (6, 5)
        else:
            hx, hy = (6, 6)
        if facing_left:
            tsp = tsp.flip()
            hx = sp.w - 1 - hx
        X0, Y0 = int(x) - sp.ax, int(y) - sp.ay
        behind = tool == "spear"
        if behind:
            cv.blit(tsp, X0 + hx, Y0 + hy, actor=True)
    cv.blit(sp, x, y, actor=True)
    if tool and not (tool == "spear"):
        cv.blit(tsp, X0 + hx, Y0 + hy, actor=True)
    if carry:
        csp = S.carry(carry, season)
        X0, Y0 = int(x) - sp.ax, int(y) - sp.ay
        bx = X0 + (5 if facing_left else 1)
        cv.blit(csp, bx, Y0 + 5, actor=True)


# ==================================================================================================== scene
class Scene:
    def __init__(self, frame, center, tiles, tw=24, season=None, fno=0, nf=16, spread=3.0, actors=True):
        self.f = frame
        self.actors = actors
        cx, cy = center
        cols, rows = tiles
        self.v = TR.View(frame, cx - cols / 2.0, cy - rows / 2.0, cols, rows, tw, season, fno, nf)
        self.spread = spread
        self.fno, self.nf = fno, nf

    def render(self):
        v = self.v
        f = self.f
        s = v.season
        G = TR.render_ground(v)
        G = TR.draw_rivers(v, G)
        blocked = np.zeros(G.shape, bool)
        objs = []
        # ---------- settlements: plan, clear, trample ----------
        sett_px = []
        self.villages = []
        village_tiles = set()
        for si, st in enumerate(f.settlements):
            cx, cy = v.tile_px(st["tile"])
            if not (-90 < cx < v.w + 90 and -90 < cy < v.h + 90):
                sett_px.append(None)
                self.villages.append(None)
                continue
            sett_px.append((cx, cy))
            vil = Village(f, si, st, v)
            self.villages.append(vil)
            village_tiles.update(vil.tiles)
            memb = vil.membership()
            vil.memb = memb
            blocked |= memb > 0.3
            lvl = vil.membership(0.45) + (v.bay - 0.5) * 0.18
            trample = (lvl > 0.62) & ~v.water & ~v.river
            speck = hash2(v.wx, v.wy, 808)
            tone = fbm(v.fx * 4.0, v.fr * 4.0, 809, 2)
            if s == "winter":
                G[trample] = np.where(tone[trample] < 0.42, IDX["e2"], np.where(speck[trample] < 0.1, IDX["n3"], IDX["n2"]))
            else:
                G[trample] = np.where(speck[trample] < 0.05, IDX["s4"],
                                      np.where((tone[trample] < 0.36) | (speck[trample] > 0.96), IDX["e3"], IDX["e4"]))
            rim = (lvl > 0.5) & (lvl <= 0.62) & ~v.water & ~v.river & (v.bay < 0.5)
            G[rim] = IDX["e3"] if s != "winter" else IDX["n1"]
        # fields and orchards
        for tile, kind in f.plots.items():
            cx, cy = v.tile_px(tile)
            if tile in village_tiles:
                continue
            if -30 < cx < v.w + 30 and -30 < cy < v.h + 30:
                objs += plot_texture(v, G, blocked, tile, kind, f)
        # pastures: fenced paddocks on free tiles next to the village
        for si, st in enumerate(f.settlements):
            if sett_px[si] is None:
                continue
            npast = st["structures"][ST["Pasture"]]
            if npast <= 0:
                continue
            cand = []
            for t in set(n2 for n1 in f.neighbours(st["tile"]) for n2 in f.neighbours(n1)):
                if f.hexdist(t, st["tile"]) == 2 and t not in f.plots and not f.water.ravel()[t] \
                        and f.biome.ravel()[t] not in (MOUNTAIN, PEAK):
                    cand.append(t)
            cand.sort(key=lambda t: hash2(t, si, 8))
            for t in cand[:npast]:
                objs += pasture(v, G, blocked, t, s)
        # footpaths: hearth -> each plot / camp of this settlement
        for si, st in enumerate(f.settlements):
            if sett_px[si] is None:
                continue
            c = sett_px[si]
            near = [t for t in f.plots if f.hexdist(t, st["tile"]) <= 3]
            near.sort(key=lambda t: hash2(t, 3, 3))
            for t in near[:3]:
                paths(v, G, blocked, c, v.tile_px(t), t % 7)
        # village objects
        fires = []
        for si, st in enumerate(f.settlements):
            vil = self.villages[si]
            if vil is None:
                continue
            civ = st["civ"]
            for kind, x, y in vil.items:
                sp = vil.sprite(kind)
                if kind == "hearth":
                    objs.append((y, x, sp, 0, "hearth"))
                    fires.append((x, y))
                elif kind in ("banner", "totem", "lookout"):
                    objs.append((y, x, sp, 1))
                else:
                    objs.append((y, x, sp, max(2, sp.w // 4)))
            if st["structures"][ST["ThornBarrier"]]:
                thorn_hedge(v, vil, vil.memb, objs, s)
            # weir on the nearest river tile
            if st["structures"][ST["Weir"]]:
                for t in f.neighbours(st["tile"]) + [st["tile"]]:
                    if f.flags.ravel()[t] & F_RIVER and not f.water.ravel()[t]:
                        wx_, wy_ = v.tile_px(t)
                        objs.append((int(wy_ + 2), int(wx_), S.structure("weir", s), 0))
                        break
        # seasonal camps
        for (t, civ) in f.camps:
            cx, cy = v.tile_px(t)
            if -20 < cx < v.w + 20 and -20 < cy < v.h + 20:
                objs.append((int(cy), int(cx), S.structure("hide_tent", s, civ), 3))
                objs.append((int(cy + 4), int(cx + 9), S.structure("hearth", s), 0, "hearth"))
                objs.append((int(cy - 1), int(cx - 8), S.banner(civ, 11), 1))
                fires.append((cx + 9, cy + 4))
                yy, xx = np.ogrid[0:v.h, 0:v.w]
                blocked |= ((xx - cx) ** 2 + (yy - cy) ** 2) < 13 ** 2
        # ---------- terrain objects (trees etc.) ----------
        objs += [(o[0], o[1], o[2], o[3]) for o in TR.decorate(v, G, blocked)]
        # ---------- herds ----------
        for (X, Y, sp_i, n) in (f.herds if self.actors else []):
            x, y = v.unit_to_px(X, Y)
            if not (-30 < x < v.w + 30 and -30 < y < v.h + 30):
                continue
            k = max(2, min(7, n // 9))
            name = S.HERD_SPECIES[sp_i]
            for i in range(k):
                a = hash2(int(X), i, 61) * 6.28
                r = 3 + hash2(int(Y), i, 62) * 11
                hx, hy = x + math.cos(a) * r * 1.3, y + math.sin(a) * r * 0.8
                for sp_xy in sett_px:
                    if sp_xy is None:
                        continue
                    ddx, ddy = hx - sp_xy[0], hy - sp_xy[1]
                    dd = math.hypot(ddx / 1.15, ddy / 0.85)
                    if dd < v.tw * 2.1:
                        k_ = v.tw * 2.1 / max(dd, 0.5)
                        hx, hy = sp_xy[0] + ddx * k_ + (hash2(i, 3, 5) - 0.5) * 4, sp_xy[1] + ddy * k_
                xi, yi = int(hx), int(hy)
                if 0 <= xi < v.w and 0 <= yi < v.h and (v.water[yi, xi]):
                    continue
                graze = hash2(int(X), i, 63) < 0.45
                spr = S.animal_grazing(name, s) if graze and name != "rabbit" else S.animal(name, s)
                # a gentle drift across the animation loop
                if hash2(int(X), i, 64) < 0.5:
                    spr = spr.flip()
                objs.append((int(hy), int(hx), spr, 2))
        # ---------- people ----------
        figs = self.place_figures(sett_px, blocked) if self.actors else []
        # ---------- shadows (ground only), then y-sorted draw ----------
        cv = Canvas(G)
        for o in objs:
            y, x, spr, sh = o[0], o[1], o[2], o[3]
            if sh and sh > 0:
                cv.shadow(x + max(1, spr.w // 6), y, max(1, int(spr.w * 0.42)), max(1, min(3, sh // 2 + 1)))
        for (y, x, info) in figs:
            cv.shadow(x + 1, y, 2, 0)
        # firelight: a few warm pixels on the ground around each fire
        for (x, y) in fires:
            rr = 7
            for yy in range(int(y) - rr, int(y) + rr):
                for xx in range(int(x) - rr - 3, int(x) + rr + 3):
                    if 0 <= xx < v.w and 0 <= yy < v.h:
                        d = math.hypot((xx - x) / 1.3, yy - y)
                        flick = 1.0 + 0.12 * math.sin(2 * math.pi * self.fno / self.nf * 4 + xx)
                        if (d < 4 * flick and v.bay[yy, xx] < 0.5) or (d < 6.5 * flick and v.bay[yy, xx] < 0.2):
                            G[yy, xx] = W_[G[yy, xx]]
        drawlist = []
        for o in objs:
            drawlist.append((o[0], 0, o))
        for fg in figs:
            drawlist.append((fg[0], 1, fg))
        drawlist.sort(key=lambda d: (d[0], d[1]))
        for (_, kind, o) in drawlist:
            if kind == 0:
                y, x, spr = o[0], o[1], o[2]
                is_actor = len(o) > 4 or spr.h < 30
                cv.blit(spr, x, y, actor=False)
                if len(o) > 4 and o[4] == "hearth":
                    fs = S.fire((self.fno + x) % 4)
                    cv.blit(fs, x, y - 2)
            else:
                y, x, (civ, age, act, flags, fh, fno, fl, carry) = o
                draw_figure(cv, s, civ, age, act, flags, x, y, fh, fno, fl, carry)
        # ---------- smoke ----------
        for (x, y) in fires:
            self.smoke(cv, x, y - 6)
        # ---------- territory: wash + dashed border on top of terrain, under people and buildings ----------
        self.territory(cv)
        self.G = cv.G
        return cv.G

    def smoke(self, cv, x, y):
        winter = self.v.season == "winter"
        for k in range(4):
            t = ((self.fno / self.nf) * 4 + k) % 4 / 4.0      # 0..1 along the plume
            px = x + math.sin(t * 5 + k) * 1.5 + t * 7
            py = y - t * 18
            r = 1 if t < 0.35 else 2
            col = IDX["r4"] if t < 0.5 else IDX["r3"]
            for yy in range(-r, r + 1):
                for xx in range(-r, r + 1):
                    if xx * xx + yy * yy <= r * r + (0 if t < 0.7 else -1):
                        X, Y = int(px + xx), int(py + yy)
                        if 0 <= X < cv.w and 0 <= Y < cv.h:
                            if t > 0.6 and (X + Y) % 2:
                                continue
                            cv.G[Y, X] = col if not (xx < 0 and yy < 0) else IDX["n3"] if winter else IDX["s4"]

    def place_figures(self, sett_px, blocked):
        v, f = self.v, self.f
        out = []
        occ = {}
        taken = []
        for i, fg in enumerate(f.figures):
            X, Y, civ, age, act, flags, rung, si = fg
            x, y = v.unit_to_px(X, Y)
            home = sett_px[si] if 0 <= si < len(sett_px) else None
            if home is not None:
                hx, hy = home
                dx, dy = x - hx, y - hy
                rr = math.hypot(dx, dy)
                vil = self.villages[si]
                spot = vil.free_spot(i, taken) if (rr < v.tw * 0.45 and vil is not None) else None
                if spot is not None:
                    x, y = spot
                elif rr < v.tw * 0.45:
                    # home-bound people: the sim jitters them within a third of a tile around the hearth; the view
                    # lays them out on the plaza ring (and a few between the houses and the thorn ring) so thirty
                    # children do not stack on one hearth
                    ang = math.atan2(dy, dx) + hash2(i, 5, 5) * 0.6
                    u = rr / (v.tw * 0.45)
                    if hash2(i, 6, 6) < 0.3:
                        rad = v.tw * (1.5 + 0.2 * u)
                    else:
                        rad = 7 + u * (v.tw * 0.85 - 7)
                    x, y = hx + math.cos(ang) * rad * 1.15, hy + math.sin(ang) * rad * 0.85 + 3
            if not (-8 < x < v.w + 8 and -4 < y < v.h + 14):
                continue
            fh = hash2(i, 1, 99)
            walking = bool(flags & 2)
            # walk loop: walkers pace out and back along their heading over the animation
            facing_left = fh < 0.5
            if walking and home is not None:
                hx, hy = home
                ang = math.atan2(y - hy, x - hx)
                ph = (self.fno / self.nf)
                tri = 1 - abs(ph * 2 - 1)               # 0 -> 1 -> 0 over the loop
                stepx = math.cos(ang) * 10 * (tri - 0.5)
                stepy = math.sin(ang) * 10 * (tri - 0.5)
                going_out = ph < 0.5
                x, y = x + stepx, y + stepy
                facing_left = (math.cos(ang) < 0) == going_out
            elif act in (13,) and home is not None:
                # children mill about: a small wobble
                x += math.sin(2 * math.pi * self.fno / self.nf * 2 + fh * 20) * 1.5
            xi, yi = int(round(x)), int(round(y))
            # avoid stacking exactly
            key = (xi // 3, yi // 3)
            while key in occ and occ[key] > 1:
                xi += 3
                key = (xi // 3, yi // 3)
            occ[key] = occ.get(key, 0) + 1
            carry = CARRY_OF.get(act) if walking and age == 1 and (fh * 10) % 2 < 1 else None
            out.append((yi, xi, (civ, age, act, flags, fh, self.fno, facing_left, carry)))
        return out

    def territory(self, cv):
        v, f = self.v, self.f
        G = cv.G
        # smooth ownership: each civ's membership is sampled like elevation (smoothly between hex centres), so borders
        # are rounded curves that still follow the sim's hexes
        wx = (fbm(v.fx * 0.9, v.fr * 0.9, 11) - 0.5) * 0.25
        wy = (fbm(v.fx * 0.9, v.fr * 0.9, 23) - 0.5) * 0.25
        best = np.full(v.fx.shape, 0.5, np.float32)
        own = np.full(v.fx.shape, -1, np.int32)
        sea = (f.biome == OCEAN)
        owner = display_owner(f)
        for c in np.unique(owner):
            if c < 0:
                continue
            memb = ((owner == c) & ~sea).astype(np.float32)
            val = TR.smooth_sample(memb, v.fx + wx, v.fr + wy)
            m = val > best
            best = np.where(m, val, best)
            own = np.where(m, c, own)
        edge = np.zeros(own.shape, bool)
        for (dy, dx) in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sh = np.roll(own, (dy, dx), (0, 1))
            edge |= (sh != own)
        edge[0, :] = edge[-1, :] = False
        edge[:, 0] = edge[:, -1] = False
        inside = TR.distance_from(edge & (own >= 0), 10)
        wx_, wy_ = v.wx, v.wy
        for c in np.unique(own):
            if c < 0:
                continue
            m = own == c
            dark, mid, light = IDX[f"c{c}d"], IDX[f"c{c}m"], IDX[f"c{c}l"]
            # wash: densest just inside the border, fading inward to a sparse diagonal hatch
            d = inside
            # inner wash: diagonal hatching (reads as a map overlay, never as water), densest at the border
            diag = (wx_ + wy_) % 4 == 0
            diag2 = ((wx_ + wy_) % 8 == 0) & (wy_ % 2 == 0)
            if v.tw >= 12:
                band = m & ~cv.actor & (((d >= 2) & (d <= 5) & diag) | ((d >= 6) & (d <= 9) & diag2))
            else:
                band = m & ~cv.actor & (((d >= 2) & (d <= 4) & diag) | ((d >= 5) & (d <= 6) & diag2))
            G[band] = np.where(v.water[band], light, mid)
            # border line with the civ's dash pattern (08 §4.3); gaps show the ground
            pat = CIV_BORDER[c]
            t = (wx_ + wy_)
            if pat == "solid":
                on = np.ones(m.shape, bool)
            elif pat == "long":
                on = (t % 10) < 7
            elif pat == "dot":
                on = (t % 4) < 2
            elif pat == "dashdot":
                on = ((t % 12) < 6) | ((t % 12) == 8) | ((t % 12) == 9)
            elif pat == "short":
                on = (t % 6) < 3
            else:
                on = (t % 8) < 5
            if v.tw >= 12:
                line = m & (d <= 1) & ~cv.actor & on
                G[line] = np.where(d[line] == 0, light, mid)
            else:   # small scale: an unbroken 2 px line whose outer pixel carries the pattern
                line = m & (d <= 1) & ~cv.actor
                G[line] = np.where((d[line] == 0) & on[line], light, mid)
            # dark rim on the outside of each dash (only where the outside is unclaimed)
            outer = TR.dilate(line & edge, 1) & ~m & (own < 0) & ~cv.actor
            G[outer] = dark

    def image(self, scale):
        img = Image.fromarray(LUT[self.G], "RGB")
        if scale != 1:
            img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
        return img


def display_owner(f):
    """Ownership as drawn: single unclaimed tiles enclosed by one civ are filled so they do not draw as rings."""
    if getattr(f, "_disp_owner", None) is not None:
        return f._disp_owner
    own = f.owner.copy()
    for t in range(f.W * f.H):
        if own.ravel()[t] >= 0 or f.water.ravel()[t]:
            continue
        nb = [f.owner.ravel()[n] for n in f.neighbours(t)]
        vals = [c for c in nb if c >= 0]
        if len(vals) >= 5 and len(set(vals)) == 1:
            own.ravel()[t] = vals[0]
    f._disp_owner = own
    return own


def pasture(v, G, blocked, tile, season):
    cx, cy = v.tile_px(tile)
    objs = []
    rx, ry = int(v.tw * 0.42), int(v.rh * 0.42)
    yy, xx = np.ogrid[0:v.h, 0:v.w]
    inside = (np.abs(xx - cx) <= rx) & (np.abs(yy - cy) <= ry)
    blocked |= (np.abs(xx - cx) <= rx + 3) & (np.abs(yy - cy) <= ry + 3)
    graze = inside & ~v.water & (((xx + yy) % 5) == 0)
    G[graze] = D_[G[graze]]
    post = IDX["e1"]
    rail = IDX["e3"]
    for x in range(int(cx - rx), int(cx + rx) + 1):
        for y, top in ((int(cy - ry), True), (int(cy + ry), False)):
            if 0 <= x < v.w and 0 <= y < v.h:
                G[y, x] = rail
                if (x - int(cx - rx)) % 4 == 0:
                    if y - 1 >= 0:
                        G[y - 1, x] = post
                    G[y, x] = post
                if y + 1 < v.h and not top:
                    G[y + 1, x] = D_[G[y + 1, x]]
    for y in range(int(cy - ry), int(cy + ry) + 1):
        for x in (int(cx - rx), int(cx + rx)):
            if 0 <= x < v.w and 0 <= y < v.h:
                G[y, x] = rail if (y - int(cy - ry)) % 3 else post
    for i in range(3):
        a = hash2(tile, i, 71)
        sx = cx + (a - 0.5) * rx * 1.4
        sy = cy + (hash2(tile, i, 72) - 0.5) * ry * 1.2 + 3
        spr = S.animal("sheep", season)
        if a < 0.5:
            spr = spr.flip()
        objs.append((int(sy), int(sx), spr, 2))
    return objs


# ==================================================================================================== CLI
def capital(f, civ=None):
    best = None
    for i, s in enumerate(f.settlements):
        if civ is not None and s["civ"] != civ:
            continue
        if best is None or s["population"] > f.settlements[best]["population"]:
            best = i
    return best


def tile_xy(f, t):
    return f.centre(t)


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["view", "all"])
    ap.add_argument("--frame", default=os.path.join(DEFAULT_DATA, "frame40.json"))
    ap.add_argument("--data", default=DEFAULT_DATA)
    ap.add_argument("--center", default=None, help="tile x,y (default: largest settlement)")
    ap.add_argument("--tiles", default="30x20")
    ap.add_argument("--tw", type=int, default=24)
    ap.add_argument("--scale", type=int, default=2)
    ap.add_argument("--season", default=None)
    ap.add_argument("--frame-no", type=int, default=0)
    ap.add_argument("--out", default="out.png")
    ap.add_argument("--native", default=None)
    a = ap.parse_args(argv)
    if a.cmd == "view":
        f = Frame(a.frame)
        if a.center:
            cx, cy = [float(t) for t in a.center.split(",")]
            cx, cy = cx + 0.5, cy + 0.5
        else:
            cx, cy = f.centre(f.settlements[capital(f)]["tile"])
        cols, rows = [int(t) for t in a.tiles.split("x")]
        sc = Scene(f, (cx, cy), (cols, rows), a.tw, a.season, a.frame_no)
        sc.render()
        sc.image(a.scale).save(a.out)
        if a.native:
            sc.image(1).save(a.native)
    else:
        import shots
        shots.all_shots(a.data, a.out)


if __name__ == "__main__":
    main()
