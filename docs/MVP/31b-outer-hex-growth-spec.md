# 31b. Outer-hex growth: the simulation decides, the renderer draws (WP6 spec)

Status: WP6a simulation implementation is complete on `codex/wp6a-outer-hex` (2026-11-11); WP6b frame and renderer work and WP6c
default-layout work remain open. This spec builds on WP3 (plot-free seven-hex core) and WP4 (clean-core siting). Numbers marked (M)
were measured, (E) are estimates, (P) are proposals. §5.5 documents the pre-implementation shadow method; §5.6 records the enforced
WP6a result.

Read first: [31](31-settlement-core-reserve-plan.md) (the seven-hex core, WP3/WP4) and [31a](31a-staged-core-and-building-budget.md)
(the growing core and the building budget; the owner approved all six of its decisions on 2026-11-10).

## 0. In plain words

**What was wrong before WP6a.** The simulation kept fields off a settlement's own hex and the six around it, while the approved design
gives a city **one more hex from the next ring out for each big building**. Before WP6a, only the renderer prototype picked those extra
hexes. WP6a now computes and enforces them in the simulation. Until WP6b sends those rows in the frame, the renderer still picks them
independently, so a live picture can disagree with the sim's claims.

**The fix.** Move the choice into the simulation as a fixed rule, and make the renderer only read the answer:

* A settlement takes one outer hex for each of these: a **castle**, an **academy**, a **theatre or arena**, a **bathhouse**, a
  **ziggurat**, the **Great Dome** and the **Great Library** (later the Gothic Cathedral). A **City** also takes a hex of homes once it
  has had 120 people, and a second at 180. Nothing else takes a hex: temples, palaces, markets, granaries and houses stay in the
  seven-hex core; fields, mines, mills and the lighthouse stay outside it.
* The hex is taken **as soon as the building is started** (the ground is cleared first). If the town sets a half-built one aside
  to build something else, the hex goes back to the fields until work resumes, because nothing is drawn there meanwhile.
* **Each kind of building has its own fixed spot** around the city, counted from the city's gate side: the castle behind the watch
  quarter, the academy behind the workshops, the ziggurat straight behind the temple, and so on. If that spot is water, a mountain, a
  river hex or a metal, salt or coal deposit, the building takes the nearest free spot instead. **A building on its own spot never
  moves** when others are added.
* A field, orchard or terrace on a newly taken hex (the city's own or a neighbour's) **moves out at the start of the next season**,
  as WP3 already does for the core.
* The hex is given back at once when the building is gone (razed, abandoned) or the settlement dies out; a City's homes hex is given
  back only after its population has stayed under the threshold through a whole year, so it does not flicker.
* **Nothing new is saved.** The hexes are worked out from what is built, so old saves and replays need no conversion.

**What it costs.** The shadow estimate before enforcing the rule (four Iron games, §5.2–5.4) gave 3.36 outer hexes per City and 1.18
per Town at minute 110, with about 0.5% lower best-k field yield overall. The enforced four-game run (§5.6) gives 3.30 per City and
1.13 per Town, with a measured 0.24% best-k yield reduction by minute 110. It observed no duplicate, unusable or in-another-core
claims, and no Town or City short of free ground at that checkpoint. There were 16 district moves between minutes 90 and 110.

**Work:** WP6a (rule and tests in the simulation, about 4 days), WP6b (send the hexes to the renderer and delete its own choosing,
about 3 days), WP6c (make the new layout the default and delete the old spiral, about 2 days).

**Two changes from 31a, decided here** (both for stability): the gate side no longer follows the nearest trail or road (those appear
during a game and would turn the whole city round), and each building has a fixed home spot instead of "fill the next hexes, then hand
them out by affinity" (which moved the academy when a castle arrived).

The owner approved both stability decisions on 2026-11-10: (1) Cities take homes hexes at 120 and 180 people; (2) the city's gate side
ignores roads and trails, facing only the sea, the river or a fixed side. They are recorded in §6.3.

## 1. The rule

### 1.1 Size classes, by structure id

Every one of the 60 entries in `content/registries/structures.yaml` falls in exactly one class (the WP6a registry test fails when a new
id is not classified; this is how owner decision 5, "every new building upgrades an old one or joins a district", is enforced until the
content schema carries a size field).

| Class | Takes an outer hex? | Structure ids (registry) | Where the sim counts it |
|---|---|---|---|
| **Whole, outer district** | **yes, one per district** | `castle`, `academy`, `theatre_arena`, `bathhouse`, `ziggurat`, `great_dome`, `great_library` | Iron works (`IronStructure.Castle`, `Academy`, `Theatre`, `Bathhouse`, `GreatDome`, `GreatLibrary`); `CivicState.Ziggurats` / `CivicBuild.Ziggurat` |
| Whole, ring 1 | no (Sacred hex, Heart) | `temple`, `temple_tier2` (colonnade), `palace` | `CivicState.Temples`, `IronStructure.Colonnade`, `CivicState.Palaces` |
| Half | no (ring 1) | `granary`, `potters_workshop`, `forge`, `market`, `caravanserai`, `school`, `crane`, `press`, `longhouse`, `hall` | |
| Small | no (ring 1) | `hearth`, `lean_to`, `hide_tent`, `pit_house`, `wattle_daub_house`, `mudbrick_house`, `storage_pit`, `drying_rack`, `kiln`, `quern`, `totem`, `lookout_post`, `signal_post`, `shrine`, `bakery`, `smelter`, `windlass_well`, `aqueduct` (its tower; the spans are outside) | |
| Perimeter | no (the wall line) | `thorn_barrier`, `palisade`, `stone_walls`, `gatehouse` | |
| Edge | no (the waterfront) | `weir`, `harbor` | |
| Outer work | **never on an outer hex** | `field`, `orchard`, `pasture`, `terrace`, `canal`, `mining_camp`, `mine_head`, `marker`, `seasonal_camp`, `hillfort`, `megalith`, `great_lighthouse`, `water_mill`, `road`, `bridge`, `frontier_wall_segment` | plots and camps have tiles in the sim; the rest are drawn by the renderer, which must keep them off outer hexes (WP6b) |

