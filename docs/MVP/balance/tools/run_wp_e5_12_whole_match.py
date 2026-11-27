#!/usr/bin/env python3
"""Fail-closed WP-E5-12 80-match natural Fief holdout runner.

One Harness process per manifest seed. This runner never resumes or overwrites
an output root, never grants E5, and preserves every attempt including failures.
Report-only E5 observers collect several whole-match balance traces in the same run.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path
from typing import Any

from cohort_provenance import capture, verify_expected

MANIFEST_SHA256 = "c69840b655fa6127ccf1565cabaecb774db7e4b0834913cc353152d814670af9"
SEED_COUNT = 80
MINUTES = 130
SEAT_ARCHETYPES = ("Builder", "Grower", "Warlord", "Sage")
E5_OBSERVER_TRACE_FLAGS = (
    "--fief-power-ledger",
    "--fief-health-trace",
    "--fief-arms-lifecycle-trace",
    "--fief-institution-trace",
    "--fief-siege-trace",
    "--fief-siege-strategist-trace",
    "--fief-wp7-trace",
    "--fief-territory-trace",
    "--fief-sea10-trace",
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_manifest(path: Path, expected_sha256: str) -> list[int]:
    if expected_sha256.lower() != MANIFEST_SHA256:
        raise ValueError(f"WP-E5-12 manifest pin must be {MANIFEST_SHA256}")
    actual = sha256(path)
    if actual != expected_sha256.lower():
        raise ValueError(f"manifest SHA-256 mismatch: expected {expected_sha256}, got {actual}")
    rows = path.read_text(encoding="ascii").splitlines()
    if any(not row.isdecimal() for row in rows):
        raise ValueError("WP-E5-12 manifest requires one unsigned decimal seed per line")
    seeds = [int(row) for row in rows]
    if len(seeds) != SEED_COUNT or len(set(seeds)) != SEED_COUNT or seeds != sorted(seeds):
        raise ValueError("WP-E5-12 manifest must contain exactly 80 unique ascending seeds")
    return seeds


def capture_candidate(repo: Path, pack: Path, harness: Path, sim: Path) -> dict[str, Any]:
    status = subprocess.run(["git", "-C", str(repo), "status", "--porcelain", "--untracked-files=all"],
                           check=True, capture_output=True, text=True)
    if status.stdout.strip():
        raise ValueError("candidate checkout is dirty; WP-E5-12 requires a clean final freeze")
    snapshot = capture(repo, pack, harness, sim)
    tree = subprocess.run(["git", "-C", str(repo), "rev-parse", "HEAD^{tree}"],
                          check=True, capture_output=True, text=True)
    snapshot["headTree"] = tree.stdout.strip()
    snapshot["sourceClean"] = True
    return snapshot


def make_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--pack", type=Path, required=True)
    parser.add_argument("--harness-dll", type=Path, required=True)
    parser.add_argument("--sim-dll", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--expected-manifest-sha256", required=True)
    parser.add_argument("--expected-head", required=True)
    parser.add_argument("--expected-tree", required=True)
    parser.add_argument("--expected-source-aggregate-sha256", required=True)
    parser.add_argument("--expected-pack-sha256", required=True)
    parser.add_argument("--expected-harness-sha256", required=True)
    parser.add_argument("--expected-sim-sha256", required=True)
    parser.add_argument("--expected-runner-sha256", required=True)
    parser.add_argument("--expected-provenance-helper-sha256", required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--dotnet", default="dotnet")
    return parser


def _verify_snapshot(snapshot: dict[str, Any], args: argparse.Namespace) -> None:
    verify_expected(snapshot, {
        "headCommit": args.expected_head,
        "headTree": args.expected_tree,
        "sourceAggregateSha256": args.expected_source_aggregate_sha256,
        "packSha256": args.expected_pack_sha256,
        "harnessDllSha256": args.expected_harness_sha256,
        "simDllSha256": args.expected_sim_sha256,
    })


def parse_match_log(text: str, seed: int) -> dict[str, Any]:
    header_re = re.compile(rf"^== fief-match seed {seed}: ([0-9.]+) minutes simulated, chapter end (.*?) at season (.*?) \(ended: (True|False)\)$", re.M)
    headers = list(header_re.finditer(text))
    if len(headers) != 1:
        raise ValueError(f"expected exactly one match summary for seed {seed}; found {len(headers)}")
    header = headers[0]
    key5_by_civ: dict[int, float | None] = {}
    table = re.compile(r"^c([0-3])\s+(Builder|Grower|Warlord|Sage)\s+(\S+)\s+(\S+)\s+", re.M)
    for match in table.finditer(text):
        civ, arch, _key4, key5 = match.groups()
        if arch != SEAT_ARCHETYPES[int(civ)]:
            raise ValueError(f"seat archetype mismatch at civ {civ}: {arch}")
        if int(civ) in key5_by_civ:
            raise ValueError(f"duplicate seat summary for civ {civ}")
        key5_by_civ[int(civ)] = None if key5 == "-" else float(key5)
    if set(key5_by_civ) != set(range(4)):
        raise ValueError(f"expected four seat timing rows; found {sorted(key5_by_civ)}")

    seats: dict[str, Any] = {}
    progress_re = re.compile(r"^\s+c([0-3]) (Builder|Grower|Warlord|Sage): .*?KEY-5→KEY-6 (.*)$", re.M)
    progress_rows = list(progress_re.finditer(text))
    if len(progress_rows) != 4:
        raise ValueError(f"expected four E5 progress rows; found {len(progress_rows)}")
    for row in progress_rows:
        civ, arch, status_text = row.groups()
        civ = int(civ)
        if arch != SEAT_ARCHETYPES[civ] or str(civ) in seats:
            raise ValueError(f"duplicate or invalid E5 progress row for civ {civ}")
        key5 = key5_by_civ[civ]
        complete = re.fullmatch(r"([0-9.]+) min from KEY-5 \(at ([0-9.]+)\)", status_text)
        censored = re.fullmatch(r"not complete; observed ([0-9.]+) min after KEY-5 \(right-censored\)", status_text)
        if complete:
            seats[str(civ)] = {"archetype": arch, "status": "key6-complete",
                               "key5Minute": key5, "key6Minute": float(complete.group(2)),
                               "key5ToKey6Minutes": float(complete.group(1)), "censor": None}
        elif censored:
            if key5 is None:
                raise ValueError(f"civ {civ} reports a KEY-5 censor but no KEY-5 completion time")
            seats[str(civ)] = {"archetype": arch, "status": "key6-right-censored",
                               "key5Minute": key5, "key6Minute": None,
                               "key5ToKey6Minutes": None,
                               "observedAfterKey5Minutes": float(censored.group(1)),
                               "censor": "horizon-after-key5"}
        elif status_text == "KEY-5 not reached":
            if key5 is not None:
                raise ValueError(f"civ {civ} KEY-5 output disagrees with its seat timing row")
            seats[str(civ)] = {"archetype": arch, "status": "key5-not-reached",
                               "key5Minute": None, "key6Minute": None,
                               "key5ToKey6Minutes": None, "censor": "key5-not-reached"}
        else:
            raise ValueError(f"unrecognized KEY-5/KEY-6 result for civ {civ}: {status_text}")
    return {"simulatedMinutes": float(header.group(1)), "endReason": header.group(2),
            "endSeason": header.group(3), "ended": header.group(4) == "True", "seats": seats}


def run(args: argparse.Namespace, *, process_run=subprocess.run) -> dict[str, Any]:
    repo, pack, harness, sim, manifest, output = (p.resolve() for p in
        (args.repo, args.pack, args.harness_dll, args.sim_dll, args.manifest, args.output_root))
    if not repo.is_dir() or not all(p.is_file() for p in (pack, harness, sim, manifest)):
        raise FileNotFoundError("repo, pack, Release Harness, adjacent Sim, and manifest must exist")
    if harness.name != "CivSim.Harness.dll" or "Release" not in harness.parts:
        raise ValueError("Harness must be the Release CivSim.Harness.dll")
    if sim.name != "CivSim.Sim.dll" or sim.parent != harness.parent:
        raise ValueError("Sim must be adjacent to the Release Harness DLL")
    if output.exists() or output.is_symlink():
        raise FileExistsError(f"refusing existing output root: {output}")
    if output == repo or repo in output.parents:
        raise ValueError("WP-E5-12 output root must be external to the repository")
    if args.expected_head.lower() != args.expected_head or len(args.expected_head) != 40:
        raise ValueError("expected HEAD must be a lowercase 40-character commit SHA")
    if len(args.expected_tree) != 40 or any(c not in "0123456789abcdef" for c in args.expected_tree):
        raise ValueError("expected tree must be a lowercase 40-character Git SHA")
    for value in (args.expected_source_aggregate_sha256, args.expected_pack_sha256,
                  args.expected_harness_sha256, args.expected_sim_sha256, args.expected_manifest_sha256,
                  args.expected_runner_sha256, args.expected_provenance_helper_sha256):
        if len(value) != 64 or any(c not in "0123456789abcdef" for c in value):
            raise ValueError("expected tree/source/pack/binary/manifest/tool pins must be lowercase SHA-256 values")
    seeds = read_manifest(manifest, args.expected_manifest_sha256)
    runner_path = Path(__file__).resolve()
    helper_path = runner_path.with_name("cohort_provenance.py")
    runner_hash, helper_hash = sha256(runner_path), sha256(helper_path)
    def verify_tool_pins() -> dict[str, str]:
        actual_runner, actual_helper = sha256(runner_path), sha256(helper_path)
        if actual_runner != args.expected_runner_sha256:
            raise ValueError(f"runner SHA-256 mismatch: expected {args.expected_runner_sha256}, got {actual_runner}")
        if actual_helper != args.expected_provenance_helper_sha256:
            raise ValueError(f"provenance helper SHA-256 mismatch: expected {args.expected_provenance_helper_sha256}, got {actual_helper}")
        return {"runnerSha256": actual_runner, "helperSha256": actual_helper}
    verify_tool_pins()
    before = capture_candidate(repo, pack, harness, sim)
    _verify_snapshot(before, args)

    output.mkdir(parents=True, exist_ok=False)
    for dirname in ("logs", "replays", "replay-index", "manifests"):
        (output / dirname).mkdir()
    records = [{"seed": seed, "status": "not-started"} for seed in seeds]
    metadata: dict[str, Any] = {
        "schema": "civsim.wp-e5-12-whole-match/v1", "status": "running",
        "startedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "repo": str(repo), "pack": str(pack), "harness": str(harness), "sim": str(sim),
        "manifest": str(manifest), "manifestSha256": args.expected_manifest_sha256,
        "seedCount": len(seeds), "minutes": MINUTES, "grantE5": False,
        "observerTraceFlags": list(E5_OBSERVER_TRACE_FLAGS),
        "provenanceBefore": before, "runner": {"path": str(runner_path), "sha256": runner_hash},
        "provenanceHelper": {"path": str(helper_path), "sha256": helper_hash}, "matches": records,
    }
    metadata_path = output / "run_metadata.json"

    def save() -> None:
        tmp = metadata_path.with_suffix(".tmp")
        tmp.write_text(json.dumps(metadata, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        tmp.replace(metadata_path)

    save()
    for i, record in enumerate(records):
        seed = record["seed"]
        seed_manifest = output / "manifests" / f"{seed}.txt"
        seed_manifest.write_text(f"{seed}\n", encoding="ascii")
        replay_dir = output / "replays" / str(seed)
        log_path = output / "logs" / f"{seed}.log"
        index_path = output / "replay-index" / f"{seed}.json"
        record_manifest = {"path": str(seed_manifest), "sha256": sha256(seed_manifest)}
        record.update({"status": "running", "startedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
                       "seedManifest": record_manifest})
        try:
            current = capture_candidate(repo, pack, harness, sim)
            _verify_snapshot(current, args)
            record["provenanceBefore"] = current
            record["toolPinsBefore"] = verify_tool_pins()
            command = [args.dotnet, str(harness), "fief-match", "--pack", str(pack), "--seed", str(seed),
                       "--minutes", str(MINUTES), "--record-dir", str(replay_dir), *E5_OBSERVER_TRACE_FLAGS]
            if replay_dir.exists():
                raise FileExistsError(f"refusing existing per-seed replay directory: {replay_dir}")
            record["command"] = command
            start = time.monotonic()
            with log_path.open("wb") as log:
                completed = process_run(command, cwd=repo, stdout=log, stderr=subprocess.STDOUT, check=False)
            record["elapsedSeconds"] = round(time.monotonic() - start, 3)
            record["exitCode"] = completed.returncode
            record["rawLogSha256"] = sha256(log_path)
            record["rawLogBytes"] = log_path.stat().st_size
            raw_text = log_path.read_text(encoding="utf-8", errors="replace")
            try:
                record["match"] = parse_match_log(raw_text, seed)
            except Exception as exc:
                record["matchParseError"] = f"{type(exc).__name__}: {exc}"
            replay_path = replay_dir / f"{seed}.replay"
            record["replayPath"] = str(replay_path)
            record["replaySha256"] = sha256(replay_path) if replay_path.is_file() else None
            if completed.returncode == 0 and replay_path.is_file():
                index_command = [args.dotnet, str(harness), "replay", "index", "--replay-dir", str(replay_dir),
                                 "--pack", str(pack), "--seeds-file", str(seed_manifest), "--output", str(index_path)]
                record["replayVerifyCommand"] = index_command
                with (output / "logs" / f"{seed}-replay-index.log").open("wb") as verify_log:
                    verify_result = process_run(index_command, cwd=repo, stdout=verify_log,
                                                stderr=subprocess.STDOUT, check=False)
                record["replayVerifyExitCode"] = verify_result.returncode
                record["replayVerifyLogSha256"] = sha256(output / "logs" / f"{seed}-replay-index.log")
                if index_path.is_file():
                    record["replayIndexSha256"] = sha256(index_path)
                    try:
                        report = json.loads(index_path.read_text(encoding="utf-8"))
                        entries = report.get("seeds", [])
                        record["replayVerification"] = entries[0] if len(entries) == 1 else {"verified": False, "error": "wrong replay index entry count"}
                    except (OSError, json.JSONDecodeError, TypeError) as exc:
                        record["replayVerification"] = {"verified": False, "error": f"{type(exc).__name__}: {exc}"}
            post = capture_candidate(repo, pack, harness, sim)
            record["provenanceAfter"] = post
            record["toolPinsAfter"] = verify_tool_pins()
            _verify_snapshot(post, args)
            record["provenanceStable"] = (post == current == before)
            if completed.returncode != 0:
                record["status"] = "process-error"
            elif record.get("matchParseError"):
                record["status"] = "parse-error"
            elif not replay_path.is_file():
                record["status"] = "replay-missing"
            elif record.get("replayVerifyExitCode") != 0 or not record.get("replayVerification", {}).get("verified", False):
                record["status"] = "replay-verification-failed"
            elif not record["provenanceStable"]:
                record["status"] = "provenance-changed"
            elif record["match"]["ended"] and record["match"]["simulatedMinutes"] < MINUTES:
                record["status"] = "match-ended-early"
            else:
                record["status"] = "complete"
        except Exception as exc:
            record["status"] = "runner-error"
            record["failure"] = f"{type(exc).__name__}: {exc}"
            record["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
            if log_path.is_file():
                record["rawLogSha256"] = sha256(log_path)
                record["rawLogBytes"] = log_path.stat().st_size
            for pending in records[i + 1:]:
                pending["status"] = "not-started-after-fatal-runner-error"
            save()
            break
        record["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
        save()
        if record.get("status") == "provenance-changed":
            for pending in records[i + 1:]:
                pending["status"] = "not-started-after-provenance-change"
            break

    try:
        after = capture_candidate(repo, pack, harness, sim)
        _verify_snapshot(after, args)
        metadata["provenanceAfter"] = after
    except Exception as exc:
        metadata["provenanceAfterFailure"] = f"{type(exc).__name__}: {exc}"
    metadata["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
    statuses = [record["status"] for record in records]
    metadata["status"] = "complete" if len(statuses) == SEED_COUNT and all(s == "complete" for s in statuses) else "failed"
    metadata["statusCounts"] = {status: statuses.count(status) for status in sorted(set(statuses))}
    save()
    return metadata


def main(argv: list[str] | None = None) -> int:
    args = make_parser().parse_args(argv)
    try:
        metadata = run(args)
        print(json.dumps({"status": metadata["status"], "statusCounts": metadata["statusCounts"]}, sort_keys=True))
        return 0 if metadata["status"] == "complete" else 1
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
