# 07c — E0–E2 Checks: R6 Adopted, and 11 §6 C1–C5

> Two jobs:
> 1. Adopt [07b](07b-territory-findings.md) R6 in the balance model: territory at the map's room, with late growth vertical rather than wider.
> 2. Run the acceptance checks C1–C5 of [11 §6](11-e0-e2-project-specs.md#6-acceptance-checks-for-this-spec) against the per-project numbers in [10](10-e0-e2-actions-and-goods.md) and [11](11-e0-e2-project-specs.md).
>
> Each mismatch below comes with a concrete fix, and the fixes already applied to 10 and 11 are marked. Design only. The checks are reproducible with [balance/tools/check_e0e2.py](balance/tools/check_e0e2.py): run it as written, or with `--fixed` for the fixed numbers.

---

## 1. Summary

| Check | Target | As written in 10 / 11 | With the fixes | Status |
|---|---|---|---|---|
| **R6** | arc ≈ 172 min, every era in its window | — | **173.5 min**, all 10 eras in window | ✅ adopted |
| **C1** FoodCap per settlement | 20 → 27 → 41 → 88 (±10%) | **8 → 14 → 26 → 99** (−60%…+12%); not determined by 10 alone | **18 → 28 → 42 → 88** (−8%…+3%) | ✅ fixed (applied to 10) |
| **C2** late-winter food share | 1.0 by E1 ≈ 0.4 | 0.71 → 0.96, never 1.0 | reaches **0.85 at E1 0.4**; 1.0 is unreachable by construction | ✅ target restated (applied to 11) |
| **C3** hazards at era midpoints | 09 §4.1 ±10% | **−13…−17% at E1, −25…−36% at E2** | −8%…+2% | ✅ fixed: table applied to 11 (2026-10-18) |
| **C4** first fission | minute 7–9, every archetype | **7–9** | — | ✅ |
| **C5** keystones with the 04 openings | 7.5 / 17.5 / 29 (±25%) | KEY-1 **7.8–8.9**, KEY-2 **17.9–19.6**, KEY-3 **29.5** | — | ✅ (one doc inconsistency, §7) |

## 2. R6 in the balance model

**What changed** in [tools/params.py](balance/tools/params.py), tuned scenario:
- **Territory:** 60 · 200 · 500 · 900 · 1,400 · **1,600 · 1,600 · 1,600 · 1,600 · 1,600**. The map's room per civilization holds from E5 on (07b T6).
- **Late per-settlement caps ×1.6, in project-sized steps.** 07b states R6 as per-era caps (food 4,000 / 9,600 / 19,200, housing 6,400 / 16,000 / 32,000). That was written against WS-A's per-era tables. Since 09, caps rise project by project, so the same ×1.6 is carried by two E7 closure projects (`params.CAPS`):
  - **NRG-07 Watt Engine** at E7 0.25: factory towns, food and housing ×1.265.
  - **TRN-12 Railways** at E7 0.40: rail-fed cities importing their hinterland's grain, food and housing ×1.265. This is on top of its existing ×1.5.
  - 1.265² = 1.60, and it avoids an era-boundary jump (09 §5.1).

**Results** (Standard, Balanced):

| Era | E0 | E1 | E2 | E3 | E4 | E5 | E6 | E7 | E8 | E9 | Arc |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Minutes, 09 (before R6) | 7.75 | 10.0 | 11.75 | 13.0 | 14.75 | 16.5 | 19.0 | 22.75 | 27.25 | 29.75 | 172.5 |
| **Minutes, with R6** | 7.75 | 10.0 | 11.75 | 13.0 | 14.75 | 16.5 | 19.0 | 22.75 | **27.5** | **30.5** | **173.5** |
| Territory 1,600 only, no ×1.6 | 7.75 | 10.0 | 11.75 | 13.0 | 14.75 | 16.5 | 19.0 | 23.25 | **34.5** | **40.5** | 191.0 |
| Target | 7–9 | 9–11 | 11–13 | 12–14 | 14–16 | 16–18 | 18–20 | 22–24 | 26–28 | 28–32 | ≈ 180 |

- **Settlements:** 15 in E7, then **9 metropolises in E8–E9** as radius 9 merges neighbors (09's run had 21 and 15).
- **Peak population** is 140,700 (09: 145,400). Rendered figures stay at 281–444 from E3 on.
- **Paces:** Quick 86.75 min, Epic 300.7 min.
- **Archetypes:** 156.5 (Grower) to 178.25 (Builder). That's −10%…+3%, the same spread as 09 §11.3.
- **Fertility ±15%:** 192 / 160 min.
- **No EraBase re-fit is needed**, because every era stays inside its window. EraBase stays at 09 §11.1's values.

The workbook ([balance/balance-model.xlsx](balance/balance-model.xlsx)) is rebuilt with R6, and its formulas were re-verified against the Python model. 07b's territory model reads the live `balance_sim.py`, so re-running it now uses this trajectory (07b §9 follow-up 1).

## 3. C1 — FoodCap trajectory

**Method.** Doc 10's rates are per worker-season (ws), and food need and labor both scale with band size. So labor alone can't produce a cap. It only says whether a band of any size can feed itself.

The cap comes from **stocks**: sustainable plant regrowth and herd growth within a settlement's range, × food value (10 §4.6), ÷ per-capita need (10 §1). Per-capita need uses 09's age structure at each checkpoint:
- E0 start: 3.6 R per person per year (8 / 2 / 2).
- E0 end: 3.1 (48% children).
- E1 end and E2 end: 3.0 (56–58% children).

The reference is temperate forest (12 §4), the 1.0 reference biome, with 15% grass-stand tiles (an average start's mix). Projects count from their 09 cap placement; others count mid-era, or half the time if optional.

**As written, 10 doesn't determine FoodCap.** Three quantities are missing, and one example is off:

| # | Mismatch | Effect |
|---|---|---|
| C1-a | **No range.** 10 never says how far a settlement forages and hunts. 09 §6.1 assumes "a radius-2 home range (19 tiles)", but 07b R3 made the camp claim radius 3. | Radius 2 gives **8 people** at E0 start (−60%); radius 3 gives 16. |
| C1-b | **No herd stock rule.** Scavenging and hunting have yields per ws (§4.2) but no limit on what a band can take from the herds around it. | The game channel is either unbounded (labor-limited) or a free assumption. |
| C1-c | **No field limit.** With field labor at 1.0 ws per tile per year, a band can farm every tile. Cooking, boiling, grinding and fermentation stack to ×2.24 on grain, so a field tile feeds ~5 people. | E2 end comes out at **99 people at radius 2, 312 at radius 4** (target 88). |
| C1-d | **10 §2's sample band** (12 adults, 5 children, 3 elders) is 60% adults. 09's bands are 35–48% adults. | The anchor understates need per worker: 2.3 R per food-ws is needed, not 1.85. Rung 0 (1.9) can't feed a grown, child-heavy band. |

**Fix (applied to 10 §2 and new §3.1):**
- **Range:** radius 3 (37 tiles) through E2, the camp claim radius. The village's radius-4 claim doesn't lengthen the daily walk.
- **Herd share:** a band takes 40% of the herds' sustainable yield (scavenging and HNT-01). It rises to 0.42 / 0.44 / 0.48 with HNT-02 / HNT-03 / HNT-04, and ×1.1 with dogs. Hunting rungs mostly raise labor efficiency.
- **Field limit:** at most 25% of range tiles under fields in E2.
- **Storable shares:** nuts and seeds are 40% of an autumn plant take (FOR-03); 30% of autumn meat is dried (FOD-02).
- §2 now uses 09's age structure and says plainly that labor is a viability check and stocks set the cap.

| Checkpoint | Target | Derived | Plants | Game | Fish | Fields | R/yr per person |
|---|---:|---:|---:|---:|---:|---:|---:|
| E0 start | 20 | **18.4** (−8%) | 39 | 27 | — | — | 3.60 |
| E0 end | 27 | **27.5** (+2%) | 51 | 35 | — | — | 3.13 |
| E1 end | 41 | **42.4** (+3%) | 78 | 47 | 4 | — | 3.03 |
| E2 end | 88 | **88.4** (+1%) | 82 | 56 | 8 | 121 (9 tiles) | 3.00 |

**Watch: the E2 cap is very sensitive to field count.** Each field tile is worth ~4.3 people at the full value stack (FOD-04 boiling +20% and FOD-05 fermentation +15% applied to all grain). WS-B could narrow that by applying FOD-05 only to the brewed share (×1.03 instead of ×1.15) and FOD-04 only to boiled foods (×1.10). If it does, the field limit for E2 = 88 rises to ~34%. I didn't change it; the current limit fits the numbers as written.

## 4. C2 — late-winter food share

**Method.** At its FoodCap a band produces exactly a year's need. So its Spring–Autumn surplus equals its Winter shortfall, and:

`Lean = w + (1 − w) × keep`

- *w* is Winter production ÷ Winter need. Plant regrowth is 0.15 in Winter (×2 with FOR-02), game is steady, fish runs stop.
- *keep* is the share of stored surplus that survives ~1.5 seasons at 10 §4.6's spoilage rates, with pits (FOD-03) halving spoilage.

| Research position | 0.0 | 1.0 | 1.3 | **1.4** | 1.5 | 1.7 | 2.0 | 2.9 |
|---|---|---|---|---|---|---|---|---|
| Lean share, with the 10 §3.1 constants | 0.76 | 0.76 | 0.84 | **0.90** | 0.91 | 0.95 | 0.96 | 0.97 |
| Lean share, 09's model (× LeanBase products) | 0.77 | 0.77 | 0.96 | 1.22 | 1.22 | 1.59 | 1.59 | 1.59 (capped at 1) |

- **The base matches:** 0.76 against LeanBase 0.77.
- **Mismatch C2-a:** a share of **1.0 is unreachable** while stored food spoils. *keep* < 1 always, so Lean tops out near 0.97. 09's multipliers (product 1.59) overshoot because they treat storage as perfect.
- **It doesn't change behavior:** once Lean ≥ 0.85, the cap trigger (85% of FoodCap) fires before the winter trigger, so winter stops deciding fission. That is all 09's "1.0" was for. The derived share crosses 0.85 between E1 0.3 and **E1 0.4** (FOR-03, then FOR-02 and FOD-02), exactly where 09 places it.

**Fix (applied to 11 §6):** C2's target is now "reaches 0.85 by E1 ≈ 0.4". **Recommended for 09 §6.2** (not edited): note that LeanFood ≥ 0.85 × FoodCap means "winter no longer binds". The balance model needs no change: its behavior is identical for any lean share between 0.85 and 1.

## 5. C3 — hazards by cause

**Method.** Apply each Hazard primitive in 11 to its cause share at E0 (09 §4.2: adults / children). Then compare the hazard at the E1 and E2 midpoints with the E0 midpoint, which is where 09's tables are anchored. These are the targets:

| Band | E1 vs E0 | E2 vs E0 |
|---|---:|---:|
| Child | 0.917 | 0.972 |
| Adult | 0.917 | 1.000 |
| Elder | 1.000 | 1.075 |

Assumptions, since 09 doesn't give them:
- **Elders:** 40% senescence (no E0–E2 project acts on it); the other 60% splits like adults.
- **Injury:** 60% of adult injury happens hunting.
- **Childbirth:** 6% of the adult hazard.
- **Infants:** age 0 is 1/6 of child deaths (09 §4.1's 3× infant weight).

**As written:**

| Band | E1 got | E1 target | E2 got | E2 target |
|---|---:|---:|---:|---:|
| Child | 0.78 (**−15%**) | 0.917 | 0.71 (**−27%**) | 0.972 |
| Adult | 0.76 (**−17%**) | 0.917 | 0.64 (**−36%**) | 1.000 |
| Elder | 0.87 (**−13%**) | 1.000 | 0.81 (**−25%**) | 1.075 |

Two causes:
- **C3-a: the multipliers are population-wide at face value, and they stack.**
  - Exposure alone: FIR-01 ×0.70 · FIR-02 ×0.80 · FIB-02 ×0.85 · BLD-01 ×0.75 · BLD-03 ×0.60 · FIB-04 ×0.50^0.5 cut a 20% cause share to 3% by mid-E1.
  - Starvation: SOC-02 and WAY-02 each ×0.70.
  - Violence: GOV-01 and GOV-03 each ×0.5.

  09 §4.2 warned about this ("the catalog percentages taken at face value overshoot"). 11 scaled only some of them (FOR-01).
- **C3-b: nothing produces the Neolithic rise.** 09's E2 hazards go *up*: sedentism, herds and stored grain bring zoonoses, waterborne disease and dietary stress, and epidemics start (09 §4.1, §10.6: 3% of E2 deaths). Every Hazard primitive in 11 is below 1, so E2 can only fall.

**Fix (applied to [11](11-e0-e2-project-specs.md) on 2026-10-18, with cold deaths in the live sim coming only from 10 §9's warmth tiers):** read each 11 Hazard value as the effect on the figures it reaches, and give the population-average multiplier, which is about **(11's value)^0.25**. Add the Neolithic rise to KEY-2. With these values every band is within ±8%: child +2% / +2%, adult +1% / −5%, elder −4% / −8%.

| Project | 11 now | Recommended | | Project | 11 now | Recommended |
|---|---|---|---|---|---|---|
| FIR-01 | exposure ×0.70, predators ×0.60 | **×0.92, ×0.88** | | DEF-01 | predators ×0.40 | **×0.80** |
| FIR-02 | exposure ×0.80 | **×0.95** | | FIB-04 | exposure ×0.50 | **×0.84** |
| FIB-02 | exposure ×0.85 | **×0.96** | | BLD-03 | exposure ×0.60 | **×0.88** |
| BLD-01 | exposure ×0.75 | **×0.93** | | HNT-05 / HNT-06 | hunters' injury ×0.70 | **×0.92** |
| FOR-01 | endemic ×0.92 | **×0.98** | | HUS-01 | predators ×0.70 | **×0.92** |
| HNT-03 | hunters' injury ×0.80 | **×0.95** | | FOD-04 | age 0 ×0.85 | **×0.96** |
| FOD-01 | endemic ×0.75 | **×0.93** | | MED-01 | endemic ×0.85 | **×0.96** |
| SOC-01 | child ×0.90 | **×0.97** | | MED-02 | injury ×0.70 | **×0.92** |
| SOC-02 | starvation ×0.70 | **×0.92** | | GOV-01 / GOV-03 | violence ×0.5 | **×0.84** |
| COM-01 | predators ×0.80 | **×0.95** | | WAY-02 | starvation ×0.70 | **×0.92** |
| CHM-03 | endemic ×0.95 | **×0.99** | | MED-03 | age 0 ×0.75, maternal ×0.60 | **×0.93, ×0.88** |
| **KEY-2** | — | **Hazard(endemic, ×1.30) · Rule(epidemics begin, 09 §10)** | | | | |

**Why it isn't applied: the alternative is a real design choice.** The face values (fire −30% cold deaths) are what a player sees for a figure at a fire. You could keep them and add a **coverage** term instead: the share of the population each effect reaches, through adoption, season and condition. An effect of ×0.70 at ~30% coverage averages to ×0.91, the same as the 0.25 power. The checks only need the population average to match. The trade-offs:
- **Coverage** keeps the felt effect, but needs a coverage rule per effect (WS-C/WS-H).
- **The table** is simpler, but shrinks the visible per-figure payoff.

My recommendation is the table for the mean-field spec in 11, plus a note that the per-figure size may be larger where coverage is partial. Either way, **the KEY-2 Neolithic rise is needed**.

## 6. C4 — first fission

Standard pace, late-winter trigger, minute: Balanced 7.0 · Grower 9.0 · Builder 9.0 · Wanderer 7.0 · Warlord 8.0 · Sage 7.0. **All in 7–9.** R6 doesn't touch E0–E2, so 09 §7.3 holds unchanged.

## 7. C5 — keystone timing with the 04 openings

**Method.** Run the opening's projects at 11's list costs in order, with earlier-era backfills at EraLag 0.7, on the balance model's cumulative research curve (Insight + Know-how).

| Opening | Before KEY-1 | KEY-1 (target 7.5) | Then before KEY-2 | KEY-2 (target 17.5) |
|---|---:|---:|---:|---:|
| Balance model (N picks at average cost) | 13 | 7.75 | 14 | 17.75 · KEY-3 **29.5** (target 29) |
| Grower | 19 | **8.9** (+19%) | 19 | **18.3** (+4%) |
| Builder | 16 | **8.0** (+7%) | 21 | **17.9** (+2%) |
| Wanderer | 15 | **7.8** (+4%) | 32 | **19.6** (+12%) |

**All within ±25%.**
- The Grower's six extra E0 picks push KEY-1 latest.
- The Wanderer's 32-project route to Sedentism pushes KEY-2 latest. That matches 04 §7.3's "Sedentism arrives late".

**Mismatch C5-a: two EraBase tables are live.** 11 §2 prices projects at EraBase 20 / 45 / 70, as 02 §8.3 still says (07). The balance model runs on 09's re-fit, 22 / 50 / 75 (09 §11.1, change list #1, not yet applied to 02). At 11's prices every keystone comes ~0.5–1 min earlier: KEY-1 7.0–8.4, KEY-2 16.8–18.7. That still passes, but the two must agree.

**Fix (recommended):** apply 09 §12 #1 to 02 §8.3. Then rescale 11's cost column:

| Type | E0 | E1 | E2 |
|---|---|---|---|
| D/M | 20 → 22 | 45 → 50 | 70 → 75 |
| S | 24 → 26 | 54 → 60 | 84 → 90 |
| I | 30 → 33 | 68 → 75 | 105 → 113 |
| R | — | 36 → 40 | 56 → 60 |
| W | — | — | 140 → 150 |
| Keystone | 50 → 55 | 112 → 125 | 175 → 188 |

Then update 11 §2's sums (×1.10, ×1.11, ×1.07). I didn't edit the column because it should follow 02, not lead it.

## 8. Change list

| # | Doc § | Change | Status |
|---|---|---|---|
| 1 | params.py, workbook | R6: Territory 1,600 from E5; E7 density steps on NRG-07 and TRN-12 (×1.265 food and housing each) | ✅ done, verified |
| 2 | 10 §2 | Labor anchor at 09's age structure; stocks, not labor, set FoodBase | ✅ applied |
| 3 | 10 §3.1 (new) | Range radius 3; herd share 0.40 → 0.48; field limit 25%; storable shares | ✅ applied |
| 4 | 11 §6 C2 | Target: late-winter share ≥ 0.85 by E1 ≈ 0.4 | ✅ applied |
| 5 | 09 §6.2 | Note that lean ≥ 0.85 means winter no longer binds; 1.0 is the model's shorthand | recommended |
| 6 | 11 §3–§5 | Hazard multipliers to population-average values (§5 table) *or* a coverage rule | recommended (design choice) |
| 7 | 11 KEY-2 | Add Hazard(endemic, ×1.30) and "epidemics begin" | recommended |
| 8 | 02 §8.3, 11 §2 and cost column | EraBase 22 / 50 / 75 (09 §12 #1), then rescale 11's costs | recommended |
| 9 | 11 FOD-04, FOD-05 | Optional: narrow the grain food-value stack; if so, raise the E2 field limit to ~34% | optional |
| 10 | [07b §9](07b-territory-findings.md#9-re-run-on-the-live-trajectory-07c-follow-up-2026-10-18) | Re-run the territory model on the R6 trajectory (9 metropolises in E8–E9) | ✅ done: recommendations hold; early map emptier (07b §9.3 L2) |
| 11 | 05 §9.1 | Replace "the balance model hasn't adopted them yet" with a link here; the per-era caps there become the E7 project steps of §2 | recommended |

## 9. Regenerating
```bash
python docs/MVP/balance/tools/check_e0e2.py           # C1–C5 as written in 10/11
python docs/MVP/balance/tools/check_e0e2.py --fixed   # with the fixes above
python docs/MVP/balance/tools/sensitivity.py final    # R6 arc, archetypes × paces
```
