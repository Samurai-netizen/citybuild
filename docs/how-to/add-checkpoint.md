# How to add a player metrics checkpoint

A **checkpoint** is a thing a player did that the metrics report counts: "placed a sawmill", "completed a research", "came back
a second time" (CR-0009, `core-instructions/31-glossary.md`). The report (`docs/modules/MetricsReport.md`) computes every checkpoint
when it runs, over the events the server has stored. So a checkpoint added today also counts the play of last week, with no
migration and no server release.

There are two cases:
- **Case 1, the server already records the fact:** one line in `server/analytics/checkpoints.json`. This is almost always the case.
- **Case 2, the server does not record it yet:** a new event type in the server's code (CR-0009-T1), then the line of case 1.

## What the server records
Every row of `player_events` has an `event_type`, a `detail` (a content id, or none) and an `outcome` (for a command):

| Event type | Detail | Outcome | Written when |
|---|---|---|---|
| `command.<type>` (for example `command.place-building`) | the content id the command names (`EventDetail` of the command, `docs/how-to/add-command.md`) | `accepted` or the error code (`INSUFFICIENT_RESOURCE`, …) | a command is applied or refused by a game rule |
| `research.completed` | the research id | — | a settlement completes a research |
| `building.completed` | the building type (the `value` is the level) | — | a settlement completes a construction or an upgrade |
| `session.start` | — | — | the first login, and the first request after a gap of `Metrics:SessionGapSeconds` |
| `player.created` | — | — | the first login of a name |
| `production.first` | the item id | — | not stored: the report reads the first produced ledger row per player and item |

A new command is recorded with no metrics code, so `command.<its type>` is known to the report as soon as the command exists.

Every combination that occurs in the data is already a row of the report's checkpoint explorer ("unnamed"). A line in the file gives
it a title, a count (`atLeast`) or a place in a funnel.

## Case 1: a line in the file
1. Find the event in the table above (or in the explorer's unnamed rows: the row title is `event / detail / outcome`).
2. Add one line to `checkpoints` in `server/analytics/checkpoints.json`. Worked example: the players who once could not pay for an
   upgrade:

   ```json
   {"id": "ran_out_upgrading", "title": "Could not pay for an upgrade", "event": "command.upgrade-building", "outcome": "INSUFFICIENT_RESOURCE"}
   ```

   | Field | Required | Meaning |
   |---|---|---|
   | `id` | yes | unique, 1 to 48 of `a-z`, `0-9` and `_`; funnels name it |
   | `title` | yes | what the page shows |
   | `event` | yes | an event type of the table above |
   | `detail` | no | the event's detail must equal it; it must be a building, research or item id of `server/content/mvp-content.json` |
   | `outcome` | no | the event's outcome must equal it (`accepted` or an error code of `core-instructions/05-api-contract.md`) |
   | `atLeast` | no | how many matching events make the reach, default 1 (`session.start` with 2 = came back once) |

   A field left out matches anything: no `detail` matches every building, no `outcome` matches accepted and refused commands alike.
3. Optional: put the id into a funnel's `steps` (`"funnels": [{"name": "…", "steps": ["…", "…"]}]`). A funnel is only one way to
   look at some checkpoints in order; a checkpoint in no funnel gets the same columns in the explorer.
4. Run the report: `bash server-panel/panel.sh metrics` (`server-panel/README.md`). The file is checked first: a duplicate id, an
   unknown event type, a `detail` that is no content id or a funnel step that names no checkpoint stops the run with exit code 1 and
   a message naming the line. Nothing is written then.
5. Run the file's test: `dotnet test tools/MetricsReport.Tests --filter CheckpointFileTests` (one case loads the committed
   file against the committed content). Commit the line with the task or CR that wants it.

The order of the lines means nothing. The file is not game content: it has no `contentVersion` and changes no rule (`02` rule 23).

## Case 2: a fact the server does not record yet
Only when no event in the table says it. Examples: a market trade in Era II, a guild joined. Most new facts come with a new command,
and a command is recorded by itself (its `EventDetail` is the only line it needs). The rest:

1. **Write the CR or the task line first** if the fact is not in an approved task: recording is server code (`15`).
2. Add the event type's name to `server/src/CityBuilder.Domain/Metrics/PlayerEventTypes.cs` (lowercase, `area.what`, past tense).
3. **Test first:** a case in `server/tests/CityBuilder.Application.Tests/Metrics/` (for a settlement fact: `SettlementEventsTests`)
   that expects one event, in the city's transaction, with a content id as its detail and no name, address or request body (`07`).
4. Write the event where the fact is known, in the Application layer: a settlement fact in
   `server/src/CityBuilder.Application/Metrics/SettlementEvents.cs` (one `events.Add(...)` line), a session fact in `SessionEvents.cs`.
   The engine in `shared/Simulation/` does not change, and nothing in the game may read the event back.
5. Add the name to `KnownEvents` (`tools/MetricsReport/KnownEvents.cs`), so a checkpoint may match it, and to the table above.
6. Then case 1. The new checkpoint counts from the release that records it on: play before it has no such events.

## Reading the result
- "Left" columns show what goes together with leaving, not what makes players leave (the page says so above the explorer).
- A row under the minimum sample (`--min-sample`, default 5 players) is marked `*`.
- Players created on a Development server with a fast clock move the report's "now" ahead; leave them out with `--exclude`
  (`server-panel/README.md`).
