# Decision Log

Project-wide decisions, one row each. Every decision first goes into the task's build log entry (`docs/build-log/`).
Project-wide ones are also added here; architectural ones also get an ADR in `docs/adr/` (`17` §4).
Never silently reverse a decision; add a new row that supersedes it. New ids continue each series (G10, 019, W9…).

## Game design (from the owner's vision, 2026-10-04)

| # | Decision | Why | Status |
|---|---|---|---|
| G1 | Medieval start; research moves the city through eras; major mechanics only on era entry | Owner's vision; simple start, depth later | Accepted (owner) |
| G2 | No XP, player levels or city levels; progress = research + building levels + land | Owner's vision | Accepted (owner) |
| G3 | Workers are assigned per building; speed ∝ workers; level raises the worker limit | Owner's vision | Accepted (owner) |
| G4 | Labor model: recipes cost worker-seconds; reassigning keeps progress; 0 workers = paused | Makes G3 exact and exploit-free (no double use of workers) | Accepted |
| G5 | Era transition when every research node of the era is complete | Owner's vision; guarded by the "no node > ¼ era length" rule | Accepted (owner) |
| G6 | Open market arrives with Era II, not at the start | Rule G1 (it's a major mechanic); keeps day 1 simple; raises the cost of alt farms | Accepted, owner may revisit |
| G7 | NPC Trading Post is the only coin faucet and the market price floor | A player market moves coins but can't create them | Accepted |
| G8 | Market price bands (0.25×–4× reference) and fees | Stops alt funneling and adds a coin sink; limits price freedom only at extremes | Proposed — tune in M2 |
| G9 | Era roadmap I–VI as in `25` | Proposal to fill the owner's outline | Proposed |

## Engineering

| # | Decision | Why | Status |
|---|---|---|---|
| 001 | Server-authoritative: the client proposes, the server decides | The economy must survive modified clients, and the shared market makes this critical | Accepted |
| 002 | PostgreSQL is the only source of truth; no Redis in M0 | One critical dependency is enough until measurements say otherwise | Accepted |
| 003 | One HTTP endpoint per command; no realtime channel | Async genre; polling every 2 min is enough | Accepted |
| 004 | Idempotency via `(playerId, requestId)` receipts + payload hash; successes only | Safe retries on flaky mobile networks | Accepted |
| 005 | Deterministic discrete-event simulation, settled lazily on every city access (`GET /city` included) | No background ticking per city; exact offline progress; one code path | Accepted |
| 006 | Integer city clock always advances fully; only production is capped per absence (dormant tail) | Long research must finish while away; cost stays bounded; replaces the earlier "pause the whole city" cap | Accepted (rev. 2026-10-04) |
| 007 | A shared engine (netstandard2.1, C# 9) is used by the server (authority) and the client (display only) | Progress bars and counters match the server without extra endpoints | Accepted; fallback below |
| 008 | Every write takes `SELECT … FOR UPDATE` on the acting player's city only, plus a `state_version` guard | Simplest correct serialization; no cross-city deadlocks | Accepted |
| 009 | UI Toolkit + code-built scenes, primitives for placeholder art | Text-based assets Claude can author and review | Accepted |
| 010 | Errors as RFC 9457 Problem Details with a stable `code` | Standard shape, built into ASP.NET Core | Accepted |
| 011 | Quantities, coins and labor are `long`; permille for modifiers; ceiling division for durations | Determinism across server, Mono and IL2CPP | Accepted |
| 012 | Same-time ordering: research completion → building completions by id → starts by id | Deterministic and explainable; research effects apply immediately | Accepted |
| 013 | Upgrading a producing building refunds the current cycle's inputs; refunds and deliveries may exceed caps | Simple and fair; caps limit production only | Accepted |
| 014 | Rejected commands still commit the settlement | Elapsed time is real whatever the command outcome | Accepted |
| 015 | Research is in M0 (4 nodes, 1 slot, 3 effect types) | It's the main progression; the content schema and engine must carry it from day one | Accepted |
| 016 | Cross-player effects go through a mailbox, applied as timed events in the receiver's settle | Avoids locking two cities; keeps settlement deterministic | Accepted (built in M2) |
| 017 | Content items carry `era` and `tradable`; city state carries `era` | Cheap now, expensive to retrofit later | Accepted |
| 018 | Auto-staff only on construction completion: `min(free, MaxWorkers)` | Offline-built buildings shouldn't sit idle; everything else stays the player's choice | Accepted |
| 019 | Server tests use xUnit v3 on VSTest: `xunit.v3.mtp-off` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` | `dotnet test <slnx> --filter "Category…"` stays as in CI and `AGENTS.md`; the default `xunit.v3` package (Microsoft Testing Platform) fails under VSTest on the .NET 10 SDK; v2 is the legacy line. Moving to MTP later changes the test commands (M0-02) | Accepted (owner, 2026-10-12) |
| 020 | Server build conventions: every project writes its XML doc file (IDE0005 runs on build), CS1591 only in Domain/Application; warnings are errors in `src/`; the build enforces the naming rules (IDE1006); CA1707 is off in tests | One analyzer verdict for build, `dotnet format` and CI; required XML docs only where `14` asks for them; test names keep `Method_Scenario_ExpectedResult` (M0-02) | Accepted |
| 021 | Shared engine build conventions: `shared/Simulation.Build` is outside `server/`, so it repeats the strict settings (`AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, warnings as errors, XML docs required) next to `netstandard2.1` and `LangVersion 9.0`; no packages and no lock file; `#nullable enable` at the top of every file; tests read the asmdef, `package.json`, folder and csproj, because the .NET build cannot see them | Build, `dotnet format` and CI give one verdict (the lesson of 020); Unity compiles the same files as C# 9 and ignores the csproj (M0-03) | Accepted |
| 022 | Content ids are typed readonly structs over non-empty text, with ordinal equality and ordering; `default` is the empty id. `ItemId` is the first. The other content id types (`BuildingTypeId`, `ResearchId`, `EraId`) are decided in M0-07's plan, and an id with a numeric order (eras) is ordered by that number, never by its text | Equality and sort order must not depend on the machine's culture (`04` §9), and typed ids stop an item id from being passed where a building id is expected. Text order would put `era_10` before `era_2`, so a type with a numeric order must not offer text-based relational operators. The id format is checked by content validation (M0-03) | Accepted |
| 023 | The Unity client uses `com.unity.inputsystem` **1.20.0** and `com.unity.nuget.newtonsoft-json` **3.2.2**: the versions that Unity 6000.3.25f1 itself bundles and names as its minimum (the editor's `PackageManager/Editor/manifest.json`: `minimumVersion` and `mustBeBundled`). The editor's own Universal 3D template still pins Input System 1.12.0, which does not compile on 6000.3 (`BuildTarget.ReservedCFE` is gone: CS0117 in `InputSystemPluginControl.cs`); 1.14.2 compiles but is below the editor's minimum, so Package Manager treats it as incompatible. Change these two versions only after a headless import (`Unity -batchmode -quit`) exits 0 | A project that does not compile blocks every client task, and a version below the editor's minimum is a trap for the next editor patch. Both packages are approved in `14` and these are version choices inside them, so G-Dependency (new package, major bump) does not apply; the owner is told in the M0-04 report. Found by the independent review after a first fix with 1.14.2; the registry's newest, 1.20.1, was not tried because the editor-verified 1.20.0 is the safer choice (M0-04) | Accepted (not a gate; the owner is told in the M0-04 report) |
| 024 | The client creates its one `GameRoot` from code: `GameBootstrap` registers a `SceneManager.sceneLoaded` handler (`RuntimeInitializeOnLoadMethod`, `BeforeSceneLoad`) and adds a `GameRoot` to the scene named `Bootstrap` unless one is already in it, so `Bootstrap.unity` stays an empty scene. UXML, USS and `PanelSettings` that code loads by name live in `Assets/_Project/Resources/UI/` and are loaded with `Resources.Load` | No scene YAML is hand-edited and a scene created by hand needs no wiring (`09` rule 1), which `[SerializeField]` references could not give: a `GameRoot` made from code has nothing to assign them to. The handler also runs when a test loads the scene again (M0-05), which a once-per-start `AfterSceneLoad` method would not. Alternatives: a `GameRoot` stored in the scene (scene YAML), Addressables (a new package, more setup than M0 needs) (M0-04) | Accepted |
| 025 | The Unity manifest holds no pre-release or experimental package. `com.unity.ai.assistant`, `com.unity.ai.inference` and `com.unity.pipeline`, which were in the owner's new project but not in the editor's own Universal 3D template, are removed (the owner's G-Dependency decision, 2026-10-14) with everything only they pulled in, and what they had written into project settings is undone. A package that the Hub, the Editor or Package Manager adds on its own is not approved by that: it needs the owner's yes like any other (`14`); `ClientAssemblyTests` fails if one of the three returns. The packages had also been enabling built-in modules, `UnityWebRequest` among them (a compile probe found it): the manifest now names `com.unity.modules.unitywebrequest`, which `ApiClient` (M0-37) needs | They were not on the approved list in `14`, are experimental, add editor-only tooling and define symbols, and bring a transitive package set (App UI, Sentis). Built-in modules in Unity 6.3 are on only when something asks for them, so removing a package can silently remove an engine API (M0-04-FOLLOWUP) | Accepted (owner, 2026-10-14) |

## Workflow (AI-building process, 2026-10-04)

| # | Decision | Why | Status |
|---|---|---|---|
| W1 | `AGENTS.md` is the tool-agnostic entry point; `CLAUDE.md` imports it and adds Claude-only details | Different AI tools work on the repo one after another | Accepted |
| W2 | The repo is the only memory: write-ahead, small green steps, checkpoint + push after every step (`16`) | Sessions end abruptly when tokens run out; the next coder may be another tool on another machine | Accepted |
| W3 | One build log entry per task, every decision logged as it's made; append-only after merge (`17` §4) | Full traceability for people and AI without merge conflicts | Accepted |
| W4 | Layered enforcement: instructions → Claude Code hooks/permissions → git hooks → CI | Instructions alone are followed inconsistently; mechanical checks are reliable | Accepted |
| W5 | Features only via tasks or owner-approved CRs; one PR per task; the owner merges (`15`) | Owner control over scope and quality | Accepted |
| W6 | Reference docs are generated (DocTools, from M0-61) and checked in CI; module docs enforced per project | Docs that can be generated can't drift | Accepted |
| W7 | The M0 plan lives in the repo (`docs/plan/M0-PLAN.md`) with task ids and a status board | Any coder can read the next task; no copy-paste from chat | Accepted |
| W8 | Squash-merge PRs; the step history stays in the build log | Clean `main`, full detail where it's useful | Accepted |
| W9 | The repository is public (OD-7); `main` is guarded by the ruleset `main-protection` (PR with 0 approvals, squash only, `docs` + `server` checks, no bypass) plus secret scanning with push protection | Owner's choice; the owner's account opens the AI's PRs, so a required approval would block every merge (M0-01) | Accepted (2026-10-08) |
| W10 | No agents (subagents, Workflows, multi-agent runs) unless really necessary; then at most one per task, on Sonnet 5.5 or an equivalent model, with the reason logged first (`15` OD-9, `02` rule 27). The `reviewer` step of `/finish-task` becomes a self-review, `/docs-sync` is done by hand, and `.claude/agents/reviewer.md` runs on Sonnet instead of Opus | The owner's instruction of 2026-10-14; the owner gave no reason. It limits agents only, not the model of the main session (the coder's reading of the instruction; the owner may correct it) | Accepted (owner, 2026-10-14) |

## Fallbacks agreed in advance
- **007 fallback:** if the Unity local-package integration blocks progress for more than one session, the client uses
  only the snapshot timer fields for progress bars and refreshes counters on poll. Record the switch here.
- **005 fallback:** if settle cost becomes a problem (not expected in M0), add a steady-state fast-forward inside the
  engine, never a background ticker that writes every city.
