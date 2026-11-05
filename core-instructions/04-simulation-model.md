# Simulation Model — the production engine

The core mechanic is **waiting while your workers run production chains and carry the goods**. This file is the spec
for the engine that computes what happened during that waiting. It lives in `shared/Simulation/`,
runs authoritatively on the server, and runs on the client for display only.

> **CR-0008 (logistics) is specified here and built in CR-0008-T1, part by part.** Built so far: the walking rules and road levels in
> content (§14 rules 11, 12), roads in the city state, the path rule (§17) and the road mutations (§8) (part C); stores, the purse and
> projects (§16), the delivery rule (§6), amounts and presets (§19 without routes), trips (§18) with `StartTrip` (§8), the report's
> `delivered`, `moved` and `refunded` (§11), and the start rule per store (part E); couriers and routes (§19: `SetRoute`, staffing a
> station, an upgrade stopping its couriers, arrivals and departures in the order of §6, a stop's takes and puts) (part F); presence,
> stamina and `ReenergizeCouriers` (§2, §8, §11), with the offline production cap and its dormant tail removed (part H). Part I (review,
> performance) updates this line. Sections 16 to 19 are new; sections 1 to 15 keep their numbers (other files refer to them).

## 1. Time
- Simulation time is **integer seconds** (`long`) on a per-city **city clock**.
- The server converts `now` (`TimeProvider`, UTC, floored to whole seconds) into
  `elapsed = nowSec − city.LastSettledUnixSec`. `elapsed < 0` → 0, keep `LastSettledUnixSec`, log a warning
  (`SettlementWindow.Compute`; the engine computes, the server logs). Each settle starts where the last one ended, so floored fractions are never lost.
- Every settlement advances the clock by the **full** elapsed time.
- Client time is never an input. The **absence start** (§2) is an input of the advance that the server derives; the client passes none.
- Development servers may run a scaled `TimeProvider` (`Simulation:DevTimeScale`, e.g. 60 = one hour per minute, 1 to 1000).
  The engine doesn't know about it; startup refuses any scale ≠ 1 outside Development. It counts from a fixed origin
  (`now = origin + (real now − origin) × scale`), so it continues across restarts; changing the scale moves it, so the dev database is
  reset after a change (`docs/getting-started.md` → Configuration).

## 2. Presence and courier stamina (the offline production cap is removed)
The fixed offline production cap and its dormant tail are gone (CR-0008). Production stops on its own: a producer stops when its store is
full or its inputs run out (§6), and goods move only with walkers (§18, §19). What bounds a long absence is the couriers' **stamina**.
- **Presence is the server's:** every authenticated city request (`GET /city`, every command that settles; not a replay) records its
  server second as the city's last-seen time. The player counts as **absent** from `lastSeen + grace` until their next request; the grace
  is `Simulation:PresenceGraceSeconds` (300) × `DevTimeScale`. The settle passes that second, on the city clock, to the advance as
  `absentFrom` (null: present for the whole window); the client sends no time and no "I'm here" flag (`07`).
- **Stamina** (seconds) is spent only by walks, and only for the part of a walk at or after `absentFrom`: a walk from `d` to `a` spends
  `max(0, a − max(d, absentFrom))`, counted when it arrives. While the game is open, couriers never tire.
- A courier whose stamina reaches 0 at an arrival does that stop's actions, then walks home and **rests** with its load (§19). Only
  `ReenergizeCouriers` (§8) refills stamina.
- Timers (constructions, upgrades, research) run for the whole window, as before; a project's timer starts when its goods are in (§16).
- Why: it bounds how much one settlement has to simulate (§12), gives players a reason to check in, and never delays research.

## 3. Workers and labor
- Completed Houses provide workers (per level; an upgrading house those of its current level until the upgrade completes).
  `free = provided − Σ assigned − workers on trips` (§18).
- Every producer and courier station level has `MaxWorkers`. `0 ≤ assigned ≤ MaxWorkers`. Placing a building never needs workers. The
  workers of a courier station are its couriers, one per worker (§19); they do no labor.
- A recipe costs **labor** in worker-seconds per cycle. Effective labor =
  `ceil(Labor × (1000 − Σ reductions‰) / 1000)`, summing the `LaborReduction`s of the building's type in completed research.
- With `w` workers, a cycle takes `ceil(remainingLabor / w)` seconds. More workers → faster. `w = 0` → frozen.
- **Changing workers keeps progress:** at time `t`, `done = (t − SegmentStartedAt) × wOld`,
  `LaborRemaining = max(0, LaborRemaining − done)`, `SegmentStartedAt = t`,
  `PhaseEndsAt = t + ceil(LaborRemaining / wNew)`, or `null` (frozen) if `wNew = 0`. This runs on every assignment to a producing
  building: the same count again starts a new segment but never moves the end.
- **Auto-staffing:** when construction completes, a producer or courier station gets `min(free, MaxWorkers)` workers.
  Nothing else is assigned automatically. Assigning 0 workers is how a player pauses a building.
- Houses, warehouses and the science building have no recipe and take no workers. During an upgrade, workers stay assigned but do nothing.

## 4. Building phases
| Phase | Meaning | Timer fields |
|---|---|---|
| `Constructing` | Placed, not usable yet; uses a builder slot. A **construction site** while goods are missing (§16): no timer | `PhaseStartedAt`, `PhaseEndsAt` (both 0/null while waiting) |
| `Upgrading` | No production; uses a builder slot; level rises at end | `PhaseStartedAt`, `PhaseEndsAt`, `TargetLevel` |
| `Idle` | Ready, not producing | — |
| `Producing` | Inputs consumed, outputs reserved | `CycleLabor`, `LaborRemaining`, `SegmentStartedAt`, `PhaseEndsAt` (null if frozen) |

Fields a phase doesn't use are 0 (`PhaseEndsAt` null), and `Producing` keeps the cycle's start in `PhaseStartedAt`, so equal situations are
equal field by field. An `Idle` or `Producing` building may also have an **upgrade waiting for goods** (§16): it keeps working.
**Stall reason** (derived, never stored), shown to players. Constructing and upgrading buildings, cycles running with workers, idle
producers that can start and courier stations with couriers have none. For every other building the first match wins:
`NoRecipe` (houses, warehouses, the science building) · `NoWorkers` (idle producer or frozen cycle, courier station without couriers) ·
`MissingInput:item` · `StorageFull:item`, both about the building's **own store** (§16). The item is the first in recipe order (ordinal ids).

## 5. Research
- One research slot in M0. Research happens at the **science building** (§16): `StartResearch` needs a completed one, pays the node's
  coins, fills the slot, and waits for the node's goods to be delivered there. Its timer starts when nothing is missing:
  `ActiveResearch.EndsAt = t + Duration` (at once when the node costs coins only). Research needs no workers.
- On completion, the node is added to `CompletedResearch` and its effects apply at that moment:
  `UnlockBuilding(type)`, `LaborReduction(type, ‰)`, `StorageCapBonus(item, +n)` (raises the limit of the item in every store that holds
  it, §16), `UnlockAmountMode(mode)` (§19), `WalkSpeedBonus(‰)` (§17) (M0 set; more types in `25`).
- A cycle that starts from that second on uses the effects (completion comes before starts, §6), and so does a walk. Running cycles keep
  their labor, walks their arrival.
- **Era transition** *(planned: built with Era II, `25`)*: when every research node of the current era is completed, `Era += 1` at that
  moment. The M0 engine never changes `Era`, and the report's `eraChanged` is always false (decision 031).

## 6. Rules
**Start rule:** a producer starts a cycle at time `t` if it is `Idle`, has `w ≥ 1`, its own store holds every input, and every output
that is not the currency fits its own store (`stock + qty ≤` the effective limit, §16; nothing is reserved while it is idle). On start:
subtract the inputs from its store, reserve the outputs, `CycleLabor = LaborRemaining = effective labor`, `SegmentStartedAt = t`,
`PhaseEndsAt = t + ceil(labor / w)`. A start touches only its own store, so one pass in ascending `Id` starts everything that can start.
**Completion rule** at `t == PhaseEndsAt`: `Producing` → the outputs move from reserved into the building's store, the currency into the
purse, then `Idle`. `Constructing` → `Idle` plus auto-staffing. `Upgrading` → `Level = TargetLevel`, then `Idle`.
**Delivery rule:** a walker that arrives with goods fills the building's waiting project first (§16), then its store up to its room; what
is left stays with a courier or goes back to a trip's source (§18).
**Ordering at the same `t`:** (1) research completion → (2) building completions in ascending `Id` → (3) trip arrivals in ascending trip
id → (4) courier arrivals in ascending (station `Id`, slot) → (5) starts: cycles in ascending `Id`, then couriers leaving their station in
ascending (station `Id`, slot). Each step is fully applied before the next one. This order is visible to players.

## 7. The algorithm
```
Advance(city, content, elapsedSeconds, absentFrom) -> SettlementReport
  T0 = city.Clock; target = T0 + max(elapsedSeconds, 0)
  t = T0
  loop:
    CompleteDue(city, t)     # research; buildings by Id; trip arrivals by id; courier arrivals by (station, slot)
    StartEligible(city, t)   # cycles by Id; couriers at their station by (station, slot)
    next = min(next due time of every event source)
    if next is none or next > target: break
    t = next
  city.Clock = target
```
`Advance(city, 0)` (a **zero-length advance**) runs `StartEligible` at the current clock (and completes an event due exactly there).
Every city request ends with one, after its mutation if it has one (`03`, request lifecycle step 7; the caller runs it, not the mutation), so
the state it stores and returns is canonical: another zero-length advance would change nothing.
The event sources (research timer, building timers, trip arrivals, courier arrivals, later the mailbox of §13) are a list in the order of §6;
`next` is the earliest of them. An event due at the clock completes first, in the order of §6 (a canonical city has none: every timer and
walk ends ≥ 1 s after it starts, but a restored city may). An event due before the clock means an invalid city: `Advance` throws before it
changes anything. Within one advance the map does not change (only mutations change it), so a walk's time between two buildings is computed
once per advance and kept (§17).

## 8. Mutations (all happen at `city.Clock`, after settling)
| Mutation | Checks (error code) | Effect |
|---|---|---|
| PlaceBuilding | type known (`UNKNOWN_BUILDING_TYPE`), of the city's era or earlier and unlocked (`BUILDING_LOCKED`), in bounds (`OUT_OF_BOUNDS`), tile free of buildings and roads (`TILE_OCCUPIED`), below the type's `maxPerCity` (`BUILDING_LIMIT`), free builder (`NO_FREE_BUILDER`), the cost's coins (`INSUFFICIENT_RESOURCE`) | pay the coins, `Id = NextBuildingId++`, `Constructing`; its project needs the cost's goods (§16); with none, the timer runs from now for the build seconds |
| UpgradeBuilding | exists (`BUILDING_NOT_FOUND`), Idle/Producing without a waiting upgrade (`BUILDING_BUSY`), level < max (`MAX_LEVEL`), free builder (`NO_FREE_BUILDER`), the next level's coins (`INSUFFICIENT_RESOURCE`) | pay the coins; with no goods in the upgrade cost the upgrade starts now (below); otherwise the building keeps working with an upgrade waiting for those goods (§16) |
| AssignWorkers | exists (`BUILDING_NOT_FOUND`), a producer or courier station (`INVALID_ARGUMENT`), not Constructing (`BUILDING_BUSY`), `0 ≤ n ≤ MaxWorkers` of the current level, also while upgrading (`WORKER_LIMIT`), the increase `n − assigned` ≤ free (`INSUFFICIENT_WORKERS`) | set `n`; a producer gets the labor segment update (§3); a station gains couriers at its station or loses its highest slots (§19) |
| StartResearch | known (`UNKNOWN_RESEARCH`), not done (`RESEARCH_COMPLETED`), slot free (`RESEARCH_IN_PROGRESS`), prerequisites + current era (`RESEARCH_LOCKED`), a completed science building (`NO_SCIENCE_BUILDING`), the coins (`INSUFFICIENT_RESOURCE`) | pay the coins, fill the slot; the goods are delivered to the science building, then the timer runs (§5) |
| PlaceRoads(tiles) | every tile in bounds (`OUT_OF_BOUNDS`), without a building or road (`TILE_OCCUPIED`), the coins of level 1 for every tile (`INSUFFICIENT_RESOURCE`) | pay, a level-1 road on every tile (§17) |
| UpgradeRoads(tiles) | every tile has a road (`ROAD_NOT_FOUND`) below the last road level (`MAX_LEVEL`), the coins of every next level (`INSUFFICIENT_RESOURCE`) | pay, every road one level up |
| RemoveRoads(tiles) | every tile has a road (`ROAD_NOT_FOUND`) | remove them; nothing is refunded |
| StartTrip(source, target, item, amount) | both buildings exist (`BUILDING_NOT_FOUND`), item known (`UNKNOWN_ITEM`) and not the currency (`INVALID_ARGUMENT`), amount allowed (§19: `INVALID_ARGUMENT` for an unknown preset, `AMOUNT_MODE_LOCKED`), the target accepts the item (`ITEM_NOT_ACCEPTED`: its waiting project needs it or its store holds it), a free worker (`INSUFFICIENT_WORKERS`), the amount gives at least 1 (`NOTHING_TO_CARRY`) | take the goods from the source's store, the worker walks (§18) |
| SetRoute(station, slot, stops) | station exists (`BUILDING_NOT_FOUND`) and is a courier station (`INVALID_ARGUMENT`), slot 1..MaxWorkers of its level, 0 or 2..`maxRouteStops` stops, 1..`maxStopActions` actions per stop, no two consecutive stops (last and first included) at one building (`INVALID_ROUTE`), every stop's building exists (`BUILDING_NOT_FOUND`), every item known (`UNKNOWN_ITEM`) and not the currency (`INVALID_ARGUMENT`), every amount allowed (as StartTrip) | replace the slot's route (0 stops clears it); the slot's courier, if any, returns its load and starts again from its station (§19) |
| ReenergizeCouriers | — | every courier's stamina is its station level's; resting couriers are at their station again, ready to leave (§19) |

**Starting an upgrade** (at once, or when its last good arrives): a running cycle (frozen or not) ends without output and its inputs go back
into the building's store, even above its limit; the building is `Upgrading` to `TargetLevel = Level + 1` for that level's upgrade seconds; a
courier station's couriers stop (§19). **Builders busy** = `Constructing` + `Upgrading` + upgrades waiting for goods.
Checks run in the listed order; the first failure is returned and changes nothing; a list of tiles or stops is applied all or nothing. An
applied mutation names what it paid and refunded (the ledger rows of `06`). "Current era" means the node's era equals the city's, and a
frozen cycle counts as running (its inputs are refunded). Workers on a house, a warehouse or the science building are `INVALID_ARGUMENT`.

## 9. Determinism rules (shared code)
- Integers only (`long` quantities, seconds, milliseconds and labor; `int` building instance ids, trip ids, slots and permille). Content
  ids are typed structs over text, compared ordinally, such as `ItemId` (decision 022 in `12`). No `float`, `double` or `decimal`.
- Game values are divided only through `IntegerMath`, on non-negative integers: durations round up (`CeilDiv`), labor reductions and walk
  bonuses use `ApplyPermilleReduction`, amounts round down (§19), display estimates round down (`FloorDiv`, §15). Index arithmetic (a
  midpoint, a heap parent) is not game logic.
- Never depend on `Dictionary`/`HashSet` enumeration order. Iterate sorted ids or arrays; a per-advance cache is read by key only.
- No `DateTime`, no randomness, no static mutable state, no I/O inside the engine.
- Use `checked` arithmetic. Overflow is a bug, not a wrap.

## 10. Invariants (each has a test)
1. Same input → structurally identical output and report.
2. **Split invariance:** for one `absentFrom`, advancing `a` then `b` equals advancing `a + b`, for any lengths.
3. **Timer invariance:** construction, upgrade and research completion times don't depend on how time is split, for any lengths.
4. Conservation per item: `final total = initial + produced − consumed − delivered + refunded − spent`, where a total counts every store,
   every load on a walker and, for the currency, the purse (§16).
5. No negative quantity in a store, a load, a project's needs or the purse.
6. Production and walkers never push a store's `stock + reserved` above its effective limit; only refunds and returned loads may.
7. The clock never decreases.
8. Nothing is produced before construction completes, and nothing during an upgrade.
9. `Σ assigned + workers on trips ≤ provided`, and `assigned ≤ MaxWorkers` for every building; a station has exactly one courier per worker.
10. Busy builders ≤ builder slots. At most one research is active.
11. Labor accounting: a cycle completes at the first second at which the worker-seconds put into it, over every worker change, reach its labor.
12. A walk crosses another building only when every path does, and then through as few buildings as possible (§17).
13. Locked buildings are never placed, research prerequisites are always respected, and a locked amount mode is never used.
14. No stamina is spent before `absentFrom`; a load never exceeds its carry limit.

## 11. Settlement report
Returned with every settled snapshot: `elapsedSeconds` (the simulated seconds, ≥ 0); `produced{item:qty}` (outputs of completed cycles, the
currency included); `consumed{item:qty}` (inputs of started cycles); `delivered{item:qty}` (goods walkers handed to projects, used up);
`moved{item:qty}` (goods walkers put into stores); `refunded{item:qty}` (inputs of cycles an upgrade ended, when its last good arrived during
the advance); `courierSeconds` (the most stamina one courier spent, 0 when none did); `tiredCouriers[{stationId, slot, at}]` (couriers
whose stamina ran out, at the second it did); `completedBuildings[id]` (constructions and upgrades), `completedResearch[id]`,
`eraChanged` (false until the era transition, M2). Ids ascending, items in ordinal order.
It is persisted as aggregated ledger entries (one per item and direction per settlement, `06`). On the server a settlement is the window's
advance plus the request's closing zero-length advance (§7), so what a command's mutation made startable counts in its report and its
ledger rows (`Settlement`, M0-23).

## 12. Performance budget
Worst case (CR-0008): an 8×8 city of 26 houses at level 2 (156 workers) and 38 courier stations at level 2 (152 couriers, 8 h stamina),
every route between two adjacent buildings, Sturdy Boots done (4 s walks): one settlement after a long absence covers the grace and the
stamina, about 7,300 arrivals per courier, ≈ 1.1 M events. Producers add less per tile than couriers (a 10 s cycle at best). Target:
< 500 ms on a dev machine in Release (CI uses a generous limit); CR-0008-T1 part I measures it and sets the final number (above it: the 005
fallback in `12`). The test and the measured numbers: `docs/modules/CityBuilder.Simulation.md` → Tests → `Performance/`.

## 13. Planned extension — external deliveries (not in M0)
Market fills, expired-order returns and other cross-player transfers will arrive through a **mailbox**
(`26-open-market.md`). The settle passes them in as timed events (`t = max(tradeTime, T0)`), processed
before building completions, into a destination store decided in M2 (`26` binding rule 4), ignoring its limit. Keep the event sources in
`Advance` a list, not hard-coded, so this is an additive change.

## 14. Content rules (checked when content loads)
Content becomes a `GameContent` only through `GameContentFactory`, and the engine relies on every rule below. The factory checks the shape
first (required values present, ids `[a-z][a-z0-9_]*` of at most 64 characters, known kinds and effect types, no value where none belongs,
quantities, durations, milliseconds and labor ≤ 10¹²; grid size and worker counts ≤ 32767, the `smallint` columns of `06`; `maxRouteStops`
2..16 and `maxStopActions` 1..8, the API's list limits), then every rule, and lists every error with a stable code (`docs/modules/CityBuilder.Simulation.md`).
1. Recipe and bonus amounts, labor, durations, store limits, tile times, carry limits, stamina, grid size and slots > 0; cost and starting
   amounts ≥ 0; an effect's permille 1..1000; `MaxLaborReductionPermille` 0..1000; exactly one research slot (§5).
2. Ids are unique per kind; era numbers are 1..n; every reference (era, item, building type, research node) exists.
3. Levels are 1..max. A house level provides ≥ 1 worker and nothing else; a producer level has ≥ 1 worker slot, a recipe with an output and
   a store; a courier station level has ≥ 1 worker slot, a carry limit and stamina; a warehouse level has a store; a science building level
   has none of these. Max workers, workers provided, carry limits and stamina never drop from one level to the next.
4. Exactly one item is the **currency** (`currency: true`). It is never a recipe input, in a store or a storage bonus, and never carried;
   it may be a recipe output and in costs. A storage bonus targets a good, a labor reduction a producer.
5. A building type that is not available at the start is unlocked by exactly one research node; one that is available, by none.
6. The research graph has no cycle, and every prerequisite is in the same era or an earlier one.
7. Σ labor reductions of one building type ≤ `MaxLaborReductionPermille`, and with all of them applied every level's shortest cycle,
   `CeilDiv(effective labor, MaxWorkers)`, is ≥ `MinCycleSeconds`.
8. Every item in a cost, a recipe, a store or the starting city belongs to the era of what uses it (building type, research node, starting
   city) or an earlier one (OD-10).
9. **Stores:** a producer level's store holds every good of its recipe with a limit ≥ that good's recipe quantity; a store's goods and
   limits never shrink from one level to the next.
10. Exactly one building type is the **science building** (kind `science`), with `maxPerCity` 1; `maxPerCity`, where given, is ≥ 1.
11. **Roads:** the ground time is 1..1 000 000 ms per tile (so a walk's sum can't overflow); road levels 1..n; tile times strictly fall from
    level to level and are below the ground time; every level has a cost (to place
    at level 1, to upgrade to it above), the currency only (CR-0008 D6). `crossingSpeedPermille` 1..1000.
12. **The shortest possible walk** (one step between adjacent buildings, every walk bonus applied, §17) takes ≥ `MinWalkSeconds`;
    Σ walk bonuses ≤ `MaxWalkBonusPermille` (≤ 900).
13. **Routes and amounts:** 1..8 amount presets with unique ids and a percentage 1..100; `tripCarryLimit` ≥ 1; an `UnlockAmountMode` names
    `percent` or `exact`.

## 15. Display helpers (not authority)
`shared/Simulation/Runtime/Display/` holds pure estimates for the player's screens. The server never calls them, and nothing they return
is sent or stored. All values are integers, rounded down: seconds, permille (0..1000), or rates in thousandths of an item per hour.
- `PerHour(city, content, building, workers)`: the inputs and outputs per hour at a staffing, from `CeilDiv(effective labor, workers)`; 0 with no workers.
- `NetPerHour(city, content, item)`: outputs minus inputs of the buildings able to run: with workers, and producing, or idle and
  able to start (no stall reason). Frozen cycles, houses, buildings without workers, constructing or upgrading ones, and stalled ones give 0.
- `SecondsUntilFull(city, content, building, item)`: seconds until the item fills that building's store at its own net rate; null when
  the store doesn't hold the item or the rate is ≤ 0 (CR-0008: per store, part E).
- `ProgressPermille(building, clock)`, `SecondsUntilNextEvent(building, clock)`, `ResearchProgressPermille(city, clock)`: the bar and the
  countdown of a job at a clock the caller chooses (the client runs ahead of its snapshot, `09`); the countdown is null when nothing is
  scheduled, also while a project waits for goods. Walk positions for the map (part D of CR-0008-T2) come from §17's path and the walk's times.

## 16. Stores, the purse and projects (CR-0008)
- **Stores.** Every item except the currency lives in a building's **store**. Each level of a building type lists the goods its store
  holds and a **limit** per good (`storage` in content): producers (their recipe's goods) and warehouses (every good of the era). Houses,
  courier stations and the science building have no store. A store exists from placement, also on a construction site and during an
  upgrade (the current level's limits). **Effective limit** = the level's limit + Σ `StorageCapBonus` of completed research for that good.
  **Room** for a good = effective limit − stock − reserved, where reserved is the building's own running cycle's output of that good.
- **The purse** holds the currency: in no building, uncapped. Every coin cost is paid from it at once; coins a cycle produces go into it.
  A **city total** of an item adds every store, every walker's load and, for the currency, the purse.
- **Projects.** A coin cost is paid when ordered; the goods of a cost are never taken from a store. They are **needs** the building waits
  for: a **construction site** (`Constructing` without a timer), an **upgrade waiting for goods** (the building keeps working and its
  builder slot is taken), and the **research** at the science building. When a delivery makes the needs empty at `t`, the project starts
  at `t`: the construction timer runs for the build seconds, the upgrade starts (§8), the research timer runs (§5).
- **Deliveries** (§6): a walker arriving at a building hands each carried good first to the building's project (the research for the
  science building), at most what it still needs, used up at once (`delivered` in the report); then into the store, at most its room
  (`moved`). Nothing else ever enters a store above its limit, except a refund (§8) and a returned load (§18, §19).
  A science building that also has a project of its own (a construction site, an upgrade waiting for goods) fills that project first,
  then the waiting research, then its store: one fixed order keeps the engine deterministic (CR-0008-T1 decision 37; M0's science
  building has one level and costs only coins, so the case needs later content).
- A new city has coins only (CR-0008 D2); the science building is one per city (§14 rule 10).

## 17. The map: tiles, roads and walks (CR-0008)
- A tile holds nothing, a building or a **road** (with a level). Roads are built, upgraded and removed by mutations (§8), never on a building.
- **Tile time** (milliseconds, content): a road tile its level's time; another building's tile `CeilDiv(ground × 1000, crossingSpeedPermille)`
  (25 s at 20 %, a **crossing**); every other tile, and the walk's own start and end buildings, the ground time (5 s).
- A **walk** steps from tile centre to tile centre in four directions (every turn 90°), inside the grid. A step costs half of the tile it
  leaves plus half of the tile it enters. Integer form: `D = Σ (leave + enter)` over the steps; the walk takes
  `max(1, CeilDiv(ApplyPermilleReduction(D, Σ WalkSpeedBonus‰), 2000))` seconds.
- **Path rule:** among all step sequences from start to end, a walk takes one with the fewest crossings, and among those the least `D`. So it
  avoids every building whenever a way around exists, and uses roads exactly when they make it faster, even with more tiles.
  **Tie rule:** among those, the first in step order, comparing steps one by one in the order x+1, y+1, x−1, y−1 (computed by a search from
  the end, then choosing from the start the first direction that stays on a best path). Every building can be reached.
- A walk keeps the time it started with; the map only changes between advances, and a change applies from the next walk.

## 18. Trips (CR-0008)
- `StartTrip` (§8) takes `q` = the amount (§19) of the source store's stock, at most `tripCarryLimit`, out of the store at once, and
  gives the trip the next trip id. A free worker walks it to the target by §17 (it counts as busy, §3); trips never tire.
- At arrival the goods are delivered (§16); what doesn't fit goes back into the source's store at that second, even above its limit (a
  **returned load**), and the worker is free again. A trip keeps its source, target, item, quantity, departure and arrival.

## 19. Couriers, routes and amounts (CR-0008)
- A **courier station**'s level sets `MaxWorkers` (couriers), `carryLimit` (all goods together) and `staminaSeconds`. Its workers are
  couriers in slots 1..assigned; adding workers adds couriers at the station with full stamina; removing them removes the highest slots.
- A **route** belongs to a slot: 2..`maxRouteStops` stops in order, each a building and 1..`maxStopActions` actions (take or put, a good,
  an amount). Two consecutive stops (the last and the first included) are at different buildings.
- **Courier states:** `AtStation` (ready, or without a route) · `Walking` to stop `i` · `Returning` home · `Resting` (no stamina). A courier
  `AtStation` with a route and stamina > 0, whose station is neither constructing nor upgrading, leaves in step (5) of §6 for stop 1. At an
  arrival at stop `i` it spends stamina (§2), runs the stop's actions in order, then walks to stop `i+1` (after the last: stop 1); if its
  stamina reached 0 it walks home instead (`Returning`), and on arrival rests, or stands `AtStation` if it was re-energized meanwhile.
- **Take:** from the building's store, the amount of its stock, at most the stock and the free carry room. **Put:** the amount of what the
  courier carries of that good, delivered by §16; the rest stays on the courier. A courier never waits: it does what it can and walks on.
- **Load:** every carried quantity remembers the building it came from. When a courier stops working (its worker is removed, its route is
  replaced, its station starts an upgrade), each quantity goes back into the store it came from at once, even above its limit (a returned
  load), and the courier stands `AtStation` (one without stamina, resting or walking home tired, rests). Otherwise a resting courier
  keeps its load.
- **Amounts:** a **preset** (content: All 100 %, Half 50 %, Quarter 25 %) is always allowed; **percent** (1..100) and **exact** (≥ 1) only
  after a research with `UnlockAmountMode` (else `AMOUNT_MODE_LOCKED`). Of a base `b` (the stock for a take or a trip, the carried quantity
  for a put): a percentage gives `max(1, b × p / 100 rounded down)` when `b ≥ 1`, else 0; an exact number gives itself; then the limits above apply.
