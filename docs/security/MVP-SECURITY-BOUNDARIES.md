# MVP security boundaries (M0)

What the M0 server protects, how, and which test shows it, written at the end of M0-B (M0-78). The rules themselves live in
`core-instructions/07-security-model.md` (the threat table names every test); this page is the summary over them and the Day 13 suites.
It describes what the tests show; it does not claim the system can't be attacked. What is not covered is named in every section and in
[Known limitations](#known-limitations).

Test paths: `IT` = `server/tests/CityBuilder.IntegrationTests`, `AT` = `server/tests/CityBuilder.Application.Tests`,
`ST` = `server/tests/CityBuilder.Simulation.Tests`, `CT` = `client/CityBuilder/Assets/_Project/Tests/EditMode`.
"On PostgreSQL" means the real API on a real database (Testcontainers, `Category=Integration`); "in memory" means the API with fakes.

## The Day 13 suites (M0-B)
| Suite | What it plays | Tests |
|---|---|---|
| HTTP end-to-end (M0-73) | A new player's Era I loop over HTTP on PostgreSQL, up to 30 h away, a host restart; the stored city equals the engine's at every step | `IT/EndToEnd/HttpEndToEndTests` (6), `EraOneLoopScriptTests` (1, engine only) |
| Attack (M0-74) | Every command endpoint of a played city: values the server owns, another player's buildings, replay, a reused request id, spam | `IT/Security/AttackTests` (5 tests, 16 cases) |
| Concurrency (M0-75) | Parallel requests of one player: coins for one of two placements, two on one tile, an upgrade racing `GET /city` at a refund | `IT/Concurrency/RaceTests` (3), each ending with `SoundCity` (ledger auditor clean, nothing below 0, workers not over-assigned, no duplicate building or ledger row) |
| Clean clone (M0-77) | A fresh clone set up from `docs/getting-started.md` alone | A procedure, recorded in build log BL-20261117-01 |

Each new test was seen failing once by a deliberate break (the races: with the city's `FOR UPDATE` removed). None found a server bug;
one found a missing guard in a test request type (`SetPausedRequest`).

## What the server validates
- **The body is intent only.** Every `/api/v1` endpoint binds exactly the values listed in `07` → "What the client sends"; a new field, or
  a query, route or header value, fails `IT/Security/ClientInputInventoryTests`. Unknown fields, a field named twice, a missing field, a
  wrong type, broken or deeply nested JSON, a non-JSON body and a body over 4 KB are 400 or 415 before anything runs:
  `IT/Security/ApiAttackContractTests`, `ApiAttackDatabaseTests` (`Security_PostWithAFieldTheContractDoesNotHave_Is400AndStoresNothing`,
  `Security_BodyLargerThanTheLimit_Is400BeforeAnythingRuns`, `Security_MegabyteBody_Is400AndStoresNothing`), and on all 12 commands
  `AttackTests.Security_ValueTheServerOwns_OnEveryCommand_Is400AndStoresNothing` (costs, balances, levels, labor, timers, worker totals,
  a player id).
- **Ids and coordinates before the lock** (OD-13): a building id below 1, a negative coordinate or count, an id that can't be a content id,
  a tile list or route outside its limits is `INVALID_ARGUMENT` before the city is locked; the per-command `…ContractTests` and
  `…DatabaseTests` (`Security_…_Is400BeforeTheCityIsLocked`, `…_Is400AndStoresNothing`).
- **Game rules in the engine:** costs, unlocks, prerequisites, level limits, free workers, bounds and occupancy are checked before any
  mutation; over HTTP in each command's `DatabaseTests` (`BUILDING_LOCKED`, `RESEARCH_LOCKED`, `MAX_LEVEL`, `WORKER_LIMIT`,
  `INSUFFICIENT_WORKERS`, `OUT_OF_BOUNDS`), and on the engine in `ST/Properties/InvariantPropertyTests`
  (`Observe_EveryStep_NeverPlacesALockedBuildingOrSkipsAPrerequisite`, `…_AssignsNoMoreWorkersThanProvidedOrAllowed`).
- **Another player's objects:** building ids resolve in the caller's city only, so another player's id is 404 `BUILDING_NOT_FOUND`, never
  a 403 that confirms it exists: `AttackTests.Security_AnotherPlayersBuildings_OnEveryCommandThatNamesOne_Are404AndTouchNeitherCity`.

## What the client may assume
- Its snapshot is the truth at the server's second; its own engine run is display only and is replaced by the next snapshot (`02` rule 7).
- A command retried with the same `requestId` and body gets the first answer (see Idempotency); a lost response is safe to retry
  (`IT/Commands/LostResponseDatabaseTests`).
- An error is the catalogue's Problem Details with a `code`; nothing else in it is stable (`IT/Errors/ExceptionHandlerTests`, `IT/Contracts/ErrorCatalogueTests`).
- Nothing it sends about costs, time or state is used, so it has nothing to keep in sync except intent.

## Auth model
- Signed JWT, HS256 only, this server's issuer and audience, expiry by the server clock without skew; the player id comes only from `sub`.
  Forged, unsigned, foreign, expired and malformed tokens get one identical 401: `IT/Contracts/UnauthenticatedContractTests`
  (`Security_EveryRefusal_GetsTheSameAnswer`); a player named in a query or header is ignored (`Security_PlayerNamedOutsideTheToken_IsIgnored`).
- The signing key is required, at least 32 bytes, never in the repo: `IT/Auth/AuthStartupTests.Start_NoOrShortSigningKey_FailsValidation`.
- The only login is the **dev login**, mapped in Development only; switched on elsewhere it stops the start:
  `IT/Auth/DevLoginEndpointTests.Security_DevLoginInProduction_IsNotMapped`, `Security_DevLoginEnabledOutsideDevelopment_StopsTheStart`.
- Not covered: real accounts, token revocation, refresh tokens (Known limitations).

## Idempotency
- Each command's receipt is keyed by `(playerId, requestId)` with a unique constraint and stores the response; the payload hash includes the
  command type. A replay answers the stored bytes and writes nothing; another body with the same id is 409 `IDEMPOTENCY_CONFLICT`; another
  player's id runs as a new command in the caller's own city.
- Tests: `IT/Commands/CommandIdempotencyDatabaseTests`, `CommandConcurrencyDatabaseTests`
  (`ExecuteAsync_TenIdenticalRequestsAtOnce_ApplyOnceAndAllGetTheSameResponse`, the receipt race);
  `ApiAttackDatabaseTests.Security_ReplayStorm_AppliesTheCommandOnce`; across all 12 commands of a played city
  `AttackTests.Security_ReplayOfEveryAcceptedRequest_AnswersItsFirstAnswerAndWritesNothing` and
  `Security_RequestIdOfEveryAcceptedRequest_WithAnotherBody_Is409AndWritesNothing`; one receipt per accepted command in the end-to-end loop
  (`HttpEndToEndTests.Play_EraOneLoopOverHttp_OneReceiptPerAcceptedCommandAndTheAuditorFindsNoMismatch`).

## Locking: the acting player's city only
- Every write is one transaction that takes `FOR UPDATE` on the caller's city row, settles to now, applies, writes ledger rows and the
  receipt, and checks `state_version` ([ADR-0009](../adr/ADR-0009-row-lock-own-city.md)). Only `SettleCity` and `CommandPipeline` can open it:
  `AT/ApplicationDependencyTests.Types_OfApplication_OnlyTheSettleAndThePipelineDependOnTheRunner`.
- Races of one player end with one winner and a sound city: `RaceTests` (three races, each with `SoundCity`);
  `PlaceBuildingDatabaseTests.Post_TenIdenticalRequestsAtOnce_PlaceOneBuilding`,
  `AssignWorkersDatabaseTests.Security_TwoAssignmentsAtOnce_NeverAssignMoreThanProvided`,
  `StartResearchDatabaseTests.Security_TwoStartsAtOnce_StartExactlyOneResearch`, the road tile race in `RoadCommandsDatabaseTests`.
- Other cities never wait: `IT/Persistence/ConcurrencyDatabaseTests.ExecuteAsync_OtherCitiesWhileOneIsLocked_FinishWithoutWaiting`.
- No command touches another player's city; cross-player effects (the market, M2) go through a mailbox (`02` rule 10). Not built yet.

## Time model
- The server clock (`TimeProvider`, UTC, whole seconds) is the only clock. No request carries a time (`ClientInputInventoryTests`); the
  engine has no clock of its own and can't read one (`server/tests/CityBuilder.Repository.Tests` →
  `ProjectFileTests.BannedSymbols_BanTheClockAndSleep`). Elapsed time is the server's now minus the stored city second.
