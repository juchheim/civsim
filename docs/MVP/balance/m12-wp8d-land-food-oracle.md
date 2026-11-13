# M12 WP-E5-5 (F-T4): land and food oracle (2026-11-13)

**Status:** The simulation-backed five-geography fixture screen is complete. E5 rows remain draft. The separate whole-match, matched-anchor balance sample is still open.

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

## Reproduction and remaining gate

`FiefLandTests.Fief_food_oracle_covers_five_geographies_and_conserves_harvested_grain` checks the crop schedule, 1.5× grain contribution, sustainable population capacity, 4:3 labor cost, six-year grain output, and closed grain-store balance for all five profiles. Run it with:

```bash
dotnet test src/CivSim.Tests/CivSim.Tests.csproj -c Release --filter FullyQualifiedName~FiefLandTests
```

F-T4 still needs whole-match matched anchors over natural starts, with the existing food-cap oracle re-anchored to a live E4 trajectory. F-T2's nine low-coverage town windows also remain for follow-up; F-T3 needs the censored fuel-demand cases retained in a broader run. None of these fixture results promotes E5 content.
