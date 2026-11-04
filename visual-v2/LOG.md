# Visual v2: build log

- **Status:** Done (the package and its fit test; adoption is a later task)
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
  14 building models, 8 store fill models, 9 item props) built by `blender/build_all.py`, every check passing (tile bounds with every moving
  part along its cycle and the full store, floor, triangle budgets, one material, anchors, poses, fill steps, road arms).

- Step 7 done: `previews/` (lineups, a CR-0008 town, eight GIFs of moving parts with the store filling, a sheet of their frames),
  25 icons (the 15 of v1 drawn again from the new models, 10 new for CR-0008), 8 UI glyphs (`ui-preview/render-glyphs.js`).
- Step 8 done: the client code in `unity/Presentation/City/` and its EditMode tests; `MOTIONS.md` is the contract between the art and
  the client.

- Step 9 done: every UI file reworked after the showcase (`unity/Resources/UI/`: `Common.uss` is the design system; each screen keeps
  every element name, type, hidden class and pointer rule the views use). `ui-preview/preview.js` renders them in a browser over the
  town for portrait and landscape phones (`previews/ui_portrait.jpg`, `previews/ui_landscape.jpg`); `ui-preview/check-ui-rules.py`
  runs the rules the client's UI tests read from the files.

- Step 10 done: the fit test on `claude/visual-v2-fit-test` (`FIT-TEST.md`); the wiring of today's client is `unity-wiring.patch`.
- Step 11 done: `README.md` (contents, pictures, rebuild, adoption), this log.
- 2026-11-04, after the close-out, at the owner's request ("Do it", to the offer of the previews on a page for the phone): a private
  page with every preview, published as an Artifact: https://claude.ai/artifact/B8XjuyHGiaafZ3owaxNzKX. Writing its facts corrected two
  counts in this log and `FIT-TEST.md`: 14 building models and 8 store fill models (not 13 and 9).

## Verification
- `blender -b --factory-startup --python visual-v2/blender/build_all.py -- --no-render`: all 49 models pass every check (Blender 5.2.3,
  Linux, software OpenGL for the renders).
