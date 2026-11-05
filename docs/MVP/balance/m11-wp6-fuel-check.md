# M11 WP6 — Fuel check (I-T6)

**Historical screen:** the later fixed-population, tile-level forest screen in [the 2026-11-05 Iron balance report](m11-iron-balance-screen-2026-11-04.md) supersedes this report's provisional clear-cut verdict and aggregate-only forest measurements. Keep this file as the record of the original throttle decision and its calibration history.

**Gate (25 §14, I-T6):** "Sustainable metal not >15% of civilian work in steady state; no mandatory clear-cut. Full replacement/fuel/haul accounting; stress import loss and low-wood starts."

**Result.** The labor clause passes with a wide margin. The "no mandatory clear-cut" clause is **not met** for a settlement with a modest forest: on the 13-forested-tile fixture the Iron chain strips the standing wood within about 30 years, and it stays safe only with a forest of about 30 tiles. The Bronze economy already thins that small forest, so the Iron chain is not the only cause, but it roughly triples the charcoal draw. This is the warning in [26 §8](../26-e4-iron-specs.md) (fuel is a central risk) showing up in the live economy. No fix has been implemented; options are in §5 and the choice is yours.

**Update (wood-aware throttle, chosen by the user 2026-11-03; see §6).** The planner now throttles the Iron chain's fuel demand by the standing wood in range. With it, the Iron chain holds the 13-tile forest at 0.58 of its start instead of 0.49, its charcoal draw after the build-out tracks the Bronze twin's, and low-wood starts simply run without iron kits (as the Bronze twin runs short of fuel). The no-coppice start still falls to 0.28. §1 to §5 are the findings that led to the throttle.

Raw outputs after the throttle: [40 seasons](m11-wp6-fuel-check-output-40.txt), [120 seasons](m11-wp6-fuel-check-output-120.txt). Before the throttle: [40](m11-wp6-fuel-check-output-40-before-throttle.txt), [120](m11-wp6-fuel-check-output-120-before-throttle.txt).

## 1. How to reproduce

```bash
dotnet run --project src/CivSim.Cli -c Release -- build-pack --out fuel.pack
dotnet run --project src/CivSim.Harness -c Release -- iron-fuel --pack fuel.pack --seasons 40
dotnet run --project src/CivSim.Harness -c Release -- iron-fuel --pack fuel.pack --seasons 120
```

Seeds 20261019 to 20261026. The output is a pure function of the arguments (the test `IronFuelCheckTests` runs it twice).

## 2. Method and what it can and cannot show

Each run is a one-settlement fixture (`BronzeFixture.CreateFuelScenario`): 60 people in the live E3 age mix, a forge, kiln and smelter from the start, an iron camp site four tiles out on own land, E4 projects MET-08 and MET-09 known, and grain topped up each season so food never binds. It runs the real economy for the stated number of seasons and reads the economy's own statistics.

- **Civilian work share** = metal-chain labor (mining, charcoal, smelting and camp work) over all work, in the second half of the run (the "steady-state window").
- **Standing wood** = wood on the fixture's forested tiles. **end/start** includes the one-time kit build-out; **end/mid** is the slope inside the window (1.00 is flat).
- **Operational definition of "mandatory clear-cut".** The doc gives none. I fixed one before the first run: standing wood ends below 50% of its start, or the chain reports a fuel shortage in more than 25% of the window. Both thresholds are mine and are open to change.
- **Scenarios.** `bronze-reference` (same settlement, Bronze chapter, no iron), `iron`, `iron-low-wood` (4 forested tiles), `iron-no-coppice` (WOD-04 unknown), `iron-import-loss` (the iron deposit is exhausted at the window start), a Bronze twin of the low-wood and no-coppice cases, and `iron-wood-rich` (30 forested tiles) with its Bronze twin.

