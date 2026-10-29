# Logs and correlation

The server writes one JSON line per log entry on stdout (`docs/getting-started.md` → Configuration). This page says how a line is tied to
a request and lists the events of the server's own code (M0-35). The rules for what may be logged are `core-instructions/07-security-model.md`
→ Logging policy: never a token, a signing key, a body, a display name or a remote address.

## `traceId` vs `requestId`
| | `traceId` | `requestId` |
|---|---|---|
| Who makes it | The server, for every HTTP request (W3C trace context) | The client, once per command; a retry sends the same one (`05` → Idempotency) |
| Where the client sees it | In every error answer (Problem Details), as `00-<trace id>-<span id>-<flags>` | In the command body, in the snapshot of a command, and in its error answers |
| Where the log has it | Every line written during the request, in the scope value `TraceId`: the 32 hex digits of the second part | The command lines (5000–5003), as the named value `RequestId` |
| What it identifies | One HTTP request: two retries of a command have two trace ids | One command: all its attempts, its receipt and its ledger rows (`ref_id`) |

- **A player's error report:** take the `traceId` of the answer, cut out the second part (`00-3cc1…a6a6-…` → `3cc1…a6a6`) and search the
  `TraceId` scope value. Every line of that request has it: the settlement, the command's outcome, a refusal, an exception.
- **A command that "didn't work":** search the named value `RequestId` for the command's request id. It finds every attempt (accepted,
  rejected, replayed, conflict); each attempt's `TraceId` then finds the rest of its lines.
- **Not the same `RequestId`:** ASP.NET Core adds a scope value of its own also called `RequestId` (the connection's request number, such as
  `0HNPLQPQGB5G1`) next to `RequestPath`. It is not the command's. The command's request id is in the line's named values (`State` in the
  JSON), the framework's in its `Scopes`.
- A successful `GET /city` or `GET /content` answer carries no trace id; its lines are found by `PlayerId` and time.

## Events
Every line of the server's own code has an event id from the block of its feature (decision 029 in `core-instructions/12-decisions.md`).
`PlayerId` is the token's player (empty when the request had no working token). `DurationMs` is real time since the endpoint started its work,
to 0.1 ms; a Development fast clock doesn't change it.

| Id | Level | Written when | Named values |
|---|---|---|---|
| 1000 | Information | Content loaded at startup | `ContentVersion`, `ContentFilePath` |
| 2000 | Information | Migrations applied at startup (Development) | `AppliedCount`, `AppliedMigrations` |
| 3000 | Error | An unexpected exception became `INTERNAL_ERROR` (with the exception and its stack) | `Method`, `Path`, `PlayerId` |
| 3001 | Warning | A request was refused with `RATE_LIMITED` | `LimitClass` (`Commands`, `City`, `Content`, `DevLogin`), `Method`, `Path`, `PlayerId`, `RetryAfterSeconds` |
| 3002 | Warning | `CONCURRENCY_CONFLICT` outside a command (`GET /city`) | `Method`, `Path`, `PlayerId` |
| 4000 | Information | A dev login issued a token | `PlayerId`, `IsNewPlayer` |
| 4001 | Information | A dev login was refused for its body | `Code` |
| 4002 | Debug | A request's access token was accepted | `PlayerId` |
| 4003 | Warning | A request was refused with 401 | `Method`, `Path`, `Reason` (`NoToken`, or the validation failure's type, such as `SecurityTokenInvalidLifetimeException` for an expired token) |
| 5000 | Information | A command was applied and stored | `Command`, `RequestId`, `PlayerId`, `DurationMs` |
| 5001 | Information | A command was refused | `Command`, `RequestId`, `PlayerId`, `Code` (a rule's code, `INVALID_ARGUMENT` or `IDEMPOTENCY_CONFLICT`), `DurationMs` |
| 5002 | Information | A retried command was answered from its receipt (nothing ran) | `Command`, `RequestId`, `PlayerId`, `DurationMs` |
| 5003 | Warning | A command hit `CONCURRENCY_CONFLICT` (nothing committed) | `Command`, `RequestId`, `PlayerId`, `DurationMs` |
| 6000 | Information | A city was settled (every `GET /city`, and every command that got past the receipt check, before its 5000 or 5001 line) | `PlayerId`, `Trigger` (`city` or the command type), `ElapsedSeconds`, `ProductionSeconds`, `Events` (constructions, upgrades and research completed, an era entered), `ResearchCompleted` (ids, comma-separated), `DurationMs` |
| 6001 | Warning | The server clock was behind the city's last settled second, so nothing elapsed (`04` §1) | `PlayerId`, `SecondsBehind` |
| 7000 | Warning | The Development fast clock is on (startup) | `TimeScale`, `ServerTime` |

Not logged by the server's code: a body that doesn't bind (the answer says what is wrong; the framework logs it at Debug), an accepted token
at the default level (Information), anything of a request body. The framework's own lines (`Microsoft.*`) follow `Logging:LogLevel`.

## Where it is built
The log classes are `<Feature>Log` next to the code that logs (`docs/modules/CityBuilder.Api.md`); the tests that hold these events and the
"never logged" rules are `server/tests/CityBuilder.IntegrationTests/Logging/` and `Auth/DevLoginDatabaseTests`.
