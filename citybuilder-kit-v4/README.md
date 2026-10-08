# CityBuilder (working title)

A mobile city builder that grows from a medieval village into a space-age metropolis through **research, not tapping**.
- Production runs in real time, staffed by workers you assign.
- Research moves your city through eras, and each era adds new mechanics.
- From Era II, you trade with other players on an open market.
- No XP, no city levels: your progress is your research, your buildings and your land.

**Status:** pre-development. Milestone **M0** is a vertical slice of Era I. The live state is in
`core-instructions/13-progress.md`, and the task board is in `docs/plan/M0-PLAN.md`.

**Stack:** Unity 6.3 LTS (client) · .NET 10 / ASP.NET Core (server-authoritative API) · PostgreSQL 18 ·
a shared deterministic simulation engine (C#).

## How this project is built
The owner directs. AI coders (Claude Code and others) build the project **one after another**, and each session
picks up from what the repository says. Every decision is logged, docs change together with code, and every change goes
through a pull request that the owner merges. See `CONTRIBUTING.md` and `AGENTS.md`.

## Documentation
Start at [docs/README.md](docs/README.md), the map of all documentation (game design, architecture, decisions,
build log, how-tos, reference). Setup: [docs/getting-started.md](docs/getting-started.md).

## Repository layout
| Path | Contents |
|---|---|
| `core-instructions/` | Rules, design and contracts (what must be true) |
| `docs/` | The system as built, the plan, the build log, ADRs, change requests |
| `shared/Simulation/` | The deterministic game engine (used by server and client) *(from M0-03)* |
| `server/` | ASP.NET Core API, application, domain, infrastructure, tests *(from M0-02)* |
| `client/CityBuilder/` | Unity project *(from M0-04)* |
| `tools/` | DocTools (generated docs), BalanceSim (economy simulator) *(from M0-61, M0-67)* |
| `scripts/dev/` | Developer and process scripts |
| `.claude/`, `.githooks/`, `.github/` | AI tool config, git hooks, CI and PR template |

## License
Proprietary, all rights reserved (owner to decide before any public release).
