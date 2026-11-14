# 33. Settlement name plates: a redesign of the map labels

Status: phases 0 and 1 built, uncommitted (2026-11-13, `codex/e5-art-p0`): `SettlementPlate.cs` + `SettlementPlateTests.cs` (sim), `UiDome.cs` + `UiSettlementPlates.cs` (game), `Capital` frame flag, hover/click through `TryPickTileOrPlate`, scenarios `plate-gallery` and `plates`. Q12 resolved: frame hashes only compare runs with each other, so the flag bit is safe. Deviations from the text below: dome 40/50/60/70/80 px at 2.2 : 1 (2.5 : 1 looked like a bump on the ribbon); the pointer rises by the tier's world height but at most 84 px, so close zoom does not fling it off screen. Phase 2 built the same day (uncommitted): one alert chip (priorities 1 to 9, privacy per Q5; hungry winter, fire out and splitting only at close zoom or on hover), pulsing rim for siege, raid and occupation (off under ReduceMotion), shoulder dot on compact plates and urgent pips, defence silhouette (thorn, palisade, battlements, castle with two towers), eight-segment ring meter (build progress, or wall integrity in Danger while besieged), hover line beside the ribbon, generated default names (Godot side only). Chip is 20 px high and tucked 1 px under the ribbon; the placeholder inner ring was dropped for foreign plates. Phase 3 built the same day (uncommitted): the Settings row "Settlement plates: Full / Names only / Off" (`UiSettings.SettlementPlates`, saved; Names only is a new `PlateLevel.Names` ribbon with a small emblem and an alert dot, hover and Alt still show the full plate), motion (`PlateMotion`: dome grows from the ribbon and fades in over 0.18 s when a plate appears or gains detail; the chip slides down 4 px with one bright pulse climbing both sides of the rim; the rim recolours from the feet up and the emblem turns over when a settlement changes hands; new defence stakes rise over 0.26 s; the ring catches up segment by segment and flashes Success once when a build finishes; all of it rests under Reduce motion), double-click on a plate pins it and frames the camera, a Tin Folk mark (pale tin disc with an ingot), and a colour-blind fix: war and alliance now also carry a shape (cross or check) on the dome's left shoulder, because red and green rims became the same olive under deuteranopia and protanopia filters. Names-only plates carry no such mark. Left out: an E5 chip kind (only if playtests ask), a Tower/Raid glyph (chips reuse Danger, Warning, Info, Hearth and Flags). Planned 2026-11-13 on `codex/e5-art-p0`. Revised the same day for the owner's request: a
half-circle (dome) shape, rounded side up, instead of a rounded rectangle, and nothing that looks generic.

## 0. In plain words

Today every settlement has a bare cream word floating over it ("Settlement 4"). It looks the same for your capital, a
starving hamlet, an enemy town under siege and a ruin you saw ten years ago. It tells you nothing except a name, and when you
zoom out the words pile on top of each other.

The plan replaces it with a **dome plate**: a small arch (a flattened half-circle, flat side down) that holds the owner's
emblem, standing on a straight **name ribbon** that carries the name and head count. The arch's rim does the talking: its
colour says friend or foe, its outline turns into stakes or battlements when the town is fortified, and a thin ring inside it
fills up as the town builds (or drains as siege engines batter its walls). A capital gets a crest on top. At most **one**
warning chip hangs under the ribbon when something needs you. How much you see depends on zoom and on how much the
settlement matters to you right now, so a crowded map stays readable.

**Recommendation in one line:** a wide, flattened dome (a half-ellipse 2.5 times wider than tall) for the emblem and
the meters, standing on a straight ribbon for the name. Don't squeeze the name into the curve. Draw it in code as one 2D
overlay with the existing HUD fonts, colours and icon drawer, fed by a tiny per-settlement fact record that the sim layer
computes and that is unit-tested like `BuildText` and `SiteText`. Ship in four phases. Phase 1 already gives the full dome
look.

---

## 1. What exists today

| Thing | Where | What it does |
|---|---|---|
| Label creation and update | `game/Scripts/Hud/Game.SettlementNames.cs` (`UpdateSettlementMapLabels`) | One `Label3D` per settlement: `UiTheme.DisplaySemi`, 48 px, outline 12, `UiTokens.Parchment` on a near-black outline, billboard, `FixedSize`, `NoDepthTest`, `RenderPriority 10`, 1.35 above the tile. |
| When it updates | `Game.cs` `ApplyNewFrame` → every new `MatchFrame`; also at start (`Game.cs:162`) | Rewrites text, position and scale for every settlement every frame; hides labels for settlements no longer in the frame. |
| Zoom scaling | `SettlementMapLabelScale`, called from `Game.Camera.cs` on zoom | Scale 0.75 at zoom ≤ 12 down to 0.34 at zoom 120 (`ZoomMin 2.7`, `ZoomMax 120`, presets 5/12/40/90). |
| Names | `_settlementNames` (Godot side, saved with quick-saves) | Player-given names; otherwise "Settlement N". Used in TileCard, People sheet, Diplomacy, Building tab. |
| Other users | `Game.Scenarios.cs` `core-gallery` | Hides all labels for clean captures. |

What is wrong with it:

