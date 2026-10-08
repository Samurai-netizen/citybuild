# Unity Client

The client renders the latest authoritative snapshot and makes waiting feel alive.
It never decides outcomes.

## Project settings (set once, Day 1 — [MANUAL] where noted)
- Unity **6.3 LTS**, project at `client/CityBuilder/`, Universal 3D template. [MANUAL: create in Unity Hub]
- Asset serialization: **Force Text**; Version control: **Visible Meta Files**. Commit every `.meta`.
- Active Input Handling: Input System. Scripting backend for device builds: IL2CPP.
- Player → "Allow downloads over HTTP": *Allowed in Development Builds* (needed for `http://localhost`).
- `client/CityBuilder/.gitignore` (inside the Unity folder only — the root must not ignore server
  `.csproj` files): `Library/ Temp/ Logs/ obj/ Build/ Builds/ UserSettings/ *.csproj *.sln`.

## AI-friendly Unity rules (Claude cannot use the Editor GUI)
1. **Minimal scenes.** `Bootstrap.unity` contains one `GameRoot` GameObject. Everything else is
   created from code at runtime. Avoid hand-editing scene or prefab YAML.
2. **UI Toolkit** (UXML + USS text files) for all screens and HUD — readable and diffable.
3. **Placeholder art from primitives** (`GameObject.CreatePrimitive`) and simple materials in M0.
   No prefabs are required for the MVP; if one is needed, create it with an editor script.
4. When a step needs the Editor (creating the project, assigning a scene to Build Settings,
   pressing Play), write exact click-by-click **[MANUAL]** instructions instead of guessing.

## Assemblies (asmdef)

| Assembly | Folder | References |
|---|---|---|
| `CityBuilder.Simulation` | `shared/Simulation/Runtime` (local package, `noEngineReferences: true`) | — |
| `CityBuilder.Client.Core` | `Assets/_Project/Core` | Simulation |
| `CityBuilder.Client.Net` | `Assets/_Project/Net` | Core, Newtonsoft |
| `CityBuilder.Client.Presentation` | `Assets/_Project/Presentation` | Core, Net, UnityEngine.UIElements, Input System |
| `CityBuilder.Client.Tests.EditMode` / `.PlayMode` | `Assets/_Project/Tests/…` | as needed |

Local package line in `Packages/manifest.json`:
`"com.citybuilder.simulation": "file:../../../shared/Simulation"`

## Runtime structure
- **Composition root:** `GameRoot` builds services by hand (no DI framework, no singletons, no static mutable state).
- **Core:** `ClientCityStore` holds the last snapshot + receive time; raises `SnapshotChanged`.
  Ignores snapshots whose `stateVersion` is lower than the current one (out-of-order responses).
- **Display prediction:** `DisplayClock` = `snapshot.clock + secondsSinceReceived × rules.timeScale`
  (`timeScale` is 1 except on Development servers using `DevTimeScale`). For counters and
  progress bars, run `Simulator.Advance` on a **copy** of the snapshot. Never send predicted values anywhere.
- **Net:** `ApiClient` (UnityWebRequest wrapped in `Awaitable`/`Task`), timeouts, cancellation via
  `destroyCancellationToken`, ProblemDetails parsing into `ApiError { Code, Status, Detail }`.
- **Commands:** `CommandSender` creates the `requestId` once per player intent and reuses it on retry.
  Retries only on network errors, timeouts, 5xx, `CONCURRENCY_CONFLICT` (max 3, backoff 0.5/1/2 s).
- **Polling:** `GET /city` on start, on `OnApplicationPause(false)`, after reconnect, every 120 s in foreground.

## M0 screens (UI Toolkit)
HUD (item chips with stock/cap/net rate per hour, workers free/total, builders, research ring, era badge) ·
build menu (locked cards name the research that unlocks them) · building inspector (recipe card, **worker
stepper − / +** with a live rate preview, upgrade section) · research screen (Era I tree: nodes with cost,
time, effect, status; finance button) · welcome-back report · toasts. Market and guild screens come in M2.

## UX rules
- No optimistic economy: never subtract resources, change worker counts or spawn a finished building
  before the server confirms. A "pending" ghost or a pending stepper value is fine.
- The worker stepper sends the **absolute** target count after a short debounce (~400 ms), so rapid taps
  become one command.
- Client-side checks (tile occupied, obviously unaffordable, no free workers, research locked) are
  **UX hints only**; the server re-checks.
- Every error code maps to a human message. Nothing is shown only in logs.
- Touch targets ≥ 48 dp; respect `Screen.safeArea` on the UI root.
- Art and labels are era-aware from the start: look up building visuals by `(typeId, era)`, even though M0 has only Era I.

## Shared-code constraints (C# 9, netstandard2.1)
No file-scoped namespaces, no global usings, no `required`, no records or `init` without an
`IsExternalInit` shim. Put `#nullable enable` at the top of shared files.

## Running tests from the CLI
`Unity -batchmode -projectPath client/CityBuilder -runTests -testPlatform EditMode -testResults <file>`
The Editor must be closed for that project (project lock). Wrap this in `scripts/dev/test-unity.*`.

## Device testing
Android device → server on your PC: use the PC's LAN IP or `adb reverse tcp:8080 tcp:8080`.
IL2CPP may strip DTO members used only via reflection: add a `link.xml` preserving the DTO assembly.
