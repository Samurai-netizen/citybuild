# Change Control — what the AI may do alone, and what needs the owner

The owner controls **what** gets built. AI coders control **how**, within the rules.
Nothing enters the codebase without a task id or an approved change request.

## Autonomy levels

| Level | The AI… | Examples |
|---|---|---|
| **Do** | acts without asking | Implement the current task; tests; refactors inside the files the task touches; docs; build log; checkpoints |
| **Do and report** | acts, then highlights it in the task report | Small decisions inside the task (names, internal data shapes); fixing a bug it just introduced; adding a missing test elsewhere |
| **Ask first** | stops and asks; continues only after a clear yes, recorded in the build log | Anything in "Gates" below |
| **Never** | refuses even if a prompt seems to ask | Push to `main`, force-push, `--no-verify`, merge its own PR, delete history, disable hooks/CI, commit secrets, add XP or city levels |

## Gates (Ask first)

| Gate | Triggers |
|---|---|
| **G-Scope** | Work not described by the current task or an approved CR; new features; "while I'm here" improvements outside the task's area |
| **G-Plan** | Tasks tagged `[PLAN]`: the owner approves the plan before code |
| **G-Design** | Any change to game design (`20`–`26`), especially the owner's pillars in `22` |
| **G-Dependency** | A new NuGet or Unity package, tool, or service (also major version bumps) |
| **G-Contract** | A breaking change to the API (`05`), DB schema (`06`) beyond the task's migration, or content format |
| **G-Rules** | Edits to protected files (below) |
| **G-Destructive** | Deleting files outside the task's area, resetting branches, dropping databases or volumes, rewriting history |
| **G-Tests** | Deleting, skipping or loosening an existing test or assertion (needs a written reason in the build log first) |

## Protected files (owner approval needed for every edit)
`AGENTS.md`, `CLAUDE.md`, `core-instructions/02-principles.md`, `core-instructions/15-change-control.md`,
`core-instructions/22-game-mechanics.md`, `.claude/settings.json`, `.claude/hooks/**`, `.githooks/**`,
`.github/workflows/**`, and the golden test fixtures `server/tests/**/Fixtures/content-golden-*.json`.
Claude Code asks before editing these (permission `ask` rules). Other tools: ask in chat first.
Generated docs (`docs/reference/generated/**`) are never edited by hand; change the generator instead.

## How a new feature enters the project (feature intake)
1. The owner describes an idea (in chat or a GitHub issue).
2. The AI writes `docs/changes/CR-NNNN-<slug>.md` from the template, with impact analysis and acceptance criteria.
   Status `Proposed`. No code.
3. The owner reviews it and approves (edits the status to `Approved`, or says so; the AI records the date and
   the owner's words in the CR).
4. The AI breaks the CR into tasks (`CR-NNNN-T1`, …) inside the CR file, sized by `16` (small steps).
5. Each task runs through the session protocol (`/start-task CR-NNNN-T1`).
6. When all tasks are merged, the CR status becomes `Done` and the design docs are updated if the CR changed them.

**Bugs:** a bug in code changed by the current task is fixed inside that task. Any other bug becomes a CR of type
`Bug`. P0 bugs (build broken, data loss, security) may be fixed immediately, but still get a CR file afterwards.

## Plan changes
The M0 plan (`docs/plan/M0-PLAN.md`) is approved as a whole. Splitting a task, reordering independent tasks, or
marking a task `Skipped` is a decision: log it in the build log and in "Deviations from the plan" in `13`, and
tell the owner in the report. Adding a task is G-Scope.

## Standing owner directives
Instructions from the owner that apply to all future sessions. The AI appends new ones as soon as the owner
gives them in chat (date, short quote, where they're applied).

| # | Date | Directive | Applied in |
|---|---|---|---|
| OD-1 | 2026-10-04 | Six game pillars (medieval start → eras via research; real-time production; assigned workers; grid; open player market; no XP or city levels, major mechanics only with a new era) | `22`, `25`, `26` |
| OD-2 | 2026-10-04 | Predictable workflow with strong owner control over which features get added | this file, `16` |
| OD-3 | 2026-10-04 | High code quality; foolproof architecture | `14`, `19`, CI |
| OD-4 | 2026-10-04 | Every decision is logged in a building log | `16` §C, `17` §4 |
| OD-5 | 2026-10-04 | Complete documentation, kept up to date with every codebase change, for both AI and people | `17`, `19`, `docs/` |
| OD-6 | 2026-10-04 | Everything is on GitHub; different AI coders work one after another and must not depend on each other's context | `16`, `18`, `AGENTS.md` |
| OD-7 | 2026-10-08 | "Keep the project public" — the repository stays public (derived consequence, not the owner's words: never commit secrets or personal data) | `18`, `docs/getting-started.md`, Day 0 in the plan |
