# F-T6 / F-T7 natural-cohort preflight — 2026-11-14

## Frozen worlds

The shared 20-world cohort is [m12-e5-9-f-t6-t7-natural-seeds-2026-11-14.tsv](m12-e5-9-f-t6-t7-natural-seeds-2026-11-14.tsv), SHA-256 `a92dd049c1c56836dd2bffad3106985694be2eb755f15b4665c0937ae318f038`. The list was selected without replacement from 20,700,000–20,799,999 using Python `random.Random(20261115)`, then sorted for execution. No geography, eligibility, progress, or outcome screening was performed.

The block is disjoint by range from the recorded F-T1 probes (`202610xx`), F-T4 blocks (`20270403–20279994`, `20291001–20296000`, and `20300000–20399999`), F-T5 worlds (`20500000–20599999`), and A6 screen/finalist seeds (`20266019`, `20270019–20270021`, `21000001–21000003`, `21010001`).

## Measurement seam

The existing natural-match entry point is `civsim-harness fief-match --pack PACK --seed SEED --minutes 130`. `fief-match` selects Fief rules and the four standard Warlord archetypes; omitting `--grant-e5` keeps E5 research and the own-KEY-5 exposure natural. It accepts only a contiguous `--seed` plus `--seeds N` block, so this frozen list requires one invocation per seed (or a runner that reads this TSV and preserves one raw file per seed).

Current per-match output is not enough to classify either gate:

- F-T6's output has the per-civ `idleS` aggregate from `IronIdleInstitutions`. That helper currently checks academies, monasteries, hospitals, castles, galleys, and temples. It does not report each F-T6 E5 institution's staffed/working, queued, or dormant state and reason; it also omits several named F-T6 institutions from that counter.
- F-T7's public per-season death counts/events distinguish `Epidemic` from other causes, but do not record the outbreak class on each death. `OutbreakAt` exposes only a settlement's current class and season, not a season history. There is no per-match history pairing natural Plague/Pox exposure, imported-outbreak attempts/acceptances, quarantine state, and deaths by class from each seat's own KEY-5.

The narrow missing instrumentation is an opt-in per-match F-T6/F-T7 sidecar. For F-T6 it needs season, civilization, settlement, institution, status (staffed/working, queued, or dormant), and a typed blocking reason. For F-T7 it needs per-seat own-KEY-5 start, per-season outbreak class/exposure, MED-08/quarantine state and route-import attempt/outcome, plus deaths attributed to the contemporaneous Pox/Plague class. Keep raw exposures and censoring; do not add acceptance cutoffs in this preflight.

## Planned invocation and outputs

After that instrumentation is present in the integrated Release harness, run each manifest seed separately against the final frozen natural Fief pack with a 130-minute horizon and no E5 grant. The command shape will be:

```sh
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll fief-match \
  --pack /tmp/civsim-e5-final-natural.pack --seed "$seed" --minutes 130 \
  --fief-t6-t7-ledger
```

The `--fief-t6-t7-ledger` switch is a proposed opt-in name; no equivalent runtime flag exists today. Preserve one raw result per seed under `docs/MVP/balance/m12-e5-9-f-t6-t7-natural-output-2026-11-14/seed-<seed>.txt`, then write the combined exposure and censoring analysis to `m12-e5-9-f-t6-t7-natural-analysis-2026-11-14.md`. The pack path must be replaced with the exact integrated candidate pack and hash recorded before launch. This is only a preflight: no runtime changes, numeric thresholds, or matches were made.
