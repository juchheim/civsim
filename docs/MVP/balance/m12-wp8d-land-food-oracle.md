# M12 WP-E5-5 (F-T4): land and food oracle (updated 2026-11-14)

**Status:** The five-geography fixture screen, matched natural-start cohorts, live E4 trajectory, and synchronized whole-match observations to the 180-minute horizon are complete. An additional 80-world unscreened holdout now supplies a population-weighted estimate and provisional empirical capacity band. These remain diagnostics, not an approved acceptance result; E5 rows remain draft and no numeric cap band is approved.

## Correction

Harvested grain already used the Fief field schedule: half the plots in the two-field baseline, then two thirds with AGR-11, plus the 1.125 legume yield. The sustainable food-capacity forecast did not. It counted every field as cropped every year, overstating food capacity before KEY-5 and understating the relative AGR-11 gain. The forecast now uses the same Fief crop share and yield factor as the harvest path. The rule is neutral outside Fief and before KEY-5.

## Fixtures and results

Each row pairs the two-field baseline and AGR-11 on the same generated world (`20261019`), with a 100-person settlement and nine fields. The fixture overrides field soil to hold geography constant: non-river `0.70`, river `0.85`, hill `0.45`, inland `0.65`, coastal `0.55`. The river profile marks the field plots as river tiles with freshwater fish growth; the coastal profile gives one plot coastal fish growth. The inland profile halves local forage to represent a dry start. Other generated herd resources stay matched. Each harvested plot is fully staffed by the fixture. These are controlled geography fixtures, not a sample of natural world starts.

| Fixture | Farm work, ws/year, baseline → AGR-11 | Grain-food value/year, baseline → AGR-11 | Grain share | Sustainable food support, people, baseline → AGR-11 | Six-year grain units harvested, baseline → AGR-11 | Harvest ratio |
|---|---:|---:|---:|---:|---:|---:|
| Non-river | 4.5 → 6.0 | 61.03 → 91.55 | 17.0% | 117.2 → 126.2 | 166.32 → 243.97 | 1.467× |
| River | 4.5 → 6.0 | 74.11 → 111.17 | 19.9% | 121.4 → 132.7 | 201.96 → 297.19 | 1.472× |
| Hill | 4.5 → 6.0 | 39.23 → 58.85 | 11.6% | 110.0 → 115.5 | 106.92 → 155.28 | 1.452× |
| Inland | 4.5 → 6.0 | 56.67 → 85.01 | 21.8% | 84.6 → 92.9 | 154.44 → 226.23 | 1.465× |
| Coastal | 4.5 → 6.0 | 47.95 → 71.93 | 13.9% | 112.9 → 119.8 | 130.68 → 190.75 | 1.460× |

All five fixtures show the expected 1.5× annual grain-food contribution before soil changes. Over six harvests, soil recovery and depletion put actual grain output at 1.452–1.472×, within the existing 1.35–1.65 fixture band. The nine-field farm's annual labor demand rises from 4.5 to 6.0 work-seasons (+33%). In the direct harvest fixture, opening grain is zero and ending grain equals opening grain plus harvested grain exactly; there is no consumption, import, transfer, or unexplained residual.

The displayed population support is the simulation's sustainable food capacity at full field staffing, including the same world's forage, herds, and fish. It is not a proven population outcome: it does not test labor shortages or long-run whole-match growth. AGR-11 lifts that capacity by 5.8–10.9% across these fixtures. Grain's share of baseline food is 11.6–21.8%, so the older illustrative oracle range of 1.2–1.45× for whole-settlement food capacity was too high for these map-backed fixtures. Treat those earlier mix assumptions as superseded; no numeric target is approved by this screen. The dry inland fixture supports 84.6 people before AGR-11 and 92.9 after, below its 100-person starting population even with all nine fields fully staffed. This marks the weak-food edge for the natural-start anchor run; AGR-11 alone does not make every low-food start self-sufficient.

## Natural-start whole-match horizon screen

