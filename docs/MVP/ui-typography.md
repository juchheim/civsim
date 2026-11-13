# UI typography and colour (Living Ages pass, 2026-11)

This replaces §1.1, §1.2 and the colour values of §1.4 in [20-ui-design-spec.md](20-ui-design-spec.md). Layout, components and
flows in doc 20 are unchanged. The code lives in two files: `game/Scripts/Ui/UiTokens.cs` (numbers and colours) and
`game/Scripts/Ui/UiTheme.cs` (fonts and the Theme built from them). Nothing else should set a font, a size or a colour by hand.

## 1. One family, two voices

The whole game uses **Commissioner** (Kostas Bartsokas, SIL Open Font License; `game/assets/fonts/commissioner/`, licence in
`OFL.txt` beside it). It is a variable font with four axes: weight `wght` 100–900, slant `slnt` −12–0, flare `FLAR` 0–100 and
volume `VOLM` 0–100. Flare turns the stroke ends into small wedge flares, which gives the carved, lapidary feel of the logo's
lettering; at reading sizes it only adds noise, so it is used by size:

| Voice | `UiTheme` font | wght | FLAR | VOLM | Used for |
|-------|----------------|-----:|-----:|-----:|----------|
| Display | `Display` | 760 | **100** | 40 | Titles, modal and sheet titles, the age banner, big numbers |
| Display semibold | `DisplaySemi` | 660 | **100** | 40 | Headings (20 px), settlement names on the map |
| Display numbers | `DisplayNum` | 760 | **100** | 40 | The top-bar year, headline figures (`NumTitle`, `NumYear`), map floaters |
| Bridge | `DisplayLead` | 660 | 50 | 0 | The 18 px step: section titles in sheets, moment headings |
| Text | `Regular` / `Medium` / `Bold` | 430 / 540 / 650 | 0 | 0 | Body, buttons, lists, tooltips, captions |
| Caps | `Caps` | 600 | 0 | 0 | ALL-CAPS eyebrows, +1 px tracking |
| Numbers | `Num` / `NumBold` | 430 / 650 | 0 | 0 | Counts, rates, table figures |

Weights sit a little above the nominal 400/500/600 because the text is light on dark surfaces, where thin strokes break up at
14–16 px.

**Figures.** Commissioner has proportional figures only (no `tnum`, no `lnum`; its features are `case`, `frac`, `ordn`,
`sups`, `subs`, `ss02`, `kern`). The number fonts still ask for `tnum` in case a future cut adds it, but what keeps numbers
steady is layout: every changing number sits in a fixed-width slot and is right-aligned in tables (doc 20 §1.1 already asked for
this). The top-bar date column is sized for the widest year string, so the year never moves the cluster.

**Missing glyphs.** ✓ ✗ → ★ and non-Latin scripts fall back to the system UI font (`UiTheme.FallbackNames`), then to any
system font. The glyph policy of doc 20 §1.1 still holds: symbols are drawn by `UiIcon`.

