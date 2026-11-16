# F-T3 furnace charcoal-factor candidate screen

**Result:** Fief's draft full-coverage furnace charcoal factor is fitted to **1.00**; the MET-12 row remains draft and unpromoted. The ≤15% rolling 12-season work-share rule (D13), existing denominator, ×4 iron/ore output semantics and ×2 labor are unchanged. The corrected 39-seed fixture screen passes the specified mechanics gates: 36/36 mature pairs and all 1,754 eligible windows are at or below 15%; 30/30 demand-eligible fuel pairs pass, nine are low-demand censored; forest crossings are zero and material residuals reconcile. Three seed pairs are censored for no mature work-share window. The full-match WP-E5-11 balance gate remains open.

## Method

An opt-in fixture/harness override changes only the Fief bloomery charcoal input factor at full shared craft coverage. It is blended from ×1 at zero coverage to the selected factor at full coverage. Following the cohort, the default Fief factor is 1.00; Iron and Bronze retain their previous rules. Bloomery labor, per-batch output, ore input, power allocation, and the work-share formula are unchanged; actual cumulative output can respond to fuel availability.

Ran paired furnace and no-furnace Fief traces for seeds 20261054, 20261037, 20261023, and passing neighbor 20261024, using 200 fixed people, 64 seasons, and the existing four-tile low-wood fixture. Swept factors 1.5, 1.4, 1.3, 1.25, 1.20, 1.15, and 1.10. Each missed the unchanged gate on seeds 20261037 and 20261054. Factor 1.00 passed those four diagnostics, so the corrected 39-seed block (`20261019–20261057`) was run at factor 1.00 against the default factor-2.00 treatment and matched no-furnace controls. The window gate is the maximum share over every rolling 12-season window after the fixture's existing maturity condition; neither the denominator nor the gate changed.

## Candidate windows

| Full-coverage factor | Seed | Maximum window | Metal / civilian work | Share | NoFuel in window | Full-run forest crossings | Late-window material residual |
|---:|---:|---|---:|---:|---:|---:|---:|
| 2.00 default | 20261023 | s7–18 | 71.89 / 451.61 | 15.9% | 0/12 | 0 | 0.000 L |
| 2.00 default | 20261024 | s5–16 | 72.14 / 520.12 | 13.9% | 0/12 | 0 | 0.000 L |
| 2.00 default | 20261037 | s5–16 | 77.89 / 470.98 | 16.5% | 0/12 | 0 | 0.000 L |
| 2.00 default | 20261054 | s6–17 | 77.96 / 463.79 | 16.8% | 0/12 | 0 | 0.000 L |
| 1.50 | 20261023 | s7–18 | 67.27 / 450.78 | 14.9% | 0/12 | 0 | 0.000 L |
| 1.50 | 20261024 | s5–16 | 67.87 / 519.32 | 13.1% | 0/12 | 0 | 0.000 L |
| 1.50 | 20261037 | s5–16 | 74.48 / 470.75 | 15.8% | 0/12 | 0 | 0.000 L |
| 1.50 | 20261054 | s6–17 | 73.49 / 463.06 | 15.9% | 0/12 | 0 | 0.000 L |
| 1.40 | 20261023 | s7–18 | 66.35 / 450.62 | 14.7% | 0/12 | 0 | 0.000 L |
| 1.40 | 20261024 | s5–16 | 66.93 / 519.09 | 12.9% | 0/12 | 0 | 0.000 L |
| 1.40 | 20261037 | s5–16 | 73.59 / 470.59 | 15.6% | 0/12 | 0 | 0.000 L |
| 1.40 | 20261054 | s6–17 | 72.41 / 462.68 | 15.7% | 0/12 | 0 | 0.000 L |
| 1.30 | 20261023 | s7–18 | 65.35 / 450.43 | 14.5% | 0/12 | 0 | 0.000 L |
| 1.30 | 20261024 | s5–16 | 65.84 / 518.79 | 12.7% | 0/12 | 0 | 0.000 L |
| 1.30 | 20261037 | s5–16 | 72.52 / 470.31 | 15.4% | 0/12 | 0 | 0.000 L |
| 1.30 | 20261054 | s6–17 | 71.43 / 462.46 | 15.4% | 0/12 | 0 | 0.000 L |

The lower four-seed sweep gives the same miss pattern until factor 1.00:

| Factor | 20261023 | 20261024 | 20261037 | 20261054 |
|---:|---:|---:|---:|---:|
| 1.25 | 14.4% | 12.6% | 15.3% | 15.4% |
| 1.20 | 14.3% | 12.4% | 15.2% | 15.3% |
| 1.15 | 14.2% | 12.3% | 15.1% | 15.2% |
| 1.10 | 14.1% | 12.2% | 15.1% | 15.1% |
| 1.00 | 13.9% | 12.0% | 14.9% | 14.9% |

## Corrected 39-seed factor-1.00 candidate screen

