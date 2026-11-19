import tempfile
import unittest
from pathlib import Path

import analyze_fief_ft5_trace as analyzer


class FiefFt5AnalyzerTests(unittest.TestCase):
    def test_terminal_records_and_observation_gates_are_reported(self):
        manifest = analyzer.DEFAULT_MANIFEST
        seeds = analyzer.load_manifest(manifest)
        with tempfile.TemporaryDirectory() as directory:
            raw_dir = Path(directory)
            for seed in seeds:
                path = raw_dir / f"seed-{seed}.txt"
                path.write_text("", encoding="utf-8")
            (raw_dir / f"seed-{seeds[0]}.txt").write_text(
                "\n".join([
                    f"fief-siege-trace seed={seeds[0]} season=2 attacker=0 defender=1 settlement=4 siegeTurn=1 cannonShots=2 powderPaid=0.50 exposed=true powderBreach=true outcome=active attritionAssignedCrews=2 attritionDeaths=1 def09Active=true",
                    f"fief-siege-trace seed={seeds[0]} season=3 attacker=0 defender=1 settlement=4 siegeTurn=2 cannonShots=0 powderPaid=0.00 exposed=false powderBreach=false outcome=active attritionAssignedCrews=0 attritionDeaths=0 def09Active=false",
                    f"fief-siege-trace seed={seeds[0]} season=4 attacker=0 defender=1 settlement=4 siegeTurn=terminal cannonShots=unavailable powderPaid=unavailable outcome=investmentcapitulated",
                    f"fief-siege-terminal seed={seeds[0]} season=5 exposure=observed settlements=1",
                    f"fief-siege-terminal-inventory seed={seeds[0]} civ=0 cannonStock=2.00 powderStock=1.50",
                    f"fief-siege-terminal-settlement seed={seeds[0]} settlement=4 attacker=0 defender=1 stoneWalls=true gatehouse=true castle=true castleGarrisoned=true concentricCastle=true integrity=0.00 powderBreach=true outcome=investmentcapitulated censorReason=none",
                ]) + "\n", encoding="utf-8")
            (raw_dir / f"seed-{seeds[1]}.txt").write_text(
                f"fief-siege-terminal seed={seeds[1]} season=5 exposure=none reason=no_siege_investment_observed\n",
                encoding="utf-8")
            report = analyzer.analyze(raw_dir, manifest)

        self.assertIn("Terminal exposure: 1 observed, 1 no-exposure", report)
        self.assertIn("Shot/powder conservation (0.25 L per shot): 2 comparable seasonal rows; 0 mismatches", report)
        self.assertIn("Powder-breach gate: 1 seasonal breach rows; 0 breach rows without a paid shot", report)
        self.assertIn("attacked settlements: 1 (0 right-censored, 1 with terminal powder breach)", report)
        self.assertIn("stoneWalls=1, gatehouse=1, castle=1, castleGarrisoned=1, concentricCastle=1", report)
        self.assertIn(f"| {seeds[0]} | 0 | 2.00 | 1.50 |", report)
        self.assertIn("investmentcapitulated | none | 0.00 | true | true | true | true | true | true", report)
        self.assertIn("Assigned adult-seasons | Observed deaths", report)
        self.assertIn("Rows with missing/unknown DEF-09 flag or attrition assignment count: 0", report)


if __name__ == "__main__":
    unittest.main()
