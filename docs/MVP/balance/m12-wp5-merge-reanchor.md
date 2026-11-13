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

The plan's decisions (D8: match Iron, D9: doubling) leave one question open: match which number, 25 (the target) or 20 (what Iron measures today)?

- **Cost:** the whole E5 age (all 49 projects plus KEY-6) costs about **402,600** Insight; the 16-project rush costs about 150,200. The doubling touches only **7 of 49** E5 projects (BLD-12, GOV-11, LRN-08, SEA-11, TRD-09, WAR-12, WAY-09), so it is a minor lever.
- **Calibration:** the Commons-and-roles formula explains only 45% of what Iron civilizations really spend, so the forward estimate is scaled by 2.22 (a one-anchor calibration, not a fit).
- **Result:** if E5 civilizations research the whole age as Iron ones do, KEY-6 takes about **19 minutes at Iron-like growth (6% a minute)**, 17 to 25 across 8% to 3%. A civilization that rushes KEY-6 finishes in about 10. So E5 as typed already lands **close to Iron's measured 20 minutes**.
- **To reach the 25-minute target instead,** the era base must rise by about 58% at 6% growth (base about 9,500), or by 17% to 117% across 4% to 8% growth. To hold Iron's measured 20, keep the base near 6,000 to 6,300.

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
