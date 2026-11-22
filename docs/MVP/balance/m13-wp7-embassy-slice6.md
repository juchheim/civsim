# M13 WP-E6-7 slice 6: DIP-09 resident embassies (2026-11-22)

**Status:** built and tested on `codex/e6-discovery`, uncommitted. Gives DIP-09's diffusion bonus the condition its row states ([38 §5.3](../38-discovery-age.md)). Discovery only; nothing is stored, so Bronze, Iron and Fief bytes, hashes and snapshots are unchanged. All numbers are the registry's.

## What the slice does

Before: DIP-09's `diffusion_discount` x1.1 applied to every project a contacted rival had finished, unconditionally. Now the bonus is **an embassy's**: it applies only to rivals the civilization keeps a resident embassy with, and is taken back out for the others.

| Part | How |
|---|---|
| **Embassy** | A resident embassy stands between two civilizations that have met, are not at war and have not turned hostile (relations at or above zero). It needs no order: one per rival, kept while those hold, read live from diplomacy (`PoliticsSystem.ResidentEmbassy`). |
| **Cost** | `EffectiveCost` counts the rivals that know a project and, of those, the ones with an embassy. The discount from each embassy rival uses the full step; each of the others uses the step with DIP-09's factor divided back out. The 0.5 cost floor still binds. |
| **Wiring** | `ResearchSystem.EmbassyOracle` (null means every contacted rival counts, the old behaviour, so existing rigs are unchanged); `MatchModel.Create` sets it from politics. |

## Design choices worth knowing

1. **No new command.** The row says "a resident embassy in a contacted civilization", which could mean an action. An automatic embassy keeps replays, the AI and the UI untouched. If a player order is wanted later, it would append a `CommandKind` and replace the oracle with stored state.
2. **War ends it.** A stranger does not stay at an enemy's court, so a civilization at war with a rival loses that rival's share of the bonus, the same way a society's bonus lapses when it is empty.
3. **Not modelled:** the row's "shows that civilization its research slots to the host" (a view matter).

## Tests (2 in `DiscoveryEmbassyTests`, all pass)

With an embassy the knowing rival's discount is deeper; without one DIP-09 adds nothing (to rounding); no oracle means every contacted rival counts; Fief never applies the rule.

## Not done

The research-slot reveal in the UI, a player order for embassies, and the remaining no-consumer rows (ART-10, COM-11, NUM-10, INQ-09, BLD-13 layout bonus).
