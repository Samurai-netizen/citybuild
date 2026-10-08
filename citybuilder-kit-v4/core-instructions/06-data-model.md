# Data Model (PostgreSQL 18)

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

Derived on load, never stored: reserved outputs, stall reasons, workers provided/free, builders busy, effective caps and labor.

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

- **Business-rule rejection:** the settlement is committed (time passed is real), but there is no mutation and no receipt.
- **Unexpected exception:** full rollback; nothing is committed.
- **Unique violation on the receipt insert** (two identical requests raced): roll back, read the stored receipt, return it.
- A lock wait timeout (configure ~3 s) → `CONCURRENCY_CONFLICT`; the client retries with the same `requestId`.
- The `FOR UPDATE` lock is the primary guard. The `state_version` check is a second guard that turns any unlocked
  write path (a bug) into a loud failure instead of a silent lost update.

## Lock ordering (binding now, matters from M2)
1. The acting player's own city row. **Never lock another player's city.**
2. Then shared aggregates (per-item market lock, guild row).
3. Then rows inside them in ascending id.
Effects for other players go to `mailbox_deliveries` and are applied by their own next settle.

## Planned tables (not M0 — for orientation)
`accounts` (M1), `market_orders`, `market_trades`, `mailbox_deliveries`, `item_price_history`, `guilds`,
`guild_members`, `guild_projects` (M2). See `26-open-market.md`.

## EF Core conventions
- Mappings live in `Infrastructure` (`IEntityTypeConfiguration<T>`); there are no attributes on domain types.
- Persistence models are separate from Simulation types. Mapping is explicit and round-trip tested.
- Use snake_case naming. Migrations are committed. Never edit an applied migration; add a new one.
- Read paths that don't settle (e.g. content) use `AsNoTracking()`.
- Integration tests run against a real PostgreSQL 18.6 container (Testcontainers), never SQLite or InMemory.

## Docker note
The official `postgres:18` images changed their data directory layout. Mount the volume at `/var/lib/postgresql`
(not `/var/lib/postgresql/data`), and verify this against the image docs when setting up.
