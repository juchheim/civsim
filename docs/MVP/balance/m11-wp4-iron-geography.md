# M11 WP4 — Iron geography audit (B4-10)

**Status:** audit only. No world generation, worldgen golden, simulation golden or E0–E3 behavior was changed, and no overlay was added or activated. The proposed guarantee ("a viable iron route for every start") is **not met for every start**; the shortfall is small and is measured below. **Q9 decided 2026-11-03: accept trade/expansion for the tail; no overlay** (see §10). Q9 in [25 §15](../25-iron-age.md#15-risks-and-unresolved-decisions) is resolved accordingly.

Raw outputs are in [m11-wp4-iron-geography-output.txt](m11-wp4-iron-geography-output.txt). Every number below is copied from it.

## 1. Result in brief

| Question | Answer (seeds 1–10,000; 40,000 starts, 20,000 geometrically distinct) |
|---|---|
| Does every start know iron at MET-08? | **Yes.** 40,000/40,000 have at least one deposit within 20 tiles (median 11). 98.9% have a depth-1 deposit within 20. |
| Local route on E3 rules, no conditions (steps 1–2) | **93.9%** (37,540). Without SEA-05: 88.2% (35,276). |
| Local route if Shaft Mining (MIN-04) counts (steps 1–3) | **99.3%** (39,728). |
| Starts outside steps 1–3 | **272 (0.68%)** = 136 distinct starts in 120 of 10,000 worlds (1.2%): 48 only reach deposits nearer a rival start, 224 know iron but only beyond every mining reach. |
| Of those 272, a capable partner exists geographically | 208 (partner at step 1–3) or 192 (partner at step 1–2). **64–80 starts (0.16–0.20%) have none**: no other start reaches step 1–3 (64) or step 1–2 (80). |
| Low-wood or inland starts worse off for iron? | **No.** Iron-route rates are 93–94% (steps 1–2) in every wood stratum and 99.5% (steps 1–3) in the inland and deep-inland strata. Zero starts are landlocked. |
| Is fuel an independent problem? | **Yes, and larger.** Regrowth within reach 8 sustains fewer than 100 adults of T11 replacement at 45.6% of starts, fewer than 50 at 22.9%. See §6. |
| Does the live game use the geography? | **Barely.** In 187 sampled civilizations only 80 had mined any iron from their own land by their own KEY-4; the median is 0.0 L; no iron camp was open at any sampled KEY-4. See §8. |
| Can a trade route stand in for local iron today? | **Not in code.** `TradeGood` has no iron, and barter parties carry food only. Trade figures here are geometry, not a working route. See §7. |

**Reading for Q9.** Existing geography satisfies "known iron for every start" and gives 99.3% an unconditional local site. It does not satisfy the literal "viable route for every start". The user decided to accept trade or expansion for that tail (§10); no overlay is added.

## 2. Reproduce

```bash
export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec
dotnet run --project src/CivSim.Harness -c Release -- iron-audit --seed 1 --seeds 10000 --csv /tmp/iron-audit.csv   # about 25–30 min on 5–6 threads
python3 -I docs/MVP/balance/tools/iron_audit_csv_supplement.py /tmp/iron-audit.csv
dotnet run --project src/CivSim.Cli -c Release -- build-pack --out /tmp/wp4.pack --check-golden tests/golden/content-pack.txt
dotnet run --project src/CivSim.Harness -c Release -- iron-live --pack /tmp/wp4.pack --seed 20261019 --seeds 24 --minutes 80
dotnet run --project src/CivSim.Harness -c Release -- iron-live --pack /tmp/wp4.pack --seed 20261100 --seeds 24 --minutes 80
dotnet run --project src/CivSim.Harness -c Release -- politics --pack /tmp/wp4.pack --check-m10 --seed 20261019 --seeds 24 --minutes 80
dotnet run --project src/CivSim.Harness -c Release -- politics --pack /tmp/wp4.pack --check-m10 --seed 20261100 --seeds 24 --minutes 80
```

- **Identity.** Source `6dcdd6bb187a8de569e96bc222ba99b84afd9666` on `codex/e4-iron` plus the uncommitted WP2/WP3 work. Pack SHA-256 `749f7fb0ca6ea4cba0bccebfbbb821b621df2fa8591aef080bf60723756f46ce` (345,022 bytes); `build-pack --check-golden` passed.
- **Audit digest** over all 40,000 per-start records: `886df080d45a6021da720673de6a66725ce5c4241d466775f42eab2a051323da`.
- **Repeatability.** Two independent builds with different thread counts (5 and 3) produced **byte-identical** reports (SHA-256 `c444daae…ea8b9f` for both). The report is a function of the seed range alone: no clock, no thread-order effect. A 1-thread and a 9-thread run of seeds 1–40 were also identical, and `IronGeographyAuditTests` pins thread independence in the suite.
- **Mirror self-check.** Civ 2 and 3 are the 180° rotation of civ 0 and 1. All local columns of the 20,000 mirror pairs match exactly (0 differences), which validates the code against the generator's symmetry.
- **Code.** [IronGeographyAudit.cs](../../../src/CivSim.Harness/IronGeographyAudit.cs) (new, read-only: it calls `WorldGenerator.Generate` and `WorldEconomyInput.FromGeneratedWorld` and writes nothing back); four added lines in `Program.cs` (two dispatch entries, two usage lines) register `iron-audit` and `iron-live`.

## 3. Method and definitions

Seed range **1–10,000**, the range of the M2 acceptance audit ([17 §3.1](../17-vertical-slice.md)). The range is contiguous, not a random sample.

**Denominators.** 10,000 worlds; 40,000 starts; 20,000 geometrically distinct starts (the generator is point-symmetric, so civ 2 and 3 repeat civ 0 and 1 exactly; verified). Every table counts all 40,000. The distinct-start percentages are identical to one decimal place.

**World truth versus knowledge.**

- *World truth:* every iron tile on the map (64 per world, 32 per main continent; about half are depth 2), regardless of who can see it.
- *Known:* what MET-08 reveals, "iron deposits within 20 tiles of every own settlement" ([met-metallurgy.yaml](../../../content/catalog/met-metallurgy.yaml)). The audit uses the start site only, so it understates what a civilization with about ten settlements knows.
- *Reach:* hex distance from the settlement site, as the economy does (`MineReach` 8; 12 with TRN-05; 16 with SEA-05 when the site is on a river, coast or lake). TRN-05 is a KEY-4 prerequisite through TRD-05; SEA-05 is not.
- *Uncontested:* strictly nearer this start than any other start (ties go to nobody). A static stand-in for ownership; live claims differ.
- *Mineable:* depth 1 at once; depth 2 needs MIN-04 (also a mine head). Each iron deposit holds 1,000 L (`OreLitres`).

**Iron-route ladder** (each start takes the first step it satisfies):

| Step | Meaning |
|---|---|
| 1 | an uncontested depth-1 deposit within 8 |
| 2 | …within 12, or 16 on a wet start site (TRN-05, SEA-05) |
| 3 | an uncontested depth-2 deposit within that reach (needs MIN-04) |
| 4 | only deposits nearer a rival start within that reach |
| 5 | iron is known (within 20) but beyond every mining reach |
| 6 | no iron within 20 |

**Wood.** Forest tile = at least 15 L standing wood (the economy's own test), taken only where the tile is nearer this start than any other. Regrowth is the economy's flat 2 L/yr per wooded tile. Demand comes from [e4_metal_oracle.py](tools/e4_metal_oracle.py): T11 replacement needs 22.29 L wood per 100 adults per season (0.892 L per adult-year), T12 half the kits.

**Trade geometry.** A partner is *capable* if its own start reaches step 1 or 2. Route = unit-cost walk (as `PoliticsSystem.Travel`), else sail with SEA-04 (open water within 11 tiles of land). Seasons = tiles / 10.

**Live probe.** `iron-live` is the planner-only stand-in used by the WP2 E3 anchor (four Balanced civilizations, Council), sampled once per civilization at its own KEY-4. It is not the AI politics harness and not human play.

**Fixed before and changed after looking.** The step ladder, the 8/12/16 reaches, and the 100- and 50-adult wood thresholds were fixed in the first version and never changed; the first full run (digest `9db99306…`) and the final run give identical step counts. After a 40-seed pilot used to debug the code, and after reading the first full run, I added reporting only: a bottom-decile wood stratum and threshold table, the SEA-05 split of step 2, a deep-inland stratum, the within-3-tiles measure, the no-walking-route diagnostic, and live MIN-04/TRN-05/SEA-05 and iron-camp counts. A tie-break artifact in the walking-cost column (found by the mirror check) was also fixed. Those additions change the digest, which is why the final digest differs.

## 4. World truth versus what a civilization can know

| Measure (per start, hex tiles) | p5 | p50 | p95 | max |
|---|---:|---:|---:|---:|
| nearest iron, any depth | 1 | 5 | 11 | 20 |
| nearest depth-1 iron | 2 | 7 | 15 | 34 |
| nearest uncontested depth-1 iron | 2 | 7 | 16 | 48 |
| iron known at MET-08 (count within 20) | 5 | 11 | 16 | 25 |

Iron is always inside the revealed radius; the question is whether it is inside a *mining* reach and mineable. Depth is the main limiter: 5.5% of starts (2,188) have nothing but depth-2 iron inside the extended reach.

## 5. Local mine, mineability and camp capacity

| Step | Starts | Share |
|---|---:|---:|
| 1 depth-1 within 8 | 26,712 | 66.8% |
| 2 depth-1 within 12 / 16 | 10,828 | 27.1% |
| 3 depth-2 (MIN-04) | 2,188 | 5.5% |
| 4 contested only | 48 | 0.1% |
| 5 known, beyond reach | 224 | 0.6% |
| 6 none within 20 | 0 | 0.0% |

Of the 10,828 step-2 starts, 2,264 depend on SEA-05; the rest need only TRN-05. Steps 1–2 without SEA-05 cover 88.2%, with it 93.9%. Coastal (<= 8), inland (9–20) and deep inland (15–20) starts behave alike (steps 1–3: 99.1%, 99.5%, 99.5%).

**Stock and throughput are not the constraint.**

- Depth-1 uncontested sites within reach: median 3, p25 2.
- One deposit (1,000 L) covers 722 adults' T11 ore need for 68 seasons (17 simulated minutes); the reference need is 2.04 L ore per 100 adults per season.
- A minimum camp (2 miners) mines about 2 L/season, a 4-miner camp 4 L/season: roughly 98 and 196 adults' need.
- Hauling to the nearest site costs a median 0.28% and at most 0.75% of adult work.

**Walking caveat.** For 90 steps 1–2 starts (0.23%) no eligible site has any walking route (separated by water or peaks); 28 more need more than 40 tiles. The planner opens camps by hex reach, so this only matters for decreed camps.

**The live planner cannot open iron camps.** `MaintainCamps` skips every ore except copper, arsenical copper and tin. Without a Mine Decree iron is dug only within 3 tiles of a settlement site (`Radius = 3`). That holds at the start site for 17.6% of starts (depth 1) and 30.6% (any depth); depth-1 iron lies inside the E3 claim floor of 4 at 27.4%. So the "camp capacity" the geography offers is far larger than what the current economy can use. This is an implementation gap for WP6, not a geography gap.

## 6. Wood regeneration, low-wood and inland starts

Adults of T11 replacement that regrowth alone can sustain (forest tiles only, reach 8):

| p5 | p25 | p50 | p75 | p95 |
|---:|---:|---:|---:|---:|
| 2 | 56 | 109 | 183 | 358 |

Reach 12 (TRN-05) raises the median to 296; T12 (half the kits) to 215; WOD-04 adds only 3%.

| Regrowth sustains fewer than | Starts | If half the regrowth is already spoken for |
|---|---:|---:|
| 25 adults | 14.3% | 22.9% |
| 50 adults | 22.9% | 45.6% |
| 100 adults | 45.6% | 78.0% |
| 200 adults | 78.0% | 98.0% |

- 1,800 starts (4.5%) have **no** forest within 8; the bottom decile has 6 or fewer forest tiles.
- Standing forest within 8: median 1,210 L, a one-time store that covers about 14 years for 100 adults before regrowth is needed.
- Wood and iron are independent: steps 1–2 are 93.9%, 94.2% and 93.3% for starts that sustain 100 or more, 50–99, and fewer than 50 adults; steps 1–3 are 99.3%, 99.4% and 99.3%. The 1,800 zero-forest starts: 63.4% step 1, 29.3% step 2, 6.1% step 3.
- **Landlocked.** None. Every start has a coast-adjacent tile within 20 (median 9, max 20), which is the generator's own start guarantee. 17,094 starts are coastal (<= 8), 22,906 inland, 3,038 deep inland (15–20).
- **Inland and low-wood together** (10,310 starts): steps 1–2 95.0%, steps 1–3 99.6%.

These figures count only the iron-tool charcoal draw. E3 bronze, hearths and construction use the same woods. They measure whether fuel can bind, not whether it does; I-T6 remains open.

## 7. Import alternatives

Geography only. Contact, relations, war and an iron trade good are not modelled.

- The mate is on the same continent at every start, 21–27 walking tiles away (median 23, 3 seasons). 93.9% of mates are capable.
- The other continent is never walkable. A capable partner there exists at 97.1% of starts and is reachable by sea at all of them: 24–113 tiles, 3–12 seasons (median 9).
- Of the 272 starts outside steps 1–3: 192 have a capable partner at step 1–2 (all of them the walkable mate); 208 with a partner at step 1–3. **80 (or 64) have none.** Those are 40 (32) distinct starts; 16 worlds (0.16%) have a whole neighbor pair, and its mirror, outside steps 1–3.

**Why this cannot yet count as a route.** `TradeGood` is `Tin, Copper, Bronze, Salt, Gold, Food, Luxury`; a Compact can carry only those, and Barter and Gift parties take food only. G-IRON has a barter value of 15 but no live path to move it. Contact and relations are also not guaranteed: see §8. A trade route for iron is a WP6/WP8 design item, not an existing alternative.

## 8. Own-era exposure and actual border contacts (B4-10)

Two 24-match blocks reused from the WP2 anchor (seeds 20261019–20261042 and 20261100–20261123, 80-minute limit). The AI politics harness reproduces the anchor exactly.

| Measure | Block 1 | Block 2 |
|---|---:|---:|
| Own KEY-4 reached, AI politics harness | 94/96 | 96/96 |
| Paired borders touching at own KEY-4 (official B17 count) | **86/94** | **85/96** |
| KEY-3→KEY-4 exposures complete / incomplete | 94 / 2 | 96 / 0 |
| Own KEY-4 reached, `iron-live` stand-in | 94/96 | 93/96 |
| Incomplete exposures (no KEY-4 by minute 80), stand-in | 2 | 3 |
| Mate contacted at own KEY-4, stand-in | 94/94 | 93/93 |
| Contact with the other continent, stand-in | 80/94 | 71/93 |
| Mate borders touching, stand-in | 14/94 | 14/93 |
| Median own E3 exposure (KEY-3→KEY-4), stand-in | 20.3 min | 19.9 min |
| Median MET-08→KEY-4, stand-in | 6.4 min | 6.2 min |
| Settlements per civilization at KEY-4, stand-in (p50) | 9 | 9 |

Incomplete exposures are counted separately and never as zero. Use the AI politics counts for borders: the stand-in expands less (14 touching versus 86 and 85). Contact is not border contact. In the stand-in every civilization had met its mate by KEY-4, but only 14/94 shared a border, which matters for trade because a Compact needs contact and Relations of 0 or more, not a shared border.

**Iron in actual play, stand-in at own KEY-4:**

| Measure | Block 1 (n=94) | Block 2 (n=93) |
|---|---:|---:|
| Known iron deposits within 20 of an own settlement (p50) | 19 | 19 |
| Iron deposits inside own territory (p50) | 4 | 4 |
| Worked an owned iron deposit by KEY-4 | 39 | 41 |
| Litres mined from owned iron (p50 / p90 / max) | 0.0 / 2.4 / 5.0 | 0.0 / 3.9 / 7.0 |
| Iron bloom in stock (p50) | 0.0 L | 0.0 L |
| Iron camps open | 0 | 0 |
| TRN-05 / MIN-04 / SEA-05 / WOD-04 done | 94 / 94 / 83 / 94 | 93 / 93 / 77 / 93 |

KEY-4 needs MET-08, yet more than half of civilizations never mined owned iron. The E3 economy asks for one litre of bloom and has no iron consumer, so E3 play says nothing about sustained iron supply. It does show MIN-04 is done by KEY-4 for every sampled civilization, so step 3 is realistic in this harness, while SEA-05 is not guaranteed (83/94, 77/93).

## 9. What the audit cannot establish

- **Live E4 supply.** No E4 economy exists. The audit shows where iron is, not that a settlement can staff, fuel, haul and forge it (I-T2, I-T3, I-T6 stay open). It does not substitute for any B4-06 case.
- **Hidden information.** The audit sees the whole map. A civilization knows only what MET-08 reveals around its own settlements and what scouts or neighbors showed it. Whether an AI believes a partner has iron is not modelled (A8 fuzz belongs to WP9).
- **Ownership.** "Uncontested" is a static nearest-start rule. Real claims, wars, captures and secession move deposits between owners; the 2,188 + 272 starts outside steps 1–2 are not a forecast of who actually mines what.
- **Daughter settlements.** Only the start is audited. A civilization with about ten settlements reaches more iron; that can rescue some of the 224 step-5 starts and understates coverage generally.
- **Contact, relations, route safety, interception, plague, ship capacity and cargo.** Only path existence and length are measured. A sea route also needs SEA-04 (E2) and risks shipwreck.
- **Wood sharing.** The wood tables count the iron-tool draw only; the real margin is smaller. Coppicing, haul from beyond reach and imports are partly covered by the reach-12 and WOD-04 rows only.
- **The live stand-in.** Four Balanced Council civilizations, no Strategist. Its border and contact counts differ from the AI harness, and 24-match blocks are descriptive, not acceptance samples.
- **Generalization.** Seeds 1–10,000 on the standard 160×100 four-player map. Other sizes, player counts or later generator changes are out of scope.
- `TerritoryFairnessSimulator` is not used as evidence of the live economy. It is only an input to world generation.

## 10. Q9: shortfall, options and a stop

**Measured shortfall (literal guarantee).** 272 of 40,000 starts (0.68%; 136 distinct; 120 worlds) have no unconditional local site even with TRN-05, SEA-05 and MIN-04. 64–80 of those (0.16–0.20%) also lack a geographically capable partner, and no iron trade path exists in code.

**What would have to be placed under an overlay.** Counts of starts that would need one added uncontested depth-1 deposit within 8 to reach each target (halve for distinct starts):

| Target | Starts needing a deposit |
|---|---:|
| step 1 for every start | 13,288 (33.2%) |
| steps 1–2 without SEA-05 | 4,724 (11.8%) |
| steps 1–2 | 2,460 (6.2%) |
| steps 1–3 (every start has any unconditional route) | 272 (0.68%) |

**Decision (2026-11-03, user): option 1, accept trade/expansion. No overlay, no worldgen change.** The options below are kept as the record of what was weighed; none was implemented.

1. **Accept trade dependence for the tail.** Keep worldgen; reword the guarantee to "known iron for all, an unconditional local route for 99.3%, trade or expansion for the rest". Needs an iron trade good and planner iron camps (both already required elsewhere in E4).
2. **Narrow overlay.** An Iron-only, separately seeded, point-symmetric overlay for only the 272 starts. It must stay inert in Bronze mode and before KEY-4, because MET-08 reveals and settlements can dig deposits inside 3 tiles in E3; a visible new deposit would move E3 trajectories.
3. **Wider overlay** for the steps 1–2 targets above. Larger and riskier for isolation; changes who is nearest to what.
4. **Settlement-based wording.** Define the guarantee over a start plus its first daughter settlements. The audit cannot size this without the live expansion model.

**Read at the time of the decision.** The geography is not what will bind first. In order: the live economy cannot open iron camps and has no iron trade; fuel regrowth falls short for 45.6% of starts at 100 adults; and only 43% of civilizations mine any iron before KEY-4. The 0.68% tail is real but small. I recommend deciding the guarantee wording and the iron trade/camp scope (WP6) before any overlay, and I have not started one.

**Consequences of the decision.** The E4 guarantee reads: known iron for every start; an unconditional local route for 99.3% (with MIN-04); trade or expansion for the remaining 0.68%. An iron trade good and planner iron camps (WP6) become load-bearing for that tail instead of optional, and the 64–80 starts with no capable partner rely on expansion alone. Worldgen and all goldens are untouched.

## 11. Verification

- `worldgen --seed 42 --check-fingerprint tests/golden/worldgen-v1.txt`: passed (`ea2cbb3fd75bd7c2`); `git status` shows no change under `src/CivSim.Sim/World` or `tests/golden/worldgen-v1.txt`.
- `toy --check-golden tests/golden/toy-v1.txt`: passed. `validate --strict`: 0 errors, 0 warnings. `build-pack --check-golden`: passed; no golden was refreshed.
- New tests ([IronGeographyAuditTests.cs](../../../src/CivSim.Tests/IronGeographyAuditTests.cs)): the report is identical for 1 and 4 threads, and the seed-42 fingerprint is unchanged before and after an audit.
- Full Release suite (`dotnet test CivSim.slnx -c Release`): **926/926 passed** (the 924 baseline plus the 2 new tests), after all code changes. The research and politics CLI goldens were not re-run here; nothing under `CivSim.Sim` was touched.