Four matched seed pairs (`20261019`–`20261022`) used the same temporary pack with all 50 E5 rows numeric, Fief mode, `--grant-e5`, and a 130-minute horizon. The treatment granted AGR-11 when each civilization reached KEY-5. The control withheld only AGR-11; all other E5 grants, settings, seeds, start positions and pack bytes were the same. The pack hash was `9ea23672798ab8cbd2b57b98fe8c1a6f450801fccebfe4c0665e90d7d4041a20`. Every seat reached KEY-5 and all four observations; none were censored. Matches reached the 130-minute horizon without a chapter-end event.

Each seat is measured at its first season boundary after its own KEY-5, before the E5 grants, then at +4, +8 and +12 seasons. The home row tracks the settlement on the natural start tile. City share, population and summed capacity are civilization-wide. Ratios are AGR-11/control; city-share differences are percentage points. These are 16 seat pairs across eight mirror-pair locations, not 16 independent geographic starts.

| Offset after own KEY-5 | Home FoodCap ratio mean / median | Civilization FoodCap ratio mean / median | Civilization sustainable capacity ratio mean / median | Civilization population ratio mean / median | City-share change mean / median |
|---:|---:|---:|---:|---:|---:|
| 0 seasons | 1.028 / 1.000 | 1.003 / 1.000 | 0.998 / 1.000 | 0.999 / 1.000 | −0.039 / 0.000 pp |
| 4 seasons | 1.058 / 1.061 | 1.109 / 1.110 | 1.108 / 1.107 | 1.002 / 1.003 | +0.107 / +0.030 pp |
| 8 seasons | 1.044 / 1.055 | 1.103 / 1.093 | 1.104 / 1.097 | 1.009 / 1.008 | +1.946 / +0.165 pp |
| 12 seasons | 1.072 / 1.052 | 1.109 / 1.108 | 1.109 / 1.114 | 1.012 / 1.015 | +1.137 / +0.255 pp |

Start descriptors use overlapping local cohorts: river/lake access within radius 3; coastal at coast distance ≤8, inland at 9–14, and deep inland at ≥15; Mountain/Peak within radius 6 marks hill. The 10,000-world geography audit found a maximum start coast distance of 20, so the earlier F-T4 cutoff of >20 classified no deep-inland start in that audit. Under the corrected bands, the four-seed screen has 16/16 river-access starts, 10 inland, four deep inland, two coastal, four hill and 12 no-hill starts. It still has no no-river start.

The runs share the same generated-world fingerprint and match-start checksum in all four seed pairs. At the individual KEY-5 anchor, however, only 4/16 seat checksums matched exactly: `20261019/c0`, `20261020/c0`, `20261021/c0`, and `20261022/c3`. Earlier AGR-11 grants in the treatment arm had already changed the whole-match state for the other seats. On that strict identical-state subset, +12-season mean/median ratios were 1.068/1.062 for home FoodCap, 1.114/1.120 for civilization FoodCap, and 1.118/1.118 for civilization sustainable capacity; city share changed +0.657/+0.275 pp. Those four anchors were all inland and included only one hill start. Treat the 16-seat table as a seed-paired whole-match diagnostic and the four-seat subset as a small exact-state check, not as a fitted cap or a pass/fail result. The raw manifests, start descriptors, checksums and samples are in [m12-wp8d-land-food-anchor-output-2026-11-13.txt](m12-wp8d-land-food-anchor-output-2026-11-13.txt); [fief_land_anchor_audit.py](tools/fief_land_anchor_audit.py) reproduces the summaries.

These whole-match values are sustainable FoodCap/income and observed population/tier readouts; grain is not held as a separate physical store in the live economy, so this screen does not claim a whole-match grain mass balance. Physical harvested-grain conservation is established only in the controlled fixtures above. In this first screen, the natural-start civilization FoodCap ratio is near the fixture-derived 5.8–10.9% capacity gain. The focused follow-up below resolves the exact-state sample limitation for one archetype, but geography coverage and broader whole-match evidence remain too small to fit an oracle band.

