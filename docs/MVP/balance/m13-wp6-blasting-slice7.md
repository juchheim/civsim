# M13 WP-E6-12 follow-up: MIN-10 black-powder blasting (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted. Closes the MIN-10 row left without a consumer by the coverage audit. Everything tests `DiscoveryChain`; nothing is stored, so Bronze, Iron and Fief bytes, hashes and snapshots are unchanged and the content-pack golden is untouched. Numbers are the row's typed values.

## What it does

| Part | How |
|---|---|
| **x2 output** | A Discovery town with MIN-10 and **powder in store** (at least 0.1) digs at the row's `mine_quarry_output` multiplier (x2, scaled by adoption) in its mining camps, coal camps included (one camp loop serves every dig), and quarries blocks at the same multiplier. |
| **Powder cost** | The town pays **0.02 powder per litre** it dug or quarried, from the one powder good the Arsenal and the cannon draw on, and never more than the store holds. With the store empty it works at the ordinary rate, so mines **compete with war** for saltpeter and sulfur as the row says. |

## Design choices worth knowing

1. **One rule, no new good.** Powder is the existing Fief good (`G-POWDER`); no new slot, no new snapshot section.
2. **Camp and quarry work only.** The multiplier lives in the camp loop and the quarry rate, where almost all Iron and Discovery ore comes from; a town's own-tile mining without a camp stays at the ordinary rate (a small share, and it keeps the change to two sites).
3. **Blasting is decided by the store at the start of a crew's turn**, not per litre: a crew that starts with powder digs the whole turn at x2 and pays what it dug, capped by the store. That can overshoot the store by a litre or two of ore at x2, which is the row's "short of powder" case and is cheap to the model.

## Tests (2 in `DiscoveryBlastingTests`, both pass)

A town with MIN-10 and powder blasts at the typed multiplier (the quarry rate rises by exactly that) and otherwise works at the ordinary rate, never without the project or in Fief; fifty litres cost one powder, the store never goes negative and an empty store ends blasting.

## Checks

Full suite 1,620 passed with the 5 known failures; toy, research and politics goldens unchanged; a 130-minute Discovery match with E6 granted runs to KEY-7 in all four civilizations as before.

## Not measured

How much powder natural play actually spends on blasting (the harness does not count it), and whether the stock AI ever holds powder in a mining town: the planner does not produce powder for mines, so MIN-10 is a bonus where an Arsenal's powder reaches a mining town. If E6 pacing wants it to matter, the lever is a powder recipe in the planner, not the multiplier.
