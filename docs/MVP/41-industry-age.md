# 41 — Industry (E7): Rails, Wires, Smoke and the Factory Town

> **Planning draft, 2026-11-28.** The E7 counterpart of [38 — Discovery](38-discovery-age.md), [27 — Faith & Fief](27-faith-and-fief-age.md) and [25 — Iron Age](25-iron-age.md): which age is next, an audit of what exists, the mechanisms, pacing, acceptance gates, a work breakdown and the decisions the owner needs to make. Nothing here is built. **E7 never changes an E6 rule**, and every E7 number below is a **draft input to an oracle**, not a fitted value.
>
> Owner brief (2026-11-28): the last ages were short; E7 should have **more research nodes** than planned and take **30 to 35 minutes** from the keystone that enters it to the keystone that leaves it (Iron, Faith & Fief and Discovery were each fitted to about 25).
>
> Audit baseline: worktree `civsim-e6`, branch `codex/e6-discovery`, 2026-11-28 (E6 promoted and in the base game). Claims marked *(verified)* were read from the catalog, registries or source in that tree. Claims marked *(assumed)* come from older docs or were not checked in code and must be re-checked in WP-E7-3. Sources: [01 §8](01-game-structure.md), [03 §4.1](03-research-catalog.md), [03a](03a-research-craft.md)/[03b](03b-research-kin.md)/[03c](03c-research-reach.md), [04 §2, §5.5](04-research-paths.md), [05 §14](05-simulation-structure.md), [08](08-ui-and-world-display.md), [09 §5, §10](09-population-and-health.md), [13 §2.2](13-contact-and-conflict.md), [38](38-discovery-age.md), and the E6 pacing reports [slice 3](balance/m13-wp12-pacing-slice3.md), [slice 4](balance/m13-wp12-pacing-slice4.md), [slice 5](balance/m13-wp12-pacing-slice5.md), [held-out](balance/m13-wp13-heldout-slice1.md).

## 0. The short version (what to accept)

- **The next age is E7 Industry**, entered by KEY-7 *Industrial Revolution* and left by **KEY-8 *Electrification***. It becomes a new match chapter, `ChapterMode.Industry = 4`, which contains Discovery (and so every earlier age). KEY-7 stops ending the match; the human's KEY-8 ends it. No rival countdown.
- **The catalog plans 43 projects plus KEY-8.** This draft adds **12 new projects** (55 plus KEY-8), each one carrying a system this doc designs, and raises KEY-8's breadth (the "N/M" count in the top bar) from **14 to 20**. The age then asks for 20 projects instead of 14, keeps 10 free choices instead of 4, and lands a project about every 1.5 minutes, a little faster than E6's 1.7.
- **Length: aim for 32 minutes.** The E6 fit says Insight grows about 6% a minute and is about 4.3 times larger at KEY-7 than at KEY-6. Carrying that forward, a 32-minute age needs about **7 times E6's research bill**, which with a breadth of 20 is an **era base near 110,000** (the registry's typed 280,000 is about 2.5 times too high, as E6's typed 32,000 was). The real number is fitted from live runs, as for every age.
- **The idea of the age:** Discovery opened the horizon and ran out of wood. **Industry sets the machine against the people who work it.** Steam, iron and rail multiply what a town makes and how far it reaches; smoke, crowding, disease and strikes are the price, and public health, law and labor's own organisations are the answers. Most of the answers are optional research; the costs arrive with the factories whether the player researches the answers or not.

## 1. Which age is next, and why

| Check | Finding |
|---|---|
| Newest completed age | E6 Discovery: built, 42 rows numeric, held-out pacing 25.4 min, hand playtest approved, **in the base game since 2026-11-28** ([38](38-discovery-age.md) status) *(verified)*. |
| What enters and leaves E7 | KEY-7 *Industrial Revolution* (`era: 6, enters: 7`) enters it; **KEY-8 *Electrification*** (`era: 7, enters: 8`, `requires: [NRG-08, COM-12, TRN-12, MED-11]`, `breadth: {era: 7, min: 14}`, status draft) leaves it *(verified, `content/catalog/keystones.yaml`)*. [01 §8](01-game-structure.md): E7 is **Industry**, anchor "Industrial Revolution", 1 figure = 100 people. |
| Is any E7 content live? | No. `ChapterRules` has Bronze 0, Iron 1, Fief 2, Discovery 3; Discovery's research ceiling is 6 and KEY-7 ends it *(verified, `Research/ChapterRules.cs`)*. KEY-7's own rule text says "no free ships, population or E7 systems; the pollution system and the Industry rules arrive with the Industry chapter" *(verified)*. |
| E7 catalog state | **43 regular projects plus KEY-8**, every one `status: draft` with one prose effect and no gates *(verified by script over `content/catalog/*.yaml`)*. That matches [03 §4.1](03-research-catalog.md) exactly (Craft 15, Kin 13, Reach 15). Types: **25 M** (Method, cost ×1.0), **7 S** (Structure, ×1.2), **7 I** (Institution, ×1.5), **4 D** (Discovery, ×1.0) and K (Keystone, ×2.5) (`constants/balance.yaml` `type_mult`) *(verified)*. |
| Era row | `eras.yaml` E7: base cost **280,000**, breadth 14, representation 1:100, claim radius **7** (E6: 5), reach **50** (E6: 35), claim multiplier 1.56, fertility multiplier 1.0, child/adult/elder mortality 0.017 / 0.0078 / 0.043, `cost_mod 0.9` *(verified)*. `cost_mod` is the balance model's INQ-08 −10%; the sim never reads it (only the schema does) and INQ-08 is implemented directly ([38](38-discovery-age.md) WP-E6-7), so it must not be applied twice *(verified by grep)*. |

The working name for the match rules is **IndustryChapter**; milestone name **M14**.

## 2. What Industry should feel like

Faith & Fief set the countryside, church and town against each other; Discovery opened the horizon and ran out of wood. **Industry sets the machine against the people who work it.** The player still sets three research slots and watches planners carry out the result.

