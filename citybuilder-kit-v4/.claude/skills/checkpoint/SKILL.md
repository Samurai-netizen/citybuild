---
name: checkpoint
description: Save progress so any other AI coder can continue - update the resume point and build log, commit and push. Use after every green step, before pausing, and before asking the owner a question.
---

## Current changes
!`git branch --show-current; git status --short | head -30`

## Do this (`core-instructions/16-session-protocol.md` §D)
1. Run the build and fast tests for the area you changed. If they're red, either fix them or record precisely
   what's broken under "Known broken" in the resume point and use a `wip(...)` commit.
2. Build log entry (path in the resume point), current session section:
   - **Done** since the last checkpoint;
   - **Decisions**: one table row per decision (alternatives, why, scope);
   - **Owner input**, if any, quoted.
3. Resume point in `core-instructions/13-progress.md`: done steps, next step (concrete enough for a stranger),
   open questions, known broken, last green commit.
4. `git add` the changed files (never `.env` or secrets), then commit:
   - `feat|fix|test|docs|refactor(<ID>): <what>` when green;
   - `wip(<ID>): <what>` when not green.
5. `git push` (or `git push -u origin HEAD` the first time).
6. Tell the owner in one line what was checkpointed.
