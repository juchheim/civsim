#!/usr/bin/env python3
"""Deterministic E5 (Faith & Fief) research closure, cost and Insight-budget oracle.

Companion to e4_research_oracle.py (docs/MVP/27 §3.2, §7 F-T1). Reads requirements from the
catalog YAML. Costs use the registry EraBase (E5 = 6000) and the 02 §4 type multipliers.
Time appears only as arithmetic against a supplied Insight rate or a supplied duration; none
of it is a live result, and nothing here fits or approves a pacing band.

Sections: (1) KEY-6 closure and the 16-project candidate, (2) the backfill debt from E3/E4
that KEY-6 needs beyond KEY-5's own closure, (3) required Insight/min against the E4
candidate and the 09 §11.2 mean-field Insight, (4) how much the E5 Insight projects can add.
"""
from __future__ import annotations

import argparse
import importlib.util
import math
from math import isclose
from pathlib import Path

HERE = Path(__file__).resolve().parent
import sys
sys.path.insert(0, str(HERE))
from e5_anchors import LIVE_E4  # noqa: E402
_spec = importlib.util.spec_from_file_location("e4_research_oracle", HERE / "e4_research_oracle.py")
e4 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(e4)

ERA_BASE = [22, 50, 150, 400, 1200, 6000]
MULT = e4.MULT
LAG = 0.7  # 02 §8.3 EraLagMult = 0.7 ^ (current era - project era)
KEY6_REQUIRES = {"COM-10", "TRD-09", "WAY-08", "LRN-08"}
BREADTH = 16
CRAFT = {"STN", "FIR", "WOD", "FIB", "CER", "BLD", "MIN", "MET", "MEC", "NRG", "CHM"}
KIN = {"FOR", "HNT", "AGR", "HUS", "FOD", "MED", "SOC", "GOV", "BEL", "ART", "LRN"}

# 09 §11.2 mean-field model (balanced, Standard): minutes and Insight/min at the end of each era.
MODEL_MINUTES = {4: 16.5, 5: 18.25}
MODEL_INSIGHT = {4: 1080.0, 5: 4123.0}
# 07c §10.1 projection for E5 (Standard 1x); the 09 table above is the older 18.25.
PROJ_MINUTES_E5 = 16.75


def theme(pid):
    lin = pid[:3]
    return "craft" if lin in CRAFT else "kin" if lin in KIN else "reach"


ROOT = HERE.parents[3]
LATE_E5_DOUBLE = True  # user decision 2026-11-13: E5 late projects cost 2x like E0-E4 (docs/MVP/28 §7, §16)


def catalog_costs():
    """Explicit `cost:` overrides from the catalog YAML (id -> cost). Main doubled the late projects of E0-E4 this way."""
    import re
    out = {}
    for path in sorted((ROOT / "content" / "catalog").glob("*.yaml")):
        for block in re.split(r"\n  - id: ", path.read_text())[1:]:
            m = re.search(r"^    cost: ([0-9.]+)", block, re.M)
            if m:
                out[block.split("\n")[0].strip()] = float(m.group(1))
    return out


_COSTS = catalog_costs()


def era_depth(projects, pid):
    """Longest same-era prerequisite chain (edges); a requires_any group counts its deepest alternative (matches docs/MVP/28's counts)."""
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
    """True for a non-keystone project with at least three projects in its own era's chain (28 §7: 'prerequisite depth 3 or more')."""
    return not pid.startswith("KEY-") and era_depth(projects, pid) >= 2


def cost(projects, pid):
    p = projects[pid]
    if pid in _COSTS:
        return _COSTS[pid]
    base = ERA_BASE[p["era"]] * MULT[p["type"]]
    if LATE_E5_DOUBLE and p["era"] == 5 and late(projects, pid):
        return base * 2
    return base


def closure(projects, roots, camel=True, forced_sea=False):
    """Full ancestor closure (all eras). requires_any picks the cheaper complete alternative."""
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
            eligible = [a for a in alts if camel or a != "HUS-09"]
            if forced_sea and "SEA-07" in alts:
                eligible = ["SEA-07"]
            choices = [(sum(cost(projects, x) for x in closure(projects, {a}, camel, forced_sea)), a) for a in sorted(eligible)]
            add(min(choices)[1])
        stack.discard(node)
        out.add(node)

    for r in sorted(roots):
        add(r)
    return out


