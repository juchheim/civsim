# M12 WP-E5-5 (F-T4): land and food oracle (2026-11-13)

**Status:** The five-geography fixture screen, the first four-seed natural-start whole-match screen, and an eight-seed focused exact-state follow-up are complete. These are diagnostics, not a fitted acceptance result; E5 rows remain draft and no numeric cap band is approved.

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

Start descriptors use overlapping local cohorts: river/lake access within radius 3, coast distance ≤8 for coastal and 9–20 for inland, and Mountain/Peak within radius 6 for hill. The sample had 16/16 starts with river/lake access, 14 inland, two coastal, four hill, and no deep-inland or no-river starts. Thus it does not re-test the non-river or deep-inland fixture edge on natural starts.

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

The c0 starts cover 8/8 river-access locations, six inland and two coastal, with four hill and four no-hill starts. The cohort still has no no-river or deep-inland location. Ratios are treatment/control; city-share differences are percentage points. Reported capacities remain sustainable model readouts, not physical grain stocks. The +12-season civilization sustainable-capacity mean/median is +11.5%/+11.6%; the population mean/median is +1.7%/+1.8%. City-share movement varies substantially by seed, so no city concentration conclusion is drawn. These observations remain diagnostic and do not approve a numeric target.

## Reproduction and remaining gate

`FiefLandTests.Fief_food_oracle_covers_five_geographies_and_conserves_harvested_grain` checks the crop schedule, 1.5× grain contribution, sustainable population capacity, 4:3 labor cost, six-year grain output, and closed grain-store balance for all five profiles. Run it with:

```bash
dotnet test src/CivSim.Tests/CivSim.Tests.csproj -c Release --filter FullyQualifiedName~FiefLandTests
```

F-T4 now has eight focused exact-state Builder anchors, but needs held-out starts covering other archetypes, no-river and deep-inland geography before its food-cap oracle can be re-anchored to a live E4 trajectory; broader whole-match balance is also still required. F-T2's nine low-coverage town windows remain for follow-up; F-T3 needs the censored fuel-demand cases retained in a broader run. None of these results promotes E5 content.
