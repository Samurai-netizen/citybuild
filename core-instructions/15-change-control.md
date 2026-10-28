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
`scripts/dev/**`, `.github/workflows/**`, and the golden test fixtures `server/tests/**/Fixtures/content-golden-*.json`.
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
| OD-8 | 2026-10-12 | "We will decide on exact license later … we can use no license file for a little bit more time" — until the owner decides there is no `LICENSE` file and the README keeps its general line, "Proprietary, all rights reserved" (derived consequence: don't add a `LICENSE` file or pick a license on your own) | `README.md` (License) |
| OD-9 | 2026-10-14 | "I want you to ask me each time when you want to use agents. You should specify why you need to use agents, how much of them and why. Make a couple of proposals and explain each option: no agents, small use of agents, big use of agents and so on. I will approve the plan each time". The model of every agent is named in the proposal. Rules of use below | `02` rule 27, `CLAUDE.md`, `11`, `16` §E, `17`, `18`, the skills, `.claude/agents/`, `.claude/settings.json`, the PR template |
| OD-10 | 2026-10-19 | "Yes, add such check for everything era-related" (the answer to: should placing a building also require that its type's era is not later than the city's era?). Derived rule: a city can't use anything of a later era; every era-related feature checks it | `04` §8 and §14 rule 8, `05`, `25`, `CityQueries.IsUnlocked`, `EraRules` |
| OD-11 | 2026-10-20 | "Implement a documentation system for citybuild game. Run through all previous work, make the documentation up to date, and make changes to the AI workflow, so it continues to keep the documentation up to date with every Task. Let's use docusaurus." The owner then chose to publish it on GitHub Pages and to add local search. Derived rule: every task keeps the handbook current: it plans and closes a Docs impact table, the handbook must build, and the docs checks pass | `17` §5 and §7, `16`, `19`, `11`, `18`, `website/`, `check-docs.sh`, CI, CR-0002 |
| OD-12 | 2026-10-26 | "I want additional documentation for the code itself. Each file, each function, etc. have to be documented with description. Human programmer should understand any part of the code. Same for AI coder." Derived rule: every file says what it is for and every declaration and function has a comment, in the code; every change keeps them true, and the checks enforce it (decision 037) | `14` → Documentation in code, `docs/how-to/document-code.md`, `11`, `19`, `08`, `17`, `AGENTS.md`, `.claude/rules/`, `CityBuilder.Repository.Tests`, `check-docs.sh`, CR-0003 |
| OD-13 | 2026-10-28 | "For decision 2 and same situations, you must make a fool-proof system, so no cheaters can do harm" (M0-31 decision 2 let a building id below 1 reach the city's lock and answered `BUILDING_NOT_FOUND`). Derived rule: a value from the client that no city and no content could ever make valid (an id below 1, a negative coordinate or count, text that can't be a content id) is refused as `INVALID_ARGUMENT` at the boundary (the binding or the command's `ValidateShape`), before the city is locked or settled; every check that depends on the city or the content stays the engine's. Each such refusal gets a test that the lock was never taken and nothing was stored. Reports still never claim "cheat-proof" (`02` rule 26): they name what is guarded and what is still open | `05` → Commands, `07`, `docs/how-to/add-command.md`, `UpgradeBuildingCommand` and `PlaceBuildingCommand` (M0-31); M0-32 (negative workers) |
| OD-14 | 2026-10-28 | "You can make a special user for all the testing purposes, if you need to. m031check you can delete" (about the check players that hand runs left in the dev database). Derived rule: hand runs against the dev database use one test player, dev name `devtest`, never a new player per task; a run that needs a fresh city deletes `devtest` first with the script in `docs/getting-started.md` → Database. Other rows of the dev database are deleted only with the owner's OK (G-Destructive) | `docs/getting-started.md` → Database; M0-31 build log (`m031check` deleted) |

### OD-9 in practice
- **"Agents"** are subagents (the `Agent` tool), Workflows and any other multi-agent run. A session setting that asks for them (for example
  "ultracode") is never an approval.
- **Default: none.** The coder does the work, the checks and the review itself: a self-review of the branch diff against the task spec, the
  Definition of Done (`11`) and the checks of `16` §E. The owner can also start a fresh session as the independent reviewer.
- **Before any agent starts, ask the owner and wait.** Ask in chat (Claude Code: `AskUserQuestion`, one question, the options as the answers). Silence is
  not approval. The proposal contains:
  1. **Why** agents would help here: what the coder cannot do or check well in one context, or what they would speed up, and what the coder does
     without them.
  2. **Options**, at least **A. No agents** (what the coder does instead, and what that risks missing), **B. Small** (for example one read-only
     agent) and **C. Big** (several agents or a Workflow, with how their findings are checked); more options where they make sense.
  3. For every agent in an option: its **job**, its **model**, **how many** and **why that many**, what it may read or change (read-only unless the
     proposal says otherwise), a rough size, and what it will not catch.
  4. A **recommendation** and the reason for it.
- **Approval covers exactly what the owner picked:** those agents, that number, that model, that purpose. Another agent, another purpose or a retry
  with more agents is a new proposal.
- **Record before starting:** the proposal and the owner's answer go into the build log (Owner input, and a decision row). The task report names
  the agents used and what they found. The coder stays responsible for their output and checks it.
- **Enforcement:** `ask` rules for the `Agent` and `Workflow` tools in `.claude/settings.json` show a permission prompt at every start. The prompt
  backs the rule up; it is not the proposal and does not replace it.
- **Scope:** this concerns agents only, not the model of the main session (`11`, `CLAUDE.md`).
