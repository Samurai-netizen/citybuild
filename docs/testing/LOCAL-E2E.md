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

**3. Check the start.** The dark label at the bottom left of the Game view reads, within a few seconds:

> Connected - Early Middle Ages - Coins: 700

Behind the label the Game view is dark blue-grey, and a green chessboard of 8 by 8 tiles shows in a diamond (the city's ground, M0-43). With the server running, a dark HUD across the top shows a chip for Coins, Logs and Planks (stock, cap and rate such as `0/h`), workers `4 / 4` and builders `0 / 2` (each with its picture), `No research`, the era `Early Middle Ages` and a green dot with `Online` (M0-49). The Console has no red messages. Try the controls: hold the left mouse button and drag to move the view (it stops at the edge of the grid), turn the mouse wheel to zoom, move the pointer to mark a tile in yellow, and click a tile to mark it in orange (click it again to clear it).

A new city has no buildings, so the ground is empty. To see the building views (M0-44), place a Cottage from the build menu (the placement flow below, M0-46), or, to test the views alone, with the command line (do it before pressing Play, or press Play again afterwards: the client asks for the city every 120 s, so a wait of up to two minutes shows it too):
```
TOKEN=$(curl -s -X POST http://localhost:8080/api/v1/auth/dev-login -H 'Content-Type: application/json' -d '{"displayName":"dev1"}' | sed 's/.*"accessToken":"\([^"]*\)".*/\1/')
curl -X POST http://localhost:8080/api/v1/commands/place-building -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d "{\"requestId\":\"$(uuidgen)\",\"buildingTypeId\":\"house\",\"x\":3,\"y\":3}"
```
The tile at column 3, row 3 shows the construction site (a foundation with a frame, a scaffold and a red flag) while the Cottage is under construction, and the half-timbered Cottage with its thatched roof once it is built (CR-0005-T3). Other `buildingTypeId` values: `woodcutter` (a log cabin with a log pile), `sawmill` (a plank shed with a water wheel), `trading_post` (a shop with a striped awning). The ground is a field of grass tiles. The Console shows no red messages.

The build menu (M0-45): a green "Build" button sits at the bottom left. Click it: a dark sheet opens above it with one card per building type, side by side (scroll it sideways if they do not fit), and the button reads "Close". Each card shows the name, the cost, the build time (such as "2m"), the most workers and what it makes, for example "2 Logs → 1 Planks · 60/h at 3 workers" for the Sawmill. The Trading Post is dimmed and says "Requires: Trade Charter"; clicking it does nothing. Click another card: its border turns gold (click it again to clear it). With 700 coins every card is affordable and has no orange warning line. Clicking a card or the button must not mark a tile on the ground behind it. 
Placing a building (M0-46): click the **Cottage** card. The sheet closes, every tile turns bright green (free) and a bar appears at the bottom right: "Cottage: tap a tile to build on" with **Cancel** (and a dimmed **Place**). Click a tile: its border turns orange and the bar asks "Build Cottage at (x, y)?". Click **Place**: the bar reads "Building Cottage at (x, y)…" with both buttons dimmed, a see-through Cottage stands on the tile for a moment, and then the real building (the construction site, CR-0005-T3) takes its place, the green goes, the bar disappears and a green toast "Construction started" shows at the bottom left for a moment (M0-47); the coins on the status line drop by the Cottage's cost only when the server's answer arrives, not before. Try the refusals. A tile with a building is red: pick the Cottage again and click that tile; the bar warns "This tile is occupied" and **Place** stays dimmed, so nothing can be sent (the API's terminal shows no `place-building` line). For a refusal from the server, place two more buildings quickly so both builders are busy (the Cottage takes 30 s) and place a third: the card shows "All builders are busy" but may still be picked, and **Place** on a free tile gives a red toast at the bottom left and a line in the bar that both read "All builders are busy" (no code on the screen), the ghost is gone, the card stays picked and no new building appears. Try **Cancel** after picking a card, and after picking a tile: the green disappears and nothing is sent (the API's terminal shows no `place-building` line). Clicking the bar's buttons or the card must not mark a tile behind them.

The local server can also be started and stopped from a small menu, `server-panel/README.md`. The same menu adds or removes goods on the city of the player the client plays as (`dev1`) (option 6, or `bash server-panel/panel.sh goods add planks 50`), for example to have the stock a step of this check needs.

**4. Stop the server and see Offline.**
1. In the API's terminal press **Ctrl+C**.
2. Click the Game view in Unity and leave it in front. The game runs only while the Editor has focus (`Run In Background` is off).
3. Wait up to 2 minutes for the next poll. The label reads:

> Offline: cannot reach the server (CLIENT_NETWORK_ERROR)

From M0-55 the HUD's dot also turns red with `Offline` and a banner appears under the HUD: "Offline. The city shown may be out of date.", "Last synced 3m ago" and a **Try again** button. The city stays on screen, and the buttons that send a command (Place, − and +, Upgrade, Finance) are dimmed with "You are offline". Press **Try again** with the server still stopped: the button reads "Trying…" and comes back after a moment, the banner stays.

**5. Start the server and see it reconnect.**
1. In the API's terminal run `dotnet run --project server/src/CityBuilder.Api` again.
2. Wait until `curl http://localhost:8080/health` answers `Healthy`.
3. Click the Game view again and wait up to 2 minutes. The label reads "Connected - Early Middle Ages - Coins: 700" again, the banner is gone and the command buttons work again.

**6. Finish.** Press **Play** to stop the game. Stop the API with **Ctrl+C**. The database can keep running.

## The waiting loop at 60 times the speed (M0-54)
A long play, to see what happens while the player waits: the counters, rings, stall bubbles, the stepper, the research ring and the welcome-back report. It takes about 25 minutes. At this speed a city hour is a real minute, so the times in `core-instructions/23-mvp-content.md` go by 60 times as fast: a Cottage takes half a second, and Trade Charter 10 seconds.

**1. Start with an empty database at the fast clock.** Stop the game and the API. This deletes every city on your development database, because a city does not survive a change of the scale (`docs/getting-started.md` → Configuration):
```
docker compose down -v
docker compose up -d postgres
dotnet run --project server/src/CityBuilder.Api -- --Simulation:DevTimeScale=60
```
The API logs a warning about the time scale. Press **Play** in Unity and keep the Game view in front. The game stops running when the Editor loses focus. The HUD shows Coins 700, Logs 40, Planks 10.

**2. Golden scenario #1.** Act quickly; the exact moments cannot be hit at this speed, so look at the behaviour, not at exact numbers after the first line.
1. Research **Trade Charter** and build two **Cottages**. Coins read 450 once the answers are in.
2. Build a **Woodcutter's Hut** and a **Sawmill** when the Cottages stand. Coins 220, Logs 10. Each gets its workers by itself when it is built (pips under the ring: 2 and 3), and Workers reads 3 / 8.
3. The research ring fills in about 10 seconds and then reads `No research`. Open the research screen: Trade Charter is done.
4. When Planks reach 20, build a **Trading Post**. Coins 20, Planks 0.
5. Watch for a minute: Coins rise by 10 about every 2 seconds, and the Trading Post switches between working and the bubble for missing Planks.

**3. Continue: research, workers, upgrade.**
1. When Coins reach 250 (about a minute, or add them: `bash server-panel/panel.sh goods add coins 500`), research **Sharpened Axes**. The ring takes 30 seconds, then reads `No research`. Open the Woodcutter's inspector: it now says `Makes 150 Logs/h` at 2 workers (120 before).
2. Open the **Sawmill**. Press − once: the stepper reads 2 / 3 at once, `Updating workers…` shows briefly, and the ring goes on from where it was. Press − twice quickly, then + once: the value moves with each press and settles without jumping back.
3. Set the Sawmill to 0: its ring stops where it is, the no-workers bubble shows, and `Next batch in` disappears. Set it back to 3: the ring goes on from the same place.
4. Set the **Trading Post** to 1 worker. Workers goes up by one.
5. Let the Logs reach 30, with Coins at 120 or more. Open a **Cottage** and press **Upgrade**. The building site shows for about 2 seconds, then the Cottage returns at level 2 and Workers provided goes from 8 to 10.

**4. Welcome-back.** Click another program and leave Unity in the background for at least 6 minutes. Click the Game view: the report opens once, with about 6 h away and what was made. Press Continue. It must not open again while you play. A second time, stay away for at least 9 minutes: the report also says `Production paused after 8 h`.

**5. A token that runs out.** Tokens last 12 real minutes at this speed. If a command then shows the toast `Session expired. Try again in a moment`, the next try a moment later must work, with no `Signed out` staying on the HUD.

**6. What to report.** Note anything that looks wrong in steps 2 to 5, and where:
- a counter that goes down and back up without a reason, especially when a snapshot arrives (every 2 minutes, after each command, after coming back);
- a ring that starts over after a worker change;
- a bubble or status sentence that stays when it no longer applies;
- a stepper value that flickers or jumps back;
- a research ring that is wrong after the research ends;
- a welcome-back report that opens at the wrong time or says something wrong.

The game also checks itself in the Editor and in development builds (`PredictionWatch`): a warning in the Console starting with `PredictionWatch:` says the clock went back or the snapshot differs from the prediction, with both cities as JSON. Show them all with:
```
grep -n -A3 'PredictionWatch:' ~/Library/Logs/Unity/Editor.log
```
Adding goods with the panel changes the city behind the client's back, so one mismatch right after it is expected.

**7. Back to normal.** Press **Play** to stop the game and stop the API with **Ctrl+C**. Then reset the database, because the clock goes back to the normal scale: `docker compose down -v`, `docker compose up -d postgres`.

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
