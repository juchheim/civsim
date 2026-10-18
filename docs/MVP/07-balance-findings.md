# 07 — Balance Findings (WS-A)

> Results of the first balance model (workstream A in [06-next-steps.md](06-next-steps.md) §3, milestone D2): one civilization simulated season by season from the first band to the KEY-10 Apex, with the v0.1 numbers as written and with a tuned set. This document lists what breaks, why, and the **constants I recommend** in place of the provisional ones in [02](02-research-structure.md), [05](05-simulation-structure.md) and [01](01-game-structure.md).
> Still design only. The model is a spreadsheet: [balance/balance-model.xlsx](balance/balance-model.xlsx). Its generator and a Python mirror of the same formulas are in [balance/tools/](balance/tools/).

---

## 1. Summary

**v0.1 misses the pacing targets in both directions.** The full arc takes **350 min** at Standard pace (target ≈ 180). The curve is inverted. E0–E2 run 4–7× too long: E0 takes 58.5 min against a target of 7–9. E7–E9 run 1.4–2× too short: E9 takes 15 min against 28–32. The first fission happens at minute 77 (target 6–10). Completions per era are the one thing that already holds, because breadth N was derived from those targets.

Two independent causes:
1. **The opening band makes far too little Insight.** Twelve people produce about 5 Insight per minute. E0 asks for about 372 (13 picks plus the keystone phase).
2. **Insight compounds faster than EraBase grows.** Population grows by the clock, so a longer era also means more growth within it. Add the multiplier stack on keystone closures and Insight per minute grows about 2× per era early and 4–7× per era late. EraBase grows a flat 2.5×.

**With the recommended constants** (§5), the arc runs **171.5 min Standard, 86 min Quick, 297 min Epic**. Every era lands inside its target window. KEY-1 is reached at 7.5 min, KEY-2 at 17.5 min, and the first fission happens at 7.0 min. Across the five archetypes the Standard arc spans 154–178 min, well inside the ±25% tolerance in 06 §6.

| Change (headline) | v0.1 | Recommended |
|---|---|---|
| Commons Insight coefficient (02 §8.1) | 0.6 | **4.8** (all role and structure outputs ×8 too) |
| EraBase E0…E9 (02 §8.3) | 20 · 50 · 125 · 300 · 750 · 1,900 · 4,700 · 12k · 30k · 75k | **20 · 45 · 70 · 180 · 600 · 3,400 · 18k · 160k · 1.0M · 4.8M** |
| Breadth N (02 §13) | 13 · 14 · 20 · 22 · 21 · 16 · 14 · 14 · 18 · 17 | **unchanged except E3: 22 → 21** |
| Know-how rate (02 §9) | 0.05 per figure-equivalent per season | **35% of the practising figure's Commons Insight** |
| Pace (01 §6) | Quick costs ×0.5, Epic ×1.6 | **Costs ×1 at every pace; season = 7.5 s / 15 s / 26 s** |
| Base fertility (05 §9.2) | 0.06 | **0.13** per fertile woman per season |
| Representation scale E0…E9 (01 §8) | 1 · 1 · 2 · 5 · 10 · 25 · 50 · 100 · 250 · 500 | **1 · 1 · 1 · 1 · 2 · 5 · 25 · 100 · 250 · 500** |

## 2. The model

**Spreadsheet:** [balance/balance-model.xlsx](balance/balance-model.xlsx), opens in Excel or Numbers. Every simulation cell is a live formula. Edit any blue input on `Params` and both runs recompute. `Params` also has selectors for pace and archetype.

| Sheet | Contents |
|---|---|
| Results | Per-era pacing vs. the 02 §8.3 targets for both scenarios, with ✓/✗; acceptance checks from 06 §6 |
| Charts | Era durations vs. targets; era reached, population and Insight over time |
| Params | Every input: scalars, per-era tables, pace and archetype tables. v0.1 and Tuned side by side |
| Catalog | Parsed from 03a–c: projects per era and type, average TypeMult, in-era critical paths, forced backfills |
| Learning | The 31 catalog effects that change Insight, cumulated into per-era multipliers |
| Derived_v01 / Derived_tuned | Per-era cost of the N picks, keystone phase, backfills, requirement, settlement caps |
| Sim_v01 / Sim_tuned | One row per season (15 s): cohorts, births, deaths, caps, fission, Insight, Know-how, era progress |
| Sweeps | Static table of every pace × archetype for both scenarios (from the Python mirror) |

