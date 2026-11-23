#!/usr/bin/env python3
"""Run and hash the frozen B5-03 Fief determinism cohort on one host.

The tool refuses to run unless the source checkout is clean and matches the
explicitly pinned source, pack, harness, and simulation hashes. It validates
inputs without writing anything by default; pass --execute to launch the
selected shard (the full 1,000-match cohort when shard-count is 1).
"""

from __future__ import annotations

import argparse
import csv
import datetime as dt
import gzip
import hashlib
import json
import os
import platform
import re
import signal
import struct
import subprocess
import sys
from pathlib import Path
from typing import Any


SCHEMA = "civsim.fief-determinism-run/v1"
INDEX_SCHEMA = "civsim-replay-index-v1"
EXPECTED_MANIFEST_SHA256 = "12f313f9878c568798b32e723f1aa1783b2dbb3039bfae60749f7a279d1d9895"
FIRST_SEED = 40_000_000
SEED_COUNT = 1_000
MINUTES = 130
TOTAL_TICKS = MINUTES * 600
CHECKSUM_INTERVAL = 50
SNAPSHOT_INTERVAL = 750
DEFAULT_MANIFEST = Path(__file__).resolve().parents[1] / "m12-e5-9-b5-03-fief-seeds-2026-11-15.txt"
DEFAULT_REPO = Path(__file__).resolve().parents[4]
DEFAULT_HARNESS = DEFAULT_REPO / "src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _git(repo: Path, *args: str) -> str:
    result = subprocess.run(["git", *args], cwd=repo, check=True, capture_output=True, text=True)
    return result.stdout.strip()


def workspace_snapshot(repo: Path) -> dict[str, Any]:
    """Return the canonical content/src/game workspace hash and complete file manifest."""
    tool = repo / "docs/MVP/balance/tools/hash_e5_workspace.py"
    result = subprocess.run([sys.executable, str(tool)], cwd=repo, check=True,
                            capture_output=True, text=True)
    snapshot = json.loads(result.stdout)
    if snapshot.get("head") != _git(repo, "rev-parse", "HEAD"):
        raise ValueError("workspace aggregate HEAD differs from the checked-out source HEAD")
    files = snapshot.get("files")
    if not isinstance(files, list) or not files:
        raise ValueError("canonical workspace hash did not return a non-empty file manifest")
    if any(item.get("status") != "clean" or not isinstance(item.get("sha256"), str) for item in files):
        raise ValueError("canonical workspace file manifest is not clean and fully hashed")
    return snapshot


def load_manifest(path: Path, expected_sha256: str) -> list[int]:
    if expected_sha256.lower() != EXPECTED_MANIFEST_SHA256:
        raise ValueError(f"manifest pin is stale or unreviewed: expected {EXPECTED_MANIFEST_SHA256}, got {expected_sha256}")
    raw = path.read_bytes()
    actual = hashlib.sha256(raw).hexdigest()
    if actual != expected_sha256.lower():
        raise ValueError(f"manifest hash mismatch: expected {expected_sha256}, got {actual}")
    lines = raw.decode("ascii").splitlines()
    if len(lines) != SEED_COUNT:
        raise ValueError(f"frozen manifest must contain exactly {SEED_COUNT} lines; found {len(lines)}")
    seeds: list[int] = []
    for line_number, line in enumerate(lines, 1):
        if not re.fullmatch(r"[0-9]+", line):
            raise ValueError(f"{path}:{line_number}: expected one unsigned decimal seed")
        seeds.append(int(line))
    expected = list(range(FIRST_SEED, FIRST_SEED + SEED_COUNT))
    if seeds != expected:
        raise ValueError("manifest does not match the explicitly pinned ascending seed block")
    return seeds


def shard_seeds(seeds: list[int], shard_index: int = 0, shard_count: int = 1) -> list[int]:
    """Partition an ordered manifest into balanced, contiguous, non-empty shards."""
    if shard_count < 1 or shard_count > len(seeds):
        raise ValueError(f"shard count must be between 1 and {len(seeds)}")
    if shard_index < 0 or shard_index >= shard_count:
        raise ValueError(f"shard index must be in [0, {shard_count})")
    start = len(seeds) * shard_index // shard_count
    end = len(seeds) * (shard_index + 1) // shard_count
    selected = seeds[start:end]
    if not selected:
        raise ValueError("each B5-03 shard must contain at least one frozen seed")
    return selected


def fresh_external_output(raw_output: Path, repo: Path) -> Path:
    """Require an absent output path outside and disjoint from the frozen checkout."""
    raw = Path(os.path.abspath(raw_output))
    if os.path.lexists(raw):
        raise FileExistsError(f"refusing to overwrite existing host artifact root: {raw}")
    output = raw.resolve()
    root = repo.resolve()
    if output == root or root in output.parents or output in root.parents:
        raise ValueError("B5-03 artifact root must be a fresh path outside the repository worktree")
    return output


def host_identity() -> tuple[str, str, str]:
    system, machine = platform.system(), platform.machine().lower()
    if system == "Darwin" and machine in {"arm64", "aarch64"}:
        return "macOS-arm64", platform.mac_ver()[0], "arm64"
    if system == "Windows" and machine in {"amd64", "x86_64"}:
        return "Windows-x64", platform.version(), "x64"
    raise ValueError(f"B5-03 runs only on macOS arm64 or Windows x64; found {system} {machine}")


