# M0 Plan — Medieval Production City, vertical slice of Era I

**Unity 6.3 LTS · .NET 10 · PostgreSQL 18.6 · AI coders (Claude Code: Opus 5.5 + Sonnet 5.5, or others)**

This is the plan for milestone M0. It has 84 tasks (`M0-01` … `M0-84`), grouped into 14 working days plus a Day 0 setup.
It lives in the repository so that **any AI coder can read the next task** without depending on earlier chats.
- Start a task: `/start-task M0-NN` (Claude Code), or "Read AGENTS.md, then start task M0-NN" (any tool).
- Print one task's spec: `bash scripts/dev/show-task.sh M0-NN`.
- Status of every task: the **task board** in §7.1. The AI updates it; the owner merges the PRs.

The game is in the genre of *Tap Tap Builder*, built on the owner's pillars (`core-instructions/22`):
- a medieval start, with research moving the city through eras;
- real-time production run by assigned workers;
- a grid you can build anywhere on;
- an open player market;
- no XP or city levels.

M0 builds the foundation for that game: a slice of Era I with workers, production chains and research, designed so eras,
the market and guilds can be added without rewriting the core.

---

## 0. What changed

### 0.0 v4 — the AI-building workflow (October 4)

| Owner directive | What the kit now does | Enforced by |
|---|---|---|
| Predictable workflow | Session protocol (`16`): start → write-ahead → small green steps → checkpoint → finish → PR | Skills, Stop hook, PR template |
| Owner control over features | Change control (`15`): autonomy levels, gates, protected files; new ideas become CRs that only the owner approves; the owner merges every PR | Permission `ask` rules, branch protection |
| High code quality | Coding standards (`14`), change recipes (`19`), XML docs required in core projects, analyzers, format check, independent reviewer subagent | Compiler, CI, `reviewer` |
| A build log of every decision | One entry per task in `docs/build-log/`; every decision logged when it's made; append-only after merge | `check-docs.sh`, Stop hook, CI |
| Complete, always-current docs for AI and people | Documentation system (`17`): normative specs vs descriptive docs, one home per fact, module doc per project, generated reference | `check-docs.sh`, DocTools in CI (M0-61), `docs-auditor` |
| Several AI coders, one after another | `AGENTS.md` entry point; the repo is the only memory; resume point + checkpoint/push after every step; recovery protocol | SessionStart hook, `/resume` |

Plan changes in v4:
- Every prompt is now a task with an id.
- Day 0 (setup) was added.
- M0-61 is now DocTools (generated docs). The dependency audit moved into M0-66.
- M0-63 is now a documentation completeness pass. The server container moved to M3.
- M0-84 is the M0 close-out (handoff is now continuous).
- Expect +10–15 % time for the process overhead. That's the price of resumability and traceability.

### 0.1 v3 — the owner's game vision

| Pillar | Design response (kit files) | Effect on M0 |
|---|---|---|
| 1. Medieval start, research moves the city through eras | Six-era roadmap, "major vs minor mechanic" rule (`25`) | `era` in content and state; era transition designed, built in M2 |
| 2. Real-time production | Unchanged; rates shown per hour | — |
| 3. Assigned workers; more = faster; level = more slots | Labor model (`04` §3): worker-seconds per cycle; progress kept on reassignment | Engine built around labor; AssignWorkers replaces Pause |
| 4. Grid, build anywhere | Unchanged; land expansion via research (`25`) | — |
| 5. Open market | Designed in `26`; arrives with Era II | Foundation rules: ledger everywhere, own-city locks, mailbox seam, `tradable` flag |
| 6. No XP; research is progression; major mechanics only with a new era | Research tree per era | StartResearch, a 4-node tree, research screen |

Also:
- The offline cap applies only to production; timers always run (`04` §2).
- The NPC "Market" building is now the **Trading Post**. "Market" always means the player market.

### 0.2 v2 — changes from the original plan (still valid)
- Offline progress is computed by settle-on-access (a deterministic event simulation), not a claim command.
- The engine is shared between server (authority) and client (display only).
- Integer-only numbers. `TimeProvider` everywhere, and no client time field.
- Typed per-command endpoints with RFC 9457 errors.
- Every write takes an own-city row lock plus a `state_version` guard.
- UI Toolkit and code-built scenes, so AI coders can work on the client.
- Determinism, split- and timer-invariance tests, and a development-only `DevTimeScale`.

### 0.3 Corrections to the original research report
- Tap Tap Builder has 5M+ downloads and ~4.7★ from ~93k reviews on Google Play, and it is still updated (Sept 2026).
- "No timers" is doubtful: recent updates sell *Instant Build* and boosters.
- In-game AI ideas are not MVP work. The budget table was generic.

---

## 1. Goal and Definition of Done

After 14 days (realistically 16–20) you have a clean, tested, extensible spine — **not** a full game:

- [ ] Unity opens without errors; the server builds with zero warnings in `src/`; `docker compose up` works.
- [ ] Dev login creates a player and an Era I starter city on the server.
- [ ] Unity shows the 8×8 grid, a build menu (locked cards name their research), and a HUD with stock / cap /
      net rate per hour, free workers, builders, a research ring and an era badge.
- [ ] Cottages provide workers; producers are auto-staffed when built; the **worker stepper** changes speed live.
- [ ] Woodcutter → Sawmill → Trading Post runs in real time; storage caps and missing inputs stall buildings visibly.
- [ ] Research: financing Trade Charter unlocks the Trading Post; Sharpened Axes and Frame Saw speed up production.
- [ ] Upgrades (L1 → L2) raise worker limits and refund an interrupted cycle.
- [ ] Coming back after hours shows a welcome-back report. Research finishes on time; production stops after 8 h.
- [ ] Invalid, locked, unaffordable, over-assigned, replayed, conflicting and concurrent requests are rejected correctly.
- [ ] State survives server restarts, and the ledger reconciles with inventory.
- [ ] Golden scenario #1 (`23-mvp-content.md`) passes in the engine and over HTTP.
- [ ] Determinism, split invariance, timer invariance, conservation, attack and concurrency suites pass.
- [ ] A clean clone can be set up from the README, and a development build reaches the city screen.
- [ ] **Process:** every task has a build log entry with its decisions; every project or assembly has a current module doc;
      generated reference docs are current; CI is green on `main`; the board shows all 84 tasks `Done` or `Skipped` (with a reason).

---

## 2. The game in brief

**Final game** (`21`, `22`, `25`, `26`): a city grows from a medieval village to the space age through research.
Workers run production chains in real time, players trade on an open market from Era II, and each era adds
major mechanics (guilds, population needs, expeditions, fuel, power grid, automation, megaprojects).

**M0 content** (exact numbers in `23`):

```
Cottage ── provides 4 workers ─────────────────────────────────────────────┐ (assigned per building)
                                                                            ▼
Woodcutter's Hut ─ 60 labor/log ─▶ Sawmill ─ 2 logs→1 plank, 180 labor ─▶ Trading Post ─ 2 planks→10 coins, 120 labor
   (max 2 workers L1, 4 L2)          (max 3 / 6)                            (max 2 / 4; unlocked by research)

Research (1 slot): Trade Charter → Storehouses · Sharpened Axes → Frame Saw
```

- 3 items, 4 buildings, levels 1–2, 4 research nodes, 8×8 grid, 2 builder slots, 1 research slot.
- Duration of a cycle = labor ÷ assigned workers (rounded up). 0 workers = paused.

---

## 3. Stack (verified October 2026)

| Layer | Choice | Notes |
|---|---|---|
| Client | **Unity 6.3 LTS** | Newest LTS, supported to Dec 2027. Re-check for a newer LTS before Day 1; never upgrade mid-sprint |
| Client UI | UI Toolkit (UXML/USS) | Text assets Claude can write |
| Client JSON | `com.unity.nuget.newtonsoft-json` | `JsonUtility` can't do dictionaries |
| Shared engine | `CityBuilder.Simulation`, netstandard2.1, C# 9 | Unity local package + .NET project |
| Server | **.NET 10** LTS, ASP.NET Core minimal APIs | Built-in OpenAPI, ProblemDetails, rate limiter, `TimeProvider` |
| Persistence | EF Core 10 + Npgsql, **PostgreSQL 18.6** | Pin the image tag; PG18 images mount data at `/var/lib/postgresql` |
| Tests | xUnit, Testcontainers, `Microsoft.Extensions.TimeProvider.Testing`, Unity Test Framework | Banned-API analyzer for `DateTime.UtcNow` (Day 11) |
| Local infra | Docker Compose: `postgres`, `server` | Redis deferred |
| Dev machine | Windows or macOS with Docker, .NET 10 SDK, Unity Hub, Git | iOS builds need macOS + Xcode |

---

## 4. Core architecture decisions

> The Unity client proposes intent. The server settles time, checks rules, applies the change,
> and returns the authoritative snapshot.

**Every request that touches a city** (full lifecycle in `03`):
1. Authenticate → `PlayerId` from the token.
2. Validate the shape.
3. `BEGIN`, then `SELECT … FOR UPDATE` on **your own** city.
4. Commands: receipt check → replay or `IDEMPOTENCY_CONFLICT`.
5. **Settle** the city to now (production capped at 8 h; timers uncapped).
6. Commands: apply the mutation or return an error code (the settlement still commits).
7. Zero-length advance.
8. Persist, write ledger rows and the receipt, `state_version + 1`.
9. `COMMIT`; return the snapshot and settlement report.

**The engine** (`04`) is an integer discrete-event simulation. Cycles take inputs at the start and give outputs at the end.
Duration = labor ÷ workers, and reassigning workers keeps progress. Research completes on a timer and applies data-driven effects.
At each moment, events run in a fixed order: research → completions by id → starts by id.
The same input gives the same output on the server and on the phone.

**Cross-player rule** (for the market later): a transaction never locks another player's city.
Effects on others go to a mailbox that their next settle applies.

---

## 5. Day 0 — setup (owner, about 1–2 hours, [MANUAL])

1. **Install the tools:** Git (Windows: Git for Windows), GitHub CLI `gh` (then `gh auth login`), .NET 10 SDK, Docker Desktop,
   Unity Hub with Unity 6.3 LTS (+ Android Build Support), and Claude Code.
2. **Create a GitHub repository** (public, owner directive OD-7) and clone it. Copy the kit into the clone's root: `AGENTS.md`, `CLAUDE.md`,
   `README.md` … must sit at the top level, next to `.claude/`, `.github/`, `.githooks/`, `core-instructions/`, `docs/`, `scripts/`.
3. **Enable the git hooks:** `bash scripts/dev/setup-hooks.sh`. Check: `bash scripts/dev/check-docs.sh` prints `check-docs: OK`.
4. **First commit (owner-only override):** `git add -A && ALLOW_MAIN_COMMIT=1 git commit -m "chore(kit): add instruction kit"` → `git push`.
5. **Protect `main` on GitHub** (`core-instructions/18-git-and-ci.md` → GitHub settings):
   require a PR and the `docs` and `server` checks, block force pushes, squash-merge only, and delete branches on merge
   (the full list, incl. linear history and security settings, is in `18`). After this, `main` changes only through PRs.
   Check that the **CI** workflow ran green on the first push (Actions tab).
6. **Read and adjust the design** before any code: `core-instructions/22` (pillars + proposals), `25`, `23`.
   Edit anything you disagree with and commit it on a branch (`chore(kit): …`) via a PR. That's good practice for the flow.
