# CityBuilder.Client.Presentation

**Path:** `client/CityBuilder/Assets/_Project/Presentation/` · **Kind:** Unity assembly (`CityBuilder.Client.Presentation.asmdef`, C# 9) ·
**References:** `CityBuilder.Client.Core`, `CityBuilder.Client.Net`, `CityBuilder.Simulation`, `Unity.InputSystem` (UI Toolkit is an engine module, so it needs no entry) ·
**Referenced by:** `CityBuilder.Client.Tests.EditMode`, `CityBuilder.Client.Tests.PlayMode`

> *Early.* The composition root, its bootstrap, the startup flow and the status screens exist (M0-38). The city view and the game screens
> come with the later client tasks *(planned, M0-43 on)*.

## Purpose
Everything the player sees and touches: the composition root, scene objects created from code, UI Toolkit screens and input. It renders
what `CityBuilder.Client.Core` holds and forwards player intent to `CityBuilder.Client.Net`. It is **not** responsible for game rules,
for balances or for any decision: it only displays server state.

## Key types
| Type | Responsibility |
|---|---|
| `GameRoot` (`Bootstrap/`) | The composition root, a thin `MonoBehaviour`: the one place that builds services by hand. It owns the `ClientCityStore`, builds `DisplayClock` and `DisplayCity` once connected, and refreshes the display city in `Update`. `Awake` creates the UI object (a `UIDocument` with `UI/PanelSettings` and the tree `UI/ConnectionStatus`); `Start` builds `ApiClient`, `AuthService` and `ContentService` from `Config/ApiClientSettings`, runs `StartupFlow` with `destroyCancellationToken`, and shows the outcome. Missing assets are logged as one error that names them (the root stays alive without UI); a failed connection is logged as a warning only |
| `StartupFlow`, `StartupOutcome` (`Bootstrap/`) | Plain classes: restore a kept token or log in, load the content, fetch the city and put its snapshot in the `ClientCityStore`. When the server refuses a kept token the flow drops it and logs in once more; a refusal after a fresh login is a failure. The outcome is `Connected` (content and first snapshot), `Failed` (an `ApiError`) or `InvalidContent` (the factory's errors). Nothing is stored: the city store is M0-39 |
| `ConnectionStatusModel` (`Bootstrap/`) | The sentences of the start screen as plain data: `Connecting...`, `Connected - <era name> - <item name>: <coins>` (names from the content, fallbacks `Era 9` and `0`), one sentence per kind of failed request ending in its code, and for invalid content or bad settings a fatal screen with the lines. The id of the coins item is `ConnectionStatusModel.CoinsItemId` |
| `GameBootstrap` (`Bootstrap/`) | `internal static`. Registers a `SceneManager.sceneLoaded` handler before the first scene loads (`RuntimeInitializeOnLoadMethod`, `BeforeSceneLoad`); when the scene named `Bootstrap` loads, it creates one `GameRoot` in that scene. A `GameRoot` already in the scene (a root or any descendant) is kept and nothing is added. It holds no field |

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
`Assets/_Project/Tests/EditMode/Presentation` (`StartupFlowTests`: the requests and tokens of a first start, a kept token, a refused kept token, no connection, a server without dev login, invalid content, a failed content or city request, a cancel; `ConnectionStatusModelTests`: every sentence) and `Assets/_Project/Tests/PlayMode` (scene behaviour), run with
`scripts/dev/test-unity.sh` (`client/README.md` → Run the tests). `BootstrapSceneTests` loads the real `Assets/Scenes/Bootstrap.unity`:
- scene 0 of the build list is the Bootstrap scene;
- exactly one `GameRoot` exists (inactive objects counted) and it lives in the Bootstrap scene;
- no error, exception or assert was logged during the load (warnings are not counted);
- the label named `connection-status` shows a start screen status ("Connecting...", "Connected - ..." or "Not connected: ...", depending on whether a server runs on this machine) and the fatal screen is not up; the exact texts are tested in EditMode against a fake wire;
- loading the scene again (single mode) still leaves exactly one `GameRoot`.

M0-04 also checked, in a throw-away copy of the project, that a scene with another name spawns nothing and that a pre-placed `GameRoot` is kept.
Those two are **not** tests: both need a second scene asset that a test can load, and none exists (`SampleScene` is the template's and is to go;
`SceneManager.CreateScene` does not raise `sceneLoaded`, so such a test would pass for the wrong reason).

## Known limitations
- `Assets/Scenes/Bootstrap.unity` is made by hand ([MANUAL], `client/README.md`; the owner did it on 2026-10-14) and is empty; it is scene 0 of
  the build list. A scene that is not named exactly `Bootstrap` runs nothing. Its Game view has no
  camera until M0-43, so the Editor may print "No cameras rendering"; the UI Toolkit overlay does not need one.
- The label has a fixed top-left position; safe-area padding and touch-target sizes come with M0-59.

Last updated: M0-39