def validate_inputs(args: argparse.Namespace) -> tuple[list[int], dict[str, Any]]:
    repo = args.repo_root.resolve()
    pack = args.pack.resolve()
    harness = args.harness_dll.resolve()
    sim = args.sim_dll.resolve() if args.sim_dll else harness.with_name("CivSim.Sim.dll")
    manifest = args.manifest.resolve()
    output = fresh_external_output(args.output_root, repo)
    for path, label in ((repo / ".git", "Git checkout"), (pack, "compiled content pack"),
                        (harness, "Release harness DLL"), (sim, "Release simulation DLL"),
                        (manifest, "frozen seed manifest")):
        if not path.exists():
            raise FileNotFoundError(f"{label} does not exist: {path}")
    if not pack.is_file() or not harness.is_file() or not sim.is_file() or not manifest.is_file():
        raise ValueError("pack, harness DLL, simulation DLL, and manifest must be files")
    if harness.parent != sim.parent:
        raise ValueError("Release Harness and adjacent Sim must be in the same directory")
    seeds = load_manifest(manifest, args.expected_manifest_sha256)
    shard_index = getattr(args, "shard_index", 0)
    shard_count = getattr(args, "shard_count", 1)
    selected_seeds = shard_seeds(seeds, shard_index, shard_count)
    host, os_version, architecture = host_identity()

    head = _git(repo, "rev-parse", "HEAD")
    tree = _git(repo, "rev-parse", "HEAD^{tree}")
    dirty_status = _git(repo, "status", "--porcelain", "--untracked-files=all")
    if dirty_status:
        raise ValueError("source checkout is dirty; freeze and commit the source before B5-03")
    source_snapshot = workspace_snapshot(repo)
    source_aggregate = source_snapshot["aggregate_sha256"]
    expected_aggregate = args.expected_source_aggregate.lower()
    if source_aggregate.lower() != expected_aggregate:
        raise ValueError(f"source workspace aggregate mismatch: expected {expected_aggregate}, got {source_aggregate}")
    for label, actual, expected in (("source HEAD", head, args.expected_source_head),
                                   ("source tree", tree, args.expected_source_tree)):
        if actual.lower() != expected.lower():
            raise ValueError(f"{label} mismatch: expected {expected}, got {actual}")

    hashes = {
        "pack": sha256(pack),
        "harnessDll": sha256(harness),
        "simDll": sha256(sim),
    }
    expected_hashes = {
        "pack": args.expected_pack_sha256,
        "harnessDll": args.expected_harness_sha256,
        "simDll": args.expected_sim_sha256,
    }
    for key, expected in expected_hashes.items():
        if hashes[key] != expected.lower():
            raise ValueError(f"{key} SHA-256 mismatch: expected {expected}, got {hashes[key]}")
    if args.minutes != MINUTES:
        raise ValueError(f"the frozen B5-03 horizon is fixed at {MINUTES} minutes")
    if args.compare_index is not None and not args.compare_index.is_file():
        raise FileNotFoundError(f"other-host replay index does not exist: {args.compare_index}")
    if args.compare_index is not None and shard_count != 1:
        raise ValueError("compare the canonical merged index after all shards finish; per-shard comparison is incomplete")

    test_assembly = args.test_assembly.resolve()
    if not test_assembly.is_file():
        raise FileNotFoundError(f"test assembly does not exist: {test_assembly}")
    test_sources = [path.resolve() for path in args.test_source]
    expected_test_sources = args.expected_test_source_sha256
    if len(test_sources) != len(expected_test_sources) or not test_sources:
        raise ValueError("provide one --expected-test-source-sha256 for every --test-source")
    test_source_hashes = []
    for path, expected in zip(test_sources, expected_test_sources):
        if not path.is_file():
            raise FileNotFoundError(f"test source does not exist: {path}")
        actual = sha256(path)
        if actual != expected.lower():
            raise ValueError(f"test source SHA-256 mismatch for {path}: expected {expected}, got {actual}")
        test_source_hashes.append({"path": str(path), "sha256": actual})
    test_assembly_hash = sha256(test_assembly)
    if test_assembly_hash != args.expected_test_assembly_sha256.lower():
        raise ValueError(f"test assembly SHA-256 mismatch: expected {args.expected_test_assembly_sha256}, got {test_assembly_hash}")
    runner_path = Path(__file__).resolve()
    runner_hash = sha256(runner_path)
    if runner_hash != args.expected_runner_sha256.lower():
        raise ValueError(f"runner SHA-256 mismatch: expected {args.expected_runner_sha256}, got {runner_hash}")
    test_artifacts = {
        "assembly": {"path": str(test_assembly), "sha256": test_assembly_hash},
        "sources": test_source_hashes,
        "runner": {"path": str(runner_path), "sha256": runner_hash},
    }

    input_hashes = {
        "sourceHead": head,
        "sourceTree": tree,
        "sourceAggregate": source_aggregate,
        "manifest": args.expected_manifest_sha256.lower(),
        "pack": hashes["pack"],
        "harnessDll": hashes["harnessDll"],
        "simDll": hashes["simDll"],
        "testAssembly": test_assembly_hash,
        "runner": runner_hash,
    }

    return seeds, {
        "host": host,
        "osVersion": os_version,
        "architecture": architecture,
        "source": {"head": head, "tree": tree, "aggregateSha256": source_aggregate,
                   "fileCount": len(source_snapshot["files"]), "dirty": False, "dirtyDiffSha256": None},
        "sourceManifest": source_snapshot,
        "manifest": {"path": str(manifest), "sha256": args.expected_manifest_sha256.lower(), "seedCount": len(seeds)},
        "pack": {"path": str(pack), "sha256": hashes["pack"], "contentHash": hashes["pack"]},
        "binaries": {
            "harnessDll": {"path": str(harness), "sha256": hashes["harnessDll"]},
            "simDll": {"path": str(sim), "sha256": hashes["simDll"]},
        },
        "shard": {
            "index": shard_index,
            "count": shard_count,
            "seeds": selected_seeds,
            "manifestSha256": args.expected_manifest_sha256.lower(),
        },
        "inputHashes": input_hashes,
        "testArtifacts": test_artifacts,
        "paths": {"repo": str(repo), "outputRoot": str(output), "manifest": str(manifest),
                  "pack": str(pack), "harness": str(harness)},
    }


