# ADR-0009 — Every write row-locks only the acting player's own city

- **Status:** Accepted
- **Date:** 2026-10-27 (decided with the kit; written down and confirmed by tests in M0-24) · **Task:** M0-24 · **Decision row:**
  `core-instructions/12-decisions.md` #008

## Context
- Every request that touches a city settles it to now and may change it, all in one transaction (ADR-0005). A poll, a command and a retry
  of the same player can arrive at once (two devices, a retry after a timeout, the 2-minute poll during a tap).
- Two requests that both read the city, advance it and write it back would count the same production twice or lose one of the writes.
  The engine is exact only if each settle starts from the state the one before committed.
- From Era II players trade on an open market (`26`) and later join guilds. A transaction that locked two cities could deadlock with one
  that locked them the other way round, and a player's city would wait for a stranger's request.
- PostgreSQL is the only store (ADR-0002), and the default isolation, read committed, is what EF Core and Npgsql use.

## Decision
Each request runs one transaction, and the first thing it does is lock the acting player's city row with `SELECT … FOR UPDATE`. It never
locks another player's city. Effects on other players go to a mailbox that their own next settle applies (decision 016). Details:
- **One entry point:** `ICityTransactionRunner.RunAsync(playerId, work)` (decision 038): begin, set `lock_timeout`, lock and load the city,
  run the work, which saves the city at most once, then commit. Any exception rolls back. A lock wait beyond `Database:LockTimeoutMilliseconds`
  (default 3 s) is `CONCURRENCY_CONFLICT`, and the client retries with the same `requestId`.
- **Read committed is enough:** a request waiting for the lock gets the row as the request before it committed (PostgreSQL re-reads the
  newest version for `FOR UPDATE`). It loads the children after the lock and reads the server clock inside the transaction, so it settles
  only the time since that commit.
- **Second guard:** `state_version` is a concurrency token. An `UPDATE` that finds another version means something wrote the city without
  its lock (a bug), and it fails loudly instead of losing an update.
- **Guards against a bypass:**
  - a runner runs once per request;
  - the repository refuses a load outside a transaction or of a second city;
  - an architecture test allows only the runner to hold `ICityRepository`;
  - a rollback clears the request's change tracker, so a failed save can't be written later without the lock (M0-24).
- **Lock ordering (binding now; the market needs it from M2, `06` → Lock ordering):**
  1. The acting player's own city row, first and only once. Never another player's city.
  2. Then shared aggregates, one class after the other and each in ascending key order. The first class is the market's per-item lock
     (`pg_advisory_xact_lock(item_key)`, `26`). A feature that adds another class (the guild row) fixes its place in this list in its own
     ADR.
  3. Then rows inside them in ascending id (orders, members).
  A transaction never takes a lock of an earlier step after one of a later step. Every transaction holds at most one city lock and takes
  the others in one global order, so no cycle of waits, and no deadlock, can form.
- **The one write without a city lock:** a new player's city is an insert, so there is no row to lock yet. `unique(player_id)` makes a
  second insert for the same player fail loudly. M0-25 builds this path; it locks nothing else.

## Alternatives considered
| Option | Pros | Cons | Why not |
|---|---|---|---|
| `SERIALIZABLE` isolation, retry on serialization failure | No explicit locks; PostgreSQL finds every conflict | Every conflict costs a whole settle plus a retry; retries everywhere in the pipeline; failures are harder to read than a lock wait | A row lock gives the same safety for one aggregate with no retries |
| Optimistic only: `state_version` check, no lock, retry on conflict | No waiting in the database | Concurrent polls and commands of one player conflict often (each settle writes); a command's retry must replay its whole decision | Kept as the second guard, not the primary one |
| A transaction-scoped advisory lock per player | Doesn't touch the row | A second lock namespace to keep in order; it doesn't protect a write that forgets it, the row lock comes with loading the row | The row is loaded anyway; its own lock is the simplest key |
| One writer per city (a queue or an actor) | No database locks | A process or queue to run and route to; ADR-0002 rules out new infrastructure in M0 | Not needed at M0 scale |
| Lock both cities in id order for a trade | An atomic move of goods between players | Ties one player's request to another's city; every cross-player feature has to get the order right; a busy seller blocks buyers | Escrow and the mailbox give the same result without it (`26`) |

## Consequences
- **Easier:** reasoning per request is local: one city, one lock, one save. Requests on different cities never wait for each other, so the
  load spreads across cities (tested with 20 at once). Concurrent requests of one player run one after the other and end exactly as if they had
  come in order.
- **Harder:** cross-player features can't change two cities atomically. They need escrow, a mailbox and timed deliveries (`26`, decision 016).
  A request holds its city's lock for its whole duration, so the settle must stay fast (`04` §12) and no work outside the database (HTTP
  calls, waits) may run inside the transaction.
- **Watch:** lock timeouts and `CONCURRENCY_CONFLICT` counts in the logs (M0-35); connection pool size against parallel requests; a new use
  case that needs another lock follows the ordering above or gets a new ADR.
- **As built:**
  - M0-22: the runner and the guards.
  - M0-23: the settle inside it.
  - M0-24: tests on PostgreSQL in `ConcurrencyDatabaseTests`:
    - 20 settles of one city queued behind its lock, at once or in waves while time passes, end as one settle at the final time;
    - settles and transactions of other cities don't wait for a held lock;
    - a rollback leaves nothing staged.
  - The guards are tested in `CityTransactionDatabaseTests` and `ApplicationDependencyTests`.
- **Reversal:** the runner is the only place that takes the lock, so another strategy (`SERIALIZABLE`, an advisory lock) would change one
  class and its tests. Dropping the "own city only" rule would need a new ADR covering deadlocks and the market design.
