# 22 — The Bronze Age (E3): Metal, Walls and the First States

> The design of the fourth era, **E3 Bronze**, from KEY-3 *Age of Metal* to the moment KEY-4 *Iron & Law* is the next keystone. **The first cut of Bronze is all of Bronze:** the 65 E3 projects and KEY-4 ship together in milestone M10 ([17 §3](17-vertical-slice.md#3-milestones)). The match no longer ends at KEY-3.
> Built on [01](01-game-structure.md), [02](02-research-structure.md), the E3 rows of [03a](03a-research-craft.md)/[03b](03b-research-kin.md)/[03c](03c-research-reach.md), [04 §5.1](04-research-paths.md#51-e3-bronze--key-4-iron--law), [05](05-simulation-structure.md), [07](07-balance-findings.md)/[07b](07b-territory-findings.md)/[07c](07c-e0-e2-checks.md), [09](09-population-and-health.md)–[14](14-ai-opponents.md), [18](18-view-layer.md)–[21](21-island-crossing.md), `content/catalog/`, the balance oracle in [balance/tools](balance/tools/) and measurements of the live sim (§1.4).
> Decided under full design autonomy. Where a decision overrides an earlier one, §1.3 says so and why. **Current status (2026-10-24):** WP1–WP10 implementation work is in the tree; M10 acceptance (WP11) and the balance/playtest work (WP12) remain open. See [17 §3](17-vertical-slice.md#3-milestones) for the milestone summary and [07d](07d-e3-balance-findings.md) for current findings. The era research-cost update is in `content/registries/eras.yaml`, and §18 lists feature follow-up edits to other documents.
> **Node count.** E3 is **65 projects** (24 Craft, 21 Kin, 20 Reach, as in [03 §4.1](03-research-catalog.md#41-projects-per-era-and-theme)) **plus KEY-4** = 66 nodes, all `status: draft` today. A text search for `era: 3` finds 68 lines because KEY-3's `era_advance` and KEY-4's `breadth` also carry the field.

---

## 1. Summary

### 1.1 Bronze in one paragraph
Roots ends with villages, fields and a chief. Bronze is **the age of metal, walls and the first states**: the hills are opened by mines, the forests around them are burned into charcoal, and a gold glint spreads through the toolkits of the people who can get **tin**. Villages become towns with markets and temples, one becomes a city with a palace and scribes, and the empty land between neighbours runs out. Borders touch, the tin field between two peoples becomes the most valuable ground on the continent, raiders arrive with bronze spears and shields, and the answer is a wall. Holding all this together needs law, writing and kingship, because for the first time a people can grow too wide to stay one people. The player gets **Decrees**: a small, research-grown budget of calls (*found a village there*, *send an expedition*, *open that mine*, *raise the levy*, *build the walls now*, *offer a tin compact*) that answer the playtest's request for agency over **where to expand, where to explore and when to fight** without breaking "choose projects, watch the world respond".

### 1.2 Headline decisions

| # | Decision | Section |
|---|----------|---------|
| B-D1 | **The Bronze chapter:** the match ends when the human completes KEY-4, or 3 years after the first rival does, or at Year 64. Legacy decides. Harness matches use the same rule without the human clause | §3.1 |
| B-D2 | **Current cadence projection:** KEY-3 ≈ 41.5 → KEY-4 ≈ 61.25 at Standard, E3 ≈ 19.75 min; EraBase E2/E3 = **150/400**. These are model projections; fresh live acceptance and Year-64 headroom checks are pending | §3.2, §15, 07c §10.1 |
| B-D3 | **Legacy units per era** replace `First = EraBase × 0.5`: Mastery `2 × (era + 1)`, First `3 × (era + 1)` (×4 Keystones and Wonders), built Wonder `25 × (era + 1)`. Dominion unchanged. Restores the 50/30/15/5 split | §3.3 |
| B-D4 | **Mandate and six Decrees**, unlocked at KEY-3 and grown by GOV/COM/BEL research; the AI uses the same levers | §4 |
| B-D5 | **Tin is scarce and placed on purpose:** per continent one contested middle field between the pair, two far placers and one island placer; **arsenical copper is guaranteed** within 25 tiles but deep (Shaft Mining). Bronze is always possible; *good* bronze is a strategic question | §5.1 |
| B-D6 | **Metal is a real chain** (ore → charcoal → smelt → alloy → cast) with tool tiers T8–T10, Chop rungs 5 and 6, durability that makes tin save fuel, and **gradual dormancy** when access is lost | §5 |
| B-D7 | **Charcoal pressure:** metal roughly doubles a smelting settlement's wood demand; forests visibly recede; Coppicing (WOD-04) is the sustainability pick | §5.5 |
| B-D8 | **Tin trade without a diplomacy UI:** goods barter replaces food-for-food; **Compacts** (standing goods routes accepted by the receiver's expected value) and **Embargo** are the only new diplomatic objects; caravans are visible and raidable | §6 |
| B-D9 | **Tin Folk:** at the first KEY-3 in the world, neutral highland villages appear on unclaimed far tin placers; trade with them, invite them, or take them | §6.4 |
| B-D10 | **Bronze war:** bronze arms 2.1–2.3, shields (casualties ×0.7), chariots (×2 on open ground), hillforts (×2.5, refuge), **stone walls (×3, cannot be assaulted in E3: investment only)**. Raids move to soft targets: mining camps, caravans, hamlets, Free Folk | §7 |
| B-D11 | **The early state:** game-scale settlement tiers (Town ≥ 60 people with a Market, City ≥ 90 with a Temple or Palace), a capital, **Admin span**, and **Cohesion and Overreach become live at each civilization's KEY-3** (secession to Free Folk is built) | §8 |
| B-D12 | Representation stays **1 figure = 1 person**; ≤ ~1,600 figures by the Year-64 cap | §3.4 |

### 1.3 Overrides of earlier decisions

| Earlier decision | Where | Override | Why |
|---|---|---|---|
| The match ends at KEY-3 (and, since commit 4cb188d, when the viewer reaches Bronze) | 17 §2.1, `game/` | Bronze chapter end rule (B-D1) | E3 is now playable; the old end was scaffolding for a two-era slice |
| `First = EraBase × 0.5` (×3 Keystones/Wonders); `Mastery = (era + 1)` | 02 §14, `ResearchSystem` | Legacy units per era (B-D3) | At E3 a First would be worth 90 and KEY-4's 1,350; Firsts would be ~60% of Legacy at the chapter end against 15% intended (14 §18.1 flagged it for this milestone) |
| **D6** expansion by fission only, no "found a city" control; **01 §5.5** the player does not control where a settlement is founded or when to fight | 06 §2, 01 §5.5 | The *Found a village* and *Raise the levy* Decrees | Playtest feedback (lack of agency). Decrees are rare, costly calls executed by the same planners, not move orders, so P1 (gardener, not general) holds |
| **D7** diplomacy fully automatic | 06 §2, 05 §12.2, 17 §2.4 | Retired formally. M7a already shipped player orders 10–17 (gift, barter, raid, assault, war, peace, alliance, recall; 18 §7). E3 adds **Compacts**, **Embargo**, **non-aggression pacts** and **border treaties**, decided by the receiver's Strategist | Tin must create trade pressure the player can act on; a one-button offer evaluated by the AI's own EV is the smallest UI that does it |
| Tin guarantee "≥ 1 source per 2 starts; may be on one continent only" | 12 §10 | Per-continent symmetric tin geography plus an arsenical-copper start guarantee (B-D5) | One-continent tin breaks the point-symmetric fairness of 12 §2; a contested middle field creates the conflict the age needs |
| Free Folk claim no land, are passive, secession not built | 13 §9, §12.3 | Free Folk claim land and count in Dominion's denominator; Tin Folk appear; secession and re-absorption are built | E3 is when polities grow wide enough to break, and tin needs neutral holders |
| Settlement tiers by real headcount (Town ≤ 2,000, City ≤ 10,000) | 05 §9.5 | Game-scale functional tiers (B-D11) | At 1 : 1 a whole E3 civilization is ~200–300 people |
| Specialist roles as shares of adults (Priest 0.2%, Scribe 0.1%) | 09 §9, `params.py` | Roles per structure (one Priest per Temple, Scribes per Palace and School) | 0.2% of 85 adults is 0.17 of a priest |
| Cohesion fixed at 1.0 in effect (a 0.6–1.2 factor nudged by events) | 05 §10, `LivingEconomyModel` | Target-seeking Cohesion with Overreach and Admin span, live from each civ's KEY-3 | Without it, Kin and Reach institutions (GOV, COM, BEL) have no job in E3 |
| DEF-05 "besiegers need siege engines" with no siege rule; capture by assault only | 03c, 13 §8.2 | **Investment** (§7.4) | Walls must buy time, not invulnerability |
| **Not overridden:** the 4 : 2 : 1 slot model (D3), breadth N 21 for KEY-4, Dominion victory (D13, left on and unreachable in E3), representation scale, the determinism rules D1–D10 | — | — | The slots are the core decision and tested (P1, P2); Decrees add a second, slower decision without diluting it |

### 1.4 Historical live baseline (measured 2026-10-22)

These measurements predate the 2026-10-24 research-cost changes. They remain the baseline for population and territory comparisons; timing and downstream T-values need fresh acceptance at the current cadence in §15.

Release build of `main`, `civsim.pack`, Standard pace, four Balanced civilizations on the Council (E3 projects cannot complete yet: `LivingEconomyModel.MaxEra = 2`).

| Measure | Seed 20261019 | Seed 777 | Oracle (09 §11.2) |
|---|---|---|---|
| KEY-3 (minutes, four civs) | 30.1 · 31.1 · 31.6 · 33.8 | 31.0 · 32.3 · 34.1 · 35.0 | 29.5 |
| People per civ at minute 35 / 40 / 45 / 55 | 91–121 / 130–171 / 183–226 / 327–387 | 84–109 / 106–139 / 135–187 / 234–312 | ≈ 170 / 240 / 340 / — (E3 end 287 at 42.5) |
| Insight per year at minute 35 / 40 / 45 / 55 | 294–444 / 309–525 / 410–618 / 902–1,068 | 301–331 / 334–425 / 322–563 / 670–960 | E3 mean 371 |
| Know-how share | 17–18% | 17–18% | 16% |
| Settlements per civ at minute 35 / 40 / 45 / 50 (`politics`, seed 20261019) | 3–4 / 4–6 / 7–10 / 8–14 | — | 2 throughout E3 |
| Claimed habitable land at minute 35 / 40 / 45 / 50 | 19.5 / 23.6 / 28.7 / 40.1% | — | 12.4–13.1% at E3 end (07b §9.4) |
| Raids, wars, captures in 50 minutes (Council politics) | 0, 0, 0 (55 gifts, 29 barter trips) | — | — |

**Findings that shape this design:**
1. **Historical Insight pace was on the earlier oracle track** (live E3-window mean ≈ 400/min against 371, Know-how 17–18% against 16%), so E3's length was governed by EraBase. Current cost pacing is updated in §15 and 07c §10.1.
2. **Live population trails the oracle by ~35%** in E3 (exact ages, 07c §7a), so population targets are set on the live sim, not on `balance_sim.py`.
3. **The live map fills 2–3× faster than 07b predicts:** 7–10 settlements per civ and ~29% of land claimed at minute 45, against 2 and ~13%. Uneven settlements split again sooner than the mean-field "average settlement". **The frontier closes during E3, not in E5–E7.** Bronze is therefore designed as the age when borders first meet, and the oracle must be re-anchored (§15.2).
4. **Without purpose, nobody fights:** zero raids in 50 Council minutes; Chieftain AIs raid 1.5–6.5 times in 80 minutes (14 §16.3a). E3 gives conflict an object (tin, mines, caravans, the midline) and gives the player the timing (§4).

---

## 2. What Bronze is

### 2.1 The fantasy and the four tensions
The Bronze Age (in game terms, the Chalcolithic through the Late Bronze Age: Uruk, Egypt's Old Kingdom, Shang, the Aegean palaces) is about **concentration**: metal concentrated in few places, people concentrated in towns, power concentrated in palaces and temples. Four tensions carry the play, and each one has research answers *and* a lever:

| Tension | Pressure | Research answers | Lever |
|---|---|---|---|
| **Metal and fuel** | Bronze multiplies output but eats charcoal; tin is rare | MET, MIN, FIR-09, WOD-04, TRN-05 | *Open a mine*, Beacons |
| **Tin** | Whoever holds the tin holds good bronze | MET-05, TRD-04/05, SEA-05/06, DIP-04/05 | *Compact*, Embargo, *Expedition*, *Raise the levy* |
| **Bronze arms against walls** | Bronze warbands make unwalled villages prey | WAR-05/06, DEF-04/05, STN-11, FOD-07 | *Corvée*, *Raise the levy*, Posture |
| **The reach of the state** | Wide polities fray: Overreach, Admin span, secession | GOV-04/05/06, COM-05/06, NUM-05, BEL-06/07, TER-06 | *Found a village* (and its cost), Posture |

### 2.2 Roots against Bronze

| Aspect | E2 Roots | E3 Bronze |
|---|---|---|
| **Map** | Field patchwork, pastures, cleared forest edges, palisades | Canals along rivers, straighter fields, mine scars and spoil heaps in the hills, **receding forest around smelting towns**, walls and hillforts, temples and one ziggurat, caravans on routes, sails on the coast |
| **Settlements** | 2–4 villages of 20–60 | 5–10: a capital of 120–250 with a palace, towns of 60–150 with markets, villages, **mining camps** |
| **Territory** | Expanding into empty land; first contact ~minute 20 | **Borders touch**; the midline and its tin field are contested; boundary stones and the first treaties |
| **Tools** | Polished stone (T7) | Copper (T8, orange) and bronze (T9/T10, gold); tools greying back to copper when tin is cut |
| **Conflict** | Raids on camps; palisades | Bronze warbands with shields, chariots on open ground; raids on mines and caravans; walls; sieges of walled towns |
| **Society** | Chiefdom, customary law, shrines | Kingship and palace, written law, temples and priests, scribes, specialists; **Cohesion and Overreach bind** |
| **Player** | Three slots, Leaning, Beacons, Posture, M7a orders | **+ Mandate and Decrees, Compacts, Embargo, forecast cards on every hostile order** |
| **Economy** | Food, wood, stone, clay, copper trinkets | + copper and tin ore, charcoal, tin, bronze, iron blooms, salt, cut stone, luxuries |

---

## 3. Match structure

### 3.1 The Bronze chapter (what ends E3, and the match)
- **E3 ends for a civilization when it completes KEY-4.** It enters the **Iron threshold**: E4 projects stay Hidden (not built), the civilization keeps researching E0–E3 leftovers at EraLag 0.7 and keeps scoring Dominion and Mastery.
- **The match ends at the end of the season in which the first of these happens:**
  1. **the human completes KEY-4** ("The Iron Age dawns"), or
  2. **12 seasons (3 years) after the first rival completes KEY-4.** A top-centre countdown appears at once: "The Iron Age is coming: 3 years left." The human can still end it sooner by completing KEY-4, or
  3. **Year 64** (the hard cap; 1.5 × the KEY-4 target).
- **Headless and AI-only matches** (harness, tournaments, every automated gate) use clauses 2 and 3: the first KEY-4 + 12 seasons, or Year 64. The game's human clause is the only difference, and `--human-seat N` in the harness emulates it.
- **Scoring:** Legacy (§3.3) at the end. The end screen keeps standings, the Keystone timeline and each AI's "why" log, and adds the Bronze summary (tin sources, walls, compacts, wars, the largest city).
- **Why this rule.** Ending when *anyone* reaches the next keystone (the old slice) punished slow and experimental play; ending only when the human does lets a leader farm Legacy forever. The countdown gives the race a finish line, gives the laggard three years of comeback, and makes **"when do I close the age?"** a real decision for a leading player (complete KEY-4 now and bank the First, or stay to finish walls and a Wonder until a rival forces the end).
- **Dominion victory** stays enabled and is unreachable in the chapter (§11). Apex: not applicable.

### 3.2 Timings (Standard pace; Quick ×0.5, Epic ×1.73)

| Moment | Oracle | Live today | **Target** | Gate tolerance |
|---|---:|---:|---:|---|
| KEY-3 | 41.5 | 30.1–35.0 measured before cadence updates | **41.5 projection** | rerun live C5 |
| E3 duration (own KEY-3 → own KEY-4) | 19.75 | — | **19.75 projection** | rerun live M10 |
| KEY-4, Council median | 61.25 | — | **61.25 projection** | complete before the Year-64 cap; rerun M10 |
| First KEY-4 in the world | — | — | earlier than Year 64; timing projection pending | fresh M10 run |
| Chapter end, median | — | — | countdown after first KEY-4 or Year 64 cap | reevaluate Year-64 share |
| Decision cadence | historical 34 s per in-era completion (07 F10) | — | current live acceptance pending; mean-field Primary interval is ~71 s | — |

### 3.3 Legacy units per era
`L(e) = e + 1` for a project of era *e*.

| Component | Rule | E0 | E1 | E2 | E3 |
|---|---|---:|---:|---:|---:|
| **Mastery** per completed project | `2 × L(e)` | 2 | 4 | 6 | 8 |
| **First** per project | `3 × L(e)` | 3 | 6 | 9 | 12 |
| **First** for a Keystone or Wonder | `12 × L(e)` | 12 (KEY-1) | 24 (KEY-2) | 36 (KEY-3, Megalith) | 48 (KEY-4, Ziggurat) |
| **Wonder built** (new) | `25 × L(e)` | — | — | 75 (Megalith) | 100 (Ziggurat) |
| **Dominion** | 100 × share of claimed land per year (unchanged, 07b R8) | | | | |

**Historical check (per civilization, four civs).** At minute 33 the earlier-fit units gave Mastery ~440 and Firsts ~260: **53% / 29% / 17%**. At the earlier chapter end near minute 46 they gave Mastery ~780, Firsts ~420, Wonders ~40: **48% / 33% / 18% / 2%**. Under the old rule that match was about Dominion 30%, Mastery 10%, **Firsts 60%**. These scores need a fresh run for the current ~61-minute KEY-4 projection. The units remain era-proportional and do not grow with EraBase.

Every A-gate (A1–A6) must be re-measured under the new units in M10 (§16); A5's archetype fit is expected to move because wins no longer hinge on the Firsts race.

### 3.4 Representation and budgets
- **1 figure = 1 person** through E3 (01 §8, 07 F6). The current live-mode projection is about **375 people per civ at KEY-4** (§15), or ~1,500 figures for four civs; rerun the Year-64 peak and render-budget checks before M10 acceptance.
- Headless chapter (to Year 64, four AIs): **≤ 30 s**; tick ≤ 4 ms mean, no GC pause over 16 ms (S12/S13 extended).

---

## 4. Agency: Mandate and Decrees

### 4.1 Why
Playtesters found the game interesting but wanted to decide **when to form settlements, where to explore and when to raid**. Beacons and Posture bias the sim, and M7a's orders (raid, assault, war, peace) exist but sit in a drawer, fire a fixed party of four and promise nothing. Bronze adds levers that are (a) **rare** (a research-grown budget), (b) **spatial or temporal** (a place or a moment the player chooses), (c) **executed by the same planners** with their own constraints, and (d) **legible before and after** (a forecast card, then a Chronicle line).

### 4.2 Mandate
- **Unlocked by KEY-3** for every civilization ("the chief's word carries"). Integer, starts at **1**, cap **2**; **GOV-05** +1 and **GOV-06** +1 (max 4).
- **Recharge:** +1 every *R* seasons, `R = 8 − 2 (COM-05) − 1 (COM-06) − 1 (BEL-07)`, minimum 4. A civilization with a full Mandate does not accumulate.
- **Historical 13-minute fit:** 52 seasons yielded about **7–9 Mandate** and **5–8 Decrees**. At the current 19.75-minute projection, the model yields about 13.2 Mandate; review the recharge or cap before accepting the longer cadence. Mean-field Primary intervals are about 71 s in Bronze rather than the earlier ~30 s completion cadence.
- Shown as pips in the top bar with "next in 3 seasons".

### 4.3 The six Decrees

| Decree | Cost | Target | Effect | Refused when |
|---|---:|---|---|---|
| **Found a village** | 2 | A tile | The own settlement with the highest population ÷ EffectivePopCap among those with ≥ 20 people, ≥ 60% of their cap and within SettleReach of the tile, sends **40% of its people (≥ 8)** next season, exactly as a fission (cross-section, tools, a season of food). The new village claims ×1.5 for 2 years (TER-05) and starts at Cohesion 60. Counts as the donor's fission for its 4-season cooldown | No donor; tile not habitable, rival- or Free-Folk-owned, < r + 1 from an own settlement, < 9 hex from a rival or Free Folk settlement, or beyond SettleReach (14 land; +6 with TRN-05; 24 by sea with SEA-04) |
| **Expedition** | 1 | A tile within 2 × Reach (Reach E3 = 15; ×1.5 with TRN-06; over open sea with SEA-04) | 4 adults (restless or curious first) with a season of food walk there (riders ×2 speed) and back. Along the path and within 5 tiles of the target they reveal terrain (sight 3, +1 riding), deposits (with INQ-03) and herds; they make the civilization Aware of anyone seen and roll first contact within 2 tiles. Discoveries become civilization-wide on return (at once with WAY-04) | Out of range; no adult to spare |
| **Open a mine** | 1 | A known deposit tile (copper ore, arsenical copper, tin, gold or silver, salt, iron) within mining reach (8 walking tiles; 12 with TRN-05; 16 along rivers and coasts with SEA-05) | A **mining camp** (§5.2) with 4 miners (6 with MIN-04) from the nearest settlement. Without the decree the planner opens camps only on deposits inside its own owned tiles | Rival-owned tile; depth-2 deposit without MIN-04; no reach |
| **Raise the levy** | 1 | A rival or Free Folk settlement seen within the last 2 seasons | For 2 seasons a **host** gathers: all warriors plus 50% of the other adults of own settlements within 2 seasons' travel. The next raid, assault or investment against that target (the player's order or the Strategist's) uses the host. The host does no work while it stands | No sighting; truce, pact or alliance with the owner |
| **Corvée** | 1 | An own settlement, optionally a structure kind | For 2 seasons building labor there ×1.5, aimed at the chosen structure or the top public work (walls > temple > palace > canal > school > market > Wonder). Cohesion −3 there | Nothing buildable |
| **Compact** | 1 (0 with DIP-04) | A contacted civilization or Free Folk village, a good (tin, copper, bronze, salt, gold, food, a luxury) and a direction (buy or sell) | Proposes a standing goods route (§6.2). The receiver's Strategist accepts if both sides gain ≥ 10% in value (5% with NUM-04) and Relations ≥ 0. **Invite** variant for a Free Folk village with an active compact for 8 seasons and Relations > 30: it joins as a daughter village (gift of 10 value) | No contact; at war; Relations < 0; route blocked by a third civilization at war with either side |

**Embargo** is a free toggle (no Mandate): stop selling a chosen good to a chosen civilization. Existing compacts in that good end; Relations −10.

### 4.4 The orders that already exist, made legible
M7a's orders 10–17 stay free and keep their prerequisites (18 §7). E3 changes three things:
1. **Raid party size** is the player's choice: the command's B argument (≥ 4, up to 30% of the adults of the three nearest settlements, or the levy host).
2. **An assault on a walled settlement becomes an investment** (§7.4).
3. **Every hostile order and every Decree shows a forecast card** before confirmation: *"Elders expect: success 70%, about 2 of ours hurt, ~30 rations of loot; Relations −30 with the Heron Folk."* It is computed by the player's own Strategist from the player's **belief state** (fog-fair, 14 §3.1), so it can be wrong, and the result line in the Chronicle says how it went. The card is the cheapest single fix for "I couldn't tell whether a raid would work".

### 4.5 Rules
- **Commands (append only, 15 §6):** `18 IssueDecree` (A = decree kind 1–6; B = target tile index, settlement index or civilization; C = packed extra: good, direction, structure kind or party size) and `19 SetEmbargo` (A = civilization, B = good, C = 1 on, 0 off). No existing kind changes; no replay contains them, so goldens are untouched until a match issues one.
- **Timing:** a command takes effect at its tick; the decree **resolves in the politics step at the end of that season**, civilizations in ascending order, each civilization's decrees in issue order. Mandate is spent only on success; a refusal emits `OrderRefused` with new reason codes `NoMandate`, `NoDonor`, `NoSite`, `OutOfReach`, `Rejected`.
- **Determinism:** Mandate and its recharge counter are integers in the civilization's research state (checksummed, snapshotted). Donor and site choices use the existing fission scoring with ascending-id tie-breaks; expedition outcomes use `Pcg32.ForStream(seed, "politics.expedition", civ, season)`.

### 4.6 The AI uses the same levers
The Strategist issues Decrees through `IStrategistActions` by expected value (§13.3): an AI is never given Mandate it did not earn, and its Decrees appear in the Chronicle exactly as the player's ("The Heron Folk founded a village by the eastern ford").

---

## 5. Metal

### 5.1 Ore, tin and arsenic geography (12 §8 and §10, revised)
Rules for the 4-player point-symmetric map (per continent, mirrored to the other):

| Deposit | Placement | Amount (L of ore) | Depth | Guarantee |
|---|---|---:|---|---|
| **Tin, middle field** | 2 adjacent tiles (a placer and a lode) on foothills or river gravel, **12–16 walking tiles from each start of the pair, distances within 2 tiles of each other** | placer 150 · lode 600 | 1 · 2 | 1 per continent (injected if needed) |
| **Tin, far placers** | 2 single river-placer tiles, each 22–32 walking tiles from its nearer start, one on each start's outer flank (distances within 3 of each other) | 100 each | 1 | 2 per continent |
| **Tin, island** | 1 placer on one of each half-map's islands (reachable only by SEA-04, 21) | 100 | 1 | 1 per half-map |
| **Arsenical copper** | Mountains and hills (12 §8 rate 3 / 1,000) | 800 | **2** | **within 25 walking tiles of every start** (new) |
| **Copper ore** | Foothills and mountains (6 / 1,000) | 300 at depth 1, 900 at depth 2 | 1–2 | **depth 1 within 15 tiles of every start** (new) |
| Native copper | unchanged (start guarantee 20) | 60 | 1 | — |
| Iron | widespread (10 / 1,000) | 1,000 | 1–2 | ≥ 1 within 20 tiles (bog iron; new) |
| Gold or silver | unchanged (continental guarantee) | 40 | 1–2 | — |
| Salt | unchanged | 400 | 1 | — |

- Five tin tiles per continent (two in the middle field, two far placers, one island); nobody starts with tin inside their E2 territory. The **middle field** is the flashpoint between paired neighbours; the **far placers** are where Tin Folk appear (§6.4); the **island** makes SEA-04 a tin route.
- **Fairness (12 §11.1 additions):** middle-field distance difference ≤ 2 tiles; far-placer distance difference ≤ 3; arsenical distance within ±20%.
- **As built (WP4, 2026-10-22):** `WorldGenerator.PlaceBronzeGeography`, checked by `WorldAcceptance.CheckBronzeGeography`; the random tin pass and the old one-tin continental guarantee are gone. Tin quantity tiers: **1** = 100 L (far placers, island), **2** = 150 L (middle placer), **3** = 600 L (middle lode); the economy maps tiers to litres. The middle placer is searched in bands (12–16 tiles from both starts and within 2 of each other; then 10–19 within 3; then 8–24 within 5, which the audit shows is needed only where terrain forces it); far placers likewise (22–32, within 3; then up to 18–36, within 5). Placers prefer rivers and foothills but may sit on any open ground, because rivers rarely cross the midline. Arsenical copper (radius 25, depth 2), depth-1 copper ore (15) and iron (20) are repaired per start with the existing hex-distance repair. 300/300 audited seeds (1–300) are accepted.
- **Golden impact:** worldgen fingerprints change once (deposit layout), and with them every downstream golden. This is a deliberate content change, explained in its commit (CLAUDE.md rule 5).

### 5.2 Mining and mining camps
- **Mining is a settlement job** like quarrying: a deposit tile within the settlement's range (radius 3) can be worked directly. Beyond it, a **mining camp** is needed.
- **A mining camp** is a satellite of a parent settlement on one deposit tile: 4–6 miners who live there (counted in the parent's cohorts), one *mine head* structure (4 ws, wood 6 L), and a **claim of radius 1 at 75% of a six-person band's emission** (√6 × 0.75 ≈ 1.8, owns its radius against T = 0.7; radius 2 with TER-06). It is **not** a settlement for fission, Insight or Dominion weight, and **is** a raid target (§7.5). It closes when its deposit is exhausted or fewer than 2 miners remain.
- Ore is hauled to the parent: 0.1 ws per L per 4 walking tiles (÷5 with TRN-05 carts, ÷3 by water with SEA-05).
- **Output per worker-season:** surface copper ore 1 L; tin placer 0.5 L of tin ore; shaft (depth 2, MIN-04) 1.5 L; fire-setting (MIN-05) ×1.5 at 0.5 L wood per L; copper picks +10%, bronze picks +30%.
- **Charcoal can be made anywhere in range or at a camp** on forest tiles (clamps; FIR-08 Collier role), which is what spreads deforestation into the hills.

### 5.3 Smelting, alloying and casting

| Step | Inputs | Output | Labor (ws) | Needs | Unlocked by |
|---|---|---|---:|---|---|
| Charcoal | 6 L wood | 1 L charcoal | 0.4 | — | FIR-08 |
| Smelt copper | 2 L copper ore + 2 L charcoal | 0.5 L copper | 0.5 | Kiln | MET-02 |
| Smelt copper, bellows | 2 L ore + 1.6 L charcoal | **0.7 L** copper | 0.5 | Smelter | FIR-09 |
| Co-smelt arsenical bronze | 2 L arsenical ore + 2 L charcoal (1.6 with bellows) | 0.5 L (0.7) arsenical bronze | 0.5 | Kiln or Smelter | MET-04 |
| Smelt tin | 1 L tin ore + 1 L charcoal | 0.5 L tin | 0.3 | Kiln or Smelter | MET-05 |
| Alloy tin bronze | 0.9 L copper + 0.1 L tin + 0.5 L charcoal | 1 L bronze | 0.2 | Kiln or Smelter | MET-05 |
| Cast a tool kit | 0.12 L copper (T8) or 0.15 L bronze (T9, T10) | 1 kit | 0.1 | moulds | MET-03 |
| Cast arms and shield | 0.3 L bronze + 1 L wood + 1 leather | 1 arms kit | 0.3 | moulds | WAR-05 |
| Bloomery iron | 2 L iron ore + 3 L charcoal | 0.5 L iron bloom | 0.8 | Smelter | MET-08 |

All quantities are Fix64; recipes run in the planner's craft step in a fixed order (ore → charcoal → smelt → alloy → cast), each limited by its scarcest input.

### 5.4 Tool tiers and the ladders

| Tier | Kit | Project | Metal per kit | Durability (ws) | Chop | Other multipliers |
|---|---|---|---|---:|---|---|
| T7 | Polished stone axe | STN-08 | — | 10 | r4 ×4.0 | (E2 reference) |
| **T8** | Copper kit | MET-03 | 0.12 L copper | 12 | **r5 Copper axe ×4.5** | quarry and mine +10%; timber build ×0.95 |
| **T9** | Arsenical bronze kit | MET-04 | 0.15 L arsenical bronze | 16 | **r6 Bronze axe ×6.0** | harvest labor ×0.8; timber build ×0.85; mine and quarry +30%; clearing ×1.25 |
| **T10** | Tin bronze kit | MET-05 | 0.15 L tin bronze | **24** | **r6 Bronze axe ×6.0** | as T9 |

- Tools are chosen by the 05 §7.2 rule (best rung known, adopted, tooled, gated). **Tin bronze is not a higher rung; it lasts 50% longer**, which means 33% less metal, ore and charcoal per year for the same work: tin's value is fuel and labor, not a bigger number on the axe.
- **Efficiency check (C6):** Chop 4.0 → 6.0 (×1.5), harvest labor ×0.8 and ard ×0.5 on fields, mining ×1.5 (shaft) × 1.3 (bronze picks) ≈ ×2. Inside ×1.5–2 per era.

### 5.5 Fuel: charcoal and the forests
- Forest tiles keep 10 §5.1's stock (30 L, regrowth 0.5 L a season, ×2 with WOD-04 Coppicing). **New:** a forest tile whose standing stock falls below 5 L becomes *cleared* (it reads and moves as open woodland, then grassland) and regrows at 0.5 L a season.
- **Demand per 100 people per year with bronze in use** (derivation, §5.6): wood for metal **20–26 L** (14–19 with bellows), against a radius-3 range whose ~13 forest tiles regrow **~26 L** (52 coppiced). Add hearths, kilns, lime and building, and a smelting settlement without coppicing draws down its standing wood by **30–50% by KEY-4**. With coppicing it stays under 15%.
- When in-range wood runs short, charcoal comes from mining camps in forested hills or from up to 8 tiles away (12 with carts) at the haul cost. Metal output then falls, tools wear out, and the toolkit greys: **the fuel loop is visible on the map and in the Methods overlay**.
- **HNT-10 Seal & Whale Hunting** gives coasts a substitute hearth fuel (1 L blubber oil = 3 L wood), freeing wood for charcoal.

### 5.6 Metal demand (derivation)
A settlement of 100 people has ~35 adults (live mix ~35%) and ~38 ws a season. About 60% of that is tool work (chop, farm, build, mine, craft): 23 ws.
- **Tools:** 23 ÷ 20 (mean bronze durability) ≈ 1.15 kits a season × 0.15 L ≈ **0.69 L of bronze a year** (0.58 with tin, 0.86 arsenical).
- **Arms:** ~4 warriors' kits over 6 years plus battle losses ≈ **0.15 L a year**.
- **Total ≈ 0.85 L of bronze per 100 people per year**: 0.77 L copper (3.1 L ore by kiln, 2.2 by bellows), **0.085 L tin** (0.17 L tin ore), **3.6 L charcoal ≈ 22 L wood** (≈ 14 L with bellows), ~2.5 ws of mining and ~3 ws of charcoal burning, smelting and casting (≈ 3.5% of a year's labor).
- **Tin is tiny in volume and huge in access.** One miner-season of placer tin covers a civilization's year. Scarcity is never volume, so pressure comes from who can reach it, who will sell it, and who can cut it (§6). Deposits are effectively inexhaustible in the chapter; depletion is tracked for later eras.

### 5.7 Dormancy: losing access
- **Access is per settlement**, recomputed each season: a settlement *has* tin (or arsenical ore) if its stores, or its polity's shared stores within 15 tiles (SOC-07) or 20 tiles (GOV-04), hold ≥ 0.05 L.
- Without access, **no new kit of that alloy is cast**. Existing kits keep working until worn (1–3 years), then figures fall back a rung at a time (tin bronze → arsenical if available → copper → stone). The project shows **Dormant** (02 §7) and the Methods overlay marks the fallback with its reason ("no tin").
- This makes an embargo or a lost mine a **slow squeeze**, not a cliff: there is time to answer it (§6, §7).

---

## 6. Tin trade without a diplomacy UI

### 6.1 Goods barter
M5's stand-in (food for food, 10% gain) is replaced by 13 §4.2 as written: each good's value to a civilization = base barter value × clamp(need ÷ stock, 0.5, 3); a trip happens when both sides gain ≥ 10% (5% with NUM-04). New base values: copper 20, tin **40**, bronze 30, iron bloom 15, gold 40, salt 6, cut stone 1, faience 8, glass 12, dyed cloth 8, silk 25, fine bronze 30, turned goods 3. A tin-poor civilization values tin at up to 120 rations a load, so **tin makes its holder rich** (food imports become people) and gives it leverage.

### 6.2 Compacts and Embargo
- A **Compact** is a standing route for one good between two parties (a civilization or a Free Folk village each). Each season the seller sends `min(surplus, buyer's next 4 seasons of need)`; the buyer pays at the scarcity rate in its own goods (food first, then luxuries, copper). It persists until either side's Relations fall below 0 (−30 with DIP-05), war, an embargo, or the route is blocked by a war across it.
- **The AI evaluates every proposal with its forecast** (14 §7.2): it sells tin when the price beats the strategic cost of arming a rival, and refuses or embargoes a forecast leader (Coalition vs. leader, 14 §7.3). That is the pressure: tin flows to friends and stops for threats.
- **Embargo** (§4.3) is the only other diplomatic object. Compacts, pacts (DIP-04) and border treaties (DIP-05) are the whole of E3's new diplomacy: one-button proposals, decided by the other side's Strategist, shown in the Diplomacy drawer.

### 6.3 Caravans
Each compact delivery is a **caravan party** on the road (pack animals, carts with TRN-05, boats with SEA-05/06), visible in the flows layer (08 §3.3). A caravan can be raided (§7.5): raiders take the cargo; the seller and buyer both lose Relations with the raider (−30) and the raider takes reputation −10. Escorts (2 warriors per caravan through territory of a civilization at Relations < 0) are a Strategist choice.

### 6.4 Tin Folk
- **When the first civilization in the world completes KEY-3**, each far or island tin placer that is not owned by a civilization and lies ≥ 8 tiles from any settlement gets a **Free Folk village of 24** (9 adults, 2 elders, 13 children; E2 technology; a palisade; claim radius 3): *"Traders speak of the tin-folk of the western hills."* Typical maps get **3–6**.
- Tin Folk mine and smelt their placer, grow to their food cap, defend at 0.8 strength behind their palisade, never raid, and accept compacts like any Strategist (with a fixed "trade-seeking" policy).
- Three ways to their tin: **trade** (a Compact), **invite** them (§4.3, after 8 seasons of trade), or **take** them (raid or capture; reputation −5 for a raid, −10 for a capture, Relations −20 with every civilization that traded with them).
- Spawning at the first KEY-3 (around minute 30) keeps the worldgen, research and politics goldens (12 and 24 minutes) unchanged until then. Seeding neutral peoples at world generation is the later, cleaner version (§19).

### 6.5 How D7 is resolved
Diplomacy was meant to be automatic (D7), but M7a already gave the player gift, barter, war, peace and alliance orders. E3 completes that set with the minimum the tin economy needs: **Compact, Invite, Embargo, Pact and Treaty**, each a single proposal the other side's Strategist accepts or declines by expected value, each with a forecast line ("They are likely to accept: they lack tin and trust you"). There is no haggling screen, no counter-offer and no multi-term treaty: the price is the barter rate, and the terms are fixed by the project that unlocked them.

---

## 7. War in the Bronze Age

### 7.1 Weapons (13 §7.1, extended)

| Weapon | Project | Strength | Notes |
|---|---|---:|---|
| Stone-tipped spear (E2 reference) | HNT-03 | 1.6 | |
| Bow (ranged) | HNT-06, WAR-03 | 1.8 | ×1.5 behind barriers |
| **Copper spear and dagger** | MET-03 | **1.8** | warriors with copper kits |
| **Bronze arms** | WAR-05 | **2.1** arsenical · **2.3** tin (2.2 as the table mean) | militia (non-warrior defenders, ×0.6) carry the settlement's best stocked spear |
| **Shields** | WAR-05 | casualties taken **×0.7** | for a side whose fighters carry arms kits |
| **Chariot** | WAR-06 | crew of 2, each **×2 strength on open terrain**, casualties ×0.5; no effect against walls | 2 horses, 0.2 L bronze, 2 L wood, 2 wheels; raids with chariots: surprise +10 points, travel ×1.5 |

The combat round (13 §7.2) is unchanged (k = 0.06; 40% of casualties die; MED-04 lowers that to 32%). Because casualties count *figures*, numbers beat quality under the square law; measured, a bronze-and-shield warband is worth about **1.35–1.4× its number** in stone-armed warriors, and **one chariot crew figure is worth two bronze infantry** on open ground (§7.5).

### 7.2 Defenses (13 §6.2, extended)

| Defense | Project | Multiplier | Other effect | Cost |
|---|---|---:|---|---|
| Palisade (E2) | DEF-03 | ×2.0 | burnable | 6 ws, 20 L wood |
| **Hillfort** | DEF-04 | **×2.5** (replaces palisade) | refuge on a hill within 4 tiles for the non-combatants and stores of every own settlement within 6: raid loot ×0.25 | 15 ws, 10 L wood |
| **Stone walls** | DEF-05 | **×3.0** (replaces) | **cannot be assaulted in E3** (no siege engines until E4): investment only (§7.4); raiders loot outside the walls only (×0.25); do not burn | 30 ws + 60 L cut stone per Village ring; ×2 Town, ×3 City |
| Archery behind barriers, hill site, home ground | (13) | ×1.25, ×1.2, ×1.1 | unchanged | |

Walls are a two-year project for a village (about 90 ws with quarrying and dressing); **Corvée** cuts that by a third, which is exactly the "build walls before their bronze arrives" decision.

### 7.3 War and capture
13 §8 holds for camps, hamlets, villages, palisades and hillforts. F5 still holds at E3 arms: at 1.5× field strength (defenses included) an attacker takes a palisaded or hillfort village in one season in 100% of trials; at 1.0× in 1%. A palisaded village of 40 adults with bronze militia needs about **61 bronze warriors** at 1.5×, most of an E3 civilization's adults, so **conquest in E3 aims at young colonies, mining camps, hamlets and Free Folk**, and established villages fall only to a levy or a siege.

### 7.4 Investment (the Bronze Age siege)
- An assault order (or a levy) against a **walled** settlement becomes an **investment** if the force's strength is ≥ **1.2 ×** the defenders' strength *without* the wall multiplier (home ground ×1.1 applies). Otherwise it is refused (`RequirementsNotMet`).
- **Each season of investment:** the target's fields and pastures yield nothing, routes to it are cut, its food need is rationed to **75%** (R5), and Cohesion falls −5 there and −2 at the besiegers' home. One flammable structure burns; structures do not yet have inside/outside positions, so the sim chooses from the settlement's flammable structures.
- **Capitulation:** while stores ≥ one season of normal need, none. Below that, **35%** a season; at zero stores, the settlement's ordinary winter-shortfall mortality applies (09 §4.3) and capitulation is **70%** a season (stream `politics.siege`, keyed by settlement and season). On capitulation the town is captured (13 §8.3) with assimilation −10% (bitterness).
- **Sally:** if the defenders' strength × 1.1 exceeds 0.8 × the besiegers', they sally and fight in the open (no walls).
- **Relief:** any force of the owner or an ally that arrives fights the besiegers in the open.
- **Supply:** besiegers carry 1 season of food; FOD-07 extends this to 2 (FOD-02 is the baseline and does not add a season; 23 §21.1 C4). An own settlement within 8 walking tiles supplies them fully. Otherwise they lose 10% of strength a season to desertion after their carried food is gone. Range-foraging has no numeric rule in the spec and is not modeled yet.
- **Target:** a town with 2–3 seasons of stores falls in **3–6 seasons** to an unrelieved, supplied investment in ≥ 50% of trials (gate B16). Walls buy a year, which is what relief, alliances and peace need.

### 7.5 Raids in E3 and the E3 raid table
Raid parties are 15–30% of the raiding settlements' adults (min 4), or the levy host. **Targets now include** mining camps, caravans, hamlets and Free Folk. Measured with a port of `Combat.Fight` and `Raids.Resolve` (4,000 trials a row; §15.2 makes it the oracle):

**A bronze warband of 12** (2.2 × 1.3 training, shields) against a village of 120 (42 adults):

| Defenses | Success | Raiders hurt | Defenders hurt | Net value (rations) |
|---|---:|---:|---:|---:|
| none, stone-armed militia | **93%** | 9% | 24% | +33 |
| palisade + watchers, stone militia | **13%** | 39% | 8% | −16 |
| palisade + watchers, bronze militia with shields | **2%** | 45% | 3% | −23 |
| palisade + watchers + 8 bronze warriors | **1%** | 60% | 3% | −29 |
| hillfort + watchers + 8 warriors | **0%** | 83% | 2% | −38 |
| walls + watchers + 8 warriors | **0%** | 93% | 2% | −42 |
| (the same 12 with stone spears, no shields, vs none) | 63% | 18% | 17% | +15 |

**A bronze warband of 8** against soft targets:

| Target | Success | Raiders hurt | Defenders hurt | Net |
|---|---:|---:|---:|---:|
| mining camp, 6 miners | **97%** | < 1% | 45% | +25 (and the ore or tin) |
| mining camp, 6 miners + 2 bronze guards + watch | 99% | 4% | 42% | +24 |
| hamlet of 15 adults, stone, no palisade | **100%** | 1% | 37% | +25 |
| hamlet of 15 adults, bronze, thorn barrier + watch | **89%** | 11% | 26% | +17 |

**Field battles:** 20 bronze-and-shield warriors beat 20 stone-armed 100% of the time and 28 of them **18%**; 12 chariot crew against 24 bronze infantry on open ground win **37%**, against 18 win 100%. Chariot crew's shield and chariot casualty reductions multiply (×0.7 ×0.5).

**Reading the table.** Bronze makes unwalled villages and every camp prey, and makes a palisade with bronze militia a wall raiders cannot pass. So E3's raids are **frontier raids**: on mines, caravans, new colonies and Tin Folk. Two guards do not save a mine; deterrence does (retaliation, reputation, pacts, the levy). This is the arms race the age needs: bronze for attack, walls and bronze militia for defense.

### 7.6 Holding what you take
Captured settlements follow 13 §8.3 for assimilation (50% ± modifiers). From KEY-3, E3 uses the R7 Cohesion rule: a captured settlement starts at C 25 and carries a −35 target term that recovers by +1 a season; hearth, feast, shrine and GOV-03 terms are suppressed while it lasts. The eight-season secession timer makes a distant conquered town without law and temples more vulnerable to a **secession**.

---

## 8. Writing, administration and the early state

### 8.1 Settlement tiers at game scale (replaces 05 §9.5 for E3)

| Tier | Unlocked by | Rule | Claim radius | Notes |
|---|---|---|---:|---|
| Village | SOC-06 + TER-04 | (E2) | 4 | |
| **Town** | TRD-04 Marketplaces | pop ≥ 60 and a Market | **5** | specialists (SOC-08); Bakery, Temple allowed |
| **City** | SOC-09 Urbanism | pop ≥ 90 and a Temple or Palace | **6**; with TER-07 a hinterland ring to radius **7 at 50% emission** | +8 Insight a year; group cap 10,000 (never binds at game scale) |
| **Capital** | GOV-05 Kingship & Palace | the settlement with the Palace | +25% claim within its radius | the reference for Overreach |

### 8.2 Admin span
The number of settlements a polity holds without strain: **4** at KEY-3 (SOC-07's "many villages, one voice"), +2 GOV-04, +3 GOV-05, +2 COM-05, +2 NUM-05, +2 GOV-06 (15 with all; 13 on KEY-4's own closure). Each settlement beyond the span costs **−3 Cohesion to every non-capital settlement**. Mining camps and seasonal camps do not count. A Grower that founds faster than it builds institutions feels it first.

### 8.3 Cohesion becomes live
The existing per-settlement factor F ∈ [0.6, 1.2] is kept as the stored value; Cohesion **C = (F − 0.6) ÷ 0.006** (0–100; F 1.0 = C 67) is how rules and the UI read it. From the season a civilization completes KEY-3, each settlement moves a quarter of the way to its **target** every season (integer arithmetic on C, then back to F):

```
Target = 60
       + 5  hearth                       + 8  feast this year (SOC-05)
       + min(5, luxuries per 10 people)  + 3  shrine within 20 (BEL-05)
       + 6  temple here (BEL-06), +2 within 10    + 4 State Cult (BEL-07)
       + 3  GOV-03,  + 5 GOV-06          + 5  Epic Poetry (ART-07)
       + 2  per living Legend here (max 6)        + Wonder within 8 (Megalith 10, Ziggurat 15; ×1.25 ART-06)
       − 20 × max(0, 1 − last winter's food security)
       − 1  per 2% of people lost last year beyond 3%
       − 10 raided in the last 2 years (−5 with BEL-01)
       − 10 × max(0, pop ÷ HousingCap − 1)
       − 5  inequality (SOC-08)
       − Overreach (non-capital):  max(0, walking tiles to the capital − 8) ÷ 2 × (1 − AdminReduction)
       − Span (non-capital):       3 × max(0, settlements − AdminSpan)
       − conquered (from KEY-3, 07d R7): initial C 25; target term −35, recovering +1 a season; suppress hearth/feast/shrine/GOV-03 while the term remains
AdminReduction = 1 − Π(1 − r):  GOV-04 0.10 · COM-05 0.20 · COM-06 0.10 · NUM-05 0.20 · GOV-06 0.15 · BEL-07 0.10 · TER-06 0.10 · TRN-05 0.10 · TRN-06 0.10
```

**Effects (from KEY-3; neutral before it, so E0–E2 behavior and goldens are unchanged):** work speed ×(1 + 0.15 × (C − 65) ÷ 35), clamped 0.85–1.15; GroupCap × F (as now); below C 35 the fission cap trigger drops to 75% (volunteers leave); **below C 15 for 8 consecutive seasons the settlement secedes** as Free Folk, keeping its land and claims, Relations +20 with its old civilization if not at war.

A colony 24 tiles from the capital without administration loses 8; with KEY-4's closure (GOV-04, COM-05, GOV-06, TRN-05) it loses 4.4. The corrected base-60 model puts typical cohesion around 63–80; distant or captured settlements can fall lower when a crisis stacks with overreach or span. Secession needs a crisis stacked on distance: famine, a raid, a conquest, a span overrun.

### 8.4 Writing, scribes and literacy
- **COM-05 Writing** gives the Scribe role (24 Insight a year, ×1.6 calibration as all Insight): **one Scribe per Palace, plus two per School (LRN-04)**; COM-06 Papyrus ×1.5 scribe output. Writing also gives Admin span +2, Overreach −20%, Mandate recharge −2 seasons, and written compacts (Relations decay between partners halved).
- **Priests:** one per Temple, 16 Insight a year (+4 with NUM-06).
- **Literacy** is the count of scribes per 100 adults; it raises adoption: LRN-04 p_teach ×1.25, and a settlement with a Scribe adopts new methods from the capital without the distance lag (05 §16.3).
- **Expected Insight mix at KEY-4:** Commons ~55%, Elders and Shamans ~25%, E3 roles (priests, scribes, cities) **12–20%** (oracle 15%).

### 8.5 Specialization and market towns
SOC-08 makes Smiths, Potters, Miners and Traders full-time in Towns and Cities (+30% to their work rates, +10% to role Insight; Cohesion −5 from inequality). TRD-05 gives a Town on ≥ 2 trade routes food cap +25% and doubles route capacity. A settlement next to a deposit, a river or a crossroads therefore grows into a *mining town*, a *granary town* or a *market town* on its own, which the tile card names.

---

## 9. Food: plough, irrigation and herds

| Project | Mechanism | Cap target (09 §5.2) |
|---|---|---|
| AGR-05 Ard Plough | Till r3 Ard: field labor ×0.5 with one ox pair per 4 fields (NRG-01); **field limit 25% → 35% of range tiles** | Food ×1.17 |
| AGR-06 Irrigation Canals `[Near: River]` | Canal (1 ws per field tile within 2 of a river): irrigated yield ×1.5, ×2 on Arid and Desert tiles (soil counts 100); flood loss ×0.5 | Food ×1.17 |
| AGR-07 Fallow & Manuring | soil loss per harvest 6 → 2.4 with ≥ 2 cattle or 6 sheep per 4 fields | Food ×1.10^0.6 |
| HUS-05 Dairying | herd food without slaughter ×1.5; cheese keeps (5% spoilage) | Food ×1.08^0.5, Lean ×1.05 |
| NUM-06 Solar Calendar | field yield +10%; flood loss ×0.5 | (in AGR) |
| FOD-07 Salting, FOD-08 Bakeries | salted food 3% spoilage; bakery grain +20% for 40 people | Lean ×1.05 |
| TRN-05, GOV-04, TRD-05 | haul, redistribution, market growth | ×1.06, ×1.04, ×1.05 |

The E3 product of 09 §5.2's steps is **×2.16** on an average settlement's food cap: **88 → 190** (C1-E3, ±10%). Fields carry ~45% of E2-end food (07c §3), so the ard's ×1.4 field area gives ≈ ×1.18 and irrigation's ×1.5 on half the fields of a river settlement ≈ ×1.2: the mechanisms reproduce the targets. River settlements pull ahead (the 04 §8 fork), and non-river civilizations lean on manuring, dairying and trade.

---

## 10. Population and health at E3 scale
- **Demography (09):** FertMult 1.25, MortChild 0.034, MortAdult 0.011, Gompertz h45 0.018 (e45 17), WinterMult 1.25. As in E0–E2, these are the **targets the mechanisms must reproduce** (09 §4.1 "live baseline" rule), not runtime bases.
- **Epidemics (09 §10):** outbreak P = min(0.25, 0.010 × pressure); Fever, Flux and **Plague from E3** (R0 2.0, CFR 35%, spreads along routes: **compacts and caravans carry plague**). Pox needs 1,000 people in one settlement to be endemic, which no E3 settlement reaches at 1 : 1, so it arrives only by import. CHM-05 Soap gives sanitation 0.10 (1 − Sanitation ≈ 0.85 in E3). Expected epidemic share of deaths ≈ **10%** (09 §10.6).
- **E3 hazard projects:** MED-04 (injury ×0.92, combat wounded die 32% instead of 40%), CHM-05 (endemic ×0.96, injury ×0.97), GOV-06 (violence ×0.85), MET-04's arsenic (smiths' injury ×1.3).
- **C3-E3:** E3/E0 child and adult annual-effective ratios within ±10% of 09's anchors, e45 17 ± 10%.
- **People per civ at KEY-4: 170–300** (live), settlements of 20–60 (villages), 60–150 (towns), 120–250 (the city).

---

## 11. Territory, Dominion and the Free Folk in E3
- **Claims:** village 4, town 5, city 6, hinterland 7 at 50% (TER-07), mining camps 1 (2 with TER-06), seasonal camps unchanged (no new camps for settlements founded after KEY-4). HUS-08 Pastoral Nomadism: +1 radius on grassland, steppe and savanna. TER-06: own tiles flip only after a challenger leads by 30% for 2 seasons. DIP-05 border treaties freeze a line for 10 years.
- **Expected map (live):** 5–10 settlements per civilization at KEY-4, **30–42% of habitable land claimed**, the paired neighbours' borders touching by KEY-4 on ≥ 75% of seeds. Dominion per civ 15–35% of claimed land.
- **Fission** stays at 85% of the cap (Expand 75%); *Found a village* adds placed foundings from 60%. For an engaged player **20–40% of new E3 settlements come from Decrees**.
- **Free Folk** now claim land (like any settlement), count in "owned by anyone" for Dominion, barter and accept compacts, defend at 0.8, never raid. Sources: secession (§8.3), refugees (13 §8.3), Tin Folk (§6.4). Re-absorption by the *Invite* compact (§4.3). The current B19 target is **0.03–0.08 secessions per civilization per E3**, mostly conquered or distant towns; see the corrected T25 model in 07d §9.
- **Dominion victory** (≥ 50% of all habitable land and ≥ 2× the runner-up for 5 years) stays on and is out of reach in the chapter: a whole neighbour is worth ~20% of the world at KEY-4.

---

## 12. The 65 projects and KEY-4

Effect primitives as in [11 §1](11-e0-e2-project-specs.md#1-effect-primitives-syntax-used-in-the-tables), plus four new kinds: `Admin(span, overreach)`, `Mandate(cap, recharge)`, `Sanitation(s)` and `Defense(×, rule)`. Current costs at **EraBase 400**: D/M **400**, S **480**, R **320**, I **600**, W **800**, K **1,000**. `Cap(…)` values are 09 §5.2's targets. Moments, flavor and the exact registry entries (goods, tools, structures, roles, actions, practice) are work package WP1 (§20); the numbers below are binding for it.

### 12.1 CRAFT (24)

| ID | Project | Type | Requires | Effects | Practice |
|---|---|---|---|---|---|
| STN-11 | Dressed Masonry | M | STN-08, MIN-03 | Good(G-ASHLAR cut stone: 1 L quarried block + 0.5 ws with T8+ chisels) · Mod(stone building labor, −25%) · Unlock(walls, Post-and-Lintel, temples, palace) | build, mine |
| FIR-09 | Bellows & Tuyères | M | FIR-08, FIB-03 | Build(Smelter) · Mod(smelt yield, ×1.4) · Mod(charcoal per smelt, ×0.8) · Unlock(iron bloom, glass) | smelt, tend fire |
| WOD-05 | Joinery | M | WOD-03, MET-03 | Mod(timber building labor, −30%) · Cap(Housing, ×1.05) · Unlock(plank boats, wheels) | build, chop |
| WOD-06 | Bow Lathe | M | WOD-05, MEC-03 | Good(G-TURNED, luxury, barter 3) · Mod(wheel and cart making, −30%) | craft |
| CER-04 | Potter's Wheel | M | CER-03, MEC-03 | Method(Pottery, r2 Wheel, ×3) · Role(Potter) · Build(Potter's workshop) | clay |
| CER-05 | Glazes & Faience | M | CER-04, CHM-03 | Good(G-FAIENCE, luxury, barter 8) | clay, tend fire |
| CER-06 | Glassmaking | D | CER-05, FIR-09 | Good(G-GLASS beads and vessels, luxury, barter 12; needs a Smelter) | tend fire, craft |
| BLD-07 | Post-and-Lintel | M | BLD-05, STN-11 | Build(Hall, the frame Temples and Palaces use) · Cap(Housing, ×1.2) | build |
| BLD-08 | Stepped Pyramid | W | BLD-07, BEL-06 | Wonder(Ziggurat at a City: 200 ws, cut stone 80 L, mudbrick 400) · Cohesion(+15 within 8, +5 everywhere) · Claim(+1.5, radius 8) · Legacy(First 48, built 100) | build, ritual |
| MIN-04 | Shaft Mining | M | MIN-03, MET-03, SOC-07 | Method(Mine, r3 Shaft: depth-2 deposits, ore ×1.5) · Role(Miner) · Build(Mine head) | mine |
| MIN-05 | Fire-Setting | M | MIN-04, FIR-08 | Mod(mine output, ×1.5; 0.5 L wood per L ore) | mine, tend fire |
| MIN-06 | Salt Works | D | MIN-03, FOD-02, [Seen: Salt] | Action+(Salt: 1 L/ws at salt deposits, coastal pans in Summer) · Good(G-SALT, barter 6) · Unlock(FOD-07) | mine |
| MET-03 | Casting & Moulds | M | MET-02, CER-03 | Good(G-TOOL8 copper kit: 0.12 L, durability 12) · Method(Chop, r5 Copper axe, ×4.5) · Mod(quarry and mine, +10%) · Good(G-ARMS1 copper spear and dagger, 1.8) | smelt, craft |
| MET-04 | Arsenical Bronze | M | MET-03, [Has: Arsenic ore] | Good(G-ABRONZE: co-smelt) · Good(G-TOOL9: 0.15 L, durability 16) · Method(Chop, r6 Bronze axe, ×6.0) · Mod(harvest labor ×0.8; timber build ×0.85; mine and quarry +30%; clearing ×1.25) · Risk(smiths' injury ×1.3) | smelt, mine |
| MET-05 | Tin Bronze | M | MET-03, [Has: Tin] | Action+(dig and smelt tin) · Good(G-TIN, G-BRONZE) · Good(G-TOOL10: 0.15 L, durability 24) · Method(Chop, r6 Bronze axe, ×6.0) · same Mods as MET-04 · Rule(tin barter value 40) | smelt, trade |
| MET-06 | Lost-Wax Casting | M | ART-06, (MET-04 \| MET-05) | Good(G-FINEBRONZE statues and vessels, luxury, barter 30) · Mod(Wonder build labor, −10%) · Rule(a Legend commemorated in bronze keeps its bonus) | craft, smelt |
| MET-07 | Gold & Silver Working | D | MET-03, TRD-03, [Seen: Gold or Silver] | Action+(gold and silver: 0.1 L/ws) · Good(G-GOLD, luxury, barter 40; luxury Cohesion ×2) | mine, craft |
| MET-08 | Bloomery Iron | D | FIR-09, (MET-04 \| MET-05) | Good(G-IRON bloom, barter 15) · Reveal(iron within 20) · Rule(no iron tools before E4) · *KEY-4* | smelt |
| MEC-03 | The Wheel | D | MEC-02, WOD-05 | Good(G-WHEEL) · Unlock(carts, potter's wheel, lathe, chariots) | craft, haul |
| NRG-02 | Harness & Yoke | M | NRG-01, FIB-03 | Mod(draft animal work on ard and cart, ×1.5) | herd, farm |
| CHM-04 | Mordants & Dyes | D | CHM-01, FIB-07 | Good(G-DYE) · Unlock(FIB-08) | craft |
| CHM-05 | Soap & Natron | D | CHM-03, FOD-05 | Good(G-SOAP) · Sanitation(0.10) · Hazard(endemic, ×0.96) · Hazard(injury, ×0.97) | craft, heal |
| FIB-08 | Dyed Cloth | M | FIB-07, CHM-04 | Good(G-DYEDCLOTH, luxury, barter 8) · Cohesion(+2 at the capital) | fiber |
| FIB-09 | Silk | D | FIB-07, HUS-03, [Seen: Silk moths] | Good(G-SILK, luxury, barter 25) · Rule(importers value silk compacts ×1.5) | fiber |

### 12.2 KIN (21)

| ID | Project | Type | Requires | Effects | Practice |
|---|---|---|---|---|---|
| AGR-05 | Ard Plough | M | AGR-02, NRG-01 | Method(Till, r3 Ard: field labor ×0.5, one ox pair per 4 fields) · Mod(field limit 25% → 35%) · Cap(Food, ×1.17) | farm, herd |
| AGR-06 | Irrigation Canals | S | AGR-05, NUM-04, [Near: River] | Build(Canal) · Mod(irrigated yield ×1.5, ×2 on Arid and Desert) · Mod(flood loss, ×0.5) · Cap(Food, ×1.17) | farm, build |
| AGR-07 | Fallow & Manuring | M | AGR-05, HUS-04 | Mod(soil loss per harvest 6 → 2.4 with herds) · Cap(Food, ×1.10^0.6) | farm, herd |
| HNT-10 | Seal & Whale Hunting | D | HNT-09, SEA-04, [Near: Coast] | Action+(Marine hunt: 4 R/ws within 2 tiles of shore, injury 1.5%/ws) · Good(G-OIL: 1 L = 3 L of hearth wood) | hunt, paddle |
| HUS-05 | Dairying | M | HUS-04, CER-03 | Mod(herd food without slaughter, ×1.5) · Good(G-CHEESE, 5% spoilage) · Cap(Food, ×1.08^0.5) · Cap(Lean, ×1.05) | herd, preserve |
| HUS-06 | Wool Breeds | M | HUS-03, FIB-06 | Good(G-WOOL) · Mod(yarn output, ×1.5) · Rule(wool garment: warmth +1, stacks with a hide wrap in Cold climates) | herd, fiber |
| HUS-07 | Horse Taming | D | HUS-04, [Seen: Wild horses] | Action+(tame horses: 1 ws an animal, ≤ 15% of a wild herd a year; pasture carries 2) · Good(G-HORSE) · Unlock(riding, chariots) | herd |
| HUS-08 | Pastoral Nomadism | I | HUS-07, TER-05 | Claim(+1 radius on grassland, steppe, savanna) · Rule(seasonal camps follow herds and claim at 75%; camps exempt from Overreach) | herd, mark |
| FOD-07 | Salting & Pickling | M | FOD-06, MIN-06 | Good(G-SALTED, 3% spoilage) · Mod(party and army supply, 1 → 2 seasons) · Cap(Lean, ×1.05) | preserve |
| FOD-08 | Ovens & Bakeries | S | FOD-05, FIR-07 | Build(Bakery: grain food value +20% for 40 people; Towns and Cities) · Role(Baker) | cook, build |
| MED-04 | Surgery & Trepanation | M | MED-03, MET-03 | Hazard(injury, ×0.92) · Rule(combat wounded die 32% instead of 40%) | heal |
| SOC-08 | Craft Specialization | I | SOC-07, TRD-04 | Rule(full-time Smith, Potter, Miner, Trader per Town and City: work rates +30%) · Mod(role Insight, +10%) · Cohesion(−5, inequality) | craft, trade |
| SOC-09 | Urbanism | I | SOC-08, AGR-06 | Tier(City: pop ≥ 90 with a Temple or Palace; radius 6) · Cap(Group, 10,000) · Cap(Housing, ×1.6) · Rule(+8 Insight a year per City) · *KEY-4 path* | socialize, build |
| GOV-04 | Tribute & Redistribution | I | GOV-02, FOD-06 | Rule(stores rebalance between own settlements within 20 tiles each season; local famine deaths −50%) · Admin(span +2, Overreach −10%) · Cap(Food, ×1.04) | council, haul |
| GOV-05 | Kingship & Palace | I | GOV-04, SOC-09 | Build(Palace at the capital: 30 ws, mudbrick 200, cut stone 20) · Role(King) · Claim(+25% in the capital's radius) · Mod(Wonder build, +20%) · Admin(span +3) · Mandate(cap +1) | council, build |
| GOV-06 | Code of Law | I | GOV-05, COM-05 | Cohesion(+5 target, all) · Hazard(violence, ×0.85) · Admin(span +2, Overreach −15%) · Mandate(cap +1) · *KEY-4* | council, scribe |
| BEL-06 | Temples & Priesthood | S | BEL-05, BLD-07 | Build(Temple: 20 ws, mudbrick 120, cut stone 10; Towns and Cities) · Role(Priest: 16 Insight a year, one per Temple) · Cohesion(+6 here, +2 within 10) | ritual, build |
| BEL-07 | State Cult | I | BEL-06, GOV-05 | Cohesion(+4, all) · Admin(Overreach −10%) · Mandate(recharge −1 season) | ritual, council |
| ART-06 | Monumental Reliefs | M | ART-03, STN-11 | Mod(Wonder Cohesion and radius, ×1.25) · Rule(one Legend per settlement keeps its bonus after death) | craft, ritual |
| ART-07 | Epic Poetry | I | ART-04, COM-05 | Cohesion(+5, all) · Lever(Beacon +1) · KnowHow(storytelling +10%) | teach |
| LRN-04 | Scribal Schools | S | LRN-03, COM-05 | Build(School: two Scribes) · Adopt(p_teach ×1.25) | teach, scribe |

### 12.3 REACH (20) and KEY-4

| ID | Project | Type | Requires | Effects | Practice |
|---|---|---|---|---|---|
| WAY-05 | Celestial Navigation | M | WAY-04, NUM-06 | Mod(open-sea reach from land ×2: 11 → 22, Sail 15 → 30) · Mod(shipwreck risk, ×0.7) | sail, scout |
| TRN-05 | Wheeled Carts | M | TRN-04, MEC-03, NRG-01 | Method(Haul, r4 Ox cart: 6 L, ×5 on flat and trail tiles) · Mod(mining and charcoal reach, 8 → 12) · Admin(Overreach −10%) · Cap(Food, ×1.06) | haul |
| TRN-06 | Horseback Riding | M | TRN-04, HUS-07 | Reach(scouts and expeditions: speed ×2, range ×1.5) · Admin(Overreach −10%) · Mod(herders handle ×1.5) | ride, herd |
| SEA-05 | Plank Boats | M | SEA-04, WOD-05 | Method(Paddle, r4 Plank boat: 12 L cargo) · Mod(river and coastal route capacity, ×3; mining reach by water 16) · Mod(fishing, +25%) | paddle, build |
| SEA-06 | Sail | M | SEA-05, FIB-07 | Method(Sail, r5 Square sail: sea speed ×2; open-sea reach 11 → 15) · Mod(shipwreck risk, ×0.7) · Rule(sea routes up to 40 tiles) | sail |
| TRD-04 | Marketplaces | S | TRD-03, SOC-07 | Build(Market) · Role(Trader) · Tier(Town: pop ≥ 60 with a Market; radius 5) · Mod(internal distribution, +30%) | trade |
| TRD-05 | Market Towns | I | TRD-04, TRN-05 | Rule(a Town on ≥ 2 routes: food cap +25%) · Mod(route capacity, ×2) · Cap(Food, ×1.05) · *KEY-4* | trade, haul |
| COM-05 | Writing | D | COM-03, NUM-03 | Role(Scribe: 24 Insight a year; one per Palace) · Admin(span +2, Overreach −20%) · Mandate(recharge −2) · Rule(written compacts and pacts: Relations decay between partners halved) · *KEY-4* | scribe, count stores |
| COM-06 | Papyrus & Parchment | M | COM-05, FIB-03 | Mod(scribe output, ×1.5) · Admin(Overreach −10%) · Mandate(recharge −1) | scribe |
| NUM-04 | Standard Weights & Measures | M | NUM-03, TRD-04 | Mod(trade gain threshold, 10% → 5%) · Mod(construction labor, −10%) | trade, count stores |
| NUM-05 | Place-Value Arithmetic | M | NUM-04, COM-05 | Admin(span +2, Overreach −20%) | count stores, scribe |
| NUM-06 | Solar Calendar & Astronomy | M | NUM-05, BEL-06 | Mod(field yield, +10%) · Mod(flood loss, ×0.5) · Mod(Priest Insight, +4) | count stores, ritual |
| TER-06 | Boundary Stones & Land Rights | I | TER-05, GOV-03 | Rule(own tiles flip only after a 30% lead held 2 seasons) · Admin(Overreach −10%) · Claim(mining camps radius 2) | mark |
| TER-07 | City-States & Hinterlands | I | TER-06, SOC-09 | Claim(each City: hinterland ring to radius 7 at 50% emission) | mark |
| DEF-04 | Earthworks & Hillforts | S | DEF-03, MIN-03 | Build(Hillfort on a hill within 4: 15 ws, 10 L wood) · Defense(×2.5, refuge within 6: raid loot ×0.25) | build, guard |
| DEF-05 | Stone Walls | S | DEF-04, STN-11 | Build(Walls: 30 ws + 60 L cut stone per Village; ×2 Town, ×3 City) · Defense(×3; no assault, investment only; loot ×0.25) | build, guard |
| DIP-04 | Envoys & Oaths | I | DIP-03, GOV-04 | Diplo(Non-aggression pact: 8 years; breaking −60 Relations, reputation −30) · Role(Envoy) · Mandate(Compact costs 0) | meet strangers |
| DIP-05 | Written Treaties | I | DIP-04, COM-05 | Diplo(Border treaty: the current line is fixed for 10 years) · Rule(compacts survive Relations down to −30) | meet strangers, scribe |
| WAR-05 | Bronze Arms & Shields | M | WAR-04, (MET-04 \| MET-05) | Good(G-ARMS2: 0.3 L bronze; 2.1 arsenical, 2.3 tin) · Combat(shields: casualties taken ×0.7) · Rule(militia carry bronze spears from stock) | fight, smelt |
| WAR-06 | Chariots | M | WAR-05, TRN-05, HUS-07 | Good(G-CHARIOT) · Combat(crew ×2 on open terrain, casualties ×0.5; none against walls) · Rule(chariot raids: surprise +10 points, travel ×1.5) | fight, ride |
| **KEY-4** | **Iron & Law** | K (500) | MET-08, COM-05, GOV-06, TRD-05; breadth **21** of E3 | EraAdvance(E4) · Legacy(Keystone, First 48) · Rule(M10: the Iron threshold and the chapter end, §3.1) | — |

**KEY-4's closure** (from the catalog): 18 in-era projects (AGR-05, AGR-06, COM-05, FIR-09, GOV-04, GOV-05, GOV-06, MEC-03, MET-03, MET-04 or MET-05, MET-08, NUM-04, SOC-08, SOC-09, TRD-04, TRD-05, TRN-05, WOD-05), so **3 free picks** at N = 21, plus about 1.7 from the keystone phase (07 F9). It also forces **20 backfills** beyond KEY-3's closure: TRN-01; COM-03, FIB-03, FOD-02, FOD-03, HNT-04, HUS-01, HUS-02, MEC-01, TRN-02 (E1); BLD-05, CER-03, FIR-08, FOD-06, HUS-03, HUS-04, MEC-02, NRG-01, TRN-04, WOD-03 (E2). Most are already done by an average E2 civilization; the Council's whole-closure boost (02 §15) schedules the rest. **No E3 project or keystone is blocked by geography:** `[Has]` and `[Near]` gate use, never research (D10).

**Where the free picks go** is where E3's strategy lives: WAR-05 (arms race), DEF-04/05 (walls), MIN-04 (arsenic route), BEL-06 (temples, priests, Cohesion), LRN-04/COM-06 (Insight), HUS-07/WAR-06 (steppe), SEA-05/06/WAY-05 (sea and tin isles), FOD-07 (supply for sieges), BLD-08 (the Wonder). Playing past KEY-4 isn't possible for the human; the countdown rule (§3.1) makes "stay in Bronze for two more picks" a real trade against the KEY-4 First.

---

## 13. The AI in Bronze (changes to 14)

### 13.1 Forecast model (14 §3.2)
- **E3 inside the rollout:** cap steps (09 §5.2 E3), E3 roles by structure, Legacy units (§3.3), the chapter end rule as the horizon end, KEY-4's closure.
- **New state per civilization:** best tool tier and access flags (tin, arsenic, copper), metal output per year (from §5.6's per-capita demand and mining labor), Mandate and recharge, mean Cohesion with span and Overreach terms, walls per settlement, compacts held.
- **Fidelity target:** KEY-4 within 15% of the live sim, median ≤ 6% (today KEY-1–3: median 4.5%).

### 13.2 Research (Spark)
- **The bronze fork:** V(MET-05) uses P(tin access within the horizon) from beliefs: own claim on a tin tile, an open compact offer, a Tin Folk village in reach, Relations with tin holders. V(MET-04) needs MIN-04 and an arsenical deposit within mining reach. A smart AI researches the route it can feed, and both when tin is contested.
- **Defense on time (A1 in practice):** if a rival's believed WAR-05 ETA is earlier than its own palisade, hillfort or walls ETA + 2 seasons, DEF and WAR projects jump and a Corvée is issued.
- **Archetype signatures in E3:** Builder STN-11, BLD-07/08, DEF-05, FIR-09; Grower AGR-05/06/07, SOC-09, FOD-08, Found; Wanderer SEA-05/06, WAY-05, TRD-04/05, HUS-07/TRN-06, Expedition and Compact; Warlord WAR-05/06, FOD-07, Levy; Sage COM-05/06, LRN-04, NUM-05/06, BEL-06/07. Weights inside the same planner; competence is equal (14 §9.1).

### 13.3 Strategist
- **New grand plan, *Secure tin*** (when MET-05's value is high and access is missing), with options ranked by forecast EV: open a mine on unclaimed tin (with a Beacon and a Found nearby), a compact with a holder, a levy against a Tin Folk village or a rival's mining camp, or the arsenical fallback.
- **Siege planning:** invest only with a supply line (an own settlement within 8 tiles, or FOD-07) and ≥ 1.2× the unwalled defenders; lift the siege when relief is forecast to win.
- **Raid targets:** mining camps, caravans and young colonies first, by the §7.5 table on *believed* defenses; never a palisade with bronze militia (the 30% floor already forbids it).
- **Decrees by EV:** each season the Strategist scores each available Decree with the forecast (a Found's land and Dominion; an Expedition's information value for unexplored frontier and unseen deposits; a mine's metal; a levy's force ratio; a Corvée's walls-before-threat margin; a compact's trade value), issues the best above a threshold, and avoids sitting at the Mandate cap.
- **Coalition and tin:** a forecast leader that holds the middle field faces embargoes from the others' Strategists (14 §7.3).

### 13.4 Belief (14 §5, §16)
New evidence by sight: copper and bronze tool tints (MET-03; MET-04 or MET-05), walls and hillforts (DEF-04/05), chariots (WAR-06), scribes (COM-05), temples (BEL-06), mining camps on deposits, caravans and what they carry (a compact, hence tin access), a palace (GOV-05). Compacts are public to their parties and to anyone who sees the caravan. The fog-fairness test (A8) extends to compacts, caravans and Mandate (randomizing unseen ones must not change decisions).

### 13.5 Archetype balance and blunders
- **A5 re-fit** at the chapter end under the new Legacy units; expect the `ArchetypeResearch` table to move (the Firsts edge that made Sage and Grower win is smaller).
- **Raid calibration:** multiply the simulated success probability by **3/4**, fitted to the prior 8-seed, 64-minute baseline (66/84 resolved raids: 75% predicted, 56% observed). The AI uses this calibrated probability for raid EV, the launch threshold and its decision log; rerun the final 8-seed check before treating the fit as accepted.
- **New blunder detectors (14 §10.4):** *Dormant bronze* (MET-04/05 done, no access for > 8 seasons while a compact, mine or arsenical option existed), *Unwalled frontier* (rival bronze warriors seen within reach and no defensive response in 4 seasons), *Siege without supply* (besiegers lose > 30% to desertion), *Mandate overflow* (> 8 seasons at the cap with a positive-EV Decree available), *Naked caravan* (tin caravan through hostile land without escort).

---

## 14. The view layer (18, 19, 20)

| Surface | M10 scope |
|---|---|
| **Frame (18 §3)** | Tiles: `ForestBand` (0–3) on visible tiles, `DepositWorked`; settlements: tier, Cohesion 0–100, walls kind, structures (new kinds appended); camps: `CampKind` (seasonal, mining) and deposit; routes: `RouteFrom/To`, `RouteGood`, `RouteVolume`, caravan parties as a new `PartyKind`; occupations: `OccupationKind` (occupy, invest) and a stores band; focus: `FocusMandate`, `FocusMandateCap`, `FocusMandateNextSeason`, decree states; civilizations: `CivMetalTier` (public, from sight), compacts with the viewer, embargo flags; header: chapter countdown |
| **Events (18 §6)** | DecreeIssued/Refused/Resolved, ExpeditionReturned, MineOpened/Closed, CompactSigned/Ended, EmbargoSet, CaravanRaided, InvestmentBegan/Relieved/Capitulated, Secession, TinFolkAppeared, TierReached (Town, City), WallsBuilt, WonderBuilt, IronThreshold, ChapterCountdown, ChapterEnded (all appended, with text keys) |
| **Figure kit (08 §2.1)** | Copper (orange) and bronze (gold) tints on every tool; new held items: pick (miner), tongs and hammer (smith), tablet and stylus (scribe), staff and tall hat (priest), circlet (king), spear, shield and helmet (bronze soldier); woven tunics; vehicles: chariot (2 crew + 2 horses), ox cart, plank boat and square sail |
| **Structures (~16, placeholders first, 17 §6)** | Mine head and spoil heap, mining camp, smelter with bellows, charcoal clamp (smoke), market, temple, palace, school, bakery, potter's workshop, hillfort rings, stone walls (village, town, city rings), canals (blue lines on field tiles), hall, ziggurat (Wonder, staged by build progress) |
| **Overlays (08 §5)** | **Deposits and mines** (extends Resources: worked deposits, mining reach), **Trade** (routes with good icons, tin highlighted, caravans), **Cohesion** (rings by band, Overreach and span reasons in the tile card), **Forest** (wood stock bands) |
| **Research web (20 §3.4, 08 §8)** | The E3 column (65 nodes) in the Map view and in Choose; KEY-4 on the spine with "Completing this ends the chapter"; E4 stays hidden; Dormant hatching with "no tin" |
| **Decrees UI (new)** | Mandate pips in the top bar; a Decrees block in the map-tools rail (six buttons) using the Beacon tool's target-picking mode; the forecast card (§4.4) before confirmation; Chronicle lines and toasts for results |
| **Diplomacy drawer (20 §3.9)** | A Compacts section (proposals with a "likely to accept" line, active compacts, Embargo toggles), Free Folk listed as parties, pacts and treaties, the raid party-size stepper, investment status |
| **End screen, onboarding** | Chapter countdown banner; the Bronze summary on the end screen; three coach tips at KEY-3 (Mandate and Decrees, tin and the tool tint, walls) |

Budgets: 60 fps at Z0–Z2 with 1,600 figures, the new structures and all overlays (S14); frame build ≤ 0.5 ms; no per-frame allocation (18 §9).

---

## 15. Balance targets

> **Historical amendment (2026-10-22 and earlier 2026-10-24):** the live Council and oracle fit moved EraBase E3 from 200 to 150, then the first cadence follow-up set E2/E3 to 100/160. Those projections and costs are superseded by [07c §10.1](07c-e0-e2-checks.md#101-roots-and-bronze-still-finish-too-quickly-2026-10-24). The current model projection uses E2/E3 150/400, puts KEY-3 at 41.5, KEY-4 at 61.25, and E3 at 19.75 minutes. It has not been confirmed by a fresh live acceptance run; the existing Year-64 cap leaves little projected headroom. T5/T10 and other downstream balance targets below also need reevaluation with the slower schedule. The finding that **live settlements are housing-bound, not food-bound**, makes Market towns build housing ahead (07d F2).

### 15.1 Target table (Standard pace, four civilizations, Balanced unless stated)

| # | Quantity | Target | Tolerance | Reasoning | Oracle |
|---|---|---|---|---|---|
| T1 | KEY-3 | 41.5 min (projection) | live C5 rerun pending | updated live-mode oracle | balance_sim projection |
| T2 | **E3 duration** (own KEY-3 → own KEY-4) | **19.75 min (projection)** | live M10 rerun pending | current EraBase E3=400 projection | balance_sim projection |
| T3 | **KEY-4** (Council median) | **61.25 min (projection)** | confirm completion before Year 64 | current KEY-3 + E3 projection | balance_sim projection |
| T4 | **EraBase E3** | **400** (D/M 400, S 480, R 320, I 600, W 800, K 1,000) | fresh live fit after M10 acceptance | tuned to slow Bronze; replaces the earlier 150 live fit | fit.py projection; acceptance pending |
| T5 | Insight per minute | ~330 at KEY-3 → 650–800 at KEY-4 | ±20% | live 300–440 at minute 35 (E2 tech) + E3 roles | live |
| T6 | Know-how share of progress | 13–20% | — | 07 F4; live 17–18% | existing |
| T7 | E3 roles' share of Insight | 12–20% | — | oracle 15% (07 §6); roles per structure (§8.4) | balance_sim (roles by structure: extension) |
| T8 | In-era completions / backfills | 22–25 / 11–20 | — | N 21 + 1.7 (07 F9); 20 forced backfills, ~30–40% pre-taken | balance_sim ✓ |
| T9 | Primary interval and Mandate | Primary ~71 s in mean-field; fresh live acceptance and Mandate budget review pending | — | Current EraBase model; old per-completion gate used the 13-minute E3 | balance_sim projection |
| T10 | **People per civ at KEY-4** | current model projection 374–378; live rebaseline pending (historical target 220, 170–300) | — | live-mode model at E2/E3 EraBase 150/400; earlier data were measured by minute 45 | exact_age_sensitivity (rerun for current cadence) |
| T11 | **Settlements per civ at KEY-4** | current model projection 10–13; live rebaseline pending (historical target 7, 5–10) | — | village/towns housing projections at E3 EraBase 400 | territory_sim on current live trajectory |
| T12 | FoodCap per average settlement | 88 → 190 | ±10% (C1-E3) | 09 §5.3; §9 mechanism check | check_e0e2 → check_e3 (extension) |
| T13 | Hazards E3/E0, e45 | 09 §4.1 ratios; e45 17 | ±10% (C3-E3) | 09 | check_e3 + harness (extension) |
| T14 | **Claimed land at KEY-4** | **30–42%** | — | live 28.7% at minute 45 (radius 4); town 5, city 6, hinterland 7 | territory_sim (extension) |
| T15 | Paired borders touch | by KEY-4 on ≥ 75% of seeds | — | live map fill (§1.4) | territory_sim (extension) |
| T16 | Fission and claims | trigger 85% (Expand 75%, Found ≥ 60%); 20–40% of an engaged player's E3 foundings by Decree; T 0.7, lead 20% (TER-06 30%, 2 seasons) | — | 09 §7.1, 07b R3 | territory_sim (extension) |
| T17 | **Metal demand** per 100 people per year | bronze 0.8–1.0 L; tin 0.08 L; charcoal 2.3–4.5 L; wood 20–26 L (14–19 with bellows); mining, charcoal and smelting ≈ 3–4% of labor | ±20% | §5.6 | metal_sim (new) |
| T18 | Bronze coverage | ≥ 70% of tool-using workers on rung 6 within 12 seasons of MET-04/05 where access holds | — | adoption 2–4 seasons (02 §10) + casting throughput | metal_sim (new) |
| T19 | Fuel | without WOD-04: smelting settlements lose 30–50% of standing wood by KEY-4; with it < 15%; metal output loss ≤ 30% with WOD-04 | — | §5.5 | metal_sim (new) |
| T20 | Tin access by KEY-4 | ≥ 50% of civs with tin (own field, compact or Tin Folk); ≥ 90% with some bronze | — | 6 tin tiles per continent, arsenic guarantee | metal_sim + territory_sim |
| T21 | **Combat** | the §7.5 tables | ±10 points | port of Combat/Raids | combat_e3 (new; the M10 design MC) |
| T22 | Capture and siege | 1.5× takes a palisaded or hillfort village in one season ≥ 60% (measured 100%); a supplied, unrelieved investment takes a 2–3-season town in 3–6 seasons ≥ 50% | — | §7.3–7.4 | combat_e3 (new) |
| T23 | Conflict happens | Chieftain 4-AI: 3–12 raids per chapter; ≥ 1 war in ≥ 50% of matches; ≥ 1 capture in 20–50% | — | §1.4 finding 4 | harness |
| T24 | **Legacy split at chapter end** | Dominion 45–55%, Mastery 25–35%, Firsts 12–20%, Wonders 0–8% | — | §3.3 | legacy_scale (new) |
| T25 | Cohesion | settlements 60–80 typical; 0.03–0.08 secessions per civ per E3; > 2 in one civ within 2 years in < 2% of matches | — | §8.3; rebaseline in 07d §2/§11 | cohesion_sim (new) |
| T26 | Tin Folk | 3–6 per map at the first KEY-3; ≥ 1 compact with one in ≥ 50% of matches | — | §6.4 | harness |
| T27 | Mandate | Historical 7–9 earned and 5–8 Decrees over 13 minutes; current projection ≈13 earned over 19.75 minutes, budget review pending | — | §4.2 | live harness rerun |
| T28 | Figures and tick | ≤ 1,600 figures at Year 64; tick ≤ 4 ms mean | — | §3.4 | harness |

### 15.2 What the Python oracle must be extended to model
1. **`balance_sim.py` live mode:** `InsightCalibration` 1.6 on the whole formula, adults from the exact-age reference (07c §6), E3 roles by structure (one Priest per Temple, Scribes per Palace and School, +8 per City) instead of shares, and a settlement-count curve from the live harness or the territory model instead of `pop ÷ 15`. Then `fit.py` re-fits EraBase[3] (T4).
2. **`check_e3.py`** (from `check_e0e2.py`): C1-E3 FoodCap with the 35% field limit, irrigation by river share, carts and dairying; C3-E3 hazard ratios with MED-04, CHM-05, GOV-06 and plague.
3. **`metal_sim.py` (new):** per settlement and season, mining labor, ore and charcoal stocks, forest stock and clearing, smelting, alloying, casting, kit wear by durability, dormancy on lost access; outputs T17–T20.
4. **`combat_e3.py` (new):** the port of `Combat.Fight`/`Raids.Resolve` used for §7.5 (shields, chariots, hillforts, walls, the levy host) and the investment model of §7.4; the reference for the C# `CombatTables` E3 rows.
5. **`territory_sim.py`:** E3 tiers and radii, the hinterland ring, mining camps as claim sources, *Found* Decrees as extra fissions toward Beacons, Tin Folk as claiming neutrals, input trajectories from the live harness (finding 3), border-touch timing (T14–T16).
6. **`legacy_scale.py` (new):** the Dominion/Mastery/First/Wonder split over a chapter under §3.3 (T24).
7. **`cohesion_sim.py` (new):** the §8.3 target model on a settlement layout with span and Overreach; secession rates (T25).

---

## 16. Acceptance gates (milestone M10)

The milestone is done when every automated row passes in the nightly report and the human rows pass with real players. They are also listed in [17 §4.4](17-vertical-slice.md#44-bronze-m10).

| # | Test | Target | How measured | Kind |
|---|---|---|---|---|
| B1 | **Agency.** Testers use Decrees and can name a decision of *where* or *when* | ≥ 5 of 8 use ≥ 2 Decree kinds unprompted and name one such decision | M9 protocol plus two questions (§19) | Human |
| B2 | **Bronze is legible.** Testers can say why their tools are or are not bronze | ≥ 5 of 8 | post-match question | Human |
| B3 | **Want more** after the Bronze end screen; can explain one AI action | ≥ 5 of 8 (P9 repeated) | M9 protocol | Human |
| B4 | Every E3 project and KEY-4 shows a visible change | 66/66 emit Completed, Moment, ≥ 1 VisibleChange | event-stream test (C8) | Auto |
| B5 | **Keystone timing** | Balanced Council KEY-4 median 57–64 min before Year 64; E3 duration median and each civilization within 19.75 ±25% (14.81–24.69 min) | `research --check-m10`, seeds 20261019 and 777; opening-bot timing still needs separate coverage | Auto |
| B6 | **Chapter end** | Every sample ends by the §3.1 rule: 12 seasons after the first rival KEY-4 or at Year 64; ≤ 10% of Balanced matches reach Year 64; the end rule is replay-identical. Report the observed end-time median. | harness, 32 seeds | Auto |
| B7 | **Hands-off loses to engaged** at the chapter end | ≥ 65% (P8 at the new horizon) | Settler proxy vs Council, mirrored | Auto |
| B8 | **Decrees pay** | a Decree-using scripted bot beats the same bot with Decrees off in ≥ 60% | mirrored duels, 32 seeds | Auto |
| B9 | FoodCap E3 (C1-E3) | 88 → 190 ±10% | check_e3 + live fixture | Auto |
| B10 | Hazards E3 (C3-E3) | over seeds 20261019–20261042: expected-hazard ratios and e45 ±10%; expected epidemic share 7–13% | `research --report-e3` expected-hazard ledger + `check_e3.py`, 24 seeds × 64 minutes | Auto |
| B11 | Efficiency (C6-E3) | chop ×1.5, mining ×1.5–2, field labor down ≥ 40% | method-rung report | Auto |
| B12 | **Metal chain** | T18; after tin is cut, rung 6 falls back within 12 seasons with a "no tin" reason; no smelting without charcoal | economy fixtures | Auto |
| B13 | **Fuel pressure** | T19 | economy fixtures + metal_sim | Auto |
| B14 | **Tin geography** | §5.1 rules in 100% of 10,000 worlds; determinism; < 5 s per world | `worldgen --audit-seeds` | Auto |
| B15 | **E3 raid table** | every §7.5 table value within ±10 of the oracle-corrected values in 07d §6; includes success, both sides hurt, net and the field rows | `combat_e3.py` plus `politics --report-combat` E3 rows | Auto |
| B16 | **Capture and siege** | T22; no assault ever succeeds against stone walls | combat tables + fixture | Auto |
| B17 | **Territory** | T11, T14, T15 sampled at each civilization's KEY-4; at chapter end, ≥ 1 Town in ≥ 75% and ≥ 1 City (tier ≥ 90) in ≥ 25% of civilizations (07d §2 F4/§11 rebaseline) | `politics --check-m10`, 24 seeds | Auto |
| B18 | **Legacy split** | T24 | Chieftain 4-AI, 24 seeds | Auto |
| B19 | **Cohesion and secession** | T25: cohesion 60–80; 0.03–0.08 secessions per civ per E3; cascades below 2% | Chieftain 4-AI, 24 seeds | Auto |
| B20 | **Conflict exists** | T23 | Chieftain 4-AI, 24 seeds | Auto |
| B21 | **Tin Folk and compacts** | T26 | `research --check-b21` (default: 24 matches, seeds 20261019–20261042, 64-minute horizon); both criteria must pass | Auto |
| B22 | Determinism | S1 (1,000 chapter replays) and S2 (macOS arm64 = Windows x64) on a new `tests/golden/bronze-v1.txt` | CI | Auto |
| B23 | Performance | headless chapter ≤ 30 s; tick ≤ 4 ms mean over the 10-minute S13 sample, no GC pause > 16 ms; 60 fps with 1,600 figures and E3 structures at Z0–Z2 | `acceptance --s12 --s13`, Godot render gate (shorter S13 samples are diagnostic only) | Auto |
| B24 | Robustness | 0 crashes, desyncs or invalid states in 1,000 chapter matches, all archetypes and levels | `acceptance --s15 --minutes 64` | Auto |
| B25 | Content | 66 E3 nodes `numeric`; validator 0 errors; pack golden refreshed | `civsim validate --strict` | Auto |
| B26 | **AI gates at the chapter end** | A1–A8 targets of 14 §10.3 under the new Legacy units | `ai --check-m10 --seeds 8 --minutes 64` | Auto |
| B27 | **Tin intelligence** | a tin-poor AI (no tin 8 seasons after KEY-3) gets tin or completes the arsenical route in ≥ 80%; Dormant-bronze blunder < 0.1 per match | harness | Auto |
| B28 | **Defense timing** | an AI facing a believed bronze-armed neighbour has palisade, hillfort or walls before the first bronze raid on it in ≥ 80% | harness | Auto |
| B29 | Forecast fidelity | KEY-4 within 15%, median ≤ 6% | harness | Auto |
| B30 | New blunders | Mandate overflow < 0.3, siege without supply 0, suicide raid 0, naked caravan < 0.5 per match | blunder detectors | Auto |
| B31 | Legend vs Warlord | ≥ 60% (the 65% of 14 §10.3 moves to E4) | ladder | Auto |

For B10, aggregate expected epidemic hazard from all modeled disease classes over the fixed 24-seed cohort and divide by total expected baseline mortality hazard across E3 age exposures. Exclude the conditional winter-shortfall surcharge from this denominator; it is separate from the §4.1 baseline. Use the season-adjusted §4.1 E0 anchors for the expected child/adult ratios and baseline expected hazards for e45. Realized death shares and shares recalculated with the winter-shortfall surcharge are diagnostic only. Reproduce with `research --report-e3 --seeds 20261019,20261020,20261021,20261022,20261023,20261024,20261025,20261026,20261027,20261028,20261029,20261030,20261031,20261032,20261033,20261034,20261035,20261036,20261037,20261038,20261039,20261040,20261041,20261042 --minutes 64`.

B7 and B8 are runnable separately with `ai --check-b7` and `ai --check-b8`. Each uses 32 seeds × 2 mirrored seatings at a 64-minute maximum horizon (chapter-end scoring). B7 compares Settler difficulty against Council; B8 compares the same scripted Grower bot with the default Council Decree policy enabled against that bot with only its Decree policy disabled. Both commands accept `--seeds`, `--minutes` and `--first-seed`; any sample below 32 seeds or 64 minutes is labelled DIAGNOSTIC ONLY and cannot pass.

For B17, T11 settlements, T14 claimed land and T15 paired-border contact are sampled separately at each civilization's own KEY-4; a civilization without KEY-4 is reported as incomplete. Town (≥ 75%) and City (tier ≥ 90 in ≥ 25%) prevalence is sampled at chapter end, as specified in [07d §2 F4 and §11](07d-e3-balance-findings.md). For B19, the live target is cohesion 60–80 with 0.03–0.08 secessions per civ per E3. These values supersede the older targets still present in earlier draft references.

The previous T15 report counted each neighbor pair once at the later civilization's KEY-4. That did not match the per-civilization sampling rule above; those border counts are historical diagnostics and are superseded by the corrected per-civ measure.

`ai --check-m10` covers A1–A5/A7 and B27–B31; A6 uses `ai --exploit` and A8 uses the fog-fairness suite, so the combined B26 gate remains open until those separate checks pass. A6 screens 10,000 distinct policies at the configured short `--screen-minutes` horizon (default 2), then validates a 16-policy diverse finalist set on fresh mirrored matches at `--minutes 64`. The screen is for selection, not full-horizon acceptance evidence. A PASS means no validated finalist in that searched candidate set exceeded 55%; it is not a universal proof over all possible policies. The output reports candidate/finalist counts and deterministic screen and validation seed ranges; incomplete validation cannot pass.
An exact-budget invocation is `ai --exploit --generations 417 --children 24 --seeds 1 --screen-minutes 1 --minutes 64` (10,009 distinct candidates; 96 chapter matches for finalist validation). The classifier accepts any positive screen horizon below 64 minutes; the one-minute screen preserves full 64-minute finalist validation and is faster than the two-minute default.
The full-sample command is `dotnet run --project src/CivSim.Harness -c Release -- ai --pack civsim.pack --check-m10 --seeds 8 --minutes 64`; omitting either sample dimension defaults that dimension to a quick diagnostic value (1 seed or 2 minutes).

**Acceptance status (2026-10-25):** S1 completed 1,000/1,000 64-minute replay matches with all checksums matched (53:20). The initial S15 sweep completed 999/1,000 clean in 2:17:23; match 329 (seed 20261348) exposed a validator error for a physically assigned weir retained after settlement capture by a civilization without HNT-09. Snapshot validation now checks unique physical placement while gameplay still requires HNT-09 to use a weir. The exact seed completed its full 64-minute horizon with all five-season snapshot restores and checksum comparisons passing. The post-fix full 1,000-match S15 rerun completed 1,000/1,000 with no failures, satisfying B24. A separate short attempt stopped after seven matches and did not replace that completed result. WP11 and overall M10 acceptance remain open.

**Follow-up evidence (2026-10-25):** B7 passed its exact 32-seed, 64-minute check: Council vs Settler produced a 79.7% engaged/enabled win rate against a 65% target. B14 passed its 10,000-seed world-generation audit (10,000 accepted, zero failures; the slowest seed retimed alone at 3.94 s). B21 passed the default 24-match, 64-minute check (24/24). B22's single-seed Bronze golden was refreshed and passed two fresh identical replays; cross-platform S2 remains unverified. B25 strict validation passed with 509 projects and 10 keystones across 33 lineages, zero errors and warnings. The Release test suite passed 686/686. The exact B8 run failed: Decrees-on won 50.0% of 64 mirrored matches against a 60% target. The 24-seed politics run remains below target for B17, B19 and B20; B10's two-seed diagnostic missed its adult-hazard and epidemic-share bands and is not full acceptance evidence. A6 passed: 10,009 distinct policies screened at one minute; 16/16 finalists validated on three fresh mirrored 64-minute seed pairs; the highest confirmed win rate was 50%, below the 55% ceiling (24:39 total). Candidate generation now limits mutation retries and falls back to deterministic random candidates when the current policy's local mutation neighborhood is exhausted. The broader B26 `ai --check-m10` sample did not finish; its partial results showed A1, A3 and A4 misses, so B26 remains open. Human gates B1–B3 also remain outstanding. These open results do not change the completed S15 result above.

**B19 oracle correction (2026-10-26):** `cohesion_sim.py` now applies capture after the season's cohesion update, starts its first cohesion/term-recovery step the following season, and matches the live minimum-one-point integer move toward target. It still does not treat capture as an automatic raid and counts secessions only within the 52-season KEY-3→KEY-4 exposure. The corrected 30-run, 32-layout R7 check gives 0.002 secessions per civ at 0.10 captures per civ, 0.027 with one capture per civ, and 0.013 with 0.15 captures per civ plus the stress rates. The deterministic semantics check and the focused live capture fixture pass. The live R7 constants remain unchanged; B19 stays open because the modeled joint rate is below target at measured B20 capture incidence.

**Bounded follow-up (2026-10-26):** the prior 24-seed politics run recorded 0.05 secessions per civ per completed E3 exposure, cohesion 62.5, B20 captures in 25% of matches, and only 84/96 KEY-4 exposures completed. Its B19 sample is incomplete; its 14/38 border count used the superseded later-pair sampling. A new four-seed B17 screen was stopped after 4:22 without producing a checkpoint, so it adds no result. The supplemental one-seed A4 duel was stopped after 1:35 without output; the earlier one-seed result remains diagnostic only. No acceptance gate is closed by these attempts.

**Current-source politics results (2026-10-26):** the bounded 24-seed, 64-minute run passes B18 (Dominion 53.9%, Mastery 31.9%, Firsts 14.2%, Wonders 0%) and B20 (7.5 raids per match, war in 95.8%, capture in 25.0%). B17's completed-KEY-4 measures are in band: 11.8 settlements/civ, 40.9% claimed land and own-KEY-4 border contact in 67/83 completed civ samples (80.7%); Town prevalence is 91.7%. B17 remains open because only 83/96 civs completed KEY-4 by the 64-minute cap and City prevalence was 24.0% against 25%. B19 cohesion mean was 63.7 with 0.01 secessions per civ per completed exposure and no cascades; it remains below the 0.03–0.08 rate target, and the 83/96 exposure denominator is incomplete. A bounded 70-minute seed20261030 diagnostic completed all four KEY-4s by minute 68.0, showing the remaining misses arrive after the Year-64 chapter cap. The chapter-end samples remained at the 64-minute mark. Both tested two-slot Spark variants failed to improve KEY-4 timing and were reverted; the one-slot behavior remains. A targeted seed20261031 trace confirms the rare intended B19 path: a captured town 37 walking tiles from its new capital fell to C3, recorded seven consecutive low seasons, and seceded on the eighth low season. No live cohesion constants were changed.

**Current-source research results (2026-10-26):** the exact B5 two-seed Council sample passed. KEY-4 median was 58.8 minutes, E3 duration median 19.0 minutes, and all eight civilizations were within the 14.81–24.69-minute E3 band. Both matches ended by RivalCountdown at minutes 59.8 and 62.5 and replayed identically. B6 passed its exact 32-seed rule/cap gate: median chapter end minute 61.2, 3/32 Year-Cap endings (9.4% against a ≤10% limit), and all replays matched.

**S13–S14 performance results (2026-10-26):** S13 passed the exact 10-minute tick sample after reaching 2,430 figures at simulated minute 60. The 6,000-tick mean was 1.244 ms, the largest tick was 212.273 ms, and the largest GC pause in a tick was 4 ms; sampled population ranged from 2,707 to 4,459. S14 passed the full 24-case Bronze matrix at 1,600 figures across Z0–Z2 and all eight overlays, with four-second samples after one-second warm-ups; minimum one-second FPS was 92.8 (Z1 Claims). B23 is complete.

---

## 17. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| **Scope:** 66 nodes, a metal chain, Cohesion, sieges, compacts, Decrees and the AI at once | M10 slips or ships shallow | Staged sub-milestones (17 §3: 10a–10f) with their own gates; the content spec (WP1) before code; the cut list below |
| The live map fills 2–3× faster than 07b (§1.4) | E4–E7 assumptions (map fill in E7, Dominion around minute 120) are wrong | Re-anchor territory_sim on live trajectories in WP2; record it as a finding for the E4 design |
| Decrees feel like micromanagement or a mandatory chore | P1 erodes; P8 inverts | Rare by construction (5–8 per era), the AI uses them, Council-only players still play (B7 checks both ways), forecast cards make each one a decision |
| Tin snowball: one civ holds the middle field and the isles | Runaway | Arsenic guarantee, embargo coalitions, raidable caravans, Tin Folk, tin durability (not rung) as the edge; T20 and A5 watch it |
| Walls freeze the map (nothing can be taken) | No conquest, no Dominion contest | Investment rule, soft targets, F5 kept for palisades and hillforts, T23 |
| Cohesion causes secession cascades | Frustrating losses | Effects live only from KEY-3, the quarter-step smoothing, 8-season secession timer, T25 cascade check |
| The AI's raid calibration gap (believed 45–60% wins 15–25%, 14 §16.3a) widens with walls and shields | Suicide raids | Fix the calibration in WP9 before tuning; B30 |
| Worldgen golden churn from the tin rules | Every golden moves | One deliberate refresh in WP4, explained in its commit, before any other M10 golden change |
| Live and oracle diverge on population (−35%) and settlements (+2–4×) | Targets fit neither | T10–T11 set on the live sim; the oracle gets a live mode (§15.2) |
| Legacy rescale moves every A gate | Re-tuning cost | Done once, at the start of WP5, then A gates re-measured |
| Art for ~16 structures, chariots, carts, sails | Presentation lags | Placeholders first (17 §6); D19 still open |
| Performance: routes, camps, caravans, sieges | Tick budget | Season-cadence systems only; arrays and ascending ids; B23 |

**Cut list for M10** (drop first to last): HNT-10 oil fuel (keep the hunt), WOD-06/CER-05/CER-06 as pure luxury goods with no bespoke art, chariot travel bonus, the Invite compact, border treaties' line freezing (keep NAPs), Tin Folk (fall back to unclaimed far placers). **Never cut:** the metal chain with dormancy, tin geography, walls and investment, Cohesion from KEY-3, Mandate and the six Decrees, the chapter end rule, the AI's tin and defense logic, and the forecast card.

---

## 18. Change list for other documents
For whoever owns each document; this design only edits 17, 01 and the README.

| # | Doc § | Change |
|---|---|---|
| 1 | 02 §8.3 | Current EraBase E2/E3 **150/400** (07c §10.1); earlier E3 fits are historical |
| 2 | 02 §14 | Firsts: `3 × (era + 1)`, ×4 Keystones and Wonders; Mastery `2 × (era + 1)`; built Wonder `25 × (era + 1)` (§3.3) |
| 3 | 02 §12 | Chop rung 5 Copper axe (MET-03), rung 6 Bronze axe (MET-04 or MET-05): unchanged; note tin's durability edge |
| 4 | 05 §9.5, §10, §12.2 | Game-scale tiers (§8.1); Cohesion model and Admin span (§8.2–8.3); diplomacy: Compacts, Embargo, Pacts, Treaties (§6) |
| 5 | 09 §9 | Roles by structure from E3 (§8.4) |
| 6 | 10 | E3 action and goods registry (§5.3–5.4, §9) in WP1's doc 23 |
| 7 | 11 | E3 counterpart: doc 23 (WP1) |
| 8 | 12 §8, §10, §11.1 | Tin, arsenic, copper and iron rules and guarantees (§5.1); fairness additions |
| 9 | 13 §6.2, §7.1, §8, §9, §12.3 | E3 weapons, shields, chariots, hillforts, walls, investment, soft targets, Free Folk claims, Tin Folk, secession (§7, §11) |
| 10 | 14 §3.2, §4, §6, §7, §10.4, §13 | §13 of this document; add an "E3 playbook" to 14 §13 |
| 11 | 18 §3, §6, §7 | Frame fields, events, commands 18–19 (§4.5, §14) |
| 12 | 20 | Decrees UI, Compacts in the Diplomacy drawer, chapter countdown, E3 research column (§14) |
| 13 | 03a–c, 04 §5.1 | Mark E3 rows as superseded by doc 23's numbers once WP1 lands |
| 14 | 06 §2 | D6 and D7 marked overridden by this document |

---

## 19. Open questions
1. **Neutral peoples at world generation.** Tin Folk appear at the first KEY-3 to keep goldens stable. Should neutral peoples instead be seeded at match start (more "where to explore" in E1–E2, but every golden moves)? Recommended for the E4 design pass.
2. **The live map fills much faster than 07b.** E3 embraces it, but it moves the E4–E7 plan (map fill, Dominion around minute 120, the late per-settlement caps). Is the faster fill the game you want, or should E4+ slow fission (for example a larger minimum daughter)?
3. **Two playtest questions for B1** need the moderator protocol's wording: "Was there a moment you chose *where* your people should go, or *when* to fight? What did you do?" and "Did you ever feel you had to do something you didn't want to?" (the second checks the chore risk).
4. **Final art source (D19)** remains open; M10 adds ~16 structures, three vehicles and new outfits.

---

## 20. Work breakdown

Ordered; **deep** = needs deep design work (opus), **routine** = implementation against this spec (sonnet-class). Each package ends with its gates green.

| WP | Package | Contents | Depth | Depends on | Gates |
|---|---|---|---|---|---|
| **WP1** | **E3 content spec** (doc 23, the E3 counterpart of 10 + 11) | Every number of §12 as typed effects; E3 registries (≈ 30 goods, 3 tool tiers and 2 arms kits, ≈ 16 structures, 10 roles, new actions and practice splits); Moments and flavor for 66 nodes; headline Moments (~10) | deep | this doc | review against §12, §15 |
| **WP2** | **Balance oracle extensions** | §15.2 items 1–7; produce T-values and the EraBase[3] fit | deep (metal_sim, cohesion_sim, territory live mode); routine (combat_e3, legacy_scale, check_e3 ports) | WP1 draft | T-table reproduced |
| **WP3** | **Content YAML and validator** | Transcribe WP1 into `content/`; new effect kinds (`admin`, `mandate`, `sanitation`, `defense`, `tier` uses); validator rules for `[Has]` metal gates and decree levers; `build-pack --write-golden` | routine | WP1 | B25 |
| **WP4** | **World generation** | Tin, arsenic, copper and iron rules and guarantees; fairness additions; one golden refresh, explained | routine | §5.1 | B14 |
| **WP5** | **Research, scoring and the chapter** | `MaxEra` 3; E3 states, KEY-4 and the Iron threshold; Legacy units; chapter end rule (game and harness); Mandate state and recharge; Council for E3 | routine | WP3 | B4, B5, B6 |
| **WP6** | **Economy: metal and fuel** | Mining jobs and mining camps; charcoal, smelting, alloying, casting; tool tiers T8–T10 and the ladders; forest stock and clearing; per-settlement access and dormancy | routine (spec complete), reviewed against metal_sim | WP3, WP4 | B11, B12, B13 |
| **WP7** | **Economy: food, settlements and the state** | Ard, canals, fallow, dairying, horses, salt, bakeries; tiers, specialization, roles by structure; **Cohesion live, Admin span, Overreach, secession**; E3 epidemics and plague on routes | deep (Cohesion tuning); routine (the rest) | WP2, WP3 | B9, B10, B19 |
| **WP8** | **Politics and combat** | E3 weapons, shields, chariots, hillforts, walls, **investment**; soft-target raids; goods barter, caravans, **Compacts and Embargo**, pacts, treaties; Free Folk claims, **Tin Folk**, re-absorption; **Decree execution** and commands 18–19 | deep (decree framework, compacts, siege); routine (tables) | WP6, WP7 | B15, B16, B17, B20, B21 |
| **WP9** | **AI** | Forecast E3 (§13.1), Spark's bronze fork, Strategist plans (Secure tin, sieges, Decrees by EV, coalition embargo), belief evidence, raid calibration fix, A5 re-fit, new blunder detectors, `ai --check-m10` | deep | WP5–WP8 | B26–B31 |
| **WP10** | **View layer** | Frame fields and events, figure kits, ~16 structures, four overlays, the E3 research column, Decrees UI and forecast card, Compacts in the drawer, chapter countdown and end screen, Bronze coach tips | routine; the Decrees and forecast-card UX pass is deep | WP5–WP8 (frame), WP9 (why log) | B23 render |
| **WP11** | **Tests, goldens and the nightly report** | B4–B31 automated in the harness; `bronze-v1` golden; a replay corpus (a siege, an embargo, a secession, a Tin Folk capture); performance runs | routine | all | B22, B23, B24 |
| **WP12** | **Balance pass and playtest** | Tune to §15 with the oracle and the live sim; run B1–B3 with 5–8 first-time players; feed the E4 design | deep (balance); human | WP1–WP11 | B1–B3 |
