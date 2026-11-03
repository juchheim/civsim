#!/usr/bin/env python3
"""Deterministic E4 research closure/cost lower-bound oracle.

Reads the E4 requirements and gates directly from the catalog YAML. Costs use
the provisional E4 EraBase=1200 and type multipliers in 02/26. Time is reported
only when the caller supplies an Insight and Insight/min rate; this is arithmetic,
not a prediction of live pacing.
"""
from __future__ import annotations

import argparse
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
CATALOG = ROOT / "content" / "catalog"
ERA_BASE = 1200
MULT = {"D": 1.0, "M": 1.0, "S": 1.2, "R": 0.8, "I": 1.5, "W": 2.0, "K": 2.5}
REQUIRED_KEY = {"AGR-10", "GOV-09", "BEL-09", "DEF-08"}


def load_catalog():
    projects = {}
    for path in sorted(CATALOG.glob("*.yaml")):
        lines = path.read_text().splitlines()
        current, section = None, None
        records = []
        for line in lines:
            if line == "projects:" or line == "keystones:":
                section = line[:-1]
            elif section and line.startswith("  - id:"):
                if current:
                    records.append(current)
                current = {"id": line.split(":", 1)[1].strip()}
            elif current and line.startswith("    "):
                key, sep, value = line.strip().partition(":")
                if not sep:
                    continue
                value = value.strip()
                if key == "era":
                    current[key] = int(value)
                elif key == "type":
                    current[key] = value
                elif key in ("requires", "requires_any"):
                    current[key] = [x.strip() for x in value.strip("[]").split(",") if x.strip()]
                elif key == "gates":
                    import re
                    current[key] = [dict(kind=k, target=t) for k, t in re.findall(r"kind:\s*(\w+),\s*target:\s*([^}]+)", value)]
            elif current and line and not line.startswith("      "):
                records.append(current)
                current, section = None, None
        if current:
            records.append(current)
        for p in records:
            # Keep earlier records too: they terminate recursion at the
            # explicitly assumed pre-E4-complete frontier.
            projects[p["id"]] = p
    return projects


def cost(p):
    return ERA_BASE * MULT[p["type"]]


def closure(projects, pid, camel_seen, forced_route=None):
    """Return E4 closure; all pre-E4 nodes are assumed already completed."""
    out, visiting = set(), set()

    def add(node):
        if node in visiting:
            raise ValueError(f"prerequisite cycle at {node}")
        p = projects[node]
        if p.get("era", 0) < 4:
            return
        if node == "HUS-09" and not camel_seen:
            raise ValueError("HUS-09 unavailable: Seen Camels or Llamas gate")
        if node in out:
            return
        visiting.add(node)
        for req in p.get("requires", []):
            add(req)
        any_groups = p.get("requires_any", [])
        if any_groups and isinstance(any_groups[0], str):
            any_groups = [any_groups]
        for alternatives in any_groups:
            eligible = [x for x in alternatives if x != "HUS-09" or camel_seen]
            if forced_route == "sea" and node == "TRD-07":
                eligible = ["SEA-07"] if "SEA-07" in alternatives else eligible
            if not eligible:
                raise ValueError(f"no available requires_any route for {node}")
            # Cheapest complete closure; deterministic ID tie break.
            choices = []
            for alt in sorted(eligible):
                trial = closure(projects, alt, camel_seen, forced_route)
                choices.append((sum(cost(projects[x]) for x in trial), alt))
            add(min(choices)[1])
        visiting.remove(node)
        out.add(node)

    add(pid)
    return out


def e4_closure(projects, roots, camel_seen, forced_route=None):
    result = set()
    for root in sorted(roots):
        result |= closure(projects, root, camel_seen, forced_route)
    return result


def candidate(projects, camel_seen, forced_route=None):
    selected = e4_closure(projects, REQUIRED_KEY, camel_seen, forced_route)
    regular = {x for x in selected if x != "KEY-5"}
    # Greedy deterministic marginal-closure candidate: repeatedly add the
    # cheapest closure extension per newly counted E4 regular completion.
    while len(regular) < 21:
        options = []
        for pid, p in projects.items():
            if p.get("era") != 4 or pid in regular:
                continue
            try:
                add = e4_closure(projects, {pid}, camel_seen, forced_route) - selected
            except ValueError:
                continue
            newly_counted = len(add - {"KEY-5"})
            marginal = sum(cost(projects[x]) for x in add)
            if newly_counted:
                options.append((marginal / newly_counted, marginal, pid, add))
        if not options:
            raise RuntimeError("could not reach 21 E4 regular projects")
        _, _, _, add = min(options, key=lambda x: (x[0], x[1], x[2]))
        selected |= add
        regular = selected - {"KEY-5"}
    # KEY-5 itself is a costed node, with its four required projects.
    selected.add("KEY-5")
    return selected


