# ADR-0003 — One HTTP endpoint per command; no realtime channel

- **Status:** Accepted
- **Date:** 2026-10-04 (decided with the kit; written down 2026-10-14) · **Task:** M0-06 · **Decision row:** `core-instructions/12-decisions.md` #003

## Context
- The genre is asynchronous: a player acts a few times per session and then waits minutes or hours. Nothing in M0 changes because of
  another player.
- The server is the authority (ADR-0001), so every action is a request that the server checks and answers with the new state.
- Mobile apps are suspended and resumed, and their networks drop. A long-lived connection has to be rebuilt after each of these.
- Each action has its own input, its own error codes and its own threats (`core-instructions/05-api-contract.md`, `07-security-model.md`).
  The market and guilds (M2) will add more actions.

## Decision
Every player action is its **own HTTP endpoint**, `POST /api/v1/commands/<action>`, with a typed body that carries a `requestId`
(ADR-0004) and the intent. There is **no realtime channel** (no WebSocket, SignalR or server-sent events).
- Reads are `GET /api/v1/city` (settles first) and `GET /api/v1/content`. Every successful command response carries the full snapshot,
  so the client never needs a second request to see the result.
- The client polls `GET /city` on start, on resume, after a reconnect and every 2 minutes in the foreground
  (`core-instructions/03-architecture.md`).
- All commands run through one pipeline: authenticate → validate the shape → lock the own city → idempotency check → settle → mutate →
  persist → snapshot (`03`, request lifecycle).
- Errors are RFC 9457 Problem Details with a stable `code` (decision 010).
- Market and guild endpoints will live under `/api/v1/market/*` and `/api/v1/guilds/*` and use the same pipeline (`05`, versioning).

## Alternatives considered
| Option | Pros | Cons | Why not |
|---|---|---|---|
| One generic `POST /commands` with a type field (a command bus) | One route; adding a command needs no new endpoint | A type switch at the edge; one shared body shape, so OpenAPI, validation and rate limits cannot be specific per command | Explicit endpoints keep the contract reviewable and the generated reference precise |
| A realtime channel (SignalR or WebSocket) for commands and state pushes | Instant updates; the server can push | Connections to rebuild after every suspend and network change; sticky connections when scaling out; a second protocol to secure | No M0 feature needs a push; every command already returns the snapshot |
| GraphQL | Flexible queries | A new dependency and a second query model | The client always needs the same snapshot |
| Push of progress events (server-sent events, push notifications) | No polling | Production and research complete on their own, and the display prediction already shows them (decision 007) | Push notifications stay a future seam (`03`) |

## Consequences
- **Easier:** each command is one small vertical slice with its own DTO, validator, error codes and tests (`core-instructions/19-change-recipes.md`,
  "New command"). Plain HTTP is easy to test in memory (`WebApplicationFactory`), to rate limit per command, and to try with `curl`.
- **Harder:** changes caused by other players (market fills, M2) appear only on the next poll or command, up to 2 minutes late. The full
  snapshot in every response costs bandwidth as cities grow. Every command adds a route to maintain.
- **Watch:** settle load from polling (each poll settles and writes, ADR-0002); snapshot size; whether 2 minutes is still enough once
  the market exists.
- **As built (M0-26):** `GET /health`, the OpenAPI document in Development, the dev login (M0-25), and the pipeline every command endpoint
  will call (`CommandPipeline`, M0-26). Then the city and content endpoints (M0-28) and the first command endpoint,
  `POST /api/v1/commands/place-building` (M0-29, `CommandEndpoints`), `upgrade-building` (M0-31), `assign-workers` (M0-32) and
  `start-research` (M0-33) on the same pipeline: the four commands of M0. Client polling follows in M0-41. The `/commands` group is one
  rate-limit class (M0-34), so a new command is limited by being in the group, and every error answer is the error catalogue's.
- **Reversal:** a push channel can be added later for notifications only (for example "your order was filled"), while commands stay
  HTTP. Merging the endpoints into one would be a breaking change (`/api/v2`).
