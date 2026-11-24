# Era I Content (M1) — items, buildings, recipes, research

> The design of the full Era I (M1-01, CR-0011). **Every number is a first pass for the balance simulator (M1-15)**, not a promise.
> M1-02 copied it into `server/content/era1-content.json` (content version `era1-1`); from then on the JSON is the source of truth and
> this file is updated in the same commit as the JSON (as `23` was for M0). `23` stays the record of M0 (`mvp-3`).
> The map, the Chronicle, merchant orders and decorations: `29-era1-chronicle.md`. Russian and English names: `32-era1-names.md`.
> Rules behind the numbers: `22`, `24` (small-integer ratios), `25` (effects, Era I durations 10 min–8 h).

## Global rules (changes from `23`)

| Rule | Value |
|---|---|
| Map | 18 × 14 tiles, terrain per tile (`29` → Map); the city starts with area A (10 × 10); areas B and C open by research |
| Builder slots / research slots | 2 / 1; +1 builder slot from `guild_of_masons` |
| Building levels | 1–3; level 3 needs a research (`MaxLevelBonus`), except the Town Hall (3 from the start) |
| Terrain needs | `woodcutter`: a forest tile beside it · `quarry`: a rock tile beside it · `farm`: stands on fertile soil (`22`, G43) |
| Reference prices (coins per unit, for orders, `29` → Orders) | logs 2 · planks 5 · stone 3 · cut_stone 9 · grain 2 · flour 5 · bread 8 |
| Demolish refund / cancel research refund | 500 ‰ of the coins paid / 500 ‰ of the coins paid (rounded down; G39, G41) |
| Everything else | as `23` → Global rules (walking, trips, routes, amount presets, minimum cycle, max labor reduction) |

## Items

| id | Era | Chain | Made by | Used by |
|---|---|---|---|---|
| `coins` | 1 | — | Trading Post, orders, goal rewards | everything |
| `logs` | 1 | wood | Woodcutter's Hut | Sawmill, early costs |
| `planks` | 1 | wood | Sawmill | Trading Post recipe, most costs |
| `stone` | 1 | stone | Quarry | Stonemason, a few costs |
| `cut_stone` | 1 | stone | Stonemason | level 3 costs, late research, Town Hall |
| `grain` | 1 | bread | Farm | Windmill, `milling` |
| `flour` | 1 | bread | Windmill | Bakery |
| `bread` | 1 | bread | Bakery | Town Hall, late research, orders |

## Buildings and levels
Cost and build time per level (L1 = construction, L2 and L3 = upgrades). Workers = max workers; for a Cottage and the Town Hall, the
workers it provides. Store = the limit per item at L1 / L2 / L3.

| id | Kind | Unlocked by | L1 | L2 | L3 (needs) | Workers | Store |
|---|---|---|---|---|---|---|---|
| `house` (Cottage) | house | start | 50c · 30 s | 120c + 30 logs · 90 s | 300c + 40 planks + 20 cut_stone · 300 s (`timber_framing`) | 4 / 6 / 9 | — |
| `woodcutter` | producer | start | 80c · 60 s | 150c + 20 planks · 120 s | 350c + 40 planks + 10 cut_stone · 300 s (`timber_framing`) | 2 / 4 / 5 | logs 40 / 80 / 120 |
| `sawmill` | producer | start | 150c + 10 logs · 120 s | 250c + 30 planks · 180 s | 500c + 40 planks + 20 cut_stone · 420 s (`timber_framing`) | 3 / 6 / 9 | logs 40/80/120, planks 20/40/60 |
| `quarry` | producer | `stone_cutting` | 120c + 20 planks · 90 s | 250c + 30 planks · 180 s | 450c + 30 planks + 20 cut_stone · 360 s (`masonry`) | 2 / 4 / 6 | stone 40 / 80 / 120 |
| `stonemason` | producer | `stone_cutting` | 200c + 20 planks + 20 stone · 150 s | 350c + 40 planks · 240 s | 600c + 40 planks + 30 cut_stone · 480 s (`masonry`) | 3 / 6 / 9 | stone 40/80/120, cut_stone 20/40/60 |
| `farm` | producer | `open_fields` | 150c + 20 planks · 90 s | 300c + 30 planks + 10 cut_stone · 180 s | 500c + 40 planks + 20 cut_stone · 360 s (`timber_framing`) | 2 / 4 / 6 | grain 40 / 80 / 120 |
| `windmill` | producer | `milling` | 300c + 30 planks + 20 cut_stone · 240 s | 450c + 40 planks + 20 cut_stone · 300 s | 700c + 50 planks + 40 cut_stone · 600 s (`masonry`) | 3 / 6 / 9 | grain 40/80/120, flour 20/40/60 |
| `bakery` | producer | `milling` | 250c + 20 planks + 20 cut_stone · 180 s | 400c + 30 planks + 20 cut_stone · 240 s | 650c + 40 planks + 30 cut_stone · 480 s (`masonry`) | 2 / 4 / 6 | flour 40/80/120, bread 20/40/60 |
| `trading_post` | producer | `trade_charter` | 200c + 5 planks · 180 s | 400c + 40 planks · 300 s | 700c + 50 planks + 30 cut_stone · 600 s (`masonry`) | 2 / 4 / 6 | every good 40 / 80 / 120 (orders are filled from it) |
| `courier_station` | courierStation | start | 120c · 60 s | 250c + 30 planks · 180 s | 500c + 40 planks + 30 cut_stone · 420 s (`masonry`) | couriers 2 / 4 / 6 | carry 10 / 15 / 20 · stamina 4 h / 8 h / 12 h |
| `warehouse` | warehouse | start | 100c + 20 logs · 60 s | 200c + 30 planks · 120 s | 450c + 40 planks + 30 cut_stone · 360 s (`masonry`) | — | every good 100 / 200 / 400 |
| `scriptorium` | science | start | 100c · 30 s | — | — | — | research goods (as M0) |
| `town_hall` (landmark) | house | `masonry` | 600c + 60 planks + 40 cut_stone · 600 s | 900c + 80 planks + 60 cut_stone + 40 bread · 900 s | 1500c + 100 planks + 100 cut_stone + 80 bread · 1800 s (none) | 4 / 8 / 12 | — |

