"""Four-civilization territory model (WS-A part 2).

A deterministic spatial model on a hex map. Each civilization follows a
population and settlement trajectory from balance_sim.py (tuned scenario,
optionally a different archetype per civ). The model places fission sites,
radiates claims per 05 §11, resolves ownership with threshold and lead
margin, and measures Dominion share and contact timing season by season.

It answers 06 §6 item 5 (first contact in E1-E2), whether Dominion >= 50% is
reachable, and how full the map is early (07 F6). See 07b-territory-findings.md.

Usage:
    python territory_sim.py                    # standard map, 4x Balanced, summary
    python territory_sim.py sweep              # every scenario used in 07b
    python territory_sim.py mapsize            # map size per player count

Everything below marked ASSUMED is a modeling choice the docs do not specify
(WS-F and WS-G replace them).
"""
import math
import os
import sys
from collections import deque
from functools import lru_cache
from pathlib import Path

import numpy as np

# CIVSIM_BALANCE_DIR points at another copy of balance_sim.py/params.py (e.g. a frozen
# WS-A part-1 snapshot while WS-H edits the live one); default is this folder.
sys.path.insert(0, os.environ.get("CIVSIM_BALANCE_DIR") or str(Path(__file__).resolve().parent))
import balance_sim as B  # noqa: E402
import params as P  # noqa: E402

# ---- constants ---------------------------------------------------------------------
# name -> (value, note). "v0.1" = as written in 05 §11; others ASSUMED unless noted.
TP = {
    "MapW": (160, "05 §4.1 standard map, hex columns"),
    "MapH": (100, "05 §4.1 standard map, hex rows"),
    "LandFrac": (0.40, "ASSUMED: Continents world type, share of tiles that are land"),
    "MapSeed": (1, "ASSUMED: worldgen seed (deterministic value noise)"),
    "T": (1.0, "05 §11.2 ownership threshold"),
    "Lead": (0.20, "05 §11.2 lead over runner-up"),
    "FlipSeasons": (1, "05 §11.2: challenger must lead a full season"),
    "IncumbentHolds": (0, "0 = as written (owner re-qualifies every season); 1 = owner keeps while highest and >= T"),
    "FalloffTail": (1.0, "ASSUMED: Falloff = max(0, 1 - d/(r + tail)); claim reaches 0 at r + tail"),
    "SightTiles": (3, "ASSUMED: a figure sees 3 tiles (Aware)"),
    "MeetTiles": (2, "05 §12.1: Contacted when figures are within 2 tiles"),
    "Presence": (0.0, "ASSUMED: presence claim at a settlement's edge (0 = off); 05 §11.1 Presence term"),
    "PresenceRange": (0.5, "ASSUMED: home range as a share of Reach"),
    "SiteSigma": (1.0, "ASSUMED: site distance-fit width, × claim radius"),
    "OverseasEra": (4, "03c TER-09 Frontier Colonies (E4): fission can cross water"),
    # 07b §9.3 L2, adopted 2026-10-18: WAY-02 seasonal camps are claim sources (05 §11.1)
    "Camps": (0, "1 = seasonal camps claim land (WAY-02); 0 = off (reproduces 07b §6 and §9.2)"),
    "CampEmit": (0.5, "camp emission as a share of its band's settlement emission (decision)"),
    "CampRadius": (2, "camp claim radius (decision)"),
    "CampsPerSettlement": (2, "camps in each settlement's seasonal round besides home (03c WAY-02: 2-4 sites)"),
    "CampLastEra": (3, "settlements founded after E3 get no camps; earlier camps stay as satellite camps"),
}

# Per-era tables (E0..E9). Claim radius comes from params.ERA["ClaimRadius"].
TERA = {
    # nominal claim radius per era (05 §9.5 tiers, as in params.ERA["ClaimRadius"]); kept
    # separate so early-radius variants do not change balance_sim's settlement cap
    "Radius": list(P.ERA["ClaimRadius"]["tuned"]),
    # how far a civ's figures routinely range from their nearest settlement (hunting
    # forays, scouts, seasonal round, traders). ASSUMED; WS-E/WS-F replace.
    "Reach": [6, 9, 12, 15, 20, 25, 35, 50, 80, 160],
    # civ-wide claim multiplier from TER/DEF projects on or near the closures:
    # TER-03 markers +30% (E1, half-taken), TER-05 new villages (E2), TER-12 +20% (E7). ASSUMED.
    "ClaimMult": [1.0, 1.15, 1.3, 1.3, 1.3, 1.3, 1.3, 1.56, 1.56, 1.56],
    # water the walking graph may cross: 0 = 1-tile straits only (SEA-01), 1 = coastal
    # water within 2 tiles of land (SEA-03/04, E2), 2 = open sea (SEA-06 sail + WAY-05, E4).
    "SeaLevel": [0, 0, 1, 1, 2, 2, 2, 2, 2, 2],
}

ICE, TUNDRA, DESERT, PEAK, MOUNTAIN, OPEN, OCEAN = range(7)
HAB_W = {ICE: 0.0, TUNDRA: 0.5, DESERT: 0.5, PEAK: 0.0, MOUNTAIN: 1.0, OPEN: 1.0, OCEAN: 0.0}


def tp(**kw):
    t = {k: v[0] for k, v in TP.items()}
    t.update({k: list(v) for k, v in TERA.items()})
    t.update(kw)
    return t


