# CityBuilder.Api

**Path:** `server/src/CityBuilder.Api/` · **Kind:** .NET project (ASP.NET Core minimal API host, `net10.0`) ·
**References:** `CityBuilder.Application`, `CityBuilder.Infrastructure` · **Referenced by:** `CityBuilder.IntegrationTests`

## Purpose
The HTTP host and the composition root of the server (`core-instructions/03-architecture.md`): hosting, logging,
service registration and endpoint mapping. It is the only project that wires Application to Infrastructure.
Endpoints stay thin: bind the request, call one Application use case, map the result (`.claude/rules/server.md`).
It holds no game rules and no persistence code.

## Key types and files
| Type / file | Responsibility |
|---|---|
| `Program.cs` | Builds the host: JSON console logging, ProblemDetails, OpenAPI, health checks, `TimeProvider.System`; maps `/health` and, in Development only, `/openapi/v1.json` |
| `Properties/launchSettings.json` | `dotnet run` profile `http`: http://localhost:8080, `ASPNETCORE_ENVIRONMENT=Development` |
| `appsettings.json` | Log levels and allowed hosts; no custom configuration keys yet |

## Endpoints
| Method | Path | Environments | Response |
|---|---|---|---|
| GET | `/health` | all | `200`, `text/plain` body `Healthy`, no-cache headers. Liveness only: checks no dependencies |
| GET | `/openapi/v1.json` | Development | The OpenAPI 3.1 document of the mapped API endpoints |

The contract is `core-instructions/05-api-contract.md`. The game endpoints (`/api/v1/…`) arrive from M0-25 on.

## Rules and invariants
- `TimeProvider` is registered once, as `TimeProvider.System`. Game code gets time only through it
  (`core-instructions/02-principles.md`, rule 3); tests replace it with `FakeTimeProvider`.
- OpenAPI is mapped only in Development (`05`); a test checks that Production answers 404 (`07`).
- Logs are JSON lines on stdout: UTC timestamp, level, category, message, the template's named values (`State`)
  and scopes. What may and may not be logged: `core-instructions/07-security-model.md` → Logging policy.
- Warnings are errors (`server/Directory.Build.props`, `src/` projects).

## How to extend
- New command endpoint: recipe "New command" in `core-instructions/19-change-recipes.md`
  (endpoint in `Endpoints/`, DTOs in `Contracts/`, use case in Application).
- New configuration key: recipe "New configuration key" (options class, validated on start).

## Tests
`server/tests/CityBuilder.IntegrationTests` starts the app in memory with `WebApplicationFactory<Program>`; no Docker needed.
They cover the health endpoint, OpenAPI only in Development, and the registered clock. The project also holds
`InfrastructureDependencyTests`, a reflection check on the Infrastructure assembly that does not start the app.
Run them with the fast suite: `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`.

## Known limitations
- Not every error is a ProblemDetails response yet: an unknown route returns an empty 404, and outside Development an
  unhandled exception returns an empty 500. The error catalogue and the global handler come in M0-34.
- `/health` is not in the OpenAPI document: health-check endpoints carry no API description.
- No authentication, rate limits or request correlation yet *(planned, M0-25, M0-34, M0-35)*.

Last updated: M0-03
