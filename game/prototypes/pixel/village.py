"""
Village plans: where a settlement's structures and home-bound people go, deterministically, from the frame data.

The sim gives a settlement tile, its population and which structure kinds it has built (counts of kinds, not
buildings). The view turns that into a little village: the hearth plaza on the settlement tile, dwellings and stores
on the neighbouring tiles that are not fields or water, a thorn hedge following the outline of that footprint, and
the home-bound people (children, the sick, fire tenders) spread across the open ground between the buildings.
"""
import math
import numpy as np
import sprites as S
from palette import IDX
from world import hash2, smooth_sample, fbm, MOUNTAIN, PEAK

ST = {n: i for i, n in enumerate(["Hearth", "LeanTo", "HideTent", "PitHouse", "WattleHouse", "MudbrickHouse",
                                   "Longhouse", "StoragePit", "DryingRack", "Granary", "Kiln", "Quern",
                                   "ThornBarrier", "Lookout", "Weir", "Field", "Pasture", "Orchard"])}
DWELLINGS = [("Longhouse", "longhouse"), ("MudbrickHouse", "mudbrick_house"), ("WattleHouse", "wattle_house"),
             ("PitHouse", "pit_house"), ("HideTent", "hide_tent"), ("LeanTo", "lean_to")]


class Village:
    def __init__(self, f, si, st, v):
        self.f, self.si, self.st, self.v = f, si, st, v
        self.civ = st["civ"]
        t = st["tile"]
        self.tile = t
        self.cx, self.cy = v.tile_px(t)
        ok = lambda n: (n not in f.plots and not f.water.ravel()[n]  # noqa: E731
                        and f.biome.ravel()[n] not in (MOUNTAIN, PEAK))
        nb = f.neighbours(t)
        nb.sort(key=lambda n: hash2(n, si, 17))
        self.free = [n for n in nb if ok(n)]
        if len(self.free) < 2:   # crowded by fields: build on the nearest two plots anyway
            self.free += [n for n in nb if n not in self.free and not f.water.ravel()[n]][:2 - len(self.free)]
        self.tiles = [t] + self.free
        self.items = []     # (kind, x, y)
        self.boxes = []
        self.plan()

    # ------------------------------------------------------------------------------------------------ layout
    def plan(self):
        st, v = self.st["structures"], self.v
        pop = self.st["population"]
        cx, cy = self.cx, self.cy
        self.add("hearth", cx, cy + 2)
        self.add("banner", cx - 8, cy - 2)
        self.add("totem", cx + 9, cy - 5)
        kinds = [spr for (name, spr) in DWELLINGS if st[ST[name]] > 0] or ["lean_to"]
        n_d = max(len(kinds), int(math.ceil(pop / 5.0)))
        util = []
        if st[ST["Granary"]]:
            util.append("granary")
        if st[ST["Kiln"]]:
            util.append("kiln")
        if st[ST["DryingRack"]]:
            util.append("drying_rack")
        # slots on the free neighbour tiles: two buildings each, set back from the plaza
        slots = []
        for n in self.free:
            nx, ny = v.tile_px(n)
            dx, dy = nx - cx, ny - cy
            L = math.hypot(dx, dy) or 1
            ux, uy = dx / L, dy / L
            px_, py_ = -uy, ux
            slots.append((nx - ux * 2 + px_ * 7, ny - uy * 2 + py_ * 6))
            slots.append((nx + ux * 3 - px_ * 7, ny + uy * 3 - py_ * 6))
        slots.sort(key=lambda p: hash2(int(p[0] * 3), int(p[1] * 5), self.si))
        # dwellings, mixing in the lesser kinds every third house
        n_d = min(n_d, max(1, len(slots) - len(util)))
        for i in range(n_d):
            k = kinds[0] if (i % 3 != 2 or len(kinds) == 1) else kinds[1 + (i // 3) % (len(kinds) - 1)]
            x, y = slots[i]
            self.add(k, x, y)
        for j, u in enumerate(util):
            if n_d + j < len(slots):
                x, y = slots[n_d + j]
                self.add(u, x, y)
        # small stores at the plaza edge
        small = []
        if st[ST["StoragePit"]]:
            small += ["storage_pit", "storage_pit"]
        if st[ST["Quern"]]:
            small.append("quern")
        for i, k in enumerate(small):
            a = 1.9 + i * 1.15
            self.add(k, cx + math.cos(a) * 11, cy + math.sin(a) * 8 + 3)
        if st[ST["Lookout"]] and self.free:
            n = self.free[-1]
            nx, ny = v.tile_px(n)
            self.add("lookout", cx + (nx - cx) * 1.45, cy + (ny - cy) * 1.45)

    def add(self, kind, x, y):
        sp = self.sprite(kind)
        x, y = int(round(x)), int(round(y))
        self.items.append((kind, x, y))
        if kind not in ("hearth",):
            self.boxes.append((x - sp.ax, y - sp.ay, x - sp.ax + sp.w, y + 1))
        else:
            self.boxes.append((x - 5, y - 5, x + 5, y + 2))

    def sprite(self, kind):
        s = self.v.season
        if kind == "banner":
            return S.banner(self.civ)
        return S.structure(kind, s, self.civ)

    # ------------------------------------------------------------------------------------------- footprint
    def membership(self, side=0.8):
        """Smooth 0..1 field over the window: 1 on the settlement tile, `side` on its building tiles."""
        f, v = self.f, self.v
        grid = np.zeros((f.H, f.W), np.float32)
        for t in self.free:
            grid.ravel()[t] = side
        grid.ravel()[self.tile] = 1.0
        # restrict computation to a local box for speed
        r = int(v.tw * 2.6)
        x0, x1 = max(0, int(self.cx) - r), min(v.w, int(self.cx) + r)
        y0, y1 = max(0, int(self.cy) - r), min(v.h, int(self.cy) + r)
        out = np.zeros((v.h, v.w), np.float32)
        if x0 < x1 and y0 < y1:
            fx, fr = v.fx[y0:y1, x0:x1], v.fr[y0:y1, x0:x1]
            wx = (fbm(fx * 1.5, fr * 1.5, 71) - 0.5) * 0.5
            wy = (fbm(fx * 1.5, fr * 1.5, 73) - 0.5) * 0.5
            out[y0:y1, x0:x1] = smooth_sample(grid, fx + wx, fr + wy)
        return out

    def free_spot(self, k, taken):
        """A deterministic open spot inside the village for home-bound person k."""
        v = self.v
        for attempt in range(24):
            h1 = hash2(k, attempt, 901 + self.si)
            h2 = hash2(k, attempt, 902 + self.si)
            h3 = hash2(k, attempt, 903 + self.si)
            if not hasattr(self, "outer"):
                f = self.f
                ring2 = set(n2 for n1 in self.free for n2 in f.neighbours(n1))
                self.outer = [t for t in sorted(ring2) if t not in self.tiles and t not in f.plots
                              and not f.water.ravel()[t] and f.biome.ravel()[t] not in (MOUNTAIN, PEAK)]
            if h1 < 0.3:
                t = self.tiles[0]
            elif h1 < 0.75 or not self.outer:
                t = self.tiles[int(h1 * 997) % len(self.tiles)]
            else:
                t = self.outer[int(h1 * 991) % len(self.outer)]
            tx, ty = v.tile_px(t)
            x = tx + (h2 - 0.5) * v.tw * 0.85
            y = ty + (h3 - 0.5) * v.rh * 0.8 + 2
            xi, yi = int(x), int(y)
            if any(b[0] - 3 <= xi <= b[2] + 2 and b[1] + 2 <= yi <= b[3] + 3 for b in self.boxes):
                continue
            if 0 <= xi < v.w and 0 <= yi < v.h and (v.water[yi, xi] or v.river[yi, xi]):
                continue
            if any(abs(xi - a) < 6 and abs(yi - b) < 5 for (a, b) in taken):
                continue
            taken.append((xi, yi))
            return xi, yi
        return None


def thorn_hedge(v, vil, memb, objs, season):
    """Thorn bushes along the outline of the village footprint, leaving gates toward the fields."""
    m = memb > 0.42
    edge = m & ~(np.roll(m, 1, 0) & np.roll(m, -1, 0) & np.roll(m, 1, 1) & np.roll(m, -1, 1))
    ys, xs = np.nonzero(edge)
    gates = []
    for t in vil.f.neighbours(vil.tile):
        if t in vil.f.plots:
            tx, ty = v.tile_px(t)
            gates.append(math.atan2(ty - vil.cy, tx - vil.cx))
    gates = gates[:2] or [math.pi * 0.35]
    placed = []
    order = np.argsort(hash2(xs, ys, 55))
    spr = S.structure("thorn", season)
    for i in order:
        x, y = xs[i], ys[i]
        a = math.atan2(y - vil.cy, x - vil.cx)
        if any(abs(((a - g + math.pi) % (2 * math.pi)) - math.pi) < 0.22 for g in gates):
            continue
        if any((x - px) ** 2 + ((y - py) * 1.4) ** 2 < 20 for (px, py) in placed):
            continue
        placed.append((x, y))
        objs.append((int(y) + 1, int(x), spr, 1))
