#!/usr/bin/env python3
"""Preflight-first, fail-closed runner for the WP-E5-9 whole-match A8 gate.

By default this verifies the frozen tuple and builds the test assembly, then
stops. Passing --execute additionally runs only the named A8 whole-match test.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
import sys
import tempfile
import time
from pathlib import Path
from typing import Any, Sequence

import validate_fief_a8_result as result_validator

TEST_FILTER = (
    "FullyQualifiedName~CivSim.Tests.FiefWholeMatchFogFairnessTests."
    "Whole_fief_matches_are_invariant_to_unseen_state_and_respond_to_visible_state"
)
TEST_PROJECT = Path("src/CivSim.Tests/CivSim.Tests.csproj")
DEFAULT_TEST_SOURCE = Path("src/CivSim.Tests/FiefWholeMatchFogFairnessTests.cs")
HASH_HELPER = Path("docs/MVP/balance/tools/hash_e5_workspace.py")
SCOPES = ("content", "src", "game")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def parse_seed_manifest(path: Path) -> list[int]:
    seeds: list[int] = []
    for line_no, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = raw.split("#", 1)[0].strip()
        if not line:
            continue
        for item in line.replace(",", " ").split():
            try:
                value = int(item, 10)
            except ValueError as exc:
                raise ValueError(f"{path}:{line_no}: invalid integer seed {item!r}") from exc
            if value < 0 or value > (2**64 - 1):
                raise ValueError(f"{path}:{line_no}: seed must fit an unsigned 64-bit integer")
            seeds.append(value)
    if not seeds:
        raise ValueError(f"{path}: seed manifest is empty")
    if len(seeds) != len(set(seeds)):
        raise ValueError(f"{path}: duplicate seed values")
    return seeds


def parse_exclusion(value: str) -> tuple[Path, str]:
    path_text, separator, expected = value.rpartition("=")
    if not separator or not path_text or len(expected) != 64:
        raise ValueError("--exclude-seed-manifest must be PATH=SHA256")
    return Path(path_text), expected.lower()


def git_output(repo: Path, *args: str) -> str:
    result = subprocess.run(["git", "-C", str(repo), *args], check=True,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    return result.stdout.strip()


def ensure_source_clean(repo: Path) -> None:
    status = git_output(repo, "status", "--porcelain=v1", "--untracked-files=all", "--",
                        *SCOPES)
    if status:
        raise ValueError("source checkout is dirty under content/, src/, or game/:\n" + status)


def source_snapshot(repo: Path) -> dict[str, str]:
    head = git_output(repo, "rev-parse", "HEAD")
    tree = git_output(repo, "rev-parse", "HEAD^{tree}")
    with tempfile.TemporaryDirectory(prefix="fief-a8-source-") as temp:
        report_path = Path(temp) / "source-hash.json"
        report = subprocess.run(
            [sys.executable, str(repo / HASH_HELPER), "--output", str(report_path)],
            cwd=repo, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
        )
        if report.returncode != 0:
            raise ValueError(f"source hash helper failed: {report.stderr.strip()}")
        data = json.loads(report_path.read_text(encoding="utf-8"))
    return {"head": head, "tree": tree,
            "contentSrcGameAggregateSha256": data["aggregate_sha256"]}


def verify_snapshot(repo: Path, expected_head: str, expected_tree: str,
                    expected_aggregate: str) -> dict[str, str]:
    ensure_source_clean(repo)
    actual = source_snapshot(repo)
    expected = (expected_head, expected_tree, expected_aggregate)
    observed = (actual["head"], actual["tree"], actual["contentSrcGameAggregateSha256"])
    if observed != expected:
        raise ValueError(f"source tuple mismatch: expected {expected}, got {observed}")
    return actual


def verify_hash(path: Path, expected: str, label: str) -> str:
    if not path.is_file():
        raise ValueError(f"{label} does not exist: {path}")
    actual = sha256_file(path)
    if actual.lower() != expected.lower():
        raise ValueError(f"{label} SHA-256 mismatch: expected {expected}, got {actual}")
    return actual


def require_absent_output_root(path: Path) -> None:
    if path.exists() or path.is_symlink():
        raise ValueError(f"fresh output root already exists: {path}")


def validate_seed_inputs(seed_manifest: Path, seed_manifest_sha256: str,
                         exclusions: Sequence[str]) -> list[int]:
    verify_hash(seed_manifest, seed_manifest_sha256, "seed manifest")
    seeds = parse_seed_manifest(seed_manifest)
    if len(seeds) != 8:
        raise ValueError(f"A8 execution requires exactly 8 unique seeds, got {len(seeds)}")
    for item in exclusions:
        path, expected = parse_exclusion(item)
        verify_hash(path, expected, f"excluded seed manifest {path}")
        overlap = sorted(set(seeds).intersection(parse_seed_manifest(path)))
        if overlap:
            raise ValueError(f"seed manifest overlaps {path}: {overlap}")
    return seeds


def run_logged(command: Sequence[str], repo: Path, logfile: Path,
               env: dict[str, str] | None = None) -> dict[str, Any]:
    started = time.time()
    with logfile.open("wb") as output:
        process = subprocess.run(command, cwd=repo, env=env, stdout=output,
                                 stderr=subprocess.STDOUT, check=False)
    return {"command": list(command), "exitCode": process.returncode,
            "seconds": round(time.time() - started, 3), "log": str(logfile),
            "logSha256": sha256_file(logfile), "logBytes": logfile.stat().st_size}


def build_metadata_snapshot(snapshot: dict[str, str], pack: Path, harness: Path,
                            sim: Path, args: argparse.Namespace) -> dict[str, Any]:
    binaries: dict[str, str | None] = {}
    for key, path in (("pack", pack), ("harnessDll", harness), ("simDll", sim)):
        try:
            binaries[key] = sha256_file(path)
        except OSError:
            binaries[key] = None
    test_source_path = args.test_source if args.test_source.is_absolute() else args.repo / args.test_source
    try:
        test_source_hash: str | None = sha256_file(test_source_path)
    except OSError:
        test_source_hash = None
    return {
        "snapshot": snapshot,
        "binaries": binaries,
        "paths": {"pack": str(pack), "harnessDll": str(harness), "simDll": str(sim)},
        "testSource": {"path": str(args.test_source), "sha256": test_source_hash},
    }


def validate_args(args: argparse.Namespace, repo: Path) -> tuple[list[int], dict[str, str], dict[str, Path]]:
    if args.minutes != 64 or args.interval_seasons != 16:
        raise ValueError("A8 requires --minutes 64 and --interval-seasons 16")
    if args.dop < 1:
        raise ValueError("--dop must be a positive integer")
    paths = {
        "pack": args.numeric_pack.resolve(), "harness": args.harness_dll.resolve(),
        "sim": args.sim_dll.resolve(), "test_source": (repo / args.test_source).resolve(),
        "seed_manifest": args.seed_manifest.resolve(),
        "runner": Path(__file__).resolve(),
        "validator": Path(result_validator.__file__).resolve(),
        "hash_helper": repo / HASH_HELPER,
    }
    if paths["harness"].parent != paths["sim"].parent:
        raise ValueError("Release Harness and adjacent Sim must be in the same directory")
    snapshot = verify_snapshot(repo, args.source_head, args.source_tree,
                               args.source_aggregate_sha256)
    verify_hash(paths["pack"], args.pack_sha256, "numeric pack")
    verify_hash(paths["harness"], args.harness_sha256, "Release Harness")
    verify_hash(paths["sim"], args.sim_sha256, "adjacent Release Sim")
    verify_hash(paths["test_source"], args.test_source_sha256, "corrected A8 test source")
    verify_hash(paths["runner"], args.runner_sha256, "A8 runner")
    verify_hash(paths["validator"], args.validator_sha256, "A8 result validator")
    verify_hash(paths["hash_helper"], args.hash_helper_sha256, "source hash helper")
    seeds = validate_seed_inputs(paths["seed_manifest"], args.seed_manifest_sha256,
                                 args.exclude_seed_manifest)
    output = args.output_root.resolve()
    require_absent_output_root(output)
    if any(output == (repo / scope).resolve() or (repo / scope).resolve() in output.parents
           for scope in SCOPES):
        raise ValueError("output root must be outside content/, src/, and game/")
    return seeds, snapshot, paths


def collect_artifacts(output_root: Path) -> dict[str, dict[str, Any]]:
    artifacts: dict[str, dict[str, Any]] = {}
    for name in ("a8-summary.json", "a8-rows.jsonl"):
        path = output_root / name
        if path.is_file():
            artifacts[name] = {"sha256": sha256_file(path), "bytes": path.stat().st_size}
    return artifacts


def build_pins(args: argparse.Namespace, snapshot: dict[str, str], seeds: list[int],
               test_assembly_sha256: str) -> dict[str, Any]:
    return {
        "head": snapshot["head"],
        "sourceTree": snapshot["tree"],
        "sourceAggregateSha256": snapshot["contentSrcGameAggregateSha256"],
        "packSha256": args.pack_sha256.lower(),
        "harnessSha256": args.harness_sha256.lower(),
        "simSha256": args.sim_sha256.lower(),
        "testSourceSha256": args.test_source_sha256.lower(),
        "testAssemblySha256": test_assembly_sha256,
        "runnerSha256": args.runner_sha256.lower(),
        "validatorSha256": args.validator_sha256.lower(),
        "hashHelperSha256": args.hash_helper_sha256.lower(),
        "seedManifestSha256": args.seed_manifest_sha256.lower(),
        "seeds": seeds, "minutes": args.minutes,
        "intervalSeasons": args.interval_seasons, "dop": args.dop,
    }


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    default_repo = Path(__file__).resolve().parents[4]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=default_repo)
    parser.add_argument("--source-head", required=True)
    parser.add_argument("--source-tree", required=True)
    parser.add_argument("--source-aggregate-sha256", required=True)
    parser.add_argument("--numeric-pack", required=True, type=Path)
    parser.add_argument("--pack-sha256", required=True)
    parser.add_argument("--harness-dll", required=True, type=Path)
    parser.add_argument("--harness-sha256", required=True)
    parser.add_argument("--sim-dll", required=True, type=Path)
    parser.add_argument("--sim-sha256", required=True)
    parser.add_argument("--test-source", type=Path, default=DEFAULT_TEST_SOURCE)
    parser.add_argument("--test-source-sha256", required=True)
    parser.add_argument("--test-assembly-sha256", required=True)
    parser.add_argument("--runner-sha256", required=True)
    parser.add_argument("--validator-sha256", required=True)
    parser.add_argument("--hash-helper-sha256", required=True)
    parser.add_argument("--seed-manifest", required=True, type=Path)
    parser.add_argument("--seed-manifest-sha256", required=True)
    parser.add_argument("--exclude-seed-manifest", action="append", required=True, metavar="PATH=SHA256",
                        help="previous/reserved seed manifest; repeat to require disjointness")
    parser.add_argument("--minutes", type=int, default=64)
    parser.add_argument("--interval-seasons", type=int, default=16)
    parser.add_argument("--dop", type=int, required=True)
    parser.add_argument("--output-root", required=True, type=Path)
    parser.add_argument("--execute", action="store_true",
                        help="after successful preflight/build, run the A8 whole-match test")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    repo = args.repo.resolve()
    args.repo = repo
    output_root = args.output_root.resolve()
    try:
        seeds, snapshot, paths = validate_args(args, repo)
        build_cmd = ["dotnet", "build", str(TEST_PROJECT), "--configuration", "Release"]
        # Build log is emitted in the OS temporary directory so preflight-only does not create the run root.
        build_log = Path(os.getenv("TMPDIR", "/tmp")) / f"fief-a8-build-{os.getpid()}.log"
        build = run_logged(build_cmd, repo, build_log)
        if build["exitCode"] != 0:
            raise ValueError(f"Release test-project build failed; log: {build_log}")
        test_output = repo / "src/CivSim.Tests/bin/Release/net10.0"
        assembly = test_output / "CivSim.Tests.dll"
        built_sim = test_output / "CivSim.Sim.dll"
        built_harness = test_output / "CivSim.Harness.dll"
        test_assembly_sha = verify_hash(assembly, args.test_assembly_sha256, "built test assembly")
        verify_hash(built_sim, args.sim_sha256, "test-output adjacent Sim")
        verify_hash(built_harness, args.harness_sha256, "test-output Harness")
        # Confirm source/binary pins remained stable through build.
        post_build = verify_snapshot(repo, args.source_head, args.source_tree,
                                     args.source_aggregate_sha256)
        if validate_seed_inputs(paths["seed_manifest"], args.seed_manifest_sha256,
                                args.exclude_seed_manifest) != seeds:
            raise ValueError("frozen seed list changed during Release build")
        for key, path, expected, label in (
            ("pack", paths["pack"], args.pack_sha256, "numeric pack"),
            ("harness", paths["harness"], args.harness_sha256, "Release Harness"),
            ("sim", paths["sim"], args.sim_sha256, "adjacent Release Sim"),
            ("test_source", paths["test_source"], args.test_source_sha256, "corrected A8 test source"),
        ):
            verify_hash(path, expected, label)
        for key, expected, label in (
            ("runner", args.runner_sha256, "A8 runner"),
            ("validator", args.validator_sha256, "A8 result validator"),
            ("hash_helper", args.hash_helper_sha256, "source hash helper"),
        ):
            verify_hash(paths[key], expected, label)
        report = {
            "status": "preflight_passed", "executeRequested": args.execute,
            "source": snapshot, "seeds": seeds,
            "inputs": {"packSha256": args.pack_sha256.lower(),
                       "harnessSha256": args.harness_sha256.lower(),
                       "simSha256": args.sim_sha256.lower(),
                       "testSourceSha256": args.test_source_sha256.lower(),
                       "testAssemblySha256": test_assembly_sha,
                       "runnerSha256": args.runner_sha256.lower(),
                       "validatorSha256": args.validator_sha256.lower(),
                       "hashHelperSha256": args.hash_helper_sha256.lower(),
                       "seedManifestSha256": args.seed_manifest_sha256.lower()},
            "build": build, "postBuildSource": post_build,
            "outputRoot": str(output_root),
        }
        if not args.execute:
            print(json.dumps(report, indent=2, sort_keys=True))
            return 0
        # Recheck that output root is still absent immediately before creating it.
        require_absent_output_root(output_root)
        output_root.mkdir(parents=True, exist_ok=False)
        pins = build_pins(args, snapshot, seeds, test_assembly_sha)
        pins_path = output_root / "a8-pins.json"
        pins_path.write_text(json.dumps(pins, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        (output_root / "build.log").write_bytes(build_log.read_bytes())
        (output_root / "preflight.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n",
                                                       encoding="utf-8")
        (output_root / "run_metadata.json").write_text(json.dumps({
            "status": "running", "preflight": build_metadata_snapshot(snapshot, paths["pack"],
                paths["harness"], paths["sim"], args), "pins": pins,
            "testSourceSha256": args.test_source_sha256.lower(),
            "testBinaries": {"CivSim.Tests.dll": test_assembly_sha,
                             "CivSim.Harness.dll": sha256_file(built_harness)},
            "seeds": seeds,
        }, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        env = os.environ.copy()
        env.update({
            "CIVSIM_A8_PACK": str(paths["pack"]),
            "CIVSIM_A8_OUT": str(output_root),
            "CIVSIM_A8_SEEDS": ",".join(str(seed) for seed in seeds),
            "CIVSIM_A8_INTERVAL": str(args.interval_seasons),
            "CIVSIM_A8_MINUTES": str(args.minutes),
            "CIVSIM_A8_DOP": str(args.dop),
            "CIVSIM_A8_TIERB": "1",
        })
        test_cmd = ["dotnet", "test", str(TEST_PROJECT), "--configuration", "Release", "--no-build",
                    "--filter", TEST_FILTER]
        test_log = output_root / "test.log"
        test_result = run_logged(test_cmd, repo, test_log, env)
        post_snapshot = source_snapshot(repo)
        post_errors: list[str] = []
        try:
            ensure_source_clean(repo)
        except ValueError as exc:
            post_errors.append(str(exc))
        if post_snapshot != snapshot:
            post_errors.append("source HEAD/tree/content-src-game aggregate changed during A8 run")
        try:
            if validate_seed_inputs(paths["seed_manifest"], args.seed_manifest_sha256,
                                    args.exclude_seed_manifest) != seeds:
                post_errors.append("frozen seed list changed during A8 run")
        except ValueError as exc:
            post_errors.append(str(exc))
        for path, expected, label in (
            (paths["pack"], args.pack_sha256, "numeric pack"),
            (paths["harness"], args.harness_sha256, "Release Harness"),
            (paths["sim"], args.sim_sha256, "adjacent Release Sim"),
            (paths["test_source"], args.test_source_sha256, "corrected A8 test source"),
            (assembly, args.test_assembly_sha256, "built test assembly"),
            (built_sim, args.sim_sha256, "test-output adjacent Sim"),
            (built_harness, args.harness_sha256, "test-output Harness"),
        ):
            try:
                verify_hash(path, expected, label)
            except ValueError as exc:
                post_errors.append(str(exc))
        for path, expected, label in (
            (paths["runner"], args.runner_sha256, "A8 runner"),
            (paths["validator"], args.validator_sha256, "A8 result validator"),
            (paths["hash_helper"], args.hash_helper_sha256, "source hash helper"),
        ):
            try:
                verify_hash(path, expected, label)
            except ValueError as exc:
                post_errors.append(str(exc))
        artifacts = collect_artifacts(output_root)
        metadata = {
            "status": ("input_changed" if post_errors else
                       "completed" if test_result["exitCode"] == 0 else "test_failed"),
            "unchangedDuringRun": not post_errors and snapshot == post_snapshot,
            "preflight": build_metadata_snapshot(snapshot, paths["pack"], paths["harness"], paths["sim"], args),
            "postflight": build_metadata_snapshot(post_snapshot, paths["pack"], paths["harness"], paths["sim"], args),
            "testSourceSha256": args.test_source_sha256.lower(),
            "toolPins": {"runnerSha256": args.runner_sha256.lower(),
                         "validatorSha256": args.validator_sha256.lower(),
                         "hashHelperSha256": args.hash_helper_sha256.lower()},
            "testBinaries": {"CivSim.Tests.dll": test_assembly_sha,
                             "CivSim.Harness.dll": sha256_file(built_harness)},
            "seeds": seeds,
            "env": {key: env[key] for key in ("CIVSIM_A8_PACK", "CIVSIM_A8_OUT", "CIVSIM_A8_SEEDS",
                "CIVSIM_A8_INTERVAL", "CIVSIM_A8_MINUTES", "CIVSIM_A8_DOP", "CIVSIM_A8_TIERB")},
            "output": test_result, "build": build, "artifacts": artifacts,
            "pins": pins, "postflightErrors": post_errors,
        }
        (output_root / "run_metadata.json").write_text(json.dumps(metadata, indent=2, sort_keys=True) + "\n",
                                                           encoding="utf-8")
        try:
            validation = result_validator.validate(output_root, pins_path)
        except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as exc:
            validation = {"status": "invalid", "error": str(exc)}
        (output_root / "validation.json").write_text(json.dumps(validation, indent=2, sort_keys=True) + "\n",
                                                       encoding="utf-8")
        print(json.dumps({"run": metadata, "validation": validation}, indent=2, sort_keys=True))
        return 0 if (test_result["exitCode"] == 0 and not post_errors
                     and validation["status"] == "pass") else 1
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError,
            json.JSONDecodeError) as exc:
        print(json.dumps({"status": "blocked", "error": str(exc)}, indent=2), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
