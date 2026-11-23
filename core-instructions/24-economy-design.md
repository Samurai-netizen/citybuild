# Economy Design — pacing and balancing

## Goals
1. The first research starts in the first minute, and the first coins arrive within ~20 minutes. From M1: something finishes every
   30–90 s in the first 10 minutes, chapter 1 (the tutorial) takes 10–15 minutes, and its rewards pay for the first steps.
2. Every check-in has something worth doing: a worker change, a build or upgrade, a research to fund, (Era II+) an order.
3. The next research is always visible and usually affordable within one or two absences. Once-a-day play may miss this (owner,
   2026-11-17, balance report 1): it costs output and pace, never progress.
4. Bottlenecks can be understood from the UI alone (stall reasons, net rates, free workers, stores, what a project still needs, where the
   couriers are).
5. (M1) Era I takes 5–8 days for a player with 3–5 check-ins a day, and the Chronicle paces it: chapter 1 in the first session,
   chapter 2 on day 1, chapter 3 by day 2–3, chapter 4 by day 4–5, chapter 5 (the Charter) on day 5–8. Numbers: `29`, tuned in M1-15.
6. (Era II+) A healthy market: prices are stable over weeks, newer players have goods veterans want, and no item can be cornered.

## Scarce resources and the decisions they create

| Scarce thing | Decision it creates |
|---|---|
| Workers (from houses) | Which buildings run, and how fast |
| Grid space | Houses vs producers; more buildings vs upgrading them |
| Builder slots | Construct now vs upgrade now |
| Research slot + coins | Which branch first; efficiency vs new buildings |
| Store limits (per building and item) | Empty producers before they fill (a route, a warehouse), or research storage |
| Couriers: count, carry limit, stamina | Which goods move first; a station upgrade vs another producer; workers carry or produce |
| Distance and roads | Where to place a building; a road's coins vs the couriers' walking time |

## Sources and sinks

| Sources (faucets) | Sinks |
|---|---|
| Trading Post: NPC merchants buy goods for coins (the **only recurring** coin faucet) | Construction and upgrade costs: coins at once, goods used up when they are delivered |
| (M1) Merchant orders and the caravan, at the Trading Post: a price above the plain recipe for the goods asked; three orders and one caravan at a time, refilled after a wait, so their coins per day are bounded | (M1) Decorations bought for coins |
| (M1) Chronicle rewards: coins and decorations, one-time and finite (39 goals in Era I, 7 100 coins, `29`) | (M1) Goods lost on demolish; half the coins of a demolished building or a cancelled research are lost |
| Raw producers make goods from nothing | Research costs, **the largest coin sink**, growing each era (goods delivered to the science building) |
| Starting grant, era charter grants, half the coins of a demolished building or a cancelled research (a refund, never a gain) | Recipe inputs (processing) |
| — | Roads: coins per tile and per upgrade |
| — | (Era II) market listing fee and sales tax |
| — | (Era III) population needs consume goods continuously |

Coins are uncapped (the purse) and materials are capped per building store. Inflation is controlled by research costs rising each era,
market fees, needs, and the caps on production below. A player market cannot create coins; it only moves them.

## What caps production
There is no fixed offline cap. Production is bounded by logistics:
- **Store limits:** a producer stops when its store has no room for its outputs; a full store needs a courier or a trip to empty it.
- **Deliveries:** a producer that needs inputs stops when its store runs out of them; inputs arrive only by courier or trip.
- **Courier stamina:** while the player is away, couriers work only as long as their stamina lasts (the Courier Station's level), then
  rest until the player re-energizes them. Without couriers, goods stop moving, and the chains stop soon after.
So a check-in a few times a day keeps a city running, and a weekend away costs output, never progress (`21`, no punishing timers).

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

## Orders and the caravan (M1)
- An order's price per good is above the plain Trading Post recipe, so an order is always worth more than selling the same goods to the
  recipe; the caravan's bonus is larger. Order tables per chapter ask only for goods the city can already make (`29`).
- Orders keep old goods useful: later chapters still ask for logs and planks, so every chain has a buyer.
- A slot's refill wait and the caravan's 24 h bound the coins per day from orders; the simulator (M1-15) checks that orders add
  20–40 % to a balanced player's coins, not more.

## Metrics we track (simulator first, analytics later)
- Time to first research, first coins, each building, each research node, and the end of the era.
- Coins/hour and items/hour per strategy over the first 72 h.
- Share of time each building is stalled, by reason (and how many workers sit idle).
- Logistics: goods moved and delivered per hour, the share of courier time spent walking empty or resting, and time-to-first-courier-station.
- Coins spent per category (build, upgrade, research), which shows where the economy pulls.
- (M1) Time to each Chronicle chapter, coins from orders vs the recipe, orders fulfilled vs dismissed, tutorial completion.

## Balance simulator (`tools/BalanceSim`, Day 12)
A headless console app that reuses `CityBuilder.Simulation` and the real content JSON (built in M0-67: `docs/modules/BalanceSim.md`, how to
run it: `docs/how-to/run-balance-simulator.md`).
- Strategies decide builds, upgrades, **worker assignment**, **research order** and **logistics** (manual trips, courier stations, routes;
  roads *(planned)*), e.g. GreedyCoins, ResearchRush, Balanced.
- Session patterns: e.g. "08:00, 13:00, 20:00 daily", "every 4 h", "once a day".
- Output: a markdown summary and a CSV timeline of stores and city totals, buildings, workers, couriers, research, stalls and the metrics above.
- Run it before every content change. A change that breaks goal 1 or 3 needs a recorded reason.
- From M2 on, extend it with simple market agents (buyers and sellers with price rules) to test fees and bands.

## Ownership of numbers
Numbers exist only in `server/content/*.json`. Code reads them. Tests reference them through the content loader
or a frozen test content file, never by copying literals.
