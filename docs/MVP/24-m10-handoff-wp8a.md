# 24 — M10 hand-off: WP8a (Mandate, Decrees, compacts, caravans, Tin Folk)

> Checkpoint written when the work package was stopped early for budget (2026-10-23). Nothing is committed. Release build is
> clean, `dotnet test CivSim.slnx -c Release` is at the known baseline (4 failures, none from WP8a: SourceRulesTests on
> `Strategist.War.cs` only, two StrategistDecisionTests, `StrategistScenarioTests.It_declares_war_only_with_the_force_ratio`),
> `validate --strict` is clean, and the toy, research-v1 and politics-v1 goldens pass **unchanged** (every new state is idle and
> unhashed until the Bronze Age; no golden was regenerated). No content or constants were changed.

## 1. Done (code in place, compiles, exercised by the live harness)

| Piece | Where | Notes |
|---|---|---|
| Commands `18 IssueDecree`, `19 SetEmbargo` (appended) | `Core/Command.cs`, `PoliticsSystem.Step` | 22 §4.5 argument packing in `Politics/DecreeTypes.cs` (`DecreeArgs`); command 12 with A < 0 raids a caravan (Bronze only) |
| Decree queue and resolution | `Politics/PoliticsSystem.Decrees.cs` | resolved in the politics step at season end, civs ascending, issue order, then the civ's policy; Mandate spent only on success via `TrySpendMandate`; refusals emit `DecreeRefused` (+ `OrderRefused` for commands) with new reasons `NoMandate, NoDonor, NoSite, OutOfReach, Rejected` |
| Found a village | `ExecuteFound`/`FoundCheck`; economy `FoundByDecree` (fission refactored into `SplitSettlement`, identical for natural fission) | donor by pop ÷ cap (≥ 20 people, ≥ 60%, off cooldown), 40% (≥ 8), Cohesion 60, counts as the donor's fission, TER-05 claim ×1.5 for 8 seasons |
| Expedition | `ExecuteExpedition`/`ArriveExplore` | 4 adults, range 2 × Reach (×1.5 TRN-06), reveal along path (sight 3/+1) and radius 5, Aware + 3-in-4 contact roll within 2 tiles on `politics.expedition`; report on return (at once with WAY-04); gate resources (deposits with INQ-03, herds) |
| Open a mine | `ExecuteMine`/`MineCheck` → `OpenMiningCamp` | known tile, not rival/Tin-Folk land, within mining reach |
| Raise the levy | `ExecuteLevy`, `LevyFor`, `TryTakeLevy` | warriors + 50% of others within 2 seasons' travel; host idle in the economy (`SetIdle`, `WorkCapacity` = 0); **combat does not consume it yet (WP8b)** |
| Corvée | `ExecuteCorvee`; economy `SetCorvee`/`HasCivicWork` | civic build labor ×1.5 for 2 seasons, aimed structure, Cohesion −3; **walls not buildable yet (WP8b)** so a walls Corvée is refused NoSite |
| Compact / Invite / Pact / Treaty | `Politics/PoliticsSystem.Trade.cs` | standing routes, scarcity valuation `UnitValue` (base × clamp(need ÷ stock, 0.5, 3)), geometric-mean price, acceptance rule (both gain ≥ 10%/5% NUM-04, Relations ≥ 0) pluggable via `ICompactJudge`; ends on war, Relations, embargo, blocked route, party gone |
| Caravans | `SendCaravan`/`ArriveCaravan`, `OrderRaidCaravan`/`ArriveCaravanRaid` | visible `Expedition` kind Caravan with id, cargo, payment on arrival, plague carried via `ImportOutbreak`; raids: −30 Relations with both traders, reputation −10 |
| Embargo | `OrderEmbargo` | free from KEY-3, ends compacts in the good, Relations −10 |
| Goods barter (E3 only) | `ChooseBarterGood` in `ArriveBarter` | falls back to food-for-food; E0–E2 unchanged |
| Tin Folk | `SpawnTinFolk`, economy `SpawnVillage`/`WorkTinPlacer` | once per world at the first KEY-3, tier-1 tin placers, unowned, ≥ 8 from settlements; Free Folk flagged; data model `TinFolkVillage {Settlement, Placer, SinceSeason, Joined}`; 0.8 defense, palisade, their land (radius 3) refused to Found/Mine and to the planner (`ITileOwnership` returns −2) |
| Council auto-Decrees | `Politics/CouncilDecreePolicy.cs` (+ helpers in `PoliticsSystem.Council.cs`) | Mine (copper, then tin / arsenical), tin Compact yearly, Found at cap (donor ≥ 85%, ≤ 2 per age, never more than natural foundings), Corvée on Temple/Palace at cap, Expedition at cap. Runs while the civ's research Council is on; pluggable via `SetDecreePolicy(civ, policy)` |
| Strategist surface | `IStrategistActions` (Ai/Strategist/StrategistView.cs) gained `Mandate, MandateCap, DecreeCost, IssueDecree, SetEmbargo, RaidCaravan, UseCouncilDecrees` (defaults refuse, so test recorders compile); `AiOrders` implements them | WP9 turns the Council policy off with `UseCouncilDecrees(false)` |
| Forecast card | `Ai/Forecast/DecreeForecaster.cs` → `DecreeForecast` | fog-fair: own state exact, others via `Belief` or known tiles only |
| Frame / events | `MatchFrame` (Focus Mandate/costs/levy, Route*, PartyGood/Cargo/Id, embargo masks, pact flags, TinFolk/Corvée/LevyTarget settlement flags), `ViewTypes` (PartyKind, FigureActivity, ViewEventKind 58–68, ChronicleKey 90–110 with text keys), `FrameBuilder` | expedition reveals mark tiles explored; belief builder too |
| Snapshot/hash | politics Bronze block appended to the snapshot; economy `SnapshotVersion` 17 → 18 | hashed only when non-empty |
| Economy fixes | Free Folk do not bud; Free Folk are excluded from metal sharing/access and from research practice | prevented Tin Folk tin leaking to their nominal civilization |
| Harness | `research --report-decrees` (T11, T14–T16, T20, T26, T27); `--report-metal` now skips Free Folk | |

