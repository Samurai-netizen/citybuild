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
A type: what it is, who uses it, and how it fits (`<remarks>` for how it works and its rules).
```csharp
/// <summary>The reserved quantities during one advance: what CityQueries.Reserved would give, kept up to date instead of recomputed.</summary>
/// <remarks>Only a start (reserves outputs) and a completion (delivers them) change it, so it stays exact; never stored.</remarks>
internal sealed class Reservations
```
A method: what it does, its units and edge cases, what it returns, what it throws.
```csharp
/// <summary>Divides and rounds up: the duration of a cycle is CeilDiv(labor, workers) (04 section 3).</summary>
/// <param name="dividend">At least 0.</param>
/// <param name="divisor">Greater than 0.</param>
/// <exception cref="ArgumentOutOfRangeException">A negative dividend or a divisor of 0 or less (a programmer error).</exception>
public static long CeilDiv(long dividend, long divisor)
```
A private field, a constant, an enum member: one line is enough, as long as it says what the code doesn't.
```csharp
/// <summary>Reserved quantity per item, kept sorted by item id so lookups are binary searches and nothing allocates per event.</summary>
private readonly ItemTotals _reserved = new ItemTotals();

/// <summary>The only precompiled DLL a client assembly may reference: Newtonsoft, and only CityBuilder.Client.Net uses it (09).</summary>
private const string _newtonsoftDll = "Newtonsoft.Json.dll";
```
A primary constructor: describe each parameter on the type.
```csharp
/// <summary>Settles a player's city to the server's now (04 section 1).</summary>
/// <param name="cities">Loads and saves the cities.</param>
/// <param name="content">The game content every settle runs on.</param>
/// <param name="timeProvider">The only clock; its now is floored to whole seconds.</param>
public sealed class SettleCity(ICityRepository cities, IContentProvider content, TimeProvider timeProvider)
```
An override that adds nothing: `/// <inheritdoc/>`.

A local function: a plain comment, because XML doc comments are not allowed there (the compiler warns, CS1587).
```csharp
// The first building of the list that is still idle at this second, or -1.
int NextIdle(int from) { … }
```
Inside a body: why, not what. Name the spec rule a block implements.
```csharp
// Completions come before starts at every second, the window's first included (04 section 7): a timer due at the clock frees workers first.
```
A test class and a test method: what the class covers and the fixture numbers it relies on; what each test proves, in one sentence.
```csharp
/// <summary>Workers and labor (04 section 3). Fixture: a lumber camp makes 1 wood from 40 labor with up to 2 workers.</summary>
public sealed class WorkerRuleTests
{
    /// <summary>A cycle keeps its progress when its workers change: 180 labor with 3 workers, after 20 s 4 workers finish in 30 s.</summary>
    [Fact]
    public void Reassigning_MidCycle_KeepsTheProgress()
```
Every other file that can hold a comment starts with one: what the file is for and, for a script, how to run it. In an XML file (a
project, the solution, UXML) the comment comes above the root element or first inside it:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Tests of the use cases with fakes, no database (core-instructions/08-testing-strategy.md, test layers). -->
```
A shell, PowerShell or JavaScript function has a comment on the line right above it:
```bash
#!/usr/bin/env bash
# Builds or previews the handbook site (website/README.md).
# Usage: bash scripts/dev/docs-site.sh build|start|serve|install

# Installs the exact packages of website/package-lock.json.
install() { (cd "$site" && npm ci); }
```

## When a check fails
`CityBuilder.Repository.Tests` (part of the fast suite and of CI) prints one line per problem in a C# file, `path:line: kind name message`:

| Message ends with | Fix |
|---|---|
| `has no doc comment (/// <summary>…</summary>)` | Write one right above the declaration, **above** its attributes |
| `has a doc comment without a <summary>` | Add the `<summary>`; `<remarks>` or `<param>` alone don't describe it |
| `has an empty or placeholder <summary>` | Replace "TODO" or the empty text with a real description |
| `of the primary constructor of … has no <param …>` | Add `<param name="…">` to the type's comment |
| `has no comment above it (// …)` | A local function: add a `//` comment above it |
| `declares no type and does not start with a comment …` | A file of top-level statements: start it with a `//` comment |

The compiler adds its own errors in the server projects and the engine: CS1591 (a public member without a comment), CS1570 (badly formed
XML), CS1572 and CS1573 (`<param>` names that don't match), CS1574 (a `cref` that names nothing), CS1587 (a doc comment where none is
allowed, such as above a local function).

`check-docs.sh` (the pre-commit hook and CI) checks the other files and prints `✗ path: …` or `✗ path:line: …`:

| Message ends with | Fix |
|---|---|
| `doesn't start with a comment that says what the file is for …` | Start the file with one: `#` in scripts, YAML and config files, `//` in JavaScript, `/* */` in CSS and USS, `<!-- -->` above or first inside the root element of a project, solution or UXML file |
| `no comment above this function that says what it does …` | Put a comment on the line right above the function, with no blank line between |

## Related
- The rules: `core-instructions/14-coding-standards.md` → Documentation in code.
- The checker and its rule tests: `server/tests/CityBuilder.Repository.Tests/Documentation/`.
- Docs outside the code (specs, module docs, the handbook): `core-instructions/17-documentation-system.md`.
