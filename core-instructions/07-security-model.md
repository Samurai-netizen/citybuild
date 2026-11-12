# Security Model

> **Built so far:** the rules the engine enforces (integers only, every mutation checked before it changes anything; M0-08 to M0-17),
> OpenAPI only in Development (tested), and authentication: signed access tokens, the dev login in Development only, `ICurrentPlayer`
> (M0-25, tested) and its 401 answer as Problem Details (M0-28); the command pipeline with its receipts (M0-26, tested with fakes; M0-27, its idempotency on PostgreSQL);
> the first command endpoint, PlaceBuilding, with its body refusing unknown fields (M0-29, tested over HTTP); the API attack review: the body
> size limit, duplicate JSON properties refused, the dev login's strict body, ids checked before the lock, and its `Security_` tests in
> `server/tests/CityBuilder.IntegrationTests/Security/` (M0-30); UpgradeBuilding with the same strict body and building ids resolved in the
> caller's city only, and owner directive OD-13: building ids below 1 and negative coordinates refused before the lock (M0-31, tested over HTTP);
> AssignWorkers with the same rules, negative counts refused before the lock, and the worker-moving property over HTTP (M0-32); StartResearch
> with the same strict body, research ids that can't be content ids refused before the lock, and the research race over HTTP (M0-33); the error catalogue, the global exception handler (no exception text outside Development, no echoed
> headers) and the per-player rate limits (M0-34, tested in memory and on PostgreSQL); the client ↔ server boundary review: the inventory of what
> the client sends and its test, the trace id the server's own, plain HTTP only in development builds (M0-60); the three road commands with
> the same strict body and their tile lists refused before the lock (CR-0008-T1 part D, tested in memory and on PostgreSQL); `start-trip` with
> the same strict body, its ids, item and amount refused before the lock and every building id resolved in the caller's city (part E); `cancel-delivery` and `set-paused` the same way (CR-0008-T3). The
> rest of the `Security_` tests are *(planned)*
> (attack suite M0-74, report M0-78). A task that builds a part updates this line.

**Goal for M0:** a modified client, a replayed request or a wrong clock cannot create resources, skip costs,
skip waiting, over-assign workers, skip research, or touch another player's city.
From Era II this matters even more: one duplication bug floods the shared market and hurts every player.

## Trust boundaries

