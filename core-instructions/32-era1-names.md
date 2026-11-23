# Era I Names (M1) — the game, the town, people, things

> M1-01 part D (CR-0011, G29, G30). Russian first, English second; the Russian is written as Russian, not translated word for word
> (`21` → Tone). Ids are in `28-era1-content.md` and `29-era1-chronicle.md`; these are the display names. M1-07 turns them into
> localization strings, and from then on the string tables are the source of truth (this file is updated with them).
> M0's English names (`mvp-content.json`) are kept.

## The game
Working title **CityBuilder** until the owner picks a name (G29). Candidates, checked 2026-11-23 by web search for a game of the same
name in RuStore and Google Play (only the names were sent). A web search doesn't replace the stores' own search and is not a trademark
check: the pick is searched once more in both stores before the RuStore listing (M1-17).

| # | Russian | English | Found | Note |
|---|---|---|---|---|
| 1 | Дорога веков | Road of Ages | nothing of that name | the roads and couriers, and the eras ahead; fits every era (recommended) |
| 2 | Летопись града | Town Chronicle | nothing of that name | the Chronicle is the guide of every era |
| 3 | Вольный город | Free Town | RU nothing; EN: *Free Towns*, a PC game (Steam) | the goal of Era I; EN too close to *Free Towns* |
| 4 | Переправа | The Crossing | RU nothing; EN: *Tales From The Crossing* (iOS) | the premise; medieval only |
| 5 | Хроники Переправы | Crossing Chronicles | nothing of that name | premise and Chronicle; long |
| 6 | Вольная грамота | Charter Town | nothing of that name | the end of Era I; medieval only |
| 7 | Курьеры и короны | Couriers & Crowns | nothing of that name | light, says logistics; medieval only |
| 8 | От избы до ракеты | Hut to Rocket | nothing of that name | humorous, promises every era; space is far away |
| 9 | Город сквозь века | Town Through the Ages | RU nothing; EN: *Through the Ages*, a board game with an app | too close to *Through the Ages* |
| 10 | Мастерград | Mastergrad | nothing in the stores | also a known Russian book series: a trademark risk |

## The town and its people

| Who | Russian | English |
|---|---|---|
| default town name (the player renames it, G28) | Новый Брод | Newford |
| the player | посадник | the Steward |
| the lord who grants the crossing | князь | the Duke |
| master builder (advisor) | мастер Гордей | Master Gordon |
| merchant (advisor) | Агафья | Agatha |
| scribe (advisor; the Chronicle, research) | брат Пимен | Brother Ambrose |
| courier master (advisor; couriers, roads) | Фрол | Wat Swiftfoot |
| `woodmonger` | Пров-лесоторговец | Hob the Woodmonger |
| `stone_dealer` | купец Ермолай | Granby the Stone Dealer |
| `miller_buyer` | мельничиха Дуня | Maud the Miller |
| `baker_buyer` | трактирщица Матрёна | Bess of the Inn |
| `quartermaster` | княжий ключник Савва | Hale, the Duke's Quartermaster |
| `river_trader` (the caravan) | Садко, речной купец | Sadko the River Trader |

## Items and buildings

| id | Russian | English |
|---|---|---|
| `coins` · `logs` · `planks` · `stone` | монеты · брёвна · доски · камень | Coins · Logs · Planks · Stone |
| `cut_stone` · `grain` · `flour` · `bread` | тёсаный камень · зерно · мука · хлеб | Cut Stone · Grain · Flour · Bread |
| `house` | Изба | Cottage |
| `woodcutter` | Хижина лесоруба | Woodcutter's Hut |
| `sawmill` | Лесопилка | Sawmill |
| `quarry` | Каменоломня | Quarry |
| `stonemason` | Двор каменотёса | Stonemason's Yard |
| `farm` | Хутор | Farm |
| `windmill` | Ветряная мельница | Windmill |
| `bakery` | Пекарня | Bakery |
| `trading_post` | Торжок | Trading Post |
| `courier_station` | Ямской двор | Courier Station |
| `warehouse` | Амбар | Warehouse |
| `scriptorium` | Скрипторий | Scriptorium |
| `town_hall` | Ратуша | Town Hall |
| roads: dirt path · log road · bridge | тропа · гать · мост | Dirt Path · Log Road · Bridge |

## Research

| Branch (RU · EN) | id | Russian | English |
|---|---|---|---|
| Торговля · Trade | `trade_charter` | Торговая грамота | Trade Charter |
| | `honest_scales` | Честные весы | Honest Scales |
| | `town_charter` | Вольная грамота | Town Charter |
| Ремёсла · Production | `sharp_axes` | Острые топоры | Sharpened Axes |
| | `frame_saw` | Рамная пила | Frame Saw |
| | `stone_cutting` | Камнетёсное дело | Stone Cutting |
| | `open_fields` | Распашка полей | Open Fields |
| | `milling` | Мельничное дело | Milling |
| Зодчество · Construction | `storehouses` | Закрома | Storehouses |
| | `timber_framing` | Плотницкое дело | Timber Framing |
| | `masonry` | Каменное зодчество | Masonry |
| | `guild_of_masons` | Артель каменщиков | Guild of Masons |
| Пути · Logistics | `tally_sticks` | Бирки | Tally Sticks |
| | `sturdy_boots` | Крепкие сапоги | Sturdy Boots |
| | `bridge_building` | Мостовое дело | Bridge Building |
| | `land_survey` | Межевание | Land Survey |

## The Chronicle, decorations, words

| What | Russian | English |
|---|---|---|
| the era | Раннее Средневековье | Early Middle Ages |
| chapters 1–6 | Переправа · Дороги и гонцы · Камень и река · Хлеб для города · Вольная грамота · Вольный город | The Crossing · Roads and Carriers · Stone and the River · Bread for the Town · The Charter · A Free Town |
| `well` · `signpost` · `banner` | Колодец · Указатель · Знамя | Well · Signpost · Banner |
| `market_cross` · `flower_bed` · `bench` | Рыночный столб · Клумба · Скамья | Market Cross · Flower Bed · Bench |
| `hay_cart` · `fountain` · `duke_statue` | Воз сена · Фонтан · Памятник князю | Hay Cart · Fountain · Duke's Statue |
| the Chronicle · chapter · goal · reward | Летопись · глава · задача · награда | Chronicle · Chapter · Goal · Reward |
| merchant order · order board · caravan | заказ · доска заказов · обоз | Merchant Order · Order Board · Caravan |
| mist · terrain: grass, forest, rock, fertile soil, water | туман · луг, лес, скалы, пашня, вода | Mist · Grass, Forest, Rock, Fertile Soil, Water |
| move · demolish · decoration | перенести · снести · украшение | Move · Demolish · Decoration |

## Lines
The advisors' lines (one per goal, chapter openings and endings, the tutorial) are written with the strings in M1-07, in this file's
voice: short, warm, a little dry; the advisor of the goal's system speaks (builder: buildings and levels; merchant: coins, orders,
the caravan; scribe: the Chronicle and research; courier master: couriers, routes, roads, land).
