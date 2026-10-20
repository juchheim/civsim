"""
Region / continent view (Z2-Z3): ~6 px per hex column, same palette and light as the detail view.

No people are drawn (08 §1: Z3 shows none). Terrain is shaded per native pixel; forests are a canopy texture of
2-3 px tree glyphs, mountains are 7-11 px peak glyphs, settlements are hut clusters sized by population under a
banner with the civ emblem, camps are tiny tents, and territory uses the same wash + patterned border as Z1.
"""
import math
import numpy as np
from PIL import Image
import sprites as S
import terrain as TR
from palette import IDX, CIV_EMBLEMS
from world import hash2, OCEAN, COAST, TAIGA, TFOREST, WOODLAND, SAVANNA, TROPIC, MOUNTAIN, PEAK, WETLAND, F_RIVER
from render import Canvas, LUT, D_, display_owner
import render as R


def peak_glyph(w, season, peak):
    h = w // 2 + (2 if peak else 1)
    px = np.full((h, w), S.T, np.uint8)
    apex = w // 2
    snow = h * (0.55 if peak else 0.35) + (h * 0.3 if season == "winter" else 0)
    for x in range(w):
        top = int(round(abs(x - apex) * (h - 1) / max(apex, 1)))
        for y in range(top, h):
            lit = x < apex or (x == apex)
            c = "r3" if lit else "r1"
            if y < snow:
                c = "n3" if lit else "n1"
            if not lit and y == top:
                c = "r0" if y >= snow else "n0"
            if y == h - 1:
                c = "r1" if lit else "r0"
            px[y, x] = IDX[c]
    return S.Spr(px, apex, h - 1)


HUT = ["...R...", "..RRr..", ".RRRrr.", "RRRrrrr", ".e1i10.", ".e1i10."]


def hut_glyph(civ, n, season):
    """A cluster of n little thatched huts (5x4 each), lit from the top-left."""
    w, h = 25, 15
    px = np.full((h, w), S.T, np.uint8)
    spots = [(5, 11), (13, 12), (10, 6), (17, 7), (1, 6)][:n]
    R, r = ("d3", "d1") if season != "winter" else ("n3", "n1")
    leg = {"R": R, "r": r, "e": "e3", "1": "e2", "i": "ink", "0": "e1"}
    for (x, y) in sorted(spots, key=lambda p: p[1]):
        for dy, row in enumerate(HUT):
            for dx, ch in enumerate(row):
                if ch != ".":
                    X, Y = x + dx, y + dy - 5
                    if 0 <= X < w and 0 <= Y < h:
                        px[Y, X] = IDX[leg[ch]]
    return S.Spr(px, 12, h - 1)


def flag_glyph(civ, big):
    if big:
        w, h = 9, 11
        px = np.full((h, w), S.T, np.uint8)
        for y in range(h):
            px[y, 0] = IDX["e0"]
        cloth = np.full((7, 8), IDX[f"c{civ}m"], np.uint8)
        cloth[0, :] = IDX[f"c{civ}l"]
        cloth[-1, :] = IDX[f"c{civ}d"]
        cloth[:, -1] = IDX[f"c{civ}d"]
        e = S.emblem(civ)
        sub = cloth[1:6, 1:6]
        sub[e != S.T] = IDX[f"c{civ}l"]
        px[0:7, 1:9] = cloth
        return S.Spr(px, 0, h - 1)
    w, h = 5, 7
    px = np.full((h, w), S.T, np.uint8)
    for y in range(h):
        px[y, 0] = IDX["e0"]
    px[0:3, 1:5] = IDX[f"c{civ}m"]
    px[0, 1:5] = IDX[f"c{civ}l"]
    px[2, 1:5] = IDX[f"c{civ}d"]
    return S.Spr(px, 0, h - 1)


