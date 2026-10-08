# Eras and Research — the progression system

There are no XP points, player levels or city levels. A city progresses in three ways only:
**research** (the main one), **building levels** and **land**. Research moves the city through **eras**.

## Rules (owner's vision — fixed)
1. The game starts in the Middle Ages.
2. The player chooses a node in the era's research tree, **finances** it, and **waits** a relatively long time.
3. Research invents new building types, makes production more efficient, or extends existing systems.
4. When **every** research node of the current era is complete, the city enters the next era.
5. **New major mechanics arrive only with a new era.** Research inside an era adds only minor ones.

## Major vs minor (how to apply rule 5)

| Major mechanic — era entry only | Minor mechanic — research inside an era |
|---|---|
| A new system with its own rules **and** a new screen or a new kind of player decision | A new building, recipe, item, level cap, efficiency bonus, storage, slot or land |
| Examples: open market, guilds, population needs, expeditions, fuel, power grid, automation | Examples: unlock Sawmill, −20 % labor, +250 storage, +1 builder slot, Land Survey (+2 rows) |

When unsure, treat it as major and put it in the next era. The game stays simple early and gets deep later.

## Era roadmap (proposal — the owner can reorder)

| # | Era | New goods (examples) | Major mechanics added | Target length* |
|---|---|---|---|---|
| I | Early Middle Ages | logs, planks, stone, grain, flour, bread | **Base set:** grid building, production chains, workers, building levels, storage, builders, research, Trading Post (NPC buyers) | 3–5 days |
| II | High Middle Ages | iron ore, iron, tools, wool, cloth, ale | **Open market** (player trading) · **Guilds** (co-op groups, shared projects) | 1–2 weeks |
| III | Renaissance & Discovery | glass, paper, dyes, spices, fine clothes | **Population classes & needs** (citizens consume goods to grow) · **Expeditions** (timed voyages for exotic goods) | 2–3 weeks |
| IV | Industrial Age | coal, steel, machine parts, textiles | **Fuel & machines** (optional fuel input speeds buildings up) · **Production lines** (switchable recipes, larger 2×2 factories) | 3–5 weeks |
| V | Age of Electricity | electricity, chemicals, electronics | **Power grid** (coverage areas on the map make placement a puzzle) · **Automation** (managers re-staff buildings and run market orders by rules) | 1–2 months |
| VI | Space Age | alloys, rocket fuel, satellites | **Megaprojects** (multi-week guild/server projects such as a launch site) | endgame |

\*For an engaged player checking in 3–5 times a day. 1.0 should ship with Eras I–III; later eras are major updates.

## Design rules that follow from the vision
- **Old goods stay useful.** Later recipes keep consuming earlier goods (e.g. factories still need planks). This creates
  market demand for goods that newer players make, so they always have something to sell.
- **"Complete everything" must not become a wall.** No single node may take more than about ¼ of the era's target length.
  Total research time is roughly 60–70 % of the target; the rest is coin and material gating.
- **Each era's tree has 3–4 branches** (e.g. Production, Construction, Trade, Civic) so the player chooses an order,
  even though everything is eventually required.
- **Research needs no workers.** Workers are for production; research is paid for in coins and goods.
- **Durations grow by era:** I 10 min–4 h · II 1–12 h · III 4–24 h · IV 8–48 h · V+ 1–3 days.
- **Slots:** one research slot at the start; at most one more from research in a later era (minor mechanic).

## Effect types
| Effect | Example | In M0 |
|---|---|---|
| `UnlockBuilding(type)` | Trade Charter → Trading Post | yes |
| `LaborReduction(type, ‰)` | Sharpened Axes → Woodcutter −200 ‰ | yes |
| `StorageCapBonus(item, +n)` | Storehouses → +250 logs, planks | yes |
| `MaxLevelBonus(type, +n)` | Timber Framing → Cottage max level 3 | later |
| `LandExpansion(+rows, +cols)` | Land Survey I → grid 10 × 10 | later |
| `BuilderSlots(+n)`, `ResearchSlots(+n)` | Guild of Masons → +1 builder | later |
| `OfflineProductionBonus(+seconds)` | Overseers → +2 h production cap | later |
| `UnlockRecipe(type, recipe)` | used once Era IV production lines exist | later |

## Era transition (built when Era II exists)
- It triggers at the exact clock time the era's last research completes (inside `Advance`, so it is deterministic).
- `city.Era` increments, the next tree becomes available, and the era's major mechanics unlock with a short guided intro.
- Existing buildings keep working. New buildings use the new era's look. There are no resets or prestige.
- The player gets a one-time "era charter" grant of goods for the new era, so the first new building isn't a wait.

## Research UX (see `21-game-vision.md`)
A tree view per era: nodes show cost, duration, effect and status (Locked / Available / In progress / Done).
There is a progress ring in the HUD, and the welcome-back report lists the research that completed.
Cancelling research is not in M0. Decide in M1 (proposal: cancel refunds 50 %).
