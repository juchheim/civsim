# M12 WP-E5-5 (first slice): mechanical power and the blast furnace (2026-11-13)

**Status:** built and tested on `codex/e5-fief`. Everything is gated on the Fief chapter (`FiefChain`); an Iron match is **byte-for-byte unchanged** (two seeds compared against the pre-change output) and the Bronze goldens pass. E5 rows are still draft in the repo; experiments used a temporary pack with all 50 E5 rows promoted (script below).

## What was built
| Part | Rule (all numbers P, from the e5_power_oracle screens) |
|---|---|
| Windmill (NRG-05) | A mill that turns 4 seasons in 5: grain value and powered-recipe labor get 80% of a water mill's effect. Built where no water mill stands; needs 14 timber, 4 block, 1 wheel. |
| Sawmill (WOD-09) | `saw_planks` labor ×1/4 (catalog "Saw: Mill ×4"); needs a river town of 60+ people. |
| Blast furnace (MET-12) | In a smelting town with a forge, a smelter and a mill, each fully powered bloomery batch yields ×4 iron from ×4 ore, ×1 charcoal and ×2 labor, so one-quarter the charcoal and half the labor per litre. The charcoal factor is fitted to 1.00 in Fief; the MET-12 row remains draft and its ×4 iron output semantics are unchanged. The first draft's separate pig-iron good was dropped: the furnace's iron is the same G-IRON the forge refines. |
| Spinning wheel / treadle loom (FIB-11, FIB-10) | `spin_yarn` labor ×1/3, `weave_cloth` ×1/2; MEC-07 gives a mill town's weaving the mill labor cut (fulling). |
| Plumbing | three new `IronStructure` kinds and gates (appended; hashed only while non-zero), `BuildWhy` reasons and texts, `LivingEconomyModel.Power.cs`, harness `iron-match --fief --grant-e5 --no-furnace`. |

## What the runs say (4 seeds, natural Fief play, E5 promoted, 130 minutes)
- Civilizations research **all 49 E5 projects** and reach KEY-6 at minutes **104–117**, about 20 minutes after KEY-5 (92.3).
- The new structures get built: windmill 44–67, sawmill 20–35, blast furnace 25–39 across four matches. Windmills were stuck on 2 wheels, so the need was cut to 1.
- **Historical factor-2 screen:** with the original ×2 charcoal factor, metal wood removal was 291,862 L with the furnace and 294,230 L without (-0.8%); kits were unchanged, 121 fuel-gate PASS and 0 FAIL both ways, and forest ledgers closed. The later F-T3 fit changed the Fief-only charcoal factor to 1.00; these four-match figures predate that change. Iron and Bronze retain their previous behavior.
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

## F-T2 follow-up: virtual one-extra-mill sensitivity (2026-11-14)

The four-seed rerun (`20261019–20261022`) with the promoted long-horizon pack recorded **83 complete town windows and 1 horizon-censored window**. Baseline weighted grain coverage was 78.5%, 74.5%, 75.4%, and 76.7% by seed; respectively 17/18, 25/26, 19/20, and 18/19 complete windows met 60%. Thus **79/83 complete windows** met the target, with four below-target windows total. The sole censored window (seed 20261019) is excluded from the sensitivity.

The diagnostic then adds one **virtual** mill source of the first observed type for each complete below-60% window and recomputes job allocation and coverage. All four misses reached 60%: seed 20261019 improved 42.0%→80.0%, 20261020 24.1%→76.0%, 20261021 45.7%→80.0%, and 20261022 48.9%→80.0%. This points to a second source as sufficient in these four cases, but does not establish buildability, cost, placement, or realized flour production. It is a small diagnostic of only four deficient windows, not a new acceptance result; coverage remains an adult-equivalent capacity proxy.

### Physical second-source follow-up (2026-11-14)

