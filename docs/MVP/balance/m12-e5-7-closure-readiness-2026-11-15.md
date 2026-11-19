# WP-E5-7 closure readiness (2026-11-15)

## Status

The GOV-10/11, TRD-08/09, DIP-08, and SOC-11 slices are implemented as Fief-only draft mechanics with focused regression coverage. The explicit design rules below are captured in the project decisions and focused reports; several numeric conventions and proxy measures are still draft fitting hypotheses. No broad natural-match balance cohort or WP-E5-11 acceptance result closes WP-E5-7, and the player-facing GOV-10 event presentation remains unreviewed. No E5 value is promoted by this report.

## Current candidate identity

Read-only verification during this audit found HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`, with canonical `hash_e5_workspace.py` aggregate `7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6`. The numeric scratch pack is `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`; Release Harness SHA-256 is `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f`; adjacent Release Sim SHA-256 is `a8cb7666042cf6651a6b9558b3d6db7b1f00566b5cc30041077e67b86540ad50`. These identify the current candidate only; older tests/runs below should not be treated as if they were performed with these fingerprints. No build or test was run for this report.

## Accepted rules and implemented slices

### GOV-10 and GOV-11

Decision D16 (2026-11-14) confirms a minimal runtime behavior: GOV-11 gives a town a derived charter after it reaches City tier and applies +10% city growth; GOV-10 runs at most one seeded crime/dispute check per live settlement-year using existing disorder, and GOV-10 halves that chance. An event removes one existing cohesion point (0.006). The implementation is Fief-only, derives charter status instead of saving another flag, and uses a keyed stream that does not consume shared random state. The stated 1% base chance, +0.01 percentage point per disorder, and 10% cap are explicitly **draft fit hypotheses**, not approved acceptance values. Evidence/code: `m12-wp10-law-money.md`, `LivingEconomyModel.Law.cs`, `PoliticsSystem.FiefLaw.cs`; focused coverage: `FiefLawMoneyTests`.

### TRD-08

Decision D15 (2026-11-14) confirms an origination-valued, non-interest credit claim for unpaid seller-requested value. Later partial repayment uses non-food goods already held at locked origination rates, after forecast food need plus a one-season reserve. Claims survive route termination and can be collected locally, are written off on debtor-settlement loss, and expire after eight seasons without payment. The Fief-only claim ledger is included in snapshot/hash state. Focused tests cover unpaid arrival, food protection, partial transfer, continuation, local collection after Compact termination, loss write-off, and expiry. Evidence/code: `m12-wp10-law-money.md`, `PoliticsSystem.FiefLaw.cs`, `PoliticsSystem.Trade.cs`; tests: `FiefLawMoneyTests`.

### TRD-09

Two Fief-only draft effects have focused coverage. `admin_waste ×0.9` reduces by 10% the actual work cost assigned to staffed Administrator/Governor duties; it releases that work to the ordinary pool without removing the staff or service. The separate `trade_profit ×1.2` metric records only the seller's realized margin at delivery, using the seller's `UnitValue` per cargo unit frozen at dispatch as a draft book basis. It does not change offer acceptance, agreed terms, shipped cargo, payment, or resource stocks. Focused tests check research ownership, frozen basis, unchanged trade transactions, persistence, and saturation. This accounting basis is documented as an autonomous draft convention, not a measured acquisition cost or a separately fitted/accepted numeric rule. Evidence: `m12-wp10-law-money.md`; code: `LivingEconomyModel.Civic.cs`, `PoliticsSystem.Trade.cs`; tests: `FiefDoubleEntryTests`, `FiefTradeProfitTests`.

### DIP-08

The implemented Fief slice provides keyed peaceful inheritance of a neutral Free Folk settlement for an allied civilization with DIP-08 and an own/allied settlement within eight hexes. A hit remains pending for two seasons and rechecks eligibility before transfer. On leader death, succession selects the oldest living adult (age 14+), then lowest FigureId; DIP-08 preserves active alliances across that succession, while no-tech alliances lapse to the 40 relation floor. The pending claims and leader identity persist in Fief snapshot/hash state. This avoids synthetic spouse/kinship data. The **2% seasonal chance, two-season delay, eight-hex radius and neutral seniority convention remain draft hypotheses**, and historical family inheritance is outside scope. Evidence/code: `m12-wp10-dip08-inheritance.md`, `PoliticsSystem.DynasticInheritance.cs`, `PoliticsSystem.Dynasty.cs`; tests: `FiefDynasticInheritanceTests`, `FiefLeaderSuccessionTests`.

### SOC-11

The typed effect's +20% craft quality and ×1.5 apprenticeship speed are represented as output-independent Fief readouts: a 1.2 quality index and 1.5 craft-role training-work exposure. The slice models a uniform 5% input-loss allowance for non-kit recipes, reduced by SOC-11 to 5%/1.2 (4.167%); it leaves output, labor rate, tool durability, and T12 life unchanged. Waste, quality, and exposure are hashed and snapshotted, with migration defaults; Iron/Bronze remain isolated. The 5% baseline, training-exposure proxy, and lack of per-recipe variation are **unvalidated fitting hypotheses**. Evidence/code: `m12-wp10-soc11-guild-craft.md`, `LivingEconomyModel.Fief.cs`, `LivingEconomyModel.Metal.cs`; focused tests: `FiefGuildCraftTests` (reported 2/2) and related persistence coverage. The project overview records a full Release suite at 1,423/1,423, but that is prior evidence, not a test run in this audit.

## Mechanics coverage versus acceptance

The reports document focused behavior, deterministic keyed draws, mode isolation, and Fief snapshot/hash persistence. They do not demonstrate how often the events, claims, margins, inheritances, or guild effects occur in ordinary matches, nor whether their resource/consequence tradeoffs are balanced. The governing overview explicitly says there are no WP-E5-7 acceptance bands and that numeric values/conventions remain draft. GOV-10's event changes cohesion through the economy API, but its event-feed presentation has not received player-facing review.

## Minimum remaining current-candidate gates

1. Freeze the integrated source, scratch pack, Release Harness, and adjacent Sim hashes before any balance run. Bind new results to those fingerprints; do not transfer pass status from older snapshots.
2. Define and freeze calibration and untouched holdout manifests for the WP-E5-7 measures before inspecting outcomes. Fit GOV-10 incident frequency/consequences; characterize TRD-08 claims/repayment and TRD-09 administrative labor/book-margin effects; measure DIP-08 inheritance frequency and consequences; and assess SOC-11 resource use, waste, quality, and training exposure on an explicit recipe selection. Preserve censored and no-event outcomes.
3. Carry the autonomous provisional working criteria from [the WP-E5-7 measurement protocol audit](m12-e5-7-measurement-protocol-audit-2026-11-15.md) into WP-E5-11 before opening any holdout. They are not separately user-approved; preserve that label and do not tune them after seeing holdout outcomes. Low exposure or inadequate precision remains inconclusive.
4. Review GOV-10 player-facing event presentation after the integrated view path is available.
5. Keep all related E5 catalog rows draft and avoid content-golden refresh/promotion until the required all-50-row WP-E5-11 promotion and review gates are complete.

The mechanics reports are evidence of implementation and regression coverage; they are not whole-match acceptance. No source, test, or simulation output was changed or produced for this report.
