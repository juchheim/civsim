# UI/UX pass 2: structure (Living Ages, 2026-11)

Pass 1 ([ui-typography.md](ui-typography.md)) changed the look (Commissioner, the logo palette, the φ type scale) and kept every
layout. This pass changes structure where it helps the player, and leaves the type system, the palette, the centred logo and its
symmetric wings alone. Nothing in `CivSim.Sim`, `src/` or `content/` changed; no feature or piece of information was removed.

**How it was reviewed.** The `typography` screenshot tour was run before and after at 1600×900, 1280×720, 2560×1080 and
1600×900 with 130 % UI size, and every shot was looked at. The tour was extended for this pass (toasts with an action, rivals
reaching an age together, a critical toast while a dock panel is open, Decrees opened over Diplomacy, the confirm dialog, the
moment card on the open map). Run it with:

```
/Applications/Godot_mono.app/Contents/MacOS/Godot --path game --resolution 1600x900 --always-on-top -- \
  --scenario=typography --shots=<dir> --start-minute=12 --ui-debug [--ui-scale=1.3]
```

`--start-minute=12` gives a world where a neighbour has been met (pass 1's shots were taken at about the same point).

**Screenshots.** Before = pass 1 (`docs/MVP/ui-screens/*.png`, plus `docs/MVP/ui-screens/pass2/before/` for screens pass 1 did
not keep). After = `docs/MVP/ui-screens/pass2/<size>-*.png`.

**Where φ is used, in one place.** φ already shaped the type scale, the Fibonacci spacing and the three research slots
(0.5 : 0.309 : 0.191 of the bar, ratios of φ). This pass adds exactly two φ rules and removes one:

| Rule | Where | Why φ here |
|---|---|---|
| Open map keeps the major share | The right dock is at most 0.382 (1 − 1/φ) of the screen width: 610 at 1600, 543 at 1280×720 | The map is the game; chrome takes the minor share on the axis it occupies. Vertically the HUD bands take 23 % at 1600×900, well inside 0.382. |
| Two dock widths in golden ratio | Wide (Diplomacy) 610, narrow (Chronicle, Decrees) 610/φ = 377 | Two related panel sizes from one family read as one system; 377 and 610 are already the spec's Fibonacci widths. |
| Research slot weights (unchanged) | 0.5 / 0.309 / 0.191 of the bar | Width matches each slot's research share in rank order. |
| **Removed:** fixed golden-rectangle modals for short content | Intro (610×377) and confirm (377×233) now take their content's height | A golden rectangle is a fine frame for a sheet that fills it; on a three-line intro it left a dead band above the button. |

Where φ was deliberately **not** used: click targets (36/42 px floors stay; Fitts's law and the platform minimum win), the
dock floors (520/321 px in small windows so form controls still fit), toast widths (set by readable line length), the rail
(sized by its labels), and the help modal (its four-column table needs 987).

---

## 1. One right-hand dock for Decrees, Diplomacy and the Chronicle

1. **Problem.** The three panels opened from the same top-right buttons but each had its own width (470, 610, 377), its own top
   edge (76, 77, 77 px from the margin) and its own height: Diplomacy reached almost to the screen bottom even when it only said
   "You haven't met anyone yet", the Chronicle stopped at 377 px (233 in small windows) with a long list in it, and Decrees grew
   with its content. Decrees did not close the others, so Decrees and Diplomacy (or the Chronicle) could be open on top of each
   other, and the game menu and modals closed the other two but not Decrees.
2. **Change.** All three share one dock (`PlaceDock`, `CloseOtherDocks` in `Game.Hud.cs`): same top edge (under the top bar),
   same right edge, same lowest edge (above the focus bar), one open at a time; the menu, modals and Esc close whichever is open.
   Diplomacy uses the wide width (it is a list plus a detail); the Chronicle and Decrees use the narrow one. Heights follow
   content: the Chronicle (a long list) fills the dock; Diplomacy fills it once someone is met and is only as tall as its
   message before that; Decrees is as tall as what it holds and scrolls if that is more than the dock.
3. **Why.** Consistency and spatial memory: the same kind of thing appears in the same place at the same size, so the player
   learns one place to look. Mutual exclusion removes an overlap bug. Content-sized panels stop an empty panel from hiding
   a third of the map (figure-ground: the map is the figure).
4. **φ.** Wide = min(610, 0.382 × screen width), narrow = wide / φ (see the table above).
5. **Trade-off.** Diplomacy's "Compact" button opens Decrees to show the forecast and the Confirm button, which now closes
   Diplomacy instead of stacking over it; the player returns with O. Decrees went from 470 to 377 wide, so its neighbour picker
   now has its own line (below).

Before/after: `ui-screens/1600x900-ty-11-diplomacy.png` → `ui-screens/pass2/1600x900-ty-11-diplomacy.png`;
`pass2/before/1600x900-ty-12-decrees.png` and `pass2/before/1600x900-ty-13-chronicle.png` → `pass2/1600x900-ty-12-decrees.png`,
`pass2/1600x900-ty-13-chronicle.png`.

## 2. Toasts keep to the open map, and wait behind full sheets

1. **Problem.** Toasts sit bottom-right above the focus bar, which is exactly where the dock's lower half is: an open Diplomacy
   panel had its order buttons covered by a Food warning. They also drew on top of the research web and covered the inspector
   (its "What changes" and "Needs" text) while the player was reading it.
2. **Change.** With a dock panel open the toast stack stands just left of it. While the research web or People & Stores is open
   the stack is hidden and the timers of non-sticky toasts stop, so they appear when the sheet closes instead of expiring unseen.
3. **Why.** A notice must not hide the thing the player is acting on (proximity and occlusion). Holding the timers keeps the
   information (nothing is lost; critical toasts were already sticky). The top bar stays visible above the sheets, so the
   date and speed still show that time is passing.
4. **φ.** Not applicable: this is about placement and timing.
5. **Trade-off.** A critical toast that arrives while the research web is open is seen when it closes, not immediately. The
   web pauses the game by default ("Pause while the research web is open"), so in the default setup nothing new happens then.

Before/after: `pass2/before/1600x900-ty-05-inspector.png` → `pass2/1600x900-ty-05-inspector.png`.

## 3. Toast layout, and quieter, merged rival news

1. **Problem.** A toast's action ("Open Diplomacy", "Go there") was a middle column between the text and the close button, so the
   title and body got about half the card: "You met the Wave Folk" wrapped its body to three lines, and in small windows the
   title read "You met the ...". Rivals entering a new age used the accent milestone style (the same as your own world firsts)
   and came one card per rival, so three rivals reaching the Hunters age filled the whole stack.
2. **Change.** The action is now a text link under the body, aligned with the text; title and body get the full width; a title
   may wrap to two lines. The card grows with its text (the wrapper used to stay 72 px and let a taller card overlap the next).
   Rival age news uses the Info style and merges: "Star Folk, Leaf Folk and Wave Folk entered the Hunters age" is one card.
3. **Why.** Reading order (title → detail → what you can do) matches the F-pattern; the full-width line stops truncation. The
   accent colour now means "about you" (Von Restorff: a highlight only works if it is rare). Merging same-kind news cuts
   notification load without dropping anything: each rival's entry is still in the Chronicle.
4. **φ.** Not applicable.
5. **Trade-off.** Cards with an action are about 20 px taller.

Before/after: `ui-screens/1280x720-ty-01-hud.png` (three cards, "You met the ...") → `pass2/1280x720-ty-03-toasts.png`;
`ui-screens/1600x900-ty-01-hud.png` → `pass2/1600x900-ty-01-hud.png`.

## 4. People & Stores: the whole civ card is the button, plus a key

1. **Problem.** The most useful information screen in the game (what slows your people, food, work, research) had no labelled
   button. It opened from a 30×26 px people icon inside the civ card, and the top-right Decrees button used the same people icon,
   so the one glyph meant two different things. There was no key for it.
2. **Change.** The whole civ card (377×64, the people's name, population and age) opens People & Stores, with a hover state and a
   pointing-hand cursor; its tooltip starts "Click for People & Stores (Y)". New key **Y** ("your people"; P is already pause)
   opens it and closes it again. The help table lists it.
3. **Why.** Fitts's law: the target grew from 780 px² to about 24,000 px² and sits in a screen corner region the eye already
   visits for the population. The card is about your people, so clicking it for more about your people is the expected
   mapping (discoverability by affordance).
4. **φ.** Not applicable.
5. **Trade-off.** Clicking the card's age block also opens People & Stores rather than the research web. The age tooltip is
   still on the card.

## 5. Decrees: its own icon, and a locked state that explains instead of disabling

1. **Problem.** Until the Age of Metal every Decrees control was disabled: three greyed buttons, an empty neighbour list, a
   greyed "Raise levy", and a "Propose compact" button that looked enabled but did nothing. The top-bar button gave no sign that
   Decrees were locked until it was opened.
2. **Change.** The top-bar button shows a lock until Decrees unlock and a new seal glyph after (in icon-only windows the seal
   stays and is dimmed, because a lone lock does not say what is locked); its tooltip names the unlocking project. Inside, the
   locked panel says what Decrees will let you do (found, explore, mine; levy and compacts; corvée) and that they spend Mandate,
   and hides the controls. Unlocked, the controls are grouped (CHOOSE A PLACE, DIPLOMACY, AT HOME); the neighbour picker has its
   own line so the narrow dock fits; Raise levy and Propose compact wrap if a window is too narrow; Propose compact is disabled
   until Decrees unlock like the others.
3. **Why.** Progressive disclosure: show the controls when they can be used, and before that the one thing the player can
   learn (what this will become and how to get it). An empty state that teaches beats a wall of disabled controls. A visible
   lock sets expectations before the click (signifier).
4. **φ.** Narrow dock width (377 = 610/φ).
5. **Trade-off.** A player can no longer see the exact control layout before the unlock; the three bullets describe it.

## 6. The focus bar says what each empty slot is worth, and Research web becomes the primary action

1. **Problem.** Every empty slot repeated "Its share goes to the others until you choose" (three times on the first screen,
   truncated in the third). The Research web button had the same weight as everything else while the game was waiting for the
   player's choice, and the accent fill on the HUD went to the 1× speed segment.
2. **Change.** An empty slot's line says what it is worth: "Worth 4 of 7 points" (2 of 7, 1 of 7; short enough for the tertiary card at 130 % UI size); the share rule
   stays in the tooltip. While any slot is empty (and the elders are not choosing), Research web is a filled accent button with
   a parchment "new" badge; once all three slots are working it returns to secondary.
3. **Why.** Remove redundancy, give each slot unique, decision-relevant information (it tells the player to fill Primary first).
   Visual hierarchy: the one strong button on the HUD is the action the player owes, which helps first-minute comprehension.
4. **φ.** The slot widths already follow the φ weights; the share text now explains them.
5. **Trade-off.** Two accent fills (speed segment and Research web) while a slot is empty. The speed segment is small and
   reads as a selected state, the button as an action.

## 7. Layer rail: one mark per layer, and two groups

1. **Problem.** Food and Forest had the same icon; Resources and Deposits had the same icon; Cohesion used the people icon and
   Trade the diplomacy flags. In compact windows the rail is icons only, so four pairs were indistinguishable. Resources (a set
   of markers that can show on top of any tint) sat in the middle of the tints that replace each other, so nothing told the
   player why it behaved differently.
2. **Change.** New glyphs: Deposits (a faceted ore lump), Trade (a route between two points), Cohesion (three linked people),
   Forest (a tree), Decrees (a seal). The rail is now LAYERS (Claims, Food, Methods, Deposits, Trade, Cohesion, Forest; one at a
   time) and, after a divider, MARKERS (Resources, and Beacon when available). The Resources tooltip says it shows on top of any
   layer. The help's Map layers tab now covers all eight (it described four).
3. **Why.** Similarity and proximity (Gestalt): things that behave alike are grouped, things that differ are visibly apart.
   Distinct icons are a precondition for an icon-only rail.
4. **φ.** Not applicable (sizes are set by labels and the 36 px target floor).
5. **Trade-off.** The rail is about 30 px taller at full size (it still ends 90 px above the focus bar at 1600×900); Resources
   moved from 4th to 8th place. No layer has a number key, so no binding moved; R still toggles Resources.

## 8. Work tab: jobs nobody holds fold into one line

1. **Problem.** Work assignments listed all twelve jobs; early in a match nine read 0, so the three that matter were lost in a
   column of zeros.
2. **Change.** Only jobs someone holds get a bar row; the rest are listed on one line under them ("None at the moment: Fishers ·
   Farmers · ..."). Nothing is hidden: every job name is still there.
3. **Why.** Signal-to-noise and progressive disclosure; the list grows as the society does.
4. **φ.** Not applicable.
5. **Trade-off.** Rows appear and disappear as jobs change, so a row's position is not fixed.

Before/after: `ui-screens/1600x900-ty-09-people-work.png` → `pass2/1600x900-ty-09-people-work.png`.

## 9. Short modals fit their content

1. **Problem.** The intro ("The Spark") and the confirm dialog were fixed golden rectangles; the intro had a 70 px empty band
   between its three steps and its button.
2. **Change.** A modal height of 0 means "as tall as its content"; the intro and confirm use it, keeping their widths
   (610, 377). Help, settings, new match and the end screen keep their fixed sizes (their content fills them or scrolls).
3. **Why.** Proximity: the button belongs to the steps above it. Empty space should separate groups, not sit inside one.
4. **φ.** Removed where it hurt (see the table).
5. **Trade-off.** None found.

Before/after: `pass2/before/1600x900-ty-17-intro.png` → `pass2/1600x900-ty-17-intro.png`.

## 10. Chronicle rows no longer clip their date

1. **Problem.** Chronicle rows were a fixed 56 px; an entry that wrapped to two lines pushed its date line ("Year 12 · Spring")
   out of the row, so long entries had no date.
2. **Change.** Rows grow to fit two lines of text plus the date (`UiRowButton.FitHeight`).
3. **Why.** No clipped text (a hard requirement of this pass).
4. **φ.** Not applicable.
5. **Trade-off.** Fewer rows fit on screen; the Chronicle is now the full dock height to make up for it.

## 11. Small placement fixes

- **Moment card** ("New knowledge") is placed right of the layer rail; at 1600×900 it used to cover the rail's bottom buttons.
- **Hover tile card** hides when a dock panel opens under a resting cursor (it used to float on top of the new panel until the
  mouse moved).

## Keys

New: **Y** opens and closes People & Stores. No existing key moved. The help's Controls tab lists Y.

## Not changed, and why

- **Research web layout.** Its map : inspector split is already about φ (925 : 570), the primary action ("Start in Primary") is
  the one accent button, and pass 1 tuned it heavily. The only change is that toasts no longer cover it.
- **Era rail abbreviations** ("DAW", "HUN" at 12 px in the research map) are cramped, but the full name needs about 60 px in a
  52 px chip; a better fix (numerals or a wider rail) touches the map layout and was left for a research-web pass.
- **Help modal width** (987) stays: the four-column Controls table needs it.
- **People & Stores footer button** looks redundant with the × but becomes "Back to Research" when opened from the web, so it stays.

## Screenshot index

After (`docs/MVP/ui-screens/pass2/`): `1600x900-ty-01-hud`, `-03-toasts`, `-05-inspector`, `-09-people-work`, `-11-diplomacy`,
`-12-decrees`, `-13-chronicle`, `-15-help`, `-17-intro`, `-17b-confirm`, `-18-moment`; `1280x720-ty-01-hud`, `-03-toasts`,
`-12-decrees`, `-13-chronicle`, `-18-moment`; `2560x1080-ty-01-hud`, `-11-diplomacy`; `1600x900-uiscale130-ty-01-hud`,
`-11-diplomacy`, `-12-decrees`.

Before: pass 1's shots in `docs/MVP/ui-screens/` and, for screens pass 1 did not keep, `docs/MVP/ui-screens/pass2/before/`
(taken from the pass-1 build at `--start-minute=4`, so the world is earlier than in the after shots).

The hover tile card in a few tour shots sits where the real mouse cursor was during the capture (the tour window is live); it is
not part of the scripted state.
