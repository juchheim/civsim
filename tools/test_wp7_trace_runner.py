import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("wp7_trace_runner", Path(__file__).with_name("wp7_trace_runner.py"))
runner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(runner)

HEAD = "bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c"

def fixture(root, cohort="calibration", seeds=None):
    source = root / "source"
    source.mkdir()
    (source / "src.cs").write_text("frozen source")
    subprocess.run(["git", "-C", str(source), "init"], check=True, capture_output=True)
    subprocess.run(["git", "-C", str(source), "config", "user.email", "test@example.invalid"], check=True)
    subprocess.run(["git", "-C", str(source), "config", "user.name", "WP7 test"], check=True)
    subprocess.run(["git", "-C", str(source), "add", "src.cs"], check=True)
    subprocess.run(["git", "-C", str(source), "commit", "-m", "fixture"], check=True, capture_output=True)
    candidates = {"source": str(source)}
    for name in ("pack",):
        path = root / name
        path.write_text(name)
        candidates[name] = str(path)
    release = root / "Release"
    release.mkdir()
    harness = release / "CivSim.Harness.dll"
    sim = release / "CivSim.Sim.dll"
    harness.write_text("harness")
    sim.write_text("sim")
    candidates.update(harness=str(harness), sim=str(sim))
    if seeds is None:
        seeds = list(range(101, 121)) if cohort == "calibration" else list(range(201, 281))
    seed_file = root / f"{cohort}-seeds.txt"
    seed_file.write_text("".join(f"{s}\n" for s in seeds))
    manifest = {
        "protocol": runner.PROTOCOL,
        "cohort": cohort,
        "criteria_status": runner.PROVISIONAL_CRITERIA,
        "head": runner.git_head(source),
        "runner_sha256": runner.sha256(Path(runner.__file__).resolve()),
        "hashes": {key: runner.sha256(path) for key, path in candidates.items()},
        "seed_file_sha256": runner.sha256(seed_file),
        "seeds": seeds,
        "minutes": 130,
    }
    manifest_path = root / f"{cohort}-manifest.json"
    manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
    return candidates, seed_file, manifest_path

