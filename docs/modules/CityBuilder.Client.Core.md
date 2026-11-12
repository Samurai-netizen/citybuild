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
| `AppInfo` | static, read-only facts about this build. `Version` is `Application.version`, the "Version" field of Project Settings → Player (`bundleVersion`, `0.1.0` today); `ApiClient` sends it in `X-Client-Version` on every request (M0-58) |
| `CitySnapshot` | One server snapshot in the engine's terms: version, content version, clock, and a city that only leaves as a copy (`CopyCity()`: new buildings with their stores and needs and a new research, whose needs a delivery changes in place; trips are values and shared; CR-0008; each courier is copied, since it walks and loads in place, and routes are shared, part G; a building's `Paused` flag and the couriers' delivery lines are copied too, CR-0008-T3), so the state the store holds cannot change. It also carries the `SettlementSummary` of the answer (null for a snapshot made without one). `SnapshotMapper` (Net) builds it, because Core cannot name the DTOs |
| `SettlementSummary` | What the settlement of one request did, in the engine's terms (M0-53): elapsed seconds, produced and consumed per item, the couriers' `CourierSeconds` and `TiredCouriers` (the engine's `TiredCourier`; CR-0008-T1 part H, for T2's report), completed building ids, completed research ids, era changed. Immutable, copies its lists, and refuses values no settlement has (a negative number). The welcome-back report reads it. CR-0008-T2 part E adds `Delivered` and `Moved` per item (optional last constructor arguments; `SnapshotMapper` fills them from the snapshot) |
| `ClientCityStore` | The last snapshot and its receive time. `Apply` takes a snapshot whose `(stateVersion, clock)` is greater than the held one's and raises `SnapshotChanged`; anything else is ignored. `Replace` takes one whatever is held, for the answer right after a new login, which may be another city (CR-0004). The default constructor reads `Time.realtimeSinceStartupAsDouble`, tests pass their own clock |
| `DisplayClock` | `snapshot.clock + whole seconds since receipt × timeScale` (`Now`), and `PreciseNow`, the same with the fraction of a second (`ClientCityStore.SecondsSinceReceived`), which moves the walkers on the map smoothly; for display only |
| `ConnectionState` | How the client stands with the server: `Connecting`, `Connected`, `Offline`, `Unauthorized`, `ServerError`, `IncompatibleVersion`. Only the names; the rules that move between them are `ConnectionMonitor`'s (Net) |
| `PollScheduler` | When to ask for the city again (M0-41): due 120 s after the last poll ended (`DefaultIntervalSeconds`), at once after `Resume()` or `RequestPoll()`, never while `Pause()`d or while a poll runs. The caller does `TryBegin()` then `End()`. Reads an injected real-time clock, so tests move time by hand; `MarkFetched()` restarts the interval when someone else fetched the city |
| `DisplayCity` | The predicted city: `Simulator.Advance` on a fresh copy of the snapshot up to the clock. `Refresh()` predicts at most once per second and allocates nothing otherwise; a new snapshot replaces the prediction at once. Raises `Changed`; views read `Current`. `SnapshotReplaced` hands over the city shown until a new snapshot came (M0-54) |
| `ClientLogger`, `LogLevel`, `ILogSink` | The logger (M0-57): a level (`Debug`, `Info`, `Warning`, `Error`, `Off`) and a sink fixed at construction; a line below the level is dropped, `IsEnabled` lets a caller skip building a message. `Exception(e)` is one Error line with the stack trace. A class whose log a test reads takes a `ClientLogger` over a recording sink (`ApiClient`, `CommandFeedback`) |
| `ClientLog`, `UnityLogSink` | The game's logger: `ClientLog.Default` is `ClientLogger` at the build's level (`DefaultLevelFor`: Debug in the Editor and development builds, Warning in release) over `UnityLogSink`, the only runtime code that calls `UnityEngine.Debug` to log. `ClientLog.Debug/Info/Warning/Error/Exception` forward to `Default`, so code with no logger handed to it logs in one line. Nothing in them can be changed after the start |
| `PredictionWatch` | A development check (M0-54): at each new snapshot it reports when the display's clock went back and, with no command on its way, when the snapshot differs from `Simulator.Advance` of the one before it, with the cities as JSON (`ToJson`: every stored value, the roads included since CR-0008-T1 part D, the purse, stores, needs, the research's needs and the trips since part E, the couriers and routes since part G, the delivery lines and each building's `paused` since CR-0008-T3; the clock-back report lists the city totals that fell). `GameRoot` makes it when `Debug.isDebugBuild`, reporting to `Debug.LogWarning`; it changes nothing the player sees |

