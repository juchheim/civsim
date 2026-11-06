#!/usr/bin/env python3
"""Audit matched E3/E4 food and city observations supplied as CSV.

This is a diagnostic, not an E4 simulator or an I-T4 pass/fail gate. It keeps
the approved proposed band visible in the report without classifying it.
"""
from __future__ import annotations

import argparse
import csv
import math
import sys

FIXTURES = {"non_river", "river", "hill", "inland", "coastal"}
NUMERIC = (
    "e3_food_cap", "e4_food_cap", "e3_city_population", "e3_population",
    "e4_city_population", "e4_population", "begin_food", "produced",
    "imports", "consumed", "exports", "loss", "end_food",
)


def audit(path: str) -> int:
    with open(path, newline="", encoding="utf-8") as stream:
        rows = list(csv.DictReader(stream))
    if not rows:
        raise ValueError("CSV has no observation rows")
    seen: set[str] = set()
    print("I-T4 matched E3-anchor diagnostics (observations only; no pass/fail)")
    print("fixture      FoodCap E4/E3   city share E3->E4   food residual")
    failures = 0
    for row in rows:
        fixture = row["fixture"].strip()
        if fixture not in FIXTURES or fixture in seen:
            raise ValueError(f"invalid or duplicate fixture: {fixture!r}")
        seen.add(fixture)
        values = {key: float(row[key]) for key in NUMERIC}
        if any(not math.isfinite(v) or v < 0 for v in values.values()):
            raise ValueError(f"{fixture}: values must be finite and non-negative")
        if values["e3_food_cap"] == 0 or values["e3_population"] == 0 or values["e4_population"] == 0:
            raise ValueError(f"{fixture}: E3 FoodCap and total populations must be positive")
        ratio = values["e4_food_cap"] / values["e3_food_cap"]
        e3_share = values["e3_city_population"] / values["e3_population"]
        e4_share = values["e4_city_population"] / values["e4_population"]
        # Closed-system mass balance: imports + production add; exports, use,
        # spoilage and remaining stock account for the food present.
        residual = values["begin_food"] + values["produced"] + values["imports"] - values["consumed"] - values["exports"] - values["loss"] - values["end_food"]
        tolerance = max(0.01, 1e-6 * max(values.values()))
        ok = abs(residual) <= tolerance
        failures += not ok
        print(f"{fixture:<12} {ratio:>8.3f}x        {e3_share:>6.1%}->{e4_share:<6.1%}       {residual:>+10.4f} {'ok' if ok else 'MISMATCH'}")
    print(f"Coverage: {len(seen)}/{len(FIXTURES)} required fixture types")
    print("Proposed 1.4–1.8x FoodCap band is unapproved; ratios are reported without classification.")
    print("City share is population in City-tier settlements / all population; compare matched cohorts and report sample size.")
    return int(failures > 0 or seen != FIXTURES)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("csv", help="matched observations CSV; see it4-matched-anchor-template.csv")
    args = parser.parse_args()
    try:
        return audit(args.csv)
    except (OSError, KeyError, ValueError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