- Long absences: there is no offline production cap since CR-0008; output is bounded by stores, labor and courier stamina instead
  (`04` §2). Tests: `ST/EngineRules/StaminaRuleTests.Advance_PlayerPresent_SpendsNoStamina`,
  `ST/Performance/CourierBudgetTests.Advance_LongAbsence_RunsEveryCourierToTheEndOfItsStamina`, over HTTP
  `HttpEndToEndTests.Play_EraOneLoopOverHttp_ThirtyHoursAway_ResearchEndsOnTimeAndCouriersRest`.
- **DevTimeScale guard:** `Simulation:DevTimeScale` is used only in Development; any other value elsewhere stops the start, and the
  production clock is the real one: `IT/Time/DevTimeScaleTests.Security_DevTimeScaleOutsideDevelopment_StopsTheStart`,
  `Security_Production_ClockIsRealAndTimeScaleIsOne`.

## Worker and labor protections
- Assigned workers never exceed what the city provides or a level allows; labor is progress-preserving, so moving workers mid-cycle gains
  nothing. Engine: `InvariantPropertyTests.Observe_EveryStep_AssignsNoMoreWorkersThanProvidedOrAllowed`,
  `…_CompletesEachCycleWhenItsWorkerSecondsReachItsLabor`. Over HTTP: `IT/Security/WorkerLaborDatabaseTests`
  (`Security_MovingWorkersBetweenBuildings_NeverMakesMoreThanTheLaborAllows`, 5 seeds), the assignment race above, and every race's
  `SoundCity` check (free workers not below 0).
