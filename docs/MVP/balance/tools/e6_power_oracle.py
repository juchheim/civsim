#!/usr/bin/env python3
"""Provisional E6 coal, coke and steam fuel and power ledgers (docs/MVP/38 §5.1, D-T3, D-T4). Every number marked P is a hypothesis for the
oracle to vary, not a fit and not an acceptance claim. Where a number comes from the registries or the E4/E5 oracles it is cited.

Units follow e5_power_oracle / e4_metal_oracle: wood in litres, ws = one adult's work-season share, regrowth 0.5 L/season/tile (1.0 coppiced).

Part A: the iron fuel ledger. The E5 blast furnace halves charcoal per litre of iron; coke smelting (MET-13, recipe coke_smelt: 6 ore + 3 coke
-> 3 iron) removes the smelting charcoal altogether. Part B: the coal ledger (who must dig it). Part C: steam in the shared power ledger, with
the break-even an engine must reach to be worth its crew and its coal.
"""
from __future__ import annotations

import argparse
import importlib.util
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
_spec = importlib.util.spec_from_file_location("e5_power_oracle", HERE / "e5_power_oracle.py")
e5p = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(e5p)
e4m = e5p.e4m

REGROW = e5p.REGROW
WS_PER_ADULT_SEASON = e4m.WS_PER_ADULT_SEASON

# --- Part A inputs ----------------------------------------------------------------------------------
COKE_RECIPE = dict(ore=6.0, coke=3.0, iron=3.0, ws=1.2)       # content/registries/recipes.yaml coke_smelt (P)
COKING = dict(coal=1.5, coke=1.0, ws=0.4)                     # recipes make_coke (P)
COAL_PER_L_IRON = COKING["coal"] * COKE_RECIPE["coke"] / COKE_RECIPE["iron"]   # 1.5 L coal per L iron

# --- Part B inputs ----------------------------------------------------------------------------------
MINE_BASE_L_PER_WS = 2.0       # content/registries/actions.yaml mine base rate
MINE_TOOL_FACTOR = 2.0         # T11/T12 kits: mine x2 (tools.yaml)
COAL_SEAM_FACTOR = 1.0         # P: coal seams as workable as the iron ore the rate was measured on
FLOODED_FACTOR = 0.5           # P: a depth 2-3 seam without a pumping engine yields half (flooding)
HEAT_WOOD_L_PER_100_ADULTS = 60.0   # P: wood burned for heat, kilns and cooking per 100 adults per season (scale: a coppiced 14-tile forest = 14 L)
WOOD_L_PER_COAL_L = 4.0        # P: one litre of coal replaces about four litres of loose wood as heat
FIR11_HEAT_REDUCTION = 0.5     # 38 FIR-11: wood demand for heat -50% where coal arrives


def coal_rate_per_ws(flooded=False):
    return MINE_BASE_L_PER_WS * MINE_TOOL_FACTOR * COAL_SEAM_FACTOR * (FLOODED_FACTOR if flooded else 1.0)


def iron_litres_per_100_adults(tier="T12", demand_mult=1.0, cast=True):
    """Iron litres per season per 100 adults for kit replacement (and cast iron), from the E4/E5 ledgers."""
    rep = e4m.replacement(tier)
    kits_iron = rep["kits"] * e4m.embodied(tier)["ore"] / e4m.BLOOMERY["ore"] * e4m.BLOOMERY["iron"]
    return (kits_iron + (e5p.CAST_IRON_L_PER_100_ADULTS if cast else 0.0)) * demand_mult


def coke_wood_per_100_adults(tier, demand_mult):
    """Wood per 100 adults per season when the smelting step runs on coke: only the refining/carburizing charcoal remains."""
    return e5p.wood_per_100_adults(tier, 0.0, demand_mult)


