# 43 — Information (E9): Networks, Orbit, a Shared Sky and the Planetary Network

> **Planning draft, 2026-11-28.** The last age and the endgame doc proper; the counterpart of [41 — Industry](41-industry-age.md) and [42 — Modern](42-modern-age.md) (both committed; doc 42 at `3d87808`). It covers the audit, the Information-age systems and their prices, pacing and the length of the whole match, **the Apex** (how KEY-10 is built, raced, sabotaged and won), how a match ends for every player, how E9 pays off the E8 Charter, gates, work packages, decisions and risks, and a whole-arc roadmap (§14). **E9 never changes an E8 rule.** Every number is a **draft input to an oracle**.
>
> The owner confirmed on 2026-11-28 that the game has **ten eras, E0 to E9, plus the Apex finish** ([01 §4, §8](01-game-structure.md); `eras.yaml` ids 0 to 9; KEY-10 `enters: 10` = the Apex). E9 is therefore the final age. The ten questions of [42 §8.6](42-modern-age.md) are marked **[A1]** to **[A10]** in the text and listed in the table below. **On 2026-11-28 the owner confirmed A1, A2, A3 and A7, changed A6 (nuclear weapons are usable), and decided D12: a rival's Dominion also ends the match** ("otherwise using them as a deterrent is hollow"). §6.5 gives the nuclear rules that follow from that. A4, A5, A8, A9 and A10 are still working assumptions to confirm.
>
> Audit baseline: worktree `civsim-e6` at `3d87808`. *(verified)* = read from catalog, registries or source; *(assumed)* = from docs, plans or older reports. Sources: [01 §6-§8](01-game-structure.md), [02 §8.3](02-research-structure.md), [03 §4.1](03-research-catalog.md), [03c](03c-research-reach.md), [04 §2, §8](04-research-paths.md), [05 §12-§15](05-simulation-structure.md), [07b](07b-territory-findings.md), [08](08-ui-and-world-display.md), [09](09-population-and-health.md), [13](13-contact-and-conflict.md), [14 §3.3, §7](14-ai-opponents.md), [22 §3.3](22-bronze-age.md), [28 §17](28-production-gap.md), [41](41-industry-age.md), [42](42-modern-age.md).

## Assumptions taken from doc 42: owner rulings and what is still to confirm

