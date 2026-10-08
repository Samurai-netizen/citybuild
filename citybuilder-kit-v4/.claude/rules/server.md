---
paths:
  - "server/**/*.cs"
  - "server/**/*.csproj"
  - "server/**/*.json"
---

# Server rules (.NET 10)

- Minimal APIs grouped per feature (`MapGroup("/api/v1/commands")`). Endpoints stay thin:
  bind DTO → call one Application use case → map result to DTO or ProblemDetails.
- Every command endpoint goes through the shared command pipeline (auth → validate → lock →
  receipt check → settle → mutate → zero-length advance → persist → receipt). No hand-rolled
  write paths. If you need one, stop and explain why.
- A transaction row-locks **only the acting player's city**. Never load-for-update or write another player's
  city; cross-player effects (market, guilds; M2) go to a mailbox. Lock order: own city → shared aggregate → rows by id.
- Time: inject `TimeProvider`; never call `DateTime.UtcNow`/`DateTimeOffset.UtcNow` in Application,
  Domain or Infrastructure code. Tests use `FakeTimeProvider`.
- Errors: return `Results.Problem` with an extension `code` from the catalogue in
  `core-instructions/05-api-contract.md`. Never expose exception messages outside Development.
- Configuration via options classes validated at startup (`ValidateOnStart`). Signing keys come from
  user-secrets / environment, never `appsettings*.json` in the repo.
- Content JSON is loaded once, validated (fail fast), and exposed through `IContentProvider`.
- EF Core: mappings in `Infrastructure/Persistence/Configurations`, snake_case, explicit indexes and
  check constraints, migrations committed. Persistence models ≠ Simulation types; map explicitly.
- Async all the way; pass `CancellationToken` from the endpoint down to EF calls.
- `Directory.Build.props`: `Nullable=enable`, `TreatWarningsAsErrors=true` for `src/`,
  `AnalysisLevel=latest-recommended`.
- Log with structured templates (`{PlayerId}`, `{RequestId}`, `{Command}`), never string concatenation,
  never tokens or secrets.
