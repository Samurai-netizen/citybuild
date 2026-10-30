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
| Log in (Development; the API running) | `curl -X POST http://localhost:8080/api/v1/auth/dev-login -H 'Content-Type: application/json' -d '{"displayName":"dev1"}'` → `{"accessToken":"…","expiresAtUtc":"…"}`; the first login of a name creates the player and the starter city. Send the token as `Authorization: Bearer <accessToken>` | M0-25 |
| Read the game content and your city (Development; after the login) | `curl http://localhost:8080/api/v1/content -H "Authorization: Bearer $TOKEN"` (the same for every player) and `curl http://localhost:8080/api/v1/city -H "Authorization: Bearer $TOKEN"` (settles your city to now, then shows it). Without a valid token both answer 401 `AUTHENTICATION_REQUIRED` | M0-28 |
| Place a building (Development; after the login) | `curl http://localhost:8080/api/v1/commands/place-building -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{\"requestId\":\"$(uuidgen)\",\"buildingTypeId\":\"house\",\"x\":0,\"y\":0}"`: answers with the snapshot; the same `requestId` again answers the same; a refusal is Problem Details with a `code` (`05` → Commands) | M0-29 |
| Upgrade a building (Development; after the login) | `curl http://localhost:8080/api/v1/commands/upgrade-building -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{\"requestId\":\"$(uuidgen)\",\"buildingId\":1}"`: the `id` is the building's in the snapshot; answers with the snapshot (the building `Upgrading`), or Problem Details such as `BUILDING_BUSY` while it is still being built | M0-31 |
| Assign workers (Development; after the login) | `curl http://localhost:8080/api/v1/commands/assign-workers -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{\"requestId\":\"$(uuidgen)\",\"buildingId\":2,\"workers\":1}"`: an absolute count, 0 pauses; answers with the snapshot (the building `Producing` with its new `phaseEndsAt`), or Problem Details such as `INSUFFICIENT_WORKERS` or `WORKER_LIMIT` | M0-32 |
| Start research (Development; after the login) | `curl http://localhost:8080/api/v1/commands/start-research -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{\"requestId\":\"$(uuidgen)\",\"researchId\":\"trade_charter\"}"`: the ids are in `GET /content` → `research`; answers with the snapshot (`research.activeId` and `endsAt`, the cost paid), or Problem Details such as `RESEARCH_LOCKED` or `RESEARCH_IN_PROGRESS`; it completes on its own in a later request at `endsAt` | M0-33 |
| Run the API with another content file (a draft) | `dotnet run --project server/src/CityBuilder.Api -- --Content:FilePath=/path/to/draft.json` | M0-07b |
| Open the Unity project | Unity Hub → Add → `client/CityBuilder` (editor 6000.3.25f1); details and the Bootstrap scene steps: `client/README.md` | M0-04 |
| Does the Unity project compile? (Editor closed) | `<UNITY> -batchmode -quit -projectPath client/CityBuilder -logFile -`, exit code 0 and no `error CS` (`client/README.md`) | M0-04 |
| Unity tests (Editor closed; set `UNITY_EDITOR` first, `client/README.md` → Run the tests) | `bash scripts/dev/test-unity.sh` (Windows: `pwsh scripts/dev/test-unity.ps1`) | M0-05 |
| Everything CI runs | `bash scripts/dev/quality.sh` | *(planned, M0-62)* |

## Configuration
The API reads `server/src/CityBuilder.Api/appsettings.json`, and `dotnet run` applies the launch profile `http`
(`Properties/launchSettings.json`): http://localhost:8080 and `ASPNETCORE_ENVIRONMENT=Development`. Logs are JSON lines on the console; what each line means and how to find a request's lines:
`docs/api/logs-and-correlation.md`.
Any key can be overridden with an environment variable (`:` becomes `__`) or a command-line argument (`-- --Key=value` after `dotnet run`).

