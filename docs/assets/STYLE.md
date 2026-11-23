# Style guide — the look and sound of Погост / Pogost

The one page every asset request (`requests/AR-*.md`) points to (M1-01 part E, `core-instructions/27-asset-pipeline.md`). A request row
says only what is particular to its asset; everything here holds for all of them. Game: `core-instructions/21-game-vision.md` → The look,
Tone; names: `core-instructions/32-era1-names.md`.

## The feel in one paragraph
A cheerful toy town on a sunny table. Chunky low-poly buildings with small bevels, flat-shaded faces, a warm palette, soft light and
soft shadows. Everything reads at a glance from a phone held upright: shapes first, details second, text never. Era I is timber,
thatch, plaster and a little stone, in a Northern Rus flavour (log walls, carved gables, thatched and plank roofs) mixed with the
half-timbering the M0 models already have. Friendly and lightly humorous, never grim, never cute to the point of baby-toy.
**Original designs only**: nothing copied or traced from Tap Tap Builder or any other game (`core-instructions/30-out-of-scope.md`).

**Reference pictures** (send them with every request): `art/previews/lineup_era1.png` (all Era I models), `art/previews/town.jpg` and
`town_closeup.jpg` (a town in play), `art/ui/city_phone.jpg` (the phone view), `art/previews/ui_portrait.jpg` (the UI), and the single
models in `art/previews/*.png`. A request adds the pictures nearest its asset.

## 3D models
The contract of `art/README.md` → Style guide holds for every model: the 8 × 8 palette texture and its fixed slots, 1 unit = 1 m = one
tile, the pivot at the tile centre on the ground, the door facing −Z, buildings within ±0.475 of the pivot and exported at 1.2, the
triangle budgets, one mesh and one material, the file names `<contentId>_l<level>.fbx` and the anchors. Moving parts, store fills,
smoke and roads follow `art/MOTIONS.md`. A delivery that breaks the contract is rejected however good it looks.

Additions for Era I (M1):
- **New colours** go into free palette slots (34 and up), never by moving one. A request that needs one names it and its hex.
- **Levels:** level 2 is the same building grown (taller, a lean-to, a second stockpile); level 3 adds stone and pride: a stone
  base or ground floor, a carved gable or a banner, a tiled or shingled roof. A player must tell L1, L2 and L3 apart from the default
  zoom by silhouette alone.
- **Readability:** each building shows its job on its front and west side: a tool, a product stockpile or a sign (the Windmill's sails,
  the Bakery's oven and loaves, the Quarry's cut face and blocks). Stockpile props follow the M0 rule (`prop_<item>_<s|m|l>`).
- **Town Hall:** the one landmark: about 1.5 × the height of other buildings, still within one tile; a bell tower or a carved tent
  roof. Its three levels read as stages: a wooden hall, then a stone floor, then the tower.
- **Terrain tiles** (1 × 1, top at y = 0, ≤ 1,000 triangles each, with 2–3 variants so a field doesn't tile visibly): forest (two to
  four trees in the M0 tree style on darker grass), rock (grey boulders with a cut face), fertile soil (brown furrows with a grass
  rim), water (flat water with a little foam at the banks; banks are tile edges, not separate models).
- **Bridge:** a log bridge on two trestles that spans one water tile, joining road tiles on both banks.
- **Mist:** soft white-blue cloud puffs over closed land, low enough that the terrain shapes show through at the edges. A mist is a
  model plus a fade the client does; it is never a wall.
- **People and animals:** the M0 worker's size (about 0.17) and style: round head, simple body, no faces at that size. Townsfolk wear
  palette colours (a red or blue tunic, a straw hat); animals are hens, a goat, a dog, a cat (about 0.06–0.1). ≤ 600 triangles each.
- **Decorations:** one tile, ≤ 1,500 triangles, lower than a Cottage, open enough that walkers can be seen behind them.

