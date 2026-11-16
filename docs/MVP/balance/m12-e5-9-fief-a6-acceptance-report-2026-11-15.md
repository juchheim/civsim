# Fief A6 acceptance search — 2026-11-15

## Result

The run screened **10,009 distinct policies** and validated **16/16 diverse finalists** on three fresh mirrored pairs each, at 64 minutes in Fief mode. The highest finalist win rate against Warlord was **50.00%**, below the unchanged 55% ceiling. **A6 passes for this searched candidate set**; the result does not claim coverage of every possible fixed policy.

The full finalist and per-seed mirrored score report is [m12-e5-9-fief-a6-output-2026-11-15.txt](m12-e5-9-fief-a6-output-2026-11-15.txt), SHA-256 `591aa1bec627f326946944901cfed4317e7b75b9c0e579bd3289824cdad28238`. The complete generation log and console result are [m12-e5-9-fief-a6-run-log-2026-11-15.txt](m12-e5-9-fief-a6-run-log-2026-11-15.txt), SHA-256 `c3f0e8fea29934e44d9b6f4b7b0343748e93912025d89239b4751f4e88c179c5`.

## Frozen protocol and identities

- Seed and policy schedule: [m12-e5-9-fief-a6-run-schedule-2026-11-15.md](m12-e5-9-fief-a6-run-schedule-2026-11-15.md), SHA-256 `5484c7804c4ff0af08821f169360f8535c16359b29d29b92ea289ad8999ca780`.
- Screen and validation seeds: [m12-e5-9-fief-a6-seeds-2026-11-15.tsv](m12-e5-9-fief-a6-seeds-2026-11-15.tsv), SHA-256 `de2069b55ff16d22c607a3da0609370f4d398482b5f19da93b596fe785a86980`. Screen seed `21010001`; fresh mirrored validation seeds `21000001`–`21000003`.
- Policy-generation RNG seed: `20261019`; search size `417 × 24 + 1 = 10,009`; screen horizon 1 minute; finalist horizon 64 minutes.
- Candidate pack `/tmp/civsim-e5-fit-candidate.pack`, SHA-256 `a45d3e5163c791632825b654bd149061660baf33d38e8e498025397941bee399`.
- Frozen content+simulation-source aggregate: [m12-e5-9-fief-ai-source-aggregate-content-src-2026-11-15.json](m12-e5-9-fief-ai-source-aggregate-content-src-2026-11-15.json), SHA-256 `9ddb1947ec28df407e891015632b836cefd97f45a8169e8f8e92ed07a3a9cb0f`.
- Release Harness DLL SHA-256 `270f0d06007133553b9363167f24655aa3df128e03fd1578462e63a2a7938fca`.
- Adjacent Release Sim DLL SHA-256 `483e3f54e11cdb472aa50885eb6b640749876d80c9720faef35ea66c6e7d0605`.

The run used the `ai --mode fief --exploit` path. The command and every frozen input are recorded in the schedule. Renderer/UI edits made while the run was active were confined to `game/` and are excluded from the content+`src/` aggregate and the loaded Release artifacts.

No numeric catalog value or acceptance threshold was changed for this run.
