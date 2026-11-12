# CR-0008 — Logistics rework: goods stored in buildings, couriers, routes and roads

- **Status:** Done
- **Type:** Design change
- **Requested by:** owner · **Date:** 2026-11-02 (revised the same day after the owner's review: D1, D5, D10 and the task grouping)
- **Milestone / era:** M0 / Era I (part of Era I's base set)
- **Conflicts with pillars (`22`) or the era rule (`25`):** no pillar changes, but the Era I text of `22` (protected) does: items are stored
  in buildings instead of city-wide, and building work and research get their goods by delivery; roads and couriers replace "there are no
  roads, and carts are only cosmetic"; courier stamina replaces the 8 h offline production cap; placement matters from Era I instead of
  Era V. Logistics is a major mechanic (`25` rule 5). The owner puts it into Era I's base set, so players have it from the start; only minor
  parts (percentages, exact amounts, faster roads) come by research.

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Owner's words (2026-11-02, in chat, quoted as written)
**The request:**
> I want to change a game mechanic a little bit. Each building produce items and store them inside the building. Each building has a limit
> of how much items can be stored in it. In order to move items between buildings, player need to build special building for couriers.
> After that, player need to make a route, select where to take items, where to go, how much to take and put (in percent, in a number, and
> just "all/half/etc." (player's choice). Era 1 should have "all/half/etc.", with percent option being opened by research, and exact numbers
> can be opened later in era 2 with a research). Courier can have many destinations in a one route. Additionally, before the courier station
> is built, player can manually move items between buildings with assigning a worker on a one-time trip (there should be a free worker to be
> able to do that). There should be an upgradable limit on how much a courier can carry in one go, and upgradable speed. In later eras, this
> will be cargo trucks, you see the analogy. The speed of a courier should start as 5 seconds per tile. Use an algorithm to find shortest
> path from each building to another. Courier can move only in 90 degrees turns, strictly following the centers of the tiles. Also add the
> "road" to be buildable. On the road, courier walks way faster, and with each upgrade of the road, it allows to go faster. Courier, when
> deciding which path is faster, should prioritize using roads (decide the path based on the actual time it will take him to walk from start
> to destination). Also the "return from long time of absence" screen should be reworked: the amount of time that game counts towards
> producing items while player closed the game (which now is 8 hours), should be calculated based on the courier building level: couriers
> walks a certain amount of time when player is absent, after some time courier get's tired and goes back to the building. Player needs to
> log in to the game and click a button to re-energize the couriers. The timer for couriers applies only when the player is absent - while
> the game is running, couriers don't become tired.
>
> Write down this as a separate game rework tasks, with it's own /start-task code. Find a time where it will be best to implement these
> changes, among regular M0-XX tasks

**The review of the first version** (D1–D10 were my recommendations then):
> D1. For any building activities player must manually re-route the resources from the building or warehouse. Coins are an exemption, they
> transfer instantly. For research that needs not only coins, resources must be delivered to the science building (initial level 1 science
> building should cost only coins to build. Same for courier building).
>
> D5. Couriers should avoid going through the buildings, but if there is no other way around, they allowed to go through with 20% of their
> base speed.
>
> D10. Okay.
>
> Also group CR tasks (WITHOUT LOOSING ANY QUALITY OF PROMPTS OR QUALITY OF WORK). Group it by the AI model needed. That way my presence
> behind the laptop is needed only to change model and merge PRs. I want no decrease in quality of work, just longer work sessions

**Amendment 1 (2026-11-12, the CR-0008-T3 playtest, in chat; D14–D22):**
> 1. I'm unable to make new routes from courier building.
> 2. Without scriptorium, player should be able to make only one-time manual courier trips. With scriptorium built, player shoul be able
> to make cycling routes.
> 2. Workers attended to couriers building are used to make a delivery, so the game should look only to the amount of free workers
> attended to the couriers building, and not to a total amount of workers.
> 3. Buttons to choose the amount of resources for delivery (All / half / quarter) don't work. Game sends 10pcs anyways. Fix it.

The answers to the follow-up questions: who carries a trip, "Station couriers, else any"; routes, "Yes, need Scriptorium"; scope, "Amend
CR-0008, do it in T3"; on the 10 per trip:
> In case when requested amount of goods is more than courier's inner capacity, the courier should make multiple trips between buildings
> until all the goods delivered. Each courier should have a list of future actions, each trip is an action (like with a real-world printer
> queue). Also courier should always start and end his trip from the couriers building.

Then: walk "Shuttle, then home"; amount "When it's sent"; worker fallback "Yes, shuttle too"; and on cancelling:
> Each line in his delivery plan should be cancelable. For example, if there is a singular 40 items request, which transformed into 4 lines
> "pick at A, put at B", player should be able to cancel each trip individualy, so, for example, he can cancel 1 trip and have 30 items
> delivered insread of 40. Also it should have a separate "Cancel all" option for cancelling ALL trips, even recurring ones

**Amendment 2 (2026-11-12, the CR-0008-T3 playtest round 5, in chat; D23–D25):**
> Now add the pause button to all buildings. That way I can, for example, upgrade building without canceling all courier routes. Also
> make it that the progress bar shows the fullness of internal storages instead of production cycle, when the storage is more than 75%
> full. New indicator should look different from the current one

The answers to the follow-up questions: scope "Amend CR-0008, in T3"; a paused producer "Finish cycle, then stop"; a paused station
"Finish stop, go home (with a timer in a couriers building shownig how much left to wait until everything stops)"; a paused building's
store "Yes, store stays open".

## Problem / goal
Today every item sits in one city-wide stock, so the map is only a place to put buildings: distance, layout and roads mean nothing (`22`).
The owner wants moving goods to be part of the game from the first session. Buildings keep what they make; goods travel on foot, along
paths the player shapes with roads, to the buildings that use them, to construction sites and to the science building; and how long the
city keeps working while the player is away depends on the couriers instead of a fixed 8 hours. Building it inside M0, before the audits,
balance simulator and test suites of Days 11–14, lets all of those cover the game as it will be (see "When to build it").

## Proposal
D1, D5 and D10 are the owner's decisions. Everywhere else the proposal uses my recommendation; the "Decisions" table lists each one (D1–D13).

### 1. Goods live in buildings
- Every item except coins is kept in a building's **store**. Each level of a building type lists the items its store holds and a **limit**
  for each item (content). Limits are per item, so inputs can never fill the room a building's outputs need. A producer's store holds its
  recipe's inputs and outputs; houses, the courier station and the science building have none.
- A producer starts a cycle only when its **own** store has the inputs and room for the outputs. The stall reasons stay
  (`MissingInput:item`, `StorageFull:item`), now about that store.
- **Coins** are the city's purse: uncapped, not in any building, paid and received at once (owner). The Trading Post's coins go straight there.
- The **Warehouse** (new, available from the start) stores every Era I good up to a limit per item: the place to collect goods for building
  work and to buffer chains (D13).
- A **refund** (an upgrade ends a cycle) goes back into that building's store, also above its limit, as today. The Storehouses research
  raises the limit of its items in every building, the warehouse included.
- A **new city** starts with coins only (D2): there is no building to keep goods in.
- A building that is being upgraded keeps its store, and goods can still be taken from it and delivered to it.

### 2. Building work and research get their goods by delivery (owner, D1)
- Only coins are paid at once. No cost is ever taken from a store by itself: the player brings the goods with manual trips or courier
  routes, from producer buildings or a warehouse.
- **Placing** a building pays its coins and takes a builder. The building appears as a **construction site** that lists the goods still
  missing, and its build timer starts when nothing is missing. A building that costs only coins starts at once, as today.
- **Upgrading** pays its coins and takes a builder, and the goods are delivered to the building. It keeps producing while it waits; when the
  last good arrives, the upgrade starts as today (production stops, a running cycle is refunded, the timer runs) (D12).
- **Research** happens at the **science building** (new, one per city). Starting a research pays its coins and takes the research slot;
  the goods it needs are delivered to the science building, and its timer starts when nothing is missing (D11). Level 1 of the science
  building and of the courier station cost only coins (owner).
- A delivery to a building with a waiting construction, upgrade or research (a **project**) first fills what the project still needs; the
  rest goes into the building's store if it keeps that good, else stays with the walker.
- Delivered goods are used up at once (ledger rows at the delivery). Cancelling a project is not part of this CR.
- Roads cost coins only, so they still build at once (D6).

### 3. Paths and roads
- **Walkers** (couriers and workers on a trip) move from tile centre to tile centre in four directions, so every turn is 90°.
- **Time:** ground takes **5 s per tile** (owner). Each road level has its own, much shorter time. Crossing **another building's tile** goes
  at 20 % of the base speed, 25 s per tile (owner, D5); the walk's own start and end buildings count as ground. A step takes half the time of
  the tile it leaves plus half of the tile it enters. Walking-speed research shortens the total, and a walk is rounded up to whole seconds
  (the engine is integer-only, `04` §9).
- **Path rule:** walkers avoid other buildings whenever a way around exists, and among those ways take the fastest, so they use roads
  exactly when roads make the walk faster, even with more tiles. Only when every way crosses a building do they cross, through as few building
  tiles as possible, then the fastest (owner, D5). Every building can always be reached. Ties are broken by a fixed rule (`04`), so the server
  and the client always pick the same path. A walk keeps the path it started with; a change to the map applies from the next walk.
- **Roads** are built on empty tiles in a line the player drags (one command for many tiles), instantly, without a builder slot, for coins
  only (D6). Each road tile has a level; upgrading a tile makes it faster; a road can be removed (no refund). Buildings can't be placed on a
  road. M0 has road levels 1–2; more come with later content.

### 4. Manual trips (before or without couriers)
- From a building's inspector the player sends one **free worker** on a one-time trip: item, amount (the Era I presets), target (a building or
  a project). It needs a free worker.
- The worker takes the goods at once (at most the trip carry limit), walks to the target by the path rule, delivers what fits (a project
  first) and is free again on arrival. What doesn't fit goes back to the source (D7). Trips run in real time, also while the player is away,
  and never tire.
- Trips stay available after a courier station exists (D7).

### 5. Courier station, couriers and routes
- The **Courier Station** is a new kind of building, available from the start; level 1 costs only coins (owner). Workers assigned to it are
  its **couriers**, one per worker, up to its level's limit. Its level also sets each courier's **carry limit** (all items together) and
  **stamina** (D4).
- Each courier has one **route**: an ordered list of **stops**, from 2 up to a content limit, with different buildings at two stops in a
  row. A stop is a building and one or more **actions**: take or put, an item, an amount. The courier starts at its station and walks to
  stop 1, 2, … the last, then stop 1 again, and so on.
- At a stop the actions run in order, at once. **Take** removes goods from the building's store, as much as the amount, the stock and the
  free carry room allow. **Put** delivers carried goods, as much as the amount and the room allow (a waiting project first, then the
  store); the rest stays on the courier. A courier never waits for goods: it does what it can and walks on.
- **Amounts:** Era I starts with **All, Half and Quarter** (D3): of the building's stock for a take, of what the courier carries of that
  item for a put; rounded down, but at least 1 when there is any. An Era I research unlocks **any percentage** (1–100). An **exact
  number** is unlocked by an Era II research; the engine and the API support it now, and it stays locked until that research exists (OD-10).
- When a courier stops working (its worker is removed, its route is replaced, its station starts an upgrade), the goods it carries go back
  to the buildings they came from, like a refund, and it starts again from its station.
- Couriers are drawn on the map walking their paths; research makes them walk faster (D4).

### 6. Courier stamina replaces the 8 h offline production cap
- The fixed **offline production cap** (8 h) and the dormant tail (`04` §2) are removed. Production stops on its own: a building stops when
  its store is full or its inputs run out, and inputs only arrive by courier or trip.
- **Stamina** is how long a courier keeps working while the player is away. It comes from its station's level (for example 4 h at level 1,
  8 h at level 2). It is spent **only while the player is absent**; while the game is open, couriers never tire.
- **Presence is decided by the server** (D8). The open game talks to the server at least every 120 s (`09`, polling). The player counts as
  absent from 5 minutes after their last request (the presence grace) until their next one. The client sends no time and no "I'm here" flag.
- When a courier's stamina is used up, it finishes the stop it is walking to, walks back to its station and **rests**. Its load stays with it.
- **Re-energize:** a button (a new command) refills every courier's stamina, and resting couriers go back to their routes (D9).
- Stamina also does the old cap's second job: it bounds how much one settlement has to simulate (`04` §12).

### 7. Welcome-back report
The report (M0-53) is reworked around the couriers: time away; how long the couriers worked and when each station's couriers got tired;
goods moved and delivered to projects; produced and consumed; coins earned; completed buildings and research. Its main button is
**Re-energize couriers** when any courier has spent stamina. Closing the report without it leaves the couriers resting; the HUD shows that,
and the courier station's inspector has the same button.

### 8. Later eras
Exact amounts arrive with an Era II research. Later eras replace couriers with carts, wagons and finally cargo trucks: the same routes and
rules with more capacity, more speed and their own stamina, and better roads (stone in Era II). `25` records this now; each era builds its part.

### First numbers (placeholders: T1 part B sets them in `23`, M0-67 and M0-68 balance them)
| What | Level 1 | Level 2 |
|---|---|---|
| Science building (one per city) | 100 coins, 30 s | none in M0 |
| Courier Station | 2 couriers · carry 10 · stamina 4 h · 120 coins, 60 s | 4 couriers · carry 15 · stamina 8 h · upgrade 250 coins + 30 planks, 180 s |
| Warehouse | 100 of each Era I good · 100 coins + 20 logs, 60 s | 200 of each · upgrade 200 coins + 30 planks, 120 s |
| Woodcutter's Hut store | 40 logs | 80 logs |
| Sawmill store | 40 logs · 20 planks | 80 logs · 40 planks |
| Trading Post store | 40 planks | 80 planks |
| Road tile | Dirt path: 2.5 s per tile · 5 coins | Log road: 1.5 s per tile · upgrade 15 coins |

Ground: 5 s per tile; crossing a building: 25 s per tile. Trip carry limit: 10. New Era I research: **Tally Sticks** (any percentage) and
**Sturdy Boots** (walking 20 % faster).

## Decisions
| # | Question | Decision or recommendation (used above) | Alternative |
|---|---|---|---|
| D1 | Where do the goods for building work and research come from? | **Owner:** delivered by the player (trips, couriers) from buildings or a warehouse; coins at once; research goods to the science building; level 1 of the science building and the courier station costs coins only | — |
| D2 | What does a new city start with? | Coins only; T1 rebalances the first costs so the first coins still come within about 20 minutes (`24` goal 1) | A starting stockpile building holding today's 40 logs and 10 planks |
| D3 | Which amounts does Era I offer before research? | All, Half, Quarter | Add Three quarters, or Third |
| D4 | What makes couriers better? | Station level: more couriers, carry limit, stamina. Research: walking speed. Roads: speed on their tiles | Speed from the station level too, or upgrades per courier |
| D5 | Can walkers cross buildings? | **Owner:** they avoid buildings; when no way around exists they cross, at 20 % of the base speed (25 s per tile) | — |
| D6 | How are roads built? | Instantly, no builder slot, a line of tiles per command, coins only (with goods, every tile would need a delivery, D1); upgrades instant; removal free, no refund | As constructions with a build time, a builder slot and delivered goods |
| D7 | Manual trips | Stay available after a courier station exists; carry at most the trip limit; what doesn't fit at the target goes back to the source | Only until the first station is built; or the target takes everything, above its limit |
| D8 | When is the player absent? | From 5 minutes after their game's last request (multiplied by `DevTimeScale` on dev servers); any request counts. A script that keeps polling keeps couriers awake, like an open game: accepted in M0, revisited with the market (M2) | A separate presence call (no safer: the client is untrusted either way) |
| D9 | Re-energizing | Free, any time, refills every courier of the city | Per station, or with a cost or a cooldown |
| D10 | Existing dev cities | **Owner ("Okay"):** the migration resets every dev city to the new starting city (G-Destructive, OD-14) | — |
| D11 | The science building | Every research needs it (it is where research happens); one per city; it builds in about 30 s, so the first research still starts in the first minutes; M0 has level 1 only | Only research with goods needs it |
| D12 | What waits for goods? | A construction site keeps its builder while it waits; an upgrade keeps producing until its goods are in, then stops and runs; a research keeps the research slot; a delivery fills the waiting project first | Upgrades stop production when ordered |
| D13 | The warehouse (named by the owner in D1) | A new building, available from the start, that stores every Era I good up to a limit per item | One total limit for all goods together |
| D14 | Who carries a one-time delivery? (amendment 1, replaces D7's "free worker") | **Owner:** a free courier (one without a route) of a built courier station; only while the city has no built courier station, any free city worker. A station that is still a construction site doesn't count | Any free worker always (the first version) |
| D15 | When can a courier get a route? (amendment 1) | **Owner:** only once the city has a built Scriptorium (content names the building); until then couriers do one-time deliveries only; a saved route keeps running | Routes from the first station |
| D16 | A delivery larger than one load (amendment 1, replaces "at most the trip limit") | **Owner:** the amount (All, Half, Quarter, a percentage) is counted from the source's stock when the player sends it and split into **lines** of at most the carrier's carry limit (the station level's `carryLimit`, a worker's `tripCarryLimit`); each line is "take at A, put at B". The goods stay in A's store until a line picks them up: a line takes what is there, up to its quantity (none: it ends empty). The courier leaves its station, runs its lines in order (each from the line's source to its target), and walks home after the last. What doesn't fit at the target goes back to the source's store at once (D7) | Carry it back on foot; take every line's goods at the send |
| D17 | Which courier gets a delivery? (amendment 1) | A free courier is picked by the engine: the fewest lines queued, then the shortest walk from its station to the source, then the lowest station id and slot. One send goes to one courier; its lines must fit in that courier's queue (content `maxDeliveryLines`, 20), else it is refused | The player picks the courier; split one send over several couriers |
| D18 | Cancelling (amendment 1) | **Owner:** every line can be cancelled on its own, and "Cancel all" cancels all of a courier's lines and its route. Cancelling the running line gives its load back to its source at once (a returned load) and the courier walks on to its next line, or home. A worker's delivery is not cancelled in M0 | No cancelling; cancel the whole delivery only |
| D19 | Routes and deliveries on one courier | A courier has a route or delivery lines, never both: a courier with a route gets no deliveries, and saving a route on a courier with lines is refused until they are done or cancelled | Lines first, then the route |
| D20 | Stamina on deliveries | A courier on deliveries spends stamina like one on a route (only while the player is away); a tired one walks home and rests, its lines wait, re-energizing lets it go on. A worker's delivery never tires (as trips before) | Deliveries never tire |
| D21 | A worker's delivery (no courier station yet) | **Owner:** it shuttles too: one delivery per worker, lines of at most `tripCarryLimit`; the worker takes the first line at the source at once, walks to the target, back to the source for the next line, and is free after the last put | One load of at most the trip limit (the first version) |
| D22 | The building menu (amendment 1, the owner's playtest points 4 and 5) | **Owner:** while a building's menu is open, the Build and Research buttons fade and can't be touched; the menu is larger and centred on the screen; every part of it fits and is readable and touchable, also the courier station's queue and routes; the map stays usable wherever a flow needs it (a tap outside the menu closes it; the route editor and the trip flow keep the map tappable) | — |
| D23 | Pausing a building (amendment 2) | **Owner:** a pause button on every building that works (a producer or a courier station; houses, warehouses, the Scriptorium and the science building do no work to pause). A new command `SetPaused(building, paused)`, allowed in every phase but construction; the flag stays through an upgrade. A paused producer finishes its running cycle (output as usual), then starts no new one; its workers stay assigned. Its stall reason is `Paused` | Freeze the cycle; end it with a refund |
| D24 | A paused courier station (amendment 2) | **Owner:** each courier finishes the stop it is walking to (a route stop's actions; a line's take or put, so a line whose put was done ends), then gives back what it carries (a returned load) and walks home; at home it waits. A courier at its station doesn't leave; routes and lines stay. Unpausing lets them leave again from the first stop (or the first line's source). The station's menu shows how long until the last courier is home | Freeze the couriers where they are; drop the stop at once |
| D25 | A paused building's store and the storage gauge (amendment 2) | **Owner:** a paused building's store stays open: couriers and deliveries still take and put there. The building's status shows a store gauge instead of the cycle ring while its store is more than 75 % full (the fullest good, against its effective limit), drawn as a segmented ring, amber, red when a good is at its limit, with that good's icon inside. Display only | Close the store too |

## Acceptance criteria
- [x] Items other than coins exist only in building stores, on couriers and on trip workers; no store goes above its limit except by a
      refund or a returned load (engine property test).
- [x] A producer starts a cycle only with the inputs in its own store and room there for the outputs; otherwise its stall reason names
      that store's missing input or full output.
- [x] Coins are paid and received at once through the purse. No other cost is taken from a store: a construction site, an upgrade and a
      research wait until their goods are delivered, then their timers start; research needs the science building, one per city.
- [x] Level 1 of the science building and of the courier station costs coins only; the warehouse stores every Era I good up to its limits.
- [x] A manual trip needs a free worker; the worker is busy until it arrives and free afterwards; it moves the chosen amount and fills a
      waiting project first.
- [x] A courier station's workers are its couriers; each follows its route of 2 or more stops in a loop, taking and putting the chosen
      amounts within its carry limit, the source's stock and the target's room.
- [x] Era I offers All, Half and Quarter; any percentage after its research; an exact number is refused until an Era II research unlocks
      it (tested with test content).
- [x] Paths use only 4-direction steps through tile centres. A path crosses another building only when every path does, and then as few as
      possible, at 20 % of the base speed; among the paths with the fewest crossings it is the fastest (checked by brute force on small maps).
      Ground costs 5 s per tile and every road level is faster.
- [x] Roads can be placed on empty tiles, upgraded and removed, for coins only; a building can't be placed on a road.
- [x] Couriers spend stamina only after the absence start that the server derives from the player's last request; a courier without
      stamina walks home and rests; re-energizing refills every courier.
- [x] There is no fixed offline production cap; the settlement report gives the couriers' working time and which couriers got tired, when.
- [x] The welcome-back report shows the courier summary and the re-energize button; the trip flow, road mode and route editor work on a
      phone screen by the M0-59 rules: one scroll direction at most, the rest scaled to fit (OD-17), phones from the iPhone XR on (OD-18).
- [x] Golden scenario #1 (rewritten) and #2 (new) pass in the engine and over HTTP; determinism, split invariance (for one absence start),
      timer invariance, conservation and the new performance budget pass.
- [x] Every new command refuses values no city could make valid (ids below 1, negative coordinates, too many tiles, stops or actions, a
      percentage outside 1–100) as `INVALID_ARGUMENT` before the city is locked (OD-13), with tests.
- [x] Every page in the impact table below is updated, `check-docs.sh` and the handbook build pass, and the CR is Done when every task is merged.
- [x] Amendment 1 (D14–D22): a delivery goes to a free station courier, or to a free worker only while no courier station is built; it is
      split into lines of at most the carry limit, counted when sent; a courier runs its lines from its station and back; each line and
      "Cancel all" can be cancelled; routes need a built Scriptorium; a courier has a route or lines, never both; the building menu is
      centred and larger, the Build and Research buttons are hidden while it is open (the owner's change in the T3 playtest, round 4), and everything in it fits (engine, server and client tests).
- [x] Amendment 2 (D23–D25): `set-paused` pauses and resumes a producer or a courier station; a paused producer finishes its cycle and
      starts no new one; a paused station's couriers finish their stop, give their loads back and walk home, keeping routes and lines; a
      paused building's store stays open; the menu has the Pause button and the station's wait timer; a store over 75 % full shows the
      segmented gauge instead of the ring (engine, server and client tests).

## Out of scope
- Era II content (it brings the research for exact amounts), carts, wagons and trucks, stone and paved roads.
- Cancelling a construction, an upgrade or a research; demolishing buildings; buildings larger than one tile.
- Science building levels above 1 (with the rest of the Era I content, M1).
- Couriers blocking each other, loading time at a stop, diagonal steps, waiting at a stop for goods.
- Notifications for resting couriers (with the other notifications in M1).
- Where market deliveries land once goods live in buildings (decided with the market, M2).
- The rest of the Era I content (stone, grain and bread chains, M1).

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | Stock moves into building stores; coins become the purse; projects wait for deliveries; new state (stores, projects, roads, trips, couriers, routes, stamina); path search with building crossings; trip and courier arrivals as new event sources in `Advance`; new mutations; §2 (cap, dormant tail) removed; the absence start becomes an input of `Advance`; invariants, §11 report and §12 budget change | `shared/Simulation/Runtime/**`, `04` |
| Content (`23`, JSON) | Store limits per level; the courier station, the science building (one per city) and the warehouse; road levels; walking and crossing times; trip carry limit; stamina; amount presets; new effect types and Era I research; a starting city without goods; `contentVersion` `mvp-2`; the golden fixtures (protected) | `server/content/mvp-content.json`, `23`, `server/tests/**/Fixtures/content-golden-*.json` |
| API (`05`) | Breaking (G-Contract, covered by this CR's approval): the snapshot's `inventory` and `storageCaps` become stores, purse and totals; projects, roads, trips, couriers, routes and stamina are added; the settlement report changes; six commands (`place-roads`, `upgrade-roads`, `remove-roads`, `start-trip`, `set-route`, `reenergize-couriers`); `place-building`, `upgrade-building` and `start-research` pay coins only and start projects; new error codes (for example `NO_SCIENCE_BUILDING`, `BUILDING_LIMIT`, `AMOUNT_MODE_LOCKED`, `INVALID_ROUTE`); limits on every list | `05`, `server/src/CityBuilder.Api/**`, `server/src/CityBuilder.Application/**` |
| Data / migration (`06`) | Tables for stores, projects, roads, trips, couriers and routes; `cities.last_seen_at`; coins stay as the purse; ledger rows for deliveries and `COST_ROAD` (moves between stores write none: they don't change the city's totals); a migration that resets dev cities (D10) | `06`, `server/src/CityBuilder.Infrastructure/Persistence/**` |
| Security (`07`) | Presence is derived by the server from request times; the new client inputs (tile lists, routes, amounts, trip targets) are bounded and refused at the boundary; a polling script keeps couriers awake (known limitation) | `07` |
| Client (`09`) | Store in the inspector, HUD totals, projects waiting for goods, trip flow, road mode, courier station inspector, route editor (a new menu, OD-16), walkers on the map, welcome-back rework; the display prediction runs couriers too | `09`, `client/CityBuilder/Assets/_Project/**`, `client/README.md` |
| Art | Courier station, science building, warehouse, courier figure, road tiles, icons | `art/**` |
| Design docs | `22` (protected), `21`, `24`, `25`, `26` binding rule 4, `31`, `10`, `12`; the M0 plan (Definition of Done, §2, schedule, board); `13` | `core-instructions/**`, `docs/plan/M0-PLAN.md` |
| Docs (recipes in `19`) | Module docs of every touched project and assembly; how-tos `add-command.md`, `add-building-or-item.md`, `add-research-effect.md`, `add-ui-screen.md`; `docs/testing/LOCAL-E2E.md`; `CHANGELOG.md` | `docs/**` |
| Tests (`08`) | A rule test per rule; property tests; golden #1 rewritten and #2 new (engine and HTTP); integration tests per command with the OD-13 refusals; the performance budget; Unity EditMode and PlayMode tests | `shared/`, `server/tests/**`, `client/…/Tests/**` |

## Risks and alternatives
- **Size.** Three long tasks, the same work as the 16 tasks of the first version: about 3–5 working days on top of M0's realistic 17–21.
  The riskiest step is T1 part E, where the storage model changes in the engine, the database and the HTTP golden test at once.
- **Long sessions.** A session can stop (usage limits, a crash) and gets compacted as it grows. The resume point names the part and the step,
  and the build log holds every decision, so `/resume` continues where it stopped. Each part ends fully verified, so a stop never leaves a
  half-checked part behind.
- **Where the sessions run.** This cloud container has no .NET SDK (the repository's setup script hasn't run in it), and Unity can't run in
  the cloud. T1 and T2 must run where every check can run: the owner's laptop (see "Running the grouped tasks").
- **Tests change with the rules.** Golden scenario #1 and the tests built on the 8 h cap and the city-wide stock change because the design
  changes, not to get green: each change gets its written reason in the build log (G-Tests), and this CR's approval covers it.
- **Performance.** Courier walks can outnumber production cycles (short walks on roads). Stamina bounds them while the player is away;
  T1 part I sets and measures the new budget.
- **Presence by polling.** A script that keeps polling keeps couriers awake, like an open game. It matters once goods can be sold to other
  players (M2): revisit then, for example with a daily limit on awake time or device attestation (M3).
- **Split invariance changes meaning.** It holds for one absence start; a request is itself presence, so a request between two advances can
  change the outcome. The dev time scale must also scale the presence grace, or couriers would tire between polls.
- **The first session gets harder.** Before the first courier station the player moves goods by hand, also to construction sites and the
  science building. `24` goal 1 (first coins within about 20 minutes) is at risk: T1 part B rebalances, M0-67 and M0-68 measure, M0-71 adds hints.
- **No punishing timers (`21`).** Resting couriers are the cap on production that `21` allows; the re-energize button must not nag (no
  countdown, no penalty beyond the stop).
- Alternative: build it in M1. M0's balance runs, audits and test suites would then be redone on the new model.
- Alternative: keep city-wide stock and make couriers decoration. Rejected: the owner wants logistics as a mechanic.

## When to build it
**Recommended: right after M0-60 (in progress while this CR was written), before M0-61.** The order becomes … M0-59, M0-60,
CR-0008-T1, CR-0008-T2, CR-0008-T3, M0-61, M0-62 …
- **Every later M0 task covers the new game once instead of twice.** M0-63 (docs completeness), M0-64 (ledger audit: it must now add up
  stores, loads and deliveries) and M0-66 (architecture review) see the final model. M0-67 (balance simulator) can only balance the game if
  its strategies build stations, routes and roads. Day 13's suites (M0-73 to M0-76) and M0-78's security report test the new commands, and
  Day 14 closes M0 on them.
- **What it builds on is done:** the command pipeline, persistence, the screens it extends (inspector, welcome-back report, research screen,
  menus), the phone-screen rules of M0-59 (OD-17, OD-18) and the art pipeline (CR-0005).
- **Not earlier:** M0-60 would be left half done. Its boundary review covers today's commands; each new command gets its boundary checks and
  their tests in its own part (OD-13), and M0-74's attack suite covers them again.
- **Not later than M0-66:** after the balance simulator, Days 12 and 13 would have to be redone.
- Workable alternative: after M0-62, so DocTools (generated reference docs) and the quality script help the rework; the cost is two more
  tasks done on the old model (M0-61 regenerates anyway).

## Tasks
Grouped by model, as the owner asked: each task is one long session, one build log entry and one PR. Their parts are the 16 tasks of the
first version, in the same order, every instruction kept, with D1 and D5 worked in. `/start-task CR-0008-T1` works once this CR is Approved;
T1 adds the tasks to the M0 board. Model: Opus = Opus 5.5, Sonnet = Sonnet 5.5.

| ID | Model | Parts | The owner is needed for | Status |
|---|---|---|---|---|
| CR-0008-T1 | Opus | A–I: spec, engine and server | starting it; two permission prompts (the `22` edit at the start, the golden fixture after part B); merging | Done (PR #86) |
| CR-0008-T2 | Sonnet | A–F: client and art | starting it; merging | Done (PR #87) |
| CR-0008-T3 | Opus | close-out with a playtest `[MANUAL]` | about an hour of playing and looking; merging | Done (this PR) |

The owner's steps: `/model` Opus → `/start-task CR-0008-T1` → answer the `22` prompt → later, answer the golden fixture prompt → merge →
`/model` Sonnet → `/start-task CR-0008-T2` → merge → `/model` Opus → `/start-task CR-0008-T3` → play → merge.

### Running the grouped tasks (T1 and T2)
Each part keeps the full quality bar of the separate task it was; only the PR and the owner's start are shared.
1. **Where:** a machine with the .NET 10 SDK, Docker and Unity 6000.3.25f1 (T2 also Blender), normally the owner's laptop, so the session
   runs every check itself, `test-unity.sh` included (Editor closed). A cloud session can't run Unity, so it can't finish T1 part E or T2.
2. **Write ahead for the whole task:** the build log plan lists every part with its steps; the Docs impact table covers every part; the
   resume point names the part and the step.
3. **Each part ends like a task, without the PR:** its tests (rule, property, integration, Unity), its docs by the recipes in `19`, a
   self-review of the part's diff against its text below, and the full verification (format, build, fast tests, integration tests, Unity
   tests when the client or the shared engine changed, `check-docs.sh`, the handbook build), then a checkpoint. Every step inside a part is
   green before its checkpoint (`16`).
4. **CI on every push:** after part A, open the task's PR as a draft. `/finish-task` at the end fills its body from the template and marks it
   ready (no second PR).
5. **No waiting for the owner inside this CR's scope:** decisions within it are made and logged (decision rows), and the work goes on. The
   session stops only for a gate this CR doesn't cover (`15`): a new dependency, a protected file it doesn't name, loosening a test for any
   reason other than a rule this CR changes, anything outside this CR.
6. **What the approval of this CR covers:** the `22` edits; creating `content-golden-v2.json` and deleting `content-golden-v1.json`;
   rewriting or removing tests whose rule this CR changes (each with its reason in the build log, never to get green); the API and database
   breaks; resetting the dev cities. Two permission prompts still appear and wait for the owner: `22` (the first step of T1, while the owner
   is still there) and the golden fixture (the last step of T1 part B, which is also the owner's chance to look at the spec and stop the
   session). Say so in the session when each is due.
7. **The `[PLAN]` stops of the first version are replaced** by the spec of T1 parts A and B, written and pushed before any code, and by a
   written plan with checkpoints in the build log before part E.
8. **Interruptions:** if a session stops, the owner runs `/resume` with the same model; the resume point says the part and the step.

### Task specs
Each spec is the prompt for its task, on top of the standing rules (`AGENTS.md`, `16`, the change recipes in `19`). "This CR" is the design source.

#### CR-0008-T1 — Spec, engine and server · [O]
```text
Follow "Running the grouped tasks" above. Parts run in this order; each ends fully verified and checkpointed.

Part A — Design docs (docs only)
Read this CR, 21, 22, 24, 25, 26, 31, 10, 12, 15 and docs/plan/M0-PLAN.md.
- First step, while the owner is still there: 22 (protected; this CR's approval covers it, the permission prompt still shows): Items and
  recipes, Workers, Grid and land, Storage, builders, offline, and the Trading Post as this CR describes them. The six pillars stay word for word.
- 21 (walkers and roads are mechanics, not decoration), 24 (logistics as a scarce resource; deliveries as the new sink; the caps on
  production), 25 (logistics in Era I's base set; the science building; percentages and exact amounts by research; vehicles in later eras),
  26 (binding rule 4 for building stores; market deliveries need a destination, M2), 31 (store, limit, purse, warehouse, science building,
  project, construction site, delivery, walker, crossing, courier, courier station, route, stop, action, trip, road, stamina, presence),
  10 (M0 scope), 12 (a row for each decision D1-D13 as approved).
- docs/plan/M0-PLAN.md: §1 Definition of Done (no 8 h cap, stores, deliveries, couriers), §2 content diagram, §7 schedule (CR-0008-T1 to T3
  between M0-60 and M0-61) and the realism check, board rows for the three tasks and the new task count; 13 "Deviations from the plan".
- Then open the task's PR as a draft.

Part B — Technical spec (docs only)
Read 04, 05, 06, 07, 09, 23, 19 and the module docs of the engine and the server.
- 04: building stores and the purse; projects (construction sites, upgrades, research at the science building) waiting for deliveries, and
  what a delivery fills first; the warehouse; trips; couriers, routes, stops, actions, amounts and rounding; tiles, roads, step times,
  building crossings at 20 % speed, the path rule (fewest buildings crossed, then least time) and its tie rule; stamina and the absence start
  as an input of Advance; the order at one second (research, building completions, trip arrivals, courier arrivals, starts); every new
  mutation and every changed one (place, upgrade, start research) with its checks and error codes; content rules (store limits, one science
  building per city, the shortest possible walk, route limits); invariants, the §11 report and the §12 budget; §2 removed.
- 05: the six new commands and the three changed ones, the snapshot (stores, purse, totals, projects with what they still need, roads, trips,
  couriers with their current walk, stamina), the settlement report, error codes and list limits. 06: tables, mapping, ledger rows for
  deliveries and COST_ROAD, the migration that resets dev cities (D10). 07: presence and the new inputs. 09: the screens and flows of T2.
- 23: every number (contentVersion mvp-2); golden scenario #1 rewritten (a first session: the science building, manual trips, deliveries to
  a construction site) and #2 new (a courier route, a road, an absence, tired couriers, re-energize), both checked by hand like the M0 table.
- Last step: server/tests/.../Fixtures/content-golden-v2.json, the frozen content of both golden scenarios (protected: say the session waits
  for the permission prompt; it is the owner's chance to look at the spec). Nothing loads it before part E; if a test loads every golden
  fixture, keep v2 out of it until then.

Part C — Engine: map tiles, roads and paths
Read 04 as part B left it and .claude/rules/simulation.md.
- Content: ground time per tile; the building-crossing speed (20 %); road levels (time per tile, coins per tile and per upgrade) and their
  content rules.
- City state: road tiles with their levels; a query for what is on a tile (ground, road, building).
- Path search: 4-direction steps through tile centres; fewest other buildings crossed first, then least time; integer step times; the
  walking bonus; rounded up; the fixed tie rule. Every building can always be reached.
- Mutations PlaceRoads, UpgradeRoads, RemoveRoads (a list of tiles, all or nothing, the checks and codes of 04); PlaceBuilding refuses a road tile.
- Tests: a rule test per check; brute force on small maps (no path crosses fewer buildings, and none with as few crossings is faster;
  a building is crossed only when every path crosses one); the tie rule; determinism. No change to production yet. Nothing places roads
  on the server before part D; if a server test needs the new state persisted, bring the roads table forward from part D and log it.

Part D — Server: roads end to end
Read 05, 06, docs/how-to/add-command.md and docs/how-to/add-migration.md.
- Table city_roads with its migration and mapping; roads in the snapshot; road levels in GET /content.
- Endpoints place-roads, upgrade-roads and remove-roads through the command pipeline; COST_ROAD ledger rows.
- Boundary (OD-13): an empty or too long tile list, a repeated tile or a negative coordinate is INVALID_ARGUMENT before the lock, each with a
  test that nothing was locked or stored.
- Integration tests for every success and refusal, and for idempotent replays.

Part E — The switch: building stores, deliveries and manual trips
The switch from city-wide stock to building stores. Engine, server and golden scenario #1 change together. Write this part's plan with its
checkpoints into the build log before code, and keep every checkpoint green.
- Engine: stores and limits per level; the storage research per building; the purse; start and stall rules per store; refunds into the
  store; the warehouse; the science building (one per city; research needs it, D11); projects: a construction site, an upgrade and a research
  wait for their goods after their coins are paid, and a delivery fills the project first (D12); a starting city with coins only; trips
  (state, StartTrip, arrival events, the worker busy until it arrives, the leftover back to the source); conservation counts carried and
  delivered goods. The offline cap stays until part H.
- Server: tables for stores, projects and trips; the migration that resets dev cities (D10: quote the owner's OK from this CR in the build
  log); mapping; the snapshot (stores, purse, totals,
  projects, trips); place-building, upgrade-building and start-research as part B changed them; the start-trip endpoint with its boundary
  checks; ledger rows for deliveries (moves between stores write none).
- Golden scenario #1 on content-golden-v2.json: the engine test and the HTTP test. Then delete content-golden-v1.json, and rewrite or remove
  the tests of rules this CR replaced, each with its reason in the build log (this CR's approval covers both).
- Client: keep it compiling and its tests green (the HUD reads the city's totals; run test-unity.sh); its new screens are T2.

Part F — Engine: courier station, couriers and routes
- Content: the courierStation building kind (per level: couriers, carry limit, stamina; level 1 costs coins only); amount presets; route
  limits; the research effects UnlockAmountMode (percent, exact) and WalkSpeedBonus; the Era I research nodes of 23.
- State: couriers (per station and slot: current walk, load with where each item came from, stamina) and routes (stops and actions).
- Mutation SetRoute with the checks of 04. AssignWorkers on a station adds or removes couriers; a removed courier's load goes back.
- Advance: courier arrivals as a new event source in the order of 04; the actions at a stop (a put fills a waiting project first); the loop.
  Until part H, no courier starts a walk after the production cap.
- Tests: amounts and rounding, the carry limit, full and empty stores, deliveries to projects, the loop, locked amount modes (exact with
  test content), determinism, conservation with loads.

Part G — Server: couriers and routes end to end
- Tables for couriers, routes and stops with their migration and mapping; the snapshot (each courier's current walk with its start, arrival
  and target, its load, and each route); the courier station and the amount presets in GET /content.
- Endpoint set-route with its boundary checks (OD-13: list lengths, amounts, ids).
- Integration tests for every success and refusal, and for idempotent replays; golden scenario #1 over HTTP stays green.

Part H — Courier stamina and presence
- Server: every authenticated city request sets cities.last_seen_at; the settle passes the absence start (last_seen_at plus the presence
  grace from the server options, multiplied by DevTimeScale) to Advance.
- Engine: stamina is spent only after the absence start; a courier without stamina finishes its stop, walks home and rests; mutation
  ReenergizeCouriers. The offline production cap and the dormant tail are removed (content rule, Advance, and the report, whose
  productionSeconds becomes the couriers' working time and which couriers got tired, when).
- Endpoint reenergize-couriers; stamina in the snapshot.
- Golden scenario #2 on content-golden-v2.json: the engine and HTTP tests.
- Tests: no stamina spent while present; split invariance for one absence start; timers unchanged; the report.

Part I — Engine review: invariants and performance
- Review the engine against 04 like M0-12, then extend the property harness of M0-14 and M0-15: determinism; split invariance for one
  absence start; timer invariance; conservation (stores, loads, reserved outputs, deliveries); no store above its limit except by refunds
  and returned loads; a path crosses a building only when every path does, and never more than needed; no stamina spent while present;
  workers on trips plus assigned workers never more than provided.
- Performance: the worst case that part B defines (most couriers, shortest walks, longest stamina) within the budget; the numbers go into
  04 §12 and the Simulation module doc.
- Then /finish-task: the draft PR becomes ready for review.
```

#### CR-0008-T2 — Client and art · [S]
```text
Follow "Running the grouped tasks" above. Read 09, docs/how-to/add-ui-screen.md and client/README.md. Every screen follows the M0-59
rules (one scroll direction at most, the rest scaled to fit, OD-17; phones from the iPhone XR on, OD-18) and joins the MenuGroup if it is a
menu (OD-16). Parts run in this order; each ends with its tests green (test-unity.sh, Editor closed) and a checkpoint.

Part A — Building stores, projects and manual trips
- Inspector: the building's store (each item as amount / limit) next to its stall reason; the HUD chips show the city's totals.
- Projects: a construction site, an upgrading building and the science building with a waiting research show what they still need; the
  research screen shows a research waiting for goods, and its start button is off with its reason until the science building stands.
- Trip flow from the inspector: pick the target (a building or a project) on the map, the item and All, Half or Quarter; the path drawn on
  the map by the shared engine (display only); the button is off with its reason (no free worker, nothing to carry, nothing needed there);
  it sends start-trip; feedback for every refusal code.
- A running trip shows on both ends with its countdown (the walking figure is part D).
- EditMode tests for the view models.

Part B — Roads
- Road mode from the build menu: drag a line of tiles; ghosts show the total cost and each refused tile's reason; confirming sends
  place-roads. Selected road tiles can be upgraded (upgrade-roads) or removed (remove-roads).
- The ground draws roads by level (the art of part F replaces the placeholder).
- Tests for the road-mode model.

Part C — Courier station and route editor
- Courier station inspector: its couriers (the worker stepper), each courier's state (working, resting), stamina and route summary.
- Route editor (a menu in the MenuGroup): add stops by tapping buildings; per stop, take and put actions with an item and an amount
  (presets; a percentage once researched; exact shown locked with the research that unlocks it); reorder and remove stops; the path and
  each walk's time on the map; it sends set-route; feedback for every refusal. A long route scrolls in one direction only and the rest
  scales to fit.
- Tests for the editor model.

Part D — Walkers on the map
- Couriers and trip workers walk from tile centre to tile centre with 90° turns, slowly across a building when their path crosses one. The
  position comes from the snapshot's current walk, the path from the shared engine and the display clock (display only, never sent).
  Resting couriers stand at their station; a load shows as an icon.
- Pooled views and no allocations per frame when nothing changes (like CityView); a PlayMode test.

Part E — Welcome-back report and re-energize
- Welcome-back report (M0-53): time away, the couriers' working time and when they got tired, goods moved and delivered, produced and
  consumed, coins, completed buildings and research; its main button Re-energize couriers (reenergize-couriers) when any courier spent stamina.
- A HUD notice while couriers rest; the same button in the courier station inspector.
- Every text about the 8 h production cap removed.
- Tests for the report model.

Part F — Art (Visual v2 adoption, Route A of visual-v2/ADOPT.md)
Read art/README.md, CR-0005 and visual-v2/ADOPT.md (Route A, from branch origin/claude/compassionate-meitner-sd7nr0).
- Before Part A, as its own checkpointed step: adopt visual-v2 (adopt.sh, unity-wiring.patch, after-cr0008.patch, merge held-back
  WelcomeBack.uxml/uss keeping T1's cap-note removal, move the Blender sources to art/blender/, move MOTIONS.md to art/MOTIONS.md,
  move ui-preview to art/ui/preview/, import in Unity and commit .meta files, add tests for the patched BuildingDecor rules).
- Do not generate new art assets: replace all old assets with visual-v2's assets; check that every building type, level, research node,
  item, road level and walker in server/content/mvp-content.json has its model and icon from visual-v2, merge ArtModelsTests with
  ArtModelsV2Tests, and list any missing assets for the owner to make for later adoption.

Then list every [MANUAL] look of parts A-F (what to open, what to check) in the build log for T3, and run /finish-task.
```

#### CR-0008-T3 — Close-out and playtest · [O] [MANUAL]
```text
- With the owner (about an hour): the [MANUAL] looks T2 listed (Device Simulator, M0-59 rules, OD-17, OD-18), the Blender run if T2 left
  it, a first session and an absence with DevTimeScale. Each problem becomes a P0-P3 issue in the build log.
- Fix P0 and P1 with regression tests (client, engine or server).
- Docs sync over this CR's impact table; every Docs impact row of T1 and T2 closed.
- docs/testing/LOCAL-E2E.md: a logistics run (a manual trip, a delivery to a construction site, a research with goods, a courier route, a
  road, an absence with DevTimeScale, re-energize).
- This CR's status becomes Done; the M0 board and 13 are updated.
```
Amendment 1 (2026-11-12, the owner's choice "Amend CR-0008, do it in T3") adds to T3: D14–D22 in the engine (`04` §18 and §19), the
content (`23`), the server (`start-trip` becomes a delivery, a new `cancel-delivery` command, the route gate in `set-route`, `05`, `06`
with a migration) and the client (the trip bar, the courier station's delivery queue with Cancel and Cancel all, the route editor's
reasons, the building menu layout), each rule with its tests, before the playtest goes on. The build log of T3 holds the plan.
Amendment 2 (2026-11-12, the owner's choice "Amend CR-0008, in T3") adds D23–D25: the engine (`04` §4, §6, §8, §19: the flag, the
`Paused` stall reason, `SetPaused`, the paused station's couriers), the server (`set-paused`, `paused` in the snapshot, `05`, `06` with a
migration) and the client (the Pause button in the building menu, the station's wait timer, the store gauge), each rule with its tests.

## Owner approval
- Approved on: 2026-11-02. The owner's words, in chat: "I approve the CR, go ahead with /start-task CR-0008-T1". The approval covers the
  whole proposal as written, so D2–D4, D6–D9 and D11–D13 stand as recommended; T1 started the same day (build log BL-20261102-07).
- 2026-11-02: the owner reviewed the first version and decided D1, D5 and D10 (quoted under "Owner's words"); the first version's
  D2–D4 and D6–D9 were not changed.
- 2026-11-08 (at the start of CR-0008-T2, in chat): `/start-task CR-0008-T2 You must use visual-v2/ADOPT.md` /
  `You must use new assets instead of old ones - change all of them for new. Do now generate new assets yourself. It you did not recieved some of the assets to change, from visual-v2, make a list of items that i should make for you for later adoption`. Persisted in Part F above and in `docs/build-log/2026/2026-11-08-01-CR-0008-T2.md`.
