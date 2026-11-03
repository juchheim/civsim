#!/usr/bin/env python3
"""Deterministic E4 state/trade/faith fixture arithmetic.

This is an accounting and bounded-transfer oracle, not a coupled world simulator
or a fit to live behavior. Values named FIXTURE_* are deliberately explicit
inputs. E4 effects remain provisional as described in 25/26.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
from math import isclose


# Explicit fixture inputs; fractions/constants below come from the M11 proposal.
BASE_OVERREACH = 100.0
DISTANCE_COHESION = 40.0
OTHER_COHESION_STRESS = 60.0
SOURCE_DOMINION_FLOW = 80.0
RECIPIENT_DOMINION_FLOW = 20.0
MAX_TRIBUTE_SHARE = 0.25
WONDER_LABOR = {"Civ A": 12.0, "Civ B": 8.0}
WONDER_MATERIAL = {"Civ A": 3.0, "Civ B": 2.0}
FAITH_POPULATION = 1000.0
FAITH_REACHED = 240.0
FAITH_RATE = 0.008  # 0.8% of eligible non-adherents/year, below the 1% ceiling.
BASE_ROUTE_LENGTH = 10.0
BASE_HARBOR_CAPACITY = 10.0
BASE_CARGO = 8.0


@dataclass(frozen=True)
class AdminCase:
    name: str
    administrator_staffed: bool
    courier_relay_maintained: bool
    province_connected: bool
    governor_staffed: bool


def admin_overreach(case: AdminCase) -> tuple[float, tuple[str, ...]]:
    factors = []
    applied = []
    if case.administrator_staffed:
        factors.append(0.75)  # GOV-07 −25%
        applied.append("GOV-07 ×0.75")
    if case.courier_relay_maintained:
        factors.append(0.80)  # COM-08 −20%
        applied.append("COM-08 ×0.80")
    # TER-08 is available only for a staffed governor in a connected province.
    if case.province_connected and case.governor_staffed:
        factors.append(0.70)
        applied.append("TER-08 ×0.70")
    result = BASE_OVERREACH
    for factor in factors:
        result *= factor
    return result, tuple(applied)


def cohesion_stress(citizenship: bool) -> tuple[float, float, float]:
    distance = DISTANCE_COHESION * (0.5 if citizenship else 1.0)
    other = OTHER_COHESION_STRESS
    return distance, other, distance + other


def tributary_transfer(source: float, recipient: float, share: float = MAX_TRIBUTE_SHARE) -> tuple[float, float, float]:
    if source <= 0 or not (0.0 <= share <= MAX_TRIBUTE_SHARE):
        raise ValueError("transfer requires positive source flow and share in [0, 25%]")
    transfer = source * share
    return source - transfer, recipient + transfer, transfer


def validate_treaty_graph(edges: tuple[tuple[str, str], ...]) -> bool:
    """Reject nested (subject is overlord elsewhere) and cyclic transfers."""
    incoming = {subject for _, subject in edges}
    outgoing = {overlord for overlord, _ in edges}
    if incoming & outgoing:
        return False
    graph: dict[str, list[str]] = {}
    for overlord, subject in edges:
        graph.setdefault(subject, []).append(overlord)
    visiting: set[str] = set()
    visited: set[str] = set()

    def visit(node: str) -> bool:
        if node in visiting:
            return False
        if node in visited:
            return True
        visiting.add(node)
        if any(not visit(parent) for parent in graph.get(node, [])):
            return False
        visiting.remove(node)
        visited.add(node)
        return True

    return all(visit(node) for node in graph)


def wonder_ledger() -> tuple[float, float]:
    labor = sum(WONDER_LABOR.values())
    materials = sum(WONDER_MATERIAL.values())
    # Labor and material remain separate physical accounts; neither multiplies output.
    return labor, materials


def faith_spread(supplied: bool, connected: bool, old_reached: float) -> tuple[float, float]:
    if not supplied or not connected:
        return 0.0, old_reached
    cap = 0.25 * FAITH_POPULATION
    eligible_nonadherents = FAITH_POPULATION - old_reached
    amount = min(eligible_nonadherents * FAITH_RATE, cap - old_reached)
    return max(0.0, amount), old_reached + max(0.0, amount)


def route_and_capacity() -> tuple[float, float, float, float]:
    length = BASE_ROUTE_LENGTH * 3.0  # TRD-07; route reach/length only.
    harbor = BASE_HARBOR_CAPACITY * 2.0
    buoyant = harbor * 1.10  # INQ-05; cargo capacity only.
    carried = min(BASE_CARGO, buoyant)
    return length, harbor, buoyant, carried


def check() -> None:
    full = AdminCase("all active", True, True, True, True)
    full_value, used = admin_overreach(full)
    assert isclose(full_value, 100 * .75 * .80 * .70)
    assert used == ("GOV-07 ×0.75", "COM-08 ×0.80", "TER-08 ×0.70")
    inactive = AdminCase("unstaffed/disconnected", False, True, False, True)
    inactive_value, inactive_used = admin_overreach(inactive)
    assert isclose(inactive_value, 80.0) and inactive_used == ("COM-08 ×0.80",)
    d, other, total = cohesion_stress(True)
    assert isclose(d, 20) and isclose(other, 60) and isclose(total, 80)
    sent, received, moved = tributary_transfer(SOURCE_DOMINION_FLOW, RECIPIENT_DOMINION_FLOW)
    assert isclose(sent + received, SOURCE_DOMINION_FLOW + RECIPIENT_DOMINION_FLOW)
    assert isclose(moved, 20) and moved <= SOURCE_DOMINION_FLOW * .25
    assert validate_treaty_graph((("Overlord", "Subject"),))
    assert not validate_treaty_graph((("A", "B"), ("B", "C")))
    assert not validate_treaty_graph((("A", "B"), ("B", "A")))
    labor, materials = wonder_ledger()
    assert isclose(labor, 20) and isclose(materials, 5)
    assert len(WONDER_LABOR) == len(WONDER_MATERIAL) == 2
    spread, reached = faith_spread(True, True, FAITH_REACHED)
    assert 0 < FAITH_RATE < .01 and spread <= (FAITH_POPULATION - FAITH_REACHED) * .01
    assert reached <= FAITH_POPULATION * .25
    assert faith_spread(False, True, FAITH_REACHED) == (0.0, FAITH_REACHED)
    assert faith_spread(True, False, FAITH_REACHED) == (0.0, FAITH_REACHED)
    length, harbor, buoyant, cargo = route_and_capacity()
    assert isclose(length, BASE_ROUTE_LENGTH * 3)
    assert isclose(buoyant, BASE_HARBOR_CAPACITY * 2 * 1.1)
    assert cargo <= buoyant and isclose(cargo, BASE_CARGO)  # no duplicate cargo


def report() -> None:
    print("E4 state/trade/faith fixture arithmetic (not fitted live behavior)")
    for case in (
        AdminCase("all active", True, True, True, True),
        AdminCase("administrator unstaffed; province disconnected", False, True, False, True),
    ):
        value, effects = admin_overreach(case)
        print(f"admin {case.name}: overreach {BASE_OVERREACH:g} -> {value:g}; effects={', '.join(effects) or 'none'}")
    d, other, total = cohesion_stress(True)
    print(f"SOC-10: distance stress {DISTANCE_COHESION:g} -> {d:g}; other stress {other:g} unchanged; total {total:g}")
    sent, received, moved = tributary_transfer(SOURCE_DOMINION_FLOW, RECIPIENT_DOMINION_FLOW)
    print(f"DIP-06: Dominion flow source {SOURCE_DOMINION_FLOW:g} -> {sent:g}; recipient {RECIPIENT_DOMINION_FLOW:g} -> {received:g}; transferred {moved:g}; combined conserved {sent + received:g}")
    labor, materials = wonder_ledger()
    print(f"DIP-07: shared Wonder ledger consumes contributed labor {labor:g} worker-seasons and materials {materials:g} units once; one work item/output, one designated owner/score recipient")
    spread, reached = faith_spread(True, True, FAITH_REACHED)
    _, blocked_reached = faith_spread(False, True, FAITH_REACHED)
    print(f"BEL-09: supplied connected route uses 1 worker-season/season; converts {spread:.2f}/{FAITH_POPULATION:g} population in year (rate {FAITH_RATE:.1%} of eligible); reached {reached:.2f}/{FAITH_POPULATION * .25:g} cap; ownership/fog unchanged")
    print(f"BEL-09 disconnected/unsupplied: converts 0; reached remains {blocked_reached:g}")
    length, harbor, buoyant, cargo = route_and_capacity()
    print(f"route/cargo dimensions: TRD-07 length {BASE_ROUTE_LENGTH:g} -> {length:g}; Harbor capacity {BASE_HARBOR_CAPACITY:g} -> {harbor:g} -> INQ-05 {buoyant:g}; fixture cargo remains {cargo:g} (no duplicate cargo)")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="prove fixture bounds and conservation invariants")
    args = parser.parse_args()
    if args.check:
        check()
        print("--check PASS: admin reductions compose multiplicatively and require staffing/connection; SOC-10 scopes to distance; DIP-06 conserves flow and graph is flat/acyclic; DIP-07 contributions counted once; BEL-09 route/rate/reached cap; route length separate from cargo capacity")
    report()


if __name__ == "__main__":
    main()
