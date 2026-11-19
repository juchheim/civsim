#!/usr/bin/env python3
"""Fail-closed, provenance-pinned F-T4 paired natural-start cohort runner."""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path
from typing import Any, Callable

from cohort_provenance import capture

ARMS = ("agr11", "control_no_agr11")
PROTOCOL = "WP-E5-5/F-T4"
MINUTES = 180
HEX64 = re.compile(r"^[0-9a-f]{64}$")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def parse_fields(line: str) -> dict[str, str]:
    fields: dict[str, str] = {}
    for token in line.split()[1:]:
        key, sep, value = token.partition("=")
        if sep:
            fields[key] = value
    return fields


def load_manifest(path: Path, expected_sha256: str) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    actual = sha256(path)
    if not HEX64.fullmatch(expected_sha256.lower()) or actual != expected_sha256.lower():
        raise ValueError(f"manifest SHA-256 mismatch: expected {expected_sha256}, got {actual}")
    manifest = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(manifest, dict) or manifest.get("protocol") != PROTOCOL:
        raise ValueError(f"manifest protocol must be {PROTOCOL}")
    if manifest.get("minutes") != MINUTES:
        raise ValueError(f"F-T4 horizon is fixed at {MINUTES} minutes")
    if manifest.get("arms") != list(ARMS):
        raise ValueError(f"manifest arms must be exactly {list(ARMS)} in that order")
    pins = manifest.get("pins")
    pin_names = ("headCommit", "sourceAggregateSha256", "packSha256", "harnessDllSha256",
                 "simDllSha256", "runnerSha256")
    if not isinstance(pins, dict) or any(not isinstance(pins.get(name), str) or not pins[name] for name in pin_names):
        raise ValueError(f"manifest pins must include {', '.join(pin_names)}")
    if not re.fullmatch(r"[0-9a-fA-F]{40}", pins["headCommit"]):
        raise ValueError("manifest pin headCommit must be a 40-digit Git commit SHA")
    for name in pin_names[1:]:
        if not HEX64.fullmatch(pins[name].lower()):
            raise ValueError(f"manifest pin {name} must be a 64-digit SHA-256")
    roster = manifest.get("runs")
    if not isinstance(roster, list) or not roster:
        raise ValueError("manifest runs must be a nonempty explicit seed/focus/arm roster")
    seen: set[tuple[int, str]] = set()
    focus_by_seed: dict[int, int] = {}
    arms_by_seed: dict[int, set[str]] = {}
    normalized: list[dict[str, Any]] = []
    for index, row in enumerate(roster):
        if not isinstance(row, dict):
            raise ValueError(f"manifest runs[{index}] must be an object")
        seed, focus, arm = row.get("seed"), row.get("focusCiv"), row.get("arm")
        if type(seed) is not int or seed < 0 or type(focus) is not int or not 0 <= focus < 4 or arm not in ARMS:
            raise ValueError(f"manifest runs[{index}] has invalid seed, focusCiv, or arm")
        key = (seed, arm)
        if key in seen:
            raise ValueError(f"duplicate manifest run for seed={seed} arm={arm}")
        seen.add(key)
        if seed in focus_by_seed and focus_by_seed[seed] != focus:
            raise ValueError(f"seed={seed} has conflicting focusCiv values")
        focus_by_seed[seed] = focus
        arms_by_seed.setdefault(seed, set()).add(arm)
        normalized.append({"seed": seed, "focusCiv": focus, "arm": arm})
    for seed, arms in arms_by_seed.items():
        if arms != set(ARMS):
            raise ValueError(f"seed={seed} must have exactly both paired arms; found {sorted(arms)}")
    expected = [(seed, focus_by_seed[seed], arm) for seed in sorted(focus_by_seed) for arm in ARMS]
    got = [(row["seed"], row["focusCiv"], row["arm"]) for row in normalized]
    if sorted(got) != sorted(expected):
        raise ValueError("manifest roster does not contain exactly one row per seed/focus/arm")
    normalized.sort(key=lambda row: (row["seed"], ARMS.index(row["arm"])))
    return manifest, normalized


