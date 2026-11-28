# 42 — Modern (E8): Grid, Engine, Broadcast and the Choices That End the Game

> **Nuclear decision changed by the owner, 2026-11-28.** Nuclear weapons are **usable**, not deterrent only ("otherwise using them as a deterrent is hollow"). The atomic rows are E8 rows, so **E8 builds the whole arsenal** (§5.8a); E9 ([43 §6.5](43-information-age.md), which holds the full rules and numbers) adds only the uplink, Charter and end-rule interactions. D10, §5.8, §5.9, §8.6 Q6, M-T11, WP-E8-11 and the risks below are updated to match. The owner also confirmed doc 42's recommendations for whole-match length, a rival ending your game (by the Apex, and, decided later the same day as [43](43-information-age.md) D12, by Dominion too), building the Dominion victory and allowing sabotage ([43](43-information-age.md), table A1 to A10).
>
> **Planning draft, 2026-11-28.** The E8 counterpart of [41 — Industry](41-industry-age.md) (approved by the owner, commit `0f10a27`) and [38 — Discovery](38-discovery-age.md): which age comes after Industry, an audit of what exists, the mechanisms and their prices, pacing, gates, work packages, decisions and risks. Because the owner wants the endgame to "sort of begin with decisions made in E8", §8 frames **the endgame as a whole** (E8, E9 and the end of the match) and offers three shapes for it. E9 itself is not designed here. **E8 never changes an E7 rule**; every E8 number is a **draft input to an oracle**, not a fitted value.
>
> Audit baseline: worktree `civsim-e6`, branch `codex/e6-discovery` at `0f10a27`. *(verified)* means read from the catalog, registries or source in that tree; *(assumed)* means taken from docs or from doc 41's plan, which is not built yet. Doc 41's approved decisions are treated as settled: E7 Industry, KEY-7 to KEY-8, 55 projects plus KEY-8, breadth 20, about 32 minutes, draft era base 110,000. Sources: [01 §7, §8](01-game-structure.md), [02 §8.3, §13](02-research-structure.md), [03 §4.1](03-research-catalog.md), [04 §2, §8](04-research-paths.md), [05 §13-14](05-simulation-structure.md), [09](09-population-and-health.md), [13](13-contact-and-conflict.md), [14 §3.3, §7](14-ai-opponents.md), [17](17-vertical-slice.md), [22 §3](22-bronze-age.md), [28 §17](28-production-gap.md), [41](41-industry-age.md).

## 0. The short version (what to accept)

- **E8 is Modern**: the electric, motor and atomic age (about 1880 to 1970), entered by KEY-8 *Electrification* and left by **KEY-9 *Digital Revolution***. New chapter `ChapterMode.Modern = 5`, containing Industry. The human's KEY-9 ends it; no rival countdown.
- **The catalog plans 60 projects plus KEY-9.** That is already the largest age, so E8 does **not** need padding for length. This draft adds **5 projects**, every one an endgame lever: a **World Charter** (the civilization's one-time commitment, on the KEY-9 path), **collective security pacts** (blocs), **intelligence services** (the root of E9 sabotage), **emission controls** (the first answer to shared climate stress) and an **Orbital Launch** Wonder (the space race's opening). That makes 65, and breadth rises from **18 to 22**.
- **Length: 32 minutes**, the same as E7. Insight growth slows in E8 because births fall (the era row halves fertility), so a 32-minute age needs a bill about **5 times E7's**. With breadth 22 that is an **era base near 400,000** (registry: 1,700,000, about 4 times too high).
- **The endgame, recommended shape:** *the Apex race, steered by a Charter.* In E8 each civilization commits to one of three Charters (Concord, Ascendancy, Renown), which changes how it can run the final race and how rivals treat it. E9 ends with KEY-10 *Planetary Network*: the first civilization to finish its uplinks **ends the match for everyone**, and the final ranking is by Legacy. This is the one place where a rival's keystone ends the match again, so the human can lose the race. Dominion stays the only other early finish, and it has to be built first.

## 1. Which age is next, and why

| Check | Finding |
|---|---|
| Newest designed age | E7 Industry ([41](41-industry-age.md)), approved, not built. E6 is the newest built age; the base game plays E0 to E6 (Discovery default) *(verified, `c1fbbd1`)*. |
| What enters and leaves E8 | KEY-8 *Electrification* (`era 7, enters 8`) enters it; **KEY-9 *Digital Revolution*** (`era 8, enters 9`, `requires: [INQ-18, MEC-13, LRN-11, TRD-14]`, `breadth: {era: 8, min: 18}`, draft) leaves it *(verified)*. [01 §8](01-game-structure.md): E8 is **Modern**, "electric, motor and atomic age (~1880-1970)", 1 figure = 250 people. |
| E8 catalog state | **60 regular projects plus KEY-9 = 61 draft rows**, prose effects only *(verified by script)*; matches [03 §4.1](03-research-catalog.md) (Craft 20, Kin 15, Reach 25). Types 27 M, 15 D, 11 I, 7 S, no Wonder. One gate: MIN-13 oil drilling `[Seen: Oil seep]` *(verified)*. |
| Era row | `eras.yaml` E8: base **1,700,000**, breadth 18, representation 1:250, claim radius **9**, reach **80**, claim multiplier 1.56, **fertility multiplier 0.55** (E7: 1.0), mortality 0.0045 / 0.003 / 0.033, `cost_mod 0.9` (not read by the sim, see [41 §1](41-industry-age.md)) *(verified)*. |
| After E8 | E9 Information: 50 regular plus KEY-10 *Planetary Network*, "must be built as 5 uplink sites ... (each ≥ 15 tiles apart). The first to finish wins the Apex victory" *(verified)*. |

Working names: **ModernChapter**, milestone **M15**.

## 2. What Modern should feel like

Industry set the machine against the people who work it. **Modern sets the nations against each other and against the world they share.** Power and engines reach everywhere, so the limits move outward: shared air, a world of rival blocs, weapons too terrible to use, and a race whose end is already in sight.

| Tension | What pushes | What answers it (and what it costs) |
|---|---|---|
| **Grid and engine** | The power station and grid reach every connected town; oil and the engine put power on wheels and wings. | Oil is a gated deposit; grids are lines that can be cut; emissions start counting against everyone (climate stress). |
| **Plenty and the transition** | Assembly lines, fertilizer, refrigeration, antibiotics: famine nearly ends and lives lengthen. | Births fall (fertility ×0.55): growth slows, cities grow up not out, and Insight must come from schools and labs, not headcount. |
| **Voice and image** | Telephone, radio, cinema and television carry culture into rival lands (soft claim). | Soft power works both ways; it moves Cohesion and Legacy, never land by itself. |
| **Blocs and the bomb** | International organizations, pacts, central banks and trade orders bind civilizations together; the bomb makes a war against a nuclear power a gamble with a city. | Pacts drag partners into wars; a usable bomb invites a world coalition, an embargo and a Legacy penalty against whoever strikes first; arms control costs readiness. |
| **The race begins** | Rocketry, the transistor and the computer point straight at the Apex. | Every E8 free pick is also Apex preparation (§3.2): a civilization that skips oil, rockets or aviation pays for it in E9. |