7. **Start Claude Code** in the repo folder (`claude`), accept the workspace trust prompt, and run `/context`:
   `CLAUDE.md`, `AGENTS.md`, `02-principles.md` and `13-progress.md` must be listed. Then `/start-task M0-01`.

## 6. How a task runs

**The owner:**
1. `/clear`.
2. `/model` (Opus 5.5 for **[O]**, Sonnet 5.5 for **[S]**).
3. `/start-task M0-NN`.
4. For **[PLAN]** tasks, approve the plan.
5. Answer questions and do the **[MANUAL]** steps.
6. Read the task report, review the PR, and **squash-merge**.

**The AI coder** follows `core-instructions/16-session-protocol.md`:
1. Preconditions; branch `task/M0-NN-…`.
2. Build log entry + resume point (write-ahead) → commit + push.
3. Small green steps, with a **checkpoint (resume point, build log decisions, commit, push) after each**.
4. `/finish-task`: Definition of Done, docs per change recipe, `check-docs.sh`, reviewer for **[O]**, PR, task report.

**If a session ends mid-task** (tokens ran out): start a new session with any tool and run `/resume`, or say
"Read AGENTS.md and resume." The resume point and build log say exactly where to continue.

**Other AI tools:** "Read AGENTS.md, then start task M0-NN following core-instructions/16-session-protocol.md."
Git hooks and CI enforce the same rules for every tool.

**A task spec is the prompt.** The text under each `#### M0-NN` heading is what the AI implements, on top of the standing rules.
Every task implicitly includes:
- follow `AGENTS.md` and the session protocol;
- log every decision;
- update the docs per `core-instructions/19`;
- update the resume point, the board and the CHANGELOG;
- end with the task report.
If a task conflicts with the design files (`21`–`26`), the files win. Report the conflict and log a decision.

---

## 7. Schedule overview

| Day | Morning (3 tasks) | Evening (3 tasks) | Outcome |
|---|---|---|---|
| 0 | Owner setup: GitHub repo, kit, hooks, branch protection, design review | — | Process ready |
| 1 | Repo audit, server skeleton, shared engine skeleton | Unity skeleton, Unity tests, checkpoint | Everything opens, builds, tests |
| 2 | Content + research model, city state + mutations, `Advance` with labor | Rule tests, golden scenario, engine review | The engine works |
| 3 | Production cap + dormant tail, property harness, invariants | Performance, display helpers, spec sync | The engine is trustworthy |
| 4 | EF model, mapping, Testcontainers | Transactions + locks, settle use case, concurrency | Persistent authoritative state |
| 5 | Dev auth, command pipeline, idempotency | Content/city endpoints, PlaceBuilding, attack review | Secure API core |
| 6 | Upgrade, AssignWorkers, StartResearch | Errors + limits, logging + dev time scale, HTTP golden E2E | Complete server MVP |
| 7 | API client, auth + content, client state store | Command sender, polling, E2E checkpoint | Client talks to server |
| 8 | Grid + camera, building views, build menu | Placement flow, feedback, client review | Playable placement |
| 9 | HUD, status visuals, inspector + workers | Research screen, welcome back, waiting-loop review | Workers and research are visible |
| 10 | Connection states, retry proof, client logging | Version checks, mobile UI, boundary review | Resilient client |
| 11 | DocTools (generated docs), quality script, docs completeness | Ledger audit, banned APIs + arch tests, fitness + dependency review | Stronger codebase, docs complete |
| 12 | Balance simulator, strategy runs, content changes | Content validation, first-session polish, playtest | Tuned, coherent loop |
| 13 | HTTP E2E, attack suite, concurrency suite | Unity smoke, clean clone, security report | Tested prototype |
| 14 | Triage, freeze review, invariant gaps | Dev build, final audit, M0 close-out | Runnable MVP |

**Realism check:** the labor model and research make Days 2, 6 and 9 heavier, and the workflow (logs, docs, PRs) adds
10–15 %. 84 tasks in 14 days means 4–8 focused hours a day; **17–21 days is the realistic outcome**. Cut in this order if you fall behind:
- Day 10 polish (M0-57–M0-59);
- M0-71;
- display prediction (use the decision-007 fallback in `12`);
- move Day 12 later.

Never cut Days 2–5 or 13, and never cut the process steps (build log, docs, checkpoints). They're what make the work resumable.

### 7.1 Task board
Status values: `Todo` · `In progress` · `In review` (PR open) · `Done` (merged) · `Blocked` · `Skipped` (reason in the build log).
The AI coder updates its row at start (`In progress`) and finish (`In review`). The next task sets the previous row to `Done` after the merge.

| ID | Day | Task | Tags | Status |
|---|---|---|---|---|
| M0-01 | 1 | Repository audit and kit verification | O PLAN | In review |
| M0-02 | 1 | Server solution skeleton | S | Todo |
| M0-03 | 1 | Shared engine skeleton and architecture tests | S | Todo |
| M0-04 | 1 | Unity project skeleton | S MANUAL first | Todo |
| M0-05 | 1 | Unity test foundation | S | Todo |
| M0-06 | 1 | Day 1 checkpoint | O | Todo |
| M0-07 | 2 | Content model with research and eras | O PLAN | Todo |
| M0-08 | 2 | City state and mutations | O | Todo |
| M0-09 | 2 | Simulator.Advance | O PLAN | Todo |
| M0-10 | 2 | Engine rule tests | O | Todo |
| M0-11 | 2 | Golden scenario #1 | O | Todo |
| M0-12 | 2 | Engine review | O | Todo |
| M0-13 | 3 | Settlement window and production cap | O | Todo |
| M0-14 | 3 | Property test harness | O | Todo |
| M0-15 | 3 | Invariant properties | O | Todo |
| M0-16 | 3 | Performance budget | O | Todo |
| M0-17 | 3 | Display helpers | S | Todo |
| M0-18 | 3 | Spec sync and ADRs | O | Todo |
| M0-19 | 4 | EF Core model, migrations, Docker | O PLAN | Todo |
| M0-20 | 4 | Repository and mapping | O | Todo |
| M0-21 | 4 | Testcontainers integration base | S | Todo |
| M0-22 | 4 | Transaction boundary and locking | O | Todo |
| M0-23 | 4 | Settle use case end to end | O | Todo |
| M0-24 | 4 | Concurrency tests and persistence review | O | Todo |
| M0-25 | 5 | Development authentication | S | Todo |
| M0-26 | 5 | Command pipeline with idempotency | O PLAN | Todo |
| M0-27 | 5 | Idempotency integration tests | O | Todo |
| M0-28 | 5 | Content and city endpoints | S | Todo |
| M0-29 | 5 | PlaceBuilding | O | Todo |
| M0-30 | 5 | API attack review | O | Todo |
| M0-31 | 6 | UpgradeBuilding | O | Todo |
| M0-32 | 6 | AssignWorkers | O | Todo |
| M0-33 | 6 | StartResearch | O | Todo |
| M0-34 | 6 | Error catalogue, rate limits, input limits | S | Todo |
| M0-35 | 6 | Logging, correlation, dev time scale | S | Todo |
| M0-36 | 6 | Server golden E2E over HTTP | O | Todo |
| M0-37 | 7 | API client and DTOs | S | Todo |
| M0-38 | 7 | Auth and content on the client | S | Todo |
| M0-39 | 7 | Client city store and display prediction | O | Todo |
| M0-40 | 7 | Command sender and retry policy | S | Todo |
| M0-41 | 7 | Polling and lifecycle | S | Todo |
| M0-42 | 7 | End-to-end checkpoint | O MANUAL | Todo |
| M0-43 | 8 | Grid and camera | S | Todo |
| M0-44 | 8 | Building views from state | S | Todo |
| M0-45 | 8 | Build menu | S | Todo |
| M0-46 | 8 | Placement flow | S | Todo |
| M0-47 | 8 | Feedback for every result | S | Todo |
| M0-48 | 8 | Client review | O | Todo |
| M0-49 | 9 | HUD | S | Todo |
| M0-50 | 9 | Building status visuals | S | Todo |
| M0-51 | 9 | Building inspector with worker stepper | S | Todo |
| M0-52 | 9 | Research screen | S | Todo |
| M0-53 | 9 | Welcome-back report | S | Todo |
| M0-54 | 9 | Waiting-loop review and tuning | O MANUAL | Todo |
| M0-55 | 10 | Connection states in the UI | S | Todo |
| M0-56 | 10 | Proving idempotent retries | O | Todo |
| M0-57 | 10 | Client logging | S | Todo |
| M0-58 | 10 | Version and content compatibility | S | Todo |
| M0-59 | 10 | Mobile-safe UI | S | Todo |
| M0-60 | 10 | Client ↔ server boundary review | O | Todo |
| M0-61 | 11 | DocTools: generated reference docs | O PLAN | Todo |
| M0-62 | 11 | Quality script | S | Todo |
| M0-63 | 11 | Documentation completeness pass | O | Todo |
| M0-64 | 11 | Ledger audit | O | Todo |
| M0-65 | 11 | Banned APIs and architecture tests | S | Todo |
| M0-66 | 11 | Architecture fitness and dependency review | O | Todo |
| M0-67 | 12 | Balance simulator | O PLAN | Todo |
| M0-68 | 12 | Strategy runs and findings | O | Todo |
| M0-69 | 12 | Apply approved content changes | S MANUAL decision | Todo |
| M0-70 | 12 | Content validation hardening | S | Todo |
| M0-71 | 12 | First-session polish | S | Todo |
| M0-72 | 12 | Playtest and smoke checklist | O MANUAL | Todo |
| M0-73 | 13 | HTTP end-to-end suite | O | Todo |
| M0-74 | 13 | Attack suite | O | Todo |
| M0-75 | 13 | Concurrency suite | O | Todo |
| M0-76 | 13 | Unity smoke run | S MANUAL | Todo |
| M0-77 | 13 | Clean-clone test | S | Todo |
| M0-78 | 13 | Security boundaries report | O | Todo |
| M0-79 | 14 | Bug triage | O | Todo |
| M0-80 | 14 | Code-freeze review | O | Todo |
| M0-81 | 14 | Invariant gap analysis | O | Todo |
| M0-82 | 14 | Development build | S MANUAL | Todo |
| M0-83 | 14 | Final MVP audit | O | Todo |
| M0-84 | 14 | M0 close-out | S | Todo |

---

## 8. Task specs

Tags: **[O]** strongest model (Opus 5.5) · **[S]** standard model (Sonnet 5.5) · **[PLAN]** owner approves the plan before code ·
**[MANUAL]** human step. Each task depends on the previous one unless it says otherwise. Every task implicitly follows §6.

---

### DAY 1 — Foundations

#### M0-01 — Repository audit and kit verification · [O] [PLAN]
```text
First task of the project. You are the first AI coder in this repository.
Read AGENTS.md, core-instructions/README.md, 01, 03, 15, 16, 17, 18, 19, and skim 22 and 25 to know where the game is going.

1. Inspect the repository and environment: list the kit files, tools installed (git, gh, dotnet --info, docker --version),
   git config core.hooksPath, and the CI result of the first push (gh run list, if gh is available).
2. Verify the kit works end to end on this machine:
   - check-docs.sh is OK;
   - show-task.sh M0-01 prints this task;
   - new-log-entry.sh creates your build log entry;
   - the commit-msg hook rejects a bad subject (test with a throwaway commit attempt and do not keep it);
   - the pre-commit hook refuses commits on main.
   Report anything that fails on this OS (e.g. Windows Git Bash differences) and fix only script portability bugs.
   Those files are protected, so ask the owner first.
3. Check the instruction kit for contradictions between AGENTS.md, CLAUDE.md, .claude/rules/*.md, core-instructions/*.md and this plan.
   List them; fix only obvious typos (protected files: ask).
4. Create the skeleton folders from 03-architecture.md that don't exist yet (shared, server, tools, client) with a one-line
   README.md each saying which task fills them.
5. Write docs/how-to/resume-interrupted-session.md: a worked example of 16 §F for a person or an AI. Update docs/how-to/README.md.
6. Finish per 16 §E (this is the first full run of the loop: build log, resume point, board, CHANGELOG, PR).
```

