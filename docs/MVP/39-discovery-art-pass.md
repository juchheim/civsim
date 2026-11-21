# 39 — Discovery (E6): the art pass

Status: draft spec, 2026-11-21. Nothing here is built. The renderer has no E6 art and the sim has no `DiscoveryFrame` yet (doc 38 WP-E6-11). Numbers marked (P) are proposals to tune in the dev gallery. The nine owner questions were all decided on 2026-11-21 (§12); no open questions remain.

Sources: [38](38-discovery-age.md) (design, §8 view layer, WP-E6-5 to -9 slice reports), the typed E6 rows in `content/registries` and `content/catalog` on `codex/e6-discovery` (worktree `civsim-e6`: 10 structures, 12 goods, 3 roles, 4 activities, 11 recipes), [32](32-e5-art-pass.md) (the E5 art pass this one copies), [31a](31a-staged-core-and-building-budget.md) (where buildings may stand), [08 §3 and §13](08-ui-and-world-display.md) (era look and generic kit), and the kit in `game/Renderer/LowPoly3D/Scripts/art_*.gd` on `main`.

## 0. In plain words

Discovery is read on the map from a few big silhouettes: **ships leaving the coast and crossing open water, black coal pits with a steam engine beside them, brick factories with chimneys and soot around them, star-shaped forts, and hedged four-course fields**. Most other E6 projects are rules and numbers and get a badge and a card, not a model.

This spec lists everything that needs a picture, how big it is, where it stands, what states it shows, and what the sim must tell the renderer. It follows the rules of doc 32 and adds two of its own:

1. **Draw only what the sim has.** A picture appears when a settlement holds the building, the figure holds the item, or the route is really sailed. No decorative E6 the sim does not back. A coal mine is drawn on a coal tile the owner has seen, never on a guess.
2. **Show idle.** Every staffed or powered institution shows working, queued, dormant or blocked (doc 38 D-T8). An engine with no coal must look cold. A garden with no botanist must look empty.
3. **Stay inside the city budget.** Doc 31a stands: a new building upgrades an older one, joins a district, or goes outside the walls. The only whole-hex newcomers are the Wonder and the star fort (which takes the Citadel slot).
4. **Honest sight.** Ocean art shows what a civilization has sailed or seen. Ships and charts never reveal land the player has not reached.
5. **E6 is inert elsewhere.** Bronze, Iron and Fief renders stay byte-identical (doc 38 B6-01); the Discovery frame is empty unless Discovery mode.

## 1. Scope and priority

| Tier | Meaning | Items |
|---|---|---|
| **P0** | Carries the identity of the age. Needed before any E6 playtest | caravel, galleon, ocean sea lanes, coal mine, atmospheric engine, factory (with soot), bastion fort, four-course hedged fields, institution states (§8.1), colony states, rival KEY-7 banner |
| **P1** | Needed for the systems to be legible | coke oven, navigation canal, turnpike and coach, botanical garden, learned society hall, exchange house, Renaissance Palace (Wonder), offshore fishing fleet and banks, cane fields, coal cargo, musket levy figure, engineer, navigator, miner (soot variant), botanist, bastion breach states, wreck marker, fleet-away and blockade markers |
| **P2** | Polish, mostly signs and small props | embassy flag, observatory dome and telescope props, greenhouse, newspaper stand, merchant and scholar variants, colonist group, convoy escort cue, sugar and cod cargo, chart grain, harbor upscale, Columbian Exchange crop pop, glazed windows |
| **Card only** | No map object. Badge, Chronicle line and a card | MIN-10 blasting numbers, FIR-11 coal firing (a smoke tint, §5), MET-13 coke smelting (recipe, no building), INQ-08/09, NUM-10/11, MEC-09, CHM-08 recipes, CER-11 (beyond a sheet-glass cargo), LRN-09 spillover numbers, GOV-12, SOC-12, ART-10, BEL-12, COM-11 text effects, TRD-10 pooling (a ledger, §7), WAY-10 chronometer (an item on the ship), HUS-11 (blocked: no sim yet), MED-09, KEY-7 progress |

## 2. Kit conventions (match the existing scripts)

- One script per family, `game/Renderer/LowPoly3D/Scripts/art_disc_<family>.gd`, `extends RefCounted`, with `static func build(kit) -> void` filling `kit.meshes["<name>"]`. New files:
  - `art_disc_power.gd`: coal mine, coke oven, atmospheric engine, factory.
  - `art_disc_civic.gd`: botanical garden, learned society, exchange house, Renaissance Palace.
  - `art_disc_war.gd`: bastion fort and its breach pieces, musket levy.
  - `art_disc_sea.gd`: caravel, galleon, fishing lugger, beached caravel, wreck.
  - `art_disc_land.gd`: four-course fields, navigation canal, turnpike, cane field, soot ground.
  - `art_disc_people.gd`: engineer, navigator, miner variant, botanist, musketeer, coachman, bargeman, colonist.
  - `art_disc_marks.gd`: glyphs, pennants, lane tokens, cargo props.