| Tension | What pushes | What answers it (and what it costs) |
|---|---|---|
| **Steam, iron and steel** | The Watt engine turns coal into power anywhere; puddled iron and Bessemer steel become rails, boilers, girders and rifles. | Everything now burns coal. Seams are finite, deep seams flood and explode, and a town without coal or iron stays in E6. |
| **Rail and wire** | Railways haul ×20 and move armies ×5; the telegraph makes distance cheap for governing; steamships keep schedules. | Rails cost wrought iron and burn coal; lines can be cut; a colony is still a sea away unless a cable reaches it. |
| **The factory town** | Mills, power looms and gasworks multiply output; machines free farm labor and people move to town. | Smoke, crowding and disease; strikes when hours are long and air is foul; booms and busts once capital drives building. |
| **Public health** | Vaccination, waterworks, germ theory and sewers end the age of plague ([09 §10.6](09-population-and-health.md): epidemics fall from 17% of deaths in E6 to 3% in E7). | Each is a staffed or powered work with upkeep; none is free and none is automatic. |
| **Mind and state** | Modern chemistry, electromagnetism, schools and polytechnics; constitutions and secular reason. | Schools and institutes need staff; balance-of-power diplomacy turns rivals against whoever leads. |

## 3. Scope audit

### 3.1 Inventory *(verified from `content/catalog`)*

**43 planned regular projects + 12 proposed (marked \*) = 55, plus KEY-8.** They fall into seven systems; each becomes one work package (§10).

| System | Projects |
|---|---|
| **A. Steam, iron and steel** (13) | NRG-07 Watt engine · INQ-12 thermodynamics · MET-14 puddling and rolling mills · MET-15 Bessemer steel · MIN-11 steam pumping · MIN-12 dynamite and drills · **MIN-17\* safety lamp and ventilation** · CHM-11 high explosives · WOD-11 steam sawmill · MEC-11 machine tools · CHM-09 Portland cement · BLD-14 reinforced concrete · NRG-08 electric generator |
| **B. Rail, wire and steam at sea** (9) | TRN-12 railways · COM-12 telegraph · SEA-14 steamships · HNT-13 steam trawlers · WAY-11 triangulation survey · TER-12 cadastre · **COM-19\* submarine cable** · **SEA-18\* coaling stations** · **TER-16\* colonial administration** |
| **C. The factory town** (8) | FIB-13 power loom and cotton mill · FIR-13 gas lighting · CHM-10 coal-tar dyes · TRD-12 industrial capitalism · SOC-13 labor movement · **GOV-16\* factory acts** · **BLD-18\* terraced housing** · **FIR-16\* smoke abatement** |
| **D. Public health** (7) | MED-10 vaccination · MED-11 germ theory · MED-12 sanitation and sewers · **MED-16\* waterworks and health boards** · NUM-12 statistics · CER-12 optical glass · HUS-12 veterinary science |
| **E. Mind, state and culture** (11) | INQ-10 modern chemistry · INQ-11 electromagnetism · INQ-13 geology and evolution · LRN-10 public schooling · **LRN-15\* polytechnic institutes** · GOV-13 constitutional democracy · BEL-13 secular humanism · ART-11 museums · **BLD-17\* Great Exhibition Hall (Wonder)** · DIP-10 balance of power · DIP-11 international law |
| **F. Food** (4) | AGR-13 seed drill and reaper · **AGR-18\* superphosphate and guano** · FOR-11 plant breeding · FOD-11 canning |
| **G. War** (3) | WAR-14 rifles and artillery · DEF-11 coastal batteries · **SEA-17\* ironclads** |

With the twelve the age is Craft 19, Kin 17, Reach 19, and types 29 M, 10 S, 10 I, 5 D, 1 W.

### 3.2 KEY-8 closure *(verified by script from `requires`)*

KEY-8 requires NRG-08, COM-12, TRN-12 and MED-11. Their in-era closure is exactly **10 projects**: NRG-07, MET-14, TRN-12, INQ-10, INQ-11, NRG-08, COM-12, CER-12, MED-10, MED-11, matching [04 §2](04-research-paths.md) (10 critical, 14 breadth, 4 free). Findings that shape the design:

1. **No E7 row carries a `[Seen]` gate**, so geography cannot hide a KEY-8 prerequisite the way coal once hid KEY-7 ([38 §3.2](38-discovery-age.md)). Research never consumes goods, so a town without iron or coal still *researches* the critical path; it just cannot *use* it.
2. **The critical path is steam, iron, rail, wire, chemistry and medicine.** The whole factory-town system (C), food (F), war (G) and most of E are off it. The costs of industry (smoke from KEY-7, strikes from factories) arrive anyway, so the answers must be cheap enough to be reached: that is the main reason for 10 free picks rather than 4.
3. **KEY-8 needs 13 more E6 rows beyond KEY-7's closure** (CER-11, CHM-08, COM-11, FIR-11, FIR-12, INQ-09, LRN-09, MED-09, MET-13, NUM-10, NUM-11, TRN-10, TRN-11) **and 16 older rows**, most of them the medicine line MED-02 to MED-08 plus BLD-09/10, CER-08, CHM-05/06/07, FIR-10, GOV-11, TRN-08. At E6's fitted base and EraLag 0.7 that backfill is about **267,000 Insight**, three times E6's backfill (79,000). A civilization that ignored medicine pays for it here.
4. **The late-project rule (D9) bites harder.** By the repo's definition (a same-era chain of three or more, `late()` in the oracles) **15 of the 43** planned rows qualify, three of them on the critical path (COM-12, NRG-08, TRN-12); with the twelve new rows it is **22 of 55**. E6 had 8 of 41 and 2 on the path. See D5.
5. **E6 threads E7 picks up.** Nine E6 rows had no E7 consumer: FIR-11, FOR-09, INQ-07, MIN-09, NUM-10, SEA-13, TER-11, TRD-10, TRN-10. The new rows take up coal firing (FIR-16), coal and colonies (SEA-18, TER-16), galleons (SEA-17). Turnpikes are deliberately left behind (railways replace them). [38](38-discovery-age.md) WP-E6-9 left DEF-11 and WAR-14 for later; both are E7 rows. [38 §5.7](38-discovery-age.md) and [05 §14](05-simulation-structure.md) promise that KEY-7 switches pollution on.
6. **Prerequisites outside E7 stay as typed.** E7 may not retype E6 rows; every new row's prerequisites exist and none points to a later era *(verified)*. Every new row has a cross-theme prerequisite (V6).
7. **E8 leans on E7.** 38 E8 rows require E7 rows (assembly line on machine tools, oil drilling on dynamite and geology, grid on the generator and puddled iron, and so on). Nothing here removes an E7 row they need.