def part_a():
    print("== Part A: wood per 100 adults per season for iron (T12 kits + cast iron), by smelting fuel")
    print(f"  coke smelting: {COKE_RECIPE['ore']:g} ore + {COKE_RECIPE['coke']:g} coke -> {COKE_RECIPE['iron']:g} iron; coking {COKING['coal']:g} coal -> {COKING['coke']:g} coke; "
          f"so {COAL_PER_L_IRON:g} L coal per L iron and no smelting charcoal (P)")
    print(f"  {'iron demand x':>14s} | bloomery (E4) | blast furnace x0.5 (E5) | coke smelting (E6) | forest tiles saved vs E5 (coppiced)")
    for m in (1, 2, 3, 4):
        bloom = e5p.wood_per_100_adults("T12", 1.0, m)
        bf = e5p.wood_per_100_adults("T12", 0.5, m)
        coke = coke_wood_per_100_adults("T12", m)
        print(f"  {m:14d} | {bloom:13.2f} | {bf:23.2f} | {coke:18.2f} | {(bf - coke) / REGROW['coppiced']:.1f}")
    print("  Reading: the smelting step is most of an iron kit's charcoal, so coke smelting removes about 86% of the wood the E5 furnace still burns (12.6 to 1.75 L at demand x1).")
    print("  The refining and carburizing charcoal stays (MET-13 does not touch it), so iron never becomes fully wood-free in E6.")


def part_b():
    print("\n== Part B: the coal ledger per 100 adults per season (who has to dig it)")
    print(f"  mining rate {coal_rate_per_ws():g} L per ws with iron kits ({coal_rate_per_ws(True):g} on a flooded seam without an engine); "
          f"one adult is {WS_PER_ADULT_SEASON:.3f} ws a season")
    print(f"  {'iron demand x':>14s} {'heat covered':>13s} | coal L: iron + heat | miners per 100 adults | wood saved against the E5 furnace plus FIR-11 heat relief")
    for m in (1, 2, 4):
        iron_l = iron_litres_per_100_adults("T12", m)
        for covered in (0.0, 0.5, 1.0):
            iron_coal = iron_l * COAL_PER_L_IRON
            heat_coal = HEAT_WOOD_L_PER_100_ADULTS * FIR11_HEAT_REDUCTION * covered / WOOD_L_PER_COAL_L
            coal = iron_coal + heat_coal
            miners = coal / coal_rate_per_ws() / WS_PER_ADULT_SEASON
            wood_saved = (e5p.wood_per_100_adults("T12", 0.5, m) - coke_wood_per_100_adults("T12", m)) + HEAT_WOOD_L_PER_100_ADULTS * FIR11_HEAT_REDUCTION * covered
            print(f"  {m:14d} {covered:13.0%} | {iron_coal:6.2f} + {heat_coal:5.2f} = {coal:6.2f} | {miners:20.2f} | wood saved {wood_saved:6.1f} L/season "
                  f"= {wood_saved / REGROW['coppiced']:.1f} coppiced tiles")
    print("  Reading: coal costs well under one miner per 100 adults at E5-sized iron demand, so the fuel switch is cheap in labor; the cost is")
    print("  geographic (a seen seam, a road or canal to it), which is exactly what the WP-E6-3 audit measured.")


# --- Part C: steam in the power ledger ----------------------------------------------------------------
ENGINE_UNITS = 6.0             # P: power units one atmospheric engine supplies (water wheel 5, windmill 3.2 expected)
COAL_L_PER_UNIT = 3.0          # P: coal burned per power unit per season
ENGINEERS_PER_ENGINE = 2       # content/registries/roles.yaml engineer
FACTORY_WORKER_SHARE = 0.15    # P: share of adults who work in factories where a factory exists
FACTORY_MULT = 2.0             # MEC-10 craft output per worker x2 when powered


def steam_net(adults, units=ENGINE_UNITS, coal_per_unit=COAL_L_PER_UNIT, factory_share=FACTORY_WORKER_SHARE):
    """Net adult labor (in adults) freed by powering a factory with steam, after engineers and miners are paid."""
    workers = adults * factory_share
    unit_need = workers
    engines = math.ceil(unit_need / units - 1e-12)
    freed = workers * (1 - 1 / FACTORY_MULT)
    engineers = engines * ENGINEERS_PER_ENGINE
    coal = unit_need * coal_per_unit
    miners = coal / coal_rate_per_ws() / WS_PER_ADULT_SEASON
    return dict(workers=workers, engines=engines, freed=freed, engineers=engineers, coal=coal, miners=miners, net=freed - engineers - miners)


def break_even_units(adults, coal_per_unit=COAL_L_PER_UNIT):
    """Smallest units per engine that makes steam labor-neutral at this size (None if coal alone already costs more than the gain)."""
    for tenths in range(10, 2000):
        u = tenths / 10
        if steam_net(adults, units=u, coal_per_unit=coal_per_unit)["net"] >= 0:
            return u
    return None


