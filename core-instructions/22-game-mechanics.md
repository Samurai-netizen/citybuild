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
**Choose research → unlock → build → staff with workers → route the goods → wait → return → reassign, trade, upgrade → fund the next research.**
Taps are decisions, never progress. No mechanic rewards tapping speed.

## Items and recipes · Era I
Everything countable is an item: raw (logs, stone, grain), processed (planks, flour) and goods (bread, tools…).
Coins are the currency. Each building level runs one recipe: `inputs → outputs`, costing a fixed amount of
**labor (worker-seconds)** per cycle. Players see rates **per hour**. Inputs are taken when a cycle starts,
and outputs land when it ends.
- **Goods live in buildings.** Every item except coins is kept in a building's **store**, which has a limit per item. A producer
  takes its inputs from its own store and puts its outputs there. The **Warehouse** stores every good of the era up to its limits.
- **Coins are the city's purse:** kept in no building, uncapped, paid and received at once.

## Workers · Era I
- Cottages (houses) provide workers. Workers are a city-wide pool, not an item.
- The player assigns workers to each production building, from 0 up to the building's level limit.
  **Production speed is proportional to workers** (labor ÷ workers). Moving workers keeps the progress already made.
- 0 workers pauses a building, so there is no separate pause button. A newly built building takes free workers automatically.
- Workers also carry goods: a free worker can make a one-time **trip**, and the workers assigned to a Courier Station are its
  **couriers** (Logistics below).
- The core tension: grid space goes either to houses (more workers) or to producers (more output per worker), and each worker either
  produces or carries.

## Building levels · Era I
- An upgrade raises a building's worker limit, which means more output per tile. Some upgrades in later eras also improve the recipe.
- Upgrading takes time and a builder slot, and stops production for that time. Research can raise the max level.
- An upgrade's coins are paid at once and its goods are delivered to the building. The building keeps producing until the last good
  arrives; then the upgrade runs.

## Logistics · Era I
- Goods move only when someone carries them. **Walkers** (couriers and workers on a trip) step from tile centre to tile centre in four
  directions, so every turn is 90°: 5 s per tile on open ground, faster on roads. They go around other buildings and cross one, slowly,
  only when there is no way around. Among the ways they could take, they pick the fastest, so they use roads when roads save time.
- **Trips:** from a building, the player sends one free worker with goods to another building or a construction site. The worker is free
  again when it arrives. Trips are always available, before and after the first Courier Station.
- **Courier Station:** its workers are its couriers. Each courier follows its **route**, a loop of stops; at each stop it takes goods from
  the building or puts goods into it, an item and an amount per action. Era I offers All, Half and Quarter; any percentage comes with an
  Era I research, an exact number with an Era II research. The station's level sets how many couriers it has, how much each carries and
  how long they keep working while the player is away.
- **Roads** are built on empty tiles, for coins, in a line the player drags. Each road level makes walking faster.
- Building work and research get their goods by delivery; only coins are paid at once. A delivery fills what a waiting construction,
  upgrade or research still needs before anything goes into the building's store.
- Later eras replace couriers with carts, wagons and finally cargo trucks, and bring better roads (`25`).

## Grid and land · Era I
- Buildings take 1 tile in Eras I–III, and some industrial buildings take 2×2 later. A road tile takes a tile too.
- **Placement matters from Era I:** distances, roads and the layout decide how fast goods move between buildings.
- The start is a small plot (8×8 in M0, about 10×10 in 1.0). **Land Survey** research adds rows and columns.
- In Era V, power coverage adds a second reason to place buildings carefully.

## Storage, builders, offline · Era I
- Every building's store has a limit per item. A full store stalls its producer, and so do missing inputs. Storehouse research raises the
  limits in every building.
- Placing a building pays its coins and takes a builder; the building is a **construction site** until its goods are delivered, then its
  build timer runs. A building that costs only coins starts at once.
- Builder slots limit how many constructions or upgrades run at once. More slots come from research.
- **Timers always run in real time.** While the player is away, couriers keep working for as long as their **stamina** lasts (the Courier
  Station's level sets it), then walk home and rest. One button re-energizes them when the player returns. While the game is open,
  couriers never tire. Production stops on its own when a store is full or its inputs run out. This rewards checking in a few times a day
  without punishing a weekend away.

## Research · Era I (main progression)
Each era has a research tree. Pick a node, pay its coins, have its goods delivered to the **science building** (one per city), and wait
(minutes early, days late). Effects: new building types, efficiency, storage, slots, land, level caps, and logistics (amount options,
walking speed). Completing every node in an era starts the next era. Full rules: `25`.

## Trading Post (NPC merchants) · Era I
The Trading Post sells goods to NPC merchants for coins, using a fixed recipe and workers. Its goods are delivered to its store, and its
coins go straight to the purse. It is the **only coin source**. Its rates set a price floor for the player market.

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
Construction done, research done, storage full, couriers resting, and (Era II) order filled.

## Monetization (later milestone, constrained by the market)
Because goods are tradable, anything that boosts production also turns into market power.
Keep monetization to **cosmetics, convenience and time**: an extra builder or research slot, speed-ups with daily caps,
and building skins. Premium currency is not tradable on the market unless the owner explicitly decides on an
EVE-style legal exchange.

## Explicitly not in this game
Tapping for income, XP, player or city levels, real-time multiplayer, PvP or combat, random disasters that
destroy progress, gacha for production buildings, and prestige resets.
