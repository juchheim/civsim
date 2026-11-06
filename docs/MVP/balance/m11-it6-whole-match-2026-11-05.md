# M11 I-T6 whole-match accounting and clearing approval protocol

**Status:** the user approved the clearing event definition, zero Iron-attributed allowance, and every-window work-share rule on 2026-11-05. The user delegated the fuel cutoff choice; the cutoff below remains fixed. The first held-out candidate failed three mature-seat fuel cutoffs and one work window, so its 80-match run was stopped after 47 completed matches; that failure is preserved below. The second candidate completed all 80 held-out matches: all 277 work-evaluable mature seats passed fuel limits, Iron-attributed clearings remained zero, and all ledgers reconciled, but four mature work-share windows exceeded 15% (maximum 16.81%). I-T6 remains open on the work-share gate.

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

## First held-out acceptance preregistration (failed candidate)

Before the acceptance run, freeze the current rule at source revision `f20e9eb6f81c7862ef853b4ab1a70fc59faf42cc`, content-pack SHA-256 `85f436c611870c6a34d75dc56a8624c702d2e3810a5c1ed3edd6daef05ae7ee9`, and natural-match seed block `20261101–20261180` (80 consecutive seeds, four Warlord AI archetypes per match). The approved limits remain unchanged: zero post-MET-09 Iron-attributed clearing crossings; no mature rolling 12-season full-chain work-share window above 15%; and, per mature-qualified seat, fuel-short demand settlement-seasons ≤5.00%, planned charcoal unmet ≤1.00%, and at least 12 eligible demand seasons. Run every seed through the natural Iron chapter terminal condition with no research grant. Classify all 320 seats, retain every match output, and report route-censored/no-route seats separately; those seats do not count as passes. Any mature-qualified failure, ledger residual, or incomplete 80-match block leaves I-T6 open. Do not alter source, pack, cutoffs, or seed block after observing any result.

Acceptance command pattern: `dotnet run --project src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-build -- iron-match --pack /tmp/civsim-it6-current.pack --seed 20261101 --seeds 80 --it6-accounting`.

## Held-out candidate result: futility stop, not acceptance

The first candidate at source revision `f20e9eb6f81c7862ef853b4ab1a70fc59faf42cc` was stopped once the fixed cutoffs had failed. It completed 47 of the preregistered 80 matches (188 seat reports); the completed seed IDs, including the non-contiguous outputs from parallel workers, are preserved in [the partial acceptance output](m11-it6-acceptance-futility-output-2026-11-05.txt). The remaining in-flight and queued matches were canceled. This is a failed, undersized candidate screen, not an acceptance result. It cannot close I-T6.

| Measure | Result in 47 completed matches | Reading |
|---|---:|---|
| Route exposure | 188 seats: 158 mature, 15 route-censored, 15 no Iron route | All seat reports retained; censored/no-route are not passes |
| Mature fuel cutoff | 155 pass, 3 fail; no mature fuel-censored seats | Fails on seed `20261112` Grower and seed `20261153` Grower and Sage |
| Mature rolling work | 5,280 windows across 158 mature seats; one above 15%, maximum 15.03% | Fails on seed `20261154` Builder, seasons 287–299: 448.83/2,985.67 work-seasons; metal/craft/facility 355.69/28.31/64.83 |
| Clearing episodes | 8,283 total: 564 chop, 0 pre-MET-09 metal, 0 Iron-era metal, 7,719 civic, 0 other | Zero Iron-attributed crossings in this partial cohort |
| Ledgers | Forest, local wood, combined wood, and kits: 0.00 residual in all 47 matches | Reconciles |
| Match length | 78.0–89.2 simulated minutes | Natural terminal condition; 47 matches are below the preregistered 80 |

The fuel-short hotspots show a catchment-edge miss rather than whole-map exhaustion. In seeds `20261112` and `20261153`, short settlements had depleted or nearly depleted harvestable wood inside their reach, while the next productive forest band began at distance 33—one tile beyond the 32-tile GOV-04 plus TRN-05 catchment. The 10-tile diagnostic band beyond that edge still held hundreds to more than a thousand usable litres. The approved fuel limits remain unchanged.

