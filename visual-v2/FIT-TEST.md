# Fit test: visual v2 applied to today's client

This branch, `claude/visual-v2-fit-test`, is the package of `visual-v2/` applied to the client of `main` (M0-60), to see that it fits. It
is a test bed: it is not meant to be merged as is, because CR-0008-T2 changes the same client files first.

## What changed against `main`
- `bash visual-v2/adopt.sh`: the overlay `visual-v2/unity/` copied into `client/CityBuilder/Assets/_Project/`: 57 models (22 replaced,
  35 new, 8 of them store fill models), the palette, 25 icons, 8 glyphs, 3 fonts with their licences, every UI layout and stylesheet,
  11 new scripts and 9 new test files.
- The wiring, five files of today's client (`visual-v2/unity-wiring.patch` on the package branch is the same change):
  - `BuildingView`: `Animator` and `Fill`; the wheel's comment.
  - `BuildingViewFactory.Refresh`: a new body of a model gets its moving parts, smoke and store fill (`BuildingDecor.Attach`).
  - `CityView.ShowStatus`: once a second, whether each building works and how full its store is (`BuildingDecor.Show`).
  - `GroundTiles`: each tile is its grass variant (`GroundVariants`), turned by its quarter turns, the plain grass tile where one is
    missing; the test constructor with one model is unchanged.
  - `ArtImportPostprocessor`: the glyphs get the icons' import settings.

## Checked here (cloud session, no Unity)
- `bash scripts/dev/check-docs.sh`: OK.
- `python3 visual-v2/ui-preview/check-ui-rules.py client/CityBuilder/Assets/_Project/Resources/UI`: every rule that SharedLookTests,
  MobileLayoutTests and the layout tests read from the files passes; all 101 element names the views look up exist with their types.
- `dotnet test server/tests/CityBuilder.Repository.Tests`: 22 / 22 (the code documentation rules over every C# file of the Unity project).
- `blender -b --factory-startup --python visual-v2/blender/build_all.py -- --no-render --out client/CityBuilder/Assets/_Project`: all
  checks pass and the models land where the client loads them; the files differ from the overlay's only in the FBX header (creation time
  and file path), so the overlay copies are kept.
- The new scripts and tests compiled against the real engine with stubs for Unity (see `LOG.md`); the 82 tests without Unity objects pass.

## Not checked here: [MANUAL] on a machine with Unity 6000.3.25f1
1. Open `client/CityBuilder` on this branch and let Unity import: no `error CS` in the Console; the new FBX, PNG and TTF files get their
   `.meta` files (commit them if this branch is kept).
2. `UNITY_EDITOR=<Unity program> bash scripts/dev/test-unity.sh` (Editor closed): EditMode and PlayMode green, the new tests included
   (`MotionRigTests`, `FillViewTests`, `BuildingAnimatorTests`, `RoadArmsTests`, `ArtModelsV2Tests` and the pure ones).
3. Play against the dev server: the Sawmill's wheel turns and its saw strokes while it produces, the Woodcutter's axe chops, the Trading
   Post's coin sign spins and its flag waves, cottages and the Woodcutter's Hut smoke; a stalled building settles at rest; the log pile,
   plank stacks and goods for sale grow with the stock; the grass varies tile to tile (eight tiles in four turns).
4. The UI in the Device Simulator (an iPhone XR and a newer phone, both turns): the fonts are Fredoka and Nunito (if the text falls back
   to the default font, make Font Assets of the three TTF files with Window > Text > Font Asset Creator and point the stylesheets at
   them); the HUD chips, the dock, the build and research sheets, the inspector, the welcome-back card, the toasts and the placement bar
   look like `visual-v2/previews/ui_portrait.jpg` and `ui_landscape.jpg`.
