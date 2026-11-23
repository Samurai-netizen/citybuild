# Era I Chronicle (M1) — map, chapters, orders, decorations

> The second half of the Era I design (M1-01, CR-0011); items, buildings and research are in `28-era1-content.md`. **Every number is a
> first pass for the balance simulator (M1-15).** M1-02 … M1-06 copy it into content data; then the JSON is the source of truth.
> Rules: `22` (the Chronicle, orders, terrain, ownership), `24` (faucets and pacing), decisions G24–G44 in `12`.

## Map (one hand-made map, the same for every player)
18 × 14 tiles, x left to right, y top to bottom. `.` grass · `F` forest · `R` rock · `S` fertile soil · `W` water (bridges only).

```text
     x 0         1
       012345678901234567      areas
y  0   FFFFFF..RRRRW..SSS      BBBBBBBBBBBBBCCCCC
   1   FF....FF..RRW.SSSS      BBBBBBBBBBBBBCCCCC
   2   FFFF......RRW.SSSS      BBAAAAAAAAAAACCCCC
   3   FFFF.....RRRWSSSS.      BBAAAAAAAAAAACCCCC
   4   F.FF........WSSSS.      BBAAAAAAAAAAACCCCC
   5   FFFF........WSS.FF      BBAAAAAAAAAAACCCCC
   6   F...........WSS.FF      BBAAAAAAAAAAACCCCC
   7   FF..........WSSSS.      BBAAAAAAAAAAACCCCC
   8   F...........W.SSSS      BBAAAAAAAAAAACCCCC
   9   FF......F...W.SSSS      BBAAAAAAAAAAACCCCC
  10   F......FF...W..SS.      BBAAAAAAAAAAACCCCC
  11   FF..........W.....      BBAAAAAAAAAAACCCCC
  12   ..RR....FF..WRRR..      BBBBBBBBBBBBBCCCCC
  13   .RRRR..FFF..WRRR..      BBBBBBBBBBBBBCCCCC
```

- **Area A** (the start): x 2–12, y 2–11: 10 × 10 of land and the river beside it. 11 forest and 5 rock tiles, 19 grass tiles beside a
  forest (Woodcutter spots), 7 beside rock (Quarry spots), no fertile soil.
- **Area B** (`land_survey`): the rim, x 0–1 and y 0–1 and y 12–13 west of x 13: more forest and rock, room for houses.
- **Area C** (`open_fields`): the east bank, x 13–17: 37 fertile soil tiles for farms. It is reached over a bridge.
- Unopened areas lie under mist (`22`). A building needs every tile it stands on to be open; walkers may only walk open tiles.

## The Chronicle
Goal conditions the engine checks (M1-04): `TownNamed` · `BuildingCount(type, minLevel, n)` · `Produced(item, n)` (since the chapter
opened) · `ResearchDone(id)` · `RoadTiles(kind, n)` · `RoutesSet(n)` · `OrdersFulfilled(n)` · `CaravansFulfilled(n)` (both since the city
began); `Produced(coins, n)` counts only the Trading Post's recipe. Rewards: coins (`c`), a decoration (a name after a goal is that goal's
reward), a story line (every goal has one, said by an advisor; texts in part D).

