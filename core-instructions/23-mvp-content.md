# MVP Content (M0) — exact numbers

> These are the design numbers for the 14-day MVP: a small slice of **Era I (Early Middle Ages)**.
> M0-07b copied them into `server/content/mvp-content.json` (content version `mvp-1`), and **from then on the JSON is the
> source of truth**. Update this file in the same commit whenever the JSON changes (`docs/how-to/add-building-or-item.md`).
> Every number is a placeholder until the balance simulator (Day 12) validates it.
> M0 research times are short so the slice can be played in one sitting. Real Era I research takes 10 min–4 h (`25`).
> **CR-0008 (logistics)** sets the numbers below for content version `mvp-2` (`04` §16–§19). The JSON is `mvp-2` from CR-0008-T1 part C
> with the walking rules and road levels; the rest arrives in part E (until then its items, buildings, research and starting city are the
> M0 numbers, git history of this file), and the 8 h cap goes in part H.

## Global rules

| Rule | Value |
|---|---|
| Era at start | 1 — `early_medieval` |
| Grid | 8 × 8 tiles, every building occupies 1 tile |
| Builder slots / research slots | 2 / 1 |
| Currency | `coins`: the purse, uncapped, `long`; every other item lives in building stores (per-building limits below) |
| Offline production cap | none (courier stamina, `04` §2; the JSON keeps `maxOfflineProductionSeconds` 28 800 until CR-0008-T1 part H) |
| Minimum cycle | 10 s with every research applied (content validation) |
| Max total labor reduction per building type | 500 ‰ |
| Walking | ground 5 000 ms per tile; crossing another building at 200 ‰ of the speed (25 000 ms per tile) |
| Shortest walk / max walk bonus | at least 2 s with every bonus (content validation) / 500 ‰ |
| Trip carry limit | 10 (all goods together) |
| Routes | at most 8 stops, at most 4 actions per stop |
| Amount presets | `all` 100 % · `half` 50 % · `quarter` 25 % |
| Content version | `mvp-2` |

## Items