Following the decision to allow a second windmill for coverage, the gate now permits at most two and makes the second wanted only while the town's modeled grain coverage is below 60%. A focused `FiefPowerTests` case checks that a 400-person town passes the second-windmill appetite gate below the target, reaches the target with its second source, and then stops; a third source remains blocked. A separate deterministic fixture uses an actual generated river-adjacent site, fills its natural fields, and confirms the water-mill gate opens despite an existing windmill; after setting that gate-approved water mill complete, the ledger reports both sources and allocates their combined power. A non-river site with natural fields is rejected. No flour stock is modeled; the gate and F-T2 ledger use adult-equivalent capacity as agreed.

**Natural second-source follow-up:** A fresh four-seed `fief-match` run used a temporary pack with all 50 E5 rows numeric only outside the repository, `--grant-e5`, the 130-minute horizon, and the shared power ledger. The seed-level population-weighted results were 77.0%, 71.5%, 75.8%, and 78.8%, all above the 60% screen. Across the cohort, **66 town windows were complete, 3 were censored, and 62/66 complete windows met the per-town 60% threshold**. Two windows naturally had a second same-type source: one town had two windmills but still reached only 57.1%, while another had two water mills. Every below-threshold complete window passed the virtual +1-source sensitivity. This historical diagnostic observed second sources in natural play, but does not establish second-windmill completion under the current source snapshot or guarantee that each town meets the local threshold. The independent cohort below contained no two-windmill town; treat natural second-windmill completion as unverified until a post-freeze current-source cohort records it.

**D19 F-T2 capacity-fit diagnostic (2026-11-14):** The next fresh four-seed run exposed why a third source could not be the general answer under D12: an inland town had two windmills, no river, and only 46.2% coverage during its first 12-season source window. The ledger now records site river adjacency, field count, water/wind build gates, and local wood/block/wheel stocks so such constraints are visible. A Fief-only fit raises the per-source adult-equivalent capacity parameter from the shared 200 to **260 residents per source**; the proxy formula, 60% threshold, two-windmill cap, power-hour conservation, and no-flour-store design stay the same. This fitted assumption gives two 80%-available windmills capacity for 416 adult-equivalent residents, enough for the observed worst town's roughly 415-person 60% threshold. With the candidate fit, the fresh 130-minute cohort (`20261019–20261022`) had **69 complete windows, one censored window, and 69/69 complete windows at or above 60%**. Per-seed weighted coverage was 80.0%, 79.9%, 79.7%, and 79.9%; all four power-hour residuals were 0.00. This is a four-seed diagnostic fit, not final F-T2 acceptance: independent and broader WP-E5-11 validation remains required, and no numeric catalog row is promoted.

**Fresh independent F-T2 validation (2026-11-14):** Four natural-start matches used the same 130-minute procedure and temporary promoted candidate pack, on seeds `20290001–20290004`, disjoint from the earlier fit and screen cohorts. The run recorded **61 complete town windows and 2 horizon-censored windows; all 61/61 complete windows reached ≥60%**. Both censored windows were in seed 20290003 (s31 c2: 3/12 samples at 80.0%; s77 c3: 9/12 at 100.0%) and are excluded from the gate. The per-seed population-weighted coverage and power-hour accounting were:

| Seed | Complete / censored | Town windows ≥60% | Weighted coverage | Offered | Grind / saw / craft | Unused | Residual |
|---|---:|---:|---:|---:|---:|---:|---:|
| 20290001 | 17 / 0 | 17/17 | 84.1% | 765.80 | 378.21 / 13.94 / 343.03 | 30.62 | 0.00 |
| 20290002 | 16 / 0 | 16/16 | 86.6% | 752.40 | 376.12 / 37.16 / 290.08 | 49.04 | 0.00 |
| 20290003 | 16 / 2 | 16/16 | 83.5% | 682.60 | 371.15 / 15.92 / 273.38 | 22.15 | 0.00 |
| 20290004 | 12 / 0 | 12/12 | 89.2% | 598.80 | 277.66 / 35.65 / 254.62 | 30.87 | 0.00 |

