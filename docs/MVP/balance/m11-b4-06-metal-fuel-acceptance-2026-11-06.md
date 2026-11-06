# B4-06 metal and fuel closeout

**Status:** deterministic B4-06 cases and I-T3 service/wear verification pass. The preregistered 20261901–20261980 block was futility-stopped after 32 matches: it found I-T2 failures and one mature I-T6 fuel-cutoff failure. B4-06 remains open on the current source. The earlier I-T6 acceptance remains historical evidence for its frozen candidate; it does not override this new failure.

## Scope and frozen screen

B4-06 requires I-T2/I-T3/I-T6, exact material accounting, and imported-kit, mine-loss, fuel-loss, captured-forge, mixed-wear, and upgrade cases ([25 §14](../25-iron-age.md#14-proposed-m11-acceptance-series-b4-xx)). The fixed I-T6 cutoffs remain unchanged: at least 12 positive planned-charcoal demand-seasons, shortfall rate at most 5%, and unmet charcoal at most 1% of planned volume. The I-T2 target remains at least 70% of tool workers equipped T11+ within 12 seasons of a sustainable route.

The harness records actual settlement tool workers as adults with positive `toolWork` during the season, after that season's kit issue. The I-T2 diagnostic starts a settlement window at its first T11/T12 kit issue or sister delivery and ends after 12 complete seasons. It follows each kit tier backward through whole-kit transfers and applies fuel cutoffs only to sites that produced the supplied tier during that window. A relay-only or stock-only sender, missing source snapshot, ownership/liveness change, or fewer than 12 pooled producer-site demand-seasons censors the route instead of allowing unrelated fuel demand to qualify it. The shortfall rate and unmet/planned volume are pooled across the proven producer-site seasons, with the unchanged minimum of 12 demand-seasons. Coverage and its age bands are still reported for censored windows. The report separates MET-09 access season, hub assignment and facilities, site and hub wood factors, local kit production, exact sister-settlement kit deliveries and their sources, issuance, and T11-to-T12 upgrades. Captured sites are censored for the I-T2 denominator because ownership changes within the measurement window.

The `iron-fuel --it3-wear` trace reports live T11/T12 holders in fixed bins of remaining service as a fraction of that tier's service life. Its mixed-tier scenario starts with T11, grants MET-10 after 120 seasons, and records the distribution at route+12, MET-10+12, and run end. The trace is read-only and does not add state to simulation checksums or snapshots.

The first preregistered attempt used source `a443c069883017c4d5ba2c43b68d7ef863bcd2cf` and seeds `20261501–20261580`. It was stopped after two matches when the whole-match T11 residual was nonzero (+1 and +3 kits); neither result is accepted. The trace defect was kit relocation/lifecycle accounting, not simulation behavior. The corrected trace was validated separately on seed `20261999`: all `109/109` settlement kit ledgers closed exactly, with T11 and T12 world residuals both zero. This diagnostic seed is not part of the acceptance sample.

The source `b00bb1b5ec7335315de3e957ccdd783f855dc738` and seeds `20261601–20261680` had been preregistered but the run had not started. Before launch, the I-T3 review added the remaining-life trace; that preregistration is superseded. Freeze source `60e1e025e2d3e1d140994214d6f6fe0958682728`, pack SHA-256 `eb08cfd11137c0c52a1b9955665355d5d1279d9464c5609f55dd190eeb2b288d`, and the next unused natural-match seeds `20261701–20261780` (80 matches, four Warlord seats each). Run each match to the natural Iron chapter end with `--it6-accounting`; do not change seeds, code, pack, or the approved cutoffs after starting. Command:

That `20261701–20261780` block was started and then stopped after early reports showed nonzero T11 residuals (+2 in seed `20261701`, +3 in seed `20261721`). All partial reports from that block are invalid for acceptance. The missing event was a worn Iron kit overwritten by the Bronze handout while the Iron kit was still in its negative-remaining-life dormancy tail. The lifecycle is now recorded as retirement. Diagnostic reruns of the two affected seeds both closed all sites: `20261701` had zero world residual and `96/96` exact site ledgers; `20261721` had zero world residual and `114/114` exact site ledgers. These reruns are diagnostic only.

The preregistered block below froze source `db7b253721fd48df39678b317e57782a5794867a`, pack SHA-256 `eb08cfd11137c0c52a1b9955665355d5d1279d9464c5609f55dd190eeb2b288d`, and natural-match seeds `20261801–20261880` (80 matches, four Warlord seats each). It completed with 80 unique matches and exact whole-match/site accounting, but it did **not** pass I-T2: 68 of 1,055 eligible per-settlement windows failed coverage (6.45%); all 68 had qualified fuel and failed only the 70% tool-worker coverage target. Pooled coverage was 93.77%, which does not override the per-window failures. The misses exposed under-supply at active tool-worker settlements: an eligible wood hub was serving as the only producer while an existing, fully wood-qualified satellite forge/smelter stood idle.

The fix keeps the selected catchment hub responsible for assigned sites without a qualified local smith, while an already-built satellite with a full local wood factor and available Iron input can cover its own adult demand. The hub excludes that satellite's deficit to avoid planning the same demand twice. The I-T2 fuel gate now follows per-tier whole-kit transfer paths and qualifies only source sites that made the supplied tier during the 12-season window; an unproven relay or stock-only source is censored. It applies the existing rate, volume, and minimum-demand cutoffs over pooled, proven producer-site seasons. A Release diagnostic replay of seed `20261824` produced 18/18 actual toolworkers equipped at s2 after 12 seasons; s2 made 48.36 kits and received 2, its local wood factor was 100%, fuel sources were `[2,29]`, and their combined fuel sample had 0/23 short site-seasons and 0.00/53.07 L unmet/planned. With the tightened per-tier source tracing, the diagnostic reported 32 eligible screens passed, 0 failed, and 58 censored (including short-demand and unproven-source windows); coverage is still shown for every censored window. The whole-match T11/T12 kit residuals, forest and local-wood residuals, and all 96 site ledgers were exact; no crossing was attributed to Iron-era metal removal; and c2's maximum mature rolling full-chain share was 6.13%. This is a diagnostic replay, not acceptance evidence.

That block is superseded. The preregistered implementation source is commit `8c866b4ba94ee01a86ec161c00f775916dc30313`; the rebuilt content pack is 345,454 bytes with SHA-256 `eb08cfd11137c0c52a1b9955665355d5d1279d9464c5609f55dd190eeb2b288d`. Use the next unused natural-match seeds `20261901–20261980` (80 matches, four Warlord seats each), running each to the natural Iron chapter end with `--it6-accounting`. The I-T2 target remains at least 70% of actual toolworkers equipped within 12 seasons of route availability. The approved I-T6 cutoffs remain shortfall rate at most 5%, unmet charcoal at most 1% of planned volume, and at least 12 positive planned-charcoal demand-seasons for each measured civilization. These source, pack, seed, and cutoff values are frozen before launch; do not change code, pack, seeds, or cutoffs during the run. Use four disjoint 20-seed shards and preserve each raw output:

```sh
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll iron-match --pack /tmp/civsim-b406-acceptance-final.pack --seed 20261901 --seeds 20 --it6-accounting > /tmp/civsim-b406-acceptance-20261901-20.txt
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll iron-match --pack /tmp/civsim-b406-acceptance-final.pack --seed 20261921 --seeds 20 --it6-accounting > /tmp/civsim-b406-acceptance-20261921-40.txt
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll iron-match --pack /tmp/civsim-b406-acceptance-final.pack --seed 20261941 --seeds 20 --it6-accounting > /tmp/civsim-b406-acceptance-20261941-60.txt
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll iron-match --pack /tmp/civsim-b406-acceptance-final.pack --seed 20261961 --seeds 20 --it6-accounting > /tmp/civsim-b406-acceptance-20261961-80.txt
```

The trace changes are diagnostic only and are excluded from simulation state. They record current-season tool workers and site-level kit made, issued, received, sent, and upgraded; whole-kit transfer provenance also retains season, sender, and receiver so the I-T2 fuel gate can follow a supply path. Whole-match kit conservation treats issuance as a move from store to holder; it accounts for broken kits, Iron kits replaced by either tier, deaths/razing, current holders, store inventory, settlement fission/refugee relocation, and internal delivery. The reported per-tier world residual and every site residual must be zero for a match to be ledger-valid.

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

The `20261801–20261880` block failed I-T2 as reported above. The next frozen block used source `8c866b4ba94ee01a86ec161c00f775916dc30313`, the recorded pack hash, approved cutoffs and seeds `20261901–20261980`; it was stopped for futility after the first 32 unique matches because a mature I-T6 fuel cutoff and multiple I-T2 screens failed. Its output is preserved in the [futility output](m11-b4-06-futility-output-2026-11-06.txt). The remaining 48 seeds were not run, so this is a failed diagnostic block, not an acceptance result; the used seed block is consumed and will not be reused.

Across the 32 complete matches, the per-settlement screen reported 852 pass, 8 fail and 2,074 censored windows. The eight failures were three coverage misses and five fuel-cutoff misses. Coverage missed in seed `20261928` s47 (2/3 tool workers, 66.67%), seed `20261945` s44 (2/4, 50.00%), and seed `20261965` s32 (5/11, 45.45%). In seed `20261965`, s2, s13, s23, s34 and s55 also failed the producer-site fuel cutoff. Pooled coverage among non-fuel-failing screen rows was 5,308/5,343 (99.34%); it does not waive these per-window failures.

The same seed `20261965` failed the mature per-civilization I-T6 fuel cutoff: c2 had 159/381 short demand-seasons (41.73%) and 242.97/609.56 L unmet (39.86%), exceeding both approved limits. Of the 32 completed matches, this was the only mature fuel-cutoff failure. Kit ledgers were exact in all 32 matches, all forest/local-wood residuals were zero, no Iron-era metal clearing crossing occurred, and no mature rolling work-share window exceeded 15% (maximum 11.80%). Those passing ledgers, clearing and work-share checks do not offset the fuel or coverage failures.

Seed `20261965`'s map-level forest stock remained substantial, but c2's fuel-short settlements typically had under 5 L of harvestable wood inside their 40-tile physical reach while productive stands began at distances 41–48. This points to local hauling reach as the fuel failure's cause, rather than exhaustion of the whole map. The next candidate will widen the physical reach and the hub planning/siting catchment, retain distance-based hauling cost, and rerun the failing seed diagnostically before another preregistration. The other fixed-population replacement screens remain in the [I-T6 acceptance report](m11-it6-whole-match-2026-11-05.md); they do not substitute for this natural-match failure or the actual tool-worker denominator.

No I-T2 or current-source I-T6 pass is claimed. A new acceptance block will be preregistered only after the next candidate and its controlled diagnostic are frozen.
