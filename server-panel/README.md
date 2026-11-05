# Server panel

A small menu for starting and stopping the local database and API while you test the Unity client (`docs/testing/LOCAL-E2E.md`).

- **Open the menu:** double-click `Server Panel.command` (or run `bash server-panel/panel.sh`).
- **One command instead:** `bash server-panel/panel.sh start|stop|restart|status|logs|stop-all`.
- The API runs in the background, so you can close the Terminal window and it keeps running. Its log is `server-panel/.run/api.log`.
- Needs Docker Desktop running, `.env` (from `.env.example`) and the two API secrets (`LOCAL-E2E.md` → What you need).
- Another port: `PORT=9090 bash server-panel/panel.sh start` (the Unity client's settings must match).

## Goods for the test account
Adds or removes coins or goods on the city of the player the Unity client plays as (the `devDisplayName` of `client/CityBuilder/Assets/_Project/Resources/Config/ApiClientSettings.asset`, `dev1`), for example to have planks in a warehouse for a build or an empty store. Since CR-0008 coins are the city's purse and every good lives in a building's store, so a good needs the id of the building that gets it (CR-0008-T1 decision 35).
- **Menu:** option 6, "Goods". It shows the purse and every store (`building <id> <type>`, item, quantity), then asks for an item (the list comes from `server/content/mvp-content.json`), `a` add, `r` remove or `s` set, an amount and, for a good, the building id.
- **One command instead:** `bash server-panel/panel.sh goods show` · `goods add planks 50 3` (into building 3's store) · `goods remove logs 10 2` · `goods set coins 0` (the purse; no building).
- A remove larger than the stock leaves 0. The script does not check the building's limits or that its level stores the good: goods above a limit stay (a limit only stops production and deliveries), and a good the building doesn't hold stays until a trip takes it.
- Another player: `PLAYER=devtest bash server-panel/panel.sh goods show` (any valid dev name). Every run prints `Player: <name>` first.
- Needs the database running and that player's city to exist (log in once with the Unity client or `docs/testing/LOCAL-E2E.md`). Needs `python3` to read the item ids.
- How it writes: `goods.sql` runs `psql` inside the database container, in one transaction. It locks the city, changes the purse or the store, adds a `DEV_ADJUST` row to the ledger (its balance the city's new total of the item) and raises `state_version`, so the running API and the client pick the change up at the next request or poll. No API restart. It touches no other player and the API has no endpoint for it.
- Tests: `bash server-panel/test-goods.sh` (arguments, no database) and `GoodsPanelScriptTests` (the SQL on PostgreSQL, `docs/testing/README.md`).
