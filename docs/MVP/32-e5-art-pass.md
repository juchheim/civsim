# 32 — Faith & Fief (E5): the art pass

Status: draft spec, 2026-11-13, owner decisions on all seven open questions applied the same day (§12); no open questions remain. Nothing here is built. The renderer has no E5 art today. Numbers marked (P) are proposals to tune in
the dev gallery; the content they depend on is itself draft (doc 27, typed on `codex/e5-fief`).

Sources: [27](27-faith-and-fief-age.md) (design, §8 view layer), the typed E5 rows in `content/catalog` and `content/registries` on
`codex/e5-fief` (worktree `civsim-e5`), [31a](31a-staged-core-and-building-budget.md) (where buildings may stand),
[08 §13](08-ui-and-world-display.md) (generic kit), and the existing kit in `game/Renderer/LowPoly3D/Scripts/art_*.gd`.

## 0. In plain words

Faith & Fief is read on the map from a few big silhouettes: **windmills on ridges, monasteries in the countryside, a cathedral
skyline, a double-walled castle, smoking blast furnaces, cogs on the sea lanes**, with **three-field strips** on the farmland. Most
other projects are rules and numbers, and get a badge and a card, not a model.

This spec lists everything that needs a picture, says how big and where it stands, what states it has, and what the simulation must
tell the renderer. It follows three rules:

1. **Draw only what the sim has.** A picture appears when a settlement has the building, the figure holds the item, or the route is
   really traveled. No decorative E5 that the sim does not back.
2. **Show idle.** Every staffed institution shows staffed, queued or dormant (doc 27 F-T6). An empty monastery must look empty.
3. **Stay inside the city budget.** Doc 31a's rule stands: a new building upgrades an older one, joins a district, or goes outside the
   walls. No new whole-hex building except a Wonder.

## 1. Scope and priority

| Tier | Meaning | Items |
|---|---|---|
| **P0** | Carries the identity of the age. Needed before any E5 playtest | windmill, monastery, blast furnace, concentric castle, Gothic Cathedral, three-field strips, cog, institution states (§8.1) |
| **P1** | Needed for the systems to be legible | university, hospital, clock tower, sawmill, powder mill, print shop, knight, gunner, monk, cannon and broken-wall meshes, pilgrimage line, outbreak and quarantine overlay, rival KEY-6 banner |
| **P2** | Polish, mostly signs and small props | manor house, game reserve, drained wetland, spectacles, goods on carts, master mason, crossbow levy, portolan reveal, dynastic marriage marker |
| **Card only** | No map object. Badge, Chronicle line and a card | MEC-07 crank, NRG-04 horse collar (beyond horses at the plough), HUS-10, TRD-08, TRD-09, NUM-08, NUM-09, GOV-10, GOV-11 (beyond a banner), SOC-11, FIR-10, MIN-08 numbers, INQ-06, CHM-06, MET-11 |

## 2. Kit conventions (match the existing scripts)

- One script per family, `game/Renderer/LowPoly3D/Scripts/art_<family>.gd`, `extends RefCounted`, with
  `static func build(kit) -> void` that fills `kit.meshes["<name>"]`. New files: `art_fief_power.gd` (mills, furnace, sawmill, powder),
  `art_fief_faith.gd` (monastery, university, hospital, cathedral, clock tower, print shop), `art_fief_war.gd` (concentric castle,
  gunners, knights, cannon), `art_fief_sea.gd` (cog). Land and rigs follow `farm_rig.gd`.
- Units: **1.0 = one tile column; an adult is about 0.1 tall.** Buildings face **+Z** toward the village.
- Material tags: `TAG_CIV` faces take the owner's colour, `TAG_PLAIN` fixed colours, `TAG_FOLIAGE`, `TAG_CROP`, `TAG_CLOTH`,
  `TAG_EMISSIVE` (fire, lanterns, glass). Reuse the existing palette constants (STONE, ROOF, TIMBER...) so E5 sits in the same world.
  Add only: **SLATE** (Gothic roofs, cooler than ROOF), **COAL_SMOKE** (furnace and powder haze), **PARCHMENT**, **GLASS_BLUE**
  (stained glass, emissive when the cathedral stands finished).