## 3. Scope audit

### 3.1 Inventory *(verified from `content/catalog`)* — 60 planned + 5 proposed (\*)

| System | Projects |
|---|---|
| **A. Grid and power** (9) | NRG-09 electric grid · NRG-11 hydroelectric dams · NRG-12 nuclear fission power · CER-13 insulators · FIR-14 electric arc furnace · MET-16 aluminium · MET-17 alloy steels · STN-14 synthetic abrasives · **NRG-17\* emission controls** |
| **B. Oil and the motor** (10) | MIN-13 oil drilling (gated) · NRG-10 internal combustion · TRN-13 automobiles and trucks · TRN-14 highways · TRN-15 aviation · MIN-14 open pit · WOD-13 chainsaw · CHM-13 petrochemicals and plastics · WOD-12 plywood · FIB-14 synthetic fibers |
| **C. Plenty and the city** (11) | MEC-12 assembly line · BLD-15 skyscrapers · TER-13 suburbs · FOD-12 refrigeration · FOD-13 food processing · HUS-13 feedlots · AGR-14 tractors · CHM-12 fertilizer synthesis · AGR-15 Green Revolution · HNT-14 aquaculture · SEA-15 steel hulls |
| **D. Electronics and computing** (7) | INQ-15 relativity and quantum physics · CER-14 silicon · INQ-18 transistor · NUM-13 computation · MEC-13 electronic computer · LRN-11 mass education · LRN-12 R&D laboratories |
| **E. Health and the transition** (5) | MED-13 antibiotics · MED-14 mass vaccination · SOC-14 welfare state · INQ-14 genetics · GOV-14 universal suffrage |
| **F. Voice and image** (5) | COM-13 telephone · COM-14 radio · COM-15 television · ART-12 photography and cinema · ART-13 broadcast media |
| **G. World order** (8) | DIP-12 international organizations · DIP-13 arms control · **DIP-16\* collective security pacts** · TRD-13 central banking · TRD-14 global trade order · TER-14 self-determination · BEL-14 human rights · **GOV-17\* World Charter** |
| **H. War, deterrence and the race** (10) | WAR-15 mechanized warfare · WAR-16 nuclear weapons · DEF-12 trenches and wire · DEF-13 radar · DEF-14 nuclear deterrence · WAY-12 aerial survey · INQ-16 nuclear physics · INQ-17 rocketry · **WAY-15\* Orbital Launch (Wonder)** · **WAR-19\* intelligence services** |

With the five the age is Craft 21, Kin 16, Reach 28; 28 M, 15 D, 14 I, 7 S, 1 W.

### 3.2 KEY-9 closure and what the Apex already asks of E8 *(verified by script)*

