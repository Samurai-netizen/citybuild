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
| `Program.cs` | Builds the host: JSON console logging, ProblemDetails, the request limits (`AddRequestBodyLimit`; HTTP JSON refuses a property named twice), OpenAPI, health checks, `TimeProvider.System`, the content (options, `IContentProvider`), the database (`DatabaseOptions`, `AddCityBuilderPersistence`), authentication (`AddCityBuilderAuthentication`), the use cases (`SettleCity`, `DevLogin`, `CommandPipeline`, scoped; `GET /city` runs `SettleCity`, the command endpoints `CommandPipeline`); loads the content and checks the database and authentication settings before anything is mapped, and in Development applies the migrations; adds the body size check first (`UseRequestBodyLimit`), then the authentication and authorization middleware; maps `/health`, in Development only `/openapi/v1.json`, and the `/api/v1` group, which requires an access token, with the dev login on it in Development when `Auth:DevLoginEnabled` is on and the game endpoints (`MapContent()` and `MapCity()` from M0-28, `MapCommands()` from M0-29) |
| `Http/RequestLimitOptions` | Section `RequestLimits`: `MaxBodyBytes` (default 4096, 1024 to 1 048 576; validated on start) |
| `Http/RequestBodyLimit` | `AddRequestBodyLimit(configuration)`: binds and validates `RequestLimitOptions` and sets Kestrel's `MaxRequestBodySize` to it (in place of about 30 MB), so no body is read past the limit, chunked ones included. `UseRequestBodyLimit()`: the first middleware; a `Content-Length` above the limit gets 400 Problem Details (`type` `urn:citybuilder:error:invalid-argument`, `code` `INVALID_ARGUMENT`) before the token check, the binding or a transaction (M0-30) |
| `Auth/AuthOptions` | Section `Auth`: `SigningKey` (required in every environment, at least 32 bytes as UTF-8; user-secrets or `Auth__SigningKey`), `TokenLifetimeMinutes` (default 720, 1 to 1440), `DevLoginEnabled` (only allowed in Development) |
| `Auth/AuthServiceCollectionExtensions` | `AddCityBuilderAuthentication(configuration, environment)`: binds and validates `AuthOptions` on start, registers `AccessTokens`, the JWT bearer handler as the default scheme with `AccessTokens.ValidationParameters()` (claim names kept: `sub` stays `sub`) and `BearerChallenge` as its challenge, authorization, and `ICurrentPlayer` (scoped) |
| `Auth/AccessTokens`, `Auth/AccessToken` | Issues a token for a player (`Issue(playerId)`: a JWT signed with HMAC-SHA256, `iss` and `aud` `citybuilder-api`, `sub` the player id, `iat` = `nbf` = now floored to the second, `exp` after the lifetime) and gives the rules that check one (`ValidationParameters()`: the key, HS256 only, signed tokens only, issuer, audience, `exp` required, lifetime checked against the server's `TimeProvider` with no clock skew). Singleton |
| `Auth/BearerChallenge` | The bearer handler's `OnChallenge`: the answer to a request with no working token. 401, `WWW-Authenticate: Bearer`, and a Problem Details body (`type` `urn:citybuilder:error:authentication-required`, `title`, `status`, `detail`, `code` `AUTHENTICATION_REQUIRED`, `traceId`). The same for a missing, malformed, expired, wrongly signed or unsigned token and another issuer: it never says why and never echoes the token. The error catalogue moves the mapping of every code to one place (M0-34) |
| `Auth/HttpCurrentPlayer` | `ICurrentPlayer` of a request: the `sub` claim of the verified token as a GUID; throws when the request has no verified token with one (an endpoint that forgot to require a token) |
| `Endpoints/AuthEndpoints` | `MapDevLogin()` on the `/api/v1` group: `POST /auth/dev-login`, open without a token; validates the display name (400 `INVALID_ARGUMENT` for a missing body or a name that isn't allowed), runs `DevLogin`, answers with a token |
| `Contracts/DevLoginRequest`, `Contracts/DevLoginResponse` | The dev login's body `{ displayName }` (unknown properties refused, M0-30) and answer `{ accessToken, expiresAtUtc }` (`05` → Authentication) |
| `Endpoints/ContentEndpoints` | `MapContent()` on the `/api/v1` group: `GET /content`, behind the token. Answers with `ContentResponseMapper.ToResponse` of the server's `IContentProvider` content and the time scale (the constant 1 until M0-35); reads no city and writes nothing |
| `Endpoints/CityEndpoints` | `MapCity()` on the `/api/v1` group: `GET /city`, behind the token. Runs `SettleCity` for `ICurrentPlayer.PlayerId` (the token's player; nothing in the request names a city) and answers with `CitySnapshotMapper.ToSnapshot` of the result, `requestId` null. Safe to repeat, not free of side effects: it materializes the elapsed time and saves the city when anything changed |
| `Contracts/CitySnapshot` and the `Snapshot*` records (`SnapshotCity`, `SnapshotWorkers`, `SnapshotBuilders`, `SnapshotResearch`, `SnapshotBuilding`, `SnapshotSettlement`) | The snapshot of `05`, the body of `GET /city` and of every successful command. Field order is the contract's; item maps are sorted dictionaries; nulls are written (`requestId`, the running research, `phaseEndsAt`, `stallReason`); `serverTimeUtc` is a string in whole seconds with a `Z` |
| `Contracts/CitySnapshotMapper` | `ToSnapshot(requestId, city, settlement, stateVersion, serverTime, content)`: reads the settled city and builds the snapshot with the engine's own queries (`CityQueries` for workers, builders and caps, `StallReason.Of` for the stall text). The building phases have written wire names, like the content. The command endpoints call it with what their `CommandSuccess` holds |
| `Endpoints/CommandEndpoints` | `MapCommands()` on the `/api/v1` group: a `/commands` group behind the token, with `POST /place-building` (M0-29) and `POST /upgrade-building` (M0-31), which each declare `CitySnapshot` as its 200 body for the OpenAPI document (the handler writes text). A handler only turns its body into an `ICityCommand` (a missing or `null` body is `INVALID_ARGUMENT`); the shared `ExecuteAsync<TCommand>` runs `CommandPipeline` for `ICurrentPlayer.PlayerId` with the snapshot as the response (`CitySnapshotMapper`, serialized with the HTTP JSON options inside the transaction, so the receipt stores the text the client got) and answers with `CommandResponses` |
| `Endpoints/CommandResponses`, `Endpoints/CommandError` | The HTTP answer of a `CommandResult`: 200 `application/json` with the snapshot text for a success and a replay (the stored bytes); otherwise Problem Details (`type` `urn:citybuilder:error:<code in kebab case>`, `title`, `status`, `detail`, `code`, `requestId` when the request had one; the framework adds `traceId`). `Of(MutationErrorCode)` maps every engine code to its `05` code and status; `IdempotencyConflict` is 409 and `InvalidArgument(detail)` 400. Public so a table test checks them; the catalogue takes them over in M0-34 |
| `Contracts/PlaceBuildingRequest` | The place-building body `{ requestId, buildingTypeId, x, y }`: unknown properties refused (`JsonUnmappedMemberHandling.Disallow`), every field `JsonRequired`, numbers strict (`"2"` is refused), so the binding answers 400 before anything runs |
| `Contracts/UpgradeBuildingRequest` | The upgrade-building body `{ requestId, buildingId }` with the same three rules (M0-31): no level, cost, time or worker field can be sent, a missing `buildingId` is refused instead of read as 0, `"3"` is refused |
| `Contracts/ContentResponse` and the `Content*` records (`ContentRules`, `ContentStartingCity`, `ContentEra`, `ContentItem`, `ContentBuilding`, `ContentBuildingLevel`, `ContentRecipe`, `ContentResearch`, `ContentResearchEffect`) | The body of `GET /content` (`05` → Content). Item maps are `IReadOnlyDictionary<string, long>` holding a `SortedDictionary` in ordinal order; values that don't apply are `null` and are written (level 1's upgrade, a house's recipe); an effect is one flat record with `type` and the fields of that type |
| `Contracts/ContentResponseMapper` | `ToResponse(content, timeScale)`: `GameContent` to `ContentResponse`. Only renames and reshapes: the lists keep the engine's sorted order; the wire names of building kinds and effect types are written in its `switch`es, so an engine enum rename can't change the contract, and an unknown kind or effect type throws |
| `Content/ContentOptions` | Section `Content`: `FilePath` (required, not blank; validated on start), resolved against the app's base directory when relative |
| `Content/ContentLog` | The content log message (source-generated `[LoggerMessage]`): "Content {ContentVersion} loaded from {ContentFilePath}", event id 1000 |
| `Persistence/DatabaseOptions` | `ConnectionString` from `ConnectionStrings:CityBuilder` (required, not blank) and section `Database`: `ApplyMigrationsAtStartup` (only allowed in Development) and `LockTimeoutMilliseconds` (default 3000, 1 to 60 000); validated on start, and passed to `AddCityBuilderPersistence` as `PersistenceSettings` |
| `Persistence/DatabaseLog` | The database log message: "Database migrated at startup: {AppliedCount} migrations applied ({AppliedMigrations})", event id 2000 (ids 2000–2999 for database messages) |
| `Properties/launchSettings.json` | `dotnet run` profile `http`: http://localhost:8080, `ASPNETCORE_ENVIRONMENT=Development` |
| `appsettings.json` | Log levels, allowed hosts, `Content:FilePath` (`content/mvp-content.json`), `Database:ApplyMigrationsAtStartup` (`false`), `Auth:DevLoginEnabled` (`false`); no `RequestLimits`, so the default applies. No connection string and no signing key: they are secrets |
| `appsettings.Development.json` | `Database:ApplyMigrationsAtStartup` = `true`, `Auth:DevLoginEnabled` = `true` |
| `CityBuilder.Api.csproj` | `UserSecretsId` `citybuilder-api` (where `dotnet user-secrets` keeps the connection string and the signing key); `Microsoft.AspNetCore.Authentication.JwtBearer` (checks the tokens; its IdentityModel dependency writes them); `Microsoft.EntityFrameworkCore.Design` for dotnet-ef at design time only (`PrivateAssets=all`) |
| `server/content/*.json` (linked) | Copied by the build to `content/` in the output (and in a publish), next to the app |

## Endpoints
| Method | Path | Environments | Response |
|---|---|---|---|
| GET | `/health` | all | `200`, `text/plain` body `Healthy`, no-cache headers. Liveness only: checks no dependencies |
| GET | `/openapi/v1.json` | Development | The OpenAPI 3.1 document of the mapped API endpoints |
| POST | `/api/v1/auth/dev-login` | Development, with `Auth:DevLoginEnabled` | `200` `{ accessToken, expiresAtUtc }`; the first login of a name creates the player and the starter city. `400` `INVALID_ARGUMENT` for a missing body or a name that isn't allowed |
| GET | `/api/v1/city` | all (token required) | `200` `application/json`: the snapshot of the token's player's city, settled to now (`05` → Snapshot), `requestId` null; the city is saved only when the settlement changed it |
| POST | `/api/v1/commands/place-building` | all (token required) | `200` `application/json`: the snapshot after the placement (`05` → Commands, Snapshot), `requestId` the command's; a replay gets the stored snapshot. Problem Details for a refusal: `400` `INVALID_ARGUMENT`, `422` or `409` with the rule's code, `409` `IDEMPOTENCY_CONFLICT`; a body that doesn't bind gets the framework's `400` |
| POST | `/api/v1/commands/upgrade-building` | all (token required) | `200` `application/json`: the snapshot after the upgrade started (`05` → Commands, Snapshot); a replay gets the stored snapshot. Problem Details for a refusal: `400` `INVALID_ARGUMENT`, `404` `BUILDING_NOT_FOUND` (also for another player's building), `409` `BUILDING_BUSY` or `NO_FREE_BUILDER`, `422` `MAX_LEVEL` or `INSUFFICIENT_RESOURCE`, `409` `IDEMPOTENCY_CONFLICT`; a body that doesn't bind gets the framework's `400` |
| GET | `/api/v1/content` | all (token required) | `200` `application/json`: the game content (`05` → Content): `contentVersion`, `rules` (with `timeScale`), `eras`, `items`, `buildings`, `research`, sorted, the same text on every call |

The contract is `core-instructions/05-api-contract.md`. Every other `/api/v1/…` endpoint requires `Authorization: Bearer <accessToken>` (the
group's `RequireAuthorization()`); without a valid token the answer is 401. The game endpoints are `GET /content` and `GET /city` (M0-28) and the commands under `/commands` (place-building from M0-29, upgrade-building from M0-31; the others M0-32 and M0-33).

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
- New command endpoint: recipe "New command" in `core-instructions/19-change-recipes.md`, worked through in `docs/how-to/add-command.md`
  (a handler in `CommandEndpoints`, the body in `Contracts/`, the command in Application).
- New configuration key: recipe "New configuration key" (options class, validated on start), like `ContentOptions`.
- A body larger than 4 KB: raise `RequestLimits:MaxBodyBytes` (no code change); each request type stays strict (`Disallow`, `JsonRequired`).
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
Contract tests read the raw JSON the server sends (`Contracts/`: `ApiClient` calls with a token the server issued, `JsonShape` checks the exact property names, JSON types and allowed nulls of an object): `ContentContractTests` check the real content's shape, nulls, sort orders and that the text repeats, `ContentGoldenTests` compare the whole text for frozen content that lists everything out of order, and both rebuild a `GameContent` from the response through the engine's validating factory (`ContentFromResponse`, what the client does in M0-38) and expect the same response back. `CitySnapshotContractTests` run `GET /city` on cities built by hand on frozen content in a `FakeCityStore` (the real settle and endpoint over an in-memory repository and unit of work): the exact text for a city with a building in every phase and every stall reason, the names and types of every object, a starter city, settling after 100 s (clock, stock, settlement block, one save and ledger row), two requests in one second (saved once), and another player's city never showing; `UnauthenticatedContractTests` read the 401 answer of the real endpoints (every kind of refusal, one answer; a command without a token). `PlaceBuildingContractTests` post raw bodies to place-building over a `FakeCityStore` (which also keeps receipts): the success snapshot equals `GET /city`'s and the saved ledger row; a replay a minute later is the same bytes and runs nothing; a conflict is 409; rule refusals; nine forbidden properties and eleven wrong shapes → 400 with nothing run; bodies that bind but are no command → `INVALID_ARGUMENT`. `UpgradeBuildingContractTests` (M0-31) do the same for upgrade-building over a small city (an idle house, a woodcutter under construction): the success snapshot equals `GET /city`'s and the saved `COST_UPGRADE` rows; replay; a conflict with another building or on place-building; ids the city doesn't have (0, negative, the largest int) → 404 and the constructing woodcutter → 409; ten forbidden properties and ten wrong shapes → 400 with nothing run. `CommandErrorMappingTests` check every engine code's API code and status against the `05` table. `Security/ApiAttackContractTests` (M0-30) attack the server in memory over a `FakeCityStore` and check that nothing was locked or saved: bodies over the limit (with and without a token, on the dev login), one of exactly the limit, Kestrel's limit equal to the setting and the setting's range, a property twice, `requestId`s that are no UUID, malformed and deeply nested JSON, type ids that can't be content ids (refused before the lock), coordinates outside the grid, a player named in the query or headers, other content types (415), the dev login's extra fields and a huge display name. Both factories set the tests' signing key (`Auth/TestTokens`). The project also holds the content loader's tests and `InfrastructureDependencyTests` (the
Infrastructure module doc → Tests).
Run them with the fast suite: `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`.
The `Integration` category runs the app on real PostgreSQL: `Postgres/PostgresApiFactory` is the same server on one test's own database
(one `postgres:18.6` container per run, a fresh database per test, a `FakeTimeProvider` clock), and `Persistence/MigrationDatabaseTests`
checks that the server in Development migrates an empty database at startup; `Persistence/SettleCityDatabaseTests` settles through the
server's registered `SettleCity` (Infrastructure module doc → Tests); `Commands/` runs the server's registered `CommandPipeline` with test
commands (Application module doc → Tests), and `Commands/PlaceBuildingDatabaseTests` posts place-building over HTTP and checks the stored city, building rows, ledger and receipts: success, every rule code, replay, conflict, ten identical requests at once, another player's request id, the settle before the mutation, auto-staffing seen in a later `GET /city`, and a `cost` field refused; `Security/ApiAttackDatabaseTests` (M0-30) runs the attacks on PostgreSQL, most after five minutes, and checks that nothing was stored: each forbidden field of the attack list, wrong forms, a megabyte, coordinates outside the grid, a replay storm (20 at once, 30 one by one: one house, one cost, one receipt), no token, the dev login's refused bodies (no player); `Contracts/CityContractDatabaseTests` calls `GET /city` over HTTP and checks the raw JSON against the stored city and the ledger rows of the request (clock, stock, version, buildings, the settlement block), a repeat in the same second, the dev login's starter city and two players; `Auth/DevLoginDatabaseTests` logs in over HTTP and checks the stored
player, city and starting grant, the same name again, two names, the token, and two first logins of one name racing. Docker must be running:
`dotnet test server/CityBuilder.slnx --filter "Category=Integration"` (`docs/testing/README.md`).

## Known limitations
- Not every error is a ProblemDetails response yet: an unknown route returns an empty 404, and outside Development an
  unhandled exception returns an empty 500. The error catalogue and the global handler come in M0-34.
- `/health` is not in the OpenAPI document: health-check endpoints carry no API description.
- No rate limits (the dev login's 10 per minute per IP included) or request correlation yet *(planned, M0-34, M0-35)*.
- The 401 answer is built by `BearerChallenge` on its own; it moves into the error catalogue with every other code *(planned, M0-34)*.
- A body that doesn't bind (an unknown, missing, mistyped or repeated field, broken JSON), or one Kestrel stops at the size limit without a `Content-Length` saying so, gets the framework's answer (400; Kestrel's limit may give 413): in Development its exception Problem Details, elsewhere an empty body; neither has `code` `INVALID_ARGUMENT` yet *(planned, M0-34)*.
- The command error mapping (`CommandResponses`) lives beside the endpoints until the catalogue *(planned, M0-34)*.
- A valid token for a player without a city (for example after the development database was reset) reaches `SettleCity` or `CommandPipeline`, which throw `InvalidOperationException`: an empty 500 until the global handler *(planned, M0-34)*. `ConcurrencyConflictException` (lock timeout), on `GET /city` and on the commands, is the same until it maps to `CONCURRENCY_CONFLICT`.
- `GET /city` does not log a clock behind the city's last settled second (`04` §1): settlement logging is M0-35 *(planned)*.
- A failed start is reported by the runtime as an unhandled exception in plain text on stderr, not as a JSON log line.

Last updated: M0-31
