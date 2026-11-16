from __future__ import annotations

import hashlib
import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

import run_fief_a8_acceptance as runner


class FiefA8RunnerFixtures(unittest.TestCase):
    def test_seed_manifest_hash_uniqueness_and_disjointness(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            fresh = root / "fresh.txt"
            old = root / "old.txt"
            fresh.write_text("# new cohort\n1001, 1002, 1003, 1004\n1005 1006 1007 1008\n", encoding="utf-8")
            old.write_text("999\n1000\n", encoding="utf-8")
            digest = runner.sha256_file(fresh)
            old_digest = runner.sha256_file(old)
            self.assertEqual(list(range(1001, 1009)), runner.validate_seed_inputs(
                fresh, digest, [f"{old}={old_digest}"]))
            overlap = root / "overlap.txt"
            overlap.write_text("1002\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "overlaps"):
                runner.validate_seed_inputs(fresh, digest,
                    [f"{overlap}={runner.sha256_file(overlap)}"])

    def test_execution_seed_manifest_requires_exactly_eight_unique_seeds(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for count in (7, 8, 9):
                manifest = root / f"seeds-{count}.txt"
                manifest.write_text("\n".join(str(seed) for seed in range(23000001, 23000001 + count)) + "\n",
                                    encoding="utf-8")
                digest = runner.sha256_file(manifest)
                if count == 8:
                    self.assertEqual(list(range(23000001, 23000009)),
                                     runner.validate_seed_inputs(manifest, digest, []))
                else:
                    with self.assertRaisesRegex(ValueError, "exactly 8"):
                        runner.validate_seed_inputs(manifest, digest, [])

    def test_seed_parser_rejects_duplicates_and_malformed_values(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            duplicate = root / "duplicate.txt"
            duplicate.write_text("10 10\n")
            with self.assertRaisesRegex(ValueError, "duplicate"):
                runner.parse_seed_manifest(duplicate)
            malformed = root / "malformed.txt"
            malformed.write_text("10 nope\n")
            with self.assertRaisesRegex(ValueError, "invalid integer"):
                runner.parse_seed_manifest(malformed)

    def test_output_root_must_be_absent(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / "new-run"
            runner.require_absent_output_root(output)
            output.mkdir()
            with self.assertRaisesRegex(ValueError, "already exists"):
                runner.require_absent_output_root(output)

    def test_source_guard_requires_pinned_clean_head_tree_and_aggregate(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp)
            subprocess.run(["git", "init", "-q", str(repo)], check=True)
            subprocess.run(["git", "-C", str(repo), "config", "user.email", "test@example.invalid"], check=True)
            subprocess.run(["git", "-C", str(repo), "config", "user.name", "Fixture"], check=True)
            for directory in ("content", "src", "game"):
                (repo / directory).mkdir()
                (repo / directory / "fixture.txt").write_text(directory)
            helper = repo / runner.HASH_HELPER
            helper.parent.mkdir(parents=True)
            shutil.copy2(Path(__file__).with_name("hash_e5_workspace.py"), helper)
            subprocess.run(["git", "-C", str(repo), "add", "."], check=True)
            subprocess.run(["git", "-C", str(repo), "commit", "-qm", "fixture"], check=True)
            snapshot = runner.source_snapshot(repo)
            verified = runner.verify_snapshot(repo, snapshot["head"], snapshot["tree"],
                                              snapshot["contentSrcGameAggregateSha256"])
            self.assertEqual(snapshot, verified)
            (repo / "src/fixture.txt").write_text("changed")
            with self.assertRaisesRegex(ValueError, "dirty"):
                runner.verify_snapshot(repo, snapshot["head"], snapshot["tree"],
                                       snapshot["contentSrcGameAggregateSha256"])

    def test_pins_and_artifact_records_capture_exact_hashes_and_sizes(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            output = root / "run"
            output.mkdir()
            (output / "a8-summary.json").write_text('{"ok": true}\n')
            (output / "a8-rows.jsonl").write_text('{"seed": 1}\n')
            artifacts = runner.collect_artifacts(output)
            for name, record in artifacts.items():
                path = output / name
                self.assertEqual(runner.sha256_file(path), record["sha256"])
                self.assertEqual(path.stat().st_size, record["bytes"])
            args = SimpleNamespace(
                pack_sha256="p" * 64, harness_sha256="h" * 64, sim_sha256="s" * 64,
                test_source_sha256="t" * 64, seed_manifest_sha256="m" * 64,
                runner_sha256="r" * 64, validator_sha256="v" * 64,
                hash_helper_sha256="x" * 64,
                minutes=64, interval_seasons=16, dop=4,
            )
            snapshot = {"head": "head", "tree": "tree",
                        "contentSrcGameAggregateSha256": "aggregate"}
            pins = runner.build_pins(args, snapshot, [42, 43], "a" * 64)
            self.assertEqual([42, 43], pins["seeds"])
            self.assertEqual("tree", pins["sourceTree"])
            self.assertEqual("a" * 64, pins["testAssemblySha256"])
            self.assertEqual("r" * 64, pins["runnerSha256"])
            self.assertEqual(64, pins["minutes"])


if __name__ == "__main__":
    unittest.main()
