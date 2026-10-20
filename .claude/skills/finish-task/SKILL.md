---
name: finish-task
description: Complete the current task - Definition of Done, docs per change recipe, checks, self-review, build log, PR, and the task report.
disable-model-invocation: true
---

## Branch diff
!`git branch --show-current; git diff --stat origin/main...HEAD 2>/dev/null | tail -25`

## Follow `core-instructions/16-session-protocol.md` §E
1. Re-read the task spec (`bash scripts/dev/show-task.sh <ID>`) and check every acceptance criterion against the code.
2. Walk the Definition of Done in `core-instructions/11-workflow.md`, and the matching recipe(s) in `core-instructions/19-change-recipes.md`.
   Update every doc the recipe lists and close every **Docs impact** row in the build log entry (`updated`, `new`, `deleted` or
   `no change: <why>`). Set `Last updated: <ID>` in the module docs you touched.
3. Once `tools/DocTools` exists, run `dotnet run --project tools/DocTools -- generate`.
4. Run `bash scripts/dev/check-docs.sh --base origin/main`, `bash scripts/dev/docs-site.sh build` (the handbook: links, anchors,
   sidebar) and the quality script (`bash scripts/dev/quality.sh` once it exists). Fix everything they report.
5. **Review:** review your own branch diff against the task spec, the Definition of Done and the recipe, line by line for `[O]` tasks and for any
   task touching the engine, persistence or security. Fix blocking findings, and log them and the fixes in the build log.
   **Don't start an agent on your own** (owner directive OD-9, `core-instructions/15`): if you think one would help, ask the owner first with a
   proposal (no agents / small / big, why, how many, which model) and wait for the answer.
6. Finalize:
   - build log entry: status Done, verification table (commands + results), follow-ups;
   - resume point: state IDLE, last completed = this task, next task id;
   - plan board row: `In review`;
   - `CHANGELOG.md`: one line under Unreleased;
   - decision rows and ADRs, if any;
   - `AGENTS.md` command list, if a command became real (protected file: ask the owner).
7. Commit `docs(<ID>): finish task` (or the last feat commit) and `git push`.
8. Open the PR: `gh pr create --title "<type>(<ID>): <summary>" --body-file <filled .github/pull_request_template.md>` (its Docs impact
   section lists the pages from the entry).
   If `gh` isn't available, give the owner the title and body. **Never merge.**
9. Reply with the task report format from `core-instructions/11-workflow.md`.
