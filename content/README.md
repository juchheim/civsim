# CivSim content

The game's data, as YAML: the project catalog, the registries and the tuned constants. **This is the source of
truth** (docs/MVP/16). The markdown catalog in `docs/MVP/03*.md` was imported into this folder once and is now
only a design record.

```
content/
├─ catalog/        one file per lineage (33) plus keystones.yaml: 509 projects + 10 keystones
├─ registries/     eras, actions, practice, goods, structures, tools, roles, biomes, fauna, deposits,
│                  archetypes, difficulty, guarantees, openings, recipes
└─ constants/      balance.yaml (tuned constants and the project type multipliers)
```

## Workflow

```bash
dotnet run --project src/CivSim.Cli -c Release -- validate --strict     # all checks; must be clean
dotnet run --project src/CivSim.Cli -c Release -- stats | paths | gates # reports
dotnet run --project src/CivSim.Cli -c Release -- build-pack --out civsim.pack
# after a deliberate content change, refresh the golden (see tests/golden/README.md):
dotnet run --project src/CivSim.Cli -c Release -- build-pack --out civsim.pack --write-golden tests/golden/content-pack.txt
```

## Rules of the format

- **Strict YAML subset:** one document per file, no anchors, aliases or merge keys, no duplicate keys. Plain
  scalars are integers, decimals (exact, stored as Q32.32), `true`, `false` or `null`; anything else is text.
  Quote text that could be mistaken for a number. Every file starts with `schema: 1`.
- **A project** has `id`, `name`, `era`, `type`, `status`, `requires`, optional `requires_any` (one-of),
  `gates`, `effects`, `practice`, `flavor`, `moment`. Status `draft` projects carry one `prose` effect; `numeric`
  projects carry typed effects (the kinds are defined in `src/CivSim.Content/EffectSpecs.cs`).
- **Ids never change.** Costs are derived (`EraBase × type multiplier`); an override needs `cost_reason`.
- **Accepting a warning:** a project may list `ack: [V11]` with an `ack_reason`; the warning is then suppressed
  for that project only.
- **Era of a keystone** is the era it *leaves*; `enters` is the era it enters (10 = the Apex).

The reference importer that produced these files is `docs/MVP/catalog-tools/import_md.py`. Do not re-run it over
edited content: it rewrites every file.