**Research.** Each era requires three things:
1. The **N picks**: the in-era critical path at its exact type multipliers, plus free picks at the era's average multiplier, all × EraBase.
2. The **keystone phase**: the keystone sits in Primary at 4/7, while Secondary and Tertiary keep completing projects. This is where completions above N come from: about 1.6–1.7 per era.
3. **Forced backfills**: earlier-era projects on this keystone's closure that no earlier keystone needed, priced at EraLag 0.7^Δ.

Progress each season is Insight plus applied Know-how, and overflow carries into the next era.

**Population.** The model tracks three cohorts: children 0–13, adults 14–44 and elders 45+. Hazards are seasonal, with a winter multiplier. Births are damped by crowding against settlements × MIN(FoodCap, HousingCap, GroupCap × Cohesion). A settlement fissions when its average size passes 85% of that cap and the daughter band would be viable. The settlement count is limited by territory ÷ claim area.

**Verification.** The Python mirror ([balance/tools/balance_sim.py](balance/tools/balance_sim.py)) and the workbook formulas agree to 4 × 10⁻¹⁶. This was checked by evaluating the workbook with an independent formula engine (`build_xlsx.py --verify`).

**Limits.** The model covers one civilization: no rivals (diffusion appears only as a sensitivity), war, trade, disease or Wonder build times, and the KEY-10 uplink build is not modeled. Settlements are mean-field. The things the docs don't specify are marked **ASSUMED** on `Params` for WS-H and WS-F to replace: mortality tables, per-era food and housing caps, territory, specialist shares and archetype multipliers.

## 3. v0.1 as written

Standard pace, Balanced archetype:

| Era | Minutes | Target | In-era done | Pop at end | Insight/min | Know-how share |
|---|---:|---|---:|---:|---:|---:|
| E0 | **58.5** | 7–9 | 14.7 | 18 | 5 | 18% |
| E1 | **62.5** | 9–11 | 15.7 | 70 | 15 | 14% |
| E2 | **56.2** | 11–13 | 21.6 | 323 | 61 | 15% |
| E3 | **43.2** | 12–14 | 23.6 | 1,148 | 187 | 18% |
| E4 | **34.8** | 14–16 | 22.5 | 3,161 | 590 | 17% |
| E5 | **24.2** | 16–18 | 17.7 | 6,284 | 1,716 | 14% |
| E6 | 21.8 | 18–20 | 15.6 | 11,860 | 4,703 | 10% |
| E7 | **16.5** | 22–24 | 15.8 | 20,345 | 13,780 | 6% |
| E8 | **17.5** | 26–28 | 19.6 | 33,054 | 41,452 | 4% |
| E9 | **15.0** | 28–32 | 18.6 | 45,048 | 121,407 | 2% |
| **Arc** | **350** | ≈ 180 | | | | |

First fission: minute 77. Quick: 283 min (target 90). Epic: 411 min (target 300).

## 4. Findings

### F1 — The opening is ~7× too slow: rescale Insight
E0 needs about **372 Insight-equivalents**: 13 picks at 20 × TypeMult ≈ 288, plus the keystone phase ≈ 88. Under 02 §8.1 the starting band makes 0.6 × (8 adults + 2 × 2 elders)^0.85 ≈ **5 Insight per year**, and one year is one minute. Children make nothing, and nobody born in E0 grows up before E1, so E0 income cannot grow. Raising the band's output is the only lever that works there.

**Recommendation:** multiply the whole Insight formula by 8. The Commons coefficient becomes **0.6 → 4.8**. Role outputs become Elder 8, Shaman 12, Priest 16, Scribe 24, Scholar 48, Researcher 120, and Monastery 16. Keep the exponent at 0.85 and the elder ×2. Scaling everything by the same factor keeps the commons-vs-specialist ratios that 02 §8.1 intends.

The equivalent alternative is to divide every EraBase by 8, which would make E0 projects cost 2.5. I don't recommend it, because small costs read badly and leave no rounding room.

