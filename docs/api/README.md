# API operations

How to run and watch the API in practice: finding a request in the logs, what each log line means. The contract itself (endpoints, DTOs,
error codes) is normative and lives in `core-instructions/05-api-contract.md`; what may be logged is `core-instructions/07-security-model.md`
→ Logging policy.

| Page | Answers |
|---|---|
| [logs-and-correlation.md](logs-and-correlation.md) | `traceId` vs `requestId`; how to find a player's report in the log; every event id the server writes and its values |
| `smoke.http` | Requests to try by hand (a REST client file, M0-36): the login, the content, the city, golden scenario #1 command by command, a replay and a few refusals; its header says how to run it |

The same golden scenario runs automatically over HTTP in the integration tests (`docs/testing/README.md`).