- Units: **1.0 = one tile column; an adult is about 0.1 tall.** Buildings face **+Z** toward the village.
- Material tags as doc 32 §2 (`TAG_CIV`, `TAG_PLAIN`, `TAG_FOLIAGE`, `TAG_CROP`, `TAG_CLOTH`, `TAG_EMISSIVE`), reusing existing palette constants. Add only: **COAL** (near-black with a blue cast; coal, spoil, pits), **SOOT** (warm grey-black for chimney smoke and stained ground), **BRICK** (the factory and coke oven red-brown), **CAST** (cast-iron grey, engine beam and boiler), **BRASS** (instruments, fittings), **CANVAS** (ship sails, cooler white than CLOTH), **GLASS_CLEAR** (plate glass, emissive when lit).
- Staged builds: `build_site_*` and the stage meshes pattern of `castle_1..4` and `gothic_cathedral_1..4`. The Wonder and the bastion fort get 4 stages.
- Era type sign (08 §13): E6 structures carry a small **type sign**, one glyph per type: coal lump (coal mine), flame-and-grate (coke oven), beam-and-piston (engine), gear-and-chimney (factory), star (bastion), leaf (garden), globe-and-quill (society), coin-and-ship (exchange house), crown-and-laurel (Palace).
- Registration: each new mesh name goes into `world_builder.gd` through a `DISCOVERY_WORKS` table shaped like `FIEF_WORKS` (mesh, scale, offset from the hearth, half-footprint, footprint radius), so placement, avoidance and site drawing reuse the Iron and Fief code paths.
- Dev gallery: `--scenario=discovery-gallery` (a clone of `fief-gallery`) with `--discovery-only=` prefix filtering, one capture per mesh and per state, light and night.

## 3. Placement (from doc 31a; E6 rows were marked "doc" there, now typed)