#### M0-02 — Server solution skeleton · [S]
```text
Read core-instructions/03-architecture.md, 14-coding-standards.md, 19 (recipe "New project") and .claude/rules/server.md.

Create the server foundation:
- global.json at the repo root pinning the .NET 10 SDK (latest patch, rollForward latestFeature).
- server/CityBuilder.slnx (the .NET 10 default solution format) with src/CityBuilder.Api, .Application, .Domain,
  .Infrastructure and tests/CityBuilder.Domain.Tests, .Application.Tests, .IntegrationTests.
- Project references exactly as in the dependency table (Simulation comes in M0-03).
- server/Directory.Build.props: net10.0, Nullable enable, ImplicitUsings enable, TreatWarningsAsErrors for src projects,
  AnalysisLevel latest-recommended, EnforceCodeStyleInBuild true, RestorePackagesWithLockFile true;
  GenerateDocumentationFile true for Domain and Application (CS1591 then fails the build for missing XML docs).
- server/Directory.Packages.props: central package management with pinned versions (approved list in 14 only).
- Api: minimal API host on http://localhost:8080 (launchSettings.json), GET /health, AddProblemDetails,
  AddOpenApi + MapOpenApi in Development, TimeProvider.System registered as a singleton, structured console logging.
- One meaningful test per test project (e.g. health endpoint returns 200 via WebApplicationFactory in IntegrationTests;
  mark it Category=Integration only if it needs Docker; this one doesn't).
- Commit the packages.lock.json files. CI's server job must now run and pass (it restores in locked mode).
- Module docs: docs/modules/CityBuilder.Api.md, .Application.md, .Domain.md, .Infrastructure.md from the template;
  docs/modules/README.md index. Update docs/getting-started.md and the AGENTS.md command marks (protected: ask).
Run restore, format check, build, test. Fix root causes of any failure.
```

#### M0-03 — Shared engine skeleton and architecture tests · [S]
```text
Read core-instructions/04-simulation-model.md (sections 1, 9), 14, and .claude/rules/simulation.md.

1. Create shared/Simulation/ as a Unity local package:
   package.json (name "com.citybuilder.simulation", version "0.1.0", unity "6000.3"),
   Runtime/CityBuilder.Simulation.asmdef with "noEngineReferences": true and "autoReferenced": true.
2. Create shared/Simulation.Build/CityBuilder.Simulation.csproj: netstandard2.1, LangVersion 9.0,
   EnableDefaultCompileItems false, Compile Include="../Simulation/Runtime/**/*.cs", GenerateDocumentationFile true,
   TreatWarningsAsErrors true. Make sure bin/ and obj/ can never appear inside shared/Simulation/.
3. Add the integer helpers first: CeilDiv(long, long) and ApplyPermilleReduction(long labor, int permille)
   (ceil(labor × (1000 − permille) / 1000)), with XML docs and exhaustive edge-case tests, plus ItemId as a readonly
   struct with ordinal comparison. Create server/tests/CityBuilder.Simulation.Tests; add both projects to CityBuilder.slnx.
4. Reference Simulation from Domain and Application.
5. Add architecture tests (plain reflection over referenced assemblies, no extra packages) proving:
   Simulation references only BCL; Domain/Application reference no ASP.NET Core or EF Core; Infrastructure doesn't reference Api.
6. docs/modules/CityBuilder.Simulation.md (covers both the package and the build project).
Run build and all tests.
```

#### M0-04 — Unity project skeleton · [S] · [MANUAL] first
```text
[MANUAL — the owner does this before starting the task]
1. Unity Hub → New project → Unity 6.3 LTS → "Universal 3D" → location client/, name CityBuilder.
2. Edit → Project Settings → Editor: Asset Serialization = Force Text; Version Control = Visible Meta Files.
3. Project Settings → Player → Active Input Handling = Input System Package (accept restart).
4. Project Settings → Player → Allow downloads over HTTP = Allowed in Development Builds.
5. Create > UI Toolkit > Panel Settings Asset → save as Assets/_Project/Resources/UI/PanelSettings.asset
   (this also creates the default runtime theme). Close the Editor.

Then the AI coder — read core-instructions/09-unity-client.md and .claude/rules/unity.md:
1. Add client/CityBuilder/.gitignore (Unity-specific, only inside this folder).
2. In Packages/manifest.json add "com.citybuilder.simulation": "file:../../../shared/Simulation",
   com.unity.nuget.newtonsoft-json and com.unity.inputsystem if missing.
3. Create Assets/_Project/{Core,Net,Presentation,Tests/EditMode,Tests/PlayMode} with asmdefs as in 09, and a module doc
   per runtime assembly (docs/modules/CityBuilder.Client.Core.md, .Net.md, .Presentation.md).
4. Create a GameRoot MonoBehaviour and a [RuntimeInitializeOnLoadMethod] bootstrap that spawns one
   GameRoot when the Bootstrap scene loads, so no scene YAML needs hand-editing.
5. GameRoot shows a UI Toolkit label "Not connected" using the PanelSettings from Resources.
6. Write [MANUAL] steps for: creating Assets/Scenes/Bootstrap.unity and adding it to Build Settings as scene 0.
Do not add networking yet.
```

#### M0-05 — Unity test foundation · [S]
```text
1. Add an EditMode test that calls CeilDiv from CityBuilder.Simulation (proves the local package compiles
   inside Unity) and an AppInfo.Version test.
2. Add a PlayMode test that loads the Bootstrap scene and asserts exactly one GameRoot exists and no
   errors were logged.
3. Create scripts/dev/test-unity.sh and test-unity.ps1 that run EditMode then PlayMode in batchmode
   (-batchmode -projectPath client/CityBuilder -runTests -testPlatform … -testResults …), print a summary,
   and exit non-zero on failure. Read the Unity path from an env var UNITY_EDITOR with a clear error if unset.
4. Run the script if Unity is available in this environment; otherwise give [MANUAL] steps and say so.
```

#### M0-06 — Day 1 checkpoint · [O]
```text
Act as a senior reviewer of everything created today (use the reviewer subagent on the combined diff of M0-01…M0-05 if
helpful).
Check:
- dependency direction and folder structure vs 03-architecture.md;
- Unity package wiring; no bin/obj inside shared/Simulation;
- warnings-as-errors and CS1591 active where required;
- tests meaningful (not just "true == true");
- every project/assembly has a module doc;
- CI green on main for the merged tasks.
Make only small fixes.
Write docs/adr/ADR-0001-server-authoritative.md, ADR-0002-postgresql-only.md, ADR-0003-command-endpoints.md and
ADR-0004-idempotency.md from docs/adr/TEMPLATE.md and core-instructions/12-decisions.md, and index them in docs/adr/README.md.
Write the first real docs/architecture/overview.md (projects table, references, mermaid diagram as built).
Fill "Commands that work right now" in 13-progress.md and update the marks in AGENTS.md (protected: ask) and docs/getting-started.md.
Run all server tests; run Unity tests if possible.
```

---

### DAY 2 — Simulation engine I: content, workers, research

#### M0-07 — Content model with research and eras · [O] [PLAN]
```text
Read core-instructions/04-simulation-model.md, 23-mvp-content.md, 25-eras-and-research.md (effect types),
24-economy-design.md (last section) and 19 (recipes "Content-only" and "New engine rule").

1. In shared/Simulation, immutable content types: ItemDefinition (id, era, capped, tradable),
   RecipeDefinition (inputs, outputs, Labor), BuildingLevelDefinition (MaxWorkers, WorkersProvided, recipe or none,
   upgrade cost/seconds), BuildingDefinition (cost, build seconds, era, UnlockedByResearch or start, levels),
   ResearchDefinition (id, era, cost, duration, prerequisites, effects), effect types UnlockBuilding /
   LaborReduction(type, permille) / StorageCapBonus(item, amount) as a closed set, EraDefinition (number, id),
   GameRules (grid, builder slots, research slots, base caps, MaxOfflineProductionSeconds, MinCycleSeconds,
   MaxLaborReductionPermille), GameContent (all of it + ContentVersion).
   Construction goes through a factory that validates and returns errors (no exceptions for bad data).
2. Validation: unique ids; references exist; quantities > 0; costs ≥ 0; levels contiguous 1..max;
   houses have WorkersProvided and no recipe; producers have MaxWorkers ≥ 1 per level;
   research graph is acyclic and prerequisites are in the same or an earlier era;
   Σ labor reductions per building type ≤ MaxLaborReductionPermille;
   with ALL reductions applied, CeilDiv(labor, MaxWorkers at max level) ≥ MinCycleSeconds;
   every non-start building is unlocked by exactly one research node.
3. server/content/mvp-content.json with exactly the numbers from 23-mvp-content.md.
4. Infrastructure: JSON loader (System.Text.Json) → GameContent factory; IContentProvider in Application;
   load at startup and fail fast listing all validation errors.
5. Tests: the real mvp-content.json loads; one failing fixture per validation rule.
6. Write docs/how-to/add-building-or-item.md (a worked content-only change) and update the module doc.
No city state yet.
```

#### M0-08 — City state and mutations · [O]
```text
Read 04-simulation-model.md sections 3, 4, 5, 8, 9.

In shared/Simulation implement:
- CityState: Clock, Era, NextBuildingId, Inventory (item → long), Buildings (ordered by Id),
  CompletedResearch (sorted set), ActiveResearch (id, StartedAt, EndsAt) or none.
- Building: Id, TypeId, X, Y, Level, AssignedWorkers, Phase, PhaseStartedAt, PhaseEndsAt (nullable), TargetLevel,
  CycleLabor, LaborRemaining, SegmentStartedAt.
- Derived queries: WorkersProvided (completed houses, by level), WorkersAssigned, FreeWorkers, BuildersBusy,
  Reserved(item), EffectiveCap(item), EffectiveLabor(type, level), IsUnlocked(type).
- CityRules.PlaceBuilding / UpgradeBuilding / AssignWorkers / StartResearch returning MutationResult
  (Ok or an error code from 05-api-contract.md) with exactly the checks and effects in section 8, including:
  upgrade cost checked before refund; refunds may exceed caps; AssignWorkers on a Producing building applies the
  labor-segment update (progress preserved, 0 workers freezes the cycle).
- CityFactory.CreateStarterCity(content).
Tests: every success path and every error code; workers count only completed houses; reassigning mid-cycle
keeps progress (e.g. 180 labor, 3 workers for 20 s → 120 labor left → 4 workers → ends 30 s later);
0 workers → PhaseEndsAt null; research costs are paid up front. Do not implement Advance yet.
```