def parse_fief_replay(path: Path, expected_seed: int, expected_pack_hash: str) -> dict[str, Any]:
    """Read the stable little-endian v3 match replay header and hash its command bytes."""
    data = path.read_bytes()
    view = memoryview(data)
    pos = 0

    def take(fmt: str) -> tuple[Any, ...]:
        nonlocal pos
        size = struct.calcsize("<" + fmt)
        if pos + size > len(view):
            raise ValueError(f"{path}: truncated replay at byte {pos}")
        values = struct.unpack_from("<" + fmt, view, pos)
        pos += size
        return values

    magic, version = take("II")
    if magic != 0x52434D53 or version != 3:
        raise ValueError(f"{path}: expected Fief control replay format v3")
    mode, = take("B")
    if mode != 2:
        raise ValueError(f"{path}: replay is not in Fief mode")
    controls = [take("BB") for _ in range(4)]
    world_fingerprint, final_checksum, archetype_count = take("QQi")
    archetypes = list(take("4B"))
    if archetype_count != 4:
        raise ValueError(f"{path}: expected four archetypes")
    if archetypes != [2, 1, 4, 5] or [arch for _, arch in controls] != archetypes:
        raise ValueError(f"{path}: replay does not use the standard Builder/Grower/Warlord/Sage seats")
    # ControlSpec encodes difficulty first and archetype second; Warlord is 2.
    # The replay retains four ordered AI seat controls.
    if any(level != 2 for level, _ in controls):
        raise ValueError(f"{path}: replay controls are not four Warlord AIs")
    replay_start = pos
    replay_magic, replay_version, seed = take("IIQ")
    if replay_magic != 0x50525343 or replay_version != 1:
        raise ValueError(f"{path}: invalid embedded command replay")
    pack_hash = bytes(view[pos:pos + 32]).hex()
    if len(pack_hash) != 64:
        raise ValueError(f"{path}: truncated content hash")
    pos += 32
    interval, ticks, command_count = take("iqi")
    if command_count < 0 or command_count > 10_000_000:
        raise ValueError(f"{path}: invalid command count {command_count}")
    command_start = pos
    command_bytes = command_count * 23
    if pos + command_bytes > len(view):
        raise ValueError(f"{path}: truncated command list")
    pos += command_bytes
    checksum_count, = take("i")
    if checksum_count < 0 or checksum_count > 10_000_000:
        raise ValueError(f"{path}: invalid checksum count {checksum_count}")
    if pos + checksum_count * 16 != len(view):
        raise ValueError(f"{path}: checksum section size or trailing bytes are invalid")
    checksums = []
    checksum_pos = pos
    for index in range(checksum_count):
        tick, checksum = struct.unpack_from("<qQ", view, checksum_pos + index * 16)
        checksums.append({"tick": tick, "checksum": f"{checksum:016x}"})
    if seed != expected_seed:
        raise ValueError(f"{path}: replay seed {seed} does not match manifest seed {expected_seed}")
    if pack_hash != expected_pack_hash:
        raise ValueError(f"{path}: embedded pack hash {pack_hash} does not match frozen pack")
    if ticks != TOTAL_TICKS:
        raise ValueError(f"{path}: expected {TOTAL_TICKS} ticks, got {ticks}")
    if interval != CHECKSUM_INTERVAL or checksum_count != TOTAL_TICKS // CHECKSUM_INTERVAL:
        raise ValueError(f"{path}: unexpected checksum interval/count {interval}/{checksum_count}")
    return {
        "worldFingerprint": f"{world_fingerprint:016x}",
        "finalChecksum": f"{final_checksum:016x}",
        "ticks": ticks,
        "commandCount": command_count,
        "checksumInterval": interval,
        "checksumCount": checksum_count,
        "checksums": checksums,
        "archetypes": archetypes,
        "controls": [{"difficulty": level, "archetype": arch} for level, arch in controls],
        "commandLogSha256": hashlib.sha256(view[command_start:command_start + command_bytes]).hexdigest(),
        "replaySha256": hashlib.sha256(data).hexdigest(),
        "replayFormatVersion": version,
        "embeddedReplayOffset": replay_start,
    }


