# M12 WP-E5-10: Fief view slice

This is a preparatory presentation slice for the Fief mechanics already present in the simulation. It does not make Fief selectable, does not change save formats, and does not establish that the era is playable. E5 content remains draft pending WP-E5-11.

## Included

- `MatchFrame.Fief` reports own-settlement seasonal mill source counts, available power hours, grain coverage, monk count, monastery/university/hospital counts, and concentric-castle presence.
- The `Game.cs` payload carries these values only while the frame's Fief block is active. Other settlements receive zeros from the existing own-only frame privacy filter.
- `world_data.gd` records the Fief-active bit and provides `fief_settlement()` to read the Fief-only settlement fields. It returns an empty dictionary for Bronze and Iron frames.
- The existing Shift settlement inspection card displays the same own-only values only for an active Fief frame. Bronze and Iron UI stays on the existing path.

## Not included

This readout does not expose hospital staffing or treatment status, university scholar staffing or research contribution, project progress, full mill demand/use breakdowns, building instructions, or Fief visuals. It is not a complete Fief UI, and it does not expose or enable a selectable Fief game mode.

## Validation

- `dotnet build game/CivSim.Game.csproj` — passed, 0 warnings and 0 errors.
- `godot-mono --headless --path game --editor --quit` — completed successfully and registered the GDScript classes; Godot warned that the nested prototype project is ignored during scanning.
- `godot-mono --headless --path game --quit-after 3` — completed without startup errors.
- Focused Fief/Iron/Bronze view tests from the preceding WP-E5-10 frame slice passed 3/3.

The C# bridge/build, Godot script scan and headless startup were validated. The Fief UI was not visually exercised in a live match because this slice intentionally does not expose a selectable Fief mode.
