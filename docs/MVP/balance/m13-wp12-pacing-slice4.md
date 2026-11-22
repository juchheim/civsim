# M13 WP-E6-12 slice 4: the Discovery era cost re-fitted against real E5 (2026-11-22)

**Status:** content change on `codex/e6-discovery`, uncommitted. `content/registries/eras.yaml` Discovery `base_cost` 12000 to **28000**; the eight D9 late-project overrides follow (2 x base x type multiplier: six M projects 24000 to 56000, two I projects 36000 to 84000) and `tests/golden/content-pack.txt` is refreshed.

## Why

The earlier fit ([slice 3](m13-wp12-pacing-slice3.md), base 12000, mean 24.6 minutes) used the harness's `--grant-e5`, which completes the E5 projects while E5 is still draft. With real E5 numbers (the playtest pack flips the 50 E5 rows to numeric, E5 itself takes about 20.5 minutes) the stock AI reaches KEY-6 with much more Insight a year and went from its own KEY-6 to its own KEY-7 in **11.2 minutes** (12 civilizations, three worlds, range 7.0 to 15.5), less than half the 25-minute target (D8).

## The sweep (real E5, seeds 12345 to 12347, four civilizations each, all 12 reached KEY-7 every time)

| Base cost | Eight D9 overrides | Mean KEY-6 to KEY-7 | Range |
|---|---|---|---|
| 12,000 | in step | 11.2 | 7.0 to 15.5 |
| 32,000 | in step | 29.2 | 19.8 to 33.8 |
| 40,000 | in step | 36.3 | 24.2 to 41.8 |
| 27,000 | in step | 24.4 | 15.8 to 28.2 |
| **28,000** | in step | about 25 (interpolated between 27,000 and 28,500) | |
| 28,500 | in step | 25.6 | 15.8 to 32.0 |

A first sweep left the eight D9 overrides at their old values and so looked far more sensitive (12,000: 11.2, 27,000: 15.2, 48,000: 25.1, 60,000: 30.9, 100,000: 52.0); it was wrong for the shipped content, because those eight projects are on the path to KEY-7 and the content test (`ContentGoldenTests`) requires them to equal twice the era base. The consistent response is roughly 0.9 minutes per 1,000 of base cost.

## Caveats

1. **The stock AI is the yardstick, as for every earlier era.** A human may be slower or faster; the playtest should say. The cost is one number to change.
2. **The old grant-E5 numbers are obsolete.** `--grant-e5` runs now take longer than 25 minutes (the proxy skips E5's Insight gains); use the playtest pack (`playtest/make-discovery-pack.sh`) for Discovery pacing from here. The script's `E6_BASE_COST` option rebuilds the scratch packs with the overrides in step.
3. **E5 is still draft in the repository.** The Discovery pacing holds only once E5 is promoted as it was flipped for the playtest pack.

## Held-out confirmation (base 28,000, content as shipped)

`discovery-match --pack playtest/discovery-numeric-e5.pack --seed 20261101 --seeds 6 --minutes 260`: six new worlds (seeds 20261101 to 20261106), four civilizations each, the stock AI, real E5 numbers, no grants. **23 civilizations reached KEY-7: mean 24.4 minutes, range 16.0 to 35.5** (per world: 23.7, 20.6, 26.5, 28.1, 25.4, 21.5), against the 25-minute target and the 25.0 fitted on the other seeds. The 24th, a Sage, never left era 2 with zero Insight a year: the same early-age collapse the earlier held-out runs showed, nothing to do with Discovery.

The full suite (5 known failures only), the three goldens (toy, research `cb6fd7b2b6ebfd06`, politics `5d3ebe260a0f55b5`, all unchanged) and `validate --strict` pass on the shipped content.
