from __future__ import annotations

import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
import run_fief_ft2_natural_ledger as runner


LEDGER = "F-T2 mill power ledger (12 seasons from first source; grain coverage is adult-equivalent capacity, not a separate flour-stock measure):\n"
SUMMARY = "  completed town windows=2; censored=1; weighted grain coverage=70.0% (target >=60%: PASS); town windows meeting 60%=2/2\n"


def result_text(seed: int) -> str:
    windows = (
        "  s1 c0 seasons 10–21: 70.0%, sources water/wind 0/1, power 20.00/25.00 hours\n"
        "  s2 c0 seasons 12–23: 75.0%, sources water/wind 1/0, power 22.00/25.00 hours\n"
        "  s3 c1 seasons 14–20: 50.0%, sources water/wind 0/1, power 10.00/20.00 hours (censored 7/12)\n"
    )
    return f"== fief-match seed {seed}: 130.0 minutes simulated, chapter end None at season none (ended: False)\n" + LEDGER + SUMMARY + windows


class RunnerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        self.pack = self.root / "numeric.pack"
        self.harness = self.root / "CivSim.Harness.dll"
        self.sim = self.root / "CivSim.Sim.dll"
        for path in (self.pack, self.harness, self.sim):
            path.write_bytes(path.name.encode())
        self.manifest = self.root / "seeds.txt"
        self.manifest.write_text("# frozen fixture\n101\n102\n", encoding="utf-8")
        self.out = self.root / "fresh-output"
        self.identity = {
            "headCommit": "head-abc",
            "sourceAggregateSha256": "source-def",
            "packSha256": "pack-hash",
            "harnessDllSha256": "harness-hash",
            "simDllSha256": "sim-hash",
        }

    def tearDown(self) -> None:
        self.tmp.cleanup()

    def args(self, **overrides):
        fields = {
            "repo": self.repo, "pack": self.pack, "harness_dll": self.harness, "sim_dll": self.sim,
            "manifest": self.manifest, "expected_seed_count": 2,
            "expected_manifest_sha256": hashlib.sha256(self.manifest.read_bytes()).hexdigest(),
            "expected_head": "head-abc", "expected_source_aggregate": "source-def",
            "expected_pack_sha256": "pack-hash", "expected_harness_sha256": "harness-hash",
            "expected_sim_sha256": "sim-hash", "output_dir": self.out, "dotnet": "dotnet", "minutes": 130,
        }
        fields.update(overrides)
        return SimpleNamespace(**fields)

    def run_fixture(self, *, outputs=None, identities=None):
        outputs = outputs or {101: result_text(101), 102: result_text(102)}
        identities = identities or [self.identity, self.identity]
        captures = iter(identities)
        commands = []

        def capture_fn(*_):
            return next(captures)

        def process_run(command, **kwargs):
            commands.append(command)
            seed = int(command[command.index("--seed") + 1])
            kwargs["stdout"].write(outputs[seed])
            return SimpleNamespace(returncode=0)

        code = runner.run(self.args(), capture_fn=capture_fn, process_run=process_run)
        return code, json.loads((self.out / "run_metadata.json").read_text()), commands

    def test_success_accounts_for_each_seed_censors_and_natural_command(self):
        code, metadata, commands = self.run_fixture()
        self.assertEqual(0, code)
        self.assertEqual("complete", metadata["status"])
        self.assertEqual({"expectedSeeds": 2, "attemptedRuns": 2, "uniqueSeeds": 2,
                          "missingSeeds": [], "unexpectedSeeds": [], "duplicateResultSeeds": [],
                          "allResultsValid": True}, metadata["accounting"])
        self.assertEqual([1, 1], [row["censoredTownWindows"] for row in metadata["runs"]])
        self.assertEqual([2, 2], [row["completeWindowRows"] for row in metadata["runs"]])
        self.assertEqual([1, 1], [row["censoredWindowRows"] for row in metadata["runs"]])
        self.assertEqual([0, 0], [row["invalidWindowRows"] for row in metadata["runs"]])
        protocol = metadata["protocol"]
        self.assertEqual(12, protocol["townWindowSeasonsInclusive"])
        self.assertEqual("adultEquivalentCapacityProxy", protocol["grainCoverageMeasure"])
        self.assertFalse(protocol["separateFlourInventory"])
        self.assertTrue(protocol["secondWindmillAllowed"])
        first_window = metadata["runs"][0]["townWindows"][0]
        self.assertEqual((10, 21, 12), (first_window["startSeason"], first_window["endSeason"], first_window["observedSeasons"]))
        self.assertEqual(70.0, first_window["coveragePercent"])
        self.assertEqual((0, 1), (first_window["waterSources"], first_window["windSources"]))
        self.assertEqual((20.0, 25.0), (first_window["powerUsedHours"], first_window["powerOfferedHours"]))
        self.assertTrue(all("--fief-power-ledger" in command for command in commands))
        self.assertTrue(all("--grant-e5" not in command for command in commands))

    def test_short_uncensored_window_is_invalid_not_complete(self):
        text = result_text(101).replace("seasons 10–21:", "seasons 10–20:", 1)
        parsed = runner.parse_result(text, 101)
        self.assertTrue(any("has 11 seasons; expected 12" in error for error in parsed["parseErrors"]))
        self.assertEqual(1, parsed["completeWindowRows"])
        self.assertEqual(1, parsed["invalidWindowRows"])

    def test_duplicate_town_window_fails_closed(self):
        text = result_text(101)
        duplicate = "  s1 c0 seasons 10–21: 70.0%, sources water/wind 0/1, power 20.00/25.00 hours\n"
        parsed = runner.parse_result(text.replace(LEDGER, LEDGER + duplicate), 101)
        self.assertTrue(any("duplicate F-T2 town/window identity" in error for error in parsed["parseErrors"]))
        self.assertEqual(1, parsed["invalidWindowRows"])

    def test_malformed_required_coverage_field_fails_closed(self):
        text = result_text(101).replace("70.0%, sources", "sources", 1)
        parsed = runner.parse_result(text, 101)
        self.assertTrue(any("malformed F-T2 town window row" in error for error in parsed["parseErrors"]))
        self.assertTrue(parsed["parseErrors"])

    def test_unrelated_sawmill_summary_is_not_parsed_as_town_window(self):
        text = result_text(101) + "  sawmill lacks wood: 1 towns\n"
        parsed = runner.parse_result(text, 101)
        self.assertEqual([], parsed["parseErrors"])
        self.assertEqual(3, parsed["townWindowRows"])

    def test_censored_short_window_counts_only_as_censored(self):
        parsed = runner.parse_result(result_text(101), 101)
        censored = next(row for row in parsed["townWindows"] if row["civ"] == 1)
        self.assertEqual("censored", censored["kind"])
        self.assertEqual(7, censored["observedSeasons"])
        self.assertEqual(2, parsed["completeWindowRows"])
        self.assertEqual(1, parsed["censoredWindowRows"])
        self.assertEqual(0, parsed["invalidWindowRows"])

    def test_missing_match_or_ledger_result_fails_closed(self):
        code, metadata, _ = self.run_fixture(outputs={101: "", 102: result_text(102)})
        self.assertEqual(2, code)
        self.assertEqual("failed", metadata["status"])
        self.assertEqual("error", metadata["runs"][0]["status"])
        self.assertTrue(metadata["runs"][0]["parseErrors"])

    def test_missing_window_detail_and_harness_invocation_error_are_recorded(self):
        text = result_text(101).replace("  s3 c1 seasons 14–20: 50.0%, sources water/wind 0/1, power 10.00/20.00 hours (censored 7/12)\n", "")
        outputs = {101: result_text(101), 102: text}
        captures = iter([self.identity, self.identity])

        def process_run(command, **kwargs):
            seed = int(command[command.index("--seed") + 1])
            if seed == 101:
                raise OSError("fixture launch failure")
            kwargs["stdout"].write(outputs[seed])
            return SimpleNamespace(returncode=0)

        code = runner.run(self.args(), capture_fn=lambda *_: next(captures), process_run=process_run)
        metadata = json.loads((self.out / "run_metadata.json").read_text())
        self.assertEqual(2, code)
        self.assertIn("could not execute harness", " ".join(metadata["runs"][0]["parseErrors"]))
        self.assertIn("summary accounts for 3 windows but found 2 detail rows", metadata["runs"][1]["parseErrors"])
        self.assertEqual(2, metadata["accounting"]["attemptedRuns"])

    def test_wrong_seed_and_duplicate_result_headers_fail(self):
        code, metadata, _ = self.run_fixture(outputs={
            101: result_text(102) + result_text(101), 102: result_text(102),
        })
        self.assertEqual(2, code)
        self.assertIn("expected exactly one match result header, found 2", metadata["runs"][0]["parseErrors"])

    def test_provenance_drift_fails_even_when_runs_parse(self):
        changed = dict(self.identity, simDllSha256="drift")
        code, metadata, _ = self.run_fixture(identities=[self.identity, changed])
        self.assertEqual(2, code)
        self.assertTrue(metadata["provenanceChangedDuringRun"])

    def test_post_run_provenance_capture_failure_is_recorded(self):
        captures = iter([self.identity])

        def capture_fn(*_):
            try:
                return next(captures)
            except StopIteration:
                raise OSError("fixture hash failure")

        def process_run(command, **kwargs):
            seed = int(command[command.index("--seed") + 1])
            kwargs["stdout"].write(result_text(seed))
            return SimpleNamespace(returncode=0)

        code = runner.run(self.args(), capture_fn=capture_fn, process_run=process_run)
        metadata = json.loads((self.out / "run_metadata.json").read_text())
        self.assertEqual(2, code)
        self.assertEqual("failed", metadata["status"])
        self.assertIn("fixture hash failure", metadata["provenanceAfterCaptureError"])

    def test_duplicate_manifest_seed_is_rejected_before_output_creation(self):
        self.manifest.write_text("101\n101\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "duplicate seeds"):
            runner.run(self.args(expected_manifest_sha256=hashlib.sha256(self.manifest.read_bytes()).hexdigest()),
                       capture_fn=lambda *_: self.identity, process_run=lambda *_a, **_k: None)
        self.assertFalse(self.out.exists())

    def test_manifest_count_hash_and_reused_output_root_are_rejected(self):
        with self.assertRaisesRegex(ValueError, "SHA-256 mismatch"):
            runner.run(self.args(expected_manifest_sha256="bad"), capture_fn=lambda *_: self.identity)
        with self.assertRaisesRegex(ValueError, "exactly 3"):
            runner.run(self.args(expected_seed_count=3), capture_fn=lambda *_: self.identity)
        self.out.mkdir()
        with self.assertRaisesRegex(FileExistsError, "refusing to reuse"):
            runner.run(self.args(), capture_fn=lambda *_: self.identity)


if __name__ == "__main__":
    unittest.main()
