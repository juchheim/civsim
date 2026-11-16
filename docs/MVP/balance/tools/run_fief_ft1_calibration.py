#!/usr/bin/env python3
"""Run the pinned 20-world F-T1 calibration block, never the acceptance holdout."""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import subprocess
import sys
import shutil
from pathlib import Path
from typing import Any, Callable

from cohort_provenance import capture, verify_expected

EXPECTED_MANIFEST_SHA256 = "ec992c60c6e4b2820f401de529a680a74b0bcd77eb33dc57d8e75eecc3ceb417"
EXPECTED_BASE_COST = 6000
EXPECTED_BASE_COST_RECORD_SHA256 = "85180312495f72ce543a2087b76219bacb96c5ed9ae4e1424a8281bfef7ff92a"
EXPECTED_SEED_COUNT = 20
MINUTES = 130


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
            seed = int(line)
        except ValueError as exc:
            raise ValueError(f"{path}:{line_number}: expected an integer seed, got {line!r}") from exc
        if seed < 0:
            raise ValueError(f"{path}:{line_number}: seed must be nonnegative")
        seeds.append(seed)
    if len(seeds) != EXPECTED_SEED_COUNT or len(set(seeds)) != EXPECTED_SEED_COUNT:
        raise ValueError(f"frozen F-T1 calibration manifest must contain exactly {EXPECTED_SEED_COUNT} unique seeds; found {len(seeds)}")
    return seeds


