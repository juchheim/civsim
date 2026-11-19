from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

import analyze_fief_sea10_trace as analyzer


ONE = analyzer.FIX64_ONE


def event(seed: int, kind: str, shipment: int, *, route: str = "water", good: str = "Food",
          sea10: bool = True, requested: int = 60 * ONE, capped: int = 48 * ONE,
          taken: int = 48 * ONE, carried: int = 48 * ONE, delivered: int = 0,
          returned: int = 0, lost: int = 0, reason: str = "test") -> str:
    return (
        "sea10-trace "
        f"kind={kind} seed={seed} season=4 shipment={shipment} compact=7 priorDeliveries=0 "
        f"sellerCiv=0 buyerCiv=1 seller=3 buyer=9 good={good} route={route} "
        f"physicalWaterTiles=8 eligibleWaterTiles=8 SEA04=true KEY6=true SEA10={str(sea10).lower()} "
        f"reason={reason} requested={requested} capped={capped} taken={taken} carried={carried} "
        f"delivered={delivered} paid=0 returned={returned} lost={lost} arrivalSeason=5"
    )


class Sea10TraceAnalyzerTests(unittest.TestCase):
    def _make_frozen_run(self, root: Path, raw_lines: list[str]) -> tuple[Path, Path, Path]:
        seed = 44
        candidate = root / "candidate"
        candidate.mkdir()
        (candidate / "pack.bin").write_bytes(b"pack")
        (candidate / "CivSim.Harness.dll").write_bytes(b"harness")
        (candidate / "CivSim.Sim.dll").write_bytes(b"sim")
        source_manifest = {
            "head": "deadbeef",
            "files": [{"path": "content/README.md", "sha256": "a" * 64, "status": "clean"}],
        }
        canonical = json.dumps({"head": source_manifest["head"], "files": source_manifest["files"]},
                               sort_keys=True, separators=(",", ":")).encode()
        source_hash = hashlib.sha256(canonical).hexdigest()
        (candidate / "source-manifest.json").write_text(json.dumps(source_manifest), encoding="utf-8")
        provenance = {
            "source_tree_sha256": source_hash,
            "source_manifest": "source-manifest.json",
            "source_manifest_sha256": analyzer.sha256(candidate / "source-manifest.json"),
            "pack_sha256": analyzer.sha256(candidate / "pack.bin"),
            "harness_sha256": analyzer.sha256(candidate / "CivSim.Harness.dll"),
            "sim_sha256": analyzer.sha256(candidate / "CivSim.Sim.dll"),
        }
        provenance_path = candidate / "sea10-provenance.json"
        provenance_path.write_text(json.dumps(provenance), encoding="utf-8")

        seeds_path = root / "seeds.txt"
        seeds_path.write_text(f"{seed}\n", encoding="utf-8")
        run_dir = root / "run"
        run_dir.mkdir()
        raw = "\n".join([
            f"== fief-match seed {seed}: 130 minutes simulated, chapter end horizon at season 100 (ended: False)",
            *raw_lines,
        ]) + "\n"
        raw_path = run_dir / f"seed-{seed}.txt"
        raw_path.write_text(raw, encoding="utf-8")
        run_hash = analyzer.sha256(raw_path)
        (run_dir / f"seed-{seed}.status.jsonl").write_text(
            json.dumps({"seed": seed, "status": "running"}) + "\n" +
            json.dumps({"seed": seed, "status": "completed", "exit_code": 0, "output_sha256": run_hash}) + "\n",
            encoding="utf-8",
        )
        (run_dir / "run-status.jsonl").write_text(
            json.dumps({"status": "running", "seed_count": 1}) + "\n" +
            json.dumps({"status": "completed", "seed_count": 1}) + "\n",
            encoding="utf-8",
        )
        runner = Path(analyzer.__file__).resolve().parents[4] / "tools/fief_trace_runner.py"
        freeze = {
            "provenance": provenance,
            "source_root": str(root),
            "pack": str(candidate / "pack.bin"),
            "harness": str(candidate / "CivSim.Harness.dll"),
            "sim": str(candidate / "CivSim.Sim.dll"),
            "minutes": 130,
            "runner": str(runner),
            "runner_sha256": analyzer.sha256(runner),
            "output": str(run_dir.resolve()),
            "seed_file": str(seeds_path),
            "seed_file_sha256": analyzer.sha256(seeds_path),
            "seeds": [seed],
            "trace": "--fief-sea10-trace",
        }
        (run_dir / "run-freeze.json").write_text(json.dumps(freeze), encoding="utf-8")
        return run_dir, seeds_path, provenance_path

    def test_frozen_run_reports_sea_exposure_caps_and_food_conversion_without_double_counting(self):
        seed = 44
        lines = [
            f"sea10-trace kind=milestone seed={seed} season=100 civ=0 project=KEY-6 complete=true completedSeason=80 canSail=true",
            event(seed, "route_rejected", -1, route="water_locked", sea10=False, reason="unreachable_or_water_locked", requested=0, capped=0, taken=0, carried=0),
            event(seed, "shipment_dispatched", 1),
            event(seed, "voyage_survived", 1),
            event(seed, "shipment_delivered", 1, delivered=42 * ONE, returned=6 * ONE),
            event(seed, "shipment_dispatched", 2, good="Tin", sea10=False, requested=20 * ONE,
                  capped=12 * ONE, taken=12 * ONE, carried=12 * ONE),
            event(seed, "voyage_turnback", 2, good="Tin", sea10=False, requested=12 * ONE,
                  capped=12 * ONE, taken=12 * ONE, carried=12 * ONE, returned=12 * ONE),
            event(seed, "shipment_dispatched", 3, requested=30 * ONE, capped=30 * ONE,
                  taken=30 * ONE, carried=30 * ONE),
            event(seed, "shipment_raid_loss", 3, requested=30 * ONE, capped=30 * ONE,
                  taken=30 * ONE, carried=30 * ONE, lost=30 * ONE),
        ]
        with tempfile.TemporaryDirectory() as tmp:
            run_dir, seeds_path, provenance_path = self._make_frozen_run(Path(tmp), lines)
            report = analyzer.analyze(run_dir, seeds_path, provenance_path)

        self.assertIn("Distinct actual water-route dispatches: 3 across 1 seeds", report)
        self.assertIn("Water shipment terminal outcomes: 1 delivery/settlement events (1 with positive delivered cargo), 1 voyage returns, 1 raid losses, 0 still", report)
        self.assertIn("Seeds with water-locked route observations: 1 (44)", report)
        self.assertIn("Food volume equivalents", report)
        self.assertIn("delivered 21.0000 L; returned 3.0000 L", report)
        self.assertIn("Food | 2 | R (food units) | 90.0000 | 78.0000 | 78.0000 | 78.0000 | 42.0000", report)
        self.assertIn("Tin | 1 | good-specific cargo units | 20.0000 | 12.0000 | 12.0000 | 12.0000", report)
        self.assertIn("| Tin | false | 1 | 1 | 1 | 0 | 0 | 12.0000 good-specific cargo units", report)
        self.assertIn("Diagnostic only", report)
        self.assertNotIn("PASS", report.upper())

    def test_unknown_kind_fails_closed(self):
        with self.assertRaisesRegex(ValueError, "unknown SEA-10 trace kind"):
            analyzer.parse_trace_row(event(44, "shipment_mystery", 1), "fixture")

    def test_shipment_mass_balance_mismatch_fails_closed(self):
        rows = [
            analyzer.parse_trace_row(event(44, "shipment_dispatched", 1), "fixture"),
            analyzer.parse_trace_row(event(44, "shipment_delivered", 1, delivered=42 * ONE, returned=5 * ONE), "fixture"),
        ]
        with self.assertRaisesRegex(ValueError, "does not reconcile"):
            analyzer.shipment_summary([row for row in rows if row is not None])

    def test_unfinished_water_shipment_is_listed_as_censored(self):
        with tempfile.TemporaryDirectory() as tmp:
            run_dir, seeds_path, provenance_path = self._make_frozen_run(
                Path(tmp), [event(44, "shipment_dispatched", 9, requested=10 * ONE,
                                  capped=10 * ONE, taken=10 * ONE, carried=10 * ONE)])
            report = analyzer.analyze(run_dir, seeds_path, provenance_path)
        self.assertIn("1 still without a traced terminal shipment event", report)
        self.assertIn("Censored water shipments (seed:shipment): 44:9", report)

    def test_raw_hash_mismatch_fails_closed(self):
        with tempfile.TemporaryDirectory() as tmp:
            run_dir, seeds_path, provenance_path = self._make_frozen_run(
                Path(tmp), [event(44, "route_rejected", -1, route="water_locked", sea10=False,
                                  requested=0, capped=0, taken=0, carried=0)])
            (run_dir / "seed-44.txt").write_text("changed after run\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "raw output SHA mismatch"):
                analyzer.analyze(run_dir, seeds_path, provenance_path)


if __name__ == "__main__":
    unittest.main()