#### M0-09 — Simulator.Advance · [O] [PLAN]
```text
Read 04-simulation-model.md sections 2, 3, 5, 6, 7, 11 carefully. Implement exactly that algorithm.

- Simulator.Advance(CityState, GameContent, long elapsedSeconds) → SettlementReport.
- Event sources gathered generically: active research end, every non-null PhaseEndsAt.
- Ordering at each time: research completion (apply effects) → building completions ascending Id
  (Constructing → Idle + auto-staffing min(free, MaxWorkers); Upgrading → level up; Producing → outputs) →
  starts ascending Id, only while t ≤ T0 + MaxOfflineProductionSeconds.
- Start rule with labor: effective labor with research reductions, duration CeilDiv(labor, workers).
- Stall reason derivation (NoRecipe, NoWorkers, MissingInput:item, StorageFull:item).
- Advance(…, 0) = zero-length advance. City clock always advances by the full elapsed time.
- SettlementReport: elapsed, productionSeconds, produced, consumed, completedBuildings, completedResearch,
  eraChanged (always false in M0 content) — with sorted output.
Write focused tests as you go: raw producer over several cycles; two-step chain; construction completing
mid-advance with auto-staffing; research completing mid-advance and speeding up the NEXT cycle only;
an event exactly at the target time. Write docs/how-to/add-research-effect.md (how a new effect type is added end to end).
```

#### M0-10 — Engine rule tests · [O]
```text
Add one test per rule in 04-simulation-model.md, named after the rule:
start requires ≥ 1 worker, all inputs, and output space incl. reserved; inputs consumed at start; outputs at end;
duration = CeilDiv(labor, workers) (check rounding with 4 workers on 180 labor = 45 s, 5 workers = 36 s);
reassigning mid-cycle preserves progress; 0 workers freezes and keeps reserved outputs; re-adding workers resumes;
auto-staffing takes min(free, max) and nothing else is automatic; a house completing provides workers;
research completion before building completions at the same t; research effects don't change a running cycle;
unlock enables placement; storage bonus raises the cap; lower id wins scarce inputs; construction blocks production;
upgrade blocks production, refunds the running cycle, keeps workers assigned, raises max workers at the end;
coins have no cap; refund can exceed the cap but blocks new cycles; no starts after the production cap while
construction and research still complete in the dormant tail.
Use small builders (CityFixture) to keep tests readable. Fix any engine bug with the test first.
```

#### M0-11 — Golden scenario #1 · [O]
```text
1. Copy mvp-content.json to server/tests/CityBuilder.Simulation.Tests/Fixtures/content-golden-v1.json
   (frozen; balance changes must not rewrite golden expectations).
2. Implement golden scenario #1 exactly as the table in 23-mvp-content.md: actions at t=0, 30 and 750; assert
   coins, logs, planks, free workers, phases and stall reasons at every checkpoint row (t = 0, 30, 90, 150, 600,
   750, 930, 990, 1050, 1170), and research state at 600.
3. Run the same script as one big advance per action gap vs many 1-second advances and assert identical states.
If the table and the engine disagree, work out by hand which is wrong before changing either, and explain it.
```

#### M0-12 — Engine review · [O]
```text
Strict review of shared/Simulation against 04-simulation-model.md and .claude/rules/simulation.md.
Look for: floats, DateTime, Random, statics; Dictionary/HashSet iteration affecting results; divisions other than
CeilDiv; unchecked arithmetic; C# features above 9 (would break Unity); allocations inside the Advance loop;
era- or research-specific if-statements (effects must be data); spec rules implemented differently than written;
missing error codes; public setters that let callers bypass rules.
Fix issues with a regression test each. If the spec is ambiguous, propose wording, update 04, and add a decision row.
```

---

### DAY 3 — Simulation engine II: offline, invariants, performance

#### M0-13 — Settlement window and production cap · [O]
```text
Read 04-simulation-model.md sections 1–2.
1. Pure function in Simulation: SettlementWindow.Compute(lastSettledUnixSec, nowUnixSec) → (elapsed, newLastSettled);
   negative elapsed → 0 and lastSettled unchanged.
2. Application: SettleCity use case skeleton taking TimeProvider, flooring to whole seconds, calling
   Simulator.Advance (persistence comes on Day 4; use an in-memory fake repo).
3. Tests with FakeTimeProvider: 0 s; 1 s; exactly 8 h; 8 h + 1 s; 30 days (research and construction finish on time,
   production stops starting cycles after 8 h, report.productionSeconds = 8 h); clock moved backwards;
   100 calls 300 ms apart from t = 0 → exactly 29 s elapsed in total (same as one call at t = 29.7 s).
```

#### M0-14 — Property test harness · [O]
```text
Build a seeded random city generator for tests (no new packages needed): random valid placements, upgrades,
worker assignments and research starts over time, random elapsed gaps — always through CityRules, never by poking state.
Properties (≥ 500 seeds each, print seed and operation list on failure):
1. Determinism: the same seed produces identical state and reports twice.
2. Split invariance: for random a, b with a + b ≤ production cap, Advance(a)+Advance(b) == Advance(a+b).
3. Timer invariance: for any split (including beyond the cap), construction/upgrade/research completion times are identical.
Compare states with a structural equality helper that reports the first difference.
```

#### M0-15 — Invariant properties · [O]
```text
Extend the property suite with invariants 4–13 from 04-simulation-model.md section 10:
conservation per item (track spent and refunded from mutation results), no negative stock, stock + reserved ≤ cap from
production, clock monotonic, no production before construction or during upgrade, Σ assigned ≤ provided and assigned ≤ max,
builders ≤ slots and ≤ 1 active research, labor accounting (track worker-seconds per cycle across reassignments),
no starts after the production cap, locked buildings never placed and research prerequisites respected.
Interleave random mutations between advances. Any failure: shrink it by hand to a minimal rule test, fix, keep both.
Write docs/how-to/debug-determinism-failure.md (from a failing seed to a minimal rule test).
```

#### M0-16 — Performance budget · [O]
```text
1. Worst-case city from the spec: 64 producers using a test content file with 10-second cycles at full staffing that
   never stall (uncapped outputs or very large caps, inputs from raw producers), advanced by 8 h, then by 7 days
   (dormant tail). Assert the expected event counts so the test can't pass by stalling early.
2. Measure with Stopwatch over several runs; assert a generous CI limit (e.g. 500 ms) and print the actual time.
   Record the measured numbers in the build log entry and the Simulation module doc.
3. If it exceeds 50 ms here, profile and optimize (priority queue for the next event, no LINQ/allocations in the loop)
   without changing behaviour — the property suite must stay green.
```

#### M0-17 — Display helpers · [S]
```text
Add pure, display-only helpers in shared/Simulation/Runtime/Display/ (documented as not used for authority):
- PerHour(building, workers) for outputs and inputs at a given staffing (for the stepper preview);
- NetPerHour(city, item) from buildings currently able to run;
- ProgressPermille(building, clock) using cycleLabor/laborRemaining/segment fields (0–1000, integers only);
- SecondsUntilNextEvent(building, clock); ResearchProgressPermille(city, clock);
- SecondsUntilFull(city, item) when net rate > 0 (null otherwise).
Tests for each, including frozen cycles and 0-worker buildings contributing 0 to net rate.
```

#### M0-18 — Spec sync and ADRs · [O]
```text
Compare the engine code with 04-simulation-model.md line by line. Update the spec where the code is right and the
text was vague; fix code where the spec is right. Then write docs/adr/ADR-0005-settle-on-access.md,
ADR-0006-production-cap-dormant-tail.md, ADR-0007-shared-engine.md (include the fallback),
ADR-0008-labor-model.md (why progress-preserving reassignment: no double use of workers).
Run the full Simulation suite and record the counts in the build log entry.
```

---

### DAY 4 — Persistence

#### M0-19 — EF Core model, migrations, Docker · [O] [PLAN]
```text
Read core-instructions/06-data-model.md and .claude/rules/server.md.
1. docker-compose.yml at the repo root: service postgres with image postgres:18.6, volume mounted at
   /var/lib/postgresql (PG18 layout — verify in the image docs), healthcheck, credentials from a gitignored .env,
   plus a committed .env.example.
2. Infrastructure: CityBuilderDbContext with persistence models for players, cities (incl. era and active research),
   city_inventory, city_buildings (incl. workers and labor fields), city_research, command_receipts, ledger_entries —
   exactly as in 06 (snake_case, keys, unique and check constraints, indexes, state_version as concurrency token).
3. Initial migration, committed. Connection string from configuration / user-secrets.
4. Development-only: apply migrations at startup. Never in other environments.
5. Write docs/how-to/add-migration.md. Update docs/getting-started.md (database section) and the module doc.
Build and run a migration against the compose database; report the result.
```

#### M0-20 — Repository and mapping · [O]
```text
1. Application ports: ICityRepository (LoadForUpdate(playerId), Save(city aggregate, ledger, receipt)),
   IUnitOfWork, IPlayerRepository.
2. Infrastructure implementation with explicit mapping persistence ↔ CityState (Simulation types stay free of EF).
   Reserved outputs, stall reasons, workers provided/free, effective caps and labor are derived after load, never stored.
3. Mapping round-trip tests (pure, no DB): CityState → rows → CityState is structurally equal, including every phase,
   frozen cycles (null end), active and completed research, and era.
```

#### M0-21 — Testcontainers integration base · [S]
```text
1. IntegrationTests: a shared PostgreSQL 18.6 Testcontainers fixture (one container per test run, a fresh database
   or transaction-reset per test), and a WebApplicationFactory wired to it.
2. Tests (Category=Integration): migrations apply on an empty DB; starter city saves and loads; inventory, buildings,
   research, receipts and ledger persist; unique (city_id, x, y) enforced; quantity ≥ 0 and assigned_workers ≥ 0
   enforced; data survives a new DbContext.
3. Document how to run integration tests (Docker required) in docs/testing/README.md.
```

#### M0-22 — Transaction boundary and locking · [O]
```text
Implement the write transaction pattern from 06-data-model.md:
BEGIN → SELECT … FOR UPDATE on the acting player's city row → load children → work → UPDATE with state_version
check → writes → COMMIT. Lock wait timeout ~3 s maps to CONCURRENCY_CONFLICT.
Provide one Application-level entry point (e.g. ICityTransactionRunner.Run(playerId, work)) so no use case can write
a city without the lock, and no use case can lock a second city (assert this in a test).
Integration tests: an exception after mutation rolls back everything; a version mismatch is detected;
two transactions on the same city serialize (the second sees the first's committed state).
```

#### M0-23 — Settle use case end to end · [O]
```text
Complete SettleCity on top of the transaction runner:
settle with TimeProvider → zero-length advance → persist → aggregated ledger rows per item and direction
(SETTLEMENT_PRODUCED / SETTLEMENT_CONSUMED, ref = settlement id) → research completions into city_research →
state_version + 1 only if anything changed.
Integration tests with FakeTimeProvider: starter city after 0 s, 30 s, 2 h; a city with a 1-hour research left alone
for 30 h (research completes at the right clock, production stops after 8 h); ledger sums equal inventory deltas;
repeated settles at the same instant change nothing; settlement after a host restart continues correctly.
```

#### M0-24 — Concurrency tests and persistence review · [O]
```text
1. Integration tests: 20 parallel settles of one city → final state equals one settle at the final time;
   parallel settles of different cities don't block each other.
2. Persistence audit: no tracking leaks; no client-supplied ids or quantities persisted; all wall timestamps UTC;
   sim times and labor bigint; migrations committed; every write path uses the transaction runner.
3. Write docs/adr/ADR-0009-row-lock-own-city.md (include the lock-ordering rule for the future market).
Fix gaps with tests.
```