## Focused exact-state natural-start follow-up

The follow-up used eight matched seed pairs (`20261019`–`20261026`) on the same promoted temporary pack and 130-minute horizon. Each run focused on Builder, civilization 0. In treatment, every seat received the other E5 grants, while AGR-11 was granted only to c0; in control, all seats received the other E5 grants and none received AGR-11. All other settings, map seeds, starts, pack bytes, and non-AGR-11 E5 grants matched. The raw focus-seat rows are in [m12-wp8d-land-focused-anchor-output-2026-11-13.txt](m12-wp8d-land-focused-anchor-output-2026-11-13.txt); the earlier all-seat screen remains in [m12-wp8d-land-food-anchor-output-2026-11-13.txt](m12-wp8d-land-food-anchor-output-2026-11-13.txt).

The offset-0 capture occurs at the first season boundary where c0 has KEY-5, before the harness submits E5 grants. All eight treatment/control pairs had identical initial match checksums and identical whole-match state checksums at c0's offset-0 anchor. All eight c0s produced observations at offsets 0, 4, 8 and 12; no focused seat-arm was censored. This resolves the earlier screen's 4/16 exact-anchor limitation for a focused, single-archetype cohort. It does not supply eight independent civilization archetypes: every measured seat is a Builder, one per generated world.

| Offset after c0 KEY-5 | Home FoodCap ratio mean / median | Home sustainable capacity ratio mean / median | Civilization FoodCap ratio mean / median | Civilization sustainable capacity ratio mean / median | Civilization population ratio mean / median | City-share change mean / median |
|---:|---:|---:|---:|---:|---:|---:|
| 0 seasons | 1.000 / 1.000 | 1.000 / 1.000 | 1.000 / 1.000 | 1.000 / 1.000 | 1.000 / 1.000 | 0.000 / 0.000 pp |
| 4 seasons | 1.066 / 1.065 | 1.063 / 1.062 | 1.107 / 1.109 | 1.108 / 1.112 | 1.002 / 1.002 | −0.894 / −0.010 pp |
| 8 seasons | 1.040 / 1.062 | 1.047 / 1.060 | 1.107 / 1.115 | 1.110 / 1.113 | 1.010 / 1.010 | −1.436 / −0.050 pp |
| 12 seasons | 1.066 / 1.060 | 1.066 / 1.066 | 1.107 / 1.104 | 1.115 / 1.116 | 1.017 / 1.018 | −0.504 / +0.205 pp |


With the corrected bands, the eight c0 starts cover 8/8 river-access locations: two coastal, five inland and one deep inland, with four hill and four no-hill starts. This cohort still has no no-river location. Ratios are treatment/control; city-share differences are percentage points. Reported capacities remain sustainable model readouts, not physical grain stocks. The +12-season civilization sustainable-capacity mean/median is +11.5%/+11.6%; population is +1.7%/+1.8%. City-share movement varies substantially by seed, so no city concentration conclusion is drawn. These observations remain diagnostic and do not approve a numeric target.

## Stratified held-out follow-up

The held-out scan examined seeds `20261101`–`20262100` and selected four new seed/seat pairs, one for each archetype. Each selected start has no river or lake within radius 3 and is 15–16 coast tiles away (the reachable deep-inland band): `20261237/c0` Builder, `20261376/c1` Grower, `20261882/c2` Warlord, and `20261515/c3` Sage. Each treatment granted AGR-11 only to the measured seat; the matched control withheld it. Both arms granted all other E5 projects to all seats. The matches used the same temporary pack and 130-minute horizon. All four pairs had matching world fingerprints and initial match checksums, identical offset-0 state checksums, all four offsets, and no censored focus seats.

The [expanded raw output](m12-wp8d-land-expanded-anchor-output-2026-11-14.txt) combines these four pairs with the eight Builder pairs above and reproduces a 12-seat exact-state cohort. It covers four no-river starts and five deep-inland starts total (the fifth is the river-access Builder start from seed `20261024`); two starts are coastal and five inland. Hill and no-hill each have six observations. Builder accounts for nine of 12 pairs; Grower, Warlord and Sage each have one, so this is targeted coverage rather than a representative archetype mix.

