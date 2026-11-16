import hashlib, importlib.util, json, tempfile, unittest, subprocess
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
        source = root / "source"; files = {"content/a.yml": b"content", "src/x.cs": b"source", "game/project.godot": b"game"}
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
        ph, hh, sh = put(pack, b"pack"), put(harness, b"harness"), put(sim, b"sim")
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
