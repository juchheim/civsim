# 31. The settlement core: a reserved village heart, a slot map for what it shows, and what it costs

Status: plan (2026-11-10). Nothing here is implemented. Numbers marked (M) were measured on main at 4098f30 with a throwaway
read-only tool (§6.4); numbers marked (E) are estimates; numbers marked (P) are provisional proposals.

## 0. Verdict in plain words

**The owner's core idea is right, and it needs to be done in the simulation, not only in the drawing.** Today the game
really does put fields on the very hex where the village's fire burns (about half of all villages by the Iron Age have a field
on their own centre hex, and about a fifth of all fields sit on the centre or the six hexes around it). The renderer then has to
squeeze houses, walls and a field onto the same patch of ground. An earlier render-only fix (commit 153a842, "Reserve settlement
plots in renderer layout") already tried shrinking fields into little corner patches; that is what still looks crammed. No drawing
trick can fix a field that the rules say is under the houses. So: **reserve the centre hex and the six around it for the village
itself, for every settlement, at every stage, from the day it is founded.**

**Two parts of the proposal should change.**

1. **"These hexes produce nothing" becomes "nothing is planted, dug or built there except the village."** No fields, orchards,
   terraces, pastures, mines, quarries or camps on those seven hexes. The people still gather, hunt, fish and cut wood across the
   whole home range exactly as now; those are pooled, invisible amounts and never had a picture on the map. Making the core truly
   barren would remove about 17% of a village's wild food and 22% of its nearby forest (M), which is what made the extra ring look
   necessary in the first place.
2. **Do not add the extra ring.** With the change above the real loss is small: the fields that used to sit in the core move one or
   two hexes out and yield about **2.7% less** (M). The extra ring, by contrast, would grow every village's home range from 37 hexes
   to 61, add about **31% more wild food** to every settlement (M), and break the balance of all four eras that has been tuned on 37.
   Worse, to stop neighbours' ranges overlapping, villages would have to sit 9 hexes apart instead of 7, which cuts the number of
   villages a map can hold by **about 37%** (M). That is not a small concern: live Iron-Age games already reach **104 to 117
   settlements** by minute 80 to 120 (M), and the 9-apart rule caps a map at **about 85 to 109** (M). The map would run out of room.

**The visual side is accepted and extended.** Every possible village visual gets a fixed place: the centre hex is the village heart
(fire, banner, meeting ground, later the palace or ziggurat); each of the six hexes around it is a quarter with a job (homes,
workshops and stores, temple and civic buildings, the watch, the gate or waterfront). Things that are upgrades of each other show
only the best one: one wall line that grows from thorn hedge to palisade to stone wall, one food store that grows from pits to a
granary, one shrine that grows into a temple, houses that grow from lean-tos to tiled houses. When there is more than fits, items
merge or the least important are dropped; nothing spills out over the fields.

**The cheapest useful first step is renderer-only** (§9, WP1): build the slot layout and the upgrade chains against today's sim and
look at screenshots. That alone fixes most of "crammed together haphazardly". The simulation rule (no plots in the core) follows
once the owner likes the look, and costs about two weeks in all.

## 1. Root cause

There are two separate problems, one per half of the complaint.

### 1.1 "Crammed haphazardly": the renderer has no layout, only a spiral

`_settlements()` in `game/Renderer/LowPoly3D/Scripts/world_builder.gd` (lines 1108 to 1401) places a village as follows:

* Houses and small structures go on a sunflower spiral round the hearth, `r = 0.112 × VS × sqrt(slot + 3.2)` with `VS = 1.45`
  (lines 1227 to 1259), up to `DWELL_MAX_TOTAL = 36` dwellings plus up to four each of storage pits, drying racks, granaries, kilns,
  querns and totems (lines 1164 to 1173). With a tile 1.0 wide, slot 10 is already 0.59 from the hearth (outside the centre hex)
  and slot 36 is about 1.0, the middle of the next ring of hexes. The spiral ignores hex edges entirely.
* Decor (`_place_decor`, lines 1552 to 1614) is then put on another spiral just outside the houses. Decor comes from
  `game/Scripts/DecorTable.cs`: **19 props driven by what the civilization has learned, not by what this village built**. A civ that
  has learned DEF-03 shows a palisade ring round every own village whether or not one was built; totems, shrines and signal fires
  are drawn twice when also built as structures.
* Walls are separate rings at separate radii that **stack**: the thorn hedge at `rmax + 0.13` (line 1297), the knowledge palisade at
  `rmax + 0.2` outside it (line 1568), stone walls at `rmax + 0.26` or beyond the decor (line 1331). A village with a thorn barrier
  and stone walls shows both. The real `Palisade` and `Hillfort` structures are never drawn at all.
* Iron Age buildings (temple, ziggurat, academy, theatre, bathhouse, crane, press, well, Wonders, castle, forge yard, drill ground,
  goods, pacts, faith and province standards, aqueduct, water mill, shrine, markers) each search their own ring of candidate spots
  at growing radii (`_civic_site` rings up to 2.3 × offset, lines 1077 to 1105; castle at the wall ring plus 0.66 to 0.78, lines
  2166 to 2171; the Wonders' offset of 1.05 × 2.3 reaches 2.4, i.e. the second ring of hexes).

So a big Iron town's drawing spreads over the centre hex, all of ring 1 and into ring 2, in an order that depends on what was placed
first. Nothing groups related items, nothing says where the next thing goes, and nothing caps it.

### 1.2 "Shares hexes with fields and mines": the simulation puts them there

The simulation has no idea where houses stand: structures are counts (`Structure` enum, `src/CivSim.Sim/Economy/LivingEconomyModel.cs`
line 273). But plots are real tiles:

* `PlaceField` (`LivingEconomyModel.cs` line 3511) picks the best field tile anywhere in the 37-tile range, **including the site tile
  itself**. Orchards (`PlaceOrchard`, line 3477) skip only the site tile. Iron terraces (`NextTerraceTile`,
  `LivingEconomyModel.Iron.cs` line 2264) skip nothing.
* Deposits are worked "in range" as a settlement job (`LivingEconomyModel.Metal.cs`, e.g. lines 720, 2582, 2715), so a copper or
  iron deposit next to the hearth becomes a mine drawn among the houses (`_deposits()`, world_builder.gd line 2527, already shrinks
  them via `_deposit_visual_placement`).
* Site choice (`ChooseFissionSite`, line 4611, and `SiteFoodScore`, line 4728) scores only forage, herds and fish in the range. It
  never looks at deposits or farmland under the village. Found decrees (`PoliticsSystem.Decrees.cs` line 311), Beacons
  (`PreviewBeaconSite`, `LivingEconomyModel.Politics.cs` line 58), Tin Folk (`FolkSite`, `PoliticsSystem.Trade.cs` line 838, which
  puts the village **next to** its tin placer) and world-generation starts (`WorldGenerator.PlaceStarts`, which also seeds wild grain
  on the start tile and its neighbours, line 945) do not either.
* Pastures are counts only; the renderer picks a free ring-1 neighbour for the paddock (world_builder.gd line 1368).

Measured at the end of live Iron-rules matches (4 Warlord AIs, §6.4):

| Seed, minute | Settlements | Fields on the site tile | Fields in ring 1 | Share of all fields in the core | Orchards in ring 1 | Settlements with a metal, salt or coal deposit in the core |
|---|---|---|---|---|---|---|
| 1, min 100 | 105 | 53 | 237 | 23% | 108 of 655 | 23 |
| 1, min 120 | 104 | 64 | 251 | 23% | 182 of 1,046 | 23 |
| 2, min 100 | 117 | 53 | 237 | 18% | 201 of 974 | 22 |
| 2, min 120 | 117 | 68 | 266 | 20% | 307 of 1,564 | 22 |
| 3, min 80 | 93 | 33 | 175 | 25% | 41 of 191 | 29 |
| 3, min 100 | 111 | 55 | 265 | 23% | 115 of 676 | 36 |

Roughly every second settlement has a field on its own hearth tile, one field in five is in the core, and one settlement in five sits
within one hex of a metal, salt or coal deposit (M). In 12 world-generation seeds, 8 of 48 starting capitals have such a deposit in
their core (M).

### 1.3 What "growth stage" means today

There is no per-stage working area. The working range is a constant `Radius = 3` (37 tiles) for every settlement in every era
(`LivingEconomyModel.cs` line 214, `BuildRangeTiles` line 6147; docs/MVP/10 §3.1). What grows with stage is the **claim** (borders):
radius 3, 3, 4, 4, 5 in E0 to E4 (`content/registries/eras.yaml`), Town 5, City 6 (`ClaimRadius`, `LivingEconomyModel.Civic.cs`
line 1373). Stages are Band/Village (tier 0), Town (tier 1: TRD-04 and a Market and enough people) and City (tier 2: SOC-09 and a
Temple or Palace), `UpdateTier` line 1183. So "+1 ring at every stage" means "range 4 instead of 3, always".

## 2. Alternatives considered

| Option | Fixes crowding | Fixes field/mine on houses | Food and balance | Map capacity | Verdict |
|---|---|---|---|---|---|
| A. Render-only slot layout (fields drawn smaller, offset, or as a separate layer) | Yes | **No**: 20% of fields and many deposits are on core tiles; drawing them elsewhere lies about the sim, shrinking them is today's look | None | None | Do it first (WP1), but not enough alone |
| B. Owner's proposal as written: core of 7 produces nothing, range radius 4 | Yes | Yes | +31% wild food, +2% field yield, more forest and deposits in range; every per-range number (FoodBase 20, 9/14 fields, 15% pastures, 13 forest tiles, in-range mining) retuned for E0 to E4 | Spacing 7 → 9: **−37%** (M) | Rejected: breaks balance and the map |
| B'. As B but rescale per-tile yields by 37/54 | Yes | Yes | Neutral on paper, but every pool, stock, deposit count and the Python oracles change | −37% unless ranges may overlap, and then a radius-4 range reaches a Found neighbour's core at distance 5 | Rejected: huge churn for no gain |
| C. Reserve the centre hex only | Partly: 7 sub-slots cannot hold a town; houses already spill to ring 1 | Removes 1/4 of the problem (site-tile fields) | ~0 | None | Rejected: too small |
| D. Core that grows with the stage (centre only for bands, +ring 1 for towns) | Yes | Yes | ~0 | None | Rejected: fields and mines would be evicted mid-game when a village grows, and the planner would have to forecast it |
| **E. Reserve centre + ring 1 for every settlement from founding; no plots or workings there; pooled gathering unchanged; range stays 3; sites avoid burying valuable deposits** | Yes (with A) | Yes | Fields −2.7% (M), pools 0, from E2 on only (E0 and E1 have no plots) | −3.3% from the deposit rule (M), spacing unchanged | **Recommended** |
| E + ring-4 "outfields" for fields only | Yes | Yes | Fields +1.8% vs today (M) | None | Fallback lever only if the acceptance run shows a food gap (§7.3) |

Option E keeps the owner's core exactly (centre plus the six around it, always) and the owner's hierarchy and upgrade chains. It
swaps "produces nothing" for "holds no plots or workings" and drops the extra ring because the measured loss does not need it.

## 3. The new rules (simulation)

### 3.1 Definitions

* **Core** of a settlement: its site tile and the six tiles at hex distance 1. `CoreRadius = 1` (content constant
  `SettlementCoreRadius`, `content/constants/balance.yaml`, P). It is fixed for the life of the settlement, the same for Band,
  Village, Town and City, and is derived from `SiteTile` (no new saved state).
* **Range**: unchanged, radius 3 (37 tiles), core included, for everything pooled: forage, hunting, small game, fish, wood, surface
  stone, flint, clay and ochre, coppicing, herd following.
* **Plot-eligible tiles**: range tiles at distance 2 or 3 (30 tiles; fewer on coasts and mountains). Fields, orchards, Iron terraces,
  canals (which sit on fields) and the renderer's pasture paddocks may only use these.
* **Core water** is not reserved: water tiles in ring 1 stay fishable and carry the weir, the harbor and the boats.
* **Valuable deposit**: native copper, copper ore, arsenical copper, tin, iron, gold, silver, salt, coal (and later kinds such as
  saltpeter and sulfur, appended in E5). Stone, flint, clay and ochre are **not** valuable for this rule: they are pooled surface
  stock, they lie in 39% of all possible cores (M), and they have no working drawn in the village.
* **Clean core**: no valuable deposit on any core tile. Every new settlement must have a clean core.

### 3.2 Hex counts per stage

| Stage (era) | Claim radius (unchanged) | Range today | Plot-eligible today | Range proposed (E) | Reserved core | Plot-eligible proposed | Owner's version (B): range / core / workable |
|---|---|---|---|---|---|---|---|
| Band (E0, E1) | 3 | 37 | 37 (no plots exist yet) | 37 | 7 | 30 | 61 / 7 / 54 |
| Village (E2, E3) | 4 | 37 | 37 | 37 | 7 | 30 | 61 / 7 / 54 |
| Village (E4 Iron) | 5 | 37 | 37 | 37 | 7 | 30 | 61 / 7 / 54 |
| Town (tier 1) | 5 | 37 | 37 | 37 | 7 | 30 | 61 / 7 / 54 |
| City (tier 2) | 6 | 37 | 37 | 37 | 7 | 30 | 61 / 7 / 54 |
| Minimum spacing (fission/Beacon) | | 7 | | 7 | | | 9 |

Field limits stay as they are, counted on the full 37 (`FieldLimit` line 3533: 9 in E2, 14 with the ard, plus terraces;
`PastureLimit` 2, or 5 in E3). The 30 plot-eligible tiles average 22.6 to 26.3 cultivable tiles (M), against a limit of 14 fields;
orchards accept almost any land. 9 to 14 of about 105 settlements per seed have fewer than 14 cultivable plot-eligible tiles (M), but
almost exactly as many fall short with the extra ring too (13 vs 14), because they are coastal or mountain sites, so the extra ring
would not help them.

### 3.3 Rule changes, one by one

| Where | Change |
|---|---|
| `PlaceField`, `CanPlaceOrchard`, `PlaceOrchard`, `NextTerraceTile`, `NoFieldTileLeft` (Iron.cs 2276) | skip tiles with `Distance(site, tile) <= CoreRadius` |
| New `ReleaseCorePlots` at season start | releases any field, orchard or terrace inside a core and lets the normal planner re-place it; a no-op in new games, it cleans up saved games (§5.4). Ascending settlement id, ascending tile id |
| `ChooseFissionSite`, `PreviewBeaconSite`, Found validation (`PoliticsSystem.Decrees.cs` 311) and `Allows` (`PoliticsSystem.cs` 581) | reject a site without a clean core (new rejection reason `CoreDeposit` in `FissionSiteRejectionReason`, appended) |
| Same choosers | soft farmland term (P): score × (1 − `CoreFarmPenalty` × share of the site's best-9 field yield that lies in its core), precomputed per tile at start like `_siteFoodScores`. Start at `CoreFarmPenalty = 0.5` and keep it only if the WP4 gate shows it helps |
| `FolkSite` (Tin Folk) | the village stands at distance exactly 2 from its placer (lowest index on ties), never 1 |
| Mining camps | a camp may not open on a tile inside any settlement's core (cannot happen once cores are clean; asserted in tests) |
| World generation | after starts are chosen, a valuable deposit inside a start's core is moved (point-symmetric, like every other repair) to the nearest free ring-2 or ring-3 tile; the wild-grain repair (line 945) seeds ring 2 instead of the start tile and ring 1 |

Nothing else changes: claims, borders, supply, food pooling, demography, fission thresholds, combat and the defence values of each
structure are untouched.

## 4. The visual hierarchy and slot map (renderer)

### 4.1 Geometry

A tile is 1.0 wide (circumradius `R = 0.577`, `world_data.gd` line 8). The core is a seven-hex flower; its largest clear circle has
radius 1.15 and its outline is 18 hex edges. Each core hex is cut into **seven sub-slots**: `s0` at the hex centre and petals
`s1..s6` at 0.29 from it (petal *i* toward hex direction *i*). A small item (house, rack, quern, totem, lookout) fits in one sub-slot
with a footprint radius up to 0.14 (the village scale `VS` drops from 1.45 to about 1.25 for core items). A **half-hex** item
(granary, kiln with potter's workshop, forge yard, drill ground, chief's hall, market) takes `s0` and two adjacent petals. A
**whole-hex** building (temple, ziggurat, palace, academy, theatre, bathhouse, castle, a Wonder) takes all seven sub-slots with a
footprint radius up to 0.45; the Wonders (today 0.6 to 0.7) are drawn at about ×0.7. The **perimeter** runs along the outer edges of
the hexes in use, about 0.06 inside the hex boundary.

### 4.2 Quarters

The **front** direction F is the first of: a ring-1 water hex (river first); else the direction of the nearest road or trail; else
`tile % 6`. Quarters are named by their direction relative to F:

| Hex | Quarter | Holds (in priority order) |
|---|---|---|
| Centre | **Heart** | `s0`: hearth and banner (always). Band: shelters on the petals. Village: council ring (GOV-01), sky-pole, totem. Town: market stalls (TRD-04 Market; not drawn today). City: one landmark behind the hearth on three petals: ziggurat, else palace (GOV-05; not drawn today), else chief's hall (SOC-07) |
| F | **Front** (gate) | the gate in the perimeter; on a water front the weir, then harbor quay, boats and lighthouse approach; on land a way-cairn, the well (windlass well), the aqueduct's water tower, the caravanserai (not drawn today) |
| F+1 | **Homes A** | houses (one per sub-slot; clusters in Town and City) |
| F−1 | **Homes B** | houses; the first hex given up when a civic building needs room |
| F+2 | **Workshops and stores** | granary (or storage pits), drying racks, quern, kiln and potter's workshop, smelter or furnace, forge yard (Iron), bakery (not drawn today), goods stacks (barrels, planks, glass), crane and press |
| F−2 | **Watch and arms** | lookout, signal post, spear rack, chariot, drill ground with soldiers; the castle takes the whole hex when built |
| F+3 | **Sacred and civic** | totem, then shrine, then temple (with colonnade), graves and cairn as dressing; first whole-hex civic building (academy, library, theatre, bathhouse) or a Wonder |

When a ring-1 hex is water, mountain or off the map, its quarter moves to the next free hex in the order Homes A, Workshops, Sacred,
Homes B, Watch, Front; a quarter that finds no hex merges into its nearest neighbour (Homes into Heart, Watch into the perimeter as
towers, Front into the gate).

### 4.3 Filling by stage

| Stage | Hexes drawn | Perimeter traced along | Houses shown |
|---|---|---|---|
| Band (no permanent house, under 20 people) | Heart only | the centre hex (6 edges) | up to 6 shelters on the Heart petals |
| Village | Heart, Homes A, Workshops, Sacred; the other three are open ground | the full core (18 edges), so a wall never has to be redrawn bigger | up to 7 + 3 |
| Town | all seven | full core | up to 14, then clusters of 2 |
| City | all seven, denser and taller meshes | full core, taller wall, more towers | up to 14 clusters of 3 |

Houses shown = the population divided by the capacity of the best house type, capped by the slots above; the rest are implied by
clusters. This replaces `DWELL_MAX_TOTAL = 36`.

### 4.4 Overflow rules

Applied in this order whenever a quarter has more than it can hold:

1. **Upgrade-replace**: only the highest built tier of a chain is drawn (§4.5).
2. **Merge**: several of one kind become one bigger or cluster mesh (four drying racks become one rack yard; three granaries one big
   granary). The exact count stays in the settlement panel.
3. **Compress homes**: houses go to clusters, freeing Homes B for a whole-hex civic building, then Front's land petals.
4. **Drop dressing**: the knowledge props (§4.6), then the lowest-priority small items.

Never: draw a settlement item outside the core, except the outer works in §4.7; never draw over a plot tile.

Priority, highest first: hearth and banner; perimeter; Wonder; castle; Heart landmark; temple; at least one house per Homes
quarter; granary or pits; civic works (academy, library, theatre, bathhouse); forge yard and kiln; market; lookout and signal; goods;
dressing.

Whole-hex budget: five (Sacred, Homes B, Watch, Heart landmark, and Front on a land front). A late Iron city with a temple, a
ziggurat, a castle, a Wonder and three civic works has more than five; the lowest-priority ones are drawn at half size on three
petals in the Workshops hex, and anything beyond that is listed in the panel only.

### 4.5 Upgrade-replace chains

| Chain | Tiers, lowest to highest (only the highest built is drawn) | Note |
|---|---|---|
| **Perimeter** | none → thorn hedge (`ThornBarrier`) → palisade (`Palisade` structure) → stone wall I → stone wall II with towers (`StoneWalls` = 2) → stone wall III, high (= 3); the gatehouse (DEF-06) replaces the gate | Visual only: the sim keeps each structure's own defence value. The knowledge palisade (DEF-03 decor) is no longer drawn |
| Shelter | lean-to → hide tent → pit house → wattle → mudbrick → tiled roof (CER-08); longhouse and hall as the large-house variant | already half done by `_dwellings` (line 1471) |
| Food store | storage pits → granary | pits vanish when a granary stands |
| Sacred | totem → shrine → temple → temple with colonnade (STN-12) | the pilgrimage shrine (BEL-05) moves from outside the village into the Sacred quarter |
| Council | council ring (GOV-01) → chief's hall (SOC-07) → palace (GOV-05) | |
| Watch | lookout → lookout with signal brazier (COM-04) → wall tower once walled | signal post merges into the lookout |
| Pottery | kiln → kiln with potter's workshop (CER-04) | one yard |
| Metal | furnace (MET-01/02 decor, stands in for the undrawn smelter) → forge yard (MET-09) | |
| Water | weir → harbor quay (SEA-07) | the weir merges into the quay |
| Grinding | quern → water mill (NRG-03, outer work on the river) | quern stays as dressing |

### 4.6 Every possible centre visual and where it goes

Sources: the 61 structure ids in `content/registries/structures.yaml`, the renderer's `Kind` enum (`world_data.gd` line 14), Iron
works (`iron_work_ids`), `DecorTable.cs` (19 knowledge props) and civ looks.

| Item | Source | Size | Slot |
|---|---|---|---|
| Hearth, flame, banner | `hearth` | small | Heart `s0` |
| Lean-to, tent, hide tent, pit house, wattle, mudbrick, longhouse, hall | dwellings | small (longhouse, hall: half) | Heart petals (Band), Homes A/B |
| Storage pit, granary | structures | small / half | Workshops |
| Drying rack, quern, kiln, potter's workshop | structures | small / half | Workshops |
| Bakery, smelter, forge | registry (not drawn today) | small | Workshops (optional new meshes) |
| Forge yard, goods (barrels, planks, glass), crane, press | Iron | half / small | Workshops |
| Totem, shrine, temple, temple_tier2, ziggurat | structures, civic | small → whole | Sacred (ziggurat: Heart in a City, else Sacred) |
| Council, sky-pole, chief's hall, palace, market | decor, looks, civic | small → whole | Heart |
| School, academy, great library, theatre_arena, bathhouse, great dome | civic, Iron | whole | Sacred, then Homes B, then Front |
| Lookout, signal post, spear rack, chariot, drill ground, castle | structures, decor, looks, Iron | small → whole | Watch |
| Thorn barrier, palisade, stone walls, gatehouse | structures, Iron | perimeter | Perimeter (one chain) |
| Weir, harbor, boats | structures, Iron | edge | Front (water) |
| Windlass well, aqueduct tower, caravanserai, way-cairn | Iron, registry, decor | small | Front (land) |
| Pacts, faith standards, province standard | Iron | small | Heart petals (flags on the meeting ground) |
| Knowledge dressing: hide frame, pots, loom, sledge, bricks, baskets, woodwork, wool bales | `DecorTable`, looks | dressing | next to their host: houses, kiln, workshops, stores; one each per village |
| Knowledge props that duplicate built structures: totem, shrine, signal, palisade | `DecorTable` | none | **dropped**: only the built structure is drawn |
| Grave, cairn | decor | dressing | Sacred petal |
| Build site of an Iron work under way | Iron | as the finished work | the slot the finished work will take |

### 4.7 Outer works (allowed outside the core)

These belong to the land, not the village, and are drawn in rings 2 and beyond on tiles that hold no plot: pasture paddocks (a
ring-2 tile, grassland first), the water mill (on the river, as now), the hillfort (on its hill tile, `HasHillfortSite`, never drawn
today), quarries and charcoal clamps (on a stone or forest tile in range), mines and mining camps (on their deposits), markers
(on seasonal camps), the megalith (STN-10, ring 2 or 3), frontier walls, siege camps, relay posts, the great lighthouse (on the
shore), bridges and roads, and the aqueduct's spans (a line feature that may cross ring 2).

### 4.8 What changes meaning for the player

* Fields and orchards are never next to the hearth; pastures appear one ring further out.
* A palisade appears only when the village builds one (not when the civilization learns DEF-03), and so on for the other
  duplicated knowledge props. Some villages will look less defended than before; they always were.
* The thorn hedge disappears once a palisade or wall stands (it still counts for defence).
* The hillfort, market, palace and caravanserai get pictures (optional meshes, WP6).
* Tin Folk villages stand two hexes from their tin, not one.

## 5. Simulation impact

### 5.1 Files

| Part | File | Change |
|---|---|---|
| Core helper | `LivingEconomyModel.cs` | `IsCore(SettlementState, tile)`; `_coreClean` bool array per tile, built once at start next to `_siteFoodScores` (line 618) |
| Plots | `LivingEconomyModel.cs`, `.Iron.cs`, `.Food.cs` | §3.3; `CanalCandidates` needs nothing (canals live on fields) |
| Cleanup | `LivingEconomyModel.cs` season start | `ReleaseCorePlots` |
| Siting | `LivingEconomyModel.cs` (fission), `.Politics.cs` (Beacon preview), `Politics/PoliticsSystem.Decrees.cs` (Found), `PoliticsSystem.cs` (`Allows`), `PoliticsSystem.Trade.cs` (Tin Folk), `FissionSiteDiagnostics.cs` (new reason, appended) | §3.3 |
| World generation | `World/WorldGenerator.cs` | start-core deposit relocation; wild grain at ring 2 |
| Content | `content/constants/balance.yaml` | `SettlementCoreRadius: 1`, `CoreFarmPenalty: 0.5` (P) |
| View | `View/FrameBuilder*.cs` | nothing new is required; optional `SettlementCoreClean` byte for the Beacon refusal text |
| UI | Beacon preview and Found refusal text | "would cover a known copper deposit" or, for an undiscovered one, "poor ground for a village" |

### 5.2 Determinism and architecture

All new logic is integer hex distance and boolean arrays: no floats, no `Dictionary`/`HashSet`, ascending id iteration, no new
random streams, so D1 to D10 hold and `SourceRulesTests` needs no allowances. No `CommandKind` changes. `CivSim.Sim` gains no
references. Fission tie-breaking (`PreferFissionTieCandidate`) is unchanged, so the mirrored-map fairness property holds; the
world-generation repair is point-symmetric like the existing ones.

### 5.3 Goldens, replays, corpus

| Golden | Changes? | Why |
|---|---|---|
| `toy-v1.txt` | No | toy model |
| `worldgen-v1.txt` | Yes, if seed 42's starts have a valuable deposit in a core or the wild-grain repair fires | start repairs |
| `content-pack.txt` | Yes | two new constants |
| `research-v1.txt` (12 min) | Likely (via world generation and early fission sites) | |
| `politics-v1.txt` (24 min) | Yes | fission sites |
| `bronze-v1.txt` | Yes | fields, sites |

Regenerate each deliberately and say why in the commit message (CLAUDE.md rule 5). Every replay in the regression corpus stops
verifying and must be re-recorded; any replay that was evidence for a bug should be re-checked for the bug, not just re-recorded.

### 5.4 Saves

The snapshot format does not change (the core is derived from `SiteTile`), so `SnapshotVersion = 30` can stay. A save from before
the change loads; at its next season start `ReleaseCorePlots` moves its core fields and orchards out. Its existing settlements that
sit on a valuable deposit are **grandfathered**: the deposit is still worked in range, and the renderer draws the working as a
small pit in the Workshops quarter. No new settlement can be in that position.

### 5.5 AI and forecasts

The Strategist's fission and Found forecasts call the same choosers, so they see the new legality without changes. `SiteFood` and
`FieldLimit` are unchanged, so food forecasts move only by the small field-yield effect. `ForecastFidelity` tests must be rerun.

### 5.6 Python oracles

| Tool | Update |
|---|---|
| `check_e3.py`, `check_e0e2.py`, `params.py` | keep `RANGE_TILES = 37` for pools; add `PLOT_TILES = 30` as a ceiling on field and pasture tiles; a field-yield factor 0.973 (M) |
| `territory_sim.py`, `territory_sweeps.py` | add the clean-core filter to the fission site rule (line 588) and confirm the claimed-land curves move by about 3% or less |
| `metal_sim.py`, `e4_*` oracles | none (forest and deposits in range unchanged) |

## 6. Measurements

### 6.1 Balance: what the core costs

| Quantity | Proposed (E) | Owner's (B) | Strict barren core, radius 3 |
|---|---|---|---|
| Pooled forage and fish growth in range | 0% | **+31.5%** (M) | −16.7% (M) |
| Forest tiles in range | 0% | about +45% (E) | −21.7% (M) |
| Best-k field yield (k = fields each settlement holds) | **−2.7%** (M) | +1.8% (M) | −2.7% |
| FoodBase of a band (10 §2, ≈ 20 people) | 20 | ≈ 26 (E) | ≈ 16.7 (E) |
| Field limit (E2 / ard) | 9 / 14 | 15 / 24 if left proportional | 9 / 14 |

The field loss applies only from E2 (no plots exist before), and fields are only part of a village's food, so the total food effect
is under 2% (E). No tuning constant needs to change; if the WP5 acceptance run shows a gap, the lever is the ring-4 outfields option
(§7.3), justified in docs/MVP/07c (C1) and 07d (R4).

### 6.2 Map capacity (risk c)

Maximum settlements by greedy packing of habitable tiles (land other than coast, ice and peaks, the rule of `IsHabitable`), 12
standard 160 × 100 maps (M). Greedy packing is a lower bound on the true maximum, so these are conservative:

| Rule | Tile order | Food-first order (as the chooser does) |
|---|---|---|
| Spacing 7 (today) | 170.7 | 136.1 |
| Spacing 7 + clean core (proposed) | | 131.6 (−3.3%) |
| Spacing 9 (owner's ring, non-overlapping) | 109.2 (−36%) | 84.8 (−38%) |
| Spacing 9 + clean core | | 83.0 |

Live Iron-rules matches (4 Warlord AIs) reach 104 to 105 (seed 1, min 100 to 120), 117 (seed 2, min 100 to 120) and 111 (seed 3,
min 100) settlements (M); Found decrees already allow own settlements 5 or 6 apart (`FoundOwnMinDist`, `balance.yaml` line 86). So a
spacing of 9 would bind in real games; the clean-core rule would not.

### 6.3 Other findings

* Starts with a valuable deposit in the core: 8 of 48 (12 seeds); in the range: 32 of 48 (M).
* Habitable tiles whose core holds a valuable deposit: 18.7%; a stone, flint or clay deposit: 38.6% (M).
* A radius-4 range would reach a Found neighbour's core at distance 5; the radius-3 range never does (5 − 1 = 4 > 3).

### 6.4 How it was measured

A throwaway console program outside the repo, referencing the Release `CivSim.Sim.dll` built from 4098f30 and the local
`civsim.pack`, in the session scratchpad (`coremeasure/Program.cs`). Modes: `static N` (world generation only: greedy packing at
spacing 7 and 9 in tile order and in radius-3 forage order, deposits in cores); `live N minutes` (Iron rules, Builder, Grower,
Warlord and Sage AIs at Warlord level, omniscient frame: plots by ring, deposits in cores, cultivable tiles); `yield N minute`
(best-k field yield per region using `FieldYieldPerYear`; pooled forage and fish growth per region). Not verified: the renderer was
not run and no screenshots were taken; yield numbers ignore soil depletion, irrigation, terraces and competition between
neighbours; only 3 live seeds were run (the field-yield and pool numbers come from seeds 1 to 3 at minute 80, 282 settlements and
2,783 fields). WP0 turns this into a harness command so the numbers can be
re-run.

## 7. Site selection

### 7.1 New settlements

Hard rule: a clean core (§3.1), for every way a settlement appears (fission, Beacon, Found, Tin Folk, world generation). The
founders are assumed to see the outcrop, so the rule uses all deposits, discovered or not. The player-facing refusal names only
deposits the player knows; otherwise it says the ground is poor for a village. Soft rule: the farmland term in §3.3, kept only if it
measurably reduces the field-yield loss without moving settlement counts.

### 7.2 Existing settlements and saves

Grandfathered (§5.4). In new games, after world generation the only way to have a valuable deposit in a core is a save from before
the change.

### 7.3 Fallback lever (only if needed)

Let fields (not pools, not mining) use ring 4 as well: plot-eligible becomes 54 tiles, field yield +1.8% versus today (M), no
spacing change. Because Found settlements may stand 5 apart, a ring-4 tile can lie in a neighbour's core, so the plot test must
check the cores of all settlements, not only its own. Not recommended up front.

## 8. Interactions with work in progress

| Work | State (checked 2026-11-10) | Overlap | Order |
|---|---|---|---|
| M11 Iron (E4) | merged on main (`codex/e4-iron` has nothing ahead); Iron acceptance rerun, castles, dead unlocks and the Bronze tail are open | Castle placement and the Iron works are in the slot map; the acceptance rerun must be repeated after WP3 to WP4 | WP1 and WP2 (renderer) any time; WP3 onward after the Iron acceptance rerun lands, or rerun it as WP5 |
| E5 Faith & Fief (doc 27, `codex/e5-fief`, 4 commits, paused) | touches `LivingEconomyModel.cs`; WP-E5-0 is a "wood/stone range fix" | E5 will add cathedrals, mills, keeps: reserve capacity in Sacred, Workshops and Watch; the range fix must keep pools on the 37-tile range and plots out of the core | land this plan's WP3 first or agree the range fix with it |
| `codex/b406-holder-sharing-v3` | 28 commits ahead, touches `LivingEconomyModel.cs` | merge conflicts only | rebase whichever lands second |
| 153a842 settlement-sprawl (merged) | renderer plot squeeze | replaced by the slot layout in WP6 | |
| Settlement map labels (merged) | label above the hearth | none | |
| Your people sheet (doc your-people-sheet-proposal) | field counts in the Stores and Work tabs | none | |
| Docs 29 (dead unlocks) and 30 (build agency) | built-structure counts | visuals for totem, marker, shrine, signal post, potter's workshop are in the slot map | |

## 9. Work packages

Cheap, reversible steps first, with a decision point after WP1.

| WP | Contents | Exit gate | Effort (E) | Rollback |
|---|---|---|---|---|
| **WP0** Core audit command | `civsim-harness core-audit --pack F --seeds N --minutes a,b,c` reporting §1.2 and §6 (plots by ring, deposits in cores, packing at 7 and 9, best-k yield). Harness only, no sim change | Reproduces §6.2's 136.1 / 84.8 on seeds 1 to 12 and §1.2's seed-1 row | 0.5 day | delete the command |
| **WP1** Renderer slot layout (prototype) | §4.1 to §4.4 behind `--core-layout`, against today's sim (core fields drawn as small garden cues); capture scenario `core-gallery`: one settlement per stage (Band, Village, Town, City, walled Iron city with castle and Wonder, coastal town) | Screenshots per stage; a `--layout-dump` JSON lists every settlement prop with its hex and sub-slot, and a checker finds no prop centre outside the core except §4.7 items and no two footprints overlapping | 3 to 4 days | flag off |
| **WP2** Upgrade chains and decor | §4.5, §4.6 (drop duplicated knowledge props, dressing to hosts, one perimeter) | The dump shows at most one perimeter tier and at most one tier of each chain per settlement; existing renderer captures still build | 1 to 2 days | flag off |
| **Decision** | Owner reviews WP1 and WP2 captures | Go or no-go for the sim rule | | |
| **WP3** Sim: plot-free core | §3.3 plot rows, `ReleaseCorePlots`, constant `SettlementCoreRadius`, tests | New test: over 4 seeds × Iron 110 min, zero fields, orchards and terraces within distance 1 of any living settlement; `dotnet test` green; content `validate --strict` clean; goldens regenerated with reasons | 2 days | revert one commit |
| **WP4** Sim: clean-core siting | §3.3 siting rows, Tin Folk, world-generation relocation and wild grain, refusal text | Test: no settlement founded after tick 0 has a valuable deposit in its core (4 seeds × Iron 110 min); `worldgen --audit-seeds 200` passes acceptance with no new failures; start cores clean on all 200 | 2 to 3 days | revert; worldgen golden back |
| **WP5** Balance check | iron-match, `politics --check-m10`, research and Bronze acceptance, Python oracles §5.6 | Against the baseline on the same 8 seeds: settlements at min 60/100 ≥ 95%, people and food security at min 60/90 within ±5%, field yield loss ≤ 3%, Iron acceptance as before; else apply §7.3 and repeat | 2 to 3 days | revert WP3 to WP4 |
| **WP6** Renderer final | slot layout on by default; remove the spiral, `_reserve_plot_visual` squeeze and DWELL_MAX_TOTAL; pastures on ring 2; hillfort, market, palace meshes (optional) | `core-gallery` captures; layout checker in the capture run; frame-time not worse than today on the `deposits` and Iron scenarios | 2 to 3 days | flag back off |
| **WP7** Records | re-record the replay corpus; update docs 10 §3.1, 07c C1, 12 §9, 22 §4.3 and §5 (Tin Folk), 23 §mining, 18, 19 | corpus verifies; docs list the core rule | 1 day | |

Total: about 14 to 19 working days (E), of which the first 4 to 6 (WP0 to WP2) need no simulation change.

### 9.1 Risks

* **Iron balance is still being tuned.** Every sim change moves the goldens and the acceptance runs. Mitigation: renderer first;
  sim after the Iron acceptance rerun or with it.
* **World-generation repairs** could make some seeds fail fairness acceptance. Mitigation: the 200-seed audit in WP4; fall back to
  relocating the deposit instead of moving starts (already the plan).
* **Coastal and mountain villages** lose quarters. Mitigation: quarter reassignment (§4.2) and merge rules; the coastal case is in
  the capture set.
* **Players notice missing props** (knowledge palisade). Mitigation: the settlement panel lists what is known and what is built.
* **Information leak**: AIs never settle on hidden deposits. Accepted (§7.1); the refusal text does not name hidden deposits.

## 10. Questions for the owner

1. **Keep gathering in the core?** The seven middle hexes hold only the village, but the people still forage, hunt, fish and cut wood
   across the whole home range, as now. *Recommended: yes.* The alternative (a truly barren core) costs 17% of wild food and needs
   the extra ring.
2. **Drop the extra ring?** *Recommended: yes.* Measured loss without it is 2.7% of field yield; the ring would cost about a third of
   the map's room for villages and a full rebalance. Keep "fields may use ring 4" as a fallback only.
3. **Should new villages avoid deposits nobody has found yet?** *Recommended: yes*, so a later discovery is never buried under a
   village. The game will only name deposits the player knows when it refuses a spot.
4. **Show only what a village actually built?** Props that today appear because the civilization learned something (the palisade
   ring after DEF-03, a second shrine, a second signal fire) would go. *Recommended: yes.*
5. **Walls as one evolving line, in the picture only?** The thorn hedge, palisade and stone wall would show as one wall that
   upgrades, while the rules keep counting each for defence as now. *Recommended: yes*; changing the defence rules is a separate
   decision.
6. **Old saves**: they keep loading; their fields next to the hearth move out at the next season, and any village already sitting
   on a metal deposit keeps mining it. *Recommended: accept.*
