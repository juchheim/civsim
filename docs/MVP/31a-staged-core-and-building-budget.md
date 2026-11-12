# 31a. The growing core and the building budget: room for every building the game will ever have

Status: plan, renderer prototype and WP6a simulation implementation (2026-11-11), an addendum to
[31](31-settlement-core-reserve-plan.md). WP6b and WP6c remain open. Numbers marked (M) were measured, (E) are estimates, (P) are
proposals. How each was measured is in §7 and [31b §5.6](31b-outer-hex-growth-spec.md#56-wp6a-enforced-rule-check).

## 0. In plain words

**The problem.** Doc 31 gives every settlement the same seven hexes: its own hex and the six around it. Villages and towns fit. A big
Iron Age city does not: it has more large buildings (temple, castle, ziggurat, academy, theatre, bathhouse, a Wonder) than seven hexes
hold, so the first prototype left five of them out and drew the temple at half size. About half the game's eras (Faith & Fief to the
Information Age) are not built yet and will add universities, hospitals, a cathedral, museums, factories and more.

**The answer, in one paragraph.** Keep the seven-hex core for every Band, Village and Town that has no large civic buildings. When a
settlement raises a large building that needs a hex of its own (a castle, an academy, a theatre, a bathhouse, a ziggurat or a Wonder),
it takes **one more hex from the next ring out**, chosen by a fixed rule that lives in the simulation so the picture and the rules
always agree. A City also takes a hex of homes once it passes 120 people and a second at 180. The next ring has 12 hexes, so a
settlement can grow to at most 19. Hexes holding metal, salt or coal, water, mountains or another village's heart are skipped, and a
field already on a hex that gets taken moves out at the next season. Later buildings mostly **upgrade** an older one (school, academy,
university, research lab are one building that grows) or **join a district** (theatre and museum share the civic quarter; bathhouse,
aqueduct, well and hospital share the health quarter), and the big industrial works of later ages (factories, mills, power stations)
stand outside the walls like mines do today. With those two habits the peak city needs about 10 large-building hexes in any era; without
them it would need about 16 large and 13 medium ones, about 35 hexes, which is the whole working range.

**What changes from the proposal I was asked to test.** Confirmed: the core grows only for settlements that need it, the rule is shared
by simulation and renderer, and every building declares a size class and an upgrade chain or district. Modified in three ways:

1. **Grow by building, not by stage.** Towns can already build castles, academies, theatres and bathhouses (the rules allow all four at
   Town tier), so "only Cities get 19 hexes" would leave crowded towns, and giving every City all 12 hexes at once would empty fields
   for buildings that do not exist. Each large building takes its own hex when it is raised; a City adds homes hexes by head count.
2. **No bigger working range for Cities.** A typical growing settlement takes 2.6 extra hexes (M), leaving about
   27 of its 30 field hexes, which is still more than its field limit of 14 in every City sampled (E: 30 field hexes hold 27.6 cultivable on average, M) of cities (M). The extra ring
   would add 31% wild food to the third of all settlements that are Cities by minute 120 (M) and break the tuned balance. The doc-31 fallback
   (fields may use ring 4) is kept as the lever, now **for settlements whose core has grown**, only if a measured food gap appears.
3. **Industry and big landscape works stay outside.** Factories, mills, furnaces, powder mills, monasteries, power stations and the like
   are outer works on ring 2 to 4, like mines today, never inside the walls.

**What the pictures show** (§5): the walled Iron city now has its academy, theatre, bathhouse, aqueduct, drill ground, castle, ziggurat,
Great Dome and a full-size temple, inside one wall that follows the hexes it uses, with nothing dropped. It reads as a planned city.
What still looks wrong is listed in §5.3.

## 1. Every settlement building, implemented and planned

### 1.1 Sources and how reliable the count is

