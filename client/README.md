# Unity client

The Unity project is `client/CityBuilder/` (Unity **6000.3.25f1**, 6.3 LTS, Universal 3D template, URP). The rules are in
`core-instructions/09-unity-client.md` and `.claude/rules/unity.md`; the module docs are `docs/modules/CityBuilder.Client.*.md`.

## Open it
Unity Hub → **Add** → `client/CityBuilder` → editor 6000.3.25f1. The first import takes a few minutes. The Console should show no red
errors. Since M0-37 `Net` has scripts, so the Console shows no "will not be compiled" warning for it any more.

## Layout
```
client/CityBuilder/
├── Assets/_Project/
│   ├── Core/            CityBuilder.Client.Core           client state, display prediction (skeleton)
│   ├── Net/             CityBuilder.Client.Net            API client, DTOs, error parsing (M0-37); login, token stores, content loading (M0-38)
│   ├── Presentation/    CityBuilder.Client.Presentation   GameRoot, bootstrap, startup flow, status screens; City/ (grid, camera, ground, M0-43; building views, M0-44), Build/ (build menu, M0-45; placement flow and bar, M0-46), Feedback/ (error messages and toasts, M0-47), Ui/ (safe area, M0-45)
│   ├── Editor/          CityBuilder.Client.Editor         Editor only: the art's import settings and shared material (`ArtImportPostprocessor`, CR-0005-T2)
│   ├── Art/             Resources/Models/ (the FBX files: the only copy, written by `art/blender/build_all.py`; under `Resources` so a player build has them and the client loads them by name), Textures/ (the palette), Materials/ (the one shared material); the city view draws them since CR-0005-T3 (`art/README.md`)
│   ├── Resources/       UI/ (PanelSettings.asset, UXML and USS the code loads by name), Config/ApiClientSettings.asset (server URL, timeout, dev player name)
│   └── Tests/EditMode · Tests/PlayMode                    test assemblies (AppInfo, engine-in-Unity and Bootstrap scene tests, M0-05; Net/ tests, M0-37); Tests/Fixtures/ real server answers
├── Assets/Scenes/       Bootstrap.unity (made by hand, scene 0 in the build list; steps below); SampleScene is the template's, unticked
├── Packages/manifest.json   "com.citybuilder.simulation": "file:../../../shared/Simulation" (relative to Packages/), Newtonsoft 3.2.2, Input System 1.20.0, the module com.unity.modules.unitywebrequest
└── ProjectSettings/     Force Text, Visible Meta Files, Input System, HTTP allowed in development builds
```
Commit every `.meta` file. `client/CityBuilder/.gitignore` covers the generated folders.

## Packages
Only what `core-instructions/14` approves, plus the template's own packages. A new project can arrive with packages nobody asked for (three
pre-release AI packages were in ours and are not in the editor's own template; the owner had them removed on 2026-10-14, decision 025): that is not an approval, and `ClientAssemblyTests` fails if one of
those three returns. A built-in module (`com.unity.modules.*`) is enabled only when the manifest or a dependency asks for it: the manifest
names `com.unity.modules.unitywebrequest` for `ApiClient` (M0-37). If code stops compiling with "type or namespace does not exist" for an
engine type, add the module that holds it to the manifest, then run the headless check below.

## Check that it compiles without the Editor GUI
Close the Editor first (a project can be open once). `<UNITY>` is `/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity`
on macOS:
```
<UNITY> -batchmode -quit -projectPath client/CityBuilder -logFile -
```
Exit code 0 and no `error CS` in the log means it compiles.

## Run the tests
Close the Editor first. Point `UNITY_EDITOR` at the Unity program of the project's editor (6000.3.25f1; the `Unity.app` folder also works on
macOS), then run the script. It runs EditMode, then PlayMode, headless, and prints one line per run:
```
export UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity   # macOS; add it to ~/.zshrc to keep it
bash scripts/dev/test-unity.sh
```
Windows (PowerShell): `$env:UNITY_EDITOR = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'`, then
`pwsh scripts/dev/test-unity.ps1`. Exit code 0: every test passed. 1: a test failed, no test ran, or Unity failed (a compile error, a project that
is already open). 2: the setup is wrong (`UNITY_EDITOR` unset or not an executable). The result files (NUnit XML) and Unity's logs are in
`client/CityBuilder/Logs/test-unity/` (git ignores the folder). The run takes about 25 seconds. Unity tests are not in CI (no license on
the runner): paste the summary into the build log's Verification table.

## [MANUAL] Create the Bootstrap scene (done by the owner on 2026-10-14; kept for re-creating it)
Claude cannot use the Editor GUI, and a scene file is never hand-edited. The scene stays **empty**: `GameBootstrap` creates the one
`GameRoot` when a scene named exactly `Bootstrap` loads.
1. Open the project in the Editor and wait until the Console is quiet.
2. **File → New Scene.** Pick the template named *Empty* if the list has one. Otherwise pick *Basic*, click **Create**, then select
   every object in the Hierarchy (Main Camera, Directional Light and any Volume object the template added) and delete them. The Hierarchy must be empty.
