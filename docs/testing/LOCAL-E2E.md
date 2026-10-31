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

The Console has no red messages. "No cameras rendering" in the Game view is expected: the scene has no camera until M0-43.

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
