# Session Protocol — how every AI coding session runs

Several AI coders build this project **one after another** (different tools, models and machines).
None of them remembers previous sessions. **The repository is the only memory.** If something
isn't committed and pushed, the next coder doesn't know it.

This is workflow v2 (CR-0010, owner directives OD-19 … OD-22 in `15`): **tests before code, focused test runs, small context, no
self-made assets.** It is tool-agnostic. Claude Code has skills for each part (`/resume`, `/start-task`, `/checkpoint`, `/finish-task`,
`/asset-request`); other tools follow this file by hand. Hooks and CI enforce the essentials.

## Units of work
| Unit | Size | Ends with |
|---|---|---|
| **Task** | One PR. M0: about an hour. From M1: one to three days | `/finish-task`: full checks, review, PR |
| **Part** (A, B, C… in the task spec) | One coherent slice; fits one context | A **checkpoint**: area tests, resume point, build log, push |
| **Step** | One red → green cycle, minutes | A **save**: commit and push, nothing else |

## Core rules
1. **One task at a time.** Every change belongs to a task id (`M1-04`) or an approved change request (`CR-0007`).
2. **Write ahead.** Record what you're about to do *before* doing it (resume point + build log plan).
3. **Tests first.** For every behaviour, the failing test exists and was seen failing before its code (`08` → Test-first loop).
4. **Save after every green step; checkpoint after every part.** A session can end at any moment, and only pushed work survives.
5. **Run the smallest test set that can fail** (`08` → Test tiers). The full suite runs at the finish and at the points a spec names.
6. **Spend tokens on the work, not on ceremony** (§H).
7. **Never leave `main` dirty,** and never work on `main`. Work happens on `task/<ID>-<slug>` branches.
8. **Ask at the gates** in `15-change-control.md`. Don't guess design; don't expand scope silently.
9. **Make no assets.** Ask for them in an asset request (`27`).

## A. Session start (every session)
1. Read `AGENTS.md`, `core-instructions/02-principles.md` and `core-instructions/13-progress.md`
   (Claude Code loads these automatically). Read nothing else yet.
2. Look at the repo state: `git status -s`, `git branch --show-current`, `git log --oneline -5`.
3. Check that `git config core.hooksPath` is `.githooks`; if not, run `bash scripts/dev/setup-hooks.sh`.
4. If the resume point says **IN PROGRESS**, go to §F (resume). Otherwise wait for the owner's instruction.
5. **Baseline:** trust it when `HEAD` is a merged commit of `main` (CI ran there) or the resume point's last green commit. Only
   otherwise, or when the first build fails, run `bash scripts/dev/test.sh fast`. If it's red and that isn't your task, stop and report.

## B. Start a task
1. **Preconditions:** the task exists in `docs/plan/` with status `Todo` (or the CR is `Approved`); the tasks it
   depends on are `Done`; the working tree is clean; `main` is up to date (`git pull --ff-only`); the previous task's
   PR is merged. If it isn't merged, ask the owner to merge it (or to explicitly allow stacking).
2. **Read the task:** `bash scripts/dev/show-task.sh <ID>`, then **only** what its "Read first" line names: the sections, not the files
   (`grep -n` the heading, read that range). Check the Definition of Ready (`11`). If acceptance criteria are unclear, ask before coding.
3. **Assets:** for a task tagged `[ASSETS]`, open its request (`docs/assets/requests/AR-<ID>.md`). Rows not yet `accepted` run on
   placeholders (`27`); that never blocks the start.
