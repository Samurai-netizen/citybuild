# Local end-to-end check

The steps that prove the Unity client and the real server work together on one machine: the client logs in, loads the content and the city,
shows "Connected", goes offline when the server stops, and reconnects when it comes back. Run them after a change to the start of the game,
the connection or the wire. They take about 10 minutes, most of it waiting for polls. Pressing Play is a manual step, because an AI coder can't
use the Editor (`core-instructions/09-unity-client.md`).

## What you need
- Docker Desktop running, and `.env` made from `.env.example` (`docs/getting-started.md` → Database).
- The .NET SDK, and Unity 6000.3.25f1 with the project `client/CityBuilder` added in Unity Hub (`client/README.md`).
- Once per clone, the API's two secrets. See which ones you have:
  ```
  dotnet user-secrets list --project server/src/CityBuilder.Api
  ```
  You need `ConnectionStrings:CityBuilder` and `Auth:SigningKey`. If one is missing, set it with step 2 of `docs/getting-started.md` → Database. The signing key
  is one line:
  ```
  dotnet user-secrets set Auth:SigningKey "$(openssl rand -base64 48)" --project server/src/CityBuilder.Api
  ```

## The steps
Run the commands from the repository root. Keep the API's terminal open: you stop the server there in step 4.

**1. Start the database and the API.**
```
docker compose up -d postgres
dotnet run --project server/src/CityBuilder.Api
```
In a second terminal, run `curl http://localhost:8080/health`. It answers `Healthy`.

**2. Press Play.** Open the project in Unity and wait until the Console has no more import messages. Open `Assets/Scenes/Bootstrap.unity` and press **Play**.

**3. Check the start.** The dark label at the top left of the Game view reads, within a few seconds:

> Connected - Early Middle Ages - Coins: 700

