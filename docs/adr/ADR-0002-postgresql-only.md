# ADR-0002 — PostgreSQL is the only source of truth; no Redis in M0

- **Status:** Accepted
- **Date:** 2026-10-04 (decided with the kit; written down 2026-10-14) · **Task:** M0-06 · **Decision row:** `core-instructions/12-decisions.md` #002

## Context
- The server must store each city, its ledger and the command receipts, and change them together or not at all
  (`core-instructions/02-principles.md`, rules 9 to 11).
- The integrity rules rely on database features: a row lock on the acting player's city, a version guard, unique keys for idempotency, and
  check constraints such as "quantity ≥ 0" (`core-instructions/06-data-model.md`). The market (M2) adds per-item locks (`26`).
- The project is built by one owner and a series of AI coders, and M0 runs on one machine. Every extra service is one more thing to run,
  test, secure and keep consistent.
- Many games put Redis next to the database for caches, sessions, rate limits or locks.

## Decision
PostgreSQL 18 is the **only** place that holds game state, and the only stateful service in M0: no Redis, no cache layer, no second store.
- All state, the ledger and the receipts live in one database, and one transaction per request writes them (`06`, write transaction pattern).
- Concurrency is handled by the database: `SELECT … FOR UPDATE` on the own city row plus the `state_version` guard (decision 008; ADR-0009,
  planned in M0-24); later the market's per-item advisory locks (`26`).
- Rate limits are in memory, for one server instance (`core-instructions/07-security-model.md`, known limitations).
- Data access goes through EF Core 10 and Npgsql (approved in `core-instructions/14-coding-standards.md`).
- Integration tests run against a real PostgreSQL 18.6 container (Testcontainers), never SQLite or an in-memory provider (`06`).
- Another store comes only when measurements show the need, through G-Dependency and a new decision row that supersedes #002.

## Alternatives considered
| Option | Pros | Cons | Why not |
|---|---|---|---|
| PostgreSQL plus Redis (cached city state, rate limits, distributed locks) | Faster reads; limits and locks shared by several server instances | A second critical service; a cached city can disagree with the database, which weakens the single authority (ADR-0001) | No measured need. Every city read settles and writes anyway (decision 005), so a cache saves little |
| A document database (one document per city) | A city loads and saves as one object | Weaker constraints and multi-row transactions; the ledger and the market's order books are relational | The integrity rules depend on constraints and row locks |
| SQLite or another embedded database | Nothing to run | No concurrent server writers; not what production would run | Tests must run on what production runs (`06`) |
| Event sourcing | Full history of every change | Much more machinery; replays and projections | The ledger already makes every resource change auditable (rule 11) |

## Consequences
- **Easier:** one transaction covers state, ledger and receipt; constraints turn bugs into loud failures; one service to run locally
  (`docker compose`, M0-19) and to back up.
- **Harder:** every `GET /city` writes (settle on access), so the database carries all the load. Rate limits restart with the server and
  do not work across several instances; scaling out needs a shared store later.
- **Watch:** settle cost (the performance budget, M0-16, and the fallback of decision 005 in `12`); lock waits (`CONCURRENCY_CONFLICT`);
  the PostgreSQL 18 Docker volume layout (`06`, Docker note).
- **As built (M0-06):** no database yet. M0-19 adds the compose file, the EF Core model and the first migration; M0-21 adds the
  Testcontainers base. No other store exists either.
- **Reversal:** a cache or shared rate limiter can be added later behind an Application port without moving the source of truth.
  Replacing PostgreSQL itself would be expensive (migrations, locks, SQL in tests).
