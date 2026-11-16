# M12 WP-E5-1c: merging main and re-anchoring the E5 oracles (2026-11-13)

**Status:** diagnostic. Nothing in simulation behavior or Iron content changed. The branch now carries the Iron closeout, the settlement-core work and the Thriving Legacy; this re-reads the Iron state the E5 oracles lean on. It supersedes the pacing section of [m12-wp4-reanchor](m12-wp4-reanchor.md). Raw run output: [m12-wp5-merge-reanchor-output.txt](m12-wp5-merge-reanchor-output.txt).

## The merge

- `main` (`daf2ed5`) merged into `codex/e5-fief` as `ad65b4a`. One conflict, `tests/golden/content-pack.txt`, resolved by regenerating it (projects 519, 394,437 bytes, hash `20599d6e…`).
- `validate --strict` clean. Toy, research and politics determinism checks pass on the merged pack, so my fingerprint fix for E5 draft rows still holds with the new settlement-core code.
- Full suite: 1,184 of 1,185 on the first run. The one failure was new: main's settlement-core test requires every registry structure to have a size class. The 11 E5 structures are now classed as doc 31a already planned (university, concentric castle and Gothic Cathedral whole; hospital half; clock tower and print shop small; monastery, windmill, sawmill, blast furnace and powder mill outer). That test group, the determinism-rule test and the content golden tests pass (72 of 72).

## Drift check

`e5_anchor_check.py` now checks 24 constants and **all match**, with two INFO lines. The Iron closeout changed the T11 kit recipe (wrought iron 0.15 to 0.08) and the T11/T12 service lives (24/48 to 30/60 tool-work seasons). `e4_metal_oracle.py` belongs to the Iron work and still has the old values, so the E5 power oracle now reads the registries directly (`e5_anchors.apply_iron_registry`). Effect on the blast-furnace screen: Iron kit replacement now needs about 9.1 coppiced forest tiles per 100 adults (T12) instead of 11.3, so the cast-iron demand I assumed (a hypothesis) is now the larger fuel draw; the conclusion that the furnace only helps at about half the bloomery's charcoal per litre stands.

## Fresh Iron anchor (8 matches, 32 seats, natural play)

| Quantity | Now | Was (2026-11-05) |
|---|---|---|
| KEY-4 median | minute **71.8** (p25–p75 69.1–74.0) | 59.8 |
| KEY-5 median | minute **92.2** (86.9–97.0) | 78.5 |
| Own KEY-4 → KEY-5 | median **20.0**, mean 20.3, range 16.3–25.2; **31 of 32 finished** | 19.6, only 16 of 32 finished |
| Chapter ends | minutes 99.0–104.5 (48-season countdown) | 78–83 |
| Iron projects done at KEY-5 | **all 56** | 37.5 |
| At the end: people, adults, settlements | 1,925; 1,049; 26 | 1,376; 617; 29 |
| Soldiers; scholars; missionaries (medians) | 122 (12% of adults); 4; 7 | 51 (9.6%); 1; 3 |
| What still never happens | no Wonder finished, no engine siege, no investment, blockade, tribute or league | same |

Two things stand out. First, the interval is almost uncensored now, because the longer countdown lets slow seats finish, so the measured Iron length is **20 minutes, not the 25 minutes** that [28 §7a](../28-production-gap.md) records as the target; the build today sits about 20% under it. Second, Iron civilizations now research **every** Iron project before closing the age, so what they spend is the whole age (127,320 Insight with the late-project doubling), not the 21-project candidate the earlier oracle used.

## What this does to E5 pacing

The plan's decisions are D8 (E5 aims for 25 minutes, decided by the user on 2026-11-13 after seeing this report) and D9 (doubling). The numbers below are what each choice implies.

