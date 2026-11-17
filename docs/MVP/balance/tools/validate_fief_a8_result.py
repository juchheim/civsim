#!/usr/bin/env python3
"""Fail-closed validator for a frozen A8 whole-match result directory.

The input pins file is created before execution and must contain:
head, sourceAggregateSha256, packSha256, harnessSha256, simSha256,
testSourceSha256, testAssemblySha256, seeds, minutes, intervalSeasons.
It also records the exact A8 runner, validator, and source-hash-helper hashes.
The run directory must contain run_metadata.json, a8-summary.json, and
a8-rows.jsonl. This validator deliberately does not turn missing exposure or
unapplied positive controls into a pass.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


PIN_KEYS = {
    "head", "sourceAggregateSha256", "packSha256", "harnessSha256",
    "simSha256", "testSourceSha256", "testAssemblySha256", "seeds",
    "runnerSha256", "validatorSha256", "hashHelperSha256", "minutes", "intervalSeasons",
}
VISIBLE = {"visible-deposit", "visible-stock", "visible-fleet", "visible-horse"}
UNSEEN = {"unseen-strict", "unseen-composite", "unseen-horse"}
REQUIRED_KINDS = {"null-control", *UNSEEN, *VISIBLE, "horse-reference"}
EXPOSURE_FIELDS = {
    "deposits": "deposits", "powderCannon": "powder",
    "fleetCrew": "fleet", "horses": "horses", "peopleStoresResearch": "people",
}


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    require(isinstance(value, dict), f"{path}: expected JSON object")
    return value


def load_rows(path: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if not line.strip():
            continue
        try:
            row = json.loads(line)
        except json.JSONDecodeError as exc:
            raise ValueError(f"{path}:{number}: invalid JSONL row: {exc}") from exc
        require(isinstance(row, dict), f"{path}:{number}: expected row object")
        rows.append(row)
    require(bool(rows), f"{path}: no rows")
    return rows


def validate(run_dir: Path, pins_path: Path) -> dict[str, Any]:
    run_dir, pins_path = run_dir.resolve(), pins_path.resolve()
    pins = read_json(pins_path)
    require(PIN_KEYS <= pins.keys(), f"pins missing: {sorted(PIN_KEYS - pins.keys())}")
    seeds = pins["seeds"]
    require(isinstance(seeds, list) and seeds and all(type(x) is int for x in seeds),
            "pins.seeds must be a nonempty list of integer seeds")
    require(len(seeds) == len(set(seeds)), "pins contain duplicate seeds")
    require(pins["minutes"] == 64 and pins["intervalSeasons"] == 16,
            "A8 protocol must use 64 simulated minutes and 16-season checkpoints")

    metadata_path = run_dir / "run_metadata.json"
    summary_path = run_dir / "a8-summary.json"
    rows_path = run_dir / "a8-rows.jsonl"
    for path in (metadata_path, summary_path, rows_path):
        require(path.is_file(), f"missing required artifact: {path}")
    meta = read_json(metadata_path)
    summary = read_json(summary_path)
    require(summary.get("schemaVersion") == 2, "A8 summary schemaVersion must be 2 (seat-isolated eligibility accounting)")

    require(meta.get("status") == "completed", f"run status is not completed: {meta.get('status')!r}")
    require(meta.get("unchangedDuringRun") is True, "run inputs changed or were not verified after execution")
    output = meta.get("output")
    require(isinstance(output, dict) and output.get("exitCode") == 0, "A8 test process did not exit successfully")
    require(meta.get("testSourceSha256") == pins["testSourceSha256"], "A8 test source SHA mismatch")
    tool_pins = meta.get("toolPins")
    require(isinstance(tool_pins, dict)
            and tool_pins.get("runnerSha256") == pins["runnerSha256"]
            and tool_pins.get("validatorSha256") == pins["validatorSha256"]
            and tool_pins.get("hashHelperSha256") == pins["hashHelperSha256"],
            "A8 runner/validator/source-helper SHA mismatch")
    test_binaries = meta.get("testBinaries")
    require(isinstance(test_binaries, dict)
            and test_binaries.get("CivSim.Tests.dll") == pins["testAssemblySha256"],
            "A8 test assembly SHA mismatch")
    pre = meta.get("preflight", {})
    snap = pre.get("snapshot", {})
    bins = pre.get("binaries", {})
    require(snap.get("head") == pins["head"], "HEAD mismatch")
    require(snap.get("contentSrcGameAggregateSha256") == pins["sourceAggregateSha256"],
            "source aggregate mismatch")
    require(bins.get("pack") == pins["packSha256"], "run pack SHA mismatch")
    require(bins.get("harnessDll") == pins["harnessSha256"], "run Harness SHA mismatch")
    require(bins.get("simDll") == pins["simSha256"], "run Sim SHA mismatch")
    require(meta.get("seeds") == seeds, "run seed list mismatch")
    env = meta.get("env", {})
    require(env.get("CIVSIM_A8_MINUTES") == str(pins["minutes"]), "run horizon mismatch")
    require(env.get("CIVSIM_A8_INTERVAL") == str(pins["intervalSeasons"]), "run interval mismatch")

    artifacts = meta.get("artifacts", {})
    for name, path in (("a8-summary.json", summary_path), ("a8-rows.jsonl", rows_path)):
        record = artifacts.get(name)
        require(isinstance(record, dict), f"run metadata missing artifact record for {name}")
        require(record.get("sha256") == sha256(path), f"artifact SHA mismatch for {name}")
        require(record.get("bytes") == path.stat().st_size, f"artifact size mismatch for {name}")

    require(summary.get("packSha256") == pins["packSha256"], "summary pack SHA mismatch")
    require(summary.get("simAssemblySha256") == pins["simSha256"], "summary Sim SHA mismatch")
    require(summary.get("seeds") == seeds, "summary seed list mismatch")
    require(summary.get("minutes") == pins["minutes"], "summary horizon mismatch")
    require(summary.get("intervalSeasons") == pins["intervalSeasons"], "summary interval mismatch")

    rows = load_rows(rows_path)
    counts: Counter[str] = Counter()
    applied: Counter[str] = Counter()
    passed: Counter[str] = Counter()
    row_seeds: dict[str, set[int]] = defaultdict(set)
    failures: list[str] = []
    exposure: Counter[str] = Counter()
    for index, row in enumerate(rows, 1):
        kind = row.get("kind")
        require(kind in REQUIRED_KINDS, f"row {index}: unknown or missing kind {kind!r}")
        seed = row.get("seed")
        require(type(seed) is int and seed in seeds, f"row {index}: seed outside frozen manifest")
        require(type(row.get("applied")) is bool, f"row {index}: applied must be boolean")
        require(type(row.get("pass")) is bool, f"row {index}: pass must be boolean")
        require(type(row.get("eligible")) is bool, f"row {index}: eligible must be boolean")
        require(type(row.get("observerHasSettlement")) is bool,
                f"row {index}: observerHasSettlement must be boolean")
        if not row["eligible"]:
            require(not row["applied"] and row["observerHasSettlement"] is False
                    and row.get("notApplied") == "observer has no settlement",
                    f"row {index}: ineligible observer must have no settlement and the exact reason")
        else:
            require(row["observerHasSettlement"] is True,
                    f"row {index}: eligible observer has no settlement")
        diffs = row.get("diffs")
        require(isinstance(diffs, list) and all(isinstance(diff, str) for diff in diffs),
                f"row {index}: diffs must be a list of strings")
        counts[kind] += 1
        row_seeds[kind].add(seed)
        if row["eligible"] and row["applied"]:
            applied[kind] += 1
        if row["eligible"] and row["applied"] and row["pass"]:
            passed[kind] += 1

        # Do not trust the harness's derived pass bit on its own. In particular,
        # the whole-match horse mutation (AddHorseTriple) compares against a
        # constructed reference clone, and must be held to the same raw-diff
        # rule as the ordinary unseen-state mutations.
        if row["eligible"] and row["applied"] and (kind == "null-control" or kind in UNSEEN):
            require(row["pass"] == (len(diffs) == 0),
                    f"row {index}: {kind} pass bit disagrees with raw diffs")
        elif kind in VISIBLE and row["eligible"] and row["applied"]:
            require(row["pass"] == (len(diffs) > 0),
                    f"row {index}: {kind} detection bit disagrees with raw diffs")

        if kind in {"unseen-strict", "unseen-composite", "unseen-horse"} and row["eligible"] and row["applied"]:
            for category, field in EXPOSURE_FIELDS.items():
                value = row.get(field)
                require(type(value) is int and value >= 0, f"row {index}: invalid {field} exposure")
                if value > 0:
                    exposure[category] += value
        if kind == "null-control" and row["eligible"] and (not row["applied"] or not row["pass"]):
            failures.append(f"null-control row {index} was unapplied or differed")
        elif kind in UNSEEN and row["eligible"] and (not row["applied"] or not row["pass"]):
            failures.append(f"{kind} row {index} was unapplied or had an observer/decision diff")
        elif kind in VISIBLE and row["eligible"] and (not row["applied"] or not row["pass"]):
            failures.append(f"{kind} row {index} was unapplied or not detected")

    for kind in REQUIRED_KINDS:
        require(counts[kind] > 0, f"required row kind absent: {kind}")
        require(row_seeds[kind] == set(seeds), f"{kind} does not cover every frozen seed")
    for kind in VISIBLE:
        for seed in seeds:
            require(any(row["kind"] == kind and row["seed"] == seed and row["eligible"] for row in rows),
                    f"{kind} has no eligible checkpoint for seed {seed}")
    no_exposure = sorted(category for category in EXPOSURE_FIELDS if exposure[category] == 0)
    if no_exposure:
        failures.append(f"no unseen-state exposure for categories: {', '.join(no_exposure)}")
    exposure_by_seed: dict[int, dict[str, int]] = {}
    for seed in seeds:
        exposure_by_seed[seed] = {
            category: sum(1 for row in rows
                          if row["seed"] == seed and row["kind"] in {"unseen-strict", "unseen-composite", "unseen-horse"}
                          and row["eligible"] and row["applied"] and row.get(field, 0) > 0)
            for category, field in EXPOSURE_FIELDS.items()
        }
        missing = sorted(category for category, count in exposure_by_seed[seed].items() if count == 0)
        if missing:
            failures.append(f"seed {seed} has no unseen-state exposure for categories: {', '.join(missing)}")
    require(summary.get("unseenExposureBySeed") == {str(seed): values for seed, values in exposure_by_seed.items()},
            "per-seed category exposure summary disagrees with raw JSONL")
    computed_exposure_totals = {
        category: sum(1 for row in rows
                      if row["kind"] in {"unseen-strict", "unseen-composite", "unseen-horse"}
                      and row["eligible"] and row["applied"] and row.get(field, 0) > 0)
        for category, field in EXPOSURE_FIELDS.items()
    }
    require(summary.get("unseenAppliedByCategory") == computed_exposure_totals,
            "unseen category exposure totals disagree with raw JSONL")
    computed_seed_totals = {
        category: sum(1 for seed in seeds if exposure_by_seed[seed][category] > 0)
        for category in EXPOSURE_FIELDS
    }
    require(summary.get("unseenMatchesWithCategory") == computed_seed_totals,
            "unseen category seed coverage disagrees with raw JSONL")

    require(summary.get("observerCheckpointsTotal") == len(rows), "summary row total disagrees with raw JSONL")
    require(summary.get("cloneChecksumMismatches") == 0, "clone checksum mismatch recorded")
    strict_failures = sum(1 for row in rows if row["kind"] in {"unseen-strict", "unseen-horse"} and row["eligible"] and not row["pass"])
    composite_failures = sum(1 for row in rows if row["kind"] == "unseen-composite" and row["eligible"] and not row["pass"])
    null_failures = sum(1 for row in rows if row["kind"] == "null-control" and row["eligible"] and not row["pass"])
    unseen_not_applied = sum(1 for row in rows if row["kind"] in {"unseen-strict", "unseen-horse"} and row["eligible"] and not row["applied"])
    unseen_ineligible = sum(1 for row in rows if row["kind"] in {"unseen-strict", "unseen-horse"} and not row["eligible"])
    require(summary.get("unseenInvarianceFailures") == strict_failures,
            "strict/horse unseen failure count disagrees with raw JSONL")
    require(summary.get("unseenNotApplied") == unseen_not_applied, "unseen not-applied count disagrees with raw JSONL")
    require(summary.get("unseenIneligible") == unseen_ineligible, "unseen ineligible count disagrees with raw JSONL")
    composite = summary.get("tierBComposite", {})
    require(composite.get("withInputOrDecisionDiffs") == composite_failures,
            "Tier-B composite failure count disagrees with raw JSONL")
    require(composite.get("notApplied") == sum(1 for row in rows if row["kind"] == "unseen-composite" and row["eligible"] and not row["applied"]),
            "Tier-B not-applied count disagrees with raw JSONL")
    require(composite.get("ineligible") == sum(1 for row in rows if row["kind"] == "unseen-composite" and not row["eligible"]),
            "Tier-B ineligible count disagrees with raw JSONL")
    nulls = summary.get("nullControls", {})
    require(nulls.get("withDiffs") == null_failures, "null-control failure count disagrees with raw JSONL")
    require(nulls.get("notApplied") == sum(1 for row in rows if row["kind"] == "null-control" and row["eligible"] and not row["applied"]),
            "null-control not-applied count disagrees with raw JSONL")
    require(nulls.get("ineligible") == sum(1 for row in rows if row["kind"] == "null-control" and not row["eligible"]),
            "null-control ineligible count disagrees with raw JSONL")

    visible_summary = summary.get("visibleControls", {})
    computed_visible_misses = 0
    for kind in sorted(VISIBLE):
        data = visible_summary.get(kind)
        require(isinstance(data, dict), f"summary missing visible-control category {kind}")
        count = counts[kind]
        applied_count = applied[kind]
        detected_count = passed[kind]
        eligible_count = sum(1 for row in rows if row["kind"] == kind and row["eligible"])
        ineligible_count = count - eligible_count
        require(data.get("checkpoints") == count, f"{kind} checkpoint count mismatch")
        require(data.get("eligible") == eligible_count, f"{kind} eligible count mismatch")
        require(data.get("ineligible") == ineligible_count, f"{kind} ineligible count mismatch")
        require(data.get("applied") == applied_count, f"{kind} applied count mismatch")
        require(data.get("unapplied") == eligible_count - applied_count, f"{kind} unapplied count mismatch")
        require(data.get("detected") == detected_count, f"{kind} detected count mismatch")
        computed_visible_misses += eligible_count - detected_count
    require(summary.get("visibleControlMisses") == computed_visible_misses,
            "visibleControlMisses does not count unapplied and undetected controls")
    if computed_visible_misses:
        failures.append(f"{computed_visible_misses} eligible visible positive controls were unapplied or not detected")
    if strict_failures:
        failures.append(f"{strict_failures} strict unseen-state observer/decision diffs")
    if composite_failures:
        failures.append(f"{composite_failures} Tier-B composite observer/decision diffs")
    if null_failures:
        failures.append(f"{null_failures} null-control diffs")

    return {
        "status": "pass" if not failures else "fail",
        "seedCount": len(seeds), "rowCount": len(rows),
        "strictFailures": strict_failures, "compositeFailures": composite_failures,
        "nullFailures": null_failures, "visibleControlMisses": computed_visible_misses,
        "exposureCounts": dict(sorted(exposure.items())), "failures": failures,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_dir", type=Path)
    parser.add_argument("--pins", required=True, type=Path,
                        help="pre-run JSON with the required frozen source, binary, test, seed, and protocol pins")
    args = parser.parse_args(argv)
    try:
        result = validate(args.run_dir, args.pins)
    except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as exc:
        print(json.dumps({"status": "invalid", "error": str(exc)}, indent=2))
        return 2
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0 if result["status"] == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