### 3.3 The twelve proposed nodes

Ids take the next free number in each lineage (catalog numbering runs across all eras and E8/E9 already used the lower numbers; ids never change, so numbers above E9's are expected). Every row is era 7, draft until WP-E7-13.

| Id | Name | Type | Requires | What it does (typed-effect idea, P) | Why it belongs here |
|---|---|---|---|---|---|
| MIN-17 | Safety Lamp & Mine Ventilation | M | MIN-11, INQ-10 | Cuts the deep-seam firedamp hazard that MIN-11 introduces by 80%; deep coal camps keep a full crew | Steam pumping opens deep coal; explosions are its price. Without an answer, deep coal is a trap |
| COM-19 | Submarine Telegraph Cable | D | COM-12, SEA-14, MET-14 | A cable station at a harbor links overseas towns to the telegraph: COM-12's Overreach relief and colony strain relief reach across the sea; cut by blockade | Colonies (TER-11) are E6's dangling thread; the 1866 cable is the industrial answer |
| SEA-18 | Coaling Stations | S | SEA-14, TER-11, MIN-09 | Steamships burn coal per leg; a stocked coaling station at a harbor or colony extends steam range; beyond it ships sail at E6 speed | Gives SEA-14 a physical price and makes colonies and coal matter at sea |
| TER-16 | Colonial Administration | I | TER-11, GOV-12, SEA-14 | Colony sea strain −50%; colonies send a share of a staple or cane to the mother city; independence odds rise with colony size unless cabled | E6 colonies roll for independence and pay strain; E7 is the age of governed empire, and its price |
| GOV-16 | Factory Acts & Inspectors | I | SOC-13, GOV-12, MEC-10 | Factory coverage capped at 0.85 (less output) for strike odds −50% and the operatives' smoke hazard halved | The price-and-answer pair to SOC-13: law trades output for peace |
| BLD-18 | Terraced Housing & Tenements | S | BLD-13, SOC-12 | Build a terrace block: housing cap ×1.3 in a Town or City; crowding disease ×1.2 unless the town has waterworks or sewers | Urban growth needs housing; dense housing is the cholera problem |
| FIR-16 | Smoke Abatement & Tall Stacks | M | FIR-11, INQ-12 | Pollution emitted at a works tile −40%; tall stacks move part of the rest to tiles two away (moved, not erased) | KEY-7 turns pollution on; E7 needs one cheap answer inside the age |
| MED-16 | Waterworks & Public Health Boards | S | NUM-12, MED-09 | A powered, staffed pump house: waterborne and crowding disease −40% in its town; stacks with sewers to a cap | Clean water before germ theory (Snow, 1854); makes the town-growth trade a choice |
| AGR-18 | Superphosphate & Guano | M | AGR-12, CHM-08, INQ-10 | Fertilizer good from acid and bone (or guano by sea): fields ×1.2 where it is spread | Feeds the towns that machines empty the farms into; uses E6's acid |
| LRN-15 | Polytechnic Institutes | I | LRN-09, MEC-10 | Staffed institute: engineers and operatives in its town come trained (engine and factory coverage +10%); scholar Insight | The age's institutions all need skilled staff; this is where they come from |
| BLD-17 | Great Exhibition Hall (Wonder) | W | CER-11, MET-14, ART-11 | Wonder of plate glass and wrought iron with master labor; Legacy as a Wonder, +1 Beacon, city Cohesion while standing | Every age since Bronze has a Wonder; E7 had none |
| SEA-17 | Ironclad Warships | M | SEA-13, SEA-14, MET-14 | Armored steam warship: blockades through galleons and is the only ship a coastal battery (DEF-11) does not stop | DEF-11 "immune to unarmored fleets" needs an armored fleet to mean anything |

**Considered and not proposed:** conscription and general staff (rail and telegraph already move armies), locomotive works (the railway structure carries its rolling stock), rubber (a new tropical deposit gate is a soft-lock risk), arc lighting and an electric grid (E8's theme; the grid is NRG-09 in E8), pulling E8 rows such as oil drilling or the telephone down into E7 (E8 would lose its identity and E8 rows already point at E7 ones).

### 3.4 Code ceilings *(verified unless noted)*

| Boundary | State now | Consequence for E7 |
|---|---|---|
| Chapter modes | `ChapterMode` Bronze 0 to Discovery 3 | Append **`Industry = 4`**; never renumber. |
| Containment flags | `IsIron` and `IsFief` are containment ("this mode or one that contains it") but **`IsDiscovery` is identity** (`Mode == Discovery`), used at 14 call sites | Turn `IsDiscovery` into containment (Discovery or Industry) and add `IsIndustry`. A missed site would otherwise switch E6 systems **off** in Industry, the unsafe failure. Audit all 14 plus 34 `ChapterMode.Discovery` references. |
| Economy flag | `DiscoveryChain` (141 references) turns on `FiefChain` | Add **`IndustryChain`**, turning `DiscoveryChain` on, the same pattern. |
| Era rows after KEY-7 | Era reach is read as `_eraReach[min(era, 9)]` (`LivingEconomySource.cs:516`), so a Discovery civilization past KEY-7 **already** uses E7 reach 50 | Audit in WP-E7-3 whether claim radius 7, the 1.56 claim multiplier and E7 mortality also already apply to post-KEY-7 rivals in Discovery *(assumed yes per [m13-wp4](balance/m13-wp4-mode-plumbing.md))*. |
| Forecast | `ForecastResult` has `Key1..Key7` (8 references to `Key7`) | Add `Key8` and the KEY-8 closure policy. |
| Replay, save, hash | Mode byte rides the shared format (`HasMode`) | **No new format numbers**: mode value 4 is enough. Bronze to Discovery bytes and hashes stay exact. |
| Soot | `_soot` per tile, Discovery only: factory +1 at site and +0.5 next door, cap 10, fades a tenth a season, endemic hazard +1/50 per point capped at ×1.2 (`LivingEconomyModel.Factory.cs`) | The seed of the pollution system (§5.3). Discovery keeps it byte-identical. |
| Power ledger | Water 5, wind 4 at 0.8, steam engine 10 units on 1 L coal each, two engineers, at most 2 engines a town, 2 units pump flooded tier-3 seams (`Steam.cs`) | The Watt engine and generator are new rows in the same ledger, never a second ledger. |
| Structures | `IronStructure` enum appended era by era (Discovery's ten last) | Append E7 structures after `RenaissancePalace`, hashed only while non-zero. |
| Livestock disease | None found in the sim *(verified by grep)* | HUS-12 has nothing to cut: same choice as E6's HUS-11 (D14). See D12. |
| Coal reserves | Camps record what is left in the ground *(assumed from the fixture comment "litres still in the ground")* | E7 multiplies coal burn; WP-E7-3 must measure whether basins run dry inside the age. |

## 4. What E6 leaves for E7 to build on

| E7 project | Builds on (E6 system) | What to watch |
|---|---|---|
| NRG-07, INQ-12, WOD-11, FIB-13 | Atmospheric engine and the shared power ledger, the Factory's coverage rule, the jenny and shuttle | "×3 on powered work" is read as **three times the power units per litre of coal** and freedom from the pumping site, not a free ×3 on output. Stacking with the Factory (×2) and the shuttle (×3) must go through the one coverage rule. |
| MET-14, MET-15, MEC-11 | Coke smelting at the blast furnace, precision parts | Wrought iron and steel are **new goods with recipes** (pig iron + coal + labor at a powered mill), so rails and rifles have a supply chain. |
| MIN-11, MIN-12, MIN-17, CHM-11 | Coal camps, flooded tier-3 seams, MIN-10 blasting powder | Pumping is already a draw on the engine; MIN-11 doubles deep yield; firedamp is the new seeded hazard. |
| TRN-12, COM-12 | Turnpikes (Cohesion walk ×1.5), the canal (coal haul ×10), the road link to the capital, GOV-12 Overreach cut | A railway is a link structure like the road, carrying **all** goods at ×20 and armies at ×5. The telegraph's −40% Overreach and GOV-12's −40% need a combined cap. |
| SEA-14, HNT-13, COM-19, SEA-18, TER-16, SEA-17, DEF-11 | Ocean reach 24, chronometer, colonies with yearly independence rolls (60‰ below Cohesion 45, +60‰ cut off), galleons, offshore banks | Steam speed must keep the `Travel` and `VoyageLost` machinery; overfishing needs a stock on the E6 offshore banks; ironclads extend the naval rows. |
| Pollution (KEY-7), FIR-13, FIR-16, MED-12, MED-16, BLD-18 | Soot and its hazard, the hospital and sanitation terms | Pollution is local (tile and neighbours) in E7; climate stress is E8-E9 ([05 §14](05-simulation-structure.md)). |
| SOC-13, GOV-13, GOV-16, TRD-12 | Cohesion, secession (below 15 for 8 seasons), operatives drawn from other work | Strikes and revolutions reuse Cohesion and secession; no new global score. |
| LRN-10, LRN-15, INQ-10/11/13, BEL-13 | Staffed-institution rule, learned society, the diffusion floor | Cost cuts go through the diffusion floor (cap), as INQ-08 and NUM-11 do. |
| AGR-13, AGR-18, FOR-11, FOD-11 | Four-course rotation (×1.2), enclosure (field labor ×0.85), the food-cap ladder | [09 §5](09-population-and-health.md) already sets E7 cap steps for AGR-13, FOR-11, FOD-11, TRN-12, MED-12 and BLD-14 (V15 targets); AGR-18 and BLD-18 must fit inside that budget or the table changes with a reason. |

## 5. New mechanisms (proposed bounded interpretations)

Each is a hypothesis for an E7 oracle to fit. "Physical" means it spends or moves ledgered goods, labor or sites; nothing is a bare multiplier. Every random event draws from `Pcg32.ForStream(seed, system, entity, tick)`; all quantities are `Fix64`; state lives in arrays indexed by id and is hashed and snapshotted only in an Industry match.

### 5.1 Steam, iron and steel (system A)
- **Watt engine (NRG-07):** a second engine row in the ledger: 30 units on the same coal (×3), no pumping-site rule, the same two engineers. INQ-12 cuts coal per unit by 1/1.4. An engine without coal or crew idles and says so. NRG-08's **generator** converts steam units into local electricity at a loss (P: 10 in, 8 out) for arc lights, the telegraph and KEY-8's night lights; there is no grid (E8).
- **Wrought iron and steel (MET-14, MET-15):** a powered puddling and rolling mill turns cast iron and coal into wrought iron (rails, boilers, girders); a Bessemer converter turns iron into steel at ×10 throughput. Steel becomes the arms and tool input above crucible steel. Each mill is a staffed works that soots its tile.
- **Mining (MIN-11, MIN-12, MIN-17, CHM-11):** steam pumping doubles tier-3 yield; dynamite doubles mine and quarry output and lets a railway cross hills; **deep seams carry a firedamp roll** per camp-season (P: 1%, kills part of the crew and idles the camp a season) that MIN-17 cuts by 80%.
- **Building (CHM-09, BLD-14, WOD-11, MEC-11):** cement is a kiln good (lime, clay, coal); reinforced concrete cuts large and infrastructure labor ×0.7; the steam sawmill saws ×2 without a river; machine tools raise the Factory's ceiling from ×2 to ×3 and halve repair time.

### 5.2 Rail, wire and steam at sea (system B)
- **Railway (TRN-12):** a link structure from a town to its capital or its coal camp, paid in wrought iron, labor and a coal burn per season. Goods on a linked pair haul at ×20 (replacing the canal's ×10 for coal and the cart for everything else) and armies travel ×5 along it. A line through an occupied or besieged tile is cut. Visible as a line with trains.
- **Telegraph (COM-12):** poles between linked towns; Overreach −40% (combined with GOV-12's cut under one cap, P: total at most −60%) and orders between linked towns take effect in the same season. COM-19 carries it across the sea from a cable station.
- **Steamships (SEA-14, SEA-18):** sea speed ×2 and the weather part of the voyage risk halved, paid by a coal burn per leg; out of coaling range a ship falls back to E6 sail. HNT-13 trawlers fish ×3 from the E6 offshore banks, which get a **stock that depletes and regrows** (overfishing); a fished-out bank is visible and stops paying.
- **Empire (TER-16):** colony sea strain halves and a colony ships a share of its staple home, while independence odds scale with colony size unless the colony is cabled. **Survey and cadastre (WAY-11, TER-12)** reveal own terrain and raise claim strength +20%; no omniscience beyond own territory.

### 5.3 The factory town: smoke, crowding and strikes (system C)
- **Pollution switches on at a civilization's own KEY-7** ([05 §14](05-simulation-structure.md)). Before it, the E6 soot rule runs unchanged. After it, that civilization's works (factory, engine, coke oven, puddling mill, converter, gasworks, textile mill, steam sawmill, and railway lines at a lower rate) write to a **pollution** value per tile, 0 to 100 ([05 §5](05-simulation-structure.md)), at the site and its neighbours, fading a tenth a season. Adult and elder hazards on a polluted tile are **×(1 + 0.3 × pollution / 100)** ([09 §10.5](09-population-and-health.md)) and field yield there falls (P: −20% at 100). Budget bound from [09 §10.5](09-population-and-health.md): a dirty-growth civilization's pollution deaths stay under about 10% of its deaths. No global haze and no climate in E7.
- **Strikes:** each factory town keeps an unrest reading built from what is already in the sim (operatives' share of adults, pollution on the town tile, crowding against the housing cap, food shortfall, Cohesion). Above a threshold the town rolls a seeded **strike** each season: its factory and mills idle for a season. A town with repeated strikes and low Cohesion feeds the **existing secession counter** (below 15 for 8 seasons), which is the "revolution" the catalog mentions. SOC-13 halves strike odds and the Cohesion drain from factory work; GOV-13 lowers the secession roll; GOV-16 trades factory output (coverage capped at 0.85) for strike odds −50%.
- **Boom and bust (TRD-12):** factories build twice as fast, and each civilization runs a seeded yearly cycle (P: one bust year in five, factory coverage ×0.8 and no new factory starts in a bust). Mild by design; it is flavor with a cost, not a trap.
- **Housing and light:** BLD-18 terraces raise the housing cap and the crowding disease weight; FIR-13 gasworks make coal gas (city Insight and craft +10% at night) and coal-tar for CHM-10 dyes (cloth trade value up); FIR-16 cuts and spreads what the stacks emit. **Urban migration** stays what E6 made it: operatives and freed farm hands are people moved, never created.

### 5.4 Public health (system D)
MED-10 vaccination cuts pox-class outbreaks −80%; MED-11 germ theory cuts all disease −30%; MED-12 sewers cut density disease −60% and raise city caps ×1.5; MED-16 waterworks cut waterborne and crowding disease −40% from a powered pump house. They stack under one cap so no town reaches zero disease. NUM-12 lets the planner stockpile against a forecast famine or outbreak (a planner rule, not a hazard change). CER-12 is the microscope good germ theory needs. E7 epidemic share target: about 3% of deaths ([09 §10.6](09-population-and-health.md)).

### 5.5 Mind, state and culture (system E)
INQ-10 cuts CHM and MET research costs −15% (through the diffusion floor); INQ-11 is the enabling discovery for the generator and telegraph; INQ-13 reveals coal and oil within own territory and adds +10% to FOR and HUS effects. LRN-10 public schooling raises Commons Insight ×1.5 **only while each school has its teacher**; LRN-15 polytechnics train engineers and operatives. GOV-13 raises Cohesion and Insight +10% and lowers the secession roll; BEL-13 adds Insight +10% and loosens faith's hold on Cohesion both ways. ART-11 museums and BLD-17's Exhibition Hall are staffed and built works. **DIP-10 balance of power** makes every AI's relations drift against whichever civilization leads in Legacy, the human included (a symmetric rule, see D9); DIP-11 halves civilian deaths in war and the war Cohesion cost.

### 5.6 Food (system F)
AGR-13 sows and harvests ×3 and cuts farm labor −40% (people freed, shown moving to town); AGR-18 spreads a made fertilizer; FOR-11 raises yield +30%; FOD-11 canning halves famine loss and doubles army supply range. Every yield step is checked against the [09 §5](09-population-and-health.md) E7 cap targets (TRN-12 ×1.50, AGR-13 ×1.40^0.9, FOR-11 ×1.40^0.8, FOD-11 ×1.05^0.6, MED-12 ×1.5^0.8, BLD-14 ×1.2^0.6).

### 5.7 War (system G)
WAR-14 rifles and breech artillery (steel goods) give firepower ×3 and raise war's Cohesion cost +50%; DEF-11 coastal batteries make a harbor immune to blockade by unarmored fleets; SEA-17 ironclads are the armored fleet that is not. Rail (×5) and telegraph move and coordinate armies. All three extend the combat tables by fixed-ratio rows fitted against the E6 force tables (`e6_combat_oracle.py` approach) before they are typed.

### 5.8 What E7 deliberately does not do
No global climate, no electric grid, no oil, no motor vehicles, no money or debt (D12 of [38](38-discovery-age.md) stands: pooled goods only), no population created by any project, no new research-side resource, no scripted events (every strike, bust, explosion and independence is a seeded roll on stated rules).

## 6. Match structure: the IndustryChapter contract

| Mode / civilization state | Research | Economy / systems | Ending |
|---|---|---|---|
| Bronze, Iron, Fief, Discovery | exactly as today | exactly as today | unchanged (KEY-4 to KEY-7) |
| Industry, before own KEY-7 | as in Discovery | Discovery rules, E6 soot | KEY-7 does **not** end this mode |
| Industry, after own KEY-7 | E7 opens by prerequisite | E7 systems eligible, pollution on for that civilization | the human completing **KEY-8**; no rival countdown, no year cap (carried D11) |
| Industry, after own KEY-8 | E8 Hidden | E7 economy continues | as above |

- **Saves, replays, hashes:** mode byte 4; a Discovery save never becomes Industry; every older mode stays byte-identical, and E7 state is written only in Industry.
- **Default chapter:** the base game stays on Discovery until E7 is promoted and the owner approves the hand playtest; then the base game plays Industry, as the owner directed for every built age on 2026-11-28. `--chapter=discovery` keeps the old ending (D1).
- **Legacy:** current rules unchanged (Mastery `2 × (era + 1)`, Wonder built `25 × (era + 1)`, Thriving). Dominion unchanged; [07b](07b-territory-findings.md) expects the map to fill and conquest wins to become possible in early-to-late E7, so Dominion endings must be reported separately.
- **Representation:** simulation 1:1; the catalog's 1:100 applies to the view only if the figure budget (600 a civilization) is exceeded.

## 7. Pacing: 30 to 35 minutes, aiming for 32

### 7.1 How E6 was pinned, and what it tells us
E6 was not fitted by formula but by sweeping the era base in live stock-AI runs: 32,000 took 45 to 56 minutes; after E5 became real and moved twice, **18,000 gives 24.9 minutes** (12 of 12 civilizations, range 19 to 31), confirmed at 25.25 on held-out seeds ([slice 5](balance/m13-wp12-pacing-slice5.md)). The eight late rows cost twice the base (D9). Four seasons make a minute, so "Insight a year" is Insight a minute.

The sweep fits the growth model the E6 oracle already uses (minutes = ln(1 + r·C/I₀)/r): **r = 0.0585 a minute** reproduces 14,000 → 21.7, 16,000 → 23.4 and 18,000 → 24.9 minutes. So Insight grows about 6% a minute and is **4.3 times larger at KEY-7 than at KEY-6**; at 18,000 the E6 bill is about 457,000 (21.0 base units on the 14-project path plus KEY-7, plus 79,000 of backfill) and the rate at KEY-7 is about 35,000 a minute (consistent with the 27,000 to 34,000 a year logged in [slice 1](balance/m13-wp12-discovery-match-slice1.md)). The model is a little less sensitive than the live runs (0.6 against 0.8 minutes per 1,000 of base), so it is a starting point, not the fit.

### 7.2 Sizing E7
Carrying r forward, a 32-minute age needs a bill of 35,000 × (e^(0.0585×32) − 1) / 0.0585 ≈ **3.3 million**, about **7.2 times E6's**. Subtract KEY-8's backfill (267,000, §3.2) and divide by the base units of the path:

| Breadth | Path in base units (cheapest picks, D9 applied, KEY-8 2.5) | Base for 32 min | Completions | Minutes per completion |
|---|---|---|---|---|
| 14 (catalog) | 19.9 | ~151,000 | 15 | 2.1 |
| 18 | 23.9 | ~124,000 | 19 | 1.7 |
| **20 (recommended)** | **25.9** | **~113,000** | **21** | **1.5** |

Sensitivity at breadth 20: 30 minutes → ~97,000, 35 → ~141,000. If growth slows in E7 (the map fills and per-settlement caps bind in E7, [09 §5](09-population-and-health.md), [07b](07b-territory-findings.md)) at r = 0.04 the base is ~75,000; if it holds at 0.07, ~147,000. Each 10,000 of base moves the age by about 1.1 minutes. **Draft input: base 110,000, breadth 20, D9 doubling, KEY-8 at 2.5 × base.** It replaces the registry's 280,000 when the rows are typed, and the live sweep (WP-E7-13) fits it.

Why raise breadth rather than only the base: the minutes come from the bill, and the bill is cost per project × projects required. Raising only the base to ~151,000 would give 32 minutes with 15 completions, each a 2-minute wait. Breadth 20 gives the owner's "more research" (a completion every 1.5 minutes, 10 free choices) and needs more nodes so the choices stay real: E4 to E6 offered 2.7 to 3.1 nodes per breadth step, and 55 nodes for breadth 20 is 2.75. That is where the twelve come from.

### 7.3 The `e7_research_oracle.py` plan (to write in WP-E7-1; not written here)
- Load the catalog (planned rows plus the twelve) through `e4_research_oracle.load_catalog`; check counts (55 + KEY-8; Craft 19, Kin 17, Reach 19), the 10-project closure, breadth 20 with 10 free picks, the D9 set (22) and that typed overrides equal 2 × base × type multiplier.
- Candidate sets: cheapest picks, and theme-restricted sets (factory-town and health picks) so the optional systems' cost is visible.
- Backfill: KEY-8's pre-E7 debt at EraLag 0.7^n, read at the live E5 and E6 bases.
- Forward pacing from a new `e7_anchors.py` holding the live E6 anchor (slice 5 sweep and the held-out run, r, the KEY-7 Insight rate), printing the base for 30, 32 and 35 minutes for r in 0.04 to 0.07, both "cheapest path" and "AI-like path" (the AI's actual E6 picks once logged).
- `--check` asserts the closed forms invert, as the E6 oracle does. Companions: `e7_power_oracle.py` (Watt engine, mills, rail and steamship coal burn against basin reserves), `e7_health_oracle.py` (pollution, the 10% death bound, disease stacking cap), `e7_combat_oracle.py` (rifles, ironclads, batteries).

## 8. Provisional targets (N-xx; none fitted, none approved)

("N" for iNdustry; "I-T" is already Iron's.)

| ID | Target / diagnostic | Measurement |
|---|---|---|
| N-T1 | Own KEY-7 → KEY-8 about **32 minutes** (30 to 35); unfinished seats reported, never imputed | 80+ matches, held-out block |
| N-T2 | **Coal budget:** no basin used by a civilization runs dry before its KEY-8 in more than a stated share of worlds; a coal-poor start can still reach KEY-8 | `e7-audit` plus live trace |
| N-T3 | **Power:** Watt, atmospheric, water, wind and generator conserve in one ledger; no ×-stacking beyond the coverage rule | `--industry-power-ledger` mirroring F-T2 |
| N-T4 | **Rail and wire:** haul and travel gains only along built, uncut links; Overreach cut capped | Per-link ledger, seeded cut cases |
| N-T5 | **Pollution:** hazard and yield follow §5.3; dirty growth costs under ~10% of deaths; FIR-16 moves, never erases | Per-cause death attribution from own KEY-7 |
| N-T6 | **Unrest:** strikes, busts, firedamp and independence fire only by their seeded rules; no runaway secession chain in natural play | Seeded replay cases plus a natural-start trace |
| N-T7 | **Health:** E7 epidemic share near 3% of deaths; no town reaches zero disease | Per-cause attribution |
| N-T8 | **Institutions:** every E7 works (mills, schools, institutes, waterworks, sewers, cable and coaling stations) working, queued or dormant with a reason within a season | F-T6 blunder counter |
| N-T9 | **Sea and war:** steam range and coaling, ironclad against battery and galleon, rifles on the force tables | Isolated trials plus live attrition |
| N-T10 | Map fill, Dominion, City share, figure budget at 1:100 | Diagnostic |

## 9. What the player sees

The generic kit of [08 §13](08-ui-and-world-display.md) (badge-burst Moments, type signs, banner swaps). The read of E7 ([08](08-ui-and-world-display.md) era table): rail lines with trains and smoke, steamships with funnels, telegraph poles along roads, brick mill sheds and gasholders, slag heaps by deep pits, terraces filling the town edge, a brown-grey **haze only near industry**, gaslight dots at night, and **newsprint** typography in the Chronicle ([08](08-ui-and-world-display.md), Chronicle feed). Strikes show as an idle factory with figures gathered outside and a Chronicle line ("The mill hands of Ash Valley walked out in the 3rd winter"). Cards needed: coal stock and burn by town, the power pip row with the Watt engine and generator, rail and telegraph links, pollution by town, unrest and the strike reason, disease by cause, colony and cable state, KEY-8 progress (N/20). Game text says KEY-8 / Electrification only in Industry mode.

**Art doc (later job) P0 silhouettes:** railway line and locomotive; telegraph pole line; Watt engine house; puddling and rolling mill (long shed, many stacks); Bessemer converter; steamship; pollution haze; strike marker; terraced row; KEY-8 night-lights Moment. P1: textile mill, gasholder, waterworks pump house, sewer outfall, school and polytechnic, coastal battery, ironclad, coaling heap, cable station, Great Exhibition Hall.

## 10. Work breakdown and order

Own worktree and branch (`civsim-e7`, branch `codex/e7-industry`), never the shared main tree. Rows change new files or additive lines; none changes an E6 rule or number.

| WP | Deliverable | Exit gate | Size | Parallel with |
|---|---|---|---|---|
| **WP-E7-0 Scope** | This document | Owner answers D1 to D5 | S | — |
| WP-E7-1 Oracles | `e7_research_oracle.py`, `e7_anchors.py`, power, health and combat oracles (§7.3) | `--check` passes; draft base and breadth printed for 30/32/35 min | M | 2, 3 |
| WP-E7-2 Content typing | 55 + KEY-8 typed in place (effects, practice, flavor, Moment; all `draft`), the twelve new rows, goods (wrought iron, steel, cement, fertilizer, gas/coal-tar, dye, rifles, microscope), structures, roles (puddler, telegraphist, teacher), recipes, V24 (E7 promoted together), era base and breadth 20 | `validate --strict` clean; draft rows inert; pack golden refreshed at merge | L | 1, 3 |
| WP-E7-3 Audit | `e7-audit` over seeds 1-10,000: coal reserves against E7 burn, offshore fish stock, rail geometry, colony counts, which E7 era rows post-KEY-7 Discovery rivals already use | Report; no worldgen change | M | 1, 2 |
| **WP-E7-4 Mode plumbing** | `Industry = 4`, ceiling 7, KEY-8 ends it, `IsDiscovery` to containment, `IsIndustry`, `IndustryChain`, `Key8`, `--mode industry`, isolation tests | All older goldens byte-identical | S | first |
| WP-E7-5 Steam, iron, steel | System A | Ledger conserves; mills, converter, firedamp tests | L | after 4 |
| WP-E7-6 Rail, wire, sea | System B | Link, cut, coal-range, overfishing tests | L | after 5 (rails need wrought iron) |
| WP-E7-7 Factory town | Pollution, strikes, boom and bust, housing, gas, smoke abatement | N-T5, N-T6 on fixtures | L | after 5 |
| WP-E7-8 Health and food | Systems D and F | Disease cap, cap-target V15 clean | M | 7 |
| WP-E7-9 Mind and state | System E, the Wonder, DIP-10/11 | Staffed-only benefits; symmetric DIP-10 | M | 6, 7 |
| WP-E7-10 War | System G on the force tables | Oracle-fitted rows | M | 6 |
| WP-E7-11 AI and forecast | KEY-8 routing, free-pick priors for factory-town answers, rail and coaling logic, blunder telemetry | Stock AI reaches KEY-8 unaided; builds the answers it researches | M | after 5 to 10 |
| WP-E7-12 View and game | `IndustryFrame`, cards of §9, save/mode selection, generic kit | Headless frame tests; game shell runs | M | 11 |
| WP-E7-13 Promotion and pacing | Promote all 56 rows together; `industry-match` with `--grant-e7`; era-base sweep to 32 min | N-T1 on fit seeds | M | — |
| WP-E7-14 Large sample and playtest | 80+ matches, held-out cohort, owner hand playtest | All gates in §11; owner approval | L | — |

**Order:** WP-E7-4 first (small, gives every slice a flag), then WP-E7-2 and WP-E7-3 in parallel, then WP-E7-1; then A, B and C (C needs A's works as pollution sources), the rest as the oracles fit them; promotion and pacing only when the whole age works (standing rule: no partial playtests).

## 11. Acceptance series B7-xx (all proposed)

| Gate | Condition |
|---|---|
| B7-01 Isolation | Toy, worldgen, research, politics, Bronze, Iron, Fief **and Discovery** goldens and checksums byte-identical; E7 inert before own KEY-7 and in every older mode |
| B7-02 Completeness | 56 nodes typed with costs, practice, flavor, Moment; strict validation clean; registries resolve |
| B7-03 Determinism | 1,000 Industry matches replay exactly; snapshot continuation exact; macOS arm64 and Windows x64 agree on an industry-v1 golden |
| B7-04 Mode and end | End clauses, ties, KEY-7 continuation, KEY-8 threshold, old-save modes |
| B7-05 Pacing | N-T1 |
| B7-06 Coal and power | N-T2, N-T3; exact input/output conservation |
| B7-07 Rail and sea | N-T4, N-T9; voyages and links conserve people and goods |
| B7-08 Pollution and health | N-T5, N-T7 |
| B7-09 Unrest | N-T6 |
| B7-10 Institutions | N-T8; no unstaffed benefit |
| B7-11 AI | A1-A8 in Industry mode, Legend ≥ 60% vs Warlord; fog fuzz for coal, rails, fleets |
| B7-12 Performance | Mean tick ≤ 4 ms, GC ≤ 16 ms at E7 p95 population; figure budget |
| B7-13 Comprehension | ≥ 5/8 uncoached players explain one coal or rail bottleneck and one price of industry (smoke, strike or disease) |
| B7-14 Integration | Full Release suite and strict validation green; golden changes explained; owner approves merge |

## 12. Decisions

| ID | Question | Recommendation (plain English) | Needs the owner? |
|---|---|---|---|
| D1 | Is the next age E7 Industry, as `ChapterMode.Industry` containing Discovery, becoming the base game once accepted? | Yes. It is what the catalog and doc 01 say comes next; the base game switches only after your playtest. | **Yes** |
| D2 | How long? | **32 minutes** from KEY-7 to KEY-8, inside your 30 to 35. It leaves room either side for the fit's spread. | **Yes** |
| D3 | How many projects must be done for KEY-8? | **20 instead of 14.** More research completed, a project every 1.5 minutes instead of 2, and 10 free choices so the answers to smoke and strikes get picked. | **Yes** |
| D4 | Add the twelve new projects of §3.3? | **Yes.** Each carries a system this age needs (mine safety, cable, coaling, colonies, factory law, housing, smoke, clean water, fertilizer, polytechnics, a Wonder, ironclads). Pulling E8 rows down instead would hollow out E8. | **Yes** |
| D5 | Should deep projects still cost double (D9)? | **Yes, for consistency with every age so far**; the oracle fits the base with it included. It now touches 22 of 55 rows, so if optional systems turn out not to run, this is the first lever to loosen. | Please confirm |
| D6 | How does pollution start? | At each civilization's own KEY-7, replacing E6 soot for that civilization's works, local only, with doc 09's death-rate rule and a 10% ceiling on what it can cost. | No (follows 05 and 09) |
| D7 | Strikes and revolutions? | Seeded local strikes that idle a factory; revolutions reuse the existing secession rule. No new global score. | No |
| D8 | Boom and bust (TRD-12)? | Keep it, mild: one bad year in five slows factories. It is the catalog's price for doubled factory building. | Please confirm |
| D9 | Balance of Power (DIP-10)? | Make it **symmetric**: AI rivals lean against whoever leads, you included. A rule that only ever helps the human would feel rigged. | **Yes** |
| D10 | A countdown after a rival's KEY-8? | None; only your KEY-8 ends the chapter (as in E5 and E6). | No |
| D11 | Electricity in E7? | Local generators only (telegraph, arc lights, KEY-8's night lights); the grid is E8. | No |
| D12 | HUS-12 veterinary science: the sim has no livestock disease to cut | Do as E6 did for HUS-11: give it a small herd-yield effect rather than invent a disease system for one row. | Please confirm |
| D13 | Era base | Type **110,000** as the first input (not the registry's 280,000) and fit it live; expect 75,000 to 150,000. | No |
| D14 | Food and housing caps | Fold AGR-18 and BLD-18 into doc 09's existing E7 cap budget (split it, do not add to it), and update doc 09's table with the reason. | No |
| D15 | Representation | Simulation stays 1:1; 1:100 only in the view if the figure budget breaks. | No |

## 13. Risks

- **The pacing estimate is a guess until it is run.** It carries E6's growth (6% a minute) into an age where the map fills and settlement caps bind; slower growth halves the right base. The base is one number to change, and the live sweep decides it.
- **E6 and E5 numbers can still move**, and E6 was refitted three times for that reason. Re-anchor `e7_anchors.py` after any change below E7.
- **Coal may run out.** Rails, steamships, mills and engines all burn it; if basins empty inside the age, coal-poor civilizations stall. WP-E7-3 measures it before any number is typed.
- **The price can become a trap.** Pollution, strikes and secession feed each other; the 10% death ceiling, a strike cap per town and the existing secession thresholds must stop a spiral.
- **Optional systems may never run** (the E4 and E6 lesson). Ten free picks and `--grant-e7` from the first slice are the guard.
- **E7 era rows may already leak.** Post-KEY-7 rivals in today's Discovery matches use E7 reach 50 and perhaps claim radius 7; Industry may only formalise what already happens. Audit first.
- **`IsDiscovery` is an identity check.** Missing one of its call sites switches E6 systems off in Industry; a test must assert every E6 system runs in Industry.
- **Performance:** rail links, telegraph and pollution add per-tile and per-link work on the largest populations yet; measure the tick budget early.
- **The content-pack golden is shared;** refresh it once, at merge.