All complete town-window rates and censor records are retained in the [independent raw ledger](m12-wp8-fief-power-independent-output-2026-11-14.txt). Five windows naturally had mixed sources (one water mill plus one windmill); no two-windmill or two-water-mill case appeared in this cohort. Power reconciles exactly in every seed (`offered = grind + saw + craft + unused`). This independently repeats the four-seed diagnostic screen at the fitted Fief-only capacity; it does not replace broader WP-E5-11 validation, alter D12, or promote the candidate value.

**Planner feasibility follow-up:** Audit found that an in-progress windmill build became infeasible as soon as one windmill already existed, despite the D12 appetite gate allowing a second below 60%. The Fief-only feasibility rule now keeps that second build eligible at count one and blocks further builds at count two; Iron behavior is unchanged. The focused Release `FiefPowerTests` run passed **13/13** after the fix. The independent natural cohort above still had no two-windmill case, so natural completion of a second windmill remains unproven; broader WP-E5-11 validation remains open.

Reproduce with the temporary promoted candidate pack (run completed in about half an hour on this checkout):

```sh
dotnet run --project src/CivSim.Harness -- fief-match --pack /tmp/civsim-e5-fit-candidate.pack --grant-e5 --fief-power-ledger --minutes 130 --seed 20261019 --seeds 4
```

Raw outputs: [the original four-seed ledger](m12-wp8-fief-power-extra-mill-output-2026-11-14.txt), [the second-source rerun](m12-wp8-fief-power-second-source-output-2026-11-14.txt), [the 200/source buildability baseline](m12-wp8-fief-power-buildability-output-2026-11-14.txt), [the 260/source candidate fit](m12-wp8-fief-power-260-output-2026-11-14.txt), and [the independent 260/source validation](m12-wp8-fief-power-independent-output-2026-11-14.txt).

## Still open
Horse-gin power (needs a draft-power model), crucible steel, broader clock-tower balance validation, the other E5 systems, AI use of any of it, the game view, and promotion of the rows remain open. F-T2 passes this four-seed candidate screen at 69/69 complete town windows; the 260/source value is a diagnostic fit, not an independently validated acceptance value. F-T2 remains a capacity proxy; no flour stock is modeled by design. The broader held-out F-T4 estimate and fitted band are in [the land-food report](m12-wp8d-land-food-oracle.md); its empirical bands remain diagnostic pending user-approved tolerances. The F-T3 draft charcoal factor fit at 1.00 passes the specified 39-seed fixture mechanics gates: 36/36 mature pairs pass all rolling windows, three are censored for no mature window, 30/30 demand-eligible pairs pass fuel shortfall and nine low-demand pairs are censored, with zero furnace-attributed forest crossings and reconciled material ledgers. See the [factor-fit report](m12-wp8c-furnace-charcoal-factor-screen-2026-11-14.md). Whole-match F-T3 balance and the broader WP-E5-11 gate remain open.

## Reproduce the promoted pack
Copy `content/` to a scratch folder, change `status: draft` to `status: numeric` on every `era: 5` row and on KEY-6 (V22 wants all or none), then `civsim build-pack --content <folder> --out e5.pack` and run `iron-match --pack e5.pack --fief --grant-e5 --minutes 130`.

## WP-E5-8 implementation slice: powder, cannon and siege crews (2026-11-14)

The Fief-only economy now maps the existing Saltpeter and Sulfur deposit kinds to separate Fief stock slots. Deposits use the existing workability, research, depth, ownership, depletion, direct mining labor and mining-camp haul rules. Powder demand is advertised only after CHM-07, WAR-12, a blast furnace and powder mill, and only when each raw ingredient is already stocked or has a reachable workable source. Iron and Bronze do not unlock these deposit paths. The catalog recipe inputs and the deposit reserve quantities remain draft fit hypotheses.

