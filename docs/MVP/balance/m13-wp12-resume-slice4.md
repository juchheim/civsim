# M13 WP-E6-12 slice 4: snapshot and resume acceptance for Discovery (2026-11-21)

**Status:** harness tooling added on `codex/e6-discovery`, uncommitted; **one pre-existing defect found, not fixed**. No simulation code changed in this slice.

## The check

`discovery-match --pack civsim.pack --seed S --minutes M --resume-at R [--mode fief] [--grant-e5] [--grant-e6]` plays the stock AI to minute R, writes a whole-match snapshot (`MatchModel.WriteSnapshot`, about 5.9 MB at minute 105), restores it into a freshly built match, and runs both on with the same commands to minute M, comparing checksums every tick after the restore. `--mode fief` runs the same check under Fief rules. On a divergence it prints the first tick, the economy, research, politics and AI hash of each side, and the byte ranges where the two economy snapshots differ.

## Results (seed 20261019)

| Mode | Resume at | Result |
|---|---|---|
| Discovery | 5, 30, 60 min | identical |
| Fief | 70 min | identical |
| Discovery, no grants | 90, 105 min | **diverges at the first season end after the restore** (tick 54150 and 63150) |
| Discovery, E5 and E6 granted | 105 min | **diverges at the same point** (tick 63150) |
| Fief | 90 min | **diverges at tick 54150, the same tick as Discovery** |

**The restore itself is exact** (the checksum right after reading equals the live one in every case). **The divergence is not caused by Discovery**: Fief diverges at the same tick without any E6 code in play, and Discovery matches with no E6 projects diverge too. It first shows at a season-end step (offset 149 of the season, economy only; politics matches, research and AI follow from the economy).

## What the divergence looks like

The two economy snapshots are the same size and differ in about 21,000 bytes out of 4.3 MB: sparse, mostly low-order (fractional) bytes of per-tile stock arrays, starting in the forage stock (first array) and continuing through the herd-food, wood and fish arrays. So it is a **small difference in the seasonal regrowth of some tiles**, not a lost field.

Ruled out by clearing the live match's lazy caches at the restore tick (no change in the divergence): the forage settlement-owner cache, the reachable-forage-share cache, the camp-by-parent cache and the per-civilization modifier and cause-modifier caches. Not yet checked: any other lazily built per-tile or per-settlement state (walk costs, range tiles, tile ownership fed by politics), and state that the original builds during play but a restored match rebuilds with different inputs.

## What it means

1. **For E6:** nothing in E6 is implicated, and every E6 slice hashes and snapshots through the same code (the restored Discovery state is equal at the restore tick). Natural play, replays from the command log and every golden are unaffected; the golden hashes come from uninterrupted runs.
2. **For saves:** a game loaded from a snapshot late in a match would continue slightly differently from the run that wrote it. It stays deterministic from the load onward, but it is not the same trajectory, so a save-and-resume replay check cannot be used as an acceptance test until this is found.
3. **It predates E6** and lives in Iron and Fief economy code shared by every mode, so it belongs with the E5 owners (the number-balancing of E5 is in progress there) and with a separate investigation, not in this branch.

## Reproduce

```bash
dotnet run --project src/CivSim.Harness -c Release -- discovery-match --pack civsim.pack --seed 20261019 --mode fief --minutes 92 --resume-at 90
```

(about 90 seconds; `--resume-at 70` is the identical control).

## Next steps for whoever takes it

Bisect by tick inside the first diverging season (the harness already steps tick by tick after the restore), diff the two economies tile by tile at the first differing tile (index about 9,150 of 16,000 in the forage array) and ask what that tile's regrowth reads that a restored match would hold differently.