def parse_continuation(path: Path, root: Path, seed: int, ticks: int) -> dict[str, Any]:
    expected_checkpoints = list(range(0, ticks + 1, SNAPSHOT_INTERVAL))
    expected_intervals = list(range(CHECKSUM_INTERVAL, ticks + 1, CHECKSUM_INTERVAL))
    checkpoints: list[dict[str, Any]] = []
    intervals: list[dict[str, Any]] = []
    finals: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8", newline="") as stream:
        rows = csv.DictReader(stream)
        required = {"kind", "seed", "tick", "snapshotFile", "snapshotSha256", "expectedChecksum", "restoredChecksum", "matchChecksum"}
        if set(rows.fieldnames or ()) != required:
            raise ValueError(f"{path}: unexpected continuation CSV header")
        for row in rows:
            if int(row["seed"]) != seed:
                raise ValueError(f"{path}: sidecar contains a row for the wrong seed")
            tick = int(row["tick"])
            expected = row["expectedChecksum"].lower()
            restored = row["restoredChecksum"].lower()
            matched = row["matchChecksum"].lower()
            if any(not re.fullmatch(r"[0-9a-f]{16}", value) for value in (expected, restored, matched)):
                raise ValueError(f"{path}: invalid checksum encoding at tick {tick}")
            if row["kind"] == "checkpoint":
                snapshot_name = row["snapshotFile"]
                if Path(snapshot_name).name != snapshot_name or not snapshot_name:
                    raise ValueError(f"{path}: invalid snapshot filename {snapshot_name!r}")
                snapshot = path.parent / snapshot_name
                if not snapshot.is_file():
                    raise FileNotFoundError(f"missing continuation snapshot: {snapshot}")
                raw_bytes = 0
                if snapshot_name == f"{seed}-{tick}.snapshot.gz":
                    with gzip.open(snapshot, "rb") as stream:
                        digest = hashlib.sha256()
                        for block in iter(lambda: stream.read(1024 * 1024), b""):
                            digest.update(block)
                            raw_bytes += len(block)
                        snapshot_hash = digest.hexdigest()
                    compression = "gzip"
                elif snapshot_name == f"{seed}-{tick}.snapshot":
                    snapshot_hash = sha256(snapshot)
                    raw_bytes = snapshot.stat().st_size
                    compression = "none"
                else:
                    raise ValueError(f"{path}: snapshot name does not agree with seed/tick")
                if snapshot_hash != row["snapshotSha256"].lower():
                    raise ValueError(f"{snapshot}: uncompressed snapshot SHA-256 differs from sidecar")
                if expected != restored or expected != matched:
                    raise ValueError(f"{path}: restored checkpoint checksum differs at tick {tick}")
                checkpoints.append({
                    "tick": tick, "path": str(snapshot.resolve()), "sha256": snapshot_hash,
                    "storedSha256": sha256(snapshot), "storedBytes": snapshot.stat().st_size,
                    "rawBytes": raw_bytes, "compression": compression,
                    "expectedChecksum": expected, "restoredChecksum": restored, "resumedChecksum": matched,
                })
            elif row["kind"] == "interval":
                if row["snapshotFile"] or row["snapshotSha256"]:
                    raise ValueError(f"{path}: interval row unexpectedly names a snapshot")
                if expected != restored or expected != matched:
                    raise ValueError(f"{path}: resumed interval checksum differs at tick {tick}")
                intervals.append({"tick": tick, "checksum": expected})
            elif row["kind"] == "final":
                if expected != restored or expected != matched:
                    raise ValueError(f"{path}: final continuation checksum differs")
                finals.append({"tick": tick, "checksum": expected})
            else:
                raise ValueError(f"{path}: unknown continuation row kind {row['kind']!r}")
    if [row["tick"] for row in checkpoints] != expected_checkpoints:
        raise ValueError(f"{path}: checkpoint ticks are missing, duplicated, or out of order")
    if [row["tick"] for row in intervals] != expected_intervals:
        raise ValueError(f"{path}: interval checksum ticks are missing, duplicated, or out of order")
    if len(finals) != 1 or finals[0]["tick"] != ticks:
        raise ValueError(f"{path}: expected exactly one final row at tick {ticks}")
    return {
        "sidecar": str(path.resolve()), "sidecarSha256": sha256(path),
        "checkpointCount": len(checkpoints), "intervalCount": len(intervals),
        "checkpoints": checkpoints, "intervals": intervals,
        "finalChecksum": finals[0]["checksum"], "snapshots": checkpoints,
    }


