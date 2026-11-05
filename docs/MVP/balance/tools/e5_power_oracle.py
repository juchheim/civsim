#!/usr/bin/env python3
"""Provisional E5 mechanical-power, textile and blast-furnace fuel ledgers (docs/MVP/27 §5.1, §5.2, F-T2, F-T3).

Every number marked P is a hypothesis for the oracle to vary, not a fit and not an acceptance claim. Where a number
comes from the E4 sim or the registries it is cited. Time and labor are in the E4 units: ws = one adult's work-season
share (38/35 ws per adult per season, e4_metal_oracle), wood in litres, forest regrowth 0.5 L/season/tile
(1.0 with WOD-04 Coppicing; 22 §5.5).

Part A: the power-unit ledger. A powered job needs one power unit per worker per season; a water wheel supplies 5
(NRG-03 'grind x5' is why a wheel is worth 5 hand grinders), a windmill 4 (P), a horse gin 2 (P). A job's multiplier
applies only to workers who hold a unit. Part B: hand-powered textile kits (no source needed). Part C: the blast furnace
against the bloomery under the forest, with the iron-demand response ("cheaper iron gets used more").
"""
from __future__ import annotations

import argparse
import importlib.util
import math
from pathlib import Path

HERE = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("e4_metal_oracle", HERE / "e4_metal_oracle.py")
e4m = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(e4m)

# --- Part A: power units -------------------------------------------------------------------------
SOURCE_UNITS = {"water_wheel": 5.0, "windmill": 4.0, "horse_gin": 2.0}  # P (wheel from 26 'grind x5')
WIND_AVAILABILITY = 0.8  # P: share of seasons a windmill turns
# job -> (share of adult work done by hand today P, multiplier when powered, source of the multiplier)
JOBS = {
    "grind": (0.060, 5.0, "NRG-03 grind x5; windmill assumed equal, P"),
    "saw": (0.020, 4.0, "WOD-09 'Saw: Mill x4'"),
    "hammer": (0.040, 2.0, "MEC-07 'mill-driven crafts x2' (trip hammer, fulling, bellows)"),
}
GRIND_PRIORITY = ("grind", "saw", "hammer")  # food first, then building, then crafts


def powered_workers_needed(adults, job):
    share, mult, _ = JOBS[job]
    return adults * share / mult  # workers who must hold a power unit to carry the whole job


def allocate(adults, units):
    """Spend `units` power units in priority order. Returns per-job covered fraction and labor freed (ws-equivalents)."""
    left = units
    out = {}
    for job in GRIND_PRIORITY:
        share, mult, _ = JOBS[job]
        need_units = powered_workers_needed(adults, job)
        used = min(left, need_units)
        left -= used
        covered = used / need_units if need_units else 1.0
        by_hand = adults * share
        freed = covered * by_hand * (1 - 1 / mult)
        out[job] = dict(need_units=need_units, used=used, covered=covered, freed=freed)
    out["_units_left"] = left
    return out


def sources_needed(adults):
    units = sum(powered_workers_needed(adults, j) for j in JOBS)
    return units, math.ceil(units / SOURCE_UNITS["water_wheel"] - 1e-12)


def part_a():
    print("== Part A: power-unit ledger (P values)")
    print("  job shares of adult work done by hand: " + ", ".join(f"{j} {JOBS[j][0]:.0%} (x{JOBS[j][1]:g})" for j in JOBS))
    max_freed = sum(s * (1 - 1 / m) for s, m, _ in JOBS.values())
    print(f"  ceiling with full coverage: {max_freed:.1%} of adult labor freed")
    print(f"  {'adults':>6s} {'units to cover all':>19s} {'wheels':>7s} | " + " | ".join(f"{w} wheel{'s' if w != 1 else ' '}: freed" for w in (0, 1, 2, 4)))
    for adults in (30, 90, 300, 900):
        units, wheels = sources_needed(adults)
        cells = []
        for w in (0, 1, 2, 4):
            a = allocate(adults, w * SOURCE_UNITS["water_wheel"])
            freed = sum(a[j]["freed"] for j in JOBS)
            cells.append(f"{100 * freed / adults:5.1f}% of work")
        print(f"  {adults:6d} {units:19.2f} {wheels:7d} | " + " | ".join(f"{c:>20s}" for c in cells))
    # What the ledger refuses: naive stacking of every multiplier on every worker.
    adults = 300
    naive = sum(adults * s * (1 - 1 / m) for s, m, _ in JOBS.values())
    one = allocate(adults, SOURCE_UNITS["water_wheel"])
    print(f"  naive stacking (no sources) at {adults} adults frees {100 * naive / adults:.1f}% of work; one wheel really frees "
          f"{100 * sum(one[j]['freed'] for j in JOBS) / adults:.1f}% and covers grind {one['grind']['covered']:.0%}.")
    print(f"  wind: a windmill is {SOURCE_UNITS['windmill']:g} units at {WIND_AVAILABILITY:.0%} availability = {SOURCE_UNITS['windmill'] * WIND_AVAILABILITY:.1f} effective units.")


