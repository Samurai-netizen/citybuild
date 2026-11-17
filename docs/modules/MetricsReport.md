# MetricsReport

**Path:** `tools/MetricsReport/` · **Kind:** .NET project (console, net10.0) · **References:** `CityBuilder.Infrastructure` (the EF Core
model and the content loader), `CityBuilder.Simulation` (content types) · **Referenced by:** `MetricsReport.Tests`, the PostgreSQL tests in
`CityBuilder.IntegrationTests` (`Metrics/`)

## Purpose
MetricsReport answers "what did the players do, how many reached each checkpoint, and where do the ones who left stop?"
([CR-0009](../changes/CR-0009-player-metrics.md), part 4). It reads the player events that the server records (`player_events`,
`core-instructions/06-data-model.md`) **read-only**, and the checkpoint file `server/analytics/checkpoints.json`, and writes one
self-contained HTML page. It is a developer tool: never deployed, no endpoint, and nothing in the game reads what it computes.
*(In progress, CR-0009-T2: the command line exists; the file, the numbers and the page follow.)*

## Key types
| Type | Responsibility |
|---|---|
| `CommandLine` | parses the options (`--help` lists them) and runs the report; exit codes 0 written, 1 bad input, 2 database |

## Rules and invariants
- Ids only: no dev name or other personal data is ever read into the page (owner's answer to the CR's question 3).
- The tool only reads: one read-only transaction.

## How to extend
A new checkpoint is a line in `server/analytics/checkpoints.json`, no code.

## Tests
`tools/MetricsReport.Tests` (no database): `dotnet test tools/MetricsReport.Tests`.

## Known limitations
- Everything is loaded and computed in memory: fine for M0 to M2 sizes.

Last updated: CR-0009-T2
