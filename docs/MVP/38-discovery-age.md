# 38 — Discovery (E6): Coal, Caravels and the Opening of the Wider World

> **Planning draft, 2026-11-20.** The E6 counterpart of [27 — Faith & Fief](27-faith-and-fief-age.md), [25 — Iron Age](25-iron-age.md) and [22 — Bronze Age](22-bronze-age.md): scope, audit, mechanisms, acceptance gates and a work breakdown. Written while E5 is still being balanced; the user confirmed on 2026-11-20 that E5 numbers are the only thing still moving (no structural changes), so E6 work may start. **E6 never changes an E5 rule.** Every E6 value is a draft input to an oracle, not a fitted number.
>
> Audit baseline: `main` at `deba515` (the full Faith & Fief line is merged). Claims marked *(verified)* were read from the catalog, registries or source on that commit. Claims marked *(assumed)* come from older docs and must be re-checked once E5's balance lands.

## 1. Which age is next, and why

[01 §8](01-game-structure.md) lists ten eras. Faith & Fief (E5) is **entered by KEY-5 Feudal Order and left by KEY-6 Printing Revolution**; KEY-6 *enters* **E6 Discovery** (Renaissance and early modern), and **KEY-7 Industrial Revolution** is the keystone that leaves it.

| Check | Finding |
|---|---|
| Newest completed age | E5 Faith & Fief: mechanics built across WP-E5-4 to WP-E5-10, merged to `main`; numbers under balance ([27](27-faith-and-fief-age.md)). |
| Is any E6 content live? | No. `ChapterRules` has Bronze (ceiling 3), Iron (4) and Fief (5) *(verified, `src/CivSim.Sim/Research/ChapterRules.cs`)*. In every mode E6 is Hidden. |
| E6 catalog state | **41 regular projects plus KEY-7**, all `status: draft` with **prose-only effects** (`kind: prose`) and no costs, practice, flavor or Moments *(verified)*. That matches [03 §4.1](03-research-catalog.md) (Craft 12, Kin 12, Reach 17 = 41) exactly, so there is no count discrepancy to resolve. |
| Era row | `content/registries/eras.yaml` E6: base cost 32,000, breadth 14, representation 1:25, claim radius 5, reach 35, sea level 2, child/adult/elder mortality 0.025 / 0.0095 / 0.049 *(verified)*. |

The working name for the new match rules is **DiscoveryChapter** (E4, E5 and E6 all play; KEY-7 ends the match). Milestone name: **M13**.

## 2. What Discovery should feel like

Faith & Fief set the countryside, church and town against each other. Discovery **opens the horizon and runs out of wood**: ships leave the coast, the first coal goes into furnaces, and the first machines move from a mill-race to a cottage and then a factory. The player still sets three research slots and watches planners carry out the result.

| Tension | What pushes | What answers it |
|---|---|---|
| **Over the horizon** | Caravels, chronometers and charts open the ocean; colonies, sugar, new crops and joint-stock voyages pay for it. | Voyages are lost, colonies strain Cohesion and may break away, and a fleet away is a fleet not defending. |
| **Wood against coal** | E5's blast furnace made forest the bottleneck. Coal firing, coking and coke smelting free the forest, and the atmospheric engine turns coal into work. | Coal is a **deposit**, one or two basins a continent; whoever sits on it, or trades for it, escapes the fuel crunch and whoever does not stays in it. |
| **Powder against stone** | Gunpowder weapons (WAR-12, E5) meet star forts, musket drill and galleons. | Bastion forts resist cannon; national armies and fleets need a state that can pay (GOV-12, SOC-12). |
| **Mind and its limits** | Telescope, scientific method, calculus and learned societies cut costs and share Know-how across lineages. | Dogma costs Cohesion (INQ-07) unless the realm has tolerated diversity (BEL-12); institutions still need staff. |
| **Town and manufactory** | Four-course rotation frees rural labor; flying shuttle and factory multiply output; the middle class pays Insight. | Urban migration and factory pollution begin; labor released from the land must be fed and housed. |

Outside the catalog, one rule from E5 is paid off here: [27 §5.7](27-faith-and-fief-age.md) says E5 **prepared** the sea (cogs, lateen sails, compass, portolan charts) and that "oceans open" is an E6 rule. E6 delivers it (§5.2).

## 3. Scope audit

### 3.1 Inventory *(verified from `content/catalog`)*

**41 regular projects + KEY-7 = 42 nodes.** The 41 fall into seven systems; each becomes one work package (§9).

