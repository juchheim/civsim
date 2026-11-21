# M13 WP-E6-12 slice 1: the `discovery-match` diagnostic and the first whole-chapter runs (2026-11-21)

**Status:** built on `codex/e6-discovery`, uncommitted. This is the first slice of [WP-E6-12](../38-discovery-age.md), the harness that plays whole Discovery matches with the stock AI. It changes no simulation behavior; the one Sim change is that a diagnostic draft grant of an **era-5** project is now allowed in a Discovery match (it was already allowed for the era-6 ceiling), which touches nothing unless the harness sets the diagnostic flag.

```bash
dotnet run --project src/CivSim.Harness -c Release -- discovery-match --pack civsim.pack --seed 20261019 --minutes 140 --grant-e6
```

`--grant-e5` grants a civilization the E5 projects (except KEY-6) when it completes KEY-5, as `fief-match` does. `--grant-e6` also grants **KEY-6** with them (the stock AI does not research a draft keystone) and every E6 project except KEY-7 once KEY-6 is done, so the E6 systems can be watched before the age is promoted. The output is one line per seed (ticks, end state, checksum), one per civilization (KEY-5, KEY-6 and KEY-7 seasons, projects, era, Insight a year, KEY-6 progress and cost) and one line of E6 structures built and settlements holding coal.

## What the first runs found (seeds 20261019 and 20261020, 140 minutes, `--grant-e6`)

| | seed 20261019 | seed 20261020 |
|---|---|---|
| KEY-5 (all four civilizations) | seasons 348 to 360 (about 87 to 90 minutes) | seasons 364 to 382 |
| E6 granted, era 6 | the next season | the next season |
| atmospheric engines | 35 | 43 |
| coke ovens | 37 | 42 |
| navigation canals | 33 | 35 |
| factories | 35 | 42 |
| galleons | 7 | 5 |
| exchange houses | 4 (one per civilization) | 4 |
| bastion forts | 0 | 0 |
| settlements holding coal | 96 of 127 | 105 of 128 |
| determinism | the same seed twice gave the same checksum `360fee40ba697d8c` | |

1. **Nothing crashed and the runs are deterministic.** About 50 minutes of match time after KEY-6, in two worlds, the natural planner raised dozens of engines, ovens, canals and factories, coal is held in four of five towns, and every civilization built exactly one exchange house (the one-per-civilization gate working).
2. **The age is not reachable without work on the AI.** With the E5 projects granted and KEY-6 left to the AI (`--grant-e5` alone) **no civilization researched KEY-6 in 130 minutes**: progress stayed at zero for all four with 20,000 to 25,000 Insight a year against a cost of 16,000. The AI's research planner only considers numeric rows, so draft keystones are invisible to it. This is exactly the WP-E6-10 and promotion work: KEY-7 routing and the Forecast catalog cannot plan a draft row, so promoting the 42 rows together (WP-E6-12) is a prerequisite for any natural run to KEY-7.
3. **E6 starts at about minute 90.** KEY-5 lands around season 350 to 380 in every run, so a Discovery match that reaches KEY-7 in the design's 25 more minutes (D8) would end near minute 115 to 120. The E5 grant is what makes this horizon reachable; E5's own pacing is the E5 balancer's.
4. **No bastion forts were built in 50 minutes.** A bastion needs a garrisoned castle, and castles are rare in these runs; whether the AI should want them is a WP-E6-10 policy question, not a gate fault.
5. **Harbors stay few (7 to 11 across 117 to 128 settlements).** Joint-stock and insurance need two harbor towns per civilization, so the pooled-goods rule of D12 will often be closed to a civilization that builds one port; the AI's harbor policy decides how much of TRD-10 and TRD-11 is felt.

## Not done (the rest of WP-E6-12)

1. **Promotion** of all 42 rows together (draft to numeric), with the pack and its golden, and the Forecast catalog and Council prior to follow (WP-E6-10).
2. A **natural-play measurement** of coal, coke and steam share, colonies founded and lost, voyage losses, the factory's labor effect, pollution and KEY-7 timing over a large sample (WP-E6-13), which needs promotion first.
3. Replay and snapshot acceptance for Discovery matches (record and resume through the harness), as `fief-match --record-dir` does.


## Update, same day: the 42 E6 rows are promoted (WP-E6-12 slice 2), and the AI plays the age

**Promotion.** All 41 E6 projects and KEY-7 went from draft to numeric together (rule V23: no half-promoted age). `civsim validate --strict` stays at 0 errors and 0 warnings after four practice lists were corrected so each feeds its own lineage (INQ-08 and INQ-09 now `scout`, MEC-09 `build`, TRN-11 `haul`; the practice registry was not touched, so the E0 to E4 Know-how numbers do not move). The pack is 313 numeric projects (271 for E0 to E4 plus 42) and 206 draft (E5 and E7 to E9); the content-pack golden is refreshed (`b1964600…`). **The research, politics and toy finals are unchanged** (`cb6fd7b2b6ebfd06`, `5d3ebe260a0f55b5`), because the chapters below Discovery cannot reach an E6 row. E5 stays draft (its own balance work), so a natural Discovery run still grants E5 with `--grant-e5`, KEY-6 included.

**The first natural runs** (`discovery-match --grant-e5`, 200 minutes, two worlds, no E6 grants; the AI researches the E6 rows itself):

| | seed 20261019 | seed 20261020 |
|---|---|---|
| KEY-6 (granted with E5) | seasons 352, 360 to 362 | 354, 365, 376, 385 |
| **KEY-7, civilization by civilization** | 540, 574, 581, 581 | 535, 571, 594, 598 |
| seasons from KEY-6 to KEY-7 | 188, 212, 220, 221 | 181, 206, 209, 222 |
| engines / ovens / canals / factories | 44 / 41 / 39 / 40 | 40 / 36 / 34 / 36 |
| galleons / exchange houses / harbors | 11 / 11 / 12 | 7 / 6 / 7 |
| bastion forts | 0 | 0 |
| settlements holding coal | 82 of 124 | 72 of 118 |

1. **The stock AI reaches KEY-7 in every civilization in both worlds, with no help.** It researches the promoted E6 rows, the whole critical closure (NRG-06, MEC-10, TRD-10, INQ-08 and their prerequisites) and 14 breadth projects. WP-E6-10 is therefore **not** a blocker for the age to be playable; it remains for tuning (coal, ocean, colony and bastion policy, Council prior).
2. **Pacing is about twice the design.** KEY-6 to KEY-7 takes **181 to 222 seasons, 45 to 56 minutes**, against the 25 minutes of decision D8 (the 25-minute age was the recommendation, not a fitted number). The Insight rate (27,000 to 34,000 a year) is not the problem; the era cost base is: E6 costs, with the late doubling of D9, make the age about twice as long. This is the number to tune next (the era base of 32,000 flagged "unfit" earlier), not any system.
3. **Natural play builds the systems.** Dozens of engines, ovens, canals and factories appear without any help; the AI builds more galleons and exchange houses (6 to 11) than in the granted runs because it had more time. **No bastion fort was built:** a bastion needs a garrisoned castle and the AI builds few castles, so DEF-10 is unexercised by natural play.
4. **A Discovery match does not end when an AI civilization completes KEY-7** (only the human ending ends it; decision D11 has no rival countdown), so the runs above continued to their 200-minute limit with every civilization in era 7.

Two more notes: the run of 200 minutes takes about 4.8 minutes of wall time; and `--grant-e6` remains for watching the E6 systems before the AI reaches them.
