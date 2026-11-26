# 40. Legends (notable people who matter, in every era)

Status: **design for approval, 2026-11-26. Nothing here is built.** Decided by the owner on 2026-11-26: Legends have **game effects**, and they exist in **every era** (from the first keystone on). Every number is provisional (P) and is fitted in the phases of §9 before it is typed.

## 1. The short version (what to accept)

- A **Legend** is a real person in the simulation who did something that mattered: founded a settlement, held a town through a famine, won a battle, gave a feast, was the first of the realm to finish a project, or simply lived a very long life. The sim keeps a deed score for every figure; when the score is high enough, a roll (doubled by ART-10 patronage) makes them a Legend.
- A living Legend gives **small, bounded, local effects**: a little more Cohesion in their settlement, a little faster skills there, and, for a warrior, better training and a steadier line in a fight. The effects are the ones already promised in docs 05 §5.5 and 13 §7.2 and §7.3 and in the catalog rows SOC-05, ART-06, MET-06 and ART-10.
- A Legend's death hurts (a mourning dip in Cohesion), and **ART-06's carved relief and MET-06's bronze statue keep the bonus** after death: that is what those projects have always said they do.
- Legends are **named in the Chronicle** (a generated name, the deed, the season) and are shown to the player. Nothing in the first version changes any other system.
- Legends begin at the first keystone (KEY-1, "Legends begin to appear" is already its rule text). Before it a figure collects deed points but cannot become a Legend.
- **Everything is bounded:** at most 3 living Legends a settlement, their Cohesion lift is capped at +4 in a settlement, and every effect goes through an existing term or modifier, so no new global multiplier appears.
- **Every golden changes**, because a Legend can appear from the first keystone in any match that reaches it. The change is explained once, in the commit of the phase that turns the effects on (§8).

The word "Legend" is also the AI's top difficulty level (`Difficulty.Legend`). The two are unrelated; in code a Legend person is `Legend` in a `LegendLog`, and the difficulty stays `Difficulty.Legend`.

## 2. What the project already promises