One per city: `scriptorium`, `town_hall`. The Town Hall can't be demolished (G41); it is built in stages by its three levels.
M0 numbers (L1, L2 of the first seven rows) are `mvp-3` unchanged.
The Woodcutter's Hut has 5 workers at L3, not 6: with `sharp_axes` a cycle of 6 would take 8 s, below the minimum cycle (`04` §14 rule 7).

## Recipes (the same at every level; a level adds worker slots)

| Building | Inputs → outputs | Labor per cycle | At L1, full staffing |
|---|---|---|---|
| Woodcutter's Hut | — → 1 logs | 60 worker-s | 2 workers: 30 s, 120 logs/h |
| Sawmill | 2 logs → 1 planks | 180 worker-s | 3 workers: 60 s, 60 planks/h (eats 120 logs/h) |
| Quarry | — → 1 stone | 90 worker-s | 2 workers: 45 s, 80 stone/h |
| Stonemason | 2 stone → 1 cut_stone | 270 worker-s | 3 workers: 90 s, 40 cut_stone/h (eats 80 stone/h) |
| Farm | — → 1 grain | 60 worker-s | 2 workers: 30 s, 120 grain/h |
| Windmill | 2 grain → 1 flour | 180 worker-s | 3 workers: 60 s, 60 flour/h (eats 120 grain/h) |
| Bakery | 1 flour → 1 bread | 120 worker-s | 2 workers: 60 s, 60 bread/h (eats 60 flour/h) |
| Trading Post | 2 planks → 10 coins | 120 worker-s | 2 workers: 60 s, 600 coins/h (eats 120 planks/h) |

Every chain is 1 : 1 at level 1: one full raw producer feeds one full processor. Stone, flour and bread are sold only through merchant
orders and the caravan; they are needed for level 3, the Town Hall and late research (`24` → Orders).

## Roads

| Kind / level | Time per tile | Cost per tile | Where |
|---|---|---|---|
| Dirt path (1) | 2 500 ms | 5 coins | grass, fertile soil |
| Log road (2) | 1 500 ms | 15 coins (upgrade) | grass, fertile soil |
| Bridge (`bridge_building`) | 1 500 ms | 60 coins | water only |

## Research (16 nodes, four branches, one slot, at the Scriptorium)

| Branch | id | Cost | Time | Requires | Effects |
|---|---|---|---|---|---|
| Trade | `trade_charter` | 150c | 10 min | — | unlock `trading_post` |
| Trade | `honest_scales` | 600c + 60 planks | 2 h | `trade_charter` | Trading Post labor −200 ‰ |
| Trade | `town_charter` | 3000c + 100 planks + 150 cut_stone + 150 bread | 8 h | every other node | `GrantCharter`: Era I complete (`25`) |
| Production | `sharp_axes` | 250c | 30 min | — | Woodcutter labor −200 ‰ |
| Production | `frame_saw` | 400c + 30 planks | 1 h | `sharp_axes` | Sawmill labor −200 ‰ |
| Production | `stone_cutting` | 400c + 40 planks | 45 min | `trade_charter` | unlock `quarry`, `stonemason` |
| Production | `open_fields` | 800c + 60 planks + 30 cut_stone | 3 h | `bridge_building` | unlock `farm`; land: area C (the east bank) |
| Production | `milling` | 1000c + 60 planks + 60 cut_stone + 50 grain | 4 h | `open_fields` | unlock `windmill`, `bakery` |
| Construction | `storehouses` | 300c + 50 logs | 45 min | `trade_charter` | +40 of every good in every store that holds it |
| Construction | `timber_framing` | 600c + 80 planks | 2 h | `frame_saw` | max level +1: Cottage, Woodcutter's Hut, Sawmill, Farm |
| Construction | `masonry` | 1200c + 100 planks + 80 cut_stone | 5 h | `stone_cutting` | max level +1: Quarry, Stonemason, Windmill, Bakery, Trading Post, Courier Station, Warehouse; unlock `town_hall` |
| Construction | `guild_of_masons` | 1500c + 120 cut_stone + 60 bread | 6 h | `masonry` | +1 builder slot |
| Logistics | `tally_sticks` | 150c | 15 min | `trade_charter` | any percentage in trips and courier actions |
| Logistics | `sturdy_boots` | 200c + 20 logs | 20 min | — | walks −200 ‰ |
| Logistics | `bridge_building` | 500c + 60 planks + 40 cut_stone | 2 h | `stone_cutting` | unlock the bridge road kind |
| Logistics | `land_survey` | 700c + 40 planks + 40 stone | 3 h | `sturdy_boots` | land: area B (the north, west and south rim) |

