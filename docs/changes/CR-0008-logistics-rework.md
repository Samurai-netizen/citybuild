# CR-0008 — Logistics rework: goods stored in buildings, couriers, routes and roads

- **Status:** Proposed
- **Type:** Design change
- **Requested by:** owner · **Date:** 2026-11-02
- **Milestone / era:** M0 / Era I (part of Era I's base set)
- **Conflicts with pillars (`22`) or the era rule (`25`):** no pillar changes, but the Era I text of `22` (protected) does: items are stored
  in buildings instead of city-wide; roads and couriers replace "there are no roads, and carts are only cosmetic"; courier stamina replaces
  the 8 h offline production cap; placement matters from Era I instead of Era V. Logistics is a major mechanic (`25` rule 5). The owner puts
  it into Era I's base set, so players have it from the start; only minor parts (percentages, exact amounts, faster roads) come by research.

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Owner's request (2026-11-02, in chat, quoted as written)
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

## Problem / goal
Today every item sits in one city-wide stock, so the map is only a place to put buildings: distance, layout and roads mean nothing (`22`).
The owner wants moving goods to be part of the game from the first session. Buildings keep what they make, goods travel on foot along
paths the player shapes with roads, and how long the city keeps working while the player is away depends on the couriers instead of a
fixed 8 hours. Building it inside M0, before the audits, balance simulator and test suites of Days 11–14, lets all of those cover
the game as it will be (see "When to build it").

## Proposal
Where a choice was needed, the proposal uses my recommendation; the "Decisions for the owner" table lists each one (D1–D10) with its alternative.

### 1. Goods live in buildings
- Every item except coins is kept in a building's **store**. Each level of a building type lists the items its store holds and a **limit**
  for each item (content). Limits are per item, so inputs can never fill the room a building's outputs need. A producer's store holds its
  recipe's inputs and outputs; houses and the courier station have none.
- A producer starts a cycle only when its **own** store has the inputs and room for the outputs. The stall reasons stay
  (`MissingInput:item`, `StorageFull:item`), now about that store.
- **Coins** are the city's purse: uncapped and not in any building. The Trading Post's coins go straight there.
- **Spending** (placing, upgrading, research, roads) takes the items from the stores of the whole city in a fixed order (D1). Goods that
  are being carried can't be spent.
- A **refund** (an upgrade ends a cycle) goes back into that building's store, also above its limit, as today. The Storehouses research
  raises the limit of its items in every building.
- A **new city** starts with coins only (D2): there is no building to keep goods in.
- A building that is being upgraded keeps its store, and goods can still be taken from it and delivered to it.

### 2. Paths and roads
- Walkers (couriers and workers on a trip) move from tile centre to tile centre in four directions, so every turn is 90°. They walk over
  empty ground and roads, never through another building: a building tile is only the start or the end of a walk (D5).
- **Time:** ground takes **5 s per tile** (owner). Each road level has its own, much shorter time. A step takes half the time of the tile
  it leaves plus half of the tile it enters (a building tile counts as ground). Walking-speed research shortens the total, and a walk is
  rounded up to whole seconds (the engine is integer-only, `04` §9).
- **Fastest path:** between two buildings the engine picks the path with the least total time (Dijkstra over the grid). It uses roads
  exactly when they make the walk faster, even when that path has more tiles. Ties are broken by a fixed rule (`04`), so the server and the
  client always pick the same path. A walk keeps the path it started with; a change to the map applies from the next walk.
- **Roads** are built on empty tiles in a line the player drags (one command for many tiles), instantly and without a builder slot (D6).
  Each road tile has a level; upgrading a tile makes it faster; a road can be removed (no refund). Buildings can't be placed on a road.
  M0 has road levels 1–2; more come with later content.

### 3. Manual trips (before or without couriers)
- From a building's inspector the player sends one **free worker** on a one-time trip: item, amount (the Era I presets), target building.
  It needs a free worker and a path.
- The worker takes the goods at once (at most the trip carry limit), walks to the target by the fastest path, delivers what fits and is
  free again on arrival. What doesn't fit goes back to the source (D7). Trips run in real time, also while the player is away, and never tire.
- Trips stay available after a courier station exists (D7).

### 4. Courier station, couriers and routes
- The **Courier Station** is a new kind of building, available from the start. Workers assigned to it are its **couriers**, one per
  worker, up to its level's limit. Its level also sets each courier's **carry limit** (all items together) and **stamina** (D4).
- Each courier has one **route**: an ordered list of **stops**, from 2 up to a content limit, with different buildings at two stops in a
  row. A stop is a building and one or more **actions**: take or put, an item, an amount. The courier starts at its station and walks to
  stop 1, 2, … the last, then stop 1 again, and so on.
- At a stop the actions run in order, at once. **Take** removes goods from the building's store, as much as the amount, the stock and the
  free carry room allow. **Put** delivers carried goods, as much as the amount and the store's free room allow; the rest stays on the
  courier. A courier never waits for goods: it does what it can and walks on.
- **Amounts:** Era I starts with **All, Half and Quarter** (D3): of the building's stock for a take, of what the courier carries of that
  item for a put; rounded down, but at least 1 when there is any. An Era I research unlocks **any percentage** (1–100). An **exact
  number** is unlocked by an Era II research; the engine and the API support it now, and it stays locked until that research exists (OD-10).
- A stop with no path is skipped and shown as unreachable. A courier with no reachable stop waits at its station until the map changes.
- When a courier stops working (its worker is removed, its route is replaced, its station starts an upgrade), the goods it carries go back
  to the buildings they came from, like a refund, and it starts again from its station.
- Couriers are drawn on the map walking their paths; research makes them walk faster (D4).

### 5. Courier stamina replaces the 8 h offline production cap
- The fixed **offline production cap** (8 h) and the dormant tail (`04` §2) are removed. Production stops on its own: a building stops when
  its store is full or its inputs run out, and inputs only arrive by courier or trip.
- **Stamina** is how long a courier keeps working while the player is away. It comes from its station's level (for example 4 h at level 1,
  8 h at level 2). It is spent **only while the player is absent**; while the game is open, couriers never tire.
