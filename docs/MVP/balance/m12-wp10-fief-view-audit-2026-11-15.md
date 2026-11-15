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
