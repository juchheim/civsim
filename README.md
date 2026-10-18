# CivSim

A mostly hands-off civilization simulation from the Paleolithic to the post-modern era. You choose what your people learn (three research projects at a time), and hundreds of small autonomous figures live out the consequences on a shared world map against smart AI rivals.

- **Design documents:** [docs/MVP/README.md](docs/MVP/README.md) (game, research system, 509-project catalog, simulation, AI, architecture, first-playable plan).
- **Status:** milestones **M0 (foundations)**, **M1 (content pipeline)**, and **M2 (world generation)** done; **M3 (living economy)** is next. See [docs/MVP/17-vertical-slice.md](docs/MVP/17-vertical-slice.md) for the full roadmap.

## Build and test

Requires the .NET SDK (10.x; `brew install dotnet` on macOS). Godot is not needed until milestone M7a.

```bash
dotnet build CivSim.slnx -c Release
dotnet test CivSim.slnx -c Release
dotnet run --project src/CivSim.Harness -c Release -- toy --check-golden tests/golden/toy-v1.txt
dotnet run --project src/CivSim.Cli -c Release -- validate --strict
```

## Layout

| Path | What |
|------|------|
| `src/CivSim.Sim` | Deterministic simulation library (`net8.0`, no engine references): `Fix64`, RNG streams, tick loop, checksums, replays, the toy canary model |
| `src/CivSim.Content` | YAML loader, schema and rule validator (checks V1 to V18), content pack compiler (tooling, uses YamlDotNet) |
| `src/CivSim.Cli` | `civsim` command: validate, build-pack, stats, paths, gates |
| `content/` | The game's data as YAML: catalog (509 projects + 10 keystones), registries, constants |
| `src/CivSim.Harness` | Headless runner (determinism golden check, replay record and verify; later AI tournaments) |
| `src/CivSim.Tests` | Unit, determinism, golden and source-rule tests |
| `tests/golden` | Golden results checked on macOS and Windows in CI |
| `docs/MVP` | Design documents and the Python balance models |
| `game/` | Planned (M7 Godot front end) |

The architecture and its rules are in [docs/MVP/15-architecture-decision.md](docs/MVP/15-architecture-decision.md).