- **Presence is decided by the server** (D8). The open game talks to the server at least every 120 s (`09`, polling). The player counts as
  absent from 5 minutes after their last request (the presence grace) until their next one. The client sends no time and no "I'm here" flag.
- When a courier's stamina is used up, it finishes the stop it is walking to, walks back to its station and **rests**. Its load stays with it.
- **Re-energize:** a button (a new command) refills every courier's stamina, and resting couriers go back to their routes (D9).
- Stamina also does the old cap's second job: it bounds how much one settlement has to simulate (`04` §12).

### 6. Welcome-back report
The report (M0-53) is reworked around the couriers: time away; how long the couriers worked and when each station's couriers got tired;
goods moved; produced and consumed; coins earned; completed buildings and research. Its main button is **Re-energize couriers** when any
courier has spent stamina. Closing the report without it leaves the couriers resting; the HUD shows that, and the courier station's
inspector has the same button.

### 7. Later eras
Exact amounts arrive with an Era II research. Later eras replace couriers with carts, wagons and finally cargo trucks: the same routes and
rules with more capacity, more speed and their own stamina, and better roads (stone in Era II). `25` records this now; each era builds its part.

### First numbers (placeholders: T2 sets them in `23`, M0-67 and M0-68 balance them)
| What | Level 1 | Level 2 |
|---|---|---|
| Courier Station (available at start) | 2 couriers · carry 10 · stamina 4 h · 120 coins, 60 s | 4 couriers · carry 15 · stamina 8 h · upgrade 250 coins + 30 planks, 180 s |
| Woodcutter's Hut store | 40 logs | 80 logs |
| Sawmill store | 40 logs · 20 planks | 80 logs · 40 planks |
| Trading Post store | 40 planks | 80 planks |
| Road tile | Dirt path: 2.5 s per tile · 5 coins | Log road: 1.5 s per tile · upgrade 5 coins + 2 logs |

