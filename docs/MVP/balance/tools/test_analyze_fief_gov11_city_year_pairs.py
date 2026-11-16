import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from analyze_fief_gov11_city_year_pairs import analyze


def pair_row(seed, settlement, treatment_end=110, control_end=100):
    return (
        "wp7-gov11-city-year-pair "
        f"seed={seed} year=2 civStart=0 settlement={settlement} "
        "treatmentStartChecksum=0123456789abcdef controlStartChecksum=0123456789abcdef "
        "sameStartingState=true treatmentPopulationStart=100 controlPopulationStart=100 "
        "treatmentGov11Start=true controlGov11Start=true treatmentEligibleAtStart=true controlEligibleAtStart=true "
        f"treatmentPopulationEnd={treatment_end} controlPopulationEnd={control_end} "
        "treatmentGov11End=true controlGov11End=true treatmentLiveEnd=true controlLiveEnd=true "
        "treatmentEligibleAllYear=true controlEligibleAllYear=true observedSeasons=4 "
        "pairedBasis=same_snapshot_four_season_branch\n"
    )


def terminal_row(seed):
    return f"wp7-match-terminal seed={seed} season=130 matchStatus=horizon_censored endReason=horizon chapterEnded=false horizonReached=true\n"


class AnalyzeGov11CityYearPairsTests(unittest.TestCase):
    def test_pairs_full_year_rows_and_clusters_bootstrap_by_seed(self):
        with tempfile.TemporaryDirectory() as directory:
            traces = Path(directory) / "pairs.log"
            traces.write_text("".join(terminal_row(seed) + pair_row(seed, 0) for seed in range(20)), encoding="utf-8")
            result = analyze([str(traces)], bootstrap_replicates=200)
        self.assertEqual("pass", result["status"])
        self.assertEqual(20, result["paired_city_years"])
        self.assertEqual(20, result["independent_match_seeds"])
        self.assertEqual(20, result["seed_accounting"]["observed_terminal_seeds"])
        self.assertEqual([], result["seed_accounting"]["seeds_without_eligible_pairs"])
        self.assertAlmostEqual(1.1, result["effect_estimate"])
        self.assertLessEqual(result["match_clustered_bootstrap_ci95"][0], 1.1)
        self.assertGreaterEqual(result["match_clustered_bootstrap_ci95"][1], 1.1)

    def test_fewer_than_twenty_pairs_is_inconclusive(self):
        with tempfile.TemporaryDirectory() as directory:
            traces = Path(directory) / "pairs.log"
            traces.write_text(terminal_row(1) + "".join(pair_row(1, year) for year in range(20)), encoding="utf-8")
            result = analyze([str(traces)], bootstrap_replicates=10)
        self.assertEqual("inconclusive", result["status"])
        self.assertEqual(20, result["paired_city_years"])
        self.assertEqual(1, result["independent_match_seeds"])
        self.assertIsNone(result["match_clustered_bootstrap_ci95"])

    def test_excluded_and_censored_rows_are_counted_but_never_paired(self):
        with tempfile.TemporaryDirectory() as directory:
            traces = Path(directory) / "pairs.log"
            traces.write_text(
                terminal_row(1) + "wp7-gov11-city-year-pair-excluded seed=1 exclusionReason=control_settlement_lost\n"
                "wp7-gov11-city-year-pair-censored seed=1 censorReason=match_end\n", encoding="utf-8")
            result = analyze([str(traces)], bootstrap_replicates=10)
        self.assertEqual("inconclusive", result["status"])
        self.assertEqual(0, result["paired_city_years"])
        self.assertEqual(1, result["trace_counts"]["excluded:control_settlement_lost"])
        self.assertEqual(1, result["trace_counts"]["censored"])

    def test_mismatched_start_checksums_fail_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            traces = Path(directory) / "pairs.log"
            traces.write_text(pair_row(1, 0).replace("controlStartChecksum=0123456789abcdef", "controlStartChecksum=ffffffffffffffff"), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "shared starting checksum"):
                analyze([str(traces)], bootstrap_replicates=10)

    def test_missing_or_malformed_start_checksums_fail_closed(self):
        for malformed in ("", "not-a-checksum", "1234"):
            with self.subTest(malformed=malformed), tempfile.TemporaryDirectory() as directory:
                traces = Path(directory) / "pairs.log"
                row = pair_row(1, 0).replace("treatmentStartChecksum=0123456789abcdef", f"treatmentStartChecksum={malformed}")
                row = row.replace("controlStartChecksum=0123456789abcdef", f"controlStartChecksum={malformed}")
                traces.write_text(row, encoding="utf-8")
                with self.assertRaisesRegex(ValueError, "shared starting checksum"):
                    analyze([str(traces)], bootstrap_replicates=10)

    def test_expected_seed_manifest_includes_no_exposure_and_fails_closed_for_missing_terminal(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            traces = root / "pairs.log"
            traces.write_text(terminal_row(1) + terminal_row(2), encoding="utf-8")
            expected = root / "seeds.txt"
            expected.write_text("1\n2\n3\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "missing=\\['3'\\]"):
                analyze([str(traces)], expected_seeds_path=str(expected), bootstrap_replicates=10)

            expected.write_text("1\n2\n", encoding="utf-8")
            result = analyze([str(traces)], expected_seeds_path=str(expected), bootstrap_replicates=10)
            self.assertEqual(2, result["seed_accounting"]["expected_seeds"])
            self.assertEqual(2, result["seed_accounting"]["observed_terminal_seeds"])
            self.assertEqual(["1", "2"], result["seed_accounting"]["seeds_without_eligible_pairs"])


if __name__ == "__main__":
    unittest.main()
