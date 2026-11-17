# BalanceSim

**Path:** `tools/BalanceSim/` · **Kind:** .NET project (console, net10.0) · **References:** `CityBuilder.Simulation`,
`CityBuilder.Infrastructure` (its content loader only) · **Referenced by:** `BalanceSim.Tests`

## Purpose
BalanceSim plays the real content on the shared engine and reports the numbers `core-instructions/24-economy-design.md` asks for
(→ "Metrics we track", "Balance simulator"). It uses simple, documented player strategies and check-in patterns, and needs no server,
database or client. A content change runs it first (`core-instructions/19-change-recipes.md` → Content-only change). It never changes
content or code, it is never deployed, and its strategies are heuristics, not a model of real players. How to run it:
[run-balance-simulator.md](../how-to/run-balance-simulator.md).

## How a run works
1. A new city from `CityFactory.CreateStarterCity` (the content's starting city). City clock 0 is the first check-in.
2. **Check-ins** come from the pattern (`SessionPattern`). The first lasts `--first-session-minutes` (30), later ones
   `--session-minutes` (5). While checked in, the strategy acts every 10 s (`RunOptions.PresentTickSeconds`) and the player is present.
3. **Between check-ins** the city advances in steps of at most 60 s (`RunOptions.AbsentTickSeconds`). The player counts as absent from
   the check-in's end plus `--grace-seconds` (300, the server's `Simulation:PresenceGraceSeconds`), so couriers spend stamina (`04` §2).
4. Every action goes through the engine's rules (`CityActions`), and each one that applies ends with a zero-length advance, as a server
   request does. `RunRecorder` advances the city, sums the settlement reports, and measures stall and idle-worker time at the start of
   every step.

## Strategies
Every step of a check-in runs, in order: research, builds and upgrades, workers, logistics. The parts the strategies share:
- **Content, not ids** (`ContentChain`): the coin chain is the producers whose recipe makes the currency, then the producers of their
  inputs, raw first (M0: Woodcutter's Hut, Sawmill, Trading Post). Houses, the science building and courier stations are found by kind.
- **Basic city** (`Construction.NextBasic`): the science building (if research is wanted), one of every chain type, then a courier
  station. A house comes first whenever the workers would not cover the next building's slots. The coins for chain types still missing
  (the basic reserve, locked types included) are kept back from growth, upgrades and research, except research that unlocks one of them.
- **Growth** (`Construction.NextExpansion`, `NextUpgrade`, `NextStationUpgrade`): more couriers when the routes can't hold another chain
  building; otherwise the producer of the first good whose demand at full staffing exceeds its supply, else another coin producer.
  Buildings go on the first free tile in row-major order. Only one project waits for goods at a time, and an upgrade with goods is only ordered
  once every chain type stands and when its goods can reach it without the building itself: other buildings hold them, or make them
  from inputs that can reach them the same way (M0-A: the only hut's upgrade waited for planks made from its own logs).
- **Workers** (`Workforce`): courier stations first, then producers one worker at a time in chain order. Upgrading buildings get none.
  One worker stays free for hand deliveries until a courier station is built.
- **Logistics** (`Logistics`, `Routes`): one route per good (its producers take, its waiting projects and consumers put, all), split into
  balanced slices beyond the route limit. Neighbouring routes are joined when couriers are short. The city's last courier stays free for
  deliveries, and couriers are re-energized at every check-in. Projects and producers no route serves get deliveries (by courier, or by
  hand before a station exists) from the building with the most stock, in the cheapest amount the city may use (`DeliveryAmount`). No
  roads are built.

| Strategy | Research | Builds |
|---|---|---|
| `GreedyCoins` | Only nodes that unlock or speed up a chain type, cheapest first | Grows the chain with every coin; upgrades only when the grid is full |
| `ResearchRush` | Keeps the slot busy: the cheapest available node first | Grows only with the coins above the next node's cost; no upgrades until all research is done, then plays like `Balanced` |
| `Balanced` | Every node, cheapest first | Alternates new buildings and upgrades; keeps the cheapest node's coins back while the research slot is free |

## Output
`--out` (default `artifacts/balance-sim/`, ignored by git) gets one folder per strategy × pattern, `<strategy>-<pattern slug>/`, and
`index.md` comparing the runs.
- `summary.md`: the settings; milestones (first research, first coins, each research started and done, all research, each building type
  first built and first at its max level, every type at max level); coins produced (per hour and per day) and spent (placements,
  upgrades, research); goods produced, used, delivered and moved; workers (average, idle share); producer time by state (running,
  upgrading, each stall reason of `04` §4); logistics; the city at the end; rejected actions (none expected).
- `timeline.csv`: a row every `--sample-minutes` (15), at every check-in start and at the end: time, presence, purse, coins earned and
  per hour, stock per good, count and levels per building type, builders, workers provided / assigned / free, couriers, research, and
  how many producers run or stall per reason. Item and building columns follow the content, so content changes need no code change.

## Rules and invariants
- **Deterministic:** the same arguments write the same bytes (tested). There is no clock and no randomness, everything is sorted
  ordinally or kept in content order, numbers are integers in the invariant culture, and lines end in `\n`.
- **Rules only through the engine:** the strategies never change state themselves. Every action is a `CityRules`, `TripRules` or
  `CourierRules` call, and every time step is `Simulator.Advance`.
- **No game numbers in the tool** (`02` rule 8): costs, rates, limits and ids come from the content. The tool's own settings (steps,
  check-in lengths, the grace) are run settings, listed in each summary.

## How to extend
- A new strategy: a `Strategy` subclass (`ChooseResearch`, `Build`), a `StrategyKind` value, a case in `Strategy.Create`, a row in the
  table above, and the strategy theory in `SimulationRunTests` picks it up.
- A new metric: measure it in `RunRecorder`, write it in `SummaryMarkdown` or `Timeline`, and describe it under Output.
- From M2 on, simple market agents (`24` → Balance simulator) *(planned, M2)*.

## Tests
`tools/BalanceSim.Tests` is in the server's solution, so the fast suite runs it:
`dotnet test server/CityBuilder.slnx --filter "Category!=Integration"`. It covers the check-in patterns, the check-in list, the
timeline rows, every strategy with every default pattern (research starts and coins arrive in the first check-in, the engine rejects no
action), ResearchRush finishing all research, Balanced with a larger purse still earning coins in the first check-in (`ConstructionTests`), the chain read from the real content, the command line and its errors, and byte-identical
output for the same arguments.

## Known limitations
- The strategies are simple heuristics. A bottleneck can come from them (for example route lengths) rather than from the content; the
  findings in `docs/balance/` *(planned, M0-68)* weigh that.
- First coins and other report-based times are at the step resolution (10 s while checked in, 60 s away). Research and building times
  are exact (their timers' end).
- No roads, no warehouses, no pausing; one row-major layout.
- `RepositoryRoot` repeats DocTools' ten lines: the tools share no project.

Last updated: M0-A (M0-68)
