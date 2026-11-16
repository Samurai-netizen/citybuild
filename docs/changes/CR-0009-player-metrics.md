# CR-0009 — Player metrics: what players did, how many reached each checkpoint, where they stop

- **Status:** In progress
- **Type:** Feature (analytics and dev tooling)
- **Requested by:** owner · **Date:** 2026-11-16
- **Milestone / era:** M0 / Era I (brings a part of M3 forward, see the next line)
- **Conflicts with pillars (`22`) or the era rule (`25`):** none. **Conflicts with the scope:** `10` plans "analytics (funnel, retention,
  stall reasons)" for M3, and `30` lists "Analytics SDKs" as out of scope for M0. This CR uses no SDK and no outside service (the data
  stays in our PostgreSQL), but it does build the funnel part of the M3 item now. Approving it is the owner's scope decision (G-Scope);
  `10` and `30` get a line for it.

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Problem / goal
The owner's words: "I want to know which actions my players did in their playthroughs, how much % of them did this or that task, i need a
summary to investigate which parts of the game are stopping players to continue playing. Make a handy pattern to implement these
checkpoints throughout the game. In the end, statistics should be shown on a local webpage."

Added by the owner the same day (amendment 1): "Also make a system, that do not rely on a chain of checkpoints. I want to be able to see
and sort by each individual checkpoint (like an advanced view). I want to use this to determine what is stopping gamers to continue
playing this game." So a checkpoint stands on its own; an ordered funnel is only one optional way to look at some of them.

Today the server keeps the result of play (the city, the ledger, the receipts of successful commands) but not the story of it: a refused
command, a session, or a screen the player opened leaves a log line at most. Nothing answers "how many players ever started a research?"
or "what was the last thing the players who left did?".

## Proposal
Three parts: the server records **events**, a file names the **checkpoints** of the funnel, and a tool writes the **report page**.

### 1. Events (recorded by the server, first-party)
A new append-only table `player_events`: `id bigint identity pk`, `player_id uuid`, `event_type text`, `detail text null`,
`outcome text null`, `city_clock bigint`, `created_at timestamptz`; `index (player_id, created_at)` and `index (event_type, detail)`.
A row holds a player id and ids from the content, **never** a display name, an address, a device or a request body (`07` → Logging policy, OD-7).

| Source | Events | Code a new feature needs |
|---|---|---|
| The command pipeline, for every command | `command.<type>` with `outcome` = `accepted` or the error code (`INSUFFICIENT_RESOURCE`, …), `detail` = the content id the command names (a building type, a research id), when it has one | **none**: a new command is recorded by itself; it only says what its `detail` is (one property on `ICityCommand`) |
| The settlement | `research.completed`, `building.completed` (construction and upgrade, `detail` = the type and level), `production.first` (the first output of an item) | none for new content; one line in the settlement recorder for a new kind of fact |
| The login and the presence rule | `player.created`, `session.start` (the first settling request after the player was away, `PresencePolicy`) | none |

- An event is written **in the same transaction** as the city's save, so it exists exactly when the action was committed, and an
  idempotent replay records nothing (`02` rules 9 and 10). A refused command writes its event with the settlement it already commits.
- Recording is the Application layer's job (`IPlayerEventRecorder`, staged on `CityTransaction`). The engine in `shared/Simulation/`
  does not change: the recorder reads what the settlement and the command report.
- Events never feed back into the game. No rule, cost or unlock reads them (no XP by another name, `02` rule 23).

### 2. Checkpoints (the handy pattern)
Checkpoints are **not** in code. One file, `server/analytics/checkpoints.json`, lists them; each one matches events. They are a **set,
not a chain**: no checkpoint depends on another, and their order in the file means nothing (amendment 1).

```
{ "id": "first_woodcutter",  "title": "Placed a woodcutter",     "event": "command.place-building", "detail": "woodcutter", "outcome": "accepted" }
{ "id": "first_research",    "title": "Started a research",      "event": "command.start-research", "outcome": "accepted" }
{ "id": "research_done",     "title": "Completed a research",    "event": "research.completed" }
{ "id": "came_back",         "title": "Came back a second time", "event": "session.start", "atLeast": 2 }
```

