---
name: asset-request
description: Write or complete the asset request (AR) of a task tagged [ASSETS] - the list of models, pictures, audio and fonts another AI model will make. Use at the finish of the task before it, in a design task, or when the owner asks. Never makes an asset.
argument-hint: "<TASK-ID>"
arguments: [id]
---

# Asset request for $id

## Task spec
!`bash scripts/dev/show-task.sh $id`

## Existing request
!`ls docs/assets/requests/AR-$id.md 2>/dev/null || echo "none yet: copy docs/assets/TEMPLATE.md to docs/assets/requests/AR-$id.md"`

## Follow `core-instructions/27-asset-pipeline.md`
1. Read the task spec above and only the design sections it names for its content (items, buildings, screens). Read
   `docs/assets/STYLE.md` once for the common specs (until it exists: `art/README.md` → Style guide, `art/MOTIONS.md`).
2. List what the task's **acceptance criteria** need, nothing more. Look at what exists first (`ls` the target folders): never request a
   file that is already there.
3. One row per file: id (= file name, `snake_case`), the path it is delivered to, the spec only where it differs from the section's
   common spec, a description another model can use as its prompt (what the player must recognize, the view, what it stands next to),
   where it is used, `must` or `nice`, the placeholder the code shows meanwhile, status `requested`.
4. Check: every id is unique in `docs/assets/requests/`; every path is inside the Unity project's asset folders or `art/`; every row of
   an earlier request for this task that is no longer needed is `dropped: <reason>`, never deleted.
5. Commit it with the task you are in (`docs(<current task>): asset request for $id`) and tell the owner in one line that
   `AR-$id` is ready to be sent to the asset model, with the row counts per kind.

Never generate, draw, compose or edit an asset yourself, with any tool.
