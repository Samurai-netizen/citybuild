# Data Model (PostgreSQL 18)

> **Built so far:** the tables below, with their keys, constraints and foreign keys, as the EF Core model and the migration `InitialCreate`
> (M0-19; `docs/modules/CityBuilder.Infrastructure.md`), and PostgreSQL 18.6 in Docker; the mapping to the engine's city state, the
> repositories and the unit of work (M0-20); the row lock and the write transaction, `ICityTransactionRunner` (M0-22); the settle with its
> ledger rows (M0-23); the first login's insert of a player and their city (M0-25); the command receipts, their lookup, a command's ledger
> rows and the migration `ReceiptResponseAsJson` (M0-26); `city_roads` with the migration `AddCityRoads`, its mapping and the `COST_ROAD`
> rows (CR-0008-T1 part D); `cities.purse` and `next_trip_id`, `city_stores`, `city_project_needs`, `city_research_needs` and `city_trips`
> in place of `city_inventory`, with the reset migration `LogisticsStoresTripsAndReset` (D10), their mapping and the
> `SETTLEMENT_DELIVERED` and `SETTLEMENT_REFUNDED` rows (part E); `city_couriers`, `city_courier_loads` and `city_route_actions` with
> the migration `AddCouriersAndRoutes` and their mapping (part G); `cities.last_seen_unix` with the migration `AddLastSeen` (part H). A
> task that builds a part updates this line.

PostgreSQL is the only source of truth. Wall timestamps are `timestamptz` (UTC). Simulation times and labor
are `bigint` city-clock seconds and worker-seconds. Quantities are `bigint`. Item, building, research and era ids are `text` from content.

## Tables (M0)

| Table | Key columns | Constraints / notes |
|---|---|---|
| `players` | `id uuid pk`, `dev_name text`, `created_at` | `unique(dev_name)`; real accounts come in M1 |
| `cities` | `id uuid pk`, `player_id uuid`, `era smallint`, `clock_seconds bigint`, `last_settled_unix bigint`, `last_seen_unix bigint`, `next_building_id int`, `purse bigint`, `next_trip_id int`, `research_id text null`, `research_started_at bigint null`, `research_ends_at bigint null`, `state_version bigint`, `content_version text`, `created_at`, `updated_at` | `unique(player_id)` (one city per player); checks: `era >= 1`, `purse >= 0` (the currency, `04` §16), `next_trip_id >= 1`; `state_version` is a concurrency token; `research_started_at` and `research_ends_at` are null while a running research waits for goods |
| `city_stores` | `(city_id, building_id, item_id) pk`, `quantity bigint` | `check (quantity >= 0)`; FK to `city_buildings`, cascade; a row per good the store holds, kept at 0 (CR-0008) |
| `city_project_needs` | `(city_id, building_id, item_id) pk`, `quantity bigint` | `check (quantity > 0)`; FK to `city_buildings`, cascade; the goods a construction site or a waiting upgrade still needs; no rows = no project waiting |
| `city_research_needs` | `(city_id, item_id) pk`, `quantity bigint` | `check (quantity > 0)`; FK to `cities`, cascade; only while the running research waits at the science building |
| `city_trips` | `(city_id, trip_id) pk`, `source_id int`, `target_id int`, `item_id text`, `quantity bigint`, `departed_at bigint`, `arrives_at bigint`, `remaining bigint` (default 0), `leg text` (default `ToTarget`) | checks: `quantity > 0` on a `ToTarget` leg, `quantity = 0 and remaining > 0` on a `ToSource` leg, `remaining >= 0`, `arrives_at > departed_at`; FK to `cities`, cascade; the row changes at each end of a leg and goes when the trip ends (amendment 1, migration `AddDeliveryLines`) |
| `city_buildings` | `(city_id, building_id) pk`, `type_id`, `x smallint`, `y smallint`, `level smallint`, `assigned_workers smallint`, `phase text`, `phase_started_at bigint null`, `phase_ends_at bigint null`, `target_level smallint null`, `cycle_labor bigint null`, `labor_remaining bigint null`, `segment_started_at bigint null` | `unique(city_id, x, y)`; checks: `x, y >= 0`, `level >= 1`, `assigned_workers >= 0`, `labor_remaining >= 0` |
| `city_research` | `(city_id, research_id) pk`, `completed_at bigint` | completed nodes only |
| `command_receipts` | `(player_id, request_id) pk`, `command_type`, `payload_hash bytea`, `response_json json`, `created_at` | Successful commands only; `json`, not `jsonb`, keeps the response text as written (decision 041); cleanup job later |
| `ledger_entries` | `id bigint identity pk`, `city_id`, `item_id`, `delta bigint`, `balance_after bigint`, `reason text`, `ref_type text`, `ref_id text`, `created_at` | `index (city_id, created_at)` |