- **Funnels are optional and separate:** the same file may name funnels, each an ordered list of checkpoint ids
  (`"funnels": [{ "name": "First hour", "steps": ["first_woodcutter", "first_research", "research_done"] }]`). A checkpoint can be in
  several funnels or in none; every checkpoint is in the checkpoint explorer (part 4) either way.
- **Every recorded event is a checkpoint by itself:** each `event_type` + `detail` + `outcome` that occurs in the data gets a row in the
  explorer without being listed in the file (for example `command.upgrade-building` refused with `INSUFFICIENT_RESOURCE`). The file is
  for giving a checkpoint a title, an `atLeast`, or a place in a funnel.
- A player **reached** a checkpoint when they have at least `atLeast` (default 1) matching events. The report computes this when it
  runs, over the stored events. So a checkpoint added today also counts the play of last week, with no migration and no server release.
- Adding a checkpoint is one line in that file. Only a fact the server does not record yet needs code, and then one line of it (the table above).
- The file is validated when the report runs and in a test: unique ids, known event types, a `detail` that is an id of the content file.
- `docs/how-to/add-checkpoint.md` describes both cases.

### 3. Nothing in the client (owner's decision at approval)
"Client should not know this thing exists." The client gets no metrics code, the API gets no metrics endpoint, and no response, header
or error says anything about metrics. Every event comes from what the server already sees. The first proposal's client checkpoints
(`POST /api/v1/events`, a `Checkpoints.Reach` call in the client) are **dropped**. What that costs: no "opened a screen but did
nothing" checkpoints. A refused command and a session with no command after it are the nearest server-side signs.

### 4. The report (a local webpage)
`tools/MetricsReport`, a console tool like DocTools: it reads the database and the checkpoint file and writes **one self-contained HTML
file** (`.run/metrics/report.html`, git-ignored: it holds player data), then prints its path. `bash server-panel/panel.sh metrics`
runs it against the local database and opens the page. No web server, no endpoint, no JavaScript library: tables and bars are plain HTML
and inline SVG, and one small script written in the page sorts and filters the tables. A player counts as **left** when not seen for N
days (default 3, an argument; `cities.last_seen_unix`). The page shows:

1. **Overview:** players, players seen in the last 1 / 7 / 30 days, events recorded, the period.
2. **Checkpoint explorer (the advanced view, amendment 1):** one table, one row per checkpoint (the named ones and every recorded
   event), independent of any order. A click on a column header sorts by it; a text box filters the rows; a switch hides the unnamed
   ones. Columns:
   - players who reached it (count and % of all), and the times it happened in total and per player (median);
   - median time from the player's creation to the first reach (real time and city clock);
   - **left after reaching it:** of the players who reached it, the % who left;
   - **left without it:** of the players who never reached it, the % who left; and the **difference** between the two (a large positive
     difference: players who get here leave more often; a large negative one: players who never get here leave more often);
   - **last stop:** the players for whom this was the last checkpoint before they left (count and % of all who left);
   - median play after the first reach (real time and sessions) for those who reached it.
   A row with fewer players than a minimum (default 5, an argument) is marked "small sample". A click on a row opens its detail: the
   most common next events after it, the last events of the players who left after it, and the other checkpoints those players reached.
3. **Funnels (optional):** for each funnel of the file, its steps in order: players who reached each, % of all players, % of the step
   before, the drop, and the median time from the step before.
4. **Actions:** every event type and detail: players who did it at least once (count and %), times in total, median per player.
5. **Friction:** refused commands by command and error code: players hit (%), times; and the last ten events before the last visit of
   the players who left, grouped (for example "8 of 20 left right after `INSUFFICIENT_RESOURCE` on `upgrade-building`").