Not shown: a whole match. No iron-hauling cost beyond the camp's own haul, no trade, no second settlement, no war, no fission. The Iron chain's wood is read from the economy's charcoal and fire-setting counters, and the hearth, construction and other uses of wood are in the standing-wood figures but not separated.

## 3. Results

Eight seeds each; worst seed unless noted.

| Scenario | Work share, 40 / 120 seasons (max) | Charcoal wood per season, 40 / 120 | Wood end ÷ start (mean), 40 / 120 | Fuel short in window, 120 (worst) |
|---|---:|---:|---:|---:|
| bronze-reference | 2.3% / 2.2% | 2.0 / 4.0 L | 0.97 / 0.56 | 0% |
| **iron** (13 forest tiles) | 4.5% / 6.1% | 5.3 / 11.1 L | **0.49 / 0.00** | 90% |
| iron-low-wood (4 tiles) | 5.9% / 5.9% | 4.4 / 7.3 L | 0.00 / 0.00 | 100% |
| iron-no-coppice | 5.0% / 7.0% | 4.8 / 8.5 L | 0.00 / 0.00 | 100% |
| iron-import-loss | 2.0% / 1.9% | 1.9 / 3.6 L | 0.61 / 0.28 | 58% |
| bronze-low-wood | 2.3% / 2.2% | 1.3 / 2.2 L | 0.00 / 0.00 | 100% |
| bronze-no-coppice | 2.3% / 2.3% | 2.0 / 3.3 L | 0.62 / 0.00 | 73% |
| **iron-wood-rich** (30 tiles) | 4.4% / 5.0% | 5.3 / 12.0 L | **0.98 / 0.98** | 0% |
| bronze-wood-rich | 2.3% / 2.3% | 2.0 / 3.9 L | 0.99 / 0.98 | 0% |

1. **Labor passes.** The metal chain takes 4.1% to 4.6% of civilian work on average and at most 7.0% on any seed, against the 15% ceiling, in every Iron scenario including the stresses. The Iron chain adds about 2 to 3 points over Bronze.
2. **Wood does not, on a modest forest.** With 13 forested tiles the Iron chain takes the standing wood to 0.49 of its start during the first 40 seasons (the kit build-out) and then holds flat, which already crosses my 50% line. Over 120 seasons, once kits wear out and are replaced, it reaches zero and the chain reports a fuel shortage in most of the window.
3. **A large forest sustains it.** With 30 forested tiles the standing wood stays at 0.98 over 120 seasons with the Iron chain at full replacement. So the chain is sustainable, but only where the forest is much larger than the E3 fixture's.
4. **Marginal cost matches the oracle.** At 120 seasons the Iron chain draws about 7 to 8 litres of wood a season more than the Bronze reference for about 30 adults, or 0.24 L per adult per season. `e4_metal_oracle.py` gives 22.29 L per 100 adults per season (0.22). Regrowth is 2 L a year per forested tile (0.5 L a season), so replacing iron kits alone needs the regrowth of about 14 forested tiles per 30 adults, on top of every other use of wood.
5. **Coppicing matters a great deal.** Without WOD-04 the Iron case reaches zero standing wood even at 40 seasons. The Bronze twin does not by then (0.62), so the Iron chain brings the clear-cut forward.
6. **Import loss does not spiral.** When the iron supply is cut at the window start the chain stops cleanly: charcoal draw falls to the Bronze level (1.9 L, 40 seasons), work share drops to 2%, the remaining iron kits keep working (77.6% of adults at 40 seasons), and the standing wood recovers (end/mid 1.29). At 120 seasons the kits have worn out (27.7% still equipped), and the wood loss that remains is the Bronze chain's, not iron's.
7. **Low-wood starts are a Bronze problem first.** The Bronze low-wood twin already strips its forest and runs short of fuel in every window. The Iron chain does not create that failure; it deepens it.

## 4. Verdict against I-T6

