---
name: checkpoint
description: End a part so any other AI coder can continue - area tests, resume point, build log, commit and push. Use after every part of a task, before pausing, and before asking the owner a question. (After a single green step, just commit and push.)
---

## Current changes
!`git branch --show-current; git status --short | head -30`

## Do this (`core-instructions/16-session-protocol.md` §D)
A checkpoint ends a **part**. After a single green step, only save: `git add`, commit, `git push`.
1. Run the tests of the areas this part touched: `bash scripts/dev/test.sh changed` (tier T1; run `test-unity.sh` only when `client/` or
   `shared/` changed and `UNITY_EDITOR` is set, and read `Logs/test-unity/*.xml` for `result="Failed"`, never the whole log). Not the
   full suite, unless the spec's "Full run" line names this point. If they're red, either fix them or record precisely what's broken
   under "Known broken" in the resume point and use a `wip(...)` commit.
2. Docs: the pages this step changed are in this commit, and their **Docs impact** rows say so (a page you didn't plan for gets a row).
3. Build log entry (path in the resume point), current session section:
   - **Done**: what this part did, in at most five lines (no test output, only counts);
   - **Decisions**: one table row per decision (`17` §4 says what counts);
   - **Owner input**, if any, quoted;
   - the **test list of the next part**.
4. Resume point in `core-instructions/13-progress.md`: done steps, next step (concrete enough for a stranger),
   open questions, known broken, last green commit.
5. `git add` the changed files (never `.env` or secrets), then commit:
   - `feat|fix|test|docs|refactor(<ID>): <what>` when green;
   - `wip(<ID>): <what>` when not green.
6. `git push` (or `git push -u origin HEAD` the first time).
7. Tell the owner in one line what was checkpointed, with any questions and `[MANUAL]` steps of the part as one numbered batch.
8. If more parts remain: `/compact` (or `/clear`, then `/resume`) before the next one. One part per context.
