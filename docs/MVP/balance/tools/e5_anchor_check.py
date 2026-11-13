#!/usr/bin/env python3
"""Drift check for the E5 oracles (docs/MVP/27 §12 'E4 may change under E5'; balance/m12-wp1-oracle-fit.md).

The four e5_*_oracle.py scripts copy a handful of constants from the live Iron simulation and registries. This script reads the
current source and content and reports, line by line, whether each assumed constant still matches (OK) or has drifted (DRIFT).
Run it after every rebase onto an Iron change, before trusting any oracle number. It also prints the live E4 anchor block that
`e5_research_oracle.py` uses (edit e5_anchors.py when a newer natural-play run is recorded).

    python3 docs/MVP/balance/tools/e5_anchor_check.py          # report; exit 1 if anything drifted
    python3 docs/MVP/balance/tools/e5_anchor_check.py --check  # same, quiet on success
"""
from __future__ import annotations

import argparse
import importlib.util
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]


def load(name):
    spec = importlib.util.spec_from_file_location(name, HERE / f"{name}.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


from e5_anchors import LIVE_E4  # noqa: E402


def text(rel):
    return (ROOT / rel).read_text()


def ratio(src, name):
    m = re.search(rf"{name}\s*=\s*Fix64\.FromRatio\((\d+),\s*(\d+)\)", src)
    return None if not m else int(m.group(1)) / int(m.group(2))


def const_int(src, name):
    m = re.search(rf"{name}\s*=\s*(\d+)\s*;", src)
    return None if not m else int(m.group(1))


def yaml_num(src, key, anchor):
    """First `key: number` on the line containing `anchor`."""
    for line in src.splitlines():
        if anchor in line:
            m = re.search(rf"{key}:\s*([0-9.]+)", line)
            if m:
                return float(m.group(1))
    return None


def recipe(src, rid):
    for line in src.splitlines():
        if re.search(rf"\{{id: {rid},", line):
            ins = dict((k, float(v)) for k, v in re.findall(r"(G-[A-Z0-9]+): ([0-9.]+)", line.split("inputs:")[1].split("outputs:")[0]))
            outs = dict((k, float(v)) for k, v in re.findall(r"(G-[A-Z0-9]+): ([0-9.]+)", line.split("outputs:")[1]))
            lab = re.search(r"labor: ([0-9.]+)", line)
            return ins, outs, float(lab.group(1)) if lab else None
    return None


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    rows = []

    def expect(label, actual, assumed, where):
        ok = actual is not None and abs(actual - assumed) < 1e-9
        rows.append((ok, label, actual, assumed, where))

    raids = text("src/CivSim.Sim/Politics/Raids.cs")
    combat = load("e5_combat_oracle")
    expect("BreachPerEngine", ratio(raids, "BreachPerEngine"), combat.BREACH_PER_ENGINE, "Raids.cs vs e5_combat_oracle")
    expect("CrewPerEngine", const_int(raids, "CrewPerEngine"), combat.CREW_PER_ENGINE, "Raids.cs vs e5_combat_oracle")
    expect("MaxEngines", const_int(raids, "MaxEngines"), combat.MAX_ENGINES, "Raids.cs vs e5_combat_oracle")
    m = re.search(r"WallDefenseMultiplier\s*=\s*Fix64\.FromInt\((\d+)\)", raids)
    expect("WallDefenseMultiplier (intact wall)", float(m.group(1)) if m else None, combat.WALL, "Raids.cs vs e5_combat_oracle")
    expect("IntactTowerWall", ratio(raids, "IntactTowerWall"), combat.TOWER_WALL, "Raids.cs vs e5_combat_oracle")
    expect("CastleStrongpoint (E4 garrisoned castle)", ratio(raids, "CastleStrongpoint"), 1.25, "Raids.cs vs 26/27 text (castle x1.25)")

    eras = text("content/registries/eras.yaml")
    research = load("e5_research_oracle")
    for era in (3, 4, 5):
        expect(f"EraBase E{era}", yaml_num(eras, "base_cost", f"{{id: {era},"), float(research.ERA_BASE[era]), "eras.yaml vs e5_research_oracle")
    expect("E5 routine reach", yaml_num(eras, "reach", "{id: 5,"), 25.0, "eras.yaml vs combat/geography audit E5Reach")
    expect("E5 claim radius", yaml_num(eras, "claim_radius", "{id: 5,"), 5.0, "eras.yaml vs geography audit ClaimRadius")

    bal = text("content/constants/balance.yaml")
    for cid, assumed in (("CommonsCoef", research.COMMONS_COEF), ("CommonsExp", research.COMMONS_EXP), ("InsightCalibration", research.CALIB),
                         ("RoleScale", float(research.ROLE_SCALE)), ("OutScholar", 6.0), ("OutMonastery", 2.0)):
        expect(cid, yaml_num(bal, "value", f"{{id: {cid},"), assumed, "balance.yaml vs e5_research_oracle")

    rec = text("content/registries/recipes.yaml")
    metal = load("e4_metal_oracle")
    for rid, (ins, outs, lab) in {
        "forge_wrought": ({"G-IRON": metal.FORGE_WROUGHT["iron"], "G-CHAR": metal.FORGE_WROUGHT["charcoal"]}, {"G-WROUGHT": metal.FORGE_WROUGHT["wrought"]}, metal.FORGE_WROUGHT["ws"]),
        "carburize_steel": ({"G-WROUGHT": metal.CARBURIZE["wrought"], "G-CHAR": metal.CARBURIZE["charcoal"]}, {"G-STEEL": metal.CARBURIZE["steel"]}, metal.CARBURIZE["ws"]),
        "make_tool11": ({"G-WROUGHT": metal.MAKE_T11["wrought"], "G-CHAR": metal.MAKE_T11["charcoal"]}, {"G-TOOL11": 1.0}, metal.MAKE_T11["ws"]),
        "make_tool12": ({"G-STEEL": metal.MAKE_T12["steel"], "G-CHAR": metal.MAKE_T12["charcoal"]}, {"G-TOOL12": 1.0}, metal.MAKE_T12["ws"]),
    }.items():
        got = recipe(rec, rid)
        ok = got is not None and got[0] == ins and got[1] == outs and got[2] == lab
        rows.append((ok, f"INFO e4_metal_oracle default for recipe {rid}", got, (ins, outs, lab), "recipes.yaml vs e4_metal_oracle.py defaults; e5 oracles read the registry"))

    tools = text("content/registries/tools.yaml")
    ok = yaml_num(tools, "service_ws", "{id: t12,") == metal.SERVICE_WS["T12"]
    rows.append((ok, "INFO e4_metal_oracle default for T12 service life (ws)", yaml_num(tools, "service_ws", "{id: t12,"), metal.SERVICE_WS["T12"], "tools.yaml vs e4_metal_oracle.py defaults; e5 oracles read the registry"))
    # What the e5 oracles actually use after apply_iron_registry must equal the registry.
    from e5_anchors import apply_iron_registry
    applied = apply_iron_registry(load("e4_metal_oracle"))
    expect("applied T11 service life (ws)", applied.SERVICE_WS["T11"], yaml_num(tools, "service_ws", "{id: t11,"), "e5_anchors.apply_iron_registry vs tools.yaml")
    expect("applied T11 wrought per kit", applied.MAKE_T11["wrought"], recipe(rec, "make_tool11")[0]["G-WROUGHT"], "e5_anchors.apply_iron_registry vs recipes.yaml")

    bad = [r for r in rows if not r[0] and not r[1].startswith('INFO')]
    if not args.check or bad:
        for ok, label, actual, assumed, where in rows:
            print(f"{'OK   ' if ok else ('INFO ' if label.startswith('INFO') else 'DRIFT')} {label}: source {actual!r}, oracle assumes {assumed!r}  [{where}]")
        print(f"\nLive E4 anchor in use: {LIVE_E4['label']}: KEY-4 to KEY-5 {LIVE_E4['key4_to_key5_minutes']} min")
    if bad:
        print(f"\n{len(bad)} constant(s) drifted: update the oracle inputs, then rerun every e5_*_oracle.py --check.")
        return 1
    print(f"--check PASS: {len(rows)} assumed constants match the current source and content")
    return 0


if __name__ == "__main__":
    sys.exit(main())
