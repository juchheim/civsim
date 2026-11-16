# Fief A8 fog-fairness fixture coverage — 2026-11-14

This is focused fixture coverage for Fief inputs at the fog boundary. It is not the A6 10,000-policy search and does not close whole-match A8 acceptance.

`FogFairnessHarness` can now create and snapshot-clone Fief matches. Its Fief unseen-state perturbation varies unexplored deposits, an out-of-sight rival settlement's cannon and powder stocks, a hidden rival harbor/galley and supplied crew, and live horse herds restricted to unexplored tiles. The comparison checks observer decisions and beliefs, including known deposit contents and horse-source knowledge, and exact own Fief siege stores and crewed-galley homes. Perturbed clones use a separate generated-world instance so the fixture does not mutate the baseline world through the shared seed cache.

The visible-state controls verify that a known Saltpeter deposit is observable, own cannon/powder stock and a crewed galley enter the own view, and a horse herd on an explored home tile yields `HorseSourceKnowledge.Seen`. No production Strategist policy code was changed.

Validation command:

```sh
dotnet test src/CivSim.Tests/CivSim.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~FiefFogFairnessTests|FullyQualifiedName~FiefHorseSourceKnowledgeTests|FullyQualifiedName~FogFairnessTests'
```

Result: Release build succeeded; 7 passed, 0 failed, 0 skipped (32 seconds). This includes the existing long Bronze unseen-state A8 test and the Fief horse-source knowledge/hash/snapshot tests, plus the new Fief unseen perturbation and visible-input controls. The fixture establishes bounded state coverage only; it does not substitute for the A6 policy search or broader Fief AI acceptance cohort.