The work miss is a separate near-boundary failure: one mature 12-season window reached 15.03% while fuel passed. The one-window excess is 0.03 percentage points, with 64.83 work-seasons in facility construction. This remains a failure under the approved every-window rule; it is not rounded down or waived.

The first correction added two tiles to each shared Iron charcoal catchment while preserving the existing per-tile hauling charge. It passed the seed `20261112` Grower and `20261153` Sage fuel cutoffs and brought seed `20261154` Builder's maximum work window below the ceiling (14.77%). Seed `20261153` Grower still failed; the nearest productive forest was at distance 36, two tiles beyond the new 34-tile GOV-04 plus TRN-05 reach. Results are in [the two-tile correction screen](m11-it6-two-tile-margin-screen-output-2026-11-05.txt).

The four-tile follow-up passed seed `20261112` Grower and seed `20261153` Sage, but seed `20261153` Grower still failed: it had 11/365 short seasons (3.01%) but 23.52/445.41 L unmet (5.28%). Its next productive forest was at distance 37, one tile beyond the resulting 36-tile reach. Seed `20261154` Builder was under the work ceiling at 14.77%. Full output is in [the four-tile correction screen](m11-it6-four-tile-margin-screen-output-2026-11-05.txt).

That trial sequence was superseded. The current readiness candidate uses an eight-tile physical-haul margin beyond domestic sharing, a four-tile margin for hub siting and the Iron forest throttle, and a 25% haul-work reduction only in the outer margin; haul work remains distance-based everywhere. For GOV-04 plus TRN-05, this is 40 tiles of physical charcoal reach and a 36-tile planning catchment. It doubles metal-facility build capacity from KEY-4 until MET-09 to prepare the chain before the measured mature route. Recipes, the 5 L reserve, approved cutoffs, and clearing attribution are unchanged. The ramp remains reported separately, including its higher work shares.

The candidate was screened on the previous natural cohort `20261019–20261026` (diagnostic, not held out): 30 mature seats, all fuel cutoffs passed, no mature rolling work window over 15% (maximum 13.57%), zero Iron-attributed crossings, and zero forest, local-wood, combined-wood, or kit-ledger residuals. It was also rerun on the three known hotspot matches `20261112`, `20261153`, and `20261154`: all mature-qualified seats passed fuel and work, with seed `20261154` Builder's maximum at 14.73%. These outputs are preserved in [the readiness-screen output](m11-it6-preroute-build-screen-output-2026-11-05.txt). The candidate now needs a fresh held-out acceptance block; this readiness evidence does not close I-T6.

## Second held-out acceptance preregistration and result

Freeze the candidate at the source revision and content-pack SHA-256 recorded below before starting this block. Use natural-match seeds `20261201–20261280` (80 consecutive seeds; four Warlord AI archetypes per match), the fixed fuel cutoffs above, zero Iron-attributed crossings, and the every-window 15% mature work-share ceiling. Run all matches to their natural Iron chapter terminal condition with no research grant. Classify every seat; route-censored and no-route seats are reported, not counted as passes. Preserve every output. Any mature-qualified failure, ledger residual, or incomplete block leaves I-T6 open. Do not change source, pack, seed block, or approved cutoffs after the first result.

**Frozen source revision:** `c753d58f991ee0252e2600f426cf16aaa4ab7c43` (also makes read-only hotspot output ordering deterministic).

**Content-pack SHA-256:** `85f436c611870c6a34d75dc56a8624c702d2e3810a5c1ed3edd6daef05ae7ee9`.

**Acceptance command:** `dotnet run --project src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-build -- iron-match --pack /tmp/civsim-it6-acceptance.pack --seed 20261201 --seeds 80 --it6-accounting`.