- Staged builds: sites use `build_site_*` and the stage meshes pattern of `castle_1..4`. Wonders and the concentric castle get 4 stages.
- Era type sign (08 §13): E5 structures carry a small **type sign** on the roof or gable, one glyph per type, so a player can tell a
  hospital from a university at far zoom. Glyphs: cross-and-bowl (hospital), open book (university), gear (mills, furnace), bell (clock
  tower), flame (powder mill), type-block (print shop), cross (monastery).
- Registration: each new mesh name goes into `world_builder.gd` through a `FIEF_WORKS` table shaped like `CIVIC` (mesh, scale, offset
  from the hearth, half-footprint, footprint radius), so placement, avoidance and site drawing reuse the Iron code paths.

## 3. Placement (from doc 31a, already decided)

| Item | Size class | Chain or district | Where it stands |
|---|---|---|---|
| University | whole | **Learning** (academy → university, drawn as one, best tier only) | the Learning hex, beside Sacred |
| Hospital | half | *Health* (shares the hex with the bathhouse) | Health hex, toward the Front |
| Clock tower | small | **Market** | the market square's landmark |
| Print shop | small | *Workshops* | Workshops quarter |
| Concentric castle | whole | **Citadel** (castle → concentric castle) | ring-2 hex on the wall line; replaces the castle mesh |
| Gothic Cathedral | whole | *Wonder* (own hex, owner decision 2026-11-10) | straight behind the Heart, then either side |
| Monastery | outer | none | rings 2 to 3, **always outside the wall, in a Town as well as a City** (decided), on a tile with no plot, near wilderness |
| Windmill | outer | **Mill** | ridge or open tile; one mill site per river, wind sites elsewhere |
| Water sawmill | outer | **Mill** | on the river beside the water mill |
| Blast furnace | outer | *Industry* | outer ring, downwind side, a tile with no plot |
| Powder mill | outer | *Industry* | **apart from houses** (doc 27); farther out than the furnace |
| Manor house | outer | none | on an estate in the farmland, not in town |

The "best tier only" rule means the academy mesh is not drawn once a university stands; the university mesh must therefore read as
the academy's upgrade (same hex, same orientation, larger).

## 4. Buildings (specs)

Each entry gives the silhouette, the parts, the states, and a reuse note. Footprints and scales are (P).

### 4.1 Windmill (NRG-05, P0)
- **Silhouette:** a tapered tower with four sails, tall against the skyline. Post-mill reads at distance; tower-mill is easier to build.
  Use a **tower mill**: a round stone base, timber cap, four lattice sails.
- **Parts:** base (stone), cap (timber, roof), hub, sails (4 arms with cloth lattice, `TAG_CLOTH`), door, small stair, a sack stack.
- **Animation:** sails turn when the mill has a staffed job and wind. Speed scales slowly, one revolution per 6 s at normal speed.
  Stopped sails mean idle. A drainage pump job adds a small stream of water from a pipe at the base.
- **States:** site (timber frame), built-working, built-idle (sails stopped and furled), none.
- **Base reuse (decided):** built on the `water_mill` mesh family: the same millstone house, door, sack stack, site and avoidance code,
  and the same footprint class. It must still read as a different building at overview zoom (see **Unique read** below).
- **Unique read:** the water mill's signature is a wheel in a stream. The windmill replaces the wheel and flume with **a raised
  round tower on a stone plinth, a rotating cap and four large cloth sails**, and it has **no water at all**. Its silhouette is
  taller and narrower (height ≈ 2× width), and its colour mass is a pale lime-washed tower with a dark cap, against the water mill's
  timber and wheel.
