---
name: finish-task
description: Complete the current task - Definition of Done, docs per change recipe, checks, self-review, build log, PR, and the task report. Run it yourself when every planned step of the task is done and checkpointed (owner directive OD-15).
---

## Branch diff
!`git branch --show-current; git diff --stat origin/main...HEAD 2>/dev/null | tail -25`

## Follow `core-instructions/16-session-protocol.md` §E
1. Re-read the task spec (`bash scripts/dev/show-task.sh <ID>`) and check every acceptance criterion against the code and its tests
   (each behaviour has a test that was red before its code: the `test(…): red` saves).
2. Walk the Definition of Done in `core-instructions/11-workflow.md`, and the matching recipe(s) in `core-instructions/19-change-recipes.md`.
   Update every doc the recipe lists and close every **Docs impact** row in the build log entry (`updated`, `new`, `deleted` or
   `no change: <why>`). Set `Last updated: <ID>` in the module docs you touched.
3. Once `tools/DocTools` exists, run `dotnet run --project tools/DocTools -- generate`.
4. **The one full run:** `bash scripts/dev/quality.sh > /tmp/quality.log 2>&1; tail -15 /tmp/quality.log` (everything CI runs: the docs
   checks, the handbook, format, the Release build, the fast suite, integration, DocTools, Unity; name any step it skipped in the
   report). Fix what it reports and re-run only the failed step (`--only <step>`).
5. **Review:** `git diff --stat origin/main...HEAD` first; then read line by line only where a mistake is expensive (the engine,
   persistence, security, contracts, content: `git diff origin/main...HEAD -- <path> ':!*.meta'`), by area elsewhere. Fix blocking
   findings, and log them and the fixes in the build log.
   **Don't start an agent on your own** (owner directive OD-9, `core-instructions/15`): if you think one would help, ask the owner first with a
   proposal (no agents / small / big, why, how many, which model) and wait for the answer.
6. **Assets:** no asset was made or edited by you; every placeholder left is a `requested` row. If a task tagged `[ASSETS]` is within
   the next two on the board, complete its request now (`/asset-request <ID>`) and name it in the report.
7. Finalize:
   - build log entry: status Done, verification table (commands + results), follow-ups; its line in `docs/build-log/README.md` says `Done` too;
   - resume point: state IDLE, last completed = this task, next task id;
   - plan board row: `In review`;
   - `CHANGELOG.md`: one line under Unreleased;
   - decision rows and ADRs, if any;
   - `AGENTS.md` command list, if a command became real (protected file: ask the owner).
8. Commit `docs(<ID>): finish task` (or the last feat commit) and `git push`.
9. Open the PR: `gh pr create --title "<type>(<ID>): <summary>" --body-file <filled .github/pull_request_template.md>` (its Docs impact
   section lists the pages from the entry).
   If `gh` isn't available, give the owner the title and body. **Never merge.**
10. Reply with the task report format from `core-instructions/11-workflow.md` (about 40 lines at most).