The trace is read-only and excluded from simulation state. It records whole-map original forest tiles once (never summing overlapping settlement catchments), all settlement wood stores, kit output/issuance/service use, the metal work ledger, Iron ore and fire-setting, charcoal use and wood, and haul labor. World transfers of stored goods remain internal to the aggregate. Forest and local-stock residuals are calculated from fixed-point flows.

### Acceptance result: failed work-share gate, complete 80/80

All 80 preregistered matches completed on frozen source `c753d58f991ee0252e2600f426cf16aaa4ab7c43` and the recorded pack hash. The complete output is preserved in [the second acceptance output](m11-it6-acceptance-output-2026-11-05.txt). No source, pack, seed, or cutoff changed during the run.

| Measure | Result | Reading |
|---|---:|---|
| Match and seat coverage | 80/80 matches; 320 seats | Full preregistered block completed |
| Mature work-share coverage | 277 seats; 9,617 rolling 12-season windows | 15 route-exposed seats ended before a full mature rolling window; 2 late-route seats did not reach the 12-season route-maturity screen; 26 seats had no MET-09 plus issued-kit exposure |
| Mature fuel cutoffs | All 277 work-evaluable mature seats passed both fuel limits; across all route-ramp traces, 289 pass and 3 are fuel-censored | No mature-qualified fuel failure |
| Mature work share | 4 of 277 seats had a window above 15%; maximum 16.81% | **Fails I-T6.** All four occurred in the first mature rolling window after route ramp |
| Iron-attributed clearing | 0 crossings; 14,837 total crossings (843 chop, 13,994 civic, 0 metal-before-MET-09, 0 Iron-era metal, 0 other) | Approved zero-Iron-crossing allowance passes |
| Ledgers | Forest, local wood, combined wood, and kits each had 0.00 residual in all 80 matches | Reconciles |

| Seed | Civilization | Window | Chain / work | Metal / craft / facility work | Share |
|---|---|---:|---:|---:|---:|
| `20261201` | Sage | 275–287 | 428.94 / 2,552.25 | 334.49 / 20.93 / 73.52 | 16.81% |
| `20261224` | Sage | 274–286 | 582.81 / 3,705.18 | 468.25 / 19.96 / 94.60 | 15.73% |
| `20261247` | Grower | 281–293 | 470.35 / 2,999.83 | 358.71 / 25.81 / 85.82 | 15.68% |
| `20261278` | Warlord | 278–290 | 483.17 / 3,111.46 | 401.49 / 16.69 / 64.99 | 15.53% |

These four misses all start on the first mature rolling window following the route ramp. The numerator is dominated by metal-chain work; facility construction contributes 64.99–94.60 work-seasons, while kit craft contributes 16.69–25.81. Preserve the approved ramp and 15% every-window rule. A next candidate must reduce real chain/build work or increase useful output per work while retaining kit service, fuel, forest, and ledger performance. The fixed acceptance block is consumed and must not be reused as held-out evidence.

### Third held-out acceptance preregistration

The next candidate doubles pre-route metal-facility build capacity from KEY-4 until MET-09, retaining the approved catchment, haul, recipe, reserve, attribution, fuel, and work-share rules. This tests moving one-time facility construction before the mature window. It does not change the 12-season ramp or the every-window 15% ceiling.

Diagnostic reruns on the four prior work failures moved all four below 15%; the maximum was 14.74% (seed `20261247`, Sage). The eight-seed readiness cohort `20261019–20261026` had 30 mature work-evaluable seats, all passing fuel and work, with a 12.67% maximum. The three original fuel hotspots (`20261112`, `20261153`, `20261154`) had 11 mature work-evaluable seats, all passing fuel and work, with a 12.65% maximum. Across those 55 mature work-evaluable seats, all fuel cutoffs passed, no work window exceeded 15%, no Iron-attributed clearing occurred, and all forest, local-wood, combined-wood, and kit ledgers reconciled. Full diagnostic outputs are preserved in [the 2× pre-route screen output](m11-it6-2x-preroute-screen-output-2026-11-05.txt). This evidence is diagnostic only and includes seeds from the failed prior acceptance block.

