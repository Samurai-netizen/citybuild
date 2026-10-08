---
name: start-task
description: Begin a plan task (e.g. M0-15) or an approved CR task (e.g. CR-0007-T1) following the session protocol.
disable-model-invocation: true
argument-hint: "<TASK-ID>"
arguments: [id]
---

# Start task $id

## Task spec
!`bash scripts/dev/show-task.sh $id`

## Repository state
!`git branch --show-current; git status --short | head -20`

(If the spec above is empty or shows an error, run `bash scripts/dev/show-task.sh $id` yourself.
If the task doesn't exist, stop.)

## Follow `core-instructions/16-session-protocol.md` §B exactly
1. **Preconditions.** If any fails, stop and tell the owner:
   - the task exists and is `Todo`, or the CR is `Approved`;
   - its dependencies are `Done` (plan board);
   - the working tree is clean;
   - you are on an up-to-date `main` (`git switch main && git pull --ff-only`);
   - the previous PR is merged.
2. **Read** every file the task names, plus the "Read before" rows in `AGENTS.md` that match. Check the Definition of Ready (`11`).
   If acceptance criteria are unclear, ask now.
3. **Branch:** `git switch -c task/$id-<short-slug>`.
4. **Write ahead:**
   - `bash scripts/dev/new-log-entry.sh $id "<task title>" "Claude Code / <model id>"`; fill in Session 1 → Plan with numbered steps;
   - resume point in `core-instructions/13-progress.md`: state IN PROGRESS, task, branch, log entry path, steps, next step;
   - plan board row (or CR task row): `In progress`;
   - commit `chore($id): start task` and push with `git push -u origin HEAD`.
5. **If the task is tagged [PLAN]:** present the plan and **wait for the owner's approval**. Record it under "Owner input" in the build log.
6. Then implement step by step. After every green step, run `/checkpoint`. Log each decision as you make it.
