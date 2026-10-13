# CityBuilder.Client.Presentation

**Path:** `client/CityBuilder/Assets/_Project/Presentation/` · **Kind:** Unity assembly (`CityBuilder.Client.Presentation.asmdef`, C# 9) ·
**References:** `CityBuilder.Client.Core`, `CityBuilder.Client.Net`, `Unity.InputSystem` (UI Toolkit is an engine module, so it needs no entry) ·
**Referenced by:** `CityBuilder.Client.Tests.EditMode`, `CityBuilder.Client.Tests.PlayMode`

> *Early.* Only the composition root, its bootstrap and the "Not connected" label exist. The screens come with the later client tasks
> *(planned, M0-12 on)*.

## Purpose
Everything the player sees and touches: the composition root, scene objects created from code, UI Toolkit screens and input. It renders
what `CityBuilder.Client.Core` holds and forwards player intent to `CityBuilder.Client.Net`. It is **not** responsible for game rules,
for balances or for any decision: it only displays server state.

## Key types
| Type | Responsibility |
|---|---|
| `GameRoot` (`Bootstrap/`) | The composition root, a thin `MonoBehaviour`: the one place that builds services by hand. So far it creates the UI object (a `UIDocument` with `UI/PanelSettings` and the tree `UI/ConnectionStatus`), which shows "Not connected". Missing assets are logged as one error that names them, and the root stays alive without UI |
| `GameBootstrap` (`Bootstrap/`) | `internal static`. Registers a `SceneManager.sceneLoaded` handler before the first scene loads (`RuntimeInitializeOnLoadMethod`, `BeforeSceneLoad`); when the scene named `Bootstrap` loads, it creates one `GameRoot` in that scene. A `GameRoot` already in the scene (a root or any descendant) is kept and nothing is added. It holds no field |

Assets the types load by name sit in `Assets/_Project/Resources/UI/`, not in this folder, because a `GameRoot` made from code has no scene
field to assign them to: `PanelSettings.asset` (the owner's, with the default runtime theme), `ConnectionStatus.uxml` and
`ConnectionStatus.uss` (the label, name `connection-status`, class `connection-status`).

## Rules and invariants
- References Core, Net and the Input System package (`03` and `09`); the asmdef is not auto-referenced.
- Views never call the API client; they use services that `GameRoot` builds by hand (no DI framework, no singletons, no static mutable
  state, no `FindObjectOfType` at runtime). The only static code is the registration method that Unity's attribute requires.
- The scene that triggers the bootstrap is named exactly `Bootstrap` (`GameBootstrap.BootstrapSceneName`); any other scene gets nothing.
- Screens are UI Toolkit (UXML + USS) loaded from `Resources/UI`; scene objects are created from code. `.unity` and `.prefab` YAML is
  never hand-edited (`.claude/rules/unity.md`).
- MonoBehaviours stay thin; logic lives in plain classes that EditMode tests can run (`14`).

## How to extend
A new screen or view: recipe "New client screen or view" in `core-instructions/19-change-recipes.md` (a folder under
`Presentation/<Feature>/` with a view-model class and a thin MonoBehaviour; its UXML and USS go to `Assets/_Project/Resources/UI/`).
Services that `GameRoot` composes are added in `GameRoot` itself, not through a static or a `Find*` call.

## Tests
`Assets/_Project/Tests/EditMode` (view-model tests) and `Assets/_Project/Tests/PlayMode` (scene behaviour). Both assemblies exist and have
no tests yet; M0-05 adds the first ones, among them "the Bootstrap scene gets exactly one `GameRoot`". M0-04 checked that behaviour in a
throw-away copy of the project (headless PlayMode run: one `GameRoot`, one `UIDocument`, the label text, no warnings or errors, a rendered
picture of the label, no spawn in another scene, a reload keeps one root, a pre-placed root is kept); that test code is not in the repository.

## Known limitations
- `Assets/Scenes/Bootstrap.unity` is created by hand ([MANUAL], `client/README.md`); until it exists nothing runs. Its Game view has no
  camera until M0-43, so the Editor may print "No cameras rendering"; the UI Toolkit overlay does not need one.
- The label has a fixed top-left position; safe-area padding and touch-target sizes come with M0-59.

Last updated: M0-04
