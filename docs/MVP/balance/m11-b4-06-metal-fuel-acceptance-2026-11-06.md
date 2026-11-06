# B4-06 metal and fuel closeout

**Status:** deterministic B4-06 cases and I-T3 service/wear verification pass. The actual tool-worker I-T2 screen is pending the preregistered run below. I-T6's separately approved balance screen remains closed under its existing report.

## Scope and frozen screen

B4-06 requires I-T2/I-T3/I-T6, exact material accounting, and imported-kit, mine-loss, fuel-loss, captured-forge, mixed-wear, and upgrade cases ([25 §14](../25-iron-age.md#14-proposed-m11-acceptance-series-b4-xx)). The fixed I-T6 cutoffs remain unchanged: at least 12 positive planned-charcoal demand-seasons, shortfall rate at most 5%, and unmet charcoal at most 1% of planned volume. The I-T2 target remains at least 70% of tool workers equipped T11+ within 12 seasons of a sustainable route.

The harness now records actual settlement tool workers as adults with positive `toolWork` during the season, after that season's kit issue. The I-T2 diagnostic starts a settlement window at its first T11/T12 kit issue and ends after 12 complete seasons. Its fuel gate requires 12/12 seasons with positive planned charcoal demand, so a shorter route exposure is censored rather than counted as a pass. Coverage and its age bands are reported even when the fuel window is censored. The report separates MET-09 access season, local kit production, exact sister-settlement kit deliveries, issuance, and T11-to-T12 upgrades. Captured sites are censored for the I-T2 denominator because ownership changes within the measurement window.

The `iron-fuel --it3-wear` trace reports live T11/T12 holders in fixed bins of remaining service as a fraction of that tier's service life. Its mixed-tier scenario starts with T11, grants MET-10 after 120 seasons, and records the distribution at route+12, MET-10+12, and run end. The trace is read-only and does not add state to simulation checksums or snapshots.

The first preregistered attempt used source `a443c069883017c4d5ba2c43b68d7ef863bcd2cf` and seeds `20261501–20261580`. It was stopped after two matches when the whole-match T11 residual was nonzero (+1 and +3 kits); neither result is accepted. The trace defect was kit relocation/lifecycle accounting, not simulation behavior. The corrected trace was validated separately on seed `20261999`: all `109/109` settlement kit ledgers closed exactly, with T11 and T12 world residuals both zero. This diagnostic seed is not part of the acceptance sample.

The source `b00bb1b5ec7335315de3e957ccdd783f855dc738` and seeds `20261601–20261680` had been preregistered but the run had not started. Before launch, the I-T3 review added the remaining-life trace; that preregistration is superseded. Freeze source `60e1e025e2d3e1d140994214d6f6fe0958682728`, pack SHA-256 `eb08cfd11137c0c52a1b9955665355d5d1279d9464c5609f55dd190eeb2b288d`, and the next unused natural-match seeds `20261701–20261780` (80 matches, four Warlord seats each). Run each match to the natural Iron chapter end with `--it6-accounting`; do not change seeds, code, pack, or the approved cutoffs after starting. Command:

That `20261701–20261780` block was started and then stopped after early reports showed nonzero T11 residuals (+2 in seed `20261701`, +3 in seed `20261721`). All partial reports from that block are invalid for acceptance. The missing event was a worn Iron kit overwritten by the Bronze handout while the Iron kit was still in its negative-remaining-life dormancy tail. The lifecycle is now recorded as retirement. Diagnostic reruns of the two affected seeds both closed all sites: `20261701` had zero world residual and `96/96` exact site ledgers; `20261721` had zero world residual and `114/114` exact site ledgers. These reruns are diagnostic only.

The active preregistration supersedes the earlier blocks before acceptance. Freeze source `db7b253721fd48df39678b317e57782a5794867a`, pack SHA-256 `eb08cfd11137c0c52a1b9955665355d5d1279d9464c5609f55dd190eeb2b288d`, and natural-match seeds `20261801–20261880` (80 matches, four Warlord seats each). Run each match to the natural Iron chapter end with `--it6-accounting`; do not change seeds, code, pack, or approved cutoffs after starting. Command:

```sh
dotnet run --project src/CivSim.Cli -c Release --no-restore -- build-pack --out /tmp/civsim-b406-acceptance.pack
dotnet run --project src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-build -- iron-match --pack /tmp/civsim-b406-acceptance.pack --seed 20261801 --seeds 80 --it6-accounting
```

The trace changes are diagnostic only and are excluded from simulation state. They record current-season tool workers and site-level kit made, issued, received, sent, and upgraded. Whole-match kit conservation treats issuance as a move from store to holder; it accounts for broken kits, Iron kits replaced by either tier, deaths/razing, current holders, store inventory, settlement fission/refugee relocation, and internal delivery. The reported per-tier world residual and every site residual must be zero for a match to be ledger-valid.

## Deterministic B4-06 case matrix

| Requirement | Evidence | Result |
|---|---|---|
| Exact recipe conservation | `A_T11_kit_embodies_the_hand_checked_ore_wood_and_labor_from_26`; planner whole-stock identity in `An_Iron_settlement_plans_T11_kits_for_its_adults_through_the_existing_chain_and_conserves_the_ore` | Pass; the stale 0.15 wrought-per-kit conservation factor was corrected to the live 0.08 recipe input. |
| Imported kit / delivery | `Spare_iron_kits_move_to_a_sister_town_only_when_the_towns_share_and_exist_once` | Pass for domestic sister-settlement delivery: sender and receiver traces agree exactly and world inventory is unchanged. Iron Compacts currently trade bloom, not finished kits; no cross-civilization kit Compact is claimed. |
| Mine loss | `Iron_mine_exhaustion_stops_new_extraction_and_forging_after_stores_are_cleared` | Pass; controlled finite deposit drawdown equals Iron ore mined, and exhausting the tile stops new ore and kit production after staged stores are cleared. |
| Fuel loss | `Total_catchment_fuel_loss_stops_new_Iron_kit_production` | Pass; zero standing catchment and zero local fuel prevent new Iron output from staged ore. Existing stocked wrought iron remains covered by the separate thin-forest forging test. |
| Captured forge | `A_captured_forge_keeps_its_inventory_and_wear_cohorts_and_can_resume_production` | Pass; transfer preserves stored material, two held wear cohorts, and the forge; production resumes under the new owner's MET-09. |
| Mixed wear | `Mixed_T11_and_T12_wear_breaks_at_their_independent_exact_lifetimes` | Pass; simultaneous T11/T12 cohorts break at independent lifetimes, with 42/84 recorded tool-work seasons. |
| I-T3 lifetime, modifier interaction, and live distribution | `T12_lasts_exactly_twice_as_long_as_T11_and_neither_counts_as_a_Bronze_kit`; `The_saw_adds_forty_percent_to_a_T12_kits_chop_and_to_nothing_else`; `I_T3_live_wear_trace_bins_remaining_service_by_kit_tier_without_changing_state`; controlled 240-season `iron-fuel --it3-wear` run below | Pass; service is 30/60 tool-work seasons (2×), the T12/WOD-08 effect is exactly ×1.4 and does not apply to T11, and the live trace shows mixed tiers and remaining-life distributions after MET-10. |
| Upgrade | `An_early_upgrade_from_T11_to_T12_gives_no_metal_back_but_a_Bronze_kit_still_remelts` | Pass; one T11 retirement is accounted for once, T12 issuance does not create or destroy inventory, and no Iron-metal refund is emitted. |

The instrumentation refactors live wear and fixture wear through the same helper. It also records Iron kits retired through death or razing so whole-match conservation includes physical losses. Capture transfers keep their stores and holders; transfers between sister settlements are traced at both ends.

## I-T3 live remaining-life distribution

Command, source `60e1e025e2d3e1d140994214d6f6fe0958682728`, and pack hash as above:

```sh
dotnet run --project src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-build -- iron-fuel --pack /tmp/civsim-b406-acceptance.pack --scenario iron-hub-low-wood-mixed-t12 --seeds 20261019,20261020 --it3-wear
```

The fixture preloaded 40 T11 kits, grants MET-10 at season 120, then adds 10 T12 kits so live replacement/upgrades exercise both tiers. “80+” means at least 80% of service remains; “<0” is the modeled dormancy tail. Counts below are `T11 / T12`, in bins `<0, 0–20, 20–40, 40–60, 60–80, 80+`:

| Seed | Checkpoint | Remaining-life bins | T11→T12 upgrades by end |
|---:|---|---|---:|
| 20261019 | route+12 | `0,0,0,0,9,19 / 0,0,0,0,0,0` (28/0 holders) | 10 |
| 20261019 | MET-10+12 (season 132) | `0,0,0,0,3,15 / 0,0,0,0,0,10` (18/10) | 10 |
| 20261019 | end (season 240) | `1,0,0,0,0,15 / 0,0,2,0,3,7` (16/12) | 10 |
| 20261020 | route+12 | `0,0,0,0,4,24 / 0,0,0,0,0,0` (28/0 holders) | 10 |
| 20261020 | MET-10+12 (season 132) | `0,0,0,0,3,15 / 0,0,0,0,0,10` (18/10) | 10 |
| 20261020 | end (season 240) | `0,0,0,1,0,14 / 1,0,4,1,3,4` (15/13) | 10 |

This closes I-T3's deterministic lifetime, multiplier, and live mixed-tier distribution evidence. It is a controlled fixed-population screen, not natural-match supply or replacement acceptance.

## I-T2 acceptance results

Pending the active preregistered 80-match run. The existing fixed-population replacement screens are in the [I-T6 acceptance report](m11-it6-whole-match-2026-11-05.md); they cover T11/T12 service and replacement but do not substitute for the new actual tool-worker denominator. The acceptance run will report every settlement's actual tool-worker coverage, access and delivery path, mixed-age exposure, fuel eligibility, and whole-match kit residuals. No I-T2 pass is claimed until the run results and uncensored, fuel-qualified observations are recorded.
