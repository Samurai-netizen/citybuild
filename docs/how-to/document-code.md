# Document code

Every file and every declaration in this repository is described, so that a person or an AI coder who opens any part of the code
understands it without asking anyone. The rules are in `core-instructions/14-coding-standards.md` → Documentation in code; this guide
shows how to write each kind and how to fix a failing check.

## What a good comment says
A comment answers what a reader can't see in the code itself: **what** the thing is for, **why** it exists or works this way, and the
**limits** that matter (units, ranges, null, errors, which spec rule). It uses the glossary's words (`31`). It never just repeats the
name.

| Weak (repeats the name) | Good (adds what the code can't say) |
|---|---|
| `/// <summary>The count.</summary>` | `/// <summary>Workers assigned to this building, 0 to the level's MaxWorkers; 0 freezes a running cycle.</summary>` |
| `/// <summary>Gets the end.</summary>` | `/// <summary>City-clock second at which the phase ends; null while idle or frozen.</summary>` |
| `/// <summary>Constructor.</summary>` | `/// <summary>Restores a building from stored values (database, tests); rejects values no game can reach.</summary>` |

## Each kind
The examples are the repository's own comments, so you can open the file and see them in place.

A type: what it is, why it exists and how it fits (`shared/Simulation/Runtime/Settlement/Reservations.cs`).
```csharp
/// <summary>
/// The reserved quantities during one advance: what <see cref="CityQueries.Reserved"/> would give, kept up to date instead of summed over
/// every building for each capped output a start checks (the worst case of <c>04</c> section 12 did that about 200 times per event time).
/// Inside an advance only two things change a reservation: a cycle that starts reserves its outputs, and a cycle that completes delivers
/// them. Mutations run between advances, and the table is made new for each one, so it never outlives the city it was taken from.
/// </summary>
internal sealed class Reservations
```
A method: what it does, its units and edge cases, what it returns, what it throws (`shared/Simulation/Runtime/IntegerMath.cs`).
```csharp
/// <summary>
/// Divides and rounds up: the smallest whole number <c>q</c> with <c>q × divisor ≥ dividend</c>.
/// Durations use it, for example the seconds a cycle takes: <c>CeilDiv(labor, workers)</c>.
/// </summary>
/// <param name="dividend">The amount to divide, for example labor in worker-seconds. It must not be negative.</param>
/// <param name="divisor">The amount to divide by, for example the number of workers. It must be greater than zero.</param>
/// <returns>The quotient rounded up. It never overflows, because it is at most <paramref name="dividend"/>.</returns>
/// <exception cref="ArgumentOutOfRangeException">
/// <paramref name="dividend"/> is negative, or <paramref name="divisor"/> is zero or negative. If both are out of range,
/// the exception names the dividend.
/// </exception>
public static long CeilDiv(long dividend, long divisor)
```
A private field, a constant, an enum member: one line is enough, as long as it says what the code doesn't (`Reservations.cs` and
`server/tests/CityBuilder.Simulation.Tests/ClientAssemblyTests.cs`).
```csharp
/// <summary>The reserved quantity per item, sorted by item id; reading and changing it allocates nothing.</summary>
private readonly ItemTotals _reserved = new ItemTotals();

/// <summary>The only precompiled DLL a client assembly may list: Newtonsoft, and only <c>CityBuilder.Client.Net</c> uses it (<c>09</c>).</summary>
private const string _newtonsoftDll = "Newtonsoft.Json.dll";
```
A primary constructor: describe each parameter on the type (`server/src/CityBuilder.Application/Cities/SettleCity.cs`).
```csharp
/// <summary>
/// Settles a player's city to now (<c>core-instructions/04-simulation-model.md</c>, sections 1 and 2): the server clock, floored to whole
/// seconds, gives the window from the last settled second, and the engine advances the city by it. Timers run for the whole window;
/// production starts cycles only during the first <c>MaxOfflineProductionSeconds</c> of it. A skeleton: M0-23 adds the closing
/// zero-length advance, the ledger rows and the transaction (M0-22).
/// </summary>
/// <param name="cities">The stored cities.</param>
/// <param name="content">The game content.</param>
/// <param name="timeProvider">The server clock, the only clock that drives progress.</param>
/// <exception cref="ArgumentNullException">A parameter is null.</exception>
public sealed class SettleCity(ICityRepository cities, IContentProvider content, TimeProvider timeProvider)
```
An override or an interface implementation that adds nothing: `/// <inheritdoc/>`. It takes the description of what it overrides or
implements; anywhere else there is nothing to take, and the check reports it (name the source with `<inheritdoc cref="…"/>` instead).