| Offset after each focus seat's KEY-5 | Home FoodCap ratio mean / median | Home sustainable capacity ratio mean / median | Civilization FoodCap ratio mean / median | Civilization sustainable capacity ratio mean / median | Civilization population ratio mean / median | City-share change mean / median |
|---:|---:|---:|---:|---:|---:|---:|
| 0 seasons | 1.000 / 1.000 | 1.000 / 1.000 | 1.000 / 1.000 | 1.000 / 1.000 | 1.000 / 1.000 | 0.000 / 0.000 pp |
| 4 seasons | 1.058 / 1.063 | 1.060 / 1.062 | 1.104 / 1.109 | 1.107 / 1.112 | 1.002 / 1.002 | −0.578 / −0.010 pp |
| 8 seasons | 1.040 / 1.062 | 1.048 / 1.060 | 1.101 / 1.104 | 1.105 / 1.107 | 1.010 / 1.009 | −0.925 / +0.055 pp |
| 12 seasons | 1.059 / 1.060 | 1.060 / 1.066 | 1.104 / 1.104 | 1.112 / 1.116 | 1.016 / 1.018 | +0.427 / +0.205 pp |

At +12 seasons, civilization sustainable capacity is +11.2% on average and +11.6% at the median; population is +1.6%/+1.8%. These remain model readouts and short-horizon outcomes. The four newly selected no-river/deep-inland seats are only one observation per archetype and were selected for their geography, so they cannot establish a broad cohort effect or a fitted oracle band.

## Balanced held-out cohort and live E4 trajectory

The exact-state cohort now has 36 matched focus-seat pairs, nine per archetype. It combines the nine Builder anchors above with eight new Grower, eight new Warlord, and eight new Sage pairs, in addition to the prior one-per-archetype stratified anchors. New seats were chosen from a separate 1,000-seed geography scan before their match outcomes were run. Each added archetype has three coastal, four inland, and one deep-inland river-access start, split four hill/four no-hill. Across the 36 measured starts, coverage is 32 river-access and four no-river; 11 coastal, 17 inland and eight deep inland; 18 hill and 18 no-hill. This balances archetypes and deliberately covers rare geography; it is not population-weighted, and deep inland is overrepresented.

The 24 new treatment/control pairs used the same promoted temporary pack (`9ea23672798ab8cbd2b57b98fe8c1a6f450801fccebfe4c0665e90d7d4041a20`), Fief mode, 130-minute horizon, and non-AGR-11 E5 grants. Treatment granted AGR-11 only to the measured seat; control withheld it. All 24 pairs matched on world fingerprint, initial match checksum, and offset-0 whole-match checksum. Together with the previous 12 pairs, all 36 anchors produced offsets 0, 4, 8 and 12 with no censored focus seats. The new cohort's raw rows and seed list are in [m12-wp8d-balanced-anchor-output-2026-11-14.txt](m12-wp8d-balanced-anchor-output-2026-11-14.txt).

The run now records each focused seat's live E4 trajectory from the first season boundary after KEY-4, every four seasons, stopping before the first boundary where KEY-5 is complete. The 36 anchors provide 745 unique pre-KEY-5 snapshots. For the 24 new matched pairs, treatment and control checksums match at all 499 paired trajectory snapshots. The other 12 anchors were replayed in one arm to add their missing E4 traces; those rows are in [m12-wp8d-e4-trajectory-replay-output-2026-11-14.txt](m12-wp8d-e4-trajectory-replay-output-2026-11-14.txt). The first-observation boundary interval from KEY-4 to KEY-5 averaged 81.6 seasons (20.4 simulation minutes), with a median of 80 seasons (20.0 minutes), across all 36 anchors; the range was 68–97 seasons.

