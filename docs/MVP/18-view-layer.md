# 18 — The View Layer (sim → renderer contract)

> What the simulation hands the Godot front end, and how. This is the "read-only view structs, double-buffered, plus an event stream" of [15 §5.1](15-architecture-decision.md#51-layers-and-the-one-way-interface), built in `CivSim.Sim.View` (`src/CivSim.Sim/View/`) ahead of M7a ([17 §3](17-vertical-slice.md#3-milestones)). The front end draws from it and sends `Command`s back; it never writes simulation state (rule D7).
> **Status (2026-10-20):** the sim-side frame contract is built and tested. The new `game/` Godot 4.7 .NET project references `CivSim.Sim` and has an initial low-poly renderer, viewer fog, season and territory shading, figures, herds, structures, camera controls and command ingress. The 2,400-figure frame-rate gate and later sprite/zoom-level LOD work remain open.

---

## 1. The pieces

| Type | File | Role |
|------|------|------|
| `WorldView` | `WorldView.cs` | The static map, built once from `GeneratedWorld`: biome, elevation, climate, flags (river, lake, spring, coast), rainfall, soil, deposits, start tiles. Terrain does not change in the MVP, so frames do not repeat it. |
| `HexLayout` | `HexLayout.cs` | Tile geometry exactly as `HexGrid`: odd-r offset, axial conversion, tile centres in map units, nearest tile of a position, neighbours, distance. |
| `MatchFrame` | `MatchFrame.cs` | One snapshot for one viewer: structure-of-arrays buffers of integers, refilled in place. |
| `FrameBuilder` | `FrameBuilder*.cs` | Fills frames from a `MatchModel` for a viewer (or omniscient), keeps the viewer's fog memory and the event log, lays figures out, saves and loads its own state. |
| `FrameStore` + `IdLink` | `FrameStore.cs` | Lock-free hand-off of frames from the sim thread to the render thread, with figures and herds matched by id between the last two frames. |
| `ViewEvent`, `ViewEventKind`, `ChronicleKey`, `ViewText` | `ViewTypes.cs` | The event stream for the Chronicle, Moments and alerts; text keys, never prose. |
| `PlayerCommands` | `PlayerCommands.cs` | Builders for every command a human player sends, including the new politics orders (§7). |

Two small read-only surfaces were added to the systems for it: `LivingEconomyModel.View.cs` (`LookFigure`, fire, build progress, fields, orchards, domestic stock, method rung) and `PoliticsSystem.View.cs` (camps, the generated world). Neither changes state.

## 2. Static world

`WorldView.FromGenerated(world)` copies the per-tile terrain into byte arrays (`Biome`, `Elevation`, `Climate`, `Flags`, `Rainfall`, `Soil`, `Continent`, `Deposit`, `DepositAmount`) plus `StartTiles`. A renderer builds its terrain mesh and textures from it once. `Deposit` is the truth; `MatchFrame.KnownDeposits[tile]` is 1 only after the viewer has seen that tile (or in omniscient mode). Read `WorldView.Deposit` and `DepositAmount` only where the mask is set. The mask is derived from the frame builder's saved fog memory, so the Resources overlay does not reveal deposits beyond explored land.

## 3. Frame fields and units

**Units.** Positions are integers in **map units**: one tile column is `HexLayout.Unit` = 1024 units east, one row is 1024 units south. The centre of tile (x, y) is `(x·1024 + 512 + (y odd ? 512 : 0), y·1024 + 512)`. Rows of a pointy-top hex grid are √3/2 of a column apart, so in Godot:

```csharp
const float U = 1f / HexLayout.Unit;
Vector3 World(int x, int y) => new(x * U * HexWidth, 0f, y * U * HexWidth * 0.8660254f);
```

Shares are permille, Fix64 values are raw Q32.32 `long`s (divide by 2³² to display), bands are the small enums in `ViewTypes.cs`. Ids: figures by economy figure id (stable for life), settlements by economy settlement index (stable for the match; events, parties and occupations use it), herds by herd id, projects by dense pack index (`ResearchCatalog.Projects[i]`).

| Block | Fields (arrays are indexed 0 .. Count-1) |
|-------|------------------------------------------|
| Header | `Sequence` (build number), `Tick` (state after ticks 0 .. Tick-1), `Season`, `SeasonOfYear` (0 spring .. 3 winter), `Year`, `TickInSeason` (0–149, to blend the seasonal palette), `LastWinterHarsh`, `ViewerCiv`, `Width`, `Height`, `TotalFigures` (debug only) |
| Tiles (W × H) | `Visibility` (Unknown, Explored, Visible), `Owner` (−1 none; remembered on explored tiles), strongest `Claim`/`Claimant` plus `Claim2`/`Claimant2` and `Claim3`/`Claimant3` (hundredths, 70 = ownership threshold; visible tiles only), `TileFlags` (Contested, Camp, Beacon, HomeRange), `KnownDeposits` (1 only for a deposit on a tile the viewer has discovered), `ForestBand` (0–3 on visible tiles), `DepositWorked` (1 after ore has been mined, only with current sight), `MiningReach` (1 on known tiles within the viewer's own mining reach; omniscient mode includes any civilization) |
| Figures | `FigureId` (ascending), `FigureCiv`, `FigureSettlement`, `FigureTile`, `FigureX/Y`, `FigureActivity` (job, Scout, Child, Convalescing, OnGift/Barter/Raid/Assault, Occupying), `FigureAge` (child/adult/elder), `FigureHealth` (4 bands), `FigureFlags` (female, walking, wounded, ill, pregnant, hungry, cold, warrior, armed, away, Free Folk), `FigureToolRung` (best method rung usable with current tools), `FigureKnownToolRung` (highest researched rung), `FigureMethodEfficiencyPermille` (active method base rate / researched method base rate; omits tool wear and unrelated modifiers), `FigureMethodFallback`/`FigureMethodFallbackTool` (missing tool or the three-person hunting crew gate), `FigureCarry` (plants, meat, fish, wood, stone, grain, goods) |
| Wild herds | `HerdId` (ascending), `HerdSpecies`, `HerdTile`, `HerdX/Y`, `HerdAnimals`, `HerdFear` (0–255), `HerdAway` (off its home range) |
| Predators | `PredatorTile`, `PredatorSpecies`, `PredatorSize` (static packs from world generation; the economy has no predator movement) |
| Settlements | `SettlementIndex` (ascending), `SettlementCiv`, `SettlementTile`, `SettlementPopulation`, `SettlementFood` and `SettlementWinterForecast` (food bands: secure ≥ 1.0, tight ≥ 0.9, short ≥ 0.7, famine), own-settlement `SettlementScoutPolicy` (`Auto`, `Zero`, `One`, `Two`), `SettlementCohesion` (strong ≥ 1.0, steady ≥ 0.9, strained ≥ 0.75, fraying), own-settlement `SettlementOverreach` and `SettlementSpanPenalty` (Cohesion target penalties), `SettlementFlags` (Free Folk, fire lit, fission pending, occupied, remembered, empty, thorn barrier, palisade, lookout, own), `SettlementBuildKind` and `SettlementBuildPermille`, domestic stock, `SettlementSeenSeason`, and `SettlementStructures[i × StructureKinds + kind]` (names: `LivingEconomyModel.StructureName(kind)`) |
| Plots | `PlotTile`, `PlotSettlement`, `PlotKind` (field, orchard): the field patchwork of E2 ([08 §3](08-ui-and-world-display.md#3-how-each-era-looks-on-the-map)) |
| Camps, Beacons | `CampTile/CampCiv`, `CampKind` (seasonal or mining), `CampParent`, `CampDeposit`, `CampCrew`, `CampBuilt`, `CampMineHead`, `BeaconTile/BeaconCiv` |
| Parties on the road | `PartyKind` (gift, barter, raid, assault), `PartyCiv`, `PartyTargetCiv`, `PartyFrom/To` (settlements of the leg walked; a returning barter trip walks back), their tiles, `PartyX/Y`, `PartyProgress` (permille), `PartyMembers`, `PartyArriveSeason`, `PartyLeg` |
| Occupations | `OccupationCiv`, `OccupationSettlement`, `OccupationTile`, `OccupationHolders`, `OccupationSinceSeason`, `OccupationRequiredSeasons` |
| Civilizations | `CivFlags` (self, detailed, aware, contacted, at war, allied, in truce), public: `CivEra`, `CivBreadthDone/Needed` (keystone breadth), `CivLegacy`, `CivFirstsLegacy`, `CivDominion`, `CivLandBasisPoints`, `CivPosture`, `CivRelations` (with the viewer), `CivWeaponTier`; private (zero unless detailed): `CivPopulation`, `CivSettlements`, `CivProjectsDone`, `CivInsightPerYear` |
| Focus bar | `FocusCiv` (the viewer; civilization 0 when omniscient), `FocusSlotProject[3]` and `FocusSlotPermille[3]`, `FocusQueue`, `FocusCouncil`, `FocusLeaning[3]`, `FocusBeaconsAllowed/Placed`, `FocusEra`, `FocusProjectState[project]` (`ProjectState` of all 519 projects: the research web's node states for M7b) |
| Events | `Events[0 .. EventCount)` (the newest, oldest first), `LatestEventSequence` (§6) |

`MatchFrame.IndexOfFigure(id)` and `IndexOfSettlement(index)` binary-search a frame.

### 3.1 Figure positions
The economy is aggregate per settlement ([05 §5](05-simulation-structure.md)). Most figures have a settlement and a job, not a place, so the builder **lays them out** as a pure function of (figure id, settlement, job, season, tick), hashed with `Hash64.Mix` (no random stream is drawn). Scouts are the exception: Politics owns their routes and interpolated positions because their paths reveal tiles and can start diplomacy:

- a home spot within ~0.3 tile of the settlement site;
- a work tile chosen per season among the settlement's tiles within 3 that suit the job (forage: forage land; hunt: tiles where a wild herd grazes within 4, else land 2–3 tiles out; fish: river, lake and shore; farm: its fields and orchards; chop: forest; herd: grass and steppe; gather: stone, flint, clay or mountain), falling back to any land and then the site;
- a 50-tick day (5 s at 1×): 8 ticks walking out, 27 working (with a small drift), 8 walking back carrying the job's goods, 7 at home;
- scouts are assigned from the settlement's adults; Politics chooses an unexplored frontier target and simulates a least-cost path out and home over one or more seasons; the moving scout reveals sight 3 around its current tile;
- children, the wounded and the ill, and home jobs (craft, build, preserve, rest, fire) drift slowly around the home spot (fire keepers close to the hearth);
- party members walk with their party (on the straight line between the two settlements, at the progress the travel time implies), occupiers stand at the settlement they hold.

The frame builder reads those scout routes and uses them to draw the scout and stamp its visible area. Other figures remain presentation positions; they have no simulated heading.

## 4. Visibility

The viewer is a civilization index, or **−1 for the omniscient debug view** (everything visible, no memory). A human viewer sees what its people see, with the belief builder's own rules ([14 §3.1](14-ai-opponents.md#31-belief-state-fog-fair), `BeliefRules`):

| Seen now (Visible) | Within |
|--------------------|--------|
| Own settlements (with people) | routine sight: era reach + 3 (9 tiles in E0) |
| Own seasonal camps | camp radius 2 + sight 3 |
| Own parties on the road, own occupying forces | sight 3 around them |
| Own scouts | sight 3 around the scouting figure |

- **Own** figures, settlements, plots, camps, Beacons and parties are always in the frame. **Rival** figures, herds, predators, parties, occupations, camps, Beacons, plots and a settlement's current details appear only on Visible tiles.
- **Memory:** every tile ever Visible stays **Explored**, with the owner as last seen and no claim strength. A rival settlement once seen stays in the frame as **Remembered** (owner and head count as last seen, `SettlementSeenSeason`, no structures or bands).
- **Civilizations:** era, keystone breadth, Legacy, Firsts, Dominion, land share, posture (warpaint shows it) and diplomacy with the viewer are public; population, settlements, Insight and projects done are only for the viewer itself.
- **Events** are filtered the same way (§6).
- **Fog fairness:** a test changes unseen rival state (kills and wounds people, cuts cohesion, adds stores in a settlement out of sight) and requires an identical frame; the omniscient frame does change, so the test is not vacuous.

The builder's memory is presentation state. It is **not** in the simulation snapshot (so checksums and the replay format are untouched); `FrameBuilder.WriteState`/`ReadState` save it next to a snapshot. Call `ReadState` right after the match's `ReadSnapshot`: the systems' event lists start empty then and the builder reads them from the start. One gap this covers: `Territory`'s claim field is not in the simulation snapshot (owners are; claims are rebuilt at each season end), so the builder saves its claim layer; without saved view state a loaded game shows no claim strength until the next season.

View-state version 4 saves the complete top-three claim layer; versions 1–3 remain readable. Scout routes and scout map knowledge are simulation state, covered by the match snapshot and checksum.

## 5. Threading and interpolation

```text
sim thread:     runner.Advance(n) → store.BuildAndPublish(builder, match)   (between ticks, every 10 ticks or so)
render thread:  if (store.TryAcquire()) …;  draw lerp(store.Previous, store.Current, alpha)
```

- `FrameStore` rotates four frames: the writer's back frame, one pending, and the reader's previous and current. `Publish` and `TryAcquire` are one `Interlocked.Exchange` each (a full fence) on an index with a "fresh" bit; no locks, no threads, no clocks in `CivSim.Sim`, so the source rules hold without any `determinism-allow`. One writer thread, one reader thread.
- If the sim publishes twice before the renderer looks, the older frame is dropped and the renderer interpolates over a longer interval; frames carry `Tick`, so `store.Alpha(renderTick × scale, scale)` gives the blend weight. Render one frame interval behind the newest to always have two frames.
- `store.Figures` and `store.Herds` (`IdLink`) match the current frame's entities to the previous frame's by id (a merge join on the ascending ids): `PreviousIndex(i)` (−1 = spawned: born or came into sight) and `Removed(k)` (died or left sight). Lerp matched ones, fade the rest.
- The builder only reads the model and must run on the sim thread between `Step`s (never during one). It draws no random numbers and writes nothing: a match with frames every 10 ticks has the same checksums and the same politics golden as one without (tested), and `civsim-harness view --compare` checks it on any seed.
- Commands go the other way: the render thread queues them (a `ConcurrentQueue<Command>` on the Godot side) and the sim thread submits them to its `SimRunner` for the next tick.

## 6. The event stream

The builder reads `ResearchSystem.Events` and `PoliticsSystem.Events` by cursor (it never clears them; do not call `ClearEvents` while a builder is attached), derives economy events by comparing state between frames, filters them for the viewer, and appends them to a bounded ring (1,024 by default). Each frame carries the newest 256 (`Events`, oldest first). Every event has a strictly increasing `Sequence`: the reader shows those above the last one it showed, and a jump means it fell more than a window behind. Ticks never go backwards: research and politics events are merged by tick (research first on a tie, as it steps first), derived events follow with the last simulated tick. Build at least once a season so no season's births and deaths are missed.

Each `ViewEvent`: `Sequence`, `Tick`, `Kind`, `Text` (a `ChronicleKey`), `Tier` (Ambient, Info, Milestone, Moment, Critical: [08 §10.1](08-ui-and-world-display.md#101-tiers)), `Civ` (whose record), `Other`, and subjects `Figure`, `Project`, `Settlement`, `Tile` (where to jump the camera), plus `A`, `B`, `C`. `ViewText.Key(text)` gives a stable dotted key (`chronicle.raid.succeeded`, `alert.famine.forecast`, `banner.first.claimed`, `moment.project.in_era` …) for the front end's localisation table, which turns the key and the numbers into a sentence. The simulation writes no prose.

| Group | Kinds (numbers are stable; append only) | Seen by a viewer |
|-------|------------------------------------------|------------------|
| Derived from the economy | `SeasonBegan` 1 (A season of year, B year, C harsh winter), `Births` 2, `Deaths` 3 (A cause, B count), `SettlementFounded` 4, `StructureBuilt` 5, `StructureLost` 6, `FireOut` 7, `FamineRisk` 8 (now or forecast; Critical), `PartyIncoming` 9 (a raid or assault on one of its settlements came into sight; Critical) | its own civilization |
| Research | `SlotFilled` 20, `CouncilPick` 21, `ProjectCompleted` 22 (Figure = innovator, Tile = where they live, A = first `VisibleChange`), `Moment` 23, `VisibleChange` 24, `FirstClaimed` 25, `EraAdvanced` 26, `Adopted` 27, `DormantChanged` 28, `ResourceSeen` 29, `ProjectUnlocked` 30 | its own; Firsts and era advances of everyone (they are announced) |
| Politics | `Aware` 40, `Contact` 41 (text by stance), `Skirmish` 42, `GiftArrived` 43, `BarterDone` 44, `RaidLaunched` 45, `RaidResolved` 46, `WarDeclared` 47, `PeaceMade` 48, `AssaultResolved` 49, `Occupied` 50, `OccupationBroken` 51, `Captured` 52, `FreeFolkFounded` 53, `AllianceFormed` 54, `BeaconPlaced` 55, `OrderRefused` 56 | records of its own side; gifts, barter and occupations also when it is the other side (those are recorded once); a refusal is visible only to the ordering civilization |

**Moments and visible changes.** `Moment` (A = 1 in-era, plays a vignette; 0 a backfill, batched) follows each `ProjectCompleted` with the same project and innovator. The front end looks the text up in the content pack: `ResearchCatalog.Projects[event.Project].Moment` and `.Flavor`. Every numeric E0–E2 project must author a Moment. `VisibleChange` is emitted once for each effect whose `Visual` is not `None`; `ProjectCompleted.A` repeats the first such category. The audit tests all numeric E0–E2 Moments, explicitly classify every `EffectKind`, and match emitted effect indexes to the completed project's visible effects.

## 7. Player commands

The front end sends `Command(Tick, Civ, Kind, A, B, C)` to the `SimRunner` (helpers in `PlayerCommands`). Existing kinds 1–9 are unchanged. **Appended** commands are processed by their owning simulation system and replayed like any other command (politics orders use the same rules as the AI's `Order*` methods; a test round-trips them through `MatchReplay`):

| # | Kind | A, B |
|---|------|------|
| 10 | `SendGift` | recipient civ; B = optional destination settlement index + 1 (0 = closest pair) |
| 11 | `SendBarter` | partner civ; B = optional destination settlement index + 1 (0 = closest pair) |
| 12 | `LaunchRaid` | target settlement, party size (≥ 4) |
| 13 | `LaunchAssault` | target settlement |
| 14 | `DeclareWar` | civ |
| 15 | `MakePeace` | civ |
| 16 | `FormAlliance` | civ |
| 17 | `RecallParty` | zero-based ordinal among the player's own parties |
| 21 | `LaunchRaidAgainst` | target civilization; the rules choose its closest living settlement, party size (≥ 4) |
| 22 | `SetScoutPolicy` | own settlement index; `Auto`, `Zero`, `One` or `Two` scouts (`Two` requires WAY-01) |

A refused order does nothing (out-of-range arguments too; negative `RemoveBeacon` indices no longer throw). No existing replay contains these kinds, so every golden is unchanged.

The sim now reports failed politics commands as `OrderRefused` events. `A` is the command kind and `B` is an `OrderRefusalReason` code (`InvalidTarget`, `RequirementsNotMet`, `InvalidState`); the front end can localize the stable `alert.order.refused` key and explain the code. A valid recall removes the expedition so its surviving members return to their origin in the next frame. `RecallParty.A` indexes only parties owned by the issuing civilization, in their expedition order; hidden rival parties cannot affect the index. `PoliticsSystem.SetExternalMind` is a match setup setting, applied before tick 0 and intentionally outside the mutable checksum and simulation snapshot. A caller resuming a match must reapply that control setting after loading the snapshot.

**Player controls:** the D panel offers gifts, barter, war, peace, alliance, raids, assaults and recall. It greys out orders when the viewer frame shows a missing prerequisite, unavailable relation, invalid settlement or missing party; the simulation remains authoritative for rules the filtered frame cannot expose, such as the other civilization's private project state. Refused orders still appear in the status panel. Gift and barter can target a visible settlement; when the destination is left automatic, the rules select the closest settlement pair. A raid can target a visible settlement or use `LaunchRaidAgainst` to have the rules select the target civilization's closest settlement, even when that settlement is out of sight. Scouting limits are exposed in the People panel's settlement controls and on a pinned own-settlement tile card. Two scouts require WAY-01. Auto mode reduces scouting when current food security is below 1.0 and uses the autumn winter forecast to reduce it before a projected shortage; that forecast reserves planned scout work. Manual limits override the automatic food response. Changes take effect at the next seasonal labor replan. Pause and speed are front-end sim-loop controls, not simulation commands: the loop decides how many ticks to run ([15 §5.5](15-architecture-decision.md#55-threading-model)); they do not affect deterministic match state or replay numbering.

## 8. Determinism and saves

The builder reads only; it draws no random numbers, never writes, and its layout hashes are pure functions of ids and ticks. Tested: the politics golden (24 minutes) passes with frames every 10 ticks plus omniscient frames every season; a match with a viewer builder has the same digest as one without; a resumed save (match snapshot + view state) produces frames identical, field for field, to the straight run's for the next 1,500 ticks.

## 9. Budgets (measured on this Mac, Release)

| Measure | Result | Budget |
|---------|--------|--------|
| Frame build, viewer 0, 40-minute match (572 people alive, 124 in sight), every 10 ticks | mean 0.073 ms, last minute 0.096 ms | < 1 ms |
| Frame build, omniscient, 70-minute match (**2,670 figures**, 85 settlements, 263 herds) | mean 0.107 ms, last minute **0.228 ms** | < 1 ms |
| First frame of a builder (cold, builds its caches) | 1–3 ms (8–9 ms including JIT) | once |
| Allocation per frame after the first minute (frames reserved with `FrameStore.Reserve`) | **0 bytes** over 2,340 and 4,140 frames | 0 |

Note: this build's matches reach ~2,400 people at about minute 68, not minute 40 (572 at minute 40, 1,045 at 50, 1,783 at 60, 2,670 at 70 on seed 20261019), so the 2,400-figure budget is measured at minute 70. Reproduce with:

```bash
dotnet run --project src/CivSim.Harness -c Release -- view --pack civsim.pack --minutes 40 --compare
dotnet run --project src/CivSim.Harness -c Release -- view --pack civsim.pack --minutes 70 --viewer -1
```

The allocation test (`ViewStoreTests`) builds viewer and omniscient frames for 600 ticks across four season boundaries and requires zero bytes; the timing test requires an omniscient frame under 1 ms. Season boundaries cost more (work tiles, method rungs and the claim layer are rebuilt once a season); a cap of `FrameStore.Reserve(figures: 4096, settlements: 128, herds: …)` keeps a full MVP match allocation-free.

## 10. Using it from Godot (C#)

The Godot project (`game/`, M7a) references the library directly; Godot 4.7 .NET targets `net8.0`, as `CivSim.Sim` does:

```xml
<!-- game/CivSim.Game.csproj (Godot.NET.Sdk) -->
<ItemGroup>
  <ProjectReference Include="../src/CivSim.Sim/CivSim.Sim.csproj" />
</ItemGroup>
```

```csharp
// Sim thread (a System.Threading.Thread owned by the game, never the render thread):
var match = MatchModel.Create(seed, pack, archetypes);
var runner = new SimRunner(match);
var builder = new FrameBuilder(match, viewerCiv: 0);
store.Reserve(figures: 4096, settlements: 128, herds: match.Economy.WildHerdCount);
while (running)
{
    while (commands.TryDequeue(out Command c)) runner.Submit(c with { Tick = runner.Tick });
    runner.Advance(10);                       // paced by the speed setting
    store.BuildAndPublish(builder, match);
}

// Render thread, every _Process:
if (store.TryAcquire()) { /* new frame: upload Owner/Visibility to an L8 texture, rebuild instance lists */ }
MatchFrame cur = store.Current!, prev = store.Previous ?? cur;
float a = store.Alpha((long)(renderTick * 1000), 1000) / 1000f;
for (int i = 0; i < cur.FigureCount; i++)
{
    int p = store.Figures.PreviousIndex(i);
    float x = p < 0 ? cur.FigureX[i] : Mathf.Lerp(prev.FigureX[p], cur.FigureX[i], a);
    float y = p < 0 ? cur.FigureY[i] : Mathf.Lerp(prev.FigureY[p], cur.FigureY[i], a);
    multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, World(x, y)));
    // sprite from FigureActivity, FigureAge, FigureToolRung, FigureCarry, FigureFlags; sash colour from FigureCiv
}
```

**Implemented in the initial Godot project:** static terraced hex terrain and water, fog/territory state texture and borders, seasonal tint and lighting, instanced low-poly figures/herds/structures, Z0–Z3 orthographic zoom, pan/orbit, pause/speed controls, right-click Beacon command, a Resources marker toggle gated by `KnownDeposits`, refused-order feedback, tile hover details, top-three visible claims, and method fallback badges with the blocker and base-rate efficiency. `IGameLook` isolates the palette, shaders, materials and primitive models behind the `civsim/look` project setting (`low_poly_3d` today); painted and pixel implementations can be swapped in there without changing the renderer-facing frame contract. The sim contract is renderer-neutral.

**Superseded 2026-10-21:** the UI described in this paragraph was redesigned; see [20-ui-design-spec.md](20-ui-design-spec.md) for the current focus bar, research web (Choose/Map) and input. The data it reads (`FocusSlot*`, `FocusProjectState`, queue, Leaning, Council) is unchanged.

**M7b first Godot pass (historical):** the persistent focus bar shows the three 4:2:1 slots, current progress, an ETA sampled from recent progress, queue peek, Insight/year and Council state; its Leaning panel edits the three shares and normalizes them to 100%. Press **T** to open a web of the viewer's visible E0–E2 projects, grouped into theme and lineage lanes with a separate Keystone spine. **W1 Overview** summarizes completed, available/slotted and glimpsed projects for each known lineage/era cell; hidden projects are omitted from both the counts and bars. **W2 Lanes** shows the individual nodes, whose optional hub sizing counts only known dependents. Search covers IDs, names, lineage codes, effect keywords and gate targets; filters cover theme, type and state. The inspector shows formatted effect previews, flavor text, current viewer-visible figure counts for method actions (including how many are below the new rung), and clickable known prerequisites and unlocks that navigate across lanes. Dragging a visible node onto a focus slot assigns it; dragging a slotted node to a different slot changes its rank while preserving progress. Slot, enqueue and dequeue orders go through `PlayerCommands`. The M7b exit gate remains open until a first-time player makes a meaningful choice in ≤30 seconds.

**M7c first Godot pass:** the left map-layer controls switch claim-strength, settlement food-security, and worker method-rung views; **R** toggles known resources. Food and method bands are built from the current filtered `MatchFrame`; remembered settlements do not contribute. Method sites with a better researched but blocked rung turn amber; the hover card names the missing tool or hunting crew and shows output efficiency against that rung. The hover card reports terrain, elevation, known owner, visible claim top three, discovered deposits and current visible workers. The Chronicle shows recent dated frame events, and rows with a known map tile jump the camera there. Critical events and refused orders also appear as alerts. An in-era `Moment` event displays the project's content-pack Moment and Flavor; keystones use a headline card. Territory tint and borders remain visible underneath overlays.

**M10 E3 overlays:** Deposits marks discovered ores and distinguishes worked deposits and mining reach; Trade shows active compact routes and caravans; Cohesion colors current settlement bands; Forest shows standing-stock bands only on currently visible tiles. Shift-hover on a visible tile includes mining-camp status and forest band; settlement tiles show Cohesion and own overreach/span penalties. Use `--overlay=claims|food|methods|resources|deposits|trade|cohesion|forest` to capture a layer directly.

**Still to do on the Godot side (M7a–M7c):** add character art and movement animations; finish zoom-specific representations (crew glyphs at Z2 and no figures at Z3); and run the M7b first-time choice check. The 2,400-figure live render gate with overlays, the E0–E2 Moment/VisibleChange audit, tile hover details, and method fallback badges are complete.