A local function: a plain comment, because XML doc comments are not allowed there (the compiler warns, CS1587). The code has none yet,
so this one is made up:
```csharp
// The first building of the list that is still idle at this second, or -1.
int NextIdle(int from) { … }
```
Inside a body: why, not what. Name the spec rule a block implements (`shared/Simulation/Runtime/Settlement/Simulator.cs`).
```csharp
// Each pass completes what is due, then starts, so the order of 04 section 6 holds at the clock too: a canonical city has nothing
// due there, but a restored one may. Every event a source completes is gone afterwards, and every new timer ends at least 1 second
// later (durations and labor are at least 1, content rule 1), so each pass moves on in time and the loop ends.
```
A test class and a test method: what the class covers and the fixture numbers it relies on; what each test proves, in one sentence
(`server/tests/CityBuilder.Simulation.Tests/EngineRules/WorkerRuleTests.cs`).
```csharp
/// <summary>
/// Workers and labor (<c>04</c> section 3): a cycle keeps its progress when its workers change, 0 workers freeze it, only a completed
/// construction staffs a building automatically, and a house provides workers once it is built. Fixture numbers: a hut provides 3 workers
/// and builds in 5 s; a lumber camp makes 1 wood from 40 labor with up to 2 workers (4 at level 2), costs 20 gold, builds in 10 s and
/// upgrades in 12 s; a workshop's level 2 makes 1 boards from 2 wood with 180 labor and up to 6 workers; the wood cap is 100.
/// </summary>
public sealed class WorkerRuleTests
{
    /// <summary>A worker change mid-cycle keeps the labor done: 180 labor with 3 workers, after 20 s 120 are left, and 4 workers finish them in 30 s.</summary>
    [Fact]
    public void Reassigning_MidCycle_KeepsTheProgress()
```
Every other file that can hold a comment starts with one: what the file is for and, for a script, how to run it. In an XML file (a
project, the solution, UXML) the comment comes above the root element or first inside it
(`server/tests/CityBuilder.Application.Tests/CityBuilder.Application.Tests.csproj`):
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Tests of the use cases with fakes, no database (core-instructions/08-testing-strategy.md, test layers). -->
```
A shell, PowerShell or JavaScript function has a comment on the line right above it (`scripts/dev/docs-site.sh`, shortened):
```bash
#!/usr/bin/env bash
# Builds or previews the handbook site (website/README.md). Needs Node.js 20 or newer (CI uses 24).
#
# Usage:
#   bash scripts/dev/docs-site.sh build     # as CI: fails on broken links and anchors, and on pages missing from the sidebar

# Installs exactly the packages of website/package-lock.json (npm ci).
install() { (cd "$site" && npm ci --no-audit --no-fund); }
```

## When a check fails
`CityBuilder.Repository.Tests` (part of the fast suite and of CI) prints one line per problem in a C# file, `path:line: kind name message`:

| Message ends with | Fix |
|---|---|
| `has no doc comment (/// <summary>…</summary>)` | Write one right above the declaration, **above** its attributes |
| `has a doc comment without a <summary>` | Add the `<summary>`; `<remarks>` or `<param>` alone don't describe it |
| `has an empty or placeholder <summary>` | Replace "TODO" or the empty text with a real description |
| `of the primary constructor of … has no <param …>` | Add `<param name="…">` to the type's comment |
| `has an <inheritdoc/> but overrides or implements nothing …` | There is no description to inherit: write a `<summary>`, or name the source with `<inheritdoc cref="…"/>` |
| `names the parameter "…" in a <param> or <paramref/>, but has no parameter of that name` | Correct the name, or remove the tag |
| `has no comment above it (// …)` | A local function: add a `//` comment above it |
| `declares no type and does not start with a comment …` | A file of top-level statements: start it with a `//` comment |

The compiler adds its own errors in the server projects and the engine: CS1591 (a public member without a comment), CS1570 (badly formed
XML), CS1572 and CS1573 (`<param>` names that don't match), CS1734 (a `<paramref>` that names no parameter), CS1574 (a `cref` that
names nothing), CS1587 (a doc comment where none is allowed, such as above a local function).

`check-docs.sh` (the pre-commit hook and CI) checks the other files and prints `✗ path: …` or `✗ path:line: …`:

| Message ends with | Fix |
|---|---|
| `doesn't start with a comment that says what the file is for …` | Start the file with one: `#` in scripts, YAML and config files, `//` in JavaScript, `/* */` in CSS and USS, `<!-- -->` above or first inside the root element of a project, solution or UXML file |
| `no comment above this function that says what it does …` | Put a comment on the line right above the function, with no blank line between |

## Related
- The rules: `core-instructions/14-coding-standards.md` → Documentation in code.
- The checker and its rule tests: `server/tests/CityBuilder.Repository.Tests/Documentation/`.
- Docs outside the code (specs, module docs, the handbook): `core-instructions/17-documentation-system.md`.
