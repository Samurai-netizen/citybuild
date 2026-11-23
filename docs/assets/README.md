# Assets — requests, style and licences

AI coders make no game assets; another AI model, run by the owner, makes them from an **asset request**
(`core-instructions/27-asset-pipeline.md`, owner directive OD-22). This folder is where the requests live.

| Path | What |
|---|---|
| `docs/assets/TEMPLATE.md` | The form of a request. `/asset-request <TASK-ID>` fills it (Claude Code) |
| `docs/assets/requests/AR-<TASK-ID>.md` | One request per task tagged `[ASSETS]`; one row per file to deliver *(the first ones: M1-01)* |
| `docs/assets/STYLE.md` | The look and sound every request points to (M1-01): 3D, UI, portraits, audio, effects |
| `docs/assets/LICENSES.md` | Every delivered file: who or what made it, when, under which terms *(starts with the first delivery)* |

## For the owner: from a request to the game
1. A task report says which request is ready, for example `AR-M1-05`.
2. Give the asset model the request file, the style page and the reference pictures it names. Each row's description is written to
   be used as the prompt; the spec column says the format and size to ask for.
3. Put each file at the path its row names, with the id as the file name.
4. Tell the coder "AR-M1-05 delivered". The coder checks every row against its spec and marks it `accepted` or `rejected: <reason>`;
   a rejected row's reason is what to change in the next attempt.
5. You judge the look and the sound. The coder never repairs an asset.

The game runs before the files arrive: every asset has a placeholder made in code (`27` → Placeholders).

## Requests
M1-01 wrote one per `[ASSETS]` task of M1 (2026-11-23). AR-M1-03 and AR-M1-04 are complete and can be sent; the others are in outline
(ids and rows final, descriptions finished one task ahead, `27`). A model and its icon may share an id (`road_l1.fbx`, `road_l1.png`).

| Request | For | Rows | State |
|---|---|---|---|
| `requests/AR-M1-03.md` | terrain tiles, bridge, mist, survey stake | 17 | complete |
| `requests/AR-M1-04.md` | chapter cards, Chronicle icons | 12 | complete |
| `requests/AR-M1-05.md` | merchant portraits, caravan, order icons | 18 | outline |
| `requests/AR-M1-06.md` | decorations and their icons | 18 | outline |
| `requests/AR-M1-07.md` | UI skin, glyphs, a display font with Cyrillic | 28 | outline |
| `requests/AR-M1-08.md` | music, ambience, sound effects, effect textures, townsfolk | 63 | outline |
| `requests/AR-M1-09.md` | Era I buildings and levels, fills, stockpiles, loads, icons | 84 | outline |
| `requests/AR-M1-10.md` | advisor portraits, premise cards, pointer | 12 | outline |
| `requests/AR-M1-17.md` | store graphics | 5 | outline |
