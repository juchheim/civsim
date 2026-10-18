# 03c — Research Catalog: REACH

> **Theme purpose: Territory and contact.** Reach decides how far your people travel and see, how strongly they claim and hold land, how they deal with rivals (trade, treaty or war), and how fast they push the frontier of knowledge. A Reach-leaning civilization spreads fast, trades widely and contests borders.
> Notation is in [02-research-structure.md §16](02-research-structure.md#16-catalog-notation). Overview is in [03-research-catalog.md](03-research-catalog.md). **Keystones** are at the bottom of this file.

Lineages: [WAY](#way--wayfinding--cartography) · [TRN](#trn--land-transport) · [SEA](#sea--watercraft--seafaring) · [TRD](#trd--exchange--commerce) · [COM](#com--language-writing--communication) · [NUM](#num--number-measure--time) · [TER](#ter--claim--settlement) · [DEF](#def--defense--fortification) · [WAR](#war--arms--warfare) · [DIP](#dip--contact--diplomacy) · [INQ](#inq--inquiry--natural-science) · [KEY](#keystones)

---

## WAY — Wayfinding & Cartography
*Purpose:* vision, exploration, map knowledge and route efficiency. Exploration triggers **exposure gates** (`[Seen: …]`), so Wayfinding unlocks research across all three themes.
*Practice:* scouting, long-distance travel. *Anchors:* landmarks and trails; seasonal rounds; star navigation; sketch maps (Pavlov ~25 ka; Mezhyrich); celestial navigation (Polynesia, Phoenicia); periplus; Ptolemy's *Geography*; magnetic compass (China ~11th c.); portolans and astrolabe; Harrison's chronometer (1761); triangulation surveys; aerial photography and radar; GPS (1978–1995); GIS.

| ID | Project | Era | Type | Requires | Effect |
|----|---------|-----|------|----------|--------|
| WAY-01 | Landmarks & Trails | E0 | M | — | Scouts range +50%; trails form on frequently walked tiles (move +20%). Starter project. |
| WAY-02 | Seasonal Round | E1 | I | WAY-01, FOR-02 | Bands migrate between planned seasonal camps: famine −30%; each settlement founded in E1–E3 sets up **2 seasonal camps** 1–3 tiles beyond its claim radius; each camp **claims radius 2 at 50% of its band's strength** and stays as a satellite camp (fishing, herding, hunting) until town and city radii absorb it ([07b §9.4](07b-territory-findings.md#94-seasonal-camps-as-claim-sources-adopted-2026-10-18)). *KEY-2 prerequisite.* |
| WAY-03 | Star Paths | E1 | M | WAY-02, INQ-02 | Night travel; orientation without landmarks: scouts never get lost. |
| WAY-04 | Sketch Maps | E2 | D | WAY-03, ART-03 | Shared maps: explored terrain stays revealed for the whole civilization; settler site choice improves. |
| WAY-05 | Celestial Navigation | E3 | M | WAY-04, NUM-06 | Sea voyages out of sight of land (open-sea hops ×2 distance). |
| WAY-06 | Periplus & Itineraries | E4 | M | WAY-05, COM-07 | Written route guides: trade route efficiency +25%. |
| WAY-07 | Grid Cartography | E4 | M | WAY-06, NUM-07, LRN-05 | Coordinate maps: **Reveal:** coastline outlines of the known world; Beacon range +. |
| WAY-08 | Magnetic Compass | E5 | M | WAY-07, INQ-06 | All-weather navigation: sea travel losses −50%. *KEY-6 prerequisite.* |
| WAY-09 | Portolan Charts & Astrolabe | E5 | M | WAY-08, NUM-08 | Accurate sea charts and latitude: sea routes +30% speed. |
| WAY-10 | Marine Chronometer | E6 | M | WAY-09, MEC-09 | Longitude solved: ocean routes optimal; shipwrecks −70%. |
| WAY-11 | Triangulation Survey | E7 | M | WAY-10, NUM-11 | National surveys: **Reveal:** full terrain in own territory; planners place infrastructure optimally. |
| WAY-12 | Aerial Survey & Radar | E8 | M | WAY-11, TRN-15, INQ-15 | **Reveal:** large radius around airfields; enemy movements visible. |
| WAY-13 | Satellite Constellation | E9 | S | WAY-12, INQ-17 | **Build:** Launch site; satellites. **Reveal:** entire map; perfect navigation. *KEY-10 prerequisite.* |
| WAY-14 | Geographic Information Systems | E9 | M | WAY-13, MEC-15 | Planners optimize land use: all settlement layouts +10% efficiency. |

## TRN — Land Transport
*Purpose:* **move and carry**. Haul capacity and travel speed decide how far a settlement's workers can reach, how fast goods flow, and how quickly a civilization can respond on its borders. Hosts the **Haul** and **Travel** ladders.
*Practice:* hauling, long walks, riding, driving. *Anchors:* tumplines; dog travois; skis (~8000 BCE); pack donkeys; wheeled carts (~3500 BCE); horse riding; Persian and Roman roads; stone bridges; stirrup (~4th c. China; Europe ~8th c.) and horseshoe; turnpikes; canals; railways (1825); automobiles (1886); highways; aviation (1903); containers (1956); high-speed rail (1964+) and EVs.

| ID | Project | Era | Type | Requires | Effect |
|----|---------|-----|------|----------|--------|
| TRN-01 | Tumplines & Carry Frames | E0 | M | FIB-01 | ▸ Haul: Tumpline. Carry capacity +30%. |
| TRN-02 | Travois & Sledges | E1 | M | TRN-01, HUS-01 | ▸ Haul: Dog travois ×2. Camps relocate faster. |
| TRN-03 | Snowshoes & Skis | E1 | M | TRN-02, FIB-03, [Climate: Cold] | Winter travel speed ×2; snow tiles no longer slow figures. |
| TRN-04 | Pack Animals | E2 | M | TRN-02, HUS-03 | ▸ Haul: Pack animal ×3. |
| TRN-05 | Wheeled Carts | E3 | M | TRN-04, MEC-03, NRG-01 | ▸ Haul: Ox cart ×5 on flat or road tiles. |
| TRN-06 | Horseback Riding | E3 | M | TRN-04, HUS-07 | ▸ Travel: Riding ×2 for scouts, messengers and herders. |
| TRN-07 | Paved Roads | E4 | S | TRN-05, STN-11 | **Build:** Road. Move ×1.5 on roads; claim strength along roads +; adoption spreads along roads ×2. |
| TRN-08 | Stone Bridges | E4 | S | TRN-07, BLD-09 | **Build:** Bridge. Rivers no longer block roads. |
| TRN-09 | Stirrup & Horseshoe | E5 | M | TRN-06, MET-10 | Riders ×1.3 speed and endurance; heavy cavalry possible. |
| TRN-10 | Turnpikes & Coaches | E6 | M | TRN-08, GOV-11 | Maintained toll roads: road speed ×1.5; scheduled coaches (messages and people). |
| TRN-11 | Canals & Locks | E6 | S | TRN-10, MEC-09 | **Build:** Canal. Bulk haul inland ×10 (coal, grain). |
| TRN-12 | Railways | E7 | S | TRN-11, NRG-07, MET-14 | **Build:** Railway. ▸ Haul: Rail ×20; ▸ Travel: Rail ×5. *KEY-8 prerequisite.* |
| TRN-13 | Automobiles & Trucks | E8 | M | TRN-12, NRG-10 | ▸ Haul/Travel: Motor. Door-to-door logistics; suburbs. |
| TRN-14 | Highways | E8 | S | TRN-13, BLD-14 | **Build:** Highway. Motor speed ×2; national market integration. |
| TRN-15 | Aviation | E8 | D | TRN-13, MET-16 | **Build:** Airfield. ▸ Travel: Air (ignores terrain); air power. |
| TRN-16 | Intermodal Containers | E9 | M | TRN-14, SEA-16 | Container logistics: haul cost −70% for long-distance trade. |
| TRN-17 | High-Speed Rail & EVs | E9 | M | TRN-16, NRG-15 | Fast clean transit: commute radius ×2 (cities draw labor from far); transport pollution −60%. |

## SEA — Watercraft & Seafaring
*Purpose:* crossing water. Opens islands, coasts, other continents and maritime trade. Water is the biggest barrier on the map and the fastest highway once mastered.
*Practice:* paddling, fishing from boats, sailing. *Anchors:* rafts (Sahul crossing ≥ ~50–65 ka); hide boats; dugouts (Pesse ~8000 BCE); outriggers (Austronesian); plank boats; sail (Egypt ~3000 BCE); harbors and the Pharos (~280 BCE); galleys and triremes; keeled longships and cogs; lateen sail and sternpost rudder; caravels (15th c.); galleons; steamships (1807+); steel hulls; container ships (1956).

| ID | Project | Era | Type | Requires | Effect |
|----|---------|-----|------|----------|--------|
| SEA-01 | Floats & Rafts | E0 | D | WOD-01, [Near: Water] | **+Action:** Cross rivers and lakes. Narrow straits passable. |
| SEA-02 | Bark & Hide Canoes | E1 | M | SEA-01, FIB-03 | ▸ Paddle: Canoe. River travel ×2; lake fishing. |
| SEA-03 | Dugout Canoes | E2 | M | SEA-02, WOD-03 | Coastal travel; offshore fishing. |
| SEA-04 | Outriggers | E2 | M | SEA-03, WAY-03 | Stable sea canoes: island-hopping up to 3 ocean tiles. |
| SEA-05 | Plank Boats | E3 | M | SEA-04, WOD-05 | Cargo boats: water haul ×3. |
| SEA-06 | Sail | E3 | M | SEA-05, FIB-07 | ▸ Sail: Square sail. Sea speed ×2; coastal trade routes. |
| SEA-07 | Harbors | E4 | S | SEA-06, BLD-09 | **Build:** Harbor. Ship capacity ×2; port cities grow +20%. |
| SEA-08 | Great Lighthouse | E4 | W | SEA-07, BLD-11 | **Wonder:** Lighthouse. Shipwrecks −50% civilization-wide; sea trade +20%; landmark claim. |
| SEA-09 | War Galleys | E4 | M | SEA-07, WAR-07 | Naval combat; blockade enemy harbors. |
| SEA-10 | Keeled Ships & Cogs | E5 | M | SEA-06, WOD-10 | Sturdy hulls: open-sea routes; cargo ×2. |
| SEA-11 | Lateen Sail & Sternpost Rudder | E5 | M | SEA-10 | Sail upwind: sea route speed +40%. |
| SEA-12 | Caravels & Ocean Voyages | E6 | M | SEA-11, WAY-09 | **Ocean crossing:** reach other continents; overseas contact. |
| SEA-13 | Galleons & Ships of the Line | E6 | M | SEA-12, WAR-12 | Armed ocean ships: naval dominance; protected trade fleets. |
| SEA-14 | Steamships | E7 | M | SEA-12, NRG-07 | Wind-independent: sea speed ×2, schedules reliable. |
| SEA-15 | Steel Hulls | E8 | M | SEA-14, MET-15 | Large ships: cargo ×5; battleships. |
| SEA-16 | Container Ships | E9 | M | SEA-15, TRD-14 | Global shipping: trade volume ×5 at minimal cost. |

## TRD — Exchange & Commerce
*Purpose:* getting what your land lacks (tin, salt, silk, oil) without conquering it; wealth; peaceful contact; diffusion. Trade is the alternative to war for satisfying `[Has: …]` gates.
*Practice:* trading, bartering, transporting goods. *Anchors:* gift exchange; obsidian and shell networks (~100 ka+); barter; Neolithic exchange; marketplaces; market towns; coinage (Lydia ~600 BCE); Silk Road; banking and bills of exchange (Italy, 13th c.); double-entry bookkeeping (Pacioli 1494); joint-stock companies (VOC 1602); stock exchange (Amsterdam); industrial capitalism; central banking; Bretton Woods and GATT (1944–47); e-commerce (1990s); global supply chains.

| ID | Project | Era | Type | Requires | Effect |
|----|---------|-----|------|----------|--------|
| TRD-01 | Gift Exchange | E0 | D | SOC-02 | **+Action:** Exchange gifts with other bands: goodwill (first contacts peaceful +40%). |
| TRD-02 | Barter | E1 | M | TRD-01, COM-02, ART-01 | **+Action:** Trade with other civilizations. Goods move between peoples; `[Seen]` gates triggered by traded goods. |
| TRD-03 | Exchange Networks | E2 | I | TRD-02, WAY-04 | Persistent trade routes between settlements and with neighbors: luxury goods flow; diffusion active along routes. *KEY-3 prerequisite.* |
| TRD-04 | Marketplaces | E3 | S | TRD-03, SOC-07 | **Build:** Market. Internal goods distribution +30%; **Role:** Trader. |
| TRD-05 | Market Towns | E3 | I | TRD-04, TRN-05 | Towns specialize in trade: settlements at route crossroads grow +25%. *KEY-4 prerequisite.* |
| TRD-06 | Coinage | E4 | M | TRD-05, MET-07 | Money: trade friction −40%; taxes (admin capacity +). |
| TRD-07 | Long-Distance Trade Routes | E4 | I | TRD-06, (HUS-09 \| SEA-07) | Caravan (overland) or maritime (harbor-to-harbor) trade: route length ×3; Silk-Road / Indian-Ocean–style network. |
| TRD-08 | Banking | E5 | I | TRD-07, NUM-08 | Credit and bills of exchange: trade volume ×1.5; projects funded (Wonder build +15%). |
| TRD-09 | Double-Entry Bookkeeping | E5 | M | TRD-08, NUM-09 | Accurate accounting: trade profit +20%, admin waste −. *KEY-6 prerequisite.* |
| TRD-10 | Joint-Stock Company | E6 | I | TRD-09, SEA-12 | Pooled capital: overseas ventures, colonies and big works ×1.5 speed. *KEY-7 prerequisite.* |
| TRD-11 | Stock Exchange & Insurance | E6 | I | TRD-10, COM-11 | Risk pooling: losses from wrecks and disasters −50%. |
| TRD-12 | Industrial Capitalism | E7 | I | TRD-11, MEC-10 | Investment in factories: factory build speed ×2; boom/bust cycle events. |
| TRD-13 | Central Banking | E8 | I | TRD-12, GOV-13 | Economic stabilization: bust events −60%. |
| TRD-14 | Global Trade Order | E8 | I | TRD-13, DIP-12 | Tariff agreements: trade with *all* contacted civilizations +30%; diffusion +. *KEY-9 prerequisite.* |
| TRD-15 | Digital Finance & E-Commerce | E9 | M | TRD-14, COM-16 | Instant global markets: internal distribution near-frictionless. |
| TRD-16 | Global Supply Chains | E9 | M | TRD-15, TRN-16 | Just-in-time production: craft output +20%, but vulnerable to disruption events. |

## COM — Language, Writing & Communication
*Purpose:* coordination, record-keeping and information speed. Communication speeds adoption, reduces Overreach, and is the backbone of the Information era.
*Practice:* talking, storytelling, writing, messaging. *Anchors:* calls and gestures; spoken language; symbols (Blombos engravings ~75 ka); signal fires and drums; writing (Uruk ~3200 BCE; Egypt; China; Mesoamerica); papyrus and parchment; alphabet (~1800 BCE); Persian and Roman relays; paper (China ~105 CE); movable type (Bi Sheng ~1040; Gutenberg ~1450); newspapers (1605); telegraph (1837–44); telephone (1876); radio (1895); TV (1920s–50s); Internet (ARPANET 1969; Web 1991); mobile phones; social media (2000s).

| ID | Project | Era | Type | Requires | Effect |
|----|---------|-----|------|----------|--------|
| COM-01 | Calls & Gestures | E0 | D | — | Coordinated groups: hunt +10%; danger warnings (predator deaths −20%). Starter project. |
| COM-02 | Spoken Language | E0 | I | COM-01 | Full language: teaching `p_teach` ×2; planning (planners look 1 season ahead). *KEY-1 prerequisite.* |
| COM-03 | Symbols & Marks | E1 | D | COM-02, ART-01 | Marks carry meaning: claims marked; property and ownership marks. |
| COM-04 | Signal Fires & Drums | E2 | M | COM-03, FIR-03 | Alerts cross territory instantly: defense response time −70%. |
| COM-05 | Writing | E3 | D | COM-03, NUM-03 | **Role:** Scribe. Records: knowledge not lost; admin capacity +. *KEY-4 prerequisite.* |
| COM-06 | Papyrus & Parchment | E3 | M | COM-05, FIB-03 | Portable writing: scribe output ×2; letters travel. |
| COM-07 | Alphabet | E4 | M | COM-06 | Literacy easy to learn: scribes ×2; Commons Insight +5%. |
| COM-08 | Courier Relays | E4 | I | COM-07, TRN-07 | Postal relays on roads: Overreach −20%; adoption across distance ×2. |
| COM-09 | Paper | E5 | M | COM-08, FIB-07 | Cheap writing: all writing-based roles +30%. |
| COM-10 | Movable Type | E5 | D | COM-09, MEC-05, MET-06 | Printing press: books abundant; Insight +15%; diffusion ×1.5. *KEY-6 prerequisite.* |
| COM-11 | Newspapers & Pamphlets | E6 | M | COM-10 | News spreads: Cohesion responds to events faster; public opinion (GOV effects +10%). |
| COM-12 | Telegraph | E7 | D | COM-11, INQ-11 | **Build:** Telegraph lines. Instant messages between settlements: Overreach −40%; coordination of rail and armies. *KEY-8 prerequisite.* |
| COM-13 | Telephone | E8 | D | COM-12, NRG-09 | Voice at distance: business and admin efficiency +15%. |
| COM-14 | Radio | E8 | D | COM-13, INQ-15 | Broadcast: national Cohesion +; soft power reaches abroad. |
| COM-15 | Television | E8 | D | COM-14, ART-12 | Mass culture at home: Cohesion +10%; soft power ×1.5. |
| COM-16 | Internet | E9 | S | COM-15, MEC-15, CER-15 | **Build:** Data center and backbone. Data links glow between cities: Insight +25%, adoption instant in connected areas. *KEY-10 prerequisite.* |
| COM-17 | Mobile Networks | E9 | M | COM-16, NRG-15 | Everyone connected: rural settlements gain urban Insight rates. |
| COM-18 | Social Media | E9 | I | COM-17, ART-14 | Viral culture: soft power ×1.5; Cohesion volatility (random swings ±). |

## NUM — Number, Measure & Time
*Purpose:* counting, calendars and mathematics, so planning, building, trade and science work. Feeds planners' foresight and many cross-theme prerequisites.
*Practice:* counting stores, trading, surveying, calculating. *Anchors:* tally bones (Lebombo ~43 ka; Ishango ~20 ka); lunar notation; clay tokens (~8000 BCE); standard weights; Babylonian place value; solar calendars; Egyptian and Greek geometry; zero and Hindu-Arabic numerals; al-Khwarizmi's algebra; Napier's logarithms (1614) and Descartes; calculus (1660s–80s); probability and statistics; Boole (1854) and Turing (1936); data science.

| ID | Project | Era | Type | Requires | Effect |
|----|---------|-----|------|----------|--------|
| NUM-01 | Tally Marks | E1 | D | COM-02 | Count stores: planners track food stores (stockpile targets). |
| NUM-02 | Lunar Calendar | E1 | M | NUM-01, INQ-02 | Predict seasons: migrations and harvests on time (+10% seasonal yields). |
| NUM-03 | Clay Tokens | E2 | D | NUM-02, CER-02 | Accounting tokens: storage loss to mismanagement −50%; trade debts tracked. *KEY-3 prerequisite.* |
| NUM-04 | Standard Weights & Measures | E3 | M | NUM-03, TRD-04 | Fair trade: trade friction −20%; construction precision (+10% build). |
| NUM-05 | Place-Value Arithmetic | E3 | M | NUM-04, COM-05 | Large-number administration: admin capacity +25%. |
| NUM-06 | Solar Calendar & Astronomy | E3 | M | NUM-05, BEL-06 | Accurate year: farm yields +10%; flood prediction. |
| NUM-07 | Geometry & Surveying | E4 | M | NUM-06 | Land surveying: fields, roads and aqueducts laid out optimally (+15% infrastructure efficiency). |
| NUM-08 | Zero & Positional Numerals | E5 | M | NUM-07, LRN-05 | Efficient calculation: trade and admin +15%. |
| NUM-09 | Algebra | E5 | M | NUM-08 | Abstract problem-solving: Scholar Insight +15%. |
| NUM-10 | Logarithms & Analytic Geometry | E6 | M | NUM-09, COM-10 | Navigation and engineering calculation ×2 speed. |
| NUM-11 | Calculus | E6 | M | NUM-10 | Physics and engineering: INQ projects −10% cost (stacking with Know-how). |
| NUM-12 | Statistics & Probability | E7 | M | NUM-11, GOV-12 | Census analysis, insurance and public health: planners predict famine and epidemics. |
| NUM-13 | Boolean Logic & Computation | E8 | M | NUM-12 | Theory of computation: enables Electronic Computer. |
| NUM-14 | Data Science | E9 | M | NUM-13, MEC-15 | Big data: all planners +10% efficiency; Council auto-picks get smarter. |

## TER — Claim & Settlement
*Purpose:* the rules of **owning land**. Claim radius, claim strength, how fissioned groups choose new sites, how distant land is held, and how expansion overseas works. The lineage most directly tied to Dominion score.
*Practice:* settling new sites, marking territory, patrolling boundaries. *Anchors:* home ranges; seasonal camp circuits; boundary markers and cairns; permanent villages; daughter-village colonization (Neolithic spread); boundary stones (kudurru); city-states; provinces (Persian satrapies, Roman provinces); Greek and Phoenician colonies; manors; overseas colonies; cadastral registries; suburbanization; decolonization (1945–75); smart cities.

| ID | Project | Era | Type | Requires | Effect |
|----|---------|-----|------|----------|--------|
| TER-01 | Home Range | E0 | I | WAY-01 | Claims decay 50% slower after a camp moves on; the home range (½ of scouting reach) shows as a hatch and earns presence. (Camps claim radius 3 from the start.) |
| TER-02 | Camp Circuits | E1 | M | TER-01, WAY-02 | Seasonal camps claim at **75%** of the band's strength (instead of 50%); their claims never decay while the band is elsewhere in the round. |
| TER-03 | Territorial Markers | E1 | M | TER-02, BEL-02 | **Build:** Cairn or totem marker. Claim strength +30% in radius. |
| TER-04 | Permanent Villages | E2 | I | TER-03, BLD-04 | Permanent settlement claim radius 4; claims never decay while occupied. |
| TER-05 | Daughter Villages | E2 | I | TER-04, SOC-06 | Fission bands choose sites by quality and toward Beacons; new villages start with +50% claim strength. |
| TER-06 | Boundary Stones & Land Rights | E3 | I | TER-05, GOV-03 | Claimed tiles resist rival pressure: −30% flip rate. |
| TER-07 | City-States & Hinterlands | E3 | I | TER-06, SOC-09 | Cities project claim radius 7 over their hinterland. |
| TER-08 | Provinces & Governors | E4 | I | TER-07, GOV-07 | **Role:** Governor. Overreach −30% in governed provinces. |
| TER-09 | Frontier Colonies | E4 | I | TER-08, SEA-06 | Planned colonies across water or far away: fission bands can found overseas settlements. |
| TER-10 | Manorial Estates | E5 | I | TER-08, GOV-09 | Rural land held cheaply: claim upkeep −30% for farm tiles. |
| TER-11 | Overseas Colonies | E6 | I | TER-09, SEA-12 | Colonies on other continents; colonial trade +. Overreach risk high (independence events). |
| TER-12 | Land Registry & Cadastre | E7 | M | TER-10, WAY-11 | Precise land ownership: claim strength +20% everywhere; border disputes −50%. |
| TER-13 | Urban Planning & Suburbs | E8 | M | TER-12, TRN-13 | Cities sprawl into suburbs: city claim radius +3. |
| TER-14 | Self-Determination | E8 | I | TER-11, DIP-12 | Former colonies become autonomous allies, not rebels: Overreach losses become Legacy-neutral partnerships. |
| TER-15 | Smart Megacities | E9 | M | TER-13, COM-16 | Megacity density: population per tile ×2; claim radius +2. |

## DEF — Defense & Fortification
*Purpose:* protecting people, stores and claims from raids, predators and conquest. Defense makes claims sticky.
*Practice:* guarding, building defenses, watching. *Anchors:* thorn zarebas; lookouts; palisades and ditches; hillforts; Jericho's walls (~8000 BCE) and Uruk; towers and gatehouses; Hadrian's Wall and the *limes*; castles (10th c.+); concentric castles; *trace italienne* (16th c.); coastal batteries; trenches (1914); radar (1930s); nuclear deterrence (1950s+); cyber defense (2000s+).

| ID | Project | Era | Type | Requires | Effect |
|----|---------|-----|------|----------|--------|
| DEF-01 | Thorn Barriers | E0 | S | WOD-01 | **Build:** Thorn ring around camp. Predator attacks −60%; raids slowed. |
| DEF-02 | Lookouts | E1 | R | DEF-01, COM-02 | **Role:** Watcher. Raids detected early: surprise −70%. |
| DEF-03 | Palisades & Ditches | E2 | S | DEF-02, WOD-03 | **Build:** Palisade. Settlement defense ×2. |
| DEF-04 | Earthworks & Hillforts | E3 | S | DEF-03, MIN-03 | **Build:** Hillfort (refuge for the whole hinterland). |
| DEF-05 | Stone Walls | E3 | S | DEF-04, STN-11 | **Build:** City walls. Defense ×3; besiegers need siege engines. |
| DEF-06 | Towers & Gatehouses | E4 | S | DEF-05, MEC-04 | Wall defense +50%; controlled gates (trade not disrupted in wartime). |
| DEF-07 | Frontier Walls | E4 | S | DEF-06, TER-08 | **Build:** Border wall segments. Border tiles resist flips −50%; raids across walls −70%. |
| DEF-08 | Castle Building | E4 | S | DEF-06, BLD-09 | **Build:** Castle. Holds a region (claim radius 5, strong); garrisons. *KEY-5 prerequisite.* |
| DEF-09 | Concentric Castles | E5 | S | DEF-08, MEC-06 | Castles ×2 defense; siege attrition favors defenders. |
| DEF-10 | Star Forts | E6 | S | DEF-09, NUM-07, WAR-12 | **Build:** Bastion fort. Resists cannon. |
| DEF-11 | Coastal Fortifications | E7 | S | DEF-10, SEA-14 | **Build:** Coastal battery. Harbors immune to blockade by unarmored fleets. |
| DEF-12 | Trenches & Wire | E8 | S | DEF-11, MET-15 | Defensive lines: front-line tiles near-impossible to flip by infantry. |
| DEF-13 | Radar & Air Defense | E8 | S | DEF-12, WAY-12 | **Build:** Radar station. Air raids −60%. |
| DEF-14 | Nuclear Deterrence | E8 | I | DEF-13, WAR-16 | Rivals cannot declare war of conquest on you (raids and proxy pressure still possible). |
| DEF-15 | Cyber Defense | E9 | M | DEF-13, COM-16 | Protects data infrastructure: sabotage of your Apex uplinks −80%. |

## WAR — Arms & Warfare
*Purpose:* taking land and stores by force, and defending them in the field. War is risky (deaths, Cohesion loss, rival alliances), but it is a legitimate path to Dominion. Posture *Raid* uses it.
*Practice:* fighting, drilling, hunting (early). *Anchors:* clubs and thrown stones; raiding; archery in conflict (Nataruk ~10 ka); warrior bands; bronze arms and shields; chariots (~2000 BCE); phalanx and legion; iron weapons; siege engines; knights; crossbows and longbows; gunpowder weapons (China, then 14th c. Europe); pike and musket; rifles and artillery; tanks and aircraft (WWI); nuclear weapons (1945); precision and drone warfare; UN peacekeeping (1948+).

| ID | Project | Era | Type | Requires | Effect |
|----|---------|-----|------|----------|--------|
| WAR-01 | Clubs & Thrown Stones | E0 | M | WOD-01 | Combat strength ×1.5 vs. predators and raiders. |
| WAR-02 | Raiding | E1 | I | WAR-01, HNT-04 | **+Action:** Raid (steal stores, capture tiles). Used by *Raid* posture. |
| WAR-03 | Archery Warfare | E1 | M | WAR-02, HNT-06 | Ranged combat ×1.5; defenders on walls ×2. |
| WAR-04 | Warrior Bands | E2 | R | WAR-03, SOC-05 | **Role:** Warrior. Dedicated fighters: raid success +40%; border patrols. |
| WAR-05 | Bronze Arms & Shields | E3 | M | WAR-04, (MET-04 \| MET-05) | Combat ×1.5; casualties −30%. |
| WAR-06 | Chariots | E3 | M | WAR-05, TRN-05, HUS-07 | Shock and mobility on open terrain ×2. |
| WAR-07 | Professional Armies | E4 | I | WAR-05, GOV-07 | **Role:** Soldier. Standing army: drilled units ×1.5; garrisons hold claims. |
| WAR-08 | Iron Weapons | E4 | M | WAR-07, MET-09 | Cheap iron arms: army size cap ×2. |
| WAR-09 | Siege Engines | E4 | M | WAR-07, MEC-05 | Assault walls: siege time −60%. |
| WAR-10 | Heavy Cavalry | E5 | M | WAR-08, TRN-09 | Mounted shock troops ×2 in open battle. |
| WAR-11 | Crossbows & Longbows | E5 | M | WAR-08, MEC-07 | Ranged troops pierce armor; peasant levies effective. |
| WAR-12 | Gunpowder Weapons | E5 | M | WAR-11, CHM-07 | Cannon and handguns: walls (pre–Star Fort) −70% effective. |
| WAR-13 | Pike & Musket Drill | E6 | M | WAR-12, GOV-12 | Disciplined national armies ×1.5. |
| WAR-14 | Rifles & Artillery | E7 | M | WAR-13, MET-15 | Firepower ×3; casualties high (Cohesion cost of war +50%). |
| WAR-15 | Mechanized Warfare | E8 | M | WAR-14, NRG-10, TRN-15 | Tanks and aircraft: break trench lines; air raids. |
| WAR-16 | Nuclear Weapons | E8 | D | WAR-15, INQ-16 | Ultimate deterrent (enables DEF-14). Use triggers global catastrophe and Legacy penalties. |
| WAR-17 | Precision & Drone Warfare | E9 | M | WAR-15, WAY-13 | Precision strikes: combat ×2 with low own casualties. Infrastructure sabotage (vs. Apex uplinks). |
| WAR-18 | Peacekeeping Forces | E9 | I | WAR-17, DIP-14 | Deploy into neutral or allied zones: conflicts there end; Legacy bonus for peace. |

## DIP — Contact & Diplomacy
*Purpose:* structured relations with rival civilizations: peaceful first contact, alliances, treaties, vassals and global institutions. Diplomacy lets a weaker civilization survive and lets everyone share diffusion.
*Practice:* meeting strangers, hosting, negotiating. *Anchors:* greeting rituals; hospitality norms; marriage alliances; envoys and oaths; Treaty of Kadesh (~1259 BCE); tributary systems; Delian League and confederacies; dynastic marriages; resident embassies (Renaissance Italy); Concert of Europe (1815); Geneva Conventions (1864+); League of Nations and UN; arms control (1963+); G20 and global forums; Paris Agreement (2015).

| ID | Project | Era | Type | Requires | Effect |
|----|---------|-----|------|----------|--------|
| DIP-01 | Stranger Greetings | E0 | D | COM-01 | First contact with strangers is cautious rather than violent: violent first contacts −50%. |
| DIP-02 | Hospitality Customs | E1 | I | DIP-01, SOC-03 | Guests welcomed: contact → diffusion begins; peaceful encounters build Relations. |
| DIP-03 | Marriage Alliances | E2 | I | DIP-02, SOC-04 | **Diplo:** Alliance (no fighting; shared diffusion; joint defense). |
| DIP-04 | Envoys & Oaths | E3 | I | DIP-03, GOV-04 | **Diplo:** Non-aggression pact; **Role:** Envoy. |
| DIP-05 | Written Treaties | E3 | I | DIP-04, COM-05 | **Diplo:** Border treaty (fixes a border line; neither side's claims flip across it). |
| DIP-06 | Tributary States | E4 | I | DIP-05, WAR-07 | **Diplo:** Vassalage (a weaker civilization pays tribute; its land counts 25% toward your Dominion). |
| DIP-07 | Leagues & Confederations | E4 | I | DIP-05, GOV-08 | **Diplo:** League (multi-civ alliance; shared Wonder build). |
| DIP-08 | Dynastic Marriages | E5 | I | DIP-07, GOV-09 | Alliances survive leader deaths; chance of peaceful **inheritance** of a Free Folk region. |
| DIP-09 | Resident Embassies | E6 | I | DIP-08, COM-10 | Permanent embassies: see rivals' research slots; diffusion +. |
| DIP-10 | Balance of Power | E7 | I | DIP-09, GOV-12 | AI civilizations coalition against the leader: comeback mechanic for *you* when behind. |
| DIP-11 | International Law | E7 | I | DIP-10, BEL-13 | Rules of war: civilian deaths −50%; war Cohesion cost −. |
| DIP-12 | International Organizations | E8 | I | DIP-11, COM-14 | **Diplo:** World assembly votes (sanctions, embargoes). |
| DIP-13 | Arms Control | E8 | I | DIP-12, WAR-16 | Nuclear catastrophe risk −90%; military upkeep −20%. |
| DIP-14 | Global Governance Forum | E9 | I | DIP-12, COM-16 | Coordinated global policy: all members' disaster damage −30%. *KEY-10 prerequisite.* |
| DIP-15 | Climate Accords | E9 | I | DIP-14, INQ-19 | Global pollution and climate damage −40% for signatories. |

## INQ — Inquiry & Natural Science
*Purpose:* **pushing the frontier**. Reveals hidden resources (prospecting, geology), unlocks the scientific institutions that cut research costs, and provides the physics behind electricity, the atom, rockets and transistors.
*Practice:* observing, experimenting, measuring. *Anchors:* tracking (an ancient "science"); sky watching; stone and ore lore; Ionian natural philosophy (~600 BCE); Archimedes; Ibn al-Haytham's optics and Peregrinus on magnetism; Galileo's telescope (1609) and Copernicus; scientific method (Bacon, Galileo, Royal Society); Newton (1687); Lavoisier (1789); Faraday and Maxwell; thermodynamics; Lyell and Darwin; genetics (1900 rediscovery); relativity and quantum theory (1905–1927); fission (1938); rocketry (1926–57); transistor (1947); ecology and climate science; molecular biology (1953+).

| ID | Project | Era | Type | Requires | Effect |
|----|---------|-----|------|----------|--------|
| INQ-01 | Tracking & Animal Lore | E1 | M | HNT-03 | **Reveal:** Game herds within 2× sight. Hunting success +20%. |
| INQ-02 | Sky Watching | E1 | M | COM-02 | Seasons read from the sky: migration timing +; enables calendars and star paths. |
| INQ-03 | Stone & Ore Lore | E2 | M | INQ-01, MIN-01 | **Reveal:** Surface ores within sight (copper, tin, gold). Makes `[Seen: …]` metal gates reachable. |
| INQ-04 | Natural Philosophy | E4 | I | INQ-03, BEL-08, NUM-07 | Rational inquiry: Scholar Insight +20%. |
| INQ-05 | Mechanics & Hydrostatics | E4 | M | INQ-04, MEC-05 | MEC and NRG project costs −10%; buoyancy (ships +10% cargo). |
| INQ-06 | Optics & Magnetism | E5 | M | INQ-05, CER-07 | Enables lenses and the compass. |
| INQ-07 | Telescope & Heliocentrism | E6 | D | INQ-06, CER-10 | **Reveal:** Sight from towers ×2; navigation +; challenges dogma (Cohesion −5% unless BEL-12). |
| INQ-08 | Scientific Method | E6 | I | INQ-07, LRN-08 | Systematic experiment: **all project costs −10%**; Insight +10%. *KEY-7 prerequisite.* |
| INQ-09 | Newtonian Physics | E6 | M | INQ-08, NUM-11 | Engineering precision: MEC, NRG and SEA effects +10%. |
| INQ-10 | Modern Chemistry | E7 | M | INQ-09, CHM-08 | CHM and MET projects −15% cost. |
| INQ-11 | Electromagnetism | E7 | D | INQ-10 | Enables generators, the telegraph and motors. |
| INQ-12 | Thermodynamics | E7 | M | INQ-09, NRG-07 | Engine efficiency +40%. |
| INQ-13 | Geology & Evolution | E7 | M | INQ-10, WAY-11 | **Reveal:** Coal and oil basins in territory; breeding theory (FOR and HUS effects +10%). |
| INQ-14 | Genetics | E8 | M | INQ-13, FOR-11 | Crop and livestock improvement ×1.2; enables biotech. |
| INQ-15 | Relativity & Quantum Physics | E8 | M | INQ-11, NUM-12 | Modern physics: enables nuclear, electronics and radio. |
| INQ-16 | Nuclear Physics | E8 | D | INQ-15 | Enables reactors and weapons. |
| INQ-17 | Rocketry | E8 | D | INQ-15, CHM-13 | Rockets: enables satellites (WAY-13). |
| INQ-18 | Transistor | E8 | D | INQ-15, CER-14 | Solid-state electronics: all electronic devices ×10 cheaper. *KEY-9 prerequisite.* |
| INQ-19 | Ecology & Climate Science | E9 | I | INQ-14, NUM-14 | **Reveal:** Pollution and climate overlays; sustainability projects −20% cost. |
| INQ-20 | Molecular Biology & Biotech | E9 | M | INQ-14, CHM-13 | Biotech: MED, AGR and HUS late projects −20% cost; new medicines. |

---

## Keystones

Keystones sit outside the lineages. Each one moves the civilization into the next era ([02 §13](02-research-structure.md#13-keystones-and-eras)). "Breadth" = minimum completed projects from the current era.

| ID | Keystone | Enters | Type | Requires | Breadth | Era effects |
|----|----------|--------|------|----------|---------|-------------|
| KEY-1 | Symbolic Mind | E1 | K | COM-02, FIR-02, STN-04, SOC-01 | 13 × E0 | Behavioral modernity. Hunters era projects become visible; Legends begin to appear. |
| KEY-2 | Sedentism | E2 | K | FOR-04, FOD-03, BLD-03, WAY-02 | 14 × E1 | Year-round camps; first villages possible. |
| KEY-3 | Age of Metal | E3 | K | MET-02, SOC-07, TRD-03, NUM-03 | 20 × E2 | Bronze era; ore deposits highlighted. |
| KEY-4 | Iron & Law | E4 | K | MET-08, COM-05, GOV-06, TRD-05 | 21 × E3 | Iron era; representation scale 1:2; provinces possible. |
| KEY-5 | Feudal Order | E5 | K | AGR-10, GOV-09, BEL-09, DEF-08 | 21 × E4 | Faith & Fief era; scale 1:5. |
| KEY-6 | Printing Revolution | E6 | K | COM-10, TRD-09, WAY-08, LRN-08 | 16 × E5 | Discovery era; scale 1:25; oceans open. |
| KEY-7 | Industrial Revolution | E7 | K | NRG-06, MEC-10, TRD-10, INQ-08 | 14 × E6 | Industry era; scale 1:100; **pollution system activates**. |
| KEY-8 | Electrification | E8 | K | NRG-08, COM-12, TRN-12, MED-11 | 14 × E7 | Modern era; scale 1:250; night lights on the map. |
| KEY-9 | Digital Revolution | E9 | K | INQ-18, MEC-13, LRN-11, TRD-14 | 18 × E8 | **Information (post-modern) era**; scale 1:500; data-link layer appears on the map. |
| KEY-10 | Planetary Network | Apex | K | COM-16, WAY-13, NRG-16, DIP-14 | 17 × E9 | Must be **built** as 5 uplink sites spread across your territory (each ≥ 15 tiles apart). The first to finish wins the **Apex victory**. |