| Seasons after first post-KEY-4 boundary | Focused anchors | Mean civilization population | Mean / median population ÷ sustainable food capacity |
|---:|---:|---:|---:|
| 0 | 36 | 712 | 34.1% / 34.0% |
| 24 | 36 | 948 | 35.2% / 32.6% |
| 48 | 36 | 1,226 | 42.5% / 39.6% |
| 72 | 32 | 1,531 | 52.2% / 50.1% |
| 80 | 17 | 1,527 | 54.4% / 50.1% |

At +12 seasons after KEY-5, the 36-pair AGR-11 comparison gives mean/median ratios of 1.036/1.044 for home FoodCap, 1.046/1.050 for home sustainable capacity, 1.096/1.099 for civilization FoodCap, 1.103/1.108 for civilization sustainable capacity, and 1.016/1.016 for civilization population. City share changes +0.357/+0.285 percentage points. The support readout rises about 10–11% while realized population moves about 1.6% over this short window. In the preceding live E4 trajectory, median population reaches only about half of modeled sustainable capacity by +72 seasons. Treat sustainable capacity as a model readout, not a prediction of realized population or a grain stock; the comparison remains diagnostic and does not fit a cap band.

| Archetype (n=9 each) | Civilization sustainable-capacity ratio mean / median | Civilization population ratio mean / median |
|---|---:|---:|
| Builder | 1.117 / 1.117 | 1.016 / 1.017 |
| Grower | 1.111 / 1.107 | 1.014 / 1.014 |
| Warlord | 1.103 / 1.098 | 1.014 / 1.013 |
| Sage | 1.083 / 1.098 | 1.020 / 1.020 |

## Long-horizon synchronized whole-match follow-up

All 36 matched focus-seat pairs were replayed to the 180-minute cap (720 seasons) on the promoted temporary pack with hash `9ea23672798ab8cbd2b57b98fe8c1a6f450801fccebfe4c0665e90d7d4041a20`. The sample remains nine anchors per archetype and deliberately geography-stratified. Treatment granted AGR-11 only to the focused seat; control withheld it from every seat. Both arms gave all seats the other E5 grants. All 36 pairs match on initial match checksum and on the synchronized offset-0 whole-match checksum. At each fixed offset after the focused seat's KEY-5, the harness sampled all four civilizations together; it also recorded every civilization at the 180-minute horizon.

| Checkpoint after focus KEY-5 | Matched pairs | Whole-match population ratio mean / median | Whole-match sustainable-capacity ratio mean / median | City-share change mean / median (pp) | Focus / rival population ratio mean |
|---:|---:|---:|---:|---:|---:|
| +12 seasons | 36 | 1.004 / 1.005 | 1.029 / 1.031 | −0.049 / +0.088 | 1.016 / 1.001 |
| +48 seasons | 36 | 0.999 / 0.998 | 0.996 / 0.998 | −0.696 / −0.795 | 0.992 / 1.001 |
| +240 seasons (60 min) | 36 | 1.000 / 0.997 | 0.996 / 0.995 | +0.051 / +0.058 | 1.001 / 1.000 |
| +320 seasons (80 min) | 31 | 0.999 / 1.000 | 0.996 / 0.996 | −0.685 / −0.528 | 0.998 / 1.000 |
| 180-minute horizon | 36 | 0.998 / 1.000 | 0.998 / 0.998 | −0.234 / +0.102 | 0.991 / — |

Ratios are AGR-11 treatment divided by its matched control; city-share values are percentage-point differences. At the full +240-season checkpoint, the average whole-match population and capacity remain within 0.4% of control, and both focused and rival population ratios are near 1. At the horizon, the mean whole-match population and sustainable-capacity ratios are both 0.998. The sample therefore shows no broad whole-match population or capacity drift from this focused AGR-11 grant through 180 minutes. This is a matched diagnostic, not a fitted food-cap band or population-weighted estimate.

