# Coverage matrix

What protects each rule the game must never break: every engine invariant (`core-instructions/04-simulation-model.md` §10), every API
error code (`core-instructions/05-api-contract.md` → Errors) and every M0 threat (`core-instructions/07-security-model.md` → Threats), with
the tests that hold it. It is sorted by protected rule, not by coverage percentage (M0-81). Test classes are under `server/tests/`; the
Unity client's under `client/CityBuilder/Assets/_Project/Tests/`.

How to keep it true: a new invariant, error code or threat gets its row here in the same PR as its test (`core-instructions/19-change-recipes.md`).
The rows say which classes hold a rule; the individual test names of the threats stay in `07`'s Test column, their one home.

## Engine invariants (`04` §10)
Invariants 4 to 11 and 13 to 16 are checked on every step of seeded random histories by `Properties/InvariantChecks` (one test per
invariant in `InvariantPropertyTests`), and `Observe_PropertySeeds_ExerciseEveryCheck` fails if the seeds stop reaching a check's cases.
The focused rule tests below are the readable examples of the same rule.

| # | Invariant | Property test (`CityBuilder.Simulation.Tests`) | Focused tests |
|---|---|---|---|
| 1 | Same input → same output | `DeterminismPropertyTests` (generate twice, replay) | `Golden/*`; `CityBuilder.Repository.Tests/Architecture/EngineSourceTests` (no clock, randomness or floats in the engine) |
| 2 | Split invariance | `SplitInvariancePropertyTests` | `Settlement/*` |
| 3 | Timer invariance | `TimerInvariancePropertyTests` (also over long absences) | `Settlement/*` |
| 4 | Conservation per item | `Observe_EveryStep_ConservesEachItem` | `EngineRules/DeliveryRuleTests`, `Rules/DeliveryTests` |
| 5 | No negative quantity | `Observe_EveryStep_LeavesNoStockBelowZero` | `State/*` (factories refuse negatives) |
| 6 | Stores within their limit | `Observe_EveryStep_ProductionKeepsStockPlusReservedWithinTheCap` | `EngineRules/StoreLimitRuleTests` (M0-C-3) |
| 7 | Clock never decreases | `Observe_EveryStep_MovesTheClockByTheRequestedTimeOnly` | `Settlement/*` |
| 8 | No production while building | `Observe_EveryStep_ProducesNothingBeforeConstructionOrDuringAnUpgrade` | `Rules/UpgradeBuildingTests` |
| 9 | Worker limits, one courier per worker | `Observe_EveryStep_AssignsNoMoreWorkersThanProvidedOrAllowed` | `Rules/AssignWorkersTests`, `EngineRules/WorkerRuleTests`; HTTP `Security/WorkerLaborDatabaseTests` |
| 10 | Builder slots, one research | `Observe_EveryStep_KeepsBuildersWithinTheSlotsAndOneResearch` | `Rules/StartResearchTests`, `EngineRules/LevelCapAndBuilderSlotRuleTests` (the slots of `builderSlotBonus`, M1-02); HTTP race `Security_TwoStartsAtOnce_StartExactlyOneResearch` |
| 11 | Labor accounting | `Observe_EveryStep_CompletesEachCycleWhenItsWorkerSecondsReachItsLabor` | `ApplyPermilleReductionTests`; HTTP `Security_MovingWorkersBetweenBuildings_NeverMakesMoreThanTheLaborAllows` |
| 12 | Walks cross buildings only when they must | none (a map property, not a city step) | `Map/PathFinderTests` (never crosses with a way around, crosses once when walled in), `Map/PathFinderBruteForceTests` (random maps against brute force) |
| 13 | Unlocks, prerequisites, amount modes | `Observe_EveryStep_NeverPlacesALockedBuildingOrSkipsAPrerequisite` | `Rules/PlaceBuildingTests`, `Rules/StartResearchTests`, `Rules/StartTripTests`, `Rules/SetRouteTests`; the level cap of `maxLevelBonus` (M1-02): `EngineRules/LevelCapAndBuilderSlotRuleTests`, HTTP `UpgradeBuildingDatabaseTests` |
| 14 | Stamina only when absent; carry limits | `Observe_EveryStep_SpendsStaminaOnlyWhenAbsentAndKeepsLoadsWithinTheirLimits` | `Settlement/*` (absence), Application `ExecuteAsync_LongAbsence_SpendsStaminaFromTheGraceOn` |
| 15 | Route or lines, line limits | `Observe_EveryStep_KeepsDeliveryLinesWithTheirCouriers` | `Rules/DeliveryTests`; HTTP `CancelDeliveryContractTests` (queue full, courier busy: M0-81) |
| 16 | Paused buildings stay still | `Observe_EveryStep_KeepsPausedBuildingsStill` | `Rules/PauseTests`; HTTP `SetPausedContractTests` |
| 17 | Builder queue: within its length, no free builder while an order waits, orders start in order (CR-0013) | `Observe_EveryStep_StartsQueuedOrdersInOrderWhenABuilderFrees` | `EngineRules/BuilderQueueRuleTests`, `Content/ContentBuilderQueueTests`; HTTP `BuilderQueueContractTests` |