### F2 — The late game is ~2× too fast: steepen EraBase
With a clock-driven population, Insight growth per era grows with the era's length. Growth runs about 6% per year, and eras lengthen from 8 to 30 minutes. On top of that, keystone closures carry a dense stack of Insight multipliers:
- E7: Public Schooling ×1.5, plus +10% each from GOV-13 and BEL-13
- E8: Mass Education ×1.5, Electric Grid +10%
- E9: Personal Computing ×1.3, Internet +25%

In the tuned run, Insight per minute grows ×2.1 per era through E3, then ×2.9, ×3.7, ×4.5, ×6.2, ×6.8 and ×4.3. Fertility falls in E8–E9 (the demographic transition). A flat ×2.5 EraBase can't follow that curve.

**Recommendation: EraBase = 20 · 45 · 70 · 180 · 600 · 3,400 · 18,000 · 160,000 · 1,000,000 · 4,800,000.** These are fitted era by era to the target midpoints, then rounded. The per-era ratio is uneven (×2.3, ×1.6, ×2.6, ×3.3, ×5.7, ×5.3, ×8.9, ×6.3, ×4.8). That shape is real, not noise:
- **E2 is low** because it carries the heaviest backfill load (F8).
- **E7 is high** because of the Industrial population boom and the schooling multipliers.

If WS-B softens the late multipliers (for example LRN-10 and LRN-11 at ×1.25 instead of ×1.5), re-fit with `tools/fit.py` and the curve gets smoother.

### F3 — The Commons exponent is not the lever: keep 0.85
I re-fitted the whole table at exponents 0.85, 0.75, 0.70 and 0.65. Results were almost identical at every exponent:
- **Fertility ±15%:** 159 to 192 min.
- **Grower:** 154 min; **Wanderer:** 165 min.
- **EraBase ratio unevenness:** the largest per-era ratio is 5.6–6.3× the smallest.

Specialists are linear in population, and growth is clock-driven, so lowering the exponent mostly lowers the coefficient's absolute scale. **Keep 0.85.**

### F4 — Know-how fades to nothing under the v0.1 rule
At 0.05 Know-how per person per season, Know-how grows linearly with adults. Insight grows with every multiplier, so Know-how's share of research falls from **18% in E0 to 2% in E9**. Learning by doing would vanish exactly when the economy is most specialized. That contradicts 01 §9 and 02 §9.

**Recommendation: a practising figure generates Know-how equal to 35% of its own Commons Insight, credited to the lineages of what it practises.** Keep the drain rules: up to 100% of the slot rate, and at most 50% of the project's cost. Know-how then stays at **13–20% of research progress in every era** (15% on average), with only about 70% of practice landing in lineages that get slotted. Concentrated practice in one lineage should reach the 25–40% next-project discount that 02 §9 aims for. The aggregate model can't confirm that per lineage, so WS-B should check it.

### F5 — No fission before minute 77: demography needs to run faster
With 05 §9.2's base fertility of 0.06 per woman per season, a band of 12 grows by about one person per decade (12 → 18 people by minute 58). The first split comes at minute 77, against the target of 6–10 (06 §6 item 4). Pacing is also very sensitive to demography: **v0.1 with only the fertility change** runs a 170 min arc, but with the wrong shape (E0 still 41 min).

**Recommendations (WS-H inputs):**
- **Base fertility 0.13.** That's a birth about every two years per woman, a deliberate game-time compression.
- **Fission trigger at 85% of the settlement cap**: food or housing pressure, as in 05 §9.4's "OR pressure high".
- **Daughter group 40% of the settlement, minimum 6 people.**
- **E0 camp food cap ≈ 22** (forage in a radius-2 range).

Result: first fission at minute 7.0, KEY-1 at 7.5, KEY-2 at 17.5.

**Warning: first fission is a knife-edge.** Across archetypes it moves between minute 6 and 13. Caps that rise at era boundaries make it depend on research speed: a fast E0 jumps the camp cap before the band reaches it. WS-H should:
- Raise caps project by project, as the catalog already does (SOC-01, FOR-02 …), rather than per era.
- Add a seasonal pressure trigger (late-winter food security) so the first split doesn't hinge on crossing one headcount.

### F6 — Population is ~10× below the representation-scale budget in E2–E6
Modeled population at each era's end (tuned): 20 · 43 · 106 · 295 · 848 · 2,553 · 9,028 · 40,315 · 88,107 · 175,386. Under the v0.1 scale, the map would render only 50–100 figures from E2 to E5 and look empty mid-game. Even with faster demography, the "hundreds of figures" in E1–E2 (01 §1) and the "millions" (01 §8) are out of reach in a 3-hour match. A civilization peaks around 175,000 people.