1. **No ownership.** Own, allied, hostile, Free Folk and Tin Folk all look identical. The only cue is the banner mesh in the village.
2. **No state.** Nothing says "starving", "under siege", "raiders arrive next season", "occupied", "abandoned" or "last seen 6 years ago".
3. **No hierarchy.** A 300-person capital and a 12-person band get the same word at the same size.
4. **No overlap handling.** At zoom 40+ neighbouring names overprint each other.
5. **Not in the UI style.** Every other piece of text in the game sits on a `SurfaceMap*` box with Commissioner type, `UiDraw` glyphs and Okabe–Ito civ colours (doc 20, `ui-typography.md`). The map label is the one floating word with a heavy black outline.
6. **"Settlement 7" for every foreign town** reads as placeholder text.
7. Work done every frame for every label, including setting the same text again.

---

## 2. What the player wants to know at a glance

Ranked by how often a player scanning the map needs it:

1. **Whose is it?** Mine, a friend's, an enemy's, nobody's.
2. **Does it need me?** Under siege, raiders coming, starving, about to secede, occupied.
3. **How big / important is it?** Capital, city, town, village, band.
4. **Is it safe?** Walls, palisade, nothing.
5. **What is it doing?** Building what, and how far along.

Everything else (stores, cohesion numbers, faith shares, scouts, livestock, works counts, trade routes) is a "click to see"
question and already lives in the TileCard and the Your People sheet.

### 2.1 The fields we show, and why

| # | Field | Shown for | Why it earns its place | Data (exists?) |
|---|---|---|---|---|
| 1 | **Owner emblem disc** (civ colour + emblem, `UiDraw.Emblem`) | everyone | Answers "whose" without reading; emblem keeps it colour-blind safe (08 §4.3). | `SettlementCiv`, `FreeFolk`, `TinFolk` flags: **exists** |
| 2 | **Name** | everyone | It is the label. | Godot `_settlementNames`: **exists** |
| 3 | **Head count** | own always; foreign when in sight; remembered as "~N" | Size is the best single "how important" number and it is public in the frame. | `SettlementPopulation`: **exists** |
| 4 | **Capital crest** | capitals | The one settlement you must never lose; the sim already treats it specially (secession, overreach). | `CapitalOf(civ)` in sim, **not on the frame**: needs one new flag bit (§7.2) |
| 5 | **Size tier** shown by dome size and rim layers, not by a word | everyone | Lets a city read bigger than a hamlet with zero extra text. | Derived from population + structures, same rule as the renderer's `core_layout.gd stage_of` (§7.2) |
| 6 | **One alert chip** (the most urgent only) | own: all alerts; foreign: siege and occupation only | "Does it need me" in one glance; capping at one keeps plates small. | see §2.2: **all exist** |
| 7 | **Defence outline** on the dome rim (stakes / palisade / battlements / towers) | own; foreign when in sight | Answers "is it safe / can I take it" for war decisions. | `ThornBarrier`, `Palisade`, `Lookout` flags; walls/hillfort from `SettlementStructures`; Iron castle from `IronFrame.Works`: **exists** |
| 8 | **Ring meter** inside the dome: build progress (own) or wall integrity (besieged) | own; any besieged town you can see | Shows towns are busy and idle towns stand out; doubles as the siege gauge. | `SettlementBuildKind`, `SettlementBuildPermille`, `SettlementBuild`; `IronFrame.SiegeIntegrityPermille`: **exists** |
| 9 | **Relation colour on the rim** (own, at war, allied, pact/truce, none) | everyone | Turns the plates into a diplomatic map. | `CivFlags` `AtWar`/`Allied`/`Pact`/`InTruce`: **exists** |
| 10 | **"Last seen" treatment** (faded, dashed rim) | remembered settlements | Stops old intel looking current. | `Remembered` flag, `SettlementSeenSeason`: **exists** |

### 2.2 Alert chips, in priority order (only the first that applies is drawn)

| Priority | Chip | Colour | Trigger (frame data) | Own | Foreign |
|---|---|---|---|---|---|
| 1 | **Besieged** (tower glyph + integrity %) | Danger | `IronFrame.SiegeSettlement` contains it | yes | yes, if visible (you see the siege) |
| 2 | **Raid / Assault incoming · N seasons** | Danger | a `Party` with kind Raid/Assault and `PartyTo` = it, viewer's own settlement; seasons = `PartyArriveSeason − Season` | yes | no |
| 3 | **Occupied** | Danger (own) / Warning (foreign) | `Occupied` flag (+ `OccupationRequiredSeasons` for a countdown) | yes | yes |
| 4 | **Famine** / **Short** | Danger / Warning | `SettlementFood` band 3 / 2 | yes | no (private) |
| 5 | **Fraying** (secession risk) | Warning | `SettlementCohesion` band 3 (and not capital) | yes | no |
| 6 | **Hungry winter ahead** | Warning | `SettlementWinterForecast` ≥ Short | yes | no |
| 7 | **Fire out** | Warning | `FireLit` clear on an own, populated settlement (early eras only, Q6) | yes | no |
| 8 | **Splitting** (fission pending) | Info | `FissionPending` | yes | no |
| 9 | **Your levy target** | Accent | `LevyTarget` flag | n/a | yes |

Rule: chips 1 to 3 also pulse (§4.5). Chips 4 to 9 are static.

### 2.3 What we deliberately leave out

