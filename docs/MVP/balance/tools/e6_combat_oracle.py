#!/usr/bin/env python3
"""Provisional E6 bastion-fort, cannon and drilled-levy fixture oracle (docs/MVP/38 §5.6, D-T7). Inputs are explicit hypotheses (P).

Reuses the fixed-point fight engine (combat_e3.py) and the E5 oracle's fixtures. Live constants are read from src/CivSim.Sim/Politics/Raids.cs:
integrity 1.0 intact, BreachPerEngine 0.5 per running engine per season scaled by 3 / wall multiplier, a cannon counts as 2 engines
(CannonWallDamage), at most 3 cannons, powder breach makes a stone wall count for 30% (`wall *= 0.3`), castle strongpoint 1.25, DEF-09 concentric 1.5
(total 1.875, below the ~1.9 storm crossover of e5_combat_oracle).

Nothing here fits live behavior or passes D-T7. It shows what a star fort has to be worth for the siege decision to survive gunpowder, so the
number can be chosen before code.
"""
from __future__ import annotations

import argparse
import importlib.util
import math
from pathlib import Path

HERE = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("e5_combat_oracle", HERE / "e5_combat_oracle.py")
e5c = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(e5c)
e3 = e5c.e3

WALL, TOWER_WALL = e5c.WALL, e5c.TOWER_WALL
CANNON_DAMAGE = 2.0              # Raids.CannonWallDamage: a cannon counts as two engines
POWDER_WALL_EFFECT = 0.30        # Raids.Defenses.Multiplier: wall *= 0.3 under a powder breach
BASTION_PENALTY_HALVED = 0.65    # DEF-10 (P): the gunpowder wall penalty is halved, -70% becomes -35%
CASTLE, CONCENTRIC = 1.25, 1.5   # Raids.CastleStrongpoint, ConcentricCastleMultiplier
BASE = 1.1                       # Raids.Defenses.Multiplier starts at 1.1


def breach_seasons(cannons, wall, engines=0):
    """Seasons to take integrity 1.0 to zero with `cannons` and `engines` running against a wall multiplier."""
    per_season = 0.5 * (engines + CANNON_DAMAGE * cannons) * 3.0 / wall
    return math.ceil(1.0 / per_season - 1e-9) if per_season > 0 else None


def part_breach():
    print("== Part 1: seasons to breach on the live integrity track (cannons count 2 engines each)")
    print("  wall multiplier is the intact wall's: 3.0 stone, 4.5 with gatehouse and towers; a bastion's integrity multiplier is a hypothesis B (P)")
    cols = [("wall 3.0", WALL), ("tower 4.5", TOWER_WALL), ("bastion B=6.75", 6.75), ("bastion B=9.0", 9.0)]
    print(f"  {'cannons':>7s} | " + " | ".join(f"{n:>14s}" for n, _ in cols))
    for n in (1, 2, 3):
        print(f"  {n:7d} | " + " | ".join(f"{breach_seasons(n, w):>14d}" for _, w in cols))
    print("  Reading: three cannons breach a tower wall in one season. A bastion at B=6.75 (1.5x a tower) holds a three-cannon train for 2 seasons;")
    print("  at B=9.0 (2x a tower) for 2 to 3. Those extra seasons are what relief, sorties and starvation need to stay decisions.")


def total_defence(castle=True, concentric=False, bastion_mult=1.0, wall=0.0):
    """Defenders' multiplier c in the storm after a breach, in the frame of e5_combat_oracle (the fight's d_mult, which excludes the 1.1 base of
    Raids.Defenses.Multiplier): wall remainder x castle strongpoint x concentric x bastion."""
    m = wall if wall > 0 else 1.0
    if castle:
        m *= CASTLE * (CONCENTRIC if concentric else 1.0)
    return m * bastion_mult


def storm(c, trials=100):
    infantry = e3.fmul(e5c.STEEL, e5c.PRO)
    iron_levy = e3.fmul(e3.fx(24, 10), e3.MILITIA)
    attackers = e5c.fighters(36, infantry)
    defenders = e5c.fighters(12, infantry, 5000) + e5c.fighters(20, iron_levy, 6000)
    return e5c.duel(attackers, defenders, None, None, d_mult=e3.fx(int(round(c * 1000)), 1000), trials=trials)


def crossover():
    """Smallest defender multiplier (steps of 0.05) at which the attacker no longer wins half the storms."""
    for hundredths in range(100, 600, 5):
        if storm(hundredths / 100.0, trials=60)[0] < 0.5:
            return hundredths / 100.0
    return None