| # | Doc 42 question | Status | Position in this doc | If the owner flips it |
|---|---|---|---|---|
| **A1** | How long is a whole match? | **Confirmed by the owner 2026-11-28** | **About 3 h 40 min at Standard** to the Apex (§7.3): E7 32, E8 32, E9 28 minutes | — |
| **A2** | Can a rival end your game? | **Confirmed by the owner 2026-11-28** | **Yes, by the Apex or by Dominion** (owner, D12, 2026-11-28; this replaces the earlier "by the Apex only"). These are the exceptions to D11's "only the human's keystone ends the chapter": either victory by any civilization ends the match for everyone (§8.1, §8.1a). Being eliminated (for example by war or by nuclear strikes) ends the *human's* match, which is defeat, not a rival finish | — |
| **A3** | Dominion victory: build or retire? | **Confirmed by the owner 2026-11-28** | **Build it** (≥ 50% of habitable land and ≥ 2× the runner-up for 5 years, [01 §7.2](01-game-structure.md)). Doc 41's E7 build must include it (§13.3-4). **Any civilization's Dominion ends the match for everyone** (D12), with the rules of §8.1a | — |
| **A4** | One finish or several? | Still to confirm | **One race (Shape 1):** the Apex, steered by the Charter | Three roads (Shape 2): §9 gives each Charter its own finish instead |
| **A5** | The Charter | Still to confirm | **Concord, Ascendancy, Renown**, chosen once at GOV-17 in E8, irreversible, public | Other names, or a Charter that can be changed once at a price |
| **A6** | Nuclear weapons | **Changed by the owner 2026-11-28: usable** | **Usable weapons**, built and first fired in E8 (the catalog's atomic rows are E8), with a real strike, fallout, retaliation and a world punishment for first use (§6.5). Deterrence works because the threat is real: DEF-14 becomes **second-strike capability**, not a ban on war | — |
| **A7** | Sabotage | **Confirmed by the owner 2026-11-28** | **Allowed** for the human and the AI against rival uplinks; **never between members of one pact**; Renown cannot order it | — |
| **A8** | Retire button and elimination | Still to confirm | **Both** (§8.1): the player may end the match at any time; a civilization with no settlements is eliminated | Neither: a stalled game stays open, as today (but nuclear strikes make elimination likelier, so an elimination rule is strongly advised) |
| **A9** | Climate | Still to confirm | **Shared climate stress from E8**, bounded; E9 has the full system and its answers; nuclear strikes add to it (§6.5) | No climate until E9, or none at all |
| **A10** | After the Apex | Still to confirm | **"Keep watching"** continues the world with no further scoring | The Apex is final: the match closes to the end screen |

## 0. The short version (what to accept)

- **E9 is Information**, the post-modern, digital and global age ([01 §8](01-game-structure.md)), entered by KEY-9 *Digital Revolution* and finished by **KEY-10 *Planetary Network***, the Apex. Its chapter, `ChapterMode.Information = 6`, plays the **whole arc** and is the last mode the game needs.
- **51 draft rows (50 projects plus KEY-10).** For breadth 19 that is a little thin (2.6 nodes for each project required, against 2.7 to 3.1 in earlier ages), and E9 must pay off the E8 Charter. I add **3 projects, one capstone per Charter**, each visible only to civilizations of that Charter: 53, of which a civilization sees 51.
- **Length: 28 minutes** from KEY-9 to the Apex, of which the uplink build race is the last 6 to 8 minutes. Growth slows again (fertility ×0.35), so the research bill is about 2.4 times E8's and the **era base about 1,100,000** (registry: 9,000,000, about 8 times too high). **The whole match then comes to about 3 h 40 min [A1]**, about 25 minutes over doc 01's old Standard target of 3 h 15 min.
- **The Apex.** KEY-10 completes only when its Insight is paid **and five working uplinks** stand in the civilization's territory, at least 15 tiles apart. The first civilization to do so ends the match for everyone [A2]. Rivals can sabotage an uplink (it goes dark for a time, never destroyed), embargo its materials, take the town it stands in, or, at a terrible price, destroy it with a nuclear strike. The Apex earns a large Legacy bonus, but **the final ranking is Legacy**, so the race winner usually but not always tops the table. Dominion is the only other finish, and **a rival's Dominion ends the match too** [A3, D12, §8.1a]; retiring and elimination are defined [A8].
- **Nuclear weapons are usable [A6, owner decision].** One weapon (a warhead, delivered by bomber or rocket), built from E8's atomic rows. A strike kills most of one town, destroys what stands next to it (an uplink included) and leaves fallout that fades over years. The first civilization to strike another pays heavily: a Legacy penalty, a world coalition and embargo, and the loss of its Charter's benefits if it is Concord. Deterrence is real because a civilization with dispersed warheads (DEF-14) can always strike back.
- **The Charter pays off.** Concord can build uplinks with its pact partners and shares the Apex; Ascendancy sabotages and captures harder but draws the coalition sooner; Renown builds cheaper and slows rivals' uplinks under its culture, but cannot sabotage.

## 1. Which age, and what it ends with

| Check | Finding |
|---|---|
| Rows | **50 regular projects plus KEY-10 = 51**, all `status: draft`, prose only, no gates *(verified)*. Matches [03 §4.1](03-research-catalog.md): Craft 17, Kin 14, Reach 19. Types 33 M, 11 I, 3 D, 3 S, no Wonder. |
| Keystones | Entered by **KEY-9 *Digital Revolution*** (`era 8, enters 9`). Left by **KEY-10 *Planetary Network*** (`era 9, enters 10`, `requires: [COM-16, WAY-13, NRG-16, DIP-14]`, `breadth: {era: 9, min: 17}`): "Must be built as 5 uplink sites spread across your territory (each ≥ 15 tiles apart). The first to finish wins the Apex victory." *(verified)* |
| Era row | Base **9,000,000**, breadth 17, representation 1:500, claim radius 9, reach 160, fertility ×0.35, mortality 0.0009 / 0.0015 / 0.027 *(verified)*. |
| Today | The base game plays E0 to E6 (Discovery default). E0 to E6 rows are numeric, E7 to E9 draft *(verified by status count)*. E7 and E8 are planned in docs 41 and 42, not built. |
| End screen | Since `788d396` the title and record follow the chapter (Bronze, Iron, Fief, Discovery each with a keystone title) *(verified, `Game.Modals.cs`)*. It knows only "human keystone" and "rival countdown" endings. |

Working names: **InformationChapter** (the whole arc), milestone **M16**.

## 2. What Information should feel like

Modern set the nations against each other and against the world they share. **Information makes the world one system and asks who will connect it first.** Work moves from muscle to flows; knowledge spreads almost instantly; the sky fills with satellites; the climate bill from two industrial ages comes due; and every civilization can see the finish line.

| Tension | What pushes | What answers it (and what it costs) |
|---|---|---|
| **Connected minds** | Internet, mobile networks, AI-assisted research, open knowledge: Insight and diffusion soar. | Data centers draw huge power; diffusion helps rivals as much as you; automation frees workers who need new work. |
| **The shared sky** | Satellites see the whole map; remote sensing finds every deposit. | Everyone sees your uplinks too: the race is public. |
| **The bill comes due** | Climate stress from E7 and E8 raises floods, droughts and storms for everyone. | Clean grids, accords and green chemistry cost sites, lithium and output; free riders gain. |
| **One market** | Containers, supply chains and digital finance make trade nearly frictionless. | Just-in-time chains break under disruption: one war or storm reaches far. |
| **The race** | Five uplinks and the Planetary Network end the game. | Sabotage, embargoes, coalitions and war; a realm too small cannot even site five uplinks 15 tiles apart. |

## 3. Scope audit

### 3.1 Inventory *(verified)* — 50 planned + 3 proposed (\*)

| System | Projects |
|---|---|
| **A. Networks and computing** (11) | CER-15 fiber optics · COM-16 Internet (KEY-10) · COM-17 mobile networks · MEC-14 integrated circuits · MEC-15 personal computing · MEC-16 robotics and automation · NUM-14 data science · LRN-13 open knowledge · LRN-14 AI-assisted research · GOV-15 digital governance · WAY-14 GIS |
| **B. Space and sensing** (3) | WAY-13 satellite constellation (KEY-10) · MIN-15 rare earths and lithium · MIN-16 remote-sensing prospecting |
| **C. Clean power and climate** (13) | NRG-13 solar · NRG-14 wind · NRG-15 batteries · NRG-16 sustainable grid (KEY-10) · INQ-19 ecology and climate science · DIP-15 climate accords · BLD-16 green buildings · CHM-14 green chemistry · MET-18 advanced materials · FIR-15 induction and plasma heating · WOD-14 mechanized forestry · FOR-12 seed banks · HNT-15 sustainable fisheries |
| **D. One market** (8) | TRD-15 digital finance · TRD-16 global supply chains · TRN-16 containers · TRN-17 high-speed rail and EVs · SEA-16 container ships · FOD-14 cold chain · FIB-15 technical textiles · TER-15 smart megacities |
| **E. Biotech and longevity** (6) | INQ-20 molecular biology · AGR-16 precision farming · AGR-17 engineered crops · MED-15 genomic medicine · HUS-14 animal genetics · CHM-15 nanomaterials |
| **F. Culture and society** (5) | ART-14 global pop culture · ART-15 interactive media · COM-18 social media · SOC-15 networked society · BEL-15 pluralism |
| **G. Security and the race** (7) | DIP-14 Global Governance Forum (KEY-10) · WAR-17 precision and drone warfare · WAR-18 peacekeeping · DEF-15 cyber defense · **DIP-17\* Joint Uplink Programme** (Concord) · **WAR-20\* Strategic Command** (Ascendancy) · **ART-16\* Open Culture** (Renown) |

### 3.2 KEY-10 closure *(verified by script)*

1. **In-era closure: 13 projects** (CER-15, COM-16, DIP-14, INQ-19, MEC-14, MEC-15, MIN-15, NRG-13, NRG-14, NRG-15, NRG-16, NUM-14, WAY-13), six of them late (D9). Breadth 17 leaves 4 free picks. [04 §2](04-research-paths.md) lists 13 critical and 17 breadth: it agrees.
2. **The Apex debt from E8.** KEY-10 needs 14 E8 rows that KEY-9 does not ([42 §3.2](42-modern-age.md)), plus 38 older rows (agriculture, arts, forestry and more). A civilization that took none of them in E8 owes **about 7.0 million Insight** at E8's draft base 400,000 and EraLag 0.7. With E8's six free picks spent on Apex preparation, roughly half of that is already paid. **This is how E8's choices reach the end**: the Apex debt is the readiness gap that §7 prices.
3. **No E9 row carries a gate**, and KEY-10 is research-only in the catalog's grammar; "built as 5 uplink sites" is new (§6). The physical inputs of uplinks must come from guaranteed or made goods, never from a gated deposit (rare earths and bauxite have no guarantee *(verified)*), or the Apex soft-locks for some starts.
4. **D9 marks 35 of 50 rows**, the highest share of any age.
5. **Promises to keep:** [01 §7.3](01-game-structure.md) (first to finish wins, sabotage, Legacy still ranks); [05 §14](05-simulation-structure.md) (climate dilemma and its mitigation rows, all in E9); [08](08-ui-and-world-display.md) (data-link arcs, satellites, Apex uplinks on the map, the "Apex uplink sabotaged" critical alert, haze thick or clearing by path); [14 §3.3](14-ai-opponents.md) (`ApexRace` plan, coalition against whoever "reaches the Apex first"); [04 §8](04-research-paths.md) (late posture decides whether the race is peaceful or contested).

### 3.3 The three proposed nodes: one Charter capstone each

A new gate kind, **`[Charter: X]`**, makes a row visible only to a civilization that chose Charter X at GOV-17 [A5]. It is a visibility gate like `[Seen]` and never sits on the KEY-10 path, so V8 does not apply. Each is type I, era 9, late (cost 3 base units).

| Id | Name | Gate | Requires | Does (P) |
|---|---|---|---|---|
| DIP-17 | Joint Uplink Programme | Concord | DIP-14, WAY-13, GOV-15 | Uplinks may stand in a pact partner's territory with its consent; the Apex winner's pact partners share the Apex bonus (§8.3) |
| WAR-20 | Strategic Command | Ascendancy | WAR-17, DEF-15, MEC-16 | Sabotage odds ×1.5 and outage ×1.5; a captured town's uplink keeps working for the captor if spacing holds |
| ART-16 | Open Culture | Renown | ART-14, LRN-13, COM-16 | Own uplinks −20% labor; rival uplink work in tiles under the Renown civ's soft claim −25% |

Each has a cross-theme prerequisite (V6). **Considered and not proposed:** a "data" good (link capacity already carries it), a space colony or second Apex, a cyber-war system beyond sabotage, separate victory projects per Charter (that is Shape 2, [A4]).

### 3.4 Code ceilings *(verified unless noted)*

| Boundary | State | Consequence for E9 |
|---|---|---|
| Chapter modes | Bronze 0 to Discovery 3 built; Industry 4, Modern 5 planned | Append **`Information = 6`**, containing Modern, ceiling 9. It is the whole arc; no `Apex` mode. |
| Match end | `MatchEndReason` None 0, HumanKeystone 1, RivalCountdown 2, YearCap 3 (obsolete) *(verified)*; `MatchEnd` has no winner field | Append **`Apex` 4, `Dominion` 5, `Eliminated` 6, `Retired` 7**; add a **winner civilization** to `MatchEnd`. Never renumber. |
| Commands | `CommandKind` append-only (CLAUDE.md rule 4) | Append **`Retire`**, **`Sabotage`** (target uplink) and **`SiteUplink`** (optional placement hint); E8 appends **`Strike`** and the **retaliation standing order** (doc 42). The replay format does not change. |
| End screen | Chapter → (end era, title, rival title) for four chapters *(verified)* | Becomes **(chapter, reason, winner) → title and record**; §10. |
| AI | `GrandPlan.ApexRace` "reserved but not yet scored"; `CoalitionAgainstLeader` scored *(verified)* | Score `ApexRace`; give the coalition an uplink-count trigger. |
| Legacy | research Legacy (Mastery + Thriving + Wonders) plus Dominion in the frame *(verified)* | The Apex bonus is added to research Legacy as a keystone award; no new term. |
| Pace | One pace only; no Quick or Epic in code *(verified by grep)* | [01 §6](01-game-structure.md)'s pace scaling is unbuilt; see §7.3. |

## 4. What E8 leaves for E9 *(planned in [42](42-modern-age.md), not built)*

| E9 | Builds on (E8) | Watch |
|---|---|---|
| COM-16/17, CER-15, MEC-14/15/16, LRN-13/14, GOV-15 | Grid, computer, labs, mass education, telephone and broadcast | Data links are a **link structure** like rail and grid lines; data centers draw grid power. |
| WAY-13, MIN-15/16, WAY-14 | Rocketry, Orbital Launch (WAY-15, −20% on WAY-13), aerial survey | Satellite sight must keep [13 §2.5](13-contact-and-conflict.md)'s "no cheating": surface only. |
| NRG-13 to 16, INQ-19, DIP-15, CHM-14, BLD-16 | Climate stress and the E7 emissions ledger ([42](42-modern-age.md) knock-on 2), NRG-17 emission controls | The stress level carried into E9 is the E7 and E8 bill. |
| DIP-14, WAR-17/18, DEF-15, the capstones | World assembly, pacts (DIP-16), intelligence (WAR-19), the Charter (GOV-17), the nuclear arsenal (WAR-16), second strike (DEF-14) and arms control (DIP-13) | One leader definition (end-screen Legacy) everywhere; E9 adds no nuclear row, only the Apex, Charter and end-rule interactions of §6.5. |
| TER-15, TRD-15/16, SEA-16, TRN-16/17 | Suburbs (claim +3), trade order, steel hulls, highways | Megacities add claim radius +2 again: Dominion and uplink spacing both move. |

## 5. Information-age systems (proposed bounded interpretations)

Deterministic throughout (D1 to D10): `Fix64`, seeded `Pcg32.ForStream(seed, system, entity, tick)` rolls, id-indexed arrays, ascending-id iteration, state hashed and snapshotted only in an Information match.

### 5.1 Networks and computing (A)
A **backbone** (COM-16) links cities by data link: connected towns get Insight +25% and adopt new methods in the same season. CER-15 raises link capacity ×10; COM-17 gives rural towns the urban Insight rate. LRN-14 doubles researcher output and spreads Know-how to every slotted project; NUM-14 gives planners +10%; GOV-15 cuts admin cost −50% (Overreach under the existing cap, never zero). **Prices:** every data center draws grid power (P: as much as a factory); MEC-16 automation runs factories with 80% fewer operatives, and **freed workers who find no service or research post feed E7's unrest reading** rather than vanishing; diffusion gains help contacted rivals too.

### 5.2 Space and sensing (B)
WAY-13 needs a **launch site** and puts up satellites: the whole map's terrain, structures and uplinks become visible to the owner, with perfect navigation; never a rival's research, stocks or plans. MIN-16 reveals every deposit on the map. MIN-15 makes rare earths and lithium goods for batteries; with no guarantee *(verified)*, they must not be an uplink input (§3.2.3). **Price:** launches use alloys, fuel and power; and a satellite owner's uplinks are as visible as everyone else's.

### 5.3 Clean power and climate (C)
Climate stress (begun in E8 [A9]) is one world value fed by every civilization's emissions; above its thresholds it raises the frequency of seeded **droughts, floods, storms and crop failures** for everyone (P: up to ×2 at full stress) ([05 §14](05-simulation-structure.md)). Answers: solar fields (best in sunny biomes), wind farms (plains, coasts), batteries (need lithium), the **sustainable grid** (energy pollution −80%, climate damage −50%; a KEY-10 prerequisite, so every Apex contender must clean up), DIP-15 accords (−40% for signatories), green chemistry, buildings, materials, forestry, fisheries and seed banks. **Prices:** sites, output and lithium; accords bind signatories while free riders still emit; stress decays slowly, so E7's and E8's dirty growth keeps costing in E9. Bound: climate deaths stay under the [09 §10.5](09-population-and-health.md) 10% share.

### 5.4 One market (D)
Containers and container ships make long-distance trade ×5 at a haul cost −70%; digital finance makes internal distribution near-frictionless; **supply chains** give craft +20% but carry a seeded **disruption** roll whose odds rise with war, blockade and climate events (a disrupted chain loses its bonus for a season). TER-15 megacities double density and add claim radius +2. **Price:** fragility, and the claim growth that worries neighbours.

### 5.5 Biotech and longevity (E)
Molecular biology cuts MED, AGR and HUS late-project costs −20% (through the diffusion floor); engineered crops halve crop failure and open marginal biomes; precision farming lowers inputs; genomic medicine adds lifespan and elder Insight ×1.25; nanomaterials raise the MET, CER and NRG ladders +30%. **Price:** mostly cost and the power these labs draw; the population keeps ageing under fertility ×0.35.

### 5.6 Culture and society (F)
ART-14 doubles soft claim and lowers rival Cohesion **against** you −10% (they like your culture) and adds Beacons; COM-18 social media and SOC-15 networked society add **Cohesion volatility**: seeded swings either way, bounded per season; BEL-15 makes diverse towns stable. **Price:** volatility can tip a town into E7's unrest rules; soft claim provokes Relations once its owner leads.

### 5.7 Security and the race (G)
DIP-14 Global Governance Forum (a KEY-10 prerequisite) is E8's world assembly grown up: members' disaster damage −30% and the forum's votes on sanctions and embargoes. WAR-17 drones double combat with low own casualties and enable **sabotage**; WAR-18 peacekeepers end conflicts in neutral or allied zones and earn Legacy for peace; DEF-15 cuts sabotage against you −80%. The capstones of §3.3 sit here. §6 gives the race's rules.

### 5.8 Deliberately not in E9
No second finish beyond Apex and Dominion [A4], no second nuclear weapon type, missile defense or new nuclear row (E9 uses E8's arsenal, §6.5), no money or debt, no orbital colonies, no new deposit, no scripted events, no project that creates people, and no rule that reads another civilization's hidden state.

## 6. The Apex: building and racing the Planetary Network

### 6.1 What has to exist
- **Uplink site** (new structure, unlocked once WAY-13 and COM-16 are known): stands on a Town or City tile in the civilization's own territory (or a pact partner's with DIP-17), **at least 15 tiles from every other uplink of the same network** (catalog), connected to the capital by the data backbone and to the grid. **Cost (P):** 120 work-seasons of labor, plate-and-alloy materials (MET-17 alloys, CER-14 silicon, concrete), a crew of six operators, and a grid draw. All inputs are made goods from guaranteed or ubiquitous sources (§3.2.3).
- **A working uplink** has its crew, its power and its backbone link this season; otherwise it is dark and the card says why.
- **KEY-10 completes** at the first season end at which (a) its Insight cost is paid (2.5 × era base, the normal keystone rule) **and** (b) five working uplinks belong to the civilization's network. Research can finish before the uplinks or the other way round; both must hold together.
- **Geography.** Five sites pairwise ≥ 15 tiles apart need a wide realm. At E9's claim radius 9 a civilization holding about 1,300 to 1,600 tiles ([07b](07b-territory-findings.md)) can fit them, but a compact one may not; colonies and megacity claim growth help, and Concord pact partners help most. WP-E9-3 measures how many civilizations *can* site five; none may be locked out without a route (a colony, a pact, expansion).

### 6.2 What everyone sees and how rivals react
- Uplinks are **public structures**: anyone who sees the tile (or has satellites) knows them. Every contacted civilization's **uplink count** is shown; at **3 working uplinks** all civilizations are warned, and at **4** a critical alert fires ([08](08-ui-and-world-display.md) already lists "Apex uplink sabotaged").
- **AI:** `ApexRace` is scored from the civilization's own ETA (research left plus uplinks to build) against the best rival's believed ETA ([14 §3.3](14-ai-opponents.md)). Every other AI evaluates `CoalitionAgainstLeader` when a rival reaches 3 uplinks (2 against Ascendancy) or is forecast to finish first. The coalition's actions are those it already has (embargo, alliance, war) plus sabotage and assembly sanctions.

### 6.3 How to sabotage, delay or take it
| Action | Needs | Effect (P) | Price |
|---|---|---|---|
| **Sabotage** an uplink | WAR-17 and a WAR-19 intelligence service; the target uplink known; not a pact partner [A7]; not Renown | Seeded roll: 25% a try (×0.2 against DEF-15, ×0.5 against the target's counter-intelligence, ×1.5 with WAR-20). Success: the uplink is **dark for 8 seasons**, never destroyed | Relations −40 with the target, reputation −20 with all who learn of it; one try per uplink per season |
| **Embargo** uplink materials | DIP-12/DIP-14 assembly majority, or an existing pact embargo | The target's imports of alloys and silicon stop; its own production still counts | Relations with the target; votes are public |
| **Take** the town | War and capture ([13 §8](13-contact-and-conflict.md)); possible against anyone, but against a nuclear power the attacker's AI prices in retaliation (§6.5.4) | The uplink stops for the loser; it works for the captor only with WAR-20 and if spacing holds | All the costs of war (E7 to E9 Cohesion costs), and the risk of a nuclear answer |
| **Strike** the town [A6] | WAR-16 warhead and a delivery (§6.5.2); at war with the target | The uplink and everything within 1 tile are **destroyed**; the town loses most of its people; fallout (§6.5.3) | First use: the full world punishment of §6.5.5; retaliation from a second-strike holder |
| **Out-build** | Research and five sites | The race itself | — |

**Sabotage stays the normal tool.** A strike destroys one uplink at most (uplinks stand ≥ 15 tiles apart, a strike reaches 1 tile), the victim can rebuild it in 120 work-seasons, and the striker pays a Legacy penalty, a world coalition and possibly a counter-strike. Sabotage costs Relations only. The bomb is therefore a last resort against a rival about to win, never a cheaper way to do what sabotage does.

### 6.4 How the match ends (see §8 for every case)
When a civilization completes KEY-10, the match ends at that season end for everyone [A2]. **Ties** (two in the same season) go to the higher Legacy, then the lower seat number, the existing deterministic tie rule. The winner receives the **Apex bonus** (P: about 10% of the median final Legacy, fitted so that the Apex winner finishes first on Legacy in about two matches of three: the race decides most games but a far-ahead leader can still finish first, [01 §7.3](01-game-structure.md)). Concord partners share it (§9).

### 6.5 Nuclear weapons: usable, and therefore a real deterrent [A6, owner decision 2026-11-28]

The owner ruled that the bomb must be usable, because "otherwise using them as a deterrent is hollow". The design below keeps it proportionate: **one weapon type, seven rules, no new research row.** It uses the catalog's own atomic rows *(verified)*: INQ-15 ("enables nuclear"), **INQ-16 Nuclear Physics** (E8, D: "enables reactors and weapons"), NRG-12 fission power (E8), **WAR-16 Nuclear Weapons** (E8, D: "Use triggers global catastrophe and Legacy penalties"), **DEF-14 Nuclear Deterrence** (E8, I), **DIP-13 Arms Control** (E8, I: "Nuclear catastrophe risk −90%; military upkeep −20%"), DEF-13 radar and air defense (E8), TRN-15 aviation and WAR-15 mechanized warfare (bombers), INQ-17 rocketry (rockets). No catalog row names missiles, ICBMs or missile defense, so none is added. **The bomb arrives in E8** and doc 42 builds it (its WP-E8-11); E9 adds only the Apex, Charter and end-rule interactions below. All numbers are drafts (P).

#### 6.5.1 Building warheads (rule 1)
- **WAR-16** unlocks the **enrichment works**: a structure in a City on the grid with 160 work-seasons of labor, 20 alloys and concrete, a crew of 6, and a grid draw of 40 power units a season (about four factories). It makes **one warhead every 8 seasons** (two years) while staffed and powered. Each stored warhead costs 1 alloy and 2 power units a season to keep; DIP-13 cuts upkeep −20% (the catalog's figure).
- Warheads are a good (`G-WARHEAD`) held at the works' city. Arsenals stay small by cost: a civilization with one works has about 4 warheads after 8 years.

#### 6.5.2 Delivery and interception (rule 2)
- **Bomber** (TRN-15 aviation and WAR-15): from an own airfield to a target within 25 tiles. A target inside a rival's **DEF-13 radar** coverage (12 tiles from a radar station) shoots a bomber down on a seeded 50% roll; the warhead is lost.
- **Rocket** (INQ-17 rocketry): 10 alloys and 10 fuel per rocket at the works; reaches any tile the owner can see; **cannot be intercepted** (no catalog row offers missile defense). That is why deterrence in the late game rests on retaliation, not on shields.
- **Strikes need a state of war** with the target (declaring war already costs Relations, [13](13-contact-and-conflict.md)), and target a **settlement** the striker can see. A strike order is a `Strike` command (appended to `CommandKind`); the human gives it through a two-step confirmation.
- **Timing:** a strike is ordered in a season and resolves at that season's end. The launch is **public at once** to every civilization that sees the launcher or the target, so a second-strike holder can answer **in the same season** ("launch on warning"). All strikes of a season are resolved together from the state at the start of resolution, in ascending civilization order for bookkeeping only, so the result does not depend on order (D-rules).

#### 6.5.3 What a strike does (rule 3)
- **The town:** 60% of its people die; every structure on its tile and within 1 tile is **destroyed** (an uplink, works, walls, the enrichment works and its stored warheads unless the owner has DEF-14). A town left with fewer than 6 people becomes a ruin; its land loses its claim source.
- **Fallout:** a per-tile value, 100 on the target, 50 within 1 tile, 25 within 2, fading 5% a year (about half in 14 years, i.e. 14 minutes of play). On a fallout tile adult and elder hazards are ×(1 + fallout ÷ 200) and field yield ×(1 − fallout ÷ 100); no settlement may be founded or claim a tile while its fallout is above 50, and such tiles drop out of Dominion's habitable weight. Fallout lives in its own per-tile `Fix64` array, hashed and snapshotted only once any strike has happened.
- **The world:** each strike adds a climate-stress pulse (P: 5% of the full-stress threshold, [A9]) and cuts every civilization's field yield by 5% for 4 seasons (a "nuclear winter", capped at −25% however many strikes). Everyone pays, the striker included.
- **Cohesion:** the struck civilization's other towns rally (+5 for 8 seasons); the town next to fallout loses 15. A first user's own towns lose 10 for 8 seasons (horror at home).

#### 6.5.4 How deterrence works in the sim (rule 4)
- **Who knows what.** Completing WAR-16 triggers a **public test** (a Moment and a Chronicle line) that every contacted civilization sees, because a deterrent nobody knows about deters nobody. Completed projects are already glimpsed on contact ([13 §2.4](13-contact-and-conflict.md)), so DEF-14 is public too. **The warhead count is hidden**: rivals estimate it from the works they have seen and the years since (one per 8 seasons); WAR-19 intelligence reveals it exactly, and an arms-control treaty (rule 6) reveals it to the other party.
- **Second strike (DEF-14, changed).** The catalog's DEF-14 says "rivals cannot declare war of conquest on you". With usable weapons that hard ban would make the bomb pointless, so **DEF-14 becomes second-strike capability**: the holder's warheads are dispersed and hardened, so no strike can destroy them, and it can always strike back. Without DEF-14, warheads stored at the works' city are destroyed with it.
- **Retaliation doctrine.** Every AI **always** strikes back at a civilization that has struck it, if it can. The human has a standing order, **"Retaliate if struck"**, on by default (a Posture-like lever, [01 §5.4](01-game-structure.md)), so the human's threat is as credible as the AI's.
- **What the AI weighs.** The Strategist's war value ([14 §6.2](14-ai-opponents.md)) gains one cost term: expected retaliation = the chance the target answers with a strike × the expected loss of one of the attacker's largest towns × the target's believed warheads. The chance is 0 for raids, 0.3 for a conventional war against a nuclear power (P), 0.8 once the target has lost a city to that war, and 1.0 after a strike. Conquest of a nuclear power is therefore **possible but rarely worth it**, which is what deterrence means; the hard "cannot be taken" rule of the earlier draft is dropped for this reason.
- **When an AI fires first** (avoiding pointless suicide). Only if all of these hold: (a) it is at war with the target; (b) its forecast Legacy *after* every penalty of rule 5 and the expected retaliation is higher than without the strike; and (c) one of two situations: it faces **elimination or the loss of its capital** to that war, or the target is within 4 seasons of the Apex, or within 4 seasons of a Dominion win, and neither sabotage nor war can stop it in time. It **never strikes first at a second-strike holder** unless it faces elimination ("nothing left to lose"). Most matches should see no strike; the gate is X-T11.
- **Fear changes diplomacy.** A public test lowers every non-partner's Relations with the tester by 10; non-nuclear AIs value pacts with nuclear powers more (a Concord pact gives an umbrella, §9); and `CoalitionAgainstLeader` switches its target to any first user, whatever its Legacy (rule 5).

#### 6.5.5 The price of first use (rule 5)
**First use** = striking a civilization that has not struck anyone. A strike back at a first user is **retaliation** and carries none of this except the world effects of rule 3 and Relations −20 with third parties.
- **Legacy:** the first user loses 15% of its end-screen Legacy at the strike, and 10% more for each further first-use strike; its standings row is marked **Aggressor** for the rest of the match, and **an Aggressor's Apex bonus is halved**.
- **The world reacts:** every civilization that learns of it takes Relations −60 with the first user; the world assembly (DIP-12, later DIP-14) votes an embargo automatically; every AI's coalition plan targets the first user.
- **The Charter reacts** (§9): Concord loses its Charter benefits; Renown pays double; Ascendancy pays less.

#### 6.5.6 Arms control and accidents (rule 6)
DIP-13 lets two holders sign an **arms-control treaty**: each sees the other's count, both cap warheads at 3, and both get the −20% upkeep. Building over the cap is a broken treaty (Relations −60, reputation −20, [13](13-contact-and-conflict.md)). The catalog's "catastrophe risk −90%" is read as an **accident roll**: each arsenal rolls once a year (P: 1% per 5 warheads) for an accident that drops fallout 50 on the works' tile without a detonation's deaths; DIP-13 multiplies the roll by 0.1. Reactor meltdowns (NRG-12) stay their own rule ([42 §5.1](42-modern-age.md)).

#### 6.5.7 The Apex, the end and the human (rule 7)
- **Uplinks** are structures, so a strike destroys one (§6.3). Spacing means a strike can cost a rival at most one uplink. A first strike on a rival's uplinks is still first use and pays the full price.
- **Elimination:** a civilization whose last settlement is destroyed by a strike is **Eliminated** like any other (§8.1). One strike hits one town, so a human with several towns gets warnings before it comes to that: the rival's public test, the "Relations hostile and the rival can deliver" alert, and the launch itself, which can be answered in the same season.
- **Counterplay for the human:** spread out (one strike, one town); radar against bombers; DEF-14 and the retaliation order; an arms-control treaty; a Concord pact's umbrella; or pressure through the assembly before a rival has a deliverable arsenal.
- **Legacy and ranking:** the Aggressor mark and penalties stay on the standings and in the replay; a nuclear aggressor can still win the Apex, but at half its bonus and after losing 15% or more of its score.

## 7. Pacing and the length of the whole match

### 7.1 From doc 42's plan
Doc 42 starts E8 at about 227,000 Insight a minute and grows it 4.5% a minute for 32 minutes: **about 960,000 a minute at KEY-9** (planned, not measured).

### 7.2 Growth in E9 and the bill
Fertility falls to ×0.35 and [09 §11.2](09-population-and-health.md) has population growth at 1.8% a year (a year is a minute), about 1.5% a minute of Insight from headcount. E9's own rows push hard the other way (backbone +25%, AI research ×2 for researchers, mobile networks, personal computing ×1.3). **Central r = 0.035**, range 0.02 to 0.05. The research part of the age (KEY-9 to KEY-10's Insight paid) is aimed at **about 25 minutes**, with the uplink build overlapping its last 6 to 8 minutes, so KEY-9 to the Apex is **about 28 minutes**.

Bill for 25 minutes of research at r 0.035 = 960,000 × (e^(0.875) − 1) / 0.035 ≈ **38 million**, about 2.4 times E8's 16 million. Subtract the unpaid half of the Apex debt (about 3.5 million, §3.2.2) and divide by the path units:

| Breadth | Free picks | Path units (D9, KEY-10 2.5) | Base for 25 min research | Completions | Minutes each |
|---|---|---|---|---|---|
| 17 (catalog) | 4 | 29.3 | ~1,190,000 | 18 | 1.4 |
| **19 (recommended)** | **6** | **31.3** | **~1,110,000** | **20** | **1.25** |
| 20 | 7 | 32.3 | ~1,080,000 | 21 | 1.2 |

Sensitivity at breadth 19: r 0.02 → ~880,000; r 0.05 → ~1,420,000; 22 minutes of research → ~900,000, 28 → ~1,350,000. Each 10% on the base moves the research part by about 1.4 minutes. **Draft input: base 1,100,000, breadth 19, D9 applied, KEY-10 2.5 × base**, fitted live in WP-E9-13 against E8's measured rate. The registry's 9,000,000 would make the age about two hours.

### 7.3 The whole match [A1]

| Stretch | Minutes | Source |
|---|---|---|
| Start to KEY-6 (E0 to E5) | ~105 | KEY-6 median near minute 100 to 110 in the E5 readiness audits of 2026-11-15/16 *(assumed; E5 has been rebalanced since)* |
| E6 Discovery | 25 | measured, held out 25.4 ([38](38-discovery-age.md)) |
| E7 Industry | 32 | planned ([41](41-industry-age.md)) |
| E8 Modern | 32 | planned ([42](42-modern-age.md)) |
| E9 Information to the Apex | 28 | this doc |
| **Whole match** | **~222 (3 h 42 min)** | |

Against [01 §6](01-game-structure.md): the original targets were Quick about 90 minutes, Standard about 3 hours, Epic about 5 hours, and the progressive cadence model gave 97.5 / 195 / 338. **The plan runs about 27 minutes (14%) over the 195-minute Standard model.** That comes from the owner's own choice of longer ages for E7 to E9, not from drift. Recommendation: **accept about 3 h 40 min as the new Standard** and update doc 01 §6 when the arc is built, because the late ages are where "there is much more to do and watch late" (01 §6). Two conflicts to note: (1) **Quick and Epic paces do not exist in code** (one pace only, verified by grep), so today every match is Standard; building Quick pace (7.5-second seasons) would give about 1 h 50 min with the same research costs. (2) Doc 42 §7.3 placed KEY-8 near minute 150 and the Apex near 210 because it started E6 at minute 90, a figure from runs with E5 granted; with E5 played the times are about 12 minutes later (knock-on §13.2-1).

### 7.4 The `e9_research_oracle.py` plan (WP-E9-1; not written here)
As docs 41 and 42, with `e9_anchors.py` reading E8's measured KEY-9 rate. It also models: (a) the **Apex debt** per archetype (how much of the 14 E8 Apex-prep rows each did), (b) the **uplink build time** from labor, materials and spacing (from `e9-audit` site counts), so that "research done" and "five uplinks working" can be timed together, and (c) the race's spread: the gap between the first and second Apex ETA, which decides whether sabotage and coalitions have time to matter (target: a median gap of at least 3 minutes). Companions: `e9_climate_oracle.py` (stress and event frequency from the E7-E9 emissions, the 10% bound), `e9_race_oracle.py` (sabotage odds, outage and coalition timing against the build curve).

## 8. How a match ends, for every player

### 8.1 End reasons (appended to `MatchEndReason`)

| Reason | When | Who it ends for |
|---|---|---|
| **Apex** (4) | A civilization completes KEY-10 (§6.4) | Everyone; winner recorded |
| **Dominion** (5) [A3, D12 decided] | **Any civilization** holds ≥ 50% of habitable land and ≥ 2× the runner-up for 5 consecutive years ([01 §7.2](01-game-structure.md)); a public warning at 40% (§8.1a) | Everyone; winner recorded |
| **Eliminated** (6) [A8] | The human civilization has no settlements left (by conquest, secession or nuclear strikes, §6.5.7) | The human's match ends; the end screen shows the standings at that moment; "Keep watching" spectates the AIs until another reason fires |
| **Retired** (7) [A8] | The human gives the `Retire` command | Everyone, at the next season end, on the current standings |
| Open (None 0) | Nothing has fired | The match continues; there is **no cap** ([28 §17](28-production-gap.md)); headless runs stop at their `--minutes` horizon |

**Order within one season end** (deterministic): Apex is checked before Dominion; if both fire in the same season the Apex winner is the match's winner and the Dominion is recorded on the standings. Eliminated and Retired apply only to the human and never pre-empt a rival's victory in the same season.

### 8.1a Dominion victory: the rules, and how a rival's run is stopped (D12, owner decision 2026-11-28)
The rule is [01 §7.2](01-game-structure.md)'s, unchanged; this section only fixes how it is checked, shown and answered. All numbers are drafts.
- **The test.** At every season end, each civilization's **share** = its owned habitable weight ÷ all habitable weight (owned or not, Free Folk included, as [05](05-simulation-structure.md) defines it). A civilization **qualifies** when its share is ≥ 50% **and** ≥ 2× the share of the runner-up, the next-largest *civilization* (Free Folk are never the runner-up). The **Dominion clock** runs while it qualifies at every season end and wins at **20 consecutive season ends (5 years)**; one failed check resets it to zero.
- **Warnings to everyone.** Crossing **40%** broadcasts a public warning to every civilization, the human included ([01 §7.2](01-game-structure.md), [08](08-ui-and-world-display.md) critical alerts). When a civilization qualifies, a second alert starts a **visible countdown** of the clock on every player's screen, with both numbers that would break it: the leader's share against 50%, and twice the runner-up's share.
- **The human's five-year window.** The run breaks if, at any one season end, either clause fails. That gives the human two ways: **take land back** (war, a pact's war, secession of the leader's towns under E7 unrest) to push the leader under 50%, or **grow the runner-up** (the human or anyone else) to more than half the leader's share (against a leader on 52%, a runner-up on 26.1% breaks it). An embargo or an assembly vote does not move land by itself; it weakens the leader's economy and wars.
- **Nuclear strikes.** A strike that ruins one of the leader's towns removes that town's claim, and fallout above 50 removes tiles from habitable weight for everyone. Either can break the clock if the share falls below a clause at the next season end. A strike is still **first use** with its full price (§6.5.5) unless the leader struck first. There is no exemption for stopping a Dominion (D19).
- **Pacts and Charters.** Dominion is **individual**: pact partners' land never adds to it and it is never shared (unlike the Concord Apex). **Concord** cannot start a war of conquest without an assembly majority (E8), so it rarely runs for Dominion, but its pacts' collective defense makes conquering a member a war against all, which is the strongest block. **Ascendancy** (WAR and DEF −15%, drill ×1.2) is the natural Dominion path; its price is that the AI coalition reacts at **35%** instead of 40%. **Renown**'s soft claim never flips land (E8 D8), so culture alone can neither win nor break a Dominion.
- **The AI.** Every AI evaluates `CoalitionAgainstLeader` against any civilization over 40% (35% for Ascendancy), as [14 §7.3](14-ai-opponents.md) already specifies; once a clock is running its coalition value is at maximum, and AIs seek pacts and wars against the leader. An AI pursues Dominion through the designed "Dominion endgame" of [14](14-ai-opponents.md) (tracking the path to 50% and 2×, planning conquests); the Warlord leans to it. X-T10 is the frequency gate.
- **When the match ends this way.** The winner receives the **same bonus as an Apex winner** (D20), and an **Aggressor** keeps its mark and gets half (§6.5.5). The ranking is still Legacy. If a rival wins, the human's end screen reads **"The {civ} rules the world"** with the human's rank; the standings add each civilization's final Dominion share and the winner's five-year hold (the year it started); the replay records reason and winner. If the human wins: **"Your people rule the world"**.
- **Before the late ages exist.** The rule applies in a match from its first season in every chapter that carries it (D18), so a rival can win by Dominion before E8 or E9 are built or reached. The match then simply ends there, with the Legacy standings as today.

Earlier chapters keep their own endings unchanged (Bronze and Iron countdowns, the human keystone from Fief to Modern). In the Information chapter the old "human keystone" clause becomes the human's **Apex**, and KEY-9 does not end the match.

### 8.2 Win, loss, defeat and ranking
- **The ranking is always Legacy** (end-screen Legacy: research Legacy plus Dominion, plus the Apex bonus). "Winning the match" means finishing first on it.
- **Winning the race** (Apex or Dominion) is shown separately and counts in the ranking through its bonus.
- **Overtaken:** a human whose rival completes the Apex sees "The {civ} has completed the Planetary Network" and their own rank; they can still be first on Legacy.
- **Defeat** means being eliminated: Legacy is **frozen** at that moment (no more accrual) and the civilization stays on the standings marked as fallen. An AI that is eliminated is shown the same way.
- **A nuclear aggressor** keeps its place in the ranking by Legacy, after the first-use penalties, with an **Aggressor** mark on the standings and, if it wins the Apex, half the bonus (§6.5.5).
- **A match nobody finishes** stays open; the player can retire at any time to see the standings. AI-only matches are bounded by their horizon.

### 8.3 The end screen, the standings and the replay
- **Title by (chapter, reason, winner)**: "Your people complete the Planetary Network", "The {civ} completes the Planetary Network", "Your people rule the world" and "The {civ} rules the world" (Dominion, §8.1a), "Your people have fallen", "Your people lay down the Spark" (retired). Earlier chapters keep today's titles.
- **Standings**: People, Legacy, Dominion, Thriving, Land (as today), plus **Charter**, **uplinks**, the **Apex** column and an **Aggressor** mark for any first user of nuclear weapons.
- **Record**: the Discovery-style record (settlements, people, wars, colonies) extended with each age's lines, plus a keystone timeline for every civilization (the end-screen promise of [22 §3.1](22-bronze-age.md)).
- **Replay**: replays carry the end reason and winner (they follow from the commands, so a replay reproduces them; `Retire`, `Sabotage` and `Strike` are recorded commands, a Dominion win records its winner and the season its clock started, and the human's "Retaliate if struck" standing order is recorded when it changes). The replay viewer shows the same end screen. After the Apex, "Keep watching" continues the world with scoring stopped [A10].

## 9. How E9 pays off the E8 Charter [A5]

The Charter was chosen at GOV-17 in E8 ([42 §8.4](42-modern-age.md)). In E9 it does not open a separate game; it changes **how a civilization can run the one race** and **how others read it**.

| | Concord (blocs and law) | Ascendancy (power and deterrence) | Renown (culture and science) |
|---|---|---|---|
| E8 carried forward | DIP/TRD −15%, assembly votes ×2, no conquest without a majority | WAR/DEF/industry −15%, drill ×1.2, Relations drift −1 a year | ART/COM/LRN/INQ −15%, soft claim ×1.5, Thriving ×1.1 |
| E9 capstone | **DIP-17:** uplinks in partners' territory; partners share the Apex bonus (P: 25% each, total capped at the bonus) | **WAR-20:** sabotage ×1.5 and outage ×1.5; captured uplinks keep working for the captor | **ART-16:** own uplinks −20% labor; rival uplink work under its soft claim −25% |
| How it races | Builds a network across a bloc; wins together | Sabotages and takes; defends with deterrence | Builds cheaper and slows rivals; cannot sabotage [A7] |
| Its price in E9 | Needs willing partners; a partner who leaves takes its uplinks out of the network | The coalition forms at 2 uplinks, not 3; assembly votes go against it | Weakest in war; no sabotage; a lead provokes Relations loss |
| **Nuclear stance [A6]** (§6.5) | **Bound by no first use.** First use voids every Concord benefit for the rest of the match (E8 cuts, assembly ×2, DIP-17), dissolves its pacts and costs the Legacy penalty ×1.5. In return, a Concord pact member with DEF-14 **covers its partners**: an attack on any member is priced by AIs as risking that member's retaliation (the "umbrella") | **Declared first-use doctrine.** Rivals price its threat ×1.5 (retaliation chance raised by half in their war value); its first-use Legacy penalty is ×0.75. The world reaction (Relations, embargo, coalition) is unchanged | **First use costs double** (Legacy penalty ×2) and ends its soft claim and Thriving bonus for the rest of the match. It may build and hold warheads for retaliation like anyone else |
| How rivals read it | A partner: AIs join its pacts readily; non-members fear the bloc | A threat: coalitions sooner; Concord blocs embargo it | Harmless until it leads; then soft-claim pressure is resented |

Fairness gate: in four-AI free-for-alls each Charter's share of Apex wins stays inside the A5 band (no Charter above 30% more wins than its share).

## 10. Provisional targets (X-xx; none fitted)

| ID | Target | Measurement |
|---|---|---|
| X-T1 | Own KEY-9 → Apex about **28 minutes**; unfinished seats reported, never imputed | 80+ matches, held out |
| X-T2 | Whole match about **222 minutes** at Standard [A1] | Same sample |
| X-T3 | **Siting:** every civilization reaching KEY-9 has a route to five sites ≥ 15 tiles apart (own land, colony, pact) | `e9-audit` |
| X-T4 | **Race spread:** median gap between first and second Apex ETA ≥ 3 minutes | Live sample |
| X-T5 | **Apex and Legacy:** the Apex winner is first on Legacy in about 2 of 3 matches | Live sample |
| X-T6 | **Sabotage:** outages follow the stated odds; never destroys; no sabotage between pact members | Seeded cases |
| X-T7 | **Climate:** stress from emissions only; events by the stated curve; deaths within the 10% bound | Per-cause attribution |
| X-T8 | **Charters:** Apex win shares within the A5 band | AI tournament |
| X-T9 | **Ends:** every reason fires exactly on its rule; ties deterministic; elimination and retire recorded in replays | Replay cases |
| X-T10 | Dominion wins rare but real (5 to 20% of Warlord matches, [14](14-ai-opponents.md)) [A3]; every Dominion win, by a rival or the human, ends the match on the season its clock completes; the 40% warning and the countdown reach every player; one failed check resets the clock | AI tournament plus seeded cases |
| X-T11 | **Nuclear restraint [A6]:** in four-AI free-for-alls, first use in at most 1 match of 10; no AI strikes first at a second-strike holder unless facing elimination; every strike's effects (deaths, destroyed structures, fallout, world yield, climate pulse, penalties) follow §6.5 exactly and replay identically | AI tournament plus seeded strike fixtures |

## 11. What the player sees

Generic kit ([08 §13](08-ui-and-world-display.md)), with the final age's read ([08](08-ui-and-world-display.md) era table): solar fields and wind farms, megacity sprawl, rewilding near clean civilizations; data centers, launch sites and **Apex uplinks**; glowing **data-link arcs** between cities, satellites crossing the sky layer, container lanes; haze thick or clearing by path, and a **climate tint** on the world view. **The race panel** (always visible from KEY-9): each known civilization's Charter, uplinks working / dark / building, and its believed ETA. Cards: backbone and power by town, climate gauge and the next likely event, supply-chain state, sabotage reports, uplink sites with the reason one is dark, and a **nuclear card** (who has tested, who holds second strike, your known or estimated threat, your retaliation order, treaty caps). A launch is a full-screen critical alert with the target and the one decision left (answer or not, if you can); fallout shows as a dull ochre stain that fades over years. Moments: KEY-9, the Charter capstone, each own uplink, the 3-uplink warning, and the Apex (a Planetary Network Moment, the only bespoke Moment worth making for E9). The Chronicle becomes a feed. **Art P0 silhouettes (later art doc):** uplink site, launch site, data center, data-link arc, satellite, solar field, wind farm, megacity tower cluster, the climate tint, the race panel and the Apex Moment.

## 12. Work breakdown and order

Own worktree (`civsim-e9`, `codex/e9-information`), after E8's mode plumbing exists. The endgame items (WP-E9-11) may start earlier on their own branch because they touch only end rules and the shell.

| WP | Deliverable | Exit gate | Size | Parallel with |
|---|---|---|---|---|
| **WP-E9-0 Scope** | This doc; A1, A2, A3, A7 confirmed and A6 decided (usable) on 2026-11-28; D12 decided (a rival's Dominion ends the match); owner confirms or flips A4, A5, A8, A9, A10, the nuclear decisions D13 to D17 and the Dominion details D18 to D21 | Recorded | S | — |
| WP-E9-1 Oracles | Research, climate and race oracles, `e9_anchors.py` from E8's live fit | `--check`; base for 25/28 min | M | 2, 3 |
| WP-E9-2 Content | 53 + KEY-10 typed (draft), the three capstones and the `[Charter]` gate kind, uplink structure, goods, roles, V26, base and breadth 19 | Strict validation clean | L | 1, 3 |
| WP-E9-3 Audit | `e9-audit`: five-site feasibility at spacing 15 per start and per bloc, launch-site and wind/solar sites, rare-earth and lithium reach | Report | M | 1, 2 |
| **WP-E9-4 Mode plumbing** | `Information = 6`, ceiling 9, the Apex end, `InformationChain`, isolation | Older goldens byte-identical | S | first |
| WP-E9-5 Networks | System A | Power draw and staffed-only gains | L | after 4 |
| WP-E9-6 Space and sensing | System B | Surface-only sight (fog fuzz) | M | 5 |
| WP-E9-7 Climate | System C on E8's stress | X-T7 | L | 5 |
| WP-E9-8 Market, biotech, culture | Systems D, E, F | Disruption and volatility bounded | L | 6, 7 |
| WP-E9-9 The Apex | Uplinks, KEY-10 completion rule, public counts, warnings | X-T3, X-T6 on fixtures | L | after 5, 6 |
| WP-E9-10 Charter payoff | Capstones and their gate; Concord shared bonus | X-T8 on fixtures | M | 9 |
| WP-E9-11 Endgame rules | End reasons 4 to 7, winner field, `Retire` and `Sabotage` commands, elimination (including by strikes), Dominion victory [A3] for any civilization (D12) with the 40% warning, the visible clock and the Ascendancy 35% coalition trigger, Aggressor mark and the halved Apex bonus, end screen by reason, replay end | X-T9; all older endings unchanged | M | can start after Modern plumbing |
| WP-E9-11a Nuclear endgame | On top of E8's arsenal (doc 42 WP-E8-11): strikes on uplinks, the Charter nuclear stances (Concord void and umbrella, Ascendancy doctrine, Renown double cost), first-use rules at the Apex (AI rule (c) of §6.5.4), DIP-14's automatic embargo vote | X-T11 on fixtures; seeded strike replays identical | M | 10, 11 |
| WP-E9-12 AI | `ApexRace` scoring, coalition triggers (including first-user targeting), sabotage and siting policy, retaliation-aware war value, Charter-aware behavior | Stock AI completes the Apex unaided; A1-A8; X-T11 | L | after 9 to 11a |
| WP-E9-13 View, promotion and pacing | `InformationFrame`, race panel, Apex Moment; promote all 54 rows together; `information-match --grant-e9`; base sweep | X-T1 on fit seeds | L | — |
| WP-E9-14 Whole-arc sample and playtest | 80+ **whole-arc** matches (E0 to the Apex), held out; owner hand playtest of the full game | All gates; owner approval | L | — |

**Order:** WP-E9-4, then 2 and 3, then 1; A, then B and the Apex (WP-E9-9) as early as possible, because the race is the age's point; C and the rest in parallel; endgame rules (WP-E9-11) can be built and tested on Modern matches with grants before E9 exists.

## 13. Acceptance, decisions and knock-ons

### 13.1 Acceptance series B9-xx (all proposed)

| Gate | Condition |
|---|---|
| B9-01 Isolation | Every older golden byte-identical; E9 inert before own KEY-9 and in older modes |
| B9-02 Completeness | 54 nodes typed; no gated input on the Apex path without a guarantee |
| B9-03 Determinism | 1,000 Information matches replay exactly **including every end reason**; snapshots exact; macOS arm64 and Windows x64 agree on an information-v1 golden |
| B9-04 Ends | X-T9; ties; human and rival Apex; Dominion; elimination; retire; open match |
| B9-05 Pacing and length | X-T1, X-T2 |
| B9-06 The race | X-T3 to X-T6 |
| B9-07 Climate | X-T7 |
| B9-08 Charters and AI | X-T8, X-T10; A1-A8 in Information; Legend ≥ 60% vs Warlord |
| B9-08a Nuclear | X-T11; a strike conserves people (deaths recorded by cause) and structures; fallout decays exactly; a human with "Retaliate if struck" on always answers; no strike without war |
| B9-09 Performance | Tick ≤ 4 ms at E9 p95 population; figure budget at 1:500 |
| B9-10 Comprehension | ≥ 5/8 uncoached players explain how the Apex is won and one way to stop a rival |
| B9-11 Integration | Full suite and strict validation green; owner approves the whole arc |

### 13.2 Decisions

| ID | Question | Recommendation (plain English) | Needs the owner? |
|---|---|---|---|
| D1 | A4, A5, A8, A9, A10 (A1, A2, A3, A7 confirmed and A6 decided on 2026-11-28) | Accept the five remaining as written; each is doc 42's recommendation | **Yes** |
| D2 | E9 length | **28 minutes** from KEY-9 to the Apex (25 of research, the build race overlapping the end) | **Yes** |
| D3 | Projects for KEY-10 | **19 instead of 17**: two more free choices for Apex preparation and the climate answers | **Yes** |
| D4 | Add the three Charter capstones and the `[Charter]` gate | **Yes**: the Charter then shows up in the last age where it matters, without a separate minigame | **Yes** |
| D5 | KEY-10 completion | Research paid **and** five working uplinks in the same season | **Yes** |
| D6 | Apex bonus size | About 10% of a typical final Legacy, fitted so the race winner tops Legacy in about 2 of 3 matches | Please confirm |
| D7 | New Standard length | Accept about 3 h 40 min; update doc 01 §6 when the arc is built; build Quick pace later | **Yes** |
| D8 | Uplink inputs | Only made goods from guaranteed or ubiquitous sources; never rare earths or bauxite | No |
| D9 | Late doubling (D9) | Keep it (35 of 50) and fit the base with it | Please confirm |
| D10 | Era base | Type **1,100,000** as the first input (not 9,000,000); expect 880,000 to 1,420,000 | No |
| D11 | Representation | Simulation 1:1; 1:500 only in the view if needed | No |
| D12 | Does a rival's Dominion end the match? | **Decided by the owner 2026-11-28: yes.** Either victory by any civilization (Apex or Dominion) ends the match for everyone (§8.1a) | Settled |
| D13 | How strong is a strike? | **60% of one town's people, everything within 1 tile destroyed, fallout to 2 tiles fading 5% a year.** Strong enough to be feared, small enough that one strike never ends a realm | **Yes** |
| D14 | DEF-14's "rivals cannot declare war of conquest on you" | **Drop the hard ban; DEF-14 becomes second strike** (your warheads survive any strike). With a usable bomb the ban would make the bomb pointless; deterrence now works through the attacker's fear of retaliation | **Yes** |
| D15 | Price of first use | **−15% Legacy (Concord ×1.5, Renown ×2, Ascendancy ×0.75), Aggressor mark, half the Apex bonus, Relations −60 with everyone, automatic embargo, and every AI coalition turns on you**; retaliation pays none of this | **Yes** |
| D16 | The human's retaliation | A standing order **"Retaliate if struck", on by default**, so the human's deterrent is as believable as an AI's; the human may switch it off | Please confirm |
| D18 | Where the Dominion victory applies | **From the Industry chapter on, from the match's first season** (older chapters stay byte-identical). A conquest win is a conquest win whatever the era | **Yes** |
| D19 | Does a nuclear strike that stops a rival's Dominion still count as first use? | **Yes, with the full price.** An exemption would make the bomb the standard answer to every Dominion run | Please confirm |
| D20 | Dominion bonus | **The same as the Apex bonus** (about 10% of a typical final Legacy), halved for an Aggressor. Neither finish should be the obvious one | Please confirm |
| D21 | The Dominion clock | Checked at **every season end**; 20 in a row win; one failure resets. A season check gives the human the most chances to break it | Please confirm |
| D17 | Missile defense | **None**: the catalog has no such row, so rockets cannot be stopped and only bombers can be shot down. Deterrence rests on retaliation | Please confirm |

### 13.3 Knock-on changes to docs 41 and 42 (noted here; neither is edited)

1. **Doc 42 §7.3 timeline:** KEY-8 is nearer minute 162 than 150, and the end of E9 nearer 222 than 210; doc 42 started E6 at minute 90, a figure from E5-granted runs.
2. **Doc 42 knock-on 4 is done:** the end-screen titles follow the chapter since `788d396`. What remains is the reason-and-winner form of §8.3, which E9 needs.
3. **Doc 42 §3.4 `MatchEndReason`:** E9 appends `Apex` 4, `Dominion` 5, `Eliminated` 6, `Retired` 7 and a winner field. Doc 42's example names (`RivalApex`) are replaced by a single `Apex` reason plus the winner.
4. **Doc 41 (E7) must build the Dominion victory.** The owner confirmed it (A3, 2026-11-28), and E7 is the first age where it can be reached, so **the E7 build is not complete without it**: the 50%-and-2× test held for 5 years, the 40% public warning, the `Dominion` end reason **for any winner, a rival included** (D12: a rival's Dominion ends the match for everyone), the 40% warning and the running clock **broadcast to the human**, and a proper end screen for a human defeated by a rival's Dominion ("The {civ} rules the world", rank, final shares). It can share WP-E9-11's end-rule code and be switched on per chapter. Doc 41 currently says only that Dominion endings are "reported separately"; this replaces that.
5. **Doc 41 WP-E7-3 and doc 42 WP-E8-3 audits** should also count **five-site room at spacing 15** per civilization, because claim radius grows from 7 (E7) to 9 (E8, E9) and the Apex depends on it.
6. **Doc 42 GOV-17** must record the Charter in the snapshot, the hash and the public Relations view, because E9's `[Charter]` gate and the AI read it.
7. **Doc 42 WAR-19** must make the uplink count and KEY-10 progress of a rival readable through its service only (and the public count of §6.2 through sight), so E9's sabotage and coalition rules use no hidden state.
8. **Doc 42 climate stress** must decay slowly (P: 1% a year) so E7 and E8 emissions are still felt in E9; doc 42 gives no decay rate.
9. **Doc 41 unrest:** E9's automation and social-media volatility feed E7's unrest reading; it must stay a general per-town reading, not a factory-only one.
10. **Doc 42 nuclear rows (updated in doc 42 on 2026-11-28):** E8 builds the arsenal of §6.5 (enrichment works, warheads, bombers and rockets, strikes, fallout, DEF-14 as second strike, DIP-13 treaties and accidents, the retaliation order, the first-use price); E9 adds only the uplink, Charter and end-rule interactions.
11. **Doc 41 pollution and doc 42 climate stress** must accept the strike pulse of §6.5.3 as one more input; fallout itself is a separate per-tile array, not pollution.

## 14. Whole-arc checklist (what the base game has, and what is left)

| Age | Base game today | Plan | Left to do |
|---|---|---|---|
| E0 Dawn | **Built**, 34 + KEY-1 numeric | [17](17-vertical-slice.md) | Balance only |
| E1 Hunters | **Built**, 59 + KEY-2 numeric | [17](17-vertical-slice.md) | Balance only |
| E2 Roots | **Built**, 52 + KEY-3 numeric | [17](17-vertical-slice.md) | Balance only |
| E3 Bronze | **Built**, 65 + KEY-4 numeric | [22](22-bronze-age.md), [23](23-e3-bronze-specs.md) | Balance only |
| E4 Iron | **Built**, 56 + KEY-5 numeric | [25](25-iron-age.md), [26](26-e4-iron-specs.md) | Balance only |
| E5 Faith & Fief | **Built**, 49 + KEY-6 numeric (some gates passed by owner exception) | [27](27-faith-and-fief-age.md) | Open acceptance gates, the snapshot-restore defect |
| E6 Discovery | **Built**, 41 + KEY-7 numeric, **the base game's default**, hand playtest approved | [38](38-discovery-age.md), [39](39-discovery-art-pass.md) | A-5 rerun, figure budget, 80-match sample |
| E7 Industry | Nothing built; 43 + KEY-8 draft | [41](41-industry-age.md) (approved) | Everything: 12 new rows, pollution, rail, unrest; Dominion victory [A3] |
| E8 Modern | Nothing built; 60 + KEY-9 draft | [42](42-modern-age.md) (nuclear decision taken; some questions open) | Everything: 5 new rows, Charter, blocs, climate stress, the usable nuclear arsenal |
| E9 Information | Nothing built; 50 + KEY-10 draft | **this doc** | Everything: 3 capstones, networks, climate answers, the Apex |
| Apex and endgame | Not built: no Apex, no Dominion victory, no retire or elimination; legacy ranking and chapter end screen exist | [01 §7](01-game-structure.md), [42 §8](42-modern-age.md), this doc | End reasons 4 to 7, race AI, end screen by reason |
| Across the arc | One pace (Standard); single player; 4 civilizations | [01 §6, §14](01-game-structure.md) | Quick and Epic paces; art docs for E7 to E9; multiplayer (post-MVP) |

## 15. Risks

- **Three planned ages stand between today and this one.** Every number here leans on docs 41 and 42; re-anchor after each is built.
- **The race can be decided too early or too late.** If the leader is always minutes ahead, sabotage and coalitions never matter; if uplinks take too long, the age drags. X-T4 is the gate; the uplink cost is the lever.
- **Small realms may not fit five uplinks 15 tiles apart.** Audit early (knock-on 5); pacts and colonies are the escape routes, not a rule change.
- **A rival's Dominion can end a match early**, before E8 or E9 exist or are reached (D12, D18). The answer is that the match simply ends there with the Legacy standings as today; the guards are the 40% warning, the visible five-year clock with the numbers that would break it, the AI coalition, and the fact that peaceful expansion tops out near 25 to 30% ([01 §7.2](01-game-structure.md)), so only a real conquest gets there.
- **A rival ending the game is a new feeling** after five chapters where only the human could end it [A2]. The 3- and 4-uplink warnings and the race panel must make it never come as a surprise.
- **Sabotage can feel unfair.** Odds are public, outages temporary, nothing is destroyed, and pact partners are safe.
- **A usable bomb can end a match abruptly and feel unfair [A6].** Answers: warning at every step (the public test when a rival gets the bomb, a hostile-and-able alert, the launch itself, answerable in the same season); counterplay (spread towns, radar against bombers, DEF-14 and the retaliation order, arms-control caps, a Concord umbrella, assembly pressure); a strike hits one town, so it never eliminates a realm at a stroke; and a first user pays so much that an AI does it only when it would otherwise lose (§6.5.4, X-T11). The AI avoids pointless suicide by pricing retaliation into every war and strike and by never striking a second-strike holder first unless facing elimination. If playtests still find it unfair, the lever is the strike's size (D13), not a return to a hollow deterrent.
- **Nuclear rules touch many systems** (war, Relations, Legacy, climate, fallout, the Apex). Keep them to the seven rules of §6.5 and test each with seeded fixtures before the AI is allowed to use them.
- **Climate stress shared by all** can punish a clean civilization for a dirty rival; keep it bounded and visible from E8.
- **The whole match is long** (about 3 h 40 min). Saves and resume must be solid; the existing snapshot-restore defect ([38](38-discovery-age.md)) matters more the longer the game runs.
- **Performance at E9 populations** (the balance model peaks near 145,000 a civilization) on a 1:1 simulation; measure early, and keep 1:500 for the view ready.
