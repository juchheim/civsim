#!/usr/bin/env python3
"""Small, deterministic E4 city-cap and public-grain fixture screen.

This is target/fixture arithmetic, not a fit to live E4 outcomes (I-T4 remains
unproven).  It intentionally models food, water, and housing capacity as
separate constraints; neither infrastructure nor grain creates food.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from math import isclose


@dataclass(frozen=True)
class Ledger:
    """Physical account ledger: begin + production/import - outflows = end."""

    begin: float
    production_import: float = 0.0
    consumption_export: float = 0.0
    loss: float = 0.0

    @property
    def ending(self) -> float:
        return self.begin + self.production_import - self.consumption_export - self.loss


@dataclass(frozen=True)
class Fixture:
    name: str
    aqueduct: bool
    public_grain: bool


@dataclass(frozen=True)
class Result:
    fixture: Fixture
    food_cap: float
    water_cap: float
    housing_cap: float
    effective_cap: float
    donor_grain: Ledger
    city_grain: Ledger
    donor_reserve: float
    transfer: float
    aqueduct_disease_factor: float
    bathhouse_disease_factor: float


# Explicit anchor inputs (people-equivalent capacity and grain units/season).
# They are intentionally parameterized fixture values, not inferred live data.
BASE_FOOD_CAP = 100.0
BASE_WATER_CAP = 80.0
BASE_HOUSING_CAP = 120.0
BASE_CITY_GRAIN = 12.0
DONOR_BEGIN_GRAIN = 50.0
DONOR_PRODUCTION = 4.0
DONOR_CONSUMPTION = 10.0
WINTER_RESERVE = 30.0
CITY_GRAIN_CONSUMPTION = 10.0
CITY_GRAIN_LOSS = 1.0

FIXTURES = (
    Fixture("no-E4", False, False),
    Fixture("aqueduct-only", True, False),
    Fixture("grain-only", False, True),
    Fixture("aqueduct+grain", True, True),
)


def simulate(fixture: Fixture, bathhouse: bool = False) -> Result:
    water_cap = BASE_WATER_CAP * (1.40 if fixture.aqueduct else 1.0)
    food_cap = BASE_FOOD_CAP * (1.25 if fixture.public_grain else 1.0)

    # Public grain transfers only donor stock above the post-season reserve.
    donor_before_transfer = DONOR_BEGIN_GRAIN + DONOR_PRODUCTION - DONOR_CONSUMPTION
    transfer = min(20.0, max(0.0, donor_before_transfer - WINTER_RESERVE)) if fixture.public_grain else 0.0
    donor = Ledger(
        begin=DONOR_BEGIN_GRAIN,
        production_import=DONOR_PRODUCTION,
        consumption_export=DONOR_CONSUMPTION + transfer,
    )
    city = Ledger(
        begin=BASE_CITY_GRAIN,
        production_import=transfer,
        consumption_export=CITY_GRAIN_CONSUMPTION,
        loss=CITY_GRAIN_LOSS,
    )

    effective_cap = min(food_cap, water_cap, BASE_HOUSING_CAP)
    return Result(
        fixture=fixture,
        food_cap=food_cap,
        water_cap=water_cap,
        housing_cap=BASE_HOUSING_CAP,
        effective_cap=effective_cap,
        donor_grain=donor,
        city_grain=city,
        donor_reserve=WINTER_RESERVE,
        transfer=transfer,
        aqueduct_disease_factor=0.85 if fixture.aqueduct else 1.0,
        bathhouse_disease_factor=0.75 if bathhouse else 1.0,
    )


def check() -> None:
    results = [simulate(fixture) for fixture in FIXTURES]
    expected = {"no-E4": 80.0, "aqueduct-only": 100.0, "grain-only": 80.0, "aqueduct+grain": 112.0}
    for result in results:
        assert isclose(result.effective_cap, expected[result.fixture.name])
        assert result.donor_grain.ending >= result.donor_reserve
        assert isclose(result.donor_grain.ending, DONOR_BEGIN_GRAIN + DONOR_PRODUCTION - DONOR_CONSUMPTION - result.transfer)
        assert isclose(result.city_grain.ending, BASE_CITY_GRAIN + result.transfer - CITY_GRAIN_CONSUMPTION - CITY_GRAIN_LOSS)
        assert isclose(
            result.donor_grain.ending + result.city_grain.ending,
            DONOR_BEGIN_GRAIN + BASE_CITY_GRAIN + DONOR_PRODUCTION - DONOR_CONSUMPTION - CITY_GRAIN_CONSUMPTION - CITY_GRAIN_LOSS,
        )
    grain = simulate(FIXTURES[2])
    assert isclose(grain.transfer, 14.0)
    assert isclose(grain.donor_grain.ending, WINTER_RESERVE)
    assert isclose(grain.city_grain.ending, 15.0)
    assert simulate(FIXTURES[0]).city_grain.ending == grain.city_grain.ending - grain.transfer
    sanitation = simulate(FIXTURES[1], bathhouse=True)
    assert isclose(sanitation.aqueduct_disease_factor, 0.85)
    assert isclose(sanitation.bathhouse_disease_factor, 0.75)


def report() -> str:
    baseline = simulate(FIXTURES[0]).effective_cap
    lines = [
        "E4 economy oracle — target/fixture arithmetic; not fitted live E4 outcomes (I-T4 remains unproven)",
        "Caps are separate constraints: effective = min(food, water, housing). Ratios are conditional fixture capacity / no-E4 capacity.",
        "Aqueduct: water-limited cap ×1.40; served disease term ×0.85. Public grain: food-cap target ×1.25; physical transfer only. Bathhouse: density-disease term ×0.75 (separate).",
        "No effect in this screen creates food. Ledger: begin + production/import − consumption/export − loss = ending.",
        "",
        "fixture             food   water  housing  effective  ratio  transfer  donor end/reserve  city grain end",
    ]
    for fixture in FIXTURES:
        result = simulate(fixture)
        lines.append(
            f"{fixture.name:<19} {result.food_cap:>5.1f}  {result.water_cap:>5.1f}  {result.housing_cap:>7.1f}"
            f"  {result.effective_cap:>9.1f}  {result.effective_cap / baseline:>5.2f}x"
            f"  {result.transfer:>8.1f}  {result.donor_grain.ending:>5.1f}/{result.donor_reserve:.1f}"
            f"             {result.city_grain.ending:>5.1f}"
        )
    lines.extend(("", "Disease factors are independent of capacity: aqueduct ×0.85 served disease; bathhouse ×0.75 density disease."))
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="assert fixture caps, donor reserve, and grain conservation")
    args = parser.parse_args()
    if args.check:
        check()
    print(report())


if __name__ == "__main__":
    main()
