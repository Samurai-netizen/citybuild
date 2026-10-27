# Security Model

> **Built so far:** the rules the engine enforces (integers only, every mutation checked before it changes anything; M0-08 to M0-17),
> OpenAPI only in Development (tested), and authentication: signed access tokens, the dev login in Development only, `ICurrentPlayer`
> (M0-25, tested); the command pipeline with its receipts (M0-26, tested with fakes and on PostgreSQL). Command endpoints, limits and the
> rest of the `Security_` tests arrive from M0-27 on (review M0-30, attack suite M0-74, report M0-78) *(planned)*. A task that builds a part
> updates this line.

**Goal for M0:** a modified client, a replayed request or a wrong clock cannot create resources, skip costs,
skip waiting, over-assign workers, skip research, or touch another player's city.
From Era II this matters even more: one duplication bug floods the shared market and hurts every player.

## Trust boundaries

| Input | Trusted? | Rule |
|---|---|---|
| Access token | Verified | Signed JWT (HMAC-SHA256 only, this server's issuer and audience, lifetime checked against the server clock); `PlayerId` comes only from its `sub` claim (`ICurrentPlayer`) |
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
| Replay a successful command | `(playerId, requestId)` receipt with a unique constraint | Replay returns the same response, ledger unchanged: `ExecuteAsync_SameRequestAgain_ReturnsTheStoredResponse`, `ExecuteAsync_IdenticalRequestCommittedFirst_ReturnsItsResponse` (Application); over HTTP M0-27 |
| Same `requestId`, different payload | Payload hash comparison (the command type is part of the payload) | `IDEMPOTENCY_CONFLICT`: `ExecuteAsync_SameRequestIdOtherPayload_IsAConflict`, `ExecuteAsync_SameRequestIdOtherCommand_IsAConflict` |
| Read another player's response with their `requestId` | The receipt's key includes the player, from the token | `Security_AnotherPlayersRequestId_FindsNoReceipt`, `ExecuteAsync_AnotherPlayersRequestId_RunsAsNew` |
| A command that writes without the receipt or the settle | Commands implement `ICityCommand` and see only the city state and content; only `SettleCity` and `CommandPipeline` hold the city transaction | `Types_OfApplication_OnlyTheSettleAndThePipelineDependOnTheRunner` |
| Race two commands (double spend) | `FOR UPDATE` row lock + `state_version` guard | Concurrent integration tests |
| Act on another player's building | Building ids are resolved inside the caller's city only | `BUILDING_NOT_FOUND`, never a `FORBIDDEN` leak |
| Spam requests or settle storms | Per-player rate limits | 429 tests |
| Huge or malformed bodies | Body size limit; strict model validation before DB access | Malformed-input tests |
| Dev login in production | Endpoint mapped only in Development with `Auth:DevLoginEnabled` on; the setting on outside Development stops the start | `Security_DevLoginInProduction_IsNotMapped` (404), `Security_DevLoginEnabledOutsideDevelopment_StopsTheStart` |
| Act as another player without their token | Every `/api/v1` endpoint requires a token (route group); the player comes only from the token's `sub`, never from a body | `GetCurrentPlayer_TokenTheServerIssued_ReturnsItsPlayer`, `Security_RequestWithoutToken_IsRefused` |
| Forge, alter or replay an old token | Signature checked with the server's key, HS256 only (unsigned tokens refused), issuer and audience checked, expiry by the server clock with no skew | `Security_TokenWithWrongSignature_IsRefused`, `Security_UnsignedToken_IsRefused`, `Security_TokenOfAnotherIssuer_IsRefused`, `Security_ExpiredToken_IsRefused`, `Security_MalformedToken_IsRefused` |
| Weak or committed signing key | `Auth:SigningKey` required in every environment, at least 32 bytes, from user-secrets or the environment; the start stops otherwise | `Start_NoOrShortSigningKey_FailsValidation` |
| Two first logins of one name create two players | `unique(dev_name)`; the second insert fails, and the login returns the first player | `PostDevLogin_SameNameCommittedFirstByAnotherRequest_ReturnsThatPlayer` |
| Dev time scale left on | `Simulation:DevTimeScale` is honoured only in Development; startup throws if ≠ 1 elsewhere | Startup-guard test |
| Overflow to wrap quantities | `checked` arithmetic, `long`, content validation of maxima | Overflow tests |
| Secret leakage | Signing key and the database connection string from environment/user-secrets; never in Unity or the repo (the compose password lives in the gitignored `.env`) | Config review prompt |
| Information leakage in errors | ProblemDetails without stack traces outside Development | Error-mapping tests |
| Probe server internals through `/health` (no auth) | The response is the status word only: no check details, versions or exception text | `GetHealth_AppRunning_Returns200WithStatusOnly` |
| Read the full API description in production | OpenAPI (`/openapi/v1.json`) is mapped only in Development | `Security_OpenApiDocument_InProduction_IsNotServed` |

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
- Dev auth only: no real account system and no token revocation; a token stays valid until it expires (12 h by default), and changing
  `Auth:SigningKey` is the only way to end every token at once.
- No device attestation (Play Integrity / App Attest). A scripted client can automate play, but only within server rules,
  so it gains no more than a perfect human player would. That's acceptable for M0, but not once the market exists.
- Rate limits are in-memory and single-instance.
- HTTP is allowed for localhost in development; production requires HTTPS.

## Rule for Claude
When adding an endpoint or command, add its rows to the threat table above and a test for each.
Never describe the system as "unhackable".
