# WP-E5-6 / WP-E5-7 per-project audit against the earlier-era requirements (2026-11-15)

**Source:** isolated copy of `/Users/ejuchheim/Projects/CivSim/civsim-e5` (branch `codex/e5-fief`, HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`, dirty snapshot, content/`src`/`game` aggregate `7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6`, 1,040 files; per-file record in [the provenance JSON](m12-e5-evidence-source-provenance-2026-11-15.json)). Read-only code and content audit; no source changed, no row promoted, no threshold invented.

> **Supersession:** The project statuses and “typed but inert” inventory below describe the 2026-11-15 `7decf87…` snapshot. Do not use them as a current-source inventory for ART-09, CER-10, NUM-08, GOV-10, MED-07 or the explicitly noted GOV-11 behavior. The current-source review at the end of this file supersedes those findings for those effects and separates implemented mechanisms from fitting and acceptance evidence.

## Which "larger per-project requirements" apply

The earlier eras do not state one checklist; they impose these per-project conditions, which this audit applies to every WP-E5-6/7 project:

1. **Typed, complete row** (25 B4-02 / 27 B5-02): typed effects, cost, requirements, practice, flavor, Moment. All 50 E5 rows pass V22; this audit does not repeat it.
2. **Typed is not consumed.** A typed effect must be *read by the simulation* (26 / 25 §15: "core ... cannot be substituted with prose-only unlocks"; 27 §5 "physical ... nothing is a bare multiplier"). This audit checks each typed `mod`/`good`/`rule` clause against the source.
3. **No unstaffed benefit** (25 B4-09, 27 §5.4/§5.5, 27 B5-09) and **conservation** (25 B4-06/07, 27 B5-06/07).
4. **No catalog promise silently dropped** (25 §15; 27 D7: "no catalog promise is dropped silently"). A reduction must be an explicit, user-accepted scope change.
5. **Per-mechanism test and measured evidence** (17 §9 "no feature without a test"; 25 B4 rows; 27 §7 F-xx, §10 B5-xx).

Only the gates that already exist in 27 are used. Where a project has no F-xx/B5-xx evidence definition, the table says "no defined gate"; this audit proposes none.

## Method

For each of the 21 projects of systems D (faith/learning), E (law/money) and the two system-B learning items (CER-10, MED is system D), every typed `effects` clause in `content/catalog/*.yaml` was matched against (a) hard-coded project-id hooks in `src/CivSim.Sim` (`Done(civ,"ID")`, `HasProject`, `Catalog.IndexOf`), (b) generic consumers of stat names (`catalog.StatIndex("…")`, `Modifier(civ,"…")`), and (c) tests. A stat name that appears in no source file is **inert**.

## Result

Legend: **I** implemented and tested; **G** generic research consumer only (works but no E5-specific test); **P** partial; **X** not consumed by the simulation.

### WP-E5-6 — faith and learning

| Project | Typed clauses | Status | Evidence / gap |
|---|---|---|---|
| BEL-10 monasticism | build monastery; role monk; rule (4 monks → 16 Insight, Know-how copy, 6 wilderness plots) | **I** | `FiefFaithLearningTests`, `FiefWildernessFarmingTests`, `FiefKnowHowRetentionTests` (6/6), `FiefInstitutionOpportunityMeasurementTests`. Open: wilderness plot yield 0.25, 3-monastery cap and 4-Insight role value are unfit; food/work opportunity cost and KEY-6 pacing effect unmeasured; capture/collapse Know-how loss only mechanism-tested. |
| BEL-11 pilgrimage | unlock action; `route_trade ×1.15`; rule (+5% claim on traveled route, no fog, journey food) | **I** | `FiefPilgrimageTests` 4/4. Open: party size/cadence draft; participation in natural play unmeasured. |
| ART-09 illuminated manuscripts | good G-MANUSCRIPT; `monastery_insight ×1.1` (staffed only); rule recipe `copy_manuscript` at a monastery | **X** | Registry has the good and the recipe, but **no source file mentions `G-MANUSCRIPT`, `copy_manuscript` or `monastery_insight`**; the Fief goods slots cover paper/book/cast iron/powder/cannon only. The project completes and does nothing. |
| LRN-07 cathedral schools | `scholar_cap ×1.5`; rule (real adult in a real seat) | **I** | `FiefCathedralSchoolTests`, `FiefUniversityTests`; nearest-integer half-up rule documented. |
| LRN-08 universities | build university; `scholar_insight ×1.5` (university scholars); `adopt ×1.25`; KEY-6 prerequisite | **I / G** | `FiefUniversityTests`. The `adopt ×1.25` is consumed by the generic research path; no E5-specific test shows its effect. |
| BLD-12 Gothic Cathedral | wonder; `cohesion +3 all`; `pilgrimage_trade ×1.1` at the cathedral | **I** | `FiefCathedralTests` 4/4. Open: natural-play completion unmeasured (the E4 lesson was that no Wonder finished); work/material values draft. |
| STN-13 master masons | role master_mason; `large_stone_works_speed ×1.5` | **I** | covered by the cathedral tests. |
| COM-09 paper | good G-PAPER; `writing_role_output ×1.3` | **I** | `FiefCommunicationTests`. |
| COM-10 movable type | build print_shop; good G-BOOK; `commons_insight ×1.15`; `diffusion_discount ×1.5` capped at 0.5 floor | **I / G** | `FiefCommunicationTests` (floor, staffing, recipe conservation). `commons_insight` is read generically (`civ.ModMult[_statCommons]`); no test isolates COM-10's ×1.15. |
| NUM-08 zero & positional numerals | `trade_volume ×1.15`; `admin_capacity ×1.15` | **X** | Neither stat name appears anywhere in `src/CivSim.Sim`. Both effects are inert. |
| NUM-09 algebra | `scholar_insight ×1.15` (staffed scholars) | **G** | Consumed by `ResearchSystem` (`civ.ModMult[_statScholar]` applied to scholar role output); no NUM-09 test; "staffed scholars only" holds only because the role itself requires a seat. |
| INQ-06 optics & magnetism | two unlocks (CER-10, WAY-08) | **I (no mechanism needed)** | Pure unlock chain. |
| MED-07 hospitals | role attendant; build hospital; hazard epidemic ×0.8 local; `elder_hazard ×0.95` for a staffed hospital; rule (physician + 2 attendants, empty hospital does nothing) | **P** | The 0.8 hazard is hard-coded (`InfectedHazard`, `HospitalStaffed`) and tested (`FiefHospitalTests`, `FiefHospitalMortalityMeasurementTests`: 16 matched pairs, 18.9% relative reduction). The typed `hazard` clause is *not* read from the catalog (the research `HazardMult` is never used by the economy), and **`elder_hazard ×0.95` is not consumed anywhere**. |
| MED-08 quarantine | `outbreak_roll ×0.75` (imports); `epidemic_spread ×0.5` | **I** | Implemented by project id (`FiefQuarantineTests`); natural import exposure is zero (0 attempts in both natural cohorts), so the attenuation is fixture-only evidence. |
| CER-10 lens grinding (system B, learning) | good G-SPECTACLES; `elder_insight ×1.25` "only for elders who hold spectacles" | **X, and unsafe** | No source file mentions `G-SPECTACLES`/`grind_spectacles`. Worse, `elder_insight` *is* a live stat: `RefreshInsight` applies `civ.ModMult[_statElder]` to **every elder** (`ResearchSystem.cs:744`). Once CER-10 is numeric, all elders get +25% Elder Insight with no spectacle needed, which breaks the stated rule. CER-10 is assigned to neither WP-E5-5 nor WP-E5-6 in the reports. |

### WP-E5-7 — law and money

| Project | Typed clauses | Status | Evidence / gap |
|---|---|---|---|
| GOV-10 canon & common law | `dispute_crime_events ×0.5`; `contract_trade ×1.05` | **P** | Keyed once-per-settlement-year incident, halved by GOV-10, cohesion −0.006 (`FiefLawMoneyTests`). **`contract_trade ×1.05` ("written contracts") is not consumed** (stat name absent from source). Numeric hypotheses (1% + 0.01 pt per disorder, cap 10%) are unfit; event-feed presentation not checked. |
| GOV-11 charters & parliaments | `admin span +1`; `city_growth ×1.1` (chartered towns) | **P** | Span consumed by research (`civ.AdminSpan`); growth multiplier consumed in births (`LivingEconomyModel.cs:4093/4264`) and tested. The general §5.5 no-unstaffed-benefit rule has the accepted D16 exception: GOV-11 + City tier grants the charter and +10% city growth automatically, without a staffed-office or structure requirement. |
| SOC-11 guilds | `craft_quality ×1.2` (durability and waste of non-kit goods); `apprenticeship_speed ×1.5` | **P** | `FiefGuildCraftTests` 2/2. Waste (5% base ÷ 1.2) is real. "Durability" is not modeled, and training exposure ×1.5 is a **counter that changes nothing downstream** (no training-time effect), so apprentice speed is observable but has no mechanism. |
| TRD-08 banking | `trade_volume ×1.5`; `wonder_build_labor ×0.8696`; rule (bills settle between route-linked markets, mint nothing) | **P** | The D15 origination-valued claim ledger is implemented and tested (`FiefLawMoneyTests`, `FiefShipCargoTests`). `wonder_build_labor` is read generically (`Civic.cs:763`) with no TRD-08 test. **`trade_volume ×1.5` is not consumed** (stat name absent). |
| TRD-09 double-entry | `trade_profit ×1.2`; `admin_waste ×0.9`; KEY-6 prerequisite | **P** | `admin_waste` → 10% off Administrator/Governor work (tested). `trade_profit` → a **separate seller-book-margin ledger that does not enter prices, payment or resources** (the report states this is an accounting convention); it has no economic effect. |
| DIP-08 dynastic marriages | alliances survive leader death; 2%/season peaceful Free Folk inheritance within 8 tiles | **I** | `FiefLeaderSuccessionTests`, `FiefDynasticInheritanceTests`. Seniority election is a neutral convention; 2%/two-season values are draft. |

## Summary of gaps against the per-project requirements

**Typed but inert (the project completes and the effect never happens):**
- ART-09 — all three clauses (good, `monastery_insight`, `copy_manuscript`).
- NUM-08 — `trade_volume`, `admin_capacity`.
- TRD-08 — `trade_volume ×1.5` (the credit-ledger half is implemented).
- GOV-10 — `contract_trade ×1.05`.
- MED-07 — `elder_hazard ×0.95`.
- CER-10 — good, recipe and the holder gate (and a live over-grant, below).

**A defect rather than a gap:** with CER-10 numeric, `elder_insight ×1.25` applies to all elders (no spectacles needed). Numeric promotion of CER-10 would grant an ungated bonus; the draft pack hides it.

**Implemented as accounting only:** TRD-09 `trade_profit`, SOC-11 training speed. They satisfy "observable" but not "physical" (27 §5), and neither feeds a decision or an output.

**Accepted exception to the general no-unstaffed-benefit rule:** GOV-11 is governed by confirmed D16: City tier + GOV-11 automatically grants the charter and +10% city growth, with no additional staffed-office or structure requirement. The current law code follows this specific rule; §5.5 was reconciled on 2026-11-16.

**Generic-consumer clauses with no E5-specific test:** LRN-08 `adopt`, COM-10 `commons_insight`, NUM-09 `scholar_insight`, TRD-08 `wonder_build_labor`.

**Evidence that 27 requires and does not yet exist** (no threshold is proposed here):
- B5-09: F-T6 is diagnostic only (trace coverage confirmed, no whole-match acceptance); "knowledge-copy and capture cases" only at mechanism level; no no-unstaffed-benefit audit of the whole project set.
- B5-07: MED-08 attenuation has zero natural exposure.
- B5-12: comprehension protocol not run (human gate).
- WP-E5-6 report "Next checks" 1–5 (monastery/university/COM-09/10 opportunity cost and pacing, hospital band, pilgrimage participation, cathedral staffing/completion) have no defined acceptance gate in 27 §7; each needs one defined by the user or by an explicit autonomous-closure rule (as D20/D21 did) before it can be closed.
- WP-E5-7 has **no F-xx row at all** (law, credit, guilds, bookkeeping, dynasty); 27 §7 defines none and doc 27 gives no numeric target to test GOV/TRD/SOC/DIP effects against.
- System-B and F-T learning items outside any WP report: CER-10 (above), CER-09 porcelain, MET-11 crucible steel, CHM-06 distillation, MIN-08 survey, FIR-10 chimneys, ART-09 (above). They need an owner.

## Cohort-based observations

(Filled in from the current-source cohort outputs where available; see the closeout report.)

## Superseding current-source review (2026-11-16)

This current-source review supersedes the 2026-11-15 inventory. The worktree is dirty overall. Scoped SOC-11 implementation and consumer-test changes are present in this worktree; the focused command `dotnet test src/CivSim.Tests/CivSim.Tests.csproj --no-restore --filter 'FullyQualifiedName~FiefGuildCraftTests|FullyQualifiedName~FiefGenericConsumerTests'` passed 9/9 on 2026-11-16. No cohort was run. This fixture result establishes mechanism wiring only; no natural exposure/fit or acceptance is claimed. All numeric rows remain draft.

### Findings that changed since the 2026-11-15 audit

| Effect | Current consumer and focused coverage | What the evidence establishes / does not establish |
|---|---|---|
| ART-09 manuscript good, recipe and monk Insight | `LivingEconomyModel.Learning.cs` queues manuscripts only for a staffed monastery, uses the registered `copy_manuscript` recipe, counts stock as luxury Cohesion items; `ResearchSystem.RefreshInsight` applies `monastery_insight` to monk role output in Fief. `FiefLearningEffectsTests` has recipe gating/input-use, play/cap, luxury, Fief-only monk Insight and persistence fixtures (12 `[Fact]` methods in the class cover ART-09 and other newly wired effects). | These formerly inert clauses now have mechanism tests. The per-season/stock caps and draft recipe quantities have no natural-play fit here; no effect-specific whole-match acceptance gate is closed. |
| CER-10 spectacles and holder gate | The economy produces/wears spectacles and reports holders; `ResearchSystem.RefreshInsight` applies the lift only to the elder share holding spectacles. `FiefLearningEffectsTests` covers production/wear, holder bounds, zero/partial/full holder Insight and non-Fief isolation. | The 2025 audit's all-elders over-grant finding is fixed in code. Fixture coverage is not natural acquisition/exposure or a fit of production/service-life values. |
| NUM-08 `trade_volume` / `admin_capacity`, TRD-08 `trade_volume` / `wonder_build_labor`, GOV-10 `contract_trade` | `FiefTradeVolumeFactor` multiplies the three researched modifiers; `PoliticsSystem.Trade.CaravanLoad` applies it to buyer need while retaining the seller-surplus cap. Fief-only `admin_capacity` widens AdminSpan in both economy and research. `FiefLearningEffectsTests` covers factor composition, caravan load/surplus, admin span and Iron isolation; `FiefGenericConsumerTests` now isolates TRD-08 Wonder labor and Iron chapter gating. `FiefLawMoneyTests` covers GOV-10's keyed incident reduction, while `FiefLawMoneyTests`/`FiefShipCargoTests` cover the D15 claim ledger. | The old “typed but unread” claims for these clauses are superseded. Tests establish local wiring and conservation bounds, not natural trade exposure, claim repayment fit, or a WP-E5-7 acceptance result. |
| MED-07 elder hazard | `LivingEconomyModel.Health.InfectedHazard` retains the staffed-hospital ×0.8 effect and applies `elder_hazard` to infected figures aged 45+ only while that hospital is staffed. `FiefLearningEffectsTests` asserts the adult ×0.8 and elder ×0.8×0.95 fixture factors. | The old claim that `elder_hazard ×0.95` is unconsumed is superseded. The ×0.8 catalog hazard remains implemented through the Fief hospital hook rather than a generic catalog hazard consumer. The existing forced-outbreak mortality measurement concerns the hospital mechanism and is not a natural-play fit of the elder factor. |
| MED-08 quarantine | `FiefQuarantineTests` exercises keyed import gating and between-settlement spread behavior. | Prior natural traces report zero valid route-import attempts, so they do not demonstrate import attenuation in natural play. Focused fixtures do not close the WP-E5-6/7 or B5 acceptance gates. |
| GOV-11 charter | `LivingEconomyModel.Law.cs` derives a City-tier charter from GOV-11 and applies +10% city growth in Fief; AdminSpan adds the typed span. `FiefLawMoneyTests` covers City-vs-Town, researched-vs-unresearched, Fief-vs-Iron and the extra admin span; `FiefWp7TraceTests` covers paired City-year tracing. | This follows confirmed D16's minimal rule and the accepted exception now stated explicitly in §5.5: there is no separate staff/structure dependency. No current-source calibration/holdout result establishes its population effect. |

### SOC-11 physical consumers and evidence boundary

- `craft_quality` now drives two physical Fief consumers using its catalog multiplier: eligible Fief recipe inputs lose less material (`FiefRecipeWasteRate`), and eligible age-wearing crafted goods lose stock more slowly (`FiefCraftServiceLifeMultiplier` / `AgeCraftedDurableGood`). Eligibility requires a positive numeric registry `ServiceSeasons`, a compiled recipe output, Fief mode, and a current age-wear path. It excludes raw/extracted stock, indefinite `durable` goods, and `G-TOOL*` working-season kits including T12; the separate kit durability model is unchanged. It does not raise recipe output or define a general product-quality score.
- `apprenticeship_speed` now reduces `FiefRecipeLabor` for recipes at or above the engine's `CraftOrder` boundary (craft-role labor); the catalog value is the divisor. Lower-order metal-chain recipes and kits remain outside this craft-role scope. `CraftRoleTraining` remains a diagnostic counter, not an independent qualification or training-completion mechanic.
- Focused `FiefGuildCraftTests` cover equal-assigned-work output and catalog-derived labor, unchanged non-craft/kit and Iron labor, `G-ARMS3` age wear versus T12 exclusion, deterministic checksum/snapshot state, existing input-loss/isolation, and trace neutrality. These prove fixture-level consumers only; no natural exposure, parameter fit, or WP-E5-11 acceptance is established. Draft quantities remain unchanged.

### Still open after this code review

- The old typed-inert list is superseded only for ART-09, CER-10, NUM-08, GOV-10 `contract_trade`, TRD-08 `trade_volume`, and MED-07 `elder_hazard`. TRD-09 `trade_profit` remains a seller-side accounting ledger with no price/payment/resource effect; `admin_waste` has a tested Fief work-cost effect. GOV-10 event presentation remains unreviewed.
- GOV-11's accepted D16 exception to the general §5.5 rule is now explicit in both §5.5 and this inventory; no additional staffing or structure dependency is implied.
- WP-E5-7 still has no F-xx row, and no WP-E5-7 natural calibration/holdout acceptance is established. WP-E5-11 remains the whole-age acceptance/promotion gate; all E5 rows stay draft.
- F-T6 trace coverage is diagnostic, not whole-match acceptance. MED-08 still lacks natural import exposure. F-T7's scale fit remains calibration-only and requires its separate holdout. Natural exposure and fit evidence for ART-09, CER-10, NUM-08/TRD-08 trade behavior, GOV-10/11 and SOC-11 remains absent or source-specific and non-acceptance.
- `FiefGenericConsumerTests` now gives each formerly uncovered generic consumer one focused Fief-vs-Iron test: LRN-08 adoption/diffusion, COM-10 commons Insight, NUM-09 scholar Insight, and TRD-08 Wonder-build labor. These tests establish chapter isolation and a fixture consumer, not natural exposure/fit or project acceptance. Other unowned system-B projects listed in the original audit remain outside this update.
