# CR-0009 — Player metrics: what players did, how many reached each step, where they stop

- **Status:** Proposed
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
| The client (part 3 below) | `ui.<name>`: a screen opened, the welcome-back report seen | one line in the client: `Checkpoints.Reach("research_screen_opened")`, and its id in the catalogue |

- An event is written **in the same transaction** as the city's save, so it exists exactly when the action was committed, and an
  idempotent replay records nothing (`02` rules 9 and 10). A refused command writes its event with the settlement it already commits.
- Recording is the Application layer's job (`IPlayerEventRecorder`, staged on `CityTransaction`). The engine in `shared/Simulation/`
  does not change: the recorder reads what the settlement and the command report.
- Events never feed back into the game. No rule, cost or unlock reads them (no XP by another name, `02` rule 23).

### 2. Checkpoints (the handy pattern)
The funnel is **not** in code. One file, `server/analytics/checkpoints.json`, lists the checkpoints in order; each one matches events:

```
{ "id": "first_woodcutter",  "title": "Placed a woodcutter",     "event": "command.place-building", "detail": "woodcutter", "outcome": "accepted" }
{ "id": "first_research",    "title": "Started a research",      "event": "command.start-research", "outcome": "accepted" }
{ "id": "research_done",     "title": "Completed a research",    "event": "research.completed" }
{ "id": "came_back",         "title": "Came back a second time", "event": "session.start", "atLeast": 2 }
```

- A player **reached** a checkpoint when they have at least `atLeast` (default 1) matching events. The report computes this when it
  runs, over the stored events. So a checkpoint added today also counts the play of last week, with no migration and no server release.
- Adding a checkpoint is one line in that file. Only a fact the server does not record yet needs code, and then one line of it (the table above).
- The file is validated when the report runs and in a test: unique ids, known event types, a `detail` that is an id of the content file.
- `docs/how-to/add-checkpoint.md` describes both cases.

### 3. Client checkpoints (UI steps the server cannot see)
`POST /api/v1/events` `{ "checkpointId": "research_screen_opened" }` records a `ui.<name>` event. The id must be one of the catalogue's
`ui` ids (anything else: `INVALID_ARGUMENT` before the city is locked, OD-13); it has a rate limit class of its own; it changes no
game state and needs no `requestId` (a repeat is one more row, and the report counts players, not rows). The client sends it
fire-and-forget: a failure is dropped and never shown. Its own task, and the owner can leave it out (see Risks).

### 4. The report (a local webpage)
`tools/MetricsReport`, a console tool like DocTools: it reads the database and the checkpoint file and writes **one self-contained HTML
file** (`.run/metrics/report.html`, git-ignored: it holds player data), then prints its path. `bash server-panel/panel.sh metrics`
runs it against the local database and opens the page. No web server, no endpoint, no JavaScript library: tables and bars are plain HTML
and inline SVG. The page shows:

1. **Overview:** players, players seen in the last 1 / 7 / 30 days, events recorded, the period.
2. **Funnel:** each checkpoint in order: players who reached it, % of all players, % of the step before, the drop, and the median time
   from the step before (real time and city clock).
3. **Where players stopped:** for the players not seen for N days (default 3, an argument), the last checkpoint each one reached, as a
   bar per checkpoint. This is the "what stops them" view.
4. **Actions:** every event type and detail: players who did it at least once (count and %), times in total, median per player.
5. **Friction:** refused commands by command and error code: players hit (%), times; and the last ten events before the last visit of
   the players who left, grouped (for example "8 of 20 left right after `INSUFFICIENT_RESOURCE` on `upgrade-building`").
6. **Return:** of the players created on a day, how many had a `session.start` 1, 3 and 7 days later.
7. **One player:** `--player <id>` adds that player's full timeline.

Filters as arguments: a date range, and `--exclude <dev names>` so test accounts (`devtest`, OD-14) don't count.

### Proposed split (the task table is filled in after approval)
T1 [PLAN]: the table, the recorder, command, settlement and session events, tests. T2: the checkpoint file, `tools/MetricsReport`, the
panel command, the how-to. T3 (optional): `POST /events` and the client's `Checkpoints.Reach`. Each is one PR.

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
- [ ] A broken checkpoint file (a duplicate id, an unknown event type or content id) stops the report with a message naming the line.
- [ ] `panel.sh metrics` writes the HTML file with the seven sections above from the local database; the file opens offline (no
      network request) and the tool only reads (a read-only transaction).