**Recommendation: representation scale 1 · 1 · 1 · 1 · 2 · 5 · 25 · 100 · 250 · 500.** That keeps rendered figures at 295–511 at every era end from E3 on (E0–E2 stay below 110 at 1:1), and keeps Pillar P2 ("every figure is someone") literal through the Bronze Age. Revise the "millions" wording in 01 §8 to "hundreds of thousands".

Also for **WS-F/WS-G:** at minute 30 a civilization has ~100 people in ~3 settlements. On a 16,000-tile map that territory is sparse, so early claim radii or the map size per player may need to scale.

### F7 — Pace by research cost can't work: scale the calendar instead
Population runs on the calendar, not on research. Halving costs halves only the research half of the equation, so Quick at costs ×0.5 runs **148 min with the tuned numbers (283 with v0.1)** against a target of 90. Reaching 90 min through costs alone would need ×0.02.

**Recommendation: research costs are ×1 at every pace, and pace sets the season length.** Quick is 7.5 s per season (one year = 30 s), Standard 15 s, Epic 26 s. Results: **86 / 171.5 / 297 min.** The match plays out identically, just faster or slower.

For implementation (WS-L), keep per-season economics fixed and change ticks per season. This turns decision D5 ("1 year = 60 s") into "1 year = 60 s at Standard".

### F8 — Forced backfills are large and uneven
Keystone closures reach back into earlier eras. KEY-3 needs **25 projects** from E0–E1 that KEY-2 didn't (18 of them from E1, including SOC-03/04, GOV-01, NUM-01/02, TRD-02 and CER-01/02). KEY-10 needs **52** from E0–E8, including TER-01…TER-08.

Two effects, with 30% assumed to be pre-taken as earlier free picks:
- **Cost:** backfills are 20% of E2's requirement and 4–7% of each later era's.
- **Completion count:** E2 becomes ~39 completions (one every 18 s) and E9 ~55.

The total time is insensitive: an overlap anywhere from 0 to 0.6 changes the arc by under 1%. Cadence and Chronicle noise are the real problems.

**Recommendations:**
- **Council:** make 02 §15's KeystoneProgress boost apply to the next keystone's *whole closure*, not just its four named projects.
- **Queueing:** auto-queue backfills, since they're cheap at EraLag.
- **Moments:** fire a Moment only for in-era completions.
- **Chronicle:** batch backfills into one Chronicle line per season.
- **WS-B:** check whether KEY-3 really needs 18 E1 projects beyond KEY-2's closure.

### F9 — Breadth N holds; one adjustment
In-era completions are N plus 1.6–1.7 from the keystone phase. That lands inside the 02 §8.3 band in every era, and uses 27–43% of each era's catalog (02 says 30–40%; E1 is lowest at 27% because it has 59 projects).

The catalog parse finds slightly shorter critical paths than 04 §2 from KEY-4 onward:

| Keystone | KEY-4 | KEY-5 | KEY-6 | KEY-7 | KEY-8 | KEY-9 | KEY-10 |
|---|---|---|---|---|---|---|---|
| In-era path, 04 §2 → parsed | 18 → 16 | 17 → 16 | 12 | 10 | 10 | 14 | 13 |
| Closure, 04 §2 → parsed | 108 → 104 | 153 → 148 | 174 → 170 | 210 → 207 | 227 → 223 | 278 → 274 | 338 → 334 |

The consistent offset of 4 suggests a different pick on one any-of; WS-M should settle the tie-break rule.

**Recommendation: N stays as written except E3, 22 → 21.** With the parsed path of 16 that leaves 5 free picks (the rule says path + 3–4; the completion band needs about 21), and E3 completions are 22.6, inside 22–25. E0 keeps only 2 free picks (11 of its 13 are forced). That's acceptable for the onboarding era, but it's below the rule. Changing N everywhere back to v0.1 moves the arc by under 0.1 min.

