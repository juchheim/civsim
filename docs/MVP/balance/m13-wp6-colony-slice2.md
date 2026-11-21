# M13 WP-E6-6 slice 2: TER-11 overseas colonies (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted (on top of [slice 1](m13-wp6-ocean-slice1.md)). Discovery chapter only (`DiscoveryChain` and `HasOceanReach` gate everything); Bronze, Iron and Fief bytes, hashes and snapshots are unchanged and nothing new is stored. All numbers are provisional.

## What the slice does

| Part | How |
|---|---|
| **A colony** | A member settlement of a civilization with TER-11 whose capital cannot reach it on foot (it lies across water). Derived each time from the walking-cost map; no flag is stored. |
| **Reach (the benefit)** | With TER-11 **and** SEA-12 a daughter band or a Beacon decree may found up to `ColonySettleReach = 40` tiles across the sea (outriggers: 24). The channel is 31 to 39 tiles, so this is what lets a civilization found on the far continent without stepping stones. The existing budding, voyage roll and `Reachable` check carry it; no second founding system. |
| **Sea strain** | A colony pays a further half of its ordinary distance Overreach (after Admin relief) in its Cohesion target, and 10 points more while its harbor is blockaded (`IFissionSiteAdvisor.CutOffBySea`, answered by the politics blockade list). |
| **Independence** | At each year end a colony whose Cohesion is below 45 rolls 60 permille (120 when blockaded) from `Pcg32.ForStream(seed, "economy.colony", settlement, season)` and, on a hit, secedes through the existing `Secede` path (the town becomes Free Folk). Replay-stable, recomputed from its key, never by score. The ordinary eight-low-seasons secession still applies. |

## Design choices worth knowing

1. **TER-11 is a trade, not a gift.** The reach is the benefit; the strain and the roll are the cost. A civilization that colonizes with a thin Admin staff loses the colony, which is the doc's intent.
2. **No `colonize` command.** The row's `unlock_action: colonize` is satisfied by the reach: the existing founding paths now cross the channel. A dedicated action would be a second founding system; not built.
3. **The independence roll fires only in low Cohesion**, so a well-run colony (admin, governor, luxuries) never rolls. The odds (6% a year, 12% blockaded) are a first guess for the E6 pacing runs.
4. **Interface change:** `IFissionSiteAdvisor` gained `CutOffBySea`; `PoliticsSystem` is the only implementer.
5. **Content text:** TER-11 gained a line stating the 40-tile reach; the content-pack golden was refreshed for it (`fb2fc8c5…` to `64fc2a9d…`).

## Tests (5 in `DiscoveryColonyTests`, all pass)

A settlement across the sea is a colony only with TER-11 in Discovery and the capital never is; TER-11 does nothing in Fief; a colony's Cohesion target is lower than the same town's without TER-11; independence is a deterministic yearly roll near 6% that never fires for a content colony; the colony reach is 40 only with TER-11 and SEA-12 and applies to decrees as well.

## Not done

1. The blockade strain and its doubled odds are wired but untested end to end (a test needs a war and a crewed galley; it belongs with the SEA-13 galleon slice).
2. A natural-play measurement of how often the AI colonizes and how often colonies break away needs the promoted E6 pack (WP-E6-12).
3. SEA-13 galleons, FOR-10, FOD-10, HNT-12, DIP-09; TRD-10/11 wait on D12.