- **Food stores, goods, cohesion numbers, overreach points.** Numbers that need context belong in the sheet. The plate says "fine" by showing nothing.
- **Faith shares, scholars, soldiers, missionaries, province/fief status.** Interesting but not urgent; E5 can add one chip kind later if playtests ask (the chip slot is generic).
- **Research, Legacy, Dominion.** Civilization-level, already in the top bar.
- **Trade routes and caravans.** Already drawn on the map as routes.
- **More than one alert.** The TileCard lists all of them.
- **Animated numbers.** Head count changes once a season; animating it would just be noise.

---

## 3. The dome: is it a good idea?

### 3.1 Honest assessment

**Yes, with one condition: the name must not live inside the curve.** In its favour:

- **It's a signature shape.** Strategy games mostly use rectangles, pills or shields. A dome on a ribbon reads as a hall
  roof or hearth vault, which fits a game about settlements growing from hearths into cities. Nothing else in our HUD uses
  it, so a plate can never be mistaken for a button or a toast.
- **The arc can carry meaning.** A half circle has a rim that can be coloured, layered, segmented and turned into
  battlements. That lets the shape show state without extra text, which a rectangle can't.
- **It points.** A flat base with the round side up reads as "standing on something", which is right for a marker above a place.

The cost is **readability**. Names are wide and short (a 9-letter name at 18 px is about 95 px wide and 18 px tall). A true
semicircle is half as tall as it is wide, so fitting the name inside makes the dome about 110 px wide and 55 px tall. That is
mostly empty space, and the vertical room shrinks towards the edges, so the first and last letters would hit the curve.
Long names would make huge domes and short names tiny ones, so plates would vary in size by name rather than by importance.
Overlap gets worse because domes are taller than rectangles.

### 3.2 Options considered

| Option | What it looks like | Verdict |
|---|---|---|
| **A. True semicircle, name inside** | One big half disc, text on the chord | **No.** Tall, mostly empty; size follows name length; letters clip the curve. |
| **B. Wide flattened half-ellipse, name inside** | A low arch whose width fits the name | **No.** Better, but long names flatten it into a pancake and short names into a bump; the shape stops being recognisable and meters on the rim change length per name. |
| **C. Name curved along the arc** | Text bent around the rim like a coin | **No.** Pretty at one size, hard to read at 14–18 px and when skimming; Godot has no text-on-path, so it would be per-glyph placement. |
| **D. Dome above, straight name ribbon across the flat base** | A fixed-size arch (sized by tier) holding the emblem and the meter, standing on a ribbon as wide as the name needs | **Recommended.** The dome keeps a constant, recognisable shape that grows with **importance**, not name length; the name reads as plain horizontal text; the ribbon is the only part that stretches. |

### 3.3 The recommended proportions

- **Dome:** a half-ellipse **2.5 times wider than tall** (height = 0.4 × width; for example 46 × 18 px for a town). A true
  semicircle is 2 : 1 and makes the plate tall. 2.5 : 1 is about a fifth lower and still reads clearly as an arch.
- **Ribbon:** a straight band across the dome's flat base, **wider than the dome** by at least 8 px each side, and wider still
  when the name needs it. Ends are cut at 60°, like the corner of a hex tile (§5.1), not swallowtails, which are the generic
  fantasy-banner cliché.
- **Pointer:** a small 60° notch under the ribbon's centre whose tip marks the settlement.

---

## 4. Layout on the dome

### 4.1 Where every piece goes

| Element | Place | Notes |
|---|---|---|
| **Owner emblem** | centre of the dome, slightly above the base | `UiDraw.Emblem`, radius ≈ 0.42 × dome height; the dome is its frame |
| **Name** | centred on the ribbon | `DisplaySemi`/`Display` by tier, never curved, never clipped by the arc |
| **Head count** | right end of the ribbon after a hairline divider | `Num` 13–14, `TextSecondary`, fixed-width slot so the ribbon doesn't jitter |
| **Capital crest** | on the apex of the dome | a small raised notch with a star finial (`UiGlyph.Star` shape), in `Warning` ochre |
| **Defence outline** | the dome's outer rim silhouette | smooth = none; fine spikes = thorn barrier; pointed stakes = palisade; square battlements = stone walls/hillfort; battlements + two small towers at the base corners = castle |
| **Ring meter** | a thin arc just inside the rim, 8 segments from left base over the top to right base | own: build progress in civ colour; besieged: wall integrity in `Danger`, draining from right to left; empty track otherwise |
| **Alert chip** | hangs under the ribbon, centred | pill, 18 high; the pointer notch moves to the chip's underside when a chip is shown |
| **Relation colour** | the rim stroke (dome and ribbon edge) | own: civ colour; at war: `Danger`; allied: `Success`; pact/truce/contacted: `BorderStrong`; Free/Tin Folk: none |
| **Alert dot** (compact levels) | on the dome's right shoulder (about 45° from the apex) | 6 px disc in the chip's colour; pulses for priorities 1–3 |

### 4.2 Sketches, close zoom (≤ 16)

Recommended plate: **own capital, town tier, building at 5/8, food short**