def regulars(projects, era=5):
    return {i for i, p in projects.items() if p.get("era") == era and not i.startswith("KEY-")}


def e5_part(projects, ids):
    return {i for i in ids if projects[i].get("era") == 5 and not i.startswith("KEY-")}


def critical(projects):
    return e5_part(projects, closure(projects, KEY6_REQUIRES))


def candidate(projects, restrict_theme=None):
    """KEY-6 closure plus the cheapest extra E5 projects (by marginal cost per newly counted E5 project)."""
    selected = closure(projects, KEY6_REQUIRES)
    count = lambda s: len(e5_part(projects, s))
    while count(selected) < BREADTH:
        options = []
        for pid in sorted(regulars(projects)):
            if pid in selected or (restrict_theme and theme(pid) != restrict_theme):
                continue
            if pid == "CHM-07":
                continue  # [Has: Saltpeter] gate: not assumed available
            try:
                add = closure(projects, {pid}) - selected
            except ValueError:
                continue
            new = len(e5_part(projects, add))
            if new:
                options.append((sum(cost(projects, x) for x in add if projects[x]["era"] == 5) / new, pid, add))
        if not options:
            raise RuntimeError("could not reach the E5 breadth")
        selected |= min(options)[2]
    selected.add("KEY-6")
    return selected


def e5_cost(projects, ids):
    return sum(cost(projects, i) for i in ids if projects[i]["era"] == 5)


def backfill(projects, ids, done):
    """Cost of the pre-E5 prerequisites in `ids` not already `done`, at EraLag 0.7^(5-era)."""
    rows = []
    for i in sorted(ids - done):
        e = projects[i]["era"]
        if e < 5:
            rows.append((i, e, cost(projects, i) * LAG ** (5 - e)))
    return rows


def key5_done(projects, camel=True):
    return closure(projects, e4.REQUIRED_KEY, camel, forced_sea=not camel)


