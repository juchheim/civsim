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
