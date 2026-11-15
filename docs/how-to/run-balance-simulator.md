# How to run the balance simulator

Use it before and after every content change (`core-instructions/19-change-recipes.md` → Content-only change): it shows what the
change does to first coins, research times, coins per hour, stalls and idle workers. What the tool does and how its strategies decide:
[BalanceSim.md](../modules/BalanceSim.md).

## Run it
From the repository root (needs the .NET SDK, nothing else):

```bash
dotnet run --project tools/BalanceSim
```

Without options it plays every strategy (GreedyCoins, ResearchRush, Balanced) with three check-in patterns for 72 hours on
`server/content/mvp-content.json`, and writes `artifacts/balance-sim/`. Open `artifacts/balance-sim/index.md` first: one row per run,
with links to each `summary.md`. The nine runs take a few seconds.

## Options

| Option | Default | Meaning |
|---|---|---|
| `--strategy <list>` | `all` | `GreedyCoins`, `ResearchRush`, `Balanced`, comma-separated, any case |
| `--sessions <pattern>` | the three below | a check-in pattern; repeat the option for several |
| `--hours <n>` | 72 | the length of every run |
| `--content <file>` | `server/content/mvp-content.json` | the content to play |
| `--out <folder>` | `artifacts/balance-sim` | where the runs go (existing files with the same names are replaced) |
| `--first-session-minutes <n>` | 30 | the first check-in (the first play session) |
| `--session-minutes <n>` | 5 | every later check-in |
| `--sample-minutes <n>` | 15 | the spacing of the timeline rows |
| `--grace-seconds <n>` | 300 | how long after a check-in the player counts as absent (the server's presence grace) |

Relative paths start at the repository root. Exit codes: 0 done, 1 the content can't be loaded (the loader's message says why), 2 a
bad command line (the usage is printed).

## Check-in patterns
- `08:00,13:00,20:00 daily`: times of day. The run starts at the first one.
- `every 4h`, `every 90min`: a fixed interval from the start.
- `once a day`: the same as `08:00 daily`.

## Compare a content change
1. Run it on `main`'s content and keep the folder:
   `dotnet run --project tools/BalanceSim -- --out artifacts/balance-before`.
2. Change `server/content/mvp-content.json` (`add-building-or-item.md`).
3. Run it again: `dotnet run --project tools/BalanceSim -- --out artifacts/balance-after`.
4. Compare the two `index.md` files and the summaries of the runs that moved. Check the goals of `24-economy-design.md`: the first
   research in minute 1, first coins in about 20 minutes, the next research affordable within one or two absences. A change that breaks
   goal 1 or 3 needs a recorded reason (build log).
5. Put the numbers that changed in the build log entry's balance note (`19` → Content-only change).

For one strategy and one pattern, for example:

```bash
dotnet run --project tools/BalanceSim -- --strategy Balanced --sessions "every 4h" --hours 48
```

## Read the results
- **Milestones:** "—" means it never happened in the run. Times are city-clock `H:MM:SS` from the first check-in.
- **Producer time by state:** `MissingInput:<good>` points at deliveries (routes, couriers, a missing producer), `StorageFull:<good>` at
  goods nobody collects, `NoWorkers` at houses.
- **Rejected actions** should say "None". Anything else is a strategy bug, not a content finding.
- `timeline.csv` opens in any spreadsheet. Its columns are described in [BalanceSim.md](../modules/BalanceSim.md) → Output.

Last updated: M0-67
