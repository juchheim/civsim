#!/usr/bin/env python3
"""Deterministic E6 (Discovery) research closure, cost and pacing oracle (docs/MVP/38 §3.2, §7 D-T1).

Companion to e5_research_oracle.py. Reads requirements and explicit cost overrides from the catalog YAML. Costs use the registry EraBase
(E6 = 32,000), the 02 §4 type multipliers and the 28 §7 / 38 §6 D9 late-project doubling. Time appears only as arithmetic against a
supplied growth rate and the live E5 anchor (e6_anchors.LIVE_E5); none of it is a live E6 result (none exists), and nothing here fits or
approves a pacing band.

Sections: (1) E6 inventory and the KEY-7 closure, (2) candidate sets and the backfill debt KEY-7 needs from E3 to E5, (3) the forward pacing
estimate from the live E5 rate and the era base that would land E6 at the 25-minute target.
"""
from __future__ import annotations

import argparse
import importlib.util
import math
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from e6_anchors import E6_ERA_BASE, E6_TARGET_MINUTES, LIVE_E5  # noqa: E402
_spec = importlib.util.spec_from_file_location("e4_research_oracle", HERE / "e4_research_oracle.py")
e4 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(e4)

ROOT = HERE.parents[3]
MULT = e4.MULT
LAG = 0.7                     # 02 §8.3 EraLagMult = 0.7 ^ (current era - project era)
KEY7_REQUIRES = {"NRG-06", "MEC-10", "TRD-10", "INQ-08"}
BREADTH = 14
EARLY_BASE = [22, 50, 150, 400, 1200]   # E0 to E4 (registry)
CRAFT = {"STN", "FIR", "WOD", "FIB", "CER", "BLD", "MIN", "MET", "MEC", "NRG", "CHM"}
KIN = {"FOR", "HNT", "AGR", "HUS", "FOD", "MED", "SOC", "GOV", "BEL", "ART", "LRN"}


def theme(pid):
    lin = pid[:3]
    return "craft" if lin in CRAFT else "kin" if lin in KIN else "reach"


def catalog_costs():
    """Explicit `cost:` overrides in the catalog YAML (id -> cost)."""
    out = {}
    for path in sorted((ROOT / "content" / "catalog").glob("*.yaml")):
        for block in re.split(r"\n  - id: ", path.read_text())[1:]:
            m = re.search(r"^    cost: ([0-9.]+)", block, re.M)
            if m:
                out[block.split("\n")[0].strip()] = float(m.group(1))
    return out


_COSTS = catalog_costs()


def era_depth(projects, pid):
    """Longest same-era prerequisite chain (edges); a requires_any group counts its deepest alternative (28 §7)."""
    era = projects[pid]["era"]
    memo = {}

    def d(i):
        if i in memo:
            return memo[i]
        p, best = projects[i], 0
        for r in p.get("requires", []):
            if r in projects and projects[r].get("era") == era:
                best = max(best, d(r) + 1)
        groups = p.get("requires_any", [])
        if groups and isinstance(groups[0], str):
            groups = [groups]
        for alts in groups:
            ins = [d(a) for a in alts if a in projects and projects[a].get("era") == era]
            if ins:
                best = max(best, max(ins) + 1)
        memo[i] = best
        return best

    return d(pid)


def late(projects, pid):
    """A non-keystone project with at least three projects in its own era's chain (28 §7: 'prerequisite depth 3 or more')."""
    return not pid.startswith("KEY-") and era_depth(projects, pid) >= 2


def cost(projects, pid, e5_base=LIVE_E5["e5_era_base"], e6_base=E6_ERA_BASE):
    """List cost. E0-E4 follow the catalog (explicit overrides carry the doublings); E5 and E6 are computed from their era base so the
    base can be varied; E5 and E6 late projects cost twice (docs/MVP/27 D9, 38 D9)."""
    p = projects[pid]
    era = p["era"]
    if era <= 4:
        return _COSTS.get(pid, EARLY_BASE[era] * MULT[p["type"]])
    base = e5_base if era == 5 else e6_base
    c = base * MULT[p["type"]]
    return c * 2 if late(projects, pid) else c


