#!/usr/bin/env python3
"""Fail-closed WP-E5-5 F-T3 matched-cohort runner and analyzer.

D13's frozen gate is <=15% metal-chain/civilian work in every mature rolling
12-season window. Maturity follows first held kit season plus 12 seasons.
"""
from __future__ import annotations
import argparse, hashlib, json, re, subprocess, sys, tempfile
from datetime import datetime, timezone
from collections import Counter
from fractions import Fraction
from pathlib import Path

SEASONS, PEOPLE, WINDOW, EXPECTED_SEEDS = 80, 200, 12, 40
MIN_ELIGIBLE_PAIRS = 30
ONE_RAW = 1 << 32
WORK_CEILING_RAW = 15 * ONE_RAW // 100
FUEL_DEMAND_MIN = 12
FUEL_SHORT_CEILING_RAW = 5 * ONE_RAW // 100
ARMS = {"treatment": "fief-low-wood-furnace", "control": "fief-low-wood-no-furnace"}
PIN_KEYS = ("source_commit", "source_tree_sha256", "pack_sha256", "release_harness_sha256", "adjacent_sim_sha256", "runner_sha256")
WINDOW_RE = re.compile(r"^F_T3_WINDOW\s+(.*)$")
ARM_RE = re.compile(r"^F_T3_ARM\s+(.*)$")

class GateError(Exception):
    pass

def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for b in iter(lambda: f.read(1024 * 1024), b""):
            h.update(b)
    return h.hexdigest()

def source_tree_sha256(repo: Path) -> str:
    roots = (repo / "src/CivSim.Harness", repo / "src/CivSim.Sim")
    files = sorted(p for root in roots for p in root.rglob("*")
                   if p.is_file() and "bin" not in p.parts and "obj" not in p.parts)
    h = hashlib.sha256()
    for p in files:
        rel = p.relative_to(repo).as_posix().encode()
        data = p.read_bytes()
        h.update(len(rel).to_bytes(8, "big")); h.update(rel)
        h.update(len(data).to_bytes(8, "big")); h.update(data)
    return h.hexdigest()

def actual_pins(repo: Path, pack: Path, harness: Path, sim: Path) -> dict[str, str]:
    commit = subprocess.run(["git", "-C", str(repo), "rev-parse", "HEAD"],
                            check=True, capture_output=True, text=True).stdout.strip()
    return {"source_commit": commit, "source_tree_sha256": source_tree_sha256(repo),
            "pack_sha256": sha256_file(pack), "release_harness_sha256": sha256_file(harness),
            "adjacent_sim_sha256": sha256_file(sim), "runner_sha256": sha256_file(Path(__file__).resolve())}

def check_pins(expected: dict, actual: dict) -> None:
    missing = [k for k in PIN_KEYS if not isinstance(expected.get(k), str) or not expected[k]]
    if missing:
        raise GateError("manifest missing pins: " + ", ".join(missing))
    mismatch = [f"{k} expected={expected[k]} actual={actual.get(k)}"
                for k in PIN_KEYS if expected[k] != actual.get(k)]
    if mismatch:
        raise GateError("pin mismatch: " + "; ".join(mismatch))

def load_manifest(path: Path, supplied_sha: str) -> dict:
    actual_sha = sha256_file(path)
    if actual_sha.lower() != supplied_sha.lower():
        raise GateError(f"manifest SHA-256 mismatch: expected={supplied_sha} actual={actual_sha}")
    try:
        m = json.loads(path.read_text())
    except Exception as e:
        raise GateError(f"manifest JSON invalid: {e}") from e
    if m.get("protocol") != "WP-E5-5/F-T3" or not m.get("preregistration_id") or not m.get("preregistered_at_utc"):
        raise GateError("manifest must identify WP-E5-5/F-T3 and external preregistration id/time")
    seeds = m.get("seeds")
    if not isinstance(seeds, list) or len(seeds) != EXPECTED_SEEDS:
        raise GateError("manifest must contain exactly 40 preregistered seeds")
    if any(type(s) is not int or s < 0 for s in seeds) or len(set(seeds)) != EXPECTED_SEEDS:
        raise GateError("manifest seeds must be 40 distinct nonnegative integers")
    if m.get("fixture") != {"seasons": SEASONS, "people": PEOPLE}:
        raise GateError(f"manifest fixture must be exactly {SEASONS} seasons and {PEOPLE} people")
    if m.get("arms") != ARMS:
        raise GateError("manifest arms must match current FiefScenarios exactly")
    return m

