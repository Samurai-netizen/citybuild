---
name: finish-task
description: Complete the current task - Definition of Done, docs per change recipe, checks, independent review, build log, PR, and the task report.
disable-model-invocation: true
---

## Branch diff
!`git branch --show-current; git diff --stat origin/main...HEAD 2>/dev/null | tail -25`

## Follow `core-instructions/16-session-protocol.md` §E
1. Re-read the task spec (`bash scripts/dev/show-task.sh <ID>`) and check every acceptance criterion against the code.
2. Walk the Definition of Done in `core-instructions/11-workflow.md`, and the matching recipe(s) in `core-instructions/19-change-recipes.md`.
   Update every doc the recipe lists. Set `Last updated: <ID>` in the module docs you touched.
3. Once `tools/DocTools` exists, run `dotnet run --project tools/DocTools -- generate`.
4. Run `bash scripts/dev/check-docs.sh --base origin/main` and the quality script (`bash scripts/dev/quality.sh` once it exists).
   Fix everything they report.
5. **Independent review:** for `[O]` tasks, or any task touching the engine, persistence or security,
   use the `reviewer` subagent on this branch. Fix blocking findings, and log them and the fixes in the build log.
6. Finalize:
   - build log entry: status Done, verification table (commands + results), follow-ups;
   - resume point: state IDLE, last completed = this task, next task id;
   - plan board row: `In review`;
   - `CHANGELOG.md`: one line under Unreleased;
   - decision rows and ADRs, if any;
   - `AGENTS.md` command list, if a command became real (protected file: ask the owner).
7. Commit `docs(<ID>): finish task` (or the last feat commit) and `git push`.
8. Open the PR: `gh pr create --title "<type>(<ID>): <summary>" --body-file <filled .github/pull_request_template.md>`.
   If `gh` isn't available, give the owner the title and body. **Never merge.**
9. Reply with the task report format from `core-instructions/11-workflow.md`.
