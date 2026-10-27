# Glossary

Use these names in code, docs and UI text. Consistent words keep Claude and the codebase aligned.

| Term | Meaning | Code name |
|---|---|---|
| Item | Any countable good, including coins | `ItemId` |
| Stock | Quantity of an item the city holds | `CityState.Inventory`, `GetQuantity(item)` |
| Storage cap | Max stock that production may fill for a capped item (base + research bonuses) | `GameRules.BaseStorageCaps`, `CityQueries.TryGetEffectiveCap` |
| Reserved | Output quantity promised to running cycles (derived) | `CityQueries.Reserved` |
| Recipe | Inputs → outputs with a labor cost, per building type and level | `Recipe` |
| Labor | Worker-seconds one cycle needs | `Labor`, `CycleLabor`, `LaborRemaining` |
| Cycle | One run of a recipe | — |
| Rate | Display value: outputs per hour at the current staffing | `PerHour` (display only) |
| Net rate | Σ production − Σ consumption per hour of running buildings (display only) | `NetPerHour` |
| Workers | Population from completed Cottages, assigned to producers | `WorkersProvided`, `AssignedWorkers`, `FreeWorkers` |
| Max workers | Worker limit of a building at its level | `MaxWorkers` |
| Auto-staffing | Free workers given to a building when its construction completes | — |
| Segment | Period during which a cycle's worker count is constant | `SegmentStartedAt` |
| Frozen cycle | A producing cycle with 0 workers (no progress) | `PhaseEndsAt == null` |
| Building type | Content definition (cost, recipes, max workers per level, unlock) | `BuildingTypeId`, `BuildingDefinition` |
| Building kind | What a building type does: a house provides workers, a producer runs a recipe | `BuildingKind` (`House`, `Producer`) |
| Building | A placed instance in a city, with a per-city integer id | `Building`, `Building.Id` (`buildingId` in the API) |
| Phase | `Constructing`, `Upgrading`, `Idle`, `Producing` | `BuildingPhase` |
| Stall reason | Why a building makes no progress (derived) | `StallReason`, `StallKind`, `StallReason.Of` |
| Builder slot | Capacity for simultaneous constructions/upgrades | `BuilderSlots` |
| Research | A node in an era's tree: cost, duration, prerequisites, effects | `ResearchId`, `ResearchDefinition` |
| Research slot | Capacity for simultaneous research (1 in M0) | `ResearchSlots` |
| Effect | What completed research changes | `UnlockBuildingEffect`, `LaborReductionEffect`, `StorageCapBonusEffect` |
| Era | Time period of a city (1 = Early Middle Ages) | `Era`, `EraId` |
| Major / minor mechanic | Era-gated system vs research-gated addition (`25`) | — |
| Trading Post | NPC merchants that buy goods for coins; the coin faucet | `trading_post` |
| City state | Everything stored about one city: clock, era, stock, buildings, research; derived values are computed, never stored | `CityState`, `CityFactory`, `CityQueries` |
| City clock | Per-city simulation time in integer seconds | `CityState.Clock` |
| Settle | Advance a city from its last settlement to now | `SettleCity`, `Simulator.Advance` |
| Settlement window | The server seconds one settle covers: from the last settled second to now; nothing elapses when the clock is behind | `SettlementWindow`, `CityAggregate.LastSettledUnixSec` |
| Event source | One kind of timed event the advance processes, in a fixed order within a second: research timer, building timers, later the mailbox | `IEventSource` |
| Production cap | Max seconds per settlement during which cycles may start | `Rules.MaxOfflineProductionSeconds` |
| Dormant tail | Part of a settlement after the production cap: timers run, no new cycles | — |
| Settlement report | What happened during a settle | `SettlementReport` |
| Settlement | One settle on the server: the window's advance and the closing zero-length advance, with its id, totals and ledger rows (`ref_type` `settlement`) | `Settlement`, `SettleCityResult` |
| Zero-length advance | `Advance(city, 0)`: start eligible cycles at the current clock | `Simulator.Advance(city, content, 0)` |
| Content | All game definitions loaded from JSON: as read, and validated (only the factory makes the latter) | `GameContentData`, `GameContent`, `GameContentFactory`, `contentVersion`; on the server `IContentProvider`, `ContentFileLoader`, `Content:FilePath` |
| Content error | One problem found when content loads: a stable code, the path in the JSON, a message (`04` §14) | `ContentError`, `ContentErrorCode` |
| Access token | The signed proof of who the player is: every `/api/v1` request but the dev login sends it, and the acting player comes only from it (`07`) | `AccessTokens`, `ICurrentPlayer`, `Auth:SigningKey` |
| Dev login | M0's stand-in for accounts, Development only: a display name logs in and its first login creates the player and the starter city | `DevLogin`, `POST /api/v1/auth/dev-login` |
| Command | A state-changing player intent | `PlaceBuilding`, `UpgradeBuilding`, `AssignWorkers`, `StartResearch` |
| Mutation | The engine's rule for one command, applied at the city clock: `Ok`, or rejected with a code and no change (`04` §8) | `CityRules`, `MutationResult`, `MutationErrorCode` |
| Request id | Client UUID for idempotent retries | `RequestId` |
| Receipt | Stored response of a successful command | `CommandReceipt` |
| Command pipeline | The one path every command takes: shape check, own-city lock, receipt check, settle, mutation, closing advance, one save with the ledger and the receipt (`03`, request lifecycle) | `CommandPipeline`, `ICityCommand` |
| Canonical payload | The command type and the command's data in a fixed form, whose SHA-256 a receipt keeps: the same request id with another payload is a conflict (`05`, idempotency rules) | `CommandPayload` |
| Ledger entry | Audit row for one item change | `LedgerEntry` |
| Snapshot | Full authoritative city state sent to the client | `CitySnapshotDto` |
| State version | Counter incremented on every persisted change | `StateVersion` |
| Display prediction | Client-side `Advance` on a copy of the snapshot, for visuals only | `DisplayClock` |
| Open market | Player-to-player exchange (Era II) | `Market*` |
| Order / escrow | A buy or sell order and the goods or coins it locks | `MarketOrder`, `Escrow` |
| Mailbox | Deliveries to a player from others' actions, applied at their next settle | `MailboxDelivery` |
| Guild | Player co-op group (Era II) | `Guild` |

