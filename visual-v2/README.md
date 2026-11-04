# Visual v2: Era I models, animations and UI, staged for later use

A self-contained package of new art and UI for the CityBuilder client. It lives in this folder so that it does not touch the
engine, the server, the Unity project or the shared docs while the main session builds CR-0008 (logistics). Nothing in the game
reads this folder. A later task adopts it (see "Adopting it"), and the branch `claude/visual-v2-fit-test` shows it applied to
today's client.

## The owner's request (2026-11-04, in chat, quoted as written)
> Make all needed 3d models. Improve detalization by one step, make it more detailed. Don't forget to make improved grass tiles, not
> only buildings and items. Also rework and improve UI a lot. Test the fit on a separate branch, and put everything in a separate folder
> for later use, so this session do not interfere with main engine building session. Use "CityBuilder UI showcase" artifact as a
> reference for UI. Don't forget to animate the buildings when they produce something. If building's internal warehouse is filling, it
> should be seen in the model (this do not interrupt the general animation cycle of the building). 1-3 steps of filling - it's your choice
> for every building.

## What "all needed models" means here
- **Today's set, one step more detailed:** Cottage, Woodcutter's Hut, Sawmill, Trading Post (levels 1 and 2), the construction
  site, ground tiles, trees, bush, rock, the worker and the item props (logs, planks, coins).
- **What CR-0008 adds** (`docs/changes/CR-0008-logistics-rework.md` on the branch `task/CR-0008-T1-logistics-engine-server`, content
  `mvp-2`): the Scriptorium (`scriptorium`, level 1), the Courier Station (`courier_station`, levels 1 and 2), the Warehouse
  (`warehouse`, levels 1 and 2), road tiles for the dirt path (level 1) and the log road (level 2) that join their neighbours, and a
  courier with a pack. CR-0008-T2 part F asks for exactly these, so it can take them from here.
- **New for this package:** grass tile variants, store fill models, smoke puffs, glyphs and icons for the new buildings and research.

## Layout
| Path | What |
|---|---|
| `blender/` | The Blender 5.2 sources: a fork of `art/blender/` that writes into `unity/` instead of the Unity project |
| `unity/` | A drop-in overlay of `client/CityBuilder/Assets/_Project/`: the FBX models, the palette, UI files, icons, fonts and client code |
| `previews/` | Renders of the models, the town, the animations and the store fill steps |
| `ui-preview/` | A browser preview of the reworked UI files (an approximation of UI Toolkit, for looking without Unity) |
| `LOG.md` | This package's build log: plan, decisions, verification |

## Status
In progress. The plan and every decision are in `LOG.md`.
