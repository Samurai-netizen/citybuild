# Adopting visual v2: instructions for the AI coder

This page is for the AI that brings this package into the game. The owner asked for it on 2026-11-04: "make a clear instruction for AI
on how to implement these new graphics, after the finish on CR-0008", then, to save AI usage, to adopt it while CR-0008 is still being
built instead of making more temporary art. So there are two routes: **A**, inside CR-0008-T2 (use it if T2 has not started), and **B**,
a separate task after CR-0008 is Done (only if T2 was built without the package).

**What the package is.** It has more detailed models for every Era I building, prop and ground tile, plus the CR-0008 buildings, roads
and courier. It also has moving parts and smoke while a building works, store fill models, a reworked UI and client code with tests
(`README.md`). It was made against `main` at `3c0dcac` (M0-60). Call that **the base** below. Nothing in the game reads this folder yet.

**The rule on both routes:** CR-0008's behaviour, names and element lookups win; the package's looks win.

## Route A (recommended, cheaper): inside CR-0008-T2
Checked on 2026-11-04 against `task/CR-0008-T1-logistics-engine-server` (T1 at part H; its parts H and I touch no art or client view):
`adopt.sh` copies cleanly, `unity-wiring.patch` applies (`git apply --check`), every UI rule passes against T1's views, and the package's
C# compiles against T1's engine except two lines of `BuildingDecor.Show`. Adopting at the start of T2 therefore costs a few steps,
and it saves: T2 part F's modelling (every model, road and icon it asks for exists here), building T2's screens twice (placeholder look,
then restyle), the road and walker placeholders, and the whole separate task of Route B (merge, second tests, second docs, second
[MANUAL] session). The owner's instruction at `/start-task CR-0008-T2` decides it; the T2 session records it as a decision and edits
T2's part F in the CR in its own PR.

**Owner's prompt for T2:** "Adopt visual v2 in this task, Route A of `visual-v2/ADOPT.md` (folder on branch
`claude/compassionate-meitner-sd7nr0`). Part F makes no new models: it only fills gaps."