Ledger reasons in M0: `STARTING_GRANT`, `SETTLEMENT_PRODUCED`, `SETTLEMENT_CONSUMED`, `COST_PLACE`, `COST_UPGRADE`,
`REFUND_UPGRADE`, `COST_RESEARCH`; CR-0008 adds `COST_ROAD` (part D), `SETTLEMENT_DELIVERED` and `SETTLEMENT_REFUNDED` (part E; below). `DEV_ADJUST` is written only by the developer's goods script of the server panel (`server-panel/goods.sql`, CR-0006) on the dev database,
never by the server (`ref_type` `dev-panel`, `ref_id` the mode: add, remove or set); the delta is the change really made. Later: `MARKET_ESCROW`, `MARKET_PROCEEDS`, `MARKET_FEE`, `MAILBOX_DELIVERY`, `ERA_CHARTER`.
A settlement writes, per item in ordinal order, one `SETTLEMENT_PRODUCED` row (its total output), one `SETTLEMENT_REFUNDED` row (inputs
given back when an upgrade started at its last delivery), one `SETTLEMENT_CONSUMED` row (its total input, negative) and one
`SETTLEMENT_DELIVERED` row (goods walkers handed to projects, negative), each only when it is not 0, with `ref_type` = `settlement` and
`ref_id` = the settlement's id, a UUID v7 made for it (there is no settlements table). Each `balance_after` runs on from the item's city
total before the settlement (`04` §16: stores, loads, the purse), so the gains first keep every balance ≥ 0, and the last one of an item
equals its total. A command's rows go between its settlement's, in the order things happened (decision 042): the
advance's rows, then the command's (`REFUND_*`, then `COST_*`, each per item in ordinal order, `ref_type` = `command`, `ref_id` = the
`requestId`), then the closing zero-length advance's `SETTLEMENT_*` rows, with the settlement's reference. The closing advance can consume
what an upgrade refunded, so its rows never come first.

Derived on load, never stored: reserved outputs, stall reasons, workers provided/on trips/free, builders busy, effective store limits and
labor, city totals, a building's project.

