#!/usr/bin/env python3
"""Provisional E4 combat and siege fixture oracle; inputs are explicit hypotheses.

Combat outcomes reuse the fixed-point fight/casualty implementation in combat_e3.py.
This is a fixture screen, not calibrated live E4 behavior. Siege breach assumptions
are deliberately exposed below because no barrier-integrity damage rule is fitted.
"""
from pathlib import Path
import argparse
import importlib.util
import math

_spec = importlib.util.spec_from_file_location("combat_e3", Path(__file__).with_name("combat_e3.py"))
e3 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(e3)

SEED = 20261102
ROUNDS = e3.MAX_ROUNDS
SHIELD = e3.fx(7, 10)
IRON = e3.fx(24, 10)
STEEL = e3.fmul(IRON, e3.fx(125, 100))  # fixed-point 2.4 × 1.25 once
PRO_TRAIN = e3.fx(15, 10)  # replaces E3 ordinary ×1.3


def force(n, weapon, training, first_id):
    strength = e3.fmul(weapon, training)
    return [(first_id + i, strength) for i in range(n)]


def number(value):
    return f"{e3.fd(value):.1f}"


def field(a, d, a_shields=False, d_shields=False, stream=0):
    result = e3.fight(a, d, e3.ONE, e3.ONE, False, e3.fx(4, 10), e3.fx(4, 10),
                      ROUNDS, False, SEED, stream,
                      SHIELD if a_shields else None, SHIELD if d_shields else None)
    return result


def engine_ledger(seasons):
    # One make_siege_engine batch. Upstream wrought: 0.5 G-IRON + 0.25 G-CHAR
    # -> 0.4 G-WROUGHT; 0.4 G-WROUGHT requires one forge_wrought batch.
    # E3 bloomery: 2 G-IRONORE + 3 G-CHAR -> 0.5 G-IRON, 0.8 ws.
    wrought_batches = 0.4 / 0.4
    wrought_iron = wrought_batches * 0.5
    wrought_char = wrought_batches * 0.25
    upkeep = 0.2 * seasons
    return {
        "wood": 8.0, "block": 4.0, "wrought": 0.4,
        "upstream_iron": wrought_iron, "upstream_ore": 2.0,
        "forge_char": wrought_char, "bloomery_char": 3.0,
        "total_char": wrought_char + 3.0 + upkeep,
        "wood_for_char": 6 * (wrought_char + 3.0 + upkeep),
        "labor": 24.0 + 0.4 + 0.8 + (wrought_char + 3.0 + upkeep) * 0.4,
        "crew": 4, "crew_seasons": 4 * seasons,
    }


def breach_time(integrity, damage_per_season):
    if integrity <= 0 or damage_per_season <= 0:
        raise ValueError("integrity and damage_per_season must be positive")
    # Damage is applied once per season, with final partial season rounded up.
    return (integrity + damage_per_season - 1e-12) // damage_per_season


def report():
    cases = [
        ("E3 bronze, ordinary training, both shielded", e3.fx(23, 10), e3.TRAIN, True),
        ("E4 wrought iron, professional training replaces ordinary, both shielded", IRON, PRO_TRAIN, True),
        ("E4 tempered steel, professional training, +25% once, both shielded", STEEL, PRO_TRAIN, True),
    ]
    print("== E4 combat fixture oracle (fixed-point fight; equal-size 20 vs 20)")
    print(f"seed={SEED}; rounds={ROUNDS}; break thresholds=40%; shield casualty multiplier=0.70")
    for i, (label, weapon, training, shields) in enumerate(cases):
        print(f"  weapon={number(weapon)} training={number(training)}")
        r = field(force(20, weapon, training, 1000), force(20, e3.fx(23, 10), e3.TRAIN, 5000),
                  shields, True, i)
        print(f"{label}: winner={r['winner']}, rounds={r['rounds']}, "
              f"attacker_survivors={r['a_surv']}, defender_survivors={r['d_surv']}, "
              f"attacker_hurt={len(r['a_dead']) + len(r['a_wounded'])}, "
              f"defender_hurt={len(r['d_dead']) + len(r['d_wounded'])}")

    print("== Provisional siege breach sensitivity")
    print("Assumptions: 1 supplied engine; 4 crew; 0.2 G-CHAR upkeep/season; "
          "integrity and damage are illustrative units, damage applied each season.")
    investment_seasons = 10
    print(f"Matched investment reference={investment_seasons} seasons (declared fixture only); "
          f"catalog intention=0.40× => {0.4 * investment_seasons:.1f} seasons")
    print("integrity damage/season breach_seasons ratio_to_investment engine_char_total")
    for integrity, damage in ((12, 1), (12, 2), (12, 3), (10, 4), (8, 4)):
        duration = breach_time(integrity, damage)
        ledger = engine_ledger(duration)
        print(f"{integrity:9.1f} {damage:17.1f} {duration:14.1f} "
              f"{duration / investment_seasons:19.2f} {ledger['total_char']:19.2f}")
    print("The 0.40× duration occurs only for selected assumptions (e.g. 12 integrity at 3 damage/season "
          "takes 4 seasons vs this declared 10-season investment). This is sensitivity, not a fit/pass. "
          "Investment/breach model calibration remains open.")
    ledger = engine_ledger(4)
    print("engine ledger at 4 breach seasons: " + ", ".join(f"{k}={v:g}" for k, v in ledger.items()))


def check():
    assert STEEL == e3.fmul(IRON, e3.fx(125, 100)), "steel must be iron ×1.25 once"
    assert PRO_TRAIN == e3.fx(15, 10), "professional training must replace ordinary ×1.3"
    l = engine_ledger(1)
    assert l["wood"] == 8 and l["block"] == 4 and l["wrought"] == 0.4
    assert l["upstream_iron"] == 0.5 and l["upstream_ore"] == 2.0
    assert l["forge_char"] == 0.25 and l["bloomery_char"] == 3.0
    assert math.isclose(l["total_char"], 3.45) and math.isclose(l["wood_for_char"], 20.7)
    assert math.isclose(l["labor"], 26.58) and l["crew"] == 4
    print("--check: PASS weapon stacking (2.4 × 1.25 once; professional ×1.5 replaces ×1.3)")
    print("--check: PASS engine ledger (8 wood, 4 block, 0.4 wrought; 0.5 iron from 2 ore + 3 bloomery charcoal; "
          "0.25 forge charcoal; 0.2 charcoal/season upkeep; wood-for-charcoal and recipe labor rolled up; 4 crew)")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="check weapon stacking and engine ledger")
    args = parser.parse_args()
    if args.check:
        check()
    else:
        report()


if __name__ == "__main__":
    main()
