# WP-E5-6 / WP-E5-7 per-project audit against the earlier-era requirements (2026-11-15)

**Source:** isolated copy of `/Users/ejuchheim/Projects/CivSim/civsim-e5` (branch `codex/e5-fief`, HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`, dirty snapshot, content/`src`/`game` aggregate `7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6`, 1,040 files; per-file record in [the provenance JSON](m12-e5-evidence-source-provenance-2026-11-15.json)). Read-only code and content audit; no source changed, no row promoted, no threshold invented.

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
| GOV-11 charters & parliaments | `admin span +1`; `city_growth ×1.1` (chartered towns) | **P** | Span consumed by research (`civ.AdminSpan`); growth multiplier consumed in births (`LivingEconomyModel.cs:4093/4264`) and tested. 27 §5.5 says benefits stop *when the staff or structure is absent*: the charter is derived from GOV-11 + City tier only, with **no staff or structure dependence**, so that clause of the spec has no counterpart. |
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

**Spec clause without a counterpart:** GOV-11 "benefits stop when the staff or structure is absent" (27 §5.5).

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
