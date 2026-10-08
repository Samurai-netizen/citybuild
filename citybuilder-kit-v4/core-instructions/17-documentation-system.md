# Documentation System

Rule: **a change is not done until the documentation describes it.** Docs change in the same PR as the code.
Every fact has exactly one home; everything else links to it.

## 1. Three kinds of documentation

| Kind | Where | Describes | Written for |
|---|---|---|---|
| **Normative** (specs and rules) | `core-instructions/`, `AGENTS.md`, `CLAUDE.md` | What must be true: rules, design, contracts | AI coders first, people too |
| **Descriptive** (the system as built) | `docs/` | What exists, how to run it, how it works, how to extend it, what happened | People first, AI too |
| **In code** | XML doc comments, tests | What a public type or member does; executable examples | Both |

When the code changes a normative fact (an engine rule, an API field), update the spec in the same commit.
Specs never describe the future as if it were built. Unbuilt parts are marked *(planned, M2)*.

## 2. Where each fact lives (single source of truth)

| Topic | Home | Notes |
|---|---|---|
| Game pillars and mechanics | `core-instructions/22` (+ `21`, `25`, `26`) | Pillars change only with the owner |
| Exact content numbers | `server/content/*.json` → generated `docs/reference/generated/content.md` | `23` holds the M0 design copy and golden scenario |
| Engine rules | `core-instructions/04` | Tests are the executable form |
| API contract | `core-instructions/05` → generated endpoint/error reference | Contract tests keep them equal |
| DB schema | `core-instructions/06` + migrations → generated schema doc | |
| Architecture (intended) | `core-instructions/03` | |
| Architecture (as built) | `docs/architecture/overview.md` + `docs/modules/<Module>.md` | One module doc per project or assembly (enforced) |
| How to run, build, test | `docs/getting-started.md` | Commands summary in `AGENTS.md` |
| How to extend (recipes) | `core-instructions/19` (checklists) + `docs/how-to/*.md` (worked guides) | |
| Decisions | build log (all) → `12-decisions.md` (project-wide) → `docs/adr/` (architectural) | §4 |
| What happened, and why | `docs/build-log/` | Append-only |
| Current state and resume point | `core-instructions/13-progress.md` | Short; overwritten at each checkpoint |
| Task list and status | `docs/plan/M0-PLAN.md` (board) and `docs/changes/CR-*.md` | |
| User-visible changes | `CHANGELOG.md` | One line per merged task |

## 3. Generated reference docs
From M0-61, `tools/DocTools` generates `docs/reference/generated/*.md` from the code and content: items, buildings,
research, error codes, endpoints (OpenAPI), configuration keys, DB schema, and the project dependency map.
- Never edit generated files by hand (Claude Code denies it).
- CI regenerates them and fails if the committed files differ ("docs out of date").
- Until M0-61, the tables in `05`, `06` and `23` are the reference and are updated by hand.

## 4. Build log and decisions
- **One entry per task**, at `docs/build-log/<YYYY>/<YYYY-MM-DD>-<NN>-<TASK>.md`, created with `scripts/dev/new-log-entry.sh`
  from `docs/build-log/TEMPLATE.md`. Each session working on the task appends a "Session N" section.
- **Every decision goes into the entry's Decisions table** when it's made: what was chosen, the alternatives, why,
  and its scope. Small, local decisions count too. That's the point.
- **Escalation:**
  - project-wide decisions (affecting other tasks, conventions or contracts) also get a row in `12-decisions.md`;
  - architectural or hard-to-reverse decisions also get an ADR in `docs/adr/` (from the template).
- **Owner input** (answers, approvals, new instructions) is recorded in the entry with the owner's wording.
- Entries are **append-only after merge** (CI fails if a merged entry changes). Corrections go into a new entry that links the old one.
- The index `docs/build-log/README.md` lists every entry, newest first (CI checks this).

## 5. Freshness: how docs stay up to date
1. The change recipes (`19`) list the docs each kind of change must touch.
2. The Definition of Done (`11`) includes "docs updated per recipe".
3. `scripts/dev/check-docs.sh` checks:
   - required files exist;
   - relative links resolve;
   - the build log index is complete and entries have the required sections;
   - CR and ADR status lines are valid;
   - every project or assembly has a module doc;
   - the always-loaded files stay small;
   - on a PR, code changes come with a build log entry, a progress update and a CHANGELOG line, and merged log entries are untouched.
4. CI runs these checks plus the generated-docs comparison. The `reviewer` subagent checks docs against the diff.
5. Each module doc ends with `Last updated: <task id>`, so stale docs are easy to spot.
6. The task M0-63 and every milestone end include a full docs audit (`docs-auditor` subagent or `/docs-sync`).

## 6. Writing style
- Short sentences, present tense, concrete examples. Write for a newcomer who is smart but has no context.
- Link instead of repeating. If you copy a fact, you've created a second source that will drift.
- Each file has one topic and stays under ~150 lines (split it rather than letting it grow).
- Diagrams are Mermaid in Markdown, so they're diffable and render on GitHub.
- Use the glossary words (`31`). Keep code identifiers in backticks.
- A doc for something not built yet says so at the top.