6. **Return:** of the players created on a day, how many had a `session.start` 1, 3 and 7 days later.
7. **One player:** `--player <id>` adds that player's full timeline.

Filters as arguments: a date range, and `--exclude <dev names>` so test accounts (`devtest`, OD-14) don't count.

## Acceptance criteria
- [ ] Every accepted and every refused command writes exactly one `player_events` row, in the command's transaction; a replay, a
      conflict and a rolled-back command write none.
- [ ] A command added later by the recipe (`docs/how-to/add-command.md`) is recorded with no metrics code; a test over all
      `ICityCommand` types proves it.
- [ ] A completed research, a completed construction or upgrade and the first production of an item are recorded once each, also when
      they happen inside a long offline settlement; `session.start` follows the presence rule.
- [ ] No event row or report line holds a display name, a token, an address or a request body; a test pins the columns.
- [ ] Recording changes no engine result: the golden and determinism tests pass unchanged, and the performance budget (`04` §12) holds.
- [ ] A checkpoint added to `checkpoints.json` appears in the report with the players who reached it **before** it was added.
- [ ] The report works with an empty `checkpoints.json` and with no funnel: the explorer then lists every recorded event.
- [ ] The explorer has one row per checkpoint with the columns of part 4; every column sorts both ways by a click, offline; a checkpoint
      in no funnel has the same columns as one in a funnel; a row under the minimum sample is marked.
- [ ] "Left after reaching", "left without it" and "last stop" are tested on PostgreSQL against seeded players with known answers,
      including a checkpoint nobody reached and one everybody reached (no division by zero, shown as "—").
- [ ] A broken checkpoint file (a duplicate id, an unknown event type or content id) stops the report with a message naming the line.
- [ ] `panel.sh metrics` writes the HTML file with the sections of part 4 from the local database; the file opens offline (no
      network request) and the tool only reads (a read-only transaction).
- [ ] The funnels' numbers are tested on PostgreSQL against a seeded set of players with known answers (percentages, the "stopped at"
      checkpoint, the medians).
- [ ] The client and the API contract are unchanged: no file under `client/` changes, the generated API reference and
      `ClientInputInventoryTests` stay as they are, and no response differs with recording on.
- [ ] The report shows player ids only: no dev name is read into it; `--exclude` takes dev names as input but prints none.
- [ ] Docs updated (see impact); `check-docs.sh` and the handbook build pass.

## Out of scope
- Any analytics SDK or outside service (Firebase, GameAnalytics, Unity Analytics), and sending data anywhere (`30`).
- A hosted or live dashboard, alerts, server health metrics (M3).
- Stall reasons over time (they are derived on load and not stored, `06`); a later CR can snapshot them at settlement.
- A/B tests, cohorts by install source, device or country, revenue.
- A cleanup job or archive for old events, and GDPR export and delete (M3). The table deletes with its player (`ON DELETE CASCADE`), so
  the M3 delete needs nothing extra.
