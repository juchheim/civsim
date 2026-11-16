from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

import validate_fief_a8_result as validator


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class ValidateFiefA8ResultTests(unittest.TestCase):
    def make_run(self, root: Path) -> tuple[Path, Path, list[dict[str, object]]]:
        run_dir = root / "run"
        run_dir.mkdir()
        pins_path = root / "pins.json"
        pins = {
            "head": "head-hash", "sourceAggregateSha256": "source-hash",
            "packSha256": "pack-hash", "harnessSha256": "harness-hash",
            "simSha256": "sim-hash", "testSourceSha256": "test-source-hash",
            "testAssemblySha256": "test-assembly-hash", "runnerSha256": "runner-hash",
            "validatorSha256": "validator-hash", "hashHelperSha256": "hash-helper-hash",
            "seeds": [101, 102],
            "minutes": 64, "intervalSeasons": 16,
        }
        pins_path.write_text(json.dumps(pins))
        rows: list[dict[str, object]] = []
        for seed in pins["seeds"]:
            for kind in sorted(validator.REQUIRED_KINDS):
                applied = kind != "horse-reference"
                row: dict[str, object] = {
                    "seed": seed, "season": 16, "observer": 0, "kind": kind,
                    "applied": applied, "pass": applied, "deposits": 1,
                    "powder": 1, "fleet": 1, "horses": 1, "people": 1,
                    "diffs": [],
                }
                rows.append(row)
        rows_path = run_dir / "a8-rows.jsonl"
        rows_path.write_text("".join(json.dumps(row) + "\n" for row in rows))
        controls = {kind: {"checkpoints": 2, "applied": 2, "detected": 2}
                    for kind in validator.VISIBLE}
        summary = {
            "packSha256": pins["packSha256"], "simAssemblySha256": pins["simSha256"],
            "seeds": pins["seeds"], "minutes": 64, "intervalSeasons": 16,
            "observerCheckpointsTotal": len(rows), "cloneChecksumMismatches": 0,
            "unseenInvarianceFailures": 0,
            "tierBComposite": {"applied": 2, "withInputOrDecisionDiffs": 0},
            "nullControls": {"applied": 2, "withDiffs": 0},
            "visibleControls": controls, "visibleControlMisses": 0,
        }
        (run_dir / "a8-summary.json").write_text(json.dumps(summary))
        meta = {
            "status": "completed", "unchangedDuringRun": True,
            "output": {"exitCode": 0}, "testSourceSha256": pins["testSourceSha256"],
            "toolPins": {"runnerSha256": pins["runnerSha256"],
                         "validatorSha256": pins["validatorSha256"],
                         "hashHelperSha256": pins["hashHelperSha256"]},
            "testBinaries": {"CivSim.Tests.dll": pins["testAssemblySha256"]},
            "preflight": {
                "snapshot": {"head": pins["head"],
                             "contentSrcGameAggregateSha256": pins["sourceAggregateSha256"]},
                "binaries": {"pack": pins["packSha256"], "harnessDll": pins["harnessSha256"],
                             "simDll": pins["simSha256"]},
            },
            "seeds": pins["seeds"],
            "env": {"CIVSIM_A8_MINUTES": "64", "CIVSIM_A8_INTERVAL": "16"},
        }
        for name in ("a8-summary.json", "a8-rows.jsonl"):
            artifact = run_dir / name
            meta.setdefault("artifacts", {})[name] = {"bytes": artifact.stat().st_size,
                                                        "sha256": digest(artifact)}
        (run_dir / "run_metadata.json").write_text(json.dumps(meta))
        return run_dir, pins_path, rows

    def test_accepts_consistent_zero_diff_run_with_exposure_and_detected_controls(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            run_dir, pins, _ = self.make_run(Path(temp))
            result = validator.validate(run_dir, pins)
            self.assertEqual("pass", result["status"])
            self.assertEqual(0, result["visibleControlMisses"])

    def test_rejects_tier_b_composite_diff_even_when_all_other_gates_pass(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            run_dir, pins, rows = self.make_run(Path(temp))
            row = next(row for row in rows if row["kind"] == "unseen-composite")
            row["pass"] = False
            (run_dir / "a8-rows.jsonl").write_text("".join(json.dumps(row) + "\n" for row in rows))
            summary = json.loads((run_dir / "a8-summary.json").read_text())
            summary["tierBComposite"]["withInputOrDecisionDiffs"] = 1
            (run_dir / "a8-summary.json").write_text(json.dumps(summary))
            meta = json.loads((run_dir / "run_metadata.json").read_text())
            for name in ("a8-summary.json", "a8-rows.jsonl"):
                artifact = run_dir / name
                meta["artifacts"][name] = {"bytes": artifact.stat().st_size, "sha256": digest(artifact)}
            (run_dir / "run_metadata.json").write_text(json.dumps(meta))
            result = validator.validate(run_dir, pins)
            self.assertEqual("fail", result["status"])
            self.assertEqual(1, result["compositeFailures"])

    def test_rejects_unapplied_positive_control_even_if_old_summary_claimed_zero_misses(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            run_dir, pins, rows = self.make_run(Path(temp))
            row = next(row for row in rows if row["kind"] == "visible-fleet")
            row["applied"] = False
            row["pass"] = False
            (run_dir / "a8-rows.jsonl").write_text("".join(json.dumps(row) + "\n" for row in rows))
            meta = json.loads((run_dir / "run_metadata.json").read_text())
            artifact = run_dir / "a8-rows.jsonl"
            meta["artifacts"]["a8-rows.jsonl"] = {"bytes": artifact.stat().st_size, "sha256": digest(artifact)}
            (run_dir / "run_metadata.json").write_text(json.dumps(meta))
            with self.assertRaisesRegex(ValueError, "applied count mismatch"):
                validator.validate(run_dir, pins)

    def test_rejects_stale_test_source_hash(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            run_dir, pins_path, _ = self.make_run(Path(temp))
            pins = json.loads(pins_path.read_text())
            pins["testSourceSha256"] = "some-other-source"
            pins_path.write_text(json.dumps(pins))
            with self.assertRaisesRegex(ValueError, "test source SHA mismatch"):
                validator.validate(run_dir, pins_path)

    def test_rejects_tool_hash_mismatch(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            run_dir, pins_path, _ = self.make_run(Path(temp))
            meta_path = run_dir / "run_metadata.json"
            meta = json.loads(meta_path.read_text())
            meta["toolPins"]["runnerSha256"] = "changed-runner"
            meta_path.write_text(json.dumps(meta))
            with self.assertRaisesRegex(ValueError, "runner/validator/source-helper SHA mismatch"):
                validator.validate(run_dir, pins_path)


if __name__ == "__main__":
    unittest.main()