## Error codes (`05` → Errors)
Every code is held to the table and its shape by `Errors/ErrorCatalogueTests`, every engine code to its API code and status by
`Contracts/CommandErrorMappingTests`, and the client's list and sentences by `ClientErrorCodesTests` and the client's `FeedbackMessages`
tests. The columns below are the tests that **produce** the code: the engine rule (`CityBuilder.Simulation.Tests/Rules`, `EngineRules`)
and the answer over HTTP (`CityBuilder.IntegrationTests`; `…DatabaseTests` run on PostgreSQL).

| Code | Engine | Over HTTP |
|---|---|---|
| `AUTHENTICATION_REQUIRED` | — | `Auth/*`, `UnauthenticatedContractTests`, `ApiAttackDatabaseTests` |
| `INVALID_ARGUMENT` | `RoadRulesTests`, `SetRouteTests`, `PauseTests` | every `…ContractTests` (`Security_…Is400BeforeTheCityIsLocked`), `ApiAttackContractTests` |
| `UNKNOWN_BUILDING_TYPE` | `PlaceBuildingTests` | `PlaceBuildingContractTests`, `PlaceBuildingDatabaseTests` |
| `BUILDING_LOCKED` | `PlaceBuildingTests`, `ResearchRuleTests` | `PlaceBuildingContractTests`, `PlaceBuildingDatabaseTests` |
| `OUT_OF_BOUNDS` | `PlaceBuildingTests`, `RoadRulesTests` | `PlaceBuildingContractTests`, `RoadCommandsContractTests`, `ApiAttackDatabaseTests` |
| `TILE_OCCUPIED` | `PlaceBuildingTests`, `RoadRulesTests` | `RoadCommandsContractTests`, `RaceTests` |
| `INSUFFICIENT_RESOURCE` | `PlaceBuildingTests`, `UpgradeBuildingTests`, `RoadRulesTests` | `PlaceBuildingDatabaseTests`, `StartResearchContractTests`, `RaceTests` |
| `INSUFFICIENT_WORKERS` | `AssignWorkersTests`, `WorkerRuleTests`, `StartTripTests` | `AssignWorkersDatabaseTests`, `WorkerLaborDatabaseTests` |
| `WORKER_LIMIT` | `AssignWorkersTests`, `UpgradeRuleTests` | `AssignWorkersContractTests`, `AssignWorkersDatabaseTests` |
| `NO_FREE_BUILDER` | `PlaceBuildingTests`, `UpgradeBuildingTests`, `BuilderQueueRuleTests` (queue full) | `PlaceBuildingDatabaseTests`, `UpgradeBuildingDatabaseTests`, `BuilderQueueContractTests` |
| `BUILDING_NOT_FOUND` | `AssignWorkersTests`, `UpgradeBuildingTests`, `PauseTests` | every command's `…ContractTests`; another player's ids: `Security_AnotherPlayersBuildings_OnEveryCommandThatNamesOne_Are404…` |
| `BUILDING_BUSY` | `AssignWorkersTests`, `UpgradeBuildingTests`, `PauseTests` | `UpgradeBuildingContractTests`, `AssignWorkersContractTests`, `SetPausedContractTests` |
| `MAX_LEVEL` | `UpgradeBuildingTests`, `RoadRulesTests`, `LevelCapAndBuilderSlotRuleTests` | `UpgradeBuildingDatabaseTests` (also before Timber Framing, M1-02), `RoadCommandsContractTests` |
| `UNKNOWN_RESEARCH` | `StartResearchTests` | `StartResearchContractTests`, `StartResearchDatabaseTests` |
| `RESEARCH_LOCKED` | `StartResearchTests` | `StartResearchContractTests`, `StartResearchDatabaseTests` |
| `RESEARCH_IN_PROGRESS` | `StartResearchTests` | `StartResearchContractTests`, `StartResearchDatabaseTests` (also raced) |
| `RESEARCH_COMPLETED` | `StartResearchTests` | `StartResearchContractTests`, `StartResearchDatabaseTests` |
| `ROAD_NOT_FOUND` | `RoadRulesTests` | `RoadCommandsContractTests`, `RoadCommandsDatabaseTests` |
| `BUILDING_LIMIT` | `PlaceBuildingTests` | `PlaceBuildingDatabaseTests` |
| `NO_SCIENCE_BUILDING` | `StartResearchTests` | `StartResearchDatabaseTests` |
| `UNKNOWN_ITEM` | `StartTripTests`, `SetRouteTests` | `StartTripContractTests`, `SetRouteContractTests` |
| `AMOUNT_MODE_LOCKED` | `StartTripTests`, `SetRouteTests` | `StartTripContractTests`, `SetRouteContractTests` |
| `ITEM_NOT_ACCEPTED` | `StartTripTests` | `StartTripContractTests` |
| `NOTHING_TO_CARRY` | `StartTripTests` | `StartTripContractTests` |
| `INVALID_ROUTE` | `SetRouteTests` | `SetRouteContractTests` |
| `NO_FREE_COURIER` | `DeliveryTests` | `CancelDeliveryContractTests` |
| `DELIVERY_QUEUE_FULL` | `DeliveryTests` | `CancelDeliveryContractTests.StartTrip_LinesBeyondTheQueue_IsDeliveryQueueFull` (M0-81) |
| `ROUTES_LOCKED` | `DeliveryTests` | `SetRouteContractTests` |
| `COURIER_BUSY` | `DeliveryTests`, `StoreLimitRuleTests` | `CancelDeliveryContractTests.SetRoute_CourierWithDeliveryLines_IsCourierBusy` (M0-81) |
| `DELIVERY_NOT_FOUND` | `DeliveryTests` | `CancelDeliveryContractTests` |
| `IDEMPOTENCY_CONFLICT` | — | Application and PostgreSQL pipeline tests (`07`), every command's `Post_SameRequestAgain_…` |
| `RATE_LIMITED` | — | `RateLimits/RateLimitTests`, `AttackTests` |
| `CONCURRENCY_CONFLICT` | — | `CommandConflictDatabaseTests`; the client's retry in `CommandSender` tests |
| `INTERNAL_ERROR` | — | `ExceptionHandlerTests` |
| `NOT_FOUND`, `METHOD_NOT_ALLOWED`, `UNSUPPORTED_MEDIA_TYPE` | — | `ExceptionHandlerTests`; 415 also `ApiAttackContractTests` |
| `UNSUPPORTED_CLIENT_VERSION` | — | reserved: no source produces it yet (`05`); only the catalogue and the client's mapping are tested |

