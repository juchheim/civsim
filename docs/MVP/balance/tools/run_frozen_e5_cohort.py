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
import math
import os
import re
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


TICK_FIELDS = ("seed", "minutes", "warmup_minutes", "timed_ticks", "expected_timed_ticks",
               "stop_reason", "censored", "final_tick", "final_checksum", "mean_ms",
               "p50_ms", "p95_ms", "p99_ms", "max_ms", "gc_pause_ms",
               "gc_pause_ms_per_tick", "gen0", "gen1", "gen2", "allocated_bytes",
               "allocated_bytes_per_tick")
SAMPLE_FIELDS = ("seed", "sim_tick", "elapsed_ms")
SEAT_FIELDS = ("seed", "civ", "archetype", "season", "tick", "key5_completed_tick",
               "population", "view_figure_count", "view_budget", "over_budget")


def _csv_rows(path: Path, fields: tuple[str, ...]) -> list[dict[str, str]]:
    with path.open(newline="", encoding="utf-8-sig") as stream:
        reader = csv.DictReader(stream)
        if tuple(reader.fieldnames or ()) != fields:
            raise ValueError(f"unexpected CSV header in {path}: {reader.fieldnames}")
        return list(reader)


def _metrics_for_seed(tick_csv: Path, expected_seed: int) -> dict[str, Any]:
    if not tick_csv.is_file():
        raise FileNotFoundError(f"per-seed timing CSV is missing: {tick_csv}")
    rows = _csv_rows(tick_csv, TICK_FIELDS)
    if len(rows) != 1:
        raise ValueError(f"expected exactly one timing row for seed {expected_seed}; found {len(rows)}")
    if rows[0].get("seed") != str(expected_seed):
        raise ValueError(f"per-seed performance CSV seed mismatch: expected {expected_seed}, got {rows[0].get('seed')}")
    row = rows[0]
    try:
        ints = {key: int(row[key]) for key in ("minutes", "warmup_minutes", "timed_ticks", "expected_timed_ticks", "censored", "final_tick", "gen0", "gen1", "gen2", "allocated_bytes")}
        numbers = {key: float(row[key]) for key in ("mean_ms", "p50_ms", "p95_ms", "p99_ms", "max_ms", "gc_pause_ms", "gc_pause_ms_per_tick", "allocated_bytes_per_tick")}
    except (ValueError, TypeError) as exc:
        raise ValueError(f"per-seed performance CSV contains a non-numeric metric: {exc}") from exc
    if ints["minutes"] != 130 or ints["warmup_minutes"] != 5 or ints["expected_timed_ticks"] != 75000:
        raise ValueError("performance row does not record the fixed 130-minute/5-minute B5-11 horizon")
    if any(value < 0 for value in ints.values()) or ints["censored"] not in (0, 1):
        raise ValueError("performance row has negative counts or invalid censor flag")
    if any(not math.isfinite(value) or value < 0 for value in numbers.values()):
        raise ValueError("performance row contains a negative or non-finite metric")
    if not re.fullmatch(r"[0-9a-fA-F]{16}", row["final_checksum"]):
        raise ValueError("performance row final checksum must be 16 hexadecimal characters")
    if ints["timed_ticks"] > 75000:
        raise ValueError("performance row exceeds the requested measured horizon")
    if ints["censored"] == 0:
        if (row["stop_reason"] != "horizon" or ints["timed_ticks"] != 75000 or ints["final_tick"] != 78000):
            raise ValueError("uncensored performance row does not reach the requested horizon")
    elif row["stop_reason"] != "terminal-before-horizon" or ints["timed_ticks"] >= 75000 or ints["final_tick"] >= 78000:
        raise ValueError("censored performance row is inconsistent with early terminal exposure")
    return row


def _validate_seed_artifacts(seed_root: Path, prefix_name: str, seed: int,
                             role: str, seed_manifest: Path, pack: Path,
                             harness: Path, expected_harness_sha256: str,
                             performance: dict[str, Any]) -> None:
    samples_path = seed_root / f"{prefix_name}-tick-samples.csv"
    sample_rows = _csv_rows(samples_path, SAMPLE_FIELDS)
    timed = int(performance["timed_ticks"])
    if len(sample_rows) != timed:
        raise ValueError(f"tick sample count {len(sample_rows)} differs from measured ticks {timed}")
    first_tick = 3001
    for offset, row in enumerate(sample_rows):
        try:
            row_seed, tick, elapsed = int(row["seed"]), int(row["sim_tick"]), float(row["elapsed_ms"])
        except (ValueError, TypeError) as exc:
            raise ValueError(f"invalid tick sample: {exc}") from exc
        if row_seed != seed or tick != first_tick + offset or not math.isfinite(elapsed) or elapsed < 0:
            raise ValueError("tick samples are not contiguous, finite, nonnegative rows for this seed")
    seats = _csv_rows(seed_root / f"{prefix_name}-seats.csv", SEAT_FIELDS)
    for row in seats:
        try:
            row_seed, civ = int(row["seed"]), int(row["civ"])
            season, tick = int(row["season"]), int(row["tick"])
            key5, population, figures, budget, over = (int(row[k]) for k in
                ("key5_completed_tick", "population", "view_figure_count", "view_budget", "over_budget"))
        except (ValueError, TypeError) as exc:
            raise ValueError(f"invalid seat sample: {exc}") from exc
        if (row_seed != seed or not 0 <= civ <= 3 or min(season, tick, key5, population, figures) < 0
                or budget != 600 or over not in (0, 1) or over != int(figures > budget)):
            raise ValueError("seat sample has invalid seed, counts, view budget, or over-budget flag")
    provenance_path = seed_root / f"{prefix_name}-provenance.txt"
    provenance = {}
    for line in provenance_path.read_text(encoding="utf-8").splitlines():
        if "=" not in line:
            raise ValueError("malformed Harness provenance line")
        key, value = line.split("=", 1)
        if key in provenance:
            raise ValueError(f"duplicate Harness provenance key: {key}")
        provenance[key] = value
    expected_cohort = "e5-granted" if role == "grant-e5" else role
    expected = {"cohort": expected_cohort,
                "minutes": "130", "warmup_minutes": "5",
                "seeds_sha256": sha256(seed_manifest),
                "pack_sha256": sha256(pack),
                "executable_sha256": expected_harness_sha256.lower()}
    if any(provenance.get(key) != value for key, value in expected.items()):
        raise ValueError("Harness provenance does not match the pinned seed, pack, binary, or cohort")
    expected_paths = {"seeds_file": seed_manifest, "pack_file": pack, "executable": harness}
    for key, expected_path in expected_paths.items():
        try:
            actual_path = Path(provenance.get(key, "")).resolve()
        except (OSError, RuntimeError):
            actual_path = None
        if actual_path != expected_path.resolve():
            raise ValueError(f"Harness provenance {key} does not resolve to the pinned input")
    expected_grant = role == "grant-e5"
    if provenance.get("grant_e5", "").strip().lower() != str(expected_grant).lower():
        raise ValueError("Harness provenance E5 grant flag does not match the cohort")
    for key in ("runtime", "os", "architecture", "process_id"):
        if not provenance.get(key):
            raise ValueError(f"Harness provenance is missing {key}")
    if not provenance["process_id"].isdecimal():
        raise ValueError("Harness provenance process_id is not numeric")


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
            _validate_seed_artifacts(seed_root, base_prefix.name, seed, command_spec["role"],
                                     seed_manifest, pack, harness, args.expected_harness_sha256,
                                     record["performance"])
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
