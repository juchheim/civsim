import hashlib, importlib.util, json, tempfile, unittest, subprocess, sys
import signal
from pathlib import Path
from unittest.mock import patch

MODULE = Path(__file__).resolve().parents[2] / "tools" / "fief_trace_runner.py"
spec = importlib.util.spec_from_file_location("wp8runner", MODULE)
runner = importlib.util.module_from_spec(spec); spec.loader.exec_module(runner)

def put(path: Path, data: bytes) -> str:
    path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
    return hashlib.sha256(data).hexdigest()

class FiefTraceRunnerTests(unittest.TestCase):
    def fixture(self, root):
        source = root / "source"; files = {"content/a.yml": b"content", "content/registries/eras.yaml": b"- {id: 5, name: E5, base_cost: 6000, }\n", "src/x.cs": b"source", "game/project.godot": b"game"}
        file_hashes = {rel: put(source / rel, data) for rel, data in files.items()}
        (source / ".gitignore").write_text("*.import\n")
        subprocess.run(["git", "init", "-q", str(source)], check=True)
        subprocess.run(["git", "-C", str(source), "add", "."], check=True)
        subprocess.run(["git", "-C", str(source), "-c", "user.name=Fixture", "-c",
                        "user.email=fixture@example.invalid", "commit", "-qm", "fixture"], check=True)
        records = [{"path": rel, "status": "clean", "sha256": digest} for rel, digest in sorted(file_hashes.items())]
        payload = {"head": "f" * 40, "files": records}
        aggregate = hashlib.sha256(json.dumps(payload, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
        source_manifest = root / "source-files.json"
        source_manifest.write_text(json.dumps({**payload, "aggregate_sha256": aggregate}))
        pack = root / "pack"; harness = root / "bin" / "CivSim.Harness.dll"; sim = harness.parent / "CivSim.Sim.dll"
        pack_bytes=json.dumps({"registries":{"eras":[{"id":5,"base_cost":6000}]}}).encode()
        ph, hh, sh = put(pack, pack_bytes), put(harness, b"harness"), put(sim, b"sim")
        manifest = root / "provenance.json"
        manifest.write_text(json.dumps({"source_tree_sha256": runner.source_tree_hash(source, {**payload, "aggregate_sha256": aggregate}),
            "pack_sha256": ph, "harness_sha256": hh, "sim_sha256": sh,
            "source_manifest_sha256": runner.sha(source_manifest), "source_manifest": source_manifest.name}))
        return source, pack, harness, sim, manifest

    def test_ignored_import_sidecar_does_not_change_source_identity(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d); source, pack, harness, sim, manifest=self.fixture(root)
            original=json.loads(manifest.read_text())["source_tree_sha256"]
            put(source / "game/scenes/world.tscn.import", b"generated sidecar")
            source_manifest=json.loads((root/"source-files.json").read_text())
            self.assertEqual(original, runner.source_tree_hash(source, source_manifest))

    def test_missing_binary_or_hash_mismatch_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d); source, pack, harness, sim, manifest=self.fixture(root)
            put(sim, b"wrong")
            with self.assertRaisesRegex(ValueError, "Sim missing or hash mismatch"):
                runner.validate(manifest, source, pack, harness, sim)

    def test_source_mutation_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d); source, pack, harness, sim, manifest=self.fixture(root)
            put(source / "src/x.cs", b"modified")
            with self.assertRaisesRegex(ValueError, "source file missing or hash mismatch"):
                runner.validate(manifest, source, pack, harness, sim)


    def test_runner_selects_each_report_only_trace(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d); source, pack, harness, sim, manifest=self.fixture(root)
            seeds=root/"seeds"; seeds.write_text("102\n")
            class Args: pass
            for kind, flag in (("sea10", "--fief-sea10-trace"), ("siege", "--fief-siege-trace")):
                a=Args(); a.provenance=manifest; a.source_root=source; a.pack=pack; a.harness=harness; a.sim=sim
                a.seeds=seeds; a.output=root/kind; a.minutes=130; a.trace_kind=kind
                def fake_process(cmd, stdout):
                    stdout.write(b"mock match output\n")
                    return 0
                with patch.object(runner, "run_frozen_command", side_effect=fake_process) as call:
                    self.assertEqual(0, runner.run(a))
                self.assertIn(flag, call.call_args.args[0])

    def test_runner_combines_selected_traces_and_freezes_provenance(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d); source, pack, harness, sim, manifest=self.fixture(root)
            seeds=root/"seeds"; seeds.write_text("105\n")
            class Args: pass
            a=Args(); a.provenance=manifest; a.source_root=source; a.pack=pack; a.harness=harness; a.sim=sim
            a.seeds=seeds; a.output=root/"combined"; a.minutes=130
            # Argument order must not affect the deterministic command order.
            a.trace_kinds=["territory", "siege", "sea10"]
            expected=["--fief-sea10-trace", "--fief-territory-trace", "--fief-siege-trace"]
            def fake_process(cmd, stdout):
                stdout.write(b"mock match output\n")
                return 0
            with patch.object(runner, "run_frozen_command", side_effect=fake_process) as call:
                self.assertEqual(0, runner.run(a))
            command=call.call_args.args[0]
            self.assertEqual(expected, command[-3:])
            freeze=json.loads((a.output/"run-freeze.json").read_text())
            self.assertEqual(expected, freeze["traces"])
            self.assertIsNone(freeze["trace"])
            self.assertEqual(json.loads(manifest.read_text()), freeze["provenance"])
            self.assertEqual("combined", Path(freeze["output"]).name)
            self.assertTrue(a.output.is_dir())

    def test_combined_e5_cohort_uses_exact_trace_set_and_freezes_test_and_runner_pins(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d); source, pack, harness, sim, manifest=self.fixture(root)
            seeds=root/"seeds"; seeds.write_text("# frozen 80-seed held-out block\n" + "\n".join(str(seed) for seed in range(501,581)) + "\n")
            test_assembly=root/"CivSim.Tests.dll"; test_hash=put(test_assembly,b"release tests")
            class Args: pass
            a=Args(); a.provenance=manifest; a.source_root=source; a.pack=pack; a.harness=harness; a.sim=sim
            a.seeds=seeds; a.output=root/"combined"; a.minutes=130; a.combined_e5_traces=True
            a.test_assembly=test_assembly; a.expected_test_assembly_sha256=test_hash
            a.expected_manifest_sha256=runner.sha(seeds); a.expected_runner_sha256=runner.sha(MODULE)
            calibration=root/"calibration-seeds"; calibration.write_text("401\n402\n")
            a.calibration_seeds=calibration; a.expected_calibration_manifest_sha256=runner.sha(calibration)
            def fake_process(cmd, stdout):
                seed=cmd[cmd.index("--seed") + 1]
                progress="civ arch KEY-5\n" + "".join(f"c{civ} {arch} 30.0\n"
                                 for civ,arch in enumerate(("Builder","Grower","Warlord","Sage"))) + "".join(f"  c{civ} {arch}: 1 E5 projects, KEY-5→KEY-6 20.0 min from KEY-5 (at 50.0)\n"
                                 for civ,arch in enumerate(("Builder","Grower","Warlord","Sage")))
                stdout.write((f"== fief-match seed {seed}: 130.0 minutes simulated, chapter end None at season none (ended: False)\n"
                              f"wp7-match-terminal seed={seed} matchStatus=horizon_censored endReason=horizon season=2080\n"
                              + progress +
                              "F-T2 mill power ledger (12 seasons from first source; grain coverage is adult-equivalent capacity, not a separate flour-stock measure):\n"
                              "  completed town windows=0; censored=0; weighted grain coverage=n/a (target >=60%: INCOMPLETE): town windows meeting 60%=0/0\n").encode())
                return 0
            with patch.object(runner, "run_frozen_command", side_effect=fake_process) as call:
                self.assertEqual(0, runner.run(a))
            expected=["--fief-power-ledger", "--fief-health-trace", "--fief-institution-trace",
                      "--fief-wp7-paired", "--fief-sea10-trace", "--fief-territory-trace", "--fief-siege-trace",
                      "--fief-siege-strategist-trace"]
            command=call.call_args.args[0]
            self.assertEqual(expected, command[-len(expected):])
            freeze=json.loads((a.output/"run-freeze.json").read_text())
            self.assertEqual(expected, freeze["traces"])
            self.assertEqual(str(test_assembly.resolve()), freeze["test_assembly"])
            self.assertEqual(test_hash, freeze["test_assembly_sha256"])
            self.assertEqual(runner.sha(MODULE), freeze["runner_sha256"])
            metadata=json.loads((a.output/"run_metadata.json").read_text())
            self.assertEqual("complete", metadata["status"])
            self.assertTrue(metadata["combinedNaturalTraceCohort"])
            self.assertEqual(80, len(metadata["seeds"]))
            self.assertEqual(80, len(metadata["runs"]))
            self.assertEqual(test_hash, metadata["testAssemblySha256"])
            sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "docs" / "MVP" / "balance" / "tools"))
            import analyze_fief_ft1_calibration
            analyzed_seeds, validated=analyze_fief_ft1_calibration.validate_combined_run(
                a.output/"combined-output.txt", a.output/"run_metadata.json", seeds)
            self.assertEqual(80, len(analyzed_seeds))
            self.assertEqual(320, sum(len(match["seats"]) for match in validated["matches"]))
            self.assertTrue(all(run["wp7Accounting"]["no_exposure"] for run in metadata["runs"]))

    def test_combined_e5_cohort_fails_closed_on_test_manifest_or_runner_pin(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d); source, pack, harness, sim, manifest=self.fixture(root)
            seeds=root/"seeds"; seeds.write_text("\n".join(str(seed) for seed in range(502,582)) + "\n")
            test_assembly=root/"CivSim.Tests.dll"; test_hash=put(test_assembly,b"release tests")
            class Args: pass
            def args(output):
                a=Args(); a.provenance=manifest; a.source_root=source; a.pack=pack; a.harness=harness; a.sim=sim
                a.seeds=seeds; a.output=root/output; a.minutes=130; a.combined_e5_traces=True
                a.test_assembly=test_assembly; a.expected_test_assembly_sha256=test_hash
                a.expected_manifest_sha256=runner.sha(seeds); a.expected_runner_sha256=runner.sha(MODULE)
                calibration=root/"calibration-seeds"; calibration.write_text("401\n402\n")
                a.calibration_seeds=calibration; a.expected_calibration_manifest_sha256=runner.sha(calibration)
                return a
            with patch.object(runner, "run_frozen_command") as invoke:
                bad_test=args("bad-test"); bad_test.expected_test_assembly_sha256="0"*64
                with self.assertRaisesRegex(ValueError,"test assembly missing or hash mismatch"):
                    runner.run(bad_test)
                bad_manifest=args("bad-manifest"); bad_manifest.expected_manifest_sha256="0"*64
                with self.assertRaisesRegex(ValueError,"seed manifest missing or SHA-256 mismatch"):
                    runner.run(bad_manifest)
                bad_runner=args("bad-runner"); bad_runner.expected_runner_sha256="0"*64
                with self.assertRaisesRegex(ValueError,"runner SHA-256 mismatch"):
                    runner.run(bad_runner)
                overlapping=args("overlap"); overlapping.calibration_seeds.write_text("502\n")
                overlapping.expected_calibration_manifest_sha256=runner.sha(overlapping.calibration_seeds)
                with self.assertRaisesRegex(ValueError,"manifests overlap"):
                    runner.run(overlapping)
            invoke.assert_not_called()

    def test_multi_trace_requires_fresh_output_root(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d); source, pack, harness, sim, manifest=self.fixture(root)
            seeds=root/"seeds"; seeds.write_text("106\n")
            output=root/"existing"; output.mkdir(); marker=put(output/"keep.txt", b"keep")
            class Args: pass
            a=Args(); a.provenance=manifest; a.source_root=source; a.pack=pack; a.harness=harness; a.sim=sim
            a.seeds=seeds; a.output=output; a.minutes=130; a.trace_kinds=["sea10", "siege"]
            with self.assertRaisesRegex(ValueError, "already exists"):
                runner.run(a)
            self.assertEqual(marker, runner.sha(output/"keep.txt"))

    def test_duplicate_seeds_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/"seeds"; p.write_text("7\n7\n")
            with self.assertRaisesRegex(ValueError, "unique"):
                runner.read_seeds(p)

    def test_success_freezes_once_and_uses_trace_flag(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d); source, pack, harness, sim, manifest=self.fixture(root)
            seeds=root/"seeds"; seeds.write_text("101\n")
            class Args: pass
            a=Args(); a.provenance=manifest; a.source_root=source; a.pack=pack; a.harness=harness; a.sim=sim
            a.seeds=seeds; a.output=root/"run"; a.minutes=25; a.trace_kind="territory"
            def fake_process(cmd, stdout):
                stdout.write(b"mock match output\n")
                return 0
            with patch.object(runner, "run_frozen_command", side_effect=fake_process) as call:
                self.assertEqual(0, runner.run(a))
            self.assertIn("--fief-territory-trace", call.call_args.args[0])
            freeze=json.loads((a.output/"run-freeze.json").read_text())
            self.assertEqual(runner.sha(seeds), freeze["seed_file_sha256"])
            self.assertEqual(runner.sha(MODULE), freeze["runner_sha256"])
            self.assertEqual([101], freeze["seeds"])
            run_events=[json.loads(line) for line in (a.output/"run-status.jsonl").read_text().splitlines()]
            seed_events=[json.loads(line) for line in (a.output/"seed-101.status.jsonl").read_text().splitlines()]
            self.assertEqual("completed", run_events[-1]["status"])
            self.assertEqual(("completed", 0), (seed_events[-1]["status"], seed_events[-1]["exit_code"]))
            with self.assertRaisesRegex(ValueError, "already exists"):
                runner.run(a)

    def test_nonzero_seed_exit_records_seed_and_terminal_run_status(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d); source, pack, harness, sim, manifest=self.fixture(root)
            seeds=root/"seeds"; seeds.write_text("103\n")
            class Args: pass
            a=Args(); a.provenance=manifest; a.source_root=source; a.pack=pack; a.harness=harness; a.sim=sim
            a.seeds=seeds; a.output=root/"failed-run"; a.minutes=130; a.trace_kind="siege"
            def fake_process(cmd, stdout):
                stdout.write(b"partial output\n")
                return 7
            with patch.object(runner, "run_frozen_command", side_effect=fake_process):
                self.assertEqual(1, runner.run(a))
            run_events=[json.loads(line) for line in (a.output/"run-status.jsonl").read_text().splitlines()]
            seed_events=[json.loads(line) for line in (a.output/"seed-103.status.jsonl").read_text().splitlines()]
            self.assertEqual(("failed", 7), (run_events[-1]["status"], run_events[-1]["exit_code"]))
            self.assertEqual(("failed", 7), (seed_events[-1]["status"], seed_events[-1]["exit_code"]))

    def test_signal_records_interrupted_seed_and_terminal_run_status(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d); source, pack, harness, sim, manifest=self.fixture(root)
            seeds=root/"seeds"; seeds.write_text("104\n")
            class Args: pass
            a=Args(); a.provenance=manifest; a.source_root=source; a.pack=pack; a.harness=harness; a.sim=sim
            a.seeds=seeds; a.output=root/"interrupted-run"; a.minutes=130; a.trace_kind="siege"
            with patch.object(runner, "run_frozen_command", side_effect=runner.RunInterrupted(signal.SIGTERM)):
                self.assertEqual(128 + signal.SIGTERM, runner.run(a))
            run_events=[json.loads(line) for line in (a.output/"run-status.jsonl").read_text().splitlines()]
            seed_events=[json.loads(line) for line in (a.output/"seed-104.status.jsonl").read_text().splitlines()]
            self.assertEqual(("interrupted", signal.SIGTERM), (run_events[-1]["status"], run_events[-1]["signal"]))
            self.assertEqual(("interrupted", signal.SIGTERM), (seed_events[-1]["status"], seed_events[-1]["signal"]))

if __name__ == "__main__": unittest.main()
