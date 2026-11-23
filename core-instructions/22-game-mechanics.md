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
Around it, from M1 (owner, CR-0011): the **Chronicle** always names the next goal, and **merchant orders** ask for different goods every
day. Taps are decisions, never progress. No mechanic rewards tapping speed.

## The town and the Chronicle · Era I
- **Premise:** the Duke grants the player, his steward, a river crossing to turn into a town. Earning the **Town Charter** (the last
  research of Era I) makes it a free town, the door to Era II. Four **advisors** speak in one or two lines: the master builder (building,
  land), the merchant (trade, orders), the scribe (research, the Chronicle) and the courier master (logistics). Names: `32`.
- **The Chronicle** has five chapters of six to eight **goals**. A goal is a condition the engine checks from the city (a building at a
  level, goods produced or delivered, a research done, road tiles, an order fulfilled) and a **reward**: coins, a decoration, or a line
  of the town's story. Progress is computed, never sent by the client; the player **claims** a done goal with a command, and the coins
  go through the ledger. A chapter opens when every goal of the previous one is claimed; its last goal is a visible event (the first
  sale, the first caravan, the bridge, the windmill, the Charter; `29`). Chapter 1 is the tutorial (`21` → First session).
- Goals and rewards are content data (`29`). Rewards are one-time and finite: they are not a recurring coin source (`24`).
- **After the Charter** (until Era II exists) the town keeps running: orders, decorations and upgrades go on, and a closing chapter
  says Era II is coming (G38).

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
- 0 workers freezes a building's cycle. A producer or a Courier Station also has a **pause** that keeps its workers: a paused producer
  finishes its cycle and starts no new one; a paused station's couriers finish their stop, give back their loads and walk home, keeping
  their routes; a paused building's store stays open (CR-0008 amendment 2). A newly built building takes free workers automatically.
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
- **Trips:** from a building, the player sends goods to another building or a construction site, any amount. Before the first Courier
  Station a free worker carries them, load after load, and is free again after the last; once a station stands, a free courier gets them as
  a queue of loads the player can cancel (CR-0008 amendment 1).
- **Courier Station:** its workers are its couriers. Each courier follows its **route**, a loop of stops; at each stop it takes goods from
  the building or puts goods into it, an item and an amount per action. Era I offers All, Half and Quarter; any percentage comes with an
  Era I research, an exact number with an Era II research. Routes need a Scriptorium, and a courier has a route or a queue of loads,
  never both. The station's level sets how many couriers it has, how much each carries and
  how long they keep working while the player is away.
- **Roads** are built on empty tiles, for coins, in a line the player drags. Each road level makes walking faster.
- Building work and research get their goods by delivery; only coins are paid at once. A delivery fills what a waiting construction,
  upgrade or research still needs before anything goes into the building's store.
- Later eras replace couriers with carts, wagons and finally cargo trucks, and bring better roads (`25`).

## Grid, terrain and land · Era I
- Buildings take 1 tile in Eras I–III, and some industrial buildings take 2×2 later. A road tile takes a tile too.
- **Placement matters from Era I:** distances, roads and the layout decide how fast goods move between buildings.
- **Terrain (M1):** every tile is grass, forest, rock, fertile soil or water, from one hand-made map that is the same for every player
  (`29`). Forest and rock hold no building or road; a Woodcutter's Hut needs a forest tile beside it (4 directions), a Quarry a rock
  tile beside it, a Farm stands on fertile soil. Water blocks building and walking, except where a **bridge** (a road kind, by research)
  crosses it. Terrain never runs out and never changes.
- The start is a plot of about 10×10 (8×8 in M0). Land beyond the border lies under **mist**: visible, not usable. **Land Survey**
  research opens more of the map.
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
walking speed). Completing every node in an era starts the next era. A running research can be **cancelled**: half its coins come back,
delivered goods do not (G39). Full rules: `25`.

## Ownership · Era I (minor mechanics, M1)
- **Town name:** the player names the town (a default name is given, `32`); it can be renamed. Other players may see it from Era II, so
  it passes a length and word check.
- **Move:** a finished building moves to another free tile it may stand on, at once and for free, with its store, workers and level. Not
  while it is a construction site or upgrading. Routes keep the building as a stop; walkers find the new way.
- **Demolish:** a finished building is removed after a confirmation that lists what is lost: half the coins paid for it come back, its
  store's goods are lost, its workers become free. A Cottage only if its workers can leave (enough free workers); the science building not
  while a research runs; the Town Hall never.
- **Decorations** take a tile like a building, produce nothing and are walked around. They come as Chronicle rewards or for coins (a sink,
  `24`); they can be moved and put away for free.

## Trading Post (NPC merchants) · Era I
The Trading Post sells goods to NPC merchants for coins, using a fixed recipe and workers. Its goods are delivered to its store, and its
coins go straight to the purse. It is the **only recurring coin source** (G7; Chronicle rewards are one-time). Its rates set a price floor
for the player market.
- **Merchant orders (M1):** the Trading Post has an **order board** with three slots. An order names a merchant, one to three goods the city
  can already make, and a price above the plain recipe. The player fulfils it from the Trading Post's store (no workers needed) or
  dismisses it; an empty slot refills after a wait. Orders come from content tables through a seeded integer function of the city and the
  order's number, so the server and every replay agree (`02` rule 6).
- **The caravan:** once a day a caravan brings one bigger order with a better reward. It stays until the next one arrives; a skipped
  caravan costs nothing (`21` → Tone).

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
Construction done, research done, storage full, couriers resting, the caravan, and (Era II) order filled. In M1 they are **local**: the
client schedules them from the timers it already predicts; there is no push server (G31). Each kind has a setting.

## Monetization (later milestone, constrained by the market)
Because goods are tradable, anything that boosts production also turns into market power.
Keep monetization to **cosmetics, convenience and time**: an extra builder or research slot, speed-ups with daily caps,
and building skins. Premium currency is not tradable on the market unless the owner explicitly decides on an
EVE-style legal exchange.

## Explicitly not in this game
Tapping for income, XP, player or city levels, real-time multiplayer, PvP or combat, random disasters that
destroy progress, gacha for production buildings, and prestige resets.
