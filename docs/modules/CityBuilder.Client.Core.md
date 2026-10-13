# CityBuilder.Client.Core

**Path:** `client/CityBuilder/Assets/_Project/Core/` · **Kind:** Unity assembly (`CityBuilder.Client.Core.asmdef`, C# 9) ·
**References:** `CityBuilder.Simulation` (the local package `com.citybuilder.simulation`) ·
**Referenced by:** `CityBuilder.Client.Net`, `CityBuilder.Client.Presentation`, `CityBuilder.Client.Tests.EditMode`, `CityBuilder.Client.Tests.PlayMode`

> *Skeleton.* The assembly exists and is wired into the dependency chain, but has no types yet (Unity warns that it "has no scripts
> associated with it" until the first one arrives). The city store and the display clock come with M0-39 *(planned)*.

## Purpose
The client's state that is not about pixels or HTTP: the last authoritative snapshot and when it was received, the display clock, and
the display prediction that runs the shared engine on a **copy** of the snapshot (`core-instructions/09-unity-client.md` → Runtime
structure). It is **not** responsible for talking to the server (`CityBuilder.Client.Net`), for scenes, UI or input
(`CityBuilder.Client.Presentation`), or for deciding anything: the server is the authority and the client only displays.

## Key types
| Type | Responsibility |
|---|---|
| — | none yet. Planned: `ClientCityStore`, `DisplayClock`, `DisplayCity` (M0-39) |

## Rules and invariants
- References only `CityBuilder.Simulation` (`03` dependency table). The asmdef is not auto-referenced, so other assemblies must name it.
- Keeps engine references on purpose: `ClientCityStore` reads `Time.realtimeSinceStartupAsDouble` (M0-39). Everything else here should
  still be plain C# that EditMode tests can run without a scene.
- No singletons and no static mutable state; `GameRoot` builds the objects by hand (`.claude/rules/unity.md`).
- A snapshot with a lower `stateVersion` than the one held is ignored; predicted values never leave the client.
- Unity compiles this as C# 9: no file-scoped namespaces, no global usings.

## How to extend
A new client-side state type goes here as one class per file with its namespace under `CityBuilder.Client.Core`; a new Unity assembly or
folder area follows the recipe "New project, assembly or folder area" in `core-instructions/19-change-recipes.md`.

## Tests
`Assets/_Project/Tests/EditMode` (`CityBuilder.Client.Tests.EditMode`). The assembly exists and has no tests yet: M0-05 adds the first
ones and `scripts/dev/test-unity.sh` *(planned, M0-05)*.

## Known limitations
- No types yet (skeleton).

Last updated: M0-04
