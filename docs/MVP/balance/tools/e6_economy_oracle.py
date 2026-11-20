#!/usr/bin/env python3
"""Provisional E6 food, epidemic, migration and staffing fixture screens (docs/MVP/38 §5.5, §5.7, D-T8, D-T9).

Fixture arithmetic with explicit inputs; nothing here is fitted to live E6 (none exists) or passes a target. P marks a hypothesis. Epidemic arithmetic
follows 09 §10.2-§10.3 with the E6 era weights (fever 0.30, flux 0.30, pox 0.25, plague 0.15) and is checked against the 09 §10.6 E6 row (3.8 per mille).
"""
from __future__ import annotations

import argparse
import importlib.util
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
_spec = importlib.util.spec_from_file_location("e5_economy_oracle", HERE / "e5_economy_oracle.py")
e5e = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(e5e)
pw = e5e.pw


# ---- A. Four-course rotation: what the +50% can physically be ------------------------------------------------
THREE_FIELD_CROPPED = 2 / 3     # AGR-11: one strip in three fallow
LEGUME_YIELD = 1.125            # AGR-11 (P)
THREE_FIELD_GRAIN = THREE_FIELD_CROPPED * LEGUME_YIELD   # grain per unit land, relative to a fully cropped, unrotated strip
FOUR_COURSE_GRAIN_SHARE = 1 / 2          # wheat + barley of a wheat, turnips, barley, clover cycle
FOUR_COURSE_TARGET = 1.5                 # AGR-12 'land productivity +50%' (typed as field_output x1.5)


def four_course_food(manure_yield, fodder_energy, conversion, grain_share=FOUR_COURSE_GRAIN_SHARE):
    """Food per unit land relative to the three-field year. Grain from the cropped half (with manure), plus livestock fed from the fodder half:
    fodder_energy = food-energy units of fodder per unit land; conversion = share of that energy that becomes meat, milk and wool (P)."""
    grain = grain_share * manure_yield
    livestock = (1 - grain_share) * fodder_energy * conversion
    return (grain + livestock) / THREE_FIELD_GRAIN


def part_food():
    print("== Part A: four-course rotation (AGR-12) decomposed against the three-field baseline (P)")
    print(f"  three-field grain per unit land {THREE_FIELD_GRAIN:.3f} (cropped {THREE_FIELD_CROPPED:.3f} x legume {LEGUME_YIELD}); catalog target x{FOUR_COURSE_TARGET}")
    print("  A four-course cycle crops grain on only half the land; the other half is turnips and clover. The gain therefore has to come from")
    print("  manure raising grain yields and from livestock fed on the fodder (HUS-11), not from a larger cropped share.")
    print(f"  {'manure yield':>12s} {'fodder energy':>14s} | " + " | ".join(f"conversion {c:.2f}" for c in (0.10, 0.15, 0.20, 0.30)))
    for my in (1.125, 1.25, 1.4):
        for fe in (1.0, 2.0, 3.0):
            print(f"  {my:12.3f} {fe:14.1f} | " + " | ".join(f"{four_course_food(my, fe, c):15.3f}" for c in (0.10, 0.15, 0.20, 0.30)))
    need = None
    for c in range(1, 200):
        if four_course_food(1.25, 2.0, c / 100) >= FOUR_COURSE_TARGET:
            need = c / 100
            break
    print(f"  With manure x1.25 and fodder worth 2 units of food energy per unit land, x{FOUR_COURSE_TARGET} needs a conversion of {need} (meat, milk and wool")
    print("  per unit of fodder energy). Real livestock convert about 0.1 to 0.2. So the physical reading gives about x0.9 to x1.3 over three-field,")
    print("  not x1.5. Recommendation: either keep x1.5 as the target of the whole bundle (AGR-12 + HUS-11 + enclosure), or type AGR-12 at about x1.2")
    print("  on field output and let HUS-11's stall-fed herds supply the rest; do not stack a free x1.5 on the AGR-11 result.")
    # What a x1.5 free multiplier on top of AGR-11 would mean against the original two-field baseline.
    two_field = 1 / 2
    print(f"  A free x1.5 on top of AGR-11 would be x{THREE_FIELD_GRAIN / two_field * 1.5:.2f} over the two-field baseline in grain per unit land, which no cropped-share or manure arithmetic here reaches.")


# ---- B. Epidemics at E6 ---------------------------------------------------------------------------------------
CLASSES_E6 = {   # R0, case fatality, immunity share (P), E6 weight from 09 §10.2
    "fever": (1.5, 0.03, 0.20, 0.30),
    "flux": (2.0, 0.08, 0.20, 0.30),
    "pox": (4.0, 0.15, 0.30, 0.25),
    "plague": (2.0, 0.35, 0.00, 0.15),
}


def epidemic_per_mille(pressure=1.1, hospital=False, quarantine=False, med_base=0.10, med09=False):
    p_season = min(0.25, 0.014 * pressure) * (0.75 if quarantine else 1.0)    # E6 outbreak rate 0.056 a year vs E5's 0.044: P 0.014 per season
    per_year = 4 * p_season
    med = (1 - med_base) * (0.80 if hospital else 1.0) * (0.95 if med09 else 1.0)  # MED-09 anatomy: surgery +30% read as -5% deaths (P)
    deaths = 0.0
    for name, (r0, cfr, immune, w) in CLASSES_E6.items():
        if name == "flux":
            r0 = r0 * (1 - e5e.SANITATION_E4_E6)
        r_eff = r0 * (1 - immune) * min(1.5, pressure) ** 0.5
        deaths += w * e5e.final_size(r_eff) * cfr * med * e5e.AGE_FACTOR
    return 1000 * per_year * deaths