Total research time about 39 h; the longest node (8 h) is under ¼ of five days (`25`). This is below the "research ≈ 60–70 % of the
era" rule of `24`: with one slot and 3–5 check-ins a day, the rest of the week is gated by goods and builders. M1-15 checks the pacing.
A node may have several effects (`masonry`, `open_fields`); `04` gets that rule in M1-02 if it doesn't hold today.
As built (M1-03): "land: area B / C" is an `openLand` effect with that `area`, "unlock the bridge road kind" an `unlockBridge`
effect; the bridge is the content's `bridge`; the raw producers' terrain is each type's `site` (`nearForest`, `nearRock`, `fertileSoil`).

## Starting city
purse 1000 coins · era 1 · area A of the map · no buildings, no goods, no research · the Chronicle at chapter 1 · default town name
(`32`) · city clock 0.

## Golden scenario #3 — the bread chain with couriers (hand-checked; re-check whenever content changes)
Content `content-golden-v3.json` (`era1-1`, frozen). It starts from a built city at t=0, since bread needs hours of research: done
`trade_charter`, `stone_cutting`, `bridge_building`, `open_fields`, `milling`; purse 1000; idle level-1 buildings without workers: Cottages
1–3 (0,7) (1,7) (2,7), Farm 4 (0,0), Windmill 5 (2,0), Bakery 6 (4,0), Courier Station 7 (2,1), Scriptorium 8 (7,7). No roads. Walks: station
→ Farm 15 s, station → Windmill 5 s, Farm ↔ Windmill and Windmill ↔ Bakery 10 s. Actions: `t=0` workers Farm 2, Windmill 3, Bakery 2,
station 2; route courier 1: Farm `take grain all`, Windmill `put grain all`; route courier 2: Windmill `take flour all`, Bakery `put flour
all` · the player looks every 300 s up to 1500, so is never absent (stamina stays 14 400). Cycles: Farm 30 s, Windmill 60 s, Bakery 60 s.

| t (s) | What happens | Farm grain | Windmill grain · flour | Bakery flour · bread | courier 1 · 2 carry |
|---|---|---|---|---|---|
| 0 | workers, routes; Farm starts (to 30); courier 1 leaves (Farm at 15), courier 2 (Windmill at 5) | 0 | 0 · 0 | 0 · 0 | 0 · 0 |
| 30 | first grain (courier 1 found none at 15) | 1 | 0 · 0 | 0 · 0 | 0 · 0 |
| 35 | courier 1 takes it (Windmill at 45) | 0 | 0 · 0 | 0 · 0 | 1 · 0 |
| 45 | grain 1 at the Windmill | 0 | 1 · 0 | 0 · 0 | 0 · 0 |
| 85 | the second grain: the Windmill starts (to 145) | 0 | 0 · 0 | 0 · 0 | 0 · 0 |
| 145 | first flour; two grain in: it starts again (to 205); courier 2 takes the flour | 0 | 0 · 0 | 0 · 0 | 0 · 1 |
| 155 | the Bakery gets it and starts (to 215) | 0 | 0 · 0 | 0 · 0 | 1 · 0 |
| 215 | first bread; flour in: it starts again (to 275) | 0 | 0 · 0 | 0 · 1 | 1 · 0 |
| 300 | look; steady: grain every 30 s, flour and bread every 60 s | 1 | 1 · 0 | 0 · 2 | 0 · 0 |
| 600 | look | 1 | 1 · 0 | 0 · 7 | 0 · 0 |
| 1355 | 20th bread: the store is full, `StorageFull:bread`; the flour waits | 0 | 0 · 0 | 1 · 20 | 1 · 0 |
| 1500 | look | 1 | 1 · 0 | 3 · 20 | 0 · 0 |

Check at 1500: grain made 50 = 1 + 1 + 48 milled (24 cycles); flour 23 = 20 baked + 3 waiting (the running cycle's flour is reserved);
bread 20; coins 1000. Courier 1 is at the Farm at 15 + 20k and takes the grain of 30k on its next visit; courier 2 is at the Windmill at
5 + 20k, the same second each flour is made.
