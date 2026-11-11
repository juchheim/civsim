#!/usr/bin/env python3
"""Checks a settlement-core layout dump (docs/MVP/31 WP1, WP2).

The dump is written by the renderer when the game runs with ``--core-layout --layout-dump=FILE``
(game/Renderer/LowPoly3D/Scripts/core_layout.gd). For every live settlement it checks:

1. no prop centre lies outside the seven-hex core, except the outer works of 31 section 4.7
   (the hex is recomputed from the position with the renderer's own tile geometry);
2. outer works stand on tiles that hold no plot;
3. no two footprints overlap (circles, and oriented boxes for wall runs, gates, the weir,
   the harbor and aqueduct spans; runs of one perimeter may touch each other, and a hearth
   and its banner are one group);
4. at most one tier of each upgrade chain is drawn (perimeter, gate, sacred, council, watch,
   food store, pottery, metal, water, grinding, shelter);
5. the growing core (docs/MVP/31a): every reserved ring-2 hex lies at hex distance exactly 2 from
   the site and outside every other settlement's seven-hex core; there is one reserved hex per
   ring-2 district; each whole-hex building of a settlement with ring-2 hexes is drawn whole (never
   dropped or halved); and no garden cue stands on a reserved ring-2 hex.

Usage: python3 tools/check_core_layout.py DUMP.json [--quiet]
Exit status 0 when every check passes, 1 otherwise.
"""
import json
import math
import sys

S = 0.8660254037844386
TOLERANCE = 0.002  # metres of overlap ignored (float noise)


def tile_center(t, width):
    x, y = t % width, t // width
    return (x + 0.5 + (0.5 if y & 1 else 0.0), (y + 0.5) * S)


def tile_at(px, pz, width, height):
    """world_data.gd tile_at: the tile whose centre is nearest."""
    ry = math.floor(pz / S)
    best, bd = -1, 1e18
    for yy in range(ry - 1, ry + 2):
        if yy < 0 or yy >= height:
            continue
        off = 0.5 if yy & 1 else 0.0
        q = px - off
        xx = math.ceil(q) - 1
        xx = min(max(xx, 0), width - 1)
        for cx in (xx, xx + 1):
            if cx < 0 or cx >= width:
                continue
            dx = cx + 0.5 + off - px
            dz = (yy + 0.5) * S - pz
            d = dx * dx + dz * dz
            if d < bd:
                bd, best = d, yy * width + cx
    return best


def cube(t, width):
    """Odd rows are shifted right (world_data.gd tile_center), so offset (x, y) is "odd-r"."""
    x, y = t % width, t // width
    q = x - (y - (y & 1)) // 2
    return q, y, -q - y


def hex_distance(a, b, width):
    qa, ra, sa = cube(a, width)
    qb, rb, sb = cube(b, width)
    return max(abs(qa - qb), abs(ra - rb), abs(sa - sb))


WHOLE_KINDS = ("temple", "academy", "theatre", "bathhouse", "castle", "ziggurat", "dome", "library")


def box_corners(b):
    cx, cz, hx, hz, phi = b
    ux, uz = math.cos(phi), math.sin(phi)
    vx, vz = -uz, ux
    return [(cx + sx * hx * ux + sz * hz * vx, cz + sx * hx * uz + sz * hz * vz) for sx, sz in ((1, 1), (1, -1), (-1, -1), (-1, 1))]


def circle_box_gap(cx, cz, r, b):
    bx, bz, hx, hz, phi = b
    dx, dz = cx - bx, cz - bz
    ux, uz = math.cos(phi), math.sin(phi)
    lx = dx * ux + dz * uz
    lz = -dx * uz + dz * ux
    qx = max(abs(lx) - hx, 0.0)
    qz = max(abs(lz) - hz, 0.0)
    return math.hypot(qx, qz) - r


def box_box_overlap(a, b):
    """Separating axis test; returns the smallest penetration (>0 overlapping)."""
    ca, cb = box_corners(a), box_corners(b)
    depth = 1e9
    for phi in (a[4], a[4] + math.pi / 2, b[4], b[4] + math.pi / 2):
        ux, uz = math.cos(phi), math.sin(phi)
        pa = [x * ux + z * uz for x, z in ca]
        pb = [x * ux + z * uz for x, z in cb]
        d = min(max(pa), max(pb)) - max(min(pa), min(pb))
        if d <= 0:
            return d
        depth = min(depth, d)
    return depth


