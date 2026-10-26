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
Goal: a programmer or an AI coder who opens any file understands what it is for and what every part of it does and why, without asking
anyone (owner directive OD-12). Worked examples for each kind: `docs/how-to/document-code.md`.
- **Every declaration has an XML doc comment**, at every access level and in every project, tests and Unity scripts included: types,
  constructors, methods, properties, indexers, fields, constants, events, operators, enum members, delegates. Its `<summary>` says what it
  is or does and why it exists, in glossary words (`31`), with units (seconds, worker-seconds, permille) and the rule it implements
  (`<c>04</c> section 6`). Add `<param>`, `<returns>`, `<exception>` and `<remarks>` where the summary doesn't say it yet: a unit, a range,
  a null case, an error, an invariant, how an algorithm works. `<inheritdoc/>` marks an override or implementation that adds nothing
  (anywhere else it inherits nothing, and the check rejects it).
  Each primary constructor parameter gets a `<param>` in the type's comment.
- **A local function** has a `//` comment above it (XML doc comments aren't allowed there). **Inside a body**, comments say why: a step that
  isn't obvious, the spec rule a block implements, a trap the next change could fall into. Never what the next line plainly does.
- **Every file says what it is for, at its top.** In C# that is the doc comment of the file's type (one type per file, named after it); a
  file without a type (top-level statements) starts with a comment. Every other file that can hold a comment starts with one: scripts and
  hooks, YAML, MSBuild files and the solution (above the root element or first inside it), JavaScript, CSS and USS, UXML, and the git and
  editor config files; every shell, PowerShell or JavaScript function has a comment on the line right above it. Files that can't hold
  comments (JSON) are described by the doc of the folder or module that owns them; files that tools generate (Unity's `.asset`, `.unity`,
  `.meta` and theme `.tss` files, lock files) are not.
- **Tests:** the class says what it covers (the spec section, the fixture numbers it relies on); each test method says in one sentence the
  behaviour it proves; helpers and fixtures are documented like production code. Test names still read as sentences (`08`).
- **Comments are code:** a change that makes a comment wrong fixes it in the same commit.
- **Checks:** the compiler reports a missing doc comment on a public member (CS1591) and a broken one (bad XML, an unknown `cref` or
  `param`) as an error in the engine and in every server project, tests included (`server/Directory.Build.props`).
  `CityBuilder.Repository.Tests` parses every C# file, the Unity scripts included, and fails for an undocumented declaration at any access
  level, an `<inheritdoc/>` with nothing to inherit, a `<param>` or `<paramref/>` that names no parameter, a local function without a
  comment, or a file without a description. `check-docs.sh` checks the header comments of the other files and the comments above shell,
  PowerShell and JavaScript functions. Whether a comment is good is for review.

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
Server: ASP.NET Core and EF Core 10 (Microsoft, incl. `Microsoft.AspNetCore.OpenApi`, `Microsoft.EntityFrameworkCore.Design` and the `dotnet-ef` tool,
pinned in `dotnet-tools.json`), `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Authentication.JwtBearer`,
`Microsoft.CodeAnalysis.BannedApiAnalyzers`. Tests: `xunit.v3.mtp-off` (xUnit v3 for VSTest; replaces `xunit`, decision 019 in `12`),
`xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing`,
`Testcontainers.PostgreSql`, `Microsoft.Extensions.TimeProvider.Testing`, `coverlet.collector`, `Microsoft.CodeAnalysis.CSharp` (the parser
of the documentation checks in `CityBuilder.Repository.Tests`, CR-0003).
Unity: `com.unity.nuget.newtonsoft-json`, `com.unity.inputsystem`, `com.unity.test-framework`, built-in UI Toolkit and the built-in module
`com.unity.modules.unitywebrequest`. A package that arrives without anyone asking for it (a new project, the Hub, the Editor, Package Manager) is not approved by that (decision 025).
Handbook site (`website/`, Node.js 20 or newer): `@docusaurus/core`, `@docusaurus/preset-classic`, `@docusaurus/theme-mermaid`, `prism-react-renderer`,
`react`, `react-dom`, `@easyops-cn/docusaurus-search-local` (CR-0002), each pinned exactly in `website/package.json` and locked by `package-lock.json`.

## Formatting and analysis (enforced)
`.editorconfig` at the repo root plus `dotnet format --verify-no-changes` in CI. `TreatWarningsAsErrors` in `src/`.
`AnalysisLevel=latest-recommended` and `EnforceCodeStyleInBuild=true`. A suppression needs a justification comment and a build log line.
