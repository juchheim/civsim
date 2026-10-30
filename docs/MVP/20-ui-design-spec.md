# 20 — In-game UI design spec (M7d redesign)

> Status: **decided**, 2026-10-21. Implementable spec for the Godot 4.7 .NET front end (`game/Scripts`). It replaces the HUD,
> focus bar, research web, Chronicle, notifications, diplomacy panel, modals and input map built in M7a–M7d.
> It changes **no** simulation code, **no** commands and **no** content. It reads only `MatchFrame`, `ContentPack`
> and `ResearchCatalog`, and sends only the existing `PlayerCommands`.
>
> Units: every pixel value is a **design px** in the 1600×900 logical canvas (see §1.9 for scaling). "W"/"H" are the
> logical viewport width/height. Sources checked: 08 §1/§5–§12, 18, 19, m9 protocol, `Game.cs`, `M7cHud.cs`, `M7dHud.cs`,
> `FocusResearchHud.cs`, `SimulationSession.cs`, `ResearchSystem.cs`, `MatchFrame.cs`, `ViewTypes.cs`, `content/catalog/*.yaml`.

---

## Implementation status (2026-10-21)

Built in `game/Scripts/Ui` (kit) and `game/Scripts/Hud` (screens). Verified with scripted screenshots at 1280×720, 1600×900, 1920×1080, 2560×1080 and 130 % UI size, scripted input checks (Space, T, Esc stack, wheel zoom, left-drag pan, right-drag turn, Beacon mode, F-keys), the 12-case render benchmark (≥ 59.7 fps min, vsync-capped; HUD tick 0.03–0.05 ms, refresh 0.1 ms) and 328 passing tests. **No human run yet.**

Deviations from the text below, decided while building:

| Spec | As built |
|------|----------|
| §1.9 scale floor uses `ScreenGetScale()` | Uses window pixels only. The default 1600×900 window on a Retina Mac is 1600×900 *pixels*, so dividing by the screen scale made the UI 1.8× too large. Min window 1280×720 px. |
| §3.4 "Or:" and "Replace which?" as rows of chips | Full-width stacked buttons; long project names truncated at three across. |
| §3.3 "Queue n · 63/yr" in the summary | Insight stays as "63 Insight/yr"; the queue count is in its tooltip and on the web's Queue chip. |
| §3.7 toast spacing ≥ 1.5 s, Moment 4 s spacing | Moment spacing implemented; toast spacing dropped (3-slot pool, Info dropped when full). |
| §2.7 cascade slide | Content fades in (0.22 s) when a slot changes project; no slide. |
| §2.7 "Elders' pick" chip | Not built; the Chronicle and an Info toast carry the pick and its reason. |
| §3.8 first-3-hovers hint | Hint shows for the first 12 distinct tiles. |
| §4.2 full Tab focus order, §2.2 custom focus rings | Godot default focus traversal with the themed 2 px accent ring; arrow keys move between project cards. |
| §6.4 `--playtest-log`, minimap | Not built. |
| Trackpad scroll constant | 1.15^Δy per pan-gesture event; **must be tuned on a real trackpad** (not confirmable here). |
| Right-click slot menu | Show in research / Move up / Move down / Stop. |

Dev arguments: `--ui-open=…`, `--scenario=…` (+ `--shots=dir`), `--opening-hold`, `--paused`, `--ui-scale=1.3`, `--reduce-motion`, `--research-map`, `--ui-debug`.

---

## 0. Summary

### 0.1 Headline decisions (one screen)