```
                       ✦                        ← crest + star finial (capitals only)
                 _..--'^'--.._
             .-'  ▰ ▰ ▰ ▰ ▰ ▱ ▱ ▱ '-.           ← ring meter: build progress, own civ colour
          .'                       '.          ← rim stroke: own civ colour, light top bevel
         /           .---.           \
        |           ( ☀ )            |         ← owner emblem disc
   _____|____________'---'____________|_____
  ╱      Amberford          │   312          ╲  ← ribbon: name (Display 20) · hairline · head count
  ╲__________________________________________╱  ← 60° hex-cut ends
                  ⚠ Short of food                ← one alert chip (Warning)
                         ▾                      ← pointer tip = settlement anchor
```

**Own town, not capital, building, no alert**

```
              _.--'''--._
          .-'  ▰ ▰ ▰ ▱ ▱ ▱ ▱ ▱ '-.
         /         .---.         \
        |         ( ☀ )          |
   _____|__________'---'__________|_____
  ╱     Lowfield         │   88         ╲
  ╲_____________________________________╱
                    ▾
```

**Hostile (at war), stone walls, in sight**

```
          ┌┐ ┌┐ ┌┐ ┌┐ ┌┐               ← battlements on the rim = stone walls
         .┘└─┘└─┘└─┘└─┘└.
       .'                '.          ← rim stroke: Danger, 2 px
      |       ( ≋ )        |          ← their emblem
   ___|___________________|___
  ╱  Settlement 9   │  140     ╲       ← ribbon edge also Danger
  ╲__________________________╱
               ▾
```

**Besieged (yours or theirs, any owner you can see)**

```
          ┌┐ ┌┐ ┌┐ ┌┐ ┌┐
         .┘└─┘└─┘└─┘└─┘└.
       .' ▰ ▰ ▰ ▰ ▰ ▱ ▱ ▱ '.        ← ring meter now = wall integrity, Danger, draining
      |       ( ≋ )        |        ← whole rim pulses slowly
   ___|___________________|___
  ╱  Settlement 9   │  140     ╲
  ╲__________________________╱
       ◼ Besieged · walls 62%        ← chip, Danger, pulsing
               ▾
```

**Foreign, remembered (out of sight)**

```
            _ . - - - . _
         .'               '.        ← dashed rim, whole plate at 60% opacity
        |      ( ✿ )       |        ← emblem desaturated
   _____|_________________|_____
  ╱  Settlement 4   │  ~60       ╲   ← "~" count; hover adds "Last seen 3 years ago"
  ╲____________________________╱
                ▾
```

### 4.3 Sketches, region zoom (16 to 55): compact

The dome shrinks to a small half-disc that holds only the emblem; the ribbon keeps the name. No meter, no defence outline.

```
     .-.               .-.•              .┴┴┐•
    ( ☀ )✦            ( ☀ )            ( ≋ )
  ╱ Amberford ╲      ╱ Lowfield 88 ╲    ╱ Settlement 9 ╲
  capital (crest)    own, warning dot   at war, walls hint (two teeth), danger dot pulsing
```

Own settlements keep the head count; foreign ones drop it. The selected, hovered, own capital and any priority 1–3 alert get
the full close-zoom plate instead.

### 4.4 Sketches, continent zoom (> 55): pips

```
     ✦                                          •
    ◖☀◗ Amberford      ◖☀◗      ◖☀◗      ◖≋◗ Settlement 9
    capital, named     own pips (no text)       besieged: named + pulsing dot
```

A pip is a tiny half-disc (radius 7) holding the emblem, flat side **on** the anchor point. It has no ribbon unless the
settlement is a capital, a city, selected, hovered or has a priority 1–3 alert, and then the name sits beside it as plain
outlined text.

### 4.5 Motion

- **Appear / level change:** cross-fade 0.18 s (`UiTokens.BaseS`); the dome grows up from the ribbon (scale-y 0.6 → 1) so the shape change feels deliberate.
- **Ring meter:** segments light one at a time over `SlowS` when progress crosses a segment; a finished build flashes the whole ring `Success` once, then it empties for the next job.
- **Alert chip appears:** slides down 4 px and fades in over `SlowS`; one ring pulse travels up the rim from both base corners to the apex.
- **Pulsing alerts (priorities 1–3):** the rim's alpha breathes 0.55 ↔ 1.0 over 1.6 s. Off under `UiSettings.ReduceMotion`.
- **Capture / ownership change:** the emblem flips (scale-x 1 → 0 → 1, 0.26 s), and the rim recolours from the base up.
- **New defences finished:** the rim outline morphs (stakes rise from the rim over 0.26 s).
- Nothing else moves. A calm map should look still.

---

## 5. Making it look finished, not generic

### 5.1 Construction of the dome, back to front

