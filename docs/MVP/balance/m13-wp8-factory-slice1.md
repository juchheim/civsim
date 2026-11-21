# M13 WP-E6-8 slice 1: the Factory and local soot (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted (on top of the committed WP-E6-6 slices 1 to 5). First slice of WP-E6-8 ([38 §5.7](../38-discovery-age.md)): MEC-10, the KEY-7 prerequisite, with the first local pollution. Discovery chapter only: the factory kind, the operative role, the soot array and its hash and snapshot tail all test `DiscoveryChain`, so Bronze, Iron and Fief bytes, hashes and snapshots are unchanged. The soot is one per-tile array written only in Discovery snapshots; no snapshot version bump. All numbers are provisional.

## What the slice does

| Part | How |
|---|---|
| **The building** | New structure `factory` (MEC-10; 50 work-seasons, 20 block, 10 wood, 4 iron for the registry's 4 cast iron). Hard gate: a town of **120 or more** with MEC-10 and a **power source** in its ledger (water mill, windmill or fired engine); one to a town. The planner raises it when the gate opens. |
| **The crew** | **Twelve operatives** (new role 13) leave other work to staff it. That draw is the row's "urban migration", shown honestly as adults pulled off the fields and other work; the staffing pass still keeps four other adults at home. A factory with no power source takes nobody. |
| **The effect** | Every recipe the town runs costs `1 / (1 + c)` of its labor, where `c` = (operatives in post / 12) x (power hours the shared ledger has left after grinding, sawing and powered craft / the crew's 12 hours). At full power that is exactly **x2 output per worker**; at half power it is a third less labor. It reads the existing ledger's unused hours, so the factory competes with the mill's other jobs for the same power. |
| **Soot** | A working factory (some crew and some power) adds **1 soot to its own tile and 0.5 to each of the six tiles around it** each season, up to 10 a tile; every tile's soot fades by a tenth a season (vanishing below 0.01). **Health:** each point of soot on a town's own tile adds a fiftieth to its people's endemic death hazard, up to a fifth more. A real local state, not a global score. |

## Design choices worth knowing

1. **The factory is a labor-cutter, not a new output.** A recipe's labor is the one place every craft already passes through (`RecipeLabor`), so the x2 applies to what the town actually makes (tools, kits, coke, plank, iron) with no per-good rule and no free goods.
2. **It runs on steam in practice.** Twelve operatives draw 12 power hours; a water mill gives 5 and a fired engine 10, and grinding and powered craft take their share first. A town gets the full x2 only with an engine or two beside its mill, which ties the factory to coal as the row intends.
3. **Natural play works.** In the test fixture the planner raised the factory six seasons after the gate opened, staffed it, powered it from the engine (coverage about 0.42 at one engine) and sooted the tile; when the engine ran out of coal the coverage fell to zero and the soot began to fade, as the row's "no power, no effect" says.
4. **Operatives' own work is spent on the role,** as every staffed role's is, so a factory costs a town twelve workers' general labor before it pays back.
5. **Soot does not yet touch the fields.** The row says "soots adjacent tiles"; the health weight is built, and a field-yield penalty is left for the pacing runs.

## Tests (6 in `DiscoveryFactoryTests`, all pass)

The gate needs MEC-10, size 120, a power source, and does not open in Fief or twice; coverage and the labor factor follow the crew and the power (x2 at full power, half the crew gives half the effect); soot builds to its cap and fades by a tenth; the health factor is 1 in clean air, a fiftieth a point, capped at a fifth, and exactly 1 in Fief; soot and the factory survive a snapshot restore and continue identically; natural play raises, staffs and powers the factory and soots the town.

## Not done in WP-E6-8

1. **FIB-12 weave and spin x3** with the power-ledger share, and cottage cloth as a trade good (needs a cloth recipe and the TRD-10 credit decision, D12, since FIB-12 requires TRD-10).
2. **A soot penalty on fields** and city migration beyond the staffing draw.
3. **AGR-12 four-course, HUS-11 stall-feeding, FOR-09 garden, MED-09 anatomy** (the rest of systems E).
4. A natural-play measurement of how often towns reach a factory and what the x2 buys (WP-E6-12).