- Arithmetic is integer and `checked`, with content maxima validated at load: `ST/CeilDivTests`, the labor reduction tests.

## Rate limits
Per player and in memory: commands 10/s and 120/min (all command endpoints together), `GET /city` and `GET /content` 30/min each, the dev
login 10/min per address; a refused request is never bound or locked (`05` → Rate limits). Tests: `IT/RateLimits/RateLimitTests`
(`Post_CommandsPastTheBurstLimit_Are429BeforeTheCityIsLocked`, `Security_OnePlayersSpam_DoesNotBlockAnotherOrTheirOtherBudget`,
`Security_EveryApiEndpoint_IsInItsLimitClass`); on a played city on PostgreSQL `AttackTests.Security_SpamPastTheBurstLimit_Is429AndStoresOnlyWhatRan`.

## Logging policy
Logged: player id, request id, the server's trace id, command type, result code, duration, settlement size. Never: tokens, keys, request
bodies, display names (`07` → Logging policy; every event: `docs/api/logs-and-correlation.md`). Tests: `IT/Logging/CommandLogTests`
(`Security_CommandRequest_LogsNoBody`), `RequestLogTests` (`Security_TraceContextFromTheClient_IsIgnored`), the auth and dev-login log
tests; on the client `CT/Core/LoggingPolicyTests` and `CT/Net/ApiClientLoggingTests`.

## Known attack surface
- `/api/v1`: the dev login (Development only), `GET /city`, `GET /content` and 12 command endpoints, all behind the token and rate limits.
- `GET /health` without auth (the status word only); `GET /openapi/v1.json` in Development only
  (`IT/OpenApiEndpointTests.Security_OpenApiDocument_InProduction_IsNotServed`).
- Response headers: `Cache-Control: no-store`, `nosniff`, no `Server` header (`ApiAttackContractTests.Security_EveryAnswer_ForbidsCachingAndSniffing`).
- The Unity client: a release build talks https only and keeps the token in memory only (`CT/Net/ApiClientTests`, `AccessTokenStoreTests`).
- `server-panel/goods.sql`, a developer script that reaches only the local Docker database (`IT/ServerPanel/GoodsPanelScriptTests`).

## Known limitations
The full list with reasons is `07` → "Known limitations in M0". In short:
- Dev auth only: no real accounts, no revocation; a token lives until it expires (12 h), and changing the signing key ends all of them.
- No device attestation: a scripted client can play, within the server's rules, as well as a perfect player; it can also keep couriers
  awake by polling (CR-0008 D8, accepted for M0).
- Rate limits are per instance and reset on restart; fixed windows allow up to twice a limit across a boundary; the dev login counts per
  address (a proxy's, or a rotating IPv6 address).
- No TLS on the server itself (planned in front of it, M1); a development build keeps its token in plain text.
- A chunked body over the limit is tested against Kestrel by hand only (M0-34).
- Not tested in M0-B: several server instances, a database failover, load beyond the rate limits, and the client against a hostile server.

## Before the market (M2) we must add
- Real accounts with platform sign-in and session revocation (M1, `07` → Future threats).
- Rate limits shared by all instances (a store outside the process), and order limits for the market.
- Per-item advisory locks, escrow and idempotent orders for every cross-player write, through a mailbox, with a ledger row on both sides.
- The ledger auditor run on a schedule with alerts, and a per-item kill switch.
- An answer to scripted presence (a daily awake-time limit or device attestation, CR-0008 D8).
- Alt-account defences: an account-age gate, price bands, trade-graph monitoring, no direct gifting (`26`).
- An attack and a concurrency suite for the market endpoints, like M0-74 and M0-75, including two players racing for one order.

Last updated: M0-78 (M0-B)
