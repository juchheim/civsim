# M11 provisional Iron balance screen — 2026-11-04

**Status:** preliminary screening evidence only. The screens below do not approve a target, fit a cost, or close an M11 gate. The proposed thresholds and protocols in [25 §13–14](../25-iron-age.md) remain unchanged.

**2026-11-05 follow-up:** the fuel fixture and Iron kit planner were corrected and rerun. The updated I-T6 and route-window results below supersede the earlier I-T6 tables and the prior statement that I-T2 was not established.

## Reproduction record

- Balance targets: [25 §13](../25-iron-age.md#13-provisional-balance-targets-and-oracle-work); proposed gates: [25 §14](../25-iron-age.md#14-proposed-m11-acceptance-series-b4-xx).
- Content pack: `/tmp/civsim-iron-balance.pack`, SHA-256 `85f436c611870c6a34d75dc56a8624c702d2e3810a5c1ed3edd6daef05ae7ee9`.
- Whole-match mode: default Iron chapter rules; four Warlord AI seats (Builder, Grower, Warlord, Sage), no E4 grants. The match harness applies the candidate Iron cap/countdown. Simulated elapsed time is Standard 1×, not wall-clock runtime.
- The initial 80 successful match reports used source baseline `df591663c53bf1db0475545e9001dd86e02bec34`. Fourteen calibration seeds that had crashed or exited without a trace were rerun on the arithmetic fix `dfd5d92ee5df3ce98d8a91bc058cc74a8f779120`; all 14 completed. The fix changes only how an oversized wood ratio is saturated before narrowing; representable Q32.32 ratios keep the existing arithmetic. These runs combine into 78 calibration matches plus the original 16 held-out matches.
- Calibration seeds (78): `20261101–20261102, 20261106–20261108, 20261110–20261127, 20261130–20261138, 20261142, 20261146–20261150, 20261154–20261155, 20261158–20261160, 20261162, 20261181, 20261185–20261187, 20261189–20261206, 20261209–20261219, 20261221`.
- Held-out seeds (16): `20261165–20261180`.
- Per-match and per-seat output: [CSV](m11-iron-balance-screen-2026-11-04.csv). The 2026-11-05 follow-up used the current `main` source base (`6ec1c97`) plus the changes in this worktree. It reran eight same-seed Iron/Bronze pairs (`20261019–20261026`), 240 seasons, fixed population, and the default 8-tile charcoal reach (no TRN-05). It traces all forest stock in that reach and reconciles regrowth against metal, chopping, civic and other standing-wood cuts.
- The Iron fuel guard now measures both its starting baseline and live stock across the charcoal catchment (8 tiles, or the cart reach with TRN-05), matching the actual charcoal haul path. Bronze retains its E0–E3 local-range baseline. After MET-09, the planner no longer requests new Bronze kits for adults already covered by the Iron re-kit demand; existing Bronze kits can still be used until replaced. The 55%/75% Iron wood thresholds, recipe quantities and fuel costs are unchanged.

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

Fixture: one fixed 60-person settlement with the live E3 age mix, grain topped up each season, forge/kiln/smelter present and an iron camp site four tiles away. Population stays fixed so the Iron/Bronze comparison does not include demographic drift. There are 240 seasons per scenario; the table measures the final 120. Eight seeds (`20261019–20261026`) are paired across identical maps. The wood balance includes all forest tiles in the fixture's actual 8-tile charcoal reach; TRN-05 is not completed here. All standing-wood removal categories and regrowth are traced. “T11+ at fixture 12” starts its clock before camp setup; the separate I-T2 table measures 12 full seasons after a built camp.

| Scenario | Metal work mean/max | Regrowth / all cuts (L/season) | Stock-flow residual (L/season) | Reachable wood end/start mean/min | End/mid | NoFuel fallback mean/max | T11+ at fixture12 / route12 / end |
|---|---:|---:|---:|---:|---:|---:|---:|
| Bronze reference | 1.7% / 1.9% | 5.10 / 5.10 | 0.000 | 0.99 / 0.98 | 1.00 | 0.0% / 0.0% | 0.0% / 0.0% / 0.0% |
| Iron | 2.9% / 3.1% | 5.10 / 5.10 | 0.000 | 0.99 / 0.98 | 1.00 | 0.0% / 0.0% | 73.7% / 93.3% / 100.0% |
| Iron, low wood | 3.0% / 3.6% | 5.49 / 5.47 | 0.000 | 0.73 / 0.00 | 1.00 | 0.0% / 0.0% | 64.7% / 78.1% / 89.7% |
| Bronze, low wood | 1.2% / 1.5% | 4.69 / 4.71 | 0.000 | 0.76 / 0.00 | 1.00 | 12.5% / 100.0% | 0.0% / 0.0% / 0.0% |
| Iron, low wood, no Coppicing | 2.8% / 3.2% | 3.25 / 3.22 | 0.000 | 0.73 / 0.00 | 1.01 | 0.0% / 0.0% | 62.9% / 78.6% / 89.7% |
| Bronze, low wood, no Coppicing | 1.1% / 1.4% | 2.50 / 2.51 | 0.000 | 0.76 / 0.00 | 1.00 | 12.5% / 100.0% | 0.0% / 0.0% / 0.0% |
| Iron, no Coppicing | 2.9% / 3.1% | 5.62 / 5.10 | 0.000 | 0.99 / 0.98 | 1.07 | 0.0% / 0.0% | 73.7% / 92.4% / 100.0% |
| Bronze, no Coppicing | 1.7% / 1.9% | 5.10 / 5.10 | 0.000 | 0.99 / 0.98 | 1.00 | 0.0% / 0.0% | 0.0% / 0.0% / 0.0% |
| Iron, import loss at midpoint | 0.9% / 0.9% | 5.10 / 5.10 | 0.000 | 0.99 / 0.98 | 1.00 | 0.0% / 0.0% | 73.7% / 93.3% / 72.3% |
| Iron, wood rich | 2.9% / 3.1% | 5.10 / 5.10 | 0.000 | 1.00 / 0.99 | 1.00 | 0.0% / 0.0% | 73.7% / 93.3% / 99.6% |
| Bronze, wood rich | 1.7% / 1.7% | 5.10 / 5.10 | 0.000 | 1.00 / 0.99 | 1.00 | 0.0% / 0.0% | 0.0% / 0.0% / 0.0% |

The documented steady-state metal-work ceiling passes every scenario: the highest maximum is 3.6%, below 15%. Mean stock-flow residual is 0.000 L/season at the report's precision. Standard, no-Coppicing, import-loss and wood-rich Iron have flat or growing final-half forest stock. On low-wood starts, mean reachable stock ends at 0.73 of its starting amount and the worst seed reaches zero; the matched Bronze low-wood controls also reach zero in a worst seed. Because the “no mandatory clear-cut” clause has no numeric acceptance rule, this is an unresolved stress result rather than a formal threshold verdict. “NoFuel fallback” is the settlement's reported metal-method fallback, not a raw count of individual charcoal-short seasons.

Under low wood, WOD-04 adds 2.24 L/season of realized regrowth and 2.25 L/season of all standing cuts versus the same-seed no-Coppicing Iron controls: about 69% more of each flow. Mean reachable stock at the 12-season route checkpoint is 0.75 with Coppicing versus 0.72 without; the worst seed retains 0.51 versus 0.37. This substantially increases sustainable throughput and gives more reserve in the stressed outlier, while long-run end/start retention and final Iron-holder coverage are similar. Standard routes meet the target with and without Coppicing.

This supports “Coppicing is optional but substantially increases sustainable wood throughput when wood is scarce.” It does not eliminate the worst low-wood T11+ coverage miss. The low-wood Iron-minus-Bronze comparison adds 0.76 L/season in metal charcoal and standing cuts, paired with 0.80 L/season more regrowth; end/start stock differs by −0.03 at displayed precision. The outlier's early route slowdown occurs when the forest guard reaches its reserve floor, not because the chain continues to mine fuel below it. The low-wood fixture still needs an alternate supply path or a more precise sustainability contract before it can certify “no mandatory clear-cut.”

**I-T6 reading:** every steady-state work-share screen passes, the paired accounting closes, and Coppicing materially raises low-wood regrowth and sustainable cut flow without changing the 55%/75% throttle thresholds. The extreme low-wood fixture remains a stress limitation: one seed reaches zero stock by season 240, as does its matched Bronze control. No numeric clear-cut threshold is approved. The earlier unpaired, variable-population table and its unsupported 50%/25% heuristic remain superseded. No fuel or recipe values changed; the follow-up fixes the reach used by the Iron guard and avoids duplicate Bronze-kit demand after MET-09.

## I-T2 sustainable-route check

The route clock starts after the fixture's own Iron camp is built; it activates in seasons 2–3 in this seed block. Coverage is measured after 12 complete seasons following that activation. The table reports per-seed coverage, not the final holder ratio after 240 seasons.

| Route | T11+ mean / min / max | Seeds at or above 70% | Reachable forest at checkpoint, mean / min |
|---|---:|---:|---:|
| Standard Iron | 93.3% / 82.1% / 96.4% | 8/8 | 0.86 / 0.63 |
| Iron, low wood with Coppicing | 78.1% / 25.0% / 89.3% | 7/8 | 0.75 / 0.51 |
| Iron, no Coppicing | 92.4% / 82.1% / 96.4% | 8/8 | 0.81 / 0.52 |
| Iron, low wood without Coppicing | 78.6% / 25.0% / 89.3% | 7/8 | 0.72 / 0.37 |
| Iron, wood rich | 93.3% / 82.1% / 96.4% | 8/8 | 0.98 / 0.97 |

Seed `20261026` is the low-wood outlier: 25.0% T11+ coverage with Coppicing and 25.0% without. At that checkpoint, reachable forest is 0.51 of its starting stock with Coppicing and 0.37 without; the forest guard is below its production floor. This is a per-seed miss for the low-wood route screen and remains open. Standard-route coverage passes all eight seeds. The one-time 70-kit delivery result is not used as evidence for I-T2.

## Other screens and remaining gates

- **I-T2:** the standard built-camp route reaches at least 70% T11+ coverage in all eight seeds after 12 full route seasons. The low-wood route reaches the target in seven of eight; seed `20261026` stalls at 25% when the wood guard reaches its floor. The one-time kit-delivery result is not evidence for I-T2. The low-wood per-seed miss remains open.
- **I-T3:** deterministic fixture confirms T12 service life is exactly 48 vs T11's 24 work-seasons. Live mixed-age wear/replacement distribution remains open.
- **I-T4:** one stipulated aqueduct/grain fixture reaches the proposed 1.4× lower bound. The required terrain set and physical mass-balance/live-mode comparison remain open.
- **I-T5:** the selected isolated siege fixture expresses the candidate 0.40 duration multiplier. No natural match began an investment, so engine-vs-investment and relief/attrition comparison remain open.
- **Geography diagnostic:** a 10,000-world audit found 45.6% of starts cannot supply replacement for 100 adults of T11 from reach-8 forest regrowth alone. Local or trade/expansion access is therefore material to I-T2/I-T6.
- **I-T7/I-T8 and B4-03/04/06–15:** not closed by these runs. In particular, no 1,000-match replay gate, performance gate, human playtest, or complete B4 acceptance protocol was run.

No target or wood-throttle parameter was changed after seeing these results. The reported observations are provisional screens to inform the next oracle fit and protocol review.