1. **KEY-9's in-era closure is 14 projects** (CER-13, CER-14, COM-13, COM-14, DIP-12, INQ-15, INQ-18, LRN-11, MEC-12, MEC-13, NRG-09, NUM-13, TRD-13, TRD-14), matching [04 §2](04-research-paths.md) (14 critical, breadth 18, 4 free). Making GOV-17 a KEY-9 prerequisite (D5) adds GOV-14 and GOV-17: **16 critical**.
2. **KEY-9 needs the whole diplomacy line.** TRD-14 needs DIP-12, which needs DIP-11 and so DIP-01 to DIP-11 back to E0, plus BEL-11 to BEL-13, GOV-12/13, LRN-10, MEC-11, NUM-12 and TRD-11/12: **21 older rows, about 1.0 million Insight of backfill** at the bases of E5 6,400, E6 18,000 and E7 110,000 with EraLag 0.7. A civilization that never practised diplomacy pays for it here. That is the endgame's first lesson: the world order is not optional at the end.
3. **The Apex reaches back into E8.** KEY-10's closure needs **14 E8 rows that KEY-9 does not**: ART-12, CHM-12, CHM-13, COM-15, INQ-14, INQ-17, MET-16, MET-17, MIN-13, MIN-14, NRG-10, TRN-13, TRN-15, WAY-12. So an E8 civilization's free picks are, in practice, **Apex preparation**: oil, rockets, aviation, alloys, television. This is the arithmetic behind "the endgame begins in E8".
4. **Oil is a gate on the Apex path, not on KEY-9's.** MIN-13 carries `[Seen: Oil seep]`; `guarantees.yaml` has a **continental** oil-seep guarantee *(verified)*. E6 showed that a continental guarantee is not a per-start one: coal was out of sight for 28.7% of starts until D13 fixed it ([38 §3.2](38-discovery-age.md)). An oil audit is required before E9 can rely on it (WP-E8-3).
5. **Bauxite.** MET-16 says "needs Bauxite" in prose but has no gate, and bauxite has no guarantee *(verified)*. Typing it must not create a hidden soft-lock on the Apex path (MET-16 is in KEY-10's closure): either aluminium is made without a gate or bauxite gets a guarantee (D13).
6. **The late-project rule (D9)** marks **36 of 60** planned rows (41 of 65 with the five), seven of them on the KEY-9 path. E8's chains are long. D9 stays the rule (as in doc 41), but here it is the biggest single cost lever.
7. **E7 rows E8 builds on** (all draft): MET-15, NRG-08, MET-14, BLD-14, MED-12, SOC-13, GOV-13, LRN-10, DIP-11, COM-12, TRN-12, SEA-14, WAY-11, TER-12, FOD-11, AGR-13, MEC-11, NUM-12, CHM-11, MIN-12, INQ-11/12/13, FOR-11, CER-12, ART-11, BEL-13, WAR-14, DEF-11, HNT-13, HUS-12, TRD-12, FIB-13, FIR-13, WOD-11. None of doc 41's twelve new rows is an E8 prerequisite; TER-16, SEA-17 and LRN-15 are natural parents (knock-on note §13).

### 3.3 The five proposed nodes

Ids take the next free numbers after doc 41's (GOV-16 is taken there). All era 8; every row has a cross-theme prerequisite.

| Id | Name | Type | Requires | What it does (P) | Why it belongs here |
|---|---|---|---|---|---|
| GOV-17 | World Charter | I | GOV-14, DIP-12 | On completion the civilization commits, once and for good, to one of three Charters (§8.4): Concord, Ascendancy or Renown. Hands-off players get the Council's pick by Leaning; AIs pick by archetype and forecast | The owner's "decisions made in E8". [01 §14](01-game-structure.md) already lists "Era Charters" as a later lever |
| DIP-16 | Collective Security Pacts | I | DIP-12, GOV-14 | A standing bloc of two or more civilizations: mutual defense (an attack on one is war with all), shared embargo, no sabotage between members; leaving costs Relations and reputation | Blocs are how the endgame becomes political rather than a lone race; DIP-03 alliances are pairwise only |
| WAR-19 | Intelligence Services | I | COM-14, GOV-14 | Staffed service: reveals a rival's next-keystone progress and uplink count; counter-intelligence halves a rival's sabotage odds against you; enables sabotage orders in E9 | [01 §7.3](01-game-structure.md) promises that "rivals can sabotage" the Apex; E9's WAR-17 and DEF-15 assume a capability that no row creates |
| NRG-17 | Emission Controls | M | NRG-09, CHM-13, MED-13 | Engines and power stations emit −30% at a cost (their output −10%); the only E8 brake on climate stress | [05 §14](05-simulation-structure.md) starts climate stress in E8, but every mitigation row (CHM-14, BLD-16, NRG-13 to 16, DIP-15) is E9 |
| WAY-15 | Orbital Launch | W | INQ-17, MET-17 | A Wonder built at a launch site: Legacy as a Wonder, one-time map reveal of the contacted world, and a 20% cost cut on WAY-13 satellites for its builder | E8 has no Wonder; the space race is the most legible endgame signal, and it points at a KEY-10 prerequisite |

**Considered and not proposed:** a separate ideology lineage (the Charter covers it without real-world ideology labels), espionage as a full system in E8 (only the service; sabotage itself is E9), a separate nuclear "use" project (WAR-16 itself carries use, D10), moving E9 rows down into E8 (E9's KEY-10 prerequisites must stay in E9).

### 3.4 Code ceilings *(verified unless noted)*

| Boundary | State | Consequence for E8 |
|---|---|---|
| Chapter modes | `ChapterMode` 0-3 built; Industry = 4 planned | Append **`Modern = 5`**. `IsIndustry` must be a **containment** check from the start (knock-on 1). |
| Economy flag | `DiscoveryChain` turns on `FiefChain`; `IndustryChain` planned | Add `ModernChain` containing `IndustryChain`. |
| Match end | `MatchEndReason` None 0, HumanKeystone 1, RivalCountdown 2, YearCap 3 (obsolete, never produced); `MatchEnd.Open` when nothing fired *(verified, `MatchEnd.cs`)* | E8 needs nothing new; the endgame (§8) will append reasons (for example `RivalApex`, `Dominion`, `Retired`); never renumber. |
| Legacy | `ResearchSystem.Legacy = Mastery + Thriving + Wonders`; the frame adds Dominion (`CivLegacy = research.Legacy + Territory.Dominion`) *(verified)* | Scoring changes in §8 go through these two terms, never a third hidden score. |
| AI plans | `GrandPlan` has `CoalitionAgainstLeader` (scored) and `ApexRace` ("reserved but not yet scored") *(verified, `Strategist.cs:310`)* | The Apex race needs only to be scored, not invented. |
| Dominion victory | Designed ([01 §7.2](01-game-structure.md), "decision locked"; [05](05-simulation-structure.md); [14](14-ai-opponents.md) D13). **No code ends a match on Dominion** *(verified by grep)* | An endgame decision, not an E8 one (§8.2). |
| End screen | Titles are hard-coded for Bronze and Iron: in any chapter where `IsIron` is true (Iron, Fief, Discovery) a human keystone ending reads "The Feudal Order is raised" *(verified, `Game.Modals.cs` `ShowEndScreen`; not run)* | A live text gap in today's Discovery game; fix before E7 (knock-on 4). |
| Pollution | E6 soot only; E7 pollution per tile planned ([41 §5.3](41-industry-age.md)) | E8's climate stress needs a cumulative emissions ledger from E7 (knock-on 2). |

## 4. What E7 leaves for E8 to build on

| E8 project | Builds on (E7, planned) | What to watch |
|---|---|---|
| NRG-09, CER-13, NRG-11, NRG-12, FIR-14 | Watt engine, generator (local electricity), the one power ledger | The grid is a **link structure** like the railway: a power station's units reach connected towns minus line loss; never a second ledger. |
| MIN-13, NRG-10, TRN-13/14/15, MIN-14 | Railways, steam, coal camps, INQ-13's oil and coal reveal | Oil is a deposit good with a gate; motors burn it. Trucks and highways supplement rail haul, they do not stack multiplicatively with it. |
| MEC-12, BLD-15, TER-13, FOD-12/13, AGR-14/15, CHM-12 | Machine tools, concrete, sewers, canning, seed drill, the E7 cap budget | [09 §5](09-population-and-health.md) E8 cap steps must be checked (V15); BLD-15 "density ×3" is a housing-cap step, not new people. |
| MED-13/14, SOC-14, GOV-14 | Germ theory, sewers, waterworks, labor movement, democracy | E8 fertility ×0.55 is the demographic transition; MED-14 "growth spike, then transition" must come from the hazards and the era row, not a scripted spike. |
| COM-13/14/15, ART-12/13 | Telegraph, newspapers (E6), museums | Soft claim is new: a culture pressure on rival tiles (§5.6). |
| DIP-12/13/16, TRD-13/14, TER-14 | DIP-10 balance of power, DIP-11 law, TRD-12 boom and bust, colonies and TER-16 | TRD-13 "bust events −60%" damps E7's cycle; TER-14 turns seceded colonies into allies, so E7 must remember who they were. |
| WAR-15/16, DEF-12/13/14, WAY-12, INQ-16/17 | Rifles, ironclads, coastal batteries, rail mobilization | Fixed-ratio rows on the force tables, fitted by an E8 combat oracle. |

## 5. New mechanisms (proposed bounded interpretations)

Deterministic and headless-testable throughout: `Fix64`, seeded `Pcg32.ForStream` rolls, id-indexed arrays, state hashed and snapshotted only in a Modern match.

### 5.1 Grid and power (A)
A **power station** (coal, oil, hydro or reactor) feeds a grid; connected towns draw from it with line loss (P: 10% a link, −30% with CER-13). Grid power counts as "work +20%, Insight +10%" in a connected town only while the station has fuel and crew. NRG-11 dams need `[Near: River]`, flood their reservoir tiles (people move, fields are lost) and emit nothing. NRG-12 reactors emit almost nothing, but carry a seeded **meltdown** roll (P: 0.2% a reactor-year, lowered by INQ-16 and GOV-14) that poisons tiles for years. FIR-14 makes steel from scrap; MET-16/17 make aluminium and alloys for aviation and rockets. **NRG-17** cuts emissions at an output cost.

### 5.2 Oil and the motor (B)
Oil wells on seen oil fields (`[Seen: Oil seep]`) produce crude, refined at a refinery into fuel. Motors (tractors, trucks, aircraft, tanks) burn fuel; with none they stand idle. Trucks give door-to-door haul off the rail line; highways double motor speed; aviation ignores terrain for travel and enables air power. Open-pit mining triples mine output, scars tiles and pollutes. Plastics (CHM-13) cut consumer-goods labor −40% and pollute.

### 5.3 Plenty and the city (C)
The assembly line triples factory output and raises Cohesion through goods actually delivered; skyscrapers and suburbs raise the housing cap and claim radius (TER-13 +3), which matters for Dominion. Fertilizer (×1.8), the Green Revolution (×2 where fertilizer is spread), refrigeration (spoilage −90%) and food processing make famine rare where they reach. Every food step stays inside [09 §5](09-population-and-health.md)'s E8 budget.

### 5.4 Electronics and computing (D)
The KEY-9 core. R&D laboratories add the **researcher** role (Insight 15, `OutResearcher` already in `balance.yaml`) *(verified)*; mass education raises Commons Insight ×1.5 and fills specialist roles; the computer raises researcher output ×1.5 and lets planners optimize logistics. All are staffed buildings; none is a free multiplier. Silicon is a made good from sand and power.

### 5.5 Health and the transition (E)
Antibiotics cut infection deaths −80%; mass vaccination brings child mortality to modern levels; the welfare state cuts famine and old-age deaths. Together with the era row's fertility ×0.55 they produce the **demographic transition**: fewer births, longer lives, older towns. That slows Insight growth (§7) and is the age's quiet price: a civilization can no longer out-breed its rivals.

### 5.6 Voice and image: soft claim (F)
ART-12 cinema and ART-13 broadcasting project a **soft claim**: culture pressure on rival border tiles within range of a staffed studio or station. Recommended bound (D8): soft claim **never flips a tile by itself**. It lowers the rival's Cohesion in those tiles and raises the projecting civilization's Legacy (Renown, §8.4). Radio and television add national Cohesion; COM-13 raises admin efficiency +15%.

### 5.7 World order and blocs (G)
DIP-12 opens a **world assembly** of contacted civilizations that votes on sanctions and embargoes, one vote each (P), on seeded schedules. DIP-16 forms **pacts** (blocs). TRD-13 damps E7's boom and bust; TRD-14's trade order raises trade with all contacted civilizations +30% and diffusion. TER-14 turns colonies that broke away into autonomous allies. BEL-14 human rights make razing and wars of annihilation impossible and raise the Legacy penalty for atrocities. **GOV-17 World Charter** is §8.4's commitment.

### 5.8 War, deterrence and the race (H)
DEF-12 trenches make front-line tiles near-impossible to take by infantry; WAR-15 tanks and aircraft break them and add air raids that DEF-13 radar cuts −60%. WAR-16 nuclear weapons are **usable** (owner decision, 2026-11-28; §5.8a). DEF-14 is **second-strike capability**, not a ban on war, and DIP-13 is the arms-control treaty. **Climate stress** ([05 §14](05-simulation-structure.md)) begins: every civilization's emissions add to one world value, and above a threshold it raises the frequency of seeded droughts, floods and storms for everyone (P: +50% at full stress in E8; E9 owns the rest). WAY-15 Orbital Launch and WAR-19 intelligence open the race.

### 5.8a Nuclear weapons: what E8 builds (D10, owner decision)
The full rules and draft numbers are in [43 §6.5](43-information-age.md); E8 builds all of them because every atomic row is E8 *(verified: INQ-16, NRG-12, WAR-16, DEF-14, DIP-13 are era 8; no catalog row names missiles or missile defense)*. In short:
1. **Warheads** come from an **enrichment works** (unlocked by WAR-16; a grid-powered, staffed City structure) at one per 8 seasons, with upkeep.
2. **Delivery** by bomber from an airfield (TRN-15, WAR-15; range 25; DEF-13 radar shoots one down on a 50% roll) or by rocket (INQ-17; any seen tile; cannot be stopped). A strike needs a state of war, is a `Strike` command, and is public at launch, so the target can answer in the same season.
3. **A strike** kills 60% of one town, destroys every structure within 1 tile, leaves fallout to 2 tiles that fades 5% a year (no founding or claiming above 50), adds a climate-stress pulse and a short world harvest loss.
4. **Deterrence:** WAR-16's public test tells the world; warhead counts are hidden except through WAR-19 intelligence or a treaty. **DEF-14 makes warheads survive any strike (second strike).** AIs always retaliate; the human has a "Retaliate if struck" standing order, on by default. Wars against nuclear powers are allowed, but the AI prices in retaliation, so they are rare.
5. **First use** (striking someone who has not struck) costs 15% of Legacy, an Aggressor mark, Relations −60 with everyone, an automatic assembly embargo and the coalition's attention; the Charter changes the price (Concord loses its Charter, Renown pays double, Ascendancy less).
6. **DIP-13 treaties** cap both sides at 3 warheads with mutual verification; the catalog's "catastrophe risk −90%" cuts a yearly accident roll.
7. **The AI fires first only** when at war, when its forecast Legacy after every penalty is still better, and when it faces elimination or the loss of its capital (or, in E9, a rival about to win the Apex); never first at a second-strike holder unless facing elimination.

**E9 adds:** strikes on Apex uplinks, the Charter capstones' nuclear clauses and the Concord umbrella, the halved Apex bonus for an Aggressor, and elimination by strikes as an end reason.

### 5.9 Deliberately not in E8
No Apex uplinks (E9), no sabotage orders (E9), no satellites (E9), no internet or data links (E9), no second nuclear weapon type and no missile defense (§5.8a), no money or debt (the D12 rule of [38](38-discovery-age.md) stands), no scripted events, no project that creates people.

## 6. Match structure: the ModernChapter contract

| Mode / civilization state | Research | Systems | Ending |
|---|---|---|---|
| Bronze to Industry | as their own docs | as their own docs | unchanged |
| Modern, before own KEY-8 | as in Industry | Industry rules | KEY-8 does not end this mode |
| Modern, after own KEY-8 | E8 opens by prerequisite and gate | E8 systems eligible; climate stress counts emissions of every civilization past KEY-8 | the human's **KEY-9**; no rival countdown, no year cap |
| Modern, after own KEY-9 | E9 Hidden | E8 economy continues | as above |

Mode byte 5; older modes byte-identical; E8 state written only in Modern. The base game moves to Modern only after promotion and the owner's playtest. Simulation 1:1; view 1:250 only if the figure budget breaks (the balance model's E8 civilization reaches about 92,000 people, [09 §11.2](09-population-and-health.md); the live game's E8 populations are unknown).

## 7. Pacing: 32 minutes

### 7.1 The method (as in [41 §7](41-industry-age.md))
E6's live fit (base 18,000 → 24.9 minutes) gives Insight growth **r = 0.0585 a minute**. Doc 41 carried it through a 32-minute E7: about 35,000 Insight a minute at KEY-7, so about **227,000 at KEY-8** (35,000 × e^(0.0585 × 32)). Both numbers are planned, not measured: E7 is not built, and its live fit replaces them.

### 7.2 Why growth is slower in E8
The E8 era row multiplies fertility by 0.55 (E7: 1.0), and [09 §11.2](09-population-and-health.md) has population growth falling from 6.5% a year in E7 to 3.6% in E8 (one year = one minute). Insight goes as population^0.85, so headcount alone gives about 3% a minute. E8's own Insight rows (mass education ×1.5, labs, the computer ×1.5, grid +10%, suffrage +10%) push it back up. **Central case r = 0.045**, range 0.035 to 0.0585.

### 7.3 Sizing
Bill for 32 minutes = 227,000 × (e^(32r) − 1) / r ≈ **16.3 million** at r = 0.045, about **5 times E7's 3.3 million**. Subtract KEY-9's backfill (1.0 million, §3.2) and divide by the path's base units (cheapest picks, D9 applied, KEY-9 at 2.5):

| Breadth | Critical | Path units | Base for 32 min (r 0.045) | Completions | Minutes each |
|---|---|---|---|---|---|
| 18 (catalog) | 14 | 31.2 | ~490,000 | 19 | 1.7 |
| **22 with GOV-17 on the path (recommended)** | **16** | **38.7** | **~395,000** | **23** | **1.4** |
| 24 | 16 | 39.7 | ~385,000 | 25 | 1.3 |

Sensitivity for the recommended row: r 0.035 → ~320,000, r 0.0585 → ~525,000; 30 minutes → ~345,000, 35 → ~475,000. Each 10% on the base moves the age by about 1.4 minutes. **Draft input: base 400,000, breadth 22, D9, KEY-9 at 2.5 × base**, fitted live in WP-E8-13. The registry's 1,700,000 would make E8 well over an hour.

**Why 32 and not longer.** [02 §8.3](02-research-structure.md) once aimed E8 at 26 to 28 minutes and the balance model ran it at 33. The pacing arithmetic itself allows any length; the limit is the whole match. With E5 to E7 at their targets, KEY-8 lands near minute 150 *(assumed: E6 starts near minute 90 in [slice 1](balance/m13-wp12-discovery-match-slice1.md), plus 25 and 32)*, so a 32-minute E8 puts KEY-9 near minute 180 and a 30-minute E9 ends the game near minute 210, three and a half hours. Longer ages push past four hours before the Apex. 32 keeps E8 equal to E7 and leaves the final say on total length to the endgame questions (§8.6, Q1).

### 7.4 The `e8_research_oracle.py` plan (WP-E8-1; not written here)
Same as doc 41's E7 oracle, with an `e8_anchors.py` that reads **E7's measured** KEY-8 Insight rate and growth (not doc 41's plan) once E7 runs. It also reports: (a) the 14 Apex-prep E8 rows (§3.2.3) and their cost, so E9 can price the readiness gap; (b) the diplomacy backfill per archetype; (c) r split into headcount and institution parts so the transition's effect is visible. Companions: `e8_power_oracle.py` (grid loss, oil burn, reactor and dam), `e8_climate_oracle.py` (emissions to stress to event frequency, the 10% death bound of [09 §10.5](09-population-and-health.md) extended), `e8_combat_oracle.py` (trenches, tanks, air, deterrence).

## 8. The endgame (E8, E9 and the end of the match)

This section frames the end of the game; it does not design E9. Its purpose is to settle, before E9 is drafted, **how a match ends and what a civilization commits to in E8 that decides it**.

### 8.1 How a match ends today *(verified unless noted)*

| Topic | Today |
|---|---|
| What ends a match | Each chapter ends on its last keystone. Bronze (KEY-4) and Iron (KEY-5) still end 24 or 48 seasons after the first rival's keystone. **Since Faith & Fief (E5) only the human's keystone ends the chapter** (D11; Discovery the same). There is no year cap ([28 §17](28-production-gap.md)). |
| A match nobody finishes | Stays open for ever (`MatchEnd.Open`); headless runs stop at their `--minutes` horizon; "an AI-only match that stalls ... now runs until the user quits" ([28 §17](28-production-gap.md)). |
| Score | Legacy = Mastery `2 × (era + 1)` a project + Thriving (replaced Firsts, which pay 0 since 2026-11-06) + Wonders `25 × (era + 1)` + Dominion (claimed-land share a year). The end screen ranks by it: People, Legacy, Dominion, Thriving, Land, and "Keep watching". |
| Who wins | Whoever leads Legacy at the end. The human cannot lose a chapter in any other way: no rival ending, no elimination rule *(assumed: none found)*. |
| Dominion victory | Designed: ≥ 50% of habitable land and ≥ 2× the runner-up for 5 years ([01 §7.2](01-game-structure.md), [05](05-simulation-structure.md), [14](14-ai-opponents.md) D13). **Not built.** |
| Apex victory | Designed: first to complete KEY-10, built as 5 uplinks ≥ 15 tiles apart, rivals can sabotage; ends the match with a large Legacy bonus; ranking still by Legacy ([01 §7.3](01-game-structure.md), catalog KEY-10). Not built. |
| AI | `CoalitionAgainstLeader` is scored (40% Dominion, Apex first, or Legacy lead > 25%, [14 §7.3](14-ai-opponents.md)); `ApexRace` is reserved and unscored *(verified)*. |
| Late posture | [04 §8](04-research-paths.md): "DEF-14 deterrence vs DIP-13 arms control vs WAR-17 / WAR-18" decides "whether the endgame race for the Apex is peaceful or contested". |

### 8.2 Where the docs disagree

| Point | One doc says | Another says | Recommended reading |
|---|---|---|---|
| Rival keystones | [01 §7.3](01-game-structure.md): the **first** civilization to finish KEY-10 wins and ends the match | D11 ([27](27-faith-and-fief-age.md), [38](38-discovery-age.md)): a rival's keystone starts no clock | D11 was about chapter ends; the Apex is a **race by definition**. Make KEY-10 the one exception (§8.5). |
| Firsts | [01 §7.1](01-game-structure.md): Firsts ~15% of Legacy | [22 §3.3](22-bronze-age.md), 2026-11-06: Firsts pay 0; Thriving replaces them | 22 is current; doc 01's table is stale. The Apex is in effect the last First that pays. |
| Match end | [17](17-vertical-slice.md): first KEY-3 or a 40-minute cap | [28 §17](28-production-gap.md): no cap; each chapter ends on its keystone | 28 is current; 17 is the old slice. |
| Dominion | [01 §7.2](01-game-structure.md): a locked decision; [07b](07b-territory-findings.md): first possible about minute 113 to 120, in E7 | Not built in any chapter | Decide whether it is built for E7 (where it first becomes reachable) or retired (§8.6, Q3). |
| Climate | [05 §14](05-simulation-structure.md): shared climate stress in E8-E9 is the "post-modern dilemma" | [06 D12](06-next-steps.md): "leave environmental systems out of the MVP" | The owner's age-by-age build supersedes the MVP cut; E8 adds a bounded version (§5.8). |

### 8.3 Three endgame shapes

**Shape 1 — The Apex race, steered by a Charter (recommended).** The match ends when **any** civilization completes KEY-10's five uplinks; that civilization gets the Apex bonus, and the final ranking is still by Legacy, so a leader that loses the race can still finish first on score ([01 §7.3](01-game-structure.md)). The human can therefore lose: a rival's third uplink sounds a public warning ([08](08-ui-and-world-display.md) already lists "Apex uplink sabotaged" as a critical alert), and the human answers with sabotage, war, a bloc's sanctions, or a faster finish. Dominion stays the one other early finish, if it is built. *E8 decisions it needs:* the Charter (GOV-17), blocs (DIP-16), intelligence (WAR-19), Apex preparation picks (§3.2.3), the posture fork (DEF-14 / DIP-13), the space opening (WAY-15). *Why recommended:* the catalog is already built around it (KEY-10's four prerequisites each descend from an E8 line: COM-16 from broadcasting, WAY-13 from rocketry, NRG-16 from the grid, DIP-14 from the world assembly), it gives one clear finish that a hands-off player can follow, and it reuses the AI's existing coalition plan.

**Shape 2 — Three roads.** The Charter chosen in E8 picks one of three finishing conditions for E9: **Concord** wins by a world-assembly majority backed by its bloc's share of the world's population; **Ascendancy** wins by Dominion (the existing 50% and 2× rule, reachable with mechanized war and deterrence); **Renown** wins by holding a share of the world's soft claim and Cohesion for some years. The Apex stays open to everyone. *E8 decisions:* everything in Shape 1, plus each Charter needs its own measurable world state from E8 on (assembly votes, Dominion share, soft-claim share). *Trade-off:* the most choice and replay value, but about three times the design, AI and balance work, and a real risk that one road dominates (E5's A5 gate shows how hard "no archetype wins more than 30%" already is).

**Shape 3 — The Legacy reckoning.** No race: the match ends when the human completes KEY-10 (as every chapter since E5) or when the human chooses to **retire**; the winner is whoever leads Legacy. The Charter only changes which Legacy channel E8 and E9 pay more for (Concord trade and diplomacy, Ascendancy land, Renown Thriving and Wonders). *E8 decisions:* the Charter and its scoring weights; blocs optional. *Trade-off:* the smallest change and fully consistent with D11, but the human can only lose on score, the AI's Apex plan stays unused, and the end is soft.

| | Shape 1 Apex race | Shape 2 Three roads | Shape 3 Legacy reckoning |
|---|---|---|---|
| Can a rival end the match? | Yes, by the Apex (and Dominion if built) | Yes, by any road | No |
| Can the human lose? | Yes: the race, and on score | Yes | Only on score |
| New end reasons | `RivalApex` (+ `Dominion`) | three road reasons (+ Apex) | `Retired` |
| E8 work beyond the age | Charter, pacts, intelligence | all of Shape 1 plus three world measures | Charter and weights |
| Fits the existing docs | 01 §7.3, 14 §3.3, catalog KEY-10 | 04 §8 posture fork, 08 soft claim | D11, 28 §17 |

### 8.4 The World Charter (all shapes)
GOV-17's commitment is once per match, never reversible, public (rivals know it on contact), and chosen by the same rules for the human and the AI. Draft effects (P, for Shape 1):

| Charter | It makes easier | Its price | How rivals react |
|---|---|---|---|
| **Concord** (blocs and law) | DIP and TRD projects −15%; pacts cost no Relations to form; assembly votes ×2 | Cannot declare a war of conquest without an assembly majority; military upkeep counts double against Cohesion | Seen as a partner; AIs join its blocs more readily |
| **Ascendancy** (power and deterrence) | WAR, DEF and the industrial lineages −15%; levies drilled ×1.2 | Relations with every non-ally drift −1 a year; never wins an assembly vote against a Concord member | Coalition-against-leader triggers sooner against it |
| **Renown** (culture and science) | ART, COM, LRN and INQ −15%; soft-claim range ×1.5; Thriving ×1.1 | Weakest defense: no deterrence bonus, pacts cost double to join | Seen as harmless until it leads, then soft claim provokes Relations loss |

These are cost and behavior levers that go through existing terms (the diffusion floor, Relations, Cohesion, Legacy). None creates a new score. **Nuclear stance (owner decision of 2026-11-28):** Concord is bound by no first use (first use voids its Charter and dissolves its pacts) and gives pact partners an umbrella; Ascendancy has a declared first-use doctrine (rivals price its threat ×1.5, its first-use penalty ×0.75); Renown pays double for first use and loses its soft claim. Full rules in [43 §6.5 and §9](43-information-age.md).

### 8.5 Rules every shape needs
- **Leader definition:** one number, the end-screen Legacy (`CivLegacy`, research Legacy plus Dominion), used by DIP-10 (E7), the coalition plan and any warning.
- **A match nobody finishes:** keep no cap ([28 §17](28-production-gap.md)), but add a **player-initiated end** (retire and see the standings now) so a stalled human game has a clean ending. AI-only runs stay bounded by their horizon.
- **Elimination:** today nothing defines a human with no settlements left *(assumed)*. Every shape needs a rule: the match ends for the human, standings shown, and the record kept.
- **End reasons are appended**, never renumbered; the end screen title becomes data per chapter and reason (knock-on 4).

### 8.6 Questions the owner must answer before E9 is designed
1. **How long should a whole match be?** About three and a half hours to the Apex at today's targets. Is that right, or should E9 be shorter (25) or the early ages faster?
2. **Can a rival end your game?** Shape 1 says yes for the Apex only; Shape 3 says never.
3. **Dominion victory: build it or retire it?** It first becomes reachable in E7.
4. **One finish or several?** Shape 1 (one race) or Shape 2 (three roads).
5. **The Charter:** accept Concord, Ascendancy and Renown (neutral names, no real-world ideologies), and is the choice irreversible?
6. **Nuclear weapons:** ~~deterrent only, or a usable weapon~~ **Decided by the owner 2026-11-28: usable** (§5.8a; [43 §6.5](43-information-age.md)).
7. **Sabotage:** may the human and the AI sabotage uplinks (the catalog says yes), and may bloc partners?
8. **Retire button and elimination:** accept both rules of §8.5?
9. **Climate:** a bounded shared climate stress from E8 (recommended), or none until E9?
10. **After the Apex:** does "Keep watching" continue the world, or is the Apex final?

**Recommendation in plain English:** pick Shape 1. It is the ending the game was designed around, it is the only one where a rival can genuinely beat you without conquering you, it gives the hands-off player one clear thing to watch, and the Charter makes the E8 choice matter without tripling the work. Shape 2 can come later on top of it (the Charters are already its seeds); Shape 3 is the fallback if you never want the AI to end your game.

## 9. Provisional targets (M-xx; none fitted)

| ID | Target | Measurement |
|---|---|---|
| M-T1 | Own KEY-8 → KEY-9 about **32 minutes**; unfinished seats reported, never imputed | 80+ matches, held out |
| M-T2 | **Oil reach:** share of starts that see, drill and move oil within 24 seasons of KEY-8; no Apex soft-lock (oil, bauxite) | `e8-audit` over seeds 1 to 10,000 |
| M-T3 | **Grid:** units conserve across stations, lines and towns; loss as stated | `--modern-power-ledger` |
| M-T4 | **Transition:** births fall and lifespans rise by the era row and hazards only; no scripted spike | Cohort trace |
| M-T5 | **Climate stress:** shared, bounded, from emissions only; event frequency follows the stated curve; deaths from it within the 09 §10.5 bound | Per-cause attribution |
| M-T6 | **Soft claim:** never flips a tile alone | Seeded cases |
| M-T7 | **Blocs and assembly:** votes, pacts and embargoes by their rules; leaving costs as stated | Replay cases |
| M-T8 | **Charters:** each Charter's win share in four-AI free-for-alls within the A5 band (no Charter above 30% more wins than its share) | AI tournament |
| M-T9 | **War:** trenches, tanks, air, radar and bomber interception on the force tables; retaliation in the war value | Isolated trials plus live attrition |
| M-T10 | Apex readiness at KEY-9: how many of the 14 Apex-prep rows each archetype has done | Diagnostic for E9 |
| M-T11 | **Nuclear restraint and rules:** first use in at most 1 four-AI match of 10; no first strike at a second-strike holder unless facing elimination; every strike's deaths, destruction, fallout, world harvest loss, climate pulse and penalties follow [43 §6.5](43-information-age.md) and replay identically | AI tournament plus seeded strike fixtures |

## 10. What the player sees

Generic kit ([08 §13](08-ui-and-world-display.md)). The read of E8 ([08](08-ui-and-world-display.md) era table): highways, suburbs, dams and reservoirs, open pits; skyscrapers, power stations, airfields, reactors; car and truck flows, **power lines**, aircraft arcs; **night lights** at the world zoom; a faint dotted **soft-claim halo** over rival tiles ([08](08-ui-and-world-display.md) overlays); a world-level **climate gauge** at Z4. The Charter is a full-screen Moment with the three choices side by side and a plain line of what each costs. Cards: grid and fuel by town, oil, emissions and the world climate gauge, bloc and assembly, rival Charters, intelligence reports, KEY-9 progress (N/22) and **Apex readiness** (how many KEY-10 prerequisites' parents are done). The Chronicle moves from newsprint to broadcast headlines. **Art P0 silhouettes (for a later art doc):** power station and pylons, oil well and refinery, motor traffic on a highway, airfield and aircraft, skyscraper block, dam, reactor dome, broadcast mast, launch pad (Orbital Launch), soft-claim halo, climate gauge, the Charter Moment.

## 11. Work breakdown and order

Own worktree and branch (`civsim-e8`, `codex/e8-modern`), started only when E7's mode plumbing exists. Rows change new files or additive lines.

| WP | Deliverable | Exit gate | Size | Parallel with |
|---|---|---|---|---|
| **WP-E8-0 Scope** | This doc; owner answers §8.6 Q1 to Q5 and D1 to D5 | Answers recorded | S | — |
| WP-E8-1 Oracles | `e8_research_oracle.py`, `e8_anchors.py` (from E7's live fit), power, climate and combat oracles | `--check` passes; base printed for 30/32/35 | M | 2, 3 |
| WP-E8-2 Content typing | 65 + KEY-9 typed in place (draft), the five new rows, goods (crude, fuel, aluminium, alloys, plastics, silicon, fertilizer, warhead), structures (enrichment works, power station, grid line, dam, reactor, oil well, refinery, highway, airfield, studio, lab, launch site), roles (researcher, operator), V25, era base and breadth 22, KEY-9 requires GOV-17 (D5) | Strict validation clean; draft inert | L | 1, 3 |
| WP-E8-3 Audit | `e8-audit`: oil-seep and bauxite reach per start (the E6 coal lesson), river sites for dams, coastal and continental spread for the Apex's ≥ 15-tile uplink spacing | Report; no worldgen change | M | 1, 2 |
| **WP-E8-4 Mode plumbing** | `Modern = 5`, ceiling 8, KEY-9 ends it, `ModernChain`, `Key9`, isolation tests | Older goldens byte-identical | S | first |
| WP-E8-5 Grid and power | System A | Ledger conserves | L | after 4 |
| WP-E8-6 Oil and the motor | System B | Fuel conserves; idle without fuel | L | after 5 |
| WP-E8-7 Plenty and health | Systems C and E, the transition | M-T4; V15 caps clean | M | 6 |
| WP-E8-8 Electronics | System D | Staffed-only Insight | M | 5 |
| WP-E8-9 Voice and soft claim | System F | M-T6 | M | 8 |
| WP-E8-10 World order and Charter | System G, GOV-17 choice, pacts, assembly | M-T7, M-T8 on fixtures | L | 9 |
| WP-E8-11 War and climate | System H, climate stress | M-T5, M-T9 | L | 10 |
| WP-E8-11a Nuclear arsenal | §5.8a: enrichment works and warheads, bomber and rocket delivery, `Strike` command and the retaliation standing order, strike effects and the fallout array, DEF-14 second strike, DIP-13 treaties and accidents, first-use penalties, the AI's retaliation-aware war value and first-use rule | M-T11 on fixtures; seeded strike replays identical | L | 11 |
| WP-E8-12 AI and forecast | KEY-9 routing, Charter choice, bloc logic, nuclear posture (build, deter, retaliate; first use only by the §5.8a rule), `ApexRace` scoring stub (reads only, no E9 actions) | Stock AI reaches KEY-9 unaided; M-T11 | M | after 5 to 11a |
| WP-E8-13 View, promotion and pacing | `ModernFrame`, cards, Charter Moment; promote all 66 rows together; `modern-match --grant-e8`; base sweep | M-T1 on fit seeds | L | — |
| WP-E8-14 Large sample and playtest | 80+ matches, held out, owner hand playtest | All gates; owner approval | L | — |

**Order:** WP-E8-4, then 2 and 3 together, then 1, then A → B → the rest; the Charter (WP-E8-10) only after the owner answers §8.6. The endgame's own work (end reasons, retire, elimination, Apex race) belongs to E9's doc, except the end-screen text fix, which should happen now (knock-on 4).

## 12. Acceptance series B8-xx (all proposed)

| Gate | Condition |
|---|---|
| B8-01 Isolation | Every older golden and checksum byte-identical (through Industry); E8 inert before own KEY-8 and in older modes |
| B8-02 Completeness | 66 nodes typed; strict validation clean; no hidden gate on the KEY-9 or KEY-10 path without a guarantee (V8) |
| B8-03 Determinism | 1,000 Modern matches replay exactly; snapshots exact; macOS arm64 and Windows x64 agree on a modern-v1 golden |
| B8-04 Mode and end | KEY-8 continuation, KEY-9 threshold, old-save modes |
| B8-05 Pacing | M-T1 |
| B8-06 Power and fuel | M-T2, M-T3 |
| B8-07 Transition and health | M-T4 |
| B8-08 Climate | M-T5 |
| B8-09 World order | M-T6, M-T7, M-T8 |
| B8-10 War | M-T9 |
| B8-10a Nuclear | M-T11; no strike without war; a human with "Retaliate if struck" on always answers; fallout decays exactly |
| B8-11 AI | A1-A8 in Modern; Legend ≥ 60% vs Warlord; Charter shares within A5 |
| B8-12 Performance | Mean tick ≤ 4 ms at E8 p95 population; figure budget at 1:250 |
| B8-13 Comprehension | ≥ 5/8 uncoached players explain their Charter's price and one shared-world effect (climate, bloc or soft claim) |
| B8-14 Integration | Full suite and strict validation green; owner approves merge |

## 13. Knock-on changes to doc 41 (noted here; doc 41 is not edited)

1. **`IsIndustry` must be a containment check** ("Industry or any mode that contains it") from the day it is written, unlike today's `IsDiscovery`, so E8 does not repeat E7's audit of every call site.
2. **Keep an emissions ledger from E7.** Doc 41's pollution should also add each civilization's yearly emissions into a cumulative total (a few `Fix64` values, hashed only in Industry and later). E8's climate stress starts from it; without it E8 starts from zero and E7's dirty growth carries no lasting price.
3. **DIP-10's "leader" should be the end-screen Legacy** (research Legacy plus Dominion) and should reuse the existing `CoalitionAgainstLeader` plan, so E8's blocs and the endgame share one leader definition.
4. **Fix the end-screen titles now.** Today's Discovery game titles a human keystone ending "The Feudal Order is raised" (the title is hard-coded for Bronze and Iron; read in code, not seen in play). Make the title data per chapter before E7 adds a fifth case.
5. **Decide Dominion victory during E7.** Doc 41 §6 says Dominion endings are "reported separately", but no code ends a match on Dominion, and E7 is when it first becomes reachable ([07b](07b-territory-findings.md)). The endgame (§8.6 Q3) needs the answer.
6. **Remember lost colonies.** E7's independence rolls (and TER-16) should record which civilization a seceded colony came from, so E8's TER-14 can turn it into an ally.
7. **Add oil and bauxite to WP-E7-3's audit.** INQ-13 already reveals oil in E7, and KEY-10's closure needs oil (MIN-13) and aluminium (MET-16). Measuring reach early avoids an E6-style soft-lock discovered late.
8. **Log the KEY-8 Insight rate** in E7's match harness; E8's pacing anchor reads it instead of doc 41's planned 227,000.
9. **Optional:** SEA-15 steel hulls could also require SEA-17 ironclads, and LRN-12 laboratories could require LRN-15 polytechnics. Neither is required; both would make E7's new rows lead somewhere.

## 14. Decisions

| ID | Question | Recommendation (plain English) | Needs the owner? |
|---|---|---|---|
| D1 | Is E8 Modern, `ChapterMode.Modern` containing Industry, the base game once accepted? | Yes. | **Yes** |
| D2 | How long? | **32 minutes**, the same as E7; the whole-match length is a separate question (§8.6 Q1). | **Yes** |
| D3 | How many projects for KEY-9? | **22 instead of 18**: a project about every 1.4 minutes, 6 free choices after the Charter, close to E7's rhythm. | **Yes** |
| D4 | Add the five endgame projects? | **Yes.** E8 does not need them for length; it needs them so the endgame has levers. | **Yes** |
| D5 | Put the World Charter on the KEY-9 path? | **Yes**, so every civilization commits before E9 (the hands-off player gets the Council's pick). | **Yes** |
| D6 | Endgame shape | **Shape 1**, the Apex race steered by a Charter (§8.3). | **Yes** |
| D7 | Late-project doubling (D9) | Keep it (36 of 60 rows); fit the base with it. | Please confirm |
| D8 | Soft claim | Never flips land by itself; it moves Cohesion and Legacy only. | Please confirm |
| D9 | Climate stress | Start it in E8, shared and bounded, only from emissions; E9 owns the full system. | Please confirm |
| D10 | Nuclear weapons | **Decided by the owner 2026-11-28: usable** (was "deterrent only"). E8 builds the arsenal of §5.8a; the sizes and prices to confirm are listed as D13 to D17 in [43 §13.2](43-information-age.md) (strike size, DEF-14 as second strike instead of a ban on war, the price of first use, the human's retaliation order, no missile defense). | Settled; details to confirm in doc 43 |
| D11 | Rival countdown on KEY-9 | None (as since E5). | No |
| D12 | Era base | Type **400,000** as the first input (not 1,700,000) and fit it live; expect 320,000 to 525,000. | No |
| D13 | Bauxite | Give aluminium no hidden gate: either no `[Has: Bauxite]` or a bauxite guarantee, chosen after the audit. | No |
| D14 | Representation | Simulation 1:1; 1:250 only in the view if needed. | No |

## 15. Risks

- **Everything here stands on a plan.** E7 is not built; its fit replaces the 227,000 starting rate, and every E8 number moves with it. Re-anchor after E7 lands.
- **Growth in E8 is the least certain number in the game.** The transition can slow Insight more than E8's institutions restore; the base range is wide for that reason.
- **Oil and bauxite could soft-lock the Apex** the way coal nearly locked KEY-7. Audit before typing.
- **The diplomacy backfill is large** (about 1.0 million). Civilizations that never used diplomacy may stall before KEY-9; watch the Warlord.
- **Charters can unbalance the AI tournament.** Fit them on the A5 gate, not by feel.
- **Climate stress is shared,** so one dirty civilization can hurt a clean one; keep the bound tight in E8.
- **Performance:** grids, motor traffic, air and the largest populations yet; measure the tick budget early, and keep 1:250 ready for the view.
- **Endgame drift:** if E9 is designed before §8.6 is answered, it will design its own ending. Answer those questions first.
- **A usable bomb can end a match abruptly and feel unfair** (D10). E8 is where it first appears, so E8 carries the guards: a public test when anyone gets the bomb, an alert when a hostile rival can deliver, a launch you can answer in the same season, one town per strike, radar against bombers, second strike, treaties, and a first-use price so high that the AI strikes first only when it would otherwise be eliminated or lose its capital (M-T11). Build and test the rules with seeded fixtures before the AI may use them; if play still feels unfair, shrink the strike, do not hollow out the deterrent.