| Input | Trusted? | Rule |
|---|---|---|
| Access token | Verified | Signed JWT (HMAC-SHA256 only, this server's issuer and audience, lifetime checked against the server clock); `PlayerId` comes only from its `sub` claim (`ICurrentPlayer`) |
| Request body | No | Shape-validated, then only *intent* is read (type id, x, y, building id, worker count, research id, a list of road tiles) |
| Client clock | Never used | Not accepted in any request |
| Presence (CR-0008, built in part H) | Server only | The server records the second of every request that settles the city (`cities.last_seen_unix`) and derives the absence start from it (`04` §2); no request carries a time, a "present" flag or a stamina value |
| Content numbers | Server only | Costs, recipes, labor, research times and effects come from server content |
| Snapshot on the client | Display only | Client prediction is never sent back |

## What the client sends (M0-60)
Every value a request carries, all of it, and what reads it. `ClientInputInventoryTests` holds the body and binding part: every `/api/v1`
endpoint binds exactly these values (read from the framework's own description of the binding), so a new field or a query, route or header
value fails it until this table changes.

| Request | Values the server reads | Read as |
|---|---|---|
| `POST /auth/dev-login` (Development only) | `displayName` | The dev player's name, checked by its pattern; never logged |
| `GET /city`, `GET /content` | nothing | The player from the token |
| `POST /commands/place-building` | `requestId`, `buildingTypeId`, `x`, `y` | Intent: which type, which tile |
| `POST /commands/upgrade-building` | `requestId`, `buildingId` | Intent: which of the caller's buildings |
| `POST /commands/assign-workers` | `requestId`, `buildingId`, `workers` | Intent: the target count for one building; the engine checks free workers and the level's limit |
| `POST /commands/start-research` | `requestId`, `researchId` | Intent: which node |
| `POST /commands/place-roads`, `upgrade-roads`, `remove-roads` (CR-0008-T1 part D) | `requestId`, `tiles` (`x`, `y`) | Intent: which tiles; 1 to 64, each once, coordinates ≥ 0 at the boundary; bounds, occupancy, levels and coins in the engine |
| `POST /commands/start-trip` (CR-0008-T1 part E) | `requestId`, `sourceId`, `targetId`, `itemId`, `amount` | Intent: from which to which of the caller's buildings, which good, how much as a rule (a preset, a percentage, an exact number); the quantity, the worker and the walk are the engine's |
| `POST /commands/set-route` (CR-0008) | `requestId`, `stationId`, `slot`, `stops` (`buildingId`, `actions`: `kind`, `itemId`, `amount`) | Intent: a route; at most 16 stops and 8 actions per stop at the boundary, the content's limits in the engine |
| `POST /commands/reenergize-couriers` (CR-0008) | `requestId` | Intent only: no stamina value is sent |
| `POST /commands/cancel-delivery` (CR-0008-T3) | `requestId`, `stationId`, `slot`, `lineId` (or null for all) | Intent: which of the caller's queued loads to drop; ids below 1 refused before the lock; the station and line resolved in the caller's city |
| `POST /commands/set-paused` (CR-0008-T3) | `requestId`, `buildingId`, `paused` | Intent: pause or resume one of the caller's buildings; an id below 1 or a flag that is no JSON boolean refused before the lock |
| Header `Authorization` | the bearer token | The player id (its `sub`), nothing else |
| Header `X-Client-Version` | a short version text | A log scope value only (`ClientVersionHeader`), `invalid` for anything else; it decides nothing |
| Headers `Content-Type`, `Content-Length`, `Accept` | | JSON only (415), the body limit (400) |
| Headers `traceparent`, `tracestate`, `baggage` | ignored | The trace id is the server's own (`ServerTraceContext`) |

Nothing else is read: no query string, no route value, no cookie. Outside `/api/v1` only `GET /health` (no input) and, in Development only,
`GET /openapi/v1.json` (the document name in its route) answer. The `requestId` is the only id the client makes; it names a command's
receipt within the caller's own player, so it can't reach another player's receipt. No client value is a cost, a rate, a time, a level, a
worker total, a research state or a player id: those come from content and the stored city (`04`), the clock from the server (`TimeProvider`).
On the client the same list is pinned by `RequestDtoTests` (each body's members) and the four `CommandIntent` kinds.

## Threats in M0 and the protection for each

| Threat | Protection | Test |
|---|---|---|
| Send fake costs, balances, levels, labor or timers | These fields don't exist in DTOs; unknown JSON properties are rejected (400, `JsonUnmappedMemberHandling.Disallow`, the dev login's body too), every field is required, numbers must be JSON numbers, a property named twice is refused (`AllowDuplicateProperties = false`) | place-building: `Security_PostWithAFieldTheContractDoesNotHave_Is400AndRunsNothing` (nine fields), `Post_BodyOfTheWrongShape_Is400AndRunsNothing`; on PostgreSQL `Security_PostWithACostField_Is400AndStoresNothing` (M0-29), `Security_PostWithAFieldTheContractDoesNotHave_Is400AndStoresNothing` (cost, coins, level, labor, cycle labor, workers, player, timer; five minutes elapsed, nothing stored), `Security_PropertyTwice_Is400AndRunsNothing`, `Security_DevLoginWithAFieldTheContractDoesNotHave_Is400` (M0-30); upgrade-building: `Security_PostWithAFieldTheContractDoesNotHave_Is400AndRunsNothing` (ten fields: level, target level, costs, time, workers, player, type), `Post_BodyOfTheWrongShape_Is400AndRunsNothing`, on PostgreSQL `Security_PostWithALevelField_Is400AndStoresNothing` (M0-31); assign-workers the same (M0-32); start-research: `StartResearchContractTests.Security_PostWithAFieldTheContractDoesNotHave_Is400AndRunsNothing` (ten fields: cost, duration, start and end, completed, prerequisites, era, slots, player, a list of ids), on PostgreSQL `Security_PostWithADurationField_Is400AndStoresNothing` (M0-33); the road commands: `RoadCommandsContractTests.Security_PostWithAFieldTheContractDoesNotHave_Is400AndRunsNothing` (a level, a cost, a tile outside the list, a player; a tile's level, cost and an unknown member), on PostgreSQL `RoadCommandsDatabaseTests.Security_UpgradeWithALevelField_Is400AndStoresNothing` (CR-0008-T1 part D); the attack suite M0-74. Until M0-34 the 400 has no `code` |
| Change the device clock to speed things up | Server clock only; elapsed time is computed from the DB timestamp | Time-cheat tests with `FakeTimeProvider` |
| Assign more workers than exist, or use workers twice | Server checks free workers and level limits; progress-preserving labor makes moving workers mid-cycle gain nothing; a negative count is refused before the lock (OD-13) | The engine's labor invariant (property tests, M0-15: invariant 11); over HTTP on PostgreSQL (M0-32): `Security_MovingWorkersBetweenBuildings_NeverMakesMoreThanTheLaborAllows` (5 seeds of 40 waits and reassignments between two woodcutters: every answer as the model predicts, never more assigned than provided, and the logs never more than the worker-seconds delivered), `Security_TwoAssignmentsAtOnce_NeverAssignMoreThanProvided`, `Post_MoreThanTheFreeWorkers_Is422InsufficientWorkers`, `Post_AboveTheLevelsMax_Is422WorkerLimit`, `Security_IdBelowOneOrNegativeWorkers_*` |
| Place locked buildings or skip research prerequisites | Unlock and prerequisite checks in engine rules | `BUILDING_LOCKED`, `RESEARCH_LOCKED` tests; over HTTP the trading post before `trade_charter` in `Post_RuleRefuses_AnswersItsCodeAndStoresNothing` (PostgreSQL, M0-29); on PostgreSQL (M0-33) Frame Saw before Sharpened Axes and Storehouses before Trade Charter: `Post_PrerequisiteMissing_Is422ResearchLocked`; the trading post refused one second before Trade Charter completes and placed at that second: `Post_TradeCharter_MakesTheTradingPostPlaceableWhenItCompletes` |
| Start two researches by racing | Row lock on the city; a single research slot is checked inside the lock | `Security_TwoStartsAtOnce_StartExactlyOneResearch` (PostgreSQL, M0-33: two nodes, both unlocked and affordable, at once: one starts, the other is `RESEARCH_IN_PROGRESS`, one cost and one receipt stored); `Post_WhileAResearchRuns_Is409ResearchInProgress`; the concurrency suite M0-74 |
| Replay a successful command | `(playerId, requestId)` receipt with a unique constraint | Replay returns the same response, ledger unchanged: `ExecuteAsync_SameRequestAgain_ReturnsTheStoredResponse`, `ExecuteAsync_IdenticalRequestCommittedFirst_ReturnsItsResponse` (Application); on PostgreSQL `ExecuteAsync_SameRequestAgain_ReturnsTheIdenticalResponseAndWritesNothing`, `ExecuteAsync_SameRequestAfterAHostRestart_ReturnsTheStoredResponse`, `ExecuteAsync_TenIdenticalRequestsAtOnce_ApplyOnceAndAllGetTheSameResponse`, `ExecuteAsync_IdenticalReceiptCommittedWhileItRuns_RollsBackAndReturnsThatResponse` (M0-27); over HTTP `Post_SameRequestAgain_ReturnsTheSameBytesAndWritesNothing`, `Post_TenIdenticalRequestsAtOnce_PlaceOneBuilding` (PostgreSQL, M0-29); a replay storm, 20 at once and 30 one by one, applies once and stores nothing after the first: `Security_ReplayStorm_AppliesTheCommandOnce` (PostgreSQL, M0-30) |
| Same `requestId`, different payload | Payload hash comparison (the command type is part of the payload) | `IDEMPOTENCY_CONFLICT`: `ExecuteAsync_SameRequestIdOtherPayload_IsAConflict`, `ExecuteAsync_SameRequestIdOtherCommand_IsAConflict`; on PostgreSQL the same two `…AndWritesNothing` and `ExecuteAsync_ReceiptOfAnotherPayloadCommittedWhileItRuns_RollsBackAndIsAConflict` (M0-27) |
| Read another player's response with their `requestId` | The receipt's key includes the player, from the token | `Security_AnotherPlayersRequestId_FindsNoReceipt`, `ExecuteAsync_AnotherPlayersRequestId_RunsAsNew`, `Security_AnotherPlayersRequestId_NeverMatches` (PostgreSQL, M0-27); over HTTP `Security_AnotherPlayersRequestId_RunsAsANewCommandInTheirOwnCity` (M0-29), for the road commands `RoadCommandsDatabaseTests.Security_AnotherPlayersRequestId_RunsForTheCallerOnly` (CR-0008-T1 part D) |
| A command that writes without the receipt or the settle | Commands implement `ICityCommand` and see only the city state and content; only `SettleCity` and `CommandPipeline` hold the city transaction | `Types_OfApplication_OnlyTheSettleAndThePipelineDependOnTheRunner` |
| Race two commands (double spend) | `FOR UPDATE` row lock + `state_version` guard | Concurrent integration tests; two road placements on one tile at once place one road and pay once: `RoadCommandsDatabaseTests.Security_TwoRequestsForOneTileAtOnce_PlaceItOnce` (CR-0008-T1 part D) |
| Act on another player's building | Building ids are resolved inside the caller's city only | `BUILDING_NOT_FOUND` (404), never a `FORBIDDEN` leak: `Security_AnotherPlayersBuildingId_Is404AndTouchesNeitherCity` (upgrade-building on PostgreSQL: the id exists only in the other city; neither city changes), and ids of 1 or more the city doesn't have are 404 too (`Post_IdNotInTheCity_Is404AndStoresNothing`, `Post_RuleRefuses_IsProblemWithTheRulesCode`); an id below 1 is 400 before the lock (`Security_BuildingIdBelowOne_Is400BeforeTheCityIsLocked`, `Security_BuildingIdBelowOne_Is400AndStoresNothing`; M0-31, OD-13). Assign-workers does the same (M0-32: `AssignWorkersDatabaseTests.Security_AnotherPlayersBuildingId_Is404AndTouchesNeitherCity`; ids below 1 in `Security_IdBelowOneOrNegativeWorkers_Is400BeforeTheCityIsLocked` and `…_Is400AndStoresNothing`) |
| Place outside the grid to wrap or clip a stored tile | A negative coordinate is refused before the city is locked (OD-13); the engine's bounds check runs before the mutation; coordinates are `int` in the body and the stored `smallint` columns only ever get grid values | negative values, down to the `int` limit: `INVALID_ARGUMENT`, the lock never taken, nothing saved: `Security_NegativeCoordinates_Are400BeforeTheCityIsLocked` (in memory), `Security_NegativeCoordinates_Are400AndStoreNothing` (PostgreSQL, five minutes elapsed, M0-31); values past the grid, up to the `int` and past the `smallint` limits: `OUT_OF_BOUNDS`, nothing saved: `Security_CoordinatesPastTheGrid_AreOutOfBoundsAndSaveNothing`, `Security_CoordinatesPastTheGrid_AreOutOfBoundsAndStoreNothing` (M0-30); road tiles: a negative coordinate is in the tile-list refusals below, a tile past the grid is `OUT_OF_BOUNDS` (placing) or `ROAD_NOT_FOUND` (`RoadCommandsContractTests.Post_RuleRefuses_IsProblemWithTheRulesCode`, CR-0008-T1 part D) |
| Spam requests or settle storms | Per-player rate limits (`RateLimits:*`, `05` → Rate limits): commands 10/s and 120/min, `GET /city` and `GET /content` 30/min each, the dev login 10/min per address; a refused request is never bound or locked | `Post_CommandsPastTheBurstLimit_Are429BeforeTheCityIsLocked`, `Post_CommandsPastTheSustainedLimit_Are429`, `Post_AllCommandEndpoints_CountTogether`, `Get_CityPastItsLimit_Is429BeforeTheCityIsLocked`, `Get_ContentPastItsLimit_Is429AndLeavesTheOtherBudgetsAlone`, `Security_OnePlayersSpam_DoesNotBlockAnotherOrTheirOtherBudget`, `Security_RequestsWithoutToken_Are401NotCounted`, `Post_DevLoginPastTheLimit_Is429PerAddress`, `Defaults_NormalPlay_IsNeverLimited_AndTheContractsNumbersHold`, `Security_EveryApiEndpoint_IsInItsLimitClass` (M0-34) |
| Huge or malformed bodies | `RequestLimits:MaxBodyBytes` (4 KB): Kestrel reads no more, and a larger `Content-Length` is 400 `INVALID_ARGUMENT` before the token check; strict binding before DB access; JSON only (415); an id that can't be a content id (`ContentIds`: a type id, a research id), a building id below 1 and a negative coordinate are `INVALID_ARGUMENT` before the city is locked (OD-13) | place-building: `Post_BodyOfTheWrongShape_Is400AndRunsNothing` (broken JSON, wrong types, an `int` overflow, a missing field; M0-29); M0-30: `Security_BodyLargerThanTheLimit_Is400BeforeAnythingRuns`, `Security_BodyOfExactlyTheLimit_IsRead`, `Security_KestrelBodyLimit_IsTheSetting`, `Security_RequestIdThatIsNoUuid_Is400AndRunsNothing`, `Security_MalformedJson_Is400AndRunsNothing`, `Security_DeeplyNestedJson_Is400AndRunsNothing`, `Security_TypeIdThatCantBeAContentId_Is400BeforeTheCityIsLocked`, `Security_ResearchIdThatCantBeAContentId_Is400BeforeTheCityIsLocked` (M0-33; on PostgreSQL `…_Is400AndStoresNothing`), `Security_BodyNotSentAsJson_Is415AndRunsNothing`, `Security_DevLoginHugeDisplayName_Is400InvalidArgument`, `Request_BodyStoppedByKestrel_Is400InvalidArgumentNot413` and `Throw_BadRequest_Is400InvalidArgument` (M0-34); on PostgreSQL `Security_BodyOfTheWrongForm_Is400AndStoresNothing`, `Security_MegabyteBody_Is400AndStoresNothing`, `Security_DevLoginRefusedBody_StoresNoPlayer` |
| Set goods by hand | There is no endpoint or client code for it (`02` rule 5). The only way is `server-panel/goods.sql`, a developer's script that runs `psql` inside the local Docker database container; `panel.sh` passes one player (the dev name the Unity client logs in as, or `$PLAYER`, checked against `^[A-Za-z0-9_.-]{1,32}$`, one city only) and an item id from the content file, the script checks mode, amount and item id again, locks the city row and writes a `DEV_ADJUST` ledger row. A production database is never reached by it (no connection string, only `docker compose exec postgres`) | `GoodsPanelScriptTests` (PostgreSQL, CR-0006): every mode with its ledger row, the other player untouched, six refusals that write nothing; `server-panel/test-goods.sh` (arguments, the fake `docker`) |
| Dev login in production | Endpoint mapped only in Development with `Auth:DevLoginEnabled` on; the setting on outside Development stops the start | `Security_DevLoginInProduction_IsNotMapped` (404), `Security_DevLoginEnabledOutsideDevelopment_StopsTheStart` |
| Act as another player without their token | Every `/api/v1` endpoint requires a token (route group); the player comes only from the token's `sub`, never from a body | `GetCurrentPlayer_TokenTheServerIssued_ReturnsItsPlayer`, `Security_RequestWithoutToken_IsRefused`; on the real endpoints, as the 401 `AUTHENTICATION_REQUIRED` Problem Details: `Get_WithoutToken_ReturnsAuthenticationRequiredProblem` (M0-28), `Post_PlaceBuildingWithoutToken_ReturnsAuthenticationRequiredProblem` (M0-29), `Post_RoadCommandWithoutToken_ReturnsAuthenticationRequiredProblem` (CR-0008-T1 part D); on PostgreSQL `Security_PostWithoutToken_Is401AndStoresNothing`, and a player named in the query or a header is ignored: `Security_PlayerNamedOutsideTheToken_IsIgnored` (M0-30) |
| Use a token that names no player, or one whose player is gone | A signed token without a player id in `sub` fails at the token check; a player without a city fails at the city lock. Both are the same 401, so the client logs in again, and neither is a 500 until the token expires. A refused token is logged as a kind of failure (`NoCity`), never as an error | `Security_SignedTokenWithoutAPlayerId_IsRefused`, `Security_Get_City_OfAPlayerWithoutACity_IsAuthenticationRequired`, `Security_Post_Command_OfAPlayerWithoutACity_IsAuthenticationRequired`; on PostgreSQL `Security_TokenOfAPlayerWithoutACity_Is401ThenANewLoginWorks` (CR-0004) |
| Forge, alter or replay an old token | Signature checked with the server's key, HS256 only (unsigned tokens refused), issuer and audience checked, expiry by the server clock with no skew | `Security_TokenWithWrongSignature_IsRefused`, `Security_UnsignedToken_IsRefused`, `Security_TokenOfAnotherIssuer_IsRefused`, `Security_ExpiredToken_IsRefused`, `Security_MalformedToken_IsRefused`; every kind of refusal gets the same answer, which never says why: `Security_EveryRefusal_GetsTheSameAnswer` (M0-28) |
| Weak or committed signing key | `Auth:SigningKey` required in every environment, at least 32 bytes, from user-secrets or the environment; the start stops otherwise | `Start_NoOrShortSigningKey_FailsValidation` |
| Build-time placeholders reaching a server (M0-61) | The build-time OpenAPI run (`OpenApi/BuildTimeDocument`) starts `Program.cs` with a placeholder connection string and signing key only when the entry assembly is the OpenAPI package's tool, `GetDocument.Insider`; that process never listens or serves, so the placeholder key signs nothing | `Security_BuildTimeDocumentSettings_AreNotUsedByAServer` |
| Two first logins of one name create two players | `unique(dev_name)`; the second insert fails, and the login returns the first player | `PostDevLogin_SameNameCommittedFirstByAnotherRequest_ReturnsThatPlayer` |
| Dev time scale left on | `Simulation:DevTimeScale` is honoured only in Development; startup throws if ≠ 1 elsewhere (the options validation), and the clock is built scaled only in Development (`ServerClock`) | `Security_DevTimeScaleOutsideDevelopment_StopsTheStart`, `Security_Production_ClockIsRealAndTimeScaleIsOne` (M0-35) |
| Overflow to wrap quantities | `checked` arithmetic, `long`, content validation of maxima | Overflow tests |
| Keep couriers awake with a script that polls (CR-0008, part H) | Known limitation, accepted in M0 (CR-0008 D8): any request counts as presence, like an open game; it gains output only while goods have nowhere better to go than the player's own city. Revisit with the market (M2): for example a daily limit on awake time, or device attestation (M3) | Presence tests: no stamina spent before the absence start, the absence start from the last request plus the grace (part H) |
| Send a huge route, tile list or amount to slow the server (CR-0008; the tile list built in part D, amounts in part E, routes in part G) | Boundary limits before the lock (64 tiles, each once, coordinates ≥ 0; 16 stops, 8 actions per stop, amounts of at most 12 digits or 1–100 %, ids ≥ 1, `slot` ≥ 1) and the 4 KB body limit; the content's own limits (`maxRouteStops`, `maxStopActions`) in the engine; every new command refuses these as `INVALID_ARGUMENT` before the city is locked (OD-13) | Tile lists (none, 65, a tile twice, a negative coordinate, a null tile or list) for each road command: `RoadCommandsContractTests.Security_TileListNoCityCouldTake_Is400BeforeTheCityIsLocked` (the lock never taken), on PostgreSQL `RoadCommandsDatabaseTests.Security_TileListNoCityCouldTake_Is400AndStoresNothing` (five minutes elapsed, not even the settle stored) (part D); `StartTripContractTests.Security_BodyNoCityCouldTake_Is400BeforeTheCityIsLocked` (ids, the same building twice, an item id's form, every malformed amount) and `Security_BodyOutsideTheContract_Is400AndRunsNothing` (part E); one such test per boundary rule of the later commands (parts G, H) |
| Move goods out of or into another player's city | Every building id of a trip or a route is resolved inside the caller's city only (`BUILDING_NOT_FOUND`), like the other commands | `StartTripDatabaseTests.Security_AnotherPlayersBuildingId_Is404AndTouchesNeitherCity` on PostgreSQL (part E); the same for `set-route` (part G) |
| Secret leakage | Signing key and the database connection string from environment/user-secrets; never in Unity or the repo (the compose password lives in the gitignored `.env`) | Config review prompt |
| Information leakage in errors | Every error is the catalogue's Problem Details; the global handler gives no stack trace anywhere and no exception text outside Development (its type and message in `detail` there); the developer exception page, which echoed the request headers and so the bearer token, never answers; the 401 never says why | `Security_Throw_InProduction_SaysNothingAboutTheException`, `Throw_Unexpected_IsInternalErrorAndDevelopmentNamesTheException`, `Security_BodyThatDoesNotBind_InDevelopment_DoesNotEchoTheRequestHeaders`, `Post_BodyThatDoesNotBind_IsInvalidArgument`, `ErrorCatalogueTests` (M0-34) |
| A shared cache keeps a player's answer; the answers name the server software | Every answer has `Cache-Control: no-store` and `X-Content-Type-Options: nosniff`, also an error answer; Kestrel's `Server` header is off | `Security_EveryAnswer_ForbidsCachingAndSniffing`, `Security_AnswerToAnException_ForbidsCachingAndSniffing`, `Security_KestrelServerHeader_IsOff` (CR-0004) |
| Choose the trace id the server logs and answers with (reuse another request's, put text into the trace) | `ServerTraceContext`: the server reads no `traceparent`, `tracestate` or `baggage` header, so every request starts a new trace of the server's own (M0-60) | `Security_TraceContextFromTheClient_IsIgnored` (the answer's `traceId` and every log line: none has the client's trace id, span id, state or baggage) |
| Read the token off the network | A release build talks https only: `ApiClient` refuses an `http://` address unless the build is the Editor or a development player (`GameRoot` passes `Debug.isDebugBuild`), and the Player setting "Allow downloads over HTTP" is development builds only. No redirect is followed (CR-0004) | `ApiClientTests.AnHttpBaseUrl_WithoutTheAllowance_IsRefused`, `…_WithTheAllowance_BuildsAClient`, `AnHttpsBaseUrl_IsAcceptedInEveryBuild`, `InsecureHttpSettingTests` (M0-60) |
| Take the token from a release build's storage | Only the Editor and development players keep the token (`PlayerPrefs`, `AccessTokenStores.For(Debug.isDebugBuild)`); a release build keeps it in memory only and never writes it | `AccessTokenStoreTests.For_AReleaseBuild_KeepsNothing`, `For_ADevelopmentBuild_IsThePlayerPrefsStore` |
| Probe server internals through `/health` (no auth) | The response is the status word only: no check details, versions or exception text | `GetHealth_AppRunning_Returns200WithStatusOnly` |
| Read the full API description in production | OpenAPI (`/openapi/v1.json`) is mapped only in Development | `Security_OpenApiDocument_InProduction_IsNotServed` |

## Future threats (design now, build with the market and guilds — see `26`)
| Threat | Planned protection |
|---|---|
| Duplicating goods through market races | Per-item advisory lock, escrow, idempotent orders, ledger auditor, per-item kill switch |
| Alt accounts funneling wealth to a main | Era II + account-age gate, price bands, trade-graph monitoring, no direct gifting |
| Real-money trading | Terms of service, large one-sided trade alerts, bans |
| Market bots and order spam | Order limits, listing fee, rate limits, time-priority matching |
| Account theft | Real accounts with platform sign-in (M1), session revocation |

## Logging policy
Log: player id, request id, trace id, command type, result code, duration, settlement size.
Never log: tokens, signing keys, full request bodies in production, personal data.
- As built (M0-35): no request body is logged in any environment; a 401 names only the kind of failure (never the token or the
  validation message); the dev login logs the player, never the display name; a rate-limit hit on the dev login names no address. Every
  event and its values: `docs/api/logs-and-correlation.md`. Tests: `Security_RefusedToken_LogsTheKindOfFailureNeverTheToken`,
  `Security_DevLoginRefused_LogsTheCodeNotTheName`, `Security_DevLoginPastItsLimit_LogsNoAddress`, `Security_CommandRequest_LogsNoBody`,
  `Security_PostDevLogin_LogsThePlayerNeverTheNameOrToken`.

- As built (M0-58): the client's `X-Client-Version` is client-controlled text that goes into every log line of the request, so only a short run
  of plain characters is kept (`ClientVersionHeader`); anything else is logged as `invalid`, never as the text. Tests: `Security_Get_WithUnusableClientVersion_IsServedAndLoggedAsInvalid`.

- As built on the client (M0-57): every line goes through `ClientLog` (`LoggingPolicyTests` fails on a direct `Debug.Log*`). A request is logged by method,
  path, status and duration only, a failed one with its error code and the server's trace id; no token and no body, in any build, so a release
  build (level Warning) writes the failures and nothing of the traffic. Tests: `ApiClientLoggingTests`.

- Reviewed (M0-60): every server log call and every client `ClientLog` call; none writes a token, a key, a body or a display name. A failed
  request's client line has the server's `code`, `detail` and `traceId`, and an answer that isn't Problem Details is never copied into it
  (`ApiResponseParser`). The trace id in every server line is the server's own (`ServerTraceContext`), so a client can't plant one.

## Known limitations in M0 (document, don't hide)
- Dev auth only: no real account system and no token revocation; a token stays valid until it expires (12 h by default), and changing
  `Auth:SigningKey` is the only way to end every token at once.
- No device attestation (Play Integrity / App Attest). A scripted client can automate play, but only within server rules,
  so it gains no more than a perfect human player would. That's acceptable for M0, but not once the market exists.
- Rate limits are in-memory and single-instance (M0-34): several server instances would each allow the full budget, and a restart resets
  it. Fixed windows allow up to twice a limit across a window boundary. `Retry-After` is the window's length, an upper bound. The dev login is counted per remote address: behind a reverse proxy that is the proxy's until forwarded
  headers are configured, and an IPv6 client can rotate addresses inside its prefix; the dev login exists in Development only. A replay
  storm below the limits still takes the player's own city lock and reads the receipt (it settles and writes nothing), so it slows only
  that player's city.
- A body without a `Content-Length` (chunked) is stopped by Kestrel at the size limit with an empty 413 that no exception reaches the
  handler with; the status code pages rewrite it to 400 `INVALID_ARGUMENT`, as they do the answers to a body that doesn't bind (an unknown,
  missing, mistyped or repeated field, broken JSON; the handler) (M0-34). The in-memory test server has no body limit of its own, so a
  chunked body against Kestrel is tested by hand only (M0-34 build log: 20 000 bytes and 1 MiB, both 400), and in the suite as the empty 413 the
  rewrite answers: `Request_BodyStoppedByKestrel_Is400InvalidArgumentNot413`.
- Plain HTTP is allowed in the Editor and development builds only (any address there, also a LAN one for a phone); a release build needs an
  https address, and the shipped settings asset names the dev server (`http://localhost:8080`), so a release build shows the bad-settings
  screen until M1 gives it a real server. The server itself has no TLS settings: in M0 it runs locally, and a production deployment puts TLS
  in front of it *(planned, M1)*.
- The token kept by a development build is plain text in `PlayerPrefs` (the registry on Windows, a plist on macOS, shared preferences on
  Android): anyone with the device can read it. Development builds only, and a dev token of a dev player.
- A client may send any `requestId` it likes; one it reuses for another payload is `IDEMPOTENCY_CONFLICT`, and it can only reach its own
  receipts.

## Rule for Claude
When adding an endpoint or command, add its rows to the threat table above and a test for each.
Never describe the system as "unhackable".