1. **Drop shadow:** the same outline offset 3 px down at 0.35 alpha, plus a second at 6 px and 0.15 (a cheap two-step soft shadow). This lifts the plate off bright sand and snow.
2. **Keyline:** 1 px very dark outline (`#0D0604` at 0.6) around dome and ribbon, which separates the plate from dark forest and water.
3. **Fill:** a vertical gradient from `Surface3` (#37231A) at the apex to `Surface1` (#21130E) at the base, at 0.92 opacity. It is subtle (about 6% lightness), so it reads as a lit vault, not a glossy button.
4. **Owner wash:** the owner's civ colour at 14% over the fill, strongest at the apex and fading to 0 at the base. Own and foreign settlements read differently even before you see the emblem.
5. **Ring meter track:** 3 px arc 4 px inside the rim, `SurfaceSunken`, split into 8 segments with 2° gaps; lit segments in the meter colour.
6. **Rim:** 1.5 px stroke in the relation colour (2 px for at war or selected), with its silhouette shaped by defence (§4.1).
7. **Top bevel:** a 1 px `Parchment` hairline at 0.14 alpha along the upper third of the arc only. The diorama sun is high, and this ties the plate to the scene's light.
8. **Crest** (capitals): a raised 60° notch at the apex topped by a 10 px star in `Warning` ochre, with a dark keyline.
9. **Ribbon:** in front of the dome base, `SurfaceMapLabelStrong` (darker than the dome so it reads as a band across it), with a 1 px parchment hairline along its top edge at 0.10 and its ends cut at 60° like a hex corner.

### 5.2 How the shape changes with tier

| Tier | Dome width (close) | Rim | Extra |
|---|---|---|---|
| Band | 30 px, no meter | single thin rim | ribbon has square ends (a band has no "hall" yet) |
| Village | 38 px | single rim | hex-cut ribbon ends |
| Town | 46 px | single rim + ring meter | — |
| City | 54 px | **double rim** (two strokes 2 px apart) | name in `Display` |
| Capital (any tier) | one step bigger than its tier | as its tier | crest + star; `SurfaceMapLabelStrong` fill |
| Free Folk / Tin Folk | 34 px | no relation colour, `Border` only | grey emblem (leaf; tin mark later) |
| Empty / abandoned | 30 px | **hollow** (rim only, no fill), dotted | "(abandoned)" in `TextTertiary`; hidden past region zoom |
| Remembered | as its tier | **dashed** | whole plate at 0.6; "~N" count |
| Occupied | as its tier | rim in the owner's colour, inner rim in the occupier's | occupier pip on the emblem's lower right |

### 5.3 Matching the house style, and where it deliberately departs

Matches: every colour is a `UiTokens` token, all type is Commissioner through `UiTheme`, emblems and glyphs come from
`UiDraw`, the chip is the HUD badge pill, motion uses the HUD timings, and `ReduceMotion` is respected.

Deliberate departures (map-only, never used in panels):
- **Curved silhouette.** HUD panels are rounded rectangles; the dome is the map's own marker shape.
- **Gradient fill and top bevel.** HUD surfaces are flat; the plate sits in a lit 3D scene and a flat fill looks pasted on.
- **Hex-cut ends.** A small nod to the hex grid the map hides (08 §1); appears nowhere else in the UI.

---

## 6. Interaction, depth and overlap

### 6.1 Interaction

- **Hover a plate** = hover the settlement tile: the existing TileCard appears and the plate goes to full detail. The plate hit test feeds the current `_hoverTile` logic; no new input routing.
- **Click a plate** = pin that tile (`_pinnedTile` and the pin marker). No new commands.
- **Double-click** (phase 3): frame the camera on it.
- Hover-only second line under the ribbon (full plates only): own: "Building Granary · about 2 seasons"; foreign: civ name and relation ("Wave Folk · at war"); remembered: "Last seen 3 years ago".
- The overlay uses `MouseFilter.Ignore` and does its own hit test, so plates never swallow map drags. The hit shape is the dome **and** the ribbon, not their bounding box, so clicks in the empty corners above the ribbon reach the map.

### 6.2 Anchoring

- The **pointer tip** under the ribbon (or under the chip) is the anchor. It sits at the projected position of the hearth tile top plus a height that grows with tier (band 0.9, village 1.2, town 1.5, city 1.9 world units), so the plate floats above the tallest building of its tier and doesn't cover the village.
- So the flat edge does **not** sit on the settlement point at close and region zoom; it floats just above it, and the pointer bridges the gap. At continent zoom the pip's flat edge sits right on the point, because there is nothing left to cover.
- Plates are always drawn over the 3D world but under every HUD panel, toast and sheet. Hide when the anchor is behind the camera or more than 40 px outside the viewport.
- At zoom ≤ 5 (Hearth view) non-selected plates fade to 0.7 and rise another 0.4 units, so figures stay visible.

### 6.3 Level of detail

Driven by camera zoom (`_zoom`, 2.7 to 120) **and** relevance, with ±10% hysteresis (08 §1) so plates don't flicker at a boundary.

| Zoom band | Range | Default | Promoted to full plate when |
|---|---|---|---|
| **Close** (Z0–Z1) | ≤ 16 | Full dome plate for every visible settlement | — |
| **Region** (Z2) | 16 to 55 | Compact: small emblem dome + name ribbon (+ count for own); alert as a shoulder dot | selected, hovered, own capital, or an alert of priority 1–3 |
| **Continent** (Z3–Z4) | > 55 | Pip only | named when capital, city, selected, hovered or alert 1–3; full plate only for selected or hovered |

**Relevance score** (higher wins space and survives overlap): selected 100 · hovered 90 · own with alert 1–3: 80 · own capital 70 · foreign with siege/occupation/levy target 60 · own other 50 · at war 40 · allied/contacted 30 · Free/Tin Folk 20 · remembered 10 · empty 0. Ties go to the larger population, then the lower settlement index (stable).

### 6.4 Overlap

The dome makes plates taller, so overlap needs one more step than before:

1. Project, then sort by relevance.
2. Each plate's footprint is **two boxes**, the dome box and the ribbon box, not one bounding box. A neighbour's ribbon may then tuck under another plate's empty dome shoulders, which packs about a quarter tighter than one box.
3. If it collides with a placed plate: try nudging up by one ribbon height (once).
4. Else **drop the dome first** (full → compact). The compact dome is about a third the height, so this usually frees the space.
5. Else drop to a pip with no text. Pips never hide; hovering a demoted pip shows its full plate.
6. Placement uses a coarse 64 px screen grid, so checks stay close to linear in the number of plates.

---

## 7. Technical plan (no code yet)

### 7.1 Shape of the solution

```
 Sim thread                         Godot main thread
 ─────────────                      ──────────────────────────────────────────────────────────
 FrameBuilder ──► MatchFrame ──►    ApplyNewFrame
                  IronFrame          └─ SettlementPlates.Refresh(frame, iron)      (once per new frame)
                                        ├─ for each row: PlateFacts f = SettlementPlate.Facts(frame, iron, row)
                                        └─ if f != cached[settlement]: cache, mark dirty
                                    _Process
                                     └─ SettlementPlates.Layout()   (only if camera moved, zoom changed, or dirty)
                                         ├─ project anchors (Camera3D.UnprojectPosition)
                                         ├─ pick level per plate, resolve overlap by relevance
                                         └─ QueueRedraw()
                                    UiSettlementPlates._Draw   (cached dome geometry, UiDraw emblems, Commissioner text)
```

- **Facts in the sim assembly, drawing in Godot.** `PlateFacts` is a small `readonly record struct` of ints, bytes and enums only (no strings, no floats): civ, population, tier, isCapital, isOwn, relation, defence level, meter kind and permille, alert kind and number, remembered, seen season, empty, occupier. It lives in `src/CivSim.Sim/View/SettlementPlate.cs` next to `BuildText.cs` and `SiteText.cs`, as a pure function of the frame. That obeys rule 1 (no engine references) and D1–D10, and gets fast headless tests.
- **Strings, shapes and layout stay in Godot.** The name comes from `SettlementLabel()`; chip words from `UiText`.
- **Equality-driven updates.** `PlateFacts` is a record struct, so "did anything change" is one `==`.
- **One overlay, not N nodes.** One `Control` on a new `CanvasLayer` (layer 5, above the world, below `_scrimLayer` 9 and `_hudLayer` 10) draws every plate. This replaces the per-settlement `Label3D`s.

### 7.2 New data the sim must expose

| Need | Today | Change | Cost |
|---|---|---|---|
| **Capital** | `LivingEconomyModel.CapitalOf(civ)` / `Capital(civ)` exists; not in the frame | Append `SettlementViewFlags.Capital = 8192`; set it in `FrameBuilder` with the other flags (own, or visible) | trivial; view-only (to confirm in phase 0 that frames feed no checksum or golden) |
| **Size tier** | renderer derives band/village/town/city in `core_layout.gd stage_of`; sim `SettlementTier` only exists for post-KEY-3 civs | `SettlementPlate.Tier(...)` mirrors `stage_of` (permanent dwellings, population 20/60/90, temple or ziggurat), with a test and cross-reference comments | small; drift risk (Q3) |
| Everything else | in `MatchFrame` / `IronFrame` | none | — |

No new `Command`s. Nothing on the Godot side writes sim state.

### 7.3 How to draw the dome: three ways compared

| Approach | How | Pros | Cons |
|---|---|---|---|
| **1. Procedural CanvasItem drawing** (recommended) | Build the dome outline as a point list (half-ellipse, 24–48 points by size, plus stakes or battlements spliced into the rim), the ribbon as a 6-point polygon. Fill with `DrawPolygon`/`DrawColoredPolygon` (per-vertex colours give the vertical gradient and owner wash for free); stroke rims and the bevel with `DrawPolyline(..., antialiased: true)`; meter segments with `DrawArc(..., antialiased: true)`; emblem with `UiDraw.Emblem`; text with `DrawString`. | Same technique as every icon in `UiIcon.cs` (111 draw calls of this kind already); any size and UI scale; every variant (tier × defence × relation × meter) is just parameters; easy to tweak. | Godot fills polygons **without** anti-aliasing; the fix is the anti-aliased keyline stroke drawn over the fill edge, which hides the stair-steps (standard practice, to be checked in the spike at UI scale 1.0 and 1.3). |
| 2. Pre-rendered textures / 9-slice | Dome textures per tier (tinted by `Modulate`); ribbon as a 9-slice that stretches the middle and keeps the hex-cut ends | Perfect edges, cheapest to draw | A 9-slice can't stretch an arc, so every dome size and every rim variant (stakes, battlements, dashed, double) is another texture at 1x and 2x; the meter still needs code; colours by tint only, so no per-part gradient. Too many assets to keep in step. Possible fallback **for the ribbon only**. |
| 3. Shader (signed-distance half-ellipse on a quad) | One `canvas_item` shader draws dome, rim, stakes, meter by angle and glow | Best anti-aliasing and glow at any size | The UI has no canvas shaders today (only the `grade` post shader); per-plate parameters need per-plate materials or packing into vertex data; text and emblem still drawn separately; harder to read and test. Keep it as the plan B if the spike shows polygon edges look rough. |

**Recommendation:** approach 1. Cache each dome outline by key (tier, level, defence, UI scale) and redraw it with a
translate (`DrawSetTransform`), so a frame builds no geometry unless a key appears for the first time.

### 7.4 Fitting text to the shape

- Text never goes inside the curve, so no clipping against the arc is needed. The name sits on the ribbon, which is a rectangle with angled ends.
- Ribbon width = `max(dome width + 16, name width + head-count slot + 2 × 12 padding)`. The dome always stands fully on the ribbon, and the ribbon never shrinks below it.
- Names are capped at 24 characters already. Own names are never cut; foreign names truncate with an ellipsis at the compact level only.
- The emblem is sized to the largest circle that fits the dome comfortably: radius = `min(0.42 × dome height, 0.3 × dome width)`, centred at 45% of the dome height above the base.
- Text widths (`Font.GetStringSize`) are cached per (name, font, size) and remeasured only when the name or UI scale changes.

### 7.5 Files

| File | Change |
|---|---|
| `src/CivSim.Sim/View/SettlementPlate.cs` | **new**: `PlateFacts`, `PlateAlert` (ordered by priority), `PlateDefence`, `PlateMeter`, `Facts(...)`, `Tier(...)`, `Relevance(...)`. |
| `src/CivSim.Sim/View/ViewTypes.cs` | append `Capital` to `SettlementViewFlags` (append only). |
| `src/CivSim.Sim/View/FrameBuilder.cs` | set the `Capital` bit. |
| `src/CivSim.Tests/SettlementPlateTests.cs` | **new**: alert priority, privacy (no famine chip on a foreign town), remembered, empty, tier thresholds, meter switches to integrity under siege, relevance ordering. |
| `game/Scripts/Ui/UiDome.cs` | **new**: dome and ribbon geometry (outline builders for tiers and defences, cache), so the overlay stays readable. |
| `game/Scripts/Ui/UiSettlementPlates.cs` | **new**: the overlay `Control` (layout, levels, overlap, `_Draw`, hit test, tweens). |
| `game/Scripts/Hud/Game.SettlementNames.cs` | keep names, rename, save/restore; drop `Label3D` creation; drive the overlay; default-name generator (phase 2). |
| `game/Scripts/Ui/UiIcon.cs` | append glyphs: `Tower` (siege chip), `Raid` (crossed spears); append only. |
| `game/Scripts/Ui/UiText.cs` | chip words and hover lines. |
| `game/Scripts/Ui/UiTokens.cs` | plate constants: dome widths by tier, 2.5 : 1 aspect, level thresholds, pulse period. |
| `game/Scripts/Hud/Game.Camera.cs` | replace `UpdateSettlementMapLabelScale()` with "camera changed → relayout". |
| `game/Scripts/Hud/Game.TileCard.cs` | hover hit test consults the overlay first. |
| `game/Scripts/Hud/Game.Scenarios.cs` | `core-gallery` hides the overlay layer; new `plates` and `plate-gallery` scenarios (§7.7). |

### 7.6 Performance

- A full dome plate is about 60–120 vertices plus a few text runs. At 200 plates that is around 20k vertices in one `_Draw`, which is small for Godot's canvas renderer.
- Geometry is cached by key; per plate per redraw it's a transform and the draw calls.
- Layout runs only when the camera moves, the zoom changes or facts change. When the camera is still, only plates with a pulsing alert redraw.
- Facts rebuild once per new frame (integer reads), compared with `==`.
- Add the plate layout and draw to the `--benchmark` HUD bucket; budget 0.5 ms at 200 plates.

### 7.7 Testing and visual checks

- **Unit (headless, CI):** `SettlementPlateTests` on hand-built frames, like `BuildTextTests`. `SourceRulesTests` scans the new sim file for D1–D10.
- **Determinism:** `dotnet test` green; toy, research and politics goldens unchanged.
- **`plate-gallery` scenario (new, dev only):** draws every variant on a plain backdrop in a grid (tier × relation × defence × meter × alert × remembered/empty), the way `buildings-gallery` and `fief-gallery` show meshes. It's the main review sheet for the dome and the place to judge edge quality at UI scale 1.0 and 1.3.
- **`plates` scenario (new):** the live map at zoom presets 5, 12, 40 and 90: own capital and towns, a siege (reusing the `defense-castle` setup), a remembered and an empty site, and a dense cluster for overlap; at 1280×720, 1600×900 and 2560×1080.
- **Terrain check:** the same plate over sand, snow, grass, forest and water; text contrast stays ≥ 4.5:1 against the ribbon.
- **Benchmark:** `--benchmark --perf-debug` before and after; no regression in window-max HUD ms.
- Screens go to `docs/MVP/ui-screens/plates/` with before/after pairs.

---

## 8. Default names (small, high-value polish)

"Settlement 9" on every foreign town is the most unfinished-looking thing on the map. Proposal: a **cosmetic name
generator** on the Godot side, with a short syllable list per civilization (eight sets matching the emblems: sun, wave, leaf,
star, mountain, flame, moon, antler), picked by a hash of (civ, settlement index). It never reaches the sim, so determinism
and replays are untouched. Player-chosen names still win, and saves keep working because only custom names are saved. The
TileCard, People sheet and Diplomacy drawer already go through `SettlementLabel()`.

---

## 9. Phases

Each phase can ship on its own. The dome changes the first slice: the **shape and its tier and relation variants** move into
phase 1, so the first thing players see already has the signature look. The rim's **state** features (defence outline,
meter, chips) stay in phase 2.

