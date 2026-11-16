# CR-0009-T2 — Player metrics: the report · implementation plan

> **For the coder who implements this later (written for Claude Opus 5.5, `[O]`).** You have no memory of the planning session; this file
> and the pages it names are everything. Spec: `bash scripts/dev/show-task.sh CR-0009-T2` (the CR: `docs/changes/CR-0009-player-metrics.md`).
> Planned 2026-11-16 against `main` at `a99cf02`, **before CR-0009-T1 was built**. T1's plan is `docs/plan/CR-0009-T1-PLAN.md`.
> This file is a plan, not a rule: where T1 as built differs from what is assumed here, follow the code and log the difference.

## 0. Before you start
1. **T1 must be merged** (`player_events` exists). Read what it built: `core-instructions/06-data-model.md` (the table), its build log
   entry `docs/build-log/2026/2026-11-16-01-CR-0009-T1.md` (decisions, and the owner's answers to T1's four points), and
   `server/src/CityBuilder.Domain/Metrics/PlayerEventTypes.cs` (the event names). Section 3 below assumes T1's plan with every point "yes".
2. **Owner questions: answered** (section 2, all yes, 2026-11-16). Copy the answer into T2's build log entry under Owner input.
3. **Start:** `/start-task CR-0009-T2`. No build log entry exists for T2: `new-log-entry.sh` creates it as usual.
4. **Read first:** `02`, `15`, `16`, `14` (every declaration documented, OD-12), `07` → Logging policy, `19` → New project, New script
   or command, New documentation page; `tools/README.md`, `docs/modules/DocTools.md` and `tools/DocTools/` (the pattern for a tool:
   `Program.cs` → `CommandLine.Run(args, out, error)`), `server-panel/panel.sh` and `server-panel/README.md`.

## 1. What this task builds
- `server/analytics/checkpoints.json`: the named checkpoints and the optional funnels, with a starter set for Era I.
- `tools/MetricsReport/` (console tool) and `tools/MetricsReport.Tests/`: reads the database **read-only**, writes one HTML file.
- `bash server-panel/panel.sh metrics [arguments]` and a menu entry: runs the tool on the local database and opens the page.
- `docs/how-to/add-checkpoint.md`, `docs/modules/MetricsReport.md`.

Not in this task: any change to the API, the client, the engine, the events T1 records, or the database schema. **No new package**
(G-Dependency): EF Core and Npgsql come through the reference to `CityBuilder.Infrastructure`, as DocTools has it; JSON and HTML
encoding are in the base library; the page uses no JavaScript or CSS library and loads nothing from the network.

## 2. Questions for the owner — **all three answered yes, 2026-11-16**
Owner: "All questions in each file - i say YES, approve". So: A sorting is checked by hand, B `AGENTS.md` may get the `MetricsReport` command line and map entry (this
approval covers exactly that edit, G-Rules), C the starter list of 3.2 is used. The last column is history.

| # | Point | If no |
|---|---|---|
| A | **Sorting by click is checked by hand.** A test checks the page's structure (every header has a sort key, every cell a sort value); that a click really sorts is a [MANUAL] line in `tools/README.md`. A browser test would need a new dependency | G-Dependency for a browser test tool (Playwright), then an automated test |
| B | **`AGENTS.md` is protected:** may its Commands list get the `MetricsReport` line and its repository map the tool? | leave `AGENTS.md`; the command is only in `tools/README.md` and `docs/getting-started.md` (as happened with CR-0006) |
| C | **The starter checkpoints** of section 3.2 are the planner's guess at what matters in Era I. The owner may change the list now or any time later (it is one file, no code) | use the owner's list |

## 3. Design

### 3.1 Input: what the tool reads (one read-only, repeatable-read transaction)
- `player_events` of the period; `players` (`id`, `created_at`; `dev_name` **only** in the `WHERE` of `--exclude`, never selected);
  `cities` (`player_id`, `last_seen_unix`).
- From `ledger_entries`: per city and item the first `SETTLEMENT_PRODUCED` row (`MIN(created_at)`), turned in the loader into an
  in-memory event `production.first` with `detail` = the item (T1 point A). If T1 recorded real `production.first` events, skip this.
