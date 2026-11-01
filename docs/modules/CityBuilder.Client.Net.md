# CityBuilder.Client.Net

**Path:** `client/CityBuilder/Assets/_Project/Net/` · **Kind:** Unity assembly (`CityBuilder.Client.Net.asmdef`, C# 9) ·
**References:** `CityBuilder.Client.Core`, `CityBuilder.Simulation` (the engine package, for `GameContent`), the precompiled `Newtonsoft.Json.dll` of the package `com.unity.nuget.newtonsoft-json` 3.2.2, and the engine module `UnityWebRequest` (`com.unity.modules.unitywebrequest`, named in the manifest) ·
**Referenced by:** `CityBuilder.Client.Presentation`, `CityBuilder.Client.Tests.EditMode`, `CityBuilder.Client.Tests.PlayMode`

## Purpose
All talking to the server: the HTTP client, the DTOs that mirror `core-instructions/05-api-contract.md`, and the translation of
Problem Details responses into an `ApiError`. It is **not** responsible for gameplay logic, for what a response means to the city
(`CityBuilder.Client.Core`), or for any UI: views never call the API
client directly (`.claude/rules/unity.md`). `GameRoot` builds it and runs the startup flow with it (login, content, city: M0-38). It also makes the request id of every command and repeats a command that did not get through (`CommandSender`, M0-40).

## Key types
| Type | Responsibility |
|---|---|
| `ApiClient` | One method per endpoint (`DevLoginAsync`, `GetContentAsync`, `GetCityAsync`, and one per command); builds the URL and body, owns the timeout, returns an `ApiResult<T>`. Holds the bearer token in memory (`SetAccessToken`) |
| `ApiResult<T>` / `ApiError` | The DTO or the error, never both. `ApiError`: `Code`, `Status`, `Detail`, `TraceId`, `RequestId`, `RetryAfterSeconds` |
| `ApiErrorCodes` | The 24 server codes of `05` and the three `CLIENT_` codes (`CLIENT_NETWORK_ERROR` and `CLIENT_TIMEOUT`, status 0; `CLIENT_INVALID_RESPONSE`, the HTTP status) |
| `ApiResponseParser` | (status, body) to an `ApiResult<T>`; never throws on a bad body, never copies the body into an error |
| `ApiJson` | The one serializer setup: camelCase members, enums as wire strings, dates kept as text, depth limit 32, no type names |
| `IApiTransport`, `UnityWebRequestTransport` | The wire (one request in, one response or network failure out); the second is the real one, tests use a fake. It follows no redirect (`redirectLimit = 0`): the API never sends one, and following it would hand the bearer token to wherever a captive portal or a proxy points (CR-0004) |
| `ApiClientSettings` | `ScriptableObject` with the server URL, the timeout and the dev player's name (`DevDisplayName`, default `dev1`); the asset is `Resources/Config/ApiClientSettings.asset` |
| `AuthService`, `AuthState` | The login state machine (`LoggedOut`, `LoggingIn`, `LoggedIn`, `Failed`): `TryRestore(name)` takes a kept token for that player without asking the server, `LoginAsync(name)` calls the dev login and hands the token to `ApiClient`, `Forget()` drops it. A cancel or an unexpected exception during a login leaves it `LoggedOut`, never `LoggingIn` (CR-0004). A failure is `State == Failed` plus `LastError`, not an exception; only a cancel throws. It never decides that a token is still good: the server does |
| `IAccessTokenStore`, `StoredLogin`, `PlayerPrefsAccessTokenStore`, `NullAccessTokenStore`, `AccessTokenStores` | Where the token lives between runs. `StoredLogin` is the token and the display name it was issued for, nothing else (no snapshot, no balance, no expiry). `AccessTokenStores.For(isDevelopmentBuild)` gives `PlayerPrefs` (keys `CityBuilder.DevLogin.AccessToken` and `.DisplayName`) to the Editor and development players, and a store that keeps nothing to every other build |
| `ContentService`, `ContentMapper`, `ContentLoadResult` | `GET /content` to the engine's `GameContent` plus the server clock's `TimeScale` (not content; below 1 is `CLIENT_INVALID_RESPONSE`): `ContentMapper` copies the answer into `GameContentData` (the one respelling: kind `House` to `house`), `GameContentFactory.Create` validates it exactly as the server does. The result is one of three: the content, a failed request (`ApiError`), or the factory's error list (the answer arrived but is invalid) |
| `SnapshotMapper` | A snapshot answer to the engine's terms (`CitySnapshot`), and `Apply(store, result)`, the one way from an answer to the `ClientCityStore`: a failure returns the error and leaves the store alone. `Replace(store, result)` does the same with `ClientCityStore.Replace`, for the answer right after a new login (CR-0004). The answer has no target level (level + 1 while upgrading) and no next building id (highest id + 1; only the server places buildings). The `settlement` block becomes the snapshot's `SettlementSummary` (M0-53; item and research ids, order as sent). The engine's constructors and `SettlementSummary` check the rest: an impossible city or settlement is `CLIENT_INVALID_RESPONSE` |
| `CommandSender`, `CommandIntent`, `CommandKind` | The one way to give the server a command. `CommandIntent` is what the player wants (`PlaceBuilding`, `UpgradeBuilding`, `AssignWorkers`, `StartResearch` factories; equal kind and arguments are the same intent). `SendAsync(intent, token)` makes the request id once, sends it with every attempt, gives the snapshot of an accepted command to the `ClientCityStore` through `SnapshotMapper.Apply`, and returns a `CommandOutcome`. A listener of `PendingChanged` that throws is logged and leaves the command and its lane unharmed (CR-0004) |
| `CommandPolicy` | The client's behaviour numbers: 3 requests per intent at most, waits of 0.5 s and 1 s between them (the default also holds a 2 s entry for a 4th), stepper debounce 400 ms; `IsRetryable(error)` is the one test of which failure is repeated |
| `PendingCommand`, `CommandPhase`, `CommandOutcome`, `CommandOutcomeKind` | What the UI shows while an intent is on its way: `CommandSender.Pending` (replaced on every change; `PendingChanged` says when; `Completed` is raised once for every intent that ended with the server's answer or an error, after the list let go of it, and not for a replaced target or a cancelled caller: the one place the feedback of M0-47 listens to) lists each intent with its `Phase` (`Waiting`, `Sending`, `Retrying`) and `Attempts`. An intent ends as `Succeeded`, `Failed` (with the last `ApiError`) or `Superseded` (a newer worker target replaced it before it was sent) |
| `ConnectionMonitor` | Polls `GET /city` into the `ClientCityStore` when the `PollScheduler` says so and turns every answer into a `ConnectionState` (`State`, `LastError`, `StateChanged`). `TickAsync` is called every frame (no task allocated when nothing is due; never throws: a cancel ends it quietly and anything unexpected is logged, CR-0004), `SetPaused` pauses and resumes the polling, `RequestPoll` asks for one now, `PollAsync` polls whatever the scheduler says. `StateFor(error, duringLogin)` is the one table from failure to state |
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
- **Commands (M0-40).** One request id per intent, the same on every attempt. Repeated: a lost connection, a timeout, any 5xx (a proxy's page too)
  and `CONCURRENCY_CONFLICT`, at most 3 requests with waits of 0.5 s and 1 s. Never repeated: every other 4xx (also `RATE_LIMITED` and
  `AUTHENTICATION_REQUIRED`, which the player or the login flow handles) and a 200 whose body is unusable. A repeat of a pending intent shares its
  result (a double tap sends once). `AssignWorkers` targets are coalesced per building: a new target replaces a waiting one, is sent after the
  debounce, and waits for the building's earlier target to end. An intent leaves `Pending` before its outcome is reported. Nothing is
  optimistic: only the server's snapshot changes the city.
- **Polling (M0-41).** A usable snapshot of the loaded content is `Connected`. No answer (`CLIENT_NETWORK_ERROR`, `CLIENT_TIMEOUT`) is `Offline`. A 5xx, a proxy's page or an unexpected refusal is `ServerError`. A 2xx answer that is not the contract's, or a snapshot whose `contentVersion` is not the loaded content's (it is not applied), is `IncompatibleVersion`. `AUTHENTICATION_REQUIRED` forgets the token, logs in once and repeats `GET /city` once; a second refusal or a refused login is `Unauthorized` (a login with no answer is `Offline`). A rate-limited poll changes nothing. Every state ends with the next usable answer, so polling never stops. The monitor is built after a successful start and does not poll before it.
- The sender runs on the main thread (its tasks continue there under Unity's context); waits and request ids are injected (`Func`s) so tests
  take no time.
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
- `CommandIntentTests`, `CommandPolicyTests`, `CommandSenderTests` (real `ApiClient`, `FakeTransport`, `FakeDelay`): endpoint and body of each command; the same
  request id on every retry and the waits; 422 and the other refusals not repeated; success after two timeouts applies one snapshot; five rapid
  stepper taps send one command with the final value; the order of two targets of one building; cancellation leaves nothing pending; `Completed` (M0-47) fires once for a success, a refusal and a lost connection, not for a replaced target or a cancel, and a throwing listener is logged.
- `ConnectionMonitorTests` (real `ApiClient`, `FakeTransport`, a fake clock): each answer's state, the single new login and its three requests, a second refusal, a refused or unreachable login, a rate limit, when `StateChanged` fires, the store untouched by other content, ticks that poll only when due, pause and resume, a network that comes back, a cancel.
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
- Only the placement flow uses `CommandSender` for now (the other screens come from M0-49 on). The table from error code to message is `FeedbackMessages` in `CityBuilder.Client.Presentation` (M0-47);
  `ConnectionStatusModel` words the start and the six connection states, not other errors.
- A server that goes down while the device still has a network is found by the next 120 s poll, not sooner. A refused command (`AUTHENTICATION_REQUIRED`) does not yet ask the monitor for a poll: the views of M0-44 on call `RequestPoll`.
- Content that changes while the app runs is not refetched (`05` Versioning): the state is `IncompatibleVersion` until the app restarts.
- An unknown enum value or a missing member fails the whole answer; a server and a client of different versions during M0 need
  matching builds.
- The real transport was walked against a running server once (M0-37 build log); it is not in CI (Unity tests are not).

Last updated: M0-53
