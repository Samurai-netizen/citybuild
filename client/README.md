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
│   ├── Presentation/    CityBuilder.Client.Presentation   GameRoot, bootstrap, startup flow, status screens
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
5. Press **Play** with Bootstrap open. Expected: a dark rounded label at the top left of the Game view: "Not connected: Cannot reach the server (CLIENT_NETWORK_ERROR)" when no API runs, or "Connected - Early Middle Ages - Coins: 700" when the dev API runs (`docs/getting-started.md`; the client logs in as the player named in `Resources/Config/ApiClientSettings.asset`, `dev1`, and keeps the token in `PlayerPrefs` in the Editor and development builds); in the Hierarchy a
   `GameRoot` with one child `UI`; no red messages in the Console. The Game
   view may print "No cameras rendering": that is
   expected, the scene has no camera until M0-43, and the UI overlay does not need one. Press Play again to stop.
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

Last updated: CR-0002-T1
