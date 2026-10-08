# Architecture

## System view

```
Unity client ──HTTPS/JSON──▶ ASP.NET Core API ──▶ Application ──▶ Domain + Simulation
   │  renders snapshots                 │  auth, DTOs     │ use cases,      │ pure rules,
   │  runs Simulation for display only  │  ProblemDetails │ transactions    │ no I/O
   ▼                                    ▼                 ▼
 local view state                  rate limiter      Infrastructure ──▶ PostgreSQL 18
                                                    (EF Core, locks)    (source of truth)
```

There is no realtime connection. The client polls `GET /city` on start, on resume, after a reconnect,
and every 2 minutes in the foreground. Every command response carries the full authoritative snapshot.

## Request lifecycle (every city request)
1. Authenticate → `PlayerId` from the token.
2. Validate the request shape (before touching the database).
3. `BEGIN` → `SELECT … FOR UPDATE` on the player's own city row → load buildings, inventory and research.
4. Commands only: look up `(PlayerId, requestId)`. If found with the same payload hash, return the stored
   response and roll back. If found with a different hash → `IDEMPOTENCY_CONFLICT`.
5. **Settle:** `Simulator.Advance(city, nowSec − LastSettledUnixSec)` → settlement report.
   (Later: mailbox deliveries are passed in as timed events, see `04` §13.)
6. Commands only: apply the mutation via engine rules. On a rule failure, return the error code;
   the settlement from step 5 is still committed, because time passing is never undone.
7. Zero-length advance → canonical state.
8. Write the city, buildings, inventory, research, ledger entries and command receipt; increment `StateVersion`.
9. `COMMIT` → map to DTO → respond with the snapshot (+ settlement report).

## Projects and dependency direction

| Project | May reference | Must not reference |
|---|---|---|
| `shared/Simulation` (`CityBuilder.Simulation`) | the BCL only (netstandard2.1) | Unity, ASP.NET, EF Core |
| `CityBuilder.Domain` | Simulation | ASP.NET, EF Core |
| `CityBuilder.Application` | Domain, Simulation | ASP.NET, EF Core |
| `CityBuilder.Infrastructure` | Application, Domain, Simulation, EF Core, Npgsql | ASP.NET endpoint types |
| `CityBuilder.Api` | Application, Infrastructure (composition root only) | — |
| Unity `CityBuilder.Client.*` | Simulation package, Newtonsoft | server projects |

- **Simulation:** content definitions (items, buildings, recipes, research, eras), city state, the `Simulator`, and pure mutation rules.
- **Domain:** server-only concepts — player, ledger entry, command receipt, settlement record (later: market order, mailbox delivery).
- **Application:** use cases (`SettleCity`, `PlaceBuilding`, `UpgradeBuilding`, `AssignWorkers`, `StartResearch`),
  ports (`ICityRepository`, `IUnitOfWork`, `IContentProvider`), and the command pipeline.
- Architecture tests enforce this table by inspecting assembly references.

## Cross-player rule (for the future market and guilds)
A transaction locks and writes only the **acting player's** city. Anything that must reach another player
goes into a mailbox table that the other player's next settle applies. Shared aggregates (market order books,
guild projects) are locked in a fixed order: own city → shared aggregate → rows by ascending id. See `26`.

## Repository layout

```
CityBuilder/
├── AGENTS.md · CLAUDE.md        # AI entry points (any tool · Claude Code)
├── README.md · CONTRIBUTING.md · CHANGELOG.md
├── global.json                  # pinned .NET SDK (M0-02)
├── .claude/                     # settings (permissions, hooks), hooks/, rules/, skills/, agents/
├── .githooks/ · .github/        # git hooks · CI workflow, PR template, dependabot
├── core-instructions/           # this folder (normative)
├── docs/                        # descriptive: plan/, build-log/, changes/, adr/, architecture/, modules/,
│                                #   how-to/, reference/(generated/), testing/, security/, balance/
├── shared/
│   ├── Simulation/              # Unity local package: package.json, Runtime/*.asmdef, Runtime/**/*.cs
│   └── Simulation.Build/        # CityBuilder.Simulation.csproj (compiles ../Simulation/Runtime/**/*.cs)
├── server/
│   ├── CityBuilder.slnx
│   ├── content/mvp-content.json
│   ├── src/  CityBuilder.Api · .Application · .Domain · .Infrastructure
│   └── tests/ CityBuilder.Simulation.Tests · .Domain.Tests · .Application.Tests · .IntegrationTests
├── tools/DocTools/              # generates docs/reference/generated (M0-61)
├── tools/BalanceSim/            # headless economy simulator (M0-67)
├── client/CityBuilder/          # Unity project
├── docker-compose.yml
└── scripts/dev/                 # check-docs, show-task, new-log-entry, setup-hooks, test-unity, quality
```

Why the split under `shared/`: Unity imports every file inside a package folder, including `bin/` and `obj/`.
Keeping the `.csproj` in a sibling folder keeps build output out of Unity.

## Content pipeline
`server/content/*.json` (items, buildings, recipes, research tree, eras, rules) → loaded and validated at
server start (fail fast) → served by `GET /content` with a `contentVersion` → the client renders names,
costs, recipes and the research tree from it. The client never bundles its own game numbers for logic.
Adding an era later means adding content plus the code for that era's major mechanics, not changing the engine core.

## Future seams (do not build yet)
Market service and mailbox (M2), guild service (M2), real accounts (M1), push notifications, Redis, background
jobs (order expiry, receipt cleanup), cloud hosting. Each plugs in behind an Application port and uses the same command pipeline.
