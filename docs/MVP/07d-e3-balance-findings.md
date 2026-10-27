# 07d — E3 Bronze Balance Findings (WP2)

> Work package WP2 of [22 §20](22-bronze-age.md#20-work-breakdown): the Python oracle extended through E3 (22 §15.2 items 1–7), the open parameters fitted, and every §15.1 target T1–T28 that an oracle can reach reproduced or flagged. Reconciled with [23](23-e3-bronze-specs.md) (WP1, the E3 registries and project specs).
> In the style of [07](07-balance-findings.md), [07b](07b-territory-findings.md) and [07c](07c-e0-e2-checks.md). Historical WP2 scope: at that checkpoint, this was design/tooling work and the live measurements used a scratch copy of commit 4cb188d while the main checkout's `src/` was being edited (§9). Later implementation and measurement updates are recorded in §§16–20.
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
| **F7** | **Secession never happens** under §8.3 as written (0.000 per civ per E3 in every stress case): base 60 + hearth 5 + feast 8 (both on every closure) puts typical C at 63–80, and the quarter-step plus 8-season timer filters every single crisis | Corrected R7 oracle: capture does not add a raid penalty or cohesion update until the following season, and integer cohesion moves at least one point toward target. It gives 0.002 secessions per civ at 0.10 captures per civ and 0.027 with one capture per civ. The 0.03–0.08 target still needs reconciliation with measured B20 capture incidence; T25 remains open |
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

**Dormancy (B12):** after a full E3 on tin bronze, tin cut at KEY-4: 100% still on rung 6 after 12 seasons with no growth, 84% in a growing village (new adults get copper), and about half of initial rung-6 adult coverage remains after 12–13 years in the live fixture. A fresh re-kit synchronises kit ages, so 23 C6's "first fallbacks within a season" holds only for old, mixed stocks. **B12 → "within 12 seasons the new and heaviest tool users fall back (≥ 10%) and the 'no tin' reason shows; about half of rung-6 adult coverage is gone after 12–13 years."** The embargo is a slow lever by design; in a 3-year countdown it bites through growth, arms and new settlements, not existing kits.

**T20 ✅** (territory_sim e3, §7): own tin mine by KEY-4 53–66% of civs, a Tin Folk village within compact reach 78%, one or the other 100%; arsenical fallback needed by 0% under that (optimistic) compact rule. **E3 length vs metal supply: < 2%** — Insight is headcount-driven and Know-how is 35% of Commons whatever is practised; metal moves labor, arms and fuel, not research pace.

## 6. Combat (`combat_e3.py`; T21, T22)

A **bit-exact port** of `Combat.Fight`, `Raids.Resolve` and `CombatTables` (Fix64 Q32.32, Pcg32, Hash64): with the E3 extensions off it reproduces `politics --report-combat` to the last digit (83.3 / 36.6 / 12.2 / 4.5% success; capture 0 / 100 / 10.3 / 100%). E3 rules: weapons 1.8 / 2.1 / 2.2 / 2.3, training ×1.3, shields ×0.7 casualties, chariot crew ×2 strength and ×0.5 casualties, barrier multipliers replace each other (23 §1.2), loot ×0.25 behind hillfort or walls. 4,000 trials a row, seed 20261019.

| §7.5 cell | doc 22 | port | |
|---|---:|---:|---|
| 12 bronze vs village, none, stone militia | 93 / 9 / 24 / +33 | 94.5 / 6.9 / 23.9 / +34 | ✅ |
| palisade + watchers, stone militia | 13 / 39 / 8 / −16 | 16.5 / 37.4 / 8.7 / −14 | ✅ |
| palisade + watchers, bronze militia + shields | 2 / 45 / 3 / −23 | 2.8 / 44.5 / 3.4 / −22 | ✅ |
| palisade + watchers + 8 bronze warriors | 1 / **60** / 3 / **−29** | 1.3 / **60.2** / 2.6 / **−28.5** | ✅ corrected in 22 §7.5 |
| hillfort + watchers + 8 warriors | 0 / **83** / 2 / −38 | 0.3 / **83.3** / 2.0 / −37.9 | ✅ corrected in 22 §7.5 |
| walls + watchers + 8 warriors | 0 / 93 / 2 / −42 | 0.1 / 92.6 / 1.6 / −41.5 | ✅ corrected in 22 §7.5 |
| the 12 with stone spears, vs none | **63** / 18 / 17 / +15 | **63.2** / 18.0 / 16.6 / +14.8 | ✅ corrected in 22 §7.5 |
| 8 bronze vs camp of 6 · + 2 guards + watch | 97 · 99 | 96.5 · 99.5 | ✅ |
| vs hamlet 15 stone · bronze + thorn + watch | 100 · 89 | 100 · 89.2 | ✅ |
| 20 bronze+shield vs 20 / **28** stone warriors | 100 / **18** | 100 / **18** (27 ≈ 50%) | ✅; worth 1.40× |
| 12 chariot crew vs 24 / 18 bronze infantry | **37 / 100** | **37 / 100** with crew shields stacking (×0.35) | ✅; shields and chariot casualties multiply |

`combat_e3.py` now checks all four table values (success, both sides' casualties and net value), the separate stone-warband row, and the published field-battle rows against these corrected §7.5 values. The oracle's `--selftest` asserts each row remains within 10; the default run exits nonzero if it does not.

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

Layouts from §7 (distance to the capital median 19, p90 37 walking tiles), research positions from §3, ASSUMED event rates: harsh winter 30% × shortfall 15% (villages sit at ~⅓ of FoodCap), epidemics 0.049 per settlement-year by 09 §10 class, raids 0.03 per settlement-year and captures 0.10 per civ per E3. Independent 0.10 capture chances imply a 34% chance that at least one of four civs captures a town in a match, within T23's 20–50% target.

| Case | Mean C (p10–p90) | Secessions per civ per E3 |
|---|---|---:|
| 22 §8.3 as written | 70.5 (62–80) | **0.000** |
| no administration projects | 64.0 (51–76) | 0.000 |
| every civ captures a distant settlement | 69.5 (61–80) | 0.000 |
| famine ×3, raids ×3, epidemics ×2 | 69.6 (61–79) | 0.000 |
| base 50 instead of 60 | 62.1 (55–71) | 0.000 |
| **R7 oracle baseline** (captured at C 25, −35 recovering +1 a season, no hearth/feast/shrine/GOV-03 terms while conquered; capture does not imply a raid) | 71.0 (62–81) | **0.002** (captures 0.10 per civ) |
| R7, every civ captures one distant settlement | 67.9 (58–81) | 0.027 (2.7% of captured distant towns secede) |
| R7, captures 0.15 per civ, famine ×3, raids ×3 | 69.6 (61–81) | 0.013 |

Typical C is 63–80 (22 says 55–70): hearth and feast are on every closure, so a typical settlement already has +13. The corrected R7 model removes the automatic eight-season raid penalty from captured settlements, applies capture after the current season's Cohesion pass, uses the live minimum-one-point integer step, and counts secessions only within the 52-season E3 exposure. The resulting rate is 0.002 at 0.10 captures per civ and 0.027 when every civ captures one distant town; an independent raid can still apply its own penalty on a later cohesion update. At B20's observed 25% match capture rate, the 0.03–0.08 B19 target would require substantially higher conditional secession risk than this model predicts. Mean Cohesion remains within 60–80 and cascades remain below 2%. At this oracle-only checkpoint B19 stayed open; the later live follow-up below tests a bounded capture-term change. Sensitivity (±20% on famine weight, Overreach divisor, span penalty, raid rate, base): mean C remains in range and the noncapture baseline has zero secessions.

**Live acceptance follow-up (2026-10-26):** changing only `ConqueredCohesion` from −35 to −45 while retaining C 25 start, +1/season recovery, the C 15 threshold and eight-season timer passed B19 on the exact 24-seed cohort with an 80-minute KEY-4 completion ceiling. All 96/96 E3 exposures completed; mean Cohesion was 63.9, secessions were 0.05 per civilization per exposure, and cascades were 0/24. The chapter-end sample remained at the normal MatchEnd tick. On the same cohort B17 remained open: 11.5 settlements/civ and 80.2% own-KEY-4 border contact passed, but claimed land was 43.1% (target ≤42%), and City prevalence was 22.9% (target ≥25%); Town prevalence was 90.6%. The earlier +3/+8 City-path Spark preference screen did not change the City rate and was reverted. No threshold changes are justified by these data.

**B17–B20 acceptance result (2026-10-26):** on the exact 24-seed cohort 20261019–20261042 with an 80-minute cap, the gated City Temple labor path plus Bronze-era Expand fission scale 0.83 (0.80 before E3; other postures unchanged) passed B17–B20 in 65.59 s. B17: 10.9 settlements/civ, 41.4% claimed land, own-KEY-4 paired border 72/96 (75.0%), KEY-4 96/96, Town 90.6%, City 26.0%. B18 Legacy split: 54.0/31.8/14.2/0.0%. B19: cohesion 64.6, 0.07 secessions/civ/E3, 0/24 cascades. B20: 7.1 raids/match, war 91.7%, capture 33.3%. This supersedes the preceding open B17/B19 result for this cohort; no acceptance thresholds changed.

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
| Captured settlement Cohesion (R7 live) | start **C 25**; conquered term **−45, +1 a season**; no hearth/feast/shrine/GOV-03 terms while it lasts | −20, +2 | §9 |
| `CohesionBase` | 60 | 60 | §9; the older 22 §8.3 "55–70" description is superseded by the R7 live target |
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
| T21 | §7.5 values corrected | palisade+8 warriors **1 / 60 / 3 / −29**; hillfort **0 / 83 / 2 / −38**; stone-12 **63 / 18 / 17 / +15**; 20 vs 28 stone **18%**; chariot vs 24 **37%** with stacked shield reduction | §6 |
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
| T19 standing wood drawn down by season 52 | fixed population: 25% / 62% without WOD-04 at 80 / 100 people; 2% / 3% with it | villages 0% (p90 3–7%) | same | ✅ within 25–70% and <5% |
| B11 chop / mining | ×1.50 / ×1.95 | ×1.50 / ×1.95 | | ✅ |
| B12 | tin cut: "no tin" the next season; casting falls to T9 (arsenical in range) or T8; about 10% fall back by 12 seasons and half of initial rung-6 adult coverage is gone after 12–13 years; no charcoal, no smelt | | | reason ✅, squeeze ✅ (slower than the earlier ~9-year estimate) |
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
| B10 expected epidemic share of E3 baseline mortality | historical realized ledger share 12–13% | diagnostic only; acceptance uses the expected-hazard measure defined in 22 §16 |
| T14 claimed land at KEY-4 | 23% (chapter end 25%) | ✗ 30–42%: needs the Found decrees (§7: "Towns (R2) + 2 Founds"); Cohesion also moves F from its E2 ceiling 1.2 to ~1.02 (C 70), which lowers claim emission by ~15% |
| T15 paired borders touching | 0/4 | ✗ (as T14) |
| T16 E3 foundings per civ | median 2 | natural fissions only (Decrees are WP8) |
| T25 Cohesion | mean 70, p10 65, p90 76; 0 secessions (0 captures) | ✅ 60–80; secession needs captures (R7), which the Council AI never makes |
| T7 E3 roles' share of Insight at KEY-4 | median 11% (EraBase 160: 14%) | just under 12–20% at 150 (KEY-4 comes before most Temples and Schools) |
| T19 fixture (fixed populations 80 / 100, `--metal-fixture`) | without WOD-04 25% / 62% drawn down; with it 2% / 3% | ✅ 25–70% and < 5% |
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
from politics; start C 25, −45 recovering +1); work ×0.85–1.15, the fission trigger 75% below C 35, secession below C 15 for 8
seasons (never the capital; `DrainSecessions` → politics marks it Free Folk). Food: the ard's 40% field limit and halved labor
per ox pair, canals on fields within 2 tiles of a river (×1.5, ×2 arid), flood loss ×0.5 each for AGR-06 and NUM-06, AGR-07 only
on manured fields, dairying (milk, cheese), pastures to 15% of the range, horses (HUS-07), coastal salt pans, bakeries, GOV-04's
redistribution of stores and halved famine deaths. Health: from KEY-3 the E2 epidemic budget is replaced by discrete outbreaks
(09 §10: density pressure × (1 − Sanitation) × (1 + inflow), SIR final size, two seasons, immunity), Plague from KEY-3, soap
sanitation (CHM-05), `ImportOutbreak`/`OutbreakAt` for WP8's routes; E3 h45 0.018 (09 §4.1).

**Decisions taken in WP7 (beyond 22/23):**
1. Civic construction (canals, the Market and other structures, a Market town's dwellings) takes at most `CivicLaborShare` 0.10 of
   the work (0.20 in a Market town), never from the food work, adults first; the builders fetch wood (range, then 8 tiles,
   hauled), stone and clay themselves and mould the bricks a structure needs. The Market remains first. An active Temple or Palace
   uses the remaining civic budget before housing and canals, with its bricks reserved from dwellings; the largest Town with SOC-09
   and buildable materials can replace a lower-priority queued structure with the City-enabling Temple or Palace before it reaches
   90 people. Other structures retain the housing, canals, then structure order. The total civic labor cap is unchanged. Without this the E2 economy,
   whose villages strip their range and gather little clay, never raises one.
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
- `ConqueredCohesion` −20 → −35 in the initial R7 rule, then **−45** in the live B19 follow-up; `ConqueredRecovery` 2 → **1**, new `ConqueredStartCohesion` **25** (R7, §10).
- New: `CivicLaborShare` 0.10, `MarketEvery` 4, `EpidemicInflowE3` 0.30 (B10 sensitivity; see below), `HearthPeople` 35, `KilnWoodPerPerson` 0.2,
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

## 16. WP8a live results (Mandate and Decrees)

`research --pack civsim.pack --report-decrees` on the WP8a tree, default two seeds (20261019, 777), four Balanced civilizations
on the Council, through KEY-4:

| Measure | Live result | Target / note |
|---|---:|---|
| KEY-4 median | 45.8 min (43.0–47.3) | 43 (07d range 43–47) |
| E3 duration median | 12.0 min (10.9–12.8) | 11.5–14.5 ✅ |
| T11 settlements at KEY-4 | median 5 | target 7 (5–10) |
| T14 claimed land at KEY-4 | median 24% | target 30–42% |
| T15 paired borders touching | 1/4 | target ≥75% |
| T16 Decree share of E3 foundings | 0% over 7 civs | target 25–50%; the Council did not produce any counted Decree founding in this run |
| T20 tin by KEY-4 / bronze (tin or arsenic) | 0/8 / 0/8 | targets ≥50% / ≥90% |
| T26 Tin Folk villages per map | 6, 6 | target 3–6; Tin Folk compact on 1/2 maps |
| T27 Mandate earned / Decrees / seasons at cap (median) | 8.5 / 5.5 / 1% | targets 7–9 / 5–8 / <25% |

The prior WP8a checkpoint numbers are superseded for these measures. The report confirms the E3 timing and Mandate cadence;
T11, T14, T15, T16 and T20 remain outside their targets in this small sample, so these are live diagnostics rather than a
multi-seed acceptance result. `--report-e3` gave KEY-4 median 45.9, E3 median 11.9, people median 167, settlements median 5,
T14 median 24%, T15 1/4 and T16 natural fissions median 1.5 per civ. B17 Towns: 50% by chapter end (target ≥75%); no City.
The two-seed `--check-m10` passed: KEY-4 median 45.8, E3 median 12.0, chapter end median 47.2, and replay checksums matched.

The latest two-seed `--report-metal` supersedes the earlier figures: T17 medians are bronze **0.31 L**, tin **0.004 L**,
charcoal **1.14 L**, wood **7.3 L** per 100 people per year and labor **2.0%** (all below target); T18 rung-6 share is **0%**
civ-wide and **9%** over 45 settlements with a casting opportunity (4 at ≥70%); T19's current WOD-04 settlements draw down
wood by a median **27%** at 80–100 people (7 settlements) and **51%** at 101–1,000 (4 settlements), so the under-5% target
is missed in the current sample. All eight civilizations had WOD-04 by the end, so this run does not estimate the no-WOD-04
case. The two-seed `--report-e3` also misses T12's live mean FoodCap ratio (1.11 vs target ×2.16), and its expected-hazard
T13 estimates are outside the child/adult ratio and e45 targets; the realized epidemic ledger share is 9% (historical diagnostic; B10 acceptance uses expected share).
These are current-source diagnostics, not acceptance samples; 07d §10's constants were not changed to chase them.

## 17. WP8b combat and siege checkpoint

`python3 docs/MVP/balance/tools/combat_e3.py` and `--selftest` pass against the corrected §7.5 values. The oracle compares all
four table values, the stone-warband row and the field-battle rows; shield and chariot crew casualty factors now stack at ×0.35.
T22's 0.75 ration model yields 65.1%, 98.0%, and 98.0% capture in 3–6 seasons for 2.0, 2.5, and 3.0 stores. A bounded C#
test now samples 1,000 fixed seeds per store level through `Sieges.Advance` and checks these three rates with a broad tolerance;
this measures the helper's stochastic collapse window. The focused WP8b lifecycle/fortification tests also cover snapshot
resume, deterministic next-season siege results, rationing, and relief transition. The sweep does not exercise live-match
supply, sally, desertion, or casualty interactions. Relief accepts an already-arrived host because the
order model has no traveling allied-relief order; range-foraging has no numeric rule and is not modeled; structures have no
inside/outside positions, so the siege burns the first available flammable structure. This WP8 checkpoint predates the current
24-seed Council run, which passes B20 at 7.5 raids per match, 95.8% of matches with war, and 25.0% with a capture.

**Bounded live-economy siege integration:** `WP8bSiegeLifecycleTests.Supplied_live_economy_investment_rations_a_walled_target_without_direct_assault_capture`
uses one fixed seed and a controlled generated-world start pair with an actual passable walking route ≤ 8 tiles from the attacker
to the walled target. It advances the real `MatchModel`/`SimRunner` through assault arrival, sets the target to 2.5 seasons of food
before the first siege-season update, then follows six live seasonal updates. The supplied besieger retains its carried-food season
and has no desertion event; the wall attack enters investment without a direct-assault capture. In this fixture, local food
production keeps the target's stores above capitulation through season six, so it validates live supply and ration wiring but does
not establish live-match capture timing. The one-seed path is not a T22 rate estimate, and the deliberately oversized supplied
host does not measure supply interruption, sally, desertion, or casualty interactions. The 1,000-seed `Sieges.Advance` sweep
remains the T22 rate evidence.

## 18. WP9 AI telemetry checkpoint

The Harness now measures B27 tin-poor recovery and dormant-route blunders, B28 defense before a believed-bronze raid, B29
KEY-4 forecast fidelity, and all four B30 signals: positive-EV Mandate overflow, suicide raids, unsupplied-siege desertion,
and naked Tin caravans. Focused detector and regression tests cover these signals. The recorded two-seed, 64-minute
diagnostic predates the B29 and expanded B30 telemetry, so its counters cannot establish those gates. That historical
sample had A1 at 100%; A2 at 75% Grower, 100% Builder and 75% Wanderer; A3 survival at 100% but wins at 50%; A4 at 50%;
A5 Builder at 37.5% and Grower at 50% (both above the 30% cap), Wanderer at 16.7%, Warlord at 12.5%, Sage at 8.3%; and
A7 with zero keystone stalls but dithering in 2 matches. B31 passed at 75%. Its B27 fallback was 8/8 tin-poor cases, while
dormant-route blunders occurred in 6/8 AI-matches; B28 defense was 2/2. The old B30 proxy reported zero suicide raids and
unsupplied-siege desertion blunders, and 0/8 matches above eight seasons at the Mandate cap, but did not measure positive-EV
decree availability. A7 now also measures avoidable starvation, unanswered captures, directly attributed herd extinctions,
and idle labor; focused detector/integration tests pass. The herd-crash detector only attributes an extinction when a hunt
directly reduces a herd to zero, so the natural half-population crash is not misattributed.

On 2026-10-24, the user requested shorter testing. The 8-seed/64-minute gate and a subsequent 2-seed/64-minute
diagnostic were interrupted because of their runtime. A 1-seed/48-minute diagnostic was also stopped shortly after A1
began (2 matches, 50%) and A2 started; this tiny sample is not acceptance evidence. No current A1–A5/A7 or B27–B31
statistical result is claimed. A8 had passed both fog-fairness tests (2/2), including unseen Bronze
compact/caravan/Mandate perturbations. Short post-change validation passed: Release solution build (0 warnings, 0 errors),
62 focused WP9 tests, 3/3 source determinism tests, 3/3 view-determinism tests, strict validation (509 projects + 10
keystones, 0 errors/warnings), and toy, research, politics, and content-pack goldens. Research and politics expected
checksums were refreshed after the WP9 and checksummed herd-attribution changes. The full suite was capped at 2 minutes
before that refresh; it reported the old politics checksum and has not been rerun. A6 exploit search remains required
for B26. A later one-seed current-source `research --check-m10 --seeds 20261019 --minutes 64` diagnostic replayed
identically: KEY-4 median 44.2 min, E3 median 11.8 min, all four civilizations within the B5 timing bands, and chapter
end at minute 45.2 (0/1 Year-64 endings). This was a single-seed diagnostic. The current exact B5 two-seed Council sample passes: KEY-4 median 58.8 minutes, E3 median 19.0 minutes with all eight civilizations in band; that 0/2 Year-64 result is diagnostic only. The current exact B6 sample passed 32/32 seeds: median chapter end minute 61.2, 3/32 Year-Cap endings (9.4% against a ≤10% limit), and every replay matched.

Harness correctness fixes now protect bounded WP9 work: `Tournament.Play` stops at the evaluated AI chapter end, after
recording all telemetry for that closing season. Regressions verify the one-year cap at tick 600/four seasons despite a
three-minute request and exercise the real KEY-4 countdown with a cheap research pack, including terminal-season scoring.
A6 now reports underspecified searches as `INCOMPLETE` regardless of observed win rate; only
at least 10,000 policy evaluations at a 64-minute chapter horizon can pass or report an exploit. Bare `ai --exploit`
defaults to a two-minute, one-seed, two-policy diagnostic; explicit search options are preserved. Focused tests cover
chapter-end scoring, short high-rate classification, acceptance thresholds, and option defaults. A fresh bare-command
smoke completed in 11.3 s; after two evaluations its candidate showed 50% against Warlord, then returned `INCOMPLETE`
because A6 requires 10,000 evaluations and a 64-minute horizon. This tiny diagnostic is not acceptance evidence; no A6
acceptance search was run.
`ai --check-m10` labels every individual gate and the aggregate diagnostic-only unless the sample has at least 8 seeds
and a 64-minute horizon. Its safe defaults are 1 seed and 2 minutes; if either option is omitted, that dimension uses
its diagnostic default while the explicitly supplied dimension is preserved. Run the full sample with
`dotnet run --project src/CivSim.Harness -c Release -- ai --pack civsim.pack --check-m10 --seeds 8 --minutes 64`.
Pure resolver/status tests cover omitted options, each missing dimension, and explicit full-window values. No tournament
was run for the defaults guard.

WP11 now has a fixed-seed `bronze-v1` chapter-end golden. Its two fresh seed-20261019 runs agree at tick 27,150, with
RivalCountdown by civilization 3 and checksum `a0360f53f234e87b`; the explicit golden check passed. The S12 Bronze
chapter gate also passed locally: four Balanced AIs ended at tick 27,150 in 23.952 s against the 30 s budget, with
the final tick matching the chapter end rule. B22 S1 later completed 1,000/1,000 full 64-minute replays; S2 cross-platform
macOS/Windows verification remains open. B23's S12, S13, and S14 requirements now pass.

**A1 follow-up (2026-10-27):** The exact Warlord-vs-Council cohort (8 seeds, 64 minutes, 16 mirrored matches) remains 13/16 wins (81%) after the B30 refusal feedback fix; the refreshed means were Legacy 6,064.3 vs 4,649.9. The earlier Warlord Keystone-tempo override of 30 improved a three-seed screen from 3/6 to 4/6, but the exact cohort remained 13/16 (Legacy 6,110 vs 4,618) and shifted which mirrored match was lost. A temporary 70% Strategist Found threshold produced no outcome or score changes on the same three-pair screen. The first-expansion Warlord candidate cap of 7 also held at 13/16, lowered mean Legacy, and raised B30 overflow to 5/16 (0.3125/match), so it was reverted. A KeystoneTempo 28 override screen likewise remained 13/16 (Legacy 6,019.4 vs 4,688.5) with B30 at 4/16 (0.25/match), so it was rejected. The three A1 losses occurred in seed 20261021 mirror 1, seed 20261023 mirror 1, and seed 20261025 mirror 0; their settlement and Legacy profiles differ, so no shared causal tuning is established. A bounded settlement timeline trace for seed 20261025 did not show a consistent daughter food-capacity deficit. A1 and B26 remain open; no full B26 gate is claimed.

**A5 Builder scale screen (2026-10-27):** A paired eight-seed FFA screen compared the Builder KnowHow scale 1.30 with 1.25 across the same 40 matches. Builder moved from 12/32 appearances (37.5%) to 13/32 (40.6%), while Warlord moved from 8/32 (25.0%) to 10/32 (31.2%); the candidate therefore failed to reduce Builder's share and pushed another archetype over the 30% ceiling, so the source scale remains 1.30. Grower moved from 10/32 (31.2%) to 7/32 (21.9%). This is a short paired screen, not A5 acceptance; the 24-seed gate remains open.

**A3 current-source follow-up (2026-10-27):** After the fission tie-symmetry change, the exact 8-seed, 64-minute Warlord-vs-Rusher cohort scored 10/16 wins (62%; target ≥70%) and lost a settlement in 1/16 matches (93.8%; survival target ≥80%). Mean Legacy was 5,638 vs 4,845. A3 survival passes, but its win criterion remains open. The 1-seed, 2-minute all-gates smoke completed in 7.4 seconds and correctly labeled every measured result diagnostic-only; it reached no KEY-4 forecasts or believed-bronze raid cases and is not acceptance evidence.

**Current B26/B27–B31 cohort (2026-10-27):** The exact `ai --check-m10 --seeds 8 --first-seed 20261019 --minutes 64` run completed in about 15 minutes. A1 was 81% (13/16, fail); A2 Grower 69% (fail), Builder 88% and Wanderer 88% (pass); A3 win 62% (fail) and survival 94% (pass); A4 was 44% (above its 25–40% band). A5's 24-seed FFA scored Builder 35.4% and Grower 36.5% (fail; both exceed 30%); Wanderer 14.6%, Warlord 21.9% and Sage 16.7% pass. A7 passed keystone stalls, dithering and undefended losses, but recorded 1,601 avoidable-starvation deaths, 7 directly attributed herd crashes and 23 idle-labor seasons. B27 failed at 13/28 tin-poor Warlord seats reaching tin or arsenical bronze (46%) and 2 dormant-route blunders/16 matches (0.125 per match). B28 had no qualifying believed-bronze raid opportunities (0/0), so its result is unsampled and cannot establish defense timing. B29 had 4 forecasts within 15% (median error 2.5%) from 28 attempts; the 24 unmatched attempts need to be split between forecasts missing at capture and KEY-4 completions censored by the horizon. B30 failed on positive-EV Mandate overflow (21 Warlord seat-match episodes/16 matches = 1.312 per match); the corrected detector checks Mandate at season start and requires that no paid Decree resolved. Suicide raids, unsupplied-siege desertion and naked caravans were zero. A bounded seed-20261019 trace showed the cause: the Warlord selected a high-EV Expedition which was refused `OutOfReach` for eight consecutive seasons, then repeated the pattern on other unexplored targets. The forecaster uses a 90% route prior because actual pathfinding reads unseen terrain; filtering by live `CanReach` would leak hidden map state. B31 was 38% (fail). A bounded seed-20261019 settlement trace showed Warlord starvation events in seasons 61 and 68 while post-step stores were 19.3 and 27.0 and food security was 1.00; because the A7 predicate only requires positive pre-season stores, this does not prove those deaths were avoidable. B26 remains open; do not change gate thresholds from this run.

**B28 response follow-up (2026-10-27):** The two baseline qualifying cases were 0/2, both hitting a settlement with DEF-03 researched but no palisade, hillfort or walls. The Strategist now prioritizes a feasible physical defense at the nearest exposed settlement when its belief contains MET-04/05 evidence and a supported threat; selection uses only believed projects, threat probability, recent sightings and own settlement state. Focused policy/build/telemetry tests pass 24/24. A new decision-level test also verifies the Strategist dispatches the physical-build priority for a current believed-bronze threat (1/1). The bounded 8-seed, 64-minute rerun completed in Release: 0/0 qualifying believed-bronze raids, with 29 AI-vs-AI settlement raids and 7 actual-bronze launches. A 16-seed B28-only attempt was interrupted after four minutes without producing results. The harness now reports zero qualifying cases as INCOMPLETE rather than FAIL; B28 remains unsampled with the 80% threshold unchanged.

**B27 tuning follow-up (2026-10-27):** The exact baseline remains below target at 13/28 tin-poor seats (46%) with 2 dormant-route blunders/16 matches (0.125). SecureTin now gives an evidenced arsenical-copper mine the same +50 gross-value bonus as a Tin mine, Tin compact, or Tin Folk levy; candidate legality, belief evidence, forecast success probability and Mandate cost remain in force. Focused policy tests pass. A new `ai --check-b27` mode defaults to one seed at the 64-minute chapter horizon and reports this gate without running the broader M10 suite. Its first bounded run (seed 20261019, one mirrored pair) measured 4/4 tin-poor seats recovered and 0/2 dormant-route blunders; this is diagnostic only, far below the required eight-seed sample. B27 remains open with no match-level acceptance change.

**B27 failure trace and candidate screen (2026-10-27):** The exact eight-seed B27-only run after the arsenical bonus change measured 22/28 recovered seats (79%, one seat short) and 0/16 dormant-route blunders (pass). `ai --check-b27` now prints compact traces only for unrecovered seats, including checkpoint timing, Tin/arsenical access, candidate deposits, research states, selected decrees and route-candidate EVs. Three failures had only Copper mine candidates in the trace; one reached the tin-poor checkpoint at season 241 with 15 seasons left; two repeatedly chose Tin Folk Levies. A bounded screen with the SecureTin Levy bonus removed still scored 22/28, so that speculative policy change was reverted. Keep the 80% threshold; B27 remains open pending a causal route or research-priority fix.

**B29 diagnostic follow-up (2026-10-27):** Added `ai --check-b29` so forecast fidelity can be measured without running the other WP9 gates, with optional per-seat details for missing KEY-4 estimates. The one-seed source run recorded 1/1 observed forecasts within 15% (4.5% median), while three of four attempts had no KEY-4 estimate; each had a current Spark decision but KEY-4 fell outside its 72-season rollout. Temporary Warlord horizon-4 and horizon-5 packs were screened on the exact eight-seed cohort without changing acceptance thresholds or source tuning. Horizon 4 measured 20/20 forecasts within 15% (1.6% median) from 31 attempts, with seven missing estimates and four KEY-4 completions censored by the 64-minute observation cap. Horizon 5 measured 19/19 within 15% (2.1% median) from 29 attempts, with no missing estimates and ten censored completions. At horizon 4, two missing cases had KEY-4 available with zero blockers but did not complete inside the rollout; another still lacked GOV-06 and had 16/21 E3 breadth. The observed forecasts are accurate, but longer planner horizons do not resolve the chapter-cap censoring. The B29 command now classifies missing forecasts and observed errors over 15% as FAIL, while any otherwise unfailed censored forecast leaves the result INCOMPLETE and blocks PASS until its outcome is observed. Keep the Warlord horizon at 3 and B29 open; thresholds are unchanged.

**B28 forced-opportunity path check (2026-10-27):** A deterministic scenario staged visible MET-04, a supported war threat, DEF-03, and building resources. With Strategist dispatch already covered by its decision test, the scenario issued the same physical-defense order through Politics, let the live economy finish a palisade, then launched an AI-vs-AI settlement raid and passed the pre-raid defense snapshot through the B28 telemetry rule. The focused test passed 1/1 in 3 seconds. This verifies the staged policy-to-construction-to-measurement path; it does not replace natural cohort evidence, so B28 remains unsampled at 0/0 and its 80% threshold is unchanged.

**B29 horizon/observation cross-check (2026-10-27):** The same seed (20261019) was observed to 80 minutes with production Warlord horizon 3 and with a temporary horizon-5 pack. Horizon 3 yielded 1/1 observed forecast within 15% (4.5% median), three missing forecasts at KEY-3, and zero censored forecasts. Horizon 5 yielded 4/4 within 15% (0.6% median), with no missing or censored forecasts. Each one-seed diagnostic took about one minute. The paired result shows that a longer planner horizon plus longer observation resolves this seed's forecast gaps, but it is not acceptance evidence and changes neither the production horizon nor the published eight-seed, 64-minute B29 measure; B29 remains open.

**B30 route-refusal follow-up (2026-10-27):** The Strategist records the route-based Decree it issued and, after that season's politics step, reads its own `DecreeRefused:OutOfReach` or `DecreeRefused:NoDonor` event. At the Mandate cap it pauses blind Expedition and Levy candidates for eight seasons, then resumes them; this uses own refusal feedback and stored AI state without checking hidden routes. The first bounded trace after the Expedition-only cooldown still showed repeated Levy `OutOfReach` refusals, so the cooldown was extended to both route-based Decrees. A focused regression checks both refusal reasons and that the pause suppresses only Expedition and Levy, leaving Mine, Found and Compact choices available. The four-match screen exposed six consecutive Expedition `NoDonor` refusals before an `OutOfReach` refusal. The subsequent exact eight-seed B30-only cohort recorded 2/16 overflow episodes (0.125 per match), passing the unchanged 0.3 ceiling; the prior 21/16 result predates the `NoDonor` trigger. Other B30 signals remain zero.

**A7 starvation measurement correction (2026-10-27):** The prior detector counted every starvation-cause death with positive reachable stores, but the simulation's baseline starvation hazard applies even in well-fed seasons. It now replays the deterministic cause-selection draw to distinguish deaths attributed to the conditional Winter shortfall hazard, and counts those only when reachable food was present at season start. The zero target is unchanged. Focused tests cover the attribution interval, reachable-food predicate, and snapshot-resume consistency. A one-seed, 64-minute diagnostic after this correction recorded 94 shortfall-attributed starvation deaths across two mirrored matches (47/match), 3 herd crashes (1.5/match), and no keystone stalls, dithering, undefended losses or idle labor. A7 still fails this diagnostic and B26 remains open; this corrects the measure without changing AI behavior or mortality.

**A7 reachable-food correction (2026-10-27):** The previous positive-stock rule still counted food that could not prevent a seasonal shortfall. The detector now samples stores after the simulation's actual season-start GOV-04 redistribution and requires those stores to cover the settlement's full seasonal need. A sister settlement's proximity or SOC-07's strategic-goods radius alone does not qualify. The zero target is unchanged. A one-seed, two-minute diagnostic returned zero A7 blunders and passed the short diagnostic, but is not acceptance evidence; this stricter measure supersedes the 94-starvation diagnostic above.

**A7 acceptance update (2026-10-27):** The individual-herd hunting path was missing the sustainable yield limit used by the aggregate-herd path. Hunting now respects both the settlement's seasonal sustainable yield and each herd's share of its own annual growth. A focused regression confirms that high hunting work cannot drive an individual herd to zero. The exact 8-seed, 64-minute A7-only cohort then passed all six checks across 16 mirrored matches: zero keystone stalls, dithering, avoidable starvation deaths, undefended losses, directly attributed herd crashes, and idle-labor seasons. The idle-labor detector now compares eligible workers within each food-deficient settlement; levy and siege hosts intentionally removed from work capacity are excluded. This corrects the prior civilization-wide correlation and levy-expiration boundary false positives. No A7 threshold changed; A7 is closed for this cohort, while B26 remains open on its other failures and gates.

**B27/B31 acceptance update (2026-10-27):** After prioritizing Tin Folk compacts, B27 passes the exact eight-seed cohort at 23/28 tin-poor recoveries (82.1%) and 0/16 dormant-route blunders. The Legend first-expansion search probe keeps later search depths and the winter rule unchanged; its mirrored seed-20261019 diagnostic measured 434 Spark rollouts per Legend decision versus 234 per Warlord decision (1.86×). The exact eight-seed B31 duel then scored 11/16 Legend wins (68.75%), passing the ≥60% gate. B31 is closed; B26 remains open on its other failed checks, and B28–B30 remain open or unsampled as recorded above.

**A3 candidate cross-gate screen (2026-10-27):** The harness-only combination of a fixed Warlord rival-pace override of 2.0× and max-force campaign-origin selection scored 10/10 on the five-seed A1 screen (baseline 7/10), but on the exact eight-seed A2 Grower cohort it scored 8/16 versus the current-source baseline's 10/16. This supersedes the older 11/16 A2 baseline recorded above; the source has since changed. On a four-seed A4 screen, Chieftain won 3/8 candidate matches versus 4/8 baseline. A two-seed A5 FFA screen shifted Warlord wins from 1/8 to 3/8, while Builder and Grower stayed at 3/8 each; this is a small-sample signal against the 30% Warlord cap, not acceptance evidence. The combination is rejected as a global tuning: its A1/A3 gains do not carry across gates, and no production behavior or thresholds changed.

**A2 same-team war coordination probe (2026-10-27):** A harness-only forced alliance between the two Warlord seats was tested in both orientations of seed 20261022 at 64 minutes (two matches; 23 seconds). It removed the same-team Warlord war declarations seen in the baseline losing orientation, confirming that the team-scoring setup permits a real coordination conflict. The losing orientation remained a Warlord loss and was slightly worse in this single pair: Warlord Legacy 4,511 vs Grower 6,402, with 5 settlements lost and 1 captured; the baseline trace had 4 lost and 3 captured. The forced alliance bypasses normal alliance research/formation rules, so this is only a causal probe. It does not establish that coordination explains A2's broader failure, and neither A2 nor any other gate is changed by this result.

## 19. WP10 view checkpoint

The game has the Decrees/forecast panel, compact and embargo controls, countdown and Bronze end summary, the E3 research column,
four E3 overlays (Deposits, Trade, Cohesion, Forest), figure/tool-tier tinting, Bronze coach tips, and the current-sight tile card
for mining/forest/cohesion details. The forest command-line overlay name is accepted; compact proposals show a likely/possible/
unlikely estimate when the view API can compute it and say unknown otherwise. The local S12 Bronze chapter gate passed in
23.952 s. S13 then passed its exact 10-minute tick/GC sample at 1.244 ms mean and 4 ms maximum GC pause. S14 passed all 24 Bronze zoom/overlay cases at 1,600 figures; the minimum one-second rate was 92.8 fps. B23 is complete.

## 20. Combined verification checkpoint

Before the latest A7 detector and herd-attribution changes, the Release solution build passed with 0 warnings/errors, the
full suite passed **555/555**, strict validation reported 509 projects + 10 keystones in 33 lineages with 0 errors and 0
warnings, and `toy-v1`, `research-v1`, `politics-v1`, and `content-pack.txt` goldens matched. The historical two-seed AI
diagnostic above returned INCOMPLETE and surfaced small-sample A3/A4/A5/A7 misses; its B29 and expanded B30 counters
predate the new telemetry. After the latest changes, the Release solution build passed with 0 warnings/errors; this turn's
broader WP9-focused set passed 91/91 in 19 s and source/view determinism passed 6/6 in 12 s. The bare-command smoke
returned `INCOMPLETE` at 2 evaluations/2 minutes in under 30 s. Strict validation reported 509 projects +
10 keystones with 0 errors/warnings. Toy, research, politics, and content-pack goldens match; research and politics expected
checksums were refreshed after WP9 and checksummed herd-attribution changes. A full-suite run was capped at 2 minutes
before the golden refresh and reported the old politics checksum; it has not been rerun. The current Bronze chapter golden
passed a two-run same-seed check at tick 27,150 (checksum `a0360f53f234e87b`), and the local S12 chapter benchmark passed
at 23.952 s. The one-seed research diagnostic passed its timing bands but is not multi-seed acceptance. The latest focused A8 fog-fairness
run passed 2/2. At the user's request, the long AI gates were stopped; B27 and B31 have since passed their exact cohorts.
At this checkpoint, A1–A5/A7 and B28–B30 were open or unsampled, and A6 had not yet run. Later updates above close A7, B27, B30 and B31, and record A6/A8 as passed. The remaining M10 AI blockers are the A1–A5 misses, B28's unsampled 0/0 cohort, and B29's missing or censored forecasts; the aggregate B26 gate remains open. The prior `research --check-m10`
passed with KEY-4 median 45.8, E3 median 12.0, chapter-end median 47.2, and replay-identical checksums. A
tiny-positive-stock barter ratio overflow found during an earlier run was fixed by clamping before division, with a
regression test. The final game Debug build had passed with 0 warnings/errors at the earlier checkpoint.

After the short-sample S13 safeguard, `dotnet build CivSim.slnx -c Release --no-restore` passed with 0 warnings/errors,
and the combined focused set (WP9 harness/AI, determinism, and acceptance sample-status tests) passed 98/98 in 12 s.
`git diff --check` is clean. These bounded checks do not replace the full suite or the open Bronze statistical,
cross-platform, performance, robustness, and human-playtest gates.

Since that combined run, a cheap KEY-4 countdown regression and a pure B27 telemetry-summary test were added. The latter
checks that tin recovery uses tin-poor eligible seats as its denominator while dormant-route blunders use matches; the
focused harness regression classes passed 5/5 in 4 s, and the Release solution build passed again with 0 warnings/errors.
After the M10 short-sample diagnostic guard, the Release build again passed with 0 warnings/errors and focused AI/harness
telemetry and acceptance-status tests passed 61/61 in 4 s. No long AI tournament or full suite was run.

After integrating M10 with `main` (merge commit `df523c9`), the combined `ResearchSpark` and Strategist behavior changed the
24-minute politics checksum. The refreshed `politics-v1` golden (`final=3b0097a2348e3076`, `digest=a3b5667993f4fdc0`)
reproduced on two consecutive runs. The conflict-sensitive focused set passed 77/77 in 10 s, and the Release test build
completed successfully. This integration did not run the full suite or any long AI acceptance tournament.

The S15/B24 acceptance command now treats horizons under 64 minutes as diagnostic and skips the soak entirely, so a short
request cannot spend hours running the default 1,000 matches and then claim acceptance. A bounded smoke using
`--s15 --minutes 10` returned `ACCEPTANCE INCOMPLETE` without entering the soak. The B30 overflow-streak transition now has
focused coverage for the ninth qualifying season and resets when eligibility or the Mandate cap ends. The combined status
and B30 tests passed 25/25 (93 ms test runtime); no tournament or soak ran.

The same review found S1 could accept 1,000 ten-minute replays as the B22 chapter-replay gate. S1 now accepts only
exactly 1,000 matches on a horizon of at least 64 minutes. It stops each replay at the exact `MatchEnd.EndTick` and fails
a match that does not reach that endpoint. Smaller requests skip replay work and report diagnostic-only. The `--s1 --minutes 10`
smoke returned `ACCEPTANCE INCOMPLETE` without starting replays; `AcceptanceSampleStatusTests.FullS1RequestInvokesReplayGateCallback`
also verifies that exactly 1,000 matches at 64 minutes enters the replay callback and propagates its result. This is a
boundary test only; no chapter replay corpus was run. The combined S1/S15/B30 focused tests passed 32/32 in 24 ms.

S13 now follows the same short-horizon safeguard: `acceptance --s13 --sample-minutes N` skips the tick gate when
`N < 10`, prints the existing S13 diagnostic line, and leaves overall acceptance INCOMPLETE. At 10 minutes or more,
the tick gate runs normally and keeps its pass/fail output. Pure callback tests cover 1- and 9-minute skips and the
10-minute execution boundary. The `--s13 --sample-minutes 1` smoke returned `ACCEPTANCE INCOMPLETE` without warm-up;
the combined status and B30 tests passed 35/35 in 42 ms. No S13 simulation or full suite was run.

WP11 now has bounded command-driven binary `MatchReplay` round-trips through first KEY-3 and Tin Folk emergence (`WP8aLifecycleTests.Tin_Folk_emergence_is_preserved_by_a_binary_match_replay`), queued refusals for commands 18–19 (`WP8aDecreeTests.Snapshot_and_match_replay_after_18_and_19_commands_reproduce_queued_refusals`), and a successful Tin embargo toggle (`WP8aDecreeTests.Successful_embargo_command_round_trips_through_binary_match_replay`). The refusal case does not cover a successful embargo; the successful replay does not exercise the contact-based relation penalty, which is covered by the direct API test. Siege, secession, and Tin Folk capture replay scenarios remain open.

B4's event-stream test passed locally: all 66 numeric E3 nodes emitted Completed, Moment, and VisibleChange events (1/1 focused test; 198 ms test runtime). The validator portion of B25 also passed: 509 projects and 10 keystones, zero errors or warnings (2.4 s wall time). These checks close structural portions only; broader M10 acceptance remains incomplete.

**Bounded continuation (2026-10-26):** the exact current-source B5/B6 refresh passed in about 45 seconds: KEY-4 median 58.4 minutes, E3 median 18.6 with every civilization inside the timing band; B6 median chapter end 60.5, 1/32 Year-Cap endings, and replay-identical results. Exact B7 passed in about 50 seconds at 71.9% against its 65% target. Exact B8 remains below target at 53.1%; the eight-seed trace and two low-cost policy experiments did not show a Legacy win-rate improvement, so no further threshold microtuning is planned. B9's two live fixture means were 192.7 (seed 20261019) and 173.6 (seed 777), inside the 171–209 band; one seed-777 civilization measured 168.0, so retain the per-civilization detail when judging the gate. The initial 24-seed B10 report was diagnostic: its realized child/adult ratios were 0.927/1.172 and realized epidemic share was 5%; its earlier expected-hazard ratios (0.803/1.212) and e45 (14.2) used the live sampled E0 denominator and included conditional winter-shortfall mortality. The corrected baseline accounting and final expected-share B10 result are recorded below. B11's method-rung report passed: chop x1.50 and mining x1.95. Focused B4, MetalEconomyTests, and WP8b tests passed 26/26 in 9 seconds; `metal_sim.py --check` passed; the 4,000-trial B15 E3 combat report passed every row. A fresh strict B25 validation found 509 projects and 10 keystones with zero errors or warnings. The 64-minute `report-metal` output is diagnostic only: it shows low live T18 rung-6 coverage, so fixture/model passes do not establish live-match coverage. B12 live coverage, B13 live pressure, and B17/B19 remained open at that checkpoint alongside the previously documented AI, cross-platform, and human gates.

**A6 runtime check (2026-10-26):** a bounded 33-candidate, one-minute screen took 4.11 s with the existing final-checksum path and 4.23 s with a temporary skip-checksum path. The difference was within run-to-run noise, so the optimization was discarded. Candidate simulation, not the final hash, is the measured cost at the one-minute screen horizon. The prior exact A6 acceptance search remains the 10,009-policy run, completed in 24:39 with all 16 finalists validated.

**B10 cause diagnostic (2026-10-26):** the one-seed current-source `research --report-e3 --seed 20261019 --minutes 64` completed in about 10 seconds. It measured a 5% realized epidemic share and 22% starvation share in E3; expected adult epidemic hazard was 0.81‰ within 13.4‰ total. Expected starvation was 3.38‰, including a 2.36‰ winter-shortfall surcharge (also reported separately for diagnosis). Realized child/adult E3:E0 ratios were 0.557/2.610; these four-civ results are diagnostic only and highly sample-sensitive. Corrected baseline expected E3 rates were 34.4‰ child and 11.1‰ adult: anchor-normalized ratios 0.763/0.737 (targets 0.803/0.779), while live sampled E3:E0 expected ratios were 0.666/0.849. Baseline expected e45 was 17.1; including conditional winter shortfall it was 15.4. The static `check_e3.py --fixed --check` midpoint model passes. This single seed is diagnostic only; see the same-seed-set sensitivity below for the expected-share comparison.


**B10 corrected 24-seed accounting at the original 0.20 inflow (2026-10-26):** the existing 24-seed sample was refreshed once after splitting conditional winter-shortfall mortality from the baseline expected hazard. T13 baseline expected E3 rates were 35.1‰ child and 11.6‰ adult, or 0.780/0.773 against the §4.1 annual-effective E0 anchors (targets 0.803/0.779; both inside ±10%). Expected baseline e45 was 16.8 (pass); including the shortfall surcharge gives 14.2 as a separate stress measure. The live sampled expected E3:E0 ratios are 0.690/0.887 because sampled E0 is 50.8‰ child and 13.1‰ adult versus anchors 45‰ and 15‰; this is diagnostic and does not redefine the T13 target. The expected baseline epidemic share at 0.20 was 6.3% (below the finalized 7–13% band); the realized full-era child/adult ratios 0.927/1.172 and epidemic share 5% are diagnostics, not acceptance measures. This separates the static baseline comparison from the sampled live E0 diagnostic.

**B10 expected-share acceptance result (2026-10-26):** the 24-seed, 64-minute sensitivity at `EpidemicInflowE3 = 0.30` passes the finalized expected-hazard measures: child/adult anchor-normalized T13 ratios are 0.785/0.781 (targets 0.803/0.779 ±10%), baseline expected e45 is 16.8 (target 17 ±10%), and the age-exposure-weighted expected epidemic share is 7.1% (target 7–13%). The share denominator is total expected baseline mortality hazard; the conditional winter-shortfall surcharge is excluded. The 5.9% surcharge-inclusive share and 5% realized ledger share remain diagnostics only. B10 passes on this 24-seed sample under the expected-share criterion now stated in 22 §16.

**A3/WP9 bounded follow-up at the 12-tile checkpoint (2026-10-26):** campaign force planning and runtime shared the 12-tile gather radius, four-fighter home reserve, and six-attacker launch floor. The radius had been increased from 10 to 12 after the planner/runtime audit; planner-only force estimates required the actual six-attacker threshold. GOV-02 received a conditional `PostureUnlockBonus = 36` only while the frontier was open and neither defense nor attack urgency was high. A bonus-48 trial did not improve the matched eight-seed A3 win rate, so 36 was retained. The current-source 8-seed × 64-minute mirrored duel (16 matches; about 35 seconds) produced 62% Warlord wins, mean Legacy 5,672.9 vs Rusher 4,805.9, and no Warlord settlement loss in 15/16 matches (93.75%). The clean-survival criterion (≥80%) passed; the ≥70% win criterion remained open. In the seed-20261019 losing orientation, Warlord Firsts were 684 vs Rusher 903 while Rusher settlement raids succeeded in 97/97 resolved attempts; the threat model predicted 1.00, so lowering Fortify urgency or the four-fighter reserve was not supported.

**A3 Firsts-ETA diagnostic (2026-10-26):** the initial same-season comparison was too close to each claim to estimate pace, so the harness now compares each claim with the nearest Warlord forecast from 4, 8, and 16 seasons earlier. On mirrored seed 20261019, these lookbacks show broad lateness across eras and lineages: at four seasons, 20/23 finite forecasts in the Warlord-win orientation and 174/183 in the loss orientation predicted claims too late (median actual-minus-predicted −11 seasons in both); at 16 seasons, 14/16 and 150/173 were late (median −12 and −10). A rough linear pace proxy often exceeds the model's 2× cap, but the rival frontier is a nonlinear project DAG, so this is diagnostic rather than a pace estimate.

**A3 rival-pace screen (2026-10-26):** a harness-only fixed Warlord rival-pace override was compared on the same 8-seed × 64-minute mirrored sample. Baseline was 62% Warlord wins; 1.5× produced 50%, 1.75× produced 62%, and 2× produced 69% (11/16, below the ≥70% gate). Warlord settlement losses were zero at all three settings, so clean survival still passed. The 2× setting brought median ETA error near zero at 8-season lookback, but did not clear the win gate; no production pace change was adopted. Detailed matches showed repeated campaigns rejected below the six-attacker floor, motivating the radius trial below; the four-fighter reserve and six-attacker floor remain unchanged.

**A3 campaign-radius screen (2026-10-26):** increasing the shared planner/runtime gather radius from 12 to 14 tiles improved the matched 8-seed × 64-minute duel from 10/16 to 11/16 Warlord wins (68.75%, still below the ≥70% gate). Mean Legacy was 5,714.3 vs Rusher 4,772.5; Warlord lost no settlements in 16/16 matches, so the ≥80% clean-survival gate passed. Rusher lost 1.62 settlements per match. A same-seed four-seed screen at radius 16 also produced five Warlord wins in eight matches, identical to radius 14's screen. However, the exact 24-seed, 80-minute B17–B20 rerun at radius 14 failed B17 (87/96 KEY-4 completions and 13.5% City prevalence), B18 (57.0/29.2/13.8/0.0 Legacy split), and B19 (87/96 complete E3 exposures); B20 passed at 4.8 raids/match, 79.2% with war, and 41.7% with capture. Radius 14 is therefore rejected and the production radius remains 12. The four-fighter home reserve and six-attacker floor also remain unchanged. A3's win gate is still open.

**A3 Firsts ETA and Raid posture screens (2026-10-26):** an eight-season harness-only penalty to contested Firsts ETA did not improve the four-seed mirrored screen: both baseline and candidate won 5/8 matches, with zero Warlord settlement losses. Candidate Legacy was 5,675.9 vs Rusher 4,869.1 (baseline 5,667.4 vs 4,784.8); mean Firsts Legacy was 957.4 vs 457.1. The penalty changed Rusher settlement losses from 1.75 to 0.75 per match but did not change the win count, so no production valuation change was adopted. A Warlord-only increase of the non-Conquer Raid-posture streak threshold from three to four was screened on the same single mirrored seed at 64 minutes. It produced identical outcomes and posture seasons to baseline, and the candidate recorded zero eligible three-season non-Conquer streak opportunities across the two matches. This hypothesis is inert on that seed; no larger run is warranted. The active Conquer-to-Raid branch, four-fighter reserve, six-attacker floor and 12-tile radius remain unchanged. The follow-up campaign-origin measurement and selector screen are recorded below.

**A3 campaign-origin follow-up (2026-10-26):** harness telemetry now counts rejected campaigns where the selected origin is below the six-attacker floor but another adult-eligible origin could gather six, separately from cases where every alternate remains short. One mirrored seed produced 6 possible alternate-origin rescues and 40 still-short rejections. A harness-only max-force origin selector was shared by planner and runtime; alone it scored 10/16 wins on the eight-seed mirrored cohort, equal to baseline, with no Warlord settlement losses. The selector therefore did not close A3 by itself. Combined with a fixed Warlord rival-pace override, four-seed screens scored 5/8 at 1.75× and 6/8 at 1.9×; the eight-seed 2.0× combination scored 12/16 (75%), with no Warlord settlement losses and mean Legacy 5,554.9 vs Rusher 5,001.5. This passes the sampled A3 win threshold but is diagnostic only: the pace override is fixed at its 2.0× cap, and neither override is production behavior. Pace alone reached only 11/16, and origin selection alone remained at 10/16, so the combined result does not justify adopting either change. A3 remains open; no production constants changed, and the accepted radius-12 B17–B20 configuration remains intact.

**A3 frontier, fission, and defense-research diagnostics (2026-10-26):** after correcting the Warlord seat targeting, raising `FrontierEarlyBonus` from 0.15 to 0.30 scored 4/8 wins on the four-seed, 64-minute mirrored screen versus the same-cohort baseline's 5/8; mean Legacy was 5,606.9 vs Rusher 4,911.6 (baseline 5,667.4 vs 4,784.8), with no Warlord settlement losses. Warlord-only E3 Expand fission threshold overrides of 0.80 and 0.75 each stayed at 5/8, matching baseline. The 0.80 screen had mean Legacy 5,665.2 vs 4,774.0 and Warlord land share 13.6% vs 10.9%; the 0.75 screen had 5,687.9 vs 4,784.2 and land share 13.0% vs 11.1%. These small-cohort land/Legacy movements did not lift wins, so all three candidates were rejected. Reducing the Warlord's research `DefenseBonus` from 40 to 20 scored 4/8, again below baseline, with no survival change and lower Warlord land share (11.3% vs 12.3%). A non-mutating one-seed shadow replan then compared live slots with a plan that set defense urgency to zero only in research valuation. The no-shadow and shadow matches were identical (1/2 Warlord wins, mean Legacy 5,964.1 vs 5,077.9, no Warlord settlements lost). Across 497 high-urgency decisions, 369 planned slot positions changed, but only 35 live DEF/WAR slot positions were replaced by non-DEF/WAR projects; the decisions were concentrated in E2–E4. The urgency signal affects the plan, but lowering its bonus did not improve A3, so production defense valuation remains 40 and the ≥70% A3 win gate remains open.

**A1 fission-site symmetry screen (2026-10-26):** the baseline five-seed × 64-minute mirrored screen (10 matches) won 7/10 for Warlord. Fourteen of 151 Warlord fission-site decisions had tied best scores. A tie-break based on the parent site's side of the 180° map rotation now selects mirrored sites for mirrored parents with mirrored candidate sets; a focused symmetry test passes. The same ten matches remained 7/10 after the change (mean Legacy 5,922.7 vs Council 4,773.3; baseline 5,928.3 vs 4,863.7). Keep the tie-break as a symmetry correction, not as an A1 balance fix. A1 remains unaccepted and the 10-match screen is diagnostic only.

**B22 current-source golden refresh (2026-10-26):** the prior `bronze-v1` expected checksum did not match the committed source even before the fission tie-break follow-up. The current-source seed-20261019 chapter replay now ends at YearCap, tick 38,400, checksum `bdeb8a6b4ecb025e`. `--write-bronze-golden` followed by `--check-bronze-golden` passed; each command compared two fresh runs for identical tick, end state and checksum. This short golden refresh does not replace the previously completed 1,000-match S1 run.