Steps for the T2 session (after T1 is merged):
1. `git checkout origin/claude/compassionate-meitner-sd7nr0 -- visual-v2` (the folder only, not the branch).
2. Before part A, as its own checkpointed step:
   - `bash visual-v2/adopt.sh`. It replaces T1's placeholder models (`courier_station_l1/l2`, `scriptorium_l1`, `warehouse_l1/l2`) and
     icons (`bld_*` of those three, `res_tally_sticks`, `res_sturdy_boots`) on purpose. `git apply visual-v2/unity-wiring.patch`. If a
     file changed since 2026-11-04 and the patch fails, redo that edit by hand (§3.3 below).
   - `BuildingDecor.Show`: replace the two lines that read `city.Inventory` and `CityQueries.TryGetEffectiveCap` with the building's
     own store: `building.Store.TryGetValue(item, out long amount)` and
     `CityQueries.TryGetEffectiveLimit(city, content, building, item, out long limit)` (0 when the store doesn't hold it: no fill).
     Update its comment and add a test.
   - `BuildingDecor.IsWorking`: the rules of §3.4 below for the Scriptorium, the Courier Station and the Warehouse, with tests.
   - Move `visual-v2/blender/` into `art/blender/` (§2 below). The package's `courier_station`, `scriptorium`, `warehouse` and the two
     research icons replace T1's versions of the same names; port only what T1 added that the package lacks.
   - Unity import, commit the new `.meta` files, `test-unity.sh` green, `check-ui-rules.py` passes.
3. Parts A to E: build every new screen in the new look from the start (`Common.uss` tokens and classes, §3.2). Part B draws roads with
   `RoadArms` and the road models (§3.4), with no placeholder. Part D's walkers are the `courier` and `worker` models with `MotionRig`
   and their loads at `Anchor_Load` (§3.4).
4. Part F: no new modelling. Check that every building type, level and road level in `server/content/mvp-content.json` has its model
   and icon (on 2026-11-04 they all do), merge the lookup tests with `ArtModelsV2Tests`, and run the Blender build only if a model
   changes.
5. Docs once (§5), then delete `visual-v2/` before `/finish-task` (§6). T3's playtest includes the looks of §7.

Route B below is the fallback when T2 has already been done without the package.

## Route B: after CR-0008 is Done
CR-0008 changed the engine (goods in building stores), the client (new screens, roads, walkers) and `art/blender/` after the base, and
added simpler models and icons with the **same file names** as this package. So you **merge** the package: a plain copy would undo
CR-0008's client work. Sections 0 to 7 are this route; Route A uses some of them by number.

## 0. Before you start
1. **Route B only: CR-0008 must be Done.** Check that its `- **Status:**` line in `docs/changes/CR-0008-logistics-rework.md` says
   Done and that T3 is merged into `main`. If T2 has not started, use Route A instead.
2. **Machine:** Unity 6000.3.25f1, Blender 5.2, the .NET 10 SDK, Docker, Python 3, ImageMagick, and Node.js with Playwright (only for the
   UI preview). Normally this is the owner's laptop. A cloud session can't run Unity, so it can't finish this task.
3. **A change request.** This work needs an approved CR (`02` rule 17).
   - Use the next free number in `docs/changes/`. The package reserved `CR-0009`; if that number is taken, use the next one. The old
     `CR-0009` commit subjects in this folder stay as they are.
   - Write the CR with `/change-request`, using "The CR text" below. The owner approves it.
   - Then run `/start-task CR-NNNN-T1` (model: Opus, `[O]`).
4. **Read:**
   - in this folder: `README.md`, `MOTIONS.md` (the art–code contract), `LOG.md` (decisions 1 to 24), `FIT-TEST.md`, `unity-wiring.patch`;
   - elsewhere: `art/README.md`, `core-instructions/09-unity-client.md`, `client/README.md`, and CR-0008's build log entries for T2 and
     T3 (what they named things, and what they left for later).

## The CR text (paste into `/change-request`)
```text
Adopt visual v2 (visual-v2/, made on 2026-11-04 at the owner's request) into the game after CR-0008: the more detailed Era I models,
eight grass tiles with quarter turns, three variants per road level, moving parts and smoke while buildings work, store fill models
(1-3 steps per item), the courier with arm and leg motion, the reworked UI (Fredoka and Nunito, Common.uss tokens and classes),
25 icons and 8 glyphs. Merge into what CR-0008 built; keep its behaviour and names. One task, T1 [O], on a machine with Unity and
Blender, following visual-v2/ADOPT.md part by part. Removes visual-v2/ at the end (its sources move to art/). No engine, server or
API change. No new dependency (the fonts are SIL OFL 1.1 files, not packages).
```

## 1. Find what changed on both sides
Run this from the repository root. It sorts every overlay file into one of four groups:
```bash
base=3c0dcac; dst=client/CityBuilder/Assets/_Project
(cd visual-v2/unity && find . -type f | sed 's|^\./||') | while read -r f; do
  if ! git cat-file -e "HEAD:$dst/$f" 2>/dev/null; then echo "NEW      $f"
  elif git diff --quiet "$base" HEAD -- "$dst/$f"; then echo "TAKE     $f"
  elif git cat-file -e "$base:$dst/$f" 2>/dev/null; then echo "MERGE    $f"
  else echo "BOTHNEW  $f"; fi
done | sort > /tmp/visual-v2-classes.txt
```

| Group | Meaning | What to do |
|---|---|---|
| `NEW` | Only the package has it | Copy it |
| `TAKE` | `main` has not changed it since the base | Copy the package's version |
| `MERGE` | Both sides changed it | Merge by hand (part 3) |
| `BOTHNEW` | CR-0008 added a file with the same name (for example `courier_station_l1.fbx`, `bld_warehouse.png`) | For models and icons, take the package's version once part 2 has rebuilt it. For code, merge by hand |

Do the same for `art/blender/`. Compare `git diff 3c0dcac HEAD -- art/blender` (CR-0008's art) with `visual-v2/blender/` (the
package's art).

Write the list into the build log, one decision row per `MERGE` and `BOTHNEW` file.

## 2. Art sources and models
1. **Move the sources into `art/blender/`.**
   - `assets_ground.py` is new. `palette.py`, `lib.py`, `assets_common.py`, `assets_era1.py`, `build_all.py`, `render_previews.py` and
     `render_ui.py` replace their `art/` versions.
   - Port CR-0008's additions that the package lacks: any model, level or icon in `art/blender` that is not in the package. Pick
     everything up from `server/content/mvp-content.json`: every building type and level needs a model, and every road level needs
     three variants.
   - The same name on both sides means the package's model wins (the owner asked for this detail). First check it still has every child
     or anchor that CR-0008's client code looks up.
2. **Paths:**
   - `build_all.py`'s `--out` default becomes `client/CityBuilder/Assets/_Project`.
   - Previews go to `art/previews/`. Keep git's ignore rule for the one-picture-per-model renders.
   - The palette keeps its slots: new colours only go into free slots (LOG decision 10). If CR-0008 added colours, give them free
     slots that don't collide with the package's 14.
3. **Run `blender -b --factory-startup --python art/blender/build_all.py`.** Every check must pass: bounds, floor, triangle budget,
   one material, anchors, poses, fill steps, the flat tile top, road arms.
4. **The build rewrites every FBX header (time and path).** Before you commit, restore the FBX files whose models did not change:
   `git checkout -- <file>`.
5. **Run `render_ui.py` only for icons that changed.** Its renders are not byte-identical between runs.
6. **Move `MOTIONS.md` to `art/MOTIONS.md`** and link it from `art/README.md`. Update `art/README.md`: the models, the ground and road
   rules, the budgets and the commands.

## 3. Client: copy, merge, wire
1. **Copy** the `NEW` and `TAKE` files into `client/CityBuilder/Assets/_Project/`.
   - Don't run `adopt.sh` as is: it overwrites `MERGE` files.
   - Existing files keep their `.meta`. New ones get theirs on Unity's import; commit them.
2. **UI (`Resources/UI/`).** The package restyled the layouts that existed at the base. CR-0008-T2 added screens and elements (road mode,
   trip flow, route editor, courier station inspector, stores in the inspector, the welcome-back report).
   - For each `MERGE` layout, keep every element name, type, hidden class and pointer rule the C# views use now. Then apply the package's
     classes and tokens.
   - Restyle CR-0008's new screens with `Common.uss`. Its header comment explains the tokens, the `.portrait` class and the scale.
     Its classes include `.sheet`, `.panel-parchment`, `.panel-wood`, `.chip-dark`, `.btn` (`--primary`, `--gold`, `--danger`, `--quiet`,
     `--round`, `--dock`), `.ribbon`, `.t-title`, `.t-heading`, `.t-body`, `.t-number`, `.t-note`, `.glyph-*` and `.themed-scroll`.
   - A screen stylesheet uses tokens only, never its own colour or font size (`SharedLookTests`).
   - UI Toolkit has no `:first-child`, `:last-child`, `gap` or `box-shadow` (LOG decision 17).
   - Check with `python3 visual-v2/ui-preview/check-ui-rules.py client/CityBuilder/Assets/_Project/Resources/UI`. It reads the element
     names from the current views.
3. **Wiring.** `unity-wiring.patch` won't apply any more, so redo its five edits on the current files. The patch shows what each edit
   does (`FIT-TEST.md` lists them):
   - `BuildingView`: `Animator` and `Fill`.
   - `BuildingViewFactory`: `BuildingDecor.Attach` when a body is made.
   - `CityView`: `BuildingDecor.Show` once a second.
   - `GroundTiles`: the grass variant and its quarter turn.
   - `ArtImportPostprocessor`: glyph settings.
4. **Hook the package up to what CR-0008 built.** Log a decision for each item.
   - **Store fill:** `BuildingDecor.Show` still reads the city's stock against its cap (the base had nothing else). Change it to read the
     building's own store and its per-item limit, using the engine's store query. Find it in `shared/Simulation/Runtime/` and don't
     re-implement it.
     - Steps come from `StorageFill.StepFor(amount, limit, steps)`.
     - If an item has no limit, show no fill for it.
     - The Warehouse shows logs and planks.
   - **When a building works:** `BuildingDecor.IsWorking`. Recommended rules:
     - a construction site hammers only while its build timer runs, not while it waits for goods;
     - a producer works while a cycle runs and nothing stalls it;
     - a cottage always works (smoke);
     - the Scriptorium works while a research timer runs;
     - the Courier Station works while at least one of its couriers is on a walk;
     - the Warehouse works while a walker is on its way to or from it.
   - **Roads:**
     - Each road tile shows `Resources.Load(RoadArms.PathOf(level, RoadArms.VariantFor(x, y)))` instead of its grass tile.
     - Each road tile calls `RoadArms.Show(road, xPlus, xMinus, yPlus, yMinus)` with whether each neighbour is a road. Call it again
       whenever a road is placed, upgraded or removed next to it.
     - Roads are never turned. Placement hints tint the tile's first renderer, as for grass.
     - Replace CR-0008's placeholder road drawing. Keep its road-mode logic.
   - **Walkers:**
     - Couriers use `Models/common/courier`; trip workers use `Models/common/worker`.
     - Get the rig with `MotionRig.Find(body)`. Call `Advance(deltaSeconds, intensity)` with the intensity easing to 1 while walking and
       to 0 while standing (as `BuildingAnimator` does).
     - A load hangs at `Anchor_Load`: `carry_logs` or `carry_planks`. Coins have no carry model, so keep CR-0008's icon for them.
     - Keep T2's pooling. Don't allocate per frame.
   - **Icons:**
     - Map the package's names (`bld_courier_station`, `bld_scriptorium`, `bld_warehouse`, `road_l1`, `road_l2`, `trips`, `couriers`,
       `reenergize`, `res_*`) to the constants in `Presentation/Ui/UiIcons.cs`.
     - If CR-0008 used other names, rename one side and log it.
   - **Glyphs** (close, plus, minus, lock, time, done, makes, alert) are tinted in USS. Use them where CR-0008's screens draw these
     shapes with text.

## 4. Tests
- **Copy the package's tests** (`Tests/EditMode/Art/ArtModelsV2Tests.cs`, `Tests/EditMode/Presentation/City/*`).
  - Merge `ArtModelsV2Tests` with CR-0008's art lookup tests, keeping both sides' checks.
  - Add cases for every model CR-0008's content needs.
- **New rules get new tests:**
  - `BuildingDecor.Show` reading a building store;
  - `IsWorking` for the three new kinds;
  - road tiles choosing a variant and showing arms;
  - walkers driving `MotionRig`.
- **Never weaken, skip or delete a test to get green** (`02` rule 15). A test whose rule this CR changes is rewritten, with its reason in
  the build log.
- **The full verification:**
  - `dotnet format` (verify), the build and the fast tests;
  - `UNITY_EDITOR=<Unity program> bash scripts/dev/test-unity.sh` with the Editor closed (EditMode and PlayMode green);
  - `bash scripts/dev/check-docs.sh` and `bash scripts/dev/docs-site.sh build`;
  - the UI rule check.

## 5. Docs (rows in the Docs impact table)
- `art/README.md` and `art/MOTIONS.md`.
- `docs/modules/CityBuilder.Client.Presentation.md`: the new types `MotionRig`, `PartMotions`, `BuildingAnimator`, `BuildingDecor`,
  `FillView`, `StorageFill`, `SmokePuffs`, `GroundVariants` and `RoadArms`.
- `core-instructions/09-unity-client.md`: the art and UX rules (one material, moving parts by name, fill models, tokens only).
- `client/README.md`: the [MANUAL] looks.
- `docs/how-to/add-ui-screen.md`: the tokens and classes of `Common.uss`.
- `13-progress.md` and the M0 board, as for every task.

## 6. Remove the package
In the same PR, delete `visual-v2/` once nothing reads it:
- the sources are in `art/blender/`;
- the contract is in `art/MOTIONS.md`;
- the pictures worth keeping are in `art/previews/`;
- `ui-preview/` moves to `art/ui/preview/`, with its default paths pointed at the Unity project.

This keeps one home for each fact. Remove the `claude/visual-v2-fit-test` branch only if the owner says so.

## 7. [MANUAL] with the owner
`FIT-TEST.md`'s "Not checked here" list, extended for CR-0008:
- The Unity import has no `error CS`.
- Walk a play session against the dev server: parts move while working and settle when stalled, smoke, store fill growing with each
  building's store, grass varying tile to tile, roads joining with their variants, couriers swinging their limbs and carrying loads.
- Check the UI in the Device Simulator: an iPhone XR and a newer phone, both orientations, every screen including CR-0008's. If the text
  falls back to the default font, make Font Assets from the three TTF files.

Each problem becomes a P0–P3 issue in the build log. Fix P0 and P1 with regression tests.

## Known traps (from `LOG.md`)
- A grass or dirt tile is two meshes: flat ground with its top at y = 0, and a `Details` child. `GroundTilesTests` reads the first
  renderer's top (LOG decision 24).
- `StorageFill`'s thresholds must not overflow for huge limits; keep its formula.
- Glyph PNGs are white with the shape only in alpha. Don't re-save them with black transparent pixels.
- In C#, don't use `??` or `?.` on Unity objects; use explicit null checks.
- Budgets:
  - buildings 6,000 triangles;
  - tiles 1,000;
  - road tiles 2,000;
  - an 8 × 8 town's ground stays under about 60,000 triangles, roads add about 30,000.

  Measure the frame time on a phone in the [MANUAL] session.