Ground: 5 s per tile. Trip carry limit: 10. New Era I research: **Tally Sticks** (any percentage) and **Sturdy Boots** (walking 20 % faster).

## Decisions for the owner
| # | Question | Recommended (used above) | Alternative |
|---|---|---|---|
| D1 | Where do costs (placing, upgrades, research, roads) come from? | The stores of the whole city, in a fixed order: buildings that make the item first, then buildings that use it, each by id | Only from a storehouse building that couriers fill (a new building, and every cost needs hauling first) |
| D2 | What does a new city start with? | Coins only; T2 rebalances the first costs so the first coins still come within about 20 minutes (`24` goal 1) | A starting stockpile building holding today's 40 logs and 10 planks |
| D3 | Which amounts does Era I offer before research? | All, Half, Quarter | Add Three quarters, or Third |
| D4 | What makes couriers better? | Station level: more couriers, carry limit, stamina. Research: walking speed. Roads: speed on their tiles | Speed from the station level too, or upgrades per courier |
| D5 | Can walkers cross buildings? | No, only ground and roads; a stop with no path is skipped and shown | Yes: never blocked, but couriers walk through houses and roads matter less |
| D6 | How are roads built? | Instantly, no builder slot, a line of tiles per command; upgrades instant; removal free, no refund | As constructions with a build time and a builder slot |
| D7 | Manual trips | Stay available after a courier station exists; carry at most the trip limit; what doesn't fit at the target goes back to the source | Only until the first station is built; or the target takes everything, above its limit |
| D8 | When is the player absent? | From 5 minutes after their game's last request (multiplied by `DevTimeScale` on dev servers); any request counts. A script that keeps polling keeps couriers awake, like an open game: accepted in M0, revisited with the market (M2) | A separate presence call (no safer: the client is untrusted either way) |
| D9 | Re-energizing | Free, any time, refills every courier of the city | Per station, or with a cost or a cooldown |
| D10 | Existing dev cities | The migration resets every dev city to the new starting city (no real players yet; G-Destructive, OD-14) | Convert today's stock into building stores (complex, no value now) |

## Acceptance criteria
- [ ] Items other than coins exist only in building stores, on couriers and on trip workers; no store goes above its limit except by a
      refund or a returned load (engine property test).
- [ ] A producer starts a cycle only with the inputs in its own store and room there for the outputs; otherwise its stall reason names
      that store's missing input or full output.
- [ ] Coins go to the city purse; costs are paid from the stores in the order of `04`, or refused with `INSUFFICIENT_RESOURCE`.
- [ ] A manual trip needs a free worker and a path; the worker is busy until it arrives and free afterwards; it moves the chosen amount.
- [ ] A courier station's workers are its couriers; each follows its route of 2 or more stops in a loop, taking and putting the chosen
      amounts within its carry limit, the source's stock and the target's free room.
- [ ] Era I offers All, Half and Quarter; any percentage after its research; an exact number is refused until an Era II research unlocks
      it (tested with test content).
- [ ] The engine's path is the fastest by walking time (never slower than any other path, checked by brute force on small maps), uses
      only 4-direction steps through tile centres and never enters another building; ground costs 5 s per tile and every road level is faster.
- [ ] Roads can be placed on empty tiles, upgraded and removed; a building can't be placed on a road.
- [ ] Couriers spend stamina only after the absence start that the server derives from the player's last request; a courier without
      stamina walks home and rests; re-energizing refills every courier.
- [ ] There is no fixed offline production cap; the settlement report gives the couriers' working time and which couriers got tired, when.
- [ ] The welcome-back report shows the courier summary and the re-energize button; the trip flow, road mode and route editor work on a
      phone screen by the M0-59 rules: one scroll direction at most, the rest scaled to fit (OD-17), phones from the iPhone XR on (OD-18).
- [ ] Golden scenario #1 (rewritten) and #2 (new) pass in the engine and over HTTP; determinism, split invariance (for one absence start),
      timer invariance, conservation and the new performance budget pass.
- [ ] Every new command refuses values no city could make valid (ids below 1, negative coordinates, too many tiles, stops or actions, a
      percentage outside 1–100) as `INVALID_ARGUMENT` before the city is locked (OD-13), with tests.
