# Change Recipes — where code goes, what to test, which docs to update

Predictable changes come from following the same recipe every time. Find your change type below and touch
exactly these places. If your change fits no recipe, that's a sign it needs a CR or a new recipe (owner decision).
Paths are the M0 layout (`03`). "Module doc" means `docs/modules/<Project>.md`.

## Content-only change (numbers, a new item, building or research that uses existing effect types)
- Code: `server/content/*.json` only. Bump `contentVersion`. Worked example: `docs/how-to/add-building-or-item.md`.
- Tests: content validation passes; balance simulator run (from M0-67); golden tests untouched (they use frozen content).
- Docs: `23` (during M0), regenerated `docs/reference/generated/content.md`, balance note in the build log, `CHANGELOG`.

## New engine rule or effect type
- Code: `shared/Simulation/Runtime/` (rules, effect type, content validation), plus content JSON if it's used.
- Tests: rule test written first; property tests still green; golden scenario unchanged or a new scenario added.
- Docs: `04` (spec section and invariants), `25` if it's an effect type, `.claude/rules/simulation.md` if a new constraint appears,
  module doc `CityBuilder.Simulation.md`, how-to `docs/how-to/add-research-effect.md` (for effect types), decision row if project-wide.

## New command (player action)
- Code:
  - engine mutation in `shared/Simulation/Runtime/Rules/`;
  - use case in `server/src/CityBuilder.Application/Commands/<Name>/`: a record implementing `ICityCommand` (its data, `ValidateShape`,
    `Apply` calling the engine rule, its ledger reasons); `CommandPipeline` runs it, so there is no handler of its own (decision 042);
  - endpoint in `server/src/CityBuilder.Api/Endpoints/CommandEndpoints.cs`;
  - DTOs in `server/src/CityBuilder.Api/Contracts/`;
  - client intent in `CityBuilder.Client.Net` + UI.
- Tests: mutation rule tests (every error code); application tests with fakes; integration tests (success, every error,
  replay, conflict, other player, concurrency); a `Security_` test per threat; client view-model tests.
- Docs: `05` (endpoint, body, error codes), `07` (threat rows), `04` §8, module docs of each touched project,
  generated API/error reference, how-to `docs/how-to/add-command.md` stays valid, `CHANGELOG`.

## New error code
- Code: the error catalogue (one place), ProblemDetails mapping, client message table.
- Tests: the mapping table test and the client "every code has a message" test.
- Docs: `05` error table (until generated), `07` if security-related.

## Database change
- Code: persistence model and configuration in `Infrastructure/Persistence/`, a **new** migration (never edit an applied one), mapping.
- Tests: mapping round-trip, migration from empty and from the previous migration, constraint tests.
- Docs: `06`, generated schema doc, module doc `CityBuilder.Infrastructure.md`, decision row if the change breaks the contract.

## New configuration key
- Code: options class + validation; `appsettings.Development.json` default; never a secret in a committed file.
- Tests: validation fails on bad values.
- Docs: generated configuration reference (until M0-61: `docs/getting-started.md` → Configuration).

## New client screen or view
- Code: `client/CityBuilder/Assets/_Project/Presentation/<Feature>/` (view-model class + thin MonoBehaviour); its UXML and USS go to
  `client/CityBuilder/Assets/_Project/Resources/UI/`, where `Resources.Load` finds them (decision 024).
- Tests: EditMode view-model tests; PlayMode only if there's scene behaviour.
- Docs: `09` (screen list), module doc `CityBuilder.Client.Presentation.md`, `21` only if the design changed (G-Design).

## New project, assembly or folder area
- Code: project + references respecting `03`, the project file starting with a comment that says what it is for; architecture tests updated.
- Docs: `03` (dependency table and layout), `docs/architecture/overview.md`, a new module doc (check-docs enforces this), `AGENTS.md` map.
- Gate: G-Contract if it changes dependency direction; otherwise a decision row.

## New script or command
- Code: `scripts/dev/<name>.sh` (and `.ps1` if it must run without Git Bash), with a header comment and a comment above every function
  (`check-docs.sh` checks both).
- Docs: the `AGENTS.md` command list, `docs/getting-started.md`.

## New documentation page
- Where: a rule, design or contract → `core-instructions/NN-<slug>.md` (next free number of its range, a row in `core-instructions/README.md`);
  a description of what exists → the `docs/` folder of its kind (`architecture/`, `modules/`, `how-to/`, …). One topic, under ~150 lines.
- Handbook: add the page to a chapter in `website/sidebars.js`; pages in `docs/modules/`, `docs/how-to/`, `docs/adr/`, `docs/changes/`,
  `docs/build-log/` and `docs/reference/` are listed by their folder. A file outside `core-instructions/` and `docs/` also goes into
  `pageFiles` in `website/docs-set.js`.
- Links: a row in its folder's `README.md` index, links from the pages that need it, and a row in the `docs/README.md` map if it
  answers a new question. Plain CommonMark only (`17` §7).
- Renaming or deleting a page: change every living doc that names it (check-docs fails otherwise). Build log entries are never renamed.
- Check: `bash scripts/dev/docs-site.sh build`.

## New dependency
G-Dependency first. Then: `Directory.Packages.props` / Unity manifest / `website/package.json` (with `npm install` in `website/`, which updates
`package-lock.json`), the approved list in `14`, and a decision row with the alternatives considered.

## Bug fix
Failing regression test first, then the fix. Build log: root cause in one paragraph. Docs: whatever describes the corrected behaviour.

## Every change, always
A doc comment on every declaration the change adds or changes, a header comment in every new file, and comments that the change made
untrue fixed with it (`14` → Documentation in code; worked guide `docs/how-to/document-code.md`).
Build log entry (decisions, and a Docs impact row for every page the recipe above names: `updated` or `no change: <why>`), the resume
point in `13`, the plan board row, the `CHANGELOG` line, `Last updated:` in each touched module doc, and a handbook that still builds
(`bash scripts/dev/docs-site.sh build`).
