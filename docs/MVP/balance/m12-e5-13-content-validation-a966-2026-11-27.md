# WP-E5-13 content validation — candidate a966ec3

Candidate `a966ec377a9d650b2b90b4467827c777e115c603`, tree `aa860512e8f97f6e1c35ebe5548e32f5159e73ef`, and `content/src/game` aggregate `e0b43203a6622829816f85b924e986995bffd474a82a78ce819272025a117048` were checked after the A8 test-fixture correction.

- Strict validation: **509 projects + 10 keystones**, 156 other-era draft rows, **0 errors and 0 warnings**.
- Content-pack golden check: **passed**.
- Numeric pack: 427,132 bytes, SHA-256 `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`.
- Release CLI SHA-256: `1f5ad72e9ff7332c4f27d66934ab10b9633f9b49458e741923c59ae6e5edd1e8`.
- E5 catalog status: all 50 rows are numeric.

Commands:

```sh
dotnet src/CivSim.Cli/bin/Release/net10.0/civsim.dll validate --content content --strict
dotnet src/CivSim.Cli/bin/Release/net10.0/civsim.dll build-pack \
  --content content \
  --out /private/tmp/e5-closeout-acceptance-a966-technical/numeric-e5.pack \
  --check-golden tests/golden/content-pack.txt
```

This closes the current-candidate content validation and golden checks, not B5-13 overall. The full Release test suite remains required.
