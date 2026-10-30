# ADR-0007 — One shared engine: the server runs it for authority, the client for display only

- **Status:** Accepted
- **Date:** 2026-10-04 (decided with the kit; written down 2026-10-19) · **Task:** M0-18 · **Decision row:** `core-instructions/12-decisions.md` #007

## Context
- The server owns every number (ADR-0001), and the client only renders snapshots. It polls every 2 minutes (ADR-0003), but players watch
  progress bars, countdowns and stock counters move every second.
- What happens between snapshots follows exact rules: cycles end at whole seconds, outputs land, the next cycle starts, a stall appears
  (`04`). A rough client-side estimate would show numbers that jump at every poll; a second implementation of the rules would drift.
- The server is .NET 10; the client is Unity 6.3 (Mono in the Editor, IL2CPP on devices), which compiles C# 9 against `netstandard2.1`.
- A rule must give the same result on all three runtimes, or the client's display disagrees with the server's authority.

## Decision
The rules live once, in `shared/Simulation/` (`CityBuilder.Simulation`). The server compiles it to settle cities and apply commands; the
Unity client imports the same source as a local package and runs it **for display only**: on a copy of the last snapshot, up to its
display clock, snapping to every new snapshot and never sending a predicted value (`09`, display prediction). Details:
- **One source, two builds:** Unity reads `shared/Simulation/` (`package.json`, an asmdef with `noEngineReferences: true`) through
  `"com.citybuilder.simulation": "file:../../../shared/Simulation"`; .NET builds the same files from `shared/Simulation.Build/`, a sibling
  folder, because Unity imports everything inside a package folder, `bin/` and `obj/` too (`03`).
- **The common subset:** `netstandard2.1` and C# 9, no packages, `#nullable enable` in every file (decision 021); the build fails if the
  target framework, the language version or warnings-as-errors change (`GuardUnityCompatibleBuild`).
- **Determinism on every runtime:** integers only, ordinal ids, no `Dictionary` order, no clock, randomness, I/O or mutable statics
  (`04` §9). Display estimates are helpers of their own (`Display/`, `04` §15) that the server never calls.
- **The fallback** (agreed in advance, `12`): if the Unity local-package integration blocks progress for more than one session, the client
  uses only the snapshot's timer fields for progress bars (phase and research start and end) and refreshes counters on poll. Record the
  switch in `12`. The plan also allows it as a cut if M0 falls behind (`docs/plan/M0-PLAN.md`, realism check).

## Alternatives considered
| Option | Pros | Cons | Why not |
|---|---|---|---|
| The client shows only snapshot values and polls (the fallback as the plan) | No engine on the client | Counters and stalls change only every 2 minutes; rates and countdowns need extra fields or endpoints | It stays the fallback; the shared engine costs little more |
| A second, simpler implementation on the client | Free choice of language features | Every rule twice; the copies drift, and the display disagrees with the server exactly where players look | Drift is a bug class the shared source removes |
| A compiled DLL copied into the Unity project | Any build setup on the .NET side | A binary in the repository or a copy step; the source Unity runs can't be read or reviewed; versions can drift | The local package needs no copy |
| A newer C# or target framework with polyfills | Records, newer syntax | Unity 6.3 compiles C# 9 against `netstandard2.1`; newer syntax fails there, and shims cover only a few features (`init` through `IsExternalInit`) | Unity sets the limit |
| A separate repository or a NuGet package for the engine | Explicit versions | Versioning work for one team; Unity doesn't read NuGet packages | Overhead without a reader who needs it |

## Consequences
- **Easier:** bars, countdowns and counters follow the server's rules between polls, so the snap at the next snapshot is usually
  invisible. One rule test suite covers both sides, and Unity EditMode tests run the engine on Unity's runtime. The same factory validates
  content on both sides (decision 027; the client's side is M0-38). The balance simulator (M0-67) reuses the engine.
- **Harder:** the engine is held to C# 9 and `netstandard2.1` (no records or `init` without a shim, no file-scoped namespaces, no
  `required`) and to no packages. It must stay free of Unity, ASP.NET and EF Core. A client with an older engine predicts with older rules
  until its next snapshot: wrong for a moment, never wrong in the authority.
- **Watch:** architecture and project tests (`SimulationDependencyTests`, `SimulationBuildProjectTests`, `SimulationPackageTests`,
  `ClientAssemblyTests`); a Unity upgrade that changes the C# version; Unity compiling every engine change (`scripts/dev/test-unity.sh`).
  Content that uses a new effect type fails validation on a client whose engine doesn't know it (M0-38 shows a fatal error then), so such
  content needs clients that do; decide how clients are made to update before the first engine change after release.
- **As built (M0-18):** the package, the `.NET` project with its build guard and the architecture tests (M0-03); the Unity project with the
  local package (M0-04); Unity EditMode tests that run content validation, mutations and `Advance` in Unity (`SimulationInUnityTests`,
  `ContentInUnityTests`, `CityRulesInUnityTests`, `SimulatorInUnityTests`; M0-05 on); the display helpers (M0-17). The client's content
  (M0-38: built through the same factory) and its store with the display prediction (M0-39) come next. The fallback has not been needed.
- **Reversal:** the client can switch to the fallback at any time, because the server never depends on the client's engine; the cost is
  jumpier numbers between polls.
