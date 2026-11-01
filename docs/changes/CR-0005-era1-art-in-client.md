# CR-0005 — Era I art in the client: models, palette and UI icons

- **Status:** In progress
- **Type:** Feature
- **Requested by:** owner · **Date:** 2026-11-01
- **Milestone / era:** M0 / Era I
- **Conflicts with pillars (`22`) or the era rule (`25`):** none. Changes `09` rule 3 ("placeholder art from primitives in M0"), which is
  not a protected file (`15`) but is the owner's call.

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Problem / goal
The M0 client draws every building as a box made from primitives (`BuildingLooks`, `PlaceholderArt`, M0-44) and the HUD has no
icons. The owner has made a set of Era I art (Blender scripts, FBX models, a palette texture, 15 UI icons, renders; 12 MB) and
moved it into the repository folder as `citybuild-assets/` (untracked). The owner wants it used to build a prototype that looks
like the game in `21` → "The look" (toy-block low poly, warm palette), so the M0 vertical slice can be judged on real art.
Doing it before M0-50 (building status visuals) lets that task use the models' anchors and the construction-site model instead of
building throwaway placeholders.

## Proposal
Bring the art into the repository and use it in the client, in small steps (the tasks are written after approval):

1. **Adopt the folder.** `citybuild-assets/` becomes a tracked top-level folder with a README that is a handbook page
   (the style guide and the rebuild command are already written in its README). **Decided by the owner:** it is named `art/`.
2. **Import into Unity** under `client/CityBuilder/Assets/_Project/Art/`: the 14 Era I and 7 common FBX files, the palette texture
   and one URP Lit material. Import settings are applied by an **editor script** (an `AssetPostprocessor`), not by hand
   (`09` rule 1): palette texture Point filter, no compression, no mips, Clamp; models scale 1, no cameras or lights, one
   shared material.
