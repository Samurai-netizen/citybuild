# ADR-0004 — Idempotent commands through `(playerId, requestId)` receipts

- **Status:** Accepted
- **Date:** 2026-10-04 (decided with the kit; written down 2026-10-14) · **Task:** M0-06 · **Decision row:** `core-instructions/12-decisions.md` #004

## Context
- On a mobile network a command can reach the server while its response is lost. The client cannot tell this from a request that never
  arrived, so it retries. Without protection the retry places a second building or pays a cost twice.
- Two copies of the same request can also arrive at the same time (a retry that overtakes a slow original).
- `core-instructions/02-principles.md` rule 9: every state-changing command carries a `requestId`, retries reuse it, and it never applies twice.
- Commands are separate HTTP endpoints that all run through one pipeline (ADR-0003), under a row lock on the acting player's city (decision 008).

## Decision
The server keeps a **receipt for every successful command**, keyed by `(player_id, request_id)`, with the command type, a SHA-256 hash of
the canonical payload and the stored response (`command_receipts`, `core-instructions/06-data-model.md`). Rules (`05-api-contract.md`,
idempotency rules):
- `requestId` is a client-generated UUID, required in every command body, created once per player intent and reused on every retry
  (`core-instructions/09-unity-client.md`, `CommandSender`).
- The receipt lookup runs inside the transaction, after the own-city row lock and before the settle (`03`, request lifecycle, step 4).
  Same key and same hash: return the stored response, execute nothing, roll back. Same key and a different hash: `IDEMPOTENCY_CONFLICT` (409).
- Business-rule rejections store no receipt. The settlement of that request is still committed (decision 014), and the player's next
  attempt uses a new `requestId`.
- If two identical requests race, the row lock serializes them. A unique violation on the receipt insert rolls back, reads the stored
  receipt and returns it (`06`).
- The key includes the player id, so another player's `requestId` never matches yours.

## Alternatives considered
| Option | Pros | Cons | Why not |
|---|---|---|---|
| No idempotency; the client never retries | Nothing to build | A lost response leaves the player unsure, and a manual retry pays twice | Breaks rule 9 |
| Store receipts for rejections too | A retry of a rejected command gets the same error | A rejection depends on the state at that moment; a stored one would block a retry that is now valid; more rows | A rejection changes nothing but the settlement, so repeating it is harmless |
| Rely on commands that are naturally idempotent | No storage | Only `assign-workers` (an absolute count) is; placing, upgrading and starting research are not | One mechanism for every command |
| Key by `requestId` alone | Simpler key | A guessed or colliding id would return another player's response | The key includes the player id (`07`) |
| The `requestId` in an HTTP header (an `Idempotency-Key` style header) | Can be handled by middleware | Optional by nature, and separate from the payload that is hashed | A body field is validated with the rest of the request DTO, so it is never missing |

## Consequences
- **Easier:** any command can be retried safely with the same `requestId`, so the client's retry policy (network errors, timeouts, 5xx,
  `CONCURRENCY_CONFLICT`; `09`) needs no per-command logic. Replays are easy to test.
- **Harder:** the canonical payload must hash the same way every time (field order, number format). A replay returns the response as it was
  then, which can be older than the current state; the client ignores snapshots with a lower `stateVersion` (`09`). Receipts grow without
  bound until a cleanup job exists (`06`, a later milestone).
- **Watch:** every command goes through the pipeline (no handler may write outside it); the receipt is written in the same transaction as
  the state it describes; replay, conflict, other-player and race tests for every command (`19`, "New command").
- **As built (M0-26):** `CommandPipeline` (Application) runs every command: receipt lookup under the row lock, replay and conflict before
  the settle, receipt stored with the city in one save, the receipt race recovered through `DuplicateKeyException`. The canonical payload
  and the `json` column that keeps the response as written are decisions 042 and 041 in `core-instructions/12-decisions.md`. Tested with
  fakes in `CityBuilder.Application.Tests` (`Commands/`) and the lookup on PostgreSQL. On PostgreSQL (M0-27, `CityBuilder.IntegrationTests`,
  `Commands/`, a test command): a retry writes nothing, also after a host restart; conflicts; another player's request id; ten identical
  requests apply once; the receipt race through the unique key; a rejection commits only the settlement. Over HTTP (M0-29, place-building): a replay
  is the same bytes and writes nothing; a conflict is 409 `IDEMPOTENCY_CONFLICT`; ten identical requests place one building; another player's
  request id runs as a new command. Still to come: the error catalogue (M0-34), the client's command sender (M0-40), and the proof of idempotent
  retries (M0-56).
- **Reversal:** storage details (for example a retention period) can change without touching the API. Dropping receipts would break
  rule 9 and is not an option while commands change state.
