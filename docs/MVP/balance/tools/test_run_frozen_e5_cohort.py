from __future__ import annotations

import argparse
import csv
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parent))
import run_frozen_e5_cohort as runner


TOOLS = Path(__file__).parent
NATURAL = TOOLS.parent / "m12-wp11-fief-natural-seeds-2026-11-15.txt"
GRANTED = TOOLS.parent / "m12-wp11-fief-grant-e5-seeds-2026-11-15.txt"


class FrozenE5CohortCommandTests(unittest.TestCase):
    def _command(self, root: Path, manifest: Path, granted: bool) -> list[str]:
        pack, harness = root / "pack", root / "Harness.dll"
        flags = ["--grant-e5"] if granted else []
        return ["dotnet", str(harness), "fief-perf", "--pack", str(pack),
                "--seeds-file", str(manifest), "--minutes", "130",
                "--warmup-minutes", "5", "--output-prefix", str(root / "out" / "perf"), *flags]

    def test_only_the_exact_two_manifests_and_matching_grant_modes_are_accepted(self) -> None:
        self.assertEqual(20, len(runner.read_seed_manifest(NATURAL, runner.NATURAL_MANIFEST_SHA256)))
        granted_seeds = runner.read_seed_manifest(GRANTED, runner.GRANT_MANIFEST_SHA256)
        self.assertEqual(20, len(granted_seeds))
        self.assertFalse(set(runner.read_seed_manifest(NATURAL, runner.NATURAL_MANIFEST_SHA256)) & set(granted_seeds))
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            pack, harness = root / "pack", root / "Harness.dll"
            manifest = NATURAL.resolve()
            natural = self._command(root, manifest, False)
            granted = self._command(root, manifest, True)
            with patch.object(runner.shutil, "which", return_value="/usr/bin/dotnet"):
                spec = runner.validate_cohort_command(natural, root, pack.resolve(), harness.resolve(), manifest,
                                                       (root / "out").resolve(), runner.NATURAL_MANIFEST_SHA256)
                self.assertEqual("natural", spec["role"])
                with self.assertRaisesRegex(ValueError, "natural cohort requires"):
                    runner.validate_cohort_command(granted, root, pack.resolve(), harness.resolve(), manifest,
                                                   (root / "out").resolve(), runner.NATURAL_MANIFEST_SHA256)
                stress = self._command(root, GRANTED.resolve(), True)
                stress[stress.index("--pack") + 1] = str(pack.resolve())
                stress[stress.index("--output-prefix") + 1] = str((root / "out" / "perf").resolve())
                stress_spec = runner.validate_cohort_command(
                    stress, root, pack.resolve(), harness.resolve(), GRANTED.resolve(),
                    (root / "out").resolve(), runner.GRANT_MANIFEST_SHA256)
                self.assertEqual("grant-e5", stress_spec["role"])

    def test_stale_manifest_hash_wrong_block_and_duplicates_fail_closed(self) -> None:
        with self.assertRaisesRegex(ValueError, "not one of the preregistered"):
            runner.read_seed_manifest(NATURAL, "0" * 64)
        with tempfile.TemporaryDirectory() as temporary:
            altered = Path(temporary) / "seeds.txt"
            altered.write_text("# altered\n" + "\n".join(str(i) for i in range(20)) + "\n", encoding="ascii")
            with self.assertRaisesRegex(ValueError, "manifest SHA-256 mismatch"):
                runner.read_seed_manifest(altered, runner.NATURAL_MANIFEST_SHA256)
            duplicate = Path(temporary) / "duplicate.txt"
            duplicate.write_text("\n".join(["7"] * 20) + "\n", encoding="ascii")
            with patch.object(runner, "sha256", return_value=runner.NATURAL_MANIFEST_SHA256):
                with self.assertRaisesRegex(ValueError, "exactly 20 unique"):
                    runner.read_seed_manifest(duplicate, runner.NATURAL_MANIFEST_SHA256)

    def test_runner_records_each_seed_command_exit_hash_timing_and_provenance(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            repo = root / "repo"
            repo.mkdir()
            pack, harness, sim = root / "pack", root / "Harness.dll", root / "CivSim.Sim.dll"
            for path in (pack, harness, sim):
                path.write_bytes(path.name.encode())
            out = root / "out"
            natural_manifest = NATURAL.resolve()
            command = self._command(root, natural_manifest, False)
            snapshot = {"headCommit": "a" * 40, "headTree": "b" * 40,
                        "sourceAggregateSha256": "c" * 64,
                        "packSha256": hashlib.sha256(pack.read_bytes()).hexdigest(),
                        "harnessDllSha256": hashlib.sha256(harness.read_bytes()).hexdigest(),
                        "simDllSha256": hashlib.sha256(sim.read_bytes()).hexdigest()}
            runner_path = Path(runner.__file__).resolve()
            helper_path = Path(runner.__file__).with_name("cohort_provenance.py").resolve()
            args = argparse.Namespace(
                repo=repo, pack=pack, harness_dll=harness, sim_dll=sim,
                manifest=natural_manifest, output_dir=out,
                expected_head="a" * 40, expected_tree="b" * 40,
                expected_source_aggregate="c" * 64,
                expected_pack_sha256=snapshot["packSha256"],
                expected_harness_sha256=snapshot["harnessDllSha256"],
                expected_sim_sha256=snapshot["simDllSha256"],
                expected_manifest_sha256=runner.NATURAL_MANIFEST_SHA256,
                expected_runner_sha256=runner.sha256(runner_path),
                expected_provenance_helper_sha256=runner.sha256(helper_path),
                command=command)

            def fake_run(actual_command: list[str], **kwargs: object) -> argparse.Namespace:
                stdout = kwargs["stdout"]
                stdout.write(b"simulated output\n")
                prefix = Path(actual_command[actual_command.index("--output-prefix") + 1])
                prefix.parent.mkdir(parents=True, exist_ok=True)
                seed = Path(actual_command[actual_command.index("--seeds-file") + 1]).read_text().strip()
                prefix.with_name(prefix.name + "-ticks.csv").write_text(
                    f"seed,timed_ticks,stop_reason,censored,final_tick,mean_ms,p95_ms,gc_pause_ms_per_tick\n{seed},75000,horizon,0,78000,1.0,2.0,0.1\n", encoding="utf-8")
                prefix.with_name(prefix.name + "-seats.csv").write_text(f"seed,civ\n{seed},0\n", encoding="utf-8")
                prefix.with_name(prefix.name + "-tick-samples.csv").write_text(f"seed,tick\n{seed},1\n", encoding="utf-8")
                prefix.with_name(prefix.name + "-provenance.txt").write_text("fixture\n", encoding="utf-8")
                return argparse.Namespace(returncode=0)

            with patch.object(runner, "capture_candidate", return_value=snapshot), \
                 patch.object(runner.subprocess, "run", side_effect=fake_run), \
                 patch.object(runner.shutil, "which", return_value="/usr/bin/dotnet"):
                self.assertEqual(0, runner.run(args))
            metadata = json.loads((out / "wrapper_metadata.json").read_text(encoding="utf-8"))
            self.assertEqual("complete", metadata["status"])
            self.assertEqual(20, len(metadata["seeds"]))
            for seed in metadata["seeds"]:
                self.assertEqual("complete", seed["status"])
                self.assertIn("fief-perf", seed["command"])
                self.assertEqual(0, seed["exitCode"])
                self.assertEqual(64, len(seed["rawLogSha256"]))
                self.assertGreaterEqual(seed["elapsedSeconds"], 0)
                self.assertEqual(snapshot, seed["provenanceBefore"])
                self.assertEqual(snapshot, seed["provenanceAfter"])
                self.assertEqual("1.0", seed["performance"]["mean_ms"])
                self.assertTrue(seed["artifactsSha256"])

    def test_existing_root_is_refused(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            pack, harness, sim = root / "pack", root / "Harness.dll", root / "CivSim.Sim.dll"
            for path in (pack, harness, sim):
                path.write_bytes(b"fixture")
            out = root / "already-there"
            out.mkdir()
            args = argparse.Namespace(repo=root, pack=pack, harness_dll=harness, sim_dll=sim,
                manifest=NATURAL, output_dir=out, expected_head="a" * 40, expected_tree="b" * 40,
                expected_source_aggregate="c" * 64, expected_pack_sha256="d" * 64,
                expected_harness_sha256="e" * 64, expected_sim_sha256="f" * 64,
                expected_manifest_sha256=runner.NATURAL_MANIFEST_SHA256,
                expected_runner_sha256="1" * 64, expected_provenance_helper_sha256="2" * 64,
                command=[])
            with self.assertRaisesRegex(FileExistsError, "refusing to overwrite"):
                runner.run(args)

    def test_output_inside_repository_is_refused_before_creation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "repo"
            root.mkdir()
            output = root / "cohort-output"
            with self.assertRaisesRegex(ValueError, "outside the repository"):
                runner._fresh_external_output(output, root)
            self.assertFalse(output.exists())

    def test_broken_symlink_output_is_refused(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "repo"
            root.mkdir()
            output = Path(temporary) / "cohort-output"
            output.symlink_to(Path(temporary) / "missing-target", target_is_directory=True)
            with self.assertRaisesRegex(FileExistsError, "refusing to overwrite cohort output path"):
                runner._fresh_external_output(output, root)



if __name__ == "__main__":
    unittest.main()