The block `20261019–20261057` produced **1,754 eligible mature rolling windows across 36 seed pairs; all 1,754 were at or below 15.0%**. The maximum was 14.94% (seed 20261054, seasons 6–17). Three seeds had no mature work-share window and are censored, not passes: `20261028`, `20261049`, and `20261055`. A second run with no harness override and the fitted production default was byte-identical to the opt-in factor-1.00 trace. The companion CSV records every eligible window's start/end seasons, metal work, civilian work, and share: [all 1,754 windows](m12-wp8c-fief-t3-factor-1-cohort-windows-2026-11-14.csv). Full default-path trace output is preserved in [the factor-1.00 cohort trace](m12-wp8c-fief-t3-factor-1-cohort-trace-2026-11-14.txt); [the earlier factor-2.00 comparison trace](m12-wp8c-fief-t3-factor-2-default-production-trace-2026-11-14.txt) supplies the historical production deltas.

Fuel and forest checks passed for the observed quantities: final-half NoFuel share was 0.0%, the planned-charcoal cutoff passed for 30/30 demand-eligible seed pairs with 9 low-demand pairs censored, furnace-attributed forest crossings were 0/39, and material residual was 0.000 L in every candidate row. The full per-seed candidate/default production comparison is recorded in [the factor-1.00 production CSV](m12-wp8c-fief-t3-factor-1-cohort-production-2026-11-14.csv).

Against the same-seed factor-2.00 treatment, factor 1.00 lowered at least one cumulative quantity (bloomery iron, wrought, or ore mined) in seven seeds: `20261019`, `20261022`, `20261025`, `20261034`, `20261037`, `20261044`, and `20261045`. The largest differences were seed `20261034` (iron −0.70 L, wrought −0.56 L, ore −1.40 L); seed `20261037` had wrought output −0.08 L while iron and ore increased. These are diagnostic differences against the earlier factor, not a specified pass/fail criterion. The MET-12 per-batch semantics remain ×4 iron from ×4 ore at full power, verified by the focused recipe test. The three immature seeds above remain excluded from work-share pass counts.

The paired no-furnace controls had zero forest crossings and 0.000 L residual across this block. The fitted Fief default is 1.00, and the replay through the production-default path was byte-identical to the opt-in factor-1.00 trace. The specified F-T3 mechanics gates pass: every eligible mature window is at most 15%, demand-eligible fuel checks pass, there are no furnace-attributed forest crossings, and material residuals reconcile. Old-factor versus fitted-factor cumulative production deltas remain diagnostic; they are not an additional acceptance threshold. Whole-match balance and WP-E5-11 remain open.

## Production and accounting checks

The recipe fixture regression confirms that the override/default leaves bloomery batch output, ore-input formula, labor, and craft coverage unchanged while changing only Fief charcoal input. At full power, the fitted default retains ×4 iron from ×4 ore, ×1 charcoal and ×2 labor per batch. Whole-run cumulative output still varies with fuel flow; the per-seed comparisons above record those effects.

Fuel shortfall remained 0/12 in every candidate's maximum work-share window, no full-run forest-clear threshold crossings occurred, and the late-window wood material residual was 0.000 L throughout. These checks support the local F-T3 mechanics result; whole-match balance remains a separate open gate.

The factor fit and default are Fief-only; Iron behavior is unchanged. MET-12 remains a draft row and was not promoted. No golden or D13 threshold changed. Focused validation passed 15/15: `FiefFurnaceCharcoalFitTests`, `FiefPowerTests`, and the mode snapshot isolation test `Snapshots_carry_the_mode_and_no_mode_reads_another_modes_save`. The local F-T3 mechanics screen passes at 1.00; whole-match balance and WP-E5-11 remain open.

## Current-source readiness audit (2026-11-16)

The results above are historical 2026-11-14 fixture diagnostics, not current-source acceptance evidence. The canonical checkout is dirty in Harness/Sim, and no frozen WP-E5-5/F-T3 40-seed manifest with preregistration identity and exact binary/source pins is present. The historical 39-seed, 1,754-window result does not substitute for the runner's 40-seed preregistered cohort. The ≤15% gate remains unchanged and applies to every measured mature window; any no-exposure mature window, no-mature-window seed, missing arm, failed run, or provenance drift leaves the cohort incomplete rather than passing it.

After the candidate is frozen and the external manifest is available, run the preregistered cohort with fresh binaries and a new output directory:

```sh
python3 tools/wp_e5_5_ft3_cohort.py \
  --manifest <frozen-ft3-manifest.json> \
  --manifest-sha256 <manifest-sha256> \
  --repo <canonical-e5-repo> \
  --pack <frozen-pack> \
  --harness <Release/CivSim.Harness.dll> \
  --sim <adjacent-Release/CivSim.Sim.dll> \
  --output-root <new-output-directory>
```

The manifest must carry the preregistered 40 distinct seeds, exact fixture and arm definitions, and source/pack/binary/runner pins. Preserve the runner's `analysis.json`, manifest copy, and per-arm logs; inspect each seed's complete window sequence, no-exposure/censor counts, run errors, and pre/post pins before reporting a result.
