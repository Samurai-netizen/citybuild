# Server panel

A small menu for starting and stopping the local database and API while you test the Unity client (`docs/testing/LOCAL-E2E.md`).

- **Open the menu:** double-click `Server Panel.command` (or run `bash server-panel/panel.sh`).
- **One command instead:** `bash server-panel/panel.sh start|stop|restart|status|logs|stop-all`.
- The API runs in the background, so you can close the Terminal window and it keeps running. Its log is `server-panel/.run/api.log`.
- Needs Docker Desktop running, `.env` (from `.env.example`) and the two API secrets (`LOCAL-E2E.md` → What you need).
- Another port: `PORT=9090 bash server-panel/panel.sh start` (the Unity client's settings must match).