All pairs have +240 and horizon-end rows. Five pairs lack the +320 snapshot because their focused KEY-5 anchor was too late for an 80-minute follow-up before the run cap (10 arm captures had at least one censored offset). The final rows cover all 288 civilization-arms, with 284 KEY-6 completions; no match ended before the 180-minute cap. This is consistent with the confirmed no-rival-countdown rule. The raw rows are in [m12-wp8d-long-horizon-output-2026-11-14.txt](m12-wp8d-long-horizon-output-2026-11-14.txt), and [fief_land_anchor_audit.py](tools/fief_land_anchor_audit.py) reports the synchronized trajectory and censorship.

## Population-weighted natural-start holdout and fitted band

The independent holdout selected 80 distinct seed worlds without replacement from `20270001..20280000` using selection seed `20261114`. One focus seat was drawn uniformly from the four generated seats in each world; no geography or match-outcome screening was used. The selected worlds span seeds `20270403..20279994`. Each world was replayed for 130 minutes in paired arms on the same promoted temporary pack (`9ea23672798ab8cbd2b57b98fe8c1a6f450801fccebfe4c0665e90d7d4041a20`): treatment grants AGR-11 only to the selected seat, while control withholds it; all other E5 grants match. The [seat list](m12-wp8d-population-heldout-seats-2026-11-14.tsv) and [raw manifests and samples](m12-wp8d-population-heldout-output-2026-11-14.txt) preserve the cohort.

The estimator uses each control civilization's population at the matched offset-0 KEY-5 anchor as its weight, before AGR-11. The primary outcome is the selected civilization's sustainable-capacity ratio (AGR-11/control) at +12 seasons. Manifests match on pack hash, world fingerprint, start checksum and selected seat; complete pairs also match on the offset-0 focused state and tick, and on the +12 tick. The fitter validates the full frozen seed/seat list, both manifests per selected world, the selected seat, matched sample identity and the exact +12-season tick offset; missing samples must have a matching censor record. Seventy-nine of 80 pairs have a focused KEY-5 anchor and +12 sample. Seed `20277457/c2` (Warlord) did not reach KEY-5 by the 130-minute horizon in either arm, so it is reported as censored and excluded without imputation. The completed pairs comprise 18 Builders, 20 Growers, 19 Sages and 22 Warlords.

| +12-season selected-civilization outcome | Result |
|---|---:|
| Complete matched seats | 79 / 80 |
| Unweighted sustainable-capacity ratio, mean / median | 1.1094 / 1.1086 |
| Population-weighted sustainable-capacity ratio | 1.1111 (+11.11%) |
| Seed-bootstrap 95% interval for weighted mean | 1.1067–1.1155 (+10.67% to +11.55%) |
| Population-weighted empirical 5th–95th percentile band | 1.0805–1.1401 (+8.05% to +14.01%) |
| Population-weighted FoodCap ratio | 1.1027 (+10.27%) |
| Population-weighted empirical 5th–95th percentile band for civilization FoodCap ratio | 1.0672–1.1297 (+6.72% to +12.97%) |
| Population-weighted realized population ratio | 1.0180 (+1.80%) |
| Weighted realized-population / sustainable-capacity coverage, control → treatment | 68.87% → 63.12% |
| Sensitivity: capacity ratio weighted by control population at +12 | 1.1113 (+11.13%); band 1.0805–1.1401 |

The main fitted band is the weighted central 90% of observed complete-seat sustainable-capacity ratios; the literal civilization FoodCap ratio has its own weighted band shown above. Neither band is a confidence interval, gameplay tolerance, physical-grain estimate, or approved pass/fail threshold. The bootstrap interval describes uncertainty in the weighted sustainable-capacity mean across sampled seed worlds. The +12 result is an early capacity response: realized population is only 1.8% higher, and the weighted realized-population/capacity ratio falls from 68.87% to 63.12%. It must not be read as an expected population increase or as stored grain. The 79 complete seats include 70 river-access and nine no-river starts, 30 coastal, 43 inland and six deep-inland starts; these tags overlap. They are naturally sampled, but this single holdout does not authorize promoting the E5 row or selecting a cap acceptance threshold.