- The connection string comes from the environment variable `ConnectionStrings__CityBuilder` (the API's key), never from an argument
  (an argument shows in the process list). No migration is run; a database without `player_events` ends with a clear message.
- Everything is loaded and computed in memory, in plain classes without the database, so the maths is unit-tested without Docker.
  This is fine for M0 to M2 sizes; say so in the module doc (a limit, not a secret).

### 3.2 The checkpoint file
```
{
  "checkpoints": [
    { "id": "placed_woodcutter", "title": "Placed a woodcutter", "event": "command.place-building", "detail": "woodcutter", "outcome": "accepted" },
    { "id": "sawmill_level_2", "title": "A sawmill reached level 2", "event": "building.completed", "detail": "sawmill", "minValue": 2 },
    { "id": "came_back", "title": "Came back a second time", "event": "session.start", "atLeast": 2 }
  ],
  "funnels": [ { "name": "First session", "steps": ["placed_woodcutter", "placed_sawmill", "started_research"] } ]
}
```
- Fields: `id` (unique, `[a-z0-9_]{1,48}`), `title`, `event` (required); `detail`, `outcome`, `minValue`, `atLeast` (default 1) optional.
  Unknown fields are an error. `funnels` may be missing or empty; a checkpoint may be in several funnels or none.
- **Validation** (at every run and in a test of the committed file): unique ids; `event` is a known type (`command.<a command type>`,
  the names of `PlayerEventTypes`, `production.first`); a `detail` exists in `server/content/mvp-content.json` (a building, research
  or item id, read with the server's content loader as DocTools does); `atLeast >= 1`; funnel steps name existing checkpoints, none
  twice in one funnel. An error names the **line** of the file: parse with `Utf8JsonReader` and keep each entry's line (count the
  newlines before `TokenStartIndex`), because `JsonDocument` loses positions.
- **Starter set** (point C): placed each building type of the content; `started_research`; completed each research; `first_upgrade`
  (`command.upgrade-building`, accepted); first production of each non-currency item; `came_back` (2) and `came_back_5` (5);
  `ran_out` (`command.*` cannot be matched by a wildcard, so one checkpoint per command for `INSUFFICIENT_RESOURCE` on
  place-building and upgrade-building). One funnel, "First session". Build the list from the content file's ids, not from memory.

### 3.3 Definitions (write them into the page's "How to read this" box and the module doc)
- **Population:** players created in `[--from, --to]`, minus `--exclude`. Every percentage is of this population unless it says otherwise.
- **Now:** the larger of the machine's clock and the newest `last_seen_unix` (a Development server with `DevTimeScale` above 1 runs ahead).
- **Left:** `now - last_seen_unix >= --left-after-days × 86400` (default 3).
- **A match:** same `event_type`; `detail` and `outcome` equal when the checkpoint names them; `value >= minValue` when it names one.
- **Reached:** at least `atLeast` matches; the **reach** is the `atLeast`-th match in `(created_at, id)` order.
- **Unnamed checkpoints:** every distinct `(event_type, detail, outcome)` in the data, shown as `event_type / detail / outcome`.
- **Last stop:** for a player who left, the checkpoint with their latest reach; computed once among the named checkpoints and once among
  the unnamed ones (a tie: the higher event id).
- **Funnel step k:** the player reached steps 1 to k, each at or after the step before.
- **Percentages:** one decimal, from integers, rounded half up; a zero denominator prints `—`. **Median:** the lower middle value.
- **Small sample:** fewer than `--min-sample` (default 5) players in a percentage's denominator; the cell gets a mark and a tooltip.

### 3.4 The page (one file; sections in this order)
1. **Overview:** population, seen in the last 1 / 7 / 30 days, left, events, the period, the arguments used, the time of the report.
2. **Checkpoint explorer** (the main view; owner: checkpoints must not rely on a chain). One row per named and per unnamed checkpoint.
   Columns: title · kind (named, unnamed) · reached (players, %) · times in total · median times per reaching player · median real
   time from creation to the reach · median city clock at the reach · **left after reaching** (%) · **left without it** (%) ·
   **difference** (percentage points) · **last stop** (players, % of those who left) · median real time played after the reach ·
   median sessions after the reach. Above it: the reading note of the CR's Risks ("goes together with leaving, not causes it").
   A text filter, a "named only" switch, sort by a click on any header (first click descending, second ascending). Default order:
   last stop, descending. Each named row, and the 50 most-reached unnamed ones, opens a detail block: the five most common **next
   events** after the reach; the five most common **last events** of the players who left after it; the other named checkpoints these
   players reached (%).
3. **Funnels:** per funnel a table: step, reached (players, % of population, % of the step before), the drop, median time from the step before.
4. **Actions:** per `event_type`: players (count, %), times, median per player.
5. **Friction:** per command and error code: players hit (count, %), times; then "the last event of each player who left", grouped, top
   ten; then "left players whose last session held a refusal", by command and code (the last session: events from their last `session.start`).
6. **Return:** per creation day (UTC): players, and the % with a `session.start` in day 1, day 3 and day 7 after it (`—` when the
   cohort is too young).
7. **One player** (only with `--player <uuid>`): the timeline (time, city clock, event, detail, outcome, value), at most 5000 rows.
- **Ids only** (owner): no dev name anywhere in the file; a player id appears only in section 7.
- Every text from the database or the file is HTML-encoded (`HtmlEncoder`). No `http`, `https`, `src=` or `<link` in the output.
  Plain HTML tables, CSS in a `<style>`, bars as inline SVG or CSS widths, one `<script>` of about 50 lines for sort and filter.
  Cells carry `data-sort` numbers; the page is readable with scripts off. Light and dark by `prefers-color-scheme`.

### 3.5 Command line
`dotnet run --project tools/MetricsReport -- --out <file> [--checkpoints <file>] [--content <file>] [--from <date>] [--to <date>]
[--exclude <dev names, comma separated>] [--left-after-days 3] [--min-sample 5] [--player <uuid>]`. Defaults for the two files: the
repository's (find the root as `tools/DocTools/RepositoryRoot.cs` does). Exit codes: 0 written; 1 a bad argument or file (message on
stderr, nothing written); 2 the database could not be read. The tool prints the path it wrote and nothing about players.

