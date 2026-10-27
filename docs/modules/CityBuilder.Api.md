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
| `Program.cs` | Builds the host: JSON console logging, ProblemDetails, OpenAPI, health checks, `TimeProvider.System`, the content (options, `IContentProvider`), the database (`DatabaseOptions`, `AddCityBuilderPersistence`), authentication (`AddCityBuilderAuthentication`), the use cases (`SettleCity`, `DevLogin`, `CommandPipeline`, scoped; `GET /city` maps `SettleCity` in M0-28, the command endpoints call `CommandPipeline` from M0-29); loads the content and checks the database and authentication settings before anything is mapped, and in Development applies the migrations; adds the authentication and authorization middleware; maps `/health`, in Development only `/openapi/v1.json`, and the `/api/v1` group, which requires an access token, with the dev login on it in Development when `Auth:DevLoginEnabled` is on and the game endpoints (`MapContent()` from M0-28) |
| `Auth/AuthOptions` | Section `Auth`: `SigningKey` (required in every environment, at least 32 bytes as UTF-8; user-secrets or `Auth__SigningKey`), `TokenLifetimeMinutes` (default 720, 1 to 1440), `DevLoginEnabled` (only allowed in Development) |
| `Auth/AuthServiceCollectionExtensions` | `AddCityBuilderAuthentication(configuration, environment)`: binds and validates `AuthOptions` on start, registers `AccessTokens`, the JWT bearer handler as the default scheme with `AccessTokens.ValidationParameters()` (claim names kept: `sub` stays `sub`), authorization, and `ICurrentPlayer` (scoped) |
| `Auth/AccessTokens`, `Auth/AccessToken` | Issues a token for a player (`Issue(playerId)`: a JWT signed with HMAC-SHA256, `iss` and `aud` `citybuilder-api`, `sub` the player id, `iat` = `nbf` = now floored to the second, `exp` after the lifetime) and gives the rules that check one (`ValidationParameters()`: the key, HS256 only, signed tokens only, issuer, audience, `exp` required, lifetime checked against the server's `TimeProvider` with no clock skew). Singleton |
| `Auth/HttpCurrentPlayer` | `ICurrentPlayer` of a request: the `sub` claim of the verified token as a GUID; throws when the request has no verified token with one (an endpoint that forgot to require a token) |
| `Endpoints/AuthEndpoints` | `MapDevLogin()` on the `/api/v1` group: `POST /auth/dev-login`, open without a token; validates the display name (400 `INVALID_ARGUMENT` for a missing body or a name that isn't allowed), runs `DevLogin`, answers with a token |
| `Contracts/DevLoginRequest`, `Contracts/DevLoginResponse` | The dev login's body `{ displayName }` and answer `{ accessToken, expiresAtUtc }` (`05` → Authentication) |
| `Endpoints/ContentEndpoints` | `MapContent()` on the `/api/v1` group: `GET /content`, behind the token. Answers with `ContentResponseMapper.ToResponse` of the server's `IContentProvider` content and the time scale (the constant 1 until M0-35); reads no city and writes nothing |
| `Contracts/ContentResponse` and the `Content*` records (`ContentRules`, `ContentEra`, `ContentItem`, `ContentBuilding`, `ContentBuildingLevel`, `ContentRecipe`, `ContentResearch`, `ContentResearchEffect`) | The body of `GET /content` (`05` → Content). Item maps are `IReadOnlyDictionary<string, long>` holding a `SortedDictionary` in ordinal order; values that don't apply are `null` and are written (level 1's upgrade, a house's recipe); an effect is one flat record with `type` and the fields of that type |
| `Contracts/ContentResponseMapper` | `ToResponse(content, timeScale)`: `GameContent` to `ContentResponse`. Only renames and reshapes: the lists keep the engine's sorted order; the wire names of building kinds and effect types are written in its `switch`es, so an engine enum rename can't change the contract, and an unknown kind or effect type throws |
| `Content/ContentOptions` | Section `Content`: `FilePath` (required, not blank; validated on start), resolved against the app's base directory when relative |
| `Content/ContentLog` | The content log message (source-generated `[LoggerMessage]`): "Content {ContentVersion} loaded from {ContentFilePath}", event id 1000 |
| `Persistence/DatabaseOptions` | `ConnectionString` from `ConnectionStrings:CityBuilder` (required, not blank) and section `Database`: `ApplyMigrationsAtStartup` (only allowed in Development) and `LockTimeoutMilliseconds` (default 3000, 1 to 60 000); validated on start, and passed to `AddCityBuilderPersistence` as `PersistenceSettings` |
| `Persistence/DatabaseLog` | The database log message: "Database migrated at startup: {AppliedCount} migrations applied ({AppliedMigrations})", event id 2000 (ids 2000–2999 for database messages) |
| `Properties/launchSettings.json` | `dotnet run` profile `http`: http://localhost:8080, `ASPNETCORE_ENVIRONMENT=Development` |
| `appsettings.json` | Log levels, allowed hosts, `Content:FilePath` (`content/mvp-content.json`), `Database:ApplyMigrationsAtStartup` (`false`), `Auth:DevLoginEnabled` (`false`). No connection string and no signing key: they are secrets |
| `appsettings.Development.json` | `Database:ApplyMigrationsAtStartup` = `true`, `Auth:DevLoginEnabled` = `true` |
| `CityBuilder.Api.csproj` | `UserSecretsId` `citybuilder-api` (where `dotnet user-secrets` keeps the connection string and the signing key); `Microsoft.AspNetCore.Authentication.JwtBearer` (checks the tokens; its IdentityModel dependency writes them); `Microsoft.EntityFrameworkCore.Design` for dotnet-ef at design time only (`PrivateAssets=all`) |
| `server/content/*.json` (linked) | Copied by the build to `content/` in the output (and in a publish), next to the app |

## Endpoints
| Method | Path | Environments | Response |
|---|---|---|---|
| GET | `/health` | all | `200`, `text/plain` body `Healthy`, no-cache headers. Liveness only: checks no dependencies |
| GET | `/openapi/v1.json` | Development | The OpenAPI 3.1 document of the mapped API endpoints |
| POST | `/api/v1/auth/dev-login` | Development, with `Auth:DevLoginEnabled` | `200` `{ accessToken, expiresAtUtc }`; the first login of a name creates the player and the starter city. `400` `INVALID_ARGUMENT` for a missing body or a name that isn't allowed |
| GET | `/api/v1/content` | all (token required) | `200` `application/json`: the game content (`05` → Content): `contentVersion`, `rules` (with `timeScale`), `eras`, `items`, `buildings`, `research`, sorted, the same text on every call |

The contract is `core-instructions/05-api-contract.md`. Every other `/api/v1/…` endpoint requires `Authorization: Bearer <accessToken>` (the
group's `RequireAuthorization()`); without a valid token the answer is 401. The game endpoints arrive from M0-28 on.

## Startup
The content is loaded once, right after the host is built and before any endpoint is mapped or a request accepted: `Program.cs` resolves
`IContentProvider`, whose factory runs `ContentFileLoader.Load` on `Content:FilePath`. A blank key (`OptionsValidationException`) or a file
that can't be loaded (`ContentLoadException`, every error listed) is unhandled there: the process stops with a non-zero exit code, and the
runtime prints the message to stderr. On success the log has one line with the content version and the full file path.

Then `Program.cs` reads `DatabaseOptions`: a missing or blank `ConnectionStrings:CityBuilder`, `Database:ApplyMigrationsAtStartup=true`
outside Development, or a `Database:LockTimeoutMilliseconds` outside 1 to 60 000, fails the validation and stops the start the same way.
So does `AuthOptions`, read right after: a missing or short `Auth:SigningKey`, a token lifetime outside 1 to 1440 minutes, or
`Auth:DevLoginEnabled=true` outside Development (read before `Run`, the validation fails the same way every time; inside the host's start
`WebApplicationFactory` sometimes reported an `ObjectDisposedException` instead). In Development with the flag on, `DatabaseMigrator.MigrateAsync`
applies the pending migrations before any request; the log names them (event id 2000), and an unreachable database stops the start with
an `NpgsqlException` (`docs/how-to/add-migration.md`). No other environment ever migrates at startup.

## Rules and invariants
- `TimeProvider` is registered once, as `TimeProvider.System`. Game code gets time only through it
  (`core-instructions/02-principles.md`, rule 3); tests replace it with `FakeTimeProvider`.
- OpenAPI is mapped only in Development (`05`); a test checks that Production answers 404 (`07`).
- Every endpoint of the `/api/v1` group requires an access token unless it opts out with `AllowAnonymous()` (only the dev login). There is
  no fallback policy: it would also cover paths that match no endpoint, which would then answer 401 instead of 404.
- The acting player comes only from `ICurrentPlayer` (the token's `sub`); no request type has a player id (`07`).
- The dev login is mapped only in Development with `Auth:DevLoginEnabled` on, and the setting stops the start anywhere else (`07`).
- The signing key never comes from a committed file: user-secrets in Development, `Auth__SigningKey` elsewhere.
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
the start; in Development the server migrates before serving) and authentication (`Auth/`: `AuthenticationTests` over HTTP through the
test-only `CurrentPlayerEndpoint`, a startup filter that puts an endpoint requiring a token in front of the server: a token the server issued,
its last valid second, none, malformed, expired, wrongly signed, unsigned, another issuer; `AuthStartupTests`: no or short key, lifetime out of
range, dev login on outside Development; `DevLoginEndpointTests`: 404 in Production and when disabled, refused bodies; `HttpCurrentPlayerTests`).
Contract tests read the raw JSON the server sends (`Contracts/`: `ApiClient` calls with a token the server issued, `JsonShape` checks the exact property names, JSON types and allowed nulls of an object): `ContentContractTests` check the real content's shape, nulls, sort orders and that the text repeats, `ContentGoldenTests` compare the whole text for frozen content that lists everything out of order. Both factories set the tests' signing key (`Auth/TestTokens`). The project also holds the content loader's tests and `InfrastructureDependencyTests` (the
Infrastructure module doc → Tests).
Run them with the fast suite: `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`.
The `Integration` category runs the app on real PostgreSQL: `Postgres/PostgresApiFactory` is the same server on one test's own database
(one `postgres:18.6` container per run, a fresh database per test, a `FakeTimeProvider` clock), and `Persistence/MigrationDatabaseTests`
checks that the server in Development migrates an empty database at startup; `Persistence/SettleCityDatabaseTests` settles through the
server's registered `SettleCity` (Infrastructure module doc → Tests); `Commands/` runs the server's registered `CommandPipeline` with test
commands (Application module doc → Tests); `Auth/DevLoginDatabaseTests` logs in over HTTP and checks the stored
player, city and starting grant, the same name again, two names, the token, and two first logins of one name racing. Docker must be running:
`dotnet test server/CityBuilder.slnx --filter "Category=Integration"` (`docs/testing/README.md`).

## Known limitations
- Not every error is a ProblemDetails response yet: an unknown route returns an empty 404, and outside Development an
  unhandled exception returns an empty 500. The error catalogue and the global handler come in M0-34.
- `/health` is not in the OpenAPI document: health-check endpoints carry no API description.
- No rate limits (the dev login's 10 per minute per IP included) or request correlation yet *(planned, M0-34, M0-35)*.
- A refused token gets an empty 401 with `WWW-Authenticate: Bearer`; the ProblemDetails body with `AUTHENTICATION_REQUIRED` comes with the
  first game endpoint and the error catalogue *(planned, M0-28, M0-34)*.
- Unknown JSON properties in a body are not refused yet *(planned, M0-30, M0-34)*.
- No command endpoint yet: `CommandPipeline` is registered, and the first endpoint, with the snapshot writer it passes, is M0-29 *(planned)*.
- A failed start is reported by the runtime as an unhandled exception in plain text on stderr, not as a JSON log line.

Last updated: M0-28
