# Data Model (PostgreSQL 18)

> **Built so far:** the tables below, with their keys, constraints and foreign keys, as the EF Core model and the migration `InitialCreate`
> (M0-19; `docs/modules/CityBuilder.Infrastructure.md`), and PostgreSQL 18.6 in Docker; the mapping to the engine's city state, the
> repositories and the unit of work (M0-20); the row lock and the write transaction, `ICityTransactionRunner` (M0-22); the settle with its
> ledger rows (M0-23). A task that builds a part updates this line.

PostgreSQL is the only source of truth. Wall timestamps are `timestamptz` (UTC). Simulation times and labor
are `bigint` city-clock seconds and worker-seconds. Quantities are `bigint`. Item, building, research and era ids are `text` from content.

## Tables (M0)

| Table | Key columns | Constraints / notes |
|---|---|---|
| `players` | `id uuid pk`, `dev_name text`, `created_at` | `unique(dev_name)`; real accounts come in M1 |
| `cities` | `id uuid pk`, `player_id uuid`, `era smallint`, `clock_seconds bigint`, `last_settled_unix bigint`, `next_building_id int`, `research_id text null`, `research_started_at bigint null`, `research_ends_at bigint null`, `state_version bigint`, `content_version text`, `created_at`, `updated_at` | `unique(player_id)` (one city per player); `check (era >= 1)`; `state_version` is a concurrency token |
| `city_inventory` | `(city_id, item_id) pk`, `quantity bigint` | `check (quantity >= 0)` |
| `city_buildings` | `(city_id, building_id) pk`, `type_id`, `x smallint`, `y smallint`, `level smallint`, `assigned_workers smallint`, `phase text`, `phase_started_at bigint null`, `phase_ends_at bigint null`, `target_level smallint null`, `cycle_labor bigint null`, `labor_remaining bigint null`, `segment_started_at bigint null` | `unique(city_id, x, y)`; checks: `x, y >= 0`, `level >= 1`, `assigned_workers >= 0`, `labor_remaining >= 0` |
| `city_research` | `(city_id, research_id) pk`, `completed_at bigint` | completed nodes only |
| `command_receipts` | `(player_id, request_id) pk`, `command_type`, `payload_hash bytea`, `response_json jsonb`, `created_at` | Successful commands only; cleanup job later |
| `ledger_entries` | `id bigint identity pk`, `city_id`, `item_id`, `delta bigint`, `balance_after bigint`, `reason text`, `ref_type text`, `ref_id text`, `created_at` | `index (city_id, created_at)` |

Ledger reasons in M0: `STARTING_GRANT`, `SETTLEMENT_PRODUCED`, `SETTLEMENT_CONSUMED`, `COST_PLACE`, `COST_UPGRADE`,
`REFUND_UPGRADE`, `COST_RESEARCH`. Later: `MARKET_ESCROW`, `MARKET_PROCEEDS`, `MARKET_FEE`, `MAILBOX_DELIVERY`, `ERA_CHARTER`.
A settlement writes, per item in ordinal order, one `SETTLEMENT_PRODUCED` row (its total output) and then one `SETTLEMENT_CONSUMED` row
(its total input, negative), with `ref_type` = `settlement` and `ref_id` = the settlement's id, a UUID v7 made for it (there is no
settlements table). Each `balance_after` runs on from the quantity before the settlement, so produced first keeps every balance ≥ 0, and the
last one of an item equals its stock.

Derived on load, never stored: reserved outputs, stall reasons, workers provided/free, builders busy, effective caps and labor.

**Mapping to the engine** (`CityMapping`, M0-20):
- `city_buildings`: a nullable column is null exactly when the building's phase doesn't use it, where the engine keeps 0. Constructing uses
  `phase_started_at`, `phase_ends_at`; Upgrading those and `target_level`; Idle none; Producing `phase_started_at`, `cycle_labor`,
  `labor_remaining`, `segment_started_at`, and `phase_ends_at` unless the cycle is frozen (no workers). A cycle can start at clock 0, so 0
  is never a stand-in for null. `phase` is the API's spelling (`Constructing`, `Upgrading`, `Idle`, `Producing`).
- `cities.research_id`, `research_started_at`, `research_ends_at` are all set (a research is running) or all null.
- `city_research.completed_at` is the city-clock second the research completed: its `research_ends_at`. The engine keeps only the ids;
  the server's city aggregate keeps the clocks and records each one when an advance completes the research.
- `city_inventory`: the engine drops an item at 0; its row stays, set to 0 (an upsert, never a delete).
- A row that breaks these rules or the engine's fails the load; a broken city is never settled and saved over.

A column without `null` in the table is `NOT NULL`; `ledger_entries.ref_type` and `ref_id` are nullable too (a `STARTING_GRANT` has no
reference). `players.id` and `cities.id` are set by the application; `ledger_entries.id` is the only identity.

**Foreign keys:** `cities.player_id` → `players`; `city_inventory`, `city_buildings` and `city_research` → `cities` with `ON DELETE CASCADE`
(the city owns its state); `ledger_entries.city_id` → `cities` and `command_receipts.player_id` → `players` with `ON DELETE RESTRICT` (the audit
trail never disappears with what it belongs to), and `cities.player_id` is `RESTRICT` as well.

**Names:** snake_case everywhere, constraints included: `pk_<table>`, `ux_<table>_<columns>` (unique), `ix_<table>_<columns>`,
`fk_<table>_<principal>_<column>`, `ck_<table>_<column>`.

## Write transaction pattern

```
BEGIN
  SELECT * FROM cities WHERE player_id = @p FOR UPDATE      -- serializes all writes to this city
  (command) SELECT receipt → replay or conflict, then ROLLBACK
  load city_buildings, city_inventory, city_research
  settle → mutate (if command) → zero-length advance
  UPDATE cities SET …, state_version = state_version + 1 WHERE id = @c AND state_version = @v
  upsert inventory, upsert buildings, insert research completions, insert ledger rows, insert receipt
COMMIT
```

- **Nothing changed** (no time elapsed, the same content, nothing produced, consumed, completed or started): nothing is written, so
  `state_version` and `updated_at` stay; the transaction commits empty. Repeated settles at one instant are free of writes.
- **Business-rule rejection:** the settlement is committed (time passed is real), but there is no mutation and no receipt.
- **Unexpected exception:** full rollback; nothing is committed.
- **Unique violation on the receipt insert** (two identical requests raced): roll back, read the stored receipt, return it.
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
