# M11 WP2 — fresh E3 anchor

**Purpose.** Record the fresh E3 evidence used as the starting anchor for M11 WP2. These are observations of the current E3 build; they are not an E4 fit, forecast, or acceptance sample. Existing E3 content and golden files were not changed for this report.

## Run identity and cohorts

- Source checkout: commit `6dcdd6bb187a8de569e96bc222ba99b84afd9666` (`codex/e4-iron`). Build-pack SHA-256: `8132d2e20376fe2c8aad046e0bb5070437f346dfca3f69fc9dda8fb4d1751b7c`.
- Pack build/check: `dotnet run --project src/CivSim.Cli -c Release -- build-pack --out /tmp/civsim-e4-wp2.pack --check-golden tests/golden/content-pack.txt` (passed).
- Report commands (each used `dotnet run --project src/CivSim.Harness -c Release --` and `--pack /tmp/civsim-e4-wp2.pack`): `research --report-e3 --seeds 20261019,...,20261042 --minutes 80`; `politics --check-m10 --seed 20261019 --seeds 24 --minutes 80`; `politics --check-m10 --seed 20261100 --seeds 24 --minutes 80`; `research --report-metal --seeds 20261019,777 --minutes 70`; and a separate held-out metal report `research --report-metal --seeds 20261020,778 --minutes 70`. C1 fixtures were run separately with `research --c1-e3 --seed 20261019` and `research --c1-e3 --seed 777`.
- E3 report cohort: 24 seeds, four Balanced civilizations on Council, 80-minute ceiling. The politics blocks are two separate 24-match diagnostic blocks. They are recorded separately and **neither is designated as an acceptance sample**. Metal runs are two-seed screens, with planner-only camps on own land; they are diagnostics, not acceptance evidence.

## E3 anchor observations

The E3 report cohort measured median KEY-3 at 42.5 min, KEY-4 at 62.6 min, and own KEY-3→KEY-4 duration at 20.4 min. At KEY-4, median population was 362.5 people/civ (range 177–479), settlements 9.0/civ (2–14), and claimed land 31%; paired-neighbor border contact was 11/48. Mean E3 Cohesion was 66.9 (p10 57, p50 68, p90 75); 2 secessions occurred in 96 civs and captures were zero. The sampled cohort had two incomplete KEY-4s; these are descriptive results, not a replacement for M10 acceptance.

The C1-E3 fixture comparison (seeds 20261019 and 777) reported mean E2 FoodCap of 128.6 and 104.6, and mean E3 FoodCap of 193.3 and 173.5, respectively. The paired E3/E2 ratios were 1.50 and 1.66. These fixtures do not establish live-match FoodCap behavior.

The 24-seed E3 mortality ledger reported expected E3/E0 child and adult hazard ratios of 0.786 and 0.794; expected E3 e45 was 16.8. Expected epidemic share was 8.2% with conditional winter-shortfall surcharge excluded. This is the expected-hazard ledger, distinct from realized death shares.

## M10 politics gates: two separate blocks

The applicable gates are B17 Territory, B18 Legacy split, B19 Cohesion/secession, and B20 Conflict. The following reproduces each block's reported result without pooling them:

| Block | B17 | B18 | B19 | B20 |
|---|---|---|---|---|
| Seeds 20261019–20261042 | **Fail** — 94/96 KEY-4 completions; 10.7 settlements/civ, 40.7% land claimed, 86/94 border contacts (91.5%); Town 94.8%, City 31.2% | **Fail** — Dominion/Mastery/Firsts/Wonders 55.3/30.8/14.0/0.0% | **Fail** — cohesion 62.3, 0.06 secessions/civ/E3, 0/24 cascades; 94/96 exposures complete | **Pass** — 9.7 raids/match, war in 79.2%, capture in 29.2% |
| Seeds 20261100–20261123 | **Fail** — 96/96 KEY-4 completions; 10.7 settlements/civ, 42.2% land claimed, 85/96 border contacts (88.5%); Town 95.8%, City 30.2% | **Fail** — Dominion/Mastery/Firsts/Wonders 55.2/30.9/14.0/0.0% | **Fail** — cohesion 62.1, 0.02 secessions/civ/E3, 0/24 cascades; 96/96 exposures complete | **Fail** — 10.7 raids/match, war in 87.5%, capture in 12.5% |

## Two-seed metal screens

Both screens hit B11's method-rung ratios: chopping ×1.50 and shaft mining ×1.95. For T18, the combined live-input-access cohort was 83% adult-weighted (344/416), median settlement coverage 97% across 33 eligible rows in the first screen; and 78% (625/800), median 89% across 68 rows in the held-out screen. The source-backed subcohort was 80% then 64%; the research-timed diagnostic median was 0% in both. These denominators include only qualifying input-access windows through +12; they do not mean all settlements maintained access.

T17 median demand per 100 people/year was bronze 0.66 L, tin 0.000 L, charcoal 2.62 L, wood 16.4 L and labor 4.3% in the first screen; held-out medians were 1.10 L, 0.000 L, 3.00 L, 18.7 L and 5.7%. T19's 80–100-person group had three rows: median forest-wood drawdown was 100% in the first screen and 16% in the held-out screen. Two seeds per screen and the selected cohorts make these exploratory measurements only.

## Limits and provenance

The fresh E3 numbers anchor comparison; they do not justify changing E3 balance, and no E3 content or golden was edited. The two politics blocks show between-block variation and are not pooled into an acceptance claim. Metal outputs are small diagnostic screens; T18 access-conditioned coverage and T17 demand do not establish population-wide supply or E4 viability. Detailed per-seed/per-settlement outputs are the run records `/tmp/civsim-e4-wp2-report-e3-24.txt`, `/tmp/civsim-e4-wp2-politics-train.txt`, `/tmp/civsim-e4-wp2-politics-heldout.txt`, `/tmp/civsim-e4-wp2-metal-train.txt`, and `/tmp/civsim-e4-wp2-metal-heldout.txt`.
