# Coding Standards

Goal: code that the next AI coder (or a person) understands in one read, and that the compiler and analyzers
check as much as possible. When a rule here and an analyzer disagree, the analyzer config wins. Fix the doc.

## Size and shape (reviewed, not compiler-enforced)
- One public type per file; the file name equals the type name.
- Methods ≤ ~40 lines, classes ≤ ~300 lines, files ≤ ~400 lines. Split by responsibility, not by line count.
- At most 3 levels of nesting; use guard clauses and early returns.
- A PR is ≤ ~600 changed lines, excluding generated files and test fixtures. Bigger means the task should have been split.

## Clarity
- Names say what something is, in glossary words (`31`): `AssignWorkers`, not `SetWrk`. No abbreviations except ids/DTO/API.
- No magic numbers. Game numbers come from content; technical constants are named `const`s with a comment explaining why.
- Comments explain **why**, never what. No commented-out code and no dead code.
- `TODO` is only allowed in tests and tools, and only as `TODO(<task-id>): …`. Production paths have none.
- No speculative generality: no interfaces with one implementation unless they cross a layer boundary (ports) or make testing possible.

## Correctness
- `Nullable` enabled everywhere. Don't use `!` (null-forgiving) without a comment explaining why it's safe.
- Prefer immutability: readonly fields, get-only properties, immutable DTOs (server: `record`; shared code: classes with readonly fields).
- Business-rule failures are **results** (`MutationResult`, `Result<T>` with an error code), never exceptions.
  Exceptions are for programmer errors and infrastructure failures only.
- Never catch `Exception` except at the top-level handler; never swallow exceptions.
- Integer-only game logic (`04` §9). `checked` arithmetic in the engine.
- Every `async` method takes a `CancellationToken` and passes it on. No `async void` (except Unity event handlers), no `.Result`/`.Wait()`.

## Documentation in code
- XML doc comments on every public type and member in `CityBuilder.Simulation`, `.Domain` and `.Application`.
  These projects set `GenerateDocumentationFile=true`, and CS1591 (missing XML comment) is an error there.
- Doc comments state the contract: what it does, units (seconds, worker-seconds, permille), errors it returns, and invariants it keeps.
- Tests are documentation too: names read as sentences (`08`).

## Server (.NET 10, C# 14)
- File-scoped namespaces; `sealed` by default; dependency injection through constructors only. No service locator, no static mutable state.
- Minimal API endpoints are thin (`.claude/rules/server.md`). One folder per use case (see `19`).
- Options classes are validated on start. Logging uses structured templates.
- Packages are pinned centrally in `server/Directory.Packages.props` with lock files (`RestorePackagesWithLockFile`).
  The SDK is pinned in `global.json`.

## Shared engine (netstandard2.1, C# 9)
`.claude/rules/simulation.md` is binding. In short: integers only, no newer C# features, no I/O, time or randomness, and effects as data.

## Unity client (C# 9)
- MonoBehaviours stay thin: they forward input to services and render state. Logic lives in plain C# classes that are testable in EditMode.
- Use `[SerializeField] private` fields; no public mutable fields. No `Find*`/`GetComponent` in `Update`.
- No LINQ or allocations in per-frame code. Events are unsubscribed in `OnDisable`/`OnDestroy`.
- UI in UXML/USS. Element names are kebab-case and are queried once and cached.

## Tests
`08` and `.claude/rules/tests.md` are binding. New behaviour needs new tests in the same step. Engine rules are written test-first.

## Approved dependencies (anything else needs G-Dependency)
Server: ASP.NET Core and EF Core 10 (Microsoft), `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Authentication.JwtBearer`,
`Microsoft.CodeAnalysis.BannedApiAnalyzers`. Tests: `xunit`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing`,
`Testcontainers.PostgreSql`, `Microsoft.Extensions.TimeProvider.Testing`, `coverlet.collector`.
Unity: `com.unity.nuget.newtonsoft-json`, `com.unity.inputsystem`, `com.unity.test-framework`, built-in UI Toolkit.

## Formatting and analysis (enforced)
`.editorconfig` at the repo root plus `dotnet format --verify-no-changes` in CI. `TreatWarningsAsErrors` in `src/`.
`AnalysisLevel=latest-recommended` and `EnforceCodeStyleInBuild=true`. A suppression needs a justification comment and a build log line.