## Tables for logistics (CR-0008: `city_roads` built in CR-0008-T1 part D, the stores, needs and trips above in part E, the courier tables in part G, `last_seen_unix` in part H)
| Table / column | Key columns | Constraints / notes |
|---|---|---|
| `cities` + `last_seen_unix bigint` (built) | — | the server second of the last request that settled the city (not a replay), on the same clock as `last_settled_unix`; it never decreases; migration `AddLastSeen` sets it to `last_settled_unix` for stored cities |
| `city_roads` (built) | `(city_id, x, y) pk`, `x smallint`, `y smallint`, `level smallint` | `x, y >= 0`, `level >= 1`; never on a building's tile (the engine checks it on load); FK to `cities`, cascade; a removed road's row is deleted |
| `city_couriers` (built) | `(city_id, station_id, slot) pk`, `state text`, `to_stop smallint`, `departed_at bigint null`, `arrives_at bigint null`, `stamina_seconds bigint` | `slot >= 1`, `to_stop >= 0`, `stamina_seconds >= 0`, a walk has both times (`departed_at >= 0`, `arrives_at > departed_at`) or neither; `state` is the API's spelling; the times are set exactly while walking or returning; FK `(city_id, station_id)` to `city_buildings`, cascade |
| `city_courier_loads` (built) | `(city_id, station_id, slot, item_id, from_building_id) pk`, `quantity bigint` | `quantity > 0`; FK to `city_couriers`, cascade; `from_building_id` gets the goods back when the courier is stopped (no FK; the engine checks it names a building of the city) |
| `city_delivery_lines` (built, amendment 1) | `(city_id, station_id, slot, line_id) pk`, `source_id int`, `target_id int`, `item_id text`, `quantity bigint` | `quantity > 0`, `source_id <> target_id`, `slot >= 1`; `line_id` is unique in the city; FK `(city_id, station_id, slot)` to `city_couriers`, cascade (the key's first columns, so no index of its own); a courier runs its lines in `line_id` order; the row goes when the line ends or is cancelled; `cities.next_line_id int` (default 1, `>= 1`) gives the ids (migration `AddDeliveryLines`) |
| `city_route_actions` (built) | `(city_id, station_id, slot, stop_index, action_index) pk`, `building_id int`, `kind text`, `item_id text`, `amount text` | `slot >= 1`, indices from 0, `kind in ('take', 'put')`; a route is its actions in order, its stops without a gap; every action of one stop names the same building (the mapping checks both on load); `amount` as the API writes it (`05`); FK `(city_id, station_id)` to `city_buildings`, cascade; a slot's route may outlive its courier |

- **Ledger (CR-0008):** `balance_after` is the item's **city total** (`04` §16). Coins go through the purse: `COST_*` rows are coins only.
  New reasons (built, parts D and E): `COST_ROAD` (placing and upgrading roads, one row per item and command like every `COST_*`), `SETTLEMENT_DELIVERED` (goods walkers handed to projects, negative,
  per settlement and item) and `SETTLEMENT_REFUNDED` (inputs back when an upgrade started inside the advance). A move between two stores, a
  load and a returned load change no total and write no row. The starting grant is coins only.
- **The reset migration (CR-0008 D10, built in part E as `LogisticsStoresTripsAndReset`):** it deletes every player and everything of theirs (cities and their rows, receipts,
  ledger rows) on the database it runs on, so each dev name logs in to a new starting city; the content numbers stay in the content file.
  It is the one migration that deletes data; the owner approved it (G-Destructive, OD-14).
- **Mapping (CR-0008, built):** a construction site is `Constructing` with `phase_started_at` and `phase_ends_at` null and need rows; an upgrade
  waiting for goods is `Idle` or `Producing` with need rows; every other phase as below.

**Mapping to the engine** (`CityMapping`, M0-20):
- `city_buildings`: a nullable column is null exactly when the building's phase doesn't use it, where the engine keeps 0. Constructing uses
  `phase_started_at`, `phase_ends_at`; Upgrading those and `target_level`; Idle none; Producing `phase_started_at`, `cycle_labor`,
  `labor_remaining`, `segment_started_at`, and `phase_ends_at` unless the cycle is frozen (no workers). A cycle can start at clock 0, so 0
  is never a stand-in for null. `phase` is the API's spelling (`Constructing`, `Upgrading`, `Idle`, `Producing`).
- `cities.research_id`, `research_started_at`, `research_ends_at` are all set (a research is running) or all null.
- `city_research.completed_at` is the city-clock second the research completed: its `research_ends_at`. The engine keeps only the ids;
  the server's city aggregate keeps the clocks and records each one when an advance completes the research.
- `city_stores`: a store row stays at 0 when its goods are taken (an upsert); a good the level no longer holds keeps its row until it is
  empty. `city_project_needs`, `city_research_needs` and `city_trips` rows are deleted when a need is filled or a trip ends; `city_delivery_lines` rows when a line ends or is cancelled.
- A row that breaks these rules or the engine's fails the load; a broken city is never settled and saved over.

A column without `null` in the table is `NOT NULL`; `ledger_entries.ref_type` and `ref_id` are nullable too (a `STARTING_GRANT` has no
reference). `players.id` and `cities.id` are set by the application; `ledger_entries.id` is the only identity.

**Foreign keys:** `cities.player_id` → `players`; `city_buildings`, `city_research`, `city_research_needs`, `city_roads` and `city_trips` →
`cities`, and `city_stores` and `city_project_needs` → `city_buildings`, with `ON DELETE CASCADE`
(the city owns its state); `ledger_entries.city_id` → `cities` and `command_receipts.player_id` → `players` with `ON DELETE RESTRICT` (the audit
trail never disappears with what it belongs to), and `cities.player_id` is `RESTRICT` as well.

**Names:** snake_case everywhere, constraints included: `pk_<table>`, `ux_<table>_<columns>` (unique), `ix_<table>_<columns>`,
`fk_<table>_<principal>_<column>`, `ck_<table>_<column>`.

## Write transaction pattern

```
BEGIN
  SELECT * FROM cities WHERE player_id = @p FOR UPDATE      -- serializes all writes to this city
  (command) SELECT receipt → replay or conflict, then ROLLBACK
  load city_buildings, city_stores, city_project_needs, city_research, city_research_needs, city_roads, city_trips,
       city_couriers, city_courier_loads, city_route_actions, city_delivery_lines
  settle → mutate (if command) → zero-length advance
  UPDATE cities SET …, state_version = state_version + 1 WHERE id = @c AND state_version = @v
  upsert buildings, upsert stores, merge needs, research needs, roads and trips (insert, update, delete), insert research completions,
  insert ledger rows, insert receipt
COMMIT
```

- **Nothing changed** (no time elapsed, the same content, nothing produced, consumed, completed or started): nothing is written, so
  `state_version` and `updated_at` stay; the transaction commits empty. Repeated settles at one instant are free of writes.
- **Business-rule rejection:** the settlement is committed (time passed is real), but there is no mutation and no receipt.
- **Unexpected exception:** full rollback; nothing is committed.
- **Unique violation on the receipt insert** (two identical requests raced): roll back, read the stored receipt, return it (a different
  payload hash: `IDEMPOTENCY_CONFLICT`; no receipt behind it: the exception propagates).
- **As built (M0-26, `CommandPipeline`):** the runner has loaded the child rows with the lock before the receipt lookup runs; a replay or a
  conflict stages nothing, so its commit is empty, the same as a rollback. A successful command always saves, receipt included, even when
  its mutation changed nothing.
- A lock wait timeout (configure ~3 s) → `CONCURRENCY_CONFLICT`; the client retries with the same `requestId`.
  Built as `Database:LockTimeoutMilliseconds` (default 3000), set per transaction as `lock_timeout`.
- The `FOR UPDATE` lock is the primary guard. The `state_version` check is a second guard that turns any unlocked
  write path (a bug) into a loud failure instead of a silent lost update.
- **One entry point (M0-22):** a use case changes a city only through `ICityTransactionRunner.RunAsync(playerId, work)`: it begins the
  transaction, takes the lock and loads the city, runs the work (which saves the city once, `CityTransaction.SaveAsync`), and commits when
  the work returns; an exception rolls everything back. A request runs it once, so it never locks a second city; the repository also
  refuses a load outside a transaction or of a second city; a rollback leaves nothing staged for a later save (M0-24). A lock timeout and a
  stale `state_version` both surface as
  `ConcurrencyConflictException`, which the API maps to `CONCURRENCY_CONFLICT`. The modules: `docs/modules/CityBuilder.Application.md`,
  `CityBuilder.Infrastructure.md`.

**The first login (M0-25):** one transaction inserts the player, the starter city (all its rows, `state_version` 1) and one `STARTING_GRANT`
ledger row for the starting purse (coins only since CR-0008), so the ledger reconciles with the totals from its first row. It is the one city write without the
row lock (there is no row yet, ADR-0009) and goes through `INewCityRepository`, which only `DevLogin` holds. Two first logins of one name:
`unique(dev_name)` refuses the second insert (`DuplicateKeyException`), and that login returns the first one's player.

## Lock ordering (binding now, matters from M2)
1. The acting player's own city row. **Never lock another player's city.**
2. Then shared aggregates (per-item market lock, guild row).
3. Then rows inside them in ascending id.
Effects for other players go to `mailbox_deliveries` and are applied by their own next settle. Why, and why this can't deadlock:
`docs/adr/ADR-0009-row-lock-own-city.md`; concurrent settles are tested there (M0-24).

## Planned tables (not M0 — for orientation)
`accounts` (M1), `market_orders`, `market_trades`, `mailbox_deliveries`, `item_price_history`, `guilds`,
`guild_members`, `guild_projects` (M2). See `26-open-market.md`.

## EF Core conventions
- Mappings live in `Infrastructure` (`IEntityTypeConfiguration<T>`); there are no attributes on domain types.
- Persistence models are separate from Simulation types. Mapping is explicit and round-trip tested.
- Use snake_case naming, written out in each configuration. Migrations are committed. Never edit an applied migration; add a new one
  (`docs/how-to/add-migration.md`).
- Migrations are applied at startup only in Development (`Database:ApplyMigrationsAtStartup`); elsewhere on purpose, never as a side effect.
- Read paths that don't settle (e.g. content) use `AsNoTracking()`.
- Integration tests run against a real PostgreSQL 18.6 container (Testcontainers), never SQLite or InMemory.

## Docker note
The official `postgres:18` images changed their data directory layout. Mount the volume at `/var/lib/postgresql`
(not `/var/lib/postgresql/data`), and verify this against the image docs when setting up. Verified for `postgres:18.6` in M0-19: the image
declares `/var/lib/postgresql` as its volume and keeps the data in `/var/lib/postgresql/18/docker` (`docker-compose.yml`).