---

### DAY 5 — Authentication, command pipeline, first command

#### M0-25 — Development authentication · [S]
```text
Read 07-security-model.md.
1. POST /api/v1/auth/dev-login { displayName } → signed JWT (short expiry, e.g. 12 h) with a player id claim.
   Creates the player and the starter city (CityFactory) on first login, in one transaction, with STARTING_GRANT
   ledger rows for the starting inventory (so the ledger always reconciles).
2. Map the endpoint only in Development; add a startup guard that throws if dev auth is enabled outside Development.
   Signing key from user-secrets/environment, validated at startup (minimum length).
3. ICurrentPlayer resolves PlayerId from the token; endpoints never read a player id from the body.
4. Tests: valid, missing, malformed, expired, wrong-signature tokens; the same displayName returns the same player;
   Production environment → endpoint returns 404 and the startup guard works.
```

#### M0-26 — Command pipeline with idempotency · [O] [PLAN]
```text
Read 03-architecture.md (request lifecycle), 05-api-contract.md (idempotency rules), 06-data-model.md.
Implement one reusable command pipeline used by every command:
validate shape → transaction runner (own-city lock) → receipt lookup by (playerId, requestId):
  same SHA-256 payload hash → return stored response, roll back; different hash → IDEMPOTENCY_CONFLICT
→ settle → mutation via CityRules → on rule error: commit the settlement only, return the code (no receipt)
→ zero-length advance → persist + ledger (COST_*, REFUND_*) + receipt (successful only) → snapshot.
Canonical payload for hashing: the typed command DTO serialized with a fixed property order.
Unique violation on receipt insert → roll back, read the stored receipt, return it.
Application tests with fakes for the ordering and every branch. Make it hard to write a command that bypasses the
pipeline (commands implement an interface the pipeline executes; there is no other write API).
```

#### M0-27 — Idempotency integration tests · [O]
```text
Integration tests (real DB) for the pipeline using PlaceBuilding (or a trivial test command if not ready yet):
sequential retry returns the identical response and adds no ledger rows; retry after host restart;
same requestId + different payload → 409 IDEMPOTENCY_CONFLICT; another player's requestId never matches;
10 concurrent identical requests → exactly one mutation, all callers get the same response;
a business-rule rejection stores no receipt but does commit the settlement.
```

#### M0-28 — Content and city endpoints · [S]
```text
Read 05-api-contract.md.
1. GET /api/v1/content: DTO of items, buildings (levels, max workers, workers provided, recipes with labor,
   unlockedBy), research nodes (cost, duration, prerequisites, effects, era), eras, rules (incl. timeScale = 1),
   contentVersion; deterministic ordering.
2. GET /api/v1/city: runs SettleCity, returns the snapshot DTO exactly as specified (camelCase, enums as strings,
   stallReason strings, workers/builders/research blocks, settlement report).
3. Contract tests on raw JSON (names, types, nulls, sorting) — not only deserialized objects;
   unauthenticated → 401 ProblemDetails with code AUTHENTICATION_REQUIRED.
```

#### M0-29 — PlaceBuilding · [O]
```text
POST /api/v1/commands/place-building { requestId, buildingTypeId, x, y } through the pipeline.
The body has no cost, level, time, worker or player fields; unknown JSON properties are rejected (400).
Integration tests: success (inventory reduced, building Constructing with the right timer, COST_PLACE ledger rows,
receipt); each error code — UNKNOWN_BUILDING_TYPE, BUILDING_LOCKED (Trading Post before Trade Charter), OUT_OF_BOUNDS,
TILE_OCCUPIED, NO_FREE_BUILDER, INSUFFICIENT_RESOURCE; replay; settlement-before-mutation (a construction finishing
during the absence frees the builder slot the command then uses); auto-staffing after completion seen in a later GET.
Assert DB state and ledger, not only status codes. Write docs/how-to/add-command.md using PlaceBuilding as the worked example.
```

#### M0-30 — API attack review · [O]
```text
Attack the API as a malicious client. For each realistic attack add a Security_ test:
extra JSON fields (cost, coins, level, labor, workers on place-building, playerId, phaseEndsAt) → 400 and state unchanged;
another player's building id → 404; negative and huge coordinates; huge displayName; 1 MB body; malformed JSON;
invalid GUID requestId; missing auth; replay storm; dev-login in Production.
Fix what's in M0 scope; list the rest in 07-security-model.md "Known limitations".
```

---

### DAY 6 — Complete the server MVP

#### M0-31 — UpgradeBuilding · [O]
```text
POST /api/v1/commands/upgrade-building { requestId, buildingId } through the pipeline (CityRules.UpgradeBuilding).
Integration tests: success from Idle; success from Producing with refund (REFUND_UPGRADE rows, refund allowed above
the cap); workers stay assigned during the upgrade and the max rises after it; Cottage upgrade raises workers provided
on completion; BUILDING_NOT_FOUND (incl. another player's id); BUILDING_BUSY while constructing/upgrading; MAX_LEVEL;
NO_FREE_BUILDER; INSUFFICIENT_RESOURCE checked before refund.
```

#### M0-32 — AssignWorkers · [O]
```text
POST /api/v1/commands/assign-workers { requestId, buildingId, workers } — absolute count.
Integration tests: assign to an Idle producer starts production in the same response; reassign mid-cycle keeps progress
(check phaseEndsAt against hand-computed values); 0 workers freezes (phaseEndsAt null, stallReason NoWorkers) and
re-assigning resumes; INSUFFICIENT_WORKERS; WORKER_LIMIT (above max, negative); INVALID_ARGUMENT for a Cottage;
BUILDING_BUSY while constructing; allowed while upgrading; idempotent replay; same value twice = no-op success.
Security: moving workers between two buildings repeatedly never produces more than the labor allows (property-style loop over HTTP).
```

#### M0-33 — StartResearch · [O]
```text
POST /api/v1/commands/start-research { requestId, researchId }.
Integration tests: success (cost paid, COST_RESEARCH ledger rows, research block in snapshot); completion during a later
settle applies the effect (Trade Charter → Trading Post placeable; Sharpened Axes → next woodcutter cycle shorter);
UNKNOWN_RESEARCH; RESEARCH_LOCKED (Frame Saw before Sharpened Axes); RESEARCH_IN_PROGRESS; RESEARCH_COMPLETED;
INSUFFICIENT_RESOURCE; two concurrent start-research requests with different ids → exactly one succeeds.
```

#### M0-34 — Error catalogue, rate limits, input limits · [S]
```text
1. Every error path returns RFC 9457 ProblemDetails with the stable code, status, traceId and requestId (when present)
   from 05-api-contract.md. Global exception handler → INTERNAL_ERROR without stack traces outside Development;
   binding/validation failures → INVALID_ARGUMENT. Table-driven tests: each code ↔ status ↔ shape; a test fails if a
   code exists in the catalogue without a mapping.
2. Built-in rate limiter partitioned by player id (by IP for dev-login), values from configuration; 429 → RATE_LIMITED
   with Retry-After. Request body limit for commands (e.g. 4 KB). Tests: burst → 429; normal polling never limited;
   one player's spam doesn't block another.
```

#### M0-35 — Logging, correlation, dev time scale · [S]
```text
1. Structured logs for: auth success/failure, command accepted/rejected (code), idempotent replay, concurrency conflict,
   settlement (elapsed, productionSeconds, events, research completed), rate-limit hits. Include traceId, playerId,
   requestId, command, duration. Never tokens or bodies. Document traceId vs requestId in docs/api/.
2. DevTimeScale: a ScaledTimeProvider used only when Environment is Development and Simulation:DevTimeScale > 1;
   startup throws if the value ≠ 1 outside Development. Expose the active value as rules.timeScale in GET /content
   (always 1 outside Development). Document that the dev database must be reset after changing the scale.
Tests for the guard, the scaled provider's arithmetic, and timeScale = 1 in Production.
```

#### M0-36 — Server golden E2E over HTTP · [O]
```text
Integration test that plays golden scenario #1 (23-mvp-content.md) through the real HTTP API with FakeTimeProvider and
the frozen golden content: dev-login → start-research trade_charter → place cottages → advance → place woodcutter +
sawmill → … → place trading post at 750 → check every checkpoint row via GET /city.
Then continue: assign the Trading Post to 1 worker and verify that coins earned over the next hour stay within one
cycle (±10 coins) of the 2-worker run, while free workers rise by 1.
Assert snapshots match the engine-level golden expectations and that ledger totals reconcile with inventory.
Create docs/api/smoke.http (REST client file) for manual checks. Update 05 if any detail differs (say which way you fixed it).
```

---

### DAY 7 — Unity networking and state

#### M0-37 — API client and DTOs · [S]
```text
Read 09-unity-client.md and 05-api-contract.md.
In CityBuilder.Client.Net: ApiClient (UnityWebRequest wrapped in Awaitable/Task, base URL from a config asset,
timeouts, CancellationToken), DTOs mirroring 05 exactly (camelCase via Newtonsoft settings, enums as strings,
long for quantities, clock and labor), ProblemDetails → ApiError { Code, Status, Detail, TraceId }.
No gameplay logic, no UI references.
EditMode tests: snapshot and content JSON (copied from real server responses into Tests/Fixtures) deserialize;
every error code parses; malformed JSON and non-JSON 502 bodies produce a clean ApiError.
```

#### M0-38 — Auth and content on the client · [S]
```text
1. AuthService: dev-login with a displayName (default from a dev config), token in memory and, in development builds
   only, in PlayerPrefs under a clearly named key. Never store snapshots or balances.
2. ContentService: GET /content → map DTO to Simulation.GameContent through the same validating factory the server uses.
   Invalid content → clear fatal error screen.
3. Startup flow in GameRoot: login → content → GET /city → "Connected" label with coins and era shown.
EditMode tests for the auth state machine and content mapping (use the fixture JSON).
```

#### M0-39 — Client city store and display prediction · [O]
```text
Read 04-simulation-model.md and 09 "Runtime structure".
1. ClientCityStore: holds the last snapshot, receive time (Time.realtimeSinceStartupAsDouble) and an immutable CityState
   built from it; ignores snapshots with a lower stateVersion; raises SnapshotChanged.
2. DisplayClock: snapshot.clock + whole seconds since receipt × rules.timeScale. DisplayCity: Simulator.Advance on a COPY
   of the snapshot state to DisplayClock, recomputed at most once per second (not every frame).
3. Tests: newer snapshot replaces older; older ignored; rejected command leaves the store unchanged; predicted state after
   N seconds equals Simulator.Advance(snapshot, N) exactly (incl. a research completing and an auto-staffed construction);
   a new server snapshot always wins over prediction.
```

#### M0-40 — Command sender and retry policy · [S]
```text
CommandSender: one requestId per player intent, reused for every retry of that intent. Retry only on network errors,
timeouts, 5xx and CONCURRENCY_CONFLICT; max 3 attempts, backoff 0.5/1/2 s; never retry 4xx business errors.
On success → ClientCityStore.Apply(snapshot). A per-intent "pending" state the UI can show.
Worker stepper intents are coalesced: a new target for the same building replaces a not-yet-sent one (debounce ~400 ms).
EditMode tests with a fake transport: retries keep the same requestId; 422 is not retried; success after two timeouts
applies exactly one snapshot; five rapid stepper taps send one command with the final value.
```