def closure(projects, roots, **kw):
    """Full ancestor closure (all eras); requires_any takes the cheaper complete alternative."""
    out, stack = set(), set()

    def add(node):
        if node in out:
            return
        if node in stack:
            raise ValueError(f"prerequisite cycle at {node}")
        stack.add(node)
        p = projects[node]
        for req in p.get("requires", []):
            add(req)
        groups = p.get("requires_any", [])
        if groups and isinstance(groups[0], str):
            groups = [groups]
        for alts in groups:
            choices = [(sum(cost(projects, x, **kw) for x in closure(projects, {a}, **kw)), a) for a in sorted(alts)]
            add(min(choices)[1])
        stack.discard(node)
        out.add(node)

    for r in sorted(roots):
        add(r)
    return out


def regulars(projects, era):
    return {i for i, p in projects.items() if p.get("era") == era and not i.startswith("KEY-")}


def of_era(projects, ids, era):
    return {i for i in ids if projects[i].get("era") == era and not i.startswith("KEY-")}


def critical(projects):
    return of_era(projects, closure(projects, KEY7_REQUIRES), 6)


def candidate(projects, restrict_theme=None):
    """KEY-7 closure plus the cheapest extra E6 projects (by marginal cost per newly counted E6 project) up to the breadth of 14."""
    selected = closure(projects, KEY7_REQUIRES)
    while len(of_era(projects, selected, 6)) < BREADTH:
        options = []
        for pid in sorted(regulars(projects, 6)):
            if pid in selected or (restrict_theme and theme(pid) != restrict_theme):
                continue
            if pid in ("FOD-10", "FOR-10"):
                continue  # [Has: Sugarcane] / overseas contact: not assumed available
            add = closure(projects, {pid}) - selected
            new = len(of_era(projects, add, 6))
            if new:
                options.append((sum(cost(projects, x) for x in add if projects[x]["era"] == 6) / new, pid, add))
        if not options:
            raise RuntimeError("could not reach the E6 breadth")
        selected |= min(options)[2]
    selected.add("KEY-7")
    return selected


def era_cost(projects, ids, era):
    return sum(cost(projects, i) for i in ids if projects[i]["era"] == era)


def backfill(projects, ids, done, era=6):
    """Cost of the pre-E6 prerequisites in `ids` not already `done`, at EraLag 0.7^(6 - project era)."""
    rows = []
    for i in sorted(ids - done):
        e = projects[i]["era"]
        if e < era:
            rows.append((i, e, cost(projects, i) * LAG ** (era - e)))
    return rows


def whole_age(projects, era, base_kw):
    ids = regulars(projects, era)
    key = {5: "KEY-6", 6: "KEY-7"}[era]
    return sum(cost(projects, i, **base_kw) for i in ids) + cost(projects, key, **base_kw) if False else \
        sum(cost(projects, i, **base_kw) for i in ids) + (2.5 * (LIVE_E5["e5_era_base"] if era == 5 else E6_ERA_BASE))


def spend_to_minutes(spend, minutes, r):
    """Insight rate at the START of an interval (per minute) that accumulates `spend` in `minutes` with exponential growth r per minute."""
    return spend * r / (math.exp(r * minutes) - 1)


def minutes_for(cost_total, i_start, r):
    """Minutes to accumulate cost_total starting at rate i_start/min and growing at r per minute."""
    return math.log(1 + cost_total * r / i_start) / r


