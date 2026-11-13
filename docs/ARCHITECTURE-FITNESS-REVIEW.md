# Architecture Fitness Review (M0-66)

The repository reviewed as if it were one large pull request, on 2026-11-13 (`main` at `ee1fd10`, after M0-65). The task is M0-66; every
decision and every command run is in its build log entry, `docs/build-log/2026/2026-11-13-01-M0-66.md`. Severity: **High** = breaks a rule
of `02` or blocks a later era · **Medium** = a gap a later change can fall through · **Low** = wording, tidiness.

## Summary
The layers, the write path and the engine's extension points hold up. No High finding. The task fixed three things: unused Unity
packages (D1), the one untested cell of the dependency table (A1) and drift in `03` (C1). What is still open needs the owner
(dependencies, CI) or is for a later task.

## Findings
| ID | Area | Severity | Finding | Status |
|---|---|---|---|---|
| A1 | References | Medium | `docs/architecture/overview.md` listed "Api references only Application and Infrastructure" as untested. Nothing checked that a `server/src` project file asks only for the references `03` allows. | Fixed: `ProjectFileTests.ServerProjectReferences_StayInsideTheDependencyTable` checks all four projects (a mutation, Domain → Application, fails it) |
| A2 | References | — | Assembly and project references match `03`: Domain → Simulation; Application → Domain, Simulation; Infrastructure → Application, Domain; Api → Application, Infrastructure; DocTools → Api, Infrastructure, Simulation. The Unity asmdefs match `09` (`ClientAssemblyTests`). | No change needed |
| W1 | Write path | — | All 12 command endpoints build a command and call `CommandPipeline`, and `GET /city` calls `SettleCity`. Only `CityTransactionRunner` holds `ICityRepository` (tested), whose one load is `SELECT … FOR UPDATE` on the acting player's city. Api holds no repository, unit of work or `DbContext`. | No change needed |
| W2 | Write path | Low | `DevLogin` opens its own transaction to insert the player and the starter city (with its `STARTING_GRANT` ledger row). It is outside the pipeline on purpose: no city exists yet, so there is nothing to lock or settle. It is not a command, so it has no `requestId`; a retry finds the player by name. | Not fixed (by design); worth a sentence in `06` when real accounts replace dev login |
| W3 | Write path | Low | `LedgerAuditSource` reads every city in one repeatable-read transaction and writes nothing. It is a test-only path (M0-64), with no endpoint. | No change needed |
| N1 | Naming | — | Every code name in `31-glossary.md` exists in the code, except the M2 names (`MarketOrder`, `Escrow`, `MailboxDelivery`, `Guild`) and the removed `Rules.MaxOfflineProductionSeconds`, which the glossary marks as such. | No change needed |
| D1 | Dependencies | Medium | The Unity manifest held four template packages that nothing uses (AI Navigation, Collab Proxy, Timeline, Visual Scripting). URP and the Test Framework were pinned below the versions the editor resolves. | Fixed: removed, pins aligned (no installed version changed); `ClientAssemblyTests.Manifest_RemovedTemplatePackages_AreAbsent`; Unity EditMode 1275/1275, PlayMode 189/189 |
| D2 | Dependencies | Medium | Four Unity packages in use are not on the approved list in `14`: URP (the render pipeline), uGUI (a dependency of URP's core), and the Rider and Visual Studio integrations (editor only). | Not fixed: the owner decides (G-Dependency); see "For the owner" |
| D3 | Dependencies | Medium | 7 open Dependabot alerts, all transitive npm packages of the handbook (`website/package-lock.json`: `tinypool` critical, `serialize-javascript` high and medium, `uuid` medium, `deepmerge` low). The handbook is built in CI and never shipped to players. NuGet has no known vulnerability. | Not fixed: fixing means upgrading (G-Dependency) |
| D4 | Dependencies | Low | `coverlet.collector` is on the approved list but no project uses it. | Not fixed: a list entry, not a package |
| X1 | Duplication | — | The client calls the engine for every rule (`CityQueries`, `TripRules`, `CourierRules`, `RoadRules`, `CityRules.StartResearch`, `Simulator.Advance`). Its own arithmetic is display only: layout, animation, percent text. | No change needed |
| X2 | Duplication | Low | The client's DTOs (`Net/Dtos/`) copy the server's by hand. They are checked against server answers captured by hand (`Tests/Fixtures/`). This review compared all 11 snapshot fixtures with the server's current OpenAPI schema: no field is missing or extra. But nothing re-captures the fixtures when the server's DTOs change. | Not fixed: a later task could generate the fixtures in an integration test |
| C1 | Docs drift | Low | `03` said Domain holds a "settlement record" (it is Application's `Cities/Settlement`). Its Application line listed only the first five use cases and four of the seven ports. | Fixed |
| C2 | Docs drift | — | `check-docs.sh` and `DocTools check` pass: the generated reference is current, and every module doc and declaration comment is in place. | No change needed |
| T1 | Tests | Medium | Unity's tests are not in CI (`scripts/dev/test-unity.sh` runs only on a machine with the editor), and neither is `test-goods.sh` (CR-0006-T1). A client break shows up only when someone runs them by hand. | Not fixed: workflow files are protected, and CI has no Unity licence |
| T2 | Tests | Low | Two tests are known to be load-sensitive: `ConnectionMonitorTests.Tick_WithNoPollDue_AllocatesNothing` (Unity, once in a cold run) and `PerformanceBudgetTests` (a time budget). Both are listed in the resume point. | Not fixed: no failure in this task's runs |
| T3 | Tests | — | No skipped or empty test in any suite (search for `Skip =`, `[Ignore]`, `Assert.True(true)`). The scan tests each have a planted-violation test (M0-65). | No change needed |
| T4 | Tests | Low | The Unity test runner's `InitTestScene*.unity` is kept out of git only by this clone's `.git/info/exclude`. | Not fixed: `client/CityBuilder/.gitignore` could name it in a client task |

## M2 readiness
- **Mailbox events in `Advance`:** `Simulator.Advance` gathers its timed events from a list of `IEventSource`s (research, building timers,
  trip arrivals, courier arrivals) and moves to the earliest. A mailbox source is one more class and one more list entry, placed before the
  building timers (`04` §13). Its one open question is which store a delivery goes into (`26` binding rule 4). `Advance` will need one more
  input, the city's pending deliveries, passed in by the settle. The mailbox is the only cross-player channel: no code locks or writes a
  second city (W1).
- **Era transition:** era is data. `CityState.Era` is read-only, and the engine compares eras only with content (a building type's era in
  placement, a research node's in `StartResearch`). Content rules keep items out of earlier eras (`EraRules`), and `EngineSourceTests`
  forbids era numbers in engine code. The transition is new work: a mutator for `Era`, a check inside `Advance` when a research completes,
  and the era charter grant. It is additive: nothing assumes the era is always 1.
- **Effect types:** each effect is a class derived from `ResearchEffect`, read from content by its `type` (`ContentReader.ReadEffect`). The
  client's `ResearchEffectText` shows no line for a type it doesn't know yet, so a new type doesn't break the research screen. A new effect type takes a class, a reader case, a content
  rule and a client text (`19` → "New engine rule or effect type"). The five M0 types and the four planned ones in `25` fit this shape.
- **Ledger and integers:** every change to goods and coins writes a ledger row with a reason, and the M0-64 auditor matches the ledger with
  the stores. Money is `long`. Players and cities are separate tables (`26` binding rules 1, 5, 6).

## Dependency table
Versions are pinned centrally (`server/Directory.Packages.props`, also imported by `tools/`). Every project has a lock file, and
`dotnet restore --locked-mode` passes. The shared engine has no packages.

| Package | Version | Used by | Why | Approved (`14`) |
|---|---|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.12 | Api | access tokens (`07`) | yes |
| `Microsoft.AspNetCore.OpenApi` | 10.0.12 | Api | the OpenAPI document | yes |
| `Microsoft.Extensions.ApiDescription.Server` | 10.0.12 | Api (build only) | writes the OpenAPI file for DocTools | yes |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.14 | Api (build only) | `dotnet-ef` migrations | yes |
| `Microsoft.EntityFrameworkCore.Relational` | 10.0.14 | Infrastructure | keeps EF Core on one patch with Design | yes (EF Core 10) |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | Infrastructure | PostgreSQL provider | yes |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | 3.3.4 | every `src` project (build only) | bans the clock and `Thread.Sleep` | yes |
| `xunit.v3.mtp-off`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | 4.0.2, 4.0.1, 18.10.1 | every test project | xUnit v3 under VSTest | yes |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.12 | IntegrationTests | in-process API | yes |
| `Testcontainers.PostgreSql` | 4.16.0 | IntegrationTests | real PostgreSQL | yes |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.10.0 | Application.Tests, IntegrationTests | `FakeTimeProvider` | yes |
| `Microsoft.CodeAnalysis.CSharp` | 5.9.0 | Repository.Tests | parser of the doc and source checks | yes |
| `dotnet-ef` (tool, `dotnet-tools.json`) | 10.0.14 | developers | migrations | yes |
| Unity `com.citybuilder.simulation` | local | client | the shared engine | yes (our code) |
| Unity `com.unity.nuget.newtonsoft-json` | 3.2.2 | `Client.Net` | JSON | yes |
| Unity `com.unity.inputsystem` | 1.20.0 | `Client.Presentation` | touch and mouse input | yes |
| Unity `com.unity.test-framework` | 1.6.0 | test assemblies | EditMode and PlayMode tests | yes |
| Unity `com.unity.modules.unitywebrequest` | 1.0.0 | `Client.Net` | HTTP | yes |
| Unity `com.unity.render-pipelines.universal` | 17.3.0 | the project | render pipeline (URP assets in `Assets/Settings`) | **no** (D2) |
| Unity `com.unity.ugui` | 2.0.0 | URP's core package | dependency; no client code uses it | **no** (D2) |
| Unity `com.unity.ide.rider`, `com.unity.ide.visualstudio` | 3.0.27, 2.0.22 | Editor only | IDE integration | **no** (D2) |
| Unity AI Navigation, Collab Proxy, Timeline, Visual Scripting | — | nothing | template leftovers | removed (D1) |
| npm `@docusaurus/core`, `preset-classic`, `theme-mermaid` | 3.10.2 | `website/` | the handbook | yes |
| npm `@easyops-cn/docusaurus-search-local`, `prism-react-renderer`, `react`, `react-dom` | 0.55.3, 2.4.1, 19.3.1 | `website/` | search, code colours, UI | yes |

Removing the four packages also turned off the built-in modules that only they enabled: AI, animation, audio, director and particle
system. Nothing uses them. A later task that adds sound names `com.unity.modules.audio` in the manifest (decision 025's lesson).

## For the owner
1. D2: add URP, uGUI and the two IDE packages to the approved list in `14`, or tell me which to remove.
2. D3: allow the handbook's transitive npm updates (`npm audit fix` in `website/`), or leave them for later.
3. T1: put Unity's tests (and `test-goods.sh`) into CI when a Unity licence for CI exists. Workflow files are protected.