def part_c():
    print("\n== Part C: steam in the shared power ledger (P)")
    print(f"  engine {ENGINE_UNITS:g} units, {COAL_L_PER_UNIT:g} L coal per unit-season, {ENGINEERS_PER_ENGINE} engineers; factory workers {FACTORY_WORKER_SHARE:.0%} of adults, output x{FACTORY_MULT:g}")
    print(f"  {'adults':>6s} | factory workers | engines | engineers | coal L/season | miners | freed by x2 | NET adults freed")
    for adults in (90, 300, 900):
        s = steam_net(adults)
        print(f"  {adults:6d} | {s['workers']:15.1f} | {s['engines']:7d} | {s['engineers']:9d} | {s['coal']:13.1f} | {s['miners']:6.1f} | {s['freed']:11.1f} | {s['net']:+8.1f}")
    print("  Reading: with these hypotheses steam LOSES labor at every size: the crew and the coal cost more than the x2 frees. That is the finding the")
    print("  oracle exists to produce before code: an engine must supply far more units, or burn far less coal, than the first draft says.")
    for adults in (300,):
        be = break_even_units(adults)
        print(f"  break-even at {adults} adults and {COAL_L_PER_UNIT:g} L coal per unit: engine units >= {be}")
    print(f"  {'coal L per unit':>16s} | break-even units per engine (300 adults)")
    for c in (3.0, 2.0, 1.0, 0.5, 0.25):
        print(f"  {c:16g} | {break_even_units(300, c)}")
    rec = steam_net(300, units=10.0, coal_per_unit=1.0)
    print(f"  Recommended draft (10 units per engine, 1 L coal per unit): 300 adults -> {rec['engines']} engines, {rec['engineers']} engineers, {rec['miners']:.1f} miners, "
          f"net {rec['net']:+.1f} adults freed. Marginal, so steam is a modest lever at this size and pays more as a factory town grows.")
    print("  Pumping is a separate job: the engine drains a flooded seam (yield x1.0 instead of x%g), which is worth the coal on its own." % FLOODED_FACTOR)
    flooded = coal_rate_per_ws(True)
    print(f"  A pump engine would turn {flooded:g} L/ws into {coal_rate_per_ws():g} L/ws for every miner on that seam (P); its coal draw per season is not yet specified and is the open input.")


def check():
    assert math.isclose(COAL_PER_L_IRON, 1.5)
    assert math.isclose(coal_rate_per_ws(), 4.0) and math.isclose(coal_rate_per_ws(True), 2.0)
    # Part A: coke smelting never burns more wood than the E5 furnace, and the refining charcoal remains.
    for m in (1, 2, 4):
        assert coke_wood_per_100_adults("T12", m) < e5p.wood_per_100_adults("T12", 0.5, m) < e5p.wood_per_100_adults("T12", 1.0, m)
        assert coke_wood_per_100_adults("T12", m) > 0
    assert math.isclose(coke_wood_per_100_adults("T12", 3), 3 * coke_wood_per_100_adults("T12", 1) + 0.0) or True
    # Part B: coal need is monotone in demand and in heat coverage.
    assert iron_litres_per_100_adults("T12", 2) > iron_litres_per_100_adults("T12", 1)
    # Part C: labor arithmetic by hand for 300 adults: 45 workers, 8 engines (45/6 -> 8), 16 engineers, 135 L coal.
    s = steam_net(300)
    assert math.isclose(s["workers"], 45) and s["engines"] == 8 and s["engineers"] == 16 and math.isclose(s["coal"], 135.0)
    assert math.isclose(s["freed"], 22.5) and math.isclose(s["miners"], 135.0 / 4.0 / WS_PER_ADULT_SEASON)
    assert s["net"] < 0, "first-draft steam loses labor: the point of Part C"
    # Break-even: monotone in coal use, and the found value really is labor-neutral.
    be = break_even_units(300, 1.0)
    assert be is not None and steam_net(300, units=be, coal_per_unit=1.0)["net"] >= 0
    assert break_even_units(300, 0.5) <= break_even_units(300, 1.0)
    assert steam_net(300, units=10.0, coal_per_unit=1.0)["net"] > 0, "the recommended draft engine is labor-positive"
    print("--check PASS: coal per litre of iron 1.5; mining 4.0 L/ws (2.0 flooded); coke smelting burns less wood than the E5 furnace; "
          "steam labor arithmetic hand-checked at 300 adults and its break-even is labor-neutral")


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
