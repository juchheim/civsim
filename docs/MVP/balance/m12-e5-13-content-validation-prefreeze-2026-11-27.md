# WP-E5-13 current-candidate content validation — 2026-11-27

This is candidate-specific content validation evidence for `8774ebdfa7dfe078fbcf366390bd5d830db6769`. The 50 E5 rows are already `numeric` on this candidate, introduced with the standard Fief enablement in commit `b487721`. A current-source catalog audit confirmed 50/50 E5 rows are numeric. These checks therefore exercise the promoted E5 content state; they do not by themselves close B5-13.

## Results

- Strict content validation: **509 projects + 10 keystones**, 0 errors, 0 warnings; exit 0.
- Content-pack golden check: **passed** against `tests/golden/content-pack.txt`.
- Built numeric pack: 427,132 bytes; SHA-256 `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`.
- Release CLI SHA-256: `ec43ccb217bdee4bec9957319e8d68b660807a367cf8e54618b44b822acb97e2`.
- E5 status audit: 50 rows, all numeric. Other-era draft rows account for the 156 draft entries in the global validation summary.

Commands:

```sh
dotnet src/CivSim.Cli/bin/Release/net10.0/civsim.dll validate --content content --strict
dotnet src/CivSim.Cli/bin/Release/net10.0/civsim.dll build-pack \
  --content content \
  --out /private/tmp/e5-closeout-acceptance-8774-ai/numeric-e5.pack \
  --check-golden tests/golden/content-pack.txt
```

Candidate identity remained HEAD `8774ebdfa7dfe078fbcf366390bd5d830db6769a`, tree `b76ab434580f7ebbf77d0799c92cce5b255e3f49`, source aggregate `840c1bf2d8824815b9f1ef11b9768c2f28b39812d9fb7c0e3b4ba49008bd914a`. The pack matches the frozen numeric-status acceptance pack. Full Release tests and the remaining technical gates are still required for B5-13/WP-E5-11 closeout.
