# Fief A1 post-fix rerun — 2026-11-15

## Result

The frozen Warlord-versus-Council mirrored cohort scored **65/80 wins (81.25%)**, passing the unchanged 80% A1 threshold. Raw match details are in [m12-e5-9-fief-a1-post-fix-2026-11-15.txt](m12-e5-9-fief-a1-post-fix-2026-11-15.txt), SHA-256 `39d11a6421892b8baa024a6879647d11ea6736d59a82883f6f7994fb6cd26ed2`.

The cohort used seeds `20261019`–`20261058`, with both mirrored layouts per seed (80 matches). The exact seed list is [m12-e5-9-fief-a1-seeds-2026-11-15.tsv](m12-e5-9-fief-a1-seeds-2026-11-15.tsv), SHA-256 `2c72ac0b30e6e556c1bff2ea4391798fbf32b3adbf1378750512c498ff35a45e`. The list is a representation of the documented contiguous range; the range was already specified in the prior acceptance protocol.

Command:

```sh
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll ai \
  --pack /tmp/civsim-e5-ai-acceptance.pack --mode fief \
  --duel warlord council --seeds 40 --first-seed 20261019 \
  --minutes 64 --match-details
```

## Executed identities

- Acceptance pack: `/tmp/civsim-e5-ai-acceptance.pack`, SHA-256 `e1ce35fd2af715db9cbf0a66c81372a95bf05ef19296248454609fc77f7bc3f2`.
- Release Harness DLL: SHA-256 `270f0d06007133553b9363167f24655aa3df128e03fd1578462e63a2a7938fca`.
- Adjacent Release Sim DLL loaded by the run: SHA-256 `cf6ad9ce86e9286c9bd3b47b996ec221d32265a1706ce2058a032736d7b96d21`.
- The run included the Fief-only normalized-intent GOV-02 posture-unlock valuation correction in `ResearchSpark`.

The workspace source aggregate was not captured before this run. A later Release rebuild produced a different Sim DLL (`483e3f54e11cdb472aa50885eb6b640749876d80c9720faef35ea66c6e7d0605`) and the current source aggregate is `1fa84935f83c4175443d4d9a7ed4905c88def9190e326e0d7cc23edb0127cbfd`; neither is claimed as the source identity for this A1 run. The raw output and executed binary hashes preserve the result, but this provenance gap means a strict current-source A1 closeout should rerun on the final integrated source aggregate.

The measured win rate is above threshold on the executed binary. The raw log also reports 5,000 idle-slot seasons; that is outside A1’s win-rate classification and should be checked under the separate A7 gate if its cohort is rerun.

## Current-source closeout status

The strict source aggregate for the run above is unavailable because the workspace aggregate was not captured before execution, and the later rebuild produced a different Sim DLL. The repository owner requested that A1 not be rerun until the F-T7 source-level route-carriage fix and remaining source changes settle. A current-source attempt was started but interrupted immediately after that instruction, before any cohort output; it is not acceptance evidence. The 80-match current-source rerun remains pending owner request after the source freeze. Do not attribute this report's 81.25% result to the later source aggregate.
