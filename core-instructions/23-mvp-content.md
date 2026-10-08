# MVP Content (M0) — exact numbers

> These are the design numbers for the 14-day MVP: a small slice of **Era I (Early Middle Ages)**.
> On Day 2 they are copied into `server/content/mvp-content.json`, and **from then on the JSON is the
> source of truth**. Update this file in the same commit whenever the JSON changes.
> Every number is a placeholder until the balance simulator (Day 12) validates it.
> M0 research times are short so the slice can be played in one sitting. Real Era I research takes 10 min–4 h (`25`).

## Global rules

| Rule | Value |
|---|---|
| Era at start | 1 — `early_medieval` |
| Grid | 8 × 8 tiles, every building occupies 1 tile |
| Builder slots / research slots | 2 / 1 |
| Base storage cap | 500 per capped item (`logs`, `planks`) |
| Coins | uncapped, `long` |
| Max offline production per settlement | 28 800 s (8 h); timers are never capped |
| Minimum cycle | 10 s with every research applied (content validation) |
| Max total labor reduction per building type | 500 ‰ |
| Content version | `mvp-1` |

## Items

| id | Name | Era | Capped | Tradable (future market) |
|---|---|---|---|---|
| `coins` | Coins | 1 | no | no (it's the currency) |
| `logs` | Logs | 1 | yes | yes |
| `planks` | Planks | 1 | yes | yes |

## Buildings

| id | Name | Unlocked by | Cost | Build time | Max level | L1 / L2 |
|---|---|---|---|---|---|---|
| `house` | Cottage | start | 50 coins | 30 s | 2 | provides 4 / 6 workers |
| `woodcutter` | Woodcutter's Hut | start | 80 coins | 60 s | 2 | max workers 2 / 4 |
| `sawmill` | Sawmill | start | 150 coins + 30 logs | 120 s | 2 | max workers 3 / 6 |
| `trading_post` | Trading Post | research `trade_charter` | 200 coins + 20 planks | 180 s | 2 | max workers 2 / 4 |

## Recipes (the same at both levels; a level adds worker slots)

| Building | Inputs → Outputs | Labor per cycle | Examples |
|---|---|---|---|
| Woodcutter's Hut | — → 1 logs | 60 worker-s | 1 worker: 60 s (60/h) · 2: 30 s (120/h) · 4: 15 s (240/h) |
| Sawmill | 2 logs → 1 planks | 180 worker-s | 3 workers: 60 s (60/h) · 4: 45 s · 6: 30 s (120/h) |
| Trading Post (NPC merchants) | 2 planks → 10 coins | 120 worker-s | 1 worker: 120 s (300 coins/h) · 2: 60 s (600/h) · 4: 30 s |

These ratios are intentional. At level 1, one full Woodcutter's Hut feeds one Sawmill, and one Trading Post
needs two Sawmills. The Trading Post is the only source of coins (the NPC "faucet").

## Upgrades (level 1 → 2)

| Building | Cost | Time |
|---|---|---|
| Cottage | 120 coins + 30 logs | 90 s |
| Woodcutter's Hut | 150 coins + 20 planks | 120 s |
| Sawmill | 250 coins + 30 planks | 180 s |
| Trading Post | 400 coins + 40 planks | 300 s |

## Research (Era I subset, one slot)

| id | Name | Cost | Time | Requires | Effect |
|---|---|---|---|---|---|
| `trade_charter` | Trade Charter | 150 coins | 600 s | — | unlocks Trading Post |
| `sharp_axes` | Sharpened Axes | 250 coins | 1 800 s | — | Woodcutter labor −200 ‰ (60 → 48) |
| `frame_saw` | Frame Saw | 400 coins + 30 planks | 3 600 s | `sharp_axes` | Sawmill labor −200 ‰ (180 → 144) |
| `storehouses` | Storehouses | 300 coins + 50 logs | 2 700 s | `trade_charter` | storage cap +250 for logs and planks |

With both labor researches, a full L1 Woodcutter (24 s/log, 150/h) feeds a full L1 Sawmill (48 s, 150 logs/h) exactly.
Completing all four doesn't change the era in M0, because there is no Era II content yet.

## Starting city (created by the server for every new player)
coins 700 · logs 40 · planks 10 · era 1 · no buildings · no research · city clock 0

## Golden scenario #1 — first session (hand-checked; re-check whenever content changes)
Ids: Cottages 1–2, Woodcutter 3, Sawmill 4, Trading Post 5.
Actions: `t=0` StartResearch `trade_charter`, place 2 Cottages · `t=30` place Woodcutter + Sawmill ·
`t=750` place Trading Post. No other actions.

| t (s) | What happens | coins | logs | planks | free workers |
|---|---|---|---|---|---|
| 0 | after actions | 450 | 40 | 10 | 0 |
| 30 | Cottages done (8 workers), after placing | 220 | 10 | 10 | 8 |
| 90 | Woodcutter done, auto-staffed with 2, starts | 220 | 10 | 10 | 6 |
| 150 | Sawmill done, auto-staffed with 3; logs 12 → 10 at start | 220 | 10 | 10 | 3 |
| 600 | `trade_charter` completes | 220 | 11 | 17 | 3 |
| 750 | before placing: planks 20 → after placing Trading Post | 20 | 10 | 0 | 3 |
| 930 | Trading Post done, auto-staffed with 2, takes 2 planks | 20 | 10 | 1 | 1 |
| 990 | first coins | 30 | 10 | 0 | 1 |
| 1050 | coins; Trading Post idle `MissingInput:planks` | 40 | 10 | 1 | 1 |
| 1170 | coins; idle again | 50 | 10 | 1 | 1 |

From then on: +10 coins every 120 s (≈ 300 coins/h). The Trading Post runs at 50 % because one Sawmill
can't feed it. That's the first bottleneck the player is meant to discover. Setting the Trading Post to
1 worker earns the same and frees a worker.
