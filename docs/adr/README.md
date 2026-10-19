# Architecture Decision Records

Architectural or hard-to-reverse decisions, one file each (`ADR-NNNN-<slug>.md` from `TEMPLATE.md`).
Every ADR also has a summary row in `core-instructions/12-decisions.md`. Smaller decisions live in the build log.
ADRs are written by the tasks that make or confirm the decision (M0-06, M0-18, M0-24, …).

## Index
| ADR | Title | Status |
|---|---|---|
| [ADR-0001](ADR-0001-server-authoritative.md) | The server is the authority; the client only proposes (decision 001) | Accepted |
| [ADR-0002](ADR-0002-postgresql-only.md) | PostgreSQL is the only source of truth; no Redis in M0 (decision 002) | Accepted |
| [ADR-0003](ADR-0003-command-endpoints.md) | One HTTP endpoint per command; no realtime channel (decision 003) | Accepted |
| [ADR-0004](ADR-0004-idempotency.md) | Idempotent commands through `(playerId, requestId)` receipts (decision 004) | Accepted |
| [ADR-0005](ADR-0005-settle-on-access.md) | Settle on access: a deterministic event simulation, run when a request touches the city (decision 005) | Accepted |
| [ADR-0006](ADR-0006-production-cap-dormant-tail.md) | Production cap per absence: timers always run, production stops after the cap (decision 006) | Accepted |
| [ADR-0007](ADR-0007-shared-engine.md) | One shared engine: the server runs it for authority, the client for display only (decision 007, with its fallback) | Accepted |

Planned: ADR-0008 (labor model) in M0-18, ADR-0009 (row lock on the own city) in M0-24.
