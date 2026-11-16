from __future__ import annotations

import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("wp7_trace_runner", Path(__file__).with_name("wp7_trace_runner.py"))
runner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(runner)


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def fixture(root: Path, cohort_role: str = "calibration", seed_count: int | None = None):
    source = root / "source"
    for directory in ("content", "src", "game", "docs/MVP/balance/tools"):
        (source / directory).mkdir(parents=True, exist_ok=True)
    (source / "content/rules.json").write_text("{}\n")
    (source / "src/sim.cs").write_text("frozen source\n")
    (source / "game/view.gd").write_text("# frozen view\n")
    analyzer = source / "docs/MVP/balance/tools/analyze_fief_gov11_city_year_pairs.py"
    analyzer.write_text("# frozen analyzer\n")
    subprocess.run(["git", "-C", str(source), "init", "-q"], check=True)
    subprocess.run(["git", "-C", str(source), "config", "user.email", "test@example.invalid"], check=True)
    subprocess.run(["git", "-C", str(source), "config", "user.name", "WP7 test"], check=True)
    subprocess.run(["git", "-C", str(source), "add", "content", "src", "game", "docs"], check=True)
    subprocess.run(["git", "-C", str(source), "commit", "-qm", "fixture"], check=True)

    pack = root / "numeric-e5.pack"
    pack.write_bytes(b"numeric status pack fixture")
    release = root / "src/CivSim.Harness/bin/Release/net10.0"
    release.mkdir(parents=True)
    harness = release / "CivSim.Harness.dll"
    sim = release / "CivSim.Sim.dll"
    harness.write_bytes(b"harness fixture")
    sim.write_bytes(b"sim fixture")
    candidates = {"source": source, "pack": pack, "harness": harness, "sim": sim}

    dotnet = root / "dotnet-fixture"
    dotnet.write_text(
        "#!/usr/bin/env python3\n"
        "import sys\n"
        "if sys.argv[1:] == ['--version']:\n"
        " print('10.0.100')\n"
        "elif sys.argv[1:] == ['--list-runtimes']:\n"
        " print('Microsoft.NETCore.App 10.0.0 [/fixture/shared/Microsoft.NETCore.App]')\n"
        " print('Microsoft.AspNetCore.App 10.0.0 [/fixture/shared/Microsoft.AspNetCore.App]')\n"
        "else:\n"
        " raise SystemExit(1)\n"
    )
    dotnet.chmod(0o755)
    count = seed_count if seed_count is not None else runner.COHORT_SIZES[cohort_role]
    seeds = list(range(10_101, 10_101 + count))
    seed_file = root / f"{cohort_role}-seeds.txt"
    seed_file.write_text("".join(f"{seed}\n" for seed in seeds), encoding="utf-8")
    snapshot = runner.candidate_snapshot(source, pack, harness, sim, str(dotnet))
    pins = {key: snapshot[key] for key in (
        "head", "source_aggregate_sha256", "pack_sha256", "harness_sha256", "sim_sha256",
        "runner_sha256", "analyzer_sha256")}
    manifest = {
        "protocol": runner.PROTOCOL,
        "cohort_role": cohort_role,
        "criteria_status": runner.PROVISIONAL_CRITERIA,
        "minutes": runner.MINUTES,
        "trace_flag": runner.TRACE_FLAG,
        "pack_role": "numeric_e5_status",
        "pins": pins,
        "environment": snapshot["environment"],
        "build_provenance": {
            "configuration": "Release",
            "target_framework": "net10.0",
            "head": snapshot["head"],
            "source_aggregate_sha256": snapshot["source_aggregate_sha256"],
            "pack_sha256": snapshot["pack_sha256"],
            "harness_sha256": snapshot["harness_sha256"],
            "sim_sha256": snapshot["sim_sha256"],
            "sim_adjacent_to_harness": True,
            "build_command": [str(dotnet), "build", "src/CivSim.Harness", "-c", "Release"],
            "build_utc": "2026-11-16T12:00:00+00:00",
        },
        "seed_file_sha256": sha(seed_file),
        "seeds": seeds,
    }
    manifest_path = root / f"{cohort_role}-manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return candidates, seed_file, manifest_path, str(dotnet)