| System | Projects |
|---|---|
| **A. Coal, coke and steam** (8) | MIN-09 coal mining (**Discovery type, gate `[Seen: Coal]`**) · MIN-10 black-powder blasting (mine ×2) · FIR-11 coal firing (heat wood demand −50%) · FIR-12 coking · MET-13 coke smelting (iron ×3, no charcoal) · NRG-06 atmospheric steam engine (coal → work) · TRN-10 turnpikes and coaches · TRN-11 canals and locks (bulk haul ×10) |
| **B. Ocean, colony and trade** (10) | SEA-12 caravels and ocean voyages · SEA-13 galleons and ships of the line · WAY-10 marine chronometer (shipwrecks −70%) · HNT-12 deep-sea fishing · FOD-10 sugar refining (needs sugarcane) · FOR-10 Columbian Exchange · TER-11 overseas colonies · TRD-10 joint-stock company · TRD-11 stock exchange and insurance · DIP-09 resident embassies |
| **C. Mind, instruments and science** (9) | INQ-07 telescope · INQ-08 scientific method · INQ-09 Newtonian physics · NUM-10 logarithms · NUM-11 calculus · MEC-09 precision instruments · CER-11 plate glass · CHM-08 acids and alkalis · LRN-09 learned societies |
| **D. State, society and culture** (6) | GOV-12 nation-state · SOC-12 middle class · BEL-12 Reformation and tolerance · ART-10 Renaissance patronage · COM-11 newspapers and pamphlets · BLD-13 Renaissance architecture (Palace Wonder) |
| **E. Land, food and health** (4) | AGR-12 four-course rotation and enclosure · HUS-11 fodder crops · FOR-09 botanical gardens · MED-09 anatomy |
| **F. War** (2) | DEF-10 star forts · WAR-13 pike and musket drill |
| **G. Manufacture** (2) | FIB-12 flying shuttle and spinning jenny · MEC-10 factory system (**Factory building, KEY-7 prerequisite**) |

### 3.2 KEY-7 closure *(verified by hand from `requires`)*

KEY-7 requires **NRG-06, MEC-10, TRD-10, INQ-08** and **14 completed E6 projects** (breadth). The in-era closure of those four is exactly **10 projects**: MIN-09, NRG-06, MEC-09, FIB-12, MEC-10, SOC-12, SEA-12, TRD-10, INQ-07, INQ-08, leaving **4 free picks**. This matches [04 §5.4](04-research-paths.md) (10 critical, 14 total, 4 free).

Findings that shape the design:

1. **Two critical projects are geography-gated.** MIN-09 carries `[Seen: Coal]` and coal is "ancient lowland basins, 1–2 basins per continent" ([12 §4](12-world-generation.md), `deposits.yaml`). A civilization that never sees coal has MIN-09 Hidden, so **KEY-7 is unreachable for it**. [25](25-iron-age.md) and [27](27-faith-and-fief-age.md) both hold that geography must not soft-lock a keystone, so E6 needed a coal-visibility audit. **WP-E6-3 measured it ([report](balance/m13-wp3-e6-geography.md)): KEY-7 is unreachable for 28.7% of starts (11,494 of 40,000), and in 28.3% of worlds for all four civilizations.** The cause is not scarce coal (every world has 24 coal deposit tiles, a median 8 hex from a start, none overseas). The sim reads `[Seen: Coal]` from the `DarkSeam` *surface sign*, and the generator's guaranteed oil-seep placement can pick the same tile as the guaranteed dark seam and overwrite it. MIN-08's "reveal veins" was prose with no code behind it. **Both causes are fixed (2026-11-20, D13):** the generator no longer lets the oil seep overwrite the dark seam, and MIN-08 now makes coal deposits within the civilization's reach count as `[Seen: Coal]`. After the fix no world lacks a seam, only 0.4% of starts (28 of 8,000, seeds 1–2,000) have no seam within reach 35, none within 52 with WAY-01, and MIN-08 covers the rest.
2. **The critical path is steam, sea, mind, money and the factory; war is not on it.** DEF-10, WAR-13, GOV-12, ART-10 and most land projects are optional. As in E4 and E5, optional systems barely run if research finishes in the last minutes, so E6 needs a `--grant-e6` harness mode and pacing that gives them time to matter.
3. **FIR-11 (coal firing) is not on the path** and needs TRN-11 canals and MIN-09. The wood relief of coal is therefore optional for a keystone-focused player; the audit must show whether natural play ever reaches it (the E4 lesson, [m11-wp11](balance/m11-wp11-whole-match.md)).
4. **Prerequisites outside E6 stay as typed.** E6 rows lean on E5 (MET-12, NRG-05, MIN-08, DEF-09, WAR-12, SEA-11, WAY-09, TRD-09, COM-10, LRN-08) and a few E4 rows. E6 may not retype E5 rows.

