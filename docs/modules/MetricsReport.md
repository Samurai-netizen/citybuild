# MetricsReport

**Path:** `tools/MetricsReport/` · **Kind:** .NET project (console, net10.0) · **References:** `CityBuilder.Infrastructure` (the EF Core
model and the content loader), `CityBuilder.Simulation` (content types) · **Referenced by:** `MetricsReport.Tests`, the PostgreSQL tests in
`CityBuilder.IntegrationTests` (`Metrics/`)

## Purpose
MetricsReport answers "what did the players do, how many reached each checkpoint, and where do the ones who left stop?"
([CR-0009](../changes/CR-0009-player-metrics.md), part 4). It reads the player events that the server records (`player_events`,
`core-instructions/06-data-model.md`) **read-only**, and the checkpoint file `server/analytics/checkpoints.json`, and writes one
self-contained HTML page. It is a developer tool: never deployed, no endpoint, and nothing in the game reads what it computes.
*(In progress, CR-0009-T2: the command line, the checkpoint file, the analysis model, the checkpoint explorer, the funnels, actions, friction, return and one-player sections, and the loader exist; the page follows.)*

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
| `Funnels`, `Actions`, `Friction`, `Returns`, `OnePlayer` | the other sections of the plan §3.4, each from `ReportContext`: funnel steps in order, per event type, refusals and the leavers' last events and last sessions, return per creation day, one player's timeline (at most 5000 rows) |
| `MetricsLoader`, `LoadFilter` | reads the population (period, `--exclude`), its events and per player and item the first `SETTLEMENT_PRODUCED` ledger row (a `GROUP BY`) as `production.first`, in one read-only, repeatable-read transaction with `AsNoTracking`; no `player_events` table: `MetricsDatabaseException`, exit code 2 |
| `Rate`, `Stats` | percentages in tenths from whole counts (half up, away from zero), `—` over nobody, the small-sample mark; the lower median |

## Rules and invariants
- Ids only: no dev name or other personal data is ever read into the page (owner's answer to the CR's question 3).
- The tool only reads: one read-only transaction. The connection string comes from `ConnectionStrings__CityBuilder`, never an argument.
- `dev_name` is used only in the `WHERE` of `--exclude`, never selected; a loaded player holds no text.
- `production.first` has id 0 (before a recorded event of the same second) and the city clock of the player's last recorded event at
  or before it (the ledger keeps no city clock).
- A broken checkpoint file stops the report with the first error and its line (exit code 1); a `detail` must be a building,
  research or item id of the content file. The `outcome` is not checked against the error codes (they live in the Api).
- A named checkpoint's missing `detail` or `outcome` matches any; an unnamed checkpoint matches its exact triple, a missing part only
  a missing one. Its title is `event_type / detail / outcome`, a missing outcome left out and a missing detail before an outcome shown as `—`.
- The explorer reads no funnel. Last stop: a leaver's latest reach, once among the named and once among the unnamed checkpoints (a tie in
  time: the higher event id; one event reaching two named checkpoints: the first in the file). "Played after the reach" is the last seen
  minus the reach; the detail blocks go to every named row and the 50 most reached unnamed ones.
- Funnels: step 1 is measured against the population and the time from the creation; a player who reached a step before the one
  before it drops out there. A refusal is a `command.` event with an outcome other than `accepted`; a leaver's last session starts at
  their last `session.start` (all events without one). Return: a `session.start` on exactly UTC day 1, 3 or 7 after the creation day;
  `—` while that day has not ended at "now".

## How to extend
A new checkpoint is a line in `server/analytics/checkpoints.json`, no code.

## Tests
`tools/MetricsReport.Tests` (no database): `dotnet test tools/MetricsReport.Tests`. The loader on PostgreSQL:
`CityBuilder.IntegrationTests/Metrics/` (`MetricsLoaderTests`, `MetricsReportDatabaseTests`; Docker), `bash scripts/dev/test.sh int Metrics`.

## Known limitations
- Everything is loaded and computed in memory: fine for M0 to M2 sizes.

Last updated: CR-0009-T2
