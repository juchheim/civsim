#!/usr/bin/env python3
"""Provisional E5 combat, siege and gunpowder fixture oracle (docs/MVP/27 §5.6, F-T5). Inputs are explicit hypotheses (P).

Fights reuse the fixed-point implementation in combat_e3.py (as e4_combat_oracle.py does). The siege track reuses the live E4
constants read from src/CivSim.Sim/Politics/Raids.cs: integrity 1.0 intact, BreachPerEngine 0.5 per running engine per season,
scaled by 3 / wall multiplier (wall 3.0, towers 4.5), crew 4 per engine, at most 3 engines, engines last 12 seasons.

Nothing here fits live behavior or passes F-T5. It shows which interpretations of the catalog text make which decisions
disappear, so the decision can be made before code.
"""
from __future__ import annotations

import argparse
import importlib.util
import math
from pathlib import Path

HERE = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("combat_e3", HERE / "combat_e3.py")
e3 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(e3)

SEED = 20261105
ROUNDS = e3.MAX_ROUNDS
ONE = e3.ONE
SHIELD = e3.fx(7, 10)            # casualty multiplier for a shielded side
STEEL = e3.fx(3)                 # E4 steel-edge arms, 3.0 (2.4 x 1.25)
PRO = e3.fx(15, 10)              # professional training 1.5 replaces ordinary 1.3
# P: E5 rows
CAV_SHOCK = e3.fx(2)             # WAR-10 'x2 in open battle'
BOLT = e3.fx(20, 10)             # WAR-11 bolt/arrow weapon strength
LEVY = e3.fx(10, 10)             # a levy needs no drilling to use a crossbow: training 1.0
# Siege constants (live E4)
BREACH_PER_ENGINE = 0.5
CREW_PER_ENGINE = 4
MAX_ENGINES = 3
WALL, TOWER_WALL = 3.0, 4.5
CANNON_WALL_EFFECT = 0.30        # WAR-12 'walls -70% effective' read as the wall counting for 30% against powder (reading A)


def fighters(n, strength, first=1000):
    return [(first + i, strength) for i in range(n)]


def duel(a, d, a_cas=None, d_cas=None, ranged_first=False, a_mult=ONE, d_mult=ONE, trials=400):
    aw = dw = so = 0
    a_surv = d_surv = 0
    for s in range(trials):
        r = e3.fight(a, d, a_mult, d_mult, False, e3.fx(4, 10), e3.fx(4, 10), ROUNDS, ranged_first, SEED, s, a_cas, d_cas)
        if r["winner"] == "attacker":
            aw += 1
        elif r["winner"] == "defender":
            dw += 1
        else:
            so += 1
        a_surv += r["a_surv"]
        d_surv += r["d_surv"]
    n = float(trials)
    return aw / n, dw / n, so / n, a_surv / n, d_surv / n


def fmt(label, res, na, nd):
    aw, dw, so, a_s, d_s = res
    print(f"  {label:58s} A wins {100 * aw:5.1f}%  D wins {100 * dw:5.1f}%  stand-off {100 * so:4.1f}%  survivors A {a_s:4.1f}/{na}  D {d_s:4.1f}/{nd}")


def part_fields():
    print("== Part 1: open-field fixtures (400 streams each; seed", SEED, ")")
    infantry = e3.fmul(STEEL, PRO)            # 4.5
    cavalry = e3.fmul(infantry, CAV_SHOCK)    # 9.0
    bolt = e3.fmul(BOLT, LEVY)                # 2.0
    print(f"  per-figure strength: steel professional infantry {e3.fd(infantry):.1f}, heavy cavalry {e3.fd(cavalry):.1f} (open battle only), "
          f"crossbow levy {e3.fd(bolt):.1f} (ranged first round, shields do not help)")
    fmt("20 heavy cav vs 20 steel infantry (shielded)", duel(fighters(20, cavalry), fighters(20, infantry, 5000), None, SHIELD), 20, 20)
    fmt("20 heavy cav vs 40 steel infantry (shielded)", duel(fighters(20, cavalry), fighters(40, infantry, 5000), None, SHIELD), 20, 40)
    fmt("30 crossbow levy vs 20 steel infantry (shield negated)", duel(fighters(30, bolt), fighters(20, infantry, 5000), None, None, ranged_first=True), 30, 20)
    fmt("30 crossbow levy vs 20 steel infantry (if shields applied)", duel(fighters(30, bolt), fighters(20, infantry, 5000), None, SHIELD, ranged_first=True), 30, 20)
    fmt("20 steel infantry + 10 crossbow vs 20 steel infantry", duel(fighters(20, infantry) + fighters(10, bolt, 2000), fighters(20, infantry, 5000), None, SHIELD, ranged_first=True), 30, 20)
    fmt("40 crossbow levy vs 20 heavy cav (cav attacks, levy first volley)", duel(fighters(20, cavalry), fighters(40, bolt, 5000), None, None), 20, 40)
    # The fights are close to deterministic (strength totals decide), so the useful output is the crossover, not one lopsided case.
    def first_win(make_attackers, defenders, d_cas, ranged_first, lo, hi, a_cas=None):
        for n in range(lo, hi + 1):
            if duel(make_attackers(n), defenders, a_cas, d_cas, ranged_first=ranged_first, trials=60)[0] >= 0.5:
                return n
        return None

    inf40 = fighters(40, infantry, 5000)
    n_cav = first_win(lambda n: fighters(n, cavalry), inf40, SHIELD, False, 5, 60)
    inf20 = fighters(20, infantry, 5000)
    n_bolt_neg = first_win(lambda n: fighters(n, bolt), inf20, None, True, 5, 80)
    n_bolt_sh = first_win(lambda n: fighters(n, bolt), inf20, SHIELD, True, 5, 80)
    print(f"  crossover (smallest attacker count that wins half the time): heavy cavalry vs 40 shielded infantry: {n_cav}; "
          f"crossbow levy vs 20 shielded-steel infantry: {n_bolt_neg} with shields negated, {n_bolt_sh} if shields still applied")
    print("  Reading: fights are decided by total strength. A heavy cavalryman is worth two steel infantry (the catalog's x2), and shields")
    print("  (casualties x0.7) mean the attacker needs about 1.4x the defender's total strength. Crossbow levies of low strength win")
    print("  with 1.4x the pros' numbers when bolts ignore shields and 1.55x if shields still counted, so 'pierce armor' is worth only")
    print("  about a tenth in numbers here. Cavalry must stay field-only (no wall assaults).")


