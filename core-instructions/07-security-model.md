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
> headers) and the per-player rate limits (M0-34, tested in memory and on PostgreSQL). The rest of the `Security_` tests are *(planned)*
> (attack suite M0-74, report M0-78). A task that builds a part updates this line.

**Goal for M0:** a modified client, a replayed request or a wrong clock cannot create resources, skip costs,
skip waiting, over-assign workers, skip research, or touch another player's city.
From Era II this matters even more: one duplication bug floods the shared market and hurts every player.

## Trust boundaries

| Input | Trusted? | Rule |
|---|---|---|
| Access token | Verified | Signed JWT (HMAC-SHA256 only, this server's issuer and audience, lifetime checked against the server clock); `PlayerId` comes only from its `sub` claim (`ICurrentPlayer`) |
| Request body | No | Shape-validated, then only *intent* is read (type id, x, y, building id, worker count, research id) |
| Client clock | Never used | Not accepted in any request |
| Content numbers | Server only | Costs, recipes, labor, research times and effects come from server content |
| Snapshot on the client | Display only | Client prediction is never sent back |

## Threats in M0 and the protection for each

| Threat | Protection | Test |
|---|---|---|
| Send fake costs, balances, levels, labor or timers | These fields don't exist in DTOs; unknown JSON properties are rejected (400, `JsonUnmappedMemberHandling.Disallow`, the dev login's body too), every field is required, numbers must be JSON numbers, a property named twice is refused (`AllowDuplicateProperties = false`) | place-building: `Security_PostWithAFieldTheContractDoesNotHave_Is400AndRunsNothing` (nine fields), `Post_BodyOfTheWrongShape_Is400AndRunsNothing`; on PostgreSQL `Security_PostWithACostField_Is400AndStoresNothing` (M0-29), `Security_PostWithAFieldTheContractDoesNotHave_Is400AndStoresNothing` (cost, coins, level, labor, cycle labor, workers, player, timer; five minutes elapsed, nothing stored), `Security_PropertyTwice_Is400AndRunsNothing`, `Security_DevLoginWithAFieldTheContractDoesNotHave_Is400` (M0-30); upgrade-building: `Security_PostWithAFieldTheContractDoesNotHave_Is400AndRunsNothing` (ten fields: level, target level, costs, time, workers, player, type), `Post_BodyOfTheWrongShape_Is400AndRunsNothing`, on PostgreSQL `Security_PostWithALevelField_Is400AndStoresNothing` (M0-31); assign-workers the same (M0-32); start-research: `StartResearchContractTests.Security_PostWithAFieldTheContractDoesNotHave_Is400AndRunsNothing` (ten fields: cost, duration, start and end, completed, prerequisites, era, slots, player, a list of ids), on PostgreSQL `Security_PostWithADurationField_Is400AndStoresNothing` (M0-33); the attack suite M0-74. Until M0-34 the 400 has no `code` |
| Change the device clock to speed things up | Server clock only; elapsed time is computed from the DB timestamp | Time-cheat tests with `FakeTimeProvider` |
| Assign more workers than exist, or use workers twice | Server checks free workers and level limits; progress-preserving labor makes moving workers mid-cycle gain nothing; a negative count is refused before the lock (OD-13) | The engine's labor invariant (property tests, M0-15: invariant 11); over HTTP on PostgreSQL (M0-32): `Security_MovingWorkersBetweenBuildings_NeverMakesMoreThanTheLaborAllows` (5 seeds of 40 waits and reassignments between two woodcutters: every answer as the model predicts, never more assigned than provided, and the logs never more than the worker-seconds delivered), `Security_TwoAssignmentsAtOnce_NeverAssignMoreThanProvided`, `Post_MoreThanTheFreeWorkers_Is422InsufficientWorkers`, `Post_AboveTheLevelsMax_Is422WorkerLimit`, `Security_IdBelowOneOrNegativeWorkers_*` |
| Place locked buildings or skip research prerequisites | Unlock and prerequisite checks in engine rules | `BUILDING_LOCKED`, `RESEARCH_LOCKED` tests; over HTTP the trading post before `trade_charter` in `Post_RuleRefuses_AnswersItsCodeAndStoresNothing` (PostgreSQL, M0-29); on PostgreSQL (M0-33) Frame Saw before Sharpened Axes and Storehouses before Trade Charter: `Post_PrerequisiteMissing_Is422ResearchLocked`; the trading post refused one second before Trade Charter completes and placed at that second: `Post_TradeCharter_MakesTheTradingPostPlaceableWhenItCompletes` |
| Start two researches by racing | Row lock on the city; a single research slot is checked inside the lock | `Security_TwoStartsAtOnce_StartExactlyOneResearch` (PostgreSQL, M0-33: two nodes, both unlocked and affordable, at once: one starts, the other is `RESEARCH_IN_PROGRESS`, one cost and one receipt stored); `Post_WhileAResearchRuns_Is409ResearchInProgress`; the concurrency suite M0-74 |
| Replay a successful command | `(playerId, requestId)` receipt with a unique constraint | Replay returns the same response, ledger unchanged: `ExecuteAsync_SameRequestAgain_ReturnsTheStoredResponse`, `ExecuteAsync_IdenticalRequestCommittedFirst_ReturnsItsResponse` (Application); on PostgreSQL `ExecuteAsync_SameRequestAgain_ReturnsTheIdenticalResponseAndWritesNothing`, `ExecuteAsync_SameRequestAfterAHostRestart_ReturnsTheStoredResponse`, `ExecuteAsync_TenIdenticalRequestsAtOnce_ApplyOnceAndAllGetTheSameResponse`, `ExecuteAsync_IdenticalReceiptCommittedWhileItRuns_RollsBackAndReturnsThatResponse` (M0-27); over HTTP `Post_SameRequestAgain_ReturnsTheSameBytesAndWritesNothing`, `Post_TenIdenticalRequestsAtOnce_PlaceOneBuilding` (PostgreSQL, M0-29); a replay storm, 20 at once and 30 one by one, applies once and stores nothing after the first: `Security_ReplayStorm_AppliesTheCommandOnce` (PostgreSQL, M0-30) |
| Same `requestId`, different payload | Payload hash comparison (the command type is part of the payload) | `IDEMPOTENCY_CONFLICT`: `ExecuteAsync_SameRequestIdOtherPayload_IsAConflict`, `ExecuteAsync_SameRequestIdOtherCommand_IsAConflict`; on PostgreSQL the same two `…AndWritesNothing` and `ExecuteAsync_ReceiptOfAnotherPayloadCommittedWhileItRuns_RollsBackAndIsAConflict` (M0-27) |
| Read another player's response with their `requestId` | The receipt's key includes the player, from the token | `Security_AnotherPlayersRequestId_FindsNoReceipt`, `ExecuteAsync_AnotherPlayersRequestId_RunsAsNew`, `Security_AnotherPlayersRequestId_NeverMatches` (PostgreSQL, M0-27); over HTTP `Security_AnotherPlayersRequestId_RunsAsANewCommandInTheirOwnCity` (M0-29) |
| A command that writes without the receipt or the settle | Commands implement `ICityCommand` and see only the city state and content; only `SettleCity` and `CommandPipeline` hold the city transaction | `Types_OfApplication_OnlyTheSettleAndThePipelineDependOnTheRunner` |
| Race two commands (double spend) | `FOR UPDATE` row lock + `state_version` guard | Concurrent integration tests |
| Act on another player's building | Building ids are resolved inside the caller's city only | `BUILDING_NOT_FOUND` (404), never a `FORBIDDEN` leak: `Security_AnotherPlayersBuildingId_Is404AndTouchesNeitherCity` (upgrade-building on PostgreSQL: the id exists only in the other city; neither city changes), and ids of 1 or more the city doesn't have are 404 too (`Post_IdNotInTheCity_Is404AndStoresNothing`, `Post_RuleRefuses_IsProblemWithTheRulesCode`); an id below 1 is 400 before the lock (`Security_BuildingIdBelowOne_Is400BeforeTheCityIsLocked`, `Security_BuildingIdBelowOne_Is400AndStoresNothing`; M0-31, OD-13). Assign-workers does the same (M0-32: `AssignWorkersDatabaseTests.Security_AnotherPlayersBuildingId_Is404AndTouchesNeitherCity`; ids below 1 in `Security_IdBelowOneOrNegativeWorkers_Is400BeforeTheCityIsLocked` and `…_Is400AndStoresNothing`) |
| Place outside the grid to wrap or clip a stored tile | A negative coordinate is refused before the city is locked (OD-13); the engine's bounds check runs before the mutation; coordinates are `int` in the body and the stored `smallint` columns only ever get grid values | negative values, down to the `int` limit: `INVALID_ARGUMENT`, the lock never taken, nothing saved: `Security_NegativeCoordinates_Are400BeforeTheCityIsLocked` (in memory), `Security_NegativeCoordinates_Are400AndStoreNothing` (PostgreSQL, five minutes elapsed, M0-31); values past the grid, up to the `int` and past the `smallint` limits: `OUT_OF_BOUNDS`, nothing saved: `Security_CoordinatesPastTheGrid_AreOutOfBoundsAndSaveNothing`, `Security_CoordinatesPastTheGrid_AreOutOfBoundsAndStoreNothing` (M0-30) |
| Spam requests or settle storms | Per-player rate limits (`RateLimits:*`, `05` → Rate limits): commands 10/s and 120/min, `GET /city` 30/min, the dev login 10/min per address; a refused request is never bound or locked | `Post_CommandsPastTheBurstLimit_Are429BeforeTheCityIsLocked`, `Post_CommandsPastTheSustainedLimit_Are429`, `Post_AllCommandEndpoints_CountTogether`, `Get_CityPastItsLimit_Is429BeforeTheCityIsLocked`, `Security_OnePlayersSpam_DoesNotBlockAnotherOrTheirOtherBudget`, `Security_RequestsWithoutToken_Are401NotCounted`, `Post_DevLoginPastTheLimit_Is429PerAddress`, `Defaults_NormalPlay_IsNeverLimited_AndTheContractsNumbersHold`, `Security_EveryApiEndpoint_IsInItsLimitClass` (M0-34) |
| Huge or malformed bodies | `RequestLimits:MaxBodyBytes` (4 KB): Kestrel reads no more, and a larger `Content-Length` is 400 `INVALID_ARGUMENT` before the token check; strict binding before DB access; JSON only (415); an id that can't be a content id (`ContentIds`: a type id, a research id), a building id below 1 and a negative coordinate are `INVALID_ARGUMENT` before the city is locked (OD-13) | place-building: `Post_BodyOfTheWrongShape_Is400AndRunsNothing` (broken JSON, wrong types, an `int` overflow, a missing field; M0-29); M0-30: `Security_BodyLargerThanTheLimit_Is400BeforeAnythingRuns`, `Security_BodyOfExactlyTheLimit_IsRead`, `Security_KestrelBodyLimit_IsTheSetting`, `Security_RequestIdThatIsNoUuid_Is400AndRunsNothing`, `Security_MalformedJson_Is400AndRunsNothing`, `Security_DeeplyNestedJson_Is400AndRunsNothing`, `Security_TypeIdThatCantBeAContentId_Is400BeforeTheCityIsLocked`, `Security_ResearchIdThatCantBeAContentId_Is400BeforeTheCityIsLocked` (M0-33; on PostgreSQL `…_Is400AndStoresNothing`), `Security_BodyNotSentAsJson_Is415AndRunsNothing`, `Security_DevLoginHugeDisplayName_Is400InvalidArgument`; on PostgreSQL `Security_BodyOfTheWrongForm_Is400AndStoresNothing`, `Security_MegabyteBody_Is400AndStoresNothing`, `Security_DevLoginRefusedBody_StoresNoPlayer` |
| Dev login in production | Endpoint mapped only in Development with `Auth:DevLoginEnabled` on; the setting on outside Development stops the start | `Security_DevLoginInProduction_IsNotMapped` (404), `Security_DevLoginEnabledOutsideDevelopment_StopsTheStart` |
| Act as another player without their token | Every `/api/v1` endpoint requires a token (route group); the player comes only from the token's `sub`, never from a body | `GetCurrentPlayer_TokenTheServerIssued_ReturnsItsPlayer`, `Security_RequestWithoutToken_IsRefused`; on the real endpoints, as the 401 `AUTHENTICATION_REQUIRED` Problem Details: `Get_WithoutToken_ReturnsAuthenticationRequiredProblem` (M0-28), `Post_PlaceBuildingWithoutToken_ReturnsAuthenticationRequiredProblem` (M0-29); on PostgreSQL `Security_PostWithoutToken_Is401AndStoresNothing`, and a player named in the query or a header is ignored: `Security_PlayerNamedOutsideTheToken_IsIgnored` (M0-30) |
| Forge, alter or replay an old token | Signature checked with the server's key, HS256 only (unsigned tokens refused), issuer and audience checked, expiry by the server clock with no skew | `Security_TokenWithWrongSignature_IsRefused`, `Security_UnsignedToken_IsRefused`, `Security_TokenOfAnotherIssuer_IsRefused`, `Security_ExpiredToken_IsRefused`, `Security_MalformedToken_IsRefused`; every kind of refusal gets the same answer, which never says why: `Security_EveryRefusal_GetsTheSameAnswer` (M0-28) |
| Weak or committed signing key | `Auth:SigningKey` required in every environment, at least 32 bytes, from user-secrets or the environment; the start stops otherwise | `Start_NoOrShortSigningKey_FailsValidation` |
| Two first logins of one name create two players | `unique(dev_name)`; the second insert fails, and the login returns the first player | `PostDevLogin_SameNameCommittedFirstByAnotherRequest_ReturnsThatPlayer` |
| Dev time scale left on | `Simulation:DevTimeScale` is honoured only in Development; startup throws if ≠ 1 elsewhere | Startup-guard test |
| Overflow to wrap quantities | `checked` arithmetic, `long`, content validation of maxima | Overflow tests |
| Secret leakage | Signing key and the database connection string from environment/user-secrets; never in Unity or the repo (the compose password lives in the gitignored `.env`) | Config review prompt |
| Information leakage in errors | Every error is the catalogue's Problem Details; the global handler gives no stack trace anywhere and no exception text outside Development (its type and message in `detail` there); the developer exception page, which echoed the request headers and so the bearer token, never answers; the 401 never says why | `Security_Throw_InProduction_SaysNothingAboutTheException`, `Throw_Unexpected_IsInternalErrorAndDevelopmentNamesTheException`, `Security_BodyThatDoesNotBind_InDevelopment_DoesNotEchoTheRequestHeaders`, `Post_BodyThatDoesNotBind_IsInvalidArgument`, `ErrorCatalogueTests` (M0-34) |
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