### 3.3 Code ceilings *(verified unless noted)*

| Boundary | State on `deba515` | Consequence for E6 |
|---|---|---|
| Chapter modes | `ChapterMode` = Bronze 0, Iron 1, Fief 2 | Add `Discovery = 3`, default off, append-only. |
| Research ceiling | `ChapterRules.LastResearchEra` is 3, 4 or 5 | Discovery carries 6. Hidden above it stays the rule. |
| Economy ceiling | `EconomyMaxEra` is 3 in every mode; Iron and Fief are delivered through `IronChain` / `FiefChain` flags and a `ProjectCeiling` shim | **Add `DiscoveryChain`, turning `FiefChain` on**, with the same one-line shim. No second ceiling mechanism. Era 6 demography, fission and reach rows need the per-chapter economy profile (D3). |
| Forecast / Spark | `ForecastResult` has `Key1..Key6` | Add `Key7` and the KEY-7 closure policy as new fields. |
| Replay / save / hash | Modes ride the shared mode-bearing format: replay version 2, snapshot trailer, research hash all carry a mode byte (`HasMode`) | **No new format numbers**: a new mode value (3) is enough. Bronze, Iron and Fief bytes must stay exact. |
| Frame / view | `IronFrame` and `FiefFrame` are empty unless their chapter | Add a `DiscoveryFrame` block under the same rule; arrays only grow. |
| Sea | `OpenSeaReach = 11`, `SeaCostTenths = 4`, shipwreck rolls, harbors, WAY-08 loss halving, naval blockade | See §5.2: ocean reach is a new rule, not a rewrite. |
| Generic visual kit | [08 §13](08-ui-and-world-display.md): bespoke Moments only for E0–E4 | E6 uses the generic kit (§8). |

## 4. What E5 leaves for E6 to build on

| E6 project | Builds on (E5 system) | What to watch |
|---|---|---|
| NRG-06 steam, FIR-11/12, MET-13 | E5 power ledger and blast furnace (F-T2, F-T3), `MIN-08` | Steam is a **third source in the same power ledger** (water 5, wind 3.2 expected power-hours per season), not a parallel ledger. Coke smelting replaces charcoal in the same recipe slot, so F-T3's fuel accounting is the template. |
| DEF-10 star forts, WAR-13 | DEF-09 concentric castles (x1.875 garrisoned), WAR-11/12, wall integrity, cannon + powder as consumed goods | Star forts must beat cannon **and** be a fair price, or gunpowder siege (F-T5) is erased a second time. Compare full force tables. |
| SEA-12/13, WAY-10, TER-11 | SEA-10/11, WAY-08/09, harbors, galleys, blockade, voyage-loss rolls, the island rules of [21](21-island-crossing.md) | The two continents are 33 tiles apart; outriggers already cross them in 2–3 legs through the islands (21 §3). The audit must measure when overseas contact **already** happens in Fief matches before E6 "opens" it. |
| TRD-10/11, SOC-12, GOV-12 | TRD-08/09 (credit design **still unresolved**, 27 WP-E5-7), GOV-11 admin span | Joint-stock and insurance need the credit model the user has not chosen. Do not invent debt rules (see D12). |
| BEL-12, LRN-09, INQ-07/08 | BEL-10 monasteries, LRN-08 universities, staffed-institution rule (F-T6) | Insight sources need staff; "knowledge shared across lineages" must reuse the existing diffusion-discount cap. |
| FOR-10 Columbian Exchange, FOD-10 sugar | FOR/AGR rotation, trade goods | Exchange needs a contacted civilization **on another continent**; sugarcane is "3 regions per map" (deposits), so most starts depend on trade. |
| AGR-12, HUS-11 | AGR-11 three-field (+50%), HUS-10 selective breeding | The +50% of four-course must be measured against the three-field baseline, not stacked on a free multiplier. |
| FIB-12, MEC-10 | FIB-10/11 treadle loom and wheel (hand kits), MEC-07 mill power | The Factory is the first building whose output scales by a **power ledger** share, and the first with pollution. |

## 5. New mechanisms (proposed bounded interpretations)

Each is a hypothesis for an E6 oracle to fit, in the style of [25](25-iron-age.md) and [27](27-faith-and-fief-age.md). "Physical" means it spends or moves ledgered goods, labor or sites; nothing is a bare multiplier.

