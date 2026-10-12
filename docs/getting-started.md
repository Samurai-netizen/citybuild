# Getting Started

How to get from a clean machine to a running project. Every command here must work. Tasks that add commands update this page.

## Prerequisites
| Tool | Version | Notes |
|---|---|---|
| Git | any recent | On Windows: Git for Windows (provides Git Bash, which the scripts and hooks need) |
| GitHub CLI `gh` | any recent | For opening PRs from the terminal; optional but recommended |
| .NET SDK | 10.0.x | Pinned by `global.json` (created in M0-02) |
| Docker Desktop | current | PostgreSQL and integration tests (Testcontainers) |
| Unity Hub + Unity | 6.3 LTS | Android Build Support for device builds |
| Claude Code | current | Or any AI coding tool that reads `AGENTS.md` |

## First-time setup (owner, Day 0)
1. Create a GitHub repository (public, owner directive OD-7) and clone it.
2. Copy the instruction kit into the clone's root (`AGENTS.md` must sit at the top level, next to `.github/` and `.claude/`).
3. `bash scripts/dev/setup-hooks.sh`: enables the git hooks.
4. `bash scripts/dev/check-docs.sh` should print `check-docs: OK`.
5. First commit on `main` (owner-only override): `ALLOW_MAIN_COMMIT=1 git commit -m "chore(kit): add instruction kit"`, then `git push`.
6. GitHub → Settings: the `main` ruleset, squash-only merges and security settings (`core-instructions/18-git-and-ci.md`).
   After this step, `main` changes only through PRs, including the owner's own `chore(kit)` changes.
7. Start Claude Code in the folder (`claude`) and run `/context` to check that `CLAUDE.md` loaded `AGENTS.md`,
   `02-principles.md` and `13-progress.md`. Accept the workspace trust prompt, so the project's permission rules apply.

## Run (filled in as tasks make commands real)
| What | Command | Since |
|---|---|---|
| Docs and process checks | `bash scripts/dev/check-docs.sh` | Day 0 |
| Print a task spec | `bash scripts/dev/show-task.sh M0-01` | Day 0 |
| Build server | `dotnet build server/CityBuilder.slnx` | *(planned, M0-02)* |
| Fast tests | `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"` | *(planned, M0-02)* |
| Database | `docker compose up -d postgres` | *(planned, M0-19)* |
| Run API | `dotnet run --project server/src/CityBuilder.Api` | *(planned, M0-02)* |
| Unity tests | `bash scripts/dev/test-unity.sh` | *(planned, M0-05)* |
| Everything CI runs | `bash scripts/dev/quality.sh` | *(planned, M0-62)* |

## Configuration
*(planned)* Every configuration key, with its default and where secrets come from (user-secrets / environment).
From M0-61 this section links to the generated reference.

## Troubleshooting
*(planned)* Added whenever a setup problem is solved (the build log entry links here).
