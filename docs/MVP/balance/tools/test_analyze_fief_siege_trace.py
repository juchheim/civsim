from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

import analyze_fief_siege_trace as analyzer


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, value) -> None:
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


class FiefSiegeTraceAnalyzerTests(unittest.TestCase):
    def make_run(self, root: Path) -> tuple[Path, Path, list[int]]:
        manifest = analyzer.DEFAULT_MANIFEST
        seeds = analyzer.load_manifest(manifest)
        run_dir = root / "run"
        run_dir.mkdir()

        candidate = root / "candidate"
        candidate.mkdir()
        pack = candidate / "numeric-e5.pack"
        harness = candidate / "CivSim.Harness.dll"
        sim = candidate / "CivSim.Sim.dll"
        for path, data in ((pack, b"pack"), (harness, b"harness"), (sim, b"sim")):
            path.write_bytes(data)
        source_manifest = candidate / "source-manifest-ft5.json"
        source_payload = {"head": "abc123", "files": [{"path": "content/fake", "status": "clean", "sha256": "0" * 64}]}
        source_payload["aggregate_sha256"] = analyzer.canonical_sha({"head": source_payload["head"], "files": source_payload["files"]})
        write_json(source_manifest, source_payload)
        provenance = {
            "source_tree_sha256": source_payload["aggregate_sha256"],
            "pack_sha256": sha(pack),
            "harness_sha256": sha(harness),
            "sim_sha256": sha(sim),
            "source_manifest_sha256": sha(source_manifest),
            "source_manifest": source_manifest.name,
        }

        freeze = {
            "provenance": provenance,
            "source_root": str(root / "source"),
            "pack": str(pack),
            "harness": str(harness),
            "sim": str(sim),
            "minutes": 130,
            "runner": "/path/to/fief_trace_runner.py",
            "runner_sha256": "1" * 64,
            "output": str(run_dir),
            "seed_file": str(manifest),
            "seed_file_sha256": sha(manifest),
            "seeds": seeds,
            "trace": "--fief-siege-trace",
        }
        write_json(run_dir / "run-freeze.json", freeze)
        (run_dir / "run-status.jsonl").write_text(
            json.dumps({"status": "running", "seed_count": 20, "trace": "--fief-siege-trace"}) + "\n" +
            json.dumps({"status": "completed", "seed_count": 20}) + "\n",
            encoding="utf-8",
        )

        for i, seed in enumerate(seeds):
            header = f"== fief-match seed {seed}: 130 minutes simulated, chapter end KEY-6 at season 500 (ended: False)\n"
            rows = [header]
            if i == 0:
                rows.extend([
                    f"fief-siege-trace seed={seed} season=2 attacker=0 defender=1 settlement=4 siegeTurn=1 engines=1 cannons=1 engineCrews=1 cannonCrews=1 assignedCrews=2 attritionAssignedCrews=8 attritionDeaths=0 def09Active=false cannonShots=1 powderPaid=0.25 fireAttempted=true exposed=true integrity=0.500 powderBreach=true outcome=active\n",
                    f"fief-siege-trace seed={seed} season=3 attacker=0 defender=1 settlement=4 siegeTurn=2 engines=1 cannons=1 engineCrews=1 cannonCrews=1 assignedCrews=2 attritionAssignedCrews=8 attritionDeaths=1 def09Active=false cannonShots=0 powderPaid=0.00 fireAttempted=true exposed=true integrity=0.000 powderBreach=true outcome=active\n",
                    f"fief-siege-trace seed={seed} season=4 attacker=0 defender=1 settlement=4 siegeTurn=3 engines=1 cannons=1 engineCrews=unavailable cannonCrews=unavailable assignedCrews=unavailable attritionAssignedCrews=unavailable attritionDeaths=unavailable def09Active=unavailable cannonShots=unavailable powderPaid=unavailable fireAttempted=false exposed=false integrity=0.000 powderBreach=true outcome=right_censored\n",
                    f"fief-siege-terminal seed={seed} season=4 exposure=observed settlements=1\n",
                    f"fief-siege-terminal-inventory seed={seed} civ=0 cannonStock=0.00 powderStock=1.50\n",
                    f"fief-siege-terminal-settlement seed={seed} settlement=4 attacker=0 defender=1 stoneWalls=true gatehouse=false castle=true castleGarrisoned=false concentricCastle=false integrity=0.00 powderBreach=true outcome=right_censored censorReason=match_horizon_active_investment\n",
                ])
            elif i == 1:
                rows.extend([
                    f"fief-siege-trace seed={seed} season=10 attacker=1 defender=0 settlement=9 siegeTurn=1 engines=0 cannons=1 engineCrews=0 cannonCrews=1 assignedCrews=1 attritionAssignedCrews=4 attritionDeaths=1 def09Active=true cannonShots=1 powderPaid=0.25 fireAttempted=true exposed=true integrity=0.000 powderBreach=true outcome=ended\n",
                    f"fief-siege-terminal seed={seed} season=11 exposure=observed settlements=1\n",
                    f"fief-siege-terminal-inventory seed={seed} civ=1 cannonStock=1.00 powderStock=0.75\n",
                    f"fief-siege-terminal-settlement seed={seed} settlement=9 attacker=1 defender=0 stoneWalls=true gatehouse=false castle=true castleGarrisoned=true concentricCastle=true integrity=0.00 powderBreach=true outcome=investmentcapitulated censorReason=none\n",
                ])
            else:
                rows.append(f"fief-siege-terminal seed={seed} season=500 exposure=none reason=no_siege_investment_observed\n")
            output = run_dir / f"seed-{seed}.txt"
            output.write_text("".join(rows), encoding="utf-8")
            write_jsonl(run_dir / f"seed-{seed}.status.jsonl", [
                {"seed": seed, "status": "running", "command": ["dotnet", str(harness), "fief-match", "--pack",
                                                                      str(pack.resolve()), "--seed", str(seed), "--minutes", "130",
                                                                      "--fief-siege-trace"]},
                {"seed": seed, "status": "completed", "exit_code": 0, "output_sha256": sha(output)},
            ])
        return run_dir, manifest, seeds

    def test_reports_exposure_attrition_censors_and_no_investment(self):
        with tempfile.TemporaryDirectory() as temp:
            run_dir, manifest, seeds = self.make_run(Path(temp))
            report = analyzer.analyze(run_dir, manifest)
        self.assertIn("natural siege investment observed in 2/20 seeds; no investment in 18/20", report)
        self.assertIn("cannon shots: 2; powder paid: 0.50 L", report)
        self.assertIn("mismatches: 0", report)
        self.assertIn("actual wall breaches (unique siege pairs with reported integrity <= 0): 2 across 2 rows", report)
        self.assertIn("DEF-09 inactive | 1/20 | 2 | 16 | 1", report)
        self.assertIn("DEF-09 active | 1/20 | 1 | 4 | 1", report)
        self.assertIn("low exposure; descriptive", report)
        self.assertIn(f"{seeds[0]}", report)
        self.assertIn("no acquisition/purchase ledger", report)

    def test_rejects_tampered_raw_output(self):
        with tempfile.TemporaryDirectory() as temp:
            run_dir, manifest, seeds = self.make_run(Path(temp))
            output = run_dir / f"seed-{seeds[0]}.txt"
            output.write_text(output.read_text(encoding="utf-8") + "tampered\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "raw output hash mismatch"):
                analyzer.analyze(run_dir, manifest)

    def test_rejects_noncomplete_run(self):
        with tempfile.TemporaryDirectory() as temp:
            run_dir, manifest, _ = self.make_run(Path(temp))
            (run_dir / "run-status.jsonl").write_text(
                json.dumps({"status": "running", "seed_count": 20}) + "\n", encoding="utf-8"
            )
            with self.assertRaisesRegex(ValueError, "not a completed full cohort"):
                analyzer.analyze(run_dir, manifest)

    def test_rejects_provenance_binary_mismatch(self):
        with tempfile.TemporaryDirectory() as temp:
            run_dir, manifest, _ = self.make_run(Path(temp))
            freeze_path = run_dir / "run-freeze.json"
            freeze = json.loads(freeze_path.read_text(encoding="utf-8"))
            freeze["provenance"]["harness_sha256"] = "f" * 64
            write_json(freeze_path, freeze)
            with self.assertRaisesRegex(ValueError, "Harness artifact is missing or its SHA-256 differs"):
                analyzer.analyze(run_dir, manifest)


def write_jsonl(path: Path, rows) -> None:
    path.write_text("".join(json.dumps(row, sort_keys=True) + "\n" for row in rows), encoding="utf-8")


if __name__ == "__main__":
    unittest.main()