## 4. Steps (each ends green, with its docs, and is checkpointed)
1. **Projects.** `tools/MetricsReport/MetricsReport.csproj` (Exe, `RootNamespace` `CityBuilder.MetricsReport`, a header comment,
   references Infrastructure and the engine's .NET build like `DocTools.csproj`) and `tools/MetricsReport.Tests/`; add both to
   `server/CityBuilder.slnx`. `Program.cs`, `CommandLine.cs` with argument parsing and tests (`CommandLineTests` in DocTools.Tests is the model).
   Docs now: `03` (dependency table, layout), `docs/architecture/overview.md`, the new module doc, `docs/modules/README.md`,
   `tools/README.md`, regenerated `project-map.md`; check what `ProjectFileTests` and the architecture tests expect of a new project.
2. **Checkpoint file.** `CheckpointFile` (parse, validate, line numbers), `Checkpoint`, `Funnel`; the starter `checkpoints.json`.
   Tests: each validation error with its line; the committed file is valid against the committed content; an empty file is valid.
3. **Analysis model.** `MetricsData` (plain records: players, events), `CheckpointMatcher`, `PlayerTimeline`. Tests: match rules,
   `atLeast`, `minValue`, ordering by `(created_at, id)`, unnamed checkpoints.
4. **Explorer.** `Explorer` → rows and details, per 3.3 and 3.4. Tests on a hand-made data set with known answers, including: a
   checkpoint nobody reached, one everybody reached, nobody left, everybody left, the small-sample mark, a tie in last stop, and a
   checkpoint in no funnel having the same numbers as when it is in one.
5. **Funnels, actions, friction, return, one player.** One class and one test class each; the funnel test covers a player who reached
   step 3 before step 2 (not counted at step 3).
6. **Loader.** `MetricsLoader` over `CityBuilderDbContext` (`AsNoTracking`, one transaction set `READ ONLY`, repeatable read; the ledger
   query as a `GROUP BY`). PostgreSQL tests on seeded players with known answers (`[Trait("Category", "Integration")]`): put them in
   `server/tests/CityBuilder.IntegrationTests/Metrics/` on the existing `PostgresTest` base, with a project reference to the tool, the
   way `GoodsPanelScriptTests` tests the server panel there; if a dependency test forbids the reference, log it and move the fixture
   instead. Cover: `--exclude` removes a player and never loads a name, the period, `production.first` from the ledger, a write
   attempt inside the tool's transaction fails, a database without the table gives exit code 2.
7. **HTML.** `HtmlReport`. Tests: the seven sections present (six without `--player`); encoding (a checkpoint title with `<script>`
   comes out escaped); no network reference; every explorer header has a sort key and every cell a `data-sort`; no dev name of the
   seeded players appears in the file; the same input gives the same bytes (stable order everywhere, no dictionary-order output).
8. **Panel.** `panel.sh metrics [arguments]` and the menu entry: needs the database running; reads `.env` for the connection values
   (as `docs/getting-started.md` → Database builds the string: host `localhost`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER`,
   `POSTGRES_PASSWORD`), exports `ConnectionStrings__CityBuilder`, writes to `server-panel/.run/metrics/report.html` (that folder is
   already git-ignored: the file holds player data), then opens it (`open`, else `xdg-open`, else prints the path). A function
   comment on each new function. `server-panel/test-metrics.sh` like `test-goods.sh` (arguments, a fake `dotnet`); it is not in CI,
   like `test-goods.sh` (the workflow files are protected): name that in Follow-ups.
9. **Try it for real** on the local dev database with Docker: play a few commands as `devtest` (OD-14), run `panel.sh metrics`, open
   the page, click every header, read the numbers against `psql` counts. Write what you saw into the build log (Verification).
   Without Docker or a browser: say so in the report and leave it as the owner's [MANUAL] step.
10. **Docs and finish.** `docs/how-to/add-checkpoint.md` (two cases: a line in the file; a fact the server does not record yet → a
    new event in T1's code, then a line), its row in `docs/how-to/README.md`; `server-panel/README.md`; `docs/getting-started.md` →
    Run; `07` (the report file: player data on the developer's disk, git-ignored, ids only; the tool is read-only); `19` (a recipe
    "New checkpoint"); `31`; `AGENTS.md` if point B is yes; `CHANGELOG.md`; the CR's status `Done` once merged (T2 is its last task);
    the plan board row and `13`. Then `/finish-task`.

## 5. Acceptance criteria of T2 (from the CR)
- [ ] A checkpoint added to the file appears with the players who reached it before it was added (computed at run time).
- [ ] The report works with an empty file and with no funnel; the explorer then lists every recorded event.
- [ ] The explorer has one row per checkpoint with the columns of 3.4; a checkpoint in no funnel has the same columns and numbers.
- [ ] "Left after reaching", "left without it" and "last stop" match known answers on seeded players (unit and PostgreSQL), including
      zero denominators.
- [ ] A broken file stops the run with a message naming the line; nothing is written.
- [ ] `panel.sh metrics` writes the page from the local database; it opens offline; the tool only reads.
- [ ] Ids only: no dev name in the output. No client, API, engine or schema change.
- [ ] Sorting: structure tested; the click checked by hand (point A, approved).
- [ ] Docs, the how-to, the module doc; `check-docs.sh`, the handbook build and `quality.sh` pass.

## 6. Traps
- The tools build with **warnings as errors** and doc-comment errors in tests (`tools/Directory.Build.props`).
- Never put a display name in the output "for readability": the owner said ids only.
- The numbers show correlation. Do not write "causes" or "because" in the page's texts.
- `--no-build` after an edit runs stale binaries. The Stop hook wants a checkpoint, not an argument. No agents without approval (OD-9).