def report(projects):
    reg = sorted(regulars(projects))
    print("== E5 inventory (catalog YAML, draft rows)")
    by_theme = {t: [i for i in reg if theme(i) == t] for t in ("craft", "kin", "reach")}
    types = {}
    for i in reg:
        types[projects[i]["type"]] = types.get(projects[i]["type"], 0) + 1
    print(f"  {len(reg)} regular + KEY-6; craft {len(by_theme['craft'])}, kin {len(by_theme['kin'])}, reach {len(by_theme['reach'])}")
    print("  type mix: " + ", ".join(f"{k} {v}" for k, v in sorted(types.items())))
    for t, ids in by_theme.items():
        print(f"  {t:5s} list cost {sum(cost(projects, i) for i in ids):>9,.0f}")
    total_list = sum(cost(projects, i) for i in reg)
    print(f"  all regular list cost {total_list:,.0f}; KEY-6 {cost(projects, 'KEY-6'):,.0f}")

    crit = sorted(critical(projects))
    print(f"\n== KEY-6 closure: {len(crit)} critical E5 projects, {BREADTH - len(crit)} free picks (breadth {BREADTH})")
    print("  critical: " + ", ".join(crit))
    print("  themes on the critical path: " + ", ".join(f"{t} {sum(1 for i in crit if theme(i) == t)}" for t in ("craft", "kin", "reach")))
    print(f"  critical list cost {sum(cost(projects, i) for i in crit):,.0f}")

    for label, rt in (("cheapest free picks", None), ("Craft-only free picks (exercises the Craft systems)", "craft")):
        sel = candidate(projects, rt)
        extra = sorted(e5_part(projects, sel) - set(crit))
        total = e5_cost(projects, sel)
        print(f"\n== Candidate: {label}")
        print(f"  free picks: {', '.join(extra)}")
        print(f"  E5 list cost (16 regular + KEY-6): {total:,.0f}")

    print("\n== Backfill debt: pre-E5 prerequisites of KEY-6 beyond KEY-5's mandatory closure (upper bound)")
    for camel in (True, False):
        done = key5_done(projects, camel)
        rows = backfill(projects, closure(projects, KEY6_REQUIRES, camel, not camel), done)
        label = "camel-seen (HUS route for KEY-5)" if camel else "no camel (SEA route for KEY-5)"
        print(f"  {label}: {len(rows)} projects, {sum(r[2] for r in rows):,.0f} Insight at EraLag 0.7^n")
        print("    " + ", ".join(f"{i}(E{e})" for i, e, _ in rows))

    # Insight demand.
    e4_sel = e4.candidate(projects, True)
    e4_cand = sum(cost(projects, x) for x in e4_sel)
    e4_all = sum(cost(projects, i) for i in regulars(projects, 4)) + cost(projects, "KEY-5")
    e5_sel = candidate(projects)
    e5_total = e5_cost(projects, e5_sel)
    rows = backfill(projects, closure(projects, KEY6_REQUIRES), key5_done(projects, True))
    e5_cand = e5_total + sum(r[2] for r in rows)
    e5_everything = sum(cost(projects, i) for i in regulars(projects, 5)) + cost(projects, "KEY-6")
    late_ids = sorted(i for i in regulars(projects, 5) if late(projects, i))
    print("\n== Insight demand (list costs; the live Iron anchor says what civilizations really spend)")
    print(f"  Iron: 21-project candidate {e4_cand:,.0f}; the WHOLE age (all 56 + KEY-5, with the 28 s7 doubling) {e4_all:,.0f}. Live Iron civilizations finish all {LIVE_E4['e4_projects_done_median']} projects")
    print(f"  before KEY-5, so the whole age is what they actually spent.")
    print(f"  E5: 16-project candidate + backfill {e5_cand:,.0f} (of which E5 list {e5_total:,.0f}); EVERY E5 project + KEY-6 {e5_everything:,.0f}.")
    print(f"  E5 late-project doubling (D9) touches {len(late_ids)} of 49: {', '.join(late_ids)}.")
    print(f"  E5/E4 whole-age cost ratio {e5_everything / e4_all:.2f} (EraBase ratio 5.00; the whole-age ratio is lower because E4 already carries 28 doubled projects).")
    live_rate = e4_all / LIVE_E4["key4_to_key5_minutes"]
    print(f"  Live anchor ({LIVE_E4['label']}): the whole age over {LIVE_E4['key4_to_key5_minutes']:.0f} min => ~{live_rate:,.0f} Insight/min on average.")
    forward_estimate(e4_all, {"A: 16-project candidate + backfill (a civilization that goes straight for KEY-6)": e5_cand,
                              "B: every E5 project + KEY-6 (how Iron civilizations actually played)": e5_everything})


# ---- Insight budget (02 §8.1; balance.yaml): a settlement-level fixture --------------------

COMMONS_COEF, COMMONS_EXP, CALIB, ROLE_SCALE = 4.8, 0.85, 1.6, 8
OUT_SCHOLAR, OUT_MONASTERY = 6 * ROLE_SCALE, 2 * ROLE_SCALE  # 48 and 16 Insight/yr, before calibration


def insight_per_year(adults_per_settlement, settlements, scholars, monasteries, commons_mult=1.0, scholar_mult=1.0, monastery_mult=1.0):
    commons = COMMONS_COEF * settlements * adults_per_settlement ** COMMONS_EXP * commons_mult
    roles = scholars * OUT_SCHOLAR * scholar_mult + monasteries * OUT_MONASTERY * monastery_mult
    return CALIB * (commons + roles)


