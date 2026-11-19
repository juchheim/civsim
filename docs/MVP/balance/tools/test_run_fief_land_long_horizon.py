from __future__ import annotations

import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
import sys
from types import SimpleNamespace

SCRIPT = Path(__file__).with_name("run_fief_land_long_horizon.py")
sys.path.insert(0, str(SCRIPT.parent))
SPEC = importlib.util.spec_from_file_location("run_fief_land_long_horizon", SCRIPT)
runner = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(runner)


class FiefLandLongHorizonRunnerTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        self.pack = self.root / "candidate.pack"
        self.release = self.root / "Release"
        self.release.mkdir()
        self.harness = self.release / "CivSim.Harness.dll"
        self.sim = self.release / "CivSim.Sim.dll"
        for path in (self.pack, self.harness, self.sim):
            path.write_text(path.name)
        self.identity = {
            "headCommit": "1" * 40, "sourceAggregateSha256": "a" * 64,
            "packSha256": "b" * 64, "harnessDllSha256": "c" * 64,
            "simDllSha256": "d" * 64,
        }
        self.pins = {**self.identity, "runnerSha256": runner.sha256(SCRIPT)}
        self.manifest_path = self.root / "manifest.json"
        self.output = self.root / "output"

    def tearDown(self):
        self.tmp.cleanup()

    def rows(self, seeds=(101, 102)):
        return [{"seed": seed, "focusCiv": seed % 4, "arm": arm}
                for seed in seeds for arm in runner.ARMS]

    def write_manifest(self, *, rows=None, arms=None, pins=None):
        manifest = {
            "protocol": runner.PROTOCOL, "minutes": runner.MINUTES,
            "arms": list(runner.ARMS) if arms is None else arms,
            "pins": self.pins if pins is None else pins,
            "runs": self.rows() if rows is None else rows,
        }
        self.manifest_path.write_text(json.dumps(manifest, sort_keys=True), encoding="utf-8")
        return hashlib.sha256(self.manifest_path.read_bytes()).hexdigest()

    def call(self, manifest_sha, *, capture_fn=None, process_run=None, output=None):
        return runner.run(self.manifest_path, manifest_sha, self.repo, self.pack, self.harness, self.sim,
                          self.output if output is None else output,
                          capture_fn=capture_fn or (lambda *_: self.identity),
                          process_run=process_run or self.successful_process)

    @staticmethod
    def successful_process(command, **kwargs):
        seed = command[command.index("--seed") + 1]
        arm = "agr11" if "--fief-land-anchor" in command else "control_no_agr11"
        focus = command[command.index("--fief-land-focus-civ") + 1]
        kwargs["stdout"].write((f"FIEF_LAND_MANIFEST seed={seed} focusCiv={focus} arm={arm} "
                                "packHash=pack worldFingerprint=world matchStartChecksum=001122\n").encode())
        return SimpleNamespace(returncode=0)

    def test_exact_roster_runs_both_paired_arms_and_records_commands_hashes(self):
        digest = self.write_manifest()
        metadata = self.call(digest)
        self.assertEqual("complete", metadata["status"])
        self.assertEqual(4, len(metadata["runs"]))
        self.assertEqual(["agr11", "control_no_agr11", "agr11", "control_no_agr11"],
                         [row["arm"] for row in metadata["runs"]])
        for row in metadata["runs"]:
            self.assertEqual(180, int(row["command"][row["command"].index("--minutes") + 1]))
            self.assertEqual(0, row["exitCode"])
            self.assertEqual(64, len(row["rawSha256"]))
            self.assertTrue(row["manifestRowValid"])
        saved = json.loads((self.output / "runner.json").read_text())
        self.assertEqual(self.pins["runnerSha256"], saved["runnerSha256"])
        self.assertEqual(digest, saved["manifestSha256"])

    def test_wrong_candidate_pin_fails_before_output_creation(self):
        pins = dict(self.pins, packSha256="e" * 64)
        digest = self.write_manifest(pins=pins)
        with self.assertRaisesRegex(ValueError, "candidate pin mismatch.*packSha256"):
            self.call(digest)
        self.assertFalse(self.output.exists())

    def test_wrong_manifest_digest_and_runner_pin_fail_closed(self):
        self.write_manifest()
        with self.assertRaisesRegex(ValueError, "manifest SHA-256 mismatch"):
            self.call("0" * 64)
        pins = dict(self.pins, runnerSha256="e" * 64)
        digest = self.write_manifest(pins=pins)
        with self.assertRaisesRegex(ValueError, "candidate pin mismatch.*runnerSha256"):
            self.call(digest)
        self.assertFalse(self.output.exists())

    def test_duplicate_roster_row_and_missing_arm_fail_closed(self):
        rows = self.rows()
        with self.subTest("duplicate"):
            digest = self.write_manifest(rows=rows + [rows[0]])
            with self.assertRaisesRegex(ValueError, "duplicate manifest run"):
                self.call(digest)
            self.assertFalse(self.output.exists())
        with self.subTest("missing arm"):
            digest = self.write_manifest(rows=rows[:-1])
            with self.assertRaisesRegex(ValueError, "exactly both paired arms"):
                self.call(digest)
            self.assertFalse(self.output.exists())

    def test_manifest_cannot_omit_or_change_required_arm_pair(self):
        digest = self.write_manifest(arms=["agr11"])
        with self.assertRaisesRegex(ValueError, "manifest arms must be exactly"):
            self.call(digest)
        self.assertFalse(self.output.exists())

    def test_stale_output_root_is_rejected_without_launch(self):
        digest = self.write_manifest()
        self.output.mkdir()
        launches = []
        with self.assertRaisesRegex(FileExistsError, "refusing existing output root"):
            self.call(digest, process_run=lambda *a, **k: launches.append(a))
        self.assertEqual([], launches)

    def test_provenance_drift_after_run_stops_and_records_failure(self):
        digest = self.write_manifest()
        changed = dict(self.identity, simDllSha256="f" * 64)
        captures = iter([self.identity, self.identity, changed, changed])
        launches = []

        def fake_process(command, **kwargs):
            launches.append(command)
            return self.successful_process(command, **kwargs)

        metadata = self.call(digest, capture_fn=lambda *_: next(captures), process_run=fake_process)
        self.assertEqual("failed", metadata["status"])
        self.assertEqual(1, len(launches))
        self.assertEqual("error", metadata["runs"][0]["status"])
        self.assertIn("candidate pin mismatch", metadata["runs"][0]["failure"])
        self.assertEqual("not_run_after_failure", metadata["runs"][1]["status"])

    def test_interruption_records_partial_raw_and_marks_pending_runs_not_run(self):
        digest = self.write_manifest()
        launches = []

        def interrupted(command, **kwargs):
            launches.append(command)
            kwargs["stdout"].write(b"partial F-T4 output\n")
            raise KeyboardInterrupt()

        metadata = self.call(digest, process_run=interrupted)
        self.assertEqual("interrupted", metadata["status"])
        self.assertEqual(1, len(launches))
        first = metadata["runs"][0]
        self.assertEqual("interrupted", first["status"])
        self.assertIsNone(first["exitCode"])
        self.assertEqual(hashlib.sha256(b"partial F-T4 output\n").hexdigest(), first["rawSha256"])
        self.assertEqual("not_run_interrupted", metadata["runs"][1]["status"])
        self.assertIn("provenanceAfter", metadata)


if __name__ == "__main__":
    unittest.main()
