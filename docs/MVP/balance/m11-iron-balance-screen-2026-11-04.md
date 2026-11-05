# M11 provisional Iron balance screen — 2026-11-04

**Status:** preliminary screening evidence only. The screens below do not approve a target, fit a cost, or close an M11 gate. The proposed thresholds and protocols in [25 §13–14](../25-iron-age.md) remain unchanged.

## Reproduction record

- Balance targets: [25 §13](../25-iron-age.md#13-provisional-balance-targets-and-oracle-work); proposed gates: [25 §14](../25-iron-age.md#14-proposed-m11-acceptance-series-b4-xx).
- Content pack: `/tmp/civsim-iron-balance.pack`, SHA-256 `85f436c611870c6a34d75dc56a8624c702d2e3810a5c1ed3edd6daef05ae7ee9`.
- Whole-match mode: default Iron chapter rules; four Warlord AI seats (Builder, Grower, Warlord, Sage), no E4 grants. The match harness applies the candidate Iron cap/countdown. Simulated elapsed time is Standard 1×, not wall-clock runtime.
- The initial 80 successful match reports used source baseline `df591663c53bf1db0475545e9001dd86e02bec34`. Fourteen calibration seeds that had crashed or exited without a trace were rerun on the arithmetic fix `dfd5d92ee5df3ce98d8a91bc058cc74a8f779120`; all 14 completed. The fix changes only how an oversized wood ratio is saturated before narrowing; representable Q32.32 ratios keep the existing arithmetic. These runs combine into 78 calibration matches plus the original 16 held-out matches.
- Calibration seeds (78): `20261101–20261102, 20261106–20261108, 20261110–20261127, 20261130–20261138, 20261142, 20261146–20261150, 20261154–20261155, 20261158–20261160, 20261162, 20261181, 20261185–20261187, 20261189–20261206, 20261209–20261219, 20261221`.
- Held-out seeds (16): `20261165–20261180`.
- Per-match and per-seat output: [CSV](m11-iron-balance-screen-2026-11-04.csv). The corrected I-T6 fixture uses eight same-seed Iron/Bronze pairs (`20261019–20261026`), 240 seasons, fixed population, and the default 8-tile charcoal reach (the fixture does not grant TRN-05). It traces all forest stock in that reach and reconciles regrowth against metal, chopping, civic and other standing-wood cuts. A second run adds low-wood Iron/Bronze controls without WOD-04 to measure Coppicing's effect under stress. The initial paired run used the `dfd5d92` source base with the I-T6 harness changes in the working tree; the follow-up adds the Coppicing comparison.

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

Fixture: one fixed 60-person settlement with the live E3 age mix, grain topped up each season, forge/kiln/smelter present and an iron camp site four tiles away. Population stays fixed so the Iron/Bronze comparison does not include demographic drift. There are 240 seasons per scenario; the table measures the final 120. Eight seeds (`20261019–20261026`) are paired across identical maps. The wood balance includes all forest tiles in the fixture's actual 8-tile charcoal reach; TRN-05 is not completed here. All standing-wood removal categories and regrowth are traced.

| Scenario | Metal work mean/max | Regrowth / all cuts (L/season) | Stock-flow residual (L/season) | Reachable wood end/start mean/min | End/mid | Fuel-short mean/max | T11+ holders |
|---|---:|---:|---:|---:|---:|---:|---:|
| Bronze reference | 1.7% / 1.9% | 5.10 / 5.10 | 0.000 | 0.99 / 0.98 | 1.00 | 0.0% / 0.0% | 0.0% |
| Iron | 3.2% / 3.4% | 5.10 / 5.10 | 0.000 | 0.99 / 0.98 | 1.00 | 0.0% / 0.0% | 97.8% |
| Iron, low wood | 1.1% / 1.4% | 4.70 / 4.72 | 0.000 | 0.76 / 0.00 | 1.00 | 12.5% / 100.0% | 12.1% |
| Bronze, low wood | 1.2% / 1.5% | 4.69 / 4.71 | 0.000 | 0.76 / 0.00 | 1.00 | 12.5% / 100.0% | 0.0% |
| Iron, low wood, no Coppicing | 1.1% / 1.4% | 2.50 / 2.52 | 0.000 | 0.76 / 0.00 | 1.00 | 12.5% / 100.0% | 9.8% |
| Bronze, low wood, no Coppicing | 1.1% / 1.4% | 2.50 / 2.51 | 0.000 | 0.76 / 0.00 | 1.00 | 12.5% / 100.0% | 0.0% |
| Iron, no Coppicing | 3.2% / 3.5% | 5.32 / 5.10 | 0.000 | 0.99 / 0.98 | 1.03 | 0.0% / 0.0% | 97.3% |
| Bronze, no Coppicing | 1.7% / 1.9% | 5.10 / 5.10 | 0.000 | 0.99 / 0.98 | 1.00 | 0.0% / 0.0% | 0.0% |
| Iron, import loss at midpoint | 1.6% / 1.7% | 5.10 / 5.10 | 0.000 | 0.99 / 0.98 | 1.00 | 0.0% / 0.0% | 66.1% |
| Iron, wood rich | 3.2% / 3.4% | 5.10 / 5.10 | 0.000 | 1.00 / 0.99 | 1.00 | 0.0% / 0.0% | 98.2% |
| Bronze, wood rich | 1.7% / 1.7% | 5.10 / 5.10 | 0.000 | 1.00 / 0.99 | 1.00 | 0.0% / 0.0% | 0.0% |

The documented metal-work ceiling passes this fixture screen in every seed: the highest scenario maximum is 3.5%, below 15%. The mean stock-flow residual is 0.000 L/season at the report's precision. Ordinary, no-Coppicing, import-loss and wood-rich Iron retain 98–100% of starting reachable forest on average, with a flat or growing final-half stock. The ordinary Iron case uses 0.87 L/season more charcoal wood than its paired Bronze control, while total standing cuts and end/start stock differ by 0.00 at displayed precision.

Under low wood, WOD-04 adds 2.19 L/season of realized regrowth and 2.20 L/season of standing cuts versus the same-seed no-Coppicing Iron controls—about 88% more sustainable harvest throughput. It does not raise the final reachable-forest reserve (0.76 mean retention in both, with a zero-stock worst seed), and fuel-short share is unchanged at 12.5% mean / 100% worst. Iron tool coverage rises from 9.8% to 12.1% (+2.3 percentage points). On the ordinary 13-forest fixture, coverage is 97.8% with Coppicing versus 97.3% without it, with no fuel shortage and about 99% forest retention in both.

This supports “Coppicing is optional but raises sustainable wood throughput when wood is scarce.” It does not yet show a large improvement in Iron-tool availability under the low-wood stress. The low-wood Iron and Bronze controls have the same 0.76 mean retention and 0.00 worst-seed end/start ratio; their Iron-minus-Bronze difference is only +0.01 L/season in charcoal wood and standing cuts, with a 0.00 stock delta at displayed precision. Treat low-wood tool/fuel availability as an open access/replacement failure for I-T2/I-T6, not an Iron-specific clear-cut. The “no mandatory clear-cut” clause still has no formally approved numeric acceptance definition.

**I-T6 reading:** the work-share screen passes; the matched standard, no-Coppicing, import-loss and wood-rich sustainability screens pass without an Iron-attributable stock loss. Coppicing is not required for the normal fixture, and the low-wood pair shows substantially more sustainable harvest throughput with WOD-04, but not materially better reserves or tool coverage. Low-wood still fails tool/fuel availability and remains an open stress case, although it has the same forest depletion as Bronze at displayed precision. The previous unpaired, variable-population table and its unsupported 50%/25% “clear-cut” heuristic are superseded. No fuel or recipe values were changed; the harness now reports paired flows and the stock-flow reconciliation instead of inventing a sustainability threshold.

## Other screens and remaining gates

- **I-T2:** a one-time fixture delivery of 70 kits plus continuous access reaches 70% coverage by season 12, but it is not a sustainable supply route. Whole-match final holder ratios are not the required 12-season route cohort. I-T2 is not established.
- **I-T3:** deterministic fixture confirms T12 service life is exactly 48 vs T11's 24 work-seasons. Live mixed-age wear/replacement distribution remains open.
- **I-T4:** one stipulated aqueduct/grain fixture reaches the proposed 1.4× lower bound. The required terrain set and physical mass-balance/live-mode comparison remain open.
- **I-T5:** the selected isolated siege fixture expresses the candidate 0.40 duration multiplier. No natural match began an investment, so engine-vs-investment and relief/attrition comparison remain open.
- **Geography diagnostic:** a 10,000-world audit found 45.6% of starts cannot supply replacement for 100 adults of T11 from reach-8 forest regrowth alone. Local or trade/expansion access is therefore material to I-T2/I-T6.
- **I-T7/I-T8 and B4-03/04/06–15:** not closed by these runs. In particular, no 1,000-match replay gate, performance gate, human playtest, or complete B4 acceptance protocol was run.

No target or candidate parameter was changed after seeing these results. The reported observations are provisional screens to inform the next oracle fit and protocol review.
