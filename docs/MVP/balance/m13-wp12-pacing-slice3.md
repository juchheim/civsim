# M13 WP-E6-12 slice 3: fitting the E6 era cost to the 25-minute target (2026-11-21)

**Status:** applied on `codex/e6-discovery`, uncommitted. Decision D8 asked for **25 minutes from KEY-6 to KEY-7** (the Iron and Fief target); the first natural runs ([slice 1 and 2 report](m13-wp12-discovery-match-slice1.md)) took 45 to 56 minutes at the typed era base of 32,000. This slice fits that one number, the way E5's 6,400 was fitted. No simulation code changed; the only edits are content: the Discovery `base_cost` in `eras.yaml` and the eight D9 cost overrides (which are written as explicit numbers equal to twice the era base times the type multiplier), plus the `cost_reason` text that names the base.

## The runs

`discovery-match --pack civsim.pack --grant-e5 --minutes 150–200`, the stock AI, four civilizations a world, KEY-6 granted with E5 (E5 is still draft). The measure is **seasons from a civilization's own KEY-6 to its own KEY-7** (4 seasons = 1 minute).

| Discovery era base (D9 overrides scaled alike) | worlds | civilizations | KEY-6 to KEY-7 |
|---|---|---|---|
| 32,000 (as typed) | 20261019, 20261020 | 8 | 181 to 222 seasons, **45 to 56 min** |
| 16,000 | 20261019, 20261020 | 8 | 92 to 133 seasons, 23 to 33 min, mean about 30 min |
| **12,000 (applied)** | 20261019, 20261020, 20261021 | 12 | 80 to 111 seasons, **20.0 to 27.8 min, mean 24.6 min** |

At 12,000 the per-civilization spread is 20.0 to 27.8 minutes; the fastest civilization (Builder or Sage) lands at about 20 and the slowest (Warlord) at about 27. The curve is not linear in the base: halving the cost cut the age by more than half (45 to 56 became about 30) because Insight, diffusion discounts and the era lag all compound while the age runs. 12,000 is 1.9 times E5's 6,400; the age has 41 projects against E5's 50 and a 14-project breadth, so a base between 1.5 and 2.5 times E5's is plausible.

## What changed

- `content/registries/eras.yaml`: Discovery `base_cost` 32000 to **12000**.
- The eight D9 late projects (CHM-08, FIB-12, FIR-11, FIR-12, INQ-09, MET-13 at 2 x base x 1.0, and MEC-10 and TRD-11 at 2 x base x 1.5) went from 64,000 and 96,000 to **24,000 and 36,000**, with the `cost_reason` text now naming `EraBase 12000`.
- `tests/golden/content-pack.txt` refreshed (`e23cb73e…`).

## Caveats

1. **The fit is against the stock AI, four archetypes, three worlds.** A human plays differently (a human can pick the critical closure directly, which the AI also does); the large held-out sample (WP-E6-13) is where this number is confirmed or moved.
2. **E5 is still granted, not played.** The age starts at KEY-6 with all E5 projects free, so Insight and the diffusion state at KEY-6 are those of a civilization that has every E5 effect; when E5 is promoted the number should be re-run.
3. **KEY-7 itself is not repriced separately.** Keystone costs follow the same base; the closure (NRG-06, MEC-10, TRD-10, INQ-08) plus 14 breadth projects sets the time.
4. The Python oracles in `docs/MVP/balance/tools/e6_*` still assume 32,000 where they mention the base; they model system behavior, not research time, so none of their `--check` outputs change.
