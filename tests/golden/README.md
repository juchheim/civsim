# Golden files

`toy-v1.txt` is the expected result of the toy determinism canary (`CivSim.Sim.Toy`) for the fixed scenario
in `ToyScenario` (seed, ticks, command script). It is checked by:

- the unit test `Golden_checksum_is_unchanged`, and
- the CI step `civsim-harness toy --check-golden tests/golden/toy-v1.txt`, which runs on **macOS (arm64)
  and Windows (x64)**. Both must reproduce the same file. That is the cross-platform determinism proof
  (docs/MVP/15 rule D9).

`worldgen-v1.txt` records the standard world fingerprint for seed 42. The test
`Seed_42_matches_world_generation_fingerprint_golden` checks it through the `dotnet test` matrix on macOS and
Windows.

`research-v1.txt` records seed, ticks, command count, final checksum and digest of a 12-minute match
(`civsim-harness research --minutes 12 --bot grower`): the living economy driven by research, civilization 0 playing the
Grower opening. It is checked by the CI step `research --check-golden` on **macOS and Windows**, which is the
cross-platform proof for M4. It changes whenever the economy, the research rules, the Council or the content change;
regenerate it deliberately and say why in the commit message:

```bash
dotnet run --project src/CivSim.Harness -c Release -- research --pack civsim.pack --minutes 12 --bot grower --write-golden tests/golden/research-v1.txt
```

`bronze-v1.txt` is the fixed-seed, chapter-end replay canary for B22. The explicit `--check-bronze-golden` and
`--write-bronze-golden` options each run seed 20261019 twice independently from scratch through `ResearchM10`'s
chapter path, stopping on the exact tick the chapter end rule fires, then compare/store the chapter ticks, end reason
and checksum at that point. Other research options cannot be combined with either Bronze golden option. This is a single-seed infrastructure check; it does
not complete B22's 1,000-replay corpus or its macOS/Windows comparison.

`politics-v1.txt` records seed, ticks, final checksum and digest of a 24-minute match of four Balanced civilizations on
the Council (`civsim-harness politics --minutes 24`): territory, first contact, gifts and barter on top of the economy and
research. It is checked in CI on **macOS and Windows**. It changes with any change to the economy, research, politics or
the content; regenerate it deliberately and say why in the commit message:

```bash
dotnet run --project src/CivSim.Harness -c Release -- politics --pack civsim.pack --minutes 24 --write-golden tests/golden/politics-v1.txt
```

`content-pack.txt` is the format, project count, size and SHA-256 of the compiled content pack built from
`content/`. It is checked by the unit test `The_pack_matches_the_committed_golden_summary` and by the CI step
`civsim build-pack --check-golden`, on macOS and Windows. **Unlike the toy golden, this one changes whenever
content changes, on purpose**: the hash is the content's identity (it goes into every save and replay). After a
deliberate content edit, regenerate it and commit it with the edit:

```bash
dotnet run --project src/CivSim.Cli -c Release -- build-pack --out civsim.pack --write-golden tests/golden/content-pack.txt
```

If `toy-v1.txt` changes, the engine-level plumbing changed: Fix64 semantics, RNG streams, command ordering,
checksums or the toy model itself. That is almost never intended. If it is, regenerate it deliberately and
say why in the commit message:

```bash
dotnet run --project src/CivSim.Harness -c Release -- toy --write-golden tests/golden/toy-v1.txt
```
