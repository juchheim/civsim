#!/usr/bin/env python3
"""Parser tests for deterministic F-T1 calibration output analysis."""

from __future__ import annotations

import importlib.util
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


SCRIPT = Path(__file__).with_name("analyze_fief_ft1_calibration.py")
SPEC = importlib.util.spec_from_file_location("analyze_fief_ft1_calibration", SCRIPT)
assert SPEC and SPEC.loader
analyzer = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(analyzer)

SYNTHETIC_MATCH = """== fief-match seed 20500859: 130.0 minutes simulated, chapter end None at season none (ended: False)
civ arch       KEY-4  KEY-5 E4 done  settl  people   T11   T12  adults soldiers scholars missions  idleS
c0  Builder     60.0   80.0      40     12    1100     1    20     500       20        2        1      0
c1  Grower      60.0   90.0      40     12    1100     1    20     500       20        2        1      0
c2  Warlord     60.0      -      40     12    1100     1    20     500       20        2        1      0
c3  Sage        60.0   80.0      40     12    1100     1    20     500       20        2        1      0
fief: E5 progress per civilization (KEY-6 timing is measured from own KEY-5; an unfinished row is censored at the run horizon):
  c0 Builder: 49 E5 projects, KEY-5→KEY-6 20.0 min from KEY-5 (at 100.0)
  c1 Grower: 12 E5 projects, KEY-5→KEY-6 not complete; observed 40.0 min after KEY-5 (right-censored)
  c2 Warlord: 0 E5 projects, KEY-5→KEY-6 KEY-5 not reached
  c3 Sage: 49 E5 projects, KEY-5→KEY-6 24.0 min from KEY-5 (at 104.0)
"""


class FiefFt1CalibrationParserTests(unittest.TestCase):
    def test_default_manifest_is_replacement_calibration_block(self) -> None:
        self.assertTrue(analyzer.DEFAULT_MANIFEST.name.endswith("calibration-r2-seeds-2026-11-16.txt"))
        self.assertEqual("ec992c60c6e4b2820f401de529a680a74b0bcd77eb33dc57d8e75eecc3ceb417",
                         analyzer.sha256(analyzer.DEFAULT_MANIFEST))

    def test_parses_all_four_seats_and_preserves_censor_states(self) -> None:
        match = analyzer.parse_raw_output(SYNTHETIC_MATCH, 20500859, "synthetic.txt")
        self.assertEqual(20500859, match["seed"])
        self.assertEqual(130.0, match["simulated_minutes"])
        self.assertEqual(4, len(match["seats"]))
        self.assertEqual(
            ["complete", "right_censored_after_KEY5", "KEY5_not_reached", "complete"],
            [seat["state"] for seat in match["seats"]],
        )
        self.assertEqual(90.0, match["seats"][1]["key5_minute"])
        self.assertIsNone(match["seats"][1]["interval_minutes"])
        self.assertIsNone(match["seats"][2]["key5_minute"])
        self.assertEqual(24.0, match["seats"][3]["interval_minutes"])

    def test_rejects_wrong_seed_and_interval_inconsistent_with_key_times(self) -> None:
        with self.assertRaisesRegex(ValueError, "does not match expected seed"):
            analyzer.parse_raw_output(SYNTHETIC_MATCH, 20500860)
        inconsistent = SYNTHETIC_MATCH.replace("at 100.0", "at 102.0")
        with self.assertRaisesRegex(ValueError, "times disagree"):
            analyzer.parse_raw_output(inconsistent, 20500859)

    def test_requires_all_four_progress_rows(self) -> None:
        missing = SYNTHETIC_MATCH.replace(
            "  c3 Sage: 49 E5 projects, KEY-5→KEY-6 24.0 min from KEY-5 (at 104.0)\n", ""
        )
        with self.assertRaisesRegex(ValueError, "expected four E5 progress rows"):
            analyzer.parse_raw_output(missing, 20500859)

    def test_parses_combined_seed_chunks_in_frozen_order(self) -> None:
        second = SYNTHETIC_MATCH.replace("20500859", "20501724")
        parsed = analyzer.parse_combined_output(
            SYNTHETIC_MATCH.rstrip() + "\n" + second, [20500859, 20501724], "combined.txt"
        )
        self.assertEqual([20500859, 20501724], [match["seed"] for match in parsed])
        self.assertEqual(8, sum(len(match["seats"]) for match in parsed))

    def test_rejects_incomplete_or_reordered_combined_seed_coverage(self) -> None:
        second = SYNTHETIC_MATCH.replace("20500859", "20501724")
        combined = SYNTHETIC_MATCH.rstrip() + "\n" + second
        with self.assertRaisesRegex(ValueError, "seed header sequence"):
            analyzer.parse_combined_output(combined, [20501724, 20500859])
        with self.assertRaisesRegex(ValueError, "seed header sequence"):
            analyzer.parse_combined_output(SYNTHETIC_MATCH, [20500859, 20501724])

    def test_validates_current_per_seed_runner_metadata_shape(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            raw_dir = root / "raw"
            raw_dir.mkdir()
            manifest = root / "seeds.txt"
            seeds = list(range(1000, 1020))
            manifest.write_text("".join(f"{seed}\n" for seed in seeds), encoding="utf-8")
            manifest_hash = hashlib.sha256(manifest.read_bytes()).hexdigest()
            provenance = {
                "headCommit": "a" * 40, "sourceAggregateSha256": "b" * 64,
                "packSha256": "c" * 64, "harnessDllSha256": "d" * 64,
                "simDllSha256": "e" * 64, "runnerSha256": "f" * 64,
                "erasSha256": "1" * 64,
            }
            enriched = {
                **provenance, "e5BaseCost": 6000, "packE5BaseCost": 6000,
                "e5BaseCostRecordSha256": analyzer.pinned.EXPECTED_BASE_COST_RECORD_SHA256,
                "packE5BaseCostRecordSha256": analyzer.pinned.EXPECTED_BASE_COST_RECORD_SHA256,
            }
            rows = []
            for seed in seeds:
                raw_name = f"seed-{seed}.txt"
                raw = SYNTHETIC_MATCH.replace("20500859", str(seed)).encode("utf-8")
                (raw_dir / raw_name).write_bytes(raw)
                rows.append({
                    "seed": seed, "status": "complete", "exitCode": 0,
                    "rawFile": raw_name, "rawSha256": hashlib.sha256(raw).hexdigest(),
                    "command": ["dotnet", "Harness.dll", "fief-match", "--pack", "pack",
                                "--seed", str(seed), "--minutes", "130"],
                    "provenanceBefore": provenance, "provenanceAfter": provenance,
                })
            metadata = {
                "status": "complete", "minutes": 130,
                "cohortRole": "calibration_only_not_acceptance",
                "manifestSha256": manifest_hash, "seeds": seeds,
                "provenanceBefore": enriched, "provenanceAfter": provenance, "runs": rows,
            }
            (raw_dir / "run_metadata.json").write_text(json.dumps(metadata), encoding="utf-8")
            with patch.object(analyzer.pinned, "EXPECTED_MANIFEST_SHA256", manifest_hash):
                loaded_seeds, validated = analyzer.validate_run(raw_dir, manifest)
            self.assertEqual(seeds, loaded_seeds)
            self.assertEqual(20, len(validated["matches"]))
            self.assertEqual(80, sum(len(match["seats"]) for match in validated["matches"]))


if __name__ == "__main__":
    unittest.main()