- Backfilling events for play before T1 (the receipts could give the accepted commands; not planned).

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | none; the recorder reads the settlement's and the command's results in the Application layer | — |
| Content (`23`, JSON) | none; `checkpoints.json` is not game content and has no `contentVersion` | new `server/analytics/checkpoints.json` |
| API (`05`) | none: no endpoint, no field, no header (part 3) | — |
| Data / migration (`06`) | new table `player_events` with two indexes (migration `AddPlayerEvents`, nothing deleted). The write pattern changes in one point: a refused command or a `session.start` writes an event row where today "nothing changed" writes nothing; `state_version` still stays | `06`, `docs/reference/generated/db-schema.md` (generated) |
| Security (`07`) | new rows: the events table (no personal data), the report file (player data on the developer's disk, git-ignored); the client sends nothing new | `07` |
| Client (`09`) | none (part 3) | — |
| Docs (recipes in `19`) | new `docs/how-to/add-checkpoint.md`, `tools/README.md`, `server-panel/README.md`, module docs (Application, Infrastructure, Api), `docs/how-to/add-command.md` (the `detail` property), a recipe row in `19`, `10` and `30` (scope lines), `13`, `M0-PLAN` (three tasks, as CR-0008 did), CHANGELOG, `AGENTS.md` commands (**protected**, G-Rules) | as listed |
| Tests (`08`) | unit: the recorder, the catalogue's validation, the funnel maths; PostgreSQL: events in the transaction, replay and rollback, the report on seeded players; architecture test: only the recorder writes events | `server/tests/…`, `tools/MetricsReport.Tests` |

## Risks and alternatives
- **Who the numbers are about.** M0 has the dev login only, so until real accounts (M1) and a soft launch (M3) "players" are the owner,
  the test accounts and whoever gets a development build (M0-82). The percentages are small-sample until then. What M0 gains: the pattern
  is in place before more commands and screens are added, and the playtest (M0-72) is measured.
- **Reading the explorer:** its "left" columns show what goes together with leaving, not what causes it: a late checkpoint is reached
  only by players who stayed long, an early one by almost everyone. The page says so above the table, shows counts next to every
  percentage and marks small samples. The detail of a row (what the players who left did last) is the evidence to read before a design change.
- **Timing.** Options: (a) T1 and T2 now, before M0-67 (recommended: recording starts early, the report serves the playtest);
  (b) after M0-84; (c) with M3 as `10` says. The board grows from 88 to 91 tasks with (a).
- **Table growth:** one row per command and per fact, about the ledger's rate. Fine for M0 to M2; a cleanup or a daily summary table
  is M3 work (Out of scope).
- **A write where there was none:** a refused command at an unchanged instant now inserts one row. It is inside the lock and bounded by
  the command rate limit; the T1 plan measures it against the performance budget.
- **Alternative A, an analytics SDK:** rejected: a new dependency and service, player data leaves the server, and the server already
  sees every action.
- **Alternative B, a dashboard endpoint in the API** (`/dev/metrics`): rejected: it would serve every player's data from the game
  binary; a local tool that reads the database exposes nothing.
- **Alternative C, no new table, report from receipts, ledger and city rows:** works today for accepted commands only; no refusals, no
  sessions, no UI steps, no order of events inside a settlement. Refusals are the best friction signal, so it is not enough.
- **Alternative D, fixed checkpoints in code, or checkpoints only as a chain** (`Checkpoint.FirstSawmill` written when reached): simpler to query, but every new
  checkpoint needs a release and starts counting from zero. Raw events with checkpoints defined at read time avoid both.
- **Protected files:** `AGENTS.md` (the command list) needs the owner's OK; nothing under `scripts/dev/` or the workflows is planned.

## Questions for the owner (answered 2026-11-16)
1. Scope change and timing: "I approve." The owner named no timing; the coder reads it as the recommended one, (a): T1 and T2 before
   M0-67. **Say so if you meant another.**
2. Client UI checkpoints: out. "Client should not know this thing exists." (part 3)
3. Dev names in the report: "ids only".

## Tasks
| ID | Title | Model | Status |
|---|---|---|---|
| CR-0009-T1 | Recording: `player_events` and its migration, `IPlayerEventRecorder`, the command, settlement and session events, in the city's transaction; tests; `06`, `07`, `10`, `30`, the modules, the plan board | O PLAN | In progress |
| CR-0009-T2 | The report: `checkpoints.json` and its validation, `tools/MetricsReport` (overview, checkpoint explorer, funnels, actions, friction, return, one player), `panel.sh metrics`, `docs/how-to/add-checkpoint.md` | O | Todo |

T1 is [PLAN] (G-Plan): it changes the write pattern of `06` in one point and adds a table, so the owner sees its plan before code.
T2 needs T1 merged. Each is one PR.

## Owner approval
- Approved on: 2026-11-16. Owner's words: "1. I approve. 2. Client should not know this thing exists. 3. ids only" (the answers to
  the three questions above, after amendment 1).
