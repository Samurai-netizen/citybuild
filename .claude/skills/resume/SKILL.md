---
name: resume
description: Orient at the start of a session or after an interruption. Reads the resume point, build log and git state, verifies the baseline, and proposes the next step. Read-only.
argument-hint: "[optional task id]"
---

## Repository state
!`git branch --show-current; git status --short | head -20; git log --oneline -8`

## Do this (read-only — change nothing)
1. Read `core-instructions/13-progress.md` → Resume point. Note the state, task, branch, next step and open questions.
2. If a task is IN PROGRESS: open its build log entry (path in the resume point) and read the last session section.
   Print the task spec with `bash scripts/dev/show-task.sh <ID>`.
3. Compare the resume point with the git state above:
   - Is the branch right and pushed?
   - Are uncommitted changes explained? If not, follow `core-instructions/16-session-protocol.md` §F.
4. Run the fast baseline if code exists: `dotnet build server/CityBuilder.slnx` and the fast tests. Note the results.
5. Report to the owner in ≤ 15 lines:
   - where things stand;
   - anything inconsistent;
   - the exact next step;
   - whether you need a decision.
   Don't start coding until the owner confirms, unless the resume point says to continue and nothing is inconsistent.