# --- Part B: textile kits ------------------------------------------------------------------------
TEXTILE = {"spin": (0.050, 3.0, "FIB-11 spinning wheel x3"), "weave": (0.030, 2.0, "FIB-10 treadle loom x2")}  # shares are P


def part_b():
    print("\n== Part B: textile kits (hand-powered; no source)")
    freed = sum(s * (1 - 1 / m) for s, m, _ in TEXTILE.values())
    print("  shares of adult work (P): " + ", ".join(f"{k} {v[0]:.0%} (x{v[1]:g})" for k, v in TEXTILE.items()))
    print(f"  full coverage frees {freed:.1%} of adult labor; together with Part A: {freed + sum(s * (1 - 1 / m) for s, m, _ in JOBS.values()):.1%}")
    print("  Reading: E5 machines free about a seventh of adult labor in total (P). That is the pool the new monks, scholars,")
    print("  soldiers and builders must be drawn from, so E5's institutions are limited by labor, not by research.")


# --- Part C: blast furnace and the forest --------------------------------------------------------
REGROW = {"plain": 0.5, "coppiced": 1.0}  # L wood / season / forested tile (22 §5.5)
CAST_IRON_L_PER_100_ADULTS = 0.40  # P: iron litres/season for pots, parts and cannon bodies per 100 adults once cast iron exists
BF_LABOR_WS_PER_L = 0.8 / 0.5 * 0.5  # P: BF labor per litre of iron = half the bloomery's 1.6 ws/L


def kit_charcoal_split(tier):
    """(bloomery-step charcoal, total charcoal) per kit. Bloom batches = ore / 2 (E3 bloomery: 2 ore + 3 charcoal)."""
    d = e4m.embodied(tier)
    bloom_char = d["ore"] / e4m.BLOOMERY["ore"] * e4m.BLOOMERY["charcoal"]
    return bloom_char, d["charcoal"]


def wood_per_100_adults(tier, bf_ratio, demand_mult, cast=True):
    """Wood litres/season per 100 adults for kit replacement plus cast iron, with the blast furnace using `bf_ratio` x the
    bloomery's charcoal per litre of iron for the iron-making step only (refining and carburizing are unchanged)."""
    rep = e4m.replacement(tier)
    bloom_char, total_char = kit_charcoal_split(tier)
    per_kit_char = (total_char - bloom_char) + bloom_char * bf_ratio
    kit_wood = rep["kits"] * per_kit_char * e4m.CHARCOAL["wood"]
    cast_char = CAST_IRON_L_PER_100_ADULTS * (e4m.BLOOMERY["charcoal"] / e4m.BLOOMERY["iron"]) * bf_ratio if cast else 0.0
    cast_wood = cast_char * e4m.CHARCOAL["wood"]
    return (kit_wood + cast_wood) * demand_mult if cast else kit_wood * demand_mult


