# 09 — Population and Health (WS-H)

> The demography spec (workstream H in [06-next-steps.md](06-next-steps.md) §3): cohorts, births, deaths per era, winter, the three population caps raised project by project, fission, and the disease model constants for [05](05-simulation-structure.md) §9 and §13. It replaces every population input the WS-A balance model marked **ASSUMED** ([07](07-balance-findings.md) §2) with a justified value, and fixes the first-fission knife-edge (07 F5).
> Design only. The numbers live in [balance/tools/params.py](balance/tools/params.py) (scenario `tuned`) and the workbook [balance/balance-model.xlsx](balance/balance-model.xlsx) (new `Caps` sheet). Locked from 07 §5 and kept: base fertility **0.13**, fission at **85%** of the settlement cap, daughter **40%** and **at least 6** people.

---

## 1. Summary

**Demography still drives pacing, and the arc still lands.** With the new population model the Standard arc runs **172.5 min** (Quick 86, Epic 299). Every era is inside its 02 §8.3 window. Fertility ±15% still moves the arc −8% / +11%, so demography is the lever WS-B must not disturb.

**First fission now lands at minute 7–9 for every archetype** (WS-A: 6–13). A late-winter food check splits the band when its lean-season food runs short (§7.2). The timing depends on the population clock, not on research speed: Insight ±20–25% moves it by at most one year.

**Caps now rise project by project.** Food, late-winter food, housing and group caps each step up when a specific catalog project completes, at the point in its era where that project typically lands (§5). In WS-A they jumped at era boundaries. 96 cap effects from 92 projects replace the per-era tables.