Freeze this candidate at source revision `633ee090b054fd40d4adf31de0b4d5c9357ede79`, pack SHA-256 `85f436c611870c6a34d75dc56a8624c702d2e3810a5c1ed3edd6daef05ae7ee9`, and new natural-match seed block `20261301–20261380` (80 consecutive seeds; four Warlord AI archetypes per match). Use the same fixed fuel cutoffs, zero Iron-attributed crossing allowance, and every-window mature work-share ceiling. Run every match to natural Iron chapter end with no research grant, classify every seat, retain every output, and report no-route and route-censored seats separately. Any mature-qualified failure, ledger residual, or incomplete block leaves I-T6 open. Do not alter source, pack, seed block, or approved cutoffs after the first result.

**Acceptance command:** `dotnet run --project src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-build -- iron-match --pack /tmp/civsim-it6-acceptance.pack --seed 20261301 --seeds 80 --it6-accounting`.

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

**Pre-hub disposition:** the accounting ledgers reconcile exactly and the approved Iron-specific clearing allowance had zero observed crossings in this screen. The approved mature work-share screen had one observed exceedance, and the approved fuel screen failed in 12/29 mature seats plus one low-wood fixture seed. One default-reach, no-Coppicing fixture seed was fuel-censored. The eight-match sample was insufficient for final acceptance. I-T6 remained open; the approved cutoffs were fixed for the acceptance run. No production balance parameters were changed by that accounting work.

## Post-hub catchment diagnostic

The post-KEY-4 Iron fuel hub implementation is recorded in source revision `f0cdfaa` (`Add catchment-aware Iron fuel hubs`). It leaves the approved cutoffs and recipe, reserve, and clearing rules unchanged. The same natural cohort and seed block was rerun with content-pack SHA-256 `85f436c611870c6a34d75dc56a8624c702d2e3810a5c1ed3edd6daef05ae7ee9`:

```sh
dotnet run --project src/CivSim.Cli -c Release -- build-pack --out /tmp/civsim-iron-hubs.pack
dotnet run --project src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-build -- iron-match --pack /tmp/civsim-iron-hubs.pack --seed 20261019 --seeds 8 --it6-accounting
```

The full output is preserved in [the post-hub screen output](m11-it6-iron-hubs-screen-output-2026-11-05.txt).

| Measure | Post-hub result | Reading |
|---|---:|---|
| Route exposure | 32 seats: 29 mature-qualified, 1 route-censored before the 12-season maturity screen, 2 with no MET-09 plus issued-kit exposure | Diagnostic only; censored and no-route seats are not passes |
| Mature fuel cutoff | 19/29 pass, 10/29 fail | Better than the earlier same-seed screen (17/29 pass, 12/29 fail), but still fails the approved per-seat requirement. Failures cluster in seeds `20261020`, `20261023` and `20261026` |
| Mature demand aggregate | 555/14,934 eligible settlement-seasons short (3.72%); 968.41/14,480.57 L planned unmet (6.69%) | Pooled rates do not override the per-seat cutoff |
| Mature rolling work windows | 941 windows across 29 seats; maximum 12.58%; 0 above 15% | Passes this eight-seed work-share screen; the earlier screen had one window at 15.20% |
| Clearing episodes | 1,453 total: 57 chop, 1,396 civic, 0 pre-MET-09 metal, 0 post-MET-09 Iron metal, 0 other | The zero Iron-attributed crossing allowance is met in this diagnostic |
| Match length | 77.2–83.0 simulated minutes | Natural terminal condition; this remains far below the 80-match acceptance sample |

All forest, local-wood, combined wood, and kit ledgers reconciled to zero residual in each match. The mature work-share measure improved and two additional seats met both fuel limits, while ten seats still failed the fuel cutoff. Thus catchment hubs and siting improve this diagnostic but do not solve low-wood failures on every map; I-T6 remains open. This is not the acceptance run and does not authorize a cutoff change.

