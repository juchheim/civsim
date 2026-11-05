# M12 WP-E5-1b: re-anchoring the E5 oracles (2026-11-05)

**Status:** diagnostic. Nothing in the simulation, content or goldens changed. This re-reads the Iron (E4) state the E5 oracles lean on and replaces one conclusion of [m12-wp1-oracle-fit](m12-wp1-oracle-fit.md). Raw run output: [m12-wp4-reanchor-output.txt](m12-wp4-reanchor-output.txt).

## What was checked

- **Did Iron change on `main`?** Since the E5 branch point (`43a1648`) `main` gained only renderer commits (up to `67c50ca`); no simulation, content or registry change. The Iron balancing session's live edits are **uncommitted** in another worktree (`iron-lowwood-availability`), so they cannot be anchored to yet.
- **Drift check (new):** `tools/e5_anchor_check.py` compares the 22 constants the oracles copy (siege engine, wall and castle constants, era bases and reach, Insight formula constants, the T11/T12 recipes, T12 service life) with the current source and content. **All 22 match.** Run it with `--check` after every Iron rebase; it exits non-zero on any drift.
- **Fresh live anchor:** `iron-match --seed 20261019 --seeds 8`, natural play (four Warlord seats, default Iron limits), on the current build. The live numbers now live in `tools/e5_anchors.py`, which the research and economy oracles import.

## The fresh Iron anchor (32 civilization seats, 8 matches)

| Quantity | Result |
|---|---|
| KEY-4 | median minute **59.8** (p25–p75 58.6–63.3; range 55.5–79.4); 31 of 32 seats reached it |
| KEY-4 → KEY-5 | 16 of 32 seats finished before the chapter ended (minutes 74.8–82.0, median **78.5**). Their own interval: median **19.6** (range 17.5–24.1). The other 15 had been researching a median **17.2** minutes after KEY-4 when it ended, so the interval is **right-censored**; the true median is probably a little above 20. |
| Size at KEY-5 (finishers) | median 1,376 people, 617 adults, 29 settlements (15–36), 37.5 Iron projects done |
| Staffing at KEY-5 | soldiers 9.6 per 100 adults; **1 scholar**, 3 missionaries and 2 idle scholar seats per civilization |
| Kits | about 0.79 T11 kits per adult; T12 kits rare |
| What never happened in 8 matches | no Wonder finished, **no engine siege**, no investment, blockade, tribute or league; every unfinished structure lacked **wood** (often block as well) |

## What this changes in the E5 plan

1. **E5 pacing is better than the first report said, and still long.** The earlier "E5 takes about 27 minutes if the rate grows 3×" compared E5 with the *average* Iron rate, which hides that Insight keeps growing through the era. A forward estimate that starts from the live end-of-Iron level (617 adults in 29 towns gives about 3,070 Insight/min by the formula) and keeps growing reproduces the Iron candidate's cost to within about 11%. On that calibrated version KEY-6 takes about **21 minutes at Iron-like growth (6% a minute), 25 at 4%, 28 at 3%**, against the 16.75–18.25 projection: roughly **15% to 40% long**, not 60%. Assuming the early Iron research modifiers are all active overshoots Iron by a third, so it is the less trusted column. Nothing is fitted; this is arithmetic on one anchor.
2. **Implication for the cap and the era base.** KEY-5 at about minute 78.5 plus 21 puts a typical KEY-6 near **minute 100**, with the slowest seats near 110. The candidate cap of Year 120 and a 12-season countdown still holds for finishers; the real lever remains the E5 era base (6,000, a catalog input) versus an E5 length target. Hold that decision until the Iron session settles its own era base.
3. **Institutions will not staff themselves.** Live Iron staffs the army and almost nothing else: scholars 0.16 and missionaries 0.5 per 100 adults. The staffing demand the economy oracle assumes for E5 beyond soldiers (7.5 per 100 adults) is about **12× what the live Iron economy staffs today**. This strengthens the earlier conclusion that staffing, not research, limits E5, and it makes monks, hospitals and universities a staffing-rule design problem before a balance problem.
4. **Wood, not research, stops construction.** Every stuck structure in the eight matches lacked wood. The E5 buildings (monastery 30 labor with 12 blocks and 6 wood, university, cathedral with 40 ashlar and 20 wood, blast furnace) will hit the same wall, which is the "Wonder and range fix" already listed as WP-E5-0 in [27 §9](../27-faith-and-fief-age.md). It should be solved once for both ages, with the Iron session.
5. **War will not have walls to break.** Iron produced eight wars and dozens of assaults and captures but no wall siege. E5's cannon, concentric castle and storm rules cannot be tuned from live Iron data; they rest on the fixture oracle until E4 produces walled targets.

## What is superseded in the earlier report

[m12-wp1-oracle-fit](m12-wp1-oracle-fit.md) said E5 needs about 4.7× the live Iron rate and takes about 27 minutes on population growth alone. The 4.7× figure is correct arithmetic but a misleading comparison (average against average of an exponentially growing quantity); use the forward estimate above. Its other findings (blast furnace fuel, cannon and castle readings, the three-field decomposition, the epidemic fixture) are unchanged and their constants still match the source.

## Reproduce

```bash
export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec
dotnet run --project src/CivSim.Cli -c Release -- build-pack --out /tmp/civsim-reanchor.pack
dotnet run --project src/CivSim.Harness -c Release -- iron-match --pack /tmp/civsim-reanchor.pack --seed 20261019 --seeds 8
python3 docs/MVP/balance/tools/e5_anchor_check.py
python3 docs/MVP/balance/tools/e5_research_oracle.py
```

## Limits

One 8-match natural-play block, four Warlord seats, right-censored at the chapter end; the pack in this worktree carries inert E5 draft rows (it cannot affect Iron play). The growth rate per minute is an assumption from the 09 model, not measured. No held-out cohort. The Iron session's uncommitted balance work is not reflected; re-run this after it lands.