## UI
The UI is built in code from UI Toolkit (layout, colours, rounded corners and borders are the coder's work, `27`). Assets are its icons,
a few 9-slice panels and the fonts.

| Role | Colour | Role | Colour |
|---|---|---|---|
| Parchment (panels) | `#F4E8CF` | Gold (coins, highlights) | `#F2C230` |
| Card | `#EADBB9` | Go (confirm, rise) | `#3B7D34` |
| Ink (text) | `#3A2A1A` | Stop (danger, fall) | `#B8402C` |
| Wood (frames) | `#7A4E2D` | Info (links, couriers) | `#4F80B9` |
| HUD (dark glass) | `#301F12` at 88 % | Cream (text on dark) | `#FFF6E3` |

The client's USS variables (`--c-*` in `client/CityBuilder/Assets/_Project/Resources/UI/*.uss`) are the source of these values.
- **Shapes:** rounded rectangles (corner 12–16 px at 1080 px width), thick wooden or gold edges, soft drop shadows; no glass, neon or
  flat-material looks. Touch targets at least 48 dp.
- **Icons:** a small 3D object rendered from the same 3/4 view and palette as the models, flat-shaded, no outline, transparent
  background, 256 × 256 PNG, the object filling about 80 % with an even margin (the 25 M0 icons in
  `client/CityBuilder/Assets/_Project/Resources/UI/Icons/` are the reference). One object per icon, readable at 48 px. Names:
  `item_<id>`, `bld_<id>`, `res_<id>`, `deco_<id>`, `road_<kind>`, or a plain word for a HUD icon.
- **9-slice panels:** PNG at 2× (for 1080 px wide screens), borders given in the request in pixels; the middle stretches, so it is
  plain colour or a texture that stretches without showing it.
- **Fonts:** body text Nunito (Bold, ExtraBold), which has Cyrillic. The heading font Fredoka has **no Cyrillic**: Russian headings
  use Nunito ExtraBold until a rounded display font with full Cyrillic and an open licence (SIL OFL) arrives through an asset request
  (a third-party font is a G-Dependency: the owner approves it and `LICENSES.md` records it). Never a font without Cyrillic.

## Portraits and illustrations
- **Advisors and merchants** (names in `32`): a head-and-shoulders bust, three-quarter view facing the screen's centre, in the 3D
  look of the models (low-poly, flat-shaded, palette colours, soft light) so they belong to the same world. Friendly faces with one
  clear prop each: the master builder a plumb line or a square, the merchant a purse, the scribe a quill and a book, the courier master
  a horn and a satchel. 512 × 512 PNG, transparent background; two moods each (`_calm`, `_glad`).
- **Chapter cards and moments:** a wide picture of the town scene the chapter is about (the crossing, the first caravan, the bridge,
  the windmill, the charter), 1600 × 900 PNG or JPG, drawn from the game's own 3/4 view.
- **Store graphics** (M1-17): the icon is the Town Hall or a crossing on a plain warm background, readable at 48 px, no text; sizes
  and the screenshot list come from the store's current rules, written into AR-M1-17.

## Audio
- **Mood:** calm, warm, a little playful; morning in a riverside village. Folk instruments of the Russian north and of medieval Europe:
  gusli, balalaika played softly, wooden flute (svirel), recorder, hurdy-gurdy, frame drum, light bells. No choirs of doom, no epic
  orchestra, no modern beats, no voices that speak words (Russian and English share the game).
- **Music:** 2–3 tracks of 2–3 min that loop seamlessly (the loop point is the file's end), mid tempo, low dynamic range so it sits
  under the sounds. OGG Vorbis, 44.1 kHz, stereo, about −18 LUFS integrated, peaks ≤ −1 dBTP.
- **Ambience:** loops of 30–60 s (birds, a river, a breeze, distant village life), OGG, stereo, about −24 LUFS.
- **Sound effects:** short and soft, wooden and natural (a tap on wood, a coin clink, a saw, an axe, a soft chime for a finished goal),
  never harsh or loud. UI 0.05–0.4 s, world 0.2–1.5 s, moments (a finished building, research, chapter) 1–3 s. WAV, 16-bit, 44.1 kHz,
  mono for the world and UI, stereo for moments, peak −3 dBFS, no silence at the start.
- **Names:** `music_<name>`, `amb_<name>`, `sfx_<thing>_<action>` (`sfx_coin_gain`, `sfx_building_done`).
- **Budget:** all audio files together ≤ 25 MB; music and ambience stream from disk, the effects loaded in memory stay ≤ 16 MB
  (the client measures it, M1-08).

## Effects
Particles and motion are the coder's work (`27`); the asset is a small texture they use: dust puffs, sparkles, coin glints, leaves,
flour dust, wood chips. A soft, round, flat-shaded shape in a palette colour, no hard photographic detail. PNG with transparency,
64 × 64 or a sheet of 4–8 frames of that size in one row, white or light grey where the client tints it. ≤ 16 effect textures in Era I.

## Licences
Every delivered file gets a row in `docs/assets/LICENSES.md`: who or what made it (tool and model), when, under which terms (`27`).
