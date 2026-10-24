# 07d — E3 Bronze Balance Findings (WP2)

> Work package WP2 of [22 §20](22-bronze-age.md#20-work-breakdown): the Python oracle extended through E3 (22 §15.2 items 1–7), the open parameters fitted, and every §15.1 target T1–T28 that an oracle can reach reproduced or flagged. Reconciled with [23](23-e3-bronze-specs.md) (WP1, the E3 registries and project specs).
> In the style of [07](07-balance-findings.md), [07b](07b-territory-findings.md) and [07c](07c-e0-e2-checks.md). Design and tooling only: nothing in `src/`, `content/` or docs 22/23 was edited. Live measurements were taken from a scratch copy of commit 4cb188d (the main checkout's `src/` was being edited), §9.
>
> **Earlier cadence follow-up (2026-10-24; superseded):** §10 first raised E2 from 75 to 100 and retained E3=160. Its projected KEY-3/KEY-4/E3 times were 36.75 / 49.75 / 13.0 minutes. Those projections are historical. The current follow-up raises E2/E3 to 150/400 and E4–E9 further; the live-mode oracle projects KEY-3/KEY-4/E3 at 41.5 / 61.25 / 19.75 minutes. It also estimates Primary intervals of 1.15 minutes in Roots and 1.18 in Bronze. These are projections, not live acceptance results. The measurements and findings below use earlier costs and need fresh acceptance runs. See [07c §10.1](07c-e0-e2-checks.md#101-roots-and-bronze-still-finish-too-quickly-2026-10-24).

---

## 1. Summary

| # | Finding | Consequence |
|---|---|---|
| **F1** | **Historical E3 fit: 200 → 150.** At 200 the live-mode E3 lasted **16.5 min**; that fit targeted the earlier 13-minute Bronze window. The 1.20× Council overhead and other measurements below still explain why that fit differed from the original Oracle projection. The current cadence in 07c §10.1 supersedes the 150 value; its projections need fresh live acceptance. | Previous T4 → **150** (D/M 150, S 180, R 120, I 225, W 300, K 375); retained as a historical fit |
| **F2** | **Live settlements are housing-bound, not food-bound.** The live planner builds dwellings just in time (`HousingCap < pop + 8`), so a village's effective cap is 40 + 8 = 48 and it **buds at ~41 people** while its FoodCap is 100–190 (measured: median effective cap 45–49, food cap 104–125, 98% of settlements housing-bound). This is the real cause of 22 §1.4 finding 3 (the map fills 2–3× faster than 07b), and it makes E3's food caps irrelevant to village size | Without a change **no settlement ever reaches the Town tier (≥ 60)**; 23 §11.2 "food binds in every tier; housing never binds" is wrong for the live sim. **R2** below |
| **F3** | **People at KEY-4 ≈ 165 (live ~170), not 220.** Growth is fertility-limited (6%/yr), E3 food caps don't bind (F2) | T10 → **170 (120–230)** |
| **F4** | **A City (≥ 120) is unreachable by KEY-4** at 1 : 1 with 165–200 people and 5–8 settlements; with R2 the capital reaches 60–90 (a Town). T10/T11/B17's "capital 120–250 + 5–10 settlements" cannot all hold at ~170 people | City tier → **≥ 90**; B17 → ≥ 1 Town in ≥ 75% of civs, ≥ 1 City in ≥ 25% at the chapter end |
| **F5** | The §7.5 raid tables reproduce within ±10 points **except three cells**; chariots match only if crew shields stack with the chariot (×0.7 × 0.5) | T21: three cells move (§6); chariot rule clarified |
| **F6** | The §7.4 investment rule gives a 2-season-stores town a **median fall at season 2** (fails T22's 3–6) | **Rationing:** an invested town eats 75% of need (R5); then 2–3 seasons of stores fall in 3–6 seasons in 65–98% |
| **F7** | **Secession never happens** under §8.3 as written (0.000 per civ per E3 in every stress case): base 60 + hearth 5 + feast 8 (both on every closure) puts typical C at 63–80, and the quarter-step plus 8-season timer filters every single crisis | R7: captured settlements start at C 25, −35 recovering +1 a season, without our hearth/feast/shrine/GOV-03 terms → ~0.05 per civ per E3; T25 restated |
| **F8** | C1-E3 mechanisms as written reach **160 (−16%)**, not 190 | R4: field limit 40%, canals reach 75% of a river settlement's fields → 177 (−7%) |
| **F9** | Metal demand (T17) reproduces 22 §5.6 exactly; T18 holds with the 10% push cap; **fuel pressure (T19) appears only above ~70 people per radius-3 range**, so villages (F2) never feel it; dormancy (B12) is a 6–9-year squeeze after a fresh re-kit | T19 measured on Towns; B12 restated |
| **F10** | Territory (T11, T14, T15, T16, T20) and Legacy (T24) land inside their targets on the live trajectories, with Found decrees | — |

## 2. The live baseline (input to everything)

`live_data.py` holds 8 seeds × 4 Balanced Council civs × 64 minutes of the live sim (`docs/MVP/balance/live-e3-baseline.csv`, 2,048 rows; probe source in `tools/probe/`). E3 projects are inert (`MaxEra = 2`), so after KEY-3 this is "E2 economy, E3 clock".

| Minute | 30 | 35 | 40 | 45 | 50 | 55 | 60 |
|---|---:|---:|---:|---:|---:|---:|---:|
| People per civ (mean) | 64 | 85 | 114 | 154 | 203 | 266 | 344 |
| Settlements per civ | 2.4 | 3.0 | 4.1 | 6.0 | 8.2 | 10.9 | 14.9 |
| Largest settlement | 34 | 36 | 36 | 37 | 39 | 41 | 40 |
| Effective cap / FoodCap (median) | 49 / 128 | 48 / 125 | 48 / 120 | 44 / 111 | 45 / 113 | 43 / 107 | 44 / 104 |
| Insight per year | 230 | 304 | 374 | 443 | 604 | 805 | 984 |
| Elder+Shaman share of Insight | 21% | 32% | 32% | 32% | 33% | 35% | 36% |
| Claimed land | 13.6% | 16.8% | 22.1% | 27.2% | 35.7% | 46.7% | 60.6% |

KEY-1 / 2 / 3 medians **6.6 / 20.8 / 34.0** (32 civs; KEY-3 range 30.3–44.1). Know-how 17.6% of progress. Progress spent ÷ oracle requirement: **1.08 / 1.18 / 1.20** by era; progress ÷ Insight 1.14–1.20. Zero raids in 64 Council minutes on every seed.

## 3. balance_sim live mode and the EraBase[3] fit (T1–T11)

**Model** (`balance_sim.py live`): per-settlement exact-age cohorts (the 07c §6 reference, vectorized), live hazards ×1.07 (fitted: ecology, shortfalls), the live housing rule (one dwelling of 4/6/8/8 by era before a village buds), InsightCalibration 1.6, Commons ×1.10 (fitted: hearth share, Era Boons), Elder/Shaman per settlement as the live source counts them (from research positions 2.0 / 2.4, fitted), E3 roles by structure (23 §9: the capital's Scribe from COM-05, 2 per School, a Priest per Temple in each Town/City, +8 per City, SOC-08 +10%), Know-how 0.314 × calibrated Commons, and the E3 requirement from 22 §12.3's closure (18 projects, 21.4 type units, 3 free picks, KEY-4, the 20 backfills at EraLag) × the Council overhead. E0–E2 overheads are fitted to the live keystones; **E3's is the measured E2 spend ratio 1.20 × the model's progress/Insight ÷ the live one = 1.25**.

**Validation** (E3 effects off, against §2): KEY-1/2/3 6.8 / 21.0 / 34.0; people −4…+1% at 30–60; Insight −6…+9%; settlements −15…−25% at 50–60 (uniform cohorts split less than lumpy live ones; carried as a bias on T11).

**Fit** (`fit.py live`): E3 = 13.0 min at **EraBase 142.7 (towns housing) / 145.6 (live villages)**.

| EraBase E3 | E3 min, Council overhead 1.25 (measured) | E3 min, no overhead (23 §20.4's reading) |
|---:|---:|---:|
| 140 | 13.0 | 11.3 |
| **150** | **13.5** | **~11.8** |
| 170 | 14.8 | 13.0 |
| 200 | 16.5 | 14.3 |
| 240 | 18.3 | 16.3 |

**Recommended 150**: inside 11.5–14.5 under both readings and both housing regimes. Cross-check by hand: 23 §20.4's 6,480 progress × 1.20 = 7,780; at the live 400 Insight/min × 1.16 progress per Insight that is **16.8 min at 200**.

**T-table at EraBase 150** (towns housing, R2; village values where they differ):

| T | Target | Oracle | Verdict |
|---|---|---|---|
| T1 KEY-3 | 29 ±25% | 33.8 (live 34.0) | ✅ (off-centre; E0–E2 unchanged) |
| T2 E3 duration | 13.0; median 11.5–14.5 | 13.5 | ✅ |
| T3 KEY-4 | 43 ±25% | 47.3 | ✅ |
| T4 EraBase | 200 | **150** | **changes** |
| T5 Insight/min | ~330 → 650–800 | **265 → 555** | restate **270 → 550 ±20%** (the live KEY-3 is later but E2 roles are fewer) |
| T6 Know-how | 13–20% | 16% | ✅ |
| T7 E3 roles | 12–20% | 21% towns / 18% villages | ✅ at the edge; 23 §20.9's lever (School 2 → 1) if WP7 measures > 20% |
| T8 completions / backfills | 22–25 / 11–20 | 22.6 / 14 | ✅ |
| T9 cadence; Mandate | 25–35 s; 7–9 | 35 s per in-era completion (21 s with backfills); 9.0 | ✅ |
| T10 people at KEY-4 | 220 (170–300) | **165** (live interpolated ~170) | **restate 170 (120–230)** |
| T11 settlements at KEY-4 | 7 (5–10) | towns 4 (+bias ≈ 5) + Founds ≈ 7; villages 5 (+bias ≈ 6.6) | ✅ with Found decrees (§7) |

**Sensitivity of the E3 duration to 20% errors:** E3-window Insight ×0.8 / ×1.2 → +17% / −13%; Council overhead 1.0 / +20% → −13% / +13%; fertility ×0.8 / ×1.2 → +13% / −12%; all Insight ×0.8 (KEY-3 moves too) −2%; E3 roles off +2%. The overhead and E3 Insight are the two things WP5's first live E3 run must measure; re-fit with `fit.py live` from them.

## 4. C1-E3 and C3-E3 (`check_e3.py`; T12, T13)

**C1-E3 as written: 160 (−16%).** Contributions at E3 end (mechanism vs 09 target): ard field limit 25 → 35% ×1.16 (1.17 ✓); irrigation on half the fields of half the settlements **×1.07 (1.17 ✗)**; manuring (soil equilibrium 14/(14 + loss): 0.70 → 0.79) ×1.07 (1.06 ✓); pastures on 15% of the range ×1.0–1.05 (herd targets 1.18 ✗, pasture displaces forage); granaries ×1.04; dairying ×1.02. The shortfall is irrigation and pasture, as 23 §20.6 / §22 item 4 suspected. **R4:** field limit **40%** with the ard, and canals reaching **75%** of a river settlement's fields ("within 2 tiles of the river" covers most of a radius-3 range) → **177 (−7%) ✅**. Re-state per river share as 23 suggests if geography gives fewer river settlements. Note F2: in villages FoodCap does not bind, so C1-E3 matters for Towns (R2) and lean-season security, not for village size.

**C3-E3 ✅:** child E3/E0 0.798 vs 0.803 (−0.7%); adult 0.758 vs 0.779 (−2.8%); e45 16.9 vs 17 (from the E2 proxy, MED-04, CHM-05, GOV-06, GOV-04's famine relief, MET-04's smith risk and WinterMult 1.25). It hinges on the winter multiplier falling 1.4 → 1.25 through granaries and mudbrick; the live check is `run_headless_balance.py` once MaxEra is 3.

## 5. Metal and fuel (`metal_sim.py`; T17–T20)

Per settlement and season: kits by job (tool share 0.3 / 0.5 / 0.7 / 0.9, mean 0.6), wear by tool work, the 23 §5 recipes in order, bellows from FIR-09, the 10% push cap (23 §7.3), the 50% upgrade remelt and ×1.4 dormancy tail (23 §4.1), charcoal from standing stock only (23 §7.4), forest 13 tiles × 30 L, regrowth 0.5 L (×2 coppiced), deadwood to hearths and kilns.

| T17 per 100 people per year | Tin, kiln | Tin, bellows | Arsenical | 22 §5.6 |
|---|---:|---:|---:|---|
| Bronze (tools + arms) | 0.83 L (0.57 + 0.26) | 0.83 L | 1.11 L | 0.8–1.0 ✅ |
| Tin | 0.083 L | 0.083 L | — | 0.08 ✅ |
| Charcoal | 3.6 L | 2.3 L | 2.6 L | 2.3–4.5 ✅ |
| Wood for metal | 22 L | 15 L | 16 L | 20–26 / 14–19 ✅ |
| Metal labor | 5.0% | 3.6% | 3.8% | 3–4% (✅ with bellows) |

Arms are 0.26 L, not 22's 0.15: 4 warriors' kits over 6 years is 0.67 kits × 0.3 L = 0.20 L before battle losses (22's own inputs). 23 §4.2's spare-kit rule keeps it there.

**T18 ✅** at the 10% cap: 100% on rung 6 within 12 seasons of MET-05 (70% after 7); arsenical 70% after 4. At 5% only 62%: the push cap is load-bearing (`MetalPushLaborCap` 0.10 confirmed, ≥ 0.08 needed).

**T19 depends on settlement size, not on metal:**

| People per radius-3 range | 40 | 60 | 80 | 100 |
|---|---:|---:|---:|---:|
| Standing wood drawn down by KEY-4, no WOD-04 | 0% | 0% | 20% | 62% |
| with WOD-04 | 0% | 0% | 0% | 0% |
| Metal output lost to fuel | 0 | 0 | 0 | 0 |

Steady non-metal demand (hearths, kilns, building) crosses the range's ~52 L/yr of regrowth and deadwood at ~70 people; the re-kit adds ~10–20 points but is spread over 8–12 seasons of regrowth, so 23 §20.8's "the re-kit is the fuel event (~47–60%)" is too high once regrowth is counted. Villages (F2, ~25–40 people) never feel fuel pressure. **T19 → measured on Towns of 80–100: 25–70% without WOD-04, < 5% with it.** ±20% on non-metal wood moves the 100-person drawdown 32–82%; ±20% forest tiles 25–82%: T19 is the least robust target here.

**Dormancy (B12):** after a full E3 on tin bronze, tin cut at KEY-4: 100% still on rung 6 after 12 seasons with no growth, 84% in a growing village (new adults get copper), 50% after ~30–36 seasons. A fresh re-kit synchronises kit ages, so 23 C6's "first fallbacks within a season" holds only for old, mixed stocks. **B12 → "within 12 seasons the new and heaviest tool users fall back (≥ 10%) and the 'no tin' reason shows; half of rung 6 is gone within 9 years."** The embargo is a slow lever by design; in a 3-year countdown it bites through growth, arms and new settlements, not existing kits.

**T20 ✅** (territory_sim e3, §7): own tin mine by KEY-4 53–66% of civs, a Tin Folk village within compact reach 78%, one or the other 100%; arsenical fallback needed by 0% under that (optimistic) compact rule. **E3 length vs metal supply: < 2%** — Insight is headcount-driven and Know-how is 35% of Commons whatever is practised; metal moves labor, arms and fuel, not research pace.

## 6. Combat (`combat_e3.py`; T21, T22)

A **bit-exact port** of `Combat.Fight`, `Raids.Resolve` and `CombatTables` (Fix64 Q32.32, Pcg32, Hash64): with the E3 extensions off it reproduces `politics --report-combat` to the last digit (83.3 / 36.6 / 12.2 / 4.5% success; capture 0 / 100 / 10.3 / 100%). E3 rules: weapons 1.8 / 2.1 / 2.2 / 2.3, training ×1.3, shields ×0.7 casualties, chariot crew ×2 strength and ×0.5 casualties, barrier multipliers replace each other (23 §1.2), loot ×0.25 behind hillfort or walls. 4,000 trials a row, seed 20261019.

| §7.5 cell | doc 22 | port | |
|---|---:|---:|---|
| 12 bronze vs village, none, stone militia | 93 / 9 / 24 / +33 | 94.5 / 6.9 / 23.9 / +34 | ✅ |
| palisade + watchers, stone militia | 13 / 39 / 8 / −16 | 16.5 / 37.4 / 8.7 / −14 | ✅ |
| palisade + watchers, bronze militia + shields | 2 / 45 / 3 / −23 | 2.8 / 44.5 / 3.4 / −22 | ✅ |
| palisade + watchers + 8 bronze warriors | 0 / **95** / 2 / **−43** | 1.3 / **60** / 2.6 / **−29** | **raiders hurt → 60, net → −29** |
| hillfort + watchers + 8 warriors | 0 / **93** / 1 / −42 | 0.3 / **83** / 2.0 / −38 | **raiders hurt → 83** |
| walls + watchers + 8 warriors | 0 / 93 / 1 / −42 | 0.1 / 93 / 1.6 / −42 | ✅ |
| the 12 with stone spears, vs none | **53** / 21 / 16 / +9 | **63** / 18 / 17 / +15 | **success → 63** (10.2 points) |
| 8 bronze vs camp of 6 · + 2 guards + watch | 97 · 99 | 96.5 · 99.5 | ✅ |
| vs hamlet 15 stone · bronze + thorn + watch | 100 · 89 | 100 · 89.2 | ✅ |
| 20 bronze+shield vs 20 / **28** stone warriors | 100 / **33** | 100 / **18** (27 ≈ 50%) | **→ 18%**; worth 1.40× ✅ |
| 12 chariot crew vs 24 / 18 bronze infantry | 39 / 100 | 37 / 100 **only if crew shields stack** (×0.35); 0 / 100 with ×0.5 alone | **rule: shields and chariot casualties multiply** |

**T22 ✅ with one fix.** Capture within a season at 1.5× (incl. defenses): palisade or hillfort, stone or bronze militia: **100%** (1.0×: 1–86%); a palisaded village of 40 bronze militia needs **61** bronze warriors ✓ 22 §7.3. The levy (220 people, 10% warriors, 80% in reach) raises ~36 figures, strength 84 ≥ 1.2 × 58: investment allowed against a walled village of 40. **Investment as written:** stores 2.0 → in 3–6 seasons only **32%** (68% fall by season 2); 2.5 → 65%; 3.0 → 99%. **R5: an invested town rations to 75% of need** (or equivalently counts gardens and herds inside the walls): 2.0 → 65%, 2.5–3.0 → 98%, median 3–4 ✅. Alternative 0.25 / 0.50 capitulation gives 49–93%. Unsupplied besiegers (10% desertion) lift after ~5 seasons but the town has usually fallen.

## 7. Territory (`territory_sim.py e3`; T11, T14–T16, T20)

The 07b model on the live trajectories (per civ, per minute), with: the live planner's site rule (best food site 7–14 from the parent, ≥ 7 from any settlement; without it the model over-claims by 25–55%), 22 §5.1 deposits on the symmetric map (middle field 12–16 from both starts, far placers 22–32 on the outer flank, arsenic 15–25, copper 8–15), mining camps (radius 1, 0.75 √6) opened on deposits within 8 (12 with carts) walking tiles, Tin Folk (24 people, radius 3) on far placers ≥ 8 from any settlement at the world's first KEY-3, the Town tier on the capital (R2), Found decrees at E3 0.25 and 0.60 toward unclaimed tin. Validation: claimed land model / live 14.8/13.6, 19.1/16.8, 23.8/22.1, 32.1/27.2, 40.8/35.7% at minutes 30–50 (the model runs ~15% high; corrected below).

| At KEY-4 (32 civs, 8 seeds) | Villages, no decrees | Villages + 2 Founds | Towns (R2) + 2 Founds | Target |
|---|---:|---:|---:|---|
| T11 settlements (median, p10–p90) | 6.5 (5–9) | 8.5 (7–11) | 7 (6–9) | 7 (5–10) ✅ |
| T14 claimed land (bias-corrected) | 35% → ~30% | 45% → ~39% | 41% → ~36% | 30–42% ✅ |
| T15 paired borders touch by own KEY-4 | 50% | 97% | 62% | ≥ 75% (✅ with Decrees) |
| T16 E3 foundings by Decree | — | 36% | 51% | 20–40% (towns → **25–50%**) |
| Tin Folk villages per map | 3.1 of 4 far placers (islands not modeled: +1–2) | | | 3–6 ✅ |

Median first border touch is minute 35–39, early in E3: Bronze is indeed the age when borders meet (22 §1.4).

## 8. Legacy (`legacy_scale.py`; T24) ✅

Chapter end (first KEY-4 + 12 seasons) median **46.5 min** (43.2–49.4; 22 §3.2: 44–48 ✅). Dominion from the territory runs (07b R8 claimed-land scoring), completions from the live keystones, Firsts to whoever finishes each project first.

| Share at chapter end | Mean (p10–p90) | Target | Old rule |
|---|---|---|---:|
| Dominion | **47.5%** (43–52) | 45–55% | 35% |
| Mastery | **33.6%** (30–38) | 25–35% | 12% |
| Firsts | **16.2%** (12–21) | 12–20% | 53% |
| Wonders | **2.7%** (2–3) | 0–8% | 0% |

Per civ: Dominion 1,134, Mastery 790, Firsts 394 (most 750, least 180), Wonders 62. ±20%: E3 completions ×1.2 → 46 / 34 / 17 / 3; Dominion ×0.8 → 42 / 37 / 18 / 3 (Dominion is the only term that can leave its band). B-D3's units hold.

## 9. Cohesion (`cohesion_sim.py`; T25)

Layouts from §7 (distance to the capital median 19, p90 37 walking tiles), research positions from §3, ASSUMED event rates: harsh winter 30% × shortfall 15% (villages sit at ~⅓ of FoodCap), epidemics 0.049 per settlement-year by 09 §10 class, raids 0.03 per settlement-year and captures 0.10 per civ per E3 (from T23's targets).

| Case | Mean C (p10–p90) | Secessions per civ per E3 |
|---|---|---:|
| 22 §8.3 as written | 70.5 (63–80) | **0.000** |
| no administration projects | 64 (51–77) | 0.000 |
| every civ captures a distant settlement | 69 (61–79) | 0.000 |
| famine ×3, raids ×3, epidemics ×2 | 69.5 (61–79) | 0.000 |
| base 50 instead of 60 | 62 (55–71) | 0.000 |
| **R7** (captured at C 25, −35 recovering +1 a season, no hearth/feast/shrine/GOV-03 terms while conquered) | 70 (62–79) | **0.034** (captures 0.10 per civ) |
| R7, every civ captures one distant settlement | 68 (61–80) | 0.37 (≈ 37% of captured distant towns secede) |
| R7, captures 0.15 per civ, famine ×3, raids ×3 | 69 (61–80) | 0.078 |

Typical C runs 63–80 (22 says 55–70): hearth and feast are on every closure, so the "typical" settlement already has +13. Either accept 60–80 or lower the base to 50 (→ 55–71, matching 22's words, still no secession). Secession needs a target below ~10 for ~3 years; no single E3 crisis does that, and the quarter step plus the 8-season timer absorb stacked ones. **T25 → typical 60–80; secessions ≈ 0.03–0.08 per civ per E3 (one in every 2–4 Chieftain matches), almost all captured distant towns; cascade (> 2 in 2 years) < 2% ✅ (0.0%).** 0.1–0.5 would need ~1 capture per civ per E3, far above T23. Sensitivity (±20% on famine weight, Overreach divisor, span penalty, raid rate, base): mean C 60–70, secessions stay 0.

## 10. Final constants for the C# implementation

| Constant | Value | Was (22/23) | Section |
|---|---|---|---|
| EraBase E3 (`eras.yaml base_cost`) | **150** | 200 (P) | §3 |
| E3 costs D/M · S · R · I · W · K | **150 · 180 · 120 · 225 · 300 · 375** | 200 · 240 · 160 · 300 · 400 · 500 | §3 |
| Town tier `min_pop` / City `min_pop` | 60 / **90** | 60 / 120 | F4 |
| Town housing rule (R2) | a settlement with a Market builds housing ahead to 0.85 × min(FoodCap, GroupCap) + 8 and buds only at 85% of min(FoodCap, GroupCap); villages unchanged | — (23 §11.2 assumed food binds) | F2 |
| Ard field limit | **40%** of range tiles | 35% | §4 |
| Canal reach | fields within 2 tiles of a river (≈ 75% of a river settlement's fields) — check it is not "half" | "half" in 22 §9 | §4 |
| `MetalPushLaborCap` | 0.10 (≥ 0.08 needed) | 0.10 | §5 |
| `UpgradeRemeltShare` · dormancy tail | 0.5 · ×1.4 | same | §5 |
| Chariot + shield casualties | multiply: crew with arms kits take ×0.35 | unstated | §6 |
| Investment rationing (R5) | invested town eats **0.75** of need | — | §6 |
| `InvestCapitulateLow` / `Zero` | 0.35 / 0.70 (unchanged with R5) | same | §6 |
| Captured settlement Cohesion (R7) | start **C 25**; conquered term **−35, +1 a season**; no hearth/feast/shrine/GOV-03 terms while it lasts | −20, +2 | §9 |
| `CohesionBase` | 60 (or 50 if 22's "55–70" wording is binding) | 60 | §9 |
| Mining camp claim | radius 1, Emit 0.75 √6 = 1.84 | same | §7 |
| Found decree rule | as 22 §4.3; AI Strategist ≤ 2 Founds per E3 | — | §7 |

## 11. Targets that change (doc 22 §15.1)

| T | Was | Now | Why |
|---|---|---|---|
| T4 | EraBase 200 | **150** | §3 (F1) |
| T5 | ~330 → 650–800 | **~270 → ~550 ±20%** | live KEY-3 at 34 with fewer E2 roles; §3 |
| T10 | 220 (170–300) | **170 (120–230)** | housing-bound villages, fertility-limited growth (F2, F3) |
| T12 | 88 → 190 | 190 kept **with R4**; as written 160 | §4 |
| T16 | 20–40% by Decree | **25–50%** with R2 towns | fewer natural foundings; §7 |
| T19 | smelting settlements 30–50% | **Towns of 80–100: 25–70% without WOD-04, < 5% with it; villages none** | §5 |
| T21 | three cells | palisade+8 warriors raiders hurt **60** (net −29); hillfort raiders hurt **83**; stone-12 success **63**; 20 vs 28 stone **18%** | §6 |
| T22 | as written | **with R5 rationing** | §6 |
| T25 | 55–70; 0.1–0.5 | **60–80; 0.03–0.08 (captured towns, R7)** | §9 |
| B12 | falls back within 12 seasons | ≥ 10% within 12 seasons, half within ~9 years | §5 |
| B17 | Town and City in ≥ 75% | **Town ≥ 75%; City (≥ 90) ≥ 25%** | F4 |

## 12. Gaps and what the oracle cannot settle

- **T23, T26–T28** (conflict counts, Tin Folk compacts, Mandate at the cap, figures and tick) are harness measurements. Raids are 0 in Council play today; T23's rates enter §9 only as assumptions.
- **E3 Council overhead (1.20)** and E3-window Insight are assumed equal to E2's; they move E3 by ±13–17% each. Re-fit EraBase with `fit.py live` after WP5's first live E3 run.
- **Compacts** are modeled as "accepted whenever a Tin Folk village is within 30 tiles"; T20's 100% is therefore an upper bound. Island placers and sea routes (SEA-04, 23 §22 item 5) are not on the oracle map.
- **Settlement-level heterogeneity:** the live-mode model under-counts settlements by 15–25% late; T11 carries that bias.
- **Archetypes:** live mode and territory runs are Balanced only; A5 at the chapter end needs the harness.

## 13. Reproducing

```bash
cd docs/MVP/balance/tools
uv run --no-project --with numpy python live_data.py                 # §2 (live CSV summary)
uv run --no-project --with numpy python balance_sim.py live          # §3 (validation, T-table, sensitivity)
uv run --no-project --with numpy python balance_sim.py live --check
uv run --no-project --with numpy python fit.py live                  # EraBase[3]
uv run --no-project --with numpy python check_e3.py [--fixed --check]
uv run --no-project --with numpy python metal_sim.py [--check]
uv run --no-project --with numpy python combat_e3.py [--selftest]
uv run --no-project --with numpy python territory_sim.py e3 [--check]   # ~45 s
uv run --no-project --with numpy python legacy_scale.py [--check]
uv run --no-project --with numpy python cohesion_sim.py [--check]       # ~90 s
```
Live data: copy the repo to a scratch directory (rsync without bin/obj/.git, restore any uncommitted `src/` files to HEAD), add `tools/probe/ProbeCommand.cs.txt` as `src/CivSim.Harness/ProbeCommand.cs` with `"probe" => ProbeCommand.Run(args[1..])` in `Program.cs`, `dotnet build`, run `probe --pack PACK --seed S --minutes 64` per seed, then `live_data.py ingest DIR`.

## 14. WP6 live results (metal and fuel economy)

Built in `src/CivSim.Sim/Economy/LivingEconomyModel.{Bronze,Metal,Fixtures}.cs`, `EconomyContent.Bronze.cs`, `BronzeFixture.cs`.
Measured with `research --metal-fixture` (one settlement held at a size, a surface copper deposit, a tin placer and deep
arsenical ore in range, 13 forest tiles × 30 L, forge, furnaces and mine heads standing, every metal project done at
season 0: the live twin of `metal_sim.py`) and `research --report-metal [--decree-mines]` (four Balanced Council
civilizations, seeds 20261019 and 777, 64 minutes). No constant this document or 23 marks tunable was changed:
`MetalPushLaborCap` 0.10, `UpgradeRemeltShare` 0.5 and the ×1.4 dormancy tail stand.

| Target | Fixture (100 people; 80 where it differs) | Live, planner only | Live, `--decree-mines` | Verdict |
|---|---|---|---|---|
| T17 bronze / tin / charcoal / wood per 100 a year | 0.83 L / 0.083 L / 2.28 L / 13.7 L (bellows); 80 people 0.65 / 0.065 / 1.80 / 10.8 | median 0.00 L (0–0.16) | median 0.43 L, tin 0.01, charcoal 1.6, wood 10.6 | fixture ✅ (wood at the low edge of 14–19); live ✗ for want of ore access |
| T17 metal labor (mining, charcoal, smelting) | 1.6–2.2% | 0.2–6% | 4–8% | fixture below 3–4%; live above (small, scattered smithies) |
| T18 rung 6 twelve seasons after MET-05 (access holds) | 69% at 12, 70% at 13 (60–100 people) | 5 of 19 settlements | per-settlement median 8%, 19 of 79 at ≥ 70%; civ-wide 17–73% at minute 64 | fixture ≈ ✅ (one season late); live ✗ |
| T19 standing wood drawn down by season 52 | 16% without WOD-04, 2% with (80 people 8% / 1%) | villages 0% (p90 3–7%) | same | with WOD-04 ✅; without it below 25–70% |
| B11 chop / mining | ×1.50 / ×1.95 | ×1.50 / ×1.95 | | ✅ |
| B12 | tin cut: "no tin" the next season; casting falls to T9 (arsenical in range) or T8; 8% of tin-bronze kits replaced after 12 seasons, about half after 12 years; no charcoal, no smelt | | | reason ✅, squeeze ✅ (slower than "half in ~9 years") |
| B13 | as T19 | | | partial (see T19) |
| E3 timing (`--check-m10`) | — | KEY-3 30.3–35.6, KEY-4 42.2–47.3, E3 median 12.2 (11.4–13.5), chapter end 45.2 / 49.5 | | ✅ unchanged within 0.3 min of the WP5 baseline |

**Why the live metal economy is thin.** Without Decrees (WP8) and the AI that issues them (WP9) the planner opens camps
only on deposits on its own land within 8 walking tiles (23 §7.2). On both seeds the nearest copper ore is 10–25 tiles
from most settlements, so few civilizations dig at all; `--decree-mines` (a harness stand-in: each civilization opens a
camp on its nearest copper, arsenical and tin deposit within mining reach every season) shows the chain working at about
half the fixture's rate, limited by ore hauled to 20–60-person villages that each run their own forge. T17–T19 should be
re-measured once WP8/WP9 issue *Open a mine*.

**Why the fixture's demand and labor sit low.** Kits wear only by tool work (chop, farm, build, gather, craft, the metal
jobs), which is about 0.4 ws a season per adult in the live economy against the oracle's 0.66: foraging is most of the
live food labor and is not tool work (22 §5.6). Kits therefore last ~1.6× longer, steady demand is lower per adult
and the dormancy squeeze slower; per 100 people the total lands in band only because the live age mix grows more adults.

**Why T19 misses without coppicing.** The E2 economy burns far less wood than the oracle assumes (one hearth per
settlement, no kiln or building wood per person): about 25 L per 100 people a year besides metal, against ~54. Forests
regrow at 0.5 L a tile a season, so a smelting town of 100 draws its 13 stands down by 16%, not 25–70%. Coppicing keeps
it under 5% as required. Raising the drawdown needs non-metal wood demand (an E2/WP7 question), not a metal constant.

**Decisions taken in WP6 (beyond 22/23), each a mechanism, not a tuned number:**
1. The metal chain plans kits and arms first, then the other E3 goods with what is left of the 10% cap; forge, furnace
   and mine-head building is construction, budgeted apart (up to another 10%).
2. A settlement with a forge casts for forge-less sisters within the sharing reach (15 tiles, 20 with GOV-04); ores,
   metals and kits move along last season's wants. A settlement that digs an ore also digs its sisters' deficit.
3. Camp crews scale 2 to 4 (6 with MIN-04) with the parent's ore need; crew time the face does not need works at home.
4. Clay for metal structures can always be dug: 3 L/ws at a deposit, 1 at a riverbank, lake or wetland, 0.5 elsewhere
   (most E3 settlements have no clay in range; without it none could build a forge).
5. From KEY-3 woodcutting stops at a stock of 24 L plus the season's charcoal wood and fells across the range's forest
   stands in proportion to what each holds; charcoal clamps cut the same way (then beyond the range, nearest first,
   hauled). E0–E2 cutting (tile by tile, unbounded) is unchanged, so the E0–E2 goldens hold.
6. E3 forest rules (clearing, grassland, coppicing's ×2) apply from the world's first KEY-3, like Tin Folk (22 §6.4).
7. Chop rungs 5 and 6 are never settlement tools (the E2 rule let every chopper use rung 6 once MET-04 was known);
   the colliers fell their wood with the best axe in the settlement.
8. Lime, pigment, yarn and cloth (doc 10 §5.3, never stocked by the E2 economy) are recipes run from KEY-3; ochre is
   gathered at 0.5 L/ws (MIN-01), blocks quarried at 2 L/ws × the kit's quarry mod (MIN-03); milk (a quarter of the
   herd's meat yield, HUS-05) and wool (0.5 L a sheep a year, HUS-06) are coarse until WP7.
9. MET-04's arsenic: smiths who co-smelted arsenical bronze that season take injury ×1.3.

**Snapshot fidelity.** Restoring a match re-derived research's season caches (settlements, adults, elders, people,
hearths) from the economy as it stood at the restore; the live match kept the values read at the season's start, and
fissions resolved since then (or figures politics moved) made the two diverge within a tick. The caches now travel in
the snapshot (`LivingEconomySource.WriteSnapshot`); `FogFairnessHarness.RunPair` compares the live match with its
restored copy again, and `MetalEconomyTests` checks live against restored at three ticks, the last in the Bronze Age.

Reproduce: `dotnet run --project src/CivSim.Harness -c Release -- research --pack civsim.pack --metal-fixture`, and
`... research --pack civsim.pack --report-metal [--decree-mines] [--seeds 20261019,777]`.

## 15. WP7 live results (food, settlements and the early state)

Built in `src/CivSim.Sim/Economy/LivingEconomyModel.{Civic,Food,Health}.cs` (with hooks in `LivingEconomyModel.cs`, the research
source, `ResearchSystem.RefreshInsight`, `PoliticsSystem` and `MatchModel`). Measured with `research --report-e3` (four Balanced
Council civilizations, seeds 20261019 and 777, 64 minutes), `research --c1-e3` (the live twin of `check_e3.py --fixed`),
`research --check-m10` and `research --metal-fixture` / `--report-metal`. Everything is live only for settlements of a
civilization past KEY-3 and hashed only once the Bronze Age has begun: `research-v1`, `politics-v1` and `toy-v1` are unchanged.

| Target | Measured | Verdict |
|---|---|---|
| T1 / T3 / T2 (`--check-m10`, 2 seeds, EraBase 150) | KEY-3 median 33.9; KEY-4 median 45.1; E3 duration median **11.3** (WP6: 12.2); 6 more seeds (1–6): E3 12.3 | ✗ by 0.2 min on the gate seeds (re-fit below) |
| T10 people per civ at KEY-4 | median 161 (123–223) | ✅ 170 (120–230) |
| T11 settlements per civ at KEY-4 | median 5 (3–6) | at the low edge: Market towns stop budding (R2) and Found decrees (WP8/WP9) are not issued yet |
| B17 Town / City | Town in 38% of civs at KEY-4, 50% at the chapter end (EraBase 160: 38% / **75%**); City 0% | Town ✗ at 150, ✅ with the re-fit; City ✗ (below) |
| T12 / C1-E3 (`--c1-e3`, need 3.0) | seed 20261019: 128 → 193 (×1.50); seed 777: 105 → 174 (×1.66); E3 contributions AGR-05 ×1.05, AGR-06 ×1.08, AGR-07 ×1.04, HUS-05 ×1.03, TRN-05 ×1.06, GOV-04 ×1.04, TRD-05 ×1.05, NUM-06 ×1.03 | E3-end value **in 190 ±10%**; the ratio is short because the live E2 start is above 88 (live FoodFactor 1.35, §3) and live fields carry 20% of E2 food, not 45%, so the ard and canals buy less |
| T12 live (mean FoodCap per settlement) | 151 at KEY-3 → 169 at KEY-4 (×1.07–1.16) | young daughters have no fields or herds; the fixture is the check |
| T13 C3-E3 (expected hazards, seasons with all four civs in the era) | child 37.6‰ (anchor 36.1, +4%), adult 11.7‰ (11.7); E3/E0 child 0.835 (0.803), adult 0.900 (0.779: the live E0 adult hazard is 13.0‰, below its 15‰ anchor); e45 15.7 (EraBase 160: 16.3) | ✅ against the 09 anchors; the adult E3/E0 ratio misses only through the live E0 base |
| B10 epidemic share of E3 deaths | 12–13% | ✅ 7–13% (top of the band) |
| T14 claimed land at KEY-4 | 23% (chapter end 25%) | ✗ 30–42%: needs the Found decrees (§7: "Towns (R2) + 2 Founds"); Cohesion also moves F from its E2 ceiling 1.2 to ~1.02 (C 70), which lowers claim emission by ~15% |
| T15 paired borders touching | 0/4 | ✗ (as T14) |
| T16 E3 foundings per civ | median 2 | natural fissions only (Decrees are WP8) |
| T25 Cohesion | mean 70, p10 65, p90 76; 0 secessions (0 captures) | ✅ 60–80; secession needs captures (R7), which the Council AI never makes |
| T7 E3 roles' share of Insight at KEY-4 | median 11% (EraBase 160: 14%) | just under 12–20% at 150 (KEY-4 comes before most Temples and Schools) |
| T19 fixture (towns of 80 / 100, `--metal-fixture`) | without WOD-04 53% / 57% drawn down; with it 4% / 3% | ✅ 25–70% and < 5% |
| T19 live (`--report-metal`) | every civilization had WOD-04 by the end; towns of 80–100 lose 18% (p10 0, p90 90), 101+ lose 53% | ✗ < 5%: R2 towns fell the timber of their housing ahead (longhouses) and civic structures, which the oracle's steady 0.15 L a person a year does not have |
| Wood per 100 people a year (fixture, besides metal) | 38–39 L chopped (hearths, kilns, crafts, building) plus up to 26 L of deadwood | ≈ the oracle's 54 (was ~25, §14) |

**Mechanisms (docs/MVP/22 §8–§10, 23 §1.2, §8–§12).** Tiers: Town with TRD-04, a Market and 60 people, City with SOC-09, a Temple
or the Palace and 90 (07d F4), demotion after 4 seasons below 0.85 × the threshold or without the structure; claim radius 5 / 6,
the Palace capital ×1.25, TER-07's hinterland ring 7 at half (politics reads them from the economy). Capital: the settlement
with the Palace, else the oldest. Market towns (07d R2): the largest settlements get the Market (one more per 4 settlements); from
the day it is chosen the settlement buds only at 85% of min(FoodCap, GroupCap) and builds housing ahead to 0.85 × min(FoodCap,
GroupCap) + 8. Roles by structure: a Priest per Temple, a Scribe at the capital from COM-05 and two per School; they work their
role all season, and research counts them (with COM-06 ×1.5, NUM-06 +4, SOC-08 +10% on E3 roles) plus OutCity × RoleScale a City;
a settlement with a Scribe adopts without the distance lag. Cohesion: from each civilization's KEY-3 the stored F is
target-seeking (C = (F − 0.6) ÷ 0.006, a quarter step a season) on 22 §8.3's target with every E3 `cohesion` effect in its scope,
Overreach by walking tiles to the capital × (1 − AdminReduction), the Admin span penalty, and R7 for captured towns (`NoteCaptured`
from politics; start C 25, −35 recovering +1); work ×0.85–1.15, the fission trigger 75% below C 35, secession below C 15 for 8
seasons (never the capital; `DrainSecessions` → politics marks it Free Folk). Food: the ard's 40% field limit and halved labor
per ox pair, canals on fields within 2 tiles of a river (×1.5, ×2 arid), flood loss ×0.5 each for AGR-06 and NUM-06, AGR-07 only
on manured fields, dairying (milk, cheese), pastures to 15% of the range, horses (HUS-07), coastal salt pans, bakeries, GOV-04's
redistribution of stores and halved famine deaths. Health: from KEY-3 the E2 epidemic budget is replaced by discrete outbreaks
(09 §10: density pressure × (1 − Sanitation) × (1 + inflow), SIR final size, two seasons, immunity), Plague from KEY-3, soap
sanitation (CHM-05), `ImportOutbreak`/`OutbreakAt` for WP8's routes; E3 h45 0.018 (09 §4.1).

**Decisions taken in WP7 (beyond 22/23):**
1. Civic construction (canals, the Market and other structures, a Market town's dwellings) takes at most `CivicLaborShare` 0.10 of
   the work (0.20 in a Market town), never from the food work, adults first; the builders fetch wood (range, then 8 tiles,
   hauled), stone and clay themselves and mould the bricks a structure needs. The order is the Market, then housing, canals and
   the next structure. Without this the E2 economy, whose villages strip their range and gather little clay, never raises one.
2. A Market town builds longhouses where the timber is there, else mudbrick houses (a cleared range), else wattle or pit houses.
3. From KEY-3 clay is dug where there is no deposit (WP6's 0.5–1 L/ws), and the woodcutters go first while the season's wood
   falls short (at most half the material work).
4. TRN-05, GOV-04 and TRD-05 have no stock mechanism in 10/23; as `check_e3.py` passes their 09 steps through, the live sim
   applies them as food-value multipliers (carts ×1.06 with a cart per 25 people, GOV-04 ×1.04, TRD-05 ×1.05 and ×1.25 net in a
   Town on 2 routes once WP8 supplies routes through `ITradeRouteSource`).
5. Outbreak class weights in E3: fever 55, flux 35, plague 10 (pox only by import); case fatality ×1.5 children, ×2 elders,
   normalised so a settlement's mean is the class CFR.
6. Non-metal wood (T19): extra hearths (one per 35 people), kiln firing 0.2 L and repairs 0.05 L a person a year, a bakery's 2 L a
   season, deadwood (2 L a forest tile a year) first. Repairs are a third of `metal_sim.py`'s 0.15 build wood because the live
   planner already fells the timber of every dwelling it builds.

**Constants changed (each one the docs mark tunable or a rule's implementation):**
- `ConqueredCohesion` −20 → **−35**, `ConqueredRecovery` 2 → **1**, new `ConqueredStartCohesion` **25** (R7, §10).
- New: `CivicLaborShare` 0.10, `MarketEvery` 4, `EpidemicInflowE3` 0.2, `HearthPeople` 35, `KilnWoodPerPerson` 0.2,
  `BuildWoodPerPerson` 0.05, `DeadwoodPerTile` 2.
- Not changed: `MetalPushLaborCap`, `UpgradeRemeltShare`, the dormancy tail, `CohesionBase` 60, any 22 §8.3 term.

**EraBase re-fit (decision needed, not applied).** T4's rule gives E3 `base_cost` 150 × 13 ÷ 12.1 (the 8-seed E3 median with WP7)
= **160**: the E3 roles and Cohesion's work pace shortened the gate seeds' median E3 from 12.2 to 11.3 minutes. Measured at 160:
E3 median 12.0 (8 seeds 12.7), KEY-4 45.9, Town in 75% of civilizations at the chapter end, T7 14%, e45 16.3. It is not applied
because it flips `StrategistScenarioTests.It_declares_war_only_with_the_force_ratio` (the Strategist's grand plan no longer picks
Conquer in that E1 scenario: the AI's valuation reads E3 costs), which belongs to the AI work package.

**Infeasible or blocked here:** T14/T15 (and T11's centre) need the Found decrees (WP8/WP9), as §7 already showed; secessions need
captures (WP8/WP9 war); a City by the chapter end needs a Temple (BEL-06, rarely picked) or the Palace (GOV-05, 30 ws, 200 bricks
and 20 L of blocks, researched near minute 42) on a town of 90, which the Council's late GOV-05 does not leave time for; live T19
with coppicing is above 5% because towns now build. Reproduce: `research --pack civsim.pack --report-e3`, `--c1-e3 --seed S`,
`--check-m10`, `--metal-fixture`, `--report-metal`.
