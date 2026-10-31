# CityBuilder.Client.Core

**Path:** `client/CityBuilder/Assets/_Project/Core/` · **Kind:** Unity assembly (`CityBuilder.Client.Core.asmdef`, C# 9) ·
**References:** `CityBuilder.Simulation` (the local package `com.citybuilder.simulation`) ·
**Referenced by:** `CityBuilder.Client.Net`, `CityBuilder.Client.Presentation`, `CityBuilder.Client.Tests.EditMode`, `CityBuilder.Client.Tests.PlayMode`

## Purpose
The client's state that is not about pixels or HTTP: the last authoritative snapshot and when it was received, the display clock, and
the display prediction that runs the shared engine on a **copy** of the snapshot (`core-instructions/09-unity-client.md` → Runtime
structure). It is **not** responsible for talking to the server (`CityBuilder.Client.Net`), for scenes, UI or input
(`CityBuilder.Client.Presentation`), or for deciding anything: the server is the authority and the client only displays.

## Key types
| Type | Responsibility |
|---|---|
| `AppInfo` | static, read-only facts about this build. `Version` is `Application.version`, the "Version" field of Project Settings → Player (`bundleVersion`, `0.1.0` today); M0-58 sends it to the server |
| `CitySnapshot` | One server snapshot in the engine's terms: version, content version, clock, and a city that only leaves as a copy (`CopyCity()`), so the state the store holds cannot change. `SnapshotMapper` (Net) builds it, because Core cannot name the DTOs |
| `ClientCityStore` | The last snapshot and its receive time. `Apply` takes a snapshot whose `(stateVersion, clock)` is greater than the held one's and raises `SnapshotChanged`; anything else is ignored. The default constructor reads `Time.realtimeSinceStartupAsDouble`, tests pass their own clock |
| `DisplayClock` | `snapshot.clock + whole seconds since receipt × timeScale`, for display only |
| `DisplayCity` | The predicted city: `Simulator.Advance` on a fresh copy of the snapshot up to the clock. `Refresh()` predicts at most once per second and allocates nothing otherwise; a new snapshot replaces the prediction at once. Raises `Changed`; views read `Current` |

## Rules and invariants
- References only `CityBuilder.Simulation` (`03` dependency table). The asmdef is not auto-referenced, so other assemblies must name it,
  and `overrideReferences` is on with no precompiled DLL listed, so Core cannot see Newtonsoft or any other package DLL by accident.
  `ClientAssemblyTests` (`server/tests/CityBuilder.Simulation.Tests`) checks all of this.
- Keeps engine references on purpose: `ClientCityStore` reads `Time.realtimeSinceStartupAsDouble` and `DisplayCity` logs with `Debug`. Everything
  here is plain C# that EditMode tests can run without a scene.
- No singletons and no static mutable state; `GameRoot` builds the objects by hand (`.claude/rules/unity.md`).
- A snapshot with a lower `stateVersion` than the one held is ignored, and so is the same version with the same or an earlier clock (build log
  M0-39, decision 4); predicted values never leave the client.
- Each prediction starts again from the snapshot, never from the last prediction: the 8-hour production cap counts from the start of an advance.
- When the engine refuses a city, `DisplayCity` logs one error and shows the snapshot unadvanced until the next one arrives.
- Unity compiles this as C# 9: no file-scoped namespaces, no global usings.
- Every type and member, private ones included, has an XML doc comment (`core-instructions/14-coding-standards.md` → Documentation
  in code). Unity's compiler doesn't check them, so `CityBuilder.Repository.Tests` (a server test project) checks every script
  under `Assets/_Project`.

## How to extend
A new client-side state type goes here as one class per file with its namespace under `CityBuilder.Client.Core`; a new Unity assembly or
folder area follows the recipe "New project, assembly or folder area" in `core-instructions/19-change-recipes.md`.

## Tests
`Assets/_Project/Tests/EditMode` (`CityBuilder.Client.Tests.EditMode`), run with `scripts/dev/test-unity.sh` (`client/README.md` → Run the tests):
- `AppInfoTests`: `Version` is the bundle version of the Player Settings (`PlayerSettings.bundleVersion`) and is not blank.
- `SimulationInUnityTests`: `IntegerMath.CeilDiv` called from the local package `com.citybuilder.simulation` rounds up. It proves that the engine
  compiles and runs inside Unity; the engine's rules are tested in `server/tests/CityBuilder.Simulation.Tests`.
- `Core/ClientCityStoreTests`: newer replaces, older and same-city answers are ignored, whole seconds, a copy holds every value and leaves the snapshot alone.
- `Core/DisplayCityTests`: the prediction equals one `Simulator.Advance` exactly on real snapshots at seconds up to past the production cap, a research
  completes on its second, a construction auto-staffs, at most one prediction per second, a new snapshot wins, the time scale, an engine refusal.

## Known limitations
- A lower `stateVersion` is ignored for as long as the app runs, so after a dev database reset the store keeps the old city until the app restarts.
- `Time.realtimeSinceStartupAsDouble` may or may not count while the app is in the background, depending on the platform. The poll on resume
  (M0-41 *(planned)*) replaces the snapshot, so this lasts only until that answer arrives.
- `CitySnapshot` has no settlement block yet; the welcome-back report adds it (M0-53 *(planned)*).

Last updated: M0-39
