# Golden files

`toy-v1.txt` is the expected result of the toy determinism canary (`CivSim.Sim.Toy`) for the fixed scenario
in `ToyScenario` (seed, ticks, command script). It is checked by:

- the unit test `Golden_checksum_is_unchanged`, and
- the CI step `civsim-harness toy --check-golden tests/golden/toy-v1.txt`, which runs on **macOS (arm64)
  and Windows (x64)**. Both must reproduce the same file. That is the cross-platform determinism proof
  (docs/MVP/15 rule D9).

If the file changes, the engine-level plumbing changed: Fix64 semantics, RNG streams, command ordering,
checksums or the toy model itself. That is almost never intended. If it is, regenerate it deliberately and
say why in the commit message:

```bash
dotnet run --project src/CivSim.Harness -c Release -- toy --write-golden tests/golden/toy-v1.txt
```
