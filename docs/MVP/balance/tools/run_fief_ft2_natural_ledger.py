#!/usr/bin/env python3
"""Fail-closed runner for a frozen natural F-T2 Fief power-ledger cohort.

This copy intentionally has no candidate-specific hashes or seed count. Every
manifest and candidate identity is supplied explicitly at invocation time.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import math
import re
import subprocess
import sys
from pathlib import Path
from typing import Any, Callable

from cohort_provenance import capture, verify_expected

MATCH_RE = re.compile(r"^== fief-match seed (\d+): .+$", re.MULTILINE)
LEDGER_RE = re.compile(r"^F-T2 mill power ledger \(12 seasons from first source; grain coverage is adult-equivalent capacity, not a separate flour-stock measure\):$", re.MULTILINE)
SUMMARY_RE = re.compile(r"^  completed town windows=(\d+); censored=(\d+); .+$", re.MULTILINE)
WINDOW_RE = re.compile(
    r"^  s(?P<settlement>\d+) c(?P<civ>\d+) seasons (?P<start>\d+)–(?P<end>\d+): "
    r"(?P<coverage>(?:\d+(?:\.\d+)?%|n/a)), sources water/wind (?P<water>\d+)/(?P<wind>\d+), "
    r"power (?P<used>\d+(?:\.\d+)?)/(?P<offered>\d+(?:\.\d+)?) hours"
    r"(?: \(censored (?P<observed>\d+)/12\))?$"
)
PROVENANCE_FIELDS = (
    ("headCommit", "expected-head"),
    ("sourceAggregateSha256", "expected-source-aggregate"),
    ("packSha256", "expected-pack-sha256"),
    ("harnessDllSha256", "expected-harness-sha256"),
    ("simDllSha256", "expected-sim-sha256"),
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_seeds(path: Path, expected_count: int) -> list[int]:
    seeds: list[int] = []
    for line_number, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("#") or line.lower() == "seed":
            continue
        try:
            seeds.append(int(line))
        except ValueError as exc:
            raise ValueError(f"{path}:{line_number}: expected integer seed, got {line!r}") from exc
    if len(seeds) != expected_count:
        raise ValueError(f"manifest has {len(seeds)} seeds; expected exactly {expected_count}")
    if len(set(seeds)) != len(seeds):
        raise ValueError("manifest contains duplicate seeds")
    return seeds


def parse_result(raw_text: str, expected_seed: int) -> dict[str, Any]:
    matches = [int(value) for value in MATCH_RE.findall(raw_text)]
    ledger_count = len(LEDGER_RE.findall(raw_text))
    summaries = SUMMARY_RE.findall(raw_text)
    errors: list[str] = []
    if len(matches) != 1:
        errors.append(f"expected exactly one match result header, found {len(matches)}")
    elif matches[0] != expected_seed:
        errors.append(f"result seed {matches[0]} does not match expected seed {expected_seed}")
    if ledger_count != 1:
        errors.append(f"expected exactly one F-T2 ledger, found {ledger_count}")
    if len(summaries) != 1:
        errors.append(f"expected exactly one F-T2 summary, found {len(summaries)}")
    completed = int(summaries[0][0]) if len(summaries) == 1 else None
    censored = int(summaries[0][1]) if len(summaries) == 1 else None
    rows: list[dict[str, Any]] = []
    seen_towns: set[tuple[int, int]] = set()
    for line_number, line in enumerate(raw_text.splitlines(), 1):
        if not line.startswith("  s"):
            continue
        match = WINDOW_RE.fullmatch(line)
        if match is None:
            errors.append(f"malformed F-T2 town window row on line {line_number}")
            continue
        row_error_count = len(errors)
        row = match.groupdict()
        settlement, civ = int(row["settlement"]), int(row["civ"])
        start, end = int(row["start"]), int(row["end"])
        identity = (settlement, civ)
        if identity in seen_towns:
            errors.append(f"duplicate F-T2 town/window identity s{settlement} c{civ}")
        seen_towns.add(identity)
        if start < 0 or end < start:
            errors.append(f"invalid F-T2 season endpoints on line {line_number}")
            continue
        observed = end - start + 1
        censored_observed = int(row["observed"]) if row["observed"] is not None else None
        if censored_observed is None:
            if observed != 12:
                errors.append(f"eligible F-T2 window s{settlement} c{civ} has {observed} seasons; expected 12")
            kind = "complete"
        else:
            if not 1 <= censored_observed < 12:
                errors.append(f"invalid censored sample count on line {line_number}")
            if observed != censored_observed:
                errors.append(f"censored F-T2 window endpoints disagree with observed seasons on line {line_number}")
            kind = "censored"
        coverage = row["coverage"]
        if coverage == "n/a":
            if kind == "complete":
                errors.append(f"complete F-T2 window s{settlement} c{civ} is missing numeric coverage")
            coverage_value = None
        else:
            coverage_value = float(coverage[:-1])
            if not math.isfinite(coverage_value) or not 0 <= coverage_value <= 100:
                errors.append(f"invalid coverage percentage on line {line_number}")
        water, wind = int(row["water"]), int(row["wind"])
        used, offered = float(row["used"]), float(row["offered"])
        if not math.isfinite(used) or not math.isfinite(offered) or used < 0 or offered < 0:
            errors.append(f"invalid power-hour coverage fields on line {line_number}")
        if len(errors) != row_error_count:
            kind = "invalid"
        rows.append({"settlement": settlement, "civ": civ, "startSeason": start, "endSeason": end,
                     "observedSeasons": observed, "kind": kind, "coveragePercent": coverage_value,
                     "waterSources": water, "windSources": wind, "powerUsedHours": used,
                     "powerOfferedHours": offered})
    complete_rows = sum(row["kind"] == "complete" for row in rows)
    censored_rows = sum(row["kind"] == "censored" for row in rows)
    invalid_rows = sum(row["kind"] == "invalid" for row in rows)
    if len(summaries) == 1:
        if complete_rows != completed:
            errors.append(f"summary reports {completed} complete windows but found {complete_rows} complete detail rows")
        if censored_rows != censored:
            errors.append(f"summary reports {censored} censored windows but found {censored_rows} censored detail rows")
        if len(rows) != completed + censored:
            errors.append(f"summary accounts for {completed + censored} windows but found {len(rows)} detail rows")
    return {
        "resultHeaders": matches,
        "ledgerCount": ledger_count,
        "completedTownWindows": completed,
        "censoredTownWindows": censored,
        "townWindowRows": len(rows),
        "completeWindowRows": complete_rows,
        "censoredWindowRows": censored_rows,
        "invalidWindowRows": invalid_rows,
        "townWindows": rows,
        "parseErrors": errors,
    }


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--repo", type=Path, required=True)
    p.add_argument("--pack", type=Path, required=True)
    p.add_argument("--harness-dll", type=Path, required=True)
    p.add_argument("--sim-dll", type=Path, required=True)
    p.add_argument("--manifest", type=Path, required=True)
    p.add_argument("--expected-seed-count", type=int, required=True)
    p.add_argument("--expected-manifest-sha256", required=True)
    p.add_argument("--expected-head", required=True)
    p.add_argument("--expected-source-aggregate", required=True)
    p.add_argument("--expected-pack-sha256", required=True)
    p.add_argument("--expected-harness-sha256", required=True)
    p.add_argument("--expected-sim-sha256", required=True)
    p.add_argument("--output-dir", type=Path, required=True)
    p.add_argument("--dotnet", default="dotnet")
    p.add_argument("--minutes", type=int, default=130)
    return p


def _expected(args: argparse.Namespace) -> dict[str, str]:
    return {key: getattr(args, arg_name.replace("-", "_")) for key, arg_name in PROVENANCE_FIELDS}


def run(
    args: argparse.Namespace,
    *,
    capture_fn: Callable[[Path, Path, Path, Path], dict[str, Any]] = capture,
    process_run: Callable[..., Any] = subprocess.run,
) -> int:
    repo, pack, harness, sim = (p.resolve() for p in (args.repo, args.pack, args.harness_dll, args.sim_dll))
    manifest, out = args.manifest.resolve(), args.output_dir.resolve()
    for path, label in ((repo, "repository"), (pack, "numeric pack"), (harness, "Release Harness DLL"),
                        (sim, "adjacent Release Sim DLL"), (manifest, "seed manifest")):
        if not path.exists():
            raise FileNotFoundError(f"{label} not found: {path}")
    if args.minutes != 130:
        raise ValueError("F-T2 natural-ledger horizon is fixed at 130 minutes")
    if args.expected_seed_count <= 0:
        raise ValueError("expected seed count must be positive")
    if out.exists():
        raise FileExistsError(f"refusing to reuse output directory: {out}")
    manifest_sha = sha256(manifest)
    if manifest_sha != args.expected_manifest_sha256:
        raise ValueError(f"manifest SHA-256 mismatch: expected {args.expected_manifest_sha256}, got {manifest_sha}")
    seeds = load_seeds(manifest, args.expected_seed_count)

    expected = _expected(args)
    before = capture_fn(repo, pack, harness, sim)
    verify_expected(before, expected)
    out.mkdir(parents=True)
    metadata: dict[str, Any] = {
        "status": "running",
        "startedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "repo": str(repo), "packPath": str(pack), "harnessDllPath": str(harness), "simDllPath": str(sim),
        "manifestPath": str(manifest), "manifestSha256": manifest_sha,
        "expectedSeedCount": args.expected_seed_count, "seeds": seeds, "minutes": args.minutes,
        "provenanceBefore": before, "runs": [],
        "protocol": {
            "naturalFief": True,
            "powerLedger": True,
            "grantE5": False,
            "townWindowSeasonsInclusive": 12,
            "grainCoverageMeasure": "adultEquivalentCapacityProxy",
            "separateFlourInventory": False,
            "secondWindmillAllowed": True,
        },
    }
    metadata_path = out / "run_metadata.json"

    def write() -> None:
        metadata_path.write_text(json.dumps(metadata, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    write()
    for seed in seeds:
        raw_path = out / f"seed-{seed}.txt"
        command = [args.dotnet, str(harness), "fief-match", "--pack", str(pack), "--seed", str(seed),
                   "--minutes", "130", "--fief-power-ledger"]
        invocation_error = None
        try:
            with raw_path.open("w", encoding="utf-8", newline="\n") as raw:
                result = process_run(command, cwd=repo, stdout=raw, stderr=subprocess.STDOUT,
                                     check=False, text=True)
        except (OSError, subprocess.SubprocessError) as exc:
            invocation_error = f"could not execute harness: {type(exc).__name__}: {exc}"
            result = type("FailedProcess", (), {"returncode": None})()
        raw_text = raw_path.read_text(encoding="utf-8")
        parsed = parse_result(raw_text, seed)
        run_row = {
            "seed": seed, "command": command, "rawFile": raw_path.name,
            "exitCode": result.returncode, "rawSha256": sha256(raw_path), "rawBytes": raw_path.stat().st_size,
            **parsed,
        }
        if result.returncode != 0:
            run_row["parseErrors"].append(f"harness exited with {result.returncode}")
        if invocation_error is not None:
            run_row["parseErrors"].append(invocation_error)
        run_row["status"] = "ok" if not run_row["parseErrors"] else "error"
        metadata["runs"].append(run_row)
        write()

    after_error = None
    try:
        after = capture_fn(repo, pack, harness, sim)
        metadata["provenanceAfter"] = after
        drift = before != after
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        after = None
        drift = True
        after_error = f"could not capture post-run provenance: {type(exc).__name__}: {exc}"
        metadata["provenanceAfterCaptureError"] = after_error
    if drift:
        metadata["provenanceChangedDuringRun"] = True
    expected_ids = set(seeds)
    run_ids = [row["seed"] for row in metadata["runs"]]
    accounting_ok = len(run_ids) == args.expected_seed_count and set(run_ids) == expected_ids and len(run_ids) == len(set(run_ids))
    all_results_ok = all(row["status"] == "ok" for row in metadata["runs"])
    metadata["accounting"] = {
        "expectedSeeds": args.expected_seed_count,
        "attemptedRuns": len(run_ids),
        "uniqueSeeds": len(set(run_ids)),
        "missingSeeds": sorted(expected_ids - set(run_ids)),
        "unexpectedSeeds": sorted(set(run_ids) - expected_ids),
        "duplicateResultSeeds": sorted(seed for seed in set(run_ids) if run_ids.count(seed) > 1),
        "allResultsValid": all_results_ok,
    }
    metadata["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
    metadata["status"] = "complete" if accounting_ok and all_results_ok and not drift else "failed"
    write()
    return 0 if metadata["status"] == "complete" else 2


def main() -> int:
    args = parser().parse_args()
    try:
        return run(args)
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