## Known limitations in M0 (document, don't hide)
- Dev auth only: no real account system and no token revocation; a token stays valid until it expires (12 h by default), and changing
  `Auth:SigningKey` is the only way to end every token at once.
- No device attestation (Play Integrity / App Attest). A scripted client can automate play, but only within server rules,
  so it gains no more than a perfect human player would. That's acceptable for M0, but not once the market exists.
- Rate limits are in-memory and single-instance (M0-34): several server instances would each allow the full budget, and a restart resets
  it. Fixed windows allow up to twice a limit across a window boundary. `Retry-After` is the window's length, an upper bound. `GET /content`
  has no limit (it reads no city). The dev login is counted per remote address: behind a reverse proxy that is the proxy's until forwarded
  headers are configured, and an IPv6 client can rotate addresses inside its prefix; the dev login exists in Development only. A replay
  storm below the limits still takes the player's own city lock and reads the receipt (it settles and writes nothing), so it slows only
  that player's city.
- A body without a `Content-Length` (chunked) is stopped by Kestrel at the size limit; the exception handler answers that stop, and a body
  that doesn't bind (an unknown, missing, mistyped or repeated field, broken JSON), as 400 `INVALID_ARGUMENT` (M0-34). The in-memory test
  server has no body limit of its own, so only the `Content-Length` check, Kestrel's configured value and the handler's mapping of its 413
  are tested, not a chunked body against Kestrel.
- 404, 405 and 415 answers come from the framework with Problem Details but no `code` (`05` → Errors; M0-34 decision 3).
- HTTP is allowed for localhost in development; production requires HTTPS.

## Rule for Claude
When adding an endpoint or command, add its rows to the threat table above and a test for each.
Never describe the system as "unhackable".
