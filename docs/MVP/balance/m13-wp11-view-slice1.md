# M13 WP-E6-11 slice 1: the Discovery frame (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted. First slice of WP-E6-11 ([38 §8](../38-discovery-age.md), [38 §9](../38-discovery-age.md)): the headless read model that the Godot cards will draw from. **No game or Godot code was touched**; nothing was run in the game. Content and simulation behavior are unchanged, so no golden moved.

## What the slice does

| Part | How |
|---|---|
| **`DiscoveryFrame`** | New read-only block on `MatchFrame` (`frame.Discovery`), inactive outside a Discovery match, shaped like `FiefFrame`: arrays aligned with the frame's settlement block, filled **for the viewer's own towns only**. Per town: coal litres, atmospheric engines, coke ovens, navigation canal, factory with its coverage (0 to 1) and operatives in post, soot at the site, colony flag, staffed botanical garden, learned society, bastion fort, crewed galleon, exchange house. Quantities are raw Q32.32. |
| **Civilization block** | The viewer's own: ocean reach (SEA-12), a staffed exchange, joint-stock and insurance open, a staffed society, colonies held, KEY-7 progress in permille (1000 once done). |
| **`DiscoveryTownReadout`** | One public method on the economy, `DiscoveryTown(settlementIndex)`, returns the per-town snapshot from existing private state (engine count, factory coverage, colony test, garden and society staffing, ...), so the builder needs no new access to internals. Empty outside Discovery. |
| **Builder** | `FrameBuilder.Discovery.cs`, called after the Fief block. Pure reads: nothing is stored or hashed. |

## Tests (2 in `DiscoveryFrameTests`, both pass)

The frame is inactive in Fief, and in Discovery it shows the engines, oven, canal, factory, bastion and coal of the viewer's own town while a rival's town stays zero; building a frame every 150 ticks for 3,000 ticks leaves the match checksum equal to an unframed twin's.

## Checks

Full suite 1,616 passed with the 5 known failures (the view-determinism failure is the known politics-golden one); toy, research and politics goldens unchanged.

## Not done in WP-E6-11

1. **The cards** of [38 §8](../38-discovery-age.md) (town card lines, coal and engine readouts, the exchange and colony panel, KEY-7 progress) and the **Godot payload and scripts**; they read this frame, need the game to be run to be checked, and are best done together with the plates and art pass.
2. **Mode selection and saves** in the game shell, `--mode discovery` plumbing in the Godot launcher.
3. **Era UI skin and Moments** (docs 34 and the generic kit).
4. **Build guidance text** for the new structures beyond the gate reasons already in `BuildText`.
