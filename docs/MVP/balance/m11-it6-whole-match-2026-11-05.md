# M11 I-T6 whole-match accounting and clearing approval protocol

**Status:** whole-match accounting implementation and 8-seed screen complete; the I-T6 targets and approval protocol remain proposed and unapproved. This report does not close B4-06.

I-T6 in [25 §13](../25-iron-age.md#13-provisional-balance-targets-and-oracle-work) proposes that sustainable metal use no more than 15% of civilian work in steady state and require no mandatory clear-cut. It requires full replacement, fuel and haul accounting, including import-loss and low-wood stresses. The game already defines a clearing transition: an original forest tile that falls below 5 L becomes cleared woodland ([23 §7.4](../23-e3-bronze-specs.md#74-forest-stock-clearing-and-regrowth)). This document defines an approval-ready measurement and records which decisions still need explicit approval.

## Reproduction

- Source baseline: `d2770d2` (Iron route access and age coverage); accounting implementation revision: `1513a70`.
- Content pack SHA-256: `85f436c611870c6a34d75dc56a8624c702d2e3810a5c1ed3edd6daef05ae7ee9`.
- Natural Iron cohort: four Warlord AI seats (Builder, Grower, Warlord, Sage), default Iron chapter limits, no research grant, played to chapter end. The current screen uses seeds `20261019–20261026`.
- Build the current-content pack with `dotnet run --project src/CivSim.Cli -c Release --no-restore -- build-pack --out /tmp/civsim-it6-current.pack`.
- Run the accounting with `dotnet run --project src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-build -- iron-match --pack /tmp/civsim-it6-current.pack --seed 20261019 --seeds 8 --it6-accounting`.
- A Bronze comparison screen was also run on the same seeds with `--minutes 82 --it6-accounting --bronze-control`. Those Bronze matches reached their normal terminal condition after 59.0–64.0 simulated minutes, earlier than their Iron counterparts; do not use their whole-match clearing totals as duration-matched causal comparisons.

The trace is read-only and excluded from simulation state. It records whole-map original forest tiles once (never summing overlapping settlement catchments), all settlement wood stores, kit output/issuance/service use, the metal work ledger, Iron ore and fire-setting, charcoal use and wood, and haul labor. World transfers of stored goods remain internal to the aggregate. Forest and local-stock residuals are calculated from fixed-point flows.

## Accounting definitions

### Work share

For each civilization, the full-chain numerator is recorded mine, charcoal, smelt and camp labor (including the haul work those jobs spend), plus actual T11/T12 kit recipe labor and metal-facility construction labor. The denominator is civilian work recorded by the metal ledger over the same interval. Startup construction is shown separately through the match totals.

The proposed mature-window protocol starts at the first season with MET-09 complete and an Iron kit issued, allows 12 complete seasons for the route to mature, then evaluates every rolling 12-season window through match end. This uses the documented 12-season route window as the proposed operational meaning of “steady state”; approval is still required. The 12-season ramp is reported and is not silently folded into the mature-window pass/fail result.

### Replacement and fuel

T11/T12 kits made, issued to adults, held at match end, and still in stores are reported separately. Tool-work by tier, kit breaks and healthy T11-to-T12 upgrades show how much service those issues actually delivered. A short match with few complete wear cycles is an incomplete live replacement sample even if kit inventories reconcile.

The report counts charcoal produced, total charcoal wood, fire-setting wood, charcoal inputs to the Iron ore/bloom/wrought/steel/tool recipes, Iron ore mined, and charcoal/ore haul work. It also counts settlement-seasons in which the metal chain could not meet its planned charcoal burn, separately for all metal work and seasons after MET-09. The proposal sets no numeric fuel-shortage allowance; shortage frequency is reported as evidence for user review, not relabeled as a pass or fail after observing it.

### Clearing event and attribution

Count a clearing episode when a tile that began as forest crosses from at least 5 L of standing wood to below 5 L. If regrowth restores a cleared tile to forest and it crosses again later, count the later episode too. Report the number of original forest tiles still cleared and the number that reached grassland at match end.

The last standing-wood removal immediately causing each crossing is classified as chop, metal before MET-09, post-MET-09 Iron-era metal, civic, or other. “Iron-era metal” conservatively includes metal-chain standing-wood removals after MET-09; it is a proximate cause label, not a claim that earlier users did not contribute to a tile’s depletion. All-cause civic/chop clearing is reported separately and is not automatically attributed to the Iron chain.

## Proposed approval and clearing protocol

1. Freeze the event definition before the gate run: count every original forest tile transition from at least 5 L to below 5 L; count a later transition again if regrowth restores forest and it is cleared again. Keep the game's 5 L threshold unchanged. Do not tune the threshold, Iron wood throttle, recipe inputs, or reserve after observing a miss; any target change requires a separate approval.
2. **Proposed clearing allowance:** zero post-MET-09 Iron-era metal-attributed crossings in every qualifying Iron match and stress fixture. Attribution follows the last standing-wood removal that caused the crossing. This is a conservative proximate-cause label, not proof that earlier users made no contribution to depletion. Civic, household chop, pre-MET-09 metal, and other causes remain in the report but do not count against this proposed Iron-specific allowance.
3. Run paired low-wood stresses with and without Coppicing and with import loss. Coppicing may add replacement margin but is not a prerequisite for the proposed zero-crossing rule. The existing fixed-pop screen reports zero Iron crossings in its eight-seed scenarios, including low wood without Coppicing and import loss; see the [preliminary balance screen](m11-iron-balance-screen-2026-11-04.md#i-t6-fuel-and-work-screen).
4. For natural whole matches, predeclare at least 80 seeds (or a justified number of qualifying route exposures under 25 §13), hold out a seed block, and classify every seat as mature-qualified, route-censored, or no Iron route. The proposed work-share test is **≤15% in every complete rolling 12-season window**, beginning only after MET-09 and the first Iron-kit issue plus 12 complete seasons. Record the route ramp separately. Report charcoal-short settlement-seasons, kit service coverage, replacements and clearings by cause; do not let one measure substitute for another.
5. Any claim about *excess total* clearing requires same-seed Bronze and Iron runs on the same predeclared season horizon, with the natural terminal condition prevented from shortening one arm. Natural Bronze runs that terminate earlier are not duration-matched controls. The Iron-specific zero-crossing decision in step 2 is reported independently of this total-clearing comparison.
6. Publish an immutable approval record with the accepted event definition and allowance, the work-share window rule, fuel-shortage interpretation, seed blocks, content hash, qualifying and censored seat counts, and source revision. A nonzero Iron-attributed event, a work-share window above 15%, an unbalanced ledger, or an undersized sample leaves the gate open. A failure does not authorize an in-run threshold change.

**Approval still needed:** accept or revise the clearing event definition and zero allowance, and confirm whether the work-share ceiling is evaluated on every mature 12-season window as proposed. I-T6 defines no numerical fuel-shortage allowance. Either set one before the final gate run or explicitly keep shortage counts diagnostic-only; until then they cannot be called a pass or fail.

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
| Fuel-short settlement-seasons | 1,154 across all metal work; 1,020 after MET-09; maximum 235 post-MET-09 in one seat | **Open diagnostic:** the requirement has no approved numerical allowance. This cannot be labeled a pass |

The per-match trace also records Iron recipe charcoal, charcoal wood, fire-setting wood, Iron/all-ore mined, charcoal/ore haul work, facility construction, kit craft work, and tool service work. The 8-seed fixture screen complements these natural matches: it reports zero Iron-attributed crossings across its low-wood, no-Coppicing, and import-loss scenarios. Coppicing remains optional for the proposed clearing rule and increases low-wood replacement margin; these runs do not prove whole-match sustainability.

Same-seed Bronze whole matches were capped at 82 minutes but ended naturally after 59.0–64.0 simulated minutes, before the Iron matches. Their total clearing counts are therefore not valid equal-duration controls. No conclusion about Iron *excess total* clearing is drawn from that comparison.

**Disposition:** the accounting ledgers reconcile exactly and the proposed Iron-specific clearing allowance had zero observed crossings in this screen. The mature work-share screen has one observed exceedance, fuel shortages are substantial but unthresholded, and the route exposure/sample is insufficient for approval. I-T6 remains open. No production balance parameters were changed by this accounting work.

