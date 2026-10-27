# 13 — Contact and Conflict (WS-F, detailed for E0–E2)

> The rules for meeting other peoples, trading with them, raiding them and taking their settlements. Numbers are detailed for Dawn, Hunters and Roots. The **capture rules** (§8) apply to every era, because the Dominion victory needs conquest (D13).
> This document is the *rules*. **When** a civilization chooses to raid, ally or go to war is the AI's job ([14](14-ai-opponents.md)). Both the player's civilization and the AI's run those decisions through the same Civ Mind, steered by Posture and Beacons.
> Inputs: [05 §12](05-simulation-structure.md#12-contact-diplomacy-and-conflict), [07b](07b-territory-findings.md) T1 and R10, [10](10-e0-e2-actions-and-goods.md), [12 §9](12-world-generation.md#9-start-placement).
> Rules for the slice; **§12 records how M5 built them and where it chose between readings.**

---

## 1. Why this matters early
Starts come in **neighbour pairs ~25 tiles apart** ([12 §9.3](12-world-generation.md#93-spacing-neighbour-pairs)). Your first rival is met around **minute 18** (late E1–E2), when bands are 40–110 people, raiding (WAR-02) is available, and palisades (DEF-03) and warrior bands (WAR-04) are one era away. **The first contact sets the tone of the match**: trade partner, uneasy neighbour, or prey.

## 2. Reach, scouting and awareness

### 2.1 Sight
- Figure sight radius: **3 tiles** (hills +1, mountains +2, forest −1). Lookout posts (DEF-02) +3. Night: −2 unless near fire.
- **Fog of war**: tiles never seen are black; seen-but-not-visible tiles show the **last seen** state (terrain, structures, claims as they were).

### 2.2 Routine reach (where your people go)
Figures range up to **Reach** tiles from the nearest own settlement while foraging, hunting, gathering stone and scouting:

| Era | E0 | E1 | E2 | E3 | E4 | E5 | E6 | E7 | E8 | E9 |
|-----|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Base reach (tiles) | 6 | 9 | 12 | 15 | 20 | 25 | 35 | 50 | 80 | 160 |

Reach grows **project by project**, not just by era: WAY-01 +50% for scouts, WAY-02 +1, WAY-03 +1, TRN-02 +1, SEA-02/03/04 extend it over water. The 07b model needs **≥ 12 tiles of routine range by E2** for paired starts to meet on time ([07b](07b-territory-findings.md) R10).

### 2.3 Scouts
- The Civ Mind keeps **1 scout per settlement** from the start (an adult with the *restless* or *curious* trait if available). From WAY-01 it keeps 2 scouts, and from WAY-03 scouts may range to **2× reach**.
- The simulation chooses each scout's reachable unexplored habitable target by **frontier value** (unknown land × distance cost), with a bias toward **Beacons** (05 §11.4). Scouts follow a least-cost route out and home over one or two seasons. Their sight-3 path updates map knowledge as they travel; when it reaches an occupied rival settlement, the civilizations become Aware and make first contact immediately.

### 2.4 Awareness and contact
| State | Trigger | What you learn |
|-------|---------|----------------|
| **Aware** | One of your figures sees a figure, structure or claim of another civilization; seeing an occupied settlement also makes the observer Aware | Their existence, color and the seen location; their visible **tool tier and outfits** (a hint of their research, §2.5) |
| **Contacted** | A scout's or exploration party's sight reaches an occupied rival settlement; the seasonal routine-reach check also brings nearby civilizations into contact. Gifts, trade, or touching owned tiles are contact triggers in the design | Their name, approximate population band (±25%), and **Glimpse** of every project they've completed (02 §14). Diffusion and `[Seen]`-by-contact start |
| **Neighbours** | Owned tiles adjacent | Border pressure; Relations drift (§3.3) |

### 2.5 What can be inferred by sight (no cheating)
Neither side ever reads the other's state directly. Each side infers from what its figures **see**:
- **Tool tier** from the tool sprites figures carry (T1–T7, copper).
- **Structures**: pit houses, longhouses, palisades, kilns, granaries.
- **Warriors**: painted figures of the Warrior role (WAR-04).
- **Herds and fields.**
- **Chronicle announcements** of Firsts and Keystones are **public** (02 §14), so every civilization knows when a rival reaches a new era first.
The AI uses exactly this information ([14 §5](14-ai-opponents.md#5-reading-the-opponent)).

## 3. First contact and relations

### 3.1 First-contact roll
When two civilizations first become Contacted, each side's meeting party resolves its **stance**:

| Stance | Base | Modifiers |
|--------|-----:|-----------|
| Friendly | 25% | +30% DIP-01 Stranger Greetings, +20% TRD-01 Gift Exchange (if the party carries gifts), +15% COM-02, +10% per *nurturing* trait in the party, +15% Posture Prosper |
| Wary | 45% | absorbs the remainder |
| Hostile | 30% | −50% relative with DIP-01, +20% Posture Raid, +15% if the party is hungry (late winter, FS < 1), +10% per *brave* trait, +10% if the other side is much weaker (< 50% of party size) |

- **Friendly:** Relations start at +20; gifts exchanged (if TRD-01) and diffusion starts.
- **Wary:** Relations 0; no violence; parties withdraw.
- **Hostile:** Relations −20; a **skirmish** (§7) between the meeting parties; the Chronicle names the first blood.
- The roll is **per side**: the more hostile stance wins for violence, the friendlier for Relations if the other side survives without loss.
- The **AI may override** the roll by intent (14 §7): a smart AI that *wants* a peaceful neighbour sends gift-carrying parties; one that plans to raid avoids revealing itself.

### 3.2 Relations score (−100 … +100)
| Event | Delta |
|-------|------:|
| Gift received | +10 (diminishing: −2 per gift in the same year) |
| Peaceful encounter (DIP-02) | +5 |
| Barter completed | +3 per trip |
| Shared enemy (both raided by the same third party within 2 years) | +15 |
| Alliance (DIP-03) | floor +40 while it lasts |
| Your figures inside their claimed land (per season, per 5 figure-seasons) | −1 |
| Claim overlap (contested tiles between you) | −2 per season per 5 contested tiles |
| Raid on them | −30 (−50 if deaths) |
| Settlement captured | −70 |
| Broken alliance or treaty | −60, and −20 with every other civilization that knows (reputation) |
| Yearly drift toward 0 | ±5 |

Relations thresholds (05 §12.2): Peaceful > 0; Alliance needs > 40 (and DIP-03); Hostile < −30.

### 3.3 Reputation
Each civilization has a public **reputation** (−100 … +100) from its record: broken pacts −20, raids on peaceful neighbours −10, gifts and honored alliances +5. Reputation shifts every other civilization's first-contact roll (±1% per 4 points) and their willingness to ally (14 §7).

## 4. Gifts, barter and exchange

### 4.1 Gifts (TRD-01)
- A **gift party** (2–4 figures) carries luxuries (beads, figurines, carvings) or food worth **≥ 5 barter value** to a rival's settlement or border.
- Effects: Relations +10; first-contact friendliness +20% if carried at first contact; makes a rival's `[Seen]` gates trigger for the gifted goods.
- Cost: the goods, plus the party's time (2–6 seasons round trip at E1 distances).

### 4.2 Barter (TRD-02)
- Requires Peaceful relations and TRD-02 on **both** sides.
- A **trade trip**: a party carries goods to a meeting point (the border, or the partner's nearest settlement), exchanges them, and returns.
- **Exchange rate:** each good's value to a civilization = base barter value ([10 §10](10-e0-e2-actions-and-goods.md#10-goods-registry-e0e2)) × local scarcity, where scarcity = clamp(need ÷ stock, 0.5, 3). A trade happens if both sides gain ≥ 10% in value. Both sides gain, which is why trade raises Relations.
- **Volume:** limited by carry (party size × carry per figure, [10 §7](10-e0-e2-actions-and-goods.md#7-move-and-haul)).
- **What it unlocks:** traded goods satisfy `[Seen]` gates for those goods (copper, salt, silk; a creature gate such as wolves or horses still needs a sighting) and `[Has]` access gates while the flow lasts.

### 4.3 Exchange networks (TRD-03)
- Trade becomes a **standing route**: the Civ Mind schedules trips each season automatically while both sides benefit.
- Routes carry **diffusion** (02 §14) and **disease** (09 §10).
- A route through a third civilization's land needs Peaceful relations with them too; routes break at war.

## 5. Raids (WAR-02 and later)

### 5.1 What a raid is
A short, violent expedition to **steal stores, destroy structures and kill or scatter defenders**, then go home. Raids do **not** capture settlements; that is war (§8). Raids are possible from **E1 (WAR-02)**, and they are the main conflict of E1–E2.

### 5.2 Raid party
- **Size:** 15–30% of the raiding settlement's adults (min 4). With WAR-04 Warriors: warriors first, plus volunteers.
- **Who goes:** adults without the *nurturing* trait first; never the last fire-keeper or the only healer.
- **Supplies:** 1 season of food per raider (or the raid must succeed to eat).

### 5.3 Approach and surprise
- The party travels by the least-seen path (avoids the target's known lookouts and trails).
- **Surprise check** when within 3 tiles of the target: base 60% surprise. Modifiers: target COM-01 alarm calls −20%, HUS-01 dogs −50% (relative), DEF-02 watchers −70% (relative), COM-04 signal fires (alerts nearby settlements of the target), night +15%, target's last raid within 1 year −20% (vigilance).
- **Surprised defenders** fight at 0.5 strength for the first engagement round.

### 5.4 Outcomes
- **Stores:** raiders carry off up to their carry capacity, preferring (in order) preserved food, luxuries, tools, fresh food.
- **Structures:** each raid round, raiders burn 1 flammable structure (tent, lean-to, palisade segment, granary) with 50% chance. Mudbrick (BLD-05) and stone don't burn.
- **Casualties:** per §7. Children and elders flee to the nearest hiding place (refuge tile 3–5 tiles away); if the raid catches them in the open they can be killed. **No captives are taken in the MVP** (design choice).
- **Cohesion:** target −10 (−5 with BEL-01 burials); raider +5 on success, −10 on failure.
- **Relations:** −30 (−50 with deaths); reputation −10 if the target was Peaceful.
- **Claims:** raiders exert **presence** on tiles they cross (capped, 05 §11.1) and burn **markers** (TER-03), which weakens the target's claim for a season.

### 5.5 Expected results (tuning targets)
For a warband of 8 attacking a band of 40 (≈ 22 adults) in E1–E2:

| Target defenses | Raid success (stores taken) | Raider deaths | Defender deaths |
|-----------------|----------------------------:|--------------:|----------------:|
| None, surprised | 75% | 5–10% | 8–15% |
| Thorn barrier + watchers + dogs | 40% | 15–25% | 5–10% |
| Palisade (DEF-03) + watchers | 15% | 25–40% | 2–5% |
| Palisade + warriors (WAR-04) | 5% | 40%+ | 1–3% |

A raid on an undefended neighbour pays; a raid on a prepared one is a costly mistake. **Reading the target before raiding is what separates a smart AI from a dumb one** (14 §6).

## 6. Defense

### 6.1 Defender mobilization
On a detected threat, adults within 2 tiles fight; others run home if within 1 season's travel. **Non-warrior adults fight at 0.6 strength**; warriors (WAR-04) at 1.0 × training. Elders, children and pregnant women hide.

### 6.2 Defense multipliers (stack multiplicatively)
| Defense | Multiplier on defenders' strength | Other effect |
|---------|-----------------------------------:|--------------|
| Thorn barrier (DEF-01) | ×1.3 | Raiders' entry speed ×0.5 |
| Archery behind barriers (WAR-03) | ×2 for ranged defenders | |
| Palisade (DEF-03) | ×2.0 | Raiders can't enter without burning a segment (1 round) |
| Lookouts (DEF-02) | — | Surprise −70% (relative) |
| Signal fires (COM-04) | — | Settlements within 20 tiles alerted; reinforcements dispatched |
| Hill or river-bend site | ×1.2 | |
| Home ground | ×1.1 | |

## 7. Combat resolution (E0–E2 numbers)

### 7.1 Strength per figure
`Strength = Base × Weapon × Training × Morale × Health × Defense`

| Factor | Values |
|--------|--------|
| Base | Adult 1.0 · elder 0.5 · non-warrior defender 0.6 |
| Weapon (best the figure has) | Bare 0.6 · club or thrown stones (WAR-01) 1.0 · wooden spear 1.3 · stone-tipped spear 1.6 · atlatl 1.7 (ranged) · bow 1.8 (ranged; ×1.5 with WAR-03) |
| Training | Warrior (WAR-04) ×1.3; Legend warrior ×1.5 |
| Morale | 1.0 → falls with losses; see §7.3 |
| Health | 0.3–1.0 |
| Defense | §6.2 (defenders only) |

### 7.2 Engagement rounds
Every 10 ticks, opposing groups within 1 tile fight a round:
```
Casualties_on_A = k × (Strength_B)² / (Strength_A) × (ranged first-round bonus)
k = 0.06 per round (E0–E2)
```
Casualties are split between **deaths** (40%) and **wounded** (60%, out of the fight, MED-02 improves survival). Ranged weapons fire one free round before melee when the target is in the open.

### 7.3 Morale and retreat
- A group **breaks** when it has lost **40%** of its starting strength (raiders: 30% if no loot yet). Broken groups retreat toward home, taking 50% of the casualty rate if pursued for 1 round.
- Legends in the group: break threshold +10 points. BEL-03 shaman present: +5.
- Defenders protecting a settlement with children inside break at 60% instead.

## 8. War and the capture of settlements (all eras)

### 8.1 War
- **War** is a declared state between two civilizations (no gifts, no trade, routes cut). Raids become free of reputation cost against an enemy at war.
- The decision to declare is the Civ Mind's ([14 §6](14-ai-opponents.md#6-war-raid-and-defense-intelligence)), allowed when Relations < −30, or under Posture Raid, or when an ally is attacked.
- **Peace:** either side can offer it; acceptance is an AI decision (14 §7). A peace sets Relations to −10 and a **truce of 4 years** (breaking it is a reputation −40 event).

### 8.2 Capturing a settlement
A settlement is **captured** when, after a battle at the settlement:
1. its defenders are broken or dead, **and**
2. the attacker holds the settlement tile with ≥ 4 fighters for **one full season** (a camp or hamlet) or **two seasons** (a village or larger), while no relief force breaks the occupiers.

### 8.3 What happens on capture
| Share of population | Fate | Notes |
|---------------------|------|-------|
| **Assimilated** | Stays and joins the captor | 50% base; +10% with GOV-03 (captor), +10% with BEL-08 (E4+), +10% if Relations were > −30 before the war; −20% if the captor raided this settlement within 2 years |
| **Fled** | Walks to the loser's nearest settlement | The rest of the survivors; if none within reach, they become **Free Folk** (a new neutral camp) |
| **Killed** | Battle deaths only (§7) | No massacre mechanic in the MVP |

- The settlement, its structures, stores (minus losses) and **all claim emission** switch to the captor at once. Owned tiles flip once the claim rule allows (05 §11.2 hysteresis), normally within a season.
- **Assimilated people** have Cohesion −20 that recovers +2 per season (faster with GOV, BEL, ART projects). Until it recovers above 40, they are fission-prone and secede easily (05 §10.2). Holding conquered land takes investment.
- **Razing instead:** an attacker may burn a camp or hamlet instead of holding it (no occupation needed). Everyone flees; the land becomes unclaimed. Useful for denial, never for Dominion.

### 8.4 Dominion and conquest
Because peaceful expansion plateaus around 30% of the world (07b T2), **capture is the only route to the 50% Dominion victory**. The rules above are tuned so that:
- A stronger civilization (≥ 1.5× field strength) can take a weakly defended village in one season of campaigning.
- Taking a whole neighbour is a multi-year campaign, with Cohesion costs, and other civilizations react (Balance of Power, DIP-10; 14 §7.3).

## 9. Free Folk in E0–E2
- Created by secession (05 §10.2) or by refugees with nowhere to go (§8.3).
- Behavior: a passive band, defends itself at 0.8 strength, barters if Peaceful, never raids. Starts with Relations 0 to everyone, +20 to its parent civilization if it seceded peacefully.
- Re-absorption: a civilization with SOC-03 can invite a Free Folk band within 15 tiles (Relations > 30, costs gifts worth 10 value): it joins as a daughter band. Or it can be captured (§8).

## 10. What the player sees (for 08)
- **Aware:** a new color appears at the fog's edge; Chronicle line: *"Our scouts saw smoke beyond the eastern ridge."*
- **First contact:** a Moment (08 §10). The stance is shown with an icon (open palm, raised hand, spear).
- **Raid incoming** (if detected): a red alert tier (08 §10.1) with the raid's estimated size; the camera can jump.
- **Raid result:** stores lost, structures burned, names of the dead in the Chronicle.
- **Capture:** the border animation (08 §4.1 "flipping"), a Chronicle entry, and a public Legacy note.

## 11. Tuning checks
| # | Check | Target |
|---|-------|--------|
| F1 | First contact, paired starts | minute 15–25 (Standard) on ≥ 90% of maps |
| F2 | First-contact outcome distribution with no DIP/TRD projects | ~25% friendly / 45% wary / 30% hostile |
| F3 | Raid table §5.5 | within ±10 points in the harness (14 §10) |
| F4 | An unprovoked raid's net value to the raider (stores − deaths − Relations cost) | positive vs undefended targets, negative vs palisaded ones |
| F5 | Capturing a defended village with 1.5× strength | succeeds ≥ 60% in one season of campaigning |
| F6 | Conquest of a whole neighbour (E4–E7) | feasible for a determined leader by minute ~110 (07b T2); harness confirms the AI can do it |

## 12. M5 as built (2026-10-19)

Code: `CivSim.Sim/Politics` (`Combat`, `Raids`, `Territory`, `Diplomacy`, `PoliticsSystem`), run once a season after the economy and research by `MatchModel`. Gates: `civsim-harness politics --pack FILE --check-m5` (scheduled workflow) and the unit tests.

### 12.1 Results

| Check | Target | Measured |
|---|---|---|
| **F1** first contact, paired starts | minute 15–25 on ≥ 90% of maps | **24 of 24** seeds, minutes 18.2–23.5 (median ≈ 20, the start of E2) |
| **F2** first-contact outcomes, no DIP/TRD projects | ~25 / 45 / 30% | 25 / 45 / 30 within ±1.5 points over 20,000 rolls (unit test) |
| **F3** raid table (§5.5) | ±10 points | all twelve cells inside (table below) |
| **F4** net value of an unprovoked raid | positive vs undefended, negative vs palisaded | **+16.4** undefended, −3.0 thorn + watchers + dogs, **−13.1** palisade, −18.1 palisade + warriors (rations; a life is 8, the Relations cost 6) |
| **F5** village taken within one season at 1.5× strength | ≥ 60% | **100%** vs thorn barrier and vs palisade (0–10% at equal strength, 100% at 2×) |
| Claimed land at the end of E2 (07b §9.4) | 12.4–13.1% | **13.2%** mean over six seeds at minute 30 (10.6–15.5%) |

Raid table, a warband of 8 (stone-tipped spears, 1.6) against a band of 40 with 22 adults (wooden spears, 1.3), 4,000 raids a row:

| Defenses | Success: target → measured | Raiders hurt | Defenders hurt |
|---|---|---|---|
| none | 75 → **83** | 5–10 → 11 | 8–15 → 20 |
| thorn barrier + watchers + dogs | 40 → **37** | 15–25 → 29 | 5–10 → 12.5 |
| palisade + watchers | 15 → **12** | 25–40 → 42 | 2–5 → 6 |
| palisade + watchers + warriors | 5 → **4.5** | 40+ → 52 | 1–3 → 3 |

### 12.2 Where I chose between readings
1. **The table's "deaths" are casualties (dead and wounded).** Raider "deaths" of 40%+ against a palisade with warriors means the whole warband is lost, which only happens if wounds count; with the 30% break rule the dead alone cannot reach it. Deaths are still 40% of casualties (§7.2). The tests compare casualties.
2. **Defenders break at 40% in a raid and at 60% when children are inside in an assault.** A raid is a grab for stores, so the camp's fighters give way at the usual 40%; the 60% of §7.3 is for those fighting for their families in a siege. With 60% in raids no row of the table can be reproduced.
3. **Mobilization.** Only the defenders who reach the fight count (§6.1): **40%** of the adults when the raid surprises the camp, **52%** when it was warned. Surprise, watchers, dogs and the stacked multipliers do the rest. These two numbers are the fitted constants (`Raids.SurprisedShare`, `AlertShare`).
4. **Field strength for F5 includes defenses** (05 §12.3): the attackers' strength is 1.5 × Σ defenders × defense multiplier, with every adult of the village fighting.
5. **Reach and contact.** Routine reach is the era value (6 / 9 / 12 …); the table already folds in the scouting projects (07b §2), so the +1s of §2.2 are not added again. It is counted in the terrain-weighted tiles the start pairs are placed by (12 §9.3), plus a tenth for trails, from **settlement** to settlement (camps are not scouting bases). The seasonal routine check contacts them at walking distance ≤ 1.1 × (reach(A) + reach(B)) + 2 (aware at +3). An exploration party also establishes contact immediately when its sight reaches an occupied rival settlement; terrain revealed around its destination does not count as sight. Daughter bands refuse sites within 23 weighted tiles of a rival settlement, so routine contact comes from reach and not from a band wandering over; without that, 5 of 24 seeds met in E1.
6. **Camps** follow 07b §9.4: two per settlement once WAY-02 is known (existing settlements included), radius 2, half the band's emission (three quarters with TER-02), none on rival land.

### 12.3 What is a stand-in
- **The Civ Mind is version 0:** a fixed rule set, the same for every civilization, that reads true state. It sends a gift every two years (TRD-01) and a barter trip every year (TRD-02, both sides) while Relations allow; allies at Relations > 40 with DIP-03 on both sides; raids when Posture is Raid, Relations < −30, or a hungry Warlord is not on good terms, only if a rehearsal of 12 raids wins at the archetype's raid margin (high 65%, medium 50%, low 35%) and pays; declares war on Raid posture or a Warlord at Relations < −30 with 1.5× the rival's field strength; makes peace after three years, or after two failed campaigns. M6 replaces it with belief state, forecast and the Strategist; the fog-fairness test (A8) belongs there.
- **Raiders and soldiers keep working** in the economy while their party is on the road; only the dead and wounded leave it. Palisades are assumed built two seasons after DEF-03 (M3 has no palisade structure).
- **Barter** is food for food with a 10% gain each way; the goods that carry sight (clay, copper, salt, tin, gold, coal, silk) pass each side's `[Seen]` knowledge to the other. Gifts are food.
- **Free Folk** are settlements flagged neutral: they make no Insight, claim no land and are left out of Dominion. Secession (Cohesion < 15 for two years) is not built because M3 has no 0–100 Cohesion; people with nowhere to flee after a capture found them. Re-absorption (§9) is not built.
- Not built: presence and the home-range hatch, trespass Relations (−1 per five figure-seasons), shared-enemy Relations (+15), the reputation effect on alliance willingness, razing, player-initiated anything. Traits (nurturing, brave) are not simulated, so their modifiers are zero.
- **Beacons** (ART-05 grants one, Era Boons add more) pull daughter bands toward them (up to +50% on a site's score within 15 tiles); **Posture** needs GOV-02 and Expand/Prosper move the fission threshold −/+20%; Fortify and Prosper never raid. Raid is the only posture that raids unprovoked.
