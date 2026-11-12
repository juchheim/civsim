#!/usr/bin/env python3
"""Checks a settlement-core layout dump (docs/MVP/31 WP1, WP2).

The dump is written by the renderer when the game runs with ``--layout-dump=FILE``
(game/Renderer/LowPoly3D/Scripts/core_layout.gd). For every live settlement it checks:

1. no prop centre lies outside the seven-hex core, except the outer works of 31 section 4.7
   (the hex is recomputed from the position with the renderer's own tile geometry);
2. outer works stand on tiles that hold no plot;
3. no two footprints overlap (circles, and oriented boxes for wall runs, gates, the weir,
   the harbor and aqueduct spans; runs of one perimeter may touch each other, and a hearth
   and its banner are one group);
4. at most one tier of each upgrade chain is drawn (perimeter, gate, sacred, council, watch,
   food store, pottery, metal, water, grinding, shelter);
5. the growing core (docs/MVP/31b): sim_outer is exactly the renderer's housed ring-2 rows;
   positions and district ids are valid, claims do not overlap each other or another core, every
   housed whole-hex building stands on its simulation tile, and no outer work or garden cue stands
   on any claimed tile. Plots still present on claims are reported for next-season release review.
6. when --frame is supplied, coreFront and coreOuter match an independent harness frame.

Usage: python3 tools/check_core_layout.py DUMP.json [--frame FRAME.json] [--quiet]
Exit status 0 when every check passes, 1 otherwise.
"""
import json
import math
import sys
import argparse

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


