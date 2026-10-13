# Unity client

The Unity project is `client/CityBuilder/` (Unity **6000.3.25f1**, 6.3 LTS, Universal 3D template, URP). The rules are in
`core-instructions/09-unity-client.md` and `.claude/rules/unity.md`; the module docs are `docs/modules/CityBuilder.Client.*.md`.

## Open it
Unity Hub → **Add** → `client/CityBuilder` → editor 6000.3.25f1. The first import takes a few minutes. The Console should show no red
errors. Yellow "Assembly for Assembly Definition File … will not be compiled, because it has no scripts associated with it" warnings
for `Core`, `Net` and the two test assemblies are expected until those assemblies get their first scripts (M0-05, M0-37, M0-39).

## Layout
```
client/CityBuilder/
├── Assets/_Project/
│   ├── Core/            CityBuilder.Client.Core           client state, display prediction (skeleton)
│   ├── Net/             CityBuilder.Client.Net            API client, DTOs (skeleton)
│   ├── Presentation/    CityBuilder.Client.Presentation   GameRoot, bootstrap, screens
│   ├── Resources/UI/    PanelSettings.asset, UXML and USS the code loads by name
│   └── Tests/EditMode · Tests/PlayMode                    test assemblies (no tests yet, M0-05)
├── Assets/Scenes/       Bootstrap.unity is created by hand (below)
├── Packages/manifest.json   the local package com.citybuilder.simulation -> ../../shared/Simulation, Newtonsoft, Input System 1.14.2
└── ProjectSettings/     Force Text, Visible Meta Files, Input System, HTTP allowed in development builds
```
Commit every `.meta` file. `client/CityBuilder/.gitignore` covers the generated folders.

## Check that it compiles without the Editor GUI
Close the Editor first (a project can be open once). `<UNITY>` is `/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity`
on macOS:
```
<UNITY> -batchmode -quit -projectPath client/CityBuilder -logFile -
```
Exit code 0 and no `error CS` in the log means it compiles. `scripts/dev/test-unity.sh` *(planned, M0-05)* wraps this and the tests.

## [MANUAL] Create the Bootstrap scene (the owner, once; M0-05 needs it)
Claude cannot use the Editor GUI, and a scene file is never hand-edited. The scene stays **empty**: `GameBootstrap` creates the one
`GameRoot` when a scene named exactly `Bootstrap` loads.
1. Open the project in the Editor and wait until the Console is quiet.
2. **File → New Scene.** Pick the template named *Empty* if the list has one. Otherwise pick *Basic*, click **Create**, then select
   every object in the Hierarchy (Main Camera, Directional Light, Global Volume) and delete them. The Hierarchy must be empty.
3. **File → Save As…** → folder `Assets/Scenes`, name **`Bootstrap`** (exactly; the name triggers the bootstrap) → Save.
4. **File → Build Profiles** (older Editors: **Build Settings**) → **Scene List**. With Bootstrap open click **Add Open Scenes**, drag
   `Scenes/Bootstrap` to the top so its number is **0**, and untick `Scenes/SampleScene` (the template's sample).
5. Press **Play** with Bootstrap open. Expected: a dark rounded label "Not connected" at the top left of the Game view; in the Hierarchy a
   `GameRoot` with one child `UI`; no red messages in the Console (the yellow "no scripts" warnings above are expected). The Game
   view may print "No cameras rendering": that is
   expected, the scene has no camera until M0-43, and the UI overlay does not need one. Press Play again to stop.
6. **File → Save**, close the Editor. Commit `Assets/Scenes/Bootstrap.unity`, `Assets/Scenes/Bootstrap.unity.meta` and
   `ProjectSettings/EditorBuildSettings.asset` yourself, or tell the next AI coder that the scene exists and it commits them.
7. If the label does not show, copy the first red Console message into the chat. Do not edit the scene or the scripts by hand.

Last updated: M0-04
