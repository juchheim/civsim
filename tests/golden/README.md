# Golden files

`toy-v1.txt` is the expected result of the toy determinism canary (`CivSim.Sim.Toy`) for the fixed scenario
in `ToyScenario` (seed, ticks, command script). It is checked by:

- the unit test `Golden_checksum_is_unchanged`, and
- the CI step `civsim-harness toy --check-golden tests/golden/toy-v1.txt`, which runs on **macOS (arm64)
  and Windows (x64)**. Both must reproduce the same file. That is the cross-platform determinism proof
  (docs/MVP/15 rule D9).

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