4. **Branch:** `git switch -c task/<ID>-<short-slug>`.
5. **Write ahead:**
   - `bash scripts/dev/new-log-entry.sh <ID> "<title>" "<tool> / <model>"` creates the build log entry. Its plan is the task's parts, one
     line each, and for the first part its **test list** (the behaviours of the spec's "Tests first", plus the neighbours you see).
   - Fill the entry's **Docs impact** table: every page the change could make wrong, from the recipe(s) in `19` (`17` §5).
   - Set the resume point in `13-progress.md`: state `IN PROGRESS`, the task, the branch, the parts, the next step.
   - Set the task's row in the plan board to `In progress`.
   - Commit `chore(<ID>): start task` and push with `git push -u origin HEAD`.
6. **[PLAN] tasks:** present the plan (parts, test lists, decisions you need) and wait for the owner's approval before writing code.
   Record the approval in the build log under "Owner input".

## C. While working: the loop of a part
1. **Red.** Write the part's tests from its test list, with the least code that lets them compile (empty types, methods that throw).
   Run only them: `bash scripts/dev/test.sh <area> <TestClass>`. Each must fail **because the behaviour is missing**, not because of a
   typo. Save: `test(<ID>): red, <what>`.
2. **Green.** Write the code. Run the same focused command after each change until it passes. Save: `feat(<ID>): <what>`.
3. **Clean.** Refactor with the tests green. Doc comments (`14`) and the pages this step made wrong go into the same commit.
4. Repeat for the next behaviour. When the part's list is green: **checkpoint** (§D).

What is not test-first, and what replaces the test: pure looks (USS, layout, art wiring, motion timing) get a line in the task's
`[MANUAL]` list; a throwaway spike is deleted and redone test-first; docs have `check-docs.sh`.

- **Decisions:** log one row when you make one that changes behaviour, a contract, a number, a name other files use, or that picks
  between real alternatives or reads an unclear spec (`17` §4). Project-wide ones also get a row in `12-decisions.md`.
- **Owner instructions** given in chat are written down in the same session: in the build log under "Owner input", and, if they are
  standing rules, in `15-change-control.md` → Standing owner directives.
- **Scope check:** if the task needs something it doesn't mention (a new file area, a new dependency, an API change, an asset),
  stop, log it, and ask, write a CR, or add an asset request row. Don't build it "while you're there".
- **Stuck rule:** after two failed attempts at the same problem, record what you tried in the build log, then
  escalate (stronger model or owner) instead of trying a third variation.
- **Questions and [MANUAL] steps are batched:** collect them and ask once per part, numbered, after a checkpoint.

## D. Save and checkpoint
**Save** (after every green step, and before anything risky): `git add` the step's files, commit, `git push`. No file ceremony. The
commit subject says what works now; when the session may end soon, its body says the next step in one line.

**Checkpoint** (after every part, before any pause, before asking the owner a question):
1. `bash scripts/dev/test.sh changed` is green (tier T1; `client/` or `shared/` changed and `UNITY_EDITOR` is set: `test-unity.sh` too),
   or the part is explicitly marked broken in the resume point and committed as `wip(<ID>): …`.
2. Build log, current session: what the part did in at most five lines, its decision rows, owner input, Docs impact rows; the **next
   part's test list**.
3. Resume point: done parts, the next step (concrete enough for a stranger), open questions, the last green commit.
4. Commit and `git push`. Tell the owner in one line.
5. **Shed context:** `/compact`, or `/clear` then `/resume`, before the next part. One part per context is the rule for tasks from M1 on.

## E. Finish a task
1. Check every acceptance criterion of the spec against the code and its tests. Walk the Definition of Done (`11`) and the change
   recipe (`19`).
2. Close every Docs impact row (`updated`, `new`, `deleted` or `no change: <why>`). Regenerate the generated docs
   (`dotnet run --project tools/DocTools -- generate`) when code or content they show changed.
3. **The one full run** (tier T2): `bash scripts/dev/quality.sh > /tmp/quality.log 2>&1; tail -15 /tmp/quality.log` (everything CI runs:
   docs checks, handbook, format, Release build, the fast suite, integration with Docker, DocTools, Unity with `UNITY_EDITOR`; it names
   what it skipped). Fix what it reports and re-run only the failed step (`--only <step>`).
4. **Review your own branch:** `git diff --stat origin/main...HEAD` first; then line by line only where a mistake is expensive (the engine,
   persistence, security, contracts, content), by area elsewhere; never `.meta` or binary files. Fix blocking findings and log them. Don't
   start an agent for this on your own (OD-9, `15`). The owner can start a fresh session as the independent reviewer.
5. **The next asset request:** if a later task tagged `[ASSETS]` is within the next two on the board, complete its request now
   (`/asset-request <ID>`, `27`), so the owner can have the files made in the meantime.
6. Finalize: build log status `Done` with the verification table and follow-ups (and its line in `docs/build-log/README.md`); resume
   point `IDLE` with the last and the next task; plan board row `In review`; one line in `CHANGELOG.md` under Unreleased; new commands in
   `AGENTS.md`.
7. Commit, push, and open a PR (filling `.github/pull_request_template.md`). **Never merge it yourself.**
8. Give the owner the task report (`11`). After the owner merges, the next task sets the row to `Done`.

## F. Resume after an interruption (a new coder picks up)
1. Read the resume point, `git log --oneline -8` on the task branch (the saves say what works), and **only the last session section** of
   the task's build log entry with its Decisions table. Don't re-read finished parts.
2. `git status -s`. The next step depends on what you find:
   - **Clean tree, branch pushed:** continue from "Next step". The last save was a `test(...): red` commit: its tests are your start.
   - **Uncommitted changes the resume point explains:** run the focused tests, then finish or redo that step.
   - **Uncommitted changes nobody explains:** don't discard them. Inspect them, run the tests, and either keep them
     (and log that) or move them to a `rescue/<date>-<ID>` branch, then ask the owner.
   - **The resume point says IN PROGRESS but the branch is missing locally:** `git fetch` and check out `origin/task/<ID>-…`.
     Work that was never pushed is lost; redo it from the last save.
3. Add a new "Session N" section to the build log entry, naming yourself (tool/model), before continuing.
4. Never use `git reset --hard`, `git clean -f` or force-push to "get a clean state" without the owner's OK.

## G. Different tools, same rules
- Tools without hooks or skills still must follow A–F; `.githooks/` and CI catch most omissions.
- Name your tool and model in every build log session header. Reviewers can then calibrate their trust.
- Tag mapping: `[O]` = the strongest available model; `[S]` = a fast standard model.
- If your tool has its own memory feature, don't rely on it. Anything worth remembering goes into the repo.

## H. Token budget
Tokens buy code and tests. Everything else is kept small.
- **Load less.** Read what the spec's "Read first" names, by section: `grep -n` the heading, then read that line range. Never load
  whole: a plan file (use `show-task.sh`), another task's build log entry, `docs/reference/generated/`, lock files, `.meta` files,
  models, logs. Don't re-read a file you just wrote. Don't re-read finished parts after a `/compact`.
- **Run quietly.** `bash scripts/dev/test.sh` prints one line per area and only the failing tests; `git status -s`, `git diff --stat`,
  `git log --oneline`; `dotnet build -v q`. Send any long output to a file and print its tail only when it failed.
- **Run less.** The focused test (T0) in the loop, the touched areas (T1) at a checkpoint, everything (T2) once at the finish and at
  the points the spec names (`08` → Test tiers). Never the full suite "to be sure" after a small change.
- **Write less.** A build log part is at most five lines; a decision is one row; nothing is narrated twice (the commit says what, the
  log says why). Docs: change only the pages a step made wrong, and link instead of copying (`17` §6). Chat: no file dumps, no
  restating the spec; a checkpoint is one line; the task report follows `11`, at most about 40 lines.
- **Keep the context short.** One part per context; `/compact` or `/clear` + `/resume` after each checkpoint. The resume point and the
  saves carry the state, not the conversation.
- **Use the cheaper tool.** A script for mechanical bulk (renames, key moves, `.meta` work), not hand edits; generated reference docs
  (DocTools), not hand-written copies; `[S]` models for work a test list already pins down; `[O]` for the engine, security, plans and
  reviews (`11` → Model guidance).
- **Ask once.** Questions and `[MANUAL]` steps go to the owner in one numbered batch per part.
- Keep the always-loaded files small (`AGENTS.md`, `CLAUDE.md`, `02`, `13`; limits in `11`). Owner questions that wait longer than one
  task live in `docs/owner-queue.md`, not in the resume point.