def forward_estimate(e4_spent, scenarios):
    """Minutes to finish E5 if Insight starts at the live end-of-Iron level and keeps growing with population (exponential, r per minute).

    Calibration: the same model run backwards over the live Iron interval must reproduce what Iron civilizations actually spent.
    """
    a = LIVE_E4["adults_at_key5_median"]
    n = LIVE_E4["settlements_at_key5_median"]
    sch = LIVE_E4["scholars_at_key5_median"]
    r0 = LIVE_E4["growth_per_minute"]
    t4 = LIVE_E4["key4_to_key5_minutes"]
    print("\n== Forward pacing estimate (Insight starts at the live end-of-Iron level and keeps growing; not a fit)")
    print(f"  live end-of-Iron fixture: {a} adults in {n} settlements ({a / n:.1f} each), {sch} scholars; Commons = 4.8 x adults^0.85 per settlement, x1.6")
    for label, cm in (("no Iron Insight modifiers taken", 1.0), ("COM-07 and GOV-08 taken (x1.2075)", 1.05 * 1.15)):
        i_end = insight_per_year(a / n, n, sch, 0, commons_mult=cm)
        implied = i_end * (1 - math.exp(-r0 * t4)) / r0
        print(f"  {label}: Insight at KEY-5 ~{i_end:,.0f}/min; run backwards at r={r0:.2f} over {t4:.0f} min it implies a spend of {implied:,.0f} "
              f"vs Iron's actual {e4_spent:,.0f} ({100 * (implied / e4_spent - 1):+.0f}%)")
    i0 = insight_per_year(a / n, n, sch, 0, commons_mult=1.0)
    implied0 = i0 * (1 - math.exp(-r0 * t4)) / r0
    k = e4_spent / implied0
    print(f"  The Commons-and-roles formula alone explains only {100 / k:.0f}% of what Iron civilizations actually spent, so it is scaled by k = {k:.2f}")
    print("  (Know-how drain, structures, diffusion and everything the fixture leaves out). That k is a calibration to ONE anchor, not a fit.")
    ik = i0 * k
    target, measured = LIVE_E4["iron_target_minutes"], LIVE_E4["key4_to_key5_minutes"]
    print(f"  Minutes to finish E5 if Insight keeps growing at r per minute (Iron target {target:.0f}, Iron measured {measured:.0f}):")
    for name, total in scenarios.items():
        print(f"   {name}: cost {total:,.0f}")
        print(f"     {'r':>5s} | {'uncalibrated':>13s} | {'calibrated (x k)':>17s} | calibrated + E5 Insight projects (+12% on average, rough)")
        for r in (0.03, 0.04, 0.06, 0.08):
            t_a = math.log(1 + total * r / i0) / r
            t_b = math.log(1 + total * r / ik) / r
            t_c = math.log(1 + total * r / (ik * 1.12)) / r
            print(f"     {r:5.2f} | {t_a:13.1f} | {t_b:17.1f} | {t_c:.1f}")
    b_total = list(scenarios.values())[-1]
    print(f"  Era base needed for scenario B to take a target number of minutes (calibrated, scaling every E5 cost; base 6,000 today, cost {b_total:,.0f}):")
    for r in (0.04, 0.06, 0.08):
        cells = []
        for t in (measured, target):
            need = ik * (math.exp(r * t) - 1) / r
            cells.append(f"{t:.0f} min: x{need / b_total:.2f} (base ~{6000 * need / b_total:,.0f})")
        print(f"     r={r:.2f}: " + "; ".join(cells))
    print("  Reading: the uncalibrated formula reproduces less than half of Iron's real spend, so trust the calibrated column. If every civilization")
    print("  researches the whole age as Iron does (scenario B), E5 lands close to Iron's own measured length; a civilization that rushes KEY-6")
    print("  (scenario A) finishes in under half that. E5's length is set by how much of the age the planners research before closing it, and by")
    print("  the era base, not by the keystone's own cost.")


def insight_budget():
    # Multipliers from the catalog text (E4 already held, E5 added). Scope-only, per 26 §1.1.
    e4_commons = 1.05 * 1.15          # COM-07, GOV-08
    e4_scholar = 1.10 * 1.20 * 1.20   # BEL-08, INQ-04, LRN-06 (+20% Insight)
    e5_commons = 1.15                 # COM-10 only; COM-09 acts on writing roles and NUM-08 on trade/admin, so neither touches Commons
    e5_scholar = 1.15 * 1.5 * 1.3     # NUM-09 +15%, LRN-08 x1.5, COM-09 +30% writing roles (an upper bound: not every scholar writes)
    print("\n== Insight budget fixture (02 8.1 formula; settlement counts and role counts are assumptions)")
    print("  Commons = 4.8 x adults^0.85 per settlement, roles at 48 (scholar) and 16 (monastery) Insight/yr, all x1.6 calibration.")
    print("  Rows: adults per settlement, settlements, scholars (1 per academy/university), monasteries.")
    print(f"  {'case':32s} {'E4 stack':>10s} {'E5 stack':>10s} {'gain':>7s}")
    for name, a, n, sch, mon in (
        ("small realm 6 x 120, 6 sch, 0 mon", 120, 6, 6, 0),
        ("mid realm 6 x 250, 6 sch, 6 mon", 250, 6, 6, 6),
        ("big realm 8 x 400, 8 sch, 8 mon", 400, 8, 8, 8),
        ("scholar-heavy 6 x 250, 24 sch, 6 mon", 250, 6, 24, 6),
    ):
        base = insight_per_year(a, n, sch, mon, e4_commons, e4_scholar, 1.0)
        full = insight_per_year(a, n, sch, mon, e4_commons * e5_commons, e4_scholar * e5_scholar, 1.10)
        print(f"  {name:32s} {base:10,.0f} {full:10,.0f} {100 * (full / base - 1):6.1f}%")
    print("  Upper bound: the E5 Insight projects add roughly 23 to 50% on the E4 stack, mostly through scholars, and only where")
    print("  scholars are actually staffed. COM-09, COM-10, NUM-09 and LRN-08 are all on the KEY-6 critical path, so a civilization")
    print("  that races for KEY-6 collects most of this gain late in the era. Against a 4x rise in required rate, the rest")
    print("  has to come from people and towns.")
    return e4_commons, e4_scholar, e5_commons, e5_scholar


