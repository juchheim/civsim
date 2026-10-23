# 07c — E0–E2 Checks: R6 Adopted, and 11 §6 C1–C5

> Two jobs:
> 1. Adopt [07b](07b-territory-findings.md) R6 in the balance model: territory at the map's room, with late growth vertical rather than wider.
> 2. Run the acceptance checks C1–C5 of [11 §6](11-e0-e2-project-specs.md#6-acceptance-checks-for-this-spec) against the per-project numbers in [10](10-e0-e2-actions-and-goods.md) and [11](11-e0-e2-project-specs.md).
>
> Each mismatch below comes with a concrete fix, and the fixes already applied to 10 and 11 are marked. Balance assumptions and simulation checks are reproducible with [balance/tools/check_e0e2.py](balance/tools/check_e0e2.py): run it as written, or with `--fixed` for the fixed numbers.

---

## 1. Summary

| Check | Target | As written in 10 / 11 | With the fixes | Status |
|---|---|---|---|---|
| **R6** | arc ≈ 172 min, every era in its window | — | **173.5 min**, all 10 eras in window | ✅ adopted |
| **C1** FoodCap per settlement | 20 → 27 → 41 → 88 (±10%) | **8 → 14 → 26 → 99** (−60%…+12%); not determined by 10 alone | **18 → 28 → 42 → 88** (−8%…+3%) | ✅ fixed (applied to 10) |
| **C2** late-winter food share | 1.0 by E1 ≈ 0.4 | 0.71 → 0.96, never 1.0 | reaches **0.85 at E1 0.4**; 1.0 is unreachable by construction | ✅ target restated (applied to 11) |
| **C3** hazards at era midpoints | 09 §4.1 ±10% | **−13…−17% at E1, −25…−36% at E2** | catalog proxy remains within tolerance; live child/adult ratios and E0–E2 elder e45 tail ranges pass | ✅ live C3 passes |
| **C4** first fission | minute 7–9 in mean-field; live median founding year 7–9 per archetype | **7–9** | **7–9 in mean-field; live medians 9/8.5/9/7 over 40 seeds; 68/160 runs founded in-window; 148/160 founded by year 30** | ✅ median timing pass; tails reported, no distribution threshold specified |
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

The live planner applies the field limit to the full 37-tile home range: `floor(37 / 4) = 9` fields. It checks cultivability only when selecting a field site, so barren or already occupied tiles do not inflate the limit. The rerun's E2 reference fixture seeded nine physical fields and remained within the ±10% C1 target; the fixed C1 analytical and runtime checkpoints also pass.

| Checkpoint | Target | Derived | Plants | Game | Fish | Fields | R/yr per person |
|---|---:|---:|---:|---:|---:|---:|---:|
| E0 start | 20 | **18.4** (−8%) | 39 | 27 | — | — | 3.60 |
| E0 end | 27 | **27.5** (+2%) | 51 | 35 | — | — | 3.13 |
| E1 end | 41 | **42.1** (+3%) | 78 | 47 | 2.9 | — | 3.03 |
| E2 end | 88 | **88.3** (+0%) | 82 | 56 | 7.3 | 120.5 (9 tiles) | 3.00 |

**Watch: the E2 cap is very sensitive to field count.** Each field tile is worth ~4.3 people at the full value stack (FOD-04 boiling +20% and FOD-05 fermentation +15% applied to all grain). WS-B could narrow that by applying FOD-05 only to the brewed share (×1.03 instead of ×1.15) and FOD-04 only to boiled foods (×1.10). If it does, the field limit for E2 = 88 rises to ~34%. I didn't change it; the current limit fits the numbers as written.

## 4. C2 — late-winter food share

**Method.** At its FoodCap a band produces exactly a year's need. So its Spring–Autumn surplus equals its Winter shortfall, and:

`Lean = w + (1 − w) × keep`

- *w* is Winter production ÷ Winter need. Plant regrowth is 0.15 in Winter (×2 with FOR-02), game is steady, fish runs stop.
- *keep* is the surviving food value divided by the raw food value of the modeled Spring–Autumn carry-over mix. The mix assumes harvest is proportional to that season's sustainable output, then applies 10 §4.6's per-good spoilage over ~1.5 seasons; constructed pits (FOD-03) halve spoilage.

| Research position | 0.0 | 1.0 | 1.3 | **1.4** | 1.5 | 1.7 | 2.0 | 2.9 |
|---|---|---|---|---|---|---|---|---|
| Lean share, with the 10 §3.1 constants | 0.76 | 0.76 | 0.84 | **0.90** | 0.91 | 0.95 | 0.96 | 0.97 |
| Lean share, 09's model (× LeanBase products) | 0.77 | 0.77 | 0.96 | 1.22 | 1.22 | 1.59 | 1.59 | 1.59 (capped at 1) |

- **The base matches:** 0.76 against LeanBase 0.77.
- **Mismatch C2-a:** a share of **1.0 is unreachable** while stored food spoils. *keep* < 1 always, so Lean tops out near 0.97. 09's multipliers (product 1.59) overshoot because they treat storage as perfect.
- **It doesn't change behavior:** once Lean ≥ 0.85, the cap trigger (85% of FoodCap) fires before the winter trigger, so winter stops deciding fission. That is all 09's "1.0" was for. The derived share crosses 0.85 between E1 0.3 and **E1 0.4** (FOR-03, then FOR-02 and FOD-02), exactly where 09 places it.

**Fix (applied to 11 §6):** C2's target is now "reaches 0.85 by E1 ≈ 0.4". **Recommended for 09 §6.2** (not edited): note that LeanFood ≥ 0.85 × FoodCap means "winter no longer binds". The balance model needs no change: its behavior is identical for any lean share between 0.85 and 1.

## 5. C3 — hazards by cause

**Method.** Treat the Hazard values now in 11 as population-average multipliers and apply them directly to their cause shares (11 §1). The V16 catalog proxy applies all numeric projects through E1 or E2 cumulatively; it does not model a typical-project midpoint schedule. For HNT effects with `scope: hunters`, use the §5 assumption that hunting accounts for 60% of adult injury: the adult injury multiplier is `1 + 0.60 × (mult − 1)`. Child injury is unaffected by these hunter-scoped effects; elders use the adult cause mix for their non-senescence share. These are the target ratios:

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

**Direct catalog calculation using the current 11 values:**

| Band | E1 proxy ratio | E1 target | E2 proxy ratio | E2 target |
|---|---:|---:|---:|---:|
| Child | 0.933 (**+1.7%**) | 0.917 | 0.991 (**+2.0%**) | 0.972 |
| Adult | 0.930 (**+1.4%**) | 0.917 | 0.955 (**−4.5%**) | 1.000 |
| Elder | 0.959 (**−4.1%**) | 1.000 | 0.987 (**−8.2%**) | 1.075 |

All six cumulative catalog-proxy ratios are within 09's ±10% tolerance; the E2 elder result is close to the limit. These values are applied directly: **do not raise them to the 0.25 power.** The old 0.25-power wording and its calibration results below are superseded. This proxy does not substantiate the acceptance target's midpoint comparison.

This is a catalog-level calculation, not verification of C3 in the live simulation. The live check measures the mechanisms described in 11 §1 (warmth for exposure; food security and spoilage for diet/storage; per-figure treatment and safety effects).

**Live midpoint diagnostic (40 seeds, annual-effective ratio target).** The harness samples four season ends after midpoint projects complete and holds later-position projects until each window ends. Windows are E0 ticks 2,400–2,850 after nominal tick 2,325; E1 ticks 7,800–8,250 after nominal tick 7,650; and E2 ticks 14,250–14,700 after nominal tick 14,175. The E1 schedule delays BLD-03 and FOR-04 until tick 8,400 so their .55/.60 completions do not contaminate the .50 sample. `run_headless_balance.py` stages the C1 cap schedule, the midpoint C3 projects, and seed-rotated two-of-four assignments for optional p=0.5 mortality projects.

The annual-effective target is `Mort* × (3 + WinterMult) / 4`, since 09 §4.1 defines Mort* outside winter. Child/adult acceptance compares E1/E0 and E2/E0 ratios of these annual-effective rates. The absolute E0 and E1/E2 rows are diagnostics; they are not independent gates. Rates are person-time weighted, before stochastic death sampling.

| Band | E0 rate / target (person-seasons) | E1 rate / target (person-seasons) | E1/E0 ratio delta | E2 rate / target (person-seasons) | E2/E0 ratio delta |
|---|---:|---:|---:|---:|---:|
| Child | 0.046584 / 0.045000 (5,223) | 0.040968 / 0.038775 (13,154) | +2.1% · pass | 0.038070 / 0.038500 (20,900) | −4.5% · pass |
| Adult | 0.013410 / 0.015000 (4,815) | 0.012451 / 0.012925 (4,867) | +7.8% · pass | 0.010799 / 0.013200 (8,814) | −8.5% · pass |
| Elder | diagnostic only | diagnostic only | — | diagnostic only | — |

The age-band person-time ledger has 5,223 / 13,154 / 20,900 child seasons, 4,815 / 4,867 / 8,814 adult seasons, and 932 / 769 / 2,573 elder seasons in E0 / E1 / E2. The elder exact-age ledger covers 233.00 / 192.25 / 643.25 person-years. Per-season oldest observed ages are E0 59/59/59/58, E1 68/68/68/67, and E2 79/79/78/78. The harness emits person-seasons and expected hazard by age and season, with the additive winter-shortfall hazard logged separately; [`analyze_c3_logs.py`](balance/tools/analyze_c3_logs.py) pools those rows across seeds, interpolates missing ages log-linearly by cause, and resamples whole seeds for 95% bootstrap intervals.

| Era | e45 target (±10%) | Tail-sensitivity point range | Seed-bootstrap envelope across tails | Exact-age support | Status |
|---|---:|---:|---:|---:|---|
| E0 | 15.0 (13.5–16.5) | 14.43–14.49 | 14.40–14.51 | ages 53–59 | robust within band |
| E1 | 16.0 (14.4–17.6) | 15.06–15.81 | 14.66–15.97 | ages 45–68 | robust within band |
| E2 | 16.0 (14.4–17.6) | 16.83 | 16.80–16.86 | ages 45–79 | robust within band |

Tail sensitivity scales the Gompertz cause budget and senescence while varying whether observed baseline starvation, the additive winter food-shortfall surcharge, and exposure stay flat beyond each season's oldest sampled age. The harness now reports the winter-shortfall component separately from combined starvation, so the analyzer can vary those assumptions independently. The flat-surcharge case does not imply baseline starvation stops increasing with age. These estimates are age-standardized under the observed midpoint mechanisms, but remain conditional on which ages and ecological conditions survived into the sample.

**C3 passes.** Child and adult E1/E0 and E2/E0 annual-effective hazard ratios are within ±10%; all four E0–E2 elder tail assumptions and their seed-bootstrap intervals remain within their e45 acceptance bands. The direct catalog proxy remains a separate static check. The earlier 40-seed run is superseded by the final calibration run documented in §6.

| Project | Earlier value | Adopted value | Project | Earlier value | Adopted value |
|---|---|---|---|---|---|
| FIR-01 | exposure ×0.70, predators ×0.60 | **×0.92, ×0.88** | DEF-01 | predators ×0.40 | **×0.80** |
| FIR-02 | exposure ×0.80 | **×0.95** | FIB-04 | exposure ×0.50 | **×0.84** |
| FIB-02 | exposure ×0.85 | **×0.96** | BLD-03 | exposure ×0.60 | **×0.88** |
| BLD-01 | exposure ×0.75 | **×0.93** | HNT-05 / HNT-06 | hunters' injury ×0.70 | **×0.92** |
| FOR-01 | endemic ×0.92 | **×0.98** | HUS-01 | predators ×0.70 | **×0.92** |
| HNT-03 | hunters' injury ×0.80 | **×0.90** | FOD-04 | age 0 ×0.85 | **×0.96** |
| FOD-01 | endemic ×0.75 | **×0.93** | MED-01 | endemic ×0.85 | **×0.96** |
| SOC-01 | child ×0.90 | **×0.97** | MED-02 | injury ×0.70 | **×0.92** |
| SOC-02 | starvation ×0.70 | **×0.92** | GOV-01 / GOV-03 | violence ×0.5 | **×0.84** |
| COM-01 | predators ×0.80 | **×0.95** | WAY-02 | starvation ×0.70 | **×0.92** |
| CHM-03 | endemic ×0.95 | **×0.99** | MED-03 | age 0 ×0.75, maternal ×0.60 | **×0.93, ×0.88** |
| KEY-2 | — | **Hazard(endemic, ×1.30) · Rule(epidemics begin, 09 §10)** | | | |

The adopted values are the population-average values in 11 §1; they are not per-figure values to be transformed by a power rule. KEY-2's endemic increase and the start of epidemics supply the E2 Neolithic rise in this catalog calculation.

## 6. C4 — first fission

Standard pace, late-winter trigger, mean-field first-fission times after the end-of-season timing correction are Balanced 7.0, Grower 8.0, Builder 8.0, Wanderer 7.0, Warlord 8.0, and Sage 7.0 minutes. `balance_sim.py` checks cap and late-winter pressure against post-season population, after winter demography, as 09 §7.2 and the live model do.

The final 40-seed headless balance run used seeds 1–8, 23, 100, and 200–229 and 18,000 ticks per seed. Each four-civilization seed received the canonical C1 cap schedule, the C3 midpoint projects, and KEY-1/2/3 at ticks 4,650/10,650/17,700: 50 unique project IDs and 180 completion assignments per seed. Optional p=0.5 mortality projects were assigned to two of four civilizations with a seed-rotated pairing. Grower calibration is fertility ×1.30/FoodCapacity ×1.11; Builder is fertility ×1.10/FoodCapacity ×1.07 with `LeanShareAdjustment = −0.02`; the other profiles use 0. All 40 seeds exited successfully at tick 18,000.

**C4 passes its median timing gate.** Live median first-fission years were Balanced 9, Grower 8.5, Builder 9, and Wanderer 7. A total of **68/160** runs founded in years 7–9 and **148/160** founded by year 30; 12 were right-censored. The tails are reported without a separate threshold because 11 §6 defines none.

The same run verifies **C1 FoodCap**. Mean sustainable FoodCap by era (Balanced / Grower / Builder / Wanderer) was:

| Era | Mean sustainable FoodCap |
|---|---|
| E0 | 27.51 / 31.58 / 29.62 / 26.16 |
| E1 | 43.11 / 48.73 / 46.62 / 41.20 |
| E2 | 81.12 / 88.25 / 84.73 / 79.17 |

Against the archetype-adjusted E2 targets 88 / 97.68 / 92.4 / 83.6, the means are −7.8% / −9.7% / −8.3% / −5.3%, all within C1's ±10% tolerance. The neutral nine-field E2 runtime fixture remains at 90.21 versus target 88 (+2.5%).

**The live population gate passes.** `exact_age_sensitivity.py` supplies an exact-age expected-demography reference with exogenous C1 research positions and ideal food/health. It retains age/sex cohorts, pregnancy countdown, seasonal aging, and fission. All 12 live means are within ±15% of the controlled reference; Grower E2 is now −4.9%, with the reference inside its 95% confidence interval. The table shows 40-seed live population means with t-based 95% confidence intervals, the controlled reference, and delta:

| Archetype | E0 live [95% CI] / reference (Δ) | E1 live [95% CI] / reference (Δ) | E2 live [95% CI] / reference (Δ) |
|---|---:|---:|---:|
| Balanced | 20.250 [19.201, 21.299] / 20.70 (−2.2%) | 32.325 [29.785, 34.865] / 34.39 (−6.0%) | 61.300 [55.324, 67.276] / 66.02 (−7.1%) |
| Grower | 23.375 [22.306, 24.444] / 24.12 (−3.1%) | 39.825 [37.273, 42.377] / 43.33 (−8.1%) | 93.400 [86.020, 100.780] / 98.24 (−4.9%) |
| Builder | 21.225 [20.172, 22.278] / 21.90 (−3.1%) | 36.375 [34.198, 38.552] / 37.59 (−3.2%) | 71.975 [66.867, 77.083] / 76.67 (−6.1%) |
| Wanderer | 20.900 [19.802, 21.998] / 20.45 (+2.2%) | 32.375 [30.459, 34.291] / 34.18 (−5.3%) | 59.150 [54.447, 63.853] / 65.34 (−9.5%) |

The old continuous `C / 56` mean-field comparison is superseded for this gate; the exact-age reference is the controlled comparator under 09's ideal-food and ideal-health assumptions. It omits live ecology, food shortfalls, project-specific cause coverage, and stochastic events, so the pass is specific to this controlled comparison.

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

## 7a. C5 in the live sim (M4, 2026-10-19)

**Method.** `civsim-harness research --pack FILE --check-c5` plays four **Balanced** civilizations (the balance model's reference) on the Council for 40 minutes of the real living economy (M3) driven by the real research system (M4). The median civilization must land in ±25% of 7.5 / 17.5 / 29 minutes and none may stray beyond ±40%. The scripted openings are played by `--bot grower|builder|wanderer` (civilization 0 follows the opening, then hands back to the Council; the other three stay on the Council).

| Run | KEY-1 (target 7.5, window 5.6–9.4) | KEY-2 (17.5, window 13.1–21.9) | KEY-3 (29, window 21.8–36.2) |
|---|---|---|---|
| Council, seed 20261019, four civs | median **6.3** (6.1 6.3 6.3 6.6) | median **19.3** (18.6 19.1 19.6 19.7) | median **32.6** (29.8 32.1 33.0 34.0) |
| Council, seed 777, four civs | median **6.6** (6.0 6.5 6.6 6.8) | median **20.5** (20.0 20.2 20.7 21.6) | median **34.4** (34.0 34.2 34.6 34.6) |
| Grower opening (civ 0) | **8.5** (+13%) | **18.4** (+5%) | **31.6** (+9%) |
| Builder opening | **7.3** (−3%) | **19.8** (+13%) | **35.4** (+22%) |
| Wanderer opening | **8.0** (+7%) | **20.9** (+19%) | **35.6** (+23%) |

**All within ±25%; C5 passes.** Know-how is 17–19% of research progress (target 13–20%). The unit test `KEY_1_lands_inside_the_C5_window_…` runs the first 9.5 minutes in `dotnet test`; the full check is the scheduled workflow `research-acceptance.yml`.

**What the live sim showed, in the order I found it.**
1. **The oracle's adults are smoother than the live ones.** M3 ages people exactly, so the founding adults (18–32) stay at 8–9 until the first-born reach 14 at minute 14, and the first adults start turning 45 at minute 13. The balance model's `C / 56` flow gave 13 adults at minute 15; the live sim has 8–9. Children contribute nothing, so Insight is flat at about 40 a year for 14 minutes, then climbs. The balance model also granted two things for free from the start of E1 that the live sim must research: the Elder and Shaman roles (GOV-01, BEL-03: +20 Insight a year per settlement) and full Insight sharing between daughter bands (SOC-03: +33% at two settlements).
2. **Slots must cascade.** Refilling a freed slot in place left a keystone prerequisite chain (STN-01 → 02 → 03 → 04) in the 1/7 slot for minutes: KEY-1 arrived at minute 12 to 20 with 22 projects done against a breadth of 13. The cascade of 08 §7 (the others slide up, the queue fills Tertiary) and a Council that ranks the critical path first removed the 2× spread between civilizations.
3. **A Council that ignores wisdom is slow.** GOV-01 + BEL-03 cost 115 Insight and return +20 a year per settlement, a payback of under three minutes. The Council now counts Insight gain in `Opportunity` (02 §15).
4. **Calibration.** Even so, the live sim needs more Insight than the oracle's formula gives. Scanning a uniform scale on the whole formula (Commons, roles, structures; four Balanced civs, Council, KEY-1 / KEY-2 / KEY-3 in minutes):

| Scale | KEY-1 | KEY-2 | KEY-3 |
|---:|---|---|---|
| ×1.0 | 9.5–10.7 | 26.6–28.1 | not by minute 34 |
| ×1.3 | 7.5–8.3 | 22.0–23.9 | not by minute 34 |
| ×1.5 | 6.5–7.1 | 19.7–21.2 | 32.7–33.7 (two of four) |
| **×1.6 (adopted)** | 6.0–6.8 | 18.6–21.6 | 29.8–34.6 |
| ×1.7 | 5.9–6.2 | 18.1–19.9 | 28.8–32.0 |

**Adopted: `InsightCalibration` = 1.6** (`content/constants/balance.yaml`, applied to the whole formula, as ×8 was in 07 F1). It is a re-fit to the live demography, not a design change to 02 §8.1. 1.7 centers KEY-2 and KEY-3 better but puts KEY-1 at the edge of its window; 1.6 keeps all three inside on both seeds.

**Consequences and limits.**
- M4 did not change the Python balance model (`params.py`); it still describes smoothed demography, so its original E0–E2 durations (7.8 / 10.0 / 11.8 min) do not directly predict the live sim, in the same way the exact-age reference superseded the `C / 56` comparison for population (§6). E2's cost was subsequently retuned in §10. EraBase for E3 and later was fit with the balance model's Insight; re-fit it when those eras run live (M8).
- **Stand-ins until their systems exist:** practice that is not an M3 job (talk, ritual, teaching, counting, marking; `AmbientPractice` in `LivingEconomySource`) is a flat background of work-seasons per adult; resource exposure for `[Seen]` gates is "within routine reach of a settlement" (6 / 9 / 12 tiles by era, ×1.5 with WAY-01, ores within 25 with INQ-03) because M3 figures have no line of sight; Elder and Shaman holders are one per settlement with an elder or six adults; contact is not simulated (M5 calls `RecordContact`).
- **The 04 openings carry more picks before KEY-1 than the breadth rule needs** (Grower 19, Builder 16, Wanderer 15; costs 521 / 438 / 418 including the keystone, against 363 for the cheapest route: KEY-1's 11 closure projects and the keystone cost 319, and two more free picks reach the breadth of 13). They pass because Know-how and the cascade make up the difference.
- **P8** (a Council-only player loses to an engaged one in ≥ 65% of paired matches) uses the Settler (learning) AI as its reproducible moderate-engagement proxy. It wins 53/64 mirrored Council matches (83%) over 32 seeds at 40 minutes, passing the target. The simpler scripted Grower policy wins 39/64 (61%) and is too weak; Chieftain wins 94% and is too strong for this proxy. This is a simulation balance gate, not a human-playtest result; P9 measures actual players.
- **C8** is checked in the sim's event stream: every one of the 148 numeric projects emits `Completed`, `Moment` and at least one `VisibleChange` on completion (`Every_numeric_project_announces_a_visible_change_when_it_completes`), and every completion in a real match does too.

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
| 8 | 02 §8.3, 11 §2 and cost column | EraBase 22 / 50 / 75 (09 §12 #1), then rescale 11's costs | applied at M4; Roots later retuned in §10 |
| 9 | 11 FOD-04, FOD-05 | Optional: narrow the grain food-value stack; if so, raise the E2 field limit to ~34% | optional |
| 10 | [07b §9](07b-territory-findings.md#9-re-run-on-the-live-trajectory-07c-follow-up-2026-10-18) | Re-run the territory model on the R6 trajectory (9 metropolises in E8–E9) | ✅ done: recommendations hold; early map emptier (07b §9.3 L2) |
| 12 | 02 §8.1, §8.2, §15; `balance.yaml` | M4: `InsightCalibration` 1.6; the slot cascade; Council critical-path depth, Insight gain, AI re-rank (§7a) | ✅ applied |
| 11 | 05 §9.1 | Replace "the balance model hasn't adopted them yet" with a link here; the per-era caps there become the E7 project steps of §2 | recommended |

## 9. Regenerating
```bash
python docs/MVP/balance/tools/check_e0e2.py           # C1–C5 as written in 10/11
python docs/MVP/balance/tools/check_e0e2.py --fixed   # with the fixes above
uv run --no-project --with numpy python docs/MVP/balance/tools/exact_age_sensitivity.py # ideal-food/health expected-demography baseline
python docs/MVP/balance/tools/sensitivity.py final    # R6 arc, archetypes × paces
```

## 10. Roots cadence follow-up (2026-10-22)

Play feedback reported that research projects completed too quickly in Roots (E2). Raise its `EraBase` from 75 to 90, a 20% increase in the list cost of each E2 project. No other era's base cost or the Insight formula changed.

The tuned season-by-season model puts E2 at **13.0 minutes**, the upper edge of the 11–13 minute target, with the first three eras' keystone proxy at **30.8 minutes** (previously 29.5). This gives Roots projects more room to play out while keeping the existing KEY-3 timing window. The tuned E2 base is updated in `balance/tools/params.py`; repeat the live C5 acceptance run before treating the resulting keystone timing as measured.
