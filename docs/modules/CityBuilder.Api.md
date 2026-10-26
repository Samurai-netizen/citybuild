# CityBuilder.Api

**Path:** `server/src/CityBuilder.Api/` · **Kind:** .NET project (ASP.NET Core minimal API host, `net10.0`) ·
**References:** `CityBuilder.Application`, `CityBuilder.Infrastructure` · **Referenced by:** `CityBuilder.IntegrationTests`

## Purpose
The HTTP host and the composition root of the server (`core-instructions/03-architecture.md`): hosting, logging,
service registration and endpoint mapping. It is the only project that wires Application to Infrastructure.
Endpoints stay thin: bind the request, call one Application use case, map the result (`.claude/rules/server.md`).
It holds no game rules and no persistence code: it registers Infrastructure's EF Core context and, in Development only, applies the
migrations at startup.

## Key types and files
| Type / file | Responsibility |
|---|---|
| `Program.cs` | Builds the host: JSON console logging, ProblemDetails, OpenAPI, health checks, `TimeProvider.System`, the content (options, `IContentProvider`), the database (`DatabaseOptions`, `AddCityBuilderPersistence`); loads the content and checks the database settings before anything is mapped, and in Development applies the migrations; maps `/health` and, in Development only, `/openapi/v1.json` |
| `Content/ContentOptions` | Section `Content`: `FilePath` (required, not blank; validated on start), resolved against the app's base directory when relative |
| `Content/ContentLog` | The content log message (source-generated `[LoggerMessage]`): "Content {ContentVersion} loaded from {ContentFilePath}", event id 1000 |
| `Persistence/DatabaseOptions` | `ConnectionString` from `ConnectionStrings:CityBuilder` (required, not blank) and section `Database`: `ApplyMigrationsAtStartup` (only allowed in Development) and `LockTimeoutMilliseconds` (default 3000, 1 to 60 000); validated on start, and passed to `AddCityBuilderPersistence` as `PersistenceSettings` |
| `Persistence/DatabaseLog` | The database log message: "Database migrated at startup: {AppliedCount} migrations applied ({AppliedMigrations})", event id 2000 (ids 2000–2999 for database messages) |
| `Properties/launchSettings.json` | `dotnet run` profile `http`: http://localhost:8080, `ASPNETCORE_ENVIRONMENT=Development` |
| `appsettings.json` | Log levels, allowed hosts, `Content:FilePath` (`content/mvp-content.json`), `Database:ApplyMigrationsAtStartup` (`false`). No connection string: it holds a password |
| `appsettings.Development.json` | `Database:ApplyMigrationsAtStartup` = `true` |
| `CityBuilder.Api.csproj` | `UserSecretsId` `citybuilder-api` (where `dotnet user-secrets` keeps the connection string); `Microsoft.EntityFrameworkCore.Design` for dotnet-ef at design time only (`PrivateAssets=all`) |
| `server/content/*.json` (linked) | Copied by the build to `content/` in the output (and in a publish), next to the app |

## Endpoints
| Method | Path | Environments | Response |
|---|---|---|---|
| GET | `/health` | all | `200`, `text/plain` body `Healthy`, no-cache headers. Liveness only: checks no dependencies |
| GET | `/openapi/v1.json` | Development | The OpenAPI 3.1 document of the mapped API endpoints |

The contract is `core-instructions/05-api-contract.md`. The game endpoints (`/api/v1/…`) arrive from M0-25 on.

## Startup
The content is loaded once, right after the host is built and before any endpoint is mapped or a request accepted: `Program.cs` resolves
`IContentProvider`, whose factory runs `ContentFileLoader.Load` on `Content:FilePath`. A blank key (`OptionsValidationException`) or a file
that can't be loaded (`ContentLoadException`, every error listed) is unhandled there: the process stops with a non-zero exit code, and the
runtime prints the message to stderr. On success the log has one line with the content version and the full file path.