### 5.1 Coal, coke and steam (system A)
- **Coal is a deposit good** (`G-COAL`, new). Mining it needs MIN-09, a staffed mine on a coal tile and a seam depth the existing shaft rules already model (depth 2–3). Coal stock is local; moving it uses roads, then canals (TRN-11, bulk ×10) before it uses carts.
- **Coal firing (FIR-11)** replaces **half** the wood a settlement burns for heat and kilns, drawn from the same fuel accounting the I-T6 and F-T3 protocols already use. No coal in range means no relief.
- **Coking (FIR-12)** turns coal into **coke** at a kiln (labor plus coal, a loss fraction), and **coke smelting (MET-13)** feeds the blast-furnace recipe with coke where it used charcoal. "Output ×3" is read as: the same iron from far less wood, so the forest stops being the limit (it does not mint iron).
- **Steam (NRG-06)** is a staffed **atmospheric engine**: it burns coal for power-hours in the E5 ledger and, at E6, pumps flooded shafts (the first drainage that makes deep coal mining work). It is not a free mill; an engine without coal or staff is idle and shown so (F-T6 rule).
- **Roads and canals (TRN-10/11)** change haul cost in the existing distance-based haul component; canals need a river or a surveyed line, a site and labor.

### 5.2 Ocean, colony and trade (system B)
- **Ocean reach (SEA-12):** E5 prepared sea power on routes harbors already open. SEA-12 raises the open-sea reach from `OpenSeaReach = 11` (coast-hugging, stepping stones) to a **one-leg ocean reach** large enough to cross the channel (the audit measured a one-leg crossing needing reach 15 to 19 depending on the world; 16 works in only 28.5% of worlds and **20 works in all of them**, so the typed value 24 keeps a margin and must not go below 20), with a longer voyage time and a higher risk roll that WAY-10 (−70%) and TRD-11 (−50% losses) cut. The same `Travel` and `VoyageLost` machinery carries it; no second travel system.
- **Visibility stays honest:** portolan charts and the chronometer extend what a civilization *sails*, never what it *knows*. No omniscience: unseen continents are met by arriving there, as in E4.
- **Overseas colonies (TER-11)** are settlements founded across the sea. They pay a distance-based Overreach strain and carry an **independence risk** as a bounded, seeded event (not scripted by score). A colony cut off from its mother city by a blockade is a real exposure.
- **Columbian Exchange (FOR-10)** transfers crops between two civilizations that have **actually met across an ocean**: the yield effect is paid only to settlements that can grow the imported staple on their biome.
- **Galleons (SEA-13)** are armed trade and war ships on the existing naval rows (blockade, convoy); a fleet at sea is out of the defense pool.
- **Joint-stock and insurance** change **who funds and who bears loss**, not how much money exists: TRD-10 pools existing goods into one voyage ledger (×1.5 speed of ventures), TRD-11 spreads voyage losses across settlements. Both wait on the credit decision (D12).
- Sugar, cod and herring are **trade goods** with a source (sugarcane region, offshore fishing fleet) and a buyer; their Cohesion effect is paid only to settlements that actually hold the good.

### 5.3 Mind, instruments and science (system C)
- **Insight sources need staff**, as in E5. INQ-08 "all project costs −10%" and NUM-11 "INQ −10%" are capped by the existing diffusion-discount cap so no stack reaches zero cost; they are cost modifiers on the research side, not output multipliers.
- **LRN-09 learned societies** add a **10% Know-how spillover** between lineages and extra diffusion with contacted civilizations (the cap applies).
- **Instruments (MEC-09), glass (CER-11), acids (CHM-08)** are recipe and tool-kit inputs: precision parts for engines and chronometers, glass for greenhouses (+winter food in a settlement that has the building), alkali/acid recipes for soap, glass and bleaching. Each is a good with a source, not a global ×2.
- **Telescope (INQ-07)** doubles tower sight and navigation; its "challenges dogma" Cohesion cost is a bounded, ownership-scoped effect that BEL-12 cancels.

### 5.4 State, society and culture (system D)
- **GOV-12 nation-state** makes Cohesion uniform across the realm and cuts Overreach 40%; it is the E6 successor to GOV-11's admin span and benefits stop when the capital or staff is lost.
- **SOC-12 middle class** adds Commons Insight +20% and demand for goods; **ART-10 patronage** doubles Legend chance and adds Insight +10% **only while patrons exist** (a staffed court or Wonder).
- **COM-11 newspapers** speed Cohesion's response to events (information delay), with GOV effects +10% as a bounded opinion effect.
- **BLD-13** adds a city-layout efficiency effect and the **Palace Wonder**, a normal Wonder with explicit stone, wood, glass and master-mason labor. The Wonder-input fix of E5 WP-E5-0 applies; check it again before adding a sixth Wonder.