### F10 — Decision cadence: fast early, on target late
| | E0 | E1 | E2 | E3 | E4 | E5 | E6 | E7 | E8 | E9 |
|---|---|---|---|---|---|---|---|---|---|---|
| Seconds per in-era completion | 31 | 38 | 33 | 34 | 40 | 57 | 74 | 85 | 82 | 95 |
| One project alone in Primary (min) | 0.8 | 0.9 | 0.7 | 0.8 | 1.0 | 1.4 | 1.8 | 2.0 | 2.0 | 2.3 |

The 01 §5 target ("a meaningful decision every 1–3 minutes") is met from E5 on. E0–E4 are faster: good for onboarding, but it puts weight on queues and the Council. A completion every ~35 s with all three slots running means the focus bar must stay legible while it churns. That's a WS-I wireframe requirement.

### F11 — Sensitivity and snowballing
Standard pace, tuned constants:

| Variation | Arc (min) | vs. 171.5 |
|---|---:|---:|
| Grower / Builder / Wanderer / Warlord / Sage | 154 / 178 / 165 / 175 / 173 | −10% … +4% |
| Fertility ×0.85 / ×1.15 | 191 / 158 | +11% / −8% |
| Follower civilization (DiffusionMult 0.8) | 162 | −6% |
| Cohesion factor 0.8 | 171.5 | 0% (caps bind on food first) |
| v0.1 Know-how rule, rate ×8 | 176 | +3% (but the share fades, F4) |

Demography is the single biggest driver of pace. **Lock WS-H (fertility, mortality, caps) before WS-B puts numbers on the 509 effects**, or every effect pass will need a re-fit.

Every archetype stays within ±25% of every era target, but some leave the exact windows. The Grower runs 8–15% fast from E3 on (E9: 25.5 min against 28–32). The Wanderer's E9 is 24.2 min, because its larger territory compounds into more settlements. The Sage's E1 is 8.8 min.

### F12 — Where Insight comes from
Commons dominates throughout. Roles are 0% in E0, 27–29% in E1–E2 (the per-camp Elder and Shaman), 13–21% in E3–E5 and 22–26% from E6 on. The specialist shares are assumptions.