## Rules and invariants
- References only `CityBuilder.Simulation` (`03` dependency table). The asmdef is not auto-referenced, so other assemblies must name it,
  and `overrideReferences` is on with no precompiled DLL listed, so Core cannot see Newtonsoft or any other package DLL by accident.
  `ClientAssemblyTests` (`server/tests/CityBuilder.Simulation.Tests`) checks all of this.
- Keeps engine references on purpose: `ClientCityStore` reads `Time.realtimeSinceStartupAsDouble` and `DisplayCity` logs with `ClientLog`. Everything
  here is plain C# that EditMode tests can run without a scene.
- No singletons and no static mutable state; `GameRoot` builds the objects by hand (`.claude/rules/unity.md`).
- No log line holds a token or a request or response body, in any build (`07` Logging policy). A failed request is logged as its `ApiError.ToString()`: code, status, detail and the server's trace id. `LoggingPolicyTests` fails when runtime code outside `UnityLogSink` calls `Debug.Log*`. `ClientLog` has one `static readonly` field and no settable state, so the rule "no static mutable state" holds (build log M0-57, decision 3).
- A snapshot with a lower `stateVersion` than the one held is ignored, and so is the same version with the same or an earlier clock (build log
  M0-39, decision 4); predicted values never leave the client.
- Each prediction starts again from the snapshot, never from the last prediction, so it is the server's one advance of the whole time (with the
  player present: the display never spends stamina, CR-0008-T1 decision 57).
- `DisplayCity` is disposed and made again when the content is replaced (M0-58); a disposed one keeps the city it last showed and ignores the store.
- When the engine refuses a city (an unknown type or level, a timer before the clock, an overflow), `DisplayCity` logs one error and shows the snapshot unadvanced until the next one arrives. It never throws, because it runs from `Update` and from the store's event, inside whoever gave the store the snapshot (CR-0004).
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
- `Core/ClientLogTests`: the level filter, `IsEnabled`, the default level per build type, an exception is one Error line with the exception, a logger needs a sink. `Core/LoggingPolicyTests`: no runtime file but `UnityLogSink` calls `Debug.Log*`.
- `Core/ClientCityStoreTests`: newer replaces, older and same-city answers are ignored, whole seconds, a copy holds every value and leaves the snapshot alone.
- `Core/SettlementSummaryTests`: every value kept (the couriers' too), the lists copied, impossible values and null lists refused, a snapshot made without an answer has no settlement.
- `Core/PollSchedulerTests`: not due at the start, due after exactly 120 s from the end of the last poll, nothing while paused whatever was requested, a poll at once on resume, never two at a time, a request during a poll is served after it, bad constructor arguments.
- `Core/DisplayCityTests`: the prediction equals one `Simulator.Advance` exactly on real snapshots at seconds up to 10 hours, a research
  completes on its second, a construction auto-staffs, at most one prediction per second, a new snapshot wins, the time scale, an engine refusal.
- `Core/PredictionWatchTests`: a snapshot as predicted reports nothing, going back and mismatches are reported (with both cities), nothing while a command is pending, the same city the same
  text, and a city with a road a different text (CR-0008-T1 part D).

## Known limitations
- A lower `stateVersion` is ignored for as long as the app runs, so after a dev database reset the store keeps the old city until the app restarts.
- `Time.realtimeSinceStartupAsDouble` may or may not count while the app is in the background, depending on the platform. The poll on resume
  (`PollScheduler`, M0-41) replaces the snapshot, so this lasts only until that answer arrives.

Last updated: CR-0008-T3
