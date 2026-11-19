# Fief A6 frozen run schedule — 2026-11-15

## Frozen identities

- Acceptance: screen at least 10,000 distinct policies; validate the selected 16 diverse finalists against Warlord on three fresh mirrored 64-minute seed pairs each; A6 passes only when no finalist exceeds 55%.
- Policy-generation RNG seed (`--first-seed`): `20261019`.
- Seed manifest: [m12-e5-9-fief-a6-seeds-2026-11-15.tsv](m12-e5-9-fief-a6-seeds-2026-11-15.tsv), SHA-256 `de2069b55ff16d22c607a3da0609370f4d398482b5f19da93b596fe785a86980`.
- Screen world: `21010001`; validation worlds: `21000001`–`21000003`. These ranges are disjoint.
- Policy count: `417 × 24 + 1 = 10,009`; screen horizon 1 minute; validation horizon 64 minutes; Fief chapter.
- Candidate pack: `/tmp/civsim-e5-fit-candidate.pack`, SHA-256 `a45d3e5163c791632825b654bd149061660baf33d38e8e498025397941bee399`. This is the existing WP8D AGR-11 scratch pack; its prior provenance record is [m12-wp8d-d20-integrated-release-pre-knowhow-validation-2026-11-15.md](m12-wp8d-d20-integrated-release-pre-knowhow-validation-2026-11-15.md). The pack is not regenerated or modified for this run.
- Frozen content and simulation source aggregate (the A6-relevant content/`src/` scope): [m12-e5-9-fief-ai-source-aggregate-content-src-2026-11-15.json](m12-e5-9-fief-ai-source-aggregate-content-src-2026-11-15.json), SHA-256 `9ddb1947ec28df407e891015632b836cefd97f45a8169e8f8e92ed07a3a9cb0f`.
- Full workspace aggregate at launch, including `game/`: [m12-e5-9-fief-ai-source-aggregate-2026-11-15.json](m12-e5-9-fief-ai-source-aggregate-2026-11-15.json), SHA-256 `1fa84935f83c4175443d4d9a7ed4905c88def9190e326e0d7cc23edb0127cbfd`. Subsequent changes reported by the repository owner are confined to Godot renderer/UI files under `game/`; they are outside this run’s content/`src/` scope and were not built into or loaded by the A6 Release process.
- Release harness DLL SHA-256: `270f0d06007133553b9363167f24655aa3df128e03fd1578462e63a2a7938fca`.
- Adjacent Release Sim DLL SHA-256: `483e3f54e11cdb472aa50885eb6b640749876d80c9720faef35ea66c6e7d0605`.

The previous 2026-11-14 seed schedule and 2026-11-15 preflight remain preserved. This dated schedule explicitly binds the run to the present pack and source aggregate.

## Command

```sh
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll ai \
  --pack /tmp/civsim-e5-fit-candidate.pack --mode fief --exploit \
  --generations 417 --children 24 --seeds 1 --screen-minutes 1 --minutes 64 \
  --first-seed 20261019 \
  --exploit-screen-first-seed 21010001 \
  --exploit-validation-first-seed 21000001 \
  --exploit-output docs/MVP/balance/m12-e5-9-fief-a6-output-2026-11-15.txt
```

Status: completed. The run screened 10,009/10,009 distinct candidates and validated 16/16 finalists. The maximum validated rate was 50.00%; A6 passes.