One structural note for WS-B: **LRN-12 R&D Laboratories (the Researcher role, 120 Insight/yr tuned) is on no keystone closure.** 02 §8.1 frames researchers as the late-game engine. The model treats it as a likely pick (it's a strong choice), but the Sage and Builder paths shouldn't be able to skip it by accident. Its chain (LRN-12 → LRN-13 → LRN-14) sits entirely off the closures. Consider making LRN-13 a prerequisite of one KEY-10 named project.

## 5. Recommended constants (change list)

| # | Doc § | Constant | v0.1 | Recommended | Finding |
|---|---|---|---|---|---|
| 1 | 02 §8.1, 05 §20 | Commons coefficient (Insight/yr) | 0.6 | **4.8** | F1 |
| 2 | 02 §8.1 | Role outputs (Insight/yr per person) | Elder 1, Shaman 1.5, Priest 2, Scribe 3, Scholar 6, Researcher 15 | **Elder 8, Shaman 12, Priest 16, Scribe 24, Scholar 48, Researcher 120** | F1 |
| 3 | 03b BEL-10 | Monastery | +2 each | **+16 each** | F1 |
| 4 | 02 §8.1 | Commons exponent; elder weight | 0.85; ×2 | unchanged | F3 |
| 5 | 02 §8.3 | EraBase E0…E9 | 20, 50, 125, 300, 750, 1,900, 4,700, 12,000, 30,000, 75,000 | **20, 45, 70, 180, 600, 3,400, 18,000, 160,000, 1,000,000, 4,800,000** | F2 |
| 6 | 02 §13, 04 §2 | Breadth N | 13, 14, 20, 22, 21, 16, 14, 14, 18, 17 | **13, 14, 20, 21, 21, 16, 14, 14, 18, 17** | F9 |
| 7 | 02 §9 | Know-how generation | 0.05 per figure-equivalent per season | **35% of the practising figure's Commons Insight**, split by practice | F4 |
| 8 | 01 §6 | Pace | costs ×0.5 / ×1 / ×1.6 | **costs ×1; season 7.5 s / 15 s / 26 s** | F7 |
| 9 | 05 §9.2, §20 | Base fertility (per fertile woman per season) | 0.06 | **0.13** | F5 |
| 10 | 05 §9.4 | Fission trigger | pop > GroupCap × CF, or pressure | **≥ 85% of MIN(food, housing, group × CF)**; daughter 40%, ≥ 6 people | F5 |
| 11 | 01 §8, 05 §5.2 | Representation scale E0…E9 | 1, 1, 2, 5, 10, 25, 50, 100, 250, 500 | **1, 1, 1, 1, 2, 5, 25, 100, 250, 500** | F6 |
| 12 | 02 §15 | Council KeystoneProgress boost | 4 named projects | **whole closure of the next keystone** | F8 |

**Resulting per-era costs** (Standard, Balanced; average in-era project / keystone): E0 22 / 50 · E1 49 / 112 · E2 82 / 175 · E3 214 / 450 · E4 728 / 1,500 · E5 3.9k / 8.5k · E6 21k / 45k · E7 152k / 360k · E8 1.0M / 2.25M · E9 5.0M / 10.8M (E7+ include Scientific Method's −10%).

## 6. Tuned run

Standard pace, Balanced archetype:

| Era | Start | Minutes | Target | In-era done | Backfills | Pop at end | Settl. | Figures | Insight/min | Know-how | Roles |
|---|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|
| E0 | 0.0 | 7.5 | 7–9 | 14.7 | 0 | 20 | 2 | 20 | 41 | 20% | 0% |
| E1 | 7.5 | 10.0 | 9–11 | 15.7 | 5.6 | 43 | 2 | 43 | 84 | 15% | 27% |
| E2 | 17.5 | 11.8 | 11–13 | 21.6 | 17.5 | 106 | 3 | 106 | 181 | 14% | 29% |
| E3 | 29.2 | 12.8 | 12–14 | 22.6 | 11.2 | 295 | 3 | 295 | 382 | 15% | 21% |
| E4 | 42.0 | 15.0 | 14–16 | 22.5 | 19.6 | 848 | 4 | 424 | 1,096 | 16% | 13% |
| E5 | 57.0 | 16.8 | 16–18 | 17.7 | 16.8 | 2,553 | 7 | 511 | 4,074 | 15% | 16% |
| E6 | 73.8 | 19.2 | 18–20 | 15.6 | 20.3 | 9,028 | 12 | 361 | 18,112 | 14% | 22% |
| E7 | 93.0 | 22.2 | 22–24 | 15.8 | 18.9 | 40,315 | 21 | 403 | 112,253 | 13% | 26% |
| E8 | 115.2 | 26.8 | 26–28 | 19.6 | 14.7 | 88,107 | 15 | 352 | 764,218 | 13% | 25% |
| E9 | 142.0 | 29.5 | 28–32 | 18.6 | 36.4 | 175,386 | 15 | 351 | 3.27M | 13% | 25% |
| **Arc** | | **171.5** | ≈ 180 | | | | | | | | |

Paces: **Quick 85.75 min, Epic 297 min.** Every archetype at every pace is in the `Sweeps` sheet. Settlements drop from 21 to 15 in E8 because claim radius 9 merges neighbors.

## 7. What this does not settle (follow-ups)

1. **WS-H (population and health):** replace the ASSUMED mortality, fertility multipliers, food and housing caps and specialist shares, then re-run `tools/fit.py`. Address the fission knife-edge (F5).
2. **WS-A part 2, the 4-civ territory model** (06 §3): claims, Dominion share and first-contact timing with ~100-person civilizations at minute 30 (F6). This model has no rivals.
3. **WS-B:** the KEY-3 and KEY-10 closure sizes (F8), LRN-12's place in the closure (F12), late multiplier softening (F2) and the per-lineage Know-how discount (F4).
4. **WS-M:** reconcile the critical-path tie-break with 04 §2 (F9). `tools/catalog_stats.py` is a starting point for the validator.
5. **WS-I:** focus-bar legibility at one completion every ~30 s in E0–E4, and backfill batching (F8, F10).
6. **Apply the change list (§5) to 01, 02, 03b, 05 and 06**, and add this document to the README index.

**Regenerating:** `python docs/MVP/balance/tools/build_xlsx.py` rebuilds the workbook from [tools/params.py](balance/tools/params.py) and the catalog docs (needs `openpyxl`). Add `--verify 720 <path>.xlsx` to re-check the formulas against the Python model (needs `formulas`). `tools/fit.py` re-fits the Insight scale and EraBase. `tools/sensitivity.py exponent|final` reproduces F3 and F11.