def parse_fields(line: str, regex: re.Pattern) -> dict[str, str]:
    match = regex.match(line.strip())
    if not match:
        return {}
    result = {}
    for part in match.group(1).split():
        if "=" not in part:
            raise GateError(f"bad telemetry field: {part}")
        key, value = part.split("=", 1)
        if key in result:
            raise GateError(f"duplicate telemetry field: {key}")
        result[key] = value
    return result

def parse_log(path: Path, scenario: str, seed: int) -> dict:
    try:
        lines = path.read_text().splitlines()
    except Exception as e:
        raise GateError(f"cannot read {path}: {e}") from e
    arm_rows = [parse_fields(line, ARM_RE) for line in lines if ARM_RE.match(line)]
    arm_rows = [r for r in arm_rows if r]
    if len(arm_rows) != 1:
        raise GateError(f"{path}: expected exactly one F_T3_ARM record, got {len(arm_rows)}")
    arm = arm_rows[0]
    try:
        int_fields = ("seed", "first_kit_season", "mature_candidates", "measured_windows",
                      "no_exposure_windows", "fuel_demand_seasons", "fuel_short_seasons",
                      "fuel_short_rate_raw", "planned_charcoal_raw", "unmet_charcoal_raw",
                      "fuel_unmet_share_raw", "no_fuel_share_raw", "forest_clear_crossings",
                      "wood_balance_residual_raw")
        for key in int_fields:
            arm[key] = int(arm[key])
        windows = []
        for line in lines:
            row = parse_fields(line, WINDOW_RE)
            if not row:
                continue
            for key in ("seed", "start", "end", "labor_raw", "metal_raw", "no_fuel_seasons"):
                row[key] = int(row[key])
            windows.append(row)
    except (KeyError, ValueError) as e:
        raise GateError(f"{path}: invalid F-T3 telemetry: {e}") from e
    if arm["scenario"] != scenario or arm["seed"] != seed:
        raise GateError(f"{path}: arm/seed identity mismatch")
    if len(windows) != arm["mature_candidates"]:
        raise GateError(f"{path}: mature candidate/window record count mismatch")
    measured = [w for w in windows if w.get("status") == "measured"]
    no_exposure = [w for w in windows if w.get("status") == "no_exposure"]
    if len(measured) != arm["measured_windows"] or len(no_exposure) != arm["no_exposure_windows"] or len(measured)+len(no_exposure) != len(windows):
        raise GateError(f"{path}: window disposition totals disagree")
    for row in windows:
        if row.get("scenario") != scenario or row["seed"] != seed:
            raise GateError(f"{path}: window arm/seed identity mismatch")
        if row["start"] != row["end"] - WINDOW + 1 or not 1 <= row["start"] <= row["end"] <= SEASONS:
            raise GateError(f"{path}: invalid rolling window span")
        if row["labor_raw"] < 0 or row["metal_raw"] < 0 or row["metal_raw"] > row["labor_raw"]:
            raise GateError(f"{path}: impossible labor counters")
        if row["status"] == "measured" and row["labor_raw"] <= 0:
            raise GateError(f"{path}: measured window has no exposure")
        if row["status"] == "no_exposure" and (row["labor_raw"] != 0 or row["metal_raw"] != 0):
            raise GateError(f"{path}: no-exposure row has nonzero labor")
        if not 0 <= row["no_fuel_seasons"] <= WINDOW:
            raise GateError(f"{path}: invalid no-fuel season count")
    first_kit = arm["first_kit_season"]
    expected = max(0, SEASONS - first_kit - 11) if 1 <= first_kit <= SEASONS else 0
    if arm["mature_candidates"] != expected:
        raise GateError(f"{path}: mature count disagrees with first-kit-plus-12 rule")
    expected_ends = list(range(first_kit + WINDOW, SEASONS + 1)) if expected else []
    if [w["end"] for w in windows] != expected_ends:
        raise GateError(f"{path}: missing, duplicate, or unordered rolling windows")
    for row in measured:
        row["share_raw"] = (row["metal_raw"] << 32) // row["labor_raw"]
        row["pass"] = row["share_raw"] <= WORK_CEILING_RAW
    arm["windows"] = windows
    arm["max_share_raw"] = max((w["share_raw"] for w in measured), default=None)
    arm["work_fail_windows"] = [w for w in measured if not w["pass"]]
    if arm["fuel_demand_seasons"] < 0 or arm["fuel_short_seasons"] < 0 or arm["fuel_short_seasons"] > arm["fuel_demand_seasons"]:
        raise GateError(f"{path}: invalid fuel-demand accounting")
    if arm["planned_charcoal_raw"] < 0 or arm["unmet_charcoal_raw"] < 0 or arm["unmet_charcoal_raw"] > arm["planned_charcoal_raw"]:
        raise GateError(f"{path}: invalid planned/unmet-charcoal accounting")
    return arm

