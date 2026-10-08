---
paths:
  - "shared/**"
  - "server/tests/CityBuilder.Simulation.Tests/**"
  - "tools/BalanceSim/**"
---

# Shared simulation engine rules

Spec: `core-instructions/04-simulation-model.md`. This code is compiled by **both** .NET 10 (server)
and Unity 6.3 (client, Mono/IL2CPP), and it must behave identically in both.

- Target `netstandard2.1`, `LangVersion 9.0`. Forbidden: file-scoped namespaces, global usings,
  `required`, records / `init` (unless an internal `IsExternalInit` shim exists), raw string literals.
- Add `#nullable enable` at the top of every file.
- Integers only: `long` for quantities, seconds and labor; `int` for ids and permille. No `float`, `double` or `decimal`.
- Durations use one shared `CeilDiv(long a, long b)` helper (a ≥ 0, b > 0). No other division in game logic.
- No `DateTime`, `TimeProvider`, `Random`, `Guid.NewGuid`, I/O, logging, or statics with mutable state.
- Never iterate a `Dictionary`/`HashSet` where order affects results. Sort keys with
  `StringComparer.Ordinal`, or use arrays indexed by sorted ids.
- `checked` arithmetic on quantities and labor. Validate inputs at the boundary (content loader, mutation entry points).
- Mutations return a result object (`Ok` or an error code); they never throw for business rules.
- Event sources in `Advance` (research timer, building timers, later mailbox deliveries) are gathered generically,
  so new sources are additive.
- Era, research and effects are data. Never write `if (researchId == "…")` or `if (era == 2)` in engine code.
  Add an effect type instead.
- Keep `Advance` allocation-light (it can run ~200k steps).
- Source lives in `shared/Simulation/Runtime/`. `shared/Simulation.Build/CityBuilder.Simulation.csproj` compiles it
  with `EnableDefaultCompileItems=false` and `<Compile Include="../Simulation/Runtime/**/*.cs" />`.
  Never put `bin/` or `obj/` inside `shared/Simulation/`.
- `Runtime/CityBuilder.Simulation.asmdef` must keep `"noEngineReferences": true`.
- Every rule change: update the spec file, add rule tests, keep the property tests green.
