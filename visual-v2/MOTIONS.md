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
`Models/common/road_l1` (dirt path) and `road_l2` (log road) are full tiles: grass with a centre patch, and four arms that reach the tile's
edges, named by the grid direction they point to: `Road_XPlus` (grid x + 1, Unity +X), `Road_XMinus`, `Road_YPlus` (grid y + 1, Unity
+Z), `Road_YMinus`. `RoadArms.Show` keeps the arms that point at neighbouring roads; a road alone is a patch.

## Ground
Four grass tiles, `tile_grass` (plain), `tile_grass_b` (flowers), `tile_grass_c` (pebbles), `tile_grass_d` (clover), picked per tile by a
fixed hash of its coordinates (`GroundVariants.For`, `ground_variant` in `blender/render_previews.py`): 4 in 10 plain, 2 in 10 each other.
Details keep away from the tile's middle, so a building's foundation covers them.