def record_hash(record: dict[str, Any]) -> str:
    canonical = json.dumps(record, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(canonical).hexdigest()


def read_base_cost_from_eras(path: Path) -> int:
    for line in path.read_text(encoding="utf-8").splitlines():
        if "{id: 5," in line:
            marker = "base_cost:"
            if marker not in line:
                break
            return int(line.split(marker, 1)[1].split(",", 1)[0].strip())
    raise ValueError(f"could not find E5 (id 5) base_cost in {path}")


def git_is_clean(repo: Path) -> bool:
    result = subprocess.run(["git", "-C", str(repo), "status", "--porcelain", "--untracked-files=all"],
                            check=True, capture_output=True, text=True)
    return not result.stdout.strip()


def expected_pins(args: argparse.Namespace) -> dict[str, str]:
    return {
        "headCommit": args.expected_head,
        "sourceAggregateSha256": args.expected_source_aggregate,
        "packSha256": args.expected_pack_sha256,
        "harnessDllSha256": args.expected_harness_sha256,
        "simDllSha256": args.expected_sim_sha256,
    }


def verify_candidate(repo: Path, pack: Path, harness: Path, sim: Path, eras: Path,
                     pins: dict[str, str], runner_sha256: str, eras_sha256: str, *,
                     capture_fn: Callable[..., dict[str, Any]],
                     clean_fn: Callable[[Path], bool]) -> dict[str, Any]:
    if not clean_fn(repo):
        raise ValueError("candidate repository is not clean")
    actual = capture_fn(repo, pack, harness, sim)
    verify_expected(actual, pins)
    actual_runner = sha256(Path(__file__).resolve())
    if actual_runner.lower() != runner_sha256.lower():
        raise ValueError(f"runner SHA-256 mismatch: expected {runner_sha256}, got {actual_runner}")
    actual_eras = sha256(eras)
    if actual_eras.lower() != eras_sha256.lower():
        raise ValueError(f"E5 era registry SHA-256 mismatch: expected {eras_sha256}, got {actual_eras}")
    return {**actual, "runnerSha256": actual_runner, "erasSha256": actual_eras}


def validate_inputs(args: argparse.Namespace, *, capture_fn: Callable[..., dict[str, Any]] = capture,
                    clean_fn: Callable[[Path], bool] = git_is_clean) -> tuple[list[int], dict[str, Any]]:
    repo, pack, harness, sim, manifest, eras, out = (
        Path(p).resolve() for p in (args.repo, args.pack, args.harness_dll, args.sim_dll,
                                    args.manifest, args.eras, args.output_dir)
    )
    if not repo.is_dir():
        raise FileNotFoundError(f"candidate repository does not exist: {repo}")
    for path, label in ((pack, "numeric pack"), (harness, "Release Harness DLL"),
                        (sim, "adjacent Release Sim DLL"), (manifest, "F-T1 calibration manifest"),
                        (eras, "E5 era registry")):
        if not path.is_file():
            raise FileNotFoundError(f"{label} does not exist: {path}")
    if args.minutes != MINUTES:
        raise ValueError(f"F-T1 calibration horizon is fixed at {MINUTES} minutes")
    if harness.name != "CivSim.Harness.dll" or "Release" not in harness.parts:
        raise ValueError("harness must be the Release CivSim.Harness.dll")
    if sim.name != "CivSim.Sim.dll" or sim.parent != harness.parent:
        raise ValueError("sim must be adjacent to the Release Harness DLL")
    if out.exists():
        raise FileExistsError(f"refusing existing output root: {out}")
    if repo == out or repo in out.parents:
        raise ValueError("output root must be outside the candidate repository")
    if len(args.expected_manifest_sha256) != 64:
        raise ValueError("--expected-manifest-sha256 must be supplied as a 64-digit SHA-256")
    if args.expected_manifest_sha256.lower() != EXPECTED_MANIFEST_SHA256:
        raise ValueError("expected manifest SHA does not identify the frozen F-T1 calibration block")
    manifest_hash = sha256(manifest)
    if manifest_hash.lower() != args.expected_manifest_sha256.lower():
        raise ValueError(f"manifest SHA-256 mismatch: expected {args.expected_manifest_sha256}, got {manifest_hash}")
    if len(args.expected_eras_sha256) != 64:
        raise ValueError("--expected-eras-sha256 must be supplied as a 64-digit SHA-256")
    eras_hash = sha256(eras)
    if eras_hash.lower() != args.expected_eras_sha256.lower():
        raise ValueError(f"E5 era registry SHA-256 mismatch: expected {args.expected_eras_sha256}, got {eras_hash}")
    source_base = read_base_cost_from_eras(eras)
    source_base_hash = record_hash({"id": 5, "base_cost": source_base})
    if source_base != EXPECTED_BASE_COST or source_base_hash != EXPECTED_BASE_COST_RECORD_SHA256:
        raise ValueError(f"F-T1 calibration requires repository E5 base cost {EXPECTED_BASE_COST}")
    try:
        pack_data = json.loads(pack.read_text(encoding="utf-8"))
        pack_eras = pack_data["registries"]["eras"]
        e5 = next(row for row in pack_eras if int(row["id"]) == 5)
        pack_base = int(e5["base_cost"])
    except (KeyError, TypeError, ValueError, StopIteration, json.JSONDecodeError) as exc:
        raise ValueError(f"could not read E5 base_cost from numeric pack {pack}") from exc
    if pack_base != EXPECTED_BASE_COST or record_hash({"id": 5, "base_cost": pack_base}) != EXPECTED_BASE_COST_RECORD_SHA256:
        raise ValueError(f"F-T1 calibration requires pack E5 base cost {EXPECTED_BASE_COST}")
    seeds = load_manifest(manifest)
    pins = expected_pins(args)
    runner_hash = args.expected_runner_sha256
    if len(runner_hash) != 64:
        raise ValueError("--expected-runner-sha256 must be supplied as a 64-digit SHA-256")
    provenance = verify_candidate(repo, pack, harness, sim, eras, pins, runner_hash, args.expected_eras_sha256,
                                  capture_fn=capture_fn, clean_fn=clean_fn)
    provenance.update({"manifestPath": str(manifest), "manifestSha256": manifest_hash,
                       "erasPath": str(eras), "erasSha256": eras_hash,
                       "e5BaseCost": source_base, "e5BaseCostRecordSha256": source_base_hash,
                       "packE5BaseCost": pack_base, "packE5BaseCostRecordSha256": record_hash({"id": 5, "base_cost": pack_base})})
    return seeds, provenance


def make_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--repo", required=True, type=Path)
    p.add_argument("--pack", required=True, type=Path)
    p.add_argument("--harness-dll", required=True, type=Path)
    p.add_argument("--sim-dll", required=True, type=Path)
    p.add_argument("--manifest", required=True, type=Path)
    p.add_argument("--expected-manifest-sha256", required=True)
    p.add_argument("--expected-head", required=True)
    p.add_argument("--expected-source-aggregate", required=True)
    p.add_argument("--expected-pack-sha256", required=True)
    p.add_argument("--expected-harness-sha256", required=True)
    p.add_argument("--expected-sim-sha256", required=True)
    p.add_argument("--expected-runner-sha256", required=True)
    p.add_argument("--expected-eras-sha256", required=True)
    p.add_argument("--eras", required=True, type=Path)
    p.add_argument("--output-dir", required=True, type=Path)
    p.add_argument("--dotnet", default="dotnet")
    p.add_argument("--minutes", type=int, default=MINUTES)
    return p


def run(args: argparse.Namespace, *, capture_fn: Callable[..., dict[str, Any]] = capture,
        clean_fn: Callable[[Path], bool] = git_is_clean,
        process_run: Callable[..., Any] = subprocess.run) -> int:
    repo, pack, harness, sim, manifest = (Path(p).resolve() for p in
        (args.repo, args.pack, args.harness_dll, args.sim_dll, args.manifest))
    out = Path(args.output_dir).resolve()
    seeds, provenance = validate_inputs(args, capture_fn=capture_fn, clean_fn=clean_fn)
    out.mkdir(parents=True, exist_ok=False)
    metadata: dict[str, Any] = {
        "status": "running", "startedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "repo": str(repo), "outputRoot": str(out), "seedCount": len(seeds), "seeds": seeds,
        "minutes": MINUTES, "cohortRole": "calibration_only_not_acceptance",
        "protocol": {"command": "fief-match", "naturalFief": True, "grantE5": False},
        "commandTemplate": [args.dotnet, str(harness), "fief-match", "--pack", str(pack),
                            "--seed", "<manifest-seed>", "--minutes", str(MINUTES)],
        "manifestCopy": "seed-manifest.txt", "provenanceBefore": provenance, "runs": [],
    }
    shutil.copyfile(manifest, out / "seed-manifest.txt")
    metadata_path = out / "run_metadata.json"

    def write_metadata() -> None:
        metadata_path.write_text(json.dumps(metadata, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    def set_not_run(status: str) -> None:
        for pending in seeds[len(metadata["runs"]):]:
            if pending not in [row["seed"] for row in metadata["runs"]]:
                metadata["runs"].append({"seed": pending, "status": status, "command": command_for(args.dotnet, harness, pack, pending)})

    def command_for(dotnet: str, harness_path: Path, pack_path: Path, seed: int) -> list[str]:
        return [dotnet, str(harness_path), "fief-match", "--pack", str(pack_path), "--seed", str(seed), "--minutes", str(MINUTES)]

    write_metadata()
    interrupted = False
    try:
        for seed in seeds:
            command = command_for(args.dotnet, harness, pack, seed)
            raw_path = out / f"seed-{seed}.txt"
            row: dict[str, Any] = {"seed": seed, "status": "running", "command": command,
                                   "rawFile": raw_path.name, "startedUtc": dt.datetime.now(dt.timezone.utc).isoformat()}
            metadata["runs"].append(row)
            write_metadata()
            try:
                row["provenanceBefore"] = verify_candidate(repo, pack, harness, sim, Path(args.eras).resolve(), expected_pins(args),
                                                           args.expected_runner_sha256, args.expected_eras_sha256,
                                                           capture_fn=capture_fn, clean_fn=clean_fn)
                with raw_path.open("wb") as raw:
                    try:
                        result = process_run(command, cwd=repo, stdout=raw, stderr=subprocess.STDOUT, check=False)
                        row["exitCode"] = result.returncode
                    except KeyboardInterrupt:
                        row["exitCode"] = None
                        row["status"] = "interrupted"
                        row["failure"] = "KeyboardInterrupt"
                        interrupted = True
                row["rawSha256"] = sha256(raw_path)
                row["rawBytes"] = raw_path.stat().st_size
                row["provenanceAfter"] = verify_candidate(repo, pack, harness, sim, Path(args.eras).resolve(), expected_pins(args),
                                                          args.expected_runner_sha256, args.expected_eras_sha256,
                                                          capture_fn=capture_fn, clean_fn=clean_fn)
                row["finishedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
                if interrupted:
                    metadata["status"] = "interrupted"
                    row["status"] = "interrupted"
                    set_not_run("not_run_interrupted")
                    write_metadata()
                    break
                if row.get("exitCode") != 0:
                    row["status"] = "failed"
                    row["failure"] = f"harness exit code {row.get('exitCode')}"
                    metadata["status"] = "failed"
                    set_not_run("not_run_after_failure")
                    write_metadata()
                    break
                row["status"] = "complete"
                write_metadata()
            except Exception as exc:
                row["status"] = "failed"
                row.setdefault("exitCode", None)
                row["failure"] = f"{type(exc).__name__}: {exc}"
                if raw_path.exists():
                    row["rawSha256"] = sha256(raw_path)
                    row["rawBytes"] = raw_path.stat().st_size
                if "provenanceBefore" in row:
                    try:
                        row["provenanceAfter"] = verify_candidate(
                            repo, pack, harness, sim, Path(args.eras).resolve(), expected_pins(args),
                            args.expected_runner_sha256, args.expected_eras_sha256,
                            capture_fn=capture_fn, clean_fn=clean_fn)
                    except Exception as post_exc:
                        row["provenanceAfterError"] = f"{type(post_exc).__name__}: {post_exc}"
                row["finishedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
                metadata["status"] = "failed"
                set_not_run("not_run_after_failure")
                write_metadata()
                break
        if metadata["status"] == "running":
            metadata["status"] = "complete" if all(row["status"] == "complete" for row in metadata["runs"]) else "failed"
    except KeyboardInterrupt:
        interrupted = True
        metadata["status"] = "interrupted"
        for row in metadata["runs"]:
            if row["status"] == "running":
                row["status"] = "interrupted"
                row["exitCode"] = None
                row["failure"] = "KeyboardInterrupt"
                raw_path = out / row["rawFile"]
                if raw_path.exists():
                    row["rawSha256"] = sha256(raw_path)
                    row["rawBytes"] = raw_path.stat().st_size
        set_not_run("not_run_interrupted")
    finally:
        try:
            metadata["provenanceAfter"] = verify_candidate(repo, pack, harness, sim, Path(args.eras).resolve(), expected_pins(args),
                                                           args.expected_runner_sha256, args.expected_eras_sha256,
                                                           capture_fn=capture_fn, clean_fn=clean_fn)
        except Exception as exc:
            metadata["provenanceAfterError"] = f"{type(exc).__name__}: {exc}"
            metadata["status"] = "failed"
        metadata["accounting"] = {
            "expectedRuns": len(seeds),
            "recordedRuns": len(metadata["runs"]),
            "completedRuns": sum(row["status"] == "complete" for row in metadata["runs"]),
            "interruptedRuns": sum(row["status"] == "interrupted" for row in metadata["runs"]),
        }
        metadata["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
        write_metadata()
    return 130 if metadata["status"] == "interrupted" else (0 if metadata["status"] == "complete" else 2)


def main(argv: list[str] | None = None) -> int:
    args = make_parser().parse_args(argv)
    try:
        return run(args)
    except (OSError, ValueError, subprocess.SubprocessError, json.JSONDecodeError) as exc:
        print(f"F-T1 calibration runner: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
