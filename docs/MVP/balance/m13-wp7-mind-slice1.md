# M13 WP-E6-7 slice 1: INQ-07, INQ-08, NUM-11 and GOV-12 (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted (on top of the committed Factory slice). First slice of WP-E6-7 ([38 §5.3 and §5.4](../38-discovery-age.md)): the research-side and state-side modifiers of the KEY-7 path. Everything tests `Rules.IsDiscovery` or `DiscoveryChain`; Bronze, Iron and Fief bytes, hashes and snapshots are unchanged and nothing new is stored. All numbers are the typed row values or marked provisional.

## What the slice does

| Part | How |
|---|---|
| **INQ-08 project cost** (KEY-7 prerequisite) | Every project costs the typed x0.9 less (x0.89 for the pioneer who finishes it first, like every project effect). It is folded into the **diffusion discount's floor**: the two stack, but the combined factor never falls below the half floor, so "no stack reaches zero". |
| **NUM-11** | INQ projects cost a further x0.9, through the same floor. |
| **INQ-08 Insight** | The typed x1.1 lifts the **staffed Insight sources only** (roles and institutions: scholars, scribes, monks, structures), not the Commons, as the row says. |
| **GOV-12 nation-state** | Overreach counts no farther than **6 tiles past the free radius** (the row's "Cohesion is uniform across the realm instead of falling with distance") and what remains is cut **40%** (the typed x0.6). Needs a living capital: with none, the town pays the ordinary strain. |
| **INQ-07 telescope** | The row's Cohesion cost of 1 in every settlement is now read for Discovery (the Cohesion terms read era 6), and **BEL-12 cancels it**. Sea routes are 5% more reliable (the loss risk is cut by 1/1.05 after WAY-08 and WAY-10). |

## Design choices worth knowing

1. **Costs share one floor.** INQ-05's old rule multiplies after the diffusion discount, with no floor. The E6 modifiers are folded into the diffusion discount instead (as the row text asks), so the lowest any project can reach is the existing floor times its list cost, however many knowers and modifiers stack.
2. **Uniform means capped, not flat.** A literal flat Cohesion would erase Overreach and make the Admin span and the capital meaningless; a cap at 6 tiles past the free radius and a 40% cut keeps distance mattering a little and removes the runaway that makes far towns secede. A colony still pays its sea share of whatever strain is left.
3. **SOC-12's Commons x1.2 needs no code.** The research system already reads `commons_insight` from every completed row, so it applies when the row is promoted. Its goods-demand x1.1 has no consumer yet.
4. **Tower sight (INQ-07 x2) is not built.** Sight is a belief and fog rule in the AI and view code; doubling it is a separate change with its own acceptance (the AI's belief tests).

## Tests (4 in `DiscoveryMindTests`, all pass)

INQ-08 and NUM-11 cut costs, never below the floor, leave other civilizations alone, do nothing in Fief; INQ-08 lifts a staffed academy's Insight and not the Commons; GOV-12 raises a far town's Cohesion target and does nothing in Fief; INQ-07 costs a point of Cohesion until BEL-12 and takes 5% off sea-route loss.

## Not done in WP-E6-7

1. LRN-09 learned societies (10% Know-how spillover, staffed structure), DIP-09 resident embassies (diffusion x1.1 per rival), tower sight, SOC-12 goods demand.
2. MEC-09 precision parts, CER-11 glass, CHM-08 acids as goods; the WAY-10 chronometer good and navigator (the project alone carries WAY-10's benefit today).
3. COM-11 newspapers, ART-10 patronage, BLD-13 and the Palace Wonder.
4. **SOC-12 and TRD-10 depend on the credit decision (D12)**: SOC-12 requires TRD-08 and TRD-10 requires the pooling rule, so the KEY-7 path is closed in play until D12 is answered.