- [ ] The funnel numbers are tested on PostgreSQL against a seeded set of players with known answers (percentages, the "stopped at"
      checkpoint, the medians).
- [ ] (T3) `POST /api/v1/events` accepts only catalogue ids, is rate limited, writes one row and changes no city state
      (`state_version` unchanged); the client never blocks or shows an error because of it.
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
| API (`05`) | T1, T2: none. T3: one additive endpoint, `POST /api/v1/events`, a rate limit class, no new error code | `05`, generated API reference |
| Data / migration (`06`) | new table `player_events` with two indexes (migration `AddPlayerEvents`, nothing deleted). The write pattern changes in one point: a refused command or a `session.start` writes an event row where today "nothing changed" writes nothing; `state_version` still stays | `06`, `docs/reference/generated/db-schema.md` (generated) |
| Security (`07`) | new rows: the events table (no personal data), the report file (player data on the developer's disk, git-ignored), T3's endpoint (a client can lie about UI events: they are statistics only, capped by the rate limit and the id list); "What the client sends" gets the T3 body | `07`, `ClientInputInventoryTests` |
| Client (`09`) | T3 only: a `Checkpoints` helper in `Net`, calls where screens open | `09`, `client/README.md` |
| Docs (recipes in `19`) | new `docs/how-to/add-checkpoint.md`, `tools/README.md`, `server-panel/README.md`, module docs (Application, Infrastructure, Api), `docs/how-to/add-command.md` (the `detail` property), a recipe row in `19`, `10` and `30` (scope lines), `13`, `M0-PLAN` (three tasks, as CR-0008 did), CHANGELOG, `AGENTS.md` commands (**protected**, G-Rules) | as listed |
| Tests (`08`) | unit: the recorder, the catalogue's validation, the funnel maths; PostgreSQL: events in the transaction, replay and rollback, the report on seeded players; architecture test: only the recorder writes events; T3: contract and security tests | `server/tests/…`, `tools/MetricsReport.Tests` |

## Risks and alternatives
- **Who the numbers are about.** M0 has the dev login only, so until real accounts (M1) and a soft launch (M3) "players" are the owner,
  the test accounts and whoever gets a development build (M0-82). The percentages are small-sample until then. What M0 gains: the pattern
  is in place before more commands and screens are added, and the playtest (M0-72) is measured.
- **Timing.** Options: (a) T1 and T2 now, before M0-67 (recommended: recording starts early, the report serves the playtest);
  (b) after M0-84; (c) with M3 as `10` says. The board grows from 88 to 91 tasks with (a).
- **Table growth:** one row per command and per fact, about the ledger's rate. Fine for M0 to M2; a cleanup or a daily summary table
  is M3 work (Out of scope).
- **A write where there was none:** a refused command at an unchanged instant now inserts one row. It is inside the lock and bounded by
  the command rate limit; the T1 plan measures it against the performance budget.
- **T3 trusts the client** for UI events. They can only skew statistics. Alternative: no T3, server-side facts only (the funnel then
  has no "opened the screen but did nothing" steps). T1 and T2 are useful without it.
- **Alternative A, an analytics SDK:** rejected: a new dependency and service, player data leaves the server, and the server already
  sees every action.
- **Alternative B, a dashboard endpoint in the API** (`/dev/metrics`): rejected: it would serve every player's data from the game
  binary; a local tool that reads the database exposes nothing.
- **Alternative C, no new table, report from receipts, ledger and city rows:** works today for accepted commands only; no refusals, no
  sessions, no UI steps, no order of events inside a settlement. Refusals are the best friction signal, so it is not enough.
- **Alternative D, fixed checkpoints in code** (`Checkpoint.FirstSawmill` written when reached): simpler to query, but every new
  checkpoint needs a release and starts counting from zero. Raw events with checkpoints defined at read time avoid both.
- **Protected files:** `AGENTS.md` (the command list) needs the owner's OK; nothing under `scripts/dev/` or the workflows is planned.

## Questions for the owner
1. Approve the scope change (a part of M3's analytics in M0), and which timing: (a), (b) or (c)?
2. T3 (client UI checkpoints) in or out?
3. May the report show dev names next to player ids (local file only), or ids only? Proposed: ids only.

## Tasks (filled in after approval)
| ID | Title | Model | Status |
|---|---|---|---|

## Owner approval
- Approved on: — (quote the owner's words)
