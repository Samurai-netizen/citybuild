# Local logistics run (CR-0008)

A play of the logistics rules with the real client and server, at 60 times the speed: a delivery by hand, goods for a construction site
and for a research, a courier route, a road, pausing, an absence and re-energizing. Run it after a change to stores, deliveries, couriers,
routes, roads or pausing. It takes about 30 minutes. What you need and how to start are in `LOCAL-E2E.md`; the rules are in
`core-instructions/04-simulation-model.md` §18 and §19, the numbers in `core-instructions/23-mvp-content.md`.

At this speed a city minute is a real second: a Cottage takes half a second, a ground step 5 city seconds (a twelfth of a real second), and a
courier's 4 h of stamina 4 real minutes.

## The steps
**1. A fresh city at the fast clock.** Stop the game and the API, then (this deletes every city on your development database):
```
docker compose down -v
docker compose up -d postgres
dotnet run --project server/src/CityBuilder.Api -- --Simulation:DevTimeScale=60
```
Press **Play** and keep the Game view in front. The HUD shows Coins 800 and no goods: every good lives in a building's store.

**2. Goods for a construction site, by hand.**
1. Build a **Cottage** and a **Woodcutter's Hut** side by side. The Woodcutter gets its two workers by itself and its store fills (`0/40`
   in its menu, one log every 30 city seconds).
2. Build a **Sawmill** two tiles away. Coins drop at once; its site says it waits for 10 Logs and does not build yet.
3. When the Woodcutter has 10 logs, open it and press **Send trip**: pick the Sawmill as the target and **All**. The path shows on the
   map, the bar names the carrier (`Worker`, no courier station yet) and the loads. A worker walks with logs on its back, gives them to
   the site and walks back; the site starts building.

**3. A research with goods.** Build a **Scriptorium** and start **Sturdy Boots** (it needs 20 Logs). The research ring waits for the
logs; send them from the Woodcutter like in step 2 (two loads of 10 when you send 20). The ring starts once all 20 arrived.

**4. A courier station and its deliveries.**
1. Build a **Courier Station** and give it two workers with its stepper: these are its couriers. Its menu lists them, their stamina and
   what they do.
2. Send a trip again: the bar now names `Courier 1 of Courier Station`, and the station's menu lists the delivery's lines (at most 10 per
   line) with **Cancel** and **Cancel all**. Cancel one line: the courier gives back what it carries and goes on with the next.

**5. A route and a road.**
1. Build a **Warehouse** (it needs 20 Logs: deliver them). With the Scriptorium standing, the station's **Edit routes** is on.
2. In the editor (a side panel in landscape, a sheet in portrait; the map stays tappable), tap the Woodcutter (`Take Logs All`) and the
   Warehouse (`Put Logs All`) and press **Save** on a courier without deliveries. It walks the loop on its own.
3. In **Build**, press **Roads** and drag a line of dirt paths between the two buildings: the picked tiles show the see-through road and
   the cost. Place them: the courier's walk gets shorter. Upgrade one tile to a log road, remove another.

**6. Pause.**
1. Open the Woodcutter and press **Pause**: the note says `Stops after this batch`; the cycle ends, the grey `Paused` bubble shows and the
   store stays open (a courier still takes from it). **Resume** starts it again.
2. Pause the Courier Station: each courier finishes its stop, gives back its load and walks home; the note counts down `Couriers home in`
   to `All couriers are home`. Upgrade the station now: its routes are kept. Resume it when it is built: the couriers start from stop 1.
3. Let the Warehouse or the Woodcutter fill above three quarters: the ring above it becomes the amber, segmented store gauge with the
   good's icon (red when full).

**7. An absence and re-energize.** Click another program and leave Unity in the background for at least 10 minutes. The presence grace is
real time (`PresencePolicy`: `Simulation:PresenceGraceSeconds` times the scale), so couriers start to spend stamina 5 real minutes after
your last request and are tired 4 real minutes later. Click the Game view: the welcome-back report lists Moved, Delivered and Couriers, says which courier rested since when, and offers
**Re-energize couriers**. The HUD says `1 courier resting` (or more). Press it: the couriers walk again.

**8. Back to normal.** Stop the game and the API, then reset the database (`docker compose down -v`, `docker compose up -d postgres`):
a city does not survive a change of the time scale.

## What to report
Anything that looks wrong, with the step, the screen size and what you saw: goods that appear or vanish (the HUD total changes only by
production, use and coins), a walker that jumps or walks through a building it need not cross, a delivery that never ends, a paused
building that starts a cycle, a button that stays off without a note. `PredictionWatch:` warnings in the Console (`LOCAL-E2E.md` → the
waiting loop) say the display prediction and the server disagree.