def breach_seasons(engines, wall, per_engine=BREACH_PER_ENGINE, effect=1.0):
    """Seasons to take integrity 1.0 to zero. `effect` < 1 reduces the wall's resistance (cannon reading A)."""
    scale = 3.0 / (wall * effect)
    per_season = per_engine * engines * scale
    return math.ceil(1.0 / per_season - 1e-9)


def part_siege():
    print("\n== Part 2: breach time on the live integrity track (seasons to breach; engines run if supplied and crewed)")
    print("  reading A: cannon treats the wall as 30% effective (catalog '-70%'); reading B: a cannon does twice an engine's damage")
    print(f"  {'pieces':>6s} | {'engines':>14s} | {'cannon, reading A':>20s} | {'cannon, reading B':>20s}")
    print(f"  {'':>6s} | {'wall 3.0 / tower 4.5':>14s} | {'wall 3.0 / tower 4.5':>20s} | {'wall 3.0 / tower 4.5':>20s}")
    for n in (1, 2, 3):
        e = (breach_seasons(n, WALL), breach_seasons(n, TOWER_WALL))
        a = (breach_seasons(n, WALL, effect=CANNON_WALL_EFFECT), breach_seasons(n, TOWER_WALL, effect=CANNON_WALL_EFFECT))
        b = (breach_seasons(n, WALL, per_engine=2 * BREACH_PER_ENGINE), breach_seasons(n, TOWER_WALL, per_engine=2 * BREACH_PER_ENGINE))
        print(f"  {n:6d} | {e[0]:>6d} / {e[1]:<6d} | {a[0]:>9d} / {a[1]:<8d} | {b[0]:>9d} / {b[1]:<8d}")
    print("  Reading A makes any single cannon a one-season breach even against towers; reading B keeps a season of delay per tower.")
    print("  Recommendation for the spec: reading B-like pacing (cannon = 2 engine-equivalents), with the '-70%' kept as the wall's")
    print("  contribution to the post-breach storm, so powder shortens a siege without deleting the decision.")


def part_storm():
    print("\n== Part 3: the storm after a breach vs a garrisoned castle (DEF-09 'castles x2 defense'); 400 streams")
    infantry = e3.fmul(STEEL, PRO)
    iron_levy = e3.fmul(e3.fx(24, 10), e3.MILITIA)   # iron-armed militia 2.4 x 0.6
    attackers = fighters(36, infantry)                # a host that can crew three cannon (12) and still storm with 24+
    defenders = fighters(12, infantry, 5000) + fighters(20, iron_levy, 6000)
    print("  36 steel professionals storm 12 steel professionals + 20 iron militia; wall already breached; defenders' multiplier c:")
    for label, c in (("no castle", ONE), ("E4 garrisoned castle x1.25", e3.fx(125, 100))):
        fmt(f"c = {label}", duel(attackers, defenders, None, None, d_mult=c), 36, 32)
    cross = None
    for tenths in range(13, 31):
        c = e3.fx(tenths, 10)
        res = duel(attackers, defenders, None, None, d_mult=c, trials=100)
        if cross is None and res[0] < 0.5:
            cross = tenths / 10
    fmt("c = 1.5", duel(attackers, defenders, None, None, d_mult=e3.fx(15, 10)), 36, 32)
    fmt("c = 2.5 (1.25 x 2)", duel(attackers, defenders, None, None, d_mult=e3.fx(25, 10)), 36, 32)
    print(f"  The storm flips from attacker wins to defender wins at c = {cross}. So 'castles x2' must land below that to leave the")
    print("  storm a real option; a reading of 1.25 x 2 = 2.5 closes it and returns the contest to investment and starvation.")
    return cross


