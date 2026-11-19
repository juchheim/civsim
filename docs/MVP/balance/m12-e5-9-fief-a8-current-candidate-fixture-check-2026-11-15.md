# Fief A8 current-candidate fixture check — 2026-11-15

This is current-candidate fixture evidence only. It does not close A8's whole-match unseen-state invariance requirement.

## Result

The Release, no-build `FiefFogFairnessTests` filter passed **2/2 tests** (0 failed, 0 skipped; 4 seconds). The captured runner output is [here](m12-e5-9-fief-a8-current-candidate-fixture-output-2026-11-15.txt), SHA-256 `1a58299065967e7e64a2ac4553767e32352853527d6196461987efed609d6303`.

## Candidate identity

- `content/src/game` workspace aggregate: `e42e3605be2de8bd73a4cc049b0219cf92ed0a43994cf11064486958b2cb7144` (1040 files; candidate provenance inventory [here](m12-e5-9-f-t7-candidate-source-provenance-2026-11-15.json)).
- `content+src` aggregate: `fc3a3606b5092b08c73ec4f2ca20d449c8d122ecbd2750be27c7c6fa299c982a` (446 files).
- Release `CivSim.Tests.dll`: `a4d3c0b7fbc47a646ae3043ea2cc4b03c1a4aac4c82597b9093bc7cb146bfeda`.
- Adjacent Release `CivSim.Sim.dll`: `3fa5f4d96f78d6427738523094ce24035bdb471e858e21aea5eaf8f66953f37b`.
- Frozen numeric-status pack: `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`.
- Release harness: `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f`.

The workspace aggregate was recomputed after the fixture capture and remains equal to the candidate freeze. This result establishes focused fixture behavior on that candidate; broad whole-match hidden-state invariance, visible-input positive controls, and the separate Legend/duel gate remain open.
