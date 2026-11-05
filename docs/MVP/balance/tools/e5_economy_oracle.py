#!/usr/bin/env python3
"""Provisional E5 food, claim-upkeep, epidemic and staffing fixture screens (docs/MVP/27 §5.3-§5.5, F-T4, F-T6, F-T7).

Fixture arithmetic with explicit inputs; nothing here is fitted to live E5 (none exists) or passes a target. P marks a
hypothesis. Epidemic arithmetic follows 09 §10.2-§10.3 and is checked against the 09 §10.6 E5 row (3.0 per mille per year).
"""
from __future__ import annotations

import argparse
import importlib.util
import math
from pathlib import Path

HERE = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("e5_power_oracle", HERE / "e5_power_oracle.py")
pw = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pw)


# ---- A. Three-field rotation, breeding and the food multiplier --------------------------------------------
TWO_FIELD_CROPPED = 1 / 2      # fallow every other year (the implied baseline)
THREE_FIELD_CROPPED = 2 / 3    # one field in three fallow
LEGUME_YIELD = 1.125           # P: nitrogen from the legume year lifts the cropped yield
HERD_BREEDING = 1.5            # HUS-10
THREE_FIELD_TARGET = 1.5       # AGR-11 'Land productivity +50%'


def three_field_gain():
    return THREE_FIELD_CROPPED / TWO_FIELD_CROPPED * LEGUME_YIELD


def food_multiplier(field_share, herd_share, rotated_share=1.0, bred_share=1.0):
    """Food-cap multiplier on a settlement whose food is field_share grain, herd_share meat/milk, rest other."""
    other = 1.0 - field_share - herd_share
    return (field_share * (1 + (three_field_gain() - 1) * rotated_share)
            + herd_share * (1 + (HERD_BREEDING - 1) * bred_share) + other)


def part_food():
    print("== Part A: three-field rotation and breeding (physical decomposition, P)")
    print(f"  cropped share {TWO_FIELD_CROPPED:.3f} -> {THREE_FIELD_CROPPED:.3f} is x{THREE_FIELD_CROPPED / TWO_FIELD_CROPPED:.3f}; "
          f"with legume yield x{LEGUME_YIELD} the tile gain is x{three_field_gain():.3f} (catalog +50% = x{THREE_FIELD_TARGET})")
    print("  Food-cap multiplier by food mix (all fields rotated, all herds bred):")
    print(f"  {'field':>6s} {'herd':>6s} {'other':>6s} | multiplier | only rotated half the fields")
    for f, h in ((0.8, 0.1), (0.6, 0.15), (0.4, 0.2), (0.2, 0.2)):
        print(f"  {f:6.0%} {h:6.0%} {1 - f - h:6.0%} | {food_multiplier(f, h):10.3f} | {food_multiplier(f, h, 0.5):.3f}")
    print("  Proposed sanity band for the E5 food step: 1.2 to 1.6 (P; I-T4's E4 step of 1.4 to 1.8 included water and cities, so a purely")
    print("  agricultural step should be smaller).")
    # Horse collar frees ploughing labor where horses exist (draft x2).
    plough_share = 0.10
    print(f"  Horse collar (draft x2) on a ploughing share of {plough_share:.0%} of adult work (P) frees {plough_share / 2:.1%} of adult labor, horses permitting.")


# ---- B. Claim upkeep for TER-10 (confirmed D4) -------------------------------------------------------------
TILE_YIELD_PER_YEAR = {1.0: 0.25, 2.0: 0.50, 3.0: 0.25}   # P: terrace 2.0 and prime clay 3.0 (Iron.cs); poor tile 1.0
SURPLUS_RATIO = 1.3                                        # P: a farming household produces 1.3x what it eats
TER10_REDUCTION = 0.30


def mean_yield():
    return sum(y * w for y, w in TILE_YIELD_PER_YEAR.items())


def fed_non_farmers(upkeep_share):
    """If upkeep (a share of farm output) is delivered as grain to those who do not farm, what share of all people it feeds:
    N/F = u * surplus; share of people = N/(N+F)."""
    ratio = upkeep_share * SURPLUS_RATIO
    return ratio / (1 + ratio)


