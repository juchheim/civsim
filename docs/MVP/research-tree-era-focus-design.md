# Research Tree: one age at a time ("era lens")

Status: design, not implemented. Scope: Godot UI only (`game/`). It never writes sim state, and the determinism rules do not apply, but the pure layout math stays testable from `CivSim.Tests`.

## 1. Current state

**Layout** (`game/Scripts/Ui/ResearchRadialLayout.cs`, pure C#, linked into tests at `src/CivSim.Tests/CivSim.Tests.csproj:22`, tested by `ResearchRadialLayoutTests.cs`):
- Concentric rings, one per age, with Dawn innermost. Three track sectors (Craft, Kin, Reach), and inside each sector one wedge per field (33 fields), so a field's projects line up along one ray through every ring.
- Inside a ring, a project's *lane* is the length of its longest chain of same-age prerequisites (`Depth`, L231). Constants (L17): `CenterRadius 230`, `LaneSpacing 44`, `RingGap 52`. Radii are cumulative (L116-125). Turning points sit in the gap after their age (L210-218). Edges are precomputed cartesian polylines (`Curve`, L362).
- The angular slot is global: `SlotAngle = 2π / totalSlots` (L149-151). Today totalSlots = 78, so the slot is 0.0806 rad (4.6°).

**Content today** (counted from `content/catalog`):

| Age | Placed projects | Lanes | Ring radius (world) |
|---|---|---|---|
| Dawn | 34 | 5 | 230-450 |
| Hunters | 59 | 5 | 502-722 |
| Roots | 52 | 7 | 774-1082 |
| Bronze | 65 | 6 | 1134-1398 |
| Iron | 56 | 5 | 1450-1670 |

That is 266 numeric projects plus 5 numeric turning points (KEY-1..5). `OuterRadius` is 1722, so the disc is about 3,450 world units across. About 490 edges: 272 within one age, 198 to the next age, 16 that skip an age. Ages 5-9 are drafted (243 projects, KEY-6..10), with expected lanes 3, 5, 4, 7 and 7. Once they become numeric the outer radius grows to about 3,100 (6,200 across).

**Presentation** (`game/Scripts/Ui/ResearchRadialMap.cs`, 1,677 lines, one `Control` that does everything in `_Draw`):
- Camera: `_center` and `_zoom`, with `ToScreen`/`ToWorld` (L409-410). `MinZoom` = viewport / (2·(DrawnOuter+60)), clamped to 0.12..0.6 (L420). `MaxZoom` is 3.
- Draw order (L834): `DrawRings`, `DrawEdges`, `DrawNodes`, `DrawLabels`, ping, breadth counter, track names, turning-point pointer. Node radius is 8·zoom, clamped to 3..12 px (L832). Labels are placed with collision avoidance in `PlaceLabels` (L1391) whenever the zoom changes.
- Input (`_GuiInput`, L616): wheel and pinch zoom at the cursor, two-finger scroll zooms (L680, following doc 20 §4.1), left/middle drag pans, click selects, double-click starts. `HitTest` is a linear scan (L698). Arrow keys use `Neighbour` (L722).
- `_focusEra` (L51) is the player's *current age*, set in `SetLooks` (L227). `_drawEra` is the furthest age drawn (current+1 or the furthest visible).
- Host: `Game.ResearchWeb.View.cs` `RefreshMapView` (L491) builds a `RadialNodeLook[]` every refresh. `JumpToProject` (L619) and `HandleWebKey` (L1422) handle navigation. The panel is at most 1,597 px wide (`LayoutResearchWeb`, `Game.ResearchWeb.cs:500`) and the map shares it with the inspector.
- `UiResearchSlot.cs` is the drag-and-drop slot card. It is unrelated to the tree and needs no change.

**Why it is cumbersome.** The rings share one angle, so density depends on radius:
- At Dawn lane 0 (r≈252), neighbouring slots are about 20 world units apart. The player needs zoom 1.5 or more to separate nodes.
- At Iron's outer lane they are about 125 apart, so the outer ring is sparse.
- Each new age adds about 300 to the radius. Fitting the whole disc in a ~750 px tall map already needs zoom ≈ 0.21. At that zoom nodes hit the 3 px floor and almost no labels place. With 10 ages it hits the 0.12 clamp.
- Adding fields also shrinks `SlotAngle`, which makes Dawn even tighter.
- No zoom shows any age comfortably without the older ages crowding the centre and later ages blowing up the extent.

## 2. Interaction model

**Options considered**
- **A. Era lens (radial accordion). Recommended.** Keep the rings, sectors and field rays. The focused age is always drawn at full size at the *same fixed radius*. Every other age shrinks to a thin band: older ages pack inside it around the hub, later ages wrap outside it. Moving focus slides the bands through the "lens" position.
- **B. Unrolled era pages.** Replace the radial with horizontal strips, one per age, with snap-scrolling and narrow collapsed columns on each side. Rejected: it throws away the radial redesign that was just finished (commits 3874506, 6098d88), its tests, and the field-ray continuity that makes cross-age edges readable. One age still does not fit a screen anyway (Bronze would be about 78 slots × 44 = 3,400 px).
- **C. Semantic zoom to summary chips.** Unfocused ages become a single chip each (name, known/total, key unlocks). Rejected as the map itself, because cross-age prerequisite lines lose an endpoint and the path to a turning point disappears. The chip idea is kept as the **era rail** (§3).

**Why A wins:**
- Every age gets the same density, because the lens radius is chosen for it.
- The total size is bounded no matter how many ages are added.
- Cross-age edges still have two real endpoints.
- It is a pure 1-D radial remap, so the existing geometry, edge curves, tests and label code survive.

**The lens.** It is a monotone, piecewise-linear map `R_f(rBase)` from layout radius to drawn radius. The parameter `f` is a continuous focus over ring indices (index, not age id, so a missing age never leaves a hole).
- Per ring i: weight `w_i = smoothstep(clamp(1 − |i − f|, 0, 1))`.
- Lane spacing is `lerp(7, 44, w_i)`, so collapsed lanes are 0.16 of full size.
- The gap after ring i is `lerp(14, 52, max(w_i, w_{i+1}))`.
- The focused ring's inner edge is pinned at `FocusInner = max(760, 62 / SlotAngle)`. That gives at least 61 units between lane-0 neighbours, about what Roots has today, and it stays the same if fields are added.
- The hub radius is `FocusInner − Σ_{k<i0} block_k − t·block_{i0}`, where `i0 = ⌊f⌋` and `t = f − i0`, floored at `HubMin = 200`.
- The map is linear inside each ring and each gap, and maps `[0, CenterRadius]` to `[0, hub]`.
- Nodes, gates (they sit in gaps), ring outlines and every edge point go through `R_f` at their base angle, so "every edge points outward" still holds.
- Bound with 10 ages: 760 + 7·44 + 9·(≈6·7+14) ≈ 1,570. Today the disc is 1,722 and it would grow to ~3,100.

**A compressed age** is a band 35-50 world units thick (about 25-45 px at normal zoom), with sector tints at alpha 0.12. It keeps:
- One dot per known project (2.5 px), coloured by state: completed is theme fill, available is a theme ring, slotted or queued is accent, glimpsed is faint. Undiscovered fog dots are hidden.
- The age name drawn in the 12 o'clock label wedge with progress ("BRONZE · 12/65"), using the existing `LabelGap` wedge.
- Turning points always at full `PaintNode` size (they already have a 10 px minimum).
- Labels for "lifted" nodes only.

**Lifted nodes** are projects in a collapsed age that matter right now: the selected or hovered project, `_onPath`/`_upstream` of the selection, `_fogRequired`, search matches, slotted or queued projects, and gates. They draw as `PaintNode` at 0.75 of full size with their label. That keeps a prerequisite chain readable across ages.

**Cross-age edges:**
- Both ends in the focused age: drawn as today.
- One end collapsed: drawn to the dot at 0.6 of the quiet alpha, with a 3 px stub cap at the collapsed end.
- Both ends collapsed: hidden unless the edge is "strong" (pass 1 in `DrawEdges`, L1020).
- Strong (highlighted path) edges always draw in full, so "what do I need for Iron?" still shows its line into Bronze and Roots.

**Hover and click on a collapsed age:**
- A lifted node hit-tests normally, with a reach of at least 13 px. Clicking it selects it *and* focuses its age.
- Anywhere else on the band, hit-testing returns a band hit: the band brightens, the tooltip reads "Bronze age · 12 of 65 known · click to open it", and a click focuses that age. Individual dots are never click targets, because they are too small.

## 3. Fluid navigation

- **Era rail.** A new `ResearchEraRail` control docked to the map's left edge, 56 px wide, vertically centred between the search pill (top) and the key (bottom). Ages read bottom (Dawn) to top, matching inside-to-outside. Each chip has a 3-letter name, a mini progress arc (known/total), a current-age accent dot, and a lock for the next unreached age. A thumb shows the continuous `f`. Clicking a chip focuses that age. Dragging the thumb scrubs `f` continuously (rail pixels map linearly to `f`). `Usable()` (L399) subtracts the rail width.
- **Drag** on the map still pans (L661). Panning never changes the focus.
- **Wheel and pinch** still zoom (unchanged, doc 20 §4.1). **Shift+wheel** steps one age per notch. A **horizontal two-finger swipe** (`InputEventPanGesture.Delta.X`, unused today) scrubs `f` continuously (0.01 per delta unit) and snaps 150 ms after the last event.
- **Keys** (in `HandleWebKey`): PageUp/`]` go to the next (outer) age and PageDown/`[` go to the previous one. Arrow keys work in drawn coordinates. If the nearest neighbour is in another age, the selection moves and that age focuses. Home ("Fit") frames the whole lensed disc. Add a "PgUp/PgDn change age" hint to the footer note (`Game.ResearchWeb.View.cs:1410`).
- **Tween.** `_lensF` approaches `_lensTarget` by exponential approach with τ = 0.11 s, which settles in about 300 ms. This is the same idiom as the camera (`_Process` L579). `UiSettings.Current.ReduceMotion` snaps instantly.
- **What interpolates during the tween:**
  - Every age's lane spacing and gap, and the hub radius, which moves all positions and edges.
  - Node radius, lerped from the 2.5 px dot to `NodeRadius` by `w`.
  - Sector tint alpha, lerped from 0.12 to today's 0.30/0.42.
  - Label alpha: an age's labels appear only once `w ≥ 0.75`, fading over 0.75 to 1.
- **Camera during a focus change.** The focused ring is always at the same radius, so the camera stays on it. Keep the camera's angle and zoom, and ease its radius to the focused ring's mid-radius. While scrubbing, the camera stays put and the content morphs under it like a carousel.
- **Mid-drag partial state.** At f = 2.4, Roots has `w ≈ 0.65` and Bronze `w ≈ 0.35`. Both are half-open, labels are off for both (and on again once one passes 0.75), and the geometry is continuous with no jumps.
- **Snapping.** On release, the target is `round(f + v·0.15 s)`, clamped to ±1 age from the age where the drag began, so a fling never skips two ages. Click and keys set integer targets directly.
- **Range.** `f` runs from 0 to the furthest drawn ring (`_drawEra`, the current age + 1). The next age shows as a preview with fog dots. Draft ages have no ring and are not on the rail.
- **Auto-focus:**
  - On open, focus the age of the project the tree opened for (`OpenResearchWeb(…, project)`), else the selected project's age, else the current age. This applies immediately, with no tween.
  - `FocusProject` and `JumpToProject`: set the lens target to the project's age first, then compute the camera target with the *target* lens (the lens is pure, so `R_target` is cheap).
  - When an age opens while the tree is up (`SetLooks` sees `focusEra` increase): after a 0.6 s beat, tween to the new age and `Ping` the completed turning point. Do this only if the player was viewing the old current age.

## 4. Implementation plan

**New: `game/Scripts/Ui/ResearchEraLens.cs`** (pure, no Godot types; about 200 LOC). Link it into the test project the same way the layout is linked (csproj L22).
- `ResearchEraLens(ResearchRadialLayout layout)`: converts each edge polyline to polar `(angle[], baseRadius[])` once.
- `SetFocus(float f)`: computes `Weight[i]`, `LaneSpacing[i]`, `RingStart[i]`, `RingEnd[i]`, `GapEnd[i]` and `Hub`.
- `float Map(float rBase)`, `float Unmap(float r)`, `float Weight(int era)`, `float OuterRadius`, `int RingAt(float r)` (for band hit-testing).
- `void Positions(float[] x, float[] y)`, plus edge points into reusable `float[][]`.
- `float FocusInner`, `float RingMid(int era)`.

**`ResearchRadialLayout.cs`**: small additions only. Add `RingIndex[era]` (era to ring index, or -1) and `MaxLanes`. Placement and tests are unchanged.

**`ResearchRadialMap.cs`** (most of the work, about 400-500 lines touched):
- State: `_lens`, `_lensF`, `_lensTarget`, `_lensVel`, `_lensScrubbing`, plus `_lx/_ly` arrays and `_edgeWorld`/`_edgeBounds`, all rebuilt in `UpdateLensGeometry()` only when `_lensF` changes. That is about 490 edges × up to 40 points: a few microseconds, and nothing is rebuilt while idle.
- Replace every `_layout.X/Y/Radius/RingInner/RingOuter/GateRadius` read with the lensed equivalents. Affected: `FitCenter` L465, `FocusProject` L490, `ResetView`, `HitTest` L698, `Neighbour` L722, `DrawRings` L854, `DrawEdges` L1014, `DrawNodes` L1109, `PlaceLabels` L1391, the ping and the turning-point pointer, and `DrawnOuter`/`MinZoom` (use `_lens.OuterRadius`).
- New public methods: `FocusEra(int era, bool animate)`, `StepEra(int delta)`, `ScrubBy(float df)`, `EndScrub()`, `float LensEra` (for host and capture), and `Action<int>? EraFocused`.
- `IsLifted(p)` and a per-node scale `NodeScale(p) = lerp(dotScale, 1, w_era)`, which is 1 for lifted nodes and gates.
- `PlaceLabels`: skip nodes whose age has `w < 0.75` unless lifted. During a tween, re-place labels at most every 4th frame and only when `w` crosses a threshold. This avoids paying the collision pass every frame. Labels freeze and fade during motion and re-place when it settles.
- `_Process`: add the lens tween next to the camera tween.
- `_GuiInput`: Shift+wheel, horizontal `PanGesture`, and band click.
- `_GetTooltip`: band tooltip, computed from `_looks` counts per age. Cache those counts in `SetLooks`.
- Rename the confusing pair: `_focusEra` becomes `_currentAge`, so "focus" means only the lens.

**New: `game/Scripts/Ui/ResearchEraRail.cs`** (a `Control` drawn in `_Draw`, about 200 LOC). The map feeds it `SetAges(names, known[], total[], current, maxFocusable)` and `SetLens(float f)`. It raises `AgePicked(int)`, `Scrubbed(float f)` and `ScrubEnded(float velocity)`. The map adds it as a child, like `_toolPill`.

**Host, `Game.ResearchWeb.View.cs` / `Game.ResearchWeb.cs`:**
- `OpenResearchWeb` and `JumpToProject` call `FocusEra`.
- `HandleWebKey` gets PageUp/PageDown/[/].
- Detect an age advance in `RefreshMapView`.
- No persistence beyond the open/close of one session. The lens is UI state only: no `GameSave` or `UiSettings` field.

**Scenario:** add `map-eras` to `Game.Scenarios.cs`, next to `map` at L78.

**Risks and edge cases:**
- **Hit-testing under scaling.** Hit-test against lensed screen positions, and only for focused-age nodes, lifted nodes and gates. Everything else in a band resolves to the band. Hover reach stays at least 13 px.
- **Tiny targets.** Collapsed dots are never individually clickable, by design.
- **Dead ends.** The rail caps at `_drawEra`. A locked chip's tooltip says what opens it ("Opens with Sedentism").
- **Window resize.** The lens is in world units, so only `MinZoom`/`Usable` change. Test at 1280×720, where the map is narrow (`Size.X < 780` already compacts the tools).
- **Many ages later.** The hub floor (`HubMin`) and the bounded outer radius hold for 10 ages. If more are ever added, drop collapsed lanes to 5 units for ages more than 3 away from the focus.
- **Theme filter and search.** These still dim alpha. Search matches in collapsed ages become lifted, and the search result list already jumps (it now also focuses the age).
- **Wheel muscle memory.** Plain wheel behaviour is unchanged.

## 5. Verification

**Unit tests** (`src/CivSim.Tests/ResearchEraLensTests.cs`, using the real `TestPack.Catalog`):
1. `Map` is strictly increasing and continuous for f on a grid of 0.05.
2. At integer f, the focused ring's inner radius equals `FocusInner` and its thickness is `Lanes·44`, for every ring.
3. Lane-0 neighbour spacing in the focused ring is at least 60 world units for every age.
4. Continuity in f: when f changes by 0.01, no node moves more than 6 world units.
5. Every node stays within its age's band, and gates stay in their gap, for all f.
6. The hub is at least `HubMin` and `OuterRadius` is at most 1,600 for a synthetic 10-ring layout. This needs a test-only constructor that takes lanes per ring.
7. Every edge still points outward after lensing.
8. `Unmap(Map(r)) ≈ r`.

Existing `ResearchRadialLayoutTests` must stay green and unchanged.

**Screenshots.** Scenario `map-eras`, run like the existing `map` scenario. Godot is `/Applications/Godot_mono.app/Contents/MacOS/Godot --path game -- --scenario=map-eras --shots=<dir>`, built first with `dotnet build game/CivSim.Game.csproj -c Debug`, with `--start-minute=30` and research granted the way `map` does it. Shots:
- Focus on Dawn, Roots and Iron.
- f = 2.5 mid-scrub, via a `LensEra` setter.
- A selected Iron project with its chain into Bronze and Roots.
- A search match in a collapsed age.
- A band hover tooltip.
- 1280×720.
- ReduceMotion on.

**Acceptance:**
- Every age reads at default zoom 1.0 without overlapping nodes.
- The whole disc fits at zoom ≥ 0.3 in the current 5-age content (today it needs 0.21).
- Focus changes settle in ≤ 350 ms with no visible jump.
- A selection's prerequisite chain stays visible across collapsed ages.
- The frame time while scrubbing is under 4 ms of `_Draw` on the dev Mac (check with the F3 overlay).
- `dotnet test CivSim.slnx -c Release` stays green.

## 6. Effort and who should implement

About 1,100 LOC across 7 files: 2 new UI classes (~400), 1 new test file (~150), ~450 changed in `ResearchRadialMap.cs`, and ~100 in the host and scenario.

- **Phase 1:** lens math and tests (`ResearchEraLens`, layout additions, `ResearchEraLensTests`). Self-contained and fully testable. **Sonnet** can do it from this spec.
- **Phase 2:** map surgery and interaction. Thread lensed coordinates through every draw, hit-test and label path; add lifted nodes, band hits, the tween, keys and auto-focus. This touches a dense 1,677-line file where a missed `_layout.X` read gives subtly wrong hit-tests or labels. **Opus.**
- **Phase 3:** the era rail, tooltips, footer hint, the `map-eras` scenario, screenshots and tuning (band thickness, τ, label thresholds). Sonnet is fine, with Opus reviewing the screenshots.

Ship each phase separately with tests green. Phase 2 alone already delivers the feature with keys and Shift+wheel, before the rail exists.

## 7. Open questions (with recommended defaults)

1. **Should the mouse wheel keep zooming?** Recommended: yes, same as the world map. Shift+wheel, a sideways two-finger swipe and PageUp/PageDown change the age.
2. **When you reach a new age with the tree open, should it slide to the new age by itself?** Recommended: yes, after a short pause, but only if you were looking at your current age at the time.
3. **Should the tree remember which age you were looking at after you close it?** Recommended: no. It always opens on your current age, or on the age of the project you clicked to open it.
4. **Should ages you have not reached show on the side strip?** Recommended: show the next age by name as locked, and leave later ages off until they have real content, so the strip never promises ages that are not built yet.