- **Cost:** the whole E5 age (all 49 projects plus KEY-6) costs about **402,600** Insight; the 16-project rush costs about 150,200. The doubling touches only **7 of 49** E5 projects (BLD-12, GOV-11, LRN-08, SEA-11, TRD-09, WAR-12, WAY-09), so it is a minor lever.
- **Calibration:** the Commons-and-roles formula explains only 45% of what Iron civilizations really spend, so the forward estimate is scaled by 2.22 (a one-anchor calibration, not a fit).
- **Result:** if E5 civilizations research the whole age as Iron ones do, KEY-6 takes about **19 minutes at Iron-like growth (6% a minute)**, 17 to 25 across 8% to 3%. A civilization that rushes KEY-6 finishes in about 10. So E5 as typed already lands **close to Iron's measured 20 minutes**.
- **Oracle sensitivity for the 25-minute target (chosen):** the initial estimate called for a base near 9,500 at 6% growth, with a broad 7,000–13,000 range at 4–8%. Live natural runs now show a four-seed 9,500 candidate at a 30.1-minute pooled median and a four-seed 7,900 candidate at 25.2 minutes; carry 7,900 as the current fit hypothesis, not an accepted or promoted value. To hold Iron's measured 20, the earlier oracle estimated 6,000–6,300.

### Natural Fief pacing cross-check (2026-11-14)

The exploratory eight-seed Fief cohort in [m12-e5-9-fief-key6-routing](m12-e5-9-fief-key6-routing.md) used the draft E5 era base of 6,000 and completed KEY-6 for 29 of 32 seats by the 130-minute horizon. The completed-seat median KEY-5→KEY-6 intervals were Builder 17.55 minutes, Grower 20.1, Warlord 17.9 (5/8; the other three were censored or had not reached KEY-5), and Sage 19.7. These are live natural-play observations, but the cohort is small and the Warlord completion rate is incomplete.

As a pre-run sensitivity calculation, applying the oracle's calibrated 6% per-minute exponential-growth assumption to each archetype median, then scaling total E5 cost by 9,500/6,000, maps those medians to approximately 22.9, 25.8, 23.3, and 25.4 minutes respectively. Inverting the same formula gives archetype-specific base estimates of roughly 11,200, 8,900, 10,800, and 9,200 for a 25-minute median. The live 9,500 and 7,900 runs supersede this rough estimate.

The sensitivity estimate above is superseded by the live candidate result below. The registry remains at 6,000 and all E5 rows remain draft; neither live cohort is an acceptance fit.

### Live 9,500-base candidate check (2026-11-14)

A four-seed natural Fief run used a temporary pack with all 50 E5 rows numeric and the E5 era base set to 9,500; the repository content was unchanged. By the 130-minute horizon, **15/16 seats reached KEY-6**, with one Grower right-censored after 29.0 minutes beyond its own KEY-5. Completed-seat KEY-5→KEY-6 medians were Builder **31.55**, Grower **31.40**, Warlord **29.35**, and Sage **30.05** minutes; pooled median **30.10** (range 25.5–35.0). This candidate is materially slower than the confirmed 25-minute aim, so 9,500 is too high. The raw output is [m12-wp5-fief-9500-live-output-2026-11-14.txt](m12-wp5-fief-9500-live-output-2026-11-14.txt).

### Live 7,900-base candidate check (2026-11-14)

A second four-seed natural Fief cohort used a temporary pack with all 50 E5 rows numeric and the E5 era base set to 7,900. The repository content was unchanged. All **16/16 seats reached KEY-6** by the 130-minute horizon, with no censoring. The pooled KEY-5→KEY-6 median was **25.2 minutes** (range 21.1–27.0); archetype medians were Builder **24.7**, Grower **23.5**, Warlord **25.4**, and Sage **25.25** minutes. This is close to the confirmed 25-minute aim and is the current fitting hypothesis. It is still a four-seed, 16-seat exploratory sample, not a held-out acceptance fit or B5 gate. Raw output: [m12-wp5-fief-7900-live-output-2026-11-14.txt](m12-wp5-fief-7900-live-output-2026-11-14.txt).

### Fresh natural 7,900-base fit cohort (2026-11-14)

A fresh seed list was frozen before any cohort run: 20 distinct seeds sampled without replacement from 20,300,000–20,399,999 using Python `Random(20261114)`, with no world or pacing screening. The seeds are disjoint by range from the prior 202610xx F-T1 probes and the 20270403–20279994 F-T4 holdout. The 80 matches (four seats per seed) used the unchanged scratch pack `/tmp/civsim-e5-fit-7900.pack` (SHA-256 `687a89cd1bc7bc1b8930ec77eafb153e678e663f71aaea0c3e1585b20dd6e4ee`), all 50 E5 rows numeric and era base 7,900. Runs used the Release harness, Fief mode, a 130-minute horizon, and **no `--grant-e5`**, so E5 research and each civilization's KEY-5→KEY-6 interval were natural.

