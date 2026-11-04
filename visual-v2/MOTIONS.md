# Moving parts, store fill, smoke, roads and ground: the contract between the art and the client

How the models of visual v2 tell the client what moves and what fills up. The Blender side is `blender/lib.py` and
`blender/assets_era1.py`; the client side is `unity/Presentation/City/` (`PartMotions`, `MotionRig`, `BuildingAnimator`, `FillView`,
`StorageFill`, `SmokePuffs`, `GroundVariants`, `RoadArms`, `BuildingDecor`). Both sides use the same names and the same formula, so the
previews in `previews/motion/` show what the game draws.

## Moving parts
- A moving part is a child mesh of a model with its origin on its joint (an axle, a hinge, a shoulder). Next to it, under the same parent,
  is an empty named the part's name plus `_Pose`: where the part is at the far end of its motion.
- The client moves the part between its own transform and the pose, or spins it about the axis between them. Both transforms come through
  the same FBX import, so no axis is written in code.
- The kind and the timing come from the part's name. A copy of a part carries a number (`Flag_2`) and moves like `Flag`.

| Part | Kind | Cycle | Where |
|---|---|---|---|
| `Wheel` | spin: a quarter turn in the pose, one full turn per cycle | 4 s | Sawmill |
| `Sign` | spin | 3 s | Trading Post (the gold coin sign) |
| `Vane` | spin | 7 s | Scriptorium (the armillary sphere) |
| `Axe` | chop: rises slowly to the pose by 62 % of the cycle, falls back by 74 %, rests | 1.4 s | Woodcutter's Hut (the chopper's arms and axe) |
| `Hammer` | chop | 0.8 s | construction site (the builder's arms and hammer) |
| `Saw` | ping-pong: glides to the pose and back | 0.9 s | Sawmill (the frame saw, up and down) |
| `Hoist` | ping-pong | 5 s | Warehouse (the crate on the hoist) |
| `Flag` | wave: to the pose, through rest to its mirror and back | 1.6 s | Trading Post, Courier Station |
| `Quill` | wave | 1.1 s | (reserved) |
| `Leg_L`, `Leg_R`, `Arm_L`, `Arm_R` | wave; the right leg and the left arm half a cycle later | 0.6 s | worker, courier |

**Working and easing.** `BuildingAnimator` moves a building's parts and smoke while it works and eases them in and out over 0.6 s, so a
building that stops settles at rest. Today a building works when it is a construction site, a producer with a running cycle that nothing
stalls, or a cottage (smoke). CR-0008's new kinds (Scriptorium, Courier Station, Warehouse) stay still until its client task decides
when they work (research running, couriers on the road, goods coming and going).

## Store fill
- Each building level that keeps goods has a fill model, `Models/era<N>/<type>_l<level>_fill`: an empty root with children
  `Fill_<item>_<step>`, in the building's space. Steps are cumulative: step 2 shows steps 1 and 2.
- The building model itself is the empty store. The fill is a separate object, so it never interrupts the moving parts, and the
  placement ghost and the icons never show goods.
- The step shown is the store's share of its limit, one step per equal part (`StorageFill`): with three steps, up to a third shows step 1,
  from a third step 2, from two thirds step 3; an empty store shows none.
- Today the store is the city's stock of the item against its storage cap. With CR-0008 it becomes the building's own store.

| Building | Items and steps |
|---|---|
| Woodcutter's Hut | logs 3 (a row of logs each: 4, 3, 2; level 2: 5, 4, 3 under its lean-to) |
| Sawmill | planks 3 (two layers each), logs 2 (by the saw bay) |
| Trading Post | planks 2 (the goods for sale) |
| Warehouse | logs 3 (rows on a rack), planks 3 (two layers each) |
| Cottage, Scriptorium, Courier Station, construction site | none (CR-0008 §1: no store) |

## Smoke
`Anchor_Smoke` (Cottage, Woodcutter's Hut, Sawmill level 2, Scriptorium, Courier Station) is where `SmokePuffs` loops three instances
of `Models/common/fx_smoke_puff` while the building works: each rises 0.34, drifts and grows to its middle, then shrinks to nothing over
2.4 s. Opaque, so the city keeps one material.

## Roads
Each road level has three models, `Models/common/road_l<level>`, `road_l<level>_b` and `road_l<level>_c` (`RoadArms.PathOf(level,
variant)`), picked per tile by `RoadArms.VariantFor` from the top bits of the ground hash. Each is a full tile: grass with a centre (a
packed dirt patch, or a plank deck with nails) and four arms that reach the tile's edges, named by the grid direction they point to:
`Road_XPlus` (grid x + 1, Unity +X), `Road_XMinus`, `Road_YPlus` (grid y + 1, Unity +Z), `Road_YMinus`. `RoadArms.Show` keeps the arms
that point at neighbouring roads; a road alone is a patch. The sides of an arm wobble, but every variant has the same width at the tile
edge (the dirt path 0.34, the log road's bed 0.38, its ruts at the same places), so any two variants join. The dirt path's variants add
a puddle and hoof prints (b) or grass between the ruts and stones in the track (c); the log road's add a missing log with a puddle (b)
or moss on the logs and a loose plank (c). Roads are not turned: their arms have a direction.

## Ground
Eight grass tiles, all with soft mottling in colours a step warmer and cooler than the grass, tufts in mixed greens and short speckles:
`tile_grass` (lush), `_b` (daisies and poppies), `_c` (mossy stone clusters and pebbles), `_d` (clover), `_e` (a dry patch with dandelions),
`_f` (a ring of mushrooms and ferns), `_g` (a molehill) and `_h` (bluebells). `GroundVariants.For` picks one per tile by a fixed hash of
its coordinates, of every 20 tiles: plain 5, clover 3, every other 2; `GroundVariants.RotationFor` turns it by 0 to 3 quarter turns from
other bits of the same hash (the tiles are square and their details have no direction), so a town shows 32 different grass tiles.
`ground_variant`, `ground_turns` and `road_variant` in `blender/render_previews.py` are the same rules. Flat patches stay inside the
tile's top (they never cover the grid line); details taller than 0.02 keep out of the middle, which a foundation covers.