## 2. Partial / not done

- **No new unit tests** were written (decree order, refusals, Found rules, expedition determinism, levy, compact EV, embargo, caravans, Tin Folk once, Council policy, snapshot round trip, replay with commands 18/19). Existing snapshot/replay/fog tests pass with the new state. This is the first thing to finish.
- Levy host and caravan raids are not used by the v0 mind or the Strategist; the levy's combat use, walls, Free Folk claims (22 §11) and capture penalties for Tin Folk (`TradedWithFolk` is provided) are WP8b.
- Escorts, coalition embargoes and Decrees by EV are WP9.
- Tin Folk carry a nominal civilization id (nearest civ) because every economy settlement needs one; they are Free Folk everywhere politics and research look, but their own metal planner follows the nominal civ's research.
- 07d was **not** updated with a "WP8a live results" section; the numbers below are the only measurements.

## 3. Measurements so far (seeds 20261019 and 777, Council, before the last two fixes)

`research --report-decrees` (run before the Free Folk metal-access fix and the Council's Expedition fallback; **re-run needed**):
KEY-4 median 45.7, E3 median 11.9 (in band). T11 settlements at KEY-4 median 5 (target 7). T14 claimed 24% (30–42%). T15 1/4 pairs
touching (≥ 75%). T16 Decree share of E3 foundings 33% (25–50%). T26 Tin Folk 6 and 6 per map (3–6); 0 compacts with them by
KEY-4 (MET-05 lands after KEY-4 for the Council, so the tin compact rule never fired). T27 Mandate earned 5.5, Decrees 2, 37% of
seasons at the cap (the Expedition fallback was added to address this; unmeasured). T20 read 8/8 but was inflated by the Tin Folk
access leak (fixed since).
`research --report-metal` (same state): T17 bronze 0.29 L / tin 0.008 L / charcoal 1.24 L / wood 8 L per 100 a year, labor 2.1%
(WP6 planner-only 0.00 L, `--decree-mines` 0.43 L); T18 per settlement 29% median, 18 of 50 at ≥ 70%.
The last `--report-decrees` re-run crashed on an overflow in `TurnedBackAtSea` (negative `ToSettlement` cast); fixed, not re-run.

## 4. How to finish

1. Re-run `research --pack civsim.pack --report-decrees`, `--report-metal`, `--report-e3`, `--check-m10` and record a "WP8a live results" section in 07d.
2. Write the tests listed in §2 (a scenario helper: `ForceContact`, `IssueDecree`, `PlayerCommands.FoundVillage/…`).
3. If a Decree path misbehaves, the whole Council layer can be disabled per civ with `politics.SetDecreePolicy(civ, null)`; commands 18/19 only act once KEY-3 has unlocked Mandate.
