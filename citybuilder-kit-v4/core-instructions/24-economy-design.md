# Economy Design — pacing and balancing

## Goals
1. The first research starts in the first minute, and the first coins arrive within ~20 minutes.
2. Every check-in has something worth doing: a worker change, a build or upgrade, a research to fund, (Era II+) an order.
3. The next research is always visible and usually affordable within one or two absences.
4. Bottlenecks can be understood from the UI alone (stall reasons, net rates, free workers).
5. (Era II+) A healthy market: prices are stable over weeks, newer players have goods veterans want, and no item can be cornered.

## Scarce resources and the decisions they create

| Scarce thing | Decision it creates |
|---|---|
| Workers (from houses) | Which buildings run, and how fast |
| Grid space | Houses vs producers; more buildings vs upgrading them |
| Builder slots | Construct now vs upgrade now |
| Research slot + coins | Which branch first; efficiency vs new buildings |
| Storage | Check in before it fills, or research storage |

## Sources and sinks

| Sources (faucets) | Sinks |
|---|---|
| Trading Post: NPC merchants buy goods for coins (the **only** coin faucet) | Construction and upgrade costs |
| Raw producers make goods from nothing | Research costs, **the largest coin sink**, growing each era |
| Starting grant and era charter grants | Recipe inputs (processing) |
| — | (Era II) market listing fee and sales tax |
| — | (Era III) population needs consume goods continuously |

Coins are uncapped and materials are capped. Inflation is controlled by research costs rising each era,
market fees, needs, and the offline production cap. A player market cannot create coins; it only moves them.

## Pacing rules of thumb
- Cycle length at full staffing by era: I 10 s–2 min · II 1–10 min · III 5–30 min · IV+ 15 min–2 h.
- Build times grow on the same scale. Research durations are in `25`.
- Each building level adds +50 % to +100 % worker slots. Upgrade cost grows about 1.6–2.0× per level.
- Keep ratios between chain steps as small integers (1:1, 1:2, 2:3) so players can plan without a spreadsheet.
- Total research time is roughly 60–70 % of an era's target length; the rest is gated by coins and materials.

## Market economics (Era II+, see `26`)
- The NPC Trading Post price is the **floor**. A player won't sell below what NPC merchants pay, minus the labor cost.
- A capped NPC "Royal Warehouse" price is the **ceiling** for basic goods.
- Cross-era demand: later recipes keep consuming early goods, so veterans buy from newer players.
- Watch these metrics: money supply, coins created vs destroyed per day, item supply, the price index per era, and trade concentration.

## Metrics we track (simulator first, analytics later)
- Time to first research, first coins, each building, each research node, and the end of the era.
- Coins/hour and items/hour per strategy over the first 72 h.
- Share of time each building is stalled, by reason (and how many workers sit idle).
- Coins spent per category (build, upgrade, research), which shows where the economy pulls.

## Balance simulator (`tools/BalanceSim`, Day 12)
A headless console app that reuses `CityBuilder.Simulation` and the real content JSON.
- Strategies decide builds, upgrades, **worker assignment** and **research order**, e.g. GreedyCoins, ResearchRush, Balanced.
- Session patterns: e.g. "08:00, 13:00, 20:00 daily", "every 4 h", "once a day".
- Output: a markdown summary and a CSV timeline of inventory, buildings, workers, research, stalls and the metrics above.
- Run it before every content change. A change that breaks goal 1 or 3 needs a recorded reason.
- From M2 on, extend it with simple market agents (buyers and sellers with price rules) to test fees and bands.

## Ownership of numbers
Numbers exist only in `server/content/*.json`. Code reads them. Tests reference them through the content loader
or a frozen test content file, never by copying literals.