**Loading.** `UiTheme.Family` loads the imported font (the font's `.import` file is exempt from the `*.import` rule in `game/.gitignore`, so it can be committed); if the
project has not been imported yet it loads the TTF directly. `UiTheme.Build()` also sets `ThemeDB.FallbackFont`, so controls
outside the HUD theme (map `Label3D`s, the boot error) use the text voice.

## 2. Type scale: φ in quarter steps

Every size is `16 × φ^(n/4)`, rounded. Four quarter steps make one whole φ step, so 16 → 26 → 42 are exact golden steps and the
sizes between them are evenly spaced on the same ratio (×1.128 per step).

| n | px | Token | Styles | Voice |
|--:|---:|-------|--------|-------|
| −2 | 13 | `FontMicro` | `NumBadge`, `MomentCaption`, `MomentEyebrow` | Text (badges only, never instructions) |
| −1 | 14 | `FontSmall` | `Caption*`, `Eyebrow*`, `Num`, tooltips, compact chips | Text / Caps |
| 0 | 16 | `FontBody` | `Body*`, `NumBody`, `NumStrong`, buttons, inputs | Text |
| 1 | 18 | `FontLead` | `SectionTitle`, `MomentHeading` | Bridge (FLAR 50) |
| 2 | 20 | `FontHeading` | `Heading` | Display (FLAR 100) |
| 4 | 26 | `FontTitle` | `Title`, `NumTitle` | Display |
| 5 | 29 | `FontYear` | `NumYear` (top-bar year), the start button | Display |
| 8 | 42 | `FontDisplay` | `Display` (end screen, age banner) | Display |

14 px is the reading floor; with the window-scale floor of doc 20 §1.9 it never renders below about 12.6 px.

**Leading.** Line pitch is `size × φ` (1.618) for reading text and `size × √φ` (1.272) for display and all-caps lines.
`UiTheme.Leading()` turns that into Godot's `line_spacing` from the font's real height at that size (Commissioner's line box is
1.22 em). Godot adds `line_spacing` only between wrapped lines, so a single-line label keeps its height and the HUD's fixed
bands are not disturbed. Body 16 px → 26 px pitch; caption 14 px → 23 px; heading 20 px → 26 px; display 42 px → 53 px.

## 3. Spacing and proportions

Unchanged from doc 20 §1.3 and still Fibonacci: spacing 4 · 8 · 13 · 21 · 34 · 55 · 89; widths 144 · 233 · 377 · 610 · 987;
radii 5 · 8 · 13. Two places use √φ steps between Fibonacci widths: the compact civ card grows from 233 to at most 296
(233 × √φ) when the centred time cluster leaves room, and compact toasts are 377 wide.

## 4. Palette (from the logo)

The logo is a terracotta spiral (`#B8501F`, highlights `#D9703A`, shadows `#9C3B14`) over the words LIVING AGES in a dark
oxblood (`#4A1505`) on white. The UI turns that around for a dark HUD over a bright map: oxblood-brown surfaces, parchment text,
and terracotta as the one bright accent.

**Brand** (`UiTokens.Brand*`): terracotta `#B8501F`, ember `#D9703A`, deep `#9C3B14`, oxblood `#4A1505`, parchment `#F5EAD7`.

| Group | Token | Value |
|-------|-------|-------|
| Surfaces | `Surface1` (HUD panels) | `#21130E` @ 0.90 |
| | `Surface2` (cards, toasts) / hover / pressed / disabled | `#2D1B13` / `#3B261C` / `#462E22` / `#25160F` |
| | `Surface3` (sheets, modals, tooltips) | `#37231A` |
| | `SurfaceSunken` | `#140A06` @ 0.60 |
| | Map label boxes (`SurfaceMap*`) | `#21130E` @ 0.85–0.88, `#2D1B13` @ 0.95 |
| | Scrims (modal / web / pause) | `#0D0604` @ 0.55 / 0.45, `#3A1A0C` @ 0.14 |
| | `Border` / `BorderStrong` / `Hover` / `Pressed` | parchment `#F5EAD7` @ 0.10 / 0.22 / 0.06 / 0.10 |
| Text | `TextPrimary` / `Secondary` / `Tertiary` / `Disabled` | `#F5EAD7` / `#D6C3AA` / `#B39C84` / `#806B5B` |
| | `TextOnAccent` (oxblood ink) | `#2A0D04` |
| Accent | `Accent` / `AccentHover` / `AccentPressed` / `AccentDisabled` | `#E07A45` / `#EA8D5A` / `#CA6835` / `#4E3426` |
| Semantic | `Success` / `Warning` / `Danger` / `Info` | `#6FC79B` / `#E8B54A` / `#F0697E` / `#9FC3E0` |
| | `SuccessDark` / `WarningDark` / `DangerDark` / `InfoDark` | `#1E4332` / `#4A3613` / `#4F1A24` / `#22394D` |

The accent is the logo's ember lifted one step so it can be both a fill and a text colour. Because the accent is now orange, the
semantic colours move away from it in hue: **warning is ochre** (it was orange, which would read as the accent) and **danger is
a rose crimson**. Each is still paired with its glyph (doc 20 §1.4). Research-theme colours, civilization colours (Okabe–Ito)
and the map-layer legend colours are unchanged because they must match the map.

**Contrast** (WCAG 2.x), against `Surface1` composited over the worst map pixel (sunlit snow), and against `Surface2`:

| Colour | on Surface1 (worst) | on Surface2 |
|--------|--------------------:|------------:|
| TextPrimary | 11.8 | 13.8 |
| TextSecondary | 8.2 | 9.6 |
| TextTertiary | 5.4 | 6.3 |
| Accent | 4.7 | 5.5 |
| Success / Warning / Danger / Info | 6.9 / 7.4 / 4.7 / 7.6 | 8.1 / 8.7 / 5.5 / 8.9 |
| TextOnAccent on Accent / Hover / Pressed | 6.1 / 7.3 / 4.8 | |
| TextPrimary on Success/Warning/Danger dark fills | 9.3 / 9.6 / 11.7 | |

`TextDisabled` (2.8) is for disabled labels only, as before.

## 5. The top bar

The time cluster is two **equal wings** around the logo, so the logo sits exactly on the screen's vertical centre line whatever
the window size:

```
[ civ card ]      [ dial  Year 13 / SPRING ][ logo ][ ½× 1× 2× 4×  (REPLAY) ]      [ Decrees Diplomacy Chronicle Menu ]
                  |<-------- wing -------->|        |<-------- wing -------->|
```

`LayoutTopBar` sizes the wing for the longest date it can show ("Year 888" at 29 px over "16 YEARS LEFT"), and never smaller
than the speed control (plus the REPLAY chip in a replay, which widens both wings together). If the labelled top-right buttons
would not leave room for that, they switch to icon buttons (as in compact windows); if even that is too tight, the wings shrink
together and the season line ellipsizes. In compact windows the civ card takes what is left, 233 to 296 px.

Measured with the `typography` scenario (`--scenario=typography --ui-debug`): at 1600×900 the wings are 195 px each
(x 556–751 and 849–1044), the logo's centre is x 800; at 1280×720 (logical 1422) they are 195 px each and the logo's centre is
x 711.1 = 1422.2 / 2; at 2560×1080 (logical 2133) and at 130 % UI size the same holds.

## 6. Checking it

`--scenario=typography --shots=<dir>` tours the HUD, tile card, toasts, research web (list, inspector, map), all four People
tabs, Diplomacy, Decrees, Chronicle, menu, help, settings, intro, moment, age banner, paused state, the longest date string
and the end screen. Run it at 1600×900, 1280×720, 2560×1080 and with `--ui-scale=1.3`.