Then `Program.cs` reads `DatabaseOptions`: a missing or blank `ConnectionStrings:CityBuilder`, `Database:ApplyMigrationsAtStartup=true`
outside Development, or a `Database:LockTimeoutMilliseconds` outside 1 to 60 000, fails the validation and stops the start the same way. In Development with the flag on, `DatabaseMigrator.MigrateAsync`
applies the pending migrations before any request; the log names them (event id 2000), and an unreachable database stops the start with
an `NpgsqlException` (`docs/how-to/add-migration.md`). No other environment ever migrates at startup.

## Rules and invariants
- `TimeProvider` is registered once, as `TimeProvider.System`. Game code gets time only through it
  (`core-instructions/02-principles.md`, rule 3); tests replace it with `FakeTimeProvider`.
- OpenAPI is mapped only in Development (`05`); a test checks that Production answers 404 (`07`).
- The connection string never comes from a committed file: user-secrets in Development, the environment variable
  `ConnectionStrings__CityBuilder` elsewhere (`docs/getting-started.md` → Database).
- Migrations run at startup only in Development: the options validation rejects the flag elsewhere, and `Program.cs` checks the
  environment again before migrating.
- The OpenAPI package's source generator (`OpenApiXmlCommentSupport`) compiles this project's XML doc comments into the assembly:
  the comments of endpoint handlers and of request and response types become descriptions in the OpenAPI document, so they are
  written for API consumers too, and a doc comment change in Api changes the assembly (found in CR-0003-T1).
- Logs are JSON lines on stdout: UTC timestamp, level, category, message, the template's named values (`State`)
  and scopes. Every log call is a source-generated `[LoggerMessage]` method in a `<Feature>Log` class (the analyzers reject `LogInformation`
  calls in `src/`: CA1848, CA1873); decision 029 in `core-instructions/12-decisions.md`. What may and may not be logged: `core-instructions/07-security-model.md` → Logging policy.
- Every type and member, private ones included, has an XML doc comment (`core-instructions/14-coding-standards.md` → Documentation
  in code): a missing one on a public member fails the build (CS1591), and `CityBuilder.Repository.Tests` checks the rest.
- Warnings are errors (`server/Directory.Build.props`, `src/` projects).

## How to extend
- New command endpoint: recipe "New command" in `core-instructions/19-change-recipes.md`
  (endpoint in `Endpoints/`, DTOs in `Contracts/`, use case in Application).
- New configuration key: recipe "New configuration key" (options class, validated on start), like `ContentOptions`.
- New content file or another content path: `Content:FilePath` (environment variable `Content__FilePath`, or `--Content:FilePath=…`).

## Tests
`server/tests/CityBuilder.IntegrationTests` starts the app in memory with `ApiFactory`, a `WebApplicationFactory<Program>` with a placeholder
connection string and no startup migration; no Docker needed.
They cover the health endpoint, OpenAPI only in Development, the registered clock, the content startup (`Content/ContentStartupTests`:
the default configuration serves the real content through one `IContentProvider`; a file with rule errors, a missing file and a blank
`Content:FilePath` each stop the start) and the database startup (`Persistence/DatabaseStartupTests`: a missing or blank connection string
stops the start; the startup migration on in Production or Staging stops the start; a lock timeout of 0, below it or above a minute stops
the start; in Development the server migrates before serving). The project also holds the content loader's tests and `InfrastructureDependencyTests` (the
Infrastructure module doc → Tests).
Run them with the fast suite: `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`.
The `Integration` category runs the app on real PostgreSQL: `Postgres/PostgresApiFactory` is the same server on one test's own database
(one `postgres:18.6` container per run, a fresh database per test, a `FakeTimeProvider` clock), and `Persistence/MigrationDatabaseTests`
checks that the server in Development migrates an empty database at startup. Docker must be running:
`dotnet test server/CityBuilder.slnx --filter "Category=Integration"` (`docs/testing/README.md`).

## Known limitations
- Not every error is a ProblemDetails response yet: an unknown route returns an empty 404, and outside Development an
  unhandled exception returns an empty 500. The error catalogue and the global handler come in M0-34.
- `/health` is not in the OpenAPI document: health-check endpoints carry no API description.
- No authentication, rate limits or request correlation yet *(planned, M0-25, M0-34, M0-35)*.
- A failed start is reported by the runtime as an unhandled exception in plain text on stderr, not as a JSON log line.

Last updated: M0-22
