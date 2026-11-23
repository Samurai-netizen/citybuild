# BATCH-01 — every missing picture of M1, in one sheet

- **What it is:** a sheet that collects the 54 pictures missing from AR-M1-03, 05, 06, 08 and 09, so the owner can have them all made at
  once (owner, 2026-11-24, build log `docs/build-log/2026/2026-11-23-02-M1-02.md` → Owner input). It is **not a new request**: each row
  comes from the request named in its "From" column, and that request still keeps the row's status (`27`). Ids, paths and priorities
  are copied from them unchanged. Where a source row was still an outline (one or two words), this sheet gives the full description.
- **Not in it:** audio (AR-M1-08, 52 files) and the 3D models already delivered.
- **Style:** `docs/assets/STYLE.md` → The feel, UI (Icons), Portraits and illustrations, Effects · reference pictures: `art/previews/lineup_era1.png`,
  `art/previews/lineup_props.jpg`, `art/previews/ui_portrait.jpg`, the 25 M0 icons in `client/CityBuilder/Assets/_Project/Resources/UI/Icons/`
  (the icon look to match), and `docs/assets/ready-assets/AR-M1-05-models/AR-M1-05-preview.jpg` (the caravan wagon).
- **Written:** 2026-11-24 by Claude Code / claude-opus-5-5 · **Sent to the owner:** — · **Delivered:** —

## How to deliver
1. Put all 54 files **flat in one folder**: `docs/assets/ready-assets/BATCH-01-pictures/`. Name each file after its id: `<id>.png`,
   lower case, nothing added (`bld_farm.png`, not `bld_farm_v2.png` or `Bld Farm.png`).
2. Add a short note in that folder with the tool and model used and the date, for `docs/assets/LICENSES.md`.
3. Tell the coder "BATCH-01 delivered". The coder checks each file against its spec, moves it to the "Deliver to" path, and marks the
   row in its source request `accepted` or `rejected: <reason>`.

## A. Icons (36)
Common spec: 256 × 256 PNG, transparent background. One small 3D object rendered from the 3/4 view of the game camera, low-poly and
flat-shaded in the Era I palette, soft light from the upper left, no outline, no text, no ground plane or shadow box. The object fills
about 80 % of the square with an even margin. It must read at 48 px. Match the 25 M0 icons.

