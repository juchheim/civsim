# M12 WP-E5-0: timber and stone for Iron construction (2026-11-13)

**Status:** built and tested on `codex/e5-fief`. It changes **Iron-chapter behavior** (and so Faith & Fief, which contains it) and leaves Bronze exactly as it was: every change is gated on the Iron chain, and the toy, research and politics determinism checks still pass. The Iron balance was just closed ([m11-b4-06](m11-b4-06-metal-fuel-acceptance-2026-11-06.md)), so the effect on its gates is reported below, not assumed. Raw outputs: [all changes, 8 matches](m12-wp7-all-changes-output.txt), [all changes, 4 long matches](m12-wp7-long-all-output.txt), [baseline accounting](m12-wp7-baseline-it6-output.txt).

## The problem, measured

In natural Iron play almost no Wonder is ever built and a steady 10% of towns stand with a fully worked structure waiting for materials. The first guess (wood is scarce) was wrong:

| Finding | Number |
|---|---|
| World forests at the end of an Iron match | **93% of their starting stock** (60,460 → 56,004 L); regrowth 115,000 L; Iron-era metal and chop removed about 115,000 L |
| What stuck towns lack (8 matches) | block 138, wood 131, bloom 41, ashlar 28, wheel 9 |
| Wood a stuck town can see | its own range (radius 3) holds 0–550 L, its Iron reach holds **7,000–18,000 L** |
| Stone a stuck town can see | **0 L** quarry stone within 8 tiles, while sister towns hold **40–130 L of dressed ashlar** each |

So the shortage was **distribution, not supply**: timber was never requested for an Iron structure, wood could be felled only inside a town's own 3-tile range, and a town's finished stone could not leave it. The E3 ziggurat shows the same pattern (never built in natural play); the Iron Wonders (70 to 140 L of stone each) are in the same position.

## What changed (all Iron chapter only, each behind a diagnostic switch)

1. **Timber is part of the wood target** (`IronBuildWoodProcurementNeed`, one line in the planner): a town building an Iron structure asks woodcutters for its timber the way the basic list and a player's build focus already do (docs/MVP/30).
2. **Felling reaches 8 tiles** (`HarvestReachWood`): when a town's own range is felled out, the rest is cut from forest tiles within 8, in proportion to standing stock, never from a rival's tile and never below the 5 L that keeps a tile forest. The wood moves from the forest ledger to the store at the full amount, so the forest and local-stock ledgers still close exactly. This is the same 8-tile reach stone has had since doc 28 §9.
3. **Sister towns lend stone** (`ShareBuildStone`): within the existing sharing reach (SOC-07 / GOV-04), a town gets the ashlar and blocks its structure under construction lacks from sister towns' spare stone (ashlar for ashlar, blocks for blocks, then spare ashlar for any block still short). A donor never gives what its own structure needs. Physical stone moves between stores, so none is created. This is the same kind of transfer as public grain and Iron kits.

Harness switches for A/B work: `iron-match --no-build-wood --no-chop-reach --no-stone-share`; `CIVSIM_WOOD_DIAG=1` prints the wood and stone context of every stuck town.

## Results (same 8 seeds, 20261019–26; baseline is the merged build before the change)

| | Baseline | + timber | + timber + reach | **all three** |
|---|---|---|---|---|
| Iron structures built (8 matches) | 2,634 | 2,617 | 2,636 | **2,888** (+10%) |
| Wonders finished (of 24 possible) | 0 | 0 | 1 | **11** (6 Dome, 4 Library, 1 Lighthouse) |
| Stuck-town reports | 347 | 306 | 331 | **282** |
| of which wood / block | 131 / 138 | 108 / 124 | 109 / 150 | 95 / 119 |
| KEY-5 median (minute) | 92.2 | 91.9 | 91.8 | 91.3 |
| Own KEY-4 → KEY-5 median | 20.0 | 20.0 | 20.2 | 20.3 |
| People / soldiers (medians) | 1,911 / 121 | 1,906 / 136 | 1,948 / 81 | 1,936 / 108 |

The long, time-unlimited case matters most for E5, which has no countdown (four matches, all Iron projects granted at KEY-4, 150 minutes): baseline **0** Wonders and 1,871 structures against **11 of 12** Wonders and 2,212 structures with all three changes; stuck-town reports fall from 188 to 84.

Timber alone and felling reach alone each do little: a stuck town lacks wood **and** stone together, so only fixing both lets a large work finish. Stone sharing is the change that unlocks the Wonders.

## Does it hurt the Iron gates? (four seeds, `--it6-accounting`)

| | Baseline | All three |
|---|---|---|
| Forest stock, start → end (seed 1) | 60,460 → 56,004 L | 60,460 → 56,253 L |
| Clearing crossings, 4 seeds | 337 / 216 / 138 / 150 | 304 / 225 / 148 / 143 |
| Iron-era-metal clearing crossings (the approved I-T6 clause) | 0 | **0** |
| Fuel-gate screen lines | 218 PASS, 0 FAIL | 163 PASS, 0 FAIL (more windows censored: 247 against 197) |
| Full-chain work share per civ | 3.3%–7.7% | 3.6%–7.5% |

No gate flips and the forest ledgers close (residual 0.00 L). The count of qualifying fuel windows is lower because route timing shifted slightly, so the screen is **not** a replacement for the frozen B4-06 block: if the Iron balance owner wants this change inside B4-06, the protocol needs a rerun.

## For E5

The E5 structures typed in [27](../27-faith-and-fief-age.md) (monastery, university, cathedral, blast furnace and the rest) will be added to the same Iron construction framework, so they inherit all three changes. The Gothic Cathedral (40 ashlar, 20 wood, 4 glass) is smaller than the Great Dome, and E5's lack of a countdown gives it the time the Iron Wonders lacked. The numbers above say a Wonder is now reachable in a long game; they do not say how often a typical 25-minute E5 civilization will build one.

## Not done

- Large works still take one structure at a time per town at 0.20 of its labor, and stone (not renewable) will run out in a long game; a Wonder-specific pooling across the realm is a possible next step and is not built.
- Haul cost for wood felled 4 to 8 tiles away is not modeled (the labor spent chopping is unchanged), as for stone in doc 28.
- Timber for the **basic** list is untouched, and so is the Bronze economy.
