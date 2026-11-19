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
    def rewrite_run(self, run_dir: Path, rows: list[dict[str, object]], summary: dict[str, object]) -> None:
        rows_path = run_dir / "a8-rows.jsonl"
        rows_path.write_text("".join(json.dumps(row) + "\n" for row in rows))
        seeds = summary["seeds"]
        per_seed = {
            str(seed): {
                category: sum(1 for row in rows if row["seed"] == seed
                              and row["kind"] in {"unseen-strict", "unseen-composite", "unseen-horse"}
                              and row["eligible"] and row["applied"] and row[validator.EXPOSURE_FIELDS[category]] > 0)
                for category in validator.EXPOSURE_FIELDS
            }
            for seed in seeds
        }
        summary["unseenExposureBySeed"] = per_seed
        summary["unseenAppliedByCategory"] = {
            category: sum(per_seed[str(seed)][category] for seed in seeds)
            for category in validator.EXPOSURE_FIELDS
        }
        summary["unseenMatchesWithCategory"] = {
            category: sum(per_seed[str(seed)][category] > 0 for seed in seeds)
            for category in validator.EXPOSURE_FIELDS
        }
        summary_path = run_dir / "a8-summary.json"
        summary_path.write_text(json.dumps(summary))
        meta_path = run_dir / "run_metadata.json"
        meta = json.loads(meta_path.read_text())
        for name in ("a8-summary.json", "a8-rows.jsonl"):
            artifact = run_dir / name
            meta["artifacts"][name] = {"bytes": artifact.stat().st_size, "sha256": digest(artifact)}
        meta_path.write_text(json.dumps(meta))

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
                    "eligible": True, "observerHasSettlement": True,
                    "applied": applied, "pass": applied, "notApplied": "", "deposits": 1,
                    "powder": 1, "fleet": 1, "horses": 1, "people": 1,
                    "diffs": (["visible input detected"] if kind in validator.VISIBLE else []),
                }
                rows.append(row)
        rows_path = run_dir / "a8-rows.jsonl"
        rows_path.write_text("".join(json.dumps(row) + "\n" for row in rows))
        controls = {kind: {"checkpoints": 2, "eligible": 2, "ineligible": 0, "applied": 2, "unapplied": 0, "detected": 2}
                    for kind in validator.VISIBLE}
        summary = {
            "schemaVersion": 2,
            "packSha256": pins["packSha256"], "simAssemblySha256": pins["simSha256"],
            "seeds": pins["seeds"], "minutes": 64, "intervalSeasons": 16,
            "observerCheckpointsTotal": len(rows), "cloneChecksumMismatches": 0,
            "unseenInvarianceFailures": 0, "unseenNotApplied": 0, "unseenIneligible": 0,
            "unseenAppliedByCategory": {"deposits": 6, "powderCannon": 6, "fleetCrew": 6, "horses": 6, "peopleStoresResearch": 6},
            "unseenMatchesWithCategory": {"deposits": 2, "powderCannon": 2, "fleetCrew": 2, "horses": 2, "peopleStoresResearch": 2},
            "unseenExposureBySeed": {str(seed): {category: 3 for category in validator.EXPOSURE_FIELDS} for seed in pins["seeds"]},
            "tierBComposite": {"applied": 2, "notApplied": 0, "ineligible": 0, "withInputOrDecisionDiffs": 0},
            "nullControls": {"applied": 2, "notApplied": 0, "ineligible": 0, "withDiffs": 0},
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

    def test_accepts_explicit_ineligible_eliminated_observer_and_checks_other_controls(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            run_dir, pins, rows = self.make_run(Path(temp))
            summary = json.loads((run_dir / "a8-summary.json").read_text())
            row = next(row for row in rows if row["seed"] == 101 and row["kind"] == "visible-fleet")
            row.update(eligible=False, observerHasSettlement=False, applied=False,
                       **{"pass": False}, notApplied="observer has no settlement")
            additional = dict(row)
            additional.update(observer=1, eligible=True, observerHasSettlement=True, applied=True,
                               **{"pass": True}, notApplied="", diffs=["visible input detected"])
            rows.append(additional)
            controls = summary["visibleControls"]["visible-fleet"]
            controls.update(checkpoints=3, eligible=2, ineligible=1, applied=2, unapplied=0, detected=2)
            summary["visibleControlMisses"] = 0
            summary["observerCheckpointsTotal"] = len(rows)
            self.rewrite_run(run_dir, rows, summary)
            result = validator.validate(run_dir, pins)
            self.assertEqual("pass", result["status"])
            self.assertEqual(0, result["visibleControlMisses"])

    def test_rejects_seed_missing_one_unseen_category_even_when_other_seed_has_exposure(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            run_dir, pins, rows = self.make_run(Path(temp))
            summary = json.loads((run_dir / "a8-summary.json").read_text())
            for row in rows:
                if row["seed"] == 101 and row["kind"] in {"unseen-strict", "unseen-composite", "unseen-horse"}:
                    row["people"] = 0
            self.rewrite_run(run_dir, rows, summary)
            result = validator.validate(run_dir, pins)
            self.assertEqual("fail", result["status"])
            self.assertTrue(any("seed 101 has no unseen-state exposure for categories: peopleStoresResearch" in item
                                for item in result["failures"]))

    def test_rejects_ineligible_row_without_explicit_no_settlement_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            run_dir, pins, rows = self.make_run(Path(temp))
            row = next(row for row in rows if row["seed"] == 101 and row["kind"] == "visible-stock")
            row.update(eligible=False, observerHasSettlement=False, applied=False,
                       **{"pass": False}, notApplied="no home")
            self.rewrite_run(run_dir, rows, json.loads((run_dir / "a8-summary.json").read_text()))
            with self.assertRaisesRegex(ValueError, "exact reason"):
                validator.validate(run_dir, pins)

    def test_rejects_tier_b_composite_diff_even_when_all_other_gates_pass(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            run_dir, pins, rows = self.make_run(Path(temp))
            row = next(row for row in rows if row["kind"] == "unseen-composite")
            row["pass"] = False
            row["diffs"] = ["belief hash differs"]
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

    def test_rejects_unseen_horse_diff_hidden_by_true_pass_bit(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            run_dir, pins, rows = self.make_run(Path(temp))
            row = next(row for row in rows if row["kind"] == "unseen-horse")
            row["diffs"] = ["horse source: Seen vs Unknown"]
            # Simulate a stale/incorrect harness pass bit and consistently update
            # the artifact hashes and summaries. The validator must inspect raw diffs.
            rows_path = run_dir / "a8-rows.jsonl"
            rows_path.write_text("".join(json.dumps(item) + "\n" for item in rows))
            meta_path = run_dir / "run_metadata.json"
            meta = json.loads(meta_path.read_text())
            meta["artifacts"]["a8-rows.jsonl"] = {
                "bytes": rows_path.stat().st_size, "sha256": digest(rows_path)}
            meta_path.write_text(json.dumps(meta))
            with self.assertRaisesRegex(ValueError, "unseen-horse pass bit disagrees"):
                validator.validate(run_dir, pins)

    def test_rejects_visible_control_marked_detected_without_any_raw_diff(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            run_dir, pins, rows = self.make_run(Path(temp))
            row = next(row for row in rows if row["kind"] == "visible-horse")
            row["diffs"] = []
            rows_path = run_dir / "a8-rows.jsonl"
            rows_path.write_text("".join(json.dumps(item) + "\n" for item in rows))
            meta_path = run_dir / "run_metadata.json"
            meta = json.loads(meta_path.read_text())
            meta["artifacts"]["a8-rows.jsonl"] = {
                "bytes": rows_path.stat().st_size, "sha256": digest(rows_path)}
            meta_path.write_text(json.dumps(meta))
            with self.assertRaisesRegex(ValueError, "visible-horse detection bit disagrees"):
                validator.validate(run_dir, pins)

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