def report(projects, label, camel_seen, forced_route=None, insight=None, rate=None):
    chosen = candidate(projects, camel_seen, forced_route)
    total = sum(cost(projects[x]) for x in chosen)
    mandatory = e4_closure(projects, REQUIRED_KEY, camel_seen, forced_route)
    mandatory_regular = mandatory - {"KEY-5"}
    need = max(0, 21 - len(mandatory_regular))
    eligible = [cost(p) for pid, p in projects.items()
                if p.get("era") == 4 and not pid.startswith("KEY-") and pid not in mandatory_regular
                and not (pid == "HUS-09" and not camel_seen)]
    lower_bound = sum(cost(projects[x]) for x in mandatory_regular) + sum(sorted(eligible)[:need]) + cost(projects["KEY-5"])
    print(f"{label}: selected {len(chosen - {'KEY-5'})} regular + KEY-5 (deterministic greedy candidate)")
    print("  route:", "HUS-09 camel-seen OR route" if camel_seen and forced_route != "sea" else "SEA-07 no-camel sea route")
    print("  IDs:", ", ".join(sorted(chosen)))
    print(f"  list cost: {total:.0f} (EraBase={ERA_BASE}; KEY-5 included)")
    print(f"  cost floor: {lower_bound:.0f} (mandatory closure + cheapest additional IDs; ignores their prerequisites)")
    if rate is not None:
        print(f"  supplied rate: {rate:g} Insight/min => {total / rate:.3f} min arithmetic")
    return total, chosen


def check(projects):
    regular = {i for i, p in projects.items() if p.get("era") == 4 and not i.startswith("KEY-")}
    assert len(regular) == 56 and "KEY-5" in projects, (len(regular), "KEY-5" in projects)
    assert projects["TRD-07"].get("requires_any") == ["HUS-09", "SEA-07"]
    assert projects["NRG-03"].get("gates") == [{"kind": "near", "target": "River"}]
    key = projects["KEY-5"]
    assert set(key.get("requires", [])) == REQUIRED_KEY
    # Count route alternatives against the other KEY-5 requirements, which
    # already include BLD-09/CER-08 shared by the harbor path.
    shared = e4_closure(projects, REQUIRED_KEY - {"BEL-09"}, True)
    land_route = closure(projects, "HUS-09", True) - shared
    sea_route = closure(projects, "SEA-07", False, "sea") - shared
    assert sum(cost(projects[x]) for x in land_route) == 1200, land_route
    assert sum(cost(projects[x]) for x in sea_route) == 1440, sea_route
    # The OR resolver must choose the cheaper HUS route when camel-seen and
    # must retain SEA-07 as a valid route when HUS-09's Seen gate is closed.
    camel_key_closure = e4_closure(projects, REQUIRED_KEY, True)
    no_camel_key_closure = e4_closure(projects, REQUIRED_KEY, False, "sea")
    assert "HUS-09" in camel_key_closure and "SEA-07" not in camel_key_closure
    assert "SEA-07" in no_camel_key_closure and "HUS-09" not in no_camel_key_closure
    for camel, route in ((True, None), (False, "sea")):
        chosen = candidate(projects, camel, route)
        assert len(chosen - {"KEY-5"}) >= 21 and REQUIRED_KEY <= chosen
        assert "TRD-07" not in chosen or (("HUS-09" in chosen and camel) or "SEA-07" in chosen)
        assert all(projects[x]["era"] == 4 for x in chosen)
        assert sum(cost(projects[x]) for x in chosen) == sum(ERA_BASE * MULT[projects[x]["type"]] for x in chosen)
        mandatory = e4_closure(projects, REQUIRED_KEY, camel, route)
        mandatory_regular = mandatory - {"KEY-5"}
        extra_count = max(0, 21 - len(mandatory_regular))
        available_costs = sorted(cost(p) for pid, p in projects.items()
                                 if p.get("era") == 4 and not pid.startswith("KEY-")
                                 and pid not in mandatory_regular
                                 and not (pid == "HUS-09" and not camel))
        floor = (sum(cost(projects[x]) for x in mandatory_regular)
                 + sum(available_costs[:extra_count]) + cost(projects["KEY-5"]))
        assert sum(cost(projects[x]) for x in chosen) == floor
    print("--check PASS: 56 regular + KEY-5; KEY-5 prerequisites; TRD-07 OR route closures (HUS 1200 / SEA 1440, deduplicated); NRG-03 Near: River; camel and forced sea closures/cost arithmetic")


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--rate", type=float, help="user-supplied Insight per minute")
    args = ap.parse_args()
    if args.rate is not None and args.rate <= 0:
        ap.error("--rate must be positive")
    projects = load_catalog()
    if args.check:
        check(projects)
    # Print two explicit access assumptions so the land and no-camel sea routes
    # can be compared under identical base/multiplier assumptions.
    a = report(projects, "camel-seen assumption", True,
               rate=args.rate)
    b = report(projects, "no-camel assumption (force SEA-07 route)", False, "sea",
               rate=args.rate)
    print(f"  sea-route delta vs camel-seen: {b[0] - a[0]:+.0f} cost")


if __name__ == "__main__":
    main()