def analyze(records: list[dict], seeds: list[int]) -> dict:
    errors, pairs = [], []
    expected = {(arm, seed) for arm in ARMS for seed in seeds}
    counts = Counter((r["arm"], r["seed"]) for r in records)
    by_key = {(r["arm"], r["seed"]): r for r in records}
    for key in sorted(expected):
        if counts[key] != 1:
            errors.append({"kind": "arm_seed_row_count", "arm": key[0], "seed": key[1], "count": counts[key]})
    for key in sorted(set(counts) - expected):
        errors.append({"kind": "unexpected_arm_seed", "arm": key[0], "seed": key[1], "count": counts[key]})
    for seed in seeds:
        if counts[("treatment", seed)] != 1 or counts[("control", seed)] != 1:
            continue
        t, c = by_key[("treatment", seed)], by_key[("control", seed)]
        if not t["windows"]:
            work = "CENSORED_NO_MATURE_WINDOW"
        elif t["work_fail_windows"]:
            work = "FAIL"
        elif t["no_exposure_windows"]:
            work = "NO_EXPOSURE_INCOMPLETE"
        else:
            work = "PASS"
        if not c["windows"]:
            control = "CENSORED_NO_MATURE_WINDOW"
        elif c["no_exposure_windows"]:
            control = "NO_EXPOSURE_INCOMPLETE"
        else:
            control = "OBSERVED"
        if t["no_fuel_share_raw"] > FUEL_SHORT_CEILING_RAW:
            fuel = "FAIL_NO_FUEL_SHARE"
        elif t["fuel_demand_seasons"] < FUEL_DEMAND_MIN:
            fuel = "CENSORED_LOW_DEMAND"
        elif (t["fuel_short_rate_raw"] <= FUEL_SHORT_CEILING_RAW and
              t["fuel_unmet_share_raw"] <= ONE_RAW // 100):
            fuel = "PASS"
        else:
            fuel = "FAIL"
        extra_clearings = max(0, t["forest_clear_crossings"] - c["forest_clear_crossings"])
        clearing = "FAIL" if extra_clearings else "PASS"
        fail = work == "FAIL" or fuel.startswith("FAIL") or clearing == "FAIL" or t["wood_balance_residual_raw"] != 0
        incomplete = work in ("CENSORED_NO_MATURE_WINDOW", "NO_EXPOSURE_INCOMPLETE") or control != "OBSERVED" or fuel == "CENSORED_LOW_DEMAND"
        pairs.append({"seed": seed, "treatment": t, "control": c, "work_status": work,
                      "control_window_status": control, "fuel_status": fuel,
                      "furnace_attributed_clearings": extra_clearings, "clearing_status": clearing,
                      "material_residual_status": "PASS" if t["wood_balance_residual_raw"] == 0 else "FAIL",
                      "status": "FAIL" if fail else "INCOMPLETE" if incomplete else "PASS"})
    eligible = [p for p in pairs if p["status"] == "PASS"]
    censored = [p for p in pairs if p["status"] == "INCOMPLETE"]
    if any(p["status"] == "FAIL" for p in pairs):
        status = "FAIL"
    elif errors or len(pairs) != len(seeds) or len(eligible) < MIN_ELIGIBLE_PAIRS:
        status = "INCOMPLETE"
    else:
        status = "PASS"
    return {"protocol": "WP-E5-5/F-T3", "gate_rule": "D13: <=15% for every mature rolling 12-season window",
            "threshold_raw": WORK_CEILING_RAW, "seasons": SEASONS, "people": PEOPLE, "window_seasons": WINDOW,
            "expected_seed_count": len(seeds), "expected_arm_seed_rows": len(expected),
            "received_arm_seed_rows": len(records), "pairs": pairs, "errors": errors,
            "cohort_eligibility": {"minimum_eligible_pairs": MIN_ELIGIBLE_PAIRS,
                                   "eligible_pairs": len(eligible), "eligible_seeds": [p["seed"] for p in eligible],
                                   "censored_pairs": len(censored), "censored_seeds": [p["seed"] for p in censored]},
            "status": status}

def fixture_checks() -> None:
    def arm(name, shares=None, seed=7, no_exp=0, first_kit=5, demand=12, fuel_raw=0, clear=0):
        shares = list(shares if shares is not None else [0.05] * 48)
        rows = []
        for i, share in enumerate(shares):
            end = first_kit + WINDOW + i
            metal = int(share * ONE_RAW)
            rows.append({"start": end-WINDOW+1, "end": end, "status": "measured", "labor_raw": ONE_RAW,
                         "metal_raw": metal, "share_raw": metal, "pass": metal <= WORK_CEILING_RAW})
        for row in rows[:no_exp]:
            row.update(status="no_exposure", labor_raw=0, metal_raw=0, share_raw=None, **{"pass": False})
        return {"arm": name, "seed": seed, "windows": rows, "mature_candidates": len(rows),
                "measured_windows": len(rows)-no_exp, "no_exposure_windows": no_exp, "first_kit_season": first_kit,
                "fuel_demand_seasons": demand, "fuel_short_seasons": 0, "fuel_short_rate_raw": fuel_raw,
                "planned_charcoal_raw": ONE_RAW, "unmet_charcoal_raw": 0, "fuel_unmet_share_raw": 0,
                "no_fuel_share_raw": 0, "forest_clear_crossings": clear, "wood_balance_residual_raw": 0,
                "work_fail_windows": [r for r in rows if r["status"] == "measured" and not r["pass"]]}
    seeds = [7]
    missing = analyze([arm("treatment")], seeds)
    assert missing["status"] == "INCOMPLETE" and missing["errors"]
    duplicate = analyze([arm("treatment"), arm("treatment"), arm("control")], seeds)
    assert duplicate["status"] == "INCOMPLETE" and duplicate["errors"]
    shares = [0.01] * 47 + [0.20]
    assert sum(shares)/len(shares) < 0.15
    result = analyze([arm("treatment", shares=shares), arm("control")], seeds)
    assert result["status"] == "FAIL" and result["pairs"][0]["treatment"]["work_fail_windows"]
    no_exposure = analyze([arm("treatment", no_exp=1), arm("control")], seeds)
    assert no_exposure["status"] == "INCOMPLETE" and no_exposure["pairs"][0]["work_status"] == "NO_EXPOSURE_INCOMPLETE"
    many_seeds = list(range(40))
    records = []
    for seed in many_seeds:
        demand = 12 if seed < 30 else 0
        records.extend([arm("treatment", seed=seed, demand=demand), arm("control", seed=seed)])
    exposure_sufficient = analyze(records, many_seeds)
    assert exposure_sufficient["status"] == "PASS"
    assert exposure_sufficient["cohort_eligibility"]["eligible_pairs"] == 30
    assert exposure_sufficient["cohort_eligibility"]["censored_pairs"] == 10
    records[58] = arm("treatment", seed=29, demand=0)
    exposure_insufficient = analyze(records, many_seeds)
    assert exposure_insufficient["status"] == "INCOMPLETE"
    assert exposure_insufficient["cohort_eligibility"]["eligible_pairs"] == 29
    # The opt-in harness telemetry parser accepts the exact machine-row shape and maturity boundary.
    with tempfile.TemporaryDirectory() as tmp:
        path = Path(tmp) / "one-arm.txt"
        path.write_text(
            "F_T3_WINDOW scenario=fief-low-wood-furnace seed=7 start=69 end=80 status=measured "
            f"labor_raw={ONE_RAW} metal_raw={ONE_RAW // 20} no_fuel_seasons=0\n" +
            "F_T3_ARM scenario=fief-low-wood-furnace seed=7 first_kit_season=68 mature_candidates=1 "
            "measured_windows=1 no_exposure_windows=0 fuel_demand_seasons=12 fuel_short_seasons=0 "
            f"fuel_short_rate_raw=0 planned_charcoal_raw={ONE_RAW} unmet_charcoal_raw=0 fuel_unmet_share_raw=0 "
            "no_fuel_share_raw=0 forest_clear_crossings=0 wood_balance_residual_raw=0\n")
        parsed = parse_log(path, ARMS["treatment"], 7)
        assert parsed["max_share_raw"] == ONE_RAW // 20 and len(parsed["windows"]) == 1
    pins = {k: "expected" for k in PIN_KEYS}
    try:
        check_pins(pins, {k: "wrong" for k in PIN_KEYS})
    except GateError as e:
        assert "pin mismatch" in str(e)
    else:
        raise AssertionError("wrong pins accepted")
    print("fixture checks passed: missing/duplicate arms, wrong pins, every-window semantics, censor sufficiency, no-exposure")

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--manifest-sha256")
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--pack", type=Path)
    parser.add_argument("--harness", type=Path)
    parser.add_argument("--sim", type=Path)
    parser.add_argument("--output-root", type=Path)
    parser.add_argument("--self-test", action="store_true")
    a = parser.parse_args()
    if a.self_test:
        fixture_checks(); return 0
    try:
        if not all((a.manifest, a.manifest_sha256, a.pack, a.harness, a.sim, a.output_root)):
            raise GateError("require external --manifest and --manifest-sha256, --pack, --harness, --sim, --output-root")
        manifest = load_manifest(a.manifest, a.manifest_sha256)
        if a.harness.name != "CivSim.Harness.dll" or "Release" not in a.harness.parts:
            raise GateError("--harness must be Release CivSim.Harness.dll")
        if a.sim.name != "CivSim.Sim.dll" or a.sim.parent != a.harness.parent:
            raise GateError("--sim must be adjacent Release CivSim.Sim.dll")
        before = actual_pins(a.repo, a.pack, a.harness, a.sim)
        check_pins(manifest["pins"], before)
        if a.output_root.exists():
            raise GateError("output root already exists; use a unique new root")
        a.output_root.mkdir(parents=True, exist_ok=False)
        (a.output_root / "manifest.json").write_bytes(a.manifest.read_bytes())
        records, run_errors, run_metadata = [], [], []
        started_at_utc = datetime.now(timezone.utc).isoformat()
        for seed in manifest["seeds"]:
            for arm_name, scenario in ARMS.items():
                log = a.output_root / f"{arm_name}-{seed}.txt"
                cmd = ["dotnet", str(a.harness), "iron-fuel", "--pack", str(a.pack), "--fief",
                       "--scenario", scenario, "--seeds", str(seed), "--seasons", str(SEASONS),
                       "--people", str(PEOPLE), "--trace-fief-seasons"]
                try:
                    proc = subprocess.run(cmd, capture_output=True, text=True)
                    log.write_text(proc.stdout + proc.stderr)
                except OSError as e:
                    log.write_text(f"runner execution error: {e}\n")
                    run_metadata.append({"arm": arm_name, "seed": seed, "command": cmd, "exit_code": None,
                                         "error": str(e), "log": log.name, "log_sha256": sha256_file(log)})
                    run_errors.append({"arm": arm_name, "seed": seed, "error": str(e), "log": log.name})
                    continue
                run_metadata.append({"arm": arm_name, "seed": seed, "command": cmd, "exit_code": proc.returncode,
                                     "log": log.name, "log_sha256": sha256_file(log)})
                if proc.returncode != 0:
                    run_errors.append({"arm": arm_name, "seed": seed, "exit_code": proc.returncode, "log": log.name})
                    continue
                try:
                    records.append({"arm": arm_name, **parse_log(log, scenario, seed)})
                except Exception as e:
                    run_errors.append({"arm": arm_name, "seed": seed, "error": str(e), "log": log.name})
        after = actual_pins(a.repo, a.pack, a.harness, a.sim)
        drift = [{"pin": k, "expected": manifest["pins"][k], "actual": after.get(k)}
                 for k in PIN_KEYS if manifest["pins"][k] != after.get(k)]
        result = analyze(records, manifest["seeds"])
        result["run_errors"], result["pin_drift"] = run_errors, drift
        result["provenance"] = {"manifest_sha256": sha256_file(a.manifest), "pins_before": before,
                                "pins_after": after, "started_at_utc": started_at_utc,
                                "finished_at_utc": datetime.now(timezone.utc).isoformat()}
        result["runs"] = run_metadata
        if drift or run_errors:
            result["status"] = "INCOMPLETE"
        (a.output_root / "analysis.json").write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps({k: result[k] for k in ("status", "received_arm_seed_rows", "errors", "run_errors", "pin_drift")}, indent=2))
        return 0 if result["status"] == "PASS" else 1
    except (GateError, OSError, subprocess.CalledProcessError) as e:
        print(f"error: {e}", file=sys.stderr)
        return 2

if __name__ == "__main__":
    raise SystemExit(main())
