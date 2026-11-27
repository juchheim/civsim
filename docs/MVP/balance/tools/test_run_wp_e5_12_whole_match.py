from __future__ import annotations

import argparse
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parent))
import run_wp_e5_12_whole_match as runner

MANIFEST = Path(__file__).resolve().parents[1] / "m12-e5-12-natural-seeds-2026-11-27-r1.txt"
EXPECTED_E5_OBSERVER_TRACE_FLAGS = (
    "--fief-power-ledger", "--fief-health-trace", "--fief-arms-lifecycle-trace",
    "--fief-institution-trace", "--fief-siege-trace", "--fief-siege-strategist-trace",
    "--fief-wp7-trace", "--fief-territory-trace", "--fief-sea10-trace",
)


class WpE512RunnerFixtures(unittest.TestCase):
    def test_manifest_is_exact_fresh_block_and_hash_is_required(self) -> None:
        seeds = runner.read_manifest(MANIFEST, runner.MANIFEST_SHA256)
        self.assertEqual(80, len(seeds))
        self.assertEqual(sorted(seeds), seeds)
        with self.assertRaisesRegex(ValueError, "manifest pin must be"):
            runner.read_manifest(MANIFEST, "0" * 64)

    def test_seat_parser_preserves_complete_and_both_censor_classes(self) -> None:
        text = """== fief-match seed 35000001: 130.0 minutes simulated, chapter end Open at season none (ended: False)
civ arch KEY-4 KEY-5 E4 done settl people T11 T12 adults soldiers scholars missions idleS
c0  Builder       -   90.0 10 1 100 0 0 0 0 0 0 0
c1  Grower       80.0  -    9 1 100 0 0 0 0 0 0 0
c2  Warlord      81.0  91.0 10 1 100 0 0 0 0 0 0 0
c3  Sage         82.0  92.0 10 1 100 0 0 0 0 0 0 0
fief: E5 progress per civilization:
  c0 Builder: 8 E5 projects, KEY-5→KEY-6 24.0 min from KEY-5 (at 114.0)
  c1 Grower: 0 E5 projects, KEY-5→KEY-6 KEY-5 not reached
  c2 Warlord: 0 E5 projects, KEY-5→KEY-6 not complete; observed 39.0 min after KEY-5 (right-censored)
  c3 Sage: 4 E5 projects, KEY-5→KEY-6 25.0 min from KEY-5 (at 117.0)
"""
        parsed = runner.parse_match_log(text, 35000001)
        self.assertEqual("key6-complete", parsed["seats"]["0"]["status"])
        self.assertEqual("key5-not-reached", parsed["seats"]["1"]["status"])
        self.assertEqual("key6-right-censored", parsed["seats"]["2"]["status"])
        self.assertEqual(24.0, parsed["seats"]["0"]["key5ToKey6Minutes"])

    def test_mocked_runner_records_all_per_match_provenance_and_artifact_hashes(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            repo = root / "repo"
            repo.mkdir()
            release = root / "Release"
            release.mkdir()
            pack, harness, sim = root / "numeric.pack", release / "CivSim.Harness.dll", release / "CivSim.Sim.dll"
            for path in (pack, harness, sim):
                path.write_bytes(path.name.encode())
            output = root / "external-output"
            snapshot = {"headCommit": "a" * 40, "headTree": "b" * 40,
                        "sourceAggregateSha256": "c" * 64,
                        "packSha256": runner.sha256(pack), "harnessDllSha256": runner.sha256(harness),
                        "simDllSha256": runner.sha256(sim)}
            script = Path(runner.__file__).resolve()
            helper = script.with_name("cohort_provenance.py")
            args = argparse.Namespace(
                repo=repo, pack=pack, harness_dll=harness, sim_dll=sim, manifest=MANIFEST,
                output_root=output, expected_manifest_sha256=runner.MANIFEST_SHA256,
                expected_head="a" * 40, expected_tree="b" * 40,
                expected_source_aggregate_sha256="c" * 64,
                expected_pack_sha256=snapshot["packSha256"],
                expected_harness_sha256=snapshot["harnessDllSha256"],
                expected_sim_sha256=snapshot["simDllSha256"],
                expected_runner_sha256=runner.sha256(script),
                expected_provenance_helper_sha256=runner.sha256(helper), dotnet="dotnet", workers=4)

            def fake_process(command: list[str], **kwargs: object) -> argparse.Namespace:
                stdout = kwargs["stdout"]
                if command[2] == "fief-match":
                    seed = command[command.index("--seed") + 1]
                    replay_dir = Path(command[command.index("--record-dir") + 1])
                    replay_dir.mkdir()
                    (replay_dir / f"{seed}.replay").write_bytes(b"fixture replay")
                    log = [f"== fief-match seed {seed}: 130.0 minutes simulated, chapter end Open at season none (ended: False)",
                           "civ arch KEY-4 KEY-5 E4 done settl people T11 T12 adults soldiers scholars missions idleS"]
                    for civ, arch in enumerate(runner.SEAT_ARCHETYPES):
                        log.append(f"c{civ}  {arch}  80.0  90.0  10 1 100 0 0 0 0 0 0 0")
                    log.append("fief: E5 progress per civilization:")
                    for civ, arch in enumerate(runner.SEAT_ARCHETYPES):
                        log.append(f"  c{civ} {arch}: 8 E5 projects, KEY-5→KEY-6 24.0 min from KEY-5 (at 114.0)")
                    stdout.write(("\n".join(log) + "\n").encode())
                else:
                    stdout.write(b"replay index: 1/1 verified\n")
                    index_path = Path(command[command.index("--output") + 1])
                    index_path.write_text(json.dumps({"seeds": [{"verified": True, "finalChecksum": "f" * 16}]}))
                return argparse.Namespace(returncode=0)

            with patch.object(runner, "capture_candidate", return_value=snapshot):
                metadata = runner.run(args, process_run=fake_process)
            self.assertEqual("complete", metadata["status"])
            self.assertEqual(80, len(metadata["matches"]))
            for match in metadata["matches"]:
                self.assertEqual("complete", match["status"])
                self.assertEqual(0, match["exitCode"])
                self.assertEqual(64, len(match["rawLogSha256"]))
                self.assertEqual(64, len(match["replaySha256"]))
                self.assertTrue(match["provenanceStable"])
                self.assertEqual(4, len(match["match"]["seats"]))
                self.assertTrue(match["replayVerification"]["verified"])
                self.assertEqual(EXPECTED_E5_OBSERVER_TRACE_FLAGS, tuple(match["command"][-len(EXPECTED_E5_OBSERVER_TRACE_FLAGS):]))
            self.assertEqual(EXPECTED_E5_OBSERVER_TRACE_FLAGS, tuple(metadata["observerTraceFlags"]))

    def test_existing_external_root_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            repo = root / "repo"
            repo.mkdir()
            release = root / "Release"
            release.mkdir()
            pack, harness, sim = root / "pack", release / "CivSim.Harness.dll", release / "CivSim.Sim.dll"
            for path in (pack, harness, sim):
                path.write_bytes(b"fixture")
            manifest = MANIFEST
            output = root / "existing"
            output.mkdir()
            args = argparse.Namespace(repo=repo, pack=pack, harness_dll=harness, sim_dll=sim,
                manifest=manifest, output_root=output)
            with self.assertRaisesRegex(FileExistsError, "refusing existing output root"):
                runner.run(args)


if __name__ == "__main__":
    unittest.main()
