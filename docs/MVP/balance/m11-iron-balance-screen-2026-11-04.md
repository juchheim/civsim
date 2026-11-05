# M11 provisional Iron balance screen — 2026-11-04

**Status:** preliminary screening evidence only. The screens below do not approve a target, fit a cost, or close an M11 gate. The proposed thresholds and protocols in [25 §13–14](../25-iron-age.md) remain unchanged.

**2026-11-05 follow-up:** the fuel fixture and Iron kit planner were corrected and rerun. A tile-level diagnostic showed that stable aggregate forest stock could hide individual tiles crossing the game's cleared-woodland threshold, so Iron wood cutting now has a per-tile 5 L reserve. A bounded candidate screen then shifted the aggregate Iron throttle band from 55%/75% to 50%/70%; this remains provisional, not an approved target. The updated I-T6 and route-window results below supersede the earlier I-T6 tables and route-window results.

## Reproduction record

- Balance targets: [25 §13](../25-iron-age.md#13-provisional-balance-targets-and-oracle-work); proposed gates: [25 §14](../25-iron-age.md#14-proposed-m11-acceptance-series-b4-xx).
- Content pack: `/tmp/civsim-iron-balance.pack`, SHA-256 `85f436c611870c6a34d75dc56a8624c702d2e3810a5c1ed3edd6daef05ae7ee9`.
- Whole-match mode: default Iron chapter rules; four Warlord AI seats (Builder, Grower, Warlord, Sage), no E4 grants. The match harness applies the candidate Iron cap/countdown. Simulated elapsed time is Standard 1×, not wall-clock runtime.
- The initial 80 successful match reports used source baseline `df591663c53bf1db0475545e9001dd86e02bec34`. Fourteen calibration seeds that had crashed or exited without a trace were rerun on the arithmetic fix `dfd5d92ee5df3ce98d8a91bc058cc74a8f779120`; all 14 completed. The fix changes only how an oversized wood ratio is saturated before narrowing; representable Q32.32 ratios keep the existing arithmetic. These runs combine into 78 calibration matches plus the original 16 held-out matches.
- Calibration seeds (78): `20261101–20261102, 20261106–20261108, 20261110–20261127, 20261130–20261138, 20261142, 20261146–20261150, 20261154–20261155, 20261158–20261160, 20261162, 20261181, 20261185–20261187, 20261189–20261206, 20261209–20261219, 20261221`.
- Held-out seeds (16): `20261165–20261180`.
- Per-match and per-seat output: [CSV](m11-iron-balance-screen-2026-11-04.csv). The 2026-11-05 fixture rerun used content pack SHA-256 `85f436c611870c6a34d75dc56a8624c702d2e3810a5c1ed3edd6daef05ae7ee9` and source base `76dafcc` plus the working-tree changes recorded in this follow-up. It ran eight same-seed Iron/Bronze pairs (`20261019–20261026`), 240 seasons, fixed population, with both the default 8-tile charcoal reach and TRN-05's 12-tile reach, with and without Coppicing. The final candidate run uses the 50%/70% aggregate throttle band. The harness records every crossing below 5 L for each forest tile in the charcoal catchment over the full run, in addition to reconciling forest stock, regrowth and standing-wood removals.
- Reproduce the fixture run with `dotnet run --project src/CivSim.Harness/CivSim.Harness.csproj -c Release --no-build -- iron-fuel --pack /tmp/civsim-iron-balance.pack --seasons 240 --seeds 20261019,20261020,20261021,20261022,20261023,20261024,20261025,20261026`.
- The Iron fuel guard measures its starting baseline and live stock across the charcoal catchment (8 tiles, or 12 with TRN-05), matching the actual charcoal haul path. Iron-related fuel, fire-setting, mine/camp construction and wood harvesting leave each initial forest tile at or above the game's 5 L cleared-woodland threshold. Bronze retains its E0–E3 baseline. After MET-09, the planner no longer requests new Bronze kits for adults already covered by Iron re-kit demand; existing Bronze kits can still be used until replaced. The final candidate uses a 50%/70% aggregate demand-throttle band; recipe quantities and fuel costs are unchanged.

## Whole-match pacing screen (I-T1)

| Cohort | Matches | KEY-4 seats | KEY-5 seats | Uncensored KEY-4→KEY-5 exposures | Mean / median | Range | Year-cap endings | Median chapter duration |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Calibration | 78 | 292/312 | 176/312 | 176 | 19.5 / 19.4 min | 15.7–24.0 min | 2/78 (2.6%) | 82.2 min |
| Held out | 16 | 61/64 | 35/64 | 35 | 19.7 / 19.3 min | 15.7–23.8 min | 0/16 | 82.7 min |
| Combined | 94 | 353/376 | 211/376 | 211 | 19.6 / 19.4 min | 15.7–24.0 min | 2/94 (2.1%) | 82.2 min |

The candidate cap-rate screen (≤10%) is below target in this sample. The previous 17-minute projection is about 2.6 minutes shorter than the combined mean; the acceptance band is still explicitly TBD, so this is a refit signal, not a pacing pass/fail. Held-out pacing is close to calibration.

Final seat states: 211 reached KEY-5; 142 were alive after KEY-4 without reaching KEY-5; 23 never reached KEY-4, of which 13 had zero population at match end and 10 remained alive. The report format records 490 `Captured` event rows (245 settlement-capture incidents, because the event writer records both parties). It does not identify unique seats that lost a settlement, so the requested captured-seat breakdown remains incomplete. Whole matches recorded 0 investment starts, 0 blockades, 0 tributes, and 2 league joins; natural matches do not validate those systems.

KEY-4→KEY-5 exposure by archetype (mean simulated minutes; only seats reaching both thresholds):

| Archetype | Calibration n / mean | Held-out n / mean |
|---|---:|---:|
| Builder | 44 / 18.3 | 8 / 18.6 |
| Grower | 49 / 19.4 | 10 / 19.2 |
| Warlord | 39 / 20.6 | 7 / 21.3 |
| Sage | 44 / 19.8 | 10 / 19.9 |

## I-T6 fuel and work screen

Fixture: one fixed 60-person settlement with the live E3 age mix, grain topped up each season, forge/kiln/smelter present and an iron camp site four tiles away. Population stays fixed so the Iron/Bronze comparison does not include demographic drift. There are 240 seasons per scenario; the table measures the final 120. Eight seeds (`20261019–20261026`) are paired across identical maps. The wood balance includes all forest tiles in the fixture's actual charcoal catchment: eight tiles without TRN-05 and twelve with it. All standing-wood removal categories and regrowth are traced. “T11+ at fixture 12” starts its clock before camp setup; the separate I-T2 table measures 12 full seasons after both a built camp and MET-09 are available. Its current coverage metric counts all adults, a conservative proxy for the proposed tool-worker denominator.

| Scenario | Metal work mean/max | Regrowth / all cuts (L/season) | Stock-flow residual (L/season) | Reachable wood end/start mean/min | End/mid | NoFuel fallback mean/max | T11+ at fixture12 / route12 / end |
|---|---:|---:|---:|---:|---:|---:|---:|
| Bronze reference | 1.7% / 1.9% | 5.10 / 5.10 | 0.000 | 0.99 / 0.98 | 1.00 | 0.0% / 0.0% | 0.0% / 0.0% / 0.0% |
| Iron | 2.9% / 3.1% | 5.10 / 5.10 | 0.000 | 0.99 / 0.98 | 1.00 | 0.0% / 0.0% | 73.7% / 93.3% / 100.0% |
| Iron, low wood | 2.9% / 3.9% | 5.38 / 5.32 | 0.000 | 0.78 / 0.17 | 1.01 | 0.5% / 4.2% | 63.4% / 82.1% / 90.2% |
| Iron, low wood with TRN-05 carts | 3.3% / 3.9% | 5.60 / 5.54 | 0.000 | 0.92 / 0.67 | 1.00 | 0.0% / 0.0% | 76.8% / 92.9% / 100.0% |
| Iron, low wood, no Coppicing | 2.9% / 3.5% | 3.30 / 3.26 | 0.000 | 0.78 / 0.17 | 1.01 | 0.0% / 0.0% | 60.7% / 79.5% / 89.7% |
| Iron, low wood, no Coppicing, with carts | 3.1% / 3.5% | 3.48 / 3.44 | 0.000 | 0.92 / 0.70 | 1.01 | 0.0% / 0.0% | 76.3% / 91.5% / 100.0% |
| Bronze, low wood with TRN-05 carts | 1.2% / 1.5% | 4.73 / 4.75 | 0.000 | 0.92 / 0.68 | 1.00 | 0.0% / 0.0% | 0.0% / 0.0% / 0.0% |
| Bronze, low wood, no Coppicing, with carts | 1.2% / 1.4% | 2.55 / 2.57 | 0.000 | 0.92 / 0.67 | 1.00 | 0.0% / 0.0% | 0.0% / 0.0% / 0.0% |
| Bronze, low wood | 1.2% / 1.5% | 4.69 / 4.71 | 0.000 | 0.76 / 0.00 | 1.00 | 12.5% / 100.0% | 0.0% / 0.0% / 0.0% |
| Bronze, low wood, no Coppicing | 1.1% / 1.4% | 2.50 / 2.51 | 0.000 | 0.76 / 0.00 | 1.00 | 12.5% / 100.0% | 0.0% / 0.0% / 0.0% |
| Iron, no Coppicing | 2.9% / 3.1% | 5.62 / 5.10 | 0.000 | 0.99 / 0.98 | 1.07 | 0.0% / 0.0% | 73.7% / 92.4% / 100.0% |
| Bronze, no Coppicing | 1.7% / 1.9% | 5.10 / 5.10 | 0.000 | 0.99 / 0.98 | 1.00 | 0.0% / 0.0% | 0.0% / 0.0% / 0.0% |
| Iron, import loss at midpoint | 0.9% / 0.9% | 5.10 / 5.10 | 0.000 | 0.99 / 0.98 | 1.00 | 0.0% / 0.0% | 73.7% / 93.3% / 72.3% |
| Iron, wood rich | 2.9% / 3.1% | 5.10 / 5.10 | 0.000 | 1.00 / 0.99 | 1.00 | 0.0% / 0.0% | 73.7% / 93.3% / 99.6% |
| Bronze, wood rich | 1.7% / 1.7% | 5.10 / 5.10 | 0.000 | 1.00 / 0.99 | 1.00 | 0.0% / 0.0% | 0.0% / 0.0% / 0.0% |

The documented steady-state metal-work ceiling passes every scenario: the highest maximum is 3.9%, below 15%. Mean stock-flow residual is 0.000 L/season at the report's precision. Final-half stock is flat or growing in every Iron scenario. The Iron runs have zero crossings below 5 L across all eight seeds in all tested reaches and Coppicing settings. In the matched low-wood Bronze controls, the default and cart reaches each record 93 such crossings with Coppicing and 49 without it over eight seeds. This isolates the Iron chain's effect from the ordinary wood draw in the same fixtures. “NoFuel fallback” is the settlement's reported metal-method fallback, not a raw count of individual charcoal-short seasons.

| Low-wood matched pair | Iron tile crossings below 5 L | Bronze tile crossings below 5 L | Iron minus Bronze, per seed |
|---|---:|---:|---:|
| Default reach, Coppicing | 0/8 seeds; 0 total | 8/8 seeds; 93 total | −11.63 |
| Default reach, no Coppicing | 0/8 seeds; 0 total | 8/8 seeds; 49 total | −6.13 |
| TRN-05 reach, Coppicing | 0/8 seeds; 0 total | 8/8 seeds; 93 total | −11.63 |
| TRN-05 reach, no Coppicing | 0/8 seeds; 0 total | 8/8 seeds; 49 total | −6.13 |

At default reach, WOD-04 adds 2.08 L/season of realized regrowth and 2.06 L/season of all standing cuts versus same-seed no-Coppicing Iron controls. In the cart-reach low-wood pair, it adds 2.12 L/season of regrowth and 2.10 L/season of standing cuts. In the worst cart-reach seed, route coverage rises from 75.0% to 82.1%; both variants clear the provisional 70% screen and have zero tile crossings below 5 L. Coppicing is therefore not required to prevent tile clearing or meet the cart-route screen, but it adds replacement margin. The no-Coppicing cart route still finishes at 100% kit coverage by the end of the 240-season fixture.

Compared with the same 5 L reserve at the earlier 55%/75% aggregate band, the 50%/70% candidate moves the worst cart-route coverage from 71.4% to 82.1% with Coppicing and from 64.3% to 75.0% without it. The worst default-reach Coppicing seed rises from 25.0% to 35.7%; the default-reach no-Coppicing seed remains at 25.0%. Both bands recorded zero Iron tile crossings below 5 L, and the candidate's highest metal-work share remains 3.9%.

This supports “Coppicing is optional but substantially increases sustainable wood throughput when wood is scarce.” It does not eliminate the default-reach low-wood T11+ coverage miss. The default-reach Iron-minus-Bronze comparison adds 0.61 L/season in metal standing cuts and 0.69 L/season in regrowth; the cart-reach Coppicing pair adds 0.79 and 0.87 L/season respectively. The 5 L tile reserve prevents Iron-related cuts from causing tile clearing in these fixtures. On the 50%/70% candidate aggregate band, TRN-05 plus no Coppicing meets the route screen in all eight seeds; default reach still misses in seed `20261026`. I-T6 does not specify a numeric allowance for clear-threshold crossings; the fixture count is now zero for Iron and the formal whole-match/accounting gate remains open.

**I-T6 reading:** every steady-state work-share screen passes, paired accounting closes, and no Iron tile crosses below the game's 5 L clearing threshold in the fixed eight-seed fixtures. Coppicing raises low-wood regrowth and sustainable cut flow; the 50%/70% aggregate throttle is only a provisional candidate. Default-reach low-wood settlements can still become supply-limited; with TRN-05, both Coppicing and no-Coppicing variants meet the proposed 70% route screen in this fixture. This resolves the observed tile-clearing failure in the fixture, while formal I-T6 remains open pending an approved crossing protocol and whole-match replacement/fuel/haul accounting. The earlier unpaired, variable-population table and its unsupported 50%/25% heuristic remain superseded. No recipe or fuel costs changed.

## I-T2 sustainable-route check

The route clock starts at the later of the fixture's Iron camp being built and MET-09 completing; both are available in seasons 2–3 in this seed block (MET-09 completes in season 1 for seed `20261026`). Coverage is measured after 12 complete seasons following route readiness. The table reports per-seed coverage, not the final holder ratio after 240 seasons. Since the documented target says “tool workers” and this harness counts every adult, these values are a conservative screen rather than a formal I-T2 verdict.

| Route | All-adult T11+ proxy, mean / min / max | Seeds at or above 70% | Reachable forest at checkpoint, mean / min |
|---|---:|---:|---:|
| Standard Iron | 93.3% / 82.1% / 96.4% | 8/8 | 0.86 / 0.63 |
| Iron, low wood with Coppicing | 82.1% / 35.7% / 96.4% | 7/8 | 0.73 / 0.34 |
| Iron, no Coppicing | 92.4% / 82.1% / 96.4% | 8/8 | 0.81 / 0.52 |
| Iron, low wood without Coppicing | 79.5% / 25.0% / 96.4% | 7/8 | 0.72 / 0.37 |
| Iron, low wood with TRN-05 and Coppicing | 92.9% / 82.1% / 96.4% | 8/8 | 0.87 / 0.53 |
| Iron, low wood with TRN-05, no Coppicing | 91.5% / 75.0% / 96.4% | 8/8 | 0.87 / 0.53 |
| Iron, wood rich | 93.3% / 82.1% / 96.4% | 8/8 | 0.98 / 0.97 |

Seed `20261026` remains the local low-wood outlier. At default reach it reaches 35.7% with Coppicing and 25.0% without. With TRN-05's 12-tile reach, it reaches 82.1% with Coppicing and 75.0% without, retaining 0.53 of catchment wood at the checkpoint in both cases. Across the eight seeds, TRN-05 with or without Coppicing clears the 70% screen in 8/8. Coppicing adds 2.12 L/season of low-wood cart-route regrowth and 2.10 L/season of standing cuts, providing more replacement margin while staying above the 5 L per-tile reserve. The default-reach variants remain at 7/8. Standard-route coverage passes all eight seeds with and without Coppicing. The one-time 70-kit delivery result is not used as evidence for I-T2.

## Other screens and remaining gates

- **I-T2:** the standard route reaches at least 70% all-adult T11+ coverage in all eight seeds after 12 full route-ready seasons. The default low-wood route reaches it in seven of eight; seed `20261026` reaches 35.7% with Coppicing and 25.0% without. TRN-05 with or without Coppicing reaches the screen in eight of eight on the 50%/70% candidate band. This identifies a transport path through the low-wood miss while Coppicing adds replacement margin. The all-adult proxy and fixture scope leave the proposed tool-worker I-T2 gate open; the one-time kit-delivery result is not evidence for it.
- **I-T3:** deterministic fixture confirms T12 service life is exactly 48 vs T11's 24 work-seasons. Live mixed-age wear/replacement distribution remains open.
- **I-T4:** one stipulated aqueduct/grain fixture reaches the proposed 1.4× lower bound. The required terrain set and physical mass-balance/live-mode comparison remain open.
- **I-T5:** the selected isolated siege fixture expresses the candidate 0.40 duration multiplier. No natural match began an investment, so engine-vs-investment and relief/attrition comparison remain open.
- **Geography diagnostic:** a 10,000-world audit found 45.6% of starts cannot supply replacement for 100 adults of T11 from reach-8 forest regrowth alone. Local or trade/expansion access is therefore material to I-T2/I-T6.
- **I-T7/I-T8 and B4-03/04/06–15:** not closed by these runs. In particular, no 1,000-match replay gate, performance gate, human playtest, or complete B4 acceptance protocol was run.

The 50%/70% aggregate throttle band is an experimental candidate and has not been approved as a target. It improves the TRN-05 no-Coppicing route screen without changing recipe or fuel costs and without producing a 5 L tile crossing in this fixture; default-reach low-wood route coverage still misses in seed `20261026`. The reported observations are provisional screens to inform the next oracle fit and protocol review.