- [ ] Every page in the impact table below is updated, `check-docs.sh` and the handbook build pass, and the CR is Done when every task is merged.

## Out of scope
- Era II content (it brings the research for exact amounts), carts, wagons and trucks, stone and paved roads.
- A storehouse or warehouse building, demolishing buildings, buildings larger than one tile.
- Couriers blocking each other, loading time at a stop, diagonal steps, waiting at a stop for goods.
- Notifications for resting couriers (with the other notifications in M1).
- Where market deliveries land once goods live in buildings (decided with the market, M2).
- The rest of the Era I content (stone, grain and bread chains, M1).

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | Stock moves into building stores; new state (stores, roads, trips, couriers, routes, stamina); path search; trip and courier arrivals as new event sources in `Advance`; new mutations; §2 (cap, dormant tail) removed; the absence start becomes an input of `Advance`; invariants, §11 report and §12 budget change | `shared/Simulation/Runtime/**`, `04` |
| Content (`23`, JSON) | Store limits per level, the courier station (a new building kind), road levels, walking time, trip carry limit, stamina, amount presets, new effect types and Era I research, a starting city without goods, `contentVersion` `mvp-2`; the golden fixture (protected) | `server/content/mvp-content.json`, `23`, `server/tests/**/Fixtures/content-golden-*.json` |
| API (`05`) | Breaking (G-Contract, covered by this CR's approval): the snapshot's `inventory` and `storageCaps` become stores, purse and totals; roads, trips, couriers, routes and stamina are added; the settlement report changes; six commands (`place-roads`, `upgrade-roads`, `remove-roads`, `start-trip`, `set-route`, `reenergize-couriers`); new error codes (for example `NO_PATH`, `AMOUNT_MODE_LOCKED`, `INVALID_ROUTE`); limits on every list | `05`, `server/src/CityBuilder.Api/**`, `server/src/CityBuilder.Application/**` |
| Data / migration (`06`) | Tables for stores, roads, trips, couriers and routes; `cities.last_seen_at`; coins stay as the purse; ledger reason `COST_ROAD` (moves write no ledger rows: they don't change the city's totals); a migration that resets dev cities (D10) | `06`, `server/src/CityBuilder.Infrastructure/Persistence/**` |
| Security (`07`) | Presence is derived by the server from request times; the new client inputs (tile lists, routes, amounts) are bounded and refused at the boundary; a polling script keeps couriers awake (known limitation) | `07` |
| Client (`09`) | Store in the inspector, HUD totals, trip flow, road mode, courier station inspector, route editor (a new menu, OD-16), walkers on the map, welcome-back rework; the display prediction runs couriers too | `09`, `client/CityBuilder/Assets/_Project/**`, `client/README.md` |
| Art | Courier station, courier figure, road tiles, icons | `art/**` |
| Design docs | `22` (protected), `21`, `24`, `25`, `26` binding rule 4, `31`, `10`, `12`; the M0 plan (Definition of Done, §2, schedule, board); `13` | `core-instructions/**`, `docs/plan/M0-PLAN.md` |
| Docs (recipes in `19`) | Module docs of every touched project and assembly; how-tos `add-command.md`, `add-building-or-item.md`, `add-research-effect.md`, `add-ui-screen.md`; `docs/testing/LOCAL-E2E.md`; `CHANGELOG.md` | `docs/**` |
| Tests (`08`) | A rule test per rule; property tests; golden #1 rewritten and #2 new (engine and HTTP); integration tests per command with the OD-13 refusals; the performance budget; Unity EditMode and PlayMode tests | `shared/`, `server/tests/**`, `client/…/Tests/**` |

## Risks and alternatives
- **Size.** 16 tasks, about 3–5 working days on top of M0's realistic 17–21 days. The riskiest step is T5: the storage model changes in the
  engine, the database and the HTTP golden test at once, so it is a `[PLAN]` task with checkpoints.
- **Tests change with the rules.** Golden scenario #1 and the tests built on the 8 h cap change because the design changes, not to get
  green: each change gets its written reason in the task's build log (G-Tests), and the protected golden fixture needs the owner's OK.
- **Performance.** Courier walks can outnumber production cycles (short walks on roads). Stamina bounds them while the player is away;
  T13 sets and measures the new budget.
- **Presence by polling.** A script that keeps polling keeps couriers awake, like an open game. It matters once goods can be sold to other
  players (M2): revisit then, for example with a daily limit on awake time or device attestation (M3).
- **Split invariance changes meaning.** It holds for one absence start; a request is itself presence, so a request between two advances can
  change the outcome. The dev time scale must also scale the presence grace, or couriers would tire between polls.
- **First session gets harder.** Before the first courier station the player moves goods by hand. `24` goal 1 (first coins within about 20
  minutes) is at risk: T2 rebalances, M0-67 and M0-68 measure, M0-71 adds hints.
- **No punishing timers (`21`).** Resting couriers are the cap on production that `21` allows; the re-energize button must not nag (no
  countdown, no penalty beyond the stop).
- **Unity can't run in a cloud session.** The client tasks need the owner's `test-unity.sh` run and the [MANUAL] looks.
- Alternative: build it in M1. M0's balance runs, audits and test suites would then be redone on the new model.
- Alternative: keep city-wide stock and make couriers decoration. Rejected: the owner wants logistics as a mechanic.

## When to build it
**Recommended: right after M0-60 (in progress while this CR was written), before M0-61.** The order becomes … M0-59, M0-60,
CR-0008-T1 … CR-0008-T16, M0-61, M0-62 …
- **Every later M0 task covers the new game once instead of twice.** M0-63 (docs completeness), M0-64 (ledger audit: it must now add up
  stores and loads) and M0-66 (architecture review) see the final model. M0-67 (balance simulator) can only balance the game if its
  strategies build stations, routes and roads. Day 13's suites (M0-73 to M0-76) and M0-78's security report test the new commands, and
  Day 14 closes M0 on them.
- **What it builds on is done:** the command pipeline, persistence, the screens it extends (inspector, welcome-back report, menus), the
  phone-screen rules of M0-59 (OD-17, OD-18) and the art pipeline (CR-0005).
- **Not earlier:** M0-60 would be left half done. Its boundary review covers today's commands; each new command gets its boundary checks and
  their tests in its own task (OD-13), and M0-74's attack suite covers them again.
- **Not later than M0-66:** after the balance simulator, Days 12 and 13 would have to be redone.
- Workable alternative: after M0-62, so DocTools (generated reference docs) and the quality script help the rework; the cost is two more
  tasks done on the old model (M0-61 regenerates anyway).

## Tasks
Written now because the owner asked for them with the proposal (`15` normally adds them after approval). `/start-task CR-0008-T1` works once
this CR is Approved; T1 adds the tasks to the M0 board. Model: O = Opus 5.5, S = Sonnet 5.5.

| ID | Title | Model | Needs | Status |
|---|---|---|---|---|
| CR-0008-T1 | Design docs and plan update `[PLAN]` | O | approval | Todo |
| CR-0008-T2 | Technical spec: engine, API, data, content numbers, golden scenarios | O | T1 | Todo |
| CR-0008-T3 | Engine: map tiles, roads and fastest paths | O | T2 | Todo |
| CR-0008-T4 | Server: roads end to end | O | T3 | Todo |
| CR-0008-T5 | Building stores and manual trips: engine, server, golden scenario #1 `[PLAN]` | O | T4 | Todo |
| CR-0008-T6 | Client: building stores and manual trips | S | T5 | Todo |
| CR-0008-T7 | Client: roads | S | T4 | Todo |
| CR-0008-T8 | Engine: courier station, couriers and routes | O | T5 | Todo |
| CR-0008-T9 | Server: couriers and routes end to end | O | T8 | Todo |
| CR-0008-T10 | Client: courier station and route editor | S | T9 | Todo |
| CR-0008-T11 | Client: walkers on the map | S | T9 | Todo |
| CR-0008-T12 | Courier stamina and presence; the 8 h cap removed; golden scenario #2 | O | T9 | Todo |
| CR-0008-T13 | Logistics engine review: invariants and performance | O | T12 | Todo |
| CR-0008-T14 | Client: welcome-back report and re-energize | S | T12 | Todo |
| CR-0008-T15 | Art: courier station, courier and roads `[MANUAL]` | S | T2 (any time) | Todo |
| CR-0008-T16 | Logistics close-out and playtest `[MANUAL]` | O | all | Todo |

Run them in this order; T15 can move anywhere after T2, since until then the client draws placeholders.

### Task specs
Each spec is the prompt for its task, on top of the standing rules (`AGENTS.md`, `16`, the change recipes in `19`). "This CR" is the design source.

#### CR-0008-T1 — Design docs and plan update · [O] [PLAN]
```text
Read this CR, 21, 22, 24, 25, 26, 31, 10, 12, 15 and docs/plan/M0-PLAN.md. Docs only, no code.
Present the plan (each page and what changes in it) and wait for the owner's approval. Then:
- 22 (protected: the approval of this CR covers it; the permission prompt still shows): Items and recipes, Workers, Grid and land,
  Storage, builders, offline, and the Trading Post as this CR describes them. The six pillars stay word for word.
- 21 (walkers and roads are mechanics, not decoration), 24 (logistics as a scarce resource; the caps on production), 25 (logistics in
  Era I's base set; percentages and exact amounts by research; vehicles in later eras), 26 (binding rule 4 for building stores; market
  deliveries need a destination, M2), 31 (store, limit, purse, walker, courier, courier station, route, stop, action, trip, road,
  stamina, presence), 10 (M0 scope), 12 (a row for each decision D1-D10 as approved).
- docs/plan/M0-PLAN.md: §1 Definition of Done (no 8 h cap, stores, couriers), §2 content diagram, §7 schedule (the CR-0008 tasks
  between M0-60 and M0-61) and the realism check, board rows for the 16 tasks and the new task count; 13 "Deviations from the plan".
```

#### CR-0008-T2 — Technical spec · [O]
```text
Read this CR, 04, 05, 06, 07, 09, 23, 19 and the module docs of the engine and the server. Docs only, no code.
- 04: building stores and the spending order (D1); trips; couriers, routes, stops, actions, amounts and rounding; tiles, roads, step
  times, the fastest-path rule and its tie rule; stamina and the absence start as an input of Advance; the order at one second
  (research, building completions, trip arrivals, courier arrivals, starts); every new mutation with its checks and error codes; content
  rules (store limits, the shortest possible walk, route limits); invariants, the §11 report and the §12 budget; §2 removed.
- 05: the six commands, the snapshot (stores, purse, totals, roads, trips, couriers with their current walk, stamina), the settlement
  report, error codes and list limits. 06: tables, mapping, COST_ROAD, the migration that resets dev cities (D10). 07: presence and the new
  inputs. 09: the screens and flows of T6, T7, T10, T11 and T14.
- 23: every number (contentVersion mvp-2); golden scenario #1 rewritten (a first session with manual trips) and #2 new (a courier route,
  a road, an absence, tired couriers, re-energize), both checked by hand like the M0 table.
```

#### CR-0008-T3 — Engine: map tiles, roads and fastest paths · [O]
```text
Read 04 as T2 left it and .claude/rules/simulation.md.
- Content: ground time per tile; road levels (time per tile, cost and upgrade cost per tile) and their content rules.
- City state: road tiles with their levels; a query for what is on a tile (ground, road, building).
- Path search: the fastest path between two buildings; 4-direction steps through tile centres; never through another building;
  integer step times; the walking bonus; rounded up; the fixed tie rule; none when there is no path.
- Mutations PlaceRoads, UpgradeRoads, RemoveRoads (a list of tiles, all or nothing, the checks and codes of 04); PlaceBuilding refuses a
  road tile.
- Tests: a rule test per check; never slower than any other path (brute force on small maps); never enters a building; the tie rule;
  determinism. No change to production. Nothing places roads on the server before T4; if a server test needs the new state persisted,
  bring the roads table forward from T4 and log it.
```

#### CR-0008-T4 — Server: roads end to end · [O]
```text
Read 05, 06, docs/how-to/add-command.md and docs/how-to/add-migration.md.
- Table city_roads with its migration and mapping; roads in the snapshot; road levels in GET /content.
- Endpoints place-roads, upgrade-roads and remove-roads through the command pipeline; COST_ROAD ledger rows.
- Boundary (OD-13): an empty or too long tile list, a repeated tile or a negative coordinate is INVALID_ARGUMENT before the lock, each with
  a test that nothing was locked or stored.
- Integration tests for every success and refusal, and for idempotent replays.
```

#### CR-0008-T5 — Building stores and manual trips · [O] [PLAN]
```text
The switch from city-wide stock to building stores. The engine, the server and the golden scenario change together, so main stays green.
Present the plan with its checkpoints and wait for the owner's approval.
- Engine: stores and limits per level; the storage research per building; the coins purse; start and stall rules per store; the
  spending order (D1); refunds into the store; a starting city with coins only; trips (state, StartTrip, arrival events, the worker busy
  until it arrives, the leftover back to the source); conservation counts carried goods. The offline cap stays until T12.
- Server: tables for stores and trips; the migration that resets dev cities (D10; record the owner's OK in the build log); mapping; the
  snapshot (stores, purse, totals, trips); the start-trip endpoint with its boundary checks. Moves write no ledger rows.
- Golden scenario #1 as T2 rewrote it: the engine test, the HTTP test and the golden fixture (protected: ask).
- Client: keep it compiling and its tests green (the HUD reads the city's totals); its new screens are T6.
```

#### CR-0008-T6 — Client: building stores and manual trips · [S]
```text
Read 09, docs/how-to/add-ui-screen.md and client/README.md.
- Inspector: the building's store (each item as amount / limit) next to its stall reason; the HUD chips show the city's totals.
- Trip flow from the inspector: pick the target building on the map, the item and All, Half or Quarter; the path drawn on the map by the
  shared engine (display only); the button is off with its reason (no free worker, no path, nothing to carry); it sends start-trip;
  feedback for every refusal code.
- A running trip shows on both buildings with its countdown (the walking figure is T11).
- EditMode tests for the view models; a [MANUAL] look in the Device Simulator (M0-59 rules, OD-17, OD-18).
```

#### CR-0008-T7 — Client: roads · [S]
```text
- Road mode from the build menu: drag a line of tiles; ghosts show the total cost and each refused tile's reason; confirming sends
  place-roads. Selected road tiles can be upgraded (upgrade-roads) or removed (remove-roads).
- The ground draws roads by level (a placeholder look until T15).
- Tests for the road-mode model; a [MANUAL] look.
```

#### CR-0008-T8 — Engine: courier station, couriers and routes · [O]
```text
Read 04 as T2 left it.
- Content: the courierStation building kind (per level: couriers, carry limit, stamina); amount presets; route limits; the research
  effects UnlockAmountMode (percent, exact) and WalkSpeedBonus; the Era I research nodes of 23.
- State: couriers (per station and slot: current walk, load with where each item came from, stamina) and routes (stops and actions).
- Mutation SetRoute with the checks of 04. AssignWorkers on a station adds or removes couriers; a removed courier's load goes back.
- Advance: courier arrivals as a new event source in the order of 04; the actions at a stop; a stop with no path is skipped; the loop.
  Until T12, no courier starts a walk after the production cap.
- Tests: amounts and rounding, the carry limit, full and empty stores, skipping, the loop, locked amount modes (exact with test content),
  determinism, conservation with loads.
```

#### CR-0008-T9 — Server: couriers and routes end to end · [O]
```text
- Tables for couriers, routes and stops with their migration and mapping; the snapshot (each courier's current walk with its start,
  arrival and target, its load, and each route); the courier station and the amount presets in GET /content.
- Endpoint set-route with its boundary checks (OD-13: list lengths, amounts, ids).
- Integration tests for every success and refusal, and for idempotent replays; golden scenario #1 over HTTP stays green.
```

#### CR-0008-T10 — Client: courier station and route editor · [S]
```text
- Courier station inspector: its couriers (the worker stepper), each courier's state (working, resting, no path), stamina and route summary.
- Route editor (a menu in the MenuGroup, OD-16): add stops by tapping buildings; per stop, take and put actions with an item and an amount
  (presets; a percentage once researched; exact shown locked with the research that unlocks it); reorder and remove stops; the path and
  each walk's time on the map; it sends set-route; feedback for every refusal. A long route scrolls in one direction only and the rest
  scales to fit (OD-17).
- Tests for the editor model; a [MANUAL] look on a phone-sized screen (OD-18).
```

#### CR-0008-T11 — Client: walkers on the map · [S]
```text
- Couriers and trip workers walk from tile centre to tile centre with 90° turns. The position comes from the snapshot's current walk, the
  path from the shared engine and the display clock (display only, never sent). Resting couriers stand at their station; a load shows as
  an icon.
- Pooled views and no allocations per frame when nothing changes (like CityView); a PlayMode test.
```

#### CR-0008-T12 — Courier stamina and presence · [O]
```text
- Server: every authenticated city request sets cities.last_seen_at; the settle passes the absence start (last_seen_at plus the presence
  grace from the server options, multiplied by DevTimeScale) to Advance.
- Engine: stamina is spent only after the absence start; a courier without stamina finishes its stop, walks home and rests; mutation
  ReenergizeCouriers. The offline production cap and the dormant tail are removed (content rule, Advance, and the report, whose
  productionSeconds becomes the couriers' working time and which couriers got tired, when).
- Endpoint reenergize-couriers; stamina in the snapshot.
- Golden scenario #2 as T2 wrote it: the engine and HTTP tests.
- Tests: no stamina spent while present; split invariance for one absence start; timers unchanged; the report.
```

#### CR-0008-T13 — Logistics engine review: invariants and performance · [O]
```text
Review the engine against 04 like M0-12, then extend the property harness of M0-14 and M0-15: determinism; split invariance for one
absence start; timer invariance; conservation (stores, loads, reserved outputs); no store above its limit except by refunds and
returned loads; paths optimal and never through a building; no stamina spent while present; workers on trips plus assigned workers
never more than provided.
Performance: the worst case that T2 defines (most couriers, shortest walks, longest stamina) within the budget; the numbers go into 04
§12 and the Simulation module doc.
```

#### CR-0008-T14 — Client: welcome-back report and re-energize · [S]
```text
- Welcome-back report (M0-53): time away, the couriers' working time and when they got tired, goods moved, produced and consumed, coins,
  completed buildings and research; its main button Re-energize couriers (reenergize-couriers) when any courier spent stamina.
- A HUD notice while couriers rest; the same button in the courier station inspector.
- Every text about the 8 h production cap removed.
- Tests for the report model; a [MANUAL] look.
```

#### CR-0008-T15 — Art: courier station, courier and roads · [S] [MANUAL]
```text
Read art/README.md and CR-0005.
- Blender scripts in art/blender/: the courier station at levels 1 and 2, a courier figure with a pack, a road tile per level that reads
  well alone and joins its neighbours, and UI icons for the courier station, roads, trips and re-energize.
- [MANUAL] the owner runs the Blender build; the client's looks pick the models up (BuildingLooks, the ground); lookup tests as in
  CR-0005-T3.
```

#### CR-0008-T16 — Logistics close-out and playtest · [O] [MANUAL]
```text
- Docs sync over this CR's impact table; every Docs impact row of T1-T15 closed.
- docs/testing/LOCAL-E2E.md: a logistics run (a manual trip, a courier route, a road, an absence with DevTimeScale, re-energize).
- [MANUAL] the owner plays a first session and an absence; each problem becomes a P0-P3 issue in the build log; P0 and P1 are fixed with
  regression tests.
- This CR's status becomes Done; the M0 board and 13 are updated.
```

## Owner approval
- Approved on: — (waiting for the owner: approve, change or reject; name any decision D1–D10 to change)
