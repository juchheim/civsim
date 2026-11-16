#!/usr/bin/env python3
"""Validate and merge completed B5-03 shards into one canonical replay index.

Shard runs must all use the same frozen 1,000-seed manifest and identical
source, pack, harness, simulation binary, and host inputs. This tool rechecks
the run metadata, full tick-index coverage, replay hashes/protocols, and every
continuation sidecar/checkpoint before producing canonical ascending-seed
artifacts. It never repairs, fills, or silently drops missing evidence.
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any

import run_b5_03_determinism as runner


MERGED_SCHEMA = "civsim.fief-determinism-merged/v1"


def _read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path}: expected a JSON object")
    return value


def _sha(path: Path, expected: str, label: str) -> None:
    actual = runner.sha256(path)
    if actual != expected.lower():
        raise ValueError(f"{label} SHA-256 mismatch for {path}: expected {expected}, got {actual}")


def _seeds_from_subset_manifest(path: Path) -> list[int]:
    try:
        rows = path.read_text(encoding="ascii").splitlines()
    except UnicodeDecodeError as exc:
        raise ValueError(f"{path}: shard manifest is not ASCII") from exc
    seeds: list[int] = []
    for line_number, line in enumerate(rows, 1):
        if not re.fullmatch(r"[0-9]+", line):
            raise ValueError(f"{path}:{line_number}: expected one unsigned decimal seed")
        seeds.append(int(line))
    if len(seeds) != len(set(seeds)):
        raise ValueError(f"{path}: duplicate shard seed")
    return seeds


def _validate_raw_logs(root: Path, metadata: dict[str, Any]) -> None:
    """Require every stdout/stderr log to remain present, local, and byte-identical."""
    shard = metadata.get("shard", {})
    seeds = shard.get("seeds", [])
    raw_logs = metadata.get("artifacts", {}).get("rawLogs")
    expected_count = 2 * len(seeds) + 4  # match, each replay verification, and replay index
    if not isinstance(raw_logs, list) or len(raw_logs) != expected_count:
        raise ValueError(f"shard {shard.get('index')}: expected {expected_count} hashed stdout/stderr logs")
    root = root.resolve()
    seen: set[Path] = set()
    for item in raw_logs:
        if not isinstance(item, dict) or not isinstance(item.get("path"), str):
            raise ValueError(f"shard {shard.get('index')}: malformed raw log record")
        path = Path(item["path"]).resolve()
        if not path.is_relative_to(root):
            raise ValueError(f"shard {shard.get('index')}: raw log is outside its artifact root: {path}")
        if path in seen:
            raise ValueError(f"shard {shard.get('index')}: duplicate raw log record: {path}")
        seen.add(path)
        if not path.is_file():
            raise FileNotFoundError(f"shard {shard.get('index')}: raw log missing: {path}")
        _sha(path, item.get("sha256", ""), "raw log")


def canonical_seed_entries(shard_metadata: list[dict[str, Any]], frozen_seeds: list[int], manifest_sha256: str) -> list[dict[str, Any]]:
    """Validate exact non-overlapping shard coverage and return ordered entries."""
    if not shard_metadata:
        raise ValueError("at least one completed shard is required")
    shard_count = len(shard_metadata)
    by_index: dict[int, dict[str, Any]] = {}
    all_entries: dict[int, dict[str, Any]] = {}
    for metadata in shard_metadata:
        shard = metadata.get("shard")
        if not isinstance(shard, dict):
            raise ValueError("run metadata is missing shard identity")
        index, count = shard.get("index"), shard.get("count")
        if not isinstance(index, int) or not isinstance(count, int) or count != shard_count:
            raise ValueError("shards must declare one consistent count matching the supplied shard roots")
        if index < 0 or index >= shard_count or index in by_index:
            raise ValueError(f"duplicate or invalid shard index {index}")
        expected = runner.shard_seeds(frozen_seeds, index, shard_count)
        if shard.get("manifestSha256") != manifest_sha256.lower():
            raise ValueError(f"shard {index}: full frozen manifest hash mismatch")
        if shard.get("seeds") != expected:
            raise ValueError(f"shard {index}: declared ordered seed subset differs from frozen partition")
        if shard.get("seedCount") != len(expected) or shard.get("seedStart") != expected[0] or shard.get("seedEnd") != expected[-1]:
            raise ValueError(f"shard {index}: seed count or bounds do not match frozen partition")
        entries = metadata.get("seeds")
        if not isinstance(entries, list) or [entry.get("seed") for entry in entries] != expected:
            raise ValueError(f"shard {index}: run metadata seed entries differ from its ordered subset")
        by_index[index] = metadata
        for entry in entries:
            seed = entry["seed"]
            if seed in all_entries:
                raise ValueError(f"duplicate seed {seed} across shards")
            all_entries[seed] = entry

    missing_indices = sorted(set(range(shard_count)) - set(by_index))
    if missing_indices:
        raise ValueError(f"missing shard indices: {missing_indices}")
    actual_seeds = sorted(all_entries)
    if actual_seeds != frozen_seeds:
        missing = sorted(set(frozen_seeds) - set(actual_seeds))
        extra = sorted(set(actual_seeds) - set(frozen_seeds))
        raise ValueError(f"shard seed coverage differs from frozen manifest; missing={missing[:10]}, extra={extra[:10]}")
    return [all_entries[seed] for seed in frozen_seeds]


def _check_common_inputs(metadata: dict[str, Any], args: argparse.Namespace) -> dict[str, Any]:
    if metadata.get("schema") != runner.SCHEMA or metadata.get("status") != "complete":
        raise ValueError("each shard must have complete B5-03 run metadata")
    source = metadata.get("source", {})
    pack = metadata.get("pack", {})
    binaries = metadata.get("binaries", {})
    manifest = metadata.get("manifest", {})
    expected = {
        "source head": (source.get("head"), args.expected_source_head),
        "source tree": (source.get("tree"), args.expected_source_tree),
        "source aggregate": (source.get("aggregateSha256"), args.expected_source_aggregate),
        "pack": (pack.get("sha256"), args.expected_pack_sha256),
        "harness DLL": (binaries.get("harnessDll", {}).get("sha256"), args.expected_harness_sha256),
        "simulation DLL": (binaries.get("simDll", {}).get("sha256"), args.expected_sim_sha256),
        "manifest": (manifest.get("sha256"), args.expected_manifest_sha256),
        "manifest input": (metadata.get("inputHashes", {}).get("manifest"), args.expected_manifest_sha256),
    }
    for label, (actual, wanted) in expected.items():
        if not isinstance(actual, str) or actual.lower() != wanted.lower():
            raise ValueError(f"{label} hash mismatch in shard metadata: expected {wanted}, got {actual}")
    test_artifacts = metadata.get("testArtifacts", {})
    assembly = test_artifacts.get("assembly", {})
    runner_info = test_artifacts.get("runner", {})
    source_rows = test_artifacts.get("sources", [])
    if assembly.get("sha256", "").lower() != args.expected_test_assembly_sha256.lower():
        raise ValueError("test assembly hash mismatch in shard metadata")
    _sha(Path(assembly.get("path", "")), args.expected_test_assembly_sha256, "test assembly")
    if runner_info.get("sha256", "").lower() != args.expected_runner_sha256.lower():
        raise ValueError("runner hash mismatch in shard metadata")
    _sha(Path(runner_info.get("path", "")), args.expected_runner_sha256, "runner")
    actual_test_source_hashes = [row.get("sha256", "").lower() for row in source_rows]
    if actual_test_source_hashes != [value.lower() for value in args.expected_test_source_sha256]:
        raise ValueError("test source hashes mismatch in shard metadata")
    for source_row, expected_source_hash in zip(source_rows, args.expected_test_source_sha256):
        _sha(Path(source_row.get("path", "")), expected_source_hash, "test source")
    input_hashes = metadata.get("inputHashes", {})
    if input_hashes.get("testAssembly") != assembly.get("sha256") or input_hashes.get("runner") != runner_info.get("sha256"):
        raise ValueError("test and runner artifact hashes disagree with recorded input hashes")
    if source.get("dirty") is not False or source.get("dirtyDiffSha256") is not None:
        raise ValueError("shard metadata does not record a clean frozen source")
    source_manifest_info = metadata.get("artifacts", {}).get("sourceManifest", {})
    source_manifest_path = Path(source_manifest_info.get("path", ""))
    if not source_manifest_path.is_file():
        raise FileNotFoundError("shard source file manifest is missing")
    _sha(source_manifest_path, source_manifest_info.get("sha256", ""), "source file manifest")
    source_manifest = _read_json(source_manifest_path)
    if (source_manifest.get("aggregate_sha256") != args.expected_source_aggregate.lower()
            or len(source_manifest.get("files", [])) != source.get("fileCount")
            or source_manifest_info.get("fileCount") != source.get("fileCount")):
        raise ValueError("source file manifest does not match the pinned workspace aggregate/file count")
    if manifest.get("seedCount") != runner.SEED_COUNT:
        raise ValueError("shard metadata does not record all 1,000 frozen manifest seeds")
    protocol = metadata.get("protocol", {})
    if protocol.get("mode") != "Fief" or protocol.get("minutes") != runner.MINUTES or protocol.get("ticks") != runner.TOTAL_TICKS:
        raise ValueError("shard protocol does not match frozen B5-03 configuration")
    if protocol.get("command") != "fief-match":
        raise ValueError("shard did not run the fief-match protocol")
    return {
        "source": source,
        "runtime": metadata.get("runtime"),
        "protocol": {key: value for key, value in protocol.items() if key not in {"shardIndex", "shardCount", "argv"}},
        "manifest": manifest,
        "pack": pack,
        "binaries": binaries,
        "inputHashes": metadata.get("inputHashes"),
        "testArtifacts": test_artifacts,
    }


def _validate_shard_artifacts(root: Path, metadata: dict[str, Any], pack_hash: str) -> dict[str, dict[str, Any]]:
    shard = metadata["shard"]
    subset_info = shard.get("subsetManifest")
    if not isinstance(subset_info, dict):
        raise ValueError(f"shard {shard['index']}: missing recorded subset manifest")
    subset_path = Path(subset_info.get("path", ""))
    if not subset_path.is_file():
        raise FileNotFoundError(f"shard {shard['index']}: subset manifest missing: {subset_path}")
    _sha(subset_path, subset_info.get("sha256", ""), "subset manifest")
    if _seeds_from_subset_manifest(subset_path) != shard["seeds"]:
        raise ValueError(f"shard {shard['index']}: subset manifest contents differ from recorded seed subset")

    artifacts = metadata.get("artifacts", {})
    _validate_raw_logs(root, metadata)
    index_path = Path(artifacts.get("replayIndex", ""))
    if not index_path.is_file():
        raise FileNotFoundError(f"shard {shard['index']}: replay index missing: {index_path}")
    _sha(index_path, artifacts.get("replayIndexSha256", ""), "replay index")
    index = runner._load_index(index_path, shard["seeds"], pack_hash)

    commands = metadata.get("commands", [])
    verify_commands = [command for command in commands if command.get("name") == "replay-verify"]
    verify_by_seed: dict[int, dict[str, Any]] = {}
    for command in verify_commands:
        seed = command.get("seed")
        if seed in verify_by_seed:
            raise ValueError(f"shard {shard['index']}: duplicate replay-verification command for seed {seed}")
        if command.get("exitCode") != 0:
            raise ValueError(f"shard {shard['index']}: replay verification failed for seed {seed}")
        verify_by_seed[seed] = command
    if set(verify_by_seed) != set(shard["seeds"]):
        raise ValueError(f"shard {shard['index']}: per-seed replay verification command coverage is incomplete")

    seed_records = metadata.get("seeds", [])
    if [record.get("seed") for record in seed_records] != shard["seeds"]:
        raise ValueError(f"shard {shard['index']}: seed records are missing, duplicated, or out of order")
    checked: dict[str, dict[str, Any]] = {}
    for record in seed_records:
        seed = record["seed"]
        if record.get("matchExitCode") != 0 or record.get("ticks") != runner.TOTAL_TICKS:
            raise ValueError(f"seed {seed}: match exit code or simulated tick count is invalid")
        entry = index[seed]
        for key in ("ticks", "commandCount", "checksumInterval", "checksumCount", "finalChecksum"):
            if record.get(key) != entry.get(key):
                raise ValueError(f"seed {seed}: run metadata {key} differs from replay index")
        replay_info = record.get("replay", {})
        replay_path = Path(replay_info.get("path", ""))
        if not replay_path.is_file():
            raise FileNotFoundError(f"seed {seed}: replay file missing: {replay_path}")
        _sha(replay_path, replay_info.get("sha256", ""), "replay")
        parsed = runner.parse_fief_replay(replay_path, seed, pack_hash)
        if parsed["replaySha256"] != replay_info.get("sha256") or parsed["finalChecksum"] != entry["finalChecksum"].lower():
            raise ValueError(f"seed {seed}: replay header differs from per-seed index metadata")
        if replay_info.get("verifyExitCode") != 0 or seed not in verify_by_seed:
            raise ValueError(f"seed {seed}: replay verification evidence is missing or failed")

        continuation_info = record.get("continuation", {})
        sidecar = Path(continuation_info.get("sidecar", ""))
        if not sidecar.is_file():
            raise FileNotFoundError(f"seed {seed}: continuation sidecar missing: {sidecar}")
        _sha(sidecar, continuation_info.get("sidecarSha256", ""), "continuation sidecar")
        continuation = runner.parse_continuation(sidecar, sidecar.parent, seed, runner.TOTAL_TICKS)
        if continuation_info.get("finalChecksum") != entry["finalChecksum"].lower():
            raise ValueError(f"seed {seed}: continuation final checksum differs from replay index")
        if continuation_info.get("checkpointCount") != continuation["checkpointCount"] or continuation_info.get("intervalCount") != continuation["intervalCount"]:
            raise ValueError(f"seed {seed}: continuation evidence count differs from verified sidecar")
        if continuation_info.get("snapshots") != continuation["snapshots"]:
            raise ValueError(f"seed {seed}: recorded snapshot evidence differs from verified sidecar")
        checked[seed] = entry
    return checked


def merge(args: argparse.Namespace) -> int:
    frozen_seeds = runner.load_manifest(args.manifest.resolve(), args.expected_manifest_sha256)
    if args.output_root.exists():
        raise FileExistsError(f"refusing to overwrite merged artifact root: {args.output_root}")
    if len(args.shard_root) < 1:
        raise ValueError("at least one --shard-root is required")

    roots = [path.resolve() for path in args.shard_root]
    metadata_rows: list[tuple[Path, dict[str, Any]]] = []
    for root in roots:
        metadata_path = root / "run_metadata.json"
        metadata = _read_json(metadata_path)
        metadata_rows.append((root, metadata))
    metadata_rows.sort(key=lambda item: item[1].get("shard", {}).get("index", -1))
    metadata_by_shard = [metadata for _, metadata in metadata_rows]
    ordered_seed_records = canonical_seed_entries(metadata_by_shard, frozen_seeds, args.expected_manifest_sha256)

    common: dict[str, Any] | None = None
    shard_indices: list[dict[str, Any]] = []
    combined_index_entries: dict[int, dict[str, Any]] = {}
    combined_commands: list[dict[str, Any]] = []
    combined_logs: list[dict[str, str]] = []
    for root, metadata in metadata_rows:
        inputs = _check_common_inputs(metadata, args)
        if common is None:
            common = inputs
        elif inputs != common:
            raise ValueError(f"{root}: source, runtime, protocol, manifest, pack, binaries, or input hashes differ across shards")
        shard = metadata["shard"]
        checked = _validate_shard_artifacts(root, metadata, args.expected_pack_sha256.lower())
        for seed, entry in checked.items():
            if seed in combined_index_entries:
                raise ValueError(f"duplicate seed {seed} found in replay indexes")
            combined_index_entries[seed] = entry
        index_path = Path(metadata["artifacts"]["replayIndex"])
        shard_indices.append({
            "index": shard["index"], "count": shard["count"], "seedCount": shard["seedCount"],
            "seeds": shard["seeds"], "root": str(root),
            "runMetadata": str((root / "run_metadata.json").resolve()),
            "runMetadataSha256": runner.sha256(root / "run_metadata.json"),
            "replayIndex": str(index_path.resolve()),
            "replayIndexSha256": runner.sha256(index_path),
            "subsetManifest": shard["subsetManifest"],
        })
        for command in metadata.get("commands", []):
            command_copy = dict(command)
            command_copy["shardIndex"] = shard["index"]
            combined_commands.append(command_copy)
        combined_logs.extend(metadata.get("artifacts", {}).get("rawLogs", []))

    if common is None or sorted(combined_index_entries) != frozen_seeds:
        raise ValueError("merged replay-index coverage does not equal the frozen 1,000-seed manifest")
    # The per-seed metadata returned by the coverage verifier and the verified
    # replay index must agree on the exact canonical ordering.
    if [record["seed"] for record in ordered_seed_records] != frozen_seeds:
        raise ValueError("combined run metadata is not in canonical frozen-manifest order")

    output = args.output_root.resolve()
    output.mkdir(parents=True, exist_ok=False)
    index_path = output / "replay-index.json"
    merged_index = {
        "schema": runner.INDEX_SCHEMA,
        "packHash": args.expected_pack_sha256.lower(),
        "seeds": [combined_index_entries[seed] for seed in frozen_seeds],
    }
    index_path.write_text(json.dumps(merged_index, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    metadata_path = output / "run_metadata.json"
    run_metadata = {
        "schema": MERGED_SCHEMA,
        "status": "complete",
        "mergedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "source": common["source"],
        "runtime": common["runtime"],
        "protocol": {**common["protocol"], "shardCount": len(shard_indices), "execution": "parallel contiguous manifest shards"},
        "manifest": common["manifest"],
        "pack": common["pack"],
        "binaries": common["binaries"],
        "inputHashes": common["inputHashes"],
        "testArtifacts": common["testArtifacts"],
        "merger": {"path": str(Path(__file__).resolve()), "sha256": runner.sha256(Path(__file__).resolve())},
        "shards": shard_indices,
        "commands": combined_commands,
        "artifacts": {
            "replayIndex": str(index_path.resolve()),
            "replayIndexSha256": runner.sha256(index_path),
            "rawLogs": combined_logs,
        },
        "seeds": ordered_seed_records,
    }
    runner._write_json(metadata_path, run_metadata)
    print(f"Merged {len(shard_indices)} shards covering {len(frozen_seeds)}/{runner.SEED_COUNT} frozen seeds.")
    print(f"Canonical replay index: {index_path}")
    print(f"Replay index SHA-256: {runner.sha256(index_path)}")
    print(f"Merged run metadata: {metadata_path}")
    print(f"Metadata SHA-256: {runner.sha256(metadata_path)}")
    return 0


def make_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--expected-manifest-sha256", required=True)
    parser.add_argument("--expected-test-assembly-sha256", required=True)
    parser.add_argument("--expected-test-source-sha256", action="append", required=True)
    parser.add_argument("--expected-runner-sha256", required=True)
    parser.add_argument("--expected-merger-sha256", required=True)
    parser.add_argument("--shard-root", type=Path, action="append", default=[],
                        help="completed shard output root; provide one per shard")
    parser.add_argument("--output-root", type=Path, required=True,
                        help="new, non-existing output directory for merged index and metadata")
    parser.add_argument("--expected-source-head", required=True)
    parser.add_argument("--expected-source-tree", required=True)
    parser.add_argument("--expected-source-aggregate", required=True)
    parser.add_argument("--expected-pack-sha256", required=True)
    parser.add_argument("--expected-harness-sha256", required=True)
    parser.add_argument("--expected-sim-sha256", required=True)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = make_parser()
    args = parser.parse_args(argv)
    for name in ("expected_source_head", "expected_source_tree", "expected_source_aggregate", "expected_pack_sha256",
                 "expected_harness_sha256", "expected_sim_sha256"):
        value = getattr(args, name)
        if not re.fullmatch(r"[0-9a-fA-F]{40,64}", value):
            parser.error(f"--{name.replace('_', '-')} must be a 40–64 character hexadecimal hash")
    for name in ("expected_source_aggregate", "expected_pack_sha256", "expected_harness_sha256", "expected_sim_sha256"):
        if len(getattr(args, name)) != 64:
            parser.error(f"--{name.replace('_', '-')} must be a 64-character SHA-256")
    for value in [args.expected_manifest_sha256, args.expected_test_assembly_sha256,
                  args.expected_runner_sha256, args.expected_merger_sha256,
                  *args.expected_test_source_sha256]:
        if not re.fullmatch(r"[0-9a-fA-F]{64}", value):
            parser.error("manifest, test, and tool pins must be 64-character SHA-256 values")
    if runner.sha256(Path(__file__).resolve()) != args.expected_merger_sha256.lower():
        parser.error("--expected-merger-sha256 does not match this merger script")
    try:
        return merge(args)
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