## Process words
| Term | Meaning |
|---|---|
| Owner | The person who decides what gets built, approves and merges |
| AI coder | Any AI tool working on the repo in a session (Claude Code or other) |
| Task | A unit of work with an id: plan task `M0-15` or CR task `CR-0007-T1` |
| CR (change request) | A proposed change, `docs/changes/CR-NNNN-*.md`; built only once `Approved` |
| Resume point | The top section of `13-progress.md`: exactly where work stands and the next step |
| Build log entry | `docs/build-log/<YYYY>/…-<ID>.md`: sessions, every decision, owner input, verification for one task |
| Checkpoint | Update the resume point and build log, then commit and push, after a green step (`16` §D) |
| Write-ahead | Recording the plan before doing the work, so an interruption loses nothing |
| Gate | A point where the AI must ask the owner (`15`) |
| Protected file | A file that needs owner approval for every edit (`15`) |
| Change recipe | The checklist of code, tests and docs for one kind of change (`19`) |
| Module doc | `docs/modules/<Project>.md`, one per project or assembly |
| Generated docs | `docs/reference/generated/`, produced by DocTools, never edited by hand |
| Handbook | The documentation as a website (`website/`, published on GitHub Pages): the same Markdown files, in book order (`17` §7) |
| Docs impact | The table in a build log entry of every page a change could make wrong, each `updated` or `no change: <why>` (`17` §5) |
| `[O]` / `[S]` / `[PLAN]` / `[MANUAL]` | Strong model / standard model / plan approval first / human step |