By the full horizon, **65/80 seats reached KEY-6**, **8** had reached KEY-5 but were right-censored before KEY-6, and **7** had not reached KEY-5. All 20 matches ran to 130.0 minutes with chapter end `None` and `ended=False`; no match ended early. Among the 65 completed seats the pooled median interval was **22.90 minutes**, with interpolated p10/p25/p75/p90 **20.14/21.00/24.60/25.34** and range **16.8–28.0**. Completed-seat archetype summaries (n; median; p10/p25/p75/p90; range, minutes) were Builder (**17; 21.50; 20.14/20.70/22.80/23.48; 19.3–25.5**), Grower (**16; 24.75; 20.00/21.15/25.97/26.60; 16.8–28.0**), Warlord (**15; 23.10; 21.16/22.30/24.30/24.94; 19.6–25.9**), and Sage (**17; 23.50; 20.26/20.80/23.80/24.74; 18.5–25.0**). Quantiles use linear interpolation over completed-seat intervals only; censoring remains visible and is not imputed.

This fresh cohort is treated as **fit data**, not untouched holdout evidence: its completed-seat median is faster than the D8 aim. A proportional estimate gives `7,900 × 25 / 22.90 ≈ 8,624`, rounded to an 8,600 candidate. The 15 censored seats remain visible and must not be imputed. The D8 target says “about 25 minutes” without a numerical tolerance, so this fit does not define an acceptance band. Keep the repository E5 base at 6,000, all catalog rows draft, and WP-E5-11 acceptance open. The frozen seed list is [m12-wp5-fief-7900-fit-seeds-2026-11-14.txt](m12-wp5-fief-7900-fit-seeds-2026-11-14.txt); seat-level outcomes, key times and censor reasons are in [m12-wp5-fief-7900-fit-seats-2026-11-14.tsv](m12-wp5-fief-7900-fit-seats-2026-11-14.tsv); exact harness output is [m12-wp5-fief-7900-fit-natural-output-2026-11-14.txt](m12-wp5-fief-7900-fit-natural-output-2026-11-14.txt).

### Fresh natural 8,600-base candidate check (2026-11-14)

After freezing a new independent seed list, 20 distinct seeds were sampled without replacement from 20,400,000–20,499,999 using Python `Random(20261115)`, with no world or pacing screening. The range is disjoint from the 7,900 fit cohort and earlier F-T1/F-T4 ranges. The 80 natural Fief matches used a temporary pack with all 50 E5 rows numeric and only the E5 era base changed to 8,600; the repository remained unchanged. Runs used the Release harness at a 130-minute horizon, with no `--grant-e5`. The temporary pack SHA-256 was `ebec57d21969d67e24c8bea2cd85692fb32f08efbb40b78ed19e8aaaf8e03c32`.

At the horizon, **72/80 seats reached KEY-6**, **7** were right-censored after KEY-5, and **1** had not reached KEY-5. Every match ran to 130.0 minutes with chapter end `None` and `ended=False`. Among completed seats, the pooled KEY-5→KEY-6 median was **22.25 minutes**, with interpolated p10/p25/p75/p90 **19.40/20.68/24.42/25.69** and range **17.5–28.7**. Archetype summaries (n; median; p10/p25/p75/p90; range, minutes) were Builder (**20; 21.25; 18.99/19.48/23.32/24.67; 18.3–26.8**), Grower (**16; 24.85; 19.45/21.18/26.90/27.70; 17.5–28.7**), Warlord (**17; 23.00; 20.02/21.40/23.90/24.56; 17.9–25.1**), and Sage (**19; 21.90; 20.28/21.00/23.25/24.96; 18.1–25.7**). Quantiles use completed intervals only; no censored interval is imputed.

