# M13 WP-E6-11 slice 2: the game shell knows Discovery (2026-11-21)

**Status:** built on `codex/e6-discovery`, uncommitted. The game shell can now start, label, save and load a Discovery match. Compiled (the Godot project builds in Debug); **not run in the game**, so nothing here has been looked at on screen.

## What the slice does

| Part | How |
|---|---|
| **Save and replay mode byte** | `GameSaveMode` encodes and decodes `ChapterMode.Discovery` (byte 3, appended after Fief's 2; Bronze stays unencoded, byte 4 and above are still refused). Bronze, Iron and Fief records are byte-identical to before. |
| **Labels** | "Discovery mode" in the chapter label, the age label (`DISCOVERY MODE · <AGE> AGE`), the load detail and the replay detail, beside Fief's; one helper decides which chapters name themselves. The rival-countdown toast stays off (the rules say Discovery has none). |
| **Debug launch** | A DEBUG-only `--discovery-playtest` argument starts a Discovery match, the same way `--fief-playtest` starts a Fief one: window title, new-match toast and detail line, and the top-bar age label (`DISCOVERY · <ERA>`). Release builds are unchanged. |

## Tests

`GameSaveModeTests` (20 pass): Discovery round-trips as byte 3, byte 4 is refused, the labels and details read as above, and the countdown toast stays off.

## Not done

The cards that draw `frame.Discovery`, a mode picker in the menu (Discovery is reachable only by the debug argument and by loading a Discovery save), the era skin and Moments, and a look in the running game. The game was not run, so the playtest title and toast are unverified on screen.
