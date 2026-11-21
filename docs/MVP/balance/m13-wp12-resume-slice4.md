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

## Narrowed further (same day)

At the first diverging tick **exactly one tile's forage stock differs** (tile 8675 of 16,000 in the Fief repro; 1.886 against 1.945, about 3%). Its field owner, wildfire, forest state and low-season counters, its forage-owning settlement (32, civilization 1) and that civilization's `winter_forage_stock` (x2), `autumn_forage` (x1.4) and the winter multiplier (1.08) are **identical** on both sides, as is the season count. So the difference is not an input that a restored match holds differently at the end of the step; it arises inside the season-end regrowth or harvest of that tile (the stock is `min(stock + regrowth, capacity)`), or from a state the hash does not cover. The harness prints this (`TileDiff`) on a divergence.

## Narrowed again: the live match holds the difference, in a peer system

1. **Two restores agree.** Restoring the same snapshot twice gives two matches that stay identical to each other and both differ from the live match (the harness runs a second twin). So the restore is deterministic and it is the **live** run that carries state the snapshot does not hold.
2. **The economy's own fields are not it.** A reflective deep comparison of the live and restored economy at the restore tick (`CIVSIM_DEEPDIFF=1`, which skips content tables, telemetry ledgers and one-sided lazy caches) leaves only: the per-town `ResidentVersion` cache counters, a per-town Fief-goods array that is empty in the live match and ten zeros in the restored one (every reader treats both alike), and the forage-owner cache count. `_coppiced` also differs but is re-derived at the start of every regrowth step.
3. **What does differ lives in the systems the economy reads.** The restored match's territory claim map is empty where the live one holds claims (for example claim 865 against 0 on a tile), its research practice accumulators and Know-how rates are zero where the live ones hold the season's totals, and its research event log is empty. The economy reads ownership and research state at the season-end step, so a one-tile difference in a regrowth term can come from a claim or accumulator that the restored match has not yet rebuilt.

**Likeliest cause:** derived peer state (territory claims, per-season research accumulators) that is not part of the snapshot and is recomputed only at a later update, so the first season-end step after a restore sees different inputs. **Not yet proven.** To prove it, make the restored match rebuild those derived maps at the restore (or snapshot them) and rerun the repro: if the divergence vanishes, that is the cause and the fix is a rebuild call in `ReadSnapshot`.

## Next steps for whoever takes it

Bisect by tick inside the first diverging season (the harness already steps tick by tick after the restore), trace tile 8675 through the final tick of the season (harvest by foragers, then `RegrowResources`) on both sides: the stock before the step is equal in the hash, so the question is which term of `min(stock + regrowth, capacity)` or which harvest share differs.