18 small + 10 half + 3 whole ring-1 + 7 outer districts + 4 perimeter + 2 edge + 16 outer works = 60. The galley is a ship, not a
registry structure. E5 (`codex/e5-fief`, not merged) adds: `gothic_cathedral` (its own Wonder district, owner decision 4),
`concentric_castle` (joins Citadel), `university` (joins Learning), `hospital` (joins Health as a half), `clock_tower` and `print_shop`
(small), and `monastery`, `windmill`, the water-powered sawmill, `blast_furnace` and `powder_mill` (outer works, owner decision 3).

### 1.2 Districts and what makes one present

`CoreDistrict` (new enum, append-only; its numeric values are frame and test ids, not the priority):

| Value | District | Present when (all read from state that is already saved) |
|---|---|---|
| 0 | Ziggurat | `Civic.Ziggurats > 0` or `Civic.BuildKind == CivicBuild.Ziggurat` |
| 1 | Great Dome | `IronWorks[GreatDome] > 0` or `IronBuildKind == GreatDome` |
| 2 | Great Library | the same for `GreatLibrary` |
| 3 | Citadel | the same for `Castle` |
| 4 | Learning | the same for `Academy` |
| 5 | Civic | the same for `Theatre` (`theatre_arena`) |
| 6 | Health | the same for `Bathhouse` |
| 7 | Homes C | `Civic.Tier == City` and `max(population, Civic.PeopleLastYear) >= SettlementCoreHomesFirst` (120) |
| 8 | Homes D | `Civic.Tier == City` and `max(population, Civic.PeopleLastYear) >= SettlementCoreHomesFirst + SettlementCoreHomesStep` (180) |
| 9 (E5, appended later) | Cathedral | the same Iron-works test for `gothic_cathedral` |

* **Queued counts.** A district is present from the moment the planner makes it the current build (`IronBuildKind`, `Civic.BuildKind`),
  so the ground is cleared before work starts (31a §3.2). A build set aside (`IronShelf[k] > 0`, the town switched to another work) does
  **not** hold its hex: the renderer draws nothing for a shelved build, and holding the hex would keep 30% more land idle (§5.3). The hex is
  taken again when the work resumes; its fields are released at the next season start as for any new claim. A build dropped with its
  progress lost (`IronBuildWants` clears it when no longer feasible) also gives the hex back.
* **Wonders built by a league** (`IronPledgeTo`) claim a hex only where the Wonder stands (the settlement whose `IronBuildKind` it is),
  never at the contributors.
* **Homes use last year's head count** as well as today's, which is the hysteresis: a City that dips under 120 keeps the hex until a
  year end confirms the drop (`PeopleLastYear` is set at each year end, `LivingEconomyModel.Civic.cs`). Tier demotion has its own
  `DemoteSeasons` delay. The two thresholds become content constants in `content/constants/balance.yaml` (`SettlementCoreHomesFirst: 120`,
  `SettlementCoreHomesStep: 60`, source 31a §3.2).
* Fission does not split districts (they stay with the mother). Conquest keeps the buildings, so the hexes stay. A razed settlement
  (`RazeSettlement` clears `IronWorks` and resets `Civic`) and a settlement with no people claim nothing.

### 1.3 Geometry: directions, the Front and the twelve positions

**Directions.** The rule uses the renderer's angular numbering (`world_data.gd neighbour()`): on odd-r offset rows, 0 = x + 1;
1 = row + 1, x + (odd ? 1 : 0); 2 = row + 1, x − (odd ? 0 : 1); 3 = x − 1; 4 = row − 1, x − (odd ? 0 : 1); 5 = row − 1, x + (odd ? 1 : 0);
off the map gives −1. `HexGrid.Neighbors` lists neighbours in a different, non-angular order and must not be used for this. WP6a added
`HexGrid.Step(tile, direction, width, height)` with this table and a test that compares it with `Neighbors` as sets.

**The Front** (the gate side, the direction the ring-1 quarters are counted from), static for the whole match:

1. Water: let `d0 = ((site % 997) * 6) / 997` (integer arithmetic; it equals the renderer's `int(seed * 6.0)` for every tile, because
   997 is prime, and keeps harbors where `world_builder._harbor` puts them). For `i = 0..5`, `d = (d0 + i) % 6`: the first ring-1 hex in
   direction `d` that exists and is `Biome.Ocean` or `Biome.Coast` is the Front.
2. River: else the first `d = 0..5` whose ring-1 hex exists, is not Ocean or Coast, and has `River`.
3. Else `site % 6`.

31a also looked at the nearest trail or road. That is dropped: trails and roads appear and change during a match, the Front would turn,
and every district would change hex at once. A static Front makes "a building on its home never moves" true.

