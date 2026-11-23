#!/usr/bin/env python3
"""Focused tests for the B5-03 frozen-cohort runner; no cohort is launched."""

from __future__ import annotations

import argparse
import csv
import hashlib
import importlib.util
import json
import os
import struct
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


SCRIPT = Path(__file__).with_name("run_b5_03_determinism.py")
MANIFEST = SCRIPT.parents[1] / "m12-e5-9-b5-03-reviewed-seeds-2026-11-16.txt"
MANIFEST_SHA256 = "12f313f9878c568798b32e723f1aa1783b2dbb3039bfae60749f7a279d1d9895"
SEED_START = 40_000_000
SPEC = importlib.util.spec_from_file_location("run_b5_03_determinism", SCRIPT)
assert SPEC and SPEC.loader
runner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(runner)
sys.path.insert(0, str(SCRIPT.parent))
import merge_b5_03_shards as shard_merger


def _valid_replay(path: Path, seed: int, pack_hash: str) -> None:
    content_hash = bytes.fromhex(pack_hash)
    data = bytearray(struct.pack("<II", 0x52434D53, 3))
    data += struct.pack("<B", 2)  # Fief
    for archetype in (2, 1, 4, 5):
        data += struct.pack("<BB", 2, archetype)  # Warlord AI
    data += struct.pack("<QQi", 0x1234, 0x5678, 4)
    data += bytes((2, 1, 4, 5))
    data += struct.pack("<IIQ", 0x50525343, 1, seed)
    data += content_hash
    data += struct.pack("<iqi", runner.CHECKSUM_INTERVAL, runner.TOTAL_TICKS, 1)
    data += struct.pack("<qBHiii", 100, 0, 1, 2, 3, 4)
    checksum_count = runner.TOTAL_TICKS // runner.CHECKSUM_INTERVAL
    data += struct.pack("<i", checksum_count)
    for tick in range(runner.CHECKSUM_INTERVAL, runner.TOTAL_TICKS + 1, runner.CHECKSUM_INTERVAL):
        data += struct.pack("<qQ", tick, tick + 7)
    path.write_bytes(data)