class Overview:
    def __init__(self, frame, x0, y0, cols, rows, tw=6, season=None):
        self.f = frame
        self.v = TR.View(frame, x0, y0, cols, rows, tw, season, 0, 16)

    def render(self):
        v, f = self.v, self.f
        s = v.season
        G = TR.render_ground(v)
        # rivers: 1 px lines along the downstream links
        river = np.zeros(G.shape, bool)
        for (a, b, acc, into) in TR.river_paths(f):
            ax, ay = a[0] * v.tw - v.ox, a[1] * v.rh - v.oy
            bx, by = b[0] * v.tw - v.ox, b[1] * v.rh - v.oy
            n = int(max(abs(bx - ax), abs(by - ay)) * 2) + 1
            L = math.hypot(bx - ax, by - ay) or 1
            nx, ny = -(by - ay) / L, (bx - ax) / L
            for i in range(n + 1):
                t = i / n
                m = math.sin(t * math.pi) * (hash2(int(a[0] * 7), int(a[1] * 5), 3) - 0.5) * v.tw * 0.6
                x, y = int(ax + (bx - ax) * t + nx * m), int(ay + (by - ay) * t + ny * m)
                if 0 <= x < v.w and 0 <= y < v.h:
                    river[y, x] = True
        G[river & ~v.water] = IDX["w3"] if s != "winter" else IDX["n1"]
        v.river = river
        # forests: canopy glyphs on a jittered 3 px grid
        objs = []
        for (gx, gy) in np.argwhere(np.ones((v.h // 3 + 2, v.w // 3 + 2), bool))[:, ::-1]:
            x = gx * 3 + int(hash2(gx + v.ox // 3, gy + v.oy // 3, 1) * 3)
            y = gy * 3 + int(hash2(gx + v.ox // 3, gy + v.oy // 3, 2) * 3)
            if not (0 <= x < v.w and 0 <= y < v.h) or v.water[y, x] or river[y, x]:
                continue
            b = v.biome[y, x]
            r = hash2(gx + v.ox // 3, gy + v.oy // 3, 3)
            dens = {TFOREST: 0.95, TAIGA: 0.95, TROPIC: 0.97, WOODLAND: 0.45, SAVANNA: 0.12}.get(b, 0)
            if r > dens:
                continue
            if b == TAIGA or (b == TFOREST and r < 0.15):
                ramp = ["f0", "f1", "f2"]
                pts = ((0, -2, 2), (-1, -1, 1), (0, -1, 2), (1, -1, 0), (-1, 0, 1), (0, 0, 1), (1, 0, 0))
            elif b == SAVANNA:
                ramp = ["f0", "g1", "g2"] if s != "autumn" else ["d0", "d1", "d2"]
                pts = ((-1, -1, 2), (0, -1, 2), (1, -1, 1), (0, 0, 0))
            else:
                role = {"spring": ["f0", "f2", "f3"], "summer": ["f0", "f1", "f2"], "autumn": ["x0", "x1", "x2"],
                        "winter": ["e0", "e1", "e1"]}[s]
                if b == TROPIC:
                    role = ["f0", "g1", "g2"]
                ramp = role
                pts = ((0, -1, 2), (1, -1, 1), (-1, 0, 1), (0, 0, 1), (1, 0, 0), (0, 1, 0))
            for (dx, dy, k) in pts:
                X, Y = x + dx, y + dy
                if 0 <= X < v.w and 0 <= Y < v.h and not v.water[Y, X]:
                    G[Y, X] = IDX[ramp[k]]
            if s == "winter" and b in (TAIGA, TFOREST, WOODLAND) and 0 <= y - 2 < v.h:
                G[max(0, y - 2 if b == TAIGA else y - 1), x] = IDX["n3"]
        # fields: furrowed specks on plot tiles
        for tile, kind in f.plots.items():
            cx, cy = v.tile_px(tile)
            for yy in range(int(cy - 2), int(cy + 2)):
                for xx in range(int(cx - 2), int(cx + 3)):
                    if 0 <= xx < v.w and 0 <= yy < v.h and not v.water[yy, xx]:
                        crop = v.roles("crop")
                        G[yy, xx] = IDX[crop[2]] if (yy % 2 == 0) else IDX[v.roles("soil")[1]]
                        if kind == 1:
                            G[yy, xx] = IDX[v.roles("leaf")[2] if v.roles("leaf") else "e1"] if (xx + yy) % 2 == 0 else G[yy, xx]
        cv = Canvas(G)
        # mountains
        for ty in range(f.H):
            for tx in range(f.W):
                b = f.biome[ty, tx]
                if b not in (MOUNTAIN, PEAK):
                    continue
                cx, cy = v.tile_px(ty * f.W + tx)
                if -12 < cx < v.w + 12 and -12 < cy < v.h + 12:
                    w = 9 if b == MOUNTAIN else 11
                    w += int(hash2(tx, ty, 4) * 3) * 2 - 2
                    objs.append((int(cy + 2), int(cx + (hash2(tx, ty, 5) - 0.5) * 3), peak_glyph(w, s, b == PEAK)))
        # camps
        for (t, civ) in f.camps:
            cx, cy = v.tile_px(t)
            px = np.array([[S.T, IDX[f"c{civ}l"], S.T], [IDX["e3"], IDX[f"c{civ}m"], IDX["e1"]],
                           [IDX["e3"], IDX["ink"], IDX["e1"]]], np.uint8)
            objs.append((int(cy + 1), int(cx), S.Spr(px, 1, 2)))
        # settlements: hut cluster + banner (capital: big banner with emblem)
        caps = {}
        for i, st in enumerate(f.settlements):
            c = st["civ"]
            if c not in caps or st["population"] > f.settlements[caps[c]]["population"]:
                caps[c] = i
        for i, st in enumerate(f.settlements):
            cx, cy = v.tile_px(st["tile"])
            n = 2 + (st["population"] >= 20) + (st["population"] >= 30) + (st["population"] >= 40)
            # trampled ring
            for yy in range(int(cy - 8), int(cy + 9)):
                for xx in range(int(cx - 14), int(cx + 15)):
                    if 0 <= xx < v.w and 0 <= yy < v.h and ((xx - cx) / 13.5) ** 2 + ((yy - cy) / 7.5) ** 2 < 1 \
                            and not v.water[yy, xx]:
                        G[yy, xx] = IDX["e1"] if hash2(xx + v.ox, yy + v.oy, 77) < 0.2 else IDX["e2"]
            objs.append((int(cy + 5), int(cx), hut_glyph(st["civ"], n, s)))
            big = caps.get(st["civ"]) == i
            objs.append((int(cy - 1), int(cx - 2), flag_glyph(st["civ"], big)))
        for o in objs:
            if o[2].w >= 9 and o[2].h <= 8:
                cv.shadow(o[1] + 1, o[0], max(1, o[2].w // 2 - 1), 0)
        objs.sort(key=lambda o: o[0])
        sc = R.Scene.__new__(R.Scene)
        sc.v, sc.f = v, f
        sc.territory(cv)
        for (y, x, spr) in objs:
            cv.blit(spr, x, y, actor=True)
        self.G = cv.G
        return cv.G

    def image(self, scale):
        img = Image.fromarray(LUT[self.G], "RGB")
        return img.resize((img.width * scale, img.height * scale), Image.NEAREST)
