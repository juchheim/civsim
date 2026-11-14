# M12 WP-E5-3: E5 geography audit (saltpeter, rivers, horses)

**Status:** audit only. No world generation, worldgen golden, simulation golden or content was changed. Baseline: branch `codex/e5-fief` at `1d09770` (cut from `main` at `43a1648`). Raw output: [m12-wp3-e5-geography-output.txt](m12-wp3-e5-geography-output.txt); every number below is copied from it unless marked as derived from the per-start CSV.

## Result in brief (seeds 1–10,000; 40,000 starts, 20,000 geometrically distinct)

| Question | Answer |
|---|---|
| Is there saltpeter in every world? | **Yes**: exactly 12 deposits in every world (sulfur 8). |
| Does a start have saltpeter on its own ground? | **74.1%** (steps 1–3: depth-1 within 8, or within 12/16 with carts or water). **28.6%** within 8 tiles, 10.5% inside the claim radius 5. |
| Can it reach any saltpeter at all? | **94.7%** have an uncontested site within the E5 reach of 25; 2.0% have none within 25. |
| Worlds where **no** start has local saltpeter | **15.1%** (1,506 of 10,000). Powder there needs expansion toward the 25-tile edge or trade. |
| Sulfur (the recipe needs 0.10 per powder) | Scarcer than saltpeter: **64.3%** of starts have it locally; **54.4%** have both. In **30.9%** of worlds no start has both. |
| Water-wheel and sawmill sites | **Never a constraint**: 100% of starts have a river or lake tile within 5 (92.9% within 3), 76.7% sit on one; **95.5%** also have forest for planks. Wind-only starts: **0%**. |
| Wild horses | **12.6%** of worlds have **no** horse herd on the map at all (so nobody can satisfy HUS-07's [Seen: Wild horses]). **74.2%** of starts have an uncontested herd within 25 (48.6% within 15). In **18.9%** of worlds no start has a herd of its own. |
| All three on own ground (saltpeter route, river within 5, horse herd) | **55.8%**; two of three 36.7%; one of three 7.5%; none 0%. |
| Does a mate or a rival fill the gap? | Seldom. The generator is point-symmetric (civ 2 and 3 repeat civ 0 and 1; verified, 0 differences in 20,000 pairs), so when a start and its mate both lack a resource, every start in that world lacks it. Saltpeter: only **41.8%** of lacking starts have a capable mate; **15.1%** of all starts have no capable start anywhere. Horses: 26.8% have a capable mate; **18.9%** no capable start anywhere. |

## What it means for E5

1. **Rivers are not the geography problem.** Water mills and sawmills are available to every start, so the windmill is not a fix for riverless starts (there are none at game start). Its value is extra power units where a river settlement has used its sites; the start-only audit cannot see later settlements, which can be away from water.
2. **Saltpeter is the E5 resource gate, but powder is optional.** CHM-07 and everything after it (WAR-12 gunpowder weapons and, in E6+, CHM-08, DEF-10, MIN-10, SEA-13, WAR-13) sit **outside both the KEY-5 and KEY-6 closures** (checked against the catalog), so no keystone can soft-lock on geography. Powder still depends on trade or expansion for roughly one start in four, and the recipe's sulfur makes it scarcer than the [Has: Saltpeter] gate suggests.
3. **Horses can lock out whole branches, never the keystone.** HUS-07 is an E3 project gated on [Seen: Wild horses]. NRG-04 (horse collar), TRN-09 (stirrup and horseshoe) and WAR-10 (heavy cavalry) all depend on it through TRN-06, and **none** of them is on the KEY-6 path. HUS-07 is in the KEY-5 and KEY-6 closure only by the camel route (HUS-09); the sea route (SEA-07) does not need it (verified with a forced-sea closure). So in the 12.6% of worlds with no wild horses, those three E5 projects are unreachable for everyone, which is a content fact rather than a bug, but the AI forecast must read the [Seen] gate so it never plans them.

## Method (definitions fixed before any result was read)

- **Code:** [E5GeographyAudit.cs](../../../src/CivSim.Harness/E5GeographyAudit.cs) (new, read-only: it calls `WorldGenerator.Generate` and `WorldEconomyInput.FromGeneratedWorld` and writes nothing back), a two-line dispatch in `Program.cs`, and [E5GeographyAuditTests.cs](../../../src/CivSim.Tests/E5GeographyAuditTests.cs) (thread-count independence, mirror symmetry, worldgen golden unchanged). Modeled on the Iron audit ([m11-wp4](m11-wp4-iron-geography.md)).
- **Saltpeter and sulfur ladder** (same shape as the iron ladder): step 1 uncontested depth-1 deposit within the base mining reach of 8; step 2 within 12 (carts, TRN-05) or 16 (SEA-05, wet start site); step 3 a depth-2 deposit within that reach; step 4 only deposits nearer a rival start; step 5 within the E5 routine reach of 25 but beyond every mining reach; step 6 nothing within 25. "Own supply" is steps 1–3. No start landed on step 3 (0.0%), so every start with a reachable uncontested site has a depth-1 one.
- **Rivers:** river or lake tiles nearer this start than any other, counted within the settlement range 3, the claim radius 5 and 8. A sawmill start also needs forest (at least 15 L standing wood) within 8.
- **Horses:** wild herds of species Horse at their generated positions; uncontested herds within 15, 20 and the E5 reach 25; animal counts; "could be Seen" means any herd within 25, contested or not.
- **Uncontested:** strictly nearer this start than any other start (ties belong to nobody). A static stand-in for ownership; live claims differ.
- **Coast strata:** the raw report uses coastal (≤8), inland (9–14), and deep inland (≥15); the audit's maximum start distance is 20. This reporting split exposes the 15–20-tile starts as a separate cohort and does not affect world generation or the resource audit.
- **Trade geometry:** a capable partner has the resource on its own ground; route is a unit-cost walk, else a sail with SEA-04 (11 tiles from land), as in the iron audit. It is geometry only: no contact, relations, trade good or barter-party carrying is modeled.
- **Per-world figures** (worlds with no start having X) were derived from the per-start CSV (`--csv`), which is a function of the seed range like the report.
- **Reproduce:**

```bash
export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec
dotnet run --project src/CivSim.Harness -c Release -- e5-audit --seed 1 --seeds 10000 --csv /tmp/e5-audit.csv   # about 15 to 20 minutes
```

  Digest over all 40,000 per-start records: `396970e01a1cd3c450d4d2caefd816ea602d52f2b9106d07b5c1ac5f6ba0dd50`.

## Limits

- Start positions only: no expansion, no rivals' growth, no later settlements. Herds migrate seasonally (20 tiles), so a start with no herd may see horses later, and a herd may leave. Deposit **knowledge** is a discovery question (MIN-08 reveals veins inside own territory; the geology-discovery branch changes how early); the audit uses generator truth.
- The generator places saltpeter and sulfur as separate deposit kinds (12 and 8 per world) although the registry lists one "saltpeter / sulfur" entry; the audit follows the generator.
- All four starts per world are two geometric configurations, so "per world" figures are driven by 20,000 distinct starts, not 40,000 independent ones.

## Decisions this suggests (for you, plain English)

1. **Sulfur.** Powder needs a little sulfur but the research gate only checks saltpeter, and a third of starts have no sulfur of their own. Recommend keeping the gate as written and making the recipe work without sulfur at a lower yield (for example 70%), so a start with saltpeter is not stopped by a trace ingredient. This is a recipe choice, not a world change.
2. **Horses.** Recommend accepting the gap, as for the iron tail (Q9): no overlay, and the AI forecast must read the [Seen: Wild horses] gate. The three horse-dependent E5 projects stay optional and degrade gracefully (a world with no horses has no cavalry).
3. **Windmill role.** Since every start has a river, recommend writing the windmill as extra capacity (more power units, drainage, and for later settlements away from water), not as a "no-river fallback".

None of these is built; they are inputs for WP-E5-4 onward.