3. **Draw buildings from the models.** `BuildingLooks` keeps its `(typeId, era)` lookup (`09` UX rules) and gets the level;
   a lookup returns the model for `(typeId, era, level)`, the construction-site model while a building is built or upgraded
   (replacing today's "low foundation"), and the existing primitive as the fallback for a type without a model. Ground tiles use
   `tile_grass` / `tile_dirt`. A view finds `Anchor_Status`, `Anchor_Door`, `Anchor_Smoke`, `Anchor_Stockpile` and `Wheel` by name.
   The look stays display-only: views never call the server, and nothing finished shows before the server says so.
4. **UI icons.** The HUD chips (coins, logs, planks), workers, builders and research use `ui/icons/`; the build menu cards use
   the `bld_*` icons and the research screen (M0-52) the `res_*` icons. They are loaded by name from `Resources/UI/` (decision 024).
5. **Effect on M0-50.** After this CR, M0-50 keeps its text and builds on the models: the ring and bubble sit on `Anchor_Status`,
   the scaffold is the construction-site model, the stalled tint is a per-renderer property on the shared material (no material
   copies per building). Only the "scaffold placeholder" wording of the task changes; the board row stays.
6. **Not in this CR:** the worker pips, walking workers, smoke, stockpile props that grow with stock and the turning sawmill
   wheel need animation or extra logic. They are proposed as follow-up tasks (M0-50 covers pips only).

The UI showcase (`ui/showcase/`, mock-ups of the screens) and `ui/city_phone.jpg` / `hotspots.json` are design references for the
screen tasks (M0-51 to M0-53); they are kept in the folder but no code reads them.

## Acceptance criteria
- [ ] The art folder is tracked, and `bash scripts/dev/check-docs.sh` and the handbook build pass with its README as a page.
- [ ] Unity imports every model and icon without errors under Unity 6000.3.25f1 (`test-unity.sh` green; compile check clean). This
      has not been checked yet (the README says so).
- [ ] In the running city, each of the four buildings shows its model at level 1 and level 2, a building under construction or
      upgrade shows the construction site, and the ground shows the grass tile.
- [ ] A unit test fails if a building type of the content (`server/content/mvp-content.json`) has no model for any level it can reach
      or if a model lacks `Anchor_Status` / `Anchor_Door`.
- [ ] An unknown type or a missing model still draws the grey primitive (no exception).
- [ ] The HUD chips show the coin, log and plank icons; the build menu cards show the building icons.
- [ ] `CityView` allocates nothing on a call where nothing changed (the M0-44 test still passes), and views use one shared
      material (draw calls stay low; checked in a PlayMode test).
- [ ] `09` (rule 3, the City view paragraph, the art lookup), the module doc `CityBuilder.Client.Presentation.md`, `client/README.md`
      and the art README describe the result, and every Docs impact row of each task is closed.

## Out of scope
- Animation, walking workers, smoke, growing stockpiles, day/night (later tasks).
- Era II or later art; sound; new Blender models (the folder's README lists the next art).
- Choosing a licence (OD-8) and Git LFS (`.gitattributes` says it waits for M1; see Risks).
- Any change to the server, the engine, the API or the content JSON.

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | none | none |
| Content (`23`, JSON) | none: model names already use the content's type ids | none |
| API (`05`) | none | none |
| Data / migration (`06`) | none | none |
| Security (`07`) | none: art is display-only and the client stays untrusted | none |
| Client (`09`) | `BuildingLooks`, `BuildingViewFactory`, `BuildingView`, `GroundTiles`, `PlaceholderArt` (fallback), HUD and build-menu views, an editor import script, the `_Project/Art/` folder and `.meta` files; rule 3 and the City view paragraph change | `client/CityBuilder/Assets/_Project/Presentation/**`, `Assets/_Project/Art/**`, `09` |
| Tooling | Blender 5.2 stays an art tool on the artist's machine; CI and the game build do not need it. The Unity import script runs in the Editor | the art README |
| Docs (recipes in `19`) | new folder area, new page, a changed rule | the art README as a handbook page (`website/docs-set.js`, `website/sidebars.js`), `docs/getting-started.md`, `docs/README.md`, `docs/modules/CityBuilder.Client.Presentation.md`, `client/README.md`, `AGENTS.md` repository map (protected: ask first), `09`, `CHANGELOG.md` |
| Tests (`08`) | Unity EditMode tests for the model lookup and the anchors; a PlayMode test for the scene; the existing no-allocation test stays | `client/CityBuilder/Assets/_Project/Tests/**` |

## Risks and alternatives
- **Unity import is unchecked.** The FBX files were only re-imported in Blender. A step may need the Editor (materials, axes,
  scale); those steps are written as **[MANUAL]** instructions (`09` rule 4) and the first task starts with one model.
- **Binary files in git.** 12 MB today (11 MB of it renders in `previews/`) and every re-render adds that again.
  **Decided:** track the sources, texture, icons and README, and of `previews/` only `city_closeup.png` and
  `city_golden_scenario.png`; the other renders are rebuilt in 15 seconds and are git-ignored. LFS is decided in M1.
  (The owner approved the CR without naming `previews/`; this follows my recommendation, and the owner can change it.)
- **One copy of the FBX files.** **Decided by the owner:** the models live only in the Unity project
  (`client/CityBuilder/Assets/_Project/Art/`). The Blender build writes there directly, and `art/` keeps only the sources, icons,
  texture source, kept previews and README.
- **Stalled tint on a shared material.** Needs a per-renderer property, not a material copy per building; written into M0-50.
- Alternative: keep primitives until M0 closes. Rejected by the owner: the prototype should look like the game.
- Alternative: a free asset pack. Rejected before (`21` asks for original designs; licences need the owner, OD-8).

## Tasks (filled in after approval)
| ID | Title | Model | Status |
|---|---|---|---|
| CR-0005-T1 | Adopt `art/`: move the folder, point the Blender build at the Unity `Art/` folder, ignore the other previews, README as a handbook page | S | Done |
| CR-0005-T2 | Import one building end to end: editor import script (texture, model, shared URP material) and a **[MANUAL]** Editor check | O | In progress |
| CR-0005-T3 | All models by `(typeId, era, level)`, construction site, ground tiles, anchors, fallback, tests | S | Todo |
| CR-0005-T4 | UI icons in the HUD, the build menu and (placed for M0-52) the research screen | S | Todo |

Order: T1 → T2 → T3 → T4, then M0-50. T2 is `[O]` because it is the first Unity import and the settings may need judgment.

## Owner approval
- Approved on: 2026-11-01: "approved, use art/ and one copy of the FBX files".
- Requested on 2026-11-01 in chat: "I moved citybuild-assets folder to citybuild folder. It has some graphics examples, ui, 3d
  models, color pallete, etc. You can use it to help you build a prototype MVP", and, asked how to proceed, the owner chose
  "CR first, then M0-50 with models".