| Phase | Delivers | Done when |
|---|---|---|
| **0. Spike (one day)** | Overlay `Control` on its own layer; `UiDome` drawing one dome + ribbon with fill, keyline and anti-aliased rim; today's names on it at projected positions. Compare polygon edges against a quick shader version at UI scale 1.0 and 1.3. Confirm `MatchFrame` feeds no checksum. | Side-by-side captures pick approach 1 or 3. |
| **1. The dome plate (first slice)** | `PlateFacts` + tests; Capital flag; full dome plate with gradient, owner wash, bevel, tier sizes, double rim for cities, capital crest, relation rim colours, remembered (dashed) and empty (hollow) styles, head count; three zoom levels with hysteresis; overlap with the two-box footprint; hover and click routed to the TileCard and pin; `plate-gallery` scenario. | Gallery and `plates` scenario at four zooms look finished; tests and goldens green; benchmark flat. |
| **2. State on the rim** | Alert chip (priorities 1–9), pulsing rim for 1–3, shoulder dot at compact level; defence outline (thorn, palisade, battlements, towers); ring meter for build progress and siege integrity; hover second line; default names. | A siege, a raid, a famine, a walled enemy and an idle town each show clearly in a screenshot without opening a panel. |
| **3. Polish** | Motion (dome grow, meter segment fill, rim pulse travel, emblem flip, stake rise), double-click to frame, colour-blind preset check, Tin Folk mark, an E5 chip kind only if playtests ask. | Reviewed in a playtest; no clutter complaints at region zoom. |