def command_for(dotnet: str, harness: Path, pack: Path, row: dict[str, Any]) -> list[str]:
    command = [dotnet, str(harness), "iron-match", "--pack", str(pack), "--seed", str(row["seed"]),
               "--minutes", str(MINUTES), "--fief", "--grant-e5", "--fief-land-long-horizon",
               "--fief-land-focus-civ", str(row["focusCiv"])]
    command.append("--fief-land-anchor" if row["arm"] == "agr11" else "--fief-land-control")
    return command


def validate_pins(actual: dict[str, Any], pins: dict[str, str], runner_hash: str) -> None:
    expected = {
        "headCommit": pins["headCommit"],
        "sourceAggregateSha256": pins["sourceAggregateSha256"],
        "packSha256": pins["packSha256"],
        "harnessDllSha256": pins["harnessDllSha256"],
        "simDllSha256": pins["simDllSha256"],
    }
    mismatches = [f"{key}: expected {value}, got {actual.get(key)}"
                  for key, value in expected.items() if actual.get(key) != value]
    if pins["runnerSha256"].lower() != runner_hash.lower():
        mismatches.append(f"runnerSha256: expected {pins['runnerSha256']}, got {runner_hash}")
    if mismatches:
        raise ValueError("candidate pin mismatch: " + "; ".join(mismatches))