| Source | What it says |
|---|---|
| [05 §5.5](05-simulation-structure.md) | The sim scores notable deeds (innovator, founder, surviving a famine as leader, winning a battle, teaching many, long life). High scorers become Legends: named in the Chronicle, a distinct look, small local bonuses (+Cohesion, +skill spread) while alive. ART-06 makes the bonuses persist after death. |
| [05 §5.2](05-simulation-structure.md) | When figures merge at a larger scale the representative keeps the most notable identity, so Legends survive. |
| [13 §7.2, §7.3](13-contact-and-conflict.md) | A Legend warrior trains at x1.5 (the ordinary warrior's x1.3 is replaced, not stacked). A group with a Legend breaks 10 points later. |
| KEY-1 | Rule: "Legends begin to appear." |
| SOC-05 | Rule: "feast-givers become Legends." |
| ART-06 | One Legend per settlement keeps its bonus after death (a carved relief). |
| MET-06 | A bronze statue commemorates a second Legend per City, who keeps the bonus after death. |
| ART-10 | `legend_chance` x2, only while a court or a Wonder patron stands. The factor already exists in the sim as `LivingEconomyModel.LegendChanceFactor`; nothing reads it yet. |
| [17 §8](17-vertical-slice.md) | "Legends beyond naming in the Chronicle" was cut from the slice: this document is the work that cut deferred. |

## 3. Deeds and score

The sim already produces most of these events (project completion, fission and founding, raid and assault resolution, the feast action, structure completion, food security, age). Two it does not yet measure directly, **teacher** (how many people adopted a method an elder taught) and **held through famine** (the recovery test), are measured from the existing adoption and food-security numbers in phase L1, and either is dropped if the number is not there. A deed adds points to the figure who did it. Points are provisional.

| Deed | Points | Who gets it | Where the sim sees it |
|---|---|---|---|
| **First of the realm** (a project finished that no one of the civilization had finished: the research system's Firsts) | 40 | the most skilled adult of the settlement where the work happened | project completion |
| **Founder** (a daughter settlement or a colony founded) | 60 | the adult who led the group (the oldest adult leaving) | fission and founding |
| **Held through famine** (the settlement's food security stayed under 0.5 for two or more seasons and then recovered with no loss over 20% of its people) | 50 | the elder or chief of the settlement | seasonal food security |
| **Won a battle** (a raid or assault resolved as a win, the figure was among the fighters and survived) | 30 each, 50 for the best fighter | surviving fighters | raid and assault resolution |
| **Feast** (SOC-05 feast given) | 25 | the giver (the figure with the highest Belonging surplus) | feast action |
| **Teacher** (an elder who taught a method that was then adopted by 10 or more people) | 30 | the elder | adoption |
| **Wonder** (a Wonder completed) | 60 | the settlement's best builder | structure completion |
| **Long life** | +1 a year past 60 | everyone | age |

A figure becomes a Legend when its score reaches **100** at a season end, subject to one roll: probability **35%** (P) times `legend_chance` (ART-10: x2, so 70%), drawn from `Pcg32.ForStream(seed, "legend", figureId, season)`. A failed roll keeps the score, so a person with 100 points rolls again each season until they succeed or die. Scores are stored per figure and cost one small integer.

Rate target (fitted in phase L1): **about two to six Legends born per civilization per match hour**, so the Chronicle fills without a crowd.

## 4. Effects of a living Legend (all bounded)

| Effect | Rule | Reads through |
|---|---|---|
| **Cohesion** | +2 points in the Legend's settlement for each living Legend, at most +4 in a settlement (a third Legend is celebrated, not paid) | the settlement's Cohesion target, as one more term |
| **Skill spread** | adults in the Legend's settlement gain skill practice 10% faster (no bonus to the Legend's own skill: they were already good) | the practice step |
| **Warrior** | a Legend who fights is a warrior at x1.5 training (replacing the warrior's x1.3; the professional's x1.5 is unchanged) | `Fighters()` training |
| **Steady line** | a fighting group with a Legend breaks 10 points later (a 40% defender break becomes 50%) | the battle setup |
| **Patronage** | ART-10: chance x2 while a court or a Wonder stands | `LegendChanceFactor` |

No effect touches food, population, research cost, trade or any global stat. The largest single swing is the Cohesion +4, roughly the size of one shrine.

## 5. Death and memory

- A Legend's death costs their settlement **−3 Cohesion points once** (mourning), unless the settlement keeps their memory.
- **ART-06's carved relief** keeps one Legend's Cohesion bonus per settlement after death; **MET-06's bronze statue** keeps a second per City. The kept bonus is the +2 only (no skill spread, no warrior effects), and it is exactly the bounded term of §4, so the settlement cap still holds.
- Dead Legends stay in the Chronicle forever.

## 6. Naming and the Chronicle

- A name is generated from a short syllable table by `Pcg32.ForStream(seed, "legend-name", figureId, 0)`, with a civilization suffix ("Ama of the River Folk").
- A `LegendRecord` holds: figure id, name, civilization, settlement, born-as-Legend season, the deed (an enum and a detail integer), death season (or open), and whether a relief or statue keeps the bonus.
- The frame carries the viewer's civilization's living Legends and the newest records; the HUD's Chronicle shows "Ama of the River Folk is told of in the valley: held Fenwick through the winter." The renderer's "distinct look" (a small banner or glow on the figure) is a later art slice and reads the same list.
- Other civilizations' Legends appear in the Chronicle only when the viewer knows the civilization (the same visibility rule as other events).

## 7. How it is built (determinism, saves, replays)

- **No clocks, floats, hash sets or `System.Random`** (D1 to D10). Scores are integers; chances are `Fix64`; every roll names its stream; iteration is by ascending figure id.
- **State:** a per-figure score and Legend flag (a small `LegendLog` section: score map as sorted arrays, the record list), saved in a new snapshot tail written only once a figure holds a non-zero score. Older saves load with an empty log. The log is hashed only when it is non-empty, as the other late sections are.
- **Figures that merge or die:** the log is keyed by figure id; a merged representative inherits the larger score and the flag (05 §5.2); a death marks the record and applies §5.
- **Replays:** Legends are produced by the simulation, never by a command, so the command format and `CommandKind` are untouched. The Chronicle is derived state.
- **Fixtures** (`FixtureGiveDeed`, `FixtureForceLegend`) let tests place a Legend directly, as the other fixtures do.

## 8. Balance risk and what changes

- **Goldens:** KEY-1 arrives in the first minutes of any match, so Legends can exist in every golden run. The toy golden (no research) is unaffected; `research-v1`, `politics-v1` and `bronze-v1` change when the effects switch on in phase L2. Phase L1 (scoring and the Chronicle, no effects) must leave all of them identical, which is the test that the score code touches nothing.
- **Archetypes:** the Warlord gains most from a Legend warrior and the Sage from teacher deeds. The tournament tables (B31: Legend at least 45% against Warlord, and the archetype shares in docs 07d and 27) are re-run after L2 and the deed points re-tuned if one archetype moves by more than 2 points.
- **Era pacing:** Cohesion and skill effects are small; the research and pacing sweeps (the E5 Fief holdout and the E6 sweep) are re-run once after L2 to confirm no era moves by more than a minute.
- **Cap:** at most 3 living Legends a settlement and 12 a civilization keeps the effect sum bounded however long the match runs.

## 9. Phases (each ends in the gate: tests, goldens, strict content validation)

| Phase | Work | Goldens |
|---|---|---|
| **L1 Scores and Chronicle** | deed hooks, scoring, the roll, `LegendLog`, snapshot tail, names, Chronicle records in the frame, fixtures, tests. **No effects.** | all unchanged (the proof that scoring is read-only) |
| **L2 Effects** | §4: Cohesion term, skill spread, Legend warrior, steady line, mourning; ART-10 `LegendChanceFactor` read; tests | research, politics and bronze goldens refreshed and explained |
| **L3 Memory** | §5: ART-06 relief and MET-06 statue keep a bonus; death handling; tests | none beyond L2 |
| **L4 Balance** | rate and archetype sweeps, pacing re-run, point tuning, docs | re-explain if constants move |
| **L5 Display** | HUD Chronicle entries, the distinct look in the renderer (a banner), the Legend list in the people sheet | none (view only) |

## 10. Open numbers, with my recommendation

| Question | Recommendation |
|---|---|
| Score threshold and base chance (100, 35%) | start there; fit to two to six Legends per civilization per hour in L1 |
| Cohesion +2 each, cap +4 | keep; it is the largest effect and is inside the existing term's range |
| Mourning −3 once | keep; it makes ART-06 and MET-06 worth a project |
| Do Legends of a civilization you do not know show in the Chronicle? | no, only known civilizations |
| Can a Legend be killed on purpose (a raid that targets one)? | no targeting in this version; a fallen fighter is a normal death with the mourning |
