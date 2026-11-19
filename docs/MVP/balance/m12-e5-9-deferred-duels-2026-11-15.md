# WP-E5-9 deferred current-candidate duels — run preparation

Prepared while the current host is busy. **Do not launch until the existing WP9 jobs finish, other heavy simulation cohorts are idle, and a fresh preflight confirms the exact candidate inputs.** If A8 diagnosis results in any code or test change, rebuild and freeze that new candidate first; do not point these commands at the previous frozen assembly.

## Frozen seed inputs

Each run uses the documented 40-seed range `20261019`–`20261058`. The harness evaluates both mirrored seatings per seed, giving 80 matches per run. The manifests are copied from the accepted WP9 seed schedule and SHA-256 checked:

| Gate | Manifest | SHA-256 |
|---|---|---|
| A4 Chieftain–Warlord | [A4 seed manifest](m12-e5-9-deferred-duels-2026-11-15/a4-chieftain-warlord-seeds.tsv) | `2c72ac0b30e6e556c1bff2ea4391798fbf32b3adbf1378750512c498ff35a45e` |
| B5-10 Legend–Warlord | [B5-10 seed manifest](m12-e5-9-deferred-duels-2026-11-15/b5-10-legend-warlord-seeds.tsv) | `2c72ac0b30e6e556c1bff2ea4391798fbf32b3adbf1378750512c498ff35a45e` |
| A2 Warlord–Builder / Warlord–Wanderer | [Completed Grower cohort seed manifest](m12-e5-9-a2-grower-current-source-2026-11-15/seeds.tsv), same 40-seed schedule | `2c72ac0b30e6e556c1bff2ea4391798fbf32b3adbf1378750512c498ff35a45e` |

The currently documented frozen numeric-status pack is `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`, harness `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f`, and Sim `a8cb7666042cf6651a6b9558b3d6db7b1f00566b5cc30041077e67b86540ad50`. Verify them again immediately before running. Set `HARNESS` and `PACK` only to the exact newly verified Release Harness and frozen pack.

## Commands to run serially after idle

Use a unique output directory for each gate and run the commands serially. Preserve the exact command and full stdout/stderr, exit code, raw SHA-256, source aggregate, HEAD, pack hash, Harness hash, Sim hash, seed-manifest hash, censors and errors in each gate's `run_metadata.json`.

```sh
dotnet "$HARNESS" ai --pack "$PACK" --mode fief \
  --duel warlord builder --seeds 40 --first-seed 20261019 \
  --minutes 64 --match-details
```

```sh
dotnet "$HARNESS" ai --pack "$PACK" --mode fief \
  --duel warlord wanderer --seeds 40 --first-seed 20261019 \
  --minutes 64 --match-details
```

These complete the remaining A2 opponents after the current-source Grower leg passed 62/80 (77.5%). Each run needs at least 70% Warlord wins in 80 mirrored matches. Keep them separate and run serially.

```sh
dotnet "$HARNESS" ai --pack "$PACK" --mode fief \
  --duel chieftain warlord --seeds 40 --first-seed 20261019 \
  --minutes 64 --match-details
```

This reports Chieftain's share against Warlord. The A4 band remains 25–40%.

```sh
dotnet "$HARNESS" ai --pack "$PACK" --mode fief \
  --duel legend warlord --seeds 40 --first-seed 20261019 \
  --minutes 64 --match-details
```

This is the separate B5-10 difficulty check: at least 60% Legend wins in 80 mirrored duels. Preserve every match result and the expected/actual denominator for both runs. A nonzero exit or any censor/error must remain visible; do not summarize a partial cohort as a pass.

The commands are preparation only. No simulation was started from this report.
