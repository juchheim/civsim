# WP-E5-9 current-source AI acceptance, A2–A8 and Legend duel — 2026-11-15

Scope: the remaining gates of [the rerun plan](../m12-e5-9-current-source-rerun-plan-2026-11-15.md), run after the A1 current-source run completed. A1 and its A7 companion were run by another session and are cited, not repeated. No AI source, numeric content or threshold was changed. All 50 Era-5 rows stay `draft` in the repository; the numeric-status pack used here is the scratch pack that run already used. Ordinary and release Fief selection stay blocked. Nothing is committed.

## Verdicts

| Gate | Criterion | Measured | Result |
|---|---|---|---|
| A1 (other session) | Warlord ≥80% vs Council, 40 seeds / 80 matches | 65/80 = 81.25% | **Pass** |
| A2 Grower | Warlord ≥70%, 80 matches | 62/80 = 77.5% | **Pass** |
| A2 Builder | ≥70% | 68/80 = 85.0% | **Pass** |
| A2 Wanderer | ≥70% | 59/80 = 73.75% | **Pass** |
| A3 survive | no settlement lost in ≥80% of matches | 80/80 matches with no Warlord settlement lost (100%) | **Pass** |
| A3 win | Warlord ≥70% vs Rusher | 58/80 = 72.5% | **Pass** |
| A4 | Chieftain 25–40% vs Warlord | 25/80 = 31.25% | **Pass** |
| A5 | no archetype >30% of FFA wins; 120 worlds × 5 rotations = 600 matches | Builder 144/480 = **30.00%** (max), Grower 26.67%, Warlord 24.38%, Sage 22.92%, Wanderer 21.04% | **Pass, zero margin** (one more Builder win fails) |
| A6 | ≥10,000 distinct policies screened; 16 finalists validated on fresh mirrored 64-minute pairs; none >55% | 10,009 screened; 16/16 validated on 3 pairs each; max 50.00% | **Pass** for the searched candidate set (caveat below) |
| A7 (other session's companion) | six telemetry counters zero on the A1 cohort | all six 0; "A7 CHECK PASSED" | **Pass** |
| A8 | decisions invariant under unseen-state randomization, with visible-input positive controls | whole-match: strict tier 636/640 clean; 4 differ (attributed below) | **Accepted by the owner on 2026-11-16** as an exception: the strict zero-difference criterion is *not met as measured*; the four differences were accepted as legitimate world propagation and an id label (see A8 section) |
| Legend vs Warlord (B5-10) | **Re-baselined by the owner on 2026-11-16 from ≥60% to ≥45%** (Legend not worse than Warlord; doc 27 B5-10 and doc 14 §10.3 updated); ≥80 mirrored duels | 41/80 = **51.25%** (95% interval 40.3–62.2%; mean Legacy gap −111 ± 121) | **Pass** against ≥45% on the point-estimate rule. It would have failed the original ≥60%. The interval's lower end is below 45%, so a rerun could read under it |

Every gate now has a recorded result on the frozen tuple. A8 is an owner exception and Legend passes only against the re-baselined bar. The results are for the frozen tuple only (see below); this is not a closeout of WP-E5-9 on the current source.

## Frozen identity

Every run below used one frozen set, identical to what the A1 run used, executed from a private copy so edits by other sessions could not change it mid-run:

| Item | Value |
|---|---|
| HEAD | `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c` (`codex/e5-fief`, working tree dirty) |
| `content+src` aggregate (446 files) | `bee3fb3a8f45d68010a649f76b71ecb1b191a884742eeb8118d2b90c64a69dba` |
| `content+src+game` aggregate (1040 files) | `7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6` |
| Numeric-status pack | `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a` |
| Harness DLL | `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f` |
| Sim DLL (adjacent and canonical net8.0) | `a8cb7666042cf6651a6b9558b3d6db7b1f00566b5cc30041077e67b86540ad50` |

Per-file hashes: [frozen-identity/workspace-file-hashes.json](frozen-identity/workspace-file-hashes.json). Binary/pack hashes: [frozen-identity/frozen-binaries-and-pack.sha256](frozen-identity/frozen-binaries-and-pack.sha256).

**These are not the hashes quoted in the plan.** The plan's F-T7 candidate was `content+src` `fc3a3606…`, full `e42e3605…`, Sim `3fa5f4d9…`. The pack (`f360a3d9…`) and Harness (`aa7b830c…`) are the same, but the source tree and Sim DLL moved on before A1 was run. I followed the A1 run's set so that A1–A8 share one frozen identity, as the plan requires ("one frozen source/pack/binary set"). The plan says to refreeze after any change, which is what this is.

Before and after every run `tools/run_gate.py` re-hashed all 1040 source files in the private snapshot, the Release binaries and the pack. Every run records `unchangedDuringRun: true`.

**The shared tree has since moved on.** After about 22:07 another session changed 24 files in `civsim-e5` (Sim: `LivingEconomyModel*.cs`, `PoliticsSystem.{DynasticInheritance,Dynasty,FiefLaw,Trade,Voyage}.cs`, new `FiefSea10Trace.cs`; Harness trace commands; tests; one `game/` file). The live `content+src+game` aggregate is no longer `7decf87a…`. The results here describe the frozen identity only. If those Sim edits are part of the candidate, the affected gates must be refrozen and rerun.

## Method and exact commands

All duel and FFA runs: `dotnet <frozen>/bin/CivSim.Harness.dll ai --pack <frozen>/numeric-e5.pack --mode fief …`, 64 minutes, no `--grant-e5`, `--match-details` for per-seed rows. Seeds are the frozen A1 manifest, 40 seeds `20261019–20261058`, two mirrored matches each ([runs/seeds-a1-40.tsv](runs/seeds-a1-40.tsv), `2c72ac0b…`, identical to the A1 run's manifest). Each run's `run_metadata.json` has the exact argv, pre/post hashes and timings.

| Run | Arguments after `ai --pack … --mode fief` | Raw output SHA-256 | Elapsed |
|---|---|---|---|
| a2-grower | `--duel warlord grower --seeds 40 --first-seed 20261019 --minutes 64 --match-details` | `3fa7a511e1d08c69…` | 8692 s |
| a2-builder | `--duel warlord builder …` (same) | `d945483d805bba48…` | 5877 s |
| a2-wanderer | `--duel warlord wanderer …` | `6984bf2409584640…` | 1033 s |
| a3-rusher | `--duel warlord rusher …` | `ce7523bfa7c05d94…` | 7519 s |
| a4-chieftain | `--duel chieftain warlord …` | `a76e536418f60933…` | 7239 s |
| b5-10-legend | `--duel legend warlord …` | `48f65e31d2d989ca…` | 1367 s |
| a5-ffa | `--ffa warlord --seeds 120 --first-seed 20261019 --minutes 64 --match-details` | `69f6e9ecdab8def9…` | 18951 s |
| a6-exploit | `--exploit --generations 417 --children 24 --seeds 1 --screen-minutes 1 --minutes 64 --first-seed 21900001 --exploit-screen-first-seed 21800001 --exploit-validation-first-seed 21700001 --exploit-output …/finalists.txt` | `6e99718095220c87…` | 15476 s |

Full hashes are in `runs/*/run_metadata.json` and [MANIFEST.sha256](MANIFEST.sha256) (SHA-256 of every file in this folder). The parsing helper is `tools/parse_duel.py`.

Elapsed times vary because the machine was heavily shared (load average above 100 for hours); results are deterministic and unaffected.

### Censor counts

For duels: expected 80 matches; per-seed rows parsed; `censored = 80 − rows`. All six duels: **0 censored**, 80 rows, 40 distinct seeds covering `20261019–20261058`, exit 0, no crashes. There were no ties to adjudicate. A5: 600 `A5_FFA_MATCH` rows, 120 distinct worlds `20261019–20261138`, exactly 120 matches per rotation 0–4, zero-margin ties 0, zero exceptions. A6 has no per-match censor concept: 16/16 finalists completed their validation (48 pair rows); no crash. A8: 0 clone-checksum mismatches.

## Gate detail

**A2 / A3 / A4.** Win counts from the per-seed rows: Grower 62/18, Builder 68/12, Wanderer 59/21 (Warlord/opponent); Rusher 58/22; Chieftain 25 vs Warlord 55. A3: Warlord lost no settlement in any of 80 matches (`settlements lost per match: warlord 0.00 (in 0 matches)`; Rusher lost a settlement in 17 of them). A3's win rate of 72.5% clears 70% by 2.5 points; A4's 31.25% sits mid-band.

**A5.** The `ai` command's `--ffa warlord --seeds 120` is equivalent to `--check-m10`'s A5 stage (it multiplies the 40 requested seeds by 3). Builder is exactly at the 30% line (144/480). The earlier historical run read 29.4%. This is a pass under "no archetype wins >30%" but has no headroom; a rerun on any changed source could flip it.

**A6.** 10,009 distinct policies, 1-minute screen on world 21800001, policy RNG seed 21900001, 16 finalists validated at 64 minutes on three fresh mirrored pairs (worlds 21700001–21700003). Finalist rates: three at 50.00%, seven at 33.33%, six at 16.67%. Seed manifest: [runs/seeds-a6.tsv](runs/seeds-a6.tsv). The three ranges share nothing with each other (the harness enforces screen/validation disjointness) or with any E5 cohort seed found in `docs/MVP/balance` (those occupy `20260000–20799999` and `21000000–21199999`). The old A6 pack, screen seed and validation seeds were not reused. Claim scope: this searched candidate set only.

Caveat: best screen fitness is 1.00 in all 417 generations and all 16 finalists carry screen fitness 1.0, so the one-world one-minute screen does not rank policies and finalist choice reduces to diversity selection. The old protocol had the same property. Validation uses three pairs per finalist, so rates move in 16.7-point steps.

**A7.** Cited from the other session: [a7-output.txt](../m12-e5-9-fief-a1-current-source-output-rerun-2026-11-15/a7-output.txt) (`4e11b050…`): keystone stalls 0, dithering 0, avoidable starvation 0, undefended loss 0, herd crash 0, idle labor 0 over 80 matches / 160 AI seats. That session's `a7_metadata.json` still says `preflight_frozen_before_run` and records no exit code; the raw output and the process ending are the evidence. Its A1 record: [run_metadata.json](../m12-e5-9-fief-a1-current-source-output-rerun-2026-11-15/run_metadata.json) (`a5117755…`), raw output `b47afe68…`, 65/80, 0 censors.

**Legend vs Warlord (B5-10).** 41/80 = 51.25% (Legacy 5494.7 vs 5605.8), 0 censors, same seed manifest, mirrored. The original criterion was ≥60% over ≥80 mirrored duels, which this missed. On 2026-11-16 the owner re-baselined B5-10 to ≥45% (Legend must not be worse than Warlord), the rule the harness's B31 gate and 22 §16 already use; this passes on the point estimate. The reason: Legend's only differences from Warlord are a wider Spark search at its first expansion and a public-information coalition timing rule, and it has read 44–51% in every block since E3, so 60% would need a new Legend capability (a separate design project, not tuning). Honest uncertainty: 95% interval 40.3–62.2%; 31 of 40 seeds split their mirrored pair; a true 60% Legend would pass 48/80 only 55% of the time, a true 50% Legend would read ≥45% (36/80 or more) about 84% of the time. No AI change was made.

## A8, whole-match fog fairness

There was no whole-match A8 runner (only fixtures), so I wrote one: `tools/FiefWholeMatchFogFairnessTests.cs` (`7d60fbd3…`). It was built and run only in the private frozen snapshot's test project; it is not added to the repository's `src/CivSim.Tests`, so the shared tree's source aggregate is untouched. It plays full 64-minute Fief matches on the frozen pack and sim: 8 pre-declared seeds `20261019 + 5i` (`i = 0…7`), four AI seats (Warlord, Builder, Grower at Warlord level, Sage at Legend level), checkpoints every 16 seasons (16 per match). At each checkpoint, for every observer, it clones the live match through a snapshot, changes state in the clone, advances both one season-end pass, and compares everything the observer received (belief, own view) and did (research slots, queue, posture, beacons, decision log). The Sim DLL it loaded is the frozen one (`a8cb7666…`, logged by the test).

Final result ([a8-summary.json](runs/a8-final/a8-summary.json), `0373c911…`; rows `2781f95b…`; raw `d85be822…`; the run exits 1 because its assertions fail on the four differences below):

| Check | Result |
|---|---|
| Null control: unmodified clone vs live match | 512/512 identical |
| Strict unseen tier (never-contacted rivals' people, stores, powder, fleets; deposits and horse herds more than 8 tiles from anything explored; hidden research grants) | 512/512 applied, every category in all 8 matches; **508/512 identical** |
| Horse triple (latch reset; hidden vs reference; visible vs reference) | hidden 128/128 identical; visible 128/128 flipped Unknown→Seen |
| Visible controls: deposit, own stock, own fleet | 128/128, 128/128, 100/100 detected (28 fleet controls not applicable: the planted crew lapsed by season end) |
| Broad tier (rivals incl. contacted ones, original `PerturbUnseen` plus hidden research) | 512 applied; 485 identical; 27 differ in observer input; **0 decision-only differences** |
| Clone checksum mismatches | 0 |

**The four strict-tier differences** (`runs/a8-triage-strict/triage.jsonl`, `8f462019…`, isolated by perturbation category; the null clone is clean in all four):
- Seed 20261039, season 240, observers 2 and 3: caused by the hidden people/stores perturbation only; the sole input change is the observer's relation with civ 0 (−20 vs −10), identical for both observers, so a third-party effect on a rival's own behaviour. Decisions do not differ.
- Seed 20261034, season 256, observer 3, and seed 20261049, season 240, observer 0: people/stores only; the sole input change is a seen mining camp's allocation `id` (26 vs 25; 6 vs 5) at the same tile, with the same crew and last-seen season. The seed 20261049 case also changes the printed decision text ("best camp 6" vs "best camp 5"); with camp ids normalized in a diagnostic rerun ([runs/a8-diag-campid](runs/a8-diag-campid)) the decisions are identical.

**Owner decision (2026-11-16): the reading below was accepted.** none of the four shows an AI decision depending on hidden state; they are legitimate world propagation (a rival's reaction) and an allocation-counter label. The criterion as run (strict zero differences) is nonetheless not met as measured; A8 is recorded as accepted by the owner on that reading, not as a clean pass. One thing worth a decision: a camp's numeric id is a counter the AI can see, and it moves with hidden events. No decision was shown to depend on it, but it is an identifier channel.

**Id-keyed random streams (the camp-id question, followed up 2026-11-16; owner decision: leave as the accepted exception, record it as a class).** A camp's `id` comes from one world-wide counter (`_nextCampId++` in `LivingEconomyModel`), so it moves with camps opened by civs the observer has never met. The AI uses it as a label and also as part of the random-stream key for its raid-success simulation (`Strategist.Raids.cs:558`: `camp.Id * 16 + party`, 32 or more trials per estimate). Settlement ids key the raid and assault simulations the same way (`Strategist.Raids.cs:719`, `Strategist.War.cs:380`); I did not read the caravan raid's key. Consequences: (1) not a usable information channel: the AI receives a hash-derived noise draw, not a decodable count, and the player never sees it; (2) the AI is not strictly a function of what it observed, because a hidden event that shifts numbering can flip a borderline raid or assault call (in the observed case the expected value was −13.8 against a 0.5 margin, so nothing flipped, and decisions were identical once the id label was normalized); (3) the strict A8 tier cannot reach zero differences while it perturbs anything that changes founding or camp timing, because it counts the id as content. A test-only normalization of ids in the comparison would hide this, so it was not applied to any reported number. The proper fix is in AI source: key these streams on stable values (civ, tile, party size) instead of ids. That changes random draws across the whole AI, so every golden and the A1–A7 results would shift and need a refreeze and rerun, and A5 has no headroom; it should be done together with the planned refreeze on the current source, not separately. Nothing was changed.

**Protocol history, for honesty.** The first whole-match pass ([runs/a8-run2-first-protocol-no-nulls](runs/a8-run2-first-protocol-no-nulls)) used only the broad tier with no null control and compared the decision log unmasked: 30 failures of 640 and 28 fleet-control misses. Triage ([runs/a8-triage](runs/a8-triage), 495 clone runs) showed: 2 failures were a clone artifact (the null clone reproduces them: a restored clone logs its first decision with `Season = 0`); the fleet-control misses were a fixture that does not persist late in a match; the remainder were belief differences from perturbing state that can still reach the observer in one season (a rival reacting, scouts or herds crossing the explored boundary). I then added the null control, the Season mask, the strict tier, the persistence rule for the fleet control and the horse triple, and reran the full cohort. **The strict tier and these rules were therefore defined after seeing the first result.** The hardened run was repeated on the final test source and reproduced identically (same four differences, identical per-row outcomes across all 2304 rows; [runs/a8-run3-hardened-earlier-source](runs/a8-run3-hardened-earlier-source) is the earlier-source copy).

**A Sim defect found while building A8 (not fixed, not mine to fix).** `PoliticsSystem.ReadSnapshot` calls `ReadBronze` → `ReadDynasticInheritance` before `RefreshSettlements()`, and `ReadDynasticInheritance` compares the snapshot's settlement count with the stale cached `_sCount`. Restoring a Fief snapshot into a freshly created match therefore throws `InvalidDataException: Dynastic inheritance state has the wrong settlement count` once the match has founded or split a settlement. The first A8 attempt crashed on it ([runs/a8-attempt1-restore-defect](runs/a8-attempt1-restore-defect)). A real Fief save loaded into a fresh match looks exposed to the same failure. The defect is still present in the live source (`PoliticsSystem.DynasticInheritance.cs`, an uncommitted file belonging to another session). My A8 test works around it by setting the cache by reflection before restoring and verifying the restored checksum equals the source's, so a bad restore cannot pass silently. The harness test was run against the frozen Sim DLL, which is unchanged.

## What was and was not done

- Not changed: AI source, Sim source, content YAML, goldens, thresholds, any `draft` status. The repository and the shared worktree's source were not edited; this folder is new documentation and evidence only.
- A1 and A7 were not rerun; they are the other session's results on the same frozen identity.
- Not done: promoting any numeric row; ordinary or release Fief selection remains blocked.
- Open items: A8: accepted 2026-11-16, the camp-id question was followed up and left as the accepted exception (see the id-keyed random streams note), with the stable-key fix deferred to the next refreeze on the current source; Legend passes only the re-baselined 45% bar with a wide interval; A5 has zero headroom; the snapshot-restore defect; the plan's F-T7 hashes versus the actual frozen set; the live tree's later Sim edits are untested.

## Files

`runs/<gate>/{raw-output.txt, run_metadata.json}` per run; `runs/a8-final/{a8-rows.jsonl, a8-summary.json}`; `runs/a6-exploit/finalists.txt`; `runs/seeds-a1-40.tsv`, `runs/seeds-a6.tsv`, `runs/triage*-list.txt`; `tools/` (`run_gate.py`, `run_a8.py`, `run_triage*.py`, `parse_duel.py`, `q.sh`, the A8 test source); `frozen-identity/`; `MANIFEST.sha256`.
