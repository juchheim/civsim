# M13 WP-E6-7 slice 7: the five rows that had no system (2026-11-24)

**Status:** built and tested on `codex/e6-discovery`. Closes the E6 simulation gaps listed in [38](../38-discovery-age.md): ART-10, COM-11, NUM-10, INQ-09 and BLD-13's layout bonus. Every value is read from the row's own `mod` effect through `Modifier`, so the numbers stay in the YAML; nothing is stored, so no snapshot version moves; every term is exactly 1 outside a Discovery match, so Bronze, Iron and Fief bytes and goldens are unchanged. All numbers are the registry's provisional ones.

TRD-10 and TRD-11 were already built (WP-E6-6 slice 6) on decision D12 (pooled goods only, no debt); the "waits on D12" lines in the doc were stale and are corrected.

## What each row now does

| Row | Stat (value) | Effect in the sim |
|---|---|---|
| **BLD-13** layout | `city_move_and_build_speed` (x1.15) | A **City** (settlement tier 2) of a civilization with BLD-13 builds faster: its construction labor is divided by the row's value. Applied at the three labor sources: ordinary structures (`StructureBuildLabor`), civic structures (`CivicLabor`) and Iron works including Wonders (`IronLaborOf`). Towns and villages are untouched. |
| **NUM-10** logarithms | `navigation_calc_speed` (x2) | The calculation behind **precision parts, chronometers and telescopes** takes half the recipe labor. Coke, soap and every other recipe are untouched. |
| **INQ-09** physics | `mec_nrg_sea_effects` (x1.1) | Every MEC, NRG and SEA project effect is a tenth stronger: the part of a multiplier above 1 (or a percentage) grows by the row's value, so a x1.2 becomes x1.22. A discount (a value below 1) is left alone: physics does not make a cart cheaper. Applied inside `ComputeModifier`, so everything that reads a modifier sees it. |
| **COM-11** newspapers | `cohesion_event_response` (x1.5), `gov_effects` (x1.1) | In a settlement with a **print shop**, Cohesion closes on its target x1.5 faster each season (a quarter of the gap becomes three eighths), and the GOV projects' share of the Cohesion target counts 10% more (a bounded opinion effect: a point or two at most). |
| **ART-10** patronage | `art_insight` (x1.1), `legend_chance` (x2), +1 Beacon | A **patron** stands when the capital holds a Palace (the court) or any settlement holds a Wonder. While one does: the monks (the masters of a staffed monastery) produce 10% more Insight, and the civilization may hold **one more Beacon**. |

## Two decisions worth knowing

1. **Legend chance has nothing to double.** The simulation has no Legend system in any era ([17](../17-vertical-slice.md) lists "Legends beyond naming in the Chronicle" as deferred). ART-10's x2 is therefore exposed as `LegendChanceFactor(civ)` (2 with a patron, else 1) and **nothing reads it yet**. When Legends are built they multiply by it. This is the one row effect that is carried rather than consumed.
2. **"Patron-funded masters" = monks.** There is no artist role. The monks of a staffed monastery are the Insight workers ART-09 already lifts (illuminated manuscripts), so they stand for the masters. If an artist role is added later, move the multiplier there.

## Tests (9 in `DiscoveryRenaissanceTests`, all pass)

BLD-13 cuts a City's Iron-work and structure labor and no Town's, and does nothing in Fief; NUM-10 halves exactly the three instrument recipes; INQ-09 lifts MEC-09's and SEA-08's effects by a tenth and leaves BLD-13, NUM-10 and a discount alone, and does nothing in Fief; COM-11's response and opinion gain need both the project and a print shop and do nothing in Fief; ART-10 needs the project and a patron (Palace or Wonder) and does nothing without the project or in Fief.

**Not covered by a test:** the research-side monk multiplier (the `PatronFundsMasters` flag is tested, the Insight arithmetic that reads it is one line in `ResearchSystem.RefreshInsight`), and the Beacon allowance through `PoliticsSystem.BeaconAllowance` (the slot count is tested).
