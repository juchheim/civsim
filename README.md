# CivSim

A mostly hands-off civilization simulation from the Paleolithic to the post-modern era. You choose what your people learn (three research projects at a time), and hundreds of small autonomous figures live out the consequences on a shared world map against smart AI rivals.

- **Design documents:** [docs/MVP/README.md](docs/MVP/README.md) (game, research system, 509-project catalog, simulation, AI, architecture, first-playable plan).
- **Status:** milestone **M0 (foundations)** code done, cross-platform CI proof pending; see [docs/MVP/17-vertical-slice.md](docs/MVP/17-vertical-slice.md) for M1 onward.

## Build and test

Requires the .NET SDK (10.x; `brew install dotnet` on macOS). Godot is not needed until milestone M7a.

```bash
dotnet build CivSim.slnx -c Release
dotnet test CivSim.slnx -c Release
dotnet run --project src/CivSim.Harness -c Release -- toy --check-golden tests/golden/toy-v1.txt
```

## Layout

| Path | What |
|------|------|
| `src/CivSim.Sim` | Deterministic simulation library (`net8.0`, no engine references): `Fix64`, RNG streams, tick loop, checksums, replays, the toy canary model |
| `src/CivSim.Harness` | Headless runner (determinism golden check, replay record and verify; later AI tournaments) |
| `src/CivSim.Tests` | Unit, determinism, golden and source-rule tests |
| `tests/golden` | Golden results checked on macOS and Windows in CI |
| `docs/MVP` | Design documents and the Python balance models |
| `content/`, `src/CivSim.Content`, `src/CivSim.Cli`, `game/` | Planned (M1 content pipeline, M7 Godot front end) |

The architecture and its rules are in [docs/MVP/15-architecture-decision.md](docs/MVP/15-architecture-decision.md).
