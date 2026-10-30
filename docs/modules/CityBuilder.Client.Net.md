# CityBuilder.Client.Net

**Path:** `client/CityBuilder/Assets/_Project/Net/` · **Kind:** Unity assembly (`CityBuilder.Client.Net.asmdef`, C# 9) ·
**References:** `CityBuilder.Client.Core`, `CityBuilder.Simulation` (the engine package, for `GameContent`), the precompiled `Newtonsoft.Json.dll` of the package `com.unity.nuget.newtonsoft-json` 3.2.2, and the engine module `UnityWebRequest` (`com.unity.modules.unitywebrequest`, named in the manifest) ·
**Referenced by:** `CityBuilder.Client.Presentation`, `CityBuilder.Client.Tests.EditMode`, `CityBuilder.Client.Tests.PlayMode`

## Purpose
All talking to the server: the HTTP client, the DTOs that mirror `core-instructions/05-api-contract.md`, and the translation of
Problem Details responses into an `ApiError`. It is **not** responsible for gameplay logic, for what a response means to the city
(`CityBuilder.Client.Core`), for request ids or retries (`CommandSender`, M0-40 *(planned)*), or for any UI: views never call the API
client directly (`.claude/rules/unity.md`). `GameRoot` builds it and runs the startup flow with it (login, content, city: M0-38).

## Key types
| Type | Responsibility |
|---|---|
| `ApiClient` | One method per endpoint (`DevLoginAsync`, `GetContentAsync`, `GetCityAsync`, and one per command); builds the URL and body, owns the timeout, returns an `ApiResult<T>`. Holds the bearer token in memory (`SetAccessToken`) |
| `ApiResult<T>` / `ApiError` | The DTO or the error, never both. `ApiError`: `Code`, `Status`, `Detail`, `TraceId`, `RequestId`, `RetryAfterSeconds` |
| `ApiErrorCodes` | The 24 server codes of `05` and the three `CLIENT_` codes (`CLIENT_NETWORK_ERROR` and `CLIENT_TIMEOUT`, status 0; `CLIENT_INVALID_RESPONSE`, the HTTP status) |
| `ApiResponseParser` | (status, body) to an `ApiResult<T>`; never throws on a bad body, never copies the body into an error |
| `ApiJson` | The one serializer setup: camelCase members, enums as wire strings, dates kept as text, depth limit 32, no type names |
| `IApiTransport`, `UnityWebRequestTransport` | The wire (one request in, one response or network failure out); the second is the real one, tests use a fake |
| `ApiClientSettings` | `ScriptableObject` with the server URL, the timeout and the dev player's name (`DevDisplayName`, default `dev1`); the asset is `Resources/Config/ApiClientSettings.asset` |
| `AuthService`, `AuthState` | The login state machine (`LoggedOut`, `LoggingIn`, `LoggedIn`, `Failed`): `TryRestore(name)` takes a kept token for that player without asking the server, `LoginAsync(name)` calls the dev login and hands the token to `ApiClient`, `Forget()` drops it. A failure is `State == Failed` plus `LastError`, not an exception; only a cancel throws. It never decides that a token is still good: the server does |
| `IAccessTokenStore`, `StoredLogin`, `PlayerPrefsAccessTokenStore`, `NullAccessTokenStore`, `AccessTokenStores` | Where the token lives between runs. `StoredLogin` is the token and the display name it was issued for, nothing else (no snapshot, no balance, no expiry). `AccessTokenStores.For(isDevelopmentBuild)` gives `PlayerPrefs` (keys `CityBuilder.DevLogin.AccessToken` and `.DisplayName`) to the Editor and development players, and a store that keeps nothing to every other build |
| `ContentService`, `ContentMapper`, `ContentLoadResult` | `GET /content` to the engine's `GameContent`: `ContentMapper` copies the answer into `GameContentData` (the one respelling: kind `House` to `house`), `GameContentFactory.Create` validates it exactly as the server does. The result is one of three: the content, a failed request (`ApiError`), or the factory's error list (the answer arrived but is invalid) |
| `Dtos/*Dto` | The records of `05` as immutable classes: snapshot (`CitySnapshotDto` …), content (`ContentResponseDto` …), the dev login and the four command bodies; three enums |

## Rules and invariants
- Networking lives only in this assembly. It references `CityBuilder.Client.Core`, `CityBuilder.Simulation` (the content factory; asmdef references are not transitive) and Newtonsoft, nothing else of the project.
- The asmdef sets `overrideReferences` and lists `Newtonsoft.Json.dll` under `precompiledReferences`. The package's DLL is
  auto-referenced, so an asmdef without `overrideReferences` would see it, and every other auto-referenced DLL of every package, too.
  With the setting on, an assembly sees only the DLLs it lists: Net lists Newtonsoft, Core and Presentation list none. The asmdef is not
  auto-referenced.
- The client never sends balances, costs, rates, timers or player ids (`core-instructions/02-principles.md` rules 1 and 2): a request DTO
  holds the player's intent and its `requestId` only.
- A DTO mirrors `05` member for member and in the server's order (a test writes each fixture back byte for byte). Members `05` writes
  as `null` are nullable; every other member is required, and a missing one fails the answer (`CLIENT_INVALID_RESPONSE`). Members the DTOs
  don't know are ignored. An enum value the DTO lacks fails the answer.
- Failures are results, not exceptions; only a cancelled token throws `OperationCanceledException`. The timeout is the client's
  (`ApiClientSettings`, default 15 s), so it behaves the same on every transport.
- Quantities are `long`. Collections are `Dictionary<string, long>` and `List<T>`: IL2CPP may not have generics over read-only wrappers.
- `Task`, not `Awaitable`: an `Awaitable` is pooled and needs the player loop, which the EditMode tests don't run.
- The token is in memory; only a development build (`Debug.isDebugBuild`: the Editor and development players) also writes it to `PlayerPrefs`,
  under a key named for it. A kept token is only a candidate: `AUTHENTICATION_REQUIRED` on the first request means log in again
  (`StartupFlow`). No client clock decides when a token expires.
- Content is never built any other way than through `GameContentFactory`: the client holds no game numbers of its own (`03`).
- IL2CPP strips what only reflection uses: `Net/link.xml` preserves this assembly (`09` → Device testing).
- No static mutable state: each `ApiClient` owns its `ApiJson`.
- Every type and member, private ones included, has an XML doc comment (`core-instructions/14-coding-standards.md` → Documentation
  in code). Unity's compiler doesn't check them, so `CityBuilder.Repository.Tests` (a server test project) checks every script
  under `Assets/_Project`.

## How to extend
A new endpoint or DTO: recipe "New command (player action)" in `core-instructions/19-change-recipes.md` (client intent in this assembly
plus UI in `CityBuilder.Client.Presentation`): add the DTO with the server's member order, a method on `ApiClient`, a fixture
captured from the real server, and the round-trip test. A new error code: add it to `ApiErrorCodes` in the table's order
(`ClientErrorCodesTests` fails until you do).

## Tests
`Assets/_Project/Tests/EditMode/Net/` (`CityBuilder.Client.Tests.EditMode`), run by `scripts/dev/test-unity.sh`:
- `SnapshotDtoTests`, `ContentDtoTests`, `RequestDtoTests`: every fixture reads, writes back to the same text, holds the expected values;
  a missing required member and an unknown phase are refused; request bodies have exactly their members.
- `ApiResponseParserTests`: every `error-*.json` fixture parses to its code and status, all 24 server codes parse, `Retry-After`,
  malformed or truncated JSON, an HTML 502, empty bodies and a lost connection give clean errors.
- `AuthServiceTests` (state machine, token on the client, what is kept, a refused or empty answer, a cancel), `AccessTokenStoreTests`
  (`PlayerPrefs` with a test-only key prefix; a release build keeps nothing), `ContentServiceTests` (the real `content.json` becomes valid
  content with the right buildings, recipes and effects; broken content lists every error; a refused, lost or non-JSON answer is a failed request).
- `ApiClientTests` (with `FakeTransport`): method, URL, body and token of every endpoint; timeout; cancellation. `UnityWebRequestTransportTests`
  run the real transport against an `HttpListener` in the test. `ApiClientSettingsTests`: the asset loads and names a valid dev player.
- **Fixtures** (`Assets/_Project/Tests/Fixtures/`) are real answers of a development server, copied byte for byte: `content.json` at time
  scale 1, the `snapshot-*.json` and `error-<status>-<CODE>.json` files from a run of the four commands as the test player `devtest` at
  `Simulation:DevTimeScale` 10 (so `serverTimeUtc` is in 2034). `INSUFFICIENT_WORKERS`, `MAX_LEVEL`, `CONCURRENCY_CONFLICT` and
  `INTERNAL_ERROR` could not be provoked by hand and have no fixture. To refresh them, redo that run (`docs/api/smoke.http` lists the
  requests) and copy the answers; a changed contract changes the round-trip tests first.
- The asmdef itself is guarded by `ClientAssemblyTests` and the error code list by `ClientErrorCodesTests`, both in
  `server/tests/CityBuilder.Simulation.Tests`, run with `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`.

## Known limitations
- There is no request id maker, retry or backoff (`CommandSender`, M0-40) and no complete error message table (M0-47 *(planned)*);
  `ConnectionStatusModel` words only the failures of starting up.
- An unknown enum value or a missing member fails the whole answer; a server and a client of different versions during M0 need
  matching builds.
- The real transport was walked against a running server once (M0-37 build log); it is not in CI (Unity tests are not).

Last updated: M0-38