Use [make_fief_land_population_holdout.py](tools/make_fief_land_population_holdout.py) to reproduce the seed/seat selection, [run_fief_land_long_horizon.py](tools/run_fief_land_long_horizon.py) to replay it, and [fief_land_population_weighted_fit.py](tools/fief_land_population_weighted_fit.py) to reproduce the weighted estimate, bootstrap interval and band. [fief_land_anchor_audit.py](tools/fief_land_anchor_audit.py) checks the paired natural-start observations and their censorship.

## Reproduction and remaining gate

`FiefLandTests.Fief_food_oracle_covers_five_geographies_and_conserves_harvested_grain` checks the crop schedule, 1.5× grain contribution, sustainable population capacity, 4:3 labor cost, six-year grain output, and closed grain-store balance for all five profiles. Run it with:

```bash
dotnet test src/CivSim.Tests/CivSim.Tests.csproj -c Release --filter FullyQualifiedName~FiefLandTests
python3 docs/MVP/balance/tools/fief_land_anchor_audit.py \
  docs/MVP/balance/m12-wp8d-land-expanded-anchor-output-2026-11-14.txt \
  docs/MVP/balance/m12-wp8d-balanced-anchor-output-2026-11-14.txt \
  docs/MVP/balance/m12-wp8d-e4-trajectory-replay-output-2026-11-14.txt
python3 docs/MVP/balance/tools/run_fief_land_long_horizon.py \
  --pack /tmp/civsim-e5-long-promoted.pack --minutes 180 --workers 9 \
  --output docs/MVP/balance/m12-wp8d-long-horizon-output-2026-11-14.txt \
  docs/MVP/balance/m12-wp8d-land-expanded-anchor-output-2026-11-14.txt \
  docs/MVP/balance/m12-wp8d-balanced-anchor-output-2026-11-14.txt
python3 docs/MVP/balance/tools/fief_land_anchor_audit.py \
  docs/MVP/balance/m12-wp8d-long-horizon-output-2026-11-14.txt
python3 docs/MVP/balance/tools/make_fief_land_population_holdout.py \
  --output docs/MVP/balance/m12-wp8d-population-heldout-seats-2026-11-14.tsv
python3 docs/MVP/balance/tools/run_fief_land_long_horizon.py \
  --pack /tmp/civsim-e5-long-promoted.pack --minutes 130 --workers 9 \
  --output docs/MVP/balance/m12-wp8d-population-heldout-output-2026-11-14.txt \
  docs/MVP/balance/m12-wp8d-population-heldout-seats-2026-11-14.tsv
python3 docs/MVP/balance/tools/fief_land_population_weighted_fit.py \
  docs/MVP/balance/m12-wp8d-population-heldout-output-2026-11-14.txt \
  --seats docs/MVP/balance/m12-wp8d-population-heldout-seats-2026-11-14.tsv
```

The long-horizon command expects the temporary test pack with all 50 E5 rows promoted; the pack is kept outside the repository and its hash is recorded in every manifest. This run did not change the content rows from `draft`.

F-T4 now has matched natural-start and long-horizon diagnostics plus an independent 80-world population-weighted holdout. At +12 seasons, the 79 complete held-out pairs estimate +11.11% weighted sustainable capacity (seed-bootstrap 95% interval +10.67% to +11.55%); the provisional weighted 5th–95th percentile band is +8.05% to +14.01%. One Warlord seat never reached KEY-5 by 130 minutes and is censored. E4 sign-off is complete, but the band is not an approved E5 acceptance threshold: the content row stays draft pending WP-E5-11 gates, and sustainable capacity is a model readout rather than a grain stock or realized-population forecast. The original F-T2 screen had nine low-coverage windows; the sensitivity rerun had four complete misses, each above 60% under a virtual extra-mill calculation. That remains capacity-only, with buildability and physical flour production unmeasured. F-T3 still needs broader treatment of its low-demand censored seeds. None of these results promotes E5 content.