def report(projects):
    reg = sorted(regulars(projects, 6))
    print("== E6 inventory (catalog YAML, draft rows)")
    by_theme = {t: [i for i in reg if theme(i) == t] for t in ("craft", "kin", "reach")}
    print(f"  {len(reg)} regular + KEY-7; craft {len(by_theme['craft'])}, kin {len(by_theme['kin'])}, reach {len(by_theme['reach'])}")
    for t, ids in by_theme.items():
        print(f"  {t:5s} list cost {sum(cost(projects, i) for i in ids):>11,.0f}")
    total_list = sum(cost(projects, i) for i in reg)
    late_ids = sorted(i for i in reg if late(projects, i))
    print(f"  all regular list cost {total_list:,.0f}; KEY-7 {cost(projects, 'KEY-7'):,.0f}; late-project doubling (D9) touches {len(late_ids)} of 41: {', '.join(late_ids)}")
    print(f"  costs match the catalog YAML overrides for the doubled rows: {all(cost(projects, i) == _COSTS[i] for i in late_ids)}")

    crit = sorted(critical(projects))
    print(f"\n== KEY-7 closure: {len(crit)} critical E6 projects, {BREADTH - len(crit)} free picks (breadth {BREADTH})")
    print("  critical: " + ", ".join(crit))
    print("  themes on the critical path: " + ", ".join(f"{t} {sum(1 for i in crit if theme(i) == t)}" for t in ("craft", "kin", "reach")))
    print("  Unlike E5 (no Craft project on the KEY-6 path), KEY-7 needs steam, the factory and coal mining, so the Craft systems of 38 §5.1 and §5.7")
    print("  run in natural play without a --grant-e6 mode; the optional systems are war, colonies, health and most of the land projects.")
    print(f"  critical list cost {sum(cost(projects, i) for i in crit):,.0f}")

    for label, rt in (("cheapest free picks", None), ("Craft-only free picks (exercises the coal, coke and fuel systems)", "craft")):
        sel = candidate(projects, rt)
        extra = sorted(of_era(projects, sel, 6) - set(crit))
        print(f"\n== Candidate: {label}")
        print(f"  free picks: {', '.join(extra)}")
        print(f"  E6 list cost (14 regular + KEY-7): {era_cost(projects, sel, 6):,.0f}")

    print("\n== Backfill debt: pre-E6 prerequisites of KEY-7 beyond KEY-6's own closure (upper bound)")
    done = closure(projects, {"COM-10", "TRD-09", "WAY-08", "LRN-08"})
    rows = backfill(projects, closure(projects, KEY7_REQUIRES), done)
    print(f"  {len(rows)} projects, {sum(r[2] for r in rows):,.0f} Insight at EraLag 0.7^n")
    print("    " + ", ".join(f"{i}(E{e})" for i, e, _ in rows))

    forward(projects)


def forward(projects):
    a = LIVE_E5
    base5 = a["e5_era_base"]
    e5_all = whole_age(projects, 5, {})
    e5_crit_sel = closure(projects, {"COM-10", "TRD-09", "WAY-08", "LRN-08"})
    e5_cand = era_cost(projects, e5_crit_sel, 5) + 2.5 * base5  # at least the 12 critical E5 projects plus KEY-6 (free picks add more)
    e6_sel = candidate(projects)
    done = closure(projects, {"COM-10", "TRD-09", "WAY-08", "LRN-08"})
    bf = sum(r[2] for r in backfill(projects, closure(projects, KEY7_REQUIRES), done))
    e6_cand = era_cost(projects, e6_sel, 6) + bf
    e6_all = whole_age(projects, 6, {})
    T5, r0 = a["key5_to_key6_median_minutes"], a["growth_per_minute"]
    print("\n== Forward pacing estimate from the live E5 rate (not a fit; E5 is still being balanced)")
    print(f"  Live anchor: {a['label']}; median {T5} min (p10-p90 {a['key5_to_key6_p10_p90'][0]}-{a['key5_to_key6_p10_p90'][1]}); E5 era base {base5:,}.")
    print(f"  E5 whole-age list cost at base {base5:,}: {e5_all:,.0f}; the 12-project critical set plus KEY-6: {e5_cand:,.0f} (a lower bound on what a seat spent).")
    print(f"  E6 whole age at base {E6_ERA_BASE:,}: {whole_age(projects, 6, {}):,.0f} ({whole_age(projects, 6, {}) / e5_all:.2f}x E5's whole age); "
          f"candidate + backfill {e6_cand:,.0f}.")
    print("  Model: Insight grows at r per minute. The live interval implies a start-of-E5 rate from the amount E5 seats spent; E6 starts at the rate")
    print("  reached at the end of that interval. Two readings of what E5 seats spent: the whole age (as Iron seats did) or only the critical set.")
    print(f"  {'E5 spend (reading)':44s} {'r':>5s} | {'Insight/min at KEY-6':>21s} | minutes for E6 candidate | minutes for whole E6 age")
    for label, spent in (("whole E5 age", e5_all), ("12 critical projects + KEY-6 only", e5_cand)):
        for r in (0.04, 0.06, 0.08):
            i_start = spend_to_minutes(spent, T5, r)
            i_end = i_start * math.exp(r * T5)
            print(f"  {label:44s} {r:5.2f} | {i_end:21,.0f} | {minutes_for(e6_cand, i_end, r):24.1f} | {minutes_for(e6_all, i_end, r):.1f}")
    print(f"  Era base that lands E6 at the target ({E6_TARGET_MINUTES:.0f} min), scenario 'whole E6 age', reading 'whole E5 age':")
    for r in (0.04, 0.06, 0.08):
        i_end = spend_to_minutes(e5_all, T5, r) * math.exp(r * T5)
        need = i_end * (math.exp(r * E6_TARGET_MINUTES) - 1) / r
        print(f"     r={r:.2f}: needs a whole-age cost of {need:,.0f} = x{need / e6_all:.2f} the typed cost (base ~{E6_ERA_BASE * need / e6_all:,.0f})")
    print("  Reading: E6's era base of 32,000 is 4.4x E5's 7,300; the whole-age cost ratio is printed above. If the ratio is well above the growth in")
    print("  Insight over E5's own length, E6 runs long; the base is the lever, as it was for E5, and it should be refit on the first live E6 run.")
    print("  Sensitivity to the still-moving E5 base: rerun with a different e6_anchors.LIVE_E5['e5_era_base'] and median.")