3. **File → Save As…** → folder `Assets/Scenes`, name **`Bootstrap`** (exactly; the name triggers the bootstrap) → Save.
4. **File → Build Profiles** (older Editors: **Build Settings**) → **Scene List**. With Bootstrap open click **Add Open Scenes**, drag
   `Scenes/Bootstrap` to the top so its number is **0**, and untick `Scenes/SampleScene` (the template's sample).
5. Press **Play** with Bootstrap open. Expected: a dark rounded label at the bottom left of the Game view, just above the Build button once connected (the top belongs to the HUD, M0-49): "Not connected: Cannot reach the server (CLIENT_NETWORK_ERROR)" when no API runs, or "Connected - Early Middle Ages - Coins: 700" when the dev API runs (`docs/getting-started.md`; the client logs in as the player named in `Resources/Config/ApiClientSettings.asset`, `dev1`, and keeps the token in `PlayerPrefs` in the Editor and development builds); in the Hierarchy a
   `GameRoot` with the children `World` and `UI`; no red messages in the Console. The Game
   view is dark blue-grey behind the label: `GameRoot` makes the camera (M0-43), the scene needs none. With the server running, a green
   8 by 8 grid appears. Press Play again to stop.
6. **File → Save**, close the Editor. Then either commit the files yourself on the current task branch, before you merge its PR (the
   message must look like `type(ID): text`; in M0-04 it was `chore(M0-04): add the Bootstrap scene` on `task/M0-04-unity-skeleton`):
   ```
   git switch task/<ID>-<slug>
   git add client/CityBuilder/Assets/Scenes client/CityBuilder/ProjectSettings/EditorBuildSettings.asset
   git commit -m "chore(<ID>): re-create the Bootstrap scene"
   git push
   ```
   or tell the next AI coder that the scene exists, and it commits `Bootstrap.unity`, its `.meta` file and `EditorBuildSettings.asset`.
7. If the label does not show, copy the first red Console message into the chat. Do not edit the scene or the scripts by hand.

## [MANUAL] Look at the first imported building (CR-0005-T2)
The tests check the settings and the sizes; only a person can say that the model looks right in the Editor. Nothing here is saved or committed.
1. Open the project in the Editor (or click into the open Editor) and wait until the Console is quiet. The first time, Unity imports the 22 models. There should be no red message.
2. In the **Project** window open `Assets/_Project/Art/Resources/Models/era1` and click **house_l1**. In the **Inspector**, tab **Model**: *Scale Factor* is 1; *Import Cameras*, *Import Lights* and *Generate Colliders* are off.
3. Tab **Materials**: under *Remapped Materials*, `M_Palette_Era1` points at the material `M_Palette_Era1` in `Art/Materials`. Do not press *Apply*.
4. **File → New Scene** (*Basic*), then drag **house_l1** from the Project window into the **Scene** view. In the Inspector set its *Position* to 0, 0, 0. Its *Scale* is 1, 1, 1 and its *Rotation* 0, 0, 0.
5. Look at it from the front: a cottage with white plaster, dark timber, a yellow thatched roof and a grey chimney, with sharp colours (no blurred edges between them) and flat faces. The door is on the blue arrow's **back** side (toward −Z). The Hierarchy shows the children `Anchor_Status` and `Anchor_Door`. It is about as wide as one grid square: 0.86 units, so a default 1-unit cube beside it would be slightly wider.
6. Close the scene without saving (**File → New Scene** → *Don't Save*), and tell the AI coder "house looks right", or what looks wrong.

## [MANUAL] Look at the city with the models (CR-0005-T3)
The tests check which model each building gets and that nothing allocates; only a person can say that the city looks right. Nothing here is saved or committed.
1. Start the server and the database as in `docs/testing/LOCAL-E2E.md`, then open the project in the Editor, open the **Bootstrap** scene and press **Play**.
2. The ground is a green grass field of square tiles, the buildings are the toy-block models standing on their tiles, and the door of each faces the camera's left-front (toward −Z).
3. In the **Build** menu pick a building, then a free tile: the tiles show green and red, and after **Place** a pale, see-through copy of the building stands on the tile until the server answers. It should look like one clean see-through house, not a patchwork of layers.
4. When the server accepts, the **construction site** (foundation, frame, scaffold, a red flag) stands there, and the finished building replaces it when the time is up. Use the dev time scale (`LOCAL-E2E.md`) to wait less.
5. Upgrade a building: the construction site shows again during the upgrade, then the level 2 model.
6. Tell the AI coder "city looks right", or what looks wrong. Stop Play mode without saving the scene.

## [MANUAL] Look at the icons (CR-0005-T4)
The tests compare textures; only a person can say the pictures look right and sit well. Nothing here is saved or committed. (Unity's `Resources/UI/Icons/` is
new: if your open Editor shows new `.meta` files under it, they are the committed ones.)
1. Start the server and the database as in `docs/testing/LOCAL-E2E.md`, then open the project in the Editor, open the **Bootstrap** scene and press **Play**.
2. The top bar: the chips for coins, logs and planks each show their picture (a gold coin pile, a stack of logs, a stack of planks) instead of a letter. Under them,
   workers and builders have a small picture beside the number, and the research ring has a book in its middle.
3. Open **Build**: each card shows its building (cottage, sawmill, trading post, woodcutter's hut) beside the name. Resize the Game view to a narrow phone shape:
   the chips wrap and the name stays readable next to the picture.
4. Tell the AI coder "icons look right", or what looks wrong (too big, too small, the wrong picture). Stop Play mode without saving the scene.

Last updated: CR-0005-T4