| Clause | Result |
|---|---|
| Sustainable metal ≤ 15% of civilian work in steady state | **Met** (worst seed 7.0%) |
| No mandatory clear-cut | **Not met** on 13 forested tiles, low-wood and no-coppice starts; **met** on 30 forested tiles |
| Replacement, fuel and haul accounting | Counted for the chain's own labor, wood and the camp's haul; the fixture does not include inter-settlement hauling |
| Stress: import loss | No spiral; the chain stops and fuel recovers |
| Stress: low-wood starts | Fuel-short in every window, as in Bronze |

The caveat on the second row is that a standing-wood drawdown below 50% is my definition of a clear-cut. By a stricter or looser line the 13-tile result moves, the 30-tile result does not, and the Bronze economy fails the stricter line too at 120 seasons.

## 5. Options (the user chose option 2, built in §6)

1. **Accept and document.** Treat iron as a wood-rich-start technology. A settlement that runs out of wood already falls back to bronze kits (the NoFuel dormancy), so play continues, but only after the forest is gone. The cost is that the Iron chain makes forest loss faster.
2. **A wood-aware throttle in the Iron planner.** Do not want or replace T11 kits when standing wood in range has fallen below a share of its start (or below a multiple of the season's regrowth), so kits wear down and the settlement uses bronze while the forest recovers. It changes Iron-chapter behavior only, and this harness can verify it. This is what the gate's "no mandatory clear-cut" most plainly asks for. It would be a new rule, so its threshold needs your approval.
3. **Retune the provisional numbers** (charcoal per kit in 26 §2.3, T11 service life, or forest regrowth). This changes the oracle fit and 26, and is the right route only if the throttle is not enough.

I recommend option 2, with this harness as the acceptance check.

## 6. The wood-aware throttle (built)

**Rule.** `IronWoodFactor` = (standing wood in range ÷ the wood in range when the settlement's metal economy began − 0.55) ÷ 0.20, clamped to 0 to 1. At 75% of its starting wood or more the Iron chain asks for fuel in full; at 55% or less it asks for none. It is stateless (recomputed each season from the forest) and the 55% and 75% marks are my choice, open to change. It scales three Iron wants and nothing else:

1. the T11 kit target (`IronKitWant`), except that iron already in store (wrought iron, or bloom at 0.8 wrought each) is always forged into kits, because forging costs about a quarter of a litre of charcoal a kit against six times that for smelting the ore;
2. the Iron bloom export reserve (`IronExportWant`);
3. the standing litre of bloom that E3's MET-08 wants (`SecondaryWants`). This one mattered most: in the Iron chapter the forge turns that litre into wrought iron, so it re-smelted a litre every season and silently pumped kits at factor 0. The first version of the throttle missed it and changed nothing; a per-season trace found it.

Kits already made keep working, the Bronze chain is untouched, and a Bronze-chapter economy never computes the factor. Tests: a thin forest stops the chain smelting ore and a full one does not; stored iron is still forged on a thin forest; the fixture's wood factor is exposed for tests.

**Results (same 8 seeds, same fixture; before the throttle in brackets).**

| Scenario | Work share max, 40 / 120 | Charcoal wood per season, 40 / 120 | Wood end ÷ start (mean), 40 / 120 | Iron kits held, 40 / 120 |
|---|---:|---:|---:|---:|
| bronze-reference | 2.3% / 2.2% | 2.0 / 4.0 L | 0.97 / 0.56 | 0 / 0 |
| **iron** (13 tiles) | 5.4% / 3.2% (4.5 / 6.1) | 5.2 / 4.9 L (5.3 / 11.1) | **0.58 / 0.25** (0.49 / 0.00) | 84.8% / 30.3% (93.7 / 86.8) |
| iron-low-wood (4 tiles) | 2.2% / 2.1% (5.9 / 5.9) | 1.3 / 2.1 L (4.4 / 7.3) | 0.00 / 0.00 (0.00 / 0.00) | 10.6% / 3.1% |
| iron-no-coppice | 2.3% / 2.1% (5.0 / 7.0) | 2.0 / 3.0 L (4.8 / 8.5) | 0.28 / 0.00 (0.00 / 0.00) | 40.3% / 8.6% |
| iron-import-loss | 2.1% / 2.0% | 1.9 / 3.8 L | 0.72 / 0.35 (0.61 / 0.28) | 65.5% / 22.3% |
| iron-wood-rich (30 tiles) | 4.4% / 5.0% | 5.3 / 12.0 L | 0.98 / 0.98 | 93.7% / 97.7% |

What it shows:

- **The chain no longer drives the forest to zero.** On 13 forested tiles, by 40 seasons the standing wood is 0.58 of its start (worst seed 0.54), above my 50% line, and the forest recovers once the chain stops. Before, the chain overshot to 0.49 and kept drawing.
- **After the one-time build-out the Iron chain costs about what Bronze does.** At 120 seasons the Iron settlement burns 4.9 L of charcoal wood a season against 4.0 for the Bronze twin (it was 11.1). A per-20-season trace on two seeds shows the Iron build-out (about 300 L in the first 20 seasons against about 96 for Bronze) is the whole difference; later windows match Bronze's.
- **Why wood is still lower than the Bronze twin at 120 seasons (0.25 against 0.56).** The one-time build-out leaves the forest about 40% lower, and the fixture's settlement then grows from 60 to about 175 people on 13 forested tiles with no splitting, so general wood use (hearths, building) overtakes regrowth in both chapters. That late depletion is a fixture artifact shared with Bronze, not iron's cost. I did not remove it from the fixture.
- **Low-wood starts go without iron kits.** With 4 forested tiles the chain is nearly off (10.6% of adults have iron kits at 40 seasons, 3.1% at 120) and the settlement behaves like its Bronze twin: both run short of fuel in 12.5% of windows. So in a low-wood start, iron has to come from trade (the slice 5 compact) or from expansion onto wooded land, which is what the Q9 decision already assumed.
- **No-coppice starts still fall to 0.28** at 40 seasons (0.22 on the worst seed), below my line. Without Coppicing the forest regrows slowly and the factor reacts late. The Bronze twin without Coppicing reaches 0.62. I did not tune for this case.
- **Import loss** stays clean: the chain stops, wood recovers (0.72 at 40 seasons, up from 0.61), and the settlement falls back to bronze kits.
- **Labor.** The work share is lower than before (3.2% worst at 120 seasons) because the chain does less in a thin forest; it never exceeded 5.4%.

**I-T6 after the throttle.**

| Clause | Result |
|---|---|
| Metal ≤ 15% of civilian work, steady state | Met (worst 5.4% at 40 seasons, 3.2% at 120) |
| No mandatory clear-cut, 13 forested tiles | Met at 40 seasons by my 50% line (0.58); the Iron chain adds a one-time ~40% drawdown and then costs about what Bronze does. Not met at 120 seasons, by a fixture effect shared with the Bronze twin |
| No mandatory clear-cut, 30 forested tiles | Met |
| Low-wood start | Iron stays mostly off; fuel shortage is the Bronze twin's |
| No-coppice start | Not met at 40 seasons (0.28); Bronze twin 0.62 |
| Import loss | Met: no spiral, wood recovers |

**Decisions (user, 2026-11-03).**

1. The 55% and 75% marks stand until playtesting says otherwise.
2. The one-time ~40% build-out drawdown is acceptable.
3. No special rule for starts without Coppicing yet, but the intent is recorded: Coppicing should make iron *viable*, and a civilization without it should still be *minimally functional*, not locked out. Today a no-coppice settlement still makes iron kits (40.3% of adults at 40 seasons) but its forest ends at 0.28 of its start against 0.62 for the Bronze twin. Revisit after playtests; a gentler floor for no-coppice settlements is the likely tool.

The fixture's unbounded settlement growth makes any 120-season wood verdict pessimistic, so a whole-match check (WP11) should replace it for the final I-T6 acceptance.