def check(projects):
    reg = regulars(projects, 6)
    assert len(reg) == 41, len(reg)
    assert "KEY-7" in projects and set(projects["KEY-7"]["requires"]) == KEY7_REQUIRES
    assert {t: sum(1 for i in reg if theme(i) == t) for t in ("craft", "kin", "reach")} == {"craft": 12, "kin": 12, "reach": 17}
    crit = critical(projects)
    expected = {"FIB-12", "INQ-07", "INQ-08", "MEC-09", "MEC-10", "MIN-09", "NRG-06", "SEA-12", "SOC-12", "TRD-10"}
    assert crit == expected, sorted(crit ^ expected)
    assert BREADTH - len(crit) == 4
    assert sum(1 for i in crit if theme(i) == "craft") == 5, "KEY-7 puts five Craft projects on the critical path"
    late_ids = {i for i in reg if late(projects, i)}
    assert late_ids == {"CHM-08", "FIB-12", "FIR-11", "FIR-12", "INQ-09", "MEC-10", "MET-13", "TRD-11"}, sorted(late_ids)
    assert all(cost(projects, i) == _COSTS[i] for i in late_ids), "typed cost overrides must equal 2 x EraBase x type multiplier"
    assert cost(projects, "KEY-7") == 2.5 * E6_ERA_BASE and cost(projects, "MIN-09") == E6_ERA_BASE
    sel = candidate(projects)
    assert len(of_era(projects, sel, 6)) == BREADTH and KEY7_REQUIRES <= sel and "KEY-7" in sel
    cs = candidate(projects, "craft")
    extra = of_era(projects, cs, 6) - crit
    assert len(extra) == 4 and sum(theme(i) == "craft" for i in extra) >= 3, sorted(extra)  # BLD-13 drags in NUM-10 (Reach), so not all four are Craft
    assert "FOD-10" not in sel and "FOR-10" not in sel
    # Pacing arithmetic: the closed forms invert each other.
    i0 = spend_to_minutes(1000.0, 10.0, 0.06)
    assert math.isclose(minutes_for(1000.0, i0, 0.06), 10.0)
    print("--check PASS: 41 regular + KEY-7 (12/12/17); KEY-7 closure is the 10 projects with 5 Craft, 4 free picks; "
          "8 late projects cost 2x and match the typed YAML; candidate sets reach 14; pacing closed forms invert")


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    projects = e4.load_catalog()
    if args.check:
        check(projects)
        return
    report(projects)


if __name__ == "__main__":
    main()
