# Assets — requests, style and licences

AI coders make no game assets; another AI model, run by the owner, makes them from an **asset request**
(`core-instructions/27-asset-pipeline.md`, owner directive OD-22). This folder is where the requests live.

| Path | What |
|---|---|
| `docs/assets/TEMPLATE.md` | The form of a request. `/asset-request <TASK-ID>` fills it (Claude Code) |
| `docs/assets/requests/AR-<TASK-ID>.md` | One request per task tagged `[ASSETS]`; one row per file to deliver *(the first ones: M1-01)* |
| `docs/assets/STYLE.md` | The look and sound every request points to *(planned, M1-01; until then `art/README.md` → Style guide)* |
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
None yet. M1-01 (`docs/plan/M1-PLAN.md`) writes the first ones; the indicative counts are in that plan's §8.
