# Resume prompt: Faith & Fief (E5), M12

Paste this to the next AI. Work is in the git worktree `/Users/ejuchheim/Projects/CivSim/civsim-e5` on branch `codex/e5-fief` (do NOT work in the shared main tree `/Users/ejuchheim/Projects/CivSim/CivSim`; other sessions edit it). Read `CLAUDE.md`, `docs/MVP/27-faith-and-fief-age.md` (the plan; §9 is the work-package table, §11 the decisions) and the `docs/MVP/balance/m12-wp*.md` reports first. `DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec`. Explain decisions to the user in plain English with a recommendation they can accept (they are not a coder). Commit only when asked; do not push.

## Where we are
E5 = Faith & Fief (Medieval), 49 projects + KEY-6. Decisions confirmed by the user: default-off chapter that contains Iron; Fief-only claim upkeep (10% of farm yield, TER-10 cuts it 30%); E5 aims for about 25 minutes KEY-5 to KEY-6 (current draft F-T1 era-base fit is 6,400, from a lower-confidence 15/20 completed-world calibration; final acceptance still requires a fresh 80-world holdout); E5 late projects cost double (7 of 49 by the 28 §7 definition); Thriving Legacy; no countdown after a rival's KEY-6 (read as: the chapter ends only on the human's KEY-6; the user has not confirmed that reading); the Iron wood and stone changes stay for Iron.

Done and committed on the branch: plan doc 27; four Python oracles and `e5_anchor_check.py` / `e5_anchors.py` (`docs/MVP/balance/tools/`); all 50 E5 rows typed in place (still `status: draft`; validator V22 wants them promoted together); registries; the geography audit (`iron-match`-style `e5-audit`); `ChapterMode.Fief = 2` plumbing (`ChapterRules`, `MatchEnd`, replay/snapshot/hash mode byte, `FiefChain` economy flag, `Key6` forecast field, `--mode fief`); the timber and stone fix for Iron construction (`LivingEconomyModel.BuildWood.cs`); the first power slice (below).

**Uncommitted in the worktree right now** (finish by running the full suite, then commit when the user asks): `LivingEconomyModel.Power.cs`, the three new Iron structures (windmill, sawmill, blast furnace), `BuildWhy` additions, `FiefPowerTests`, registry cleanup (pig iron removed), `iron-match --fief --grant-e5 --no-furnace`, content-pack golden refresh, `docs/MVP/balance/m12-wp8-power-furnace.md`, this file. The last full run had only the two recipe-count assertions failing; they are fixed (56 recipes) and the affected test groups pass. Rerun `dotnet test CivSim.slnx -c Release` once to confirm.

## How to check your work (must stay green)
```bash
dotnet build CivSim.slnx -c Release
dotnet test CivSim.slnx -c Release
dotnet run --project src/CivSim.Cli -c Release -- validate --strict
dotnet run --project src/CivSim.Cli -c Release -- build-pack --out /tmp/p.pack --check-golden tests/golden/content-pack.txt   # refresh with --write-golden after deliberate content changes
dotnet run --project src/CivSim.Harness -c Release -- toy --check-golden tests/golden/toy-v1.txt
dotnet run --project src/CivSim.Harness -c Release -- research --pack /tmp/p.pack --minutes 12 --bot grower --check-golden tests/golden/research-v1.txt
dotnet run --project src/CivSim.Harness -c Release -- politics --pack /tmp/p.pack --minutes 24 --check-golden tests/golden/politics-v1.txt
python3 docs/MVP/balance/tools/e5_anchor_check.py     # after any Iron change on main
```
Rules that bit us: every E5 effect must be gated on `FiefChain` so Bronze and Iron stay byte-identical (check an `iron-match --seeds 2` output before and after); the economy content fingerprint treats E4 and E5 draft rows as one prose placeholder (`Era is 4 or 5` in `EconomyContent.Bronze.cs` and `LivingEconomyModel.cs`), and Fief will need its own full fingerprint before E5 effects are promoted; new registry structures need a settlement-core size class in `SettlementCoreGrowthTests` (docs 31a); no `Dictionary`/`HashSet`/floats/clocks in `CivSim.Sim` (SourceRulesTests); in zsh use `${=var}` to word-split.

To run Fief matches you need a temporary pack with E5 promoted: copy `content/`, change `status: draft` to `numeric` on every `era: 5` row and on KEY-6, `build-pack --content <dir> --out e5.pack`, then `iron-match --pack e5.pack --fief [--grant-e5] --minutes 130 [--it6-accounting]`. Never commit that promotion until WP-E5-11.

## What remains (plan 27 §9)
1. **WP-E5-5 rest, systems C to G** in the same pattern as `LivingEconomyModel.Power.cs` (gate on `FiefChain`, own state only in a Fief snapshot/hash section, tests with `BronzeFixture.CreateFiefWithCamp`, then a whole-match A/B):
   - Land and food: three-field rotation (+50% as cropped share 1/2 to 2/3 times a 1.125 legume yield), horse collar, selective breeding, game reserves, manorial estates and the Fief-only claim upkeep (10% of farm yield paid as grain to the non-farming staff).
   - Faith and learning: monastery (4 monks, 16 Insight/yr, copies the Know-how pool), pilgrimage, universities, paper, movable type, hospital, quarantine, Gothic Cathedral (Wonder). Staffing is the hard part: live Iron staffs about 0.4 scholars and 0.7 missionaries per 100 adults, so design the staffing rule first.
   - Law and money: canon law, charters, guilds (durability, not tool life), banking (no new money), dynastic marriages.
   - War: heavy cavalry (open field only), crossbows, gunpowder and cannon (cannon = two engines' damage on the existing wall-integrity track; concentric castle multiplier 1.5; needs cast iron, saltpeter, powder; cast iron and cannon goods need `IronGood` slots), crucible steel.
   - Sea and maps: cogs, lateen, compass, portolan.
2. **WP-E5-9 AI and forecast:** Spark KEY-6 closure, Strategist powder and cannon logic, blunder telemetry; planners must read the [Seen: Wild horses] gate (12.6% of worlds have no horses).
3. **WP-E5-10 view and game:** Fief mode choice, `GameSave` currently rejects any saved mode but Iron and Bronze, texts for KEY-6, generic visual kit.
4. **WP-E5-11 promotion and whole-match:** promote all 50 rows together (V22), refit the E5 era base toward 25 minutes with `e5_research_oracle.py`, a Fief full-content fingerprint, `fief-match` harness command, gates B5-xx in doc 27 §10.
5. **WP-E5-12:** large-sample balance, held-out cohort, human playtest (the user wants the whole age working before any playtest).

## Open questions for the user
Confirm the "no countdown" reading; whether to push or merge the branch into main (it contains an Iron behavior change, the timber and stone fix, and the frozen B4-06 block was not rerun).
