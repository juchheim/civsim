#!/usr/bin/env python3
"""Fail-closed runner for a frozen WP-E5-7 GOV-11 paired cohort.

The runner never builds artifacts, selects seeds, or changes the fixed 130-minute
paired-match command. Manifests must pin the exact source, pack, adjacent Release
Harness/Sim binaries, cohort and seed list, runner and analyzer, build attestation,
and runtime/platform tuple. Output roots are single-use and outside the source tree.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import platform
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from typing import Any

PROTOCOL = "WP-E5-7/GOV-11"
PROVISIONAL_CRITERIA = "autonomous_gov11_conception_probability_criteria_not_separately_user_approved"
MINUTES = 130
TRACE_FLAG = "--fief-wp7-paired"
COHORT_SIZES = {"calibration": 20, "holdout": 80}
PAIR_PREFIXES = (
    "wp7-gov11-city-year-pair ",
    "wp7-gov11-city-year-pair-excluded ",
    "wp7-gov11-city-year-pair-censored ",
)
GROWTH_PREFIX = "wp7-gov11-growth "
MATCH_TERMINAL_PREFIX = "wp7-match-terminal "
ALLOWED_MATCH_STATUSES = {"chapter_end", "horizon_censored", "run_stopped"}
SOURCE_ROOTS = ("content/", "src/", "game/")
BUILD_DIRS = {"bin", "obj", "build", "dist", ".godot", ".vs", "__pycache__"}


def sha256(path: str | Path) -> str:
    path = Path(path)
    digest = hashlib.sha256()
    if path.is_dir():
        for item in sorted(path.rglob("*")):
            if not item.is_file() or any(part in {".git", *BUILD_DIRS} for part in item.relative_to(path).parts):
                continue
            relative = item.relative_to(path).as_posix().encode("utf-8")
            digest.update(len(relative).to_bytes(8, "big"))
            digest.update(relative)
            digest.update(item.read_bytes())
    else:
        with path.open("rb") as stream:
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
    return digest.hexdigest()


def _git(root: Path, *args: str) -> str:
    return subprocess.run(
        ["git", "-C", str(root), *args], check=True, capture_output=True, text=True
    ).stdout


def source_snapshot(source: str | Path) -> dict[str, Any]:
    """Return the canonical hash_e5_workspace.py source aggregate; reject dirty code."""
    root = Path(source).resolve()
    if not root.is_dir():
        raise ValueError("source root must be an existing repository directory")
    top = Path(_git(root, "rev-parse", "--show-toplevel").strip()).resolve()
    if top != root:
        raise ValueError("source root must be the Git worktree root")
    head = _git(root, "rev-parse", "HEAD").strip()
    changed = _git(root, "status", "--porcelain=v1", "--untracked-files=all", "--", "content", "src", "game")
    if changed:
        raise ValueError("source workspace must be clean under content/, src/, and game/")

    tracked = _git(root, "ls-files", "-z").split("\0")
    untracked = _git(root, "ls-files", "--others", "--exclude-standard", "-z").split("\0")
    paths = sorted({path for path in tracked + untracked if path.startswith(SOURCE_ROOTS)
                    and not (set(Path(path).parts) & BUILD_DIRS)})
    records = []
    for relative in paths:
        path = root / relative
        if path.is_file():
            file_hash = sha256(path)
        else:
            file_hash = None
        records.append({"path": relative, "status": "clean", "sha256": file_hash})
    payload = {"head": head, "files": records}
    canonical = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return {"head": head, "aggregate_sha256": hashlib.sha256(canonical).hexdigest(), "files": records}


def environment_snapshot(dotnet: str = "dotnet", cwd: str | Path | None = None) -> dict[str, Any]:
    executable = shutil.which(dotnet)
    if executable is None:
        raise ValueError(f"cannot resolve .NET host executable: {dotnet}")
    executable_path = Path(executable).resolve()
    if not executable_path.is_file():
        raise ValueError(f".NET host executable is missing: {executable_path}")
    sdk = subprocess.run([str(executable_path), "--version"], cwd=cwd, check=True,
                         capture_output=True, text=True).stdout.strip()
    runtimes = subprocess.run([str(executable_path), "--list-runtimes"], cwd=cwd, check=True,
                              capture_output=True, text=True).stdout.splitlines()
    if not sdk or not runtimes:
        raise ValueError(".NET SDK/runtime inventory is empty")
    return {
        "dotnet_host_path": str(executable_path),
        "dotnet_host_sha256": sha256(executable_path),
        "working_directory": str(Path(cwd).resolve()) if cwd is not None else str(Path.cwd().resolve()),
        "dotnet_sdk_version": sdk,
        "dotnet_runtimes": [line.strip() for line in runtimes if line.strip()],
        "os_name": platform.system(),
        "os_release": platform.release(),
        "os_version": platform.version(),
        "architecture": platform.machine(),
        "python_version": platform.python_version(),
    }


def candidate_snapshot(source: str | Path, pack: str | Path, harness: str | Path,
                       sim: str | Path, dotnet: str = "dotnet") -> dict[str, Any]:
    source_root = Path(source).resolve()
    pack_path = Path(pack).resolve()
    harness_path = Path(harness).resolve()
    sim_path = Path(sim).resolve()
    for path, label in ((pack_path, "numeric-status pack"), (harness_path, "Release Harness DLL"),
                        (sim_path, "adjacent Release Sim DLL")):
        if not path.is_file():
            raise ValueError(f"{label} is missing: {path}")
    if harness_path.name != "CivSim.Harness.dll" or sim_path.name != "CivSim.Sim.dll":
        raise ValueError("candidate must use CivSim.Harness.dll and CivSim.Sim.dll")
    if harness_path.parent != sim_path.parent:
        raise ValueError("CivSim.Sim.dll must be adjacent to the Release Harness DLL")
    if "Release" not in harness_path.parts or "net10.0" not in harness_path.parts:
        raise ValueError("Harness and Sim must come from the Release net10.0 output directory")
    source_info = source_snapshot(source_root)
    analyzer = source_root / "docs/MVP/balance/tools/analyze_fief_gov11_growth_effect.py"
    runner = Path(__file__).resolve()
    if not analyzer.is_file():
        raise ValueError(f"GOV-11 analyzer is missing: {analyzer}")
    return {
        "head": source_info["head"],
        "source_aggregate_sha256": source_info["aggregate_sha256"],
        "pack_sha256": sha256(pack_path),
        "harness_sha256": sha256(harness_path),
        "sim_sha256": sha256(sim_path),
        "runner_sha256": sha256(runner),
        "analyzer_sha256": sha256(analyzer),
        "environment": environment_snapshot(dotnet, source_root),
    }


def read_seed_file(path: str | Path) -> list[int]:
    seeds = []
    for line_no, line in enumerate(Path(path).read_text(encoding="utf-8").splitlines(), 1):
        value = line.strip()
        if not value:
            continue
        if not value.isdigit():
            raise ValueError(f"seed file line {line_no} is not a nonnegative integer")
        seeds.append(int(value))
    if not seeds or len(seeds) != len(set(seeds)):
        raise ValueError("seed file must contain distinct nonnegative seeds")
    return seeds


def verify(manifest: dict[str, Any], candidates: dict[str, str | Path], cohort_role: str,
           seed_file: str | Path, dotnet: str = "dotnet") -> dict[str, Any]:
    if cohort_role not in COHORT_SIZES:
        raise ValueError("cohort role must be calibration or holdout")
    if manifest.get("protocol") != PROTOCOL:
        raise ValueError("manifest protocol mismatch")
    if manifest.get("cohort_role") != cohort_role:
        raise ValueError("manifest cohort role mismatch")
    if manifest.get("criteria_status") != PROVISIONAL_CRITERIA:
        raise ValueError("manifest must preserve the autonomous provisional criteria status")
    if manifest.get("minutes") != MINUTES or manifest.get("trace_flag") != TRACE_FLAG:
        raise ValueError("manifest must freeze the 130-minute --fief-wp7-paired command")
    if manifest.get("pack_role") != "numeric_e5_status":
        raise ValueError("manifest must identify the numeric E5 status pack")

    pins = manifest.get("pins")
    required_pins = ("head", "source_aggregate_sha256", "pack_sha256", "harness_sha256",
                     "sim_sha256", "runner_sha256", "analyzer_sha256")
    if not isinstance(pins, dict) or any(not isinstance(pins.get(key), str) or not pins[key] for key in required_pins):
        raise ValueError("manifest pins must include HEAD, source/pack/Harness/Sim, runner, and analyzer hashes")
    actual = candidate_snapshot(candidates["source"], candidates["pack"], candidates["harness"],
                                candidates["sim"], dotnet)
    for key in required_pins:
        if actual[key] != pins[key]:
            raise ValueError(f"{key} mismatch: expected {pins[key]}, got {actual[key]}")
    if manifest.get("environment") != actual["environment"]:
        raise ValueError("runtime, OS, or architecture does not match the frozen environment pin")

    build = manifest.get("build_provenance")
    expected_build = {
        "configuration": "Release",
        "target_framework": "net10.0",
        "head": actual["head"],
        "source_aggregate_sha256": actual["source_aggregate_sha256"],
        "pack_sha256": actual["pack_sha256"],
        "harness_sha256": actual["harness_sha256"],
        "sim_sha256": actual["sim_sha256"],
        "sim_adjacent_to_harness": True,
    }
    if not isinstance(build, dict) or any(build.get(key) != value for key, value in expected_build.items()):
        raise ValueError("build provenance must attest the exact frozen source, numeric pack, and adjacent Release binaries")
    if not isinstance(build.get("build_command"), list) or not build["build_command"]:
        raise ValueError("build provenance must record the build command")
    build_command = [str(part) for part in build["build_command"]]
    build_host = shutil.which(build_command[0])
    build_host_path = Path(build_host or build_command[0]).resolve()
    if build_host_path != Path(actual["environment"]["dotnet_host_path"]):
        raise ValueError("build provenance must use the pinned .NET host")
    if "build" not in build_command or "Release" not in build_command:
        raise ValueError("build provenance command must record a Release build")
    if not isinstance(build.get("build_utc"), str) or not build["build_utc"]:
        raise ValueError("build provenance must record build_utc")
    try:
        datetime.fromisoformat(build["build_utc"].replace("Z", "+00:00"))
    except ValueError as exc:
        raise ValueError("build_utc must be an ISO-8601 timestamp") from exc

    seeds = manifest.get("seeds")
    expected_count = COHORT_SIZES[cohort_role]
    if not isinstance(seeds, list) or len(seeds) != expected_count or any(type(seed) is not int or seed < 0 for seed in seeds):
        raise ValueError(f"{cohort_role} manifest must contain exactly {expected_count} nonnegative seeds")
    if len(set(seeds)) != expected_count:
        raise ValueError("manifest contains duplicate seeds")
    seed_file = Path(seed_file)
    seed_hash = sha256(seed_file)
    if manifest.get("seed_file_sha256") != seed_hash:
        raise ValueError("preregistered seed-file SHA-256 mismatch")
    if read_seed_file(seed_file) != seeds:
        raise ValueError("manifest seeds do not exactly match the preregistered seed file")
    return actual


def parse_output(raw_text: str, expected_seed: int) -> dict[str, Any]:
    terminal_lines = [line for line in raw_text.splitlines() if line.startswith(MATCH_TERMINAL_PREFIX)]
    if len(terminal_lines) != 1:
        raise ValueError(f"expected exactly one terminal row, found {len(terminal_lines)}")
    fields = {}
    for token in terminal_lines[0].split()[1:]:
        if "=" in token:
            key, value = token.split("=", 1)
            if key in fields:
                raise ValueError(f"terminal row repeats field {key}")
            fields[key] = value
    if fields.get("seed") != str(expected_seed):
        raise ValueError("terminal row seed does not match manifest seed")
    if fields.get("matchStatus") not in ALLOWED_MATCH_STATUSES:
        raise ValueError("terminal row has missing or unrecognized matchStatus")
    if not fields.get("endReason") or not fields.get("season", "").isdigit():
        raise ValueError("terminal row lacks endReason or numeric season")

    counts = {"eligible_pairs": 0, "excluded_pairs": 0, "censored_pairs": 0,
              "growth_trace_rows": 0, "chartered_growth_opportunities": 0}
    for line_no, line in enumerate(raw_text.splitlines(), 1):
        if line.startswith(GROWTH_PREFIX):
            row = {}
            for token in line.split()[1:]:
                if "=" in token:
                    key, value = token.split("=", 1)
                    row[key] = value
            if row.get("seed") != str(expected_seed):
                raise ValueError(f"GOV-11 growth trace row on line {line_no} has a missing or mismatched seed")
            counts["growth_trace_rows"] += 1
            if row.get("gov11") == "true":
                control = row.get("controlConceptionChance", "")
                treatment = row.get("treatmentConceptionChance", "")
                if not control.isdigit() or not treatment.isdigit() or int(treatment) < int(control):
                    raise ValueError(f"GOV-11 growth trace row on line {line_no} has invalid paired chances")
                if row.get("pairedBasis") != "same_figure_same_state_same_keyed_roll":
                    raise ValueError(f"GOV-11 growth trace row on line {line_no} lacks its paired keyed-roll basis")
                counts["chartered_growth_opportunities"] += int(control) > 0
            continue
        kind = next((prefix for prefix in PAIR_PREFIXES if line.startswith(prefix)), None)
        if kind is None:
            continue
        row = {}
        for token in line.split()[1:]:
            if "=" in token:
                key, value = token.split("=", 1)
                row[key] = value
        if row.get("seed") != str(expected_seed):
            raise ValueError(f"GOV-11 trace row on line {line_no} has a missing or mismatched seed")
        if kind == PAIR_PREFIXES[0]:
            counts["eligible_pairs"] += 1
        elif kind == PAIR_PREFIXES[1]:
            counts["excluded_pairs"] += 1
        else:
            counts["censored_pairs"] += 1
    counts["no_exposure"] = sum(counts[key] for key in ("eligible_pairs", "excluded_pairs", "censored_pairs")) == 0
    return {
        "terminal_row": fields,
        "terminal_status": fields["matchStatus"],
        "terminal_season": int(fields["season"]),
        **counts,
    }


def _write_json(path: Path, value: dict[str, Any]) -> None:
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _assert_manifest_hash(path: Path, expected: str) -> None:
    actual = sha256(path)
    if actual.lower() != expected.lower():
        raise ValueError(f"manifest changed during run: expected {expected}, got {actual}")


def run(manifest_path: str | Path, candidates: dict[str, str | Path], output: str | Path, *,
        cohort_role: str, seed_file: str | Path, manifest_sha256: str,
        dotnet: str = "dotnet", preflight_only: bool = False) -> dict[str, Any]:
    manifest_path = Path(manifest_path).resolve()
    seed_file = Path(seed_file).resolve()
    actual_manifest_sha = sha256(manifest_path)
    if actual_manifest_sha.lower() != manifest_sha256.lower():
        raise ValueError(f"manifest SHA-256 mismatch: expected {manifest_sha256}, got {actual_manifest_sha}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    snapshot = verify(manifest, candidates, cohort_role, seed_file, dotnet)
    output = Path(output).resolve()
    source_root = Path(candidates["source"]).resolve()
    if output == source_root or source_root in output.parents:
        raise ValueError("output root must be outside the source tree")
    if output.exists():
        raise ValueError(f"output path already exists: {output}")
    output.mkdir(parents=True, exist_ok=False)
    metadata: dict[str, Any] = {
        "protocol": PROTOCOL,
        "cohort_role": cohort_role,
        "criteria_status": PROVISIONAL_CRITERIA,
        "minutes": MINUTES,
        "trace_flag": TRACE_FLAG,
        "manifest_path": str(manifest_path),
        "manifest_sha256": actual_manifest_sha,
        "seed_file_path": str(seed_file),
        "seed_file_sha256": sha256(seed_file),
        "head": snapshot["head"],
        "pins": {key: snapshot[key] for key in (
            "source_aggregate_sha256", "pack_sha256", "harness_sha256", "sim_sha256",
            "runner_sha256", "analyzer_sha256")},
        "environment": snapshot["environment"],
        "output": str(output),
        "seeds": [],
        "status": "preflighting" if preflight_only else "running",
    }
    _write_json(output / "manifest.json", manifest)
    (output / "seed-file.txt").write_bytes(seed_file.read_bytes())
    _write_json(output / "provenance.json", {"head": snapshot["head"], "pins": metadata["pins"],
                                               "environment": snapshot["environment"],
                                               "build_provenance": manifest["build_provenance"]})
    _write_json(output / "runner.json", metadata)
    if preflight_only:
        try:
            _assert_manifest_hash(manifest_path, actual_manifest_sha)
            after = verify(manifest, candidates, cohort_role, seed_file, dotnet)
            if after != snapshot:
                raise ValueError("candidate changed during preflight")
            metadata["status"] = "preflight_passed"
            metadata["postflight"] = "passed"
            return metadata
        except BaseException as exc:
            metadata["status"] = "failed"
            metadata["failure"] = f"{type(exc).__name__}: {exc}"
            metadata["failed_utc"] = datetime.now(timezone.utc).isoformat()
            raise
        finally:
            _write_json(output / "runner.json", metadata)

    try:
        for seed in manifest["seeds"]:
            _assert_manifest_hash(manifest_path, actual_manifest_sha)
            before = verify(manifest, candidates, cohort_role, seed_file, dotnet)
            raw = output / f"seed-{seed}.log"
            command = [manifest["environment"]["dotnet_host_path"], str(Path(candidates["harness"]).resolve()), "fief-match", "--pack",
                       str(Path(candidates["pack"]).resolve()), "--seed", str(seed), "--minutes",
                       str(MINUTES), TRACE_FLAG]
            entry: dict[str, Any] = {
                "seed": seed, "status": "running", "raw_file": raw.name,
                "started_utc": datetime.now(timezone.utc).isoformat(),
                "pre_snapshot": before, "command": command,
            }
            metadata["seeds"].append(entry)
            _write_json(output / "runner.json", metadata)
            try:
                with raw.open("x", encoding="utf-8", newline="\n") as stream:
                    proc = subprocess.run(command, cwd=source_root, stdout=stream, stderr=subprocess.STDOUT,
                                          check=False, text=True)
                accounting = parse_output(raw.read_text(encoding="utf-8", errors="replace"), seed)
                entry.update(accounting)
                after = verify(manifest, candidates, cohort_role, seed_file, dotnet)
                _assert_manifest_hash(manifest_path, actual_manifest_sha)
                if after != before:
                    raise ValueError("candidate changed during seed execution")
                entry.update({
                    "exit_code": proc.returncode,
                    "output_sha256": sha256(raw),
                    "output_bytes": raw.stat().st_size,
                    "post_snapshot": after,
                    "finished_utc": datetime.now(timezone.utc).isoformat(),
                    "status": "complete" if proc.returncode == 0 else "error",
                })
                if proc.returncode != 0:
                    raise RuntimeError(f"seed {seed} exited with {proc.returncode}; raw output preserved")
            except BaseException as exc:
                entry["status"] = "error"
                entry["failure"] = f"{type(exc).__name__}: {exc}"
                entry["output_sha256"] = sha256(raw) if raw.exists() else None
                entry["output_bytes"] = raw.stat().st_size if raw.exists() else 0
                entry["finished_utc"] = datetime.now(timezone.utc).isoformat()
                raise
            finally:
                _write_json(output / "runner.json", metadata)
        metadata["status"] = "complete"
        metadata["completed_utc"] = datetime.now(timezone.utc).isoformat()
        return metadata
    except BaseException as exc:
        metadata["status"] = "failed"
        metadata["failure"] = f"{type(exc).__name__}: {exc}"
        metadata["failed_utc"] = datetime.now(timezone.utc).isoformat()
        _write_json(output / "runner.json", metadata)
        raise
    finally:
        try:
            _assert_manifest_hash(manifest_path, actual_manifest_sha)
            final_snapshot = verify(manifest, candidates, cohort_role, seed_file, dotnet)
            metadata["final_snapshot"] = final_snapshot
        except BaseException as exc:
            metadata["final_snapshot_error"] = f"{type(exc).__name__}: {exc}"
            if metadata["status"] == "complete":
                metadata["status"] = "failed"
                metadata["failure"] = "candidate changed by run completion"
        _write_json(output / "runner.json", metadata)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path, help="clean E5 Git worktree root")
    parser.add_argument("--pack", required=True, type=Path, help="frozen numeric E5 status pack")
    parser.add_argument("--harness", required=True, type=Path, help="Release net10.0 CivSim.Harness.dll")
    parser.add_argument("--sim", required=True, type=Path, help="adjacent Release net10.0 CivSim.Sim.dll")
    parser.add_argument("--manifest", required=True, type=Path, help="frozen cohort JSON")
    parser.add_argument("--manifest-sha256", required=True)
    parser.add_argument("--seed-file", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--cohort-role", required=True, choices=tuple(COHORT_SIZES))
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--preflight-only", action="store_true")
    args = parser.parse_args()
    candidates = {"source": args.source, "pack": args.pack, "harness": args.harness, "sim": args.sim}
    try:
        result = run(args.manifest, candidates, args.output, cohort_role=args.cohort_role,
                     seed_file=args.seed_file, manifest_sha256=args.manifest_sha256,
                     dotnet=args.dotnet, preflight_only=args.preflight_only)
        print(json.dumps(result, indent=2, sort_keys=True))
        return 0
    except (OSError, ValueError, RuntimeError, json.JSONDecodeError, subprocess.CalledProcessError) as exc:
        print(f"wp7_trace_runner: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
