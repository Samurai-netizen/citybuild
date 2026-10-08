# Game Mechanics (final game)

## Owner's pillars (fixed — change only with the owner's approval)
1. The setting starts in the **Middle Ages**. Research moves the city through **eras**, and each era adds mechanics gradually.
2. **Real-time production:** a production building makes goods over real time (e.g. 100 pcs per hour).
3. **Workers** are assigned to buildings. More workers mean faster production. A higher building level allows more workers.
4. **Grid map:** buildings can be placed anywhere on the grid.
5. **Open market:** an MMO-style exchange where players trade with each other at their own prices.
6. **No XP and no city levels**, only building levels. Research is the main progression, and new
   **major** mechanics arrive **only** when the city enters a new era.

Everything below is a proposal that serves those pillars. Detail: eras and research `25` · market `26`
· engine `04` · M0 numbers `23` · economy `24`.

## Core loop
**Choose research → unlock → build → staff with workers → wait → return → reassign, trade, upgrade → fund the next research.**
Taps are decisions, never progress. No mechanic rewards tapping speed.

## Items and recipes · Era I
Everything countable is an item: raw (logs, stone, grain), processed (planks, flour) and goods (bread, tools…).
Coins are the currency. Each building level runs one recipe: `inputs → outputs`, costing a fixed amount of
**labor (worker-seconds)** per cycle. Players see rates **per hour**. Inputs are taken when a cycle starts,
and outputs land when it ends. Items are stored city-wide.

## Workers · Era I
- Cottages (houses) provide workers. Workers are a city-wide pool, not an item.
- The player assigns workers to each production building, from 0 up to the building's level limit.
  **Production speed is proportional to workers** (labor ÷ workers). Moving workers keeps the progress already made.
- 0 workers pauses a building, so there is no separate pause button. A newly built building takes free workers automatically.
- The core tension: grid space goes either to houses (more workers) or to producers (more output per worker).

## Building levels · Era I
- An upgrade raises a building's worker limit, which means more output per tile. Some upgrades in later eras also improve the recipe.
- Upgrading takes time and a builder slot, and stops production for that time. Research can raise the max level.

## Grid and land · Era I
- Buildings take 1 tile in Eras I–III, and some industrial buildings take 2×2 later. There are no roads, and carts are only cosmetic.
- The start is a small plot (8×8 in M0, about 10×10 in 1.0). **Land Survey** research adds rows and columns.
- Placement starts to matter beyond space in Era V, when power coverage arrives.

## Storage, builders, offline · Era I
- Every material has a storage cap. A full store stalls its producers, and storehouse research raises the caps.
- Builder slots limit how many constructions or upgrades run at once. More slots come from research.
- **Timers always run in real time.** Production runs for at most the offline production cap per absence
  (8 h at the start, more via research). This rewards checking in a few times a day without punishing a weekend away.

## Research · Era I (main progression)
Each era has a research tree. Pick a node, pay its cost (coins and goods), and wait (minutes early, days late).
Effects: new building types, efficiency, storage, slots, land, and level caps. Completing every node in an era
starts the next era. Full rules: `25`.

## Trading Post (NPC merchants) · Era I
The Trading Post sells goods to NPC merchants for coins, using a fixed recipe and workers. It is the **only coin source**.
Its rates set a price floor for the player market.

## Era-gated major mechanics (summary — details in `25`)
| Era | Mechanic | One line |
|---|---|---|
| II | Open market | Sell and buy orders between players, escrow, fees, price bands (`26`) |
| II | Guilds | Groups of 5–30 players; shared guild projects that members feed with goods; guild research perks |
| III | Population classes & needs | Houses consume goods (bread, cloth…) to grow into higher classes that give more workers and new worker types |
| III | Expeditions | Send cargo on a timed voyage (hours to days) and receive exotic goods; ships are built and upgraded |
| IV | Fuel & machines | A building can burn fuel (coal) for a large speed boost; a decision per building |
| IV | Production lines | Factories with several recipes; switching costs time |
| V | Power grid | Power plants cover an area of the map; covered buildings unlock powered recipes |
| V | Automation | Managers that re-staff buildings and run market orders by simple rules |
| VI | Megaprojects | Multi-week guild or server projects (e.g. a launch site) built from many players' goods |

## Notifications (platform feature, not era-gated)
Construction done, research done, storage full, production cap reached, and (Era II) order filled.

## Monetization (later milestone, constrained by the market)
Because goods are tradable, anything that boosts production also turns into market power.
Keep monetization to **cosmetics, convenience and time**: an extra builder or research slot, speed-ups with daily caps,
and building skins. Premium currency is not tradable on the market unless the owner explicitly decides on an
EVE-style legal exchange.

## Explicitly not in this game
Tapping for income, XP, player or city levels, real-time multiplayer, PvP or combat, random disasters that
destroy progress, gacha for production buildings, and prestige resets.
