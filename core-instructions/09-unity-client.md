# Unity Client

> **Built so far:** the project, its four assemblies and their tests, and the `Bootstrap` scene that shows "Not connected" (M0-04, M0-05;
> `client/README.md`). The API client, the DTOs and the error parsing (`CityBuilder.Client.Net`, M0-37) and the start of the game (login, content through the engine's factory, the first `GET /city`, "Connected" or the fatal error screen: `AuthService`, `ContentService`, `StartupFlow`, `GameRoot`, M0-38) and the city store with its display prediction (`ClientCityStore`, `DisplayClock`, `DisplayCity`, M0-39) and the command sender with its retry policy (`CommandSender`, M0-40) and the polling with the connection state (`ConnectionMonitor`, `PollScheduler`, M0-41) are built and tested; the city view with M0-43, the screens from M0-45 *(planned)*. A task that builds a
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
3. **Placeholder art from primitives** (`GameObject.CreatePrimitive`) and simple materials in M0.
   No prefabs are required for the MVP; if one is needed, create it with an editor script.
4. When a step needs the Editor (creating the project, assigning a scene to Build Settings,
   pressing Play), write exact click-by-click **[MANUAL]** instructions instead of guessing.

## Assemblies (asmdef)

| Assembly | Folder | References |
|---|---|---|
| `CityBuilder.Simulation` | `shared/Simulation/Runtime` (local package, `noEngineReferences: true`) | — |
| `CityBuilder.Client.Core` | `Assets/_Project/Core` | Simulation |
| `CityBuilder.Client.Net` | `Assets/_Project/Net` | Core, Simulation, Newtonsoft |
| `CityBuilder.Client.Presentation` | `Assets/_Project/Presentation` | Core, Net, Simulation, Input System (UI Toolkit is an engine module: no entry) |
| `CityBuilder.Client.Tests.EditMode` / `.PlayMode` | `Assets/_Project/Tests/…` | as needed |

Local package line in `Packages/manifest.json`:
`"com.citybuilder.simulation": "file:../../../shared/Simulation"`

## Runtime structure
- **Composition root:** `GameRoot` builds services by hand (no DI framework, no singletons, no static mutable state).
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
  repeat, then `Unauthorized`; content of another version is `IncompatibleVersion` and is not applied. `GameRoot` ticks it in `Update` and shows the state.

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
The Editor must be closed for that project (project lock). `scripts/dev/test-unity.sh` (and `.ps1`) wraps this for EditMode then PlayMode: it reads the
Unity program from `UNITY_EDITOR`, prints a summary and exits non-zero on a failure or when no test ran (`client/README.md` → Run the tests).

## Device testing
Android device → server on your PC: use the PC's LAN IP or `adb reverse tcp:8080 tcp:8080`.
IL2CPP may strip DTO members used only via reflection: add a `link.xml` preserving the DTO assembly.
