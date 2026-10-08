# Glossary

Use these names in code, docs and UI text. Consistent words keep Claude and the codebase aligned.

| Term | Meaning | Code name |
|---|---|---|
| Item | Any countable good, including coins | `ItemId` |
| Stock | Quantity of an item the city holds | `Inventory[item]` |
| Storage cap | Max stock that production may fill for a capped item (base + research bonuses) | `StorageCap` |
| Reserved | Output quantity promised to running cycles (derived) | `Reserved` |
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
| Building | A placed instance in a city, with a per-city integer id | `Building`, `BuildingId` |
| Phase | `Constructing`, `Upgrading`, `Idle`, `Producing` | `BuildingPhase` |
| Stall reason | Why a building makes no progress (derived) | `StallReason` |
| Builder slot | Capacity for simultaneous constructions/upgrades | `BuilderSlots` |
| Research | A node in an era's tree: cost, duration, prerequisites, effects | `ResearchId`, `ResearchDefinition` |
| Research slot | Capacity for simultaneous research (1 in M0) | `ResearchSlots` |
| Effect | What completed research changes | `UnlockBuilding`, `LaborReduction`, `StorageCapBonus` |
| Era | Time period of a city (1 = Early Middle Ages) | `Era`, `EraId` |
| Major / minor mechanic | Era-gated system vs research-gated addition (`25`) | — |
| Trading Post | NPC merchants that buy goods for coins; the coin faucet | `trading_post` |
| City clock | Per-city simulation time in integer seconds | `City.Clock` |
| Settle | Advance a city from its last settlement to now | `SettleCity`, `Simulator.Advance` |
| Production cap | Max seconds per settlement during which cycles may start | `Rules.MaxOfflineProductionSeconds` |
| Dormant tail | Part of a settlement after the production cap: timers run, no new cycles | — |
| Settlement report | What happened during a settle | `SettlementReport` |
| Zero-length advance | `Advance(city, 0)`: start eligible cycles at the current clock | — |
| Content | All game definitions loaded from JSON | `GameContent`, `contentVersion` |
| Command | A state-changing player intent | `PlaceBuilding`, `UpgradeBuilding`, `AssignWorkers`, `StartResearch` |
| Request id | Client UUID for idempotent retries | `RequestId` |
| Receipt | Stored response of a successful command | `CommandReceipt` |
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
| `[O]` / `[S]` / `[PLAN]` / `[MANUAL]` | Strong model / standard model / plan approval first / human step |
