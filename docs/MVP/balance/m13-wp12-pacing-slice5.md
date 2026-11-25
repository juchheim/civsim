# M13 WP-E6-12 slice 5: Discovery cost re-fit to 18,000 (2026-11-25)

**Status:** content change on `codex/e6-discovery`. `content/registries/eras.yaml` Discovery `base_cost` 28000 to **18000**; the eight D9 late-project overrides follow at exactly twice the era base times their type multiplier (CHM-08, FIR-11, FIR-12, FIB-12, INQ-09 and MET-13 at 36,000; MEC-10 and TRD-11 at 54,000) with their `cost_reason` text saying "EraBase 18000". `tests/golden/content-pack.txt` and `playtest/discovery-numeric-e5.pack` refreshed (pack hash `e06a26fb...`).

## Why

[Slice 4](m13-wp12-pacing-slice4.md) fitted 28,000 to 25 minutes against real E5. Main then changed E5 (the Fief arms and material balance, E5 promoted to numeric), which [38 §9](../38-discovery-age.md) warns shifts every E6 baseline. On the merged build at 28,000 only three of twelve civilizations reached KEY-7 within 125 minutes (27 to 34 minutes), and the rest needed more than 26 to 36.

## Sweep (merged build with the five Renaissance rows; seeds 12345 to 12347; E5 granted at KEY-5; 125 minutes; stock AI)

Minutes from own KEY-6 to own KEY-7 (4 seasons a minute). A civilization that had not finished by minute 125 counts as unfinished.

| Era base | Finished (of 12) | Mean | Range | Builder / Grower / Warlord / Sage |
|---|---|---|---|---|
| 14,000 | 12 | 21.7 | 17 to 26 | 17.3 / 24.2 / 22.3 / 22.9 |
| 16,000 | 12 | 23.4 | 17 to 28 | 18.2 / 26.4 / 24.3 / 24.8 |
| **18,000** | **12** | **24.9** | **19 to 31** | 20.4 / 28.0 / 26.1 / 25.1 |
| 20,000 | 10 | 27.0 (at least 27.3 counting the two unfinished) | 21 to 33 | 22.8 / 28.9 / 29.1 / 28.7 |
| 23,000 | 9 | 29.3 (at least 29.0) | 21 to 35 | |
| 26,000 | 3 | 28.2 on the three finished; at least 30.6 over all twelve | | |

The curve is roughly linear below 20,000 (about 0.8 minutes per 1,000) and the unfinished civilizations make the averages above it optimistic. 18,000 is the value that gives 25 minutes with every civilization finishing.

## Not yet done

A held-out confirmation on other seeds (slice 4 did one at 28,000; this slice fitted on the three sweep seeds only). The slowest archetypes (Grower, Warlord) sit 3 to 5 minutes above the Builder; that spread is the stock AI's, not the cost's.

## Held-out confirmation of 18,000 (seeds 777 to 779, 125 minutes, grant-e5)

11 of 12 civs reached KEY-7: mean 25.25 minutes from KEY-6 to KEY-7, range 20.0 to 28.75 (the fit seeds gave 24.9). The twelfth civ
(seed 777, Warlord) never left era 2 (112 projects, no KEY-5), so it is a stalled early-game civ and not a Discovery pacing result; it is
not counted in the mean. The 18,000 fit holds on unseen seeds.
