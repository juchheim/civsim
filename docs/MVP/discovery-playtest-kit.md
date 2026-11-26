# Discovery hand playtest: launch and checklist

Status: 2026-11-27. For the owner to play; nothing here changes the game. Discovery is a **debug-build** chapter: it is reachable only with `--discovery-playtest`, so build the Debug game.

## Launch

From `/Users/ejuchheim/Projects/CivSim/civsim-e6` (the `codex/e6-discovery` branch, also on `main`):

```bash
export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec
dotnet build game/CivSim.Game.csproj -c Debug
CIVSIM_PACK="$PWD/playtest/discovery-numeric-e5.pack" /Applications/Godot_mono.app/Contents/MacOS/Godot --path game -- --discovery-playtest
```

`CIVSIM_PACK` points the game at the playtest pack: the repository content with the 50 Era 5 rows flipped from draft to numeric, so you can play through Faith & Fief into Discovery. (Rebuild it with `playtest/make-discovery-pack.sh` after any content change.) The default pack has Era 5 as draft.

**Skip the first 90 minutes** (inspection only: the saves and replays of such a match are not for play):

```bash
CIVSIM_PACK="$PWD/playtest/discovery-numeric-e5.pack" /Applications/Godot_mono.app/Contents/MacOS/Godot --path game -- --discovery-playtest --research-era=6 --seed=20261019
```

`--research-era=6` gives your civilization every project of the ages before Discovery, so you start at the door of Era 6. `--seed=` makes the world repeatable (a normal launch gets a fresh one).

**Dev aids (Debug only):** `--dev-disc-works` puts an engine, coke oven and factory with coal on your biggest town and one ocean lane, so the Discovery map art shows at once; `--legend-debug` fakes three Legends a few frames in (toasts, Chronicle, people sheet, plates) and makes a few people look like Legends; `--paused` starts paused. For screenshots: `--scenario=legends --shots=/abs/dir`.

## What to look at

Tick each as you go; note anything surprising even if it is not on the list.

**A. The research web (the Pottery problem)**
1. In Era 1, can you research **Pottery**? If not, click it and read the panel: it should now say what it waits on and why a prerequisite is hidden ("it waits until your people have seen clay", "it is two steps away; research X first", or "it belongs to the Nth age").
2. Is that wording enough to know what to do? Did the **Beads & Body Ornament** fix un-block it?

**B. Getting through Era 5 and into Discovery**
3. Do you reach KEY-6 (Printing Revolution) and does the Discovery chapter start (top bar reads DISCOVERY, the toast says so)?
4. Is KEY-6 to KEY-7 about 25 minutes of play at normal speed? Note where you stalled and why.

**C. Industry and the map**
5. Coal pits, the atmospheric engine, coke oven, factory: placed on the right towns, working and idle states readable, smoke and wheel moving, soot on the ground? Does a town with no coal look different?
6. Bastion fort, botanical garden, learned society, exchange house, Renaissance Palace (stages): are they drawn where you expect, and does each state read without a tooltip?
7. Navigation canals with a barge, luggers and the offshore bank at a fishing harbor, soot under a working factory, hedged four-course fields, and the small cues (observatory, greenhouse, news board, embassy flag): do you see them? (Toll gates, cane fields, crops and cargo are not wired yet: doc 39 §13 lists why.)

**D. The sea**
8. Ocean lanes appear once you can cross deep water; ship tokens; colonies (content, strained, blockaded) with their tether; the galleon at a harbor with the right instruments held.
9. Do instruments (chronometer, telescope) get made? Does the telescope widen what you see around a town with a lookout post?

**E. War**
10. After WAR-13 and GOV-12, do your levy look like **musketeers**? (The drilled-levy look follows the muskets a town holds; tell me if it is wrong or missing.) Do muskets pile up in a forge town (check the arms panel)?
11. Raids and assaults: does a bastion hold against cannon? Does a Legend in the group make a visible difference?

**F. Legends**
12. A Legend is a larger figure with a gold plate. Do you notice one without being told? Is the toast ("A Legend is told of") welcome or noise? Does the Chronicle line read well, and the people sheet's **Legends** section?
13. When a Legend dies, does the line say whether a relief remembers them? (ART-06 and MET-06.)

**G. Feel and speed**
14. Frame rate at normal zoom and at regional zoom in a late-game city; any hitching when a season turns.
15. Anything that looks like a ghost: a machine drawn that the sim does not hold (an engine with no coal, a garden nobody staffs).

## What to send back

For each item: **ok**, **wrong** (what you saw and what you expected), or **missing**. A screenshot of anything wrong helps most. If something crashes, the Godot console output (the terminal you launched from) is the useful part.