**The twelve positions.** Position `p = 0..11` counts in 30° steps from the Front. Corner `k` (`p = 2k`) is two steps in direction
`F + k`; edge `k` (`p = 2k + 1`) is one step in `F + k` and then one in `F + k + 1` (all directions mod 6). These are the 12 ring-2
hexes, each at hex distance exactly 2 (the renderer's `ring2_tiles` already uses this construction).

Ring 1 for reference (31 §4.2): Front `F`, Homes A `F+1`, Workshops `F+2`, Sacred `F+3` (straight back), Watch `F+4`, Homes B `F+5`.

### 1.4 Home position of each district, and priority

| Priority | District | Home | Lies behind | Why |
|---|---|---|---|---|
| 1 | Ziggurat | p6 (corner 3) | Sacred, straight back | the first Wonder faces the gate across the whole city (31a §4.2) |
| 2 | Great Dome | p7 (edge 3) | between Sacred and Watch | second Wonder beside the first |
| 3 | Great Library | p5 (edge 2) | between Workshops and Sacred | beside Learning (31a §1.3) |
| (E5) | Cathedral | p9 (edge 4) | between Watch and Homes B | reserved now so E5 moves nothing |
| 4 | Citadel | p8 (corner 4) | Watch | on the wall line toward the Watch (owner decision 6) |
| 5 | Learning | p4 (corner 2) | Workshops | beside the Sacred side |
| 6 | Civic | p3 (edge 1) | between Homes A and Workshops | toward the Front side |
| 7 | Health | p2 (corner 1) | Homes A | toward the Front, near the aqueduct tower and well |
| 8 | Homes C | p10 (corner 5) | Homes B | beside the homes quarters |
| 9 | Homes D | p11 (edge 5) | between Homes B and Front | beside Homes C |
| | spare | p1 (edge 0), then p0 (corner 0) | the gate | p0 is straight out of the gate and is taken last of all |

The homes are distinct, so in the usual case every district simply sits on its home. The priority (the first column; the Cathedral
goes after the Great Library when E5 appends it) only matters when hexes run short: Wonders first because they cannot be doubled up,
homes last because houses can crowd into ring 1.

### 1.5 Which hexes may be taken

A position is **usable** when its tile:

| Skip | Test in the sim | Static? |
|---|---|---|
| off the map | `Step` gives −1 | yes |
| water | `Biome.Ocean` or `Biome.Coast`, or `Lake` | yes |
| mountain | `Biome.Mountain`, `Biome.Peak` or `Biome.Ice` | yes |
| valuable deposit | `SettlementCoreRules.IsValuableCoreDeposit(world.Deposit(tile).Kind)`, true map, **known or not** (metal, salt, coal; later saltpeter and sulfur through the same function) | yes |
| river | `River` | yes |
| a neighbour's core | within 1 of another living settlement's site | no (guard) |
| possibly a neighbour's outer hex | within 2 of a **lower-id** living settlement's site (lower id wins, so no recursion is needed) | no (guard) |

Not skipped: fields, orchards, terraces and canals of anyone (they are released, §1.7); stone, flint and clay deposits (pooled surface
stock, as in WP4); forest (wood stays pooled); pastures (no tile in the sim). Mining camps stand only on deposit tiles, so they are
never on a usable hex.

The two guards cannot fire under today's spacing (no two living sites closer than 5): a neighbour's core is at least 5 − 2 − 1 = 2 away
from any ring-2 hex, and two ring-2 hexes coincide only when the sites are at most 4 apart. They are kept because Found, conquest and
world-generation repairs could change, and they cost one distance test per living settlement.

River hexes are skipped (31a §3.3 (c)) because the sim only knows "a river runs through this tile" and the renderer cannot stand a
whole-hex building on a river; it also keeps the best irrigated farmland as farmland. §5 measures what that costs in blocked homes.

### 1.6 Assignment (pure, deterministic)

Input: the 12 usable flags and the set of present districts. Output: a position (0..11) or "none" per present district.

1. **Pass 1**, in priority order: each present district whose home is usable takes it.
2. **Pass 2**, in priority order: each present district still without a hex takes the first usable, untaken position of its
   **preference list**: all positions by circular distance from its home (in 30° steps), at equal distance the one at `home + d` before
   `home − d`, and p0 (straight out of the gate) last regardless of distance.
3. A district left without a position is **unhoused**: it claims nothing. The sim never blocks a build for it.

Properties (each is a WP6a test, checked exhaustively over all 2^9 present sets and all 2^12 usable-position masks):

* a district whose home is usable is always on its home, whatever else is present (so it never moves);
* the number of hexes claimed is `min(present districts, usable positions)`; adding a district adds exactly one hex while one is free;
* only a district pushed off an unusable home can move, and only when the district whose home it borrowed is added (§5 counts this).

Unhoused districts are drawn per 31a §3.5 (homes crowd into ring 1; a district doubles up with its neighbour at about 0.7 scale) and
listed in the settlement panel as "no room in the city". §5 shows how rare this is.

### 1.7 Fields already there, release and hysteresis

* **Claim.** From the moment a district is present, its hex is part of the reserved core: `IsReservedCoreTile` answers true, so the
  planner places no field, orchard or terrace there and no mining camp. A plot already on it (the settlement's own or a neighbour's)
  is released by the existing `ReleaseCorePlots` at the **next season start**, in ascending settlement and tile order: the tile is freed,
  the field or orchard count (or the Iron terrace work) drops, and a canal on it goes with it. The planner re-places the plot elsewhere
  when it next wants one. The plot keeps producing for the rest of the season it was claimed in.
* **Release.** When a district stops being present (build dropped, building razed, settlement empty, City below the homes threshold at a
  year end), its hex is free at once and the planner may farm it again. There is no delay, because claims return only with a new
  building or a year of growth, which cannot flicker season to season (§5 measures re-claims within 8 seasons).
* **Shrinking, capture, destruction** follow from §1.2: nothing extra is needed.

### 1.8 Coast, river, mountain and edge cases

* **Coastal:** the Front faces the sea, so p0, p1 and p11 are usually water and skipped; every home but Homes D lies on the landward side
  by construction. The harbor stays an edge item on the Front.
* **River:** the Front faces the river when there is no sea; river hexes are never taken, so districts whose home is on the river move
  one or two positions along.
* **Mountain:** a site with few usable hexes fills them in priority order; the rest are unhoused (Wonders and castle first, homes last).
* **Map edge:** positions off the map are skipped like water.
* **Neighbours 5 or 6 apart:** their ring-2 hexes can touch but never coincide; a neighbour's field on a claimed hex is released like
  one's own (already measured in 31a; §5 again).

## 2. Data model

### 2.1 Derived, not stored

The claimed hexes are a **pure function** of: the static world (biomes, flags, deposits), the site tile, the living settlements' sites
and ids, and per settlement `IronWorks`, `IronBuildKind`, `Civic.Ziggurats`, `Civic.BuildKind`, `Civic.Tier`,
`Civic.PeopleLastYear` and the population. All of these are already saved and hashed. Therefore:

* `SnapshotVersion` does not change; old saves load, their claims are computed on load, and any plot on a claimed hex is released at the
  next season start (exactly how WP3 cleaned old cores).
* The replay format and command numbering do not change. Replays recorded before WP6 diverge where a claim releases a plot, as WP3's did.
* There is no cache that can go stale. A cache of the claims would have to equal the pure function at every tick, or a save taken in
  the middle of a season (after a build is queued) and loaded would differ from the continuous run. The function is cheap enough to
  recompute (§2.2); static parts are precomputed like WP4's `_coreClean`.

The alternative, storing per settlement the order in which districts arrived (a `byte[12]` in `SettlementState`), would remove even the
rare moves of §1.6, but needs a snapshot version bump, a migration for old saves, hashing, and a second source of truth that can
disagree with the buildings. §5 shows moves are rare enough not to justify it.

### 2.2 Simulation code

New `src/CivSim.Sim/World/SettlementCoreGrowth.cs` (static, pure, no state; next to `SettlementCoreRules`):

```text
enum CoreDistrict : byte { Ziggurat, GreatDome, GreatLibrary, Citadel, Learning, Civic, Health, HomesC, HomesD, Count }   // append-only
const int OuterPositions = 12
static ReadOnlySpan<byte> Priority   // district order for the passes: Ziggurat, GreatDome, GreatLibrary, Citadel, Learning, Civic, Health, HomesC, HomesD
static ReadOnlySpan<byte> Home       // home position per district (§1.4)
static int Front(WorldEconomyInput world, int site)
static int PositionTile(int site, int front, int position, int width, int height)        // -1 off the map
static bool IsUsableStatic(WorldEconomyInput world, int tile)                             // §1.5 static rows
static ushort Assign(ushort usableMask, ushort presentMask, Span<sbyte> positionOf)       // §1.6; returns the claimed-position mask
```

`HexGrid.Step` is added to `src/CivSim.Sim/World/HexGrid.cs`. Preference lists are a static `byte[9 * 12]` table built once from §1.6
(no sorting at run time).

In `src/CivSim.Sim/Economy/LivingEconomyModel.SettlementCore.cs`:

* constructor: `_coreFront = new byte[tileCount]` and `_outerUsable = new bool[tileCount]` precomputed from the world (static, like
  `_coreClean`; never saved);
* `private ushort CoreDistrictMask(SettlementState s)` reads §1.2;
* `private int OuterClaims(SettlementState s, Span<int> tiles, Span<sbyte> positionOf)` = static usable flags minus the two guards
  (one scan of `_settlements`), then `Assign`; returns the number claimed. Returns 0 at once when the mask is 0 (most settlements);
* `InReservedCore(tile, living)`: after the radius-1 test, for every living settlement at distance exactly 2 from `tile` whose mask is
  not 0, the tile is reserved if `OuterClaims` contains it. Nothing else in the planner changes, because every placement path already
  asks `IsPlotTile` / `InReservedCore`: `CanPlaceField` and `PlaceField` (`SettlementCore.cs:131`, `LivingEconomyModel.cs:3607`),
  `CanPlaceOrchard` / `PlaceOrchard` (`:3620`, `:3655`), `NextTerraceTile` (`Iron.cs:2269`), the mining-camp check (`Metal.cs:2681`),
  the build gates (`BuildGates.cs:131-179`, "no free ground") and the build outlook (`BuildOutlook.cs:124`);
* `ReleaseCorePlots` is unchanged; it reaches claimed hexes through `InReservedCore`;
* public read-outs: `int OuterCoreHexes(int settlementIndex, Span<int> tiles, Span<byte> districts, Span<byte> positions)` (unhoused
  districts come back with tile −1), `int CoreFront(int settlementIndex)`, and for the debug gallery only
  `int PreviewOuterCore(int site, ushort districtMask, ...)` (same rule, today's living settlements, writes nothing);
* constants `SettlementCoreHomesFirst`, `SettlementCoreHomesStep` read like `SettlementCoreRadius`.

Performance: a derived tile index limits claim and guard checks to settlement sites within two hexes; the index is rebuilt after
snapshot restore and model copy, and extended when settlements are founded. A one-seed 110-minute live audit took 139.14 seconds here
versus 137.96 seconds on the base branch (+0.9% wall time); the resulting paths differ by one settlement, so this is an indicative
end-to-end comparison rather than an isolated season-step benchmark. The full slow suite fell from about 58 minutes with the global
scan to 8m41s with the local index. The ≤3% season-step budget is met by the end-to-end comparison, with that measurement caveat.

### 2.3 Planner, forecasts and workers

* **Plot counting.** `FieldLimit` (14) is unchanged; a City that runs short of free cultivable ground hits the existing "no free ground"
  stop (`BuildWhy.NoFreeGround`) and the build outlook shows it, as for WP3.
* **Workers.** A released field lowers `Structures[Field]` (or the terrace work count), so fewer farmers are assigned next season;
  gathering, hunting, fishing and wood still count on claimed hexes (doc 31 decision: only plots are barred), so foraging and wood
  assignment do not change.
* **Forecasts.** The late-winter food forecast reads the fields that exist; no change. No AI change: the AI does not need to know which
  hex a building takes.

### 2.4 Frame and view data (appended, nothing renumbered)

`src/CivSim.Sim/View/MatchFrame.cs`, after `SettlementZigguratPermille`:

```text
/// The direction (0..5, world_data neighbour order) the settlement's core quarters are counted from (§1.3); 255 when remembered.
public byte[] SettlementCoreFront
/// Outer core hexes and unhoused districts of current (not remembered) settlements, frame-row order, then district priority:
public int CoreOuterCount
public int[] CoreOuterTile          // -1 for an unhoused district
public int[] CoreOuterSettlement    // frame row
public byte[] CoreOuterDistrict     // CoreDistrict
public byte[] CoreOuterPosition     // 0..11 from the Front, 255 when unhoused
```

`FrameBuilder.FillSettlements` fills them from `OuterCoreHexes` / `CoreFront` with the same visibility as the structure counts
(remembered settlements get none). `MatchFrame.EnsureSettlements` sizes the per-settlement array; a new `EnsureCoreOuter(n)` the rows.
`FrameDump.cs` writes them (`coreFront`, `coreOuter`). Frames are not hashed, so this moves no golden by itself.

Godot side (`game/Scripts/Game.cs`): each settlement dictionary gains `"coreFront"` (int) and `"coreOuter"` (Array of
`[tile, district, position]`); the payload gains `"coreDistrictIds"` (the enum names, sent once like `"ironWorkIds"`).
`Game.CoreGallery.cs` (debug only) computes the forced gallery settlements' hexes with `PreviewOuterCore` instead of letting the
renderer guess, so the gallery photographs the sim's rule.

## 3. Files and work packages

### 3.1 WP6a: the rule in the simulation (about 4 days)

| File | Change |
|---|---|
| `src/CivSim.Sim/World/SettlementCoreGrowth.cs` | new (§2.2) |
| `src/CivSim.Sim/World/HexGrid.cs` | `Step` appended |
| `src/CivSim.Sim/Economy/LivingEconomyModel.SettlementCore.cs` | precomputed front and usable tables, `CoreDistrictMask`, `OuterClaims`, `InReservedCore` extended, read-outs, fixtures |
| `src/CivSim.Sim/Economy/LivingEconomyModel.cs` | constructor: build the two tables; read the two constants |
| `content/constants/balance.yaml` | `SettlementCoreHomesFirst: 120`, `SettlementCoreHomesStep: 60` (source 31a §3.2); refresh `tests/golden/content-pack.txt` |
| `src/CivSim.Harness/CoreAuditCommand.cs` | `--outer`: the §5 report on the real (enforced) rule |
| `src/CivSim.Sim/Politics/PoliticsSystem.cs` (seasonal-camp placement) | new seasonal camps skip `IsReservedCoreTile` tiles (§6.2 risk 2) |
| `src/CivSim.Tests/SettlementCoreGrowthTests.cs` | new (§4) |
| `src/CivSim.Tests/SettlementCoreTests.cs` | the slow gate extended to outer hexes |
| `docs/MVP/31a…`, this doc | status lines |

**Exit gate (17 §3 style):** (1) `dotnet test` green against the recorded baseline, `SourceRulesTests` included; (2) `CIVSIM_SLOW=1`
gate: 4 seeds × Iron 110 minutes, no field, orchard or terrace stands on any claimed hex of a living settlement one season after the
claim, no claimed hex is water, mountain, river or a valuable deposit, no tile is claimed twice, and at least one settlement per seed
grows (non-vacuous); (3) save in the middle of a season right after a district is queued, load, run 8 seasons: checksum equals the
continuous run; (4) toy and worldgen goldens unchanged; research golden unchanged (E: no City at minute 12; if it moves, explain);
content-pack golden refreshed for the two constants; politics and bronze goldens re-recorded with the reason (§6.1); (5) `core-audit
--outer` within ±15% of the §5 shadow numbers, or the difference explained; (6) season step on Iron 110 at most +3%.

### 3.2 WP6b: frame data, and the renderer reads it (about 3 days)

| File | Change |
|---|---|
| `src/CivSim.Sim/View/MatchFrame.cs`, `FrameBuilder.cs` | §2.4 fields |
| `src/CivSim.Harness/FrameDump.cs` | write them |
| `src/CivSim.Tests/ViewFrameTests.cs` | rows equal `OuterCoreHexes` for current settlements; none for remembered; front in 0..5 |
| `game/Scripts/Game.cs`, `game/Scripts/Game.CoreGallery.cs` | payload fields; gallery via `PreviewOuterCore` |
| `game/Renderer/LowPoly3D/Scripts/core_layout.gd` | **delete** `ring2_demand`, `ring2_tiles`, `RING2_ORDER`, `RING2_WISH`, `_foreign_core`, `_front_of` and the affinity loop in `_assign_quarters`; read `coreFront` and `coreOuter` instead (district name → quarter `Q`); `reserved_ring2()` returns the frame's tiles; `_outer()` skips every claimed hex of every settlement (paddock, markers, charcoal, quarry, lighthouse); the dump gains `sim_outer` rows and `front` |
| `game/Renderer/LowPoly3D/Scripts/world_builder.gd` | `_structure_digest` hashes `coreFront` and `coreOuter` (the village is rebuilt when a hex is claimed); harbor and moored fleet face `coreFront` under `--core-layout` |
| `game/Renderer/LowPoly3D/Scripts/world_data.gd` | keep the two new keys on settlement dictionaries |
| `tools/check_core_layout.py` | check 6 (§4.2) |

**Exit gate:** `check_core_layout.py` passes on the dev gallery and on live captures of seeds 1 to 4 at Iron minute 110, with the
renderer's ring-2 hexes identical to the sim's rows for every settlement; no garden cue, paddock, marker, quarry, charcoal clamp or
lighthouse on a claimed hex; `grep` finds none of the deleted names in `core_layout.gd`; frame build time within 5% of WP6a.

### 3.3 WP6c: the core layout becomes the default (about 2 days)

| File | Change |
|---|---|
| `game/Renderer/LowPoly3D/Scripts/world_builder.gd` | `CORE_LAYOUT` true by default (a `--spiral` debug flag only for one release, then gone); delete the sunflower-spiral branch of the settlements loop and `_dwellings`' `DWELL_MAX_TOTAL` / `DWELL_MAX_EACH` caps, replaced by the slot capacity of the homes quarters (ring 1 plus Homes C and D) |
| `game/Renderer/LowPoly3D/Scripts/core_layout.gd` | drop `--core-v1` and the seven-hex-only path; garden cues stay only for old saves until their first season |
| capture scenario and docs | `core-gallery` without the flag; screenshots in `builds/core-gallery-v3/` |

**Exit gate:** the game draws the core layout with no flag; checker passes as in WP6b; the gallery and seeds 1 to 4 screenshots reviewed
by the owner; frame time within 5% of the spiral; no references to `DWELL_MAX_TOTAL` remain.

## 4. Tests

### 4.1 Simulation (`SettlementCoreGrowthTests`, plus the slow gate)

| Test | Checks |
|---|---|
| `Step_matches_the_renderer_and_HexGrid` | the direction table on even and odd rows; as a set equal to `HexGrid.Neighbors`; −1 off the map |
| `Positions_are_the_twelve_ring_two_hexes` | for every land site of the seed-20261019 world: 12 distinct positions on the map are at distance 2, and with no off-map position they are all of ring 2 |
| `Front_prefers_sea_then_river_then_site` | fixtures for each branch; `d0` equals the float formula for `site % 997` in 0..996 |
| `Assign_is_pure` | same inputs, same outputs, twice and in a different call order |
| `A_district_on_a_usable_home_never_moves` | exhaustive over 2^9 present sets × 2^12 usable sets |
| `Each_added_district_adds_one_hex_while_room` | exhaustive; claimed count = min(present, usable) |
| `Skips_water_mountain_river_deposit_and_offmap` | fixture world; a valuable deposit is skipped whether or not any civilization knows it |
| `Neighbour_guards` | synthetic sites 3 and 4 apart: no claim in another core; a shared tile goes to the lower id |
| `Queued_build_claims_shelved_and_dropped_release` | `FixtureStartIronBuild(i, "academy")` claims; shelving releases and resuming claims again; dropping releases |
| `Homes_follow_last_years_people` | a City at 125 claims Homes C; at 100 it keeps it until the year end, then releases |
| `Field_on_a_new_claim_is_released_next_season` | own field, orchard, terrace (works count drops) and canal; a neighbour's field |
| `Planner_never_places_a_plot_on_a_claimed_hex` | `CanPlaceField`, `CanPlaceOrchard`, `NextTerraceTile` with only claimed tiles free |
| `Razing_releases_and_capture_keeps` | `RazeSettlement` → no claims; owner change → same claims |
| `Mid_season_save_matches_the_continuous_run` | §3.1 gate (3) |
| `Every_registry_structure_has_a_core_class` | each of the 60 ids in exactly one §1.1 class |
| `Slow_four_Iron_matches_of_110_minutes_keep_every_outer_hex_clear` | §3.1 gate (2), `CIVSIM_SLOW=1`, the WP3 slow-test convention |
| `ViewFrameTests` additions | §3.2 |

### 4.2 Renderer checker (`tools/check_core_layout.py`, check 6)

The dump carries, per settlement, the sim's rows (`sim_outer`: tile, district, position) and `front`, copied by `core_layout.gd` from
the frame. Check 6: the renderer's ring-2 hexes and their districts equal `sim_outer` exactly; every whole-hex building of a housed
district stands on its row's tile; unhoused districts are drawn doubled or listed, never dropped silently; no outer work and no garden
cue on any settlement's claimed hex; no plot on a claimed hex except one claimed this season (reported, not failed). An optional
`--frame FILE` (from `civsim-harness view --dump-frame`) cross-checks the dump against an independent harness frame of the same seed
and tick.

## 5. Measurements

### 5.1 Set-up and honesty note

**This is a shadow measurement.** The four games ran the current rules (WP3 and WP4 in force, the outer-hex rule not). After every
season a throwaway program evaluated the rule exactly as §1 specifies (positions, Front, skips, guards, two-pass assignment, homes by
`max(population, PeopleLastYear)`) on the live state, read through reflection, and recorded what it would claim and release. So:

* claim counts, blocked homes, moves, overlaps and unhoused districts are **measured (M)** for the rule on these games;
* "plots released" counts the plots standing on a hex at the season it is newly claimed (**M** in the shadow; in an enforced game the
  planner would have avoided some of them, so the real number is at most this);
* field-yield loss is an **estimate (E)**: best-k base `FieldYieldPerYear` of the free cultivable tiles at distance 2 to 3 (no living core),
  with and without claimed hexes, k = fields held, the same method as 31 §6 and 31a §2.3; no depletion, irrigation or terraces;
* second-order effects (the game evolving differently once fields actually move) are **not** measured; WP6a's `core-audit --outer` on
  the enforced rule is the check (gate (5)).

Games: Iron rules, Builder, Grower, Warlord and Sage AIs at Warlord level, seeds 1 to 4, samples at minutes 60, 90 and 110, every
season evaluated for the event counts. Tiers from `LivingEconomyModel.Tier`. Program and raw output: session scratchpad
`outeraudit/` (`Program.cs`, `agg.py`, `runs/*.json`), referencing a Release build of this branch's `CivSim.Sim` and `civsim.pack`.

### 5.2 Outer hexes claimed (the rule as specified: shelved builds do not hold a hex, river hexes skipped)

| Minute | Tier | Settlements | Growing (N > 0) | Outer hexes N: 0/1/2/3/4/5/6/7/8 | Mean N, all | Mean N, growing | of which homes |
|---|---|---|---|---|---|---|---|
| 60 | Village/Band | 170 | 0 | all 0 | 0 | | 0 |
| 60 | Town | 12 | 0 | all 0 | 0 | | 0 |
| 90 | Village/Band | 306 | 0 | all 0 | 0 | | 0 |
| 90 | Town | 66 | 10 (15%) | 56/8/2 | 0.18 | 1.20 | 0 |
| 90 | City | 45 | 35 (78%) | 10/8/12/7/4/4 | 1.98 | 2.54 | 40 |
| 110 | Village/Band | 278 | 1 | 277/1 | 0.00 | 1.00 | 0 |
| 110 | Town | 34 | 25 (74%) | 9/14/8/2/1 | 1.18 | 1.60 | 0 |
| 110 | City | 105 | 103 (98%) | 2/10/26/23/13/20/7/4/0 | 3.36 | 3.43 | 111 |

No City exists at minute 60 and no Iron district has been started. The maximum seen is 7 outer hexes (14 in all). What the districts are
at minute 110 (present, all 4 seeds): homes C 83 and homes D 28 (homes are 111 of 353 City hexes, 31%), civic (theatre) 68, health
(bathhouse) 71, ziggurat 59, learning (academy) 53, citadel 20, Great Library 9, Great Dome 3.

### 5.3 Plots released and field land

| Measure | Value |
|---|---|
| New claims over the four games | 585 |
| Plots standing on a hex when it was newly claimed (would be released) | 485: own fields 318, own orchards 167, terraces 0, **neighbours' plots 0** (M) |
| Per new claim | 0.83 plots (0.54 fields) (M) |
| Plots on claimed hexes at minute 110 (shadow stock, an upper bound) | Cities 335 (173 fields), Towns 36 (28), villages 1 (M) |
| Best-k field yield, vs WP3, minute 90 | Cities −0.57%, Towns −0.02%, all settlements −0.09% (E) |
| Best-k field yield, vs WP3, minute 110 | Cities **−1.34%**, Towns **−0.78%**, all settlements **−0.50%** (E) |
| Settlements with fewer free cultivable hexes at distance 2–3 than fields held, minute 110 | 2 Cities, 3 Towns of 139 (M) |

On top of WP3's measured 0.9% the total for all settlements is therefore about 1.4% (E; the two are not strictly additive), and about
2.2% for a City. Settlements short of land fall back on the existing "no free ground" stop; the doc-31 ring-4 lever stays unused.

Variant measured for the shelved-build choice of §1.2: if shelved builds **kept** their hex, Cities would hold 4.50 (not 3.36) outer hexes
at minute 110 and Towns 2.09 (not 1.18), the yield loss would be −0.81% overall (−1.81% Cities, −2.33% Towns), and hexes would be given
back 40 times instead of 191 over the four games; quick re-claims (within 8 seasons of a release) are 13 against 17, so not holding them
costs almost no churn. Hence the rule as written.

### 5.4 Blocked homes, moves, overlaps, room

| Measure | Value |
|---|---|
| District home position unusable, minute 110 (of 394 present districts) | water or lake 21, mountain 18, river 18, valuable deposit 12, off map 0, neighbour guard 0: **69 (18%)** (M); those districts take the nearest free position |
| Districts moved to another hex while present (whole games) | **23** in four games, against 585 claims (M); with river hexes allowed, 17 |
| Unhoused districts (no usable position left) | **0** at minutes 90 and 110 (M); 1 City at minute 110 when shelved builds also claim |
| Two settlements claiming the same tile | **0** (M); closest living pair of sites 6 (minutes 90 and 110), 7 at minute 60, so the guards never fired |
| Claimed hex inside another settlement's seven-hex core | 0 (M; impossible at spacing ≥ 5) |
| Camps standing on a claimed hex, minute 110 | 34 (M). Mining camps stand only on valuable deposits (never claimed), so these are seasonal camps (E; the split was not recorded) |
| Releases re-claimed within 8 seasons | 17 in four games (M) |

Allowing river hexes changes almost nothing (City yield −1.92% against −1.81% in the shelf-holding variant; 8 fewer moves) while putting
whole buildings on rivers the renderer cannot draw, so river hexes stay skipped. Preferring spare positions before other districts' homes
in pass 2 (variant "spare first") gave 20 moves against 25 in the same shelf-holding set-up: not worth the odd-looking placements (a castle
by the gate), so pass 2 stays purely angular.

### 5.5 How the shadow was measured

* Rule implementation: `outeraudit/Program.cs` in the session scratchpad: `Step` as `world_data.gd neighbour()`, Front as §1.3, positions
  as §1.3 (checked at distance 2 on every evaluation), usability as §1.5, two passes as §1.6, districts as §1.2 from `IronWorks`,
  `IronBuildKind`, `IronShelf` (variant only), `Civic.Ziggurats`, `Civic.BuildKind`, `Civic.Tier`, `Civic.PeopleLastYear`.
* Variants run on the same four games: main (as written, but shelved builds holding their hex), `shelf=0` (**the rule as written**,
  quoted in §0 and §5.2), `river=0`, `pref=spare`, `homes=0`. All 20 runs and the aggregation are in `outeraudit/runs/`.
* Not measured: the enforced rule's effect on food, growth or AI choices; frame and season-step time; Faith & Fief (no E5 games exist).

### 5.6 WP6a enforced-rule check

Command: `dotnet run --project src/CivSim.Harness -c Release -- core-audit --pack civsim.pack --live-only --seeds 4 --first-seed 1
--minutes 60,90,110 --outer`. It completed in 717 seconds on the WP6a branch. These are live results with claimed tiles enforced,
not the shadow estimate above.

| Minute | Settlements (Village/Band, Town, City) | Mean claims: Town / City | Claims added / released / moved since prior sample | Yield change | Other checks |
|---|---|---|---|---|---|
| 60 | No outer districts present | 0 / 0 | 0 / 0 / 0 | 0.00% | no duplicate, unusable, in-core claims or camps on claims |
| 90 | 315, 69, 39 | 0.30 / 2.03 | 115 / 14 / 1 | −0.02% | 0 duplicate, unusable or in-core claims; 9 seasonal camps on claims |
| 110 | 273, 53, 97 | 1.13 / 3.30 | 489 / 210 / 16 | −0.24% | 0 duplicate, unusable or in-core claims; 34 seasonal camps on claims |

At minute 110, 33 of 53 Towns and 92 of 97 Cities hold at least one outer hex; all 97 Cities have one or more homes hexes. The claim
distributions are 20/14/11/8 Towns with 0/1/2/3 claims and 5/11/17/15/26/14/7/2 Cities with 0 through 7. No Town or City is short of
free cultivable ground and no district is unhoused at this checkpoint. Best-k base field yield across 5,440 fields is 12,075 without
outer claims and 12,046 with them (−0.24%). The 90→110 yield effect is smaller than the pre-run shadow estimate (−0.50% overall);
this is an expected second-order difference because fields actually move and the economy and AI choices then diverge. The measured
one-season claim gate passes: plots present when a new outer claim is made are removed at the following `ReleaseCorePlots` boundary.
The 34 camps are camp records that happen to remain on claimed tiles at the sample; new seasonal-camp placement checks the same
`IsReservedCoreTile` rule before choosing a tile.

Verification: Release build (0 warnings/errors); `CIVSIM_SLOW=1 dotnet test CivSim.slnx -c Release --no-restore --verbosity quiet`
passed 1,177/1,177 in 8m41s, including all four seeds to Iron minute 110, determinism, ordering, skip, shelving, release and snapshot
tests. `validate --strict` passed (509 projects, 10 keystones, 33 lineages, zero warnings/errors). The toy, worldgen, 12-minute
research and 24-minute politics goldens remained unchanged and passed. The content-pack golden changed for the two homes thresholds;
the Bronze chapter golden changed because City homes and ziggurat claims release plots, and the updated Bronze golden passed twice
from fresh runs. Replay corpus regeneration remains outside WP6a.

## 6. Risks, goldens and open questions

### 6.1 Goldens and replays

| Golden | Expected | Why |
|---|---|---|
| `toy-v1` | unchanged, checked | no economy |
| `worldgen-v1` | unchanged, checked | world generation untouched |
| `research-v1` (12 min) | unchanged, checked | no City or Iron work by minute 12 |
| `politics-v1` (24 min) | unchanged, checked | no outer district appears by minute 24 |
| `bronze-v1` (Bronze chapter end, about minute 74) | **changed and checked** | Bronze City homes and ziggurat claims change plot release; checksum `f5879601ca18dca7` |
| `content-pack.txt` | **changed and checked** | two new homes thresholds; hash `c64e0118248f986d2a86aa6aa9f0818274b2ad05f431efafb179915c7b42c940` |
| Replay corpus | Not regenerated in WP6a | Bronze and Iron replays with Cities diverge; regeneration remains in WP7 |

### 6.2 Risks

1. **Food.** The pre-WP6a shadow estimated a 1.34% City and 0.50% overall best-k yield loss on top of WP3, and 5 of 139 Towns and
   Cities short of free cultivable hexes. The enforced minute-110 run measured −0.24% overall and zero short-ground cases. No ring-4
   fallback is indicated at this horizon; later eras and longer games still need watching.
2. **Seasonal camps on claimed hexes** (34 camp records at minute 110). New seasonal-camp placement checks `IsReservedCoreTile` (core
   and outer hexes); the sample still found camp records on claims. The renderer draws a camp on a claimed hex as a marker at the hex edge
   until that camp expires. The politics golden was unchanged at 24 minutes because no outer district had appeared by then.
3. **Performance.** `InReservedCore` and the ring-two guard use a derived per-tile list of sites within two hexes. The one-seed
   end-to-end 110-minute comparison was +0.9% wall time (139.14s vs 137.96s on base); see §5.6 for the limitation that the two runs
   follow slightly different paths.
4. **Moves look like jumps** (23 in four games): a building redrawn on a neighbouring hex when another district arrives. Accepted; storing
   arrival order (§2.1) is the fix if the owner minds.
5. **The picture rotates once** for settlements whose renderer Front came from a trail: WP6b switches them to the sim's static Front.
6. **E5 merge.** `codex/e5-fief` must append `CoreDistrict.Cathedral` (home p9, priority after Great Library) and classify its 11
   structures; the §4 registry test forces this.
7. **Shadow vs enforced.** §§5.2–5.4 are shadow results. The enforced four-seed check is now in §5.6; differences reflect fields
   moving and the subsequent economy and AI changes.

### 6.3 Owner decisions (confirmed 2026-11-10)

1. **Homes hexes for big Cities: approved.** The design gives a City one outer hex of houses at 120 people and a second at 180. They are 31% of
   all outer hexes a City takes and about a quarter of the field land the rule costs (E: −1.34% with them, about −1.0% without, for
   Cities). Without them the extra people are drawn as denser houses in the seven core hexes. They stay in the rule; the enforced run saw
   97 Cities with at least one homes hex.
2. **Gate side ignores roads: approved.** 31a let the gate face the nearest trail or road. Roads appear during a match, so the whole
   city would turn and every big building would change hex. The spec instead faces the sea, else the river, else a fixed side; roads
   still run to the gate but do not choose it.

Decided here without the owner (design autonomy, all reversible): river hexes are never built on; a set-aside build gives its hex back;
each district has a fixed home spot; seasonal camps keep off outer hexes; the hex table of §1.4.
