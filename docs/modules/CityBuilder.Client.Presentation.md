# CityBuilder.Client.Presentation

**Path:** `client/CityBuilder/Assets/_Project/Presentation/` · **Kind:** Unity assembly (`CityBuilder.Client.Presentation.asmdef`, C# 9) ·
**References:** `CityBuilder.Client.Core`, `CityBuilder.Client.Net`, `CityBuilder.Simulation`, `Unity.InputSystem` (UI Toolkit is an engine module, so it needs no entry) ·
**Referenced by:** `CityBuilder.Client.Tests.EditMode`, `CityBuilder.Client.Tests.PlayMode`

> *Early.* The composition root, its bootstrap, the startup flow and the status screens exist (M0-38), and so do the city's grid, ground and
> camera (M0-43) and the building views (M0-44). The game screens come with the later client tasks *(planned, M0-45 on)*.

## Purpose
Everything the player sees and touches: the composition root, scene objects created from code, UI Toolkit screens and input. It renders
what `CityBuilder.Client.Core` holds and forwards player intent to `CityBuilder.Client.Net`. It is **not** responsible for game rules,
for balances or for any decision: it only displays server state.

## Key types
| Type | Responsibility |
|---|---|
| `GameRoot` (`Bootstrap/`) | The composition root, a thin `MonoBehaviour`: the one place that builds services by hand. It owns the `ClientCityStore`, builds `DisplayClock`, `DisplayCity`, the `CommandSender` and the `ConnectionMonitor` (`CommandPolicy.Default`, kept in `CommandSender` for the views of M0-44 on) once connected, refreshes the display city and ticks the monitor in `Update`, forwards `OnApplicationPause` to it and shows its state on the status label whenever it changes (`Connection` gives views the monitor). `Awake` creates the UI object (a `UIDocument` with `UI/PanelSettings` and the tree `UI/ConnectionStatus`); `Start` builds `ApiClient`, `AuthService` and `ContentService` from `Config/ApiClientSettings`, runs `StartupFlow` with `destroyCancellationToken`, and shows the outcome. Missing assets are logged as one error that names them (the root stays alive without UI); a failed connection is logged as a warning only |
| `StartupFlow`, `StartupOutcome` (`Bootstrap/`) | Plain classes: restore a kept token or log in, load the content, fetch the city and put its snapshot in the `ClientCityStore`. When the server refuses a kept token, at `GET /content` or at `GET /city` (only the city can find that the token's player is gone, CR-0004), the flow drops it and logs in once more; a refusal after a fresh login is a failure. The outcome is `Connected` (content and first snapshot), `Failed` (an `ApiError`) or `InvalidContent` (the factory's errors). Nothing is stored: the city store is M0-39 |
| `ConnectionStatusModel` (`Bootstrap/`) | The sentences of the start screen as plain data: `Connecting...`, `Connected - <era name> - <item name>: <coins>` (names from the content, fallbacks `Era 9` and `0`), one sentence per kind of failed request ending in its code, and for invalid content or bad settings a fatal screen with the lines. The id of the coins item is `ConnectionStatusModel.CoinsItemId`. `ForConnection(state, error, content, snapshot)` (M0-41) words the six connection states: `Connected - <era> - <item>: <coins>` from the latest snapshot, `Offline: ...`, `Signed out: ...`, `Server problem: ...`, `Update needed: ...`, each ending in the error's code |
| `GameBootstrap` (`Bootstrap/`) | `internal static`. Registers a `SceneManager.sceneLoaded` handler before the first scene loads (`RuntimeInitializeOnLoadMethod`, `BeforeSceneLoad`); when the scene named `Bootstrap` loads, it creates one `GameRoot` in that scene. A `GameRoot` already in the scene (a root or any descendant) is kept and nothing is added. It holds no field |

| `CityWorld` (`City/`) | The one new `MonoBehaviour` of the city view (M0-43). `GameRoot.Awake` creates it before the UI; its `Awake` makes the `City Camera` (tag `MainCamera`, solid dark background, so nothing of an earlier screen shows through) and a directional light. `ShowGrid(CityGrid)`, called by `GameRoot` once the content is loaded, builds the `TileSelection`, the `GroundTiles`, the `CityCameraRig`, the `CityInputReader` and an empty `CityView`, and replaces an earlier ground and buildings; `ShowCity(CityState)` makes the buildings match a city; `Update` ticks the reader |
| `CityGrid`, `GridCoordinate` (`City/`) | The grid's shape from `GameRules.GridWidth/GridHeight` and its world conversions. A tile is 1 world unit, the ground is y = 0, grid x is world x and grid y is world z; a tile owns its west and south edges. `TryGetCoordinate` (world to tile), `ToWorldCenter` (tile to world), `Contains`, `ClampToBounds`. `GridCoordinate` is a `readonly struct` with value equality |
| `CityCameraModel` (`City/`) | The angled 3/4 camera as maths: focus on the ground, distance, yaw 45°, pitch 50°, field of view 40°. `Pan` clamps the focus to the grid, `Zoom(factor)` clamps the distance between 3 units and the distance where the whole grid fits (`Frame(aspect)`), `Position` and `Rotation` give the pose |
| `CityCameraRig`, `CityInputReader` (`City/`) | The rig projects screen points onto the ground plane: `DragBy` keeps the ground point under the pointer under it, `Zoom`, `TapAt` selects a tile, `HoverAt` marks one. The reader is the only code that touches the Input System devices: one finger or the left mouse button drags or taps, two fingers pinch, the wheel zooms (every wheel event is one ×0.9 step), the mouse pointer hovers, touch wins over the mouse, and the finger left after a pinch cannot tap. `PointerGesture` (tap or drag by a pixel slop) and `PinchGesture` (zoom factor) hold the gesture rules |
| `TileSelection`, `GroundTiles` (`City/`) | `TileSelection` is the hovered and the selected tile (a tap on the selected tile or off the grid clears it). `GroundTiles` builds one flat cube per tile in two greens plus a hover and a selection marker from primitives, with no colliders, and `Destroy` removes what it made. Display only: nothing here decides whether a building may stand on a tile |
| `BuildingLooks`, `BuildingLook`, `BuildingPart` (`City/`) | The placeholder looks as plain data: a code table filed by `(typeId, era)` (the choice of a code table over a ScriptableObject is M0-44 decision 1), each look a list of box or cylinder parts with place, size, turn and colour inside one tile. `Get(typeId, era)` returns the entry for the era, else the latest earlier era's, else `Fallback`. `HeightPermille(level, phase)` is the one height rule: 1000 + 200 per level above 1, and 250 while constructing |
| `BuildingViewFactory`, `BuildingView` (`City/`) | The factory makes a building's object under `CityView` (named `Building <id> <typeId>`, on its tile, a `Body` child holding the look's primitives without colliders, one material per colour) and `Refresh` brings it up to date: a new body only when the type or the era changed, a new height when the level or the phase changed, nothing otherwise. `BuildingView` is the thin component that remembers id, type, level, phase and era |
| `CityView` (`City/`) | Reconciles the views with a `CityState` by building id (`Show`): add, update in place, remove, never a duplicate. Reuses its id set and list, so an unchanged call allocates nothing. `PlaceholderArt` holds the material and destroy helpers it shares with `GroundTiles` |