| Item | Size class | Chain or district | Where it stands |
|---|---|---|---|
| Coal mine | outer | none | on a seen coal tile the town works, **often 3 or more tiles from the town**; one per camp |
| Atmospheric engine | outer | *Industry* | beside the town's coal mine when a seam floods; otherwise in the Industry cluster (decided, §12) |
| Coke oven | outer | *Industry* | beside the blast furnace; downwind of the houses |
| Factory | outer | *Industry* | outer ring, downwind side, a tile with no plot; farther out than the blast furnace is acceptable |
| Navigation canal | outer, a line | none | from the town to its coal camps 3 or more tiles out, along a river or surveyed line |
| Turnpike | outer, a line | none | upgrades an existing road; a toll gate at the town edge |
| Bastion fort | whole | **Citadel** (castle → concentric castle **or** bastion fort, as doc 38's registry row says "an alternative to the concentric step") | ring-2 hex on the wall line; replaces the castle mesh (decided, §12) |
| Botanical garden | outer | none | ring 2 to 3, outside the wall, a tile with no plot, near a river or wet ground (31a's recommended reading) |
| Learned society | small | **Learning** (joins the Learning hex beside the university) | the Learning hex; scale to read as a collegiate sibling of the university |
| Exchange house | small | **Market** | the market square, toward the harbor; **harbor settlements only** |
| Renaissance Palace | whole | *Wonder* | own hex straight behind the Heart, then either side (decided, §12) |
| Galleon, caravel | (ship) | none | at the harbor quay; at sea on routes |
| Fishing luggers | (ship) | none | harbor, then the offshore banks 2 to 3 tiles out |
| Colony | a settlement | the usual chains | across the sea; no change to the building budget |

Everything small or whole follows the "best tier only" rule where it upgrades something (the star fort replaces the castle mesh, the society reads as a sibling of the university, not a duplicate).

## 4. Buildings (specs)

Footprints and scales are (P). Each entry gives silhouette, parts, states, reuse.

### 4.1 Coal mine (MIN-09, P0)
- **Silhouette:** a wooden **headframe** over a dark shaft, a black **spoil heap** and a loading shed; a line of coal sacks or a coal cart in front.
- **Parts:** headframe (timber, a winding wheel on top), shaft collar with a rail, lamp hut, spoil mound (COAL), a small winch house, a coal stack beside a loading bay, a ground stain (COAL tint on the tile and a ring of the next).
- **Reuse:** built on the existing `mine_head` family (the shaft, spoil heap, site and avoidance code); the coal deposit marker in `art_deposits.gd` (`_coal`) already draws the seen tile and stays under it.
- **States:** site, **working** (the winding wheel turns, miners at the bay, a coal stack), **flooded** (the shaft shows a dark pool at the collar, the wheel locked, a small water-drop glyph: a deep seam that needs an engine, doc 38 §5.1), **dormant** (no miners staffed), none. A flooded mine beside a working engine shows a pipe and a thin stream.
- **Unique read:** against an E3 mine head, the headframe is **taller, with a winding wheel and a coal-black mound**, so it reads as a coal pit even from overview zoom.
- **Footprint (P):** radius 0.16, height 0.24.

### 4.2 Atmospheric steam engine (NRG-06, P0)
- **Silhouette:** a **brick engine house with a tall rocking beam** projecting from one wall, a squat brick boiler and a short chimney. The beam's nodding is the signature.
- **Parts:** engine house (BRICK), the beam (CAST, `TAG_PLAIN`) on a pivot, a vertical cylinder in the house, a boiler with a firebox glow, a chimney, a coal heap and a water tank, a pump rod running down to the shaft when pumping.
- **Animation:** the beam rocks one cycle per 4 s while it burns coal and is staffed; a smoke puff each stroke (SOOT), a steam puff at the cylinder. Stopped beam, no smoke and a cold firebox mean idle.
- **States:** site, **working**, **pumping** (as working, plus a visible pump rod and a bucket stream at the mine), **dormant** (no engineers: stopped beam, shuttered), **blocked** (no coal: banked firebox, a red caution glyph). Doc 38 §5.1: an engine without coal or staff is idle and shown so.
- **Staff cue:** two engineers (`person_engineer`) visible while staffed.
- **Footprint (P):** radius 0.17, height 0.3 (the beam to 0.32).

### 4.3 Coke oven (FIR-12, P1)
- **Silhouette:** a row of low **beehive ovens** in a brick bank, with a small chimney stack and a pile of coke beside it.
- **Parts:** a bank of three to five beehive domes (BRICK, charging hole on each), a door apron, a coke stack (grey-silver), a coal stack, a thin smoke wisp.
- **States:** working (some doors glowing, smoke), idle (cold, no smoke), dormant, none. Coke smelting at the blast furnace (MET-13) is a recipe and has no building of its own; the `blast_furnace` mesh gains a coke-burning look (white-blue glow, no charcoal heap) via a look bit (§9).
- **Footprint (P):** radius 0.13, height 0.12.

### 4.4 Factory (MEC-10, P0, KEY-7 prerequisite)
- **Silhouette:** a long **brick shed with a sawtooth roof** and a tall square chimney, a loading dock and a water tank. The age's industrial landmark.
- **Parts:** brick body (BRICK), sawtooth skylights (GLASS_CLEAR, emissive when working), a chimney to about 0.3, a power shaft entering the wall from the engine or the water wheel, a gate, bales and crates at the dock, a gear-and-chimney type sign.
- **Effects:** a smoke column (SOOT) while it works, window light at dusk, and the **local soot** stain on the adjacent tiles (§5.3).
- **Power cue:** a **power-unit pip row** as for mills (doc 32 §8.2): filled pips are the share of the shared ledger the factory receives (water 5, wind 3.2, steam 10 per engine, P). At full power the sawtooth windows are lit and the shed hums; half power shows half the windows lit; no power shows a cold chimney.
- **Staff cue:** operatives (`person_operative`) at the door while staffed (a town of 120 with twelve operatives, doc 38 WP-E6-8 slice 1).
- **States:** site, working, **unpowered** (blocked: red glyph and a pip row empty), **unstaffed** (dormant), none.
- **Unique read:** a long low sawtooth silhouette with a tall chimney is unlike any E5 building, and nothing else on the map smokes black.
- **Footprint (P):** 0.26 × 0.14, chimney height 0.3.

### 4.5 Bastion fort (DEF-10, P0)
- **Silhouette:** a **star-shaped fort**: angled arrowhead bastions at the corners, low thick earth-and-brick curtains, a ravelin at the gate, a ditch. Low and wide where the castle is tall.
- **Parts:** inner keep or barracks (reuse a low `castle_4` roof), a star-trace rampart ring with 4 to 6 bastion points, cannon platforms at the points, a dry ditch (ground tint), a ravelin and a drawbridge at the gate, owner banners.
- **Stages:** `bastion_1..4` like `concentric_1..4`: ditch and footings, rampart rising, bastions up, finished with banners and guns. The old castle stays visible inside while it is being built.
- **Garrison cue (important):** registry says it is a **garrisoned** star fort. A garrison shows as figures and a gunner on each bastion and a lit window; an **ungarrisoned** fort draws the banner **lowered** (as the concentric castle, doc 32 §4.10).
- **Breach states (P1):** it must read as cannon-resistant. Its damage meshes are `bastion_cracked` (scarred earth face, a few displaced bricks), `bastion_breach` (a ramp of fallen earth through the face, a crumbled point) and `bastion_ruin`. Bands follow the integrity track (1000 intact; the fort counts as a wall of 6.75 to 9.0, doc 38 registry) and are driven by `siege_of`, as the E5 wall pieces are. The fort shows **fewer** breach stages than an E5 wall at the same cannon fire, because it halves the gunpowder penalty.
- **Footprint (P):** whole hex, radius 0.42, height 0.1 (the keep to 0.18). Sits on the Citadel ring-2 hex.

### 4.6 Botanical garden (FOR-09, P1)
- **Silhouette:** a walled formal garden with a **glasshouse** and an ornamental gate, set among clipped hedges and beds.
- **Parts:** brick-and-hedge wall with a gate, geometric beds in two or three crop tints (`TAG_CROP`), a glasshouse (GLASS_CLEAR) with a lean-to roof, a small potting shed, a fountain, a few trees in tubs; a leaf type sign.
- **States:** staffed (a botanist figure in the beds, glass faintly lit, beds in leaf), site, **dormant** (no botanist: gate closed, beds brown, glass dark, a small grey "empty" glyph). Doc 38: an empty garden has no effect, so it must never look alive.
- **Imported crops:** while a Columbian Exchange crop is on trial, one bed shows a new crop tint. P2.
- **Footprint (P):** radius 0.2, height 0.12 (glasshouse to 0.14).

### 4.7 Learned society (LRN-09, P1)
- **Silhouette:** a small **domed hall** with a portico and a globe or orrery on the roof, in the Learning hex beside the university.
- **Parts:** square hall with a classical portico (columns), a low copper dome, a book-and-globe sign, lit windows when staffed, a few steps with a scholar.
- **States:** site, staffed (lit windows, a scholar on the steps), **dormant** (no scholars: dark, doors closed). Doc 38: an empty society has no effect.
- **Footprint (P):** small, 0.14 × 0.12, height 0.2. Reads as a sibling of the university, not a second copy.

### 4.8 Exchange house (TRD-11, P1)
- **Silhouette:** a **colonnaded loggia** with a bell, open at street level so traders are visible, with a ship-and-coin sign, on the market square at the harbor side.
- **Parts:** arcade of round arches, upper floor with a clock or a bell, a ledger board with painted columns, bale and barrel props, owner banner.
- **States:** staffed (traders at the arches, lit upper windows), dormant (shuttered, no traders), none. Doc 38 registry: only in a harbor settlement.
- **Footprint (P):** small, 0.12 × 0.1, height 0.18.

### 4.9 Renaissance Palace (BLD-13, P1, Wonder)
- **Silhouette:** a symmetrical, many-windowed palace with a central domed or pedimented block, two wings and a formal forecourt and garden. Bright stone with plate-glass windows (GLASS_CLEAR, emissive when finished).
- **Parts:** central block with a dome, two wings with regular windows, a portico, a forecourt with a fountain and clipped hedges, flagpoles with owner banners.
- **Stages:** `renaissance_palace_1..4` plus a build site, as `gothic_cathedral_1..4`: foundations and a master mason's lodge; walls and scaffold; wings and dome shell; glass, banners and gardens. It needs a **master mason**: a figure on the site while one is assigned; scaffolding stops when none is.
- **Footprint (P):** the Wonder hex, about 0.6 radius, like `great_dome`. Placement decided in §12.
- **Moment:** a badge burst, a bell ring and a light pulse over the settlement.

### 4.10 Small cues beyond buildings (P2)
- **Embassy (DIP-09).** A flag on the Heart in the rival's colour while the permanent embassy stands. Sim not built yet (doc 38: remaining); draw only when it is.
- **Observatory and telescope (INQ-07).** A small brass dome on the Learning hex or a telescope prop on a watch tower. Tower sight ×2 is a rule, not a building; the prop appears only with a `LOOK` bit (§9).
- **Greenhouse (CER-11).** A glass lean-to on the garden and a cue on farmland where glazed winter food is held. Not typed yet; draw only when typed.
- **Print shop (COM-11 pamphlets).** The E5 `print_shop` mesh and its `print_shop_idle` state already exist (COM-10). The only E6 addition is a **stack of pamphlets at the door** and a hanging **broadsheet** while a `print_pamphlet` recipe runs. No new building.
- **Harbor upscale.** The E4 `harbor` mesh may read small beside a galleon; audit at the first gallery capture and add a longer quay or crane if it does (a variant, not a new building).

## 5. Land and terrain

- **Four-course enclosure (AGR-12, P0).** Fields drawn as **four parallel plots**, each a different tint (wheat, turnips or fodder, barley, clover), with **no fallow strip**, and **hedge lines** around each plot. The four-course phase rotates visibly each year. Reuse `farm_rig` field draw and `art_fief_land.gd`'s `field_three_*`; add `field_four_0..3` and a `hedge_line` mesh. Enclosure (field labor ×0.85, doc 38) shows as hedges replacing the open strips: a settlement that has AGR-12 draws hedges, one that does not keeps E5 strips. This is the ground-level signature of the age (08 §3).
- **Coal tiles (P1).** The coal deposit marker (`art_deposits.gd` `_coal`) already exists; E6 adds a **black ground tint** around a working mine (two rings) and a spoil heap prop. Not drawn until the owner has *seen* coal.
- **Navigation canal (TRN-11, P1).** A wide channel with stone-faced banks, a **towpath**, and **lock gates** at changes of level (or every few tiles), drawn as a line from the town toward its coal camps 3 or more tiles away. Barges (`barge`, with a coal load) tow along it. Distinct from the E3 irrigation `canal`: wider, straight-edged, with a towpath and locks.
- **Turnpike (TRN-10, P1).** The E4 paved road with a **toll gate and a toll house** at the town edge, and a slightly wider, lighter surface. Coaches (§6) run it. A road upgrade, not a new route.
- **Soot (P1).** A **local state**, not a global haze. Tiles adjacent to a working factory (and, at a smaller level, a coke oven or furnace) take a warm grey-black ground stain and dingy fields. Its strength follows the soot permille (doc 38 WP-E6-8 slice 1). No pollution haze volume: that begins at E7 with KEY-7.
- **Sugar cane (FOD-10, P1).** The cane deposit marker (`_cane`) exists. Add a **cane field** overlay on the cane tile worked by a settlement that holds FOD-10, tall green rows with a cutting gang. Sugar has **no structure**; the `refine_sugar` recipe runs in an existing workshop and shows as a sugar-loaf stack at the door (P2).
- **Offshore banks (HNT-12, P1).** Water tiles 2 to 3 out from a harbor town that has fishing fleets: a **bank marker** (a rippled patch with gulls) shown only to the owner and only while fleets fish there. These tiles are Discovery-only (doc 38 WP-E6-6 slice 4).
- **Fuel-use smoke tint (FIR-11, P1).** Chimney smoke from houses and kilns turns from pale grey-white (wood) to darker grey (coal) in a settlement that holds coal and burns it. A look bit; no new mesh.
- **Chart grain (P2).** A subtle parchment-and-line texture on the world map at Z3 to Z4 (08 §3). A shader layer, off by default in the accessibility option set.
- **Colonial ground (P2).** None. A colony uses the normal houses and chains; its identity comes from the pennant and states in §7.2.

## 6. People and figures

Figure budget is unchanged: **at most 600 per civilization**; the sim stays 1:1 and the view samples if over (doc 38 §6, 1:25 applies to the view only if needed). E6 figures use the existing kit (08 §2.1) and the material ladder.

| Figure | Look | Where seen | Tier |
|---|---|---|---|
| Engineer | leather apron, rolled sleeves, an oil can or wrench, soot on the face | at the engine; two while staffed | P0 |
| Miner (soot variant) | the existing miner with a black-smudged face, a lamp on the cap, a pick and a coal sack | at the coal mine bay | P1 |
| Navigator | a cap and coat, a brass **chronometer box** or a spyglass; on the stern deck | at the helm of a ship that carries a chronometer (WAY-10); on the quay | P1 |
| Botanist | wide straw hat, a collecting case (vasculum), a trowel | in the garden beds | P1 |
| Factory operative | cap, work apron, sleeves; may carry a bobbin or a crate | at the factory door and dock | P1 |
| Musketeer (drilled levy) | a coat in the owner's colour, a **tricorn or morion**, a flintlock or matchlock, a cartridge box; stands in **ranks** | in defense, on bastions, in drilled formation | P1 |
| Gunner (reuse `person_gunner`) | existing gunner | on bastion platforms and beside cannon | P1 |
| Coachman and passenger | a coachman with a whip on a coach box; a coat figure at the window | on turnpikes | P1 |
| Bargeman | a figure with a pole or a towing horse | on navigation canals | P1 |
| Scholar (reuse) | existing scholar with a telescope or a book | society steps, university | P2 |
| Merchant | a long coat and a ledger or hat | market, exchange house | P2 |
| Master mason | the E5 master mason (a P2 figure in doc 32, not built yet; build it once for both Wonders) | Palace site | P2 |
| Colonist group | a handful of figures with bundles and tools at a beached caravel | at a new colony on founding | P2 |

Notes: the drilled levy needs a `LOOK_DRILL` bit (§9): before WAR-13 the E5 crossbow, longbow and gunner figures stay; after it, the levy takes the coat-and-ranks variant. The sim has no typed musketeer role yet (doc 38 WP-E6-9 "musket rung" remaining); the wiring waits for it, as decided in §12. **No new figure mesh is needed for this:** `person_pikeman`, `person_musketeer`, `person_rifleman` and `field_gun` already exist from the WAR-13/14 levy art (doc 35 §7, `art_fief_people.gd`), so only the `LOOK_DRILL` switch is left.

## 7. Ships, flows and the open sea

Doc 32 §7 decided **no open-sea route art in E5**; doc 38 §5.2 delivers ocean crossing in E6. This is where it is drawn.

### 7.1 Ships (P0)
- **Caravel (SEA-12).** A small, fast ship with **two or three masts**, a lateen on the aft mast and a square sail forward, a low stern castle, a pennant in the owner's colour. Cargo about like a cog. The ocean ship of the age.
- **Galleon (SEA-13).** A tall ship with **three or four masts**, a high stern castle, a beak, a row of gun ports along the hull side, square sails with an owner-colour stripe, and a lantern at the stern. Bigger than a galley and a cog.
- **Ship of the line (SEA-13, P2).** The row says "Galleons & Ships of the Line", but the sim has one `galleon` structure (doc 38 WP-E6-6 slice 3). Draw one galleon mesh; a larger `galleon_line` variant is a later look bit, not a sim need.
- **Fishing lugger (HNT-12, P1).** A small two-mast boat with a net frame at the stern and a catch of cod or herring in baskets.
- **Wreck (P1).** A floating broken mast and flotsam for a lost voyage, a transient marker (a few seconds) at the tile of the loss, with a Chronicle line. WAY-10 and TRD-11 reduce losses (−70%, −50%); they change how often this appears, not how it looks.
- **Beached caravel (P2).** A caravel pulled up at a new colony's shore at founding.
- **States:** in port (at the quay), at sea on a lane, **on station** (a galleon blockading a harbor, drawn at the station tile), **escorting** (a galleon trailing a caravan, P2). A galleon on station shows a small **anchor glyph** at its home harbor: the crew is out of the defense pool (doc 38 §5.2).
- **Scale (mesh units, as built):** galleon 0.62 long, caravel 0.46, cog 0.5, so a caravel is slimmer and lower than a cog and a galleon is the biggest ship on the map (the first draft of this line gave figures in a different unit, in error). Audit against the harbor mesh and tile size at the gallery captures (decided, §12).

### 7.2 Ocean sea lanes (P0)
- **Drawn:** **dashed ocean arcs** between harbors that really have a sailed route, with ship tokens moving along them (08 §3.3). Reuse the dashed-line shader of `fief_overlay.gd` (`route_line.gdshader`) with an arc height and a lane tint in the owner's colour.
- **Encoding:** line thickness is the route volume (as caravan routes); dashes slow at low volume; a lane that has not carried a voyage this year fades to a faint line and disappears after two years.
- **Honest rule:** a lane is drawn **only for a route the viewer has sailed or seen**. Ocean crossing by a rival is drawn only inside the viewer's own sight. No lane reveals land.
- **Zoom:** flows are on by default from Z2 outward and can be toggled; at Z0 and Z1 the physical ships render instead (08 §3.3).
- **Existing shortcuts:** the E4/E5 coast-hugging routes already draw as dotted caravan and ship routes; ocean lanes must look **different** (dashed arcs, a lighter tint) so a player can tell an ocean voyage from a coastal one at a glance.
- **Voyage ledger (TRD-10 and TRD-11):** pooling (joint-stock) and insurance are ledger changes, not drawings. Show them as a small **ship-and-coin chip** on the lane's tooltip, no mesh.

### 7.3 Colonies (P0)
A colony is any member settlement the capital cannot reach on foot (doc 38 WP-E6-6 slice 2). It draws as a normal settlement plus:
- a **colony pennant** (owner colour with a small ship device) on the Heart and a **thin dotted tether** line to the capital;
- a **state ring** at overview zoom: **content** (no ring), **strained** (a yellow ring; Cohesion low from sea strain), **blockaded** (a red ring and a ship on station), **independent** (the pennant is struck and replaced by the Free Folk banner, with a Moment burst; the secession fires only by the seeded yearly roll, never by score).
- a **founding Moment:** a beached caravel, a small colonist group, a flag planted, and a badge burst.

### 7.4 Contact and exchange (P1)
- **First overseas contact** raises the standard Moment burst when a ship first arrives; it reveals only the arrival coast (observed), no omniscient map reveal.
- **Columbian Exchange (FOR-10, P2):** a crop pop above fields that gain a new staple, and a tint on newly suited fields. Paid only where the biome suits the crop.
- **Embassies and diffusion (P2):** a flag on the Heart (§4.10).
- **Cargo.** E6 cargo meshes for the flows layer, shown on carts, barges and quays: **coal** (black lumps, P1), **coke** (grey-silver, P1), **sugar** (white loaves, P2), **salt cod** (stockfish bundles and barrels, P2), **plate glass** (crated sheets, P2), **precision parts** (small crates, P2), **acid** (carboys, P2), **alkali** (sacks, P2), **pamphlets** (stacks, P2), chronometer and telescope (small boxes, P2).

## 8. War, overlays and status

### 8.1 Institution states (P0, doc 38 D-T8)
The E5 four-state set of doc 32 §8.1 stands, with new reasons:

| State | Cue | E6 reasons |
|---|---|---|
| **Working** | smoke or animation, lit windows, figure present | engine pumping, factory powered |
| **Queued** | build site, scaffolding | |
| **Dormant** | shutters closed, cold, no smoke, a small grey glyph, reason on hover | no engineer, no botanist, no scholars, no traders, no crew |
| **Blocked** | working cue but a red caution glyph | engine **out of coal**, factory **out of power**, coal mine **flooded** with no engine, galleon **with no harbor or no crew** |

Applies to: coal mine, engine, coke oven, factory, bastion fort (garrisoned or lowered banner), botanical garden, learned society, exchange house, Palace. The hover reason line is the same text the build guidance already carries (`BuildText.cs`).

### 8.2 Power ledger (P1)
Doc 38 §5.1: steam is a third source in the **same** power ledger as water and wind. Extend the mill pip row of doc 32 §8.2 to a **pip row over the Industry cluster**: filled pips assigned (water 5, wind 3.2, steam 10 per engine, P), hollow free. A factory with no pip is dormant; an engine whose pips are idle shows banked. Sources are drawn as small icons beside the row (wheel, sail, piston).

### 8.3 Siege (P1)
- **Cannon and gunners** reuse the E5 meshes; a cannon crew on a bastion platform fires with flash and smoke.
- **Bastion breaches** per §4.5.
- **Naval blockade:** the existing blockade drawing (IronFrame `BlockadeHarbor`, `BlockadeCiv`, `BlockadeOrigin`) draws a galley today; a crewed galleon takes the station when present. A galleon on station shows a gun-port flash if the harbor is under fire (P2). No new ship-versus-ship rule exists in the sim (doc 38 WP-E6-6 slice 3), so no broadside art yet.
- **Drilled levy** (P1): ranks of musketeers behind a bastion parapet; the army stays one icon and the figure sampling stays under 600.

### 8.4 Health (P1)
- **Soot and illness:** soot has a health weight (doc 38 WP-E6-8). The soot stain on tiles (§5) is the only cue. No separate overlay; no haze.
- **Plague weight 0.15 at E6** (doc 09 §10.2): the E5 outbreak overlay and quarantine gate are reused unchanged.

### 8.5 Civic cues (P2)
- **KEY-6 Printing Revolution (own).** The era-advance Moment, as for KEY-5. Text says "KEY-6" or "Printing Revolution" **only in Fief mode** (A-9 of doc 32); here it ends Fief and opens E6 in Discovery mode.
- **KEY-7 Industrial Revolution (own).** The era-advance Moment; E7 stays Hidden (doc 38 §6). Text says "KEY-7" or "Industrial Revolution" **only in Discovery mode**.
- **Rival KEY-7 banner (P0, same rule as doc 32 §8.5).** When a rival completes KEY-7, the human sees a notice, not a countdown: doc 38 D11 says **no rival clock**. The banner uses the existing rival-era path (`Game.Modals.cs` and `Game.Notify.cs` `rival-age` key), a full-width banner in the rival's colour with its emblem and a **gear-and-chimney icon**, and the line "*The {civ} has completed the Industrial Revolution.*" It is announced for every rival (met or not), reveals no location, is keyed `rival-age-7-{civ}` and shows once per rival. The map cue (badge burst at the rival's capital) only appears if the capital is known.
- **Chronicle lines:** every E6 Moment is logged. Newspaper text (COM-11) appears as a **printed broadsheet** card in the Chronicle (doc 38 §8).
- **State of the nation (GOV-12, SOC-12, ART-10):** banners on the Heart only; drawn only if cheap.

## 9. What the sim must tell the renderer

The renderer never reads sim state directly; it reads the frame. Doc 38 §3.3 says E6 appends a `DiscoveryFrame`, empty unless Discovery mode, following the `FiefFrame` pattern (per-settlement arrays aligned with the MatchFrame settlement block, own towns only, an `EnsureSettlements` grow method). Proposed fields, append-only and cheap:

| Field | Shape | Used for |
|---|---|---|
| `Active` | bool | gate: everything below is empty unless Discovery |
| `discWorks` | per settlement, a count per kind (coal mine, engine, coke oven, factory, bastion, garden, society, exchange house, galleon, lugger), like `ironWorks` | §4, `world_data.disc_works(s, id)` |
| `discWorkState` | per work, a state byte 0 working, 1 queued, 2 dormant, 3 blocked, plus a reason code | §8.1 |
| `coalStock`, `cokeStock` | per settlement, raw Q32.32 | cargo and stack size at the mine and ovens |
| `mineFlooded` | per mine, 0 or 1 | §4.1 |
| `steamUnits`, `waterUnits`, `windUnits`, `powerAssigned` | per town, units total and assigned | §8.2 pip row |
| `soot` | per settlement, soot permille; plus per tile for the stained ring | §5 |
| `canalCamps` | per town, the list of coal-camp tiles hauled by canal | §5 canal line |
| `turnpikeTiles` | tile list | §5 |
| `garrisoned`, `bastionIntegrity` | per fort, 0 or 1; integrity already in `sieges` | §4.5 |
| `colonyState` | per settlement, 0 not a colony, 1 content, 2 strained, 3 blockaded; plus the capital id | §7.3 |
| `galleonStation` | per galleon, 0 in port, 1 on station (blockade), 2 escorting; plus station tile | §7.1 |
| `voyages` | list: civ, from tile, to tile, progress, ship kind, route id | ocean lane tokens (§7.2) |
| `oceanLanes` | list of ocean route ids that carried a voyage this year, with volume | §7.2 |
| `wrecks` | transient list: tile, season | §7.1 |
| `bankTiles`, `fleetCount` | offshore tile list; per harbor, the fleet size | §5 |
| `botanistStaff`, `scholarStaff`, `traderStaff`, `engineerStaff` | per settlement, staffed count | §4, figures |
| `rivalKey7` | per civilization, the season it completed KEY-7 (or -1) | rival banner; the sim has no timer attached |
| `key7Progress` | permille | card only |
| `looks` | a new `LOOK_*` bit per E6 look that changes a mesh, as `has_look(civ, bit)` | `LOOK_COAL_FIRE` (coal smoke), `LOOK_COKE` (furnace glow), `LOOK_ENCLOSE` (hedges), `LOOK_DRILL` (musket levy), `LOOK_CHRONO` (navigator figure), `LOOK_SCOPE` (telescope prop), `LOOK_CARAVEL`, `LOOK_GALLEON` (ship mesh by knowledge) |

`has_look` bits are cosmetic and per civilization, as the E5 horse collar and cog sail were, and need no sim state. The existing frame blocks stay as they are: **IronFrame** already carries sieges (integrity, engines, origin) and blockades; **MatchFrame** already carries `Route*` arrays for trade routes and `Caravan` parties; ocean lanes may need their own list if the second sea graph (doc 38 WP-E6-6 slice 1) is not in the route arrays (check at the first frame audit).

## 10. Order of work

1. **Data first (after doc 38 WP-E6-4, the mode, already plumbed):** `DiscoveryFrame`, `discWorks`, state bytes, `colonyState`, `voyages`. Art can be gallery-driven before that.
2. **Shared pieces:** palette additions, type-sign glyphs, E6 state-glyph reasons, smoke and steam effect, the pip row.
3. **P0 buildings:** coal mine, engine, factory, bastion fort (with stages).
4. **P0 ships and sea:** caravel, galleon, ocean lane overlay, colony pennant and states.
5. **P0 land:** four-course fields, hedges.
6. **P1 buildings and figures:** coke oven, canal, turnpike, garden, society, exchange house, Palace; engineer, navigator, miner, botanist, operative, musketeer, coach, barge; cane field, fishing luggers and banks, coal cargo, soot ground.
7. **P1 effects:** bastion breaches, wreck marker, blockade and fleet-away markers, rival KEY-7 banner.
8. **P2 polish.**

**Status (2026-11-21): the P0 meshes are built, as meshes, not wired.** On `codex/e6-art-p0` (worktree `civsim-e6-art`): `art_disc_power.gd` (`coal_mine`, `_idle`, `_flooded`, `_site` and the separate `coal_mine_wheel`; `atmospheric_engine`, `_idle` and the separate `atmospheric_engine_beam`; `factory`, `_cold`, `_site`), `art_disc_war.gd` (`bastion_1..4`, `bastion_4_idle`), `art_disc_sea.gd` (`caravel`, `galleon`), `art_disc_land.gd` (`field_four_0..3`, `hedge_line`) and `art_disc_marks.gd` (`colony_pennant`, `mark_dormant`, `mark_blocked`, `mark_flooded`, `mark_strained`, `mark_blockaded`), registered last in `ART_PASSES` and photographed by `--scenario=discovery-gallery` (`LowPoly3DView.show_discovery_gallery`; `--discovery-only=` filters shots by prefix; the game args go after `--`). First gallery pass done: lit factory glow toned to amber, the engine beam thinned, the coal stain shrunk. **Weight:** the four-course field is about 2,500 triangles against the three-field field's 934 (the first draft was 6,900: its hedge was blobs, now box runs); that is the one P0 mesh to watch for A-5. **Not built:** the smoke, steam and beam/wheel rigs, the ocean-lane overlay, the colony state wiring, the bastion breach pieces, the P1 and P2 items, and anything that places a mesh in a match (that needs the `DiscoveryFrame`, WP-E6-11).

## 11. Acceptance

| ID | Check |
|---|---|
| A-1 | Every P0 and P1 item has a gallery capture for each state it can show, in light and in night lighting |
| A-2 | Silhouette test: at overview zoom a player can tell mine, engine, factory, star fort, caravel and galleon apart in a still |
| A-3 | `check_core_layout.py` passes: every whole building drawn whole, none dropped, no building on a reserved hex it should not use; outer works (garden, factory, engine) clear of houses and downwind of them |
| A-4 | Every institution state in §8.1 is reachable in a `--grant-e6` capture and reads correctly with no tooltip |
| A-5 | Draw cost: a late-game City with every E6 item does not add more than 8% to the frame time of the Fief baseline (measure; the first run sets the budget); smoke column count capped per settlement |
| A-6 | Figures stay at ≤ 600 per civilization with the view sampling on, including the factory and coal-mine crews |
| A-7 | Bronze, Iron and Fief renders are byte-identical with Discovery off (the Discovery frame is empty unless Discovery, doc 38 B6-01) |
| A-8 | No E6 picture appears for something the sim does not hold (ghost engine, factory with no power source drawn as powered, a garden that is not staffed drawn alive) |
| A-9 | "KEY-7" and "Industrial Revolution" appear only in Discovery mode; "KEY-6" and "Printing Revolution" only in Fief (as doc 32 A-9) |
| A-10 | The ocean lane is told apart from a coastal route in an overview still with no labels |
| A-11 | An ocean lane is drawn only for a route the viewer has sailed or seen; no lane or ship reveals unexplored land or changes fog |
| A-12 | A colony shows content, strained, blockaded and independent correctly in `--grant-e6` seeded runs; independence fires only through the yearly roll |
| A-13 | A flooded coal mine with no engine is drawn flooded and blocked; with a staffed engine and coal it is drawn pumping and working |
| A-14 | A rival KEY-7 raises the banner once per rival, including a rival the human has never met, with no location or fog reveal and **no countdown text or chip** anywhere in Discovery |
| A-15 | The bastion fort shows fewer breach stages than an E5 wall at the same cannon fire, driven by `siege_of` integrity |
| A-16 | Soot stains appear only adjacent to a working factory or oven and fade as the soot permille falls |
| A-17 | The galleon's on-station anchor glyph appears only while the crew is out of the defense pool |

## 12. Decisions

**Decided (owner, 2026-11-21), all nine accepted as recommended:**

1. **The Renaissance Palace is a Wonder on its own hex behind the Heart,** like the Gothic Cathedral. The Heart palace stays the capital seat; E6 gets a sixth landmark (§3, §4.9).
2. **The star fort is one whole-hex `bastion_1..4` mesh on the Citadel hex,** replacing the castle mesh and sharing the slot with the concentric castle. A low angled perimeter-wall variant is a later polish step (§3, §4.5).
3. **The engine stands beside the coal mine when a seam is flooded, otherwise in the Industry cluster beside the factory.** One mesh in either place (§3, §4.2).
4. **Galleon scale is set in the first gallery captures.** A longer quay variant is added to the E4 harbor only if the galleon looks wrong beside it (§4.10, §7.1).
5. **A colony looks like home.** Same houses and chains, plus the pennant, the dotted tether to the capital and the state ring (content, strained, blockaded, independent). The sim's colony definition stays the only rule (§7.3).
6. **Smoke budget:** at most one animated smoke column per working industrial building and one per chimney cluster, with a shared steam-puff pool, so a late-game City meets A-5 (§2, A-5).
7. **Ocean lanes show only the viewer's own lanes and lanes seen by their ships, towers or telescopes.** No omniscient lane view (§7.2, A-11).
8. **The `LOOK_DRILL` musket figure ships when the sim adds the musket rung, not before.** Until then the E5 crossbow, longbow and gunner figures stay (§6).
9. **Sugar has no structure.** A cane field on the cane tile and a loaf stack at the workshop door; no refinery building (§5).

**Earlier decisions this spec follows:** the Gothic Cathedral is its own Wonder hex (31a, owner decision 2026-11-10); no open-sea route art in E5 (doc 32 §12), delivered here in §7; a rival's age keystone is always announced and starts no clock (doc 32 §8.5, doc 38 D11).

**Still open:** nothing.