## Expanded domestic charcoal reach screen

The final local-access trial gives every Iron fuel consumer the existing domestic sharing radius in addition to the ordinary charcoal reach, once KEY-4 and SOC-07 or GOV-04 enable the sharing network. TRN-05 still raises the base charcoal radius from 8 to 12 and lowers haul work. The resulting reach is 23/27 tiles with SOC-07, or 28/32 with GOV-04, without TRN-05/with TRN-05. All forest beyond the normal radius-3 cutting area retains the existing per-litre, per-tile haul charge; the 5 L reserve, recipe, and approved cutoffs are unchanged. Catchment scoring and the I-T6 trace use the same effective radius.

This applies to every Iron fuel consumer, not only the assigned kit hub. In seed `20261023`, settlement c2/s36 still made local metal goods after its kit work was assigned elsewhere. The hub-only trial left its fuel reach at the normal 8 tiles. Its next 10-tile band contained 5,350.50 L of harvestable wood across 250 forest tiles, with the nearest productive tile 14 tiles away. Adding the domestic sharing radius to all fuel consumers makes those nearby stands reachable while the hub continues to concentrate kit production.

The natural cohort `20261019–20261026` was rerun with content-pack SHA-256 `85f436c611870c6a34d75dc56a8624c702d2e3810a5c1ed3edd6daef05ae7ee9`. Full output is in [the expanded-reach screen](m11-it6-iron-hubs-extended-reach-screen-output-2026-11-05.txt).

| Measure | Expanded-reach result | Reading |
|---|---:|---|
| Route exposure | 32 seats: 30 mature-qualified, 2 with no MET-09 plus issued-kit exposure; no route-censored seats | Diagnostic only; no-route seats are reported and are not counted as passes |
| Mature fuel cutoff | 30/30 pass; 0/15,528 eligible demand-seasons short and 0/14,635.50 L planned unmet | Meets both approved per-seat fuel limits in this diagnostic |
| Mature rolling work windows | 957 across 30 seats; maximum 14.34%; 0 above 15% | Passes the every-window rule in the diagnostic; the maximum is seed `20261022`, Grower, seasons 275–287 |
| Clearing episodes | 1,552 total; 0 post-MET-09 Iron-era metal-attributed crossings | Meets the zero Iron-attributed allowance in the diagnostic |
| Ledger residuals | Forest, local wood, combined wood, and kits: 0.00 in every match | Reconciles |
| Match length | 77.8–82.5 simulated minutes | Natural terminal condition; only 8 of the required 80 matches |

The earlier hub-only reach trials still failed one or more hotspot seats. Extending reach to every Iron fuel consumer cleared those misses in this cohort without changing the approved cutoffs, and all 957 mature work windows remain below 15%. This is the first local-access design ready for the predeclared acceptance run; it is not itself acceptance and does not close I-T6.

## Deferred option: cross-civilization wood-for-ore Compacts

Direct wood-for-iron-ore trade remains shelved. The completed acceptance found no mature-qualified fuel failure; its remaining failures are four work-share windows. Cross-civilization trade would add no direct remedy for those work-share misses. The current Compact good list includes Iron bloom but no Wood or Iron Ore, and a Compact carries one good. A caravan loads up to seller surplus and the buyer's next four seasons of shortfall; there is no finite cargo capacity. A trade-based I-T6 fix would first need explicit source reserves, shipment capacity and labor, delivery losses, and per-civilization wood/clearing accounting. Otherwise it could move fuel across the map without charging the harvest, haul work, or forest impact to either side.

Revisit this option if future controlled diagnostics show domestic forest access is again the binding fuel constraint, or if a domestic fuel change cannot meet the approved fuel cutoffs. Do not reopen the Compact model solely to address the current work-share failures. When revisited, compare a controlled wood-for-ore exchange against domestic hauling, with both sides' forest stock, reserves, work and deliveries reconciled under the existing cutoffs. The approved cutoffs remain fixed.

