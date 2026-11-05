# M11 I-T6 whole-match accounting and clearing approval protocol

**Status:** the user approved the clearing event definition, zero Iron-attributed allowance, and every-window work-share rule on 2026-11-05. The user delegated the fuel cutoff choice; the cutoff below is fixed before the acceptance run. The eight-seed results remain diagnostic and do not close B4-06.

I-T6 in [25 §13](../25-iron-age.md#13-provisional-balance-targets-and-oracle-work) proposes that sustainable metal use no more than 15% of civilian work in steady state and require no mandatory clear-cut. It requires full replacement, fuel and haul accounting, including import-loss and low-wood stresses. The game already defines a clearing transition: an original forest tile that falls below 5 L becomes cleared woodland ([23 §7.4](../23-e3-bronze-specs.md#74-forest-stock-clearing-and-regrowth)). This document defines an approval-ready measurement and records which decisions still need explicit approval.

## Reproduction

- Source baseline: `d2770d2` (Iron route access and age coverage); approved fuel accounting and cutoff instrumentation revision: `5b779d1` (extends the whole-match accounting in `1513a70`); spatial fuel diagnostic revision: `0a62944` (read-only trace only); controlled spatial supply/reach comparison revision: `defd9ec`.
- Content pack SHA-256: `85f436c611870c6a34d75dc56a8624c702d2e3810a5c1ed3edd6daef05ae7ee9`.
- Natural Iron cohort: four Warlord AI seats (Builder, Grower, Warlord, Sage), default Iron chapter limits, no research grant, played to chapter end. The current screen uses seeds `20261019–20261026`.
- Build the current-content pack with `dotnet run --project src/CivSim.Cli -c Release --no-restore -- build-pack --out /tmp/civsim-it6-current.pack`.
- Run the accounting with `dotnet run --project src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-build -- iron-match --pack /tmp/civsim-it6-current.pack --seed 20261019 --seeds 8 --it6-accounting`.
- Run the paired fixed-population fuel stress with `dotnet run --project src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-build -- iron-fuel --pack /tmp/civsim-it6-current.pack --seasons 240 --seeds 20261019,20261020,20261021,20261022,20261023,20261024,20261025,20261026`.
- Run the matched spatial supply/reach comparison with the same fixture population and seed block using `dotnet run --project src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-build -- iron-fuel --pack /tmp/civsim-it6-current.pack --seasons 240 --seeds 20261019,20261020,20261021,20261022,20261023,20261024,20261025,20261026 --spatial-only`.
- A Bronze comparison screen was also run on the same seeds with `--minutes 82 --it6-accounting --bronze-control`. Those Bronze matches reached their normal terminal condition after 59.0–64.0 simulated minutes, earlier than their Iron counterparts; do not use their whole-match clearing totals as duration-matched causal comparisons.

The trace is read-only and excluded from simulation state. It records whole-map original forest tiles once (never summing overlapping settlement catchments), all settlement wood stores, kit output/issuance/service use, the metal work ledger, Iron ore and fire-setting, charcoal use and wood, and haul labor. World transfers of stored goods remain internal to the aggregate. Forest and local-stock residuals are calculated from fixed-point flows.

## Accounting definitions

### Work share

For each civilization, the full-chain numerator is recorded mine, charcoal, smelt and camp labor (including the haul work those jobs spend), plus actual T11/T12 kit recipe labor and metal-facility construction labor. The denominator is civilian work recorded by the metal ledger over the same interval. Startup construction is shown separately through the match totals.

The approved mature-window protocol starts at the first season with MET-09 complete and an Iron kit issued, allows 12 complete seasons for the route to mature, then evaluates every rolling 12-season window through match end. This uses the documented 12-season route window as the operational meaning of “steady state.” The 12-season ramp is reported and is not folded into the mature-window pass/fail result.

### Replacement and fuel

T11/T12 kits made, issued to adults, held at match end, and still in stores are reported separately. Tool-work by tier, kit breaks and healthy T11-to-T12 upgrades show how much service those issues actually delivered. A short match with few complete wear cycles is an incomplete live replacement sample even if kit inventories reconcile.

The report counts charcoal produced, total charcoal wood, fire-setting wood, charcoal inputs to the Iron ore/bloom/wrought/steel/tool recipes, Iron ore mined, and charcoal/ore haul work. For fuel accounting, an eligible settlement-season has a positive planned charcoal-production volume after stored charcoal and other recipe inputs are accounted for. A fuel-short season is one in which more than 0.01 charcoal batch remains unmade while more than 0.01 work remains after the available wood sources are exhausted. The unmet volume is the remaining planned batches times the charcoal output quantity; it is reported in litres and as a share of planned charcoal output. The shortfall rate is short settlement-seasons divided by eligible demand settlement-seasons.

The approved fuel cutoff applies per mature-qualified civilization after the 12-season route ramp, and per seed in each qualifying fixed-pop stress fixture: shortfall rate **≤5.00%** and unmet charcoal **≤1.00% of planned charcoal volume**. Both limits must pass. A seat/fixture with fewer than 12 eligible demand settlement-seasons is fuel-censored and is not a pass. Report raw unmet litres as well as the normalized share. This permits isolated very small shortfalls while limiting both their frequency and their material volume; no cohort average may hide a failing seat or stress seed.

### Clearing event and attribution

Count a clearing episode when a tile that began as forest crosses from at least 5 L of standing wood to below 5 L. If regrowth restores a cleared tile to forest and it crosses again later, count the later episode too. Report the number of original forest tiles still cleared and the number that reached grassland at match end.

The last standing-wood removal immediately causing each crossing is classified as chop, metal before MET-09, post-MET-09 Iron-era metal, civic, or other. “Iron-era metal” conservatively includes metal-chain standing-wood removals after MET-09; it is a proximate cause label, not a claim that earlier users did not contribute to a tile’s depletion. All-cause civic/chop clearing is reported separately and is not automatically attributed to the Iron chain.

## Approved measurement and clearing protocol

1. Count every original forest tile transition from at least 5 L to below 5 L; count a later transition again if regrowth restores forest and it is cleared again. Keep the game's 5 L threshold unchanged. Do not tune it, the Iron wood throttle, recipe inputs, or reserve after observing a miss; a target change requires separate approval.
2. **Approved clearing allowance:** zero post-MET-09 Iron-era metal-attributed crossings in every qualifying Iron match and stress fixture. Attribution follows the last standing-wood removal that caused the crossing. This is proximate attribution, not proof that earlier users made no contribution to depletion. Civic, household chop, pre-MET-09 metal, and other causes remain in the report but do not count against the Iron-specific allowance.
3. Run paired low-wood stresses with and without Coppicing and with import loss. Coppicing may add replacement margin but is not a prerequisite for the zero-crossing rule. The existing fixed-pop screen reports zero Iron crossings in its eight-seed scenarios, including low wood without Coppicing and import loss; see the [preliminary balance screen](m11-iron-balance-screen-2026-11-04.md#i-t6-fuel-and-work-screen).
4. For natural whole matches, predeclare at least 80 seeds (or a justified number of qualifying route exposures under 25 §13), hold out a seed block, and classify every seat as mature-qualified, route-censored, or no Iron route. The approved work-share test is **≤15% in every complete rolling 12-season window**, starting after MET-09, the first Iron-kit issue, and 12 complete route seasons. Report the ramp separately. Apply the approved fuel limits in Replacement and fuel to each mature-qualified civilization; for fixed-pop stresses, apply them to each seed. Also report kit service, replacements, and clearings by cause.
5. Any claim about *excess total* clearing requires same-seed Bronze and Iron runs on the same predeclared season horizon, with the natural terminal condition prevented from shortening one arm. Natural Bronze runs that terminate earlier are not duration-matched controls. The Iron-specific zero-crossing decision in step 2 is reported independently of this total-clearing comparison.
6. Publish an immutable acceptance record with the approved event definition, zero allowance, every-window work-share rule, fuel limits, seed blocks, content hash, qualifying/censored seat counts, and source revision. A nonzero Iron-attributed event, a work-share window above 15%, fuel above either cutoff, an unbalanced ledger, or an undersized sample leaves the gate open. A failure does not authorize an in-run target change.

**Approval record (2026-11-05):** user approved the event definition and zero Iron-attributed allowance, and approved evaluating the 15% ceiling on every mature 12-season window. The user delegated selection of the fuel cutoff. This report fixes the per-seat/per-seed limits at ≤5.00% of eligible demand settlement-seasons fuel-short and ≤1.00% of planned charcoal output unmet, before the acceptance run.

## Whole-match screen results

Eight natural Iron matches completed at 78.0–83.0 simulated minutes each, using seeds `20261019–20261026` and four Warlord AI archetypes per match. This is a diagnostic screen below the 80-match sample in §13. It cannot close I-T6.

| Whole-match measure | Result across the eight matches | Reading |
|---|---:|---|
| Route exposure | 32 seats: 29 mature-qualified, 1 route-censored before maturity, 2 with no Iron route | All seats remain in the sample report; censored and no-route seats do not count as passes |
| Full-chain work share, entire match | Maximum seat share 10.90% | Startup and ramp dilute this aggregate; it does not establish steady-state compliance |
| Full-chain work share, mature rolling windows | 968 windows across 29 seats; maximum 15.20%; 1 window above 15% | **Proposed work-share screen misses** under the every-window rule. The outlier is seed `20261023`, Grower, seasons 271–283: 745.21/4,902.51 work-seasons, split metal/craft/facility 549.52/54.72/140.97. The 12-season ramp peaked at 21.87% in a different seat. |
| Whole-map forest flow | 413,630.00 L opening; 228,030.43 L regrowth; 48,178.93 L chop removal; 181,695.41 L metal removal; 33,181.77 L civic removal; 378,604.31 L closing | Forest-stock residual is 0.00 L in each match. These are sums of eight independent worlds, not unique matched tiles |
| Local wood flow | 0.00 L opening; 159,422.14 L inflow; 154,449.02 L use; 4,973.11 L closing | Local-stock residual and combined forest-plus-local residual are each 0.00 L in every match |
| Threshold crossings | 1,537 episodes: 57 chop, 0 pre-MET-09 metal, 0 post-MET-09 Iron metal, 1,480 civic, 0 other; 448 cleared-tile match-observations and 5 grassland observations at end | Observed Iron-attributed crossing count is zero. Civic/chop crossings remain all-cause outcomes, not Iron-attributed events |
| Kit output and service | T11: 13,661.56 made, 13,249 issued, 412.56 stored, 11,208 holder observations; T12: 744.81 made, 647 issued, 97.81 stored, 620 holder observations | Made − issued − stored residual is 0.00 kits; holder observations are end-state people, not kit counts. Service exposure is incomplete: one T11 break in the natural cohort |
| Mature post-ramp fuel cutoff | 29 seats measured; 17 pass, 12 fail. Across measured seats: 843/17,538 eligible demand-seasons short (4.81%) and 1,351.28/18,502.67 L planned unmet (7.30%). Worst seat: 31.85% short seasons and 39.46% of planned charcoal unmet | **Fails** the approved per-seat cutoffs (≤5.00% and ≤1.00%). The pooled rate hides failing seats; this is why each seat is gated separately |
| Fixed-pop fuel stresses, final 120 of 240 seasons | Standard Iron, standard-reach no-Coppicing, cart-reach low wood with and without Coppicing, import loss, and wood-rich: 8/8 seeds pass each. Default-reach low wood with Coppicing: 7/8 pass; seed `20261026` fails at 6/120 short seasons (5.00%) and 0.1056/0.6738 L unmet (15.7%). Default-reach low wood without Coppicing: 7 pass, 1 seed has no planned-charcoal demand and is censored. | The fuel cutoff catches the default-reach low-wood Coppicing outlier. TRN-05 cart reach passes this screen with and without Coppicing. A censored no-demand seed is not counted as a pass |

The per-match trace also records Iron recipe charcoal, charcoal wood, fire-setting wood, Iron/all-ore mined, charcoal/ore haul work, facility construction, kit craft work, and tool service work. The fixed-pop fuel screen applies the same two cutoffs per seed to the final 120 seasons; all tested scenarios report zero Iron-attributed crossings. Coppicing remains optional for the clearing rule and increases low-wood replacement margin; these runs do not prove whole-match sustainability.

## Spatial diagnosis of fuel-short settlements

The read-only `--it6-accounting` trace now reports wood available to each fuel-short settlement immediately before its short burn: raw standing stock and harvestable stock above the game's 5 L/tile reserve, split between the normal radius-3 cutting area and the wider charcoal reach (8 tiles, or 12 with TRN-05 carts). It also reports local stored wood, remaining work, and the settlement's current match-end catchment stock. These per-settlement catchments overlap, so their values must not be added together.

| Natural match | Whole-map forest stock, opening → end; regrowth | Fuel-short settlement evidence |
|---|---|---|
| Seed `20261026` | 26,420.00 → 22,399.98 L; 22,231.95 L regrowth; zero stock-ledger residual | c1/s91 had 37/37 demand-seasons short with no reachable or harvestable wood at any recorded short burn and none at match end. c0/s14 had 71/71 with the same zero-wood pattern. c3/s35 had 50/52; mean reachable stock was 10.46 L but only 0.46 L was above the per-tile reserve, all outside radius 3; its end catchment held 10.00 L raw and 0.00 L harvestable. |
| Seed `20261020` | 51,000.00 → 46,803.90 L; 34,960.32 L regrowth; zero stock-ledger residual | c1/s11 had 57/61 demand-seasons short; at short burns it averaged 37.95 L raw but only 2.85 L harvestable, all outside radius 3; at match end only 5.00 L raw and 0.00 L harvestable remained in reach. |

This rules out **whole-map exhaustion** as the explanation for these failures: both maps finish with most of their opening forest stock, while particular settlements have no local wood or almost no wood above the 5 L reserve. The evidence points first to **settlement-level supply distribution and transport reach**, with the reserve threshold making thinly stocked catchments unusable for Iron fuel.

It is not only a case of permanently empty catchments. In seed `20261026`, c1/s1 was short in 10/46 demand-seasons despite averaging 281.34 L raw and 34.18 L harvestable across its full reach before short burns; its match-end catchment had 186.95 L harvestable. All of that harvestable stock was beyond radius 3 and required hauling. This is consistent with a transient or haul-cost bottleneck, but the trace does not yet prove whether each such event is caused by haul work, overlapping use, or a demand spike. The reported neighbor counts alone do not establish competition. The fixed-population 30-forest-tile “wood-rich” fixture passes the fuel cutoff in all eight seeds, supporting sensitivity to supply around the settlement, but it is not a matched natural-map counterfactual.

**Natural-match diagnosis:** adding more wood to the world globally is not currently supported as the needed fix. Shortages occur where individual settlement catchments are empty or hold almost no wood above reserve; some other shortages occur with wood in the outer catchment and may be haul- or timing-limited.

## Controlled nearby-supply and hauling-reach comparison

To separate total biomass from its position, the fixed-population fixture placed the same four 30 L forest tiles (120 L opening stock) around one 60-person Iron settlement. Coppicing was disabled so all positions had the same base growth (up to 2 L/season below the per-tile cap). All other initial wood within radius 12 was removed. The only changed factors were forest distance and the existing transport project: near at radii 1–3, haul-only at 4–8, and cart-only at 9–12; default charcoal reach is 8 and TRN-05 extends it to 12 while reducing haul labor cost. Each condition ran 240 seasons on seeds `20261019–20261026`; the approved cutoff was applied per seed to the final 120 seasons: shortfall rate ≤5.00%, unmet volume ≤1.00% of planned charcoal, and at least 12 eligible demand-seasons.

| Forest position | Default reach 8 | TRN-05 reach 12 |
|---|---|---|
| Near (radii 1–3) | Fuel-censored in 8/8 seeds: no positive planned-charcoal demand in the final half, so this is not a cutoff pass. The reachable forest ended at 17% of opening stock; no metal charcoal wood was cut in the final half. | Same: 8/8 censored, 17% stock retention, no final-half metal charcoal wood. Extending transport reach does not help wood already within the normal cutting radius. |
| Haul-only (radii 4–8) | **8/8 pass**: zero short seasons and zero unmet volume; mean final-half planned charcoal 27.45 L/seed. Reachable forest retained 60% on average (53% worst seed); final-half regrowth/removal was 1.49/1.37 L/season. | **8/8 pass**: zero short seasons and zero unmet volume; mean planned charcoal 27.25 L/seed. Mean retention was 62% (58% worst seed); regrowth/removal was 1.49/1.36 L/season. The default reach already serves this distance band; carts add little here. |
| Cart-only (radii 9–12) | **0/8 pass**: all 120/120 demand-seasons were short and 100% of planned charcoal was unmet (153.22 L/seed on average); the forest lies beyond reach. | **8/8 pass**: zero short seasons and zero unmet volume; mean planned charcoal 27.32 L/seed. Mean forest retention was 62% (56% worst seed); regrowth/removal was 1.48/1.37 L/season. |

The near results are censored rather than successful: the local stand is drawn down to about 17% of its opening stock, which is below the Iron throttle's 50% floor, and the Iron chain makes no planned charcoal in the measured half. In this fixture, the close wood is available to other local uses, while the 4–8 tile stands remain reachable to Iron through hauling. That is consistent with local demand paying down the nearby stock before mature Iron fuel demand; it shows why “forest is near the town” alone does not guarantee Iron fuel.

The far pair isolates the practical value of TRN-05: a 9–12 tile stand is unusable at default reach and fully usable with carts. Because TRN-05 changes both reach and haul cost, this comparison measures the combined project effect rather than separating those two mechanics. Every scenario recorded zero forest-threshold crossings, and the mature work-share maximum was 4.5%, below 15% where a route was active. This fixture comparison supports a spatial-access explanation, but it does not replace natural whole-match acceptance.

The full harness output is preserved in [the spatial comparison result](m11-it6-spatial-comparison-output-2026-11-05.txt).

Same-seed Bronze whole matches were capped at 82 minutes but ended naturally after 59.0–64.0 simulated minutes, before the Iron matches. Their total clearing counts are therefore not valid equal-duration controls. No conclusion about Iron *excess total* clearing is drawn from that comparison.

**Disposition:** the accounting ledgers reconcile exactly and the approved Iron-specific clearing allowance had zero observed crossings in this screen. The approved mature work-share screen has one observed exceedance, and the approved fuel screen fails in 12/29 mature seats plus one low-wood fixture seed. One default-reach, no-Coppicing fixture seed is fuel-censored. The eight-match sample is insufficient for final acceptance. I-T6 remains open; the approved cutoffs are fixed for the acceptance run. No production balance parameters were changed by this accounting work.

