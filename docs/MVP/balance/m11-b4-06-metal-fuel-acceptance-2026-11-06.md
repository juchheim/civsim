# B4-06 metal and fuel closeout

**Status:** focused implementation and deterministic case verification pass. The new actual tool-worker screen is preregistered below and remains pending; I-T2 and the live I-T3 distribution are not closed until its results are recorded. I-T6's separately approved balance screen remains closed under its existing report.

## Scope and frozen screen

B4-06 requires I-T2/I-T3/I-T6, exact material accounting, and imported-kit, mine-loss, fuel-loss, captured-forge, mixed-wear, and upgrade cases ([25 §14](../25-iron-age.md#14-proposed-m11-acceptance-series-b4-xx)). The fixed I-T6 cutoffs remain unchanged: at least 12 positive planned-charcoal demand-seasons, shortfall rate at most 5%, and unmet charcoal at most 1% of planned volume. The I-T2 target remains at least 70% of tool workers equipped T11+ within 12 seasons of a sustainable route.

The harness now records actual settlement tool workers as adults with positive `toolWork` during the season, after that season's kit issue. The I-T2 diagnostic starts a settlement window at its first T11/T12 kit issue and ends after 12 complete seasons. Its fuel gate requires 12/12 seasons with positive planned charcoal demand, so a shorter route exposure is censored rather than counted as a pass. Coverage and its age bands are reported even when the fuel window is censored. The report separates MET-09 access season, local kit production, exact sister-settlement kit deliveries, issuance, and T11-to-T12 upgrades. Captured sites are censored for the I-T2 denominator because ownership changes within the measurement window.

Before the run, freeze source commit `TBD`, pack SHA-256 `eb08cfd11137c0c52a1b9955665355d5d1279d9464c5609f55dd190eeb2b288d`, and natural-match seeds `20261501–20261580` (80 matches, four Warlord seats each). Run each match to the natural Iron chapter end with `--it6-accounting`; do not change seeds, code, pack, or the approved cutoffs after starting. Command:

```sh
dotnet run --project src/CivSim.Cli -c Release --no-restore -- build-pack --out /tmp/civsim-b406-acceptance.pack
dotnet run --project src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-build -- iron-match --pack /tmp/civsim-b406-acceptance.pack --seed 20261501 --seeds 80 --it6-accounting
```

The trace changes are diagnostic only and are excluded from simulation state. They record current-season tool workers and site-level kit made, issued, received, sent, and upgraded. Whole-match kit conservation now treats issuance as a move from store to holder; it accounts for broken kits, T11 retirement on upgrade, deaths/razing, current holders, store inventory, and internal delivery. The reported per-tier residual must be zero.

## Deterministic B4-06 case matrix

| Requirement | Evidence | Result |
|---|---|---|
| Exact recipe conservation | `A_T11_kit_embodies_the_hand_checked_ore_wood_and_labor_from_26`; planner whole-stock identity in `An_Iron_settlement_plans_T11_kits_for_its_adults_through_the_existing_chain_and_conserves_the_ore` | Pass; the stale 0.15 wrought-per-kit conservation factor was corrected to the live 0.08 recipe input. |
| Imported kit / delivery | `Spare_iron_kits_move_to_a_sister_town_only_when_the_towns_share_and_exist_once` | Pass for domestic sister-settlement delivery: sender and receiver traces agree exactly and world inventory is unchanged. Iron Compacts currently trade bloom, not finished kits; no cross-civilization kit Compact is claimed. |
| Mine loss | `Iron_mine_exhaustion_stops_new_extraction_and_forging_after_stores_are_cleared` | Pass; controlled finite deposit drawdown equals Iron ore mined, and exhausting the tile stops new ore and kit production after staged stores are cleared. |
| Fuel loss | `Total_catchment_fuel_loss_stops_new_Iron_kit_production` | Pass; zero standing catchment and zero local fuel prevent new Iron output from staged ore. Existing stocked wrought iron remains covered by the separate thin-forest forging test. |
| Captured forge | `A_captured_forge_keeps_its_inventory_and_wear_cohorts_and_can_resume_production` | Pass; transfer preserves stored material, two held wear cohorts, and the forge; production resumes under the new owner's MET-09. |
| Mixed wear | `Mixed_T11_and_T12_wear_breaks_at_their_independent_exact_lifetimes` | Pass; simultaneous T11/T12 cohorts break at independent lifetimes, with 42/84 recorded tool-work seasons. |
| Upgrade | `An_early_upgrade_from_T11_to_T12_gives_no_metal_back_but_a_Bronze_kit_still_remelts` | Pass; one T11 retirement is accounted for once, T12 issuance does not create or destroy inventory, and no Iron-metal refund is emitted. |

The instrumentation refactors live wear and fixture wear through the same helper. It also records Iron kits retired through death or razing so whole-match conservation includes physical losses. Capture transfers keep their stores and holders; transfers between sister settlements are traced at both ends.

## I-T2 and I-T3 run results

Pending the preregistered 80-match run. The existing fixed-population replacement screens are in the [I-T6 acceptance report](m11-it6-whole-match-2026-11-05.md); they cover T11/T12 service and replacement but do not substitute for the new actual tool-worker denominator. The natural-match screen will separately report mixed-age and mixed-tier exposure. No I-T2 or I-T3 pass is claimed until this section is updated with uncensored observations.
