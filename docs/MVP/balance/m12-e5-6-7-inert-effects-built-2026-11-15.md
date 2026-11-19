# WP-E5-6/7 inert effects built (2026-11-15)

**Status:** implemented in a separate dev copy (`civsim-e5-dev`, derived from source aggregate `7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6`). All values are draft hypotheses; no row is promoted; everything is inert outside a Fief match. Closes the "typed but unread" findings of [the per-project audit](m12-e5-6-7-per-project-audit-2026-11-15.md) except those listed under *Not built*.

| Project | Typed clause | Implementation (files: `LivingEconomyModel.Learning.cs`, `ResearchSystem.cs`, `PoliticsSystem.Trade.cs`, `LivingEconomyModel.Health.cs`) |
|---|---|---|
| ART-09 | G-MANUSCRIPT, `copy_manuscript`, `monastery_insight ×1.1` | Recipe is available only at a monastery with all four Monk posts filled; consumes paper, pigment and gold (plus the draft 5% Fief loss allowance). Settlements want two manuscripts per staffed monastery. Each manuscript is one luxury item for Cohesion. Monk Insight is multiplied by `monastery_insight` (Fief only; staffed because monk heads are staffed posts). |
| CER-10 | G-SPECTACLES, `grind_spectacles`, `elder_insight ×1.25` for holders | Recipe from blown glass and wood; want of one pair per elder (age 45+), at most four a season; wear 1/80 of stock a season. The generic `elder_insight` modifier no longer carries CER-10 in Fief: research stores it as `SpectacleElderMult` and applies it only to the share of elders holding a pair (`InsightInputs.SpectacleHolders`, capped by elders). Without spectacles the bonus is zero. Outside Fief unchanged. |
| NUM-08, TRD-08, GOV-10 | `trade_volume`, `contract_trade` | One caravan may carry up to the buyer's stated need × (`trade_volume` × `contract_trade`), still capped by the seller's surplus and the vessel limit (`CaravanLoad`). Credit moves existing goods earlier; it creates none. |
| NUM-08 | `admin_capacity ×1.15` | Admin span (economy and research) is the summed span × multiplier, rounded half up, Fief only. |
| MED-07 | `elder_hazard ×0.95` | Infected figures aged 45+ in a settlement with a staffed hospital take the extra factor on top of the 0.8 hospital factor. |

Persistence: the per-settlement Fief goods array grows from 2 to 4 entries (manuscripts, spectacles after the bows). Fief snapshot version is **45**; versions 40-44 still load, the older array is zero-padded, and the two new entries enter the hash only when non-zero, so a match without them keeps its earlier checksum. Iron and Bronze snapshots are unchanged.

Tests: `FiefLearningEffectsTests` 12/12 (recipe gating and conservation, Iron isolation, luxury count, in-play copying, monk Insight, spectacle production/wear, holder cap, holder-gated Insight, trade factors, caravan load with surplus cap, admin span, elder hazard, snapshot round trip). Full Release suite on the dev copy: 1,431 of 1,435 passed. The four failures: two snapshot-version assertions (44 to 45, fixed and rerun, pass), `IronChainTests.Iron_goods_are_hashed_and_saved_only_in_the_Iron_chapter_and_round_trip` (fails identically on the unmodified `7decf87…` snapshot, so it predates this work), and `ViewStoreTests` frame-time (3.6 ms vs a sub-millisecond bound, measured while the host ran at load 130-200).

## Not built
- TRD-09 `trade_profit`: still a separate book-margin ledger with no effect on price, payment or output.
- SOC-11 `apprenticeship_speed`: still a training-exposure counter with no downstream effect; `craft_quality` durability is not modeled.
- GOV-11: the charter still has no staff or structure dependence (27 §5.5); this needs a design decision.
- System-B learning items without an owner: CER-09, MET-11, CHM-06, MIN-08, FIR-10.

## Evidence invalidated
Every Fief cohort run on `7decf87…` (F-T1, F-T2, F-T3, F-T6/7, A1-A8) predates these effects. Natural play now completes NUM-08/TRD-08/GOV-10/ART-09/CER-10 with real consequences, so those cohorts must be rerun on the final source before acceptance. No balance fit of the new draft quantities (two manuscripts per monastery, four spectacles a season, 80-season wear, one luxury item per manuscript) has been attempted.
