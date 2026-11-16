#!/usr/bin/env python3
"""Run one preregistered B5-11 20-seed performance cohort with fail-closed provenance.

The only accepted cohorts are the exact frozen natural and E5-granted manifests.
Each seed runs in a separate Harness invocation with its own command, timing,
raw-log hash, output hashes, and pre/post candidate provenance. Output roots are
single-use and are never resumed or overwritten.
"""
from __future__ import annotations

import argparse
import csv
import datetime as dt
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path
from typing import Any

from cohort_provenance import capture, sha256 as provenance_sha256, verify_expected

NATURAL_MANIFEST_SHA256 = "f787766662b46319f7f0807eacd27592aa8c2af4163f366d52ddec07a3e3a786"
GRANT_MANIFEST_SHA256 = "b5d541d0f820ff3fc3979d2ff5158c15b9bc24ac61fa7e11af4196eec1f006b6"
SEED_COUNT = 20


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def read_seed_manifest(path: Path, expected_sha256: str) -> list[int]:
    if expected_sha256.lower() not in {NATURAL_MANIFEST_SHA256, GRANT_MANIFEST_SHA256}:
        raise ValueError("manifest pin is stale or is not one of the preregistered B5-11 cohorts")
    actual = sha256(path)
    if actual != expected_sha256.lower():
        raise ValueError(f"manifest SHA-256 mismatch: expected {expected_sha256}, got {actual}")
    seeds: list[int] = []
    for line_number, raw in enumerate(path.read_text(encoding="ascii").splitlines(), 1):
        value = raw.split("#", 1)[0].strip()
        if not value:
            continue
        if not value.isascii() or not value.isdecimal():
            raise ValueError(f"{path}:{line_number}: expected one unsigned decimal seed")
        seeds.append(int(value))
    if len(seeds) != SEED_COUNT or len(set(seeds)) != SEED_COUNT:
        raise ValueError(f"B5-11 manifest must contain exactly {SEED_COUNT} unique seeds")
    return seeds


def capture_candidate(repo: Path, pack: Path, harness: Path, sim: Path) -> dict[str, Any]:
    snapshot = capture(repo, pack, harness, sim)
    proc = subprocess.run(["git", "-C", str(repo), "rev-parse", "HEAD^{tree}"],
                          check=True, capture_output=True, text=True)
    snapshot["headTree"] = proc.stdout.strip()
    status = subprocess.run(["git", "-C", str(repo), "status", "--porcelain", "--untracked-files=all"],
                            check=True, capture_output=True, text=True)
    if status.stdout.strip():
        raise ValueError("candidate checkout is dirty; freeze and commit before B5-11")
    snapshot["sourceClean"] = True
    return snapshot