| Source | What it gave | Reliability |
|---|---|---|
| `content/registries/structures.yaml` on main (0cc3ea0) | 60 entries, E0 to E4, all with simulation behaviour | complete for what exists |
| `codex/e5-fief` (worktree `civsim-e5`), `structures.yaml` and doc 27 | 11 E5 structures with provisional costs | complete for E5 as typed; E5 is paused |
| `content/catalog/*.yaml` | E5 to E9 rows are `draft` and carry no `build` effects (except the 10 E5 rows on `codex/e5-fief`) | not usable for E6 to E9 |
| docs 03a, 03b, 03c ("**Build:**" and "**Wonder:**" in the effect column) | 27 more named buildings for E6 to E9 | the only list for E6 to E9; it is a design sketch, never typed |
| docs 03a to 03c effects without a "Build:" tag | 15 implied buildings (bank, guildhall, courthouse, museum's concert hall and so on) | guesses: the docs name an institution, not a building |
| `codex/e4-iron` | nothing ahead of main | |
| other `codex/*` branches and agent worktrees (checked by name) | renderer, balance and UI work; no new structures found | names only, not every diff read |

**Total: 98 named buildings and works** (60 implemented, 11 typed for E5, 27 named for E6 to E9), plus **15 implied** ones that may or
may not become buildings. The 38 planned items are a reliable lower bound for what the docs intend; E6 to E9 have never been designed in
detail (doc 08 §13 gives them a "modular era building kit" with a sign on the roof, not bespoke models), so expect the real list to differ
by perhaps ±10 items. The size classes below are proposals; nothing in content declares a size today.

### 1.2 Size classes

| Class | Footprint | Holds |
|---|---|---|
| **Small** | one sub-slot (radius ≈ 0.135 of a hex) | a house, rack, quern, kiln, lookout, well, totem, shrine, flag |
| **Half** | half a hex: centre and two petals (radius ≈ 0.19) | granary, potter's yard, forge yard, market, school, crane, press, drill ground, longhouse, hall |
| **Whole** | a hex (radius ≈ 0.42) | temple, palace, academy, theatre, bathhouse, castle, ziggurat, a Wonder |
| **Perimeter** | the wall line round the hexes in use | thorn hedge, palisade, stone walls, gatehouse, star fort |
| **Edge** | the waterfront | weir, harbor, coastal battery |
| **Outer** | on a tile in rings 2 to 4 that holds no plot, never inside the wall | fields, pastures, mines, mills, quarries, megalith, monastery, factories, power plants, roads |

### 1.3 The table

Impl. = the simulation has it (✔), typed on `codex/e5-fief` only (E5), named in docs 03 only (doc), implied by an effect (imp.).
**Bold** chain names are upgrade chains (only the highest tier is drawn); *italic* names are districts (several buildings share one hex).

| Building | Era | Impl. | Size | Chain or district | Note |
|---|---|---|---|---|---|
| Hearth | E0 | ✔ | small | Heart | always on the centre hex |
| Lean-to | E0 | ✔ | small | **Shelter** | |
| Thorn barrier | E0 | ✔ | perimeter | **Perimeter** | |
| Hide tent | E1 | ✔ | small | **Shelter** | |
| Pit house | E1 | ✔ | small | **Shelter** | |
| Totem | E1 | ✔ | small | **Sacred** | |
| Lookout post | E1 | ✔ | small | **Watch** | |
| Drying rack | E1 | ✔ | small | *Workshops* | merges into a rack yard |
| Storage pit | E1 | ✔ | small | **Food store** | |
| Marker | E1 | ✔ | outer | | at seasonal camps |
| Seasonal camp | E1 | ✔ | outer | | |
| Wattle and daub house | E2 | ✔ | small | **Shelter** | |
| Mudbrick house | E2 | ✔ | small | **Shelter** | |
| Longhouse | E2 | ✔ | half | **Shelter** (large) | |
| Shrine | E2 | ✔ | small | **Sacred** | |
| Signal post | E2 | ✔ | small | **Watch** | merges into the lookout |
| Palisade | E2 | ✔ | perimeter | **Perimeter** | |
| Kiln | E2 | ✔ | small | **Pottery** | |
| Granary | E2 | ✔ | half | **Food store** | |
| Quern | E2 | ✔ | small | **Grinding** | |
| Weir | E2 | ✔ | edge | **Water front** | |
| Field, orchard, pasture | E2 | ✔ | outer | | plots, never in the core |
| Megalith (Wonder) | E2 | ✔ | outer | | ring 2 or 3, a landscape monument |
| Hall | E3 | ✔ | half | **Shelter** (large) | |
| Temple | E3 | ✔ | whole | **Sacred** | the Sacred hex in ring 1 |
| Palace | E3 | ✔ | whole (heart) | **Council** | the Heart landmark (three Heart petals; no mesh yet) |
| School | E3 | ✔ | half | **Learning** | |
| Market | E3 | ✔ | half | **Market** | no mesh yet |
| Caravanserai | E3 | ✔ | half | **Market** | at the gate; no mesh yet |
| Bakery | E3 | ✔ | small | *Workshops* | no mesh yet |
| Potter's workshop | E3 | ✔ | half | **Pottery** | |
| Smelter | E3 | ✔ | small | **Metal** | no mesh (drawn as the furnace) |
| Forge | E3 | ✔ | half | **Metal** | the forge yard |
| Stone walls (I to III) | E3 | ✔ | perimeter | **Perimeter** | |
| Hillfort | E3 | ✔ | outer | | on its hill |
| Canal | E3 | ✔ | outer | | on fields |
| Mining camp, mine head | E3 | ✔ | outer | | on deposits |
| Ziggurat (Wonder) | E3 | ✔ | whole | *Wonder* | a City only; its own ring-2 hex |
| Academy | E4 | ✔ | whole | **Learning** | school → academy |
| Theatre or arena | E4 | ✔ | whole | *Civic* | |
| Bathhouse | E4 | ✔ | whole | *Health* | |
| Aqueduct | E4 | ✔ | small + outer | *Health* | water tower in the Front or Health hex; spans outside the wall |
| Windlass well | E4 | ✔ | small | *Health* | |
| Castle | E4 | ✔ | whole | **Citadel** | always a ring-2 hex on the wall line |
| Gatehouse | E4 | ✔ | perimeter | **Perimeter** (gate) | |
| Crane | E4 | ✔ | half | *Workshops* | |
| Press | E4 | ✔ | half | *Workshops* | |
| Colonnade (temple tier 2) | E4 | ✔ | whole | **Sacred** | the temple's upgrade, same hex |
| Great Dome (Wonder) | E4 | ✔ | whole | *Wonder* | |
| Great Library (Wonder) | E4 | ✔ | whole | *Wonder* | placed beside Learning |
| Great Lighthouse (Wonder) | E4 | ✔ | outer | | on the shore |
| Harbor | E4 | ✔ | edge | **Water front** | weir → harbor |
| Water mill | E4 | ✔ | outer | **Mill** | on the river |
| Terrace | E4 | ✔ | outer | | a plot |
| Road, bridge, frontier wall | E4 | ✔ | outer | | lines |
| Galley | E4 | ✔ | (ship) | | not a building |
| University | E5 | E5 | whole | **Learning** | academy → university |
| Hospital | E5 | E5 | half | *Health* | shares the Health hex with the bathhouse |
| Clock tower | E5 | E5 | small | **Market** | the market square's landmark |
| Print shop | E5 | E5 | small | *Workshops* | |
| Concentric castle | E5 | E5 | whole | **Citadel** | castle → concentric castle |
| Gothic Cathedral (Wonder) | E5 | E5 | whole | *Wonder* | ambiguous: own Wonder hex (recommended) or top of **Sacred** |
| Monastery | E5 | E5 | outer | | ambiguous: monks "farm nearby wilderness", so ring 2 to 3 outside the wall (recommended) |
| Windmill | E5 | E5 | outer | **Mill** | on a ridge or open tile |
| Water-powered sawmill | E5 | E5 | outer | **Mill** | on the river beside the water mill |
| Blast furnace | E5 | E5 | outer | *Industry* | ambiguous: a charcoal hog with smoke; outside (recommended) |
| Powder mill | E5 | E5 | outer | *Industry* | doc 27: "stands apart from houses" |
| Bastion (star) fort | E6 | doc | perimeter | **Perimeter** | stone wall → star fort |
| Palace (Wonder) | E6 | doc | whole (heart) | **Council** | palace → Renaissance palace |
| Botanical garden | E6 | doc | outer | | ambiguous: half in *Civic* or a ring-2 garden; outer recommended |
| Factory | E6 | doc | outer | *Industry* | |
| Atmospheric engine | E6 | doc | outer | | at a mine |
| Canal (transport) | E6 | doc | outer | | a line |
| Museum | E7 | doc | half | *Civic* | shares the Civic hex with the theatre |
| Sewer system | E7 | doc | none | *Health* | underground; no footprint |
| Railway | E7 | doc | outer + half | **Market** / Front | the line outside; a station half-hex in the Front |
| Textile mill | E7 | doc | outer | *Industry* | |
| Gasworks | E7 | doc | outer | *Industry* | |
| Telegraph lines | E7 | doc | outer | | a line |
| Coastal battery | E7 | doc | edge | **Water front** | |
| Research lab | E8 | doc | whole | **Learning** | university → research lab |
| Computing center | E8 | doc | whole | **Learning** | ambiguous: top of Learning or *Industry* |
| Power station, reactor, dam | E8 | doc | outer | *Industry* | |
| Radar station, airfield, oil well, fish farm, highway | E8 | doc | outer | | |
| Data center, launch site, solar field, wind farm | E9 | doc | outer | *Industry* | |
| Guildhall (SOC-11), bank (TRD-08), stock exchange (TRD-11) | E5, E6 | imp. | small | **Market** | if they get pictures: signs on the market square |
| Courthouse (GOV-10), parliament or town hall (GOV-11) | E5 | imp. | small | **Council** | banners on the Heart |
| Manor house (TER-10) | E5 | imp. | outer | | on an estate, not in town |
| Concert hall (ART-11), cinema (ART-12) | E7, E8 | imp. | half | *Civic* | |
| Observatory (INQ-07) | E6 | imp. | small | **Learning** | |
| Public school (LRN-10) | E7 | imp. | none | **Learning** | an effect, not a building |
| Embassy (DIP-09) | E6 | imp. | small | **Council** | a flag on the Heart |
| Greenhouse (CER-11), shipyard (WOD-10), rolling mill (MET-14) | E6, E7 | imp. | outer | | |
| Skyscrapers (BLD-15) | E8 | imp. | none | **Shelter** | taller houses, the same slots |

### 1.4 Counts

| Class | E0 to E4 (implemented) | E5 | E6 to E9 (doc) | All |
|---|---|---|---|---|
| Small (core) | 18 | 2 | 0 | 20 |
| Half (core) | 10 | 1 | 1 | 12 |
| Whole (core) | 10 | 3 | 3 | 16 |
| Perimeter | 4 | 0 | 1 | 5 |
| Edge | 2 | 0 | 1 | 3 |
| Outer, lines, plots | 16 | 5 | 21 | 42 |
| **All** | **60** | **11** | **27** | **98** |

(The 60 are the registry entries; the Galley is a ship and not counted. The Aqueduct counts as small: its tower is in the core, its spans
outside.) Of the 16 whole-hex buildings, 6 are higher tiers of a chain (colonnade, concentric castle, university, research lab, computing
center, Renaissance palace), so only **10 need a hex of their own at the same time**: Sacred and the Heart's palace in ring 1, and
Citadel, Learning, Civic, Health and up to four Wonders (ziggurat, Great Dome, Great Library, Gothic Cathedral) in ring 2. With two
homes hexes that is **10 of the 12 ring-2 hexes**, in every era.

## 2. Sizing

### 2.1 Peak load of one late-game city

A "peak" city holds one of every building its era allows (the dev gallery's walled Iron city is exactly that for E4; Wonders are counted
at their realistic maximum of two per city, one in the world each).

| Horizon | Whole-hex items, no merging | Whole-hex hexes, with chains and districts | Half items, no merging | Half items, merged | Small items (plus houses) |
|---|---|---|---|---|---|
| E4 Iron (today) | 10 (temple, colonnade, palace, castle, academy, theatre, bathhouse, ziggurat, Great Dome, Great Library) | **7** in ring 2, + Sacred and Heart in ring 1 | 11 | 8 | about 20 |
| E5 Faith & Fief | 13 (+ university, concentric castle, Gothic Cathedral) | **8** (+ cathedral) | 12 (+ hospital) | 9 | about 22 |
| E9 (all named) | 16 (+ Renaissance palace, research lab, computing center) | **8** (everything else upgrades or joins) | 13 (+ museum) | 10 | about 22 |

Without merging, an E9 city would need 16 whole hexes, 13 half items (one per hex, since a half takes the hex centre, 31 §4.1) and
homes, workshops, the Heart and the gate: about **35 hexes, almost the whole 37-hex working range**. With chains and districts it needs
**at most 8 ring-2 hexes for buildings and 2 for homes, 10 of the 12**, in every era. Halves merged: the school folds into the academy,
the caravanserai into the market, the drill ground into the castle yard.

### 2.2 Core size per stage

| Stage | Core | When (P) |
|---|---|---|
| Band | 1 hex drawn, 7 reserved | from founding (doc 31) |
| Village | 7 | from founding |
| Town | 7, + 1 per large building it raises | castle, academy, theatre, bathhouse (all allowed at Town tier today) |
| City | 7, + 1 per large building, + 1 homes hex at 120 people, + 1 at 180 | as above; ziggurat and Wonders are City buildings |
| Maximum | 19 | ring 2 is full |

Measured in live Iron games (4 seeds, minutes 100 and 120, M): 

| Minute | Tier | Settlements | Growing (N > 0) | Extra hexes N: 0 / 1 / 2 / 3 / 4 / 5 | Mean N of the growing | Plots released |
|---|---|---|---|---|---|---|
| 100 | Village and Band | 296 | 0 | 296 / 0 / 0 / 0 / 0 / 0 | | 0 |
| 100 | Town | 37 | 10 (27%) | 27 / 6 / 3 / 1 / 0 / 0 | 1.5 | 11 |
| 100 | City | 98 | 77 (79%) | 21 / 28 / 21 / 17 / 9 / 2 | 2.2 | 126 |
| 120 | Village and Band | 267 | 2 (1%) | 265 / 1 / 1 / 0 / 0 / 0 | 1.5 | 3 |
| 120 | Town | 22 | 11 (50%) | 11 / 5 / 3 / 3 / 0 / 0 | 1.8 | 19 |
| 120 | City | 136 | 116 (85%) | 20 / 33 / 19 / 30 / 29 / 5 | 2.6 | 282 |

What the growing settlements hold at minute 120 (M, all 4 seeds): theatre 65, bathhouse 47, academy 45 and castle 16 among 136 Cities;
academy 8, bathhouse 9, theatre 3 among 22 Towns; 129 of 136 Cities pass 120 people. **No ziggurat or Wonder was standing in any of
the four games**, so the live maximum is 5 extra hexes (14 in all); the gallery's Iron city (7 extra) is the designed peak, not a common
case. Village-tier settlements with a work are former Towns that lost their market or people.

### 2.3 Working range and field land

The range stays radius 3 (37 hexes) for everything pooled (forage, fish, wood, herds) and for every stage (P). Field land:

| Case | Range | Core | Field hexes | Comment |
|---|---|---|---|---|
| Today | 37 | none | 37 | fields under the houses |
| Doc 31 | 37 | 7 | 30 | field limit 14 (+ terraces) |
| City with all of ring 2 (owner's 19-hex flower) | 37 | 19 | 18 | the limit of 14 leaves almost no choice; 16 to 28% (mean 21%) of Cities have fewer cultivable ring-3 hexes than the fields they hold (M) |
| Same, range 4 for Cities | 61 | 19 | 42 | +31% pooled food for a third of all settlements; ranges of Found neighbours 5 apart overlap |
| **Proposed: hex by hex** | 37 | 7 + N | 30 − N | N = 2.6 for a growing City at minute 120, at most 5 in live games and 7 in the gallery peak (M) |

Field yield of the best-k hexes in ring 3 only, against ring 2 and 3 (doc 31's plan): **−5.6% to −9.9% by seed and minute, about −8% overall** (M); in rings 3 and 4: +2.0% to +4.1%, about +3%
(M). So a full 19-hex core without a bigger range costs Cities a real share of their best field land, while the hex-by-hex rule costs
only the few hexes actually built on, and the eviction is small: 2.4 plots per growing City at minute 120 (1.6 at minute 100) and 1.7 per growing Town, against 9.3 plots (4.6 of them fields) that a full 19-hex flower would evict from every City (M).

### 2.4 Map capacity and neighbours

The spacing rule does not change (7 for fission, 5 to 6 for Found), so the map holds as many settlements as doc 31 measured (131.6 per map
with clean cores, M in doc 31). Settlements per live game and how many are Cities (M, Iron rules, 4 Warlord AIs):

| Seed | Minute 80: all / Towns / Cities | Minute 100 | Minute 120 | Mean City size at 120 |
|---|---|---|---|---|
| 1 | 77 / 8 / 7 | 104 / 11 / 15 | 104 / 3 / 34 | 150 |
| 2 | 112 / 10 / 7 | 113 / 7 / 33 | 113 / 5 / 37 | 149 |
| 3 | 94 / 14 / 4 | 108 / 9 / 25 | 105 / 8 / 34 | 161 |
| 4 | 105 / 5 / 10 | 106 / 10 / 25 | 103 / 6 / 31 | 154 |

So a typical game has **about 25 Cities at minute 100 and 34 at minute 120, a third of all settlements** (M). Towns become Cities fast
once SOC-09 is known; "only Cities grow" therefore is not a small exception, it is a third of the map.

What a City's ring 2 looks like (M, means over Cities at minute 100 and 120):

| Per City, ring 2 (12 hexes) | Minute 100 | Minute 120 |
|---|---|---|
| water (sea, coast, lake) | 0.33 to 0.96 | 0.35 to 1.03 |
| mountain, peak, ice | 0.32 to 0.76 | 0.26 to 0.89 |
| valuable deposit (metal, salt, coal) | 0.30 to 0.60; **24 to 44% of Cities have one** | 0.38 to 0.45; 24 to 39% |
| a river through it | 0.33 to 0.84 | 0.32 to 0.82 |
| own plots (fields + orchards) | 7.4 to 9.9 (fields 4.2 to 4.9) | 9.7 to 10.5 (fields 4.5 to 4.9) |
| another settlement's plot | 0 to 0.09 (0 to 3% of Cities) | 0 to 0.08 |
| inside another settlement's seven-hex core | 0 | 0 |
| usable for buildings (land, no deposit, not a neighbour's core) | 10.2 to 10.8 | 10.3 to 10.6 |
| Cities with fewer than 6 usable | 0 to 7% | 0 to 6% |
| nearest neighbour within 6 / 7 / 8 | 0–15% / 60–88% / 80–91% | 0–14% / 68–84% / 85–89% |

Ranges are over the four seeds.

Because no settlement may stand closer than 5, another settlement's seven-hex core never lies in a ring-2 hex (2 + 1 < 5), and two
settlements' ring-2 hexes never touch (2 + 2 < 5): **growth can never collide with a neighbour's core** (proved by the distances; the
measurement agrees). A neighbour's field can lie on a ring-2 hex when the two are 5 or 6 apart; it is released like one's own.

## 3. The rules (simulation, proposed)

### 3.1 Where it lives

A pure static function in `CivSim.Sim` (new file `Economy/SettlementCore.cs`, P):

```text
CoreHexes(world, siteTile, frontDirection, districtCount) -> ring-2 tiles, in reservation order
Districts(settlement) -> ordered list of district kinds that need a ring-2 hex
```

It reads only static world data (tiles, biomes, rivers, lakes, deposits) and the settlement's own structure counts, so it is stateless:
the reserved set is recomputed whenever it is needed and **nothing new is saved** (`SnapshotVersion` unchanged). The frame carries, per
settlement, a 12-bit mask of reserved ring-2 positions and one byte per district (which hex it took), so the renderer never decides
which hex a building stands on (31 §4 still places small items inside each hex, which does not affect the rules).

### 3.2 What triggers growth

`N = whole-hex districts other than the Sacred one + homes hexes`, where

* districts: Citadel (castle, concentric castle), Learning (academy, university, research lab), Civic (theatre or arena, museum),
  Health (bathhouse, hospital), each Wonder that is not an outer work (Great Dome, Great Library, Gothic Cathedral, ziggurat), and a
  district **counts from the season its build is queued**, so the site is clear before work starts;
* the Sacred district (shrine, temple, colonnade) keeps its ring-1 hex and the palace is the Heart landmark: neither adds a hex;
* homes hexes: a City (tier 2) takes one at 120 people and a second at 180 (P, `CoreHomesPerHex = 60`, the hex's 7 sub-slots × about 9
  people per tiled house);
* half and small items never add a hex: they fill ring 1 and the spare petals of district hexes;
* N falls when a building is destroyed or a City loses people; the hex is released at the next season and may be farmed again.

### 3.3 Which hexes (fixed order)

The **Front** is the first ring-1 direction (ascending 0 to 5) that is water, then river, then the direction of the nearest road or trail,
else `siteTile % 6` (doc 31 §4.2, now computed in the sim). The 12 ring-2 positions are numbered from the Front: corner hex *k* lies two
steps out in direction F + k; edge hex *k* lies between corners *k* and *k* + 1. They are taken in this order, **back first, the gate side
last**, corners before edges at the same angle:

`corner 3, edge 2, edge 3, corner 2, corner 4, edge 1, edge 4, corner 1, corner 5, edge 0, edge 5, corner 0`

skipping any hex that is (a) water, a lake, mountain, peak or ice; (b) holding a valuable deposit (metal, salt, coal, and later saltpetre
and sulfur), **known or not**, as in doc 31 §3.1; (c) crossed by a river through its middle; (d) inside another settlement's
seven-hex core (cannot happen with today's spacing; kept as a guard); (e) off the map. Stone, flint and clay are not skipped. Hexes holding
a mining camp are skipped by (b).

Districts then pick among the reserved hexes by **affinity** (Wonder straight behind, Citadel toward the Watch side, Learning beside the
Sacred hex, Civic and Health toward the Front, homes beside the homes quarters), in the priority order Wonder, Citadel, second Wonder,
Learning, Civic, Health, homes; ties go to the lower position number. This is part of the same pure function.

### 3.4 Fields already there

At the next season start `ReleaseCorePlots` (doc 31) releases any field, orchard or terrace on a reserved hex, the owner's or a
neighbour's, in ascending settlement and tile order, and the planner re-places it next season. A pasture count is unaffected (pastures
have no tile). Canals on a released field go with it. No compensation is needed: the measured loss is 2.4 plots per growing City at minute 120 (1.6 at minute 100) and 1.7 per growing Town, against 9.3 plots (4.6 of them fields) that a full 19-hex flower would evict from every City (M).

### 3.5 When there is not enough room

If fewer usable ring-2 hexes exist than N, the homes hexes are given up first (the houses cluster in threes, as in 31 §4.3), then the
lowest-priority districts double up: two whole buildings share one hex at about 0.7 scale (theatre with bathhouse, academy with a Wonder),
and only then is anything drawn at half size in the Workshops hex (31 §4.4). In the live sample this happened to no settlement (0 of 545 settlement-samples; 96 to 100% of Cities have at least 4 usable ring-2 hexes, the worst had 3) (M).

### 3.6 Siting, fission and expansion

* New settlements (fission, Beacon, Found, Tin Folk, world generation) keep doc 31's clean seven-hex core; **no extra rule is needed for
  ring 2**: a site with a poor ring 2 can still grow, it just doubles up later. A soft preference is not recommended (it would push
  settlements toward open plains and change the map for a late-game picture).
* No new spacing rule: the minimum spacing of 5 already keeps a new seven-hex core at least one hex clear of every possible ring-2 hex
  of its neighbours (5 − 2 − 1 = 2), so a grown city never blocks a site that is legal today, and the map capacity of doc 31 §6.2 holds.
* Fission does not split districts: the buildings stay with the mother settlement.
* Conquest keeps the buildings and hence the hexes.

### 3.7 What else changes

| Part | Change |
|---|---|
| `PlaceField`, `PlaceOrchard`, `NextTerraceTile`, `NoFieldTileLeft` | skip tiles in any settlement's reserved core (seven hexes plus reserved ring-2 hexes) |
| `ReleaseCorePlots` | as doc 31, over the reserved set |
| Build queue | a district build reserves its hex when queued |
| Frame | `SettlementCoreRing2` (ushort mask) and `SettlementDistrictHex` (byte per district) |
| Content | a `size` and `chain`/`district` field per structure (validator: every core structure must declare both), constants `CoreHomesPerHex: 60` |
| Tests | stateless rule: same input, same hexes; no plot on a reserved hex after a season; no reserved hex on a valuable deposit |

Determinism: integer hex distance and ordered lists only; no floats, no dictionaries, no new random streams (D1 to D10 hold).
Goldens: changes only where fields move off reserved hexes (politics, Bronze and research goldens likely; toy no).

## 4. The slot map for a grown core

### 4.1 Ring 1 (unchanged from 31 §4.2)

Heart (hearth, banner, council or palace landmark), Front (gate or quay, well, aqueduct tower), Homes A, Homes B, Workshops (granary,
pottery, forge, crane, press, racks, quern), Watch (lookout, drill ground, spear rack), Sacred (totem → shrine → temple → colonnade).
The castle no longer takes the Watch hex: it is always a ring-2 Citadel on the wall line, which frees the Watch hex for the drill ground.

### 4.2 Ring 2 districts

| District | Holds (only the best tier is drawn) | Shares the hex with | Faces |
|---|---|---|---|
| Wonder (up to 4) | Great Dome, Great Library, ziggurat, Gothic Cathedral | nothing | straight behind the Heart, then either side |
| Citadel | castle → concentric castle; the drill ground moves in once a castle stands | soldiers | toward the Watch quarter, on the wall |
| Learning | school (half, ring 1) → academy → university → research lab → computing center | an observatory | beside Sacred |
| Civic | theatre or arena (+ museum, concert hall as halves from E7) | | toward the Front side |
| Health | bathhouse (+ hospital as a half from E5; sewer has no footprint) | the aqueduct tower and the well when Front is full | toward the Front side |
| Homes C, D | houses one per sub-slot, clusters of three when full | | beside Homes A and B |

### 4.3 Merging rules (all eras)

1. **Upgrade-replace** (31 §4.5) plus the new chains: Learning, Citadel, Council (palace → Renaissance palace), Mill (water mill,
   sawmill, windmill: one mill site per river), Perimeter (+ star fort).
2. **Districts**: a new building whose purpose matches a district joins it as a half instead of taking a hex (museum into Civic, hospital
   into Health). A district hex holds at most one whole building or two halves.
3. **Industry outside**: anything that burns fuel at scale, needs a river or wind site, or is dangerous (blast furnace, powder mill,
   factories, power) is an outer work on rings 2 to 4, on a tile with no plot, never on a reserved hex.
4. **New content must declare** its class and chain or district; the validator refuses a new whole-hex building that is neither a tier
   of an existing chain nor a member of a district, unless it is a Wonder. That is the **budget**: at most 4 Wonder hexes + 4 districts
   + 2 homes hexes = 10 of 12.

## 5. Visual check (renderer prototype)

### 5.1 What was built

In the worktree `agent-a2ddcf3c980c17cde` (branch `worktree-agent-a2ddcf3c980c17cde`), on top of the doc-31 WP1/WP2 prototype copied
from `agent-afd063ce7ba43f74e`: `core_layout.gd` computes the districts a settlement needs, reserves ring-2 hexes in the fixed order of
§3.3 (using the renderer's own view of terrain, rivers, known deposits and other settlements' cores, since the sim does not send them
yet), assigns districts by affinity, places each whole building on its district hex, extends the wall round every hex in use, paints
village ground on each reserved hex and keeps trees and garden cues off them. During the prototype, `--core-v1` selected the old
seven-hex layout; WP6c removed that switch and made the simulation-claimed layout the default. The `core-gallery` capture gains a
wide top view and a wide angled view. `tools/check_core_layout.py` gains check 5 (reserved hexes at
distance 2, outside other cores, one per district, every whole building drawn whole and none dropped, no garden cue on a reserved hex).

Current command: `Godot --path game --resolution 1600x900 --always-on-top -- --scenario=core-gallery --start-minute=30 --viewer=-1
--dev-core-gallery --looks-debug --layout-dump=F --shots=DIR`. Images in `builds/core-gallery-v2/`: `v1_*` are the seven-hex
prototype, `after_*` the grown core (`_wide`, `_wideangle`, `_top`, `_close` and the default view per stage). The checker passes on
9 settlements, 592 items, 15,574 footprint pairs (M).

### 5.2 What the images show

* **Walled Iron city** (`after_iron_wide`, `after_iron_wideangle`, against `v1_iron_top`): 7 ring-2 hexes (Great Dome, Citadel, ziggurat,
  Learning, Civic, Health, Homes C). Every building the first prototype dropped is present at full size: academy, theatre, bathhouse,
  aqueduct (tower in the Front, spans out over the river outside the wall), drill ground (in the Watch hex the castle freed), castle,
  ziggurat, dome, and the colonnaded temple at full size in the Sacred hex. Nothing whole-hex is dropped (only knowledge dressing such as
  pots and baskets, as before). One stone wall with towers and the gatehouse follows the 14 hexes in use; the gate stays on the Front.
  It reads as a planned city: landmarks on the rim, homes and workshops round the hearth.
* **City** (`after_city_wideangle`): 110 people with an academy: one ring-2 hex (Learning), no homes hex yet. The academy sits in a
  walled bump behind the temple.
* **Band, Village, Town, coastal town**: unchanged from the seven-hex prototype (no district, no ring-2 hex).

### 5.3 What still looks wrong

1. **The wall is spiky.** Following each hex edge gives a zig-zag outline, with deep notches where a ring-2 hex is not taken (on the gate
   side of the Iron city, and both sides of the City's single bump). A later pass should cut corners: draw the wall along the outer
   edges of the hexes in use but straight across any notch one hex deep.
2. **A single ring-2 hex looks like an annex** (the City). Acceptable for one building, but a City with one district might look better
   with the district hex chosen next to the homes, or with the wall left round the seven hexes and the district outside it as a walled
   precinct. Owner's taste.
3. **The temple is a plain red box from above** at full size; its mesh was made to be seen at half size from an angle.
4. **The ground patches** under ring-2 hexes are circles, so grass shows in the corners near the wall.
5. **Ring 1 looks emptier than before** in the Iron city because the big buildings moved out; houses could spread to fill (more of
   the population drawn as single houses) now that there is room.
6. The renderer uses only **known deposits** to skip hexes; the sim rule would use all, so the drawing could differ on an undiscovered
   deposit until the sim sends its mask (§3.1).
7. Not checked: a coastal or river city with ring-2 hexes over water, a mountain city with few usable hexes, frame time.

## 6. Questions for the owner

1. **Grow hex by hex for each big building (recommended), or give every City the full 19-hex flower at once?** Hex by hex keeps fields
   where nothing is built and also serves crowded Towns; the full flower is simpler to explain but empties 12 hexes of farmland when a
   town becomes a City.
2. **Keep the home range at 3 hexes for Cities (recommended)?** A bigger range for Cities adds about a third more wild food to a third of
   all settlements. Fields may use ring 4 only as a fallback if a test shows a food gap.
3. **Industry outside the walls (recommended)?** Factories, mills, furnaces, power stations and monasteries stand in the countryside like
   mines, not inside the city.
4. **Gothic Cathedral as its own Wonder hex (recommended) or as the temple's top tier?** Its own hex makes it a landmark and keeps the
   temple's colonnade; the alternative saves a hex.
5. **Every new building must upgrade an old one or join a district (recommended)?** This is the budget that keeps the city at 19 hexes
   in every future era; a Wonder is the only exception.
6. **Castles always on the rim (recommended)?** A castle takes a ring-2 hex on the wall line even in a Town, which frees the Watch hex
   for the drill ground.

## 7. How it was measured

* Building audit: the registries and catalog on main (0cc3ea0), `codex/e5-fief` (worktree `civsim-e5`, ebf5d2a), docs 03a to 03c, 08,
  25 to 27; a script listing every `build` effect. Other branches were checked by name only.
* Live games: a throwaway console program in the session scratchpad (`cityaudit/`) referencing the Release `CivSim.Sim.dll` and
  `civsim.pack` of the `core-audit` worktree (agent-a9c09503b5af7a984, 0cc3ea0), the same set-up as `core-audit` (Iron rules, Builder,
  Grower, Warlord and Sage AIs at Warlord level, omniscient frame), seeds 1 to 4, minutes 80, 100 and 120. Tiers come from
  `LivingEconomyModel.Tier`, Iron works from the frame, temples, palaces and ziggurats from `Civic()`. The ring-2 reservation is the
  §3.3 order with the Front taken as the first water, then river, neighbour (no trail term; E). Field yield is the base
  `FieldYieldPerYear` only. Not measured: Faith & Fief and later (no such games exist), frame time, coastal and mountain cities in the
  renderer.
* Renderer: one Godot capture of the dev gallery, checked by eye and by `check_core_layout.py`.

## Owner decisions (2026-11-10)

The owner approved all six recommendations above:

1. The core grows hex by hex, one hex per big building (not a full 19-hex flower at City).
2. The City working range stays at 3. Fields may use ring 4 only as a fallback if a food gap shows up.
3. Industry (factories, mills, furnaces, powder mills) and monasteries are outer works outside the walls.
4. The Gothic Cathedral is its own Wonder hex, not the temple's top tier.
5. Every new building must upgrade an older one or join a district (Wonders excepted).
6. Castles always sit on the outer ring, on the wall line.

Earlier decisions that still stand (doc 31 §10): keep gathering on core hexes; drop the extra ring; new villages avoid
undiscovered deposits; draw only what a village built; walls are one evolving line (picture only); old saves keep loading.

Status (2026-11-11): WP6a is implemented on `codex/wp6a-outer-hex`: districts and City homes are derived from saved building and
population state, `OuterCoreHexes` exposes the sim's deterministic answer, plots are released at the next season boundary, and new
seasonal camps avoid claimed tiles. The simulation keeps no extra save data. Build, strict validation, all 1,177 tests including the
four-seed slow gate, and the toy, research, politics, worldgen, Bronze and content-pack golden checks pass. The four-seed enforced
audit is in [31b §5.6](31b-outer-hex-growth-spec.md#56-wp6a-enforced-rule-check). Next: WP6b (append frame data and have the renderer
read the sim's result); WP6c then makes that layout the default. The renderer prototype and gallery checker remain in the separate
renderer worktrees listed above.