def command_runner(fake_dotnet: str, seeds: list[int], *, mutate_after: Path | None = None,
                   omit_terminal: bool = False, wrong_seed: bool = False):
    real_run = subprocess.run

    def fake_run(command, **kwargs):
        if command[0] == "git":
            return real_run(command, **kwargs)
        if (Path(command[0]).name == Path(fake_dotnet).name
                and command[1:] in (["--version"], ["--list-runtimes"])):
            return real_run(command, **kwargs)
        if "fief-match" in command:
            seed = int(command[command.index("--seed") + 1])
            if not omit_terminal:
                written_seed = seed + 1 if wrong_seed else seed
                kwargs["stdout"].write(
                    f"wp7-match-terminal seed={written_seed} season=520 matchStatus=horizon_censored endReason=horizon\n"
                )
            if mutate_after is not None and seed == seeds[0]:
                mutate_after.write_text("changed during run\n")
            return SimpleNamespace(returncode=0)
        raise AssertionError(f"unexpected subprocess command: {command}")
    return fake_run


class RunnerPreflightTests(unittest.TestCase):
    def test_source_aggregate_matches_shared_workspace_hash_tool(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, _, _, _ = fixture(root)
            source = Path(candidates["source"])
            hash_tool = Path(__file__).resolve().parents[1] / "docs/MVP/balance/tools/hash_e5_workspace.py"
            proc = subprocess.run([os.fspath(Path(os.sys.executable)), os.fspath(hash_tool)],
                                  cwd=source, check=True, capture_output=True, text=True)
            expected = json.loads(proc.stdout)["aggregate_sha256"]
            self.assertEqual(runner.source_snapshot(source)["aggregate_sha256"], expected)

    def test_preflight_pins_clean_source_environment_and_fresh_output(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seed_file, manifest, dotnet = fixture(root)
            output = root / "fresh"
            with patch.object(runner.subprocess, "run", side_effect=command_runner(dotnet, [])):
                result = runner.run(manifest, candidates, output, cohort_role="calibration",
                                    seed_file=seed_file, manifest_sha256=sha(manifest),
                                    dotnet=dotnet, preflight_only=True)
            self.assertEqual(result["status"], "preflight_passed")
            self.assertEqual(result["postflight"], "passed")
            self.assertEqual(result["head"], runner.source_snapshot(candidates["source"])["head"])
            self.assertEqual(result["environment"]["architecture"], runner.platform.machine())
            self.assertTrue((output / "provenance.json").is_file())
            with self.assertRaisesRegex(ValueError, "already exists"):
                with patch.object(runner.subprocess, "run", side_effect=command_runner(dotnet, [])):
                    runner.run(manifest, candidates, output, cohort_role="calibration",
                               seed_file=seed_file, manifest_sha256=sha(manifest),
                               dotnet=dotnet, preflight_only=True)

    def test_failed_preflight_is_recorded_as_failed(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seed_file, manifest, dotnet = fixture(root)
            output = root / "preflight-drift"
            real_verify = runner.verify
            calls = 0

            def drift_after_initial(manifest_obj, candidate_paths, cohort_role, frozen_seeds, dotnet_host="dotnet"):
                nonlocal calls
                calls += 1
                if calls == 2:
                    Path(candidate_paths["pack"]).write_bytes(b"drifted after preflight")
                return real_verify(manifest_obj, candidate_paths, cohort_role, frozen_seeds, dotnet_host)

            with patch.object(runner, "verify", side_effect=drift_after_initial):
                with self.assertRaisesRegex(ValueError, "pack_sha256 mismatch"):
                    runner.run(manifest, candidates, output, cohort_role="calibration",
                               seed_file=seed_file, manifest_sha256=sha(manifest),
                               dotnet=dotnet, preflight_only=True)
            metadata = json.loads((output / "runner.json").read_text())
            self.assertEqual(metadata["status"], "failed")
            self.assertIn("pack_sha256 mismatch", metadata["failure"])

    def test_requires_exact_cohort_size_and_role(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seed_file, manifest_path, dotnet = fixture(root, seed_count=20)
            manifest = json.loads(manifest_path.read_text())
            with self.assertRaisesRegex(ValueError, "exactly 20"):
                runner.verify({**manifest, "seeds": list(range(19))},
                              candidates, "calibration", seed_file, dotnet)
            with self.assertRaisesRegex(ValueError, "cohort role mismatch"):
                runner.verify(manifest, candidates, "holdout", seed_file, dotnet)

    def test_holdout_manifest_requires_and_accepts_eighty_seeds(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seed_file, manifest_path, dotnet = fixture(root, cohort_role="holdout")
            manifest = json.loads(manifest_path.read_text())
            self.assertEqual(len(manifest["seeds"]), 80)
            self.assertEqual(runner.verify(manifest, candidates, "holdout", seed_file, dotnet)["head"],
                             manifest["pins"]["head"])

    def test_rejects_manifest_and_seed_hash_mismatch(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seed_file, manifest_path, dotnet = fixture(root)
            with self.assertRaisesRegex(ValueError, "manifest SHA-256 mismatch"):
                runner.run(manifest_path, candidates, root / "out", cohort_role="calibration",
                           seed_file=seed_file, manifest_sha256="0" * 64, dotnet=dotnet,
                           preflight_only=True)
            manifest = json.loads(manifest_path.read_text())
            manifest["seeds"][0] += 100
            with self.assertRaisesRegex(ValueError, "preregistered seed-file SHA-256 mismatch|exactly match"):
                runner.verify(manifest, candidates, "calibration", seed_file, dotnet)

    def test_rejects_changed_source_pack_binary_runner_or_analyzer_pins(self):
        for pin_name, error in (("source_aggregate_sha256", "source_aggregate_sha256 mismatch"),
                                ("pack_sha256", "pack_sha256 mismatch"),
                                ("harness_sha256", "harness_sha256 mismatch"),
                                ("sim_sha256", "sim_sha256 mismatch"),
                                ("runner_sha256", "runner_sha256 mismatch"),
                                ("analyzer_sha256", "analyzer_sha256 mismatch")):
            with self.subTest(pin=pin_name), tempfile.TemporaryDirectory() as td:
                root = Path(td)
                candidates, seed_file, manifest_path, dotnet = fixture(root)
                manifest = json.loads(manifest_path.read_text())
                manifest["pins"][pin_name] = "0" * 64
                with self.assertRaisesRegex(ValueError, error):
                    runner.verify(manifest, candidates, "calibration", seed_file, dotnet)

    def test_rejects_dirty_source_and_build_attestation_mismatch(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seed_file, manifest_path, dotnet = fixture(root)
            manifest = json.loads(manifest_path.read_text())
            manifest["build_provenance"]["source_aggregate_sha256"] = "0" * 64
            with self.assertRaisesRegex(ValueError, "build provenance"):
                runner.verify(manifest, candidates, "calibration", seed_file, dotnet)
            (Path(candidates["source"]) / "src/untracked.cs").write_text("dirty\n")
            with self.assertRaisesRegex(ValueError, "source workspace must be clean"):
                runner.verify(json.loads(manifest_path.read_text()), candidates, "calibration", seed_file, dotnet)

    def test_rejects_runtime_or_architecture_drift(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seed_file, manifest_path, dotnet = fixture(root)
            manifest = json.loads(manifest_path.read_text())
            manifest["environment"]["architecture"] = "wrong-architecture"
            with self.assertRaisesRegex(ValueError, "runtime, OS, or architecture"):
                runner.verify(manifest, candidates, "calibration", seed_file, dotnet)

    def test_rejects_output_inside_source_and_missing_artifacts(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seed_file, manifest, dotnet = fixture(root)
            with self.assertRaisesRegex(ValueError, "outside the source tree"):
                runner.run(manifest, candidates, Path(candidates["source"]) / "runs", cohort_role="calibration",
                           seed_file=seed_file, manifest_sha256=sha(manifest), dotnet=dotnet,
                           preflight_only=True)
            Path(candidates["sim"]).unlink()
            with self.assertRaisesRegex(ValueError, "adjacent Release Sim DLL is missing"):
                runner.verify(json.loads(manifest.read_text()), candidates, "calibration", seed_file, dotnet)


class RunnerExecutionTests(unittest.TestCase):
    def test_paired_command_terminal_and_no_exposure_accounting(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seed_file, manifest, dotnet = fixture(root)
            seeds = json.loads(manifest.read_text())["seeds"]
            commands = []
            fake_run = command_runner(dotnet, seeds)

            def recording_run(command, **kwargs):
                if "fief-match" in command:
                    commands.append(command)
                return fake_run(command, **kwargs)

            output = root / "run"
            with patch.object(runner.subprocess, "run", side_effect=recording_run):
                result = runner.run(manifest, candidates, output, cohort_role="calibration",
                                    seed_file=seed_file, manifest_sha256=sha(manifest), dotnet=dotnet)
            self.assertEqual(result["status"], "complete")
            self.assertEqual(len(commands), 20)
            self.assertIn(runner.TRACE_FLAG, commands[0])
            self.assertEqual(commands[0][commands[0].index("--minutes") + 1], "130")
            self.assertTrue(all(entry["terminal_status"] == "horizon_censored" for entry in result["seeds"]))
            self.assertTrue(all(entry["no_exposure"] for entry in result["seeds"]))
            self.assertEqual(sum(entry["eligible_pairs"] for entry in result["seeds"]), 0)
            self.assertEqual(json.loads((output / "runner.json").read_text())["status"], "complete")

    def test_counts_eligible_excluded_and_censored_rows(self):
        output = "\n".join([
            "wp7-match-terminal seed=7 season=520 matchStatus=horizon_censored endReason=horizon",
            "wp7-gov11-city-year-pair seed=7",
            "wp7-gov11-city-year-pair-excluded seed=7 exclusionReason=lost",
            "wp7-gov11-city-year-pair-censored seed=7 observedSeasons=2",
        ])
        result = runner.parse_output(output, 7)
        self.assertFalse(result["no_exposure"])
        self.assertEqual(result["eligible_pairs"], 1)
        self.assertEqual(result["excluded_pairs"], 1)
        self.assertEqual(result["censored_pairs"], 1)

    def test_fails_closed_on_missing_duplicate_or_wrong_terminal(self):
        valid = "wp7-match-terminal seed=7 season=520 matchStatus=horizon_censored endReason=horizon\n"
        for raw, message in (("", "exactly one terminal row"),
                             (valid + valid, "exactly one terminal row"),
                             (valid.replace("seed=7", "seed=8"), "does not match manifest seed"),
                             (valid.replace("horizon_censored", "unknown"), "unrecognized matchStatus")):
            with self.subTest(raw=raw), self.assertRaisesRegex(ValueError, message):
                runner.parse_output(raw, 7)

    def test_stops_and_preserves_accounting_if_source_changes_during_seed(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seed_file, manifest, dotnet = fixture(root)
            seeds = json.loads(manifest.read_text())["seeds"]
            source_file = Path(candidates["source"]) / "src/sim.cs"
            output = root / "drift"
            with patch.object(runner.subprocess, "run",
                              side_effect=command_runner(dotnet, seeds, mutate_after=source_file)):
                with self.assertRaisesRegex(ValueError, "clean under content"):
                    runner.run(manifest, candidates, output, cohort_role="calibration",
                               seed_file=seed_file, manifest_sha256=sha(manifest), dotnet=dotnet)
            metadata = json.loads((output / "runner.json").read_text())
            self.assertEqual(metadata["status"], "failed")
            self.assertEqual(metadata["seeds"][0]["status"], "error")
            self.assertTrue(metadata["seeds"][0]["no_exposure"])
            self.assertTrue((output / f"seed-{seeds[0]}.log").is_file())

    def test_stops_if_binary_changes_during_seed(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            candidates, seed_file, manifest, dotnet = fixture(root)
            seeds = json.loads(manifest.read_text())["seeds"]
            sim_file = Path(candidates["sim"])
            output = root / "binary-drift"
            with patch.object(runner.subprocess, "run",
                              side_effect=command_runner(dotnet, seeds, mutate_after=sim_file)):
                with self.assertRaisesRegex(ValueError, "sim_sha256 mismatch"):
                    runner.run(manifest, candidates, output, cohort_role="calibration",
                               seed_file=seed_file, manifest_sha256=sha(manifest), dotnet=dotnet)
            metadata = json.loads((output / "runner.json").read_text())
            self.assertEqual(metadata["status"], "failed")
            self.assertEqual(metadata["seeds"][0]["status"], "error")

    def test_missing_terminal_or_wrong_seed_fails_and_keeps_log(self):
        for options, message in (({"omit_terminal": True}, "exactly one terminal row"),
                                 ({"wrong_seed": True}, "does not match manifest seed")):
            with self.subTest(options=options), tempfile.TemporaryDirectory() as td:
                root = Path(td)
                candidates, seed_file, manifest, dotnet = fixture(root)
                seeds = json.loads(manifest.read_text())["seeds"]
                output = root / "bad-output"
                with patch.object(runner.subprocess, "run",
                                  side_effect=command_runner(dotnet, seeds, **options)):
                    with self.assertRaisesRegex(ValueError, message):
                        runner.run(manifest, candidates, output, cohort_role="calibration",
                                   seed_file=seed_file, manifest_sha256=sha(manifest), dotnet=dotnet)
                metadata = json.loads((output / "runner.json").read_text())
                self.assertEqual(metadata["seeds"][0]["status"], "error")
                self.assertTrue((output / f"seed-{seeds[0]}.log").is_file())


if __name__ == "__main__":
    unittest.main()
