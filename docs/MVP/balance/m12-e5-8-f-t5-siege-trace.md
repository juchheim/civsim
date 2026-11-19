# F-T5 siege trace

The opt-in `fief-match --fief-siege-trace` switch writes diagnostic-only `fief-siege-trace` rows to standard output. It does not write simulation state, snapshots, or checksums. The narrow observation hook is null by default and runs only when requested.

Example:

```sh
dotnet run --project src/CivSim.Harness -- fief-match \
  --pack path/to/content.pack --seed 20261019 --minutes 110 \
  --fief-siege-trace
```

An `outcome=active` row is emitted for each active investment at each completed season boundary. `siegeTurn` is the zero-based season offset from the investment's began season. Rows with `exposed=false` are active but had no engine/cannon wall-fire update that season; zero shots and powder are reported for these rows. A siege still active at the run horizon receives a final `outcome=right_censored` row. Existing investment, assault, and capture events are also emitted as `outcome=<event>` rows, with their event `A`/`B` payloads preserved as `outcomeA`/`outcomeB`.

Fields include attacker/defender civilization, home and target settlement IDs, siege turn, carried engine/cannon counts, live member count, engine/cannon crews allocated by the existing firing rules when a fire update occurred, exact cannon shots and powder paid, supply status, wall integrity, powder-breach state, and outcome. Each shot consumes 0.25 L powder; wall damage from cannon fire requires a supplied update, an available cannon crew, a cannon, and paid powder. A powder breach is recorded only when a paid cannon shot fires; ordinary engine damage remains separately observable. `assignedCrews` is the sum of engine and cannon crews allocated for that wall-fire update, with four adults per crew. The simulator diagnostic observation also captures `attritionAssignedCrews`, `attritionDeaths`, and `def09Active` from the existing siege attrition calculation; focused fixtures check four assigned adults per engine/cannon, DEF-09 activation only for a garrisoned concentric castle, and observed deaths against `CombatCasualtyObserverForDiagnostics`. The confirmed draft rates are 1% per assigned adult per siege turn, raised to 1.25% under garrisoned DEF-09. The analyzer accepts either `attritionDeaths` or the legacy `deadCrews` column. An observer may receive the same per-season observation once after attrition and again after wall fire; both value copies share the diagnostic payload, so the later callback includes the firing fields. Natural sidecar rows serialize `attritionAssignedCrews`, `attritionDeaths`/`deadCrews`, `def09Active`, and `fireAttempted`; an attrition-only row therefore remains distinct from a wall-fire update.

At the end of every traced match, `fief-siege-terminal` reports either `exposure=none reason=no_siege_investment_observed` or an exposure summary. Exposed matches include aggregate final cannon and powder stocks for each civilization and one final attacked-settlement row with stone walls, gatehouse, castle/garrison, concentric castle, last observed siege integrity and powder-breach state, ending outcome, and censor reason. `outcome=right_censored censorReason=match_horizon_active_investment` means the investment remained active at the run horizon; its eventual result is unknown. Ended spells carry the observed ending event and `censorReason=none`. Terminal stocks come from end-of-match economy state, while the settlement row uses the last live siege observation for integrity/breach and end-of-match economy for defenses. The trace records seasonal snapshots and live events, not a durable siege identifier across separate matches beyond seed plus settlement/season.

## Frozen F-T5 cohort preflight

The cohort manifest is [m12-e5-8-f-t5-natural-seeds-2026-11-15.txt](m12-e5-8-f-t5-natural-seeds-2026-11-15.txt), reusing the 20 fresh unscreened seeds frozen for B5-11. SHA-256: `f787766662b46319f7f0807eacd27592aa8c2af4163f366d52ddec07a3e3a786`. The runner refuses a changed manifest, a pack whose hash differs from the hash supplied at preflight, a Release harness without the trace switch, a non-130-minute horizon, or an existing output directory. Each raw seed output and its hash are recorded in `run_metadata.json`; raw files are opened exclusively and never overwritten.

After the active D20 run releases the shared Release output, the required build is:

```sh
dotnet build src/CivSim.Harness/CivSim.Harness.csproj -c Release
```

Record the exact natural Fief pack SHA-256 before running. Then execute the frozen 20-seed cohort (replace both placeholders with the reviewed absolute pack path and its 64-digit hash):

```sh
python3 docs/MVP/balance/tools/run_fief_ft5_cohort.py \
  --pack /absolute/path/to/natural-fief.pack \
  --expected-pack-sha256 <recorded-pack-sha256>
```

Analyze the preserved raw sidecars without changing them:

```sh
python3 docs/MVP/balance/tools/analyze_fief_ft5_trace.py \
  --raw-dir docs/MVP/balance/m12-e5-8-f-t5-natural-output-2026-11-15
```

The analyzer accepts the current space-delimited `fief-siege-trace` and `fief-siege-terminal*` records, JSONL objects, or CSV rows. It validates the frozen seed list and raw filename/row seed agreement, then summarizes exposed and unexposed investment-seasons, shot/powder conservation at 0.25 L per shot, breach-without-paid-shot anomalies, base versus DEF-09 attrition exposures, event rows, and right-censored spells. Attrition rates are deaths per assigned adult-season (`attritionAssignedCrews` counts adults); a stratum with fewer than 20 assigned adult-seasons is labeled inconclusive for low/no exposure. Rates remain descriptive, with missing death counts explicit. Terminal summaries include no-exposure versus observed matches, final cannon/powder stock rows, attacked-settlement censor/breach outcomes, and counts of stone walls, gatehouses, castles, garrisoned castles, and concentric castles. A right-censored investment is still active at the run horizon; its eventual result is unknown. No match was run for this preflight, and the final pack hash must be recorded before any cohort launch.