def make_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[4])
    parser.add_argument("--pack", type=Path, required=True)
    parser.add_argument("--harness-dll", type=Path, required=True)
    parser.add_argument("--sim-dll", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--expected-head", required=True)
    parser.add_argument("--expected-tree", required=True)
    parser.add_argument("--expected-source-aggregate", required=True)
    parser.add_argument("--expected-pack-sha256", required=True)
    parser.add_argument("--expected-harness-sha256", required=True)
    parser.add_argument("--expected-sim-sha256", required=True)
    parser.add_argument("--expected-manifest-sha256", required=True)
    parser.add_argument("--expected-runner-sha256", required=True)
    parser.add_argument("--expected-provenance-helper-sha256", required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("command", nargs=argparse.REMAINDER,
                        help="dotnet <pinned Harness> fief-perf arguments after --")
    return parser


def validate_cohort_command(command: list[str], repo: Path, pack: Path,
                            harness: Path, manifest: Path, output: Path,
                            manifest_sha256: str) -> dict[str, Any]:
    """Validate exact B5-11 settings and return role plus output-prefix base."""
    if len(command) < 4:
        raise ValueError("wrapped command must be dotnet <pinned Harness> fief-perf ...")
    dotnet = shutil.which("dotnet")
    requested_dotnet = shutil.which(command[0]) if not Path(command[0]).is_absolute() else command[0]
    if not dotnet or not requested_dotnet or Path(requested_dotnet).resolve() != Path(dotnet).resolve():
        raise ValueError("wrapped command must use the current dotnet host")
    if Path(command[1]).resolve() != harness or command[2] != "fief-perf":
        raise ValueError("wrapped command must invoke the pinned Harness fief-perf command")

    options: dict[str, str] = {}
    grant_count = 0
    i = 3
    value_options = {"--pack", "--seeds-file", "--output-prefix", "--minutes", "--warmup-minutes"}
    while i < len(command):
        item = command[i]
        if item == "--grant-e5":
            grant_count += 1
            i += 1
            continue
        if item not in value_options or i + 1 >= len(command) or item in options:
            raise ValueError(f"wrapped command has an unknown, missing, or duplicate option: {item}")
        options[item] = command[i + 1]
        i += 2
    if set(options) != value_options:
        raise ValueError("wrapped fief-perf command must specify each required option exactly once")
    if Path(options["--pack"]).resolve() != pack:
        raise ValueError("wrapped fief-perf command does not use the pinned numeric pack")
    if Path(options["--seeds-file"]).resolve() != manifest:
        raise ValueError("wrapped fief-perf command does not use the pinned seed manifest")
    if options["--minutes"] != "130" or options["--warmup-minutes"] != "5":
        raise ValueError("B5-11 command must use exactly --minutes 130 --warmup-minutes 5")

    if manifest_sha256.lower() == NATURAL_MANIFEST_SHA256:
        role, grant_expected = "natural", False
    elif manifest_sha256.lower() == GRANT_MANIFEST_SHA256:
        role, grant_expected = "grant-e5", True
    else:
        raise ValueError("manifest pin does not identify a preregistered B5-11 cohort")
    if grant_count != int(grant_expected):
        raise ValueError(f"{role} cohort requires --grant-e5={str(grant_expected).lower()}")

    prefix = Path(options["--output-prefix"])
    if not prefix.is_absolute():
        prefix = repo / prefix
    prefix = prefix.resolve()
    if output not in prefix.parents:
        raise ValueError("wrapped fief-perf output prefix must be inside the fresh output directory")
    return {"role": role, "grantE5": grant_expected, "outputPrefix": prefix}


def _write_metadata(path: Path, metadata: dict[str, Any]) -> None:
    tmp = path.with_suffix(".tmp")
    tmp.write_text(json.dumps(metadata, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    tmp.replace(path)


def _hash_files(root: Path) -> dict[str, str]:
    return {str(path.relative_to(root)): sha256(path)
            for path in sorted(root.rglob("*")) if path.is_file()}


def _fresh_external_output(raw_output: Path, repo: Path) -> Path:
    """Require a genuinely absent output path outside the pinned source worktree."""
    raw = Path(os.path.abspath(raw_output))
    if os.path.lexists(raw):
        raise FileExistsError(f"refusing to overwrite cohort output path: {raw}")
    output = raw.resolve()
    root = repo.resolve()
    if output == root or root in output.parents or output in root.parents:
        raise ValueError("cohort output directory must be outside the repository worktree")
    return output


def _verify_snapshot(snapshot: dict[str, Any], args: argparse.Namespace) -> None:
    verify_expected(snapshot, {
        "headCommit": args.expected_head,
        "headTree": args.expected_tree,
        "sourceAggregateSha256": args.expected_source_aggregate,
        "packSha256": args.expected_pack_sha256,
        "harnessDllSha256": args.expected_harness_sha256,
        "simDllSha256": args.expected_sim_sha256,
    })


def _metrics_for_seed(tick_csv: Path, expected_seed: int) -> dict[str, Any] | None:
    if not tick_csv.is_file():
        raise FileNotFoundError(f"per-seed timing CSV is missing: {tick_csv}")
    with tick_csv.open(newline="", encoding="utf-8-sig") as stream:
        rows = list(csv.DictReader(stream))
    if len(rows) != 1:
        raise ValueError(f"expected exactly one timing row for seed {expected_seed}; found {len(rows)}")
    if rows[0].get("seed") != str(expected_seed):
        raise ValueError(f"per-seed performance CSV seed mismatch: expected {expected_seed}, got {rows[0].get('seed')}")
    for field in ("timed_ticks", "stop_reason", "censored", "final_tick", "mean_ms", "p95_ms", "gc_pause_ms_per_tick"):
        if field not in rows[0] or rows[0][field] == "":
            raise ValueError(f"per-seed performance CSV is missing required timing field {field}")
    return rows[0]


def run(args: argparse.Namespace) -> int:
    repo = args.repo.resolve()
    pack, harness, sim = args.pack.resolve(), args.harness_dll.resolve(), args.sim_dll.resolve()
    manifest = args.manifest.resolve()
    out = _fresh_external_output(args.output_dir, repo)
    for path, label in ((pack, "numeric pack"), (harness, "Harness DLL"),
                        (sim, "adjacent Sim DLL"), (manifest, "seed manifest")):
        if not path.is_file():
            raise FileNotFoundError(f"{label} not found: {path}")

    expected_hashes = [args.expected_source_aggregate, args.expected_pack_sha256,
                       args.expected_harness_sha256, args.expected_sim_sha256,
                       args.expected_manifest_sha256, args.expected_runner_sha256,
                       args.expected_provenance_helper_sha256]
    if any(len(value) != 64 or any(c not in "0123456789abcdefABCDEF" for c in value)
           for value in expected_hashes):
        raise ValueError("all aggregate, artifact, manifest, and tool pins must be SHA-256 values")
    for value, label in ((args.expected_head, "HEAD"), (args.expected_tree, "tree")):
        if len(value) != 40 or any(c not in "0123456789abcdefABCDEF" for c in value):
            raise ValueError(f"expected {label} must be a 40-character Git hash")

    manifest_hash = sha256(manifest)
    seeds = read_seed_manifest(manifest, args.expected_manifest_sha256)
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    if not command:
        raise ValueError("a command is required after --")
    command_spec = validate_cohort_command(command, repo, pack, harness, manifest, out, manifest_hash)

    runner_path = Path(__file__).resolve()
    helper_path = Path(__file__).with_name("cohort_provenance.py").resolve()
    actual_runner_hash, actual_helper_hash = sha256(runner_path), sha256(helper_path)
    if actual_runner_hash != args.expected_runner_sha256.lower():
        raise ValueError(f"runner SHA-256 mismatch: expected {args.expected_runner_sha256}, got {actual_runner_hash}")
    if actual_helper_hash != args.expected_provenance_helper_sha256.lower():
        raise ValueError(f"provenance helper SHA-256 mismatch: expected {args.expected_provenance_helper_sha256}, got {actual_helper_hash}")

    before = capture_candidate(repo, pack, harness, sim)
    _verify_snapshot(before, args)
    out.mkdir(parents=True, exist_ok=False)
    metadata_path = out / "wrapper_metadata.json"
    metadata: dict[str, Any] = {
        "schema": "civsim.b5-11-performance-wrapper/v2", "status": "running",
        "startedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "repo": str(repo), "packPath": str(pack), "harnessDllPath": str(harness),
        "simDllPath": str(sim), "manifestPath": str(manifest), "manifestSha256": manifest_hash,
        "manifestSeedCount": len(seeds), "cohort": command_spec["role"],
        "grantE5": command_spec["grantE5"], "provenanceBefore": before,
        "runner": {"path": str(runner_path), "sha256": actual_runner_hash},
        "provenanceHelper": {"path": str(helper_path), "sha256": actual_helper_hash},
        "requestedCommand": command, "workingDirectory": str(repo), "seeds": [],
    }
    _write_metadata(metadata_path, metadata)

    base_prefix: Path = command_spec["outputPrefix"]
    overall_exit = 0
    for index, seed in enumerate(seeds):
        seed_root = out / f"seed-{seed}"
        seed_root.mkdir()
        seed_manifest = out / "manifests" / f"seed-{seed}.txt"
        seed_manifest.parent.mkdir(exist_ok=True)
        seed_manifest.write_text(f"{seed}\n", encoding="ascii")
        actual_seed_command = list(command)
        for option, value in (("--seeds-file", str(seed_manifest)),
                              ("--output-prefix", str(seed_root / base_prefix.name))):
            position = actual_seed_command.index(option)
            actual_seed_command[position + 1] = value

        record: dict[str, Any] = {
            "seed": seed, "status": "running", "seedManifest": str(seed_manifest),
            "seedManifestSha256": sha256(seed_manifest), "command": actual_seed_command,
            "startedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        }
        try:
            pre = capture_candidate(repo, pack, harness, sim)
            _verify_snapshot(pre, args)
            record["provenanceBefore"] = pre
            log_path = seed_root / "command.log"
            started = time.monotonic()
            with log_path.open("wb") as log:
                completed = subprocess.run(actual_seed_command, cwd=repo, stdout=log,
                                           stderr=subprocess.STDOUT, check=False)
            record["elapsedSeconds"] = time.monotonic() - started
            record["exitCode"] = completed.returncode
            record["rawLogSha256"] = sha256(log_path)
            post = capture_candidate(repo, pack, harness, sim)
            record["provenanceAfter"] = post
            _verify_snapshot(post, args)
            record["provenanceStable"] = (post == pre == before)
            expected_outputs = [seed_root / f"{base_prefix.name}{suffix}" for suffix in
                                ("-ticks.csv", "-seats.csv", "-tick-samples.csv", "-provenance.txt")]
            missing_outputs = [str(path) for path in expected_outputs if not path.is_file()]
            if missing_outputs:
                raise FileNotFoundError(f"Harness omitted required per-seed outputs: {missing_outputs}")
            record["artifactsSha256"] = _hash_files(seed_root)
            tick_csv = seed_root / f"{base_prefix.name}-ticks.csv"
            record["performance"] = _metrics_for_seed(tick_csv, seed)
            record["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
            record["status"] = "complete" if completed.returncode == 0 and record["provenanceStable"] else "failed"
            if completed.returncode != 0 or not record["provenanceStable"]:
                overall_exit = completed.returncode or 2
                metadata["seeds"].append(record)
                for remaining in seeds[index + 1:]:
                    metadata["seeds"].append({"seed": remaining, "status": "not-started",
                                              "reason": "prior seed failed or provenance changed"})
                break
        except Exception as exc:
            record.update({"status": "failed", "failure": f"{type(exc).__name__}: {exc}",
                           "completedUtc": dt.datetime.now(dt.timezone.utc).isoformat()})
            metadata["seeds"].append(record)
            for remaining in seeds[index + 1:]:
                metadata["seeds"].append({"seed": remaining, "status": "not-started",
                                          "reason": "prior seed failed or provenance changed"})
            overall_exit = 2
            break
        metadata["seeds"].append(record)
        _write_metadata(metadata_path, metadata)

    try:
        after = capture_candidate(repo, pack, harness, sim)
        _verify_snapshot(after, args)
        stable = after == before
        metadata["provenanceAfter"] = after
        metadata["provenanceStable"] = stable
    except Exception as exc:
        stable = False
        overall_exit = overall_exit or 2
        metadata["provenanceAfterFailure"] = f"{type(exc).__name__}: {exc}"
    metadata.update({"completedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
                     "status": "complete" if overall_exit == 0 and stable and
                              len(metadata["seeds"]) == SEED_COUNT and
                              all(item.get("status") == "complete" for item in metadata["seeds"]) else "failed",
                     "exitCode": overall_exit})
    _write_metadata(metadata_path, metadata)
    return overall_exit


def main() -> int:
    args = make_parser().parse_args()
    try:
        return run(args)
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
