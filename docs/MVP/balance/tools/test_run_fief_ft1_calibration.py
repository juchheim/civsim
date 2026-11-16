from __future__ import annotations

import argparse
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent))
import run_fief_ft1_calibration as runner


class Ft1CalibrationRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        self.pack = self.root / "numeric.pack"
        self.pack.write_text(json.dumps({"registries": {"eras": [{"id": 5, "base_cost": 6000}]}}))
        self.release = self.root / "Release"
        self.release.mkdir()
        self.harness = self.release / "CivSim.Harness.dll"
        self.sim = self.release / "CivSim.Sim.dll"
        self.harness.write_bytes(b"release harness fixture")
        self.sim.write_bytes(b"adjacent sim fixture")
        self.eras = self.root / "eras.yaml"
        self.eras.write_text("- {id: 5, base_cost: 6000, status: draft}\n")
        self.manifest = self.root / "synthetic-calibration-seeds.txt"
        self.manifest.write_text("".join(f"{seed}\n" for seed in range(1000, 1020)))
        self.manifest_sha = runner.sha256(self.manifest)
        self.out = self.root / "fresh-output"
        self.identity = {
            "headCommit": "1" * 40,
            "sourceAggregateSha256": "a" * 64,
            "packSha256": runner.sha256(self.pack),
            "harnessDllSha256": runner.sha256(self.harness),
            "simDllSha256": runner.sha256(self.sim),
        }
        self.runner_hash = runner.sha256(Path(runner.__file__).resolve())
        self.expected_manifest_pin = patch.object(runner, "EXPECTED_MANIFEST_SHA256", self.manifest_sha)
        self.expected_manifest_pin.start()
        self.addCleanup(self.expected_manifest_pin.stop)

    def tearDown(self):
        self.temp.cleanup()

    def args(self, **overrides):
        values = {
            "repo": self.repo, "pack": self.pack, "harness_dll": self.harness, "sim_dll": self.sim,
            "manifest": self.manifest, "expected_manifest_sha256": self.manifest_sha,
            "expected_head": self.identity["headCommit"],
            "expected_source_aggregate": self.identity["sourceAggregateSha256"],
            "expected_pack_sha256": self.identity["packSha256"],
            "expected_harness_sha256": self.identity["harnessDllSha256"],
            "expected_sim_sha256": self.identity["simDllSha256"],
            "expected_runner_sha256": self.runner_hash,
            "expected_eras_sha256": runner.sha256(self.eras),
            "eras": self.eras, "output_dir": self.out, "dotnet": "dotnet", "minutes": 130,
        }
        values.update(overrides)
        return argparse.Namespace(**values)

    def fake_process(self, calls, *, interrupt=False):
        def run(command, **kwargs):
            calls.append(command)
            kwargs["stdout"].write(b"fixture raw output\n")
            if interrupt:
                raise KeyboardInterrupt()
            return SimpleNamespace(returncode=0)
        return run

    def test_exact_20_seed_natural_commands_and_per_seed_provenance(self):
        calls = []
        result = runner.run(self.args(), capture_fn=lambda *_: self.identity,
                            clean_fn=lambda _: True, process_run=self.fake_process(calls))
        self.assertEqual(0, result)
        metadata = json.loads((self.out / "run_metadata.json").read_text())
        self.assertEqual("complete", metadata["status"])
        self.assertEqual(20, len(metadata["runs"]))
        self.assertEqual(20, len(calls))
        for run in metadata["runs"]:
            command = run["command"]
            self.assertEqual("fief-match", command[2])
            self.assertEqual("130", command[command.index("--minutes") + 1])
            self.assertNotIn("--grant-e5", command)
            self.assertEqual(0, run["exitCode"])
            self.assertEqual(hashlib.sha256(b"fixture raw output\n").hexdigest(), run["rawSha256"])
            self.assertIn("provenanceBefore", run)
            self.assertIn("provenanceAfter", run)
        self.assertEqual(self.runner_hash, metadata["provenanceBefore"]["runnerSha256"])
        self.assertEqual(self.runner_hash, metadata["provenanceAfter"]["runnerSha256"])

    def test_wrong_candidate_and_runner_pins_fail_before_output_creation(self):
        with self.assertRaisesRegex(ValueError, "frozen cohort provenance mismatch.*packSha256"):
            runner.validate_inputs(self.args(expected_pack_sha256="0" * 64),
                                   capture_fn=lambda *_: self.identity, clean_fn=lambda _: True)
        with self.assertRaisesRegex(ValueError, "runner SHA-256 mismatch"):
            runner.validate_inputs(self.args(expected_runner_sha256="0" * 64),
                                   capture_fn=lambda *_: self.identity, clean_fn=lambda _: True)
        self.assertFalse(self.out.exists())

    def test_wrong_manifest_digest_and_wrong_seed_block_fail_closed(self):
        with self.assertRaisesRegex(ValueError, "manifest SHA"):
            runner.validate_inputs(self.args(expected_manifest_sha256="0" * 64),
                                   capture_fn=lambda *_: self.identity, clean_fn=lambda _: True)
        wrong = self.root / "wrong-seeds.txt"
        wrong.write_text("".join(f"{seed}\n" for seed in range(2000, 2020)))
        with self.assertRaisesRegex(ValueError, "manifest SHA-256 mismatch"):
            runner.validate_inputs(self.args(manifest=wrong),
                                   capture_fn=lambda *_: self.identity, clean_fn=lambda _: True)

    def test_stale_output_root_and_dirty_candidate_rejected_before_launch(self):
        self.out.mkdir()
        with self.assertRaisesRegex(FileExistsError, "refusing existing output root"):
            runner.run(self.args(), capture_fn=lambda *_: self.identity, clean_fn=lambda _: True,
                       process_run=self.fake_process([]))
        self.out.rmdir()
        with self.assertRaisesRegex(ValueError, "repository is not clean"):
            runner.validate_inputs(self.args(), capture_fn=lambda *_: self.identity, clean_fn=lambda _: False)
        self.assertFalse(self.out.exists())

    def test_interrupted_run_preserves_partial_hash_and_marks_rest_not_run(self):
        calls = []
        result = runner.run(self.args(), capture_fn=lambda *_: self.identity,
                            clean_fn=lambda _: True, process_run=self.fake_process(calls, interrupt=True))
        self.assertEqual(130, result)
        metadata = json.loads((self.out / "run_metadata.json").read_text())
        self.assertEqual("interrupted", metadata["status"])
        self.assertEqual(1, len(calls))
        self.assertEqual("interrupted", metadata["runs"][0]["status"])
        self.assertEqual(hashlib.sha256(b"fixture raw output\n").hexdigest(), metadata["runs"][0]["rawSha256"])
        self.assertTrue(all(row["status"] == "not_run_interrupted" for row in metadata["runs"][1:]))
        self.assertIn("provenanceAfter", metadata)

    def test_provenance_drift_stops_after_first_seed(self):
        changed = dict(self.identity, simDllSha256="f" * 64)
        captures = iter([self.identity, self.identity, changed, changed])
        calls = []
        result = runner.run(self.args(), capture_fn=lambda *_: next(captures),
                            clean_fn=lambda _: True, process_run=self.fake_process(calls))
        metadata = json.loads((self.out / "run_metadata.json").read_text())
        self.assertEqual(2, result)
        self.assertEqual("failed", metadata["status"])
        self.assertEqual(1, len(calls))
        self.assertIn("frozen cohort provenance mismatch", metadata["runs"][0]["failure"])
        self.assertTrue(all(row["status"] == "not_run_after_failure" for row in metadata["runs"][1:]))


if __name__ == "__main__":
    unittest.main()
