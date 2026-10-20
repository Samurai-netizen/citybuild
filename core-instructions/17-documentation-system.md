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
The handbook site (§7) renders both kinds as one book, and every page says which kind it is.

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
| Which pages a change touched, and why | the build log entry's Docs impact table | §5 |
| The handbook's chapters and reading order | `website/sidebars.js` | §7; the files stay where they are |

## 3. Generated reference docs
From M0-61, `tools/DocTools` generates `docs/reference/generated/*.md` from the code and content: items, buildings,
research, error codes, endpoints (OpenAPI), configuration keys, DB schema, and the project dependency map.
- Never edit generated files by hand (Claude Code denies it).
- CI regenerates them and fails if the committed files differ ("docs out of date").
- Until M0-61, the tables in `05`, `06` and `23` are the reference and are updated by hand.
- Generated files are handbook pages too (chapter "Development guide" → Reference): DocTools writes plain CommonMark (§7).

## 4. Build log and decisions
- **One entry per task**, at `docs/build-log/<YYYY>/<YYYY-MM-DD>-<NN>-<TASK>.md`, created with `scripts/dev/new-log-entry.sh`
  from `docs/build-log/TEMPLATE.md`. Each session working on the task appends a "Session N" section.
- **Every decision goes into the entry's Decisions table** when it's made: what was chosen, the alternatives, why,
  and its scope. Small, local decisions count too. That's the point.
- **Escalation:**
  - project-wide decisions (affecting other tasks, conventions or contracts) also get a row in `12-decisions.md`;
  - architectural or hard-to-reverse decisions also get an ADR in `docs/adr/` (from the template).
- **Owner input** (answers, approvals, new instructions) is recorded in the entry with the owner's wording.
- **Docs impact:** a table of every page the change could make wrong (from the recipes in `19`), each with a result:
  `updated`, `new`, `deleted` or `no change: <why>` (`planned` only while the task is in progress). It is filled when the task starts and
  closed when it finishes (§5).
- Entries are **append-only after merge** (CI fails if a merged entry changes). Corrections go into a new entry that links the old one.
- The index `docs/build-log/README.md` lists every entry, newest first (CI checks this).

## 5. Freshness: how docs stay up to date
Every task keeps the docs current (owner directive OD-11, `15`), in this order:
1. **Plan it.** At the start, the change recipes (`19`) give the pages the change could make wrong; they become the rows of the
   entry's Docs impact table (§4).
2. **Do it with the code.** A step that changes behaviour, a contract, a command or a file name changes its pages in the same commit.
3. **Close it.** At the finish every row has a result, and the Definition of Done (`11`) checks it.
4. **Checks** (`scripts/dev/check-docs.sh`; the pre-commit hook runs the static part, CI the rest):
   - required files exist; relative links resolve; the build log index is complete and entries have the required sections;
   - CR and ADR status lines are valid; every project or assembly has a module doc; the always-loaded files stay small;
   - on a PR (`--base`): code changes (`server/`, `shared/`, the Unity project, `tools/`, `scripts/`, `website/`) come with a build log
     entry, a progress update and a CHANGELOG line; merged log entries are untouched;
   - on a PR: a module's code changed → its module doc changed, or a Docs impact row names it with the reason (the module is the
     nearest `.csproj` or `.asmdef` above the file; tests have none);
   - on a PR: a deleted or renamed file, or a removed folder, is no longer named in the living docs (everything except build log
     entries, `CHANGELOG.md`, ADRs, CRs and the plan, which record their time);
   - on a PR: a new build log entry has a Docs impact table, and none of its rows is open once the entry is `Done`.
5. **The handbook builds** (`bash scripts/dev/docs-site.sh build`, in the CI job `docs` and in `/finish-task`): broken links,
   anchors and pages missing from the sidebar fail it (§7).
6. CI runs these checks plus the generated-docs comparison. The coder checks the docs against the diff (an agent only after the owner
   approved it, OD-9).
7. Each module doc ends with `Last updated: <task id>`, and each handbook page shows the date of its last commit, so stale docs are easy to spot.
8. The task M0-63 and every milestone end include a full docs audit (`/docs-sync`, done by hand; the `docs-auditor` agent only after the
   owner approved it, OD-9).

## 6. Writing style
- Short sentences, present tense, concrete examples. Write for a newcomer who is smart but has no context.
- Link instead of repeating. If you copy a fact, you've created a second source that will drift.
- Each file has one topic and stays under ~150 lines (split it rather than letting it grow).
- Diagrams are Mermaid in Markdown, so they're diffable and render on GitHub.
- Use the glossary words (`31`). Keep code identifiers in backticks.
- A doc for something not built yet says so at the top.
- Placeholders and anything that looks like an HTML tag go in backticks (`` `<task id>` ``); bare angle brackets in prose break the page (§7).

## 7. The handbook site
The docs are also a website: the **handbook**, published at https://samurai-netizen.github.io/citybuild/ after every merge to `main`,
with search. It is a presentation layer (Docusaurus, `website/README.md`): it renders the files where they are, and nothing is
copied. Every Markdown file under `core-instructions/` and `docs/` is a page (templates excepted), plus the root and folder READMEs
listed in `website/docs-set.js`.
- **Plain Markdown.** Pages are `.md`: CommonMark with GitHub tables and Mermaid blocks. No MDX, no front matter, no raw HTML, so the
  same text reads the same on GitHub, on the site and in every AI tool.
- **URL = path.** `core-instructions/04-simulation-model.md` is `/core-instructions/04-simulation-model/`; a folder's `README.md` is the
  folder's page. Never rename a page for the site's sake.
- **Links.** Write cross-references as before: spec numbers (`` `04` ``) and repository paths in backticks become links on the site
  (to the page, or to the file on GitHub); a path that doesn't exist yet stays plain code. Links between pages are relative Markdown
  links, which work on GitHub too. `#anchors` use GitHub's heading slugs.
- **Kinds.** Each page shows its kind (specification, as built, how-to, decision, change request, plan, history, live status,
  generated) and its source file, taken from its folder (`website/src/components/PageKind/kinds.js`). Put a page where its kind belongs.
- **The book.** `website/sidebars.js` places every page in a chapter; the build fails, naming the page, when one is missing.
  A new page follows the recipe "New documentation page" in `19`.
- **Check before the PR:** `bash scripts/dev/docs-site.sh build` (preview with `start`). Broken links and anchors fail it.