# ---- map ---------------------------------------------------------------------------
class HexMap:
    """Odd-r offset hex grid, no wrap. Tile index i = y * W + x."""

    def __init__(self, W, H, land_frac=0.40, seed=7, layout="continents", sym=False):
        self.W, self.H, self.N = W, H, W * H
        ys, xs = np.divmod(np.arange(self.N), W)
        self.x, self.y = xs, ys
        self.q = xs - (ys - (ys & 1)) // 2
        self.r = ys
        self.px = xs + 0.5 * (ys & 1)
        self.py = ys * 0.866
        rng = np.random.default_rng(seed)
        elev = self._noise(rng, [(6, 1.0), (12, 0.5), (24, 0.25)])
        moist = self._noise(rng, [(5, 1.0), (10, 0.5)])
        rough = self._noise(rng, [(10, 1.0), (20, 0.6)])
        if sym:  # point-symmetric (180° rotation = reversed tile order): a perfectly fair map
            elev, moist, rough = [(f + f[::-1]) / 2 for f in (elev, moist, rough)]
        self.sym = sym
        # Continents: two landmasses split by an ocean channel, oceans at the edges.
        u = self.px / W
        v = self.py / (H * 0.866)
        edge = np.minimum.reduce([u, 1 - u, v, 1 - v])
        base = 0.9 * np.clip(edge / 0.12, 0, 1)
        if layout == "continents":
            base = base - 0.55 * np.exp(-((u - 0.5) / 0.045) ** 2)
        if sym:  # px carries a half-tile row offset that a 180° rotation flips, so mirror this too
            base = (base + base[::-1]) / 2
        score = base + 0.8 * elev
        sea = np.quantile(score, 1 - land_frac)
        land = score > sea
        lat = np.abs(ys - (H - 1) / 2) / ((H - 1) / 2)
        biome = np.full(self.N, OPEN)
        biome[~land] = OCEAN
        landr = rough[land]
        pk, mt = np.quantile(landr, 0.96), np.quantile(landr, 0.88)
        biome[land & (rough >= mt)] = MOUNTAIN
        biome[land & (rough >= pk)] = PEAK
        biome[land & (lat > 0.78) & (biome == OPEN)] = TUNDRA
        biome[land & (lat > 0.92)] = ICE
        biome[land & (lat > 0.12) & (lat < 0.42) & (moist < np.quantile(moist, 0.30)) & (biome == OPEN)] = DESERT
        self.land = land
        self.biome = biome
        self.hab = np.array([HAB_W[b] for b in biome])
        self.total_hab = self.hab.sum()
        self._nbrs = [self._neighbors(i) for i in range(self.N)]
        self.coast_dist = self._bfs_multi(np.flatnonzero(land), lambda j: True, limit=3)
        self.mass = self._components()

    def _noise(self, rng, octaves):
        """Smooth value noise in [0, 1], bilinear between random lattice points."""
        out = np.zeros(self.N)
        for cells, amp in octaves:
            gw, gh = cells + 2, max(2, int(cells * self.H / self.W)) + 2
            g = rng.random((gh, gw))
            fx = self.px / self.W * (gw - 1.001)
            fy = self.py / (self.H * 0.866) * (gh - 1.001)
            x0, y0 = fx.astype(int), fy.astype(int)
            tx, ty = fx - x0, fy - y0
            tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
            a = g[y0, x0] * (1 - tx) + g[y0, x0 + 1] * tx
            b = g[y0 + 1, x0] * (1 - tx) + g[y0 + 1, x0 + 1] * tx
            out += amp * (a * (1 - ty) + b * ty)
        return (out - out.min()) / (out.max() - out.min())

    def idx(self, q, r):
        x = q + (r - (r & 1)) // 2
        return r * self.W + x if (0 <= r < self.H and 0 <= x < self.W) else -1

    def _neighbors(self, i):
        q, r = self.q[i], self.r[i]
        out = []
        for dq, dr in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, -1), (-1, 1)):
            j = self.idx(q + dq, r + dr)
            if j >= 0:
                out.append(j)
        return out

    def dist_from(self, i):
        dq = self.q - self.q[i]
        dr = self.r - self.r[i]
        return np.maximum(np.maximum(np.abs(dq), np.abs(dr)), np.abs(dq + dr))

    def _bfs_multi(self, sources, passable, limit=10 ** 9):
        d = np.full(self.N, 10 ** 6, dtype=np.int64)
        dq = deque()
        for s in sources:
            d[s] = 0
            dq.append(s)
        while dq:
            i = dq.popleft()
            if d[i] >= limit:
                continue
            for j in self._nbrs[i]:
                if d[j] > d[i] + 1 and passable(j):
                    d[j] = d[i] + 1
                    dq.append(j)
        return d

    def _components(self):
        comp = np.full(self.N, -1)
        c = 0
        for i in np.flatnonzero(self.land):
            if comp[i] >= 0:
                continue
            comp[i] = c
            dq = deque([i])
            while dq:
                k = dq.popleft()
                for j in self._nbrs[k]:
                    if self.land[j] and comp[j] < 0:
                        comp[j] = c
                        dq.append(j)
            c += 1
        return comp

    def walk_dist(self, sources, sea_level, limit):
        """Path distance over land plus whatever water the era can cross."""
        land, cd = self.land, self.coast_dist
        if sea_level == 0:
            ok = lambda j: land[j] or cd[j] <= 1  # noqa: E731  (narrow straits, rivers)
        elif sea_level == 1:
            ok = lambda j: land[j] or cd[j] <= 2  # noqa: E731
        else:
            ok = lambda j: True  # noqa: E731
        return self._bfs_multi(sources, ok, limit)


@lru_cache(maxsize=None)
def make_map(W, H, land_frac, seed, layout="continents", sym=False):
    return HexMap(W, H, land_frac, seed, layout, sym)


