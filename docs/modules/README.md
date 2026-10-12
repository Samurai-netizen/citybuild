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
| `CityBuilder.Simulation` | Unity package `shared/Simulation/` + .NET project `shared/Simulation.Build/` (the deterministic engine; skeleton) | [CityBuilder.Simulation.md](CityBuilder.Simulation.md) |
