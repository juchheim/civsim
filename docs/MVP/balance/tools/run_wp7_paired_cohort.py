#!/usr/bin/env python3
"""Run one frozen WP-E5-7 natural Fief cohort with paired GOV-11 traces."""
from __future__ import annotations

import argparse
import concurrent.futures
import datetime as dt
import hashlib
import json
import os
import shutil
import subprocess
import sys
import threading
import time
from pathlib import Path
from typing import Any


SCHEMA = "civsim.wp-e5-7-paired-cohort/v1"
COHORT_SIZES = {"calibration": 20, "holdout": 80}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def git(repo: Path, *args: str) -> str:
    return subprocess.check_output(["git", "-C", str(repo), *args], text=True).strip()


def atomic_json(path: Path, payload: dict[str, Any]) -> None:
    temp = path.with_suffix(".tmp")
    temp.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    temp.replace(path)


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--repo", type=Path, required=True)
    p.add_argument("--pack", type=Path, required=True)
    p.add_argument("--harness-dll", type=Path, required=True)
    p.add_argument("--sim-dll", type=Path, required=True)
    p.add_argument("--seeds-file", type=Path, required=True)
    p.add_argument("--role", choices=tuple(COHORT_SIZES), required=True)
    p.add_argument("--minutes", type=int, default=130)
    p.add_argument("--workers", type=int, default=4)
    p.add_argument("--output-dir", type=Path, required=True)
    p.add_argument("--expected-head", required=True)
    p.add_argument("--expected-tree", required=True)
    p.add_argument("--expected-source-aggregate", required=True)
    p.add_argument("--expected-pack-sha256", required=True)
    p.add_argument("--expected-harness-sha256", required=True)
    p.add_argument("--expected-sim-sha256", required=True)
    p.add_argument("--expected-seeds-sha256", required=True)
    p.add_argument("--expected-runner-sha256", required=True)
    p.add_argument("--expected-provenance-helper-sha256", required=True)
    return p


def read_seeds(path: Path, role: str) -> list[int]:
    values = []
    for line_no, raw in enumerate(path.read_text(encoding="ascii").splitlines(), 1):
        value = raw.split("#", 1)[0].strip()
        if not value:
            continue
        if not value.isascii() or not value.isdecimal():
            raise ValueError(f"{path}:{line_no}: expected one unsigned decimal seed")
        values.append(int(value))
    if len(values) != COHORT_SIZES[role] or len(set(values)) != len(values):
        raise ValueError(f"{role} cohort needs {COHORT_SIZES[role]} unique seeds")
    return values


def trace_counts(path: Path) -> dict[str, int]:
    counts: dict[str, int] = {}
    with path.open("r", encoding="utf-8", errors="replace") as stream:
        for line in stream:
            token = line.split(" ", 1)[0]
            if token.startswith("wp7-"):
                counts[token] = counts.get(token, 0) + 1
    return counts