def compress_continuation_snapshots(sidecar: Path, root: Path) -> dict[str, Any]:
    """Store exact snapshots losslessly as gzip while retaining each raw SHA-256.

    The continuation CSV is updated to name the gzip artifact; its snapshotSha256
    column continues to identify the uncompressed bytes emitted by the harness.
    """
    original_sidecar_sha256 = sha256(sidecar)
    with sidecar.open("r", encoding="utf-8", newline="") as stream:
        reader = csv.DictReader(stream)
        required = {"kind", "seed", "tick", "snapshotFile", "snapshotSha256",
                    "expectedChecksum", "restoredChecksum", "matchChecksum"}
        if set(reader.fieldnames or ()) != required:
            raise ValueError(f"{sidecar}: unexpected continuation CSV header")
        rows = list(reader)
        fieldnames = list(reader.fieldnames or ())

    stored: list[dict[str, Any]] = []
    for row in rows:
        if row["kind"] != "checkpoint":
            continue
        raw_name = row["snapshotFile"]
        if Path(raw_name).name != raw_name or not raw_name.endswith(".snapshot"):
            raise ValueError(f"{sidecar}: invalid raw snapshot filename {raw_name!r}")
        raw_path = root / raw_name
        if not raw_path.is_file():
            raise FileNotFoundError(f"missing continuation snapshot: {raw_path}")
        raw_bytes = raw_path.read_bytes()
        raw_hash = hashlib.sha256(raw_bytes).hexdigest()
        if raw_hash != row["snapshotSha256"].lower():
            raise ValueError(f"{raw_path}: snapshot SHA-256 differs from continuation sidecar")
        gzip_name = raw_name + ".gz"
        gzip_path = root / gzip_name
        if gzip_path.exists():
            raise FileExistsError(f"refusing to overwrite compressed snapshot: {gzip_path}")
        compressed = gzip.compress(raw_bytes, compresslevel=6, mtime=0)
        gzip_path.write_bytes(compressed)
        if gzip.decompress(gzip_path.read_bytes()) != raw_bytes:
            raise ValueError(f"{gzip_path}: lossless compression verification failed")
        compressed_hash = sha256(gzip_path)
        raw_path.unlink()
        row["snapshotFile"] = gzip_name
        stored.append({
            "path": str(gzip_path.resolve()), "rawSha256": raw_hash,
            "storedSha256": compressed_hash, "rawBytes": len(raw_bytes),
            "storedBytes": len(compressed), "compression": "gzip",
        })

    temporary = sidecar.with_suffix(sidecar.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    temporary.replace(sidecar)
    return {
        "sidecarOriginalSha256": original_sidecar_sha256,
        "sidecarStoredSha256": sha256(sidecar),
        "snapshots": stored,
    }


def validate_continuation_against_index(seed: int, continuation: dict[str, Any],
                                        index_entry: dict[str, Any]) -> None:
    indexed = {row["tick"]: row["checksum"].lower() for row in index_entry["checksums"]}
    for row in continuation["intervals"]:
        if indexed.get(row["tick"]) != row["checksum"]:
            raise ValueError(f"seed {seed}: continuation interval checksum differs from replay index at tick {row['tick']}")
    for row in continuation["checkpoints"]:
        if row["tick"] and indexed.get(row["tick"]) != row["expectedChecksum"]:
            raise ValueError(f"seed {seed}: continuation checkpoint checksum differs from replay index at tick {row['tick']}")


def make_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=DEFAULT_REPO)
    parser.add_argument("--pack", type=Path, required=True)
    parser.add_argument("--harness-dll", type=Path, default=DEFAULT_HARNESS)
    parser.add_argument("--sim-dll", type=Path)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--expected-manifest-sha256", required=True)
    parser.add_argument("--test-assembly", type=Path, required=True)
    parser.add_argument("--expected-test-assembly-sha256", required=True)
    parser.add_argument("--test-source", type=Path, action="append", required=True)
    parser.add_argument("--expected-test-source-sha256", action="append", required=True)
    parser.add_argument("--expected-runner-sha256", required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--minutes", type=int, default=MINUTES)
    parser.add_argument("--expected-source-head", required=True)
    parser.add_argument("--expected-source-tree", required=True)
    parser.add_argument("--expected-source-aggregate", required=True)
    parser.add_argument("--expected-pack-sha256", required=True)
    parser.add_argument("--expected-harness-sha256", required=True)
    parser.add_argument("--expected-sim-sha256", required=True)
    parser.add_argument("--compare-index", type=Path,
                        help="optional other-host replay index; comparator output is preserved in this run")
    parser.add_argument("--shard-index", type=int, default=0,
                        help="zero-based shard number; default 0/1 runs the full manifest sequentially")
    parser.add_argument("--shard-count", type=int, default=1,
                        help="number of contiguous, non-empty manifest shards (1 through 1,000)")
    parser.add_argument("--execute", action="store_true",
                        help="after validation, run the selected shard (1,000 matches when shard-count is 1)")
    return parser


def _write_json(path: Path, value: Any) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    temporary.replace(path)


def _mark_interrupted_run_failed(output_root: Path, error: str) -> None:
    """Make an already-created run terminal when orchestration exits exceptionally."""
    metadata_path = output_root / "run_metadata.json"
    if not metadata_path.is_file():
        return
    try:
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
        if metadata.get("status") != "running" or metadata.get("runnerPid") != os.getpid():
            return
        metadata["status"] = "failed"
        metadata["failure"] = error
        metadata["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
        _write_json(metadata_path, metadata)
    except (OSError, json.JSONDecodeError, TypeError):
        # Preserve the original failure and all raw artifacts if metadata itself is damaged.
        return


def _handle_termination_signal(signum: int, _frame: Any) -> None:
    raise KeyboardInterrupt(f"received signal {signum}")


def _run_logged(name: str, argv: list[str], stdout_path: Path, stderr_path: Path,
                cwd: Path) -> dict[str, Any]:
    stdout_path.parent.mkdir(parents=True, exist_ok=True)
    with stdout_path.open("wb") as stdout, stderr_path.open("wb") as stderr:
        completed = subprocess.run(argv, cwd=cwd, stdout=stdout, stderr=stderr, check=False)
    return {
        "name": name, "argv": argv,
        "argvSha256": hashlib.sha256(json.dumps(argv, separators=(",", ":")).encode("utf-8")).hexdigest(),
        "exitCode": completed.returncode,
        "stdout": str(stdout_path.resolve()), "stdoutSha256": sha256(stdout_path),
        "stderr": str(stderr_path.resolve()), "stderrSha256": sha256(stderr_path),
    }


def _load_index(path: Path, seeds: list[int], pack_hash: str) -> dict[int, dict[str, Any]]:
    artifact = json.loads(path.read_text(encoding="utf-8"))
    if artifact.get("schema") != INDEX_SCHEMA or artifact.get("packHash") != pack_hash:
        raise ValueError(f"{path}: invalid index schema or pack hash")
    entries = artifact.get("seeds")
    if not isinstance(entries, list) or [entry.get("seed") for entry in entries] != seeds:
        raise ValueError(f"{path}: replay index seed coverage/order differs from frozen manifest")
    by_seed = {entry["seed"]: entry for entry in entries}
    for seed, entry in by_seed.items():
        if not entry.get("verified") or entry.get("mode") != "fief" or entry.get("ticks") != TOTAL_TICKS:
            raise ValueError(f"{path}: seed {seed} failed replay verification or protocol checks")
        if entry.get("checksumInterval") != CHECKSUM_INTERVAL or entry.get("checksumCount") != TOTAL_TICKS // CHECKSUM_INTERVAL:
            raise ValueError(f"{path}: seed {seed} has an unexpected checksum interval/count")
        checksums = entry.get("checksums")
        expected_ticks = list(range(CHECKSUM_INTERVAL, TOTAL_TICKS + 1, CHECKSUM_INTERVAL))
        if not isinstance(checksums, list) or len(checksums) != len(expected_ticks):
            raise ValueError(f"{path}: seed {seed} does not carry the complete per-tick checksum list")
        if [row.get("tick") for row in checksums] != expected_ticks:
            raise ValueError(f"{path}: seed {seed} checksum ticks are missing, duplicated, or out of order")
        if any(not isinstance(row.get("checksum"), str) or not re.fullmatch(r"[0-9a-fA-F]{16}", row["checksum"])
               for row in checksums):
            raise ValueError(f"{path}: seed {seed} contains an invalid checksum value")
        if entry.get("finalChecksum", "").lower() != checksums[-1]["checksum"].lower():
            raise ValueError(f"{path}: seed {seed} final checksum differs from the last tick record")
    return by_seed


def run(args: argparse.Namespace) -> int:
    seeds, provenance = validate_inputs(args)
    selected_seeds = provenance["shard"]["seeds"]
    if not args.execute:
        print("B5-03 preflight passed; no files written and no matches launched.")
        print(f"host={provenance['host']} source={provenance['source']['head']} tree={provenance['source']['tree']} "
              f"aggregate={provenance['source']['aggregateSha256']} files={provenance['source']['fileCount']}")
        print(f"manifest={provenance['manifest']['sha256']} seeds={len(seeds)} pack={provenance['pack']['sha256']} "
              f"harness={provenance['binaries']['harnessDll']['sha256']} sim={provenance['binaries']['simDll']['sha256']}")
        shard = provenance["shard"]
        print(f"shard={shard['index']}/{shard['count']} selected={selected_seeds[0]}..{selected_seeds[-1]} ({len(selected_seeds)} seeds)")
        print("Pass --execute to create the fresh host root and run this shard.")
        return 0

    repo, output = Path(provenance["paths"]["repo"]), Path(provenance["paths"]["outputRoot"])
    pack, harness = Path(provenance["paths"]["pack"]), Path(provenance["paths"]["harness"])
    manifest = Path(provenance["paths"]["manifest"])
    output.mkdir(parents=True, exist_ok=False)
    (output / "logs").mkdir()
    replay_dir, continuation_dir = output / "replays", output / "continuations"
    index_path, metadata_path = output / "replay-index.json", output / "run_metadata.json"
    subset_manifest = manifest
    shard = provenance["shard"]
    if shard["count"] > 1:
        subset_manifest = output / "seed-manifest.txt"
        subset_manifest.write_text("".join(f"{seed}\n" for seed in selected_seeds), encoding="ascii")
    shard["seedCount"] = len(selected_seeds)
    shard["seedStart"] = selected_seeds[0]
    shard["seedEnd"] = selected_seeds[-1]
    shard["subsetManifest"] = {"path": str(subset_manifest.resolve()), "sha256": sha256(subset_manifest)}
    metadata: dict[str, Any] = {
        "schema": SCHEMA, "status": "running", "startedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "runnerPid": os.getpid(),
        "source": provenance["source"],
        "runtime": {"sdk": None, "runtime": None, "os": f"{platform.system()} {provenance['host']}",
                    "osVersion": provenance["osVersion"], "architecture": provenance["architecture"]},
        "protocol": {"mode": "Fief", "minutes": MINUTES, "ticks": TOTAL_TICKS, "command": "fief-match",
                     "controls": "four Warlord AI archetypes from fief-match defaults", "argv": None,
                     "shardIndex": shard["index"], "shardCount": shard["count"]},
        "manifest": provenance["manifest"], "pack": provenance["pack"], "binaries": provenance["binaries"],
        "shard": shard, "inputHashes": provenance["inputHashes"],
        "testArtifacts": provenance["testArtifacts"],
        "commands": [], "artifacts": {}, "seeds": [],
    }
    source_manifest_path = output / "source-manifest.json"
    _write_json(source_manifest_path, provenance["sourceManifest"])
    metadata["artifacts"]["sourceManifest"] = {
        "path": str(source_manifest_path.resolve()), "sha256": sha256(source_manifest_path),
        "aggregateSha256": provenance["source"]["aggregateSha256"],
        "fileCount": provenance["source"]["fileCount"],
    }
    _write_json(metadata_path, metadata)

    def save() -> None:
        _write_json(metadata_path, metadata)

    # Runtime identification is captured before the cohort starts.
    version = subprocess.run([args.dotnet, "--version"], cwd=repo, capture_output=True, text=True, check=False)
    runtimes = subprocess.run([args.dotnet, "--list-runtimes"], cwd=repo, capture_output=True, text=True, check=False)
    metadata["runtime"]["sdk"] = version.stdout.strip()
    runtime_rows = [line for line in runtimes.stdout.splitlines() if line.startswith("Microsoft.NETCore.App ")]
    metadata["runtime"]["runtime"] = runtime_rows[-1] if runtime_rows else None
    metadata["commands"].extend([
        {"name": "dotnet-version", "argv": [args.dotnet, "--version"], "exitCode": version.returncode,
         "stdout": version.stdout, "stdoutSha256": hashlib.sha256(version.stdout.encode()).hexdigest(),
         "stderr": version.stderr, "stderrSha256": hashlib.sha256(version.stderr.encode()).hexdigest()},
        {"name": "dotnet-list-runtimes", "argv": [args.dotnet, "--list-runtimes"], "exitCode": runtimes.returncode,
         "stdout": runtimes.stdout, "stdoutSha256": hashlib.sha256(runtimes.stdout.encode()).hexdigest(),
         "stderr": runtimes.stderr, "stderrSha256": hashlib.sha256(runtimes.stderr.encode()).hexdigest()},
    ])
    if version.returncode != 0 or runtimes.returncode != 0 or not metadata["runtime"]["sdk"] or not runtime_rows:
        metadata["status"] = "failed"
        metadata["failure"] = "could not identify .NET SDK/runtime"
        save()
        return 2
    save()

    run_argv = [args.dotnet, str(harness), "fief-match", "--pack", str(pack), "--seeds-file", str(subset_manifest),
                "--minutes", str(MINUTES), "--record-dir", str(replay_dir),
                "--checkpoint-continuation", str(continuation_dir)]
    metadata["protocol"]["argv"] = run_argv
    match_cmd = _run_logged("fief-match", run_argv, output / "logs/fief-match.stdout.log",
                            output / "logs/fief-match.stderr.log", repo)
    metadata["commands"].append(match_cmd)
    metadata["commands"].append({**match_cmd, "name": "checkpoint-continuation", "combinedWith": "fief-match"})
    metadata["artifacts"]["rawLogs"] = [
        {"path": match_cmd["stdout"], "sha256": match_cmd["stdoutSha256"]},
        {"path": match_cmd["stderr"], "sha256": match_cmd["stderrSha256"]},
    ]
    save()
    if match_cmd["exitCode"] != 0:
        metadata["status"] = "failed"
        metadata["failure"] = f"fief-match exited {match_cmd['exitCode']}"
        save()
        return match_cmd["exitCode"] or 1

    compressed_by_seed: dict[int, dict[str, Any]] = {}
    snapshot_count = 0
    raw_snapshot_bytes = 0
    stored_snapshot_bytes = 0
    for seed in selected_seeds:
        sidecar = continuation_dir / f"{seed}.continuation.csv"
        compression = compress_continuation_snapshots(sidecar, continuation_dir)
        compressed_by_seed[seed] = compression
        snapshot_count += len(compression["snapshots"])
        raw_snapshot_bytes += sum(item["rawBytes"] for item in compression["snapshots"])
        stored_snapshot_bytes += sum(item["storedBytes"] for item in compression["snapshots"])
        metadata["artifacts"]["snapshotStorage"] = {
            "compression": "gzip", "snapshotCount": snapshot_count,
            "rawBytes": raw_snapshot_bytes, "storedBytes": stored_snapshot_bytes,
        }
        save()

    expected_pack_hash = provenance["pack"]["sha256"]
    replay_metadata: dict[int, dict[str, Any]] = {}
    for seed in selected_seeds:
        replay = replay_dir / f"{seed}.replay"
        replay_metadata[seed] = parse_fief_replay(replay, seed, expected_pack_hash)

    # Explicit per-file verification is retained even though replay index also verifies the cohort.
    verify_results: dict[int, dict[str, Any]] = {}
    for seed in selected_seeds:
        replay = replay_dir / f"{seed}.replay"
        stem = f"replay-verify-{seed}"
        verify_cmd = _run_logged("replay-verify", [args.dotnet, str(harness), "replay", "verify", str(replay),
                                                   "--pack", str(pack)],
                                 output / f"logs/{stem}.stdout.log", output / f"logs/{stem}.stderr.log", repo)
        verify_cmd["seed"] = seed
        metadata["commands"].append(verify_cmd)
        metadata["artifacts"]["rawLogs"].extend([
            {"path": verify_cmd["stdout"], "sha256": verify_cmd["stdoutSha256"]},
            {"path": verify_cmd["stderr"], "sha256": verify_cmd["stderrSha256"]},
        ])
        verify_results[seed] = verify_cmd
        if seed % 25 == 0:
            save()

    index_argv = [args.dotnet, str(harness), "replay", "index", "--replay-dir", str(replay_dir),
                  "--pack", str(pack), "--seeds-file", str(subset_manifest), "--output", str(index_path)]
    index_cmd = _run_logged("replay-index", index_argv, output / "logs/replay-index.stdout.log",
                            output / "logs/replay-index.stderr.log", repo)
    metadata["commands"].append(index_cmd)
    metadata["artifacts"]["rawLogs"].extend([
        {"path": index_cmd["stdout"], "sha256": index_cmd["stdoutSha256"]},
        {"path": index_cmd["stderr"], "sha256": index_cmd["stderrSha256"]},
    ])
    save()
    if index_cmd["exitCode"] != 0:
        metadata["failure"] = f"replay index exited {index_cmd['exitCode']}"
    index = _load_index(index_path, selected_seeds, expected_pack_hash)
    metadata["artifacts"]["replayIndex"] = str(index_path.resolve())
    metadata["artifacts"]["replayIndexSha256"] = sha256(index_path)

    continuation_results: dict[int, dict[str, Any]] = {}
    for seed in selected_seeds:
        sidecar = continuation_dir / f"{seed}.continuation.csv"
        continuation_results[seed] = parse_continuation(sidecar, continuation_dir, seed, TOTAL_TICKS)

    for seed in selected_seeds:
        entry = index[seed]
        replay_info = replay_metadata[seed]
        verify = verify_results[seed]
        continuation = continuation_results[seed]
        if entry["finalChecksum"].lower() != replay_info["finalChecksum"]:
            raise ValueError(f"seed {seed}: replay header and index final checksums differ")
        if continuation["finalChecksum"] != replay_info["finalChecksum"]:
            raise ValueError(f"seed {seed}: continuation and replay final checksums differ")
        validate_continuation_against_index(seed, continuation, index[seed])
        continuation_record = {key: value for key, value in continuation.items()
                               if key not in {"checkpoints", "intervals"}}
        continuation_record["harnessSidecarSha256"] = compressed_by_seed[seed]["sidecarOriginalSha256"]
        replay_path = replay_dir / f"{seed}.replay"
        metadata["seeds"].append({
            "seed": seed, "matchExitCode": match_cmd["exitCode"], "ticks": entry["ticks"],
            "commandCount": entry["commandCount"], "checksumInterval": entry["checksumInterval"],
            "checksumCount": entry["checksumCount"], "finalChecksum": entry["finalChecksum"],
            "worldFingerprint": replay_info["worldFingerprint"],
            "commandLogSha256": replay_info["commandLogSha256"],
            "orderedTickChecksumRecords": "replay-index.json",
            "replay": {"path": str(replay_path.resolve()), "sha256": replay_info["replaySha256"],
                       "verifyExitCode": verify["exitCode"], "verificationStdout": verify["stdout"],
                       "verificationStdoutSha256": verify["stdoutSha256"], "verificationStderr": verify["stderr"],
                       "verificationStderrSha256": verify["stderrSha256"]},
            "continuation": continuation_record,
        })
    save()

    if args.compare_index:
        comparison = output / "logs/index-comparison.txt"
        compare_cmd = _run_logged("index-comparator",
                                  [sys.executable, str(Path(__file__).with_name("compare_replay_indexes.py")),
                                   str(index_path), str(args.compare_index.resolve()),
                                   "--seeds-file", str(manifest)],
                                  comparison, output / "logs/index-comparison.stderr.log", repo)
        metadata["commands"].append(compare_cmd)
        metadata["artifacts"]["indexComparison"] = {
            "path": compare_cmd["stdout"], "sha256": compare_cmd["stdoutSha256"],
            "otherIndex": str(args.compare_index.resolve()), "otherIndexSha256": sha256(args.compare_index.resolve()),
        }
        metadata["artifacts"]["rawLogs"].append({"path": compare_cmd["stderr"], "sha256": compare_cmd["stderrSha256"]})
        if compare_cmd["exitCode"] != 0:
            metadata["failure"] = f"replay-index comparison exited {compare_cmd['exitCode']}"
    else:
        metadata["artifacts"]["indexComparison"] = None

    final_source = workspace_snapshot(repo)
    if final_source["aggregate_sha256"].lower() != provenance["source"]["aggregateSha256"].lower():
        raise ValueError("source workspace aggregate changed during the B5-03 shard")
    for key, path in (("pack", pack), ("harnessDll", harness),
                      ("simDll", Path(provenance["binaries"]["simDll"]["path"]))):
        if sha256(path) != provenance["inputHashes"][key]:
            raise ValueError(f"{key} changed during the B5-03 shard")
    metadata["inputIntegrity"] = {"unchangedAfterRun": True,
                                  "sourceAggregateSha256": final_source["aggregate_sha256"],
                                  "packSha256": sha256(pack), "harnessSha256": sha256(harness),
                                  "simSha256": sha256(Path(provenance["binaries"]["simDll"]["path"]))}
    failed_verify = [seed for seed, result in verify_results.items() if result["exitCode"] != 0]
    metadata["status"] = "complete" if not failed_verify and "failure" not in metadata else "failed"
    if failed_verify:
        metadata["failedReplayVerificationSeeds"] = failed_verify
    metadata["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
    save()
    return 0 if metadata["status"] == "complete" else 1


def main(argv: list[str] | None = None) -> int:
    parser = make_parser()
    args = parser.parse_args(argv)
    output_was_absent = not os.path.lexists(args.output_root)
    if hasattr(signal, "SIGTERM"):
        signal.signal(signal.SIGTERM, _handle_termination_signal)
    hash_values = [args.expected_source_head, args.expected_source_tree, args.expected_source_aggregate,
                   args.expected_pack_sha256, args.expected_harness_sha256, args.expected_sim_sha256,
                   args.expected_manifest_sha256, args.expected_test_assembly_sha256,
                   args.expected_runner_sha256, *args.expected_test_source_sha256]
    if any(not re.fullmatch(r"[0-9a-fA-F]{40,64}", value) for value in hash_values):
        parser.error("all expected pins must be 40–64 hexadecimal characters")
    if any(len(value) != 64 for value in hash_values if value in {
            args.expected_source_aggregate, args.expected_pack_sha256, args.expected_harness_sha256,
            args.expected_sim_sha256, args.expected_manifest_sha256, args.expected_test_assembly_sha256,
            args.expected_runner_sha256, *args.expected_test_source_sha256}):
        parser.error("SHA-256 pins must be exactly 64 hexadecimal characters")
    try:
        return run(args)
    except KeyboardInterrupt as exc:
        if output_was_absent:
            _mark_interrupted_run_failed(args.output_root.resolve(), f"interrupted: {exc or 'KeyboardInterrupt'}")
        print("error: interrupted; preserve any partial shard artifacts", file=sys.stderr)
        return 130
    except Exception as exc:
        if output_was_absent:
            _mark_interrupted_run_failed(args.output_root.resolve(), f"{type(exc).__name__}: {exc}")
        print(f"error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
