# MetricsReport

**Path:** `tools/MetricsReport/` · **Kind:** .NET project (console, net10.0) · **References:** `CityBuilder.Infrastructure` (the EF Core
model and the content loader), `CityBuilder.Simulation` (content types) · **Referenced by:** `MetricsReport.Tests`, the PostgreSQL tests in
`CityBuilder.IntegrationTests` (`Metrics/`)

## Purpose
MetricsReport answers "what did the players do, how many reached each checkpoint, and where do the ones who left stop?"
([CR-0009](../changes/CR-0009-player-metrics.md), part 4). It reads the player events that the server records (`player_events`,
`core-instructions/06-data-model.md`) **read-only**, and the checkpoint file `server/analytics/checkpoints.json`, and writes one
self-contained HTML page. It is a developer tool: never deployed, no endpoint, and nothing in the game reads what it computes.
*(In progress, CR-0009-T2: the command line, the checkpoint file, the analysis model and the checkpoint explorer exist; the other sections and the page follow.)*

## Key types
| Type | Responsibility |
|---|---|
| `CommandLine` | parses the options (`--help` lists them) and runs the report; exit codes 0 written, 1 bad input, 2 database |
| `CheckpointFile` | reads and checks `checkpoints.json` with `Utf8JsonReader`, so each error names its line; gives `Checkpoint` and `Funnel` records |
| `KnownEvents` | the event types a checkpoint may name: `command.` and every command's wire type (found by reflection), the recorded names, `production.first` |
| `MetricsData` | the players (ids, creation, last seen) and their events as plain records, so every number is computed without a database |
| `CheckpointMatcher`, `ReportCheckpoint` | which events match a checkpoint; the named checkpoints of the file plus one unnamed per distinct `(event_type, detail, outcome)` |
| `PlayerTimeline` | one player's events in `(created_at, id)` order and the reach of a checkpoint: its `atLeast`-th match |
| `ReportContext` | the population's timelines, "now" (the later of the clock and the newest last seen), who left, the least sample |
| `Explorer`, `ExplorerRow`, `ExplorerDetail` | the checkpoint explorer: one row per checkpoint with the columns of the plan §3.4, last stop descending, and the detail blocks |
| `Rate`, `Stats` | percentages in tenths from whole counts (half up, away from zero), `—` over nobody, the small-sample mark; the lower median |

## Rules and invariants
- Ids only: no dev name or other personal data is ever read into the page (owner's answer to the CR's question 3).
- The tool only reads: one read-only transaction.
- A broken checkpoint file stops the report with the first error and its line (exit code 1); a `detail` must be a building,
  research or item id of the content file. The `outcome` is not checked against the error codes (they live in the Api).
- A named checkpoint's missing `detail` or `outcome` matches any; an unnamed checkpoint matches its exact triple, a missing part only
  a missing one. Its title is `event_type / detail / outcome`, a missing outcome left out and a missing detail before an outcome shown as `—`.
- The explorer reads no funnel. Last stop: a leaver's latest reach, once among the named and once among the unnamed checkpoints (a tie in
  time: the higher event id; one event reaching two named checkpoints: the first in the file). "Played after the reach" is the last seen
  minus the reach; the detail blocks go to every named row and the 50 most reached unnamed ones.

## How to extend
A new checkpoint is a line in `server/analytics/checkpoints.json`, no code.

## Tests
`tools/MetricsReport.Tests` (no database): `dotnet test tools/MetricsReport.Tests`.

## Known limitations
- Everything is loaded and computed in memory: fine for M0 to M2 sizes.

Last updated: CR-0009-T2
