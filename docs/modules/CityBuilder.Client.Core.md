# CityBuilder.Client.Core

**Path:** `client/CityBuilder/Assets/_Project/Core/` · **Kind:** Unity assembly (`CityBuilder.Client.Core.asmdef`, C# 9) ·
**References:** `CityBuilder.Simulation` (the local package `com.citybuilder.simulation`) ·
**Referenced by:** `CityBuilder.Client.Net`, `CityBuilder.Client.Presentation`, `CityBuilder.Client.Tests.EditMode`, `CityBuilder.Client.Tests.PlayMode`

> *Skeleton.* The assembly exists and is wired into the dependency chain, and holds one small type, `AppInfo`. The city store and the display
> clock come with M0-39 *(planned)*.

## Purpose
The client's state that is not about pixels or HTTP: the last authoritative snapshot and when it was received, the display clock, and
the display prediction that runs the shared engine on a **copy** of the snapshot (`core-instructions/09-unity-client.md` → Runtime
structure). It is **not** responsible for talking to the server (`CityBuilder.Client.Net`), for scenes, UI or input
(`CityBuilder.Client.Presentation`), or for deciding anything: the server is the authority and the client only displays.

## Key types
| Type | Responsibility |
|---|---|
| `AppInfo` | static, read-only facts about this build. `Version` is `Application.version`, the "Version" field of Project Settings → Player (`bundleVersion`, `0.1.0` today); M0-58 sends it to the server |
| — | planned: `ClientCityStore`, `DisplayClock`, `DisplayCity` (M0-39) |

## Rules and invariants
- References only `CityBuilder.Simulation` (`03` dependency table). The asmdef is not auto-referenced, so other assemblies must name it,
  and `overrideReferences` is on with no precompiled DLL listed, so Core cannot see Newtonsoft or any other package DLL by accident.
  `ClientAssemblyTests` (`server/tests/CityBuilder.Simulation.Tests`) checks all of this.
- Keeps engine references on purpose: `ClientCityStore` reads `Time.realtimeSinceStartupAsDouble` (M0-39). Everything else here should
  still be plain C# that EditMode tests can run without a scene.
- No singletons and no static mutable state; `GameRoot` builds the objects by hand (`.claude/rules/unity.md`).
- A snapshot with a lower `stateVersion` than the one held is ignored; predicted values never leave the client.
- Unity compiles this as C# 9: no file-scoped namespaces, no global usings.

## How to extend
A new client-side state type goes here as one class per file with its namespace under `CityBuilder.Client.Core`; a new Unity assembly or
folder area follows the recipe "New project, assembly or folder area" in `core-instructions/19-change-recipes.md`.

## Tests
`Assets/_Project/Tests/EditMode` (`CityBuilder.Client.Tests.EditMode`), run with `scripts/dev/test-unity.sh` (`client/README.md` → Run the tests):
- `AppInfoTests`: `Version` is the bundle version of the Player Settings (`PlayerSettings.bundleVersion`) and is not blank.
- `SimulationInUnityTests`: `IntegerMath.CeilDiv` called from the local package `com.citybuilder.simulation` rounds up. It proves that the engine
  compiles and runs inside Unity; the engine's rules are tested in `server/tests/CityBuilder.Simulation.Tests`.

## Known limitations
- Only `AppInfo` exists (skeleton).

Last updated: M0-05
