# AGENTS.md — entry point for every AI coder

**CityBuilder** (working title) is a mobile city builder.
- The city starts in the Middle Ages, and **research** moves it through eras.
- Production runs in real time with **assigned workers**.
- Players trade on an **open market** from Era II.
- There is no XP and no city level.
- Stack: Unity 6.3 LTS client · .NET 10 server-authoritative API · PostgreSQL 18 · a shared deterministic engine.
- Current milestone: **M0, a vertical slice of Era I**.

## You are one of several AI coders working one after another
You don't remember earlier sessions, and the next coder won't remember yours. **The repository is the only memory.**
Anything not committed and pushed is lost. So you work in small, checkpointed, logged steps.

## Session start — read these first (in this order)
1. `core-instructions/02-principles.md`: the non-negotiable rules.
2. `core-instructions/13-progress.md`: the **resume point**. If it says IN PROGRESS, you are resuming someone's work.
3. `core-instructions/16-session-protocol.md`: how to start, checkpoint, finish and resume.
4. The task: `bash scripts/dev/show-task.sh <ID>`, plus every file the task names.
Then check the repo state (`git status`, branch, last commits) and that hooks are enabled (`git config core.hooksPath` = `.githooks`).

## Read before working in an area
| If the task touches… | Read first |
|---|---|
| what needs owner approval | `core-instructions/15-change-control.md` |
| where code goes and which docs to update | `core-instructions/19-change-recipes.md` |
| code style and quality rules | `core-instructions/14-coding-standards.md` |
| documentation, the build log, the handbook site | `core-instructions/17-documentation-system.md`, `website/README.md` |
| branches, commits, PRs, CI | `core-instructions/18-git-and-ci.md` |
| production, workers, research, time | `core-instructions/04-simulation-model.md` |
| eras, research tree, progression | `core-instructions/25-eras-and-research.md` |
| other players (market, guilds) | `core-instructions/26-open-market.md` |
| endpoints, DTOs, errors | `core-instructions/05-api-contract.md` |
| EF Core, migrations, transactions | `core-instructions/06-data-model.md` |
| security | `core-instructions/07-security-model.md` |
| tests | `core-instructions/08-testing-strategy.md` |
| anything under `client/` | `core-instructions/09-unity-client.md` |
| game numbers | `core-instructions/23-mvp-content.md`, `server/content/*.json` |
| game design questions | `core-instructions/22-game-mechanics.md` (owner's pillars are fixed) |
| scope | `core-instructions/10-milestones.md`, `core-instructions/30-out-of-scope.md` |
| all files | `core-instructions/README.md` (index), `docs/README.md` (docs map) |

## The loop (details in `16`)
1. **Start:** preconditions → branch `task/<ID>-<slug>` → build log entry (`scripts/dev/new-log-entry.sh`) with its plan and its
   **Docs impact** table (the pages the change could make wrong, from `19`) → resume point IN PROGRESS → commit and push.
2. **Work in small green steps,** code, tests and docs together. After each step, checkpoint: resume point, build log, commit, push.
3. **Finish:** Definition of Done (`11`) → docs per recipe, every Docs impact row closed → `check-docs.sh` and the handbook build →
   review → PR. The owner merges.

## Hard rules (full list in `02`, `15`, `18`)
- Only work on a task from `docs/plan/` or an **approved** CR. New ideas → `docs/changes/CR-NNNN-*.md` (Proposed), then stop.
- **Log every decision** in the build log entry as you make it. Write owner instructions into the repo the same session.
- **Docs are updated in the same PR as the code,** and the build log's Docs impact table says which pages and why (`17` §5).
  Never hand-edit `docs/reference/generated/`. Docs are plain Markdown, placeholders in backticks (`17` §7).
- **Every file and every declaration says what it is for** (OD-12): a doc comment on each type, member and local function, a header
  comment in each other file (`14` → Documentation in code). The build, `CityBuilder.Repository.Tests` and `check-docs.sh` check it.
- Ask before: new dependencies, design changes, API/DB contract breaks, edits to protected files, deleting or loosening tests.
- Never work on `main`, push to `main`, force-push, use `--no-verify`, merge your own PR, or commit secrets.
- Server-authoritative, integer-only deterministic engine, game numbers only in content JSON, no XP or city levels.
- If a step fails twice, log what you tried and escalate. Don't thrash.

## Commands
Commands marked [Mx-NN] don't exist until that task creates them. The task removes the mark.
```
bash scripts/dev/setup-hooks.sh                 # once per clone: enable .githooks
bash scripts/dev/show-task.sh M0-15             # print a task (or CR) spec
bash scripts/dev/new-log-entry.sh M0-15 "Title" "Claude Code / claude-opus-5-5"
bash scripts/dev/check-docs.sh [--base origin/main]   # docs and process checks (also in pre-commit and CI)
bash scripts/dev/docs-site.sh build             # the handbook site, as CI builds it (Node.js 20+); `start` = live preview
bash scripts/dev/test-guard-bash.sh             # after changing .claude/hooks/guard-bash.sh
dotnet build server/CityBuilder.slnx
dotnet format server/CityBuilder.slnx --verify-no-changes   # as CI; part of Done
dotnet test  server/CityBuilder.slnx --filter "Category!=Integration"  # fast suite
dotnet test  server/CityBuilder.slnx --filter "Category=Integration"   # [M0-21] needs Docker
dotnet run   --project server/src/CityBuilder.Api                      # http://localhost:8080
docker compose up -d postgres                                          # dev database; first .env from .env.example
dotnet tool restore                                                    # once per clone: the pinned dotnet-ef
dotnet ef migrations add <Name> --project server/src/CityBuilder.Infrastructure --startup-project server/src/CityBuilder.Api --output-dir Persistence/Migrations
bash scripts/dev/test-unity.sh                                         # needs UNITY_EDITOR; close the Unity Editor first
bash scripts/dev/quality.sh                                            # [M0-62] everything CI runs, locally
dotnet run --project tools/DocTools -- generate                        # [M0-61] regenerate reference docs
dotnet run --project tools/BalanceSim -- --help                        # [M0-67]
```

## Repository map
`core-instructions/` rules and design (normative) · `docs/` the system as built, the plan, the build log, ADRs, CRs ·
`website/` the handbook site over both (https://samurai-netizen.github.io/citybuild/) ·
`shared/Simulation/` engine · `server/` API and tests · `client/CityBuilder/` Unity · `tools/` DocTools, BalanceSim ·
`scripts/dev/` helpers · `.githooks/` git hooks · `.github/` CI and PR template · `.claude/` Claude Code config.

## Finishing every task
Give the owner the task report format from `core-instructions/11-workflow.md`.
