from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from analyze_fief_gov11_growth_effect import analyze


def growth(seed: int, figure: int, *, gov11: bool = True, control: int = 100, treatment: int = 110) -> str:
    return (
        f"wp7-gov11-growth seed={seed} season=100 civ=0 settlement=1 figure={figure} city=true "
        f"gov11={str(gov11).lower()} actualMode=treatment growthMultiplier=4724464025 actualConceptionChance={treatment} "
        f"actualOutcome=no_conception fertilityInput=429496729 controlConceptionChance={control} "
        f"treatmentConceptionChance={treatment} keyedRoll=2000000000 controlOutcome=no_conception "
        "treatmentOutcome=no_conception pairedBasis=same_figure_same_state_same_keyed_roll"
    )


class Gov11GrowthAnalyzerTests(unittest.TestCase):
    def test_passes_ten_percent_paired_growth_chance_with_twenty_seed_clusters(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            seeds = list(range(20_000, 20_020))
            (root / "seeds.txt").write_text("\n".join(map(str, seeds)) + "\n", encoding="utf-8")
            lines = []
            for seed in seeds:
                lines.extend((growth(seed, 1), growth(seed, 2)))
                lines.append(f"wp7-match-terminal seed={seed} status=complete\n")
            (root / "trace.log").write_text("\n".join(lines), encoding="utf-8")

            result = analyze([str(root / "trace.log")], str(root / "seeds.txt"))

        self.assertEqual("pass", result["status"])
        self.assertAlmostEqual(1.1, result["estimate"])
        self.assertEqual(40, result["nonzero_control_opportunities"])
        self.assertEqual([], result["seeds_without_exposure"])

    def test_non_chartered_rows_do_not_count_as_growth_exposure(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            seeds = list(range(20_000, 20_020))
            (root / "seeds.txt").write_text("\n".join(map(str, seeds)) + "\n", encoding="utf-8")
            lines = []
            for seed in seeds:
                lines.extend((growth(seed, 1, gov11=False), growth(seed, 2)))
                lines.append(f"wp7-match-terminal seed={seed} status=complete\n")
            (root / "trace.log").write_text("\n".join(lines), encoding="utf-8")

            result = analyze([str(root / "trace.log")], str(root / "seeds.txt"))

        self.assertEqual(20, result["nonzero_control_opportunities"])
        self.assertEqual(1.1, result["estimate"])

    def test_missing_match_terminal_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            seeds = list(range(20_000, 20_020))
            (root / "seeds.txt").write_text("\n".join(map(str, seeds)) + "\n", encoding="utf-8")
            (root / "trace.log").write_text("\n".join(growth(seed, 1) for seed in seeds), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "terminal output differs"):
                analyze([str(root / "trace.log")], str(root / "seeds.txt"))


if __name__ == "__main__":
    unittest.main()
