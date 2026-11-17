# Unity smoke test

The one manual run that says "Era I works in Unity against the real server" before M0 closes (M0-72, run in M0-76). It walks the whole
loop at 60 times the speed: login, golden scenario #1, workers, all six research nodes, upgrades, a server restart, closing and opening
the game, the server going away and coming back. Each step has an id (S1 … S12), what to do and what must happen; the results go into
the table at the end. It takes about 45 minutes.

It does not repeat the detailed steps of the other checks; it points at them:
- `LOCAL-E2E.md`: the start, the build menu and placement, offline and reconnect, the waiting loop at 60 times the speed;
- `LOCAL-E2E-LOGISTICS.md`: stores, trips, couriers, routes, roads, pause, re-energize;
- `KILL-SERVER-CHECK.md`: the server killed in the middle of a command.

Pressing Play is a manual step: an AI coder can't use the Editor (`core-instructions/09-unity-client.md`). The coder guides, the owner
plays and reports.

## Before you start
- What `LOCAL-E2E.md` → "What you need" lists (Docker, `.env`, the API's two secrets, Unity 6000.3.25f1).
- The Unity Editor shows the Game view; keep it in front, the game runs only while the Editor has focus.
- A terminal for the API and one for commands, both at the repository root.

## How to report
For every step: **pass**, or **fail** with what you saw, where and when (the step id, the building, the number on screen). Copy red
Console lines and any `PredictionWatch:` warnings (`LOCAL-E2E.md` → step 6 of the waiting loop says how to find them). The coder files
every problem in the task's build log with a priority:

| Priority | Means |
|---|---|
| P0 | The game won't open, build or log in |
| P1 | The core loop is broken: a building, worker change, research, delivery or upgrade can't be done, or state is lost or wrong |
| P2 | Wrong but recoverable: a wrong label or number that the next snapshot fixes, a retry that works |
| P3 | Polish: layout, wording, a slow animation |

## The steps

**S1. Fresh database at the fast clock.** This deletes every city on the development database.
```
docker compose down -v
docker compose up -d postgres
dotnet run --project server/src/CityBuilder.Api -- --Simulation:DevTimeScale=60
```
Expected: the API logs a warning about the time scale; `curl http://localhost:8080/health` answers `Healthy`.

**S2. Login.** Open `Assets/Scenes/Bootstrap.unity`, press **Play**.
Expected: within a few seconds the HUD shows Coins 800, workers `4 / 4`, builders `0 / 2`, `No research`, `Early Middle Ages` and a
green `Online`; the 8 × 8 ground; no red Console line (`LOCAL-E2E.md` step 3).

**S3. Golden scenario #1.** Play steps 2.1 to 2.5 of the waiting loop in `LOCAL-E2E.md`: Cottage + Woodcutter's Hut, Scriptorium,
Trade Charter, Sawmill + second Cottage, logs sent to the Sawmill, Trading Post fed with planks.
Expected: coins 670 after the first two buildings, 220 and then 20 as listed there; the Trading Post unlocks when Trade Charter ends;
coins rise by 10 per batch of planks sold. Add coins whenever a later step needs them: `bash server-panel/panel.sh goods add coins 500`.

**S4. Worker reassignment.** Steps 3.2 to 3.4 of the waiting loop in `LOCAL-E2E.md`.
Expected: the stepper moves with each press and doesn't jump back; at 0 workers the ring stops and the no-workers bubble shows; back at
3 the ring goes on from the same place; free workers on the HUD always match.

**S5. Research: Sharpened Axes, then Frame Saw.** Start Sharpened Axes (250 coins, 30 s). When it ends start Frame Saw (400 coins and
30 Planks; it needs Sharpened Axes; 60 s).
Expected: Frame Saw is locked until Sharpened Axes ends; the Woodcutter makes 150 Logs/h at 2 workers after Sharpened Axes (120 before),
the Sawmill needs fewer workers' time after Frame Saw (its inspector's rate goes up); the ring reads `No research` after each.

**S6. Research: Storehouses, Tally Sticks, Sturdy Boots.** One after another (each needs Trade Charter; Storehouses costs 50 Logs,
Sturdy Boots 20 Logs, so the ring waits until they are delivered: `LOCAL-E2E-LOGISTICS.md` step 3).
Expected: Storehouses raises the logs and planks store limits by 40 (the stores' gauges show the bigger limit); Tally Sticks offers
`Half` and `Quarter` in the trip amount; Sturdy Boots makes walkers faster. Only one research runs at a time: starting a second shows
the refusal toast.

**S7. Upgrades.** Upgrade a Cottage (step 3.5 of the waiting loop in `LOCAL-E2E.md`), then the Woodcutter's Hut.
Expected: coins go at once, the site waits for its goods, then the building comes back one level higher (the Cottage: workers provided
+2); an upgrade at max level is not offered.

**S8. Logistics.** `LOCAL-E2E-LOGISTICS.md` steps 4 to 6: a courier station, a route on a road, pause and resume.
Expected: as written there.

**S9. Server restart.** In the API's terminal press **Ctrl+C**, start the API again with the same command as S1 (the same scale).
Click the Game view.
Expected: within 2 minutes the HUD is `Online` again with the same city: the same buildings, workers, research and about the same
stock (production went on while the server was down, because the city settles to the server's time when it next loads).

**S10. Close and reopen the game.** Press **Play** to stop, wait one minute (an hour at this speed), press **Play** again.
Expected: the same player and city come back (no new city with 800 coins); the welcome-back report opens once with about 1 h away and
what was made; Continue closes it and it doesn't open again while you play.

**S11. A long absence.** Click another program for at least 10 minutes, then the Game view.
Expected: `LOCAL-E2E.md` waiting loop step 4 and `LOCAL-E2E-LOGISTICS.md` step 7: one report with about 10 h away; couriers that
worked their stamina rest (there is no 8 h production cap since CR-0008); **Re-energize couriers** works once.

**S12. Offline and reconnect.** `LOCAL-E2E.md` steps 4 and 5, with the API at the fast clock.
Expected: red `Offline`, the banner and dimmed command buttons; **Try again** comes back; after the restart the banner goes and the
buttons work.

**Finish.** Stop the game and the API, then reset the database (the next run uses the normal clock): `docker compose down -v`,
`docker compose up -d postgres`.

## Results
The last run, filled in by M0-76. Issues are in the build log entry of the run.

| Step | Result | Issue |
|---|---|---|
| S1 … S12 | not run yet | — |
