# Unity Client

> **Built so far:** the project, its four assemblies and their tests, and the `Bootstrap` scene that shows "Not connected" (M0-04, M0-05;
> `client/README.md`). The API client, the DTOs and the error parsing (`CityBuilder.Client.Net`, M0-37) and the start of the game (login, content through the engine's factory, the first `GET /city`, "Connected" or the fatal error screen: `AuthService`, `ContentService`, `StartupFlow`, `GameRoot`, M0-38) and the city store with its display prediction (`ClientCityStore`, `DisplayClock`, `DisplayCity`, M0-39) and the command sender with its retry policy (`CommandSender`, M0-40) and the polling with the connection state (`ConnectionMonitor`, `PollScheduler`, M0-41) are built and tested; the city's grid, ground and camera (`CityWorld`, `CityGrid`, `CityCameraModel`, `CityInputReader`, M0-43) and the building views (`BuildingLooks`, `BuildingViewFactory`, `CityView`, M0-44) are built and tested; the build menu (`BuildMenu`, `BuildMenuModel`, `BuildCardModel`, M0-45) and the placement flow (`PlacementFlow`, `PlacementFlowModel`, `PlacementBar`, `PlacementGhosts`, M0-46) are built and tested; the feedback for every result (`FeedbackMessages`, `ToastQueue`, `ToastHost`, `CommandFeedback`, M0-47) is built and tested; a review of all of it (M0-48) split the status screen out of `GameRoot` (`ConnectionStatusView`); the top HUD (`HudView`, `HudModel`, `ItemChipModel`, `ResearchRingModel`, `PerHourText`, M0-49) is built and tested; the Era I art is imported by an editor script (`ArtImportPostprocessor`, CR-0005-T2) and drawn by the city view: buildings and ground from the models, the construction site while a building is built or upgraded, ghosts from the models (`BuildingLooks`, `BuildingViewFactory`, `GroundTiles`, `PlacementGhosts`, CR-0005-T3); the other screens *(planned)*. A task that builds a
> part updates this line.

The client renders the latest authoritative snapshot and makes waiting feel alive.
It never decides outcomes.

## Project settings (set once, Day 1 — [MANUAL] where noted)
- Unity **6.3 LTS**, project at `client/CityBuilder/`, Universal 3D template. [MANUAL: create in Unity Hub]
- Asset serialization: **Force Text**; Version control: **Visible Meta Files**. Commit every `.meta`.
- Active Input Handling: Input System (package **1.20.0**, the version the editor bundles: 1.12.0 does not compile on 6000.3, decision 023).
  Scripting backend for device builds: IL2CPP.
- Built-in modules are enabled only when the manifest or a dependency asks for them: the manifest names `com.unity.modules.unitywebrequest`
  (`ApiClient` is built on `UnityWebRequest`). No pre-release or experimental package (decision 025).
- Player → "Allow downloads over HTTP": *Allowed in Development Builds* (needed for `http://localhost`).
- `client/CityBuilder/.gitignore` (inside the Unity folder only — the root must not ignore server
  `.csproj` files): `Library/ Temp/ Logs/ obj/ Build/ Builds/ UserSettings/ *.csproj *.sln *.slnx`.

## AI-friendly Unity rules (Claude cannot use the Editor GUI)
1. **Minimal scenes.** `Bootstrap.unity` is empty: `GameBootstrap` creates the one `GameRoot` GameObject when a scene
   named `Bootstrap` loads (a `GameRoot` already in the scene is kept). Everything else is
   created from code at runtime. Avoid hand-editing scene or prefab YAML.
2. **UI Toolkit** (UXML + USS text files) for all screens and HUD — readable and diffable. The UXML, USS and
   `PanelSettings` that code loads by name live in `Assets/_Project/Resources/UI/` (decision 024).
3. **Art from the Era I models** (CR-0005): FBX files made by `art/blender/` that sit in `Assets/_Project/Art/Resources/Models/` (so a player build has them) and are loaded by name with `Resources.Load` (`Models/era<N>/<typeId>_l<level>`, decision W16), all drawn with one shared URP material. Primitives (`GameObject.CreatePrimitive`) are only the fallback for a level or a type with no model, the hover and selection markers and the tile hints. No prefabs are required for the MVP; if one is needed, create it with an editor script. The UI pictures are 256 px PNG icons in `Assets/_Project/Resources/UI/Icons/` (the only copy; `art/blender/render_ui.py` writes there), found by content id through `UiIcons` (`item_<id>`, `bld_<typeId>`, `res_<id>`, and `workers`, `builders`, `research`; CR-0005-T4); an editor script sets their import settings, and a test fails for an item, building type or research node of the content that has no icon. A view with no picture keeps its text.
4. When a step needs the Editor (creating the project, assigning a scene to Build Settings,
   pressing Play), write exact click-by-click **[MANUAL]** instructions instead of guessing.

## Assemblies (asmdef)

| Assembly | Folder | References |
|---|---|---|
| `CityBuilder.Simulation` | `shared/Simulation/Runtime` (local package, `noEngineReferences: true`) | — |
| `CityBuilder.Client.Core` | `Assets/_Project/Core` | Simulation |
| `CityBuilder.Client.Net` | `Assets/_Project/Net` | Core, Simulation, Newtonsoft |
| `CityBuilder.Client.Presentation` | `Assets/_Project/Presentation` | Core, Net, Simulation, Input System (UI Toolkit is an engine module: no entry) |
| `CityBuilder.Client.Editor` | `Assets/_Project/Editor` (Editor only; the art's import, CR-0005-T2) | — |
| `CityBuilder.Client.Tests.EditMode` / `.PlayMode` | `Assets/_Project/Tests/…` | as needed |

Local package line in `Packages/manifest.json`:
`"com.citybuilder.simulation": "file:../../../shared/Simulation"`

## Runtime structure
- **Composition root:** `GameRoot` builds services by hand (no DI framework, no singletons, no static mutable state) and draws nothing itself: a screen is a component of its own (`ConnectionStatusView`, `BuildMenu`, `PlacementBar`, `ToastHost`).
  `GameBootstrap` (a `sceneLoaded` handler registered before the first scene) creates it when the `Bootstrap` scene loads.
- **Core:** `ClientCityStore` holds the last snapshot + receive time; raises `SnapshotChanged`.
  Ignores snapshots whose `stateVersion` is lower than the current one (out-of-order responses), and the same version with the same or an
  earlier clock (the same city, for example a late replay: taking it would move the display clock back). The snapshot's city never leaves it
  uncopied: readers get `CopyCity()`. `SnapshotMapper.Apply` (Net) is the way from an answer to the store; a refused command never changes it.
- **Display prediction:** `DisplayClock` = `snapshot.clock + whole secondsSinceReceived × rules.timeScale`
  (`timeScale` is 1 except on Development servers using `DevTimeScale`). For counters and
  progress bars, `DisplayCity` runs `Simulator.Advance` on a **copy** of the snapshot, at most once per second and always from the snapshot, so it
  equals one advance of the whole time (the production cap counts from the start of an advance). A new snapshot replaces it at once. Views use the `Simulation.Display` helpers (`04` §15) for rates,
  bars and countdowns. Never send predicted values anywhere.
- **Net:** `ApiClient` (`UnityWebRequest` wrapped in a `Task` that continues on the main thread; base URL and timeout from the
  `ApiClientSettings` asset), one method per endpoint of `05`, each returning an `ApiResult<T>`: the DTO or an
  `ApiError { Code, Status, Detail, TraceId, RequestId, RetryAfterSeconds }` parsed from Problem Details. A rule's refusal, a lost
  connection (`CLIENT_NETWORK_ERROR`), a timeout (`CLIENT_TIMEOUT`) and a body that is not the contract's, such as a proxy's 502 page
  (`CLIENT_INVALID_RESPONSE`), are results; only a cancelled token throws (cancel with `destroyCancellationToken`).
- **Start (M0-38):** `GameRoot.Start` runs `StartupFlow`: `AuthService` takes a kept token for the configured dev player or calls the dev
  login, `ContentService` turns `GET /content` into `GameContent` through `GameContentFactory` (so the client trusts no content its own
  engine would refuse), then `GET /city`, whose snapshot goes into the store (`GameRoot` then builds `DisplayCity` and refreshes it in `Update`). The token is in memory; the Editor and development players also keep it in `PlayerPrefs`
  (keys `CityBuilder.DevLogin.*`), never a snapshot or a balance. A kept token the server refuses (`AUTHENTICATION_REQUIRED`) is dropped
  and the flow logs in once more. Invalid content shows the fatal error screen (`UI/FatalError`); a failed request shows "Not connected"
  with a sentence and the code. The dev player's name is `devDisplayName` in `ApiClientSettings`.
- **Commands (M0-40):** `CommandSender` (Net) creates the `requestId` once per player intent (`CommandIntent`) and reuses it on every attempt.
  Retries only on network errors, timeouts, 5xx and `CONCURRENCY_CONFLICT`: at most 3 requests, waits 0.5 s then 1 s (`CommandPolicy.Default`
  holds 0.5/1/2; the 2 s would precede a 4th request). Any other 4xx (also `RATE_LIMITED`) is final. On success the snapshot goes to the store
  through `SnapshotMapper.Apply`. `Pending` lists the intents on their way (`Waiting`, `Sending`, `Retrying`) for ghosts and disabled buttons;
  a repeat of a pending intent shares its result. `AssignWorkers` targets are coalesced per building: a new target replaces a not-yet-sent one,
  is sent after a 400 ms pause with no newer one, and waits for the building's earlier target to end. `GameRoot` builds the sender once connected.
- **Polling (M0-41):** `GET /city` on start (the start flow's own), on `OnApplicationPause(false)`, after the device's network comes back, and every 120 s in the
  foreground; nothing while paused, one poll at a time (`PollScheduler`). `ConnectionMonitor` (Net) puts each snapshot in the store and holds the
  `ConnectionState` (`Connecting`, `Connected`, `Offline`, `Unauthorized`, `ServerError`, `IncompatibleVersion`): a refused token gets one new login and one
  repeat, then `Unauthorized`, and the answer to that repeat replaces the city whatever its version (it may be a new city, CR-0004); content of another version is `IncompatibleVersion` and is not applied. `GameRoot` ticks it in `Update` and shows the state.
- **City view (M0-43):** `GameRoot.Awake` creates `CityWorld` first: the one `City Camera` (tag `MainCamera`, solid dark background, perspective, yaw 45° and pitch 50°) and a sun, so the screen is cleared from the first frame. Once connected, `ShowGrid(new CityGrid(rules.GridWidth, rules.GridHeight))` builds the ground (an instance of the `tile_grass` model on every tile, CR-0005-T3) and the controls.
  Grid x is world x, grid y is world z, a tile is 1 unit, the ground is y = 0. A drag (one finger or the left mouse button) pans so the ground point under the pointer stays under it, clamped to the grid; a pinch or the mouse wheel zooms between 3 units and the distance where the whole grid fits; a tap selects the tile (again, or off the grid, clears it); the mouse pointer marks its tile. `TileSelection` is display state only: tile checks are UX hints, the server decides (`UX rules`).
- **Building views (M0-44, CR-0005-T3):** `CityView` (under `CityWorld`) reconciles one `BuildingView` per building of `DisplayCity.Current` by building id: a new id gets a view, a changed one is updated in place, a missing one is destroyed; `GameRoot` calls `CityWorld.ShowCity` on every `DisplayCity.Changed` (once a second), and a call where nothing changed allocates nothing. The look comes from `BuildingLooks` by `(typeId, city era, level)`: the model `Models/era<N>/<typeId>_l<level>` (a later era without art of its own keeps the latest earlier era's model); while the building is `Constructing` or `Upgrading` the `construction_site` model of the era instead, so nothing finished shows before the server says so (an upgrade shows the site, then the next level). A level with no model falls back to the type's primitive look of M0-44 (20 % taller per level; a low foundation while constructing, only if there is no site model), and a type nobody knows is a grey box, never an exception. A model is never stretched; models share the art's one material (no copy per building, so a stalled tint is a per-renderer property, M0-50). The view finds `Anchor_Status`, `Anchor_Door`, `Anchor_Smoke`, `Anchor_Stockpile` and `Wheel` by name once when it builds the body (null when a model has none); nothing uses them yet and nothing turns the wheel. A unit test fails when a building type of the content has no model for a level it can reach or a model lacks `Anchor_Status` / `Anchor_Door`. Views have no colliders and never call the server.
- **Build menu (M0-45):** `BuildMenu` (a `UIDocument` of its own, made by `GameRoot` once connected; layouts `UI/BuildMenu` and `UI/BuildCard`) shows one card per building type of the content, with the type's icon (`bld_<typeId>`, set once when the card is made). `BuildCardModel` holds a card's words: name, `Cost: 150 Coins, 30 Logs`, `Build time: 2m` (`DurationText`), `Max workers: 3`, the recipe `2 Logs → 1 Planks · 60/h at 3 workers` (the rate is `ProductionDisplay.PerHour`, so research shows in it), the lock `Requires: Trade Charter` (a locked card is disabled and cannot be selected) and one hint (`All builders are busy`, then `Not enough <items>`; the server's order). `BuildMenuModel.Update` follows `DisplayCity.Current` once a second; hints never stop a selection, the server decides. `TryToggle` selects a card (a second tap clears it); placing is M0-46. A press over the menu belongs to the UI: `CityWorld.IsPointerOverUi` (set to `BuildMenu.IsOverUi`, which asks the panel) keeps it from panning, tapping or hovering the ground. The menu's root is padded by `Screen.safeArea` (`SafeAreaInsets`). How to add a screen: `docs/how-to/add-ui-screen.md`.
- **Feedback (M0-47):** every command result is shown as a toast, from one place. `CommandSender.Completed` fires once for each intent that ended with the server's answer or an error (not for a replaced worker target); `CommandFeedback` listens to it and adds a toast to the `ToastQueue`, which `ToastHost` draws at the top centre (its own `UIDocument`, sort order 3, no label takes the pointer). A success says what started ("Construction started", "Upgrade started", "Workers updated", "Research started"); a failure says the sentence `FeedbackMessages` has for its error code ("All builders are busy", "Research needed first", "Not enough free workers. Build more housing"), a lost connection or a timeout says to check the network and try again, a rate limit names the wait the server gave. **A test fails when a code of `ApiErrorCodes` has no sentence** (it lists the constants by reflection), and `ClientErrorCodesTests` holds `ApiErrorCodes` to the table of `05`. No sentence holds a game number or a content name. The code, status and trace id of a failure go to the log as a warning, never to the screen; a stack never does. At most three toasts show at once, an error stays 5 s, a success 2.5 s, and the same words shown again renew the toast instead of stacking. A screen that sends a command needs no toast code of its own; one that needs a sentence for a new command adds it to `FeedbackMessages.ForSuccess`.
- **HUD (M0-49):** `HudView` (its own `UIDocument`, sort order 0.5, so the menu, the bar and the toasts draw over it; layouts `UI/Hud` and `UI/HudChip`) shows the top of the screen from `HudModel`, which follows `DisplayCity.Current` and the monitor's `ConnectionState` once a second and raises `Changed` only when a value differs. One `ItemChipModel` per item of the city's era or an earlier one, in the content's order: the item's icon (`UiIcons.ForItem`; the item's first letter only when there is no picture), the stock, the cap when the item has one (`CityQueries.TryGetEffectiveCap`, research bonuses included) and the net rate per hour from `ProductionDisplay.NetPerHour` worded by `PerHourText` (`+60/h`, `−30/h`, `0/h`; one tenth when not whole, rounded toward zero). Then workers free / provided and builders busy / slots (`Rules.BuilderSlots`), each with its icon beside the number, the research ring (`ProgressDisplay.ResearchProgressPermille` at the displayed clock, the node's name, the time left; the research icon sits in its middle) or `No research`, the era badge (the era's name from the content) and the connection indicator (a dot and a word: `Online`, `Connecting`, `Offline`, `Signed out`, `Update needed`, `Server problem`). The long status sentence with the error code stays in `ConnectionStatusView`, now at the bottom left; the toasts start below the HUD. No element of the HUD takes the pointer.
- **Placement (M0-46):** a card picked in the menu starts `PlacementFlow` (`PlacementFlowModel`: `Idle` → `PickingTile` → `Confirming` → `Sending`) and closes the sheet; the ground paints every tile green (free) or red (occupied) from the displayed city (`GroundTiles.ShowHints`; a red tile can be tapped, and the bar says "This tile is occupied", but **Place** stays dimmed and nothing is sent for it; the server still checks every command); a tap picks the tile, and the `PlacementBar` (bottom right, its own `UIDocument`) asks "Build Cottage at (3, 4)?" with **Place** and **Cancel**. **Place** gives `CommandSender.SendAsync` a `PlaceBuilding` intent; while the request is open (retries included) `PlacementGhosts` draws a translucent copy of the type on the tile for every `PlaceBuilding` in `CommandSender.Pending`, and nothing else changes: no coins subtracted, no building. A success puts the real building on screen from the new snapshot, removes the ghost in the same call and ends the flow; a refusal removes the ghost, shows its human message (`FeedbackMessages`, M0-47; never the code) in the bar, keeps the card and drops the tile. **Cancel**, or tapping the card again, ends the flow at any step before **Place**; a sent command cannot be cancelled. While one is on its way the picks are frozen.
- **The start (`StartupFlow`):** a kept token refused by `GET /content` or by `GET /city` gets one new login and one repeat; a refusal after a fresh login is a failure.
- **The wire:** no redirect is followed, because the API never sends one and the bearer token must go to no one else (CR-0004).

## M0 screens (UI Toolkit)
HUD (item chips with stock/cap/net rate per hour, workers free/total, builders, research ring, era badge; built, M0-49) ·
build menu (locked cards name the research that unlocks them; built, M0-45) · building inspector (recipe card, **worker
stepper − / +** with a live rate preview, upgrade section) · research screen (Era I tree: nodes with cost,
time, effect, status; finance button) · welcome-back report · toasts (built, M0-47). Market and guild screens come in M2.

## UX rules
- No optimistic economy: never subtract resources, change worker counts or spawn a finished building
  before the server confirms. A "pending" ghost or a pending stepper value is fine.
- The worker stepper sends the **absolute** target count after a short debounce (~400 ms), so rapid taps
  become one command.
- Client-side checks (tile occupied, obviously unaffordable, no free workers, research locked) are
  **UX hints only**; the server re-checks.
- Every error code maps to a human message (`FeedbackMessages`, M0-47; a test fails for a code without one). Nothing is shown only in logs, and no code, server text or stack is shown to the player.
- Touch targets ≥ 48 dp; respect `Screen.safeArea` on the UI root.
- Art and labels are era-aware from the start: look up building visuals by `(typeId, era, level)` (`BuildingLooks`), even though M0 has only Era I.

## Shared-code constraints (C# 9, netstandard2.1)
No file-scoped namespaces, no global usings, no `required`, no records or `init` without an
`IsExternalInit` shim. Put `#nullable enable` at the top of shared files.

## Running tests from the CLI
`Unity -batchmode -projectPath client/CityBuilder -runTests -testPlatform EditMode -testResults <file>`
The Editor must be closed for that project (project lock). `scripts/dev/test-unity.sh` (and `.ps1`) wraps this for EditMode then PlayMode: it reads the
Unity program from `UNITY_EDITOR`, prints a summary and exits non-zero on a failure or when no test ran (`client/README.md` → Run the tests).

## Device testing
Android device → server on your PC: use the PC's LAN IP or `adb reverse tcp:8080 tcp:8080`.
IL2CPP may strip DTO members used only via reflection: add a `link.xml` preserving the DTO assembly.
