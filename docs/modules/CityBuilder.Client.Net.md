# CityBuilder.Client.Net

**Path:** `client/CityBuilder/Assets/_Project/Net/` · **Kind:** Unity assembly (`CityBuilder.Client.Net.asmdef`, C# 9) ·
**References:** `CityBuilder.Client.Core`, the precompiled `Newtonsoft.Json.dll` of the package `com.unity.nuget.newtonsoft-json` 3.2.2, and the engine module `UnityWebRequest` (`com.unity.modules.unitywebrequest`, named in the manifest) ·
**Referenced by:** `CityBuilder.Client.Presentation`, `CityBuilder.Client.Tests.EditMode`, `CityBuilder.Client.Tests.PlayMode`

> *Skeleton.* No networking exists yet and none may be added before M0-37 (Unity warns that the assembly "has no scripts associated with
> it" until then). The API client and the DTOs come with M0-37 *(planned)*.

## Purpose
All talking to the server: the HTTP client, the DTOs that mirror `core-instructions/05-api-contract.md`, and the translation of
ProblemDetails responses into an `ApiError`. It is **not** responsible for gameplay logic, for what a response means to the city
(`CityBuilder.Client.Core`), or for any UI: views never call the API client directly (`.claude/rules/unity.md`).

## Key types
| Type | Responsibility |
|---|---|
| — | none yet. Planned: `ApiClient`, the DTOs, `ApiError` (M0-37); `CommandSender` (M0-40) |

## Rules and invariants
- Networking lives only in this assembly. It references `CityBuilder.Client.Core` and Newtonsoft, nothing else of the project.
- The asmdef sets `overrideReferences` and lists `Newtonsoft.Json.dll` under `precompiledReferences`. The package's DLL is
  auto-referenced, so an asmdef without `overrideReferences` would see it, and every other auto-referenced DLL of every package, too.
  With the setting on, an assembly sees only the DLLs it lists: Net lists Newtonsoft, Core and Presentation list none. The asmdef is not
  auto-referenced.
- The client never sends balances, costs, rates, timers or player ids (`core-instructions/02-principles.md` rules 1 and 2).
- Async work uses `Awaitable` or `Task` with a cancellation token (`destroyCancellationToken` in MonoBehaviours); no `async void`
  except Unity event handlers.
- IL2CPP may strip DTO members that only reflection uses; a `link.xml` that keeps this assembly belongs to the first DTOs (M0-37)
  (`09` → Device testing).

## How to extend
A new endpoint or DTO: recipe "New command (player action)" in `core-instructions/19-change-recipes.md` (client intent in this assembly
plus UI in `CityBuilder.Client.Presentation`).

## Tests
`Assets/_Project/Tests/EditMode` (`CityBuilder.Client.Tests.EditMode`): fixture-based DTO tests with a fake transport (M0-37, M0-40
*(planned)*). The assembly exists and has no tests yet. The asmdef itself (its references, the Newtonsoft DLL) is guarded by
`ClientAssemblyTests` in `server/tests/CityBuilder.Simulation.Tests`, run with `dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`.

## Known limitations
- No types yet (skeleton).

Last updated: M0-04