def part_c():
    print("\n== Part C: blast furnace against the forest (wood per 100 adults per season, T12 kits + cast iron)")
    bloom, total = kit_charcoal_split("T12")
    print(f"  T12 kit charcoal {total:.5f}, of which the bloomery step {bloom:.5f} ({bloom / total:.0%}); only that step changes with the furnace.")
    base = wood_per_100_adults("T12", 1.0, 1.0, cast=False)
    print(f"  E4 baseline (bloomery, T12 replacement only): {base:.2f} L/season = {base / REGROW['plain']:.1f} forested tiles plain, "
          f"{base / REGROW['coppiced']:.1f} coppiced, per 100 adults.")
    t11 = wood_per_100_adults("T11", 1.0, 1.0, cast=False)
    print(f"  (T11 kits wear out twice as fast: {t11:.2f} L/season = {t11 / REGROW['coppiced']:.1f} coppiced tiles per 100 adults; the live mix sits between.)")
    print("  Table cells: forested tiles needed per 100 adults, coppiced (WOD-04), cast iron demand included.")
    ratios = (1.0, 0.75, 0.5, 0.33)
    print(f"  {'iron demand x':>14s} | " + " | ".join(f"furnace charcoal/L x{r:<4g}" for r in ratios))
    for m in (1, 2, 3, 4):
        cells = [wood_per_100_adults("T12", r, m) / REGROW["coppiced"] for r in ratios]
        print(f"  {m:14d} | " + " | ".join(f"{c:19.1f}" for c in cells))
    print("  Marks: a modest E3-style forest is ~13 tiles; a wood-rich one ~30 (m11-wp6-fuel-check). Houses, ships and hearths come on top.")
    # Break-even demand growth: the furnace saves forest only if the demand multiplier stays below this.
    for r in ratios[1:]:
        be = wood_per_100_adults("T12", 1.0, 1.0, cast=False) / wood_per_100_adults("T12", r, 1.0, cast=False)
        print(f"  furnace at x{r:g} charcoal per litre breaks even on forest if iron use grows less than x{be:.2f} (kits only, no cast iron).")
    cast_wood = CAST_IRON_L_PER_100_ADULTS * (e4m.BLOOMERY["charcoal"] / e4m.BLOOMERY["iron"]) * e4m.CHARCOAL["wood"]
    print(f"  Cast iron adds {CAST_IRON_L_PER_100_ADULTS:g} L iron/100 adults/season (P); at x1 fuel that is {cast_wood:.1f} L wood on its own.")


def check():
    # Part A
    a = allocate(300, 0)
    assert all(a[j]["freed"] == 0 for j in JOBS)
    units, wheels = sources_needed(300)
    assert math.isclose(units, 300 * (0.06 / 5 + 0.02 / 4 + 0.04 / 2)) and wheels == 3, (units, wheels)  # 11.1 units / 5 per wheel = 2.22 -> 3
    a = allocate(300, SOURCE_UNITS["water_wheel"] * wheels)
    assert all(math.isclose(a[j]["covered"], 1.0) for j in JOBS) and a["_units_left"] >= 0
    full = sum(a[j]["freed"] for j in JOBS)
    assert math.isclose(full, 300 * sum(s * (1 - 1 / m) for s, m, _ in JOBS.values()))
    one = allocate(300, 5.0)
    assert one["grind"]["covered"] == 1.0 and one["saw"]["covered"] < 1.0 + 1e-12 and sum(one[j]["used"] for j in JOBS) <= 5.0 + 1e-12
    # No multiplier is claimed without a unit: freed labor is monotone and bounded by the ceiling.
    prev = -1.0
    for u in (0, 1, 2, 5, 10, 40):
        f = sum(allocate(300, u)[j]["freed"] for j in JOBS)
        assert f >= prev - 1e-12 and f <= 300 * sum(s * (1 - 1 / m) for s, m, _ in JOBS.values()) + 1e-9
        prev = f
    # Part C: T12 split sums, ratio 1.0 reproduces the E4 replacement exactly.
    bloom, total = kit_charcoal_split("T12")
    d = e4m.embodied("T12")
    assert math.isclose(total, d["charcoal"]) and 0 < bloom < total
    assert math.isclose(bloom, 1.125), bloom  # 0.375 bloom batches x 3 charcoal, hand-checked from 26 §2.3
    assert math.isclose(wood_per_100_adults("T12", 1.0, 1.0, cast=False), e4m.replacement("T12")["wood"])
    assert wood_per_100_adults("T12", 0.5, 1.0) < wood_per_100_adults("T12", 1.0, 1.0)
    assert math.isclose(wood_per_100_adults("T12", 1.0, 3.0, cast=False), 3 * wood_per_100_adults("T12", 1.0, 1.0, cast=False))
    print("--check PASS: power units are conserved and monotone; 3 wheels cover 300 adults; blast furnace ratio 1.0 reproduces the E4 T12 wood ledger")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    if args.check:
        check()
    else:
        part_a()
        part_b()
        part_c()
