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
2. **Read only** what the spec's "Read first" line names, by section (`grep -n` the heading, then read that line range); for a spec
   without that line, the files it names and the matching "Read before" rows in `AGENTS.md`. Check the Definition of Ready (`11`).
   If acceptance criteria are unclear, ask now. For a task tagged `[ASSETS]`, open `docs/assets/requests/AR-$id.md`: rows that are
   not `accepted` run on placeholders (`core-instructions/27-asset-pipeline.md`).
3. **Branch:** `git switch -c task/$id-<short-slug>`.
4. **Write ahead:**
   - `bash scripts/dev/new-log-entry.sh $id "<task title>" "Claude Code / <model id>"`; fill in Session 1: the plan is the task's parts,
     one line each, and the **test list of the first part** (the spec's "Tests first" behaviours plus the neighbours you see);
   - fill its **Docs impact** table: one row per page the change could make wrong, from the recipe(s) in `core-instructions/19-change-recipes.md`
     (result `planned` for now);
   - resume point in `core-instructions/13-progress.md`: state IN PROGRESS, task, branch, log entry path, parts, next step;
   - plan board row (or CR task row): `In progress`;
   - commit `chore($id): start task` and push with `git push -u origin HEAD`.
5. **If the task is tagged [PLAN]:** present the plan (parts, test lists, the decisions you need) and **wait for the owner's approval**.
   Record it under "Owner input" in the build log.
6. Then work part by part, **tests first** (`16` §C): write the part's failing tests, run only them
   (`bash scripts/dev/test.sh <area> <TestClass>`), save `test($id): red, …`; write the code until the same run is green, save
   `feat($id): …`. A save is a commit and a push, nothing else. At the end of a part: `/checkpoint`, then `/compact` before the next
   part. No full suite before the finish, except where the spec's "Full run" line says so. Log a decision when you make one. Make no
   assets: a missing one is a row in the asset request.