### 5.5 Land, food and health (system E)
AGR-12 removes fallow (four-course with fodder): the productivity step is derived from the field schedule exactly as AGR-11 was, with the rural labor freed shown as people moved to towns, not erased. HUS-11 (stall-feeding) raises winter livestock survival from a fodder stock. FOR-09 is a staffed garden with a bounded medical bonus. MED-09 anatomy raises surgery and training on the existing hospital model. Plague weight is **0.15 at E6** ([09 §10.2](09-population-and-health.md), [27 §4](27-faith-and-fief-age.md)).

### 5.6 War (system F)
DEF-10 **bastion forts** are a new fortification tier that resists cannon: it raises the integrity a cannon must breach and lowers the −70% wall penalty that gunpowder inflicts, on the same integrity track (1000 = intact). WAR-13 national armies ×1.5 are a drilled-levy rule that needs GOV-12 and muskets. Both extend the combat tables by fixed-ratio rows (the `e4_combat_oracle.py` approach) and must be fitted against the F-T5 force tables before they are typed.

### 5.7 Manufacture (system G)
FIB-12 raises weave and spin throughput through a **power-ledger share** (shuttle and jenny, ×3) and boosts cottage output as a trade good. MEC-10 adds the **Factory**: a staffed, powered building whose craft output per worker scales with the power it actually receives (×2), with **urban migration** and a first **pollution** source that feeds the system KEY-7 turns fully on. Pollution here is a real local state (soot in a tile, health weight), not a global score.

## 6. Match structure: the DiscoveryChapter contract

| Mode / civilization state | Research | Economy / systems | Ending |
|---|---|---|---|
| Bronze, Iron, Fief | exactly as today | exactly as today | unchanged (KEY-4, KEY-5, KEY-6) |
| Discovery, before own KEY-5 | E4, E5 and E6 Hidden as in Fief | nothing past the Iron/Fief rules | KEY-6 does **not** end this mode |
| Discovery, after own KEY-5 | E5 opens as in Fief | Fief systems active | unchanged until a civilization has KEY-6 |
| Discovery, after own KEY-6 | E6 opens (or glimpses) by prerequisite and gate | E6 systems eligible; each still needs its local inputs, site, staff | the human completing KEY-7; **no rival countdown and no year cap** (D11) |
| Discovery, after own KEY-7 | E7 Hidden | E6 economy continues, no E7 systems | as above |