---

## 10. Open questions and risks (each with a default you can accept)

| # | Question / risk | Recommended default |
|---|---|---|
| Q1 | 2D overlay or 3D (`Label3D`/`Sprite3D`)? | **2D overlay.** Crisp HUD fonts and icons, and overlap is a screen-space problem. |
| Q2 | Show head count on foreign settlements? | **Yes when in sight, "~N" when remembered.** It's already public in the frame and the Diplomacy drawer. |
| Q3 | Tier rule: mirror `stage_of` in C# or add a tier byte to the frame? | **Mirror it in `SettlementPlate.Tier` with a test.** Move it into the sim only if it drifts. |
| Q4 | One alert chip or several? | **One.** The TileCard lists all. |
| Q5 | Alerts on foreign towns? | **Only public ones:** siege, occupation, your levy target. Famine and cohesion are private. |
| Q6 | "Fire out" chip after the early eras? | **Show before KEY-3 only.** |
| Q7 | Default cosmetic names? | **Yes, phase 2**, Godot side; player names always win. |
| Q8 | Player control over plates? | **One setting** in `UiSettings` ("Settlement plates: Full / Names only / Off", default Full) plus hold Alt to show every plate in full. "Names only" draws ribbons without domes. |
| Q9 | Plates hiding figures at Hearth zoom? | Fade non-selected plates to 0.7 and lift them at zoom ≤ 5. |
| Q10 | Alerts crying wolf (a chip on half the towns every winter)? | Chips 6 and 8 only at close zoom and on hover; only 1–5 at region zoom. Check in the phase 2 screenshots. |
| Q11 | Performance on a large late map? | Layout only on change; cached dome geometry; grid-bucketed overlap; 0.5 ms at 200 plates. |
| Q12 | New flag bit touches something hashed? | Phase 0 checks; if frames are hashed, carry the capital as a separate `int[] CivCapital` row instead. |
| Q13 | E5 data on the plate? | **Not now.** The chip slot is generic. |
| Q14 | True semicircle or flattened dome? | **Flattened, 2.5 : 1 (width : height).** Still clearly a dome, about a fifth lower than a true semicircle (2 : 1). |
| Q15 | Name inside the dome or on a ribbon? | **Ribbon across the flat base.** The dome keeps a fixed shape by tier; only the ribbon stretches for long names. |
| Q16 | Ribbon ends: swallowtail, square or angled? | **60° hex-cut** (square for Bands). Swallowtails are the generic fantasy-banner look. |
| Q17 | Polygon fills have no anti-aliasing in Godot 2D: will edges look rough? | Cover every fill edge with an anti-aliased 1 px keyline (the usual fix); the phase 0 spike compares it with a shader at both UI scales. Switch to the shader only if the captures show stair-steps. |
| Q18 | Taller plates crowd the region view? | The compact level shrinks the dome to a small emblem half-disc; overlap drops the dome before anything else; the two-box footprint packs tighter. |
| Q19 | Defence outline (stakes, battlements) too fussy at small sizes? | **Close zoom and full plates only**; the compact level shows at most two small teeth for "walled"; pips show none. |
| Q20 | The ring meter meaning two things (build progress, wall integrity)? | Integrity replaces progress only while besieged, always in `Danger` and with the siege chip, so they can't be confused. |
| Q21 | Should the dome tilt with camera yaw or perspective? | **No.** It stays upright in screen space, like all UI. |