| # | Decision |
|---|----------|
| D1 | **One theme, built in code.** `UiTheme.Build()` returns a single `Theme` set on one full-rect root `Control` under the HUD `CanvasLayer`. All styling via theme type variations; no per-control `AddTheme*Override` calls. System font **Avenir Next** with fallbacks; 3 weights; tabular numbers. |
| D2 | **Type scale on φ:** 14 · 16 · 20 · 26 · 42. Nothing below 14. **Spacing on Fibonacci:** 4 · 8 · 13 · 21 · 34 · 55 · 89. Panels 233/377/610/987 wide; modals are golden rectangles. Click targets ≥ 36 (not φ). |
| D3 | **Warm ink-glass panels** (`#1F1B16` at 90%) with 1 px parchment border and soft shadow; parchment text; **ochre-gold accent `#E9BE4E`**. Theme colours (Craft copper-amber, Kin sage, Reach sky) appear only in UI, always with a shape glyph. Text never sits directly on the map. |
| D4 | **Stretch:** keep `canvas_items` with the 1600×900 base, set aspect to **`expand`**, set a minimum window of 1280×720, and apply an **automatic UI scale floor of 0.9** (so 14 px never renders below 12.6 px). Player UI size setting: 100 / 115 / 130 %. |
| D5 | **Top band:** Civ card (left) · **Time cluster with the dominant Pause/Resume button and ½×/1×/2×/4× speed** (centre) · Diplomacy / Chronicle / Help / Menu (right). FPS, figure count, zoom number and tick move to a developer overlay on **F3**. Year is shown **1-based** everywhere. |
| D6 | **Paused state is unmistakable:** accent-filled Resume button, a "PAUSED" pill under the time cluster, a 3 px ochre frame around the viewport and a 14 % warm scrim over the map. **A new match starts paused** ("opening hold") through the first research choice and resumes automatically when the player closes the research web. Opening the research web does **not** pause (setting, default off). |
| D7 | **Focus bar:** three cards sized **610 : 377 : 233** (1 : 0.618 : 0.382) so rank reads as size, every card ≥ 200 px. Each shows rank badge, name, theme glyph, live share %, progress, ETA. **Open = dashed ochre outline + "+ Choose a project"** (pulses until the player's first ever assignment). Completion: **Done ✓ flash**, then the visible cascade (others slide up, bottom slot opens). Assignments made while paused show a **pending** state. |
| D8 | **Research web:** default view **"Choose"** — a card grid of the projects ready now, grouped in three theme columns, plus "Almost ready" and a quiet "+N more to discover this age"; the lanes grid becomes the optional **"Map"** view with human field names and only current-age columns. Canvas 61.8 % / inspector 38.2 %. One click selects, the big CTA (or double-click, Enter, 1/2/3) assigns. **CTA targets the first open slot** ("Start in Primary"); when all slots are busy it opens an inline **"Replace which?"** row. Live slot strip on top. Codes never shown to players (F3 only). |
| D9 | **Notifications:** project completions get a **Moment card** (bottom-left, 8 s, "Show me" / "Pick next"); other events use a **toast stack** (bottom-right, max 3); the **Chronicle** is a collapsed top-right button with an unread badge that expands to a grouped, iconised, newest-first feed. Critical alerts are sticky toasts; they never auto-expand panels. |
| D10 | **Camera/input:** left-drag **pans** (grab the map), right-drag (or Option/Alt-drag) **turns and tilts**, wheel and two-finger trackpad scroll **zoom toward the cursor** (1.15× per notch), pinch zooms. Right-click never places a Beacon: Beacons use an explicit **Beacon mode** (B). **Diplomacy moves from D to O.** Esc closes the topmost layer; with nothing open it opens the Menu. Space pauses from anywhere except a text field. |
| D11 | **Left map-tools rail:** compass (click = north), Layers (Claims, Food, Methods; Resources as an add-on toggle) with a swatch legend card, and the Beacon button once unlocked. |
| D12 | **Diplomacy:** civ cards (emblem, name, relation chip) → status + orders grouped **Friendly / Hostile / Your parties**, each with consequence text and a visible disabled reason; war, raid and assault need a two-step confirm. Civs get readable names from their emblem: **Sun, Wave, Leaf, Star Folk**. |
| D13 | **Plain words:** Era → **Age**, Keystone → **Turning point**, Lineage → **Field**, Council → **the elders / Auto-fill**, Glimpsed → **Almost ready**, Dormant → **Blocked**. "Insight" stays, always with "/yr" and the tooltip "research speed". |

### 0.2 Audit findings (verified in code and screenshots), ranked by player impact

| # | Sev | Finding | Evidence | Fixed by |
|---|-----|---------|----------|----------|
| A1 | Critical | **Space cannot pause while the research web is open**: `HandleResearchInput` returns `true` for every key when the web is visible. Pause is a plain text button equal in weight to Save/Load; the paused state appears only as the word "Paused" inside the run-on status line. | `FocusResearchHud.cs` L1324–1333, `M7dHud.cs` L80 | D5, D6, §4.1 |
| A2 | Critical | **Assigning while paused looks broken**: the frame does not change until the sim steps, so the slot still says OPEN. The match **starts unpaused** behind the intro modal and opening web; with all slots empty, `ResearchStep` discards that Insight (`totalWeight == 0 → return`). | `SimulationSession` `_paused = 0`; `ResearchSystem.cs` L378 | D6 opening hold, D7 pending state |
| A3 | High | **Open slots are hard to see** (pilot): same panel colour as working slots; "OPEN — choose a project" in low-contrast green; meta text 10–11 px. | screenshots default_1600 | D7 |
| A4 | High | **Research web is cryptic and mostly empty**: 3-letter lane codes; Glimpsed nodes prefixed "?" read as "unknown"; E1/E2 columns are always empty in the Dawn age (projects of a later era are Hidden by rule) so ~⅔ of the canvas is blank; inspector headings (17–20 px) outweigh the action text (11–12 px); title shows the ID ("Calls & Gestures · COM-01"); effects are raw stat keys ("raid_surprise: -20%"); disabled reasons cite codes ("Requires TRD-01"). | web_1600, overview_1600 | D8, §5 |
| A5 | High | **Controls are rebuilt while you click them**: prerequisite/unlock buttons are freed and recreated every 0.2 s (`UpdateResearchSelection` from `UpdateResearchHud`); all Chronicle rows are freed and recreated on every applied frame (`ProcessViewEvents → RefreshChronicleText`); diplomacy `OptionButton`s are `Clear()`ed and refilled every frame while open, resetting an open dropdown. Clicks get lost, hover flickers, GC churn. | `FocusResearchHud.cs` L727, `M7cHud.cs` L299, `Game.cs` L248 | §6.4 perf rules |
| A6 | High | **No mouse panning at all**: left and middle drag both orbit; WASD is the only pan. Wheel zoom is centre-anchored and unsmoothed; pinch is damped to the 4th root (`^0.25`) and feels sluggish. | `Game.cs` L295–311 | D10, §4.1 |
| A7 | High | **Key conflicts**: D pans right *and* toggles Diplomacy; WASD/arrows are polled every frame regardless of focus, so **typing in the research search pans the camera**. Esc is a hard-coded priority chain that also closes the passive Chronicle. | `Game.cs` L352, L1849 | D10, §4.2 |
| A8 | High | **Right-click instantly places a Beacon** (no mode, no confirm), even before Beacons are unlocked, producing refusal alerts with enum names ("PlaceBeacon refused: requirements are not met."). | `Game.cs` L299, `M7cHud.cs` L612 | D10 Beacon mode |
| A9 | High | **Text too small at 1280×720**: canvas_items scale 0.8 turns 10–12 px into 8–9.6 px; the help strip runs off-screen and is clipped; that strip and other text sit directly on the map (contrast 1.0–1.4 : 1 on snow/grass/sand). | default_1280x720 | D2, D4, D3 |
| A10 | Medium | HUD says **"Year 0"** while the Chronicle says **"Y1"** (frame year is 0-based). | default_1600 | D5 |
| A11 | Medium | Developer data in the player HUD (Figures, fps, Z-level, speed in one string). | `Game.cs` L1739 | D5 F3 overlay |
| A12 | Medium | Chronicle repeats identical lines ("A people claimed a world first." ×5), rivals are anonymous, no icons or grouping, and it occupies 326×320 px permanently. | m20_1600 | D9, §3.6 |
| A13 | Medium | Moment banner (y 112–238) and tip panel (y 122–226) share the top-centre spot and collide. | `M7cHud.cs` L168, `M7dHud.cs` L99 | §3.1 zones |
| A14 | Medium | Focus bar widths 4:2:1 leave Tertiary ~200 px at 1600 and tiny at 1280; the "4/7" label is wrong whenever another slot is empty (empty shares are redistributed). | `ResearchSystem.cs` L388 | D7 live share |
| A15 | Medium | Toolbar: six equal text buttons; disabled Load/Watch replay give no reason. | default_1600 | D5 menu |
| A16 | Medium | Map Layers: no swatches, 11 px legend, large dead space; Z0–Z3 meaning unexplained; no compass. | default_1600 | D11 |
| A17 | Low | Jargon: Insight, Council, Leaning, Lineage, Keystone, "ETA calibrating", W1/W2. | various | D13, §5 |
| A18 | Low | `RefreshM7dButtons` calls `File.Exists` twice per rendered frame. | `M7dHud.cs` L604 | §6.4 |
| A19 | Low | End screen is a single wall of text with "Civilization 2" and raw AI fields ("Layer / Kind"). | `M7dHud.cs` L526 | §3.11 |
| A20 | Low | Drag-and-drop assignment still exists and was mentioned to the pilot; it is a second, undiscoverable path. | `ResearchProjectButton.cs` | Remove (§6.3) |

---

## 1. Design tokens

### 1.1 Fonts

| Token | Value |
|-------|-------|
| Family | `SystemFont` with `FontNames = ["Avenir Next", "Segoe UI", "SF Pro Text", "Helvetica Neue", "Inter", "Noto Sans", "Arial"]`. If none resolves, Godot's default font is used (no bundled font). Avenir Next is present on this Mac (`/System/Library/Fonts/Avenir Next.ttc`); Segoe UI covers Windows. |
| Weights | `Regular` = `FontWeight 400`; `Medium` = 500; `Bold` = 600 (Avenir Next Demi Bold; never 700+). Three `SystemFont` instances, created once and cached in `UiTheme`. |
| Rendering | `Antialiasing = Gray`, `Hinting = Light`, `SubpixelPositioning = Auto`, `GenerateMipmaps = false`, `AllowSystemFallback = true`. |
| Caps variation | `FontVariation` over Medium with `SpacingGlyph = 1` (≈ +0.07 em at 14 px). Used only for ALL-CAPS eyebrows/labels. |
| Numbers | `FontVariation` with `OpentypeFeatures = {"tnum": 1}` over Regular and Bold (`NumRegular`, `NumBold`). **Also** give every changing number a fixed-width slot (`CustomMinimumSize.X`, right-aligned) so nothing jitters if the font ignores `tnum`. |
| Glyph policy | Text may contain only ASCII plus `· × – — … ’ “ ” ½ %`. Every other symbol (check, arrows, pause, play, stars, warnings, theme and state marks, emblems) is drawn by `UiIcon` (§2.15). No emoji. |

### 1.2 Type scale (φ = 1.618)

| Token | Size | Weight | Line height | Use |
|-------|-----:|--------|------------:|-----|
| `Display` | 42 | Bold | 52 | Modal titles, age banner |
| `Title` | 26 | Bold | 32 | Panel titles, inspector project name, top-bar year |
| `Heading` | 20 | Medium | 26 | Card titles (project in a slot, civ name), key values |
| `Body` | 16 | Regular | 24 | Paragraphs, list rows, tooltips |
| `BodyStrong` | 16 | Medium | 24 | Buttons, emphasised body, CTA labels |
| `Eyebrow` | 14 | Caps variation | 18 | ALL-CAPS section and card labels ("PRIMARY", "WHAT CHANGES") |
| `Caption` | 14 | Regular | 20 | Meta (ETA, dates, hints, legends) |
| `Number` | as context | `NumRegular`/`NumBold` | as context | All changing numbers |

Steps: 16 × φ = 26, 26 × φ = 42; 20 ≈ 16 × √φ is the half-step; 14 is the floor (≈ 16 / φ^¼). Set Godot `Label` `line_spacing` = target line height − `font.GetHeight(size)` computed once at startup per style.

**Minimum readable size:** 14 design px. With the scale floor (§1.9) it renders at ≥ 12.6 px on a 1280×720 window. Caption/Eyebrow text is never used for anything the player must act on; actions are ≥ 16.

### 1.3 Spacing, sizes, radii

| Token | px | Use |
|-------|---:|-----|
| `Sp1` | 4 | Icon–label gap, chip inner gap (practical exception to Fibonacci) |
| `Sp2` | 8 | Gap between controls in a row, card stack gap, panel padding (dense) |
| `Sp3` | 13 | Card padding (horizontal), gap between groups, compact safe margin |
| `Sp4` | 21 | Panel padding, safe margin (base) |
| `Sp5` | 34 | Section separation in modals |
| `Sp6` | 55 | Top band height, large CTA height |
| `Sp7` | 89 | Focus-bar card height, rail width |
| Widths | 144 · 233 · 377 · 610 · 987 · 1597 | Chips/columns · narrow panels · drawers/toasts · wide panels/coach tips · modals · max web width |
| Control heights | 36 small · 42 default · 55 large | Deliberately **not** φ: click-target floor is 36 |
| Icon sizes | 16 inline · 26 buttons · 34 emblems | |
| Radii | `R1` 5 (cards, chips' inner, inputs) · `R2` 8 (panels, buttons) · `R3` 13 (modals, web) · pill = height / 2 | Fibonacci |
| Borders | 1 px default · 2 px for selected/open/focus | |
| Progress bar | 6 px tall (cards), 4 px (chips) | |

### 1.4 Colour tokens

Contrast was computed (WCAG 2.x) against each surface **composited over the worst map pixels** (snow `#E6ECF2`, sunlit grass `#C9D27A`, sand `#E3D6A8`, water `#2F7FC0`); the worst case is listed.

**Surfaces**

| Token | Value | Use |
|-------|-------|-----|
| `Surface1` | `#1F1B16` @ 0.90 | HUD panels over the map (elevation 1) |
| `Surface2` | `#2A251E` @ 0.96 | Cards, popovers, toasts (elevation 2) |
| `Surface3` | `#322C25` opaque | Research web, modals, tooltips, menus (elevation 3) |
| `SurfaceSunken` | `#16130F` @ 0.60 | Progress tracks, input fields, inset wells |
| `ScrimModal` | `#0E0C0A` @ 0.55 | Behind modals |
| `ScrimWeb` | `#0E0C0A` @ 0.45 | Behind the research web |
| `ScrimPause` | `#2A2216` @ 0.14 | Over the 3D view while paused |
| `Border` | `#F3ECDD` @ 0.10 | 1 px panel border |
| `BorderStrong` | `#F3ECDD` @ 0.22 | Card border, dividers in modals |
| `Hover` | `#F3ECDD` @ 0.06 | Hover fill on ghost rows/buttons |
| `Pressed` | `#F3ECDD` @ 0.10 | Pressed fill |

**Text**

| Token | Hex | Worst contrast on Surface1 | Use |
|-------|-----|---------------------------:|-----|
| `TextPrimary` | `#F3ECDD` | 11.2 : 1 | Body, titles |
| `TextSecondary` | `#C9BFAC` | 7.2 : 1 | Meta, descriptions |
| `TextTertiary` | `#A79E8B` | 4.9 : 1 | Captions, timestamps, placeholders |
| `TextDisabled` | `#7A7263` | 2.8 : 1 | Disabled labels only (exempt); always paired with a visible reason in `TextSecondary` |
| `TextOnAccent` | `#1E1608` | 10.2 : 1 on accent | Text on accent-filled buttons |

**Accent and semantic**

| Token | Hex | Contrast (Surface1) | Meaning | Always paired with |
|-------|-----|--------------------:|---------|--------------------|
| `Accent` | `#E9BE4E` | 7.5 : 1 | Interactive emphasis: primary CTA fill, selection, focus ring, open slot outline, paused frame | — |
| `AccentHover` | `#F2CD6A` | — | Primary hover | — |
| `AccentPressed` | `#CFA13A` | (TextOnAccent 7.5 : 1) | Primary pressed | — |
| `Success` | `#5FC49B` | 6.2 : 1 | Done, positive change | check glyph |
| `Warning` | `#F57F3A` | 5.0 : 1 | Blocked, fallback, risk (same meaning as the map's orange "fallback" tint) | warning triangle |
| `Danger` | `#F2786A` | 4.8 : 1 | Critical alerts, war, losses, destructive confirm | danger triangle (filled) |
| `Info` | `#9FC3E0` | 7.1 : 1 | Neutral notices | info circle |

Dark fills for tinted cards/badges (text on them is `TextPrimary`, ≥ 9.1 : 1): `SuccessDark #1F4536`, `WarningDark #4F2C17`, `DangerDark #4E2220`.

**Research themes (UI only, never on the map)**

| Theme | Base | Light (text on dark) | Dark (fills) | Glyph (drawn, §2.15) | Contrast base / light |
|-------|------|----------------------|--------------|----------------------|----------------------:|
| Craft | `#DDA15E` | `#EDC08E` | `#4A3420` | **Hammer** (head + handle, 45°) | 5.8 / 7.8 |
| Kin | `#8DBA74` | `#B5D9A0` | `#2A4127` | **Hearth** (flame over a base bar) | 5.9 / 8.4 |
| Reach | `#74A9D8` | `#A7CBEA` | `#22394D` | **Compass** (ring + vertical needle) | 5.3 / 7.7 |
| Turning point (keystone) | `#E2D3A6` | `#F3ECDD` | `#3D3626` | **Cut gem** (diamond with inner diamond) | 8.8 |

**Slot states** (focus bar and web slot strip)

| State | Border | Fill | Text | Glyph / motion |
|-------|--------|------|------|----------------|
| Open (Auto-fill off) | 2 px **dashed** `Accent` (drawn, §2.7) | `Surface1` | CTA in `Accent` | "+" ; border alpha pulse 0.45↔1.0, 1.6 s, until the first-ever assignment, then only for 3 s after a slot opens |
| Open (Auto-fill on) | 2 px dashed `TextTertiary` | `Surface1` | `TextSecondary` | elders glyph; no pulse |
| Pending (ordered while paused) | 1 px solid `Accent` | `Surface2` | name `TextPrimary` + caption "Starts when you resume" | clock-ring glyph |
| Working | 1 px `BorderStrong`, 3 px left bar in theme base | `Surface2` | `TextPrimary` | theme glyph; progress in theme base |
| Done (flash, 900 ms) | 2 px `Success` | `SuccessDark` | "Done" `TextPrimary` | check badge, fade out |

**Project-node states** (research web cards and chips)

| State (sim → UI word) | Border | Fill | Name colour | State glyph |
|-----------------------|--------|------|-------------|-------------|
| Hidden | not drawn | — | — | — |
| Glimpsed → **Almost ready** | 1 px **dotted** `TextTertiary` (drawn) | none | `TextSecondary` | half-filled circle, tertiary |
| Glimpsed with a hidden prerequisite | as above | none | `TextSecondary` | lock |
| Available → **Ready** | 1 px theme base | `Surface2` | `TextPrimary` | open ring, theme base |
| Queued (Ready or Almost ready) | as its state | as its state | as its state | + queue badge "Q2" (`Caption`, `TextSecondary` on `SurfaceSunken`) |
| Slotted → **In progress** | 2 px theme base | theme dark | `TextPrimary` | rank badge 1/2/3 (parchment disc, dark digit) + 4 px progress underline in theme base |
| Completed → **Known** | 1 px `Border` | none | `TextSecondary` | check badge, `Success` |
| Dormant → **Blocked** | 1 px `Warning` | `WarningDark` @ 0.5 | `TextPrimary` | check badge with slash, `Warning` |
| Selected (any) | + 2 px `Accent` outer ring (expand margin 2) | — | — | — |

### 1.5 Elevation (StyleBoxFlat)

| Level | Surface | Border | Shadow (size, offset, colour) | Radius |
|------:|---------|--------|-------------------------------|-------:|
| 1 HUD panel | `Surface1` | 1 px `Border` | 6, (0, 2), `#000` @ 0.30 | 8 |
| 2 Card / toast / popover | `Surface2` | 1 px `BorderStrong` | 10, (0, 4), `#000` @ 0.35 | 5 (cards), 8 (toasts, popovers) |
| 3 Web / modal / tooltip | `Surface3` | 1 px `BorderStrong` | 21, (0, 8), `#000` @ 0.45 | 13 (web, modals), 8 (tooltips) |

Shadows only on top-level containers (≤ 14 shadowed boxes on screen); inner cards inside an elevation-3 panel use border only.

### 1.6 Motion tokens

| Token | Duration | Easing | Use |
|-------|---------:|--------|-----|
| `Fast` | 120 ms | Out Cubic | Hover/press colour, chip toggles |
| `Base` | 180 ms open / 135 ms close | Out Cubic / In Cubic | Drawers, popovers: fade + 8 px slide |
| `Slow` | 260 ms open / 195 ms close | Out Cubic / In Cubic | Modals and web: fade + scale 0.98 → 1.0 |
| `Toast` | 220 ms in / 160 ms out | Out Cubic / In Cubic | Toasts and Moment card: fade + 21 px slide from their screen edge |
| `Pulse` | 1.6 s period | Sine | Open-slot outline alpha 0.45 ↔ 1.0 |
| `DoneFlash` | 900 ms | Out Cubic | Slot Done state, then cascade |
| `Cascade` | 220 ms | Out Cubic | Slot content slides 34 px left + fades in when projects move up a rank |
| `Progress` | 300 ms | Linear | Progress bar value; skip the tween if the jump is > 20 % |
| `CameraZoom` | τ = 90 ms | exponential (log space) | Zoom smoothing |
| `CameraMove` | 450 ms | In-out Cubic | Frame capital, jump to event |
| `CameraTurn` | 260 ms | In-out Cubic | Q/E 90° turns |

Animate only `Modulate`, `Position` (of non-container children), `Scale` and `Rotation` with `Tween`. Never animate `Size` or anything that forces container relayout. **Reduce motion** (setting): durations become 0 except 80 ms fades; no pulses; camera moves are instant.

### 1.7 Where φ is used — and where it is not

| Element | φ / Fibonacci use |
|---------|-------------------|
| Type scale | 16 → 26 → 42 by φ; 20 by √φ |
| Spacing | 8, 13, 21, 34, 55, 89 |
| Radii | 5, 8, 13 |
| Panel widths | 233 (narrow), 377 (drawers, toasts, Moment card), 610 (coach tip, diplomacy), 987 (modals), 1597 (max web) |
| Modals | 987×610, 610×377, 377×233 golden rectangles |
| Top band / focus bar | 55 and 89 high (consecutive Fibonacci) |
| Focus-bar cards | 610 : 377 : 233 = 1 : 0.618 : 0.382 |
| Research web body | canvas 61.8 % / inspector 38.2 % |
| Web slot strip | same 1 : 0.618 : 0.382 ratio as the focus bar (spatial mapping) |
| **Not φ** | Click targets 36/42/55 (usability floor); minimum text 14; body line-height 1.5 (φ = 1.618 is too loose for UI lists); the actual research shares (4:2:1 come from the sim and are shown as numbers); safe margin collapse to 13 on compact; icon sizes 16/26/34; progress bar heights; all durations |

### 1.8 Layout grid

Safe margin `M` = 21 (base) / 13 (compact). Zones never overlap; every text element sits on a panel (D3).

### 1.9 Window, stretch and scale

| Setting | Value |
|---------|-------|
| `display/window/size/viewport_width/height` | 1600 × 900 (unchanged; all numbers in this spec) |
| `display/window/stretch/mode` | `canvas_items` (unchanged; fonts re-rasterise crisply at any scale, incl. Retina) |
| `display/window/stretch/aspect` | **`expand`** (was `keep`). 2560×1080 becomes a 2133×900 logical canvas instead of pillar-boxing. 1920×1080 is exactly the base layout at 1.2×. |
| Minimum window | `DisplayServer.WindowSetMinSize(1280, 720)` at startup (scaled by `ScreenGetScale()` on macOS if the window reports physical pixels — verify once). |
| Scale floor | On startup and on `GetTree().Root.SizeChanged`: `s = min(winW/1600, winH/900) / DisplayServer.ScreenGetScale()`; `auto = max(1, 0.9 / s)`; `Root.ContentScaleFactor = auto × userUiScale`. A 1280×720 window gets 0.9 effective scale and a 1422×800 logical canvas. |
| UI size setting | 100 % / 115 % / 130 % (`userUiScale` 1.0 / 1.15 / 1.3), saved in `user://settings.cfg`. Smallest logical canvas supported: **1230 × 690** (130 % at 1600×900). |
| Breakpoints (logical) | **Compact**: W < 1440 or H < 820 · **Base**: otherwise · **Wide**: W ≥ 2000 (only adds map area) |
| Tooltips | `gui/timers/tooltip_delay_sec = 0.5`; custom tooltip panels via `_MakeCustomTooltip` (Surface3, Body text, max width 377). |

---

## 2. Component library

All components live in `game/Scripts/Ui/`. Sizes are base (1600×900).

### 2.1 Panel
- **Anatomy:** `PanelContainer` with theme variation `HudPanel` (elev 1), `CardPanel` (elev 2) or `SheetPanel` (elev 3). Padding: HudPanel 13 h / 8 v; CardPanel 13 / 10; SheetPanel 21.
- **Behaviour:** `MouseFilter = Stop` so clicks never fall through to the map; panels that only display (tile card, hint pill) use `Ignore`.

### 2.2 Button
| Variant | Height | Fill / text | Use |
|---------|-------:|-------------|-----|
| Primary | 42 (55 for the web CTA) | `Accent` / `TextOnAccent`, `BodyStrong` | One per panel: the action the panel exists for |
| Secondary | 42 | `Surface2` + 1 px `BorderStrong` / `TextPrimary` | Other actions |
| Ghost | 36 | transparent; hover `Hover` / `TextSecondary` → `TextPrimary` | Tertiary actions, links, rows |
| Danger | 42 | `DangerDark` + 1 px `Danger` / `TextPrimary` | Confirm step of destructive orders |
| Icon | 42×42 (36×36 compact) | as Ghost; icon 26 (`UiIcon`) | Rail, top-right, close |
- **States:** normal, hover (+`Hover` or `AccentHover`), pressed (`Pressed`/`AccentPressed`), focus (2 px `Accent` ring, expand margin 2, radius +2), disabled (`TextDisabled`, no shadow, **always** with a reason visible next to it or in the tooltip in plain words).
- Min width 89; horizontal padding 21 (Primary/Secondary), 13 (Ghost). Icon-plus-label: icon 16, gap 4.
- HUD buttons use `FocusMode = None` (Space must never "press" a focused HUD button). Buttons inside the web, modals and diplomacy use `FocusMode = All`.

### 2.3 Segmented control
- Row of equal segments, height 36, min segment width 38, container `SurfaceSunken` radius 5, selected segment `Accent` fill + `TextOnAccent`, others Ghost. Used for speed (½×, 1×, 2×, 4×), web view switch (Choose | Map), help tabs.

### 2.4 Chip
- Height 26 (display) or 36 (interactive), pill radius, `Eyebrow` or `Caption` text, optional 16 glyph. Interactive chips toggle: off = Secondary style, on = `Accent` 1 px border + `Accent` text. Theme filter chips show the theme glyph in theme base colour.

### 2.5 Progress bar
- `UiProgressBar : Control` drawing two rounded rects in `_Draw` (track `SurfaceSunken`, fill colour per context). Height 6 (cards) / 4 (chips). Value tween per `Progress`. Optional right-aligned `%` label in a fixed 36 px slot (`NumRegular` 14).

### 2.6 Rank badge
- 20 px disc `#E2D3A6`, digit 14 `NumBold` `#1E1608`. Always followed by the slot name (`Eyebrow`): "1 PRIMARY".

### 2.7 Slot card (focus bar, 89 high)
```
┌─────────────────────────────────────────────────────────────┐  padding 13 h / 10 v
│ (1) PRIMARY · 57%                              ETA 0:38      │  Eyebrow + Caption (tnum, slot 55 px)
│ [hammer] Pebble Choppers                                     │  glyph 16 + Heading 20 (ellipsis)
│ ███████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   42%   │  bar 6 + Caption % (slot 36)
└─────────────────────────────────────────────────────────────┘
```
- **Share %** = this slot's weight ÷ sum of occupied weights (weights 4/2/1 from constants `SlotPrimary/Secondary/Tertiary`), so a lone Primary shows 100 %.
- **ETA**: from the observed progress rate (existing `ObserveFocusProgress`), formatted `m:ss` under 10 minutes, else "12 min"; "—" until two samples exist (tooltip "Measuring speed…"); "Paused" while paused.
- **Open** (Auto-fill off): dashed border (draw with `DrawDashedLine`, dash 8 gap 5, inset 1), row 2 = "+ Choose a project" (`BodyStrong` 16 in `Accent`), row 3 = "Its share goes to the others until you choose" (Caption tertiary).
- **Open** (Auto-fill on): row 2 "The elders will choose soon" (`TextSecondary`), row 3 "Or click to pick one yourself".
- **Pending**: row 2 name, row 3 "Starts when you resume" (Caption, Accent).
- **Done**: 900 ms flash (check badge + "Done" + name), then cascade: content of lower cards slides 34 px left + fades in (`Cascade`) as the frame shows them moving up.
- **Elders' pick**: when a `CouncilPick` event fills it, a 26 px chip "Elders' pick" shows on row 1 right for one season; tooltip gives the reason (§5.6).
- **Interactions:** click → web for this slot (§4.4 flow 1/2); right-click → popover: "Move up", "Move down" (`SetSlot` swap), "Stop — keeps progress" (`ClearSlot`), "Show in research". Tooltip: §5.2.

### 2.8 Toast
- 377×72 (compact 320×64), `CardPanel` with a 3 px left bar in tier colour (Info `Info`, Milestone `Accent`, Critical `Danger`, refusal `Warning`). Row 1: 16 icon + title `BodyStrong`; row 2: body `Caption` (1 line, ellipsis; full text in tooltip). Optional trailing Ghost button ("Go there", "Open"). Close × on hover (Icon 26).
- Click anywhere on the toast = its primary action (jump camera if the event has a tile) and dismiss.

### 2.9 Moment card
- 320×128 (compact 280×112), compact `MomentCard` panel, 3 px left bar in the project's theme colour.
```
NEW KNOWLEDGE                                          ×     Eyebrow 12
[hammer] Pebble Choppers                                    Heading 18
An elder cracks a river cobble; the flake opens a bone…     Body 14 secondary, 1 line max; full text in tooltip
Chopping is now ×1.5 faster as the skill spreads.           Caption 12 (first "What changes" line)
[ Show me ]   [ Pick next ▸ ]                               Secondary 30 / Primary 30
```
- "Pick next" shows only if a slot is open and Auto-fill is off; otherwise the line reads "The elders will pick the next one." (Auto-fill on) or nothing.
- Turning points use eyebrow "A NEW AGE" and also trigger the age banner (§3.1).

### 2.10 Modal shell
- `ModalShell : Control`: full-rect `ScrimModal` (MouseFilter Stop) + centred `SheetPanel` in a golden rectangle (987×610, 610×377 or 377×233). Anatomy: Title (`Display` 42 or `Title` 26 for 377×233), optional subtitle (Body secondary), scrollable body, action row right-aligned (Primary last, rightmost; Secondary/Ghost to its left), close × top-right (Icon 42). Esc and the scrim click close (except confirm dialogs: scrim click does nothing). Opens with `Slow`. Focus moves to the primary button.

### 2.11 Tooltip
- Surface3, radius 8, padding 13, `Body` 16 primary, optional second line `Caption` secondary, max width 377, delay 500 ms, never shows while dragging.

### 2.12 Toggle
- Switch 34×20 (track pill; knob 16) + label `BodyStrong` 16; on = track `Accent`, off = `SurfaceSunken` with `BorderStrong`. Whole row is the hit target (min height 36).

### 2.13 List row
- Height ≥ 36 (one line) / 55 (two lines). 16 icon left (gap 8), text `Body`, optional meta `Caption` tertiary on line 2, optional trailing chevron 16 when clickable. Hover `Hover`; pressed `Pressed`. Rows are **pooled** (created once, updated in place).

### 2.14 Section header and scroll area
- Section header: `Eyebrow` in `TextTertiary`, 13 above / 8 below, optional trailing Ghost button. Scroll area: `ScrollContainer` with themed 6 px scrollbar (grabber `BorderStrong`, hover `TextTertiary`), vertical only; follow-focus on.

### 2.15 Icons (`UiIcon : Control`, all drawn with `_Draw`; stroke 2 px at 26, 1.5 px at 16)

| Glyph | Drawing (in a unit box) |
|-------|-------------------------|
| Pause | two vertical rounded bars, each 0.22 w, gap 0.18 |
| Play | right-pointing triangle, centroid-centred |
| Plus / Minus / Close | lines; Close = two diagonals |
| Chevron right/down | two-segment polyline |
| Check | polyline (0.2,0.55)→(0.42,0.75)→(0.8,0.3) |
| Check badge | filled circle + check in `Surface1` |
| Blocked badge | check badge + diagonal slash |
| Ring / Half ring | `DrawArc` full circle; half = filled left semicircle + ring |
| Lock | rounded rect body + arc shackle |
| Queue | two offset rounded rectangles |
| Clock ring | ring + two hands |
| Warning / Danger | triangle outline + "!" bar and dot / filled triangle + dot |
| Info | ring + "i" (dot + bar) |
| Hammer (Craft) | rotated −45°: rect head 0.5×0.2, handle line 0.6 |
| Hearth (Kin) | teardrop flame polygon over a 0.6-wide base bar |
| Compass (Reach) | ring + vertical diamond needle, upper half filled |
| Cut gem (Turning point) | diamond + inner diamond at 0.5 scale |
| People | head circle + shoulder arc |
| Hut | square + triangle roof |
| Flags (Diplomacy) | two poles with small pennants facing each other |
| Scroll (Chronicle) | rounded rect with three text lines |
| Menu | three horizontal lines |
| Spark (research event) | 4-point star |
| Globe (world event) | ring + equator line + meridian ellipse |
| Age (era event) | up chevron in a ring |
| Layer: Claims | hexagon outline, lower half filled |
| Layer: Food | three grain ellipses on a stem |
| Layer: Methods | three ascending bars |
| Layer: Resources | three stacked pebbles (circles) |
| Beacon | tall rect + triangle cap + ring at base |
| Compass rose (camera) | ring + N-needle (filled red-free `Accent` north half) rotated by −yaw |
| Seasons | Spring: stem + two leaves · Summer: disc + 8 rays · Autumn: tilted single leaf with vein · Winter: 3 crossed lines with tick ends. All in `TextPrimary` (no hue: avoids colliding with theme colours) |
| Civ emblems (34 px, white on civ colour disc, 1 px dark outline) | Sun: disc + 8 rays · Wave: two stacked sine polylines · Leaf: lens polygon + midrib · Star: 5-point · Mountain: two triangles · Flame: teardrop · Moon: crescent (two circles) · Antler: branching polyline |

`QueueRedraw()` only when glyph, tint or size changes.

---

## 3. Screen layouts

### 3.1 HUD at 1600×900 (base)

```
0                                                800                                              1600
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐ 0
│ ┌Civ card 377×55──────────┐        ┌Time cluster ≈416×55────────────┐  ┌Top-right 482×55──────┐ │ 21
│ │(☼) SUN FOLK  186 people │        │(❄) Year 3  ‖ Pause │½× 1× 2× 4×│  │Diplomacy Chronicle•3 │ │
│ │    DAWN AGE ▓▓▓░ 9/13   │        └────────────────────────────────┘  │ Help  Menu           │ │
│ └─────────────────────────┘              [ PAUSED · Space to resume ]   └──────────────────────┘ │ 84
│ ┌Rail 89┐                        ┌Coach tip 610 (y 131)─────────────┐   ┌Right drawer 377──────┐ │ 97
│ │  +    │┌Legend 233┐            └──────────────────────────────────┘   │ Chronicle (expanded) │ │
│ │ Z0 ●  ││ Food     │                                                   │ or Diplomacy (610)   │ │
│ │ Z1    ││ ■ Well fed│                    MAP (3D diorama)              └──────────────────────┘ │
│ │ Z2    │└──────────┘                                                                          │
│ │ Z3    │                                                                                       │
│ │  −    │                                                                                       │
│ │compass│                                                                                       │
│ │LAYERS │                                                               ┌Toast stack 377───────┐ │ ≥529
│ │Claims │                                                               │ toast 72             │ │
│ │Food   │                                                               │ toast 72             │ │
│ │Methods│                                                               │ toast 72             │ │
│ │Resourc│                                                               └──────────────────────┘ │
│ └───────┘ ┌Moment card 320×128─┐                                                hint pill y 727 h 34 │
│           └────────────────────┘  [ Drag to move · Scroll to zoom … ]                               │ 761
│ ┌Focus bar 1508×105 (x 46, y 774)────────────────────────────────────────────────────────────┐  │ 774
│ │[Research web T •3][Primary 610            ][Secondary 377      ][Tertiary 233  ]           │  │
│ │[Auto-fill ○ 63/yr]                                                                          │  │
│ └─────────────────────────────────────────────────────────────────────────────────────────────┘  │ 879
└──────────────────────────────────────────────────────────────────────────────────────────────────┘ 900
```

| Zone | Rect at 1600×900 (x, y, w, h) | Anchoring |
|------|-------------------------------|-----------|
| Civ card | 21, 21, 377, 55 | top-left |
| Time cluster | centred, 21, auto ≈ 416, 55 | top-centre |
| Paused pill | centred, 84, 280, 34 | top-centre; hidden while the web or a modal is open |
| Top-right cluster | W−21−482, 21, 482, 55 | top-right |
| Map-tools rail | 21, 97, 89, ≤ 507 (ends ≤ 604) | top-left |
| Layer legend card | 118, aligned to the active layer button's y, 233, auto ≤ 144 | follows rail |
| Coach tip / age banner slot | centred, 131, 610, ≤ 110 | top-centre (one at a time; banner wins) |
| Right drawer | W−21−w, 97; Chronicle w 377 h ≤ 377; Diplomacy w 610 h ≤ 664 | top-right; only one drawer open at a time |
| Toast stack | W−21−377, bottom at 761, 377, cards 72, max 3, gap 8 | bottom-right, stacks upward |
| Moment card | 21, 642, 320, 128 | bottom-left, 4 px above focus bar |
| Hint pill | centred, 727, auto ≤ 610, 34 | bottom-centre, above focus bar |
| Focus bar | centred, 774, 1508, 105 | bottom-centre |
| Tile card | floating, 320 wide | near cursor |
| Dev overlay (F3) | 21, 97 (pushes rail to y 230 while shown), 233, auto | top-left |

**Civ card (377×55):** emblem 34 (civ colour disc) · column 1: "SUN FOLK" (Eyebrow, secondary) over "186 people · 5 settlements" (`NumBold` 16 + Caption); vertical divider; column 2: "DAWN AGE" (Eyebrow) over an 89 px progress bar (4 px, `Accent`) + "9/13" (Caption tnum). Data: `CivPopulation[viewer]`, `CivSettlements[viewer]`, `FocusEra` (age name from `registries.eras[].name`), `CivBreadthDone/Needed[viewer]`. Tooltip §5.2. Hover shows Legacy rank: "Legacy 2,340 · 1st of 4".

**Time cluster:** season icon 26 with a 2 px season-progress arc (`TickInSeason / 150`, redraw ≤ 4 Hz) · "Year 3" (`Title` 26, NumBold) over "WINTER" (Eyebrow) · **Pause button** 104×42: running = Secondary with Pause icon + "Pause"; paused = **Primary (accent fill)** with Play icon + "Resume" · speed segmented ½× 1× 2× 4× (each 38×36; selected = speed index 0–3 of `SimulationSession`). Replay: an extra chip "REPLAY" (Info) left of the season icon; match ended: Pause disabled, label "Match over".

**Paused treatment (all together):** (1) Resume button accent-filled; (2) paused pill "PAUSED · Space to resume" (Eyebrow on Surface2, accent 1 px border, clickable = resume); (3) a 3 px `Accent` frame around the viewport (4 `ColorRect`s on the HUD layer, MouseFilter Ignore); (4) `ScrimPause` full-rect `ColorRect` on a CanvasLayer **below** the HUD (layer 9), MouseFilter Ignore. No shaders. Fade in/out with `Base`.

**Top-right cluster:** four Icon+label buttons, 42 high, FocusMode None: Flags "Diplomacy" (O) · Scroll "Chronicle" (C) with badge · "?" "Help" (H) · Menu "Menu" (Esc). Badge: 18 px pill on the button's top-right, `Accent` fill + `TextOnAccent` count (Info+ tier unread, "9+" max); turns `Danger` with "!" while an unseen Critical exists.

**Age banner:** on the viewer's `EraAdvanced`: 610×89 SheetPanel at the coach-tip slot, Eyebrow "A NEW AGE", `Display`-sized age name ("The Hunters age") at 42 → falls back to 26 if it does not fit, Caption "Symbolic Mind turned the age." 4 s (`Toast` motion), then the Moment card.

### 3.2 Reflow

| Element | Compact (W < 1440 or H < 820; e.g. 1280×720 → 1422×800, 130 % → 1231×692) | Wide (W ≥ 2000) |
|---------|----------------------------|-----------------|
| Safe margin | 13 | 21 |
| Civ card | 233 wide: emblem + people count + age chip (progress moves to tooltip) | as base |
| Time cluster | unchanged (essential) | as base |
| Top-right | icon-only 42×42 ×4 (tooltips carry names + keys), 208 wide | as base |
| Rail | 55 wide, icons only (36×36), legend card at x = 13+55+8 | as base |
| Focus bar | summary column 199; cards: Tertiary = max(200, 0.191·A), Secondary = max(233, 0.309·A), Primary = rest, where A = W − 2M − 16 − 199 − 13 − 26 (e.g. 1231: 457/294/200; 1422: 571/353/218) | fixed 1508 centred |
| Toasts / Moment | Toasts 320×64; Moment 280×112 at x=76 beside the 55 px rail | as base |
| Chronicle drawer | 320 × ≤ 233 | 377 × ≤ 377 |
| Diplomacy drawer | 520 (civ list 160 + detail 347) | 610 |
| Research web | inset 13, top 76, same φ split; Choose view keeps 3 columns (≥ 221 each) | max width 1597 centred |
| Tile card | 288 wide | 320 |
| Coach tip / banner | 466 wide | 610 |
| Hint pill | hidden if it would overlap the Moment card or toasts | as base |

### 3.3 Focus bar (1508×105 base)

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│┌Summary 233────────┐ ┌Primary 610──────────────────────┐ ┌Secondary 377──────┐ ┌Tertiary 233──┐│
││[Research web  T •3]│ │(1) PRIMARY · 57%       ETA 0:38 │ │(2) SECONDARY · 29%│ │(3) TERTIARY  ││
││                    │ │[hammer] Pebble Choppers           │ │[hearth] Kin Bonds │ │ + Choose a   ││
││ Auto-fill ○  63/yr │ │███████████░░░░░░░░░░░░░░   42% │ │██░░░░░░░░░   12% │ │   project    ││
│└────────────────────┘ └─────────────────────────────────┘ └───────────────────┘ └──────────────┘│
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
 panel padding 8; summary 233; gaps 13; cards 610/377/233 × 89     (glyphs above are illustrative; drawn by UiIcon)
```
- **Summary column** (no card background): row 1 Secondary button 233×42 "Research web" + key hint "T" (Caption tertiary) + a "N new" badge when `ProjectUnlocked` events arrived since the web was last opened; row 2 (36 high): Toggle "Auto-fill" (left) and "63 Insight/yr" (right, `NumRegular` 14, tooltip §5.2). Queue: when `FocusQueueCount > 0`, row 2's right text becomes "Queue 3 · 63/yr" and the tooltip lists queued names.
- Leaning moves into the web footer (§3.4).
- Shapes justified: φ widths make Primary literally the biggest (pilot read the names as descending priority; size reinforces it) while 233 keeps Tertiary readable (≥ 20 characters of Heading 20 — the E0–E2 median name is 16 characters) and clickable; the live share % carries the exact 4:2:1 numbers.

### 3.4 Research web

**Shell:** SheetPanel x 21, y 89, w W−42 (max 1597, centred), h H−89−21 (base 1558×790), radius 13, over `ScrimWeb`. The top band (time cluster) stays above and interactive. Opening: `Slow`. The focus bar is hidden behind it.

```
┌Research──────────────────────────────────────────────────────────────────────────────────────────┐
│ Research   [Choose | Map]   [ All ][hammer Craft][hearth Kin][compass Reach]  [Search…       ]  × │ header 55
│┌(1) PRIMARY · 57% ──────────────────┐┌(2) SECONDARY ─────────┐┌(3) TERTIARY ─┐ [Queue 2 ▾]       │ slot strip 55
││ Pebble Choppers ███████░░ 42%  0:38 ││ + Open — pick below   ││ + Open        │                   │
│└────────────────────────────────────┘└───────────────────────┘└──────────────┘                   │
│┌Canvas 61.8 % (924)───────────────────────────────────┐┌Inspector 38.2 % (571)──────────────────┐│
││ READY NOW · 7                                          ││ [hammer CRAFT] [DAWN AGE] [METHOD]     ││
││ ┌hammer CRAFT──────┐┌hearth KIN─────────┐┌compass REACH┐││ Pebble Choppers                        ││ Title 26
││ │ Pebble Choppers  ││ Plant Lore        ││ Calls & Ges…│││ ○ Ready to start                       ││
││ │ Chop ×1.5 …      ││ Forage yield +30% ││ Hunt +10% … │││ [      Start in Primary      ] 55      ││ Primary CTA
││ │ ~0:40 · Method   ││ ~0:35 · Discovery ││ ~0:30 · …   │││ Or: (2) Secondary  (3) Tertiary  Queue ││
││ ├──────────────────┤├───────────────────┤├─────────────┤││ WHAT CHANGES                           ││
││ │ Fire Keeping     ││ Kin Bonds         ││ Landmarks … │││ • Chop with a pebble chopper: ×1.5     ││
││ └──────────────────┘│ Power Scavenging  │└─────────────┘││ • Butchering yields +25% meat          ││
││                     └───────────────────┘              ││ WHAT YOU'LL SEE                        ││
││ ALMOST READY · 6                          [Show all ▾]  ││ An elder cracks a river cobble; …      ││
││ ◐ Hand Axes — after Pebble Choppers                    ││ NEEDS                                  ││
││ ◐ Hearths — after Fire Keeping                         ││ Nothing — you can start it now.        ││
││ + 21 more to discover in the Dawn age                  ││ LEADS TO                               ││
│└────────────────────────────────────────────────────────┘│ [Hand Axes ◐] [Prepared Cores ◐]       ││
│                                                           └────────────────────────────────────────┘│
│ Auto-fill ○  ·  Elders' leaning…           Click a project to see it · Double-click or Enter to start│ footer 34
└──────────────────────────────────────────────────────────────────────────────────────────────────────┘
```
Vertical: padding 21 · header 55 · 13 · slot strip 55 · 13 · body (flex; base 570) · 13 · footer 34 · padding 21.

**Header:** "Research" (`Title` 26) · segmented [Choose | Map] · theme chips (single-select; All default) · search `LineEdit` 233 (placeholder §5.3; `/` focuses it; Esc clears it first) · close Icon button (tooltip "Close · Esc"). Removed: W1 Overview, Hub size, type and state dropdowns.

**Slot strip:** three slot chips in the 1 : 0.618 : 0.382 ratio of the remaining width after a 144 px "Queue" chip (base 667/412/254). Each shows rank badge + name + mini progress + ETA (same data as the focus bar) or "+ Open". The **target slot** (where the CTA will put the selection) has a 2 px `Accent` border and a "TARGET" eyebrow; clicking a slot chip makes it the target (it does not assign). Queue chip opens a 377-wide popover listing queued projects (row: state glyph, name, "Remove" ghost → `Dequeue(index)`), empty state §5.3.

**Choose view (default every time the web opens):**
- "READY NOW · n" (Eyebrow): three theme columns (Craft | Kin | Reach), each with header glyph + theme name + one-line purpose (§5.7). Cards 1 per row, full column width (base ≈ 290), 89 high, gap 8: row 1 theme glyph + name (`BodyStrong` 16; Heading 20 if the column ≥ 280 wide) + state glyph right; row 2 first "What changes" line (Caption secondary, 1 line); row 3 "~0:40 · Method" (Caption tertiary: estimate §5.4 + type name). Sort: cost ascending, then name. Slotted projects show in their column with rank badge and progress (they are still "ready"). Columns scroll together (one ScrollContainer); empty column shows "Nothing ready here yet" (Caption).
- "ALMOST READY · n": rows (36 high), half-ring glyph, name, "— after {missing prerequisite name}" (or "— after a project you haven't found yet"), collapsed to 5 with "Show all (n)".
- Footer line: "+ {N} more to discover in the {Age} age" where N = Hidden projects with `Numeric` and `Era ≤ FocusEra` (names never shown). *Deliberate change from 08 §8.2, which shows fog without a count; update 08 when shipped.*
- Search replaces the sections with one flat result list grouped "Ready", "Almost ready", "Known", "Blocked" (Hidden never matches).

**Map view (radial; every known project and what it needs).** One custom-drawn control (`ResearchRadialMap`, layout in `ResearchRadialLayout`) fills the canvas column; the inspector, slot strip, chips, search and footer are unchanged. *Replaced the lanes grid, which showed no connections.*
- **Rings = ages**, Dawn in the middle; rings are drawn up to the current age and the next one (dashed, "next age"). **Sectors = tracks**, clockwise from 12 o'clock: Craft, Kin, Reach, tinted with the theme dark colour; inside a sector each **field** owns a wedge through every ring (thin separators; short field names at the outer edge when zoomed in). Age names sit in the empty wedge at 12 o'clock; the centre disc shows "{AGE} AGE" and "{done} of {n} {Age} projects known toward {turning point}".
- Inside an age ring a project's lane is its longest chain of same-age prerequisites, so **every line runs outward** from a prerequisite to what it unlocks. Fields are ordered within a track to keep cross-field lines short; projects sit inside their wedge nearest the mean angle of their prerequisites. **Turning points** (gem diamonds) sit on a dashed gate circle between the age they leave and the next, with an `Accent` ring for the age's breadth progress.
- Lines: required = solid, "any one of" = dashed `Info`; quiet (low alpha) by default. The selected (or hovered) project highlights its **whole prerequisite chain** (met links neutral, unmet `Accent`) and its **direct unlocks** in the dependant's theme colour, and dims the rest. The chain of first missing prerequisites is drawn thick `Accent`, ending at a pulsing ring on the step you can start now.
- Nodes per §1.4: Known = filled theme disc + check; Ready = bright theme ring with a halo; In a slot = progress ring + rank badge 1–3; queued = "Q{n}"; Almost ready = dim dotted ring + lock; Blocked = warning disc. Hidden projects in reached ages are faint unnamed dots (no hit, no name).
- Labels (Medium 14 on a dark pill) are placed without overlap by priority (selection, missing chain, search matches, chain, in slot, ready, …), truncated with an ellipsis; the selection's name is never cut. Level of detail: below zoom 0.3 only the selection and its missing chain are named; below 0.5 also search matches, the chain, turning points and ready or in-slot projects; icons inside nodes from zoom 0.7.
- Controls: click selects; double-click (or Enter) starts exactly like a card; wheel, trackpad scroll or pinch zooms toward the cursor; drag pans; −/+ (and the − / = keys), "Fit" (Home) shows every known project, "Selected" centres the selection, "Key" toggles the legend (open by default, bottom-left). Arrow keys move to the nearest project in that screen direction. Opening the Map, following an inspector link or pressing Enter in search centres the target (a locked target is framed with its missing chain). Theme chips dim the other tracks; search rings the matches and shows "{n} matches · Enter to jump to the first".

**Inspector (571 base; scrolls):**
1. Chips row: theme (glyph + name), "{Age} age", type (Discovery, Method, Structure, Role, Institution, Wonder, Turning point).
2. Name (`Title` 26, wraps ≤ 2 lines; tooltip shows the ID only with F3 on).
3. State line (BodyStrong 16, state colour + glyph): "Ready to start" · "In Primary · 42% · about 0:38 left" · "Almost ready — needs Hand Axes" · "Queued · #2 in line" · "Known" · "Blocked — needs copper at a settlement".
4. **Primary CTA** 55 high, full width — text by state (§4.4).
5. "Or:" row of up to three Secondary 36 buttons: the other slots ("(2) Secondary", "(3) Tertiary"; label adds "· replaces Kin Bonds" when occupied) and "Queue" / "Remove from queue". For a slotted selection: "Stop — keeps progress" (`ClearSlot`).
6. "WHAT CHANGES": ≤ 4 bullets from effects (§5.5). Method effects add the existing figure count: "12 of your people chop today."
7. "WHAT YOU'LL SEE": `ProjectDef.Moment` (Body secondary). Flavor in quotes beneath (Caption tertiary).
8. "NEEDS": rows per §4.4 flow 3: "All of these:" (`Requires`) and "Any one of these:" (`RequiresAny`), each row = state glyph + name + state word, clickable (navigates; adds "← Back to {name}" link at the top of the inspector). Hidden prerequisites: "1 more you haven't discovered yet" (lock glyph). Gates: Seen → "Your people have seen {target}" with check (a visible project's Seen gates are always met); Has → "Needs {target} at a settlement"; Near → "Needs to live near {target}"; Climate → "Needs a {target} climate" (status NOT AVAILABLE: neutral bullet). Turning points add "{done}/{needed} {Age} projects known" with a progress bar.
9. "LEADS TO": chips of known dependants (max 8, "+n more").
- Empty inspector (nothing selected): "Pick a project to see what it does." (Body secondary).

**Footer (34):** Toggle "Auto-fill" · Ghost "Elders' leaning…" (opens a 377-wide popover: three sliders with theme glyphs, live normalised %, Primary "Apply", Ghost "Cancel"; copy §5.3) · right-aligned context hint (Caption tertiary) or the last action confirmation (Caption `Success`, 4 s).

**In-web completion notice:** when a viewer project completes while the web is open: the slot strip runs the Done flash/cascade and a 34-high notice bar appears at the top of the canvas: success check + "Plant Lore is done. Tertiary is open." + Ghost "Pick for Tertiary" (sets the target). Auto-hides after 8 s.

**1280×720 (1422×800 logical) check:** web 1396×711; body ≈ 491 high; canvas 846 / inspector 523; Choose columns 3 × 270; 7 starter cards fit without scrolling (2–3 per column). At 1231×692: canvas 721, columns 3 × 227 (names wrap to `BodyStrong` 16 with ellipsis), inspector 445 scrolls.

### 3.5 Map-tools rail (89 wide, HudPanel)

Top to bottom: compass rose 42 (rotates with yaw; click = turn to north over `CameraTurn`; tooltip "Turn: Q / E · click for north") · divider · Eyebrow "LAYERS" · Claims / Food / Methods (89×55 each: icon 26 over Caption label; exclusive: clicking the active one turns it off) · 8 px gap · Resources (same size, add-on toggle with a small "+" corner mark; key R) · when `FocusBeaconsAllowed > 0`: divider + Beacon button showing slots left, or "Beacon · replace" when full (B). In Beacon mode, an empty tile places a Beacon, or moves the nearest Beacon when at capacity; clicking an own Beacon removes it.

**Legend card (233, CardPanel)**, shown while a layer is active, beside the rail aligned with that layer's button:

| Layer | Legend |
|-------|--------|
| Claims | gradient bar (viewer civ colour, 20 % → 100 % alpha) labelled "Weak" … "Strong"; Caption "70+ makes land yours. Hover a tile for the top three claims." |
| Food | four 13 px swatches with words: Well fed `#338C38` · Tight `#C79E29` · Short `#D4541F` · Famine `#A6141A` (sample the same constants as `terrain.gdshader` L105 via a shared `MapLegendColors` table); Caption "Autumn and winter show the forecast for late winter." |
| Methods | gradient `#1F6B9C` → `#9140B3` "Older tools → newer tools"; swatch `#FF8A1A` "A better method is blocked — hover for why" |
| Resources | Caption "Coloured stones mark deposits you have found. Hover a tile for the type." |

### 3.6 Chronicle

- **Collapsed (default):** the top-right "Chronicle" button with the unread badge. **Expanded:** right drawer 377 × ≤ 377 at y 97 (HudPanel), opens with `Base` from the right. Header row: "Chronicle" (`Heading` 20) + filter chips [All] [Yours] [World] (36 high) + close Icon. Body: pooled list (30 rows), **newest first**.
- **Row:** 16 class icon (tinted: research `Accent`, people/settlement `TextSecondary`, season `TextSecondary`, danger `Danger`, rivals `Info`, world `TextTertiary`, age `Accent`) · text Body 16 (≤ 2 lines) · meta line Caption tertiary "Year 3 · Winter" + " · ×5" when grouped · trailing chevron if the event has a tile (click = jump camera with `CameraMove`, Z ≤ Z1). Rows older than 3 game years render text in `TextSecondary`.
- **Grouping:** a new entry merges into the newest row when kind, civ and other civ match and it is the same game year. Births/Deaths sum their counts ("7 children were born this year"). `ProjectUnlocked` lists names ("New projects ready: Plant Lore, Fire Keeping +1"). Rival `FirstClaimed` → "The Wave Folk claimed 5 world firsts".
- **What is logged:** everything in §5.6 except `SeasonBegan` (logged only when `C != 0`: "A harsh winter has ended."), `SlotFilled` by the player, and `Adopted` below 90 %.
- **Unread:** counts new Info+ entries while collapsed; opening clears it. Esc closes it when it is the topmost layer; it is never auto-opened.

### 3.7 Notifications

| Tier / kind | Surface | Placement | Duration | Queue rule | Also logged |
|-------------|---------|-----------|---------:|------------|-------------|
| Viewer `ProjectCompleted` (+ its `Moment` A=1 within 2 s, merged) | Moment card | bottom-left | 8 s (hover pauses) | one at a time, 4 s minimum spacing; if > 2 waiting or one waited > 20 s → one summary card "3 projects finished: A, B, C" + "Open research" | yes |
| Viewer `EraAdvanced` | Age banner, then Moment card | top-centre slot | 4 s | pre-empts coach tips | yes |
| Critical (`ViewEventTier.Critical`, any `PartyIncoming`, `WarDeclared` on you, `Captured` loss, `FamineRisk`) | Toast, Danger bar | bottom-right stack | sticky until clicked/closed | always shown; pushes out the oldest Info | yes + red badge |
| Milestone (rival `EraAdvanced`, `Contact`, viewer `FirstClaimed`, `AllianceFormed`, `PeaceMade`) | Toast, Accent bar | bottom-right | 8 s | max 3 visible; new toasts enter ≥ 1.5 s apart | yes |
| Info (`CouncilPick`, gifts, barter, raid launched, `SettlementFounded`, `StructureBuilt` first of a kind) | Toast, Info bar | bottom-right | 5 s | dropped (not shown) if 3 are visible; still logged | yes |
| `OrderRefused` | Toast, Warning bar | bottom-right | 6 s | merge identical refusals within 5 s | yes |
| Ambient (births, deaths, seasons, adoption) | — | — | — | — | yes |

Auto-pause: setting "Pause on danger" (default **off**) pauses on any Critical toast.

### 3.8 Tile card (hover)

- Appears after the cursor rests 250 ms on the map (not while dragging, not over UI); hides 150 ms after leaving or on any drag. Position: cursor + (21, 21); flips left/up to stay 8 px inside the viewport. Width 320, CardPanel, MouseFilter Ignore. Content refreshes only when the tile changes or once per second.
- **Default ≤ 5 lines:** (1) `BodyStrong` "Open woodland · Yours" (owner as civ name with a 10 px emblem disc); (2) Caption secondary "River · Copper deposit" (features + known deposit) or "No known deposit"; (3) Caption "Working here: 4 — Forage 3, Chop 1" or "Nobody working here"; (4) only with fallbacks: warning glyph + "2 use older tools — need a hand axe"; (5) first 3 hovers per profile only: Caption tertiary "Hold Shift for more · Click to pin".
- **Shift held or pinned (click):** adds "Claims: You 82% · Wave Folk 21% · —", "Elevation 3", and per-job method lines "Chop: 70% of your best method (needs a hand axe)". Pinned card gets a close × and MouseFilter Stop; Esc, right-click or clicking elsewhere unpins.
- Unknown tile: one line "Unexplored". Explored-not-visible: owner "as last seen", workers "Out of sight".

### 3.9 Diplomacy drawer (610 × ≤ 664, right drawer, key O)

```
┌Diplomacy────────────────────────────────────────────────────────────────────────── × ┐
│┌Civ list 199──────┐ ┌Detail 377────────────────────────────────────────────────────┐ │
││(~) Wave Folk     │ │(~) Wave Folk                         [Contacted] [Warm +46] │ │
││    Warm · Hunters│ │ Hunters age · 3 settlements seen                             │ │
││(*) Star Folk     │ │ Their elders: "Our stores are thin; we trade for food."      │ │
││    At war        │ │ DESTINATION  [Nearest] [Settlement 4 · 23] [Settlement 9 · 8]│ │
││(?) A distant     │ │ FRIENDLY                                                     │ │
││    people        │ │ [Send gift]   Carries food to them. Improves relations.      │ │
│└──────────────────┘ │ [Barter]      Trades your surplus for theirs.                │ │
│                     │ [Form alliance] lock Needs relations above 40 (now 46 ✓) …   │ │
│                     │ HOSTILE                                                      │ │
│                     │ [Raid]        Sends 4 raiders to take stores. Hurts relations│ │
│                     │ [Declare war] Breaks any truce. Enables assaults.            │ │
│                     │ [Assault]     lock Declare war first.                        │ │
│                     │ YOUR PARTIES                                                 │ │
│                     │ Gift · 6 people · ████░░ halfway        [Recall]             │ │
│                     └──────────────────────────────────────────────────────────────┘ │
└───────────────────────────────────────────────────────────────────────────────────────┘
```
- **Civ list:** cards 199×55 (List row style) for every civ with `Aware`: emblem 26 on civ-colour disc, name `BodyStrong`, status Caption (relation word · age). Civs not yet aware are not listed. Sorted: at war, allied, contacted, aware. Selected card: Accent left bar.
- **Detail header:** emblem 34 + name `Title` 26; chips: relation status (At war `Danger` / Allied `Success` / In truce `Info` / Contacted / Known only), relations word + number (≤ −40 "Hostile", −39…−1 "Cold", 0…40 "Neutral", > 40 "Warm"; `CivRelations`), age (`CivEra`). Line: "{n} settlements seen". AI line: newest `AiWhyEntries` with this civ, shown as "Their elders: “{Reason}”" (only for `Contacted` civs; omit if none).
- **Destination chips:** "Nearest" (default) + one chip per visible, non-remembered, non-Free-Folk, non-empty settlement of that civ ("Settlement {index+1} · {pop}"). "Nearest" for assault is resolved to the visible settlement closest (hex distance between tiles) to any of your settlements. A raid with "Nearest" targets that civilization's closest settlement in simulation state, so it remains available when no enemy settlement is in sight; selecting a visible settlement sends the raid there. Gift/barter pass −1 as today.
- **Raid party size:** The player chooses 4, up to 8 or up to 12 raiders before confirming a raid; the rules send available fighters and still require at least four.
- **Order rows:** button 144×42 + consequence text (Caption secondary). Disabled: button disabled + lock glyph + reason in `TextSecondary` replacing the consequence (copy §5.8; project codes resolved to names via `ResearchCatalog.IndexOf` → `Name`). Rows show only for applicable state: "Make peace" only when at war; "Form alliance" hidden when allied; "Declare war" hidden when at war or allied (reason shown instead when allied).
- **Two-step confirm** (Raid, Assault, Declare war): first click turns the button `Danger` with label "Confirm war" / "Confirm raid" / "Confirm assault" and shows a Ghost "Cancel" for 4 s; second click queues the command; Esc or timeout reverts.
- **Your parties:** pooled rows (kind, members, progress bar from `PartyProgress`, "halfway" words: < 34 % "setting out", < 67 % "halfway", else "nearly there", returning legs "on the way home"), Ghost "Recall" per row (`RecallParty(ordinal)`).
- Empty states §5.8. Viewer ≠ 0: whole drawer shows "Orders are available only when you play your own civilization." The drawer replaces the Chronicle drawer while open. **Update rule:** rebuild rows only when the set of civ ids / settlement ids / party ids changes; otherwise update text in place (fixes A5).

### 3.10 Menus and settings

- **Game menu** (popover 233 wide under the Menu button, right-aligned, Surface3): rows (List row, 42): "Resume" (only when paused) · "Save game · F5" · "Load game… · F9" (Caption "No save yet" + disabled when absent) · "Save replay" · "Watch last replay" ("No replay yet") · divider · "Settings…" · "Help · H" · divider · "New match…" · "Quit…". Load, New match and Quit open a 377×233 confirm (copy §5.9). Save/Load/Replay results go to toasts. File existence is checked when the menu opens, not per frame.
- **Settings** (modal 610×377): UI size segmented 100 % / 115 % / 130 % · Toggle "Reduce motion" · Toggle "Show control hints" · Toggle "Show tips" · Toggle "Pause while the research web is open" (off) · Toggle "Pause on danger" (off). Saved immediately to `user://settings.cfg`.

### 3.11 Help, intro, end screen, coach tips

- **Help** (modal 987×610): segmented tabs **Controls · Research · Map layers · Tips**. Controls = the key/mouse/trackpad table of §4.1 as three columns (Action, Mouse, Trackpad) with a "Keys" column. Research = 6 short paragraphs (§5.10). Map layers = the four legends. Tips = every tip seen (title + text), with "Turn tips back on" when off.
- **Intro** (modal 610×377, first launch or `--show-onboarding`): Title "The Spark" (`Display`), subtitle, three numbered steps (rank-badge style digits + Body), Primary "Choose a first project" (Enter/Space). Esc = same as the button. Copy §5.9.
- **End screen** (modal 987×610, pauses as today): Title (`Display`), subtitle (Body), then two columns split 61.8 / 38.2: left **Standings** table (rank, emblem, name, Legacy, Firsts, Land %) and **Turning points** list (Year · civ · age · project); right **What your rivals were thinking** (grouped by civ, newest 4 each, "Year {n} · {Reason}"). Actions: Ghost "Save replay", Ghost "Watch replay", Secondary "New match", Primary "Keep watching". One-line explanation under the table: "Legacy = points for knowledge mastered plus world firsts. Land % = share of claimed land."
- **Coach tips** (non-modal): one card 610 (CardPanel) at the coach slot: info glyph, title `BodyStrong`, body Body 16, Primary 36 "Got it", Ghost "Turn off tips". Auto-hide 30 s (hover pauses); ≥ 45 s between tips (except the first two); queue max 2 (drop the oldest). Never shown while a modal is open.
- **Hint pill** (non-modal, MouseFilter Ignore): one line Caption on Surface1, context text §5.11. Visible until the player has panned, zoomed and turned at least once each (stored in `user://settings.cfg`), then only for special modes (Beacon mode, pinned tile) and while the "Show control hints" setting is on.
- **Beacon mode** (B or the rail button): clicking an empty tile places a Beacon (`PlaceBeacon`); when at capacity, it moves the nearest Beacon to that tile. Clicking one of your Beacon tiles removes it (`RemoveBeacon`) and frees a slot. The mode exits after either order. The hint pill explains which action is available and that right-click or Esc cancels. The rail button stays available when all slots are used.

### 3.12 Developer overlay (F3, also `--perf-debug`)

Caption `NumRegular` lines on HudPanel 233 wide: "60 fps", "Figures 2,412 (all 2,833)", "Zoom 12.0 · Z1", "Tick 36,000 · frame #812", "Speed 1× · sim paused: no", "UI update 0.21 ms". IDs appear in project tooltips while F3 is on.

---

## 4. Interaction spec

### 4.1 Input map (complete)

| Input | Action | Context / notes |
|-------|--------|-----------------|
| **Space** or **P** | Pause / resume | Global, handled in `_Input` before the GUI; ignored while a `LineEdit` has focus. Remove Space from `ui_accept` at startup (`InputMap.ActionEraseEvent`) so it never presses a focused button. |
| **[** / **]** | Slower / faster (½×, 1×, 2×, 4×) | Global |
| **Esc** | Close the topmost layer (§4.2); nothing open → dismiss a visible coach tip; else open the Game menu | Global |
| **T** | Open / close research web | Global (not in text fields) |
| **O** | Open / close Diplomacy | Global (**was D**) |
| **C** | Open / close Chronicle | Global |
| **H** or **F1** | Help | Global |
| **R** | Resources layer on/off | Map |
| **B** | Beacon mode | Map, only when Beacons are available; click an empty tile to place or replace, or an own Beacon to remove |
| **F** or **Home** | Frame your largest settlement (`CameraMove`, Z0) | Map |
| **Q** / **E** | Turn 90° left / right (`CameraTurn`) | Map |
| **W A S D** / **arrows** | Pan (0.85 × zoom units/s; **Shift** × 2.5) | Map only: ignored while the web, a modal, the menu or any text field has focus. **No letter in WASD toggles anything.** |
| **− / =** (and keypad − / +) | Zoom out / in one notch (screen-centre) | Map |
| **0 1 2 3** | Zoom to Z0 Hearth (5), Z1 Settlement (12), Z2 Region (40), Z3 Continent (90) | Map (web closed) |
| **1 2 3** | Put the selected project in Primary / Secondary / Tertiary | Research web |
| **Enter** | Primary CTA / primary button | Web, modals, confirm dialogs |
| **← → ↑ ↓** | Move selection between cards/chips | Web |
| **/** | Focus search | Web |
| **Tab / Shift+Tab** | Move keyboard focus within the topmost layer | Web, drawers, modals |
| **F3** | Developer overlay | Global |
| **F5** / **F9** | Quick save / quick load (confirm) | Global, live matches only |
| Left-drag (≥ 6 px) | **Pan**: the ground point under the cursor stays under the cursor | Map |
| Left-click (< 6 px movement) | Pin the tile card; in Beacon mode: place or replace on an empty tile, or remove an own Beacon | Map |
| Right-drag / Middle-drag | **Turn and tilt** (yaw −0.006 rad/px, pitch +0.004 rad/px, pitch 25°–80°) | Map |
| Option/Alt + left-drag | Turn and tilt (trackpad-friendly) | Map |
| Right-click (no drag) | Unpin tile card / cancel Beacon mode; **never places or removes anything** | Map |
| Mouse wheel | Zoom toward the cursor, ×1.15 per notch | Map |
| Trackpad two-finger scroll (`InputEventPanGesture`, vertical) | Zoom toward the cursor, `target *= 1.15^(Δy × 0.5)`, same direction as the wheel; horizontal component ignored | Map (tune the 0.5 on device) |
| Trackpad pinch (`InputEventMagnifyGesture`) | Zoom toward the gesture centre, `target /= clamp(factor, 0.8, 1.25)` | Map |
| Double-click a project card/chip | Start it in the target slot (same as CTA) | Web |
| Edge-pan | None | — |

**Camera details:** zoom target clamped 2.7–120; displayed zoom eases to the target with τ = 90 ms in log space; zoom-to-cursor = ray-hit the ground plane at the cursor before and after the size change and add the difference to `_target`. Map bounds: target clamped to the map ± 5. Q/E and the compass animate yaw only. Frame capital and Chronicle jumps ease position and zoom together (`CameraMove`). Reduce motion: instant.

**Teaching input:** (1) the hint pill (§3.11) — the mouse variant by default, switching permanently to the trackpad variant on the first `PanGesture` or `MagnifyGesture`; (2) a coach tip "Look around" after the first research assignment when the web closes; (3) the Help → Controls tab; (4) tooltips on the rail ("Zoom in · scroll up or =").

### 4.2 Esc stack and focus order

- One `UiStack` (LIFO). Pushed on open, popped on close: confirm dialog, intro, end screen, settings, help, game menu, leaning popover, queue popover, slot context popover, research web, Diplomacy, Chronicle, pinned tile card, Beacon mode. Modals block opening anything beneath them, so the top of the stack is always what the player sees in front.
- Esc closes the top entry only. Special cases: in the web, Esc with search text clears the search first; an inline "Replace which?" row or a pending two-step confirm collapses first.
- Toggle keys (T, O, C, H) close their layer if it is open anywhere in the stack.
- Focus order in the web: header (view switch → theme chips → search → close) → slot strip → canvas cards (reading order) → inspector (CTA → "Or" buttons → Needs rows → Leads-to chips) → footer. On open, focus goes to the selected card (or the first Ready card). Focus ring per §2.2.

### 4.3 Opening hold and pending orders

- **Opening hold:** when a live match starts at tick 0 with all three slots empty and Auto-fill off, call `SetPaused(true)` before the intro shows. The paused pill reads "PAUSED · choose a first project, or press Space to start". The first `SetSlot` sent by the player leaves the hold in place while the research web is open; closing the web calls `SetPaused(false)`. Pressing Space resumes normally and ends the hold.
- **Pending orders:** after sending `SetSlot`/`ClearSlot` while paused, store `pending[slot] = (project, sentTick)` and draw the slot (focus bar, web strip, node) in the Pending state. Clear when a frame with `Tick > sentTick` arrives (the frame then shows the result) or on an `OrderRefused` for `SetSlot`. Diplomacy orders sent while paused: Info toast "Order sent — it starts when you resume."

### 4.4 Click flows for the five key tasks

**1 · Set the first research (target ≤ 30 s first time; pilot 5–8 s).**
1. Launch → opening hold + intro modal → click "Choose a first project" (or Enter).
2. Web opens in Choose view; target = Primary (first open slot); the first Ready card (Craft column, cheapest) is preselected so the CTA is live.
3. Click any Ready card (inspector updates) → click **"Start in Primary"** (or double-click the card, or Enter, or 1).
4. Feedback within 100 ms: slot chip shows the project (Pending while paused), footer "Plant Lore started in Primary. Time starts when you close the research menu." The opening hold remains active while the web is open; target moves to Secondary and the CTA reads "Start in Secondary". Esc/× closes the web and starts time; the slot then changes to Working when the next frame lands.

**2 · Replace a slot.**
1. Click a working focus card (e.g. Primary) → web opens with that project selected and Primary as target.
2. Click another Ready card → CTA reads **"Put in Primary — replaces Pebble Choppers"** → click. Footer: "Pebble Choppers keeps its 42% and waits." (`SetSlot` keeps progress.)
- From the web without a target and all slots busy: CTA reads **"Choose a slot to replace…"** → it expands an inline row of three slot chips "(1) Primary · Pebble Choppers 42%", "(2) Secondary · …", "(3) Tertiary · …" (36 high, Secondary style) → click one → assigned. Keys 1/2/3 do the same without the row.
- Selected project already in another slot: CTA "Move to Primary" (swaps ranks; progress kept). In the target slot already: CTA disabled "Already in Primary" + "Stop — keeps progress" in the Or row.

**3 · Check a prerequisite.**
1. Click an "Almost ready" row/chip → inspector state line "Almost ready — needs Hand Axes"; CTA is Secondary **"Queue — starts when it's ready"**; under it, Caption: "Why can't I start this? It needs Hand Axes first."
2. NEEDS lists "All of these: ✓ Pebble Choppers — known · ○ Hand Axes — in Secondary, 30%". Click "Hand Axes" → selection jumps to it (Choose view if it is Ready, else the Map centred on it), with "← Back to Prepared Cores" at the top. From there "Start in …" or "Move to Primary".

**4 · Zoom in on a settlement.**
- Mouse: point at it and scroll up (zoom follows the cursor) or click Z0 on the rail; F frames your largest settlement. Trackpad: pinch or two-finger scroll over it. From the Chronicle or a toast: click the row/"Go there" (camera eases there at ≤ Z1).

**5 · Pause and resume.**
- Space (anywhere, incl. inside the web) or the Pause button → Resume button turns Accent, paused pill appears, the frame and scrim fade in (180 ms). Resume: Space, the Resume button or the paused pill. ETAs read "Paused"; orders queue as Pending.

---

## 5. Copy deck

Rules: plain English, second person, sentence case (caps only for Eyebrows), no codes, numbers as digits, "·" separates facts. Year shown = `frame.Year + 1`; event year = `Tick / 600 + 1`.

### 5.1 Names
- **Project names:** always `ProjectDef.Name` exactly as in content ("Calls & Gestures"). Never the ID. IDs only in tooltips while F3 is on, as a final Caption line "STN-01". Search still matches IDs.
- **Civs** (slot → emblem → name): 0 Sun Folk · 1 Wave Folk · 2 Leaf Folk · 3 Star Folk · 4 Mountain Folk · 5 Flame Folk · 6 Moon Folk · 7 Antler Folk. Viewer = "Your people" in sentences, "{X} Folk" on the civ card. Not `Aware` → "a distant people".
- **Settlements:** "Settlement {index+1}" (no names exist).
- **Ages** (from `registries.eras[].name`): Dawn, Hunters, Roots, Bronze, Iron, Faith & Fief, Discovery, Industry, Modern, Information → "the {Name} age".
- **Zoom levels:** Z0 Hearth · Z1 Settlement · Z2 Region · Z3 Continent.
- **Food bands:** Well fed · Tight · Short · Famine.
- **Project types:** Discovery · Method · Structure · Role · Institution · Wonder · Turning point.
- **Themes:** Craft — "Tools, fire, materials and building" · Kin — "Food, family, belief and health" · Reach — "Travel, contact, claims and knowledge".

**Field (lineage) names** — full names are the `lineage.name` values in `content/catalog/*.yaml` (present in the compiled pack's `lineages` list; read `pack.List("lineages")` → `GetString("name")`, keyed by `code`). Short names are a UI table (`UiText.FieldShort`) used in the Map view label column; fall back to the full name if a code is missing.

| Code | Theme | Full name (content) | Short (Map view) |
|------|-------|---------------------|------------------|
| BLD | Craft | Building & Architecture | Building |
| CER | Craft | Ceramics & Glass | Ceramics |
| CHM | Craft | Chemistry & Materials | Chemistry |
| FIB | Craft | Fiber, Hide & Textiles | Fiber & Hide |
| FIR | Craft | Fire & Heat | Fire |
| MEC | Craft | Mechanics & Machines | Machines |
| MET | Craft | Metallurgy | Metallurgy |
| MIN | Craft | Mining & Quarrying | Mining |
| NRG | Craft | Energy & Power | Energy |
| STN | Craft | Stoneworking | Stoneworking |
| WOD | Craft | Woodworking | Woodworking |
| AGR | Kin | Agriculture | Agriculture |
| ART | Kin | Arts & Culture | Arts |
| BEL | Kin | Belief & Ritual | Belief |
| FOD | Kin | Food Craft & Storage | Food Storage |
| FOR | Kin | Foraging & Plant Lore | Foraging |
| GOV | Kin | Governance & Law | Governance |
| HNT | Kin | Hunting & Fishing | Hunting |
| HUS | Kin | Animal Husbandry | Herding |
| LRN | Kin | Learning & Education | Learning |
| MED | Kin | Medicine & Health | Medicine |
| SOC | Kin | Kinship & Society | Kinship |
| COM | Reach | Language, Writing & Communication | Language |
| DEF | Reach | Defense & Fortification | Defense |
| DIP | Reach | Contact & Diplomacy | Diplomacy |
| INQ | Reach | Inquiry & Natural Science | Science |
| NUM | Reach | Number, Measure & Time | Numbers |
| SEA | Reach | Watercraft & Seafaring | Seafaring |
| TER | Reach | Claim & Settlement | Land Claims |
| TRD | Reach | Exchange & Commerce | Trade |
| TRN | Reach | Land Transport | Transport |
| WAR | Reach | Arms & Warfare | Warfare |
| WAY | Reach | Wayfinding & Cartography | Wayfinding |

### 5.2 HUD labels and tooltips

| Element | Label | Tooltip |
|---------|-------|---------|
| Civ card | "SUN FOLK" / "186 people · 5 settlements" / "DAWN AGE" "9/13" | "Your people. Next age: Symbolic Mind turns the age once 13 Dawn projects are known and its own needs are met. Legacy 2,340 · 1st of 4." |
| Season | "Year 3" / "WINTER" | "A season lasts 15 seconds at 1×. A year is one minute." |
| Pause | "Pause" / "Resume" | "Pause (Space). You can still choose research and give orders." / "Resume (Space)." |
| Speed | "½×" "1×" "2×" "4×" | "Game speed ([ and ])" |
| Paused pill | "PAUSED · Space to resume" (opening hold: "PAUSED · choose a first project, or press Space to start") | — |
| Diplomacy | "Diplomacy" | "Your neighbours and your orders (O)" |
| Chronicle | "Chronicle" | "Your people's story (C)" |
| Help | "Help" | "Controls, research and map layers (H)" |
| Menu | "Menu" | "Save, load, replays and settings (Esc)" |
| Focus summary | "Research web" "T" · "Auto-fill" · "63 Insight/yr" | Web: "See every project and choose what to learn (T)". Auto-fill: "When a slot opens, the elders pick a project after one season. Your own choices stay." Insight: "Insight is your research speed. More people, elders and learning raise it." |
| Slot (working) | "(1) PRIMARY · 57%" | "Primary gets 4 of every 7 research points (57% now). Empty slots pass their share to the others. Right-click for more." |
| Slot (open) | "+ Choose a project" | "This slot is open. Its share goes to the other slots until you choose." |
| Rail | compass, "LAYERS", "Claims", "Food", "Methods", "Resources", Beacon slots left / "Beacon · replace" | "Turn: Q / E · click for north" · "Who holds the land, and how firmly" · "Which settlements have enough food" · "How good your people's tools are" · "Deposits you have found (R)" · "A Beacon draws scouts and settlers toward a spot. Click an own Beacon in Beacon mode to remove it. (B)" |

### 5.3 Research web

| Element | Copy |
|---------|------|
| Title | "Research" |
| View switch | "Choose" · "Map" (tooltips: "Projects you can start now" · "Every field you know, by age") |
| Theme chips | "All" · "Craft" · "Kin" · "Reach" |
| Search placeholder | "Search projects (e.g. fire, fish, tools)" |
| No results | "No known project matches “{query}”." |
| Sections | "READY NOW · {n}" · "ALMOST READY · {n}" · "+ {N} more to discover in the {Age} age" · "Show all ({n})" · "Show fewer" |
| Empty column | "Nothing ready here yet" |
| Almost-ready row | "{Name} — after {Prereq}" / "{Name} — after a project you haven't found yet" |
| Slot chip open | "+ Open" (target: "+ Open — pick below") |
| Queue chip / popover | "Queue {n}" · title "Waiting in line" · row action "Remove" · empty "Nothing queued. Queue a project and it starts by itself when a slot opens." |
| Inspector empty | "Pick a project to see what it does." |
| State lines | "Ready to start" · "In {Slot} · {p}% · about {eta} left" · "Almost ready — needs {Name}" · "Almost ready — needs a project you haven't found yet" · "Queued · #{n} in line" · "Known" · "Blocked — {gate text}" |
| CTA | "Start in {Slot}" · "Put in {Slot} — replaces {Name}" · "Choose a slot to replace…" · "Move to {Slot}" · "Already in {Slot}" (disabled) · "Queue — starts when it's ready" · "Known" (disabled) |
| Or row | "Or:" · "({n}) {Slot}" / "({n}) {Slot} · replaces {Name}" · "Queue" · "Remove from queue" · "Stop — keeps progress" |
| Why-not | "Why can't I start this? It needs {Name} first." / "Why can't I start this? One step before it is still undiscovered." |
| Sections | "WHAT CHANGES" · "WHAT YOU'LL SEE" · "NEEDS" · "LEADS TO" · "All of these:" · "Any one of these:" · "1 more you haven't discovered yet" · "Nothing — you can start it now." · "No known projects yet" · "+{n} more" |
| Turning point needs | "{done}/{needed} {Age} projects known" |
| Back link | "← Back to {Name}" |
| Confirmations (footer) | "{Name} started in {Slot}." · "{Name} moved to {Slot}." · "{Old} keeps its {p}% and waits." · "{Name} added to the queue." · "{Name} removed from the queue." · paused suffix " It starts when you resume." |
| Completion notice | "{Name} is done. {Slot} is open." + "Pick for {Slot}" |
| Footer hint | "Click a project to see it · Double-click or Enter to start · 1 2 3 choose a slot" |
| Auto-fill toggled | "Auto-fill is on. The elders fill empty slots after one season; your choices stay." · "Auto-fill is off. Empty slots wait for you." |
| Leaning popover | title "Elders' leaning" · body "When Auto-fill picks, the elders favour these themes." · "Apply" · "Cancel" |
| Map view | "{AGE} AGE" · "{done} of {n} {Age} projects known toward {turning point}" · "CRAFT" · "KIN" · "REACH" · "Fit" · "Selected" · "Key" · "{n} matches · Enter to jump to the first" · "No known project matches" |
| Estimate tooltip | "An estimate at today's research speed. Practice and contact with neighbours can make it faster." |

### 5.4 Numbers and estimates
- Percent: integer "42%". Insight: integer if ≥ 10, else one decimal; "63 Insight/yr". Counts: "1,234".
- Live ETA (slots): `m:ss` < 10 min, else "{n} min"; "—" while measuring; "Paused".
- Estimate (cards/inspector, not yet slotted): `~m:ss` = `Cost / (InsightPerYear × share) × 60 s / speed`, where share = the target slot's weight ÷ (sum of occupied weights + that weight); at speed while paused use 1×. Show with "~"; prefix "as {Slot}" in the inspector ("~0:40 as Primary").

### 5.5 "What changes" phrasing (from `EffectDef`, ≤ 4 lines, skip effects without text)

| Effect kind | Template | Example |
|-------------|----------|---------|
| Method | "{Action} with {method name}: ×{mult}" (method name from the effect's `name`/`Text`; else "better tools") | "Chop with a pebble chopper: ×1.5" |
| Mod | humanised stat + value: `pct` "+30%" · `mult` "×0.8" · `flat` "+5" · `set` "→ 52%" (stat: replace `.`/`_` with spaces, sentence case) | "Forage yield +30%" |
| Hazard | mult < 1: "Fewer deaths from {cause} (−{(1−mult)·100}%)"; else "More deaths from {cause}" | "Fewer deaths from cold (−8%)" |
| Cap food / group / housing / lean | "Land feeds {+x%} more people" · "Groups can grow to {n} before splitting" · "More room in each settlement" · "Lean seasons hurt less" | "Groups can grow to 35 before splitting" |
| Cohesion | "People hold together better (+{n})" | |
| UnlockAction | "People start to {action words}" (keep_fire → "keep a fire") | "People start to keep a fire" |
| Good / Build / Role | "Adds {good}" · "Lets them build {structure}" · "New role: {role}" | |
| Reach / Reveal / Show / Lever | effect `Text` (sentence case) | "Scout range +50%" |
| Risk | warning glyph + "Risk: {Text}" | "Risk: fire can die" |
| Hazard causes | endemic illness · epidemics · hunger · cold · injuries · predators · violence · childhood illness · infant deaths · childbirth | |

### 5.6 Chronicle and toast lines (civ names per §5.1)

| Event | Line | Toast |
|-------|------|-------|
| `SeasonBegan` (harsh) | "A harsh winter has ended." | — |
| `Births` / `Deaths` | "{n} children were born this year." / "{n} people died this year." | — |
| `SettlementFounded` | "Your people founded a new settlement." / "Free Folk founded a camp." | Info "A new settlement" + "Go there" |
| `StructureBuilt` | "{Structure} built." (grouped "{n} {structure}s built") | Info on the first of each kind |
| `StructureLost` | "A {structure} was lost." | Critical |
| `FireOut` | "A hearth has gone cold." | Info |
| `FamineRisk` | "Food will run short this winter." (B=1) / "A settlement is starving." | Critical "Food warning" + "Go there" |
| `PartyIncoming` | "{A raid / An assault} party is coming." | Critical + "Go there" |
| `SlotFilled` (queue) | "{Name} moved up from the queue into {Slot}." | — |
| `CouncilPick` | "The elders chose {Name} for {Slot}: {reason}." | Info "The elders chose {Name}" |
| Council reasons (B) | Cheap "it's quick" · Leaning "it fits your leaning" · KeystonePath "it leads to the next age" · FamineRisk "food is short" · Threat "danger is near" · Disease "sickness is spreading" · CohesionCrisis "your people are drifting apart" · KnowHow "your people already practise it" · RivalsKnowIt "neighbours already know it" | |
| `ProjectCompleted` | "Your people learned {Name}." | Moment card |
| `Moment` (backfill) | "{Name}: {moment}" | — |
| `FirstClaimed` | "Your people were the first in the world to learn {Name}." / "The {Civ} were first to {Name}." | Milestone (yours only) |
| `EraAdvanced` | "Your people entered the {Age} age through {Name}." / "The {Civ} entered the {Age} age." | Banner (yours) / Milestone (rivals) |
| `Adopted` (≥ 90) | "{Name} is now known by {p}% of your people." | — |
| `DormantChanged` | "{Name} is blocked: its methods fall back." / "{Name} works again." | Info |
| `ResourceSeen` | "Your people found {target}." | Info |
| `ProjectUnlocked` | "New project ready: {Name}." (grouped) | — (badge on the web button) |
| `Aware` / `Contact` | "You learned of the {Civ}." / "You met the {Civ}." | — / Milestone + "Open Diplomacy" |
| `Skirmish` | "A border skirmish with the {Civ} left people hurt." | Critical |
| `GiftArrived` / `BarterDone` | "A gift arrived from the {Civ}." / "Your barter party came home." | Info |
| `RaidLaunched` / `RaidResolved` | "A raid party set out." / "The raid came back with food." · "The {Civ} raided one of your settlements." · "Your people drove off a raid." · "The raid failed." | Info / Critical when raided |
| `WarDeclared` | "The {Civ} declared war on you." / "You declared war on the {Civ}." | Critical / Info |
| `PeaceMade` / `AllianceFormed` | "Peace with the {Civ}." / "Alliance with the {Civ}." | Milestone |
| `AssaultResolved` | "Your assault succeeded." · "Your assault was driven back." · "A settlement fell." · "Your people held their settlement." | Milestone / Critical on loss |
| `Occupied` / `OccupationBroken` / `Captured` | "A settlement is occupied." · "An occupation ended." · "You captured a settlement." / "You lost a settlement." | Critical on loss |
| `FreeFolkFounded` | "Refugees founded a Free Folk camp." | — |
| `BeaconPlaced` | "You placed a Beacon." | — |
| `OrderRefused` | "{Order} didn't happen: {reason}." | Warning |
| Order nouns (A) | SetSlot "Your research choice" · QueueProject "Queueing" · SendGift "Your gift" · SendBarter "Your barter trip" · LaunchRaid "Your raid" · LaunchAssault "Your assault" · DeclareWar "War" · MakePeace "Peace" · FormAlliance "The alliance" · RecallParty "The recall" · PlaceBeacon "The Beacon" · other "Your order" | |
| Refusal reasons (B) | 1 "that target isn't valid any more" · 2 "something it needs is missing" · 3 "it isn't possible right now" · other "it was refused" | |
| Moment card summary | "{n} projects finished: {A}, {B}, {C}" + "Open research" | |
| Save/load toasts | "Game saved." · "Game loaded · Year {n}." · "Replay saved." · "Watching a replay." · failures "Couldn't save: {reason}." | |

### 5.7 Coach tips

| # | Trigger (existing) | Title | Text |
|---|--------------------|-------|------|
| 0 | first slot filled, web closed | Your research slots | Primary gets the biggest share of research, then Secondary, then Tertiary. Click a slot to change it. |
| 1 | first viewer `Moment` | New knowledge | When a project finishes, someone tries it first and the idea spreads. Click Show me to watch. |
| 2 | first winter | Winter | A season lasts 15 seconds at 1×. Winter is when stored food and shelter matter most. |
| 3 | first `VisibleChange` | Tools and methods | Turn on Methods to see who has the best tools. Orange tiles know a better method they can't use yet. |
| 4 | second settlement | A growing people | Your people split and founded a new settlement. Turn on Claims to see the land you hold. |
| 5 | first `EraAdvanced` | A new age | A turning point moved your people into a new age. New projects are ready. |
| 6 | first `Contact` | First contact | You met another people. Open Diplomacy (O) to see how they feel about you. |
| 7 | first `FamineRisk` | Food warning | A settlement may go hungry this winter. Turn on Food to see where. |
| 8 | web closed after the first assignment | Look around | Drag to move. Scroll or pinch to zoom. Right-drag (or Option-drag) to turn. |
| 9 | a slot opens with Auto-fill off, after tip 0 | An open slot | A slot is open. Its share goes to the others until you choose — or turn on Auto-fill. |
Buttons: "Got it" · "Turn off tips".

### 5.8 Diplomacy

| Element | Copy |
|---------|------|
| Title | "Diplomacy" |
| Empty | "You haven't met anyone yet. Scouts and growing borders will find your neighbours." |
| Viewer ≠ 0 | "Orders are available only when you play your own civilization." |
| Sections | "DESTINATION" · "FRIENDLY" · "HOSTILE" · "YOUR PARTIES" · "No parties on the road." |
| Destination chips | "Nearest" (tooltip "Raids choose their closest settlement even if it is out of sight; assaults need a visible settlement.") · "Settlement {n} · {pop}" |
| AI line | "Their elders: “{Reason}”" |
| Gift | "Send gift" — "Carries food to them. Improves relations." |
| Barter | "Barter" — "Trades your surplus for theirs." |
| Alliance | "Form alliance" — "You defend each other." |
| Peace | "Make peace" — "Ends the war. A truce follows." |
| Raid | Party size 4 / 8 / 12 · "Raid" — "Sends raiders to their closest settlement, even if it is out of sight. Hurts relations." → "Confirm raid" |
| War | "Declare war" — "Breaks any truce and harms your reputation. Lets you assault." → "Confirm war" |
| Assault | "Assault" — "Tries to take the settlement." → "Confirm assault" |
| Recall | "Recall" — tooltip "Survivors head home." |
| Disabled reasons | "Needs {Gift Giving} (research)." · "Needs {name} — both of you must know it." (alliance) · "Meet them first." · "Not while you're at war." · "Needs warmer relations (above 40; now {n})." · "A barter trip is already on the way." · "Pick one of their settlements you can see." (assault) · "Declare war first." · "You can't raid an ally." · "Not during a truce." · "Not while you hold one of their settlements." · "End the alliance first." · "Already allied." |
(Project names resolved from `TRD-01`, `TRD-02`, `WAR-02`, `DIP-03` via the catalog at runtime.)

### 5.9 Modals

| Modal | Copy |
|-------|------|
| Intro | Title "The Spark" · subtitle "You are your people's curiosity." · steps "1 Choose what they learn: click a project, then Start." · "2 Watch them use it. New knowledge changes how they live." · "3 Let time run. Press Space to pause whenever you like." · button "Choose a first project" (40 words) |
| End (KEY-3 reached) | "The Bronze age has begun" · subtitle "{Leader} lead the world in Legacy. Your people finished {rank} of {n}." |
| End (time limit) | "Forty years have passed" · same subtitle |
| End sections | "STANDINGS" (columns "Legacy", "Firsts", "Land") · "TURNING POINTS" ("Year {n} · {Civ} entered the {Age} age through {Name}") · "WHAT YOUR RIVALS WERE THINKING" · empty "No rival explanations were recorded." · buttons "Save replay" · "Watch replay" · "New match" · "Keep watching" |
| Confirm load | "Load the quick-save?" · "Anything since that save will be lost." · "Load" · "Cancel" |
| Confirm new match | "Start a new match?" · "This match ends. Save first if you want to come back." · "New match" · "Cancel" |
| Confirm quit | "Quit CivSim?" · "Unsaved progress will be lost." · "Quit" · "Cancel" |
| Settings | "Settings" · "UI size" · "Reduce motion" · "Show control hints" · "Show tips" · "Pause while the research web is open" · "Pause on danger" · "Close" |
| Help tabs | "Controls" · "Research" · "Map layers" · "Tips" |

### 5.10 Help → Research (exact paragraphs)
1. "**Slots.** You have three research slots. Primary gets 4 of every 7 research points, Secondary 2, Tertiary 1. An empty slot passes its share to the others."
2. "**Insight** is your research speed. More people, elders and learning raise it."
3. "**Ready** projects can start now. **Almost ready** projects need one more thing first. Queue them and they start by themselves."
4. "**Turning points** move your people into a new age once enough projects of the current age are known."
5. "**Auto-fill** lets the elders choose when a slot opens. Their **leaning** says which themes they favour."
6. "Progress is never lost. Replace or stop a project any time; it keeps its progress."

### 5.11 Hint pill variants
- Mouse: "Drag to move · Scroll to zoom · Right-drag to turn · Space to pause"
- Trackpad (macOS): "Drag to move · Pinch or two-finger scroll to zoom · Option-drag to turn · Space to pause"
- Windows trackpad wording uses "Alt-drag".
- Beacon mode: "Empty tile: place or replace · Your Beacon: remove · Right-click/Esc: cancel" (when full: "Empty tile: replace nearest · Your Beacon: remove · Right-click/Esc: cancel")
- Pinned tile: "Esc or right-click to unpin"
- Web footer (see §5.3).

---

## 6. Implementation notes

### 6.1 Build order (highest player impact first)

| Step | Work | Fixes |
|-----:|------|-------|
| 1 | `UiTokens`, `UiTheme` (fonts, styleboxes, type variations), `UiIcon`, `UiKit`, root theme `Control`, stretch `expand`, min window, scale floor, settings file | A9 foundation |
| 2 | Input router + `UiStack`: Space/P global, D→O, WASD gating, Esc stack, Beacon mode, left-drag pan / right-drag turn, zoom-to-cursor + smoothing, trackpad/pinch constants | A1, A6, A7, A8 |
| 3 | Top band: civ card, time cluster with Pause/speed, paused treatment, opening hold, F3 overlay, top-right buttons + Game menu (move save/load/replay); Year + 1 | A1, A2, A10, A11, A15 |
| 4 | Focus bar: slot cards (φ widths, live share, ETA, open/pending/done/cascade), summary column | A2, A3, A14 |
| 5 | Research web: shell, slot strip, Choose view, inspector, CTA flows, effect phrasing, field names; then Map view | A4, A5 (web), A20 |
| 6 | Notifications: Moment card, toast stack, Chronicle drawer (pooled, grouped) | A12, A13, A5 |
| 7 | Map-tools rail, legend card, compass, hint pill, tile card | A16 |
| 8 | Diplomacy drawer | A5, A7 |
| 9 | Modal shell; intro, help, settings, end screen, coach tips | A17, A19 |
| 10 | Motion polish, reduce motion, UI size setting, screenshots (§6.5) | — |

### 6.2 Recommended shared API (`game/Scripts/Ui/`)

```csharp
public static class UiTokens {               // constants only
    public const int Sp1 = 4, Sp2 = 8, Sp3 = 13, Sp4 = 21, Sp5 = 34, Sp6 = 55, Sp7 = 89;
    public const int R1 = 5, R2 = 8, R3 = 13, ControlS = 36, ControlM = 42, ControlL = 55;
    public static readonly Color Surface1 = new(0x1F1B16E6), Accent = new("#E9BE4E"), /* … all §1.4 tokens … */;
    public static Color Theme(Theme t), ThemeLight(Theme t), ThemeDark(Theme t);
    public static Color Civ(int civ);                      // existing Okabe–Ito table
    public const float FastMs = 120, BaseMs = 180, SlowMs = 260, ToastInMs = 220, ToastOutMs = 160;
}
public enum UiText { Display, Title, Heading, Body, BodyStrong, Eyebrow, Caption, Num, NumStrong }
public enum UiGlyph { None, Pause, Play, Plus, Minus, Close, ChevronRight, ChevronDown, Check, CheckBadge, BlockedBadge,
    Ring, HalfRing, Lock, Queue, ClockRing, Warning, Danger, Info, Hammer, Hearth, Compass, Gem, People, Hut, Flags,
    Scroll, Menu, Spark, Globe, Age, LayerClaims, LayerFood, LayerMethods, LayerResources, Beacon, CompassRose,
    Spring, Summer, Autumn, Winter, EmblemSun, EmblemWave, EmblemLeaf, EmblemStar, EmblemMountain, EmblemFlame,
    EmblemMoon, EmblemAntler }
public static class UiTheme {
    public static Theme Build();                           // once; all type variations + fonts + styleboxes
    public static Font Regular { get; } public static Font Medium { get; } public static Font Bold { get; }
    public static FontVariation Caps { get; } public static FontVariation Num { get; } public static FontVariation NumBold { get; }
}
public static class UiKit {
    public static PanelContainer Panel(UiElevation level, string? name = null);
    public static Label Text(string text, UiText style, Color? color = null, int minWidth = 0, bool rightAlign = false);
    public static Button Primary(string text, Action pressed, string? tooltip = null, UiGlyph icon = UiGlyph.None, int height = UiTokens.ControlM);
    public static Button Secondary(string text, Action pressed, string? tooltip = null, UiGlyph icon = UiGlyph.None);
    public static Button Ghost(string text, Action pressed, string? tooltip = null, UiGlyph icon = UiGlyph.None);
    public static Button Danger(string text, Action pressed);
    public static Button IconButton(UiGlyph glyph, string tooltip, Action pressed, int size = UiTokens.ControlM);
    public static UiSegmented Segmented(string[] labels, int selected, Action<int> changed);
    public static UiChip Chip(string text, UiGlyph glyph = UiGlyph.None, Color? tint = null, bool interactive = false);
    public static UiProgressBar Progress(Color fill, int height = 6, bool showPercent = false);
    public static UiToggle Toggle(string label, bool on, Action<bool> changed);
    public static Control SectionHeader(string caps, Control? trailing = null);
    public static ScrollContainer Scroll(Control content);
    public static UiIcon Icon(UiGlyph glyph, int size, Color color);
    public static bool SetText(Label label, string text);  // assigns only when different; returns true if changed
    public static void Show(CanvasItem item, bool visible, UiMotion motion = UiMotion.Base);   // tweened fade/slide
}
public sealed partial class UiIcon : Control { public UiGlyph Glyph { get; set; } public Color Tint { get; set; } }
public interface IUiLayer { string Name { get; } bool IsModal { get; } void Close(); }
public sealed class UiStack { public void Push(IUiLayer l); public void Remove(IUiLayer l); public bool CloseTop(); public bool Contains(IUiLayer l); }
public sealed class UiSettings { public float UiScale; public bool ReduceMotion, Hints, Tips, PauseInWeb, PauseOnDanger; public static UiSettings Load(); public void Save(); }
```
Components (one file each): `TopBar` (CivCard, TimeCluster, TopRightCluster, PausedOverlay), `FocusBar` + `SlotCard`, `ResearchWeb` (`ChooseView`, `MapView`, `Inspector`, `SlotStrip`, `QueuePopover`, `LeaningPopover`), `MapToolsRail` + `LegendCard`, `ChronicleDrawer`, `ToastHost`, `MomentCard`, `TileCard`, `DiplomacyDrawer`, `ModalShell` (+ `IntroModal`, `HelpModal`, `SettingsModal`, `EndScreenModal`, `ConfirmDialog`), `GameMenu`, `CoachTips`, `HintPill`, `DevOverlay`, `CameraController`, `InputRouter`, `UiText` (copy tables, field names, effect phrasing).

### 6.3 What to keep, replace or delete

| File | Keep (logic) | Replace (UI) | Delete |
|------|--------------|--------------|--------|
| `Game.cs` | sim/session/frame plumbing, renderer, benchmark, `PlaceBeacon` (call only from Beacon mode), `FrameCapital` (eased), `SetZoom` (becomes target), `UpdateCamera`, politics availability rules in `UpdatePoliticsButtons` (move into a `DiplomacyModel` that returns `(enabled, reasonKey)` per order), `Queue*` methods, `TryGetTargetSettlement` | `CreateHud` status label + help strip → TopBar; `CreatePoliticsPanel`/`UpdatePoliticsControls` → DiplomacyDrawer; `_UnhandledInput`/`_Input`/`HandleEscapeAction` → InputRouter + UiStack; `PanCamera` → CameraController (gated); `UpdateHud` 5 Hz rebuild → event-driven updates | `NewPicker`, `NewOrderButton`, `SelectItemId`, status-alert string |
| `M7cHud.cs` | tile picking (`UpdateTileHover` ray math), `DescribeTileWorkers`, `TerrainName`, `ActivityName`, `ToolName`, overlay byte builders, `ProcessViewEvents` (as the event router), `FocusChronicleTile` | overlays panel → MapToolsRail; chronicle UI → ChronicleDrawer (pooled); moment banner → MomentCard; hover panel → TileCard; `DescribeEvent`/`RefusalText` → `UiText` tables §5.6 | `RefreshChronicleText` rebuild loop, `_overlayLegendLabel` |
| `M7dHud.cs` | save/load/replay/new-match methods, intro preference file, onboarding trigger detection (`ProcessM7dFrame`), end-screen data gathering | toolbar → TopRight + GameMenu; tip panel → CoachTips; intro/help/end → modal shell components | per-frame `RefreshM7dButtons` (`File.Exists` on menu open only), `ToolbarButton` |
| `FocusResearchHud.cs` | `ObserveFocusProgress`, `FormatEta` (new format), `CanAssignResearchProject`, `AssignResearchToSlot`, `QueueSelectedResearch`, `RemoveSelectedResearchFromQueue`, `SetResearchCouncil`, `ApplyLeanings`, `ResearchMatches` (+ field names), `DependsOn`, `FindFocusSlot`, `TryGetActivity`, method figure counts | everything that builds controls; `UpdateResearchSelection` → Inspector bound on selection change and on frames that change the selected project's state | `ResearchOverviewCell`, overview mode, filters `OptionButton`s, hub emphasis, `FocusSlotTitles` "4/7" strings, `ResearchProjectButton.cs` (drag-and-drop) |
| `project.godot` | — | `window/stretch/aspect="expand"`, `gui/timers/tooltip_delay_sec=0.5` | — |
| Dev args | `--research-web`, `--show-onboarding`, `--screenshot`, `--start-minute`, `--overlay` | `--research-overview` → `--research-map` | — |
| New dev args (UI only) | `--ui-open=web|map|inspector-glimpsed|diplomacy|chronicle|help|settings|end|menu`, `--paused`, `--ui-scale=1.3`, `--reduce-motion` for screenshot capture | | |

### 6.4 Performance rules (the renderer budget is 2,400 figures at ≥ 60 fps, Z0–Z2 with overlays)

1. Build every control tree once at startup (the web's nodes only for numeric projects with `Era ≤ 2` plus turning points: 148); show/hide with `Visible`. No `QueueFree`/`new` of controls in steady state.
2. Update on change, not on a timer: bind HUD updates to "new `MatchFrame` applied" and diff against cached values (`UiKit.SetText` returns early when equal; cache ints, not strings). The only timers: ETA labels 1 Hz, season arc 4 Hz, tooltips/animations.
3. Research web refresh: per frame, compare `FocusProjectState` bytes and slot arrays with the previous frame; restyle only changed nodes; rebuild the inspector only when the selection or its state changes. No LINQ, no `Dictionary` allocation, no `CountVisibleDependents` per refresh.
4. Pooled rows: Chronicle 30, toasts 3 + 1 Moment card, diplomacy lists 8 civs / 16 settlements / 16 parties, queue 8, inspector needs 12 / leads-to 8.
5. Tile card: recompute when the hovered tile changes or once per second; the worker scan over figures runs at most 1 Hz.
6. Styling through the single `Theme` + type variations; never call `AddTheme*Override` per update (state changes swap `ThemeTypeVariation` on the control).
7. No full-screen shaders, `BackBufferCopy` or blur. Full-screen elements are only flat `ColorRect` scrims (≤ 2 at a time). ≤ 14 shadowed boxes on screen.
8. `UiIcon.QueueRedraw` only on property change; the zoom marker and compass redraw only when zoom/yaw change.
9. Measure under `--perf-debug`: UI update time per frame and `GC.GetAllocatedBytesForCurrentThread()` delta per second; targets **< 0.5 ms** average UI work per frame and **0 bytes/s** steady-state allocation with the HUD idle, **< 64 KB/s** with the web open and running at 4×.

### 6.5 Verification checklist

**Screenshots** (`--screenshot` + `--ui-open`), at **1600×900, 1280×720, 1920×1080, 2560×1080** (or `--resolution 2133x900` if the display clamps), plus **1600×900 at `--ui-scale=1.3`**:
1. Default HUD at minute 0 (opening hold, intro) and minute 20 (`--start-minute=20`) with Food layer on.
2. Paused state (`--paused`).
3. Web Choose view at minute 0; web Map view at minute 20; inspector on an Almost-ready project; "Replace which?" row open.
4. Focus bar with one open, one working, one pending slot.
5. Chronicle expanded at minute 20 (grouping visible); a toast stack of 3; a Moment card.
6. Diplomacy with a contacted civ (minute 20+ omniscient or a seed with contact); help; settings; end screen (`--start-minute=40`).
Check on each: no text < 14 design px; nothing clipped; nothing overlapping (§3.1 zones); no text directly on the map; every disabled control shows a reason; no project ID visible with F3 off.

**Perf:** run the existing `--benchmark-matrix=all` at minute 70 with the new HUD visible and with the web open in a second run; every case ≥ 60 fps minimum 1 s window; report the §6.4 UI timings.

**Usability walk-throughs** (fresh profile, `--show-onboarding`, 1600×900 and 1280×720): (1) set first research ≤ 30 s, 2 clicks after the intro; (2) replace Primary from the focus bar in ≤ 3 clicks; (3) find why an Almost-ready project can't start and open its prerequisite in ≤ 2 clicks; (4) zoom onto a settlement with wheel, trackpad scroll and pinch; (5) pause and resume with Space while the web is open and closed. Also: Esc closes web → help → menu in that order; typing "wood" in search does not move the camera; right-click never places a Beacon; D only pans.

---

## 7. Open risks and what M9 must measure

### 7.1 Risks
| Risk | Mitigation |
|------|------------|
| Switching left-drag from orbit to pan breaks the pilot's habit | Hint pill + "Look around" tip; Alt/right-drag still orbit; measure (below). |
| φ card widths (50/31/19 %) could be read as the actual shares (57/29/14 %) | Live share % on every card; Help paragraph 1. Watch the slot-meaning question. |
| Esc opening the Menu after the last panel closes may surprise | Menu is harmless and its first row is Resume when paused; if players report it, change to "Esc twice within 1 s". |
| Opening hold: "nothing moves" before the first choice | Paused pill copy names the reason; Space starts. |
| Avenir Next lacks `tnum` or a glyph on another OS | Fixed-width number slots; all symbols drawn; fallback list. |
| Trackpad scroll constant varies by device and natural-scrolling setting | One constant (0.5), tuned on device; direction verified with natural scrolling on/off. |
| "+N more to discover" deviates from 08 §8.2 (no counts) | Decided here; update 08 §8.2 when shipped. 08 §6–§8 also need updating for the new layout. |
| `ContentScaleFactor` × `canvas_items` on Retina | Verify that the formula uses apparent (point) size on macOS; screenshots at 1280×720. |
| Gate status for Has/Near/Climate is NOT AVAILABLE in `MatchFrame` | Shown as neutral conditions; Blocked state names the gate. |
| NOT AVAILABLE (skipped): Council suggestions before a pick, effective cost/Know-how glow, keystone-path lens, minimap, Director mode, settlement names, adoption % except 50/90/100 events | Designed without them. |

### 7.2 M9 UI measures (add to the session record)
| Measure | How | Target |
|---------|-----|--------|
| Time to first assignment | stopwatch from window shown (protocol) | ≤ 30 s for every participant; median ≤ 10 s |
| First assignment method | CTA / double-click / key / slot click | record (expect CTA) |
| Open-slot dwell | seconds a slot stays open after a completion, Auto-fill off (observe or `--playtest-log`) | median ≤ 20 s real time |
| Pause discovery | first pause unprompted, and how (Space / button) | ≥ 6 of 8 pause at least once in 10 min |
| Zoom discovery | first zoom action and device (wheel / trackpad / pinch / rail) | all within 2 min; no "how do I zoom?" question |
| Camera confusion | any unintended orbit/pan, accidental Beacon (should be 0) | 0 accidental Beacons |
| Esc success | player presses Esc to close a panel and it closes | 100 % |
| Prerequisite understanding | ask "Why couldn't you start X?" when they inspect an Almost-ready project | ≥ 6 of 8 answer correctly |
| Moment engagement | clicks "Show me" or "Pick next" at least once | ≥ 5 of 8 |
| AI-action naming | uses a civ name ("Wave Folk") when explaining an AI action (P9 question 2) | record; supports P9 |
| Slot meaning (P9 Q3) | quote | ≥ 7 of 8 say descending priority |

An optional `--playtest-log` flag (UI only) may append timestamped UI events (web open/close, assignments, pause, zoom input type, Esc) to `user://playtest/ui-<date>.csv`; no personal data.
