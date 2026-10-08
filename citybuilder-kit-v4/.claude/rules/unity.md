---
paths:
  - "client/**"
---

# Unity client rules (Unity 6.3 LTS)

Full guide: `core-instructions/09-unity-client.md`.

- The client renders server snapshots. It never computes balances, costs or timers that are sent anywhere.
- Display prediction runs `Simulator.Advance` on a **copy** of the snapshot; replace it on every server response.
- Ignore snapshots with a lower `stateVersion` than the one shown.
- No optimistic economy: don't subtract resources, change worker counts or show a finished building before the server confirms.
- Worker steppers send an absolute target count, debounced; research and build cards show server-content costs only.
- One composition root (`GameRoot`). No singletons, no static mutable state, no `FindObjectOfType` lookups at runtime.
- Networking only in `CityBuilder.Client.Net`; views never call `ApiClient` directly.
- UI with UI Toolkit (UXML/USS). Scene objects created from code. Don't hand-edit `.unity`/`.prefab` YAML.
- No allocations or LINQ in `Update()`. No per-frame server calls.
- Use `Awaitable`/`async` with `destroyCancellationToken`; never `async void` except Unity event handlers.
- C# 9 only (Unity's compiler): no file-scoped namespaces, no global usings.
- Every new script folder needs the correct `.asmdef`; tests go in `Assets/_Project/Tests/EditMode|PlayMode`.
- Commit `.meta` files with their assets. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`.
- Anything requiring the Editor GUI → write numbered **[MANUAL]** steps in your report.