def part_upkeep():
    print("\n== Part B: claim upkeep on held farm tiles (D4: Fief mode only; the grain feeds the non-farming staff, so mass is conserved)")
    print(f"  mean tile yield {mean_yield():.2f}/year (tiles 1.0/2.0/3.0 at 25/50/25%, P); surplus ratio {SURPLUS_RATIO:g} (P)")
    print(f"  {'upkeep share':>13s} {'with TER-10':>12s} | people it can feed (before / after TER-10) | grain per 100 tiles per year (before / after)")
    for u in (0.04, 0.06, 0.08, 0.10, 0.12):
        after = u * (1 - TER10_REDUCTION)
        print(f"  {u:13.0%} {after:12.1%} | {fed_non_farmers(u):17.1%} / {fed_non_farmers(after):<17.1%} | "
              f"{u * mean_yield() * 100:8.1f} / {after * mean_yield() * 100:<8.1f}")
    print("  Reading: TER-10 is a 30% cut of whatever the upkeep is, so it is worth only 1.2 to 3.6 points of farm output across this range.")
    print("  A first fit of about 10% makes the project worth ~3 points of output (about 3% of a town's people kept off the fields),")
    print("  and the same grain is what pays the staff the new institutions need (Part D). The size is for the oracle and live runs to fit.")


# ---- C. Epidemics (09 §10.2-§10.3) -------------------------------------------------------------------------
# class: (R0, case fatality, immunity share assumed P, weight at E5 = mean of E4 and E6 weights in 09 §10.2)
CLASSES = {
    "fever": (1.5, 0.03, 0.20, (0.35 + 0.30) / 2),
    "flux": (2.0, 0.08, 0.20, 0.30),
    "pox": (4.0, 0.15, 0.30, (0.25 + 0.25) / 2),
    "plague": (2.0, 0.35, 0.00, (0.10 + 0.15) / 2),
}
SANITATION_E4_E6 = 1 - 0.58   # 09 §10.1: '1 - Sanitation runs 0.58 (E4-E6)' applies to flux R0
AGE_FACTOR = 1 / 1.25         # P: deaths fall 1.5x on children and 2x on elders relative to adults; blended over the age mix


def final_size(r_eff, iters=200):
    if r_eff <= 1:
        return 0.0
    z = 0.5
    for _ in range(iters):
        z = 1 - math.exp(-r_eff * z)
    return z


def epidemic_per_mille(pressure=1.1, hospital=False, quarantine=False, med_base=0.10):
    p_season = min(0.25, 0.010 * pressure) * (0.75 if quarantine else 1.0)
    per_year = 4 * p_season
    med = (1 - med_base) * (0.80 if hospital else 1.0)   # MED-01 -10% always known by E5; MED-07 hospitals -20%
    deaths_fraction = 0.0
    for name, (r0, cfr, immune, w) in CLASSES.items():
        if name == "flux":
            r0 = r0 * (1 - SANITATION_E4_E6)
        r_eff = r0 * (1 - immune) * min(1.5, pressure) ** 0.5
        deaths_fraction += w * final_size(r_eff) * cfr * med * AGE_FACTOR
    return 1000 * per_year * deaths_fraction


def part_epidemic():
    print("\n== Part C: epidemic deaths per 1,000 people per year at E5 (09 §10.2-§10.3; weights = mean of E4 and E6 rows)")
    base = epidemic_per_mille()
    print(f"  baseline at pressure 1.1: {base:.2f} per mille per year (09 §10.6 expects 3.0 for E5)")
    print(f"  {'pressure':>8s} | {'none':>6s} | {'hospital':>8s} | {'quarantine':>10s} | {'both':>6s}")
    for p in (0.8, 1.1, 1.5, 2.0):
        print(f"  {p:8.1f} | {epidemic_per_mille(p):6.2f} | {epidemic_per_mille(p, hospital=True):8.2f} | "
              f"{epidemic_per_mille(p, quarantine=True):10.2f} | {epidemic_per_mille(p, hospital=True, quarantine=True):6.2f}")
    both = epidemic_per_mille(1.1, True, True)
    print(f"  hospitals and quarantine together cut the baseline by {100 * (1 - both / base):.0f}% (hospital -20% of deaths, quarantine x0.75 outbreak rolls;")
    print("  quarantine's halving of spread between settlements is a second effect not shown here).")
    print(f"  The fixture lands {100 * (1 - base / 3.0):.0f}% under 09 §10.6's 3.0 (inside the check band, not equal): the age factor and immunity shares are P and")
    print("  should be fitted against live attributed deaths before this table is used as a target.")


