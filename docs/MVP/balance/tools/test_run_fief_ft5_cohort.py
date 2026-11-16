import unittest

import run_fief_ft5_cohort as runner


class FiefFt5CohortRunnerTests(unittest.TestCase):
    def test_harness_option_probe_accepts_ascii_and_dotnet_utf16le_literals(self):
        option = runner.FIEF_SIEGE_TRACE_OPTION

        self.assertTrue(runner.has_fief_siege_trace_option(option.encode("ascii")))
        self.assertTrue(runner.has_fief_siege_trace_option(option.encode("utf-16le")))

    def test_harness_option_probe_rejects_missing_option(self):
        self.assertFalse(runner.has_fief_siege_trace_option(b"unrelated harness literal"))


if __name__ == "__main__":
    unittest.main()
