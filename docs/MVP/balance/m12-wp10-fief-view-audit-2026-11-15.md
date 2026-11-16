# WP-E5-10 Fief view and persistence audit (2026-11-15)

## Result

The existing WP-E5-10 implementation covers the requested Fief readout and mode persistence paths, with one HUD countdown-visibility correction made during this audit. Fief remains unavailable to new-match selection: `ReadChapter()` accepts only the development Bronze override and otherwise starts Iron. Loading a save or replay already tagged Fief remains supported; this does not create a start selector or bypass the WP-E5-11 promotion gate.

## Verified paths

- `MatchFrame.Fief` is copied into the settlement payload only when the frame's Fief block is active. `world_data.gd` returns an empty Fief record for non-Fief frames. The payload includes own settlement mill demand/use, hospital staffing and patient/treatment counts, university staffing/research contribution, KEY-6 progress, and optional building guidance.
- The Shift settlement card renders these fields only for active Fief frames and the viewer-owned settlement row. The Fief-specific top bar labels the age as `FIEF MODE · <AGE> AGE`.
- The low-poly renderer keys rebuilds on the Fief mode bit and the three Fief institutions, and adds those institutional props only for Fief frames.
- Version-5 Bronze quick saves retain their existing format. Version-6 quick saves store a non-Bronze mode tag and restore it through `ChapterRules.For`; unsupported mode bytes are rejected. Replay saves append an optional mode tag after difficulty. Older replay footers without a tag continue to load as Bronze.
- Replay watch passes the decoded mode to `SimulationSession`; loaded and watched Fief sessions retain Fief rules and identify their mode in the user-facing age label/toast.
- The rival-era countdown toast now checks `ChapterRules.HasRivalCountdown`; Fief still gets the ordinary rival age news, but no false “years left” countdown toast. The season label also remains on its normal season display when the session reports no countdown.

## Checks

- `dotnet test src/CivSim.Tests/CivSim.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~IronViewTests.Fief_frame_exposes_power_and_fief_state_only_for_owned_settlements|FullyQualifiedName~GameSaveModeTests|FullyQualifiedName~SimulationSessionSaveTests'` — passed, 20 tests after the countdown correction.
- The broader mode/research Release filter passed 41 tests; `dotnet build game/CivSim.Game.csproj -c Release --no-restore` passed with 0 warnings and 0 errors.
- Static review confirmed there is no Fief branch in the new-match chapter selection path. Save/replay restoration is the only game UI path that can enter Fief at present.

The audit found that `ProcessEndTimeline` selected the rival countdown toast using `IsIron`, which is also true for Fief, and could therefore display a countdown after a rival reached KEY-5. The HUD gate now follows the chapter rule, with `GameSaveModeTests.Rival_countdown_toast_visibility_follows_chapter_rules` covering Bronze/Iron/Fief. The updated focused tests and Release game build pass.

## Remaining review boundary

Live visual review of an already-loaded Fief HUD and renderer remains outstanding. The current game has no Fief start selector, and this audit did not add one or manufacture a user-facing route around the WP-E5-11 gate. The renderer reuses the existing civic mesh kit; bespoke Fief art and dedicated windmill/blast-furnace forms remain outside this slice. WP-E5-11 content promotion and subsequent playability/comprehension review remain separate gates.

No simulation rules, catalog statuses, golden outputs, shared selection logic, or broad simulation cohorts were changed or run for this audit.

## Isolated Debug playtest smoke (2026-11-15)

To keep Godot import and Debug build artifacts away from the active source-sensitive cohorts, the exact E5 working tree was copied to `/tmp/civsim-wp10-playtest-20261115/repo`, excluding only `.git`, `bin`, `obj`, and `.godot` directories. The pre-copy and post-copy primary `content/src/game` aggregate remained `7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6` (1,040 files); all 1,040 scoped source file hashes matched in the scratch copy. No build, import, or launch was run in the primary checkout.

The documented status-pair helper ran against the scratch copy:

```sh
python3 docs/MVP/balance/tools/build_e5_status_pack_pair.py \
  --content-root /tmp/civsim-wp10-playtest-20261115/repo/content \
  --output-dir /tmp/civsim-wp10-playtest-20261115/packs
```

It audited exactly 50 differences, all E5 project status fields. `draft.pack` SHA-256 is `bf208c3cc96f020d0e2bff2498dc4dc5717e28d9d309c6e359373a3ff86a132a`; `numeric-e5.pack` SHA-256 is `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`, matching the already verified pack.

The scratch-only validation commands and results were:

