# Visual v2: build log

- **Status:** In progress
- **Started:** 2026-11-04 · **Tool:** Claude Code (cloud session) · **Branch:** `claude/compassionate-meitner-sd7nr0` (this folder) and
  `claude/visual-v2-fit-test` (the fit test)
- **Change id:** `CR-0009`, reserved by this package for its commits (the commit hook needs an id). The CR file itself is written when
  the package is adopted, so `docs/changes/` stays untouched now.
- **Request:** the owner's words are in `README.md`. They also count as the approval of this work: the owner asked for it to be done now,
  in a separate folder, instead of a CR first. When a task adopts the package, it writes the regular CR and build log entry and links here.

## Owner input
- 2026-11-04: the request quoted in `README.md`. Reference for the UI: the "CityBuilder UI Showcase" artifact, whose source is
  `art/ui/showcase/` (byte for byte the same files).

## Plan (each step ends with a commit and a push)
1. Write-ahead: this folder, the request and the plan.
2. The Blender pipeline: a fork of `art/blender/` that writes into `unity/`; new palette colours; moving parts with pose empties; store
   fill models; smoke; checks for all of it.
3. Ground: four grass tiles, the dirt tile, road tiles for both road levels that join their neighbours.
4. Nature, people, items, smoke: trees, bush, rock, stump, flowers; the worker and the courier with limbs that can walk; carried loads;
   logs, planks and coins in three sizes; the smoke puff.
5. Today's buildings, more detailed, with their moving parts and store fill: Cottage, Woodcutter's Hut, Sawmill, Trading Post (levels 1
   and 2) and the construction site.
6. CR-0008's buildings: Scriptorium, Courier Station (levels 1 and 2), Warehouse (levels 1 and 2).
7. Previews (every model, the town, animation and fill strips), every UI icon rendered again from the new models, new icons, UI glyphs.
8. Client code for the overlay: the motion rig and animator, smoke, store fill, ground variants, road arms, with EditMode tests; the pure
   logic compiled and tested under .NET here.
9. The UI rework after the showcase: shared tokens and components, every screen, fonts; a browser preview with screenshots; the rules of
   `SharedLookTests` run as a script.
10. The fit test on `claude/visual-v2-fit-test`: the package applied to `art/` and the Unity project and wired into the client; every check
    that runs without Unity; the Unity steps listed as [MANUAL].
11. Close-out: status, verification, adoption steps, follow-ups.

## Progress
- Steps 1-6 done: 49 models (5 grass and dirt tiles, 2 road tiles, 6 nature models, the worker, the courier, 2 loads, the smoke puff,
  13 buildings, 9 store fill models, 9 item props) built by `blender/build_all.py`, every check passing (tile bounds with every moving
  part along its cycle and the full store, floor, triangle budgets, one material, anchors, poses, fill steps, road arms).

## Decisions
| # | Decision | Why / alternative |
|---|---|---|
| 1 | Everything goes into `visual-v2/`; on this branch nothing outside it changes, not even `13-progress.md`, the M0 board, `docs/build-log/` or `CHANGELOG.md`; this file is the build log | The owner asked for no interference with the main session, which edits those files on its own branch. Alternative: a regular task entry, which would conflict with it |
| 2 | The fit test is the branch `claude/visual-v2-fit-test`, made from this one: the overlay copied into `art/` and the Unity project and wired into the client. It is a test bed, not meant to be merged as is | CR-0008-T2 changes the same client files; the package is adopted after it |
| 3 | Moving parts are code-driven: each is a mesh with its pivot on its joint, plus an empty `<Part>_Pose` that marks the far end of its motion; the client moves between the two, or spins around their axis | The model import has animation off (`ArtImportPostprocessor`) and the client already finds parts by name (`Wheel`). Part and pose go through the same FBX axis conversion, so no axis is written in code |
| 4 | Store fill is a separate model per building level, `<type>_l<level>_fill.fbx`, with a child per item and step, `Fill_<item>_<step>`, shown cumulatively (step k shows 1 to k) | The building model stays what an empty building looks like, so the placement ghost, the icons and today's code see no fill; fill changes never touch the moving parts |
| 5 | Fill steps (the owner left them to me): Woodcutter logs 3; Sawmill logs 2 and planks 3; Trading Post planks 2; Warehouse logs 3 and planks 3. Cottage, Scriptorium, Courier Station and the construction site keep no goods (CR-0008 §1) | One step per third of the store; the Sawmill's input yard is smaller than its output stack; the Trading Post's store holds only the planks it sells, coins go to the purse |
| 6 | Smoke is a separate model, `fx_smoke_puff`, that the client loops at `Anchor_Smoke`: puffs rise, grow and shrink to nothing; opaque | Keeps the one shared material; a fading puff would need a transparent one |
| 7 | A road tile is one model per level: a grass tile with a centre patch and four arms `Road_N`, `Road_E`, `Road_S`, `Road_W`; the client shows the arms that point at neighbouring roads | One model per level instead of six shapes in four turns |
| 8 | Ground variety: four grass tiles (`tile_grass`, `tile_grass_b`, `tile_grass_c`, `tile_grass_d`), picked per tile by a fixed hash of its coordinates | Same picture on every device and every run |
| 9 | Triangle budgets: buildings 6,000 (was 4,500), store fill and props 1,500, tiles 700 (was 200), road tiles 1,000, the smoke puff 300 | One more step of detail took the level 2 buildings to 4,900-5,600. A full 8 x 8 town (64 tiles, about 20 buildings) stays under 200,000 triangles with one material and a handful of draw calls; every phone from the iPhone XR on (OD-18) draws that easily |
| 10 | New palette colours go only into free slots (from 20); no slot moves | The art README's rule |
| 11 | The package's change id is `CR-0009`; its CR file is written at adoption | Commits need an id (`.githooks/commit-msg`); a CR file now would edit `docs/changes/README.md`, a shared file. If the main session takes CR-0009 first, the adoption renumbers |
