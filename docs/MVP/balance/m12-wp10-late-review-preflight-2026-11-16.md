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

For a local UI review, build a disposable numeric-status pack from a copied content tree. This route is for the Debug playtest only; it is not a pinned balance cohort or promotion artifact. Run from the repository root:

```sh
PLAYTEST_OUT="$(mktemp -d /tmp/civsim-wp10-late-review.XXXXXX)"
mkdir "$PLAYTEST_OUT/content-numeric-e5"
cp -R content/. "$PLAYTEST_OUT/content-numeric-e5/"
python3 - "$PLAYTEST_OUT/content-numeric-e5" <<'PY'
import sys
from pathlib import Path
sys.path.insert(0, "docs/MVP/balance/tools")
from build_e5_status_pack_pair import promote_e5_statuses

ids = promote_e5_statuses(Path(sys.argv[1]))
print(f"Enabled {len(ids)} E5 rows in the temporary content copy.")
PY
dotnet run --project src/CivSim.Cli -c Release -- build-pack \
  --content "$PLAYTEST_OUT/content-numeric-e5" \
  --out "$PLAYTEST_OUT/numeric-e5.pack"

godot-mono --headless --path game --script res://_wp10_userdata_probe.gd
godot-mono --headless --editor --path game --quit
dotnet build game/CivSim.Game.csproj -c Debug
CIVSIM_PACK="$PLAYTEST_OUT/numeric-e5.pack" \
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

## Superseding isolated live inspection — 2026-11-16

Completed a disposable live Debug pass from clean committed source `ceaadefd96063098830331cb5c496d37c76e2c58` (`codex/e5-fief`). The isolated detached worktree was `/private/tmp/civsim-wp10-live-20261116`; its source aggregate before adding the isolated user-dir probe/custom project setting was `57abb7fb20550cb2872efc223842c3723fefbdcec871262daabe22f207d634b8` over 1,050 files. This was the latest committed source at inspection start. The primary checkout and other active agents' artifacts were not used for building or running.

The isolated `project.godot` pointed to custom user data `/Users/ejuchheim/Library/Application Support/CivSim-WP10-LateReview-20261116-CEAadef`, confirmed at runtime by `OS.get_user_data_dir()`. A temporary numeric-status pack was built from a copy of the content tree; pack SHA-256 `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`. Godot `4.7.2.stable.mono.official.ed1daf0bf`; Debug build completed with 0 warnings/errors. Runtime command used `--fief-playtest --research-era=6 --seed=12345`; log confirms the inspection-only grant of 320 projects. No match cohorts ran.

At Year 34, People & Stores > Building rendered monastery, university, and hospital cards and their exact construction material requirements. The town had built none of these institutions; the UI reported material shortages (e.g. monastery short 7.5 stone blocks and 5.1 wood; university short 13.5 stone blocks and 3.1 wood; hospital short 1.1 wood). The learned/buildable panel also rendered correctly. The visible settlement hover card rendered; the detailed Shift card and an actually constructed late-age institution were not verified. This is live UI evidence of institutional options and gating, not evidence of completed institution construction.

Late-session persistence passed on this live session: F5 wrote a quick-save, F9 presented the confirmation, and confirming Load restored Year 34 Winter and the saved population 145 after the simulation had advanced to Year 35 Summer/population 152. Save SHA-256 `f12fbdddd090b807fecc915d82e1264f6e5f6f78ba52914203a9125afc5f0477`. Save replay showed “Replay saved”; Watch last replay entered the replay UI at Year 1 Spring with the REPLAY indicator. Replay SHA-256 `2b88d9a634b57e34850fa70819eeb161ead84fb24098cf1b6f81a6a899ab6c1c`. Both artifacts are copied to `/private/tmp/civsim-wp10-live-evidence-20261116/user-data-artifacts/`. Debug assemblies: `CivSim.Game.dll` `3162aeb8d2ad0d6d7ee3b9ba237ebe7f48b11896994ae5d2024c16a81f435ff8`; `CivSim.Sim.dll` `4d43b5b206e9a19687cafc7241fdb391ec6d78b9fc7c9f60aff66208dbe216ff`. Game log is `/private/tmp/civsim-wp10-live-evidence-20261116/logs/game.log`.

Remaining WP-E5-10 live evidence: show an actually constructed late-age institution and inspect the detailed Shift settlement card in a live session. The normal-speed simulation reached Year 34 but did not construct institutions because the material inventory stayed below the displayed requirements. The graphical inspection was performed through the desktop UI; screenshots are visible in the task transcript but were not exported as image files. Keep E5 statuses unchanged.

## Renderer fixture follow-up — 2026-11-16

To cover the unverified Shift renderer fields despite natural material shortages, I added a **temporary Debug-only inspection fixture in the disposable worktree** (only `game/Scripts/Game.cs` and `game/Scripts/Hud/Game.TileCard.cs`; no primary source changes). Launch flag `--dev-wp10-institution-card` fills only the published Fief presentation frame for the largest own settlement and pins its card; it does not mutate simulation/economy state or save data. Fixture values were monasteries 1, monks 4, universities 1, hospitals 1, concentric castle built, hospital staffed with 2 physicians/3 attendants and 1 ill/2 wounded/1 treated, plus 6 scholars, 2 Insight heads, and KEY-6 25%. This allows the renderer to be checked with every institution field populated while clearly remaining synthetic UI evidence.

Debug build with this inspection-only patch succeeded (0 warnings/errors). The captured screenshot `/private/tmp/civsim-wp10-live-evidence-20261116/screenshots/institution-card-shift.png` (SHA-256 `e159e374e5c474ade941cae82025683856e8299c888bc1e49cb425ddb3668876`) visibly confirms the detailed pinned settlement card renders the monastery, university, hospital, concentric castle, hospital staffing/patient, and scholar/KEY-6 lines with those fixture values. The patched `CivSim.Game.dll` SHA-256 is `1c8d2aa2797d313b910139ed677f83ca85f2b5cfd5474c08c80fff9a0c35d0e9`; Sim DLL unchanged at `4d43b5b206e9a19687cafc7241fdb391ec6d78b9fc7c9f60aff66208dbe216ff`.

This closes the renderer-field smoke check only. Actual late-age buildings still were not constructed in the natural session, so institutional construction/asset placement and the four live simulation values remain unverified. The quick-save/replay checks above were performed on the unmodified committed-source runtime before this temporary renderer-only fixture. Remove/ignore the isolated temporary diff when leaving the scratch checkout; do not copy it into product source or cite the fixture as a balance, buildability, or save/load claim.

## Actual-model structure renderer fixture and castle fix — 2026-11-16

A second disposable Debug-only fixture now seeds the **actual `LivingEconomyModel`** before its first published frame (`--dev-wp10-live-institutions`). It sets the model settlement's completed iron works to one each for `monastery`, `university`, `hospital`, `castle`, and `concentric_castle`, attempts to staff four monks, and places 100 each of G-BLOCK/G-ASHLAR/G-WOOD, 10 G-IRON, and 20 G-PLANK in that settlement. The log prints the `DEV ONLY` label. These are directly injected completed model counts and stocks; they do not represent naturally completed builds, consumed construction costs, or balance/save evidence. The renderer receives its normal FrameBuilder output from those model counts; unlike the preceding synthetic presentation fixture, the Fief card values are not overwritten after frame building.

The live pinned card from this fixture showed Monasteries 1, monks 4, universities 1, hospitals 1, and concentric castle built from model data. The same live frame placed the monastery, university, hospital, and castle meshes. Visual comparison exposed a product defect: the pre-fix frame drew the ordinary castle despite reporting a completed concentric-castle upgrade. Inspection found `art_fief_war.gd` defines `concentric_4` and `concentric_4_idle`, and the model/frame already carries the completed upgrade, but the normal `core_layout.gd` castle branch never selected those meshes, and its cached layout digest omitted concentric-castle changes.

The focused renderer patch was first prepared in detached worktree `/private/tmp/civsim-wp10-renderfix-20261116` at `ceaadefd96063098830331cb5c496d37c76e2c58`, then reviewed and applied to the `codex/e5-fief` checkout in `game/Renderer/LowPoly3D/Scripts/core_layout.gd` and `game/Renderer/LowPoly3D/Scripts/world_builder.gd`. Completed concentric castles now replace the ordinary castle mesh with `concentric_4` when the model reports soldiers and `concentric_4_idle` otherwise; the settlement layout digest includes the concentric work count/state so changes trigger a visual rebuild. The same patch was copied into the disposable inspection worktree for a live render check. The post-fix screenshot shows the added lower outer ring, round towers and pennants around the existing keep; the pinned UI still reports the model's institution counts. The live launch imported/ran these GDScript changes without parse/runtime errors, and a subsequent headless editor import of the target checkout completed successfully.

Evidence files:

- Pre-fix actual-model screenshot: `/private/tmp/civsim-wp10-live-evidence-20261116/screenshots/actual-sim-fixture-pre-fix.png`, SHA-256 `61ca3b39237afa9baecdafc9518156813ad7b4678e272d57fb740a6d4febb8e1`.
- Post-fix actual-model screenshot: `/private/tmp/civsim-wp10-live-evidence-20261116/screenshots/actual-sim-fixture-renderfix.png`, SHA-256 `27afb8def4b9c85ab91ce28ba4f830fe4fbc1267cb667292e04138dac85c6e95`.
- Dev-fixture launch log: `/private/tmp/civsim-wp10-live-evidence-20261116/logs/game-live-fixture.log`, SHA-256 `baae9cbb01eda04e5d2bbd5f15d025aa953806d58a1899a1f817df42d76154ce`.

This closes the visual mesh placement check using explicitly injected real model state and verifies the concentric castle fix in a live renderer. Natural institution construction, natural stock accumulation, and save/load of the injected fixture remain unverified; retain those boundaries in any WP-E5 gate decision. No E5 status was changed, and no balance cohorts were run.
