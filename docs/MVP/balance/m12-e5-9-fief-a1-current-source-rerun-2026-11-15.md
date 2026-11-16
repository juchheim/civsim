# Fief A1 current-source acceptance rerun — 2026-11-15

**Status:** A1 passes on the current candidate. The frozen Warlord-versus-Council mirrored cohort produced 65 Warlord wins out of 80 matches (81.25%) against the unchanged ≥80% threshold, with zero censors and exit code 0.

## Frozen inputs

- Candidate HEAD: `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`.
- Current `content/src/game` aggregate: `7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6` (1,040 files); pre-run and post-run aggregates match.
- `content/src` aggregate: `bee3fb3a8f45d68010a649f76b71ecb1b191a884742eeb8118d2b90c64a69dba` (446 files).
- Numeric-status scratch pack SHA-256: `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`.
- Release Harness SHA-256: `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f`.
- Loaded adjacent `CivSim.Sim.dll` SHA-256: `a8cb7666042cf6651a6b9558b3d6db7b1f00566b5cc30041077e67b86540ad50` (same as canonical Release Sim DLL).
- Seed manifest: 40 seeds, `20261019`–`20261058`, SHA-256 `2c72ac0b30e6e556c1bff2ea4391798fbf32b3adbf1378750512c498ff35a45e`.
- Raw output SHA-256: `b47afe687083429a382c34e77a563c66797ef8ce764747ab764cbd86fac03544`.

## Reproduction

```sh
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll ai \
  --pack /private/tmp/civsim-fief-playtest-20261115-visual-refresh/numeric-e5.pack \
  --mode fief --duel warlord council --seeds 40 \
  --first-seed 20261019 --minutes 64 --match-details
```

Raw match details, seed manifest, and full JSON provenance are in [the run artifact directory](m12-e5-9-fief-a1-current-source-output-rerun-2026-11-15/). The companion A7 telemetry run on the same frozen inputs also passed: 80 matches / 160 AI seats, with all six telemetry counters at zero ([combined report](m12-e5-9-fief-a1-current-source-output-rerun-2026-11-15/report.md)). A1/A7 closure does not close A2–A6, A8, the Legend/duel requirements, or WP-E5-9 as a whole. All E5 rows remain draft.