#### M0-41 — Polling and lifecycle · [S]
```text
ConnectionState model: Connecting, Connected, Offline, Unauthorized, ServerError, IncompatibleVersion.
Poll GET /city on start, OnApplicationPause(false), after reconnect, and every 120 s while in the foreground
(no polling while paused). Unauthorized → re-login once, then show the state.
Tests for the state machine and the poll scheduler with a fake clock.
```

#### M0-42 — End-to-end checkpoint · [O] · [MANUAL]
```text
Produce exact [MANUAL] steps and then help debug until this works:
1. docker compose up -d postgres; dotnet run the API.
2. Open Unity, press Play in the Bootstrap scene.
3. The client logs in, loads content and city, shows "Connected", coins = 700, era = Early Middle Ages.
4. Stop the server → client shows Offline; start it → client reconnects on the next poll.
Document the steps in docs/testing/LOCAL-E2E.md. Fix every blocker before finishing; no new features.
```

---

### DAY 8 — City view and placement

#### M0-43 — Grid and camera · [S]
```text
1. GridCoordinate (readonly struct) with world↔grid conversion for the grid size from content rules.
2. Ground tiles built from primitives at runtime; tile hover/selection highlight.
3. Camera: angled 3/4 view; pan by drag and zoom by pinch/scroll via the Input System; clamped to the grid.
EditMode tests for conversions and bounds; no economy logic here.
```

#### M0-44 — Building views from state · [S]
```text
1. BuildingViewFactory: medieval placeholder buildings from primitives, looked up by (typeId, era) so later eras can
   add looks: Cottage = small box + pitched roof; Woodcutter's Hut = low box + log pile; Sawmill = longer box + wheel;
   Trading Post = box + awning. Level 2 is 20 % taller. Look data in a small ScriptableObject or code table.
2. CityView reconciles views with DisplayCity by building id: add new, update changed, remove missing.
   Views never call the server.
3. PlayMode test: given a sample snapshot, the right number and types of views exist; applying a second snapshot updates
   and removes correctly with no duplicates.
```

#### M0-45 — Build menu · [S]
```text
UI Toolkit build menu (UXML + USS) generated from GameContent: one card per building type with name, cost, build time,
max workers, and recipe summary ("2 Logs → 1 Plank · 60/h at 3 workers"). Locked cards show "Requires: Trade Charter"
and can't be selected. Hints (unaffordable, no builder) come from the latest snapshot — hints only, never blocking the
server check. Format durations as "2m 30s". Tests for formatting, lock display and hint logic.
Write docs/how-to/add-ui-screen.md using the build menu as the worked example.
```

#### M0-46 — Placement flow · [S]
```text
Select a card → tiles highlight (green free / red occupied, from the last snapshot) → tap a tile → confirm →
CommandSender sends place-building → a translucent "pending" ghost on the tile → on success the real building appears
from the new snapshot; on failure the ghost disappears and the error is shown. Cancel at any step.
No local resource subtraction, no local building creation.
```

#### M0-47 — Feedback for every result · [S]
```text
A toast/banner system in UI Toolkit. Map every error code in 05-api-contract.md to a short human message
(e.g. NO_FREE_BUILDER → "Both builders are busy", BUILDING_LOCKED → "Research needed first",
INSUFFICIENT_WORKERS → "Not enough free workers — build a Cottage"). Success toasts for placement, upgrade,
worker change and research start. A test fails if an error code has no message.
Network errors and timeouts show a retry hint, not a stack trace.
```

#### M0-48 — Client review · [O]
```text
Review client code for: any local economy calculation that changes shown values outside display prediction;
optimistic updates (incl. worker counts); network calls from views; MonoBehaviours doing more than one job;
statics/singletons; allocations or LINQ in Update(); missing cancellation on destroy; C# > 9 features;
era-specific hard-coding instead of (typeId, era) lookups.
Refactor only clear problems; run Unity tests or give [MANUAL] steps.
```

---

### DAY 9 — Making workers, waiting and research visible

#### M0-49 — HUD · [S]
```text
Top HUD (UI Toolkit): one chip per item — icon placeholder, stock, cap (if capped), net rate per hour from the display
helpers ("+60/h", "−30/h", "0/h"); workers free / provided; builders busy / slots; a research progress ring with the
active node's name and time left; an era badge ("Early Middle Ages"); a connection indicator.
Values come from DisplayCity only and refresh once per second. Tests for the chip and ring view-models.
```

#### M0-50 — Building status visuals · [S]
```text
For each building view: a progress ring for Constructing/Upgrading/Producing driven by DisplayClock (ProgressPermille);
a status bubble for NoWorkers, MissingInput(item) and StorageFull(item); a scaffold placeholder while constructing;
slight desaturation when stalled; small worker pips (one per assigned worker, max 6).
Update at most a few times per second; no per-frame allocations.
```

#### M0-51 — Building inspector with worker stepper · [S]
```text
Tap a building → inspector panel: name, level, recipe card with per-hour rates at the current staffing, a worker stepper
(− / + with "3 / 3", disabled at limits or when no free workers) and a live preview of the rate at the pending value
(PerHour helper), status and reason in words ("Waiting for Logs"), time to next event, and an upgrade section
(cost, time, gain: "max workers 3 → 6") with a disabled state and reason when not possible.
Stepper and upgrade send commands through CommandSender with pending states; double taps can't double-submit.
EditMode tests for the inspector view-model: stepper limits, preview numbers, disabled reasons.
```

#### M0-52 — Research screen · [S]
```text
Research screen for the current era: nodes laid out by prerequisites (simple columns are fine), each showing name,
effect text, cost, duration and status (Locked with missing prerequisites / Available / In progress with ring / Done).
"Finance" button sends start-research; disabled with a reason when the slot is busy or coins are short.
Era header shows "Early Middle Ages — 1 / 4 researched" and the rule "complete all to advance" (greyed out in M0).
EditMode tests for node status derivation and button states.
```

#### M0-53 — Welcome-back report · [S]
```text
When a GET /city settlement has elapsedSeconds ≥ 300, show "While you were away (7 h 12 m)": produced and consumed per item,
constructions and upgrades completed, research completed (with its effect), and — when productionSeconds < elapsedSeconds —
"Production paused after 8 h. Check in more often or research storage." Dismissable; never shown for routine polls.
Tests for the view-model, including the capped-production message and research lines.
```

#### M0-54 — Waiting-loop review and tuning · [O] · [MANUAL]
```text
Run the server with DevTimeScale = 60 (reset the dev DB first) and play golden scenario #1 in the Editor, then continue:
research Sharpened Axes, rebalance workers, upgrade a Cottage. Give me [MANUAL] steps, then fix what I report and what you
find in logs: counters jumping backwards when snapshots arrive, progress rings restarting after a worker change, stale stall
reasons, stepper values flickering, research ring wrong after completion, welcome-back errors.
Record any prediction mismatch with the snapshot JSON that caused it and add a regression test.
```

---

### DAY 10 — Resilient client

#### M0-55 — Connection states in the UI · [S]
```text
Show ConnectionState in the HUD. While Offline/ServerError: keep rendering the last snapshot with display prediction,
show "Last synced 3m ago", disable command buttons and steppers with a reason, keep pending intents visible.
Unauthorized → re-login flow. IncompatibleVersion → blocking screen with a clear message.
Tests for button-enable rules per state.
```

#### M0-56 — Proving idempotent retries · [O]
```text
1. Server integration test simulating a lost response: send place-building, discard the response, resend the same body →
   same response, one building, one set of ledger rows. Repeat for start-research and assign-workers.
2. Client test with a fake transport that times out after the server applied the command: the retry with the same
   requestId receives the stored response and the store ends with exactly one new building.
3. Manual check script in docs/testing/: kill the server mid-request and confirm no duplicates.
```

#### M0-57 — Client logging · [S]
```text
Small logging abstraction (Debug/Info/Warning/Error) over UnityEngine.Debug with a level per build type; network logs at
Debug only; never log tokens or full responses in release builds. Include traceId from error responses in Warning/Error
logs so server and client logs can be matched.
```

#### M0-58 — Version and content compatibility · [S]
```text
Client sends its build version in a header (e.g. X-Client-Version). Server logs it and may later reject unsupported versions
(not now). Client: if snapshot.contentVersion ≠ loaded content → refetch /content before applying; if the API reports an
incompatible version → IncompatibleVersion state. Tests for the refetch-then-apply order.
```

#### M0-59 — Mobile-safe UI · [S]
```text
Apply Screen.safeArea padding to the UI root; touch targets ≥ 48 dp (stepper buttons included); USS layouts that work in
portrait and landscape at 16:9 to 20:9; the research screen scrolls; text never overlaps at small widths.
Use the Device Simulator and give [MANUAL] steps for what to look at. No new features.
```

#### M0-60 — Client ↔ server boundary review · [O]
```text
Security review across the boundary: list every field the client sends (should be only intent + requestId); confirm no
client value influences costs, labor, rates, times, worker totals, research or ids; token storage is dev-only; logs contain
no secrets; HTTP is allowed only for development builds. Add tests or fixes for gaps; update 07-security-model.md.
```

---

### DAY 11 — Codebase quality

#### M0-61 — DocTools: generated reference docs · [O] [PLAN]
```text
Read core-instructions/17-documentation-system.md §3 and docs/reference/README.md.
Create tools/DocTools (console, net10.0; G-Dependency applies to any new package — prefer none) with two commands:
- generate: writes docs/reference/generated/*.md:
  - content.md: items, buildings (levels, workers, recipes with per-hour at full staffing), research tree, from server/content/*.json;
  - errors.md: the error catalogue with HTTP status and meaning;
  - endpoints.md: from the OpenAPI document (build the API in-process or read a generated openapi.json);
  - configuration.md: every options class property with default and validation;
  - db-schema.md: tables, columns, keys and constraints from the EF Core model;
  - project-map.md: projects/assemblies and their references (mermaid).
  Each file starts with "Generated by tools/DocTools — do not edit".
- check: regenerates into a temp folder and exits non-zero with a diff summary if anything differs from the committed files.
Output must be deterministic (sorted, no timestamps). Add docs/modules/DocTools.md. Wire `check` into CI (the step already
exists and activates when tools/DocTools exists). Replace the hand-maintained tables in docs/getting-started.md
(Configuration) with links to the generated files. Keep 05/06/23 as normative specs, with a note that the generated files show the as-built state.
Tests: generation is deterministic; the check detects a modified file.
```

#### M0-62 — Quality script · [S]
```text
scripts/dev/quality.sh (and quality.ps1 for PowerShell-only machines) runs exactly what CI runs, locally:
check-docs.sh --base origin/main → dotnet format --verify-no-changes → build → fast tests → integration tests (skip with a clear
message if Docker is not running) → DocTools check → Unity tests (skip with a clear message if UNITY_EDITOR is unset).
Print a summary table at the end; non-zero exit on any failure; never hide output.
Update the marks in AGENTS.md (protected: ask) and docs/getting-started.md. /finish-task now runs quality.sh.
```

#### M0-63 — Documentation completeness pass · [O]
```text
Run /docs-sync (docs-auditor subagent + check-docs + DocTools check) over the whole repository and fix every confirmed finding:
- every module doc is accurate (key types exist, invariants and extension points are correct) and shows `Last updated: M0-63`;
- docs/architecture/overview.md has the as-built project table and a request-lifecycle sequence diagram (mermaid);
- every how-to listed in docs/how-to/README.md exists and its steps match the current code paths in core-instructions/19;
- docs/getting-started.md works from a clean clone (cross-check with M0-77 later);
- core-instructions/04, 05, 06 and 09 match the implementation, with gaps either fixed in the docs or logged as follow-ups or CRs;
- the build log index is complete, and every merged task has a Done entry with decisions.
Write a short audit summary into this task's build log entry (findings, fixed, deferred).
```

