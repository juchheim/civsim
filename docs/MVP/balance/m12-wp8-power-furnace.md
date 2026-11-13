# M12 WP-E5-5 (first slice): mechanical power and the blast furnace (2026-11-13)

**Status:** built and tested on `codex/e5-fief`. Everything is gated on the Fief chapter (`FiefChain`); an Iron match is **byte-for-byte unchanged** (two seeds compared against the pre-change output) and the Bronze goldens pass. E5 rows are still draft in the repo; experiments used a temporary pack with all 50 E5 rows promoted (script below).

## What was built
| Part | Rule (all numbers P, from the e5_power_oracle screens) |
|---|---|
| Windmill (NRG-05) | A mill that turns 4 seasons in 5: grain value and powered-recipe labor get 80% of a water mill's effect. Built where no water mill stands; needs 14 timber, 4 block, 1 wheel. |
| Sawmill (WOD-09) | `saw_planks` labor ×1/4 (catalog "Saw: Mill ×4"); needs a river town of 60+ people. |
| Blast furnace (MET-12) | In a smelting town with a forge, a smelter and a mill, each bloomery batch yields ×4 iron from ×4 ore, ×2 charcoal and ×2 labor, so half the charcoal and half the labor per litre. The first draft's separate pig-iron good was dropped: the furnace's iron is the same G-IRON the forge refines. |
| Spinning wheel / treadle loom (FIB-11, FIB-10) | `spin_yarn` labor ×1/3, `weave_cloth` ×1/2; MEC-07 gives a mill town's weaving the mill labor cut (fulling). |
| Plumbing | three new `IronStructure` kinds and gates (appended; hashed only while non-zero), `BuildWhy` reasons and texts, `LivingEconomyModel.Power.cs`, harness `iron-match --fief --grant-e5 --no-furnace`. |

## What the runs say (4 seeds, natural Fief play, E5 promoted, 130 minutes)
- Civilizations research **all 49 E5 projects** and reach KEY-6 at minutes **104–117**, about 20 minutes after KEY-5 (92.3).
- The new structures get built: windmill 44–67, sawmill 20–35, blast furnace 25–39 across four matches. Windmills were stuck on 2 wheels, so the need was cut to 1.
- **The furnace barely moves fuel**: with it on, metal wood removal is 291,862 L against 294,230 L off (-0.8%), kits made unchanged, 121 fuel-gate PASS and 0 FAIL both ways, forest ledgers close. The reason matches the oracle: iron demand is only the kits (no cast iron or cannon yet), and the bloomery step is a small share of charcoal once kits are issued. The furnace's real value arrives with cast iron and gunpowder (the war slice), not before.
- Stuck towns are still mostly short of **blocks** (131), as in Iron.

## F-T2 follow-up: shared power ledger and coverage diagnostic (2026-11-13)

The Fief simulation now derives one deterministic seasonal power budget per town from its built water and wind sources. A water mill contributes 5 worker-season power hours; a windmill contributes 4 × 80% = 3.2 expected hours. The budget funds the oracle's modeled grinding, sawing and powered-craft demand in that priority order. Labor savings and blast-furnace output/fuel use use the same job coverage fractions, so work cannot claim power that another job already spent. Unused power is reported explicitly. Iron and Bronze keep their previous rules.

The read-only `iron-match --fief --fief-power-ledger` report opens a 12-season window when each town first has a water or wind source. It reports town windows, population-weighted grain coverage, source counts, offered/allocated/unused power hours, and windows censored by the match horizon. Grain coverage is the resident share within the existing `MillPeople` throughput cap, adjusted for wind availability and the grinding allocation. It is a capacity proxy: the economy has no separate flour store, so this is not a physical grain-mass audit. Four 130-minute promoted-pack runs (`20261019–20261022`) recorded 90 complete town windows and zero censored windows:

| Seed | Complete town windows | Population-weighted coverage | Town windows ≥60% |
|---|---:|---:|---:|
| 20261019 | 20 | 77.6% | 18/20 |
| 20261020 | 25 | 73.6% | 22/25 |
| 20261021 | 22 | 76.8% | 20/22 |
| 20261022 | 23 | 75.4% | 21/23 |

All four runs clear the 60% population-weighted screen, while 9 of 90 individual town windows fall below it (lowest 31.7%). Each below-target window has one or two sources and spends its full power budget; these are capacity- or budget-limited towns rather than cases with idle mill power. This is a useful four-seed screen, not a fitted pass; the per-town misses and the proxy's lack of physical flour accounting remain visible for later fitting.

## Still open
Horse-gin power (needs a draft-power model), follow-up on the 9 low-coverage town windows, natural-start whole-match F-T4 anchors, cast-iron goods and cannon (no goods slots yet), crucible steel, clock tower, the other E5 systems, AI use of any of it, the game view, and promotion of the rows.

## Reproduce the promoted pack
Copy `content/` to a scratch folder, change `status: draft` to `status: numeric` on every `era: 5` row and on KEY-6 (V22 wants all or none), then `civsim build-pack --content <folder> --out e5.pack` and run `iron-match --pack e5.pack --fief --grant-e5 --minutes 130`.