def part_epidemic():
    print("\n== Part B: epidemic deaths per 1,000 people per year at E6 (09 §10.2-§10.3, E6 weights; 09 §10.6 expects 3.8)")
    base = epidemic_per_mille()
    print(f"  baseline at pressure 1.1: {base:.2f} per mille per year ({100 * (1 - base / 3.8):.0f}% under 3.8; the age factor, immunity and outbreak rate are P)")
    print(f"  {'pressure':>8s} | {'none':>6s} | {'hospital':>8s} | {'quarantine':>10s} | {'both':>6s} | both + MED-09")
    for p in (0.8, 1.1, 1.5, 2.0, 3.0):
        print(f"  {p:8.1f} | {epidemic_per_mille(p):6.2f} | {epidemic_per_mille(p, hospital=True):8.2f} | "
              f"{epidemic_per_mille(p, quarantine=True):10.2f} | {epidemic_per_mille(p, True, True):6.2f} | {epidemic_per_mille(p, True, True, med09=True):.2f}")
    print("  Pressure is a town's crowding and route traffic: factories (MEC-10) and ocean ports raise it. At pressure 3, a town with neither hospital")
    print("  nor quarantine loses about three times the baseline, which is the pull that makes MED-07/08 and MED-09 a real choice in E6.")
    print("  Columbian Exchange (FOR-10) carries old-world disease overseas; that is a separate imported-outbreak rule for the colony system.")


# ---- C. Urban migration and the labor pool ----------------------------------------------------------------------
STAFF_PER_100_ADULTS = {   # P
    "soldiers (10% of adults)": 10.0,
    "monks, scholars, hospital, mills (E5 institutions)": 7.5,
    "engineers and coal miners (38 power oracle)": 2.0,
    "navigators and fleets (ocean ships)": 1.0,
    "factory hands (MEC-10 workforce)": 15.0,
}


def part_labor():
    print("\n== Part C: labor pool for E6 institutions (per 100 adults, P)")
    freed_machines = sum(s * (1 - 1 / m) for s, m, _ in pw.JOBS.values()) + sum(s * (1 - 1 / m) for s, m, _ in pw.TEXTILE.values()) + 0.05
    farm_share = 0.50                       # P: share of adults on the land before enclosure
    four_course_release = farm_share * (1 - 0.85)   # AGR-12 typed farm_labor_per_output x0.85: the freed adults move to town work
    pool = 100 * (freed_machines + four_course_release)
    demand = sum(STAFF_PER_100_ADULTS.values())
    for k, v in STAFF_PER_100_ADULTS.items():
        print(f"  {k:52s} {v:5.1f}")
    print(f"  total demand {demand:.1f}; labor freed by E5 machines and collar {100 * freed_machines:.1f}, plus AGR-12 enclosure release {100 * four_course_release:.1f} = {pool:.1f}")
    print(f"  Reading: even with enclosure releasing farm labor the pool ({pool:.1f}) is short of the demand ({demand:.1f}) by {demand - pool:.1f} per 100 adults, so the")
    print("  factory workforce cannot be filled by freed labor alone: it must draw from towns' growth (urban migration, MEC-10) or from soldiers.")
    print("  This is the D-T8 'idle institution' risk in numbers: a factory with no workers is idle and shown so, and build order must follow the pool.")


def check():
    # A: three-field grain per unit land is 2/3 x 1.125 = 0.75; the four-course formula reproduces it when it is configured as three-field-like.
    assert math.isclose(THREE_FIELD_GRAIN, 0.75)
    assert math.isclose(four_course_food(1.125, 0.0, 0.0, grain_share=THREE_FIELD_CROPPED), 1.0)
    # More manure, more fodder energy and better conversion each raise the result; the free x1.5 stack is far above the physical grid.
    assert four_course_food(1.4, 3.0, 0.3) > four_course_food(1.25, 2.0, 0.15) > four_course_food(1.125, 1.0, 0.10)
    assert four_course_food(1.25, 2.0, 0.15) < FOUR_COURSE_TARGET
    # B: E6 weights sum to one; E6 baseline is positive and higher than the E5 fixture's, closes in on 09's 3.8; hospital, quarantine and MED-09 reduce it.
    assert math.isclose(sum(c[3] for c in CLASSES_E6.values()), 1.0)
    base = epidemic_per_mille()
    assert 2.0 <= base <= 5.0, base
    assert base > e5e.epidemic_per_mille(), "E6 carries more epidemic weight and a higher outbreak rate than E5"
    assert epidemic_per_mille(1.1, True, True, med09=True) < epidemic_per_mille(1.1, True, True) < epidemic_per_mille(1.1, True) < base
    assert epidemic_per_mille(3.0) > epidemic_per_mille(1.1)
    # C: demand exceeds the freed pool.
    freed = sum(s * (1 - 1 / m) for s, m, _ in pw.JOBS.values()) + sum(s * (1 - 1 / m) for s, m, _ in pw.TEXTILE.values()) + 0.05 + 0.5 * 0.15
    assert 100 * freed < sum(STAFF_PER_100_ADULTS.values())
    print(f"--check PASS: three-field grain 0.75; four-course formula reduces to 1.0 at the three-field mix and the physical grid stays under x1.5; "
          f"E6 epidemic baseline {base:.2f} per mille (09 §10.6: 3.8) above E5's; labor demand exceeds the freed pool")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    if args.check:
        check()
    else:
        part_food()
        part_epidemic()
        part_labor()