#### M0-64 — Ledger audit · [O]
```text
Application service LedgerAuditor: for a city, sum ledger deltas per item (from STARTING_GRANT on) and compare with inventory;
report mismatches. Integration test running a long random sequence of all four commands and settles over HTTP with
FakeTimeProvider, then auditing every city: zero mismatches. Run it locally via a test or a tiny CLI in tools/, not an HTTP
endpoint. (This auditor becomes cross-player in M2 when the market exists.)
```

#### M0-65 — Banned APIs and architecture tests · [S]
```text
Add Microsoft.CodeAnalysis.BannedApiAnalyzers to src projects with BannedSymbols.txt banning DateTime.Now, DateTime.UtcNow,
DateTimeOffset.Now, DateTimeOffset.UtcNow (use TimeProvider) and Thread.Sleep. Extend architecture tests: Api is the only
project referencing ASP.NET hosting; no Simulation type has public setters on state; shared code has no float/double/decimal
fields; no engine source file contains a literal research id or era number (scan for content ids).
Fix violations.
```

#### M0-66 — Architecture fitness and dependency review · [O]
```text
Review the repository as if accepting a large pull request:
- references, naming vs 31-glossary.md, duplicated logic between server and client;
- every write goes through the pipeline and the own-city lock;
- readiness for the M2 seams (mailbox events in Advance, era transition, effect types);
- docs vs code drift; test quality.
Dependency audit: list every NuGet and Unity package with why it exists; flag unused ones and anything not on the approved list
in 14; versions pinned centrally and lock files current. Removing an unused package is allowed; adding or upgrading is G-Dependency.
Write docs/ARCHITECTURE-FITNESS-REVIEW.md (findings, severity, fixed/not fixed, "M2 readiness" notes, dependency table) and
link it from docs/README.md. Fix only high-value issues.
```

---

### DAY 12 — Balance simulator and content pass

#### M0-67 — Balance simulator · [O] [PLAN]
```text
Read 24-economy-design.md. Create tools/BalanceSim (console, net10.0) referencing CityBuilder.Simulation and loading
server/content/mvp-content.json.
- Strategies (simple, documented heuristics using CityRules only): GreedyCoins, ResearchRush (always keeps the research
  slot busy, cheapest available first), Balanced. Every strategy decides builds, upgrades, worker assignment and research.
- Session patterns: e.g. "08:00,13:00,20:00 daily", "every 4 h", "once a day".
- Run N hours; output a markdown summary + CSV timeline: inventory, buildings, workers free/assigned, research, coins/h,
  stall share by reason, time-to-first-coin, time-to-each-research, time-to-all-research, time-to-max-level.
- Deterministic: same args → same output (test it).
Add docs/modules/BalanceSim.md and docs/how-to/run-balance-simulator.md.
```

#### M0-68 — Strategy runs and findings · [O]
```text
Run all strategies × patterns for 72 h. Compare with the goals in 24-economy-design.md (first research in minute 1,
first coins ≤ 20 min, something worth doing every check-in, next research affordable within one or two absences).
Write docs/balance/REPORT-mvp-1.md with tables and a short list of proposed number changes and the expected effect of each.
Note how often workers sit idle and which research order wins. Do not change content yet.
```

#### M0-69 — Apply approved content changes · [S] · [MANUAL] decision
```text
[MANUAL] Review docs/balance/REPORT-mvp-1.md and tell Claude which changes to apply.
Apply exactly the approved changes to server/content/mvp-content.json, bump contentVersion to mvp-2, update
23-mvp-content.md in the same commit (but NOT the golden scenario table, which belongs to the frozen golden content),
rerun the simulator and append before/after results to the report. If any non-golden test hard-coded a number,
make it read from content instead.
```

#### M0-70 — Content validation hardening · [S]
```text
Add graph checks to content validation: every recipe input is produced by some recipe or the starting grant; every
non-coin item is consumed or spent somewhere; level 2 is never strictly worse than level 1; upgrade costs are
non-decreasing; every research node is reachable from the era's roots; every era-1 building is reachable via start or
research. Errors name the exact building/item/level/research. Tests with small broken fixtures for each check.
```

#### M0-71 — First-session polish · [S]
```text
Polish only the first 15 minutes: an empty-city hint ("Finance your first research, then build Cottages for workers"),
one-line contextual hints for the first worker change and the first stalled building, readable durations, clear stall
reasons, sensible default camera framing. No tutorial system, no new mechanics.
```

#### M0-72 — Playtest and smoke checklist · [O] · [MANUAL]
```text
Write docs/testing/UNITY-SMOKE-TEST.md: numbered steps covering login, golden scenario #1, worker reassignment,
all four research nodes, upgrades, server restart, app close/reopen (welcome back incl. capped production), offline server,
reconnect. Then guide me through it with DevTimeScale = 60 and turn every problem I report into an issue list in
the build log entry (P0–P3), with a one-line summary under "Open issues" in 13-progress.md. Fix P0/P1 issues found today,
each with a regression test where testable.
```

---

### DAY 13 — End-to-end, attacks, concurrency

#### M0-73 — HTTP end-to-end suite · [O]
```text
Integration suite over the real API + PostgreSQL with FakeTimeProvider and frozen content:
new player → research Trade Charter → cottages → woodcutter + sawmill → trading post → failed duplicate tile →
failed locked building → failed over-assignment → worker rebalance → Sharpened Axes + Frame Saw → upgrades (with refund)
→ 30 h absence (research done on time, production capped at 8 h) → host restart → reload.
Assert snapshots, final DB rows, receipts, and that LedgerAuditor reports zero mismatches.
```

#### M0-74 — Attack suite · [O]
```text
Create Security/AttackTests.cs. One test per attempt, each named after the protected property:
inject resources, costs, labor, rates, levels, timers, worker totals or playerId via JSON; act on another player's
building; replay; same requestId with a different payload; impossible coordinates; upgrade past max; assign above max
or below 0; assign more than free; move workers back and forth to gain labor; place a locked building; start research
out of order or twice; spam beyond rate limits; dev-login and DevTimeScale in Production.
Each test asserts the response AND that state and ledger are unchanged (or changed exactly as allowed).
```

#### M0-75 — Concurrency suite · [O]
```text
Concurrent scenarios (real DB, parallel HTTP calls):
1. Two placements whose combined cost exceeds coins → exactly one succeeds.
2. Two placements on the same tile → one succeeds, one TILE_OCCUPIED.
3. The same request ×10 concurrently → one mutation, identical responses.
4. Upgrade + GET /city racing on a producing building → consistent refund and ledger.
5. Two assign-workers on different buildings that together exceed free workers → one INSUFFICIENT_WORKERS.
6. Two start-research with different ids → one RESEARCH_IN_PROGRESS.
Expect: no negative stock, no over-assigned workers, no duplicate buildings or ledger rows, auditor clean. Fix races found.
```

#### M0-76 — Unity smoke run · [S] · [MANUAL]
```text
Walk me through docs/testing/UNITY-SMOKE-TEST.md step by step, collect results, fix what fails (each fix with a test where
possible), and record the final pass/fail table in the build log entry and at the end of docs/testing/UNITY-SMOKE-TEST.md.
```

#### M0-77 — Clean-clone test · [S]
```text
Clone the repository into a fresh temporary directory and follow README.md literally: prerequisites, .env from .env.example,
user-secrets for the signing key, docker compose, migrations, server run, tests, Unity package resolution.
Every missing step, variable or command → fix README or scripts. Do not rely on anything from the original working copy.
```

#### M0-78 — Security boundaries report · [O]
```text
Write docs/security/MVP-SECURITY-BOUNDARIES.md from 07-security-model.md and the actual tests: what the server validates,
what the client may assume, auth model, idempotency, locking (own city only), time model (incl. production cap and
DevTimeScale guard), worker and labor protections, rate limits, logging policy, known attack surface, known limitations,
and a short "before the market (M2) we must add…" list. Link each protection to its test. Precise language; no "unhackable".
```

---

### DAY 14 — Freeze and handoff

#### M0-79 — Bug triage · [O]
```text
Run quality.sh (all suites) and the smoke checklist results. Classify every open issue P0 (won't open/build), P1 (core loop
broken), P2 (recoverable), P3 (polish). Fix all P0 and P1 with regression tests. No new features.
```

#### M0-80 — Code-freeze review · [O]
```text
Lead-engineer review before external testers: no TODOs in critical paths; no client authority; no secrets; every write uses
the pipeline and an own-city transaction; every command has negative tests; every endpoint has an auth rule; no swallowed
exceptions; no skipped tests without a written reason; no XP/level concepts crept in. Fix high-risk items only.
```

#### M0-81 — Invariant gap analysis · [O]
```text
Map every invariant (04 §10), every API error code (05) and every threat (07) to the tests that cover them.
Write the matrix to docs/testing/COVERAGE-MATRIX.md and add focused tests for any gap.
Optimize for protected invariants, not coverage percentage.
```

#### M0-82 — Development build · [S] · [MANUAL]
```text
Prepare a development build that reaches the city screen: Windows/macOS standalone first, Android second (iOS only on macOS
with Xcode). Server URL configurable per build without code changes; HTTP allowed only in development builds; link.xml
preserving DTOs for IL2CPP. Give [MANUAL] build steps, fix build errors (missing scenes, stripping, platform defines),
and write docs/BUILD.md including the adb reverse tip for Android.
```

#### M0-83 — Final MVP audit · [O]
```text
Audit the full chain: Unity input → intent → DTO → HTTP → auth → validation → own-city lock → receipt → settle (labor,
research, production cap) → rules → zero-length advance → persistence → ledger → snapshot → client store → prediction → UI.
For each link: source of truth, failure mode, and the tests covering it. Write docs/MVP-AUDIT.md and check every box of
the Definition of Done in this plan (section 1) with evidence. No new features.
```

#### M0-84 — M0 close-out · [S]
```text
Close the milestone so the next coder (or the owner, months later) can start M1 cold:
1. Verify the M0 exit criteria in core-instructions/10-milestones.md and the Definition of Done in this plan (§1). Link evidence.
2. Board: every task is Done or Skipped (with a reason). Move CHANGELOG "Unreleased" lines into a "0.1.0 — M0" section.
3. docs/HANDOFF-M0.md: what exists; how to run everything (link getting-started); current security guarantees and limitations;
   known issues; and the M1 backlog (Era I complete, from 10-milestones.md) as 10–15 concrete items ordered by value, each phrased
   so it can become a CR. Add a short "M2 readiness" list (era transition, mailbox events, accounts, market).
4. Resume point: state IDLE, "M0 complete", next step "owner approves M1 CRs".
5. Run quality.sh one last time and leave the repository green.
```

---

## 9. Small prompts for when something breaks

Utility prompts for situations outside the task flow. They run *inside* the current task (log what you do in its build log entry).
If one is needed often, turn it into a skill via a CR.

**A — Compile error** · [S]
```text
Read this compiler error and the surrounding code. Find the root cause, make the smallest safe fix, rebuild, run the
related tests. Don't touch unrelated code. Report root cause, files, tests run.
Error: [PASTE]
```

