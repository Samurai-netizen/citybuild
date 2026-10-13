# CityBuilder.Client.Presentation

**Path:** `client/CityBuilder/Assets/_Project/Presentation/` · **Kind:** Unity assembly (`CityBuilder.Client.Presentation.asmdef`, C# 9) ·
**References:** `CityBuilder.Client.Core`, `CityBuilder.Client.Net`, `Unity.InputSystem` (UI Toolkit is an engine module, so it needs no entry) ·
**Referenced by:** `CityBuilder.Client.Tests.EditMode`, `CityBuilder.Client.Tests.PlayMode`

> *Skeleton.* The assembly exists; its first types (`GameRoot` and the bootstrap) arrive in the next step of M0-04.

## Purpose
Everything the player sees and touches: the composition root, scene objects created from code, UI Toolkit screens and input. It renders
what `CityBuilder.Client.Core` holds and forwards player intent to `CityBuilder.Client.Net`. It is **not** responsible for game rules,
for balances or for any decision: it only displays server state.

## Key types
| Type | Responsibility |
|---|---|
| — | none yet |

## Rules and invariants
- References Core, Net and the Input System package (`03` and `09`); the asmdef is not auto-referenced.
- Views never call the API client; they use services that `GameRoot` builds by hand (no DI framework, no singletons, no static mutable
  state, no `FindObjectOfType` at runtime).
- Screens are UI Toolkit (UXML + USS); scene objects are created from code. `.unity` and `.prefab` YAML is never hand-edited
  (`.claude/rules/unity.md`).
- MonoBehaviours stay thin; logic lives in plain classes that EditMode tests can run (`14`).

## How to extend
A new screen or view: recipe "New client screen or view" in `core-instructions/19-change-recipes.md` (a folder under
`Presentation/<Feature>/` with a view-model class and a thin MonoBehaviour; UXML and USS are loaded from `Assets/_Project/Resources/UI/`).

## Tests
`Assets/_Project/Tests/EditMode` (view-model tests) and `Assets/_Project/Tests/PlayMode` (scene behaviour). Both assemblies exist and have
no tests yet; M0-05 adds the first ones.

## Known limitations
- No types yet (skeleton).

Last updated: M0-04
