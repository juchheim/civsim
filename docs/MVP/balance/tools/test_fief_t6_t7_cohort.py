"""Small pure-Python sanity checks; these never invoke the harness or start matches."""

from __future__ import annotations

import sys
import unittest
import hashlib
import json
import tempfile
from types import SimpleNamespace
from pathlib import Path
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))

import analyze_fief_t6_t7_cohort as analysis  # noqa: E402
import run_fief_t6_t7_cohort as runner  # noqa: E402


class FrozenCohortScriptTests(unittest.TestCase):
    def test_combined_heldout_accepts_eighty_only_with_full_flag_set(self) -> None:
        flags = ["--fief-power-ledger", "--fief-health-trace", "--fief-institution-trace",
                 "--fief-wp7-paired", "--fief-sea10-trace", "--fief-territory-trace",
                 "--fief-siege-trace", "--fief-siege-strategist-trace"]
        metadata = {"combinedNaturalTraceCohort": True, "traceFlags": flags}
        self.assertTrue(analysis.validate_cohort_protocol(metadata, 80))
        metadata["traceFlags"] = flags[:-1]
        with self.assertRaisesRegex(ValueError, "missing a required trace flag"):
            analysis.validate_cohort_protocol(metadata, 80)

    def test_manifest_is_the_frozen_twenty_seed_list(self) -> None:
        manifest = runner.DEFAULT_MANIFEST
        self.assertEqual(runner.EXPECTED_MANIFEST_SHA256, runner.sha256(manifest))
        seeds = runner.load_manifest(manifest)
        self.assertEqual(20, len(seeds))
        self.assertEqual(sorted(seeds), seeds)
        self.assertEqual(len(seeds), len(set(seeds)))

    def test_test_assembly_is_required_and_hash_mismatch_fails_before_run(self) -> None:
        with tempfile.TemporaryDirectory() as temp_name:
            root = Path(temp_name)
            pack, harness, sim = root / "numeric.pack", root / "harness.dll", root / "sim.dll"
            manifest, test_assembly = root / "seeds.txt", root / "CivSim.Tests.dll"
            pack.write_bytes(b"frozen pack")
            harness.write_bytes(b"frozen harness")
            sim.write_bytes(b"frozen sim")
            manifest.write_bytes(runner.DEFAULT_MANIFEST.read_bytes())
            args = SimpleNamespace(
                pack=pack, harness_dll=harness, sim_dll=sim, test_assembly=test_assembly,
                manifest=manifest, output_dir=root / "output", expected_head="unused",
                expected_source_aggregate="unused", expected_pack_sha256="unused",
                expected_harness_sha256="unused", expected_sim_sha256="unused",
                expected_test_assembly_sha256="unused", expected_manifest_sha256=runner.sha256(manifest),
                dotnet="dotnet", minutes=130, cohort_role="calibration", resume=False,
            )
            with patch.object(runner.subprocess, "run") as invoke:
                with self.assertRaisesRegex(FileNotFoundError, "test assembly does not exist"):
                    runner.run(args)
                test_assembly.write_bytes(b"unexpected test assembly")
                with self.assertRaisesRegex(ValueError, "test assembly SHA-256 mismatch"):
                    runner.run(args)
            self.assertFalse(args.output_dir.exists())
            invoke.assert_not_called()

    def test_interrupted_cohort_resume_verifies_prefix_and_reruns_only_incomplete_seeds(self) -> None:
        seeds = runner.load_manifest(runner.DEFAULT_MANIFEST)
        with tempfile.TemporaryDirectory() as temp_name:
            root = Path(temp_name)
            pack, harness, sim = root / "numeric.pack", root / "harness.dll", root / "sim.dll"
            test_assembly = root / "CivSim.Tests.dll"
            manifest = root / "seeds.txt"
            pack.write_bytes(b"frozen pack")
            harness.write_bytes(b"frozen harness")
            sim.write_bytes(b"frozen sim")
            test_assembly.write_bytes(b"frozen test assembly")
            provenance = runner.capture(Path(__file__).resolve().parents[4], pack, harness, sim)
            manifest.write_bytes(runner.DEFAULT_MANIFEST.read_bytes())
            output_dir = root / "calibration"
            output_dir.mkdir()
            completed_runs = []
            for seed in seeds[:17]:
                raw = output_dir / f"seed-{seed}.txt"
                raw.write_text(f"verified result for {seed}\n", encoding="utf-8")
                completed_runs.append({
                    "seed": seed,
                    "command": ["dotnet", str(harness.resolve()), "fief-match", "--pack", str(pack.resolve()),
                                "--seed", str(seed), "--minutes", "130", "--fief-health-trace",
                                "--fief-institution-trace"],
                    "exitCode": 0,
                    "rawFile": raw.name,
                    "rawSha256": runner.sha256(raw),
                })
            (output_dir / f"seed-{seeds[17]}.txt").write_text("", encoding="utf-8")
            command_template = ["dotnet", str(harness.resolve()), "fief-match", "--pack", str(pack.resolve()),
                                "--seed", "<manifest-seed>", "--minutes", "130", "--fief-health-trace",
                                "--fief-institution-trace"]
            metadata = {
                "status": "running",
                "workingDirectory": str(Path.cwd()),
                "seedCount": len(seeds),
                "cohortRole": "calibration",
                "seeds": seeds,
                "minutes": 130,
                "packPath": str(pack.resolve()),
                "packSha256": runner.sha256(pack),
                "harnessDllPath": str(harness.resolve()),
                "harnessDllSha256": runner.sha256(harness),
                "simDllPath": str(sim.resolve()), "simDllSha256": runner.sha256(sim),
                "testAssemblyPath": str(test_assembly.resolve()),
                "testAssemblySha256": runner.sha256(test_assembly),
                "headCommit": provenance["headCommit"],
                "sourceAggregateSha256": provenance["sourceAggregateSha256"],
                "manifestPath": str(manifest.resolve()),
                "manifestSha256": runner.sha256(manifest),
                "commandTemplate": command_template,
                "runs": completed_runs,
            }
            (output_dir / "run_metadata.json").write_text(json.dumps(metadata), encoding="utf-8")
            args = SimpleNamespace(
                pack=pack, harness_dll=harness, sim_dll=sim, test_assembly=test_assembly,
                manifest=manifest, output_dir=output_dir,
                expected_head=provenance["headCommit"], expected_source_aggregate=provenance["sourceAggregateSha256"],
                expected_pack_sha256=provenance["packSha256"], expected_harness_sha256=provenance["harnessDllSha256"],
                expected_sim_sha256=provenance["simDllSha256"], expected_manifest_sha256=runner.sha256(manifest),
                expected_test_assembly_sha256=runner.sha256(test_assembly),
                dotnet="dotnet", minutes=130, cohort_role="calibration", resume=True,
            )
            expected_run_provenance = runner.capture_run_provenance(
                Path(__file__).resolve().parents[4], args, runner.sha256(manifest))
            expected_run_provenance["dotnetVersion"] = "10.0.100"
            (output_dir / "provenance.json").write_text(json.dumps(expected_run_provenance), encoding="utf-8")

            def fake_harness(_command: list[str], *, stdout=None, stderr=None, check: bool = False,
                             text: bool = False, capture_output: bool = False) -> SimpleNamespace:
                if _command == ["dotnet", "--version"]:
                    return SimpleNamespace(returncode=0, stdout="10.0.100\n")
                stdout.write("synthetic resumed output\n")
                return SimpleNamespace(returncode=0)

            with patch.object(runner.subprocess, "run", side_effect=fake_harness) as invoke:
                self.assertEqual(0, runner.run(args))

            self.assertEqual(5, invoke.call_count)  # initial/final runtime pins plus three unfinished seeds
            completed = json.loads((output_dir / "run_metadata.json").read_text(encoding="utf-8"))
            provenance = json.loads((output_dir / "provenance.json").read_text(encoding="utf-8"))
            self.assertEqual("calibration", provenance["cohortRole"])
            self.assertEqual("10.0.100", provenance["dotnetVersion"])
            self.assertEqual(str(test_assembly.resolve()), provenance["testAssemblyPath"])
            self.assertEqual(runner.sha256(test_assembly), provenance["testAssemblySha256"])
            self.assertIn("FiefQuarantineTests.cs", " ".join(provenance["testSources"]))
            self.assertEqual("complete", completed["status"])
            self.assertEqual(20, len(completed["runs"]))
            self.assertEqual(17, completed["resumeEvents"][0]["verifiedCompletedSeeds"])
            self.assertEqual(seeds[17], completed["resumeEvents"][0]["firstSeedToRun"])
            for seed in seeds[17:]:
                self.assertGreater((output_dir / f"seed-{seed}.txt").stat().st_size, 0)

    def test_interrupted_cohort_resume_refuses_changed_completed_output(self) -> None:
        seeds = runner.load_manifest(runner.DEFAULT_MANIFEST)
        with tempfile.TemporaryDirectory() as temp_name:
            root = Path(temp_name)
            pack, harness, sim = root / "numeric.pack", root / "harness.dll", root / "sim.dll"
            test_assembly = root / "CivSim.Tests.dll"
            manifest = root / "seeds.txt"
            pack.write_bytes(b"frozen pack")
            harness.write_bytes(b"frozen harness")
            sim.write_bytes(b"frozen sim")
            test_assembly.write_bytes(b"frozen test assembly")
            provenance = runner.capture(Path(__file__).resolve().parents[4], pack, harness, sim)
            manifest.write_bytes(runner.DEFAULT_MANIFEST.read_bytes())
            output_dir = root / "calibration"
            output_dir.mkdir()
            first_raw = output_dir / f"seed-{seeds[0]}.txt"
            first_raw.write_text("tampered\n", encoding="utf-8")
            metadata = {
                "status": "running", "workingDirectory": str(Path.cwd()), "cohortRole": "calibration",
                "seedCount": len(seeds), "seeds": seeds, "minutes": 130,
                "packPath": str(pack.resolve()), "packSha256": runner.sha256(pack),
                "harnessDllPath": str(harness.resolve()), "harnessDllSha256": runner.sha256(harness),
                "simDllPath": str(sim.resolve()), "simDllSha256": runner.sha256(sim),
                "testAssemblyPath": str(test_assembly.resolve()),
                "testAssemblySha256": runner.sha256(test_assembly),
                "headCommit": provenance["headCommit"],
                "sourceAggregateSha256": provenance["sourceAggregateSha256"],
                "manifestPath": str(manifest.resolve()), "manifestSha256": runner.sha256(manifest),
                "commandTemplate": ["dotnet", str(harness.resolve()), "fief-match", "--pack", str(pack.resolve()),
                                    "--seed", "<manifest-seed>", "--minutes", "130", "--fief-health-trace",
                                    "--fief-institution-trace"],
                "runs": [{"seed": seeds[0],
                          "command": ["dotnet", str(harness.resolve()), "fief-match", "--pack", str(pack.resolve()),
                                      "--seed", str(seeds[0]), "--minutes", "130", "--fief-health-trace",
                                      "--fief-institution-trace"],
                          "exitCode": 0, "rawFile": first_raw.name,
                          "rawSha256": "0" * 64}],
            }
            (output_dir / "run_metadata.json").write_text(json.dumps(metadata), encoding="utf-8")
            args = SimpleNamespace(
                pack=pack, harness_dll=harness, sim_dll=sim, test_assembly=test_assembly,
                manifest=manifest, output_dir=output_dir,
                expected_head=provenance["headCommit"], expected_source_aggregate=provenance["sourceAggregateSha256"],
                expected_pack_sha256=provenance["packSha256"], expected_harness_sha256=provenance["harnessDllSha256"],
                expected_sim_sha256=provenance["simDllSha256"], expected_manifest_sha256=runner.sha256(manifest),
                expected_test_assembly_sha256=runner.sha256(test_assembly),
                dotnet="dotnet", minutes=130, cohort_role="calibration", resume=True,
            )
            (output_dir / "provenance.json").write_text(json.dumps(
                runner.capture_run_provenance(Path(__file__).resolve().parents[4], args, runner.sha256(manifest))),
                encoding="utf-8")
            with patch.object(runner.subprocess, "run") as invoke:
                with self.assertRaisesRegex(ValueError, "completed raw output.*hash mismatch"):
                    runner.run(args)
            self.assertEqual(["dotnet", "--version"], invoke.call_args.args[0])

    def test_seeded_bootstrap_is_repeatable_and_bounded_by_seed_shares(self) -> None:
        seed_hazards = {11: (1.0, 10.0), 12: (3.0, 10.0), 13: (4.0, 10.0)}
        first = analysis.seeded_bootstrap_ratio(seed_hazards, replicates=500, bootstrap_seed=17)
        second = analysis.seeded_bootstrap_ratio(seed_hazards, replicates=500, bootstrap_seed=17)
        self.assertEqual(first, second)
        assert first is not None
        self.assertGreaterEqual(first[0], 0.1)
        self.assertLessEqual(first[1], 0.4)

    def test_key_value_parser_preserves_reason_tokens(self) -> None:
        row = analysis.fields("fief-institution-trace season=3 civ=1 state=queued reason=buildStatus_Short;gate=Materials/None;project=NRG-05")
        self.assertEqual("3", row["season"])
        self.assertEqual("queued", row["state"])
        self.assertEqual("buildStatus_Short;gate=Materials/None;project=NRG-05", row["reason"])

    def test_analysis_template_fills_from_synthetic_rows_without_running_harness(self) -> None:
        seeds = runner.load_manifest(runner.DEFAULT_MANIFEST)
        with tempfile.TemporaryDirectory() as temp_name:
            root = Path(temp_name)
            raw_dir = root / "raw"
            raw_dir.mkdir()
            runs = []
            for seed in seeds:
                raw = raw_dir / f"seed-{seed}.txt"
                raw.write_text(
                    f"fief-health-trace seed={seed} season=1 civ=0 ownKey5Season=1 seasonOffsetFromOwnKey5=0 "
                    "adultEquivalentPersonSeasons=100 expectedEpidemic=0.100000 expectedTotal=1.000000 "
                    "observedEpidemicDeaths=1 observedPoxDeaths=1 observedPlagueDeaths=0 observedTotalDeaths=2 "
                    "quarantine=true importedOutbreakAttempts=2 importedOutbreakBlocked=1 importedOutbreakSuccesses=1\n"
                    "fief-institution-trace season=1 civ=0 settlement=0 institution=monastery state=working builtCount=1 reason=monks_present=4\n"
                    "fief-institution-trace season=1 civ=0 settlement=0 institution=university state=dormant builtCount=1 reason=no_resident_scholars\n"
                    "fief-institution-trace season=1 civ=0 settlement=0 institution=hospital state=working builtCount=1 reason=physicians=1;attendants=2\n"
                    "fief-institution-trace season=1 civ=0 settlement=0 institution=gothic_cathedral state=working builtCount=1 reason=built_passive_wonder_no_postbuild_staff_role\n"
                    "fief-institution-trace season=1 civ=0 settlement=0 institution=clock_tower state=working builtCount=1 reason=built_tower_includes_its_own_settlement_in_reach\n"
                    "fief-institution-trace season=1 civ=0 settlement=0 institution=windmill state=queued builtCount=0 reason=buildStatus_Waiting;gate=Ahead/None;project=none;shortages=none\n"
                    "fief-institution-summary season=1 civ=0 workingStaffed=2 dormantStaffed=1 unresolvedStaffing=0 dormantRatePercent=33.33 denominator=3\n",
                    encoding="utf-8",
                )
                runs.append({"seed": seed, "rawFile": raw.name, "exitCode": 0,
                             "rawSha256": hashlib.sha256(raw.read_bytes()).hexdigest()})
            metadata = {
                "status": "complete", "manifestSha256": runner.EXPECTED_MANIFEST_SHA256, "seeds": seeds,
                "packSha256": "packhash", "harnessDllSha256": "dllhash", "runs": runs,
            }
            (raw_dir / "run_metadata.json").write_text(json.dumps(metadata), encoding="utf-8")
            output = root / "report.md"
            result = analysis.analyze(raw_dir, runner.DEFAULT_MANIFEST, analysis.DEFAULT_TEMPLATE, output,
                                      replicates=100, bootstrap_seed=17)
            report = output.read_text(encoding="utf-8")
            self.assertAlmostEqual(0.1, result["expectedShare"])
            self.assertAlmostEqual(1 / 3, result["dormantRate"])
            self.assertEqual(0, result["summaryMismatches"])
            self.assertEqual("clock_tower, gothic_cathedral, hospital, mill, monastery, university", result["institutionTypes"])
            self.assertEqual("PASS", result["institutionCoverageStatus"])
            self.assertIn("active Pox / Plague: 20 / 0", report)
            self.assertIn("valid route-import attempts / keyed blocks / accepted imports: 40 / 20 / 20", report)
            self.assertNotIn("{{", report)
            self.assertIn("10.00%", report)
            self.assertIn("0.010000", report)

    def test_analysis_rejects_cohort_missing_an_institution_type(self) -> None:
        seeds = runner.load_manifest(runner.DEFAULT_MANIFEST)
        with tempfile.TemporaryDirectory() as temp_name:
            root = Path(temp_name)
            raw_dir = root / "raw"
            raw_dir.mkdir()
            runs = []
            for seed in seeds:
                raw = raw_dir / f"seed-{seed}.txt"
                rows = [
                    "fief-health-trace season=1 civ=0 ownKey5Season=1 seasonOffsetFromOwnKey5=0 adultEquivalentPersonSeasons=100 expectedEpidemic=0.1 expectedTotal=1 observedEpidemicDeaths=1 observedTotalDeaths=1",
                    "fief-institution-trace season=1 civ=0 settlement=0 institution=monastery state=working builtCount=1 reason=monks_present=4",
                    "fief-institution-summary season=1 civ=0 workingStaffed=1 dormantStaffed=0 unresolvedStaffing=0 dormantRatePercent=0.00 denominator=1",
                ]
                raw.write_text("\n".join(rows) + "\n", encoding="utf-8")
                runs.append({"seed": seed, "rawFile": raw.name, "exitCode": 0,
                             "rawSha256": hashlib.sha256(raw.read_bytes()).hexdigest()})
            (raw_dir / "run_metadata.json").write_text(json.dumps({
                "status": "complete", "manifestSha256": runner.EXPECTED_MANIFEST_SHA256, "seeds": seeds,
                "packSha256": "packhash", "harnessDllSha256": "dllhash", "runs": runs,
            }), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "all six institution types"):
                analysis.analyze(raw_dir, runner.DEFAULT_MANIFEST, analysis.DEFAULT_TEMPLATE, root / "out.md",
                                 replicates=10, bootstrap_seed=17)


if __name__ == "__main__":
    unittest.main()