- **Ending (D11, carried from E5):** a rival's KEY-7 starts no clock; the chapter ends only when the human completes KEY-7. Headless and AI-only runs are bounded by their own `--minutes` horizon.
- **Length (D8, recommended, not yet confirmed):** **25 minutes from own KEY-6 to own KEY-7**, the same target as Iron and Fief. The era base is 32,000 in `eras.yaml`; it is **left as typed until a live E6 run can fit it**, as E5's was.
- **Late projects (D9, recommended):** the [28 §7 and §16](28-production-gap.md) rule (non-keystone projects with at least three projects in their own era's prerequisite chain cost twice the derived cost) applies. **Eight of the 41** qualify by the repo's depth definition (the longest same-era chain has three projects, `late()` in the oracles): CHM-08, FIB-12, FIR-11, FIR-12, INQ-09, MEC-10, MET-13 and TRD-11. (An earlier draft said seven, counting all ancestors instead of chain depth; CHM-08 sits at the end of INQ-07, INQ-08, CHM-08.) Only FIB-12 and MEC-10 are on the KEY-7 path, so the doubling is a small lever and the era base is the real one.
- **Legacy (D10):** main's current Legacy rules unchanged (Firsts pay nothing, Mastery `2 × (era + 1)`, Wonder built `25 × (era + 1)`, Thriving score). No E6-specific rescale.
- **Dominion victory** is unchanged and reported separately from KEY-7 endings.
- **Saves:** a Bronze, Iron or Fief save never becomes Discovery implicitly; the mode byte is 3 and no mode reads another's save or replay.
- **Representation:** keep the **simulation at 1:1**, as in E4 and E5. The catalog's 1:25 is applied only to the **view** if the figure budget (≤ 600 per civilization) is exceeded; the first `--grant-e6` runs must measure it.

## 7. Provisional targets (D-xx; none fitted, none approved)

| ID | Target / diagnostic | Measurement |
|---|---|---|
| D-T1 | Own KEY-6 → KEY-7 about **25 minutes** (D8); seats not finished by the horizon are reported, never imputed | 80+ matches, held-out block |
| D-T2 | **Coal reach:** the share of starts that can see, mine and move coal by 24 seasons after KEY-6, split by geography; no start soft-locked out of KEY-7 (**audit baseline: 28.7% locked before the D13 fix, about 0% after**) | New `e6-audit` over seeds 1–10,000 (the E5 saltpeter audit pattern) plus live trace |
| D-T3 | **Fuel relief:** coal firing and coke cut wood burned for heat and iron by the stated fractions only where coal arrives; full-chain forest work share ≤ the F-T3 cap in mature windows; zero fuel-attributed clearing crossings | The I-T6 / F-T3 protocol reused on the same fixtures and low-wood starts |
| D-T4 | **Power:** steam hours conserve with water and wind in one ledger; no ×-stacking; an engine with no coal or staff shows idle | `--discovery-power-ledger` mirroring F-T2 |
| D-T5 | **Ocean:** first overseas contact and first colony timing against the already-existing outrigger route; voyage loss follows the stated rolls, with WAY-10 and TRD-11 reductions; no omniscient discovery | Per-voyage ledger and a natural-start trace |
| D-T6 | **Colonies:** distance strain and independence events fire only by their seeded rules; no colony transfers land by score | Seeded replay cases |
| D-T7 | **Cannon against bastion:** star forts resist cannon only when built and garrisoned; engines and relief stay viable; musket drill needs GOV-12 | Isolated trials plus live siege attrition on the F-T5 force tables |
| D-T8 | **Institutions:** every E6 institution (factory, botanical garden, learned society, engine, bastion) is working, queued or dormant with a reason within a season | The F-T6 blunder counter, extended |
| D-T9 | Pollution and urban migration follow the stated local rules; plague weight 0.15; no scripted event | Per-cause attribution from own KEY-6 |
| D-T10 | Map fill, Dominion and City share | Diagnostic only until re-anchored on the live E5 trajectory |

## 8. View layer

Build views last. E6 uses the **generic kit** of [08 §13](08-ui-and-world-display.md): a badge burst for Moments, tool tint for method rungs, the modular building kit with a type sign, banner swaps for institutions. The read of E6 is caravels and galleons on open water, colony pennants on far shores, coal-black tiles around pits and canals, smoking chimneys at factories, star-shaped forts and printed broadsheets in the Chronicle. Cards needed: coal stock and fuel by town, power sources (water/wind/steam), ship and colony state, bastion and cannon integrity, institution staffing, KEY-7 progress. The game text must say KEY-7 / Industrial Revolution only in Discovery mode.

## 9. Work breakdown and what can start now

E5 balance is numbers only, so nothing in E6 needs E5 to finish. Rows below change **new** files or additive lines; they never change an E5 rule or number. Do the work in its own worktree and branch (`civsim-e6`, branch `codex/e6-discovery`), never in the shared main tree.

| WP | Deliverable | Needs E5 final? | Files |
|---|---|---|---|
| **WP-E6-0 Scope and plan** | **This document.** | No | `docs/MVP/38-*.md` |
| WP-E6-1 Oracle | **First pass done 2026-11-20** ([report](balance/m13-wp1-oracle-fit.md)): four oracles with `--check` plus `e6_anchors.py`; findings changed CHM-08 (late), DEF-10 (x1.5, integrity-track bastion), AGR-12 (x1.2) and the draft engine (10 units on 1 L coal). Original scope: `e6_research_oracle.py` (pacing, era base, late doubling), `e6_power_oracle.py` (coal, coke, steam fuel and power), `e6_combat_oracle.py` (bastion vs cannon), `e6_economy_oracle.py`. Re-anchor on the live E5 trajectory after each E5 rebase | Reads E5 results; **no** | new tool files only |
| WP-E6-2 Content typing | **Done 2026-11-20** ([report](balance/m13-wp2-content-typing.md)): all 42 rows typed (each stays draft), 12 goods, 10 structures, 3 roles, 4 actions, 11 recipes, validator V23, content-pack golden refreshed. Original scope: type all 41 + KEY-7 rows in place (typed effects, practice, flavor, moment; every row stays `draft`), new goods (coal, coke, sugar, cod, glass sheets, precision parts), structures (mine shaft, engine, factory, bastion, garden, society), roles and recipes, a V-rule (E6 promoted together, typed only), content-pack golden refreshed **at merge** | No (draft rows are inert) | `content/catalog/*.yaml` E6 rows, registries |
| WP-E6-3 Geography audit | **Done 2026-11-20** ([report](balance/m13-wp3-e6-geography.md), command `e6-audit`, 5 tests): 28.7% of starts cannot see coal under today's rules (generator sign collision), coal itself is plentiful, sugarcane is absent from 79% of worlds, outriggers already cross in 2 legs. Original scope: `e6-audit` over seeds 1–10,000: coal visibility and reach per start, sugarcane access, overseas distance, first-contact route, wild-fish offshore; no worldgen change | No (read-only) | new harness command and report |
| **WP-E6-4 Mode plumbing** | `ChapterMode.Discovery = 3`, ceiling 6, KEY-7 ends it with no rival countdown, mode byte in replays, snapshots and the research hash (already `HasMode`), `DiscoveryChain` economy flag containing `FiefChain`, `Key7` forecast field, `--mode discovery`, isolation tests. **Done 2026-11-20** ([report](balance/m13-wp4-mode-plumbing.md)) | Small and additive | `ChapterRules`, `MatchModel`, `MatchReplay`, `LivingEconomyModel.Discovery.cs`, tests |
| WP-E6-5 Coal and steam | **Slice 1 done 2026-11-20** ([report](balance/m13-wp5-coal-slice1.md)): the coal and coke goods layer, coal mining at own deposits after MIN-09, coking in the kiln, coke smelting at a blast furnace with the bloomery standing aside, snapshot and hash tail, 5 tests. **Slice 2 done 2026-11-20** ([report](balance/m13-wp5-steam-slice2.md)): the atmospheric engine (structure, engineer role, coal burn, steam in the shared power ledger), pumping of flooded tier-3 seams, FIR-11 coal firing, 8 tests. **Slice 3 done 2026-11-20** ([report](balance/m13-wp5-transport-slice3.md)): the coke oven (1.2 L coal per coke in half the labor), the navigation canal (coal camps hauled at x10) and turnpikes (road speed x1.5 in the Cohesion walk), 3 tests. Open: the Factory's use of the ledger (WP-E6-8), view (WP-E6-11), natural-play measurement (WP-E6-12). Original scope: System A: coal good, mine staffing and drainage, firing, coke, coke smelting, steam in the power ledger, canals and roads | Reads E5 power and furnace code; additive | `LivingEconomyModel.*Coal*.cs`, tests |
| WP-E6-6 Ocean and empire | **Slice 1 done 2026-11-21** ([report](balance/m13-wp6-ocean-slice1.md)): SEA-12 ocean reach (second sea graph, reach 24, deep-water risk term) and the WAY-10 chronometer (risk x0.3), 4 tests. **Slice 2 done 2026-11-21** ([report](balance/m13-wp6-colony-slice2.md)): TER-11 colonies (reach 40 across the sea, sea strain, blockade strain, seeded yearly independence roll), 5 tests. Remaining: exchange, sugar and fishing fleets, galleons; insurance and joint-stock wait for D12. System B: SEA-12 reach, colonies, exchange, sugar and fishing fleets, galleons, insurance (waits for D12) | Reads sea, naval and voyage code; additive | `PoliticsSystem.Voyage.cs`, `Naval.cs`, tests |
| WP-E6-7 Mind and state | Systems C and D: Insight/Know-how effects, instruments and glass, nation-state, patronage, newspapers, Palace Wonder | Reads E5 staffing and Wonder code | `ResearchSystem.*`, `LivingEconomyModel.*`, tests |
| WP-E6-8 Land, health and manufacture | Systems E and G: four-course, fodder, garden, anatomy, shuttle and jenny, factory, local pollution | Reads AGR-11 and hospital code | `LivingEconomyModel.*`, tests |
| WP-E6-9 War | System F: bastion forts and musket drill on the F-T5 force tables | Reads F-T5 and the cannon work | `PoliticsSystem.*`, oracle, tests |
| WP-E6-10 AI and forecast | KEY-7 routing, Council prior, closure policy, Strategist coal/ocean/colony logic, blunder telemetry | After WP-E6-5/6 | `Ai/*`, harness |
| WP-E6-11 View and game | `DiscoveryFrame`, cards of §8, save/mode selection, generic kit | After core mechanics | view frame, Godot payload/card, tests |
| WP-E6-12 Promotion and whole-match | Promote all 42 rows together (no half-promoted age), `discovery-match` and `--grant-e6` | Yes | |
| WP-E6-13 Large sample and playtest | 80+ matches, held-out cohort, human test | Yes, and only when the whole age works | |

**Order of work:** WP-E6-4 first (small, additive, gives every later slice a flag to read), then WP-E6-2 and WP-E6-3 in parallel (neither touches simulation behavior), then WP-E6-1, then A, B and the rest as the oracle fits them.

## 10. Acceptance series B6-xx (all proposed)

| Gate | Condition |
|---|---|
| B6-01 Isolation | Toy, worldgen, research, politics, Bronze, Iron **and Fief** goldens and checksums byte-identical; E6 inert before own KEY-6 and throughout Bronze, Iron and Fief even with a numeric E6 pack |
| B6-02 Completeness | All 42 nodes typed with requirements/costs/practice/flavor/moment; strict validator clean; registry references resolve |
| B6-03 Determinism | 1,000 Discovery matches replay exactly; snapshot continuation exact; macOS arm64 and Windows x64 produce the same discovery-v1 golden |
| B6-04 Mode and end | All three end clauses, ties, human/headless, KEY-6 continuation, KEY-7 threshold, old-save modes, mode-bearing new saves |
| B6-05 Pacing | D-T1; no imputation of uncompleted KEY-7 |
| B6-06 Coal, fuel, power | D-T2, D-T3, D-T4; exact input/output conservation; mine loss, engine loss, captured coal |
| B6-07 Ocean | D-T5, D-T6; voyages, colonies and independence conserve people and goods |
| B6-08 Siege | D-T7; no wall assault without a breach; powder consumption conserved |
| B6-09 Institutions | D-T8; no unstaffed benefit |
| B6-10 AI | A1–A8 in Discovery mode, Legend ≥ 60% vs Warlord, ≥ 80 mirrored duels; fog fuzz for coal deposits, powder stock, fleets |
| B6-11 Performance | Mean tick ≤ 4 ms, GC ≤ 16 ms; figure budget at E6 p95 population |
| B6-12 Comprehension | ≥ 5/8 uncoached players explain a coal or ocean bottleneck and one money or science effect |
| B6-13 Integration | Full Release suite and strict validation green; golden changes isolated and explained; user approves merge |

## 11. Decisions

| ID | Question | Recommendation | Needs the user? |
|---|---|---|---|
| D1 | Is the next age E6 Discovery, as a default-off DiscoveryChapter that includes E4 and E5? | Yes | **Confirmed implicitly 2026-11-20** ("see what the next era is… begin implementation") |
| D2 | Finish the whole age before any playtest | Yes (standing rule) | No |
| D3 | Raise the economy ceiling through the existing flag-plus-`ProjectCeiling` shim and a per-chapter economy profile, not another mechanism | Yes | No |
| D5 | Keep the simulation at 1:1; use 1:25 only to draw if the figure budget breaks | Yes | No |
| D6 | Own worktree and branch; content-pack golden refreshed only at merge | Yes | No |
| D8 | How long should E6 be? | About 25 minutes, as Iron and Fief | Please confirm or change |
| D9 | Do E6 late projects cost double? | Yes (8 of 41) | Please confirm |
| D10 | Use the Thriving Legacy score? | Yes | No (carried from E5) |
| D11 | A countdown after the first rival's KEY-7? | None; only the human's KEY-7 ends Discovery | No (carried from E5) |
| D12 | Credit model for TRD-10/11 | Wait: the TRD-08 credit model is still undecided in E5 ([27 WP-E5-7](27-faith-and-fief-age.md)); build E6 voyage pooling and insurance **in kind** only after that choice | **Yes, one decision covers both ages** |
| D13 | Coal visibility rule so KEY-7 cannot soft-lock | **Done 2026-11-20 (user: make coal visible, fix the seam-overwrite bug).** MIN-08 feeds coal deposits within reach (25 at E5, 35 at E6) into the Seen mask (`LivingEconomySource.SeenMask`), and `EnsureBarrenEvidence` no longer overwrites a guaranteed sign with the other. The `[Seen: Coal]` gate itself is unchanged. The worldgen golden moved by design (seed 42 fingerprint now `089dd0ae5d889ae2`) | No |
| D7 | Which cut is acceptable if time runs short | None by default; no catalog promise is dropped silently | No |

## 12. Risks

- **E5 numbers still move.** A later E5 era-base, cap or fuel rebase shifts every E6 baseline. Re-anchor after any such change and keep E6 numbers as oracle inputs until WP-E6-12.
- **The content-pack golden is shared.** Typing E6 YAML changes the pack hash; refresh it once, at merge.
- **Coal visibility soft-locked KEY-7 for 28.7% of starts** (§3.2); fixed by D13 on 2026-11-20. Re-run `e6-audit` after any worldgen or MIN-08 change.
- **"Oceans open" may already be open.** Outriggers cross in two or three legs today. If first overseas contact already happens in E3–E5, the E6 reach rule has to change *how* the sea is used (one leg, colonies, exchange), not *whether*.
- **Optional systems may never run.** Most E6 mechanisms are off the KEY-7 path; plan `--grant-e6` early.
- **Star forts can erase gunpowder** a second time, or never matter. Fit with the full force tables.
- **Credit is undecided.** Joint-stock and insurance block on D12; do not invent debt rules.
- **Pollution is new.** Keep it local (tile and health) until KEY-7 makes the global system real.
