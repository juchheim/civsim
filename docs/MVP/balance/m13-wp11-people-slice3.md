# M13 WP-E6-11 slice 3: the Discovery section of the People sheet (2026-11-22)

**Status:** built on `codex/e6-discovery`, uncommitted. The first Godot card that reads `MatchFrame.Discovery` ([38 §8](../38-discovery-age.md)). Game code only; the simulation, hashes and goldens are untouched.

## What it does

`game/Scripts/Hud/Game.People.Discovery.cs` adds a section, **The age of discovery**, to the People sheet's Overview tab. It is hidden unless `frame.Discovery.Active`, so Bronze, Iron and Fief screens are unchanged.

| Part | Shows |
|---|---|
| **The realm** | Whether ships cross the deep ocean (SEA-12), overseas colonies, a staffed exchange house with joint-stock and insurance, a staffed learned society, and progress to KEY-7 in percent. |
| **Each town** (the sheet's scope filter applies) | Coal in store, steam engines, coke oven, canal, factory with its power coverage and operatives, staffed botanical garden, learned society, bastion fort, crewed galleon, exchange house, overseas colony. Towns with none of these are left out; if none has any the section says so. |

## How it was checked

The game project builds (Debug, 0 warnings). The Discovery playtest opens the People sheet (`--ui-open=people`) and captures a screenshot with no script exceptions. At Year 1 the section is empty and below the fold, so the filled-in text has **not** been seen on screen; that needs a played Discovery match.

## Not done

Tile and settlement cards for the new structures (engine, factory, canal), art and icons for them, the era skin and Moments, the mode picker, and a Palace or greenhouse line (the frame carries neither yet).