| Ch | Name, when | Goals (in order shown; any order to complete) and rewards | Final event |
|---|---|---|---|
| 1 | The Crossing · first session, 10–15 min (tutorial) | name the town 50c · build a Cottage 50c · build a Woodcutter's Hut 50c · produce 10 logs 50c · build the Scriptorium 50c · research `trade_charter` 100c · produce 5 planks 100c · produce 20 coins at the Trading Post (`Produced(coins, 20)`) · Well | the first sale |
| 2 | Roads and Carriers · day 1 | build a Courier Station 100c · set a route 100c · lay 8 road tiles 100c · Woodcutter's Hut at level 2 150c · build a Warehouse 100c · fulfil 2 orders · Signpost · research `stone_cutting` 150c · fulfil a caravan 200c · Banner | the first caravan |
| 3 | Stone and the River · days 2–3 | build a Quarry 150c · build a Stonemason 150c · produce 50 cut_stone 200c · Sawmill at level 2 150c · research `land_survey` 200c · research `bridge_building` 200c · build a bridge 300c · Market Cross | the bridge |
| 4 | Bread for the Town · days 4–5 | research `open_fields` 250c · 2 Farms 200c · produce 100 grain 200c · research `timber_framing` 250c · any building at level 3 300c · fulfil 10 orders · Flower Bed · research `milling` 300c · build the Windmill 400c | the windmill turns |
| 5 | The Charter · days 5–8 | build a Bakery 300c · produce 100 bread 300c · research `masonry` 400c · build the Town Hall 400c · Town Hall at level 3 600c · fulfil 3 caravans · Fountain · research `guild_of_masons` 500c · research `town_charter` · Duke's Statue | the Town Charter |
| 6 | A Free Town (epilogue, G38) | fulfil 25 orders 500c · fulfil 7 caravans 500c · every building type at level 3 · Bench · Era II is coming (story) | — (no end) |

39 goals in chapters 1–5; their coins add up to 7 100 over the era, a one-time faucet (G40). Chapter 1's 450 coins and the
1 000 starting coins pay for everything chapter 1 asks (M1-15 checks it; golden #3 in M1-04 walks it).

## Merchant orders and the caravan
- **Merchants** (one per kind of goods; names in Names below): `woodmonger` (logs, planks) · `stone_dealer` (stone, cut_stone) ·
  `miller_buyer` (grain, flour) · `baker_buyer` (bread) · `quartermaster` (the Duke's man: any two kinds) · `river_trader` (the caravan).
- **Order price** = Σ (amount × reference price, `28`) × markup ‰ ÷ 1000, rounded down. A merchant offers only goods the city can make
  (its buildings unlocked).

| From chapter | Goods in an order | Amount per good | Markup | Refill after fulfil or dismiss |
|---|---|---|---|---|
| 1–2 | 1 kind: logs, planks | 10–30 | 1300–1500 ‰ | 20 min |
| 3 | 1–2 kinds: + stone, cut_stone | 10–30 (cut_stone 5–15) | 1300–1500 ‰ | 30 min |
| 4 | 1–3 kinds: + grain, flour | 15–40 (flour 10–20) | 1400–1600 ‰ | 40 min |
| 5+ | 1–3 kinds: + bread | 20–40 (bread 10–30) | 1400–1600 ‰ | 40 min |

- **Caravan:** the first arrives when the first Trading Post is finished, then every 24 h after the last arrival; it stays until the next
  replaces it (G44). Two or three kinds, three times the chapter's amounts, markup 2000 ‰; every third caravan also gives a decoration
  (Bench, Flower Bed, Hay Cart in turn).
- **Seed:** the goods, amounts and markup of order number *n* come from an integer function of the city's id and *n* over these tables
  (spec and test vectors in `04` by M1-05; no `System.Random`, no clock).

## Decorations

| id | How to get it | Cost (coins) |
|---|---|---|
| `well` | chapter 1 | — |
| `signpost` | chapter 2 | — |
| `banner` | chapter 2 (first caravan) | — |
| `market_cross` | chapter 3 (the bridge) | — |
| `flower_bed` | chapter 4, caravans, shop | 50 |
| `bench` | chapter 6, caravans, shop | 80 |
| `hay_cart` | caravans, shop | 120 |
| `fountain` | chapter 5, shop | 1 500 |
| `duke_statue` | chapter 5 (the Charter) | — |

A shop decoration is bought from the build menu: coins go out through the ledger (a sink, `24`). An earned one waits in the build menu
until placed; put away, it returns there (G42).

## Names
Russian and English names of the game, the town, the advisors, the merchants and every item, building, research, chapter and decoration:
M1-01 part D (pending). Ids above are final; display names are content strings (localization keys, M1-07).