| Key | Default | Meaning |
|---|---|---|
| `Content:FilePath` | `content/mvp-content.json` | The content file the server loads at startup, absolute or relative to the app's base directory (the build copies `server/content/*.json` to `content/` there). Required; a blank value or invalid content stops the start with every error listed (`docs/how-to/add-building-or-item.md`) |
| `ConnectionStrings:CityBuilder` | none (required) | The PostgreSQL connection string. It holds a password, so it lives in user-secrets (Development) or the environment variable `ConnectionStrings__CityBuilder`, never in a committed file. Missing or blank stops the start |
| `Database:ApplyMigrationsAtStartup` | `false`; `true` in `appsettings.Development.json` | Apply pending migrations before the server accepts requests. Allowed only in Development: `true` in another environment stops the start (`docs/how-to/add-migration.md`) |
| `Database:LockTimeoutMilliseconds` | `3000` | How long a request waits for its city's row lock before it fails with `CONCURRENCY_CONFLICT` and the client retries (`core-instructions/06-data-model.md`). Between 1 and 60000, or the start stops |
| `Auth:SigningKey` | none (required) | The secret the access tokens are signed and checked with (HMAC-SHA256), at least 32 bytes. Anyone who has it can act as any player, so it lives in user-secrets (Development) or the environment variable `Auth__SigningKey`, never in a committed file. Missing or shorter stops the start; changing it ends every token (`core-instructions/07-security-model.md`) |
| `Auth:TokenLifetimeMinutes` | `720` | How long an access token is valid (12 h). Between 1 and 1440, or the start stops |
| `Auth:DevLoginEnabled` | `false`; `true` in `appsettings.Development.json` | Map `POST /api/v1/auth/dev-login`, which logs anyone in by name alone. Allowed only in Development: `true` in another environment stops the start |
| `RequestLimits:MaxBodyBytes` | `4096` | The largest request body the server reads, in bytes; a larger one is 400 `INVALID_ARGUMENT` before anything runs (`core-instructions/05-api-contract.md` → Request limits). Between 1024 and 1048576, or the start stops |
| `RateLimits:Commands:Burst:Limit`, `:WindowSeconds` | `10`, `1` | The most commands one player may send in a window of that many seconds (all command endpoints together). A command must pass this and the next limit; a refused request is 429 `RATE_LIMITED` with `Retry-After` (`core-instructions/05-api-contract.md` → Rate limits). A limit is 1 to 1000000, a window 1 to 3600 seconds, or the start stops |
| `RateLimits:Commands:Sustained:Limit`, `:WindowSeconds` | `120`, `60` | The long window of the commands, per player |
| `RateLimits:City:Limit`, `:WindowSeconds` | `30`, `60` | `GET /city` per player |
| `RateLimits:Content:Limit`, `:WindowSeconds` | `30`, `60` | `GET /content` per player, a budget apart from the city's |
| `RateLimits:DevLogin:Limit`, `:WindowSeconds` | `10`, `60` | The dev login per remote address |
| `Simulation:DevTimeScale` | `1`; `1` in `appsettings.Development.json` | Development only: how many seconds of server time pass per real second (60 = one hour per minute), for playing a long scenario by hand; `GET /content` reports it as `rules.timeScale` and the start logs a warning. 1 to 1000; anything but 1 outside Development stops the start (`core-instructions/04-simulation-model.md` §1). The clock is the same after a restart with the same scale, but **reset the dev database after changing the scale** (`docker compose down -v`, then start again): a higher scale jumps every city years ahead, a lower one leaves them all behind the clock, frozen. Access tokens age at the same speed (12 hours last 12 minutes at 60), so the client logs in more often |
| `Logging:LogLevel:*`, `AllowedHosts` | as in `appsettings.json` | ASP.NET Core's own keys |

Secrets come from user-secrets or the environment, never a committed file: the connection string and the signing key.

## Database
PostgreSQL 18.6 runs in Docker (`docker-compose.yml` at the repo root; `core-instructions/06-data-model.md`). Once per clone:
1. `cp .env.example .env` and set your own `POSTGRES_PASSWORD` in `.env` (gitignored; compose refuses to start without the three
   `POSTGRES_*` values).
2. Give the API the same values (in one line):
   `dotnet user-secrets set ConnectionStrings:CityBuilder "Host=localhost;Port=5432;Database=citybuilder;Username=citybuilder;Password=<your password>" --project server/src/CityBuilder.Api`
   (with the user and database from your `.env`), and a signing key of your own for the access tokens:
   `dotnet user-secrets set Auth:SigningKey "$(openssl rand -base64 48)" --project server/src/CityBuilder.Api`.
3. `docker compose up -d postgres`; `docker compose ps` shows it `healthy` after a few seconds.
4. `dotnet run --project server/src/CityBuilder.Api`: in Development it applies the pending migrations and logs which ones
   ("Database migrated at startup: …").

The data lives in the Docker volume `citybuild_postgres-data` and survives `docker compose down`; `docker compose down -v` deletes it.
A shell in the database: `docker compose exec postgres psql -U citybuilder -d citybuilder`. Schema changes: `docs/how-to/add-migration.md`.

**The test player.** Hand runs against the dev database (a task's check of the real server, a curl try) log in as one player, dev name
`devtest`, instead of a new player per task (owner directive OD-14 in `core-instructions/15-change-control.md`). Its city carries over
from run to run; a run that needs the starter city deletes the player first, which removes its city, rows, ledger and receipts:
```
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' <<'SQL'
begin;
delete from ledger_entries where city_id in (select c.id from cities c join players p on p.id = c.player_id where p.dev_name = 'devtest');
delete from command_receipts where player_id in (select id from players where dev_name = 'devtest');
delete from cities where player_id in (select id from players where dev_name = 'devtest');
delete from players where dev_name = 'devtest';
commit;
SQL
```
The next dev login as `devtest` creates the player and a starter city again.
From M0-61 this section links to the generated reference.

## Troubleshooting
Added whenever a setup problem is solved (the build log entry links here).
- **The API stops at startup with "ConnectionStrings:CityBuilder must hold the database connection string":** set it (Database, step 2).
- **The API stops at startup with an `NpgsqlException`:** the database isn't running (`docker compose up -d postgres`) or the
  connection string doesn't match `.env` (M0-19).
- **`docker compose up` says "required variable POSTGRES_PASSWORD is missing a value":** create `.env` from `.env.example` (M0-19).
