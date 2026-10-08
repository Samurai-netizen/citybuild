# Session Protocol — how every AI coding session runs

Several AI coders build this project **one after another** (different tools, models and machines).
None of them remembers previous sessions. **The repository is the only memory.** If something
isn't committed and pushed, the next coder doesn't know it.

This protocol is tool-agnostic. Claude Code has skills for each part (`/resume`, `/start-task`,
`/checkpoint`, `/finish-task`); other tools follow this file by hand. Hooks and CI enforce the essentials.

## Core rules
1. **One task at a time.** Every change belongs to a task id (`M0-15`) or an approved change request (`CR-0007`).
2. **Write ahead.** Record what you're about to do *before* doing it (resume point + build log).
3. **Small steps.** One step is at most ~1 hour of work or ~300 changed lines. Each step ends green.
4. **Checkpoint after every green step:** update the resume point and build log, commit, push.
   A session can end at any moment (token limit), and only pushed work survives.
5. **Never leave `main` dirty,** and never work on `main`. Work happens on `task/<ID>-<slug>` branches.
6. **Ask at the gates** in `15-change-control.md`. Don't guess design; don't expand scope silently.

## A. Session start (every session)
1. Read `AGENTS.md`, `core-instructions/02-principles.md` and `core-instructions/13-progress.md`
   (Claude Code loads these automatically).
2. Look at the repo state: `git status`, `git branch --show-current`, `git log --oneline -8`.
3. Check that `git config core.hooksPath` is `.githooks`; if not, run `bash scripts/dev/setup-hooks.sh`.
4. If the resume point says **IN PROGRESS**, go to §F (resume). Otherwise wait for the owner's instruction.
5. Before changing anything, confirm the baseline is green (build + fast tests). If it's red and that isn't
   your task, stop and report it. Don't fix unrelated breakage silently.

## B. Start a task
1. **Preconditions:** the task exists in `docs/plan/` with status `Todo` (or the CR is `Approved`); the tasks it
   depends on are `Done`; the working tree is clean; `main` is up to date (`git pull --ff-only`); the previous task's
   PR is merged. If it isn't merged, ask the owner to merge it (or to explicitly allow stacking).
2. **Read the task:** `bash scripts/dev/show-task.sh <ID>`, plus every file it names. Check the
   Definition of Ready (`11-workflow.md`). If acceptance criteria are unclear, ask before coding.
3. **Branch:** `git switch -c task/<ID>-<short-slug>`.
4. **Write ahead:**
   - `bash scripts/dev/new-log-entry.sh <ID> "<title>" "<tool> / <model>"` creates the build log entry. Fill in the plan.
   - Set the resume point in `13-progress.md`: state `IN PROGRESS`, the task, the branch, the planned steps, and the next step.
   - Set the task's row in the plan board to `In progress`.
   - Commit `chore(<ID>): start task` and push with `git push -u origin HEAD`.
5. **[PLAN] tasks:** present the plan and wait for the owner's approval before writing code.
   Record the approval in the build log under "Owner input".

## C. While working
- Implement step by step. Tests come with the step, not at the end (test-first for engine rules).
- **Log each decision when you make it** (build log → Decisions), including small ones: names, data shapes,
  trade-offs, rejected alternatives. Project-wide ones also get a row in `12-decisions.md` (`17` §4).
- Owner instructions given in chat must be written down in the same session: in the build log under "Owner input",
  and, if they are standing rules, in `15-change-control.md` → Standing owner directives.
- **Scope check:** if the task needs something it doesn't mention (a new file area, a new dependency, an API change),
  stop, log it, and ask, or write a CR. Don't build it "while you're there".
- **Stuck rule:** after two failed attempts at the same problem, record what you tried in the build log, then
  escalate (stronger model or owner) instead of trying a third variation.

## D. Checkpoint (after every green step, before any pause, before asking the owner a question)
1. Build and fast tests are green, or the step is explicitly marked broken in the resume point.
2. Resume point updated: done steps, next step, open questions, anything half-done.
3. Build log updated: session notes and decisions since the last checkpoint.
4. Commit (`feat(<ID>): …`, or `wip(<ID>): …` on task branches only) and `git push`.

## E. Finish a task
1. Go through the Definition of Done (`11-workflow.md`) and the change recipe for this kind of change (`19-change-recipes.md`).
2. Update the docs per the recipe. Regenerate the generated docs once `tools/DocTools` exists (from M0-61).
3. Run `bash scripts/dev/check-docs.sh --base origin/main` and the quality script (once it exists), and fix everything.
4. Independent review: for `[O]` tasks (and any task touching the engine, persistence or security), run the
   `reviewer` subagent (Claude Code) or ask a fresh session to review. Fix blocking findings and log them.
5. Finalize:
   - build log status `Done`, plus verification results and follow-ups;
   - resume point `IDLE`, with the last completed task and the next task;
   - plan board row `In review`;
   - one line in `CHANGELOG.md` under Unreleased;
   - new commands in `AGENTS.md`.
6. Commit, push, and open a PR (`gh pr create`, filling `.github/pull_request_template.md`). **Never merge it yourself.**
7. Give the owner the task report (`11-workflow.md`). After the owner merges, the next task sets the row to `Done`.

## F. Resume after an interruption (a new coder picks up)
1. Read the resume point and the task's build log entry, including the last session section.
2. `git status`. The next step depends on what you find:
   - **Clean tree, branch pushed:** continue from "Next step".
   - **Uncommitted changes the resume point explains:** run the tests, then finish or redo that step.
   - **Uncommitted changes nobody explains:** don't discard them. Inspect them, run the tests, and either keep them
     (and log that) or move them to a `rescue/<date>-<ID>` branch, then ask the owner.
   - **The resume point says IN PROGRESS but the branch is missing locally:** `git fetch` and check out `origin/task/<ID>-…`.
     Work that was never pushed is lost; redo it from the last checkpoint.
3. Add a new "Session N" section to the build log entry, naming yourself (tool/model), before continuing.
4. Never use `git reset --hard`, `git clean -f` or force-push to "get a clean state" without the owner's OK.

## G. Different tools, same rules
- Tools without hooks or skills still must follow A–F; `.githooks/` and CI catch most omissions.
- Name your tool and model in every build log session header. Reviewers can then calibrate their trust.
- Tag mapping: `[O]` = the strongest available model; `[S]` = a fast standard model.
- If your tool has its own memory feature, don't rely on it. Anything worth remembering goes into the repo.

## H. Context hygiene
Read only the files the task and the "Read before" table point to. Don't paste whole large files into chat; quote the lines you need.
Keep `13-progress.md` short (≤ 80 lines). History belongs in the build log and git, not in the resume point.
