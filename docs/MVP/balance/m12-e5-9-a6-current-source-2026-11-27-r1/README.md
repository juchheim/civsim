# A6 exploit search — current E5 candidate 2fce6aa

**Result: PASS for the searched policy set.** The frozen Fief run screened 10,009 unique policies, retained 16 finalists, and validated all 16 on three fresh mirrored seed pairs each at 64 simulated minutes. No finalist exceeded the 55% exploit threshold. This is evidence about the searched candidate set, not every possible policy.

The run used candidate `2fce6aa29eab5d75e0cd6018c44d6ea6d5a65b78`, tree `de5be3c634f2b806c87f0b9ea49cde8352f0f4d5`, source aggregate `227902576ec15604a8399d395b170d55305ad0e50823e30fd49087d8f742342b`, the pinned numeric E5 pack, and the adjacent Release Harness/Sim pair. The exact command and hashes are in [run metadata](run_metadata.json).

- [Search stdout](raw-search-log.txt), SHA-256 `1f826a7ece7d4c1915249b5475e34a5d4e427f6bd0f0247f3bbb801fc9bc74cb`
- [Finalist and validation output](finalist-validation.txt), SHA-256 `880b39bfcde6439d105e3cab3184306c859f7bb2be6a9603c6ba818bec01d001`

The raw output includes the explicit Harness result `EXPLOIT SEARCH PASS` and every finalist's three mirrored validation pairs. The invoking shell's exit code was not retained; the pass disposition is based on the completed raw Harness output.