Behind the label the Game view is dark blue-grey, and a green chessboard of 8 by 8 tiles shows in a diamond (the city's ground, M0-43). The Console has no red messages. Try the controls: hold the left mouse button and drag to move the view (it stops at the edge of the grid), turn the mouse wheel to zoom, move the pointer to mark a tile in yellow, and click a tile to mark it in orange (click it again to clear it).

A new city has no buildings, so the ground is empty. To see the building views (M0-44), place a Cottage from the build menu (the placement flow below, M0-46), or, to test the views alone, with the command line (do it before pressing Play, or press Play again afterwards: the client asks for the city every 120 s, so a wait of up to two minutes shows it too):
```
TOKEN=$(curl -s -X POST http://localhost:8080/api/v1/auth/dev-login -H 'Content-Type: application/json' -d '{"displayName":"dev1"}' | sed 's/.*"accessToken":"\([^"]*\)".*/\1/')
curl -X POST http://localhost:8080/api/v1/commands/place-building -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d "{\"requestId\":\"$(uuidgen)\",\"buildingTypeId\":\"house\",\"x\":3,\"y\":3}"
```
The tile at column 3, row 3 shows a low flat block while the Cottage is under construction, and a small cream box with a red pitched roof once it is built. Other `buildingTypeId` values: `woodcutter` (low box and a log pile), `sawmill` (long box with a wheel), `trading_post` (box with a red awning). The Console shows no red messages.

The build menu (M0-45): a green "Build" button sits at the bottom left. Click it: a dark sheet opens above it with one card per building type, side by side (scroll it sideways if they do not fit), and the button reads "Close". Each card shows the name, the cost, the build time (such as "2m"), the most workers and what it makes, for example "2 Logs → 1 Planks · 60/h at 3 workers" for the Sawmill. The Trading Post is dimmed and says "Requires: Trade Charter"; clicking it does nothing. Click another card: its border turns gold (click it again to clear it). With 700 coins every card is affordable and has no orange warning line. Clicking a card or the button must not mark a tile on the ground behind it. 
Placing a building (M0-46): click the **Cottage** card. The sheet closes, every tile turns bright green (free) and a bar appears at the bottom right: "Cottage: tap a tile to build on" with **Cancel** (and a dimmed **Place**). Click a tile: its border turns orange and the bar asks "Build Cottage at (x, y)?". Click **Place**: the bar reads "Building Cottage at (x, y)…" with both buttons dimmed, a see-through Cottage stands on the tile for a moment, and then the real building (a low foundation, as in M0-44) takes its place, the green goes and the bar disappears; the coins on the status line drop by the Cottage's cost only when the server's answer arrives, not before. Try the failures: place a second building on the same tile; its tile is red once the first exists, and the bar warns "This tile is occupied". Press **Place** anyway: the bar shows `TILE_OCCUPIED: …` in red, the ghost is gone, the card stays picked and no new building appears. Try **Cancel** after picking a card, and after picking a tile: the green disappears and nothing is sent (the API's terminal shows no `place-building` line). Clicking the bar's buttons or the card must not mark a tile behind them.

The local server can also be started and stopped from a small menu, `server-panel/README.md`.

**4. Stop the server and see Offline.**
1. In the API's terminal press **Ctrl+C**.
2. Click the Game view in Unity and leave it in front. The game runs only while the Editor has focus (`Run In Background` is off).
3. Wait up to 2 minutes for the next poll. The label reads:

> Offline: cannot reach the server (CLIENT_NETWORK_ERROR)

**5. Start the server and see it reconnect.**
1. In the API's terminal run `dotnet run --project server/src/CityBuilder.Api` again.
2. Wait until `curl http://localhost:8080/health` answers `Healthy`.
3. Click the Game view again and wait up to 2 minutes. The label reads "Connected - Early Middle Ages - Coins: 700" again.

**6. Finish.** Press **Play** to stop the game. Stop the API with **Ctrl+C**. The database can keep running.

## Why the wait is up to 2 minutes
The client asks for the city every 120 s while it is in the foreground, and it has no faster retry while offline (`core-instructions/09-unity-client.md` → Polling).
Coming back to the app also asks at once, so the label can change as soon as you click the Editor.

## If a step goes wrong
| What you see | What it means | What to do |
|---|---|---|
| The API stops with "Auth:SigningKey must hold the token signing key" | The secret isn't set | Set it (What you need) |
| The API stops with "ConnectionStrings:CityBuilder must hold…" or an `NpgsqlException` | The secret is missing, or the database isn't running or doesn't match `.env` | `docs/getting-started.md` → Database and Troubleshooting |
| "Not connected: Cannot reach the server (CLIENT_NETWORK_ERROR)" after Play | The API wasn't running when you pressed Play. A failed start doesn't retry | Start the API, then press Play twice: stop, then start |
| The coins aren't 700 | The dev player `dev1` has played. Its city carries over from run to run | Reset the city (below) |
| The label doesn't change after 2 minutes | The Editor didn't have focus, or the game is paused | Click the Game view and check that the Pause button isn't on |
| A red message in the Console, or another label | Not a known state | Copy the label and the first red message into the chat with the AI coder |

A token kept from an earlier run that the server no longer accepts (a new signing key, a reset database) isn't a problem: the client logs in again by itself.

## Reset the city
The client plays as the dev player named `devDisplayName` in `client/CityBuilder/Assets/_Project/Resources/Config/ApiClientSettings.asset` (`dev1`). To get the
starter city back, stop the game and delete the player, which removes its city, ledger and receipts. The next Play creates them again:
```
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' <<'SQL'
begin;
delete from ledger_entries where city_id in (select c.id from cities c join players p on p.id = c.player_id where p.dev_name = 'dev1');
delete from command_receipts where player_id in (select id from players where dev_name = 'dev1');
delete from cities where player_id in (select id from players where dev_name = 'dev1');
delete from players where dev_name = 'dev1';
commit;
SQL
```
This deletes data: run it only for the dev player, and only on your development database.
