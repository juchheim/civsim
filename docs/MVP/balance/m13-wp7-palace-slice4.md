# M13 WP-E6-7 slice 4: the Renaissance Palace Wonder (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted. Adds the BLD-13 Palace as the sixth Wonder ([38 §5.7](../38-discovery-age.md)) on the existing Wonder framework. Everything is inert outside a Discovery match; Bronze, Iron and Fief bytes, hashes and snapshots are unchanged (the structure is appended to `IronStructure` and is written only in Discovery snapshots). All numbers are provisional.

## What the slice does

| Part | How |
|---|---|
| **The Wonder** | New structure `renaissance_palace` (150 work-seasons, 50 ashlar, 20 wood, 6 plate glass; the registry's numbers). One in the world, built in a civilization's most populous town of tier 1 or more, needs BLD-13, STN-13 and a staffed master mason. It is a large, stone work (crane and STN-12 shorten it). Leagues can pledge labor to it like any Wonder, and completing it earns the Wonder Legacy for BLD-13. |
| **Plate glass** | `IronSpec` gets a `PlateGlass` field. Plate glass is paid from the **whole realm's stores** (builder first, then other towns by ascending id), the same "held anywhere in the realm" rule the instruments use. While a Palace is under construction, towns of 50 or more want plate glass until the realm holds six, so the towns that can make it (salt, lime, glass, coal or char) do and the rest spend nothing. |
| **Not in Fief** | The gate refuses the Palace outside Discovery. |

## What natural play shows (seeds 20261019 and 20261020, 130 minutes, E5 and E6 granted)

One civilization in four finishes the Palace in each world. The others sit on the **Gothic Cathedral** (E5): its four *plain* glass is not in store, so the town's one-at-a-time Wonder slot waits. That is the E5 glass shortage (the glass recipe needs charcoal, lime and stone in one town), not a Palace fault; the Palace had the same shape before pooling (blocked on plate glass for want of salt in the builder's town). Pooling plate glass fixed the Palace's own half of it.

## Design choices worth knowing

1. **Realm pooling.** A Palace town rarely holds salt, so it cannot make plate glass itself. Pooling matches how instruments already work and avoids a new transport model. The cost is that plate glass appears in several towns while it is wanted (bounded: six each, only while a Palace is being built).
2. **The Cathedral keeps its place in the queue.** I did not reorder Wonders. If E6 pacing wants the Palace built more often, the fix is plain-glass supply (an E5 matter), not Palace priority.
3. **The BLD-13 city-layout speed bonus** (`city_move_and_build_speed` x1.15) still has no consumer; no view, art or core district yet (`CoreDistrict` would need a new entry).

## Tests (4 in `DiscoveryPalaceTests`, all pass)

Gate (needs BLD-13, STN-13, a master mason; absent in Fief); plate glass, ashlar and wood paid once and conserved; plate glass taken from the builder first then the realm's other towns, never another realm's; one in the world and the Wonder Legacy recorded.

## Not done

Palace art and core district, the layout speed bonus, any effect of owning the Palace beyond the Legacy (the docs give none), and plain-glass supply for the Cathedral (E5).
