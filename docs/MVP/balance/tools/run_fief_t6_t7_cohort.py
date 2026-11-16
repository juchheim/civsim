#!/usr/bin/env python3
"""Run the frozen 20-seed natural Fief health/institution measurement cohort."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import subprocess
import sys
from pathlib import Path

from cohort_provenance import capture, verify_expected


EXPECTED_MANIFEST_SHA256 = "a92dd049c1c56836dd2bffad3106985694be2eb755f15b4665c0937ae318f038"
DEFAULT_MANIFEST = Path(__file__).resolve().parents[1] / "m12-e5-9-f-t6-t7-natural-seeds-2026-11-14.tsv"
DEFAULT_HARNESS = Path(__file__).resolve().parents[4] / "src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll"
DEFAULT_OUTPUT = Path(__file__).resolve().parents[1] / "m12-e5-9-f-t6-t7-natural-output-2026-11-14"
PINNED_TEST_SOURCES = (
    Path("src/CivSim.Tests/FiefInstitutionTraceTests.cs"),
    Path("src/CivSim.Tests/FiefHealthTraceTests.cs"),
    Path("src/CivSim.Tests/FiefQuarantineTests.cs"),
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_manifest(path: Path) -> list[int]:
    seeds: list[int] = []
    for line_number, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("#") or line.lower() == "seed":
            continue
        try:
            seeds.append(int(line))
        except ValueError as exc:
            raise ValueError(f"{path}:{line_number}: expected an integer seed, got {line!r}") from exc
    if len(seeds) != 20 or len(set(seeds)) != 20:
        raise ValueError(f"frozen F-T6/F-T7 manifest must contain 20 unique seeds; found {len(seeds)} rows")
    return seeds


def capture_run_provenance(repo_root: Path, args: argparse.Namespace, manifest_hash: str) -> dict[str, object]:
    """Capture the extra source/runtime pins omitted from legacy run_metadata."""
    test_dll = args.harness_dll.parent / "CivSim.Tests.dll"
    test_sources = {str(path): sha256(repo_root / path) for path in PINNED_TEST_SOURCES}
    runtime = subprocess.run([args.dotnet, "--version"], check=True, capture_output=True, text=True).stdout.strip()
    return {
        "cohortRole": args.cohort_role,
        "manifestPath": str(args.manifest.resolve()),
        "manifestSha256": manifest_hash,
        "sourceAggregateSha256": capture(repo_root, args.pack, args.harness_dll, args.sim_dll)["sourceAggregateSha256"],
        "packSha256": sha256(args.pack),
        "harnessDllSha256": sha256(args.harness_dll),
        "simDllSha256": sha256(args.sim_dll),
        "testAssemblyPath": str(test_dll) if test_dll.is_file() else None,
        "testAssemblySha256": sha256(test_dll) if test_dll.is_file() else None,
        "testSources": test_sources,
        "runnerSha256": sha256(Path(__file__).resolve()),
        "analyzerSha256": sha256(Path(__file__).with_name("analyze_fief_t6_t7_cohort.py")),
        "testHelperSha256": sha256(Path(__file__).with_name("test_fief_t6_t7_cohort.py")),
        "dotnetVersion": runtime,
    }


def verify_run_provenance(repo_root: Path, args: argparse.Namespace, manifest_hash: str,
                          provenance: dict[str, object]) -> None:
    current = capture_run_provenance(repo_root, args, manifest_hash)
    if current != provenance:
        raise ValueError("WP-E5-6 source, binary, test, runner, analyzer, manifest, or runtime pin changed during run")


def make_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pack", required=True, type=Path, help="Exact natural Fief content pack to measure")
    parser.add_argument("--harness-dll", type=Path, default=DEFAULT_HARNESS)
    parser.add_argument("--sim-dll", type=Path, help="adjacent CivSim.Sim.dll; defaults beside the harness")
    parser.add_argument("--expected-head", required=True)
    parser.add_argument("--expected-source-aggregate", required=True)
    parser.add_argument("--expected-pack-sha256", required=True)
    parser.add_argument("--expected-harness-sha256", required=True)
    parser.add_argument("--expected-sim-sha256", required=True)
    parser.add_argument("--expected-manifest-sha256", required=True,
                        help="required SHA-256, including for a custom manifest path")
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--resume", action="store_true",
                        help="resume an interrupted running output directory after verifying its frozen inputs and completed outputs")
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--minutes", type=int, default=130)
    parser.add_argument("--cohort-role", choices=("calibration", "held_out"), default="calibration")
    return parser


def run(args: argparse.Namespace) -> int:
    args.pack = args.pack.resolve()
    args.harness_dll = args.harness_dll.resolve()
    args.sim_dll = (args.sim_dll or args.harness_dll.with_name("CivSim.Sim.dll")).resolve()
    args.manifest = args.manifest.resolve()
    args.output_dir = args.output_dir.resolve()
    for path, label in ((args.pack, "content pack"), (args.harness_dll, "harness DLL"), (args.sim_dll, "adjacent Sim DLL"), (args.manifest, "seed manifest")):
        if not path.is_file():
            raise FileNotFoundError(f"{label} does not exist: {path}")
    manifest_hash = sha256(args.manifest)
    if manifest_hash != args.expected_manifest_sha256:
        raise ValueError(f"manifest SHA-256 mismatch: expected {args.expected_manifest_sha256}, got {manifest_hash}")
    if args.manifest == DEFAULT_MANIFEST.resolve() and manifest_hash != EXPECTED_MANIFEST_SHA256:
        raise ValueError(f"pinned default manifest mismatch: expected {EXPECTED_MANIFEST_SHA256}, got {manifest_hash}")
    seeds = load_manifest(args.manifest)
    repo_root = Path(__file__).resolve().parents[4]
    provenance_before = capture(repo_root, args.pack, args.harness_dll, args.sim_dll)
    run_provenance = capture_run_provenance(repo_root, args, manifest_hash)
    verify_expected(provenance_before, {
        "headCommit": args.expected_head,
        "sourceAggregateSha256": args.expected_source_aggregate,
        "packSha256": args.expected_pack_sha256,
        "harnessDllSha256": args.expected_harness_sha256,
        "simDllSha256": args.expected_sim_sha256,
    })
    if args.minutes != 130:
        raise ValueError("the frozen-cohort measurement horizon is fixed at 130 minutes")
    command_template = [args.dotnet, str(args.harness_dll), "fief-match", "--pack", str(args.pack),
                        "--seed", "<manifest-seed>", "--minutes", "130", "--fief-health-trace",
                        "--fief-institution-trace"]
    metadata_path = args.output_dir / "run_metadata.json"
    if args.resume:
        if not args.output_dir.is_dir() or not metadata_path.is_file():
            raise FileNotFoundError(f"cannot resume without an existing run directory and metadata: {args.output_dir}")
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
        if not isinstance(metadata, dict):
            raise ValueError("resume metadata must be a JSON object")
        if metadata.get("status") != "running":
            raise ValueError(f"only an interrupted run marked 'running' can be resumed; found {metadata.get('status')!r}")
        expected_inputs = {
            "workingDirectory": str(Path.cwd()),
            "cohortRole": args.cohort_role,
            "seedCount": len(seeds),
            "seeds": seeds,
            "minutes": 130,
            "packPath": str(args.pack),
            "packSha256": sha256(args.pack),
            "harnessDllPath": str(args.harness_dll),
            "harnessDllSha256": sha256(args.harness_dll),
            "simDllPath": str(args.sim_dll),
            "simDllSha256": sha256(args.sim_dll),
            "headCommit": provenance_before["headCommit"],
            "sourceAggregateSha256": provenance_before["sourceAggregateSha256"],
            "manifestPath": str(args.manifest),
            "manifestSha256": manifest_hash,
            "commandTemplate": command_template,
        }
        for key, expected in expected_inputs.items():
            if metadata.get(key) != expected:
                raise ValueError(f"resume input mismatch for {key}: expected {expected!r}, got {metadata.get(key)!r}")
        runs = metadata.get("runs")
        if not isinstance(runs, list) or len(runs) > len(seeds):
            raise ValueError("resume metadata has an invalid completed run list")
        for index, run_info in enumerate(runs):
            seed = seeds[index]
            expected_name = f"seed-{seed}.txt"
            expected_command = [args.dotnet, str(args.harness_dll), "fief-match", "--pack", str(args.pack),
                                "--seed", str(seed), "--minutes", "130", "--fief-health-trace",
                                "--fief-institution-trace"]
            if (not isinstance(run_info, dict) or run_info.get("seed") != seed or run_info.get("exitCode") != 0 or
                    run_info.get("command") != expected_command or
                    run_info.get("rawFile") != expected_name):
                raise ValueError(f"resume metadata is not a successful ordered seed prefix at seed {seed}")
            raw_path = args.output_dir / expected_name
            if not raw_path.is_file() or sha256(raw_path) != run_info.get("rawSha256"):
                raise ValueError(f"completed raw output is missing or has a hash mismatch: {raw_path}")
        allowed_raw_names = {f"seed-{seed}.txt" for seed in seeds[:len(runs) + 1]}
        actual_raw_names = {path.name for path in args.output_dir.glob("seed-*.txt")}
        unexpected_raw_names = actual_raw_names - allowed_raw_names
        if unexpected_raw_names:
            raise ValueError(f"resume directory contains unrecorded future or unexpected seed outputs: {sorted(unexpected_raw_names)}")
        resume_events = metadata.setdefault("resumeEvents", [])
        if not isinstance(resume_events, list):
            raise ValueError("resume metadata has an invalid resumeEvents list")
        resume_events.append({
            "resumedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
            "verifiedCompletedSeeds": len(runs),
            "firstSeedToRun": seeds[len(runs)] if len(runs) < len(seeds) else None,
        })
        start_index = len(runs)
    else:
        args.output_dir.mkdir(parents=True, exist_ok=False)
        metadata = {
            "status": "running",
            "startedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
            "workingDirectory": str(Path.cwd()),
            "seedCount": len(seeds),
            "cohortRole": args.cohort_role,
            "seeds": seeds,
            "minutes": 130,
            "packPath": str(args.pack.resolve()),
            "packSha256": sha256(args.pack),
            "harnessDllPath": str(args.harness_dll.resolve()),
            "harnessDllSha256": sha256(args.harness_dll),
            "simDllPath": str(args.sim_dll),
            "simDllSha256": sha256(args.sim_dll),
            "headCommit": provenance_before["headCommit"],
            "sourceAggregateSha256": provenance_before["sourceAggregateSha256"],
            "manifestPath": str(args.manifest.resolve()),
            "manifestSha256": manifest_hash,
            "commandTemplate": command_template,
            "runs": [],
        }
        start_index = 0

    provenance_path = args.output_dir / "provenance.json"
    if args.resume:
        if not provenance_path.is_file():
            raise ValueError("resume provenance.json is missing")
        saved_run_provenance = json.loads(provenance_path.read_text(encoding="utf-8"))
        if saved_run_provenance != run_provenance:
            changed = sorted(key for key in set(saved_run_provenance) | set(run_provenance)
                             if saved_run_provenance.get(key) != run_provenance.get(key))
            raise ValueError("resume provenance.json differs from frozen WP-E5-6 pins: " + ", ".join(changed))
    else:
        provenance_path.write_text(json.dumps(run_provenance, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    def write_metadata() -> None:
        metadata_path.write_text(json.dumps(metadata, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    write_metadata()
    for seed in seeds[start_index:]:
        raw_path = args.output_dir / f"seed-{seed}.txt"
        command = [args.dotnet, str(args.harness_dll), "fief-match", "--pack", str(args.pack),
                   "--seed", str(seed), "--minutes", "130", "--fief-health-trace", "--fief-institution-trace"]
        with raw_path.open("w", encoding="utf-8", newline="\n") as output:
            completed = subprocess.run(command, stdout=output, stderr=subprocess.STDOUT, check=False, text=True)
        run_info = {"seed": seed, "command": command, "rawFile": raw_path.name, "exitCode": completed.returncode,
                    "rawSha256": sha256(raw_path)}
        metadata["runs"].append(run_info)
        write_metadata()
        if completed.returncode != 0:
            metadata["status"] = "failed"
            metadata["failedSeed"] = seed
            write_metadata()
            return completed.returncode

    provenance_after = capture(repo_root, args.pack, args.harness_dll, args.sim_dll)
    verify_run_provenance(repo_root, args, manifest_hash, run_provenance)
    metadata["provenanceAfter"] = provenance_after
    if provenance_after != provenance_before:
        metadata["status"] = "failed"
        metadata["provenanceChangedDuringRun"] = True
        metadata["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
        write_metadata()
        return 2
    metadata["status"] = "complete"
    metadata["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
    write_metadata()
    return 0


def main(argv: list[str] | None = None) -> int:
    args = make_parser().parse_args(argv)
    try:
        return run(args)
    except (OSError, ValueError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
