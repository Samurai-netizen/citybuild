# Game Vision — what the finished game looks and feels like

## One line
A city that grows from a muddy medieval village into a space-age metropolis through **research and planning, not tapping**.
You staff production chains with your own people, trade with real players, and come back to a city that has visibly moved forward in time.

## Player fantasy
"I'm the planner and the merchant." The satisfaction comes from three things:
- a chain that hums: logs into planks into coins into the next invention;
- couriers walking the roads you laid out, carrying each good to where it is needed;
- the small puzzles of where to put the next worker and the next road;
- the big moment when the city crosses into a new era.

## Session shape

| Session | Length | What happens |
|---|---|---|
| Check-in | 1–3 min, several times a day | Welcome-back report → re-energize the couriers → reassign workers → send goods where they are short → queue a build, upgrade or research → leave |
| Planning | 10–20 min, a few times a week | Choose the research path, lay out new land and roads, set courier routes, rebalance chains, (Era II+) place market orders |
| Milestone | Rare | Era transition: a ceremony, a new look, new mechanics with a short guided intro |

Waiting is the mechanic, so it must be **legible**. The player always knows what is being made, how fast,
what is blocked, what is waiting for goods, where the goods are, which research is running, and when the next interesting thing happens.

## The look
- **Camera:** angled 3/4 perspective over a grid plot; pinch-zoom, drag-pan, fixed tilt.
- **Art:** chunky low-poly "toy block" buildings, soft lighting, warm palette. Original designs only.
- **Eras change the city's look.** Each era has its own building set, materials and palette:
  timber and thatch (I) → stone and banners (II) → tiled roofs and domes (III) → brick and chimneys (IV)
  → concrete and power lines (V) → glass, steel and launch towers (VI). Old buildings keep their era's look.
  A mixed skyline tells the city's history.
- **Visible production:** stockpiles beside buildings grow and shrink, chimneys smoke while producing,
  and tiny workers walk in and out (their number matches the workers assigned).
- **Walkers are the logistics, not decoration.** Couriers and workers on a trip walk the real paths between buildings, tile centre to
  tile centre with 90° turns, faster on roads and slowly across a building, with their load shown; resting couriers stand at their station.
- **Status at a glance:** a progress ring under each building, and bubbles for no workers, missing input
  (item icon), storage full, waiting for goods, construction and upgrade. Stalled buildings desaturate slightly.
- **Day/night and weather** are cosmetic and follow the device's local time.

## Key screens
1. **City view.** The map is the main interface.
2. **HUD.** Item chips with the city's total of each item (all stores) and its **net rate per hour**; coins; workers free / total;
   builders busy / slots; research progress ring; era badge; a notice while couriers rest.
3. **Building inspector.** Recipe card ("2 Logs → 1 Plank · 60/h at 3 workers"), its **store** (each item as amount / limit), a
   **worker stepper (− / +)** with a live rate preview, status and reason, what a construction or upgrade still needs, a **send goods**
   button (a trip), and an upgrade preview ("max workers 3 → 6"). A Courier Station's inspector shows its couriers, their stamina and
   routes, and a re-energize button.
4. **Build menu.** Cards with cost, build time, max workers and recipe. Locked cards say which research unlocks them.
5. **Research tree.** Era tabs, branches, nodes with cost, time and effect; one tap to finance a node; the active node shows progress,
   or the goods it still waits for at the science building.
6. **Welcome back.** "While you were away (7 h 12 m)": how long the couriers worked and when they got tired, goods moved and delivered,
   produced and consumed per item, coins earned, constructions and research completed; its main button **Re-energize couriers**.
7. **Route editor.** A courier's stops in order, tapped on the map; per stop its take and put actions (item and amount); the path and
   each walk's time drawn on the map. **Road mode** in the build menu: drag a line of road tiles, see the cost, confirm.
8. **Era transition.** A full-screen moment: before/after skyline, the new mechanics, and the first new research.
9. **Market (Era II).** Items, order book, my orders, price history (`26`).
10. **Guild (Era II).** Members, projects, contributions.

## Tone
Friendly, lightly humorous names and descriptions with a light historical flavour, never snarky toward the player.
No dark patterns: no fake urgency, no punishing timers, and no loss of progress for being away (only a cap on production: couriers rest
when their stamina is spent, and production stops when stores fill or inputs run out). Re-energizing is one free tap, never a countdown or a nag.

## What "done" looks like for 1.0
Eras I–III fully playable (about 25 items, about 30 buildings, 45–55 research nodes), open market and guilds,
population needs and expeditions, land expansion, notifications, and fair monetization. Eras IV–VI ship as major updates.