Assets the types load by name sit in `Assets/_Project/Resources/UI/`, not in this folder, because a `GameRoot` made from code has no scene
field to assign them to: `PanelSettings.asset` (the owner's; its theme is `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss`, which must stay), `ConnectionStatus.uxml` and
`ConnectionStatus.uss` (the label, name `connection-status`, class `connection-status`), `FatalError.uxml` and `FatalError.uss` (the full-screen error for invalid content or unusable settings: `fatal-error-title`, `fatal-error-details`). The config asset it loads is `Resources/Config/ApiClientSettings.asset` (owned by `CityBuilder.Client.Net`).

## Rules and invariants
- References Core, Net, Simulation (for `GameContent` in the outcome) and the Input System package (`03` and `09`); the asmdef is not auto-referenced, and `overrideReferences` is on
  with no precompiled DLL listed, so it cannot use Newtonsoft (only Net does). `ClientAssemblyTests` checks the references.
- Views never call the API client; they use services that `GameRoot` builds by hand (no DI framework, no singletons, no static mutable
  state, no `FindObjectOfType` at runtime). `GameBootstrap` is the only static class: it has no field, and its registration method is the
  one that Unity's attribute requires.
- The scene that triggers the bootstrap is named exactly `Bootstrap` (`GameBootstrap.BootstrapSceneName`); any other scene gets nothing.
- Screens are UI Toolkit (UXML + USS) loaded from `Resources/UI`; scene objects are created from code. `.unity` and `.prefab` YAML is
  never hand-edited (`.claude/rules/unity.md`).
- MonoBehaviours stay thin; logic lives in plain classes that EditMode tests can run (`14`).
- Every type and member, private ones included, has an XML doc comment (`core-instructions/14-coding-standards.md` → Documentation
  in code). Unity's compiler doesn't check them, so `CityBuilder.Repository.Tests` (a server test project) checks every script
  under `Assets/_Project`.

## How to extend
A new screen or view: recipe "New client screen or view" in `core-instructions/19-change-recipes.md` (a folder under
`Presentation/<Feature>/` with a view-model class and a thin MonoBehaviour; its UXML and USS go to `Assets/_Project/Resources/UI/`).
Services that `GameRoot` composes are added in `GameRoot` itself, not through a static or a `Find*` call.

## Tests
`Assets/_Project/Tests/EditMode/Presentation` (`StartupFlowTests`: the requests and tokens of a first start, a kept token, a kept token refused by the content or only by the city, no connection, a server without dev login, invalid content, a failed content or city request, a cancel; `ConnectionStatusModelTests`: every sentence; `Presentation/City/`: `CityGridTests` (conversions, edges, bounds), `CityCameraModelTests`, `PointerGestureTests` (also the pinch), `TileSelectionTests`, `GroundTilesTests` (tiles, markers, clean-up), `BuildingLooksTests` (the table, the era lookup, the height rule), `BuildingViewFactoryTests` (parts, tile, level, phase, refresh)) and `Assets/_Project/Tests/PlayMode` (scene behaviour), run with
`scripts/dev/test-unity.sh` (`client/README.md` → Run the tests). `BootstrapSceneTests` loads the real `Assets/Scenes/Bootstrap.unity`:
- scene 0 of the build list is the Bootstrap scene;
- exactly one `GameRoot` exists (inactive objects counted) and it lives in the Bootstrap scene;
- no error, exception or assert was logged during the load (warnings are not counted);
- the label named `connection-status` shows a start screen status ("Connecting...", "Connected - ..." or "Not connected: ...", depending on whether a server runs on this machine) and the fatal screen is not up; the exact texts are tested in EditMode against a fake wire;
- exactly one camera exists and it is the `GameRoot`'s (M0-43);
- loading the scene again (single mode) still leaves exactly one `GameRoot`.

`CityWorldTests` (PlayMode) shows an 8 by 8 grid in a real camera: framing, the whole grid in view, a drag keeping the ground point under the pointer, the outline limit, zoom limits, tap, hover and replacing the ground. `CityViewTests` (PlayMode, M0-44) gives a sample city of four buildings and checks the number, types and tiles of the views, then a second city that updates one, adds one and removes two with no duplicate and no leftover object, an empty city, a construction that finishes, and a new grid. `CityInputReaderTests` drives the reader with the Input System's fake mouse and touch screen (`InputTestFixture`; the PlayMode test assembly references `Unity.InputSystem` and its `TestFramework` for it).

M0-04 also checked, in a throw-away copy of the project, that a scene with another name spawns nothing and that a pre-placed `GameRoot` is kept.
Those two are **not** tests: both need a second scene asset that a test can load, and none exists (`SampleScene` is the template's and is to go;
`SceneManager.CreateScene` does not raise `sceneLoaded`, so such a test would pass for the wrong reason).

## Known limitations
- `Assets/Scenes/Bootstrap.unity` is made by hand ([MANUAL], `client/README.md`; the owner did it on 2026-10-14) and is empty; it is scene 0 of
  the build list. A scene that is not named exactly `Bootstrap` runs nothing. The scene has no camera of its own:
  `GameRoot` makes the only one (`CityWorld`, M0-43), and a `Camera` in the scene would be a second one.
- The label has a fixed top-left position; safe-area padding and touch-target sizes come with M0-59.

Last updated: M0-44