**B — Failing test** · [S] → [O] if unclear
```text
This test fails. First decide whether the production code, the test, or the environment is wrong — explain your reasoning
before changing anything. Never weaken the assertion to pass. Fix, rerun the test, then the wider suite.
Failure: [PASTE]
```

**C — Review one file** · [O]
```text
Review [PATH] for architecture, security, nullability, error handling, testability, client authority and coupling.
Change nothing unless you can name a concrete problem; list findings by severity first.
```

**D — Break this command** · [O]
```text
Assume a malicious client. Find realistic ways [COMMAND] could create resources, skip costs or waiting, reuse workers,
duplicate effects, touch another player's data, or race itself. Add a Security_ test per realistic exploit, then fix.
Update 07-security-model.md.
```

**E — Safe refactor** · [S]
```text
Refactor [AREA] for readability only. Behaviour and API contracts stay identical; run the relevant tests before and after;
no new abstractions unless they remove real duplication. Explain each change in one line.
```

**F — Explain before changing** · [O]
```text
Don't modify code. Explain current behaviour, the architecture involved, what would change, risks, files likely touched,
and which tests should exist first. Wait for my go-ahead.
```

**G — Client/server contract drift** · [S]
```text
Compare Unity DTOs with server DTOs and 05-api-contract.md: names, nullability, enum strings, integer types, required fields,
error shape, version fields. Inspect both sides — don't guess. Report, then fix confirmed drift and add a contract test
using a real server response fixture.
```

**H — Migration problem** · [O]
```text
Inspect this migration error. Determine current vs intended schema and whether data could be lost. No destructive shortcuts
unless justified for local dev. Fix and prove migration from an empty database and from the previous migration.
Error: [PASTE]
```

**I — Performance sanity check** · [S]
```text
Small performance audit of [AREA] only: N+1 queries, allocations in hot paths (Advance loop, Update()), repeated
serialization, polling too often. Fix only obvious issues; measure before and after.
```

**J — Docs sync** · [S]
```text
Compare code with core-instructions/ and README. Find statements that are no longer true and fix the docs to describe the
actual implementation (or flag where the code should change instead). No speculative features.
```

**K — Determinism failure** · [O]
```text
A property/golden test failed with seed [SEED] and this operation list: [PASTE]. Reproduce it as a minimal rule test, find
which rule in 04-simulation-model.md is violated (or ambiguous), fix the engine with the minimal test kept, and confirm the
full property suite passes.
```

**L — Unity manual steps** · [S]
```text
I need to do [TASK] in the Unity Editor. Give numbered click-by-click steps for Unity 6.3, the expected result after each
step, and how to verify it worked. Then tell me what to paste back to you.
```

**M — Resume after a break** · [S]
```text
Read 13-progress.md and the last 10 commits (git log --oneline -10). Summarize where we are, what is unfinished or broken,
and the exact next task from the plan. Don't change code. (Claude Code: /resume does this.)
```

**N — I changed the design** · [O]
```text
I edited [FILES in core-instructions/ 21–26]. Read the diff (git diff), list what it changes for the engine, content, API,
data model, client and tests, and propose the smallest set of tasks or CRs to apply it. Flag anything that conflicts with the
pillars in 22 or with already-built code. Don't change code yet.
```

---

## 10. Test matrix (minimum permanent set)

### Simulation
- [ ] Integer helpers (CeilDiv, permille reduction) edge cases
- [ ] Each content validation rule · real content loads
- [ ] Each rule in M0-10 (labor, workers, auto-staffing, research effects, ordering, caps, upgrade, dormant tail)
- [ ] Each mutation success path and error code (place, upgrade, assign workers, start research)
- [ ] Golden scenario #1 (exact table) · big advances == 1-second advances
- [ ] Settlement window: 0 s, cap, cap + 1, 30 days, clock backwards, sub-second calls
- [ ] Properties: determinism, split invariance, timer invariance, conservation, non-negative, caps, worker limits,
      labor accounting, slots, no starts after the cap, unlock/prerequisite respect
- [ ] Performance budget (8 h and 7-day windows) with asserted event counts
- [ ] Display helpers (per hour at a staffing, net rate, progress permille, research progress, time to full)

### Server application and persistence
- [ ] Pipeline ordering and every branch (fakes)
- [ ] Mapping round-trip (frozen cycles, research, era) · migrations · constraints
- [ ] Rollback on exception · settlement commits on rule rejection
- [ ] Own-city row lock serializes · version guard detects unlocked writes · no second-city lock possible
- [ ] Idempotency: sequential, after restart, conflicting payload, other player, concurrent duplicates
- [ ] Ledger reconciles with inventory (auditor)

### API
- [ ] Health · dev login (and 404 in Production) · token cases
- [ ] Content and city contracts (raw JSON)
- [ ] Place / upgrade / assign workers / start research: success + every error code, DB state asserted
- [ ] ProblemDetails mapping for every code · no stack traces
- [ ] Rate limits per player · body size limit · unknown JSON fields rejected
- [ ] DevTimeScale guard · timeScale = 1 outside Development

### Unity
- [ ] DTO fixtures deserialize · error parsing · malformed bodies
- [ ] Auth state machine · content mapping
- [ ] Store ordering by stateVersion · prediction == engine · server snapshot wins
- [ ] Command sender: same requestId on retry, no retry on 4xx, no double submit, stepper coalescing
- [ ] Poll scheduler and connection states
- [ ] Grid math · view reconciliation (PlayMode) · bootstrap (PlayMode)
- [ ] View-models: HUD chips and research ring, inspector stepper, research node states, welcome back, error messages for every code

### End-to-end
- [ ] Golden scenario over HTTP · full E2E suite (M0-73)
- [ ] Attack suite (M0-74) · concurrency suite (M0-75)
- [ ] Unity smoke checklist on a development build

---

## 11. API at a glance

```text
GET  /health
POST /api/v1/auth/dev-login                 (Development only)
GET  /api/v1/content                        (items, buildings, research tree, eras, rules)
GET  /api/v1/city                           (settles to now, persists, returns snapshot)
POST /api/v1/commands/place-building        { requestId, buildingTypeId, x, y }
POST /api/v1/commands/upgrade-building      { requestId, buildingId }
POST /api/v1/commands/assign-workers        { requestId, buildingId, workers }
POST /api/v1/commands/start-research        { requestId, researchId }
```

The full contract, snapshot shape and error catalogue are in `core-instructions/05-api-contract.md`.
Never add endpoints like `give-coins`, `set-resources`, `set-level`, `finish-research` or `set-time`, not even for debugging.

---

## 12. What not to build in these two weeks

The full list is `core-instructions/30-out-of-scope.md`. Headlines:
- No era transitions or Era II content.
- No market, guilds, mailbox or social features.
- No research beyond the 3 effect types.
- No land expansion, roads, demolition, needs or expeditions.
- No monetization, analytics, Redis, cloud, real accounts or final art.
- Never XP or city levels.

The sprint proves the **spine**: authoritative, deterministic, real-time production run by workers, plus research progression,
with a client that makes waiting legible.

---

## 13. After the sprint

Milestones with exit criteria are in `core-instructions/10-milestones.md`. Every post-M0 feature enters through a CR
(`docs/changes/`), and the M1 backlog in `docs/HANDOFF-M0.md` (written in M0-84) is the starting list.

| Milestone | Focus | Rough size |
|---|---|---|
| M1 | Era I complete: ~8 items, ~10 buildings, ~12 research nodes, land expansion, guest accounts, tutorial, notifications, first art | 4–5 weeks |
| M2 | Era II: era transition, **open market**, guilds, real accounts, operator tools | 6–8 weeks |
| M3 | Soft-launch readiness: server container, cloud, CD, Unity tests in CI, attestation, analytics, GDPR, monetization v1 | ~4 weeks |
| M4 | Era III: population needs, expeditions | ~6 weeks |
| M5+ | Eras IV–VI as major updates | per era |

Run the balance simulator before every content change, and add market agents to it in M2.

---

## 14. Risks and honest trade-offs

1. **The pace is aggressive.** Six tasks a day with real review is a full workday. v3 added workers and research, and v4 adds
   process overhead (logs, docs, PRs) of about 10–15 %.
   Expect 17–21 days. Protect Days 2–5 and 13; cut polish first, never the process steps.
2. **The open market turns this into an MMO.** It brings real benefits (social, long-term economy) and real costs:
   - it needs enough simultaneous players to have liquidity;
   - it needs economy monitoring, moderation, anti-fraud and rollback tools;
   - one duplication bug hurts everyone.
   The design keeps it in Era II and adds NPC price floors and ceilings, but **M2 is the riskiest milestone of the project**.
   Plan a closed test with real players before any launch.
3. **"Complete all research to advance" can become a wall.** If one node is expensive or long, the whole era stalls.
   The design rule (no node > ¼ of the era length) helps. Playtests in M1 decide whether to keep "all" or switch to "all core nodes".
4. **Waiting games can feel empty.** Worker decisions and research choices give each check-in a decision, but only
   playtesters can confirm it's fun. If check-ins feel empty, the fix is more decisions per check-in, not tapping.
5. **Settle-on-GET is unconventional.** `GET /city` writes. It is safe and idempotent in effect, but never put an HTTP cache in front of it.
6. **One engine, three runtimes** (.NET 10, Mono, IL2CPP). Integer-only rules and golden tests running in Unity EditMode are
   the safety net. If the local package fights you, use the decision-007 fallback in `12`.
7. **Claude can't see the Unity Editor.** The [MANUAL] steps reduce the friction but don't remove it. Budget extra time on Day 1 and Days 7–9.
8. **Six eras is years of content.** Each era is a large content and art effort. Ship 1.0 with Eras I–III and treat later eras as updates.
9. **Placeholder numbers.** Everything in `23` is a starting guess until the simulator and playtests say otherwise.
10. **Process is enforced only partly by machines.** Hooks and CI check that logs, progress, module docs and generated docs
   exist and are current. They can't check that a decision log is *honest and complete*. The reviewer subagent and the owner's
   PR review are the remaining safety net. Read at least the Decisions table of each build log entry before merging.
11. **The owner is a bottleneck by design.** Every PR waits for your merge. If that's too slow, the cheapest relaxation is
   letting `[S]` docs-only PRs auto-merge on green CI. That's an owner decision (`15`), not something the AI may assume.
12. **Windows needs Git Bash** for hooks and scripts (Git for Windows provides it). Claude Code runs shell hooks through Git Bash.

---

## 15. Sources (checked October 2026)

- Unity 6 releases and LTS support: https://unity.com/releases/unity-6/support
- PostgreSQL versions and support dates: https://endoflife.date/postgresql · https://www.postgresql.org/docs/18/
- .NET server guidance: https://learn.microsoft.com/en-us/dotnet/standard/choosing-core-framework-server
- Claude Code memory (CLAUDE.md, imports, `.claude/rules/`, AGENTS.md): https://code.claude.com/docs/en/memory
- Claude Code hooks: https://code.claude.com/docs/en/hooks · skills: https://code.claude.com/docs/en/skills
- Claude Code subagents: https://code.claude.com/docs/en/sub-agents · permissions: https://code.claude.com/docs/en/permissions
- Tap Tap Builder official page: https://herocraft.com/en/games/395e481a-82d7-4428-8a51-91d151562e13/
- Tap Tap Builder on Google Play: https://play.google.com/store/apps/details?id=com.herocraft.game.free.taptapbuilder
- Tap Tap Builder on the App Store: https://apps.apple.com/us/app/-/id1097114930
