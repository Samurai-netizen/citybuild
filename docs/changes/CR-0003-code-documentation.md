# CR-0003 — Code documentation: every file and every declaration described, and checked

- **Status:** In progress
- **Type:** Process
- **Requested by:** owner · **Date:** 2026-10-26
- **Milestone / era:** M0 (before M0-19) / —
- **Conflicts with pillars (`22`) or the era rule (`25`):** none

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Problem / goal
The code is documented unevenly. The compiler demands XML doc comments only on the public types and members of the engine, Domain and
Application; everything else depends on the author. About a third of the roughly 1,700 declarations in the 189 C# files have a
description: most private and internal members, almost every test method, the Unity scripts and most helper code have none, and only one
file starts with a description of itself. A programmer or an AI coder opening a file must work out from the code what a part is for. The
owner wants every file and every function described, so that "any part of the code" is understandable to people and to AI coders alike,
and kept that way.

## Proposal
1. **A standard** (`core-instructions/14-coding-standards.md` → Documentation in code, with a worked guide `docs/how-to/document-code.md`):
   - every C# declaration at every access level (types, constructors, methods, properties, indexers, fields, constants, events, operators,
     enum members, delegates) has an XML doc comment with a `<summary>` that says what it is or does and why, in glossary words, with units
     and the spec rule it implements; `<param>`, `<returns>`, `<exception>`, `<remarks>` where the summary isn't enough;
   - a local function (where XML docs are not allowed) has a `//` comment above it; non-obvious steps inside a body say why;
   - every file says what it is for at its top: in C# the doc comment of the file's one type (a file without a type starts with a comment);
     scripts, JavaScript, workflows, MSBuild files, UXML and USS start with a header comment; files that can't hold comments (JSON) are
     described by the doc of the folder or module that owns them;
   - tests too: each test class says what it covers, each test method the behaviour it proves.
2. **Checks that keep it so:**
   - a new test project `server/tests/CityBuilder.Repository.Tests` parses every C# file of the repository with Microsoft's C# parser
     (`Microsoft.CodeAnalysis.CSharp`, syntax only) and fails, naming file and line, for any declaration without a doc comment, a local
     function without a comment, or a file without a description; its own rules are tested on small samples;
   - the compiler reports a missing or broken doc comment (CS1591, CS1570, CS1572, CS1573, CS1574) as an error in every server project,
     tests included (it was suppressed outside Domain and Application);
   - `check-docs.sh` checks the header comments of scripts, JavaScript, workflows, MSBuild files, UXML and USS, and a comment above every
     shell and JavaScript function.
3. **The documentation itself**, for all existing code: server, engine, tests, Unity client, scripts and the handbook site.
4. **The workflow** asks for it with every change: the recipes (`19`), the Definition of Done (`11`), the rules for AI coders.

## Acceptance criteria
- [ ] `CityBuilder.Repository.Tests` passes with no exemption except the Unity template's own sample scripts (`Assets/TutorialInfo/`, an
      open question since M0-04); each of its rules has a failing sample test.
- [ ] CS1591 and the doc comment checks are errors in every server project; build and fast suite green.
- [ ] Every script, JavaScript, workflow, MSBuild, UXML and USS file starts with a header comment, every shell and JavaScript function has
      a comment above it, and `check-docs.sh` checks both.
- [ ] The change is comments only: a script shows that every changed line of code files is a comment or blank, and the server assemblies
      compile byte-identical without debug information.
- [ ] `14`, the how-to, `19`, `11`, `08`, `17`, `03`, the decision log, the rules for AI coders and the module docs describe the standard and the checks.

## Out of scope
- A generated code reference in the handbook: M0-61 (DocTools) builds it from these comments (the owner's choice, 2026-10-26).
- Any change to what the code does. Renames and refactors, even of unclear code, become follow-ups.
- Comments in JSON files (content, manifests, lock files), which JSON doesn't allow, and Unity's generated files (`.asset`, `.unity`, `.meta`).

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | comments only; no rule changes | `shared/Simulation/Runtime/**` |
| Content (`23`, JSON) | none | — |
| API (`05`) | none | — |
| Data / migration (`06`) | none | — |
| Security (`07`) | none | — |
| Client (`09`) | comments only | `client/CityBuilder/Assets/_Project/**` |
| Build | CS1591 and the doc comment checks become errors in every server project (decision 020 superseded in part) | `server/Directory.Build.props` |
| Tests (`08`) | a new test project for repository-wide checks; comments on every existing test | `server/tests/**`, `server/CityBuilder.slnx`, `server/Directory.Packages.props` |
| Docs (recipes in `19`) | the standard, a how-to, the workflow steps | `14`, `docs/how-to/document-code.md`, `19`, `11`, `08`, `17`, `03`, `12`, `15`, `AGENTS.md`, `.claude/rules/*`, module docs |

## Risks and alternatives
- **Comments that restate the code** add reading without adding understanding. Mitigation: the standard asks for what and why, and the
  how-to shows good and bad examples; the check can only see that a comment exists, so review judges its quality.
- **Comments drift from the code.** Mitigation: they are part of the change (recipes, Definition of Done, Docs impact), and the compiler checks
  every `cref`, `param` and XML element.
- Alternative: StyleCop.Analyzers (rule SA1600). Rejected: only beta versions for years, and it can't see the Unity scripts, which only Unity compiles.
- Alternative: a text-pattern scanner without a new package. Rejected by the owner's choice: it can miss or misreport declarations.
- Alternative: reflection over the compiled assemblies against their XML doc files. Rejected: it can't see local functions or the Unity scripts.

## Tasks (filled in after approval)
| ID | Title | Model | Status |
|---|---|---|---|
| CR-0003-T1 | The standard, the checks, and the documentation of all existing code (one PR) | O | In review |

## Owner approval
- Approved on: 2026-10-26 — the owner's request in chat: "I want additional documentation for the code itself. Each file, each function,
  etc. have to be documented with description. Human programmer should understand any part of the code. Same for AI coder."
- 2026-10-26, answers to three questions: the checker: "Microsoft parser (Recommended)" (`Microsoft.CodeAnalysis.CSharp` 5.9.0 in a new
  test-only project, G-Dependency approved); delivery: "One PR (Recommended)"; a code reference in the handbook: "Later, in M0-61 (Recommended)".