class RunnerPreflightTests(unittest.TestCase):
    def test_exact_pins_fresh_output_and_preflight_only(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seeds, manifest = fixture(root)
            out = root / "fresh"
            result = runner.run(manifest, candidates, out, cohort="calibration", seed_file=seeds,
                                manifest_sha256=runner.sha256(manifest), preflight_only=True)
            self.assertEqual(result["preflight"], "passed")
            self.assertEqual(result["cohort"], "calibration")
            self.assertEqual(result["pre_hashes"], result["post_hashes"])
            self.assertEqual(json.loads((out / "manifest.json").read_text())["seeds"], list(range(101, 121)))
            with self.assertRaisesRegex(ValueError, "already exists"):
                runner.run(manifest, candidates, out, cohort="calibration", seed_file=seeds,
                           manifest_sha256=runner.sha256(manifest), preflight_only=True)

    def test_rejects_wrong_manifest_hash_and_seed_block(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seeds, manifest = fixture(root)
            with self.assertRaisesRegex(ValueError, "manifest SHA-256 mismatch"):
                runner.run(manifest, candidates, root / "out", cohort="calibration", seed_file=seeds,
                           manifest_sha256="0" * 64, preflight_only=True)
            bad_manifest = json.loads(manifest.read_text())
            bad_manifest["seeds"][0] = 999
            with self.assertRaisesRegex(ValueError, "seed-file SHA-256 mismatch|exactly match"):
                runner.verify(bad_manifest, candidates, "calibration", seeds)

    def test_rejects_wrong_candidate_pin(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seeds, manifest_path = fixture(root)
            manifest = json.loads(manifest_path.read_text())
            Path(candidates["pack"]).write_text("drifted pack")
            with self.assertRaisesRegex(ValueError, "pack hash mismatch"):
                runner.verify(manifest, candidates, "calibration", seeds)

    def test_rejects_wrong_runner_pin(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seeds, manifest_path = fixture(root)
            manifest = json.loads(manifest_path.read_text())
            manifest["runner_sha256"] = "0" * 64
            with self.assertRaisesRegex(ValueError, "runner hash mismatch"):
                runner.verify(manifest, candidates, "calibration", seeds)

    def test_manifest_rejects_duplicate_seed(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            seeds = [5] * 20
            candidates, seed_file, manifest_path = fixture(root, seeds=seeds)
            manifest = json.loads(manifest_path.read_text())
            with self.assertRaisesRegex(ValueError, "duplicate seeds"):
                runner.verify(manifest, candidates, "calibration", seed_file)

    def test_mock_launch_uses_paired_mode_and_requires_terminal_rows(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seeds, manifest = fixture(root)
            commands = []
            real_run = subprocess.run

            def fake_run(command, **kwargs):
                if command[0] == "git":
                    return real_run(command, **kwargs)
                commands.append(command)
                seed = command[command.index("--seed") + 1]
                kwargs["stdout"].write(f"wp7-match-terminal seed={seed} season=1 matchStatus=horizon_censored endReason=horizon\n")
                return SimpleNamespace(returncode=0)

            out = root / "run"
            with patch.object(runner.subprocess, "run", side_effect=fake_run):
                result = runner.run(manifest, candidates, out, cohort="calibration", seed_file=seeds,
                                    manifest_sha256=runner.sha256(manifest))
            self.assertEqual(len(commands), 20)
            self.assertIn("--fief-wp7-paired", commands[0])
            self.assertNotIn("--fief-wp7-trace", commands[0])
            self.assertTrue(all(row["terminal_row_valid"] for row in result["seeds"]))
            self.assertTrue(all(row["status"] == "complete" for row in result["seeds"]))
            self.assertEqual([row["command"] for row in result["seeds"]], commands)
            self.assertEqual(result["runner_sha256"], json.loads(manifest.read_text())["runner_sha256"])

    def test_source_drift_after_seed_is_recorded_and_stops(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seeds, manifest = fixture(root)
            commands = []
            real_run = subprocess.run

            def fake_run(command, **kwargs):
                if command[0] == "git":
                    return real_run(command, **kwargs)
                commands.append(command)
                kwargs["stdout"].write("wp7-match-terminal seed=101 season=1 matchStatus=horizon_censored endReason=horizon\n")
                (Path(candidates["source"]) / "src.cs").write_text("drifted source")
                return SimpleNamespace(returncode=0)

            out = root / "drift-run"
            with patch.object(runner.subprocess, "run", side_effect=fake_run):
                with self.assertRaisesRegex(ValueError, "source hash mismatch"):
                    runner.run(manifest, candidates, out, cohort="calibration", seed_file=seeds,
                               manifest_sha256=runner.sha256(manifest))
            metadata = json.loads((out / "runner.json").read_text())
            self.assertEqual(metadata["seeds"][0]["status"], "error")
            self.assertIn("post_hash_error", metadata)

    def test_missing_terminal_row_is_an_error(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seeds, manifest = fixture(root)
            real_run = subprocess.run

            def fake_run(command, **kwargs):
                if command[0] == "git":
                    return real_run(command, **kwargs)
                return SimpleNamespace(returncode=0)

            out = root / "missing-terminal"
            with patch.object(runner.subprocess, "run", side_effect=fake_run):
                with self.assertRaisesRegex(RuntimeError, "matching terminal row"):
                    runner.run(manifest, candidates, out, cohort="calibration", seed_file=seeds,
                               manifest_sha256=runner.sha256(manifest))
            metadata = json.loads((out / "runner.json").read_text())
            self.assertEqual(metadata["seeds"][0]["terminal_row_count"], 0)
            self.assertEqual(metadata["seeds"][0]["status"], "error")

if __name__ == "__main__":
    unittest.main()