def overlap(i, j):
    """Penetration depth of two footprints (positive = overlapping)."""
    bi, bj = i.get("box"), j.get("box")
    if bi and bj:
        return box_box_overlap(bi, bj)
    if bi:
        return -circle_box_gap(j["x"], j["z"], j["r"], bi)
    if bj:
        return -circle_box_gap(i["x"], i["z"], i["r"], bj)
    return i["r"] + j["r"] - math.hypot(i["x"] - j["x"], i["z"] - j["z"])


def check(path, quiet=False):
    data = json.load(open(path))
    width, height = data["width"], data["height"]
    plots = set(data["plots"][0::2])
    problems = []
    totals = {"settlements": 0, "items": 0, "outer": 0, "pairs": 0}
    by_stage = {}
    sites = [r["tile"] for r in data["settlements"]]
    grown = 0
    for row in data["settlements"]:
        totals["settlements"] += 1
        by_stage[row["stage"]] = by_stage.get(row["stage"], 0) + 1
        label = f"settlement {row['settlement']} (tile {row['tile']}, {row['stage']}{', ' + row['gallery'] if row['gallery'] else ''})"
        core = set(t for t in row["core_tiles"] if t >= 0)
        items = row["items"]
        totals["items"] += len(items)
        for it in items:
            tile = tile_at(it["x"], it["z"], width, height)
            if it.get("outer"):
                totals["outer"] += 1
                if tile in plots and it["kind"] not in ("water_mill", "aqueduct_span", "frontier_wall", "siege"):
                    problems.append(f"{label}: outer work {it['kind']} stands on plot tile {tile}")
                continue
            if tile not in core:
                problems.append(f"{label}: {it['kind']} ({it['mesh']}) at ({it['x']:.3f}, {it['z']:.3f}) is on tile {tile}, outside the core")
        for a in range(len(items)):
            for b in range(a + 1, len(items)):
                i, j = items[a], items[b]
                if i.get("group") == j.get("group"):
                    continue
                if i.get("perimeter") and j.get("perimeter"):
                    continue
                totals["pairs"] += 1
                d = overlap(i, j)
                if d > TOLERANCE:
                    problems.append(f"{label}: {i['kind']} ({i['quarter']} {i['slot']}) and {j['kind']} ({j['quarter']} {j['slot']}) overlap by {d:.3f}")
        ring2 = row.get("ring2_tiles", [])
        districts = row.get("ring2_districts", [])
        if ring2:
            grown += 1
            if len(ring2) != len(districts):
                problems.append(f"{label}: {len(ring2)} ring-2 hexes for {len(districts)} districts")
            for t in ring2:
                d = hex_distance(row["tile"], t, width)
                if d != 2:
                    problems.append(f"{label}: reserved hex {t} is {d} from the site, not 2")
                for other in sites:
                    if other != row["tile"] and hex_distance(other, t, width) <= 1:
                        problems.append(f"{label}: reserved hex {t} lies in the core of the settlement at {other}")
            for it in items:
                if it["kind"] == "garden" and tile_at(it["x"], it["z"], width, height) in ring2:
                    problems.append(f"{label}: a garden cue stands on reserved hex {tile_at(it['x'], it['z'], width, height)}")
                if it["kind"] in WHOLE_KINDS and it["slot"] != "whole":
                    problems.append(f"{label}: {it['kind']} is drawn at {it['slot']}, not whole")
            for k in row["dropped"]:
                if k in WHOLE_KINDS:
                    problems.append(f"{label}: whole-hex building {k} was dropped")
            if not quiet and row["gallery"]:
                print(f"{label}: ring 2 {dict(zip(districts, ring2))}")
        tiers = {}
        for it in items:
            if it.get("chain"):
                tiers.setdefault(it["chain"], set()).add(it["tier"])
        for chain, ts in sorted(tiers.items()):
            if len(ts) > 1:
                problems.append(f"{label}: chain {chain} shows {len(ts)} tiers {sorted(ts)}")
        if not quiet and row["gallery"]:
            chains = ", ".join(f"{c}={'/'.join(sorted(t))}" for c, t in sorted(tiers.items()))
            print(f"{label}: {len(items)} items; quarters {row['active_quarters']}; chains {chains}; dropped {row['dropped']}")
    print(f"checked {totals['settlements']} settlements {by_stage} ({grown} with ring-2 hexes), {totals['items']} items ({totals['outer']} outer works), {totals['pairs']} footprint pairs")
    for p in problems:
        print("FAIL", p)
    print("PASS" if not problems else f"{len(problems)} problem(s)")
    return 0 if not problems else 1


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    sys.exit(check(sys.argv[1], "--quiet" in sys.argv))