# ---- Gunpowder ledger and saltpeter access -------------------------------------------------------
POWDER = dict(saltpeter=0.75, sulfur=0.10, charcoal=0.15, powder=1.0, ws=0.4)   # P: one batch
CANNON = dict(cast_iron_l=2.0, wood=6.0, ws=30.0, crew=4, powder_per_season=0.25)  # P
SALTPETER_PER_1000, SULFUR_PER_1000 = 1.5, 1.0  # content/registries/deposits.yaml saltpeter_sulfur


def access_probability(land_tiles, per_1000):
    return 1.0 - math.exp(-per_1000 * land_tiles / 1000.0)


def part_powder(bf_ratio=0.5):
    print("\n== Part 4: powder and cannon ledgers (P)")
    iron_char = CANNON["cast_iron_l"] * (e3.fd(e3.fx(1)) * 6.0) * bf_ratio
    cannon_wood = iron_char * 6.0 + CANNON["wood"]
    print(f"  cannon: {CANNON['cast_iron_l']:g} L cast iron (charcoal {iron_char:.1f} at furnace x{bf_ratio:g}, i.e. {iron_char * 6:.0f} L wood), "
          f"{CANNON['wood']:g} wood carriage, {CANNON['ws']:g} ws; {CANNON['crew']} crew; {CANNON['powder_per_season']:g} powder per firing season")
    print(f"  engine for comparison (e4_combat_oracle): 8 wood, 4 block, 0.4 wrought, 20.7 L wood in charcoal; cannon total wood ~{cannon_wood:.0f} L")
    season_powder = 2 * CANNON["powder_per_season"] * 4
    print(f"  a 4-season siege with 2 cannon burns {season_powder:g} powder = {season_powder * POWDER['saltpeter']:.1f} saltpeter, "
          f"{season_powder * POWDER['sulfur']:.1f} sulfur, {season_powder * POWDER['charcoal'] * 6:.1f} L wood in charcoal")
    print("  Own saltpeter / sulfur in territory (deposit rates per 1,000 land tiles: saltpeter 1.5, sulfur 1.0; WP-E5-3 audits real maps):")
    print(f"  {'territory (land tiles)':>24s} {'P(saltpeter)':>13s} {'P(sulfur)':>11s} {'P(both)':>9s}")
    for t in (100, 300, 600, 1000, 1600):
        ps, pu = access_probability(t, SALTPETER_PER_1000), access_probability(t, SULFUR_PER_1000)
        print(f"  {t:24d} {ps:13.0%} {pu:11.0%} {ps * pu:9.0%}")
    print("  A civilization with ~300 tiles has an own supply a third of the time; powder depends on trade or expansion for most starts.")


def check():
    infantry = e3.fmul(STEEL, PRO)
    assert infantry == e3.fx(45, 10)
    assert e3.fmul(infantry, CAV_SHOCK) == e3.fx(90, 10)
    # Live E4 siege fixtures: one engine, bare wall 2 seasons; tower 3 seasons (the E4 test note); three engines 1 season.
    assert breach_seasons(1, WALL) == 2 and breach_seasons(1, TOWER_WALL) == 3 and breach_seasons(3, WALL) == 1
    # Reading A: a single cannon breaches in one season; reading B: one cannon vs tower takes 2.
    assert breach_seasons(1, TOWER_WALL, effect=CANNON_WALL_EFFECT) == 1
    assert breach_seasons(1, TOWER_WALL, per_engine=1.0) == 2
    assert math.isclose(POWDER["saltpeter"] + POWDER["sulfur"] + POWDER["charcoal"], POWDER["powder"])
    assert math.isclose(access_probability(1000, 1.5), 1 - math.exp(-1.5)) and access_probability(0, 1.5) == 0.0
    assert access_probability(600, 1.5) > access_probability(300, 1.5)
    # Determinism: the same duel twice gives the same numbers.
    a, d = fighters(10, infantry), fighters(10, infantry, 5000)
    assert duel(a, d, None, SHIELD, trials=20) == duel(a, d, None, SHIELD, trials=20)
    # A shielded defender takes fewer casualties than an unshielded one against the same attackers.
    s = duel(a, d, None, SHIELD, trials=60)
    u = duel(a, d, None, None, trials=60)
    assert s[4] >= u[4], (s, u)
    print("--check PASS: weapon stack 4.5 / 9.0; live E4 breach fixtures (2 / 3 / 1 seasons); cannon readings A and B; powder recipe sums to 1; "
          "saltpeter access formula; duels deterministic and shields help")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    if args.check:
        check()
    else:
        part_fields()
        part_siege()
        part_storm()
        part_powder()