# ---- D. Staffing: can the new institutions be manned? ----------------------------------------------------------
STAFF_PER_100_ADULTS = {   # P
    "soldiers (WAR-07 slots 10% of adults)": 10.0,
    "monks (staffed monasteries)": 3.0,
    "scholars (academies, universities)": 2.0,
    "hospital and quarantine staff": 1.0,
    "millers and mill hands": 1.5,
}


def part_staffing():
    print("\n== Part D: labor pool for E5 institutions (per 100 adults, P)")
    freed_machines = sum(s * (1 - 1 / m) for s, m, _ in pw.JOBS.values()) + sum(s * (1 - 1 / m) for s, m, _ in pw.TEXTILE.values())
    freed_collar = 0.10 / 2
    pool = 100 * (freed_machines + freed_collar)
    demand = sum(STAFF_PER_100_ADULTS.values())
    for k, v in STAFF_PER_100_ADULTS.items():
        print(f"  {k:42s} {v:5.1f}")
    print(f"  demand at a 10% army: {demand:.1f}; at the 20% WAR-08 ceiling: {demand + 10:.1f}")
    print(f"  labor freed by E5 machines and the horse collar (Part A of the power oracle, full coverage): {pool:.1f}")
    print(f"  Reading: at a 10% army the new institutions fit with almost no slack even at full machine coverage ({pool:.1f} freed against")
    print(f"  {demand:.1f} wanted); a war footing at 20% soldiers does not fit, so staffing, not research, limits an E5 civilization. This is")
    print("  the F-T6 'idle institution' risk in numbers: build order must follow the pool, or monasteries and hospitals sit empty.")


def check():
    # A: decomposition reproduces the catalog +50% exactly.
    assert math.isclose(three_field_gain(), 1.5), three_field_gain()
    assert math.isclose(food_multiplier(0.6, 0.15), 1 + 0.6 * 0.5 + 0.15 * 0.5)
    assert math.isclose(food_multiplier(0.6, 0.15, rotated_share=0.0, bred_share=0.0), 1.0)
    # B: upkeep arithmetic: 10% at surplus 1.3 feeds 0.13/(1.13) of people; TER-10 gives 7%.
    assert math.isclose(fed_non_farmers(0.10), 0.13 / 1.13)
    assert math.isclose(mean_yield(), 2.0)
    # C: final size solves z = 1 - exp(-R z); below R=1 no outbreak; hospitals and quarantine reduce deaths.
    z = final_size(2.0)
    assert math.isclose(z, 1 - math.exp(-2.0 * z), abs_tol=1e-9) and final_size(1.0) == 0.0
    base = epidemic_per_mille()
    assert 2.0 <= base <= 4.0, base          # within the 09 §10.6 band around 3.0
    assert epidemic_per_mille(1.1, hospital=True) < base
    assert epidemic_per_mille(1.1, quarantine=True) < base
    assert math.isclose(sum(c[3] for c in CLASSES.values()), 1.0)
    # D: pool is positive and below demand at a 20% army.
    freed = sum(s * (1 - 1 / m) for s, m, _ in pw.JOBS.values()) + sum(s * (1 - 1 / m) for s, m, _ in pw.TEXTILE.values()) + 0.05
    assert 100 * freed < sum(STAFF_PER_100_ADULTS.values()) + 10
    print(f"--check PASS: three-field decomposition = x1.5; upkeep arithmetic; epidemic baseline {base:.2f} per mille inside 2 to 4 (09 §10.6: 3.0); staffing pool below war-footing demand")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    if args.check:
        check()
    else:
        part_food()
        part_upkeep()
        part_epidemic()
        part_staffing()
