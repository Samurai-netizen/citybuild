# Asset Pipeline — who makes assets, and how a task asks for them

**AI coders write code, data and text. They do not make game assets** (owner directive OD-22, `15`). Another AI model, run by the
owner, makes them from a written **asset request**. This file says what counts as an asset, how a request is written and when, and how
a delivery is accepted.

## What is an asset
| An asset (never made or edited by an AI coder) | Not an asset (the coder's work) |
|---|---|
| 3D models, their textures and animations, including scripts that generate them (`art/blender/`) | Content data (`server/content/*.json`), string tables, config |
| 2D pictures: icons, sprites, 9-slice panels, portraits, illustrations, store graphics | UXML, USS, layout, colours taken from the style guide |
| Audio: music, ambience, sound effects, voice | Shader and effect **code**, tween and particle settings in code |
| Fonts | Import settings, name maps, loaders, the code that plays or shows an asset |
| Video | Screenshots captured from the running game by a script |

The models and icons made before this rule (CR-0005, `art/README.md`) stay as they are. The Blender scripts are frozen: a change to a
model is an asset request like any other, and whoever fulfils it may use those scripts or not.

## The asset request (AR)
- One file per task that needs assets: `docs/assets/requests/AR-<TASK-ID>.md`, from `docs/assets/TEMPLATE.md`. A task that has one is
  tagged `[ASSETS]` in the plan.
- One table row per file to deliver: a stable **id** (the file name without its extension, `snake_case`), the **kind**, the **path** it
  is delivered to, the **spec** (format, size, budget), a **description** another model can use as its prompt, where it is **used**, its
  **priority** (`must` or `nice`), the **placeholder** the code shows until it arrives, and its **status**.
- The look and sound of everything is in one page, `docs/assets/STYLE.md` (written in M1-01). A request points to it and to reference
  pictures; it never repeats it. A row says only what is particular to that asset.
- Describe what the player must **recognize** ("a windmill: four cloth sails, a stone base, a sack of flour at the door"), the view it
  is seen from, and what it stands next to. Don't describe taste; that is the style guide's job.
- Ask only for what the task's acceptance criteria need. `nice` rows are the first to be dropped.

## When it is written: one task ahead
The owner needs time to have the files made. So a request exists **before** its task starts:
1. A milestone's design task writes every request of the milestone in outline (M1-01 part F).
2. At the finish of each task, the coder completes the request of the **next** `[ASSETS]` task (`/asset-request <TASK-ID>`): every row
   filled, checked against that task's spec. This is a step of `/finish-task`.
3. The task report tells the owner which request is ready to be sent to the asset model.
A task never waits for assets: see Placeholders.

## Placeholders
- Code that shows or plays an asset loads it by id and falls back to a **placeholder made in code**: a tinted box for a model, a flat
  tile with the id's initials for an icon, silence for a sound. A placeholder is never a file in the repository.
- A missing asset is logged once per id, never an error for the player.
- A test lists the ids still on placeholders (the "pending" list). A task tagged `[ASSETS]` is done when its acceptance criteria pass;
  the pending list must be empty where the task's spec says so (M1-09 for Era I).

## Delivery and acceptance
1. The owner puts the files at the paths of the request and says so ("AR-M1-05 delivered").
2. The coder checks each row: the file exists at the path, with the name, format and size asked for, inside its budget; a model keeps
   its tile, pivot and naming contract (`art/README.md`, `art/MOTIONS.md`). From M1-09: `bash scripts/dev/check-assets.sh <AR file>`.
3. Each row gets a status: `accepted`, or `rejected: <reason>` (the reason is the next prompt). The coder **never repairs an asset**: no
   repainting, re-exporting, trimming or renaming of the source. Wiring it into the game is the coder's work.
4. The owner judges the look and sound; the coder judges only the spec. A `[MANUAL]` step in the task asks for that look.
5. Every delivered file is listed in `docs/assets/LICENSES.md`: who or what made it (tool and model), when, and under which licence
   or terms. No row, no merge: the store listing depends on it.

Status values of a row: `requested` · `delivered` · `accepted` · `rejected: <reason>` · `dropped: <reason>`.

## Rules for the coder
- Don't generate, draw, model, compose or edit an asset, also not "just a quick icon", and not with a tool the session happens to have.
- Don't block on a missing asset and don't ask the owner for one in chat: add or correct the row in the request.
- Don't change an id after its request was sent. A wrong id is a new row; the old one is `dropped`.
- Keep the request current: a task that finds it needs one more file adds the row and tells the owner in the checkpoint line.
- Third-party assets (a font, a sound pack) are a G-Dependency: ask first, and record the licence.
