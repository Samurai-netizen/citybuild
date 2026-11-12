# Reference

- `generated/`: produced by `tools/DocTools` from the code and content, never edited by hand, and checked in CI: a committed page that
  differs from what the code generates fails the build ("Generated docs are current"; `docs/modules/DocTools.md`).

| Page | What it shows | From |
|---|---|---|
| [content.md](generated/content.md) | Rules, eras, items, buildings with levels, workers, recipes and per-hour rates at full staffing, roads, amount presets, research tree | `server/content/*.json` |
| [endpoints.md](generated/endpoints.md) | Every operation with auth, request body and success response; every request and response schema | the OpenAPI document the API's build writes |
| [errors.md](generated/errors.md) | Every error code with HTTP status, title, meaning and type | `ErrorCatalogue` |
| [configuration.md](generated/configuration.md) | Every configuration key with type, default, committed values, meaning and allowed range | the API's options classes and `appsettings*.json` |
| [db-schema.md](generated/db-schema.md) | Tables, columns, keys, foreign keys, indexes, check constraints, migrations | the EF Core model |
| [project-map.md](generated/project-map.md) | .NET projects and Unity assemblies with their references (Mermaid) | `.csproj` and `.asmdef` files |

- The normative specs stay in `core-instructions/`: content `23`, API and errors `05`, schema `06`. The generated pages show the system as built.