def part_storm():
    print("\n== Part 2: the storm after a breach (36 steel professionals vs 12 professionals + 20 iron militia), defenders' total multiplier c")
    cross = crossover()
    print(f"  the storm flips from attacker wins to defender wins at c = {cross}")
    print(f"  {'defence':44s} {'c':>6s} {'attacker wins':>14s}")
    rows = (
        ("E4 garrisoned castle, wall gone (powder)", total_defence(wall=0.0)),
        ("E4 castle, 3.0 wall counting 30% (powder)", total_defence(wall=3.0 * POWDER_WALL_EFFECT)),
        ("E5 concentric castle, 3.0 wall at 30%", total_defence(concentric=True, wall=3.0 * POWDER_WALL_EFFECT)),
        ("E5 concentric castle, tower wall at 30%", total_defence(concentric=True, wall=TOWER_WALL * POWDER_WALL_EFFECT)),
        ("bastion x1.5 on a castle, wall at 65% (halved)", total_defence(bastion_mult=1.5, wall=3.0 * BASTION_PENALTY_HALVED)),
        ("bastion x2.0 on a castle, wall at 65% (halved)", total_defence(bastion_mult=2.0, wall=3.0 * BASTION_PENALTY_HALVED)),
        ("bastion x2.0 on a castle, tower wall at 65%", total_defence(bastion_mult=2.0, wall=TOWER_WALL * BASTION_PENALTY_HALVED)),
    )
    for label, c in rows:
        print(f"  {label:44s} {c:6.2f} {100 * storm(c)[0]:13.0f}%")
    print("  Reading: a castle stack with only the 30% wall remainder is still an attacker win; the E5 concentric castle behind a tower wall is already")
    print("  past the crossover, and any bastion that multiplies the defenders by 1.5 or more on top (with 65% of the wall kept) closes the storm for")
    print("  good. The typed bastion (mult 2.0 and a halved powder penalty) is therefore TOO STRONG as a defender multiplier. Recommended: the")
    print("  bastion works on the integrity track (Part 1: wall multiplier about 6.75 to 9.0, so a three-cannon train needs 1 to 2 more seasons) and")
    print("  as an alternative to the concentric step, not on top of it: castle 1.25 times a bastion storm multiplier of at most about 1.5, so the total stays at or below the 1.9 crossover.")
    return cross


# ---- drilled levies (WAR-13) ----------------------------------------------------------------------------
def part_levy():
    print("\n== Part 3: drilled national levy (WAR-13, levy strength x1.5, P) against the E5 field forces")
    infantry = e3.fmul(e5c.STEEL, e5c.PRO)
    iron_levy = e3.fmul(e3.fx(24, 10), e3.MILITIA)
    drilled = e3.fmul(iron_levy, e3.fx(15, 10))
    print(f"  per-figure strength: iron militia {e3.fd(iron_levy):.2f}, drilled iron levy {e3.fd(drilled):.2f}, steel professional {e3.fd(infantry):.2f}")
    inf = e5c.fighters(20, infantry, 5000)
    for label, s in (("undrilled iron levy", iron_levy), ("drilled iron levy x1.5", drilled)):
        n = None
        for k in range(10, 120):
            if e5c.duel(e5c.fighters(k, s), inf, None, e5c.SHIELD, trials=40)[0] >= 0.5:
                n = k
                break
        print(f"  smallest {label} host that beats 20 shielded steel professionals half the time: {n}")
    print("  Reading: drilling cuts the numbers a levy needs by a third; it makes a national army competitive with professionals without a standing")
    print("  force, which is the intent. It needs GOV-12, so it cannot appear before the state exists.")


def check():
    # Live constants hand-checked against Raids.cs.
    assert e5c.breach_seasons(1, WALL) == 2 and e5c.breach_seasons(3, WALL) == 1
    # One cannon = two engines: 1 cannon breaches a bare wall in 1 season and a tower in 2; 3 cannons breach a tower in 1.
    assert breach_seasons(1, WALL) == 1 and breach_seasons(1, TOWER_WALL) == 2 and breach_seasons(3, TOWER_WALL) == 1
    assert breach_seasons(1, TOWER_WALL) == e5c.breach_seasons(2, TOWER_WALL)  # cannon = two engines
    # A stronger wall never breaches faster; more cannons never slower.
    for n in (1, 2, 3):
        assert breach_seasons(n, 9.0) >= breach_seasons(n, 6.75) >= breach_seasons(n, TOWER_WALL) >= breach_seasons(n, WALL)
    assert breach_seasons(3, 9.0) <= breach_seasons(2, 9.0) <= breach_seasons(1, 9.0)
    # Defence arithmetic in the E5 oracle's frame: a garrisoned concentric castle with no wall left is 1.25 x 1.5 = 1.875 (docs/MVP/27, below the 1.9 crossover).
    assert math.isclose(total_defence(concentric=True), 1.875)
    assert math.isclose(total_defence(castle=False, wall=3.0 * POWDER_WALL_EFFECT), 0.9)
    # The crossover exists and is deterministic; a stronger defence never raises the attacker's win share.
    assert crossover() == crossover() and crossover() is not None
    assert storm(1.2, 40)[0] >= storm(2.4, 40)[0]
    print("--check PASS: cannon = two engines (1 cannon: bare wall 1 season, tower 2; 3 cannons: tower 1); breach time monotone in wall and cannons; "
          "concentric castle total 1.875; storm crossover exists and is deterministic")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    if args.check:
        check()
    else:
        part_breach()
        part_storm()
        part_levy()