- The new C# (code and tests) compiles against the real engine sources (`shared/Simulation/Runtime`) and thin stubs of the Unity and
  client members it uses, with warnings as errors and XML documentation checked (.NET 10 SDK, C# 9). The 66 tests that need no Unity
  objects pass under NUnit there (motion formula, motion table, store steps, ground variants). That run found and fixed an overflow in
  `StorageFill.StepFor` for limits near `long.MaxValue`.
- `python3 visual-v2/ui-preview/check-ui-rules.py`: every rule of SharedLookTests, MobileLayoutTests and the layout tests passes for the
  new UI files (and for today's, as a check of the script); all 101 element names the views look up exist with their types.
- The browser preview found two layout bugs that Unity's layout engine has too, both fixed: in portrait the inspector's boxes kept the
  `flex-basis: 0` of the side-by-side layout and collapsed in a column; in landscape the build sheet covered the HUD.
- On the fit branch: `check-docs.sh` OK; the UI rules pass on the client's folder; `dotnet test server/tests/CityBuilder.Repository.Tests`
  22 / 22 (the code-documentation rules over every C# file of the Unity project, the new and the edited ones included);
  `build_all.py -- --out client/CityBuilder/Assets/_Project` passes and writes the same models (only the FBX header's time and path
  differ); `git apply --check visual-v2/unity-wiring.patch` applies to `main`'s client.
- Not run here: the tests that build GameObjects (`MotionRigTests`, `FillViewTests`, `BuildingAnimatorTests`, `RoadArmsTests`,
  `ArtModelsV2Tests`) need the Unity Editor.

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
| 8 | Ground variety: four grass tiles (`tile_grass`, `tile_grass_b`, `tile_grass_c`, `tile_grass_d`), picked per tile by a fixed hash of its coordinates (replaced by decision 19) | Same picture on every device and every run |
| 9 | Triangle budgets: buildings 6,000 (was 4,500), store fill and props 1,500, tiles 700 (was 200), road tiles 1,000, the smoke puff 300 | One more step of detail took the level 2 buildings to 4,900-5,600. A full 8 x 8 town (64 tiles, about 20 buildings) stays under 200,000 triangles with one material and a handful of draw calls; every phone from the iPhone XR on (OD-18) draws that easily |
| 10 | New palette colours go only into free slots (from 20); no slot moves | The art README's rule |
| 12 | Fonts of the showcase: Fredoka (titles, buttons, numbers) and Nunito (labels, text), SIL OFL 1.1, as static instances (Fredoka 600, Nunito 700 and 800) made with fontTools from Google Fonts' variable files; the licences sit beside them | Unity's text engine draws one weight per font file. OFL allows this, and neither font has a reserved name |
| 13 | UI glyphs (close, plus, minus, lock, time, done, makes, alert) are white PNGs with the shape in alpha only, tinted in USS | One file per glyph in any colour; white transparent pixels keep a dark fringe out when Unity filters them |
| 14 | Previews are kept as JPEG and optimised GIFs (1.7 MB in all); the one-picture-per-model renders are git-ignored | Git keeps every version of a picture; the renders rebuild in minutes |
| 15 | The UI is reworked in USS and UXML only: every name, type, hidden class and pointer rule the C# views use stays, so the views need no change. Close, minus and plus become tinted glyphs (their texts are static); the Build and Research buttons carry their pictures as backgrounds; the HUD drops its wood bar for dark chips; a portrait inspector is a sheet above the dock | The showcase's look without touching the views that CR-0008-T2 is about to change |
| 16 | `--touch` grows from 48 to 52 and the dock buttons with it (`--dock-lift` 60); the HUD is 40 high in landscape and three rows in portrait (`--hud-height` 52 and 122) | Chunkier buttons like the showcase's; the tests' relations (dock lift = touch + gap, HUD height + 12) still hold |
| 17 | UI Toolkit has no `:first-child`, `:last-child` or `gap`: spacing uses margins on every item and a negative margin on the row | The rule check rejects those selectors |
| 11 | The package's change id is `CR-0009`; its CR file is written at adoption | Commits need an id (`.githooks/commit-msg`); a CR file now would edit `docs/changes/README.md`, a shared file. If the main session takes CR-0009 first, the adoption renumbers |

## Ground, second pass (2026-11-04)
- **Owner input** (in chat, after the preview page): "iterate on the tiles and roads. Make them more detailed, less blend. Add more random
  variety where applicable and feasible".
- **Done:** the ground moved to `blender/assets_ground.py`. Eight grass tiles instead of four, each with soft mottling, tufts in mixed
  greens, short speckles and its own features (lush grass, daisies and poppies, mossy stone clusters, clover, a dry patch with dandelions,
  a ring of mushrooms with ferns, a molehill, bluebells); a quarter turn per tile; three variants of each road level with wobbling sides
  (exact at the tile edges), ruts, edge tufts and pebbles, and per variant a puddle and hoof prints, grass between the ruts, a missing log,
  moss or a loose plank; the log road's logs differ in size, colour and angle; the dirt tile has mottling, wavy ruts and a puddle. The
  client's `GroundVariants` (weights, `RotationFor`, `Hash`) and `RoadArms` (`Variants`, `PathOf(level, variant)`, `VariantFor`) follow,
  with their tests; `GroundTiles` turns each grass tile (`unity-wiring.patch`). 57 models now.
- **Verification:** all 57 models pass `build_all.py`'s checks; the C# compiles against the engine with the Unity stubs and the 82 tests
  without Unity objects pass (new vectors for the variants, the turns and the road variants, computed by the previews' Python); the
  previews, the GIFs and the road icons were rendered again. The fit branch took the second pass (`adopt.sh`, `GroundTiles` turning each
  model tile on top of its own rotation): `check-docs.sh`, the UI rules and `CityBuilder.Repository.Tests` (22) pass there, and
  `unity-wiring.patch` is its diff again (`git apply --check` on `main` passes).

| # | Decision | Why / alternative |
|---|---|---|
| 19 | Eight grass tiles with weights per 20 tiles (plain 5, clover 3, the others 2) and a quarter turn per tile from other bits of the same hash: 32 looks | The plain tile stays the most common so the town is calm; turns cost nothing because the tiles are square and their details have no direction |
| 20 | Three variants per road level, picked per tile by the hash's top bits; the arms keep the same width and rut places at the tile edge | Any two variants join, so the client needs no rule about neighbours' variants. Roads are not turned (their arms point somewhere) |
| 21 | Three colours in free slots: `grass_warm`, `grass_cool` (mottling a step off the grass) and `grass_dry` (the dry patch) | Low contrast patches make the ground less flat without reading as holes; the first dry patch in thatch yellow looked like a stain |
| 22 | Budgets: tiles 1,000 triangles (the richest is 902), road tiles 2,000 (the log road's variants 1,574 to 1,796) | 64 tiles stay under 60,000 triangles; a town's 15 or so road tiles add about 30,000 |
| 23 | Flat patches are clipped to the tile's flat top (0.465 from the centre) | Patches that reached past the edge painted over the grid line between tiles (also in the first pass) |
| 24 | A grass or dirt tile is two meshes: the flat ground (top at y = 0) and a child `Details` with everything that rises above it; `build_all.py` and `ArtModelsV2Tests` check it | `GroundTilesTests.Tiles_StandOnTheirTiles_WithTheirTopAtTheGround` reads the tile's first renderer and wants its top at 0; with tufts in the same mesh it would fail (it would have with the first pass too). The split keeps that test as it is, and placement hints now tint only the ground, not the flowers. Cost: two renderers per tile, one material |

## Adoption instructions (2026-11-04)
- **Owner input** (in chat): "make a clear instruction for AI on how to implement these new graphics, after the finish on CR-0008".
- **Done:** `ADOPT.md`: the preconditions, the CR text to approve, a script that sorts every overlay file into new, take, merge and
  same-name-on-both-sides (tried against the CR-0008-T1 branch: its five placeholder models and five icons come out as the last group),
  the art merge into `art/blender/`, the UI merge, the wiring, the hooks into CR-0008 (building stores, when the new kinds work, roads,
  walkers, icons), tests, docs, removing this folder and the [MANUAL] looks. `README.md` "Adopting it" now points there.

## Follow-ups
- [MANUAL] The Unity steps of `FIT-TEST.md`: compile, `test-unity.sh`, a play session for the motion and the fill, the UI in the Device
  Simulator (fonts: if UI Toolkit draws the TTF files in its default font, make Font Assets of them).
- With CR-0008-T2: read the building's own store in `BuildingDecor.Show`; decide when the Scriptorium, Courier Station and Warehouse
  work (`BuildingDecor.IsWorking`); draw roads with `RoadArms` and walkers with `MotionRig`; new screens (routes, trips) use the tokens
  and classes of `Common.uss`.
- The showcase's Market button (locked until Era II) and its welcome-back shortcut were left out: both need view code, not only layout.
- Owner: whether `CR-0009` stays this package's number, and whether `--touch` 52 (the chunkier buttons) is wanted everywhere.
