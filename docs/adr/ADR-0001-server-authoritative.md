# ADR-0001 — The server is the authority; the client only proposes

- **Status:** Accepted
- **Date:** 2026-10-04 (decided with the kit; written down 2026-10-14) · **Task:** M0-06 · **Decision row:** `core-instructions/12-decisions.md` #001

## Context
- CityBuilder is a mobile game. The Unity client runs on the player's device, so it can be modified, scripted, or run with a wrong clock.
- Production runs in real time while the player is away, so "how much time has passed" is itself a resource.
- From Era II, players trade on one shared open market (`core-instructions/26-open-market.md`). Then one duplication bug in a single city
  floods everyone's economy (`core-instructions/07-security-model.md`).
- The genre is asynchronous: a few seconds of round-trip delay is acceptable, but the progress bars and counters must still look live.

## Decision
The server owns all game state and every rule. The client sends **intents** (place this building type at x, y; assign n workers to
building b), and the server checks them, applies them through the shared engine and answers with the authoritative snapshot.
The details are binding rules in `core-instructions/02-principles.md` (rules 1 to 8):
- The client never sends balances, costs, rates, levels, timers, worker totals, timestamps or player ids. The player id comes only from the
  verified token; request bodies carry intent only (`07`, trust boundaries), and unknown JSON fields are rejected (`07`, threats).
- Server time (`TimeProvider`, UTC, whole seconds) is the only clock that drives progress. The client's clock is never accepted.
- Every request that touches a city settles it to "now" first, inside the same transaction (`core-instructions/03-architecture.md`, request
  lifecycle).
- No endpoint sets state directly (`give-coins`, `set-level`, `finish-research`), not even for debugging.
- Game numbers live in server content (`server/content/*.json`), which the client downloads and renders.
- The client runs the shared engine **for display only** and snaps to every server snapshot (decision 007, [ADR-0007](ADR-0007-shared-engine.md)).

## Alternatives considered
| Option | Pros | Cons | Why not |
|---|---|---|---|
| Client-authoritative with save upload (the server stores what the client reports, with sanity checks) | Simplest server; offline play | A modified client can create any amount of resources; sanity checks only narrow the gap | Incompatible with a shared market (`26`) and with the M0 security goal (`07`) |
| Split authority: the client runs production, the server checks only trades | Less server work | Production is the source of everything that gets traded, so a production cheat still floods the market | Same reason; it only moves the hole |
| Server-authoritative with a realtime connection and server ticks | Instant updates, no polling | A persistent connection and a background tick per city; much higher cost for an asynchronous game | Rejected by ADR-0003 (no realtime channel) and decision 005 (settle on access) |

## Consequences
- **Easier:** rules are enforced in one place. A scripted client can only automate legal play, so it gains no more than a perfect human
  player would (`07`, known limitations). The market, guilds and later eras can be added without trusting the client more.
- **Harder:** every action needs a round trip and the game needs a network. The delay is hidden by the display prediction (decision 007);
  retries on flaky networks need idempotent commands (ADR-0004). Every request costs a settle on the server.
- **Watch:** no DTO field that carries state from the client into the server; one `Security_` test per threat for every new command
  (`core-instructions/19-change-recipes.md`); the attack suite (M0-74) and the boundary reviews (M0-30, M0-60).
- **As built (M0-06):** the server registers `TimeProvider.System` as its only clock (`ServerClockTests`). The command pipeline, the
  endpoints and the attack tests arrive from M0-22 on.
- **Reversal:** not planned and very expensive: it would mean redesigning the economy and the security model.