| id | Name | Era | Currency | Tradable (future market) |
|---|---|---|---|---|
| `coins` | Coins | 1 | yes | no (it's the currency) |
| `logs` | Logs | 1 | no | yes |
| `planks` | Planks | 1 | no | yes |

## Buildings

| id | Name | Kind | Unlocked by | Cost | Build time | Max level | L1 / L2 |
|---|---|---|---|---|---|---|---|
| `house` | Cottage | house | start | 50 coins | 30 s | 2 | provides 4 / 6 workers |
| `woodcutter` | Woodcutter's Hut | producer | start | 80 coins | 60 s | 2 | max workers 2 / 4 · store logs 40 / 80 |
| `sawmill` | Sawmill | producer | start | 150 coins + 10 logs | 120 s | 2 | max workers 3 / 6 · store logs 40, planks 20 / logs 80, planks 40 |
| `trading_post` | Trading Post | producer | research `trade_charter` | 200 coins + 10 planks | 180 s | 2 | max workers 2 / 4 · store planks 40 / 80 |
| `scriptorium` | Scriptorium (the science building, one per city) | science | start | 100 coins | 30 s | 1 | research happens here |
| `courier_station` | Courier Station | courierStation | start | 120 coins | 60 s | 2 | 2 / 4 couriers · carry 10 / 15 · stamina 14 400 s (4 h) / 28 800 s (8 h) |
| `warehouse` | Warehouse | warehouse | start | 100 coins + 20 logs | 60 s | 2 | store logs 100, planks 100 / logs 200, planks 200 |

## Recipes (the same at both levels; a level adds worker slots)

| Building | Inputs → Outputs | Labor per cycle | Examples |
|---|---|---|---|
| Woodcutter's Hut | — → 1 logs | 60 worker-s | 1 worker: 60 s (60/h) · 2: 30 s (120/h) · 4: 15 s (240/h) |
| Sawmill | 2 logs → 1 planks | 180 worker-s | 3 workers: 60 s (60/h) · 4: 45 s · 6: 30 s (120/h) |
| Trading Post (NPC merchants) | 2 planks → 10 coins | 120 worker-s | 1 worker: 120 s (300 coins/h) · 2: 60 s (600/h) · 4: 30 s |

These ratios are intentional. At level 1, one full Woodcutter's Hut feeds one Sawmill, and one Trading Post
needs two Sawmills. The Trading Post is the only source of coins (the NPC "faucet"); its coins go straight to the purse.

## Upgrades (level 1 → 2)

| Building | Cost | Time |
|---|---|---|
| Cottage | 120 coins + 30 logs | 90 s |
| Woodcutter's Hut | 150 coins + 20 planks | 120 s |
| Sawmill | 250 coins + 30 planks | 180 s |
| Trading Post | 400 coins + 40 planks | 300 s |
| Courier Station | 250 coins + 30 planks | 180 s |
| Warehouse | 200 coins + 30 planks | 120 s |

## Roads

| Level | Name | Time per tile | Cost per tile |
|---|---|---|---|
| 1 | Dirt path | 2 500 ms | 5 coins (to place) |
| 2 | Log road | 1 500 ms | 15 coins (to upgrade) |

## Research (Era I subset, one slot, at the Scriptorium)

| id | Name | Cost | Time | Requires | Effect |
|---|---|---|---|---|---|
| `trade_charter` | Trade Charter | 150 coins | 600 s | — | unlocks Trading Post |
| `sharp_axes` | Sharpened Axes | 250 coins | 1 800 s | — | Woodcutter labor −200 ‰ (60 → 48) |
| `frame_saw` | Frame Saw | 400 coins + 30 planks | 3 600 s | `sharp_axes` | Sawmill labor −200 ‰ (180 → 144) |
| `storehouses` | Storehouses | 300 coins + 50 logs | 2 700 s | `trade_charter` | +40 logs and +40 planks in every store that holds them |
| `tally_sticks` | Tally Sticks | 150 coins | 900 s | `trade_charter` | any percentage in trips and courier actions |
| `sturdy_boots` | Sturdy Boots | 200 coins + 20 logs | 1 200 s | — | walks −200 ‰ (5 s ground steps take 4 s) |

With both labor researches, a full L1 Woodcutter (24 s/log, 150/h) feeds a full L1 Sawmill (48 s, 150 logs/h) exactly.
A research's goods are delivered to the Scriptorium; its timer runs once they are all in. Completing all six doesn't change the era in M0.

## Starting city (created by the server for every new player)
purse 800 coins · era 1 · no buildings, no goods · no research · city clock 0.
The first session (golden #1): Cottage and Woodcutter first, the Scriptorium and Trade Charter, the Sawmill fed by hand trips; the first
coins come at about 20–23 minutes (`24` goal 1), measured again by the balance simulator (M0-67, M0-68).

## Golden scenario #1 — first session (hand-checked; re-check whenever content changes)
Content `content-golden-v2.json` (a frozen copy of the numbers above). Ids and tiles: Cottage 1 (0,2), Woodcutter 2 (0,0),
Scriptorium 3 (4,0), Sawmill 4 (2,0), Cottage 5 (1,2), Trading Post 6 (2,2). Walks: Woodcutter ↔ Sawmill and Sawmill ↔ Trading Post are two
ground steps (10 s). Actions: `t=0` place Cottage 1 and Woodcutter 2 · `t=30` place Scriptorium 3 · `t=60` start `trade_charter`, place
Sawmill 4 and Cottage 5 · `t=360` trip 2 → 4 logs `all` · `t=490` trip 2 → 4 logs `all` · `t=660` place Trading Post 6, trip 4 → 6 planks
`all`, trip 2 → 4 logs `half`. No other actions.

| t (s) | What happens | purse | free workers | Woodcutter logs | Sawmill logs / planks | waiting for goods |
|---|---|---|---|---|---|---|
| 0 | after actions | 670 | 0 | 0 | — | — |
| 30 | Cottage 1 done (4 workers); Scriptorium placed | 570 | 4 | 0 | — | — |
| 60 | Woodcutter done, staffed with 2, starts; Scriptorium done; `trade_charter` runs to 660; Sawmill site; Cottage 5 placed | 220 | 2 | 0 | 0 / 0 | Sawmill: 10 logs |
| 90 | first log; Cottage 5 done (8 workers) | 220 | 6 | 1 | 0 / 0 | Sawmill: 10 logs |
| 360 | 10th log; trip 1 takes 10 logs (arrives 370) | 220 | 5 | 0 | 0 / 0 | Sawmill: 10 logs |
| 370 | trip 1 delivers 10 logs to the site; Sawmill builds until 490 | 220 | 6 | 0 | 0 / 0 | — |
| 490 | Sawmill done, staffed with 3, `MissingInput:logs`; trip 2 takes 4 logs (arrives 500) | 220 | 2 | 0 | 0 / 0 | — |
| 500 | trip 2 puts 4 logs into the Sawmill's store; it starts (2 logs) | 220 | 3 | 0 | 2 / 0 | — |
| 560 | first plank; it starts again | 220 | 3 | 2 | 0 / 1 | — |
| 620 | second plank; `MissingInput:logs` | 220 | 3 | 4 | 0 / 2 | — |
| 660 | `trade_charter` completes; Trading Post site; trip 3 takes 2 planks, trip 4 takes half of 6 logs = 3 (both arrive 670) | 20 | 1 | 3 | 0 / 0 | Trading Post: 10 planks |
| 670 | trip 3 delivers 2 planks to the site; trip 4 puts 3 logs into the Sawmill; it starts | 20 | 3 | 3 | 1 / 0 | Trading Post: 8 planks |
| 730 | third plank; `MissingInput:logs` | 20 | 3 | 5 | 1 / 1 | Trading Post: 8 planks |

Check: logs produced 22 (90 … 720) = 10 delivered + 6 consumed + 5 + 1 in stores; planks 3 produced = 2 delivered + 1; coins
800 − 780 spent = 20. The Woodcutter fills one log every 30 s; the Sawmill waits for hand trips: the reason to build a Courier Station.

## Golden scenario #2 — couriers, a road, an absence (hand-checked; re-check whenever content changes)
Content `content-golden-v2.json`. Ids and tiles: Cottage 1 (0,3), Woodcutter 2 (0,0), Courier Station 3 (0,1), Warehouse 4 (3,0); dirt
paths on (1,0) and (2,0). Walks: station → Woodcutter 1 step (5 s); Woodcutter ↔ Warehouse along the road (10 s: steps of 3 750 + 2 500 + 3 750
ms; without the road 15 s); Warehouse → station 15 s along the road and round the Woodcutter (`04` §17: never through it).
Actions (each a request; the player is absent from 300 s after the last one, `04` §2): `t=0` place Cottage 1 and Woodcutter 2 · `t=30`
place Courier Station 3 · `t=60` place Warehouse 4, place roads (1,0) and (2,0) · `t=90` route for courier 1: Woodcutter `take logs all`,
Warehouse `put logs all` · `t=745` and `t=25000` the player looks (`GET /city`) · `t=25000` re-energize · `t=25005` the player looks.

| t (s) | What happens | purse | Woodcutter logs | Warehouse | courier 1 (stamina) |
|---|---|---|---|---|---|
| 60 | Woodcutter done (2 workers), starts; Warehouse site; roads | 440 | 0 | needs 20 logs | — |
| 90 | first log; station done, 2 couriers; route set; courier 1 leaves (to the Woodcutter at 95) | 440 | 1 | needs 20 logs | walking (14 400) |
| 95 | courier 1 takes 1 log, walks to the Warehouse (105) | 440 | 0 | needs 20 logs | carries 1 |
| 105 | delivers 1 log to the site | 440 | 0 | needs 19 logs | carries 0 |
| 685 | the 20th log arrives: the Warehouse builds until 745 | 440 | 0 | builds | — |
| 745 | the player looks: Warehouse done; store 2 logs (from 705 and 745); courier 1 spent 355 s (absent from 390) | 440 | 0 | 2 logs | 14 045 |
| 3685 | the Warehouse store is full (100) | 440 | 0 | 100 logs | carries 0 |
| 3975 | courier 1 carries 10 (full): it can neither put nor take | 440 | 0 | 100 logs | carries 10 |
| 5160 | the Woodcutter's store is full (40): `StorageFull:logs` | 440 | 40 | 100 logs | carries 10 |
| 15095 | courier 1 is tired (absent from 1045; 14 045 s spent) at the Woodcutter; walks home | 440 | 40 | 100 logs | 0 |
| 15100 | courier 1 rests at the station with its 10 logs | 440 | 40 | 100 logs | resting |
| 25000 | the player looks: report (745 → 25000) produced 148 logs, moved 98, `courierSeconds` 14 045, courier 1 tired at 15095; re-energize: courier 1 leaves (Woodcutter at 25005) | 440 | 40 | 100 logs | 14 400 |
| 25005 | courier 1 at the Woodcutter: full, takes nothing; walks on (Warehouse at 25015) | 440 | 40 | 100 logs | carries 10 |

Check: logs produced 170 (90 … 5160) = 20 delivered + 100 + 10 carried + 40; coins 800 − 360 = 440. Courier 2 has no route and
never walks (stamina 14 400). Every walk of the route ends at a second ending in 5 (`95 + 20k` at the Woodcutter, `105 + 20k` at the
Warehouse).
