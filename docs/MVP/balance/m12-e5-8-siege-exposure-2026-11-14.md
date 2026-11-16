# WP-E5-8 F-T5 whole-match siege exposure screen (2026-11-14)

**Result:** siege mechanics are exercised in a deterministic Fief fixture, but the natural sample has no persistent investments and cannot establish whole-match F-T5 balance. No acceptance threshold, catalog row, or golden changed.

F-T5 requires gunpowder walls to use the ×0.3 wall barrier only with a cannon and paid powder breach; garrisoned concentric castles use the fitted ×1.5 over the existing ×1.25 strongpoint; engine/investment siege remains viable; and DEF-09 raises assigned-adult per-turn attrition from 1% to 1.25%. B5-08 also requires no assault against standing stone walls before a breach and conserved powder consumption. The required evidence is isolated supplied trials **plus** natural whole-match siege exposure and relief. This screen is a mechanic diagnostic, not acceptance of F-T5 or WP-E5-8.

## Natural whole-match exposure

Ran `fief-match --seeds 4 --minutes 130 --grant-e5` for seeds `20261019–20261022` with the built draft content pack. Full outputs are preserved in [the natural-screen trace](m12-e5-8-natural-siege-screen-2026-11-14.txt).

| Seed | Wars | Assaults | Captures | Persistent investment starts | F-T5 siege status |
|---:|---:|---:|---:|---:|---|
| 20261019 | 0 | 0 | 0 | 0 | No war/assault exposure |
| 20261020 | 2 | 6 | 2 | 0 | Assaults/captures occurred, no siege investment |
| 20261021 | 2 | 2 | 2 | 0 | Assaults/captures occurred, no siege investment |
| 20261022 | 0 | 0 | 0 | 0 | No war/assault exposure |
| **Total** | **4** | **8** | **4** | **0** | **4/4 worlds unexposed to a persistent siege** |

Thus natural engine-only and cannon-backed breaches, powder use, crew losses, DEF-09 storm outcomes, and relief were all unexposed/censored in this four-world sample. The summary harness does not currently record these fields per natural investment; the raw match trace has no siege investment details to recover.

## Controlled matched mechanic fixture

`FiefSiegeBalanceExposureTests` creates the same seed/world, nearby attacker, settlement pair, force, and standing stone walls for the engine and cannon arms. It directly starts a supplied assault and records the first siege turn; it explicitly grants prerequisites, weapon stocks and powder, so these are not natural acquisition or balance observations.

| Arm | Observed result |
|---|---|
| Cannon, no engines, no DEF-09 | One investment started; two cannons deployed with eight assigned crew; two paid shots consumed 0.50 L from 1.00 L powder; wall integrity changed 1.00→0.00 and the powder breach flag was set; the storm had 9 defenders down, 0 attackers down, and captured the settlement. The crew-attrition observer recorded 0 crew deaths in this one siege turn. |
| Engine-only, garrisoned DEF-09 | One investment started; two engines with eight assigned crew; no cannon shots and no powder change; wall integrity changed 1.00→0.00 with powder breach flag unset; storm resolved with 9 defenders down, 0 attackers down, and captured the settlement. The intact standing-wall defense multiplier was 6.1875 (base 1.1 × wall 3 × castle 1.25 × concentric 1.5); after engine breach the wall barrier is removed and the garrisoned castle strongpoint remains. Crew-attrition observer recorded 0 deaths in the one turn. |
| Defender relief | A prepared cannon siege was ended by a defender-side relief force before firing. Both unspent cannons returned, the full 1.00 L powder remained, and the relief event resolved; the diagnostic observer counted 5 relief-side and 63 besieger casualty notifications (dead or wounded combined). |

The cannon fixture also checks zero cannon stock at the home settlement after deployment. Separate unit tests assert that the powder-breach multiplier reduces only the wall barrier and that a standing wall remains unassaultable without breach. These assertions and the controlled outcomes support wiring/conservation regressions, not the frequency, accessibility, or relative value of siege choices in ordinary matches.

## Crew attrition and isolation

The existing 100,000 keyed assigned-person-turn Monte Carlo test observed 1,006/100,000 losses at 1.00% (95% Wilson interval 0.945–1.071%) and 1,244/100,000 at 1.25% under DEF-09 (1.176–1.315%). The live fixture saw no crew death in its single turn per arm; this is too little exposure to validate those probabilities. The added Bronze/Iron regression checks show cannon draw/payment operations return zero and leave the match checksum unchanged. Existing powder sourcing tests also keep saltpeter/sulfur/powder unavailable in Bronze and Iron.

Focused validation:

```sh
dotnet test src/CivSim.Tests/CivSim.Tests.csproj --no-restore --filter 'FullyQualifiedName~FiefSiegeBalanceExposureTests.Engine_only|FullyQualifiedName~FiefSiegeBalanceExposureTests.Relief_|FullyQualifiedName~FiefSiegeBalanceExposureTests.Non_Fief'
dotnet test src/CivSim.Tests/CivSim.Tests.csproj --no-restore --filter 'FullyQualifiedName~FiefSiegeBalanceExposureTests.Powder_backed'
dotnet test src/CivSim.Tests/CivSim.Tests.csproj --no-restore --filter 'FullyQualifiedName~FiefSiegeWeaponTests'
```

The complete controlled `FiefSiegeBalanceExposureTests` class passed 6/6, including an active Fief-investment snapshot round trip for cannon inventory and breach state; the existing powder/weapon suite passed 6/6. No source implementation change was needed for this slice.

**Still open:** whole-match F-T5 siege exposure and strategy balance; a larger natural cohort with siege events; natural acquisition and sustained availability of engines/cannon/powder; repeated attrition exposure under ordinary siege duration; and capture/relief rates under balanced defender and relief conditions. No row is promoted.