Siege hosts can carry at most three cannons. Engines are assigned first; four adults crew each engine or cannon. A supplied cannon consumes 0.25 powder per shot and adds twice one engine's draft wall damage. An ordinary engine breach still removes the standing wall for the storm. A cannon breach retains the wall and reduces its wall multiplier by 70% for that storm; the castle and concentric-castle multipliers remain in force. Cannon inventory and breach state are hashed and serialized only in Fief matches. Unused cannons return to their home settlement.

**WAY-09 and SEA-11 route-speed; WAY-09 coast-outline slices:** after KEY-6 enables Fief sailing, completed WAY-09 and SEA-11 shorten actual sea-crossing routes using one deterministic integer rounding step for their catalogued ×1.3 and ×1.4 speed modifiers (×1.82 together). Land routes are unchanged and Iron remains inert. The Fief view builder also marks water tiles adjacent to the viewer's already observed land as explored after WAY-09; adjacent land remains unknown and tiles are not made currently visible. The chart effect derives from existing exploration plus static adjacency and adds no simulation state. Existing voyage coverage exercises WAY-09 speed and the KEY-6 ocean gate; no SEA-11-specific regression was added or run for this slice. The whole-match impact of the combined modifiers is unmeasured. No row is promoted.

Fief siege crews now have a keyed, per-adult, per-siege-turn attrition roll: draft 1.00%, multiplied by 1.25 against a garrisoned concentric castle when DEF-09 is complete. A deterministic 100,000-exposure-per-case test reports 1,006 losses (1.006%, 95% Wilson interval 0.945–1.071%) against 1,000 expected, and 1,244 (1.244%, interval 1.176–1.315%) against 1,250 expected. These are mechanic-level seeded diagnostics, not whole-match casualty estimates or a fitted balance result. Ocean sailing remains disabled in Fief after SEA-04 until KEY-6; non-Fief sailing is unchanged.

Focused tests cover local saltpeter/sulfur mining, an out-of-range owned-land camp and haul, no-source powder suppression, Bronze/Iron isolation, shot conservation, engine and cannon breach integrity, defense modifiers, and seeded attrition. `dotnet test src/CivSim.Tests/CivSim.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~FiefSiegeWeaponTests` passes 6/6. An integration filter first exposed four IronChain failures caused by selecting only Iron-specific recipes and dropping shared/base recipes such as bloomery. Iron recipe composition now includes shared plus Iron recipes and excludes Fief recipes. The combined focused filter for IronChain, Fief power/law, quarantine and siege tests passes 103/103. After restoring the Iron recipe composition, the current two-seed `iron-match` output (seeds 20261019–20) is byte-identical to both the saved pre-change and prior post-slice outputs. No 80-match or whole-match war/F-T4 balance cohort was run for this slice. The 1.00%, 1.25%, 0.25 powder, 2x wall damage, and ore capacities are all draft values and have not promoted rows.

## WP-E5-5 implementation slice: MEC-08 Clock Tower (2026-11-14)

The Fief-only `clock_tower` unlocks through MEC-08, costs 30 work, 10 block, 1 G-IRON, and 4 timber, and is buildable in a town. Each settlement within six inclusive hexes of a built tower receives a 5% scheduled-work capacity multiplier and +2 Cohesion target points. The work effect is applied at the shared work-capacity stage, before job allocation; the Cohesion term is local and does not change the global pace. Nearby settlements benefit regardless of which Fief civilization owns the tower. Multiple towers do not stack either effect.

The structure kind is appended to the Fief-only Iron snapshot tail. Iron and Bronze retain their prior snapshot layout and checksum behavior. `FiefClockTowerTests` covers the MEC-08/Fief/town gate, the inclusive six-hex edge and exclusion at seven, scheduled-work and Cohesion effects, Fief snapshot continuation, and non-Fief isolation; the focused run passed 3/3. This is an implementation slice only. The local effect, construction costs, and any balance impact remain draft and have not received a whole-match or cohort fit.