def main() -> int:
    args = parser().parse_args()
    repo = args.repo.resolve()
    pack = args.pack.resolve()
    harness = args.harness_dll.resolve()
    sim = args.sim_dll.resolve()
    seeds_file = args.seeds_file.resolve()
    output = Path(os.path.abspath(args.output_dir))
    helper = repo / "docs/MVP/balance/tools/cohort_provenance.py"
    runner = Path(__file__).resolve()
    if args.minutes != 130 or args.workers < 1:
        raise ValueError("WP7 cohorts require 130 minutes and at least one worker")
    if os.path.lexists(output):
        raise FileExistsError(f"refusing to overwrite output directory: {output}")
    if output == repo or repo in output.parents or output in repo.parents:
        raise ValueError("output directory must be outside the candidate checkout")
    if not all(p.is_file() for p in (pack, harness, sim, seeds_file, helper, runner)):
        raise FileNotFoundError("a frozen cohort input is missing")
    if sha256(seeds_file) != args.expected_seeds_sha256:
        raise ValueError("seed manifest SHA-256 mismatch")
    if sha256(runner) != args.expected_runner_sha256:
        raise ValueError("runner SHA-256 mismatch")
    if sha256(helper) != args.expected_provenance_helper_sha256:
        raise ValueError("provenance helper SHA-256 mismatch")
    seeds = read_seeds(seeds_file, args.role)

    sys.path.insert(0, str(helper.parent))
    import cohort_provenance  # type: ignore

    before = cohort_provenance.capture(repo, pack, harness, sim)
    expected = {
        "headCommit": args.expected_head,
        "sourceAggregateSha256": args.expected_source_aggregate,
        "packSha256": args.expected_pack_sha256,
        "harnessDllSha256": args.expected_harness_sha256,
        "simDllSha256": args.expected_sim_sha256,
    }
    cohort_provenance.verify_expected(before, expected)
    tree = git(repo, "rev-parse", "HEAD^{tree}")
    if tree != args.expected_tree:
        raise ValueError(f"tree mismatch: expected {args.expected_tree}, got {tree}")
    if git(repo, "status", "--porcelain", "--untracked-files=all"):
        raise ValueError("candidate checkout is dirty")
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise FileNotFoundError("dotnet host not found")

    output.mkdir(parents=True, exist_ok=False)
    (output / "logs").mkdir()
    (output / "replays").mkdir()
    metadata: dict[str, Any] = {
        "schema": SCHEMA,
        "status": "running",
        "role": args.role,
        "minutes": args.minutes,
        "workers": args.workers,
        "startedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "sourceBefore": before,
        "headTree": tree,
        "packPath": str(pack),
        "harnessDllPath": str(harness),
        "simDllPath": str(sim),
        "seedsFile": str(seeds_file),
        "seedsSha256": args.expected_seeds_sha256,
        "runnerPath": str(runner),
        "runnerSha256": args.expected_runner_sha256,
        "provenanceHelperSha256": args.expected_provenance_helper_sha256,
        "dotnetPath": dotnet,
        "dotnetInfo": subprocess.run([dotnet, "--info"], cwd=repo, capture_output=True, text=True).stdout,
        "protocol": "one natural Fief whole match per seed; --fief-wp7-paired; observer-only trace",
        "matches": [{"seed": seed, "status": "not_started"} for seed in seeds],
    }
    metadata_path = output / "run_metadata.json"
    atomic_json(metadata_path, metadata)
    lock = threading.Lock()

    def run_seed(seed: int) -> dict[str, Any]:
        log_path = output / "logs" / f"{seed}.log"
        replay_dir = output / "replays" / str(seed)
        replay_path = replay_dir / f"{seed}.replay"
        replay_dir.mkdir(parents=True, exist_ok=False)
        command = [dotnet, str(harness), "fief-match", "--pack", str(pack), "--seed", str(seed),
                   "--minutes", str(args.minutes), "--fief-wp7-paired", "--record-dir", str(replay_dir)]
        started = dt.datetime.now(dt.timezone.utc).isoformat()
        start_clock = time.monotonic()
        with log_path.open("wb") as log:
            match = subprocess.run(command, cwd=repo, stdout=log, stderr=subprocess.STDOUT)
        match_seconds = time.monotonic() - start_clock
        replay_verify = None
        if match.returncode == 0 and replay_path.is_file():
            verify_command = [dotnet, str(harness), "replay", "verify", str(replay_path), "--pack", str(pack)]
            with log_path.open("ab") as log:
                log.write(b"\n=== REPLAY VERIFY ===\n")
                verify = subprocess.run(verify_command, cwd=repo, stdout=log, stderr=subprocess.STDOUT)
            replay_verify = verify.returncode
        counts = trace_counts(log_path)
        return {
            "seed": seed,
            "status": "complete" if match.returncode == 0 and replay_verify == 0 else "failed",
            "startedUtc": started,
            "elapsedSeconds": round(match_seconds, 3),
            "exitCode": match.returncode,
            "replayVerifyExitCode": replay_verify,
            "rawLogBytes": log_path.stat().st_size,
            "rawLogSha256": sha256(log_path),
            "replayPath": str(replay_path) if replay_path.exists() else None,
            "replaySha256": sha256(replay_path) if replay_path.is_file() else None,
            "traceRows": counts,
            "pairedGov11Rows": sum(v for k, v in counts.items() if k.startswith("wp7-gov11")),
        }

    by_seed = {seed: record for seed, record in zip(seeds, metadata["matches"])}
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        futures = {pool.submit(run_seed, seed): seed for seed in seeds}
        for future in concurrent.futures.as_completed(futures):
            seed = futures[future]
            try:
                result = future.result()
            except Exception as exc:  # preserve the failure while allowing other seeds to finish
                result = {"seed": seed, "status": "failed", "error": repr(exc)}
            with lock:
                by_seed[seed].clear()
                by_seed[seed].update(result)
                atomic_json(metadata_path, metadata)

    after = cohort_provenance.capture(repo, pack, harness, sim)
    stable = before == after and git(repo, "rev-parse", "HEAD^{tree}") == tree and not git(
        repo, "status", "--porcelain", "--untracked-files=all")
    metadata["sourceAfter"] = after
    metadata["sourceStable"] = stable
    metadata["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
    matches_ok = all(record.get("status") == "complete" for record in metadata["matches"])
    metadata["status"] = "complete" if stable and matches_ok else "failed"
    atomic_json(metadata_path, metadata)
    return 0 if metadata["status"] == "complete" else 1


if __name__ == "__main__":
    raise SystemExit(main())