| Change (headline) | WS-A placeholder | WS-H value |
|---|---|---|
| Caps | one value per era | **start value × 96 project steps** (`Caps` sheet) |
| Fission triggers | ≥ 85% of cap | ≥ 85% of cap **or late-winter food security < 1** |
| Fertility × (E0…E9) | 1.0 · 1.25 · 1.35 · 1.3 · 1.2 · 1.1 · 1.05 · 1.0 · 0.75 · 0.55 | **1.0 · 1.15 · 1.3 · 1.25 · 1.2 · 1.1 · 1.1 · 1.0 · 0.55 · 0.35** (TFR anchors, §3.3) |
| Mortality | ASSUMED | life-table anchors per era (§4.1): child q(14) 47% → 1%, e45 15 → 37 years |
| Winter multiplier | 2.0 … 1.0 | **2.0 · 1.7 · 1.4 · 1.25 · 1.2 · 1.15 · 1.1 · 1.08 · 1.05 · 1.05** (§4.3) |
| Crowding | counts the group cap | **food and housing only** (social size splits a group; it doesn't starve it) |
| EraBase E0…E9 (02 §8.3) | 20 · 45 · 70 · 180 · 600 · 3,400 · 18k · 160k · 1.0M · 4.8M | **22 · 50 · 75 · 180 · 550 · 3,000 · 15k · 130k · 750k · 3.8M** (re-fit, §11) |
| Disease | not specified | 4 outbreak classes, density pressure, SIR-lite final size, novel-disease shock (§10) |

Unchanged: Insight scale (Commons 4.8, roles ×8), breadth N, representation scale, claim radii, territory and specialist shares (justified in §8–§9).

## 2. Scope and method

**Two levels.** The game simulates single-year cohorts per settlement (§3.1). The balance model collapses them to three bands (children 0–13, adults 14–44, elders 45+) in one mean-field "average settlement". Every number below is given at the level the game needs, then checked in the balance model.

**What a per-era table means.** The fertility and mortality tables are **expected values for an average civilization partway through that era**, having completed that era's typical projects. In the game, hazards are built bottom-up: cause-specific base hazards × the effects of completed projects (§4.2). The tables are the **targets WS-B must hit** when it numbers those effects. If WS-B's effect numbers reproduce a table within ±10%, the pacing in §11 holds.

**Method.** Anchor each value on a real-world demographic figure, adjust it for the game's time compression (one year = one minute at Standard, people age in real time), then run the balance model across all archetypes and paces. Where an anchor and the pacing targets disagreed, I kept the anchor and re-fit EraBase (§11).

**Time compression.** Base fertility 0.13 per woman per season is about **16 births per lifetime** of unconstrained fertility, roughly three times a real forager's 5–6. That is what lets 12 people become ~145,000 in three hours. Two consequences follow:
- Populations are **young**: 54–58% children from E1 to E7 (real foragers: ~40%). The workforce is about 35% of the population, so WS-E's labor targets should assume it.
- Crowding against the caps does most of the braking. That is why caps and fission timing matter so much for pacing.

## 3. Cohorts and births

### 3.1 Cohorts and aging
- **Game:** each settlement holds counts by **single year of age (0–99) × sex**: 200 fixed-point counters. Births enter age 0 each season, and all cohorts shift one year at the end of Winter. Figures are drawn from the cohorts as before (05 §5.2). At 1:1 every counter is a real person.
- **Life stages** stay as in 05 §5.3: child 0–13, adult 14–44, elder 45+.
- **Balance model:** three bands, with flows of C/(14·4) children to adults and A/(31·4) adults to elders each season.
- **Migration:** a daughter group takes a **cross-section** of the parent: 40% of every age band, rounded so it holds at least 1 adult woman and 1 elder or skilled adult (05 §9.4).

### 3.2 Births and crowding
```
BirthsPerSeason = FertileWomen(14–44) × 0.13 × FertMult[era] × FoodSecurity × Health × (1 − Crowding) × Modifiers
Crowding        = min(1, (Pop / MIN(FoodCap, HousingCap)) ^ 6)
```
- **Crowding is food and housing pressure only.** The v0.1 formula also counted GroupCap × Cohesion. A group that has grown too large to hold together **splits** (§7). It doesn't stop having children. This also means low Cohesion triggers fission but doesn't cut fertility.
- The exponent 6 keeps births nearly unaffected below 60% of the cap (−5%). They halve at 89% and stop at the cap. Real density dependence in foragers works through birth spacing and weaning age, both of which respond late and sharply.
- **FoodSecurity** (0–1) is the share of the band fed last season. **Health** = 1 − 0.5 × (share of fertile women sick this season, §10). Both are 1.0 in the balance model, whose hazards already carry the expected losses.
- **Per figure (1:1 scale):** a woman conceives with probability *p* per season and is then unavailable for 3 seasons (pregnancy and early nursing). *p* = 1 / (1 / (0.13 × FertMult) − 3): 0.21 at FertMult 1.0, 0.34 at 1.3, 0.05 at 0.35. Above 1:1 the cohort rate is used directly.
- **Modifiers** are catalog effects with no era of their own, such as Posture and Legends. FOD-04 and MED-03 are already inside FertMult and the child hazard (§3.3, §4.1).

### 3.3 Fertility by era

Anchor: total fertility rate (TFR) relative to the forager baseline. Foragers average about 5.5 (Hadza, !Kung and Ache range 4.7–8). Neolithic farmers rose to 7+ as weaning foods and sedentism shortened birth spacing. Pre-industrial agrarian societies settled near 6, and the demographic transition brings it down to about 2.

| Era | E0 | E1 | E2 | E3 | E4 | E5 | E6 | E7 | E8 | E9 |
|---|---|---|---|---|---|---|---|---|---|---|
| Anchor TFR | 5.5 | 6.2 | 7.2 | 7.0 | 6.5 | 6.0 | 6.0 | 5.5 | 3.0 | 2.0 |
| **FertMult** | **1.0** | **1.15** | **1.3** | **1.25** | **1.2** | **1.1** | **1.1** | **1.0** | **0.55** | **0.35** |
| Drivers | — | cooked food (FOD-01), stores (FOR-03, FOD-03) | FOD-04 weaning foods, AGR-01/02 cereals, KEY-2 sedentism | | | late marriage (no catalog project) | | early transition: mortality falls first | MED-14 Public Health, LRN-11 Mass Education | SOC-14 Welfare, LRN-11 |

WS-A's 1.25 in E1 had no anchor; Upper Paleolithic fertility gains were modest. The E8–E9 drop is steeper than WS-A's (0.55 / 0.35 vs 0.75 / 0.55), because that is what the transition did. Late population is held mostly by settlement count × cap (§5.3). The steeper drop costs about 17% of peak population (145,000 vs WS-A's 175,000) and mostly shifts the age structure. Elders reach 27% by the end of E9, which counts double in Commons Insight (02 §8.1). "Lifespan *is* research" (03b MED) becomes literal in the late game.

## 4. Deaths

### 4.1 Hazards by era
The table values are **annual hazards outside winter**. Per season, the hazard is *h*/4, and ×WinterMult in Winter, so the effective annual hazard is *h* × (3 + W)/4. The anchors below are effective values.

| Era | E0 | E1 | E2 | E3 | E4 | E5 | E6 | E7 | E8 | E9 |
|---|---|---|---|---|---|---|---|---|---|---|
| Anchor: child q(0–14) | 47% | 42% | 42% | 40% | 36% | 34% | 30% | 22% | 6% | 1% |
| **MortChild** | **0.036** | **0.033** | **0.035** | **0.034** | **0.030** | **0.029** | **0.025** | **0.017** | **0.0045** | **0.0009** |
| Anchor: adult annual death rate | 1.5% | 1.3% | 1.3% | 1.2% | 1.1% | 1.1% | 1.0% | 0.8% | 0.3% | 0.15% |
| **MortAdult** | **0.012** | **0.011** | **0.012** | **0.011** | **0.0105** | **0.0105** | **0.0095** | **0.0078** | **0.003** | **0.0015** |
| Anchor: e45 (years left at 45) | 15 | 16 | 16 | 17 | 18 | 19 | 20 | 23 | 30 | 37 |
| **MortElder** | **0.053** | **0.053** | **0.057** | **0.055** | **0.053** | **0.051** | **0.049** | **0.043** | **0.033** | **0.027** |
| Survive 14 → 45 | 63% | 67% | 66% | 70% | 71% | 71% | 74% | 78% | 91% | 95% |

- **Anchors.** Forager life tables give 40–50% of children dead by 15, about 1–1.5% adult mortality a year, and e45 of about 20 (Gurven & Kaplan's cross-forager synthesis). E0 is set harsher than living foragers (e45 = 15) because the band has no fire-making, cooking or shelter yet. Pre-industrial child mortality stays near 30–40% until vaccination and germ theory (E7). Antibiotics and public health (E8) bring it to modern levels.
- **The Neolithic paradox (E2).** Child and adult hazards rise slightly in E2 while fertility jumps. Sedentism, herds and stored grain bring zoonoses, waterborne disease and dental and nutritional stress (§10: epidemics start in E2). Kin-leaning players feel it as more births and more funerals.
- **Elders.** The balance model uses one constant elder hazard, 1/e45. In the game the elder hazard is **Gompertz**, *h*(*a*) = *h*45 × e^(0.085 (*a* − 45)), with *h*45 chosen to give the same e45: 0.023 (E0), 0.021 (E1–E2), 0.018 (E3), 0.016 (E4), 0.015 (E5), 0.013 (E6), 0.009 (E7), 0.0045 (E8) and 0.0024 (E9). MED-07/13/15 effects shift *h*45, not the slope.
- **Infants.** In the game, age 0 carries **3×** the child hazard and ages 1–4 carry 1.5×, rescaled so the 0–13 average equals MortChild. MED-03 (midwifery) and FOD-04 (weaning foods) act mainly on age 0. The balance model doesn't need the split.
- **The tables include expected epidemic losses** (§10.6). The game's base hazards are the table values minus that share, so outbreaks don't double count.

### 4.2 Causes, and how catalog effects act
Each hazard is a sum of causes. A catalog effect such as "injury deaths −30%" (MED-02) scales only its own cause. Cause shares of the effective hazard, by age band (adults / children; elders follow adults, with age and disease larger):

| Cause | E0 | E3 | E6 | E9 | Catalog effects acting on it |
|---|---|---|---|---|---|
| Endemic disease (incl. food-, water-borne) | 30 / 45% | 35 / 50% | 30 / 50% | 30 / 40% | FOD-01, CHM-03/05, MED-01/05/06/11/12/13, FOD-14 |
| Epidemics (§10) | 0 / 0% | 10 / 10% | 17 / 17% | 2 / 2% | MED-07/08/10/11/13/14 |
| Starvation (lean-season shortfall) | 15 / 20% | 10 / 15% | 8 / 10% | 1 / 1% | FOR-02, SOC-02, WAY-02, FOD-03/06, GOV-04, FOD-11, AGR-15 |
| Exposure (cold) | 15 / 20% | 10 / 10% | 8 / 8% | 2 / 2% | FIR-01/02, BLD-01/03, FIB-04, FIR-10 |
| Injury and accident (incl. childbirth) | 25 / 10% | 20 / 10% | 20 / 10% | 40 / 50% | HNT-03/05, MED-02/03/04/09 |
| Predators | 5 / 5% | 0 | 0 | 0 | FIR-01, HUS-01 |
| Violence (feud, raid) | 10 / 0% | 15 / 5% | 17 / 5% | 25 / 5% | GOV-01/03/06/10, DIP-11 |

**WS-B rule.** Apply each project's effect to its cause share, with the project's take probability, starting from E0. Then check that the product reproduces §4.1 within ±10% at each era's midpoint. The catalog percentages taken at face value overshoot early. For example, SOC-01's "child survival +20%" alone would deliver all of E0→E1's child-hazard drop. WS-B should scale them, not the tables.

### 4.3 Winter hazards
Winter is the early game's main pressure (05 §4.3). It works through three channels:

1. **The winter multiplier** on every hazard in the Winter season:

   | Era | E0 | E1 | E2 | E3 | E4 | E5 | E6 | E7 | E8 | E9 |
   |---|---|---|---|---|---|---|---|---|---|---|
   | **WinterMult** | **2.0** | **1.7** | **1.4** | **1.25** | **1.2** | **1.15** | **1.1** | **1.08** | **1.05** | **1.05** |
   | Winter's share of all deaths | 40% | 36% | 32% | 29% | 29% | 28% | 27% | 26% | 26% | 26% |
   | Drivers | no stores, open sleeping | FIR-02, BLD-02/03, FOD-02/03 | granaries, longhouses | | | FIR-10 chimneys | | | | modern excess winter mortality |

   Pre-modern Europe shows winter excess mortality of 20–30% above the annual mean. Modern temperate countries show 10–15%, concentrated in elders. In the game the multiplier is the **expected** winter, made of the two channels below plus seasonal disease. It falls as those channels are closed.
2. **Lean-season food (starvation).** At the end of each Autumn the planner forecasts **food security** FS = (stores + expected winter food) / winter need. The forecast is already exposed to the UI (05 §16.5). If a Winter ends with FS < 1, the unfed share 1 − FS takes an extra hazard of 0.25 per season for children and elders and 0.10 for adults (the band shares food, so the weakest go first). FS < 1 also fires the fission check (§7.2).
3. **Exposure (cold).** For each figure without warmth (no fire, shelter or clothing at the biome's cold tier), add 0.04 per Winter season in Cold climates and 0.02 in Temperate ones. FIR-01 fire, BLD-01 lean-tos and FIB-04 tailored clothing remove it one tier at a time.

**Climate variance** (05 §4.3, seeded): each winter's late-winter food is ×0.75 when **harsh** (P = 0.30: forage −40%, cold +1 tier), ×1.15 when **mild** (P = 0.25), and ×1.08 when normal (P = 0.45). The average is ×1.00, so LeanBase is the climate-averaged value and the mean-field model needs no correction.

**Is the first winter a felt threat? (06 §6 item 3)** The starting band's late-winter food is 20 × 0.77 = 15.4, and about 17 once FOR-01 lands (minute ~0.8). Against a band of 13, FS ≈ 1.3 in a normal first winter and ≈ 0.96–0.98 in a harsh one. That means a short hungry spell and the food forecast turning red, but rarely a death: the margin is thin, not yet lethal. The chance of at least one harsh winter in the first two is 1 − 0.7² = **51%**.

The margin then narrows every year as the band grows. By years 4–7 a harsh winter means real losses, until the split (§7.2) or the storage projects FOR-02, FOR-03, FOD-02 and FOD-03 (§6.2) end it. That is the "unless food-storage projects are chosen" in the criterion.

## 5. Caps raised project by project

### 5.1 The rule
```
FoodCap(settlement)    = FoodBase 20     × Arch_FoodCap × Π (food projects done)
LeanFood(settlement)   = FoodCap × MIN(1, LeanBase 0.77 × Π (lean-season projects done))
HousingCap(settlement) = HousingBase 40  × Π (housing projects done)
GroupCap(settlement)   = GroupBase 25    × CohesionFactor × ladder step (SOC/GOV projects done)
EffectivePopCap        = MIN(FoodCap, HousingCap, GroupCap)              (05 §9.1)
```
- In the game, caps come from the world: tiles, yields, stores and dwellings (05 §9.1). The multipliers here are **what each project does to an average settlement's cap**, and they are the targets WS-B's effect primitives should reproduce. The rule for deriving one: take the effect's size and weight it by the share of food it touches. For example, forage +30% at ~40% of the E0 diet ≈ ×1.12 (FOR-01 is set at ×1.10). Effects that save labor rather than add food (tools, carts, hauling) count at about half, because they raise output more than carrying capacity.
- **When a cap step happens.** A step applies once research reaches *era + at*, where *at* is the share of that era's requirement done by then:
  - **Critical-path projects:** *at* follows their depth in the in-era path.
  - **Likely free picks:** early (0.2–0.5).
  - **Forced backfills:** 0.10–0.20 of the era that forces them (07 F8 recommends auto-queuing them). This is why SOC-04, an E1 project on KEY-3's closure, is listed under E2.
  - **Optional projects:** a take probability, so the cap is × mult^prob.
- In the balance model, the research position (era + share done) comes from the previous season's progress. A fast civilization gets its caps earlier, but **only in steps the size of one project**, not one era.

### 5.2 The cap projects
The full list, with *at* in brackets, is in `params.CAPS` and on the workbook's `Caps` sheet. Group steps show the new cap.

| Done in | Food (× per settlement) | Late-winter food (× LeanBase) | Housing (×) | Group (new cap) |
|---|---|---|---|---|
| E0 | FOR-01 ×1.10 (.10) · HNT-01 ×1.12 (.25) · FOD-01 ×1.10 (.45) | — | BLD-01 ×1.25 (.85) | SOC-01 → 35 (.35) |
| E1 | HNT-02 ×1.04 (.20) · FOR-03 ×1.08 (.30) · HNT-03 ×1.04 (.45) · WAY-02 ×1.05 (.50) · HNT-07 ×1.10^0.6 (.50) · FOR-04 ×1.10 (.60) · HNT-04 ×1.10^0.7 (.85) | FOR-03 ×1.25 (.30) · FOR-02 ×1.10 (.40) · FOD-02 ×1.15 (.40) · FOD-03 ×1.30 (.70) | BLD-02 ×1.2 (.25) · BLD-03 ×1.5 (.55) | SOC-03 → 50 (.80) |
| E2 | FOD-04 ×1.05 · FOR-05 ×1.04 (.10) · FOR-06 ×1.06 (.15) · AGR-01 ×1.30 (.30) · AGR-02 ×1.22 (.50) · FOD-05 ×1.05 (.65) · AGR-03 ×1.08^0.5 · HNT-09 ×1.08^0.4 (.70) · FOR-07 ×1.08^0.4 (.75) | (≥ 1 already) | SOC-04 hamlet ×1.3 (.10) · BLD-04 ×1.5 (.35) · SOC-06 village ×1.5 (.50) · BLD-06 ×1.5 (.60) | SOC-04 → 150 (.10) · SOC-06 → 500 (.50) · SOC-07 → 2,000 (.80) |
| E3 | HUS-02/03/04 ×1.04/1.08/1.05 · FOD-06 ×1.05 (.15) · AGR-05 ×1.17 (.35) · TRN-05 ×1.06 (.40) · HUS-05 ×1.08^0.5 (.50) · AGR-06 ×1.17 (.55) · TRD-05 ×1.05 · AGR-07 ×1.10^0.6 (.60) · GOV-04 ×1.04 (.70) | | BLD-05 ×1.5 (.15) · SOC-09 city ×1.6 (.60) | SOC-09 → 10,000 (.60) |
| E4 | HNT-06 ×1.03 (.15) · AGR-09 ×1.20 (.40) · TRN-07 ×1.06^0.7 · AGR-08 ×1.10^0.5 · MEC-05 ×1.04^0.6 (.50) · FOR-08 ×1.08^0.4 · FOD-09 ×1.15^0.4 · SEA-07 ×1.08^0.5 (.60) · AGR-10 ×1.25 (.70) | | BLD-07 ×1.2 (.15) · BLD-09 ×1.3 (.40) · BLD-10 ×1.4^0.6 · BLD-11 ×1.2^0.5 (.60) | **SOC-10 → 50,000 (.50)** |
| E5 | BEL-10 ×1.05 (.30) · AGR-11 ×1.35^0.9 · NRG-05 ×1.08^0.6 · TER-10 ×1.05^0.6 (.50) · HUS-10 ×1.15^0.7 (.60) | | FIR-10 ×1.15^0.5 (.60) | — |
| E6 | AGR-12 ×1.35^0.9 · HUS-11 ×1.10^0.5 (.50) · FOR-10 ×1.20^0.6 · TRN-11 ×1.10^0.7 · HNT-12 ×1.08^0.4 (.60) | | BLD-13 ×1.3^0.5 (.60) | **GOV-12 → unbounded (.60)** |
| E7 | TRN-12 ×1.50 (.40) · AGR-13 ×1.40^0.9 (.50) · FOR-11 ×1.40^0.8 · FOD-11 ×1.05^0.6 (.60) | | MED-12 ×1.5^0.8 (.50) · BLD-14 ×1.2^0.6 (.60) | — |
| E8 | CHM-12 ×1.50^0.9 (.20) · FOD-12 ×1.10^0.8 (.40) · AGR-14 ×1.15^0.8 (.50) · AGR-15 ×2.0^0.7 (.60) · FOD-13 ×1.15^0.6 (.70) | | TRN-13 ×1.1 (.15) · BLD-15 ×2.0^0.7 (.50) · TER-13 ×1.3^0.6 (.60) | — |
| E9 | AGR-16 ×1.20^0.7 · FOD-14 ×1.15^0.6 (.50) · AGR-17 ×1.15^0.6 (.60) | | TRN-17 ×1.2^0.5 (.50) · TER-15 ×2.0^0.5 · BLD-16 ×1.1^0.6 (.60) | — |

**Two additions to the group ladder.** The 05 §9.1 ladder stops at 10,000 (SOC-09) until "effectively unbounded (E9)". But the settlement tiers already allow Metropolis 100k+ in E8 (05 §9.5), so the ladder needs steps in between:
- **SOC-10 Citizenship (E4) raises the group cap to 50,000.** Shared civic identity holds a group beyond kin and city.
- **GOV-12 Nation-State (E6) removes it** (1,000,000 in the model). From then on food and housing bind.

SOC-15's "effectively unlimited" becomes the removal of the CohesionFactor floor: networked identity no longer shrinks with low Cohesion. The model never lets group bind after E1 anyway (§5.3), so these steps change no number in §11. They keep the spec consistent.

**Housing follows tier.** Settlement-tier projects (SOC-04 hamlet, SOC-06 village, SOC-09 city) raise housing as well as the group cap. A higher tier lets the planner build on more tiles.

### 5.3 What the caps do (Balanced, Standard)
| Era | Food cap / settlement (start → end) | Housing | Group | Binding cap | Avg people / settlement at era end |
|---|---|---|---|---|---|
| E0 | 20 → 27 (late-winter 15 → 21) | 40 → 50 | 25 → 35 | food (late-winter food triggers the split) | 11 |
| E1 | 27 → 41 | 50 → 90 | 35 → 50 | food; group 35 for about a quarter of the era, from FOR-04 until SOC-03 | 23 |
| E2 | 41 → 88 | 90 → 395 | 50 → 2,000 | food | 58 |
| E3 | 88 → 190 | 395 → 948 | 2,000 → 10,000 | food | 144 |
| E4 | 190 → 373 | 948 → 1,982 | 10,000 → 50,000 | food | 266 |
| E5 | 373 → 610 | 1,982 → 2,125 | 50,000 | food | 444 |
| E6 | 610 → 1,031 | 2,125 → 2,423 | → unbounded | food | 821 |
| E7 | 1,031 → 2,821 | 2,423 → 3,739 | — | food | 1,552 |
| E8 | 2,821 → 8,664 | 3,739 → 7,821 | — | food, then housing (skyscrapers) | 5,740 |
| E9 | 8,664 → 11,640 | 7,821 → 12,829 | — | housing, then food | 9,691 |

The story the caps tell: **food binds from the Paleolithic to the Industrial Revolution** (Malthusian), with a hungry winter as the first constraint and the band size briefly in late E1. **Urban housing and density bind in the Modern era.** Below E7, population runs at 40–80% of its caps: growth is fertility-limited, braked by crowding. From E7 it runs at 55–85%, and in E8–E9 the territory limit on settlement count (§8) makes total cap = settlements × cap the real ceiling.

## 6. The three caps in detail

### 6.1 FoodCap
**FoodBase = 20 people per settlement** with no techniques. That is what a radius-2 home range (19 tiles) supports by scavenging and unskilled foraging. The E0 projects take it to about 27 by mid-E0, close to WS-A's assumed 22. Ethnographic bands average about 25 people, and the base GroupCap is 25 (05 §20), so the band, the food and the social layer start in step. The archetype FoodCap multiplier (Grower ×1.2, Wanderer ×0.95, …) acts here and on the late-winter food.

In the game, FoodCap = (sustainable food income over 4 seasons + stores) ÷ per-capita need (05 §9.1). The mean-field values above are what that formula should return for an average settlement. WS-D's goods and yields should be checked against them.

### 6.2 Late-winter food (the lean season)
**LeanBase = 0.77.** Before any storage project, a camp can count on 77% of its average food in the last weeks of winter: forage regrowth at 20% (05 §20), hunting continuing, plus the season of stores the band starts with. Lean-season projects raise the share. Once it reaches 1, winter no longer limits the band beyond its normal cap:
- FOR-03 Nut & Seed Gathering ×1.25: storable foods.
- FOR-02 Roots & Tubers ×1.10: dry-season food.
- FOD-02 Smoking & Drying ×1.15.
- FOD-03 Storage Pits ×1.30.

For an average civilization the share reaches 0.96 at E1 0.3 (FOR-03) and 1.0 at E1 0.4.

FOR-02 is an E0 project, but its lean-season effect is placed at E1 0.40, where it lands as a KEY-2 backfill. Earlier placements put a cap step right inside the first-fission window and brought the knife-edge back (§7.3).

**Note ([07c](07c-e0-e2-checks.md#4-c2--late-winter-food-share) §4):** with 10's per-project spoilage the derived share reaches **0.96, not 1.0**, because stored food keeps spoiling. It crosses **0.85**, the cap-trigger share, at about E1 0.4, so fission behavior is unchanged. 11 §6 C2 now targets ≥ 0.85.

### 6.3 HousingCap
**HousingBase = 40 per camp.** Open sleeping is allowed early (05 §9.1) with an exposure risk (§4.3), so early housing is generous and never binds. The planner builds dwellings ahead of demand, so housing is mostly **build-limited, not technology-limited**. Its project steps are density steps: walls, then multi-family halls, mudbrick, cities, aqueducts, sewers, skyscrapers, suburbs and megacities. Housing stays at 1.3–5× the food cap until E7, which keeps it out of the way until modern cities make density the binding limit.

### 6.4 GroupCap
**The ladder is unchanged from 05 §9.1** (25 → 35 → 50 → 150 → 500 → 2,000 → 10,000), with the two additions in §5.2. GroupCap × CohesionFactor (0.6–1.2, 05 §10.1) is the only cap that Cohesion moves. A low-Cohesion band therefore splits sooner, which is what 05 §9.4's "belonging low → volunteers" describes. In the model, CohesionFactor 0.8 changes the Standard arc by less than 1%.

## 7. Fission

### 7.1 Parameters
| Parameter | Value | Note |
|---|---|---|
| Cap trigger | settlement pop ≥ **85%** of EffectivePopCap | 07 §5, locked |
| **Late-winter trigger** | at the end of Winter, settlement pop ≥ late-winter food (FS < 1) | §7.2, new |
| Daughter size | **40%** of the settlement (cross-section of ages), at least **6** people | 07 §5, locked; below 6 the split waits |
| Daughter composition | ≥ 1 adult woman, ≥ 1 elder or skilled adult; carries 1 season of food per person and its tools | 05 §9.4 |
| Departure | the season after the trigger (spring for a winter split); median delay 1 season, while volunteers form | 05 §9.4 |
| Mean-field rate | 34% of over-trigger settlements split per season (at least 1) | balance model's stand-in for the volunteer delay and site search |
| Cooldown | a settlement can't split again for 4 seasons | stops the cap trigger from firing twice on one crowding spell |
| Posture Expand / Prosper | cap trigger 75% / 95%; late-winter trigger at FS < 1.1 / off | 05 §9.4 "lowers / raises the threshold" |
| Before SOC-03 | daughter bands share Insight at 50% | 05 §9.4 |
| Over-cap loss | 10% of the people above the cap per season starve or leave | kept from WS-A: a settlement above cap that can't split (territory full) sheds people to Free Folk land or death |

### 7.2 The late-winter food-pressure trigger
At the end of each Winter, each settlement compares its population with its late-winter food (§6.2). If the band is larger (FS < 1) and the daughter would be viable, a group leaves in spring to found its own camp. This happened among real foragers: bands disperse when the lean season can't feed them together. In the game it gives the first split a cause the player can read: *"After the hungry winter of year 7, Ama led eight of the Reed Camp west."*

**Why it fixes the knife-edge.** WS-A's only trigger was 85% of a cap that jumped at the E0→E1 boundary. Two things made the timing fragile:
1. Crowding slows growth as the band nears 85% (births −38% there), so a small change in the cap moved the crossing a lot.
2. A fast E0 raised the cap just before the band reached it, so the timing hung on research speed.

The late-winter threshold sits at about 0.77 of the food cap. The band crosses it while it is still growing fast (crowding −21%, against −38% at 85%). The check runs once a year, and E0 lean food rises only through E0 food projects that are done by minute ~4 for every archetype. So the split lands on the first winter after the band outgrows its lean food. That timing is set by the population clock.

### 7.3 Results: first fission
Standard pace, in minutes (= in-game years at every pace). "w" = by the late-winter trigger, "c" = by the cap trigger.

| Archetype | WS-A | **WS-H** | Insight ×0.8 | Insight ×1.25 | Fertility ×0.85 | Fertility ×1.15 | CohesionFactor 0.8 | Without the winter trigger |
|---|---|---|---|---|---|---|---|---|
| Balanced | 7.0 | **7 w** | 8 | 7 | 9 | 6 | 7 | 9.0 c |
| Grower | 11.75 | **9 w** | 9 | 8 | 10 | 7 | 3 c ¹ | 9.75 c |
| Builder | 12.75 | **9 w** | 9 | 8 | 11 | 7 | 9 | 11.0 c |
| Wanderer | 6.0 | **7 w** | 7 | 7 | 8 | 6 | 7 | 8.5 c |
| Warlord | 7.75 | **8 w** | 8 | 8 | 10 | 7 | 8 | 9.75 c |
| Sage | 6.75 | **7 w** | 7 | 7 | 9 | 6 | 7 | 8.75 c |

¹ A low-Cohesion Grower outgrows its group cap of 25 × 0.8 = 20 before SOC-01 completes. That is the intended Cohesion behavior, not a timing fault.

- **Every archetype splits at minute 7–9** at Standard, and at year 7–9 at Quick (minute 3.5–4.5) and Epic (minute 12–16).
- **Research speed barely matters:** Insight ±20–25% moves the split by at most one year. That was the source of the 6–13 spread.
- **Fertility still matters:** ±15% gives 6–11. That is demography doing its job, and fertility is locked.
- **Project steps alone fix half of it:** the last column (8.5–11) is caps by project without the winter trigger.

### 7.4 Finding: the long wait for the third settlement
After the first split, the average civilization stays at **2 settlements until minute ~45**, then grows: 3 at 45, 5 at 67, 9 at 82, 16 at 99, 21 at 104. WS-A had the same shape, a bit earlier (3 at minute 29). This is not a cap or trigger problem:
- Each daughter must have at least 6 people, so a civilization can hold at most pop ÷ 15 settlements. With 46 people at minute 18 and 115 at minute 30, that is 3 and 7.
- Settlements grow toward caps that rise with farming, so they stay below 85% of them.
- **Even a group ladder shrunk to game scale** (SOC-04 → 45, SOC-06 → 60, SOC-07 → 80, SOC-09 → 200) only moves the third settlement to minute 32 and costs the historical anchors. I tested it and **did not adopt it**.

In the game the gap is smaller than in the mean-field model. Settlements aren't identical: the parent band keeps 60% and reaches its threshold years before its daughter. Visible growth in E1–E3 also comes from claims rather than settlement count:
- TER-01 radius 3.
- WAY-02 seasonal camps and TER-02 camp circuits (claims across the round).
- TER-04 radius-4 villages.

**This is the open question for WS-A part 2 and WS-F** (06 §6 item 4: "territory visibly grows thereafter"). If the 4-civ model finds the E1–E3 map too empty, here are the options in order of preference:
- (a) Let WAY-02's seasonal camps count as claim sources. **Adopted 2026-10-18** ([07b §9.4](07b-territory-findings.md#94-seasonal-camps-as-claim-sources-adopted-2026-10-18)): end-of-E2 claimed land rises from 8% to ~12.5%.
- (b) Lower the minimum daughter to 5. This reopens a value 07 §5 locked, so it needs that decision first.
- (c) Add an E1 "budding" bias under Posture Expand.

Changing fertility is not an option: it is locked, and it moves the whole arc.

## 8. Settlement limits

### 8.1 Claim radius
Claim radius stays as WS-F owns it (2 · 3 · 3 · 4 · 5 · 5 · 5 · 7 · 9 · 9, `params.ERA["ClaimRadius"]`). It sets the maximum settlement count (territory ÷ claim area × 0.6 packing). From E7 that maximum binds: 23 settlements in E7, then 15 in E8–E9 as radius 9 merges neighbors.

### 8.2 Territory
**Values kept: 60 · 200 · 500 · 900 · 1,400 · 1,800 · 2,200 · 2,400 · 2,500 · 2,500 tiles**, now with a derivation. The standard map has 16,000 tiles. At the Continents land share of 0.40 (the value WS-A part 2 assumes), that is about 6,400 land tiles, of which ~85% (≈ 5,400) is habitable (not ice, peak or deep desert). A fair share for four civilizations is ~1,350 tiles.
- **E0–E2: the land a band can know and reach.** 60 is a radius-4 disc around the start (61 tiles), 200 a radius-8 disc (217), 500 a radius-12–13 disc. That is a few days' walk widening with seasonal rounds and maps.
- **E8–E9: the leader's envelope.** 2,500 is 46% of the habitable land, just short of the 50% Dominion line (05 §11.3). The single-civilization model represents the leader (DiffusionMult 1.0), so its territory is a strong leader's, not a fair share.
- E3–E7 interpolate as states and empires consolidate. WS-A part 2's spatial model (`territory_sim.py`) should replace this table once its contested claims settle.

## 9. Specialist shares

These are kept, because they hold up against historical anchors. A role figure here is the **research-active** part of a group, not every member. Per-settlement roles (one Elder and one Shaman per settlement, Monastery 0.15–0.3 per settlement) follow the catalog unlocks.

| Role | Share of adults | Anchor |
|---|---|---|
| Priest (BEL-06, E3+) | 0.2% → 0.5% (E5–E6) → 0.2% (E9) | Clergy were 1–2% of pre-modern populations, but most did ritual and pastoral work. Temple astronomers and calendar keepers were a few per thousand. |
| Scribe (LRN-04, E3+) | 0.1% → 1.0% (E7–E9) | Literacy in Bronze Age states was under 1% of the population. Clerks and record-keepers reached ~1% of adults by the industrial era. |
| Scholar (LRN-05, E4+) | 0.05% → 2.0% (E9) | From academies to universities to modern higher education and teaching faculties (~2% of the adult workforce in OECD countries). |
| Researcher (LRN-12, E8+) | 0.1% (E8) → 0.5% (E9) | OECD frontier: ~0.8–1% of employment in R&D. Halved for LRN-12's take probability (it is on no closure, 07 F12). |

Adults are only ~35% of the model's population (§2), so a share of adults is about three times the same count as a share of the whole population. Priests at 0.5% of adults are 0.18% of all people, the "few per thousand" of the anchor.

## 10. Disease model (05 §13)

### 10.1 Density pressure
```
pressure = ρ × (1 − Sanitation) × (1 + Inflow)
ρ        = (settlement pop / built tiles) ÷ 50     (50 people per built tile = a crowded village)
```
| Tier | Camp | Hamlet | Village | Town | City | Metropolis | Megacity |
|---|---|---|---|---|---|---|---|
| Typical ρ | 0.4 | 0.6 | 0.8–1.0 | 1.2–1.5 | 1.8–2.0 | 2.5–3.0 | 3.0–3.5 |

- **Inflow** = (trade-route arrivals + migrants + armies passing) ÷ settlement pop per season, capped at 1. Typical values run 0 (E0) → 0.1 (E2) → 0.3 (E4) → 0.6 (E6, oceanic contact) → 1.0 (E8+).
- **Sanitation** combines multiplicatively as 1 − Π(1 − *s*):

  | Source | *s* |
  |---|---|
  | CHM-03 lime | 0.05 |
  | CHM-05 soap | 0.10 |
  | MED-06 baths & latrines | 0.25 |
  | BLD-10 aqueducts | 0.15 |
  | MED-11 germ theory | 0.30 |
  | MED-12 sewers | 0.60 |

  An average civilization's 1 − Sanitation runs 1.0 (E0–E1), 0.95 (E2), 0.85 (E3), 0.58 (E4–E6) and 0.20 (E7+).

### 10.2 Outbreaks
Each settlement rolls once per season for an outbreak, with **P = min(0.25, 0.010 × pressure)**. From MED-08 Quarantine (E5) the roll is ×0.75, because quarantine halves imported outbreaks, which are about half of all outbreaks. The outbreak's class is drawn by era weight:

| Class | R0 | Case fatality | Immunity after | Era weights (E2 / E4 / E6 / E8) | Notes |
|---|---|---|---|---|---|
| Fever (influenza-like, malaria-like) | 1.5 | 3% | 2 years | 0.6 / 0.35 / 0.3 / 0.5 | the only class in E0–E1 |
| Flux (waterborne) | 2.0 × (1 − Sanitation) | 8% | 2 years | 0.3 / 0.3 / 0.3 / 0.2 | sewers end it |
| Pox (crowd disease) | 4.0 | 15% | lifelong | 0.1 / 0.25 / 0.25 / 0.2 | endemic in settlements ≥ **1,000 people** (the critical community size at game scale); below that it must be imported |
| Plague (vector, trade-borne) | 2.0 | 35% | none | 0 / 0.1 / 0.15 / 0.1 | from E3; spreads along routes |

### 10.3 SIR-lite at settlement level
- R_eff = R0 × (1 − immune share) × min(1.5, pressure)^0.5. No outbreak takes hold if R_eff ≤ 1.
- **Attack rate** *z* solves the final-size relation *z* = 1 − e^(−R_eff·*z*), read from a 64-entry fixed-point table (deterministic, 05 §18).
- **The outbreak runs 2 seasons:** 60% of cases in the first, 40% in the second. Sick figures rest and look pale (05 §13). Each season, the share of fertile women sick lowers births (Health, §3.2).
- **Deaths** = *z* × pop × CFR × Π(1 − MED effects):

  | Effect | Applies to |
  |---|---|
  | MED-01 −10% | all classes |
  | MED-07 hospitals −20% | all classes |
  | MED-10 vaccination −80% | Pox |
  | MED-11 germ theory −30% | all classes |
  | MED-13 antibiotics −80% | Flux, Plague |
  | MED-14 −50% | all classes |

  Deaths fall 1.5× on children and 2× on elders relative to adults.
- **Spread:** each season of an outbreak, every settlement linked by a route or a shared border rolls P = 0.25 × link traffic (0–1) to catch it. MED-08 halves that.

### 10.4 Novel disease shock
Each civilization keeps a **disease pool**: the classes it has had endemic, meaning Pox in any settlement of 1,000+ people for 10 years, or Plague along its routes. On first contact, a class one side has and the other never had strikes the less-exposed side once:
- It hits every settlement within 8 tiles of the contact, with R0 × 1.5 and no immunity, over 4 seasons.
- Total loss is **capped at 15%** of that civilization's population. Historical shocks killed far more. The cap is what keeps it "tuned to be survivable" (05 §13).
- After that, the class joins its pool.

It matters most at oceanic contact after SEA-12 (E6), and against Free Folk.

### 10.5 Pollution (E7+)
On polluted tiles, adult and elder hazards are ×(1 + 0.3 × pollution/100) (05 §14). The balance model leaves this at zero (a clean baseline). WS-B/WS-D should keep a dirty-growth civilization's expected penalty under ~10% of E8 deaths, so the dilemma (D12) stays a choice rather than a trap.

### 10.6 Check: expected epidemic losses
Using an average settlement per era (from the balance model), the constants above give these expected epidemic deaths. They are **inside** the §4.1 hazards, not added to them.

| Era | E0 | E1 | E2 | E3 | E4 | E5 | E6 | E7 | E8 | E9 |
|---|---|---|---|---|---|---|---|---|---|---|
| Outbreaks per settlement per year | 0.016 | 0.021 | 0.033 | 0.049 | 0.045 | 0.044 | 0.056 | 0.026 | 0.035 | 0.035 |
| Expected epidemic deaths (‰ per year) | 0.0 | 0.1 | 1.0 | 2.8 | 2.9 | 3.0 | 3.8 | 0.5 | 0.2 | 0.2 |
| Share of all deaths | 0% | 0% | 3% | 10% | 11% | 12% | 17% | 3% | 2% | 2% |

Epidemics arrive with farming (E2) and peak in the age of cities, trade and oceanic contact (E6). Germ theory and sewers end them in E7. The game's base (endemic) hazards are the §4.1 values × (1 − that share).

Per settlement, a pre-modern town sees an outbreak about every 20 years. A plague kills about a quarter of the town and a pox about a tenth. That is often enough to make MED-07/08 a real choice, and rare enough that no single roll decides a match.

## 11. Results

### 11.1 Re-fit
The new demography makes E0 faster (elders live longer: e45 15 years instead of WS-A's 8) and E8–E9 slower (steeper fertility transition). I kept the Insight formula as 02 §8.1 has it (Commons 4.8, roles ×8) and re-fit only EraBase (`fit.py Standard base`, then rounded):

| | E0 | E1 | E2 | E3 | E4 | E5 | E6 | E7 | E8 | E9 |
|---|---|---|---|---|---|---|---|---|---|---|
| EraBase, WS-A | 20 | 45 | 70 | 180 | 600 | 3,400 | 18,000 | 160,000 | 1,000,000 | 4,800,000 |
| Fitted raw | 22.3 | 49.4 | 76.9 | 180.4 | 560 | 3,057 | 14,899 | 132,311 | 736,826 | 3,786,285 |
| **EraBase, WS-H** | **22** | **50** | **75** | **180** | **550** | **3,000** | **15,000** | **130,000** | **750,000** | **3,800,000** |
| Avg in-era project / keystone | 24 / 55 | 55 / 125 | 87 / 188 | 214 / 450 | 668 / 1,375 | 3.4k / 7.5k | 17.7k / 37.5k | 123k / 293k | 773k / 1.69M | 3.93M / 8.55M |

Ratios per era are now ×2.3, 1.5, 2.4, 3.1, 5.5, 5.0, 8.7, 5.8, 5.1, the same shape as WS-A's (07 F2). For comparison, the default `fit.py` (which also re-fits the Insight scale) asks for k = 7.9, essentially the published 8.

### 11.2 Tuned run (Standard, Balanced)
| Era | Start | Minutes | Target | Pop at end | WS-A pop | Settl. | Figures | Insight/min | Know-how | Roles |
|---|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|
| E0 | 0.0 | 7.75 | 7–9 | 22 | 20 | 2 | 22 | 43 | 20% | 0% |
| E1 | 7.75 | 10.0 | 9–11 | 46 | 43 | 2 | 46 | 93 | 15% | 24% |
| E2 | 17.75 | 11.75 | 11–13 | 115 | 106 | 2 | 115 | 192 | 15% | 26% |
| E3 | 29.5 | 13.0 | 12–14 | 287 | 295 | 2 | 287 | 371 | 16% | 15% |
| E4 | 42.5 | 14.75 | 14–16 | 798 | 848 | 3 | 399 | 1,033 | 16% | 13% |
| E5 | 57.25 | 16.5 | 16–18 | 2,220 | 2,553 | 5 | 444 | 3,643 | 15% | 16% |
| E6 | 73.75 | 19.0 | 18–20 | 7,393 | 9,028 | 9 | 296 | 15,224 | 14% | 22% |
| E7 | 92.75 | 22.75 | 22–24 | 32,600 | 40,315 | 21 | 326 | 89,201 | 14% | 24% |
| E8 | 115.5 | 27.25 | 26–28 | 86,097 | 88,107 | 15 | 344 | 560,755 | 13% | 23% |
| E9 | 142.75 | 29.75 | 28–32 | 145,365 | 175,386 | 15 | 291 | 2.55M | 13% | 24% |
| **Arc** | | **172.5** | ≈ 180 | | | | | | | |

- KEY-1 is reached at 7.75 min and KEY-2 at 17.75 (targets 7–9 and 16–20). First fission comes at 7.0.
- **Rendered figures stay at 287–444 from E3 on**, so the representation scale (07 F6) holds unchanged.
- The peak is ~145,000 people rather than 175,000, still "hundreds of thousands" (01 §8).
- Population grows 7–8% a year through E4, 6.5% through E7, then 3.6% (E8) and 1.8% (E9) as the transition bites.

### 11.3 Archetypes, paces and sensitivity
| Variation | Arc (min) | vs. 172.5 | Eras in window |
|---|---:|---:|---|
| Balanced / Grower / Builder / Wanderer / Warlord / Sage | 172.5 / 156.5 / 177.75 / 165.5 / 177 / 172.75 | −9% … +3% | 10 / 3 / 9 / 8 / 10 / 9 |
| Quick / Epic (Balanced) | 86.25 / 299 | target 90 / 300 | |
| Fertility ×0.85 / ×1.15 | 192 / 158 | +11% / −8% | |
| CohesionFactor 0.8 | 172.5 | 0% | |

- **Every archetype stays within ±15% of every era midpoint**, well inside the ±25% tolerance (06 §6). The worst cases are the Grower (8–14% fast from E3) and the Wanderer's E8–E9 (25.75 and 25.5 min).
- The pattern matches WS-A's (07 F11). It is a property of the archetype multipliers, which are WS-E's to set, not of demography.
- The Sweeps sheet has every pace × archetype.

## 12. Change list for other documents
For whoever owns each document. Nothing outside this file and `balance/` was edited.

| # | Doc § | Change |
|---|---|---|
| 1 | 02 §8.3 | EraBase → **22 · 50 · 75 · 180 · 550 · 3,000 · 15,000 · 130,000 · 750,000 · 3,800,000** (§11.1); update the costs line in 07 §5 to §11.1's |
| 2 | 05 §9.1 | Caps: FoodBase 20, HousingBase 40, GroupBase 25, each raised project by project (§5); GroupCap ladder gains SOC-10 → 50,000 and GOV-12 → unbounded |
| 3 | 05 §9.2 | Define Crowding = (Pop / MIN(Food, Housing))^6; FertMult table (§3.3); per-figure conception rule |
| 4 | 05 §9.3 | Hazard tables (§4.1), Gompertz elders, cause shares (§4.2), winter channels (§4.3) |
| 5 | 05 §9.4 | Replace "a seasonal pressure trigger … is a candidate for WS-H" with the late-winter trigger (§7.2) and the parameter table (§7.1); first fission at minute 7–9 for all archetypes |
| 6 | 05 §13 | Point to §10 for constants |
| 7 | 05 §20 | Add LeanBase 0.77, FoodBase 20, HousingBase 40; climate variance P(harsh) 0.30 |
| 8 | 03b SOC-10, GOV-12, SOC-15 | Add **Cap:** group 50,000 (SOC-10), group unbounded (GOV-12); SOC-15 becomes "Cohesion no longer shrinks the group cap" |
| 9 | 07 F5 | Mark the knife-edge resolved (09 §7); 07 §6 tables superseded by §11.2 |
| 10 | 06 §1, §3, §9 | WS-H done; README index: add 09 |

## 13. Open questions and follow-ups
1. **WS-A part 2 (territory):** re-run `territory_sim.py` on this trajectory, since settlements and population changed (§11.2), and decide the E1–E3 expansion gap (§7.4).
2. **WS-B:** number the health and cap effects so they reproduce §4.1 and §5.2. Raw catalog percentages overshoot (§4.2).
3. **WS-D:** check goods and yields against the FoodCap trajectory (§5.3) and the lean-season share (§6.2).
4. **WS-E:** the archetype multipliers drive the Grower's fast arc (§11.3), and the workforce is ~35% of people (§2).
5. **WS-G:** the habitable-land assumption behind Territory (§8.2) and the harsh-winter rate by climate band.
6. **Paper prototype #3** (06 §5): run the first 30 seasons by hand with §4.3 and §7.2. Expected: a hungry winter at year 6–8, a split in spring, and a named death in about half the runs.

## 14. Regenerating
```bash
python docs/MVP/balance/tools/fit.py Standard base     # EraBase fit, Insight scale held (this doc)
python docs/MVP/balance/tools/sensitivity.py final     # archetypes × paces, fertility ±15%
python docs/MVP/balance/tools/build_xlsx.py            # rebuild the workbook (openpyxl)
python docs/MVP/balance/tools/build_xlsx.py --verify 720 /tmp/check.xlsx   # re-check formulas (formulas)
```
The workbook's `Caps` sheet holds the cap projects, `Params` holds the new scalars (CapsByProject, WinterTrigger, LeanBase, GroupInCrowding), and `Sim_tuned` gains columns for research position, the four caps and the late-winter flag. Setting CapsByProject = 0, WinterTrigger = 0 and GroupInCrowding = 1 brings back WS-A's structure. The v0.1 scenario is unchanged and still runs 350 min.
