# 11 — Project Specs for E0–E2 (WS-B, first three eras)

> Every one of the **145 projects** in Dawn, Hunters and Roots (plus KEY-1…KEY-3) with **numeric effects**, **cost**, **practice sources**, the **innovator Moment** and its **Chronicle flavor**. This is the build-ready version of the prose in [03a](03a-research-craft.md), [03b](03b-research-kin.md) and [03c](03c-research-reach.md).
> Rates and rungs referenced here are defined in [10-e0-e2-actions-and-goods.md](10-e0-e2-actions-and-goods.md). Cap and hazard targets come from [09](09-population-and-health.md).
> Design only. No code.

---

## 1. Effect primitives (syntax used in the tables)

| Primitive | Meaning | Example |
|-----------|---------|---------|
| `Action+(a)` | Figures may perform a new action | `Action+(Fish)` |
| `Method(action, rN name, ×m or rate)` | Adds rung *N* to that action's ladder ([10](10-e0-e2-actions-and-goods.md)) | `Method(Chop, r3 Hafted axe, ×3.0)` |
| `Mod(stat, value)` | Modifier on a stat (percent or new value) | `Mod(Carry, 1.0 → 1.3 L)` |
| `Hazard(cause, ×k)` | The **population-average** multiplier on one **cause** of death ([09 §4.2](09-population-and-health.md#42-causes-and-how-catalog-effects-act)): endemic, epidemic, starvation, exposure, injury, predators, violence; or an age band (child, age 0, maternal) | `Hazard(exposure, ×0.93)` |
| `Cap(Food \| Lean \| Housing, ×k)` | The **target** effect on an average settlement's cap from [09 §5.2](09-population-and-health.md#52-the-cap-projects). It is *not* applied directly: it is what the project's real effects should produce, and the check in §5 compares the two. `^p` = take probability | `Cap(Food, ×1.10)` |
| `Cap(Group, N)` | New group-cap ladder step (applied directly) | `Cap(Group, 150)` |
| `Build(S)` / `Good(G)` / `Role(R)` | Unlock a structure, good or role ([10 §6, §10](10-e0-e2-actions-and-goods.md#6-build-structures-available-in-e0e2)) | `Role(Shaman, Insight 12/yr)` |
| `Tier(T)` | New settlement tier ([05 §9.5](05-simulation-structure.md#95-settlement-tiers)) | `Tier(Village)` |
| `Claim(…)` / `Reach(…)` / `Reveal(…)` / `Show(…)` | Territory, scouting range, map knowledge, map display | `Claim(+0.5, radius 3)` |
| `Cohesion(+n)` | Flat Cohesion for affected settlements | `Cohesion(+3)` |
| `KnowHow(…)` / `Adopt(…)` / `Planner(…)` | Research-side and AI-planner effects | `Adopt(p_teach, ×1.5)` |
| `Combat(…)` / `Defense(…)` / `Diplo(…)` / `Relations(…)` | Conflict and diplomacy ([13](13-contact-and-conflict.md)) | `Combat(×1.5)` |
| `Lever(…)` | Player lever | `Lever(Beacon +1)` |
| `Rule(…)` / `Risk(…)` / `Gate(…)` / `Unlock(…)` | Behavior rules, hazards, gates on use, and enabled downstream content | `Rule(no border conflict between your own bands)` |

**Stacking:** multiplicative across projects, additive within a single project's list. `×k` on hazards always multiplies the *current* hazard of that cause.

**How Hazard values are used ([07c](07c-e0-e2-checks.md#5-c3--hazards-by-cause) §5, decision of 2026-10-18).**
- Every `Hazard(…)` value is the effect on the **whole population**, averaged over who the effect actually reaches (adoption, season, being in camp, being treated). The balance model applies these values directly. A per-figure effect is larger. A figure at a fire *feels* much less cold than this number suggests.
- **In the live simulation, deaths come from mechanisms, not from these multipliers:**
  - **Cold (exposure)** comes **only** from each figure's **warmth tiers** ([10 §9](10-e0-e2-actions-and-goods.md#9-warmth-the-cold-rule): fire + shelter + clothing vs. the climate's need). The exposure values here (FIR-01/02, FIB-02/04, BLD-01/03) must **not** be stacked on top. They are the mean-field equivalent that the warmth tiers should produce on average.
  - **Treatment and safety effects** (MED-01/02/03 healers and midwives, GOV-01/03 councils, DEF-01 barriers, HUS-01 dogs, hunting injury rungs) apply per figure through the actions in [10 §4.2 and §8](10-e0-e2-actions-and-goods.md#8-people-actions-heal-teach-ritual-govern-scout).
  - **Diet and storage effects** (FOD, FOR, SOC-02, WAY-02) act through food security and spoilage (09 §4.3).
- Check **C3** (§6) verifies that the mechanisms' population average matches these values, and so 09's death tables, within ±10%.
- **The Neolithic rise:** KEY-2 (Sedentism) multiplies endemic disease by **×1.30**, and **epidemics begin** (09 §10). Settled life, herds and stored grain bring disease, so E2 death rates rise while fertility jumps (09 §4.1).

## 2. Costs

`Cost = EraBase × TypeMult` ([02 §8.3](02-research-structure.md#83-cost-formula)). Current EraBase is E0 = 22, E1 = 50, E2 = 150 ([07c §10.1](07c-e0-e2-checks.md#101-roots-and-bronze-still-finish-too-quickly-2026-10-24)). TypeMult: D/M 1.0 · S 1.2 · R 0.8 · I 1.5 · W 2.0 · K 2.5. Costs here are the **list cost**. In play they are reduced by Diffusion, EraLag and Know-how ([02 §8–9](02-research-structure.md#8-insight-and-cost)).

| Era | Projects | Sum of list costs | Average | Keystone |
|-----|---------:|------------------:|--------:|---------:|
| E0 Dawn | 34 | 826 | 24.3 | 55 |
| E1 Hunters | 59 | 3185 | 54.0 | 125 |
| E2 Roots | 52 | 8925 | 171.6 | 375 |

These are the costs the balance model runs on ([07c](07c-e0-e2-checks.md) §7).

## 3. E0 — Dawn

| ID | Project | Type | Cost | Requires | Effects (primitives) | Practice |
|---|---|---|---:|---|---|---|
| STN-01 | Pebble Choppers | M | 22 | — | Method(Chop, r1 Pebble chopper, ×1.5) · Mod(Butcher.meat, +25%) · Good(G-TOOL1) | knap, chop |
| STN-02 | Hand Axes | M | 22 | STN-01 | Method(Chop, r2 Hand axe, ×2.0) · Mod(Butcher.meat, +20%) · Good(G-TOOL2) | knap, chop |
| STN-03 | Prepared Cores | M | 22 | STN-02 | Mod(Stone per tool, −30%) · Mod(Knap.speed, +10%) · Good(G-TOOL3, stone points) | knap |
| STN-04 | Hafting | M | 22 | STN-03, FIB-01 | Method(Chop, r3 Hafted axe, ×3.0) · Good(G-TOOL4, hafted spear) · Unlock(HNT-03 points) | knap, chop, hunt |
| FIR-01 | Fire Keeping | D | 22 | — | Action+(Keep fire) · Hazard(exposure, ×0.92) · Hazard(predators, ×0.88) · Risk(fire can die, 10.§9) | tend fire |
| FIR-02 | Fire Making | D | 22 | FIR-01, STN-02 | Action+(Make fire) · Risk(fire loss removed) · Hazard(exposure, ×0.95) · Mod(Warmth +1 at any night camp) | tend fire |
| FIR-03 | Hearths | S | 26 | FIR-02 | Build(Hearth) · Mod(Commons Insight, +10% at hearth settlements) · Cohesion(+3) · Mod(Storytelling site) | tend fire, tell stories |
| FIR-04 | Fire-Hardened Points | M | 22 | FIR-01, WOD-01 | Mod(Wooden tool durability, +50%) · Mod(Hunt r1, +10%) | tend fire, hunt |
| WOD-01 | Digging Sticks | D | 22 | STN-01 | Good(G-STICK, wooden club) · Method(Forage, r1 Digging stick, ×1.5) | forage, chop |
| FIB-01 | Cordage | D | 22 | FOR-01 | Good(G-CORD) · Unlock(hafting, nets, tumplines) | fiber |
| FIB-02 | Hide Scraping | D | 22 | STN-02 | Good(G-RAWHIDE, G-WRAP) · Hazard(exposure, ×0.96) · Mod(Carry, +10%) | fiber/hide, butcher |
| BLD-01 | Windbreaks & Lean-tos | S | 26 | WOD-01 | Build(Lean-to: houses 4, warmth +1) · Cap(Housing, ×1.25) · Hazard(exposure, ×0.93) | build |
| FOR-01 | Plant Lore | D | 22 | — | Mod(Forage.yield, +30%) · Mod(Forage.reachable stock, 40% → 52%) · Hazard(endemic, ×0.98: poisonings −80%) · Cap(Food, ×1.10) | forage |
| FOR-02 | Roots & Tubers | M | 22 | FOR-01, WOD-01 | Method(Forage, roots and tubers open) · Mod(Winter forage stock, ×2) · Cap(Lean, ×1.10) | forage |
| HNT-01 | Power Scavenging | M | 22 | — | Method(Scavenge, r0+ Power scavenging, ×1.5) · Cap(Food, ×1.12) | hunt, fight |
| HNT-02 | Wooden Thrusting Spears | M | 22 | HNT-01, WOD-01 | Action+(Hunt, ambush) · Method(Hunt, r1 Wooden spear, 1.6 R/ws) · Cap(Food, ×1.04) | hunt |
| HNT-03 | Stone-Tipped Spears | M | 22 | HNT-02, STN-04 | Method(Hunt, r2 Stone-tipped spear, 2.2 R/ws) · Hazard(injury of hunters, ×0.90) · Cap(Food, ×1.04) | hunt |
| FOD-01 | Cooking | D | 22 | FIR-01 | Mod(Food value of meat, roots, cereals, +30%) · Hazard(endemic, ×0.93: food-borne deaths fall) · Cap(Food, ×1.10) | cook |
| SOC-01 | Kin Bonds | I | 33 | — | Cap(Group, 35) · Hazard(child, ×0.97) · Cohesion(+5) | socialize |
| SOC-02 | Food Sharing | I | 33 | SOC-01 | Hazard(starvation, ×0.92) · Cohesion(+3) · Rule(no figure starves while another camp of yours within reach has food) | socialize, feast |
| BEL-01 | Mourning & Burial | I | 33 | SOC-01 | Action+(Bury) · Mod(Cohesion loss per death, ×0.5) · Claim(+0.3, radius 2 around burial grounds) | ritual |
| ART-01 | Beads & Body Ornament | D | 22 | SOC-01 | Good(G-BEADS) · Cohesion(+2) · Mod(First tradeable good) | craft, socialize |
| ART-02 | Storytelling | I | 33 | ART-01, COM-02 | KnowHow(+20%) · Cohesion(+1 per season at a hearth) | tell stories |
| LRN-01 | Imitation & Demonstration | M | 22 | SOC-01 | Adopt(p_teach, ×1.5) | teach |
| WAY-01 | Landmarks & Trails | M | 22 | — | Reach(scout range +50%) · Mod(Trails form after 20 crossings: cost ×0.8) · Mod(Lost scouts, −90%) | scout |
| TRN-01 | Tumplines & Carry Frames | M | 22 | FIB-01 | Mod(Carry, 1.0 → 1.3 L) | haul |
| SEA-01 | Floats & Rafts | D | 22 | WOD-01, [Near: Water] | Action+(Raft crossing: rivers, lakes, 1-tile straits) · Gate(use: [Near: Water]) | paddle |
| TRD-01 | Gift Exchange | D | 22 | SOC-02 | Action+(Gift exchange) · Diplo(peaceful first contact +40%) · Relations(+10 per gift) | trade |
| COM-01 | Calls & Gestures | D | 22 | — | Mod(Hunt, +10% for parties ≥ 2) · Hazard(predators, ×0.95) · Mod(Raid surprise, −20%) | socialize, scout |
| COM-02 | Spoken Language | I | 33 | COM-01 | Adopt(p_teach, ×2) · Planner(look-ahead 1 season: winter stockpiling) · Unlock(group actions of ≥ 6) | tell stories, teach |
| TER-01 | Home Range | I | 33 | WAY-01 | Mod(Claim decay after a camp moves, ×0.5) · Show(home range hatch) · Presence(capped 0.5 T) | scout, mark |
| DEF-01 | Thorn Barriers | S | 26 | WOD-01 | Build(Thorn barrier) · Hazard(predators, ×0.80) · Mod(Raiders entering camp, speed ×0.5) | build |
| WAR-01 | Clubs & Thrown Stones | M | 22 | WOD-01 | Combat(×1.5 vs predators and raiders) · Good(club) | fight |
| DIP-01 | Stranger Greetings | D | 22 | COM-01 | Diplo(violent first contacts −50%) · Rule(default stance to strangers: wary, not hostile) | meet strangers |
| KEY-1 | Symbolic Mind | K | 55 | COM-02, FIR-02, STN-04, SOC-01 | EraAdvance(E1) · Show(E1 projects) · Rule(Legends begin to appear) · Legacy(Keystone, ×3 if First) | — |

### E0 Moments and flavor
| ID | Moment (the innovator scene) | Chronicle flavor |
|---|---|---|
| STN-01 | An elder cracks a river cobble; the flake opens a bone for marrow | "The stone remembered how to cut." |
| STN-02 | A teardrop axe turns in firelight; the knapper admires its symmetry | "Shaped with patience, the same on both sides." |
| STN-03 | One strike lifts a perfect point from a prepared core | "See the tool in the stone before you strike." |
| STN-04 | Cord binds a point to a shaft; the first spear is lifted | "Stone married to wood." |
| FIR-01 | Embers carried from a lightning-struck tree, cupped in bark | "We did not make it. We kept it." |
| FIR-02 | Sparks from struck stone catch in dry moss | "Now fire lives in our hands." |
| FIR-03 | A ring of stones; faces gather around it at dusk | "The night became a place to talk." |
| FIR-04 | A spear tip is turned slowly over coals until it darkens | "Fire taught the wood to bite." |
| WOD-01 | A sharpened stick pries a tuber from hard ground | "The earth gave up its hidden food." |
| FIB-01 | Bark strips rolled on a thigh into twisted cord | "Three weak strands, one strong." |
| FIB-02 | A hide stretched and scraped clean, then worn | "We borrowed the animal's coat." |
| BLD-01 | Branches leaned against a fallen trunk, roofed with brush | "A wall against the wind." |
| FOR-01 | A child is shown which berries to leave alone | "Learn the plants, or they will teach you." |
| FOR-02 | Tubers dug from frozen ground in the hungry months | "Food that waits underground." |
| HNT-01 | Shouting figures with clubs drive hyenas off a fresh kill | "We took our share from the lions." |
| HNT-02 | Hunters crouch in reeds by a watering hole, spears ready | "Patience, then one thrust." |
| HNT-03 | A stone-tipped spear brings down a deer at the first strike | "Sharper teeth than any wolf." |
| FOD-01 | Meat sizzling on a flat stone by the fire | "Fire chewed our food for us." |
| SOC-01 | An aunt carries a sleeping child while its mother forages | "Every child has many mothers." |
| SOC-02 | A hunter cuts the kill into equal shares | "Meat is shared, or it rots in the heart." |
| BEL-01 | A body laid in a shallow grave, red ochre and a stone tool beside it | "They stay with us in the earth." |
| ART-01 | Shell beads strung on cord, held up to the sun | "This is who we are." |
| ART-02 | An elder acts out a hunt by firelight; children laugh | "The story of the bear kept us from the bear." |
| LRN-01 | A youth copies an elder's knapping, stroke for stroke | "Watch my hands." |
| WAY-01 | A cairn on a ridge; a scout looks back and finds home | "The land became a map in our heads." |
| TRN-01 | A strap across the forehead takes the weight of a bundle | "Carry it with your whole body." |
| SEA-01 | Logs lashed together; a family floats across a river | "The water stopped being a wall." |
| TRD-01 | Beads laid on a rock between two wary groups | "A gift says we come in peace." |
| COM-01 | A sharp whistle; the whole band freezes and looks the same way | "One call, and we moved as one." |
| COM-02 | Two hunters plan a hunt for tomorrow, pointing at the hills | "We could speak of what was not here." |
| TER-01 | Children name every stream and hill around the camp | "This valley knows our names." |
| DEF-01 | A ring of thorny branches pulled around the sleeping camp | "Let the night beasts go hungry." |
| WAR-01 | A thrown stone drives off a prowling wolf | "We were small, but we could throw." |
| DIP-01 | Two strangers, palms open, circling each other slowly | "Stranger, not enemy. Not yet." |
| KEY-1 | The band gathers at the hearth; painted hands appear on a rock wall | "We began to think in symbols." |

## 4. E1 — Hunters

| ID | Project | Type | Cost | Requires | Effects (primitives) | Practice |
|---|---|---|---:|---|---|---|
| STN-05 | Blade Industry | M | 50 | STN-04 | Good(G-TOOL5 blade kit) · Mod(Knap.speed, +40%) · Mod(Tool durability, ×1.5) · Need(flint, else T4 quality) | knap |
| STN-06 | Microliths | M | 50 | STN-05 | Good(G-TOOL6 composite microliths) · Unlock(arrows, sickles) | knap |
| STN-07 | Bone & Antler Tools | D | 50 | STN-05, HNT-03 | Good(G-NEEDLE, harpoon heads, flutes, awls) · Mod(Carcass value, +15%) | knap, butcher |
| FIR-05 | Landscape Burning | D | 50 | FIR-02, FOR-02 | Action+(Controlled burn) · Mod(Burned tile: forage +40%, game +30% for 3 yrs) · Risk(5% wildfire spread per burn) | tend fire, forage |
| FIR-06 | Heat-Treating Stone | M | 50 | FIR-03, STN-05 | Mod(Knap.speed, +30%) · Mod(New tools start at quality 1.3) | knap, tend fire |
| WOD-02 | Woodcarving | M | 50 | WOD-01, STN-05, ART-01 | Good(G-CARVING, bowls, handles) · Mod(Craft.speed, +15%) | craft, chop |
| FIB-03 | Tanning & Leather | M | 50 | FIB-02, FIR-03 | Good(G-LEATHER) · Mod(Tent and bag durability, ×2) | fiber/hide, tend fire |
| FIB-04 | Tailored Clothing | M | 50 | FIB-03, STN-07 | Good(G-TAILORED, warmth +2) · Hazard(exposure, ×0.84) · Unlock(settlement in Cold band) | fiber/hide |
| FIB-05 | Basketry & Netting | D | 50 | FIB-01 | Good(G-BASKET, G-NET) · Method(Forage, r2 Baskets: carry ×1.5) · Unlock(storing nuts and seeds) | fiber |
| CER-01 | Fired Clay Figurines | D | 50 | FIR-03, ART-01, [Seen: Clay] | Good(G-FIGURINE) · Action+(Gather clay) · Cohesion(+2) | clay |
| CER-02 | Pottery | D | 50 | CER-01, FOD-01 | Good(G-POT) · Mod(Spoilage of stored food, −25%) · Unlock(boiling) | clay, tend fire |
| BLD-02 | Hide Tents | S | 60 | BLD-01, FIB-02 | Build(Hide tent: houses 5, packs to 1 L) · Cap(Housing, ×1.2) · Mod(Camp relocation time, ×0.5) | build, hide |
| BLD-03 | Pit House | S | 60 | BLD-02, WOD-02 | Build(Pit house: houses 6, warmth +2) · Cap(Housing, ×1.5) · Hazard(exposure, ×0.88) | build |
| MIN-01 | Flint & Ochre Seeking | D | 50 | STN-05 | Reveal(flint and ochre within sight) · Action+(Collect flint and ochre) · Good(G-FLINT, G-OCHRE) | mine, scout |
| MEC-01 | Lever & Wedge | M | 50 | WOD-02 | Mod(Heavy-object handling, +30%) · Mod(Wood yield, +15% by splitting) | build, chop |
| CHM-01 | Ochre Pigments | D | 50 | FIR-03 | Good(G-PIGMENT) | craft |
| CHM-02 | Birch-Tar Adhesive | M | 50 | FIR-03, STN-04 | Good(G-TAR) · Mod(Hafted tool durability, ×1.4) | craft, tend fire |
| FOR-03 | Nut & Seed Gathering | M | 50 | FOR-02, FIB-05 | Method(Forage with baskets: nuts and seeds storable) · Mod(Autumn forage, +40%) · Cap(Food, ×1.08) · Cap(Lean, ×1.25) | forage |
| FOR-04 | Wild Cereal Harvest | M | 50 | FOR-03, STN-06 | Method(Forage, r3 Wild-grass sickle, ×2.4 on stand tiles) · Cap(Food, ×1.10) | forage |
| FOR-05 | Seed Grinding | M | 50 | FOR-04, STN-05 | Mod(Cereal food value, +25%) · Cap(Food, ×1.04) | forage, cook |
| FOR-06 | Tending Wild Stands | M | 50 | FOR-04, WAY-02 | Method(Forage, r4 Tended stands: stand regrowth ×2) · Show(tended tile state) · Cap(Food, ×1.06) | forage, farm |
| HNT-04 | Cooperative Drives | M | 50 | HNT-03, COM-02 | Method(Hunt, r3 Cooperative drive: party ≥ 6, 3.2 R/ws on large herds) · Mod(Herd fear, ×3) · Cap(Food, ×1.10^0.7) | hunt |
| HNT-05 | Spear-Thrower | M | 50 | HNT-04, WOD-02 | Method(Hunt, r4 Spear-thrower, 2.8 R/ws) · Hazard(injury of hunters, ×0.92) | hunt |
| HNT-06 | Bow & Arrow | M | 50 | HNT-05, STN-06, FIB-01 | Method(Hunt, r5 Bow, 3.0 R/ws, solo; small game prey class) · Hazard(injury of hunters, ×0.92) · Cap(Food, ×1.03) | hunt |
| HNT-07 | Harpoons & Fish Spears | D | 50 | HNT-03, STN-07, [Near: Water] | Action+(Fish) · Method(Fish, r1 Harpoon, 1.6 R/ws) · Gate(use: [Near: Water]) · Cap(Food, ×1.10^0.6) | fish |
| HNT-08 | Hooks, Lines & Nets | M | 50 | HNT-07, FIB-05 | Method(Fish, r2 Hooks, lines and nets, 3.2 R/ws) | fish, fiber |
| HUS-01 | Wolf Companions | D | 50 | HNT-04, [Seen: Wolves] | Good(Dogs: 1 per 4 people, eat 0.3 R/season) · Mod(Hunt, +20%) · Mod(Raid surprise, −50%) · Unlock(dog travois) · Hazard(predators, ×0.92) | hunt, herd |
| HUS-02 | Herd Following | M | 50 | HUS-01, WAY-02 | Method(Herd, r0 Herd following: herd fear decays ×2, drives can't push it off) · Cap(Food, ×1.04) | herd, hunt |
| FOD-02 | Smoking & Drying | M | 50 | FOD-01, FIR-03 | Build(Drying rack) · Good(G-DRIED: spoilage 10%) · Cap(Lean, ×1.15) | preserve |
| FOD-03 | Storage Pits | S | 60 | FOD-02, FOR-03 | Build(Storage pit: 15 R, spoilage ×0.5) · Cap(Lean, ×1.30) | preserve, build |
| FOD-04 | Boiling & Stews | M | 50 | FOD-01, CER-02 | Mod(Food value, +20%) · Hazard(age 0, ×0.96: weaning foods) · Mod(Fertility, +5%) · Cap(Food, ×1.05) | cook |
| MED-01 | Herbal Remedies | D | 50 | FOR-01, COM-02 | Action+(Tend the sick) · Hazard(endemic, ×0.96) | heal, forage |
| MED-02 | Bone Setting & Wound Care | M | 50 | MED-01, FIB-02 | Hazard(injury, ×0.92) | heal |
| SOC-03 | Band Alliances | I | 75 | SOC-02, COM-02 | Cap(Group, 50) · Rule(daughter bands share Insight 100% and stay allied) | socialize |
| SOC-04 | Clans & Totems | I | 75 | SOC-03, BEL-02 | Cap(Group, 150) · Tier(Hamlet) · Cap(Housing, ×1.3) | socialize, ritual |
| GOV-01 | Elders' Council | I | 75 | SOC-02, COM-02 | Role(Elder, Insight 8/yr) · Hazard(violence, ×0.84) | council |
| BEL-02 | Animism & Totems | I | 75 | BEL-01, ART-01 | Cohesion(+3) · Build(Totem) · Claim(+0.3, radius 2 around totems) | ritual |
| BEL-03 | Shamanism | R | 40 | BEL-02, MED-01 | Role(Shaman, Insight 12/yr, heals as MED-01) · Mod(Morale floor in famine: Cohesion never below 25) | ritual, heal |
| ART-03 | Cave Painting | D | 50 | ART-02, CHM-01 | Claim(+0.5, radius 3 around painted sites) · Cohesion(+3) · KnowHow(HNT, +10%) | craft, ritual |
| ART-04 | Music & Dance | D | 50 | ART-02, STN-07 | Cohesion(+3) · Mod(Feast and gathering effect, +25%) | ritual, socialize |
| ART-05 | Songlines | I | 75 | ART-02, WAY-02 | Lever(Beacon +1) · Mod(Travel speed in known land, +30%) | scout, tell stories |
| LRN-02 | Oral Tradition | I | 75 | LRN-01, ART-02 | Mod(Elder Insight, ×1.5) · Rule(Know-how not lost when elders die) | teach, tell stories |
| WAY-02 | Seasonal Round | I | 75 | WAY-01, FOR-02 | Rule(seasonal round: bands move between 2–4 planned camps) · **Claim(2 seasonal camps per settlement founded in E1–E3, placed r+1 to r+3 from it; each radius 2 at 50% of the band's Emit; satellite camps persist)** · Hazard(starvation, ×0.92) · Cap(Food, ×1.05) · Reach(+1) | scout, haul |
| WAY-03 | Star Paths | M | 50 | WAY-02, INQ-02 | Rule(night travel; scouts never get lost) · Reach(+1) | scout |
| TRN-02 | Travois & Sledges | M | 50 | TRN-01, HUS-01 | Mod(Carry, +2 L per dog) · Mod(Camp move speed, ×2) | haul |
| TRN-03 | Snowshoes & Skis | M | 50 | TRN-02, FIB-03, [Climate: Cold] | Mod(Snow tile cost, ×2 → ×1) · Gate(soft: [Climate: Cold] ×0.7 cost) | haul |
| SEA-02 | Bark & Hide Canoes | M | 50 | SEA-01, FIB-03 | Method(Paddle, r1 Canoe: river speed ×2; lake fishing) | paddle, fish |
| TRD-02 | Barter | M | 50 | TRD-01, COM-02, ART-01 | Action+(Barter with other civilizations) · Rule(traded goods satisfy [Seen] gates) | trade |
| COM-03 | Symbols & Marks | D | 50 | COM-02, ART-01 | Claim(+10% on marked tiles) · Mod(Ownership marks on goods) | craft, mark |
| NUM-01 | Tally Marks | D | 50 | COM-02 | Planner(tracks stores: stockpile targets; winter forecast error −50%) | count stores |
| NUM-02 | Lunar Calendar | M | 50 | NUM-01, INQ-02 | Mod(Seasonal yields, +10% from timing) · Planner(migration and harvest dates exact) | count stores, scout |
| TER-02 | Camp Circuits | M | 50 | TER-01, WAY-02 | Claim(seasonal camp emission 50% → 75% of the band's) · Rule(camp claims never decay while the band is elsewhere) | mark, scout |
| TER-03 | Territorial Markers | M | 50 | TER-02, BEL-02 | Build(Marker: cairn or totem, claim +30% within radius 2) | mark |
| DEF-02 | Lookouts | R | 40 | DEF-01, COM-02 | Role(Watcher) · Build(Lookout post: sight +3) · Mod(Raid surprise, −70%) | guard |
| WAR-02 | Raiding | I | 75 | WAR-01, HNT-04 | Action+(Raid: steal stores, burn, capture camps; 13 §5) | fight, hunt |
| WAR-03 | Archery Warfare | M | 50 | WAR-02, HNT-06 | Combat(ranged ×1.5) · Defense(defenders behind barriers ×2) | fight |
| DIP-02 | Hospitality Customs | I | 75 | DIP-01, SOC-03 | Rule(guests welcomed: contact → diffusion begins) · Relations(+5 per peaceful encounter) | meet strangers |
| INQ-01 | Tracking & Animal Lore | M | 50 | HNT-03 | Reveal(herds within 2× sight) · Mod(Hunt, +20%) | hunt, scout |
| INQ-02 | Sky Watching | M | 50 | COM-02 | Rule(season prediction: migrations on time) · Unlock(calendars, star paths) | scout |
| KEY-2 | Sedentism | K | 125 | FOR-04, FOD-03, BLD-03, WAY-02 | EraAdvance(E2) · Rule(year-round camps possible) · Legacy(Keystone) · **Hazard(endemic, ×1.30)** · **Rule(epidemics begin, [09 §10](09-population-and-health.md#10-disease-model-05-13))** | — |

### E1 Moments and flavor
| ID | Moment (the innovator scene) | Chronicle flavor |
|---|---|---|
| STN-05 | Long blades peel off a core like pages | "One stone, many knives." |
| STN-06 | Tiny blades set in a row along a grooved bone | "Small teeth, set together." |
| STN-07 | A needle's eye drilled through a sliver of bone | "Nothing of the animal was wasted." |
| FIR-05 | A line of flame walks across dry grass at dusk | "Burn the old, and the green comes back." |
| FIR-06 | Stones buried under the hearth, then flaked: glossy and sharp | "Fire made the stone obey." |
| WOD-02 | A bowl takes shape under a blade | "Wood holds whatever shape we dream." |
| FIB-03 | Hides hung in smoke over the fire | "Smoke made the skin last." |
| FIB-04 | A fitted parka is sewn with a bone needle | "Now the cold could not find us." |
| FIB-05 | Reeds woven into a basket heaped with hazelnuts | "Carry more, waste less." |
| CER-01 | A small clay figure is fired in the hearth and cracks in the heat | "Earth, made into us." |
| CER-02 | The first pot holds water over the fire without breaking | "A stomach of clay." |
| BLD-02 | Hides stretched over poles; the camp packs up and moves before noon | "Home goes where we go." |
| BLD-03 | A dugout floor roofed with poles and turf, smoke rising from the center | "Half in the earth, warm as a burrow." |
| MIN-01 | A scout pockets a lump of red earth and a dark flint nodule | "The best stone hides where few look." |
| MEC-01 | A long pole pries a boulder from the stream | "A long enough stick moves anything." |
| CHM-01 | Ochre ground on a flat stone into red paste | "Red as blood, red as life." |
| CHM-02 | Birch bark heated under a stone; black tar oozes out | "Glue from the tree's tears." |
| FOR-03 | Baskets of acorns poured into a dry cache | "Autumn's gift, saved for winter." |
| FOR-04 | A sickle sweeps through a golden stand of wild wheat | "The grass gave us grain." |
| FOR-05 | Seeds rubbed between two stones into coarse flour | "Grind it, and it feeds twice." |
| FOR-06 | Weeds pulled from a wild-grain stand; seeds pressed back into the soil | "We began to help the plants." |
| HNT-04 | A line of hunters drives bison toward a cliff edge | "Together we are the cliff." |
| HNT-05 | A dart flies twice as far as any throw | "A longer arm." |
| HNT-06 | A lone hunter draws a bow; a rabbit drops at thirty paces | "The string sings, the meat falls." |
| HNT-07 | A barbed harpoon lifts a salmon from the shallows | "The river feeds those who learn it." |
| HNT-08 | A net comes up heavy and silver | "A hundred fish in one pull." |
| HUS-01 | A wolf pup takes meat from a child's hand | "The wolf chose to stay." |
| HUS-02 | Hunters walk beside a reindeer herd, not chasing it | "We followed, and the herd kept us." |
| FOD-02 | Strips of fish hang over a smoky fire | "Smoke for later." |
| FOD-03 | A lined pit filled with nuts and sealed with stones | "We buried our winter, and it waited." |
| FOD-04 | A stew bubbles in a pot; an infant is fed soft food | "Soft food, strong children." |
| MED-01 | A healer presses chewed leaves onto a fevered brow | "The forest is also medicine." |
| MED-02 | A broken leg splinted with sticks and cord | "Broken, but mended." |
| SOC-03 | Two bands meet; a young couple leaves together | "Our daughters are their mothers." |
| SOC-04 | A totem post carved with the clan's animal | "We are the Heron people." |
| GOV-01 | Elders sit in a circle; a quarrel ends with a nod | "The old ones settle what the young would fight over." |
| BEL-02 | Offerings left at the foot of a great tree | "Everything is alive and watching." |
| BEL-03 | A shaman drums at the edge of the firelight | "One of us walks between worlds." |
| ART-03 | Torchlight on a cave wall where a painted aurochs seems to move | "Paint the herd, and it returns." |
| ART-04 | A bone flute plays; feet stamp in rhythm | "Music made us one body." |
| ART-05 | A wanderer sings the way across the desert | "The song is the path." |
| LRN-02 | An old woman recites the names of forty plants | "When an elder dies, nothing is forgotten." |
| WAY-02 | The band leaves the river camp for the autumn hills, as every year | "We go where the food will be." |
| WAY-03 | A scout steers by a bright star across a dark plain | "The sky is a map." |
| TRN-02 | Dogs drag poles loaded with a whole camp | "The dogs carry our home." |
| TRN-03 | Figures glide down a snowy slope on long boards | "Winter became a road." |
| SEA-02 | A bark canoe slides past a slow-walking family on the bank | "The river carries us." |
| TRD-02 | Flint traded for shell across a river | "Your stone for our shells." |
| COM-03 | A pattern scratched on bone, then on a boundary stone | "Marks that speak when we are gone." |
| NUM-01 | Notches cut in a bone, one for each basket | "We counted, and we knew." |
| NUM-02 | Moon phases scratched on antler | "The moon keeps our calendar." |
| TER-02 | The band returns to last year's camp; its claim never faded | "All these places are ours." |
| TER-03 | A cairn rises on the border hill | "Here begins our land." |
| DEF-02 | A watcher on a rock shouts a warning | "Eyes that never sleep." |
| WAR-02 | Painted warriors slip through the grass at dawn | "What they have, we can take." |
| WAR-03 | Arrows arc over a thorn barrier | "The bow made the few the many." |
| DIP-02 | A stranger is given the warm place by the fire | "A guest is sacred." |
| INQ-01 | A tracker kneels at hoof prints and points east | "The ground tells stories." |
| INQ-02 | An elder watches the first star rise at dawn | "The sky warns us of winter." |
| KEY-2 | A camp that does not move when the leaves fall | "We stayed." |

## 5. E2 — Roots

| ID | Project | Type | Cost | Requires | Effects (primitives) | Practice |
|---|---|---|---:|---|---|---|
| STN-08 | Ground & Polished Stone | M | 150 | STN-06 | Method(Chop, r4 Polished axe, ×4.0) · Mod(Clearing, ×2) · Good(G-POLAXE, prestige) | knap, chop |
| STN-09 | Querns & Grindstones | S | 180 | STN-08, FOR-05 | Build(Quern: cereal food value +30% for 10 people, replaces FOR-05's +25%) | cook, knap |
| STN-10 | Megalith Raising | W | 300 | STN-08, MEC-02, BEL-05 | Wonder(Megalith: 120 ws, stone blocks 60 L, rollers) · Cohesion(+10 within radius 8) · Claim(+1.0, radius 6) · Event(seasonal festival, +Cohesion for all your settlements) | build, ritual |
| FIR-07 | Kilns | S | 180 | FIR-03, CER-02 | Build(Kiln) · Mod(Pottery breakage, 40% → 20%) · Unlock(lime, copper smelting) | tend fire, clay |
| FIR-08 | Charcoal Burning | D | 150 | FIR-07, WOD-03 | Good(G-CHAR) · Role(Collier) | tend fire, chop |
| WOD-03 | Adzes & Gouges | M | 150 | WOD-02, STN-08 | Method(Shape timber: adze) · Mod(Timber building, +20%) · Unlock(planks, dugouts) | chop, build |
| WOD-04 | Coppicing | D | 150 | WOD-03, FOR-06 | Mod(Wood regrowth, ×2 within settlement radius) | chop, forage |
| FIB-06 | Spinning | M | 150 | FIB-05, (AGR-02 \ | HUS-03) | Good(G-YARN) | fiber, herd |
| FIB-07 | Loom Weaving | M | 150 | FIB-06 | Good(G-CLOTH: warmth +1, trade staple) | fiber |
| CER-03 | Storage Jars | M | 150 | CER-02 | Good(G-JAR) · Mod(Storage capacity per storage building, +50%) | clay |
| BLD-04 | Wattle & Daub | M | 150 | BLD-03, FIB-05 | Build(Wattle & daub house: houses 8) · Cap(Housing, ×1.5) | build |
| BLD-05 | Mudbrick | M | 150 | BLD-04, [Seen: Clay] | Good(G-BRICK) · Build(Mudbrick house: houses 8, fire-resistant) · Cap(Housing, ×1.5) | build, clay |
| BLD-06 | Longhouses | S | 180 | BLD-04, SOC-05 | Build(Longhouse: houses 25) · Cap(Housing, ×1.5) · Cohesion(+5) | build |
| MIN-02 | Flint Mines | D | 150 | MIN-01, STN-08 | Action+(Mine flint pits: 3 L/ws) | mine |
| MIN-03 | Quarrying | D | 150 | MIN-02 | Action+(Quarry stone blocks: 2 L/ws) | mine |
| MET-01 | Native Copper | D | 150 | STN-08, [Seen: Native copper] | Action+(Collect native copper) · Good(G-COPORN) · Gate([Seen: Native copper]) | mine, craft |
| MET-02 | Copper Smelting | D | 150 | MET-01, FIR-07, INQ-03 | Good(G-COPPER) · Role(Smith) · Rule(copper awls and ornaments; copper tools come with MET-03) | smelt |
| MEC-02 | Rollers & Sledges | M | 150 | MEC-01, FIB-05 | Unlock(moving blocks and logs at 0.5 speed) · Mod(Heavy haul, +50%) | haul, build |
| NRG-01 | Draft Animals | D | 150 | HUS-04 | Action+(Draft oxen) · Mod(Farm and haul labor, −30%; needs cattle) | herd, farm |
| CHM-03 | Lime Burning | D | 150 | FIR-07, MIN-03 | Good(G-LIME) · Hazard(endemic, ×0.99: lime plaster) | tend fire, build |
| FOR-07 | Orchards | D | 150 | AGR-01, WOD-03 | Build(Orchard: matures in 6 yrs, then 3 R/tile/yr) | farm, forage |
| HNT-09 | Weirs & Fish Traps | S | 180 | HNT-08, WOD-03 | Build(Weir: passive 2.0 R/season, ×3 in salmon runs) · Cap(Food, ×1.08^0.4) | fish, build |
| AGR-01 | Cultivation | D | 150 | FOR-05, FOR-06 | Action+(Farm) · Build(Field) · Method(Farm, r1 Cultivation, 4 R/tile/yr) · Cap(Food, ×1.30) | farm |
| AGR-02 | Domesticated Staples | M | 150 | AGR-01 | Method(Farm, r2 Domesticated staple by biome, 8 R/tile/yr) · Cap(Food, ×1.22) | farm |
| AGR-03 | Legume Pairing | M | 150 | AGR-02 | Mod(Field yield, ×1.08) · Mod(Soil fertility loss, ×0.5) · Cap(Food, ×1.08^0.5) | farm |
| AGR-04 | Slash-and-Burn | M | 150 | AGR-01, STN-08, FIR-05 | Method(Clear, Slash-and-burn: forest 1 ws/tile, fields shift every 4 yrs) | farm, tend fire |
| HUS-03 | Goats & Sheep | D | 150 | HUS-02, [Seen: Wild goats or sheep] | Action+(Herd: tame 0.5 ws per animal; pasture carries 6) · Good(meat 0.8 R, hide 0.2 per animal per yr) · Gate([Seen: Wild goats or sheep]) | herd |
| HUS-04 | Cattle & Pigs | D | 150 | HUS-03, AGR-01 | Action+(Herd cattle and pigs) · Good(cattle 2.5 R + 0.3 hide/yr; pigs 1.2 R, eat waste) · Unlock(NRG-01) | herd |
| FOD-05 | Fermentation | D | 150 | FOD-04, AGR-02 | Good(G-BREW) · Mod(Cereal food value, +15%) · Mod(Feast cost, ×0.7) · Cap(Food, ×1.05) | cook, feast |
| FOD-06 | Granaries | S | 180 | FOD-03, BLD-05 | Build(Granary: 80 R, spoilage ×0.25, pests −50%) · Cap(Food, ×1.05) | preserve, build |
| MED-03 | Midwifery | R | 120 | MED-02, SOC-04 | Role(Midwife) · Hazard(age 0, ×0.93) · Hazard(maternal, ×0.88) | heal |
| SOC-05 | Feasting | I | 225 | SOC-04, FOD-05 | Action+(Feast: 0.5 R per guest, Cohesion +8 for a year) · Rule(feast-givers become Legends) | feast |
| SOC-06 | Village Life | I | 225 | SOC-05, BLD-06 | Tier(Village) · Cap(Group, 500) · Cap(Housing, ×1.5) | socialize, build |
| SOC-07 | Chiefdom | I | 225 | SOC-06, GOV-02 | Cap(Group, 2,000 per polity core) · Rule(daughter villages stay fully in the polity; stores shared within 15 tiles) | council, feast |
| GOV-02 | Chieftainship | I | 225 | GOV-01, SOC-05 | Role(Chief) · Lever(Posture: Expand / Prosper / Fortify / Raid) | council |
| GOV-03 | Customary Law | I | 225 | GOV-02, LRN-02 | Hazard(violence, ×0.84) · Rule(no border conflict between your own bands) | council |
| BEL-04 | Ancestor Veneration | I | 225 | BEL-03, SOC-04 | Claim(×1.5 within radius 4 of ancestral burial grounds) · Rule(settlements near ancestors resist abandonment) | ritual |
| BEL-05 | Sacred Places | S | 180 | BEL-04, ART-03 | Build(Shrine: claim +0.5 in radius 3; pilgrimage) · Cohesion(+3 for all settlements within 20 tiles) | ritual, build |
| LRN-03 | Apprenticeship | I | 225 | LRN-02, SOC-05 | Mod(Role training time, −50%) · Mod(Skill gain, ×1.5) | teach |
| WAY-04 | Sketch Maps | D | 150 | WAY-03, ART-03 | Rule(explored terrain stays revealed for the whole civilization) · Planner(settler site choice +) | scout |
| TRN-04 | Pack Animals | M | 150 | TRN-02, HUS-03 | Mod(Carry, +3 L per pack animal) | haul, herd |
| SEA-03 | Dugout Canoes | M | 150 | SEA-02, WOD-03 | Method(Paddle, r2 Dugout: coastal water within 2 tiles; 4 L) · Mod(Fishing offshore, +50%) | paddle, chop |
| SEA-04 | Outriggers | M | 150 | SEA-03, WAY-03 | Method(Paddle, r3 Outrigger: open-sea voyages within 11 tiles of land; see 21) | paddle |
| TRD-03 | Exchange Networks | I | 225 | TRD-02, WAY-04 | Rule(persistent trade routes; diffusion along routes; luxuries flow) · Mod(Luxury Cohesion, ×1.5) | trade, haul |
| COM-04 | Signal Fires & Drums | M | 150 | COM-03, FIR-03 | Build(Signal post) · Mod(Defense response time, −70%) · Rule(raid warnings reach all settlements within 20 tiles at once) | guard |
| NUM-03 | Clay Tokens | D | 150 | NUM-02, CER-02 | Mod(Storage losses to mismanagement, −50%) · Rule(trade debts tracked: barter over several seasons) | count stores, trade |
| TER-04 | Permanent Villages | I | 225 | TER-03, BLD-04 | Claim(Village radius 4) · Rule(claims never decay while occupied) | mark, build |
| TER-05 | Daughter Villages | I | 225 | TER-04, SOC-06 | Rule(fission sites scored by quality and Beacons; new villages start with claim ×1.5) | mark, scout |
| DEF-03 | Palisades & Ditches | S | 180 | DEF-02, WOD-03 | Build(Palisade: settlement defense ×2) | build, guard |
| WAR-04 | Warrior Bands | R | 120 | WAR-03, SOC-05 | Role(Warrior) · Mod(Raid success, +40%) · Rule(border patrols: presence along borders) | fight |
| DIP-03 | Marriage Alliances | I | 225 | DIP-02, SOC-04 | Diplo(Alliance: no fighting, shared diffusion, joint defense) | meet strangers, feast |
| INQ-03 | Stone & Ore Lore | M | 150 | INQ-01, MIN-01 | Reveal(surface ores within sight: copper, tin, gold) · Rule(makes [Seen] metal gates reachable) | mine, scout |
| KEY-3 | Age of Metal | K | 375 | MET-02, SOC-07, TRD-03, NUM-03 | EraAdvance(E3) · Highlight(ore deposits on the map) · Legacy(Keystone) | — |

### E2 Moments and flavor
| ID | Moment (the innovator scene) | Chronicle flavor |
|---|---|---|
| STN-08 | Hours of grinding; a jade-green axe gleams | "Polished until it shone like water." |
| STN-09 | A woman kneels at a saddle quern, flour gathering at its lip | "Bread begins on its knees." |
| STN-10 | Dozens haul a standing stone on rollers as the sun sets behind it | "We raised the sky's pillars." |
| FIR-07 | A domed kiln glows orange at night | "A fire that does not escape." |
| FIR-08 | A smoking mound of earth-covered wood | "Black wood that burns hotter." |
| WOD-03 | An adze hollows a log | "We cut the tree into what we needed." |
| WOD-04 | Stumps sprouting straight poles in a managed grove | "Cut, and it grows back." |
| FIB-06 | A spindle whorl twirls, drawing wool into thread | "Turning fluff into thread." |
| FIB-07 | A warp-weighted loom; a striped cloth grows | "The first cloth, woven." |
| CER-03 | Rows of large jars sealed with clay lids | "Every harvest has a home." |
| BLD-04 | Woven branches plastered with mud | "Walls that breathe." |
| BLD-05 | Rows of bricks drying in the sun | "The river's mud became our walls." |
| BLD-06 | A long hall, smoke rising from several hearths | "Many families, one roof." |
| MIN-02 | Antler picks chip at chalk at the bottom of a deep pit | "We dug for the best stone." |
| MIN-03 | A block is wedged loose from a rock face | "The mountain gives us its bones." |
| MET-01 | Green-streaked stone, hammered into a bright bead | "A stone that bends." |
| MET-02 | Green stone in a hot kiln; red metal runs into the ash | "We melted the rock." |
| MEC-02 | Logs rolled under a great stone | "Rolling the impossible." |
| NRG-01 | Oxen drag a sledge of stone | "Their strength, our purpose." |
| CHM-03 | White plaster spread on a floor | "Burned stone made our homes clean." |
| FOR-07 | Saplings planted in rows | "We planted for our grandchildren." |
| HNT-09 | A fence of stakes across a stream, fish trapped behind it | "The river fishes for us." |
| AGR-01 | The first seeds pressed into cleared soil | "We stopped following the food." |
| AGR-02 | Plump grain that doesn't fall from the stalk | "The plants changed for us, and we for them." |
| AGR-03 | Lentils and wheat growing side by side | "The beans feed the soil." |
| AGR-04 | A burning forest edge; ash-blackened fields beyond | "Fire opens the forest." |
| HUS-03 | A child leads a goat on a rope | "Meat that walks beside us." |
| HUS-04 | An aurochs calf penned behind stakes | "The wild bull, tamed." |
| FOD-05 | Foam on a clay jar of beer | "Grain that makes us glad." |
| FOD-06 | A raised granary with grain pouring into it | "Hunger has a door, and we closed it." |
| MED-03 | A midwife holds up a newborn | "More mothers live, more children grow." |
| SOC-05 | A great feast; a host stands, arms wide | "Generosity is power." |
| SOC-06 | Houses clustered along a stream, paths between them | "We stayed, and became a village." |
| SOC-07 | A chief receives tribute from three villages | "Many villages, one voice." |
| GOV-02 | A chosen leader with a staff stands before the gathering | "One will decide." |
| GOV-03 | Compensation paid: a feud ends | "The law is older than anger." |
| BEL-04 | Plastered ancestor skulls set in a niche | "The ancestors hold the land." |
| BEL-05 | Pilgrims climb toward a shrine at dawn | "A place where the world is thin." |
| LRN-03 | A master potter guides an apprentice's hands | "Skill passes hand to hand." |
| WAY-04 | A map scratched on a mammoth tusk | "We drew the land, and shared it." |
| TRN-04 | A donkey laden with baskets plods down a trail | "A beast to bear our load." |
| SEA-03 | A hollowed log slides into the sea | "The sea's edge opened." |
| SEA-04 | An outrigger rides a swell toward a distant island | "Beyond the horizon, more land." |
| TRD-03 | Traders with obsidian arrive along a well-worn path | "Our goods travel farther than we do." |
| COM-04 | Fires lit on hill after hill | "The news ran faster than feet." |
| NUM-03 | Clay tokens in a sealed ball | "Little clay shapes stood for grain." |
| TER-04 | A boundary ditch dug around fields | "This place is ours, always." |
| TER-05 | A daughter village is founded across the valley | "Our children's children spread across the land." |
| DEF-03 | Sharpened stakes ring a village | "A wall of trees." |
| WAR-04 | Warriors paint their faces before a raid | "Some of us live for war." |
| DIP-03 | A wedding between two peoples | "Blood ties make peace." |
| INQ-03 | A prospector cracks open a green-streaked rock | "The earth's colors tell of metal." |
| KEY-3 | Copper glints in a chief's hand as villages gather | "The Age of Metal begins." |
## 6. Acceptance checks for this spec

These are the checks implementation (and the balance model) runs against the numbers above:

| # | Check | Target | Source |
|---|-------|--------|--------|
| C1 | **Food cap trajectory**: average settlement FoodCap produced by the real effects (yields, stocks, food value) | 20 → 27 (E0) → 41 (E1) → 88 (E2), ±10% | [09 §5.3](09-population-and-health.md#53-what-the-caps-do-balanced-standard) |
| C2 | **Late-winter food share** reaches **0.85**, the cap-trigger share: from then on the cap trigger fires before the winter one, so winter no longer limits the band. A share of 1.0 is out of reach while stored food spoils ([07c](07c-e0-e2-checks.md) C2). | by E1 ≈ 0.4 of the era (FOR-03, FOR-02, FOD-02, FOD-03) | [09 §6.2](09-population-and-health.md#62-late-winter-food-the-lean-season) |
| C3 | **Hazards by cause**: project Hazard primitives and live mechanisms reproduce the 09 child/adult targets from the E0 base at each era midpoint. Do not apply the era's MortChild/MortAdult value a second time as the runtime base. Compare annual-effective E1/E0 and E2/E0 ratios after converting each era's outside-winter Mort* with its WinterMult. Elder live rates are age-standardized from exact-age seasonal hazards to e45 and compared with 09's 15/16/16-year anchors; a transient 45+ cohort mean is diagnostic only. | ±10% for ratios and e45; live gate passes per 07c §5 | [09 §4.1–4.2](09-population-and-health.md#41-hazards-by-era) |
| C4 | **First fission** (late-winter trigger) | per-archetype median successful founding at years 7–9 at Standard; report stochastic tails and non-fissions separately | [09 §7.3](09-population-and-health.md#73-results-first-fission) and [07c §6](07c-e0-e2-checks.md#6-c4--first-fission) |
| C5 | **Keystone timing** with the 04 openings | KEY-1 ≈ 7.5 min, KEY-2 ≈ 17.5, current KEY-3 projection ≈ 41.5 (Standard) | [07c §10.1](07c-e0-e2-checks.md#101-roots-and-bronze-still-finish-too-quickly-2026-10-24); fresh live C5 acceptance pending |
| C6 | **Efficiency growth** on core actions | ×1.5–2 per era | [10 §12](10-e0-e2-actions-and-goods.md#12-efficiency-check-e0--e2-growth) |
| C7 | **First winter is a felt threat** in about half of games without storage picks | 51% chance of a harsh winter in the first two | [09 §4.3](09-population-and-health.md#43-winter-hazards) |
| C8 | **Every project has a visible change** within one season of completion (its Moment and a new action, tool sprite, structure or overlay change) | 145/145 | [08](08-ui-and-world-display.md); checked in the sim's event stream (148/148 numeric projects including the three keystones) ✅ |

**Note on catalog percentages.** The prose percentages in 03a–03c may describe a per-figure effect (e.g. SOC-01 "child survival +20%"); the Hazard values in this document are already whole-population averages, as specified in §1. Where they differ, **this document wins**. V16 checks the cumulative catalog proxy; only the live mechanism-level run can establish C3.