- `godot-mono --headless --editor --path game --quit` — exit 0. Godot registered 106 script classes and reimported 196 assets. It emitted the existing warning that `res://prototypes/lowpoly3d/project.godot` is ignored as a nested project; no import errors were reported.
- `dotnet build game/CivSim.Game.csproj -c Debug` — passed with 0 warnings and 0 errors. Debug `CivSim.Game.dll` SHA-256: `5b604c447a755167777ae1d1468fda547c66e8d488e8a735c745aaf36b3e37ee`; `CivSim.Sim.dll` SHA-256: `75eabcf379d55f594e950b509cf316b3c0c85ddf7ab126bcc3a924fdd16cb672`.
- `CIVSIM_PACK=/tmp/civsim-wp10-playtest-20261115/packs/numeric-e5.pack godot-mono --path game -- --fief-playtest --seed=12345 --screenshot=/tmp/civsim-wp10-playtest-20261115/fief-playtest.png` — exit 0; screenshot SHA-256 `27a28485388bfb521947d783b1027778dfc8f3bd81fb871ddafda058312cb474`. The image shows the `FIEF · DRAFT` age chip, the Fief start state, and the research UI with seven new choices. It starts at Year 1 with one settlement and 12 people, so the monastery, university, and hospital props are not yet constructed and their project-gated renderer path is not demonstrated.

The screenshot launch logged repeated `burn_map` global shader texture warnings and exit-time renderer cleanup diagnostics (leaked CanvasItem, shader, RID, and resource allocations). The scene rendered and the screenshot was saved; these shutdown diagnostics remain for engineering review. The headless import's nested-project warning is known and did not prevent script registration or asset import.

An interactive scratch-copy Fief session was opened for review, launched with `CIVSIM_PACK=/tmp/civsim-wp10-playtest-20261115/packs/numeric-e5.pack godot-mono --path game -- --fief-playtest --seed=12345` (process PID 29075; Codex exec session 12096). A live UI pass confirmed the Fief-only debug banner, the seven new research choices, and the pinned own-settlement card with mill-power and institution readouts. Saving showed `Game saved`; loading the quick-save returned to Year 1 and showed `Game loaded`, retaining the Fief session. A five-simulated-minute Fief replay saved and watched successfully; the window stayed in Fief playtest mode and showed `Watching a replay · Fief mode`. The pre-existing `latest.csr` was backed up and restored with its original SHA-256 `574a582a300031fa054d1f880b2b44164de0fa0deaeffb4596f99dd9ef74811a`. The card displayed `Claims: You 255%` on the home tile; this is the current saturated claim-strength display and merits a later wording check. This row remains **partial**: the game has not yet reached E5 institutions, so the monastery/university/hospital/castle renderer props, full Fief research detail, Shift-held card behavior, and late-session save/load paths remain unreviewed. The scratch process was later stopped after preserving its process state; no repository catalog statuses were promoted.

## Release-path static recheck (2026-11-15)

This follow-up checked the release boundary without building or launching another game while shared simulation cohorts were active. The `--fief-playtest` title-screen bypass in `Startup.IsDevelopmentPresentationRun`, the startup chapter choice in `Game.ReadChapter`, and the new-match override in `Game.Session.StartNewMatch` are each enclosed in `#if DEBUG`. In a Release compilation those branches are absent: the ordinary route still selects Iron (with the existing Bronze command-line override), and the development Fief flag cannot turn an ordinary new match into Fief. The project export presets are the existing macOS Universal and Windows x64 presets; neither adds a Fief selector or mode override. This is source-level verification, not a new Release export test.

The intentional persistence distinction remains: a save with a valid version-6 Fief mode tag restores as Fief, and a replay with a valid Fief footer watches as Fief. Quick-save loading checks the current content-pack hash and rejects malformed mode tags or trailing bytes; replay watching uses the current pack and the replay's content-hash check in `SimulationSession`. This keeps saved/replay compatibility separate from new-match availability.

No safe code gap was established, so this audit made no production code change and ran no tests/builds. Remaining WP-E5-10 review is the live later-age visual pass: reach actual Fief institution construction, inspect the monastery/university/hospital/castle props and detailed Shift card, then save/load and watch a replay at that state. The existing Year-1 scratch playtest does not cover those visuals. Ordinary/release Fief selection remains gated until WP-E5-11 promotes the complete 50-row age and clears its acceptance checks; human comprehension/playtesting remains WP-E5-12.

## Fresh isolated import, build, and visual asset check (2026-11-15)

The user-visible discrepancy was not missing visual source in the current working tree or a failed Godot import. Comparison showed that visual files/assets present in the dirty `codex/e5-fief` worktree byte-match `main`, while the committed branch HEAD predates those files. A clean checkout at that branch HEAD will therefore lack them. The current worktree also contains the uncommitted source/assets, so keep those edits intact. A scratch copy of that current source was imported and built successfully (106 script classes registered, 196 assets reimported; Debug game build 0 warnings/0 errors). This rules out an import/build failure for the scratch copy; no primary checkout build artifacts were touched.

The first gallery command intentionally used `--fief-only=civic` and therefore wrote exactly `fief-all.png` plus the five civic-prefixed views. This was the scenario's documented filter behavior, not a six-shot limit or an early exit. To see windmill/power/furnace meshes, include their prefixes in the filter. The exact command, run in `/tmp/civsim-wp10-visual-recheck-20261115/repo`, was:

```sh
CIVSIM_PACK=/tmp/civsim-wp10-visual-recheck-20261115/numeric-e5.pack \
godot-mono --path game --resolution 1600x900 --always-on-top -- \
  --fief-playtest --seed=12345 --scenario=fief-gallery \
  --fief-only=power,windmill,furnace \
  --shots=/tmp/civsim-wp10-visual-recheck-20261115/gallery-power-shots
```

