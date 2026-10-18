# CivSim: working rules for Claude

Read [docs/MVP/README.md](docs/MVP/README.md) for the design. The architecture decision is [docs/MVP/15-architecture-decision.md](docs/MVP/15-architecture-decision.md); the build plan and acceptance tests are [docs/MVP/17-vertical-slice.md](docs/MVP/17-vertical-slice.md).

## Commands

```bash
dotnet build CivSim.slnx -c Release
dotnet test CivSim.slnx -c Release                      # must stay green
dotnet run --project src/CivSim.Harness -c Release -- toy --check-golden tests/golden/toy-v1.txt
```

On this Mac the SDK comes from Homebrew; if `dotnet` cannot find its runtime set
`export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec`.

## Architecture rules (do not break)

1. **`CivSim.Sim` has no engine or UI references** and no project or package references at all. The Godot project (`game/`, later) only draws and sends `Command`s. It never writes simulation state.
2. **Determinism rules D1 to D10** (15 §6) bind everything in `CivSim.Sim`. The test `SourceRulesTests` scans the source and fails the build on violations:
   - No `float` or `double` (use `Fix64`, Q32.32), no `Math.Sqrt`/`Pow`/`Sin`/etc.
   - No `System.Random` (use `Pcg32.ForStream(seed, system, entity, tick)`).
   - No `GetHashCode`, no `Dictionary`/`HashSet` (use ids, arrays, ascending-id iteration).
   - No clocks, threads, files or console.
   A line may opt out only with a `// determinism-allow: reason` comment, and the reason must be real.
3. **Integer overflow throws** (project setting). Hashing code uses `unchecked` explicitly.
4. **Never change the semantics of `Fix64`, `Pcg32`, `Hash64`, the replay format or command numbering casually.** Replays and the golden checksum depend on them. Append to `CommandKind`; never renumber.
5. **Cross-platform determinism is a CI check** (macOS arm64 and Windows x64 must reproduce `tests/golden/toy-v1.txt`). If the golden changes, explain why in the commit message.

## Workflow

- **Sim first, headless, with tests.** A system is done when its exit gate in 17 §3 passes, not when it runs.
- **Replays are bug reports.** Capture odd behavior as a replay and add it to the regression corpus.
- **Balance numbers come from the docs** (`docs/MVP/07*`, `09`, `10`, `11`, `13`); tuning changes update the constants and the document that justifies them.
- **No game code beyond the current milestone.** Check 17 §3 for what M-next is.
- Python models in `docs/MVP/balance/tools` are the oracle for the sim's aggregate behavior. Run them with `uv run --no-project --with numpy python ...`.