class B503DeterminismRunnerTests(unittest.TestCase):
    def test_frozen_manifest_has_exact_seed_block_and_hash(self) -> None:
        seeds = runner.load_manifest(MANIFEST, MANIFEST_SHA256)
        self.assertEqual(runner.SEED_COUNT, len(seeds))
        self.assertEqual(SEED_START, seeds[0])
        self.assertEqual(SEED_START + runner.SEED_COUNT - 1, seeds[-1])

    def test_fresh_reviewed_block_requires_exact_hash_and_rejects_stale_pin(self) -> None:
        seeds = runner.load_manifest(MANIFEST, MANIFEST_SHA256)
        self.assertEqual(40_000_000, seeds[0])
        with self.assertRaisesRegex(ValueError, "manifest pin is stale or unreviewed"):
            runner.load_manifest(MANIFEST, "1c05d1be1edf3b6b4cad9bcbf35503ab6b454133515388cc8f42ca82a1ad3ce6")
        with tempfile.TemporaryDirectory() as temporary:
            stale = Path(temporary) / "stale.txt"
            stale.write_text("".join(f"{seed}\n" for seed in range(32_000_000, 32_001_000)), encoding="ascii")
            stale_hash = hashlib.sha256(stale.read_bytes()).hexdigest()
            with patch.object(runner, "EXPECTED_MANIFEST_SHA256", stale_hash):
                with self.assertRaisesRegex(ValueError, "does not match the explicitly pinned ascending seed block"):
                    runner.load_manifest(stale, stale_hash)

    def test_manifest_mutation_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            altered = Path(temporary) / "manifest.txt"
            altered.write_bytes(MANIFEST.read_bytes() + b"\n")
            with self.assertRaisesRegex(ValueError, "manifest hash mismatch"):
                runner.load_manifest(altered, MANIFEST_SHA256)

    def test_shard_partition_is_balanced_contiguous_disjoint_and_complete(self) -> None:
        seeds = runner.load_manifest(MANIFEST, MANIFEST_SHA256)
        partitions = [runner.shard_seeds(seeds, index, 4) for index in range(4)]
        self.assertEqual([250, 250, 250, 250], [len(partition) for partition in partitions])
        self.assertEqual(seeds, [seed for partition in partitions for seed in partition])
        self.assertEqual(len(seeds), len({seed for partition in partitions for seed in partition}))
        for left, right in zip(partitions, partitions[1:]):
            self.assertEqual(left[-1] + 1, right[0])

    def test_shard_partition_balances_non_even_ranges_and_rejects_bad_indices(self) -> None:
        seeds = list(range(11))
        partitions = [runner.shard_seeds(seeds, index, 3) for index in range(3)]
        self.assertEqual([3, 4, 4], [len(partition) for partition in partitions])
        self.assertEqual(seeds, [seed for partition in partitions for seed in partition])
        with self.assertRaisesRegex(ValueError, "shard count"):
            runner.shard_seeds(seeds, 0, 12)
        with self.assertRaisesRegex(ValueError, "shard index"):
            runner.shard_seeds(seeds, 3, 3)

    def test_shard_merge_orders_all_valid_shards_by_frozen_manifest(self) -> None:
        seeds = runner.load_manifest(MANIFEST, MANIFEST_SHA256)
        metadata = []
        for index in range(4):
            subset = runner.shard_seeds(seeds, index, 4)
            metadata.append({
                "shard": {
                    "index": index,
                    "count": 4,
                    "manifestSha256": MANIFEST_SHA256,
                    "seeds": subset,
                    "seedCount": len(subset),
                    "seedStart": subset[0],
                    "seedEnd": subset[-1],
                },
                "seeds": [{"seed": seed} for seed in subset],
            })
        entries = shard_merger.canonical_seed_entries(list(reversed(metadata)), seeds, MANIFEST_SHA256)
        self.assertEqual(seeds, [entry["seed"] for entry in entries])

    def test_shard_merge_rejects_missing_duplicate_and_mismatched_shards(self) -> None:
        seeds = list(range(100))

        def shard(index: int, subset: list[int] | None = None) -> dict[str, object]:
            expected = runner.shard_seeds(seeds, index, 4)
            actual = expected if subset is None else subset
            return {
                "shard": {
                    "index": index,
                    "count": 4,
                    "manifestSha256": MANIFEST_SHA256,
                    "seeds": actual,
                    "seedCount": len(actual),
                    "seedStart": actual[0],
                    "seedEnd": actual[-1],
                },
                "seeds": [{"seed": seed} for seed in actual],
            }

        with self.assertRaisesRegex(ValueError, "matching the supplied shard roots"):
            shard_merger.canonical_seed_entries([shard(0), shard(1), shard(2)], seeds, MANIFEST_SHA256)
        with self.assertRaisesRegex(ValueError, "duplicate or invalid shard index"):
            shard_merger.canonical_seed_entries([shard(0), shard(0), shard(2), shard(3)], seeds, MANIFEST_SHA256)
        with self.assertRaisesRegex(ValueError, "ordered seed subset differs"):
            shard_merger.canonical_seed_entries([shard(0, [0, 1]), shard(1), shard(2), shard(3)], seeds, MANIFEST_SHA256)

    def test_shard_merge_requires_all_hashed_raw_logs_inside_artifact_root(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            logs = []
            for index in range(6):  # one match, one replay verification, one index: stdout and stderr each
                path = root / f"log-{index}.txt"
                path.write_text(f"log {index}\n", encoding="utf-8")
                logs.append({"path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
            metadata = {"shard": {"index": 0, "seeds": [123]}, "artifacts": {"rawLogs": logs}}
            shard_merger._validate_raw_logs(root, metadata)

            metadata["artifacts"]["rawLogs"] = logs[:-1]
            with self.assertRaisesRegex(ValueError, "expected 6 hashed"):
                shard_merger._validate_raw_logs(root, metadata)

            metadata["artifacts"]["rawLogs"] = logs
            (root / "log-0.txt").write_text("altered\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "raw log SHA-256 mismatch"):
                shard_merger._validate_raw_logs(root, metadata)

    def test_shard_merge_rejects_logs_outside_shard_artifact_root(self) -> None:
        with tempfile.TemporaryDirectory() as temporary, tempfile.TemporaryDirectory() as external:
            root = Path(temporary)
            external_log = Path(external) / "log.txt"
            external_log.write_text("external\n", encoding="utf-8")
            record = {"path": str(external_log), "sha256": hashlib.sha256(external_log.read_bytes()).hexdigest()}
            metadata = {"shard": {"index": 0, "seeds": [123]}, "artifacts": {"rawLogs": [record] * 6}}
            with self.assertRaisesRegex(ValueError, "outside its artifact root"):
                shard_merger._validate_raw_logs(root, metadata)

    def test_input_validation_requires_clean_exact_frozen_source_and_binary_hashes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            repo = root / "repo"
            repo.mkdir()
            (repo / ".git").mkdir()
            pack, harness, sim = (root / name for name in ("pack", "harness.dll", "CivSim.Sim.dll"))
            pack.write_bytes(b"canonical pack")
            harness.write_bytes(b"harness")
            sim.write_bytes(b"sim")
            output = root / "output"
            test_assembly = root / "CivSim.Tests.dll"
            test_assembly.write_bytes(b"test assembly")
            test_source = root / "ChapterRulesTests.cs"
            test_source.write_text("fixture", encoding="utf-8")
            args = argparse.Namespace(
                repo_root=repo, pack=pack, harness_dll=harness, sim_dll=sim,
                manifest=MANIFEST, expected_manifest_sha256=MANIFEST_SHA256,
                test_assembly=test_assembly, expected_test_assembly_sha256=hashlib.sha256(test_assembly.read_bytes()).hexdigest(),
                test_source=[test_source], expected_test_source_sha256=[hashlib.sha256(test_source.read_bytes()).hexdigest()],
                expected_runner_sha256=hashlib.sha256(SCRIPT.read_bytes()).hexdigest(), output_root=output, minutes=130,
                expected_source_head="a" * 40, expected_source_tree="b" * 40,
                expected_source_aggregate="c" * 64,
                expected_pack_sha256=hashlib.sha256(pack.read_bytes()).hexdigest(),
                expected_harness_sha256=hashlib.sha256(harness.read_bytes()).hexdigest(),
                expected_sim_sha256=hashlib.sha256(sim.read_bytes()).hexdigest(), compare_index=None,
            )

            def git(_repo: Path, *command: str) -> str:
                if command == ("rev-parse", "HEAD"):
                    return "a" * 40
                if command == ("rev-parse", "HEAD^{tree}"):
                    return "b" * 40
                if command == ("status", "--porcelain", "--untracked-files=all"):
                    return ""
                raise AssertionError(command)

            source_snapshot = {"head": "a" * 40, "aggregate_sha256": "c" * 64,
                               "files": [{"path": "src/example.cs", "status": "clean", "sha256": "d" * 64}]}
            with patch.object(runner, "_git", side_effect=git), patch.object(
                runner, "workspace_snapshot", return_value=source_snapshot
            ), patch.object(
                runner, "host_identity", return_value=("macOS-arm64", "15.3", "arm64")
            ):
                seeds, data = runner.validate_inputs(args)
            self.assertEqual(runner.SEED_COUNT, len(seeds))
            self.assertEqual("a" * 40, data["source"]["head"])
            self.assertEqual("c" * 64, data["source"]["aggregateSha256"])
            self.assertEqual(1, data["source"]["fileCount"])
            self.assertEqual("c" * 64, data["inputHashes"]["sourceAggregate"])
            self.assertEqual(hashlib.sha256(pack.read_bytes()).hexdigest(), data["pack"]["contentHash"])
            self.assertEqual(MANIFEST_SHA256, data["inputHashes"]["manifest"])
            self.assertEqual(args.expected_runner_sha256, data["inputHashes"]["runner"])
            self.assertEqual(args.expected_test_assembly_sha256, data["testArtifacts"]["assembly"]["sha256"])

            output.mkdir()
            with self.assertRaisesRegex(FileExistsError, "refusing to overwrite existing host artifact root"):
                runner.validate_inputs(args)
            output.rmdir()

            pinned_runner = args.expected_runner_sha256
            args.expected_runner_sha256 = "0" * 64
            with patch.object(runner, "_git", side_effect=git), patch.object(
                runner, "workspace_snapshot", return_value=source_snapshot
            ), patch.object(runner, "host_identity", return_value=("macOS-arm64", "15.3", "arm64")):
                with self.assertRaisesRegex(ValueError, "runner SHA-256 mismatch"):
                    runner.validate_inputs(args)
            args.expected_runner_sha256 = pinned_runner

            args.expected_source_aggregate = "e" * 64
            with patch.object(runner, "_git", side_effect=git), patch.object(
                runner, "workspace_snapshot", return_value=source_snapshot
            ), patch.object(runner, "host_identity", return_value=("macOS-arm64", "15.3", "arm64")):
                with self.assertRaisesRegex(ValueError, "source workspace aggregate mismatch"):
                    runner.validate_inputs(args)
            args.expected_source_aggregate = "c" * 64

            with patch.object(runner, "_git", side_effect=lambda *_: " M modified.cs"), patch.object(
                runner, "host_identity", return_value=("macOS-arm64", "15.3", "arm64")
            ):
                with self.assertRaisesRegex(ValueError, "checkout is dirty"):
                    runner.validate_inputs(args)

            args.expected_harness_sha256 = "0" * 64
            with patch.object(runner, "_git", side_effect=git), patch.object(
                runner, "workspace_snapshot", return_value=source_snapshot
            ), patch.object(
                runner, "host_identity", return_value=("macOS-arm64", "15.3", "arm64")
            ):
                with self.assertRaisesRegex(ValueError, "harnessDll SHA-256 mismatch"):
                    runner.validate_inputs(args)

    def test_interrupted_run_metadata_becomes_terminal_failed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            metadata_path = root / "run_metadata.json"
            runner._write_json(metadata_path, {"status": "running", "runnerPid": os.getpid(),
                                               "artifacts": {"preserve": True}})
            runner._mark_interrupted_run_failed(root, "ValueError: verifier failed")
            metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
            self.assertEqual("failed", metadata["status"])
            self.assertEqual("ValueError: verifier failed", metadata["failure"])
            self.assertTrue(metadata["artifacts"]["preserve"])
            self.assertIn("completedUtc", metadata)

    def test_fief_v3_replay_parser_checks_seed_protocol_hash_and_command_digest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            replay_path = Path(temporary) / "40000000.replay"
            pack_hash = "ab" * 32
            _valid_replay(replay_path, SEED_START, pack_hash)
            info = runner.parse_fief_replay(replay_path, SEED_START, pack_hash)
            self.assertEqual("0000000000001234", info["worldFingerprint"])
            self.assertEqual("0000000000005678", info["finalChecksum"])
            self.assertEqual(1, info["commandCount"])
            self.assertEqual(runner.TOTAL_TICKS // runner.CHECKSUM_INTERVAL, info["checksumCount"])
            self.assertEqual(hashlib.sha256(replay_path.read_bytes()).hexdigest(), info["replaySha256"])
            self.assertEqual(f"{runner.CHECKSUM_INTERVAL + 7:016x}", info["checksums"][0]["checksum"])
            with self.assertRaisesRegex(ValueError, "does not match manifest seed"):
                runner.parse_fief_replay(replay_path, SEED_START + 1, pack_hash)
            with self.assertRaisesRegex(ValueError, "does not match frozen pack"):
                runner.parse_fief_replay(replay_path, SEED_START, "cd" * 32)

    def test_continuation_parser_checks_all_checkpoint_and_interval_records(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            seed = SEED_START
            sidecar = root / f"{seed}.continuation.csv"
            columns = ["kind", "seed", "tick", "snapshotFile", "snapshotSha256",
                       "expectedChecksum", "restoredChecksum", "matchChecksum"]
            with sidecar.open("w", encoding="utf-8", newline="") as stream:
                writer = csv.DictWriter(stream, fieldnames=columns)
                writer.writeheader()
                for tick in range(0, runner.TOTAL_TICKS + 1, runner.SNAPSHOT_INTERVAL):
                    snapshot_name = f"{seed}-{tick}.snapshot"
                    snapshot = root / snapshot_name
                    snapshot.write_bytes(f"snapshot-{tick}".encode())
                    checksum = f"{tick + 1:016x}"
                    writer.writerow({"kind": "checkpoint", "seed": seed, "tick": tick,
                                     "snapshotFile": snapshot_name,
                                     "snapshotSha256": hashlib.sha256(snapshot.read_bytes()).hexdigest(),
                                     "expectedChecksum": checksum, "restoredChecksum": checksum,
                                     "matchChecksum": checksum})
                for tick in range(runner.CHECKSUM_INTERVAL, runner.TOTAL_TICKS + 1, runner.CHECKSUM_INTERVAL):
                    checksum = f"{tick + 1:016x}"
                    writer.writerow({"kind": "interval", "seed": seed, "tick": tick,
                                     "snapshotFile": "", "snapshotSha256": "",
                                     "expectedChecksum": checksum, "restoredChecksum": checksum,
                                     "matchChecksum": checksum})
                checksum = f"{runner.TOTAL_TICKS + 1:016x}"
                writer.writerow({"kind": "final", "seed": seed, "tick": runner.TOTAL_TICKS,
                                 "snapshotFile": "", "snapshotSha256": "",
                                 "expectedChecksum": checksum, "restoredChecksum": checksum,
                                 "matchChecksum": checksum})
            result = runner.parse_continuation(sidecar, root, seed, runner.TOTAL_TICKS)
            self.assertEqual(105, result["checkpointCount"])
            self.assertEqual(1_560, result["intervalCount"])
            self.assertEqual(f"{runner.TOTAL_TICKS + 1:016x}", result["finalChecksum"])
            self.assertEqual(hashlib.sha256(sidecar.read_bytes()).hexdigest(), result["sidecarSha256"])
            raw_snapshot_hashes = [snapshot["sha256"] for snapshot in result["snapshots"]]
            storage = runner.compress_continuation_snapshots(sidecar, root)
            self.assertEqual(105, len(storage["snapshots"]))
            self.assertTrue(all(item["compression"] == "gzip" for item in storage["snapshots"]))
            self.assertFalse(any(root.glob("*.snapshot")))
            result = runner.parse_continuation(sidecar, root, seed, runner.TOTAL_TICKS)
            self.assertEqual(raw_snapshot_hashes, [snapshot["sha256"] for snapshot in result["snapshots"]])
            self.assertTrue(all(snapshot["compression"] == "gzip" for snapshot in result["snapshots"]))
            self.assertTrue(all(snapshot["path"].endswith(".snapshot.gz") for snapshot in result["snapshots"]))
            index_entry = {"checksums": [{"tick": tick, "checksum": f"{tick + 1:016x}"}
                                          for tick in range(runner.CHECKSUM_INTERVAL, runner.TOTAL_TICKS + 1,
                                                            runner.CHECKSUM_INTERVAL)]}
            runner.validate_continuation_against_index(seed, result, index_entry)
            bad_index = {"checksums": [{**row, "checksum": "0" * 16} for row in index_entry["checksums"]]}
            with self.assertRaisesRegex(ValueError, "differs from replay index"):
                runner.validate_continuation_against_index(seed, result, bad_index)

            sidecar.write_text(sidecar.read_text(encoding="utf-8").replace(
                ",0000000000000001,0000000000000001,0000000000000001\n",
                ",0000000000000001,0000000000000002,0000000000000001\n", 1), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "restored checkpoint checksum differs"):
                runner.parse_continuation(sidecar, root, seed, runner.TOTAL_TICKS)

    def test_default_mode_is_preflight_only_and_never_launches_commands(self) -> None:
        args = argparse.Namespace(
            execute=False,
            repo_root=Path("repo"), output_root=Path("absent"), dotnet="dotnet",
        )
        provenance = {
            "host": "macOS-arm64", "source": {"head": "a" * 40, "tree": "b" * 40,
                                                   "aggregateSha256": "e" * 64, "fileCount": 1},
            "manifest": {"sha256": MANIFEST_SHA256}, "pack": {"sha256": "c" * 64},
            "binaries": {"harnessDll": {"sha256": "f" * 64}, "simDll": {"sha256": "1" * 64}},
            "shard": {"index": 0, "count": 1, "seeds": list(range(1, 1001))},
        }
        with patch.object(runner, "validate_inputs", return_value=(list(range(1, 1001)), provenance)), \
                patch.object(runner.subprocess, "run") as run_command:
            self.assertEqual(0, runner.run(args))
        run_command.assert_not_called()


if __name__ == "__main__":
    unittest.main()