def check(path, quiet=False, frame_path=None):
    data = json.load(open(path))
    width, height = data["width"], data["height"]
    plots = set(data["plots"][0::2])
    problems = []
    totals = {"settlements": 0, "items": 0, "outer": 0, "pairs": 0, "outer_claims": 0}
    by_stage = {}
    district_ids = data.get("core_district_ids", [])
    rows = data["settlements"]
    claims = set()
    for row in rows:
        claims.update(t for t in row.get("core_tiles", []) if t >= 0)
        claims.update(int(outer[0]) for outer in row.get("sim_outer", []) if int(outer[0]) >= 0)
    outer_owner = {}
    plot_claims = []
    for row in data["settlements"]:
        totals["settlements"] += 1
        by_stage[row["stage"]] = by_stage.get(row["stage"], 0) + 1
        label = f"settlement {row['settlement']} (tile {row['tile']}, {row['stage']}{', ' + row['gallery'] if row['gallery'] else ''})"
        core = set(t for t in row["core_tiles"] if t >= 0)
        items = row["items"]
        totals["items"] += len(items)
        if row.get("front", -1) not in range(6):
            problems.append(f"{label}: front {row.get('front')} is outside 0..5")
        outer_rows = row.get("sim_outer", [])
        housed_tiles = []
        housed_districts = []
        unhoused_districts = []
        seen_districts = set()
        for tile, district, position in outer_rows:
            if district < 0 or district >= len(district_ids):
                problems.append(f"{label}: invalid core district id {district}")
                continue
            name = district_ids[district]
            if district in seen_districts:
                problems.append(f"{label}: district {name} appears more than once in sim_outer")
            seen_districts.add(district)
            if tile < 0:
                if tile != -1 or position != 255:
                    problems.append(f"{label}: malformed unhoused {name} row [{tile}, {district}, {position}]")
                unhoused_districts.append(name)
                continue
            totals["outer_claims"] += 1
            housed_tiles.append(tile)
            housed_districts.append(name)
            if position not in range(12):
                problems.append(f"{label}: {name} has invalid outer position {position}")
            if hex_distance(row["tile"], tile, width) != 2:
                problems.append(f"{label}: claimed hex {tile} is not at distance 2")
            previous = outer_owner.get(tile)
            if previous is not None:
                problems.append(f"{label}: claimed hex {tile} is also claimed by settlement {previous}")
            else:
                outer_owner[tile] = row["settlement"]
            for other in rows:
                if other is row:
                    continue
                if tile in set(t for t in other.get("core_tiles", []) if t >= 0):
                    problems.append(f"{label}: claimed hex {tile} lies in settlement {other['settlement']}'s seven-hex core")
        if housed_districts != sorted(housed_districts, key=lambda name: district_ids.index(name)):
            problems.append(f"{label}: sim_outer districts are not in priority order")
        if housed_tiles != row.get("rendered_outer_tiles", []):
            problems.append(f"{label}: renderer outer tiles do not equal the housed sim_outer tiles")
        if housed_districts != row.get("rendered_outer_districts", []):
            problems.append(f"{label}: renderer outer districts do not equal the housed sim_outer districts")
        if unhoused_districts != row.get("unhoused_districts", []):
            problems.append(f"{label}: unhoused districts are not listed accurately")
        for it in items:
            tile = tile_at(it["x"], it["z"], width, height)
            if it.get("outer"):
                totals["outer"] += 1
                slot_tile = int(it.get("slot_hex", tile))
                if slot_tile < 0:
                    slot_tile = tile
                if slot_tile in claims:
                    problems.append(f"{label}: outer work {it['kind']} stands on claimed tile {slot_tile}")
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
        for it in items:
            tile = tile_at(it["x"], it["z"], width, height)
            if it["kind"] == "garden" and tile in claims:
                problems.append(f"{label}: a garden cue stands on claimed hex {tile}")
        for district, kind in row.get("core_expected_items", []):
            if district < 0 or district >= len(district_ids):
                continue
            sim_row = next((r for r in outer_rows if r[1] == district and r[0] >= 0), None)
            if sim_row is None:
                continue
            tile = int(sim_row[0])
            matches = [it for it in items if it["kind"] == kind]
            if not matches:
                problems.append(f"{label}: housed {district_ids[district]} building {kind} is missing or dropped")
            elif not any(it["slot"] == "whole" and int(it.get("slot_hex", -1)) == tile for it in matches):
                problems.append(f"{label}: housed {district_ids[district]} building {kind} is not whole on sim tile {tile}")
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
    for tile in sorted(plots & claims):
        plot_claims.append(tile)
    print(f"checked {totals['settlements']} settlements {by_stage} ({totals['outer_claims']} outer claims), {totals['items']} items ({totals['outer']} outer works), {totals['pairs']} footprint pairs")
    if plot_claims:
        print(f"reported {len(plot_claims)} plots still on claimed tiles (check whether each claim is from this season): {plot_claims[:20]}")
    if frame_path:
        frame = json.load(open(frame_path))
        if int(frame.get("tick", -1)) != int(data.get("tick", -2)):
            problems.append(f"frame tick {frame.get('tick')} differs from renderer tick {data.get('tick')}")
        frame_by_tile = {int(s["tile"]): s for s in frame.get("settlements", [])}
        for row in rows:
            source = frame_by_tile.get(int(row["tile"]))
            if source is None:
                problems.append(f"settlement at {row['tile']} is absent from harness frame")
                continue
            expected_front = int(source.get("coreFront", 255))
            expected_outer = source.get("coreOuter", [])
            if row.get("front") != expected_front:
                problems.append(f"settlement at {row['tile']}: front {row.get('front')} differs from frame {expected_front}")
            if row.get("sim_outer", []) != expected_outer:
                problems.append(f"settlement at {row['tile']}: sim_outer differs from harness frame")
        print(f"cross-checked renderer claims against {frame_path}")
    for p in problems:
        print("FAIL", p)
    print("PASS" if not problems else f"{len(problems)} problem(s)")
    return 0 if not problems else 1


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("dump")
    parser.add_argument("--frame", help="optional independent civsim-harness view --dump-frame JSON")
    parser.add_argument("--quiet", action="store_true")
    args = parser.parse_args()
    sys.exit(check(args.dump, args.quiet, args.frame))