def run(manifest_path: Path, manifest_sha256: str, repo: Path, pack: Path, harness: Path, sim: Path,
        output: Path, *, dotnet: str = "dotnet", capture_fn: Callable[..., dict[str, Any]] = capture,
        process_run: Callable[..., Any] = subprocess.run) -> dict[str, Any]:
    manifest_path, repo, pack, harness, sim, output = (p.resolve() for p in
                                                       (manifest_path, repo, pack, harness, sim, output))
    manifest, roster = load_manifest(manifest_path, manifest_sha256)
    if not repo.is_dir() or not all(p.is_file() for p in (pack, harness, sim)):
        raise FileNotFoundError("repo, pack, Release Harness, and adjacent Release Sim must exist")
    if harness.name != "CivSim.Harness.dll" or "Release" not in harness.parts:
        raise ValueError("harness must be the Release CivSim.Harness.dll")
    if sim.name != "CivSim.Sim.dll" or sim.parent != harness.parent:
        raise ValueError("sim must be adjacent to the Release Harness DLL")
    if output.exists():
        raise FileExistsError(f"refusing existing output root: {output}")

    pins = manifest["pins"]
    runner_hash = sha256(Path(__file__).resolve())
    before = capture_fn(repo, pack, harness, sim)
    validate_pins(before, pins, runner_hash)
    output.mkdir(parents=True, exist_ok=False)
    (output / "manifest.json").write_bytes(manifest_path.read_bytes())
    (output / "roster.json").write_text(json.dumps(roster, indent=2) + "\n", encoding="utf-8")
    entries = []
    for row in roster:
        entries.append({**row, "status": "not_started", "command": command_for(dotnet, harness, pack, row),
                        "rawFile": f"seed-{row['seed']}-{row['arm']}.log"})
    metadata: dict[str, Any] = {
        "protocol": PROTOCOL, "status": "running", "startedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "minutes": MINUTES, "arms": list(ARMS), "manifestPath": str(manifest_path),
        "manifestSha256": manifest_sha256.lower(), "runnerSha256": runner_hash,
        "repo": str(repo), "pack": str(pack), "harnessDll": str(harness), "simDll": str(sim),
        "outputRoot": str(output), "provenanceBefore": before, "runs": entries,
    }
    meta_path = output / "runner.json"

    def write_meta() -> None:
        meta_path.write_text(json.dumps(metadata, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    write_meta()
    interrupted = False
    try:
        for entry in entries:
            try:
                run_before = capture_fn(repo, pack, harness, sim)
                validate_pins(run_before, pins, sha256(Path(__file__).resolve()))
                entry["provenanceBefore"] = run_before
                entry["startedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
                entry["status"] = "running"
                write_meta()
                raw_path = output / entry["rawFile"]
                run_interrupted = False
                try:
                    with raw_path.open("wb") as raw:
                        result = process_run(entry["command"], cwd=repo, stdout=raw, stderr=subprocess.STDOUT,
                                             check=False)
                    entry["exitCode"] = result.returncode
                except KeyboardInterrupt:
                    entry["exitCode"] = None
                    entry["status"] = "interrupted"
                    entry["failure"] = "KeyboardInterrupt"
                    interrupted = True
                    run_interrupted = True
                except (OSError, subprocess.SubprocessError) as exc:
                    entry["exitCode"] = None
                    entry["failure"] = f"{type(exc).__name__}: {exc}"
                entry["rawSha256"] = sha256(raw_path) if raw_path.exists() else None
                entry["rawBytes"] = raw_path.stat().st_size if raw_path.exists() else 0
                entry["provenanceAfter"] = capture_fn(repo, pack, harness, sim)
                validate_pins(entry["provenanceAfter"], pins, sha256(Path(__file__).resolve()))
                entry["finishedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
                rows = []
                if raw_path.exists():
                    rows = [line for line in raw_path.read_text(encoding="utf-8", errors="replace").splitlines()
                            if line.startswith("FIEF_LAND_MANIFEST ")]
                row_matches = (len(rows) == 1 and parse_fields(rows[0]).get("seed") == str(entry["seed"])
                               and parse_fields(rows[0]).get("focusCiv") == str(entry["focusCiv"])
                               and parse_fields(rows[0]).get("arm") == entry["arm"])
                entry["manifestRowCount"] = len(rows)
                entry["manifestRowValid"] = row_matches
                if run_interrupted:
                    metadata["status"] = "interrupted"
                    for pending in entries:
                        if pending["status"] == "not_started":
                            pending["status"] = "not_run_interrupted"
                    write_meta()
                    break
                if entry.get("failure") or entry.get("exitCode") != 0 or not row_matches:
                    entry["status"] = "error"
                    if not row_matches:
                        entry["failure"] = entry.get("failure", "missing, duplicate, or mismatched FIEF_LAND_MANIFEST row")
                    metadata["status"] = "failed"
                    write_meta()
                    break
                entry["status"] = "complete"
                write_meta()
            except KeyboardInterrupt:
                metadata["status"] = "interrupted"
                for pending in entries:
                    if pending["status"] == "not_started":
                        pending["status"] = "not_run_interrupted"
                break
            except Exception as exc:
                entry["status"] = "error"
                entry["failure"] = f"{type(exc).__name__}: {exc}"
                metadata["status"] = "failed"
                for pending in entries:
                    if pending["status"] == "not_started":
                        pending["status"] = "not_run_after_failure"
                write_meta()
                break
        if metadata["status"] == "running":
            metadata["status"] = "complete" if all(entry["status"] == "complete" for entry in entries) else "failed"
    finally:
        try:
            after = capture_fn(repo, pack, harness, sim)
            validate_pins(after, pins, sha256(Path(__file__).resolve()))
            metadata["provenanceAfter"] = after
        except Exception as exc:
            metadata["provenanceAfterError"] = f"{type(exc).__name__}: {exc}"
            metadata["status"] = "failed"
        metadata["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
        if interrupted and "provenanceAfterError" not in metadata:
            metadata["status"] = "interrupted"
        write_meta()
    return metadata


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", required=True, type=Path)
    parser.add_argument("--pack", required=True, type=Path)
    parser.add_argument("--harness", required=True, type=Path)
    parser.add_argument("--sim", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--manifest-sha256", required=True)
    parser.add_argument("--output", required=True, type=Path, help="new absent output root")
    parser.add_argument("--dotnet", default="dotnet")
    args = parser.parse_args(argv)
    try:
        result = run(args.manifest, args.manifest_sha256, args.repo, args.pack, args.harness, args.sim,
                     args.output, dotnet=args.dotnet)
        print(json.dumps(result, indent=2))
        return 0 if result["status"] == "complete" else (130 if result["status"] == "interrupted" else 2)
    except (OSError, ValueError, RuntimeError, json.JSONDecodeError) as exc:
        print(f"F-T4 runner: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
