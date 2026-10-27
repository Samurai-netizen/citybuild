# Getting Started

How to get from a clean machine to a running project. Every command here must work. Tasks that add commands update this page.

## Prerequisites
| Tool | Version | Notes |
|---|---|---|
| Git | any recent | On Windows: Git for Windows (provides Git Bash, which the scripts and hooks need) |
| GitHub CLI `gh` | any recent | For opening PRs from the terminal; optional but recommended |
| .NET SDK | 10.0.401 or a newer 10.0 SDK | `global.json` pins 10.0.401 (`rollForward: latestFeature`); with an older SDK, SDK commands such as `dotnet build` fail with "A compatible .NET SDK was not found" (`dotnet --list-sdks` shows what is installed) |
| Docker Desktop | current | PostgreSQL and integration tests (Testcontainers) |
| Unity Hub + Unity | 6.3 LTS; the project is on 6000.3.25f1 (`client/CityBuilder/ProjectSettings/ProjectVersion.txt`) | Android Build Support for device builds |
| Node.js | 20 or newer (CI uses 24) | Only for the handbook site (`website/`); `npm` comes with it |
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
| Build the handbook as CI does (fails on broken links, anchors, pages missing from the sidebar) | `bash scripts/dev/docs-site.sh build` (first run installs the pinned packages) | CR-0002-T1 |
| Preview the handbook while editing (live reload) | `bash scripts/dev/docs-site.sh start` → http://localhost:3000/citybuild/ (published: https://samurai-netizen.github.io/citybuild/) | CR-0002-T1 |
| Print a task spec | `bash scripts/dev/show-task.sh M0-01` | Day 0 |
| Test the Claude Code Bash guard (after changing `.claude/hooks/guard-bash.sh`; `core-instructions/18-git-and-ci.md`) | `bash scripts/dev/test-guard-bash.sh` | KIT-AUDIT-FIXES |
| Build server | `dotnet build server/CityBuilder.slnx` | M0-02 |
| Fast tests | `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"` | M0-02 |
| Integration tests (Docker running; `docs/testing/README.md`) | `dotnet test server/CityBuilder.slnx --filter "Category=Integration"` (one `postgres:18.6` container per run, removed afterwards) | M0-21 |
| Format check (as CI) | `dotnet format server/CityBuilder.slnx --verify-no-changes` | M0-02 |
| Restore exactly the locked package versions (as CI) | `dotnet restore server/CityBuilder.slnx --locked-mode` | M0-02 |
| Database (Docker running; once: `.env` from `.env.example`) | `docker compose up -d postgres`; stop it with `docker compose stop postgres` (section Database below) | M0-19 |
| Restore the pinned `dotnet-ef` tool (once per clone) | `dotnet tool restore` | M0-19 |
| Add a migration | `dotnet ef migrations add <Name> --project server/src/CityBuilder.Infrastructure --startup-project server/src/CityBuilder.Api --output-dir Persistence/Migrations` (`docs/how-to/add-migration.md`) | M0-19 |
| Run API (needs the connection string; in Development it applies pending migrations, so the database must be running) | `dotnet run --project server/src/CityBuilder.Api` → http://localhost:8080 (Development) | M0-02 |
| Is the API up? | `curl http://localhost:8080/health` prints `Healthy`; OpenAPI (Development only): http://localhost:8080/openapi/v1.json | M0-02 |
| Run the API with another content file (a draft) | `dotnet run --project server/src/CityBuilder.Api -- --Content:FilePath=/path/to/draft.json` | M0-07b |
| Open the Unity project | Unity Hub → Add → `client/CityBuilder` (editor 6000.3.25f1); details and the Bootstrap scene steps: `client/README.md` | M0-04 |
| Does the Unity project compile? (Editor closed) | `<UNITY> -batchmode -quit -projectPath client/CityBuilder -logFile -`, exit code 0 and no `error CS` (`client/README.md`) | M0-04 |
| Unity tests (Editor closed; set `UNITY_EDITOR` first, `client/README.md` → Run the tests) | `bash scripts/dev/test-unity.sh` (Windows: `pwsh scripts/dev/test-unity.ps1`) | M0-05 |
| Everything CI runs | `bash scripts/dev/quality.sh` | *(planned, M0-62)* |

## Configuration
The API reads `server/src/CityBuilder.Api/appsettings.json`, and `dotnet run` applies the launch profile `http`
(`Properties/launchSettings.json`): http://localhost:8080 and `ASPNETCORE_ENVIRONMENT=Development`. Logs are JSON lines on the console.
Any key can be overridden with an environment variable (`:` becomes `__`) or a command-line argument (`-- --Key=value` after `dotnet run`).

| Key | Default | Meaning |
|---|---|---|
| `Content:FilePath` | `content/mvp-content.json` | The content file the server loads at startup, absolute or relative to the app's base directory (the build copies `server/content/*.json` to `content/` there). Required; a blank value or invalid content stops the start with every error listed (`docs/how-to/add-building-or-item.md`) |
| `ConnectionStrings:CityBuilder` | none (required) | The PostgreSQL connection string. It holds a password, so it lives in user-secrets (Development) or the environment variable `ConnectionStrings__CityBuilder`, never in a committed file. Missing or blank stops the start |
| `Database:ApplyMigrationsAtStartup` | `false`; `true` in `appsettings.Development.json` | Apply pending migrations before the server accepts requests. Allowed only in Development: `true` in another environment stops the start (`docs/how-to/add-migration.md`) |
| `Logging:LogLevel:*`, `AllowedHosts` | as in `appsettings.json` | ASP.NET Core's own keys |

Secrets come from user-secrets or the environment, never a committed file: today the connection string; signing keys follow *(planned, M0-25)*.

## Database
PostgreSQL 18.6 runs in Docker (`docker-compose.yml` at the repo root; `core-instructions/06-data-model.md`). Once per clone:
1. `cp .env.example .env` and set your own `POSTGRES_PASSWORD` in `.env` (gitignored; compose refuses to start without the three
   `POSTGRES_*` values).
2. Give the API the same values (in one line):
   `dotnet user-secrets set ConnectionStrings:CityBuilder "Host=localhost;Port=5432;Database=citybuilder;Username=citybuilder;Password=<your password>" --project server/src/CityBuilder.Api`
   (with the user and database from your `.env`).
3. `docker compose up -d postgres`; `docker compose ps` shows it `healthy` after a few seconds.
4. `dotnet run --project server/src/CityBuilder.Api`: in Development it applies the pending migrations and logs which ones
   ("Database migrated at startup: …").

The data lives in the Docker volume `citybuild_postgres-data` and survives `docker compose down`; `docker compose down -v` deletes it.
A shell in the database: `docker compose exec postgres psql -U citybuilder -d citybuilder`. Schema changes: `docs/how-to/add-migration.md`.
From M0-61 this section links to the generated reference.

## Troubleshooting
Added whenever a setup problem is solved (the build log entry links here).
- **The API stops at startup with "ConnectionStrings:CityBuilder must hold the database connection string":** set it (Database, step 2).
- **The API stops at startup with an `NpgsqlException`:** the database isn't running (`docker compose up -d postgres`) or the
  connection string doesn't match `.env` (M0-19).
- **`docker compose up` says "required variable POSTGRES_PASSWORD is missing a value":** create `.env` from `.env.example` (M0-19).