Exit code was 0. The run logged successful captures of `fief-all`, `fief-power`, `fief-windmill`, `fief-windmill-side`, `fief-furnace`, and `fief-furnace-back`. It emitted existing Godot renderer shutdown resource-leak and `burn_map` warnings after capture; these did not prevent the screenshots or change the zero exit status. The screenshots visibly contain the two windmill models and both furnace states alongside other staged Fief meshes. Their SHA-256 values are:

| Image | SHA-256 |
|---|---|
| `fief-all.png` | `606bbcf5021c13e9f3844e654556d8a3caf349df24ae31870390b4767a958d7a` |
| `fief-power.png` | `93441f8a1528e8906ea6cceaac7fba11641c44d57373864c7e20b8ad66243242` |
| `fief-windmill.png` | `db2041e911606906da0c0dbad75be39122949d2b63eb40dba9bcfa0667bc42a8` |
| `fief-windmill-side.png` | `0c16d3f1c0214de252bc69e42ef89d5ec5c509de881bf9695018e44a0a632449` |
| `fief-furnace.png` | `147faa470caec3d406200a2b242d46e7fe6fca1c12b1b60694f15531fe1ed8e0` |
| `fief-furnace-back.png` | `3058445802a3dec1e32ad20a703e88a9c97fb11bfdc29b2cd91257c0010a0bdb` |

The scratch numeric pack is deliberately required: `CIVSIM_PACK` overrides project/root `civsim.pack`, while plain `build-pack --out civsim.pack` leaves E5 rows draft. These screenshots verify asset import and staged meshes, not natural gameplay construction, interaction, save/load, or player comprehension. The later-age in-session review and WP-E5-11/WP-E5-12 gates remain.

The old interactive scratch playtest process (PID 29075) was still consuming CPU after the visual capture and was stopped with SIGTERM. Before stopping, its process command and open-file list were preserved at `/tmp/civsim-wp10-playtest-20261115/process-preservation/` (`ps-29075.txt` SHA-256 `56bed256ff81616d3a9b871afee7902de813d01090e86a2a6d1795999237cdf6`; `lsof-29075.txt` SHA-256 `a4958394d0eb788eb1f19dd15fa69c773bbbb579e313747f62842b8590e5b0fb`). The Year-1 screenshot is preserved at `/tmp/civsim-wp10-playtest-20261115/fief-playtest.png`, SHA-256 `27a28485388bfb521947d783b1027778dfc8f3bd81fb871ddafda058312cb474`. No primary-checkout or user app process was targeted.

## Fresh-source late-age preflight (2026-11-16)

This attempt starts from the current `codex/e5-fief` checkout, including the WP7 and F-T8 trace changes. `docs/MVP/balance/tools/hash_e5_workspace.py` reports HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`, 1,043 scoped files, and aggregate SHA-256 `2fae166f4f3078097d44b5ed4642b758abb2793a3e78c0903cfb4fc30384f34e`. A fresh full scratch copy was made at `/tmp/civsim-wp10-late-review-20261116-r2/repo`, excluding only `.git`, `bin`, `obj`, and `.godot`. All 1,043 files matched the source manifest before scratch-only setup.

For scratch-only user-data isolation, `game/project.godot` sets `config/use_custom_user_dir=true` and `config/custom_user_dir_name="CivSim-WP10-LateReview-20261116-r2"`. The one-shot lightweight probe ran:

```sh
godot-mono --headless \
  --path /tmp/civsim-wp10-late-review-20261116-r2/repo/game \
  --script res://_wp10_userdata_probe.gd \
  --log-file /tmp/civsim-wp10-late-review-20261116-r2/user-data-probe.log
```

It exited 0 and printed `WP10_USER_DATA_DIR=/Users/ejuchheim/Library/Application Support/CivSim-WP10-LateReview-20261116-r2`, separate from the normal CivSim user-data directory. The scratch copy now has 1,044 scoped files after its project setting and probe were added. Its canonical aggregate (same HEAD and file-record format as `hash_e5_workspace.py`, including these two scratch-only differences) is `0fcd8e297d384560e06ea790917aa153410677916c73249f9b12c74d7bd3318a`; the changed `game/project.godot` SHA-256 is `b4dcc819786750778273e905dcfdeb565198c0eadf48e8868381855aacdd062c`, and the probe SHA-256 is `5cdd301ec04de273bc33d11d40f0b8e77781bd52596dabc297c5c00e5a343b9c`.

No content-pack generation, Godot import, full game build, or gameplay session was run. At the latest check, `vm.loadavg` was `15.97 / 21.87 / 36.68`; the 120-seed FFA remained active at over 800% CPU. The full live pass is therefore still pending: generate and hash the scratch numeric pack, import/build only after the active cohorts settle, run `--fief-playtest --research-era=6` without `--start-minute`, then inspect actual monastery/university/hospital/castle construction, the detailed Shift card, and save/load/replay at that late-age state. The staged `--research-era=6` route grants all numeric projects through Fief; it is for UI inspection, not evidence of natural research or balance.