# ---- starts ------------------------------------------------------------------------
def place_starts(m, n, spacing=None, fair=0):
    """Start tiles. spacing=None: maximize the minimum distance between starts (the
    implicit v0.1 behavior). spacing=D: starts come in neighbour pairs, each pair ~D tiles
    apart on one landmass, pairs as far apart as possible. fair=k: search k first starts
    (and a few alternatives per pick) and keep the set whose room is most even."""
    good = np.flatnonzero((m.biome == OPEN) & (m.coast_dist == 0))
    qual = np.zeros(m.N)
    for i in good:
        qual[i] = m.hab[m.dist_from(i) <= 6].sum()
    cand = good[qual[good] >= 0.92 * np.percentile(qual[good], 80)]
    cand = cand[:: max(1, len(cand) // 600)]
    dists = {int(i): m.dist_from(i) for i in cand}
    order = sorted(cand, key=lambda i: (-qual[i], i))
    if m.sym and n % 2 == 0:
        # half the starts in the left half, the rest are their 180° images
        half = [int(i) for i in cand if m.x[i] < m.W // 2 - 4]
        best = None
        if n == 2:
            pool = [i for i in half if spacing is None or abs(m.dist_from(i)[m.N - 1 - i] - spacing) <= 2]
            if not pool:
                return None
            f = max(pool, key=lambda i: (m.dist_from(i)[m.N - 1 - i] if spacing is None else qual[i], -i))
            return [f, m.N - 1 - f]
        for f in [i for i in order if m.x[i] < m.W // 2 - 4][:: max(1, len(half) // 24)][:24]:
            for st in _starts_from(m, n // 2, spacing, half, dists, qual, f, 3):
                if not st:
                    continue
                full = st + [m.N - 1 - i for i in st]
                gap = min(m.dist_from(a)[b] for a in full for b in full if a != b)
                if spacing is not None and gap < 0.9 * spacing:
                    continue
                err = (-gap / 10 if spacing is None
                       else abs(m.dist_from(st[0])[st[1]] - spacing) / 10 if len(st) > 1 else 0)
                key = (round(_room_spread(m, full, fast=True) + 0.2 * err, 3), full)
                if best is None or key < best[0]:
                    best = (key, full)
        return best[1] if best else None
    if not fair:
        return _starts_from(m, n, spacing, cand, dists, qual, order[0], 1)[0]
    firsts = order[:: max(1, len(order) // fair)][:fair]
    sets = []
    for f in firsts:
        sets += _starts_from(m, n, spacing, cand, dists, qual, f, 3)
    sets = [s for s in sets if s]
    return min(sets, key=lambda st: (_room_spread(m, st, fast=True), st)) if sets else None


def room(m, starts, fast=False):
    """Habitable weight closest to each start: walking distance (land and straits),
    then hex distance for land no start can walk to. fast: hex distance within landmass."""
    if fast:
        hexd = np.stack([m.dist_from(s) + np.where(m.mass == m.mass[s], 0, 10 ** 4) for s in starts])
        owner = np.argmin(hexd, axis=0)
    else:
        walk = np.stack([m.walk_dist([s], 0, 10 ** 6) for s in starts])
        hexd = np.stack([m.dist_from(s) for s in starts])
        owner = np.argmin(np.where(walk < 10 ** 6, walk, 10 ** 4 + hexd), axis=0)
    return [float(m.hab[owner == c].sum()) for c in range(len(starts))]


def _room_spread(m, starts, fast=False):
    r = room(m, starts, fast)
    return (max(r) - min(r)) / (sum(r) / len(r))


def _starts_from(m, n, spacing, cand, dists, qual, first, branch):
    """Greedy start sets from `first`, branching over the best `branch` options per pick."""
    sets = [[int(first)]]
    while sets and len(sets[0]) < n:
        nxt = []
        for chosen in sets:
            near_pick = spacing is not None and len(chosen) % 2 == 1
            scored = []
            for i in cand:
                i = int(i)
                if i in chosen:
                    continue
                dmin = min(dists[c][i] for c in chosen)
                if not near_pick:
                    sc = dmin + 0.001 * qual[i]
                else:
                    if dmin < 0.9 * spacing or m.mass[i] != m.mass[chosen[-1]]:
                        continue
                    sc = -abs(dists[chosen[-1]][i] - spacing) + 0.001 * qual[i]
                scored.append((sc, i))
            scored.sort(reverse=True)
            if near_pick:
                scored = [x for x in scored if x[0] > -2.5] or scored[:1]
            nxt += [chosen + [i] for _, i in scored[:branch]]
        sets = nxt
    return sets or [None]


# ---- simulation --------------------------------------------------------------------
def civ_trajectory(arch="Balanced", overrides=None):
    s = P.scenario("tuned", "Standard", arch, overrides)
    rows, D = B.run(s)
    end = next((r["t"] for r in rows if r["era"] == 10), len(rows))
    return dict(arch=arch, rows=rows, end=end, season_s=s["SeasonSeconds"])


def disk(m, i, R, cache={}):
    key = (id(m), i, R)
    if key not in cache:
        dd = m.dist_from(i)
        js = np.flatnonzero(dd <= R)
        cache[key] = (js, dd[js])
    return cache[key]


class Civ:
    def __init__(self, cid, start, traj):
        self.id, self.traj = cid, traj
        self.setts = [start]
        self.blocked = 0
        self.camps = []                        # WAY-02 seasonal camps (claim sources)
        self.camp_map = {}                     # settlement tile -> its camp tiles (sticky once placed)
        self.lost_k, self.lost_frac = 0, 0.0   # settlements and population share captured from it
        self.gains = []                        # (loser civ, k, population share) captured by it


def run(m, starts, trajs, t=None, captures=(), max_seasons=None, snaps=()):
    """Season-by-season territory run. captures: (minute, winner, loser, k) events, a
    conquest stand-in that moves the loser's k newest settlements and their share of its
    population to the winner (k >= the loser's count eliminates it)."""
    t = t or tp()
    nc = len(starts)
    civs = [Civ(c, starts[c], trajs[c]) for c in range(nc)]
    owner = np.full(m.N, -1)
    pend_who = np.full(m.N, -2)
    pend_n = np.zeros(m.N, dtype=int)
    end = min(tr["end"] for tr in trajs) if max_seasons is None else max_seasons
    ss = trajs[0]["season_s"]
    contact = {}  # (a, b) -> {"aware": t, "contact": t, "border": t}
    walk_cache, near_cache = {}, {}
    out = []
    claim = np.zeros((nc, m.N))
    done_caps = set()
    camp_from = []  # step from which each civ's seasonal round claims (WAY-02 ~ mid-E1)
    for tr in trajs:
        e1 = next((i for i, r in enumerate(tr["rows"]) if r["era"] >= 1), None)
        e2 = next((i for i, r in enumerate(tr["rows"]) if r["era"] >= 2), None)
        camp_from.append((e1 + e2) // 2 if e1 is not None and e2 is not None else 10 ** 9)
    snap_out = {}
    hk = t.get("hooks")                 # E3 extension (07d §7); None = the 07b model unchanged
    for step in range(1, end + 1):
        minute = step * ss / 60
        if hk:
            hk.step(step, minute, civs, owner, claim, m)
        rows = [cv.traj["rows"][min(step, len(cv.traj["rows"]) - 1)] for cv in civs]
        eras = [min(9, r["era"]) for r in rows]
        for ci, ev in enumerate(captures):
            if ci not in done_caps and minute >= ev[0]:
                done_caps.add(ci)
                _, w, l, k = ev
                lv = civs[l]
                k = min(k, len(lv.setts))
                frac = (1 - lv.lost_frac) * (k / len(lv.setts)) if lv.setts else 0
                moved = lv.setts[len(lv.setts) - k:]
                lv.setts = lv.setts[:len(lv.setts) - k]
                lv.lost_k += k
                lv.lost_frac += frac
                civs[w].setts += moved
                civs[w].gains.append((l, k, frac))
        pops = [rows[c]["pop"] * (1 - cv.lost_frac) + sum(rows[l]["pop"] * f for l, _, f in cv.gains)
                for c, cv in enumerate(civs)]
        # --- settlements follow the trajectory (fission adds, merges remove the newest).
        # Every civ decides from last season's state, then clashing picks are resolved by
        # score, so no civ gets a first-mover edge from the loop order.
        snap = [list(cv.setts) for cv in civs]
        props = []
        for c, cv in enumerate(civs):
            if cv.lost_frac >= 0.999 or (hk and hk.skip(c)):
                continue  # eliminated, or a neutral with a fixed site
            target = max(1, rows[c]["S"] - cv.lost_k) + sum(k for _, k, _ in cv.gains) + (hk.target_add(c) if hk else 0)
            while len(cv.setts) > target:
                # a merge absorbs the settlement whose own claim area overlaps its
                # neighbours most (an interior town folds into a city); the start stays
                r = t["Radius"][eras[c]]
                cover = np.zeros(m.N, dtype=int)
                for x in cv.setts:
                    cover[disk(m, x, r)[0]] += 1
                uniq = [(int((cover[disk(m, x, r)[0]] == 1).sum()), -k) for k, x in enumerate(cv.setts)]
                k = -min(uniq[1:])[1]
                cv.setts.pop(k)
            own = list(cv.setts)
            if hk:
                for site in hk.decree_sites(c, cv, own, [snap[o] for o in range(nc) if o != c and not hk.skip(o)], owner,
                                            claim, eras[c], minute):
                    own.append(site)
                    props.append((1e6, c, site))
            for _ in range(min(3, target - len(own))):
                if hk and hasattr(hk, "pick"):
                    got = hk.pick(m, c, own, [snap[o] for o in range(nc) if o != c], starts, owner, claim, eras[c], t)
                else:
                    got = pick_site(m, c, own, [snap[o] for o in range(nc) if o != c], starts, owner, claim, eras[c], t)
                if got is None:
                    cv.blocked += 1
                    break
                own.append(got[0])
                props.append((got[1], c, got[0]))
        accepted = []
        for sc, c, site in sorted(props, key=lambda p: (-p[0], p[1])):
            rr = 2 * t["Radius"][eras[c]] + 1
            if any(o != c and m.dist_from(site)[x] < rr for _, o, x in accepted):
                civs[c].blocked += 1
                continue
            accepted.append((sc, c, site))
            civs[c].setts.append(site)
        # --- claim field ---
        claim[:] = 0
        for c, cv in enumerate(civs):
            if not cv.setts:
                continue
            e = eras[c]
            r = t["Radius"][e]
            per = pops[c] / len(cv.setts)  # mean-field, as in balance_sim
            emit = math.sqrt(per) * 1.0 * t["ClaimMult"][e]  # × CohesionFactor (1.0)
            for k_i, s_i in enumerate(cv.setts):
                rr, em = (r, emit) if not hk else hk.settlement_claim(c, k_i, cv, e, pops[c], r, emit, minute)
                js, dd = disk(m, s_i, rr + int(math.ceil(t["FalloffTail"])))
                claim[c, js] += em * np.maximum(0.0, 1 - dd / (rr + t["FalloffTail"]))
            if hk:
                for x, rr, em in hk.extra(c, cv, emit, owner, minute, m):
                    js, dd = disk(m, x, rr + int(math.ceil(t["FalloffTail"])))
                    claim[c, js] += em * np.maximum(0.0, 1 - dd / (rr + t["FalloffTail"]))
            if t["Camps"] and step >= camp_from[c]:
                # camps are placed once per settlement founded up to CampLastEra and then stay
                # (satellite fishing, herding and hunting camps); they go with their settlement
                cv.camp_map = {k: v for k, v in cv.camp_map.items() if k in cv.setts}
                if e <= t["CampLastEra"]:
                    for s_i in cv.setts:
                        if s_i not in cv.camp_map:
                            others = [x for o in range(nc) if o != c for x in civs[o].setts]
                            placed = [x for v in cv.camp_map.values() for x in v]
                            cv.camp_map[s_i] = place_camps(m, c, cv.setts, others, owner, e, t,
                                                           only=s_i, existing=placed)
                cv.camps = [x for v in cv.camp_map.values() for x in v if owner[x] in (-1, c)]
                cr = t["CampRadius"]
                for x in cv.camps:
                    js, dd = disk(m, x, cr + int(math.ceil(t["FalloffTail"])))
                    claim[c, js] += t["CampEmit"] * emit * np.maximum(0.0, 1 - dd / (cr + t["FalloffTail"]))
            else:
                cv.camps = []
            if t["Presence"] > 0:
                # 05 §11.1 Presence: routine work and travel inside the home range
                hr = t["PresenceRange"] * t["Reach"][e]
                key = (tuple(cv.setts),)
                if key not in near_cache:
                    near_cache[key] = np.min([m.dist_from(x) for x in cv.setts], axis=0)
                claim[c] += t["Presence"] * np.maximum(0.0, 1 - near_cache[key] / (hr + 1))
        # --- ownership with threshold, lead and hysteresis ---
        if nc > 1:
            srt = np.sort(claim, axis=0)
            top, second = srt[-1], srt[-2]
        else:
            top, second = claim[0], np.zeros(m.N)
        lead_civ = np.argmax(claim, axis=0)
        qualifies = (top >= t["T"]) & (top >= (1 + t["Lead"]) * second)
        want = np.where(qualifies, lead_civ, -1)
        if t["IncumbentHolds"]:
            inc = owner >= 0
            own_claim = claim[np.clip(owner, 0, nc - 1), np.arange(m.N)]
            holds = inc & (lead_civ == owner) & (own_claim >= t["T"])
            want = np.where(holds, owner, want)
        want = np.where(m.land, want, -1)
        old = owner
        change = want != owner
        same_pend = change & (pend_who == want)
        pend_n = np.where(same_pend, pend_n + 1, np.where(change, 1, 0))
        pend_who = np.where(change, want, -2)
        flip = change & (pend_n > t["FlipSeasons"])
        # first claims on empty land take effect after the same delay
        owner = np.where(flip, want, owner)
        pend_n = np.where(flip, 0, pend_n)
        pend_who = np.where(flip, -2, pend_who)
        contested = (owner == -1) & (top >= t["T"]) & (m.hab > 0)
        civ_flips = int((flip & (old >= 0) & (want >= 0)).sum())
        # --- contact ---
        for a in range(nc):
            for b in range(a + 1, nc):
                if hk and (hk.skip(a) or hk.skip(b)):
                    continue
                rec = contact.setdefault((a, b), {})
                if ("contact" in rec and "border" in rec) or not civs[a].setts or not civs[b].setts:
                    continue
                sl = max(TERA["SeaLevel"][eras[a]], TERA["SeaLevel"][eras[b]])
                ra, rb = t["Reach"][eras[a]], t["Reach"][eras[b]]
                key = (tuple(civs[a].setts), sl, min(400, ra + rb + 6))
                if key not in walk_cache:
                    walk_cache[key] = m.walk_dist(civs[a].setts, sl, key[2])
                wd = walk_cache[key]
                dab = min(wd[s] for s in civs[b].setts)
                if "aware" not in rec and dab <= ra + rb + t["SightTiles"]:
                    rec["aware"] = minute
                if "contact" not in rec and dab <= ra + rb + t["MeetTiles"]:
                    rec["contact"] = minute
                    rec["contact_dist"] = int(dab)
                if "border" not in rec:
                    oa = owner == a
                    if oa.any():
                        ob = owner == b
                        nb = np.zeros(m.N, dtype=bool)
                        for j in np.flatnonzero(ob):
                            for k in m._nbrs[j]:
                                nb[k] = True
                        if (nb & oa).any():
                            rec["border"] = minute
        if step in snaps:
            snap_out[step] = (owner.copy(), [list(cv.setts) for cv in civs], eras)
        hab_owned = [float(m.hab[owner == c].sum()) for c in range(nc)]
        out.append(dict(step=step, minute=minute, eras=eras, pops=pops,
                        S=[len(cv.setts) for cv in civs], S_target=[r["S"] for r in rows],
                        camps=[len(getattr(cv, "camps", [])) for cv in civs],
                        owned=[int((owner == c).sum()) for c in range(nc)],
                        share=[h / m.total_hab for h in hab_owned],
                        claimed=float(m.hab[owner >= 0].sum() / m.total_hab),
                        contested=float(m.hab[contested].sum() / m.total_hab),
                        flips=civ_flips))
    return dict(rows=out, contact=contact, civs=civs, owner=owner, starts=starts, map=m, claim=claim,
                snaps=snap_out)


def place_camps(m, cid, setts, rival_setts, owner, era, t, only=None, existing=()):
    """WAY-02 seasonal round: up to CampsPerSettlement camps per settlement, inside the home range
    (r + 1 to r + 3 tiles from the settlement, about half of E1-E2 reach), on unclaimed or own land on the same
    landmass, greedily maximizing new habitable land within the camp radius. Deterministic."""
    r = t["Radius"][era]
    cr = t["CampRadius"]
    hi = r + 3
    rival_near = np.min([m.dist_from(x) for x in rival_setts], axis=0) if rival_setts else np.full(m.N, 10 ** 6)
    covered = np.zeros(m.N, dtype=bool)
    for x in setts:
        covered[disk(m, x, r)[0]] = True
    for x in existing:
        covered[disk(m, x, cr)[0]] = True
    camps = []
    for s_i in ([only] if only is not None else setts):
        d = m.dist_from(s_i)
        ok = ((m.hab > 0) & (m.biome != MOUNTAIN) & (d >= r + 1) & (d <= hi) & (m.mass == m.mass[s_i])
              & ((owner == -1) | (owner == cid)) & (rival_near >= r + 3))
        for _ in range(t["CampsPerSettlement"]):
            best, bkey = None, None
            for i in np.flatnonzero(ok):
                js, _ = disk(m, i, cr)
                gain = float((m.hab[js] * (~covered[js]) * ((owner[js] == -1) | (owner[js] == cid))).sum())
                key = (round(gain, 9), -int(m.N - 1 - i if setts[0] >= m.N / 2 else i))
                if gain > 0 and (bkey is None or key > bkey):
                    best, bkey = int(i), key
            if best is None:
                break
            camps.append(best)
            covered[disk(m, best, cr)[0]] = True
    return camps


def pick_site(m, cid, own, rival_setts, starts, owner, claim, era, t):
    """Civ Mind site choice (05 §9.4): new land gained × distance fit, never on rival land.
    Returns (tile, score) or None. t["SiteRadius"] (E3 live calibration, 07d §7) spaces sites by a smaller radius
    than the claim radius, as the live planner does (≥ 2 × range radius + 1 = 7 tiles from any settlement)."""
    r = (t.get("SiteRadius") or t["Radius"])[era]
    dstar = 2 * r + 1
    sigma = max(1.0, t["SiteSigma"] * r)
    near = np.min([m.dist_from(x) for x in own], axis=0)
    rs = [x for lst in rival_setts for x in lst]
    rival_near = np.min([m.dist_from(x) for x in rs], axis=0) if rs else np.full(m.N, 10 ** 6)
    ok = (m.hab > 0) & (m.biome != MOUNTAIN) & (near >= r + 1) & (near <= dstar + 3 * r) & (rival_near >= 2 * r + 1)
    ok &= (owner == -1) | (owner == cid)
    if era < t["OverseasEra"]:
        ok &= np.isin(m.mass, list({m.mass[x] for x in own}))
    cand = np.flatnonzero(ok)
    if len(cand) == 0:
        return None
    free = (m.hab * ((owner == -1) & (claim.max(axis=0) < t["T"]))).astype(float)
    home = m.dist_from(starts[cid])
    full = 3 * r * (r + 1) + 1
    best, bkey = None, None
    for i in cand:
        js, _ = disk(m, i, r)
        gain = free[js].sum() / full
        fit = math.exp(-max(0, near[i] - dstar) ** 2 / (2 * sigma ** 2))
        infill = 0.15 * min(1.0, near[i] / dstar)  # towns between cities once land runs out
        sc = (gain + infill) * fit * m.hab[i]
        # ties: closer to home, then away from rivals, then a mirror-invariant tile order (civs on the
        # rotated half count tiles from the other end) so mirrored civs on symmetric maps act alike
        flip = starts[cid] >= m.N / 2
        key = (round(sc, 9), -home[i], rival_near[i], -(m.N - 1 - i if flip else i))
        if bkey is None or key > bkey:
            best, bkey = i, key
    return (int(best), bkey[0]) if bkey[0] > 0.05 else None


# ---- reporting ---------------------------------------------------------------------
def era_end_rows(res, civ=0):
    """Row at the end of each era for one civ."""
    rows = res["rows"]
    out = {}
    for r in rows:
        out[r["eras"][civ]] = r
    return out


def first_contact(res):
    c = [v.get("contact") for v in res["contact"].values() if "contact" in v]
    return min(c) if c else None


def contact_era(res, minute, civ=0):
    for r in res["rows"]:
        if r["minute"] >= minute:
            return r["eras"][civ]
    return None


def crossing(res, level):
    for r in res["rows"]:
        for c, sh in enumerate(r["share"]):
            if sh >= level:
                return r["minute"], c
    return None, None


def dominion_win(res, level=0.5, years=5):
    """First minute a civ has held >= level for `years` consecutive years (4 seasons/yr)."""
    need = years * 4
    nc = len(res["rows"][0]["share"])
    run_ = [0] * nc
    for r in res["rows"]:
        for c in range(nc):
            run_[c] = run_[c] + 1 if r["share"][c] >= level else 0
            if run_[c] >= need:
                return r["minute"], c
    return None, None


def legacy_dominion(res):
    """Dominion Legacy per civ (100 × share per year) and the share earned by era."""
    nc = len(res["rows"][0]["share"])
    tot = [0.0] * nc
    by_era = [[0.0] * 10 for _ in range(nc)]
    for r in res["rows"]:
        for c in range(nc):
            v = 100 * r["share"][c] / 4
            tot[c] += v
            by_era[c][r["eras"][c]] += v
    return tot, by_era


def scenario(name, W=160, H=100, n=4, archs=None, spacing=None, land=None, seed=None, t=None,
             captures=(), layout="continents", overrides=None, fair=0, sym=True, snaps=()):
    t = t or tp()
    m = make_map(W, H, land if land is not None else t["LandFrac"], seed if seed is not None else t["MapSeed"],
                 layout, sym)
    starts = place_starts(m, n, spacing, fair)
    archs = archs or ["Balanced"] * n
    trajs = [civ_trajectory(a, overrides) for a in archs]
    res = run(m, starts, trajs, t, captures, snaps=snaps)
    res["name"] = name
    res["archs"] = archs
    res["W"], res["H"] = W, H
    return res


def summary(res, out=print):
    m = res["map"]
    rows = res["rows"]
    st = res["starts"]
    nc = len(st)
    dmin = min(int(m.dist_from(st[a])[st[b]]) for a in range(nc) for b in range(a + 1, nc))
    same = sum(m.mass[st[a]] == m.mass[st[b]] for a in range(nc) for b in range(a + 1, nc))
    out(f"== {res['name']}: {res['W']}x{res['H']}, land {int(m.land.sum())} tiles, habitable weight "
        f"{m.total_hab:.0f} ({m.total_hab / nc:.0f}/civ); nearest starts {dmin} tiles; {same} same-landmass pairs")
    fc = first_contact(res)
    if fc:
        out(f"   first contact minute {fc:.1f} (E{contact_era(res, fc)})")
    else:
        out("   no contact")
    for (a, b), v in sorted(res["contact"].items()):
        out(f"   pair {a}-{b}: aware {v.get('aware', '-')}  contact {v.get('contact', '-')}  border {v.get('border', '-')}")
    ee = era_end_rows(res)
    out("   era  min   pop/civ  S  owned/civ  share%(each)            claimed%  contested%")
    for e in sorted(ee):
        r = ee[e]
        out(f"   E{e} {r['minute']:6.1f} {r['pops'][0]:8.0f} {r['S'][0]:3d} {r['owned'][0]:6d}   "
            + " ".join(f"{100 * x:5.1f}" for x in r["share"]) + f"   {100 * r['claimed']:6.1f}  {100 * r['contested']:6.2f}")
    w40 = crossing(res, 0.40)
    w50 = dominion_win(res)
    out(f"   40% warning: {w40}   Dominion win (50% × 5 yr): {w50}   blocked fissions: {[c.blocked for c in res['civs']]}")


# =====================================================================================
# E3 extension (docs/MVP/22 §5.1, §5.2, §6.4, §8.1, §11; §15.2 item 5; findings in 07d §7)
#
# Live trajectories (tools/live_data.py: population and settlement count per civ per minute, 8 seeds), the
# 07b recommended rules (paired starts 25 apart, T 0.7, radii 3·3·4·4, seasonal camps), and from each
# civilization's own KEY-3: the Town tier on the capital (radius 5, from TRD-04 at E3 0.30) and City
# (radius 6 at pop >= 120 with SOC-09), the capital's +25% (GOV-05), mining camps on deposits (radius 1 at
# 0.75 × √6), Found decrees toward unclaimed tin, and Tin Folk villages on far placers at the world's first KEY-3.
#
#   python territory_sim.py e3            # T11, T14–T16, T20 over 8 live seeds, both housing regimes
#   python territory_sim.py e3 --check    # assertions
# =====================================================================================
E3T = dict(E3Minutes=13.0, TownAt=0.30, CityAt=0.60, CityPop=120, PalaceAt=0.75, CapShare={"village": None, "towns": 0.41},
           MineEmit=0.75 * math.sqrt(6), MineRadius=1, MineReach=8, CartReach=12, CartAt=0.40, CopperAt=0.20,
           TinAt=0.40, ArsenicAt=0.50, FoundAt=(0.25, 0.60), SettleReach=14, TinFolkPop=24, TinFolkRadius=3,
           TinFolkGap=8, CompactReach=30)


def live_trajs(seed, regime="village"):
    """Per-civ trajectories (one row per season) from the live CSV; 'towns' scales the settlement count from KEY-3 by
    balance_sim live mode's towns ÷ village ratio (07d §3)."""
    import live_data as L
    rows = [r for r in L.load() if r["seed"] == seed]
    ratio = None
    if regime == "towns":
        rv, _, _ = B.run_live(dict(Regime="village", EraBase=[22, 50, 75, 140]), 70)
        rt, _, _ = B.run_live(dict(Regime="towns", EraBase=[22, 50, 75, 140]), 70)
        ratio = [(a["minute"], b["S"] / max(1, a["S"])) for a, b in zip(rv, rt)]
    out = []
    for c in range(4):
        rc = {r["minute"]: r for r in rows if r["civ"] == c}
        last = rc[max(rc)]
        ks = [last["k1"], last["k2"], last["k3"]]
        seq = [dict(pop=12.0, S=1, era=0)]
        for step in range(1, 4 * 64 + 1):
            mi = min(64, max(1, math.ceil(step / 4)))
            r = rc[mi]
            minute = step / 4
            era = sum(1 for k in ks if k is not None and k <= minute)
            S = r["S"]
            if ratio and ks[2] is not None and minute >= ks[2]:
                f = min(ratio, key=lambda x: abs(x[0] - minute))[1]
                S = max(1, int(round(S * f)))
            seq.append(dict(pop=float(r["pop"]), S=S, era=era))
        out.append(dict(arch="Balanced", rows=seq, end=len(seq) - 1, season_s=15, k3=ks[2]))
    return out


def neutral_traj(pop=24.0, seasons=400):
    rows = [dict(pop=pop * 1.0075 ** k, S=1, era=1) for k in range(seasons)]
    return dict(arch="TinFolk", rows=rows, end=seasons - 1, season_s=15, k3=None)


def place_deposits(m, starts):
    """22 §5.1 on the point-symmetric map: per pair a middle tin field (2 tiles, 12–16 from both starts, distances
    within 2), a far placer on each start's outer flank (22–32 tiles), arsenical copper (mountain, 15–25 tiles,
    depth 2) and depth-1 copper (8–15 tiles) per start. The second pair gets the mirror images."""
    land = m.land & (m.biome != PEAK) & (m.hab > 0)
    a, b = starts[0], starts[1]
    da, db = m.walk_dist([a], 0, 60), m.walk_dist([b], 0, 60)
    idx = np.arange(m.N)
    ok = land & (da >= 12) & (da <= 16) & (db >= 12) & (db <= 16) & (np.abs(da - db) <= 2)
    cand = np.flatnonzero(ok)
    if len(cand) == 0:   # injected (22 §5.1 "injected if needed"): best compromise
        cand = np.flatnonzero(land & (da < 10 ** 5) & (db < 10 ** 5))
        cand = cand[np.argsort(np.abs(da[cand] - db[cand]) * 10 + np.abs(da[cand] + db[cand] - 28), kind="stable")][:1]
    mid = int(min(cand, key=lambda i: (abs(int(da[i]) - int(db[i])), int(da[i] + db[i]), i)))
    lode = next((j for j in m._nbrs[mid] if land[j]), mid)
    dep = {"tin_mid": [mid, lode]}
    far, ars, cu = [], [], []
    for s_, d_own, d_oth in ((a, da, db), (b, db, da)):
        okf = land & (d_own >= 22) & (d_own <= 32) & (d_oth - d_own >= 6)
        cf = np.flatnonzero(okf)
        if len(cf) == 0:
            cf = np.flatnonzero(land & (d_own >= 18) & (d_own < 10 ** 5))
        far.append(int(min(cf, key=lambda i: (-(int(d_oth[i]) - int(d_own[i])), abs(int(d_own[i]) - 27), i))))
        oka = (m.biome == MOUNTAIN) & (d_own >= 15) & (d_own <= 25)
        ca = np.flatnonzero(oka) if oka.any() else np.flatnonzero(land & (d_own >= 15) & (d_own <= 25))
        ars.append(int(min(ca, key=lambda i: (abs(int(d_own[i]) - 20), i))))
        okc = land & (d_own >= 8) & (d_own <= 15) & ((m.biome == MOUNTAIN) | (m.biome == OPEN))
        cc = np.flatnonzero(okc & (m.biome == MOUNTAIN)) if (okc & (m.biome == MOUNTAIN)).any() else np.flatnonzero(okc)
        cu.append(int(min(cc, key=lambda i: (abs(int(d_own[i]) - 12), i))))
    mir = lambda i: m.N - 1 - i  # noqa: E731
    dep["tin_mid"] += [mir(x) for x in dep["tin_mid"]]
    dep["tin_far"] = far + [mir(x) for x in far]
    dep["arsenic"] = ars + [mir(x) for x in ars]
    dep["copper"] = cu + [mir(x) for x in cu]
    dist = {k: [[int(m.walk_dist([s_], 0, 60)[x]) for x in v] for s_ in starts] for k, v in dep.items()}
    return dep, dist


class E3Hooks:
    def __init__(self, m, starts, trajs, nreal, regime, dep, cfg=None, found=True):
        self.m, self.starts, self.nreal, self.regime, self.dep = m, starts, nreal, regime, dep
        self.c = dict(E3T, **(cfg or {}))
        self.k3 = [tr["k3"] for tr in trajs[:nreal]]
        self.founds = [0] * nreal
        self.found_done = [set() for _ in range(nreal)]
        self.mines = [[] for _ in range(nreal)]
        self.tin_at = [None] * nreal          # first minute with own tin (mine)
        self.compact_at = [None] * nreal      # first minute a Tin Folk compact is in reach
        self.arsenic_at = [None] * nreal
        self.tinfolk = []                     # (neutral civ index, tile)
        self.spawned = False
        self.found = found
        self.walk = {}
        self.neutral_slots = list(range(nreal, len(trajs)))
        self.live_sites = (cfg or {}).get("LiveSites", True)
        self.turn = [0] * len(trajs)
        self.food = None

    def x(self, c, minute):
        k3 = self.k3[c]
        return None if k3 is None or minute < k3 else (minute - k3) / self.c["E3Minutes"]

    def skip(self, c):
        return c >= self.nreal

    def target_add(self, c):
        return self.founds[c] if c < self.nreal else 0

    def _walk(self, setts, limit):
        key = (tuple(setts), limit)
        if key not in self.walk:
            if len(self.walk) > 400:
                self.walk.clear()
            self.walk[key] = self.m.walk_dist(setts, 0, limit)
        return self.walk[key]

    def step(self, step, minute, civs, owner, claim, m):
        if step == 1:
            for k in self.neutral_slots:
                civs[k].setts = []
        first_k3 = min(k for k in self.k3 if k is not None)
        if not self.spawned and minute >= first_k3:
            self.spawned = True
            allsett = [x for cv in civs for x in cv.setts]
            near = np.min([m.dist_from(x) for x in allsett], axis=0) if allsett else np.full(m.N, 10 ** 6)
            slots = iter(self.neutral_slots)
            for tile in self.dep["tin_far"]:
                if owner[tile] < 0 and near[tile] >= self.c["TinFolkGap"]:
                    k = next(slots, None)
                    if k is None:
                        break
                    civs[k].setts = [tile]
                    self.tinfolk.append((k, tile))
        for c in range(self.nreal):
            xc = self.x(c, minute)
            cv = civs[c]
            if xc is None or not cv.setts:
                continue
            reach = self.c["CartReach"] if xc >= self.c["CartAt"] else self.c["MineReach"]
            wd = self._walk(cv.setts, self.c["CompactReach"] + 2)
            taken = {t_ for o in range(self.nreal) for t_ in self.mines[o]}
            # the planner (and the Open-a-mine decree) opens camps on deposits in reach, not rival-owned
            kinds = [("copper", self.c["CopperAt"]), ("tin_mid", self.c["TinAt"]), ("tin_far", self.c["TinAt"])]
            if self.tin_at[c] is None and self.compact_at[c] is None:
                kinds.append(("arsenic", self.c["ArsenicAt"]))   # the arsenical route when tin is missing (MIN-04)
            for kind, at in kinds:
                if xc < at:
                    continue
                for tile in self.dep[kind]:
                    if tile in taken or owner[tile] not in (-1, c) or wd[tile] > reach:
                        continue
                    if any(tile == t_ for k_, t_ in self.tinfolk):
                        continue
                    self.mines[c].append(tile)
                    taken.add(tile)
                    if kind.startswith("tin") and self.tin_at[c] is None:
                        self.tin_at[c] = minute
                    if kind == "arsenic" and self.arsenic_at[c] is None:
                        self.arsenic_at[c] = minute
                    break
            # tin already mined by c keeps counting while it holds the tile
            if self.compact_at[c] is None and xc >= 0.1:
                for k_, tile in self.tinfolk:
                    if wd[tile] <= self.c["CompactReach"]:
                        self.compact_at[c] = minute
                        break
        # mines are lost when a rival owns the tile
        for c in range(self.nreal):
            self.mines[c] = [t_ for t_ in self.mines[c] if owner[t_] in (-1, c)]

    def settlement_claim(self, c, k, cv, era, pop, r, emit, minute):
        if c >= self.nreal:
            return self.c["TinFolkRadius"], emit
        xc = self.x(c, minute)
        if xc is None:
            return r, emit
        share = self.c["CapShare"][self.regime]
        S = len(cv.setts)
        rr = r
        if share is not None and S > 1:
            emit = math.sqrt((share if k == 0 else (1 - share) / (S - 1)) * pop) * emit / math.sqrt(pop / S)
        if k == 0 and self.regime == "towns" and xc >= self.c["TownAt"]:
            rr = 5
            if xc >= self.c["CityAt"] and (share or 0) * pop >= self.c["CityPop"]:
                rr = 6
            if xc >= self.c["PalaceAt"]:
                emit *= 1.25
        return rr, emit

    def extra(self, c, cv, emit, owner, minute, m):
        if c >= self.nreal:
            return []
        return [(t_, self.c["MineRadius"], self.c["MineEmit"]) for t_ in self.mines[c]]

    def pick(self, m, c, own, rivals, starts, owner, claim, era, t):
        """The live planner's fission site (LivingEconomyModel.ChooseFissionSite): the best food site 7–14 tiles
        from the parent, at least 7 from any settlement, not on rival land. Food score ≈ habitable weight within
        the radius-3 range; parents take turns, oldest first."""
        if not self.live_sites:
            return pick_site(m, c, own, rivals, starts, owner, claim, era, t)
        parent = own[self.turn[c] % len(own)]
        self.turn[c] += 1
        allsett = own + [x for lst in rivals for x in lst]
        near = np.min([m.dist_from(x) for x in allsett], axis=0)
        dp = m.dist_from(parent)
        ok = (m.hab > 0) & (m.biome != PEAK) & (dp >= 7) & (dp <= 14) & (near >= 7) & ((owner == -1) | (owner == c))
        ok &= m.mass == m.mass[parent]
        cand = np.flatnonzero(ok)
        if len(cand) == 0:
            return None
        if self.food is None:
            self.food = np.array([m.hab[disk(m, i, 3)[0]].sum() for i in range(m.N)])
        flip = starts[c] >= m.N / 2
        best = max(cand, key=lambda i: (round(float(self.food[i]), 6), -(m.N - 1 - i if flip else i)))
        return int(best), float(self.food[best])

    def decree_sites(self, c, cv, own, rivals, owner, claim, era, minute):
        xc = self.x(c, minute)
        if not self.found or xc is None:
            return []
        out = []
        for at in self.c["FoundAt"]:
            if xc < at or at in self.found_done[c]:
                continue
            self.found_done[c].add(at)
            m = self.m
            r = 4
            near = np.min([m.dist_from(x) for x in own], axis=0)
            rs = [x for lst in rivals for x in lst] + [t_ for _, t_ in self.tinfolk]
            rnear = np.min([m.dist_from(x) for x in rs], axis=0) if rs else np.full(m.N, 10 ** 6)
            ok = (m.hab > 0) & (m.biome != MOUNTAIN) & (near >= r + 1) & (rnear >= 9) & ((owner == -1) | (owner == c))
            wd = self._walk(own, self.c["SettleReach"] + 4)
            ok &= wd <= self.c["SettleReach"]
            targets = [t_ for t_ in self.dep["tin_mid"] + self.dep["tin_far"] if owner[t_] in (-1, c)
                       and all(t_ not in mm for mm in self.mines)]
            site = None
            for tgt in sorted(targets, key=lambda t_: (int(wd[t_]), t_)):
                d = m.dist_from(tgt)
                cand = np.flatnonzero(ok & (d <= 3))
                if len(cand):
                    site = int(min(cand, key=lambda i: (int(d[i]), -int(near[i]), i)))
                    break
            if site is None:   # no tin target: the frontier site with the most free land
                got = pick_site(m, c, own, rivals, self.starts, owner, claim, era, tp(Radius=[3, 3, 4, 4, 5, 5, 5, 7, 9, 9], T=0.7))
                site = got[0] if got else None
            if site is not None:
                self.founds[c] += 1
                out.append(site)
                own = own + [site]
        return out


def run_e3(seed_live, map_seed, regime="village", found=True, cfg=None, minutes=64, site_radius=3, claim_mult=None):
    t = tp(Radius=[3, 3, 4, 4, 5, 5, 5, 7, 9, 9], T=0.7, Camps=1)
    if claim_mult is not None:
        t["ClaimMult"] = [1.0, 1.15, claim_mult, claim_mult, 1.3, 1.3, 1.3, 1.56, 1.56, 1.56]
    if site_radius:
        t["SiteRadius"] = [3, 3, site_radius, site_radius, 5, 5, 5, 7, 9, 9]
    m = make_map(160, 100, t["LandFrac"], map_seed, "continents", True)
    starts = place_starts(m, 4, 25, 0)
    trajs = live_trajs(seed_live, regime)
    dep, dist = place_deposits(m, starts)
    n_neutral = len(dep["tin_far"])
    trajs_all = trajs + [neutral_traj() for _ in range(n_neutral)]
    starts_all = list(starts) + [starts[0]] * n_neutral   # placeholders; cleared at step 1
    hk = E3Hooks(m, starts_all, trajs_all, 4, regime, dep, cfg, found)
    t["hooks"] = hk
    res = run(m, starts_all, trajs_all, t, max_seasons=minutes * 4)
    res["hooks"], res["dep"], res["dist"], res["trajs"] = hk, dep, dist, trajs
    return res


def e3_metrics(res):
    hk, rows, trajs = res["hooks"], res["rows"], res["trajs"]
    out = []
    for c in range(4):
        k3 = trajs[c]["k3"]
        if k3 is None:
            continue
        k4 = k3 + hk.c["E3Minutes"]
        r4 = min(rows, key=lambda r: abs(r["minute"] - k4))
        r3 = min(rows, key=lambda r: abs(r["minute"] - k3))
        partner = c ^ 1
        border = res["contact"].get((min(c, partner), max(c, partner)), {}).get("border")
        natural = max(0, r4["S_target"][c] - r3["S_target"][c])
        out.append(dict(civ=c, k3=k3, k4=k4, S=r4["S"][c], claimed=r4["claimed"], share=r4["share"][c],
                        border=border, touch=border is not None and border <= k4, founds=hk.founds[c],
                        found_share=hk.founds[c] / max(1, natural + hk.founds[c]),
                        tin=hk.tin_at[c] is not None and hk.tin_at[c] <= k4,
                        compact=hk.compact_at[c] is not None and hk.compact_at[c] <= k4,
                        arsenic=hk.arsenic_at[c] is not None and hk.arsenic_at[c] <= k4,
                        mines=len(hk.mines[c]), tinfolk=len(hk.tinfolk)))
    return out


def e3_report(out=print, seeds=None, check=False):
    import live_data as L
    import statistics as st
    seeds = seeds or sorted({r["seed"] for r in L.load()})
    maps = [1, 3, 4, 6]
    live = L.load()
    results = {}
    for regime, found in (("village", False), ("village", True), ("towns", True)):
        ms = []
        val = []
        for i, sd in enumerate(seeds):
            res = run_e3(sd, maps[i % len(maps)], regime, found)
            ms += e3_metrics(res)
            if regime == "village" and not found:
                for mi in (30, 35, 40, 45, 50):
                    r = min(res["rows"], key=lambda x: abs(x["minute"] - mi))
                    lv = st.mean(x["claimed"] for x in L.by_minute(live, mi) if x["seed"] == sd)
                    val.append((mi, 100 * r["claimed"], lv))
        results[(regime, found)] = ms
        lab = f"{regime} housing, {'Found decrees at E3 0.25 and 0.60' if found else 'no decrees'}"
        out(f"== {lab} ({len(ms)} civs, {len(seeds)} seeds)")
        if val:
            out("  validation, claimed % of habitable land (model / live, mean over seeds):  " + "  ".join(
                f"min {mi}: {st.mean(v[1] for v in val if v[0] == mi):.1f}/{st.mean(v[2] for v in val if v[0] == mi):.1f}"
                for mi in (30, 35, 40, 45, 50)))
        S = [x["S"] for x in ms]
        cl = [100 * x["claimed"] for x in ms]
        touch_pairs = {}
        for i, sd in enumerate(seeds):
            pass
        out(f"  T11 settlements per civ at KEY-4: median {st.median(S)} (p10 {L.q(S, .1):.0f}, p90 {L.q(S, .9):.0f})  [target 7 (5–10)]")
        out(f"  T14 claimed land at KEY-4: median {st.median(cl):.1f}% (p10 {L.q(cl, .1):.1f}, p90 {L.q(cl, .9):.1f})  [30–42%]")
        out(f"  T15 paired borders touch by own KEY-4: {100 * sum(x['touch'] for x in ms) / len(ms):.0f}% of civs; median first touch "
            f"{st.median([x['border'] for x in ms if x['border']]) if any(x['border'] for x in ms) else '-'} min  [≥ 75% of seeds]")
        if found:
            out(f"  T16 E3 foundings by decree: {100 * st.mean(x['found_share'] for x in ms):.0f}% ({st.mean(x['founds'] for x in ms):.1f} a civ)  [20–40%]")
        out(f"  T20 tin by KEY-4: own mine {100 * st.mean(x['tin'] for x in ms):.0f}%, Tin Folk compact in reach "
            f"{100 * st.mean(x['compact'] for x in ms):.0f}%, either {100 * st.mean(x['tin'] or x['compact'] for x in ms):.0f}%; "
            f"arsenical mine instead {100 * st.mean(x['arsenic'] for x in ms):.0f}%; some bronze "
            f"{100 * st.mean(x['tin'] or x['compact'] or x['arsenic'] for x in ms):.0f}%  [≥ 50% tin; ≥ 90% some bronze]")
        out(f"  Tin Folk villages per map: {st.mean(x['tinfolk'] for x in ms):.1f} (of 4 far placers)  [3–6 incl. islands]")
    if check:
        ms = results[("village", True)]
        assert st.median([x["S"] for x in ms]) >= 4
        assert st.median([100 * x["claimed"] for x in ms]) >= 25
        print("territory_sim e3 check passed")
    return results


if __name__ == "__main__":
    if sys.argv[1:2] == ["e3"]:
        e3_report(check="--check" in sys.argv)
    else:
        summary(scenario("standard, 4x Balanced, max-spread starts"))
