# M12 WP-E5-10: Fief view slice

This is a preparatory presentation and persistence slice for Fief mechanics already present in the simulation. It does not make Fief selectable or establish that the era is playable. E5 content remains draft pending WP-E5-11.

## Included

- `MatchFrame.Fief` reports own-settlement seasonal mill source counts, available power hours, grain coverage, monk count, monastery/university/hospital counts, and concentric-castle presence.
- The `Game.cs` payload carries these values only while the frame's Fief block is active. Other settlements receive zeros from the existing own-only frame privacy filter.
- `world_data.gd` records the Fief-active bit and provides `fief_settlement()` to read the Fief-only settlement fields. It returns an empty dictionary for Bronze and Iron frames.
- The existing Shift settlement inspection card displays the same own-only values only for an active Fief frame. Bronze and Iron UI stays on the existing path.
- The Fief settlement payload now carries grinding/sawing/craft mill demand and use, hospital staffing plus current illness/wound/treatment counts, university scholar heads and Insight-equivalent contribution, and the viewer civilization's KEY-6 progress. Those additions remain inside the active Fief frame and the existing own-settlement privacy filter.
- The Fief-only Shift card adds the operational breakdowns. It reports hospital staffing and current treated/patient counts, university staffing and research contribution, and KEY-6 progress. When the Building view has requested its existing detailed outlook, the Fief card also shows the first unfinished E5 work and a short blocker/action line.
- The renderer uses the established civic mesh kit for Fief monasteries, universities, and hospitals, and adds those props only when the frame's Fief-active bit is set. It keys settlement rebuilds on the Fief structures.
- Version-6 quick saves now accept the Fief mode byte; new non-Bronze replay footers record the chapter mode, and old replay footers still default to Bronze. Loading remains gated by a save/replay that already records Fief; the game still offers no Fief start selector.

## Not included

This is still a narrow inspection slice, not a complete Fief UI. Detailed blocker/action text is available only while the existing Building view has requested a build outlook. The visual pass reuses Iron civic meshes; it does not add bespoke Fief art, and the windmill/blast furnace do not yet have dedicated Fief forms. It does not expose or enable a selectable Fief game mode. Mode persistence is implemented for saves and replays, but it does not bypass the WP-E5-11 gate.

## Validation

- `dotnet build game/CivSim.Game.csproj --no-restore` — passed, 0 warnings and 0 errors after the shared duplicate-member conflict was resolved.
- `godot-mono --headless --path game --editor --quit` — completed successfully and registered the GDScript classes; Godot warned that the nested prototype project is ignored during scanning.
- `godot-mono --headless --path game --quit-after 3` — completed without startup errors.
- `dotnet test src/CivSim.Tests/CivSim.Tests.csproj --no-restore --filter 'FullyQualifiedName~IronViewTests.Fief_frame_exposes_power_and_fief_state_only_for_owned_settlements'` — passed, 1/1. The test checks own-settlement privacy, Fief-only mode gating, mill values, hospital state, scholar/research values, and the detailed outlook path.
- `godot-mono --headless --path game --editor --quit` — completed successfully; Godot registered the edited `M7CoreLayout`, `M7WorldBuilder`, and `M7WorldData` scripts. It emitted the existing nested prototype-project warning and regenerated uncached `.uid` files, which were removed after the scan.

The C# bridge/build, focused frame test, and Godot script scan passed. This follow-up's live Godot visual work was not exercised because D18 keeps Fief unselectable and the start selector closed; the renderer changes are source-reviewed only until an authorized Fief session exists.

## Save/load integration audit (2026-11-14)

The mode-tagged paths are now connected end to end in the game code: quick-save capture writes the session's chapter mode, version-6 load restores it through `ChapterRules.For`, replay save writes the mode footer, and replay watch passes that mode into the new session. Bronze quick saves remain version 5 with no extra byte; old replay footers without a mode still default to Bronze. Invalid mode bytes are rejected. The top age label now marks a Fief session as `FIEF MODE · <CURRENT ERA> AGE`; Fief load and replay-watch toasts identify the mode. Iron and Bronze labels/toast details retain their existing text.

The targeted `GameSaveModeTests` exercise actual quick-save write/read for Bronze, Iron and Fief, the loaded mode's rules, replay footer round trips and legacy replay fallback, plus the player-facing mode-label strings. `SimulationSessionSaveTests` also captures a live Fief session, writes and reads its save, restores it, verifies the restored Fief rules and exact simulation snapshot/checksum, and confirms that the resumed session advances while remaining Fief (16 targeted tests passed). `dotnet build game/CivSim.Game.csproj --no-restore` passes with 0 warnings and 0 errors. A test-only `Godot.OS` stub supplies unused user-data path/launch arguments; the save tests write to unique temporary files.

Remaining gaps: the loaded Fief HUD was not visually exercised in a live Godot session because no Fief start selector exists. Fief remains unselectable under D18; content promotion, whole-match acceptance and human comprehension/playtesting remain WP-E5-11/12 gates. The lower-level Fief snapshot cross-mode rejection remains covered separately by `FiefChapterTests`.

## Isolated Debug playtest smoke (2026-11-15)

The scratch-copy Godot import, Debug build, and screenshot playtest now pass; exact commands, primary aggregate comparison, status-pack and binary hashes, diagnostics, and screenshot location are recorded in the [WP-E5-10 audit](m12-wp10-fief-view-audit-2026-11-15.md#isolated-debug-playtest-smoke-2026-11-15). The initial Year 1 screenshot confirms the Fief draft label and research UI, but the three Fief institution props are not built yet. This remains partial pending human review after advancing to those projects and checking the Shift view and save/load behavior.

## Release integration recheck (2026-11-15)

Static inspection confirms the opt-in `--fief-playtest` is compiled only under `DEBUG` in the startup bypass, chapter selection, and new-match override. Release entry paths therefore remain Iron/Bronze only; exported macOS and Windows presets do not add a Fief mode selection. Fief save/replay restore remains supported for recorded version-6 save tags and replay footers, with pack identity checked on restoration. No new code or runtime validation was needed for this recheck; the remaining view gate is live review after Fief institutions exist, and ordinary/release selection stays closed pending WP-E5-11 promotion and acceptance.
