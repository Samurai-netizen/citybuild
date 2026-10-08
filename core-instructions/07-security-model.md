# Security Model

**Goal for M0:** a modified client, a replayed request or a wrong clock cannot create resources, skip costs,
skip waiting, over-assign workers, skip research, or touch another player's city.
From Era II this matters even more: one duplication bug floods the shared market and hurts every player.

## Trust boundaries

| Input | Trusted? | Rule |
|---|---|---|
| Access token | Verified | Signed JWT; `PlayerId` comes only from its claims |
| Request body | No | Shape-validated, then only *intent* is read (type id, x, y, building id, worker count, research id) |
| Client clock | Never used | Not accepted in any request |
| Content numbers | Server only | Costs, recipes, labor, research times and effects come from server content |
| Snapshot on the client | Display only | Client prediction is never sent back |

## Threats in M0 and the protection for each

| Threat | Protection | Test |
|---|---|---|
| Send fake costs, balances, levels, labor or timers | These fields don't exist in DTOs; unknown JSON properties are rejected (400, `JsonUnmappedMemberHandling.Disallow`) | Attack suite: extra fields → 400, state unchanged |
| Change the device clock to speed things up | Server clock only; elapsed time is computed from the DB timestamp | Time-cheat tests with `FakeTimeProvider` |
| Assign more workers than exist, or use workers twice | Server checks free workers and level limits; progress-preserving labor makes moving workers mid-cycle gain nothing | Worker attack tests; labor invariant (property tests) |
| Place locked buildings or skip research prerequisites | Unlock and prerequisite checks in engine rules | `BUILDING_LOCKED`, `RESEARCH_LOCKED` tests |
| Start two researches by racing | Row lock on the city; a single research slot is checked inside the lock | Concurrency suite |
| Replay a successful command | `(playerId, requestId)` receipt with a unique constraint | Replay returns the same response, ledger unchanged |
| Same `requestId`, different payload | Payload hash comparison | `IDEMPOTENCY_CONFLICT` |
| Race two commands (double spend) | `FOR UPDATE` row lock + `state_version` guard | Concurrent integration tests |
| Act on another player's building | Building ids are resolved inside the caller's city only | `BUILDING_NOT_FOUND`, never a `FORBIDDEN` leak |
| Spam requests or settle storms | Per-player rate limits | 429 tests |
| Huge or malformed bodies | Body size limit; strict model validation before DB access | Malformed-input tests |
| Dev login in production | Endpoint mapped only when `Environment == Development`; startup guard throws otherwise | Production-mode test returns 404 |
| Dev time scale left on | `Simulation:DevTimeScale` is honoured only in Development; startup throws if ≠ 1 elsewhere | Startup-guard test |
| Overflow to wrap quantities | `checked` arithmetic, `long`, content validation of maxima | Overflow tests |
| Secret leakage | Signing key from environment/user-secrets; never in Unity or the repo | Config review prompt |
| Information leakage in errors | ProblemDetails without stack traces outside Development | Error-mapping tests |

## Future threats (design now, build with the market and guilds — see `26`)
| Threat | Planned protection |
|---|---|
| Duplicating goods through market races | Per-item advisory lock, escrow, idempotent orders, ledger auditor, per-item kill switch |
| Alt accounts funneling wealth to a main | Era II + account-age gate, price bands, trade-graph monitoring, no direct gifting |
| Real-money trading | Terms of service, large one-sided trade alerts, bans |
| Market bots and order spam | Order limits, listing fee, rate limits, time-priority matching |
| Account theft | Real accounts with platform sign-in (M1), session revocation |

## Logging policy
Log: player id, request id, trace id, command type, result code, duration, settlement size.
Never log: tokens, signing keys, full request bodies in production, personal data.

## Known limitations in M0 (document, don't hide)
- Dev auth only: no real account system and no token revocation.
- No device attestation (Play Integrity / App Attest). A scripted client can automate play, but only within server rules,
  so it gains no more than a perfect human player would. That's acceptable for M0, but not once the market exists.
- Rate limits are in-memory and single-instance.
- HTTP is allowed for localhost in development; production requires HTTPS.

## Rule for Claude
When adding an endpoint or command, add its rows to the threat table above and a test for each.
Never describe the system as "unhackable".
