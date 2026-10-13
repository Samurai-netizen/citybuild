# Module Documentation

One file per .NET project and per Unity assembly (`<Name>.md`, from `TEMPLATE.md`): purpose, key types, invariants,
how to extend, tests. `check-docs.sh` fails if a project or assembly has no module doc, or a doc has no `Last updated:` line.
Test projects are covered by `docs/testing/` instead.

Modules are added by the tasks that create them (M0-02, M0-03, M0-04, M0-61, M0-67).

## Index
| Module | Kind | Doc |
|---|---|---|
| `CityBuilder.Api` | .NET project, `server/src/` (ASP.NET Core host, composition root) | [CityBuilder.Api.md](CityBuilder.Api.md) |
| `CityBuilder.Application` | .NET project, `server/src/` (use cases, ports; skeleton) | [CityBuilder.Application.md](CityBuilder.Application.md) |
| `CityBuilder.Domain` | .NET project, `server/src/` (server-only concepts; skeleton) | [CityBuilder.Domain.md](CityBuilder.Domain.md) |
| `CityBuilder.Infrastructure` | .NET project, `server/src/` (port implementations; skeleton) | [CityBuilder.Infrastructure.md](CityBuilder.Infrastructure.md) |
| `CityBuilder.Simulation` | Unity package `shared/Simulation/` + .NET project `shared/Simulation.Build/` (the deterministic engine; integer helpers and `ItemId` so far) | [CityBuilder.Simulation.md](CityBuilder.Simulation.md) |
| `CityBuilder.Client.Core` | Unity assembly, `client/CityBuilder/Assets/_Project/Core/` (client state and display prediction; skeleton) | [CityBuilder.Client.Core.md](CityBuilder.Client.Core.md) |
| `CityBuilder.Client.Net` | Unity assembly, `client/CityBuilder/Assets/_Project/Net/` (API client and DTOs; skeleton) | [CityBuilder.Client.Net.md](CityBuilder.Client.Net.md) |
| `CityBuilder.Client.Presentation` | Unity assembly, `client/CityBuilder/Assets/_Project/Presentation/` (composition root, UI Toolkit screens, input) | [CityBuilder.Client.Presentation.md](CityBuilder.Client.Presentation.md) |