This independent candidate cohort does **not validate a 25-minute target**: its pooled completed-seat median is 22.25 minutes, faster than the prior 7,900 fit cohort's 22.90. The archetype results vary, and 8/80 seats remain incomplete at the horizon. Since “about 25 minutes” has no stated numerical tolerance, this is a fit diagnostic rather than pass/fail evidence. Preserve the repository base at 6,000 and keep all E5 values draft; the numeric acceptance and WP-E5-11 gates remain open. The frozen seeds are [m12-wp5-fief-8600-validation-seeds-2026-11-14.txt](m12-wp5-fief-8600-validation-seeds-2026-11-14.txt), seat-level outcomes and censor reasons are [m12-wp5-fief-8600-validation-seats-2026-11-14.tsv](m12-wp5-fief-8600-validation-seats-2026-11-14.tsv), and verbatim harness output is [m12-wp5-fief-8600-validation-natural-output-2026-11-14.txt](m12-wp5-fief-8600-validation-natural-output-2026-11-14.txt).

### Pre-COM natural 7,900-base diagnostic (2026-11-14)

A fresh, non-overlapping eight-seed cohort (`20261023–20261030`) ran with the same temporary 7,900-base pack and all 50 E5 rows numeric, **without** `--grant-e5`, at a 130-minute horizon. This ran on the pre-COM-09/10 Debug harness snapshot, before the draft COM-10 diffusion discount was wired into Fief research; it is therefore a diagnostic of that code snapshot. KEY-6 completed for **27/32 seats**; four seats were right-censored after KEY-5 and one Sage did not reach KEY-5. Completed-seat KEY-5→KEY-6 medians were Builder **23.95** (8/8; range 20.0–26.6), Grower **24.25** (6/8; range 22.8–29.4), Warlord **23.8** (6/8; range 21.4–24.6), and Sage **22.1** (7/8; range 20.6–26.5) minutes. The pooled completed-seat median was **23.8 minutes** (27 seats; range 20.0–29.4). The censored KEY-5 intervals were Grower 25.7 and 25.6 minutes, and Warlord 26.4 and 22.6 minutes; the remaining Sage seat had not reached KEY-5 by the horizon. The four bounded run batches took a combined **65 minutes 15 seconds** of reported process wall time. Because this 7,900 value was informed by the earlier fit and this snapshot predates COM-10 integration, this is neither held-out evidence nor acceptance. Re-run against the finalized COM code and candidate pack before treating the fit as current. Full raw output: [m12-wp5-fief-7900-pre-com-natural-output-2026-11-14.txt](m12-wp5-fief-7900-pre-com-natural-output-2026-11-14.txt).

The 6,000-base eight-seed baseline and the 9,500/7,900 candidate cohorts are exploratory fits with different cohort sizes. Keep the repository base at 6,000 until the larger held-out fit and the WP-E5-11 promotion. Reserve 80+ whole-match and playtest gates for WP-E5-11/12.

## Other findings for E5

- **Institutions:** the staffing demand the economy oracle assumes beyond soldiers (7.5 per 100 adults) is about **7 times** what live Iron staffs (scholars 0.38 and missionaries 0.67 per 100 adults), with 11 idle scholar seats per civilization. Soldiers are now 12% of adults, above the 10% slot.
- **Wood:** unchanged conclusion; construction is still wood-bound, and no Wonder finishes in any of the eight matches.
- **War:** still no wall siege to learn from, so the cannon and concentric-castle numbers rest on the fixture oracle alone.
- **Legacy:** E5 will use main's constants unchanged (Firsts pay nothing, Mastery 2 × (era + 1), Wonder built 25 × (era + 1), Thriving √people ÷ 5 per season).

## Limits

One 8-match block of four Warlord seats; end-of-match population is read as end-of-age; the growth rate per minute is an assumption; the k = 2.22 calibration rests on that same block. The research oracle's late-project definition reproduces main's own E4 overrides exactly (28 of 28) and E2's 26, which is what justifies applying it to E5.

## Reproduce

```bash
export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec
dotnet run --project src/CivSim.Cli -c Release -- build-pack --out /tmp/civsim-e5m.pack
dotnet run --project src/CivSim.Harness -c Release -- iron-match --pack /tmp/civsim-e5m.pack --seed 20261019 --seeds 8
python3 docs/MVP/balance/tools/e5_anchor_check.py
python3 docs/MVP/balance/tools/e5_research_oracle.py
```
