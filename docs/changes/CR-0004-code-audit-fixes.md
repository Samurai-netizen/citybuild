# CR-0004 — Whole-code audit: bugs, edge cases and security fixes

- **Status:** In progress
- **Type:** Bug
- **Requested by:** owner · **Date:** 2026-10-31
- **Milestone / era:** M0 / Era I
- **Conflicts with pillars (`22`) or the era rule (`25`):** none

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Problem / goal
The owner asked for a scan of the whole code, system by system, and for every bug, error, edge case and security vulnerability found to be
fixed. The audit read the engine, the server, the Unity client and the repository's configuration, and probed a running server. These are the
defects it confirmed; the build log entry has the evidence for each.

| # | Area | Defect | What a player or the project sees |
|---|---|---|---|
| 1 | Server | A valid token of a player without a city reaches the city lock, which throws | 500 `INTERNAL_ERROR` on every city request until the token expires (up to 12 h), and an Error log line each time |
| 2 | Client | `StartupFlow` retries a refused kept token only on `GET /content`, which reads no database | With defect 1 fixed, a kept token of a deleted player would still fail the start, and a failed start never polls |
| 3 | Client | After a re-login the store refuses the new city's snapshot, because its `stateVersion` is lower | The old city stays on screen, labelled `Connected`, until the app restarts |
| 4 | Client | Requests follow redirects with the bearer token | A redirect from a captive portal or a proxy receives the token, and a POST turns into a GET: measured against a test server, the request went on to the other path until Unity's redirect limit |
| 5 | Client | `DisplayCity` catches only `InvalidOperationException` | A snapshot of other content throws from `Update` every frame, and fails a command that succeeded |
| 6 | Client | `AuthService` stays `LoggingIn` after an unexpected exception | Every later login and `Forget` throws, so the client never recovers |
| 7 | Client | `CommandSender` finishes a command after it tells the listeners | A listener that throws leaves the command's task unfinished and its stepper lane blocked |
| 8 | Client | An unexpected exception in a poll is dropped with the discarded task | A failing poll leaves no trace |
| 9 | Client | The status label reads the coins only when the connection state changes | The coins shown are out of date while connected |
| 10 | Server | A body without a `Content-Type` is 400 | The contract says 415 (`05` → Request limits) |
| 11 | Server | A signed token whose `sub` is no player id passes authentication | 500 instead of the 401 of every other bad token |
| 12 | Server | `/health` takes every method, the answers name the server software, and nothing forbids caching a response made for one player | Contract mismatch and needless information; a shared cache could keep a player's snapshot |
| 13 | Engine | Content values that reach `smallint` columns (grid size, workers) have no upper limit | Content that loads can fail with a 500 on the first save |
| 14 | Handbook | Known vulnerabilities in the site's npm packages | Build-time only, the published site is static; 31 reported by `npm audit` |

The audit also suspected that `UnityWebRequest.Abort` fails when a timer cancels the request on another thread. Measured against a server that
never answers: the request ended in 0.21 s with no error in the log, in the Editor. So that is no defect, and its test stays as coverage.

## Proposal
Fix each defect with a regression test, in small steps. No game rule changes. Contract changes are limited to making an unexpected 500 the
401 a client can act on, and to the 415 the contract already promises.

## Acceptance criteria
- [ ] Each defect above has a test that failed before its fix, except where the build log says why that isn't possible.
- [ ] Build, format check, fast suite, integration suite and Unity tests pass; `check-docs.sh` and the handbook build pass.
- [ ] `05`, `07`, `04` and the module docs describe the new behaviour.
- [ ] The task report lists what was fixed, what was only reported and what couldn't be verified.

## Out of scope
- Protected files (`15`): `.github/workflows/**`, `scripts/dev/**`, hooks. Findings there are reported and wait for the owner.
- Game design: findings are reported, not changed (G-Design).
- Major version changes or new packages (G-Dependency).

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | One stricter content rule; no change to the simulation | `Content/ValueRules.cs`, `04` §14 |
| Content (`23`, JSON) | None: the M0 content is far below the new limits | — |
| API (`05`) | A token of a player without a city and a token without a player id are 401; a body without a content type is 415; `/health` takes GET and HEAD; `Cache-Control: no-store`, no `Server` header | `05`, `docs/modules/CityBuilder.Api.md` |
| Data / migration (`06`) | None | — |
| Security (`07`) | New threat rows | `07` |
| Client (`09`) | Transport, start flow, store, monitor, auth service, command sender, display city, `GameRoot` | `docs/modules/CityBuilder.Client.*.md` |
| Docs (recipes in `19`) | Per the Docs impact table of the build log entry | — |
| Tests (`08`) | A regression test per defect; one existing test changes its expected answer (the build log gives the reason, G-Tests) | `ExceptionHandlerTests` |

## Risks and alternatives
- A 401 for a player without a city makes a client log in again and get a new, empty city. That is right after a reset development database. In
  production a player has no way to lose their city today.
- Alternative for defect 3, a city id in the snapshot: a contract change, and it would not help a client that is already running. Not chosen.

## Tasks (filled in after approval)
| ID | Title | Model | Status |
|---|---|---|---|
| CR-0004-T1 | The fixes of the whole-code audit | O | In progress |

## Owner approval
- Approved on: 2026-10-31. "Scan the whole code, whole systems one by one, and try to find and fix as many bugs, errors, edge-cases, security vulnerabilities as possible."
