# WP-E5-10 late-age review preflight — 2026-11-16

## Status

The prior WP-E5-10 audit already covers the Year-1 Debug Fief route, save/load/replay, staged mesh gallery, and Release selector boundary. This preflight prepares the remaining live inspection of actual in-world monastery, university, hospital, and castle construction; Fief HUD and Shift settlement card; late-session save/load; and replay save/watch. Those checks remain **unverified** because the shared host was saturated by long-running AI cohorts and testhosts. No Godot import, game build, game launch, or primary-checkout test was run for this preflight.

## Source-copy identity and freshness

The inspection copy is `/tmp/civsim-wp10-late-review-20261116/repo`. Before isolation setup it matched a full filesystem hash over 1,199 `content/src/game` files, SHA-256 `e979249e551d9d7a0e23e5990e73cc2db2fb4a8d51c99936b87abfdb79ca49b0`. This aggregate is not the canonical `hash_e5_workspace.py` identity. The copy predates the WP-E5-7 and F-T8 trace patches now in the target tree; the canonical aggregate was `3091928a…` after WP7 and is now `2fae166f4f3078097d44b5ed4642b758abb2793a3e78c0903cfb4fc30384f34e` over 1,043 scoped files. The game/HUD files were not changed by these trace patches, but refresh the scratch copy and compute new hashes after remaining source changes settle, before final live review or persistence claims.

The scratch `game/project.godot` has a unique user-data directory configured:

```ini
config/use_custom_user_dir=true
config/custom_user_dir_name="CivSim-WP10-LateReview-20261116"
```

A small `OS.GetUserDataDir()` probe is staged in the scratch copy. The resolved path has not yet been checked at runtime. Before launching the game, run the probe and confirm it points to the unique scratch directory, separate from the user's ordinary `Godot/app_userdata/CivSim` state.

## Safe inspection route

Use only the Debug `--fief-playtest` path with a scratch numeric-status pack. `--research-era=6` queues the normal `CompleteProject` command for numeric projects in eras before 6, including Fief projects; it does not grant structures or represent natural research. Keystones are assigned to the era they enter, so KEY-6 stays incomplete and does not end the inspection match. Do not use `--start-minute` because `SimulationSession.CanSave` is false for those sessions. The `fief-gallery` is synthetic and hides the HUD, so it cannot cover this review.

When the host is idle and the scratch source copy is current:

```sh
python3 docs/MVP/balance/tools/build_e5_status_pack_pair.py \
  --content-root content \
  --output-dir /tmp/civsim-wp10-late-review-20261116/packs

godot-mono --headless --path game --script res://_wp10_userdata_probe.gd
godot-mono --headless --editor --path game --quit
dotnet build game/CivSim.Game.csproj -c Debug
CIVSIM_PACK=/tmp/civsim-wp10-late-review-20261116/packs/numeric-e5.pack \
  godot-mono --path game -- --fief-playtest --research-era=6 --seed=12345 --always-on-top
```

At minute 0, use `]` to advance speed, `[` to reduce speed, and Space or P to pause. Wait for ordinary simulation-driven construction and inspect actual structure counts. Hover an owned visible settlement and hold Shift, or pin it by clicking, to inspect mill power, grain coverage, monastery/monk, university, hospital, and concentric castle lines. Then save and reload the quick save, save a replay, and watch it. Record screenshots, in-world counts, save/replay outcomes, and exact source/pack/binary hashes. Do not report grant-unlocked projects as natural research progress.

## Remaining checks

- Verify the scratch `OS.GetUserDataDir()` runtime path before any save/replay action.
- Refresh the scratch source from the final candidate after WP-E5-7/WP-E5-8/F-T8 source work settles; the existing copy is pre-WP7 instrumentation.
- On an idle host, build/import and run the late-age route, then inspect actual institutions, detailed Shift card, save/load, and replay save/watch.
- Preserve ordinary/Release Fief selection gating until the WP-E5-11 promotion and integration gates pass.

## Restore regression found during late review

The 2026-11-16 live review reached Year 23, saved successfully, then failed quick-load with `Dynastic inheritance state has the wrong settlement count.` `MatchModel.ReadSnapshot()` restores Economy before Politics; Politics previously read its Fief settlement-indexed tail before refreshing its cached settlement count. `PoliticsSystem.ReadSnapshot()` now refreshes that cache immediately before reading the Bronze extension tail. `FiefDynasticInheritanceTests.Fief_snapshot_refreshes_settlement_count_before_reading_dynastic_state` adds a settlement, snapshots the match, restores it, and requires the restored settlement count and full match checksum to match. Focused Release results after the fix: Fief dynastic-inheritance tests 6/6 passed; SimulationSession save/replay tests 2/2 passed. The current Debug game build succeeds with 0 warnings and errors. The previous live scratch process used the pre-fix binary, so successful in-game quick-load/replay still needs a fresh live pass before WP-E5-10 can close.

## Superseding E5 readiness snapshot — 2026-11-16

Current diagnostic candidate: external `candidate-5ee659bd`, HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`, aggregate `5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733` (1,045 files), pack `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`, Release Harness `26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100`, Sim `b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe`. This advances the diagnostic snapshot beyond earlier 7dec/2fae notes, but is not a final acceptance freeze. Keep all E5 rows draft and the ordinary/Release selector closed. WP-E5-11 current-source acceptance remains unproven; WP-E5-12 reserved holdout remains unused, comprehension study unrun, Windows parity outstanding. The F-T1 calibration was active with 3/20 successful seed exits at time of check; WP7/F-T5/F-T2-F-T3 evidence is preflight only. F-T4 remains diagnostic.