def check(projects):
    reg = regulars(projects)
    assert len(reg) == 49, len(reg)
    assert "KEY-6" in projects and set(projects["KEY-6"]["requires"]) == KEY6_REQUIRES
    assert {t: sum(1 for i in reg if theme(i) == t) for t in ("craft", "kin", "reach")} == {"craft": 18, "kin": 13, "reach": 18}
    crit = critical(projects)
    expected = {"COM-09", "COM-10", "NUM-08", "NUM-09", "TRD-08", "TRD-09", "INQ-06", "WAY-08", "BEL-10", "GOV-10", "LRN-07", "LRN-08"}
    assert crit == expected, sorted(crit ^ expected)
    assert BREADTH - len(crit) == 4
    assert all(theme(i) != "craft" for i in crit), "no Craft project is on the KEY-6 path"
    sel = candidate(projects)
    assert len(e5_part(projects, sel)) == BREADTH and KEY6_REQUIRES <= sel and "KEY-6" in sel
    # Cost arithmetic by hand: critical list cost and KEY-6.
    hand = {"COM-09": "M", "COM-10": "D", "NUM-08": "M", "NUM-09": "M", "TRD-08": "I", "TRD-09": "M",
            "INQ-06": "M", "WAY-08": "M", "BEL-10": "S", "GOV-10": "I", "LRN-07": "S", "LRN-08": "S"}
    assert all(projects[i]["type"] == t for i, t in hand.items()), "types drifted from 27's hand check"
    # E5 late doubling (user decision 2026-11-13): hand-checked against the same-era chains in the catalog.
    late_crit = {i for i in crit if late(projects, i)}
    assert sum(cost(projects, i) for i in crit) == sum(6000 * MULT[t] * (2 if i in late_crit else 1) for i, t in hand.items())
    # The depth definition must reproduce main's own E4 overrides exactly (28 numeric E4 projects carry a 28 §7 cost).
    e4_late = {i for i, p in projects.items() if p.get("era") == 4 and late(projects, i)}
    e4_override = {i for i in _COSTS if projects[i].get("era") == 4}
    assert e4_late == e4_override and len(e4_late) == 28, (sorted(e4_late ^ e4_override), len(e4_late))
    assert cost(projects, "KEY-6") == 6000 * 2.5
    # The Craft-only variant must hold 4 craft picks and still include the 12 critical projects.
    cs = candidate(projects, "craft")
    extra = e5_part(projects, cs) - crit
    assert len(extra) == 4 and all(theme(i) == "craft" for i in extra), sorted(extra)
    assert "CHM-07" not in e5_part(projects, sel) and "CHM-07" not in cs
    # Insight formula sanity: one 250-adult town, no roles, is 4.8*250^0.85*1.6.
    assert isclose(insight_per_year(250, 1, 0, 0), 4.8 * 250 ** 0.85 * 1.6)
    assert OUT_SCHOLAR == 48 and OUT_MONASTERY == 16
    print("--check PASS: 49 regular + KEY-6 (18/13/18); KEY-6 closure is the 12 Reach/Kin projects, 4 free picks; "
          "candidate sets reach 16 with CHM-07 excluded; cost and Insight arithmetic")


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    projects = e4.load_catalog()
    if args.check:
        check(projects)
        return
    report(projects)
    insight_budget()


if __name__ == "__main__":
    main()