| Id | Deliver to | Description | From | Priority |
|---|---|---|---|---|
| `road_bridge` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/road_bridge.png` | A short log bridge alone, no water: a deck of round logs on two wooden trestles, low rails of thin poles. | AR-M1-03 | must |
| `land` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/land.png` | A rolled parchment map, half open, with a thin wooden survey stake beside it that has a red cloth tied on top. | AR-M1-03 | must |
| `orders` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/orders.png` | The order board: a small wooden notice board on two legs with three cream paper notes pinned to it. | AR-M1-05 | must |
| `caravan` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/caravan.png` | The caravan wagon seen from the side: a covered wagon with a red-and-cream striped canvas top, crates and sacks, a small horse in front. | AR-M1-05 | must |
| `order_done` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/order_done.png` | A cream paper note with a gold coin pinned to it. | AR-M1-05 | must |
| `order_dismiss` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/order_dismiss.png` | A cream paper note being crumpled into a loose ball. | AR-M1-05 | nice |
| `deco_well` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/deco_well.png` | A round stone well with a small wooden roof and a bucket on a rope. | AR-M1-06 | must |
| `deco_signpost` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/deco_signpost.png` | A wooden signpost with three arrow-shaped boards pointing different ways, no writing. | AR-M1-06 | must |
| `deco_banner` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/deco_banner.png` | A tall wooden pole with the town's blue-and-gold banner hanging from a crossbar. | AR-M1-06 | must |
| `deco_market_cross` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/deco_market_cross.png` | A carved wooden market post with a small pointed roof on top. | AR-M1-06 | must |
| `deco_flower_bed` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/deco_flower_bed.png` | A low bed of pink and purple flowers in a square log frame. | AR-M1-06 | must |
| `deco_bench` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/deco_bench.png` | A plank bench under a small round tree. | AR-M1-06 | must |
| `deco_hay_cart` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/deco_hay_cart.png` | A two-wheeled wooden cart heaped with golden hay. | AR-M1-06 | must |
| `deco_fountain` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/deco_fountain.png` | A round stone fountain with water spouting from the middle into the basin. | AR-M1-06 | must |
| `deco_duke_statue` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/deco_duke_statue.png` | A grey stone statue of the Duke on a square plinth, one hand raised. | AR-M1-06 | must |
| `item_stone` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/item_stone.png` | Raw stone: a small heap of three rough grey rocks with flat broken faces. | AR-M1-09 | must |
| `item_cut_stone` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/item_cut_stone.png` | Cut stone: two neat light-grey rectangular blocks, one stacked on the other. Clearly smoother and more regular than `item_stone`. | AR-M1-09 | must |
| `item_grain` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/item_grain.png` | Grain: a tied sheaf of golden wheat ears standing upright. | AR-M1-09 | must |
| `item_flour` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/item_flour.png` | Flour: a plump cream cloth sack tied at the top, a little white flour spilled in front. | AR-M1-09 | must |
| `item_bread` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/item_bread.png` | Bread: a round golden-brown loaf with three scored lines on top. | AR-M1-09 | must |
| `bld_quarry` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/bld_quarry.png` | The Quarry at level 1: a grey rock face cut into steps, a wooden crane pole, a few stacked grey blocks. | AR-M1-09 | must |
| `bld_stonemason` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/bld_stonemason.png` | The Stonemason at level 1: a small open yard with a wooden workbench, a mallet and chisel on it, cut blocks beside it, a little roof. | AR-M1-09 | must |
| `bld_farm` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/bld_farm.png` | The Farm at level 1: a small thatched farmhouse beside rows of golden grain, a scarecrow. | AR-M1-09 | must |
| `bld_windmill` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/bld_windmill.png` | The Windmill at level 1: a wooden post mill on a stone base with four cream cloth sails. | AR-M1-09 | must |
| `bld_bakery` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/bld_bakery.png` | The Bakery at level 1: a plaster cottage with a big round clay oven at its side, a smoking chimney, loaves on a shelf by the door. | AR-M1-09 | must |
| `bld_town_hall` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/bld_town_hall.png` | The Town Hall at level 1, the town's landmark: a large wooden log hall with a carved gable and a steep plank roof, a small blue-and-gold flag. Bigger and grander than a house. | AR-M1-09 | must |
| `res_honest_scales` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/res_honest_scales.png` | Research "Honest Scales" (cheaper trading): a brass balance scale, the two pans level, a gold coin on one pan. | AR-M1-09 | must |
| `res_town_charter` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/res_town_charter.png` | Research "Town Charter" (the end of Era I): a rolled parchment charter tied with a blue ribbon and a big red wax seal. Should look like the most important icon of the set. | AR-M1-09 | must |
| `res_stone_cutting` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/res_stone_cutting.png` | Research "Stone Cutting" (unlocks Quarry and Stonemason): a mallet and a chisel crossed over a grey stone block. | AR-M1-09 | must |
| `res_open_fields` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/res_open_fields.png` | Research "Open Fields" (unlocks the Farm, opens the east bank): a small wooden plough in front of a patch of brown furrows with green shoots. | AR-M1-09 | must |
| `res_milling` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/res_milling.png` | Research "Milling" (unlocks Windmill and Bakery): a round grey millstone with a hole in the middle and a few wheat ears leaning on it. | AR-M1-09 | must |
| `res_timber_framing` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/res_timber_framing.png` | Research "Timber Framing" (taller wooden buildings): a half-timbered wall section, dark beams in a cross pattern on cream plaster, a carpenter's square leaning on it. | AR-M1-09 | must |
| `res_masonry` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/res_masonry.png` | Research "Masonry" (stone buildings, unlocks the Town Hall): a short stone wall of neat blocks being built, a trowel resting on top. | AR-M1-09 | must |
| `res_guild_of_masons` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/res_guild_of_masons.png` | Research "Guild of Masons" (one more builder): a small hanging guild sign of wood with a carved mallet and square on it. Must look different from `res_masonry` and `res_stone_cutting`. | AR-M1-09 | must |
| `res_bridge_building` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/res_bridge_building.png` | Research "Bridge Building" (unlocks the bridge): a small arched wooden bridge over a strip of blue water. | AR-M1-09 | must |
| `res_land_survey` | `client/CityBuilder/Assets/_Project/Resources/UI/Icons/res_land_survey.png` | Research "Land Survey" (opens more land): a surveyor's measuring rope coiled around two wooden stakes with red cloths, on a patch of grass. Must look different from `land`. | AR-M1-09 | must |

## B. Merchant portraits (12)
Common spec: 512 × 512 PNG, transparent background. A head-and-shoulders bust in three-quarter view facing the screen's centre, in the
3D look of the models (low-poly, flat-shaded, palette colours, soft light), so the merchants belong to the same world. A friendly face
and one clear prop. Two moods per merchant: `_calm` (pleasant, neutral) and `_glad` (the same person, same pose and framing, smiling
broadly because the order was filled). Make each pair together so they match.

| Id | Deliver to | Description | From | Priority |
|---|---|---|---|---|
| `por_woodmonger_calm` | `client/CityBuilder/Assets/_Project/Resources/UI/Portraits/por_woodmonger_calm.png` | Hob the Woodmonger: a broad man in a leather apron, a log on his shoulder. | AR-M1-05 | must |
| `por_woodmonger_glad` | `client/CityBuilder/Assets/_Project/Resources/UI/Portraits/por_woodmonger_glad.png` | The same, smiling (order filled). | AR-M1-05 | nice |
| `por_stone_dealer_calm` | `client/CityBuilder/Assets/_Project/Resources/UI/Portraits/por_stone_dealer_calm.png` | Granby the Stone Dealer: a dusty, square-jawed trader with a chisel behind his ear. | AR-M1-05 | must |
| `por_stone_dealer_glad` | `client/CityBuilder/Assets/_Project/Resources/UI/Portraits/por_stone_dealer_glad.png` | The same, smiling (order filled). | AR-M1-05 | nice |
| `por_miller_buyer_calm` | `client/CityBuilder/Assets/_Project/Resources/UI/Portraits/por_miller_buyer_calm.png` | Maud the Miller: a cheerful woman in a flour-white kerchief, a small sack in her arms. | AR-M1-05 | must |
| `por_miller_buyer_glad` | `client/CityBuilder/Assets/_Project/Resources/UI/Portraits/por_miller_buyer_glad.png` | The same, smiling (order filled). | AR-M1-05 | nice |
| `por_baker_buyer_calm` | `client/CityBuilder/Assets/_Project/Resources/UI/Portraits/por_baker_buyer_calm.png` | Bess of the Inn: an innkeeper with a round face, a tankard and a loaf. | AR-M1-05 | must |
| `por_baker_buyer_glad` | `client/CityBuilder/Assets/_Project/Resources/UI/Portraits/por_baker_buyer_glad.png` | The same, smiling (order filled). | AR-M1-05 | nice |
| `por_quartermaster_calm` | `client/CityBuilder/Assets/_Project/Resources/UI/Portraits/por_quartermaster_calm.png` | Hale, the Duke's Quartermaster: a neat official in the Duke's blue, holding a tally board. | AR-M1-05 | must |
| `por_quartermaster_glad` | `client/CityBuilder/Assets/_Project/Resources/UI/Portraits/por_quartermaster_glad.png` | The same, smiling (order filled). | AR-M1-05 | nice |
| `por_river_trader_calm` | `client/CityBuilder/Assets/_Project/Resources/UI/Portraits/por_river_trader_calm.png` | Sadko the River Trader: a bearded river merchant in a fur hat, a gusli on his back. | AR-M1-05 | must |
| `por_river_trader_glad` | `client/CityBuilder/Assets/_Project/Resources/UI/Portraits/por_river_trader_glad.png` | The same, smiling (order filled). | AR-M1-05 | nice |

## C. Effect textures (6)
Common spec: 64 × 64 PNG with transparency, one shape centred, soft edges, flat-shaded, no photographic detail. **White or light grey
only**: the game tints and animates these, so colour in the file gets in the way. A single image, not a sheet.

| Id | Deliver to | Description | From | Priority |
|---|---|---|---|---|
| `fx_dust` | `client/CityBuilder/Assets/_Project/Art/Resources/Fx/fx_dust.png` | A soft round dust puff, a few overlapping blobs, fading to transparent at the edge. Used when something is built or demolished. | AR-M1-08 | must |
| `fx_sparkle` | `client/CityBuilder/Assets/_Project/Art/Resources/Fx/fx_sparkle.png` | A four-pointed star sparkle with a small soft glow in the middle. Used for finished goals and research. | AR-M1-08 | must |
| `fx_coin_glint` | `client/CityBuilder/Assets/_Project/Art/Resources/Fx/fx_coin_glint.png` | A small flat round coin seen at a slight angle with a bright glint on one side. Used when coins are gained. | AR-M1-08 | must |
| `fx_leaf` | `client/CityBuilder/Assets/_Project/Art/Resources/Fx/fx_leaf.png` | One simple leaf, an oval with a centre vein. Falls from trees and the woodcutter. | AR-M1-08 | must |
| `fx_flour` | `client/CityBuilder/Assets/_Project/Art/Resources/Fx/fx_flour.png` | A tiny cloud of fine powder, softer and lighter than `fx_dust`. Drifts from the windmill and bakery. | AR-M1-08 | must |
| `fx_chips` | `client/CityBuilder/Assets/_Project/Art/Resources/Fx/fx_chips.png` | Two or three small angular wood or stone chips flying apart. Used at the sawmill, woodcutter, quarry and stonemason. | AR-M1-08 | must |

## Notes for the asset model
- All icons are one family: same camera angle, same light, same margin. Make them in one go and compare them side by side at 48 px.
- Pairs that must not be confused at 48 px: `item_stone` and `item_cut_stone`, `res_masonry`, `res_stone_cutting` and `res_guild_of_masons`,
  `land` and `res_land_survey`, `item_flour` and `fx_flour`.
- Original designs only, nothing copied from another game (`STYLE.md`).

## Sort log
| Date | Files moved | Rejected | By |
|---|---|---|---|
| | | | |
