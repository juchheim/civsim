# B5-13 current-candidate Release suite — 2026-11-27

The complete Release test suite passed on the clean closeout candidate `a966ec377a9d650b2b90b4467827c777e115c603` (tree `aa860512e8f97f6e1c35ebe5548e32f5159e73ef`, source aggregate `e0b43203a6622829816f85b924e986995bffd474a82a78ce819272025a117048`). The candidate remained clean after the run. The test assembly SHA-256 was `279035650540e27f496a123fa87fbfd5392ece252005ca8665b5aa9d9a457023`; the pack SHA-256 was `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`.

Command:

```sh
DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec dotnet test src/CivSim.Tests/CivSim.Tests.csproj --configuration Release --no-restore --logger 'console;verbosity=minimal'
```

Result: **1,706 passed, 0 failed, 0 skipped**, duration **7m 51s**. The full console output was returned by the test command; [test-result.txt](test-result.txt) preserves its terminal summary. The run overlapped the pinned WP-E5-12 cohort's replay-index verification, and still passed all timing-sensitive tests.

Strict content validation and content-pack, toy, research, and politics golden checks were separately passed on the same candidate in [the candidate golden report](../m12-e5-13-current-candidate-goldens-2026-11-27/README.md). This closes the B5-13 technical validation checks; owner review/merge approval remains a separate human decision.
