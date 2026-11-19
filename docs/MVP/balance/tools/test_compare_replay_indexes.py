import contextlib
import io
import tempfile
import unittest
from pathlib import Path

from compare_replay_indexes import compare, main


def artifact(checksum: str = "0123456789abcdef") -> dict:
    entry = {
        "seed": 42,
        "ticks": 2,
        "commandCount": 1,
        "checksumInterval": 1,
        "checksumCount": 2,
        "packHash": "a" * 64,
        "mode": "fief",
        "finalChecksum": "fedcba9876543210",
        "checksums": [{"tick": 1, "checksum": checksum}, {"tick": 2, "checksum": "fedcba9876543210"}],
        "verified": True,
    }
    return {"artifact": {"schema": "civsim-replay-index-v1", "packHash": "a" * 64}, "seeds": {42: entry}, "orderedSeeds": [42]}


class ReplayIndexComparisonTests(unittest.TestCase):
    def test_matching_indexes_compare_equal(self):
        self.assertEqual([], compare(artifact(), artifact(), [42]))

    def test_cross_host_comparison_checks_intermediate_tick_checksums(self):
        differences = compare(artifact(), artifact("1111111111111111"), [42])
        self.assertTrue(any("checksum differs at tick 1" in difference for difference in differences))

    def test_cross_host_comparison_detects_missing_seed(self):
        right = {"artifact": artifact()["artifact"], "seeds": {}, "orderedSeeds": []}
        self.assertIn("right index seed coverage/order differs", " ".join(compare(artifact(), right, [42])))

    def test_comparison_rejects_same_missing_seed_on_both_hosts(self):
        left, right = artifact(), artifact()
        left["orderedSeeds"] = right["orderedSeeds"] = [42]
        self.assertEqual([], compare(left, right, [42]))
        differences = compare(left, right, [42, 43])
        self.assertEqual(2, len(differences))
        self.assertTrue(all("missing=[43]" in difference for difference in differences))

    def test_comparison_rejects_same_extra_seed_on_both_hosts(self):
        left, right = artifact(), artifact()
        extra = dict(left["seeds"][42])
        extra["seed"] = 43
        for index in (left, right):
            index["orderedSeeds"] = [42, 43]
            index["seeds"][43] = extra
        differences = compare(left, right, [42])
        self.assertEqual(2, len(differences))
        self.assertTrue(all("extra=[43]" in difference for difference in differences))

    def test_cli_rejects_a_non_frozen_manifest(self):
        with tempfile.TemporaryDirectory() as temporary:
            manifest = Path(temporary) / "seeds.txt"
            manifest.write_text("42\n", encoding="ascii")
            stderr = io.StringIO()
            with contextlib.redirect_stderr(stderr):
                result = main(["left.json", "right.json", "--seeds-file", str(manifest)])
            self.assertEqual(2, result)
            self.assertIn("frozen manifest hash mismatch", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