- **Footprint (P):** radius 0.16, height 0.30. Placement prefers high ground and open tiles; never on a river tile (that is the water mill's).

### 4.2 Water-powered sawmill (WOD-09, P1)
- **Silhouette:** a long shed over the water with a wheel at one end and a stack of planks and logs.
- **Parts:** wheel (reuse `water_mill` wheel), shed with open sides, a carriage with a saw frame, a log ramp, plank stack.
- **Animation:** the saw frame moves up and down while a log is on the carriage and the wheel turns. Idle: wheel locked, no logs.
- **Base reuse (decided):** built on the `water_mill` mesh family (the wheel, flume and bank), so it sits naturally on the same river.
- **Unique read:** the water mill is a compact millhouse. The sawmill is **a long, low, open-sided shed running along the river**, with
  a visible **reciprocating saw frame**, a **log ramp climbing from the water**, a **plank stack** and **log pile**, and a pale
  fresh-cut-wood colour mass. At overview zoom it reads as a long horizontal shape with stacked timber, never as a second millhouse.
- **Footprint (P):** 0.22 × 0.12. Stands in the Mill chain next to the water mill; if both exist, the water mill is drawn beside it.

### 4.3 Blast furnace (MET-12, P0)
- **Silhouette:** a tall tapering stone stack with a smoking top, a bellows wheel and a casting shed.
- **Parts:** stack (stone, `TAG_PLAIN`), brick crown, tuyere arch with an emissive glow at the base, bellows house with a small wheel,
  charcoal heap, pig-iron bed in front.
- **Effects:** a smoke column (COAL_SMOKE) rising while it runs, glow at the arch (`TAG_EMISSIVE`), a ground scar (bare earth ring) where the
  charcoal burners cleared forest. This ties to doc 27's charcoal crunch: **the clearing and the charcoal heap size show fuel pressure.**
- **States:** site, burning, banked (cold, no smoke: fuel-short or idle), none.
- **Footprint (P):** radius 0.2, height 0.34.

### 4.4 Powder mill (CHM-07, P1)
- **Silhouette:** a low timber shed with a heavy stone-and-earth berm round it, no chimney.
- **Parts:** shed with a light roof, berm on three sides (an explosion lane toward open ground), stamp mill wheel on the leat, barrels.
- **States:** working (small stamp motion), idle, none. A **spark warning glyph** at overview zoom only while it holds powder.
- **Footprint (P):** radius 0.15. Never within 0.45 of any house footprint.

### 4.5 Monastery (BEL-10, P0)
- **Silhouette:** a walled cloister with a church, a bell gable, a walled garden and a few farm plots outside.
- **Parts:** church (nave and transept, cross on the gable), cloister square with a covered walk, dormitory range, bell gable,
  fish pond, kitchen garden (crop tags), a gate. The owner's colour on the banner and church door only.
- **States (important):** **staffed** (4 monks: smoke from the kitchen, a figure at the garden, a bell flicker at the hour),
  **queued** (site scaffolded), **dormant** (shutters closed, no smoke, the garden brown, a small "empty" glyph at overview zoom).
  Doc 27: an empty monastery yields nothing; it must never look alive.
- **Placement (decided):** always an outer work, outside the wall, even in a Town. A monastery never takes a core hex and never
  joins a district; the wall line does not extend to include it. It may have its own low precinct wall and gate.
- **Land:** up to 6 wilderness tiles are monk-farmed; those tiles get a small garden-plot overlay (not full fields). Drawn only while staffed.
- **Footprint (P):** radius 0.3, height 0.18 with a bell gable to 0.26.

### 4.6 University (LRN-08, P1)
- **Silhouette:** the academy's upgrade, a long collegiate hall with a bell turret and a courtyard, larger than the academy.
- **Parts:** hall with Gothic windows (GLASS_BLUE, emissive at night when staffed), an open arcade, a book-gable sign, a small garden.
- **States:** site, staffed (lit windows, a scholar figure on the steps), unstaffed (dark windows, closed doors).
- **Footprint (P):** same hex as the academy; scale 1.3 to 1.45 of the academy's.

### 4.7 Hospital (MED-07, P1)
- **Silhouette:** a long low hall with a cloister and a bed-hall gable, a cross-and-bowl sign.
- **Parts:** hall, chapel gable, herb garden, a covered walk, a few white-cloth bed shapes visible through the open end.
- **States:** staffed (a physician and 2 attendants: smoke, a figure), empty (shuttered). A small **spirits barrel** at the door while the
  hospital holds G-SPIRITS (CHM-06, doc: epidemic deaths −25% rather than −20%).
- **Footprint (P):** half-hex, radius 0.19. Shares the Health hex with the bathhouse.

### 4.8 Clock tower (MEC-08, P1)
- **Silhouette:** a slim square tower with a clock face and a bell, in the market square.
- **Parts:** shaft, clock face (emissive ring at night), belfry with open arches, pyramid cap.
- **Effect cue:** a faint **6-tile ring** on the ground only when the player selects it (radius from the effect, Cohesion and scheduled work).
- **Footprint (P):** small; radius 0.07, height 0.28. The tallest object in the market square.

### 4.9 Print shop (COM-10, P1)
- **Silhouette:** a narrow two-storey workshop with a hanging type-block sign and a drying loft of paper sheets.
- **Parts:** timber-and-plaster front, press visible in the open ground floor, a line of paper sheets in the loft.
- **States:** working (a figure at the press, sheets), idle, none.
- **Footprint (P):** small, 0.1 × 0.08.

### 4.10 Concentric castle (DEF-09, P0)
- **Silhouette:** the castle with a second, lower outer ring, a gatehouse and round corner towers.
- **Parts:** inner keep (reuse `castle_4`), outer curtain wall with six to eight round towers, a killing ground (bare ring) between the
  walls, a barbican gate, owner banners.
- **Stages:** `concentric_1..4` as `castle_1..4`: outer foundations, outer wall rising, towers up, finished with banners. The old
  castle stays standing and visible inside the whole time.
- **Garrison cue (important):** the ×1.5 defense applies **only to a garrisoned** castle. A garrison shows as a few figures on the walls and
  a lit window; an **ungarrisoned** concentric castle draws the banner **lowered**.
- **Footprint (P):** whole hex; radius 0.42. Stays on the wall line, ring 2.

### 4.11 Gothic Cathedral (BLD-12, P0, Wonder)
- **Silhouette:** the age's landmark: a cruciform stone church with twin west towers, a spire over the crossing, flying buttresses
  and pinnacles, and a glowing rose window. The tallest object on the map.
- **Parts:** nave, transept, twin towers, crossing spire (to ≈ 0.7 high), flying buttresses, pinnacles, west portal, rose window
  (GLASS_BLUE emissive), owner's banners on both towers.
- **Stages:** `gothic_cathedral_1..4` plus a build site. Stage 1 foundations and a master mason's lodge; stage 2 nave walls and scaffold;
  stage 3 towers and buttresses with the roof open; stage 4 spire, glass and banners. Needs a **master mason**: show a figure on the site
  while one is assigned; scaffolding stops when none is.
- **Footprint (P):** the Wonder hex, about 0.62 radius like `great_dome`. Straight behind the Heart.
- **Moment:** a badge burst plus a bell ring and a light pulse over the settlement.

### 4.12 Manor house (TER-10, P2)
- **Silhouette:** a timber-framed hall with a small barn and a yard, among fenced fields.
- **Rule (decided):** a **farm cue only**, not a sim structure and never counted as a building. It is drawn on an estate (a settlement
  holding TER-10 and farm tiles in Fief mode), one per estate, outer ring, like the ox cart in `farm_rig`. It does not take a hex, does
  not enter the building budget, has no states, and is a cosmetic function of `estateFlag` and the farm tiles. It disappears if TER-10
  is not held.

## 5. Land and terrain

- **Three-field strips (AGR-11, P0).** Three parallel strips in a field cluster: crop, crop or legume (a different tint), and a **fallow strip
  with grazing**. One third is always fallow. The fallow strip rotates each year (visible change). Reuse `farm_rig` field draw, add a
  `fallow_grazing` state with 1 to 2 cattle on the fallow strip.
- **Heavy plough and horse teams.** Where the settlement holds horses (NRG-04) the ard teams in `farm_rig` use a horse pair instead of oxen.
- **Game reserves (HNT-11, P2).** A low timber fence and a lodge on one tile, with deer visible; the fence is the owner's colour.
- **Drained wetland (NRG-05 pump, P2).** A wetland tile with a drainage ditch and a small pump house; becomes field only while the pump
  job is staffed. A pump that stops shows the tile reverting to wet.
- **Monk-farmed wilderness (P1).** See §4.5.
- **Revealed ore veins (MIN-08, P2).** Own-territory veins show the existing deposit markers (`art_deposits.gd`); no new art, only the reveal.
- **Saltpeter and sulfur deposits (P1).** Two new `art_deposits` entries: **saltpeter** pale crust on cave mouths and dry walls, **sulfur** yellow
  crust at volcanic vents. Visible to the owner only after a find, like existing deposits.
- **Blasting at a mine head (P2).** A powder barrel and a small blast mark at a mine head holding powder.
- **Larger halls and workshops (WOD-10, P2).** The 1.5 cap shows as an extra bay on halls, barns, shipyards and workshops, not new meshes.

## 6. People and figures

Figure budget is unchanged: **at most 600 per civilization**; the sim stays 1:1 and the view samples if over (doc 27 §6). All E5 figures
use the existing kit (08 §2.1) and the material ladder.

| Figure | Look | Where seen | Tier |
|---|---|---|---|
| Monk | brown or undyed robe, a hood, a hoe or a book | monastery, garden plots | P1 |
| Master mason | leather apron, mallet, a set square | Cathedral and big stone works | P2 |
| Knight | mounted, steel arms, owner's colour on the surcoat and horse cloth, a lance | open battle, on road | P1 |
| Gunner | cap, powder horn, linstock | beside a cannon | P1 |
| Crossbow or longbow levy | peasant cloth, a crossbow or a long bow, a quiver | in defense, on walls, in battle | P2 |
| Pilgrim | a staff, a cloak, a scallop badge | on completed pilgrimage roads | P1 |
| Scholar and physician | existing scholar figure, plus a book; a physician with a bag | university, hospital | P2 |
| Elder with spectacles | a small glint on the face | at close zoom only | P2 |
| Horse (draft) | collar on the horse | farm_rig and road carts | P2 |

## 7. Goods, routes and ships

- **No open-sea route art (decided).** Nothing is drawn for crossing open sea in E5, no new route type, lane style or sea-crossing marker.
  Cogs, lateen sails and rudders are ship meshes on routes the harbors already open (doc 27 §5.7); crossing arrives with E6.
- **Cog (SEA-10, P0).** A broad single-masted ship with a high stern castle, a square sail (lateen sails SEA-11 add a second, triangular
  sail and a visible rudder). Cargo doubles, so a cog shows **more bundles** than a galley. Placed at the harbor like the galley; at sea on routes. Wake and
  sail animation from the existing ship code.
- **Sail and rudder variants (SEA-11).** A lateen sail on the cog and a **sternpost rudder** as a mesh swap; the speed gain reads in how fast
  the ship moves, not how it looks.
- **Compass and charts (WAY-08, WAY-09, P2).** No object. The charts reveal only observed or shared coast (no omniscience): the revealed
  coast outline draws like other observed coast.
- **Pilgrimage routes (BEL-11, P1).** A thin road overlay line with a **scallop-badge marker** at each end, drawn **only on completed,
  connected roads that pilgrims actually travel**. Pilgrim figures walk it. A route ending at the Cathedral gets a small gold edge.
- **Goods on carts and quays (P2).** New cargo meshes for the flows layer: pig iron and cast iron (grey ingots), crucible steel (a few
  bright ingots), powder (barrels), paper and books (stacks), porcelain (white crates), spirits (barrels with a flame mark),
  planks (stacked), cannon (a barrel on a carriage), horseshoes (small).
- **Trade volume and bank (TRD-08, TRD-09, P2).** No object; a gold edge glow on busy routes at overview zoom, from the flows layer.

## 8. War, overlays and status

### 8.1 Institution states (P0, F-T6)
Every E5 institution shows one of four states, on the building and in an overview-zoom glyph.

| State | Cue |
|---|---|
| **Working** | smoke or animation, lit windows, figure present |
| **Queued** | build site, scaffolding |
| **Dormant** | shutters closed, cold, no smoke, a small grey glyph, with the reason on hover (no staff, no power, no input, no fuel) |
| **Blocked** | working cue but a red caution glyph (for example a blast furnace out of charcoal, a mill with no power unit) |

Applies to: monastery, university, hospital, clock tower, mill (all three), blast furnace, powder mill, print shop, cathedral.

### 8.2 Mill power (P1)
Doc 27 §5.1: mills in a town share a power budget. Show a **power-unit pip row** over the mill cluster: filled pips are assigned,
hollow are free. A mill job with no pip is dormant. Sources: water wheel 5, windmill 4 at 80% availability, horse gin 2 (P).

### 8.3 Cannon and siege (P1)
- **Cannon (WAR-12).** A bronze or iron barrel on a wooden carriage, 4 gunners, smoke and a flash on firing, a recoil frame. One cannon draws at
  the besieger's position per engine slot.
- **Wall damage (P1, decided: broken-wall visuals).** Doc 27: a cannon does twice an engine's damage to wall integrity. Breaches are
  drawn as **the wall itself broken**, by mesh swap, not as an overlay or a bar. New wall pieces, in the same units and palette as
  `wall_seg`, `wall_tower` and `wall_gate`:

  | Mesh | Looks like | Integrity (P, tune against `siege_of`) |
  |---|---|---|
  | `wall_seg` | whole wall | above 70% |
  | `wall_seg_cracked` | cracks, chipped merlons, a few fallen stones at the foot, a dust wisp | 40% to 70% |
  | `wall_seg_breach` | a **gap through the curtain**, a rubble slope, broken ends, a few merlons down | 15% to 40% |
  | `wall_seg_ruin` | a low stump and a rubble heap, the line open | under 15%, and after a storm |
  | `wall_tower_cracked`, `wall_tower_breach` | a tower with a split wall, then a half-collapsed tower | same bands |
  | `wall_gate_broken` | splintered gate doors, one hanging | on a gate breach |

  Driven by the integrity permille already in `siege_of(sim_index)`. Which segment breaks is the one facing the besieger's origin tile
  (`siege_of` returns it), so the damage faces the attacker. Pieces repair back through the same meshes over seasons as integrity
  recovers; a rebuilt wall returns to `wall_seg`. Dust and a small cannon-flash accompany a hit; they stop when the firing stops.
- **Concentric castle under siege.** The outer ring takes the broken meshes first and the inner curtain stays whole until the outer
  ring is gone; the keep never shows a breach before the storm.
- **Heavy cavalry charge.** In an open-battle frame only, a mounted line; never drawn on an assault.
- **Powder stock (P2).** A barrel stack at the besieger's camp while powder is held.

### 8.4 Health (P1)
- **Outbreak overlay.** Existing outbreak tint, extended to show spread **between settlements linked by a route or shared border**
  (doc 27, MED-08).
- **Quarantine.** A **yellow flag and a gate bar** on routes and borders where it applies (a rule that halves imports); drawn on the route
  line, not as a building.
- **Chimneys (FIR-10).** Smoke from house chimneys in winter (a small chimney mesh on the Fief house variant). This is also the cue for the
  winter death cut.

### 8.5 Civic cues (P2)
- **Chartered town (GOV-11).** A small charter banner on the Heart and a market bell sound.
- **Guild, bank, courthouse.** Signs on the market square and the Heart (31a: small, Market or Council chains); drawn only if cheap.
- **Dynastic marriage (DIP-08).** A banner pairing on the allied settlement; when a Free Folk region joins by inheritance, the standard
  Moment burst, **never an instant territory fill**: the border redraws over a season.
- **KEY-6 Printing Revolution (own).** The era-advance Moment, as for KEY-5. Text says "KEY-6" or "Printing Revolution" **only in Fief mode**.
- **Rival KEY-6 banner (P1, decided).** When a rival completes KEY-6, the human sees a banner. Because doc 27 D11 says a rival's KEY-6
  **starts no clock**, it is a notice, not a warning timer.
  - **Where:** the existing rival-era path (`Game.Modals.cs` rival-era notice and `Game.Notify.cs` `rival-age` key), a full-width
    banner at the top of the map in the rival's colour with the rival's emblem, a **type-block (movable type) icon**, and the line
    "*The {civ} has completed the Printing Revolution.*"
  - **No countdown wording or chip.** The Iron text "or N years after a rival does", the top-bar countdown chip and its tooltip
    (`Game.TopBar.cs`) must **not** appear in Fief. The banner says plainly that the match continues until the human's people get there.
  - **Map cue:** a Moment burst at the rival's capital (badge burst, bell ring, light pulse) if the capital is in view or already known;
    the camera does not jump (08 §10 tier "high", no auto-camera).
  - **Visibility (decided): always announced.** The banner fires for every rival's KEY-6, whether or not the human has met that rival
    or seen their land. It names the rival's civilization (as the Iron banner does) and reveals nothing else: no location, no map
    reveal, no fog change. The map cue below is shown only if the capital is already known; otherwise the banner and Chronicle line
    stand alone. If the human has never met the rival, the banner uses the civilization's name and colour as the roster already shows them.
  - **Once per rival.** Keyed `rival-age-6-{civ}` so a second rival gets its own banner and none repeats.
  - **Chronicle line:** logged in the Chronicle either way.

## 9. What the sim must tell the renderer

The renderer never reads sim state directly; it reads the frame. Doc 27 §3.3 says Fief appends a `FiefFrame` block, empty unless Fief mode.
Proposed fields, append-only and cheap:

| Field | Shape | Used for |
|---|---|---|
| `fiefWorks` | per settlement, a count per kind, like `ironWorks` (`world_data.iron_works`) | buildings and mills (§4) |
| `fiefWorkState` | per work, a state byte: 0 working, 1 queued, 2 dormant, 3 blocked, plus a reason code | §8.1 |
| `millPower` | per town, units total and units assigned, per source kind | §8.2 |
| `monkStaff`, `monasteryTiles` | staffed count; the farmed tile list | §4.5 |
| `fallowTiles` | per field tile, the rotation phase (0 crop, 1 legume, 2 fallow) | §5 |
| `estateFlag` | per settlement, 1 when TER-10 is held in Fief mode | §4.12 |
| `garrisoned` | per castle, 0 or 1 | §4.10 |
| `pilgrimRoutes` | list of route ids that carried pilgrims this season | §7 |
| `rivalKey6` | per civilization, the season it completed KEY-6 (or -1) | rival banner (§8.5); the sim has no timer attached |
| `wallIntegrity` | already in `sieges` (permille, origin tile); per wall segment if the renderer needs finer damage than one value per siege | §8.3 |
| `quarantine` | list of route and border ids with quarantine in force | §8.4 |
| `cannons` | per siege, cannon count; integrity already comes from `sieges` | §8.3 |
| `looks` | a new `LOOK_*` bit for each E5 look that changes a mesh, as `has_look(civ, bit)` | variants (cog sail, horse collar, chimney house) |
| `reserveTiles`, `drainedTiles` | tile lists | §5 |

`has_look` bits are cosmetic and per civilization (what a civilization knows), so the figure and mesh variants follow the same
cosmetic path as the ox cart in `farm_rig` and need no sim state.

## 10. Order of work

1. **Data first (blocked on doc 27 WP-E5-4 mode plumbing):** `FiefFrame`, `fiefWorks`, state bytes. Art can be gallery-driven before that.
2. **Shared pieces:** palette additions, type-sign glyphs, state-glyph set, smoke and glow effect.
3. **P0 buildings:** windmill, blast furnace, monastery, concentric castle, cathedral (with stages).
4. **P0 land and ships:** three-field strips, cog.
5. **P1 buildings and figures:** university, hospital, clock tower, sawmill, powder mill, print shop; monk, knight, gunner, pilgrim.
6. **P1 effects:** pilgrimage line, siege breach, cannon, outbreak and quarantine.
7. **P2 polish.**

The dev gallery (the same one that checks Iron works) should have one capture per mesh and per state before any of it goes into a match.

## 11. Acceptance

| ID | Check |
|---|---|
| A-1 | Every P0 and P1 item has a gallery capture for each state it can show, in light and in night lighting |
| A-2 | Silhouette test: at overview zoom a player can tell windmill, monastery, furnace, castle and cathedral apart in a still |
| A-3 | `check_core_layout.py` passes: every whole building drawn whole, none dropped, no building on a reserved hex it should not use, outer works clear of houses (powder mill ≥ 0.45) |
| A-4 | Every institution state in §8.1 is reachable in a `--grant-e5` capture and reads correctly with no tooltip |
| A-5 | Draw cost: a late-game City with every E5 item does not add more than 8% to the frame time of the Iron baseline (measure; the first run sets the budget) |
| A-6 | Figures stay at ≤ 600 per civilization with the view sampling on |
| A-7 | Bronze and Iron renders are byte-identical with Fief off (the Fief frame is empty unless Fief, as for `IronFrame`) |
| A-8 | No E5 picture appears for something the sim does not hold (ghost monastery, windmill without a unit) |
| A-9 | "KEY-6" and "Printing Revolution" appear only in Fief mode |
| A-10 | Windmill, sawmill and water mill are told apart in an overview-zoom still with no labels, although they share a base |
| A-11 | Each breach mesh in §8.3 has a gallery capture at its integrity band, facing the besieger's origin, and repair returns to `wall_seg` |
| A-12 | A rival KEY-6 raises the banner once per rival, **including a rival the human has never met**, with no location or fog reveal, and **no countdown text or chip appears** anywhere in Fief; Iron's countdown UI is unchanged |
| A-15 | No new route, lane or marker for open-sea crossing exists in E5; cogs appear only on routes that already exist |
| A-13 | A Town's monastery stands outside the wall on every seed in the capture set; no monastery takes a core hex or extends the wall line |
| A-14 | A manor house appears only on an estate with TER-10 held and never changes the building count or the 31a hex budget |

## 12. Decisions and open questions

**Decided (owner, 2026-11-13):**

1. **Monastery stands outside the walls, even in a Town.** Always an outer work (§3, §4.5).
2. **Manor house is drawn only as a farm cue.** Not a structure, no hex, no states (§4.12).
3. **Sawmill and windmill reuse the water-mill base and must have unique visuals.** Shared base meshes and code, distinct silhouettes,
   colours and animation, checked by A-10 (§4.1, §4.2).
4. **Siege breaches are broken wall visuals.** Mesh swaps of the wall pieces, not overlays or bars (§8.3, A-11).
5. **A rival finishing KEY-6 needs a banner.** A notice with no countdown, since doc 27 D11 has no clock (§8.5, A-12).

**Earlier decision that this spec follows:** the Gothic Cathedral is its own Wonder hex (31a, owner decision 2026-11-10).

6. **No open-sea route art.** E5 draws better ships (cogs, lateen sails, rudder) on routes that already work, and nothing new for crossing
   open sea; open-sea crossing art arrives with E6 (§7).
7. **A rival's KEY-6 is always announced**, including a rival the human has not met. The banner shows only the civilization's name and
   colour and reveals no location or map (§8.5, A-12).

**Still open:** nothing.