## Threats (`07` → Threats in M0)
Every test `07` names was checked to exist (M0-81: 81 of 81). The rows below say where each threat is held; the test names are in `07`.

| Threat | Held by |
|---|---|
| Fake costs, balances, levels, labor, timers | every command's `Security_PostWithAFieldTheContractDoesNotHave_…`; `Security/ClientInputInventoryTests` (what each endpoint binds) |
| Device clock | the server clock only (`FakeTimeProvider` in every test); `ClientInputInventoryTests` (no time is bound); `StartTripContractTests.Security_ClockHeadersFromTheClient_AreIgnored` (M0-81) |
| Workers used twice or above the limit | invariants 9 and 11; `Security/WorkerLaborDatabaseTests` |
| Locked buildings, skipped prerequisites | invariant 13; `BUILDING_LOCKED`, `RESEARCH_LOCKED` above |
| Two researches by racing | `StartResearchDatabaseTests`, `Concurrency/RaceTests` |
| Replay; same id other payload; another player's id | Application `CommandPipeline` tests, PostgreSQL receipt tests, `ApiAttackDatabaseTests` (every command) |
| A command that writes without receipt or settle | the architecture test `Types_OfApplication_OnlyTheSettleAndThePipelineDependOnTheRunner` |
| Double spend by racing | `Concurrency/RaceTests`, `RoadCommandsDatabaseTests` |
| Another player's building, goods or route | `Security_AnotherPlayersBuildingId_…` per command, `ApiAttackDatabaseTests` |
| Outside the grid; huge or malformed bodies; huge routes and tile lists | the `Security_…Is400BeforeTheCityIsLocked` tests per command, `ApiAttackContractTests` |
| Spam and settle storms | `RateLimits/RateLimitTests` |
| Set goods by hand | `ServerPanel/GoodsPanelScriptTests`, `server-panel/test-goods.sh` |
| Dev login, OpenAPI, dev time scale in production | `Auth/*`, `OpenApiEndpointTests`, `Time/DevTimeScaleTests` |
| No token, another player's token, a token of no player, forged or expired tokens | `Auth/*` (`Security_EveryRefusal_GetsTheSameAnswer`) |
| Weak signing key; build-time placeholders | `Start_NoOrShortSigningKey_FailsValidation`, `Security_BuildTimeDocumentSettings_AreNotUsedByAServer` |
| Two first logins of one name | `PostDevLogin_SameNameCommittedFirstByAnotherRequest_ReturnsThatPlayer` |
| Overflow | `checked` in the engine; content maxima `Create_NumberAboveMaxQuantity_ReportsValueTooLargeAtItsPath`; `CeilDivTests`, `ApplyPermilleReductionTests`, `ProgressPermilleTests`; an `int` overflow in a body is 400 |
| Couriers kept awake by polling | accepted limitation (CR-0008 D8); presence tests `ExecuteAsync_TwoRequests_TheSecondCountsTheAbsenceFromTheFirst` |
| Secret leakage | `CityBuilder.Repository.Tests/Security/SecretScanTests` (no committed key or connection string, none in the Unity client, `.env` ignored; M0-81) |
| Information leakage; caching; trace ids | `ExceptionHandlerTests`, `ErrorCatalogueTests`, `Security_EveryAnswer_ForbidsCachingAndSniffing`, `Security_TraceContextFromTheClient_IsIgnored` |
| Token on the network or in a release build's storage | the client's `ApiClientTests`, `InsecureHttpSettingTests`, `AccessTokenStoreTests` |
| `/health` probing | `GetHealth_AppRunning_Returns200WithStatusOnly` |

## Known gaps
- `UNSUPPORTED_CLIENT_VERSION` is reserved: it gets a producing test when the server first refuses a build.
- `COURIER_BUSY` from assign-workers (a removed courier carrying goods its store can't take back, M0-C-3) is held by the engine
  (`StoreLimitRuleTests`) and the shared error mapping, not by its own HTTP test.

Last updated: M1-02
